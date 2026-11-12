using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Insert ribbon tab, as AutoCAD 2021's for what the presenter has built: Block (Insert, its gallery of the drawing's
    /// blocks, Edit Attribute), Block Definition (Create Block, Define Attributes, Set Base Point) and Reference (Attach, Clip,
    /// External References, the image frame box, Transparency). A drawing with the blocks DOOR_900 and GRID_BUBBLE shows
    /// their two tiles in the gallery in that order, and a click on DOOR_900 starts INSERT placing it.
    /// </summary>
    public sealed class InsertRibbonViewModel : ObservableObject
    {
        private IReadOnlyList<BlockTileViewModel> _gallery = Array.Empty<BlockTileViewModel>();
        private bool _isGalleryOpen;

        internal InsertRibbonViewModel(MainViewModel model)
        {
            Model = model;
            OpenGalleryCommand = new DelegateCommand(_ => IsGalleryOpen = true);
            InsertBlockCommand = new DelegateCommand(p =>
            {
                IsGalleryOpen = false;
                model.InsertBlock(p as string);
            });
            OpenPaletteCommand = new DelegateCommand(p =>
            {
                IsGalleryOpen = false;
                model.OpenBlocksPalette(p as string);
            });
        }

        /// <summary>Gets the window's view model, whose commands the buttons run.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets the command of Insert ▾, which drops the gallery.</summary>
        public ICommand OpenGalleryCommand { get; }

        /// <summary>Gets the command a tile of the gallery runs, given the block's name: INSERT started, placing that block with the palette's options.</summary>
        public ICommand InsertBlockCommand { get; }

        /// <summary>
        /// Gets the command of the gallery's lines under the tiles, given the palette's tab: INSERT started, the Blocks palette
        /// opened on that tab, "Recent" or "Other drawing".
        /// </summary>
        public ICommand OpenPaletteCommand { get; }

        /// <summary>Gets or sets whether the gallery is dropped below Insert ▾; dropped, its tiles are read from the drawing shown.</summary>
        public bool IsGalleryOpen
        {
            get => _isGalleryOpen;
            set
            {
                if (Set(ref _isGalleryOpen, value) && value)
                {
                    Refill();
                }
            }
        }

        /// <summary>Gets the tiles of the gallery, the drawing's blocks in the order of the Blocks palette: by name, letters of either case alike.</summary>
        public IReadOnlyList<BlockTileViewModel> Gallery => _gallery;

        /// <summary>Gets whether the drawing has a block to show; with none the gallery reads "No blocks in this drawing".</summary>
        public bool HasGallery => _gallery.Count > 0;

        /// <summary>Gets the line above the tiles: "Current drawing · plan-level2.dxf".</summary>
        public string GalleryTitle => "Current drawing · " + (Model.Document?.Title ?? string.Empty);

        /// <summary>The tiles read again from the drawing shown, their pictures drawn when first shown.</summary>
        private void Refill()
        {
            CadDocument document = Model.Document;

            if (document == null)
            {
                _gallery = Array.Empty<BlockTileViewModel>();
            }
            else
            {
                var pictures = new BlockPictures(document.Database, document.Fonts, 72, 48);
                _gallery = CadBlocks
                    .Named(document.Database)
                    .Select(b => new BlockTileViewModel(b.Name, b.Comments, pictures.Draw))
                    .ToList();
            }

            Raise(nameof(Gallery));
            Raise(nameof(HasGallery));
            Raise(nameof(GalleryTitle));
        }
    }

    /// <summary>
    /// The Annotate ribbon tab: Text (Multiline Text, Single Line), Dimensions (Linear, Aligned, Angular, Radius, Diameter) and
    /// Leaders (Multileader), each button starting its command through <see cref="MainViewModel.StartCommandCommand"/>, as
    /// the buttons of Home do.
    /// </summary>
    public sealed class AnnotateRibbonViewModel
    {
        internal AnnotateRibbonViewModel(MainViewModel model)
        {
            Model = model;
        }

        /// <summary>Gets the window's view model, whose commands the buttons run.</summary>
        public MainViewModel Model { get; }
    }

    /// <summary>
    /// The View ribbon tab: Navigate (Zoom Extents, Window, Previous, In, Out, Pan, Regen), Views (Model, Layout), Interface
    /// (the DRAWINGS list and Properties shown or hidden) and the drafting toggles GRID, SNAP, ORTHO, POLAR and OSNAP, the
    /// very toggles of the status bar.
    /// </summary>
    public sealed class ViewRibbonViewModel
    {
        private readonly MainViewModel _model;

        internal ViewRibbonViewModel(MainViewModel model)
        {
            _model = model;
            RunCommand = new DelegateCommand(p => model.RunCommandLine(p as string));
            ModelSpaceCommand = new DelegateCommand(_ => model.SelectSpace(model.Document?.ModelSpace));
            LayoutCommand = new DelegateCommand(_ =>
                model.SelectSpace(model.Document?.Spaces.FirstOrDefault(s => !s.IsModel))
            );
        }

        /// <summary>Gets the window's view model.</summary>
        public MainViewModel Model => _model;

        /// <summary>Gets the command that runs a line of the command line, given it, as "ZOOM 2X": the command it names is started, ending any running, and the rest answers its prompts.</summary>
        public ICommand RunCommand { get; }

        /// <summary>Gets the command that shows model space.</summary>
        public ICommand ModelSpaceCommand { get; }

        /// <summary>Gets the command that shows the first layout of the drawing.</summary>
        public ICommand LayoutCommand { get; }

        /// <summary>Gets the toggle GRID of the status bar.</summary>
        public StatusToggleViewModel Grid => Toggle("GRID");

        /// <summary>Gets the toggle SNAP of the status bar.</summary>
        public StatusToggleViewModel Snap => Toggle("SNAP");

        /// <summary>Gets the toggle ORTHO of the status bar.</summary>
        public StatusToggleViewModel Ortho => Toggle("ORTHO");

        /// <summary>Gets the toggle POLAR of the status bar.</summary>
        public StatusToggleViewModel Polar => Toggle("POLAR");

        /// <summary>Gets the toggle OSNAP of the status bar.</summary>
        public StatusToggleViewModel Osnap => Toggle("OSNAP");

        private StatusToggleViewModel Toggle(string name) => _model.Toggles.First(t => t.Name == name);
    }

    /// <summary>
    /// The ribbon tabs Insert, Annotate and View, a new empty drawing (NEW), Save As (SAVEAS), a new layout, the panels shown or
    /// hidden and the drafting toggles as typed commands (GRID, SNAP, ORTHO, POLAR, OSNAP).
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadBuiltInTab _insertTab;
        private CadBuiltInTab _annotateTab;
        private CadBuiltInTab _viewTab;
        private DelegateCommand _newCommand;
        private DelegateCommand _saveAsCommand;
        private DelegateCommand _newLayoutCommand;
        private bool _fileListShown = true;
        private bool _propertiesShown = true;
        private bool _ribbonShown = true;
        private int _newDrawings;
        private DelegateCommand _startCommandWithOptionCommand;

        // The option a split button's choice gives the first prompt that offers it, as F for DRAWORDER's Front; null for none.
        private string _pendingOption;

        // What a command of the window leaves to be done once it has ended: a document cannot be changed under a command running.
        private Action _afterCommand;

        /// <summary>Gets the Insert ribbon tab, always shown, after the extensions' tabs and before Annotate.</summary>
        public CadWorkspaceExtension InsertTab
        {
            get
            {
                if (_insertTab == null)
                {
                    _insertTab = new CadBuiltInTab("Insert", new InsertRibbonViewModel(this));
                    _builtInTabs.Add(_insertTab);
                }

                return _insertTab;
            }
        }

        /// <summary>Gets the Annotate ribbon tab, always shown, after Insert.</summary>
        public CadWorkspaceExtension AnnotateTab
        {
            get
            {
                if (_annotateTab == null)
                {
                    _annotateTab = new CadBuiltInTab("Annotate", new AnnotateRibbonViewModel(this));
                    _builtInTabs.Add(_annotateTab);
                }

                return _annotateTab;
            }
        }

        /// <summary>Gets the View ribbon tab, always shown, after Annotate.</summary>
        public CadWorkspaceExtension ViewTab
        {
            get
            {
                if (_viewTab == null)
                {
                    _viewTab = new CadBuiltInTab("View", new ViewRibbonViewModel(this));
                    _builtInTabs.Add(_viewTab);
                }

                return _viewTab;
            }
        }

        /// <summary>Gets the command that makes a new empty drawing (NEW, the quick access New and the start screen's Blank drawing).</summary>
        public ICommand NewCommand => _newCommand ?? (_newCommand = new DelegateCommand(_ => NewDrawing()));

        /// <summary>Gets the command that runs SAVEAS (the quick access Save As).</summary>
        public ICommand SaveAsCommand =>
            _saveAsCommand ?? (_saveAsCommand = new DelegateCommand(_ => StartCommand("SAVEAS")));

        /// <summary>Gets the command of the + after the layout tabs: LAYOUT New, the layout named as offered (Layout2 after Layout1).</summary>
        public ICommand NewLayoutCommand =>
            _newLayoutCommand ?? (_newLayoutCommand = new DelegateCommand(_ => NewLayout()));

        /// <summary>
        /// Gets the command a split button's choice runs, given the command and its option, as "DRAWORDER F": the command is
        /// started, ending any running, and the option given to the first prompt that offers it, the objects asked before it
        /// picked by the user, as AutoCAD's ribbon macro <c>_draworder;\_f</c> does. "TEXTTOFRONT T" brings the text to the
        /// front at once; "DRAWORDER B" asks for the objects, then sends them to the back.
        /// </summary>
        public ICommand StartCommandWithOptionCommand =>
            _startCommandWithOptionCommand
            ?? (
                _startCommandWithOptionCommand = new DelegateCommand(
                    p => StartCommandWithOption(p as string),
                    _ => HasDocument
                )
            );

        /// <summary>
        /// Gets or sets whether a pick takes a selectable group whole (PICKSTYLE 1), as Home › Groups › Group Selection On/Off
        /// shows it; setting it switches PICKSTYLE as Ctrl+Shift+A does, writing "&lt;Group on&gt;" or "&lt;Group off&gt;".
        /// </summary>
        public bool IsGroupSelectionOn
        {
            get => _current != null && (_current.Document.PickStyle & 1) != 0;
            set
            {
                if (_current != null && value != IsGroupSelectionOn)
                {
                    _current.Session.TogglePickStyle();
                    SyncHistory();
                    Raise(nameof(IsGroupSelectionOn));
                }
            }
        }

        /// <summary>Gets or sets whether the DRAWINGS list is shown at the left (View › Interface).</summary>
        public bool IsFileListShown
        {
            get => _fileListShown;
            set => Set(ref _fileListShown, value);
        }

        /// <summary>Gets or sets whether Properties is shown at the right (View › Interface; PROPERTIES shows it, PROPERTIESCLOSE hides it).</summary>
        public bool IsPropertiesPanelShown
        {
            get => _propertiesShown;
            set => Set(ref _propertiesShown, value);
        }

        /// <summary>
        /// Gets or sets whether the ribbon, its tabs and its panels, is shown above the canvas: Ctrl+3 shows or hides it, and
        /// RIBBON shows it where RIBBONCLOSE hides it, as AutoCAD's do. Hidden, the canvas takes its room.
        /// </summary>
        public bool IsRibbonShown
        {
            get => _ribbonShown;
            set => Set(ref _ribbonShown, value);
        }

        /// <summary>
        /// Makes a new empty drawing and shows it, as AutoCAD's NEW: a drawing of R2013 of the presenter's metric template
        /// (<see cref="CadTemplate"/>, the settings of acadiso.dwt: millimetres, ISO-25) with the layer 0, the layers of Global
        /// settings (<see cref="CadTemplate.AddLayers"/>, the current layer staying 0) and Layout1, named Drawing1.dwg, then Drawing2.dwg, as AutoCAD's DWGNAME of a new drawing, so that Save As offers a DWG first; listed
        /// under Open drawings until it is saved to a file of its own.
        /// </summary>
        /// <returns>The document.</returns>
        public CadDocument NewDrawing()
        {
            string name;

            do
            {
                _newDrawings++;
                name = "Drawing" + _newDrawings.ToString(CultureInfo.InvariantCulture) + ".dwg";
            } while (
                _files.ContainsKey(name)
                || _files.Values.Any(f =>
                    f.Document != null && string.Equals(f.Document.Title, name, StringComparison.OrdinalIgnoreCase)
                )
            );

            // The template's GRIDMODE is the window's, so that a new drawing shows the grid as Global settings say.
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ViewportTable["*Active"].GridEnabled = _settings.Grid;

            // The layers of Global settings (Layers, colours and lines), with their colours, linetypes, lineweights and plot flags;
            // the current layer stays 0.
            CadTemplate.AddLayers(database, _settings.Layers);
            CadDocument document = CadDocument.From(database, name);
            OpenDocument(document, name);
            return document;
        }

        /// <summary>Runs a line of the command line from a button, as "ZOOM 2X": the command named started, ending any running, then each word after it given to its prompts.</summary>
        internal void RunCommandLine(string line)
        {
            string[] words = (line ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (_current == null || words.Length == 0)
            {
                return;
            }

            StartCommand(words[0]);

            for (int i = 1; i < words.Length && IsCommandRunning; i++)
            {
                Execute(words[i]);
            }
        }

        /// <summary>Starts the command of a line as "DRAWORDER F", keeping its option for the first prompt that offers it.</summary>
        private void StartCommandWithOption(string line)
        {
            string[] words = (line ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (_current == null || words.Length == 0)
            {
                return;
            }

            if (_current.Session.IsRunning)
            {
                _current.Session.Escape();
            }

            _pendingOption = words.Length > 1 ? words[1] : null;
            StartCommand(words[0]);
        }

        /// <summary>
        /// Gives the option kept by <see cref="StartCommandWithOption"/> to the prompt now asked where it offers it; forgets it
        /// once the command has ended.
        /// </summary>
        private void AnswerPendingOption()
        {
            if (_pendingOption == null)
            {
                return;
            }

            CadCommandSession session = _current?.Session;

            if (session == null || !session.IsRunning || session.Prompt == null)
            {
                _pendingOption = null;
            }
            else if (session.Prompt.MatchKeyword(_pendingOption) != null)
            {
                string option = _pendingOption;
                _pendingOption = null;
                session.Feed(option);
            }
        }

        /// <summary>The + of the layout tabs: LAYOUT New, Enter taking the name offered.</summary>
        private void NewLayout()
        {
            if (_current != null)
            {
                StartCommand("LAYOUT");
                Execute("N");
                Enter();
            }
        }

        /// <summary>
        /// The commands of the window this file adds: NEW, SAVEAS, PROPERTIESCLOSE, RIBBON, RIBBONCLOSE, INPUTSEARCHOPTIONS and -INPUTSEARCHOPTIONS
        /// (the command line's Input Search Options), the six toggles of the status bar, DYN the last, and DSETTINGS with
        /// AutoCAD's aliases DS and OS.
        /// </summary>
        private void AddWindowCommands(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("NEW", NewBody));
            registry.Add(new CadCommand("SAVEAS", SaveAsBody));
            registry.Add(new CadCommand("PROPERTIESCLOSE", PropertiesCloseBody));
            registry.Add(new CadCommand("RIBBON", RibbonBody));
            registry.Add(new CadCommand("RIBBONCLOSE", RibbonCloseBody));
            registry.Add(new CadCommand("INPUTSEARCHOPTIONS", InputSearchOptionsBody));
            registry.Add(new CadCommand("-INPUTSEARCHOPTIONS", InputSearchOptionsTyped));

            registry.Add(new CadCommand("GRID", GridBody));
            registry.Add(new CadCommand("SNAP", SnapBody));

            foreach (string name in new[] { "ORTHO", "POLAR", "OSNAP", "DYN" })
            {
                registry.Add(new CadCommand(name, c => ToggleBody(c, name)));
            }

            registry.Add(new CadCommand("ISOPLANE", IsoplaneBody));
            registry.Add(new CadCommand("DSETTINGS", DraftingSettingsBody));
            var aliases = new Dictionary<string, string> { ["DS"] = "DSETTINGS", ["OS"] = "DSETTINGS" };
            registry.Shortcuts = new CadShortcuts(registry.Shortcuts.Aliases.Concat(aliases), registry.Shortcuts.Keys);
        }

        /// <summary>Does what a command of the window left to be done once it ended.</summary>
        private void RunAfterCommand()
        {
            Action after = _afterCommand;
            _afterCommand = null;
            after?.Invoke();
        }

        /// <summary>Shows the tabs of the spaces again where the document's spaces changed, as LAYOUT New or its undo changes them.</summary>
        private void SyncSpaceTabs()
        {
            if (_current == null || SpaceTabs.Select(t => t.Space).SequenceEqual(TabSpaces(_current.Document)))
            {
                return;
            }

            SpaceTabs.Clear();

            foreach (CadSpace space in TabSpaces(_current.Document))
            {
                SpaceTabs.Add(new SpaceTabViewModel(space) { IsActive = space == _current.Document.CurrentSpace });
            }

            RaiseSpace();
            RebuildRows();
        }

        /// <summary>NEW: a new empty drawing, shown once the command has ended.</summary>
        private IEnumerable<CadPrompt> NewBody(CadCommandContext c)
        {
            _afterCommand = () => NewDrawing();
            return Array.Empty<CadPrompt>();
        }

        /// <summary>
        /// SAVEAS: the window's Save As dialog; with no window, or FILEDIA 0, "Save drawing as &lt;file&gt;:" asked on the command
        /// line, as AutoCAD asks it with FILEDIA 0, a name typed with no extension given the offered one's: .dwg for a drawing
        /// opened from a DWG, which is saved as a DWG.
        /// </summary>
        private IEnumerable<CadPrompt> SaveAsBody(CadCommandContext c)
        {
            if (SaveAsRequested != null && c.Session.FileDialogs)
            {
                _afterCommand = () => SaveAsRequested?.Invoke(this, EventArgs.Empty);
                yield break;
            }

            string offered = c.Document.FileName ?? c.Document.Title;
            yield return CadPrompt.ForString("Save drawing as", offered);
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
            string path =
                typed.Length == 0 ? offered
                : System.IO.Path.HasExtension(typed) ? typed
                : typed + System.IO.Path.GetExtension(offered);
            _afterCommand = () => SaveAs(path);
        }

        /// <summary>PROPERTIESCLOSE: Properties hidden.</summary>
        private IEnumerable<CadPrompt> PropertiesCloseBody(CadCommandContext c)
        {
            IsPropertiesPanelShown = false;
            return Array.Empty<CadPrompt>();
        }

        /// <summary>RIBBON: the ribbon shown.</summary>
        private IEnumerable<CadPrompt> RibbonBody(CadCommandContext c)
        {
            IsRibbonShown = true;
            return Array.Empty<CadPrompt>();
        }

        /// <summary>RIBBONCLOSE: the ribbon hidden.</summary>
        private IEnumerable<CadPrompt> RibbonCloseBody(CadCommandContext c)
        {
            IsRibbonShown = false;
            return Array.Empty<CadPrompt>();
        }

        /// <summary>
        /// ORTHO, POLAR, OSNAP, DYN typed: "Enter mode [ON/OFF] &lt;OFF&gt;:", the same toggle as the status bar's. SNAP and
        /// GRID have their own (<see cref="SnapBody"/>, <see cref="GridBody"/>).
        /// </summary>
        private IEnumerable<CadPrompt> ToggleBody(CadCommandContext c, string name)
        {
            StatusToggleViewModel toggle = FindToggle(name);
            yield return CadPrompt.ForKeyword("Enter mode", new[] { "ON", "OFF" }, toggle.IsOn ? "ON" : "OFF");

            if (c.Input.Is("ON") || c.Input.Is("OFF"))
            {
                toggle.IsOn = c.Input.Is("ON");
            }
        }

        /// <summary>
        /// ISOPLANE: "Current isoplane: Left", then "Enter isometric plane setting [Left/Top/Right] &lt;Top&gt;:", Enter taking
        /// the next plane, and the plane taken written, as AutoCAD 2021 does whether the snap is isometric or not
        /// (SNAPISOPAIR).
        /// </summary>
        private IEnumerable<CadPrompt> IsoplaneBody(CadCommandContext c)
        {
            var plane = (CadIsoplane)Layout.Isoplane;
            var next = (CadIsoplane)(((int)plane + 1) % 3);
            c.Write("Current isoplane: " + plane);
            yield return CadPrompt.ForKeyword(
                "Enter isometric plane setting",
                new[] { "Left", "Top", "Right" },
                next.ToString()
            );

            if (c.Input.Is("Left") || c.Input.Is("Top") || c.Input.Is("Right"))
            {
                int taken =
                    c.Input.Is("Left") ? 0
                    : c.Input.Is("Top") ? 1
                    : 2;
                ChangeDrafting(
                    "SNAPISOPAIR",
                    s => s.Isoplane = taken,
                    v => v.SnapPair = taken,
                    v => v.SnapIsoPair = taken
                );
                c.Write("Current isoplane: " + (CadIsoplane)taken);
            }
        }
    }
}
