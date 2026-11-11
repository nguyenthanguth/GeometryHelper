using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>What the dialog of a dimension style does with OK: makes a new style, changes one, or overrides the current one on the dimensions selected.</summary>
    public enum DimStyleEditMode
    {
        /// <summary>New dimension style: a style added, starting with another's variables.</summary>
        New,

        /// <summary>Modify dimension style: a style's variables written.</summary>
        Modify,

        /// <summary>Override current style: the variables that differ from the current style written as overrides of the dimensions selected.</summary>
        Override,
    }

    /// <summary>
    /// The New, Modify and Override dialogs of a dimension style, as the design lays them out: seven tabs (Lines, Symbols and
    /// arrows, Text, Fit, Primary units, Alternate units, Tolerances), each field one dimension variable of the style with the
    /// variable named beside it, the preview drawn by the builders the DIM commands draw with, a description of the tab shown
    /// and a notice of what OK does. The variables are kept by name as <see cref="CadDimStyleEditor"/> takes them.
    /// </summary>
    public sealed class DimStyleEditViewModel : ObservableObject
    {
        private static readonly string[] TabNames =
        {
            "Lines",
            "Symbols and arrows",
            "Text",
            "Fit",
            "Primary units",
            "Alternate units",
            "Tolerances",
        };

        // The mark in DIMPOST that puts the alternate units below the primary value, as AutoCAD's Placement writes it.
        private const string Below = "\\X";

        private readonly CadDocument _document;
        private readonly SortedDictionary<string, object> _values;
        private readonly IReadOnlyCollection<string> _selection;
        private readonly string _startWith;
        private readonly int _uses;
        private IReadOnlyList<DimTabViewModel> _chips;
        private readonly Dictionary<string, IReadOnlyList<DimFieldViewModel>> _tabs = new Dictionary<
            string,
            IReadOnlyList<DimFieldViewModel>
        >(StringComparer.Ordinal);
        private string _tab = TabNames[0];
        private string _refused = string.Empty;
        private ImageSource _preview;
        private DxfDatabase _copy;

        /// <summary>Makes the dialog's view model.</summary>
        /// <param name="document">The document.</param>
        /// <param name="mode">New, Modify or Override.</param>
        /// <param name="name">The style: the new name for New, the style changed for Modify, the current style for Override.</param>
        /// <param name="startWith">The style New starts with; ignored by the others.</param>
        /// <param name="selection">The dimensions Override writes into; ignored by the others.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style read is not in the drawing.</exception>
        public DimStyleEditViewModel(
            CadDocument document,
            DimStyleEditMode mode,
            string name,
            string startWith = null,
            IReadOnlyCollection<string> selection = null
        )
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            Mode = mode;
            StyleName = name ?? throw new ArgumentNullException(nameof(name));
            string read = mode == DimStyleEditMode.New ? startWith : name;

            if (read == null || !document.Database.DimStyleTable.Has(read))
            {
                throw new ArgumentException("The drawing has no dimension style " + read + ".", nameof(name));
            }

            _values = CadDimStyleEditor.Read(document.Database.DimStyleTable[read]);
            Original = new SortedDictionary<string, object>(_values, StringComparer.Ordinal);
            _selection = selection ?? Array.Empty<string>();
            _startWith = startWith;
            _uses = mode == DimStyleEditMode.Modify ? CadDimStyleEditor.CountUses(document, name) : 0;
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

        /// <summary>Gets what OK does.</summary>
        public DimStyleEditMode Mode { get; }

        /// <summary>Gets the style's name.</summary>
        public string StyleName { get; }

        /// <summary>Gets the title: "New dimension style: Copy of ISO-25", "Modify dimension style: ISO-25", "Override current style: ISO-25".</summary>
        public string Title =>
            (
                Mode == DimStyleEditMode.New ? "New dimension style: "
                : Mode == DimStyleEditMode.Modify ? "Modify dimension style: "
                : "Override current style: "
            ) + StyleName;

        /// <summary>Gets the title over the preview: "Preview of ISO-25".</summary>
        public string PreviewTitle => "Preview of " + StyleName;

        /// <summary>Gets the variables as they were read, before any field set them.</summary>
        public IReadOnlyDictionary<string, object> Original { get; }

        /// <summary>Gets the variables as the fields have set them, by name.</summary>
        public IReadOnlyDictionary<string, object> Values => _values;

        /// <summary>Gets the tabs, in the design's order.</summary>
        public IReadOnlyList<string> Tabs => TabNames;

        /// <summary>Gets or sets the tab shown, by its name as <see cref="Tabs"/> spells it, letters of either case the same.</summary>
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

                    foreach (DimTabViewModel chip in TabChips)
                    {
                        chip.Refresh();
                    }
                }
            }
        }

        /// <summary>Gets the chips of the tabs, the one shown checked.</summary>
        public IReadOnlyList<DimTabViewModel> TabChips =>
            _chips ?? (_chips = TabNames.Select(t => new DimTabViewModel(this, t)).ToList());

        /// <summary>Gets the fields of the tab shown, a group's title before its fields.</summary>
        public IReadOnlyList<DimFieldViewModel> Fields => _tabs[_tab];

        /// <summary>Gets the fields of a tab.</summary>
        /// <param name="tab">The tab, as "Text".</param>
        /// <returns>The fields.</returns>
        public IReadOnlyList<DimFieldViewModel> FieldsOf(string tab) =>
            _tabs[TabNames.First(t => string.Equals(t, tab, StringComparison.OrdinalIgnoreCase))];

        /// <summary>
        /// Gets the description under the preview, of the tab shown: "ISO-25 · text 2.5 in Standard, above the line, centred,
        /// offset 0.625, aligned as ISO." on the Text tab.
        /// </summary>
        public string Description => Describe(_tab);

        /// <summary>
        /// Gets the notice under the description: why OK was refused; else the model drawing's notice of a style it makes; else
        /// what OK does, as "The preview redraws as a value changes. OK writes the style to the file and redraws the 38
        /// dimensions that use it, as one undo step."
        /// </summary>
        public string Notice
        {
            get
            {
                if (_refused.Length > 0)
                {
                    return _refused;
                }

                if (CadDimStyleEditor.IsModelStyle(StyleName))
                {
                    return ManagerNotices.ModelStyle;
                }

                switch (Mode)
                {
                    case DimStyleEditMode.Override:
                        return ManagerNotices.Override(_selection.Count);
                    case DimStyleEditMode.New:
                        return "The preview redraws as a value changes. OK adds the style to the file, as one undo step.";
                    default:
                        return "The preview redraws as a value changes. OK writes the style to the file and redraws the "
                            + _uses.ToString(CultureInfo.InvariantCulture)
                            + (_uses == 1 ? " dimension that uses it" : " dimensions that use it")
                            + ", as one undo step.";
                }
            }
        }

        /// <summary>Gets whether the notice says why OK was refused, drawn as a warning.</summary>
        public bool IsNoticeWarning => _refused.Length > 0;

        /// <summary>Gets the preview: a linear, a radius and an angular dimension in the variables set, drawn by the DIM commands' builders.</summary>
        public ImageSource Preview =>
            _preview ?? (_preview = CadStylePreview.Dimensions(_document.Database, _values, _document.Fonts, 256, 164));

        /// <summary>Gets the command of OK.</summary>
        public ICommand OkCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>Gets the command of the "…" beside Text style: the Text Style dialog, then the list of text styles read again.</summary>
        public ICommand TextStylesCommand { get; }

        /// <summary>Writes what the fields set, as OK does: the style added (New), written (Modify) or the overrides written into the dimensions selected (Override).</summary>
        /// <returns>Whether it was written; the reason in <see cref="Notice"/> where not.</returns>
        public bool Ok()
        {
            DimFieldViewModel wrong = _tabs.Values.SelectMany(f => f).FirstOrDefault(f => !f.IsValid);

            if (wrong != null)
            {
                Refuse(wrong.Label + " must be a number.");
                return false;
            }

            CadEditResult result;

            switch (Mode)
            {
                case DimStyleEditMode.New:
                    result = CadDimStyleEditor.Add(_document, StyleName, _startWith, _values);
                    break;
                case DimStyleEditMode.Modify:
                    result = CadDimStyleEditor.Apply(_document, StyleName, _values);
                    break;
                default:
                    IDictionary<string, object> changed = CadDimStyleEditor
                        .Compare(Original.ToDictionary(v => v.Key, v => v.Value), _values)
                        .ToDictionary(d => d.Key, d => _values[d.Key]);
                    result = CadDimStyleEditor.Override(_document, _selection, changed);
                    break;
            }

            if (!result.Succeeded)
            {
                Refuse(result.Message);
                return false;
            }

            CloseRequested?.Invoke(this, true);
            return true;
        }

        private void Refuse(string why)
        {
            _refused = why ?? string.Empty;
            Raise(nameof(Notice));
            Raise(nameof(IsNoticeWarning));
        }

        /// <summary>A field set a variable: the fields read again (one may set or grey another), the preview and the description drawn again.</summary>
        private void Changed()
        {
            foreach (DimFieldViewModel field in _tabs.Values.SelectMany(f => f))
            {
                field.Refresh();
            }

            _preview = null;
            Raise(nameof(Preview));
            Raise(nameof(Description));
        }

        private void RefreshTextStyles()
        {
            foreach (DimFieldViewModel field in _tabs.Values.SelectMany(f => f).Where(f => f.Variable == "DIMTXSTY"))
            {
                field.SetChoices(TextStyleChoices());
            }

            Changed();
        }

        private object Get(string name) => _values.TryGetValue(name, out object value) ? value : null;

        private double Real(string name) => Get(name) is double value ? value : 0.0;

        private int Whole(string name) => Get(name) is int value ? value : 0;

        private bool Flag(string name) => Get(name) is bool value && value;

        private string Words(string name) => Get(name) as string ?? string.Empty;

        private void Put(string name, object value)
        {
            if (_values.ContainsKey(name))
            {
                _values[name] = value;
            }
        }

        private DimFieldViewModel Field(
            DimFieldKind kind,
            string label,
            string variable,
            Func<object> get,
            Action<object> set,
            IReadOnlyList<DimChoice> choices = null,
            Func<bool> enabled = null,
            string shown = null
        ) => new DimFieldViewModel(kind, label, variable, get, set, choices, Changed, enabled, shown);

        private DimFieldViewModel Header(string label) => Field(DimFieldKind.Header, label, string.Empty, null, null);

        private DimFieldViewModel Plain(
            DimFieldKind kind,
            string label,
            string name,
            IReadOnlyList<DimChoice> choices = null,
            Func<bool> enabled = null
        ) => Field(kind, label, name.ToUpperInvariant(), () => Get(name), v => Put(name, v), choices, enabled);

        /// <summary>
        /// The zeros a variable leaves out, as one list: None, Leading, Trailing, Leading and trailing; the bits of feet and
        /// inches (1 and 2) of DIMZIN, DIMALTZ and DIMTZIN kept as they are.
        /// </summary>
        private DimFieldViewModel Zeros(string name, bool angular, Func<bool> enabled = null)
        {
            int leading = angular ? 1 : 4;
            int trailing = angular ? 2 : 8;
            return Field(
                DimFieldKind.Choice,
                "Zero suppression",
                name.ToUpperInvariant(),
                () => Whole(name) & (leading | trailing),
                v => Put(name, (Whole(name) & ~(leading | trailing)) | (int)v),
                C("None", 0, "Leading", leading, "Trailing", trailing, "Leading and trailing", leading | trailing),
                enabled
            );
        }

        /// <summary>DIMPOST or DIMAPOST as the one box of the design writes it, the measurement as &lt;&gt; or []: "&lt;&gt; mm"; the mark of the alternate units' placement kept out of it.</summary>
        private DimFieldViewModel Affix(string name, Func<bool> enabled = null) =>
            Field(
                DimFieldKind.Text,
                "Prefix and suffix",
                name.ToUpperInvariant(),
                () => Unmarked(Words(name)),
                v =>
                {
                    string typed = (string)v ?? string.Empty;
                    Put(
                        name,
                        name == "Dimpost" && Words(name).EndsWith(Below, StringComparison.Ordinal)
                            ? Unmarked(typed) + Below
                            : typed
                    );
                },
                null,
                enabled
            );

        private static string Unmarked(string post) =>
            post.EndsWith(Below, StringComparison.Ordinal) ? post.Substring(0, post.Length - Below.Length) : post;

        private static IReadOnlyList<DimChoice> C(params object[] pairs)
        {
            var choices = new List<DimChoice>();

            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                choices.Add(new DimChoice((string)pairs[i], pairs[i + 1]));
            }

            return choices;
        }

        private static IReadOnlyList<DimChoice> Precision() =>
            Enumerable.Range(0, 9).Select(i => new DimChoice(i == 0 ? "0" : "0." + new string('0', i), i)).ToList();

        private static IReadOnlyList<DimChoice> Units() =>
            C(
                "Scientific",
                1,
                "Decimal",
                2,
                "Engineering",
                3,
                "Architectural",
                4,
                "Fractional",
                5,
                "Windows desktop",
                6
            );

        private IReadOnlyList<DimChoice> Colours(string name)
        {
            var choices = new List<DimChoice>
            {
                new DimChoice("ByBlock", DxfColor.ByBlock),
                new DimChoice("ByLayer", DxfColor.ByLayer),
            };
            string[] names = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

            for (int i = 0; i < names.Length; i++)
            {
                choices.Add(new DimChoice(names[i], DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)(i + 1))));
            }

            if (
                Get(name) is DxfColor own
                && !choices.Any(c => CadDimStyleEditor.Text(c.Value) == CadDimStyleEditor.Text(own))
            )
            {
                choices.Add(new DimChoice("Colour " + CadDimStyleEditor.Text(own), own));
            }

            return choices;
        }

        private static IReadOnlyList<DimChoice> Weights() =>
            Enum.GetValues(typeof(DxfLineWeight))
                .Cast<DxfLineWeight>()
                .Where(w => w != DxfLineWeight.ByDIPs)
                .Select(w => new DimChoice(
                    w == DxfLineWeight.ByBlock ? "ByBlock"
                        : w == DxfLineWeight.ByLayer ? "ByLayer"
                        : w == DxfLineWeight.ByLineWeightDefault ? "Default"
                        : ((int)w / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm",
                    w
                ))
                .ToList();

        private IReadOnlyList<DimChoice> Linetypes()
        {
            var choices = new List<DimChoice> { new DimChoice("ByBlock", null) };

            foreach (DxfLinetypeTableRecord linetype in _document.Database.LinetypeTable)
            {
                if (!string.Equals(linetype.Name, "ByBlock", StringComparison.OrdinalIgnoreCase))
                {
                    choices.Add(new DimChoice(linetype.Name, linetype.Name));
                }
            }

            return choices;
        }

        private IReadOnlyList<DimChoice> Arrows()
        {
            var names = CadArrowheads.Names.ToList();

            foreach (string own in new[] { Words("Dimblk"), Words("Dimblk1"), Words("Dimblk2"), Words("Dimldrblk") })
            {
                if (own.Length > 0 && !names.Contains(own))
                {
                    names.Add(own);
                }
            }

            return names.Select(n => new DimChoice(n, n, ArrowPicture)).ToList();
        }

        /// <summary>The picture of an entry of the arrowhead lists, as AutoCAD's lists show it; a user arrow from a copy of the drawing.</summary>
        private BitmapSource ArrowPicture(string name) =>
            CadStylePreview.ArrowheadEntry(
                name,
                () => _copy = _copy ?? DxfDatabase.Parse(_document.Database.ToText()),
                "DimensionStyle"
            );

        private IReadOnlyList<DimChoice> TextStyleChoices() =>
            CadTextStyleEditor.Styles(_document.Database).Select(s => new DimChoice(s.Name, s.Name)).ToList();

        private bool Fractions => Whole("Dimlunit") == 4 || Whole("Dimlunit") == 5;

        private string Method =>
            Flag("Dimlim") ? "Limits"
            : !Flag("Dimtol") ? "None"
            : Real("Dimtm") == Real("Dimtp") ? "Symmetrical"
            : "Deviation";

        /// <summary>The fields of the seven tabs, each a variable of the style, in the design's order and words.</summary>
        private void BuildTabs()
        {
            _tabs["Lines"] = new[]
            {
                Header("Dimension lines"),
                Plain(DimFieldKind.Choice, "Colour", "Dimclrd", Colours("Dimclrd")),
                Plain(DimFieldKind.Choice, "Linetype", "Dimltype", Linetypes()),
                Plain(DimFieldKind.Choice, "Lineweight", "Dimlwd", Weights()),
                Plain(DimFieldKind.Number, "Extend beyond ticks", "Dimdle"),
                Plain(DimFieldKind.Number, "Baseline spacing", "Dimdli"),
                Plain(DimFieldKind.Flag, "Suppress dim line 1", "Dimsd1"),
                Plain(DimFieldKind.Flag, "Suppress dim line 2", "Dimsd2"),
                Header("Extension lines"),
                Plain(DimFieldKind.Choice, "Colour", "Dimclre", Colours("Dimclre")),
                Plain(DimFieldKind.Choice, "Linetype ext line 1", "Dimltex1", Linetypes()),
                Plain(DimFieldKind.Choice, "Linetype ext line 2", "Dimltex2", Linetypes()),
                Plain(DimFieldKind.Choice, "Lineweight", "Dimlwe", Weights()),
                Plain(DimFieldKind.Flag, "Suppress ext line 1", "Dimse1"),
                Plain(DimFieldKind.Flag, "Suppress ext line 2", "Dimse2"),
                Plain(DimFieldKind.Number, "Extend beyond dim lines", "Dimexe"),
                Plain(DimFieldKind.Number, "Offset from origin", "Dimexo"),
                Field(
                    DimFieldKind.Flag,
                    "Fixed length extension lines",
                    "DIMFXLON",
                    () => Get("DimfxlenOn"),
                    v => Put("DimfxlenOn", v)
                ),
                Field(
                    DimFieldKind.Number,
                    "Length",
                    "DIMFXL",
                    () => Get("Dimfxlen"),
                    v => Put("Dimfxlen", v),
                    null,
                    () => Flag("DimfxlenOn")
                ),
            };

            IReadOnlyList<DimChoice> arrows = Arrows();
            _tabs["Symbols and arrows"] = new[]
            {
                Header("Arrowheads"),
                Field(
                    DimFieldKind.Choice,
                    "First",
                    "DIMBLK1",
                    () => Flag("Dimsah") ? Get("Dimblk1") : Get("Dimblk"),
                    v =>
                    {
                        Put("Dimsah", false);
                        Put("Dimblk", v);
                        Put("Dimblk1", v);
                        Put("Dimblk2", v);
                    },
                    arrows
                ),
                Field(
                    DimFieldKind.Choice,
                    "Second",
                    "DIMBLK2",
                    () => Flag("Dimsah") ? Get("Dimblk2") : Get("Dimblk"),
                    v =>
                    {
                        object first = Flag("Dimsah") ? Get("Dimblk1") : Get("Dimblk");
                        bool same = string.Equals(
                            CadDimStyleEditor.Text(first),
                            CadDimStyleEditor.Text(v),
                            StringComparison.Ordinal
                        );
                        Put("Dimsah", !same);
                        Put(same ? "Dimblk" : "Dimblk1", first);
                        Put("Dimblk2", v);
                    },
                    arrows
                ),
                Plain(DimFieldKind.Choice, "Leader", "Dimldrblk", arrows),
                Plain(DimFieldKind.Number, "Arrow size", "Dimasz"),
                Header("Centre marks"),
                Field(
                    DimFieldKind.Choice,
                    "Type",
                    "DIMCEN",
                    () =>
                        Real("Dimcen") > 0.0 ? "Mark"
                        : Real("Dimcen") < 0.0 ? "Line"
                        : "None",
                    v =>
                    {
                        double size = Math.Abs(Real("Dimcen")) > 0.0 ? Math.Abs(Real("Dimcen")) : 2.5;
                        Put(
                            "Dimcen",
                            (string)v == "Mark" ? size
                                : (string)v == "Line" ? -size
                                : 0.0
                        );
                    },
                    C("None", "None", "Mark", "Mark", "Line", "Line")
                ),
                Field(
                    DimFieldKind.Number,
                    "Size",
                    "DIMCEN",
                    () => Math.Abs(Real("Dimcen")),
                    v => Put("Dimcen", Real("Dimcen") < 0.0 ? -(double)v : (double)v),
                    null,
                    () => Real("Dimcen") != 0.0
                ),
                Header("Arc length symbol"),
                Plain(
                    DimFieldKind.Choice,
                    "Placement",
                    "Dimarcsym",
                    C("Preceding dimension text", 0, "Above dimension text", 1, "None", 2)
                ),
                Header("Radius dimension jog"),
                Plain(DimFieldKind.Angle, "Jog angle", "Dimjogang"),
            };

            _tabs["Text"] = new[]
            {
                Header("Text appearance"),
                // A style that names no text style writes in Standard, as AutoCAD reads it.
                Field(
                    DimFieldKind.Choice,
                    "Text style",
                    "DIMTXSTY",
                    () => Words("Dimtxsty").Length > 0 ? Words("Dimtxsty") : "Standard",
                    v => Put("Dimtxsty", v),
                    TextStyleChoices()
                ),
                Plain(DimFieldKind.Choice, "Text colour", "Dimclrt", Colours("Dimclrt")),
                Field(
                    DimFieldKind.Choice,
                    "Fill colour",
                    "DIMTFILL",
                    () =>
                        Whole("Dimtfill") == 0 ? "None"
                        : Whole("Dimtfill") == 1 ? (object)"Background"
                        : Get("Dimtfillclr"),
                    v =>
                    {
                        Put("Dimtfill", v is string word ? (word == "Background" ? 1 : 0) : 2);

                        if (v is DxfColor colour)
                        {
                            Put("Dimtfillclr", colour);
                        }
                    },
                    C("None", "None", "Background", "Background").Concat(Colours("Dimtfillclr")).ToList()
                ),
                Plain(DimFieldKind.Number, "Text height", "Dimtxt"),
                Plain(DimFieldKind.Number, "Fraction height scale", "Dimtfac", null, () => Fractions),
                Field(
                    DimFieldKind.Flag,
                    "Draw frame around text",
                    "DIMGAP",
                    () => Real("Dimgap") < 0.0,
                    v => Put("Dimgap", (bool)v ? -Math.Abs(Real("Dimgap")) : Math.Abs(Real("Dimgap"))),
                    null,
                    null,
                    "DIMGAP<0"
                ),
                Header("Text placement"),
                Plain(
                    DimFieldKind.Choice,
                    "Vertical",
                    "Dimtad",
                    C("Centred", 0, "Above", 1, "Outside", 2, "JIS", 3, "Below", 4)
                ),
                Plain(
                    DimFieldKind.Choice,
                    "Horizontal",
                    "Dimjust",
                    C("Centred", 0, "At ext line 1", 1, "At ext line 2", 2, "Over ext line 1", 3, "Over ext line 2", 4)
                ),
                Plain(
                    DimFieldKind.Choice,
                    "View direction",
                    "Dimtxtdirection",
                    C("Left to right", false, "Right to left", true)
                ),
                Field(
                    DimFieldKind.Number,
                    "Offset from dim line",
                    "DIMGAP",
                    () => Math.Abs(Real("Dimgap")),
                    v => Put("Dimgap", Real("Dimgap") < 0.0 ? -(double)v : (double)v)
                ),
                Header("Text alignment"),
                Field(
                    DimFieldKind.Choice,
                    "Alignment",
                    "DIMTIH",
                    () =>
                        Flag("Dimtih") ? "Horizontal"
                        : Flag("Dimtoh") ? "ISO standard"
                        : "Aligned with dimension line",
                    v =>
                    {
                        Put("Dimtih", (string)v == "Horizontal");
                        Put("Dimtoh", (string)v != "Aligned with dimension line");
                    },
                    C(
                        "Horizontal",
                        "Horizontal",
                        "Aligned with dimension line",
                        "Aligned with dimension line",
                        "ISO standard",
                        "ISO standard"
                    ),
                    null,
                    "DIMTIH · DIMTOH"
                ),
            };

            _tabs["Fit"] = new[]
            {
                Header("Fit options"),
                Plain(
                    DimFieldKind.Choice,
                    "When space is short, move",
                    "Dimatfit",
                    C(
                        "Either the text or the arrows, whichever fits best",
                        3,
                        "The arrows",
                        1,
                        "The text",
                        2,
                        "Both the text and the arrows",
                        0
                    )
                ),
                Header("Text placement"),
                Plain(
                    DimFieldKind.Choice,
                    "When text is not at the default position",
                    "Dimtmove",
                    C(
                        "Beside the dimension line",
                        0,
                        "Over the dimension line, with a leader",
                        1,
                        "Over the dimension line, without a leader",
                        2
                    )
                ),
                Header("Scale for dimension features"),
                Plain(DimFieldKind.Number, "Overall scale", "Dimscale"),
                Header("Fine tuning"),
                Plain(DimFieldKind.Flag, "Place text manually", "Dimupt"),
                Plain(DimFieldKind.Flag, "Draw dim line between ext lines", "Dimtofl"),
            };

            _tabs["Primary units"] = new[]
            {
                Header("Linear dimensions"),
                Plain(DimFieldKind.Choice, "Unit format", "Dimlunit", Units()),
                Plain(DimFieldKind.Choice, "Precision", "Dimdec", Precision()),
                Plain(
                    DimFieldKind.Choice,
                    "Fraction format",
                    "Dimfrac",
                    C("Horizontal", 0, "Diagonal", 1, "Not stacked", 2),
                    () => Fractions
                ),
                Plain(
                    DimFieldKind.Choice,
                    "Decimal separator",
                    "Dimdsep",
                    C(". (Period)", '.', ", (Comma)", ',', "  (Space)", ' ')
                ),
                Plain(DimFieldKind.Number, "Round off", "Dimrnd"),
                Affix("Dimpost"),
                Plain(DimFieldKind.Number, "Measurement scale factor", "Dimlfac"),
                Zeros("Dimzin", false),
                Header("Angular dimensions"),
                Plain(
                    DimFieldKind.Choice,
                    "Units format",
                    "Dimaunit",
                    C("Decimal degrees", 0, "Degrees minutes seconds", 1, "Gradians", 2, "Radians", 3)
                ),
                Plain(DimFieldKind.Choice, "Precision", "Dimadec", Precision()),
                Zeros("Dimazin", true),
            };

            Func<bool> alternate = () => Flag("Dimalt");
            _tabs["Alternate units"] = new[]
            {
                Header("Alternate units"),
                Plain(DimFieldKind.Flag, "Display alternate units", "Dimalt"),
                Plain(DimFieldKind.Choice, "Unit format", "Dimaltu", Units(), alternate),
                Plain(DimFieldKind.Choice, "Precision", "Dimaltd", Precision(), alternate),
                Plain(DimFieldKind.Number, "Multiplier for alt units", "Dimaltf", null, alternate),
                Plain(DimFieldKind.Number, "Round distances to", "Dimaltrnd", null, alternate),
                Affix("Dimapost", alternate),
                Zeros("Dimaltz", false, alternate),
                Header("Placement"),
                Field(
                    DimFieldKind.Choice,
                    "Alternate units shown",
                    "DIMPOST",
                    () => Words("Dimpost").EndsWith(Below, StringComparison.Ordinal) ? "Below" : "After",
                    v =>
                    {
                        string post = Unmarked(Words("Dimpost"));
                        Put("Dimpost", (string)v == "Below" ? post + Below : post);
                    },
                    C("After primary value", "After", "Below primary value", "Below"),
                    alternate,
                    "DIMPOST \\X"
                ),
            };

            Func<bool> tolerance = () => Method != "None";
            _tabs["Tolerances"] = new[]
            {
                Header("Tolerance format"),
                Field(
                    DimFieldKind.Choice,
                    "Method",
                    "DIMTOL",
                    () => Method,
                    v =>
                    {
                        string method = (string)v;
                        Put("Dimlim", method == "Limits");
                        Put("Dimtol", method == "Symmetrical" || method == "Deviation");

                        if (method == "Symmetrical")
                        {
                            Put("Dimtm", Real("Dimtp"));
                        }
                    },
                    C("None", "None", "Symmetrical", "Symmetrical", "Deviation", "Deviation", "Limits", "Limits"),
                    null,
                    "DIMTOL · DIMLIM"
                ),
                Plain(DimFieldKind.Choice, "Precision", "Dimtdec", Precision(), tolerance),
                Field(
                    DimFieldKind.Number,
                    "Upper value",
                    "DIMTP",
                    () => Get("Dimtp"),
                    v =>
                    {
                        bool symmetrical = Method == "Symmetrical";
                        Put("Dimtp", v);

                        if (symmetrical)
                        {
                            Put("Dimtm", v);
                        }
                    },
                    null,
                    tolerance
                ),
                Plain(DimFieldKind.Number, "Lower value", "Dimtm", null, () =>
                    Method == "Deviation" || Method == "Limits"
                ),
                Plain(DimFieldKind.Number, "Scaling for height", "Dimtfac", null, tolerance),
                Plain(
                    DimFieldKind.Choice,
                    "Vertical position",
                    "Dimtolj",
                    C("Bottom", 0, "Middle", 1, "Top", 2),
                    tolerance
                ),
                Zeros("Dimtzin", false, tolerance),
            };
        }

        /// <summary>The description of a tab, in the design's words, from the variables set.</summary>
        private string Describe(string tab)
        {
            string name = StyleName + " · ";

            switch (tab)
            {
                case "Lines":
                    return name
                        + "dimension lines "
                        + Colour("Dimclrd")
                        + ", baseline spacing "
                        + N(Real("Dimdli"))
                        + " · extension lines "
                        + N(Real("Dimexe"))
                        + " beyond, "
                        + N(Real("Dimexo"))
                        + " offset.";
                case "Symbols and arrows":
                    double centre = Real("Dimcen");
                    string arrow = CadDimStyleEditor
                        .Text(Flag("Dimsah") ? Get("Dimblk1") : Get("Dimblk"))
                        .ToLowerInvariant();
                    string symbol =
                        Whole("Dimarcsym") == 0 ? "before the text"
                        : Whole("Dimarcsym") == 1 ? "above the text"
                        : "none";
                    return name
                        + "arrows "
                        + arrow
                        + " "
                        + N(Real("Dimasz"))
                        + " · "
                        + (
                            centre > 0.0 ? "centre mark " + N(centre)
                            : centre < 0.0 ? "centre line " + N(-centre)
                            : "no centre mark"
                        )
                        + " · arc length symbol "
                        + symbol
                        + ".";
                case "Text":
                    string[] vertical =
                    {
                        "centred on the line",
                        "above the line",
                        "outside the line",
                        "placed as JIS",
                        "below the line",
                    };
                    string[] horizontal =
                    {
                        "centred",
                        "at extension line 1",
                        "at extension line 2",
                        "over extension line 1",
                        "over extension line 2",
                    };
                    string aligned =
                        Flag("Dimtih") ? "horizontal"
                        : Flag("Dimtoh") ? "aligned as ISO"
                        : "aligned with the line";
                    return name
                        + "text "
                        + N(Real("Dimtxt"))
                        + " in "
                        + (Words("Dimtxsty").Length > 0 ? Words("Dimtxsty") : "Standard")
                        + ", "
                        + Pick(vertical, Whole("Dimtad"))
                        + ", "
                        + Pick(horizontal, Whole("Dimjust"))
                        + ", offset "
                        + N(Math.Abs(Real("Dimgap")))
                        + ", "
                        + aligned
                        + ".";
                case "Fit":
                    string[] moves =
                    {
                        "text and arrows moved together",
                        "arrows moved",
                        "text moved",
                        "text or arrows moved, whichever fits best",
                    };
                    return name
                        + Pick(moves, Whole("Dimatfit"))
                        + " · overall scale "
                        + N(Real("Dimscale"))
                        + " · line "
                        + (Flag("Dimtofl") ? "drawn" : "not drawn")
                        + " between the extension lines.";
                case "Primary units":
                    string separator = Get("Dimdsep") is char c
                        ? (
                            c == ',' ? "comma"
                            : c == ' ' ? "space"
                            : "point"
                        )
                        : "point";
                    string post = Unmarked(Words("Dimpost"));
                    string[] angles = { "decimal degrees", "degrees, minutes and seconds", "gradians", "radians" };
                    return name
                        + UnitName(Whole("Dimlunit"))
                        + ", "
                        + Whole("Dimdec").ToString(CultureInfo.InvariantCulture)
                        + " places, "
                        + separator
                        + " separator · "
                        + Affixes(post)
                        + " · angles in "
                        + Pick(angles, Whole("Dimaunit"))
                        + ", "
                        + Whole("Dimadec").ToString(CultureInfo.InvariantCulture)
                        + " places.";
                case "Alternate units":
                    if (!Flag("Dimalt"))
                    {
                        return name + "alternate units off.";
                    }

                    return name
                        + "alternate units on: "
                        + UnitName(Whole("Dimaltu"))
                        + ", "
                        + Whole("Dimaltd").ToString(CultureInfo.InvariantCulture)
                        + " places, multiplier "
                        + N(Real("Dimaltf"))
                        + ", shown "
                        + (Words("Dimpost").EndsWith(Below, StringComparison.Ordinal) ? "below" : "after")
                        + " the primary value.";
                default:
                    switch (Method)
                    {
                        case "None":
                            return name + "no tolerance.";
                        case "Symmetrical":
                            return name
                                + "symmetrical tolerance, plus or minus "
                                + N(Real("Dimtp"))
                                + ", "
                                + Whole("Dimtdec").ToString(CultureInfo.InvariantCulture)
                                + " places, text height scale "
                                + N(Real("Dimtfac"))
                                + ".";
                        case "Limits":
                            return name
                                + "limits, upper "
                                + N(Real("Dimtp"))
                                + " and lower "
                                + N(Real("Dimtm"))
                                + ", "
                                + Whole("Dimtdec").ToString(CultureInfo.InvariantCulture)
                                + " places, text height scale "
                                + N(Real("Dimtfac"))
                                + ".";
                        default:
                            return name
                                + "deviation tolerance, plus "
                                + N(Real("Dimtp"))
                                + " and minus "
                                + N(Real("Dimtm"))
                                + ", "
                                + Whole("Dimtdec").ToString(CultureInfo.InvariantCulture)
                                + " places, text height scale "
                                + N(Real("Dimtfac"))
                                + ".";
                    }
            }
        }

        private string Colour(string name)
        {
            DimChoice choice = Colours(name)
                .FirstOrDefault(c => CadDimStyleEditor.Text(c.Value) == CadDimStyleEditor.Text(Get(name)));
            return choice?.Label ?? CadDimStyleEditor.Text(Get(name));
        }

        /// <summary>DIMPOST in words: "suffix mm" for "&lt;&gt; mm", "prefix ~ and suffix mm" for "~&lt;&gt; mm", "no prefix or suffix" for none.</summary>
        private static string Affixes(string post)
        {
            int at = post.IndexOf("<>", StringComparison.Ordinal);
            string prefix = (at < 0 ? string.Empty : post.Substring(0, at)).Trim();
            string suffix = (at < 0 ? post : post.Substring(at + 2)).Trim();

            if (prefix.Length == 0)
            {
                return suffix.Length == 0 ? "no prefix or suffix" : "suffix " + suffix;
            }

            return suffix.Length == 0 ? "prefix " + prefix : "prefix " + prefix + " and suffix " + suffix;
        }

        private static string UnitName(int unit) =>
            Units().FirstOrDefault(u => (int)u.Value == unit)?.Label.ToLowerInvariant() ?? "decimal";

        private static string Pick(string[] words, int index) =>
            index >= 0 && index < words.Length ? words[index] : words[0];

        private static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>A tab's chip of the dialogs of a dimension style: its name, checked while it is shown.</summary>
    public sealed class DimTabViewModel : ObservableObject
    {
        private readonly DimStyleEditViewModel _dialog;

        internal DimTabViewModel(DimStyleEditViewModel dialog, string name)
        {
            _dialog = dialog;
            Name = name;
        }

        /// <summary>Gets the tab's name, as "Symbols and arrows".</summary>
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

    /// <summary>The notices of the dimension style dialogs.</summary>
    internal static class ManagerNotices
    {
        /// <summary>The notice of the styles a model drawing makes.</summary>
        internal const string ModelStyle =
            "A style named GH_MODEL_… is made by the model drawing. Changing it is a hand edit that an update keeps; its settings remake the rest.";

        /// <summary>The notice of Override: what OK writes, and into how many dimensions.</summary>
        internal static string Override(int count) =>
            count == 0
                ? "Select dimensions before Override: OK writes the changes as overrides into each dimension selected."
                : "OK writes the changes as overrides into the "
                    + count.ToString(CultureInfo.InvariantCulture)
                    + (count == 1 ? " dimension" : " dimensions")
                    + " selected; the style is kept.";
    }
}
