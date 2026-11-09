using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
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
            LayoutCommand = new DelegateCommand(_ => model.SelectSpace(model.Document?.Spaces.FirstOrDefault(s => !s.IsModel)));
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
    /// The ribbon tabs Annotate and View, a new empty drawing (NEW), Save As (SAVEAS), a new layout, the panels shown or hidden
    /// and the drafting toggles as typed commands (GRID, SNAP, ORTHO, POLAR, OSNAP).
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadBuiltInTab _annotateTab;
        private CadBuiltInTab _viewTab;
        private DelegateCommand _newCommand;
        private DelegateCommand _saveAsCommand;
        private DelegateCommand _newLayoutCommand;
        private bool _fileListShown = true;
        private bool _propertiesShown = true;
        private int _newDrawings;

        // What a command of the window leaves to be done once it has ended: a document cannot be changed under a command running.
        private Action _afterCommand;

        /// <summary>Gets the Annotate ribbon tab, always shown, after the extensions' tabs.</summary>
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
        public ICommand SaveAsCommand => _saveAsCommand ?? (_saveAsCommand = new DelegateCommand(_ => StartCommand("SAVEAS")));

        /// <summary>Gets the command of the + after the layout tabs: LAYOUT New, the layout named as offered (Layout2 after Layout1).</summary>
        public ICommand NewLayoutCommand => _newLayoutCommand ?? (_newLayoutCommand = new DelegateCommand(_ => NewLayout()));

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
        /// Makes a new empty drawing and shows it, as AutoCAD's NEW: a drawing of R2013 in millimetres with the layer 0 and
        /// Layout1, named Drawing1.dxf, then Drawing2.dxf, and listed under Open drawings until it is saved to a file of its own.
        /// </summary>
        /// <returns>The document.</returns>
        public CadDocument NewDrawing()
        {
            string name;

            do
            {
                _newDrawings++;
                name = "Drawing" + _newDrawings.ToString(CultureInfo.InvariantCulture) + ".dxf";
            }
            while (_files.ContainsKey(name) || _files.Values.Any(f => f.Document != null && string.Equals(f.Document.Title, name, StringComparison.OrdinalIgnoreCase)));

            CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), name);
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
        /// The commands of the window this file adds: NEW, SAVEAS, PROPERTIESCLOSE, the six toggles of the status bar, DYN the
        /// last, and DSETTINGS with AutoCAD's aliases DS and OS.
        /// </summary>
        private void AddWindowCommands(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("NEW", NewBody));
            registry.Add(new CadCommand("SAVEAS", SaveAsBody));
            registry.Add(new CadCommand("PROPERTIESCLOSE", PropertiesCloseBody));

            foreach (string name in new[] { "GRID", "SNAP", "ORTHO", "POLAR", "OSNAP", "DYN" })
            {
                registry.Add(new CadCommand(name, c => ToggleBody(c, name)));
            }

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
            if (_current == null || SpaceTabs.Select(t => t.Space).SequenceEqual(_current.Document.Spaces))
            {
                return;
            }

            SpaceTabs.Clear();

            foreach (CadSpace space in _current.Document.Spaces)
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

        /// <summary>SAVEAS: the window's Save As dialog; with no window, "Save drawing as &lt;file&gt;:" asked on the command line, as AutoCAD asks it with FILEDIA 0.</summary>
        private IEnumerable<CadPrompt> SaveAsBody(CadCommandContext c)
        {
            if (SaveAsRequested != null)
            {
                _afterCommand = () => SaveAsRequested?.Invoke(this, EventArgs.Empty);
                yield break;
            }

            string offered = c.Document.FileName ?? c.Document.Title;
            yield return CadPrompt.ForString("Save drawing as", offered);
            string path = c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : offered;
            _afterCommand = () => SaveAs(path);
        }

        /// <summary>PROPERTIESCLOSE: Properties hidden.</summary>
        private IEnumerable<CadPrompt> PropertiesCloseBody(CadCommandContext c)
        {
            IsPropertiesPanelShown = false;
            return Array.Empty<CadPrompt>();
        }

        /// <summary>GRID, SNAP, ORTHO, POLAR, OSNAP, DYN typed: "Enter mode [ON/OFF] &lt;OFF&gt;:", the same toggle as the status bar's; SNAP also takes a spacing, which turns it on.</summary>
        private IEnumerable<CadPrompt> ToggleBody(CadCommandContext c, string name)
        {
            StatusToggleViewModel toggle = FindToggle(name);
            bool spacing = name == "SNAP";
            string[] modes = { "ON", "OFF" };
            yield return spacing
                ? CadPrompt.ForDistance("Specify snap spacing", null, modes, CadCommandContext.Length(Snap.GridX))
                : CadPrompt.ForKeyword("Enter mode", modes, toggle.IsOn ? "ON" : "OFF");

            if (c.Input.Is("ON") || c.Input.Is("OFF"))
            {
                toggle.IsOn = c.Input.Is("ON");
            }
            else if (spacing && c.Input.Kind == CadInputKind.Number)
            {
                if (!(c.Input.Number > 0.0) || double.IsInfinity(c.Input.Number))
                {
                    c.Write("Value must be positive and nonzero.");
                    yield break;
                }

                Snap.GridX = c.Input.Number;
                Snap.GridY = c.Input.Number;
                toggle.IsOn = true;
            }
        }
    }
}
