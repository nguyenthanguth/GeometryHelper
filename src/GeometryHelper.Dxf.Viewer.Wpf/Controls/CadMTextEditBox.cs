using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The MTEXT editor's frame on the canvas, as the design draws it: the ruler on top, its diamond at the right dragging the
    /// frame's width, and under it the words where the canvas will draw them, at the size it shows them, with the caret and
    /// the selection, in a dark box with a dashed border. It draws and takes keys for a <see cref="TextEditorViewModel"/>;
    /// what the keys do is the editor's (<see cref="CadMTextEditor"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The words are laid out by the display's own layout (<see cref="CadMTextEditorLayout"/>), in the drawing's fonts: wrapped
    /// at the width, their lines spaced at least or exactly as the MTEXT says, their stacks stacked, their tabs, indents,
    /// paragraph spacing and columns set, the mask and the frame drawn, as AutoCAD's editor shows the words as they will look.
    /// The frame holds the MTEXT's box and all that is drawn, and is put with the MTEXT's attachment point on its own
    /// (<see cref="Anchor"/>). Nothing is drawn without a <see cref="Document"/>.
    /// </para>
    /// <para>
    /// Keys as AutoCAD's editor: letters type, Enter a new paragraph, Backspace and Delete, the arrows, Home and End (Shift
    /// selecting) along the lines as they wrap, Ctrl+A, Ctrl+B, Ctrl+I, Ctrl+U, Ctrl+Z and Ctrl+Y, Ctrl+C, Ctrl+X and Ctrl+V as
    /// plain text, Ctrl+Enter closes keeping the words, Esc asks (<see cref="EscapePressed"/>). In a table cell, as AutoCAD's:
    /// Tab keeps the words and goes to the next cell, Shift+Tab to the one before, Enter to the one below, and Alt+Enter starts
    /// a new paragraph.
    /// </para>
    /// </remarks>
    public sealed class CadMTextEditBox : FrameworkElement
    {
        /// <summary>The space between the frame and the words, in device-independent pixels.</summary>
        public const double Pad = 6.0;

        /// <summary>The height of the ruler.</summary>
        public const double RulerHeight = 22.0;

        private static readonly Brush Back = Freeze(new SolidColorBrush(Color.FromArgb(0xD9, 0, 0, 0)));
        private static readonly Brush RulerBack = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0xDD, 0xE3)));
        private static readonly Brush Dark = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x24)));
        private static readonly Brush Accent = Freeze(new SolidColorBrush(Color.FromRgb(0x3D, 0x9B, 0xFF)));
        private static readonly Brush Selection = Freeze(new SolidColorBrush(Color.FromArgb(0x99, 0x3D, 0x9B, 0xFF)));
        private static readonly Brush MaskBack = Freeze(new SolidColorBrush(Color.FromRgb(0, 0, 0)));
        private static readonly Pen Border = FreezePen(
            new Pen(new SolidColorBrush(Color.FromRgb(0x9A, 0xA4, 0xB1)), 1.0) { DashStyle = DashStyles.Dash }
        );
        private static readonly Pen Tick = FreezePen(
            new Pen(new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x24)), 1.0)
        );
        private static readonly Pen Caret = FreezePen(new Pen(Brushes.White, 2.0));

        private readonly Dictionary<int, Brush> _brushes = new Dictionary<int, Brush>();
        private readonly Dictionary<int, Pen> _pens = new Dictionary<int, Pen>();
        private Dictionary<string, FormattedText> _lastFrame = new Dictionary<string, FormattedText>();
        private Dictionary<string, FormattedText> _thisFrame = new Dictionary<string, FormattedText>();
        private TextEditorViewModel _model;
        private CadDocument _document;
        private CadMTextEditorLayout _layout;
        private bool _stale = true;
        private int _defaultRgb = 0xFFFFFF;
        private int _layerRgb = 0xFFFFFF;
        private double _pixelsPerUnit = 1.0;
        private double _left;
        private double _top;
        private double _areaWidth;
        private double _textHeight;
        private bool _draggingWidth;
        private bool _selecting;

        /// <summary>Makes the frame; it takes the keyboard focus when shown.</summary>
        public CadMTextEditBox()
        {
            Focusable = true;
            FocusVisualStyle = null;
            Cursor = Cursors.IBeam;
        }

        /// <summary>Raised on Esc: the window asks whether to keep the changes.</summary>
        public event EventHandler EscapePressed;

        /// <summary>Raised on Ctrl+Enter: the window closes the editor keeping the words.</summary>
        public event EventHandler CloseRequested;

        /// <summary>Gets or sets the editor drawn; null for none.</summary>
        public TextEditorViewModel Model
        {
            get => _model;
            set
            {
                if (_model != null)
                {
                    _model.Editor.Changed -= OnEditorChanged;
                    _model.PropertyChanged -= OnModelChanged;
                }

                _model = value;
                _layout = null;

                if (_model != null)
                {
                    _model.Editor.Changed += OnEditorChanged;
                    _model.PropertyChanged += OnModelChanged;
                }

                Restale();
            }
        }

        /// <summary>Gets or sets the document the MTEXT is drawn in, whose fonts and layout the words are set by; null draws nothing.</summary>
        public CadDocument Document
        {
            get => _document;
            set
            {
                if (!ReferenceEquals(_document, value))
                {
                    _document = value;
                    _layout = null;
                    Restale();
                }
            }
        }

        /// <summary>Gets the layout of the words as the frame last set them, in the MTEXT's own frame and drawing units; null before there is one.</summary>
        public CadMTextEditorLayout TextLayout
        {
            get
            {
                Relayout();
                return _layout;
            }
        }

        /// <summary>Gets or sets how many device-independent pixels a drawing unit is on the canvas now.</summary>
        public double PixelsPerUnit
        {
            get => _pixelsPerUnit;
            set
            {
                _pixelsPerUnit = value > 0.0 && !double.IsInfinity(value) ? value : 1.0;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        /// <summary>Gets or sets the colour of letters of no colour of their own: the MTEXT's as the canvas draws it, 0xRRGGBB.</summary>
        public int DefaultRgb
        {
            get => _defaultRgb;
            set
            {
                if (_defaultRgb != value)
                {
                    _defaultRgb = value;
                    Restale();
                }
            }
        }

        /// <summary>Gets or sets the colour of letters coded ByLayer: the layer's, 0xRRGGBB.</summary>
        public int LayerRgb
        {
            get => _layerRgb;
            set
            {
                if (_layerRgb != value)
                {
                    _layerRgb = value;
                    Restale();
                }
            }
        }

        /// <summary>Gets where, in the frame, the MTEXT's attachment point is: the frame is put so that this point is on it.</summary>
        public Point Anchor
        {
            get
            {
                Relayout();
                return new Point(PixelX(0.0), PixelY(0.0));
            }
        }

        private double Top => (_model == null || _model.IsRulerShown ? RulerHeight : 0.0) + Pad;

        /// <summary>Gets the place of the editor nearest a point of the frame, for a click.</summary>
        /// <param name="point">The point.</param>
        /// <returns>The place.</returns>
        public int PlaceAt(Point point)
        {
            Relayout();
            return _layout == null ? 0 : _layout.PlaceAt(UnitX(point.X), UnitY(point.Y));
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            Relayout();
            return new Size(_areaWidth + 2.0 * Pad, Top + _textHeight + Pad + 8.0);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext dc)
        {
            if (_model == null)
            {
                return;
            }

            Relayout();
            double width = _areaWidth + 2.0 * Pad;
            double top = _model.IsRulerShown ? RulerHeight : 0.0;
            dc.DrawRectangle(Back, Border, new Rect(0.5, top + 0.5, width - 1.0, _textHeight + 2.0 * Pad));

            if (_model.IsRulerShown)
            {
                DrawRuler(dc, width);
            }

            if (_layout != null)
            {
                // The mask under everything, the selection over it, then the frame and the words as the canvas draws them.
                foreach (DisplayItem2 part in _layout.Parts)
                {
                    if (part is DisplayFill2 fill)
                    {
                        DrawFill(dc, fill);
                    }
                }

                DrawSelection(dc);

                foreach (DisplayItem2 part in _layout.Parts)
                {
                    if (part is DisplayPolyline2 line)
                    {
                        DrawChain(dc, line.Xy, line.Closed, PenOf(line.Style.Rgb));
                    }
                    else if (part is DisplayText2 text)
                    {
                        DrawText(dc, text);
                    }
                }

                Dictionary<string, FormattedText> drawn = _lastFrame;
                _lastFrame = _thisFrame;
                _thisFrame = drawn;
                _thisFrame.Clear();

                if (IsKeyboardFocused && _model.Editor.SelectionLength == 0)
                {
                    int caret = _model.Editor.Caret;
                    int line = _layout.LineOfPlace(caret);
                    double x = PixelX(_layout.XOf(caret));
                    dc.DrawLine(
                        Caret,
                        new Point(x, PixelY(_layout.TopOf(line))),
                        new Point(x, PixelY(_layout.BottomOf(line)))
                    );
                }
            }

            // The arrow under the middle of the frame, which AutoCAD drags for the height of columns; drawn as the design has it.
            double mid = width / 2.0;
            double bottom = top + _textHeight + 2.0 * Pad;
            var arrow = new StreamGeometry();

            using (StreamGeometryContext g = arrow.Open())
            {
                g.BeginFigure(new Point(mid - 4.0, bottom + 1.0), true, true);
                g.LineTo(new Point(mid + 4.0, bottom + 1.0), true, false);
                g.LineTo(new Point(mid, bottom + 7.0), true, false);
            }

            dc.DrawGeometry(Border.Brush, null, arrow);
        }

        /// <inheritdoc/>
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            InvalidateVisual();
        }

        /// <inheritdoc/>
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            InvalidateVisual();
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_model == null || e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            Point at = e.GetPosition(this);

            if (_model.IsRulerShown && at.Y < RulerHeight && Math.Abs(at.X - BoxRight) <= 8.0)
            {
                _draggingWidth = true;
                CaptureMouse();
            }
            else if (e.ClickCount == 2)
            {
                SelectWord(PlaceAt(at));
            }
            else
            {
                _model.Editor.MoveTo(PlaceAt(at), (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
                _selecting = true;
                CaptureMouse();
            }

            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_model == null)
            {
                return;
            }

            Point at = e.GetPosition(this);

            if (_draggingWidth)
            {
                double left = _layout != null ? PixelX(_layout.Box[0]) : Pad;
                _model.Width = Math.Max(1.0, (at.X - left) / _pixelsPerUnit);
            }
            else if (_selecting && e.LeftButton == MouseButtonState.Pressed)
            {
                _model.Editor.MoveTo(PlaceAt(at), true);
            }

            Cursor =
                _model.IsRulerShown && at.Y < RulerHeight && Math.Abs(at.X - BoxRight) <= 8.0
                    ? Cursors.SizeWE
                    : Cursors.IBeam;
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            _draggingWidth = false;
            _selecting = false;
            ReleaseMouseCapture();
        }

        /// <inheritdoc/>
        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            if (_model != null && !string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]))
            {
                _model.Editor.Type(e.Text);
                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_model == null)
            {
                return;
            }

            e.Handled = HandleKey(e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
        }

        /// <summary>Does what a key does in the editor, as the remarks say.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held.</param>
        /// <returns>Whether the key was taken.</returns>
        public bool HandleKey(Key key, ModifierKeys modifiers)
        {
            CadMTextEditor editor = _model?.Editor;

            if (editor == null)
            {
                return false;
            }

            bool shift = (modifiers & ModifierKeys.Shift) != 0;
            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool inCell = _model.Edit.IsTableCell;

            switch (key)
            {
                case System.Windows.Input.Key.Escape:
                    EscapePressed?.Invoke(this, EventArgs.Empty);
                    return true;
                case System.Windows.Input.Key.Tab when inCell && !control:
                    _model.Edit.Move = shift ? CadTableCellMove.Previous : CadTableCellMove.Next;
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    return true;
                case System.Windows.Input.Key.Enter when inCell && !control && (modifiers & ModifierKeys.Alt) == 0:
                    _model.Edit.Move = CadTableCellMove.Down;
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    return true;
                case System.Windows.Input.Key.Enter when control:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    return true;
                case System.Windows.Input.Key.Enter:
                    editor.Type("\n");
                    return true;
                case System.Windows.Input.Key.Back:
                    editor.Backspace();
                    return true;
                case System.Windows.Input.Key.Delete:
                    editor.Delete();
                    return true;
                case System.Windows.Input.Key.Left:
                    editor.MoveTo(
                        !shift && editor.SelectionLength > 0 ? editor.SelectionStart : editor.Caret - 1,
                        shift
                    );
                    return true;
                case System.Windows.Input.Key.Right:
                    editor.MoveTo(
                        !shift && editor.SelectionLength > 0
                            ? editor.SelectionStart + editor.SelectionLength
                            : editor.Caret + 1,
                        shift
                    );
                    return true;
                case System.Windows.Input.Key.Home:
                case System.Windows.Input.Key.End:
                case System.Windows.Input.Key.Up:
                case System.Windows.Input.Key.Down:
                    Relayout();

                    if (_layout == null)
                    {
                        return true;
                    }

                    int line = _layout.LineOfPlace(editor.Caret);

                    if (key == System.Windows.Input.Key.Home)
                    {
                        editor.MoveTo(control ? 0 : _layout.LineStart(line), shift);
                    }
                    else if (key == System.Windows.Input.Key.End)
                    {
                        editor.MoveTo(control ? editor.Length : _layout.LineEnd(line), shift);
                    }
                    else
                    {
                        int at = line + (key == System.Windows.Input.Key.Up ? -1 : 1);

                        if (at >= 0 && at < _layout.LineCount)
                        {
                            editor.MoveTo(_layout.PlaceOnLine(at, _layout.XOf(editor.Caret)), shift);
                        }
                    }

                    return true;
            }

            if (!control)
            {
                return false;
            }

            switch (key)
            {
                case System.Windows.Input.Key.A:
                    editor.SelectAll();
                    return true;
                case System.Windows.Input.Key.B:
                    _model.ToggleCommand.Execute("Bold");
                    return true;
                case System.Windows.Input.Key.I:
                    _model.ToggleCommand.Execute("Italic");
                    return true;
                case System.Windows.Input.Key.U:
                    _model.ToggleCommand.Execute("Underline");
                    return true;
                case System.Windows.Input.Key.Z:
                    editor.Undo();
                    return true;
                case System.Windows.Input.Key.Y:
                    editor.Redo();
                    return true;
                case System.Windows.Input.Key.C:
                case System.Windows.Input.Key.X:
                    if (editor.SelectionLength > 0)
                    {
                        TrySetClipboard(editor.SelectedText.Replace("\n", "\r\n"));

                        if (key == System.Windows.Input.Key.X)
                        {
                            editor.Delete();
                        }
                    }

                    return true;
                case System.Windows.Input.Key.V:
                    string text = TryGetClipboard();

                    if (!string.IsNullOrEmpty(text))
                    {
                        editor.Type(text);
                    }

                    return true;
                case System.Windows.Input.Key.Space when shift:
                    editor.Type(" ");
                    return true;
                default:
                    return false;
            }
        }

        private double BoxRight => _layout != null ? PixelX(_layout.Box[2]) : Pad + _areaWidth;

        private double PixelX(double x) => Pad + ((x - _left) * _pixelsPerUnit);

        private double PixelY(double y) => Top + ((_top - y) * _pixelsPerUnit);

        private double UnitX(double x) => _left + ((x - Pad) / _pixelsPerUnit);

        private double UnitY(double y) => _top - ((y - Top) / _pixelsPerUnit);

        private void Restale()
        {
            _stale = true;
            InvalidateMeasure();
            InvalidateVisual();
        }

        /// <summary>Lays the words out again where they or the box changed: the display's layout, the frame around all it draws.</summary>
        private void Relayout()
        {
            if (_model == null || _document == null)
            {
                _layout = null;
                _left = _top = 0.0;
                _areaWidth = 40.0;
                _textHeight = 0.0;
                return;
            }

            if (_stale || _layout == null)
            {
                if (_layout == null)
                {
                    _layout = new CadMTextEditorLayout(_document, _model.Edit);
                }

                _layout.Update(
                    _model.Editor,
                    _model.Width,
                    _model.Attachment,
                    _model.LineSpacing,
                    DefaultRgb,
                    LayerRgb
                );
                _stale = false;
            }

            double[] bounds = _layout.Bounds;
            _left = bounds[0];
            _top = bounds[3];
            _areaWidth = Math.Max(40.0, (bounds[2] - bounds[0]) * _pixelsPerUnit);
            _textHeight = Math.Max(0.0, (bounds[3] - bounds[1]) * _pixelsPerUnit);
        }

        private void SelectWord(int place)
        {
            CadMTextEditor editor = _model.Editor;
            int start = place;
            int end = place;

            while (start > 0 && IsWordLetter(editor.TextAt(start - 1)))
            {
                start--;
            }

            while (end < editor.Length && IsWordLetter(editor.TextAt(end)))
            {
                end++;
            }

            editor.Select(start, end - start);
        }

        private static bool IsWordLetter(string text) => text.Length > 0 && char.IsLetterOrDigit(text[0]);

        private void DrawRuler(DrawingContext dc, double width)
        {
            dc.DrawRectangle(RulerBack, Tick, new Rect(0.5, 0.5, width - 1.0, RulerHeight - 1.0));

            for (int i = 0; Pad + i * 10.0 < width - 2.0; i++)
            {
                double x = Math.Floor(Pad + i * 10.0) + 0.5;
                dc.DrawLine(Tick, new Point(x, i % 5 == 0 ? 11.0 : 15.0), new Point(x, RulerHeight - 1.0));
            }

            // The indents, first line above and paragraph below, at the left; the diamond of the width at the right.
            dc.DrawGeometry(Dark, null, Triangle(Pad, 1.0, Pad + 8.0, 1.0, Pad + 4.0, 7.0));
            dc.DrawGeometry(
                Dark,
                null,
                Triangle(Pad, RulerHeight - 1.0, Pad + 8.0, RulerHeight - 1.0, Pad + 4.0, RulerHeight - 7.0)
            );
            double right = BoxRight;
            var diamond = new StreamGeometry();

            using (StreamGeometryContext g = diamond.Open())
            {
                g.BeginFigure(new Point(right, 4.0), true, true);
                g.LineTo(new Point(right + 6.0, 11.0), true, false);
                g.LineTo(new Point(right, 18.0), true, false);
                g.LineTo(new Point(right - 6.0, 11.0), true, false);
            }

            dc.DrawGeometry(_draggingWidth ? Accent : Dark, null, diamond);
        }

        private static System.Windows.Media.Geometry Triangle(
            double x0,
            double y0,
            double x1,
            double y1,
            double x2,
            double y2
        )
        {
            var geometry = new StreamGeometry();

            using (StreamGeometryContext g = geometry.Open())
            {
                g.BeginFigure(new Point(x0, y0), true, true);
                g.LineTo(new Point(x1, y1), true, false);
                g.LineTo(new Point(x2, y2), true, false);
            }

            geometry.Freeze();
            return geometry;
        }

        /// <summary>The selection, place by place, each as high as its line's cell.</summary>
        private void DrawSelection(DrawingContext dc)
        {
            CadMTextEditor editor = _model.Editor;
            int from = editor.SelectionStart;
            int to = from + editor.SelectionLength;

            for (int q = from; q < to; q++)
            {
                int line = _layout.LineOfPlace(q);
                double x0 = PixelX(_layout.XOf(q));
                double x1 = PixelX(_layout.RightOf(q));
                double y0 = PixelY(_layout.TopOf(line));
                double y1 = PixelY(_layout.BottomOf(line));
                dc.DrawRectangle(Selection, null, new Rect(x0, y0, Math.Max(1.0, x1 - x0), Math.Max(1.0, y1 - y0)));
            }
        }

        /// <summary>A mask: in the canvas's black where it takes the drawing's background, else in its colour.</summary>
        private void DrawFill(DrawingContext dc, DisplayFill2 fill)
        {
            var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };

            using (StreamGeometryContext g = geometry.Open())
            {
                foreach (double[] ring in fill.Rings)
                {
                    if (ring.Length < 6)
                    {
                        continue;
                    }

                    g.BeginFigure(new Point(PixelX(ring[0]), PixelY(ring[1])), true, true);

                    for (int i = 2; i + 1 < ring.Length; i += 2)
                    {
                        g.LineTo(new Point(PixelX(ring[i]), PixelY(ring[i + 1])), false, false);
                    }
                }
            }

            dc.DrawGeometry(fill.IsMask ? MaskBack : BrushOf(fill.Style.Rgb, false), null, geometry);
        }

        private void DrawChain(DrawingContext dc, IReadOnlyList<double> xy, bool closed, Pen pen)
        {
            if (xy.Count < 4)
            {
                return;
            }

            var geometry = new StreamGeometry();

            using (StreamGeometryContext g = geometry.Open())
            {
                g.BeginFigure(new Point(PixelX(xy[0]), PixelY(xy[1])), false, closed);

                for (int i = 2; i + 1 < xy.Count; i += 2)
                {
                    g.LineTo(new Point(PixelX(xy[i]), PixelY(xy[i + 1])), true, false);
                }
            }

            dc.DrawGeometry(null, pen, geometry);
        }

        /// <summary>A run of letters as the canvas draws it: an SHX font's strokes, or a TrueType font a capital's height high, as wide as its width factor says, slanted.</summary>
        private void DrawText(DrawingContext dc, DisplayText2 text)
        {
            if (text.Strokes != null)
            {
                Pen pen = PenOf(text.Style.Rgb);

                foreach (double[] stroke in text.Strokes)
                {
                    DrawChain(dc, stroke, false, pen);
                }

                return;
            }

            double size = text.Height * _pixelsPerUnit / 0.72;

            if (!(size >= 0.5) || text.Text.Length == 0)
            {
                return;
            }

            // The runs of the lines a key did not change are drawn from the letters set for the last frame.
            string family =
                text.FontName == null ? "Arial"
                : System.IO.Path.HasExtension(text.FontName) ? System.IO.Path.GetFileNameWithoutExtension(text.FontName)
                : text.FontName;
            string key = string.Join(
                "\u0001",
                text.Text,
                family,
                size.ToString("R", CultureInfo.InvariantCulture),
                text.IsBold ? "b" : string.Empty,
                text.IsItalic ? "i" : string.Empty,
                text.Style.Rgb.ToString(CultureInfo.InvariantCulture)
            );

            if (
                !_lastFrame.TryGetValue(key, out FormattedText formatted) && !_thisFrame.TryGetValue(key, out formatted)
            )
            {
                var typeface = new Typeface(
                    new FontFamily(family),
                    text.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                    text.IsBold ? FontWeights.Bold : FontWeights.Normal,
                    FontStretches.Normal
                );
                formatted = new FormattedText(
                    text.Text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    size,
                    BrushOf(text.Style.Rgb, true),
                    1.0
                );
            }

            _thisFrame[key] = formatted;
            double x = PixelX(text.X);
            double baseline = PixelY(text.Y);
            double slant = Math.Tan(text.Oblique);
            dc.PushTransform(
                new MatrixTransform(
                    new Matrix(text.WidthFactor, 0.0, -slant, 1.0, x - (text.WidthFactor * x) + (slant * baseline), 0.0)
                )
            );
            dc.DrawText(formatted, new Point(x, baseline - formatted.Baseline));
            dc.Pop();
        }

        /// <summary>The brush of a colour; a colour too dark to read on the frame's black drawn white, as the canvas turns black to white.</summary>
        private Brush BrushOf(int rgb, bool readable)
        {
            if (readable && ((rgb >> 16) & 0xFF) + ((rgb >> 8) & 0xFF) + (rgb & 0xFF) < 60)
            {
                rgb = 0xFFFFFF;
            }

            if (!_brushes.TryGetValue(rgb, out Brush brush))
            {
                brush = Freeze(new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)));
                _brushes[rgb] = brush;
            }

            return brush;
        }

        private Pen PenOf(int rgb)
        {
            if (!_pens.TryGetValue(rgb, out Pen pen))
            {
                pen = FreezePen(new Pen(BrushOf(rgb, true), 1.0));
                _pens[rgb] = pen;
            }

            return pen;
        }

        private void OnEditorChanged(object sender, EventArgs e) => Restale();

        private void OnModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (
                e.PropertyName == nameof(TextEditorViewModel.IsRulerShown)
                || e.PropertyName == nameof(TextEditorViewModel.Attachment)
                || e.PropertyName == nameof(TextEditorViewModel.LineSpacing)
                || e.PropertyName == nameof(TextEditorViewModel.Width)
            )
            {
                Restale();
            }
        }

        private static void TrySetClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                GeometryHelperLog.Warn(
                    "CadMTextEditBox: the clipboard was busy; the words were not copied.",
                    exception
                );
            }
        }

        private static string TryGetClipboard()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                GeometryHelperLog.Warn("CadMTextEditBox: the clipboard was busy; nothing was pasted.", exception);
                return null;
            }
        }

        private static Brush Freeze(Brush brush)
        {
            brush.Freeze();
            return brush;
        }

        private static Pen FreezePen(Pen pen)
        {
            pen.Freeze();
            return pen;
        }
    }
}
