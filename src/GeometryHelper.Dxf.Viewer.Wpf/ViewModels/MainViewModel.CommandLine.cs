using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A row of the command line's AutoComplete list: a command, a synonym of one or a system variable whose name holds what is
    /// typed, the letters typed marked in it; a named object of Search Content; or the header of a group of them.
    /// </summary>
    public sealed class CommandSuggestionViewModel : ObservableObject
    {
        private readonly Lazy<ImageSource> _thumbnail;
        private bool _isSelected;
        private bool _isExpanded;

        internal CommandSuggestionViewModel(
            string name,
            string command,
            bool isVariable,
            int hit,
            int hitLength,
            string note,
            string synonymOf,
            bool startsMidString,
            string iconKey,
            string contentType = null,
            bool startsContent = false,
            Action run = null,
            bool isHeader = false,
            Func<ImageSource> thumbnail = null
        )
        {
            IsHeader = isHeader;
            _thumbnail = thumbnail == null ? null : new Lazy<ImageSource>(thumbnail);
            Name = name;
            Command = command;
            IsVariable = isVariable;
            Note = note ?? string.Empty;
            Synonym = synonymOf == null ? string.Empty : " (" + synonymOf + ")";
            StartsMidString = startsMidString;
            IconKey = iconKey;
            ContentType = contentType;
            StartsContent = startsContent;
            Run = run;
            hit = Math.Max(0, Math.Min(hit, name.Length));
            hitLength = Math.Max(0, Math.Min(hitLength, name.Length - hit));
            Before = name.Substring(0, hit);
            Hit = name.Substring(hit, hitLength);
            After = name.Substring(hit + hitLength);
        }

        /// <summary>Gets the name shown: LAYER, ROUND, LASTPOINT; for a named object, its name as the drawing has it: WALLS.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets what choosing the row runs: the command's name, the command a synonym stands for (FILLET for ROUND), or the
        /// variable's name; for a named object, the command that acts on it: CLAYER, -INSERT, HATCH, TEXTSTYLE or DIMSTYLE.
        /// </summary>
        public string Command { get; }

        /// <summary>
        /// Gets the kind of named object the row is, as Search Content lists them: "Block", "Layer", "Hatch", "Text Style" or
        /// "Dim Style"; null for a command, a synonym, a variable or a header.
        /// </summary>
        public string ContentType { get; }

        /// <summary>Gets whether the row is the header of the first kind of named object, drawn below a rule.</summary>
        public bool StartsContent { get; }

        /// <summary>Gets whether a rule is drawn above the row: the first of the names with the letters inside, the header of the variables listed apart, or the header of the first kind of named object.</summary>
        public bool StartsGroup => StartsMidString || StartsContent;

        /// <summary>What choosing a named object does: the layer made current, the block inserted; null for any other row.</summary>
        internal Action Run { get; }

        /// <summary>Gets whether the row is a system variable, drawn with {x}.</summary>
        public bool IsVariable { get; }

        /// <summary>Gets the words at the right: "alias LA", "system variable", "AutoCorrect", a header's count; empty for none.</summary>
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

        /// <summary>The width and height in pixels of a hatch pattern's thumbnail: 96.</summary>
        public const int ThumbnailSize = 96;

        /// <summary>
        /// Gets whether the row is the header of a group: of a kind of named object of Search Content ("Layers"), or of the
        /// system variables listed apart ("System Variables"), its count in <see cref="Note"/>. Choosing it collapses or
        /// expands the group rather than running anything.
        /// </summary>
        public bool IsHeader { get; }

        /// <summary>Gets whether a header's group shows its rows; false for a row that is no header.</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            internal set
            {
                if (Set(ref _isExpanded, value))
                {
                    Raise(nameof(Chevron));
                }
            }
        }

        /// <summary>Gets the chevron drawn before a header: ▾ expanded, ▸ collapsed; empty for a row that is no header.</summary>
        public string Chevron =>
            !IsHeader ? string.Empty
            : _isExpanded ? "▾"
            : "▸";

        /// <summary>Gets whether the row stands in a header's group, drawn indented.</summary>
        public bool IsIndented => Header != null;

        /// <summary>Gets whether the row has a thumbnail to show when the pointer is over it: a hatch pattern's.</summary>
        public bool HasThumbnail => _thumbnail != null;

        /// <summary>
        /// Gets the thumbnail of a hatch pattern's row, <see cref="ThumbnailSize"/> pixels square, drawn the first time it is
        /// asked for from the app's own pattern lines; null for any other row, or where the pattern cannot be drawn.
        /// </summary>
        public ImageSource Thumbnail => _thumbnail?.Value;

        /// <summary>The header whose group the row is in; null for a row of no group, or a header.</summary>
        internal CommandSuggestionViewModel Header { get; set; }

        /// <summary>The kind of named object a header stands for ("Layer"); null for the system variables' header and any other row.</summary>
        internal string GroupKind { get; set; }

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
    /// With <see cref="GlobalSettings.SearchContent"/> on and the list at the command line, below another rule come the named
    /// objects of the drawing, as AutoCAD 2021's Search Content: its blocks, layers, hatch patterns, text styles and dimension
    /// styles, each kind under a header with its count (design CmdContentSection): a click on the header, or Enter on it,
    /// collapses or expands the kind, and a kind collapsed stays so while the app runs. A hatch pattern's row shows a thumbnail
    /// of the pattern when the pointer is over it. Choosing a name acts on it: a layer, a text style or a dimension style is
    /// made current, a block is inserted, a hatch pattern is taken by HATCH. With Separate commands and system variables on,
    /// the variables wait under a collapsed header of their own after the commands until Tab expands it
    /// (<see cref="TabSuggestions"/>), as AutoCAD 2021 shows them.
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

        private static readonly Dictionary<string, string> Icons = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["LINE"] = "GhIconLine",
            ["PLINE"] = "GhIconPolyline",
            ["CIRCLE"] = "GhIconCircle",
            ["ARC"] = "GhIconArc",
            ["TEXT"] = "GhIconText",
            ["MTEXT"] = "GhIconMText",
            ["MOVE"] = "GhIconMove",
            ["COPY"] = "GhIconCopy",
            ["ROTATE"] = "GhIconRotate",
            ["SCALE"] = "GhIconScale",
            ["MIRROR"] = "GhIconMirror",
            ["ERASE"] = "GhIconErase",
            ["EXPLODE"] = "GhIconExplode",
            ["STRETCH"] = "GhIconStretch",
            ["TRIM"] = "GhIconTrim",
            ["EXTEND"] = "GhIconExtend",
            ["FILLET"] = "GhIconFillet",
            ["CHAMFER"] = "GhIconChamfer",
            ["OFFSET"] = "GhIconOffset",
            ["BREAK"] = "GhIconBreak",
            ["JOIN"] = "GhIconJoin",
            ["LENGTHEN"] = "GhIconLengthen",
            ["ALIGN"] = "GhIconAlign",
            ["RECTANG"] = "GhIconRectangle",
            ["ELLIPSE"] = "GhIconEllipse",
            ["POINT"] = "GhIconPoint",
            ["PAN"] = "GhIconPan",
            ["ZOOM"] = "GhIconZoomIn",
            ["NEW"] = "GhIconNew",
            ["OPEN"] = "GhIconOpen",
            ["QSAVE"] = "GhIconSave",
            ["SAVE"] = "GhIconSave",
            ["SAVEAS"] = "GhIconSaveAs",
            ["INSERT"] = "GhIconInsertBlock",
            ["BLOCK"] = "GhIconCreateBlock",
            ["WBLOCK"] = "GhIconWriteBlock",
            ["MATCHPROP"] = "GhIconMatchProperties",
            ["LAYMCUR"] = "GhIconCurrent",
            ["LAYMCH"] = "GhIconMatch",
            ["LAYERP"] = "GhIconLayerPrevious",
            ["PASTECLIP"] = "GhIconPaste",
            ["TABLE"] = "GhIconTable",
            ["QSELECT"] = "GhIconQuickSelect",
            ["DIST"] = "GhIconDistance",
            ["AREA"] = "GhIconArea",
            ["ID"] = "GhIconId",
            ["LIST"] = "GhIconList",
            ["DIMLINEAR"] = "GhIconDimension",
            ["DIMALIGNED"] = "GhIconAligned",
            ["DIMANGULAR"] = "GhIconAngular",
            ["DIMRADIUS"] = "GhIconRadius",
            ["DIMDIAMETER"] = "GhIconDiameter",
            ["QLEADER"] = "GhIconLeader",
            ["LEADER"] = "GhIconLeader",
            ["MLEADER"] = "GhIconLeader",
            ["GROUP"] = "GhIconGroup",
            ["UNGROUP"] = "GhIconUngroup",
            ["OPTIONS"] = "GhIconGear",
            ["DSETTINGS"] = "GhIconGear",
        };

        private readonly HashSet<string> _collapsedKinds = new HashSet<string>(StringComparer.Ordinal);
        private IReadOnlyList<CommandSuggestionViewModel> _suggestions = Array.Empty<CommandSuggestionViewModel>();
        private IReadOnlyList<CommandSuggestionViewModel> _allSuggestions = Array.Empty<CommandSuggestionViewModel>();
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

            if (
                !_settings.AutoComplete
                || _current == null
                || IsCommandRunning
                || typed.Length == 0
                || typed.IndexOfAny(new[] { ' ', '\t' }) >= 0
            )
            {
                CloseSuggestions();
                return;
            }

            List<CommandSuggestionViewModel> rows = Suggest(_current.Session.Registry, typed);

            // The named objects, as AutoCAD's Search Content: at the command line alone, not in the box at the cursor.
            if (_settings.SearchContent && !DynamicInput.IsOn)
            {
                rows.AddRange(SuggestContent(typed, rows.Count == 0));
            }

            _allSuggestions = rows;
            ShowGroups(null);
        }

        /// <summary>
        /// Tab with the list open, or with a name typed: where the system variables are listed apart (Separate commands and
        /// system variables) and wait under their collapsed header, expands it and chooses its first variable, as AutoCAD
        /// 2021 shows them only after Tab. Each new list starts with them collapsed again.
        /// </summary>
        /// <returns>Whether the variables were shown; false where there are none waiting.</returns>
        public bool TabSuggestions()
        {
            if (
                _settings.SeparateSystemVariables
                && _settings.SearchSystemVariables
                && !IsSuggestionListOpen
                && !IsCommandRunning
                && CommandText.Trim().Length > 0
            )
            {
                ShowSuggestions();
            }

            CommandSuggestionViewModel header = _allSuggestions.FirstOrDefault(r =>
                r.IsHeader && r.GroupKind == null && !r.IsExpanded
            );

            if (header == null || !IsSuggestionListOpen)
            {
                return false;
            }

            header.IsExpanded = true;
            ShowGroups(_allSuggestions.FirstOrDefault(r => r.Header == header) ?? header);
            return true;
        }

        /// <summary>
        /// Collapses a header's group, or expands it, as a click on the header or Enter on it does; the header stays chosen and
        /// the list open. A kind of named object collapsed stays collapsed in the lists after, while the app runs.
        /// </summary>
        /// <param name="header">The header; a row that is no header is passed over.</param>
        public void ToggleSuggestionGroup(CommandSuggestionViewModel header)
        {
            if (header == null || !header.IsHeader || !_allSuggestions.Contains(header))
            {
                return;
            }

            header.IsExpanded = !header.IsExpanded;

            if (header.GroupKind != null)
            {
                if (header.IsExpanded)
                {
                    _collapsedKinds.Remove(header.GroupKind);
                }
                else
                {
                    _collapsedKinds.Add(header.GroupKind);
                }
            }

            ShowGroups(header);
        }

        /// <summary>The rows of the list shown: every row but those of a collapsed group, the row given chosen, else the first.</summary>
        private void ShowGroups(CommandSuggestionViewModel chosen)
        {
            List<CommandSuggestionViewModel> shown = _allSuggestions
                .Where(r => r.Header == null || r.Header.IsExpanded)
                .ToList();

            if (chosen == null || !shown.Contains(chosen))
            {
                chosen = shown.FirstOrDefault();
            }

            foreach (CommandSuggestionViewModel row in _allSuggestions)
            {
                row.IsSelected = row == chosen;
            }

            Suggestions = shown;
            Raise(nameof(SelectedSuggestion));
        }

        /// <summary>Closes the AutoComplete list, as Esc does.</summary>
        public void CloseSuggestions()
        {
            _allSuggestions = Array.Empty<CommandSuggestionViewModel>();
            Suggestions = Array.Empty<CommandSuggestionViewModel>();
        }

        /// <summary>Moves the row chosen down or up the list, as the arrows do, stopping at either end.</summary>
        /// <param name="step">1 for down, -1 for up.</param>
        public void MoveSuggestion(int step)
        {
            if (_suggestions.Count == 0)
            {
                return;
            }

            int index = Math.Max(
                0,
                Math.Min(_suggestions.Count - 1, _suggestions.ToList().FindIndex(s => s.IsSelected) + step)
            );

            foreach (CommandSuggestionViewModel row in _suggestions)
            {
                row.IsSelected = row == _suggestions[index];
            }

            Raise(nameof(SelectedSuggestion));
        }

        /// <summary>
        /// Runs a row of the list, or the row chosen where none is given, as Enter, Space or a click does; the list closes. A
        /// header is collapsed or expanded instead (<see cref="ToggleSuggestionGroup"/>), the list kept open.
        /// </summary>
        /// <param name="row">The row; null for the one chosen.</param>
        /// <returns>False where there is no row to run.</returns>
        public bool RunSuggestion(CommandSuggestionViewModel row = null)
        {
            row = row ?? SelectedSuggestion;

            if (row != null && row.IsHeader)
            {
                ToggleSuggestionGroup(row);
                return true;
            }

            CloseSuggestions();

            if (row == null)
            {
                return false;
            }

            CommandText = string.Empty;
            _recall = -1;

            if (row.Run != null)
            {
                row.Run();
            }
            else
            {
                Execute(row.Command);
            }

            return true;
        }

        /// <summary>
        /// The menu of a right click on the command line: Recent Commands, Input Settings (AutoComplete, AutoCorrect, Search
        /// System Variables, Search Content, Mid-string Search, Delay Time…, which asks INPUTSEARCHDELAY, and Input Search
        /// Options…), Copy, Copy History, Paste, Paste To CmdLine and Options….
        /// </summary>
        /// <param name="selectedText">The text selected in the command line, which Copy copies; null or empty for none.</param>
        /// <returns>The items.</returns>
        public IReadOnlyList<CadMenuItemViewModel> CommandLineMenu(string selectedText = null)
        {
            string clipboard = ClipboardText();
            var recent = _settings
                .RecentCommands.Select(name => new CadMenuItemViewModel(name, () => RunRecentCommand(name)))
                .ToList();
            var input = new List<CadMenuItemViewModel>
            {
                new CadMenuItemViewModel(
                    "AutoComplete",
                    () => ChangeSettings(s => s.AutoComplete = !s.AutoComplete),
                    isChecked: _settings.AutoComplete
                ),
                new CadMenuItemViewModel(
                    "AutoCorrect",
                    () => ChangeSettings(s => s.AutoCorrect = !s.AutoCorrect),
                    isChecked: _settings.AutoCorrect
                ),
                new CadMenuItemViewModel(
                    "Search System Variables",
                    () => ChangeSettings(s => s.SearchSystemVariables = !s.SearchSystemVariables),
                    isChecked: _settings.SearchSystemVariables
                ),
                new CadMenuItemViewModel(
                    "Search Content",
                    () => ChangeSettings(s => s.SearchContent = !s.SearchContent),
                    isChecked: _settings.SearchContent
                ),
                new CadMenuItemViewModel(
                    "Mid-string Search",
                    () => ChangeSettings(s => s.MidStringSearch = !s.MidStringSearch),
                    isChecked: _settings.MidStringSearch
                ),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel(
                    "Delay Time…",
                    () => StartCommand("INPUTSEARCHDELAY"),
                    _current != null,
                    _current == null ? "Needs a drawing open: the delay is asked on the command line." : null
                ),
                new CadMenuItemViewModel(
                    "Input Search Options…",
                    () => StartCommand("INPUTSEARCHOPTIONS"),
                    _current != null,
                    _current == null ? "Needs a drawing open: INPUTSEARCHOPTIONS is a command." : null
                ),
            };

            return new[]
            {
                recent.Count > 0
                    ? new CadMenuItemViewModel("Recent Commands", recent)
                    : new CadMenuItemViewModel("Recent Commands", () => { }, false, "No command has been run yet."),
                new CadMenuItemViewModel("Input Settings", input),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel(
                    "Copy",
                    () => WriteClipboardText?.Invoke(selectedText),
                    !string.IsNullOrEmpty(selectedText) && WriteClipboardText != null
                ),
                new CadMenuItemViewModel(
                    "Copy History",
                    () => WriteClipboardText?.Invoke(string.Join(Environment.NewLine, History)),
                    History.Count > 0 && WriteClipboardText != null
                ),
                new CadMenuItemViewModel("Paste", () => PasteToCommandLine(clipboard, false), clipboard.Length > 0),
                new CadMenuItemViewModel(
                    "Paste To CmdLine",
                    () => PasteToCommandLine(clipboard, true),
                    clipboard.Length > 0
                ),
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
            catch (Exception exception)
                when (exception is InvalidOperationException
                    || exception is System.Runtime.InteropServices.ExternalException
                )
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

        /// <summary>
        /// Gets or sets how the workspace shows Input Search Options (INPUTSEARCHOPTIONS), so that a test drives it with none:
        /// true for OK, which writes its settings. With none set, the command asks -INPUTSEARCHOPTIONS's prompts.
        /// </summary>
        public Func<InputSearchOptionsViewModel, bool> ShowInputSearchOptions { get; set; }

        /// <summary>INPUTSEARCHOPTIONS: Input Search Options shown, its settings kept on OK; with no window, -INPUTSEARCHOPTIONS.</summary>
        private IEnumerable<CadPrompt> InputSearchOptionsBody(CadCommandContext c)
        {
            if (ShowInputSearchOptions == null)
            {
                foreach (CadPrompt prompt in InputSearchOptionsTyped(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            var dialog = new InputSearchOptionsViewModel(_settings);

            if (ShowInputSearchOptions(dialog))
            {
                ChangeSettings(dialog.WriteInto);
            }
        }

        /// <summary>
        /// -INPUTSEARCHOPTIONS, word for word as AutoCAD 2021's console asks it: the current settings, then "Enter an input
        /// search Option [autoComplete/autocoRrect/System variables/conTent/Midstring/Delay]:" until Enter, each switch asked
        /// Yes or No ("Automatically complete a command as you type? [Yes/No] &lt;Yes&gt;:"), and Delay in seconds, "Specify
        /// the delay time in seconds &lt;0.30&gt;:": above 10 refused with "Invalid. Maximum delay is 10 seconds" and asked
        /// again, below 0.1 left as it was, as AutoCAD leaves it.
        /// </summary>
        private IEnumerable<CadPrompt> InputSearchOptionsTyped(CadCommandContext c)
        {
            string[] options = { "autoComplete", "autocoRrect", "System variables", "conTent", "Midstring", "Delay" };
            string Yes(bool on) => on ? "Y" : "N";

            while (true)
            {
                c.Write(
                    "Current settings: AutoComplete = "
                        + Yes(_settings.AutoComplete)
                        + ", AutoCorrect = "
                        + Yes(_settings.AutoCorrect)
                        + ", System variable = "
                        + Yes(_settings.SearchSystemVariables)
                        + ", Content = "
                        + Yes(_settings.SearchContent)
                        + ", Midstring = "
                        + Yes(_settings.MidStringSearch)
                        + ", Delay="
                        + Seconds(_settings.AutoCompleteDelay)
                );
                yield return CadPrompt.ForKeyword("Enter an input search Option", options);

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                string option = c.Input.Text;

                if (option == "Delay")
                {
                    while (true)
                    {
                        yield return CadPrompt.ForDistance(
                            "Specify the delay time in seconds",
                            null,
                            null,
                            Seconds(_settings.AutoCompleteDelay)
                        );

                        if (c.Input.Kind != CadInputKind.Number || double.IsNaN(c.Input.Number))
                        {
                            break;
                        }

                        if (c.Input.Number > 10.0)
                        {
                            c.Write("Invalid. Maximum delay is 10 seconds");
                            continue;
                        }

                        if (c.Input.Number >= 0.1)
                        {
                            int milliseconds = (int)Math.Round(c.Input.Number * 1000.0);
                            ChangeSettings(s => s.AutoCompleteDelay = milliseconds);
                        }

                        break;
                    }

                    continue;
                }

                int bit =
                    option == "autoComplete" ? 1
                    : option == "autocoRrect" ? 2
                    : option == "System variables" ? 4
                    : option == "conTent" ? 8
                    : 16;
                string question =
                    bit == 1 ? "Automatically complete a command as you type?"
                    : bit == 2 ? "Automatically correct a mistake as you type?"
                    : bit == 4 ? "Search system variables as you type?"
                    : bit == 8 ? "Search content in the drawing as you type?"
                    : "Enable mid-string search?";
                bool on = (_settings.InputSearchFlags & bit) != 0;
                yield return CadPrompt.ForKeyword(question, new[] { "Yes", "No" }, on ? "Yes" : "No");

                if (c.Input.Is("Yes") || c.Input.Is("No"))
                {
                    bool wanted = c.Input.Is("Yes");
                    ChangeSettings(s =>
                        s.InputSearchFlags = wanted ? s.InputSearchFlags | bit : s.InputSearchFlags & ~bit
                    );
                }
            }
        }

        /// <summary>A delay in milliseconds as -INPUTSEARCHOPTIONS writes it, in seconds to two decimals: 300 is 0.30.</summary>
        private static string Seconds(int milliseconds) =>
            (milliseconds / 1000.0).ToString("0.00", CultureInfo.InvariantCulture);

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
                candidates.Add(
                    new Candidate(command.Name.ToUpperInvariant(), command.Name.ToUpperInvariant(), false, null)
                );
            }

            foreach (KeyValuePair<string, string> synonym in Synonyms)
            {
                if (registry.Find(synonym.Key) == null && registry.Find(synonym.Value) is CadCommand target)
                {
                    candidates.Add(
                        new Candidate(
                            synonym.Key,
                            target.Name.ToUpperInvariant(),
                            false,
                            target.Name.ToUpperInvariant()
                        )
                    );
                }
            }

            if (_settings.SearchSystemVariables)
            {
                foreach (CadSystemVariable variable in registry.Variables)
                {
                    if (registry.Find(variable.Name) == null)
                    {
                        candidates.Add(
                            new Candidate(
                                variable.Name.ToUpperInvariant(),
                                variable.Name.ToUpperInvariant(),
                                true,
                                null
                            )
                        );
                    }
                }
            }

            void Add(Candidate candidate, int hit, string note, bool midString)
            {
                if (rows.Count < MostRows && taken.Add(candidate.Name))
                {
                    rows.Add(
                        new CommandSuggestionViewModel(
                            candidate.Name,
                            candidate.Command,
                            candidate.IsVariable,
                            hit,
                            key.Length,
                            note ?? (candidate.IsVariable ? "system variable" : string.Empty),
                            candidate.SynonymOf,
                            midString,
                            candidate.IsVariable ? null : IconOf(candidate.Command)
                        )
                    );
                }
            }

            // First: the command of that name, the command an alias names, the command AutoCorrect remembers.
            Candidate exact = candidates.FirstOrDefault(c => c.SynonymOf == null && c.Name == key);

            if (exact != null)
            {
                Add(exact, 0, null, false);
            }

            if (
                aliases.TryGetValue(key, out string aliased)
                && candidates.FirstOrDefault(c => c.SynonymOf == null && c.Name == aliased.ToUpperInvariant())
                    is Candidate alias
            )
            {
                Add(alias, Math.Max(0, alias.Name.IndexOf(key, StringComparison.Ordinal)), "alias " + key, false);
            }

            string corrected = _settings.AutoCorrect ? _settings.CorrectionOf(key) : null;

            if (
                corrected != null
                && candidates.FirstOrDefault(c => c.SynonymOf == null && c.Name == corrected) is Candidate correction
            )
            {
                Add(
                    correction,
                    Math.Max(0, correction.Name.IndexOf(key, StringComparison.Ordinal)),
                    "AutoCorrect",
                    false
                );
            }

            // Alphabetically of Input Search Options: no count of uses, every name by its letters. Separate commands and
            // system variables: the variables kept for a group of their own at the end.
            bool alphabetical = _settings.SortAlphabetically;
            bool apart = _settings.SeparateSystemVariables;
            List<Candidate> named = apart ? candidates.Where(c => !c.IsVariable).ToList() : candidates;

            // Then the names that begin with the letters: commands and synonyms by use, then by name; then the variables.
            foreach (
                Candidate candidate in named
                    .Where(c => c.Name.StartsWith(key, StringComparison.Ordinal))
                    .OrderBy(c => c.IsVariable)
                    .ThenByDescending(c => alphabetical ? 0 : Uses(c.Command))
                    .ThenBy(c => c.Name, StringComparer.Ordinal)
            )
            {
                Add(candidate, 0, null, false);
            }

            // Then, below a rule, the names with the letters inside: by use, by where the letters stand, by name.
            if (_settings.MidStringSearch)
            {
                AddInside(named);
            }

            // The variables apart, under a header of their own below a rule, collapsed until Tab: those that begin with the
            // letters, then those with them inside.
            if (apart)
            {
                List<Candidate> variables = candidates.Where(c => c.IsVariable).ToList();
                int before = rows.Count;

                foreach (
                    Candidate candidate in variables
                        .Where(c => c.Name.StartsWith(key, StringComparison.Ordinal))
                        .OrderBy(c => c.Name, StringComparer.Ordinal)
                )
                {
                    Add(candidate, 0, null, false);
                }

                if (_settings.MidStringSearch)
                {
                    AddInside(variables, false);
                }

                if (rows.Count > before)
                {
                    var header = new CommandSuggestionViewModel(
                        "System Variables",
                        string.Empty,
                        false,
                        0,
                        0,
                        (rows.Count - before).ToString(CultureInfo.InvariantCulture),
                        null,
                        before > 0,
                        null,
                        isHeader: true
                    );

                    for (int i = before; i < rows.Count; i++)
                    {
                        rows[i].Header = header;
                    }

                    rows.Insert(before, header);
                }
            }

            return rows;

            void AddInside(List<Candidate> from, bool rule = true)
            {
                bool first = rule;

                foreach (
                    Candidate candidate in from.Where(c =>
                            c.Name.IndexOf(key, 1, StringComparison.Ordinal) > 0
                            && !c.Name.StartsWith(key, StringComparison.Ordinal)
                        )
                        .OrderByDescending(c => alphabetical ? 0 : Uses(c.Command))
                        .ThenBy(c => alphabetical ? 0 : c.Name.IndexOf(key, StringComparison.Ordinal))
                        .ThenBy(c => c.Name, StringComparer.Ordinal)
                )
                {
                    int count = rows.Count;
                    Add(
                        candidate,
                        candidate.Name.IndexOf(key, StringComparison.Ordinal),
                        null,
                        first && rows.Count > 0
                    );

                    if (rows.Count > count)
                    {
                        first = false;
                    }
                }
            }
        }

        /// <summary>
        /// The rows of Search Content for a name typed: the blocks, layers, hatch patterns, text styles and dimension styles of
        /// the drawing, in the order of <see cref="GlobalSettings.ContentTypes"/> (AutoCAD 2021's Content Type list), a kind not
        /// searched left out. In each kind, the names that begin with
        /// the letters, then, with Mid-string Search, those with the letters inside, each by name; a kind's keyword typed
        /// (BLOCK or INSERT, LAYER, HATCH, TEXTSTYLE or STYLE, DIMSTYLE) lists every name of the kind.
        /// </summary>
        /// <param name="typed">What is typed.</param>
        /// <param name="first">Whether the rows are the first of the list, so that no rule is drawn above them.</param>
        private List<CommandSuggestionViewModel> SuggestContent(string typed, bool first)
        {
            const int MostRows = 100;
            string key = typed.ToUpperInvariant();
            DxfDatabase database = _current.Document.Database;
            var rows = new List<CommandSuggestionViewModel>();
            var kinds = new[]
            {
                new ContentKind(
                    "Block",
                    new[] { "BLOCK", "INSERT" },
                    "-INSERT",
                    "GhIconInsertBlock",
                    CadBlocks.Named(database).Select(b => b.Name)
                ),
                new ContentKind("Layer", new[] { "LAYER" }, "CLAYER", "GhIconLayers", _layers.Select(l => l.Name)),
                new ContentKind(
                    "Hatch",
                    new[] { "HATCH" },
                    "HATCH",
                    IconOf("HATCH"),
                    CadPatternFile.BuiltIn.Select(p => p.Name)
                ),
                new ContentKind(
                    "Text Style",
                    new[] { "TEXTSTYLE", "STYLE" },
                    "TEXTSTYLE",
                    "GhIconText",
                    CadTextStyleEditor.Styles(database).Select(s => s.Name)
                ),
                new ContentKind(
                    "Dim Style",
                    new[] { "DIMSTYLE" },
                    "DIMSTYLE",
                    "GhIconDimension",
                    CadDimStyleEditor.Styles(database).Select(s => s.Name)
                ),
            };

            // In the order of Input Search Options' Content type list, the kinds not searched left out; Visual Style, of which
            // the app's drawings have none, finds nothing.
            var ordered = _settings
                .ContentTypes.Where(t => t.IsSearched)
                .Select(t => kinds.FirstOrDefault(k => k.Name == t.Name))
                .Where(k => k != null)
                .ToList();

            int listed = 0;

            foreach (ContentKind kind in ordered)
            {
                bool every = kind.Keywords.Contains(key);
                IEnumerable<string> names = kind
                    .Names.Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                IEnumerable<string> begin = names.Where(n => n.StartsWith(key, StringComparison.OrdinalIgnoreCase));
                IEnumerable<string> inside =
                    _settings.MidStringSearch || every
                        ? names.Where(n =>
                            !n.StartsWith(key, StringComparison.OrdinalIgnoreCase)
                            && (every || n.IndexOf(key, StringComparison.OrdinalIgnoreCase) > 0)
                        )
                        : Enumerable.Empty<string>();

                List<string> found = begin
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(n => n, StringComparer.Ordinal)
                    .Concat(
                        inside.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ThenBy(n => n, StringComparer.Ordinal)
                    )
                    .Take(MostRows - listed)
                    .ToList();

                if (found.Count == 0)
                {
                    continue;
                }

                // The kind's header: its name, its count at the right, expanded unless the user collapsed the kind.
                var header = new CommandSuggestionViewModel(
                    kind.Plural,
                    string.Empty,
                    false,
                    0,
                    0,
                    found.Count.ToString(CultureInfo.InvariantCulture),
                    null,
                    false,
                    kind.IconKey,
                    null,
                    !first && rows.Count == 0,
                    null,
                    true
                )
                {
                    GroupKind = kind.Name,
                    IsExpanded = !_collapsedKinds.Contains(kind.Name),
                };
                rows.Add(header);

                foreach (string name in found)
                {
                    int hit = Math.Max(0, name.IndexOf(key, StringComparison.OrdinalIgnoreCase));
                    int length = name.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0 ? 0 : key.Length;
                    string chosen = name;
                    rows.Add(
                        new CommandSuggestionViewModel(
                            name,
                            kind.Command,
                            false,
                            hit,
                            length,
                            kind.Name,
                            null,
                            false,
                            kind.IconKey,
                            kind.Name,
                            false,
                            () => ActOnContent(kind.Name, chosen),
                            false,
                            kind.Name == "Hatch" ? () => HatchThumbnail(chosen) : (Func<ImageSource>)null
                        )
                        {
                            Header = header,
                        }
                    );
                }

                listed += found.Count;

                if (listed == MostRows)
                {
                    break;
                }
            }

            return rows;
        }

        /// <summary>
        /// The thumbnail of a hatch pattern of the app's own set, shown when the pointer is over its row: a square of 25 units
        /// hatched with the pattern at scale 1, drawn by the canvas's renderer on the dark of the dialogs.
        /// </summary>
        private static ImageSource HatchThumbnail(string pattern)
        {
            try
            {
                var settings = new CadHatchSettings { Type = CadHatchType.Pattern, PatternName = pattern };
                return HatchSettingsViewModel.Render(
                    settings.Preview(null),
                    CommandSuggestionViewModel.ThumbnailSize,
                    CommandSuggestionViewModel.ThumbnailSize
                );
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                GeometryHelperLog.Debug(
                    "MainViewModel: the thumbnail of " + pattern + " was not drawn (" + e.Message + ")."
                );
                return null;
            }
        }

        /// <summary>
        /// A named object of Search Content chosen, as AutoCAD 2021 acts on it: a layer, a text style or a dimension style made
        /// current, a block inserted by -INSERT, a hatch pattern made HATCH's and HATCH started.
        /// </summary>
        private void ActOnContent(string kind, string name)
        {
            switch (kind)
            {
                case "Layer":
                    StartWithAnswer("CLAYER", name);
                    break;
                case "Block":
                    StartWithAnswer("-INSERT", name);
                    break;
                case "Text Style":
                    StartWithAnswer("TEXTSTYLE", name);
                    break;
                case "Dim Style":
                    CurrentDimStyle = name;
                    break;
                case "Hatch":
                    CadHatchSettings hatch = _current.Document.HatchSettings;
                    hatch.Type = CadHatchType.Pattern;
                    hatch.PatternName = name;
                    hatch.PatternFile = null;
                    StartCommand("HATCH");
                    break;
            }
        }

        /// <summary>Starts a command and answers its first prompt with a name, spaces and all, as a line typed would.</summary>
        private void StartWithAnswer(string command, string answer)
        {
            StartCommand(command);

            if (_current.Session.IsRunning)
            {
                _current.Session.Feed(answer);
                SyncHistory();
                RunAfterCommand();
            }
        }

        private int Uses(string name) => _settings.CommandUses.TryGetValue(name, out int count) ? count : 0;

        /// <summary>The icon of a command: its own where the icon set has one, the layers' for the LAY commands, the prompt's for any other.</summary>
        private static string IconOf(string command) =>
            Icons.TryGetValue(command, out string key) ? key
            : command.StartsWith("LAY", StringComparison.Ordinal) ? "GhIconLayers"
            : command.StartsWith("DIM", StringComparison.Ordinal) ? "GhIconDimension"
            : "GhIconPrompt";

        /// <summary>A kind of named object Search Content lists: its name, the keywords that list it whole, the command that acts on it, its icon and the names of the drawing.</summary>
        private sealed class ContentKind
        {
            public ContentKind(
                string name,
                string[] keywords,
                string command,
                string iconKey,
                IEnumerable<string> names
            )
            {
                Name = name;
                Keywords = keywords;
                Command = command;
                IconKey = iconKey;
                Names = names.ToList();
            }

            public string Name { get; }

            /// <summary>The name of the kind's header: Blocks, Layers, Hatch Patterns, Text Styles, Dimension Styles.</summary>
            public string Plural =>
                Name == "Hatch" ? "Hatch Patterns"
                : Name == "Dim Style" ? "Dimension Styles"
                : Name + "s";

            public string[] Keywords { get; }

            public string Command { get; }

            public string IconKey { get; }

            public IReadOnlyList<string> Names { get; }
        }

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
