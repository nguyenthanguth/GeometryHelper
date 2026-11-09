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
    /// frame's width, and under it the words in their own looks (font, bold, italic, height, colour, lines, width, slant) at the
    /// size the canvas shows them, with the caret and the selection, in a dark box with a dashed border. It draws and takes keys
    /// for a <see cref="TextEditorViewModel"/>; what the keys do is the editor's (<see cref="CadMTextEditor"/>).
    /// </summary>
    /// <remarks>
    /// Keys as AutoCAD's editor: letters type, Enter a new paragraph, Backspace and Delete, the arrows, Home and End (Shift
    /// selecting), Ctrl+A, Ctrl+B, Ctrl+I, Ctrl+U, Ctrl+Z and Ctrl+Y, Ctrl+C, Ctrl+X and Ctrl+V as plain text, Ctrl+Enter
    /// closes keeping the words, Esc asks (<see cref="EscapePressed"/>). Lines are 1.25 times their tallest letters apart, as
    /// the canvas sets them.
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
        private static readonly Pen Border = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(0x9A, 0xA4, 0xB1)), 1.0) { DashStyle = DashStyles.Dash });
        private static readonly Pen Tick = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x24)), 1.0));
        private static readonly Pen Caret = FreezePen(new Pen(Brushes.White, 2.0));

        private readonly List<LineLayout> _lines = new List<LineLayout>();
        private TextEditorViewModel _model;
        private double _pixelsPerUnit = 1.0;
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

                if (_model != null)
                {
                    _model.Editor.Changed += OnEditorChanged;
                    _model.PropertyChanged += OnModelChanged;
                }

                InvalidateMeasure();
                InvalidateVisual();
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
        public int DefaultRgb { get; set; } = 0xFFFFFF;

        /// <summary>Gets or sets the colour of letters coded ByLayer: the layer's, 0xRRGGBB.</summary>
        public int LayerRgb { get; set; } = 0xFFFFFF;

        /// <summary>Gets where, in the frame, the MTEXT's attachment point is: the frame is put so that this point is on it.</summary>
        public Point Anchor
        {
            get
            {
                int attachment = _model?.Attachment ?? 1;
                int column = (attachment - 1) % 3;
                int row = (attachment - 1) / 3;
                double top = Top;
                return new Point(Pad + column * _areaWidth / 2.0, top + row * _textHeight / 2.0);
            }
        }

        private double Top => (_model == null || _model.IsRulerShown ? RulerHeight : 0.0) + Pad;

        /// <summary>Gets the place of the editor nearest a point of the frame, for a click.</summary>
        /// <param name="point">The point.</param>
        /// <returns>The place.</returns>
        public int PlaceAt(Point point)
        {
            if (_lines.Count == 0)
            {
                return 0;
            }

            LineLayout line = _lines[_lines.Count - 1];

            foreach (LineLayout candidate in _lines)
            {
                if (point.Y <= candidate.Baseline + candidate.Tallest * 0.25)
                {
                    line = candidate;
                    break;
                }
            }

            for (int i = 0; i < line.Cells.Count; i++)
            {
                CellLayout cell = line.Cells[i];

                if (point.X < cell.X + cell.Width / 2.0)
                {
                    return cell.Index;
                }
            }

            return line.End;
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            Layout();
            return new Size(_areaWidth + 2.0 * Pad, Top + _textHeight + Pad + 8.0);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext dc)
        {
            if (_model == null)
            {
                return;
            }

            double width = _areaWidth + 2.0 * Pad;
            double top = _model.IsRulerShown ? RulerHeight : 0.0;
            dc.DrawRectangle(Back, Border, new Rect(0.5, top + 0.5, width - 1.0, _textHeight + 2.0 * Pad));

            if (_model.IsRulerShown)
            {
                DrawRuler(dc, width);
            }

            CadMTextEditor editor = _model.Editor;
            int from = editor.SelectionStart;
            int to = from + editor.SelectionLength;

            foreach (LineLayout line in _lines)
            {
                foreach (CellLayout cell in line.Cells)
                {
                    if (cell.Index >= from && cell.Index < to)
                    {
                        dc.DrawRectangle(Selection, null, new Rect(cell.X, line.Baseline - line.Tallest, Math.Max(1.0, cell.Width), line.Tallest * 1.25));
                    }
                }

                foreach (RunLayout run in line.Runs)
                {
                    DrawRun(dc, run, line);
                }
            }

            if (IsKeyboardFocused && editor.SelectionLength == 0)
            {
                Point caret = CaretPoint(out double height);
                dc.DrawLine(Caret, new Point(caret.X, caret.Y - height), new Point(caret.X, caret.Y + height * 0.25));
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

            if (_model.IsRulerShown && at.Y < RulerHeight && Math.Abs(at.X - (Pad + _areaWidth)) <= 8.0)
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
                _model.Width = Math.Max(1.0, (at.X - Pad) / _pixelsPerUnit);
                InvalidateMeasure();
                InvalidateVisual();
            }
            else if (_selecting && e.LeftButton == MouseButtonState.Pressed)
            {
                _model.Editor.MoveTo(PlaceAt(at), true);
            }

            Cursor = _model.IsRulerShown && at.Y < RulerHeight && Math.Abs(at.X - (Pad + _areaWidth)) <= 8.0 ? Cursors.SizeWE : Cursors.IBeam;
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

            switch (key)
            {
                case System.Windows.Input.Key.Escape:
                    EscapePressed?.Invoke(this, EventArgs.Empty);
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
                    editor.MoveTo(!shift && editor.SelectionLength > 0 ? editor.SelectionStart : editor.Caret - 1, shift);
                    return true;
                case System.Windows.Input.Key.Right:
                    editor.MoveTo(!shift && editor.SelectionLength > 0 ? editor.SelectionStart + editor.SelectionLength : editor.Caret + 1, shift);
                    return true;
                case System.Windows.Input.Key.Home:
                    editor.MoveTo(control ? 0 : LineOf(editor.Caret).Start, shift);
                    return true;
                case System.Windows.Input.Key.End:
                    editor.MoveTo(control ? editor.Length : LineOf(editor.Caret).End, shift);
                    return true;
                case System.Windows.Input.Key.Up:
                case System.Windows.Input.Key.Down:
                    {
                        int at = _lines.IndexOf(LineOf(editor.Caret)) + (key == System.Windows.Input.Key.Up ? -1 : 1);

                        if (at >= 0 && at < _lines.Count)
                        {
                            Point caret = CaretPoint(out _);
                            editor.MoveTo(PlaceAt(new Point(caret.X, _lines[at].Baseline)), shift);
                        }

                        return true;
                    }
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
                    editor.Type(" ");
                    return true;
                default:
                    return false;
            }
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
            dc.DrawGeometry(Dark, null, Triangle(Pad, RulerHeight - 1.0, Pad + 8.0, RulerHeight - 1.0, Pad + 4.0, RulerHeight - 7.0));
            double right = Pad + _areaWidth;
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

        private static System.Windows.Media.Geometry Triangle(double x0, double y0, double x1, double y1, double x2, double y2)
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

        private void DrawRun(DrawingContext dc, RunLayout run, LineLayout line)
        {
            double rise = run.Look.Align == 1 ? (line.Tallest - run.Height) / 2.0 : run.Look.Align == 2 ? line.Tallest - run.Height : 0.0;
            double baseline = line.Baseline - rise;
            FormattedText text = Formatted(run.Text, run.Look, run.Height, RunBrush(run.Look));
            double slant = run.Look.Oblique.HasValue ? Math.Tan(run.Look.Oblique.Value * Math.PI / 180.0) : 0.0;
            dc.PushTransform(new MatrixTransform(new Matrix(run.WidthFactor, 0.0, -slant, 1.0, run.X + slant * baseline, 0.0)));
            dc.DrawText(text, new Point(0.0, baseline - text.Baseline));
            dc.Pop();
        }

        private Brush RunBrush(CadMTextLook look)
        {
            int rgb = DefaultRgb;

            if (look.TrueColor.HasValue)
            {
                rgb = look.TrueColor.Value;
            }
            else if (look.ColorIndex.HasValue && look.ColorIndex.Value >= 1 && look.ColorIndex.Value <= 255)
            {
                rgb = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)look.ColorIndex.Value).ColorValue & 0xFFFFFF;
            }
            else if (look.ColorIndex == 256)
            {
                rgb = LayerRgb;
            }

            // A colour too dark to read on the frame's black is drawn white, as the canvas turns black to white.
            if (((rgb >> 16) & 0xFF) + ((rgb >> 8) & 0xFF) + (rgb & 0xFF) < 60)
            {
                rgb = 0xFFFFFF;
            }

            return Freeze(new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)));
        }

        private FormattedText Formatted(string text, CadMTextLook look, double height, Brush brush)
        {
            string family = look.Font != null && !look.IsShx ? look.Font : _model.StyleIsShx || _model.StyleFont == null ? "Arial" : _model.StyleFont;
            var typeface = new Typeface(new FontFamily(family), look.Italic ? FontStyles.Italic : FontStyles.Normal, look.Bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);

            // Letters as high as the canvas sets them: a capital of a TrueType font is about 0.72 of the size.
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, Math.Max(1.0, height / 0.72), brush, 1.0);
            var decorations = new TextDecorationCollection();

            if (look.Underline)
            {
                decorations.Add(TextDecorations.Underline);
            }

            if (look.Overline)
            {
                decorations.Add(TextDecorations.OverLine);
            }

            if (look.Strikethrough)
            {
                decorations.Add(TextDecorations.Strikethrough);
            }

            if (decorations.Count > 0)
            {
                formatted.SetTextDecorations(decorations);
            }

            return formatted;
        }

        /// <summary>Lays the words out: each paragraph a line, each run measured, each place given its left and width.</summary>
        private void Layout()
        {
            _lines.Clear();

            if (_model == null)
            {
                _areaWidth = 0.0;
                _textHeight = 0.0;
                return;
            }

            CadMTextEditor editor = _model.Editor;
            double baseHeight = editor.Height * _pixelsPerUnit;
            double spacing = _model.LineSpacing;
            var line = new LineLayout { Start = 0 };
            int i = 0;

            while (true)
            {
                if (i >= editor.Length || editor.TextAt(i) == "\n")
                {
                    line.End = i;
                    _lines.Add(line);

                    if (i >= editor.Length)
                    {
                        break;
                    }

                    line = new LineLayout { Start = i + 1 };
                    i++;
                    continue;
                }

                // A run: the places of one look in a row, measured place by place.
                CadMTextLook look = editor.LookAt(i);
                int start = i;
                var letters = new System.Text.StringBuilder();

                while (i < editor.Length && editor.TextAt(i) != "\n" && editor.LookAt(i).Equals(look))
                {
                    letters.Append(editor.TextAt(i));
                    i++;
                }

                double height = (look.Height ?? editor.Height) * _pixelsPerUnit;
                double factor = look.WidthFactor ?? 1.0;
                var run = new RunLayout { Text = letters.ToString(), Look = look, Height = height, WidthFactor = factor, X = line.Width };
                double pen = line.Width;
                int offset = 0;

                for (int k = start; k < i; k++)
                {
                    string cell = editor.TextAt(k);
                    offset += cell.Length;
                    double reach = Formatted(run.Text.Substring(0, offset), look, height, Brushes.White).WidthIncludingTrailingWhitespace * factor;
                    line.Cells.Add(new CellLayout { Index = k, X = pen, Width = line.Width + reach - pen });
                    pen = line.Width + reach;
                }

                line.Runs.Add(run);
                line.Width = pen;
                line.Tallest = Math.Max(line.Tallest, height);
            }

            double y = 0.0;

            for (int k = 0; k < _lines.Count; k++)
            {
                LineLayout each = _lines[k];
                each.Tallest = each.Tallest > 0.0 ? each.Tallest : baseHeight;
                y += k == 0 ? each.Tallest : 1.25 * spacing * each.Tallest;
                each.Baseline = y;
            }

            double widest = 0.0;

            foreach (LineLayout each in _lines)
            {
                widest = Math.Max(widest, each.Width);
            }

            _areaWidth = Math.Max(Math.Max(_model.Width * _pixelsPerUnit, widest), 40.0);
            _textHeight = y + (_lines.Count > 0 ? _lines[_lines.Count - 1].Tallest * 0.25 : 0.0);
            double top = Top;

            // Each line put in the frame by its paragraph's alignment, and down below the ruler.
            for (int k = 0; k < _lines.Count; k++)
            {
                LineLayout each = _lines[k];
                char alignment = AlignmentOf(each.Start);
                double shift = Pad + (alignment == 'c' ? (_areaWidth - each.Width) / 2.0 : alignment == 'r' ? _areaWidth - each.Width : 0.0);
                each.Baseline += top;

                foreach (CellLayout cell in each.Cells)
                {
                    cell.X += shift;
                }

                foreach (RunLayout run in each.Runs)
                {
                    run.X += shift;
                }

                each.Left = shift;
            }
        }

        private char AlignmentOf(int place)
        {
            CadMTextEditor editor = _model.Editor;

            // The editor gives the alignment of the paragraph of its caret; the frame asks it of each line's.
            CadMTextContent content = editor.Content;
            int paragraph = editor.ParagraphOf(place);
            char alignment = paragraph < content.Paragraphs.Count ? content.Paragraphs[paragraph].Alignment : '\0';
            return alignment == '\0' ? editor.DefaultAlignment : alignment;
        }

        private LineLayout LineOf(int place)
        {
            foreach (LineLayout line in _lines)
            {
                if (place >= line.Start && place <= line.End)
                {
                    return line;
                }
            }

            return _lines.Count > 0 ? _lines[_lines.Count - 1] : new LineLayout();
        }

        private Point CaretPoint(out double height)
        {
            CadMTextEditor editor = _model.Editor;
            LineLayout line = LineOf(editor.Caret);
            height = line.Tallest;

            foreach (CellLayout cell in line.Cells)
            {
                if (cell.Index == editor.Caret)
                {
                    return new Point(cell.X, line.Baseline);
                }
            }

            double x = line.Cells.Count > 0 ? line.Cells[line.Cells.Count - 1].X + line.Cells[line.Cells.Count - 1].Width : line.Left;
            return new Point(x, line.Baseline);
        }

        private void OnEditorChanged(object sender, EventArgs e)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }

        private void OnModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TextEditorViewModel.IsRulerShown) || e.PropertyName == nameof(TextEditorViewModel.Attachment)
                || e.PropertyName == nameof(TextEditorViewModel.LineSpacing) || e.PropertyName == nameof(TextEditorViewModel.Width))
            {
                InvalidateMeasure();
                InvalidateVisual();
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
                GeometryHelperLog.Warn("CadMTextEditBox: the clipboard was busy; the words were not copied.", exception);
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

        private sealed class LineLayout
        {
            public int Start { get; set; }

            public int End { get; set; }

            public double Width { get; set; }

            public double Left { get; set; }

            public double Tallest { get; set; }

            public double Baseline { get; set; }

            public List<CellLayout> Cells { get; } = new List<CellLayout>();

            public List<RunLayout> Runs { get; } = new List<RunLayout>();
        }

        private sealed class CellLayout
        {
            public int Index { get; set; }

            public double X { get; set; }

            public double Width { get; set; }
        }

        private sealed class RunLayout
        {
            public string Text { get; set; }

            public CadMTextLook Look { get; set; }

            public double Height { get; set; }

            public double WidthFactor { get; set; }

            public double X { get; set; }
        }
    }
}
