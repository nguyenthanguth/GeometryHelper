using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The grips of an MTEXT: its location and the corners of its box, as AutoCAD 2021 gives them.</summary>
    public static partial class CadGrips
    {
        private static void MTextGrips(DxfMText text, Action<CadGripKind, GeoPoint2> add)
        {
            var location = new GeoPoint2(text.Location.X, text.Location.Y);

            if (
                !IsPlan(text.Normal)
                || !CadMTextEdit.TryGetGripBox(
                    text,
                    out double left,
                    out double bottom,
                    out double right,
                    out double top
                )
            )
            {
                add(CadGripKind.Insertion, location);
                return;
            }

            CadMTextEdit.Axes(text, out GeoVector2 along, out GeoVector2 up);
            CadMTextEdit.Place(text, out int column, out int row);

            // The location is a corner of the box unless the MTEXT is held at a middle, across or down.
            int held = column == 1 || row == 1 ? -1 : (row == 2 ? 2 : 0) + (column == 2 ? 1 : 0);

            if (held < 0)
            {
                add(CadGripKind.Insertion, location);
            }

            for (int corner = 0; corner < 4; corner++)
            {
                double x = corner % 2 == 0 ? left : right;
                double y = corner < 2 ? top : bottom;
                add(corner == held ? CadGripKind.Insertion : CadGripKind.Corner, location + (along * x) + (up * y));
            }
        }

        /// <summary>
        /// An MTEXT's box with a corner (0 top left, 1 top right, 2 bottom left, 3 bottom right) taken to a point: its width
        /// and defined height the box's, its location at the point of the box its attachment names.
        /// </summary>
        private static CadEditResult ResizeMText(
            CadDocument document,
            DxfMText text,
            string handle,
            string property,
            string was,
            string went,
            int corner,
            GeoPoint2 to
        )
        {
            if (!CadMTextEdit.TryGetGripBox(text, out double left, out double bottom, out double right, out double top))
            {
                return CadEditResult.Refuse("The grip cannot be dragged.");
            }

            CadMTextEdit.Axes(text, out GeoVector2 along, out GeoVector2 up);
            CadMTextEdit.Place(text, out int column, out int row);
            var location = new GeoPoint2(text.Location.X, text.Location.Y);
            GeoVector2 way = to - location;
            double x = (way.X * along.X) + (way.Y * along.Y);
            double y = (way.X * up.X) + (way.Y * up.Y);

            if (corner % 2 == 0)
            {
                left = x;
            }
            else
            {
                right = x;
            }

            double width = right - left;

            if (width <= Tolerance.Default.EqualPoint)
            {
                return CadEditResult.Refuse("An MTEXT needs a box of some width.");
            }

            // A file before R2007 holds no height of the box: the box keeps its top and bottom there.
            bool high = text.Item.File.Supports(1021);
            double height = top - bottom;

            if (high)
            {
                // The box is at least as high as the letters, the edge dragged staying at the point, as AutoCAD's.
                if (corner < 2)
                {
                    top = y;
                    bottom = Math.Min(bottom, top - text.TextHeight);
                }
                else
                {
                    bottom = y;
                    top = Math.Max(top, bottom + text.TextHeight);
                }

                height = top - bottom;
            }

            double atX = left + (column * width / 2.0);
            double atY = top - (row * height / 2.0);
            GeoPoint2 moved = location + (along * atX) + (up * atY);
            return Reshape(document, handle, property, was, went, () =>
            {
                var m = (DxfMText)document.FindEntity(handle);
                m.Width = width;

                if (high)
                {
                    m.Height = height;
                }

                m.Location = new GeoPoint3(moved.X, moved.Y, m.Location.Z);
                return true;
            });
        }
    }
}
