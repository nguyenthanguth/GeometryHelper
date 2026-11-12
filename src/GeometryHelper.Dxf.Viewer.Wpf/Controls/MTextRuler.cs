using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>What a point of the MTEXT editor's ruler, or a height grip under the frame, is over.</summary>
    internal enum MTextRulerPart
    {
        /// <summary>Nothing of the ruler.</summary>
        None,

        /// <summary>The tab type box at the ruler's left end.</summary>
        TypeBox,

        /// <summary>The ruler where no marker is: a click adds a tab stop.</summary>
        Ruler,

        /// <summary>The upper triangle: the first line's indent.</summary>
        First,

        /// <summary>The lower triangle: the later lines' indent.</summary>
        Hanging,

        /// <summary>A tab stop.</summary>
        Tab,

        /// <summary>The arrows at the right end: the MTEXT's width.</summary>
        Width,

        /// <summary>The arrows at the end of the first column: the width of every column.</summary>
        ColumnWidth,

        /// <summary>The right edge of the first gutter.</summary>
        Gutter,

        /// <summary>The arrows at the end of static columns: the total width.</summary>
        Total,

        /// <summary>A height grip under a column, or under the frame.</summary>
        Height,
    }

    /// <summary>
    /// The MTEXT editor's ruler as the design draws it, without drawing it: where its markers stand on the editor's frame,
    /// what a point is over, and what a click or a drag does to the editor (<see cref="CadMTextEditor"/>). A click on the
    /// ruler adds a tab stop of the type box's kind at the nearest minor tick; a drag moves a stop, an indent, the width, a
    /// column's width, the gutter, the total or a height, snapping to minor ticks, and is one step of the editor's undo when
    /// released, put back by <see cref="Cancel"/> (Esc); a stop dragged more than 12 pixels off the ruler is removed.
    /// </summary>
    /// <remarks>
    /// Pixels are the frame's: the ruler 22 high from <see cref="RulerTop"/>, the indent zero of the first column at
    /// <see cref="Zero"/>, a drawing unit <see cref="Scale"/> pixels. Major ticks stand every u drawing units, u the smallest of
    /// 1, 2 or 5 × 10ⁿ at least 30 pixels across, with four minor ticks between: at 6 pixels to a unit, 5 units and 1.
    /// </remarks>
    internal sealed class MTextRuler
    {
        /// <summary>The height of the ruler.</summary>
        internal const double Height = 22.0;

        /// <summary>The width of the tab type box.</summary>
        internal const double TypeBoxWidth = 18.0;

        private readonly TextEditorViewModel _model;
        private MTextRulerPart _drag = MTextRulerPart.None;
        private int _dragIndex;
        private CadMTextTabStop _tab;
        private double _tabAt = double.NaN;
        private double _value;

        internal MTextRuler(TextEditorViewModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        /// <summary>Gets or sets the pixel X of the first column's indent zero: the left of the words.</summary>
        internal double Zero { get; set; }

        /// <summary>Gets or sets the pixel X of the frame's left, the tab type box standing just left of it.</summary>
        internal double FrameLeft { get; set; }

        /// <summary>Gets or sets the pixel Y of the ruler's top.</summary>
        internal double RulerTop { get; set; }

        /// <summary>Gets or sets the pixel Y of the top of the words' box, from which heights are measured down.</summary>
        internal double BoxTop { get; set; }

        /// <summary>Gets or sets how many pixels a drawing unit is.</summary>
        internal double Scale { get; set; } = 1.0;

        /// <summary>Gets or sets how many columns the words stand in now.</summary>
        internal int Columns { get; set; } = 1;

        /// <summary>Gets or sets the column of the caret, whose segment shows the indents and stops.</summary>
        internal int CaretColumn { get; set; }

        /// <summary>Gets or sets the width of the words where the MTEXT has none, so that the ruler spans them.</summary>
        internal double NaturalWidth { get; set; }

        /// <summary>Gets the kind the next tab stop clicked on the ruler takes: l, c, r or d, Left each time the editor opens.</summary>
        internal char TabType { get; private set; } = 'l';

        /// <summary>Gets what is being dragged; <see cref="MTextRulerPart.None"/> for nothing.</summary>
        internal MTextRulerPart Dragging => _drag;

        /// <summary>Gets the column or stop dragged.</summary>
        internal int DraggedIndex => _dragIndex;

        /// <summary>Gets whether the stop dragged is off the ruler, to be removed on release.</summary>
        internal bool IsOff => _drag == MTextRulerPart.Tab && double.IsNaN(_tabAt);

        /// <summary>Gets the value the drag has now, in drawing units.</summary>
        internal double DragValue => _value;

        private CadMTextEditor Editor => _model.Editor;

        private CadMTextFrame Frame => Editor.Frame;

        private double Step => Frame.HasColumns ? Frame.ColumnWidth + Frame.Gutter : 0.0;

        /// <summary>Gets how far an indent or a stop may reach: a column's width, the MTEXT's, or the words' with none.</summary>
        internal double Room =>
            Frame.HasColumns ? Frame.ColumnWidth
            : Frame.Width > 0.0 ? Frame.Width
            : Math.Max(NaturalWidth, Editor.Height);

        /// <summary>Gets the units between two major ticks.</summary>
        internal double Major
        {
            get
            {
                double scale = Scale > 0.0 ? Scale : 1.0;
                double power = Math.Pow(10.0, Math.Floor(Math.Log10(30.0 / scale)));

                foreach (double m in new[] { 1.0, 2.0, 5.0, 10.0 })
                {
                    if (m * power * scale >= 30.0 - 1E-9)
                    {
                        return m * power;
                    }
                }

                return 10.0 * power;
            }
        }

        /// <summary>Gets the units between two minor ticks, a fifth of a major one.</summary>
        internal double Minor => Major / 5.0;

        /// <summary>Gets whether the markers are shown: the frame (or column) at least 120 pixels across.</summary>
        internal bool ShowsMarkers => Room * Scale >= 120.0;

        /// <summary>The pixel X of a column's indent zero.</summary>
        internal double ZeroOf(int column) => Zero + (column * Step * Scale);

        /// <summary>The pixel X of a length from the caret column's zero.</summary>
        internal double X(double units) => ZeroOf(CaretColumn) + (units * Scale);

        /// <summary>The pixel X of the arrows at the right end: the MTEXT's width, or the end of static columns.</summary>
        internal double EndX =>
            Frame.ColumnType == DxfColumnType.StaticColumns
                ? ZeroOf(Frame.ColumnCount - 1) + (Frame.ColumnWidth * Scale)
            : Frame.HasColumns ? ZeroOf(0) + (Frame.ColumnWidth * Scale)
            : Zero + (Room * Scale);

        /// <summary>The height a column's grip stands at: its own, or the columns' one height, or the box's defined or natural height.</summary>
        internal double HeightOf(int column, double natural)
        {
            if (Frame.HasColumns && !Frame.AutoHeight && Frame.ColumnType == DxfColumnType.DynamicColumns)
            {
                return column < Frame.ColumnHeights.Count && Frame.ColumnHeights[column] > 0.0
                    ? Frame.ColumnHeights[column]
                    : Frame.Height;
            }

            return Frame.Height > 0.0 ? Frame.Height : natural;
        }

        /// <summary>The columns that have a height grip: every one of dynamic columns of their own heights, else the first.</summary>
        internal int GripCount =>
            Frame.ColumnType == DxfColumnType.DynamicColumns && !Frame.AutoHeight ? Math.Max(1, Columns) : 1;

        /// <summary>The pixel point of a column's height grip.</summary>
        internal Point GripAt(int column, double natural)
        {
            double width = Frame.HasColumns ? Frame.ColumnWidth : Room;
            return new Point(ZeroOf(column) + (width * Scale / 2.0), BoxTop + (HeightOf(column, natural) * Scale));
        }

        /// <summary>A length snapped to the nearest minor tick.</summary>
        internal double Snap(double units) => Math.Round(units / Minor) * Minor;

        /// <summary>
        /// What a pixel point is over, the markers first: a stop or a triangle within 5 pixels in its half of the ruler, then the
        /// arrows (16 by 22), the gutter edge (8 across) and the height grips (16 by 16); the rest of the ruler is the ruler.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="natural">The words' height where the box has none, for the height grip.</param>
        /// <param name="index">The stop or column it is over.</param>
        /// <returns>The part.</returns>
        internal MTextRulerPart HitTest(Point point, double natural, out int index)
        {
            index = 0;
            double y = point.Y - RulerTop;
            CadMTextFrame frame = Frame;

            for (int c = 0; c < GripCount; c++)
            {
                Point grip = GripAt(c, natural);

                if (Math.Abs(point.X - grip.X) <= 8.0 && point.Y >= grip.Y - 2.0 && point.Y <= grip.Y + 14.0)
                {
                    index = c;
                    return MTextRulerPart.Height;
                }
            }

            if (y < 0.0 || y > Height)
            {
                return MTextRulerPart.None;
            }

            if (point.X < FrameLeft && point.X >= FrameLeft - TypeBoxWidth)
            {
                return MTextRulerPart.TypeBox;
            }

            if (ShowsMarkers)
            {
                CadMTextParagraphFormat format = Editor.CurrentFormat;

                if (y >= Height / 2.0)
                {
                    for (int i = 0; i < format.TabStops.Count; i++)
                    {
                        if (Math.Abs(point.X - X(format.TabStops[i].Position)) <= 5.0)
                        {
                            index = i;
                            return MTextRulerPart.Tab;
                        }
                    }

                    if (Math.Abs(point.X - X(format.HangingIndent)) <= 5.0)
                    {
                        return MTextRulerPart.Hanging;
                    }
                }
                else if (Math.Abs(point.X - X(format.FirstLineIndent)) <= 5.0)
                {
                    return MTextRulerPart.First;
                }
            }

            if (frame.ColumnType == DxfColumnType.StaticColumns && Near(point.X, EndX))
            {
                return MTextRulerPart.Total;
            }

            if (frame.HasColumns && Near(point.X, ZeroOf(0) + (frame.ColumnWidth * Scale)))
            {
                return MTextRulerPart.ColumnWidth;
            }

            if (frame.HasColumns && Columns > 1 && Math.Abs(point.X - ZeroOf(1)) <= 4.0)
            {
                return MTextRulerPart.Gutter;
            }

            if (!frame.HasColumns && Near(point.X, EndX))
            {
                return MTextRulerPart.Width;
            }

            return MTextRulerPart.Ruler;
        }

        /// <summary>The tip of a part, as the design writes it: "Left tab: 10.0000", "Width: 55.0000".</summary>
        internal string TipOf(MTextRulerPart part, int index, double natural)
        {
            CadMTextParagraphFormat format = Editor.CurrentFormat;
            CadMTextFrame frame = Frame;

            switch (part)
            {
                case MTextRulerPart.TypeBox:
                    return "Tab type: " + ParagraphViewModel.TypeName(TabType) + ". Click to change.";
                case MTextRulerPart.First:
                    return "First line indent: " + Text(format.FirstLineIndent);
                case MTextRulerPart.Hanging:
                    return "Paragraph indent: " + Text(format.HangingIndent);
                case MTextRulerPart.Tab:
                    return index < format.TabStops.Count
                        ? ParagraphViewModel.TypeName(format.TabStops[index].Kind)
                            + " tab: "
                            + Text(format.TabStops[index].Position)
                        : null;
                case MTextRulerPart.Width:
                    return "Width: " + Text(Room);
                case MTextRulerPart.ColumnWidth:
                    return "Column width: " + Text(frame.ColumnWidth);
                case MTextRulerPart.Gutter:
                    return "Gutter: " + Text(frame.Gutter);
                case MTextRulerPart.Total:
                    return "Total width: " + Text(frame.Width);
                case MTextRulerPart.Height:
                    return GripCount > 1
                        ? "Column "
                            + (index + 1).ToString(CultureInfo.InvariantCulture)
                            + " height: "
                            + Text(HeightOf(index, natural))
                        : "Height: " + Text(HeightOf(index, natural));
                default:
                    return null;
            }
        }

        /// <summary>The tip shown while dragging: the value it has now, or that the stop will be removed.</summary>
        internal string DragTip
        {
            get
            {
                switch (_drag)
                {
                    case MTextRulerPart.None:
                        return null;
                    case MTextRulerPart.Tab:
                        return IsOff
                            ? "Release to remove the tab"
                            : ParagraphViewModel.TypeName(_tab.Kind) + " tab: " + Text(_value);
                    case MTextRulerPart.First:
                        return "First line indent: " + Text(_value);
                    case MTextRulerPart.Hanging:
                        return "Paragraph indent: " + Text(_value);
                    case MTextRulerPart.Width:
                        return "Width: " + Text(_value);
                    case MTextRulerPart.ColumnWidth:
                        return "Column width: " + Text(_value);
                    case MTextRulerPart.Gutter:
                        return "Gutter: " + Text(_value);
                    case MTextRulerPart.Total:
                        return "Total width: " + Text(_value);
                    default:
                        return GripCount > 1
                            ? "Column "
                                + (_dragIndex + 1).ToString(CultureInfo.InvariantCulture)
                                + " height: "
                                + Text(_value)
                            : "Height: " + Text(_value);
                }
            }
        }

        /// <summary>
        /// A press of the left button: the type box cycles Left, Centre, Right, Decimal; the ruler adds a stop (one undo step);
        /// a marker, arrows, the gutter edge or a grip starts a drag.
        /// </summary>
        /// <returns>Whether the press was the ruler's.</returns>
        internal bool Press(Point point, double natural)
        {
            MTextRulerPart part = HitTest(point, natural, out int index);

            switch (part)
            {
                case MTextRulerPart.None:
                    return false;
                case MTextRulerPart.TypeBox:
                    TabType = "lcrdl"["lcrd".IndexOf(TabType) + 1];
                    return true;
                case MTextRulerPart.Ruler:
                    AddStop(point);
                    return true;
            }

            _drag = part;
            _dragIndex = index;
            CadMTextParagraphFormat format = Editor.CurrentFormat;
            CadMTextFrame frame = Frame;
            _value =
                part == MTextRulerPart.Tab ? format.TabStops[index].Position
                : part == MTextRulerPart.First ? format.FirstLineIndent
                : part == MTextRulerPart.Hanging ? format.HangingIndent
                : part == MTextRulerPart.Width ? Room
                : part == MTextRulerPart.ColumnWidth ? frame.ColumnWidth
                : part == MTextRulerPart.Gutter ? frame.Gutter
                : part == MTextRulerPart.Total ? frame.Width
                : HeightOf(index, natural);

            if (part == MTextRulerPart.Tab)
            {
                _tab = format.TabStops[index];
                _tabAt = _tab.Position;
            }

            Editor.BeginChange();
            return true;
        }

        /// <summary>The pointer moved during a drag: the value follows it, snapped and kept in its range, shown at once.</summary>
        internal void Move(Point point)
        {
            if (_drag == MTextRulerPart.None)
            {
                return;
            }

            CadMTextFrame frame = Frame;
            double minor = Minor;
            double across = (point.X - ZeroOf(CaretColumn)) / Scale;

            switch (_drag)
            {
                case MTextRulerPart.Tab:
                    double y = point.Y - RulerTop;

                    if (y < -12.0 || y > Height + 12.0)
                    {
                        if (!double.IsNaN(_tabAt))
                        {
                            double from = _tabAt;
                            Editor.SetFormat(f => f.WithTabStops(f.TabStops.Where(s => !Same(s.Position, from))));
                            _tabAt = double.NaN;
                        }

                        return;
                    }

                    _value = Clamp(Snap(across), minor, Room);
                    MoveStop(_value);
                    return;
                case MTextRulerPart.First:
                    _value = Clamp(Snap(across), 0.0, Room - minor);
                    double first = _value;
                    Editor.SetFormat(f => f.WithIndents(first, f.HangingIndent, f.RightIndent));
                    return;
                case MTextRulerPart.Hanging:
                    _value = Clamp(Snap(across), 0.0, Room - minor);
                    double hanging = _value;
                    Editor.SetFormat(f => f.WithIndents(f.FirstLineIndent, hanging, f.RightIndent));
                    return;
                case MTextRulerPart.Width:
                    _value = Math.Max(minor, Snap((point.X - Zero) / Scale));
                    Editor.SetFrame(frame.WithWidth(_value));
                    return;
                case MTextRulerPart.ColumnWidth:
                    _value = Math.Max(minor, Snap((point.X - Zero) / Scale));
                    Editor.SetFrame(frame.WithColumnWidth(_value));
                    return;
                case MTextRulerPart.Gutter:
                    _value = Math.Max(0.0, Snap(((point.X - Zero) / Scale) - frame.ColumnWidth));
                    Editor.SetFrame(frame.WithGutter(_value));
                    return;
                case MTextRulerPart.Total:
                    int count = frame.ColumnCount;
                    _value = Math.Max((count * minor) + ((count - 1) * frame.Gutter), Snap((point.X - Zero) / Scale));
                    Editor.SetFrame(frame.WithWidth(_value));
                    return;
                case MTextRulerPart.Height:
                    _value = Math.Max(Editor.Height, Snap((point.Y - BoxTop) / Scale));
                    Editor.SetFrame(
                        GripCount > 1 || (frame.ColumnType == DxfColumnType.DynamicColumns && !frame.AutoHeight)
                            ? frame.WithColumnHeight(_dragIndex, _value)
                            : frame.WithHeight(_value)
                    );
                    return;
            }
        }

        /// <summary>The button released: the drag is one step of the editor's undo, a stop off the ruler removed.</summary>
        internal void Release()
        {
            if (_drag == MTextRulerPart.None)
            {
                return;
            }

            _drag = MTextRulerPart.None;
            Editor.EndChange(true);
        }

        /// <summary>Esc during a drag: what it moved is put back.</summary>
        /// <returns>Whether a drag was put back.</returns>
        internal bool Cancel()
        {
            if (_drag == MTextRulerPart.None)
            {
                return false;
            }

            _drag = MTextRulerPart.None;
            Editor.EndChange(false);
            return true;
        }

        private static bool Same(double a, double b) => Math.Abs(a - b) <= Tolerance.Default.EqualPoint;

        private static double Clamp(double value, double low, double high) =>
            Math.Max(low, Math.Min(Math.Max(low, high), value));

        private static bool Near(double x, double arrows) => x >= arrows - 14.0 && x <= arrows + 2.0;

        private string Text(double value) =>
            value.ToString("F" + Math.Max(0, Math.Min(8, _model.Decimals)), CultureInfo.InvariantCulture);

        /// <summary>A click on the ruler: a stop of the box's kind at the nearest minor tick, on the selected paragraphs.</summary>
        private void AddStop(Point point)
        {
            double at = Snap((point.X - ZeroOf(CaretColumn)) / Scale);

            if (!(at > 0.0) || at > Room + 1E-9)
            {
                return;
            }

            var stop = new CadMTextTabStop(at, TabType);
            Editor.SetFormat(f =>
                f.TabStops.Any(s => Same(s.Position, at)) ? f : f.WithTabStops(f.TabStops.Concat(new[] { stop }))
            );
        }

        /// <summary>The stop dragged put at a place: moved there, or put back from off the ruler.</summary>
        private void MoveStop(double place)
        {
            double from = _tabAt;

            if (!double.IsNaN(from) && Same(from, place))
            {
                return;
            }

            var moved = new CadMTextTabStop(place, _tab.Kind, _tab.DecimalSeparator);
            Editor.SetFormat(f =>
            {
                bool had = double.IsNaN(from) || f.TabStops.Any(s => Same(s.Position, from));
                return had
                    ? f.WithTabStops(
                        f.TabStops.Where(s => double.IsNaN(from) || !Same(s.Position, from)).Concat(new[] { moved })
                    )
                    : f;
            });
            _tabAt = place;
        }
    }
}
