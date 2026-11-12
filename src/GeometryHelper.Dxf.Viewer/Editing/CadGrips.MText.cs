using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The grips of an MTEXT: its location and the corners of its box, or the grips of its columns, as AutoCAD 2021 gives them.</summary>
    public static partial class CadGrips
    {
        private static void MTextGrips(DxfMText text, Action<CadGripKind, GeoPoint2, GeoVector2> add)
        {
            var location = new GeoPoint2(text.Location.X, text.Location.Y);
            var none = new GeoVector2(0.0, 0.0);

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
                // No box of corners: the grips of its columns where it has some, else its location alone.
                if (IsPlan(text.Normal) && MTextColumns.TryRead(text, out MTextColumns columns))
                {
                    columns.Grips(add);
                }
                else
                {
                    add(CadGripKind.Insertion, location, none);
                }

                return;
            }

            CadMTextEdit.Axes(text, out GeoVector2 along, out GeoVector2 up);
            CadMTextEdit.Place(text, out int column, out int row);

            // The location is a corner of the box unless the MTEXT is held at a middle, across or down.
            int held = column == 1 || row == 1 ? -1 : (row == 2 ? 2 : 0) + (column == 2 ? 1 : 0);

            if (held < 0)
            {
                add(CadGripKind.Insertion, location, none);
            }

            // Drawn as the design draws them: the top left a square, the top right a triangle along the MTEXT, the bottom left
            // one down it, the bottom right one into the corner; the location a square wherever it is.
            for (int corner = 0; corner < 4; corner++)
            {
                double x = corner % 2 == 0 ? left : right;
                double y = corner < 2 ? top : bottom;
                GeoVector2 way =
                    corner == held || corner == 0 ? none
                    : corner == 1 ? along
                    : corner == 2 ? -up
                    : along - up;
                add(
                    corner == held ? CadGripKind.Insertion : CadGripKind.Corner,
                    location + (along * x) + (up * y),
                    way
                );
            }
        }

        /// <summary>
        /// A grip of an MTEXT in columns dragged to a point, as AutoCAD 2021's MoveGripPointsAt does it: the step taken along
        /// the way the grip points, the location kept. The column width grip sets every column's width, static columns keeping
        /// their total; the height grip the height of the columns; a column's own height grip its height; the last column's grip
        /// the gutter, the columns keeping their width; the corner the height and, as the last column's grip, the gutter of
        /// static columns or, as the column width grip, the width of dynamic ones. Widths and heights stop at the text height,
        /// a gutter at nought.
        /// </summary>
        private static CadEditResult DragMTextColumns(
            CadDocument document,
            DxfMText text,
            string handle,
            string property,
            string was,
            string went,
            IReadOnlyList<CadGrip> grips,
            int gripIndex,
            GeoPoint2 to
        )
        {
            if (!MTextColumns.TryRead(text, out MTextColumns columns))
            {
                return CadEditResult.Refuse("The grip cannot be dragged.");
            }

            CadGrip grip = grips[gripIndex];
            GeoVector2 moved = to - grip.Point;
            double across = ((moved.X * columns.Along.X) + (moved.Y * columns.Along.Y)) * columns.AwayX;
            double down = ((moved.X * columns.Up.X) + (moved.Y * columns.Up.Y)) * columns.AwayY;
            double least = text.TextHeight;
            CadMTextFrame frame = CadMTextFrame.Of(text);
            bool fixedCount = frame.ColumnType == DxfColumnType.StaticColumns;
            int count = columns.Count;

            // The gutter that leaves the columns their width with the total moved by a step; nought for one column.
            double Gutter(double step) =>
                count < 2
                    ? frame.Gutter
                    : Math.Max(0.0, (columns.Total + step - (count * frame.ColumnWidth)) / (count - 1));

            switch (grip.Kind)
            {
                case CadGripKind.MTextColumnWidth:
                {
                    double width = Math.Max(least, frame.ColumnWidth + across);

                    if (fixedCount && count > 1)
                    {
                        // Static columns keep their total: the gutter gives or takes the rest, never below nought.
                        width = Math.Min(width, columns.Total / count);
                        frame = frame.WithColumns(
                            frame.ColumnType,
                            width,
                            Math.Max(0.0, (columns.Total - (count * width)) / (count - 1)),
                            frame.ColumnCount,
                            false
                        );
                    }
                    else
                    {
                        frame = frame.WithColumnWidth(width);
                    }

                    break;
                }

                case CadGripKind.MTextGutter:
                    frame = frame.WithGutter(Gutter(across));
                    break;
                case CadGripKind.MTextHeight:
                    frame = frame.WithHeight(Math.Max(least, columns.Height + down));
                    break;
                case CadGripKind.MTextSize:
                    frame = fixedCount
                        ? frame.WithGutter(Gutter(across))
                        : frame.WithColumnWidth(Math.Max(least, frame.ColumnWidth + across));
                    frame = frame.WithHeight(Math.Max(least, columns.Height + down));
                    break;
                default:
                {
                    int own = 0;

                    for (int i = 0; i < gripIndex; i++)
                    {
                        own += grips[i].Kind == CadGripKind.MTextColumnHeight ? 1 : 0;
                    }

                    frame = frame.WithColumnHeight(own, Math.Max(least, columns.HeightOf(own) + down));
                    break;
                }
            }

            CadMTextFrame result = frame;
            return Reshape(document, handle, property, was, went, () =>
            {
                result.ApplyTo((DxfMText)document.FindEntity(handle));
                return true;
            });
        }

        /// <summary>
        /// The columns of an MTEXT as its grips stand at them, in the MTEXT's own frame (its location at nought, x along its
        /// direction, y up), as AutoCAD 2021's GetGripPoints gives them: the grips of widths on the line across the location,
        /// those of heights on the line down it, each at the side of the box away from the location, the way it points.
        /// </summary>
        private sealed class MTextColumns
        {
            private DxfMText _text;
            private CadMTextLayout _layout;
            private double _left;
            private int _row;
            private int _column;

            /// <summary>The way along the MTEXT, of length one.</summary>
            internal GeoVector2 Along { get; private set; }

            /// <summary>The way up the MTEXT, of length one.</summary>
            internal GeoVector2 Up { get; private set; }

            /// <summary>1 where the box grows along the MTEXT away from its location, -1 where against it (held at the right).</summary>
            internal double AwayX { get; private set; }

            /// <summary>1 where the box grows up away from its location (held at the bottom), -1 where down.</summary>
            internal double AwayY { get; private set; }

            /// <summary>How many columns the words stand in: the count of static ones, those laid out of dynamic ones.</summary>
            internal int Count { get; private set; }

            /// <summary>The width of all the columns with their gutters.</summary>
            internal double Total { get; private set; }

            /// <summary>The height of columns of one height: the defined height, with none that of the lines.</summary>
            internal double Height { get; private set; }

            /// <summary>Whether the columns are dynamic ones of their own heights.</summary>
            internal bool OwnHeights { get; private set; }

            /// <summary>The columns of an MTEXT in columns of some width, in a drawing; false for any other.</summary>
            internal static bool TryRead(DxfMText text, out MTextColumns columns)
            {
                columns = null;
                DxfColumnType type = text.ColumnType;

                if (type == DxfColumnType.NoColumns || !(text.ColumnWidth > 0.0))
                {
                    return false;
                }

                CadMTextLayout layout = CadMTextEdit.LayoutOf(text);

                if (layout == null)
                {
                    return false;
                }

                CadMTextEdit.Axes(text, out GeoVector2 along, out GeoVector2 up);
                CadMTextEdit.Place(text, out int column, out int row);
                int count = type == DxfColumnType.StaticColumns ? Math.Max(1, text.ColumnCount) : layout.ColumnCount;
                double total = (count * text.ColumnWidth) + ((count - 1) * Math.Max(0.0, text.ColumnGutterWidth));
                columns = new MTextColumns
                {
                    _text = text,
                    _layout = layout,
                    _row = row,
                    _column = column,
                    _left =
                        column == 0 ? 0.0
                        : column == 1 ? -total / 2.0
                        : -total,
                    Along = along,
                    Up = up,
                    AwayX = column == 2 ? -1.0 : 1.0,
                    AwayY = row == 2 ? 1.0 : -1.0,
                    Count = count,
                    Total = total,
                    Height = text.Height > 0.0 ? text.Height : layout.ActualHeight,
                    OwnHeights = type == DxfColumnType.DynamicColumns && !text.ColumnAutoHeight,
                };
                return true;
            }

            /// <summary>The height of a column of its own height: its own, with none that of the lines.</summary>
            internal double HeightOf(int column)
            {
                double own = _text.GetColumnHeight(column);
                return own > 0.0 ? own : _layout.ActualHeight;
            }

            /// <summary>
            /// The grips in AutoCAD 2021's order: the location; the first column's width; then for columns of one height the
            /// corner and the height, for dynamic ones of their own heights the height of each column under its middle; last, in
            /// two columns or more, the last column's, the gutter's.
            /// </summary>
            internal void Grips(Action<CadGripKind, GeoPoint2, GeoVector2> add)
            {
                var location = new GeoPoint2(_text.Location.X, _text.Location.Y);
                double width = _text.ColumnWidth;
                double far = _column == 2 ? _left : _left + Total;
                GeoVector2 across = Along * AwayX;
                GeoVector2 down = Up * AwayY;
                GeoPoint2 At(double x, double y) => location + (Along * x) + (Up * y);
                add(CadGripKind.Insertion, location, new GeoVector2(0.0, 0.0));
                add(CadGripKind.MTextColumnWidth, At(_column == 2 ? -width : _left + width, 0.0), across);

                if (OwnHeights)
                {
                    double step = width + Math.Max(0.0, _text.ColumnGutterWidth);

                    for (int i = 0; i < Count; i++)
                    {
                        add(
                            CadGripKind.MTextColumnHeight,
                            At(_left + (i * step) + (width / 2.0), Edge(HeightOf(i))),
                            down
                        );
                    }
                }
                else
                {
                    add(CadGripKind.MTextSize, At(far, Edge(Height)), across + down);
                    add(CadGripKind.MTextHeight, At(0.0, Edge(Height)), down);
                }

                if (Count > 1)
                {
                    add(CadGripKind.MTextGutter, At(far, 0.0), across);
                }
            }

            /// <summary>The side of a box of a height away from the location, up or down: below the top, the middle, or above the bottom.</summary>
            private double Edge(double height) =>
                _row == 0 ? -height
                : _row == 1 ? -height / 2.0
                : height;
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
