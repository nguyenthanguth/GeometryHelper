using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A plotter of the Printer/plotter list: the built-in PDF writer, None, or a printer of Windows, shown and not chosen.</summary>
    public sealed class PlotDeviceRow
    {
        internal PlotDeviceRow(string name, string kind, bool isEnabled, bool startsGroup)
        {
            Name = name;
            Kind = kind;
            IsEnabled = isEnabled;
            StartsGroup = startsGroup;
        }

        /// <summary>Gets the name: GeometryHelper PDF, None, or the name Windows gives a printer.</summary>
        public string Name { get; }

        /// <summary>Gets what it is, shown after it: "built in", "Windows", "Windows · network".</summary>
        public string Kind { get; }

        /// <summary>Gets whether it can be chosen: the PDF writer alone; None and the printers of Windows are shown greyed.</summary>
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
    /// (<see cref="Settings"/>); Apply to Layout writes it into the drawing as one undo step, OK plots it to a PDF (or, in the
    /// Page Setup form, writes it), and Window&lt; closes the dialog for a window to be picked on the canvas.
    /// </summary>
    public sealed class PlotDialogViewModel : ObservableObject
    {
        private const string NoPageSetup = "<None>";
        private const string CustomScale = "Custom";

        private static readonly NumberFormatInfo Spaced = new NumberFormatInfo { NumberGroupSeparator = " ", NumberDecimalSeparator = "." };

        private readonly CadPlotJob _job;
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
        /// <param name="windowsPrinters">The printers of Windows to list, greyed, each its name and whether it is on the network; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the job is null.</exception>
        public PlotDialogViewModel(CadDocument document, CadPlotJob job, bool isPageSetup = false, IEnumerable<KeyValuePair<string, bool>> windowsPrinters = null)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            _job = job ?? throw new ArgumentNullException(nameof(job));
            IsPageSetup = isPageSetup;
            _settings = job.Settings.Clone();
            _settings.DeviceName = CadPlotSettings.PdfDevice;

            var devices = new List<PlotDeviceRow>
            {
                new PlotDeviceRow("None", string.Empty, false, false),
                new PlotDeviceRow(CadPlotSettings.PdfDevice, "built in", true, false),
            };

            bool first = true;

            foreach (KeyValuePair<string, bool> printer in (windowsPrinters ?? Array.Empty<KeyValuePair<string, bool>>()).OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                devices.Add(new PlotDeviceRow(printer.Key, printer.Value ? "Windows · network" : "Windows", false, first));
                first = false;
            }

            Devices = devices;
            Device = devices[1];

            var papers = new List<CadPaper>(CadPaper.Standard);

            if (!papers.Any(p => p.IsSameAs(_settings.Paper)))
            {
                papers.Add(_settings.Paper);
            }

            Papers = papers;
            Areas = Space.IsModel
                ? new[] { new PlotAreaRow(CadPlotArea.Display, "what the view shows"), new PlotAreaRow(CadPlotArea.Extents, "every object"), new PlotAreaRow(CadPlotArea.Limits, "LIMMIN to LIMMAX"), new PlotAreaRow(CadPlotArea.Window, "two points") }
                : new[] { new PlotAreaRow(CadPlotArea.Display, "what the view shows"), new PlotAreaRow(CadPlotArea.Extents, "every object"), new PlotAreaRow(CadPlotArea.Layout, "the sheet"), new PlotAreaRow(CadPlotArea.Window, "two points") };

            if (Space.IsModel && _settings.Area == CadPlotArea.Layout)
            {
                _settings.Area = CadPlotArea.Extents;
            }

            Scales = CadPlotScale.ListFor(document.Database);
            ScaleNames = Scales.Select(s => s.Name).Concat(new[] { CustomScale }).ToList();
            PageSetups = new[] { NoPageSetup }.Concat(CadPlotSettings.PageSetupNames(document)).ToList();
            StyleTables = new[] { CadPlotStyleTable.Monochrome, CadPlotStyleTable.Grayscale, CadPlotStyleTable.None };
            Refresh();
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the space plotted.</summary>
        public CadSpace Space => _job.Space;

        /// <summary>Gets whether this is PAGESETUP's form.</summary>
        public bool IsPageSetup { get; }

        /// <summary>Gets the page setup as the dialog has it, a copy apart from the drawing's.</summary>
        public CadPlotSettings Settings => _settings;

        /// <summary>Gets the title: "Plot - Model", or "Page Setup - Model" in PAGESETUP's form.</summary>
        public string Title => (IsPageSetup ? "Page Setup - " : "Plot - ") + Space.Name;

        /// <summary>Gets the words of the footer: the drawing's file and the space, "plan-level2.dxf · Model".</summary>
        public string Footer => Document.Title + " · " + Space.Name;

        /// <summary>Gets the page setups to choose from: &lt;None&gt;, then the drawing's by name.</summary>
        public IReadOnlyList<string> PageSetups { get; }

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
                    named.DeviceName = CadPlotSettings.PdfDevice;
                    _settings = named;
                    Refresh();
                }
            }
        }

        /// <summary>Gets the plotters of the Printer/plotter list: None and the printers of Windows greyed, GeometryHelper PDF the one chosen.</summary>
        public IReadOnlyList<PlotDeviceRow> Devices { get; }

        /// <summary>Gets or sets the plotter chosen: only GeometryHelper PDF is taken.</summary>
        public PlotDeviceRow Device
        {
            get => Devices[1];
            set => Raise(nameof(Device));
        }

        /// <summary>Gets what the plotter is: "GeometryHelper PDF · vector, built in".</summary>
        public string PlotterText => CadPlotSettings.PdfDevice + " · vector, built in";

        /// <summary>Gets where it writes: File.</summary>
        public string WhereText => "File";

        /// <summary>Gets what it does.</summary>
        public string DescriptionText => "Writes a PDF, one page per plot; text stays searchable.";

        /// <summary>Gets the sheets of the Paper size list.</summary>
        public IReadOnlyList<CadPaper> Papers { get; }

        /// <summary>Gets or sets the sheet chosen.</summary>
        public CadPaper Paper
        {
            get => Papers.FirstOrDefault(p => p.IsSameAs(_settings.Paper)) ?? _settings.Paper;
            set
            {
                if (value != null && !value.IsSameAs(_settings.Paper))
                {
                    _settings.Paper = value;
                    Refresh();
                }
            }
        }

        /// <summary>Gets the width of the sheet as it lies, over the small picture of it: "420 MM".</summary>
        public string SheetWidthText => Whole(Page.SheetWidth) + " MM";

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
                return new System.Windows.Thickness(page.Printable.MinX * k, (page.SheetHeight - page.Printable.MaxY) * k, (page.SheetWidth - page.Printable.MaxX) * k, page.Printable.MinY * k);
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
                return w.IsEmpty ? string.Empty : "From (" + Spaced0(w.MinX) + ", " + Spaced0(w.MinY) + ") to (" + Spaced0(w.MaxX) + ", " + Spaced0(w.MaxY) + ")";
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
                CadPlotScale scale = Scales.FirstOrDefault(s => Math.Abs(s.PaperUnits / s.DrawingUnits - ratio) <= 1E-9 * ratio);
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
                string name = System.IO.Path.GetFileNameWithoutExtension(Document.Title) + "-" + Space.Name + ".pdf";

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
        /// Plots to a PDF as OK does: the file written, and the page setup written into the layout first where Save changes to
        /// layout is ticked.
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

            return CadPdfWriter.Write(path, Document, Space, _settings, _job.Display);
        }

        /// <summary>Makes the preview of the plot as the dialog has it, as Preview… shows it.</summary>
        /// <returns>The preview.</returns>
        public PlotPreviewViewModel CreatePreview() => new PlotPreviewViewModel(Document, Space, _settings, _job.Display);

        /// <summary>Hands the settings back to the command where the dialog closed to have a window picked.</summary>
        internal void HandBack()
        {
            _job.Settings = _settings.Clone();
            _job.PicksWindow = PicksWindow;
        }

        private double PictureScale
        {
            get
            {
                CadPlotPage page = Page;
                return Math.Min(92.0 / page.SheetWidth, 66.0 / page.SheetHeight);
            }
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

            foreach (string name in new[]
            {
                nameof(Paper), nameof(Area), nameof(IsWindow), nameof(WindowText), nameof(Centered), nameof(IsCenterEnabled), nameof(IsOffsetEnabled),
                nameof(OffsetXText), nameof(OffsetYText), nameof(FitToPaper), nameof(StyleTable), nameof(PlotLineweights), nameof(PlotTransparency),
                nameof(PlotWithPlotStyles), nameof(Landscape), nameof(Portrait), nameof(UpsideDown), nameof(OrientationAngle), nameof(ScaleLineweights),
            })
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
                _drawingUnitsText = page.IsEmpty ? string.Empty : (1.0 / page.Scale).ToString("0.##", CultureInfo.InvariantCulture);
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

        private static bool TryNumber(string text, out double value)
            => double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);

        private static string Whole(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Spaced0(double value) => value.ToString("#,0.##", Spaced);
    }
}
