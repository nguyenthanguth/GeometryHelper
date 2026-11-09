using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

    /// <summary>The object an event of the canvas is about, as the one double-clicked.</summary>
    public sealed class CadObjectEventArgs : EventArgs
    {
        /// <summary>Makes the arguments.</summary>
        /// <param name="handle">The handle of the object.</param>
        public CadObjectEventArgs(string handle)
        {
            Handle = handle;
        }

        /// <summary>Gets the handle of the object.</summary>
        public string Handle { get; }
    }

    /// <summary>
    /// The canvas of the presenter: one space of a <see cref="CadDocument"/> drawn through <see cref="SkiaCanvasRenderer"/> into
    /// a bitmap of the device's pixels, and handled with the mouse as AutoCAD is. The wheel zooms about the cursor, 1.2 times
    /// a notch; dragging with the middle button pans and double-clicking it zooms to the extents. With no command running a
    /// left click picks within 5 pixels (Shift+click adds or removes), and a click on nothing starts a box, as AutoCAD does
    /// with PICKDRAG 0: the box follows the cursor to the click that ends it, left to right a solid blue window that takes
    /// what lies wholly inside, right to left a dashed green crossing that takes what it touches, Esc dropping it; the first
    /// corner stays on its point of the drawing while the wheel zooms or the middle button pans. <see cref="SelectByDragging"/>
    /// lets a box be dragged too; with it off, a press on nothing dragged draws a lasso, as AutoCAD's: drawn clockwise a window,
    /// counter-clockwise a crossing. The selected entities show their grips, which drag. With a
    /// command of <see cref="Session"/> running a click answers its prompt: a point prompt takes the point
    /// <see cref="Snap"/> gives, drawn with its marker and a rubber band from the prompt's base point, and "Select objects:"
    /// takes a click or a box; while MOVE, COPY, ROTATE, SCALE or MIRROR waits for its second point, a dashed ghost of the
    /// objects follows the cursor. A right click is Enter; Shift+right-click while a point is asked asks for the object snap
    /// menu (<see cref="SnapMenuRequested"/>); Esc cancels. <see cref="ShowGrid"/> draws grid lines under the drawing.
    /// <para>
    /// The drawing is kept as a picture and drawn again only when what it shows changes: the drawing, its layers, the view,
    /// the size or the colours. The selection is drawn over a copy of that picture and the entity under the cursor over the
    /// selection, so moving the mouse over a sheet of 100 000 items costs a copy of the picture and the one entity. While
    /// the wheel zooms or the middle button pans, the picture is moved and scaled with the view and drawn again once the
    /// view has been still for 80 milliseconds.
    /// </para>
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

        /// <summary>Identifies the <see cref="DynamicInput"/> property.</summary>
        public static readonly DependencyProperty DynamicInputProperty = DependencyProperty.Register(
            nameof(DynamicInput), typeof(bool), typeof(CadCanvas), new PropertyMetadata(false, (d, e) => ((CadCanvas)d).InvalidateVisual()));

        private const double GridPixels = 6.0;
        private const double DragPixels = 4.0;
        private const int SettleMilliseconds = 80;

        /// <summary>How long the cursor rests on a point before it is acquired for tracking, Extension or Parallel: half a second.</summary>
        private const int RestMilliseconds = 500;
        private const double GripSize = 8.0;
        private const int GripLimit = 100;
        private const double UcsIconArm = 44.0;
        private const double UcsIconLetter = 12.0;
        private const double UcsIconMargin = 12.0;

        private static readonly Brush WindowFill = Frozen(new SolidColorBrush(Color.FromArgb(26, 0x3D, 0x9B, 0xFF)));
        private static readonly Brush CrossingFill = Frozen(new SolidColorBrush(Color.FromArgb(26, 0x3F, 0xB9, 0x50)));
        private static readonly Pen WindowPen = Frozen(new Pen(Solid(0x3D9BFF), 1.0));
        private static readonly Pen CrossingPen = Frozen(new Pen(Solid(0x3FB950), 1.0) { DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0.0) });
        private static readonly Brush DefaultGripBrush = Solid(0x3D9BFF);
        private static readonly Brush HotGripBrush = Solid(0xF85149);
        private static readonly Brush DragGripBrush = Solid(0x3FB950);
        private static readonly Pen CrossPenLight = Frozen(new Pen(Solid(0xE6E9ED), 1.0));
        private static readonly Pen CrossPenDark = Frozen(new Pen(Solid(0x1A1A1A), 1.0));
        private static readonly Pen GhostPen = Frozen(new Pen(Solid(0x3D9BFF), 1.2) { DashStyle = new DashStyle(new[] { 6.0, 3.0 }, 0.0) });
        private static readonly Pen RubberPen =Frozen(new Pen(Solid(0x3D9BFF), 1.0) { DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0.0) });
        private static readonly Pen DarkMarkerPen = Frozen(new Pen(Solid(0xFFFF00), CadSnapMarker.LineWidth));
        private static readonly Pen LightMarkerPen = Frozen(new Pen(Solid(0x1A1A1A), CadSnapMarker.LineWidth));
        private static readonly Pen DarkDottedPen = Frozen(new Pen(Solid(0xFFFF00), 1.0) { DashStyle = new DashStyle(new[] { 1.0, 2.0 }, 0.0) });
        private static readonly Pen LightDottedPen = Frozen(new Pen(Solid(0x1A1A1A), 1.0) { DashStyle = new DashStyle(new[] { 1.0, 2.0 }, 0.0) });
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
        private SKBitmap _scene;
        private SKBitmap _marked;
        private SceneKey _sceneKey;
        private string _marking;
        private bool _markedDirty = true;
        private double[] _shown;
        private DispatcherTimer _settle;
        private bool _renderQueued;
        private Point? _panFrom;
        private Point? _boxFrom;
        private readonly CadSelectionCycler _cycler = new CadSelectionCycler();
        private GeoPoint2 _boxCorner;
        private List<GeoPoint2> _lasso;
        private Point _boxTo;
        private bool _boxing;
        private bool _boxOpen;
        private bool _pressOnObject;
        private bool _swallowUp;
        private Point? _pointDown;
        private GeoPoint2? _cursor;
        private Point? _mouse;
        private double _crosshairPercent;
        private int _gripRgb = 0x3D9BFF;
        private Brush _gripBrush = DefaultGripBrush;
        private CadSnapResult _snapped;
        private DispatcherTimer _rest;
        private GeoPoint2? _restAt;
        private double _restAperture;
        private int _hotGrip = -1;
        private int _dragGrip = -1;
        private GeoPoint2 _dragTo;

        /// <summary>Makes an empty canvas.</summary>
        public CadCanvas()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            Cursor = IdleCursor;
            Loaded += (s, e) => RequestRender();
            Unloaded += (s, e) =>
            {
                _renderQueued = false;
                StopQueue();
                _settle?.Stop();
                _rest?.Stop();
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

        /// <summary>Raised by Shift+right-click while a point is asked: the window shows the object snap menu at the cursor.</summary>
        public event EventHandler SnapMenuRequested;

        /// <summary>Raised by a double click on an object with no command running: the window edits a text in place, as AutoCAD's double click runs TEXTEDIT.</summary>
        public event EventHandler<CadObjectEventArgs> ObjectDoubleClicked;

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
        /// Gets or sets whether model space shows the grid (GRID, F7): lines across and up a little lighter than the
        /// background, every fifth a little stronger as AutoCAD's major lines, under the drawing, every
        /// <see cref="GridSpacing"/> units or, where those would come closer than 6 pixels, every 5, 10, 50, 100 times that.
        /// </summary>
        public bool ShowGrid
        {
            get => (bool)GetValue(ShowGridProperty);
            set => SetValue(ShowGridProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the box of dynamic input (DYN) is shown beside the cursor, which then gives the distance and
        /// angle: the tip of a point prompt names an object snap alone, "Endpoint", above the cursor, and the grid, ortho and
        /// polar none, as AutoCAD shows one tip. Off, the tip shows the distance and angle from the base point too,
        /// "Grid · 215.4 &lt; 22°".
        /// </summary>
        public bool DynamicInput
        {
            get => (bool)GetValue(DynamicInputProperty);
            set => SetValue(DynamicInputProperty, value);
        }

        /// <summary>
        /// Gets the tip a point prompt shows beside the cursor now; empty for none. A test reads it. With dynamic input shown
        /// it names an object snap alone, as AutoCAD's: the grid, ortho and polar give none.
        /// </summary>
        internal string PromptTip
        {
            get
            {
                if (!CadPicking.WantsPoint(Session) || !_cursor.HasValue)
                {
                    return string.Empty;
                }

                if (DynamicInput)
                {
                    CadSnapKind kind = _snapped?.Kind ?? CadSnapKind.None;
                    return kind == CadSnapKind.None || kind == CadSnapKind.Grid || kind == CadSnapKind.Ortho || kind == CadSnapKind.Polar ? string.Empty : CadSnapMarker.NameOf(kind);
                }

                return TipText(_snapped, Session.Prompt.BasePoint, _snapped?.Point ?? _cursor.Value, Session.Ucs);
            }
        }

        /// <summary>Gets or sets the spacing of the grid in drawing units; 10 by default, as the snap grid.</summary>
        public double GridSpacing
        {
            get => (double)GetValue(GridSpacingProperty);
            set => SetValue(GridSpacingProperty, value);
        }

        /// <summary>Gets how the space is drawn: background, paper and selection colours, the entity under the cursor.</summary>
        public CanvasOptions Options { get; } = new CanvasOptions();

        /// <summary>Gets or sets how much a notch of the wheel zooms: 1.2 by default, as AutoCAD's ZOOMFACTOR (Global settings › Canvas and cursor).</summary>
        public double WheelZoomFactor { get; set; } = 1.2;

        /// <summary>Gets or sets how far from the cursor a click picks an entity, in device-independent pixels: 5 by default, as AutoCAD's PICKBOX.</summary>
        public double PickboxPixels { get; set; } = 5.0;

        /// <summary>Gets or sets how far from the cursor a point snaps, in device-independent pixels: 10 by default, as AutoCAD's APERTURE.</summary>
        public double AperturePixels { get; set; } = 10.0;

        /// <summary>
        /// Gets or sets whether a box can also be dragged, pressed at one corner and released at the other, as AutoCAD's
        /// PICKDRAG 1: off by default, when a box is only clicked at one corner and then at the other, as PICKDRAG 0. Either
        /// way a click on nothing starts a box that the next click ends.
        /// </summary>
        public bool SelectByDragging { get; set; }

        /// <summary>
        /// Gets or sets the size of the crosshair drawn at the cursor, in per cent of the canvas's larger side, with the pickbox
        /// at its middle while no point is asked for, as AutoCAD's CURSORSIZE: 0, the default, for the system's cross.
        /// </summary>
        public double CrosshairPercent
        {
            get => _crosshairPercent;
            set
            {
                _crosshairPercent = Math.Max(0.0, Math.Min(100.0, value));
                Cursor = IdleCursor;
                InvalidateVisual();
            }
        }

        /// <summary>The cursor while nothing is dragged: none where the crosshair is drawn, else the system's cross.</summary>
        private Cursor IdleCursor => _crosshairPercent > 0.0 ? Cursors.None : Cursors.Cross;

        /// <summary>The brush of the grips, in the colour of <see cref="CanvasOptions.GripColor"/>.</summary>
        private Brush GripBrush
        {
            get
            {
                if (_gripRgb != Options.GripColor)
                {
                    _gripRgb = Options.GripColor;
                    _gripBrush = _gripRgb == 0x3D9BFF ? DefaultGripBrush : Solid(_gripRgb);
                }

                return _gripBrush;
            }
        }

        /// <summary>
        /// Gets the spacing the grid is drawn at: the spacing asked, times 5, 10, 50, 100 and on, the first whose lines lie at
        /// least a number of pixels apart. A grid of 10 at 0.18 pixels to the unit is drawn every 50, 9 pixels apart.
        /// </summary>
        /// <param name="pixelsPerUnit">The zoom.</param>
        /// <param name="spacing">The spacing asked, in drawing units.</param>
        /// <param name="leastPixels">The least distance between lines, in pixels.</param>
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
            var at = new Point(px / scale, py / scale);
            Track(at);

            if (_crosshairPercent > 0.0)
            {
                _mouse = at;
                InvalidateVisual();
            }
        }

        /// <summary>Gets how many times the drawing itself has been drawn, not counting the selection and the hover drawn over it; a test reads it.</summary>
        internal int SceneRenders { get; private set; }

        /// <summary>Gets whether the view has moved since the picture was drawn and the canvas waits for it to be still before drawing it again.</summary>
        internal bool IsSettling => _settle != null && _settle.IsEnabled;

        /// <summary>
        /// Draws the space now rather than at the next frame, as a screenshot taken with no window showing needs. The drawing
        /// itself is drawn again only where what it shows has changed; the selection and the entity under the cursor always.
        /// </summary>
        public void RenderNow()
        {
            _renderQueued = false;
            StopQueue();
            _settle?.Stop();
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

            CadDocument document = Document;
            CadSpace space = Space;
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            bool empty = document == null || space == null || view == null;

            if (!empty)
            {
                DrawScene(document, space, view, info, scale);
                DrawMarked(document, space, view, info);
            }

            _bitmap.Lock();

            try
            {
                using (SKSurface surface = SKSurface.Create(info, _bitmap.BackBuffer, _bitmap.BackBufferStride))
                {
                    if (empty)
                    {
                        int rgb = Options.Background;
                        surface.Canvas.Clear(new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
                    }
                    else
                    {
                        Copy(_marked ?? _scene, info, _bitmap.BackBuffer, _bitmap.BackBufferStride);
                        string hover = Options.Hover;

                        if (hover != null && !(Options.Selected is ICollection<string> marked && marked.Contains(hover)))
                        {
                            _renderer.RenderHighlight(surface.Canvas, document, space, view, Options, new[] { hover }, true);
                        }
                    }

                    surface.Canvas.Flush();
                }

                _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                _bitmap.Unlock();
            }

            if (empty)
            {
                _sceneKey = null;
                _shown = null;
                FindGrips();
            }
            else
            {
                _shown = new[] { view.CenterX, view.CenterY, view.PixelsPerUnit, view.Width, view.Height };
            }

            InvalidateVisual();
        }

        /// <summary>The drawing and its grid, into the kept picture, where what it shows has changed since it was last drawn.</summary>
        private void DrawScene(CadDocument document, CadSpace space, CadView view, SKImageInfo info, double scale)
        {
            var o = Options;
            DisplayBox paper = space.PaperBounds;
            CadUcs ucs = document.Ucs.WorkingOf(space);
            string numbers = string.Join("|", new object[]
            {
                view.CenterX, view.CenterY, view.PixelsPerUnit, view.Width, view.Height, info.Width, info.Height, scale,
                o.Background, o.Paper, o.ShowLineWeights, o.FontName, o.VisibleLineColor, o.HiddenLineColor, o.HiddenLayerName,
                ShowGrid, GridSpacing, paper.MinX, paper.MinY, paper.MaxX, paper.MaxY, ucs.Origin.X, ucs.Origin.Y, ucs.Angle,
            }.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)));
            var key = new SceneKey(document.GetDisplay(space), numbers);

            if (_scene != null && _scene.Width == info.Width && _scene.Height == info.Height && key.Same(_sceneKey))
            {
                return;
            }

            Fit(ref _scene, info);

            using (SKSurface surface = SKSurface.Create(info, _scene.GetPixels(), _scene.RowBytes))
            {
                _renderer.RenderScene(surface.Canvas, document, space, view, Options);
                surface.Canvas.Flush();
            }

            if (ShowGrid && space.IsModel)
            {
                DrawGrid(_scene, view, scale, ucs);
            }

            _sceneKey = key;
            _markedDirty = true;
            SceneRenders++;
        }

        /// <summary>
        /// The selection, and while "Select objects:" waits the objects it has gathered, drawn over a copy of the kept picture
        /// where either or the picture has changed; nothing kept where nothing is highlighted. The grips follow.
        /// </summary>
        private void DrawMarked(CadDocument document, CadSpace space, CadView view, SKImageInfo info)
        {
            CadCommandSession session = Session;
            string marking = string.Join("|", Options.SelectionColor, Options.ShowLineWeights, IsRunning, document.Selection.Count, session?.Gathering.Count ?? 0);

            if (!_markedDirty && marking == _marking && (_marked == null || (_marked.Width == info.Width && _marked.Height == info.Height)))
            {
                return;
            }

            IReadOnlyCollection<string> highlighted = Highlighted(document);
            Options.Selected = highlighted;

            if (highlighted.Count == 0)
            {
                _marked?.Dispose();
                _marked = null;
            }
            else
            {
                Fit(ref _marked, info);

                using (SKSurface surface = SKSurface.Create(info, _marked.GetPixels(), _marked.RowBytes))
                {
                    Copy(_scene, info, _marked.GetPixels(), _marked.RowBytes);
                    _renderer.RenderHighlight(surface.Canvas, document, space, view, Options, highlighted, false);
                    surface.Canvas.Flush();
                }
            }

            _marking = marking;
            _markedDirty = false;
            FindGrips();
        }

        /// <summary>A kept picture of the canvas's size, made anew where it has none or one of another size.</summary>
        private static void Fit(ref SKBitmap bitmap, SKImageInfo info)
        {
            if (bitmap != null && bitmap.Width == info.Width && bitmap.Height == info.Height)
            {
                return;
            }

            bitmap?.Dispose();
            bitmap = new SKBitmap(info);
        }

        /// <summary>Copies a kept picture, pixel for pixel, into a buffer of the same size.</summary>
        private static void Copy(SKBitmap from, SKImageInfo info, IntPtr to, int rowBytes)
        {
            using (SKPixmap pixels = from.PeekPixels())
            {
                pixels.ReadPixels(info, to, rowBytes);
            }
        }

        /// <summary>
        /// How the picture shown has to move to follow a view zoomed or panned since it was drawn, in device-independent
        /// units: scaled by the change of zoom about the canvas's corner and moved by the change of centre; null where the
        /// view is the one it was drawn at.
        /// </summary>
        private Transform MovedSinceDrawn(CadView view, double scale)
        {
            double[] shown = _shown;

            if (view == null || shown == null || (shown[0] == view.CenterX && shown[1] == view.CenterY && shown[2] == view.PixelsPerUnit && shown[3] == view.Width && shown[4] == view.Height))
            {
                return null;
            }

            double s = view.PixelsPerUnit / shown[2];
            double tx = view.Width / 2.0 + (shown[0] - view.CenterX) * view.PixelsPerUnit - shown[3] / 2.0 * s;
            double ty = view.Height / 2.0 - (shown[1] - view.CenterY) * view.PixelsPerUnit - shown[4] / 2.0 * s;
            var moved = new MatrixTransform(s, 0.0, 0.0, s, tx / scale, ty / scale);
            moved.Freeze();
            return moved;
        }

        /// <summary>The view zoomed or panned by the mouse: the picture follows it at once and is drawn again once the view is still.</summary>
        private void ViewMoved()
        {
            ViewChanged?.Invoke(this, EventArgs.Empty);

            if (_settle == null)
            {
                _settle = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(SettleMilliseconds) };
                _settle.Tick += (s, e) =>
                {
                    _settle.Stop();
                    RenderNow();
                };
            }

            _settle.Stop();
            _settle.Start();
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
            bool any = _boxFrom.HasValue || _dragGrip >= 0 || _cycler.IsArmed;
            _cycler.Disarm();
            _boxFrom = null;
            _lasso = null;
            _boxing = false;
            _boxOpen = false;
            _dragGrip = -1;

            if (any)
            {
                ReleaseMouseCapture();
                InvalidateVisual();
            }

            return any;
        }

        /// <summary>Releases the typefaces of the renderer and the kept pictures.</summary>
        public void Dispose()
        {
            StopQueue();
            _settle?.Stop();
            _renderer.Dispose();
            _scene?.Dispose();
            _scene = null;
            _marked?.Dispose();
            _marked = null;
            _sceneKey = null;
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var whole = new Rect(0, 0, ActualWidth, ActualHeight);
            int rgb = Options.Background;
            drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)), null, whole);
            double scale = PixelScale;

            CadView view = View;

            if (_bitmap != null)
            {
                Transform moved = MovedSinceDrawn(view, scale);

                if (moved != null)
                {
                    drawingContext.PushTransform(moved);
                }

                drawingContext.DrawImage(_bitmap, new Rect(0, 0, _bitmap.PixelWidth / scale, _bitmap.PixelHeight / scale));

                if (moved != null)
                {
                    drawingContext.Pop();
                }
            }

            if (view != null)
            {
                DrawUcsIcon(drawingContext, view, scale);
                DrawTags(drawingContext, view, scale);
                DrawGrips(drawingContext, view, scale);
                DrawPrompt(drawingContext, view, scale);
                DrawCrosshair(drawingContext);
            }

            if (_lasso != null && view != null)
            {
                DrawLasso(drawingContext, view, scale);
            }
            else if (_boxing && _boxFrom.HasValue)
            {
                Point a = BoxCorner();
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
            view.ZoomAt(p.X * scale, p.Y * scale, Math.Pow(WheelZoomFactor, e.Delta / 120.0));
            e.Handled = true;
            ViewMoved();
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
            else if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2 && !IsRunning && ObjectDoubleClicked != null && ObjectAt(p) is string handle)
            {
                CancelGesture();
                ObjectDoubleClicked(this, new CadObjectEventArgs(handle));
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                Track(p);

                if (e.ClickCount == 2 && EditAttributesAt(p))
                {
                    _swallowUp = true;
                }
                else if (CadPicking.WantsPoint(Session))
                {
                    _pointDown = p;
                }
                else if (_boxOpen && _boxFrom.HasValue)
                {
                    // The click that ends a box started by a click; its release is not a click of its own.
                    Point from = BoxCorner();
                    GeoPoint2 corner = _boxCorner;
                    CancelGesture();
                    _swallowUp = true;
                    SelectBox(corner, from, p, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
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
                    _boxCorner = DrawingAt(p);
                    _boxTo = p;
                    _boxing = false;
                    _pressOnObject = IsObjectAt(p);
                    CaptureMouse();
                }

                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point at = PositionOf(e);
            Track(at);

            if (_crosshairPercent > 0.0)
            {
                _mouse = at;
                InvalidateVisual();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            Point at = PositionOf(e);

            if (e.ChangedButton == MouseButton.Middle && _panFrom.HasValue)
            {
                _panFrom = null;
                Cursor = IdleCursor;
                ReleaseMouseCapture();

                if (IsSettling)
                {
                    // The pan has ended: the picture is drawn at the next frame rather than after the wait.
                    _settle.Stop();
                    RequestRender();
                }

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
            else if (e.ChangedButton == MouseButton.Left && _swallowUp)
            {
                _swallowUp = false;
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left && _boxFrom.HasValue && !_boxOpen)
            {
                Point from = BoxCorner();
                Point pressed = _boxFrom.Value;
                GeoPoint2 corner = _boxCorner;
                bool boxing = _boxing;
                List<GeoPoint2> lasso = _lasso;
                _lasso = null;
                ReleaseMouseCapture();
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

                if (lasso != null && CadPicking.IsLoop(lasso))
                {
                    // A lasso drawn clockwise takes what it holds whole, counter-clockwise what it touches.
                    CancelGesture();

                    if (Document != null && Space != null)
                    {
                        CadPicking.LassoDone(Document, Space, Session, lasso, shift);
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                else if (boxing && SelectByDragging)
                {
                    CancelGesture();
                    SelectBox(corner, from, at, shift);
                }
                else if (_pressOnObject || !TakesBox)
                {
                    // An object pressed is the one picked, wherever the button is let go.
                    CancelGesture();
                    Click(_pressOnObject ? pressed : at, shift);
                }
                else
                {
                    // A press on nothing leaves the box open, following the cursor to the click that ends it.
                    _boxOpen = true;
                    _boxing = true;
                    _boxTo = at;
                    InvalidateVisual();
                }

                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Right && (Keyboard.Modifiers & ModifierKeys.Shift) != 0 && CadPicking.WantsPoint(Session))
            {
                // Shift+right-click while a point is asked: the object snap menu, as AutoCAD's, its choice for that point alone.
                SnapMenuRequested?.Invoke(this, EventArgs.Empty);
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
            _rest?.Stop();
            _restAt = null;

            if (_mouse.HasValue)
            {
                _mouse = null;
                InvalidateVisual();
            }

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

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            _markedDirty = true;
            RequestRender();
        }

        private void OnPromptChanged(object sender, EventArgs e)
        {
            _snapped = null;
            _ghost.Follow(Session);
            _markedDirty = true;

            if (_boxOpen)
            {
                // A command started or a prompt answered drops a box waiting for its second corner.
                CancelGesture();
            }

            RequestRender();
        }

        /// <summary>
        /// The grid's lines, across and up, put into the picture of the drawing only where it shows the background, so that
        /// they lie under the drawing: one pixel wide, 8 per cent lighter than the background, every fifth from the origin 18
        /// per cent lighter, as AutoCAD draws its major lines.
        /// </summary>
        private void DrawGrid(SKBitmap picture, CadView view, double scale, CadUcs ucs)
        {
            double step = GridStep(view.PixelsPerUnit, GridSpacing, GridPixels * scale);

            if (step <= 0.0)
            {
                return;
            }

            int width = picture.Width;
            int height = picture.Height;
            int stride = picture.RowBytes / 4;
            int rgb = Options.Background & 0xFFFFFF;
            int background = unchecked((int)0xFF000000) | rgb;
            int Lighter(double share) => unchecked((int)0xFF000000) | (Lift((rgb >> 16) & 0xFF, share) << 16) | (Lift((rgb >> 8) & 0xFF, share) << 8) | Lift(rgb & 0xFF, share);
            int minor = Lighter(0.08);
            int major = Lighter(0.18);
            var pixels = new int[stride * height];
            Marshal.Copy(picture.GetPixels(), pixels, 0, pixels.Length);
            DisplayBox visible = view.VisibleBox;

            if (!ucs.IsWorld)
            {
                DrawUcsGrid(pixels, width, height, stride, view, step, ucs, background, minor, major);
                Marshal.Copy(pixels, 0, picture.GetPixels(), pixels.Length);
                return;
            }

            for (long k = (long)Math.Ceiling(visible.MinX / step); k * step <= visible.MaxX; k++)
            {
                view.DrawingToPixel(k * step, 0.0, out double px, out _);
                int ix = (int)Math.Round(px);
                int color = k % 5 == 0 ? major : minor;

                for (int iy = 0; ix >= 0 && ix < width && iy < height; iy++)
                {
                    if (pixels[iy * stride + ix] == background)
                    {
                        pixels[iy * stride + ix] = color;
                    }
                }
            }

            for (long k = (long)Math.Ceiling(visible.MinY / step); k * step <= visible.MaxY; k++)
            {
                view.DrawingToPixel(0.0, k * step, out _, out double py);
                int iy = (int)Math.Round(py);
                int color = k % 5 == 0 ? major : minor;

                for (int ix = 0; iy >= 0 && iy < height && ix < width; ix++)
                {
                    if (pixels[iy * stride + ix] == background)
                    {
                        pixels[iy * stride + ix] = color;
                    }
                }
            }

            Marshal.Copy(pixels, 0, picture.GetPixels(), pixels.Length);
        }

        /// <summary>
        /// The grid of a UCS other than the world's, as AutoCAD draws it: its lines along the axes of the UCS and through its
        /// origin, each pixel of the background within half a pixel of one taken, every fifth from the origin the stronger.
        /// </summary>
        private static void DrawUcsGrid(int[] pixels, int width, int height, int stride, CadView view, double step, CadUcs ucs, int background, int minor, int major)
        {
            // The UCS coordinates of a pixel are linear in its column and row: those of the corner and the steps across and down.
            view.ScreenToDrawing(0.0, 0.0, out double x0, out double y0);
            view.ScreenToDrawing(1.0, 0.0, out double x1, out double y1);
            view.ScreenToDrawing(0.0, 1.0, out double x2, out double y2);
            GeoPoint2 corner = ucs.ToUcs(new GeoPoint2(x0, y0));
            GeoVector2 across = ucs.ToUcs(new GeoVector2(x1 - x0, y1 - y0)) / step;
            GeoVector2 down = ucs.ToUcs(new GeoVector2(x2 - x0, y2 - y0)) / step;
            double half = 0.5 / (view.PixelsPerUnit * step);

            for (int iy = 0; iy < height; iy++)
            {
                double u = corner.X / step + iy * down.X;
                double v = corner.Y / step + iy * down.Y;

                for (int ix = 0; ix < width; ix++, u += across.X, v += across.Y)
                {
                    int at = iy * stride + ix;

                    if (pixels[at] != background)
                    {
                        continue;
                    }

                    double ku = Math.Round(u);
                    double kv = Math.Round(v);
                    bool onU = Math.Abs(u - ku) <= half;
                    bool onV = Math.Abs(v - kv) <= half;

                    if (onU || onV)
                    {
                        bool strong = (onU && Math.Abs(Math.IEEERemainder(ku, 5.0)) < 0.5) || (onV && Math.Abs(Math.IEEERemainder(kv, 5.0)) < 0.5);
                        pixels[at] = strong ? major : minor;
                    }
                }
            }
        }

        /// <summary>A channel of a colour taken a share of the way to white.</summary>
        private static int Lift(int channel, double share) => channel + (int)((255 - channel) * share);

        /// <summary>Gets whether a box may start here: with no command running, or while "Select objects:" waits.</summary>
        private bool TakesBox => !IsRunning || Session.Prompt?.Kind == CadPromptKind.Selection;

        /// <summary>
        /// A double click with no command running on an INSERT with attributes: the Enhanced Attribute Editor, as AutoCAD opens
        /// it, by EATTEDIT started once the click is done with and given the INSERT; false where no such INSERT is there.
        /// </summary>
        private bool EditAttributesAt(Point p)
        {
            CadDocument document = Document;
            CadView view = View;
            CadCommandSession session = Session;

            if (document == null || Space == null || view == null || session == null || session.IsRunning)
            {
                return false;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            IReadOnlyList<string> picked = document.GetPickIndex(Space).Pick(x, y, PickboxPixels * scale / view.PixelsPerUnit);

            if (picked.Count == 0 || !(document.FindEntity(picked[0]) is DxfBlockReference insert) || insert.AttributeCollection.Count == 0)
            {
                return false;
            }

            CancelGesture();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                session.Start("EATTEDIT");
                session.FeedSelection(new[] { insert.Handle });
            }));
            return true;
        }

        /// <summary>Whether an entity lies within the pickbox of a point of the canvas.</summary>
        /// <summary>The object nearest a point of the canvas within the pick box, or null.</summary>
        private string ObjectAt(Point p)
        {
            CadDocument document = Document;
            CadView view = View;

            if (document == null || Space == null || view == null)
            {
                return null;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            IReadOnlyList<string> found = document.GetPickIndex(Space).Pick(x, y, PickboxPixels * scale / view.PixelsPerUnit);
            return found.Count > 0 ? found[0] : null;
        }

        private bool IsObjectAt(Point p)
        {
            CadDocument document = Document;
            CadView view = View;

            if (document == null || Space == null || view == null)
            {
                return false;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            return document.GetPickIndex(Space).Pick(x, y, PickboxPixels * scale / view.PixelsPerUnit).Count > 0;
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
            double pick = PickboxPixels * scale / view.PixelsPerUnit;
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
                ViewMoved();
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
                Vector moved = p - BoxCorner();
                bool far = Math.Abs(moved.X) > DragPixels || Math.Abs(moved.Y) > DragPixels;

                // Select by dragging off, a press on nothing dragged draws a lasso, as AutoCAD's: its points kept in drawing
                // units, one every 2 pixels.
                if (_lasso == null && far && !_boxOpen && !SelectByDragging && !_pressOnObject && TakesBox)
                {
                    _lasso = new List<GeoPoint2> { _boxCorner };
                }

                if (_lasso != null)
                {
                    if ((p - ToScreen(view, _lasso[_lasso.Count - 1], scale)).Length >= 2.0)
                    {
                        _lasso.Add(cursor);
                    }

                    InvalidateVisual();
                    return;
                }

                // A press on an object picks it unless a box may be dragged.
                _boxing |= far && (SelectByDragging || !_pressOnObject);

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
                RestartRest(cursor, aperture);
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

        /// <summary>The cursor moved at a point prompt: the wait for it to rest starts again, where tracking, Extension or Parallel can use a rest.</summary>
        private void RestartRest(GeoPoint2 cursor, double aperture)
        {
            ObjectSnap snap = Snap;
            _rest?.Stop();
            _restAt = null;

            if (snap == null || !(snap.Tracking || ((snap.Override ?? (snap.IsOn ? snap.Modes : CadSnapModes.None)) & (CadSnapModes.Extension | CadSnapModes.Parallel)) != 0))
            {
                return;
            }

            if (_rest == null)
            {
                _rest = new DispatcherTimer(DispatcherPriority.Input, Dispatcher) { Interval = TimeSpan.FromMilliseconds(RestMilliseconds) };
                _rest.Tick += (s, e) => AcquireAtRest();
            }

            _restAt = cursor;
            _restAperture = aperture;
            _rest.Start();
        }

        /// <summary>
        /// The cursor has rested for <see cref="RestMilliseconds"/> at a point prompt: the point under it is acquired, or let go,
        /// as AutoCAD acquires one when the cursor pauses on it, and the snap is worked out again from it.
        /// </summary>
        /// <returns>Whether a point was acquired or let go.</returns>
        internal bool AcquireAtRest()
        {
            _rest?.Stop();
            CadCommandSession session = Session;
            CadDocument document = Document;

            if (!_restAt.HasValue || document == null || Space == null || !CadPicking.WantsPoint(session))
            {
                return false;
            }

            GeoPoint2 at = _restAt.Value;
            _restAt = null;

            if (!CadPicking.Acquire(document, Space, Snap, at, _restAperture, session.Prompt.BasePoint))
            {
                return false;
            }

            _snapped = CadPicking.Snap(document, Space, Snap, at, _restAperture, session.Prompt.BasePoint);
            InvalidateVisual();
            return true;
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
            double aperture = (CadPicking.WantsPoint(Session) ? AperturePixels : PickboxPixels) * scale / view.PixelsPerUnit;
            var at = new GeoPoint2(x, y);

            // Shift+Space armed: each click at one place takes the next object there, as AutoCAD's cycling.
            if (_cycler.IsArmed && (Session == null || !Session.IsRunning))
            {
                _cycler.Next(document, Space, at, aperture);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
                return;
            }

            // SELECTIONCYCLING 2: where several objects lie under the pick, the list of them to choose from.
            IReadOnlyList<string> overlapping = CadPicking.Overlapping(document, Space, Session, at, aperture);

            if (overlapping.Count > 1)
            {
                ShowCycleList(document, overlapping, shift);
                return;
            }

            if (CadPicking.Click(document, Space, Session, Snap, at, aperture, shift))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            _snapped = null;
            InvalidateVisual();
        }

        /// <summary>
        /// The Selection list of SELECTIONCYCLING 2 near the cursor: a row for each object under the pick, "Line · WALLS",
        /// the nearest first; the row chosen is taken as a click on that object alone.
        /// </summary>
        private void ShowCycleList(CadDocument document, IReadOnlyList<string> handles, bool shift)
        {
            var items = new List<CadMenuItemViewModel>();

            foreach (string handle in handles)
            {
                string chosen = handle;
                items.Add(new CadMenuItemViewModel(CadPicking.CycleLabel(document, handle), () =>
                {
                    CadPicking.Choose(document, Session, chosen, shift);
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    InvalidateVisual();
                }));
            }

            System.Windows.Controls.ContextMenu menu = CadMenus.Make(items);
            menu.PlacementTarget = this;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        /// <summary>Arms selection cycling, as Shift+Space does in AutoCAD: each click at one place then takes the next object there.</summary>
        public void ArmCycling() => _cycler.Arm();

        /// <summary>A box ended: its first corner kept in drawing units and shown at <paramref name="a"/>, its second at <paramref name="b"/>.</summary>
        private void SelectBox(GeoPoint2 corner, Point a, Point b, bool remove)
        {
            CadDocument document = Document;
            CadView view = View;

            if (document == null || Space == null || view == null)
            {
                return;
            }

            double scale = PixelScale;
            double x0 = corner.X;
            double y0 = corner.Y;
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

        /// <summary>The point of the drawing under a point of the canvas; the origin with no view.</summary>
        private GeoPoint2 DrawingAt(Point p)
        {
            CadView view = View;

            if (view == null)
            {
                return default;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            return new GeoPoint2(x, y);
        }

        /// <summary>
        /// Where the canvas shows the first corner of a box now: the corner is kept in drawing units, so that a wheel zoom or a
        /// pan while a box waits for its second click keeps it on the same point of the drawing.
        /// </summary>
        private Point BoxCorner()
        {
            CadView view = View;
            return view == null ? _boxFrom.Value : ToScreen(view, _boxCorner, PixelScale);
        }

        /// <summary>The lasso being drawn, closed back to its start: solid blue drawn clockwise, a window; dashed green the other way, a crossing.</summary>
        private void DrawLasso(DrawingContext dc, CadView view, double scale)
        {
            if (_lasso.Count < 2)
            {
                return;
            }

            bool crossing = CadPicking.SignedArea(_lasso) > 0.0;
            var geometry = new StreamGeometry();

            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(ToScreen(view, _lasso[0], scale), true, true);

                for (int i = 1; i < _lasso.Count; i++)
                {
                    context.LineTo(ToScreen(view, _lasso[i], scale), true, false);
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(crossing ? CrossingFill : WindowFill, crossing ? CrossingPen : WindowPen, geometry);
        }

        /// <summary>
        /// The crosshair at the cursor, as AutoCAD draws it: two lines across a share of the canvas, in the colour that shows on
        /// its background, and the pickbox square at their meeting while no point is asked for.
        /// </summary>
        private void DrawCrosshair(DrawingContext dc)
        {
            if (_crosshairPercent <= 0.0 || !_mouse.HasValue || _panFrom.HasValue)
            {
                return;
            }

            int rgb = Space != null && !Space.IsModel ? Options.Paper : Options.Background;
            double luminance = (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255.0;
            Pen pen = luminance < 0.5 ? CrossPenLight : CrossPenDark;
            Point at = _mouse.Value;
            double half = _crosshairPercent / 100.0 * Math.Max(ActualWidth, ActualHeight) / 2.0;
            double box = PickboxPixels;
            bool picking = !CadPicking.WantsPoint(Session);
            double gap = picking ? box : 0.0;

            // The lines run along the axes of the UCS, as AutoCAD's crosshair does; across and up in the world's.
            Vector along = CrosshairAxis();
            var up = new Vector(along.Y, -along.X);
            dc.DrawLine(pen, at - half * along, at - gap * along);
            dc.DrawLine(pen, at + gap * along, at + half * along);
            dc.DrawLine(pen, at + half * up, at + gap * up);
            dc.DrawLine(pen, at - gap * up, at - half * up);

            if (picking)
            {
                dc.DrawRectangle(null, pen, new Rect(at.X - box, at.Y - box, 2.0 * box, 2.0 * box));
            }
        }

        /// <summary>The way the X axis of the UCS runs on the canvas, of unit length: to the right for the world's, Y being down on the screen.</summary>
        private Vector CrosshairAxis()
        {
            CadDocument document = Document;
            CadUcs ucs = document == null || Space == null ? CadUcs.World : document.Ucs.WorkingOf(Space);
            return ucs.IsWorld ? new Vector(1.0, 0.0) : new Vector(Math.Cos(ucs.Angle), -Math.Sin(ucs.Angle));
        }

        /// <summary>
        /// Where the UCS icon is drawn, in device-independent pixels: the corner its arms leave and the way of its X arm, at the
        /// origin of the UCS where UCSICON ORigin is on and the whole icon fits there, else at the lower left corner, as
        /// AutoCAD places it. False where UCSICON is off. A test reads it.
        /// </summary>
        internal bool TryPlaceUcsIcon(out Point corner, out Vector xArm, out bool atOrigin)
        {
            corner = default;
            atOrigin = false;
            xArm = CrosshairAxis();
            CadDocument document = Document;
            CadView view = View;

            if (document == null || Space == null || view == null || !document.Ucs.IconOn)
            {
                return false;
            }

            // The box the arms and their letters take about the corner.
            var yArm = new Vector(xArm.Y, -xArm.X);
            Vector[] reach = { new Vector(0.0, 0.0), xArm * (UcsIconArm + UcsIconLetter), yArm * (UcsIconArm + UcsIconLetter), (xArm + yArm) * UcsIconArm };
            double minX = reach.Min(v => v.X) - UcsIconLetter / 2.0;
            double maxX = reach.Max(v => v.X) + UcsIconLetter / 2.0;
            double minY = reach.Min(v => v.Y) - UcsIconLetter / 2.0;
            double maxY = reach.Max(v => v.Y) + UcsIconLetter / 2.0;
            CadUcs ucs = document.Ucs.WorkingOf(Space);

            if (document.Ucs.IconAtOrigin)
            {
                Point origin = ToScreen(view, new GeoPoint2(ucs.Origin.X, ucs.Origin.Y), PixelScale);

                if (origin.X + minX >= UcsIconMargin && origin.X + maxX <= ActualWidth - UcsIconMargin && origin.Y + minY >= UcsIconMargin && origin.Y + maxY <= ActualHeight - UcsIconMargin)
                {
                    corner = origin;
                    atOrigin = true;
                    return true;
                }
            }

            corner = new Point(UcsIconMargin - minX, ActualHeight - UcsIconMargin - maxY);
            return true;
        }

        /// <summary>
        /// The UCS icon, as AutoCAD's 2D icon: the X and Y arms with their arrows and letters along the axes of the UCS, a small
        /// box at their corner with a + in it where the icon is at the origin, and a W by the Y arm while the UCS is the world;
        /// white on a dark background, dark on a light one.
        /// </summary>
        private void DrawUcsIcon(DrawingContext dc, CadView view, double scale)
        {
            if (!TryPlaceUcsIcon(out Point corner, out Vector xArm, out bool atOrigin))
            {
                return;
            }

            int rgb = Space.IsModel ? Options.Background : Options.Paper;
            double luminance = (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255.0;
            Brush ink = luminance < 0.5 ? Brushes.White : Brushes.Black;
            var pen = new Pen(ink, 1.5);
            pen.Freeze();
            var yArm = new Vector(xArm.Y, -xArm.X);
            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (Vector arm in new[] { xArm, yArm })
            {
                var side = new Vector(-arm.Y, arm.X);
                Point tip = corner + arm * UcsIconArm;
                dc.DrawLine(pen, corner, tip - arm * 8.0);
                var head = new StreamGeometry();

                using (StreamGeometryContext context = head.Open())
                {
                    context.BeginFigure(tip, true, true);
                    context.LineTo(tip - arm * 9.0 + side * 3.5, true, false);
                    context.LineTo(tip - arm * 9.0 - side * 3.5, true, false);
                }

                head.Freeze();
                dc.DrawGeometry(ink, null, head);
                var letter = new FormattedText(arm == xArm ? "X" : "Y", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, TagFace, 11.0, ink, dip);
                Point at = corner + arm * (UcsIconArm + UcsIconLetter / 2.0 + 2.0);
                dc.DrawText(letter, new Point(at.X - letter.Width / 2.0, at.Y - letter.Height / 2.0));
            }

            // The box at the corner, along the arms.
            var box = new StreamGeometry();

            using (StreamGeometryContext context = box.Open())
            {
                context.BeginFigure(corner, false, true);
                context.LineTo(corner + xArm * 8.0, true, false);
                context.LineTo(corner + (xArm + yArm) * 8.0, true, false);
                context.LineTo(corner + yArm * 8.0, true, false);
            }

            box.Freeze();
            dc.DrawGeometry(null, pen, box);

            if (atOrigin)
            {
                Point middle = corner + (xArm + yArm) * 4.0;
                dc.DrawLine(pen, middle - xArm * 2.5, middle + xArm * 2.5);
                dc.DrawLine(pen, middle - yArm * 2.5, middle + yArm * 2.5);
            }

            if (Document.Ucs.WorkingOf(Space).IsWorld)
            {
                var w = new FormattedText("W", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, TagFace, 10.0, ink, dip);
                Point at = corner + yArm * 20.0 + xArm * 10.0;
                dc.DrawText(w, new Point(at.X - w.Width / 2.0, at.Y - w.Height / 2.0));
            }
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

            DrawTracking(dc, view, scale);
            DrawMarker(dc, _snapped, to);
            string tip = PromptTip;

            if (tip.Length > 0)
            {
                var text = new FormattedText(tip, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, TipFace, 10.0, TipInk, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                double w = text.Width + 16.0;

                // Beside the cursor, below and to the right; turned to the other side where the canvas ends. With the box of
                // dynamic input below the cursor, the name of the snap goes above it.
                double left = to.X + 16.0 + w > ActualWidth ? to.X - 16.0 - w : to.X + 16.0;
                double top = DynamicInput || to.Y + 14.0 + 22.0 > ActualHeight ? to.Y - 14.0 - 22.0 : to.Y + 14.0;
                var box = new Rect(left, top, w, 22.0);
                dc.DrawRoundedRectangle(TipFill, TipPen, box, 2.0, 2.0);
                dc.DrawText(text, new Point(box.X + 8.0, box.Y + (22.0 - text.Height) / 2.0));
            }
        }

        /// <summary>
        /// The points acquired, as AutoCAD marks them: a small cross, 7 pixels across, a line acquired for Parallel by the
        /// marker of Parallel; and the dotted paths of the snap to its point (an extension, a parallel, tracking lines).
        /// </summary>
        private void DrawTracking(DrawingContext dc, CadView view, double scale)
        {
            Pen pen = MarkerPen(Options.Background);
            ObjectSnap snap = Snap;

            if (snap != null)
            {
                foreach (CadAcquiredPoint acquired in snap.Acquired)
                {
                    Point at = ToScreen(view, acquired.Point, scale);

                    if (acquired.Kind == CadSnapKind.Parallel)
                    {
                        DrawMarker(dc, CadSnapKind.Parallel, at, pen);
                        continue;
                    }

                    dc.DrawLine(pen, new Point(at.X - 3.5, at.Y), new Point(at.X + 3.5, at.Y));
                    dc.DrawLine(pen, new Point(at.X, at.Y - 3.5), new Point(at.X, at.Y + 3.5));
                }
            }

            if (_snapped == null)
            {
                return;
            }

            foreach (IReadOnlyList<GeoPoint2> track in _snapped.Tracks)
            {
                for (int i = 1; i < track.Count; i++)
                {
                    dc.DrawLine(DottedPen(pen), ToScreen(view, track[i - 1], scale), ToScreen(view, track[i], scale));
                }
            }
        }

        /// <summary>The tip by the cursor, as AutoCAD's dynamic input: the snap's name, the distance and the angle from the base point, the angle from the X axis of the UCS.</summary>
        private static string TipText(CadSnapResult snapped, GeoPoint2? basePoint, GeoPoint2 target, CadUcs ucs)
        {
            string kind = snapped == null ? string.Empty : CadSnapMarker.NameOf(snapped.Kind);

            if (!basePoint.HasValue)
            {
                return kind;
            }

            GeoVector2 way = ucs.ToUcs(target - basePoint.Value);
            double degrees = Math.Atan2(way.Y, way.X) * 180.0 / Math.PI;
            degrees = degrees < 0.0 ? degrees + 360.0 : degrees;
            string measured = way.Length.ToString("0.0", CultureInfo.InvariantCulture) + " < " + degrees.ToString("0", CultureInfo.InvariantCulture) + "°";
            return kind.Length == 0 ? measured : kind + " · " + measured;
        }

        /// <summary>
        /// The marker of a snap over the kept picture, as AutoCAD draws it (<see cref="CadSnapMarker"/>): its shape 9 pixels
        /// across, 1.5 wide, yellow on a dark background and dark on a light one.
        /// </summary>
        private void DrawMarker(DrawingContext dc, CadSnapResult snapped, Point p)
        {
            if (snapped == null)
            {
                return;
            }

            DrawMarker(dc, snapped.Kind, p, MarkerPen(Options.Background));
        }

        /// <summary>Draws a snap kind's marker about a point with a pen, as the canvas and Drafting Settings draw it.</summary>
        internal static void DrawMarker(DrawingContext dc, CadSnapKind kind, Point p, Pen pen)
        {
            foreach (CadMarkerStroke stroke in CadSnapMarker.Strokes(kind))
            {
                var figure = new PathFigure { StartPoint = new Point(p.X + stroke.Points[0].X, p.Y + stroke.Points[0].Y), IsClosed = stroke.IsClosed, IsFilled = stroke.IsFilled };

                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    figure.Segments.Add(new LineSegment(new Point(p.X + stroke.Points[i].X, p.Y + stroke.Points[i].Y), true));
                }

                var geometry = new PathGeometry(new[] { figure });
                geometry.Freeze();
                dc.DrawGeometry(stroke.IsFilled ? pen.Brush : null, stroke.IsDotted ? DottedPen(pen) : pen, geometry);
            }
        }

        /// <summary>The pen of the markers on a background: AutoCAD's yellow on a dark one, dark grey on a light one.</summary>
        internal static Pen MarkerPen(int background)
        {
            int rgb = CadSnapMarker.ColorOn(background);
            return rgb == 0xFFFF00 ? DarkMarkerPen : LightMarkerPen;
        }

        private static Pen DottedPen(Pen pen) => pen == LightMarkerPen ? LightDottedPen : DarkDottedPen;

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

            if (IsSettling)
            {
                // The view is moving: the picture follows it and is drawn once the view is still.
                _renderQueued = false;
                return;
            }

            if (_renderQueued)
            {
                RenderNow();
            }
        }

        /// <summary>
        /// What the kept picture shows: the display list, which the document makes anew when the drawing or its layers
        /// change, and the view, the size, the colours and the grid, written out.
        /// </summary>
        private sealed class SceneKey
        {
            private readonly DisplayList2 _list;
            private readonly string _numbers;

            public SceneKey(DisplayList2 list, string numbers)
            {
                _list = list;
                _numbers = numbers;
            }

            public bool Same(SceneKey other) => other != null && ReferenceEquals(_list, other._list) && string.Equals(_numbers, other._numbers, StringComparison.Ordinal);
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
