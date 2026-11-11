using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            : this(x, y, x, y) { }

        /// <summary>Makes the arguments of a cursor whose rubber band ends elsewhere, as at a snap.</summary>
        /// <param name="x">The X of the drawing point.</param>
        /// <param name="y">The Y of the drawing point.</param>
        /// <param name="bandX">The X of the point the rubber band ends at.</param>
        /// <param name="bandY">The Y of the point the rubber band ends at.</param>
        public CadCursorEventArgs(double x, double y, double bandX, double bandY)
        {
            X = x;
            Y = y;
            BandX = bandX;
            BandY = bandY;
        }

        /// <summary>Gets the X of the drawing point.</summary>
        public double X { get; }

        /// <summary>Gets the Y of the drawing point.</summary>
        public double Y { get; }

        /// <summary>
        /// Gets the X of the point the rubber band ends at, which a click takes: at a point prompt the snap's, an endpoint, ortho
        /// or polar, where one holds the cursor; else the cursor's own, <see cref="X"/>.
        /// </summary>
        public double BandX { get; }

        /// <summary>Gets the Y of the point the rubber band ends at, as <see cref="BandX"/> says of the X.</summary>
        public double BandY { get; }
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

    /// <summary>The arguments of a custom grip clicked: the handle of its block reference and the grip's index.</summary>
    public sealed class CadGripEventArgs : EventArgs
    {
        /// <summary>Makes the arguments.</summary>
        /// <param name="handle">The handle of the block reference.</param>
        /// <param name="gripIndex">The index of the grip, as <see cref="CadGrips.Of"/> gives it.</param>
        public CadGripEventArgs(string handle, int gripIndex)
        {
            Handle = handle;
            GripIndex = gripIndex;
        }

        /// <summary>Gets the handle of the block reference.</summary>
        public string Handle { get; }

        /// <summary>Gets the index of the grip, as <see cref="CadGrips.Of"/> gives it.</summary>
        public int GripIndex { get; }
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
            nameof(Document),
            typeof(CadDocument),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnDocumentChanged)
        );

        /// <summary>Identifies the <see cref="Space"/> property.</summary>
        public static readonly DependencyProperty SpaceProperty = DependencyProperty.Register(
            nameof(Space),
            typeof(CadSpace),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="View"/> property.</summary>
        public static readonly DependencyProperty ViewProperty = DependencyProperty.Register(
            nameof(View),
            typeof(CadView),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="Revision"/> property.</summary>
        public static readonly DependencyProperty RevisionProperty = DependencyProperty.Register(
            nameof(Revision),
            typeof(int),
            typeof(CadCanvas),
            new PropertyMetadata(0, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="Session"/> property.</summary>
        public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
            nameof(Session),
            typeof(CadCommandSession),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnSessionChanged)
        );

        /// <summary>Identifies the <see cref="Snap"/> property.</summary>
        public static readonly DependencyProperty SnapProperty = DependencyProperty.Register(
            nameof(Snap),
            typeof(ObjectSnap),
            typeof(CadCanvas),
            new PropertyMetadata(null)
        );

        /// <summary>Identifies the <see cref="ShowGrid"/> property.</summary>
        public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
            nameof(ShowGrid),
            typeof(bool),
            typeof(CadCanvas),
            new PropertyMetadata(false, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="GridSpacing"/> property.</summary>
        public static readonly DependencyProperty GridSpacingProperty = DependencyProperty.Register(
            nameof(GridSpacing),
            typeof(double),
            typeof(CadCanvas),
            new PropertyMetadata(10.0, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="GridSpacingY"/> property.</summary>
        public static readonly DependencyProperty GridSpacingYProperty = DependencyProperty.Register(
            nameof(GridSpacingY),
            typeof(double),
            typeof(CadCanvas),
            new PropertyMetadata(10.0, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="GridMajor"/> property.</summary>
        public static readonly DependencyProperty GridMajorProperty = DependencyProperty.Register(
            nameof(GridMajor),
            typeof(int),
            typeof(CadCanvas),
            new PropertyMetadata(5, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="SnapBase"/> property.</summary>
        public static readonly DependencyProperty SnapBaseProperty = DependencyProperty.Register(
            nameof(SnapBase),
            typeof(GeoPoint2),
            typeof(CadCanvas),
            new PropertyMetadata(GeoPoint2.Origin, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="SnapAngle"/> property.</summary>
        public static readonly DependencyProperty SnapAngleProperty = DependencyProperty.Register(
            nameof(SnapAngle),
            typeof(double),
            typeof(CadCanvas),
            new PropertyMetadata(0.0, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="Isoplane"/> property.</summary>
        public static readonly DependencyProperty IsoplaneProperty = DependencyProperty.Register(
            nameof(Isoplane),
            typeof(CadIsoplane?),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnSceneChanged)
        );

        /// <summary>Identifies the <see cref="DynamicInput"/> property.</summary>
        public static readonly DependencyProperty DynamicInputProperty = DependencyProperty.Register(
            nameof(DynamicInput),
            typeof(bool),
            typeof(CadCanvas),
            new PropertyMetadata(false, (d, e) => ((CadCanvas)d).InvalidateVisual())
        );

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
        private static readonly Pen CrossingPen = Frozen(
            new Pen(Solid(0x3FB950), 1.0) { DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0.0) }
        );
        private static readonly Brush DefaultGripBrush = Solid(0x3D9BFF);
        private static readonly Brush HotGripBrush = Solid(0xF85149);

        // A dynamic block's custom grips, in AutoCAD's dynamic grip colour (GRIPDYNCOLOR) and edged dark, as the design draws them.
        private static readonly Brush CustomGripBrush = Solid(0x19B5F0);
        private static readonly Pen CustomGripEdge = Frozen(new Pen(Solid(0x0B1220), 0.8));
        private static readonly Brush GripTipFill = Solid(0x262B33);
        private static readonly Pen GripTipEdge = Frozen(new Pen(Solid(0x3A414C), 1.0));
        private static readonly Brush GripTipInk = Solid(0xC9D1DB);
        private static readonly Typeface GripTipFace = new Typeface(
            new FontFamily("JetBrains Mono, Consolas"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal
        );
        private static readonly Brush CellFill = Frozen(new SolidColorBrush(Color.FromArgb(46, 0x3D, 0x9B, 0xFF)));
        private static readonly Pen CellPen = Frozen(new Pen(Solid(0x3D9BFF), 3.0));
        private static readonly Brush CellBrush = Solid(0x3D9BFF);
        private static readonly Brush BandFill = Solid(0x5B6573);
        private static readonly Brush BandHot = Frozen(new SolidColorBrush(Color.FromArgb(153, 0xD2, 0x99, 0x22)));
        private static readonly Pen CrossPenLight = Frozen(new Pen(Solid(0xE6E9ED), 1.0));
        private static readonly Pen CrossPenDark = Frozen(new Pen(Solid(0x1A1A1A), 1.0));
        private static readonly Pen GhostPen = Frozen(
            new Pen(Solid(0x3D9BFF), 1.2) { DashStyle = new DashStyle(new[] { 6.0, 3.0 }, 0.0) }
        );
        private static readonly Pen RubberPen = Frozen(
            new Pen(Solid(0x3D9BFF), 1.0) { DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0.0) }
        );
        private static readonly Pen DarkMarkerPen = Frozen(new Pen(Solid(0xFFFF00), CadSnapMarker.LineWidth));
        private static readonly Pen LightMarkerPen = Frozen(new Pen(Solid(0x1A1A1A), CadSnapMarker.LineWidth));
        private static readonly Pen DarkDottedPen = Frozen(
            new Pen(Solid(0xFFFF00), 1.0) { DashStyle = new DashStyle(new[] { 1.0, 2.0 }, 0.0) }
        );
        private static readonly Pen LightDottedPen = Frozen(
            new Pen(Solid(0x1A1A1A), 1.0) { DashStyle = new DashStyle(new[] { 1.0, 2.0 }, 0.0) }
        );
        private static readonly Pen BasePen = Frozen(new Pen(Solid(0x3FB950), 2.0));
        private static readonly Pen ActiveFrameLight = Frozen(new Pen(Solid(0xE6E9ED), 3.0));
        private static readonly Pen ActiveFrameDark = Frozen(new Pen(Solid(0x1A1A1A), 3.0));
        private static readonly Brush TipFill = Solid(0x1A1E24);
        private static readonly Pen TipPen = Frozen(new Pen(Solid(0x3A414C), 1.0));
        private static readonly Brush TipInk = Solid(0xE6E9ED);
        private static readonly Brush TagInk = Solid(0x0B1220);
        private static readonly Typeface TipFace = new Typeface(
            new FontFamily("Inter, Segoe UI"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal
        );
        private static readonly Typeface TagFace = new Typeface(
            new FontFamily("Inter, Segoe UI"),
            FontStyles.Normal,
            FontWeights.SemiBold,
            FontStretches.Normal
        );

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
        private string _typed;
        private Point? _mouse;
        private double _crosshairPercent;
        private int _gripRgb = 0x3D9BFF;
        private Brush _gripBrush = DefaultGripBrush;
        private CadSnapResult _snapped;
        private DispatcherTimer _rest;
        private GeoPoint2? _restAt;
        private double _restAperture;
        private int _hotGrip = -1;

        // The tip of the custom grip under the cursor: "Width = 900.00 · snaps 800 / 900 / 1000"; null for none.
        private string _gripTip;

        // The hot grips, each the handle of its object and its index there, the one a grip edit starts from first; a grip
        // Shift+clicked turns hot, and a grip edit runs while _gripEditing.
        private readonly List<KeyValuePair<string, int>> _hot = new List<KeyValuePair<string, int>>();
        private bool _gripEditing;
        private Point? _gripPress;

        // Through a viewport: its view in model units as the picture was drawn, and as the wheel or the middle button has
        // moved it since, written into the viewport as one undo step once the view is still.
        private CadView _throughDrawn;
        private CadView _throughNow;
        private string _throughName;

        /// <summary>Makes an empty canvas.</summary>
        public CadCanvas()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            Cursor = IdleCursor;

            // The pictures of images are decoded off the window's thread; the canvas draws again as each is ready.
            _renderer.LazyImages = true;
            Loaded += (s, e) =>
            {
                CadImageCache.Decoded += OnPictureDecoded;
                RequestRender();
            };
            Unloaded += (s, e) =>
            {
                CadImageCache.Decoded -= OnPictureDecoded;
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

        /// <summary>
        /// Raised by a click on a dynamic block's flip, visibility or lookup grip, as AutoCAD's: the window flips the block, or
        /// shows the grip's list at the cursor. A drag grip starts the grip edit instead.
        /// </summary>
        public event EventHandler<CadGripEventArgs> CustomGripClicked;

        /// <summary>Raised by a right-click while no command runs: the window shows the shortcut menu of the selection at the cursor.</summary>
        public event EventHandler ShortcutMenuRequested;

        /// <summary>Raised by a double click on an object with no command running: the window edits a text in place, as AutoCAD's double click runs TEXTEDIT.</summary>
        public event EventHandler<CadObjectEventArgs> ObjectDoubleClicked;

        /// <summary>
        /// Gets or sets what a click with no command running inside a cell of a table does (the point, whether Shift was held):
        /// true where it selected the cell, and the click selects nothing else. A click on a line of the table's grid, within the
        /// pick box, picks the table as any object. Set by the window.
        /// </summary>
        public Func<GeoPoint2, bool, bool> CellClicked { get; set; }

        /// <summary>Gets or sets what a double click with no command running inside a cell of a table does: true where it opened the cell for typing. Set by the window.</summary>
        public Func<GeoPoint2, bool> CellDoubleClicked { get; set; }

        /// <summary>Gets or sets what a click with no command running outside every cell does: the cells of a table selected let go. Set by the window.</summary>
        public Action CellsLetGo { get; set; }

        /// <summary>Gets or sets what gives the corners of the cells of a table selected and of their table, which the canvas draws; null for none. Set by the window.</summary>
        public Func<GeoPoint2[][]> CellOutline { get; set; }

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
        /// background, a major line every <see cref="GridMajor"/> a little stronger as AutoCAD's, under the drawing, every
        /// <see cref="GridSpacing"/> units or, where those would come closer than 6 pixels, at the major lines alone, then 2,
        /// 5, 2 and on times them: every 5, 10, 50, 100 times the spacing with a GRIDMAJOR of 5.
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

                // AUTOSNAP 2 off leaves out the name of an object snap, and 32 off the tip where ortho, polar or tracking hold the cursor.
                ObjectSnap snap = Snap;
                bool tracked =
                    _snapped != null
                    && (
                        _snapped.Kind == CadSnapKind.Ortho
                        || _snapped.Kind == CadSnapKind.Polar
                        || _snapped.Kind == CadSnapKind.Tracking
                        || _snapped.Tracks.Count > 0
                    );
                bool shown = snap == null || (tracked ? snap.ShowsTrackingTip : snap.ShowsSnapTip);

                if (DynamicInput)
                {
                    CadSnapKind kind = _snapped?.Kind ?? CadSnapKind.None;
                    return
                        !shown
                        || kind == CadSnapKind.None
                        || kind == CadSnapKind.Grid
                        || kind == CadSnapKind.Ortho
                        || kind == CadSnapKind.Polar
                        ? string.Empty
                        : CadSnapMarker.NameOf(kind);
                }

                if (tracked && !shown)
                {
                    return string.Empty;
                }

                return TipText(
                    shown ? _snapped : null,
                    Session.Prompt.BasePoint,
                    _snapped?.Point ?? _cursor.Value,
                    Session
                );
            }
        }

        /// <summary>Whether a snap is an object snap, whose marker the magnet holds the cursor on: not the grid, ortho, polar or a tracking line.</summary>
        private static bool IsObjectSnap(CadSnapKind kind) =>
            kind != CadSnapKind.None
            && kind != CadSnapKind.Grid
            && kind != CadSnapKind.Ortho
            && kind != CadSnapKind.Polar
            && kind != CadSnapKind.Tracking;

        /// <summary>Gets or sets the spacing of the grid across, in drawing units; 10 by default, as the snap grid.</summary>
        public double GridSpacing
        {
            get => (double)GetValue(GridSpacingProperty);
            set => SetValue(GridSpacingProperty, value);
        }

        /// <summary>Gets or sets the spacing of the grid up, in drawing units, as the Y of GRIDUNIT; 10 by default.</summary>
        public double GridSpacingY
        {
            get => (double)GetValue(GridSpacingYProperty);
            set => SetValue(GridSpacingYProperty, value);
        }

        /// <summary>
        /// Gets or sets how many lines of the grid lie between its major lines, as AutoCAD's GRIDMAJOR; 5 by default. A grid of
        /// 10 with 4 draws a stronger line every 40 units.
        /// </summary>
        public int GridMajor
        {
            get => (int)GetValue(GridMajorProperty);
            set => SetValue(GridMajorProperty, value);
        }

        /// <summary>Gets or sets the point the grid is laid from, in the coordinates of the UCS, as AutoCAD's SNAPBASE; the origin by default.</summary>
        public GeoPoint2 SnapBase
        {
            get => (GeoPoint2)GetValue(SnapBaseProperty);
            set => SetValue(SnapBaseProperty, value);
        }

        /// <summary>
        /// Gets or sets the angle the grid and the crosshair are turned by, in radians from the X axis of the UCS, as AutoCAD's
        /// SNAPANG; nought by default.
        /// </summary>
        public double SnapAngle
        {
            get => (double)GetValue(SnapAngleProperty);
            set => SetValue(SnapAngleProperty, value);
        }

        /// <summary>
        /// Gets or sets the plane of an isometric snap, as AutoCAD's SNAPISOPAIR while SNAPSTYL is 1: the grid is drawn on the
        /// two axes of the plane, its lines through points twice <see cref="GridSpacingY"/> apart, and the crosshair along
        /// them; null, by default, for the rectangular grid and crosshair.
        /// </summary>
        public CadIsoplane? Isoplane
        {
            get => (CadIsoplane?)GetValue(IsoplaneProperty);
            set => SetValue(IsoplaneProperty, value);
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
        public static double GridStep(double pixelsPerUnit, double spacing, double leastPixels) =>
            AdaptiveStep(pixelsPerUnit, spacing, leastPixels, 5);

        /// <summary>
        /// The spacing the grid is drawn at with a major line every so many: the spacing asked, then the major lines alone,
        /// then 2, 5, 2 and on times that, the first whose lines lie at least a number of pixels apart; with 5, as
        /// <see cref="GridStep"/> gives.
        /// </summary>
        private static double AdaptiveStep(double pixelsPerUnit, double spacing, double leastPixels, int major)
        {
            if (!(spacing > 0.0) || !(pixelsPerUnit > 0.0))
            {
                return 0.0;
            }

            double step = spacing;

            for (int i = 0; step * pixelsPerUnit < leastPixels && i < 60; i++)
            {
                step *=
                    i == 0 && major > 1 ? major
                    : i % 2 == 0 ? 5.0
                    : 2.0;
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
            CadView view = WorkView;

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
        /// itself is drawn again only where what it shows has changed; the selection and the entity under the cursor always. A
        /// view of a viewport zoomed or panned through it and not yet written is written first, one undo step.
        /// </summary>
        public void RenderNow()
        {
            _renderQueued = false;
            StopQueue();
            _settle?.Stop();
            CommitThrough();
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
            bool fresh =
                _bitmap == null
                || _bitmap.PixelWidth != width
                || _bitmap.PixelHeight != height
                || PresentationSource.FromVisual(this) == null;

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

                        if (
                            hover != null
                            && !(Options.Selected is ICollection<string> marked && marked.Contains(hover))
                        )
                        {
                            Highlight(surface.Canvas, document, new[] { hover }, true);
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
                _throughDrawn = Through is DxfViewport through ? CadViewports.ModelView(view, through) : null;
            }

            InvalidateVisual();
        }

        /// <summary>The drawing and its grid, into the kept picture, where what it shows has changed since it was last drawn.</summary>
        private void DrawScene(CadDocument document, CadSpace space, CadView view, SKImageInfo info, double scale)
        {
            var o = Options;
            DisplayBox paper = space.PaperBounds;
            CadUcs ucs = document.Ucs.WorkingOf(space);
            string numbers = string.Join(
                "|",
                new object[]
                {
                    view.CenterX,
                    view.CenterY,
                    view.PixelsPerUnit,
                    view.Width,
                    view.Height,
                    info.Width,
                    info.Height,
                    scale,
                    o.Background,
                    o.Paper,
                    o.BlockEditorBackground,
                    o.ShowLineWeights,
                    o.FontName,
                    o.VisibleLineColor,
                    o.HiddenLineColor,
                    o.HiddenLayerName,
                    ShowGrid,
                    GridSpacing,
                    GridSpacingY,
                    GridMajor,
                    SnapBase.X,
                    SnapBase.Y,
                    SnapAngle,
                    Isoplane,
                    paper.MinX,
                    paper.MinY,
                    paper.MaxX,
                    paper.MaxY,
                    ucs.Origin.X,
                    ucs.Origin.Y,
                    ucs.Angle,
                    CadImageCache.Version,
                }.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))
            );
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
            string marking = string.Join(
                "|",
                Options.SelectionColor,
                Options.ShowLineWeights,
                IsRunning,
                document.Selection.Count,
                session?.Gathering.Count ?? 0
            );

            if (
                !_markedDirty
                && marking == _marking
                && (_marked == null || (_marked.Width == info.Width && _marked.Height == info.Height))
            )
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
                using (SKPixmap pixels = _marked.PeekPixels())
                {
                    Copy(_scene, info, _marked.GetPixels(), _marked.RowBytes);
                    Highlight(surface.Canvas, document, highlighted, false, pixels);
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

            if (
                view == null
                || shown == null
                || (
                    shown[0] == view.CenterX
                    && shown[1] == view.CenterY
                    && shown[2] == view.PixelsPerUnit
                    && shown[3] == view.Width
                    && shown[4] == view.Height
                )
            )
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
                _settle = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(SettleMilliseconds),
                };
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

        /// <summary>Stops a box being dragged, or turns the grips Shift made hot cold, as the first Esc does.</summary>
        /// <returns>Whether there was one to stop.</returns>
        public bool CancelGesture()
        {
            bool any = _boxFrom.HasValue || _cycler.IsArmed || (_hot.Count > 0 && !_gripEditing);
            _cycler.Disarm();
            _boxFrom = null;
            _lasso = null;
            _boxing = false;
            _boxOpen = false;
            _gripPress = null;

            if (!_gripEditing)
            {
                _hot.Clear();
            }

            if (any)
            {
                ReleaseMouseCapture();
                InvalidateVisual();
            }

            return any;
        }

        /// <summary>A picture decoded on a worker thread: the canvas draws again on its own thread.</summary>
        private void OnPictureDecoded(object sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(RequestRender));

        /// <summary>Releases the typefaces of the renderer and the kept pictures.</summary>
        public void Dispose()
        {
            CadImageCache.Decoded -= OnPictureDecoded;
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
            int rgb = Space != null && Space.IsBlockEditor ? Options.BlockEditorBackground : Options.Background;
            drawingContext.DrawRectangle(
                new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)),
                null,
                whole
            );
            double scale = PixelScale;

            CadView view = View;

            if (_bitmap != null)
            {
                Transform moved = MovedSinceDrawn(view, scale);

                if (moved != null)
                {
                    drawingContext.PushTransform(moved);
                }

                drawingContext.DrawImage(
                    _bitmap,
                    new Rect(0, 0, _bitmap.PixelWidth / scale, _bitmap.PixelHeight / scale)
                );

                if (moved != null)
                {
                    drawingContext.Pop();
                }

                DrawMovedThrough(drawingContext, scale);
            }

            // Through a viewport, what follows the cursor is drawn in model units, as the viewport shows them.
            CadView work = WorkView;

            if (view != null)
            {
                DrawUcsIcon(drawingContext, work, scale);
                DrawTags(drawingContext, view, scale);
                DrawActiveFrame(drawingContext);
                DrawGrips(drawingContext, work, scale);
                DrawCells(drawingContext, work, scale);
                DrawMark(drawingContext, work, scale);
                DrawPrompt(drawingContext, work, scale);
                DrawBand(drawingContext, work, scale);
                DrawCrosshair(drawingContext);
            }

            if (_lasso != null && view != null)
            {
                DrawLasso(drawingContext, work, scale);
            }
            else if (_boxing && _boxFrom.HasValue)
            {
                Point a = BoxCorner();
                bool crossing = _boxTo.X < a.X;
                drawingContext.DrawRectangle(
                    crossing ? CrossingFill : WindowFill,
                    crossing ? CrossingPen : WindowPen,
                    new Rect(a, _boxTo)
                );
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
            double factor = Math.Pow(WheelZoomFactor, e.Delta / 120.0);

            // Through a viewport its view zooms, the paper staying; through one whose display is locked, the paper zooms.
            if (MovesThrough)
            {
                CadView model = WorkView;
                model.ZoomAt(p.X * scale, p.Y * scale, factor);
                _throughNow = model;
                _throughName = "ZOOM";
            }
            else
            {
                view.ZoomAt(p.X * scale, p.Y * scale, factor);
            }

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
            else if (e.ChangedButton == MouseButton.Left && ViewportClick(p, e.ClickCount == 2))
            {
                CancelGesture();
                _swallowUp = true;
                e.Handled = true;
            }
            else if (
                e.ChangedButton == MouseButton.Left
                && e.ClickCount == 2
                && !IsRunning
                && CellDoubleClicked != null
                && InsideCell(p)
                && CellDoubleClicked(DrawingAt(p))
            )
            {
                CancelGesture();
                _swallowUp = true;
                e.Handled = true;
            }
            else if (
                e.ChangedButton == MouseButton.Left
                && e.ClickCount == 2
                && !IsRunning
                && ObjectDoubleClicked != null
                && ObjectAt(p) is string handle
            )
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
                    // Shift+click makes a grip hot, or cold again; a click starts the grip edit from the grip clicked, the
                    // grips made hot before going with it, as AutoCAD's. Let go away from where it was pressed, the grip is
                    // dragged there.
                    var grip = new KeyValuePair<string, int>(_grips[_hotGrip].Handle, _grips[_hotGrip].Grip.Index);
                    CadGripKind kind = _grips[_hotGrip].Grip.Kind;

                    if (
                        (Keyboard.Modifiers & ModifierKeys.Shift) == 0
                        && (kind == CadGripKind.CustomFlip || kind == CadGripKind.CustomList)
                    )
                    {
                        // A dynamic block's flip grip flips it and a visibility or lookup grip lists its values, as AutoCAD's.
                        _hot.Clear();
                        _swallowUp = true;
                        CustomGripClicked?.Invoke(this, new CadGripEventArgs(grip.Key, grip.Value));
                    }
                    else if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    {
                        if (!_hot.Remove(grip))
                        {
                            _hot.Add(grip);
                        }

                        _swallowUp = true;
                    }
                    else
                    {
                        _hot.Remove(grip);
                        _hot.Insert(0, grip);
                        _gripEditing = Session != null && Session.StartGripEdit(_hot);

                        if (_gripEditing)
                        {
                            _gripPress = p;
                            CaptureMouse();
                        }
                        else
                        {
                            _hot.Clear();
                        }
                    }

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

            // Through a viewport the cursor is an arrow off its frame, as AutoCAD's.
            if (!_panFrom.HasValue && Through is DxfViewport through)
            {
                Cursor = InsideFrame(through, at) ? IdleCursor : Cursors.Arrow;
            }

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
            else if (e.ChangedButton == MouseButton.Left && _gripPress.HasValue)
            {
                // The press that started a grip edit: let go where it was pressed, the grip waits for a click; dragged away,
                // the point it is let go at, snapped, is the stretch point.
                Vector moved = at - _gripPress.Value;
                _gripPress = null;
                ReleaseMouseCapture();

                if (Math.Abs(moved.X) > DragPixels || Math.Abs(moved.Y) > DragPixels)
                {
                    Track(at);
                    Click(at, false);
                    HistoryWritten?.Invoke(this, EventArgs.Empty);
                }

                InvalidateVisual();
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

                    if (Document != null && WorkSpace != null)
                    {
                        CadPicking.LassoDone(Document, WorkSpace, Session, lasso, shift);
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
            else if (
                e.ChangedButton == MouseButton.Right
                && (Keyboard.Modifiers & ModifierKeys.Shift) != 0
                && CadPicking.WantsPoint(Session)
            )
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
            else if (e.ChangedButton == MouseButton.Right && ShortcutMenuRequested != null)
            {
                // With no command running, the shortcut menu of the selection, as AutoCAD's.
                ShortcutMenuRequested(this, EventArgs.Empty);
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

        /// <summary>Shift pressed or released: a preview that follows it, as TRIM's, is drawn anew.</summary>
        internal void OnShiftChanged()
        {
            if (Session?.Prompt?.Preview != null)
            {
                InvalidateVisual();
            }
        }

        /// <summary>Gets or sets the text typed so far in the command line, which a preview of a line of text draws, as TEXT's at "Enter text".</summary>
        internal string Typed
        {
            get => _typed;
            set
            {
                _typed = value;

                if (Session?.Prompt?.Kind == CadPromptKind.String && Session.Prompt.Preview != null)
                {
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Gets or sets what holds the point of a prompt in place of the cursor, as the fields of dynamic input do once one
        /// holds a number: given the cursor, snapped, and whether a click takes the point now, the point held, or null to
        /// leave the cursor free. Null for none.
        /// </summary>
        internal Func<GeoPoint2, bool, GeoPoint2?> HeldAt { get; set; }

        /// <summary>Gets or sets where a mouse event happened; null for the mouse itself. A test sets it, having no mouse to move.</summary>
        internal Func<MouseEventArgs, Point> Pointer { get; set; }

        private bool IsRunning => Session != null && Session.IsRunning;

        private Point PositionOf(MouseEventArgs e) => Pointer != null ? Pointer(e) : e.GetPosition(this);

        private static SolidColorBrush Solid(int rgb) =>
            Frozen(new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)));

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
                old.ActiveViewportChanged -= canvas.OnActiveViewportChanged;
            }

            if (e.NewValue is CadDocument next)
            {
                next.Selection.Changed += canvas.OnSelectionChanged;
                next.ActiveViewportChanged += canvas.OnActiveViewportChanged;
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
                old.UiRequested -= canvas.OnImageRequested;
            }

            if (e.NewValue is CadCommandSession next)
            {
                next.PromptChanged += canvas.OnPromptChanged;
                next.UiRequested += canvas.OnImageRequested;
            }

            canvas._ghost.Follow(e.NewValue as CadCommandSession);

            canvas.RequestRender();
        }

        /// <summary>
        /// IMAGEOUT, the image of PNGOUT, JPGOUT or BMPOUT: the scene the canvas keeps, drawn anew at its view and size with its
        /// colours, its grid where all objects are written, and none of what is drawn over it (cursor, grips, highlight, the
        /// UCS icon); with objects selected, those alone on the background.
        /// </summary>
        private void OnImageRequested(object sender, CadUiRequestEventArgs e)
        {
            CadImageJob job = e.Image;
            CadView view = View;

            if (
                e.Name != "IMAGEOUT"
                || job?.FileName == null
                || view == null
                || Document == null
                || e.Document != Document
            )
            {
                return;
            }

            e.Handled = true;

            try
            {
                using (SKBitmap picture = CadRasterWriter.Draw(Document, job.Space, view, Options, job.Handles))
                {
                    if (job.Handles == null && ShowGrid && job.Space.IsModel)
                    {
                        DrawGrid(picture, view, PixelScale, Document.Ucs.WorkingOf(job.Space));
                    }

                    File.WriteAllBytes(job.FileName, CadRasterWriter.Encode(picture, job.Format));
                }
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                GeometryHelperLog.Warn("CadCanvas: the image " + job.FileName + " was not written.", exception);
                ((CadCommandSession)sender).Write("The image was not written: " + exception.Message);
            }
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

            // The hot grips go cold once their grip edit has ended, as AutoCAD's.
            if (_gripEditing && Session != null && !Session.IsGripEditing)
            {
                _gripEditing = false;
                _hot.Clear();
            }

            if (_boxOpen)
            {
                // A command started or a prompt answered drops a box waiting for its second corner.
                CancelGesture();
            }

            RequestRender();
        }

        /// <summary>
        /// The grid's lines, across and up, put into the picture of the drawing only where it shows the background, so that
        /// they lie under the drawing: one pixel wide, 8 per cent lighter than the background, a major line every
        /// <see cref="GridMajor"/> from the base point 18 per cent lighter, as AutoCAD draws its major lines; turned by
        /// <see cref="SnapAngle"/> about <see cref="SnapBase"/>, or on the two axes of the isometric plane, as AutoCAD lays
        /// them.
        /// </summary>
        private void DrawGrid(SKBitmap picture, CadView view, double scale, CadUcs ucs)
        {
            int every = Math.Max(1, GridMajor);
            CadIsoplane? isoplane = Isoplane;

            // An isometric grid has its lines through points twice the Y spacing apart, as AutoCAD keeps half of it, the
            // lines of each way that far times the cosine of 30 degrees apart.
            double across = isoplane.HasValue ? 2.0 * GridSpacingY * Math.Sqrt(3.0) / 2.0 : GridSpacing;
            double up = isoplane.HasValue ? across : GridSpacingY;
            double step = AdaptiveStep(view.PixelsPerUnit, across, GridPixels * scale, every);
            double stepY = AdaptiveStep(view.PixelsPerUnit, up, GridPixels * scale, every);

            if (step <= 0.0 || stepY <= 0.0)
            {
                return;
            }

            int width = picture.Width;
            int height = picture.Height;
            int stride = picture.RowBytes / 4;
            int rgb =
                (Space != null && Space.IsBlockEditor ? Options.BlockEditorBackground : Options.Background) & 0xFFFFFF;
            int background = unchecked((int)0xFF000000) | rgb;
            int Lighter(double share) =>
                unchecked((int)0xFF000000)
                | (Lift((rgb >> 16) & 0xFF, share) << 16)
                | (Lift((rgb >> 8) & 0xFF, share) << 8)
                | Lift(rgb & 0xFF, share);
            int minor = Lighter(0.08);
            int major = Lighter(0.18);
            var pixels = new int[stride * height];
            Marshal.Copy(picture.GetPixels(), pixels, 0, pixels.Length);
            DisplayBox visible = view.VisibleBox;
            Tolerance tolerance = Snap?.Tolerance ?? Tolerance.Default;
            bool turned = Math.Abs(Math.IEEERemainder(SnapAngle, 2.0 * Math.PI)) > tolerance.EqualAngleRad;
            bool based = SnapBase.DistanceTo(GeoPoint2.Origin) > tolerance.EqualPoint;

            if (isoplane.HasValue || turned || based)
            {
                // The lines of each way, given by the angle they run at in the world, through the base point: those up the
                // grid the X spacing apart, those across it the Y spacing.
                double angle = (ucs.IsWorld ? 0.0 : ucs.Angle) + (turned ? SnapAngle : 0.0);
                SnapAxes(isoplane, out double first, out double second);
                GeoPoint2 origin = ucs.ToWorld(SnapBase);
                DrawLineGrid(
                    pixels,
                    width,
                    height,
                    stride,
                    view,
                    origin,
                    angle + second,
                    step,
                    angle + first,
                    stepY,
                    background,
                    minor,
                    major,
                    every
                );
                Marshal.Copy(pixels, 0, picture.GetPixels(), pixels.Length);
                return;
            }

            if (!ucs.IsWorld)
            {
                DrawUcsGrid(pixels, width, height, stride, view, step, stepY, ucs, background, minor, major, every);
                Marshal.Copy(pixels, 0, picture.GetPixels(), pixels.Length);
                return;
            }

            for (long k = (long)Math.Ceiling(visible.MinX / step); k * step <= visible.MaxX; k++)
            {
                view.DrawingToPixel(k * step, 0.0, out double px, out _);
                int ix = (int)Math.Round(px);
                int color = k % every == 0 ? major : minor;

                for (int iy = 0; ix >= 0 && ix < width && iy < height; iy++)
                {
                    if (pixels[iy * stride + ix] == background)
                    {
                        pixels[iy * stride + ix] = color;
                    }
                }
            }

            for (long k = (long)Math.Ceiling(visible.MinY / stepY); k * stepY <= visible.MaxY; k++)
            {
                view.DrawingToPixel(0.0, k * stepY, out _, out double py);
                int iy = (int)Math.Round(py);
                int color = k % every == 0 ? major : minor;

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
        /// origin, each pixel of the background within half a pixel of one taken, a major line every so many (GRIDMAJOR) from
        /// the origin the stronger.
        /// </summary>
        private static void DrawUcsGrid(
            int[] pixels,
            int width,
            int height,
            int stride,
            CadView view,
            double step,
            double stepY,
            CadUcs ucs,
            int background,
            int minor,
            int major,
            int every
        )
        {
            // The UCS coordinates of a pixel, in steps of the grid, are linear in its column and row: those of the corner and
            // the steps across and down.
            view.ScreenToDrawing(0.0, 0.0, out double x0, out double y0);
            view.ScreenToDrawing(1.0, 0.0, out double x1, out double y1);
            view.ScreenToDrawing(0.0, 1.0, out double x2, out double y2);
            GeoPoint2 corner = ucs.ToUcs(new GeoPoint2(x0, y0));
            GeoVector2 right = ucs.ToUcs(new GeoVector2(x1 - x0, y1 - y0));
            GeoVector2 below = ucs.ToUcs(new GeoVector2(x2 - x0, y2 - y0));
            var across = new GeoVector2(right.X / step, right.Y / stepY);
            var down = new GeoVector2(below.X / step, below.Y / stepY);
            double halfU = 0.5 / (view.PixelsPerUnit * step);
            double halfV = 0.5 / (view.PixelsPerUnit * stepY);

            for (int iy = 0; iy < height; iy++)
            {
                double u = corner.X / step + iy * down.X;
                double v = corner.Y / stepY + iy * down.Y;

                for (int ix = 0; ix < width; ix++, u += across.X, v += across.Y)
                {
                    int at = iy * stride + ix;

                    if (pixels[at] != background)
                    {
                        continue;
                    }

                    double ku = Math.Round(u);
                    double kv = Math.Round(v);
                    bool onU = Math.Abs(u - ku) <= halfU;
                    bool onV = Math.Abs(v - kv) <= halfV;

                    if (onU || onV)
                    {
                        bool strong =
                            (onU && Math.Abs(Math.IEEERemainder(ku, every)) < 0.5)
                            || (onV && Math.Abs(Math.IEEERemainder(kv, every)) < 0.5);
                        pixels[at] = strong ? major : minor;
                    }
                }
            }
        }

        /// <summary>
        /// A grid of two ways of lines through a point of the world, each given by the angle it runs at and how far apart its
        /// lines lie, as AutoCAD draws the grid turned by SNAPANG from SNAPBASE, and the isometric grid on the two axes of its
        /// plane: each pixel of the background within half a pixel of a line taken, a major line every so many (GRIDMAJOR)
        /// from the point the stronger.
        /// </summary>
        private static void DrawLineGrid(
            int[] pixels,
            int width,
            int height,
            int stride,
            CadView view,
            GeoPoint2 origin,
            double angleU,
            double stepU,
            double angleV,
            double stepV,
            int background,
            int minor,
            int major,
            int every
        )
        {
            // How many steps a point lies off the line of each way through the origin is linear in the pixel's column and row.
            view.ScreenToDrawing(0.0, 0.0, out double x0, out double y0);
            view.ScreenToDrawing(1.0, 0.0, out double x1, out double y1);
            view.ScreenToDrawing(0.0, 1.0, out double x2, out double y2);
            double nux = -Math.Sin(angleU) / stepU;
            double nuy = Math.Cos(angleU) / stepU;
            double nvx = -Math.Sin(angleV) / stepV;
            double nvy = Math.Cos(angleV) / stepV;
            double u0 = (x0 - origin.X) * nux + (y0 - origin.Y) * nuy;
            double v0 = (x0 - origin.X) * nvx + (y0 - origin.Y) * nvy;
            double uAcross = (x1 - x0) * nux + (y1 - y0) * nuy;
            double vAcross = (x1 - x0) * nvx + (y1 - y0) * nvy;
            double uDown = (x2 - x0) * nux + (y2 - y0) * nuy;
            double vDown = (x2 - x0) * nvx + (y2 - y0) * nvy;
            double halfU = 0.5 / (view.PixelsPerUnit * stepU);
            double halfV = 0.5 / (view.PixelsPerUnit * stepV);

            for (int iy = 0; iy < height; iy++)
            {
                double u = u0 + iy * uDown;
                double v = v0 + iy * vDown;

                for (int ix = 0; ix < width; ix++, u += uAcross, v += vAcross)
                {
                    int at = iy * stride + ix;

                    if (pixels[at] != background)
                    {
                        continue;
                    }

                    double ku = Math.Round(u);
                    double kv = Math.Round(v);
                    bool onU = Math.Abs(u - ku) <= halfU;
                    bool onV = Math.Abs(v - kv) <= halfV;

                    if (onU || onV)
                    {
                        bool strong =
                            (onU && Math.Abs(Math.IEEERemainder(ku, every)) < 0.5)
                            || (onV && Math.Abs(Math.IEEERemainder(kv, every)) < 0.5);
                        pixels[at] = strong ? major : minor;
                    }
                }
            }
        }

        /// <summary>
        /// The two axes of the snap, in radians from its X axis: across and up for the rectangular snap; for an isometric one
        /// those of its plane, 90 and 150 degrees on the left, 30 and 150 on the top, 30 and 90 on the right.
        /// </summary>
        private static void SnapAxes(CadIsoplane? isoplane, out double first, out double second)
        {
            switch (isoplane)
            {
                case CadIsoplane.Left:
                    first = Math.PI / 2.0;
                    second = 5.0 * Math.PI / 6.0;
                    break;
                case CadIsoplane.Top:
                    first = Math.PI / 6.0;
                    second = 5.0 * Math.PI / 6.0;
                    break;
                case CadIsoplane.Right:
                    first = Math.PI / 6.0;
                    second = Math.PI / 2.0;
                    break;
                default:
                    first = 0.0;
                    second = Math.PI / 2.0;
                    break;
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
            CadView view = WorkView;
            CadCommandSession session = Session;

            if (document == null || WorkSpace == null || view == null || session == null || session.IsRunning)
            {
                return false;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            IReadOnlyList<string> picked = document
                .GetPickIndex(WorkSpace)
                .Pick(x, y, PickboxPixels * scale / view.PixelsPerUnit);

            if (
                picked.Count == 0
                || !(document.FindEntity(picked[0]) is DxfBlockReference insert)
                || insert.AttributeCollection.Count == 0
            )
            {
                return false;
            }

            CancelGesture();
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    session.Start("EATTEDIT");
                    session.FeedSelection(new[] { insert.Handle });
                })
            );
            return true;
        }

        /// <summary>Whether an entity lies within the pickbox of a point of the canvas.</summary>
        /// <summary>The object nearest a point of the canvas within the pick box, or null.</summary>
        private string ObjectAt(Point p)
        {
            CadDocument document = Document;
            CadView view = WorkView;

            if (document == null || WorkSpace == null || view == null)
            {
                return null;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            IReadOnlyList<string> found = document
                .GetPickIndex(WorkSpace)
                .Pick(x, y, PickboxPixels * scale / view.PixelsPerUnit);
            return found.Count > 0 ? found[0] : null;
        }

        private bool IsObjectAt(Point p)
        {
            CadDocument document = Document;
            CadView view = WorkView;

            if (document == null || WorkSpace == null || view == null)
            {
                return false;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            return document.GetPickIndex(WorkSpace).Pick(x, y, PickboxPixels * scale / view.PixelsPerUnit).Count > 0;
        }

        /// <summary>
        /// The ghost copy of the objects a transforming command took, dashed in the selection colour, under the transformation
        /// the cursor gives; or the block INSERT drags, placed as the cursor would place it.
        /// </summary>
        private void DrawGhost(DrawingContext dc, CadView view, double scale, GeoPoint2 target)
        {
            CadBlockDrag drag = Session?.Prompt?.Drag;

            // A prompt with a preview of its own draws that in place of the ghost its message would give.
            if (drag == null && Session?.Prompt?.Preview != null)
            {
                return;
            }

            Affine2? moved = drag != null ? drag.Transform(target) : CadGhost.Transform(Session, target);

            if (!moved.HasValue || Document == null || WorkSpace == null)
            {
                return;
            }

            Affine2 m = moved.Value;
            double tolerance = 0.5 * scale / view.PixelsPerUnit;
            IReadOnlyList<double[]> paths;
            IReadOnlyList<double[]> rays;

            // An XLINE or a RAY is cut, once transformed, to the view grown by a few pixels, so that its ends lie off the canvas.
            DisplayBox grown = view.VisibleBox.Inflate(4.0 * scale / view.PixelsPerUnit);

            if (drag != null)
            {
                // Outlined in the block's own units, as SCALE's ghost is outlined before it is scaled.
                paths = _ghost.Paths(drag, tolerance);
                rays = _ghost.DragRays(m, grown);
            }
            else
            {
                paths = _ghost.Paths(Document, WorkSpace, tolerance);
                rays = _ghost.Rays(m, grown);
            }

            if (paths.Count == 0 && rays.Count == 0)
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

                foreach (double[] part in rays)
                {
                    context.BeginFigure(ToScreen(view, new GeoPoint2(part[0], part[1]), scale), false, false);
                    context.LineTo(ToScreen(view, new GeoPoint2(part[2], part[3]), scale), true, false);
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(null, GhostPen, geometry);
        }

        /// <summary>
        /// What the prompt waiting previews with the cursor at a point (<see cref="CadPrompt.Preview"/>), as AutoCAD 2021 drags
        /// the object about to be made: the entities the command would make, each in its own colour as it will be drawn, and
        /// the copies of the objects it would move, dashed as the ghost of MOVE. False where the prompt has no preview or one
        /// drawn without the rubber band.
        /// </summary>
        private bool DrawPreview(DrawingContext dc, CadView view, double scale, GeoPoint2 target)
        {
            CadPreview preview = Session?.Prompt?.Preview;

            if (preview == null || Document == null || WorkSpace == null)
            {
                return true;
            }

            // TRIM and EXTEND preview the other's result while Shift is held, as a pick with it gives.
            preview.Shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            double tolerance = 0.5 * scale / view.PixelsPerUnit;
            DisplayBox grown = view.VisibleBox.Inflate(4.0 * scale / view.PixelsPerUnit);
            IReadOnlyList<Affine2> moves = preview.Moves(target);

            if (moves.Count > 0 && preview.Handles.Count > 0)
            {
                IReadOnlyList<double[]> paths = _ghost.Paths(Document, WorkSpace, preview.Handles, tolerance);
                var geometry = new StreamGeometry();

                using (StreamGeometryContext context = geometry.Open())
                {
                    foreach (Affine2 m in moves)
                    {
                        foreach (double[] xy in paths)
                        {
                            Trace(context, view, scale, xy, m, false);
                        }

                        foreach (double[] part in _ghost.MovedRays(m, grown))
                        {
                            Trace(context, view, scale, part, Affine2.Identity, false);
                        }
                    }
                }

                geometry.Freeze();
                dc.DrawGeometry(null, GhostPen, geometry);
            }

            IReadOnlyList<DisplayItem2> items = preview.Items(target);

            if (items.Count > 0)
            {
                int background = Options.BackgroundOf(WorkSpace);

                // One figure of lines and one of fills for each colour, drawn in the order the colours first come.
                var colours = new List<int>();
                var lines = new Dictionary<int, StreamGeometry>();
                var fills = new Dictionary<int, StreamGeometry>();
                var contexts = new Dictionary<int, StreamGeometryContext>();
                var fillContexts = new Dictionary<int, StreamGeometryContext>();

                foreach (DisplayItem2 item in items)
                {
                    int rgb = PreviewColor(item.Style?.Rgb ?? 0xFFFFFF, background);

                    if (!lines.ContainsKey(rgb))
                    {
                        colours.Add(rgb);
                        lines[rgb] = new StreamGeometry();
                        contexts[rgb] = lines[rgb].Open();
                        fills[rgb] = new StreamGeometry { FillRule = FillRule.EvenOdd };
                        fillContexts[rgb] = fills[rgb].Open();
                    }

                    StreamGeometryContext context = contexts[rgb];

                    switch (item)
                    {
                        case DisplayPolyline2 chain:
                            var xy = new double[chain.Xy.Count + (chain.Closed ? 2 : 0)];

                            for (int i = 0; i < chain.Xy.Count; i++)
                            {
                                xy[i] = chain.Xy[i];
                            }

                            if (chain.Closed && chain.Xy.Count >= 2)
                            {
                                xy[xy.Length - 2] = chain.Xy[0];
                                xy[xy.Length - 1] = chain.Xy[1];
                            }

                            Trace(context, view, scale, xy, Affine2.Identity, false);
                            break;
                        case DisplaySegments2 segments:
                            for (int i = 0; i + 3 < segments.Xy.Count; i += 4)
                            {
                                Trace(
                                    context,
                                    view,
                                    scale,
                                    new[]
                                    {
                                        segments.Xy[i],
                                        segments.Xy[i + 1],
                                        segments.Xy[i + 2],
                                        segments.Xy[i + 3],
                                    },
                                    Affine2.Identity,
                                    false
                                );
                            }

                            break;
                        case DisplayArc2 arc:
                            Trace(context, view, scale, arc.Flatten(tolerance), Affine2.Identity, false);
                            break;
                        case DisplayRay2 ray:
                            if (ray.TryClip(grown, out double x0, out double y0, out double x1, out double y1))
                            {
                                Trace(context, view, scale, new[] { x0, y0, x1, y1 }, Affine2.Identity, false);
                            }

                            break;
                        case DisplayFill2 fill:
                            foreach (double[] ring in fill.Rings)
                            {
                                Trace(fillContexts[rgb], view, scale, ring, Affine2.Identity, true);
                            }

                            break;
                        case DisplayText2 text when text.Strokes != null:
                            foreach (double[] stroke in text.Strokes)
                            {
                                Trace(context, view, scale, stroke, Affine2.Identity, false);
                            }

                            break;
                        default:
                            // A text in a TrueType font, an image or a viewport: its box, as the ghost of MOVE draws a text.
                            DisplayBox box = item.Bounds;

                            if (!box.IsEmpty)
                            {
                                Trace(
                                    context,
                                    view,
                                    scale,
                                    new[]
                                    {
                                        box.MinX,
                                        box.MinY,
                                        box.MaxX,
                                        box.MinY,
                                        box.MaxX,
                                        box.MaxY,
                                        box.MinX,
                                        box.MaxY,
                                        box.MinX,
                                        box.MinY,
                                    },
                                    Affine2.Identity,
                                    false
                                );
                            }

                            break;
                    }
                }

                foreach (int rgb in colours)
                {
                    contexts[rgb].Close();
                    fillContexts[rgb].Close();
                    lines[rgb].Freeze();
                    fills[rgb].Freeze();
                    int shade = rgb;

                    if (preview.Fades)
                    {
                        // A piece about to be cut away: the object drawn there blanked out, and the piece drawn a third of its colour.
                        var blank = new SolidColorBrush(
                            Color.FromRgb((byte)(background >> 16), (byte)(background >> 8), (byte)background)
                        );
                        blank.Freeze();
                        var cover = new Pen(blank, 3.0);
                        cover.Freeze();
                        dc.DrawGeometry(null, cover, lines[rgb]);
                        shade = Blend(rgb, background, 1.0 / 3.0);
                    }

                    var brush = new SolidColorBrush(
                        Color.FromRgb((byte)(shade >> 16), (byte)(shade >> 8), (byte)shade)
                    );
                    brush.Freeze();
                    dc.DrawGeometry(brush, null, fills[rgb]);
                    var pen = new Pen(brush, 1.0);
                    pen.Freeze();
                    dc.DrawGeometry(null, pen, lines[rgb]);
                }
            }

            return preview.ShowsBand;
        }

        /// <summary>A run of X and Y in drawing units, under a transformation, added to a figure as one line, or one ring where it is filled.</summary>
        private void Trace(
            StreamGeometryContext context,
            CadView view,
            double scale,
            double[] xy,
            Affine2 m,
            bool filled
        )
        {
            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                Point p = ToScreen(view, new GeoPoint2(m.X(xy[i], xy[i + 1]), m.Y(xy[i], xy[i + 1])), scale);

                if (i == 0)
                {
                    context.BeginFigure(p, filled, filled);
                }
                else
                {
                    context.LineTo(p, !filled, false);
                }
            }
        }

        /// <summary>The colour a preview draws an item in: its own, white turned black on a light background and black white on a dark one, as the drawing shows colour 7.</summary>
        private static int PreviewColor(int rgb, int background)
        {
            double Luminance(int c) =>
                (0.2126 * ((c >> 16) & 0xFF) + 0.7152 * ((c >> 8) & 0xFF) + 0.0722 * (c & 0xFF)) / 255.0;

            if (Luminance(background) > 0.6 && Luminance(rgb) >= 0.98)
            {
                return 0x000000;
            }

            return Luminance(background) < 0.4 && Luminance(rgb) <= 0.02 ? 0xFFFFFF : rgb;
        }

        /// <summary>A colour mixed into the background, its own share of it given: a third of white on black is a dark grey.</summary>
        private static int Blend(int rgb, int background, double share)
        {
            int Channel(int shift) =>
                (int)Math.Round(share * ((rgb >> shift) & 0xFF) + (1.0 - share) * ((background >> shift) & 0xFF));
            return (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
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
            CadView view = WorkView;
            CadDocument document = Document;

            if (view == null || document == null || WorkSpace == null)
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

            // At a point prompt the snap is found first, so that what follows the cursor, the fields of dynamic input and a
            // distance typed along the band, measures to the point the rubber band ends at: snapped, ortho or polar.
            bool pointing =
                CadPicking.WantsPoint(session)
                && !session.Prompt.Tracks
                && !_panFrom.HasValue
                && !_boxFrom.HasValue
                && (Through == null || InsideFrame(Through, p));

            if (pointing)
            {
                _snapped = CadPicking.Snap(
                    document,
                    WorkSpace,
                    Snap,
                    cursor,
                    aperture,
                    session.Prompt.BasePoint,
                    session.Prompt.SnapFrom
                );
            }

            GeoPoint2 band = pointing ? BandEnd(cursor) : cursor;

            if (session != null)
            {
                session.Cursor = band;
                session.PickAperture = pick;
            }

            CursorMoved?.Invoke(this, new CadCursorEventArgs(x, y, band.X, band.Y));

            // A prompt that follows the cursor, as REVCLOUD's Freehand, takes each point the cursor moves to.
            if (session != null && session.Prompt != null && session.Prompt.Tracks && !_panFrom.HasValue)
            {
                session.Feed(cursor);
            }

            if (_panFrom.HasValue)
            {
                Point from = _panFrom.Value;

                // Through a viewport its view pans, the paper staying; through one whose display is locked, the paper pans.
                if (MovesThrough)
                {
                    view.Pan((p.X - from.X) * scale, (p.Y - from.Y) * scale);
                    _throughNow = view;
                    _throughName = "PAN";
                }
                else
                {
                    View.Pan((p.X - from.X) * scale, (p.Y - from.Y) * scale);
                }

                _panFrom = p;
                ViewMoved();
                return;
            }

            // Through a viewport the cursor works inside its frame alone, as AutoCAD's crosshair shows only there.
            if (Through != null && !_boxFrom.HasValue && !InsideFrame(Through, p))
            {
                _cursor = null;
                _snapped = null;
                SetHover(null);
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
                if (!pointing)
                {
                    _snapped = CadPicking.Snap(
                        document,
                        WorkSpace,
                        Snap,
                        cursor,
                        aperture,
                        session.Prompt.BasePoint,
                        session.Prompt.SnapFrom
                    );
                }

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
                _gripTip = hot >= 0 ? GripTip(document, _grips[hot]) : null;
                InvalidateVisual();
            }

            IReadOnlyList<string> found = document.GetPickIndex(WorkSpace).Pick(x, y, pick);
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

            if (
                snap == null
                || !(
                    snap.Tracking
                    || (
                        (snap.Override ?? (snap.IsOn ? snap.Modes : CadSnapModes.None))
                        & (CadSnapModes.Extension | CadSnapModes.Parallel)
                    ) != 0
                )
            )
            {
                return;
            }

            if (_rest == null)
            {
                _rest = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(RestMilliseconds),
                };
                _rest.Tick += (s, e) => AcquireAtRest();
            }

            _restAt = cursor;
            _restAperture = aperture;
            _rest.Start();
        }

        /// <summary>
        /// The cursor has rested for <see cref="RestMilliseconds"/> at a point prompt: the point under it is acquired, or let go,
        /// as AutoCAD acquires one when the cursor pauses on it, and the snap is worked out again from it; only with Shift held
        /// where the snap acquires so (<see cref="ObjectSnap.AcquiresWithShift"/>, POLARMODE 8).
        /// </summary>
        /// <returns>Whether a point was acquired or let go.</returns>
        internal bool AcquireAtRest()
        {
            _rest?.Stop();
            CadCommandSession session = Session;
            CadDocument document = Document;

            if (!_restAt.HasValue || document == null || WorkSpace == null || !CadPicking.WantsPoint(session))
            {
                return false;
            }

            if (Snap != null && Snap.AcquiresWithShift && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                return false;
            }

            GeoPoint2 at = _restAt.Value;
            _restAt = null;

            if (!CadPicking.Acquire(document, WorkSpace, Snap, at, _restAperture, session.Prompt.BasePoint))
            {
                return false;
            }

            _snapped = CadPicking.Snap(
                document,
                WorkSpace,
                Snap,
                at,
                _restAperture,
                session.Prompt.BasePoint,
                session.Prompt.SnapFrom
            );
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
            CadView view = WorkView;

            if (document == null || WorkSpace == null || view == null)
            {
                return;
            }

            double scale = PixelScale;
            view.ScreenToDrawing(p.X * scale, p.Y * scale, out double x, out double y);
            double aperture =
                (CadPicking.WantsPoint(Session) ? AperturePixels : PickboxPixels) * scale / view.PixelsPerUnit;
            var at = new GeoPoint2(x, y);

            // A click inside a cell of a table, away from its lines, selects the cell, as AutoCAD's.
            if ((Session == null || !Session.IsRunning) && !_cycler.IsArmed && CellClicked != null)
            {
                if (InsideCell(p) && CellClicked(at, shift))
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    InvalidateVisual();
                    return;
                }

                // A click anywhere else lets the cells go, as AutoCAD's does.
                CellsLetGo?.Invoke();
            }

            // Shift+Space armed: each click at one place takes the next object there, as AutoCAD's cycling.
            if (_cycler.IsArmed && (Session == null || !Session.IsRunning))
            {
                _cycler.Next(document, WorkSpace, at, aperture);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
                return;
            }

            // SELECTIONCYCLING 2: where several objects lie under the pick, the list of them to choose from.
            IReadOnlyList<string> overlapping = CadPicking.Overlapping(document, WorkSpace, Session, at, aperture);

            if (overlapping.Count > 1)
            {
                ShowCycleList(document, overlapping, shift);
                return;
            }

            // A field of dynamic input that holds a number gives the point, the click its direction or its distance.
            bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            GeoPoint2? fields =
                HeldAt != null && CadPicking.WantsPoint(Session)
                    ? HeldAt(
                        CadPicking
                            .Snap(
                                document,
                                WorkSpace,
                                Snap,
                                at,
                                aperture,
                                Session.Prompt.BasePoint,
                                Session.Prompt.SnapFrom
                            )
                            ?.Point
                            ?? at,
                        true
                    )
                    : null;

            if (
                fields.HasValue
                    ? Session.Feed(fields.Value, false, control)
                    : CadPicking.Click(document, WorkSpace, Session, Snap, at, aperture, shift, control)
            )
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
            CadView view = WorkView;

            if (document == null || WorkSpace == null || view == null)
            {
                return;
            }

            double scale = PixelScale;
            double x0 = corner.X;
            double y0 = corner.Y;
            view.ScreenToDrawing(b.X * scale, b.Y * scale, out double x1, out double y1);

            // Through a twisted viewport the box on the screen is a turned one in model space, taken as a polygon of its four
            // corners: counter-clockwise for a crossing, clockwise for a window, as a lasso is.
            if (view.Twist != 0.0)
            {
                view.ScreenToDrawing(b.X * scale, a.Y * scale, out double x2, out double y2);
                view.ScreenToDrawing(a.X * scale, b.Y * scale, out double x3, out double y3);
                var ring = new List<GeoPoint2>
                {
                    new GeoPoint2(x0, y0),
                    new GeoPoint2(x2, y2),
                    new GeoPoint2(x1, y1),
                    new GeoPoint2(x3, y3),
                };

                if (CadPicking.SignedArea(ring) > 0.0 != b.X < a.X)
                {
                    ring.Reverse();
                }

                CadPicking.LassoDone(document, WorkSpace, Session, ring, remove);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            var box = new DisplayBox(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
            CadPicking.BoxDone(document, WorkSpace, Session, box, b.X < a.X, remove);
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

            // Through a viewport, its view is fitted to what model space draws, one undo step.
            if (MovesThrough)
            {
                DxfViewport through = Through;
                DisplayBox extents = document.GetDisplay(document.ModelSpace).Bounds;

                if (!extents.IsEmpty)
                {
                    _throughNow = null;
                    CadViewports.Fit(through, extents, out GeoPoint2 center, out double height);
                    Report(document.SetViewportView(through.Handle, center, height, "ZOOM"));
                }

                RequestRender();
                return;
            }

            view.ZoomTo(CadPicking.Extents(document, Space));
            ViewChanged?.Invoke(this, EventArgs.Empty);
            RequestRender();
        }

        /// <summary>The viewport of the layout shown that is worked through (MSPACE); null on the paper and in model space.</summary>
        private DxfViewport Through
        {
            get
            {
                CadDocument document = Document;
                return
                    document != null && Space != null && !Space.IsModel && ReferenceEquals(Space, document.CurrentSpace)
                    ? document.ActiveViewport
                    : null;
            }
        }

        /// <summary>Whether the wheel and the middle button change the view of the viewport worked through: one is, its display not locked.</summary>
        private bool MovesThrough => Through is DxfViewport through && !through.Locked;

        /// <summary>The space picks, snaps and grips work in: model space through a viewport, else the space shown.</summary>
        private CadSpace WorkSpace => Through != null ? Document.ModelSpace : Space;

        /// <summary>The view picks, snaps and grips work in: through a viewport, the paper's view in model units; else <see cref="View"/>.</summary>
        private CadView WorkView
        {
            get
            {
                CadView view = View;
                DxfViewport through = Through;

                if (view == null || through == null)
                {
                    return view;
                }

                return _throughNow ?? CadViewports.ModelView(view, through);
            }
        }

        /// <summary>Gets the handle of the viewport worked through, as the canvas shows it; null on the paper. A test reads it.</summary>
        internal string ThroughHandle => Through?.Handle;

        /// <summary>A viewport's frame on the canvas, in device-independent pixels.</summary>
        private Rect FrameOnScreen(DxfViewport viewport)
        {
            double scale = PixelScale;
            GeoPoint3 c = viewport.CenterPoint;
            Point a = ToScreen(View, new GeoPoint2(c.X - viewport.Width / 2.0, c.Y - viewport.Height / 2.0), scale);
            Point b = ToScreen(View, new GeoPoint2(c.X + viewport.Width / 2.0, c.Y + viewport.Height / 2.0), scale);
            return new Rect(a, b);
        }

        private bool InsideFrame(DxfViewport viewport, Point p) => View != null && FrameOnScreen(viewport).Contains(p);

        /// <summary>The viewport of the layout shown whose frame holds a point of the canvas, the one drawn last where frames overlap; null for none.</summary>
        private DxfViewport FrameAt(Point p)
        {
            IReadOnlyList<DxfViewport> all = Document.EnterableViewports;

            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (InsideFrame(all[i], p))
                {
                    return all[i];
                }
            }

            return null;
        }

        /// <summary>
        /// A click of the left button in a layout, as AutoCAD takes it about viewports: on the paper a double click inside a
        /// viewport's frame, on no object of the paper, works through it (MSPACE); through a viewport a click inside another's
        /// frame works through that one, a double click off every frame goes back to the paper (PSPACE), and any other click
        /// off the frame worked through is taken by nothing. False where the click is left to the rest of the canvas.
        /// </summary>
        internal bool ViewportClick(Point p, bool twice)
        {
            CadDocument document = Document;

            if (
                document == null
                || Space == null
                || Space.IsModel
                || View == null
                || !ReferenceEquals(Space, document.CurrentSpace)
            )
            {
                return false;
            }

            DxfViewport through = Through;

            if (through != null)
            {
                if (InsideFrame(through, p))
                {
                    return false;
                }

                if (IsRunning)
                {
                    return true;
                }

                DxfViewport other = FrameAt(p);

                if (other != null)
                {
                    document.EnterViewport(other.Handle);
                }
                else if (twice)
                {
                    document.LeaveViewport();
                }

                return true;
            }

            if (!twice || IsRunning)
            {
                return false;
            }

            DxfViewport at = FrameAt(p);

            if (at == null || (ObjectAt(p) is string handle && !(document.FindEntity(handle) is DxfViewport)))
            {
                return false;
            }

            return document.EnterViewport(at.Handle);
        }

        /// <summary>Model space entered through a viewport or left: what follows the cursor and the selection drawn anew.</summary>
        private void OnActiveViewportChanged(object sender, EventArgs e)
        {
            _throughNow = null;
            Options.Hover = null;
            _snapped = null;
            CancelGesture();
            Cursor = IdleCursor;
            _markedDirty = true;
            RequestRender();
        }

        /// <summary>
        /// Writes into the viewport worked through the view the wheel or the middle button gave it, named ZOOM or PAN, one undo
        /// step with the real-time zooms and pans made straight before it; a refusal written on the command line.
        /// </summary>
        private void CommitThrough()
        {
            CadView model = _throughNow;
            _throughNow = null;
            DxfViewport through = Through;
            CadView paper = View;

            if (model == null || through == null || paper == null)
            {
                return;
            }

            CadViewports.ViewOf(paper, model, through, out GeoPoint2 center, out double height);
            Report(Document.SetViewportView(through.Handle, center, height, _throughName ?? "ZOOM", true));
        }

        private void Report(CadEditResult result)
        {
            if (result != null && !result.Succeeded && Session != null)
            {
                Session.Write(result.Message);
                HistoryWritten?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// While the view of a viewport is zoomed or panned and not yet drawn again: the picture inside its frame moved and
        /// scaled with the view, as the whole picture follows a view of the paper.
        /// </summary>
        private void DrawMovedThrough(DrawingContext dc, double scale)
        {
            CadView drawn = _throughDrawn;
            CadView now = _throughNow;
            DxfViewport through = Through;

            if (drawn == null || now == null || through == null)
            {
                return;
            }

            double s = now.PixelsPerUnit / drawn.PixelsPerUnit;
            double tx = now.Width / 2.0 + (drawn.CenterX - now.CenterX) * now.PixelsPerUnit - drawn.Width / 2.0 * s;
            double ty = now.Height / 2.0 - (drawn.CenterY - now.CenterY) * now.PixelsPerUnit - drawn.Height / 2.0 * s;

            // Through a twisted viewport the step between the two centres is the canvas's, turned with the view.
            if (now.Twist != 0.0)
            {
                now.DrawingToPixel(drawn.CenterX, drawn.CenterY, out double cx, out double cy);
                tx = cx - drawn.Width / 2.0 * s;
                ty = cy - drawn.Height / 2.0 * s;
            }
            var moved = new MatrixTransform(s, 0.0, 0.0, s, tx / scale, ty / scale);
            moved.Freeze();
            dc.PushClip(new RectangleGeometry(FrameOnScreen(through)));
            dc.PushTransform(moved);
            dc.DrawImage(_bitmap, new Rect(0, 0, _bitmap.PixelWidth / scale, _bitmap.PixelHeight / scale));
            dc.Pop();
            dc.Pop();
        }

        /// <summary>The frame of the viewport worked through drawn thick, as AutoCAD shows the current viewport, in the ink that shows on the paper.</summary>
        private void DrawActiveFrame(DrawingContext dc)
        {
            DxfViewport through = Through;

            if (through == null)
            {
                return;
            }

            int rgb = Options.Paper;
            double luminance =
                (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255.0;
            dc.DrawRectangle(null, luminance < 0.5 ? ActiveFrameLight : ActiveFrameDark, FrameOnScreen(through));
        }

        /// <summary>
        /// Draws entities highlighted over a picture: through a viewport those of model space, inside its frame alone. Given the
        /// picture's pixels, many are drawn by several threads (<see cref="SkiaCanvasRenderer.RenderHighlight"/>).
        /// </summary>
        private void Highlight(
            SKCanvas canvas,
            CadDocument document,
            IReadOnlyCollection<string> handles,
            bool hover,
            SKPixmap pixels = null
        )
        {
            DxfViewport through = Through;

            if (through == null)
            {
                _renderer.RenderHighlight(canvas, document, Space, View, Options, handles, hover, pixels);
                return;
            }

            Rect frame = FrameOnScreen(through);
            double scale = PixelScale;
            canvas.Save();
            canvas.ClipRect(
                new SKRect(
                    (float)(frame.Left * scale),
                    (float)(frame.Top * scale),
                    (float)(frame.Right * scale),
                    (float)(frame.Bottom * scale)
                )
            );
            _renderer.RenderHighlight(canvas, document, document.ModelSpace, WorkView, Options, handles, hover);
            canvas.Restore();
        }

        /// <summary>The grips of the selected entities while no command runs, and the tags of the selected viewports.</summary>
        private void FindGrips()
        {
            _grips.Clear();
            _tags.Clear();
            CadDocument document = Document;

            // A grip edit keeps the grips shown, its hot grips among them, as AutoCAD's.
            if (
                document == null
                || WorkSpace == null
                || (IsRunning && !Session.IsGripEditing)
                || document.Selection.Count == 0
            )
            {
                _hotGrip = -1;
                _gripTip = null;
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

            if (!WorkSpace.IsModel)
            {
                var selected = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);

                foreach (DisplayItem2 item in document.GetDisplay(WorkSpace).Items)
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

            // The tip reads the values anew, as an edit may have changed them.
            _gripTip = _hotGrip >= 0 ? GripTip(document, _grips[_hotGrip]) : null;
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
            CadView view = WorkView;

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
            CadView view = WorkView;
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
        /// The band an option of "Select objects:" draws while it asks its next point (<see cref="CadPrompt.Band"/>), to the
        /// snapped cursor: the box of Window, Crossing and BOX as a box dragged is drawn, solid blue for a window and dashed
        /// green for a crossing; the line of Fence dashed green, not closed; the polygon of WPolygon solid blue and of CPolygon
        /// dashed green, closed back to its first point.
        /// </summary>
        private void DrawBand(DrawingContext dc, CadView view, double scale)
        {
            CadSelectionBand band = Session?.Prompt?.Band;

            if (band == null || band.Points.Count == 0 || !_cursor.HasValue)
            {
                return;
            }

            GeoPoint2 cursor = _snapped?.Point ?? _cursor.Value;
            GeoPoint2 first = band.Points[0];
            bool crossing;

            switch (band.Kind)
            {
                case CadSelectionBandKind.Window:
                case CadSelectionBandKind.Crossing:
                case CadSelectionBandKind.Box:
                    crossing =
                        band.Kind == CadSelectionBandKind.Crossing
                        || (band.Kind == CadSelectionBandKind.Box && cursor.X < first.X);
                    dc.DrawRectangle(
                        crossing ? CrossingFill : WindowFill,
                        crossing ? CrossingPen : WindowPen,
                        new Rect(ToScreen(view, first, scale), ToScreen(view, cursor, scale))
                    );
                    return;
            }

            bool closed = band.Kind != CadSelectionBandKind.Fence;
            crossing = band.Kind != CadSelectionBandKind.WindowPolygon;
            var geometry = new StreamGeometry();

            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(ToScreen(view, first, scale), closed, closed);

                for (int i = 1; i < band.Points.Count; i++)
                {
                    context.LineTo(ToScreen(view, band.Points[i], scale), true, false);
                }

                context.LineTo(ToScreen(view, cursor, scale), true, false);
            }

            geometry.Freeze();
            dc.DrawGeometry(
                closed ? (crossing ? CrossingFill : WindowFill) : null,
                crossing ? CrossingPen : WindowPen,
                geometry
            );
        }

        /// <summary>
        /// The crosshair at the cursor, as AutoCAD draws it: two lines across a share of the canvas, in the colour that shows on
        /// its background, and the pickbox square at their meeting while no point is asked for.
        /// </summary>
        private void DrawCrosshair(DrawingContext dc)
        {
            if (
                _crosshairPercent <= 0.0
                || !_mouse.HasValue
                || _panFrom.HasValue
                || (Through is DxfViewport through && !InsideFrame(through, _mouse.Value))
            )
            {
                return;
            }

            int rgb = Space != null ? Options.BackgroundOf(Space) : Options.Background;
            double luminance =
                (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255.0;
            Pen pen = luminance < 0.5 ? CrossPenLight : CrossPenDark;
            Point at = _mouse.Value;
            double half = _crosshairPercent / 100.0 * Math.Max(ActualWidth, ActualHeight) / 2.0;
            double box = PickboxPixels;
            bool picking = !CadPicking.WantsPoint(Session);
            double gap = picking ? box : 0.0;

            // The lines run along the axes of the UCS, as AutoCAD's crosshair does; across and up in the world's. Turned by
            // SNAPANG they follow the snap, and with an isometric snap they run on the two axes of its plane.
            Vector along = CrosshairAxis();
            var up = new Vector(along.Y, -along.X);
            CadIsoplane? isoplane = Isoplane;
            Tolerance tolerance = Snap?.Tolerance ?? Tolerance.Default;

            if (isoplane.HasValue || Math.Abs(Math.IEEERemainder(SnapAngle, 2.0 * Math.PI)) > tolerance.EqualAngleRad)
            {
                double axis = Math.Atan2(-along.Y, along.X) + SnapAngle;
                SnapAxes(isoplane, out double first, out double second);
                along = new Vector(Math.Cos(axis + first), -Math.Sin(axis + first));
                up = new Vector(Math.Cos(axis + second), -Math.Sin(axis + second));
            }
            dc.DrawLine(pen, at - half * along, at - gap * along);
            dc.DrawLine(pen, at + gap * along, at + half * along);
            dc.DrawLine(pen, at + half * up, at + gap * up);
            dc.DrawLine(pen, at - gap * up, at - half * up);

            if (picking)
            {
                dc.DrawRectangle(null, pen, new Rect(at.X - box, at.Y - box, 2.0 * box, 2.0 * box));
            }
        }

        /// <summary>
        /// The way the X axis of the UCS runs on the canvas, of unit length: to the right for the world's, Y being down on the
        /// screen; through a twisted viewport turned with its view, as AutoCAD turns the crosshair and the icon there.
        /// </summary>
        private Vector CrosshairAxis()
        {
            CadDocument document = Document;
            CadUcs ucs = document == null || WorkSpace == null ? CadUcs.World : document.Ucs.WorkingOf(WorkSpace);
            double twist = Through?.TwistAngle ?? 0.0;

            if (twist == 0.0)
            {
                return ucs.IsWorld ? new Vector(1.0, 0.0) : new Vector(Math.Cos(ucs.Angle), -Math.Sin(ucs.Angle));
            }

            double angle = (ucs.IsWorld ? 0.0 : ucs.Angle) + twist;
            return new Vector(Math.Cos(angle), -Math.Sin(angle));
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
            CadView view = WorkView;

            if (document == null || WorkSpace == null || view == null || !document.Ucs.IconOn)
            {
                return false;
            }

            // The box the arms and their letters take about the corner.
            var yArm = new Vector(xArm.Y, -xArm.X);
            Vector[] reach =
            {
                new Vector(0.0, 0.0),
                xArm * (UcsIconArm + UcsIconLetter),
                yArm * (UcsIconArm + UcsIconLetter),
                (xArm + yArm) * UcsIconArm,
            };
            double minX = reach.Min(v => v.X) - UcsIconLetter / 2.0;
            double maxX = reach.Max(v => v.X) + UcsIconLetter / 2.0;
            double minY = reach.Min(v => v.Y) - UcsIconLetter / 2.0;
            double maxY = reach.Max(v => v.Y) + UcsIconLetter / 2.0;
            CadUcs ucs = document.Ucs.WorkingOf(WorkSpace);

            // In the Block Editor the icon stands at the block's base point, as AutoCAD's.
            CadBlockEditor editor = WorkSpace.IsBlockEditor ? document.BlockEditor : null;

            if (document.Ucs.IconAtOrigin || editor != null)
            {
                GeoPoint2 at =
                    editor != null
                        ? new GeoPoint2(editor.BasePoint.X, editor.BasePoint.Y)
                        : new GeoPoint2(ucs.Origin.X, ucs.Origin.Y);
                Point origin = ToScreen(view, at, PixelScale);

                if (
                    origin.X + minX >= UcsIconMargin
                    && origin.X + maxX <= ActualWidth - UcsIconMargin
                    && origin.Y + minY >= UcsIconMargin
                    && origin.Y + maxY <= ActualHeight - UcsIconMargin
                )
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

            int rgb = Options.BackgroundOf(Space);
            double luminance =
                (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255.0;
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
                var letter = new FormattedText(
                    arm == xArm ? "X" : "Y",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    TagFace,
                    11.0,
                    ink,
                    dip
                );
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

            if (Document.Ucs.WorkingOf(WorkSpace).IsWorld)
            {
                var w = new FormattedText(
                    "W",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    TagFace,
                    10.0,
                    ink,
                    dip
                );
                Point at = corner + yArm * 20.0 + xArm * 10.0;
                dc.DrawText(w, new Point(at.X - w.Width / 2.0, at.Y - w.Height / 2.0));
            }
        }

        /// <summary>
        /// The cells of a table selected, as AutoCAD 2021 draws them: the range in the accent, filled lightly, a grip at the middle
        /// of each of its sides, and the table's column letters above it and row numbers left of it, those of the range in amber.
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
            double half = GripSize / 2.0;

            for (int k = 0; k < 4; k++)
            {
                Point a = range[k],
                    b = range[(k + 1) % 4];
                dc.DrawRectangle(
                    CellBrush,
                    null,
                    new Rect((a.X + b.X) / 2.0 - half, (a.Y + b.Y) / 2.0 - half, GripSize, GripSize)
                );
            }

            // The bands of column letters over the table and row numbers left of it, the range's part in amber.
            Vector across = table[1] - table[0];
            Vector down = table[3] - table[0];

            if (across.Length < 1.0 || down.Length < 1.0)
            {
                return;
            }

            Vector up = down / down.Length * -16.0;
            Vector left = across / across.Length * -22.0;
            dc.DrawGeometry(BandFill, null, Polygon(new[] { table[0] + up, table[1] + up, table[1], table[0] }));
            dc.DrawGeometry(BandFill, null, Polygon(new[] { table[0] + left, table[0], table[3], table[3] + left }));
            // A corner of the range carried onto the table's top (or left) edge.
            Point Along(Point p, Vector axis) => table[0] + axis * ((p - table[0]) * axis / axis.LengthSquared);
            Point c0 = Along(range[0], across),
                c1 = Along(range[1], across);
            Point w0 = Along(range[0], down),
                w3 = Along(range[3], down);
            dc.DrawGeometry(BandHot, null, Polygon(new[] { c0 + up, c1 + up, c1, c0 }));
            dc.DrawGeometry(BandHot, null, Polygon(new[] { w0 + left, w0, w3, w3 + left }));
        }

        /// <summary>Whether a point of the canvas falls in one cell of a table of the space shown, the pick box about it in that cell too, away from its lines.</summary>
        private bool InsideCell(Point p)
        {
            CadView view = WorkView;

            if (Document == null || WorkSpace == null || view == null)
            {
                return false;
            }

            GeoPoint2 at = DrawingAt(p);
            double aperture = PickboxPixels * PixelScale / view.PixelsPerUnit;

            if (!CadTableEditor.TryCellAt(WorkSpace, at, out string handle, out int row, out int column))
            {
                return false;
            }

            foreach (
                GeoPoint2 near in new[]
                {
                    new GeoPoint2(at.X - aperture, at.Y),
                    new GeoPoint2(at.X + aperture, at.Y),
                    new GeoPoint2(at.X, at.Y - aperture),
                    new GeoPoint2(at.X, at.Y + aperture),
                }
            )
            {
                if (
                    !CadTableEditor.TryCellAt(WorkSpace, near, out string other, out int r, out int c)
                    || other != handle
                    || r != row
                    || c != column
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static StreamGeometry Polygon(Point[] points)
        {
            var geometry = new StreamGeometry();

            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(points[0], true, true);
                context.PolyLineTo(points.Skip(1).ToList(), true, false);
            }

            geometry.Freeze();
            return geometry;
        }

        private void DrawGrips(DrawingContext dc, CadView view, double scale)
        {
            double half = GripSize / 2.0;

            for (int i = 0; i < _grips.Count; i++)
            {
                Point at = ToScreen(view, _grips[i].Grip.Point, scale);
                bool hot =
                    i == _hotGrip
                    || _hot.Contains(new KeyValuePair<string, int>(_grips[i].Handle, _grips[i].Grip.Index));

                if (_grips[i].Grip.Custom != null)
                {
                    DrawCustomGrip(dc, view, scale, _grips[i].Grip, at, hot ? HotGripBrush : CustomGripBrush);
                    continue;
                }

                dc.DrawRectangle(
                    hot ? HotGripBrush : GripBrush,
                    null,
                    new Rect(at.X - half, at.Y - half, GripSize, GripSize)
                );
            }

            // The tip of a custom grip hovered, beside it, as the design shows: the values it drives and the steps they snap to.
            if (_gripTip != null && _hotGrip >= 0 && _hotGrip < _grips.Count)
            {
                Point at = ToScreen(view, _grips[_hotGrip].Grip.Point, scale);
                double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                var text = new FormattedText(
                    _gripTip,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    GripTipFace,
                    11.0,
                    GripTipInk,
                    dip
                );
                var box = new Rect(at.X + 14.0, at.Y + 10.0, text.Width + 16.0, text.Height + 6.0);

                // Kept on the canvas: to the left of the grip where it would run off the right.
                if (box.Right > ActualWidth && at.X - 14.0 - box.Width >= 0.0)
                {
                    box.X = at.X - 14.0 - box.Width;
                }

                dc.DrawRoundedRectangle(GripTipFill, GripTipEdge, box, 2.0, 2.0);
                dc.DrawText(text, new Point(box.X + 8.0, box.Y + 3.0));
            }
        }

        /// <summary>
        /// The tip of a custom grip: each property it drives and its value, a distance to 2 places and an angle in degrees, and
        /// the values of the first's list it snaps to, as "Door width = 900.00 · snaps 800 / 900 / 1000"; null for a grip of
        /// no dynamic block, or one whose properties cannot be read.
        /// </summary>
        private static string GripTip(CadDocument document, GripAt grip)
        {
            if (grip.Grip.Custom == null || !(document?.FindEntity(grip.Handle) is DxfBlockReference insert))
            {
                return null;
            }

            try
            {
                IReadOnlyList<DxfDynamicBlockReferenceProperty> properties =
                    insert.DynamicBlockReferencePropertyCollection;
                var parts = new List<string>();
                string snaps = null;

                foreach (string name in grip.Grip.Custom.PropertyNames)
                {
                    DxfDynamicBlockReferenceProperty property = properties.FirstOrDefault(p => p.PropertyName == name);

                    if (property == null)
                    {
                        continue;
                    }

                    parts.Add(name + " = " + TipText(property, property.Value));
                    object[] allowed = property.GetAllowedValues();

                    if (snaps == null && allowed.Length > 0 && allowed[0] is double)
                    {
                        snaps =
                            " · snaps "
                            + string.Join(
                                " / ",
                                allowed.Select(v => ((double)v).ToString("0.##", CultureInfo.InvariantCulture))
                            );
                    }
                }

                return parts.Count == 0 ? null : string.Join(", ", parts) + snaps;
            }
            catch (Exception exception)
                when (exception is NotSupportedException
                    || exception is InvalidOperationException
                    || exception is FormatException
                )
            {
                return null;
            }
        }

        /// <summary>A value of a property as the tip writes it: a distance to 2 places, an angle in degrees, a flip's state by its name.</summary>
        private static string TipText(DxfDynamicBlockReferenceProperty property, object value)
        {
            switch (value)
            {
                case double angle when property.UnitsType == DxfDynamicBlockReferencePropertyUnitsType.Angular:
                    return (angle * 180.0 / Math.PI).ToString("0.##", CultureInfo.InvariantCulture) + "°";
                case double distance:
                    return distance.ToString("0.00", CultureInfo.InvariantCulture);
                case short state when property.StateNames.Count == 2 && (state == 0 || state == 1):
                    return property.StateNames[state];
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// A custom grip of a dynamic block, of AutoCAD's shapes: a square for a point, a polar or an XY parameter, an arrow
        /// along a linear parameter, a circle for a rotation, a bent arrow across a flip's line, a triangle pointing down for a
        /// visibility or a lookup parameter; each a little larger than a plain grip, as the design draws them on a grid of 12.
        /// </summary>
        private void DrawCustomGrip(DrawingContext dc, CadView view, double scale, CadGrip grip, Point at, Brush fill)
        {
            double k = GripSize / 12.0 * 1.25;

            // The way the arrow points on the screen: the grip's direction carried by the view, the screen's y down.
            Point ahead = ToScreen(
                view,
                new GeoPoint2(grip.Point.X + grip.Direction.X, grip.Point.Y + grip.Direction.Y),
                scale
            );
            double angle = Math.Atan2(ahead.Y - at.Y, ahead.X - at.X);

            switch (grip.Kind)
            {
                case CadGripKind.CustomLinear:
                    dc.DrawGeometry(fill, CustomGripEdge, Polygon(Shape(at, angle, k, -6, -7, 8, 0, -6, 7)));
                    break;
                case CadGripKind.CustomFlip:
                    dc.DrawGeometry(
                        fill,
                        CustomGripEdge,
                        Polygon(Shape(at, angle, k, -9, -3, 0, -3, 0, -8, 9, 0, 0, 8, 0, 3, -9, 3))
                    );
                    break;
                case CadGripKind.CustomList:
                    dc.DrawGeometry(fill, CustomGripEdge, Polygon(Shape(at, 0.0, k, -8, -5, 8, -5, 0, 6)));
                    break;
                case CadGripKind.CustomRotation:
                    dc.DrawEllipse(fill, CustomGripEdge, at, 6.0 * k, 6.0 * k);
                    break;
                default:
                    dc.DrawRectangle(
                        fill,
                        CustomGripEdge,
                        new Rect(at.X - 6.0 * k, at.Y - 6.0 * k, 12.0 * k, 12.0 * k)
                    );
                    break;
            }
        }

        /// <summary>The corners of a shape given as x, y pairs on the design's grid, scaled, turned by an angle on the screen and put at a point.</summary>
        private static Point[] Shape(Point at, double angle, double k, params double[] xy)
        {
            double cos = Math.Cos(angle),
                sin = Math.Sin(angle);
            var points = new Point[xy.Length / 2];

            for (int i = 0; i < points.Length; i++)
            {
                double x = xy[2 * i] * k,
                    y = xy[2 * i + 1] * k;
                points[i] = new Point(at.X + x * cos - y * sin, at.Y + x * sin + y * cos);
            }

            return points;
        }

        private void DrawTags(DrawingContext dc, CadView view, double scale)
        {
            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (ViewportTag tag in _tags)
            {
                Point corner = ToScreen(view, new GeoPoint2(tag.Frame.MinX, tag.Frame.MaxY), scale);
                var text = new FormattedText(
                    tag.Text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    TagFace,
                    9.0,
                    TagInk,
                    dip
                );
                var box = new Rect(corner.X, corner.Y - 20.0, text.Width + 12.0, 16.0);
                dc.DrawRoundedRectangle(GripBrush, null, box, 2.0, 2.0);
                dc.DrawText(text, new Point(box.X + 6.0, box.Y + (16.0 - text.Height) / 2.0));
            }
        }

        /// <summary>
        /// Where the rubber band ends with the cursor at a point: at the snap found, an object snap, ortho or polar; at the cursor
        /// with no snap, or without the magnet (AUTOSNAP 4) while an object snap is shown.
        /// </summary>
        private GeoPoint2 BandEnd(GeoPoint2 cursor) =>
            _snapped != null && (Snap == null || Snap.Magnet || !IsObjectSnap(_snapped.Kind)) ? _snapped.Point : cursor;

        /// <summary>While a point prompt waits: the base point, the rubber band to the snapped cursor, the snap's marker and its tip.</summary>
        private void DrawPrompt(DrawingContext dc, CadView view, double scale)
        {
            CadCommandSession session = Session;

            // A prompt for a line of text previews the words typed so far, wherever the cursor is.
            if (session?.Prompt?.Kind == CadPromptKind.String && session.Prompt.Preview != null)
            {
                session.Prompt.Preview.Typed = Typed;
                DrawPreview(dc, view, scale, _cursor ?? default(GeoPoint2));
                return;
            }

            // A prompt for objects previews what the object under the cursor would give, as FILLET its arc; a prompt for a
            // keyword what Enter would, as -ARRAY's copies while it asks whether to rotate them.
            if (
                (CadPicking.WantsObjects(session) || session?.Prompt?.Kind == CadPromptKind.Keyword) && _cursor.HasValue
            )
            {
                DrawPreview(dc, view, scale, _cursor.Value);
                return;
            }

            if (!CadPicking.WantsPoint(session) || !_cursor.HasValue)
            {
                return;
            }

            GeoPoint2 target = BandEnd(_cursor.Value);

            // A field of dynamic input that holds a number holds the rubber band and the preview with it.
            target = HeldAt?.Invoke(target, false) ?? target;
            Point to = ToScreen(view, target, scale);
            GeoPoint2? basePoint = session.Prompt.BasePoint;
            DrawGhost(dc, view, scale, target);
            bool band = DrawPreview(dc, view, scale, target);

            if (basePoint.HasValue)
            {
                Point from = ToScreen(view, basePoint.Value, scale);

                // A preview drawn alone, as RECTANG's rectangle, has no rubber band beside it.
                if (band)
                {
                    dc.DrawLine(RubberPen, from, to);
                }

                dc.DrawRectangle(null, BasePen, new Rect(from.X - 5.0, from.Y - 5.0, 10.0, 10.0));
            }

            DrawTracking(dc, view, scale);

            if (_snapped != null && (Snap == null || Snap.ShowsMarker))
            {
                DrawMarker(dc, _snapped, ToScreen(view, _snapped.Point, scale));
            }

            string tip = PromptTip;

            if (tip.Length > 0)
            {
                var text = new FormattedText(
                    tip,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    TipFace,
                    10.0,
                    TipInk,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                );
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

        /// <summary>The point the prompt waiting marks (<see cref="CadPrompt.Mark"/>), as -ATTEDIT marks the attribute it edits: a cross, 10 pixels across, in the colour of the snap markers.</summary>
        private void DrawMark(DrawingContext dc, CadView view, double scale)
        {
            GeoPoint2? mark = Session?.Prompt?.Mark;

            if (!mark.HasValue)
            {
                return;
            }

            Pen pen = MarkerPen(Options.Background);
            Point at = ToScreen(view, mark.Value, scale);
            dc.DrawLine(pen, new Point(at.X - 5.0, at.Y - 5.0), new Point(at.X + 5.0, at.Y + 5.0));
            dc.DrawLine(pen, new Point(at.X - 5.0, at.Y + 5.0), new Point(at.X + 5.0, at.Y - 5.0));
        }

        /// <summary>The tip by the cursor, as AutoCAD's dynamic input: the snap's name, the distance and the angle from the base point, the angle counted as the drawing counts angles (from the X axis of the UCS turned by ANGBASE, clockwise with ANGDIR 1).</summary>
        private static string TipText(
            CadSnapResult snapped,
            GeoPoint2? basePoint,
            GeoPoint2 target,
            CadCommandSession session
        )
        {
            string kind = snapped == null ? string.Empty : CadSnapMarker.NameOf(snapped.Kind);

            if (!basePoint.HasValue)
            {
                return kind;
            }

            GeoVector2 way = session.Ucs.ToUcs(target - basePoint.Value);
            double degrees = session.CountAngle(Math.Atan2(way.Y, way.X)) * 180.0 / Math.PI;
            string measured =
                way.Length.ToString("0.0", CultureInfo.InvariantCulture)
                + " < "
                + degrees.ToString("0", CultureInfo.InvariantCulture)
                + "°";
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
                var figure = new PathFigure
                {
                    StartPoint = new Point(p.X + stroke.Points[0].X, p.Y + stroke.Points[0].Y),
                    IsClosed = stroke.IsClosed,
                    IsFilled = stroke.IsFilled,
                };

                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    figure.Segments.Add(
                        new LineSegment(new Point(p.X + stroke.Points[i].X, p.Y + stroke.Points[i].Y), true)
                    );
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

            public bool Same(SceneKey other) =>
                other != null
                && ReferenceEquals(_list, other._list)
                && string.Equals(_numbers, other._numbers, StringComparison.Ordinal);
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
