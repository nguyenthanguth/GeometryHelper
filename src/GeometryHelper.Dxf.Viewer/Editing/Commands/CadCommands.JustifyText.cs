using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    internal static partial class CadCommands
    {
        /// <summary>
        /// JUSTIFYTEXT: "Select objects:", then "Enter a justification option [Left/Align/Fit/Center/Middle/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR]
        /// &lt;Left&gt;:". Each text is given the justification and stays where it is drawn: a TEXT keeps its first point and is
        /// held by the point of its box the option names (Right by the end of its baseline, Align and Fit by it too, with the
        /// first point as the other end); an MTEXT is attached by the matching corner of its words, Left, Align and Fit by the
        /// bottom left, Center by the bottom middle, Middle by the centre and Right by the bottom right, as AutoCAD 2021's
        /// console attaches it. The option is kept for the next run.
        /// </summary>
        private static IEnumerable<CadPrompt> JustifyText(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectFirst(CadPrompt.ForSelection(), IsText))
            {
                yield return prompt;
            }

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            List<string> texts = TextsOf(c, c.Input.Handles);

            if (texts.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword(
                "Enter a justification option",
                JustifyTextOptions,
                JustifyTextOptions[(int)c.Remembered("justifytext.option", 0.0)]
            );

            int chosen = Array.IndexOf(JustifyTextOptions, c.Input.Text);

            if (chosen < 0)
            {
                yield break;
            }

            c.Remember("justifytext.option", chosen);
            string option = JustifyTextOptions[chosen];
            TextHold(option, out int across, out int level);
            DxfAttachmentPoint text = TextPoints[Array.IndexOf(DTextJustifications, option)];
            int row = 3 - level;
            int attachment =
                option == "Left" || option == "Align" || option == "Fit" ? 7
                : option == "Center" ? 8
                : option == "Right" ? 9
                : option == "Middle" ? 5
                : row * 3 + across + 1;
            var planned = new List<CadEditRecord>();
            var moves = new List<KeyValuePair<string, Action<DxfEntity>>>();

            // Every box is measured before any text moves.
            foreach (string handle in texts)
            {
                DxfEntity entity = c.Document.FindEntity(handle);
                TextFrame frame = FrameOf(c, entity);

                if (frame == null)
                {
                    continue;
                }

                GeoPoint2 held = frame.At(across, level);
                GeoPoint2 start = frame.At(0, 0);
                planned.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Justify", null, option));
                moves.Add(new KeyValuePair<string, Action<DxfEntity>>(handle, target =>
                    {
                        if (target is DxfMText mtext)
                        {
                            mtext.Attachment = (DxfAttachmentPoint)attachment;
                            mtext.Location = new GeoPoint3(held.X, held.Y, mtext.Location.Z);
                            return;
                        }

                        var dbText = (DxfDBText)target;
                        double z = dbText.Position.Z;
                        dbText.Justify = text;

                        // Left, Align and Fit hold the first point, which is where the words start.
                        if (text == DxfAttachmentPoint.BaseLeft || option == "Align" || option == "Fit")
                        {
                            dbText.Position = new GeoPoint3(start.X, start.Y, z);
                        }

                        if (text != DxfAttachmentPoint.BaseLeft)
                        {
                            dbText.AlignmentPoint = new GeoPoint3(held.X, held.Y, z);
                        }
                    }));
            }

            if (moves.Count == 0)
            {
                yield break;
            }

            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (KeyValuePair<string, Action<DxfEntity>> move in moves)
                {
                    if (!s.IsHandled(move.Key))
                    {
                        move.Value(s.Document.FindEntity(move.Key));
                    }
                }
            });

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }
    }
}
