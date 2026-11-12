using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A page setup of the drawing in the Page Setup Manager's list: its name, a line on how it plots, and whether the space is set to it.</summary>
    public sealed class PageSetupRow
    {
        internal PageSetupRow(string name, string details, bool isCurrent)
        {
            Name = name;
            Details = details;
            IsCurrent = isCurrent;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets how it plots, as "ISO A3 (420.00 x 297.00 MM) · GeometryHelper PDF · Window 1:20 · monochrome.ctb".</summary>
        public string Details { get; }

        /// <summary>Gets whether the space the manager was opened on is set to it.</summary>
        public bool IsCurrent { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD's Page Setup Manager, which PAGESETUP shows: the page setups the drawing keeps by name, and the one the space is
    /// set to. Set Current copies the one chosen into the space's layout; New… asks a name and what to start with, then the Page
    /// Setup dialog, and adds the page setup when OK is pressed; Modify… edits the one chosen in the same dialog; Import… copies
    /// page setups of another drawing in. Each is one undo step named PAGESETUP. The dialogs it opens are functions the window
    /// sets, so that a test drives it with none.
    /// </summary>
    public sealed class PageSetupManagerViewModel : ObservableObject
    {
        private const string None = "<None>";

        private readonly List<IPlotPrinter> _printers;
        private readonly List<CadPaper> _customSizes;
        private IReadOnlyList<PageSetupRow> _items = Array.Empty<PageSetupRow>();
        private PageSetupRow _selected;
        private string _current = string.Empty;
        private string _message;

        /// <summary>Makes the manager on a space of a document.</summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space whose layout the page setups are set on.</param>
        /// <param name="windowsPrinters">The printers of Windows the Page Setup dialog lists; null for none.</param>
        /// <param name="customPixelSizes">The sizes of an image of the user's own the Page Setup dialog lists; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the space is null.</exception>
        public PageSetupManagerViewModel(
            CadDocument document,
            CadSpace space,
            IEnumerable<IPlotPrinter> windowsPrinters = null,
            IEnumerable<CadPaper> customPixelSizes = null
        )
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Space = space ?? throw new ArgumentNullException(nameof(space));
            _printers = (windowsPrinters ?? Array.Empty<IPlotPrinter>()).Where(p => p != null).ToList();
            _customSizes = (customPixelSizes ?? Array.Empty<CadPaper>()).Where(p => p != null).ToList();
            Refresh();
        }

        /// <summary>Gets or sets how the window asks a name (New Page Setup): true where OK was pressed. Null for none, when New… does nothing.</summary>
        public Func<PageSetupNameViewModel, bool> ShowName { get; set; }

        /// <summary>Gets or sets how the window shows the Page Setup dialog on a page setup: true where OK was pressed. Null for none.</summary>
        public Func<PlotDialogViewModel, bool> ShowPageSetup { get; set; }

        /// <summary>Gets or sets how the window asks the drawing to import page setups from, as its Select File dialog: the path, or null where it was cancelled.</summary>
        public Func<string> AskImportFile { get; set; }

        /// <summary>Gets or sets how the window shows Import Page Setups: true where OK was pressed. Null for none.</summary>
        public Func<ImportPageSetupsViewModel, bool> ShowImport { get; set; }

        /// <summary>Raised when the Page Setup dialog made a size of an image of the user's own, so that the window keeps it between sessions.</summary>
        public event EventHandler<CadPaper> CustomSizeMade;

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the space whose layout the page setups are set on.</summary>
        public CadSpace Space { get; }

        /// <summary>Gets the title.</summary>
        public string Title => "Page Setup Manager";

        /// <summary>Gets the words over the list: "Current layout: Model".</summary>
        public string LayoutText => "Current layout: " + Space.Name;

        /// <summary>Gets the page setup the space is set to, or &lt;None&gt;: "Current page setup: A3 plan".</summary>
        public string CurrentText => "Current page setup: " + (_current.Length == 0 ? None : _current);

        /// <summary>Gets the page setups of the drawing, by name.</summary>
        public IReadOnlyList<PageSetupRow> Items => _items;

        /// <summary>Gets or sets the page setup chosen in the list; null for none.</summary>
        public PageSetupRow Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(CanSetCurrent));
                    Raise(nameof(CanModify));
                    Raise(nameof(DetailsText));
                }
            }
        }

        /// <summary>Gets how the page setup chosen plots, over the buttons; empty for none.</summary>
        public string DetailsText => _selected?.Details ?? string.Empty;

        /// <summary>Gets whether Set Current is offered: a page setup is chosen and the space is not set to it already.</summary>
        public bool CanSetCurrent => _selected != null && !_selected.IsCurrent;

        /// <summary>Gets whether Modify… is offered: a page setup is chosen.</summary>
        public bool CanModify => _selected != null;

        /// <summary>Gets what went wrong in the last thing done, in a sentence; null while nothing has.</summary>
        public string Message
        {
            get => _message;
            private set
            {
                if (Set(ref _message, value))
                {
                    Raise(nameof(HasMessage));
                }
            }
        }

        /// <summary>Gets whether there is a message to show.</summary>
        public bool HasMessage => !string.IsNullOrEmpty(_message);

        /// <summary>Sets the space to the page setup chosen, as Set Current does.</summary>
        /// <returns>What came of the edit; null where no page setup is chosen.</returns>
        public CadEditResult SetCurrent()
        {
            if (_selected == null)
            {
                return null;
            }

            return Finish(CadPlotSettings.SetCurrentPageSetup(Document, Space, _selected.Name));
        }

        /// <summary>
        /// Adds a page setup, as New… does: asks its name and what it starts with, shows the Page Setup dialog on it, and adds
        /// it when OK is pressed there. Nothing is added when either is cancelled.
        /// </summary>
        /// <returns>What came of the edit; null where it was cancelled or the window shows no dialog.</returns>
        public CadEditResult New()
        {
            if (ShowName == null)
            {
                return null;
            }

            var asked = new PageSetupNameViewModel(
                "New Page Setup",
                CadPlotSettings.PageSetupNames(Document),
                StartWithChoices()
            );

            if (!ShowName(asked) || !asked.CanOk)
            {
                return null;
            }

            return Edit(asked.Name.Trim(), StartSettings(asked.StartWithChoice));
        }

        /// <summary>Edits the page setup chosen in the Page Setup dialog, as Modify… does, and writes it back when OK is pressed there.</summary>
        /// <returns>What came of the edit; null where it was cancelled, none is chosen or the window shows no dialog.</returns>
        public CadEditResult Modify()
        {
            if (_selected == null)
            {
                return null;
            }

            CadPlotSettings settings = CadPlotSettings.ReadPageSetup(Document, Space, _selected.Name);
            return settings == null
                ? Finish(CadEditResult.Refuse("The drawing has no page setup named " + _selected.Name + "."))
                : Edit(_selected.Name, settings);
        }

        /// <summary>
        /// Copies page setups of another drawing into this one, as Import… does: asks the drawing, lists its page setups in
        /// Import Page Setups, and copies those ticked.
        /// </summary>
        /// <returns>What came of the edit; null where it was cancelled or the window shows no dialog.</returns>
        public CadEditResult Import()
        {
            string path = AskImportFile?.Invoke();

            if (path == null || ShowImport == null)
            {
                return null;
            }

            DxfDatabase source;

            try
            {
                source = CadDrawingFile.Open(path);
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
                GeometryHelperLog.Warn("PageSetupManagerViewModel: " + path + " could not be opened.", exception);
                return Finish(
                    CadEditResult.Refuse(
                        "The drawing " + Path.GetFileName(path) + " could not be opened: " + exception.Message
                    )
                );
            }

            IReadOnlyList<string> names = CadPlotSettings.PageSetupNames(source);

            if (names.Count == 0)
            {
                return Finish(CadEditResult.Refuse("The drawing " + Path.GetFileName(path) + " has no page setups."));
            }

            var chosen = new ImportPageSetupsViewModel(path, names);

            if (!ShowImport(chosen) || !chosen.CanOk)
            {
                return null;
            }

            return Finish(CadPlotSettings.ImportPageSetups(Document, source, chosen.Chosen));
        }

        /// <summary>The Page Setup dialog on a page setup of a name, its OK writing the page setup under that name.</summary>
        private CadEditResult Edit(string name, CadPlotSettings settings)
        {
            if (ShowPageSetup == null)
            {
                return null;
            }

            var job = new CadPlotJob(Space, settings, DisplayBox.Empty, null);
            var dialog = new PlotDialogViewModel(Document, job, true, _printers, _customSizes, name);
            dialog.CustomSizeMade += (s, size) => CustomSizeMade?.Invoke(this, size);
            return ShowPageSetup(dialog) ? Finish(dialog.SaveToPageSetup()) : null;
        }

        /// <summary>What a new page setup can start with: &lt;None&gt;, the settings of each space as *Model*, then the drawing's page setups.</summary>
        private IReadOnlyList<string> StartWithChoices()
        {
            var choices = new List<string> { None };

            foreach (CadSpace each in Document.Spaces)
            {
                choices.Add("*" + each.Name + "*");
            }

            choices.AddRange(CadPlotSettings.PageSetupNames(Document));
            return choices;
        }

        /// <summary>The settings a new page setup starts from.</summary>
        private CadPlotSettings StartSettings(string choice)
        {
            if (choice != null && choice.Length > 2 && choice[0] == '*' && choice[choice.Length - 1] == '*')
            {
                string spaceName = choice.Substring(1, choice.Length - 2);
                CadSpace from = Document.Spaces.FirstOrDefault(s =>
                    string.Equals(s.Name, spaceName, StringComparison.OrdinalIgnoreCase)
                );

                if (from != null)
                {
                    return CadPlotSettings.Read(Document, from);
                }
            }

            CadPlotSettings named =
                choice == null || choice == None ? null : CadPlotSettings.ReadPageSetup(Document, Space, choice);

            if (named != null)
            {
                return named;
            }

            // None: the defaults of a sheet in the space, a layout's plotting its paper at 1:1.
            var settings = new CadPlotSettings();

            if (!Space.IsModel)
            {
                settings.Area = CadPlotArea.Layout;
                settings.FitToPaper = false;
                settings.Centered = false;
            }

            return settings;
        }

        /// <summary>An edit done: its refusal kept as the message, the list made again.</summary>
        private CadEditResult Finish(CadEditResult result)
        {
            Message = result.Succeeded ? null : result.Message;
            Refresh();
            return result;
        }

        /// <summary>The list and the current page setup read again from the drawing, the choice kept by name.</summary>
        private void Refresh()
        {
            string chosen = _selected?.Name;
            _current = CadPlotSettings.CurrentPageSetupName(Document, Space);
            var rows = new List<PageSetupRow>();

            foreach (string name in CadPlotSettings.PageSetupNames(Document))
            {
                rows.Add(
                    new PageSetupRow(name, DetailsOf(name), string.Equals(name, _current, StringComparison.Ordinal))
                );
            }

            _items = rows;
            Raise(nameof(Items));
            Raise(nameof(CurrentText));
            Selected = rows.FirstOrDefault(r => r.Name == chosen);
        }

        private string DetailsOf(string name)
        {
            CadPlotSettings settings = CadPlotSettings.ReadPageSetup(Document, Space, name);

            if (settings == null)
            {
                return string.Empty;
            }

            string plotter =
                settings.IsRaster ? settings.DeviceName
                : settings.SavedPlotter.Length > 0 ? settings.SavedPlotter
                : "None";
            string scale = settings.FitToPaper
                ? "Fit to paper"
                : settings.PaperUnits.ToString("0.####", CultureInfo.InvariantCulture)
                    + ":"
                    + settings.DrawingUnits.ToString("0.####", CultureInfo.InvariantCulture);
            string table =
                settings.StyleTable == CadPlotStyleTable.Monochrome ? "monochrome.ctb"
                : settings.StyleTable == CadPlotStyleTable.Grayscale ? "grayscale.ctb"
                : "no plot style table";
            return settings.Paper.Label + " · " + plotter + " · " + settings.Area + " " + scale + " · " + table;
        }
    }
}
