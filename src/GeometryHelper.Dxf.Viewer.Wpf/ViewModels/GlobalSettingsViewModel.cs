using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>What a row of Global settings is.</summary>
    public enum GlobalSettingKind
    {
        /// <summary>A text typed.</summary>
        Text,

        /// <summary>A number typed, with its unit.</summary>
        Number,

        /// <summary>A switch.</summary>
        Toggle,

        /// <summary>A choice from a list.</summary>
        Choice,

        /// <summary>A colour: a swatch, its #RRGGBB typed, and named colours to choose from.</summary>
        Colour,

        /// <summary>Check boxes, each on or off.</summary>
        Checks,

        /// <summary>A value shown, not edited.</summary>
        Info,
    }

    /// <summary>A check box of a row of Global settings, as a snap of Snap to.</summary>
    public sealed class GlobalSettingCheck : ObservableObject
    {
        private readonly Func<bool> _get;
        private readonly Action<bool> _set;

        internal GlobalSettingCheck(string name, Func<bool> get, Action<bool> set)
        {
            Name = name;
            _get = get;
            _set = set;
        }

        /// <summary>Gets its name.</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether it is on.</summary>
        public bool IsOn
        {
            get => _get();
            set
            {
                _set(value);
                Raise(nameof(IsOn));
            }
        }
    }

    /// <summary>
    /// A row of Global settings, read from and written to the settings being edited: a value the settings refuse shows why
    /// and is not taken.
    /// </summary>
    public sealed class GlobalSettingRow : ObservableObject
    {
        private readonly Func<string> _get;
        private readonly Action<string> _set;
        private readonly Action _changed;
        private string _value;
        private string _error = string.Empty;

        internal GlobalSettingRow(
            GlobalSettingKind kind,
            string label,
            string unit,
            Func<string> get,
            Action<string> set,
            Action changed,
            IReadOnlyList<string> options = null,
            IReadOnlyList<GlobalSettingCheck> checks = null
        )
        {
            Kind = kind;
            Label = label;
            Unit = unit ?? string.Empty;
            _get = get;
            _set = set;
            _changed = changed;
            Options = options;
            Checks = checks ?? Array.Empty<GlobalSettingCheck>();
            _value = get?.Invoke() ?? string.Empty;
        }

        /// <summary>Gets what the row is.</summary>
        public GlobalSettingKind Kind { get; }

        /// <summary>Gets the label.</summary>
        public string Label { get; }

        /// <summary>Gets the unit after a number; empty for none.</summary>
        public string Unit { get; }

        /// <summary>Gets the values a choice offers, or the named colours of a colour.</summary>
        public IReadOnlyList<string> Options { get; }

        /// <summary>Gets the check boxes of a row of checks.</summary>
        public IReadOnlyList<GlobalSettingCheck> Checks { get; }

        /// <summary>Gets or sets the value as text: "1" or "0" for a switch, #RRGGBB for a colour.</summary>
        public string Value
        {
            get => _value;
            set
            {
                string typed = value ?? string.Empty;

                if (_set == null || string.Equals(typed, _value, StringComparison.Ordinal))
                {
                    return;
                }

                _value = typed;

                try
                {
                    _set(typed);
                    Error = string.Empty;
                }
                catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is OverflowException)
                {
                    Error =
                        ex is FormatException || ex is OverflowException
                            ? (Kind == GlobalSettingKind.Colour ? "A colour is written #RRGGBB." : "Not a number.")
                            : FirstLine(ex.Message);
                }

                Raise(nameof(Value));
                Raise(nameof(IsOn));
                Raise(nameof(Rgb));
                _changed?.Invoke();
            }
        }

        /// <summary>Gets or sets whether a switch is on.</summary>
        public bool IsOn
        {
            get => _value == "1";
            set => Value = value ? "1" : "0";
        }

        /// <summary>Gets the colour of a colour row as 0xRRGGBB, for its swatch; 0 where it does not read.</summary>
        public int Rgb => GlobalSettings.TryParseHex(_value, out int rgb) ? rgb : 0;

        /// <summary>Gets or sets a named colour chosen for a colour row: "Black · #000000" sets #000000.</summary>
        public string NamedColour
        {
            get => Options?.FirstOrDefault(o => o.EndsWith(_value, StringComparison.OrdinalIgnoreCase));
            set
            {
                if (value != null && value.Length >= 7)
                {
                    Value = value.Substring(value.Length - 7);
                }

                Raise(nameof(NamedColour));
            }
        }

        /// <summary>Gets why the value typed is not taken; empty where it is.</summary>
        public string Error
        {
            get => _error;
            private set
            {
                if (Set(ref _error, value))
                {
                    Raise(nameof(HasError));
                }
            }
        }

        /// <summary>Gets whether the value typed is not taken.</summary>
        public bool HasError => _error.Length > 0;

        /// <summary>Gets whether the row is a text or a number typed.</summary>
        public bool IsTyped => Kind == GlobalSettingKind.Text || Kind == GlobalSettingKind.Number;

        /// <summary>Gets whether the row is a switch.</summary>
        public bool IsToggle => Kind == GlobalSettingKind.Toggle;

        /// <summary>Gets whether the row is a choice.</summary>
        public bool IsChoice => Kind == GlobalSettingKind.Choice;

        /// <summary>Gets whether the row is a colour.</summary>
        public bool IsColour => Kind == GlobalSettingKind.Colour;

        /// <summary>Gets whether the row is check boxes.</summary>
        public bool IsChecks => Kind == GlobalSettingKind.Checks;

        /// <summary>Gets whether the row only shows a value.</summary>
        public bool IsInfo => Kind == GlobalSettingKind.Info;

        /// <summary>Reads the value again from the settings, as after the units changed an example.</summary>
        internal void Refresh()
        {
            if (_get != null && !HasError)
            {
                _value = _get();
                Raise(nameof(Value));
                Raise(nameof(IsOn));
                Raise(nameof(Rgb));
            }
        }

        private static string FirstLine(string message)
        {
            int end = message.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? message : message.Substring(0, end);
        }

        /// <inheritdoc/>
        public override string ToString() => Label + " = " + _value;
    }

    /// <summary>A titled group of rows of a category.</summary>
    public sealed class GlobalSettingSection
    {
        internal GlobalSettingSection(string title, IReadOnlyList<GlobalSettingRow> rows)
        {
            Title = title;
            Rows = rows;
        }

        /// <summary>Gets the title.</summary>
        public string Title { get; }

        /// <summary>Gets the rows.</summary>
        public IReadOnlyList<GlobalSettingRow> Rows { get; }
    }

    /// <summary>A category of the list at the left: its name, and the MODEL tag of those for drawings made from a model.</summary>
    public sealed class GlobalSettingCategory
    {
        internal GlobalSettingCategory(string name, bool isModel, string intro)
        {
            Name = name;
            IsModel = isModel;
            Intro = intro;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets whether it is for drawings made from a model, tagged MODEL.</summary>
        public bool IsModel { get; }

        /// <summary>Gets the line on top of its page.</summary>
        public string Intro { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>A layer of the default table as the Layers page edits it.</summary>
    public sealed class GlobalLayerRow : ObservableObject
    {
        private string _name;
        private string _colour;
        private string _linetype;
        private string _lineWeight;
        private bool _plot;

        internal GlobalLayerRow(GlobalLayer layer)
        {
            _name = layer.Name;
            _colour = GlobalSettings.Hex(layer.Rgb);
            _linetype = layer.Linetype;
            _lineWeight = layer.LineWeight.ToString("0.00", CultureInfo.InvariantCulture);
            _plot = layer.Plot;
        }

        /// <summary>Gets or sets the name.</summary>
        public string Name
        {
            get => _name;
            set => Set(ref _name, value ?? string.Empty);
        }

        /// <summary>Gets or sets the colour, #RRGGBB.</summary>
        public string Colour
        {
            get => _colour;
            set
            {
                if (Set(ref _colour, value ?? string.Empty))
                {
                    Raise(nameof(Rgb));
                }
            }
        }

        /// <summary>Gets the colour as 0xRRGGBB for the swatch; 0 where it does not read.</summary>
        public int Rgb => GlobalSettings.TryParseHex(_colour, out int rgb) ? rgb : 0;

        /// <summary>Gets or sets the linetype.</summary>
        public string Linetype
        {
            get => _linetype;
            set => Set(ref _linetype, value ?? string.Empty);
        }

        /// <summary>Gets or sets the lineweight in millimetres, as text.</summary>
        public string LineWeight
        {
            get => _lineWeight;
            set => Set(ref _lineWeight, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether it is plotted.</summary>
        public bool Plot
        {
            get => _plot;
            set => Set(ref _plot, value);
        }

        /// <summary>The layer, or why it is not one.</summary>
        internal GlobalLayer ToLayer(out string error)
        {
            error = null;

            try
            {
                if (!GlobalSettings.TryParseHex(_colour, out int rgb))
                {
                    throw new FormatException("A colour is written #RRGGBB.");
                }

                return new GlobalLayer(
                    _name,
                    rgb,
                    _linetype,
                    double.Parse(_lineWeight, NumberStyles.Float, CultureInfo.InvariantCulture),
                    _plot
                );
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is OverflowException)
            {
                error =
                    "The layer "
                    + (_name.Length == 0 ? "with no name" : _name)
                    + ": "
                    + ex.Message.Split('\n')[0].Trim();
                return null;
            }
        }
    }

    /// <summary>A command of the Shortcuts page: its aliases and keys, typed parted by commas, and what clashes with them.</summary>
    public sealed class ShortcutRowViewModel : ObservableObject
    {
        private readonly Action _changed;
        private string _aliases;
        private string _keys;
        private string _conflict = string.Empty;

        internal ShortcutRowViewModel(string command, string aliases, string keys, string description, Action changed)
        {
            Command = command;
            _aliases = aliases;
            _keys = keys;
            Description = description ?? string.Empty;
            _changed = changed;
        }

        /// <summary>Gets the command's name, or a toggle's, as OSNAP.</summary>
        public string Command { get; }

        /// <summary>Gets or sets its aliases, parted by commas: "CO, CP".</summary>
        public string Aliases
        {
            get => _aliases;
            set
            {
                if (Set(ref _aliases, value ?? string.Empty))
                {
                    _changed();
                }
            }
        }

        /// <summary>Gets or sets its keys, parted by commas: "Ctrl+C".</summary>
        public string Keys
        {
            get => _keys;
            set
            {
                if (Set(ref _keys, value ?? string.Empty))
                {
                    _changed();
                }
            }
        }

        /// <summary>Gets what it does.</summary>
        public string Description { get; }

        /// <summary>Gets what clashes: "Ctrl+1 is already used by PROPERTIES"; empty for nothing.</summary>
        public string Conflict
        {
            get => _conflict;
            internal set
            {
                if (Set(ref _conflict, value ?? string.Empty))
                {
                    Raise(nameof(HasConflict));
                }
            }
        }

        /// <summary>Gets whether something clashes, drawn red.</summary>
        public bool HasConflict => _conflict.Length > 0;

        internal IEnumerable<string> AliasList => Split(_aliases);

        internal IEnumerable<string> KeyList => Split(_keys);

        private static IEnumerable<string> Split(string text) =>
            (text ?? string.Empty).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);

        /// <inheritdoc/>
        public override string ToString() => Command;
    }

    /// <summary>A folder fonts are looked for in, as the Fonts page lists it: D:\Fonts\Shx.</summary>
    public sealed class FontFolderRow : ObservableObject
    {
        private string _folder;

        internal FontFolderRow(string folder)
        {
            _folder = folder ?? string.Empty;
        }

        /// <summary>Gets or sets the folder.</summary>
        public string Folder
        {
            get => _folder;
            set => Set(ref _folder, value ?? string.Empty);
        }

        /// <inheritdoc/>
        public override string ToString() => _folder;
    }

    /// <summary>A font drawn in another, as the Fonts page's table lists it: romans.shx drawn in isocp.shx.</summary>
    public sealed class FontSubstitutionRow : ObservableObject
    {
        private string _font;
        private string _drawnIn;

        internal FontSubstitutionRow(string font, string drawnIn)
        {
            _font = font ?? string.Empty;
            _drawnIn = drawnIn ?? string.Empty;
        }

        /// <summary>Gets or sets the font a style names: romans.shx, Arial.</summary>
        public string Font
        {
            get => _font;
            set => Set(ref _font, value ?? string.Empty);
        }

        /// <summary>Gets or sets the font it is drawn in.</summary>
        public string DrawnIn
        {
            get => _drawnIn;
            set => Set(ref _drawnIn, value ?? string.Empty);
        }

        /// <inheritdoc/>
        public override string ToString() => _font + " -> " + _drawnIn;
    }

    /// <summary>
    /// Global settings, as the design draws it: ten categories at the left, the rows of the one chosen at the right, the
    /// layer table, the font folders and substitutions, the model sources and the shortcuts on their own pages; Reset to defaults, Cancel, Apply and OK. It edits a
    /// copy of the window's settings: Apply gives them to the window at once, OK applies and saves them to its file, Cancel
    /// drops them.
    /// </summary>
    /// <remarks>
    /// A value the settings refuse is said under its row and not taken; a key or an alias given to two commands is drawn red
    /// with whom it clashes; Apply and OK wait until every one is put right.
    /// </remarks>
    public sealed class GlobalSettingsViewModel : ObservableObject
    {
        private static readonly string[] Toggles = { "OSNAP", "GRID", "ORTHO", "SNAP", "POLAR", "OTRACK", "DYN" };

        private static readonly string[] NamedColours =
        {
            "Black · #000000",
            "Dark grey · #212830",
            "Grey · #3A414C",
            "Light grey · #9AA4B1",
            "White · #FFFFFF",
            "Blue · #3D9BFF",
            "Green · #3FB950",
            "Amber · #D29922",
            "Red · #F85149",
            "Cyan · #39C5CF",
        };

        private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["ZOOM"] = "Zoom; E extents, W window, P previous",
            ["PAN"] = "Pan the canvas",
            ["REGEN"] = "Draw the canvas again",
            ["LINE"] = "Draw lines",
            ["PLINE"] = "Draw a polyline",
            ["CIRCLE"] = "Draw a circle",
            ["ARC"] = "Draw an arc",
            ["RECTANG"] = "Draw a rectangle",
            ["TEXT"] = "Write a line of text",
            ["MTEXT"] = "Write a paragraph of text",
            ["MOVE"] = "Move objects",
            ["COPY"] = "Copy objects",
            ["ROTATE"] = "Turn objects about a point",
            ["SCALE"] = "Scale objects about a point",
            ["MIRROR"] = "Mirror objects across a line",
            ["ERASE"] = "Delete objects",
            ["EXPLODE"] = "Break objects into their parts",
            ["U"] = "Undo the last step",
            ["REDO"] = "Redo the step undone",
            ["PROPERTIES"] = "Show or hide Properties",
            ["DRAWINGS"] = "Show or hide the Drawings list",
            ["MATCHPROP"] = "Copy properties from one object to others",
            ["LAYER"] = "Open the Layer manager",
            ["OPTIONS"] = "Open Global settings",
            ["DIMLINEAR"] = "Horizontal or vertical dimension",
            ["DIMALIGNED"] = "Dimension along two points",
            ["DIMRADIUS"] = "Radius of an arc or circle",
            ["DIMDIAMETER"] = "Diameter of an arc or circle",
            ["DIMANGULAR"] = "Angle between two lines",
            ["MLEADER"] = "A mark with a leader",
            ["MVIEW"] = "A new view (viewport) on a layout",
            ["QSAVE"] = "Save the drawing",
            ["LAYMCUR"] = "The layer of an object becomes current",
            ["LAYMCH"] = "Objects go onto the layer of another",
            ["PLUGINLOAD"] = "Load a plugin .dll from any folder",
            ["PLUGINUNLOAD"] = "Unload a plugin",
            ["PLUGINRELOAD"] = "Reload a plugin after rebuilding it",
            ["OSNAP"] = "Status toggle: object snaps",
            ["DSETTINGS"] = "Drafting Settings: object snaps, snap and polar",
            ["GRID"] = "Status toggle: the dot grid",
            ["ORTHO"] = "Status toggle: ortho",
            ["SNAP"] = "Status toggle: grid snap",
            ["POLAR"] = "Status toggle: polar tracking",
            ["OTRACK"] = "Status toggle: object snap tracking",
            ["DYN"] = "Status toggle: input at the cursor",
        };

        private readonly MainViewModel _model;
        private readonly Dictionary<string, IReadOnlyList<GlobalSettingSection>> _pages = new Dictionary<
            string,
            IReadOnlyList<GlobalSettingSection>
        >(StringComparer.Ordinal);
        private GlobalSettings _working;
        private GlobalSettingCategory _category;
        private string _search = string.Empty;
        private string _notice = string.Empty;
        private List<ShortcutRowViewModel> _allShortcuts = new List<ShortcutRowViewModel>();

        /// <summary>Makes the view model over a copy of the window's settings.</summary>
        /// <param name="model">The window's view model.</param>
        /// <param name="category">The category shown first; null for Appearance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the window's view model is null.</exception>
        public GlobalSettingsViewModel(MainViewModel model, string category = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            Categories = new[]
            {
                new GlobalSettingCategory(
                    "Appearance",
                    false,
                    "Applies to every drawing on this machine. Press Apply to see it on the canvas at once; the values are kept for the next start."
                ),
                new GlobalSettingCategory(
                    "Canvas and cursor",
                    false,
                    "How the canvas zooms, picks and snaps, as AutoCAD does by default."
                ),
                new GlobalSettingCategory(
                    "Drawing defaults",
                    true,
                    "For drawings made from a model (the MODEL categories). Used when a new drawing is made; each drawing keeps its own settings afterwards."
                ),
                new GlobalSettingCategory(
                    "Units and rounding",
                    false,
                    "Units are millimetres throughout; these settings change how values are shown."
                ),
                new GlobalSettingCategory(
                    "Layers, colours and lines",
                    false,
                    "The layers written to new drawings, with their colour, linetype and lineweight."
                ),
                new GlobalSettingCategory(
                    "Text and fonts",
                    false,
                    "Fonts of the app and of the drawings. Hidden messages: the answer kept when Always perform my current choice was ticked (AutoCAD’s Hidden Message Settings); set it back to Ask, or Reset to defaults, to be asked again."
                ),
                new GlobalSettingCategory(
                    "Fonts",
                    false,
                    "Where the fonts of text styles are found, as AutoCAD's support path, and what draws a font not found, as FONTALT and the font mapping file. The folder of the drawing is looked in first, then the folders listed here; AutoCAD's own folders are not looked in unless listed. A font not found is drawn in GeometryHelper Simplex (ghsimplex), the presenter's own single-stroke font, by default."
                ),
                new GlobalSettingCategory(
                    "Data sources",
                    true,
                    "For drawings made from a model (the MODEL categories). Where drawings get their objects."
                ),
                new GlobalSettingCategory(
                    "Shortcuts",
                    false,
                    "Commands, aliases and keys default to AutoCAD's. Type a command's aliases and keys parted by commas; a key or an alias used twice is shown in red."
                ),
                new GlobalSettingCategory(
                    "Save and export",
                    false,
                    "Where drawings are saved and how they are exported."
                ),
            };
            CategoryCommand = new DelegateCommand(p =>
                SelectedCategory = Categories.FirstOrDefault(c =>
                    c.Name == (p as string ?? (p as GlobalSettingCategory)?.Name)
                )
            );
            ResetCommand = new DelegateCommand(_ => Reset());
            ResetShortcutsCommand = new DelegateCommand(_ => FillShortcuts(true));
            AddLayerCommand = new DelegateCommand(_ => AddLayer());
            RemoveLayerCommand = new DelegateCommand(p => RemoveLayer(p as GlobalLayerRow));
            AddFontFolderCommand = new DelegateCommand(p => FontFolders.Add(new FontFolderRow(p as string)));
            RemoveFontFolderCommand = new DelegateCommand(p => FontFolders.Remove(p as FontFolderRow));
            AddFontSubstitutionCommand = new DelegateCommand(_ =>
                FontSubstitutions.Add(new FontSubstitutionRow(string.Empty, string.Empty))
            );
            RemoveFontSubstitutionCommand = new DelegateCommand(p =>
                FontSubstitutions.Remove(p as FontSubstitutionRow)
            );
            AddSupportFolderCommand = new DelegateCommand(p => SupportFolders.Add(new FontFolderRow(p as string)));
            RemoveSupportFolderCommand = new DelegateCommand(p => SupportFolders.Remove(p as FontFolderRow));
            ApplyCommand = new DelegateCommand(_ => Apply(), _ => !HasErrors);
            OkCommand = new DelegateCommand(_ => Ok(), _ => !HasErrors);
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
            Load(_model.Settings.Clone());
            _category = Categories.FirstOrDefault(c =>
                string.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase)
            ) ?? Categories[0];
        }

        /// <summary>Raised when the dialog is to close: true after OK, false after Cancel.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the settings being edited.</summary>
        public GlobalSettings Working => _working;

        /// <summary>Gets the categories, in the order of the design.</summary>
        public IReadOnlyList<GlobalSettingCategory> Categories { get; }

        /// <summary>Gets or sets the category shown.</summary>
        public GlobalSettingCategory SelectedCategory
        {
            get => _category;
            set
            {
                if (value != null && Set(ref _category, value))
                {
                    RaisePage();
                }
            }
        }

        /// <summary>Gets the line on top of the page.</summary>
        public string Intro => _category.Intro;

        /// <summary>Gets the sections of the page shown.</summary>
        public IReadOnlyList<GlobalSettingSection> Sections =>
            _pages.TryGetValue(_category.Name, out IReadOnlyList<GlobalSettingSection> page)
                ? page
                : Array.Empty<GlobalSettingSection>();

        /// <summary>Gets whether the layer table is shown.</summary>
        public bool IsLayersPage => _category.Name == "Layers, colours and lines";

        /// <summary>Gets whether the model sources are shown.</summary>
        public bool IsSourcesPage => _category.Name == "Data sources";

        /// <summary>Gets whether the shortcuts are shown.</summary>
        public bool IsShortcutsPage => _category.Name == "Shortcuts";

        /// <summary>Gets whether the font folders and the substitution table are shown.</summary>
        public bool IsFontsPage => _category.Name == "Fonts";

        /// <summary>Gets the layers of the default table, edited in place.</summary>
        public ObservableCollection<GlobalLayerRow> Layers { get; } = new ObservableCollection<GlobalLayerRow>();

        /// <summary>Gets the folders fonts are looked for in, in order, edited in place; a blank one is left out.</summary>
        public ObservableCollection<FontFolderRow> FontFolders { get; } = new ObservableCollection<FontFolderRow>();

        /// <summary>Gets the fonts drawn in others, edited in place; a row of a blank name is left out.</summary>
        public ObservableCollection<FontSubstitutionRow> FontSubstitutions { get; } =
            new ObservableCollection<FontSubstitutionRow>();

        /// <summary>Gets the command of Add folder: the folder given, else a blank row to type it in.</summary>
        public ICommand AddFontFolderCommand { get; }

        /// <summary>Gets the command that takes a folder out of the list, given its row.</summary>
        public ICommand RemoveFontFolderCommand { get; }

        /// <summary>Gets the command of Add substitution: a blank row to type it in.</summary>
        public ICommand AddFontSubstitutionCommand { get; }

        /// <summary>Gets the command that takes a substitution out of the table, given its row.</summary>
        public ICommand RemoveFontSubstitutionCommand { get; }

        /// <summary>Gets the support file search folders, where linetype (.lin) and hatch pattern (.pat) files are looked for, in order, edited in place; a blank one is left out.</summary>
        public ObservableCollection<FontFolderRow> SupportFolders { get; } = new ObservableCollection<FontFolderRow>();

        /// <summary>Gets the command of Add folder under the support file search folders: the folder given, else a blank row to type it in.</summary>
        public ICommand AddSupportFolderCommand { get; }

        /// <summary>Gets the command that takes a support folder out of the list, given its row.</summary>
        public ICommand RemoveSupportFolderCommand { get; }

        /// <summary>Gets the model sources the host gives, with their state.</summary>
        public IReadOnlyList<CadSourceInfo> Sources => _model.SettingsSources;

        /// <summary>Gets the commands of the Shortcuts page, as the search finds them.</summary>
        public ObservableCollection<ShortcutRowViewModel> Shortcuts { get; } =
            new ObservableCollection<ShortcutRowViewModel>();

        /// <summary>Gets or sets the search of the Shortcuts page: a command, an alias or a key, in part.</summary>
        public string ShortcutSearch
        {
            get => _search;
            set
            {
                if (Set(ref _search, value ?? string.Empty))
                {
                    FilterShortcuts();
                }
            }
        }

        /// <summary>Gets whether a value is refused or a shortcut clashes, so that Apply and OK wait.</summary>
        public bool HasErrors =>
            _pages.Values.SelectMany(p => p).SelectMany(s => s.Rows).Any(r => r.HasError)
            || _allShortcuts.Any(r => r.HasConflict);

        /// <summary>Gets a line at the foot: what Apply or OK did, or why not.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value ?? string.Empty);
        }

        /// <summary>Gets the command that shows a category, given it or its name.</summary>
        public ICommand CategoryCommand { get; }

        /// <summary>Gets the command of Reset to defaults: every category back to its defaults, not applied until Apply or OK.</summary>
        public ICommand ResetCommand { get; }

        /// <summary>Gets the command of Reset to AutoCAD defaults on the Shortcuts page.</summary>
        public ICommand ResetShortcutsCommand { get; }

        /// <summary>Gets the command of Add layer.</summary>
        public ICommand AddLayerCommand { get; }

        /// <summary>Gets the command that takes a layer out of the table, given its row.</summary>
        public ICommand RemoveLayerCommand { get; }

        /// <summary>Gets the command of Apply.</summary>
        public ICommand ApplyCommand { get; }

        /// <summary>Gets the command of OK.</summary>
        public ICommand OkCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>Finds a row of a category by its label.</summary>
        /// <param name="category">The category's name.</param>
        /// <param name="label">The row's label.</param>
        /// <returns>The row, or null.</returns>
        public GlobalSettingRow Row(string category, string label) =>
            _pages.TryGetValue(category, out IReadOnlyList<GlobalSettingSection> page)
                ? page.SelectMany(s => s.Rows).FirstOrDefault(r => r.Label == label)
                : null;

        /// <summary>Gives the window the settings edited, at once; nothing is saved.</summary>
        /// <returns>Whether they were applied: false while a value is refused, a layer does not read or a shortcut clashes.</returns>
        public bool Apply()
        {
            if (!Commit())
            {
                return false;
            }

            _model.ApplySettings(_working);
            Notice = "Applied.";
            return true;
        }

        /// <summary>Applies the settings edited and saves them to the window's settings file, then closes.</summary>
        /// <returns>Whether they were applied and saved.</returns>
        public bool Ok()
        {
            if (!Apply())
            {
                return false;
            }

            try
            {
                _working.Save(_model.SettingsPath);
            }
            catch (Exception ex)
                when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is ArgumentException
                    || ex is NotSupportedException
                )
            {
                Notice = "Applied, but not saved: " + ex.Message;
                return false;
            }

            CloseRequested?.Invoke(this, true);
            return true;
        }

        /// <summary>
        /// Puts every category back to its defaults, not applied until Apply or OK; what the settings remember (the commands
        /// used, the misspellings corrected, the recent files) is kept.
        /// </summary>
        public void Reset()
        {
            var defaults = new GlobalSettings();
            defaults.CopyRecordsFrom(_working);
            Load(defaults);
            Notice = "Every category is back to its defaults; Apply or OK takes them.";
        }

        private void Load(GlobalSettings settings)
        {
            _working = settings;
            Build();
            Layers.Clear();

            foreach (GlobalLayer layer in settings.Layers)
            {
                Layers.Add(new GlobalLayerRow(layer));
            }

            FontFolders.Clear();
            FontSubstitutions.Clear();

            foreach (string folder in settings.FontSearchFolders)
            {
                FontFolders.Add(new FontFolderRow(folder));
            }

            foreach (KeyValuePair<string, string> pair in settings.FontSubstitutions)
            {
                FontSubstitutions.Add(new FontSubstitutionRow(pair.Key, pair.Value));
            }

            SupportFolders.Clear();

            foreach (string folder in settings.SupportSearchFolders)
            {
                SupportFolders.Add(new FontFolderRow(folder));
            }

            FillShortcuts(false);
            RaisePage();
        }

        /// <summary>Takes the layer table and the shortcuts into the settings edited; false, said in the notice, where one does not read.</summary>
        private bool Commit()
        {
            if (HasErrors)
            {
                Notice = "Put the values in red right first.";
                return false;
            }

            var layers = new List<GlobalLayer>();

            foreach (GlobalLayerRow row in Layers)
            {
                GlobalLayer layer = row.ToLayer(out string error);

                if (layer == null)
                {
                    Notice = error;
                    return false;
                }

                layers.Add(layer);
            }

            try
            {
                _working.Layers = layers;
                _working.FontSearchFolders = FontFolders.Select(r => r.Folder).ToList();
                _working.SupportSearchFolders = SupportFolders.Select(r => r.Folder).ToList();
                _working.SetFontSubstitutions(
                    FontSubstitutions.Select(r => new KeyValuePair<string, string>(r.Font, r.DrawnIn))
                );
                _working.SetShortcuts(
                    _allShortcuts.SelectMany(r =>
                        r.AliasList.Select(a => new KeyValuePair<string, string>(a, r.Command))
                    ),
                    _allShortcuts.SelectMany(r => r.KeyList.Select(k => new KeyValuePair<string, string>(k, r.Command)))
                );
            }
            catch (ArgumentException ex)
            {
                Notice = ex.Message.Split('\n')[0].Trim();
                return false;
            }

            return true;
        }

        private void Build()
        {
            GlobalSettings s = _working;
            _pages["Appearance"] = new[]
            {
                new GlobalSettingSection(
                    "Colours",
                    new[]
                    {
                        Info("Mode", "Dark (Light comes later)"),
                        Colour("Canvas background", () => s.CanvasBackground, v => s.CanvasBackground = v),
                        Colour("Paper", () => s.Paper, v => s.Paper = v),
                        Colour(
                            "Block Editor background",
                            () => s.BlockEditorBackground,
                            v => s.BlockEditorBackground = v
                        ),
                        Colour("Visible lines on dark", () => s.VisibleLines, v => s.VisibleLines = v),
                        Colour("Hidden lines", () => s.HiddenLines, v => s.HiddenLines = v),
                        Colour("Selected objects", () => s.Selection, v => s.Selection = v),
                        Colour("Grip", () => s.Grip, v => s.Grip = v),
                        Colour("Table indicator", () => s.TableIndicatorColor, v => s.TableIndicatorColor = v),
                    }
                ),
                new GlobalSettingSection(
                    "Cursor",
                    new[]
                    {
                        Number("Crosshair size", "% of screen", () => s.CrosshairPercent, v => s.CrosshairPercent = v),
                        Number("Pickbox", "px", () => s.PickboxPixels, v => s.PickboxPixels = Whole(v)),
                    }
                ),
                new GlobalSettingSection(
                    "Display",
                    new[]
                    {
                        Toggle("Grid", () => s.Grid, v => s.Grid = v),
                        Toggle("Show lineweights", () => s.ShowLineWeights, v => s.ShowLineWeights = v),
                    }
                ),
            };

            CadSnapModes[] snaps =
            {
                CadSnapModes.Endpoint,
                CadSnapModes.Midpoint,
                CadSnapModes.Center,
                CadSnapModes.Quadrant,
                CadSnapModes.Intersection,
                CadSnapModes.Perpendicular,
                CadSnapModes.Tangent,
                CadSnapModes.Nearest,
                CadSnapModes.Node,
                CadSnapModes.GeometricCenter,
                CadSnapModes.Extension,
                CadSnapModes.Insertion,
                CadSnapModes.ApparentIntersection,
                CadSnapModes.Parallel,
            };
            _pages["Canvas and cursor"] = new[]
            {
                new GlobalSettingSection(
                    "Zoom and pan",
                    new[]
                    {
                        Number("Wheel zoom factor", "×", () => s.WheelZoomFactor, v => s.WheelZoomFactor = v),
                        Toggle("Reverse the wheel", () => s.WheelZoomReversed, v => s.WheelZoomReversed = v),
                        Info("Zoom about the cursor", "Always"),
                        Info("Middle double-click", "Zoom extents"),
                    }
                ),
                new GlobalSettingSection(
                    "Selection",
                    new[]
                    {
                        Info("Window, left to right", "On"),
                        Info("Crossing, right to left", "On"),
                        Info("Select first, then command", "On"),
                        Toggle("Select by dragging", () => s.SelectByDragging, v => s.SelectByDragging = v),
                    }
                ),
                new GlobalSettingSection(
                    "Object snaps",
                    new[]
                    {
                        new GlobalSettingRow(
                            GlobalSettingKind.Checks,
                            "Snap to",
                            null,
                            () => string.Empty,
                            null,
                            Changed,
                            checks: snaps
                                .Select(m => new GlobalSettingCheck(
                                    m == CadSnapModes.Center ? "Centre"
                                        : m == CadSnapModes.GeometricCenter ? "Geometric centre"
                                        : m == CadSnapModes.ApparentIntersection ? "Apparent intersection"
                                        : m.ToString(),
                                    () => (s.Snaps & m) != 0,
                                    on =>
                                    {
                                        s.Snaps = on ? s.Snaps | m : s.Snaps & ~m;
                                        Changed();
                                    }
                                ))
                                .ToList()
                        ),
                        Number("Aperture", "px", () => s.AperturePixels, v => s.AperturePixels = Whole(v)),
                        Number("Polar angle", "°", () => s.PolarAngle, v => s.PolarAngle = v),
                        Toggle("F3 toggles snaps", () => s.F3TogglesSnaps, v => s.F3TogglesSnaps = v),
                    }
                ),
                new GlobalSettingSection(
                    "Input",
                    new[]
                    {
                        Toggle("Dynamic input at the cursor (DYN, F12)", () => s.DynamicInput, v => s.DynamicInput = v),
                        Info("Command line", "Ctrl+9 shows or hides it"),
                    }
                ),
            };

            _pages["Drawing defaults"] = new[]
            {
                new GlobalSettingSection(
                    "New drawings",
                    new[]
                    {
                        Choice("Preset", GlobalSettings.Presets, () => s.DefaultPreset, v => s.DefaultPreset = v),
                        Choice(
                            "Sheet size",
                            GlobalSettings.SheetSizes,
                            () => s.DefaultSheetSize,
                            v => s.DefaultSheetSize = v
                        ),
                        Choice(
                            "Orientation",
                            new[] { "Landscape", "Portrait" },
                            () => s.DefaultOrientation,
                            v => s.DefaultOrientation = v
                        ),
                        Choice(
                            "Projection",
                            new[] { "Third angle", "First angle" },
                            () => s.DefaultProjection,
                            v => s.DefaultProjection = v
                        ),
                    }
                ),
            };

            GlobalSettingRow lengthExample = Info("Example", null, () =>
                s.FormatNumber(12345.678, s.CoordinateDecimals) + " mm"
            );
            GlobalSettingRow angleExample = Info("Example", null, () => s.FormatNumber(30.0, s.AngleDecimals) + "°");
            Action examples = () =>
            {
                lengthExample.Refresh();
                angleExample.Refresh();
            };
            _pages["Units and rounding"] = new[]
            {
                new GlobalSettingSection(
                    "Lengths",
                    new[]
                    {
                        Info("Unit", "mm"),
                        Number(
                            "Coordinate decimals",
                            null,
                            () => s.CoordinateDecimals,
                            v => s.CoordinateDecimals = Whole(v),
                            examples
                        ),
                        Number(
                            "Dimension decimals",
                            null,
                            () => s.DimensionDecimals,
                            v => s.DimensionDecimals = Whole(v),
                            examples
                        ),
                        Choice(
                            "Thousands separator",
                            GlobalSettings.ThousandsSeparators,
                            () => s.ThousandsSeparator,
                            v => s.ThousandsSeparator = v,
                            examples
                        ),
                        Choice(
                            "Decimal separator",
                            new[] { "Point", "Comma" },
                            () => s.DecimalSeparator,
                            v => s.DecimalSeparator = v,
                            examples
                        ),
                        lengthExample,
                    }
                ),
                new GlobalSettingSection(
                    "Angles",
                    new[]
                    {
                        Info("Format", "Degrees"),
                        Number("Decimals", null, () => s.AngleDecimals, v => s.AngleDecimals = Whole(v), examples),
                        angleExample,
                    }
                ),
            };

            _pages["Text and fonts"] = new[]
            {
                new GlobalSettingSection("Interface", new[] { Text("UI font", () => s.UiFont, v => s.UiFont = v) }),
                new GlobalSettingSection(
                    "Drawing text",
                    new[]
                    {
                        Text("Font where a style's is missing", () => s.DrawingFont, v => s.DrawingFont = v),
                        Number("Text height", "mm", () => s.TextHeight, v => s.TextHeight = v),
                        Number("Title text height", "mm", () => s.TitleTextHeight, v => s.TitleTextHeight = v),
                        Number("Width factor", null, () => s.WidthFactor, v => s.WidthFactor = v),
                    }
                ),
                new GlobalSettingSection(
                    "Hidden messages",
                    new[]
                    {
                        // AutoCAD's Hidden Message Settings: the answer Always perform my current choice keeps, Ask to be asked again.
                        Choice(
                            "Esc in the MTEXT editor with changes",
                            new[] { "Ask", "Yes, save the changes", "No, discard the changes" },
                            () =>
                                s.MTextUnsavedChanges == "Yes" ? "Yes, save the changes"
                                : s.MTextUnsavedChanges == "No" ? "No, discard the changes"
                                : "Ask",
                            v =>
                                s.MTextUnsavedChanges =
                                    v.StartsWith("Yes", StringComparison.Ordinal) ? "Yes"
                                    : v.StartsWith("No", StringComparison.Ordinal) ? "No"
                                    : "Ask"
                        ),
                    }
                ),
            };

            _pages["Fonts"] = new[]
            {
                new GlobalSettingSection(
                    "Font not found",
                    new[]
                    {
                        Text("Replacement font (FONTALT)", () => s.ReplacementFont, v => s.ReplacementFont = v),
                        Info("Where it is not found either", "Font where a style's is missing (Text and fonts)"),
                    }
                ),
                new GlobalSettingSection(
                    "Font mapping",
                    new[] { Text("Font mapping file (FONTMAP)", () => s.FontMapFile, v => s.FontMapFile = v) }
                ),
            };

            _pages["Data sources"] = new[]
            {
                new GlobalSettingSection(
                    "Model changes",
                    new[]
                    {
                        Toggle(
                            "Check for model changes",
                            () => s.CheckForModelChanges,
                            v => s.CheckForModelChanges = v
                        ),
                        Number("Check every", "s", () => s.CheckEverySeconds, v => s.CheckEverySeconds = Whole(v)),
                    }
                ),
            };

            _pages["Save and export"] = new[]
            {
                new GlobalSettingSection(
                    "Saving",
                    new[]
                    {
                        Text("Save folder", () => s.SaveFolder, v => s.SaveFolder = v),
                        Number("Autosave every", "min", () => s.AutosaveMinutes, v => s.AutosaveMinutes = Whole(v)),
                        Number("Backups kept", null, () => s.BackupsKept, v => s.BackupsKept = Whole(v)),
                    }
                ),
                new GlobalSettingSection(
                    "DXF export",
                    new[]
                    {
                        Choice(
                            "Layout",
                            new[] { "Layout + viewports (like MVIEW)", "Sheet in model space" },
                            () =>
                                s.ExportLayout == "SheetInModelSpace"
                                    ? "Sheet in model space"
                                    : "Layout + viewports (like MVIEW)",
                            v =>
                                s.ExportLayout =
                                    v == "Sheet in model space" ? "SheetInModelSpace" : "LayoutAndViewports"
                        ),
                        Info("Version", "AutoCAD 2013 (AC1027)"),
                        Toggle("Open the folder after export", () => s.OpenAfterExport, v => s.OpenAfterExport = v),
                    }
                ),
            };
        }

        /// <summary>The commands with their aliases and keys: those of the settings, or AutoCAD's with <paramref name="autoCad"/>, and those plugins and extensions added.</summary>
        private void FillShortcuts(bool autoCad)
        {
            CadShortcuts current = _model.CurrentShortcuts();
            IReadOnlyDictionary<string, string> aliases = Merge(
                current.Aliases,
                autoCad ? CadShortcuts.Default.Aliases : _working.Aliases,
                CadShortcuts.Default.Aliases
            );
            IReadOnlyDictionary<string, string> keys = Merge(
                current.Keys,
                autoCad ? CadShortcuts.Default.Keys : _working.Keys,
                CadShortcuts.Default.Keys
            );
            var commands = new SortedSet<string>(_model.CommandNames(), StringComparer.OrdinalIgnoreCase);
            commands.UnionWith(Toggles);
            commands.UnionWith(aliases.Values);
            commands.UnionWith(keys.Values);
            _allShortcuts = commands
                .Select(c => new ShortcutRowViewModel(
                    c,
                    string.Join(
                        ", ",
                        aliases
                            .Where(a => string.Equals(a.Value, c, StringComparison.OrdinalIgnoreCase))
                            .Select(a => a.Key)
                    ),
                    string.Join(
                        ", ",
                        keys.Where(k => string.Equals(k.Value, c, StringComparison.OrdinalIgnoreCase))
                            .Select(k => k.Key)
                    ),
                    Descriptions.TryGetValue(c, out string d) ? d : string.Empty,
                    OnShortcutChanged
                ))
                .ToList();
            FindConflicts();
            FilterShortcuts();

            if (autoCad)
            {
                Notice = "The shortcuts are back to AutoCAD's; Apply or OK takes them.";
            }
        }

        /// <summary>The aliases or keys: those the settings hold, and those of the command line that AutoCAD's table has not.</summary>
        private static IReadOnlyDictionary<string, string> Merge(
            IReadOnlyDictionary<string, string> current,
            IReadOnlyDictionary<string, string> settings,
            IReadOnlyDictionary<string, string> autoCad
        )
        {
            var merged = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> pair in current.Where(p => !autoCad.ContainsKey(p.Key)))
            {
                merged[pair.Key] = pair.Value;
            }

            foreach (KeyValuePair<string, string> pair in settings)
            {
                merged[pair.Key] = pair.Value;
            }

            return merged;
        }

        private void OnShortcutChanged()
        {
            FindConflicts();
            RaiseErrors();
        }

        /// <summary>Marks the rows whose key or alias another row has, or whose alias is another command's name.</summary>
        private void FindConflicts()
        {
            var keyOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var aliasOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(_allShortcuts.Select(r => r.Command), StringComparer.OrdinalIgnoreCase);

            foreach (ShortcutRowViewModel row in _allShortcuts)
            {
                foreach (string key in row.KeyList)
                {
                    if (!keyOwners.ContainsKey(key))
                    {
                        keyOwners[key] = row.Command;
                    }
                }

                foreach (string alias in row.AliasList)
                {
                    if (!aliasOwners.ContainsKey(alias))
                    {
                        aliasOwners[alias] = row.Command;
                    }
                }
            }

            foreach (ShortcutRowViewModel row in _allShortcuts)
            {
                string key = row.KeyList.FirstOrDefault(k =>
                    !string.Equals(keyOwners[k], row.Command, StringComparison.OrdinalIgnoreCase)
                );
                string alias = row.AliasList.FirstOrDefault(a =>
                    !string.Equals(aliasOwners[a], row.Command, StringComparison.OrdinalIgnoreCase)
                    || (names.Contains(a) && !string.Equals(a, row.Command, StringComparison.OrdinalIgnoreCase))
                );
                row.Conflict =
                    key != null ? key + " is already used by " + keyOwners[key]
                    : alias != null
                        ? (
                            names.Contains(alias)
                                ? alias + " is the name of a command"
                                : alias + " is already used by " + aliasOwners[alias]
                        )
                    : string.Empty;
            }
        }

        private void FilterShortcuts()
        {
            Shortcuts.Clear();

            foreach (
                ShortcutRowViewModel row in _allShortcuts.Where(r =>
                    _search.Length == 0
                    || r.Command.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                    || r.Aliases.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                    || r.Keys.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                )
            )
            {
                Shortcuts.Add(row);
            }
        }

        private void AddLayer()
        {
            int n = 1;

            while (
                Layers.Any(l =>
                    string.Equals(
                        l.Name,
                        "Layer" + n.ToString(CultureInfo.InvariantCulture),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                n++;
            }

            Layers.Add(
                new GlobalLayerRow(
                    new GlobalLayer("Layer" + n.ToString(CultureInfo.InvariantCulture), 0xFFFFFF, "Continuous", 0.25)
                )
            );
        }

        private void RemoveLayer(GlobalLayerRow row)
        {
            if (row != null)
            {
                Layers.Remove(row);
            }
        }

        private void Changed() => RaiseErrors();

        private void RaiseErrors()
        {
            Raise(nameof(HasErrors));
            ((DelegateCommand)ApplyCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)OkCommand).RaiseCanExecuteChanged();
        }

        private void RaisePage()
        {
            Raise(nameof(SelectedCategory));
            Raise(nameof(Intro));
            Raise(nameof(Sections));
            Raise(nameof(IsLayersPage));
            Raise(nameof(IsSourcesPage));
            Raise(nameof(IsShortcutsPage));
            Raise(nameof(IsFontsPage));
            Raise(nameof(Sources));
            RaiseErrors();
        }

        private static int Whole(double value) =>
            value == Math.Floor(value) && Math.Abs(value) < int.MaxValue
                ? (int)value
                : throw new ArgumentException("A whole number is needed.");

        private static string Plain(double value) =>
            Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);

        private GlobalSettingRow Toggle(string label, Func<bool> get, Action<bool> set) =>
            new GlobalSettingRow(
                GlobalSettingKind.Toggle,
                label,
                null,
                () => get() ? "1" : "0",
                v => set(v == "1"),
                Changed
            );

        private GlobalSettingRow Number(
            string label,
            string unit,
            Func<double> get,
            Action<double> set,
            Action also = null
        ) =>
            new GlobalSettingRow(
                GlobalSettingKind.Number,
                label,
                unit,
                () => Plain(get()),
                v => set(double.Parse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)),
                () =>
                {
                    Changed();
                    also?.Invoke();
                }
            );

        private GlobalSettingRow Text(string label, Func<string> get, Action<string> set) =>
            new GlobalSettingRow(GlobalSettingKind.Text, label, null, get, set, Changed);

        private GlobalSettingRow Choice(
            string label,
            IReadOnlyList<string> options,
            Func<string> get,
            Action<string> set,
            Action also = null
        ) =>
            new GlobalSettingRow(
                GlobalSettingKind.Choice,
                label,
                null,
                get,
                set,
                () =>
                {
                    Changed();
                    also?.Invoke();
                },
                options
            );

        private GlobalSettingRow Colour(string label, Func<int> get, Action<int> set) =>
            new GlobalSettingRow(
                GlobalSettingKind.Colour,
                label,
                null,
                () => GlobalSettings.Hex(get()),
                v =>
                    set(
                        GlobalSettings.TryParseHex(v, out int rgb)
                            ? rgb
                            : throw new FormatException("A colour is written #RRGGBB.")
                    ),
                Changed,
                NamedColours
            );

        private static GlobalSettingRow Info(string label, string value, Func<string> get = null) =>
            new GlobalSettingRow(GlobalSettingKind.Info, label, null, get ?? (() => value), null, null);
    }
}
