using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A row of the command line's AutoComplete list: a command, a synonym of one or a system variable whose name holds what is
    /// typed, the letters typed marked in it.
    /// </summary>
    public sealed class CommandSuggestionViewModel : ObservableObject
    {
        private bool _isSelected;

        internal CommandSuggestionViewModel(string name, string command, bool isVariable, int hit, int hitLength, string note, string synonymOf, bool startsMidString, string iconKey)
        {
            Name = name;
            Command = command;
            IsVariable = isVariable;
            Note = note ?? string.Empty;
            Synonym = synonymOf == null ? string.Empty : " (" + synonymOf + ")";
            StartsMidString = startsMidString;
            IconKey = iconKey;
            hit = Math.Max(0, Math.Min(hit, name.Length));
            hitLength = Math.Max(0, Math.Min(hitLength, name.Length - hit));
            Before = name.Substring(0, hit);
            Hit = name.Substring(hit, hitLength);
            After = name.Substring(hit + hitLength);
        }

        /// <summary>Gets the name shown: LAYER, ROUND, LASTPOINT.</summary>
        public string Name { get; }

        /// <summary>Gets what choosing the row runs: the command's name, the command a synonym stands for (FILLET for ROUND), or the variable's name.</summary>
        public string Command { get; }

        /// <summary>Gets whether the row is a system variable, drawn with {x}.</summary>
        public bool IsVariable { get; }

        /// <summary>Gets the words at the right: "alias LA", "system variable", "AutoCorrect"; empty for none.</summary>
        public string Note { get; }

        /// <summary>Gets the command a synonym runs, in brackets after its name: " (FILLET)"; empty for a row that is no synonym.</summary>
        public string Synonym { get; }

        /// <summary>Gets whether the row is the first of those with the letters inside the name, drawn below a rule.</summary>
        public bool StartsMidString { get; }

        /// <summary>Gets the key of the icon of a command in the icon set, as GhIconLayers; null for a variable.</summary>
        public string IconKey { get; }

        /// <summary>Gets the letters of the name before those typed.</summary>
        public string Before { get; }

        /// <summary>Gets the letters typed, as the name has them, drawn bold in the accent.</summary>
        public string Hit { get; }

        /// <summary>Gets the letters of the name after those typed.</summary>
        public string After { get; }

        /// <summary>Gets or sets whether the row is the one Enter or Space runs.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        /// <inheritdoc/>
        public override string ToString() => Name + Synonym;
    }

    /// <summary>
    /// The command line as AutoCAD 2021's: AutoComplete of what is typed, AutoCorrect of misspellings, the commands used most
    /// offered first, the recent commands, and the menu of its right click with the Input Settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With a drawing open and no command running, <see cref="ShowSuggestions"/> lists the commands and system variables
    /// whose name holds what is typed: first a command of exactly that name, the command an alias typed stands for ("alias LA"
    /// at the right of LAYER) and the command AutoCorrect remembers for a misspelling; then the names that begin with the
    /// letters, commands and synonyms by how often they were used, then by name, then the variables; then, below a rule, the
    /// names with the letters inside, by use, then where the letters stand, then by name. The window shows the list after
    /// <see cref="GlobalSettings.AutoCompleteDelay"/> milliseconds without a key.
    /// </para>
    /// <para>
    /// The synonyms built in: ROUND for FILLET, BEVEL for CHAMFER, DELETE and REMOVE for ERASE, DUPLICATE for COPY, FLIP for
    /// MIRROR, TURN for ROTATE, RESIZE for SCALE, SYMBOL for INSERT and PRINT for PLOT; each is offered only where its command
    /// is known and its own word is not a command or an alias.
    /// </para>
    /// </remarks>
    public sealed partial class MainViewModel
    {
        /// <summary>How many commands Recent Commands keeps: 20, as AutoCAD's.</summary>
        public const int RecentCommandsKept = GlobalSettings.RecentCommandsKept;

        private static readonly KeyValuePair<string, string>[] Synonyms =
        {
            new KeyValuePair<string, string>("ROUND", "FILLET"),
            new KeyValuePair<string, string>("BEVEL", "CHAMFER"),
            new KeyValuePair<string, string>("DELETE", "ERASE"),
            new KeyValuePair<string, string>("REMOVE", "ERASE"),
            new KeyValuePair<string, string>("DUPLICATE", "COPY"),
            new KeyValuePair<string, string>("FLIP", "MIRROR"),
            new KeyValuePair<string, string>("TURN", "ROTATE"),
            new KeyValuePair<string, string>("RESIZE", "SCALE"),
            new KeyValuePair<string, string>("SYMBOL", "INSERT"),
            new KeyValuePair<string, string>("PRINT", "PLOT"),
        };

        private static readonly Dictionary<string, string> Icons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LINE"] = "GhIconLine", ["PLINE"] = "GhIconPolyline", ["CIRCLE"] = "GhIconCircle", ["ARC"] = "GhIconArc", ["TEXT"] = "GhIconText",
            ["MTEXT"] = "GhIconMText", ["MOVE"] = "GhIconMove", ["COPY"] = "GhIconCopy", ["ROTATE"] = "GhIconRotate", ["SCALE"] = "GhIconScale",
            ["MIRROR"] = "GhIconMirror", ["ERASE"] = "GhIconErase", ["EXPLODE"] = "GhIconExplode", ["STRETCH"] = "GhIconStretch", ["TRIM"] = "GhIconTrim",
            ["EXTEND"] = "GhIconExtend", ["FILLET"] = "GhIconFillet", ["CHAMFER"] = "GhIconChamfer", ["OFFSET"] = "GhIconOffset", ["BREAK"] = "GhIconBreak",
            ["JOIN"] = "GhIconJoin", ["LENGTHEN"] = "GhIconLengthen", ["ALIGN"] = "GhIconAlign", ["RECTANG"] = "GhIconRectangle", ["ELLIPSE"] = "GhIconEllipse",
            ["POINT"] = "GhIconPoint", ["PAN"] = "GhIconPan", ["ZOOM"] = "GhIconZoomIn", ["NEW"] = "GhIconNew", ["OPEN"] = "GhIconOpen", ["QSAVE"] = "GhIconSave",
            ["SAVE"] = "GhIconSave", ["SAVEAS"] = "GhIconSaveAs", ["INSERT"] = "GhIconInsertBlock", ["BLOCK"] = "GhIconCreateBlock", ["WBLOCK"] = "GhIconWriteBlock",
            ["MATCHPROP"] = "GhIconMatchProperties", ["LAYMCUR"] = "GhIconCurrent", ["LAYMCH"] = "GhIconMatch", ["PASTECLIP"] = "GhIconPaste", ["TABLE"] = "GhIconTable",
            ["QSELECT"] = "GhIconQuickSelect", ["DIST"] = "GhIconDistance", ["AREA"] = "GhIconArea", ["ID"] = "GhIconId", ["LIST"] = "GhIconList",
            ["DIMLINEAR"] = "GhIconDimension", ["DIMALIGNED"] = "GhIconAligned", ["DIMANGULAR"] = "GhIconAngular", ["DIMRADIUS"] = "GhIconRadius",
            ["DIMDIAMETER"] = "GhIconDiameter", ["QLEADER"] = "GhIconLeader", ["MLEADER"] = "GhIconLeader", ["GROUP"] = "GhIconGroup", ["UNGROUP"] = "GhIconUngroup",
            ["OPTIONS"] = "GhIconGear", ["DSETTINGS"] = "GhIconGear",
        };

        private IReadOnlyList<CommandSuggestionViewModel> _suggestions = Array.Empty<CommandSuggestionViewModel>();
        private string _lastUnknown;

        /// <summary>Gets the rows of the AutoComplete list, empty while it is closed.</summary>
        public IReadOnlyList<CommandSuggestionViewModel> Suggestions
        {
            get => _suggestions;
            private set
            {
                if (Set(ref _suggestions, value))
                {
                    Raise(nameof(IsSuggestionListOpen));
                    Raise(nameof(SelectedSuggestion));
                }
            }
        }

        /// <summary>Gets whether the AutoComplete list is open above the command line.</summary>
        public bool IsSuggestionListOpen => _suggestions.Count > 0;

        /// <summary>Gets the row Enter or Space runs: the first when the list opens, the arrows moving it; null with the list closed.</summary>
        public CommandSuggestionViewModel SelectedSuggestion => _suggestions.FirstOrDefault(s => s.IsSelected);

        /// <summary>Gets the commands run last, the newest first, each once, <see cref="RecentCommandsKept"/> at most: the command line menu's Recent Commands, kept in the settings file from one session to the next.</summary>
        public IReadOnlyList<string> RecentCommands => _settings.RecentCommands;

        /// <summary>Gets or sets how the window reads the text of the clipboard, for Paste; null where there is none.</summary>
        public Func<string> ReadClipboardText { get; set; }

        /// <summary>Gets or sets how the window puts text on the clipboard, for Copy and Copy History.</summary>
        public Action<string> WriteClipboardText { get; set; }

        /// <summary>
        /// Lists the commands and variables that match what is typed, the first row chosen, as the window does once the delay
        /// has gone by; closes the list where nothing is typed, nothing matches, AutoComplete is off, no drawing is open or a
        /// command is running.
        /// </summary>
        public void ShowSuggestions()
        {
            string typed = CommandText.Trim();

            if (!_settings.AutoComplete || _current == null || IsCommandRunning || typed.Length == 0 || typed.IndexOfAny(new[] { ' ', '\t' }) >= 0)
            {
                CloseSuggestions();
                return;
            }

            List<CommandSuggestionViewModel> rows = Suggest(_current.Session.Registry, typed);

            if (rows.Count > 0)
            {
                rows[0].IsSelected = true;
            }

            Suggestions = rows;
        }

        /// <summary>Closes the AutoComplete list, as Esc does.</summary>
        public void CloseSuggestions() => Suggestions = Array.Empty<CommandSuggestionViewModel>();

        /// <summary>Moves the row chosen down or up the list, as the arrows do, stopping at either end.</summary>
        /// <param name="step">1 for down, -1 for up.</param>
        public void MoveSuggestion(int step)
        {
            if (_suggestions.Count == 0)
            {
                return;
            }

            int index = Math.Max(0, Math.Min(_suggestions.Count - 1, _suggestions.ToList().FindIndex(s => s.IsSelected) + step));

            foreach (CommandSuggestionViewModel row in _suggestions)
            {
                row.IsSelected = row == _suggestions[index];
            }

            Raise(nameof(SelectedSuggestion));
        }

        /// <summary>Runs a row of the list, or the row chosen where none is given, as Enter, Space or a click does; the list closes.</summary>
        /// <param name="row">The row; null for the one chosen.</param>
        /// <returns>False where there is no row to run.</returns>
        public bool RunSuggestion(CommandSuggestionViewModel row = null)
        {
            row = row ?? SelectedSuggestion;
            CloseSuggestions();

            if (row == null)
            {
                return false;
            }

            CommandText = string.Empty;
            _recall = -1;
            Execute(row.Command);
            return true;
        }

        /// <summary>
        /// The menu of a right click on the command line: Recent Commands, Input Settings (AutoComplete, AutoCorrect, Search
        /// System Variables, Search Content greyed, Mid-string Search, Delay Time…), Copy, Copy History, Paste, Paste To
        /// CmdLine and Options….
        /// </summary>
        /// <param name="selectedText">The text selected in the command line, which Copy copies; null or empty for none.</param>
        /// <returns>The items.</returns>
        public IReadOnlyList<CadMenuItemViewModel> CommandLineMenu(string selectedText = null)
        {
            string clipboard = ClipboardText();
            var recent = _settings.RecentCommands.Select(name => new CadMenuItemViewModel(name, () => RunRecentCommand(name))).ToList();
            var input = new List<CadMenuItemViewModel>
            {
                new CadMenuItemViewModel("AutoComplete", () => ChangeSettings(s => s.AutoComplete = !s.AutoComplete), isChecked: _settings.AutoComplete),
                new CadMenuItemViewModel("AutoCorrect", () => ChangeSettings(s => s.AutoCorrect = !s.AutoCorrect), isChecked: _settings.AutoCorrect),
                new CadMenuItemViewModel("Search System Variables", () => ChangeSettings(s => s.SearchSystemVariables = !s.SearchSystemVariables), isChecked: _settings.SearchSystemVariables),
                new CadMenuItemViewModel("Search Content", () => { }, false, "Not built yet: AutoCAD's search of the blocks, layers, hatches and styles of the drawing.", false),
                new CadMenuItemViewModel("Mid-string Search", () => ChangeSettings(s => s.MidStringSearch = !s.MidStringSearch), isChecked: _settings.MidStringSearch),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Delay Time…", () => StartCommand("INPUTSEARCHDELAY"), _current != null, _current == null ? "Needs a drawing open: the delay is asked on the command line." : null),
            };

            return new[]
            {
                recent.Count > 0 ? new CadMenuItemViewModel("Recent Commands", recent) : new CadMenuItemViewModel("Recent Commands", () => { }, false, "No command has been run yet."),
                new CadMenuItemViewModel("Input Settings", input),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Copy", () => WriteClipboardText?.Invoke(selectedText), !string.IsNullOrEmpty(selectedText) && WriteClipboardText != null),
                new CadMenuItemViewModel("Copy History", () => WriteClipboardText?.Invoke(string.Join(Environment.NewLine, History)), History.Count > 0 && WriteClipboardText != null),
                new CadMenuItemViewModel("Paste", () => PasteToCommandLine(clipboard, false), clipboard.Length > 0),
                new CadMenuItemViewModel("Paste To CmdLine", () => PasteToCommandLine(clipboard, true), clipboard.Length > 0),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Options…", () => RequestSettings(null)),
            };
        }

        /// <summary>
        /// Notes a line run at the command line with no command running: its command (or variable) counted as used, put first
        /// in Recent Commands, and, where the line before was a name no command has, noted as its correction for AutoCorrect.
        /// </summary>
        private void NoteCommandTyped(CadCommandRegistry registry, string line)
        {
            string word = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (word == null)
            {
                return;
            }

            string name = registry.Find(word)?.Name ?? registry.FindVariable(word)?.Name;

            if (name == null)
            {
                // A name no command has, as LIEN: kept until a command is typed after it.
                _lastUnknown = char.IsLetter(word[0]) ? word.ToUpperInvariant() : null;
                return;
            }

            if (_lastUnknown != null && _settings.AutoCorrect)
            {
                _settings.NoteCorrection(_lastUnknown, name);
            }

            _lastUnknown = null;
            NoteCommandUsed(name);
        }

        /// <summary>A command run, by name: counted, and put first in Recent Commands; the settings file keeps both.</summary>
        private void NoteCommandUsed(string name)
        {
            string upper = name.ToUpperInvariant();
            _settings.CountCommandUse(upper);
            _settings.NoteRecentCommand(upper);
            Raise(nameof(RecentCommands));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>A command of Recent Commands chosen: run again, ending any command running.</summary>
        private void RunRecentCommand(string name)
        {
            CommandText = string.Empty;
            CloseSuggestions();
            StartCommand(name);
        }

        /// <summary>The text of the clipboard, empty where there is none or it cannot be read.</summary>
        private string ClipboardText()
        {
            try
            {
                return ReadClipboardText?.Invoke() ?? string.Empty;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is System.Runtime.InteropServices.ExternalException)
            {
                GeometryHelperLog.Warn("MainViewModel: the clipboard could not be read.", exception);
                return string.Empty;
            }
        }

        /// <summary>
        /// Paste: the first line of the text added to what is typed. Paste To CmdLine: each line of the text run in turn, as
        /// lines pasted into AutoCAD's command line are.
        /// </summary>
        private void PasteToCommandLine(string text, bool run)
        {
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            if (!run)
            {
                CommandText += lines[0];
                return;
            }

            CloseSuggestions();

            foreach (string line in lines.Where(l => l.Trim().Length > 0))
            {
                CommandText = string.Empty;
                Execute(line);
            }
        }

        /// <summary>INPUTSEARCHDELAY, Delay Time… of Input Settings: "Enter the delay time in milliseconds &lt;300&gt;:", 100 to 10 000.</summary>
        private IEnumerable<CadPrompt> DelayTimeBody(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForInteger("Enter the delay time in milliseconds", null, _settings.AutoCompleteDelay.ToString(CultureInfo.InvariantCulture));

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number < 100 || c.Input.Number > 10000)
                {
                    c.Write("Requires an integer between 100 and 10000.");
                    continue;
                }

                int value = (int)c.Input.Number;
                ChangeSettings(s => s.AutoCompleteDelay = value);
                yield break;
            }
        }

        /// <summary>The rows of the list for a name typed, as <see cref="ShowSuggestions"/> orders them.</summary>
        private List<CommandSuggestionViewModel> Suggest(CadCommandRegistry registry, string typed)
        {
            const int MostRows = 100;
            string key = typed.ToUpperInvariant();
            var rows = new List<CommandSuggestionViewModel>();
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, string> aliases = registry.Shortcuts.Aliases;

            // The candidates: the commands, the synonyms of known commands, and the variables where they are searched.
            var candidates = new List<Candidate>();

            foreach (CadCommand command in registry.Commands)
            {
                candidates.Add(new Candidate(command.Name.ToUpperInvariant(), command.Name.ToUpperInvariant(), false, null));
            }

            foreach (KeyValuePair<string, string> synonym in Synonyms)
            {
                if (registry.Find(synonym.Key) == null && registry.Find(synonym.Value) is CadCommand target)
                {
                    candidates.Add(new Candidate(synonym.Key, target.Name.ToUpperInvariant(), false, target.Name.ToUpperInvariant()));
                }
            }

            if (_settings.SearchSystemVariables)
            {
                foreach (CadSystemVariable variable in registry.Variables)
                {
                    if (registry.Find(variable.Name) == null)
                    {
                        candidates.Add(new Candidate(variable.Name.ToUpperInvariant(), variable.Name.ToUpperInvariant(), true, null));
                    }
                }
            }

            void Add(Candidate candidate, int hit, string note, bool midString)
            {
                if (rows.Count < MostRows && taken.Add(candidate.Name))
                {
                    rows.Add(new CommandSuggestionViewModel(candidate.Name, candidate.Command, candidate.IsVariable, hit, key.Length,
                        note ?? (candidate.IsVariable ? "system variable" : string.Empty), candidate.SynonymOf, midString, candidate.IsVariable ? null : IconOf(candidate.Command)));
                }
            }

            // First: the command of that name, the command an alias names, the command AutoCorrect remembers.
            Candidate exact = candidates.FirstOrDefault(c => c.SynonymOf == null && c.Name == key);

            if (exact != null)
            {
                Add(exact, 0, null, false);
            }

            if (aliases.TryGetValue(key, out string aliased) && candidates.FirstOrDefault(c => c.SynonymOf == null && c.Name == aliased.ToUpperInvariant()) is Candidate alias)
            {
                Add(alias, Math.Max(0, alias.Name.IndexOf(key, StringComparison.Ordinal)), "alias " + key, false);
            }

            string corrected = _settings.AutoCorrect ? _settings.CorrectionOf(key) : null;

            if (corrected != null && candidates.FirstOrDefault(c => c.SynonymOf == null && c.Name == corrected) is Candidate correction)
            {
                Add(correction, Math.Max(0, correction.Name.IndexOf(key, StringComparison.Ordinal)), "AutoCorrect", false);
            }

            // Then the names that begin with the letters: commands and synonyms by use, then by name; then the variables.
            foreach (Candidate candidate in candidates.Where(c => c.Name.StartsWith(key, StringComparison.Ordinal))
                .OrderBy(c => c.IsVariable).ThenByDescending(c => Uses(c.Command)).ThenBy(c => c.Name, StringComparer.Ordinal))
            {
                Add(candidate, 0, null, false);
            }

            // Then, below a rule, the names with the letters inside: by use, by where the letters stand, by name.
            if (_settings.MidStringSearch)
            {
                bool first = true;

                foreach (Candidate candidate in candidates.Where(c => c.Name.IndexOf(key, 1, StringComparison.Ordinal) > 0 && !c.Name.StartsWith(key, StringComparison.Ordinal))
                    .OrderByDescending(c => Uses(c.Command)).ThenBy(c => c.Name.IndexOf(key, StringComparison.Ordinal)).ThenBy(c => c.Name, StringComparer.Ordinal))
                {
                    int count = rows.Count;
                    Add(candidate, candidate.Name.IndexOf(key, StringComparison.Ordinal), null, first && rows.Count > 0);

                    if (rows.Count > count)
                    {
                        first = false;
                    }
                }
            }

            return rows;
        }

        private int Uses(string name) => _settings.CommandUses.TryGetValue(name, out int count) ? count : 0;

        /// <summary>The icon of a command: its own where the icon set has one, the layers' for the LAY commands, the prompt's for any other.</summary>
        private static string IconOf(string command)
            => Icons.TryGetValue(command, out string key) ? key
                : command.StartsWith("LAY", StringComparison.Ordinal) ? "GhIconLayers"
                : command.StartsWith("DIM", StringComparison.Ordinal) ? "GhIconDimension"
                : "GhIconPrompt";

        /// <summary>A name the list may show: a command's, a synonym's or a variable's, and what it runs.</summary>
        private sealed class Candidate
        {
            public Candidate(string name, string command, bool isVariable, string synonymOf)
            {
                Name = name;
                Command = command;
                IsVariable = isVariable;
                SynonymOf = synonymOf;
            }

            public string Name { get; }

            public string Command { get; }

            public bool IsVariable { get; }

            public string SynonymOf { get; }
        }
    }
}
