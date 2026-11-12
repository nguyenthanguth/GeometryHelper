using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// How a page setup plots, as the "Selected page setup details" of the Page Setup Manager and of Import Page Setups list it:
    /// the device name, the plotter, the plot size with its orientation, where it writes and what it does.
    /// </summary>
    public sealed class PageSetupInfo
    {
        internal PageSetupInfo(string deviceName, string plotter, string plotSize, string where, string description)
        {
            DeviceName = deviceName;
            Plotter = plotter;
            PlotSize = plotSize;
            Where = where;
            Description = description;
        }

        /// <summary>Gets the details of no page setup: every field empty.</summary>
        internal static PageSetupInfo Empty { get; } =
            new PageSetupInfo(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        /// <summary>Gets the device name: the name of the plotter the page setup names, <c>GeometryHelper PDF</c>, <c>Office printer</c>, or None.</summary>
        public string DeviceName { get; }

        /// <summary>Gets what the plotter is: "GeometryHelper PDF · vector, built in", "Office printer · printer of Windows".</summary>
        public string Plotter { get; }

        /// <summary>Gets the size of the sheet with its orientation: "594.00 x 420.00 mm (Landscape)", "1920 x 1080 pixels (Landscape)".</summary>
        public string PlotSize { get; }

        /// <summary>Gets where it writes: File, or for a printer of Windows its port or share.</summary>
        public string Where { get; }

        /// <summary>Gets what the plotter does, in a sentence.</summary>
        public string Description { get; }
    }

    /// <summary>
    /// A row of the Page Setup Manager's list: the layout's own setup, named <c>*Layout1*</c> or <c>*Layout1 (A2 plan)*</c> when
    /// a named page setup is current, or a page setup of the drawing by name. It carries a line on how it plots, the details
    /// of the grid under the list, and the state of its name being edited in place by Rename.
    /// </summary>
    public sealed class PageSetupRow : ObservableObject
    {
        private bool _isEditing;
        private string _editText = string.Empty;

        internal PageSetupRow(string name, string details, bool isCurrent, bool isLayout, PageSetupInfo info)
        {
            Name = name;
            Details = details;
            IsCurrent = isCurrent;
            IsLayout = isLayout;
            Info = info;
        }

        /// <summary>Gets the name: <c>*Layout1*</c> for the layout's own setup, else the name of the page setup.</summary>
        public string Name { get; }

        /// <summary>Gets how it plots, as "ISO A3 (420.00 x 297.00 MM) · GeometryHelper PDF · Window 1:20 · monochrome.ctb".</summary>
        public string Details { get; }

        /// <summary>Gets whether the space the manager was opened on is set to it; true for the layout's own setup.</summary>
        public bool IsCurrent { get; }

        /// <summary>Gets whether this is the layout's own setup, the first row, which cannot be renamed or deleted.</summary>
        public bool IsLayout { get; }

        /// <summary>Gets how it plots, for the grid of details under the list.</summary>
        public PageSetupInfo Info { get; }

        /// <summary>Gets whether the name is being edited in the list (Rename).</summary>
        public bool IsEditing
        {
            get => _isEditing;
            internal set => Set(ref _isEditing, value);
        }

        /// <summary>Gets or sets the name as typed while it is edited.</summary>
        public string EditText
        {
            get => _editText;
            set => Set(ref _editText, value ?? string.Empty);
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD's Page Setup Manager, which PAGESETUP shows: the layout's own setup as the first row and the page setups the
    /// drawing keeps by name below it, with the details of the one chosen. Set Current copies the one chosen into the space's
    /// layout; New… asks a name and what to start with, then the Page Setup dialog, and adds the page setup when OK is pressed;
    /// Modify… edits the one chosen in the same dialog (the layout's own row edits the layout); Import… copies page setups of
    /// another drawing in; Rename and Delete, on the list's right-click menu, rename and delete a named page setup. Each is
    /// one undo step named PAGESETUP, but for Modify… of the layout's row, which is PLOT as Apply to Layout is. The dialogs it
    /// opens are functions the window sets, so that a test drives it with none.
    /// </summary>
    public sealed class PageSetupManagerViewModel : ObservableObject
    {
        private const string None = "<None>";
        private const string DefaultDevice = "<Default output device>";
        private const string PreviousPlotChoice = "<Previous plot>";
        private const string DisplayVariable = "showpagesetupfornewlayouts";

        private readonly List<IPlotPrinter> _printers;
        private readonly List<CadPaper> _customSizes;
        private IReadOnlyList<PageSetupRow> _items = Array.Empty<PageSetupRow>();
        private PageSetupRow _selected;
        private CadCommandSession _session;
        private bool _display;
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
            Refresh(null);
        }

        /// <summary>Gets or sets how the window asks a name (New Page Setup): true where OK was pressed. Null for none, when New… does nothing.</summary>
        public Func<PageSetupNameViewModel, bool> ShowName { get; set; }

        /// <summary>Gets or sets how the window shows the Page Setup dialog on a page setup: true where OK was pressed. Null for none.</summary>
        public Func<PlotDialogViewModel, bool> ShowPageSetup { get; set; }

        /// <summary>Gets or sets how the window asks the drawing to import page setups from, as its Select File dialog: the path, or null where it was cancelled.</summary>
        public Func<string> AskImportFile { get; set; }

        /// <summary>Gets or sets how the window shows Import Page Setups: true where OK was pressed. Null for none.</summary>
        public Func<ImportPageSetupsViewModel, bool> ShowImport { get; set; }

        /// <summary>
        /// Gets or sets the settings of the last plot made, which New… can start with (&lt;Previous plot&gt;); null, or a function
        /// that returns null, where none has been made, when it starts with the defaults.
        /// </summary>
        public Func<CadPlotSettings> PreviousPlot { get; set; }

        /// <summary>
        /// Gets or sets the command session of the drawing, which keeps "Display when creating a new layout" as the system
        /// variable SHOWPAGESETUPFORNEWLAYOUTS. Null for none, when the box is kept by the manager alone.
        /// </summary>
        public CadCommandSession Session
        {
            get => _session;
            set
            {
                if (Set(ref _session, value))
                {
                    Raise(nameof(DisplayWhenCreatingLayout));
                }
            }
        }

        /// <summary>Raised when the Page Setup dialog made a size of an image of the user's own, so that the window keeps it between sessions.</summary>
        public event EventHandler<CadPaper> CustomSizeMade;

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the space whose layout the page setups are set on.</summary>
        public CadSpace Space { get; }

        /// <summary>Gets the title.</summary>
        public string Title => "Page Setup Manager";

        /// <summary>Gets the page of the guide the Help button opens.</summary>
        public string HelpUrl => PlotHelp.PageSetups;

        /// <summary>Gets the words over the list: "Current layout: Model".</summary>
        public string LayoutText => "Current layout: " + Space.Name;

        /// <summary>Gets the page setup the space is set to, or &lt;None&gt;: "Current page setup: A3 plan".</summary>
        public string CurrentText => "Current page setup: " + (_current.Length == 0 ? None : _current);

        /// <summary>Gets the layout's own setup first, named <c>*Layout1*</c>, then the page setups of the drawing, by name.</summary>
        public IReadOnlyList<PageSetupRow> Items => _items;

        /// <summary>Gets or sets the page setup chosen in the list, the layout's own to begin with; null for none.</summary>
        public PageSetupRow Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(CanSetCurrent));
                    Raise(nameof(CanModify));
                    Raise(nameof(CanRename));
                    Raise(nameof(CanDelete));
                    Raise(nameof(DetailsText));
                    Raise(nameof(SelectedInfo));
                }
            }
        }

        /// <summary>Gets how the page setup chosen plots, in one line; empty for none.</summary>
        public string DetailsText => _selected?.Details ?? string.Empty;

        /// <summary>Gets how the page setup chosen plots, in the grid of "Selected page setup details"; empty fields for none.</summary>
        public PageSetupInfo SelectedInfo => _selected?.Info ?? PageSetupInfo.Empty;

        /// <summary>Gets whether Set Current is offered: a named page setup is chosen and the space is not set to it already.</summary>
        public bool CanSetCurrent => _selected != null && !_selected.IsLayout && !_selected.IsCurrent;

        /// <summary>Gets whether Modify… is offered: a page setup is chosen, the layout's own included.</summary>
        public bool CanModify => _selected != null;

        /// <summary>Gets whether Rename is offered: a named page setup is chosen; the layout's own row cannot be renamed.</summary>
        public bool CanRename => _selected != null && !_selected.IsLayout;

        /// <summary>Gets whether Delete is offered: a named page setup is chosen; the layout's own row cannot be deleted.</summary>
        public bool CanDelete => _selected != null && !_selected.IsLayout;

        /// <summary>
        /// Gets or sets "Display when creating a new layout", the box at the foot of the manager: unticked to begin with. It is the
        /// system variable SHOWPAGESETUPFORNEWLAYOUTS of the <see cref="Session"/>; the presenter makes no layouts of its own, so
        /// it only keeps the choice.
        /// </summary>
        public bool DisplayWhenCreatingLayout
        {
            get =>
                _session != null
                    ? _session.Remembered.TryGetValue(DisplayVariable, out double value) && value != 0.0
                    : _display;
            set
            {
                if (_session != null)
                {
                    _session.Remembered[DisplayVariable] = value ? 1.0 : 0.0;
                }
                else
                {
                    _display = value;
                }

                Raise(nameof(DisplayWhenCreatingLayout));
            }
        }

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
        /// <returns>What came of the edit; null where no named page setup is chosen.</returns>
        public CadEditResult SetCurrent()
        {
            if (_selected == null || _selected.IsLayout)
            {
                return null;
            }

            return Finish(CadPlotSettings.SetCurrentPageSetup(Document, Space, _selected.Name), null);
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

        /// <summary>
        /// Edits the page setup chosen in the Page Setup dialog, as Modify… does, and writes it back when OK is pressed there.
        /// The layout's own row edits the layout, as Apply to Layout writes it.
        /// </summary>
        /// <returns>What came of the edit; null where it was cancelled, none is chosen or the window shows no dialog.</returns>
        public CadEditResult Modify()
        {
            if (_selected == null)
            {
                return null;
            }

            if (_selected.IsLayout)
            {
                return EditLayout();
            }

            CadPlotSettings settings = CadPlotSettings.ReadPageSetup(Document, Space, _selected.Name);
            return settings == null
                ? Finish(CadEditResult.Refuse("The drawing has no page setup named " + _selected.Name + "."), null)
                : Edit(_selected.Name, settings);
        }

        /// <summary>
        /// Starts renaming the page setup chosen in the list, as Rename on the right-click menu does: its name becomes a box to
        /// type in, <see cref="CommitRename"/> taking the new name and <see cref="CancelRename"/> leaving the old.
        /// </summary>
        /// <returns>True when the name is open to edit; false where no named page setup is chosen.</returns>
        public bool BeginRename()
        {
            if (!CanRename)
            {
                return false;
            }

            CancelRename();
            _selected.EditText = _selected.Name;
            _selected.IsEditing = true;
            return true;
        }

        /// <summary>Takes the name typed in the list as the new name of the page setup being renamed, one undo step.</summary>
        /// <returns>What came of the edit, refused with a reason where AutoCAD refuses the name or the drawing has it; null where no name is being edited or it was left as it was.</returns>
        public CadEditResult CommitRename()
        {
            PageSetupRow row = _items.FirstOrDefault(r => r.IsEditing);

            if (row == null)
            {
                return null;
            }

            string typed = (row.EditText ?? string.Empty).Trim();
            row.IsEditing = false;

            if (string.Equals(typed, row.Name, StringComparison.Ordinal))
            {
                return null;
            }

            CadEditResult result = CadPlotSettings.RenamePageSetup(Document, row.Name, typed);
            return Finish(result, result.Succeeded ? typed : null);
        }

        /// <summary>Leaves the name being edited as it was.</summary>
        public void CancelRename()
        {
            foreach (PageSetupRow row in _items)
            {
                row.IsEditing = false;
            }
        }

        /// <summary>Deletes the page setup chosen, as Delete on the right-click menu does, one undo step.</summary>
        /// <returns>What came of the edit; null where no named page setup is chosen.</returns>
        public CadEditResult Delete()
        {
            if (!CanDelete)
            {
                return null;
            }

            return Finish(CadPlotSettings.DeletePageSetup(Document, _selected.Name), null);
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
                    ),
                    null
                );
            }

            IReadOnlyList<string> names = CadPlotSettings.PageSetupNames(source);

            if (names.Count == 0)
            {
                return Finish(
                    CadEditResult.Refuse("The drawing " + Path.GetFileName(path) + " has no page setups."),
                    null
                );
            }

            var chosen = new ImportPageSetupsViewModel(
                path,
                names.Select(name => new ImportPageSetupRow(
                    name,
                    true,
                    CadPlotSettings.PageSetupIsModel(source, name) ? "Model" : "Layout",
                    InfoOf(CadPlotSettings.ReadPageSetup(source, name, Space.IsModel))
                ))
            );

            if (!ShowImport(chosen) || !chosen.CanOk)
            {
                return null;
            }

            return Finish(CadPlotSettings.ImportPageSetups(Document, source, chosen.Chosen), null);
        }

        /// <summary>The Page Setup dialog on a page setup of a name, its OK writing the page setup under that name.</summary>
        private CadEditResult Edit(string name, CadPlotSettings settings)
        {
            if (ShowPageSetup == null)
            {
                return null;
            }

            PlotDialogViewModel dialog = Dialog(settings, name);
            return ShowPageSetup(dialog) ? Finish(dialog.SaveToPageSetup(), null) : null;
        }

        /// <summary>The Page Setup dialog on the layout's own setup, its OK writing the layout.</summary>
        private CadEditResult EditLayout()
        {
            if (ShowPageSetup == null)
            {
                return null;
            }

            PlotDialogViewModel dialog = Dialog(CadPlotSettings.Read(Document, Space), null);
            return ShowPageSetup(dialog) ? Finish(dialog.ApplyToLayout(), null) : null;
        }

        private PlotDialogViewModel Dialog(CadPlotSettings settings, string name)
        {
            var job = new CadPlotJob(Space, settings, DisplayBox.Empty, null);
            var dialog = new PlotDialogViewModel(Document, job, true, _printers, _customSizes, name)
            {
                OpenedFromManager = true,
            };
            dialog.CustomSizeMade += (s, size) => CustomSizeMade?.Invoke(this, size);
            return dialog;
        }

        /// <summary>
        /// What a new page setup can start with: &lt;None&gt;, &lt;Default output device&gt;, &lt;Previous plot&gt;, the layout's own
        /// setup as the first row names it, then the drawing's page setups.
        /// </summary>
        private IReadOnlyList<string> StartWithChoices()
        {
            var choices = new List<string> { None, DefaultDevice, PreviousPlotChoice, _items[0].Name };
            choices.AddRange(CadPlotSettings.PageSetupNames(Document));
            return choices;
        }

        /// <summary>The settings a new page setup starts from.</summary>
        private CadPlotSettings StartSettings(string choice)
        {
            if (string.Equals(choice, _items[0].Name, StringComparison.Ordinal))
            {
                return CadPlotSettings.Read(Document, Space);
            }

            if (choice == PreviousPlotChoice)
            {
                CadPlotSettings previous = PreviousPlot?.Invoke();

                if (previous != null)
                {
                    return previous.Clone();
                }
            }

            CadPlotSettings named =
                choice == null || choice == None || choice == DefaultDevice || choice == PreviousPlotChoice
                    ? null
                    : CadPlotSettings.ReadPageSetup(Document, Space, choice);

            if (named != null)
            {
                return named;
            }

            // The defaults of a sheet in the space, a layout's plotting its paper at 1:1.
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
        private CadEditResult Finish(CadEditResult result, string choose)
        {
            Message = result.Succeeded ? null : result.Message;
            Refresh(choose);
            return result;
        }

        /// <summary>The list and the current page setup read again from the drawing, the choice kept by name or the name given.</summary>
        private void Refresh(string choose)
        {
            bool layoutChosen = _selected == null || _selected.IsLayout;
            string chosen = choose ?? _selected?.Name;
            _current = CadPlotSettings.CurrentPageSetupName(Document, Space);
            CadPlotSettings own = CadPlotSettings.Read(Document, Space);
            var rows = new List<PageSetupRow>
            {
                new PageSetupRow(
                    "*" + Space.Name + (_current.Length == 0 ? string.Empty : " (" + _current + ")") + "*",
                    DetailsOf(own),
                    true,
                    true,
                    InfoOf(own)
                ),
            };

            foreach (string name in CadPlotSettings.PageSetupNames(Document))
            {
                CadPlotSettings settings = CadPlotSettings.ReadPageSetup(Document, Space, name);
                rows.Add(
                    new PageSetupRow(
                        name,
                        settings == null ? string.Empty : DetailsOf(settings),
                        string.Equals(name, _current, StringComparison.Ordinal),
                        false,
                        InfoOf(settings)
                    )
                );
            }

            _items = rows;
            Raise(nameof(Items));
            Raise(nameof(CurrentText));
            Selected =
                layoutChosen && choose == null
                    ? rows[0]
                    : rows.Skip(1).FirstOrDefault(r => r.Name == chosen) ?? rows[0];
        }

        private static string DetailsOf(CadPlotSettings settings)
        {
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

        /// <summary>
        /// How a page setup plots, for the grid of details: the plotter it names, a printer of this computer's list being a
        /// printer of Windows, the built-in writers what they are, and any other name a printer this computer lacks.
        /// </summary>
        private PageSetupInfo InfoOf(CadPlotSettings settings)
        {
            if (settings == null)
            {
                return PageSetupInfo.Empty;
            }

            string device =
                settings.IsRaster || settings.IsPrinter ? settings.DeviceName
                : settings.SavedPlotter.Length > 0 ? settings.SavedPlotter
                : "None";
            bool landscape = settings.Landscape;
            double longer = Math.Max(settings.Paper.Width, settings.Paper.Height);
            double shorter = Math.Min(settings.Paper.Width, settings.Paper.Height);
            string size = settings.Paper.IsPixels
                ? (landscape ? longer : shorter).ToString("0", CultureInfo.InvariantCulture)
                    + " x "
                    + (landscape ? shorter : longer).ToString("0", CultureInfo.InvariantCulture)
                    + " pixels"
                : (landscape ? longer : shorter).ToString("0.00", CultureInfo.InvariantCulture)
                    + " x "
                    + (landscape ? shorter : longer).ToString("0.00", CultureInfo.InvariantCulture)
                    + " mm";
            size += landscape ? " (Landscape)" : " (Portrait)";

            if (device == "None")
            {
                return new PageSetupInfo(
                    device,
                    device,
                    size,
                    "—",
                    "No output device is named: the plot goes to GeometryHelper PDF."
                );
            }

            IPlotPrinter printer =
                settings.IsRaster
                || string.Equals(device, CadPlotSettings.PdfDevice, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : _printers.FirstOrDefault(p => string.Equals(p.Name, device, StringComparison.OrdinalIgnoreCase));

            if (printer != null)
            {
                return new PageSetupInfo(
                    device,
                    PlotDeviceText.Plotter(device, false, true),
                    size,
                    PlotDeviceText.Where(printer),
                    PlotDeviceText.Description(device, true)
                );
            }

            if (
                settings.IsRaster
                || string.Equals(device, CadPlotSettings.PdfDevice, StringComparison.OrdinalIgnoreCase)
            )
            {
                return new PageSetupInfo(
                    device,
                    PlotDeviceText.Plotter(device, settings.IsRaster, false),
                    size,
                    "File",
                    PlotDeviceText.Description(device, false)
                );
            }

            return new PageSetupInfo(
                device,
                device + " · not on this computer",
                size,
                "—",
                "This computer has no such printer: the page setup plots with GeometryHelper PDF."
            );
        }
    }
}
