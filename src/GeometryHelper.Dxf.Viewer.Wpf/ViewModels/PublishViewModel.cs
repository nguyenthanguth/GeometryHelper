using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A row of the Publish dialog's sheet list: the sheet's name (edited in place), the page setup it is plotted with (a list
    /// of the drawing's page setups, with Import… for one of another drawing) and its status.
    /// </summary>
    public sealed class PublishSheetRow : ObservableObject
    {
        /// <summary>The last entry of a Page Setup list, which asks for a page setup of another drawing.</summary>
        public const string ImportChoice = "Import…";

        private readonly PublishViewModel _owner;

        internal PublishSheetRow(PublishViewModel owner, CadPublishSheet sheet)
        {
            _owner = owner;
            Sheet = sheet;
        }

        /// <summary>Gets the sheet.</summary>
        public CadPublishSheet Sheet { get; }

        /// <summary>
        /// Gets or sets the name in the list. A name AutoCAD refuses (one with <c>&lt; &gt; / \ " : ? * |</c>, or one another
        /// sheet has) is not taken, and the dialog says why.
        /// </summary>
        public string Name
        {
            get => Sheet.Name;
            set
            {
                string refused = value == null ? null : _owner.List.Rename(Sheet, value);
                _owner.Say(refused);
                Raise(nameof(Name));
            }
        }

        /// <summary>Gets the layout the sheet is of: <c>Model</c> or the layout's name.</summary>
        public string LayoutName => Sheet.LayoutName;

        /// <summary>Gets the words of the layout's own page setup, as the list shows it: <c>&lt;Default: None&gt;</c>, or <c>&lt;Default: A4 sheet&gt;</c> where the layout is set to a page setup of that name.</summary>
        public string DefaultText =>
            "<Default: "
            + (
                Sheet.Document == null || Sheet.Space == null ? "None"
                : CadPlotSettings.CurrentPageSetupName(Sheet.Document, Sheet.Space) is string name && name.Length > 0
                    ? name
                : "None"
            )
            + ">";

        /// <summary>Gets the page setups to choose from: the layout's own, those of the sheet's drawing by name, the one imported where there is one, and Import….</summary>
        public IReadOnlyList<string> PageSetupChoices
        {
            get
            {
                var choices = new List<string> { DefaultText };
                choices.AddRange(_owner.List.PageSetupNames(Sheet));

                if (Sheet.PageSetupFile.Length > 0)
                {
                    choices.Add(ImportedText);
                }

                choices.Add(ImportChoice);
                return choices;
            }
        }

        private string ImportedText => "Imported: " + Sheet.PageSetup;

        /// <summary>
        /// Gets or sets the page setup shown: the layout's own, a page setup of the drawing, or the one imported. Choosing
        /// Import… asks the drawing to take one from.
        /// </summary>
        public string PageSetupChoice
        {
            get =>
                Sheet.PageSetup.Length == 0 ? DefaultText
                : Sheet.PageSetupFile.Length > 0 ? ImportedText
                : Sheet.PageSetup;
            set
            {
                if (value == null || value == PageSetupChoice)
                {
                    return;
                }

                if (value == ImportChoice)
                {
                    _owner.ImportPageSetup(this);
                }
                else
                {
                    _owner.Say(_owner.List.SetPageSetup(Sheet, value == DefaultText ? string.Empty : value));
                }

                Refresh();
            }
        }

        /// <summary>Gets how far the sheet is ready: <c>Loaded</c>, or why it is not.</summary>
        public string Status => Sheet.Status;

        internal void Refresh()
        {
            Raise(nameof(Name));
            Raise(nameof(PageSetupChoices));
            Raise(nameof(PageSetupChoice));
            Raise(nameof(Status));
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>The page setups of another drawing, listed for Import Page Setups for Publishing: the drawing and its page setups, one chosen.</summary>
    public sealed class PublishImportChoiceViewModel : ObservableObject
    {
        private string _selected;

        /// <summary>Makes the choice over the page setups of a drawing; the first is chosen.</summary>
        /// <param name="fileName">The drawing the page setups come from, as shown over the list.</param>
        /// <param name="names">The page setups the drawing has.</param>
        /// <exception cref="ArgumentNullException">Thrown when the file name or the names are null.</exception>
        public PublishImportChoiceViewModel(string fileName, IEnumerable<string> names)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            Names = (names ?? throw new ArgumentNullException(nameof(names))).ToList();
            _selected = Names.FirstOrDefault();
        }

        /// <summary>Gets the title: <c>Import Page Setups for Publishing</c>.</summary>
        public string Title => "Import Page Setups for Publishing";

        /// <summary>Gets the drawing the page setups come from.</summary>
        public string FileName { get; }

        /// <summary>Gets the page setups the drawing has.</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>Gets or sets the page setup chosen.</summary>
        public string Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        /// <summary>Gets whether OK is offered: a page setup is chosen.</summary>
        public bool CanOk => Selected != null;
    }

    /// <summary>
    /// AutoCAD 2021's Publish dialog: the list of sheets (Sheet Name, Page Setup, Status) filled from the drawing open, Add Sheets
    /// for other drawing files, Remove, Remove All, Move Up and Move Down, Load List and Save List for a sheet list file (.dsd),
    /// what is included when sheets are added (Model, Layouts), where they go (the plotter named in the page setup, or PDF), a PDF
    /// of a page for each sheet or a PDF each, the number of copies, the output folder and the file's name, Publish in
    /// background (a box that does nothing here) and Open in viewer when done. The dialog works on a <see cref="CadPublishList"/>;
    /// Publish checks it (<see cref="Accept"/>) and the window's writers carry it out.
    /// </summary>
    public sealed class PublishViewModel : ObservableObject
    {
        /// <summary>The Publish to entry that sends each sheet to the plotter its page setup names.</summary>
        public const string PlotterEntry = "Plotter named in page setup";

        /// <summary>The Publish to entry that writes PDF.</summary>
        public const string PdfEntry = "PDF";

        private PublishSheetRow _selected;
        private string _copiesText;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over a sheet list.</summary>
        /// <param name="list">The list and its options; the dialog edits it.</param>
        /// <exception cref="ArgumentNullException">Thrown when the list is null.</exception>
        public PublishViewModel(CadPublishList list)
        {
            List = list ?? throw new ArgumentNullException(nameof(list));
            _copiesText = list.Options.Copies.ToString(CultureInfo.InvariantCulture);
            Rows = new ObservableCollection<PublishSheetRow>();
            Reload();
        }

        /// <summary>Gets the sheet list the dialog edits.</summary>
        public CadPublishList List { get; }

        /// <summary>Gets the options of the list.</summary>
        public CadPublishOptions Options => List.Options;

        /// <summary>Gets the title: <c>Publish</c>.</summary>
        public string Title => "Publish";

        /// <summary>Gets the page of the guide the Help button opens.</summary>
        public string HelpUrl => PlotHelp.Publish;

        /// <summary>Gets the rows of the sheet list, in the order the sheets are published.</summary>
        public ObservableCollection<PublishSheetRow> Rows { get; }

        /// <summary>Gets or sets the row selected; null for none.</summary>
        public PublishSheetRow Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    RaiseSelection();
                }
            }
        }

        /// <summary>Gets whether a sheet is selected, so that Remove is offered.</summary>
        public bool CanRemove => _selected != null;

        /// <summary>Gets whether the selected sheet can move up.</summary>
        public bool CanMoveUp => _selected != null && Rows.IndexOf(_selected) > 0;

        /// <summary>Gets whether the selected sheet can move down.</summary>
        public bool CanMoveDown => _selected != null && Rows.IndexOf(_selected) < Rows.Count - 1;

        /// <summary>Gets whether the list has a sheet, so that Remove All and Save List are offered.</summary>
        public bool HasSheets => Rows.Count > 0;

        /// <summary>Gets the words under the list: what was refused or went wrong, or how many sheets are listed.</summary>
        public string Message => _message.Length > 0 ? _message : Count(Rows.Count, "sheet") + " listed";

        /// <summary>Gets whether the last thing told is a refusal, which the dialog shows in its warning colour.</summary>
        public bool HasProblem => _message.Length > 0;

        /// <summary>Gets what the Publish to list offers: the plotter named in the page setup, and PDF.</summary>
        public IReadOnlyList<string> PublishToItems { get; } = new[] { PlotterEntry, PdfEntry };

        /// <summary>Gets or sets where the sheets go, one of <see cref="PublishToItems"/>.</summary>
        public string PublishTo
        {
            get => Options.PublishTo == CadPublishTo.Pdf ? PdfEntry : PlotterEntry;
            set
            {
                if (value == null)
                {
                    return;
                }

                Options.PublishTo = value == PdfEntry ? CadPublishTo.Pdf : CadPublishTo.PlotterNamedInPageSetup;
                Raise(nameof(PublishTo));
                Raise(nameof(IsPdf));
                Raise(nameof(IsMultiSheetEnabled));
                Raise(nameof(IsCopiesEnabled));
                Raise(nameof(IsFileNameEnabled));
            }
        }

        /// <summary>Gets whether the sheets are written as PDF.</summary>
        public bool IsPdf => Options.PublishTo == CadPublishTo.Pdf;

        /// <summary>Gets whether the choice of one file or a file each is offered: only a PDF has it.</summary>
        public bool IsMultiSheetEnabled => IsPdf;

        /// <summary>Gets whether the number of copies is offered: only a sheet printed has copies.</summary>
        public bool IsCopiesEnabled => !IsPdf;

        /// <summary>Gets whether the name of the multi-sheet file is offered.</summary>
        public bool IsFileNameEnabled => IsPdf && Options.MultiSheet;

        /// <summary>Gets or sets whether the PDF is one file of a page for each sheet.</summary>
        public bool IsMultiSheet
        {
            get => Options.MultiSheet;
            set
            {
                if (Options.MultiSheet != value)
                {
                    Options.MultiSheet = value;
                    Raise(nameof(IsMultiSheet));
                    Raise(nameof(IsSingleSheet));
                    Raise(nameof(IsFileNameEnabled));
                }
            }
        }

        /// <summary>Gets or sets whether the PDF is a file for each sheet.</summary>
        public bool IsSingleSheet
        {
            get => !Options.MultiSheet;
            set => IsMultiSheet = !value;
        }

        /// <summary>Gets or sets the number of copies as typed; a number from 1 to 99 is taken, anything else is told when Publish is pressed.</summary>
        public string CopiesText
        {
            get => _copiesText;
            set
            {
                if (Set(ref _copiesText, value ?? string.Empty) && TryCopies(out int copies))
                {
                    Options.Copies = copies;
                }
            }
        }

        /// <summary>Gets or sets the folder the PDFs are written to as typed; empty for the first drawing's folder.</summary>
        public string OutputFolder
        {
            get => Options.OutputFolder;
            set
            {
                Options.OutputFolder = value?.Trim() ?? string.Empty;
                Raise(nameof(OutputFolder));
            }
        }

        /// <summary>Gets or sets the name of the multi-sheet PDF as typed; empty for the first drawing's name.</summary>
        public string FileName
        {
            get => Options.FileName;
            set
            {
                Options.FileName = value?.Trim() ?? string.Empty;
                Raise(nameof(FileName));
            }
        }

        /// <summary>Gets the file the multi-sheet PDF would be written to now, as the list works it out.</summary>
        public string FileNameHint => List.MultiSheetFile();

        /// <summary>Gets or sets whether Add Sheets lists the Model of a drawing.</summary>
        public bool IncludeModel
        {
            get => Options.IncludeModel;
            set
            {
                Options.IncludeModel = value;
                Raise(nameof(IncludeModel));
            }
        }

        /// <summary>Gets or sets whether Add Sheets lists the layouts of a drawing.</summary>
        public bool IncludeLayouts
        {
            get => Options.IncludeLayouts;
            set
            {
                Options.IncludeLayouts = value;
                Raise(nameof(IncludeLayouts));
            }
        }

        /// <summary>Gets or sets Publish in background. The box is kept and does nothing here: a publish runs at once.</summary>
        public bool PublishInBackground
        {
            get => Options.PublishInBackground;
            set
            {
                Options.PublishInBackground = value;
                Raise(nameof(PublishInBackground));
            }
        }

        /// <summary>Gets or sets whether each PDF is opened in a viewer when it is written.</summary>
        public bool OpenInViewer
        {
            get => Options.OpenInViewer;
            set
            {
                Options.OpenInViewer = value;
                Raise(nameof(OpenInViewer));
            }
        }

        /// <summary>Gets or sets how the window asks the drawing files Add Sheets lists, as its Select Drawings dialog: the paths chosen, or null where it was cancelled.</summary>
        public Func<IReadOnlyList<string>> AskSheetFiles { get; set; }

        /// <summary>Gets or sets how the window asks the sheet list file Load List reads, as its Load List of Sheets dialog: the path, or null where it was cancelled.</summary>
        public Func<string> AskLoadList { get; set; }

        /// <summary>
        /// Gets or sets how the window asks where Save List writes, as its Save List As dialog: given the file offered, the path
        /// chosen, or null where it was cancelled.
        /// </summary>
        public Func<string, string> AskSaveList { get; set; }

        /// <summary>Gets or sets how the window asks whether a list loaded replaces the sheets listed or is added to them: true to append, false to replace, null to cancel.</summary>
        public Func<bool?> AskAppend { get; set; }

        /// <summary>Gets or sets how the window asks the folder the PDFs go to, given the folder now; the path chosen, or null where it was cancelled.</summary>
        public Func<string, string> AskOutputFolder { get; set; }

        /// <summary>Gets or sets how the window asks the drawing Import… takes a page setup from: the path, or null where it was cancelled.</summary>
        public Func<string> AskPageSetupFile { get; set; }

        /// <summary>Gets or sets how the window shows Import Page Setups for Publishing: true where OK was pressed.</summary>
        public Func<PublishImportChoiceViewModel, bool> ShowImportChoice { get; set; }

        private static string Count(int n, string what) =>
            n.ToString(CultureInfo.InvariantCulture) + " " + what + (n == 1 ? string.Empty : "s");

        internal void Say(string problem)
        {
            if (!string.IsNullOrEmpty(problem) || _message.Length > 0)
            {
                _message = problem ?? string.Empty;
                Raise(nameof(Message));
                Raise(nameof(HasProblem));
            }
        }

        private bool TryCopies(out int copies) =>
            int.TryParse(_copiesText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out copies)
            && copies >= 1
            && copies <= 99;

        /// <summary>Makes the rows again from the list, keeping the sheet selected where it is still there.</summary>
        private void Reload()
        {
            CadPublishSheet keep = _selected?.Sheet;
            Rows.Clear();

            foreach (CadPublishSheet sheet in List.Sheets)
            {
                Rows.Add(new PublishSheetRow(this, sheet));
            }

            Selected = Rows.FirstOrDefault(r => ReferenceEquals(r.Sheet, keep));
            RaiseSelection();
        }

        private void RaiseSelection()
        {
            Raise(nameof(CanRemove));
            Raise(nameof(CanMoveUp));
            Raise(nameof(CanMoveDown));
            Raise(nameof(HasSheets));
            Raise(nameof(Message));
            Raise(nameof(FileNameHint));
        }

        /// <summary>
        /// Add Sheets: asks the drawing files and adds their Model and layouts as <see cref="CadPublishOptions.IncludeModel"/> and
        /// <see cref="CadPublishOptions.IncludeLayouts"/> say. A drawing that cannot be read is told, and the others are
        /// still added.
        /// </summary>
        /// <returns>The number of sheets added.</returns>
        public int AddSheets()
        {
            IReadOnlyList<string> files = AskSheetFiles?.Invoke();

            if (files == null || files.Count == 0)
            {
                return 0;
            }

            var problems = new List<string>();
            int added = 0;

            foreach (string file in files)
            {
                added += List.AddDrawing(file, out string problem).Count;

                if (problem != null)
                {
                    problems.Add(Path.GetFileName(file) + ": " + problem);
                }
            }

            Reload();
            Say(problems.Count == 0 ? null : string.Join(" ", problems));
            return added;
        }

        /// <summary>Remove: takes the selected sheet out of the list; the sheet after it, or the last, is selected.</summary>
        public void Remove()
        {
            if (_selected == null)
            {
                return;
            }

            int at = Rows.IndexOf(_selected);
            List.Remove(_selected.Sheet);
            Reload();
            Selected = Rows.Count == 0 ? null : Rows[Math.Min(at, Rows.Count - 1)];
        }

        /// <summary>Remove All: empties the list.</summary>
        public void RemoveAll()
        {
            List.RemoveAll();
            Reload();
        }

        /// <summary>Move Up: moves the selected sheet one place earlier.</summary>
        public void MoveUp() => Move(List.MoveUp);

        /// <summary>Move Down: moves the selected sheet one place later.</summary>
        public void MoveDown() => Move(List.MoveDown);

        private void Move(Func<CadPublishSheet, bool> move)
        {
            if (_selected != null && move(_selected.Sheet))
            {
                Reload();
            }
        }

        /// <summary>
        /// Load List: asks the sheet list file and whether it replaces the sheets listed or is added to them, and loads it,
        /// with the options it holds. A file that cannot be read is told as AutoCAD does, "Error reading list file."
        /// </summary>
        /// <returns>True where a list was loaded.</returns>
        public bool LoadList()
        {
            string file = AskLoadList?.Invoke();

            if (file == null)
            {
                return false;
            }

            bool append = false;

            if (List.Sheets.Count > 0)
            {
                bool? answer = AskAppend?.Invoke();

                if (answer == null && AskAppend != null)
                {
                    return false;
                }

                append = answer == true;
            }

            try
            {
                IReadOnlyList<string> notes = List.Load(file, append);
                _copiesText = Options.Copies.ToString(CultureInfo.InvariantCulture);
                Reload();

                foreach (
                    string name in new[]
                    {
                        nameof(PublishTo),
                        nameof(IsPdf),
                        nameof(IsMultiSheet),
                        nameof(IsSingleSheet),
                        nameof(IsMultiSheetEnabled),
                        nameof(IsCopiesEnabled),
                        nameof(IsFileNameEnabled),
                        nameof(CopiesText),
                        nameof(OutputFolder),
                        nameof(FileName),
                        nameof(OpenInViewer),
                    }
                )
                {
                    Raise(name);
                }

                Say(notes.Count == 0 ? null : string.Join(" ", notes));
                return true;
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is FormatException
                    || exception is UnauthorizedAccessException
                )
            {
                GeometryHelperLog.Warn("PublishViewModel: the sheet list " + file + " could not be read.", exception);
                Say("Error reading list file.");
                return false;
            }
        }

        /// <summary>Save List: asks the file, offered as the first drawing's name, and writes the sheets and options to it.</summary>
        /// <returns>True where the list was written.</returns>
        public bool SaveList()
        {
            string first = List.Sheets.Select(s => s.DrawingFile).FirstOrDefault(f => f.Length > 0);
            string offered =
                first == null ? Path.Combine(List.OutputFolder(), "Publish.dsd") : Path.ChangeExtension(first, ".dsd");
            string file = AskSaveList?.Invoke(offered);

            if (file == null)
            {
                return false;
            }

            try
            {
                List.Save(file.EndsWith(".dsd", StringComparison.OrdinalIgnoreCase) ? file : file + ".dsd");
                Say(null);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn(
                    "PublishViewModel: the sheet list " + file + " could not be written.",
                    exception
                );
                Say("Error writing list.");
                return false;
            }
        }

        /// <summary>Browse for the output folder: asks it and sets it.</summary>
        public void BrowseOutputFolder()
        {
            string folder = AskOutputFolder?.Invoke(List.OutputFolder());

            if (!string.IsNullOrEmpty(folder))
            {
                OutputFolder = folder;
            }
        }

        /// <summary>
        /// Import… in a sheet's Page Setup list: asks the drawing, lists its page setups in Import Page Setups for Publishing
        /// and sets the sheet to the one chosen. A drawing with none, or one that cannot be read, is told.
        /// </summary>
        /// <param name="row">The sheet.</param>
        internal void ImportPageSetup(PublishSheetRow row)
        {
            string file = AskPageSetupFile?.Invoke();

            if (file == null)
            {
                return;
            }

            IReadOnlyList<string> names = List.ImportablePageSetups(file, out string problem);

            if (problem != null)
            {
                Say(problem);
                return;
            }

            if (names.Count == 0)
            {
                Say("The drawing " + Path.GetFileName(file) + " has no page setups.");
                return;
            }

            var choice = new PublishImportChoiceViewModel(file, names);

            if (names.Count > 1 && (ShowImportChoice == null || !ShowImportChoice(choice) || !choice.CanOk))
            {
                return;
            }

            Say(List.SetPageSetup(row.Sheet, choice.Selected, file));
        }

        /// <summary>
        /// Publish: checks what the dialog holds before it is carried out: the number of copies a number from 1 to 99, and a
        /// sheet that can be published.
        /// </summary>
        /// <returns>True where the list may be published; else <see cref="Message"/> says what is wrong.</returns>
        public bool Accept()
        {
            if (!TryCopies(out _))
            {
                Say("The number of copies must be between 1 and 99.");
                return false;
            }

            if (!List.Sheets.Any(s => s.IsLoaded))
            {
                Say("There were no plottable sheets in the current operation.");
                return false;
            }

            Say(null);
            return true;
        }
    }
}
