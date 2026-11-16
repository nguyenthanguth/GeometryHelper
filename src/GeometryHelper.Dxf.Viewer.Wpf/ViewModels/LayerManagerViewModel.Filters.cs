using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>What a node of the Layer manager's filter tree is.</summary>
    public enum LayerFilterNodeKind
    {
        /// <summary>The root: every layer.</summary>
        All,

        /// <summary>One of the filters AutoCAD makes: All Used Layers, Unreconciled New Layers, Viewport Overrides.</summary>
        Dynamic,

        /// <summary>A property filter of the drawing.</summary>
        Property,

        /// <summary>A group filter of the drawing.</summary>
        Group,
    }

    /// <summary>One node of the filter tree on the left of the Layer manager: All, a filter of the drawing or one AutoCAD makes.</summary>
    public sealed class LayerFilterNodeViewModel : ObservableObject
    {
        private bool _expanded = true;
        private bool _selected;
        private bool _renaming;
        private string _editName = string.Empty;

        internal LayerFilterNodeViewModel(
            LayerFilterNodeKind kind,
            string name,
            IReadOnlyList<string> path,
            CadLayerFilter filter
        )
        {
            Kind = kind;
            Name = name;
            Path = path;
            Filter = filter;
        }

        /// <summary>Gets what the node is.</summary>
        public LayerFilterNodeKind Kind { get; }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets the names from the top: empty for All, one for a filter at the top, more for one nested.</summary>
        public IReadOnlyList<string> Path { get; }

        /// <summary>Gets the filter of the drawing the node stands for; null for All and for a filter AutoCAD makes.</summary>
        public CadLayerFilter Filter { get; }

        /// <summary>Gets the filters nested in it, by name.</summary>
        public ObservableCollection<LayerFilterNodeViewModel> Children { get; } =
            new ObservableCollection<LayerFilterNodeViewModel>();

        /// <summary>Gets whether it is one of the drawing's own filters: it can be renamed, changed and deleted.</summary>
        public bool IsOwn => Kind == LayerFilterNodeKind.Property || Kind == LayerFilterNodeKind.Group;

        /// <summary>Gets whether it is a group filter, which layers are dragged onto.</summary>
        public bool IsGroup => Kind == LayerFilterNodeKind.Group;

        /// <summary>Gets or sets whether its children are shown.</summary>
        public bool IsExpanded
        {
            get => _expanded;
            set => Set(ref _expanded, value);
        }

        /// <summary>Gets or sets whether it is the filter chosen, whose layers the list shows.</summary>
        public bool IsSelected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        /// <summary>Gets or sets whether the name is open for a new one, as F2 opens it.</summary>
        public bool IsRenaming
        {
            get => _renaming;
            set => Set(ref _renaming, value);
        }

        /// <summary>Gets or sets the name typed in the open name.</summary>
        public string EditName
        {
            get => _editName;
            set => Set(ref _editName, value ?? string.Empty);
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The filters of the Layer manager, as AutoCAD 2021's tree shows them: All, the filters the drawing keeps and the three
    /// AutoCAD makes; property filters made and changed in the Layer Filter Properties dialog, group filters given the layers
    /// dragged onto them or those of the objects selected, Invert filter, and Apply to layers toolbar.
    /// </summary>
    public sealed partial class LayerManagerViewModel
    {
        private readonly ObservableCollection<LayerFilterNodeViewModel> _filterTree =
            new ObservableCollection<LayerFilterNodeViewModel>();
        private LayerFilterNodeViewModel _selectedFilter;
        private string _filterStatus = string.Empty;
        private bool _readingFilters;
        private ICommand _newPropertyFilter,
            _newGroupFilter,
            _editFilter,
            _deleteFilter,
            _renameFilter,
            _convertToGroup,
            _addSelectedToGroup,
            _replaceGroupWithSelected,
            _addChosenToGroup;

        /// <summary>Gets the tree: one node, All, with the filters in it.</summary>
        public ObservableCollection<LayerFilterNodeViewModel> FilterTree => _filterTree;

        /// <summary>Gets the nodes of the group filters, for the right-click item that adds the layers chosen to one.</summary>
        public IReadOnlyList<LayerFilterNodeViewModel> GroupFilters =>
            Flatten(_filterTree).Where(n => n.IsGroup).ToList();

        /// <summary>Gets or sets the filter chosen in the tree; the list shows its layers, and it is the one kept in the drawing as the current one when the dialog closes.</summary>
        public LayerFilterNodeViewModel SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (value == null || _readingFilters)
                {
                    return;
                }

                if (Set(ref _selectedFilter, value))
                {
                    foreach (LayerFilterNodeViewModel node in Flatten(_filterTree))
                    {
                        node.IsSelected = node == value;
                    }

                    Document.LayerFilterPath = value.Path;
                    UpdateFilterCommands();
                }
            }
        }

        /// <summary>Gets or sets Invert filter: the layers the filter lets through are left out and the others listed.</summary>
        public bool InvertFilter
        {
            get => Document.InvertLayerFilter;
            set
            {
                Document.InvertLayerFilter = value;
                Raise(nameof(InvertFilter));
            }
        }

        /// <summary>Gets or sets Apply to layers toolbar: the layer box of the ribbon lists only the layers the filter lets through.</summary>
        public bool ApplyFilterToToolbar
        {
            get => Document.ApplyLayerFilterToToolbar;
            set
            {
                Document.ApplyLayerFilterToToolbar = value;
                Raise(nameof(ApplyFilterToToolbar));
            }
        }

        /// <summary>Gets the line under the tree: "All: displaying 6 of 6 layers", "Steel (inverted): displaying 2 of 6 layers".</summary>
        public string FilterStatus
        {
            get => _filterStatus;
            private set => Set(ref _filterStatus, value);
        }

        /// <summary>
        /// Gets or sets what shows AutoCAD's Layer Filter Properties dialog, as the window shows <see cref="LayerFilterPropertiesWindow"/>:
        /// given the dialog's logic, it says whether OK was pressed. Null for none, when New Property Filter and Filter Properties do nothing.
        /// </summary>
        public Func<LayerFilterEditViewModel, bool> EditFilter { get; set; }

        /// <summary>Gets the command of New Property Filter: the Layer Filter Properties dialog over a new filter, nested in the one chosen where it is one of the drawing's.</summary>
        public ICommand NewPropertyFilterCommand =>
            _newPropertyFilter ?? (_newPropertyFilter = new DelegateCommand(_ => NewPropertyFilter()));

        /// <summary>Gets the command of New Group Filter: a group filter named Group Filter1, or the first the drawing has not, empty and open for a new name.</summary>
        public ICommand NewGroupFilterCommand =>
            _newGroupFilter ?? (_newGroupFilter = new DelegateCommand(_ => NewGroupFilter()));

        /// <summary>Gets the command of Properties on a property filter: the Layer Filter Properties dialog over it.</summary>
        public ICommand EditFilterCommand =>
            _editFilter
            ?? (
                _editFilter = new DelegateCommand(
                    _ => ChangeFilter(_selectedFilter),
                    _ => _selectedFilter?.Kind == LayerFilterNodeKind.Property
                )
            );

        /// <summary>Gets the command of Delete on a filter of the drawing; the layers stay.</summary>
        public ICommand DeleteFilterCommand =>
            _deleteFilter
            ?? (
                _deleteFilter = new DelegateCommand(
                    _ => DeleteFilter(_selectedFilter),
                    _ => _selectedFilter?.IsOwn == true
                )
            );

        /// <summary>Gets the command of Rename on a filter of the drawing: its name opened in the tree.</summary>
        public ICommand RenameFilterCommand =>
            _renameFilter
            ?? (
                _renameFilter = new DelegateCommand(
                    _ => BeginRenameFilter(_selectedFilter),
                    _ => _selectedFilter?.IsOwn == true
                )
            );

        /// <summary>Gets the command of Convert to Group Filter on a property filter: the layers it shows now become a group's.</summary>
        public ICommand ConvertToGroupCommand =>
            _convertToGroup
            ?? (
                _convertToGroup = new DelegateCommand(
                    _ => Done(CadLayerFilters.ConvertToGroup(Document, _selectedFilter.Path)),
                    _ => _selectedFilter?.Kind == LayerFilterNodeKind.Property
                )
            );

        /// <summary>Gets the command of Select Layers, Add, on a group filter: the layers of the objects selected in the drawing are added to it.</summary>
        public ICommand AddSelectedToGroupCommand =>
            _addSelectedToGroup
            ?? (
                _addSelectedToGroup = new DelegateCommand(
                    _ => SelectLayers(_selectedFilter, false),
                    _ => _selectedFilter?.IsGroup == true
                )
            );

        /// <summary>Gets the command of Select Layers, Replace, on a group filter: it holds the layers of the objects selected in the drawing and no others.</summary>
        public ICommand ReplaceGroupWithSelectedCommand =>
            _replaceGroupWithSelected
            ?? (
                _replaceGroupWithSelected = new DelegateCommand(
                    _ => SelectLayers(_selectedFilter, true),
                    _ => _selectedFilter?.IsGroup == true
                )
            );

        /// <summary>Gets the command of Add to Group Filter on the layers chosen: its parameter is the node of the group filter.</summary>
        public ICommand AddChosenToGroupCommand =>
            _addChosenToGroup
            ?? (
                _addChosenToGroup = new DelegateCommand(p =>
                {
                    if (p is LayerFilterNodeViewModel node)
                    {
                        AddLayersToGroup(node, ChosenNames());
                    }
                })
            );

        /// <summary>
        /// Adds layers to a group filter, as dragging them onto the group in the tree does; layers it has already are not added twice.
        /// </summary>
        /// <param name="group">The node of the group filter.</param>
        /// <param name="layerNames">The layers.</param>
        /// <returns>Whether they were added; why not is said on the command line.</returns>
        public bool AddLayersToGroup(LayerFilterNodeViewModel group, IEnumerable<string> layerNames)
        {
            if (group == null || !group.IsGroup)
            {
                Say("Layers are added to a group filter: choose one.");
                return false;
            }

            return Done(CadLayerFilters.SetLayers(Document, group.Path, layerNames, false));
        }

        /// <summary>
        /// Select Layers: the layers of the objects selected in the drawing, added to the group filter or in place of its layers. AutoCAD
        /// hides the dialog and asks for objects; here the objects are those selected before the dialog was opened.
        /// </summary>
        /// <param name="group">The node of the group filter.</param>
        /// <param name="replace">Whether they replace the layers the group has.</param>
        /// <returns>Whether the group was changed; why not is said on the command line.</returns>
        public bool SelectLayers(LayerFilterNodeViewModel group, bool replace)
        {
            if (group == null || !group.IsGroup)
            {
                Say("Layers are added to a group filter: choose one.");
                return false;
            }

            var layers = new List<string>();

            foreach (string handle in Document.Selection.Handles)
            {
                string layer = Document.FindEntity(handle)?.Layer;

                if (layer != null && !layers.Contains(layer, StringComparer.OrdinalIgnoreCase))
                {
                    layers.Add(layer);
                }
            }

            if (layers.Count == 0)
            {
                Say("No objects are selected: select objects in the drawing, then open the Layer manager.");
                return false;
            }

            return Done(CadLayerFilters.SetLayers(Document, group.Path, layers, replace));
        }

        /// <summary>Makes a group filter named Group Filter1, or the first the drawing has not, empty, nested in the filter chosen where it is one of the drawing's; its name is open in the tree.</summary>
        /// <returns>The name made, or null where it was refused.</returns>
        public string NewGroupFilter()
        {
            IReadOnlyList<string> parent = ParentOfNew();
            string name = FreeFilterName(parent, "Group Filter");

            if (!Done(CadLayerFilters.AddGroup(Document, parent, name, null)))
            {
                return null;
            }

            LayerFilterNodeViewModel made = Find(parent.Concat(new[] { name }).ToList());

            if (made != null)
            {
                // Choosing it lists the layers again and builds the tree again: its name is opened on the new node.
                SelectedFilter = made;
                BeginRenameFilter(SelectedFilter);
            }

            return name;
        }

        /// <summary>
        /// Shows Layer Filter Properties over a new property filter, named Properties Filter1 or the first the drawing has not, nested in
        /// the filter chosen where it is one of the drawing's; OK adds it, Cancel does nothing.
        /// </summary>
        /// <returns>The name added, or null where it was cancelled or refused.</returns>
        public string NewPropertyFilter()
        {
            IReadOnlyList<string> parent = ParentOfNew();
            var dialog = new LayerFilterEditViewModel(Document, parent, null, _target);
            return EditFilter != null && EditFilter(dialog) && Done(dialog.Apply()) ? dialog.Name : null;
        }

        /// <summary>Shows Layer Filter Properties over a property filter; OK changes its definition and its name.</summary>
        /// <param name="node">The node of the filter.</param>
        /// <returns>Whether it was changed.</returns>
        public bool ChangeFilter(LayerFilterNodeViewModel node)
        {
            if (node == null || node.Kind != LayerFilterNodeKind.Property)
            {
                return false;
            }

            var dialog = new LayerFilterEditViewModel(
                Document,
                node.Path.Take(node.Path.Count - 1).ToList(),
                node.Filter,
                _target
            );
            return EditFilter != null && EditFilter(dialog) && Done(dialog.Apply());
        }

        /// <summary>Deletes a filter of the drawing with the filters nested in it; the layers stay. The filter shown falls back to All.</summary>
        /// <param name="node">The node of the filter.</param>
        /// <returns>Whether it was deleted.</returns>
        public bool DeleteFilter(LayerFilterNodeViewModel node)
        {
            if (node == null || !node.IsOwn)
            {
                return false;
            }

            bool shown =
                Document.LayerFilterPath.Count >= node.Path.Count
                && node.Path.Zip(Document.LayerFilterPath, (a, b) =>
                        string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
                    )
                    .All(x => x);

            if (!Done(CadLayerFilters.Delete(Document, node.Path)))
            {
                return false;
            }

            if (shown)
            {
                Document.LayerFilterPath = Array.Empty<string>();
            }

            return true;
        }

        /// <summary>Opens the name of a filter of the drawing in the tree, the whole name chosen.</summary>
        /// <param name="node">The node.</param>
        /// <returns>Whether it was opened.</returns>
        public bool BeginRenameFilter(LayerFilterNodeViewModel node)
        {
            if (node == null || !node.IsOwn)
            {
                return false;
            }

            foreach (LayerFilterNodeViewModel other in Flatten(_filterTree).Where(n => n.IsRenaming && n != node))
            {
                other.IsRenaming = false;
            }

            node.EditName = node.Name;
            node.IsRenaming = true;
            return true;
        }

        /// <summary>Enter or a click elsewhere in a filter's name: the filter renamed, one undo step; a name a sibling has is refused on the command line and the cell left open.</summary>
        /// <param name="node">The node being renamed.</param>
        /// <returns>Whether the name was shut: the filter renamed, or its name left as it was.</returns>
        public bool CommitRenameFilter(LayerFilterNodeViewModel node)
        {
            if (node == null || !node.IsRenaming)
            {
                return false;
            }

            string wanted = (node.EditName ?? string.Empty).Trim();
            IReadOnlyList<string> parent = node.Path.Take(node.Path.Count - 1).ToList();

            if (!Done(CadLayerFilters.Rename(Document, node.Path, wanted)))
            {
                return false;
            }

            node.IsRenaming = false;

            // The tree is read again by the rename; the filter shown keeps its place under its new name.
            if (Document.LayerFilterPath.SequenceEqual(node.Path, StringComparer.OrdinalIgnoreCase))
            {
                Document.LayerFilterPath = parent.Concat(new[] { wanted }).ToList();
            }

            RefreshFilters();
            return true;
        }

        /// <summary>Esc in a filter's name: the cell shut, the old name kept.</summary>
        /// <param name="node">The node being renamed.</param>
        public void CancelRenameFilter(LayerFilterNodeViewModel node)
        {
            if (node != null)
            {
                node.IsRenaming = false;
                node.EditName = node.Name;
            }
        }

        /// <summary>Finds a node by the names from the top; null where the tree has none.</summary>
        /// <param name="path">The names, empty for All.</param>
        /// <returns>The node.</returns>
        public LayerFilterNodeViewModel Find(IReadOnlyList<string> path) =>
            Flatten(_filterTree)
                .FirstOrDefault(n =>
                    n.Path.Count == path.Count
                    && n.Path.Zip(path, (a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase)).All(x => x)
                );

        private IReadOnlyList<string> ParentOfNew() =>
            _selectedFilter != null && _selectedFilter.IsOwn ? _selectedFilter.Path : Array.Empty<string>();

        private static IEnumerable<LayerFilterNodeViewModel> Flatten(IEnumerable<LayerFilterNodeViewModel> nodes)
        {
            foreach (LayerFilterNodeViewModel node in nodes)
            {
                yield return node;

                foreach (LayerFilterNodeViewModel child in Flatten(node.Children))
                {
                    yield return child;
                }
            }
        }

        /// <summary>The first name of a kind of filter, as Group Filter1, no sibling has.</summary>
        internal string FreeFilterName(IReadOnlyList<string> parent, string stem)
        {
            LayerFilterNodeViewModel at = Find(parent);
            IEnumerable<string> siblings = at == null ? Array.Empty<string>() : at.Children.Select(c => c.Name);
            var taken = new HashSet<string>(siblings, StringComparer.OrdinalIgnoreCase);
            int n = 1;

            while (taken.Contains(stem + n.ToString(CultureInfo.InvariantCulture)))
            {
                n++;
            }

            return stem + n.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateFilterCommands()
        {
            foreach (
                ICommand command in new[]
                {
                    _editFilter,
                    _deleteFilter,
                    _renameFilter,
                    _convertToGroup,
                    _addSelectedToGroup,
                    _replaceGroupWithSelected,
                }
            )
            {
                (command as DelegateCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>Builds the tree again from the drawing, the filter chosen kept by its names (All where it is gone), and the nodes' expansion kept.</summary>
        private void RefreshFilters()
        {
            var collapsed = new HashSet<string>(
                Flatten(_filterTree).Where(n => !n.IsExpanded).Select(n => string.Join("/", n.Path)),
                StringComparer.OrdinalIgnoreCase
            );
            _readingFilters = true;

            try
            {
                var root = new LayerFilterNodeViewModel(LayerFilterNodeKind.All, "All", Array.Empty<string>(), null);

                foreach (CadLayerFilter filter in CadLayerFilters.Read(Document.Database))
                {
                    root.Children.Add(NodeOf(filter, collapsed));
                }

                foreach (KeyValuePair<string, string> made in CadLayerFilters.Dynamic)
                {
                    root.Children.Add(
                        new LayerFilterNodeViewModel(LayerFilterNodeKind.Dynamic, made.Key, new[] { made.Key }, null)
                    );
                }

                _filterTree.Clear();
                _filterTree.Add(root);
                LayerFilterNodeViewModel chosen = Find(Document.LayerFilterPath) ?? root;
                chosen.IsSelected = true;
                _selectedFilter = chosen;
            }
            finally
            {
                _readingFilters = false;
            }

            Raise(nameof(SelectedFilter));
            Raise(nameof(GroupFilters));
            UpdateFilterCommands();
        }

        private static LayerFilterNodeViewModel NodeOf(CadLayerFilter filter, HashSet<string> collapsed)
        {
            var node = new LayerFilterNodeViewModel(
                filter.Kind == CadLayerFilterKind.Group ? LayerFilterNodeKind.Group : LayerFilterNodeKind.Property,
                filter.Name,
                filter.Path,
                filter
            )
            {
                IsExpanded = !collapsed.Contains(string.Join("/", filter.Path)),
            };

            foreach (CadLayerFilter child in filter.Children)
            {
                node.Children.Add(NodeOf(child, collapsed));
            }

            return node;
        }

        private void OnLayerFilterChanged(object sender, EventArgs e)
        {
            Refresh();
            Raise(nameof(InvertFilter));
            Raise(nameof(ApplyFilterToToolbar));
        }

        /// <summary>The line under the tree: the filter's name, "(inverted)" where it is, and how many of the layers are listed.</summary>
        private string StatusOf(int shown, int total)
        {
            string name = _selectedFilter?.Name ?? "All";
            return name
                + (Document.InvertLayerFilter ? " (inverted)" : string.Empty)
                + ": displaying "
                + MainViewModel.Count(shown)
                + " of "
                + MainViewModel.Count(total)
                + (total == 1 ? " layer" : " layers");
        }
    }
}
