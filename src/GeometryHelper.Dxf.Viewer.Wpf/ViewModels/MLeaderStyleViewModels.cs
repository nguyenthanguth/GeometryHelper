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
    /// <summary>What a field of the Modify Multileader Style dialog is: a group's title, a number, a text, a list, or a box to tick alone.</summary>
    public enum MLeaderFieldKind
    {
        /// <summary>The title of a group of fields, as "Arrowhead".</summary>
        Header,

        /// <summary>A number, typed.</summary>
        Number,

        /// <summary>A text, typed.</summary>
        Text,

        /// <summary>One of a list.</summary>
        Choice,

        /// <summary>No value of its own: the box or the button before the label is the field.</summary>
        Flag,
    }

    /// <summary>One item of a list field of the Modify Multileader Style dialog: its words, the value it stands for, and whether it can be chosen.</summary>
    public sealed class MLeaderChoice
    {
        internal MLeaderChoice(string label, object value, bool isEnabled = true)
        {
            Label = label;
            Value = value;
            IsEnabled = isEnabled;
        }

        /// <summary>Gets the words, as "Closed filled".</summary>
        public string Label { get; }

        /// <summary>Gets the value it sets.</summary>
        public object Value { get; }

        /// <summary>Gets whether it can be chosen; false for an item shown greyed.</summary>
        public bool IsEnabled { get; }

        /// <inheritdoc/>
        public override string ToString() => Label;
    }

    /// <summary>
    /// A field of the Modify Multileader Style dialog, as the design lays it: a box to tick or a button of a pair before the
    /// label where it has one ("Maximum leader points", "Specify scale"), and its value, a number or a text typed or one of a
    /// list with the colour's swatch, greyed where the box before it is clear or the values make it mean nothing.
    /// </summary>
    public sealed class MLeaderFieldViewModel : ObservableObject
    {
        private readonly Func<object> _get;
        private readonly Action<object> _set;
        private readonly Func<bool> _enabled;
        private readonly Func<bool> _leadGet;
        private readonly Action<bool> _leadSet;
        private readonly Func<bool> _leadEnabled;
        private readonly Func<bool> _shown;
        private readonly Action _changed;
        private string _text;

        internal MLeaderFieldViewModel(
            MLeaderFieldKind kind,
            string label,
            Func<object> get,
            Action<object> set,
            IReadOnlyList<MLeaderChoice> choices,
            Action changed
        )
        {
            Kind = kind;
            Label = label;
            _get = get;
            _set = set;
            _changed = changed;
            Choices = choices ?? Array.Empty<MLeaderChoice>();
            _text = TextOf(get?.Invoke());
        }

        private MLeaderFieldViewModel(
            MLeaderFieldViewModel field,
            Func<bool> enabled,
            string lead,
            Func<bool> leadGet,
            Action<bool> leadSet,
            Func<bool> leadEnabled,
            Func<bool> shown,
            string more
        )
            : this(field.Kind, field.Label, field._get, field._set, field.Choices, field._changed)
        {
            _enabled = enabled;
            Lead = lead;
            _leadGet = leadGet;
            _leadSet = leadSet;
            _leadEnabled = leadEnabled;
            _shown = shown;
            More = more;
        }

        /// <summary>Gets what the field is.</summary>
        public MLeaderFieldKind Kind { get; }

        /// <summary>Gets the label, as "Arrow size".</summary>
        public string Label { get; }

        /// <summary>Gets what stands before the label: "check" for a box, a group's name for a button of a pair, empty for none.</summary>
        public string Lead { get; } = string.Empty;

        /// <summary>Gets whether a box to tick stands before the label.</summary>
        public bool HasLeadCheck => Lead == "check";

        /// <summary>Gets whether a button of a pair stands before the label.</summary>
        public bool HasLeadRadio => Lead.Length > 0 && Lead != "check";

        /// <summary>Gets or sets whether the box or the button before the label is ticked.</summary>
        public bool LeadChecked
        {
            get => _leadGet != null && _leadGet();
            set
            {
                if (_leadSet == null || value == LeadChecked || (HasLeadRadio && !value))
                {
                    Raise(nameof(LeadChecked));
                    return;
                }

                _leadSet(value);
                _changed();
            }
        }

        /// <summary>Gets whether the box or the button before the label can be ticked.</summary>
        public bool IsLeadEnabled => _leadEnabled == null || _leadEnabled();

        /// <summary>Gets whether the value can be changed: false where the box before it is clear or the values make it mean nothing.</summary>
        public bool IsEnabled => _enabled == null || _enabled();

        /// <summary>Gets whether the field is shown: the attachments of the left and right while the leaders meet the text on its sides, of the top and bottom otherwise.</summary>
        public bool IsShown => _shown == null || _shown();

        /// <summary>Gets what the "…" after the value opens: "textstyle" for the Text Style dialog, "text" for the default text (not built, greyed); empty for none.</summary>
        public string More { get; } = string.Empty;

        /// <summary>Gets whether a "…" stands after the value.</summary>
        public bool HasMore => More.Length > 0;

        /// <summary>Gets the swatch of a colour field as 0xRRGGBB: the colour chosen, white for ByBlock and ByLayer; null for a field of no colour.</summary>
        public int? SwatchRgb =>
            _get?.Invoke() is DxfColor colour
                ? (colour.IsByBlock || colour.IsByLayer ? 0xFFFFFF : colour.ColorValue & 0xFFFFFF)
                : (int?)null;

        /// <summary>Gets the items of a list field.</summary>
        public IReadOnlyList<MLeaderChoice> Choices { get; private set; }

        /// <summary>Gets or sets the text of a number or a text field, as typed; written when it reads.</summary>
        public string Text
        {
            get => _text;
            set
            {
                if (!Set(ref _text, value ?? string.Empty))
                {
                    return;
                }

                Raise(nameof(IsValid));

                if (Kind == MLeaderFieldKind.Text)
                {
                    _set(_text);
                }
                else if (
                    double.TryParse(_text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    && !double.IsNaN(number)
                    && !double.IsInfinity(number)
                )
                {
                    _set(number);
                }
                else
                {
                    return;
                }

                _changed();
            }
        }

        /// <summary>Gets whether the text typed reads as a number, for a number field; true for the others.</summary>
        public bool IsValid =>
            Kind != MLeaderFieldKind.Number
            || double.TryParse((_text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        /// <summary>Gets or sets the item of a list field: that of the value the field reads.</summary>
        public MLeaderChoice SelectedChoice
        {
            get
            {
                object now = _get?.Invoke();
                return Choices.FirstOrDefault(c => Same(c.Value, now));
            }
            set
            {
                if (value == null || !value.IsEnabled)
                {
                    Raise(nameof(SelectedChoice));
                    return;
                }

                _set(value.Value);
                Raise(nameof(SelectedChoice));
                _changed();
            }
        }

        /// <summary>The field with a box to tick before its label.</summary>
        internal MLeaderFieldViewModel Checked(
            Func<bool> get,
            Action<bool> set,
            Func<bool> enabled = null,
            Func<bool> leadEnabled = null
        ) => new MLeaderFieldViewModel(this, enabled, "check", get, set, leadEnabled, _shown, More);

        /// <summary>The field with a button of a pair before its label, the button greyed while <paramref name="leadEnabled"/> does not hold.</summary>
        internal MLeaderFieldViewModel Paired(
            string group,
            Func<bool> get,
            Action set,
            Func<bool> enabled = null,
            Func<bool> leadEnabled = null
        ) => new MLeaderFieldViewModel(this, enabled, group, get, v => set(), leadEnabled, _shown, More);

        /// <summary>The field greyed while a condition does not hold.</summary>
        internal MLeaderFieldViewModel When(Func<bool> enabled) =>
            new MLeaderFieldViewModel(this, enabled, Lead, _leadGet, _leadSet, _leadEnabled, _shown, More);

        /// <summary>The field shown while a condition holds.</summary>
        internal MLeaderFieldViewModel ShownWhen(Func<bool> shown) =>
            new MLeaderFieldViewModel(this, _enabled, Lead, _leadGet, _leadSet, _leadEnabled, shown, More);

        /// <summary>The field with a "…" after its value.</summary>
        internal MLeaderFieldViewModel WithMore(string more) =>
            new MLeaderFieldViewModel(this, _enabled, Lead, _leadGet, _leadSet, _leadEnabled, _shown, more);

        /// <summary>Reads the value again, after another field set it.</summary>
        internal void Refresh()
        {
            string now = TextOf(_get?.Invoke());
            bool same =
                Kind == MLeaderFieldKind.Text
                    ? _text == now
                    : double.TryParse(
                        (_text ?? string.Empty).Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double typed
                    )
                        && TextOf(typed) == now;

            if (!same && Kind != MLeaderFieldKind.Header)
            {
                _text = now;
                Raise(nameof(Text));
                Raise(nameof(IsValid));
            }

            Raise(nameof(LeadChecked));
            Raise(nameof(IsLeadEnabled));
            Raise(nameof(SelectedChoice));
            Raise(nameof(IsEnabled));
            Raise(nameof(IsShown));
            Raise(nameof(SwatchRgb));
        }

        /// <summary>Gives the list new items, the drawing's text styles after the Text Style dialog.</summary>
        internal void SetChoices(IReadOnlyList<MLeaderChoice> choices)
        {
            Choices = choices;
            Raise(nameof(Choices));
            Raise(nameof(SelectedChoice));
        }

        /// <summary>Whether two colours are the same: of one method and index, and of one value where true colours.</summary>
        internal static bool SameColour(DxfColor a, DxfColor b) =>
            a == null
                ? b == null
                : b != null
                    && a.ColorMethod == b.ColorMethod
                    && a.ColorIndex == b.ColorIndex
                    && (a.ColorMethod != DxfColorMethod.ByColor || a.ColorValue == b.ColorValue);

        private static bool Same(object a, object b)
        {
            if (a is DxfColor x && b is DxfColor y)
            {
                return SameColour(x, y);
            }

            if (a is string s && b is string t)
            {
                return string.Equals(s, t, StringComparison.OrdinalIgnoreCase);
            }

            return Equals(a, b);
        }

        private static string TextOf(object value)
        {
            switch (value)
            {
                case double number:
                    return number.ToString("0.####", CultureInfo.InvariantCulture);
                case int whole:
                    return whole.ToString(CultureInfo.InvariantCulture);
                default:
                    return value as string ?? string.Empty;
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            Label
            + " "
            + (
                Kind == MLeaderFieldKind.Choice ? SelectedChoice?.Label
                : Kind == MLeaderFieldKind.Flag ? LeadChecked.ToString()
                : _text
            );
    }

    /// <summary>A row of the styles list of the Multileader Style Manager: the style's name, whether it is current and annotative, and how many multileaders use it.</summary>
    public sealed class MLeaderStyleRowViewModel
    {
        internal MLeaderStyleRowViewModel(string name, bool isCurrent, bool isAnnotative, int uses)
        {
            Name = name;
            IsCurrent = isCurrent;
            IsAnnotative = isAnnotative;
            Uses = uses;
        }

        /// <summary>Gets the style's name.</summary>
        public string Name { get; }

        /// <summary>Gets whether the style is current (CMLEADERSTYLE), ticked in the list.</summary>
        public bool IsCurrent { get; }

        /// <summary>Gets whether the style is annotative, marked by the annotative triangle.</summary>
        public bool IsAnnotative { get; }

        /// <summary>Gets how many multileaders are drawn in the style.</summary>
        public int Uses { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD's Multileader Style Manager (MLEADERSTYLE, MLS): the styles of the drawing (all, or those in use), the current
    /// one ticked and the annotative ones marked, a preview of the one selected drawn as MLEADER draws one with its description,
    /// and Set Current, New…, Modify… and Delete, greyed for Standard, the current style and a style a multileader uses. Each
    /// button is one undo step of <see cref="CadMLeaderStyleEditor"/>; a drawing of no style is given Standard first, as
    /// AutoCAD's always has one.
    /// </summary>
    public sealed class MLeaderStyleManagerViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private string _filter = TextStyleViewModel.AllStyles;
        private MLeaderStyleRowViewModel _selected;
        private string _notice = string.Empty;
        private ImageSource _preview;

        /// <summary>Makes the Manager's view model over a document, the current style selected.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public MLeaderStyleManagerViewModel(CadDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            SetCurrentCommand = new DelegateCommand(_ => SetCurrent(), _ => _selected != null);
            NewCommand = new DelegateCommand(_ => New());
            ModifyCommand = new DelegateCommand(_ => Modify(), _ => _selected != null);
            DeleteCommand = new DelegateCommand(_ => Delete(), _ => CanDelete);
            CloseCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, true));
            Report(CadMLeaderStyleEditor.AddStandard(document));
            Reload(CadMLeaderStyleEditor.Current(document.Database));
        }

        /// <summary>Raised when the Manager is to close.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets or sets what asks the name of a new style and the style it starts with: the name, or null for Cancel. Set by the window.</summary>
        public Func<StyleNameViewModel, string> AskName { get; set; }

        /// <summary>Gets or sets what shows the Modify Multileader Style dialog: true where OK wrote it. Set by the window.</summary>
        public Func<MLeaderStyleEditViewModel, bool> ShowEditor { get; set; }

        /// <summary>Gets the filters of the list: All styles, Styles in use.</summary>
        public IReadOnlyList<string> Filters { get; } =
            new[] { TextStyleViewModel.AllStyles, TextStyleViewModel.StylesInUse };

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

        /// <summary>Gets the styles listed.</summary>
        public ObservableCollection<MLeaderStyleRowViewModel> Styles { get; } =
            new ObservableCollection<MLeaderStyleRowViewModel>();

        /// <summary>Gets the line over the list: "Current: Standard".</summary>
        public string CurrentStyleText => "Current: " + CadMLeaderStyleEditor.Current(_document.Database);

        /// <summary>Gets the title: "Multileader style manager · plan-level2.dxf".</summary>
        public string Title => "Multileader style manager · " + _document.Title;

        /// <summary>Gets the title over the preview: "Preview of Detail notes".</summary>
        public string PreviewTitle => "Preview of " + _selected?.Name;

        /// <summary>Gets the line at the foot: "4 styles · double-click a style to modify it".</summary>
        public string Footer =>
            Styles.Count.ToString(CultureInfo.InvariantCulture)
            + (Styles.Count == 1 ? " style" : " styles")
            + " · double-click a style to modify it";

        /// <summary>Gets the note under the buttons, of the styles Delete leaves.</summary>
        public string DeleteNote => "Standard, the current style and a style in use cannot be deleted.";

        /// <summary>Gets why the last button was refused; empty where it was not.</summary>
        public string Notice => _notice;

        /// <summary>Gets whether there is a refusal to show.</summary>
        public bool HasNotice => _notice.Length > 0;

        /// <summary>Gets whether the notice is a warning: always, as it says why a button was refused.</summary>
        public bool IsNoticeWarning => _notice.Length > 0;

        /// <summary>Gets or sets the style selected, whose preview is shown.</summary>
        public MLeaderStyleRowViewModel SelectedStyle
        {
            get => _selected;
            set
            {
                if (value != null && Set(ref _selected, value))
                {
                    _preview = null;
                    Raise(nameof(Preview));
                    Raise(nameof(Description));
                    Raise(nameof(PreviewTitle));
                    Raise(nameof(CanDelete));
                    ((DelegateCommand)SetCurrentCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)ModifyCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)DeleteCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets the description under the preview: "Detail notes · straight leader, closed filled arrow 2.5 · landing 8 · MTEXT
        /// 2.5 in ROMANS, attached at the middle of the top line · Used by 14 multileaders."
        /// </summary>
        public string Description
        {
            get
            {
                DxfMLeaderStyle style =
                    _selected == null ? null : CadMLeaderStyleEditor.Find(_document.Database, _selected.Name);
                return style == null
                    ? string.Empty
                    : MLeaderStyleEditViewModel.Summary(
                        _selected.Name,
                        CadMLeaderStyleSettings.Of(style),
                        _selected.Uses
                    );
            }
        }

        /// <summary>Gets the preview of the style selected: a multileader drawn as MLEADER draws one in it.</summary>
        public ImageSource Preview
        {
            get
            {
                DxfMLeaderStyle style =
                    _selected == null ? null : CadMLeaderStyleEditor.Find(_document.Database, _selected.Name);
                return _preview
                    ?? (
                        style == null
                            ? null
                            : _preview = CadStylePreview.MLeader(
                                _document.Database,
                                CadMLeaderStyleSettings.Of(style),
                                _document.Fonts,
                                360,
                                230
                            )
                    );
            }
        }

        /// <summary>Gets whether the style selected may be deleted: not Standard, not current, not in use.</summary>
        public bool CanDelete =>
            _selected != null && CadMLeaderStyleEditor.WhyNotDeleted(_document.Database, _selected.Name) == null;

        /// <summary>Gets the command of Set Current.</summary>
        public ICommand SetCurrentCommand { get; }

        /// <summary>Gets the command of New…: a name and the style it starts with asked, the style added, then the Modify dialog over it.</summary>
        public ICommand NewCommand { get; }

        /// <summary>Gets the command of Modify….</summary>
        public ICommand ModifyCommand { get; }

        /// <summary>Gets the command of Delete.</summary>
        public ICommand DeleteCommand { get; }

        /// <summary>Gets the command of Close.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Makes the style selected current (CMLEADERSTYLE).</summary>
        /// <returns>Whether it was made current; the reason in <see cref="Notice"/> where not.</returns>
        public bool SetCurrent() =>
            _selected != null && Report(CadMLeaderStyleEditor.SetCurrent(_document, _selected.Name), _selected.Name);

        /// <summary>
        /// New…: the name asked (Copy of the style selected offered) with the style it starts with; the style added, one undo
        /// step, then the Modify dialog over it.
        /// </summary>
        /// <returns>Whether a style was added.</returns>
        public bool New()
        {
            string start = _selected?.Name ?? CadMLeaderStyleEditor.Current(_document.Database);
            var ask = new StyleNameViewModel(
                "Create New Multileader Style",
                "Copy of " + start,
                n => CadMLeaderStyleEditor.Find(_document.Database, n) != null,
                Styles.Select(s => s.Name).ToList(),
                start
            )
            {
                UseFor = null,
            };
            string name = AskName?.Invoke(ask);

            if (name == null || !Report(CadMLeaderStyleEditor.Add(_document, name, ask.StartWith), name.Trim()))
            {
                return false;
            }

            Open(CreateModify());
            return true;
        }

        /// <summary>Makes the Modify dialog of the style selected.</summary>
        /// <returns>The dialog's view model; null where none is selected.</returns>
        public MLeaderStyleEditViewModel CreateModify() =>
            _selected == null ? null : new MLeaderStyleEditViewModel(_document, _selected.Name);

        /// <summary>Modify…: the Modify dialog over the style selected.</summary>
        public void Modify() => Open(CreateModify());

        /// <summary>Deletes the style selected; refused for Standard, the current style and a style in use, the reason in <see cref="Notice"/>.</summary>
        /// <returns>Whether it was deleted.</returns>
        public bool Delete() =>
            _selected != null
            && Report(
                CadMLeaderStyleEditor.Delete(_document, _selected.Name),
                CadMLeaderStyleEditor.Current(_document.Database)
            );

        private void Open(MLeaderStyleEditViewModel editor)
        {
            if (editor != null && ShowEditor != null && ShowEditor(editor))
            {
                Reload(editor.StyleName);
            }
        }

        private bool Report(CadEditResult result, string show = null)
        {
            _notice = result.Succeeded ? string.Empty : result.Message;
            Raise(nameof(Notice));
            Raise(nameof(HasNotice));
            Raise(nameof(IsNoticeWarning));

            if (result.Succeeded && show != null)
            {
                Reload(show);
            }

            return result.Succeeded;
        }

        /// <summary>Reads the list again, as the filter has it, the style named selected where listed, else the first.</summary>
        internal void Reload(string show)
        {
            Styles.Clear();
            string current = CadMLeaderStyleEditor.Current(_document.Database);

            foreach (string name in CadMLeaderStyleEditor.Styles(_document.Database))
            {
                int uses = CadMLeaderStyleEditor.Uses(_document.Database, name).Count;
                bool isCurrent = string.Equals(name, current, StringComparison.OrdinalIgnoreCase);

                if (_filter == TextStyleViewModel.AllStyles || uses > 0 || isCurrent)
                {
                    bool annotative =
                        CadMLeaderStyleEditor.Find(_document.Database, name).Annotative == DxfAnnotativeStates.True;
                    Styles.Add(new MLeaderStyleRowViewModel(name, isCurrent, annotative, uses));
                }
            }

            _selected = null;
            SelectedStyle = Styles.FirstOrDefault(s => string.Equals(s.Name, show, StringComparison.OrdinalIgnoreCase))
                ?? Styles.FirstOrDefault();
            Raise(nameof(SelectedStyle));
            Raise(nameof(CurrentStyleText));
            Raise(nameof(Footer));
        }
    }

    /// <summary>
    /// The Modify Multileader Style dialog, as the design lays it: three tabs, Leader Format, Leader Structure and Content,
    /// each field a value of the style; the preview drawn as MLEADER draws one, a description of the tab shown and a notice of
    /// what OK does. Block of the Multileader type shows the block options (Source block, one of the drawing's blocks,
    /// Attachment, Color and Scale); the "…" of the default text is greyed, the default text typed in its box; Scale
    /// multileaders to layout and Specify scale are greyed while Annotative is ticked, as AutoCAD 2021 greys them.
    /// </summary>
    public sealed class MLeaderStyleEditViewModel : ObservableObject
    {
        private static readonly string[] TabNames = { "Leader Format", "Leader Structure", "Content" };

        private readonly CadDocument _document;
        private readonly CadMLeaderStyleSettings _settings;
        private readonly int _uses;
        private readonly bool _extendable;
        private readonly Dictionary<string, IReadOnlyList<MLeaderFieldViewModel>> _tabs = new Dictionary<
            string,
            IReadOnlyList<MLeaderFieldViewModel>
        >(StringComparer.Ordinal);
        private IReadOnlyList<MLeaderTabViewModel> _chips;
        private string _tab = TabNames[0];
        private string _refused = string.Empty;
        private ImageSource _preview;

        /// <summary>Makes the dialog's view model over a style.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no multileader style of the name.</exception>
        public MLeaderStyleEditViewModel(CadDocument document, string name)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            DxfMLeaderStyle style = CadMLeaderStyleEditor.Find(document.Database, name)
                ?? throw new ArgumentException("The drawing has no multileader style \"" + name + "\".", nameof(name));
            StyleName = style.Name;
            _settings = CadMLeaderStyleSettings.Of(style);
            Original = _settings.Clone();
            _uses = CadMLeaderStyleEditor.Uses(document.Database, StyleName).Count;
            _extendable = string.CompareOrdinal(document.Database.Version ?? string.Empty, "AC1027") >= 0;
            OkCommand = new DelegateCommand(_ => Ok());
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
            TextStylesCommand = new DelegateCommand(_ =>
            {
                ShowTextStyles?.Invoke();
                RefreshTextStyles();
            });
            BuildTabs();
        }

        /// <summary>Raised when the dialog is to close: true after OK, false after Cancel.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets or sets what shows the Text Style dialog, as the "…" beside Text style does. Set by the window.</summary>
        public Action ShowTextStyles { get; set; }

        /// <summary>Gets the style's name.</summary>
        public string StyleName { get; }

        /// <summary>Gets the title: "Modify multileader style: Detail notes".</summary>
        public string Title => "Modify multileader style: " + StyleName;

        /// <summary>Gets the title over the preview: "Preview of Detail notes".</summary>
        public string PreviewTitle => "Preview of " + StyleName;

        /// <summary>Gets the settings as they were read.</summary>
        public CadMLeaderStyleSettings Original { get; }

        /// <summary>Gets the settings as the fields have set them.</summary>
        public CadMLeaderStyleSettings Settings => _settings;

        /// <summary>Gets the tabs, in the design's order: Leader Format, Leader Structure, Content.</summary>
        public IReadOnlyList<string> Tabs => TabNames;

        /// <summary>Gets or sets the tab shown, by its name, letters of either case the same.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                string tab = TabNames.FirstOrDefault(t => string.Equals(t, value, StringComparison.OrdinalIgnoreCase));

                if (tab != null && Set(ref _tab, tab))
                {
                    Raise(nameof(Fields));
                    Raise(nameof(Description));

                    foreach (MLeaderTabViewModel chip in TabChips)
                    {
                        chip.Refresh();
                    }
                }
            }
        }

        /// <summary>Gets the chips of the tabs, the one shown checked.</summary>
        public IReadOnlyList<MLeaderTabViewModel> TabChips =>
            _chips ?? (_chips = TabNames.Select(t => new MLeaderTabViewModel(this, t)).ToList());

        /// <summary>Gets the fields of the tab shown, a group's title before its fields.</summary>
        public IReadOnlyList<MLeaderFieldViewModel> Fields => _tabs[_tab];

        /// <summary>Gets the fields of a tab.</summary>
        /// <param name="tab">The tab, as "Content".</param>
        /// <returns>The fields.</returns>
        public IReadOnlyList<MLeaderFieldViewModel> FieldsOf(string tab) =>
            _tabs[TabNames.First(t => string.Equals(t, tab, StringComparison.OrdinalIgnoreCase))];

        /// <summary>Gets the description under the preview, of the tab shown, as "Detail notes · straight leader ByBlock · closed filled arrow 2.5 · break 3.75."</summary>
        public string Description => Describe(_tab);

        /// <summary>
        /// Gets the notice under the description: why OK was refused; else what OK does, "The preview redraws as a value
        /// changes. OK writes the style to the file and redraws the 14 multileaders that use it, as one undo step."
        /// </summary>
        public string Notice =>
            _refused.Length > 0
                ? _refused
                : "The preview redraws as a value changes. OK writes the style to the file and redraws the "
                    + _uses.ToString(CultureInfo.InvariantCulture)
                    + (_uses == 1 ? " multileader that uses it" : " multileaders that use it")
                    + ", as one undo step.";

        /// <summary>Gets whether the notice says why OK was refused, drawn as a warning.</summary>
        public bool IsNoticeWarning => _refused.Length > 0;

        /// <summary>Gets the preview: a multileader of the values set, drawn as MLEADER draws one.</summary>
        public ImageSource Preview =>
            _preview ?? (_preview = CadStylePreview.MLeader(_document.Database, _settings, _document.Fonts, 256, 164));

        /// <summary>Gets the command of OK.</summary>
        public ICommand OkCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>Gets the command of the "…" beside Text style: the Text Style dialog, then the list of text styles read again.</summary>
        public ICommand TextStylesCommand { get; }

        /// <summary>Writes what the fields set, as OK does, one undo step, the multileaders of the style given its values.</summary>
        /// <returns>Whether it was written; the reason in <see cref="Notice"/> where not.</returns>
        public bool Ok()
        {
            MLeaderFieldViewModel wrong = _tabs.Values.SelectMany(f => f).FirstOrDefault(f => !f.IsValid);

            if (wrong != null)
            {
                Refuse(wrong.Label + " must be a number.");
                return false;
            }

            CadEditResult result = CadMLeaderStyleEditor.Apply(_document, StyleName, _settings);

            if (!result.Succeeded)
            {
                Refuse(result.Message);
                return false;
            }

            CloseRequested?.Invoke(this, true);
            return true;
        }

        /// <summary>The description of a style's settings as the Manager writes it under its preview.</summary>
        internal static string Summary(string name, CadMLeaderStyleSettings s, int uses)
        {
            string content =
                s.ContentType == DxfContentType.NoneContent ? "no content"
                : s.ContentType == DxfContentType.BlockContent ? "a block"
                : "MTEXT "
                    + N(s.TextHeight)
                    + " in "
                    + s.TextStyle
                    + ", attached at the "
                    + Attachment(s.LeftAttachment).ToLowerInvariant();
            return name
                + " · "
                + LeaderWord(s.LeaderType)
                + " leader, "
                + s.Arrowhead.ToLowerInvariant()
                + " arrow "
                + N(s.ArrowSize)
                + " · landing "
                + (s.SetLandingDistance ? N(s.LandingDistance) : "free")
                + " · "
                + content
                + " · Used by "
                + uses.ToString(CultureInfo.InvariantCulture)
                + (uses == 1 ? " multileader." : " multileaders.");
        }

        private void Refuse(string why)
        {
            _refused = why ?? string.Empty;
            Raise(nameof(Notice));
            Raise(nameof(IsNoticeWarning));
        }

        /// <summary>A field set a value: the fields read again, the preview and the description drawn again.</summary>
        private void Changed()
        {
            foreach (MLeaderFieldViewModel field in _tabs.Values.SelectMany(f => f))
            {
                field.Refresh();
            }

            _preview = null;
            Raise(nameof(Preview));
            Raise(nameof(Description));
        }

        private void RefreshTextStyles()
        {
            foreach (MLeaderFieldViewModel field in _tabs.Values.SelectMany(f => f).Where(f => f.More == "textstyle"))
            {
                field.SetChoices(TextStyleChoices());
            }

            Changed();
        }

        private MLeaderFieldViewModel Field(
            MLeaderFieldKind kind,
            string label,
            Func<object> get,
            Action<object> set,
            IReadOnlyList<MLeaderChoice> choices = null
        ) => new MLeaderFieldViewModel(kind, label, get, set, choices, Changed);

        private MLeaderFieldViewModel Header(string label) => Field(MLeaderFieldKind.Header, label, null, null);

        private MLeaderFieldViewModel Flag(string label) => Field(MLeaderFieldKind.Flag, label, null, null);

        private static IReadOnlyList<MLeaderChoice> C(params object[] pairs)
        {
            var choices = new List<MLeaderChoice>();

            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                choices.Add(new MLeaderChoice((string)pairs[i], pairs[i + 1]));
            }

            return choices;
        }

        private static IReadOnlyList<MLeaderChoice> Colours(DxfColor own)
        {
            var choices = new List<MLeaderChoice>
            {
                new MLeaderChoice("ByBlock", DxfColor.ByBlock),
                new MLeaderChoice("ByLayer", DxfColor.ByLayer),
            };
            string[] names = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

            for (int i = 0; i < names.Length; i++)
            {
                choices.Add(new MLeaderChoice(names[i], DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)(i + 1))));
            }

            if (own != null && !choices.Any(c => MLeaderFieldViewModel.SameColour((DxfColor)c.Value, own)))
            {
                choices.Add(
                    new MLeaderChoice(
                        "Colour "
                            + (
                                own.IsByColor
                                    ? "#" + (own.ColorValue & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture)
                                    : own.ColorIndex.ToString(CultureInfo.InvariantCulture)
                            ),
                        own
                    )
                );
            }

            return choices;
        }

        private static IReadOnlyList<MLeaderChoice> Weights() =>
            Enum.GetValues(typeof(DxfLineWeight))
                .Cast<DxfLineWeight>()
                .Where(w => w != DxfLineWeight.ByDIPs)
                .Select(w => new MLeaderChoice(
                    w == DxfLineWeight.ByBlock ? "ByBlock"
                        : w == DxfLineWeight.ByLayer ? "ByLayer"
                        : w == DxfLineWeight.ByLineWeightDefault ? "Default"
                        : ((int)w / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm",
                    w
                ))
                .ToList();

        private IReadOnlyList<MLeaderChoice> Linetypes()
        {
            var choices = new List<MLeaderChoice>
            {
                new MLeaderChoice("ByBlock", "ByBlock"),
                new MLeaderChoice("ByLayer", "ByLayer"),
            };

            foreach (DxfLinetypeTableRecord linetype in _document.Database.LinetypeTable)
            {
                if (
                    !string.Equals(linetype.Name, "ByBlock", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(linetype.Name, "ByLayer", StringComparison.OrdinalIgnoreCase)
                )
                {
                    choices.Add(new MLeaderChoice(linetype.Name, linetype.Name));
                }
            }

            return choices;
        }

        private IReadOnlyList<MLeaderChoice> Arrows()
        {
            var names = CadArrowheads.Names.ToList();

            if (!names.Contains(_settings.Arrowhead, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(_settings.Arrowhead);
            }

            return names.Select(n => new MLeaderChoice(n, n)).ToList();
        }

        private IReadOnlyList<MLeaderChoice> TextStyleChoices() =>
            CadTextStyleEditor.Styles(_document.Database).Select(s => new MLeaderChoice(s.Name, s.Name)).ToList();

        private static IReadOnlyList<MLeaderChoice> Angles() =>
            C(
                "0",
                DxfAngleConstraint.DegreesAny,
                "15",
                DxfAngleConstraint.Degrees015,
                "30",
                DxfAngleConstraint.Degrees030,
                "45",
                DxfAngleConstraint.Degrees045,
                "60",
                DxfAngleConstraint.Degrees060,
                "90",
                DxfAngleConstraint.Degrees090
            );

        private static IReadOnlyList<MLeaderChoice> SideAttachments() =>
            Enumerable
                .Range(0, 9)
                .Select(i => new MLeaderChoice(Attachment((DxfTextAttachmentType)i), (DxfTextAttachmentType)i))
                .ToList();

        private static string Attachment(DxfTextAttachmentType type)
        {
            switch (type)
            {
                case DxfTextAttachmentType.AttachmentTopOfTop:
                    return "Top of top line";
                case DxfTextAttachmentType.AttachmentMiddleOfTop:
                    return "Middle of top line";
                case DxfTextAttachmentType.AttachmentMiddle:
                    return "Middle of multi-line text";
                case DxfTextAttachmentType.AttachmentMiddleOfBottom:
                    return "Middle of bottom line";
                case DxfTextAttachmentType.AttachmentBottomOfBottom:
                    return "Bottom of bottom line";
                case DxfTextAttachmentType.AttachmentBottomLine:
                    return "Underline bottom line";
                case DxfTextAttachmentType.AttachmentBottomOfTopLine:
                    return "Underline top line";
                case DxfTextAttachmentType.AttachmentBottomOfTop:
                    return "Bottom of top line";
                case DxfTextAttachmentType.AttachmentAllLine:
                    return "Underline all text";
                case DxfTextAttachmentType.AttachmentCenter:
                    return "Center";
                default:
                    return "Overline and center";
            }
        }

        private static string LeaderWord(DxfLeaderType type) =>
            type == DxfLeaderType.SplineLeader ? "spline"
            : type == DxfLeaderType.InVisibleLeader ? "no"
            : "straight";

        private static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private bool Horizontal => _settings.AttachmentDirection == DxfTextAttachmentDirection.AttachmentHorizontal;

        private bool Words => _settings.ContentType == DxfContentType.MTextContent;

        private bool Blocks => _settings.ContentType == DxfContentType.BlockContent;

        /// <summary>The blocks of the drawing a style of block content can point to: named, no layout's, no reference's, in ordinal order, letters of either case alike first.</summary>
        private List<string> BlockNames()
        {
            var names = new List<string>();

            foreach (DxfBlockTableRecord block in _document.Database.BlockTable)
            {
                if (
                    !block.IsLayout
                    && !block.IsAnonymous
                    && !block.Name.StartsWith("*", StringComparison.Ordinal)
                    && !block.IsFromExternalReference
                    && !block.IsDependent
                )
                {
                    names.Add(block.Name);
                }
            }

            names.Sort(
                (a, b) =>
                {
                    int byName = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                    return byName != 0 ? byName : string.CompareOrdinal(a, b);
                }
            );
            return names;
        }

        /// <summary>The content type set, a style turned to block content given the first block of the drawing where it names none.</summary>
        private void SetContentType(DxfContentType type)
        {
            _settings.ContentType = type;

            if (type == DxfContentType.BlockContent && string.IsNullOrEmpty(_settings.BlockName))
            {
                _settings.BlockName = BlockNames().FirstOrDefault() ?? string.Empty;
            }
        }

        /// <summary>The fields of the three tabs, in the design's order and words.</summary>
        private void BuildTabs()
        {
            CadMLeaderStyleSettings s = _settings;
            _tabs["Leader Format"] = new[]
            {
                Header("General"),
                Field(
                    MLeaderFieldKind.Choice,
                    "Type",
                    () => s.LeaderType,
                    v => s.LeaderType = (DxfLeaderType)v,
                    C(
                        "Straight",
                        DxfLeaderType.StraightLeader,
                        "Spline",
                        DxfLeaderType.SplineLeader,
                        "None",
                        DxfLeaderType.InVisibleLeader
                    )
                ),
                Field(
                    MLeaderFieldKind.Choice,
                    "Color",
                    () => s.LeaderColor,
                    v => s.LeaderColor = (DxfColor)v,
                    Colours(s.LeaderColor)
                ),
                Field(
                    MLeaderFieldKind.Choice,
                    "Linetype",
                    () => s.LeaderLinetype,
                    v => s.LeaderLinetype = (string)v,
                    Linetypes()
                ),
                Field(
                    MLeaderFieldKind.Choice,
                    "Lineweight",
                    () => s.LeaderLineweight,
                    v => s.LeaderLineweight = (DxfLineWeight)v,
                    Weights()
                ),
                Header("Arrowhead"),
                Field(MLeaderFieldKind.Choice, "Symbol", () => s.Arrowhead, v => s.Arrowhead = (string)v, Arrows()),
                Field(MLeaderFieldKind.Number, "Size", () => s.ArrowSize, v => s.ArrowSize = (double)v),
                Header("Leader break"),
                Field(MLeaderFieldKind.Number, "Break size", () => s.BreakSize, v => s.BreakSize = (double)v),
            };

            _tabs["Leader Structure"] = new[]
            {
                Header("Constraints"),
                Field(
                        MLeaderFieldKind.Number,
                        "Maximum leader points",
                        () => s.MaxLeaderPoints == 0 ? 2 : s.MaxLeaderPoints,
                        v => s.MaxLeaderPoints = Math.Max(2, (int)Math.Round((double)v))
                    )
                    .Checked(
                        () => s.MaxLeaderPoints != 0,
                        on => s.MaxLeaderPoints = on ? 2 : 0,
                        () => s.MaxLeaderPoints != 0
                    ),
                Field(
                        MLeaderFieldKind.Choice,
                        "First segment angle",
                        () => s.FirstSegmentAngle,
                        v => s.FirstSegmentAngle = (DxfAngleConstraint)v,
                        Angles()
                    )
                    .Checked(
                        () => s.FirstSegmentAngle != DxfAngleConstraint.DegreesAny,
                        on => s.FirstSegmentAngle = on ? DxfAngleConstraint.Degrees015 : DxfAngleConstraint.DegreesAny,
                        () => s.FirstSegmentAngle != DxfAngleConstraint.DegreesAny
                    ),
                Field(
                        MLeaderFieldKind.Choice,
                        "Second segment angle",
                        () => s.SecondSegmentAngle,
                        v => s.SecondSegmentAngle = (DxfAngleConstraint)v,
                        Angles()
                    )
                    .Checked(
                        () => s.SecondSegmentAngle != DxfAngleConstraint.DegreesAny,
                        on => s.SecondSegmentAngle = on ? DxfAngleConstraint.Degrees015 : DxfAngleConstraint.DegreesAny,
                        () => s.SecondSegmentAngle != DxfAngleConstraint.DegreesAny
                    ),
                Header("Landing settings"),
                Flag("Automatically include landing").Checked(() => s.IncludeLanding, on => s.IncludeLanding = on),
                Field(
                        MLeaderFieldKind.Number,
                        "Set landing distance",
                        () => s.LandingDistance,
                        v => s.LandingDistance = (double)v
                    )
                    .Checked(
                        () => s.SetLandingDistance,
                        on => s.SetLandingDistance = on,
                        () => s.SetLandingDistance && s.IncludeLanding,
                        () => s.IncludeLanding
                    ),
                Header("Scale"),
                Flag("Annotative").Checked(() => s.Annotative, on => s.Annotative = on),
                // Both greyed while Annotative is ticked, as AutoCAD 2021's dialog greys them.
                Flag("Scale multileaders to layout")
                    .Paired("mlscale", () => s.Scale == 0.0, () => s.Scale = 0.0, null, () => !s.Annotative),
                Field(
                        MLeaderFieldKind.Number,
                        "Specify scale",
                        () => s.Scale == 0.0 ? 1.0 : s.Scale,
                        v => s.Scale = (double)v > 0.0 ? (double)v : s.Scale
                    )
                    .Paired(
                        "mlscale",
                        () => s.Scale != 0.0,
                        () => s.Scale = Original.Scale > 0.0 ? Original.Scale : 1.0,
                        () => s.Scale != 0.0 && !s.Annotative,
                        () => !s.Annotative
                    ),
            };

            _tabs["Content"] = new[]
            {
                Header("Multileader type"),
                Field(
                    MLeaderFieldKind.Choice,
                    "Type",
                    () => s.ContentType,
                    v => SetContentType((DxfContentType)v),
                    new[]
                    {
                        new MLeaderChoice("Mtext", DxfContentType.MTextContent),
                        new MLeaderChoice("Block", DxfContentType.BlockContent),
                        new MLeaderChoice("None", DxfContentType.NoneContent),
                    }
                ),
                Header("Block options").ShownWhen(() => Blocks),
                Field(
                        MLeaderFieldKind.Choice,
                        "Source block",
                        () => s.BlockName,
                        v => s.BlockName = (string)v,
                        BlockNames().Select(n => new MLeaderChoice(n, n)).ToList()
                    )
                    .ShownWhen(() => Blocks),
                Field(
                        MLeaderFieldKind.Choice,
                        "Attachment",
                        () => s.BlockConnection,
                        v => s.BlockConnection = (DxfBlockConnectionType)v,
                        C(
                            "Center extents",
                            DxfBlockConnectionType.ConnectExtents,
                            "Insertion point",
                            DxfBlockConnectionType.ConnectBase
                        )
                    )
                    .ShownWhen(() => Blocks),
                Field(
                        MLeaderFieldKind.Choice,
                        "Color",
                        () => s.BlockColor,
                        v => s.BlockColor = (DxfColor)v,
                        Colours(s.BlockColor)
                    )
                    .ShownWhen(() => Blocks),
                Field(MLeaderFieldKind.Number, "Scale", () => s.BlockScale, v => s.BlockScale = (double)v)
                    .ShownWhen(() => Blocks),
                Header("Text options"),
                Field(MLeaderFieldKind.Text, "Default text", () => s.DefaultText, v => s.DefaultText = (string)v)
                    .When(() => Words)
                    .WithMore("text"),
                Field(
                        MLeaderFieldKind.Choice,
                        "Text style",
                        () => s.TextStyle,
                        v => s.TextStyle = (string)v,
                        TextStyleChoices()
                    )
                    .When(() => Words)
                    .WithMore("textstyle"),
                Field(
                        MLeaderFieldKind.Choice,
                        "Text angle",
                        () => s.TextAngle,
                        v => s.TextAngle = (DxfTextAngleType)v,
                        C(
                            "Insert angle",
                            DxfTextAngleType.InsertAngle,
                            "Keep horizontal",
                            DxfTextAngleType.HorizontalAngle,
                            "Always right-reading",
                            DxfTextAngleType.AlwaysRightReadingAngle
                        )
                    )
                    .When(() => Words),
                Field(
                        MLeaderFieldKind.Choice,
                        "Text color",
                        () => s.TextColor,
                        v => s.TextColor = (DxfColor)v,
                        Colours(s.TextColor)
                    )
                    .When(() => Words),
                Field(MLeaderFieldKind.Number, "Text height", () => s.TextHeight, v => s.TextHeight = (double)v)
                    .When(() => Words),
                Flag("Always left justify")
                    .Checked(() => s.AlwaysLeftJustify, on => s.AlwaysLeftJustify = on, null, () => Words),
                Flag("Frame text").Checked(() => s.FrameText, on => s.FrameText = on, null, () => Words),
                Header("Leader connection"),
                Flag("Horizontal attachment")
                    .Paired(
                        "mlconn",
                        () => Horizontal,
                        () => s.AttachmentDirection = DxfTextAttachmentDirection.AttachmentHorizontal,
                        () => Words
                    ),
                Flag("Vertical attachment")
                    .Paired(
                        "mlconn",
                        () => !Horizontal,
                        () => s.AttachmentDirection = DxfTextAttachmentDirection.AttachmentVertical,
                        () => Words
                    ),
                Field(
                        MLeaderFieldKind.Choice,
                        "Left attachment",
                        () => s.LeftAttachment,
                        v => s.LeftAttachment = (DxfTextAttachmentType)v,
                        SideAttachments()
                    )
                    .When(() => Words)
                    .ShownWhen(() => Horizontal),
                Field(
                        MLeaderFieldKind.Choice,
                        "Right attachment",
                        () => s.RightAttachment,
                        v => s.RightAttachment = (DxfTextAttachmentType)v,
                        SideAttachments()
                    )
                    .When(() => Words)
                    .ShownWhen(() => Horizontal),
                Field(
                        MLeaderFieldKind.Choice,
                        "Top attachment",
                        () => s.TopAttachment,
                        v => s.TopAttachment = (DxfTextAttachmentType)v,
                        C(
                            "Center",
                            DxfTextAttachmentType.AttachmentCenter,
                            "Overline and center",
                            DxfTextAttachmentType.AttachmentLinedCenter
                        )
                    )
                    .When(() => Words)
                    .ShownWhen(() => !Horizontal),
                Field(
                        MLeaderFieldKind.Choice,
                        "Bottom attachment",
                        () => s.BottomAttachment,
                        v => s.BottomAttachment = (DxfTextAttachmentType)v,
                        C(
                            "Center",
                            DxfTextAttachmentType.AttachmentCenter,
                            "Underline and center",
                            DxfTextAttachmentType.AttachmentLinedCenter
                        )
                    )
                    .When(() => Words)
                    .ShownWhen(() => !Horizontal),
                Field(MLeaderFieldKind.Number, "Landing gap", () => s.LandingGap, v => s.LandingGap = (double)v)
                    .When(() => Words),
                Flag("Extend leader to text")
                    .Checked(
                        () => s.ExtendLeaderToText,
                        on => s.ExtendLeaderToText = on,
                        null,
                        () => Words && _extendable
                    ),
            };
        }

        /// <summary>The description of a tab, in the design's words, from the values set.</summary>
        private string Describe(string tab)
        {
            CadMLeaderStyleSettings s = _settings;
            string name = StyleName + " · ";

            switch (tab)
            {
                case "Leader Format":
                    string colour = Colours(s.LeaderColor)
                        .FirstOrDefault(c => MLeaderFieldViewModel.SameColour((DxfColor)c.Value, s.LeaderColor))
                        ?.Label
                        ?? "ByBlock";
                    return name
                        + LeaderWord(s.LeaderType)
                        + " leader "
                        + colour
                        + " · "
                        + s.Arrowhead.ToLowerInvariant()
                        + " arrow "
                        + N(s.ArrowSize)
                        + " · break "
                        + N(s.BreakSize)
                        + ".";
                case "Leader Structure":
                    string points =
                        s.MaxLeaderPoints == 0
                            ? "any number of leader points"
                            : "at most " + s.MaxLeaderPoints.ToString(CultureInfo.InvariantCulture) + " leader points";
                    string angles =
                        s.FirstSegmentAngle == DxfAngleConstraint.DegreesAny
                        && s.SecondSegmentAngle == DxfAngleConstraint.DegreesAny
                            ? "no angle constraints"
                            : "angle constraints";
                    string landing =
                        !s.IncludeLanding ? "no landing"
                        : s.SetLandingDistance ? "landing " + N(s.LandingDistance) + " included"
                        : "landing included";
                    return name
                        + points
                        + ", "
                        + angles
                        + " · "
                        + landing
                        + " · "
                        + (s.Scale == 0.0 ? "scaled to layout." : "scale " + N(s.Scale) + ".");
                default:
                    if (!Words)
                    {
                        return name + (s.ContentType == DxfContentType.NoneContent ? "no content." : "a block.");
                    }

                    string kept =
                        s.TextAngle == DxfTextAngleType.HorizontalAngle ? "kept horizontal"
                        : s.TextAngle == DxfTextAngleType.InsertAngle ? "at the insert angle"
                        : "always right-reading";
                    string attached = Horizontal
                        ? (
                            s.LeftAttachment == s.RightAttachment
                                ? "attached at the " + Attachment(s.LeftAttachment).ToLowerInvariant() + " both sides"
                                : "attached at the "
                                    + Attachment(s.LeftAttachment).ToLowerInvariant()
                                    + " and the "
                                    + Attachment(s.RightAttachment).ToLowerInvariant()
                        )
                        : "attached at the top and bottom";
                    return name
                        + "MTEXT "
                        + N(s.TextHeight)
                        + " in "
                        + s.TextStyle
                        + ", "
                        + kept
                        + " · "
                        + attached
                        + ", gap "
                        + N(s.LandingGap)
                        + ".";
            }
        }
    }

    /// <summary>A tab's chip of the Modify Multileader Style dialog: its name, checked while it is shown.</summary>
    public sealed class MLeaderTabViewModel : ObservableObject
    {
        private readonly MLeaderStyleEditViewModel _dialog;

        internal MLeaderTabViewModel(MLeaderStyleEditViewModel dialog, string name)
        {
            _dialog = dialog;
            Name = name;
        }

        /// <summary>Gets the tab's name, as "Leader Structure".</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether the tab is shown; set true, it is.</summary>
        public bool IsSelected
        {
            get => _dialog.Tab == Name;
            set
            {
                if (value)
                {
                    _dialog.Tab = Name;
                }
            }
        }

        internal void Refresh() => Raise(nameof(IsSelected));
    }
}
