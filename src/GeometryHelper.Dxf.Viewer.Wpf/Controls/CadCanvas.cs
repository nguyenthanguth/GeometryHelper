using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Geometry;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The point of the drawing under the cursor, in drawing units.</summary>
    public sealed class CadCursorEventArgs : EventArgs
    {
        /// <summary>Makes the arguments.</summary>
        /// <param name="x">The X of the drawing point.</param>
        /// <param name="y">The Y of the drawing point.</param>
        public CadCursorEventArgs(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Gets the X of the drawing point.</summary>
        public double X { get; }

        /// <summary>Gets the Y of the drawing point.</summary>
        public double Y { get; }
    }

    /// <summary>
    /// The canvas of the presenter: one space of a <see cref="CadDocument"/> drawn through <see cref="SkiaCanvasRenderer"/> into
    /// a bitmap of the device's pixels, and handled with the mouse as AutoCAD is. The wheel zooms about the cursor, 1.2 times
    /// a notch; dragging with the middle button pans and double-clicking it zooms to the extents. With no command running a
    /// left click picks within 5 pixels (Shift+click adds or removes, a click on nothing clears), dragging left to right
    /// selects by a solid blue window and right to left by a dashed green crossing, and the selected entities show their
    /// grips, which drag. With a command of <see cref="Session"/> running a click answers its prompt: a point prompt takes
    /// the point <see cref="Snap"/> gives, drawn with its marker and a rubber band from the prompt's base point, and
    /// "Select objects:" takes a click or a box; while MOVE, COPY, ROTATE, SCALE or MIRROR waits for its second point, a
    /// dashed ghost of the objects follows the cursor. A right click is Enter; Esc cancels. <see cref="ShowGrid"/> draws a dot
    /// grid under the drawing. It draws the drawing again only when something has changed, once per frame at most; the
    /// cursor's marks are drawn over it without drawing it again.
    /// </summary>
    public sealed class CadCanvas : FrameworkElement, IDisposable
    {
        /// <summary>Identifies the <see cref="Document"/> property.</summary>
        public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
            nameof(Document), typeof(CadDocument), typeof(CadCanvas), new PropertyMetadata(null, OnDocumentChanged));

        /// <summary>Identifies the <see cref="Space"/> property.</summary>
        public static readonly DependencyProperty SpaceProperty = DependencyProperty.Register(
            nameof(Space), typeof(CadSpace), typeof(CadCanvas), new PropertyMetadata(null, OnSceneChanged));

        /// <summary>Identifies the <see cref="View"/> property.</summary>
        public static readonly DependencyProperty ViewProperty = DependencyProperty.Register(
            nameof(View), typeof(CadView), typeof(CadCanvas), new PropertyMetadata(null, OnSceneChanged));

        /// <summary>Identifies the <see cref="Revision"/> property.</summary>
        public static readonly DependencyProperty RevisionProperty = DependencyProperty.Register(
            nameof(Revision), typeof(int), typeof(CadCanvas), new PropertyMetadata(0, OnSceneChanged));

        /// <summary>Identifies the <see cref="Session"/> property.</summary>
        public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
            nameof(Session), typeof(CadCommandSession), typeof(CadCanvas), new PropertyMetadata(null, OnSessionChanged));

        /// <summary>Identifies the <see cref="Snap"/> property.</summary>
        public static readonly DependencyProperty SnapProperty = DependencyProperty.Register(
            nameof(Snap), typeof(ObjectSnap), typeof(CadCanvas), new PropertyMetadata(null));

        /// <summary>Identifies the <see cref="ShowGrid"/> property.</summary>
        public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
            nameof(ShowGrid), typeof(bool), typeof(CadCanvas), new PropertyMetadata(false, OnSceneChanged));

        /// <summary>Identifies the <see cref="GridSpacing"/> property.</summary>
        public static readonly DependencyProperty GridSpacingProperty = DependencyProperty.Register(
            nameof(GridSpacing), typeof(double), typeof(CadCanvas), new PropertyMetadata(10.0, OnSceneChanged));

        private const double GridPixels = 8.0;
        private const double PickPixels = 5.0;
        private const double AperturePixels = 10.0;
        private const double DragPixels = 4.0;
        private const double WheelFactor = 1.2;
        private const double GripSize = 8.0;
        private const int GripLimit = 100;

        private static readonly Brush WindowFill = Frozen(new SolidColorBrush(Color.FromArgb(26, 0x3D, 0x9B, 0xFF)));
        private static readonly Brush CrossingFill = Frozen(new SolidColorBrush(Color.FromArgb(26, 0x3F, 0xB9, 0x50)));
        private static readonly Pen WindowPen = Frozen(new Pen(Solid(0x3D9BFF), 1.0));
        private static readonly Pen CrossingPen = Frozen(new Pen(Solid(0x3FB950), 1.0) { DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0.0) });
        private static readonly Brush GripBrush = Solid(0x3D9BFF);
        private static readonly Brush HotGripBrush = Solid(0xF85149);
        private static readonly Brush DragGripBrush = Solid(0x3FB950);
        private static readonly Pen GhostPen = Frozen(new Pen(Solid(0x3D9BFF), 1.2) { DashStyle = new DashStyle(new[] { 6.0, 3.0 }, 0.0) });
        private static readonly Pen RubberPen =Frozen(new Pen(Solid(0x3D9BFF), 1.0) { DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0.0) });
        private static readonly Pen MarkerPen = Frozen(new Pen(Solid(0xE0A800), 2.0));
        private static readonly Pen BasePen = Frozen(new Pen(Solid(0x3FB950), 2.0));
        private static readonly Brush TipFill = Solid(0x1A1E24);
        private static readonly Pen TipPen = Frozen(new Pen(Solid(0x3A414C), 1.0));
        private static readonly Brush TipInk = Solid(0xE6E9ED);
        private static readonly Brush TagInk = Solid(0x0B1220);
        private static readonly Typeface TipFace = new Typeface(new FontFamily("Inter, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface TagFace = new Typeface(new FontFamily("Inter, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        private readonly SkiaCanvasRenderer _renderer = new SkiaCanvasRenderer();
        private readonly List<GripAt> _grips = new List<GripAt>();
        private readonly List<ViewportTag> _tags = new List<ViewportTag>();
        private readonly CadGhost _ghost = new CadGhost();
        private WriteableBitmap _bitmap;
        private bool _renderQueued;
        private Point? _panFrom;
        private Point? _boxFrom;
        private Point _boxTo;
        private bool _boxing;
        private Point? _pointDown;
        private GeoPoint2? _cursor;
        private CadSnapResult _snapped;
        private int _hotGrip = -1;
        private int _dragGrip = -1;
        private GeoPoint2 _dragTo;

        /// <summary>Makes an empty canvas.</summary>
        public CadCanvas()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            Cursor = Cursors.Cross;
            Loaded += (s, e) => RequestRender();
            Unloaded += (s, e) =>
            {
                _renderQueued = false;
                StopQueue();
            };
        }

        /// <summary>Raised when a click, a box, a grip or Esc on the canvas has changed the selection or answered a command.</summary>
        public event EventHandler SelectionChanged;

        /// <summary>Raised when the cursor moves over the canvas, with the point of the drawing under it.</summary>
        public event EventHandler<CadCursorEventArgs> CursorMoved;

        /// <summary>Raised when the wheel or the middle button has zoomed or panned the view.</summary>
        public event EventHandler ViewChanged;

        /// <summary>Raised when the canvas takes a new size in device pixels, after <see cref="View"/> has been resized to it.</summary>
        public event EventHandler PixelSizeChanged;

        /// <summary>Raised when the canvas has written a line into the session's history, as a grip drag refused.</summary>
        public event EventHandler HistoryWritten;

        /// <summary>Gets or sets the document shown.</summary>
        public CadDocument Document
        {
            get => (CadDocument)GetValue(DocumentProperty);
            set => SetValue(DocumentProperty, value);
        }

        /// <summary>Gets or sets the space of the document shown.</summary>
        public CadSpace Space
        {
            get => (CadSpace)GetValue(SpaceProperty);
            set => SetValue(SpaceProperty, value);
        }

        /// <summary>
        /// Gets or sets the view: the part of the space shown and its zoom, in device pixels. The canvas resizes it to its own
        /// size and changes it as the user zooms and pans.
        /// </summary>
        public CadView View
        {
            get => (CadView)GetValue(ViewProperty);
            set => SetValue(ViewProperty, value);
        }

        /// <summary>Gets or sets a number that, changed, makes the canvas draw again: a view model raises it after it has changed the view or the drawing.</summary>
        public int Revision
        {
            get => (int)GetValue(RevisionProperty);
            set => SetValue(RevisionProperty, value);
        }

        /// <summary>Gets or sets the command line of the document, whose prompts the clicks answer; null for a canvas that only selects.</summary>
        public CadCommandSession Session
        {
            get => (CadCommandSession)GetValue(SessionProperty);
            set => SetValue(SessionProperty, value);
        }

        /// <summary>Gets or sets the object snaps, ortho, polar and grid snap a point is taken through; null for none.</summary>
        public ObjectSnap Snap
        {
            get => (ObjectSnap)GetValue(SnapProperty);
            set => SetValue(SnapProperty, value);
        }

        /// <summary>
        /// Gets or sets whether model space shows the grid (GRID, F7): dots a little lighter than the background, under the
        /// drawing, every <see cref="GridSpacing"/> units or, where those would come closer than 8 pixels, every 5, 10, 50,
        /// 100 times that.
        /// </summary>
        public bool ShowGrid
        {
            get => (bool)GetValue(ShowGridProperty);
            set => SetValue(ShowGridProperty, value);
        }

        /// <summary>Gets or sets the spacing of the grid in drawing units; 10 by default, as the snap grid.</summary>
        public double GridSpacing
        {
            get => (double)GetValue(GridSpacingProperty);
            set => SetValue(GridSpacingProperty, value);
        }

        /// <summary>Gets how the space is drawn: background, paper and selection colours, the entity under the cursor.</summary>
        public CanvasOptions Options { get; } = new CanvasOptions();

        /// <summary>
        /// Gets the spacing the grid is drawn at: the spacing asked, times 5, 10, 50, 100 and on, the first whose dots lie at
        /// least a number of pixels apart. A grid of 10 at 0.18 pixels to the unit is drawn every 50, 9 pixels apart.
        /// </summary>
        /// <param name="pixelsPerUnit">The zoom.</param>
        /// <param name="spacing">The spacing asked, in drawing units.</param>
        /// <param name="leastPixels">The least distance between dots, in pixels.</param>
        /// <returns>The spacing drawn, in drawing units; 0 where the spacing or the zoom is not above nought.</returns>
        public static double GridStep(double pixelsPerUnit, double spacing, double leastPixels)
        {
            if (!(spacing > 0.0) || !(pixelsPerUnit > 0.0))
            {
                return 0.0;
            }

            double step = spacing;

            for (int i = 0; step * pixelsPerUnit < leastPixels && i < 60; i++)
            {
                step *= i % 2 == 0 ? 5.0 : 2.0;
            }

            return step;
        }

        /// <summary>Gets the width of the canvas in device pixels.</summary>
        public int PixelWidth { get; private set; }

        /// <summary>Gets the height of the canvas in device pixels.</summary>
        public int PixelHeight { get; private set; }

        /// <summary>Gets the snap the cursor takes now, while a point prompt waits; null otherwise.</summary>
        public CadSnapResult CurrentSnap => _snapped;

        /// <summary>Gets the number of grips drawn: those of the selected entities while no command runs, 100 entities at most.</summary>
        public int GripCount => _grips.Count;

        /// <summary>Gets the device pixels to one WPF unit: 1 at 96 dots per inch, 1.5 at 144, and 1 where the canvas is in no window.</summary>
        public double PixelScale
        {
            get
            {
                PresentationSource source = PresentationSource.FromVisual(this);
                return source?.CompositionTarget == null ? 1.0 : source.CompositionTarget.TransformToDevice.M11;
            }
        }

        /// <summary>
        /// Puts the cursor on a point of the drawing, as moving the mouse there does: the snap, the rubber band and the hover
        /// follow it. A screenshot taken with no mouse uses it to show a command in progress.
        /// </summary>
        /// <param name="x">The X of the drawing point.</param>
        /// <param name="y">The Y of the drawing point.</param>
        public void MoveCursorTo(double x, double y)
        {
            CadView view = View;

            if (view == null)
            {
                return;
            }

            view.DrawingToPixel(x, y, out double px, out double py);
            double scale = PixelScale;
            Track(new Point(px / scale, py / scale));
        }

        /// <summary>Draws the space now rather than at the next frame, as a screenshot taken with no window showing needs.</summary>
        public void RenderNow()
        {
            _renderQueued = false;
            StopQueue();
            double scale = PixelScale;
            int width = (int)Math.Ceiling(ActualWidth * scale);
            int height = (int)Math.Ceiling(ActualHeight * scale);

            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (width != PixelWidth || height != PixelHeight)
            {
                PixelWidth = width;
                PixelHeight = height;
                View?.Resize(width, height);
                PixelSizeChanged?.Invoke(this, EventArgs.Empty);

                // What the handlers changed is drawn now, not at the next frame.
                _renderQueued = false;
                StopQueue();
            }

            // With no window there is no render pass to copy a drawn bitmap to the screen's side, so a screenshot or a test
            // would see the bitmap as first drawn: there each drawing takes a bitmap of its own.
            bool fresh = _bitmap == null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height || PresentationSource.FromVisual(this) == null;

            if (fresh)
            {
                _bitmap = new WriteableBitmap(width, height, 96.0 * scale, 96.0 * scale, PixelFormats.Pbgra32, null);
            }

            CadView view = View;

            if (view != null && (Math.Abs(view.Width - width) > 0.5 || Math.Abs(view.Height - height) > 0.5))
            {
                view.Resize(width, height);
            }

            _bitmap.Lock();

            try
            {
                var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

                using (SKSurface surface = SKSurface.Create(info, _bitmap.BackBuffer, _bitmap.BackBufferStride))
                {
                    CadDocument document = Document;
                    CadSpace space = Space;

                    if (document == null || space == null || view == null)
                    {
                        int rgb = Options.Background;
                        surface.Canvas.Clear(new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
                    }
                    else
                    {
                        Options.Selected = Highlighted(document);
                        _renderer.Render(surface.Canvas, document, space, view, Options);
                    }

                    surface.Canvas.Flush();
                }

                if (ShowGrid && Document != null && Space != null && Space.IsModel && view != null)
                {
                    DrawGrid(view, width, height, scale);
                }

                _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                _bitmap.Unlock();
            }

            FindGrips();
            InvalidateVisual();
        }

        /// <summary>
        /// Esc on the canvas: stops a box or a grip being dragged; else ends the command running; else clears the selection.
        /// </summary>
        public void Cancel()
        {
            if (CancelGesture())
            {
                return;
            }

            CadCommandSession session = Session;

            if (session != null && session.IsRunning)
            {
                session.Escape();
            }
            else
            {
                Document?.Selection.Clear();
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Stops a box or a grip being dragged, as the first Esc does.</summary>
        /// <returns>Whether there was one to stop.</returns>
        public bool CancelGesture()
        {
            bool any = _boxFrom.HasValue || _dragGrip >= 0;
            _boxFrom = null;
            _boxing = false;
            _dragGrip = -1;

            if (any)
            {
                ReleaseMouseCapture();
                InvalidateVisual();
            }

            return any;
        }

        /// <summary>Releases the typefaces of the renderer.</summary>
        public void Dispose()
        {
            StopQueue();
            _renderer.Dispose();
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var whole = new Rect(0, 0, ActualWidth, ActualHeight);
            int rgb = Options.Background;
            drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)), null, whole);
            double scale = PixelScale;

            if (_bitmap != null)
            {
                drawingContext.DrawImage(_bitmap, new Rect(0, 0, _bitmap.PixelWidth / scale, _bitmap.PixelHeight / scale));
            }

            CadView view = View;

            if (view != null)
            {
                DrawTags(drawingContext, view, scale);
                DrawGrips(drawingContext, view, scale);
                DrawPrompt(drawingContext, view, scale);
            }

            if (_boxing && _boxFrom.HasValue)
            {
                Point a = _boxFrom.Value;
                bool crossing = _boxTo.X < a.X;
                drawingContext.DrawRectangle(crossing ? CrossingFill : WindowFill, crossing ? CrossingPen : WindowPen, new Rect(a, _boxTo));
            }
        }

        /// <inheritdoc/>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            RequestRender();
        }

        /// <inheritdoc/>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            RequestRender();
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            CadView view = View;

            if (view == null)
            {
                return;
            }

            Point p = PositionOf(e);
            double scale = PixelScale;
            view.ZoomAt(p.X * scale, p.Y * scale, Math.Pow(WheelFactor, e.Delta / 120.0));
            e.Handled = true;
            ViewChanged?.Invoke(this, EventArgs.Empty);
            RequestRender();
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Point p = PositionOf(e);

            if (e.ChangedButton == MouseButton.Middle)
            {
                if (e.ClickCount == 2)
                {
                    ZoomExtents();
                }
                else
                {
                    _panFrom = p;
                    CaptureMouse();
                    Cursor = Cursors.SizeAll;
                }

                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                Track(p);

                if (CadPicking.WantsPoint(Session))
                {
                    _pointDown = p;
                }
                else if (!IsRunning && _hotGrip >= 0)
                {
                    _dragGrip = _hotGrip;
                    _dragTo = _grips[_dragGrip].Grip.Point;
                    CaptureMouse();
                    InvalidateVisual();
                }
                else
                {
                    _boxFrom = p;
                    _boxTo = p;
                    _boxing = false;
                    CaptureMouse();
                }

                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Track(PositionOf(e));
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            Point at = PositionOf(e);

            if (e.ChangedButton == MouseButton.Middle && _panFrom.HasValue)
            {
                _panFrom = null;
                Cursor = Cursors.Cross;
                ReleaseMouseCapture();
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left && _dragGrip >= 0)
            {
                GripAt grip = _grips[_dragGrip];
                _dragGrip = -1;
                ReleaseMouseCapture();
                Track(at);
                GeoPoint2 to = _dragTo;
                CadEditResult result = Document?.GripDrag(grip.Handle, grip.Grip.Index, to);

                if (result != null && !result.Succeeded && Session != null)
                {
                    Session.Write(result.Message);
                    HistoryWritten?.Invoke(this, EventArgs.Empty);
                }

                InvalidateVisual();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left && _pointDown.HasValue)
            {
                _pointDown = null;
                Click(at, false);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left && _boxFrom.HasValue)
            {
                Point from = _boxFrom.Value;
                bool boxing = _boxing;
                _boxFrom = null;
                _boxing = false;
                ReleaseMouseCapture();
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

                if (boxing)
                {
                    SelectBox(from, at, shift);
                    InvalidateVisual();
                }
                else
                {
                    Click(at, shift);
                }

                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Right && IsRunning)
            {
                // A right click is Enter while a command runs, as AutoCAD has it by default.
                Session.Enter();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            if (Options.Hover != null)
            {
                Options.Hover = null;
                RequestRender();
            }
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                Cancel();
                e.Handled = true;
            }
        }

        /// <summary>Gets or sets where a mouse event happened; null for the mouse itself. A test sets it, having no mouse to move.</summary>
        internal Func<MouseEventArgs, Point> Pointer { get; set; }

        private bool IsRunning => Session != null && Session.IsRunning;

        private Point PositionOf(MouseEventArgs e) => Pointer != null ? Pointer(e) : e.GetPosition(this);

        private static SolidColorBrush Solid(int rgb) => Frozen(new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)));

        private static T Frozen<T>(T freezable)
            where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }

        private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (CadCanvas)d;

            if (e.OldValue is CadDocument old)
            {
                old.Selection.Changed -= canvas.OnSelectionChanged;
            }

            if (e.NewValue is CadDocument next)
            {
                next.Selection.Changed += canvas.OnSelectionChanged;
            }

            canvas.Options.Hover = null;
            canvas.CancelGesture();
            canvas.RequestRender();
        }

        private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (CadCanvas)d;

            if (e.OldValue is CadCommandSession old)
            {
                old.PromptChanged -= canvas.OnPromptChanged;
            }

            if (e.NewValue is CadCommandSession next)
            {
                next.PromptChanged += canvas.OnPromptChanged;
            }

            canvas._ghost.Follow(e.NewValue as CadCommandSession);

            canvas.RequestRender();
        }

        private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (CadCanvas)d;

            if (e.Property == SpaceProperty)
            {
                canvas.Options.Hover = null;
                canvas.CancelGesture();
            }

            canvas.RequestRender();
        }

        private void OnSelectionChanged(object sender, EventArgs e) => RequestRender();

        private void OnPromptChanged(object sender, EventArgs e)
        {
            _snapped = null;
            _ghost.Follow(Session);
            RequestRender();
        }

        /// <summary>
        /// The grid's dots, put into the bitmap the drawing was drawn into only where it shows the background, so that they
        /// lie under the drawing.
        /// </summary>
        private void DrawGrid(CadView view, int width, int height, double scale)
        {
            double step = GridStep(view.PixelsPerUnit, GridSpacing, GridPixels * scale);

            if (step <= 0.0)
            {
                return;
            }

            int rgb = Options.Background & 0xFFFFFF;
            int background = unchecked((int)0xFF000000) | rgb;
            int Lighter(int channel) => channel + (int)((255 - channel) * 0.2);
            int dot = unchecked((int)0xFF000000) | (Lighter((rgb >> 16) & 0xFF) << 16) | (Lighter((rgb >> 8) & 0xFF) << 8) | Lighter(rgb & 0xFF);
            DisplayBox visible = view.VisibleBox;
            IntPtr buffer = _bitmap.BackBuffer;
            int stride = _bitmap.BackBufferStride;

            for (double x = Math.Ceiling(visible.MinX / step) * step; x <= visible.MaxX; x += step)
            {
                for (double y = Math.Ceiling(visible.MinY / step) * step; y <= visible.MaxY; y += step)
                {
                    view.DrawingToPixel(x, y, out double px, out double py);
                    int ix = (int)Math.Round(px);
                    int iy = (int)Math.Round(py);

                    if (ix < 0 || iy < 0 || ix >= width || iy >= height)
                    {
                        continue;
                    }

                    int offset = iy * stride + ix * 4;

                    if (System.Runtime.InteropServices.Marshal.ReadInt32(buffer, offset) == background)
                    {
                        System.Runtime.InteropServices.Marshal.WriteInt32(buffer, offset, dot);
                    }
                }
            }
        }

        /// <summary>The ghost copy of the objects a transforming command took, dashed in the selection colour, under the transformation the cursor gives.</summary>
        private void DrawGhost(DrawingContext dc, CadView view, double scale, GeoPoint2 target)
        {
            Affine2? moved = CadGhost.Transform(Session, target);

            if (!moved.HasValue || Document == null || Space == null)
            {
                return;
            }

            Affine2 m = moved.Value;
            IReadOnlyList<double[]> paths = _ghost.Paths(Document, Space, 0.5 * scale / view.PixelsPerUnit);

            if (paths.Count == 0)
            {
                return;
            }

            var geometry = new StreamGeometry();

            using (StreamGeometryContext context = geometry.Open())
            {
                foreach (double[] xy in paths)
                {
                    for (int i = 0; i + 1 < xy.Length; i += 2)
                    {
                        Point p = ToScreen(view, new GeoPoint2(m.X(xy[i], xy[i + 1]), m.Y(xy[i], xy[i + 1])), scale);

                        if (i == 0)
                        {
                            context.BeginFigure(p, false, false);
                        }
                        else
                        {
                            context.LineTo(p, true, false);
                        }
                    }
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(null, GhostPen, geometry);
        }

        /// <summary>The selection, and while "Select objects:" waits the objects it has gathered, drawn highlighted.</summary>
        private IReadOnlyCollection<string> Highlighted(CadDocument document)
        {
            CadCommandSession session = Session;

            if (session == null || session.Gathering.Count == 0)
            {
                return document.Selection.Handles;
            }

            var all = new HashSet<string>(document.Selection.Handles, StringComparer.OrdinalIgnoreCase);
            all.UnionWith(session.Gathering);
            return all;
        }

        /// <summary>Follows the cursor: its drawing point, the session's cursor and aperture, the snap, the grip under it, the hover.</summary>
        private void Track(Point p)
        {
            CadView view = View;
            CadDocument document = Document;

            if (view == null || document == null || Space == null)
            {
                return;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            var cursor = new GeoPoint2(x, y);
            _cursor = cursor;
            double pick = PickPixels * scale / view.PixelsPerUnit;
            double aperture = AperturePixels * scale / view.PixelsPerUnit;
            CadCommandSession session = Session;

            if (session != null)
            {
                session.Cursor = cursor;
                session.PickAperture = pick;
            }

            CursorMoved?.Invoke(this, new CadCursorEventArgs(x, y));

            if (_panFrom.HasValue)
            {
                Point from = _panFrom.Value;
                view.Pan((p.X - from.X) * scale, (p.Y - from.Y) * scale);
                _panFrom = p;
                ViewChanged?.Invoke(this, EventArgs.Empty);
                RequestRender();
                return;
            }

            if (_dragGrip >= 0)
            {
                CadSnapResult snapped = CadPicking.Snap(document, Space, Snap, cursor, aperture, _grips[_dragGrip].Grip.Point);
                _snapped = snapped;
                _dragTo = snapped?.Point ?? cursor;
                InvalidateVisual();
                return;
            }

            if (_boxFrom.HasValue)
            {
                _boxTo = p;
                Vector moved = p - _boxFrom.Value;
                _boxing |= Math.Abs(moved.X) > DragPixels || Math.Abs(moved.Y) > DragPixels;

                if (_boxing)
                {
                    InvalidateVisual();
                }

                return;
            }

            if (CadPicking.WantsPoint(session))
            {
                _snapped = CadPicking.Snap(document, Space, Snap, cursor, aperture, session.Prompt.BasePoint);
                SetHover(null);
                InvalidateVisual();
                return;
            }

            _snapped = null;
            int hot = -1;

            if (!IsRunning)
            {
                for (int i = 0; i < _grips.Count; i++)
                {
                    view.DrawingToPixel(_grips[i].Grip.Point.X, _grips[i].Grip.Point.Y, out double gx, out double gy);

                    if (Math.Abs(gx / scale - p.X) <= GripSize * 0.75 && Math.Abs(gy / scale - p.Y) <= GripSize * 0.75)
                    {
                        hot = i;
                        break;
                    }
                }
            }

            if (hot != _hotGrip)
            {
                _hotGrip = hot;
                InvalidateVisual();
            }

            IReadOnlyList<string> found = document.GetPickIndex(Space).Pick(x, y, pick);
            SetHover(found.Count > 0 ? found[0] : null);

            if (session != null && session.Prompt != null)
            {
                InvalidateVisual();
            }
        }

        private void SetHover(string hover)
        {
            if (!string.Equals(hover, Options.Hover, StringComparison.OrdinalIgnoreCase))
            {
                Options.Hover = hover;
                RequestRender();
            }
        }

        private void Click(Point p, bool shift)
        {
            CadDocument document = Document;
            CadView view = View;

            if (document == null || Space == null || view == null)
            {
                return;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            double aperture = (CadPicking.WantsPoint(Session) ? AperturePixels : PickPixels) * scale / view.PixelsPerUnit;

            if (CadPicking.Click(document, Space, Session, Snap, new GeoPoint2(x, y), aperture, shift))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            _snapped = null;
            InvalidateVisual();
        }

        private void SelectBox(Point a, Point b, bool remove)
        {
            CadDocument document = Document;
            CadView view = View;

            if (document == null || Space == null || view == null)
            {
                return;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(a.X * scale, a.Y * scale, out double x0, out double y0);
            view.ScreenToDrawing(b.X * scale, b.Y * scale, out double x1, out double y1);
            var box = new DisplayBox(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
            CadPicking.BoxDone(document, Space, Session, box, b.X < a.X, remove);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ZoomExtents()
        {
            CadDocument document = Document;
            CadView view = View;

            if (document == null || Space == null || view == null)
            {
                return;
            }

            view.ZoomTo(CadPicking.Extents(document, Space));
            ViewChanged?.Invoke(this, EventArgs.Empty);
            RequestRender();
        }

        /// <summary>The grips of the selected entities while no command runs, and the tags of the selected viewports.</summary>
        private void FindGrips()
        {
            _grips.Clear();
            _tags.Clear();
            CadDocument document = Document;

            if (document == null || Space == null || IsRunning || document.Selection.Count == 0)
            {
                _hotGrip = -1;
                return;
            }

            var handles = new List<string>(document.Selection.Handles);
            handles.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < handles.Count && i < GripLimit; i++)
            {
                DxfEntity entity = document.FindEntity(handles[i]);

                if (entity == null)
                {
                    continue;
                }

                foreach (CadGrip grip in CadGrips.Of(entity))
                {
                    _grips.Add(new GripAt(handles[i], grip));
                }
            }

            if (!Space.IsModel)
            {
                var selected = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);

                foreach (DisplayItem2 item in document.GetDisplay(Space).Items)
                {
                    if (item is DisplayViewport2 viewport && item.Handle != null && selected.Contains(item.Handle))
                    {
                        _tags.Add(new ViewportTag(viewport.Bounds, ScaleText(viewport.Scale)));
                    }
                }
            }

            if (_hotGrip >= _grips.Count)
            {
                _hotGrip = -1;
            }
        }

        private static string ScaleText(double scale)
        {
            if (scale <= 0.0)
            {
                return "VIEWPORT";
            }

            return scale < 1.0
                ? "VIEWPORT · 1:" + (1.0 / scale).ToString("0.##", CultureInfo.InvariantCulture)
                : "VIEWPORT · " + scale.ToString("0.##", CultureInfo.InvariantCulture) + ":1";
        }

        private Point ToScreen(CadView view, GeoPoint2 point, double scale)
        {
            view.DrawingToPixel(point.X, point.Y, out double px, out double py);
            return new Point(px / scale, py / scale);
        }

        private void DrawGrips(DrawingContext dc, CadView view, double scale)
        {
            double half = GripSize / 2.0;

            for (int i = 0; i < _grips.Count; i++)
            {
                Point at = ToScreen(view, _grips[i].Grip.Point, scale);
                Brush fill = i == _dragGrip ? DragGripBrush : i == _hotGrip ? HotGripBrush : GripBrush;
                dc.DrawRectangle(fill, null, new Rect(at.X - half, at.Y - half, GripSize, GripSize));
            }

            if (_dragGrip >= 0)
            {
                Point from = ToScreen(view, _grips[_dragGrip].Grip.Point, scale);
                Point to = ToScreen(view, _dragTo, scale);
                dc.DrawLine(RubberPen, from, to);
                dc.DrawRectangle(DragGripBrush, null, new Rect(to.X - half, to.Y - half, GripSize, GripSize));
                DrawMarker(dc, _snapped, to);
            }
        }

        private void DrawTags(DrawingContext dc, CadView view, double scale)
        {
            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (ViewportTag tag in _tags)
            {
                Point corner = ToScreen(view, new GeoPoint2(tag.Frame.MinX, tag.Frame.MaxY), scale);
                var text = new FormattedText(tag.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, TagFace, 9.0, TagInk, dip);
                var box = new Rect(corner.X, corner.Y - 20.0, text.Width + 12.0, 16.0);
                dc.DrawRoundedRectangle(GripBrush, null, box, 2.0, 2.0);
                dc.DrawText(text, new Point(box.X + 6.0, box.Y + (16.0 - text.Height) / 2.0));
            }
        }

        /// <summary>While a point prompt waits: the base point, the rubber band to the snapped cursor, the snap's marker and its tip.</summary>
        private void DrawPrompt(DrawingContext dc, CadView view, double scale)
        {
            CadCommandSession session = Session;

            if (!CadPicking.WantsPoint(session) || !_cursor.HasValue)
            {
                return;
            }

            GeoPoint2 target = _snapped?.Point ?? _cursor.Value;
            Point to = ToScreen(view, target, scale);
            GeoPoint2? basePoint = session.Prompt.BasePoint;
            DrawGhost(dc, view, scale, target);

            if (basePoint.HasValue)
            {
                Point from = ToScreen(view, basePoint.Value, scale);
                dc.DrawLine(RubberPen, from, to);
                dc.DrawRectangle(null, BasePen, new Rect(from.X - 5.0, from.Y - 5.0, 10.0, 10.0));
            }

            DrawMarker(dc, _snapped, to);
            string tip = TipText(_snapped, basePoint, target);

            if (tip.Length > 0)
            {
                var text = new FormattedText(tip, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, TipFace, 10.0, TipInk, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                double w = text.Width + 16.0;

                // Beside the cursor, below and to the right; turned to the other side where the canvas ends.
                double left = to.X + 16.0 + w > ActualWidth ? to.X - 16.0 - w : to.X + 16.0;
                double top = to.Y + 14.0 + 22.0 > ActualHeight ? to.Y - 14.0 - 22.0 : to.Y + 14.0;
                var box = new Rect(left, top, w, 22.0);
                dc.DrawRoundedRectangle(TipFill, TipPen, box, 2.0, 2.0);
                dc.DrawText(text, new Point(box.X + 8.0, box.Y + (22.0 - text.Height) / 2.0));
            }
        }

        /// <summary>The tip by the cursor, as AutoCAD's dynamic input: the snap's name, the distance and the angle from the base point.</summary>
        private static string TipText(CadSnapResult snapped, GeoPoint2? basePoint, GeoPoint2 target)
        {
            string kind = snapped == null || snapped.Kind == CadSnapKind.None ? string.Empty : snapped.Kind.ToString();

            if (!basePoint.HasValue)
            {
                return kind;
            }

            GeoVector2 way = target - basePoint.Value;
            double degrees = Math.Atan2(way.Y, way.X) * 180.0 / Math.PI;
            degrees = degrees < 0.0 ? degrees + 360.0 : degrees;
            string measured = way.Length.ToString("0.0", CultureInfo.InvariantCulture) + " < " + degrees.ToString("0", CultureInfo.InvariantCulture) + "°";
            return kind.Length == 0 ? measured : kind + " · " + measured;
        }

        /// <summary>The marker of a snap, as AutoCAD draws it: endpoint a square, midpoint a triangle, centre a circle, quadrant a diamond, intersection a cross, perpendicular a corner, tangent a circle with a line on top, nearest an hourglass.</summary>
        private static void DrawMarker(DrawingContext dc, CadSnapResult snapped, Point p)
        {
            if (snapped == null)
            {
                return;
            }

            const double r = 5.0;

            switch (snapped.Kind)
            {
                case CadSnapKind.Endpoint:
                    dc.DrawRectangle(null, MarkerPen, new Rect(p.X - r, p.Y - r, 2 * r, 2 * r));
                    break;
                case CadSnapKind.Midpoint:
                    dc.DrawGeometry(null, MarkerPen, Polygon(new Point(p.X, p.Y - r - 1), new Point(p.X + r + 1, p.Y + r), new Point(p.X - r - 1, p.Y + r)));
                    break;
                case CadSnapKind.Center:
                    dc.DrawEllipse(null, MarkerPen, p, r + 1, r + 1);
                    break;
                case CadSnapKind.Quadrant:
                    dc.DrawGeometry(null, MarkerPen, Polygon(new Point(p.X, p.Y - r - 1), new Point(p.X + r + 1, p.Y), new Point(p.X, p.Y + r + 1), new Point(p.X - r - 1, p.Y)));
                    break;
                case CadSnapKind.Intersection:
                    dc.DrawLine(MarkerPen, new Point(p.X - r, p.Y - r), new Point(p.X + r, p.Y + r));
                    dc.DrawLine(MarkerPen, new Point(p.X - r, p.Y + r), new Point(p.X + r, p.Y - r));
                    break;
                case CadSnapKind.Perpendicular:
                    dc.DrawLine(MarkerPen, new Point(p.X - r, p.Y - r), new Point(p.X - r, p.Y + r));
                    dc.DrawLine(MarkerPen, new Point(p.X - r, p.Y + r), new Point(p.X + r, p.Y + r));
                    dc.DrawLine(MarkerPen, new Point(p.X - r, p.Y), new Point(p.X, p.Y));
                    dc.DrawLine(MarkerPen, new Point(p.X, p.Y), new Point(p.X, p.Y + r));
                    break;
                case CadSnapKind.Tangent:
                    dc.DrawEllipse(null, MarkerPen, p, r, r);
                    dc.DrawLine(MarkerPen, new Point(p.X - r, p.Y - r), new Point(p.X + r, p.Y - r));
                    break;
                case CadSnapKind.Nearest:
                    dc.DrawGeometry(null, MarkerPen, Polygon(new Point(p.X - r, p.Y - r), new Point(p.X + r, p.Y - r), new Point(p.X - r, p.Y + r), new Point(p.X + r, p.Y + r)));
                    break;
            }
        }

        private static System.Windows.Media.Geometry Polygon(params Point[] points)
        {
            var figure = new PathFigure { StartPoint = points[0], IsClosed = true };

            for (int i = 1; i < points.Length; i++)
            {
                figure.Segments.Add(new LineSegment(points[i], true));
            }

            var geometry = new PathGeometry(new[] { figure });
            geometry.Freeze();
            return geometry;
        }

        private void RequestRender()
        {
            if (_renderQueued)
            {
                return;
            }

            _renderQueued = true;
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopQueue()
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            StopQueue();

            if (_renderQueued)
            {
                RenderNow();
            }
        }

        /// <summary>A grip of a selected entity.</summary>
        private sealed class GripAt
        {
            public GripAt(string handle, CadGrip grip)
            {
                Handle = handle;
                Grip = grip;
            }

            public string Handle { get; }

            public CadGrip Grip { get; }
        }

        /// <summary>The tag over a selected viewport: its frame on the sheet and its scale.</summary>
        private sealed class ViewportTag
        {
            public ViewportTag(DisplayBox frame, string text)
            {
                Frame = frame;
                Text = text;
            }

            public DisplayBox Frame { get; }

            public string Text { get; }
        }
    }
}
