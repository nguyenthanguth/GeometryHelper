using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GeometryHelper.Drawing.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The model drawing in the presenter (Part B), with no window: the drawings made and shown, the Drawing ribbon tab and its
    /// commands, the commands of the command line, what the file list and Properties say of a model drawing, and the dialogs
    /// it asks the window to show. It is a <see cref="CadWorkspaceExtension"/> of the presenter's <see cref="MainViewModel"/>,
    /// added to it when made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The commands are DRAWINGNEW (DN), DRAWINGUPDATE (DU), DRAWINGSETTINGS (DS), EXPORTDXF (EXD), AUTODIM (AD), AUTOMARK
    /// (AM), VIEWSECTION (VS), VIEWDETAIL (VD) and PLUGIN, which runs a drawing plugin; Ctrl+N and the start screen's button
    /// make a new drawing too. A command that needs a model drawing says so on the command line when a plain DXF is shown.
    /// </para>
    /// <para>
    /// A dialog is asked for through <see cref="DialogRequested"/> with its view model; the window shows it after the command
    /// that asked has ended. A drawing of the beam B1 made with the assembly preset is listed under "Test bodies · model
    /// drawings" as B-101, its Drawing tab shown while it is, and Properties with nothing selected shows its number, name,
    /// revision, preset, state, sheet and source.
    /// </para>
    /// <para>
    /// Once Global settings are applied, as the app does when it starts, a drawing made is saved at once into their save
    /// folder, or where that is empty into "GeometryHelper Drawing" in the user's Documents, the folder made where it is
    /// not there: named by its number, B-101.dxf, or B-101 (2).dxf where a file of that name is there already, so that no
    /// file is written over. It then has a file, which Save writes to and autosave keeps copies beside.
    /// </para>
    /// </remarks>
    public sealed partial class DrawingWorkspace : CadWorkspaceExtension
    {
        /// <summary>The source of a drawing opened from its file, which does not keep where it was made from.</summary>
        private static readonly IDrawingSourceProvider OpenedFromFile = new UnconnectedSourceProvider(
            "Opened from a file",
            "A model drawing opened from its DXF, which does not keep the source it was made from",
            "Not connected · choose the source in Update from model"
        );

        // The names of what a drawing's file keeps of where it was made from (DrawingDocumentStore.WriteValues).
        private const string SourceKey = "Source";
        private const string PresetKey = "Preset";
        private const string WholeAssemblyKey = "WholeAssembly";

        private readonly List<IDrawingSourceProvider> _providers;
        private readonly ObservableCollection<ModelDrawing> _drawings = new ObservableCollection<ModelDrawing>();
        private DrawingPluginCatalog _plugins;

        // The drawings being made again off the UI thread: one change of a drawing at a time.
        private readonly HashSet<ModelDrawing> _busy = new HashSet<ModelDrawing>();
        private ModelDrawing _current;
        private bool _settingsApplied;

        /// <summary>Makes the model drawing of a presenter and adds it to the presenter.</summary>
        /// <param name="model">The presenter's view model.</param>
        /// <param name="providers">The sources drawings are made from, in the order the New drawing dialog lists them.</param>
        /// <param name="tolerance">The tolerance drawings are made within.</param>
        /// <exception cref="ArgumentNullException">Thrown when the presenter or the sources are null.</exception>
        public DrawingWorkspace(MainViewModel model, IEnumerable<IDrawingSourceProvider> providers, Tolerance tolerance)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _providers = new List<IDrawingSourceProvider>(
                providers ?? throw new ArgumentNullException(nameof(providers))
            );
            Tolerance = tolerance;
            Drawings = new ReadOnlyObservableCollection<ModelDrawing>(_drawings);

            NewCommand = new DelegateCommand(_ => ShowNewDrawing());
            UpdateCommand = new DelegateCommand(_ => ShowUpdate(), _ => _current != null);
            SettingsCommand = new DelegateCommand(_ => ShowSettings(), _ => _current != null);
            AddViewCommand = new DelegateCommand(
                p => _ = AddViewAsync(p is ViewKind kind ? kind : ParseKind(p as string)),
                _ => _current != null
            );
            SectionCommand = new DelegateCommand(_ => Model.StartCommand("VIEWSECTION"), _ => _current != null);
            DetailCommand = new DelegateCommand(_ => Model.StartCommand("VIEWDETAIL"), _ => _current != null);
            AutoDimensionCommand = new DelegateCommand(_ => ReDimensionSelectedView(), _ => _current != null);
            AutoMarksCommand = new DelegateCommand(_ => ReMarkSelectedView(), _ => _current != null);
            CentreLinesCommand = new DelegateCommand(_ => ReCentreLinesSelectedView(), _ => _current != null);
            ExportDxfCommand = new DelegateCommand(_ => ShowExport(), _ => _drawings.Count > 0);
            RunPluginCommand = new DelegateCommand(_ => ShowRunPlugin(), _ => _current != null);

            NewDocumentOffer = new CadNewDocumentOffer(
                "Drawing from a model",
                "New drawing from a model…  ·  Ctrl+N",
                "Make a drawing from the objects of a model (DRAWINGNEW)",
                "Open and edit any 2D drawing, or make drawings from a model. No file is open.",
                "Welcome. Ctrl+O opens a DXF, Ctrl+N makes a drawing from a model.",
                () => ShowNewDrawing()
            );
            model.SettingsApplied += (s, e) => _settingsApplied = true;
            model.PropertyChanged += OnModelChanged;
            model.SettingsSources = _providers
                .Select(p => new CadSourceInfo(p.Name, p.AvailabilityText, p.IsAvailable))
                .ToList();
            model.AddExtension(this);
        }

        /// <summary>Raised when a dialog is to be shown: the window shows it over the presenter, after the command that asked has ended.</summary>
        public event EventHandler<DrawingDialogRequestEventArgs> DialogRequested;

        /// <summary>Gets "Drawing", the ribbon tab shown while a model drawing is.</summary>
        public override string TabHeader => "Drawing";

        /// <summary>Gets "Model drawings", the chip of the file list.</summary>
        public override string FileKindName => "Model drawings";

        /// <summary>
        /// Gets "Drawing from a model", the new document the start screen, the New entry of the application menu and Ctrl+N
        /// offer: it shows the New drawing dialog (DRAWINGNEW).
        /// </summary>
        public override CadNewDocumentOffer NewDocumentOffer { get; }

        /// <summary>Gets the presenter's view model.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets the sources, in the order the New drawing dialog lists them.</summary>
        public IReadOnlyList<IDrawingSourceProvider> Providers => _providers;

        /// <summary>Gets the tolerance drawings are made within.</summary>
        public Tolerance Tolerance { get; }

        /// <summary>
        /// Gets or sets the drawing plugins every drawing made or opened here runs its plugin runs again with on update
        /// (<see cref="Drawing.Plugins"/>); null for none. Set, it is given to the drawings open too.
        /// </summary>
        public DrawingPluginCatalog Plugins
        {
            get => _plugins;
            set
            {
                _plugins = value;

                foreach (ModelDrawing entry in _drawings)
                {
                    entry.Drawing.Plugins = value;
                }
            }
        }

        /// <summary>Gets the model drawings made or opened, in the order they were.</summary>
        public ReadOnlyObservableCollection<ModelDrawing> Drawings { get; }

        /// <summary>Gets the model drawing shown; null while a plain DXF, or nothing, is.</summary>
        public ModelDrawing Current
        {
            get => _current;
            private set
            {
                if (Set(ref _current, value))
                {
                    IsTabVisible = value != null;
                    Raise(nameof(HasCurrent));
                    Raise(nameof(AddableViews));

                    foreach (
                        ICommand command in new[]
                        {
                            UpdateCommand,
                            SettingsCommand,
                            AddViewCommand,
                            SectionCommand,
                            DetailCommand,
                            AutoDimensionCommand,
                            AutoMarksCommand,
                            CentreLinesCommand,
                            RunPluginCommand,
                        }
                    )
                    {
                        ((DelegateCommand)command).RaiseCanExecuteChanged();
                    }
                }
            }
        }

        /// <summary>Gets whether a model drawing is shown.</summary>
        public bool HasCurrent => _current != null;

        /// <summary>Gets the views along the axes, and the isometric one, the drawing shown has not: those Add view offers.</summary>
        public IReadOnlyList<ViewKind> AddableViews
        {
            get
            {
                if (_current == null)
                {
                    return Array.Empty<ViewKind>();
                }

                IList<ViewKind> kinds = _current.Drawing.Settings.Views.Kinds;
                return new[]
                {
                    ViewKind.Front,
                    ViewKind.Top,
                    ViewKind.Bottom,
                    ViewKind.Left,
                    ViewKind.Right,
                    ViewKind.Back,
                    ViewKind.Isometric,
                }
                    .Where(k => !kinds.Contains(k))
                    .ToList();
            }
        }

        /// <summary>Gets the command of New (DRAWINGNEW).</summary>
        public ICommand NewCommand { get; }

        /// <summary>Gets the command of Update (DRAWINGUPDATE).</summary>
        public ICommand UpdateCommand { get; }

        /// <summary>Gets the command of Settings (DRAWINGSETTINGS).</summary>
        public ICommand SettingsCommand { get; }

        /// <summary>Gets the command of Add view, given the kind of view, as a <see cref="ViewKind"/> or its name.</summary>
        public ICommand AddViewCommand { get; }

        /// <summary>
        /// Gets the command of Section: VIEWSECTION started, its cut line picked on the canvas. A drawing opened from its file
        /// and not made again, which has no views to pick in, is made again from its source first, as Add view makes it.
        /// </summary>
        public ICommand SectionCommand { get; }

        /// <summary>
        /// Gets the command of Detail: VIEWDETAIL started, its rectangle picked on the canvas; a drawing opened from its file is
        /// made again from its source first, as for a section.
        /// </summary>
        public ICommand DetailCommand { get; }

        /// <summary>Gets the command of Auto dimension: the selected view dimensioned again (AUTODIM).</summary>
        public ICommand AutoDimensionCommand { get; }

        /// <summary>Gets the command of Auto marks: the selected view marked again (AUTOMARK).</summary>
        public ICommand AutoMarksCommand { get; }

        /// <summary>Gets the command of Centre lines: the selected view's centre lines drawn again.</summary>
        public ICommand CentreLinesCommand { get; }

        /// <summary>Gets the command of Export DXF (EXPORTDXF).</summary>
        public ICommand ExportDxfCommand { get; }

        /// <summary>Gets the command of Run plugin: the dialog that runs a drawing plugin loaded on the drawing shown (PLUGIN on the command line).</summary>
        public ICommand RunPluginCommand { get; }

        /// <summary>Finds the model drawing a document shows; null for a document of none.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The model drawing, or null.</returns>
        public ModelDrawing Find(CadDocument document) =>
            document == null ? null : _drawings.FirstOrDefault(d => d.Document == document);

        /// <summary>Asks the window for the New drawing dialog.</summary>
        /// <returns>The dialog's view model.</returns>
        public NewDrawingViewModel ShowNewDrawing() => Ask(DrawingDialogKind.NewDrawing, new NewDrawingViewModel(this));

        /// <summary>Asks the window for the Update from model dialog of the drawing shown; null, said on the command line, with none.</summary>
        /// <returns>The dialog's view model, or null.</returns>
        public UpdateFromModelViewModel ShowUpdate() =>
            _current == null
                ? Refuse<UpdateFromModelViewModel>("DRAWINGUPDATE")
                : Ask(DrawingDialogKind.Update, new UpdateFromModelViewModel(this, _current));

        /// <summary>Asks the window for the Drawing settings dialog of the drawing shown; null, said on the command line, with none.</summary>
        /// <returns>The dialog's view model, or null.</returns>
        public DrawingSettingsViewModel ShowSettings() =>
            _current == null
                ? Refuse<DrawingSettingsViewModel>("DRAWINGSETTINGS")
                : Ask(DrawingDialogKind.Settings, new DrawingSettingsViewModel(this, _current));

        /// <summary>Asks the window for the Export dialog; null, said on the command line, with no model drawing made.</summary>
        /// <returns>The dialog's view model, or null.</returns>
        public ExportViewModel ShowExport() =>
            _drawings.Count == 0
                ? Refuse<ExportViewModel>("EXPORTDXF")
                : Ask(DrawingDialogKind.Export, new ExportViewModel(this));

        /// <summary>Asks the window to show a message.</summary>
        /// <param name="message">The message.</param>
        public void ShowMessage(MessageViewModel message) =>
            Ask(DrawingDialogKind.Message, message ?? throw new ArgumentNullException(nameof(message)));

        /// <summary>
        /// Shows a drawing made in the presenter: its document over its DXF with its owner registered, listed under its source,
        /// on its sheet's layout. The drawing shown before stays listed.
        /// </summary>
        /// <param name="drawing">The drawing.</param>
        /// <param name="provider">The source it was made from.</param>
        /// <param name="presetName">The preset it was made with, as "Assembly".</param>
        /// <param name="wholeAssemblies">The assemblies drawn whole, whose new fittings an update draws too; null for none.</param>
        /// <returns>The model drawing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the source is null.</exception>
        public ModelDrawing Open(
            Drawing drawing,
            IDrawingSourceProvider provider,
            string presetName,
            IEnumerable<string> wholeAssemblies = null
        )
        {
            if (drawing == null)
            {
                throw new ArgumentNullException(nameof(drawing));
            }

            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            CadDocument document = drawing.OpenInPresenter();
            var entry = new ModelDrawing(UniqueKey(drawing), drawing, document, provider, presetName, wholeAssemblies);
            drawing.Plugins = _plugins;
            KeepValues(entry, provider.Name, entry.PresetName);
            _drawings.Add(entry);
            document.Changed += OnDocumentChanged;
            ((DelegateCommand)ExportDxfCommand).RaiseCanExecuteChanged();
            Show(entry);
            Say(
                "Made "
                    + entry.Number
                    + " ("
                    + entry.Name
                    + ") from "
                    + provider.Name
                    + ": "
                    + drawing.Views.Count.ToString(CultureInfo.InvariantCulture)
                    + " views at 1:"
                    + DialogViewModel.Length(drawing.Scale)
                    + " on "
                    + entry.SheetSize
                    + "."
                    + Warnings(drawing)
            );
            SaveNew(entry);
            return entry;
        }

        /// <summary>
        /// Saves a drawing just made into the save folder of Global settings, once they are applied, under a name no file there
        /// has; a folder that cannot be made or written to is said on the command line, and the drawing stays without a file.
        /// </summary>
        private void SaveNew(ModelDrawing entry)
        {
            if (!_settingsApplied)
            {
                return;
            }

            string folder =
                Model.Settings.SaveFolder.Length > 0
                    ? Model.Settings.SaveFolder
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "GeometryHelper Drawing"
                    );
            string name = entry.Number;

            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }

            try
            {
                folder = Path.GetFullPath(folder);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, name + ".dxf");

                for (int i = 2; File.Exists(path); i++)
                {
                    path = Path.Combine(folder, name + " (" + i.ToString(CultureInfo.InvariantCulture) + ").dxf");
                }

                entry.Document.SaveAs(path);
                Say("Saved " + entry.Number + " as " + path + ".");
            }
            catch (Exception ex)
                when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is ArgumentException
                    || ex is NotSupportedException
                )
            {
                GeometryHelperLog.Warn(
                    "DrawingWorkspace: " + entry.Number + " could not be saved into " + folder + ".",
                    ex
                );
                Say(
                    "Could not save " + entry.Number + " into " + folder + ": " + ex.Message + " Save it with Save As."
                );
            }
        }

        /// <summary>Shows a model drawing again: its document, on its sheet's layout.</summary>
        /// <param name="entry">The model drawing.</param>
        public void Show(ModelDrawing entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            Model.OpenDocument(entry.Document, entry.Key);
            CadSpace sheet = entry.Document.Spaces.FirstOrDefault(s =>
                !s.IsModel && string.Equals(s.Name, DrawingDxfWriter.LayoutName, StringComparison.OrdinalIgnoreCase)
            );

            if (sheet != null)
            {
                Model.SelectSpace(sheet, asStep: false);
            }

            Current = entry;
            Model.RefreshFromExtensions();
        }

        /// <summary>
        /// Makes the drawing shown again with other settings through the core's update: prepared off the UI thread, then
        /// written in one undo step of its document, its hand edits kept and what was drawn on it by hand left. Undo takes the
        /// settings back with the rest. A drawing opened from its file and not updated is made from the source
        /// <see cref="SourceToMakeFrom"/> finds.
        /// </summary>
        /// <param name="entry">The model drawing.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="presetName">The name shown for its preset from then on.</param>
        /// <param name="runner">Runs the preparing off the UI thread, showing a line while it does; null to run it on the caller's thread.</param>
        /// <returns>What changed and what became of the hand edits.</returns>
        /// <exception cref="InvalidOperationException">Thrown, in words saying what to do, when a drawing opened from its file
        /// has no source at hand that holds its objects, or the drawing is being made again already.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the drawing was closed while it was made; nothing is written.</exception>
        internal async Task<UpdateReport> ApplySettingsAsync(
            ModelDrawing entry,
            DrawingSettings settings,
            string presetName,
            Func<string, Func<PreparedUpdate>, Task<PreparedUpdate>> runner
        )
        {
            Drawing drawing = entry.Drawing;
            IDrawingSourceProvider provider = SourceToMakeFrom(entry);
            DrawingSettings copy = settings.Clone();
            Func<PreparedUpdate> prepare = () => drawing.Prepare(ObjectsToMakeFrom(entry, provider), copy);
            UpdateReport report;
            Begin(entry);

            try
            {
                PreparedUpdate prepared =
                    runner == null
                        ? prepare()
                        : await runner("Making " + entry.Number + " again with the new settings…", prepare);
                ThrowIfClosed(entry);
                report = WriteChange(entry, provider, presetName ?? entry.PresetName, () => drawing.Apply(prepared));
            }
            finally
            {
                End(entry);
            }

            Changed(entry);
            Say(
                entry.Number
                    + " made again with the new settings: "
                    + Count(report.EditsKept.Count, "hand edit")
                    + " kept"
                    + (
                        report.EditsDropped.Count == 0
                            ? string.Empty
                            : ", " + Count(report.EditsDropped.Count, "hand edit") + " no longer apply"
                    )
                    + "."
                    + Warnings(drawing)
            );
            SayDropped(report);
            return report;
        }

        /// <summary>Writes each hand edit an update dropped, with why, on the command line.</summary>
        internal void SayDropped(UpdateReport report)
        {
            foreach (DroppedEdit dropped in report.EditsDropped)
            {
                Say("  No longer applies: " + dropped.Edit.ObjectId + ": " + dropped.Reason + ".");
            }
        }

        /// <summary>Says whether a drawing is being made again off the UI thread, and where it is, says so on the command line.</summary>
        internal bool IsBusy(ModelDrawing entry)
        {
            if (!_busy.Contains(entry))
            {
                return false;
            }

            Say(BusyText(entry));
            return true;
        }

        /// <summary>Marks a drawing as being made again off the UI thread, until <see cref="End"/>: one change of it at a time.</summary>
        /// <exception cref="InvalidOperationException">Thrown, in words, when it is being made again already.</exception>
        internal void Begin(ModelDrawing entry)
        {
            if (!_busy.Add(entry))
            {
                throw new InvalidOperationException(BusyText(entry));
            }
        }

        /// <summary>Marks a drawing made again as done with.</summary>
        internal void End(ModelDrawing entry) => _busy.Remove(entry);

        /// <summary>
        /// Drops a drawing made again off the UI thread whose document was closed meanwhile: said on the command line, and
        /// nothing written into the closed document.
        /// </summary>
        /// <exception cref="OperationCanceledException">Thrown when the drawing was closed.</exception>
        internal void ThrowIfClosed(ModelDrawing entry)
        {
            if (!_drawings.Contains(entry))
            {
                Say(entry.Number + " was closed while it was made again; nothing was written.");
                throw new OperationCanceledException(entry.Number + " was closed while it was made again.");
            }
        }

        private static string BusyText(ModelDrawing entry) => entry.Number + " is being made again; wait for it.";

        /// <summary>Adds a view along an axis, or the isometric one, to the drawing shown: its settings with the view, made again, hand edits kept.</summary>
        /// <param name="kind">The kind of view.</param>
        /// <returns>Whether the view was added.</returns>
        public async Task<bool> AddViewAsync(ViewKind kind)
        {
            ModelDrawing entry = _current;

            if (entry == null)
            {
                Say("Add view works on a model drawing.");
                return false;
            }

            if (kind == ViewKind.Section || kind == ViewKind.Detail || kind == ViewKind.Free)
            {
                Say(
                    kind == ViewKind.Free
                        ? "A view along a direction of one's own comes in a later step."
                        : "A "
                            + kind.ToString().ToLowerInvariant()
                            + " is drawn by VIEW"
                            + kind.ToString().ToUpperInvariant()
                            + " on the canvas."
                );
                return false;
            }

            if (IsBusy(entry))
            {
                return false;
            }

            DrawingSettings settings = entry.Drawing.Settings;

            if (settings.Views.Kinds.Contains(kind))
            {
                Say("The drawing has a " + kind.ToString().ToLowerInvariant() + " view already.");
                return false;
            }

            settings.Views.Kinds.Add(kind);

            try
            {
                await ApplySettingsAsync(entry, settings, "Changed", (text, work) => Task.Run(work));
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                ShowMessage(
                    MessageViewModel.Failure(
                        "View not added",
                        "The "
                            + kind.ToString().ToLowerInvariant()
                            + " view could not be added to "
                            + entry.Number
                            + ".",
                        ex
                    )
                );
                return false;
            }
        }

        /// <summary>
        /// Looks again whether each drawing's source still gives the objects it was made from, and marks those that do not out
        /// of date. A drawing opened from its file and not made again is looked at in the source its file names, where that is
        /// connected, against the objects its views draw: one of them gone, or a fitting added to an assembly it draws whole,
        /// marks it out of date, and nothing of it is changed. One whose source is not at hand is left as it is.
        /// </summary>
        public void CheckSources()
        {
            foreach (ModelDrawing entry in _drawings)
            {
                IDrawingSourceProvider provider = entry.Drawing.Source != null ? entry.Provider : NamedSource(entry);

                if (provider == null || !provider.IsAvailable)
                {
                    continue;
                }

                try
                {
                    DrawingSource now = SourceFor(entry, provider.CreateObjects());
                    entry.IsOutOfDate =
                        entry.Drawing.Source != null
                            ? !SourceComparer.Same(entry.Drawing.Source, now)
                            : !new HashSet<string>(Drawn(entry), StringComparer.Ordinal).SetEquals(
                                now.Objects.Select(o => o.Id)
                            );
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    GeometryHelperLog.Warn(
                        "DrawingWorkspace: the source of " + entry.Number + " could not be read.",
                        ex
                    );
                }

                entry.Refresh();
            }

            Model.RefreshFromExtensions();
        }

        /// <inheritdoc/>
        public override void AddCommands(CadCommandRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            registry.Add(new CadCommand("DRAWINGNEW", c => Run(c, false, () => ShowNewDrawing())));
            registry.Add(new CadCommand("DRAWINGUPDATE", c => Run(c, true, () => ShowUpdate())));
            registry.Add(new CadCommand("DRAWINGSETTINGS", c => Run(c, true, () => ShowSettings())));
            registry.Add(new CadCommand("EXPORTDXF", c => Run(c, false, () => ShowExport())));
            registry.Add(new CadCommand("AUTODIM", c => OnViewCommand(c, ReDimensionSelectedView), true));
            registry.Add(new CadCommand("AUTOMARK", c => OnViewCommand(c, ReMarkSelectedView), true));
            registry.Add(new CadCommand("VIEWSECTION", SectionPrompts));
            registry.Add(new CadCommand("VIEWDETAIL", DetailPrompts));
            registry.Add(new CadCommand("PLUGIN", PluginPrompts));

            var aliases = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> alias in registry.Shortcuts.Aliases)
            {
                aliases[alias.Key] = alias.Value;
            }

            aliases["DN"] = "DRAWINGNEW";
            aliases["DU"] = "DRAWINGUPDATE";
            aliases["DS"] = "DRAWINGSETTINGS";
            aliases["EXD"] = "EXPORTDXF";
            aliases["AD"] = "AUTODIM";
            aliases["AM"] = "AUTOMARK";
            aliases["VS"] = "VIEWSECTION";
            aliases["VD"] = "VIEWDETAIL";
            var keys = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> key in registry.Shortcuts.Keys)
            {
                keys[key.Key] = key.Value;
            }

            keys["Ctrl+N"] = "DRAWINGNEW";
            registry.Shortcuts = new CadShortcuts(aliases, keys);
        }

        /// <summary>Runs DRAWINGNEW (DN) and EXPORTDXF (EXD) typed with no document open.</summary>
        /// <param name="typed">The name or alias typed.</param>
        /// <returns>Whether it was one of them.</returns>
        public override bool TryRunWithoutDocument(string typed)
        {
            switch ((typed ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "DRAWINGNEW":
                case "DN":
                    ShowNewDrawing();
                    return true;
                case "EXPORTDXF":
                case "EXD":
                    ShowExport();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Says how the file list shows a model drawing: under its source, by its number, its name and revision, its sheet and its dot.</summary>
        /// <param name="document">The document.</param>
        /// <returns>What the list shows; null for a document that is no model drawing.</returns>
        public override CadFileInfo DescribeFile(CadDocument document)
        {
            ModelDrawing entry = Find(document);

            if (entry == null)
            {
                return null;
            }

            int outOfDate = _drawings.Count(d => d.IsOutOfDate);
            return new CadFileInfo(
                entry.Provider.Name + " · model drawings",
                entry.Number,
                entry.Name + (string.IsNullOrEmpty(entry.Revision) ? string.Empty : " · rev " + entry.Revision),
                entry.SheetSize,
                entry.State,
                entry.Number + " · " + entry.Name,
                "Source: " + entry.Provider.Name + " · " + (entry.Provider.IsAvailable ? "connected" : "not connected"),
                outOfDate == 0 ? null : Count(outOfDate, "drawing") + " out of date"
            );
        }

        /// <summary>
        /// Takes a model drawing saved and opened again back as one: where the DXF carries a drawing document, the drawing is
        /// opened over the document with its owner registered, listed under "Opened from a file · model drawings" and shown
        /// on its sheet's layout, so that its mark can be moved and its projected lines cannot, as in a drawing just made.
        /// The file keeps the name of its source, its preset and its whole assemblies, not the source itself: Update from model, or a source of that name already connected, gives the objects. A DXF with no drawing document is left as it is.
        /// </summary>
        /// <param name="document">The document opened.</param>
        public override void DocumentOpened(CadDocument document)
        {
            if (document == null || Find(document) != null || DrawingDocumentStore.Read(document.Database) == null)
            {
                return;
            }

            Drawing drawing;

            try
            {
                drawing = Drawing.Open(document, null, Tolerance);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                GeometryHelperLog.Warn(
                    "DrawingWorkspace: the model drawing of "
                        + document.Title
                        + " could not be opened; it is shown as a DXF.",
                    ex
                );
                return;
            }

            // What the file keeps of where the drawing was made from; a file written before it kept any gives none.
            string sourceName = null,
                presetName = null;
            var whole = new List<string>();

            foreach (KeyValuePair<string, string> value in DrawingDocumentStore.ReadValues(document.Database))
            {
                switch (value.Key)
                {
                    case SourceKey:
                        sourceName = value.Value;
                        break;
                    case PresetKey:
                        presetName = value.Value;
                        break;
                    case WholeAssemblyKey:
                        whole.Add(value.Value);
                        break;
                }
            }

            var entry = new ModelDrawing(
                document.FileName ?? UniqueKey(drawing),
                drawing,
                document,
                OpenedFromFile,
                presetName,
                whole
            )
            {
                SourceName = sourceName,
            };
            drawing.Plugins = _plugins;
            _drawings.Add(entry);
            document.Changed += OnDocumentChanged;
            ((DelegateCommand)ExportDxfCommand).RaiseCanExecuteChanged();
            CadSpace sheet = document.Spaces.FirstOrDefault(s =>
                !s.IsModel && string.Equals(s.Name, DrawingDxfWriter.LayoutName, StringComparison.OrdinalIgnoreCase)
            );

            if (sheet != null)
            {
                document.CurrentSpace = sheet;
            }
        }

        /// <summary>Lets go of a model drawing closed (CLOSE): it leaves the list of drawings, and the Drawing tab hides.</summary>
        /// <param name="document">The document closed.</param>
        public override void DocumentClosed(CadDocument document)
        {
            ModelDrawing entry = Find(document);

            if (entry == null)
            {
                return;
            }

            document.Changed -= OnDocumentChanged;
            _drawings.Remove(entry);
            ((DelegateCommand)ExportDxfCommand).RaiseCanExecuteChanged();

            if (_current == entry)
            {
                Current = null;
            }
        }

        /// <summary>
        /// Says what Properties shows of a model drawing: with nothing selected the drawing itself; for a view, a dimension, a
        /// mark, a line or the sheet the banner of its owner and its link to the model.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles selected.</param>
        /// <param name="shown">What Properties shows otherwise.</param>
        /// <param name="banner">The banner; null for none.</param>
        /// <returns>What Properties shows.</returns>
        public override CadSelectionInfo DescribeSelection(
            CadDocument document,
            IReadOnlyList<string> handles,
            CadSelectionInfo shown,
            out string banner
        )
        {
            ModelDrawing entry = Find(document);
            banner = null;

            if (entry == null || shown == null)
            {
                return shown;
            }

            return DrawingProperties.Describe(entry, document, handles, shown, out banner);
        }

        /// <summary>
        /// The objects of a drawing as its source gives them now: those it has, and the fittings added to the assemblies it
        /// draws whole; for a drawing opened from its file, with no source, those its views draw.
        /// </summary>
        internal static DrawingSource SourceFor(ModelDrawing entry, IReadOnlyList<DrawingObject> now)
        {
            var ids = new HashSet<string>(Drawn(entry), StringComparer.Ordinal);
            var whole = new HashSet<string>(entry.WholeAssemblies, StringComparer.Ordinal);
            List<DrawingObject> objects = now.Where(o =>
                    ids.Contains(o.Id) || (o.ParentId != null && whole.Contains(o.ParentId))
                )
                .ToList();
            return new DrawingSource(objects);
        }

        /// <summary>The ids of the objects a drawing was made from; for a drawing opened from its file, those its views draw.</summary>
        private static IEnumerable<string> Drawn(ModelDrawing entry) =>
            entry.Drawing.Source != null
                ? entry.Drawing.Source.Objects.Select(o => o.Id)
                : entry.Drawing.Elements.Where(e => e.Kind == "View").SelectMany(e => e.Links.ObjectIds);

        /// <summary>The source of the name a drawing's file keeps, or Update from model last found its objects in, connected; null for none.</summary>
        private IDrawingSourceProvider NamedSource(ModelDrawing entry) =>
            _providers.FirstOrDefault(p =>
                p.IsAvailable && string.Equals(p.Name, entry.SourceName, StringComparison.Ordinal)
            );

        /// <summary>
        /// The source a change of a drawing's settings makes it again from: the one it was made from; for a drawing opened from
        /// its file and not updated, the source its file names or Update from model last found its objects in, connected.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown, saying what to do, where no such source is at hand.</exception>
        internal IDrawingSourceProvider SourceToMakeFrom(ModelDrawing entry)
        {
            if (entry.Drawing.Source != null)
            {
                return entry.Provider;
            }

            return NamedSource(entry)
                ?? throw new InvalidOperationException(
                    entry.Number
                        + " was opened from its file, and the source it was made from is not at hand: pick its source in Update from model first."
                );
        }

        /// <summary>
        /// The objects a change of a drawing's settings makes it again from: those it was made from; for a drawing opened from
        /// its file, those its views draw as the source gives them now, as Update from model finds them. A source with none of
        /// them is refused, so that no drawing is wiped.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown, saying what to do, where the source has none of the objects.</exception>
        internal static DrawingSource ObjectsToMakeFrom(ModelDrawing entry, IDrawingSourceProvider provider)
        {
            if (entry.Drawing.Source != null)
            {
                return entry.Drawing.Source;
            }

            DrawingSource source = SourceFor(entry, provider.CreateObjects());
            return source.Objects.Count > 0
                ? source
                : throw new InvalidOperationException(
                    "None of the objects "
                        + entry.Number
                        + " draws is in "
                        + provider.Name
                        + ": pick the source it was made from in Update from model first."
                );
        }

        /// <summary>
        /// Writes a change of a drawing made from a source with a preset, one undo step of its document: what the file keeps of
        /// the source and the preset is written first, so that the step holds it and undo and redo take it with the rest; put
        /// back where the change fails. The drawing is then known by the source and the preset.
        /// </summary>
        internal UpdateReport WriteChange(
            ModelDrawing entry,
            IDrawingSourceProvider provider,
            string presetName,
            Func<UpdateReport> change
        )
        {
            // A document with no step yet keeps itself as it stands at its first edit: a group opened and closed with no edit
            // in it makes it do so now, before the values are written, so that undoing this first step takes them back too.
            entry.Document.BeginGroup("UPDATE");
            entry.Document.EndGroup();
            KeepValues(entry, provider.Name, presetName);
            UpdateReport report;

            try
            {
                report = change();
            }
            catch (Exception)
            {
                KeepValues(entry, entry.SourceName, entry.PresetName);
                throw;
            }

            entry.Provider = provider;
            entry.SourceName = provider.Name;
            entry.PresetName = presetName;
            return report;
        }

        /// <summary>Writes into a drawing's file the name of its source, its preset and the assemblies it draws whole (<see cref="DrawingDocumentStore.WriteValues"/>).</summary>
        private static void KeepValues(ModelDrawing entry, string sourceName, string presetName)
        {
            var values = new List<KeyValuePair<string, string>>();

            if (sourceName != null)
            {
                values.Add(new KeyValuePair<string, string>(SourceKey, sourceName));
            }

            values.Add(new KeyValuePair<string, string>(PresetKey, presetName));

            foreach (string id in entry.WholeAssemblies)
            {
                values.Add(new KeyValuePair<string, string>(WholeAssemblyKey, id));
            }

            DrawingDocumentStore.WriteValues(entry.Document.Database, values);
        }

        /// <summary>Tells the presenter the drawing changed state: made again, updated.</summary>
        internal void Changed(ModelDrawing entry)
        {
            entry.LastMade = DateTime.Now;
            entry.Refresh();
            Raise(nameof(AddableViews));
            Model.RefreshFromExtensions();
        }

        /// <summary>Writes a line on the command line of the document shown, or of the start screen.</summary>
        internal void Say(string line)
        {
            if (Model.Session != null)
            {
                Model.Session.Write(line);
                Model.RefreshHistory();
            }
            else
            {
                Model.History.Add(line);
            }
        }

        internal static string Count(int count, string one, string many = null) =>
            count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? one : many ?? one + "s");

        private static string Warnings(Drawing drawing) =>
            drawing.Warnings.Count == 0 ? string.Empty : " " + string.Join(" ", drawing.Warnings);

        private static ViewKind ParseKind(string name) =>
            Enum.TryParse(name, true, out ViewKind kind) ? kind : ViewKind.Front;

        private T Ask<T>(DrawingDialogKind kind, T dialog)
            where T : DialogViewModel
        {
            DialogRequested?.Invoke(this, new DrawingDialogRequestEventArgs(kind, dialog));
            return dialog;
        }

        private T Refuse<T>(string command)
            where T : class
        {
            Say(
                command == "EXPORTDXF"
                    ? "No model drawing is made yet: DRAWINGNEW makes one."
                    : command + " works on a model drawing; the drawing shown is not one."
            );
            return null;
        }

        private IEnumerable<CadPrompt> Run(CadCommandContext c, bool needsDrawing, Action show)
        {
            if (needsDrawing && Find(c.Document) == null)
            {
                c.Write(c.Command.Name + " works on a model drawing; " + c.Document.Title + " is a DXF.");
            }
            else
            {
                show();
            }

            return Array.Empty<CadPrompt>();
        }

        private IEnumerable<CadPrompt> Write(CadCommandContext c, bool needsDrawing, string line)
        {
            c.Write(
                needsDrawing && Find(c.Document) == null
                    ? c.Command.Name + " works on a model drawing; " + c.Document.Title + " is a DXF."
                    : line
            );
            return Array.Empty<CadPrompt>();
        }

        private string UniqueKey(Drawing drawing)
        {
            string key = "drawing:" + drawing.Id;

            for (int i = 2; _drawings.Any(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase)); i++)
            {
                key = "drawing:" + drawing.Id + " (" + i.ToString(CultureInfo.InvariantCulture) + ")";
            }

            return key;
        }

        private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.Document))
            {
                Current = Find(Model.Document);
            }
        }

        private void OnDocumentChanged(object sender, CadDocumentChangedEventArgs e)
        {
            ModelDrawing entry = Find(sender as CadDocument);

            if (entry == null)
            {
                return;
            }

            // Undone or redone, the source and the preset are those the file keeps at the step it stands at now.
            if (e.Kind == CadChangeKind.Undo || e.Kind == CadChangeKind.Redo)
            {
                foreach (KeyValuePair<string, string> value in DrawingDocumentStore.ReadValues(entry.Document.Database))
                {
                    if (value.Key == SourceKey)
                    {
                        entry.SourceName = value.Value;
                    }
                    else if (value.Key == PresetKey)
                    {
                        entry.PresetName = value.Value;
                    }
                }

                Model.RefreshFromExtensions();
            }

            entry.Refresh();
        }
    }
}
