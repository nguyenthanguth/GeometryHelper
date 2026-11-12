using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A command, as AutoCAD's: a name and the few lines that ask its prompts in turn and make its edit. The body is an
    /// iterator that yields each prompt and reads the answer from <see cref="CadCommandContext.Input"/> when it is resumed,
    /// so that a command reads top to bottom as its prompts do, and a <see cref="CadCommandSession"/> drives it from the
    /// command line, the canvas or a test alike.
    /// </summary>
    public sealed class CadCommand
    {
        /// <summary>Makes a command.</summary>
        /// <param name="name">The name typed to run it, in capitals as AutoCAD writes them: MOVE.</param>
        /// <param name="body">The body: yields each prompt, reads each answer, makes the edit.</param>
        /// <param name="usesSelection">Whether it works on objects selected first (MOVE, ERASE), so that a selection made
        /// before it is taken instead of asking "Select objects:".</param>
        /// <exception cref="ArgumentNullException">Thrown when the name or the body is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a space in it.</exception>
        public CadCommand(string name, Func<CadCommandContext, IEnumerable<CadPrompt>> body, bool usesSelection = false)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Trim().Length == 0 || name.IndexOf(' ') >= 0)
            {
                throw new ArgumentException("A command's name is one word: " + name + ".", nameof(name));
            }

            Name = name.ToUpperInvariant();
            Body = body ?? throw new ArgumentNullException(nameof(body));
            UsesSelection = usesSelection;
        }

        /// <summary>Gets the name, in capitals.</summary>
        public string Name { get; }

        /// <summary>Gets the body.</summary>
        public Func<CadCommandContext, IEnumerable<CadPrompt>> Body { get; }

        /// <summary>Gets whether the command takes the objects selected before it was started.</summary>
        public bool UsesSelection { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The shortcuts of the presenter as data, which Global settings › Shortcuts replaces: the aliases of the commands (M for
    /// MOVE) and what each key does (F3 toggles OSNAP, Ctrl+Z runs U). The defaults are AutoCAD's (A12 in the plan).
    /// </summary>
    public sealed class CadShortcuts
    {
        /// <summary>Makes a set of shortcuts.</summary>
        /// <param name="aliases">The aliases, each to the name of a command.</param>
        /// <param name="keys">The keys, each to a command's name or a toggle (OSNAP, GRID, ORTHO, SNAP, POLAR, DYN).</param>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public CadShortcuts(
            IEnumerable<KeyValuePair<string, string>> aliases,
            IEnumerable<KeyValuePair<string, string>> keys
        )
        {
            Aliases = Copy(aliases ?? throw new ArgumentNullException(nameof(aliases)));
            Keys = Copy(keys ?? throw new ArgumentNullException(nameof(keys)));
        }

        /// <summary>Gets the aliases, each to the name of a command, letters of either case the same; listed in ordinal order.</summary>
        public IReadOnlyDictionary<string, string> Aliases { get; }

        /// <summary>Gets what each key does, as "F3" to "OSNAP" and "Ctrl+Z" to "U"; listed in ordinal order.</summary>
        public IReadOnlyDictionary<string, string> Keys { get; }

        /// <summary>Gets AutoCAD's own: the aliases and keys of the table A12 of the plan.</summary>
        public static CadShortcuts Default { get; } =
            new CadShortcuts(
                new Dictionary<string, string>
                {
                    ["Z"] = "ZOOM",
                    ["P"] = "PAN",
                    ["RE"] = "REGEN",
                    ["L"] = "LINE",
                    ["PL"] = "PLINE",
                    ["C"] = "CIRCLE",
                    ["A"] = "ARC",
                    ["REC"] = "RECTANG",
                    ["DT"] = "TEXT",
                    ["T"] = "MTEXT",
                    ["MT"] = "MTEXT",
                    ["M"] = "MOVE",
                    ["CO"] = "COPY",
                    ["CP"] = "COPY",
                    ["RO"] = "ROTATE",
                    ["SC"] = "SCALE",
                    ["MI"] = "MIRROR",
                    ["E"] = "ERASE",
                    ["X"] = "EXPLODE",
                    ["PR"] = "PROPERTIES",
                    ["CH"] = "PROPERTIES",
                    ["MA"] = "MATCHPROP",
                    ["LA"] = "LAYER",
                    ["OP"] = "OPTIONS",
                    ["DLI"] = "DIMLINEAR",
                    ["DAL"] = "DIMALIGNED",
                    ["DRA"] = "DIMRADIUS",
                    ["DDI"] = "DIMDIAMETER",
                    ["DAN"] = "DIMANGULAR",
                    ["DAR"] = "DIMARC",
                    ["DCO"] = "DIMCONTINUE",
                    ["DIMCONT"] = "DIMCONTINUE",
                    ["DBA"] = "DIMBASELINE",
                    ["DIMBASE"] = "DIMBASELINE",
                    ["DED"] = "DIMEDIT",
                    ["DIMED"] = "DIMEDIT",
                    ["DOR"] = "DIMORDINATE",
                    ["DIMORD"] = "DIMORDINATE",
                    ["DJL"] = "DIMJOGLINE",
                    ["DJO"] = "DIMJOGGED",
                    ["JOG"] = "DIMJOGGED",
                    ["MLD"] = "MLEADER",
                    ["MLS"] = "MLEADERSTYLE",
                    ["DOV"] = "DIMOVERRIDE",
                    ["DIMOVER"] = "DIMOVERRIDE",
                    ["MLE"] = "MLEADEREDIT",
                    ["TA"] = "TEXTALIGN",
                    ["MLA"] = "MLEADERALIGN",
                    ["ATI"] = "ATTIPEDIT",
                    ["SP"] = "SPELL",
                    ["LEAD"] = "LEADER",
                    ["LE"] = "QLEADER",
                    ["TOL"] = "TOLERANCE",
                    ["MV"] = "MVIEW",
                    ["MS"] = "MSPACE",
                    ["PS"] = "PSPACE",
                    ["EL"] = "ELLIPSE",
                    ["PO"] = "POINT",
                    ["LO"] = "LAYOUT",
                    ["DI"] = "DIST",
                    ["AA"] = "AREA",
                    ["LI"] = "LIST",
                    ["LS"] = "LIST",
                    ["XL"] = "XLINE",
                    ["POL"] = "POLYGON",
                    ["DO"] = "DONUT",
                    ["MEA"] = "MEASUREGEOM",
                    ["-LA"] = "-LAYER",
                    ["LAS"] = "LAYERSTATE",
                    ["V"] = "VIEW",
                    ["-V"] = "-VIEW",
                    ["UN"] = "UNITS",
                    ["-UN"] = "-UNITS",
                    ["SCR"] = "SCRIPT",
                    ["UC"] = "UCSMAN",
                    ["LT"] = "LINETYPE",
                    ["LTYPE"] = "LINETYPE",
                    ["-LT"] = "-LINETYPE",
                    ["LTS"] = "LTSCALE",
                    ["H"] = "HATCH",
                    ["BH"] = "HATCH",
                    ["BHATCH"] = "HATCH",
                    ["HE"] = "HATCHEDIT",
                    ["BO"] = "BOUNDARY",
                    ["BPOLY"] = "BOUNDARY",
                    ["ST"] = "STYLE",
                    ["D"] = "DIMSTYLE",
                    ["DST"] = "DIMSTYLE",
                    ["TB"] = "TABLE",
                    ["TS"] = "TABLESTYLE",
                    ["DDIM"] = "DIMSTYLE",
                    ["DDPTYPE"] = "PTYPE",
                    ["O"] = "OFFSET",
                    ["F"] = "FILLET",
                    ["CHA"] = "CHAMFER",
                    ["S"] = "STRETCH",
                    ["AR"] = "ARRAY",
                    ["-AR"] = "-ARRAY",
                    ["AL"] = "ALIGN",
                    ["TR"] = "TRIM",
                    ["EX"] = "EXTEND",
                    ["BR"] = "BREAK",
                    ["J"] = "JOIN",
                    ["LEN"] = "LENGTHEN",
                    ["PE"] = "PEDIT",
                    ["B"] = "BLOCK",
                    ["-B"] = "-BLOCK",
                    ["BMAKE"] = "BLOCK",
                    ["I"] = "INSERT",
                    ["-I"] = "-INSERT",
                    ["W"] = "WBLOCK",
                    ["-W"] = "-WBLOCK",
                    ["BE"] = "BEDIT",
                    ["BS"] = "BSAVE",
                    ["BC"] = "BCLOSE",
                    ["ATT"] = "ATTDEF",
                    ["DDATTDEF"] = "ATTDEF",
                    ["-ATT"] = "-ATTDEF",
                    ["ATE"] = "ATTEDIT",
                    ["DDATTE"] = "ATTEDIT",
                    ["-ATE"] = "-ATTEDIT",
                    ["ED"] = "TEXTEDIT",
                    ["DDEDIT"] = "TEXTEDIT",
                    ["DR"] = "DRAWORDER",
                    ["G"] = "GROUP",
                    ["-G"] = "-GROUP",
                    ["PU"] = "PURGE",
                    ["-PU"] = "-PURGE",
                    ["XA"] = "XATTACH",
                    ["XC"] = "XCLIP",
                    ["XR"] = "XREF",
                    ["-XR"] = "-XREF",
                    ["ER"] = "EXTERNALREFERENCES",
                    ["IAT"] = "IMAGEATTACH",
                    ["IM"] = "IMAGE",
                    ["-IM"] = "-IMAGE",
                    ["ICL"] = "IMAGECLIP",
                },
                new Dictionary<string, string>
                {
                    ["F3"] = "OSNAP",
                    ["F7"] = "GRID",
                    ["F8"] = "ORTHO",
                    ["F9"] = "SNAP",
                    ["F10"] = "POLAR",
                    ["F11"] = "OTRACK",
                    ["F12"] = "DYN",
                    ["Delete"] = "ERASE",
                    ["Ctrl+Z"] = "U",
                    ["Ctrl+Y"] = "REDO",
                    ["Ctrl+S"] = "QSAVE",
                    ["Ctrl+1"] = "PROPERTIES",
                    ["Ctrl+2"] = "DRAWINGS",
                    ["Ctrl+3"] = "RIBBON",
                    ["Ctrl+A"] = "SELECTALL",
                    ["Ctrl+C"] = "COPYCLIP",
                    ["Ctrl+X"] = "CUTCLIP",
                    ["Ctrl+V"] = "PASTECLIP",
                    ["Ctrl+Shift+C"] = "COPYBASE",
                    ["Ctrl+Shift+V"] = "PASTEBLOCK",
                    ["Ctrl+P"] = "PLOT",
                    ["Ctrl+Shift+A"] = "PICKSTYLE",
                    ["Ctrl+W"] = "SELECTIONCYCLING",
                }
            );

        private static IReadOnlyDictionary<string, string> Copy(IEnumerable<KeyValuePair<string, string>> pairs)
        {
            var sorted = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> pair in pairs)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                {
                    throw new ArgumentException(
                        "A shortcut and what it does are both given: " + pair.Key + " = " + pair.Value + ".",
                        nameof(pairs)
                    );
                }

                sorted[pair.Key.Trim()] = pair.Value.Trim().ToUpperInvariant();
            }

            return sorted;
        }
    }

    /// <summary>
    /// The commands a session knows, by name, and the aliases that reach them. <see cref="CreateDefault"/> gives the commands
    /// of C2 under AutoCAD's names and aliases; a plugin adds its own with <see cref="Add"/>.
    /// </summary>
    public sealed class CadCommandRegistry
    {
        private readonly SortedDictionary<string, CadCommand> _commands = new SortedDictionary<string, CadCommand>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly SortedDictionary<string, CadSystemVariable> _variables = new SortedDictionary<
            string,
            CadSystemVariable
        >(StringComparer.OrdinalIgnoreCase);

        /// <summary>Makes a registry with no command and AutoCAD's aliases.</summary>
        public CadCommandRegistry()
        {
            Shortcuts = CadShortcuts.Default;
        }

        /// <summary>Gets or sets the aliases and keys; AutoCAD's until replaced, and again when set to null.</summary>
        public CadShortcuts Shortcuts
        {
            get => _shortcuts;
            set => _shortcuts = value ?? CadShortcuts.Default;
        }

        private CadShortcuts _shortcuts;

        /// <summary>Gets the commands in ordinal order of their names.</summary>
        public IReadOnlyCollection<CadCommand> Commands => _commands.Values;

        /// <summary>Adds a command, or replaces the one of its name.</summary>
        /// <param name="command">The command.</param>
        /// <exception cref="ArgumentNullException">Thrown when the command is null.</exception>
        public void Add(CadCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            _commands[command.Name] = command;
        }

        /// <summary>Takes a command out.</summary>
        /// <param name="name">Its name.</param>
        /// <returns>Whether there was one of the name.</returns>
        public bool Remove(string name) => name != null && _commands.Remove(name);

        /// <summary>Finds the command a typed name or alias names, letters of either case the same; a name wins over an alias.</summary>
        /// <param name="typed">The name or alias typed.</param>
        /// <returns>The command, or null.</returns>
        public CadCommand Find(string typed)
        {
            if (string.IsNullOrWhiteSpace(typed))
            {
                return null;
            }

            string key = typed.Trim();

            if (_commands.TryGetValue(key, out CadCommand command))
            {
                return command;
            }

            return Shortcuts.Aliases.TryGetValue(key, out string name) && _commands.TryGetValue(name, out command)
                ? command
                : null;
        }

        /// <summary>
        /// Gets the system variables SETVAR knows, and a name typed alone reaches where no command has it, in ordinal order of
        /// their names: those of <see cref="CreateDefault"/>, kept in the drawing or by the session, and those a window adds.
        /// </summary>
        public IReadOnlyCollection<CadSystemVariable> Variables => _variables.Values;

        /// <summary>Adds a system variable, or replaces the one of its name, as the window replaces ORTHOMODE with its status bar's ORTHO.</summary>
        /// <param name="variable">The variable.</param>
        /// <exception cref="ArgumentNullException">Thrown when the variable is null.</exception>
        public void AddVariable(CadSystemVariable variable)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }

            _variables[variable.Name] = variable;
        }

        /// <summary>Finds the system variable a typed name or alias names, letters of either case the same: LTS names LTSCALE.</summary>
        /// <param name="typed">The name or alias typed.</param>
        /// <returns>The variable, or null.</returns>
        public CadSystemVariable FindVariable(string typed)
        {
            if (string.IsNullOrWhiteSpace(typed))
            {
                return null;
            }

            string key = typed.Trim();

            if (_variables.TryGetValue(key, out CadSystemVariable variable))
            {
                return variable;
            }

            return Shortcuts.Aliases.TryGetValue(key, out string name) && _variables.TryGetValue(name, out variable)
                ? variable
                : null;
        }

        /// <summary>
        /// Makes a registry of the commands of C2, under AutoCAD's names: the view, edit and draw commands, SETVAR and the
        /// system variables the drawing and the session keep.
        /// </summary>
        /// <returns>The registry.</returns>
        public static CadCommandRegistry CreateDefault()
        {
            var registry = new CadCommandRegistry();
            CadCommands.AddTo(registry);
            return registry;
        }
    }
}
