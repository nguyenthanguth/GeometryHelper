using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A plotter of the Printer/plotter list: the built-in PDF and image writers, None (shown, not chosen), or a printer of Windows.</summary>
    public sealed class PlotDeviceRow
    {
        internal PlotDeviceRow(string name, string kind, bool isEnabled, bool startsGroup, IPlotPrinter printer = null)
        {
            Name = name;
            Kind = kind;
            IsEnabled = isEnabled;
            StartsGroup = startsGroup;
            Printer = printer;
        }

        /// <summary>Gets the printer of Windows this row is; null for a plotter of the presenter's own and None.</summary>
        public IPlotPrinter Printer { get; }

        /// <summary>Gets the name: GeometryHelper PDF, None, or the name Windows gives a printer.</summary>
        public string Name { get; }

        /// <summary>Gets what it is, shown after it: "built in", "Windows", "Windows · network".</summary>
        public string Kind { get; }

        /// <summary>Gets whether it can be chosen: every one but None, which is shown greyed.</summary>
        public bool IsEnabled { get; }

        /// <summary>Gets whether a line is drawn above it: the first printer of Windows.</summary>
        public bool StartsGroup { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>An entry of the What to plot list: the area and the hint shown after it, "every object" for Extents.</summary>
    public sealed class PlotAreaRow
    {
        internal PlotAreaRow(CadPlotArea area, string hint)
        {
            Area = area;
            Hint = hint;
        }

        /// <summary>Gets the area.</summary>
        public CadPlotArea Area { get; }

        /// <summary>Gets its name: Display, Extents, Limits, Window, Layout.</summary>
        public string Name => Area.ToString();

        /// <summary>Gets the hint shown after it.</summary>
        public string Hint { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD's Plot dialog in its expanded form, which PLOT (Ctrl+P) shows, and its Page Setup form, which PAGESETUP shows:
    /// the page setup, the plotter, the sheet, what to plot, the offset and the scale on the left; the plot style table, the
    /// shaded viewport options, the plot options and the orientation on the right. It edits a copy of the space's page setup
    /// (<see cref="Settings"/>); Apply to Layout writes it into the drawing as one undo step, OK plots it to a PDF or an image
    /// or prints it on a printer of Windows (or, in the Page Setup form, writes it), Add… keeps it as a named page setup, and
    /// Window&lt; closes the dialog for a window to be picked on the canvas.
    /// </summary>
    public sealed class PlotDialogViewModel : ObservableObject
    {
        private const string NoPageSetup = "<None>";
        private const string CustomScale = "Custom";

        private static readonly NumberFormatInfo Spaced = new NumberFormatInfo
        {
            NumberGroupSeparator = " ",
            NumberDecimalSeparator = ".",
        };

        private readonly CadPlotJob _job;
        private readonly List<CadPaper> _customSizes;
        private readonly List<IPlotPrinter> _printers;
        private readonly Dictionary<string, IReadOnlyList<CadPaper>> _printerPapers = new Dictionary<
            string,
            IReadOnlyList<CadPaper>
        >(StringComparer.OrdinalIgnoreCase);
        private int _copies = 1;
        private bool _plotToFile;
        private string _notice;
        private CadPaper _mmPaper;
        private CadPlotSettings _settings;
        private string _pageSetup = NoPageSetup;
        private string _offsetXText;
        private string _offsetYText;
        private string _paperUnitsText;
        private string _drawingUnitsText;
        private bool _saveChanges;

        /// <summary>Makes the dialog of a plot a command handed to the window.</summary>
        /// <param name="document">The document.</param>
        /// <param name="job">The space and its page setup; the dialog edits a copy of the settings.</param>
        /// <param name="isPageSetup">True for PAGESETUP's form: titled Page Setup, OK writing the page setup instead of plotting.</param>
        /// <param name="windowsPrinters">The printers of Windows to list after the presenter's own plotters, by name; null for none.</param>
        /// <param name="customPixelSizes">The sizes of an image of the user's own a raster device lists after those built in, the newest first; null for none.</param>
        /// <param name="editsPageSetup">
        /// The name of the page setup of the drawing this form edits, as the Page Setup Manager's Modify… and New… do: OK then
        /// writes it with <see cref="SaveToPageSetup"/> and not into the layout, and Window&lt; is not offered. Null for the
        /// layout's own.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the job is null.</exception>
        public PlotDialogViewModel(
            CadDocument document,
            CadPlotJob job,
            bool isPageSetup = false,
            IEnumerable<IPlotPrinter> windowsPrinters = null,
            IEnumerable<CadPaper> customPixelSizes = null,
            string editsPageSetup = null
        )
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            _job = job ?? throw new ArgumentNullException(nameof(job));
            IsPageSetup = isPageSetup;
            PageSetupName = editsPageSetup;
            _pageSetup = editsPageSetup ?? NoPageSetup;
            _printers = (windowsPrinters ?? Array.Empty<IPlotPrinter>())
                .Where(p => p != null)
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ToList();
            _settings = job.Settings.Clone();
            UseKnownDevice(_settings);
            _customSizes = (customPixelSizes ?? Array.Empty<CadPaper>())
                .Where(p => p != null && p.IsPixels && !p.IsCustomChoice)
                .ToList();

            var devices = new List<PlotDeviceRow>
            {
                new PlotDeviceRow("None", string.Empty, false, false),
                new PlotDeviceRow(CadPlotSettings.PdfDevice, "built in, vector", true, false),
                new PlotDeviceRow(CadPlotSettings.PngDevice, "built in, raster", true, false),
                new PlotDeviceRow(CadPlotSettings.JpgDevice, "built in, raster", true, false),
            };

            bool first = true;

            foreach (IPlotPrinter printer in _printers)
            {
                devices.Add(new PlotDeviceRow(printer.Name, PlotDeviceText.Hint(printer), true, first, printer));
                first = false;
            }

            Devices = devices;
            _mmPaper = _settings.Paper.IsPixels || _settings.IsPrinter ? CadPaper.Default : _settings.Paper;
            Papers = PapersOf(_settings);
            Areas = Space.IsModel
                ? new[]
                {
                    new PlotAreaRow(CadPlotArea.Display, "what the view shows"),
                    new PlotAreaRow(CadPlotArea.Extents, "every object"),
                    new PlotAreaRow(CadPlotArea.Limits, "LIMMIN to LIMMAX"),
                    new PlotAreaRow(CadPlotArea.Window, "two points"),
                }
                : new[]
                {
                    new PlotAreaRow(CadPlotArea.Display, "what the view shows"),
                    new PlotAreaRow(CadPlotArea.Extents, "every object"),
                    new PlotAreaRow(CadPlotArea.Layout, "the sheet"),
                    new PlotAreaRow(CadPlotArea.Window, "two points"),
                };

            if (Space.IsModel && _settings.Area == CadPlotArea.Layout)
            {
                _settings.Area = CadPlotArea.Extents;
            }

            Scales = CadPlotScale.ListFor(document.Database);
            ScaleNames = Scales.Select(s => s.Name).Concat(new[] { CustomScale }).ToList();
            PageSetups = PageSetupList();
            StyleTables = new[] { CadPlotStyleTable.Monochrome, CadPlotStyleTable.Grayscale, CadPlotStyleTable.None };
            Refresh();
        }

        /// <summary>
        /// Gets or sets how the window shows Custom Image Size, for the Custom… entry of a raster device's Paper size list:
        /// true where OK was pressed. Null for none, when Custom… does nothing.
        /// </summary>
        public Func<CustomImageSizeViewModel, bool> ShowCustomSize { get; set; }

        /// <summary>Raised when Custom Image Size made a size of the user's own, so that the window keeps it between sessions (<see cref="GlobalSettings.CustomPixelSizes"/>).</summary>
        public event EventHandler<CadPaper> CustomSizeMade;

        /// <summary>Gets whether the plotter is a raster device, GeometryHelper PNG or JPG: sizes in pixels, Plot to file always, the DPI grey.</summary>
        public bool IsRaster => _settings.IsRaster;

        /// <summary>Gets whether the plotter is a printer of Windows: the plot is printed on its paper, not written to a file.</summary>
        public bool IsPrinter => _settings.IsPrinter;

        /// <summary>Gets the printer of Windows chosen; null for a plotter of the presenter's own.</summary>
        internal IPlotPrinter SelectedPrinter => IsPrinter ? PrinterNamed(_settings.DeviceName) : null;

        /// <summary>
        /// Gets or sets whether Plot to file is ticked: always for the PDF writer and a raster device, which write a file. For a
        /// printer of Windows it is the user's to tick, unticked to begin with; ticked, OK says that a plot to file is not
        /// supported for this printer (<see cref="TryAccept"/>) and prints nothing.
        /// </summary>
        public bool PlotsToFile
        {
            get => !IsPrinter || _plotToFile;
            set
            {
                if (IsPrinter && Set(ref _plotToFile, value))
                {
                    Notice = null;
                }
            }
        }

        /// <summary>Gets whether the Plot to file box can be ticked: for a printer of Windows, as AutoCAD leaves it; the file writers always plot to a file.</summary>
        public bool IsPlotToFileEnabled => IsPrinter;

        /// <summary>Gets the words of the Plot to file box: plain for a printer, which may be ticked, and with its reason for a file writer, where it is always ticked.</summary>
        public string PlotToFileText =>
            IsPrinter ? "Plot to file"
            : IsRaster ? "Plot to file (always, for a raster device)"
            : "Plot to file (always, for PDF)";

        /// <summary>Gets what the dialog says in its footer in place of the drawing and space, for OK that was refused; null for nothing.</summary>
        public string Notice
        {
            get => _notice;
            private set
            {
                if (Set(ref _notice, value))
                {
                    Raise(nameof(HasNotice));
                    Raise(nameof(FooterText));
                }
            }
        }

        /// <summary>Gets whether there is a notice to show.</summary>
        public bool HasNotice => !string.IsNullOrEmpty(_notice);

        /// <summary>Gets the words of the footer: the notice where there is one, else the drawing and space (<see cref="Footer"/>).</summary>
        public string FooterText => HasNotice ? _notice : Footer;

        /// <summary>
        /// Asks whether OK can go on: it cannot where a printer of Windows is chosen with Plot to file ticked, a plot written
        /// through the printer's driver to a file not being supported, and the footer then says so (<see cref="Notice"/>). The Page Setup form, which plots nothing, is not held back.
        /// </summary>
        /// <returns>True when the dialog may close with OK.</returns>
        public bool TryAccept()
        {
            if (!IsPageSetup && IsPrinter && _plotToFile)
            {
                Notice = "Plot to file is not supported for this printer: untick it to print on paper.";
                return false;
            }

            Notice = null;
            return true;
        }

        /// <summary>Gets the page of the guide the Help button opens.</summary>
        public string HelpUrl => PlotHelp.PlotDialog;

        /// <summary>Gets whether the number of copies can be typed: for a printer of Windows alone.</summary>
        public bool IsCopiesEnabled => IsPrinter;

        /// <summary>Gets the number of copies printed: what was typed for a printer, 1 for a file.</summary>
        public int Copies => IsPrinter ? _copies : 1;

        /// <summary>Gets or sets the number of copies as typed; a number from 1 to 999 is taken, anything else is shown as the copies were.</summary>
        public string CopiesText
        {
            get => Copies.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (
                    IsPrinter
                    && int.TryParse(
                        (value ?? string.Empty).Trim(),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int copies
                    )
                    && copies >= 1
                    && copies <= 999
                )
                {
                    _copies = copies;
                }

                Raise(nameof(CopiesText));
            }
        }

        /// <summary>Gets the unit of the offsets: "mm", or "pixels" for a raster device.</summary>
        public string OffsetUnit => IsRaster ? "pixels" : "mm";

        /// <summary>Gets the unit of the paper side of the scale: "mm =", or "pixels =" for a raster device.</summary>
        public string ScaleUnit => IsRaster ? "pixels =" : "mm =";

        /// <summary>Gets what the DPI box shows: 300 for the PDF plotter, 96 for a raster device, whose size is in pixels.</summary>
        public string DpiText => IsRaster ? "96" : "300";

        /// <summary>Gets the words under the shaded viewport options.</summary>
        public string ShadeNote =>
            IsRaster
                ? "Grey for a raster device: the image size is set in pixels, not by DPI."
                : "2D only: kept grey, as AutoCAD shows them for 2D Wireframe.";

        /// <summary>Gets the words under the plot style table.</summary>
        public string StyleNote =>
            IsRaster
                ? "The image is drawn on white, as the paper; colour 7 plots black. None keeps the colours."
                : "Monochrome: every colour plots black (as monochrome.ctb). Grayscale: colours as greys. None: as displayed.";

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the space plotted.</summary>
        public CadSpace Space => _job.Space;

        /// <summary>Gets whether this is PAGESETUP's form.</summary>
        public bool IsPageSetup { get; }

        /// <summary>Gets the page setup as the dialog has it, a copy apart from the drawing's.</summary>
        public CadPlotSettings Settings => _settings;

        /// <summary>Gets the name of the page setup of the drawing this form edits (the Page Setup Manager's Modify… and New…); null for the layout's own.</summary>
        public string PageSetupName { get; }

        /// <summary>
        /// Gets or sets whether the Page Setup Manager opened this form, as Modify… and New… do: Window&lt; and Add… are then
        /// greyed, there being no canvas to pick on and the page setup being kept by OK. Set by the manager before it is shown.
        /// </summary>
        public bool OpenedFromManager { get; set; }

        /// <summary>Gets whether Window&lt; can be pressed: not while a named page setup is edited, which has no canvas to pick on.</summary>
        public bool CanPickWindow => PageSetupName == null && !OpenedFromManager;

        /// <summary>Gets whether Add… is offered: not while a named page setup is edited, which is kept by OK.</summary>
        public bool CanAddPageSetup => PageSetupName == null && !OpenedFromManager;

        /// <summary>
        /// Gets or sets how the window asks the name of a new page setup (Add Page Setup): true where OK was pressed. Null for
        /// none, when <see cref="AskAndAddPageSetup"/> does nothing.
        /// </summary>
        public Func<PageSetupNameViewModel, bool> ShowPageSetupName { get; set; }

        /// <summary>Gets the title: "Plot - Model", or "Page Setup - Model" in PAGESETUP's form.</summary>
        public string Title => (IsPageSetup ? "Page Setup - " : "Plot - ") + Space.Name;

        /// <summary>Gets the words of the footer: the drawing's file and the space, "plan-level2.dxf · Model".</summary>
        public string Footer => Document.Title + " · " + Space.Name;

        /// <summary>Gets the page setups to choose from: &lt;None&gt;, then the drawing's by name.</summary>
        public IReadOnlyList<string> PageSetups { get; private set; }

        /// <summary>Gets or sets the page setup chosen; one of the drawing's sets the dialog to it.</summary>
        public string PageSetup
        {
            get => _pageSetup;
            set
            {
                if (value == null || !Set(ref _pageSetup, value) || value == NoPageSetup)
                {
                    return;
                }

                CadPlotSettings named = CadPlotSettings.ReadPageSetup(Document, Space, value);

                if (named != null)
                {
                    UseKnownDevice(named);

                    // The plot stamp and the SHX text are the session's, not the page setup's: they stay.
                    named.PlotStamp = _settings.PlotStamp;
                    named.PdfShx = _settings.PdfShx;
                    _settings = named;
                    RefreshDevice();
                }
            }
        }

        /// <summary>
        /// Gets the plotters of the Printer/plotter list: None greyed, GeometryHelper PDF, PNG and JPG, then the printers of
        /// Windows, to choose from.
        /// </summary>
        public IReadOnlyList<PlotDeviceRow> Devices { get; }

        /// <summary>
        /// Gets or sets the plotter chosen, GeometryHelper PDF, PNG or JPG, or a printer of Windows; None is greyed and not
        /// taken. A raster device starts on 1920 by 1080 pixels, fitted and centred, with no plot style table; going back to the
        /// PDF one gives the sheet it had. A printer takes the sheets it lists, the one of the size the plot was on where it has
        /// it, else its first.
        /// </summary>
        public PlotDeviceRow Device
        {
            get => Devices.First(d => string.Equals(d.Name, _settings.DeviceName, StringComparison.OrdinalIgnoreCase));
            set
            {
                if (
                    value != null
                    && value.IsEnabled
                    && !string.Equals(value.Name, _settings.DeviceName, StringComparison.OrdinalIgnoreCase)
                )
                {
                    bool wasFile = !_settings.IsPrinter;
                    bool wasRaster = _settings.IsRaster;
                    _plotToFile = false;
                    Notice = null;

                    if (value.Printer != null)
                    {
                        CadPaper wanted = _settings.Paper.IsPixels ? _mmPaper : _settings.Paper;
                        _settings.UsePrinter(value.Name);
                        _settings.Paper = ClosestPaper(PrinterPapers(value.Printer), wanted);
                    }
                    else
                    {
                        bool wasPrinter = _settings.IsPrinter;
                        _settings.UseDevice(value.Name);

                        if ((wasRaster || wasPrinter) && !_settings.IsRaster)
                        {
                            _settings.Paper = _mmPaper;
                        }
                    }

                    if (wasFile && value.Printer != null && !_settings.Paper.IsPixels)
                    {
                        _copies = 1;
                    }

                    RefreshDevice();
                }
                else
                {
                    Raise(nameof(Device));
                }
            }
        }

        /// <summary>Gets what the plotter is: "GeometryHelper PDF · vector, built in", "GeometryHelper PNG · raster, built in".</summary>
        public string PlotterText => PlotDeviceText.Plotter(_settings.DeviceName, IsRaster, IsPrinter);

        /// <summary>
        /// Gets where it writes: File, or for a printer of Windows its port or share where it says
        /// (<c>USB001</c>, <c>\\office-srv\Office printer</c>), else Printer, or Network printer when it is on the network.
        /// </summary>
        public string WhereText =>
            IsPrinter ? (SelectedPrinter == null ? "Printer" : PlotDeviceText.Where(SelectedPrinter)) : "File";

        /// <summary>Gets what it does.</summary>
        public string DescriptionText => PlotDeviceText.Description(_settings.DeviceName, IsPrinter);

        /// <summary>
        /// Gets the entries of the Paper size list: the ISO and ANSI sheets for the PDF plotter; for a raster device the sizes
        /// in pixels built in, those of the user's own, and Custom… last (<see cref="CadPaper.CustomPixelsChoice"/>).
        /// </summary>
        public IReadOnlyList<CadPaper> Papers { get; private set; }

        /// <summary>
        /// Gets or sets the sheet chosen. Custom… shows Custom Image Size (<see cref="ShowCustomSize"/>): OK adds the size to
        /// the list, chooses it and raises <see cref="CustomSizeMade"/>; Cancel keeps the size chosen before.
        /// </summary>
        public CadPaper Paper
        {
            get => Papers.FirstOrDefault(p => p.IsSameAs(_settings.Paper)) ?? _settings.Paper;
            set
            {
                if (value == null || value.IsSameAs(_settings.Paper))
                {
                    return;
                }

                if (value.IsCustomChoice)
                {
                    var custom = new CustomImageSizeViewModel(_settings.Paper);

                    if (ShowCustomSize != null && ShowCustomSize(custom) && custom.CanOk)
                    {
                        CadPaper made = custom.Size;

                        if (!CadPaper.PixelSizes.Contains(made))
                        {
                            _customSizes.RemoveAll(p => p.IsSameAs(made));
                            _customSizes.Insert(0, made);
                            CustomSizeMade?.Invoke(this, made);
                        }

                        _settings.Paper = made;
                        Papers = PapersOf(_settings);
                        Raise(nameof(Papers));
                    }

                    Refresh();
                    return;
                }

                _settings.Paper = value;

                if (!value.IsPixels && !_settings.IsPrinter)
                {
                    _mmPaper = value;
                }

                Refresh();
            }
        }

        /// <summary>Gets the width of the sheet as it lies, over the small picture of it: "420 MM", or for an image "1920 PIXELS".</summary>
        public string SheetWidthText => Whole(Page.SheetWidth) + (IsRaster ? " PIXELS" : " MM");

        /// <summary>Gets the height of the sheet as it lies, beside the picture: "297".</summary>
        public string SheetHeightText => Whole(Page.SheetHeight);

        /// <summary>Gets the width of the small picture of the sheet, in pixels: the sheet's shape within 92 by 66.</summary>
        public double SheetPictureWidth => PictureScale * Page.SheetWidth;

        /// <summary>Gets the height of the small picture of the sheet, in pixels.</summary>
        public double SheetPictureHeight => PictureScale * Page.SheetHeight;

        /// <summary>Gets the printable area inside the picture, as the margins in pixels: left, top, right, bottom.</summary>
        public System.Windows.Thickness SheetPictureMargins
        {
            get
            {
                CadPlotPage page = Page;
                double k = PictureScale;
                return new System.Windows.Thickness(
                    page.Printable.MinX * k,
                    (page.SheetHeight - page.Printable.MaxY) * k,
                    (page.SheetWidth - page.Printable.MaxX) * k,
                    page.Printable.MinY * k
                );
            }
        }

        /// <summary>Gets the entries of the What to plot list: Display, Extents, Limits and Window, Layout in place of Limits in a layout.</summary>
        public IReadOnlyList<PlotAreaRow> Areas { get; }

        /// <summary>Gets or sets the entry chosen.</summary>
        public PlotAreaRow Area
        {
            get => Areas.FirstOrDefault(a => a.Area == _settings.Area) ?? Areas[0];
            set
            {
                if (value != null && value.Area != _settings.Area)
                {
                    _settings.Area = value.Area;
                    Refresh();
                }
            }
        }

        /// <summary>Gets whether the area is a window: the Window&lt; button and its corners are shown.</summary>
        public bool IsWindow => _settings.Area == CadPlotArea.Window;

        /// <summary>Gets the corners of the window, as "From (120, 60) to (7 620, 5 360)"; empty where none is picked.</summary>
        public string WindowText
        {
            get
            {
                DisplayBox w = _settings.Window;
                return w.IsEmpty
                    ? string.Empty
                    : "From ("
                        + Spaced0(w.MinX)
                        + ", "
                        + Spaced0(w.MinY)
                        + ") to ("
                        + Spaced0(w.MaxX)
                        + ", "
                        + Spaced0(w.MaxY)
                        + ")";
            }
        }

        /// <summary>
        /// Gets or sets whether the dialog closes for a window to be picked on the canvas, as Window&lt; does: the window then
        /// passes the settings back to the command, which asks the two corners and shows the dialog again.
        /// </summary>
        public bool PicksWindow { get; set; }

        /// <summary>Gets or sets whether the plot is centred on the printable area.</summary>
        public bool Centered
        {
            get => _settings.Centered && _settings.Area != CadPlotArea.Layout;
            set
            {
                _settings.Centered = value;
                Refresh();
            }
        }

        /// <summary>Gets whether Center the plot can be ticked: not for a layout plotted as its sheet.</summary>
        public bool IsCenterEnabled => _settings.Area != CadPlotArea.Layout;

        /// <summary>Gets whether the offsets are typed: where the plot is not centred.</summary>
        public bool IsOffsetEnabled => !Centered;

        /// <summary>Gets or sets the offset to the right, in millimetres, "0.00"; the one centring gives where the plot is centred.</summary>
        public string OffsetXText
        {
            get => _offsetXText;
            set
            {
                _offsetXText = value;

                if (TryNumber(value, out double x))
                {
                    _settings.OffsetX = x;
                    RefreshPicture();
                }

                Raise(nameof(OffsetXText));
            }
        }

        /// <summary>Gets or sets the offset upward, in millimetres.</summary>
        public string OffsetYText
        {
            get => _offsetYText;
            set
            {
                _offsetYText = value;

                if (TryNumber(value, out double y))
                {
                    _settings.OffsetY = y;
                    RefreshPicture();
                }

                Raise(nameof(OffsetYText));
            }
        }

        /// <summary>Gets or sets whether the plot fills the printable area.</summary>
        public bool FitToPaper
        {
            get => _settings.FitToPaper;
            set
            {
                _settings.FitToPaper = value;
                Refresh();
            }
        }

        /// <summary>Gets whether a scale is chosen or typed: where the plot is not fitted.</summary>
        public bool IsScaleEnabled => !_settings.FitToPaper;

        /// <summary>Gets the scales of the list: 1:1 to 1:1000, the drawing's own, then Custom.</summary>
        public IReadOnlyList<CadPlotScale> Scales { get; }

        /// <summary>Gets the names the Scale list shows.</summary>
        public IReadOnlyList<string> ScaleNames { get; }

        /// <summary>Gets or sets the scale chosen by its name; Custom where the units typed are none of the list's.</summary>
        public string ScaleName
        {
            get
            {
                double ratio = _settings.PaperUnits / _settings.DrawingUnits;
                CadPlotScale scale = Scales.FirstOrDefault(s =>
                    Math.Abs(s.PaperUnits / s.DrawingUnits - ratio) <= 1E-9 * ratio
                );
                return _settings.FitToPaper || scale == null ? CustomScale : scale.Name;
            }
            set
            {
                CadPlotScale scale = Scales.FirstOrDefault(s => s.Name == value);

                if (scale != null)
                {
                    _settings.PaperUnits = scale.PaperUnits;
                    _settings.DrawingUnits = scale.DrawingUnits;
                    Refresh();
                }
            }
        }

        /// <summary>Gets or sets the millimetres of the scale, "1"; that of the fitted scale where the plot is fitted.</summary>
        public string PaperUnitsText
        {
            get => _paperUnitsText;
            set
            {
                _paperUnitsText = value;

                if (TryNumber(value, out double units) && units > 0.0)
                {
                    _settings.PaperUnits = units;
                    RefreshScale();
                }

                Raise(nameof(PaperUnitsText));
            }
        }

        /// <summary>Gets or sets the drawing units of the scale, "20"; those of the fitted scale where the plot is fitted, as "18.47".</summary>
        public string DrawingUnitsText
        {
            get => _drawingUnitsText;
            set
            {
                _drawingUnitsText = value;

                if (TryNumber(value, out double units) && units > 0.0)
                {
                    _settings.DrawingUnits = units;
                    RefreshScale();
                }

                Raise(nameof(DrawingUnitsText));
            }
        }

        /// <summary>Gets or sets whether the lineweights are scaled with the plot.</summary>
        public bool ScaleLineweights
        {
            get => _settings.ScaleLineweights;
            set
            {
                _settings.ScaleLineweights = value;
                Raise(nameof(ScaleLineweights));
            }
        }

        /// <summary>Gets the plot style tables: Monochrome, Grayscale, None.</summary>
        public IReadOnlyList<CadPlotStyleTable> StyleTables { get; }

        /// <summary>Gets or sets the plot style table.</summary>
        public CadPlotStyleTable StyleTable
        {
            get => _settings.StyleTable;
            set
            {
                _settings.StyleTable = value;
                Raise(nameof(StyleTable));
            }
        }

        /// <summary>Gets or sets whether the lines plot at their lineweights (Plot object lineweights).</summary>
        public bool PlotLineweights
        {
            get => _settings.PlotLineweights;
            set
            {
                _settings.PlotLineweights = value;
                Raise(nameof(PlotLineweights));
            }
        }

        /// <summary>Gets or sets whether transparency is plotted; the plot is the same either way, the display lists holding none.</summary>
        public bool PlotTransparency
        {
            get => _settings.PlotTransparency;
            set
            {
                _settings.PlotTransparency = value;
                Raise(nameof(PlotTransparency));
            }
        }

        /// <summary>Gets or sets whether the plot style table is used (Plot with plot styles).</summary>
        public bool PlotWithPlotStyles
        {
            get => _settings.PlotWithPlotStyles;
            set
            {
                _settings.PlotWithPlotStyles = value;
                Raise(nameof(PlotWithPlotStyles));
            }
        }

        /// <summary>Gets or sets whether OK writes the page setup into the layout too (Save changes to layout).</summary>
        public bool SaveChangesToLayout
        {
            get => _saveChanges;
            set => Set(ref _saveChanges, value);
        }

        /// <summary>Gets or sets whether the sheet lies on its side.</summary>
        public bool Landscape
        {
            get => _settings.Landscape;
            set
            {
                if (value != _settings.Landscape)
                {
                    _settings.Landscape = value;
                    Refresh();
                }
            }
        }

        /// <summary>Gets or sets whether the sheet stands upright.</summary>
        public bool Portrait
        {
            get => !_settings.Landscape;
            set => Landscape = !value;
        }

        /// <summary>Gets or sets whether the plot is turned half round.</summary>
        public bool UpsideDown
        {
            get => _settings.UpsideDown;
            set
            {
                _settings.UpsideDown = value;
                Refresh();
            }
        }

        /// <summary>Gets the turn of the letter A beside the orientation: 0 for landscape, 90 upright, 180 more upside down.</summary>
        public double OrientationAngle => (_settings.Landscape ? 0.0 : 90.0) + (_settings.UpsideDown ? 180.0 : 0.0);

        /// <summary>Gets the plot as the dialog lays it on its sheet now.</summary>
        public CadPlotPage Page => CadPlotPage.Compute(Document, Space, _settings, _job.Display);

        /// <summary>Gets whether there is something to plot.</summary>
        public bool CanPlot => !Page.IsEmpty;

        /// <summary>Gets the file a plot is offered as: the drawing's name and the space's, "plan-level2-Model.pdf".</summary>
        public string SuggestedFileName
        {
            get
            {
                string extension =
                    string.Equals(_settings.DeviceName, CadPlotSettings.PngDevice, StringComparison.OrdinalIgnoreCase)
                        ? ".png"
                    : string.Equals(_settings.DeviceName, CadPlotSettings.JpgDevice, StringComparison.OrdinalIgnoreCase)
                        ? ".jpg"
                    : ".pdf";
                string name = System.IO.Path.GetFileNameWithoutExtension(Document.Title) + "-" + Space.Name + extension;

                foreach (char bad in System.IO.Path.GetInvalidFileNameChars())
                {
                    name = name.Replace(bad, '_');
                }

                return name;
            }
        }

        /// <summary>Gets the folder a plot is offered in: the drawing's, none for a drawing of no file.</summary>
        public string SuggestedFolder => Document.HasFile ? System.IO.Path.GetDirectoryName(Document.FileName) : null;

        /// <summary>
        /// Writes the page setup into the space's layout as Apply to Layout does: one undo step named PLOT, the layout's sheet
        /// moved with it.
        /// </summary>
        /// <returns>What came of the edit.</returns>
        public CadEditResult ApplyToLayout() => _settings.ApplyTo(Document, Space);

        /// <summary>
        /// Writes the page setup as it is into the named page setup this form edits (<see cref="PageSetupName"/>), one undo step
        /// named PAGESETUP: what OK does in the Page Setup Manager's Modify… and New….
        /// </summary>
        /// <returns>What came of the edit; refused where this form edits the layout's own page setup.</returns>
        public CadEditResult SaveToPageSetup() =>
            PageSetupName == null
                ? CadEditResult.Refuse("This page setup is the layout's own: it is applied to the layout.")
                : _settings.SaveAsPageSetup(Document, PageSetupName);

        /// <summary>
        /// Keeps the page setup as the dialog has it as a named page setup of the drawing, as Add… does, one undo step named
        /// PAGESETUP; the name is then the one chosen in the Page setup list. A name the drawing has is replaced.
        /// </summary>
        /// <param name="name">The name, trimmed.</param>
        /// <returns>What came of the edit; refused where the name is not a good one.</returns>
        public CadEditResult AddPageSetup(string name)
        {
            CadEditResult result = _settings.SaveAsPageSetup(Document, name);

            if (result.Succeeded)
            {
                PageSetups = PageSetupList();
                _pageSetup = CadPlotSettings
                    .PageSetupNames(Document)
                    .First(n => string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase));
                Raise(nameof(PageSetups));
                Raise(nameof(PageSetup));
            }

            return result;
        }

        /// <summary>
        /// Asks the name of a new page setup (<see cref="ShowPageSetupName"/>) and keeps the page setup as the dialog has it
        /// under it, as Add… does.
        /// </summary>
        /// <returns>What came of the edit; null where no name was given or the window does not ask.</returns>
        public CadEditResult AskAndAddPageSetup()
        {
            if (ShowPageSetupName == null || !CanAddPageSetup)
            {
                return null;
            }

            var asked = new PageSetupNameViewModel("Add Page Setup", CadPlotSettings.PageSetupNames(Document));

            if (AskImportFile != null && ShowImport != null)
            {
                asked.ImportPageSetups = ImportPageSetups;
            }

            return ShowPageSetupName(asked) && asked.CanOk ? AddPageSetup(asked.Name) : null;
        }

        /// <summary>
        /// Gets or sets how the window asks the drawing Import… of Add Page Setup copies page setups from, as its Select File
        /// dialog: the path, or null where it was cancelled. Null for none, when Import… is not offered.
        /// </summary>
        public Func<string> AskImportFile { get; set; }

        /// <summary>Gets or sets how the window shows Import Page Setups: true where OK was pressed. Null for none, when Import… is not offered.</summary>
        public Func<ImportPageSetupsViewModel, bool> ShowImport { get; set; }

        /// <summary>
        /// Import… of Add Page Setup: copies page setups of another drawing in, as the Page Setup Manager's Import… does, and
        /// lists them in the Page setup list.
        /// </summary>
        /// <returns>The names the drawing has now, for the name box to refuse; null where nothing was imported.</returns>
        private IEnumerable<string> ImportPageSetups()
        {
            var manager = new PageSetupManagerViewModel(Document, Space, _printers)
            {
                AskImportFile = AskImportFile,
                ShowImport = ShowImport,
            };
            CadEditResult result = manager.Import();

            if (result == null || !result.Succeeded)
            {
                return null;
            }

            PageSetups = PageSetupList();
            Raise(nameof(PageSetups));
            Raise(nameof(PageSetup));
            return CadPlotSettings.PageSetupNames(Document);
        }

        /// <summary>
        /// Prints the plot on the printer of Windows chosen, as OK does for one: the page drawn on its sheet and handed to the
        /// printer with the paper, the turn and the copies; the page setup written into the layout first where Save changes
        /// to layout is ticked. No file is written.
        /// </summary>
        /// <returns>The plot as it was laid on its sheet.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no printer of Windows is chosen, Plot to file is ticked for it (not supported), there is nothing to plot, or the printer cannot take the job.</exception>
        public CadPlotPage Print()
        {
            IPlotPrinter printer = SelectedPrinter
                ?? throw new InvalidOperationException("No printer of Windows is chosen.");

            if (_plotToFile)
            {
                throw new InvalidOperationException("Plot to file is not supported for this printer.");
            }

            if (_saveChanges)
            {
                ApplyToLayout();
            }

            DrawingVisual visual = CadPrintWriter.CreatePage(
                Document,
                Space,
                _settings,
                _job.Display,
                out CadPlotPage page
            );
            printer.Print(
                new PlotPrintJob(
                    Document.Title + " - " + Space.Name,
                    _settings.Paper,
                    page.SheetWidth,
                    page.SheetHeight,
                    Copies,
                    visual
                )
            );
            return page;
        }

        /// <summary>
        /// Plots as OK does, to a PDF or, with a raster device, to a PNG or a JPEG of the size in pixels: the file written,
        /// and the page setup written into the layout first where Save changes to layout is ticked.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>The plot as it was laid on its sheet.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when there is nothing to plot.</exception>
        public CadPlotPage Plot(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (_saveChanges)
            {
                ApplyToLayout();
            }

            return Write(path, Document, Space, _settings, _job.Display);
        }

        /// <summary>A plot written by the writer of its device: the image writer for GeometryHelper PNG and JPG, else the PDF writer.</summary>
        internal static CadPlotPage Write(
            string path,
            CadDocument document,
            CadSpace space,
            CadPlotSettings settings,
            DisplayBox display
        ) =>
            settings.IsRaster
                ? CadRasterWriter.WritePlot(path, document, space, settings, display)
                : CadPdfWriter.Write(path, document, space, settings, display);

        /// <summary>Makes the preview of the plot as the dialog has it, as Preview… shows it.</summary>
        /// <returns>The preview.</returns>
        public PlotPreviewViewModel CreatePreview() =>
            new PlotPreviewViewModel(Document, Space, _settings, _job.Display);

        /// <summary>Hands the settings back to the command where the dialog closed to have a window picked, or with FILEDIA 0 for the command to ask the file.</summary>
        internal void HandBack(bool asksFile = false)
        {
            _job.Settings = _settings.Clone();
            _job.PicksWindow = PicksWindow;
            _job.AsksFile = asksFile;
        }

        /// <summary>The Page setup list: &lt;None&gt;, then the drawing's by name.</summary>
        private IReadOnlyList<string> PageSetupList() =>
            new[] { NoPageSetup }.Concat(CadPlotSettings.PageSetupNames(Document)).ToList();

        /// <summary>The printer of Windows of a name, letters of either case the same; null for none, or a plotter of the presenter's own.</summary>
        private IPlotPrinter PrinterNamed(string name) =>
            string.IsNullOrEmpty(name)
                ? null
                : _printers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>The sheets a printer lists, asked once; the PDF plotter's where it lists none or cannot say.</summary>
        private IReadOnlyList<CadPaper> PrinterPapers(IPlotPrinter printer)
        {
            if (printer == null)
            {
                return CadPaper.Standard;
            }

            if (!_printerPapers.TryGetValue(printer.Name, out IReadOnlyList<CadPaper> papers))
            {
                try
                {
                    papers = printer.GetPapers() ?? Array.Empty<CadPaper>();
                }
                catch (Exception exception)
                {
                    GeometryHelperLog.Warn(
                        "PlotDialogViewModel: the sheets of " + printer.Name + " could not be listed.",
                        exception
                    );
                    papers = Array.Empty<CadPaper>();
                }

                _printerPapers[printer.Name] = papers.Count == 0 ? CadPaper.Standard : papers;
            }

            return _printerPapers[printer.Name];
        }

        /// <summary>The sheet of a list that is the size of a wanted one in either turn, to a millimetre; else the list's first.</summary>
        private static CadPaper ClosestPaper(IReadOnlyList<CadPaper> papers, CadPaper wanted)
        {
            foreach (CadPaper paper in papers)
            {
                bool sameWay =
                    Math.Abs(paper.Width - wanted.Width) <= 1.0 && Math.Abs(paper.Height - wanted.Height) <= 1.0;
                bool turned =
                    Math.Abs(paper.Width - wanted.Height) <= 1.0 && Math.Abs(paper.Height - wanted.Width) <= 1.0;

                if (sameWay || turned)
                {
                    return paper;
                }
            }

            return papers[0];
        }

        /// <summary>
        /// Settings made to use a plotter this dialog has: the printer of Windows they were read with (or already use) where
        /// the computer has it, on a sheet of its list; else the presenter's own plotter, a raster device as it was and the
        /// PDF one for anything else.
        /// </summary>
        private void UseKnownDevice(CadPlotSettings settings)
        {
            IPlotPrinter printer = PrinterNamed(
                settings.IsPrinter ? settings.DeviceName
                : settings.IsRaster ? null
                : settings.SavedPlotter
            );

            if (printer != null)
            {
                CadPaper wanted = settings.Paper.IsPixels ? CadPaper.Default : settings.Paper;
                settings.UsePrinter(printer.Name);
                settings.Paper = ClosestPaper(PrinterPapers(printer), wanted);
            }
            else
            {
                settings.UseDevice(settings.IsRaster ? settings.DeviceName : CadPlotSettings.PdfDevice);
            }
        }

        private double PictureScale
        {
            get
            {
                CadPlotPage page = Page;
                return Math.Min(92.0 / page.SheetWidth, 66.0 / page.SheetHeight);
            }
        }

        /// <summary>The entries of the Paper size list for the settings' device.</summary>
        private IReadOnlyList<CadPaper> PapersOf(CadPlotSettings settings)
        {
            var papers = new List<CadPaper>(
                settings.IsRaster ? CadPaper.PixelSizes.Concat(_customSizes)
                : settings.IsPrinter ? PrinterPapers(PrinterNamed(settings.DeviceName))
                : CadPaper.Standard
            );

            if (!papers.Any(p => p.IsSameAs(settings.Paper)))
            {
                papers.Add(settings.Paper);
            }

            if (settings.IsRaster)
            {
                papers.Add(CadPaper.CustomPixelsChoice);
            }

            return papers;
        }

        /// <summary>The plotter changed: the Paper size list made again for it, then every field.</summary>
        private void RefreshDevice()
        {
            Papers = PapersOf(_settings);
            Raise(nameof(Papers));

            foreach (
                string name in new[]
                {
                    nameof(Device),
                    nameof(IsRaster),
                    nameof(IsPrinter),
                    nameof(PlotsToFile),
                    nameof(IsPlotToFileEnabled),
                    nameof(PlotToFileText),
                    nameof(IsCopiesEnabled),
                    nameof(CopiesText),
                    nameof(WhereText),
                    nameof(PlotterText),
                    nameof(DescriptionText),
                    nameof(OffsetUnit),
                    nameof(ScaleUnit),
                    nameof(DpiText),
                    nameof(ShadeNote),
                    nameof(StyleNote),
                    nameof(SuggestedFileName),
                }
            )
            {
                Raise(name);
            }

            Refresh();
        }

        /// <summary>Every field read again from the settings, the offsets and a fitted scale as the sheet now gives them.</summary>
        private void Refresh()
        {
            RefreshScale();
            CadPlotPage page = Page;
            double x = Centered ? page.OffsetX : _settings.OffsetX;
            double y = Centered ? page.OffsetY : _settings.OffsetY;
            _offsetXText = x.ToString("0.00", CultureInfo.InvariantCulture);
            _offsetYText = y.ToString("0.00", CultureInfo.InvariantCulture);

            foreach (
                string name in new[]
                {
                    nameof(Paper),
                    nameof(Area),
                    nameof(IsWindow),
                    nameof(WindowText),
                    nameof(Centered),
                    nameof(IsCenterEnabled),
                    nameof(IsOffsetEnabled),
                    nameof(OffsetXText),
                    nameof(OffsetYText),
                    nameof(FitToPaper),
                    nameof(StyleTable),
                    nameof(PlotLineweights),
                    nameof(PlotTransparency),
                    nameof(PlotWithPlotStyles),
                    nameof(Landscape),
                    nameof(Portrait),
                    nameof(UpsideDown),
                    nameof(OrientationAngle),
                    nameof(ScaleLineweights),
                }
            )
            {
                Raise(name);
            }

            RefreshPicture();
        }

        private void RefreshScale()
        {
            if (_settings.FitToPaper)
            {
                CadPlotPage page = Page;
                _paperUnitsText = "1";
                _drawingUnitsText = page.IsEmpty
                    ? string.Empty
                    : (1.0 / page.Scale).ToString("0.##", CultureInfo.InvariantCulture);
            }
            else
            {
                _paperUnitsText = _settings.PaperUnits.ToString("0.####", CultureInfo.InvariantCulture);
                _drawingUnitsText = _settings.DrawingUnits.ToString("0.####", CultureInfo.InvariantCulture);
            }

            Raise(nameof(PaperUnitsText));
            Raise(nameof(DrawingUnitsText));
            Raise(nameof(ScaleName));
            Raise(nameof(IsScaleEnabled));
            RefreshPicture();
        }

        private void RefreshPicture()
        {
            Raise(nameof(SheetWidthText));
            Raise(nameof(SheetHeightText));
            Raise(nameof(SheetPictureWidth));
            Raise(nameof(SheetPictureHeight));
            Raise(nameof(SheetPictureMargins));
            Raise(nameof(CanPlot));
        }

        private static bool TryNumber(string text, out double value) =>
            double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);

        private static string Whole(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Spaced0(double value) => value.ToString("#,0.##", Spaced);
    }
}
