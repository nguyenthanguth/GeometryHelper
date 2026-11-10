using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A row of the Purge dialog's tree: All Items, a kind with its count, or a named object; checked to be purged, opened to
    /// show what is under it, greyed where there is nothing of it.
    /// </summary>
    public sealed class PurgeNodeViewModel : ObservableObject
    {
        private readonly PurgeViewModel _owner;
        private bool _isChecked = true;
        private bool _isExpanded;

        internal PurgeNodeViewModel(PurgeViewModel owner, string name, int level, CadPurgeItem item, IReadOnlyList<PurgeNodeViewModel> children, bool counted)
        {
            _owner = owner;
            Name = name;
            Level = level;
            Item = item;
            Children = children ?? Array.Empty<PurgeNodeViewModel>();
            Count = counted ? "(" + Leaves.Count().ToString(CultureInfo.InvariantCulture) + ")" : string.Empty;
            _isExpanded = level == 0 || (level == 1 && owner.IsPurgeable && Children.Count > 0);
            _isChecked = IsEnabled;
        }

        /// <summary>Gets the name shown: All Items, Layers, OLD_GRID.</summary>
        public string Name { get; }

        /// <summary>Gets the count shown after a kind, "(2)"; empty for an object.</summary>
        public string Count { get; }

        /// <summary>Gets how deep the row is: 0 All Items, 1 a kind, 2 an object.</summary>
        public int Level { get; }

        /// <summary>Gets the room to the left of the row, 10 pixels and 18 more a level.</summary>
        public System.Windows.Thickness Indent => new System.Windows.Thickness(10.0 + (Level * 18.0), 0.0, 0.0, 0.0);

        /// <summary>Gets the object of a row of one; null for All Items and the kinds.</summary>
        public CadPurgeItem Item { get; }

        /// <summary>Gets the rows under it.</summary>
        public IReadOnlyList<PurgeNodeViewModel> Children { get; }

        /// <summary>Gets the twisty: ▾ opened, ▸ closed, nothing where there is nothing under it.</summary>
        public string Twisty => Children.Count == 0 ? string.Empty : _isExpanded ? "▾" : "▸";

        /// <summary>Gets whether there is anything of it to purge or to show; a kind of none is greyed.</summary>
        public bool IsEnabled => Item != null || Leaves.Any();

        /// <summary>Gets whether the row has a check box: in Purgeable Items, as AutoCAD's tree.</summary>
        public bool HasCheckBox => _owner.IsPurgeable;

        /// <summary>Gets or sets whether it is checked to be purged; checking a kind checks every object of it.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (Set(ref _isChecked, value))
                {
                    foreach (PurgeNodeViewModel child in Children)
                    {
                        child.IsChecked = value;
                    }
                }
            }
        }

        /// <summary>Gets or sets whether the rows under it are shown.</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (Children.Count > 0 && Set(ref _isExpanded, value))
                {
                    Raise(nameof(Twisty));
                    _owner.ShowNodes();
                }
            }
        }

        /// <summary>The objects under the row, its own where it is one.</summary>
        internal IEnumerable<PurgeNodeViewModel> Leaves => Item != null ? new[] { this } : Children.SelectMany(c => c.Leaves);
    }

    /// <summary>
    /// AutoCAD's Purge dialog, which PURGE shows, over the request its command hands the window: the modes Purgeable Items and
    /// Find Non-Purgeable Items; the tree of named objects nothing uses, by kind, with its options and the preview of a block
    /// or style; or the tree of those in use, with the possible reasons and Select Objects. Purge Checked Items and Purge All
    /// purge with the dialog open; Close ends it. A drawing whose layers OLD_GRID and TMP are unused lists Layers (2).
    /// </summary>
    public sealed class PurgeViewModel : ObservableObject
    {
        private readonly ObservableCollection<PurgeNodeViewModel> _shown = new ObservableCollection<PurgeNodeViewModel>();
        private BlockPictures _pictures;
        private string _mode = "Purgeable Items";
        private PurgeNodeViewModel _root;
        private PurgeNodeViewModel _selected;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of PURGE.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public PurgeViewModel(CadPurgeRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Build();
        }

        /// <summary>Gets the request.</summary>
        public CadPurgeRequest Request { get; }

        /// <summary>Gets the title, "Purge - plan-level2.dxf".</summary>
        public string Title => "Purge - " + Request.Document.Title;

        /// <summary>Gets the modes.</summary>
        public IReadOnlyList<string> Modes { get; } = new[] { "Purgeable Items", "Find Non-Purgeable Items" };

        /// <summary>Gets or sets the mode shown.</summary>
        public string Mode
        {
            get => _mode;
            set
            {
                if (Set(ref _mode, Modes.Contains(value) ? value : Modes[0]))
                {
                    Raise(nameof(IsPurgeable));
                    Raise(nameof(IsNonPurgeable));
                    Raise(nameof(TreeTitle));
                    Raise(nameof(FootNote));
                    Build();
                }
            }
        }

        /// <summary>Gets whether Purgeable Items is shown.</summary>
        public bool IsPurgeable => _mode == Modes[0];

        /// <summary>Gets whether Find Non-Purgeable Items is shown.</summary>
        public bool IsNonPurgeable => !IsPurgeable;

        /// <summary>Gets the title of the tree: Named items not used, or Named items in use.</summary>
        public string TreeTitle => IsPurgeable ? "Named items not used" : "Named items in use";

        /// <summary>Gets the rows of the tree shown, those under a row closed left out.</summary>
        public ObservableCollection<PurgeNodeViewModel> Nodes => _shown;

        /// <summary>Gets or sets the row chosen, whose preview or reasons the right side shows.</summary>
        public PurgeNodeViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(Preview));
                    Raise(nameof(PreviewNote));
                    Raise(nameof(ReasonsTitle));
                    Raise(nameof(Reasons));
                    Raise(nameof(CanSelectObjects));
                }
            }
        }

        /// <summary>Gets or sets whether each object is asked about before it is purged.</summary>
        public bool ConfirmEach { get => Request.ConfirmEach; set { Request.ConfirmEach = value; Raise(nameof(ConfirmEach)); } }

        /// <summary>Gets or sets whether what a purge leaves unused is purged too.</summary>
        public bool PurgeNested { get => Request.PurgeNested; set { Request.PurgeNested = value; Raise(nameof(PurgeNested)); } }

        /// <summary>Gets or sets whether the geometry of no length is purged too.</summary>
        public bool PurgeZeroLength { get => Request.PurgeZeroLength; set { Request.PurgeZeroLength = value; Raise(nameof(PurgeZeroLength)); } }

        /// <summary>Gets or sets whether the texts of no content are purged too.</summary>
        public bool PurgeEmptyText { get => Request.PurgeEmptyText; set { Request.PurgeEmptyText = value; Raise(nameof(PurgeEmptyText)); } }

        /// <summary>Gets "Purge zero-length geometry (2)".</summary>
        public string ZeroLengthText => "Purge zero-length geometry (" + Request.ZeroLength.ToString(CultureInfo.InvariantCulture) + ")";

        /// <summary>Gets "Purge empty text objects (1)".</summary>
        public string EmptyTextText => "Purge empty text objects (" + Request.EmptyText.ToString(CultureInfo.InvariantCulture) + ")";

        /// <summary>Gets the line beside Close: how many objects can be purged, or how to see why one cannot.</summary>
        public string FootNote => IsPurgeable
            ? Request.Purgeable.Sum(k => k.Items.Count).ToString(CultureInfo.InvariantCulture) + " named items · " + (Request.ZeroLength + Request.EmptyText).ToString(CultureInfo.InvariantCulture) + " unnamed objects can be purged"
            : "Pick an item to see why it cannot be purged";

        /// <summary>
        /// Gets the picture of the object chosen in Purgeable Items, white on black: a block, a text style or a dimension
        /// style; null for the other kinds, which show their name only.
        /// </summary>
        public ImageSource Preview
        {
            get
            {
                CadPurgeItem item = _selected?.Item;

                if (item == null || !IsPurgeable)
                {
                    return null;
                }

                DxfDatabase database = Request.Document.Database;

                try
                {
                    switch (item.Kind)
                    {
                        case "Blocks":
                            _pictures = _pictures ?? new BlockPictures(database, Request.Document.Fonts, 180, 180);
                            return _pictures.Draw(item.Name);
                        case "Text Styles":
                            return database.TextStyleTable.Has(item.Name) ? CadStylePreview.Text(CadTextStyleSettings.Of(database.TextStyleTable[item.Name]), Request.Document.Fonts, 180, 180) : null;
                        case "Dimension Styles":
                            return database.DimStyleTable.Has(item.Name) ? CadStylePreview.Dimensions(database, CadDimStyleEditor.Read(database.DimStyleTable[item.Name]), Request.Document.Fonts, 180, 180) : null;
                        default:
                            return null;
                    }
                }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is FormatException)
                {
                    GeometryHelperLog.Debug("PurgeViewModel: no preview of " + item.Name + ": " + exception.Message);
                    return null;
                }
            }
        }

        /// <summary>Gets the line under the preview: "Block DOOR_OLD · not used anywhere.", or what to pick.</summary>
        public string PreviewNote => _selected?.Item == null
            ? "Pick an item to see it. Blocks, dimension styles and text styles show a preview; the other kinds show their name only."
            : Singular(_selected.Item.Kind) + " " + _selected.Item.Name + " · not used anywhere.";

        /// <summary>Gets the heading of the reasons: "Layer WALLS"; empty where no object in use is chosen.</summary>
        public string ReasonsTitle => _selected?.Item == null || IsPurgeable ? string.Empty : Singular(_selected.Item.Kind) + " " + _selected.Item.Name;

        /// <summary>Gets the reasons the object chosen cannot be purged.</summary>
        public IReadOnlyList<string> Reasons => _selected?.Item == null || IsPurgeable ? Array.Empty<string>() : _selected.Item.Reasons;

        /// <summary>Gets whether Select Objects can run: an object in use is chosen, and objects of the space shown use it.</summary>
        public bool CanSelectObjects => IsNonPurgeable && _selected?.Item != null && _selected.Item.Objects.Count > 0;

        /// <summary>Purge Checked Items: the objects checked purged, as the options say; the tree read again.</summary>
        /// <returns>How many named objects were purged.</returns>
        public int PurgeChecked() => Purge(_root.Leaves.Where(l => l.IsChecked).Select(l => l.Item).ToList());

        /// <summary>Purge All: every object listed purged, as the options say; the tree read again.</summary>
        /// <returns>How many named objects were purged.</returns>
        public int PurgeAll() => Purge(Request.Purgeable.SelectMany(k => k.Items).ToList());

        /// <summary>Select Objects: the dialog closed, the objects of the space shown that use the object chosen selected.</summary>
        public void SelectObjects()
        {
            if (CanSelectObjects)
            {
                Request.Selected = _selected.Item;
                Request.Action = CadDialogAction.SelectObjects;
            }
        }

        /// <summary>The rows shown made again from the tree: each row, then those under it where it is opened.</summary>
        internal void ShowNodes()
        {
            PurgeNodeViewModel chosen = _selected;
            _shown.Clear();

            void Add(PurgeNodeViewModel node)
            {
                _shown.Add(node);

                if (node.IsExpanded)
                {
                    foreach (PurgeNodeViewModel child in node.Children)
                    {
                        Add(child);
                    }
                }
            }

            Add(_root);
            Selected = chosen != null && _shown.Contains(chosen) ? chosen : null;
        }

        private int Purge(IReadOnlyList<CadPurgeItem> items)
        {
            int purged = Request.Purge(items);
            _pictures = null;
            Build();
            Raise(nameof(ZeroLengthText));
            Raise(nameof(EmptyTextText));
            Raise(nameof(FootNote));
            return purged;
        }

        /// <summary>The tree of the mode shown made again from the request: the kinds of Purgeable Items each opened where it has objects.</summary>
        private void Build()
        {
            IReadOnlyList<CadPurgeCategory> categories = IsPurgeable ? Request.Purgeable : Request.InUse;
            var kinds = new List<PurgeNodeViewModel>();

            foreach (CadPurgeCategory category in categories)
            {
                var items = category.Items.Select(i => new PurgeNodeViewModel(this, i.Name, 2, i, null, false)).ToList();
                kinds.Add(new PurgeNodeViewModel(this, category.Name, 1, null, items, true));
            }

            _root = new PurgeNodeViewModel(this, "All Items", 0, null, kinds, IsPurgeable);
            _selected = null;
            ShowNodes();
            Selected = _root.Leaves.FirstOrDefault(l => _shown.Contains(l));
        }

        private static string Singular(string kind)
        {
            string name = kind.EndsWith("ies", StringComparison.Ordinal) ? kind.Substring(0, kind.Length - 3) + "y" : kind.TrimEnd('s');
            return name.Length > 0 ? name.Substring(0, 1) + name.Substring(1).ToLowerInvariant() : name;
        }
    }
}
