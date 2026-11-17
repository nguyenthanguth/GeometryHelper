using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// SCALETEXT and JUSTIFYTEXT, as AutoCAD 2021's (their prompts and results probed in its console). SCALETEXT scales each
    /// TEXT, MTEXT and attribute selected about its own base point, the one the "Enter a base point option for scaling"
    /// names (Existing, the point the text is held by, or a point of its box: Left, Center, Middle, Right on the baseline and
    /// the middle, TL to BR on the corners of the box), to a new height, to the height of a text picked or by a scale
    /// factor: "HELLO" 5 high at (10, 10), 23.33 wide, taken to a height of 10 about Center stands at (-1.67, 10), its centre
    /// still on (21.67, 10). JUSTIFYTEXT changes the justification of each and keeps it where it stands: the same "HELLO"
    /// justified Right is held by (33.33, 10), the end of its baseline, and drawn where it was. The box of a TEXT is the one
    /// the display draws it in; that of an MTEXT is its actual width and height laid out at its attachment point.
    /// </summary>
    internal static partial class CadCommands
    {
        // The options of both commands as AutoCAD lists them, each with where it holds a box: across (0 left, 1 centre,
        // 2 right) and level (0 baseline, 1 bottom, 2 middle, 3 top).
        private static readonly string[] ScaleTextBases =
        {
            "Existing",
            "Left",
            "Center",
            "Middle",
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

        private static readonly string[] JustifyTextOptions =
        {
            "Left",
            "Align",
            "Fit",
            "Center",
            "Middle",
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

        private static void AddTextToolsTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("SCALETEXT", ScaleText, true));
            registry.Add(new CadCommand("JUSTIFYTEXT", JustifyText, true));
            registry.Add(new CadCommand("TXT2MTXT", TextToMText, true));
        }

        /// <summary>Where an option holds a box: across (0 left, 1 centre, 2 right) and level (0 baseline, 1 bottom, 2 middle, 3 top).</summary>
        private static void TextHold(string option, out int across, out int level)
        {
            switch (option)
            {
                case "Left":
                    across = 0;
                    level = 0;
                    break;
                case "Center":
                    across = 1;
                    level = 0;
                    break;
                case "Right":
                case "Align":
                case "Fit":
                    across = 2;
                    level = 0;
                    break;
                case "Middle":
                    across = 1;
                    level = 2;
                    break;
                default:
                    // TL to BR: the first letter the level (T, M, B), the second the side (L, C, R).
                    level =
                        option[0] == 'T' ? 3
                        : option[0] == 'M' ? 2
                        : 1;
                    across =
                        option[1] == 'L' ? 0
                        : option[1] == 'C' ? 1
                        : 2;
                    break;
            }
        }

        /// <summary>
        /// The box of a text on the page: where its baseline starts (the bottom left corner for an MTEXT), the way along it and
        /// the way up, how wide and high it is and how far the letters reach below the baseline.
        /// </summary>
        private sealed class TextFrame
        {
            public double StartX { get; set; }

            public double StartY { get; set; }

            public double AlongX { get; set; }

            public double AlongY { get; set; }

            public double Width { get; set; }

            public double Height { get; set; }

            public double Descent { get; set; }

            /// <summary>The point of the box an option holds: across 0, 1, 2 of its width, level 0 the baseline, 1 the bottom, 2 the middle, 3 the top.</summary>
            public GeoPoint2 At(int across, int level)
            {
                double x = Width * across / 2.0;
                double y =
                    level == 0 ? 0.0
                    : level == 1 ? -Descent
                    : level == 2 ? Height / 2.0
                    : Height;
                return new GeoPoint2(StartX + AlongX * x - AlongY * y, StartY + AlongY * x + AlongX * y);
            }
        }

        /// <summary>The picture of a TEXT in the display of its space; null where it is drawn nowhere.</summary>
        private static DisplayText2 TextItem(CadCommandContext c, string handle)
        {
            CadSpace space = c.Document.FindSpace(handle);

            if (space == null)
            {
                return null;
            }

            foreach (DisplayItem2 item in c.Document.GetDisplay(space).Items)
            {
                if (item is DisplayText2 text && string.Equals(item.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
        }

        /// <summary>The box of a TEXT as the display draws it, of an MTEXT as it is laid out; null for a text drawn nowhere.</summary>
        private static TextFrame FrameOf(CadCommandContext c, DxfEntity entity)
        {
            if (entity is DxfMText mtext)
            {
                CadMTextLayout layout = c.Document.LayoutOf(mtext);
                GeoVector3 direction = mtext.Direction;
                double length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                double ax = length > 1E-12 ? direction.X / length : 1.0;
                double ay = length > 1E-12 ? direction.Y / length : 0.0;
                int at = (int)mtext.Attachment;
                double across = at >= 1 && at <= 9 ? (at - 1) % 3 / 2.0 : 0.0;
                double up = at >= 1 && at <= 9 ? (2 - (at - 1) / 3) / 2.0 : 1.0;
                double width = layout.ActualWidth;
                double height = layout.ActualHeight;
                double x = -across * width;
                double y = -up * height;
                GeoPoint3 location = mtext.Location;
                return new TextFrame
                {
                    StartX = location.X + ax * x - ay * y,
                    StartY = location.Y + ay * x + ax * y,
                    AlongX = ax,
                    AlongY = ay,
                    Width = width,
                    Height = height,
                };
            }

            DisplayText2 item = TextItem(c, entity.Handle);

            if (item == null)
            {
                return null;
            }

            item.GetFrame(out double x0, out double y0, out double x1, out double y1);

            // The first line's capitals reach up to the height, so the baseline is that far below the top of the box.
            double baseline = y1 - item.Height;
            double cos = Math.Cos(item.Rotation);
            double sin = Math.Sin(item.Rotation);
            return new TextFrame
            {
                StartX = item.X + x0 * cos - baseline * sin,
                StartY = item.Y + x0 * sin + baseline * cos,
                AlongX = cos,
                AlongY = sin,
                Width = x1 - x0,
                Height = item.Height,
                Descent = baseline - y0,
            };
        }
    }
}
