using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The MTEXT editor's frame on the canvas, as the design draws it: the ruler on top (the tab type box at its left end,
    /// the indents and tab stops of the caret's paragraph, the arrows of the width, with columns a column's width, the gutter
    /// edge and the total), the height grips under the frame, and the words where the canvas will draw them, at the size it
    /// shows them, with the caret and the selection, in a dark box with a dashed border. A click on the ruler adds a tab stop
    /// and a drag moves what it holds, one step of the editor's undo each, Esc during a drag putting it back; a right-click
    /// on the ruler gives Set Mtext Width and Height, elsewhere the editor's menu. It draws and takes keys for a
    /// <see cref="TextEditorViewModel"/>; what the keys do is the editor's (<see cref="CadMTextEditor"/>).
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
    /// plain text, Alt+Enter a column break in an MTEXT in columns, Ctrl+Enter closes keeping the words, Esc puts back a drag
    /// of the ruler or else asks (<see cref="EscapePressed"/>). In a table cell, as AutoCAD's:
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
        private static readonly Brush TypeBoxBack = Freeze(new SolidColorBrush(Color.FromRgb(0xC3, 0xC8, 0xD0)));
        private static readonly Brush GutterBack = Freeze(new SolidColorBrush(Color.FromRgb(0xAE, 0xB4, 0xBD)));
        private static readonly Brush Active = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x6F, 0xD1)));
        private static readonly Brush TipBack = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x3D)));
        private static readonly Brush TipText = Freeze(new SolidColorBrush(Color.FromRgb(0xE6, 0xE9, 0xED)));
        private static readonly Brush Faint = Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0x0B, 0x12, 0x20)));
        private static readonly Pen RulerEdge = FreezePen(
            new Pen(new SolidColorBrush(Color.FromRgb(0x5B, 0x65, 0x73)), 1.0)
        );
        private static readonly Pen TipEdge = FreezePen(
            new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x41, 0x4C)), 1.0)
        );
        private static readonly Pen Guide = FreezePen(
            new Pen(new SolidColorBrush(Color.FromRgb(0x3D, 0x9B, 0xFF)), 1.0) { DashStyle = DashStyles.Dash }
        );
        private static readonly Pen OldOutline = FreezePen(
            new Pen(new SolidColorBrush(Color.FromRgb(0x5B, 0x65, 0x73)), 1.0) { DashStyle = DashStyles.Dash }
        );

        private readonly System.Windows.Threading.DispatcherTimer _tipTimer =
            new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };

        private MTextRuler _ruler;
        private MTextRulerPart _hover = MTextRulerPart.None;
        private int _hoverIndex;
        private Point _pointer;
        private string _tip;
        private Rect _oldOutline = Rect.Empty;
        private bool _selecting;

        // The line the caret stands at the end of, where End or a click past the line's end put it at a break between two
        // letters with no space, whose place is the first of the next line; -1 where it stands on its place's own line.
        private int _endOfLine = -1;
        private int _endPlace = -1;

        /// <summary>Makes the frame; it takes the keyboard focus when shown.</summary>
        public CadMTextEditBox()
        {
            Focusable = true;
            FocusVisualStyle = null;
            Cursor = Cursors.IBeam;
            _tipTimer.Tick += (s, e) =>
            {
                _tipTimer.Stop();
                _tip = _ruler?.TipOf(_hover, _hoverIndex, NaturalHeight);
                InvalidateVisual();
            };
        }

        /// <summary>Gets the ruler's logic, for a test to press and drag it as the mouse does; null without a model.</summary>
        internal MTextRuler Ruler => _ruler;

        /// <summary>The height of the words where the box defines none, for the height grip.</summary>
        private double NaturalHeight => _layout == null ? 0.0 : _layout.Box[3] - _layout.Box[1];

        /// <summary>The room left of the frame: none, the tab type box being drawn just outside it, left of the ruler, as AutoCAD draws it.</summary>
        private const double Lead = 0.0;

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
                _ruler = value == null ? null : new MTextRuler(value);
                _tip = null;

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

        /// <summary>
        /// Gets the line the caret is drawn on: the line of its place, but where End or a click past the end of a line put it at a
        /// break between two letters with no space, as between two ideographs, the end of that line, though its place is the
        /// first of the next; Up and Down go on from there.
        /// </summary>
        public int CaretLine
        {
            get
            {
                Relayout();
                return _layout == null || _model == null ? 0 : CaretLineNow();
            }
        }

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
            return new Size(Lead + _areaWidth + 2.0 * Pad, Top + _textHeight + Pad + 8.0);
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

            if (!_oldOutline.IsEmpty)
            {
                dc.DrawRectangle(null, OldOutline, _oldOutline);
            }

            dc.DrawRectangle(Back, Border, new Rect(Lead + 0.5, top + 0.5, width - 1.0, _textHeight + 2.0 * Pad));

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
                    int line = CaretLineNow();
                    double x = PixelX(
                        line == _layout.LineOfPlace(caret) ? _layout.XOf(caret) : _layout.RightOf(caret - 1)
                    );
                    dc.DrawLine(
                        Caret,
                        new Point(x, PixelY(_layout.TopOf(line))),
                        new Point(x, PixelY(_layout.BottomOf(line)))
                    );
                }
            }

            DrawGrips(dc);
            DrawGuide(dc, top);
            DrawTip(dc);
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

            if (_model == null)
            {
                return;
            }

            Point at = e.GetPosition(this);

            if (e.ChangedButton == MouseButton.Right)
            {
                // The ruler's own menu, Set Mtext Width and Height; elsewhere the editor's.
                bool onRuler = _model.IsRulerShown && at.Y >= 0.0 && at.Y <= RulerHeight;
                ShowMenu(_model.Menu(onRuler ? "Ruler" : "Editor"));
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            Relayout();

            if (_model.IsRulerShown && _ruler.Press(at, NaturalHeight))
            {
                _tipTimer.Stop();
                _tip = null;

                if (_ruler.Dragging != MTextRulerPart.None)
                {
                    _oldOutline =
                        _ruler.Dragging == MTextRulerPart.Width
                        || _ruler.Dragging == MTextRulerPart.ColumnWidth
                        || _ruler.Dragging == MTextRulerPart.Gutter
                        || _ruler.Dragging == MTextRulerPart.Total
                            ? new Rect(
                                Lead + 0.5,
                                RulerHeight + 0.5,
                                _areaWidth + 2.0 * Pad - 1.0,
                                _textHeight + 2.0 * Pad
                            )
                            : Rect.Empty;
                    CaptureMouse();
                }

                InvalidateVisual();
            }
            else if (e.ClickCount == 2)
            {
                SelectWord(PlaceAt(at));
            }
            else
            {
                _model.Editor.MoveTo(PlaceAt(at), (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
                KeepAtEndOfLineAt(at);
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
            _pointer = at;

            if (_ruler != null && _ruler.Dragging != MTextRulerPart.None)
            {
                _ruler.Move(at);
                InvalidateVisual();
                return;
            }

            if (_selecting && e.LeftButton == MouseButtonState.Pressed)
            {
                _model.Editor.MoveTo(PlaceAt(at), true);
            }

            // Over a marker: the marker in the accent and its tip after 400 ms; the arrows and edges ↔.
            int index = 0;
            MTextRulerPart part =
                _ruler != null && _model.IsRulerShown && _layout != null
                    ? _ruler.HitTest(at, NaturalHeight, out index)
                    : MTextRulerPart.None;

            if (part != _hover || index != _hoverIndex)
            {
                _hover = part;
                _hoverIndex = index;
                _tip = null;
                _tipTimer.Stop();

                if (part != MTextRulerPart.None && part != MTextRulerPart.Ruler)
                {
                    _tipTimer.Start();
                }

                InvalidateVisual();
            }

            Cursor =
                part == MTextRulerPart.Height ? Cursors.SizeNS
                : part == MTextRulerPart.None ? Cursors.IBeam
                : part == MTextRulerPart.Ruler || part == MTextRulerPart.TypeBox ? Cursors.Arrow
                : Cursors.SizeWE;
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _tipTimer.Stop();

            if (_hover != MTextRulerPart.None || _tip != null)
            {
                _hover = MTextRulerPart.None;
                _tip = null;
                InvalidateVisual();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (_ruler != null && _ruler.Dragging != MTextRulerPart.None)
            {
                _ruler.Release();
                _oldOutline = Rect.Empty;
                InvalidateVisual();
            }

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
                    // Esc during a drag of the ruler puts back what it moved; else the window asks.
                    if (_ruler != null && _ruler.Cancel())
                    {
                        _oldOutline = Rect.Empty;
                        ReleaseMouseCapture();
                        InvalidateVisual();
                        return true;
                    }

                    EscapePressed?.Invoke(this, EventArgs.Empty);
                    return true;
                case System.Windows.Input.Key.Enter
                    when !inCell && (modifiers & ModifierKeys.Alt) != 0 && editor.Frame.HasColumns:
                    editor.InsertColumnBreak();
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

                    int line = CaretLineNow();
                    double caretX =
                        line == _layout.LineOfPlace(editor.Caret)
                            ? _layout.XOf(editor.Caret)
                            : _layout.RightOf(editor.Caret - 1);

                    if (key == System.Windows.Input.Key.Home)
                    {
                        editor.MoveTo(control ? 0 : _layout.LineStart(line), shift);
                    }
                    else if (key == System.Windows.Input.Key.End)
                    {
                        editor.MoveTo(control ? editor.Length : _layout.LineEnd(line), shift);

                        if (!control)
                        {
                            KeepAtEndOf(line);
                        }
                    }
                    else
                    {
                        int at = line + (key == System.Windows.Input.Key.Up ? -1 : 1);

                        if (at >= 0 && at < _layout.LineCount)
                        {
                            editor.MoveTo(_layout.PlaceOnLine(at, caretX), shift);
                            KeepAtEndOf(at);
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

        private double PixelX(double x) => Lead + Pad + ((x - _left) * _pixelsPerUnit);

        private double PixelY(double y) => Top + ((_top - y) * _pixelsPerUnit);

        private double UnitX(double x) => _left + ((x - Lead - Pad) / _pixelsPerUnit);

        private double UnitY(double y) => _top - ((y - Top) / _pixelsPerUnit);

        private void Restale()
        {
            _stale = true;
            InvalidateMeasure();
            InvalidateVisual();
        }

        /// <summary>Lays the words out again where they or the box changed: the display's layout, the frame around all it draws, and the ruler over it.</summary>
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

                _layout.Update(_model.Editor, _model.Attachment, _model.LineSpacing, DefaultRgb, LayerRgb);
                _stale = false;
            }

            double[] bounds = _layout.Bounds;
            _left = bounds[0];
            _top = bounds[3];
            _areaWidth = Math.Max(40.0, (bounds[2] - bounds[0]) * _pixelsPerUnit);
            _textHeight = Math.Max(0.0, (bounds[3] - bounds[1]) * _pixelsPerUnit);

            // The ruler on the frame's top edge, its zero at the left of the words, scaled with the view.
            double[] box = _layout.Box;
            _ruler.Zero = PixelX(box[0]);
            _ruler.FrameLeft = Lead;
            _ruler.RulerTop = 0.0;
            _ruler.BoxTop = PixelY(box[3]);
            _ruler.Scale = _pixelsPerUnit;
            _ruler.Columns = _layout.ColumnCount;
            _ruler.NaturalWidth = box[2] - box[0];
            _ruler.CaretColumn = _layout.LineCount > 0 ? _layout.ColumnOf(_layout.LineOfPlace(_model.Editor.Caret)) : 0;
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

        private void ShowMenu(IReadOnlyList<CadMenuItemViewModel> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            System.Windows.Controls.ContextMenu menu = CadMenus.Make(items);
            menu.PlacementTarget = this;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        /// <summary>
        /// The ruler as the design draws it: the tab type box left of the frame, the ruler over every column with the gutters
        /// shaded, major and minor ticks, the indents and stops of the caret's paragraph on its column (stops set on only some
        /// selected paragraphs at half strength), and the arrows of the width, a column's width and the total, the gutter edge.
        /// </summary>
        private void DrawRuler(DrawingContext dc, double width)
        {
            MTextRuler ruler = _ruler;
            CadMTextFrame frame = _model.Editor.Frame;
            dc.DrawRectangle(RulerBack, RulerEdge, new Rect(Lead + 0.5, 0.5, width - 1.0, RulerHeight - 1.0));
            dc.DrawRectangle(
                TypeBoxBack,
                RulerEdge,
                new Rect(0.5 - MTextRuler.TypeBoxWidth, 0.5, MTextRuler.TypeBoxWidth - 1.0, RulerHeight - 1.0)
            );
            Glyph(dc, ruler.TabType, -MTextRuler.TypeBoxWidth / 2.0, Dark, 1.6, 7.0, 15.0);
            int columns = frame.HasColumns ? Math.Max(1, ruler.Columns) : 1;
            double segment = (frame.HasColumns ? frame.ColumnWidth : ruler.Room) * _pixelsPerUnit;
            double minor = ruler.Minor * _pixelsPerUnit;

            for (int c = 0; c < columns; c++)
            {
                double zero = ruler.ZeroOf(c);

                if (c + 1 < columns)
                {
                    dc.DrawRectangle(
                        GutterBack,
                        null,
                        new Rect(
                            zero + segment,
                            1.0,
                            Math.Max(0.0, ruler.ZeroOf(c + 1) - zero - segment),
                            RulerHeight - 2.0
                        )
                    );
                }

                for (int i = 0; minor > 0.5 && i * minor <= segment + 1E-6; i++)
                {
                    double x = Math.Floor(zero + (i * minor)) + 0.5;
                    dc.DrawLine(
                        Tick,
                        new Point(x, RulerHeight - (i % 5 == 0 ? 9.0 : 5.0)),
                        new Point(x, RulerHeight - 1.0)
                    );
                }
            }

            // The width arrows: the MTEXT's, or a column's and the total of static ones; the gutter edge a 2 px bar.
            if (frame.HasColumns)
            {
                Arrows(dc, ruler.ZeroOf(0) + segment, Lit(MTextRulerPart.ColumnWidth));

                if (columns > 1)
                {
                    double edge = ruler.ZeroOf(1);
                    var bar = new Pen(Lit(MTextRulerPart.Gutter), 2.0);
                    dc.DrawLine(bar, new Point(edge, 3.0), new Point(edge, RulerHeight - 3.0));
                }

                if (frame.ColumnType == DxfColumnType.StaticColumns)
                {
                    Arrows(dc, ruler.EndX, Lit(MTextRulerPart.Total));
                }
            }
            else
            {
                Arrows(dc, ruler.EndX, Lit(MTextRulerPart.Width));
            }

            if (!ruler.ShowsMarkers)
            {
                return;
            }

            CadMTextParagraphFormat format = _model.Editor.CurrentFormat;
            (int first, int last) = _model.Editor.SelectedParagraphs;
            dc.DrawGeometry(
                Lit(MTextRulerPart.First),
                null,
                Triangle(
                    ruler.X(format.FirstLineIndent) - 4.0,
                    1.0,
                    ruler.X(format.FirstLineIndent) + 4.0,
                    1.0,
                    ruler.X(format.FirstLineIndent),
                    7.0
                )
            );
            double hanging = ruler.X(format.HangingIndent);
            dc.DrawGeometry(
                Lit(MTextRulerPart.Hanging),
                null,
                Triangle(hanging - 4.0, RulerHeight - 1.0, hanging + 4.0, RulerHeight - 1.0, hanging, RulerHeight - 7.0)
            );

            for (int i = 0; i < format.TabStops.Count; i++)
            {
                CadMTextTabStop stop = format.TabStops[i];

                if (ruler.Dragging == MTextRulerPart.Tab && ruler.IsOff)
                {
                    break;
                }

                bool everywhere = Enumerable
                    .Range(first, last - first + 1)
                    .All(p => _model.Editor.FormatOf(p).TabStops.Any(s => s.Equals(stop)));
                Brush brush =
                    (_hover == MTextRulerPart.Tab && _hoverIndex == i)
                    || (ruler.Dragging == MTextRulerPart.Tab && Math.Abs(stop.Position - ruler.DragValue) < 1E-9)
                        ? Active
                    : everywhere ? Dark
                    : Faint;
                Glyph(dc, stop.Kind, ruler.X(stop.Position), brush, 2.0, 10.0, 18.0);
            }

            // A stop dragged off the ruler follows the pointer at 40 % in white.
            if (ruler.Dragging == MTextRulerPart.Tab && ruler.IsOff)
            {
                dc.PushOpacity(0.4);
                Glyph(dc, 'l', _pointer.X, Brushes.White, 2.0, _pointer.Y - 4.0, _pointer.Y + 4.0);
                dc.Pop();
            }
        }

        /// <summary>The brush of a part: the accent while it is hovered or dragged.</summary>
        private Brush Lit(MTextRulerPart part) => _hover == part || _ruler.Dragging == part ? Active : Dark;

        /// <summary>A tab glyph: L for left, an inverted T for centre, a reversed L for right, the T with a dot for decimal.</summary>
        private static void Glyph(
            DrawingContext dc,
            char kind,
            double x,
            Brush brush,
            double thickness,
            double y0,
            double y1
        )
        {
            var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(pen, new Point(x, y0), new Point(x, y1));

            switch (kind)
            {
                case 'c':
                    dc.DrawLine(pen, new Point(x - 5.0, y1), new Point(x + 5.0, y1));
                    break;
                case 'r':
                    dc.DrawLine(pen, new Point(x - 7.0, y1), new Point(x, y1));
                    break;
                case 'd':
                    dc.DrawLine(pen, new Point(x - 5.0, y1), new Point(x + 5.0, y1));
                    dc.DrawEllipse(brush, null, new Point(x + 4.0, y0 + 3.0), 1.0, 1.0);
                    break;
                default:
                    dc.DrawLine(pen, new Point(x, y1), new Point(x + 7.0, y1));
                    break;
            }
        }

        /// <summary>The width arrows, 12 by 8, ending at a point of the ruler.</summary>
        private static void Arrows(DrawingContext dc, double x, Brush brush)
        {
            var pen = new Pen(brush, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(pen, new Point(x - 12.0, 11.0), new Point(x, 11.0));
            dc.DrawLine(pen, new Point(x - 12.0, 11.0), new Point(x - 9.0, 8.0));
            dc.DrawLine(pen, new Point(x - 12.0, 11.0), new Point(x - 9.0, 14.0));
            dc.DrawLine(pen, new Point(x, 11.0), new Point(x - 3.0, 8.0));
            dc.DrawLine(pen, new Point(x, 11.0), new Point(x - 3.0, 14.0));
        }

        /// <summary>The height grips under the frame: one under the first column, or one under each dynamic column of its own height.</summary>
        private void DrawGrips(DrawingContext dc)
        {
            if (_layout == null || _ruler == null)
            {
                return;
            }

            for (int c = 0; c < _ruler.GripCount; c++)
            {
                Point at = _ruler.GripAt(c, NaturalHeight);
                bool lit =
                    (_hover == MTextRulerPart.Height && _hoverIndex == c)
                    || (_ruler.Dragging == MTextRulerPart.Height && _ruler.DraggedIndex == c);
                var pen = new Pen(lit ? Accent : Border.Brush, 1.6);
                dc.DrawLine(pen, new Point(at.X, at.Y + 1.0), new Point(at.X, at.Y + 11.0));
                dc.DrawLine(pen, new Point(at.X - 3.5, at.Y + 7.5), new Point(at.X, at.Y + 11.0));
                dc.DrawLine(pen, new Point(at.X + 3.5, at.Y + 7.5), new Point(at.X, at.Y + 11.0));
            }
        }

        /// <summary>The dashed guide of a drag: down the frame at a stop or an indent, or across at a height.</summary>
        private void DrawGuide(DrawingContext dc, double top)
        {
            if (_ruler == null || _layout == null)
            {
                return;
            }

            double bottom = top + _textHeight + 2.0 * Pad;

            switch (_ruler.Dragging)
            {
                case MTextRulerPart.Tab when !_ruler.IsOff:
                case MTextRulerPart.First:
                case MTextRulerPart.Hanging:
                    double x = Math.Floor(_ruler.X(_ruler.DragValue)) + 0.5;
                    dc.DrawLine(Guide, new Point(x, top), new Point(x, bottom));
                    break;
                case MTextRulerPart.Height:
                    Point grip = _ruler.GripAt(_ruler.DraggedIndex, NaturalHeight);
                    double y = Math.Floor(grip.Y) + 0.5;
                    double half =
                        (_model.Editor.Frame.HasColumns ? _model.Editor.Frame.ColumnWidth : _ruler.Room)
                        * _pixelsPerUnit
                        / 2.0;
                    dc.DrawLine(Guide, new Point(grip.X - half, y), new Point(grip.X + half, y));
                    break;
            }
        }

        /// <summary>The tip: during a drag its value, over a marker after 400 ms its name and value, 4 px under the ruler at the pointer.</summary>
        private void DrawTip(DrawingContext dc)
        {
            string tip = _ruler?.DragTip ?? _tip;

            if (string.IsNullOrEmpty(tip))
            {
                return;
            }

            var text = new FormattedText(
                tip,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11.0,
                TipText,
                1.0
            );
            double y = _ruler.Dragging == MTextRulerPart.Height ? _pointer.Y + 14.0 : RulerHeight + 4.0;
            var box = new Rect(_pointer.X + 8.0, y, text.Width + 16.0, text.Height + 6.0);
            dc.DrawRoundedRectangle(TipBack, TipEdge, box, 3.0, 3.0);
            dc.DrawText(text, new Point(box.X + 8.0, box.Y + 3.0));
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

        private void OnEditorChanged(object sender, EventArgs e)
        {
            _endOfLine = -1;
            Restale();
        }

        /// <summary>The line the caret is drawn on: its place's, or the line it was put at the end of (<see cref="CaretLine"/>).</summary>
        private int CaretLineNow()
        {
            int caret = _model.Editor.Caret;
            return
                _endOfLine >= 0
                && caret == _endPlace
                && _endOfLine < _layout.LineCount
                && _layout.LineEnd(_endOfLine) == caret
                ? _endOfLine
                : _layout.LineOfPlace(caret);
        }

        /// <summary>Keeps the caret a click put at the first place of a line at the end of the line before, where the click was on that line, past its end.</summary>
        private void KeepAtEndOfLineAt(Point at)
        {
            if (_layout == null || _layout.LineCount == 0)
            {
                return;
            }

            int line = _layout.LineOfPlace(_model.Editor.Caret) - 1;
            double x = UnitX(at.X);
            double y = UnitY(at.Y);

            if (
                line >= 0
                && y <= _layout.TopOf(line)
                && y >= _layout.BottomOf(line)
                && x >= _layout.XOf(_layout.LineStart(line))
            )
            {
                KeepAtEndOf(line);
            }
        }

        /// <summary>Keeps the caret at the end of a line where its place is the first of the next: the line broke between two letters with no space.</summary>
        private void KeepAtEndOf(int line)
        {
            int caret = _model.Editor.Caret;

            if (_layout != null && line >= 0 && _layout.LineEnd(line) == caret && _layout.LineOfPlace(caret) != line)
            {
                _endOfLine = line;
                _endPlace = caret;
                InvalidateVisual();
            }
        }

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
