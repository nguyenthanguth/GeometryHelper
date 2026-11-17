using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// One thing a publish does, in the order of the list: a sheet plotted to a PDF, printed on a printer, or passed over, with
    /// the settings it is plotted by (<see cref="CadPublishList.Plan"/>). A window's writers carry the steps out.
    /// </summary>
    public sealed class CadPublishStep
    {
        internal CadPublishStep(CadPublishSheet sheet)
        {
            Sheet = sheet;
        }

        /// <summary>Gets the sheet.</summary>
        public CadPublishSheet Sheet { get; }

        /// <summary>Gets how the sheet is plotted: its page setup, as the list's options leave it. Null where the sheet is passed over.</summary>
        public CadPlotSettings Settings { get; internal set; }

        /// <summary>Gets the name of the printer of Windows the sheet is printed on; null for a sheet that goes to a PDF or is passed over.</summary>
        public string Printer { get; internal set; }

        /// <summary>Gets the PDF the sheet is written to; null for a sheet that is printed or passed over. Sheets of one multi-sheet PDF name the same file.</summary>
        public string File { get; internal set; }

        /// <summary>Gets why the sheet is passed over: its status, or that its plot area is empty; null where it is published.</summary>
        public string Skipped { get; internal set; }
    }

    /// <summary>
    /// The sheet list of the Publish dialog and of -PUBLISH, with the options that go with it: the Model and the layouts of
    /// the drawing open and of drawings added from files, in the order they are to be published, each with the page setup it is
    /// plotted by. The list is read from and written to a sheet list file (<see cref="Load"/>, <see cref="Save"/>), and
    /// <see cref="Plan"/> says what a publish does with each sheet. Nothing here draws or writes a plot: a window's writers carry
    /// the plan out, with the PDF writer and the printers it has.
    /// </summary>
    public sealed class CadPublishList
    {
        private const string InvalidNameLetters = "<>/\\\":?*|";
        private static readonly string[] KnownSections =
        {
            "DWF6Version",
            "DWF6MinorVersion",
            "Target",
            "SheetSet Properties",
        };

        private readonly List<CadPublishSheet> _sheets = new List<CadPublishSheet>();
        private readonly Dictionary<string, CadDocument> _documents = new Dictionary<string, CadDocument>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, DxfDatabase> _imported = new Dictionary<string, DxfDatabase>(
            StringComparer.OrdinalIgnoreCase
        );
        private CadDsdFile _kept;
        private CadPublishOptions _options = new CadPublishOptions();

        /// <summary>Makes an empty list for the drawing open.</summary>
        /// <param name="current">The drawing open, plotted as it stands where a sheet is of it; null for none.</param>
        public CadPublishList(CadDocument current)
        {
            Current = current;
        }

        /// <summary>Gets the drawing open, whose sheets are plotted with the edits not saved yet; null for none.</summary>
        public CadDocument Current { get; }

        /// <summary>Gets the sheets, in the order they are published.</summary>
        public IReadOnlyList<CadPublishSheet> Sheets => _sheets;

        /// <summary>Gets or sets where the sheets go and how, the options that the list file keeps and the dialog sets.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the options set are null.</exception>
        public CadPublishOptions Options
        {
            get => _options;
            set => _options = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets or sets the plot stamp every sheet is given, as AutoCAD's Include plot stamp; null for none.</summary>
        public CadPlotStamp PlotStamp { get; set; }

        /// <summary>Gets or sets whether the text of SHX fonts is written as words too, as PDFSHX 1 does.</summary>
        public bool PdfShx { get; set; }

        /// <summary>
        /// Adds the Model and the layouts of the drawing open, as the Publish dialog lists them when it opens: Model first and
        /// the layouts in the order of their tabs, each as the drawing's name and the space's joined by a hyphen
        /// (<c>plan-level2-Model</c>, <c>plan-level2-Layout1</c>), by <see cref="CadPublishOptions.IncludeModel"/> and
        /// <see cref="CadPublishOptions.IncludeLayouts"/>. A sheet the list has already is not added again.
        /// </summary>
        /// <returns>The sheets added, in order; empty where there is no drawing open or all were listed.</returns>
        public IReadOnlyList<CadPublishSheet> AddCurrentDrawing() =>
            Current == null ? new CadPublishSheet[0] : AddSheetsOf(Current);

        /// <summary>
        /// Adds the sheets of a drawing file, as Add Sheets does: the drawing read with the readers of the presenter (a DWG or
        /// a DXF), its Model and layouts listed by <see cref="CadPublishOptions.IncludeModel"/> and
        /// <see cref="CadPublishOptions.IncludeLayouts"/>. The drawing that is open is used as it stands, not read again.
        /// </summary>
        /// <param name="path">The drawing's file.</param>
        /// <param name="problem">Why nothing was added: <c>Drawing not found</c> or <c>Drawing failed to load: …</c>; null where the drawing was read.</param>
        /// <returns>The sheets added, in order; empty where the drawing was not read or all its sheets were listed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public IReadOnlyList<CadPublishSheet> AddDrawing(string path, out string problem)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            CadDocument document = DocumentOf(path, out string status, out problem);
            return document == null ? new CadPublishSheet[0] : AddSheetsOf(document);
        }

        private IReadOnlyList<CadPublishSheet> AddSheetsOf(CadDocument document)
        {
            var added = new List<CadPublishSheet>();
            string file = document.HasFile ? document.FileName : string.Empty;
            string drawing = Path.GetFileNameWithoutExtension(document.Title);

            foreach (CadSpace space in document.Spaces)
            {
                if (space.IsModel ? !_options.IncludeModel : !_options.IncludeLayouts)
                {
                    continue;
                }

                if (
                    _sheets.Any(s =>
                        string.Equals(s.DrawingFile, file, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(s.LayoutName, space.Name, StringComparison.OrdinalIgnoreCase)
                        && ReferenceEquals(s.Document, document)
                    )
                )
                {
                    continue;
                }

                var sheet = new CadPublishSheet(UniqueName(drawing + "-" + space.Name), file, space.Name)
                {
                    Document = document,
                    Space = space,
                    Status = CadPublishSheet.Loaded,
                };
                _sheets.Add(sheet);
                added.Add(sheet);
            }

            return added;
        }

        private CadDocument DocumentOf(string path, out string status, out string problem)
        {
            problem = null;
            status = CadPublishSheet.Loaded;
            string full;

            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                status = CadPublishSheet.DrawingNotFound;
                problem = status;
                return null;
            }

            if (Current != null && Current.HasFile && SamePath(Current.FileName, full))
            {
                return Current;
            }

            if (_documents.TryGetValue(full, out CadDocument known))
            {
                return known;
            }

            if (!File.Exists(full))
            {
                status = CadPublishSheet.DrawingNotFound;
                problem = status;
                return null;
            }

            try
            {
                CadDocument opened = CadDocument.Open(full);
                _documents[full] = opened;
                return opened;
            }
            catch (Exception exception)
                when (exception is FormatException
                    || exception is NotSupportedException
                    || exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is InvalidOperationException
                )
            {
                GeometryHelperLog.Warn("CadPublishList: " + full + " could not be read.", exception);
                status = CadPublishSheet.DrawingFailedToLoad;
                problem = CadPublishSheet.DrawingFailedToLoad + ": " + exception.Message;
                return null;
            }
        }

        private static bool SamePath(string a, string b) =>
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

        /// <summary>A name no sheet has, letters of either case the same: the name given, or with <c> (2)</c>, <c> (3)</c> after it.</summary>
        private string UniqueName(string name, CadPublishSheet except = null)
        {
            string tried = name;

            for (int n = 2; _sheets.Any(s => !ReferenceEquals(s, except) && NameIs(s, tried)); n++)
            {
                tried = name + " (" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            }

            return tried;
        }

        private static bool NameIs(CadPublishSheet sheet, string name) =>
            string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase);

        /// <summary>Removes a sheet from the list.</summary>
        /// <param name="sheet">The sheet.</param>
        /// <returns>True where it was in the list.</returns>
        public bool Remove(CadPublishSheet sheet) => sheet != null && _sheets.Remove(sheet);

        /// <summary>Removes every sheet from the list.</summary>
        public void RemoveAll() => _sheets.Clear();

        /// <summary>Moves a sheet one place earlier, so that it is published sooner.</summary>
        /// <param name="sheet">The sheet.</param>
        /// <returns>True where it moved; false for the first sheet and for one not in the list.</returns>
        public bool MoveUp(CadPublishSheet sheet) => Move(sheet, -1);

        /// <summary>Moves a sheet one place later.</summary>
        /// <param name="sheet">The sheet.</param>
        /// <returns>True where it moved; false for the last sheet and for one not in the list.</returns>
        public bool MoveDown(CadPublishSheet sheet) => Move(sheet, 1);

        private bool Move(CadPublishSheet sheet, int by)
        {
            int at = sheet == null ? -1 : _sheets.IndexOf(sheet);

            if (at < 0 || at + by < 0 || at + by >= _sheets.Count)
            {
                return false;
            }

            _sheets.RemoveAt(at);
            _sheets.Insert(at + by, sheet);
            return true;
        }

        /// <summary>
        /// Renames a sheet, as AutoCAD's Rename Sheet: the name has none of <c>&lt; &gt; / \ " : ? * |</c> and no other sheet has
        /// it, letters of either case the same.
        /// </summary>
        /// <param name="sheet">The sheet.</param>
        /// <param name="name">The new name; its ends are trimmed.</param>
        /// <returns>Why it was refused; null where the sheet was renamed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sheet or the name is null.</exception>
        public string Rename(CadPublishSheet sheet, string name)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            string trimmed = (name ?? throw new ArgumentNullException(nameof(name))).Trim();

            if (trimmed.Length == 0)
            {
                return "Enter a name for the sheet.";
            }

            if (trimmed.IndexOfAny(InvalidNameLetters.ToCharArray()) >= 0)
            {
                return "Invalid characters in sheet name: \""
                    + trimmed
                    + "\". The following characters are not valid in sheet names: "
                    + InvalidNameLetters;
            }

            if (_sheets.Any(s => !ReferenceEquals(s, sheet) && NameIs(s, trimmed)))
            {
                return trimmed + ": This sheet name already exists.";
            }

            sheet.Name = trimmed;
            return null;
        }

        /// <summary>Gets the page setups of the drawing a sheet is of, by name, for the sheet's Page Setup list.</summary>
        /// <param name="sheet">The sheet.</param>
        /// <returns>The names; empty where the drawing was not read or has none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sheet is null.</exception>
        public IReadOnlyList<string> PageSetupNames(CadPublishSheet sheet)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            return sheet.Document == null ? new string[0] : CadPlotSettings.PageSetupNames(sheet.Document);
        }

        /// <summary>
        /// Gets the page setups of another drawing, as Import Page Setups for Publishing lists them; the drawing is kept so that
        /// a sheet set to one of them (<see cref="SetPageSetup"/>) plots by it.
        /// </summary>
        /// <param name="file">The drawing's file.</param>
        /// <param name="problem">Why none were listed: <c>Drawing not found</c> or <c>Drawing failed to load: …</c>; null where the drawing was read.</param>
        /// <returns>The names in ordinal order; empty where the drawing has none or was not read.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the file is null.</exception>
        public IReadOnlyList<string> ImportablePageSetups(string file, out string problem)
        {
            DxfDatabase database = ImportedFrom(file ?? throw new ArgumentNullException(nameof(file)), out problem);
            return database == null ? new string[0] : CadPlotSettings.PageSetupNames(database);
        }

        private DxfDatabase ImportedFrom(string file, out string problem)
        {
            problem = null;
            string full = Path.GetFullPath(file);

            if (_imported.TryGetValue(full, out DxfDatabase known))
            {
                return known;
            }

            CadDocument document = DocumentOf(full, out _, out problem);

            if (document == null)
            {
                return null;
            }

            _imported[full] = document.Database;
            return document.Database;
        }

        /// <summary>
        /// Sets the page setup a sheet is plotted with, as the sheet's Page Setup list: none for the layout's own, the name of
        /// a page setup of the sheet's drawing, or a page setup of another drawing.
        /// </summary>
        /// <param name="sheet">The sheet.</param>
        /// <param name="name">The page setup's name; empty for the sheet's own.</param>
        /// <param name="file">The drawing the page setup is imported from; empty for one of the sheet's own drawing.</param>
        /// <returns>Why it was refused (<c>Page setup not found</c>, <c>Drawing not found</c>, <c>Drawing failed to load: …</c>); null where it was set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sheet is null.</exception>
        public string SetPageSetup(CadPublishSheet sheet, string name, string file = "")
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            name = name?.Trim() ?? string.Empty;
            file = file ?? string.Empty;

            if (name.Length == 0)
            {
                sheet.PageSetup = string.Empty;
                sheet.PageSetupFile = string.Empty;
                RefreshStatus(sheet);
                return null;
            }

            string found;

            if (file.Length == 0)
            {
                found =
                    sheet.Document == null
                        ? null
                        : CadPlotSettings
                            .PageSetupNames(sheet.Document)
                            .FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                DxfDatabase database = ImportedFrom(file, out string problem);

                if (database == null)
                {
                    return problem;
                }

                found = CadPlotSettings
                    .PageSetupNames(database)
                    .FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                file = Path.GetFullPath(file);
            }

            if (found == null)
            {
                return CadPublishSheet.PageSetupNotFound;
            }

            sheet.PageSetup = found;
            sheet.PageSetupFile = file;
            RefreshStatus(sheet);
            return null;
        }

        private void RefreshStatus(CadPublishSheet sheet)
        {
            if (sheet.Space != null)
            {
                sheet.Status = CadPublishSheet.Loaded;
            }
        }

        /// <summary>
        /// Gets how a sheet is plotted by the page setup it is set to: the layout's own, a page setup of its drawing, or one
        /// imported from another drawing, read as the Plot dialog reads a page setup. The plotter is left as the setup names it.
        /// </summary>
        /// <param name="sheet">The sheet.</param>
        /// <returns>The settings, a new object; null where the sheet is not loaded or its page setup is not there (its status says which).</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sheet is null.</exception>
        public CadPlotSettings SettingsOf(CadPublishSheet sheet)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            if (sheet.Space == null || sheet.Document == null)
            {
                return null;
            }

            if (sheet.PageSetup.Length == 0)
            {
                return CadPlotSettings.Read(sheet.Document, sheet.Space);
            }

            CadPlotSettings settings;

            if (sheet.PageSetupFile.Length == 0)
            {
                settings = CadPlotSettings.ReadPageSetup(sheet.Document, sheet.Space, sheet.PageSetup);
            }
            else
            {
                DxfDatabase database = ImportedFrom(sheet.PageSetupFile, out _);
                settings =
                    database == null
                        ? null
                        : CadPlotSettings.ReadPageSetup(database, sheet.PageSetup, sheet.Space.IsModel);
            }

            if (settings == null)
            {
                sheet.Status = CadPublishSheet.PageSetupNotFound;
            }

            return settings;
        }

        /// <summary>
        /// Gets the folder the PDFs are written to: <see cref="CadPublishOptions.OutputFolder"/>, else the folder of the first
        /// sheet whose drawing has a file, else Documents.
        /// </summary>
        /// <returns>The folder.</returns>
        public string OutputFolder()
        {
            if (!string.IsNullOrWhiteSpace(_options.OutputFolder))
            {
                return _options.OutputFolder;
            }

            foreach (CadPublishSheet sheet in _sheets)
            {
                if (sheet.DrawingFile.Length > 0)
                {
                    return Path.GetDirectoryName(sheet.DrawingFile) ?? string.Empty;
                }
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        /// <summary>
        /// Gets the multi-sheet PDF: <see cref="CadPublishOptions.FileName"/> in the output folder, <c>.pdf</c> added; with no name
        /// the name of the first drawing listed, so that sheets of plan-level2.dxf make <c>plan-level2.pdf</c>.
        /// </summary>
        /// <returns>The path.</returns>
        public string MultiSheetFile()
        {
            string name = _options.FileName?.Trim() ?? string.Empty;

            if (name.Length == 0)
            {
                CadPublishSheet first = _sheets.FirstOrDefault(s => s.Document != null);
                name = first == null ? "Publish" : Path.GetFileNameWithoutExtension(first.Document.Title);
            }

            string path = Path.IsPathRooted(name) ? name : Path.Combine(OutputFolder(), name);
            return path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? path : path + ".pdf";
        }

        /// <summary>Gets the PDF of one sheet where each sheet has a file: its name in the output folder, <c>plan-level2-Layout1.pdf</c>.</summary>
        /// <param name="sheet">The sheet.</param>
        /// <returns>The path.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sheet is null.</exception>
        public string SheetFile(CadPublishSheet sheet)
        {
            string name = (sheet ?? throw new ArgumentNullException(nameof(sheet))).Name;

            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }

            return Path.Combine(OutputFolder(), name + ".pdf");
        }

        /// <summary>
        /// Says what a publish does with each sheet, in the order of the list. A sheet not loaded, or whose plot area is empty,
        /// is passed over (<see cref="CadPublishStep.Skipped"/>). With <see cref="CadPublishTo.Pdf"/> every other sheet is
        /// plotted by its page setup to a PDF: one file for all (<see cref="MultiSheetFile"/>), or a file each
        /// (<see cref="SheetFile"/>). With <see cref="CadPublishTo.PlotterNamedInPageSetup"/> a sheet whose page setup names a
        /// printer in <paramref name="printers"/> is printed there, and any other is written as a PDF of its own. The list's plot
        /// stamp and SHX text go on every sheet.
        /// </summary>
        /// <param name="printers">The names of the printers of Windows this computer has, letters of either case the same; null for none.</param>
        /// <returns>The steps, one for each sheet.</returns>
        public IReadOnlyList<CadPublishStep> Plan(IEnumerable<string> printers)
        {
            var known = new List<string>(printers ?? new string[0]);
            bool toPlotter = _options.PublishTo == CadPublishTo.PlotterNamedInPageSetup;
            string multi = !toPlotter && _options.MultiSheet ? MultiSheetFile() : null;
            var steps = new List<CadPublishStep>();

            foreach (CadPublishSheet sheet in _sheets)
            {
                var step = new CadPublishStep(sheet);
                steps.Add(step);
                CadPlotSettings settings = SettingsOf(sheet);

                if (settings == null)
                {
                    step.Skipped = sheet.Status;
                    continue;
                }

                settings.PlotStamp = PlotStamp?.Clone();
                settings.PdfShx = PdfShx;
                string printer = toPlotter
                    ? known.FirstOrDefault(p =>
                        string.Equals(p, settings.SavedPlotter, StringComparison.OrdinalIgnoreCase)
                    )
                    : null;

                if (printer != null)
                {
                    settings.UsePrinter(printer);
                    step.Printer = printer;
                }
                else
                {
                    settings.UseDevice(CadPlotSettings.PdfDevice);
                    step.File = multi ?? SheetFile(sheet);
                }

                if (CadPlotPage.Compute(sheet.Document, sheet.Space, settings, DisplayBox.Empty).IsEmpty)
                {
                    step.Skipped = "Nothing to plot: the plot area is empty.";
                    step.Printer = null;
                    step.File = null;
                    continue;
                }

                step.Settings = settings;
            }

            return steps;
        }

        /// <summary>
        /// Loads the sheets and the options of a sheet list file (.dsd), as Load List does. A sheet whose drawing is not there,
        /// or has no such layout or page setup, stays in the list with that as its status. Paths of a relative kind are taken
        /// from the folder of the list. A list that names DWF or DWFx is set to PDF, which says so in the notes.
        /// </summary>
        /// <param name="path">The list's file.</param>
        /// <param name="append">True to add the sheets after those listed; false to replace them.</param>
        /// <returns>What was not as the file asked, a line each; empty where all was.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="FormatException">Thrown when the file is no sheet list.</exception>
        public IReadOnlyList<string> Load(string path, bool append)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            CadDsdFile dsd = CadDsdFile.Read(path);
            string folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
            var notes = new List<string>();

            if (!append)
            {
                _sheets.Clear();
            }

            foreach (CadDsdSection section in dsd.Sections)
            {
                if (!section.Name.StartsWith("DWF6Sheet:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string layout = section.Get("Layout");
                string drawing = Unquote(section.Get("DWG") ?? section.Get("OriginalSheetPath"));
                string setup = section.Get("Setup") ?? string.Empty;
                int bar = setup.IndexOf('|');
                string setupFile = bar < 0 ? string.Empty : Unquote(setup.Substring(bar + 1));
                string setupName = bar < 0 ? setup : setup.Substring(0, bar);
                var sheet = new CadPublishSheet(
                    UniqueName(section.Name.Substring("DWF6Sheet:".Length).Trim()),
                    Absolute(drawing, folder),
                    string.IsNullOrWhiteSpace(layout) ? CadPublishSheet.ModelName : layout.Trim()
                )
                {
                    PageSetup = setupName.Trim(),
                    PageSetupFile = setupFile.Length == 0 ? string.Empty : Absolute(setupFile, folder),
                };
                Resolve(sheet, drawing);
                _sheets.Add(sheet);

                if (!sheet.IsLoaded)
                {
                    notes.Add(sheet.Name + ": " + sheet.Status);
                }
            }

            ReadTarget(dsd, notes);
            _kept = dsd;
            return notes;
        }

        private void Resolve(CadPublishSheet sheet, string written)
        {
            CadDocument document =
                written == "<Unsaved Drawing>" && Current != null && !Current.HasFile
                    ? Current
                    : DocumentOf(sheet.DrawingFile, out _, out _);

            if (document == null)
            {
                sheet.Status = File.Exists(sheet.DrawingFile)
                    ? CadPublishSheet.DrawingFailedToLoad
                    : CadPublishSheet.DrawingNotFound;
                return;
            }

            sheet.Document = document;
            sheet.Space = sheet.IsModel
                ? document.ModelSpace
                : document.Spaces.FirstOrDefault(s =>
                    !s.IsModel && string.Equals(s.Name, sheet.LayoutName, StringComparison.OrdinalIgnoreCase)
                );
            sheet.Status = sheet.Space == null ? CadPublishSheet.LayoutNotFound : CadPublishSheet.Loaded;

            if (sheet.Space != null && sheet.PageSetup.Length > 0)
            {
                string refused = SetPageSetup(sheet, sheet.PageSetup, sheet.PageSetupFile);
                sheet.Status = refused ?? CadPublishSheet.Loaded;
            }
        }

        private void ReadTarget(CadDsdFile dsd, List<string> notes)
        {
            CadDsdSection target = dsd.Find("Target");

            if (
                target != null
                && int.TryParse(
                    target.Get("Type"),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int type
                )
            )
            {
                // AutoCAD's SheetType: 0 single DWF, 1 multi DWF, 2 original device, 3 single DWFx, 4 multi DWFx, 5 single PDF, 6 multi PDF.
                _options.PublishTo = type == 2 ? CadPublishTo.PlotterNamedInPageSetup : CadPublishTo.Pdf;

                if (type != 2)
                {
                    _options.MultiSheet = type == 1 || type == 4 || type == 6;
                }

                if (type == 0 || type == 1 || type == 3 || type == 4)
                {
                    notes.Add("DWF and DWFx files have no writer in the presenter: the sheets are published as PDF.");
                }

                string file = Unquote(target.Get("DWF"));
                _options.FileName =
                    _options.MultiSheet && file.Length > 0 && type != 2
                        ? Path.ChangeExtension(file, ".pdf")
                        : string.Empty;
                _options.OutputFolder = Unquote(target.Get("OUT"));
            }

            CadDsdSection properties = dsd.Find("SheetSet Properties");

            if (properties != null)
            {
                if (
                    int.TryParse(
                        properties.Get("NoOfCopies"),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out int copies
                    )
                    && copies >= 1
                    && copies <= 99
                )
                {
                    _options.Copies = copies;
                }

                string view = properties.Get("ViewFile");

                if (view != null)
                {
                    _options.OpenInViewer =
                        string.Equals(view, "TRUE", StringComparison.OrdinalIgnoreCase) || view == "1";
                }
            }
        }

        private static string Unquote(string value) => (value ?? string.Empty).Trim().Trim('"');

        private static string Absolute(string path, string folder) =>
            path.Length == 0 || path.StartsWith("<", StringComparison.Ordinal) || Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(folder, path));

        /// <summary>
        /// Saves the sheets and the options to a sheet list file (.dsd), as Save List does, replacing a file of the name. Where
        /// the list was loaded from a file, what that file held besides is written back as it was.
        /// </summary>
        /// <param name="path">The list's file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public void Save(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var dsd = new CadDsdFile();
            AddSection(dsd, _kept?.Find("DWF6Version"), "DWF6Version", "Ver", "1");
            AddSection(dsd, _kept?.Find("DWF6MinorVersion"), "DWF6MinorVersion", "MinorVer", "1");

            foreach (CadPublishSheet sheet in _sheets)
            {
                string drawing = sheet.DrawingFile.Length > 0 ? sheet.DrawingFile : "<Unsaved Drawing>";
                var section = new CadDsdSection("DWF6Sheet:" + sheet.Name);
                section.Set("DWG", drawing);
                section.Set("Layout", sheet.LayoutName);
                section.Set(
                    "Setup",
                    sheet.PageSetup.Length == 0 ? string.Empty
                        : sheet.PageSetupFile.Length == 0 ? sheet.PageSetup
                        : sheet.PageSetup + "|" + sheet.PageSetupFile
                );
                section.Set("OriginalSheetPath", drawing);
                section.Set("Has Plot Port", "0");
                section.Set("Has3DDWF", "0");
                dsd.Sections.Add(section);
            }

            CadDsdSection target = Copy(_kept?.Find("Target"), "Target");
            bool toPlotter = _options.PublishTo == CadPublishTo.PlotterNamedInPageSetup;
            string folder = OutputFolder().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            target.Set(
                "Type",
                toPlotter ? "2"
                    : _options.MultiSheet ? "6"
                    : "5"
            );
            target.Set("DWF", toPlotter || !_options.MultiSheet ? Path.Combine(folder, "plot.pdf") : MultiSheetFile());
            target.Set("OUT", folder + Path.DirectorySeparatorChar);
            target.Set("PWD", target.Get("PWD") ?? string.Empty);
            dsd.Sections.Add(target);

            if (_kept != null)
            {
                foreach (CadDsdSection other in _kept.Sections)
                {
                    bool own =
                        other.Name.StartsWith("DWF6Sheet:", StringComparison.OrdinalIgnoreCase)
                        || KnownSections.Any(n => string.Equals(n, other.Name, StringComparison.OrdinalIgnoreCase));

                    if (!own)
                    {
                        dsd.Sections.Add(other);
                    }
                }
            }

            CadDsdSection properties = Copy(_kept?.Find("SheetSet Properties"), "SheetSet Properties");

            if (properties.Pairs.Count == 0)
            {
                foreach (
                    string line in new[]
                    {
                        "IsSheetSet=FALSE",
                        "IsHomogeneous=FALSE",
                        "SheetSet Name=",
                        "NoOfCopies=1",
                        "PlotStampOn=FALSE",
                        "ViewFile=FALSE",
                        "JobID=0",
                        "SelectionSetName=",
                        "AcadProfile=",
                        "CategoryName=",
                        "LogFilePath=",
                        "IncludeLayer=TRUE",
                        "LineMerge=FALSE",
                        "CurrentPrecision=",
                        "PromptForDwfName=FALSE",
                        "PwdProtectPublishedDWF=FALSE",
                        "PromptForPwd=FALSE",
                        "RepublishingMarkups=FALSE",
                        "PublishSheetSetMetadata=FALSE",
                        "PublishSheetMetadata=FALSE",
                        "3DDWFOptions=0 0",
                    }
                )
                {
                    int equals = line.IndexOf('=');
                    properties.Set(line.Substring(0, equals), line.Substring(equals + 1));
                }
            }

            properties.Set("NoOfCopies", _options.Copies.ToString(System.Globalization.CultureInfo.InvariantCulture));
            properties.Set("ViewFile", _options.OpenInViewer ? "TRUE" : "FALSE");
            properties.Set("PlotStampOn", PlotStamp != null ? "TRUE" : "FALSE");
            dsd.Sections.Add(properties);
            dsd.Write(path);
            _kept = dsd;
        }

        private static void AddSection(CadDsdFile dsd, CadDsdSection kept, string name, string key, string value)
        {
            CadDsdSection section = Copy(kept, name);

            if (section.Pairs.Count == 0)
            {
                section.Set(key, value);
            }

            dsd.Sections.Add(section);
        }

        private static CadDsdSection Copy(CadDsdSection from, string name)
        {
            var copy = new CadDsdSection(name);

            if (from != null)
            {
                foreach (KeyValuePair<string, string> pair in from.Pairs)
                {
                    copy.Add(pair.Key, pair.Value);
                }
            }

            return copy;
        }
    }
}
