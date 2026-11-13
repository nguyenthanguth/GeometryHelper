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

        /// <summary>A font family: typed, or chosen from the families installed on Windows, each shown in its own face.</summary>
        Font,
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

        /// <summary>Gets the values a choice offers, the named colours of a colour, or the font families of a font.</summary>
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

        /// <summary>Gets whether the row is a font family, its <see cref="Options"/> the families installed.</summary>
        public bool IsFont => Kind == GlobalSettingKind.Font;

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

    /// <summary>A category of the list at the left: its name and the line on top of its page.</summary>
    public sealed class GlobalSettingCategory
    {
        internal GlobalSettingCategory(string name, string intro)
        {
            Name = name;
            Intro = intro;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

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

    /// <summary>
    /// A command of the Shortcuts page: its aliases, typed parted by commas, its keys, taken by pressing them in the Keys cell,
    /// and what clashes with them.
    /// </summary>
    public sealed class ShortcutRowViewModel : ObservableObject
    {
        private readonly Action _changed;
        private string _aliases;
        private string _keys;
        private string _conflict = string.Empty;
        private string _keysBefore;

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

        /// <summary>Gets whether the Keys cell is taking the keys pressed, from <see cref="BeginKeyCapture"/> until Esc or <see cref="EndKeyCapture"/>.</summary>
        public bool IsCapturingKeys => _keysBefore != null;

        /// <summary>Starts taking the keys pressed, as the Keys cell does when it is entered; Esc gives back the keys it has now.</summary>
        public void BeginKeyCapture()
        {
            _keysBefore = _keys;
            Raise(nameof(IsCapturingKeys));
        }

        /// <summary>Stops taking the keys pressed, keeping what was taken, as the Keys cell does when it is left.</summary>
        public void EndKeyCapture()
        {
            if (_keysBefore != null)
            {
                _keysBefore = null;
                Raise(nameof(IsCapturingKeys));
            }
        }

        /// <summary>
        /// Takes a key pressed in the Keys cell, as AutoCAD's Customize User Interface and the hot key boxes of Windows do:
        /// Ctrl+Shift+D pressed shows Ctrl+Shift+D in place of the keys the command had; Backspace or Delete clears them; Esc
        /// gives back those it had when the cell was entered and stops. A key with Ctrl or Alt, or F1 to F24 with any, is a
        /// shortcut; a modifier alone waits for the rest, and a key alone or with Shift alone, which is typing, is not taken.
        /// </summary>
        /// <param name="key">The key, the system key where Alt is held.</param>
        /// <param name="modifiers">The modifiers held.</param>
        /// <returns>Whether the cell took the key, so that it goes no further; false for Tab, which leaves the cell, and while not taking keys.</returns>
        public bool PressKey(Key key, ModifierKeys modifiers)
        {
            if (!IsCapturingKeys || (key == Key.Tab && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0))
            {
                return false;
            }

            if (modifiers == ModifierKeys.None && key == Key.Escape)
            {
                Keys = _keysBefore;
                EndKeyCapture();
            }
            else if (modifiers == ModifierKeys.None && (key == Key.Back || key == Key.Delete))
            {
                Keys = string.Empty;
            }
            else if (
                !IsModifier(key)
                && ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0 || (key >= Key.F1 && key <= Key.F24))
            )
            {
                Keys = CadWorkspace.Gesture(key, modifiers);
            }

            return true;
        }

        private static bool IsModifier(Key key) =>
            key == Key.LeftCtrl
            || key == Key.RightCtrl
            || key == Key.LeftAlt
            || key == Key.RightAlt
            || key == Key.LeftShift
            || key == Key.RightShift
            || key == Key.LWin
            || key == Key.RWin
            || key == Key.System;

        internal IEnumerable<string> AliasList => Split(_aliases);

        internal IEnumerable<string> KeyList => Split(_keys);

        private static IEnumerable<string> Split(string text) =>
            (text ?? string.Empty).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);

        /// <inheritdoc/>
        public override string ToString() => Command;
    }

    /// <summary>A folder fonts or support files are looked for in, as the Folder preference page lists it: D:\Fonts\Shx.</summary>
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

    /// <summary>A font drawn in another, as the table of the Text and fonts page lists it: romans.shx drawn in isocp.shx.</summary>
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
    /// Global settings, as the design draws it: eight categories at the left, the rows of the one chosen at the right, the
    /// layer table, the font substitutions, the search folders and the shortcuts on their pages; Reset to defaults, Cancel, Apply and OK. It edits a
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
            ["RIBBON"] = "Show or hide the ribbon",
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
            ["LAYERP"] = "Undo the last change to layer settings",
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
            ["-ARRAY"] = "Array objects, asked on the command line",
            ["-ATTDEF"] = "Define an attribute on the command line",
            ["-ATTEDIT"] = "Edit attributes one by one or globally",
            ["-BEDIT"] = "Open a block in the Block Editor, typed",
            ["-BLOCK"] = "Make a block, asked on the command line",
            ["-BOUNDARY"] = "Make a boundary polyline, typed",
            ["-COLOR"] = "Set the colour of new objects, typed",
            ["-DIMDISASSOCIATE"] = "Free dimensions from their objects, typed",
            ["-EXPORT"] = "Export to PDF, asked on the command line",
            ["-GROUP"] = "Make and edit groups on the command line",
            ["-HATCH"] = "Hatch an area, asked on the command line",
            ["-IMAGE"] = "Attach and manage images, typed",
            ["-INPUTSEARCHOPTIONS"] = "Command line search options, typed",
            ["-INSERT"] = "Insert a block, asked on the command line",
            ["-LAYER"] = "Manage layers on the command line",
            ["-LAYOUT"] = "Make, copy, rename and delete layouts, typed",
            ["-LINETYPE"] = "Load and set linetypes, typed",
            ["-LWEIGHT"] = "Set the lineweight of new objects, typed",
            ["-MTEXT"] = "Write paragraph text on the command line",
            ["-OSNAP"] = "Set the running object snaps, typed",
            ["-PAN"] = "Pan by two points or a displacement",
            ["-PLOT"] = "Plot, asked on the command line",
            ["-PLOTSTAMP"] = "Set the plot stamp, typed",
            ["-PURGE"] = "Remove unused named objects, typed",
            ["-RENAME"] = "Rename named objects, typed",
            ["-SCALELISTEDIT"] = "Edit the list of scales, typed",
            ["-STYLE"] = "Make or change a text style, typed",
            ["-TABLE"] = "Insert a table, asked on the command line",
            ["-UNITS"] = "Set drawing units, typed",
            ["-VIEW"] = "Save and restore named views, typed",
            ["-VPORTS"] = "Split the model area into tiles, typed",
            ["-WBLOCK"] = "Write objects or a block to a file, typed",
            ["-XBIND"] = "Bind symbols of an xref, typed",
            ["-XREF"] = "Attach and manage xrefs, typed",
            ["ALIGN"] = "Move, turn and scale objects to others",
            ["AREA"] = "Area and perimeter of points or objects",
            ["ARRAY"] = "Copy objects in a grid, round or along a path",
            ["ARRAYPOLAR"] = "Copy objects round a centre",
            ["ARRAYRECT"] = "Copy objects in rows and columns",
            ["ATTDEF"] = "Define an attribute",
            ["ATTEDIT"] = "Edit the attribute values of a block",
            ["ATTIPEDIT"] = "Edit an attribute value in place",
            ["ATTSYNC"] = "Bring blocks in line with their attributes",
            ["AUDIT"] = "Check the drawing and fix errors",
            ["BASE"] = "Set the insertion base point",
            ["BATTMAN"] = "Block Attribute Manager",
            ["BAUTHORPALETTE"] = "Block Authoring palettes",
            ["BCLOSE"] = "Close the Block Editor",
            ["BEDIT"] = "Open a block in the Block Editor",
            ["BLOCK"] = "Make a block from objects",
            ["BMPOUT"] = "Save the view as a BMP image",
            ["BOUNDARY"] = "Make a polyline round an enclosed area",
            ["BREAK"] = "Break an object between two points",
            ["BREAKATPOINT"] = "Break an object at a point",
            ["BSAVE"] = "Save the block in the Block Editor",
            ["BSAVEAS"] = "Save the block under another name",
            ["CAL"] = "Evaluate an expression",
            ["CHAMFER"] = "Bevel the corner of two lines",
            ["CHANGE"] = "Change points or properties of objects",
            ["CLASSICGROUP"] = "Object Grouping dialog",
            ["CLASSICINSERT"] = "Insert a block (classic dialog)",
            ["COLOR"] = "Set the colour of new objects",
            ["COMMANDLINE"] = "Show the command line",
            ["COMMANDLINEHIDE"] = "Hide the command line",
            ["COPYBASE"] = "Copy objects with a base point",
            ["COPYCLIP"] = "Copy objects to the clipboard",
            ["CUTCLIP"] = "Cut objects to the clipboard",
            ["DIM"] = "Dimension by the object picked",
            ["DIMARC"] = "Length of an arc",
            ["DIMBASELINE"] = "Dimensions from one base line",
            ["DIMBREAK"] = "Break dimension lines where objects cross",
            ["DIMCENTER"] = "Centre mark of a circle or arc",
            ["DIMCONTINUE"] = "Dimensions end to end",
            ["DIMDISASSOCIATE"] = "Free dimensions from their objects",
            ["DIMEDIT"] = "Edit dimension text and extension lines",
            ["DIMJOGGED"] = "Jogged radius dimension",
            ["DIMJOGLINE"] = "Jog a linear dimension line",
            ["DIMORDINATE"] = "Ordinate (X or Y) dimension",
            ["DIMOVERRIDE"] = "Override dimension variables",
            ["DIMREASSOCIATE"] = "Tie dimensions to objects again",
            ["DIMSPACE"] = "Space dimension lines evenly",
            ["DIMSTYLE"] = "Dimension Style Manager",
            ["DIMTEDIT"] = "Move or turn dimension text",
            ["DIST"] = "Distance and angle between two points",
            ["DIVIDE"] = "Points or blocks at equal parts of an object",
            ["DONUT"] = "Draw a filled ring",
            ["DRAWORDER"] = "Bring objects above or below others",
            ["DXFOUT"] = "Save a copy as DXF",
            ["EATTEDIT"] = "Enhanced Attribute Editor",
            ["ELLIPSE"] = "Draw an ellipse or elliptical arc",
            ["EXPORT"] = "Export the drawing to another format",
            ["EXPORTDWF"] = "Export to DWF",
            ["EXPORTDWFX"] = "Export to DWFx",
            ["EXPORTPDF"] = "Export to PDF",
            ["EXTEND"] = "Extend objects to meet others",
            ["EXTERNALREFERENCES"] = "External References palette",
            ["FILLET"] = "Round the corner of two objects",
            ["FILTER"] = "Select objects by a list of properties",
            ["FIND"] = "Find and replace text",
            ["GRADIENT"] = "Fill an area with a gradient",
            ["GROUP"] = "Make a group of objects",
            ["HATCH"] = "Fill an area with a hatch pattern",
            ["HATCHEDIT"] = "Edit a hatch",
            ["HATCHTOBACK"] = "Send all hatches to the back",
            ["HIDEOBJECTS"] = "Hide the objects selected",
            ["HIDEPALETTES"] = "Hide the palettes",
            ["ID"] = "Coordinates of a point",
            ["IMAGE"] = "Images in the External References palette",
            ["IMAGEATTACH"] = "Attach an image",
            ["IMAGECLIP"] = "Clip an image to a boundary",
            ["IMPORT"] = "Bring a DWG or DXF in as a block",
            ["INPUTSEARCHOPTIONS"] = "Command line search options",
            ["INSERT"] = "Insert a block or a drawing",
            ["ISOLATEOBJECTS"] = "Show only the objects selected",
            ["ISOPLANE"] = "Status toggle: the isometric plane",
            ["JOIN"] = "Join objects end to end into one",
            ["JPGOUT"] = "Save the view as a JPG image",
            ["LAYERPMODE"] = "Turn the tracking of LAYERP on or off",
            ["LAYERSTATE"] = "Layer States Manager",
            ["LAYFRZ"] = "Freeze the layers of objects picked",
            ["LAYISO"] = "Show only the layers of objects picked",
            ["LAYLCK"] = "Lock the layer of an object picked",
            ["LAYOFF"] = "Turn off the layers of objects picked",
            ["LAYON"] = "Turn all layers on",
            ["LAYOUT"] = "Make, rename and delete layouts",
            ["LAYTHW"] = "Thaw all layers",
            ["LAYULK"] = "Unlock the layer of an object picked",
            ["LAYUNISO"] = "Restore the layers before LAYISO",
            ["LAYVPI"] = "Freeze layers in all viewports but one",
            ["LEADER"] = "Draw a leader with an annotation",
            ["LENGTHEN"] = "Change the length of objects",
            ["LINETYPE"] = "Linetype Manager",
            ["LIST"] = "List the data of objects",
            ["LTSCALE"] = "Set the global linetype scale",
            ["LWEIGHT"] = "Set the lineweight of new objects",
            ["MEASURE"] = "Points or blocks at a step along an object",
            ["MEASUREGEOM"] = "Measure distance, radius, angle or area",
            ["MLEADERALIGN"] = "Line up multileaders",
            ["MLEADEREDIT"] = "Add or remove leader lines",
            ["MLEADERSTYLE"] = "Multileader Style Manager",
            ["MLEDIT"] = "Edit multiline crosses, tees, corners and cuts",
            ["MLINE"] = "Draw parallel lines (a multiline)",
            ["MLSTYLE"] = "Make and set multiline styles",
            ["MSPACE"] = "Work in model space through a viewport",
            ["MTEDIT"] = "Edit paragraph text",
            ["NEW"] = "Start a new drawing",
            ["OFFSET"] = "Copy an object at a distance",
            ["PAGESETUP"] = "Page Setup Manager",
            ["PASTEBLOCK"] = "Paste the clipboard as a block",
            ["PASTECLIP"] = "Paste from the clipboard",
            ["PASTEORIG"] = "Paste at the original coordinates",
            ["PASTESPEC"] = "Paste in a chosen format",
            ["PDFSHX"] = "Write SHX text as searchable words in PDF",
            ["PEDIT"] = "Edit a polyline",
            ["PICKSTYLE"] = "Status toggle: group selection",
            ["PLAN"] = "View the plan of a UCS",
            ["PLOT"] = "Plot to a printer or a file",
            ["PLOTSTAMP"] = "Set the plot stamp",
            ["PNGOUT"] = "Save the view as a PNG image",
            ["POINT"] = "Draw a point",
            ["POLYGON"] = "Draw a regular polygon",
            ["PREVIEW"] = "Plot preview",
            ["PROPERTIESCLOSE"] = "Hide Properties",
            ["PSPACE"] = "Work on the paper of a layout",
            ["PTYPE"] = "Point Style",
            ["PURGE"] = "Remove unused named objects",
            ["QDIM"] = "Quick dimensions of objects",
            ["QLEADER"] = "Quick leader",
            ["QPMODE"] = "Turn Quick Properties on or off",
            ["QSELECT"] = "Quick Select by properties",
            ["QUICKCALC"] = "QuickCalc calculator",
            ["QUICKPROPERTIES"] = "Quick Properties of the selection",
            ["QUIT"] = "Close the program",
            ["RAY"] = "Draw a line infinite one way",
            ["RECOVER"] = "Open a damaged drawing and repair it",
            ["REDRAW"] = "Refresh the view",
            ["REDRAWALL"] = "Refresh every view",
            ["REGENALL"] = "Regenerate every view",
            ["REGION"] = "Make regions from closed loops",
            ["RENAME"] = "Rename named objects",
            ["RESETBLOCK"] = "Reset dynamic blocks to their defaults",
            ["REVCLOUD"] = "Draw a revision cloud",
            ["RIBBONCLOSE"] = "Hide the ribbon",
            ["SAVE"] = "Save the drawing under a file name",
            ["SAVEAS"] = "Save the drawing under a new name or version",
            ["SCALELISTEDIT"] = "Edit the list of scales",
            ["SCRIPT"] = "Run a script of commands",
            ["SELECTALL"] = "Select all objects",
            ["SELECTIONCYCLING"] = "Status toggle: selection cycling",
            ["SETVAR"] = "List or set system variables",
            ["SHOWPALETTES"] = "Show the palettes hidden",
            ["SOLID"] = "Draw filled triangles and quadrilaterals",
            ["SPELL"] = "Check spelling",
            ["SPLINEDIT"] = "Edit a spline",
            ["STRETCH"] = "Stretch objects crossed by a window",
            ["STYLE"] = "Text Style",
            ["TABLE"] = "Insert a table",
            ["TABLEDIT"] = "Edit a table cell",
            ["TABLESTYLE"] = "Table Style",
            ["TEXTALIGN"] = "Line up texts",
            ["TEXTEDIT"] = "Edit text",
            ["TEXTTOFRONT"] = "Bring text and dimensions to the front",
            ["TILEMODE"] = "Switch between Model and the layout",
            ["TOLERANCE"] = "Geometric tolerance frame",
            ["TRANSPARENCY"] = "Status toggle: show transparency",
            ["TRIM"] = "Trim objects at others",
            ["UCS"] = "Set the user coordinate system",
            ["UCSICON"] = "Show and place the UCS icon",
            ["UCSMAN"] = "UCS manager",
            ["UNDO"] = "Undo steps, marks and groups",
            ["UNGROUP"] = "Break a group up",
            ["UNISOLATEOBJECTS"] = "Show the objects hidden or isolated",
            ["UNITS"] = "Drawing Units",
            ["VIEW"] = "View Manager: named views",
            ["VPCLIP"] = "Clip a viewport to a boundary",
            ["VPLAYER"] = "Layers frozen in viewports",
            ["VPORTS"] = "Split the model area into tiles",
            ["VPSCALE"] = "Scale of a viewport",
            ["WBLOCK"] = "Write objects or a block to a file",
            ["XATTACH"] = "Attach a drawing as an xref",
            ["XBIND"] = "Bind symbols of an xref",
            ["XCLIP"] = "Clip an xref or block to a boundary",
            ["XLINE"] = "Draw an infinite line",
            ["XOPEN"] = "Open an xref in its own tab",
            ["XREF"] = "External References palette",
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
                    "Applies to every drawing on this machine. Press Apply to see it on the canvas at once; the values are kept for the next start."
                ),
                new GlobalSettingCategory(
                    "Canvas and cursor",
                    "How the canvas zooms, picks and snaps, as AutoCAD does by default."
                ),
                new GlobalSettingCategory(
                    "Units and rounding",
                    "Units are millimetres throughout; these settings change how values are shown."
                ),
                new GlobalSettingCategory(
                    "Layers, colours and lines",
                    "The layers written to new drawings, with their colour, linetype and lineweight."
                ),
                new GlobalSettingCategory(
                    "Text and fonts",
                    "The font of the app, what draws a font of a drawing not found (FONTALT, the font mapping file and the substitutions), and the answers of hidden messages (AutoCAD’s Hidden Message Settings): set one back to Ask, or Reset to defaults, to be asked again. A font not found is drawn in GeometryHelper Simplex (ghsimplex), the presenter's own single-stroke font, by default."
                ),
                new GlobalSettingCategory(
                    "Folder preference",
                    "Where the files a drawing names are looked for, as AutoCAD's support file search path. The folder of the drawing is looked in first, then the folders listed here in order; AutoCAD's own folders are not looked in unless listed."
                ),
                new GlobalSettingCategory(
                    "Shortcuts",
                    "Commands, aliases and keys default to AutoCAD's. Click a Keys cell and press the keys (Backspace or Delete clears it, Esc gives the old ones back); type aliases parted by commas. Import acad.pgp… takes the aliases of an acad.pgp you pick. A key or an alias used twice is shown in red."
                ),
                new GlobalSettingCategory(
                    "Save and export",
                    "Where drawings are saved, how often a drawing with changes is saved aside and how many backups are kept."
                ),
            };
            CategoryCommand = new DelegateCommand(p =>
                SelectedCategory = Categories.FirstOrDefault(c =>
                    c.Name == (p as string ?? (p as GlobalSettingCategory)?.Name)
                )
            );
            ResetCommand = new DelegateCommand(_ => Reset());
            ResetShortcutsCommand = new DelegateCommand(_ => FillShortcuts(true));
            ImportPgpCommand = new DelegateCommand(_ =>
            {
                string path = BrowsePgpFile?.Invoke();

                if (!string.IsNullOrWhiteSpace(path))
                {
                    ImportPgp(path);
                }
            });
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

        /// <summary>Gets whether the shortcuts are shown.</summary>
        public bool IsShortcutsPage => _category.Name == "Shortcuts";

        /// <summary>Gets whether Text and fonts is shown, with the table of the fonts drawn in others under its rows.</summary>
        public bool IsTextAndFontsPage => _category.Name == "Text and fonts";

        /// <summary>Gets whether Folder preference is shown: the font search folders and the support file search folders.</summary>
        public bool IsFolderPreferencePage => _category.Name == "Folder preference";

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

        /// <summary>Gets the command of Import acad.pgp… on the Shortcuts page: a file asked for through <see cref="BrowsePgpFile"/>, then <see cref="ImportPgp"/>.</summary>
        public ICommand ImportPgpCommand { get; }

        /// <summary>Gets or sets how the window asks for a .pgp file: its path, or null where none was chosen.</summary>
        public Func<string> BrowsePgpFile { get; set; }

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
                        Number("Grip size", "px", () => s.GripSize, v => s.GripSize = Whole(v)),
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

            // The font of drawing text, its height and width factor are the defaults of drawings made from a model, kept in the
            // file for GeometryHelper.Drawing and not shown here, as the other settings of drawings made from a model are not.
            _pages["Text and fonts"] = new[]
            {
                new GlobalSettingSection(
                    "Interface",
                    new[]
                    {
                        new GlobalSettingRow(
                            GlobalSettingKind.Font,
                            "UI font",
                            null,
                            () => s.UiFont,
                            v => s.UiFont = v,
                            Changed,
                            InstalledFontFamilies.Value
                        ),
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
                new GlobalSettingSection(
                    "Font not found",
                    new[]
                    {
                        Text("Replacement font (FONTALT)", () => s.ReplacementFont, v => s.ReplacementFont = v),
                        Info("Where it is not found either", null, () => s.DrawingFont),
                    }
                ),
                new GlobalSettingSection(
                    "Font mapping",
                    new[] { Text("Font mapping file (FONTMAP)", () => s.FontMapFile, v => s.FontMapFile = v) }
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

        /// <summary>
        /// Imports the aliases of an acad.pgp file into the Shortcuts page, as AutoCAD reads them, its entries winning: an alias
        /// new is added to its command, one of another command is moved, and one whose command this app does not have is
        /// skipped; the notice says how many. Nothing is applied until Apply or OK.
        /// </summary>
        /// <param name="path">The .pgp file.</param>
        /// <returns>Whether the file was read; false, said in the notice, where it was not.</returns>
        public bool ImportPgp(string path)
        {
            string name = Path.GetFileName(path ?? string.Empty);
            string[] lines;

            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception ex)
                when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is ArgumentException
                    || ex is NotSupportedException
                    || ex is System.Security.SecurityException
                )
            {
                Notice = name + " was not read: " + ex.Message;
                return false;
            }

            List<KeyValuePair<string, string>> aliases = ReadPgp(lines, out int unread);
            var rows = new Dictionary<string, ShortcutRowViewModel>(StringComparer.OrdinalIgnoreCase);

            foreach (ShortcutRowViewModel row in _allShortcuts)
            {
                rows[row.Command] = row;
            }

            int added = 0,
                changed = 0,
                same = 0,
                skipped = 0;

            foreach (KeyValuePair<string, string> pair in aliases)
            {
                if (!rows.TryGetValue(pair.Value, out ShortcutRowViewModel target))
                {
                    skipped++;
                    continue;
                }

                ShortcutRowViewModel owner = _allShortcuts.FirstOrDefault(r =>
                    r.AliasList.Contains(pair.Key, StringComparer.OrdinalIgnoreCase)
                );

                if (owner == target)
                {
                    same++;
                    continue;
                }

                if (owner == null)
                {
                    added++;
                }
                else
                {
                    changed++;
                    owner.Aliases = string.Join(
                        ", ",
                        owner.AliasList.Where(a => !string.Equals(a, pair.Key, StringComparison.OrdinalIgnoreCase))
                    );
                }

                target.Aliases = string.Join(", ", target.AliasList.Concat(new[] { pair.Key }));
            }

            Notice =
                name
                + ": "
                + Count(added, "alias", "aliases")
                + " added, "
                + changed.ToString(CultureInfo.InvariantCulture)
                + " changed, "
                + same.ToString(CultureInfo.InvariantCulture)
                + " unchanged"
                + (
                    skipped > 0
                        ? "; "
                            + skipped.ToString(CultureInfo.InvariantCulture)
                            + (skipped == 1 ? " skipped, its command" : " skipped, their commands")
                            + " not in this app"
                        : string.Empty
                )
                + (unread > 0 ? "; " + Count(unread, "line", "lines") + " not read" : string.Empty)
                + ". Apply or OK takes them.";
            return true;
        }

        /// <summary>
        /// The aliases of the lines of a .pgp file, in their order, as AutoCAD reads them: <c>ALIAS, *COMMAND</c>, written in
        /// capitals, a hyphen kept (<c>-LA, *-LAYER</c>); a semicolon starts a comment, and a blank line or an external command
        /// (<c>NOTEPAD, START NOTEPAD, 1,*File to edit: ,</c>) is passed over. A line that is none of these, as an alias with a
        /// space or a command with no star, is counted in <paramref name="unread"/>.
        /// </summary>
        internal static List<KeyValuePair<string, string>> ReadPgp(IEnumerable<string> lines, out int unread)
        {
            var aliases = new List<KeyValuePair<string, string>>();
            unread = 0;

            foreach (string line in lines)
            {
                int semicolon = line.IndexOf(';');
                string text = (semicolon < 0 ? line : line.Substring(0, semicolon)).Trim();

                if (text.Length == 0)
                {
                    continue;
                }

                string[] fields = text.Split(',');

                if (fields.Skip(2).Any(f => f.Trim().Length > 0))
                {
                    continue;
                }

                string alias = fields[0].Trim();
                string command = fields.Length > 1 ? fields[1].Trim() : string.Empty;

                if (
                    alias.Length == 0
                    || !command.StartsWith("*", StringComparison.Ordinal)
                    || alias.Any(char.IsWhiteSpace)
                    || (command = command.Substring(1).Trim()).Length == 0
                    || command.Any(char.IsWhiteSpace)
                )
                {
                    unread++;
                    continue;
                }

                aliases.Add(new KeyValuePair<string, string>(alias.ToUpperInvariant(), command.ToUpperInvariant()));
            }

            return aliases;
        }

        private static string Count(int count, string one, string many) =>
            count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? one : many);

        /// <summary>The font families installed on Windows, by name, ordered without case; read once.</summary>
        private static readonly Lazy<IReadOnlyList<string>> InstalledFontFamilies = new Lazy<IReadOnlyList<string>>(
            () =>
                System
                    .Windows.Media.Fonts.SystemFontFamilies.Select(f =>
                        f.Source.Substring(f.Source.LastIndexOf('#') + 1).Trim()
                    )
                    .Where(n => n.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(n => n, StringComparer.Ordinal)
                    .ToList()
        );

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
            Raise(nameof(IsShortcutsPage));
            Raise(nameof(IsTextAndFontsPage));
            Raise(nameof(IsFolderPreferencePage));
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
