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
    /// AutoCAD 2021's Multiline Style dialog over MLSTYLE's request: the styles of the drawing, Set Current, New, Modify,
    /// Rename, Delete, Load and Save, the description and a preview of the style picked. The buttons act at once, as
    /// AutoCAD's do, each an edit of MLSTYLE's one undo step; Set Current is taken when the dialog closes with OK. A button
    /// refused says why in <see cref="Message"/>.
    /// </summary>
    public sealed class MlineStyleViewModel : ObservableObject
    {
        private readonly CadMlineStyleRequest _request;
        private string _selected;
        private string _current;
        private string _message = string.Empty;
        private string _renameText = string.Empty;
        private bool _isRenaming;
        private ImageSource _preview;

        /// <summary>Makes the dialog over MLSTYLE's request.</summary>
        /// <param name="request">The request.</param>
        public MlineStyleViewModel(CadMlineStyleRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _current = request.Current;
            Names = new ObservableCollection<string>(request.Names);
            _selected = Names.FirstOrDefault(n => string.Equals(n, _current, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Gets the names of the styles of the drawing, sorted by their letters.</summary>
        public ObservableCollection<string> Names { get; }

        /// <summary>Gets the style made current on OK, as the line above the list says it: "STANDARD".</summary>
        public string CurrentName => _current;

        /// <summary>Gets or sets the style picked in the list.</summary>
        public string Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    _preview = null;
                    IsRenaming = false;
                    Message = string.Empty;
                    Raise(nameof(Description));
                    Raise(nameof(PreviewCaption));
                    Raise(nameof(Preview));
                    Raise(nameof(CanDelete));
                    Raise(nameof(CanRename));
                }
            }
        }

        /// <summary>Gets the description of the style picked.</summary>
        public string Description => SelectedSettings?.Description ?? string.Empty;

        /// <summary>Gets the line above the preview: "Preview of: WALL-240".</summary>
        public string PreviewCaption => "Preview of: " + (_selected ?? string.Empty);

        /// <summary>Gets the preview of the style picked: a multiline of it with a bend, drawn as MLINE draws one.</summary>
        public ImageSource Preview
        {
            get
            {
                DxfMlineStyleSettings settings = SelectedSettings;
                return _preview ?? (settings == null ? null : _preview = CadStylePreview.Mline(settings, 300, 120));
            }
        }

        /// <summary>Gets whether Delete may delete the style picked: not STANDARD, current or in use.</summary>
        public bool CanDelete =>
            _selected != null
            && _request.WhyNotDeleted(_selected) == null
            && !string.Equals(_selected, _current, StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets whether Rename may rename the style picked: any but STANDARD.</summary>
        public bool CanRename =>
            _selected != null && !string.Equals(_selected, "STANDARD", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets whether the name of the style picked is being edited in the list.</summary>
        public bool IsRenaming
        {
            get => _isRenaming;
            private set => Set(ref _isRenaming, value);
        }

        /// <summary>Gets or sets the name typed while renaming.</summary>
        public string RenameText
        {
            get => _renameText;
            set => Set(ref _renameText, value ?? string.Empty);
        }

        /// <summary>Gets why the last button was refused; empty where it was not.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets or sets how the window asks the name of a new style and the one it starts with: true on Continue.</summary>
        public Func<MlineStyleNewViewModel, bool> AskNewStyle { get; set; }

        /// <summary>Gets or sets how the window shows New or Modify Multiline Style: true on OK.</summary>
        public Func<MlineStyleEditViewModel, bool> EditStyle { get; set; }

        /// <summary>Gets or sets how the window asks a file of styles to load from, and the style of it: true on OK.</summary>
        public Func<MlineStyleLoadViewModel, bool> AskLoad { get; set; }

        /// <summary>Gets or sets how the window asks the file to save a style to; null where cancelled.</summary>
        public Func<string, string> AskSaveFile { get; set; }

        private DxfMlineStyleSettings SelectedSettings => _selected == null ? null : _request.SettingsOf(_selected);

        /// <summary>Set Current: the style picked is made current when the dialog closes with OK.</summary>
        public void SetCurrent()
        {
            if (_selected != null)
            {
                _current = _selected;
                Raise(nameof(CurrentName));
                Raise(nameof(CanDelete));
            }
        }

        /// <summary>New: a name and the style it starts with asked, then New Multiline Style; on OK the style is added and picked.</summary>
        /// <returns>True where a style was added.</returns>
        public bool New()
        {
            if (AskNewStyle == null || EditStyle == null)
            {
                return false;
            }

            var naming = new MlineStyleNewViewModel(_request, Names.ToList(), _selected ?? _current);

            if (!AskNewStyle(naming))
            {
                return false;
            }

            var editing = new MlineStyleEditViewModel(
                "New Multiline Style: " + naming.Name.Trim().ToUpperInvariant(),
                _request.SettingsOf(naming.StartWith) ?? new DxfMlineStyleSettings(),
                false,
                Linetypes()
            );

            while (EditStyle(editing))
            {
                string refusal = _request.Add(naming.Name, editing.Settings);

                if (refusal == null)
                {
                    Refresh(naming.Name.Trim().ToUpperInvariant());
                    return true;
                }

                editing.Refuse(refusal);
            }

            return false;
        }

        /// <summary>Modify: Modify Multiline Style over the style picked, all but the description greyed where it is STANDARD or in use.</summary>
        /// <returns>True where the style was changed.</returns>
        public bool Modify()
        {
            if (EditStyle == null || _selected == null)
            {
                return false;
            }

            var editing = new MlineStyleEditViewModel(
                "Modify Multiline Style: " + _selected,
                _request.SettingsOf(_selected),
                _request.IsLocked(_selected),
                Linetypes()
            );

            while (EditStyle(editing))
            {
                string refusal = _request.Modify(_selected, editing.Settings);

                if (refusal == null)
                {
                    Refresh(_selected);
                    return true;
                }

                editing.Refuse(refusal);
            }

            return false;
        }

        /// <summary>Rename: the name of the style picked is edited in the list, STANDARD refused.</summary>
        public void BeginRename()
        {
            if (!CanRename)
            {
                Message = "The STANDARD multiline style cannot be renamed.";
                return;
            }

            RenameText = _selected;
            IsRenaming = true;
        }

        /// <summary>Renames the style picked to the name typed; a name refused is said and the edit kept open.</summary>
        /// <returns>True where it was renamed.</returns>
        public bool CommitRename()
        {
            if (!_isRenaming || _selected == null)
            {
                return false;
            }

            string old = _selected;
            string refusal = _request.Rename(old, RenameText);

            if (refusal != null)
            {
                Message = refusal;
                return false;
            }

            IsRenaming = false;

            if (string.Equals(_current, old, StringComparison.OrdinalIgnoreCase))
            {
                _current = RenameText.Trim().ToUpperInvariant();
                Raise(nameof(CurrentName));
            }

            Refresh(RenameText.Trim().ToUpperInvariant());
            return true;
        }

        /// <summary>Ends the rename with the name kept.</summary>
        public void CancelRename() => IsRenaming = false;

        /// <summary>Delete: the style picked is deleted; STANDARD, the current one and one in use are refused.</summary>
        /// <returns>True where it was deleted.</returns>
        public bool Delete()
        {
            if (_selected == null)
            {
                return false;
            }

            string refusal = string.Equals(_selected, _current, StringComparison.OrdinalIgnoreCase)
                ? "The multiline style "
                    + _selected
                    + " cannot be deleted: STANDARD, the current style and one a multiline is drawn by are kept."
                : _request.Delete(_selected);

            if (refusal != null)
            {
                Message = refusal;
                return false;
            }

            Refresh(_current);
            return true;
        }

        /// <summary>Load: Load Multiline Styles asks a file and a style of it, which is added and picked.</summary>
        /// <returns>True where a style was added.</returns>
        public bool Load()
        {
            if (AskLoad == null)
            {
                return false;
            }

            var loading = new MlineStyleLoadViewModel(_request);

            if (!AskLoad(loading) || loading.Selected == null)
            {
                return false;
            }

            string refusal = _request.Load(loading.File, loading.Selected);

            if (refusal != null)
            {
                Message = refusal;
                return false;
            }

            Refresh(loading.Selected.ToUpperInvariant());
            return true;
        }

        /// <summary>Save: the style picked written to a file of styles the window asks, its other styles kept.</summary>
        /// <returns>True where it was written.</returns>
        public bool Save()
        {
            if (AskSaveFile == null || _selected == null)
            {
                return false;
            }

            string file = AskSaveFile("acad.mln");

            if (string.IsNullOrEmpty(file))
            {
                return false;
            }

            string refusal = _request.Save(_selected, file);
            Message = refusal ?? "Multiline style " + _selected + " saved to " + System.IO.Path.GetFileName(file) + ".";
            return refusal == null;
        }

        /// <summary>OK: the style set current is made current once the dialog closes.</summary>
        public void Accept()
        {
            _request.Current = _current;
            _request.IsAccepted = true;
        }

        private IReadOnlyList<string> Linetypes()
        {
            var names = new List<string> { "BYLAYER", "BYBLOCK" };
            names.AddRange(
                _request
                    .Document.Database.LinetypeTable.Select(l => l.Name)
                    .Where(n =>
                        !string.Equals(n, "ByLayer", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(n, "ByBlock", StringComparison.OrdinalIgnoreCase)
                    )
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(n => n, StringComparer.Ordinal)
            );
            return names;
        }

        private void Refresh(string pick)
        {
            Names.Clear();

            foreach (string name in _request.Names)
            {
                Names.Add(name);
            }

            _selected = null;
            Selected = Names.FirstOrDefault(n => string.Equals(n, pick, StringComparison.OrdinalIgnoreCase))
                ?? Names.FirstOrDefault();
            Message = string.Empty;
        }
    }

    /// <summary>Create New Multiline Style: the new style's name and the style it starts with.</summary>
    public sealed class MlineStyleNewViewModel : ObservableObject
    {
        private readonly CadMlineStyleRequest _request;
        private string _name = string.Empty;
        private string _startWith;
        private string _message = string.Empty;

        internal MlineStyleNewViewModel(CadMlineStyleRequest request, IReadOnlyList<string> styles, string startWith)
        {
            _request = request;
            Styles = styles;
            _startWith = startWith ?? styles.FirstOrDefault();
        }

        /// <summary>Gets the styles one may start with.</summary>
        public IReadOnlyList<string> Styles { get; }

        /// <summary>Gets or sets the name typed.</summary>
        public string Name
        {
            get => _name;
            set => Set(ref _name, value ?? string.Empty);
        }

        /// <summary>Gets or sets the style whose elements, caps and fill the new one starts with.</summary>
        public string StartWith
        {
            get => _startWith;
            set => Set(ref _startWith, value);
        }

        /// <summary>Gets why the name was refused; empty where it was not.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Continue: true where the name is one a new style may take, else the reason is said.</summary>
        /// <returns>Whether to go on to New Multiline Style.</returns>
        public bool TryContinue()
        {
            Message = _request.WhyNotNamed(_name) ?? string.Empty;
            return Message.Length == 0;
        }
    }

    /// <summary>Load Multiline Styles: a file of styles, acad.mln to start with, and the style of it to load.</summary>
    public sealed class MlineStyleLoadViewModel : ObservableObject
    {
        private readonly CadMlineStyleRequest _request;
        private string _file = string.Empty;
        private string _selected;
        private string _message = string.Empty;
        private IReadOnlyList<string> _names = Array.Empty<string>();

        internal MlineStyleLoadViewModel(CadMlineStyleRequest request)
        {
            _request = request;
            File = "acad.mln";
        }

        /// <summary>Gets or sets the file; its styles are listed as it is set.</summary>
        public string File
        {
            get => _file;
            set
            {
                if (Set(ref _file, value ?? string.Empty))
                {
                    Names = _request.NamesInFile(_file, out string refusal);
                    Message = refusal ?? string.Empty;
                    Selected = Names.FirstOrDefault();
                }
            }
        }

        /// <summary>Gets the styles of the file, in its order.</summary>
        public IReadOnlyList<string> Names
        {
            get => _names;
            private set => Set(ref _names, value);
        }

        /// <summary>Gets or sets the style picked.</summary>
        public string Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        /// <summary>Gets why the file was not read; empty where it was.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets or sets how the window asks a file of styles; null where cancelled.</summary>
        public Func<string> BrowseFile { get; set; }

        /// <summary>File…: the file the window asks for.</summary>
        public void Browse()
        {
            string file = BrowseFile?.Invoke();

            if (!string.IsNullOrEmpty(file))
            {
                File = file;
            }
        }
    }

    /// <summary>A row of the Elements list of New or Modify Multiline Style: offset, colour and linetype.</summary>
    public sealed class MlineElementRow : ObservableObject
    {
        private double _offset;
        private string _color;
        private string _linetype;

        internal MlineElementRow(double offset, string color, string linetype)
        {
            _offset = offset;
            _color = color;
            _linetype = linetype;
        }

        /// <summary>Gets the offset.</summary>
        public double Offset
        {
            get => _offset;
            internal set
            {
                if (Set(ref _offset, value))
                {
                    Raise(nameof(OffsetText));
                }
            }
        }

        /// <summary>Gets the offset as the list writes it.</summary>
        public string OffsetText => _offset.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Gets the colour, as the list writes it: BYLAYER, BYBLOCK or a name or an index.</summary>
        public string Color
        {
            get => _color;
            internal set => Set(ref _color, value);
        }

        /// <summary>Gets the linetype.</summary>
        public string Linetype
        {
            get => _linetype;
            internal set => Set(ref _linetype, value);
        }
    }

    /// <summary>
    /// New or Modify Multiline Style: the description, the caps at the start and end (line, outer arc, inner arcs, angle),
    /// the fill, whether joints are shown, and the elements with their offset, colour and linetype, Add and Delete. Where
    /// the style is STANDARD or a multiline is drawn by it, only the description may be changed (<see cref="IsEditable"/>).
    /// </summary>
    public sealed class MlineStyleEditViewModel : ObservableObject
    {
        /// <summary>The colours offered for an element or the fill, by layer, by block and the seven named ones.</summary>
        private static readonly string[] ColorNames =
        {
            "BYLAYER",
            "BYBLOCK",
            "Red",
            "Yellow",
            "Green",
            "Cyan",
            "Blue",
            "Magenta",
            "White",
        };

        private readonly DxfMlineStyleSettings _settings;
        private MlineElementRow _element;
        private string _startAngle;
        private string _endAngle;
        private string _offset = "0";
        private string _message = string.Empty;

        /// <summary>Makes the page over the settings of a style.</summary>
        /// <param name="title">The title: "New Multiline Style: WALL" or "Modify Multiline Style: WALL".</param>
        /// <param name="settings">The settings it starts from, a copy, which it changes.</param>
        /// <param name="locked">Whether only the description may be changed.</param>
        /// <param name="linetypes">The linetypes offered.</param>
        public MlineStyleEditViewModel(
            string title,
            DxfMlineStyleSettings settings,
            bool locked,
            IReadOnlyList<string> linetypes
        )
        {
            Title = title ?? string.Empty;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            IsEditable = !locked;
            Linetypes = linetypes ?? new[] { "BYLAYER", "BYBLOCK" };
            _startAngle = Degrees(settings.StartAngle);
            _endAngle = Degrees(settings.EndAngle);
            Elements = new ObservableCollection<MlineElementRow>(
                settings
                    .Elements.OrderByDescending(e => e.Offset)
                    .Select(e => new MlineElementRow(e.Offset, ColorName(e.Color), e.Linetype))
            );
            Element = Elements.FirstOrDefault();
        }

        /// <summary>Gets the title.</summary>
        public string Title { get; }

        /// <summary>Gets whether more than the description may be changed.</summary>
        public bool IsEditable { get; }

        /// <summary>Gets the colours offered: BYLAYER, BYBLOCK and the seven named ones.</summary>
        public IReadOnlyList<string> Colors => ColorNames;

        /// <summary>Gets the fill colours offered: None, then the colours.</summary>
        public IReadOnlyList<string> FillColors { get; } = new[] { "None" }.Concat(ColorNames).ToList();

        /// <summary>Gets the linetypes offered.</summary>
        public IReadOnlyList<string> Linetypes { get; }

        /// <summary>Gets or sets the description.</summary>
        public string Description
        {
            get => _settings.Description;
            set => _settings.Description = value ?? string.Empty;
        }

        /// <summary>Gets or sets whether the start is closed by a line.</summary>
        public bool StartLine
        {
            get => _settings.StartSquareCap;
            set => _settings.StartSquareCap = value;
        }

        /// <summary>Gets or sets whether the end is closed by a line.</summary>
        public bool EndLine
        {
            get => _settings.EndSquareCap;
            set => _settings.EndSquareCap = value;
        }

        /// <summary>Gets or sets whether the outer elements are joined by an arc at the start.</summary>
        public bool StartOuterArc
        {
            get => _settings.StartRoundCap;
            set => _settings.StartRoundCap = value;
        }

        /// <summary>Gets or sets whether the outer elements are joined by an arc at the end.</summary>
        public bool EndOuterArc
        {
            get => _settings.EndRoundCap;
            set => _settings.EndRoundCap = value;
        }

        /// <summary>Gets or sets whether the inner elements are joined by arcs at the start.</summary>
        public bool StartInnerArcs
        {
            get => _settings.StartInnerArcs;
            set => _settings.StartInnerArcs = value;
        }

        /// <summary>Gets or sets whether the inner elements are joined by arcs at the end.</summary>
        public bool EndInnerArcs
        {
            get => _settings.EndInnerArcs;
            set => _settings.EndInnerArcs = value;
        }

        /// <summary>Gets or sets the angle of the start in degrees, 10 to 170, as typed.</summary>
        public string StartAngle
        {
            get => _startAngle;
            set => Set(ref _startAngle, value ?? string.Empty);
        }

        /// <summary>Gets or sets the angle of the end in degrees, 10 to 170, as typed.</summary>
        public string EndAngle
        {
            get => _endAngle;
            set => Set(ref _endAngle, value ?? string.Empty);
        }

        /// <summary>Gets or sets the fill colour: None, or a colour of <see cref="Colors"/>.</summary>
        public string FillColor
        {
            get => _settings.Filled ? ColorName(_settings.FillColor) : "None";
            set
            {
                _settings.Filled = value != null && value != "None";

                if (_settings.Filled)
                {
                    _settings.FillColor = ColorOf(value);
                }

                Raise(nameof(FillColor));
            }
        }

        /// <summary>Gets or sets whether a line is drawn across each joint.</summary>
        public bool DisplayJoints
        {
            get => _settings.ShowMiters;
            set => _settings.ShowMiters = value;
        }

        /// <summary>Gets the elements, the greatest offset first.</summary>
        public ObservableCollection<MlineElementRow> Elements { get; }

        /// <summary>Gets or sets the element picked, whose offset, colour and linetype are shown below the list.</summary>
        public MlineElementRow Element
        {
            get => _element;
            set
            {
                if (Set(ref _element, value))
                {
                    _offset = value?.OffsetText ?? "0";
                    Raise(nameof(Offset));
                    Raise(nameof(ElementColor));
                    Raise(nameof(ElementLinetype));
                }
            }
        }

        /// <summary>Gets or sets the offset of the element picked, as typed; a number moves it in the list.</summary>
        public string Offset
        {
            get => _offset;
            set
            {
                Set(ref _offset, value ?? string.Empty);

                if (
                    _element != null
                    && double.TryParse(_offset, NumberStyles.Float, CultureInfo.InvariantCulture, out double offset)
                )
                {
                    _element.Offset = offset;
                    Sort();
                }
            }
        }

        /// <summary>Gets or sets the colour of the element picked.</summary>
        public string ElementColor
        {
            get => _element?.Color;
            set
            {
                if (_element != null && value != null)
                {
                    _element.Color = value;
                }
            }
        }

        /// <summary>Gets or sets the linetype of the element picked.</summary>
        public string ElementLinetype
        {
            get => _element?.Linetype;
            set
            {
                if (_element != null && value != null)
                {
                    _element.Linetype = value;
                }
            }
        }

        /// <summary>Gets why OK was refused; empty where it was not.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets the settings as the page leaves them; read once <see cref="TryAccept"/> has said true.</summary>
        public DxfMlineStyleSettings Settings => _settings;

        /// <summary>Add: an element at offset nought, by layer, picked; at most 16.</summary>
        /// <returns>True where one was added.</returns>
        public bool Add()
        {
            if (Elements.Count >= 16)
            {
                Message = "A multiline style has at most 16 elements.";
                return false;
            }

            var row = new MlineElementRow(0.0, "BYLAYER", "BYLAYER");
            Elements.Add(row);
            Sort();
            Element = row;
            return true;
        }

        /// <summary>Delete: the element picked; one is always kept.</summary>
        /// <returns>True where it was deleted.</returns>
        public bool Delete()
        {
            if (_element == null)
            {
                return false;
            }

            if (Elements.Count == 1)
            {
                Message = "A multiline style has at least one element.";
                return false;
            }

            int at = Elements.IndexOf(_element);
            Elements.Remove(_element);
            Element = Elements[Math.Min(at, Elements.Count - 1)];
            return true;
        }

        /// <summary>OK: the angles checked (10 to 170) and the elements written into the settings; false with the reason said.</summary>
        /// <returns>Whether the page may close.</returns>
        public bool TryAccept()
        {
            if (!IsEditable)
            {
                Message = string.Empty;
                return true;
            }

            if (!Angle(_startAngle, out double start) || !Angle(_endAngle, out double end))
            {
                Message = "An angle is from 10 to 170 degrees.";
                return false;
            }

            _settings.StartAngle = start * Math.PI / 180.0;
            _settings.EndAngle = end * Math.PI / 180.0;
            _settings.Elements.Clear();

            foreach (MlineElementRow row in Elements)
            {
                _settings.Elements.Add(new DxfMlineStyleElement(row.Offset, ColorOf(row.Color), row.Linetype));
            }

            Message = string.Empty;
            return true;
        }

        /// <summary>Says why MLSTYLE refused the settings, the page then shown again.</summary>
        /// <param name="reason">The reason.</param>
        internal void Refuse(string reason) => Message = reason;

        private static bool Angle(string text, out double degrees) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees)
            && degrees >= 10.0
            && degrees <= 170.0;

        private static string Degrees(double radians) =>
            (radians * 180.0 / Math.PI).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>The name a colour is listed by: BYLAYER, BYBLOCK, Red to White, or its index.</summary>
        internal static string ColorName(DxfColor color)
        {
            if (color == null || color.IsByLayer)
            {
                return "BYLAYER";
            }

            if (color.IsByBlock)
            {
                return "BYBLOCK";
            }

            int index = color.ColorIndex;
            return index >= 1 && index <= 7 ? ColorNames[index + 1] : index.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The colour a name of the list, or an index, stands for.</summary>
        internal static DxfColor ColorOf(string name)
        {
            int listed = Array.IndexOf(ColorNames, name);

            if (listed == 0 || name == null)
            {
                return DxfColor.ByLayer;
            }

            if (listed == 1)
            {
                return DxfColor.ByBlock;
            }

            short index =
                listed > 1 ? (short)(listed - 1)
                : short.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out short typed) ? typed
                : (short)256;
            return index >= 1 && index <= 255 ? DxfColor.FromColorIndex(DxfColorMethod.ByAci, index) : DxfColor.ByLayer;
        }

        private void Sort()
        {
            List<MlineElementRow> sorted = Elements.OrderByDescending(e => e.Offset).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                int at = Elements.IndexOf(sorted[i]);

                if (at != i)
                {
                    Elements.Move(at, i);
                }
            }
        }
    }

    /// <summary>
    /// AutoCAD 2021's Multiline Edit Tools dialog over MLEDIT's request: the twelve tools as pictures in its rows of four; a
    /// tool picked closes the dialog and MLEDIT asks its multilines.
    /// </summary>
    public sealed class MlineEditToolsViewModel
    {
        // The pictures of the tools, in the order of CadMlineEditRequest.Tools, on a square of 80.
        private static readonly string[] Pictures =
        {
            "M10 30 H70 M10 50 H70 M30 10 V30 M50 10 V30 M30 50 V70 M50 50 V70",
            "M10 30 H70 M10 50 H70 M30 50 V70 M50 50 V70",
            "M30 10 V50 H70 M50 10 V30 H70",
            "M10 30 H35 M45 30 H70 M10 50 H70",
            "M10 30 H30 V10 M50 10 V30 H70 M10 50 H30 V70 M50 70 V50 H70",
            "M10 30 H70 M10 50 H30 V70 M50 70 V50 H70",
            "M10 30 H70 M10 50 H70 M37 40 A3 3 0 1 0 43 40 A3 3 0 1 0 37 40",
            "M10 30 H35 M45 30 H70 M10 50 H35 M45 50 H70",
            "M10 30 H70 M10 50 H70 M30 10 V70 M50 10 V70",
            "M10 30 H70 M10 50 H30 V70 M50 70 V50 H70 M30 30 V50 M50 30 V50",
            "M10 50 L40 30 L70 50 M10 60 L40 40 L70 60",
            "M10 30 H70 M10 50 H70 M40 22 V38",
        };

        private readonly CadMlineEditRequest _request;

        /// <summary>Makes the dialog over MLEDIT's request.</summary>
        /// <param name="request">The request.</param>
        public MlineEditToolsViewModel(CadMlineEditRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            Tools = CadMlineEditRequest
                .Tools.Select(
                    (t, i) => new MlineEditTool(t.Key, t.Value, System.Windows.Media.Geometry.Parse(Pictures[i]))
                )
                .ToList();
        }

        /// <summary>Gets the tools, row by row, four to a row.</summary>
        public IReadOnlyList<MlineEditTool> Tools { get; }

        /// <summary>Picks a tool by its letters, which MLEDIT then asks the multilines of.</summary>
        /// <param name="code">The letters, as CC.</param>
        public void Pick(string code) => _request.Tool = Tools.Any(t => t.Code == code) ? code : null;
    }

    /// <summary>A tool of Multiline Edit Tools: its letters, its name and its picture.</summary>
    public sealed class MlineEditTool
    {
        internal MlineEditTool(string code, string name, System.Windows.Media.Geometry picture)
        {
            Code = code;
            Name = name;
            picture.Freeze();
            Picture = picture;
        }

        /// <summary>Gets the letters MLEDIT takes, as CC.</summary>
        public string Code { get; }

        /// <summary>Gets the name under the picture, as Closed Cross.</summary>
        public string Name { get; }

        /// <summary>Gets the picture, on a square of 80.</summary>
        public System.Windows.Media.Geometry Picture { get; }
    }
}
