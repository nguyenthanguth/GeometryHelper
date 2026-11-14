using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// TEXTALIGN (TA), as AutoCAD 2021's (its prompts and settings probed in its console): the TEXT and MTEXT selected moved
    /// so that an edge, a middle or a corner of the box each is drawn in (the alignment) lies on that of the text picked, or on
    /// a line of two points, the way the spacing mode says: Current Vertical keeps each text's height and moves it across,
    /// Current Horizontal keeps its place along and moves it up or down, Set spacing stacks the texts a gap apart above and
    /// below the one picked, Distribute spaces them evenly between the top and the bottom one. The alignment and the spacing
    /// mode are kept for the next run, as AutoCAD keeps them in TEXTALIGNMODE, TEXTALIGNSPACING and TEXTALIGNSPACINGVALUE.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] TextAlignNames =
        {
            "Left",
            "Center",
            "Right",
            "TL",
            "TC",
            "TR",
            "ML",
            "MC",
            "MR",
            "BL",
            "BC",
            "BR",
        };

        // Distribute 0, Set spacing 1, current Vertical 2 (the default), current Horizontal 3.
        private static readonly string[] TextAlignModes =
        {
            "Distribute",
            "Set spacing",
            "current Vertical",
            "current Horizontal",
        };

        private static IEnumerable<CadPrompt> TextAlign(CadCommandContext c)
        {
            c.Write(TextAlignSettings(c));
            List<string> texts;

            while (true)
            {
                yield return CadPrompt
                    .ForSelection("Select text objects to align", new[] { "alIgnment", "Options" })
                    .WithoutOr();

                if (c.Input.Is("alIgnment"))
                {
                    foreach (CadPrompt prompt in TextAlignChooseAlignment(c))
                    {
                        yield return prompt;
                    }

                    c.Write(TextAlignSettings(c));
                    continue;
                }

                if (c.Input.Is("Options"))
                {
                    foreach (CadPrompt prompt in TextAlignChooseMode(c))
                    {
                        yield return prompt;
                    }

                    c.Write(TextAlignSettings(c));
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

            DisplayBox? reference = null;
            string referenceHandle = null;
            GeoPoint2? first = null;
            GeoPoint2? second = null;

            while (true)
            {
                yield return CadPrompt.ForEntity("Select text object to align to", new[] { "Point" }).WithoutOr();

                if (c.Input.Is("Point"))
                {
                    yield return CadPrompt.ForPoint("Pick first point");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    first = c.Input.Point;
                    c.Write("Spacing mode: " + TextAlignMode(c));

                    while (true)
                    {
                        yield return CadPrompt.ForPoint("Pick second point", first, new[] { "Options" });

                        if (c.Input.Is("Options"))
                        {
                            foreach (CadPrompt prompt in TextAlignChooseMode(c))
                            {
                                yield return prompt;
                            }

                            c.Write(TextAlignSettings(c));
                            continue;
                        }

                        if (c.Input.IsPoint)
                        {
                            second = c.Input.Point;
                        }

                        break;
                    }

                    if (!second.HasValue)
                    {
                        yield break;
                    }

                    break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                string handle = c.Input.Handles[0];

                if (!IsText(c.Document.FindEntity(handle)))
                {
                    c.Write("Selected object is not a text object.");
                    continue;
                }

                DisplayBox box = TextBoxes(c, new[] { handle })[handle];

                if (box.IsEmpty)
                {
                    c.Write("Selected object is not a text object.");
                    continue;
                }

                reference = box;
                referenceHandle = handle;
                break;
            }

            IReadOnlyDictionary<string, DisplayBox> boxes = TextBoxes(c, texts);
            Dictionary<string, GeoVector2> moves = TextAlignMoves(
                texts.Where(t => !boxes[t].IsEmpty).ToList(),
                boxes,
                reference,
                referenceHandle,
                first,
                second,
                (int)c.Remembered("textalign.align", 0.0),
                (int)c.Remembered("textalign.mode", 2.0),
                c.Remembered("textalign.spacing", 0.0),
                out bool discarded
            );

            if (discarded)
            {
                c.Write(
                    "The base line is near horizontal or vertical, aligned texts may be out of available area, alignment is discarded."
                );
                yield break;
            }

            MoveEach(c, moves);
        }

        /// <summary>Whether an object is a TEXT or an MTEXT.</summary>
        private static bool IsText(DxfEntity entity) => entity is DxfDBText || entity is DxfMText;

        /// <summary>The TEXT and MTEXT among the handles not on a locked layer, saying how many were not texts and how many were locked.</summary>
        private static List<string> TextsOf(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var texts = new List<string>();
            int others = 0;
            int locked = 0;

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity == null)
                {
                    continue;
                }

                if (!IsText(entity))
                {
                    others++;
                }
                else if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    locked++;
                }
                else
                {
                    texts.Add(handle);
                }
            }

            if (others > 0)
            {
                c.Write(
                    others.ToString(CultureInfo.InvariantCulture) + (others == 1 ? " was" : " were") + " filtered out."
                );
            }

            if (locked > 0)
            {
                c.Write(
                    locked.ToString(CultureInfo.InvariantCulture)
                        + (locked == 1 ? " was" : " were")
                        + " on a locked layer."
                );
            }

            return texts;
        }

        /// <summary>The box each text is drawn in, as the display of its space has it; an empty box for a text drawn nowhere.</summary>
        private static IReadOnlyDictionary<string, DisplayBox> TextBoxes(
            CadCommandContext c,
            IEnumerable<string> handles
        )
        {
            var boxes = new Dictionary<string, DisplayBox>(StringComparer.OrdinalIgnoreCase);
            var wanted = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);
            var spaces = new List<CadSpace>();

            foreach (string handle in wanted.OrderBy(h => h, StringComparer.OrdinalIgnoreCase))
            {
                boxes[handle] = DisplayBox.Empty;
                CadSpace space = c.Document.FindSpace(handle);

                if (space != null && !spaces.Contains(space))
                {
                    spaces.Add(space);
                }
            }

            foreach (CadSpace space in spaces)
            {
                foreach (DisplayItem2 item in c.Document.GetDisplay(space).Items)
                {
                    if (item.Handle != null && wanted.Contains(item.Handle))
                    {
                        boxes[item.Handle] = boxes[item.Handle].Union(item.Bounds);
                    }
                }
            }

            return boxes;
        }

        private static IEnumerable<CadPrompt> TextAlignChooseAlignment(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Select alignment direction",
                TextAlignNames,
                TextAlignNames[(int)c.Remembered("textalign.align", 0.0)]
            );

            int chosen = Array.IndexOf(TextAlignNames, c.Input.Text);

            if (chosen >= 0)
            {
                c.Remember("textalign.align", chosen);
            }
        }

        private static IEnumerable<CadPrompt> TextAlignChooseMode(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Enter an option",
                TextAlignModes,
                TextAlignModes[(int)c.Remembered("textalign.mode", 2.0)]
            );

            int chosen = Array.IndexOf(TextAlignModes, c.Input.Text);

            if (chosen < 0)
            {
                yield break;
            }

            c.Remember("textalign.mode", chosen);

            if (chosen == 1)
            {
                yield return CadPrompt.ForDistance(
                    "Set spacing",
                    null,
                    null,
                    c.Remembered("textalign.spacing", 0.0).ToString("0.000000", CultureInfo.InvariantCulture)
                );

                if (c.Input.Kind == CadInputKind.Number)
                {
                    c.Remember("textalign.spacing", c.Input.Number);
                }
            }
        }

        private static string TextAlignMode(CadCommandContext c)
        {
            switch ((int)c.Remembered("textalign.mode", 2.0))
            {
                case 0:
                    return "Distribute evenly";
                case 1:
                    return "Set spacing ("
                        + c.Remembered("textalign.spacing", 0.0).ToString("0.000000", CultureInfo.InvariantCulture)
                        + ")";
                case 3:
                    return "Current Horizontal";
                default:
                    return "Current Vertical";
            }
        }

        private static string TextAlignSettings(CadCommandContext c) =>
            "Current settings: Align = "
            + TextAlignNames[(int)c.Remembered("textalign.align", 0.0)]
            + ", Spacing Mode = "
            + TextAlignMode(c);

        /// <summary>What the alignment names of a box: across (0 left, 1 centre, 2 right) and up (0 top, 1 middle, 2 bottom); Left, Center and Right stand at the middle.</summary>
        private static void TextAlignParts(int align, out int across, out int up)
        {
            if (align < 3)
            {
                across = align;
                up = 1;
                return;
            }

            across = (align - 3) % 3;
            up = (align - 3) / 3;
        }

        private static double AnchorX(DisplayBox box, int across) =>
            across == 0 ? box.MinX
            : across == 1 ? (box.MinX + box.MaxX) / 2.0
            : box.MaxX;

        private static double AnchorY(DisplayBox box, int up) =>
            up == 0 ? box.MaxY
            : up == 1 ? (box.MinY + box.MaxY) / 2.0
            : box.MinY;

        /// <summary>
        /// How far each text moves, as <see cref="TextAlign"/> aligns them: to the box of a text, or to the line through two
        /// points. Texts are put in order from the top by the middle of their boxes, the handle breaking a tie. A line that
        /// cannot be reached by the way the mode moves (a near horizontal one by moving across, a near vertical one by moving
        /// up or down, within a degree) discards the alignment.
        /// </summary>
        private static Dictionary<string, GeoVector2> TextAlignMoves(
            IReadOnlyList<string> handles,
            IReadOnlyDictionary<string, DisplayBox> boxes,
            DisplayBox? reference,
            string referenceHandle,
            GeoPoint2? first,
            GeoPoint2? second,
            int align,
            int mode,
            double spacing,
            out bool discarded
        )
        {
            discarded = false;
            var moves = new Dictionary<string, GeoVector2>(StringComparer.OrdinalIgnoreCase);
            TextAlignParts(align, out int across, out int up);

            // Along a line of two points AutoCAD 2021 (probed in its console) lays Left and Right on the top corner of each box
            // and Center on its middle, as TL, TR and MC.
            if (first.HasValue && align < 3 && align != 1)
            {
                up = 0;
            }

            double lineDx = 0.0;
            double lineDy = 0.0;

            if (first.HasValue && second.HasValue)
            {
                lineDx = second.Value.X - first.Value.X;
                lineDy = second.Value.Y - first.Value.Y;
                double length = Math.Sqrt(lineDx * lineDx + lineDy * lineDy);
                double flat = mode == 3 ? Math.Abs(lineDx) : Math.Abs(lineDy);

                if (length < 1E-9 || flat <= length * 0.0175)
                {
                    discarded = true;
                    return moves;
                }
            }

            int count = handles.Count;
            var minY = new double[count];
            var maxY = new double[count];
            var shiftY = new double[count];
            int[] order = Enumerable
                .Range(0, count)
                .OrderByDescending(i => (boxes[handles[i]].MinY + boxes[handles[i]].MaxY) / 2.0)
                .ThenBy(i => handles[i], StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int i = 0; i < count; i++)
            {
                minY[i] = boxes[handles[i]].MinY;
                maxY[i] = boxes[handles[i]].MaxY;
            }

            if (mode == 3)
            {
                // Each text keeps its place along and moves up or down to the middle (or edge) of the text picked, or to the line.
                for (int i = 0; i < count; i++)
                {
                    DisplayBox box = boxes[handles[i]];
                    double wanted = reference.HasValue
                        ? AnchorY(reference.Value, up)
                        : first.Value.Y + (AnchorX(box, across) - first.Value.X) * lineDy / lineDx;
                    shiftY[i] = wanted - AnchorY(box, up);
                }
            }
            else if (mode == 1 && count > 0)
            {
                // The text picked stays where it is (the top one where it is not among them); the rest are stacked a gap away.
                int fixedAt = 0;

                for (int k = 0; k < count && referenceHandle != null; k++)
                {
                    if (string.Equals(handles[order[k]], referenceHandle, StringComparison.OrdinalIgnoreCase))
                    {
                        fixedAt = k;
                        break;
                    }
                }

                for (int k = fixedAt - 1; k >= 0; k--)
                {
                    int i = order[k];
                    int below = order[k + 1];
                    double bottom = minY[below] + shiftY[below] + (maxY[below] - minY[below]) + spacing;
                    shiftY[i] = bottom - minY[i];
                }

                for (int k = fixedAt + 1; k < count; k++)
                {
                    int i = order[k];
                    int above = order[k - 1];
                    double top = minY[above] + shiftY[above] - spacing;
                    shiftY[i] = top - maxY[i];
                }
            }
            else if (mode == 0 && count >= 3)
            {
                double total = 0.0;

                for (int i = 0; i < count; i++)
                {
                    total += maxY[i] - minY[i];
                }

                int topText = order[0];
                int bottomText = order[count - 1];
                double gap = (maxY[topText] - minY[bottomText] - total) / (count - 1);
                double cursor = maxY[topText];

                for (int k = 0; k < count; k++)
                {
                    int i = order[k];
                    double height = maxY[i] - minY[i];
                    shiftY[i] = cursor - height - minY[i];
                    cursor -= height + gap;
                }
            }

            for (int i = 0; i < count; i++)
            {
                DisplayBox box = boxes[handles[i]];
                double shift = shiftY[i];
                double across2 = 0.0;

                if (mode != 3)
                {
                    DisplayBox moved = new DisplayBox(box.MinX, box.MinY + shift, box.MaxX, box.MaxY + shift);
                    double wanted = reference.HasValue
                        ? AnchorX(reference.Value, across)
                        : first.Value.X + (AnchorY(moved, up) - first.Value.Y) * lineDx / lineDy;
                    across2 = wanted - AnchorX(box, across);
                }

                if (Math.Abs(across2) > 1E-9 || Math.Abs(shift) > 1E-9)
                {
                    moves[handles[i]] = new GeoVector2(across2, shift);
                }
            }

            return moves;
        }

        /// <summary>Moves each text by its own vector as one edit of the command.</summary>
        private static void MoveEach(CadCommandContext c, IReadOnlyDictionary<string, GeoVector2> moves)
        {
            if (moves.Count == 0)
            {
                return;
            }

            List<string> handles = moves.Keys.OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToList();
            var planned = handles
                .Select(h => new CadEditRecord(
                    CadEditRecordKind.Transformed,
                    h,
                    "Move",
                    null,
                    Values(moves[h].X, moves[h].Y)
                ))
                .ToList();
            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (string handle in handles)
                {
                    if (!s.IsHandled(handle))
                    {
                        s.Document.FindEntity(handle)
                            .TransformBy(
                                GeoTransform3.Translation(new GeoVector3(moves[handle].X, moves[handle].Y, 0.0))
                            );
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
