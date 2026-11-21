using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The gate in front of every file of code the presenter loads, as AutoCAD 2021's: SECURELOAD says what happens to a file
    /// outside the trusted folders, TRUSTEDPATHS lists those folders and LEGACYCODESEARCH says whether a search for a code file
    /// looks in the current and the drawing folder. AutoLISP's <c>load</c> and everything built on it asks
    /// <see cref="MayLoad"/> before it reads a file, and the app's Python does the same for PYRUN and PYLOAD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SECURELOAD 0 loads from anywhere. 1, the start value, loads from a trusted folder and asks (<see cref="Ask"/>) about a
    /// file anywhere else: Always Load, Load Once or Do Not Load. 2 loads from a trusted folder only. The trusted folders are
    /// the folder of the program with every folder under it, and the entries of <see cref="TrustedPaths"/>: a folder, or a
    /// folder and every folder under it where the entry ends in <c>\...</c>. <c>D:\tools</c> trusts the files in
    /// <c>D:\tools</c> and not those in <c>D:\tools\old</c>; <c>D:\tools\...</c> trusts both.
    /// </para>
    /// <para>
    /// Folders are compared as Windows paths, in full and without regard to case. The instance can be used from any thread:
    /// the question itself is asked outside any lock, so it may show a window and wait.
    /// </para>
    /// </remarks>
    public sealed class CadLoadSecurity
    {
        private static readonly string[] CodeExtensions =
        {
            ".lsp",
            ".fas",
            ".vlx",
            ".mnl",
            ".scr",
            ".dcl",
            ".dll",
            ".arx",
            ".dvb",
            ".cuix",
            ".py",
        };

        private static readonly string Program = FolderOf(AppDomain.CurrentDomain.BaseDirectory);

        private readonly object _lock = new object();
        private readonly HashSet<string> _once = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _secureLoad = 1;
        private string _trustedPaths = string.Empty;
        private List<Trusted> _trusted = new List<Trusted>();
        private bool _legacyCodeSearch;
        private List<KeyValuePair<string, string>> _always = new List<KeyValuePair<string, string>>();

        /// <summary>
        /// Gets or sets what happens to a file of code outside the trusted folders: 0 loads it, 1 asks (<see cref="Ask"/>), 2
        /// refuses it. Starts at 1, as AutoCAD does.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not 0, 1 or 2.</exception>
        public int SecureLoad
        {
            get
            {
                lock (_lock)
                {
                    return _secureLoad;
                }
            }
            set
            {
                if (value < 0 || value > 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "SECURELOAD is 0, 1 or 2.");
                }

                bool changed;

                lock (_lock)
                {
                    changed = _secureLoad != value;
                    _secureLoad = value;
                }

                RaiseChanged(changed);
            }
        }

        /// <summary>
        /// Gets or sets the folders trusted besides the program's, as AutoCAD's TRUSTEDPATHS: entries parted by <c>;</c>, an
        /// entry that ends in <c>\...</c> trusting that folder and every folder under it and any other the folder alone, so
        /// <c>D:\tools;D:\lisp\...</c> trusts <c>D:\tools</c>, <c>D:\lisp</c> and everything below <c>D:\lisp</c>. Spaces around an
        /// entry are dropped and an empty entry is skipped; a <c>%NAME%</c> is replaced by the environment variable when the value
        /// is set; an entry in quotes, with a wildcard or not starting at a drive or a share trusts nothing. Empty at the start;
        /// null is taken as empty, and any text is kept.
        /// </summary>
        public string TrustedPaths
        {
            get
            {
                lock (_lock)
                {
                    return _trustedPaths;
                }
            }
            set
            {
                string text = Environment.ExpandEnvironmentVariables(value ?? string.Empty);
                List<Trusted> parsed = Parse(text);
                bool changed;

                lock (_lock)
                {
                    changed = !string.Equals(_trustedPaths, text, StringComparison.Ordinal);
                    _trustedPaths = text;
                    _trusted = parsed;
                }

                RaiseChanged(changed);
            }
        }

        /// <summary>
        /// Gets or sets whether a search for a file of code (<c>findfile</c>, <c>load_dialog</c>, <c>load</c>) looks in the
        /// current folder and the folder of the drawing before the support folders, as AutoCAD's LEGACYCODESEARCH 1. Off at the
        /// start: a <c>.lsp</c> file lying beside the drawing is not found by its bare name.
        /// </summary>
        public bool LegacyCodeSearch
        {
            get
            {
                lock (_lock)
                {
                    return _legacyCodeSearch;
                }
            }
            set
            {
                bool changed;

                lock (_lock)
                {
                    changed = _legacyCodeSearch != value;
                    _legacyCodeSearch = value;
                }

                RaiseChanged(changed);
            }
        }

        /// <summary>
        /// Gets the folder of the program, the one the presenter's own linetypes, hatches and Python package lie in, with no
        /// closing backslash. It is trusted with every folder under it, whatever <see cref="TrustedPaths"/> says.
        /// </summary>
        public static string ProgramFolder => Program;

        /// <summary>
        /// Gets or sets the files answered Always Load, each as the SHA-256 of its bytes in lower-case hexadecimal, a bar and its
        /// full path: <c>9f86d0...0a08|C:\work\tools\label.lsp</c>. Such a file loads under SECURELOAD 1 with no question for as
        /// long as its bytes are the same, so a file edited since is asked about again. An entry that is not of this form is left
        /// out, and a second entry for one path replaces the first. Null is taken as empty.
        /// </summary>
        public IReadOnlyList<string> AlwaysLoaded
        {
            get
            {
                lock (_lock)
                {
                    return _always.Select(e => e.Key + "|" + e.Value).ToList().AsReadOnly();
                }
            }
            set
            {
                var parsed = new List<KeyValuePair<string, string>>();

                foreach (string entry in value ?? Array.Empty<string>())
                {
                    int bar = entry == null ? -1 : entry.IndexOf('|');

                    if (bar != 64 || bar == entry.Length - 1 || !IsHex(entry, bar))
                    {
                        continue;
                    }

                    string hash = entry.Substring(0, bar).ToLowerInvariant();
                    string path = entry.Substring(bar + 1);
                    parsed.RemoveAll(e => string.Equals(e.Value, path, StringComparison.OrdinalIgnoreCase));
                    parsed.Add(new KeyValuePair<string, string>(hash, path));
                }

                bool changed;

                lock (_lock)
                {
                    changed = !_always.SequenceEqual(parsed);
                    _always = parsed;
                }

                RaiseChanged(changed);
            }
        }

        /// <summary>
        /// Gets or sets what asks about a file under SECURELOAD 1: it gets the full path and answers with one of
        /// <see cref="CadLoadAnswer"/>. The window shows AutoCAD's warning here. Null, as it is at the start, makes a library, a
        /// test or a console refuse the file, as AutoCAD's console does. An exception from it is a refusal and a line in the
        /// log.
        /// </summary>
        public Func<string, CadLoadAnswer> Ask { get; set; }

        /// <summary>
        /// Occurs when SECURELOAD, TRUSTEDPATHS, LEGACYCODESEARCH or <see cref="AlwaysLoaded"/> changes, which includes a file
        /// answered Always Load. Raised on the thread that made the change, after it; setting a value to what it is raises
        /// nothing.
        /// </summary>
        public event EventHandler Changed;

        /// <summary>
        /// Says whether a file lies in a trusted folder: the folder of the program and every folder under it, or a folder of
        /// <see cref="TrustedPaths"/>. Nothing is asked and the file need not exist.
        /// </summary>
        /// <param name="path">The file, in full or as the working folder reads it.</param>
        /// <returns>True when its folder is trusted; false too for a path that cannot be made into a full one.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public bool IsTrusted(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string full = Full(path);
            return full != null && IsTrustedFull(full);
        }

        /// <summary>
        /// Decides whether a file of code may be loaded, in this order: SECURELOAD 0 loads it; a file in a trusted folder loads;
        /// SECURELOAD 2 refuses any other, an Always Load answer given before notwithstanding; under SECURELOAD 1 a file answered
        /// Load Once in this run, or Always Load with the same bytes, loads, and any other is asked about (<see cref="Ask"/>),
        /// and refused where nothing asks. Each refusal is one line of the log at the Debug level.
        /// </summary>
        /// <param name="path">The file, in full or as the working folder reads it.</param>
        /// <returns>True when it may be loaded.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public bool MayLoad(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            int level = SecureLoad;

            if (level == 0)
            {
                return true;
            }

            string full = Full(path);

            if (full == null)
            {
                return Refused(path, level);
            }

            if (IsTrustedFull(full))
            {
                return true;
            }

            if (level >= 2)
            {
                return Refused(full, level);
            }

            if (Answered(full))
            {
                return true;
            }

            Func<string, CadLoadAnswer> ask = Ask;

            if (ask == null)
            {
                return Refused(full, level);
            }

            CadLoadAnswer answer;

            try
            {
                answer = ask(full);
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn(
                    "Load security: the question about " + full + " failed: " + exception.Message,
                    exception
                );
                return Refused(full, level);
            }

            if (answer == CadLoadAnswer.LoadOnce)
            {
                lock (_lock)
                {
                    _once.Add(full);
                }

                return true;
            }

            if (answer == CadLoadAnswer.AlwaysLoad)
            {
                Remember(full);
                return true;
            }

            return Refused(full, level);
        }

        /// <summary>
        /// Forgets every file answered Always Load, which AutoCAD gives no way to do: each is asked about again under SECURELOAD
        /// 1. A file answered Load Once in this run stays answered.
        /// </summary>
        public void ForgetAlwaysLoaded()
        {
            bool changed;

            lock (_lock)
            {
                changed = _always.Count > 0;
                _always = new List<KeyValuePair<string, string>>();
            }

            RaiseChanged(changed);
        }

        /// <summary>
        /// Says whether a file is one of the kinds AutoCAD keeps out of the current and the drawing folder while
        /// LEGACYCODESEARCH is 0: <c>.lsp .fas .vlx .mnl .scr .dcl .dll .arx .dvb .cuix</c>, and <c>.py</c> for Python. Only the
        /// extension is read, without regard to case, so <c>label.LSP</c> is one and <c>notes.txt</c> and <c>acad.lin</c> are not.
        /// </summary>
        /// <param name="path">A file name or path.</param>
        /// <returns>True for a file of code; false for null.</returns>
        public static bool IsCodeFile(string path)
        {
            if (path == null)
            {
                return false;
            }

            try
            {
                return Array.IndexOf(CodeExtensions, Path.GetExtension(path).ToLowerInvariant()) >= 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private bool IsTrustedFull(string full)
        {
            string folder = FolderOf(Path.GetDirectoryName(full) ?? string.Empty);

            if (Within(folder, Program, true))
            {
                return true;
            }

            List<Trusted> trusted;

            lock (_lock)
            {
                trusted = _trusted;
            }

            return trusted.Any(t => Within(folder, t.Folder, t.Below));
        }

        // A file answered Load Once in this run, or Always Load with the bytes it has now.
        private bool Answered(string full)
        {
            bool listed;

            lock (_lock)
            {
                if (_once.Contains(full))
                {
                    return true;
                }

                listed = _always.Any(e => string.Equals(e.Value, full, StringComparison.OrdinalIgnoreCase));
            }

            string hash = listed ? HashOf(full) : null;

            lock (_lock)
            {
                return hash != null
                    && _always.Any(e =>
                        string.Equals(e.Value, full, StringComparison.OrdinalIgnoreCase) && e.Key == hash
                    );
            }
        }

        private void Remember(string full)
        {
            string hash = HashOf(full);

            // A file that cannot be read has no bytes to remember: it loads this once and is asked about again.
            if (hash == null)
            {
                return;
            }

            lock (_lock)
            {
                var next = new List<KeyValuePair<string, string>>(_always);
                next.RemoveAll(e => string.Equals(e.Value, full, StringComparison.OrdinalIgnoreCase));
                next.Add(new KeyValuePair<string, string>(hash, full));
                _always = next;
            }

            RaiseChanged(true);
        }

        private static bool Refused(string path, int level)
        {
            GeometryHelperLog.Debug("Load security: refused " + path + " (SECURELOAD " + level + ")");
            return false;
        }

        private void RaiseChanged(bool changed)
        {
            if (changed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private static string HashOf(string path)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool IsHex(string text, int count)
        {
            for (int i = 0; i < count; i++)
            {
                char c = text[i];

                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F'))
                {
                    return false;
                }
            }

            return true;
        }

        private static string Full(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                return null;
            }
        }

        private static bool IsRooted(string path)
        {
            try
            {
                return Path.IsPathRooted(path);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        // The full path of a folder with no closing backslash, so a drive's own folder is C: here.
        private static string FolderOf(string folder) =>
            (Full(folder) ?? folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static bool Within(string folder, string trusted, bool below) =>
            string.Equals(folder, trusted, StringComparison.OrdinalIgnoreCase)
            || (below && folder.StartsWith(trusted + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        private static List<Trusted> Parse(string text)
        {
            var trusted = new List<Trusted>();

            foreach (string part in text.Split(';'))
            {
                string entry = part.Trim(' ');
                bool below = entry.EndsWith("\\...", StringComparison.Ordinal);

                if (below)
                {
                    entry = entry.Substring(0, entry.Length - 3);
                }

                if (entry.Length == 0 || !IsRooted(entry))
                {
                    continue;
                }

                string folder = Full(entry);

                if (folder != null)
                {
                    trusted.Add(new Trusted(FolderOf(folder), below));
                }
            }

            return trusted;
        }

        private readonly struct Trusted
        {
            internal Trusted(string folder, bool below)
            {
                Folder = folder;
                Below = below;
            }

            internal string Folder { get; }

            internal bool Below { get; }
        }
    }
}
