using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A file of the Recent Documents list, as the application menu and the start screen show it: its path, when it was last
    /// opened and whether it is pinned.
    /// </summary>
    public sealed class GlobalRecentFile
    {
        /// <summary>Makes a file of the list.</summary>
        /// <param name="path">The full path of the file.</param>
        /// <param name="opened">When it was last opened, in UTC.</param>
        /// <param name="isPinned">Whether it is pinned, kept at the top of the list whatever its date.</param>
        /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
        public GlobalRecentFile(string path, DateTime opened, bool isPinned)
        {
            Path = string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentException("A recent file needs a path.", nameof(path))
                : path.Trim();
            Opened = opened.Kind == DateTimeKind.Utc ? opened : opened.ToUniversalTime();
            IsPinned = isPinned;
        }

        /// <summary>Gets the full path of the file.</summary>
        public string Path { get; }

        /// <summary>Gets when the file was last opened, in UTC.</summary>
        public DateTime Opened { get; }

        /// <summary>Gets whether the file is pinned.</summary>
        public bool IsPinned { get; }

        /// <inheritdoc/>
        public override string ToString() => Path;
    }

    /// <summary>
    /// A command name typed wrongly and the command typed next, with how many times that has happened: once it has happened
    /// <see cref="GlobalSettings.AutoCorrectAfter"/> times, the command line offers the command first when the misspelling is
    /// typed again, as AutoCAD's AutoCorrect does. LIEN typed three times, each followed by LINE, has LINE offered for LIEN.
    /// </summary>
    public sealed class GlobalAutoCorrection
    {
        /// <summary>Makes a correction.</summary>
        /// <param name="typed">The misspelling, as LIEN.</param>
        /// <param name="command">The command typed after it, as LINE.</param>
        /// <param name="count">How many times the misspelling was followed by the command, at least 1.</param>
        /// <exception cref="ArgumentException">Thrown when the misspelling or the command is empty, or the count below 1.</exception>
        public GlobalAutoCorrection(string typed, string command, int count)
        {
            Typed = string.IsNullOrWhiteSpace(typed)
                ? throw new ArgumentException("A correction needs the name typed.", nameof(typed))
                : typed.Trim().ToUpperInvariant();
            Command = string.IsNullOrWhiteSpace(command)
                ? throw new ArgumentException("A correction needs a command.", nameof(command))
                : command.Trim().ToUpperInvariant();
            Count =
                count >= 1
                    ? count
                    : throw new ArgumentException("A correction is counted at least once.", nameof(count));
        }

        /// <summary>Gets the misspelling, in capitals.</summary>
        public string Typed { get; }

        /// <summary>Gets the command typed after it, in capitals.</summary>
        public string Command { get; }

        /// <summary>Gets how many times the misspelling was followed by the command.</summary>
        public int Count { get; }

        /// <summary>Gets whether it has happened often enough to be offered.</summary>
        public bool IsRemembered => Count >= GlobalSettings.AutoCorrectAfter;
    }

    /// <summary>
    /// The settings of the command line's input (its right-click menu's Input Settings) and what the app remembers between
    /// sessions: how often each command was used, the misspellings corrected and the recent files.
    /// </summary>
    public sealed partial class GlobalSettings
    {
        /// <summary>How many times a misspelling has to be followed by one command before AutoCorrect offers it: 3, as AutoCAD.</summary>
        public const int AutoCorrectAfter = 3;

        /// <summary>How many recent files the lists show, the pinned ones first: 9, as AutoCAD's "Number of recently-used files".</summary>
        public const int RecentFilesShown = 9;

        // Misspellings not yet remembered are kept up to this many, the oldest dropped first.
        private const int PendingCorrectionsKept = 100;

        private int _autoCompleteDelay = 300;
        private SortedDictionary<string, int> _uses = new SortedDictionary<string, int>(
            StringComparer.OrdinalIgnoreCase
        );
        private List<GlobalAutoCorrection> _corrections = new List<GlobalAutoCorrection>();
        private List<GlobalRecentFile> _recent = new List<GlobalRecentFile>();
        private List<string> _recentCommands = new List<string>();
        private List<CadPaper> _pixelSizes = new List<CadPaper>();

        // ---- Input settings

        /// <summary>Gets or sets whether the command line lists the commands and variables that match what is typed: on by default.</summary>
        public bool AutoComplete { get; set; } = true;

        /// <summary>Gets or sets whether a misspelling corrected <see cref="AutoCorrectAfter"/> times is offered its command: on by default.</summary>
        public bool AutoCorrect { get; set; } = true;

        /// <summary>Gets or sets whether the list holds system variables as well as commands: on by default.</summary>
        public bool SearchSystemVariables { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the list holds the drawing's named objects too, below the commands and variables: its blocks,
        /// layers, hatch patterns, text styles and dimension styles, as AutoCAD 2021's Search Content: on by default.
        /// </summary>
        public bool SearchContent { get; set; } = true;

        /// <summary>Gets or sets whether the list holds names with the letters inside them, below those that begin with them: on by default.</summary>
        public bool MidStringSearch { get; set; } = true;

        /// <summary>
        /// Gets or sets how long the command line waits after a key before it lists what matches, in milliseconds, 100 to
        /// 10 000: 300 by default, as AutoCAD's INPUTSEARCHDELAY.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range.</exception>
        public int AutoCompleteDelay
        {
            get => _autoCompleteDelay;
            set => _autoCompleteDelay = (int)Range(value, 100, 10000, nameof(AutoCompleteDelay));
        }

        // ---- Remembered between sessions

        /// <summary>Gets how many times each command was used, by name in capitals; a command never used is not listed.</summary>
        public IReadOnlyDictionary<string, int> CommandUses => _uses;

        /// <summary>Gets the misspellings followed by a command, in the order they were first seen.</summary>
        public IReadOnlyList<GlobalAutoCorrection> AutoCorrections => _corrections;

        /// <summary>
        /// Gets the recent files: the pinned ones first in the order they were pinned, then the others by the date they were
        /// opened, newest first, the newest <see cref="RecentFilesShown"/> of them kept. The lists show the first
        /// <see cref="RecentFilesShown"/> of these, and every pinned file where more are pinned.
        /// </summary>
        public IReadOnlyList<GlobalRecentFile> RecentFiles => _recent;

        /// <summary>
        /// Gets the commands run last, the newest first, each once, <see cref="RecentCommandsKept"/> at most, by name in
        /// capitals: the command line menu's Recent Commands, kept from one session to the next as AutoCAD keeps them.
        /// </summary>
        public IReadOnlyList<string> RecentCommands => _recentCommands;

        /// <summary>How many commands Recent Commands keeps: 20, as AutoCAD's.</summary>
        public const int RecentCommandsKept = 20;

        /// <summary>
        /// Gets the sizes of an image made with Custom… of a raster device's Paper size list, the newest first, each once,
        /// <see cref="CustomPixelSizesKept"/> at most: "2480 x 1754 Pixels (Custom)", listed after the sizes built in and
        /// kept from one session to the next.
        /// </summary>
        public IReadOnlyList<CadPaper> CustomPixelSizes => _pixelSizes;

        /// <summary>How many sizes of an image of the user's own <see cref="CustomPixelSizes"/> keeps: 10.</summary>
        public const int CustomPixelSizesKept = 10;

        /// <summary>Puts a size of an image first in <see cref="CustomPixelSizes"/>, once, the oldest past <see cref="CustomPixelSizesKept"/> dropped; a size built in is not kept.</summary>
        /// <param name="size">The size, as <see cref="CadPaper.Pixels"/> makes it.</param>
        /// <exception cref="ArgumentNullException">Thrown when the size is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the size is not in pixels.</exception>
        public void NoteCustomPixelSize(CadPaper size)
        {
            if (size == null)
            {
                throw new ArgumentNullException(nameof(size));
            }

            if (!size.IsPixels || size.IsCustomChoice)
            {
                throw new ArgumentException("A size of an image in pixels is kept.", nameof(size));
            }

            if (CadPaper.PixelSizes.Any(p => p.IsSameAs(size)))
            {
                return;
            }

            _pixelSizes.RemoveAll(p => p.IsSameAs(size));
            _pixelSizes.Insert(0, size);

            if (_pixelSizes.Count > CustomPixelSizesKept)
            {
                _pixelSizes.RemoveRange(CustomPixelSizesKept, _pixelSizes.Count - CustomPixelSizesKept);
            }
        }

        /// <summary>Puts a command first in <see cref="RecentCommands"/>, once, the oldest past <see cref="RecentCommandsKept"/> dropped.</summary>
        /// <param name="name">The command's name, as LINE.</param>
        public void NoteRecentCommand(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            string key = name.Trim().ToUpperInvariant();
            _recentCommands.Remove(key);
            _recentCommands.Insert(0, key);

            if (_recentCommands.Count > RecentCommandsKept)
            {
                _recentCommands.RemoveRange(RecentCommandsKept, _recentCommands.Count - RecentCommandsKept);
            }
        }

        /// <summary>Counts a use of a command.</summary>
        /// <param name="name">The command's name, as LINE.</param>
        public void CountCommandUse(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            string key = name.Trim().ToUpperInvariant();
            _uses[key] = _uses.TryGetValue(key, out int count) && count < int.MaxValue ? count + 1 : Math.Max(count, 1);
        }

        /// <summary>
        /// Notes that a misspelling was followed by a command. The third time LIEN is followed by LINE, LINE is remembered for
        /// LIEN. A name typed and then typed again the same is no misspelling, and is not noted.
        /// </summary>
        /// <param name="typed">The misspelling.</param>
        /// <param name="command">The command typed after it.</param>
        /// <returns>True when the command is remembered for the misspelling from now on.</returns>
        public bool NoteCorrection(string typed, string command)
        {
            if (
                string.IsNullOrWhiteSpace(typed)
                || string.IsNullOrWhiteSpace(command)
                || string.Equals(typed.Trim(), command.Trim(), StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            var noted = new GlobalAutoCorrection(typed, command, 1);
            int index = _corrections.FindIndex(c => c.Typed == noted.Typed && c.Command == noted.Command);

            if (index >= 0)
            {
                GlobalAutoCorrection old = _corrections[index];
                _corrections[index] = new GlobalAutoCorrection(
                    old.Typed,
                    old.Command,
                    old.Count == int.MaxValue ? old.Count : old.Count + 1
                );
                return _corrections[index].IsRemembered;
            }

            _corrections.Add(noted);

            while (_corrections.Count(c => !c.IsRemembered) > PendingCorrectionsKept)
            {
                _corrections.RemoveAt(_corrections.FindIndex(c => !c.IsRemembered));
            }

            return noted.IsRemembered;
        }

        /// <summary>Gets the command remembered for a misspelling; where several are, the one that followed it most, then the first by name.</summary>
        /// <param name="typed">The name typed.</param>
        /// <returns>The command in capitals, or null.</returns>
        public string CorrectionOf(string typed)
        {
            string key = (typed ?? string.Empty).Trim().ToUpperInvariant();
            return _corrections
                .Where(c => c.IsRemembered && c.Typed == key)
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.Command, StringComparer.Ordinal)
                .Select(c => c.Command)
                .FirstOrDefault();
        }

        /// <summary>
        /// Notes that a file was opened: it goes to the top of the files not pinned, or keeps its place where it is pinned, its
        /// date the one given. Past <see cref="RecentFilesShown"/> files not pinned, the oldest are dropped.
        /// </summary>
        /// <param name="path">The full path of the file.</param>
        /// <param name="opened">When it was opened.</param>
        public void NoteFileOpened(string path, DateTime opened)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            int index = IndexOfRecent(path);
            bool pinned = index >= 0 && _recent[index].IsPinned;
            var file = new GlobalRecentFile(path, opened, pinned);

            if (index >= 0)
            {
                _recent[index] = file;
            }
            else
            {
                _recent.Add(file);
            }

            OrderRecent();
        }

        /// <summary>
        /// Pins a recent file, putting it after the files pinned before it, or unpins it, putting it back among the others by
        /// its date.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="pinned">Whether to pin it.</param>
        /// <returns>False where the file is not in the list or is already so.</returns>
        public bool SetFilePinned(string path, bool pinned)
        {
            int index = IndexOfRecent(path);

            if (index < 0 || _recent[index].IsPinned == pinned)
            {
                return false;
            }

            GlobalRecentFile old = _recent[index];
            _recent.RemoveAt(index);
            int lastPinned = _recent.FindLastIndex(f => f.IsPinned);
            _recent.Insert(pinned ? lastPinned + 1 : _recent.Count, new GlobalRecentFile(old.Path, old.Opened, pinned));
            OrderRecent();
            return true;
        }

        /// <summary>Takes a file off the recent files, pinned or not, as a file chosen there and no longer found is.</summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>False where the file is not in the list.</returns>
        public bool RemoveRecentFile(string path)
        {
            int index = IndexOfRecent(path);

            if (index < 0)
            {
                return false;
            }

            _recent.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Takes the records of other settings: the command uses, the misspellings, the recent files and the recent commands,
        /// as Global settings' Reset keeps them while putting every setting back to its default.
        /// </summary>
        /// <param name="other">The settings whose records are taken.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public void CopyRecordsFrom(GlobalSettings other)
        {
            (other ?? throw new ArgumentNullException(nameof(other))).CopyRecordsInto(this);
        }

        private void CopyRecordsInto(GlobalSettings copy)
        {
            copy._uses = new SortedDictionary<string, int>(_uses, StringComparer.OrdinalIgnoreCase);
            copy._corrections = new List<GlobalAutoCorrection>(_corrections);
            copy._recent = new List<GlobalRecentFile>(_recent);
            copy._recentCommands = new List<string>(_recentCommands);
            copy._pixelSizes = new List<CadPaper>(_pixelSizes);
        }

        private int IndexOfRecent(string path)
        {
            string key = (path ?? string.Empty).Trim();
            return _recent.FindIndex(f => string.Equals(f.Path, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The pinned files first in their order, then the others newest first, ties by path; the oldest beyond the count dropped.</summary>
        private void OrderRecent()
        {
            List<GlobalRecentFile> pinned = _recent.Where(f => f.IsPinned).ToList();
            IEnumerable<GlobalRecentFile> others = _recent
                .Where(f => !f.IsPinned)
                .OrderByDescending(f => f.Opened)
                .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Path, StringComparer.Ordinal)
                .Take(RecentFilesShown);
            _recent = pinned.Concat(others).ToList();
        }

        private void WriteInputAndRecords(JsonText w)
        {
            w.Open("input");
            w.Value("autoComplete", AutoComplete);
            w.Value("autoCorrect", AutoCorrect);
            w.Value("searchSystemVariables", SearchSystemVariables);
            w.Value("searchContent", SearchContent);
            w.Value("midStringSearch", MidStringSearch);
            w.Value("delayMilliseconds", _autoCompleteDelay);
            w.Close();
            w.Open("commandUses");

            foreach (KeyValuePair<string, int> use in _uses)
            {
                w.Value(use.Key, use.Value);
            }

            w.Close();
            w.OpenList("autoCorrections");

            foreach (GlobalAutoCorrection correction in _corrections)
            {
                w.Open(null);
                w.Value("typed", correction.Typed);
                w.Value("command", correction.Command);
                w.Value("count", correction.Count);
                w.Close();
            }

            w.CloseList();
            w.OpenList("recentFiles");

            foreach (GlobalRecentFile file in _recent)
            {
                w.Open(null);
                w.Value("path", file.Path);
                w.Value("opened", file.Opened.ToString("o", CultureInfo.InvariantCulture));
                w.Value("pinned", file.IsPinned);
                w.Close();
            }

            w.CloseList();
            w.OpenList("recentCommands");

            foreach (string command in _recentCommands)
            {
                w.Value(null, command);
            }

            w.CloseList();
            w.OpenList("customPixelSizes");

            foreach (CadPaper size in _pixelSizes)
            {
                w.Value(
                    null,
                    ((int)size.Width).ToString(CultureInfo.InvariantCulture)
                        + "x"
                        + ((int)size.Height).ToString(CultureInfo.InvariantCulture)
                );
            }

            w.CloseList();
        }

        /// <summary>Reads the command uses, the misspellings, the recent files and the recent commands; an entry that does not read is left out with a Warn.</summary>
        private void ReadRecords(XElement root, string path)
        {
            foreach (XElement use in root.Element("commandUses")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                string name = (string)use.Attribute("item") ?? use.Name.LocalName;

                if (
                    int.TryParse(use.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                    && count > 0
                    && name.Trim().Length > 0
                )
                {
                    _uses[name.Trim().ToUpperInvariant()] = count;
                }
                else
                {
                    GeometryHelperLog.Warn(
                        "GlobalSettings: the use count of " + name + " in " + path + " is not valid; it is left out."
                    );
                }
            }

            foreach (XElement item in root.Element("autoCorrections")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                try
                {
                    _corrections.Add(
                        new GlobalAutoCorrection(
                            item.Element("typed")?.Value,
                            item.Element("command")?.Value,
                            int.Parse(
                                item.Element("count")?.Value ?? "0",
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture
                            )
                        )
                    );
                }
                catch (Exception exception)
                    when (exception is ArgumentException
                        || exception is FormatException
                        || exception is OverflowException
                    )
                {
                    GeometryHelperLog.Warn(
                        "GlobalSettings: a correction of "
                            + path
                            + " is not valid ("
                            + exception.Message.Split('\n')[0].Trim()
                            + "); it is left out."
                    );
                }
            }

            foreach (XElement item in root.Element("recentFiles")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                try
                {
                    var file = new GlobalRecentFile(
                        item.Element("path")?.Value,
                        DateTime.Parse(
                            item.Element("opened")?.Value ?? string.Empty,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind
                        ),
                        string.Equals(item.Element("pinned")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                    );

                    if (IndexOfRecent(file.Path) < 0)
                    {
                        _recent.Add(file);
                    }
                }
                catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
                {
                    GeometryHelperLog.Warn(
                        "GlobalSettings: a recent file of "
                            + path
                            + " is not valid ("
                            + exception.Message.Split('\n')[0].Trim()
                            + "); it is left out."
                    );
                }
            }

            OrderRecent();

            foreach (XElement item in root.Element("recentCommands")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                string command = item.Value.Trim().ToUpperInvariant();

                if (
                    command.Length > 0
                    && !_recentCommands.Contains(command)
                    && _recentCommands.Count < RecentCommandsKept
                )
                {
                    _recentCommands.Add(command);
                }
            }

            foreach (XElement item in root.Element("customPixelSizes")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                string[] sides = item.Value.Split('x');

                if (
                    sides.Length == 2
                    && int.TryParse(sides[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width)
                    && int.TryParse(sides[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height)
                    && width >= CadPaper.MinPixels
                    && width <= CadPaper.MaxPixels
                    && height >= CadPaper.MinPixels
                    && height <= CadPaper.MaxPixels
                )
                {
                    CadPaper size = CadPaper.Pixels(width, height);

                    if (
                        CadPaper.PixelSizes.Contains(size)
                        || _pixelSizes.Any(p => p.IsSameAs(size))
                        || _pixelSizes.Count >= CustomPixelSizesKept
                    )
                    {
                        continue;
                    }

                    _pixelSizes.Add(size);
                }
                else
                {
                    GeometryHelperLog.Warn(
                        "GlobalSettings: the image size "
                            + item.Value
                            + " of "
                            + path
                            + " is not valid; it is left out."
                    );
                }
            }
        }
    }
}
