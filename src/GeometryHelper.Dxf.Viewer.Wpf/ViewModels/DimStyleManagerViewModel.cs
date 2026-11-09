using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The name asked by New and Rename of the style dialogs, as AutoCAD's Create New Dimension Style asks it: the name, and
    /// for a dimension style the style it starts with and what it is used for (All dimensions, as only that is built).
    /// </summary>
    public sealed class StyleNameViewModel : ObservableObject
    {
        private readonly Func<string, bool> _taken;
        private string _name;
        private string _startWith;
        private string _notice = string.Empty;

        /// <summary>Makes the view model.</summary>
        /// <param name="title">The title, as "Create New Dimension Style".</param>
        /// <param name="name">The name offered.</param>
        /// <param name="taken">Whether a name is taken already; null for none.</param>
        /// <param name="startWith">The styles New may start with; null where the dialog asks only the name.</param>
        /// <param name="start">The style offered to start with.</param>
        public StyleNameViewModel(string title, string name, Func<string, bool> taken, IReadOnlyList<string> startWith = null, string start = null)
        {
            Title = title ?? string.Empty;
            _name = name ?? string.Empty;
            _taken = taken ?? (n => false);
            StartWithOptions = startWith;
            _startWith = start ?? startWith?.FirstOrDefault();
            OkCommand = new DelegateCommand(_ => Ok());
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
        }

        /// <summary>Raised when the dialog is to close: true after OK (Continue), false after Cancel.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the title.</summary>
        public string Title { get; }

        /// <summary>Gets or sets the name typed.</summary>
        public string Name
        {
            get => _name;
            set => Set(ref _name, value ?? string.Empty);
        }

        /// <summary>Gets the styles a new one may start with; null where none is asked.</summary>
        public IReadOnlyList<string> StartWithOptions { get; }

        /// <summary>Gets whether Start With and Use for are shown.</summary>
        public bool AsksStartWith => StartWithOptions != null;

        /// <summary>Gets or sets the style a new one starts with.</summary>
        public string StartWith
        {
            get => _startWith;
            set => Set(ref _startWith, value);
        }

        /// <summary>Gets what a new dimension style is used for: All dimensions, the one choice built.</summary>
        public string UseFor => "All dimensions";

        /// <summary>Gets why OK was refused; empty where it was not.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value ?? string.Empty);
        }

        /// <summary>Gets the command of OK (Continue).</summary>
        public ICommand OkCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>Takes the name: refused where empty or taken, the reason in <see cref="Notice"/>.</summary>
        /// <returns>Whether it was taken.</returns>
        public bool Ok()
        {
            string name = _name.Trim();

            if (name.Length == 0)
            {
                Notice = "Enter a name.";
                return false;
            }

            if (_taken(name))
            {
                Notice = "A style named \"" + name + "\" already exists.";
                return false;
            }

            Name = name;
            CloseRequested?.Invoke(this, true);
            return true;
        }
    }

    /// <summary>
    /// AutoCAD's Compare Dimension Styles: two styles side by side, each variable in which they differ with both values; the
    /// same style twice lists all its variables.
    /// </summary>
    public sealed class DimStyleCompareViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private string _first;
        private string _second;

        internal DimStyleCompareViewModel(CadDocument document, string first, string second)
        {
            _document = document;
            Styles = CadDimStyleEditor.Styles(document.Database).Select(s => s.Name).ToList();
            _first = first ?? Styles.FirstOrDefault();
            _second = second ?? _first;
            CloseCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, true));
        }

        /// <summary>Raised when the dialog is to close.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the styles of the drawing.</summary>
        public IReadOnlyList<string> Styles { get; }

        /// <summary>Gets or sets the style compared (Compare).</summary>
        public string First
        {
            get => _first;
            set
            {
                if (Set(ref _first, value))
                {
                    Raise(nameof(Rows));
                    Raise(nameof(Summary));
                }
            }
        }

        /// <summary>Gets or sets the style it is compared with (With).</summary>
        public string Second
        {
            get => _second;
            set
            {
                if (Set(ref _second, value))
                {
                    Raise(nameof(Rows));
                    Raise(nameof(Summary));
                }
            }
        }

        /// <summary>Gets the variables listed: each as Variable, then the values of the two styles; all of one style compared with itself.</summary>
        public IReadOnlyList<string[]> Rows
        {
            get
            {
                if (_first == null || _second == null || !_document.Database.DimStyleTable.Has(_first) || !_document.Database.DimStyleTable.Has(_second))
                {
                    return Array.Empty<string[]>();
                }

                SortedDictionary<string, object> a = CadDimStyleEditor.Read(_document.Database.DimStyleTable[_first]);

                if (string.Equals(_first, _second, StringComparison.OrdinalIgnoreCase))
                {
                    return a.Select(v => new[] { v.Key.ToUpperInvariant(), CadDimStyleEditor.Text(v.Value), string.Empty }).ToList();
                }

                SortedDictionary<string, object> b = CadDimStyleEditor.Read(_document.Database.DimStyleTable[_second]);
                return CadDimStyleEditor.Compare(a, b).Select(d => new[] { d.Key.ToUpperInvariant(), d.Value[0], d.Value[1] }).ToList();
            }
        }

        /// <summary>Gets the line over the list, as AutoCAD's: "AutoCAD found 3 differences:", or "All properties of ISO-25:".</summary>
        public string Summary
            => string.Equals(_first, _second, StringComparison.OrdinalIgnoreCase)
                ? "All properties of " + _first + ":"
                : Rows.Count == 0 ? "No differences found." : "Found " + Rows.Count.ToString(CultureInfo.InvariantCulture) + " differences:";

        /// <summary>Gets the command of Close.</summary>
        public ICommand CloseCommand { get; }
    }

    /// <summary>
    /// AutoCAD's Dimension Style Manager (DIMSTYLE, D): the dimension styles of the drawing (all, or those in use), a preview
    /// of the one selected drawn by the DIM commands' builders with its description, Set current, New, Modify, Override, Compare,
    /// and Rename and Delete of its list's menu. A style a model drawing makes (GH_MODEL_...) is marked MODEL and noticed.
    /// </summary>
    public sealed class DimStyleManagerViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private string _filter = TextStyleViewModel.AllStyles;
        private StyleRowViewModel _selected;
        private string _notice = string.Empty;
        private ImageSource _preview;

        /// <summary>Makes the Manager's view model over a document, the current style selected.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public DimStyleManagerViewModel(CadDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            SetCurrentCommand = new DelegateCommand(_ => SetCurrent());
            NewCommand = new DelegateCommand(_ => Open(CreateNew()));
            ModifyCommand = new DelegateCommand(_ => Open(CreateModify()));
            OverrideCommand = new DelegateCommand(_ => Open(CreateOverride()));
            CompareCommand = new DelegateCommand(_ => ShowCompare?.Invoke(CreateCompare()));
            DeleteCommand = new DelegateCommand(_ => Delete());
            RenameCommand = new DelegateCommand(_ => Rename(AskName?.Invoke(new StyleNameViewModel("Rename Dimension Style", _selected?.Name, n => _document.Database.DimStyleTable.Has(n) && !string.Equals(n, _selected?.Name, StringComparison.OrdinalIgnoreCase)))));
            CloseCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, true));
            Reload(CadDimStyleEditor.Current(document.Database));
        }

        /// <summary>Raised when the Manager is to close.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets or sets what asks a name (New's Create New Dimension Style, Rename): the name, or null for Cancel. Set by the window.</summary>
        public Func<StyleNameViewModel, string> AskName { get; set; }

        /// <summary>Gets or sets what shows the New, Modify or Override dialog: true where OK wrote it. Set by the window.</summary>
        public Func<DimStyleEditViewModel, bool> ShowEditor { get; set; }

        /// <summary>Gets or sets what shows Compare. Set by the window.</summary>
        public Action<DimStyleCompareViewModel> ShowCompare { get; set; }

        /// <summary>Gets the filters of the list: All styles, Styles in use.</summary>
        public IReadOnlyList<string> Filters { get; } = new[] { TextStyleViewModel.AllStyles, TextStyleViewModel.StylesInUse };

        /// <summary>Gets or sets the filter of the list.</summary>
        public string Filter
        {
            get => _filter;
            set
            {
                if (Filters.Contains(value) && Set(ref _filter, value))
                {
                    Reload(_selected?.Name);
                }
            }
        }

        /// <summary>Gets the styles listed: those of the table but the ones AutoCAD keeps for one kind of dimension (ISO-25$0).</summary>
        public ObservableCollection<StyleRowViewModel> Styles { get; } = new ObservableCollection<StyleRowViewModel>();

        /// <summary>Gets the line over the list: the current dimension style.</summary>
        public string CurrentStyleText => "Current: " + CadDimStyleEditor.Current(_document.Database);

        /// <summary>Gets the title: "Dimension style manager · plan-level2.dxf".</summary>
        public string Title => "Dimension style manager · " + _document.Title;

        /// <summary>Gets the title over the preview: "Preview of ISO-25".</summary>
        public string PreviewTitle => "Preview of " + _selected?.Name;

        /// <summary>Gets the line at the foot: "5 styles · double-click a style to modify it".</summary>
        public string Footer => Styles.Count.ToString(CultureInfo.InvariantCulture) + (Styles.Count == 1 ? " style" : " styles") + " · double-click a style to modify it";

        /// <summary>Gets whether the notice says why an edit was refused, drawn as a warning.</summary>
        public bool IsNoticeWarning => _notice.Length > 0;

        /// <summary>Gets whether there is a notice to show.</summary>
        public bool HasNotice => Notice.Length > 0;

        /// <summary>Gets or sets the style selected, whose preview is shown.</summary>
        public StyleRowViewModel SelectedStyle
        {
            get => _selected;
            set
            {
                if (value != null && Set(ref _selected, value))
                {
                    _preview = null;
                    Raise(nameof(Preview));
                    Raise(nameof(Description));
                    Raise(nameof(Notice));
                    Raise(nameof(HasNotice));
                    Raise(nameof(PreviewTitle));
                }
            }
        }

        /// <summary>Gets the description under the preview: the style's arrows, text and scale, and how many dimensions are drawn in it.</summary>
        /// <remarks>"ISO-25 · arrows closed filled 2.5 · text 2.5 above the line · overall scale 1 · Used by 38 dimensions."</remarks>
        public string Description
        {
            get
            {
                if (_selected == null || !_document.Database.DimStyleTable.Has(_selected.Name))
                {
                    return string.Empty;
                }

                SortedDictionary<string, object> v = CadDimStyleEditor.Read(_document.Database.DimStyleTable[_selected.Name]);
                string[] vertical = { "centred on the line", "above the line", "outside the line", "placed as JIS", "below the line" };
                int tad = v.TryGetValue("Dimtad", out object t) && t is int i && i >= 0 && i < vertical.Length ? i : 0;
                object arrow = v.TryGetValue("Dimsah", out object sah) && sah is bool split && split ? Value(v, "Dimblk1") : Value(v, "Dimblk");
                return _selected.Name + " · arrows " + CadDimStyleEditor.Text(arrow).ToLowerInvariant() + " " + Number(v, "Dimasz") + " · text " + Number(v, "Dimtxt") + " " + vertical[tad]
                    + " · overall scale " + Number(v, "Dimscale") + " · Used by " + _selected.Uses.ToString(CultureInfo.InvariantCulture) + (_selected.Uses == 1 ? " dimension." : " dimensions.");
            }
        }

        /// <summary>Gets the preview of the style selected: a linear, a radius and an angular dimension, as the DIM commands draw them.</summary>
        public ImageSource Preview
            => _preview ?? (_selected == null ? null : _preview = CadStylePreview.Dimensions(_document.Database, CadDimStyleEditor.Read(_document.Database.DimStyleTable[_selected.Name]), _document.Fonts, 360, 230));

        /// <summary>
        /// Gets the notice under the description: why an edit was refused; else, where the drawing has styles a model drawing
        /// makes (GH_MODEL_...), what changing one does; else empty, the notice hidden.
        /// </summary>
        public string Notice => _notice.Length > 0 ? _notice : Styles.Any(s => s.IsModel) ? ManagerNotices.ModelStyle : string.Empty;

        /// <summary>Gets the command of Set Current.</summary>
        public ICommand SetCurrentCommand { get; }

        /// <summary>Gets the command of New...: a name and the style it starts with asked, then the New dialog.</summary>
        public ICommand NewCommand { get; }

        /// <summary>Gets the command of Modify...</summary>
        public ICommand ModifyCommand { get; }

        /// <summary>Gets the command of Override...: the current style's dialog, OK writing overrides into the dimensions selected.</summary>
        public ICommand OverrideCommand { get; }

        /// <summary>Gets the command of Compare...</summary>
        public ICommand CompareCommand { get; }

        /// <summary>Gets the command of Delete.</summary>
        public ICommand DeleteCommand { get; }

        /// <summary>Gets the command of Rename.</summary>
        public ICommand RenameCommand { get; }

        /// <summary>Gets the command of Close.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Makes the style selected current, its variables given to the header.</summary>
        /// <returns>Whether it was made current.</returns>
        public bool SetCurrent() => _selected != null && Report(CadDimStyleEditor.SetCurrent(_document, _selected.Name), _selected.Name);

        /// <summary>
        /// Makes the New dialog as Continue of Create New Dimension Style opens it: the name asked (Copy of the style selected
        /// offered) and the style it starts with; null where the name is not given.
        /// </summary>
        /// <returns>The dialog's view model, or null.</returns>
        public DimStyleEditViewModel CreateNew()
        {
            string start = _selected?.Name ?? CadDimStyleEditor.Current(_document.Database);
            var ask = new StyleNameViewModel("Create New Dimension Style", "Copy of " + start, n => _document.Database.DimStyleTable.Has(n), Styles.Select(s => s.Name).ToList(), start);
            string name = AskName?.Invoke(ask);
            return name == null ? null : new DimStyleEditViewModel(_document, DimStyleEditMode.New, name, ask.StartWith);
        }

        /// <summary>Makes the Modify dialog of the style selected.</summary>
        /// <returns>The dialog's view model; null where none is selected.</returns>
        public DimStyleEditViewModel CreateModify() => _selected == null ? null : new DimStyleEditViewModel(_document, DimStyleEditMode.Modify, _selected.Name);

        /// <summary>Makes the Override dialog of the current style, OK writing into the dimensions selected in the drawing.</summary>
        /// <returns>The dialog's view model; null where the drawing has no dimension style.</returns>
        public DimStyleEditViewModel CreateOverride()
        {
            string current = CadDimStyleEditor.Current(_document.Database);
            return current == null ? null : new DimStyleEditViewModel(_document, DimStyleEditMode.Override, current, null, _document.Selection.Handles.ToList());
        }

        /// <summary>Makes Compare, the style selected compared with itself until another is chosen, as AutoCAD opens it.</summary>
        /// <returns>The view model.</returns>
        public DimStyleCompareViewModel CreateCompare() => new DimStyleCompareViewModel(_document, _selected?.Name, _selected?.Name);

        /// <summary>Deletes the style selected; refused for Standard, the current style and a style in use, the reason in <see cref="Notice"/>.</summary>
        /// <returns>Whether it was deleted.</returns>
        public bool Delete() => _selected != null && Report(CadDimStyleEditor.Delete(_document, _selected.Name), CadDimStyleEditor.Current(_document.Database));

        /// <summary>Renames the style selected.</summary>
        /// <param name="name">The new name; null does nothing.</param>
        /// <returns>Whether it was renamed.</returns>
        public bool Rename(string name) => _selected != null && name != null && Report(CadDimStyleEditor.Rename(_document, _selected.Name, name), name.Trim());

        /// <summary>Shows a dialog of a style and reads the list again where it wrote, on the style it wrote.</summary>
        private void Open(DimStyleEditViewModel editor)
        {
            if (editor != null && ShowEditor != null && ShowEditor(editor))
            {
                Reload(editor.Mode == DimStyleEditMode.Override ? _selected?.Name : editor.StyleName);
            }
        }

        private bool Report(CadEditResult result, string show)
        {
            _notice = result.Succeeded ? string.Empty : result.Message;
            Raise(nameof(Notice));
            Raise(nameof(HasNotice));
            Raise(nameof(IsNoticeWarning));

            if (result.Succeeded)
            {
                Reload(show);
            }

            return result.Succeeded;
        }

        /// <summary>Reads the list again, as the filter has it, the style named selected where listed, else the first.</summary>
        internal void Reload(string show)
        {
            Styles.Clear();
            string current = CadDimStyleEditor.Current(_document.Database);

            foreach (DxfDimStyleTableRecord style in CadDimStyleEditor.Styles(_document.Database))
            {
                int uses = CadDimStyleEditor.CountUses(_document, style.Name);

                if (_filter == TextStyleViewModel.AllStyles || uses > 0 || string.Equals(style.Name, current, StringComparison.OrdinalIgnoreCase))
                {
                    Styles.Add(new StyleRowViewModel(style.Name, string.Equals(style.Name, current, StringComparison.OrdinalIgnoreCase), uses));
                }
            }

            _selected = null;
            SelectedStyle = Styles.FirstOrDefault(s => string.Equals(s.Name, show, StringComparison.OrdinalIgnoreCase)) ?? Styles.FirstOrDefault();
            Raise(nameof(SelectedStyle));
            Raise(nameof(CurrentStyleText));
            Raise(nameof(Footer));
            Raise(nameof(Notice));
            Raise(nameof(HasNotice));
        }

        private static object Value(IDictionary<string, object> values, string name) => values.TryGetValue(name, out object value) ? value : null;

        private static string Number(IDictionary<string, object> values, string name)
            => Value(values, name) is double number ? number.ToString("0.####", CultureInfo.InvariantCulture) : string.Empty;
    }
}
