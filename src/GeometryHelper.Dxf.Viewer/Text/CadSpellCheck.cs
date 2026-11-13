using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What SPELL gives its Check Spelling dialog: where to check, the word found not in the dictionary with the words nearest
    /// it, and AutoCAD's answers to it (Ignore, Ignore All, Change, Change All, Add to Dictionary). The words are those of the
    /// built-in English list and of a custom dictionary file; a word of one letter, and those the settings name (with a digit
    /// or all in capitals until set otherwise), are passed over. Nothing is written into the drawing while the dialog is
    /// open: once it closes, SPELL writes the words changed, each text one edit of its one undo step.
    /// </summary>
    public sealed class CadSpellCheck
    {
        /// <summary>The place that checks every space of the drawing: model space and each layout.</summary>
        public const string EntireDrawing = "Entire drawing";

        /// <summary>The place that checks the space on screen.</summary>
        public const string CurrentSpace = "Current space/layout";

        /// <summary>The place that checks the objects picked with the dialog's Select objects button.</summary>
        public const string SelectedObjects = "Selected objects";

        internal delegate List<CadCommands.SpellItem> Collector(
            string where,
            IReadOnlyList<string> selected,
            out int locked
        );

        private readonly Collector _collect;
        private readonly HashSet<string> _custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _changeAll = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly List<string> _added = new List<string>();

        // The changes, by the text they are of (its object and attribute), in the order the texts were first checked.
        private readonly Dictionary<
            string,
            KeyValuePair<CadCommands.SpellItem, List<KeyValuePair<CadSpelling.Word, string>>>
        > _changes = new Dictionary<
            string,
            KeyValuePair<CadCommands.SpellItem, List<KeyValuePair<CadSpelling.Word, string>>>
        >(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _changeOrder = new List<string>();

        private List<CadCommands.SpellItem> _items = new List<CadCommands.SpellItem>();
        private List<CadSpelling.Word> _words = new List<CadSpelling.Word>();
        private int _item;
        private int _word;
        private string _where = EntireDrawing;
        private string _lockedNote = string.Empty;
        private CadSpellSettings _settings = new CadSpellSettings();
        private readonly Stack<State> _undo = new Stack<State>();

        internal CadSpellCheck(Collector collect)
        {
            _collect = collect;
        }

        /// <summary>Gets the places Where to check offers, as AutoCAD 2021 lists them.</summary>
        public static IReadOnlyList<string> Places { get; } = new[] { EntireDrawing, CurrentSpace, SelectedObjects };

        /// <summary>Gets or sets where to check, one of <see cref="Places"/>: the entire drawing until set.</summary>
        public string Where
        {
            get => _where;
            set => _where = Places.Contains(value) ? value : EntireDrawing;
        }

        /// <summary>Gets or sets the handles of the objects picked for <see cref="SelectedObjects"/>.</summary>
        public IReadOnlyList<string> SelectedHandles { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets whether the dialog closed to have objects picked, as its Select objects button does: SPELL asks
        /// "Select objects:" and shows the dialog again with them.
        /// </summary>
        public bool IsSelecting { get; set; }

        /// <summary>Gets the custom dictionary file read, whose words count as spelt right and to which Add to Dictionary appends; null for none.</summary>
        public string CustomDictionary { get; private set; }

        /// <summary>Gets whether a word not in the dictionary waits for an answer.</summary>
        public bool IsChecking { get; private set; }

        /// <summary>Gets the word not in the dictionary; empty when none waits.</summary>
        public string Word => IsChecking ? _words[_word].Text : string.Empty;

        /// <summary>Gets the text the word is in, as written; empty when none waits.</summary>
        public string Context => IsChecking ? _items[_item].Raw : string.Empty;

        /// <summary>Gets the words nearest the word, the best first; none when no word waits or none is near.</summary>
        public IReadOnlyList<string> Suggestions { get; private set; } = Array.Empty<string>();

        /// <summary>Gets the line the dialog shows under the buttons: which text is checked, or that the check is complete.</summary>
        public string Status { get; private set; } = string.Empty;

        /// <summary>Gets how many words are changed so far.</summary>
        public int ChangedCount => _changes.Values.Sum(p => p.Value.Count);

        /// <summary>Gets the words added to the dictionary, in the order they were added.</summary>
        public IReadOnlyList<string> Added => _added;

        /// <summary>The changes, by text, in the order the texts were checked.</summary>
        internal IEnumerable<
            KeyValuePair<CadCommands.SpellItem, List<KeyValuePair<CadSpelling.Word, string>>>
        > Changes => _changeOrder.Select(k => _changes[k]);

        /// <summary>Reads a custom dictionary file, its words one to a line, which then count as spelt right.</summary>
        /// <param name="path">The file.</param>
        /// <returns>Null where it was read; else the line saying why not.</returns>
        public string LoadDictionary(string path)
        {
            try
            {
                _custom.UnionWith(CadSpelling.ReadDictionary(path));
                CustomDictionary = path;
                return null;
            }
            catch (Exception e)
                when (e is IOException
                    || e is UnauthorizedAccessException
                    || e is ArgumentException
                    || e is NotSupportedException
                )
            {
                GeometryHelperLog.Warn("SPELL: the custom dictionary " + path + " was not read.", e);
                return "Cannot read custom dictionary.";
            }
        }

        /// <summary>
        /// Starts the check where <see cref="Where"/> says, from its first text: the first word not in the dictionary waits, or
        /// the check is complete. Words already changed keep their changes and are not asked again.
        /// </summary>
        /// <returns>Whether a word waits.</returns>
        public bool Start()
        {
            _items = (_collect(_where, SelectedHandles, out int locked) ?? new List<CadCommands.SpellItem>())
                .Where(i => Settings.BlockAttributes || i.Attribute == null)
                .ToList();
            _undo.Clear();
            _item = 0;
            _word = -1;
            _words =
                _items.Count > 0 ? CadSpelling.WordsOf(_items[0].Raw, _items[0].IsMText) : new List<CadSpelling.Word>();
            _lockedNote =
                locked > 0
                    ? locked.ToString(CultureInfo.InvariantCulture)
                        + (locked == 1 ? " was" : " were")
                        + " on a locked layer. "
                    : string.Empty;
            return Advance();
        }

        /// <summary>
        /// Gets or sets the settings: what is included and which words are passed over, as Check Spelling Settings gives them;
        /// they count from the next <see cref="Start"/>.
        /// </summary>
        public CadSpellSettings Settings
        {
            get => _settings;
            set => _settings = value ?? new CadSpellSettings();
        }

        /// <summary>Gets whether an answer can be taken back by <see cref="Undo"/>.</summary>
        public bool CanUndo => _undo.Count > 0;

        /// <summary>
        /// Undo of the dialog: the last answer since <see cref="Start"/> taken back, its word waiting again, the change it made,
        /// the word it ignored for all or added to the dictionary forgotten.
        /// </summary>
        /// <returns>Whether there was one to take back.</returns>
        public bool Undo()
        {
            if (_undo.Count == 0)
            {
                return false;
            }

            _undo.Pop().Restore(this);
            return true;
        }

        /// <summary>Leaves the word as it is, this once.</summary>
        public void Ignore()
        {
            if (IsChecking)
            {
                _undo.Push(new State(this));
            }

            Answered(null);
        }

        /// <summary>Leaves the word as it is here and wherever it comes again.</summary>
        public void IgnoreAll()
        {
            if (IsChecking)
            {
                _undo.Push(new State(this));
                _ignored.Add(Word);
            }

            Answered(null);
        }

        /// <summary>Changes the word to another, this once: the replacement typed, or the first suggestion where none is typed.</summary>
        /// <param name="replacement">The word to put in its place; empty for the first suggestion.</param>
        /// <returns>False where nothing was typed and there is no suggestion, the word still waiting.</returns>
        public bool Change(string replacement) => ChangeTo(replacement, false);

        /// <summary>Changes the word to another here and wherever it comes again, as <see cref="Change"/>.</summary>
        /// <param name="replacement">The word to put in its place; empty for the first suggestion.</param>
        /// <returns>False where nothing was typed and there is no suggestion, the word still waiting.</returns>
        public bool ChangeAll(string replacement) => ChangeTo(replacement, true);

        /// <summary>Adds the word to the custom dictionary, so that it is spelt right from now on, and to its file once SPELL ends.</summary>
        public void AddToDictionary()
        {
            if (IsChecking)
            {
                _undo.Push(new State(this));
            }

            if (IsChecking && _custom.Add(Word))
            {
                _added.Add(Word);
            }

            Answered(null);
        }

        private bool ChangeTo(string replacement, bool all)
        {
            if (!IsChecking)
            {
                return false;
            }

            string typed = (replacement ?? string.Empty).Trim();

            if (typed.Length == 0)
            {
                if (Suggestions.Count == 0)
                {
                    Status = "No suggestions.";
                    return false;
                }

                typed = Suggestions[0];
            }

            _undo.Push(new State(this));

            if (all)
            {
                _changeAll[Word] = typed;
            }

            Answered(typed);
            return true;
        }

        /// <summary>The waiting word answered, changed where a replacement is given; then the next one.</summary>
        private void Answered(string replacement)
        {
            if (!IsChecking)
            {
                return;
            }

            if (replacement != null)
            {
                Record(_items[_item], _words[_word], replacement);
            }

            Advance();
        }

        private void Record(CadCommands.SpellItem item, CadSpelling.Word word, string replacement)
        {
            string key = Key(item);

            if (!_changes.TryGetValue(key, out var changes))
            {
                changes = new KeyValuePair<CadCommands.SpellItem, List<KeyValuePair<CadSpelling.Word, string>>>(
                    item,
                    new List<KeyValuePair<CadSpelling.Word, string>>()
                );
                _changes[key] = changes;
                _changeOrder.Add(key);
            }

            changes.Value.Add(new KeyValuePair<CadSpelling.Word, string>(word, replacement));
        }

        private static string Key(CadCommands.SpellItem item) => item.Handle + "|" + (item.Attribute ?? string.Empty);

        /// <summary>Moves to the next word not in the dictionary, changing those Change All named on the way; false at the end.</summary>
        private bool Advance()
        {
            while (_item < _items.Count)
            {
                _word++;

                if (_word >= _words.Count)
                {
                    _item++;
                    _word = -1;
                    _words =
                        _item < _items.Count
                            ? CadSpelling.WordsOf(_items[_item].Raw, _items[_item].IsMText)
                            : new List<CadSpelling.Word>();
                    continue;
                }

                CadSpelling.Word word = _words[_word];
                CadCommands.SpellItem item = _items[_item];

                if (
                    Settings.IsPassedOver(word.Text)
                    || _ignored.Contains(word.Text)
                    || CadSpelling.IsKnown(word.Text, _custom)
                    || IsChanged(item, word)
                )
                {
                    continue;
                }

                if (_changeAll.TryGetValue(word.Text, out string replacement))
                {
                    Record(item, word, replacement);
                    continue;
                }

                IsChecking = true;
                Suggestions = CadSpelling.Suggest(word.Text, _custom);
                Status =
                    _lockedNote
                    + "Checking text "
                    + (_item + 1).ToString(CultureInfo.InvariantCulture)
                    + " of "
                    + _items.Count.ToString(CultureInfo.InvariantCulture)
                    + ".";
                return true;
            }

            IsChecking = false;
            Suggestions = Array.Empty<string>();
            Status = _lockedNote + "Spelling check complete.";
            return false;
        }

        private bool IsChanged(CadCommands.SpellItem item, CadSpelling.Word word) =>
            _changes.TryGetValue(Key(item), out var changes) && changes.Value.Any(p => p.Key.Start == word.Start);

        /// <summary>The check as it was before an answer, for Undo to put back.</summary>
        private sealed class State
        {
            private readonly int _item;
            private readonly int _word;
            private readonly List<CadSpelling.Word> _words;
            private readonly bool _checking;
            private readonly IReadOnlyList<string> _suggestions;
            private readonly string _status;
            private readonly List<string> _ignored;
            private readonly List<KeyValuePair<string, string>> _changeAll;
            private readonly List<string> _custom;
            private readonly List<string> _added;
            private readonly List<string> _changeOrder;
            private readonly List<
                KeyValuePair<string, KeyValuePair<CadCommands.SpellItem, List<KeyValuePair<CadSpelling.Word, string>>>>
            > _changes;

            internal State(CadSpellCheck check)
            {
                _item = check._item;
                _word = check._word;
                _words = check._words;
                _checking = check.IsChecking;
                _suggestions = check.Suggestions;
                _status = check.Status;
                _ignored = check._ignored.ToList();
                _changeAll = check._changeAll.ToList();
                _custom = check._custom.ToList();
                _added = check._added.ToList();
                _changeOrder = check._changeOrder.ToList();
                _changes = check
                    ._changes.Select(p => new KeyValuePair<
                        string,
                        KeyValuePair<CadCommands.SpellItem, List<KeyValuePair<CadSpelling.Word, string>>>
                    >(
                        p.Key,
                        new KeyValuePair<CadCommands.SpellItem, List<KeyValuePair<CadSpelling.Word, string>>>(
                            p.Value.Key,
                            p.Value.Value.ToList()
                        )
                    ))
                    .ToList();
            }

            internal void Restore(CadSpellCheck check)
            {
                check._item = _item;
                check._word = _word;
                check._words = _words;
                check.IsChecking = _checking;
                check.Suggestions = _suggestions;
                check.Status = _status;
                check._ignored.Clear();
                check._ignored.UnionWith(_ignored);
                check._changeAll.Clear();

                foreach (KeyValuePair<string, string> pair in _changeAll)
                {
                    check._changeAll[pair.Key] = pair.Value;
                }

                check._custom.Clear();
                check._custom.UnionWith(_custom);
                check._added.Clear();
                check._added.AddRange(_added);
                check._changeOrder.Clear();
                check._changeOrder.AddRange(_changeOrder);
                check._changes.Clear();

                foreach (var pair in _changes)
                {
                    check._changes[pair.Key] = pair.Value;
                }
            }
        }
    }
}
