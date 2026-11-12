using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A layer of the default layer table written to new drawings (Global settings › Layers, colours and lines): its name,
    /// colour, linetype, lineweight and whether it is plotted.
    /// </summary>
    public sealed class GlobalLayer
    {
        /// <summary>Makes a layer of the table.</summary>
        /// <param name="name">The name; not empty.</param>
        /// <param name="rgb">The colour as 0xRRGGBB.</param>
        /// <param name="linetype">The linetype, as Continuous or HIDDEN; Continuous where empty.</param>
        /// <param name="lineWeight">The lineweight in millimetres, from 0 to 2.11.</param>
        /// <param name="plot">Whether it is plotted.</param>
        /// <exception cref="ArgumentException">Thrown when the name is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the colour or the lineweight is out of range.</exception>
        public GlobalLayer(string name, int rgb, string linetype, double lineWeight, bool plot = true)
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("A layer needs a name.", nameof(name))
                : name.Trim();
            Rgb = GlobalSettings.Colour(rgb, nameof(rgb));
            Linetype = string.IsNullOrWhiteSpace(linetype) ? "Continuous" : linetype.Trim();
            LineWeight = GlobalSettings.Range(lineWeight, 0.0, 2.11, nameof(lineWeight));
            Plot = plot;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets the colour as 0xRRGGBB.</summary>
        public int Rgb { get; }

        /// <summary>Gets the linetype.</summary>
        public string Linetype { get; }

        /// <summary>Gets the lineweight in millimetres.</summary>
        public double LineWeight { get; }

        /// <summary>Gets whether the layer is plotted.</summary>
        public bool Plot { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The presenter's Global settings (16-settings.md, D13): what applies to the whole app and every drawing on this machine,
    /// kept as JSON in the user's file <see cref="DefaultPath"/>. Every setting has its default in code, so deleting the file
    /// gives the defaults back; a value read that is not valid (a colour that does not read, a pickbox of 500 pixels) falls
    /// back to its default with a Warn in the log, and reading never throws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The categories are those of the dialog: Appearance, Canvas and cursor, Drawing defaults and Data sources (for drawings
    /// made from a model), Units and rounding, Layers, colours and lines, Text and fonts, Shortcuts, and Save and export. A
    /// setter refuses a value out of its range, so that the dialog can say why: a wheel zoom factor of 0.5 is refused, it
    /// would zoom out on the wheel turned in.
    /// </para>
    /// <para>
    /// The defaults are AutoCAD's where it has one: a black canvas, white paper, selection and grips #3D9BFF, a pickbox of 5
    /// pixels, a wheel zoom of 1.2 times, the snaps but Nearest, AutoCAD's aliases and keys (<see cref="CadShortcuts.Default"/>).
    /// </para>
    /// </remarks>
    public sealed partial class GlobalSettings
    {
        /// <summary>The DXF export layouts, as Drawing defaults names them: layout and viewports, or the sheet in model space (D10).</summary>
        public static readonly IReadOnlyList<string> ExportLayouts = new[]
        {
            "LayoutAndViewports",
            "SheetInModelSpace",
        };

        /// <summary>
        /// What a message hidden by its Always perform my current choice does instead of asking: Ask shows it, Yes and No give
        /// that answer at once, as AutoCAD's Hidden Message Settings keep them.
        /// </summary>
        public static readonly IReadOnlyList<string> HiddenMessageChoices = new[] { "Ask", "Yes", "No" };

        /// <summary>The presets of a new drawing.</summary>
        public static readonly IReadOnlyList<string> Presets = new[] { "Part", "Assembly", "General" };

        /// <summary>The sheet sizes of a new drawing.</summary>
        public static readonly IReadOnlyList<string> SheetSizes = new[] { "A0", "A1", "A2", "A3", "A4" };

        /// <summary>The separators of thousands: a space, none, a comma, a point.</summary>
        public static readonly IReadOnlyList<string> ThousandsSeparators = new[] { "Space", "None", "Comma", "Point" };

        private const int Format = 1;
        private int _background;
        private int _paper = 0xFFFFFF;
        private int _blockEditor = 0x3B4049;
        private int _visibleLines = 0xFFFFFF;
        private int _hiddenLines = 0x9AA4B1;
        private int _selection = 0x3D9BFF;
        private int _grip = 0x3D9BFF;
        private int _tableIndicator = 0x5B6573;
        private int _gripDynamicColor = 140;
        private double _crosshairPercent = 5.0;
        private int _pickboxPixels = 5;
        private int _aperturePixels = 10;
        private int _shortcutMenu = 11;
        private double _polarAngle = 90.0;
        private int _polarMode;
        private string _polarAddedAngles = string.Empty;
        private double _polarDistance;
        private int _gridStyle;
        private double _wheelZoomFactor = 1.2;
        private string _preset = "Assembly";
        private string _sheetSize = "A2";
        private string _orientation = "Landscape";
        private string _projection = "Third angle";
        private int _coordinateDecimals = 2;
        private int _dimensionDecimals;
        private int _angleDecimals = 1;
        private string _thousands = "Space";
        private string _decimal = "Point";
        private string _uiFont = "Inter";
        private string _drawingFont = "Arial";
        private double _textHeight = 2.5;
        private double _titleTextHeight = 5.0;
        private double _widthFactor = 0.8;
        private List<string> _fontFolders = new List<string>(CadFonts.DefaultFolders);
        private string _replacementFont = CadFonts.BuiltInName;
        private string _fontMapFile = string.Empty;
        private List<string> _supportFolders = new List<string>(CadSupportFiles.DefaultFolders);
        private SortedDictionary<string, string> _fontSubstitutions = new SortedDictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );
        private int _checkEverySeconds = 30;
        private string _saveFolder = string.Empty;
        private int _autosaveMinutes = 10;
        private int _backupsKept = 3;
        private string _exportLayout = "LayoutAndViewports";
        private string _mtextUnsavedChanges = "Ask";
        private List<GlobalLayer> _layers = DefaultLayers();
        private SortedDictionary<string, string> _aliases = Copy(CadShortcuts.Default.Aliases);
        private SortedDictionary<string, string> _keys = Copy(CadShortcuts.Default.Keys);

        /// <summary>Gets the file the settings are kept in: %AppData%\GeometryHelper\Drawing\settings.json.</summary>
        public static string DefaultPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GeometryHelper",
                "Drawing",
                "settings.json"
            );

        // ---- Appearance

        /// <summary>Gets the mode: Dark; Light comes later.</summary>
        public string Mode => "Dark";

        /// <summary>Gets or sets the colour of the canvas behind a drawing, 0xRRGGBB: black by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a colour of 0 to 0xFFFFFF.</exception>
        public int CanvasBackground
        {
            get => _background;
            set => _background = Colour(value, nameof(CanvasBackground));
        }

        /// <summary>Gets or sets the colour of the paper of a layout: white by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a colour of 0 to 0xFFFFFF.</exception>
        public int Paper
        {
            get => _paper;
            set => _paper = Colour(value, nameof(Paper));
        }

        /// <summary>Gets or sets the colour of the canvas behind a block open in the Block Editor (BEDIT): #3B4049 by default, a grey no drawing's canvas is taken for.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a colour of 0 to 0xFFFFFF.</exception>
        public int BlockEditorBackground
        {
            get => _blockEditor;
            set => _blockEditor = Colour(value, nameof(BlockEditorBackground));
        }

        /// <summary>Gets or sets the colour of a drawing's visible lines of colour 7 on a dark canvas: white by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a colour of 0 to 0xFFFFFF.</exception>
        public int VisibleLines
        {
            get => _visibleLines;
            set => _visibleLines = Colour(value, nameof(VisibleLines));
        }

        /// <summary>Gets or sets the colour of the hidden lines of a model drawing: #9AA4B1 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a colour of 0 to 0xFFFFFF.</exception>
        public int HiddenLines
        {
            get => _hiddenLines;
            set => _hiddenLines = Colour(value, nameof(HiddenLines));
        }

        /// <summary>Gets or sets the colour of the objects selected: #3D9BFF by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a colour of 0 to 0xFFFFFF.</exception>
        public int Selection
        {
            get => _selection;
            set => _selection = Colour(value, nameof(Selection));
        }

        /// <summary>Gets or sets the colour of the grips: #3D9BFF by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a colour of 0 to 0xFFFFFF.</exception>
        public int Grip
        {
            get => _grip;
            set => _grip = Colour(value, nameof(Grip));
        }

        /// <summary>
        /// Gets or sets the fill of the bands of column letters and row numbers drawn round a table while its cells are
        /// selected, as AutoCAD's Table Indicator Color: #5B6573 by default. The letters stay white.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a colour of 0 to 0xFFFFFF.</exception>
        public int TableIndicatorColor
        {
            get => _tableIndicator;
            set => _tableIndicator = Colour(value, nameof(TableIndicatorColor));
        }

        /// <summary>
        /// Gets or sets whether the column letters and row numbers are drawn round a table while its cells are selected, as
        /// AutoCAD's TABLEINDICATOR 1, kept on this machine as AutoCAD keeps it in the registry: on by default, as AutoCAD 2021
        /// starts.
        /// </summary>
        public bool TableIndicator { get; set; } = true;

        /// <summary>
        /// Gets or sets the colour of a dynamic block's custom grips, as AutoCAD's GRIPDYNCOLOR: an index of AutoCAD's colours,
        /// 1 to 255, kept on this machine as AutoCAD keeps it in the registry: 140 by default, as AutoCAD 2021 starts, the
        /// light blue #00BFFF. AutoCAD 2021 took 1 to 255 and wrote no GRIPDYNCOLOR into a drawing (plugin GhDb1).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int GripDynamicColor
        {
            get => _gripDynamicColor;
            set => _gripDynamicColor = (int)Range(value, 1, 255, nameof(GripDynamicColor));
        }

        /// <summary>Gets or sets the size of the crosshair in per cent of the screen, 1 to 100: 5 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public double CrosshairPercent
        {
            get => _crosshairPercent;
            set => _crosshairPercent = Range(value, 1.0, 100.0, nameof(CrosshairPercent));
        }

        /// <summary>Gets or sets how far from the cursor a click picks, in pixels, 1 to 50: 5 by default, as AutoCAD's PICKBOX.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int PickboxPixels
        {
            get => _pickboxPixels;
            set => _pickboxPixels = (int)Range(value, 1, 50, nameof(PickboxPixels));
        }

        /// <summary>Gets or sets whether the canvas shows its dot grid at start (F7): off by default.</summary>
        public bool Grid { get; set; }

        /// <summary>Gets or sets whether lines are drawn with their lineweights: on by default.</summary>
        public bool ShowLineWeights { get; set; } = true;

        // ---- Canvas and cursor

        /// <summary>Gets or sets how much a notch of the wheel zooms, 1.01 to 4: 1.2 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public double WheelZoomFactor
        {
            get => _wheelZoomFactor;
            set => _wheelZoomFactor = Range(value, 1.01, 4.0, nameof(WheelZoomFactor));
        }

        /// <summary>Gets or sets the object snaps on: those of <see cref="CadSnapModes.All"/> but Nearest by default, Node and Geometric Center off.</summary>
        public CadSnapModes Snaps { get; set; } = CadSnapModes.All & ~CadSnapModes.Nearest;

        /// <summary>Gets or sets how near the cursor a snap is taken, in pixels, 2 to 50: 10 by default, as AutoCAD's APERTURE.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int AperturePixels
        {
            get => _aperturePixels;
            set => _aperturePixels = (int)Range(value, 2, 50, nameof(AperturePixels));
        }

        /// <summary>
        /// Gets or sets what a right-click on the canvas shows, as AutoCAD's SHORTCUTMENU, kept on this machine as AutoCAD keeps
        /// it in the registry, 0 to 31 as AutoCAD 2021 takes it: 1 the Default menu with nothing selected, 2 the Edit menu with
        /// objects selected, 4 the Command menu whenever a command runs, 8 the Command menu only where the prompt offers
        /// options, 16 a menu only for a right-click held a quarter of a second or longer, a shorter one Enter. A right-click
        /// no bit gives a menu is Enter. 11 by default, as AutoCAD 2021 starts: Default, Edit, and Command where it has options.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int ShortcutMenu
        {
            get => _shortcutMenu;
            set => _shortcutMenu = (int)Range(value, 0, 31, nameof(ShortcutMenu));
        }

        /// <summary>
        /// Gets or sets the polar tracking increment in degrees, as AutoCAD's POLARANG: above nought and up to 360; 90 by
        /// default, so that POLAR holds the cursor every quarter turn, and 45 every eighth.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not above nought and up to 360.</exception>
        public double PolarAngle
        {
            get => _polarAngle;
            set =>
                _polarAngle =
                    value > 0.0 && value <= 360.0
                        ? value
                        : throw new ArgumentOutOfRangeException(
                            nameof(PolarAngle),
                            value,
                            "The polar angle has to be above 0 and up to 360 degrees."
                        );
        }

        /// <summary>
        /// Gets or sets the bits of AutoCAD's POLARMODE, 0 to 15: 1 polar angles measured from the last segment, 2 object snap
        /// tracking on every polar angle (else level and upright alone), 4 additional polar angles, 8 points acquired only with
        /// Shift held; 0 by default, as AutoCAD's.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int PolarMode
        {
            get => _polarMode;
            set => _polarMode = (int)Range(value, 0, 15, nameof(PolarMode));
        }

        /// <summary>
        /// Gets or sets the additional polar angles, as AutoCAD's POLARADDANG: up to ten angles parted by semicolons, a number
        /// in degrees or an angle with its own mark, "10;33.5;50g", as <see cref="CadUnits.TryAngles"/> reads them; empty, as by
        /// default, for none. They are held while POLARMODE has its bit 4.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the value is not such a list.</exception>
        public string PolarAddedAngles
        {
            get => _polarAddedAngles;
            set =>
                _polarAddedAngles = CadUnits.TryAngles(value, 0, out _)
                    ? value
                    : throw new ArgumentException(
                        "The additional polar angles are up to ten angles parted by semicolons: " + value + ".",
                        nameof(value)
                    );
        }

        /// <summary>
        /// Gets or sets whether SNAP steps along the polar angles in place of the grid, as AutoCAD's SNAPTYPE 1 (PolarSnap);
        /// off by default, as SNAPTYPE 0 (Grid snap).
        /// </summary>
        public bool PolarSnap { get; set; }

        /// <summary>
        /// Gets or sets the step of PolarSnap in drawing units, as AutoCAD's POLARDIST: nought by default, when the snap X
        /// spacing is the step, as it is for a value below nought, which AutoCAD 2021 keeps as typed (SETVAR POLARDIST -1).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not finite.</exception>
        public double PolarDistance
        {
            get => _polarDistance;
            set => _polarDistance = Range(value, -double.MaxValue, double.MaxValue, nameof(PolarDistance));
        }

        /// <summary>
        /// Gets or sets the bits of AutoCAD's GRIDSTYLE, 0 to 7: the grid drawn as dots in 1 model space, 2 the Block Editor and
        /// 4 the layouts, as lines where its bit is clear; 0 by default, lines everywhere.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int GridStyle
        {
            get => _gridStyle;
            set => _gridStyle = (int)Range(value, 0, 7, nameof(GridStyle));
        }

        /// <summary>
        /// Gets or sets whether SNAP holds the cursor on the snap grid at all times, as AutoCAD's SNAPGRIDLEGACY 1 (SNAP Legacy
        /// Yes): with no point asked too, so that the crosshair and the objects picked go from one grid point to the next; off
        /// by default, as SNAPGRIDLEGACY 0, when SNAP holds the cursor only while a point is asked.
        /// </summary>
        public bool SnapGridLegacy { get; set; }

        /// <summary>Gets or sets whether F3 switches the snaps: on by default.</summary>
        public bool F3TogglesSnaps { get; set; } = true;

        /// <summary>
        /// Gets or sets whether a selection box can also be dragged, pressed at one corner and let go at the other, as AutoCAD's
        /// PICKDRAG 1: off by default, a box then clicked at one corner and then at the other, as PICKDRAG 0.
        /// </summary>
        public bool SelectByDragging { get; set; }

        /// <summary>
        /// Gets or sets whether dynamic input is on (DYN, F12), as AutoCAD's DYNMODE: the prompt of the command running and the
        /// cursor's coordinates, or its distance and angle from the base point, shown beside the cursor. On by default, as in AutoCAD.
        /// </summary>
        public bool DynamicInput { get; set; } = true;

        // ---- Drawing defaults (model drawings)

        /// <summary>Gets or sets the preset of a new drawing: Part, Assembly or General; Assembly by default.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is not one of <see cref="Presets"/>.</exception>
        public string DefaultPreset
        {
            get => _preset;
            set => _preset = OneOf(value, Presets, nameof(DefaultPreset));
        }

        /// <summary>Gets or sets the sheet of a new drawing: A0 to A4; A2 by default.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is not one of <see cref="SheetSizes"/>.</exception>
        public string DefaultSheetSize
        {
            get => _sheetSize;
            set => _sheetSize = OneOf(value, SheetSizes, nameof(DefaultSheetSize));
        }

        /// <summary>Gets or sets which way round the sheet lies: Landscape or Portrait; Landscape by default.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is neither.</exception>
        public string DefaultOrientation
        {
            get => _orientation;
            set => _orientation = OneOf(value, new[] { "Landscape", "Portrait" }, nameof(DefaultOrientation));
        }

        /// <summary>Gets or sets the projection method (D9): Third angle or First angle; Third angle by default.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is neither.</exception>
        public string DefaultProjection
        {
            get => _projection;
            set => _projection = OneOf(value, new[] { "Third angle", "First angle" }, nameof(DefaultProjection));
        }

        // ---- Units and rounding

        /// <summary>Gets or sets the decimals of the coordinates the status bar shows, 0 to 8: 2 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int CoordinateDecimals
        {
            get => _coordinateDecimals;
            set => _coordinateDecimals = (int)Range(value, 0, 8, nameof(CoordinateDecimals));
        }

        /// <summary>Gets or sets the decimals of the dimensions of a new model drawing, 0 to 8: 0 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int DimensionDecimals
        {
            get => _dimensionDecimals;
            set => _dimensionDecimals = (int)Range(value, 0, 8, nameof(DimensionDecimals));
        }

        /// <summary>Gets or sets the decimals of angles shown, 0 to 8: 1 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int AngleDecimals
        {
            get => _angleDecimals;
            set => _angleDecimals = (int)Range(value, 0, 8, nameof(AngleDecimals));
        }

        /// <summary>Gets or sets what parts the thousands: Space, None, Comma or Point; Space by default, as "12 345".</summary>
        /// <exception cref="ArgumentException">Thrown when the value is not one of <see cref="ThousandsSeparators"/>, or is the decimal separator.</exception>
        public string ThousandsSeparator
        {
            get => _thousands;
            set
            {
                string v = OneOf(value, ThousandsSeparators, nameof(ThousandsSeparator));
                _thousands =
                    v == _decimal
                        ? throw new ArgumentException(
                            "The thousands cannot be parted by the decimal separator.",
                            nameof(ThousandsSeparator)
                        )
                        : v;
            }
        }

        /// <summary>Gets or sets the decimal separator: Point or Comma; Point by default.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is neither, or is the thousands separator.</exception>
        public string DecimalSeparator
        {
            get => _decimal;
            set
            {
                string v = OneOf(value, new[] { "Point", "Comma" }, nameof(DecimalSeparator));
                _decimal =
                    v == _thousands
                        ? throw new ArgumentException(
                            "The decimal separator cannot be the thousands separator.",
                            nameof(DecimalSeparator)
                        )
                        : v;
            }
        }

        // ---- Layers, colours and lines

        /// <summary>
        /// Gets or sets the layer table written to new drawings, in order: by default the layers a model drawing writes, Visible,
        /// Hidden, Centre, Hatch, Dimension, Mark, Symbol, Sheet and Viewport, the last not plotted. A file that lists its own
        /// table is read as it lists it, so one written before Symbol and Viewport were added keeps the seven it has.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the value is null, holds a null layer, or holds two layers of one name.</exception>
        public IReadOnlyList<GlobalLayer> Layers
        {
            get => _layers.AsReadOnly();
            set
            {
                if (value == null || value.Any(l => l == null))
                {
                    throw new ArgumentException("The layer table holds no null layer.", nameof(Layers));
                }

                string twice = value
                    .GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .FirstOrDefault();
                _layers =
                    twice == null
                        ? value.ToList()
                        : throw new ArgumentException("The layer " + twice + " is in the table twice.", nameof(Layers));
            }
        }

        // ---- Text and fonts

        /// <summary>Gets or sets the font of the app: Inter by default.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is empty.</exception>
        public string UiFont
        {
            get => _uiFont;
            set => _uiFont = Name(value, nameof(UiFont));
        }

        /// <summary>Gets or sets the font text is drawn in where its style's font is missing and <see cref="ReplacementFont"/> is not found either: Arial by default.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is empty.</exception>
        public string DrawingFont
        {
            get => _drawingFont;
            set => _drawingFont = Name(value, nameof(DrawingFont));
        }

        /// <summary>
        /// Gets or sets the height of the text of a new model drawing on paper, in millimetres, which TEXT and MTEXT start
        /// from and the dimensions DIM* makes are written with: 2.5 by default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not above 0 and up to 100.</exception>
        public double TextHeight
        {
            get => _textHeight;
            set => _textHeight = Range(value, 0.1, 100.0, nameof(TextHeight));
        }

        /// <summary>Gets or sets the height of the titles of views on paper, in millimetres: 5 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not above 0 and up to 100.</exception>
        public double TitleTextHeight
        {
            get => _titleTextHeight;
            set => _titleTextHeight = Range(value, 0.1, 100.0, nameof(TitleTextHeight));
        }

        /// <summary>Gets or sets the width factor of the drawing text, which TEXT and MTEXT write, 0.1 to 10: 0.8 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public double WidthFactor
        {
            get => _widthFactor;
            set => _widthFactor = Range(value, 0.1, 10.0, nameof(WidthFactor));
        }

        // ---- Fonts

        /// <summary>
        /// Gets or sets the folders the fonts of text styles are looked for in, in order, after the folder of the drawing, as
        /// AutoCAD's support file search path: none by default (<see cref="CadFonts.DefaultFolders"/>), so that an SHX font is
        /// read only from the drawing's folder and the folders the user lists, and AutoCAD's Fonts folders are never looked
        /// for; a TrueType file is looked for in the Fonts folder of Windows as well. A list saved before keeps the folders it
        /// holds. Blank entries are left out and a folder given twice is kept once.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        public IReadOnlyList<string> FontSearchFolders
        {
            get => _fontFolders.AsReadOnly();
            set =>
                _fontFolders = (value ?? throw new ArgumentNullException(nameof(value)))
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Select(f => f.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        /// <summary>
        /// Gets or sets the font text is drawn in where its style's font is not found, as AutoCAD's FONTALT: an SHX file, a
        /// TrueType file or a typeface, and after a comma the big font a style's big font not found is drawn in
        /// (simplex.shx,gbcbig.shx); by default the presenter's own single-stroke font, GeometryHelper Simplex
        /// (<see cref="CadFonts.BuiltInName"/>). Empty, or a font not found either, draws it in <see cref="DrawingFont"/>.
        /// </summary>
        public string ReplacementFont
        {
            get => _replacementFont;
            set => _replacementFont = (value ?? string.Empty).Trim();
        }

        /// <summary>
        /// Gets or sets the font mapping file, as AutoCAD's FONTMAP: the path of a .fmp file of a line <c>oldfont;newfont</c>
        /// for each font drawn in another, its pairs read into <see cref="FontSubstitutions"/> as the drawings are given their
        /// fonts, a font the table names as well drawn as the table says; empty, the default, for none.
        /// </summary>
        public string FontMapFile
        {
            get => _fontMapFile;
            set => _fontMapFile = (value ?? string.Empty).Trim();
        }

        /// <summary>
        /// Gets or sets whether the Missing SHX files dialog is shown once as a drawing opens whose text styles name SHX fonts
        /// not found, as AutoCAD shows it: on by default; its Do not show again turns it off. The command line says the
        /// missing files in one line either way.
        /// </summary>
        public bool ShowMissingShxDialog { get; set; } = true;

        /// <summary>
        /// Gets or sets the support file search folders: the user's own folders where linetype files (.lin) and hatch pattern
        /// files (.pat) are looked for, in order, after the folder of the drawing, as AutoCAD's support path; none by default
        /// (<see cref="CadSupportFiles.DefaultFolders"/>), the presenter's own linetypes and patterns then the only ones, and
        /// AutoCAD's Support folders never looked for. Blank entries are left out and a folder given twice is kept once.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        public IReadOnlyList<string> SupportSearchFolders
        {
            get => _supportFolders.AsReadOnly();
            set =>
                _supportFolders = (value ?? throw new ArgumentNullException(nameof(value)))
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Select(f => f.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        /// <summary>Gets the fonts drawn in others, as AutoCAD's font mapping file gives them: each name to the name it is drawn in (romans.shx to isocp.shx); none by default.</summary>
        public IReadOnlyDictionary<string, string> FontSubstitutions => _fontSubstitutions;

        /// <summary>Sets the fonts drawn in others, each name to the name it is drawn in; a pair of a blank name is left out, a name given twice the last kept.</summary>
        /// <param name="substitutions">The pairs.</param>
        /// <exception cref="ArgumentNullException">Thrown when the pairs are null.</exception>
        public void SetFontSubstitutions(IEnumerable<KeyValuePair<string, string>> substitutions)
        {
            var table = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (
                KeyValuePair<string, string> pair in substitutions
                    ?? throw new ArgumentNullException(nameof(substitutions))
            )
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    table[pair.Key.Trim()] = pair.Value.Trim();
                }
            }

            _fontSubstitutions = table;
        }

        // ---- Data sources (model drawings)

        /// <summary>Gets or sets whether the sources are asked for changes of the model: on by default.</summary>
        public bool CheckForModelChanges { get; set; } = true;

        /// <summary>Gets or sets how often the sources are asked, in seconds, 5 to 3 600: 30 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int CheckEverySeconds
        {
            get => _checkEverySeconds;
            set => _checkEverySeconds = (int)Range(value, 5, 3600, nameof(CheckEverySeconds));
        }

        // ---- Shortcuts

        /// <summary>Gets the aliases, each to a command's name: AutoCAD's by default.</summary>
        public IReadOnlyDictionary<string, string> Aliases => _aliases;

        /// <summary>Gets the keys, each to a command's name or a toggle: AutoCAD's by default.</summary>
        public IReadOnlyDictionary<string, string> Keys => _keys;

        // ---- Save and export

        /// <summary>Gets or sets the folder drawings are saved and exported to; empty, the default, for the user's Documents.</summary>
        public string SaveFolder
        {
            get => _saveFolder;
            set => _saveFolder = (value ?? string.Empty).Trim();
        }

        /// <summary>Gets or sets how often a drawing with changes is saved aside, in minutes, 0 (never) to 240: 10 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int AutosaveMinutes
        {
            get => _autosaveMinutes;
            set => _autosaveMinutes = (int)Range(value, 0, 240, nameof(AutosaveMinutes));
        }

        /// <summary>Gets or sets how many backups of a file are kept, 0 to 50: 3 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int BackupsKept
        {
            get => _backupsKept;
            set => _backupsKept = (int)Range(value, 0, 50, nameof(BackupsKept));
        }

        /// <summary>Gets or sets how a model drawing's DXF is laid out (D10): LayoutAndViewports, the default, or SheetInModelSpace.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is not one of <see cref="ExportLayouts"/>.</exception>
        public string ExportLayout
        {
            get => _exportLayout;
            set => _exportLayout = OneOf(value, ExportLayouts, nameof(ExportLayout));
        }

        /// <summary>Gets or sets whether the folder is opened after an export: off by default.</summary>
        public bool OpenAfterExport { get; set; }

        // ---- Hidden messages

        /// <summary>
        /// Gets or sets what Esc in the MTEXT editor with changes does, one of <see cref="HiddenMessageChoices"/>: Ask, the
        /// default, shows Multiline Text - Unsaved Changes; Yes saves the changes and No drops them with no question, as
        /// AutoCAD remembers the answer given with Always perform my current choice ticked. Cancel is never remembered.
        /// Setting it back to Ask, or Global settings' Reset to defaults, shows the question again.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the value is not one of <see cref="HiddenMessageChoices"/>.</exception>
        public string MTextUnsavedChanges
        {
            get => _mtextUnsavedChanges;
            set => _mtextUnsavedChanges = OneOf(value, HiddenMessageChoices, nameof(MTextUnsavedChanges));
        }

        /// <summary>
        /// Sets the aliases and keys: each alias and key to a command's name, letters of either case the same. A key given to
        /// two commands is kept, and said by <see cref="ShortcutConflicts"/>.
        /// </summary>
        /// <param name="aliases">The aliases.</param>
        /// <param name="keys">The keys.</param>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        /// <exception cref="ArgumentException">Thrown when an alias or key, or its command, is empty.</exception>
        public void SetShortcuts(
            IEnumerable<KeyValuePair<string, string>> aliases,
            IEnumerable<KeyValuePair<string, string>> keys
        )
        {
            var shortcuts = new CadShortcuts(
                aliases ?? throw new ArgumentNullException(nameof(aliases)),
                keys ?? throw new ArgumentNullException(nameof(keys))
            );
            _aliases = Copy(shortcuts.Aliases);
            _keys = Copy(shortcuts.Keys);
        }

        /// <summary>Gets the aliases and keys as the command line takes them.</summary>
        /// <returns>The shortcuts.</returns>
        public CadShortcuts ToShortcuts() => new CadShortcuts(_aliases, _keys);

        /// <summary>
        /// Gets the fonts a document's text is drawn in, as the font settings give them: the pairs of <see cref="FontMapFile"/>
        /// read into <see cref="FontSubstitutions"/>, a font the table names as well drawn as the table says.
        /// </summary>
        /// <param name="familyExists">Whether the canvas has a TrueType typeface of a name; null to take every typeface as there.</param>
        /// <returns>The fonts.</returns>
        public CadFonts ToFonts(Func<string, bool> familyExists = null) =>
            new CadFonts(
                _fontFolders,
                _replacementFont,
                CadFonts.ReadFontMap(_fontMapFile).Concat(_fontSubstitutions),
                familyExists
            );

        /// <summary>
        /// Says the shortcuts that clash: an alias that is a command's own name of another command, written "DU is the name of
        /// the command DU"; none where nothing clashes. Keys cannot clash here, a key reaching one command; the dialog says where
        /// two rows ask for one key.
        /// </summary>
        /// <param name="commandNames">The names of the commands known.</param>
        /// <returns>A line per clash, in ordinal order.</returns>
        public IReadOnlyList<string> ShortcutConflicts(IEnumerable<string> commandNames)
        {
            var names = new HashSet<string>(
                commandNames ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase
            );
            return _aliases
                .Where(a => names.Contains(a.Key) && !string.Equals(a.Key, a.Value, StringComparison.OrdinalIgnoreCase))
                .Select(a => "The alias " + a.Key + " is the name of the command " + a.Key.ToUpperInvariant() + ".")
                .ToList();
        }

        /// <summary>Makes a copy that shares nothing with these settings.</summary>
        /// <returns>The copy.</returns>
        public GlobalSettings Clone()
        {
            var copy = (GlobalSettings)MemberwiseClone();
            copy._layers = new List<GlobalLayer>(_layers);
            copy._fontFolders = new List<string>(_fontFolders);
            copy._supportFolders = new List<string>(_supportFolders);
            copy._fontSubstitutions = Copy(_fontSubstitutions);
            copy._aliases = Copy(_aliases);
            copy._keys = Copy(_keys);
            CopyRecordsInto(copy);
            return copy;
        }

        /// <summary>Formats a number as the units say: decimals, the thousands parted, the decimal separator: 12 345.68.</summary>
        /// <param name="value">The number.</param>
        /// <param name="decimals">The decimals.</param>
        /// <returns>The text.</returns>
        public string FormatNumber(double value, int decimals)
        {
            var format = new NumberFormatInfo
            {
                NumberGroupSeparator =
                    _thousands == "Space" ? " "
                    : _thousands == "Comma" ? ","
                    : _thousands == "Point" ? "."
                    : string.Empty,
                NumberDecimalSeparator = _decimal == "Comma" ? "," : ".",
            };
            return value.ToString("#,0" + (decimals > 0 ? "." + new string('0', decimals) : string.Empty), format);
        }

        /// <summary>
        /// Reads settings from a JSON file, never throwing: no file gives the defaults; a file that does not read gives the
        /// defaults, said once in the log; each value that is missing keeps its default, and each that is not valid falls back
        /// to its default with a Warn naming it.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>The settings.</returns>
        public static GlobalSettings Load(string path)
        {
            var settings = new GlobalSettings();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return settings;
            }

            XElement root;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);

                using (
                    XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(
                        bytes,
                        XmlDictionaryReaderQuotas.Max
                    )
                )
                {
                    root = XElement.Load(reader);
                }
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is XmlException
                    || exception is ArgumentException
                )
            {
                GeometryHelperLog.Warn("GlobalSettings: " + path + " does not read; the defaults are used.", exception);
                return settings;
            }

            new Reader(root, path, settings).ReadAll();
            return settings;
        }

        /// <summary>Writes the settings to a JSON file, the folder made where it is missing; LF line ends, two spaces of indent.</summary>
        /// <param name="path">The file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file may not be written.</exception>
        public void Save(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string folder = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
            GeometryHelperLog.Info("GlobalSettings: saved to " + path + ".");
        }

        /// <summary>Gives the settings as the JSON text <see cref="Save"/> writes.</summary>
        /// <returns>The text.</returns>
        public string ToJson()
        {
            var w = new JsonText();
            w.Open(null);
            w.Value("format", Format);
            w.Open("appearance");
            w.Value("mode", Mode);
            w.Value("canvasBackground", Hex(_background));
            w.Value("paper", Hex(_paper));
            w.Value("blockEditorBackground", Hex(_blockEditor));
            w.Value("visibleLines", Hex(_visibleLines));
            w.Value("hiddenLines", Hex(_hiddenLines));
            w.Value("selection", Hex(_selection));
            w.Value("grip", Hex(_grip));
            w.Value("tableIndicatorColor", Hex(_tableIndicator));
            w.Value("tableIndicator", TableIndicator);
            w.Value("gripDynamicColor", _gripDynamicColor);
            w.Value("crosshairPercent", _crosshairPercent);
            w.Value("pickboxPixels", _pickboxPixels);
            w.Value("grid", Grid);
            w.Value("showLineWeights", ShowLineWeights);
            w.Close();
            w.Open("canvas");
            w.Value("wheelZoomFactor", _wheelZoomFactor);
            w.Value("shortcutMenu", _shortcutMenu);
            w.Value("snaps", string.Join(",", SnapNames(Snaps)));
            w.Value("aperturePixels", _aperturePixels);
            w.Value("polarAngle", _polarAngle);
            w.Value("polarMode", _polarMode);
            w.Value("polarAddedAngles", _polarAddedAngles);
            w.Value("polarSnap", PolarSnap);
            w.Value("polarDistance", _polarDistance);
            w.Value("gridStyle", _gridStyle);
            w.Value("snapGridLegacy", SnapGridLegacy);
            w.Value("f3TogglesSnaps", F3TogglesSnaps);
            w.Value("selectByDragging", SelectByDragging);
            w.Value("dynamicInput", DynamicInput);
            w.Close();
            w.Open("drawingDefaults");
            w.Value("preset", _preset);
            w.Value("sheetSize", _sheetSize);
            w.Value("orientation", _orientation);
            w.Value("projection", _projection);
            w.Close();
            w.Open("units");
            w.Value("coordinateDecimals", _coordinateDecimals);
            w.Value("dimensionDecimals", _dimensionDecimals);
            w.Value("angleDecimals", _angleDecimals);
            w.Value("thousandsSeparator", _thousands);
            w.Value("decimalSeparator", _decimal);
            w.Close();
            w.OpenList("layers");

            foreach (GlobalLayer layer in _layers)
            {
                w.Open(null);
                w.Value("name", layer.Name);
                w.Value("colour", Hex(layer.Rgb));
                w.Value("linetype", layer.Linetype);
                w.Value("lineweight", layer.LineWeight);
                w.Value("plot", layer.Plot);
                w.Close();
            }

            w.CloseList();
            w.Open("text");
            w.Value("uiFont", _uiFont);
            w.Value("drawingFont", _drawingFont);
            w.Value("textHeight", _textHeight);
            w.Value("titleTextHeight", _titleTextHeight);
            w.Value("widthFactor", _widthFactor);
            w.Close();
            w.Open("hiddenMessages");
            w.Value("mtextUnsavedChanges", _mtextUnsavedChanges);
            w.Close();
            w.Open("fonts");
            w.OpenList("searchFolders");

            foreach (string folder in _fontFolders)
            {
                w.Value(null, folder);
            }

            w.CloseList();
            w.Value("replacement", _replacementFont);
            w.Value("fontMap", _fontMapFile);
            w.Value("showMissingShxDialog", ShowMissingShxDialog);
            w.Open("substitutions");

            foreach (KeyValuePair<string, string> pair in _fontSubstitutions)
            {
                w.Value(pair.Key, pair.Value);
            }

            w.Close();
            w.Close();
            w.Open("support");
            w.OpenList("searchFolders");

            foreach (string folder in _supportFolders)
            {
                w.Value(null, folder);
            }

            w.CloseList();
            w.Close();
            w.Open("dataSources");
            w.Value("checkForModelChanges", CheckForModelChanges);
            w.Value("checkEverySeconds", _checkEverySeconds);
            w.Close();
            w.Open("shortcuts");
            w.Open("aliases");

            foreach (KeyValuePair<string, string> alias in _aliases)
            {
                w.Value(alias.Key, alias.Value);
            }

            w.Close();
            w.Open("keys");

            foreach (KeyValuePair<string, string> key in _keys)
            {
                w.Value(key.Key, key.Value);
            }

            w.Close();
            w.Close();
            w.Open("saveAndExport");
            w.Value("saveFolder", _saveFolder);
            w.Value("autosaveMinutes", _autosaveMinutes);
            w.Value("backupsKept", _backupsKept);
            w.Value("exportLayout", _exportLayout);
            w.Value("openAfterExport", OpenAfterExport);
            w.Close();
            WriteInputAndRecords(w);
            w.Close();
            return w.ToString();
        }

        /// <summary>Writes a colour as #RRGGBB.</summary>
        /// <param name="rgb">The colour as 0xRRGGBB.</param>
        /// <returns>The text.</returns>
        public static string Hex(int rgb) => "#" + (rgb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);

        /// <summary>Reads a colour written #RRGGBB or RRGGBB.</summary>
        /// <param name="text">The text.</param>
        /// <param name="rgb">The colour as 0xRRGGBB.</param>
        /// <returns>Whether it read.</returns>
        public static bool TryParseHex(string text, out int rgb)
        {
            string hex = (text ?? string.Empty).Trim().TrimStart('#');
            rgb = 0;
            return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb);
        }

        /// <summary>The names of the snaps on, in the order they came to the presenter.</summary>
        internal static IEnumerable<string> SnapNames(CadSnapModes modes) =>
            new[]
            {
                CadSnapModes.Endpoint,
                CadSnapModes.Midpoint,
                CadSnapModes.Center,
                CadSnapModes.Quadrant,
                CadSnapModes.Intersection,
                CadSnapModes.Perpendicular,
                CadSnapModes.Tangent,
                CadSnapModes.Nearest,
                CadSnapModes.Node,
                CadSnapModes.GeometricCenter,
                CadSnapModes.Extension,
                CadSnapModes.Insertion,
                CadSnapModes.ApparentIntersection,
                CadSnapModes.Parallel,
            }
                .Where(m => (modes & m) != 0)
                .Select(m => m.ToString());

        internal static int Colour(int value, string name) =>
            value >= 0 && value <= 0xFFFFFF
                ? value
                : throw new ArgumentOutOfRangeException(name, value, "A colour is 0 to 0xFFFFFF, #000000 to #FFFFFF.");

        internal static double Range(double value, double min, double max, string name) =>
            value >= min && value <= max
                ? value
                : throw new ArgumentOutOfRangeException(
                    name,
                    value,
                    "The value has to be from "
                        + min.ToString(CultureInfo.InvariantCulture)
                        + " to "
                        + max.ToString(CultureInfo.InvariantCulture)
                        + "."
                );

        private static string OneOf(string value, IEnumerable<string> allowed, string name) =>
            allowed.FirstOrDefault(a =>
                string.Equals(a, (value ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
            ) ?? throw new ArgumentException("The value has to be one of " + string.Join(", ", allowed) + ".", name);

        private static string Name(string value, string name) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("The value needs a name.", name)
                : value.Trim();

        private static SortedDictionary<string, string> Copy(IEnumerable<KeyValuePair<string, string>> pairs)
        {
            var copy = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> pair in pairs)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }

        private static List<GlobalLayer> DefaultLayers() =>
            new List<GlobalLayer>
            {
                new GlobalLayer("Visible", 0xFFFFFF, "Continuous", 0.35),
                new GlobalLayer("Hidden", 0x9AA4B1, "HIDDEN", 0.18),
                new GlobalLayer("Centre", 0xF85149, "CENTER", 0.18),
                new GlobalLayer("Hatch", 0x9AA4B1, "Continuous", 0.13),
                new GlobalLayer("Dimension", 0x3FB950, "Continuous", 0.18),
                new GlobalLayer("Mark", 0x39C5CF, "Continuous", 0.18),
                new GlobalLayer("Symbol", 0xBC8CFF, "Continuous", 0.18),
                new GlobalLayer("Sheet", 0xFFFFFF, "Continuous", 0.25),
                new GlobalLayer("Viewport", 0x9AA4B1, "Continuous", 0.13, plot: false),
            };

        /// <summary>Reads the values of the JSON file, each on its own: a value that is not valid falls back to its default with a Warn.</summary>
        private sealed class Reader
        {
            private readonly XElement _root;
            private readonly string _path;
            private readonly GlobalSettings _s;

            public Reader(XElement root, string path, GlobalSettings settings)
            {
                _root = root;
                _path = path;
                _s = settings;
            }

            public void ReadAll()
            {
                Colour("appearance", "canvasBackground", v => _s.CanvasBackground = v);
                Colour("appearance", "paper", v => _s.Paper = v);
                Colour("appearance", "blockEditorBackground", v => _s.BlockEditorBackground = v);
                Colour("appearance", "visibleLines", v => _s.VisibleLines = v);
                Colour("appearance", "hiddenLines", v => _s.HiddenLines = v);
                Colour("appearance", "selection", v => _s.Selection = v);
                Colour("appearance", "grip", v => _s.Grip = v);
                Colour("appearance", "tableIndicatorColor", v => _s.TableIndicatorColor = v);
                Flag("appearance", "tableIndicator", v => _s.TableIndicator = v);
                Number("appearance", "gripDynamicColor", v => _s.GripDynamicColor = Whole(v));
                Number("appearance", "crosshairPercent", v => _s.CrosshairPercent = v);
                Number("appearance", "pickboxPixels", v => _s.PickboxPixels = Whole(v));
                Flag("appearance", "grid", v => _s.Grid = v);
                Flag("appearance", "showLineWeights", v => _s.ShowLineWeights = v);
                Number("canvas", "wheelZoomFactor", v => _s.WheelZoomFactor = v);
                Number("canvas", "shortcutMenu", v => _s.ShortcutMenu = Whole(v));
                Text("canvas", "snaps", v => _s.Snaps = Snaps(v));
                Number("canvas", "aperturePixels", v => _s.AperturePixels = Whole(v));
                Number("canvas", "polarAngle", v => _s.PolarAngle = v);
                Number("canvas", "polarMode", v => _s.PolarMode = Whole(v));
                Text("canvas", "polarAddedAngles", v => _s.PolarAddedAngles = v);
                Flag("canvas", "polarSnap", v => _s.PolarSnap = v);
                Number("canvas", "polarDistance", v => _s.PolarDistance = v);
                Number("canvas", "gridStyle", v => _s.GridStyle = Whole(v));
                Flag("canvas", "snapGridLegacy", v => _s.SnapGridLegacy = v);
                Flag("canvas", "f3TogglesSnaps", v => _s.F3TogglesSnaps = v);
                Flag("canvas", "selectByDragging", v => _s.SelectByDragging = v);
                Flag("canvas", "dynamicInput", v => _s.DynamicInput = v);
                Text("drawingDefaults", "preset", v => _s.DefaultPreset = v);
                Text("drawingDefaults", "sheetSize", v => _s.DefaultSheetSize = v);
                Text("drawingDefaults", "orientation", v => _s.DefaultOrientation = v);
                Text("drawingDefaults", "projection", v => _s.DefaultProjection = v);
                Number("units", "coordinateDecimals", v => _s.CoordinateDecimals = Whole(v));
                Number("units", "dimensionDecimals", v => _s.DimensionDecimals = Whole(v));
                Number("units", "angleDecimals", v => _s.AngleDecimals = Whole(v));
                Text("units", "decimalSeparator", v => _s.DecimalSeparator = v);
                Text("units", "thousandsSeparator", v => _s.ThousandsSeparator = v);
                Text("text", "uiFont", v => _s.UiFont = v);
                Text("text", "drawingFont", v => _s.DrawingFont = v);
                Number("text", "textHeight", v => _s.TextHeight = v);
                Number("text", "titleTextHeight", v => _s.TitleTextHeight = v);
                Number("text", "widthFactor", v => _s.WidthFactor = v);
                Text("hiddenMessages", "mtextUnsavedChanges", v => _s.MTextUnsavedChanges = v);
                Text("fonts", "replacement", v => _s.ReplacementFont = v);
                Text("fonts", "fontMap", v => _s.FontMapFile = v);
                Flag("fonts", "showMissingShxDialog", v => _s.ShowMissingShxDialog = v);
                Take("fonts", "searchFolders", e => _s.FontSearchFolders = e.Elements().Select(f => f.Value).ToList());
                Take("fonts", "substitutions", e => _s.SetFontSubstitutions(Pairs(e)));
                Take("support", "searchFolders", e =>
                    _s.SupportSearchFolders = e.Elements().Select(f => f.Value).ToList()
                );
                Flag("dataSources", "checkForModelChanges", v => _s.CheckForModelChanges = v);
                Number("dataSources", "checkEverySeconds", v => _s.CheckEverySeconds = Whole(v));
                Text("saveAndExport", "saveFolder", v => _s.SaveFolder = v);
                Number("saveAndExport", "autosaveMinutes", v => _s.AutosaveMinutes = Whole(v));
                Number("saveAndExport", "backupsKept", v => _s.BackupsKept = Whole(v));
                Text("saveAndExport", "exportLayout", v => _s.ExportLayout = v);
                Flag("saveAndExport", "openAfterExport", v => _s.OpenAfterExport = v);
                Flag("input", "autoComplete", v => _s.AutoComplete = v);
                Flag("input", "autoCorrect", v => _s.AutoCorrect = v);
                Flag("input", "searchSystemVariables", v => _s.SearchSystemVariables = v);
                Flag("input", "searchContent", v => _s.SearchContent = v);
                Flag("input", "midStringSearch", v => _s.MidStringSearch = v);
                Number("input", "delayMilliseconds", v => _s.AutoCompleteDelay = Whole(v));
                Flag("input", "sortAlphabetically", v => _s.SortAlphabetically = v);
                Flag("input", "separateSystemVariables", v => _s.SeparateSystemVariables = v);
                Number("input", "autoCorrectMistypes", v => _s.AutoCorrectMistypes = Whole(v));
                ReadLayers();
                ReadShortcuts();
                _s.ReadRecords(_root, _path);
            }

            private static int Whole(double value) =>
                value == Math.Floor(value) && value >= int.MinValue && value <= int.MaxValue
                    ? (int)value
                    : throw new ArgumentException("A whole number is needed.");

            private static CadSnapModes Snaps(string names)
            {
                CadSnapModes modes = CadSnapModes.None;

                foreach (string name in names.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    modes |=
                        Enum.TryParse(name.Trim(), true, out CadSnapModes mode) && mode != CadSnapModes.All
                            ? mode
                            : throw new ArgumentException("No snap is called " + name.Trim() + ".");
                }

                return modes;
            }

            private XElement Find(string group, string name) => _root.Element(group)?.Element(name);

            private void Take(string group, string name, Action<XElement> set)
            {
                XElement element = Find(group, name);

                if (element == null)
                {
                    return;
                }

                try
                {
                    set(element);
                }
                catch (Exception exception)
                    when (exception is ArgumentException
                        || exception is FormatException
                        || exception is OverflowException
                    )
                {
                    GeometryHelperLog.Warn(
                        "GlobalSettings: "
                            + group
                            + "."
                            + name
                            + " = \""
                            + element.Value
                            + "\" of "
                            + _path
                            + " is not valid ("
                            + exception.Message.Split('\n')[0].Trim()
                            + "); its default is used."
                    );
                }
            }

            private void Colour(string group, string name, Action<int> set) =>
                Take(group, name, e =>
                    set(
                        TryParseHex(e.Value, out int rgb)
                            ? rgb
                            : throw new FormatException("A colour is written #RRGGBB.")
                    )
                );

            private void Number(string group, string name, Action<double> set) =>
                Take(group, name, e => set(double.Parse(e.Value, NumberStyles.Float, CultureInfo.InvariantCulture)));

            private void Flag(string group, string name, Action<bool> set) =>
                Take(group, name, e => set(bool.Parse(e.Value)));

            private void Text(string group, string name, Action<string> set) => Take(group, name, e => set(e.Value));

            private void ReadLayers()
            {
                XElement list = _root.Element("layers");

                if (list == null)
                {
                    return;
                }

                var layers = new List<GlobalLayer>();

                foreach (XElement item in list.Elements())
                {
                    try
                    {
                        layers.Add(
                            new GlobalLayer(
                                item.Element("name")?.Value,
                                TryParseHex(item.Element("colour")?.Value, out int rgb)
                                    ? rgb
                                    : throw new FormatException("A colour is written #RRGGBB."),
                                item.Element("linetype")?.Value,
                                double.Parse(
                                    item.Element("lineweight")?.Value ?? "0",
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture
                                ),
                                !string.Equals(item.Element("plot")?.Value, "false", StringComparison.OrdinalIgnoreCase)
                            )
                        );
                    }
                    catch (Exception exception)
                        when (exception is ArgumentException
                            || exception is FormatException
                            || exception is OverflowException
                        )
                    {
                        GeometryHelperLog.Warn(
                            "GlobalSettings: a layer of "
                                + _path
                                + " is not valid ("
                                + exception.Message.Split('\n')[0].Trim()
                                + "); it is left out."
                        );
                    }
                }

                try
                {
                    _s.Layers = layers;
                }
                catch (ArgumentException exception)
                {
                    GeometryHelperLog.Warn(
                        "GlobalSettings: the layers of "
                            + _path
                            + " are not valid ("
                            + exception.Message.Split('\n')[0].Trim()
                            + "); the default table is used."
                    );
                }
            }

            private void ReadShortcuts()
            {
                XElement shortcuts = _root.Element("shortcuts");

                if (shortcuts == null)
                {
                    return;
                }

                try
                {
                    _s.SetShortcuts(
                        Pairs(shortcuts.Element("aliases")) ?? _s.Aliases,
                        Pairs(shortcuts.Element("keys")) ?? _s.Keys
                    );
                }
                catch (ArgumentException exception)
                {
                    GeometryHelperLog.Warn(
                        "GlobalSettings: the shortcuts of "
                            + _path
                            + " are not valid ("
                            + exception.Message.Split('\n')[0].Trim()
                            + "); AutoCAD's are used."
                    );
                }
            }

            /// <summary>The members of an object of the JSON, the name of each held in the item attribute where it is no XML name.</summary>
            private static IEnumerable<KeyValuePair<string, string>> Pairs(XElement element) =>
                element
                    ?.Elements()
                    .Select(e => new KeyValuePair<string, string>(
                        (string)e.Attribute("item") ?? e.Name.LocalName,
                        e.Value
                    ))
                    .ToList();
        }

        /// <summary>Writes JSON by hand, its members in the order written, two spaces of indent and LF line ends.</summary>
        private sealed class JsonText
        {
            private readonly StringBuilder _text = new StringBuilder();
            private readonly Stack<bool> _first = new Stack<bool>();

            public void Open(string name) => Begin(name, "{");

            public void OpenList(string name) => Begin(name, "[");

            public void Close() => End("}");

            public void CloseList() => End("]");

            public void Value(string name, string value) => Member(name, Quote(value));

            public void Value(string name, double value) =>
                Member(name, value.ToString("R", CultureInfo.InvariantCulture));

            public void Value(string name, int value) => Member(name, value.ToString(CultureInfo.InvariantCulture));

            public void Value(string name, bool value) => Member(name, value ? "true" : "false");

            public override string ToString() => _text.ToString() + "\n";

            private void Begin(string name, string bracket)
            {
                Member(name, bracket);
                _first.Push(true);
            }

            private void End(string bracket)
            {
                bool empty = _first.Pop();
                _text.Append(empty ? string.Empty : "\n" + new string(' ', 2 * _first.Count)).Append(bracket);
            }

            private void Member(string name, string value)
            {
                if (_first.Count > 0)
                {
                    _text.Append(_first.Peek() ? "\n" : ",\n").Append(' ', 2 * _first.Count);
                    _first.Pop();
                    _first.Push(false);
                }

                if (name != null)
                {
                    _text.Append(Quote(name)).Append(": ");
                }

                _text.Append(value);
            }

            private static string Quote(string text)
            {
                var quoted = new StringBuilder("\"");

                foreach (char c in text ?? string.Empty)
                {
                    switch (c)
                    {
                        case '"':
                            quoted.Append("\\\"");
                            break;
                        case '\\':
                            quoted.Append("\\\\");
                            break;
                        default:
                            if (c < ' ')
                            {
                                quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                quoted.Append(c);
                            }

                            break;
                    }
                }

                return quoted.Append('"').ToString();
            }
        }
    }
}
