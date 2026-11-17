using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// TXTEXP, the Express Tools command, as AutoCAD 2021's own does it (its source reads it): "Select text to be EXPLODED:"
    /// for TEXT and MTEXT, not the text of a leader; each is replaced by the lines of its letters, "2 text object(s) have been
    /// exploded to lines." and "The line objects have been placed on layer 0." AutoCAD sends the text through a metafile and
    /// takes it back in, which makes polylines; here the letters are made as the display draws them. An SHX font's strokes are
    /// open polylines, one for each stroke of each letter; a TrueType font's letters are closed polylines, one for each outline
    /// of the letter (an O two, the ring and its hole), its curves cut into six pieces each. An MTEXT is broken into the TEXTs
    /// EXPLODE makes of it first. The polylines are on layer 0, ByLayer, in the space of the text. Text in a font whose file is
    /// not found or has no outlines to read (an OpenType file of CFF outlines) is left as it is, said so with the count.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> TextExplode(CadCommandContext c)
        {
            c.Write("Select text to be EXPLODED: ");

            foreach (
                CadPrompt prompt in c.SelectObjectsOf("Select objects", e =>
                    (e is DxfDBText text && text.GetType() == typeof(DxfDBText)) || e is DxfMText
                )
            )
            {
                yield return prompt;
            }

            var styles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var builder = new DisplayBuilder(c.Database, c.Document);
            var parts = new List<KeyValuePair<string, List<DxfEntity>>>();
            int left = 0;

            foreach (string handle in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                // The text of a leader belongs to it, as AutoCAD's filter on a reactor leaves it out.
                if (entity == null || c.Document.Owners.GetEditRule(entity) != CadEditRule.Free)
                {
                    continue;
                }

                IReadOnlyList<DxfEntity> pieces = entity is DxfMText words
                    ? CadMTextEdit.Explode(words, styles)
                    : new[] { entity };
                List<DxfEntity> lines = pieces == null ? null : LinesOfText(builder, pieces);

                if (lines == null)
                {
                    left++;
                    continue;
                }

                parts.Add(new KeyValuePair<string, List<DxfEntity>>(handle, lines));
            }

            if (left > 0)
            {
                c.Write(
                    left.ToString(CultureInfo.InvariantCulture)
                        + " text object(s) could not be exploded: their font has no outlines to read here."
                );
            }

            if (parts.Count == 0)
            {
                yield break;
            }

            var planned = new List<CadEditRecord>();

            foreach (KeyValuePair<string, List<DxfEntity>> pair in parts)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, pair.Key));
            }

            CadEditResult result = c.Apply(planned, s =>
            {
                CadMTextEdit.AddExplodedStyles(s.Database, styles);

                foreach (KeyValuePair<string, List<DxfEntity>> pair in parts)
                {
                    if (s.IsHandled(pair.Key))
                    {
                        continue;
                    }

                    CadSpace space = s.Document.FindSpace(pair.Key);
                    DxfEntity entity = s.Document.FindEntity(pair.Key);

                    foreach (DxfEntity line in pair.Value)
                    {
                        s.Append(space, line);
                    }

                    entity.Erase();
                }
            });

            if (result.Succeeded)
            {
                c.Write(
                    parts.Count.ToString(CultureInfo.InvariantCulture) + " text object(s) have been exploded to lines."
                );
                c.Write("The line objects have been placed on layer 0.");
            }
        }

        /// <summary>
        /// The polylines the letters of some texts draw, and the lines the bars of a stack are, on layer 0; null where a text is of
        /// a TrueType font whose outlines are not read.
        /// </summary>
        private static List<DxfEntity> LinesOfText(DisplayBuilder builder, IReadOnlyList<DxfEntity> pieces)
        {
            var made = new List<DxfEntity>();

            foreach (DxfEntity piece in pieces)
            {
                if (piece is DxfLine bar)
                {
                    var copy = new DxfLine { StartPoint = bar.StartPoint, EndPoint = bar.EndPoint };
                    copy.Layer = "0";
                    made.Add(copy);
                    continue;
                }

                foreach (DisplayItem2 item in builder.BuildLoose(new[] { piece }))
                {
                    if (!(item is DisplayText2 text))
                    {
                        continue;
                    }

                    if (text.Strokes != null)
                    {
                        foreach (double[] stroke in text.Strokes)
                        {
                            AddPolyline(made, stroke, false);
                        }
                    }
                    else
                    {
                        List<double[]> contours = CadTextOutlines.Contours(text);

                        if (contours == null)
                        {
                            return null;
                        }

                        foreach (double[] contour in contours)
                        {
                            AddPolyline(made, contour, true);
                        }
                    }
                }
            }

            return made;
        }

        private static void AddPolyline(List<DxfEntity> into, double[] points, bool closed)
        {
            if (points.Length < 4)
            {
                return;
            }

            var polyline = new DxfPolyline();

            for (int i = 0; i + 1 < points.Length; i += 2)
            {
                polyline.AddVertexAt(
                    i / 2,
                    new GeometryHelper.Geometry.GeoPoint2(points[i], points[i + 1]),
                    0.0,
                    0.0,
                    0.0
                );
            }

            polyline.Closed = closed;
            polyline.Layer = "0";
            into.Add(polyline);
        }
    }
}
