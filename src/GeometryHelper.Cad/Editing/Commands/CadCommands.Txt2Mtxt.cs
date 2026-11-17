using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    internal static partial class CadCommands
    {
        /// <summary>
        /// TXT2MTXT: the TEXT and MTEXT selected combined into one MTEXT ("Select objects or [Settings]:"). The new MTEXT
        /// stands where the top text starts, as high, in the style and on the layer of the first text (the top one, or the
        /// first selected), turned as it is, its top left on the top left of that text; its words are those of every text, each
        /// its own paragraph, or run together into one paragraph, wrapped at the width the settings give (nought, no wrapping),
        /// as the settings say; the originals are erased or kept as the settings say. Settings asks the four in turn, as
        /// AutoCAD 2021's Text to Mtext Options dialog does (the console runs no TXT2MTXT, so the question is ours): "Specify
        /// the width of the MTEXT, 0 for no limit", "Keep each text as a paragraph of its own?", "Erase the original texts?"
        /// and "Order the texts by position or as selected?", which the next run keeps. Four texts 2.5 high
        /// stacked 5 apart come out as an MTEXT of four paragraphs at the top left of the top one.
        /// </summary>
        private static IEnumerable<CadPrompt> TextToMText(CadCommandContext c)
        {
            List<string> texts;

            while (true)
            {
                foreach (
                    CadPrompt asked in c.SelectFirst(CadPrompt.ForSelection("Select objects", new[] { "Settings" }))
                )
                {
                    yield return asked;
                }

                if (c.Input.Is("Settings"))
                {
                    foreach (CadPrompt prompt in TextToMTextSettings(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                texts = TextsOf(c, c.Input.Handles);
                break;
            }

            if (texts.Count == 0)
            {
                yield break;
            }

            bool byPosition = c.Remembered("txt2mtxt.order", 0.0) == 0.0;
            bool paragraphs = c.Remembered("txt2mtxt.paragraphs", 1.0) != 0.0;
            bool erase = c.Remembered("txt2mtxt.erase", 1.0) != 0.0;
            double width = Math.Max(0.0, c.Remembered("txt2mtxt.width", 0.0));
            var parts = new List<TextPart>();

            foreach (string handle in texts)
            {
                DxfEntity entity = c.Document.FindEntity(handle);
                TextFrame frame = FrameOf(c, entity);
                parts.Add(
                    new TextPart
                    {
                        Handle = handle,
                        Entity = entity,
                        Top = frame == null ? double.NegativeInfinity : frame.At(0, 3).Y,
                        Left = frame == null ? double.PositiveInfinity : frame.At(0, 3).X,
                        TopLeft = frame == null ? (GeoPoint2?)null : frame.At(0, 3),
                    }
                );
            }

            // The texts from the top down, a text level with another the left one first, the handle the last say.
            List<TextPart> ordered = byPosition
                ? parts
                    .OrderByDescending(p => Math.Round(p.Top, 6))
                    .ThenBy(p => Math.Round(p.Left, 6))
                    .ThenBy(p => p.Handle, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : parts;
            TextPart first = ordered[0];
            GeoPoint2 corner = first.TopLeft
                ?? (
                    first.Entity is DxfMText m
                        ? new GeoPoint2(m.Location.X, m.Location.Y)
                        : new GeoPoint2(((DxfDBText)first.Entity).Position.X, ((DxfDBText)first.Entity).Position.Y)
                );
            double height = first.Entity is DxfMText firstWords
                ? firstWords.TextHeight
                : ((DxfDBText)first.Entity).Height;
            string style = first.Entity is DxfMText styled
                ? styled.TextStyleName
                : ((DxfDBText)first.Entity).TextStyleName;
            double turn = first.Entity is DxfMText turned ? turned.Rotation : ((DxfDBText)first.Entity).Rotation;
            string layer = first.Entity.Layer;
            string words = string.Join(
                paragraphs ? "\\P" : " ",
                ordered.Select(p => ContentsOf(p.Entity, paragraphs)).Where(w => w.Length > 0)
            );
            var planned = new List<CadEditRecord>();

            if (erase)
            {
                foreach (TextPart part in ordered)
                {
                    planned.Add(new CadEditRecord(CadEditRecordKind.Erased, part.Handle));
                }
            }

            CadSpace space = c.Document.FindSpace(first.Handle) ?? c.Space;
            CadEditResult result = c.Apply(planned, s =>
            {
                if (erase)
                {
                    foreach (TextPart part in ordered)
                    {
                        if (!s.IsHandled(part.Handle))
                        {
                            s.Document.FindEntity(part.Handle).Erase();
                        }
                    }
                }

                var combined = new DxfMText
                {
                    Location = new GeoPoint3(corner.X, corner.Y, 0.0),
                    TextHeight = height > 0.0 ? height : TextSize(c),
                    Width = width,
                    Attachment = DxfAttachmentPoint.TopLeft,
                    Contents = words,
                };

                if (Math.Abs(turn) > 1E-12)
                {
                    combined.Rotation = turn;
                }

                if (!string.IsNullOrEmpty(style))
                {
                    combined.TextStyleName = style;
                }

                s.Append(space, Current(c, combined));
                combined.Layer = layer;
            });

            c.Write(
                result.Succeeded
                    ? ordered.Count.ToString(CultureInfo.InvariantCulture)
                        + (ordered.Count == 1 ? " text object" : " text objects")
                        + " combined into 1 MTEXT."
                    : result.Message
            );
        }

        /// <summary>A text TXT2MTXT combines: its handle, the object, where its top left stands and what the order sorts by.</summary>
        private sealed class TextPart
        {
            public string Handle { get; set; }

            public DxfEntity Entity { get; set; }

            public double Top { get; set; }

            public double Left { get; set; }

            public GeoPoint2? TopLeft { get; set; }
        }

        /// <summary>The words of a text as MTEXT writes them: a backslash and a brace as their codes, an MTEXT's own codes kept; its lines run together by spaces where the paragraphs are not kept.</summary>
        private static string ContentsOf(DxfEntity entity, bool paragraphs)
        {
            if (entity is DxfMText mtext)
            {
                string contents = mtext.Contents ?? string.Empty;
                return paragraphs ? contents : contents.Replace("\\P", " ");
            }

            var plain = new StringBuilder();

            foreach (char letter in ((DxfDBText)entity).TextString)
            {
                if (letter == '\\' || letter == '{' || letter == '}')
                {
                    plain.Append('\\');
                }

                plain.Append(letter);
            }

            return plain.ToString();
        }

        /// <summary>TXT2MTXT's Settings: the width, whether each text is a paragraph, whether the originals are erased, and the order; each kept for the next run.</summary>
        private static IEnumerable<CadPrompt> TextToMTextSettings(CadCommandContext c)
        {
            yield return CadPrompt.ForDistance(
                "Specify the width of the MTEXT, 0 for no limit",
                null,
                null,
                Length(c.Remembered("txt2mtxt.width", 0.0))
            );

            if (c.Input.Kind == CadInputKind.Number && c.Input.Number >= 0.0)
            {
                c.Remember("txt2mtxt.width", c.Input.Number);
            }

            yield return CadPrompt.ForKeyword(
                "Keep each text as a paragraph of its own?",
                new[] { "Yes", "No" },
                c.Remembered("txt2mtxt.paragraphs", 1.0) != 0.0 ? "Yes" : "No"
            );
            c.Remember("txt2mtxt.paragraphs", c.Input.Is("No") ? 0.0 : 1.0);
            yield return CadPrompt.ForKeyword(
                "Erase the original texts?",
                new[] { "Yes", "No" },
                c.Remembered("txt2mtxt.erase", 1.0) != 0.0 ? "Yes" : "No"
            );
            c.Remember("txt2mtxt.erase", c.Input.Is("No") ? 0.0 : 1.0);
            yield return CadPrompt.ForKeyword(
                "Order the texts by position or as selected?",
                new[] { "Position", "Selection" },
                c.Remembered("txt2mtxt.order", 0.0) == 0.0 ? "Position" : "Selection"
            );
            c.Remember("txt2mtxt.order", c.Input.Is("Selection") ? 1.0 : 0.0);
        }
    }
}
