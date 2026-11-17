using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>What a row of DesignCenter's tree is.</summary>
    public enum DesignCenterNodeKind
    {
        /// <summary>A folder: its folders and the drawings in it.</summary>
        Folder,

        /// <summary>A drawing (a .dwg, .dws, .dwt or .dxf, or one open in the presenter): the kinds of content it holds.</summary>
        Drawing,

        /// <summary>A kind of content of a drawing: Blocks, Layers, Textstyles.</summary>
        Content,

        /// <summary>The row an expander shows before its folder or drawing is read; replaced when it is opened.</summary>
        Placeholder,
    }

    /// <summary>A row of DesignCenter's tree, read as it is opened: a folder's folders and drawings, a drawing's kinds of content.</summary>
    public sealed class DesignCenterNode : ObservableObject
    {
        private readonly DesignCenterViewModel _owner;
        private bool _isExpanded;
        private bool _isSelected;
        private bool _loaded;

        internal DesignCenterNode(
            DesignCenterViewModel owner,
            DesignCenterNode parent,
            DesignCenterNodeKind kind,
            string text,
            string path
        )
        {
            _owner = owner;
            Parent = parent;
            Kind = kind;
            Text = text;
            Path = path;

            if (kind == DesignCenterNodeKind.Folder || kind == DesignCenterNodeKind.Drawing)
            {
                Children.Add(new DesignCenterNode(owner, this, DesignCenterNodeKind.Placeholder, string.Empty, null));
            }
        }

        /// <summary>Gets what the row is.</summary>
        public DesignCenterNodeKind Kind { get; }

        /// <summary>Gets the words of the row: a folder's or drawing's name, or a kind of content as AutoCAD names it (Blocks, Layers).</summary>
        public string Text { get; internal set; }

        /// <summary>Gets the path of the folder or drawing; for a kind of content, that of its drawing; null for a drawing not saved yet.</summary>
        public string Path { get; }

        /// <summary>Gets the kind of content of a Content row; null for another.</summary>
        public CadDesignCenterKind? ContentKind { get; internal set; }

        /// <summary>Gets the row above it; null for a root.</summary>
        public DesignCenterNode Parent { get; }

        /// <summary>Gets the drawing a Drawing or Content row lists, once read; null before, and where it cannot be read.</summary>
        public CadDesignCenterDrawing Drawing { get; internal set; }

        /// <summary>Gets the rows under it.</summary>
        public ObservableCollection<DesignCenterNode> Children { get; } = new ObservableCollection<DesignCenterNode>();

        /// <summary>Gets or sets whether the row is open; opening it reads what is under it.</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (Set(ref _isExpanded, value) && value)
                {
                    EnsureLoaded();
                }
            }
        }

        /// <summary>Gets or sets whether the row is the one selected; selecting it lists its content on the right.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (Set(ref _isSelected, value) && value)
                {
                    _owner.Select(this);
                }
            }
        }

        /// <summary>Marks the row as no longer the one selected, without selecting anything: the palette has another selected.</summary>
        internal void Deselect()
        {
            if (_isSelected)
            {
                _isSelected = false;
                Raise(nameof(IsSelected));
            }
        }

        /// <summary>Reads what is under a folder or drawing, once: the folders and drawings of a folder, the kinds of content of a drawing.</summary>
        public void EnsureLoaded()
        {
            if (_loaded || (Kind != DesignCenterNodeKind.Folder && Kind != DesignCenterNodeKind.Drawing))
            {
                return;
            }

            _loaded = true;
            Children.Clear();

            foreach (DesignCenterNode child in _owner.ReadChildren(this))
            {
                Children.Add(child);
            }
        }
    }

    /// <summary>What a row of DesignCenter's list on the right is.</summary>
    public enum DesignCenterItemKind
    {
        /// <summary>A folder.</summary>
        Folder,

        /// <summary>A drawing file.</summary>
        Drawing,

        /// <summary>A kind of content of a drawing.</summary>
        Section,

        /// <summary>A block, layer, style, layout or reference.</summary>
        Item,
    }

    /// <summary>A row of DesignCenter's list: a folder, a drawing, a kind of content or one thing of a drawing, with its picture where it has one.</summary>
    public sealed class DesignCenterItemViewModel : ObservableObject
    {
        private readonly DesignCenterViewModel _owner;
        private ImageSource _picture;
        private bool _pictureMade;

        internal DesignCenterItemViewModel(
            DesignCenterViewModel owner,
            DesignCenterItemKind kind,
            string name,
            string description,
            string path
        )
        {
            _owner = owner;
            Kind = kind;
            Name = name;
            Description = description ?? string.Empty;
            Path = path;
        }

        /// <summary>Gets what the row is.</summary>
        public DesignCenterItemKind Kind { get; }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets the description shown under the list.</summary>
        public string Description { get; }

        /// <summary>Gets the path of the folder or drawing the row is, or is in; null for a drawing not saved yet.</summary>
        public string Path { get; }

        /// <summary>Gets the kind of content of a Section or Item row.</summary>
        public CadDesignCenterKind? ContentKind { get; internal set; }

        /// <summary>Gets the drawing an Item or Section row is in.</summary>
        public CadDesignCenterDrawing Drawing { get; internal set; }

        /// <summary>The row of the tree a Folder, Drawing or Section row stands for, which a double click selects.</summary>
        internal DesignCenterNode Node { get; set; }

        /// <summary>Gets the picture of the row, made when it is first asked: a block drawn, a drawing's model space; null for a row with none.</summary>
        public ImageSource Picture
        {
            get
            {
                if (!_pictureMade)
                {
                    _pictureMade = true;
                    _picture = _owner.PictureOf(this, 72, 54);
                }

                return _picture;
            }
        }

        /// <summary>Gets the larger picture the preview pane shows.</summary>
        public ImageSource Preview => _owner.PictureOf(this, 260, 190);
    }

    /// <summary>One thing the right-click menu or a double click can do with the rows selected.</summary>
    public sealed class DesignCenterAction
    {
        internal DesignCenterAction(string text, Action run)
        {
            Text = text;
            Run = run;
        }

        /// <summary>Gets the words of the menu item.</summary>
        public string Text { get; }

        /// <summary>Gets what it does.</summary>
        public Action Run { get; }
    }

    /// <summary>
    /// What a drag out of DesignCenter carries, as one text: the path of the drawing, the kind of content and the names, which
    /// the canvas reads back where it is dropped.
    /// </summary>
    public static class DesignCenterDrag
    {
        /// <summary>The name of the data format a drag carries.</summary>
        public const string Format = "GeometryHelper.DesignCenter";

        /// <summary>Writes the path, the kind (empty for a whole drawing file) and the names as one text.</summary>
        /// <param name="path">The path of the drawing.</param>
        /// <param name="kind">The kind of content; null for the drawing file itself.</param>
        /// <param name="names">The names dragged.</param>
        /// <returns>The text.</returns>
        public static string Write(string path, CadDesignCenterKind? kind, IEnumerable<string> names) =>
            string.Join("\n", new[] { path, kind?.ToString() ?? string.Empty }.Concat(names ?? Array.Empty<string>()));

        /// <summary>Reads what <see cref="Write"/> wrote.</summary>
        /// <param name="text">The text.</param>
        /// <param name="path">Receives the path of the drawing.</param>
        /// <param name="kind">Receives the kind; null for the drawing file itself.</param>
        /// <param name="names">Receives the names.</param>
        /// <returns>Whether the text was one a drag wrote.</returns>
        public static bool TryRead(
            string text,
            out string path,
            out CadDesignCenterKind? kind,
            out IReadOnlyList<string> names
        )
        {
            path = null;
            kind = null;
            names = Array.Empty<string>();
            string[] parts = (text ?? string.Empty).Split('\n');

            if (parts.Length < 2 || parts[0].Length == 0)
            {
                return false;
            }

            if (
                parts[1].Length > 0
                && Enum.TryParse(parts[1], out CadDesignCenterKind parsed)
                && Enum.IsDefined(typeof(CadDesignCenterKind), parsed)
            )
            {
                kind = parsed;
            }
            else if (parts[1].Length > 0)
            {
                return false;
            }

            path = parts[0];
            names = parts.Skip(2).Where(n => n.Length > 0).ToList();
            return true;
        }
    }

    /// <summary>
    /// AutoCAD's DesignCenter palette: Folders, Open Drawings and History tabs over a tree of folders, drawings and their kinds of
    /// content (Blocks, Dimstyles, Layers, Layouts, Linetypes, Multileaderstyles, Tablestyles, Textstyles, Xrefs), a list of what
    /// the one selected holds with its pictures, and a preview and description under it. A block is inserted, a layer, style,
    /// linetype or layout copied into the drawing shown (what it has by the name kept as it is), an external reference attached,
    /// by a double click, the right-click menu or a drag onto the canvas. Back, Forward and Up walk the places visited.
    /// </summary>
    public sealed class DesignCenterViewModel : ObservableObject
    {
        /// <summary>The tabs, in order.</summary>
        public static readonly IReadOnlyList<string> TabNames = new[] { "Folders", "Open Drawings", "History" };

        private static readonly string[] DrawingExtensions = { ".dwg", ".dws", ".dwt", ".dxf" };

        private readonly MainViewModel _model;
        private readonly IReadOnlyList<string> _roots;
        private readonly List<string> _back = new List<string>();
        private readonly List<string> _forward = new List<string>();
        private string _tab = TabNames[0];
        private DesignCenterNode _selected;
        private bool _showTree = true;
        private bool _showPreview = true;
        private bool _showDescription = true;
        private string _message = string.Empty;
        private IReadOnlyList<DesignCenterItemViewModel> _selectedItems = Array.Empty<DesignCenterItemViewModel>();
        private bool _walking;
        private string _location = string.Empty;

        /// <summary>Makes the palette over the window's view model, its Folders tab rooted at the desktop, the documents folder and the drives.</summary>
        /// <param name="model">The window's view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public DesignCenterViewModel(MainViewModel model)
            : this(model, DefaultRoots()) { }

        /// <summary>Makes the palette with the folders of the Folders tab given, as a test or a host with its own places does.</summary>
        /// <param name="model">The window's view model.</param>
        /// <param name="roots">The folders the Folders tab starts with, in order.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model or the folders are null.</exception>
        public DesignCenterViewModel(MainViewModel model, IEnumerable<string> roots)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _roots = (roots ?? throw new ArgumentNullException(nameof(roots))).Where(Directory.Exists).ToList();
            LoadCommand = new DelegateCommand(_ => LoadRequested?.Invoke(this, EventArgs.Empty));
            BackCommand = new DelegateCommand(_ => Back(), _ => _back.Count > 0);
            ForwardCommand = new DelegateCommand(_ => Forward(), _ => _forward.Count > 0);
            UpCommand = new DelegateCommand(_ => Up(), _ => UpPath() != null);
            Refill();
        }

        /// <summary>Raised when the Load button asks for a drawing file; the window shows its file dialog and calls <see cref="Load"/>.</summary>
        public event EventHandler LoadRequested;

        /// <summary>Gets the command of the Load button, which raises <see cref="LoadRequested"/>.</summary>
        public System.Windows.Input.ICommand LoadCommand { get; }

        /// <summary>Gets the command of the Back button.</summary>
        public System.Windows.Input.ICommand BackCommand { get; }

        /// <summary>Gets the command of the Forward button.</summary>
        public System.Windows.Input.ICommand ForwardCommand { get; }

        /// <summary>Gets the command of the Up button: the folder above the one shown.</summary>
        public System.Windows.Input.ICommand UpCommand { get; }

        /// <summary>Gets the roots of the tab shown.</summary>
        public ObservableCollection<DesignCenterNode> Roots { get; } = new ObservableCollection<DesignCenterNode>();

        /// <summary>Gets the rows of the list on the right.</summary>
        public ObservableCollection<DesignCenterItemViewModel> Items { get; } =
            new ObservableCollection<DesignCenterItemViewModel>();

        /// <summary>Gets the drawings the History tab lists, the latest first.</summary>
        public IReadOnlyList<string> History => _model.DesignCenterHistory;

        /// <summary>Gets or sets the tab shown: Folders, Open Drawings or History.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (TabNames.Contains(value) && Set(ref _tab, value))
                {
                    _selected = null;
                    Items.Clear();
                    Refill();
                    Raise(nameof(Location));
                }
            }
        }

        /// <summary>Gets the row of the tree selected; null for none.</summary>
        public DesignCenterNode Selected => _selected;

        /// <summary>Gets the words in the address line above the list: the path of the folder, drawing or content shown.</summary>
        public string Location => _location;

        /// <summary>Gets or sets whether the tree is shown; off, the list takes the width, as the Tree View Toggle does.</summary>
        public bool ShowTree
        {
            get => _showTree;
            set => Set(ref _showTree, value);
        }

        /// <summary>Gets or sets whether the picture of the row selected is shown under the list.</summary>
        public bool ShowPreview
        {
            get => _showPreview;
            set => Set(ref _showPreview, value);
        }

        /// <summary>Gets or sets whether the description of the row selected is shown under the list.</summary>
        public bool ShowDescription
        {
            get => _showDescription;
            set => Set(ref _showDescription, value);
        }

        /// <summary>Gets what the last action said, as "2 layers added, 1 kept."; empty for nothing.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets the rows selected in the list.</summary>
        public IReadOnlyList<DesignCenterItemViewModel> SelectedItems => _selectedItems;

        /// <summary>Gets the picture of the first row selected, larger; null for none.</summary>
        public ImageSource Preview => _selectedItems.Count == 0 ? null : _selectedItems[0].Preview;

        /// <summary>Gets the description of the first row selected.</summary>
        public string Description => _selectedItems.Count == 0 ? string.Empty : _selectedItems[0].Description;

        /// <summary>Gets the document the content is copied into; null where none is shown.</summary>
        public CadDocument Document => _model.Document;

        /// <summary>Sets the rows selected in the list, which the preview, the description and the menu act on.</summary>
        /// <param name="items">The rows.</param>
        public void SetSelectedItems(IReadOnlyList<DesignCenterItemViewModel> items)
        {
            _selectedItems = items ?? Array.Empty<DesignCenterItemViewModel>();
            Raise(nameof(SelectedItems));
            Raise(nameof(Preview));
            Raise(nameof(Description));
        }

        /// <summary>Lists the drawings open in the presenter again, as the Open Drawings tab shows them; a tab of the others is left as it is.</summary>
        public void RefreshOpenDrawings()
        {
            if (_tab == "Open Drawings")
            {
                Refill();
            }
        }

        /// <summary>
        /// Load: shows a drawing's content. The Folders tab opens at it, the folders above it opened, the drawing selected; it
        /// is put in History.
        /// </summary>
        /// <param name="path">The path of a drawing file.</param>
        /// <returns>False where the file is not there or is not a drawing's name.</returns>
        public bool Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !IsDrawingFile(path))
            {
                Message = "Cannot find " + path + ".";
                return false;
            }

            return NavigateTo(Path.GetFullPath(path), null);
        }

        /// <summary>
        /// Shows a folder, a drawing or one kind of a drawing's content: in the tab shown where it is there (a drawing on the Open
        /// Drawings tab), else on the Folders tab, the nodes above it opened and it selected.
        /// </summary>
        /// <param name="path">The folder or drawing.</param>
        /// <param name="kind">The kind of content to show; null for the folder or drawing itself.</param>
        /// <returns>Whether it was found in the tree.</returns>
        public bool NavigateTo(string path, CadDesignCenterKind? kind)
        {
            // The tab shown is looked in first; the Folders tab is where a place that is not in it is found.
            DesignCenterNode node = Reveal(path);

            if (node == null && _tab != TabNames[0])
            {
                Tab = TabNames[0];
                node = Reveal(path);
            }

            if (node == null)
            {
                Message = "Cannot show " + path + ".";
                return false;
            }

            if (kind != null && node.Kind == DesignCenterNodeKind.Drawing)
            {
                node.IsExpanded = true;
                DesignCenterNode content = node.Children.FirstOrDefault(c => c.ContentKind == kind);

                if (content != null)
                {
                    node = content;
                }
            }

            node.IsSelected = true;
            return true;
        }

        /// <summary>Back: shows the place visited before this one.</summary>
        public void Back() => Step(_back, _forward);

        /// <summary>Forward: shows the place Back left.</summary>
        public void Forward() => Step(_forward, _back);

        /// <summary>Up: shows the folder above the one shown, or the folder a drawing is in.</summary>
        public void Up()
        {
            string up = UpPath();

            if (up != null)
            {
                NavigateTo(up, null);
            }
        }

        /// <summary>Reads what the tree holds under a folder or a drawing; used as a row is opened.</summary>
        internal IEnumerable<DesignCenterNode> ReadChildren(DesignCenterNode node)
        {
            if (node.Kind == DesignCenterNodeKind.Folder)
            {
                return ReadFolder(node);
            }

            return ReadDrawing(node);
        }

        /// <summary>A row of the tree selected: the list on the right shows what it holds.</summary>
        internal void Select(DesignCenterNode node)
        {
            // A row selected by code leaves the one selected before it, as the tree does it for a click.
            if (_selected != null && !ReferenceEquals(_selected, node))
            {
                _selected.Deselect();
            }

            _selected = node;
            Items.Clear();
            string place = null;

            switch (node.Kind)
            {
                case DesignCenterNodeKind.Folder:
                    foreach (DesignCenterItemViewModel item in FolderItems(node))
                    {
                        Items.Add(item);
                    }

                    place = node.Path;
                    break;
                case DesignCenterNodeKind.Drawing:
                    OpenDrawingNode(node);
                    node.EnsureLoaded();

                    foreach (
                        DesignCenterNode section in node.Children.Where(c => c.Kind == DesignCenterNodeKind.Content)
                    )
                    {
                        int count = node.Drawing.Items(section.ContentKind.Value).Count;
                        Items.Add(
                            new DesignCenterItemViewModel(
                                this,
                                DesignCenterItemKind.Section,
                                section.Text,
                                count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " item" : " items"),
                                node.Path
                            )
                            {
                                ContentKind = section.ContentKind,
                                Drawing = node.Drawing,
                                Node = section,
                            }
                        );
                    }

                    if (node.Drawing != null)
                    {
                        Note(node.Path);
                    }

                    place = node.Path ?? node.Text;
                    break;
                case DesignCenterNodeKind.Content:
                    foreach (
                        CadDesignCenterItem item in node.Drawing?.Items(node.ContentKind.Value)
                            ?? new CadDesignCenterItem[0]
                    )
                    {
                        Items.Add(
                            new DesignCenterItemViewModel(
                                this,
                                DesignCenterItemKind.Item,
                                item.Name,
                                item.Description,
                                node.Path
                            )
                            {
                                ContentKind = item.Kind,
                                Drawing = node.Drawing,
                            }
                        );
                    }

                    place = (node.Path ?? node.Parent?.Text) + "\\" + node.Text;
                    break;
            }

            if (
                !_walking
                && !string.IsNullOrEmpty(_location)
                && !string.Equals(_location, place, StringComparison.OrdinalIgnoreCase)
            )
            {
                _back.Add(_location);
                _forward.Clear();
            }

            _location = place ?? string.Empty;
            SetSelectedItems(Array.Empty<DesignCenterItemViewModel>());
            Raise(nameof(Selected));
            Raise(nameof(Location));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>The picture of a row: a block drawn, a drawing's model space; null for a row with none, or one that cannot be drawn.</summary>
        internal ImageSource PictureOf(DesignCenterItemViewModel item, int width, int height)
        {
            try
            {
                CadFonts fonts = _model.Document?.Fonts;

                if (item.Kind == DesignCenterItemKind.Drawing && item.Path != null)
                {
                    return ExternalReferencesViewModel.PreviewOf(item.Path, false, fonts, width, height);
                }

                if (item.Kind == DesignCenterItemKind.Item && item.ContentKind == CadDesignCenterKind.Blocks)
                {
                    return new BlockPictures(item.Drawing.WorkingCopy(), fonts, width, height).Draw(item.Name);
                }
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidOperationException
                    || exception is IOException
                    || exception is NotSupportedException
                    || exception is FormatException
                )
            {
                GeometryHelperLog.Debug("DesignCenterViewModel: no picture of " + item.Name + ": " + exception.Message);
            }

            return null;
        }

        /// <summary>What a double click does on a row: a folder, drawing or kind is shown, a block inserted, a layer, style, linetype or layout copied in, a reference attached.</summary>
        /// <param name="item">The row.</param>
        public void Activate(DesignCenterItemViewModel item)
        {
            if (item == null)
            {
                return;
            }

            switch (item.Kind)
            {
                case DesignCenterItemKind.Folder:
                case DesignCenterItemKind.Drawing:
                case DesignCenterItemKind.Section:
                    if (item.Node != null)
                    {
                        Show(item.Node);
                    }

                    break;
                default:
                    ActionsFor(new[] { item }).FirstOrDefault()?.Run();
                    break;
            }
        }

        /// <summary>Selects a row of the tree, the rows above it opened, as the tree does for a click on it.</summary>
        private static void Show(DesignCenterNode node)
        {
            for (DesignCenterNode above = node.Parent; above != null; above = above.Parent)
            {
                above.IsExpanded = true;
            }

            node.IsSelected = true;
        }

        /// <summary>
        /// The things the right-click menu offers for rows: for blocks Insert Block and Insert Block… (the Blocks palette) and
        /// Add Block Definition; for a layer, linetype, style or layout Add Layer(s), Load Linetype(s) and the like; for an
        /// external reference Attach Xref; for a drawing file Open in Application Window, Insert as Block and Attach as Xref.
        /// The first is what a double click does.
        /// </summary>
        /// <param name="items">The rows selected, of one kind.</param>
        /// <returns>The actions, empty where none is offered.</returns>
        public IReadOnlyList<DesignCenterAction> ActionsFor(IReadOnlyList<DesignCenterItemViewModel> items)
        {
            var actions = new List<DesignCenterAction>();

            if (
                items == null
                || items.Count == 0
                || items.Any(i => i.Kind != items[0].Kind || i.ContentKind != items[0].ContentKind)
            )
            {
                return actions;
            }

            DesignCenterItemViewModel first = items[0];
            string[] names = items.Select(i => i.Name).ToArray();
            bool document = _model.Document != null;

            switch (first.Kind)
            {
                case DesignCenterItemKind.Drawing:
                    actions.Add(new DesignCenterAction("Open in Application Window", () => _model.Open(first.Path)));

                    if (document)
                    {
                        actions.Add(
                            new DesignCenterAction("Insert as Block…", () =>
                                _model.InsertFromDesignCenter(first.Path, null, null, false)
                            )
                        );
                        actions.Add(
                            new DesignCenterAction("Attach as Xref…", () => _model.AttachFromDesignCenter(first.Path))
                        );
                    }

                    actions.Add(new DesignCenterAction("Load into Content Area", () => NavigateTo(first.Path, null)));
                    break;
                case DesignCenterItemKind.Item
                    when document && first.Path != null && first.ContentKind == CadDesignCenterKind.Blocks:
                    actions.Add(
                        new DesignCenterAction("Insert Block", () =>
                            _model.InsertFromDesignCenter(first.Path, first.Name, null, false)
                        )
                    );
                    actions.Add(
                        new DesignCenterAction("Insert Block…", () =>
                            _model.InsertFromDesignCenter(first.Path, first.Name, null, true)
                        )
                    );
                    actions.Add(
                        new DesignCenterAction("Add Block Definition", () =>
                            Add(first.Drawing, CadDesignCenterKind.Blocks, names)
                        )
                    );
                    break;
                case DesignCenterItemKind.Item when document && first.ContentKind == CadDesignCenterKind.Xrefs:
                    actions.Add(
                        new DesignCenterAction("Attach Xref…", () =>
                            _model.AttachFromDesignCenter(
                                first.Drawing.Database.BlockTable.Has(first.Name) ? ResolveXref(first) : null
                            )
                        )
                    );
                    break;
                case DesignCenterItemKind.Item when document && first.ContentKind != null:
                    CadDesignCenterKind kind = first.ContentKind.Value;
                    actions.Add(new DesignCenterAction(AddText(kind), () => Add(first.Drawing, kind, names)));
                    break;
            }

            return actions;
        }

        /// <summary>
        /// Copies items into the drawing shown: layers, linetypes, styles, block definitions and layouts, those it has by the name
        /// kept as they are; one undo step. The result is said in <see cref="Message"/>.
        /// </summary>
        /// <param name="from">The drawing the items are in.</param>
        /// <param name="kind">The kind of content.</param>
        /// <param name="names">The names.</param>
        /// <returns>What was added, kept and what failed; null where no drawing is shown.</returns>
        public CadDesignCenterResult Add(
            CadDesignCenterDrawing from,
            CadDesignCenterKind kind,
            IEnumerable<string> names
        )
        {
            CadDocument document = _model.Document;

            if (document == null || from == null)
            {
                return null;
            }

            CadDesignCenterResult result = CadDesignCenter.Add(document, from, kind, names);
            Message = Say(result, kind);
            _model.SayFromDesignCenter(Message);
            return result;
        }

        /// <summary>
        /// Adds what a drag carried into the drawing, as <see cref="MainViewModel.DropFromDesignCenter"/> does: the same copies
        /// as the right-click menu.
        /// </summary>
        /// <param name="text">What <see cref="DesignCenterDrag.Write"/> wrote.</param>
        /// <returns>Whether the text was one a drag wrote.</returns>
        public bool Drop(string text) => _model.DropFromDesignCenter(text, null);

        /// <summary>Gets what a drag of the rows selected carries; null for rows that cannot be dragged (a folder, a kind, a drawing not saved yet).</summary>
        /// <param name="items">The rows.</param>
        /// <returns>The text for the drag data.</returns>
        public string DragText(IReadOnlyList<DesignCenterItemViewModel> items)
        {
            if (items == null || items.Count == 0 || items[0].Path == null)
            {
                return null;
            }

            switch (items[0].Kind)
            {
                case DesignCenterItemKind.Drawing:
                    return DesignCenterDrag.Write(items[0].Path, null, new string[0]);
                case DesignCenterItemKind.Item when items[0].ContentKind != null:
                    return DesignCenterDrag.Write(
                        items[0].Path,
                        items[0].ContentKind,
                        items.Where(i => i.ContentKind == items[0].ContentKind).Select(i => i.Name)
                    );
                default:
                    return null;
            }
        }

        private static string Say(CadDesignCenterResult result, CadDesignCenterKind kind)
        {
            string noun =
                kind == CadDesignCenterKind.Layers ? "layer"
                : kind == CadDesignCenterKind.Blocks ? "block"
                : "item";
            var said = new List<string>();

            if (result.Added.Count > 0)
            {
                said.Add(
                    result.Added.Count.ToString(CultureInfo.InvariantCulture)
                        + " "
                        + noun
                        + (result.Added.Count == 1 ? string.Empty : "s")
                        + " added"
                );
            }

            if (result.Kept.Count > 0)
            {
                said.Add(result.Kept.Count.ToString(CultureInfo.InvariantCulture) + " kept as the drawing has them");
            }

            said.AddRange(result.Failed);
            return said.Count == 0 ? "Nothing was added." : string.Join(", ", said) + ".";
        }

        private static string AddText(CadDesignCenterKind kind)
        {
            switch (kind)
            {
                case CadDesignCenterKind.Dimstyles:
                    return "Add Dimstyle(s)";
                case CadDesignCenterKind.Layers:
                    return "Add Layer(s)";
                case CadDesignCenterKind.Layouts:
                    return "Add Layout(s)";
                case CadDesignCenterKind.Linetypes:
                    return "Load Linetype(s)";
                case CadDesignCenterKind.Multileaderstyles:
                    return "Add Multileaderstyle(s)";
                case CadDesignCenterKind.Tablestyles:
                    return "Add Tablestyle(s)";
                default:
                    return "Add Textstyle(s)";
            }
        }

        /// <summary>The kinds of content in the order of AutoCAD's tree.</summary>
        internal static readonly CadDesignCenterKind[] Kinds = (CadDesignCenterKind[])
            Enum.GetValues(typeof(CadDesignCenterKind));

        private static string KindName(CadDesignCenterKind kind) => kind.ToString();

        /// <summary>The file an external reference of a drawing is, as the drawing says it; null where it has no path.</summary>
        private static string ResolveXref(DesignCenterItemViewModel item)
        {
            string path = item.Drawing.Database.BlockTable[item.Name].PathName;

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string folder = item.Path == null ? null : Path.GetDirectoryName(item.Path);
            string full = Path.IsPathRooted(path) || folder == null ? path : Path.Combine(folder, path);
            return File.Exists(full) ? Path.GetFullPath(full) : null;
        }

        private static IEnumerable<string> DefaultRoots()
        {
            var roots = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };

            try
            {
                roots.AddRange(DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.RootDirectory.FullName));
            }
            catch (IOException exception)
            {
                GeometryHelperLog.Debug("DesignCenterViewModel: the drives were not listed: " + exception.Message);
            }

            return roots;
        }

        private static bool IsDrawingFile(string path) =>
            DrawingExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        private void Refill()
        {
            Roots.Clear();

            switch (_tab)
            {
                case "Open Drawings":
                    foreach (CadDocument document in _model.OpenDocuments)
                    {
                        string title = document.Title;
                        var node = new DesignCenterNode(
                            this,
                            null,
                            DesignCenterNodeKind.Drawing,
                            title,
                            document.HasFile ? document.FileName : null
                        )
                        {
                            Drawing = CadDesignCenterDrawing.Of(document, title),
                        };
                        Roots.Add(node);
                    }

                    break;
                case "History":
                    foreach (string path in _model.DesignCenterHistory.Where(File.Exists))
                    {
                        Roots.Add(
                            new DesignCenterNode(this, null, DesignCenterNodeKind.Drawing, Path.GetFileName(path), path)
                        );
                    }

                    break;
                default:
                    foreach (string root in _roots)
                    {
                        string name = Path.GetFileName(
                            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        );
                        Roots.Add(
                            new DesignCenterNode(
                                this,
                                null,
                                DesignCenterNodeKind.Folder,
                                name.Length == 0 ? root : name,
                                Path.GetFullPath(root)
                            )
                        );
                    }

                    break;
            }
        }

        private IEnumerable<DesignCenterNode> ReadFolder(DesignCenterNode node)
        {
            foreach (string folder in Entries(node.Path, true))
            {
                yield return new DesignCenterNode(
                    this,
                    node,
                    DesignCenterNodeKind.Folder,
                    Path.GetFileName(folder),
                    folder
                );
            }

            foreach (string file in Entries(node.Path, false))
            {
                yield return new DesignCenterNode(
                    this,
                    node,
                    DesignCenterNodeKind.Drawing,
                    Path.GetFileName(file),
                    file
                );
            }
        }

        private IEnumerable<DesignCenterNode> ReadDrawing(DesignCenterNode node)
        {
            OpenDrawingNode(node);

            if (node.Drawing == null)
            {
                yield break;
            }

            foreach (CadDesignCenterKind kind in Kinds)
            {
                yield return new DesignCenterNode(this, node, DesignCenterNodeKind.Content, KindName(kind), node.Path)
                {
                    ContentKind = kind,
                    Drawing = node.Drawing,
                };
            }
        }

        /// <summary>Reads the drawing of a Drawing row once; a file that cannot be read is said and the row left without content.</summary>
        private void OpenDrawingNode(DesignCenterNode node)
        {
            if (node.Drawing != null || node.Path == null)
            {
                return;
            }

            try
            {
                node.Drawing = CadDesignCenterDrawing.Open(node.Path);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is FormatException
                    || exception is InvalidDataException
                )
            {
                GeometryHelperLog.Warn("DesignCenterViewModel: the drawing " + node.Path + " was not read.", exception);
                Message = "Cannot read " + node.Text + ": " + exception.Message;
            }
        }

        /// <summary>The folders (or drawing files) in a folder, in ordinal order of the names, hidden and system ones left out; none where the folder cannot be read.</summary>
        private static IEnumerable<string> Entries(string folder, bool folders)
        {
            try
            {
                IEnumerable<string> found = folders
                    ? Directory
                        .GetDirectories(folder)
                        .Where(d =>
                            (new DirectoryInfo(d).Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0
                        )
                    : Directory.GetFiles(folder).Where(IsDrawingFile);
                return found
                    .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private IEnumerable<DesignCenterItemViewModel> FolderItems(DesignCenterNode folder)
        {
            folder.EnsureLoaded();

            foreach (DesignCenterNode child in folder.Children.Where(c => c.Kind != DesignCenterNodeKind.Placeholder))
            {
                if (child.Kind == DesignCenterNodeKind.Folder)
                {
                    yield return new DesignCenterItemViewModel(
                        this,
                        DesignCenterItemKind.Folder,
                        child.Text,
                        child.Path,
                        child.Path
                    )
                    {
                        Node = child,
                    };
                    continue;
                }

                var info = new FileInfo(child.Path);
                yield return new DesignCenterItemViewModel(
                    this,
                    DesignCenterItemKind.Drawing,
                    child.Text,
                    (info.Length / 1024).ToString(CultureInfo.InvariantCulture)
                        + " KB, "
                        + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    child.Path
                )
                {
                    Node = child,
                };
            }
        }

        /// <summary>Opens the nodes of the Folders tab down to a path: the longest root it is under, then each folder on the way; null where it is not found.</summary>
        private DesignCenterNode Reveal(string path)
        {
            string full = Path.GetFullPath(path);
            DesignCenterNode node = Roots
                .Where(r => Under(r.Path, full))
                .OrderByDescending(r => r.Path.Length)
                .FirstOrDefault();

            while (node != null && !string.Equals(node.Path, full, StringComparison.OrdinalIgnoreCase))
            {
                node.IsExpanded = true;
                node = node.Children.FirstOrDefault(c =>
                    c.Kind != DesignCenterNodeKind.Placeholder && Under(c.Path, full)
                );
            }

            return node;
        }

        private static bool Under(string folder, string path) =>
            folder != null
            && (
                string.Equals(folder.TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(
                    folder.TrimEnd('\\', '/') + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        private string UpPath()
        {
            DesignCenterNode node = _selected;

            if (node == null)
            {
                return null;
            }

            // A kind of content goes up to its drawing, a drawing or a folder to the folder it is in.
            if (node.Kind == DesignCenterNodeKind.Content)
            {
                return node.Path;
            }

            string up = node.Path == null ? null : Path.GetDirectoryName(node.Path.TrimEnd('\\', '/'));
            return string.IsNullOrEmpty(up) ? null : up;
        }

        private void Step(List<string> from, List<string> to)
        {
            if (from.Count == 0)
            {
                return;
            }

            string place = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);

            if (!string.IsNullOrEmpty(_location))
            {
                to.Add(_location);
            }

            _walking = true;

            try
            {
                int slash = place.LastIndexOf('\\');
                string tail = slash >= 0 ? place.Substring(slash + 1) : string.Empty;

                if (
                    Enum.TryParse(tail, out CadDesignCenterKind kind)
                    && Enum.IsDefined(typeof(CadDesignCenterKind), kind)
                    && File.Exists(place.Substring(0, slash))
                )
                {
                    NavigateTo(place.Substring(0, slash), kind);
                }
                else
                {
                    NavigateTo(place, null);
                }
            }
            finally
            {
                _walking = false;
            }

            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void Note(string path)
        {
            if (path != null)
            {
                _model.NoteDesignCenterVisit(path);
                Raise(nameof(History));
            }
        }
    }
}
