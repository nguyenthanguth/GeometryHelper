using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Block Editor of the window (BEDIT): the Edit Block Definition dialog, the green Block Editor tab while a block is
    /// open, the one Model tab, and the question BCLOSE asks of a block with changes not saved.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadBlockEditorTab _blockEditorTab;
        private CadBlockEditor _shownEditor;

        /// <summary>
        /// Gets or sets how the workspace shows the Edit Block Definition dialog, and the same window titled Save Block As; the
        /// block chosen and the action it closed with are in its request. With none set, BEDIT and BSAVEAS ask on the command line.
        /// </summary>
        public Func<BlockEditViewModel, bool> ShowBlockEdit { get; set; }

        /// <summary>
        /// Gets or sets how the workspace asks BCLOSE's question of a block with changes not saved, "Save changes to
        /// DOOR_900?", the name given: Save, Discard, or null for Cancel. With none set, BCLOSE asks on the command line.
        /// </summary>
        public Func<string, string> AskSaveBlockChanges { get; set; }

        /// <summary>Gets the contextual Block Editor tab, shown, green, while a block is open in the Block Editor.</summary>
        public CadWorkspaceExtension BlockEditorTab
        {
            get
            {
                if (_blockEditorTab == null)
                {
                    _blockEditorTab = new CadBlockEditorTab();
                    _builtInTabs.Add(_blockEditorTab);
                }

                return _blockEditorTab;
            }
        }

        /// <summary>Gets whether the drawing shown has a block open in the Block Editor.</summary>
        public bool IsBlockEditing => _current?.Document.BlockEditor != null;

        /// <summary>Gets whether the + after the tabs that adds a layout shows: with a drawing, outside the Block Editor, where the layout tabs are hidden.</summary>
        public bool CanAddLayout => HasDocument && !IsBlockEditing;

        /// <summary>The state text of the status bar in the Block Editor: "Unsaved changes to DOOR_900", or "Block Editor: DOOR_900"; null outside it.</summary>
        private string BlockEditorStatus
        {
            get
            {
                CadBlockEditor editor = _current?.Document.BlockEditor;
                return editor == null ? null : (editor.HasUnsavedChanges ? "Unsaved changes to " : "Block Editor: ") + editor.Name;
            }
        }

        /// <summary>The spaces given a tab: the Block Editor's alone while it is open, as AutoCAD hides the layout tabs; else every space.</summary>
        private static IReadOnlyList<CadSpace> TabSpaces(CadDocument document)
            => document.BlockEditor != null ? new[] { document.CurrentSpace } : document.Spaces;

        /// <summary>
        /// Takes every dialog the window hooked off the view model, as the screenshot mode needs, where nothing may open a real
        /// window: each Show*, Ask*, Pick* and Confirm* function set to null, so that its command asks on the command line or
        /// does nothing, and the handlers of every *Requested event (Save As, Open, Global settings, the Layer manager) let go.
        /// </summary>
        internal void StubDialogs()
        {
            const System.Reflection.BindingFlags Instance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            foreach (System.Reflection.PropertyInfo property in typeof(MainViewModel).GetProperties(Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                bool hook = property.Name.StartsWith("Show", StringComparison.Ordinal) || property.Name.StartsWith("Ask", StringComparison.Ordinal)
                    || property.Name.StartsWith("Pick", StringComparison.Ordinal) || property.Name.StartsWith("Confirm", StringComparison.Ordinal);

                if (hook && typeof(Delegate).IsAssignableFrom(property.PropertyType) && property.CanWrite && property.GetSetMethod(true) != null)
                {
                    property.SetValue(this, null);
                }
            }

            foreach (System.Reflection.FieldInfo field in typeof(MainViewModel).GetFields(Instance).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType) && field.Name.IndexOf("Requested", StringComparison.Ordinal) >= 0)
                {
                    field.SetValue(this, null);
                }
            }
        }

        /// <summary>Block Properties: the selection let go, so that Properties shows the Block group of the block open.</summary>
        internal void ShowBlockProperties()
        {
            ClearSelection();
            IsPropertiesPanelShown = true;
            PropertiesRequested?.Invoke(this, EventArgs.Empty);
            UpdateProperties();
        }

        /// <summary>Edit Block: the block open closed (asked about its changes), then the Edit Block Definition dialog, as AutoCAD's button.</summary>
        internal void EditAnotherBlock()
        {
            StartCommand("BCLOSE");

            if (_current != null && _current.Document.BlockEditor == null && !_current.Session.IsRunning)
            {
                StartCommand("BEDIT");
            }
        }

        /// <summary>A request of the Block Editor's commands: the dialog or the question shown where the workspace shows it.</summary>
        private void OnBlockEditorRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadBlockEditRequest edit when ShowBlockEdit != null:
                    e.Handled = ShowBlockEdit(new BlockEditViewModel(edit));
                    break;
                case string name when e.Name == "BCLOSE" && AskSaveBlockChanges != null:
                    e.Answer = AskSaveBlockChanges(name);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>Follows the Block Editor of the drawing shown: its tab shown and chosen when a block opens, hidden when it closes; the tabs under the canvas with it.</summary>
        private void SyncBlockEditor()
        {
            CadBlockEditor editor = _current?.Document.BlockEditor;

            if (!ReferenceEquals(editor, _shownEditor))
            {
                _shownEditor = editor;

                if (editor != null)
                {
                    ((CadBlockEditorTab)BlockEditorTab).Show(new BlockEditorRibbonViewModel(this, editor));
                    SelectedTab = BlockEditorTab;
                }
                else if (_blockEditorTab != null && _blockEditorTab.IsTabVisible)
                {
                    _blockEditorTab.Hide();

                    if (SelectedTab == _blockEditorTab || SelectedTab == null)
                    {
                        SelectedTab = null;
                    }
                }

                Raise(nameof(IsBlockEditing));

                if (_current != null)
                {
                    SyncSpaceTabs();
                    RaiseSpace();
                }
            }

            (_blockEditorTab?.TabContent as BlockEditorRibbonViewModel)?.Refresh();
            Raise(nameof(CanAddLayout));
            Raise(nameof(StatusText));
            Raise(nameof(ViewportLabel));
        }
    }

    /// <summary>The contextual Block Editor tab, green as a contextual tab, shown while a block is open in the Block Editor.</summary>
    internal sealed class CadBlockEditorTab : CadWorkspaceExtension
    {
        public override string TabHeader => "Block Editor";

        public void Show(BlockEditorRibbonViewModel ribbon)
        {
            TabContent = ribbon;
            IsTabVisible = true;
        }

        public void Hide()
        {
            IsTabVisible = false;
            TabContent = null;
        }
    }

    /// <summary>
    /// What the Block Editor tab does, as AutoCAD 2021's: Edit Block, Save Block, Save As; Attribute Definition and Block
    /// Properties; the visibility states of the block, live only where it has some; Close Block Editor. Test Block, the
    /// constraints, the parameters and the actions are greyed, not in this version.
    /// </summary>
    public sealed class BlockEditorRibbonViewModel : ObservableObject
    {
        private readonly MainViewModel _model;
        private readonly CadBlockEditor _editor;

        internal BlockEditorRibbonViewModel(MainViewModel model, CadBlockEditor editor)
        {
            _model = model;
            _editor = editor;
            EditBlockCommand = new DelegateCommand(_ => model.EditAnotherBlock());
            SaveBlockCommand = new DelegateCommand(_ => model.StartCommand("BSAVE"));
            SaveAsCommand = new DelegateCommand(_ => model.StartCommand("BSAVEAS"));
            AttributeDefinitionCommand = new DelegateCommand(_ => model.StartCommand("ATTDEF"));
            BlockPropertiesCommand = new DelegateCommand(_ => model.ShowBlockProperties());
            CloseCommand = new DelegateCommand(_ => model.StartCommand("BCLOSE"));
        }

        /// <summary>Gets the name of the block open, or &lt;Current Drawing&gt;.</summary>
        public string Name => _editor.Name;

        /// <summary>Gets whether the block has changes not saved.</summary>
        public bool HasUnsavedChanges => _editor.HasUnsavedChanges;

        /// <summary>Gets whether the visibility state box is live: where the block open has visibility states; greyed where it has none.</summary>
        public bool HasVisibilityStates => _editor.VisibilityStates.Count > 0;

        /// <summary>Gets the states the visibility state box lists, in the block's order; "(none)" alone for a block of none.</summary>
        public IReadOnlyList<string> VisibilityStates => HasVisibilityStates ? _editor.VisibilityStates : new[] { NoState };

        /// <summary>
        /// Gets or sets the state the box shows, and the editor with it: the entities another state lists and it does not are
        /// hidden, as AutoCAD's Block Editor hides them; "(none)" for a block of none. A state set is a view of the block, not
        /// an edit: no undo step, nothing to save.
        /// </summary>
        public string VisibilityState
        {
            get => _editor.VisibilityState ?? NoState;
            set
            {
                if (value == null || value == NoState || value == _editor.VisibilityState)
                {
                    return;
                }

                CadEditResult result = _model.Document?.SetBlockEditorVisibilityState(value) ?? CadEditResult.Refuse("No drawing is shown.");

                if (!result.Succeeded)
                {
                    _model.Say(result.Message);
                }

                Raise(nameof(VisibilityState));
            }
        }

        /// <summary>The text of the visibility state box of a block of no states.</summary>
        private const string NoState = "(none)";

        /// <summary>Gets the command of Edit Block: the block open closed, then the Edit Block Definition dialog.</summary>
        public ICommand EditBlockCommand { get; }

        /// <summary>Gets the command of Save Block (BSAVE).</summary>
        public ICommand SaveBlockCommand { get; }

        /// <summary>Gets the command of Save As (BSAVEAS).</summary>
        public ICommand SaveAsCommand { get; }

        /// <summary>Gets the command of Attribute Definition (ATTDEF), which puts the definition into the block open.</summary>
        public ICommand AttributeDefinitionCommand { get; }

        /// <summary>Gets the command of Block Properties: Properties shows the Block group of the block open.</summary>
        public ICommand BlockPropertiesCommand { get; }

        /// <summary>Gets the command of Close Block Editor (BCLOSE).</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Reads the block's state again, after an edit, a save or an undo.</summary>
        internal void Refresh()
        {
            Raise(nameof(Name));
            Raise(nameof(HasUnsavedChanges));
            Raise(nameof(HasVisibilityStates));
            Raise(nameof(VisibilityStates));
            Raise(nameof(VisibilityState));
        }
    }

    /// <summary>A row of the Edit Block Definition list: a block's name, and "dynamic" or "attributes" beside it.</summary>
    public sealed class BlockEditRowViewModel
    {
        internal BlockEditRowViewModel(string name, string tag)
        {
            Name = name;
            Tag = tag;
        }

        /// <summary>Gets the name: &lt;Current Drawing&gt; or a block's.</summary>
        public string Name { get; }

        /// <summary>Gets what the row says of the block: dynamic, attributes, or nothing.</summary>
        public string Tag { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The logic of the Edit Block Definition dialog of BEDIT, and of the same window titled Save Block As: the list of
    /// &lt;Current Drawing&gt; then the drawing's named blocks (no layout, no external reference, no anonymous block), the name
    /// typed or chosen, the preview of the block chosen and its description. A name not in the list makes a new, empty block.
    /// </summary>
    public sealed class BlockEditViewModel : ObservableObject
    {
        private readonly BlockPictures _pictures;
        private string _name;
        private BlockEditRowViewModel _selected;

        /// <summary>Makes the dialog's logic over its request.</summary>
        /// <param name="request">The request of BEDIT or BSAVEAS.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public BlockEditViewModel(CadBlockEditRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            DxfDatabase database = request.Document.Database;
            var rows = new List<BlockEditRowViewModel>();

            if (!request.IsSaveAs)
            {
                rows.Add(new BlockEditRowViewModel(CadBlockEditor.CurrentDrawing, string.Empty));
            }

            foreach (DxfBlockTableRecord block in CadBlocks.Named(database))
            {
                string tag = block.IsDynamicBlock ? "dynamic" : block.OfType<DxfAttributeDefinition>().Any() ? "attributes" : string.Empty;
                rows.Add(new BlockEditRowViewModel(block.Name, tag));
            }

            Rows = rows;
            _pictures = new BlockPictures(database, request.Document.Fonts, 200, 170);
            Name = request.Name;
        }

        /// <summary>Gets the request the dialog answers.</summary>
        public CadBlockEditRequest Request { get; }

        /// <summary>Gets the title: Edit Block Definition, or Save Block As.</summary>
        public string Title => Request.Title;

        /// <summary>Gets the label over the name: "Block to create or edit", or "Block name" for Save Block As.</summary>
        public string NameLabel => Request.IsSaveAs ? "Block name" : "Block to create or edit";

        /// <summary>Gets the rows of the list.</summary>
        public IReadOnlyList<BlockEditRowViewModel> Rows { get; }

        /// <summary>Gets or sets the name typed; the row of that name is selected where there is one.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (!Set(ref _name, value ?? string.Empty))
                {
                    return;
                }

                BlockEditRowViewModel row = Rows.FirstOrDefault(r => string.Equals(r.Name, _name.Trim(), StringComparison.OrdinalIgnoreCase));

                if (!ReferenceEquals(row, _selected))
                {
                    _selected = row;
                    Raise(nameof(Selected));
                }

                Raise(nameof(Preview));
                Raise(nameof(Description));
                Raise(nameof(CanOk));
            }
        }

        /// <summary>Gets or sets the row selected, which gives the name.</summary>
        public BlockEditRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (value != null && !ReferenceEquals(value, _selected))
                {
                    Name = value.Name;
                }
            }
        }

        /// <summary>Gets the picture of the block named, white on black as the canvas draws it; null for a new name or &lt;Current Drawing&gt;.</summary>
        public ImageSource Preview => IsBlock ? _pictures.Draw(_selected.Name) : null;

        /// <summary>Gets the description of the block named; empty for none.</summary>
        public string Description => IsBlock ? Request.Document.Database.BlockTable[_selected.Name].Comments ?? string.Empty : string.Empty;

        /// <summary>
        /// Gets whether OK may be pressed: a name AutoCAD takes, or &lt;Current Drawing&gt;; for Save Block As, one no block of
        /// the drawing has.
        /// </summary>
        public bool CanOk
        {
            get
            {
                string name = (_name ?? string.Empty).Trim();

                if (Request.IsSaveAs)
                {
                    return CadBlocks.IsName(name) && !Request.Document.Database.BlockTable.Has(name);
                }

                return string.Equals(name, CadBlockEditor.CurrentDrawing, StringComparison.OrdinalIgnoreCase) || CadBlocks.IsName(name);
            }
        }

        private bool IsBlock => _selected != null && !string.Equals(_selected.Name, CadBlockEditor.CurrentDrawing, StringComparison.Ordinal);

        /// <summary>Closes the dialog: the name and the action written into the request.</summary>
        /// <param name="action">OK or Cancel.</param>
        public void Close(CadDialogAction action)
        {
            Request.Name = (_name ?? string.Empty).Trim();
            Request.Action = action == CadDialogAction.Ok && CanOk ? CadDialogAction.Ok : CadDialogAction.Cancel;
        }
    }
}
