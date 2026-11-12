using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The canvas's part of a table, as AutoCAD 2021 draws and drags it: the column letters and row numbers round the cells
    /// selected (TABLEINDICATOR), a range of cells dragged across, the edge grips and the autofill grip of the cells, the
    /// grips of a whole table selected, and the preview of a drag of any of them, the new line, the new outline or the cells
    /// to fill, with its tip or its field of dynamic input.
    /// </summary>
    public sealed partial class CadCanvas
    {
        private const double LetterBand = 16.0;
        private const double NumberBand = 22.0;

        private static readonly Brush HotCellFill = Frozen(new SolidColorBrush(Color.FromArgb(20, 0x3D, 0x9B, 0xFF)));
        private static readonly Pen FillRangePen = Frozen(
            new Pen(Solid(0x3D9BFF), 1.5) { DashStyle = new DashStyle(new[] { 4.0 / 1.5, 3.0 / 1.5 }, 0.0) }
        );
        private static readonly Brush ComingInk = Solid(0x6CB6FF);
        private static readonly Brush BandInk = Solid(0xFFFFFF);
        private static readonly Pen BandLine = Frozen(new Pen(Solid(0x1E2228), 1.0));
        private static readonly Brush NotOfferedBrush = Solid(0x5B6573);
        private static readonly Pen DimensionPen = Frozen(new Pen(Solid(0x9AA4B1), 1.0));
        private static readonly Brush FieldFill = Solid(0x1A1E24);
        private static readonly Pen FieldEdge = Frozen(new Pen(Solid(0x3D9BFF), 1.0));
        private static readonly Typeface BandFace = new Typeface(
            new FontFamily("Inter, Segoe UI"),
            FontStyles.Normal,
            FontWeights.Medium,
            FontStretches.Normal
        );

        private CadTableDrag _tableDrag;
        private Point? _tableDragPress;
        private GeoPoint2? _tableDragBase;
        private string _tableTyped = string.Empty;
        private Point? _cellPress;
        private GeoPoint2 _cellAnchor;
        private bool _cellDragging;
        private int _cellGripHot = -1;
        private Brush _bandFill = Solid(0x5B6573);
        private int _bandRgb = 0x5B6573;

        /// <summary>
        /// Gets or sets what a press in a cell dragged across others does, given the point pressed and the cursor, in the
        /// drawing: the window selects the range between them and says whether it did.
        /// </summary>
        public Func<GeoPoint2, GeoPoint2, bool> CellRangeDragged { get; set; }

        /// <summary>
        /// Gets or sets what a press on a grip of the cells selected starts: 0 the top edge grip, 1 the right, 2 the bottom, 3 the
        /// left, 4 the autofill grip; the window gives the drag, or null for none.
        /// </summary>
        public Func<int, CadTableDrag> CellGripPressed { get; set; }

        /// <summary>Gets or sets what ends a drag of a table's grip let go or clicked again: the window makes it, one undo step.</summary>
        public Action<CadTableDrag> TableDragEnded { get; set; }

        /// <summary>Raised on a right click while the autofill grip is dragged, for the window to show the menu of the ways to fill.</summary>
        public event EventHandler TableFillMenuRequested;

        /// <summary>Gets the drag of a table's grip under way; null for none.</summary>
        public CadTableDrag TableDrag => _tableDrag;

        /// <summary>Gets or sets whether the column letters and row numbers are drawn round a table whose cells are selected, as TABLEINDICATOR 1: on by default.</summary>
        public bool TableIndicator { get; set; } = true;

        /// <summary>Gets or sets the fill of the bands of letters and numbers, 0xRRGGBB, as Table Indicator Color: #5B6573 by default.</summary>
        public int TableIndicatorColor
        {
            get => _bandRgb;
            set
            {
                if (value != _bandRgb)
                {
                    _bandRgb = value;
                    _bandFill = Solid(value);
                }
            }
        }

        /// <summary>
        /// Takes a key while a drag of a table's line runs, as the field of dynamic input takes it: digits and a point typed
        /// give the size, Backspace takes one back, Enter makes the drag with the size typed (or where the cursor is), Esc drops
        /// the drag.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>Whether it was taken.</returns>
        public bool TableDragKey(Key key)
        {
            if (_tableDrag == null)
            {
                return false;
            }

            if (key == Key.Escape)
            {
                CancelGesture();
                return true;
            }

            if (key == Key.Enter || key == Key.Return)
            {
                EndTableDrag();
                return true;
            }

            bool sized = _tableDrag.Kind == CadTableDragKind.ColumnLine || _tableDrag.Kind == CadTableDragKind.RowLine;
            string typed = _tableTyped;

            if (key == Key.Back)
            {
                typed = typed.Length > 0 ? typed.Substring(0, typed.Length - 1) : typed;
            }
            else if (key >= Key.D0 && key <= Key.D9)
            {
                typed += (char)('0' + (key - Key.D0));
            }
            else if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                typed += (char)('0' + (key - Key.NumPad0));
            }
            else if ((key == Key.OemPeriod || key == Key.Decimal) && typed.IndexOf('.') < 0)
            {
                typed += ".";
            }
            else
            {
                return false;
            }

            if (!sized)
            {
                return true;
            }

            _tableTyped = typed;

            if (double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double size) && size > 0.0)
            {
                _tableDrag.TypeSize(size);
            }

            InvalidateVisual();
            return true;
        }

        /// <summary>
        /// Starts the drag of a grip of the cells selected as though it had been clicked, as Columns › Resize and Rows › Resize
        /// start it: 1 the right edge grip, 2 the bottom one, and the others as <see cref="CellGripPressed"/> numbers them. The
        /// line follows the cursor; a click, Enter or a size typed makes it, Esc drops it.
        /// </summary>
        /// <param name="grip">The grip, 0 to 4.</param>
        /// <returns>Whether a drag started: false where no cells are selected, a command runs, or a drag is under way.</returns>
        public bool DragCellGrip(int grip)
        {
            if (_tableDrag != null || IsRunning || CellGripPressed == null || !(CellGripBase(grip) is GeoPoint2 at))
            {
                return false;
            }

            CadTableDrag drag = CellGripPressed(grip);

            if (drag == null)
            {
                return false;
            }

            _tableDrag = drag;
            _tableDragPress = null;
            _tableDragBase = at;
            _tableTyped = string.Empty;
            InvalidateVisual();
            return true;
        }

        /// <summary>A press of a mouse button that a table's grips or a drag of one take: true where it is taken.</summary>
        private bool TableMouseDown(MouseButton button, Point p)
        {
            if (button == MouseButton.Right && _tableDrag != null)
            {
                // A right click while the autofill grip is dragged: its menu of the ways to fill; else nothing.
                if (_tableDrag.Kind == CadTableDragKind.Autofill)
                {
                    TableFillMenuRequested?.Invoke(this, EventArgs.Empty);
                }

                return true;
            }

            if (button != MouseButton.Left || IsRunning)
            {
                return false;
            }

            if (_tableDrag != null)
            {
                // The second click of a grip clicked, then clicked again where it goes.
                Track(p);
                UpdateTableDrag(p);
                EndTableDrag();
                _swallowUp = true;
                return true;
            }

            int cellGrip = CellGripAt(p);
            CadTableDrag drag = null;
            GeoPoint2? at = null;

            if (cellGrip >= 0 && CellGripPressed != null)
            {
                drag = CellGripPressed(cellGrip);
                at = CellGripBase(cellGrip);
            }
            else if (
                _hotGrip >= 0
                && _hotGrip < _grips.Count
                && (Keyboard.Modifiers & ModifierKeys.Shift) == 0
                && Document != null
            )
            {
                CadGripKind kind = _grips[_hotGrip].Grip.Kind;

                if (kind == CadGripKind.TableBreak)
                {
                    // Drawn for parity: a press does nothing.
                    _swallowUp = true;
                    return true;
                }

                if (
                    kind == CadGripKind.TableColumn
                    || kind == CadGripKind.TableWidth
                    || kind == CadGripKind.TableHeight
                    || kind == CadGripKind.TableSize
                )
                {
                    drag = CadTableDrag.FromGrip(Document, _grips[_hotGrip].Handle, _grips[_hotGrip].Grip.Index);
                    at = _grips[_hotGrip].Grip.Point;
                }
            }

            if (drag == null)
            {
                return false;
            }

            _tableDrag = drag;
            _tableDragPress = p;
            _tableDragBase = at;
            _tableTyped = string.Empty;
            CaptureMouse();
            UpdateTableDrag(p);
            InvalidateVisual();
            return true;
        }

        /// <summary>A press that started a box inside a cell: kept, as a drag past the system's distance selects a range instead.</summary>
        private void PressInCell(Point p)
        {
            _cellPress = null;
            _cellDragging = false;

            if (!IsRunning && (CellRangeDragged != null || CellClicked != null) && InsideCell(p))
            {
                _cellPress = p;
                _cellAnchor = DrawingAt(p);
            }
        }

        /// <summary>The mouse moved: a drag of a table's grip or across cells follows it; true where nothing else is to follow it.</summary>
        private bool TableMouseMove(Point at)
        {
            if (_tableDrag != null)
            {
                if (_crosshairPercent > 0.0)
                {
                    _mouse = at;
                }

                if (_tableTyped.Length == 0)
                {
                    UpdateTableDrag(at);
                }

                InvalidateVisual();
                return true;
            }

            if (_cellPress.HasValue)
            {
                Vector moved = at - _cellPress.Value;

                if (!_cellDragging && (Math.Abs(moved.X) > DragPixels || Math.Abs(moved.Y) > DragPixels))
                {
                    if (CellRangeDragged == null)
                    {
                        // No range to select: the box the press started goes on.
                        _cellPress = null;
                        return false;
                    }

                    // Past the system's drag distance: the range, not a box.
                    _cellDragging = true;
                    _boxFrom = null;
                    _boxing = false;
                    _lasso = null;
                }

                if (_cellDragging)
                {
                    CellRangeDragged?.Invoke(_cellAnchor, DrawingAt(at));
                    InvalidateVisual();
                    return true;
                }

                return false;
            }

            int hot = IsRunning ? -1 : CellGripAt(at);

            if (hot != _cellGripHot)
            {
                _cellGripHot = hot;
                InvalidateVisual();
            }

            return false;
        }

        /// <summary>The left button let go: a drag of a table's grip let go away from where it was pressed is made, one let go where it was pressed waits for its click; a range dragged ends.</summary>
        private bool TableMouseUp(Point at)
        {
            if (_cellPress.HasValue)
            {
                bool dragged = _cellDragging;
                Point pressed = _cellPress.Value;
                _cellPress = null;
                _cellDragging = false;
                ReleaseMouseCapture();

                if (!dragged)
                {
                    // A click in a cell, away from its lines, selects the cell, though no object lies under the pick box: the
                    // box it started is dropped rather than left open.
                    _boxFrom = null;
                    _boxing = false;
                    _lasso = null;
                    Click(pressed, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
                    return true;
                }

                SelectionChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
                return true;
            }

            if (_tableDrag == null || !_tableDragPress.HasValue)
            {
                return false;
            }

            Vector moved = at - _tableDragPress.Value;
            _tableDragPress = null;

            if (Math.Abs(moved.X) > DragPixels || Math.Abs(moved.Y) > DragPixels)
            {
                if (_tableTyped.Length == 0)
                {
                    UpdateTableDrag(at);
                }

                EndTableDrag();
            }

            return true;
        }

        /// <summary>Drops a drag of a table's grip or across cells, as Esc does: nothing changed.</summary>
        private void CancelTableGesture()
        {
            if (_tableDrag != null)
            {
                // The snap the drag followed goes with it.
                _snapped = null;
            }

            _tableDrag = null;
            _tableDragPress = null;
            _tableDragBase = null;
            _tableTyped = string.Empty;
            _cellPress = null;
            _cellDragging = false;
        }

        /// <summary>
        /// The drag follows the cursor as the other grip drags do: snapped from the grip's point by the object snaps, ORTHO and
        /// polar tracking, as they are on.
        /// </summary>
        private void UpdateTableDrag(Point p)
        {
            if (_tableDrag == null)
            {
                return;
            }

            GeoPoint2 cursor = DrawingAt(p);
            CadView view = WorkView;
            _snapped = null;

            if (_tableDragBase.HasValue && view != null && Document != null && WorkSpace != null)
            {
                double aperture = AperturePixels * PixelScale / view.PixelsPerUnit;
                _snapped = CadPicking.Snap(Document, WorkSpace, Snap, cursor, aperture, _tableDragBase);

                // A perpendicular from the grip to a line through it, as the table's own edge the grip stands on, is the grip
                // itself, which would hold the line where it was: not taken.
                if (
                    _snapped != null
                    && _snapped.Kind == CadSnapKind.Perpendicular
                    && _snapped.Point.DistanceTo(_tableDragBase.Value) <= Snap.Tolerance.EqualPoint
                )
                {
                    _snapped = null;
                }

                cursor = BandEnd(cursor);
            }

            _tableDrag.Update(cursor, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
        }

        /// <summary>The point a grip of the cells selected stands at, in the drawing: the middle of its edge, or the corner of the autofill grip; null for none.</summary>
        private GeoPoint2? CellGripBase(int grip)
        {
            GeoPoint2[][] outline = CellOutline?.Invoke();

            if (outline == null || outline.Length < 1 || outline[0].Length < 4 || grip < 0 || grip > 4)
            {
                return null;
            }

            GeoPoint2[] range = outline[0];

            if (grip == 4)
            {
                return range[2];
            }

            GeoPoint2 a = range[grip],
                b = range[(grip + 1) % 4];
            return new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
        }

        /// <summary>Makes the drag of a table's grip, through the window, and ends it.</summary>
        private void EndTableDrag()
        {
            CadTableDrag drag = _tableDrag;
            CancelTableGesture();
            ReleaseMouseCapture();

            if (drag != null)
            {
                TableDragEnded?.Invoke(drag);
                HistoryWritten?.Invoke(this, EventArgs.Empty);
            }

            InvalidateVisual();
        }

        /// <summary>The grip of the cells selected under a point of the canvas: 0 to 3 the edges from the top round, 4 the autofill grip; -1 for none.</summary>
        private int CellGripAt(Point p)
        {
            GeoPoint2[][] outline = CellOutline?.Invoke();
            CadView view = WorkView;

            if (outline == null || outline.Length < 2 || view == null)
            {
                return -1;
            }

            double scale = PixelScale;
            Point[] range = Array.ConvertAll(outline[0], q => ToScreen(view, q, scale));
            double reach = GripSquare * 0.75;

            if (Math.Abs(range[2].X - p.X) + Math.Abs(range[2].Y - p.Y) <= 7.0 + 2.0)
            {
                return 4;
            }

            for (int k = 0; k < 4; k++)
            {
                Point a = range[k],
                    b = range[(k + 1) % 4];

                if (Math.Abs((a.X + b.X) / 2.0 - p.X) <= reach && Math.Abs((a.Y + b.Y) / 2.0 - p.Y) <= reach)
                {
                    return k;
                }
            }

            return -1;
        }

        /// <summary>
        /// The cells of a table selected, as AutoCAD 2021 draws them: the range in the accent, filled lightly, a grip at the
        /// middle of each of its sides and the autofill diamond at its bottom right corner, and with TABLEINDICATOR 1 the table's
        /// column letters above it and row numbers left of it, those of the range on amber. While a range is dragged, or the
        /// autofill grip, the grips hide.
        /// </summary>
        private void DrawCells(DrawingContext dc, CadView view, double scale)
        {
            GeoPoint2[][] outline = CellOutline?.Invoke();

            if (outline == null || outline.Length < 2)
            {
                return;
            }

            Point[] range = Array.ConvertAll(outline[0], p => ToScreen(view, p, scale));
            Point[] table = Array.ConvertAll(outline[1], p => ToScreen(view, p, scale));
            dc.DrawGeometry(CellFill, CellPen, Polygon(range));
            bool gripsShown = !_cellDragging && (_tableDrag == null || _tableDrag.Kind != CadTableDragKind.Autofill);

            if (gripsShown)
            {
                double half = GripSquare / 2.0;

                for (int k = 0; k < 4; k++)
                {
                    Point a = range[k],
                        b = range[(k + 1) % 4];
                    dc.DrawRectangle(
                        k == _cellGripHot ? HotGripBrush : CellBrush,
                        null,
                        new Rect((a.X + b.X) / 2.0 - half, (a.Y + b.Y) / 2.0 - half, GripSquare, GripSquare)
                    );
                }

                DrawDiamond(dc, range[2], _cellGripHot == 4 ? HotGripBrush : CellBrush);
            }

            if (TableIndicator)
            {
                DrawBands(dc, view, scale, outline, range, table);
            }
        }

        /// <summary>The autofill grip: a diamond 14 pixels across, edged dark.</summary>
        private static void DrawDiamond(DrawingContext dc, Point at, Brush fill) =>
            dc.DrawGeometry(
                fill,
                CustomGripEdge,
                Polygon(
                    new[]
                    {
                        new Point(at.X, at.Y - 7.0),
                        new Point(at.X + 7.0, at.Y),
                        new Point(at.X, at.Y + 7.0),
                        new Point(at.X - 7.0, at.Y),
                    }
                )
            );

        /// <summary>The bands of column letters over the table and row numbers left of it, with their corner box; the range's part on amber.</summary>
        private void DrawBands(
            DrawingContext dc,
            CadView view,
            double scale,
            GeoPoint2[][] outline,
            Point[] range,
            Point[] table
        )
        {
            Vector across = table[1] - table[0];
            Vector down = table[3] - table[0];

            if (across.Length < 1.0 || down.Length < 1.0)
            {
                return;
            }

            Vector up = down / down.Length * -LetterBand;
            Vector left = across / across.Length * -NumberBand;
            dc.DrawGeometry(_bandFill, null, Polygon(new[] { table[0] + up, table[1] + up, table[1], table[0] }));
            dc.DrawGeometry(_bandFill, null, Polygon(new[] { table[0] + left, table[0], table[3], table[3] + left }));
            dc.DrawGeometry(
                _bandFill,
                null,
                Polygon(new[] { table[0] + up + left, table[0] + up, table[0], table[0] + left })
            );

            // A corner of the range carried onto the table's top (or left) edge.
            Point Along(Point p, Vector axis) => table[0] + axis * ((p - table[0]) * axis / axis.LengthSquared);
            Point c0 = Along(range[0], across),
                c1 = Along(range[1], across);
            Point w0 = Along(range[0], down),
                w3 = Along(range[3], down);
            dc.DrawGeometry(BandHot, null, Polygon(new[] { c0 + up, c1 + up, c1, c0 }));
            dc.DrawGeometry(BandHot, null, Polygon(new[] { w0 + left, w0, w3, w3 + left }));

            if (outline.Length < 4)
            {
                return;
            }

            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            Point[] columns = Array.ConvertAll(outline[2], p => ToScreen(view, p, scale));
            Point[] rows = Array.ConvertAll(outline[3], p => ToScreen(view, p, scale));

            for (int c = 0; c < columns.Length; c++)
            {
                dc.DrawLine(BandLine, columns[c] + up, columns[c]);

                if (c + 1 < columns.Length)
                {
                    BandText(dc, CadTableEditor.ColumnName(c), columns[c], columns[c + 1], up, dip);
                }
            }

            for (int r = 0; r < rows.Length; r++)
            {
                dc.DrawLine(BandLine, rows[r] + left, rows[r]);

                if (r + 1 < rows.Length)
                {
                    BandText(dc, (r + 1).ToString(CultureInfo.InvariantCulture), rows[r], rows[r + 1], left, dip);
                }
            }
        }

        /// <summary>A letter or a number of a band, white, in the middle of its part of the band, left out where the part is too small for it.</summary>
        private static void BandText(DrawingContext dc, string text, Point from, Point to, Vector band, double dip)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                BandFace,
                11.0,
                BandInk,
                dip
            );
            Point middle = from + (to - from) / 2.0 + band / 2.0;

            if ((to - from).Length < Math.Min(formatted.Width, formatted.Height))
            {
                return;
            }

            dc.DrawText(formatted, new Point(middle.X - formatted.Width / 2.0, middle.Y - formatted.Height / 2.0));
        }

        /// <summary>
        /// A grip of a whole table: a stretch grip a triangle pointing along the table, down it, or into its corner; the table
        /// breaking grip a grey triangle under the bottom edge, never hot; true where the grip is one of these.
        /// </summary>
        private bool DrawTableGrip(DrawingContext dc, CadView view, double scale, CadGrip grip, Point at, bool hot)
        {
            if (
                grip.Kind != CadGripKind.TableWidth
                && grip.Kind != CadGripKind.TableHeight
                && grip.Kind != CadGripKind.TableSize
                && grip.Kind != CadGripKind.TableBreak
            )
            {
                return false;
            }

            Point ahead = ToScreen(
                view,
                new GeoPoint2(grip.Point.X + grip.Direction.X, grip.Point.Y + grip.Direction.Y),
                scale
            );
            double angle = Math.Atan2(ahead.Y - at.Y, ahead.X - at.X);

            if (grip.Kind == CadGripKind.TableBreak)
            {
                var below = new Point(at.X + 8.0 * Math.Cos(angle), at.Y + 8.0 * Math.Sin(angle));
                dc.DrawGeometry(NotOfferedBrush, null, Polygon(Shape(below, angle, 1.0, -4, -7, 4, 0, -4, 7)));
                return true;
            }

            dc.DrawGeometry(
                hot ? HotGripBrush : GripBrush,
                CustomGripEdge,
                Polygon(Shape(at, angle, 1.0, -5, -5, 5, 0, -5, 5))
            );
            return true;
        }

        /// <summary>
        /// What a drag of a table's grip will make: the new line dashed across the table, the new outline of a stretch, or the
        /// cells an autofill will fill outlined dashed with their coming values; the grip hot at the cursor, and the tip, or with
        /// dynamic input on the field of a width or height over the table.
        /// </summary>
        private void DrawTableDrag(DrawingContext dc, CadView view, double scale)
        {
            CadTableDrag drag = _tableDrag;

            if (drag == null || Document == null)
            {
                return;
            }

            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            Point? cursor = _mouse ?? (_cursor.HasValue ? ToScreen(view, _cursor.Value, scale) : (Point?)null);

            switch (drag.Kind)
            {
                case CadTableDragKind.ColumnLine:
                case CadTableDragKind.RowLine:
                    if (drag.Line != null)
                    {
                        dc.DrawLine(
                            RubberPen,
                            ToScreen(view, drag.Line[0], scale),
                            ToScreen(view, drag.Line[1], scale)
                        );
                    }

                    break;
                case CadTableDragKind.Size:
                    if (drag.Outline != null)
                    {
                        dc.DrawGeometry(
                            null,
                            RubberPen,
                            Polygon(Array.ConvertAll(drag.Outline, p => ToScreen(view, p, scale)))
                        );
                    }

                    break;
                default:
                    DrawFillRange(dc, view, scale, drag, dip);
                    break;
            }

            // The snap the drag follows: its marker, and polar tracking's path from the grip.
            if (_snapped != null)
            {
                DrawTracking(dc, view, scale, _tableDragBase);

                if (Snap == null || Snap.ShowsMarker)
                {
                    DrawMarker(dc, _snapped, ToScreen(view, _snapped.Point, scale));
                }
            }

            // The grip dragged, hot, at the cursor.
            if (cursor.HasValue)
            {
                Point at = cursor.Value;

                if (drag.Kind == CadTableDragKind.Autofill)
                {
                    DrawDiamond(dc, at, HotGripBrush);
                }
                else
                {
                    double half = GripSquare / 2.0;
                    dc.DrawRectangle(HotGripBrush, null, new Rect(at.X - half, at.Y - half, GripSquare, GripSquare));
                }
            }

            DxfTable table = Document.FindEntity(drag.Handle) as DxfTable;
            bool field =
                DynamicInput
                && (drag.Kind == CadTableDragKind.ColumnLine || drag.Kind == CadTableDragKind.RowLine)
                && drag.Line != null
                && table != null;

            if (field)
            {
                DrawSizeField(dc, view, scale, drag, table, dip);
            }
            else if (drag.Tip != null && cursor.HasValue)
            {
                DrawDragTip(dc, drag.Tip, cursor.Value, dip);
            }
        }

        /// <summary>The cells an autofill will fill: outlined dashed and filled faintly, each with its coming value in light blue.</summary>
        private void DrawFillRange(DrawingContext dc, CadView view, double scale, CadTableDrag drag, double dip)
        {
            if (drag.FillRange == null || !(Document.FindEntity(drag.Handle) is DxfTable table))
            {
                return;
            }

            Point[] corners = Array.ConvertAll(CadTableEditor.Corners(table, drag.FillRange), p =>
                ToScreen(view, p, scale)
            );
            dc.DrawGeometry(HotCellFill, FillRangePen, Polygon(corners));

            foreach (CadTableCellValue value in drag.Values)
            {
                if (string.IsNullOrEmpty(value.Text))
                {
                    continue;
                }

                var text = new FormattedText(
                    value.Text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    TipFace,
                    11.0,
                    ComingInk,
                    dip
                );
                Point middle = ToScreen(view, value.Center, scale);
                dc.DrawText(text, new Point(middle.X - text.Width / 2.0, middle.Y - text.Height / 2.0));
            }
        }

        /// <summary>The tip of a drag, as the dimension grips draw theirs: 14 pixels right of the cursor and 10 below, kept on the canvas.</summary>
        private void DrawDragTip(DrawingContext dc, string tip, Point at, double dip)
        {
            var text = new FormattedText(
                tip,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                GripTipFace,
                11.0,
                GripTipInk,
                dip
            );
            var box = new Rect(at.X + 14.0, at.Y + 10.0, text.Width + 16.0, text.Height + 6.0);

            if (box.Right > ActualWidth && at.X - 14.0 - box.Width >= 0.0)
            {
                box.X = at.X - 14.0 - box.Width;
            }

            dc.DrawRoundedRectangle(GripTipFill, GripTipEdge, box, 2.0, 2.0);
            dc.DrawText(text, new Point(box.X + 8.0, box.Y + 3.0));
        }

        /// <summary>
        /// With dynamic input on, the width of a column (or the height of a row) dragged: a dimension 30 pixels over (or left
        /// of) the table from the column's left (the row's top) line to the new line, ticked at 45 degrees, and on it the field
        /// that shows the size, or what is typed. Over is the side of the table's first row, below the table where its rows
        /// flow up.
        /// </summary>
        private void DrawSizeField(
            DrawingContext dc,
            CadView view,
            double scale,
            CadTableDrag drag,
            DxfTable table,
            double dip
        )
        {
            bool columns = drag.Kind == CadTableDragKind.ColumnLine;
            double size = (columns ? drag.Widths : drag.Heights)[drag.Changing];
            Point top = ToScreen(view, drag.Line[0], scale);
            Point bottom = ToScreen(view, drag.Line[1], scale);
            Vector down = bottom - top;

            // The way the line goes as its column (or row) grows, on the screen: along the table's columns (or its rows).
            GeoPoint2[] first = CadTableEditor.Corners(table, new DxfCellRange(0, 0, 0, 0));
            Vector along = ToScreen(view, columns ? first[1] : first[3], scale) - ToScreen(view, first[0], scale);

            if (down.Length < 1.0 || along.Length < 1E-9)
            {
                return;
            }

            down /= down.Length;
            along /= along.Length;
            double width = size * view.PixelsPerUnit / scale;

            // The way along the table: from the column's left (the row's top) line to its other, the new line at one end of it.
            Point right = drag.IsFirstEdge ? top - down * 30.0 + along * width : top - down * 30.0;
            Point left = right - along * width;
            dc.DrawLine(DimensionPen, left, right);
            dc.DrawLine(DimensionPen, left, left + down * 30.0);
            dc.DrawLine(DimensionPen, right, right + down * 30.0);

            foreach (Point end in new[] { left, right })
            {
                Vector tick = (along - down) * 4.0;
                dc.DrawLine(DimensionPen, end - tick, end + tick);
            }

            string shown =
                _tableTyped.Length > 0
                    ? _tableTyped
                    : size.ToString(
                        "F" + Math.Max(0, Math.Min(8, Document.Database.Luprec)),
                        CultureInfo.InvariantCulture
                    );
            var text = new FormattedText(
                shown,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                GripTipFace,
                11.0,
                GripTipInk,
                dip
            );
            Point middle = left + (right - left) / 2.0;
            var box = new Rect(
                middle.X - text.Width / 2.0 - 6.0,
                middle.Y - text.Height / 2.0 - 3.0,
                text.Width + 12.0,
                text.Height + 6.0
            );
            dc.DrawRectangle(FieldFill, FieldEdge, box);
            dc.DrawText(text, new Point(box.X + 6.0, box.Y + 3.0));
        }
    }
}
