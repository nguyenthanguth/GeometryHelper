using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A style of the list of the Text Style dialog or the Dimension Style Manager: its name, whether it is current, how many
    /// texts or dimensions are drawn in it, the font of a text style and whether a model drawing makes it.
    /// </summary>
    public sealed class StyleRowViewModel : ObservableObject
    {
        internal StyleRowViewModel(string name, bool isCurrent, int uses, string detail = null)
        {
            Name = name;
            IsCurrent = isCurrent;
            Uses = uses;
            Detail = detail ?? string.Empty;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets whether the style is current.</summary>
        public bool IsCurrent { get; }

        /// <summary>Gets how many texts (or dimensions) are drawn in the style.</summary>
        public int Uses { get; }

        /// <summary>Gets what the list shows after the name: the font of a text style, as romans.shx or Arial; empty for a dimension style.</summary>
        public string Detail { get; }

        /// <summary>Gets whether a model drawing makes the style (GH_MODEL_...), marked MODEL in the list.</summary>
        public bool IsModel => CadDimStyleEditor.IsModelStyle(Name);

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>A font of the Font Name list: its name as AutoCAD lists it (romans.shx, Arial) and whether it is TrueType, which its icon shows.</summary>
    public sealed class FontItemViewModel
    {
        internal FontItemViewModel(string name, bool isTrueType)
        {
            Name = name;
            IsTrueType = isTrueType;
        }

        /// <summary>Gets the name: an SHX file with its extension, a TrueType family.</summary>
        public string Name { get; }

        /// <summary>Gets whether the font is TrueType (the TT icon), not an SHX file (the compass icon).</summary>
        public bool IsTrueType { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD's Text Style dialog (STYLE, ST): the styles of the drawing (all, or those in use), a preview of the one
    /// selected, Set Current, New, Delete and Rename, its font (the presenter's own GeometryHelper Simplex, the SHX files of the
    /// font folders and the TrueType families of the machine, a font style for TrueType, a big font for SHX), its height, and
    /// the effects upside down, backwards, vertical, width factor and oblique angle. Apply writes the style, one undo step, every text written in it drawn again;
    /// a change not applied is asked about before another style is shown or the dialog closes, as AutoCAD asks.
    /// </summary>
    public sealed class TextStyleViewModel : ObservableObject
    {
        /// <summary>The filter of every style.</summary>
        public const string AllStyles = "All styles";

        /// <summary>The filter of the styles texts are written in.</summary>
        public const string StylesInUse = "Styles in use";

        /// <summary>
        /// The name the Font Name list gives the presenter's own font, first in the list: GeometryHelper Simplex (built-in). A
        /// style given it names the file ghsimplex.shx (<see cref="CadFonts.BuiltInName"/>).
        /// </summary>
        public const string BuiltInFont = CadFonts.BuiltInTitle + " (built-in)";

        private static readonly string[] Styles4 = { "Regular", "Bold", "Italic", "Bold Italic" };

        private readonly CadDocument _document;
        private readonly List<KeyValuePair<string, bool>> _shxFiles;
        private string _filter = AllStyles;
        private StyleRowViewModel _selected;
        private string _fontName;
        private string _fontStyle = "Regular";
        private bool _useBigFont;
        private string _bigFont;
        private string _height;
        private string _widthFactor;
        private string _obliqueAngle;
        private bool _upsideDown;
        private bool _backwards;
        private bool _vertical;
        private bool _isDirty;
        private bool _loading;
        private string _notice = string.Empty;
        private ImageSource _preview;
        private CadTextStyleSettings _saved;

        /// <summary>Makes the dialog's view model over a document, the selected style the current one.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public TextStyleViewModel(CadDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _shxFiles = ShxFiles(document.Fonts.Folders.Concat(new[] { document.HasFile ? Path.GetDirectoryName(document.FileName) : null }));
            var fonts = new List<FontItemViewModel>();
            fonts.AddRange(_shxFiles.Where(f => !f.Value && !CadFonts.IsBuiltIn(f.Key)).Select(f => new FontItemViewModel(f.Key, false)));
            fonts.AddRange(CadTrueTypeFonts.Families.Select(f => new FontItemViewModel(f, true)));
            fonts.Add(new FontItemViewModel(BuiltInFont, false));
            FontNames = Ordered(fonts);
            BigFonts = _shxFiles.Where(f => f.Value).Select(f => f.Key).ToList();
            SetCurrentCommand = new DelegateCommand(_ => SetCurrent());
            NewCommand = new DelegateCommand(_ => New(AskName?.Invoke("New Text Style", NextName())));
            DeleteCommand = new DelegateCommand(_ => Delete());
            RenameCommand = new DelegateCommand(_ => Rename(AskName?.Invoke("Rename Text Style", _selected?.Name)));
            ApplyCommand = new DelegateCommand(_ => Apply(), _ => IsDirty);
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
            CloseCommand = new DelegateCommand(_ => Close());
            Reload(CadTextStyleEditor.Current(document.Database));
        }

        /// <summary>Raised when the dialog is to close.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets or sets what asks for a name (the title, the name offered), as New and Rename do; the name, or null for Cancel. Set by the window.</summary>
        public Func<string, string, string> AskName { get; set; }

        /// <summary>Gets or sets what asks whether to save a change not applied: true Yes, false No, null Cancel. Set by the window; with none, the change is dropped.</summary>
        public Func<string, bool?> AskSave { get; set; }

        /// <summary>Gets the filters of the list, as AutoCAD's: All styles, Styles in use.</summary>
        public IReadOnlyList<string> Filters { get; } = new[] { AllStyles, StylesInUse };

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
        public ObservableCollection<StyleRowViewModel> Styles { get; } = new ObservableCollection<StyleRowViewModel>();

        /// <summary>Gets the line over the list: the current text style.</summary>
        public string CurrentStyleText => "Current text style:  " + CadTextStyleEditor.Current(_document.Database);

        /// <summary>
        /// Gets or sets the style shown. Set with a change not applied, <see cref="AskSave"/> is asked first: Yes applies it, No
        /// drops it, Cancel keeps the style shown.
        /// </summary>
        public StyleRowViewModel SelectedStyle
        {
            get => _selected;
            set
            {
                if (value == null || value == _selected || !Settle())
                {
                    Raise(nameof(SelectedStyle));
                    return;
                }

                _selected = value;
                Raise(nameof(SelectedStyle));
                Raise(nameof(PreviewTitle));
                Load();
            }
        }

        /// <summary>
        /// Gets the fonts: the presenter's own first (<see cref="BuiltInFont"/>), then the SHX files of the font folders and the
        /// TrueType families, in the order of their names; a file ghsimplex.shx of a folder is passed over for the presenter's own.
        /// </summary>
        public IReadOnlyList<FontItemViewModel> FontNames { get; private set; }

        /// <summary>Gets or sets the font, as listed.</summary>
        public string FontName
        {
            get => _fontName;
            set
            {
                if (Set(ref _fontName, value))
                {
                    Raise(nameof(IsTrueType));
                    Raise(nameof(IsVerticalEnabled));
                    Raise(nameof(VerticalText));

                    if (IsTrueType)
                    {
                        _useBigFont = false;
                        _vertical = false;
                        Raise(nameof(UseBigFont));
                        Raise(nameof(Vertical));
                    }

                    Changed();
                }
            }
        }

        /// <summary>Gets whether the font is TrueType: Font Style is offered, Use Big Font and Vertical are not.</summary>
        public bool IsTrueType => FontNames.Any(f => f.IsTrueType && string.Equals(f.Name, _fontName, StringComparison.OrdinalIgnoreCase))
            || (_fontName != null && !_fontName.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(_fontName).Length > 0);

        /// <summary>Gets the font styles of a TrueType font: Regular, Bold, Italic, Bold Italic.</summary>
        public IReadOnlyList<string> FontStyles => Styles4;

        /// <summary>Gets or sets the font style of a TrueType font.</summary>
        public string FontStyle
        {
            get => _fontStyle;
            set
            {
                if (Styles4.Contains(value) && Set(ref _fontStyle, value))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets whether an SHX font uses a big font.</summary>
        public bool UseBigFont
        {
            get => _useBigFont;
            set
            {
                if (Set(ref _useBigFont, value && !IsTrueType))
                {
                    // Ticked with none chosen, the first big font of the folders is offered, as AutoCAD's list opens on one.
                    if (_useBigFont && string.IsNullOrEmpty(_bigFont) && !_loading)
                    {
                        _bigFont = BigFonts.FirstOrDefault();
                        Raise(nameof(BigFont));
                    }

                    Changed();
                }
            }
        }

        /// <summary>Gets the big fonts of the font folders.</summary>
        public IReadOnlyList<string> BigFonts { get; }

        /// <summary>Gets or sets the big font.</summary>
        public string BigFont
        {
            get => _bigFont;
            set
            {
                if (Set(ref _bigFont, value))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets the height, as typed: 0 lets each text be given its own, as TEXT then asks.</summary>
        public string Height
        {
            get => _height;
            set
            {
                if (Set(ref _height, value ?? string.Empty))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets the width factor, as typed.</summary>
        public string WidthFactor
        {
            get => _widthFactor;
            set
            {
                if (Set(ref _widthFactor, value ?? string.Empty))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets the oblique angle in degrees, as typed.</summary>
        public string ObliqueAngle
        {
            get => _obliqueAngle;
            set
            {
                if (Set(ref _obliqueAngle, value ?? string.Empty))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets Upside down.</summary>
        public bool UpsideDown
        {
            get => _upsideDown;
            set
            {
                if (Set(ref _upsideDown, value))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets Backwards.</summary>
        public bool Backwards
        {
            get => _backwards;
            set
            {
                if (Set(ref _backwards, value))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets Vertical, which only an SHX font takes.</summary>
        public bool Vertical
        {
            get => _vertical;
            set
            {
                if (Set(ref _vertical, value && IsVerticalEnabled))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets whether Vertical is offered: for an SHX font, not for TrueType, as AutoCAD greys it.</summary>
        public bool IsVerticalEnabled => !IsTrueType;

        /// <summary>Gets whether a change is not applied yet: Apply is enabled.</summary>
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (Set(ref _isDirty, value))
                {
                    ((DelegateCommand)ApplyCommand).RaiseCanExecuteChanged();
                    RaiseNotice();
                }
            }
        }

        /// <summary>
        /// Gets the notice under the settings: why an edit was refused; else, with a change not applied, "Unsaved changes to
        /// ROMANS. Apply redraws the 124 texts that use it, as one undo step."; else empty, the notice hidden.
        /// </summary>
        public string Notice
        {
            get
            {
                if (_notice.Length > 0 || !_isDirty || _selected == null)
                {
                    return _notice;
                }

                return "Unsaved changes to " + _selected.Name + ". Apply redraws the " + _selected.Uses.ToString(CultureInfo.InvariantCulture)
                    + (_selected.Uses == 1 ? " text that uses it" : " texts that use it") + ", as one undo step.";
            }

            private set
            {
                _notice = value ?? string.Empty;
                RaiseNotice();
            }
        }

        /// <summary>Gets whether there is a notice to show.</summary>
        public bool HasNotice => Notice.Length > 0;

        /// <summary>Gets whether the notice is a warning, its dot the warning's: a change not applied, or an edit refused.</summary>
        public bool IsNoticeWarning => HasNotice;

        /// <summary>Gets the title: "Text style · plan-level2.dxf".</summary>
        public string Title => "Text style · " + _document.Title;

        /// <summary>Gets the title over the preview: "Preview · ROMANS".</summary>
        public string PreviewTitle => "Preview · " + _selected?.Name;

        /// <summary>Gets the line at the foot: "Current style: Standard · 6 styles".</summary>
        public string Footer => "Current style: " + CadTextStyleEditor.Current(_document.Database) + " · " + Styles.Count.ToString(CultureInfo.InvariantCulture) + (Styles.Count == 1 ? " style" : " styles");

        /// <summary>Gets what the Font style box shows for an SHX font, which takes none: "Regular · not for SHX fonts".</summary>
        public string FontStyleUnused => "Regular · not for SHX fonts";

        /// <summary>Gets the words of the Vertical box: "Vertical", or "Vertical (not for this font)" where the font takes none.</summary>
        public string VerticalText => IsVerticalEnabled ? "Vertical" : "Vertical (not for this font)";

        private void RaiseNotice()
        {
            Raise(nameof(Notice));
            Raise(nameof(HasNotice));
            Raise(nameof(IsNoticeWarning));
        }

        /// <summary>Gets the preview: "AaBbCcD 123" in the settings shown, drawn as the canvas draws it.</summary>
        public ImageSource Preview => _preview ?? (_preview = Read(out CadTextStyleSettings settings, out _) ? CadStylePreview.Text(settings, _document.Fonts, 516, 92) : null);

        /// <summary>Gets the command of Set Current.</summary>
        public ICommand SetCurrentCommand { get; }

        /// <summary>Gets the command of New...: a name asked, style1 offered.</summary>
        public ICommand NewCommand { get; }

        /// <summary>Gets the command of Delete.</summary>
        public ICommand DeleteCommand { get; }

        /// <summary>Gets the command of Rename.</summary>
        public ICommand RenameCommand { get; }

        /// <summary>Gets the command of Apply, enabled by a change.</summary>
        public ICommand ApplyCommand { get; }

        /// <summary>Gets the command of Cancel: the change not applied dropped, the dialog closed.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>Gets the command of Close: a change not applied asked about first.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Makes the style shown current; a change not applied is asked about first.</summary>
        /// <returns>Whether it was made current.</returns>
        public bool SetCurrent()
        {
            if (_selected == null || !Settle())
            {
                return false;
            }

            return Report(CadTextStyleEditor.SetCurrent(_document, _selected.Name), _selected.Name);
        }

        /// <summary>Adds a style of a name, a copy of the one shown, and shows it, as New... does.</summary>
        /// <param name="name">The name; null does nothing, as Cancel of the name asked.</param>
        /// <returns>Whether it was added.</returns>
        public bool New(string name)
        {
            if (name == null || !Settle())
            {
                return false;
            }

            return Report(CadTextStyleEditor.Add(_document, name, _selected?.Name), name.Trim());
        }

        /// <summary>Deletes the style shown; refused for Standard, the current style and a style in use, the reason in <see cref="Notice"/>.</summary>
        /// <returns>Whether it was deleted.</returns>
        public bool Delete()
        {
            if (_selected == null)
            {
                return false;
            }

            return Report(CadTextStyleEditor.Delete(_document, _selected.Name), CadTextStyleEditor.Current(_document.Database));
        }

        /// <summary>Renames the style shown.</summary>
        /// <param name="name">The new name; null does nothing.</param>
        /// <returns>Whether it was renamed.</returns>
        public bool Rename(string name)
        {
            if (_selected == null || name == null || !Settle())
            {
                return false;
            }

            return Report(CadTextStyleEditor.Rename(_document, _selected.Name, name), name.Trim());
        }

        /// <summary>Writes the settings shown into the style, one undo step; a value that does not read, or one AutoCAD does not take, is said in <see cref="Notice"/>.</summary>
        /// <returns>Whether they were written.</returns>
        public bool Apply()
        {
            if (_selected == null)
            {
                return false;
            }

            if (!Read(out CadTextStyleSettings settings, out string why))
            {
                Notice = why;
                return false;
            }

            return Report(CadTextStyleEditor.Apply(_document, _selected.Name, settings), _selected.Name);
        }

        /// <summary>Closes the dialog: a change not applied is asked about first, as AutoCAD asks.</summary>
        /// <returns>Whether it closed.</returns>
        public bool Close()
        {
            if (!Settle())
            {
                return false;
            }

            CloseRequested?.Invoke(this, true);
            return true;
        }

        /// <summary>
        /// Settles a change not applied before the dialog moves on: asked, Yes applies it, No drops it, Cancel stops; nothing
        /// asked where there is none, and with no one to ask the change is dropped.
        /// </summary>
        private bool Settle()
        {
            if (!_isDirty)
            {
                return true;
            }

            bool? answer = AskSave == null ? false : AskSave("The current style has been modified. Do you want to save your changes?");

            if (answer == null)
            {
                return false;
            }

            if (answer == true)
            {
                return Apply();
            }

            IsDirty = false;
            return true;
        }

        /// <summary>Shows what an edit came to: the list read again on the style named, or the reason in <see cref="Notice"/>.</summary>
        private bool Report(CadEditResult result, string show)
        {
            if (!result.Succeeded)
            {
                Notice = result.Message;
                return false;
            }

            Notice = string.Empty;
            Reload(show);
            return true;
        }

        /// <summary>Reads the list again, as the filter has it, and shows a style of it: the one named where listed, else the first.</summary>
        private void Reload(string show)
        {
            Styles.Clear();
            string current = CadTextStyleEditor.Current(_document.Database);

            foreach (DxfTextStyleTableRecord style in CadTextStyleEditor.Styles(_document.Database))
            {
                int uses = CadTextStyleEditor.CountUses(_document.Database, style.Name);

                if (_filter == AllStyles || uses > 0 || string.Equals(style.Name, current, StringComparison.OrdinalIgnoreCase))
                {
                    Styles.Add(new StyleRowViewModel(style.Name, string.Equals(style.Name, current, StringComparison.OrdinalIgnoreCase), uses, FontOf(CadTextStyleSettings.Of(style))));
                }
            }

            _selected = Styles.FirstOrDefault(s => string.Equals(s.Name, show, StringComparison.OrdinalIgnoreCase)) ?? Styles.FirstOrDefault();
            Raise(nameof(SelectedStyle));
            Raise(nameof(CurrentStyleText));
            Raise(nameof(PreviewTitle));
            Raise(nameof(Footer));
            Load();
        }

        /// <summary>Shows the settings of the selected style as it is in the drawing, nothing changed.</summary>
        private void Load()
        {
            DxfTextStyleTableRecord style = _selected == null ? null : _document.Database.TextStyleTable[_selected.Name];
            CadTextStyleSettings settings = style == null ? new CadTextStyleSettings() : CadTextStyleSettings.Of(style);
            _saved = settings;
            _loading = true;
            FontName = FontOf(settings);

            if (!FontNames.Any(f => string.Equals(f.Name, _fontName, StringComparison.OrdinalIgnoreCase)))
            {
                FontNames = Ordered(FontNames.Concat(new[] { new FontItemViewModel(_fontName, IsTrueType) }));
                Raise(nameof(FontNames));
            }

            FontStyle = settings.Font.Bold ? (settings.Font.Italic ? "Bold Italic" : "Bold") : (settings.Font.Italic ? "Italic" : "Regular");
            BigFont = settings.BigFontFileName.Length > 0 ? settings.BigFontFileName : null;
            UseBigFont = settings.BigFontFileName.Length > 0;
            Height = Number(settings.Height);
            WidthFactor = Number(settings.WidthFactor);
            ObliqueAngle = Number(settings.ObliqueAngle * 180.0 / Math.PI);
            UpsideDown = settings.UpsideDown;
            Backwards = settings.Backwards;
            Vertical = settings.Vertical;
            _loading = false;
            IsDirty = false;
            Changed();
        }

        /// <summary>A setting changed: Apply enabled where it differs from the style's, the preview drawn again.</summary>
        private void Changed()
        {
            if (_loading)
            {
                return;
            }

            IsDirty = !Read(out CadTextStyleSettings settings, out _) || !settings.SameAs(_saved);
            _preview = null;
            Raise(nameof(Preview));
        }

        /// <summary>The settings shown, as a style is written: false, with the reason, where a number does not read.</summary>
        private bool Read(out CadTextStyleSettings settings, out string why)
        {
            settings = new CadTextStyleSettings();
            why = null;

            if (!TryNumber(_height, out double height) || height < 0.0)
            {
                why = "Height must be a number of nought or more.";
                return false;
            }

            if (!TryNumber(_widthFactor, out double width) || !(width > 0.0))
            {
                why = "Width factor must be a number above nought.";
                return false;
            }

            if (!TryNumber(_obliqueAngle, out double oblique) || Math.Abs(oblique) > 85.0)
            {
                why = "Oblique angle must be from -85 to 85 degrees.";
                return false;
            }

            settings.Height = height;
            settings.WidthFactor = width;
            settings.ObliqueAngle = oblique * Math.PI / 180.0;
            settings.UpsideDown = _upsideDown;
            settings.Backwards = _backwards;

            if (IsTrueType)
            {
                string family = Path.GetExtension(_fontName ?? string.Empty).Length > 0 ? CadTrueTypeFonts.FamilyOf(_fontName) ?? Path.GetFileNameWithoutExtension(_fontName) : _fontName;
                bool bold = _fontStyle.StartsWith("Bold", StringComparison.Ordinal);
                bool italic = _fontStyle.EndsWith("Italic", StringComparison.Ordinal);
                DxfFontDescriptor was = _saved?.Font ?? default;
                bool same = string.Equals(was.TypeFace, family, StringComparison.OrdinalIgnoreCase);
                settings.FileName = same && _saved.FileName.Length > 0 ? _saved.FileName : CadTrueTypeFonts.FileOf(family);
                settings.Font = new DxfFontDescriptor(family ?? string.Empty, bold, italic, same ? was.CharacterSet : 0, same ? was.PitchAndFamily : 34);
            }
            else
            {
                settings.FileName = _fontName == BuiltInFont ? CadFonts.BuiltInName + ".shx" : _fontName ?? "txt";

                // The file the style had, as it spelled it (txt, not txt.shx), where the font is the same.
                if (_saved != null && string.Equals(FontOf(_saved), _fontName, StringComparison.OrdinalIgnoreCase))
                {
                    settings.FileName = _saved.FileName;
                }

                settings.BigFontFileName = _useBigFont ? _bigFont ?? string.Empty : string.Empty;
                settings.Vertical = _vertical;
            }

            return true;
        }

        /// <summary>The fonts in the list's order: the presenter's own first, then by name, an SHX file before a TrueType family of the same name.</summary>
        private static List<FontItemViewModel> Ordered(IEnumerable<FontItemViewModel> fonts)
            => fonts.OrderBy(f => f.Name == BuiltInFont ? 0 : 1).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.IsTrueType).ToList();

        /// <summary>The font of a style as the list names it: its TrueType family, the presenter's own font by its name in the list, else its SHX file with .shx.</summary>
        private static string FontOf(CadTextStyleSettings settings)
        {
            if (settings.Font.TypeFace.Length > 0)
            {
                return settings.Font.TypeFace;
            }

            string file = settings.FileName ?? string.Empty;

            if (CadFonts.IsBuiltIn(file))
            {
                return BuiltInFont;
            }
            string extension = Path.GetExtension(file).ToLowerInvariant();

            if (extension == ".ttf" || extension == ".ttc" || extension == ".otf")
            {
                return CadTrueTypeFonts.FamilyOf(file) ?? file;
            }

            return extension.Length == 0 ? file + ".shx" : file;
        }

        /// <summary>The name New offers: style1, else the first styleN the drawing has not.</summary>
        private string NextName()
        {
            for (int i = 1; ; i++)
            {
                string name = "style" + i.ToString(CultureInfo.InvariantCulture);

                if (!_document.Database.TextStyleTable.Has(name))
                {
                    return name;
                }
            }
        }

        /// <summary>
        /// The SHX files of the font folders, each its name and whether it is a big font (its first line "AutoCAD-86 bigfont"),
        /// the first folder's where two have one name, in the order of their names.
        /// </summary>
        private static List<KeyValuePair<string, bool>> ShxFiles(IEnumerable<string> folders)
        {
            var found = new SortedDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (string folder in folders.Where(f => !string.IsNullOrEmpty(f) && Directory.Exists(f)))
            {
                try
                {
                    foreach (string path in Directory.GetFiles(folder, "*.shx").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                    {
                        string name = Path.GetFileName(path);

                        if (!found.ContainsKey(name))
                        {
                            found[name] = IsBigFont(path);
                        }
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Warn("TextStyleViewModel: the font folder " + folder + " could not be listed.", exception);
                }
            }

            return found.ToList();
        }

        private static bool IsBigFont(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var head = new byte[22];
                    int read = stream.Read(head, 0, head.Length);
                    return Encoding.ASCII.GetString(head, 0, read).StartsWith("AutoCAD-86 bigfont", StringComparison.Ordinal);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool TryNumber(string text, out double value)
            => double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);

        private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
