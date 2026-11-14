using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD 2021's Save As PDF over EXPORTPDF's file request: the file, with Browse… for the Save dialog, and the Output
    /// Controls beside it, Open in viewer when done, Include plot stamp and its settings, Export (Current layout and All
    /// layouts in a layout, Display, Extents and Window in model space), Page Setup Current or Override with Page Setup
    /// Override…, and Options… for Export to PDF Options. Save gives the request the file and the options; Cancel gives it
    /// no file, and nothing is written.
    /// </summary>
    public sealed class SaveAsPdfViewModel : ObservableObject
    {
        /// <summary>The Page Setup choice that exports each space by its own page setup.</summary>
        public const string Current = "Current";

        /// <summary>The Page Setup choice that exports by the page setup Page Setup Override sets.</summary>
        public const string Override = "Override";

        private readonly CadExportJob _job;
        private readonly CadExportPdfOptions _options;
        private string _fileName;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over EXPORTPDF's file request.</summary>
        /// <param name="job">The request; its <see cref="CadExportJob.PdfOptions"/> holds the options.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the request carries no PDF options.</exception>
        public SaveAsPdfViewModel(CadExportJob job)
        {
            _job = job ?? throw new ArgumentNullException(nameof(job));
            _options = job.PdfOptions ?? throw new ArgumentException("The request is not EXPORTPDF's.", nameof(job));
            _fileName = job.FileName ?? string.Empty;
        }

        /// <summary>Gets the options the dialog sets.</summary>
        public CadExportPdfOptions Options => _options;

        /// <summary>Gets or sets how Browse… shows the Save dialog: it sets the job's file, or null where it was cancelled.</summary>
        public Action<CadExportJob> BrowseFile { get; set; }

        /// <summary>Gets or sets how Page Setup Override… is shown: true when OK was pressed.</summary>
        public Func<PageSetupOverrideViewModel, bool> ShowPageSetupOverride { get; set; }

        /// <summary>Gets or sets how Options… shows Export to PDF Options: true when OK was pressed.</summary>
        public Func<ExportPdfOptionsViewModel, bool> ShowOptions { get; set; }

        /// <summary>Gets or sets how the Plot Stamp button shows Plot Stamp: true when OK was pressed with the values taken.</summary>
        public Func<PlotStampViewModel, bool> EditPlotStamp { get; set; }

        /// <summary>Gets or sets the file the PDF is written to.</summary>
        public string FileName
        {
            get => _fileName;
            set => Set(ref _fileName, value ?? string.Empty);
        }

        /// <summary>Gets why Save did not close the dialog; empty where it has not been refused.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets or sets whether the PDF opens in the viewer once written.</summary>
        public bool OpenInViewer
        {
            get => _options.OpenInViewer;
            set
            {
                _options.OpenInViewer = value;
                Raise(nameof(OpenInViewer));
            }
        }

        /// <summary>Gets or sets whether the PDF carries the plot stamp.</summary>
        public bool IncludePlotStamp
        {
            get => _options.IncludePlotStamp;
            set
            {
                _options.IncludePlotStamp = value;
                Raise(nameof(IncludePlotStamp));
            }
        }

        /// <summary>Gets what the Export list offers.</summary>
        public IReadOnlyList<string> Exports => _options.Exports;

        /// <summary>Gets or sets what is exported.</summary>
        public string Export
        {
            get => _options.Export;
            set
            {
                if (value != null && _options.Exports.Contains(value))
                {
                    _options.Export = value;
                }

                Raise(nameof(Export));
                Raise(nameof(IsWindow));
            }
        }

        /// <summary>Gets whether a window of model space is exported, so that Window &lt; picks its corners.</summary>
        public bool IsWindow => _options.Export == CadExportPdfOptions.Window;

        /// <summary>Gets the Page Setup choices: Current and Override.</summary>
        public IReadOnlyList<string> PageSetups { get; } = new[] { Current, Override };

        /// <summary>Gets or sets the Page Setup choice.</summary>
        public string PageSetup
        {
            get => _options.OverridePageSetup ? Override : Current;
            set
            {
                _options.OverridePageSetup = value == Override;
                Raise(nameof(PageSetup));
            }
        }

        /// <summary>Gets the options in a line beside Options…, as "Hyperlinks included".</summary>
        public string OptionsSummary => _options.IncludeHyperlinks ? "Hyperlinks included" : "Hyperlinks left out";

        /// <summary>Browse…: the Save dialog of PDF (*.pdf) on the file; the file it chose taken, a cancel leaving it.</summary>
        public void Browse()
        {
            if (BrowseFile == null)
            {
                return;
            }

            var asked = new CadExportJob(_job.Title, _job.FileTypes, _job.FileType, Trimmed(_fileName));
            BrowseFile(asked);

            if (asked.FileName != null)
            {
                FileName = asked.FileName;
            }
        }

        /// <summary>Page Setup Override…: on OK the override is set and Page Setup turns to Override.</summary>
        /// <returns>True where OK was pressed.</returns>
        public bool OverridePageSetup()
        {
            if (ShowPageSetupOverride == null)
            {
                return false;
            }

            var dialog = new PageSetupOverrideViewModel(_options.PageSetupOverride);

            if (!ShowPageSetupOverride(dialog) || !dialog.TryAccept())
            {
                return false;
            }

            PageSetup = Override;
            return true;
        }

        /// <summary>Options…: Export to PDF Options, its choices taken on OK.</summary>
        /// <returns>True where OK was pressed.</returns>
        public bool EditOptions()
        {
            if (ShowOptions == null)
            {
                return false;
            }

            var dialog = new ExportPdfOptionsViewModel(_options);

            if (!ShowOptions(dialog))
            {
                return false;
            }

            dialog.Accept();
            Raise(nameof(OptionsSummary));
            return true;
        }

        /// <summary>The Plot Stamp button: Plot Stamp over the dialog's stamp, taken on OK, Include plot stamp then on.</summary>
        /// <returns>True where OK was pressed.</returns>
        public bool PlotStampSettings()
        {
            if (EditPlotStamp == null)
            {
                return false;
            }

            CadPlotStamp edited = _options.PlotStamp.Clone();

            if (!EditPlotStamp(new PlotStampViewModel(edited)))
            {
                return false;
            }

            _options.PlotStamp.CopyFrom(edited);
            IncludePlotStamp = true;
            return true;
        }

        /// <summary>Save: the request given the file, ".pdf" added where it has no extension; refused for an empty name.</summary>
        /// <returns>True where the dialog may close.</returns>
        public bool Accept()
        {
            string file = Trimmed(_fileName);

            if (file == null)
            {
                Message = "Enter a file name.";
                return false;
            }

            if (!System.IO.Path.HasExtension(file))
            {
                file += ".pdf";
            }

            _job.FileName = file;
            Message = string.Empty;
            return true;
        }

        /// <summary>Cancel: the request given no file, so that nothing is written.</summary>
        public void Cancel() => _job.FileName = null;

        private static string Trimmed(string text)
        {
            string trimmed = (text ?? string.Empty).Trim().Trim('"');
            return trimmed.Length == 0 ? null : trimmed;
        }
    }

    /// <summary>
    /// AutoCAD 2021's Page Setup Override from Save As PDF: the paper size, the plot style table, the drawing orientation, and
    /// the plot scale, Fit to paper or millimetres to drawing units, with Scale lineweights. It edits its own values and gives
    /// them to the settings only on OK (<see cref="TryAccept"/>), which refuses a scale not above nought.
    /// </summary>
    public sealed class PageSetupOverrideViewModel : ObservableObject
    {
        /// <summary>The plot style table names, as AutoCAD lists them: none, monochrome.ctb and grayscale.ctb.</summary>
        public static IReadOnlyList<string> StyleTableNames { get; } =
            new[] { "None", "monochrome.ctb", "grayscale.ctb" };

        private readonly CadPlotSettings _settings;
        private string _paper;
        private string _styleTable;
        private bool _landscape;
        private bool _fitToPaper;
        private string _paperUnits;
        private string _drawingUnits;
        private bool _scaleLineweights;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over the settings it overrides.</summary>
        /// <param name="settings">The settings, changed on OK.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public PageSetupOverrideViewModel(CadPlotSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Papers = CadPaper.Standard.Select(p => p.Label).ToList();
            CadPaper paper = CadPaper.Standard.FirstOrDefault(p => p.IsSameAs(settings.Paper)) ?? CadPaper.Default;
            _paper = paper.Label;
            _styleTable = StyleTableNames[
                !settings.PlotWithPlotStyles || settings.StyleTable == CadPlotStyleTable.None ? 0
                : settings.StyleTable == CadPlotStyleTable.Monochrome ? 1
                : 2
            ];
            _landscape = settings.Landscape;
            _fitToPaper = settings.FitToPaper;
            _paperUnits = Text(settings.PaperUnits);
            _drawingUnits = Text(settings.DrawingUnits);
            _scaleLineweights = settings.ScaleLineweights;
        }

        /// <summary>Gets the paper sizes of the presenter's PDF plotter, as "ISO A1 (841.00 x 594.00 MM)".</summary>
        public IReadOnlyList<string> Papers { get; }

        /// <summary>Gets or sets the paper size chosen, one of <see cref="Papers"/>.</summary>
        public string Paper
        {
            get => _paper;
            set => Set(ref _paper, Papers.Contains(value) ? value : _paper);
        }

        /// <summary>Gets the plot style tables listed.</summary>
        public IReadOnlyList<string> StyleTables => StyleTableNames;

        /// <summary>Gets or sets the plot style table chosen, one of <see cref="StyleTables"/>.</summary>
        public string StyleTable
        {
            get => _styleTable;
            set => Set(ref _styleTable, StyleTableNames.Contains(value) ? value : _styleTable);
        }

        /// <summary>Gets or sets whether the drawing is plotted landscape.</summary>
        public bool Landscape
        {
            get => _landscape;
            set
            {
                Set(ref _landscape, value);
                Raise(nameof(Portrait));
            }
        }

        /// <summary>Gets or sets whether the drawing is plotted portrait.</summary>
        public bool Portrait
        {
            get => !_landscape;
            set => Landscape = !value;
        }

        /// <summary>Gets or sets whether the plot is fitted to the paper; off, the scale is the two numbers.</summary>
        public bool FitToPaper
        {
            get => _fitToPaper;
            set
            {
                Set(ref _fitToPaper, value);
                Raise(nameof(IsScaleEditable));
            }
        }

        /// <summary>Gets whether the scale's numbers can be typed: where the plot is not fitted.</summary>
        public bool IsScaleEditable => !_fitToPaper;

        /// <summary>Gets or sets the millimetres of paper of the scale, as typed.</summary>
        public string PaperUnits
        {
            get => _paperUnits;
            set => Set(ref _paperUnits, value ?? string.Empty);
        }

        /// <summary>Gets or sets the drawing units the millimetres stand for, as typed.</summary>
        public string DrawingUnits
        {
            get => _drawingUnits;
            set => Set(ref _drawingUnits, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether the lineweights are scaled with the plot.</summary>
        public bool ScaleLineweights
        {
            get => _scaleLineweights;
            set => Set(ref _scaleLineweights, value);
        }

        /// <summary>Gets why OK did not close the dialog; empty where it has not been refused.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>OK: the values given to the settings; refused, nothing changed, where a number of the scale is not above nought.</summary>
        /// <returns>True where the dialog may close.</returns>
        public bool TryAccept()
        {
            double paperUnits = _settings.PaperUnits;
            double drawingUnits = _settings.DrawingUnits;

            if (
                !_fitToPaper && (!TryNumber(_paperUnits, out paperUnits) || !TryNumber(_drawingUnits, out drawingUnits))
            )
            {
                Message = "Enter a scale above nought.";
                return false;
            }

            _settings.Paper = CadPaper.Standard.First(p => p.Label == _paper);
            int table = StyleTableNames.ToList().IndexOf(_styleTable);
            _settings.PlotWithPlotStyles = table > 0;
            _settings.StyleTable =
                table == 1 ? CadPlotStyleTable.Monochrome
                : table == 2 ? CadPlotStyleTable.Grayscale
                : CadPlotStyleTable.None;
            _settings.Landscape = _landscape;
            _settings.FitToPaper = _fitToPaper;
            _settings.PaperUnits = paperUnits;
            _settings.DrawingUnits = drawingUnits;
            _settings.ScaleLineweights = _scaleLineweights;
            Message = string.Empty;
            return true;
        }

        /// <summary>Gives one page setup the values Page Setup Override sets of another, as an override kept for the next EXPORTPDF.</summary>
        /// <param name="from">The settings copied.</param>
        /// <param name="to">The settings changed.</param>
        public static void CopyOverride(CadPlotSettings from, CadPlotSettings to)
        {
            if (from == null || to == null)
            {
                return;
            }

            to.Paper = from.Paper;
            to.PlotWithPlotStyles = from.PlotWithPlotStyles;
            to.StyleTable = from.StyleTable;
            to.Landscape = from.Landscape;
            to.FitToPaper = from.FitToPaper;
            to.PaperUnits = from.PaperUnits;
            to.DrawingUnits = from.DrawingUnits;
            to.ScaleLineweights = from.ScaleLineweights;
        }

        private static bool TryNumber(string text, out double value) =>
            (
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            )
            && value > 0.0
            && !double.IsInfinity(value);

        private static string Text(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// AutoCAD 2021's Export to PDF Options from Save As PDF: Include hyperlinks, which the PDF writer honours. Vector quality,
    /// Raster image quality, Merge control, Include layer information, Create bookmarks, Capture fonts and Convert all text to
    /// geometry are shown greyed, as the writer always or never does them: lines are exact vectors, raster images are kept at
    /// their own resolution, TrueType fonts are embedded. OK gives the options Include hyperlinks.
    /// </summary>
    public sealed class ExportPdfOptionsViewModel : ObservableObject
    {
        private readonly CadExportPdfOptions _options;
        private bool _includeHyperlinks;

        /// <summary>Makes the dialog over the options of Save As PDF.</summary>
        /// <param name="options">The options, changed on OK.</param>
        /// <exception cref="ArgumentNullException">Thrown when the options are null.</exception>
        public ExportPdfOptionsViewModel(CadExportPdfOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _includeHyperlinks = options.IncludeHyperlinks;
        }

        /// <summary>Gets or sets whether the hyperlinks of the objects become links of the PDF.</summary>
        public bool IncludeHyperlinks
        {
            get => _includeHyperlinks;
            set => Set(ref _includeHyperlinks, value);
        }

        /// <summary>OK: the options given Include hyperlinks.</summary>
        public void Accept() => _options.IncludeHyperlinks = _includeHyperlinks;
    }
}
