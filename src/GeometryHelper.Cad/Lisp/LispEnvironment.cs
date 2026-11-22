using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// What <c>getenv</c>, <c>setenv</c>, <c>getcfg</c> and <c>setcfg</c> keep: AutoCAD 2021 keeps a name and its text in the
    /// registry, so that the value a routine sets is there in the next drawing and the next session. The library keeps them in
    /// memory; the window gives every drawing one shared store, loads it from the Global settings file and saves it there at
    /// each change (<see cref="Changed"/>). Names are compared without regard to case. A value set to the empty text is no
    /// value: <c>getenv</c> then falls back to the Windows environment, as it does for a name never set, and the store forgets
    /// the name (AutoCAD keeps an empty one in the registry, which no function can tell from a missing one). A
    /// <c>getcfg</c> value is the exception: AutoCAD keeps an empty one, and it tells a folder that has values from one that
    /// has none.
    /// </summary>
    internal sealed class LispEnvironment
    {
        // The prefix that keeps the entries of getcfg apart from the names of getenv, in one table.
        private const string ConfigPrefix = "cfg:";

        private readonly object _lock = new object();
        private readonly SortedDictionary<string, string> _values = new SortedDictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>Raised after a name was set or removed by a routine, never by <see cref="Load"/>.</summary>
        internal event EventHandler Changed;

        /// <summary>
        /// Gets the entries, for the Global settings file: the names <c>getenv</c> has set, and the entries of <c>getcfg</c> under
        /// <c>cfg:</c> and the path, sorted by name without regard to case.
        /// </summary>
        internal IReadOnlyList<KeyValuePair<string, string>> Entries
        {
            get
            {
                lock (_lock)
                {
                    return new List<KeyValuePair<string, string>>(_values);
                }
            }
        }

        /// <summary>Replaces what the store holds, as the settings file gave it; raises no event.</summary>
        /// <param name="entries">The entries as <see cref="Entries"/> gave them.</param>
        internal void Load(IEnumerable<KeyValuePair<string, string>> entries)
        {
            lock (_lock)
            {
                _values.Clear();

                foreach (KeyValuePair<string, string> entry in entries ?? Array.Empty<KeyValuePair<string, string>>())
                {
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Value != null)
                    {
                        _values[entry.Key] = entry.Value;
                    }
                }
            }
        }

        /// <summary>Gets what <c>setenv</c> set for a name.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The value; null where none was set.</returns>
        internal string GetEnvironment(string name)
        {
            lock (_lock)
            {
                return _values.TryGetValue(name, out string value) && value.Length > 0 ? value : null;
            }
        }

        /// <summary>Sets a name for <c>getenv</c>; the empty text takes it away.</summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        internal void SetEnvironment(string name, string value)
        {
            lock (_lock)
            {
                if (value.Length == 0)
                {
                    _values.Remove(name);
                }
                else
                {
                    _values[name] = value;
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets what <c>setcfg</c> set for a path such as <c>AppData/Tools/Last</c>: the text before the last slash is the folder
        /// and the rest the value. A folder that holds no value at all, and a path with no slash, gives nothing; a folder that
        /// holds a value gives the empty text for a name it does not.
        /// </summary>
        /// <param name="path">The path as the routine wrote it.</param>
        /// <returns>The value; the empty text for a missing value in a folder that exists; null where the folder does not.</returns>
        internal string GetConfig(string path)
        {
            if (!TryCut(path, out string folder))
            {
                return null;
            }

            lock (_lock)
            {
                if (_values.TryGetValue(ConfigPrefix + path, out string value))
                {
                    return value;
                }

                string below = ConfigPrefix + folder + "/";

                foreach (string key in _values.Keys)
                {
                    if (key.StartsWith(below, StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Empty;
                    }
                }

                return null;
            }
        }

        /// <summary>Sets a path for <c>getcfg</c>; a path with no slash, or ending in one, is not kept.</summary>
        /// <param name="path">The path as the routine wrote it.</param>
        /// <param name="value">The value, which may be empty.</param>
        /// <returns>True where the value was kept.</returns>
        internal bool SetConfig(string path, string value)
        {
            if (!TryCut(path, out _))
            {
                return false;
            }

            lock (_lock)
            {
                _values[ConfigPrefix + path] = value;
            }

            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        // A path is kept when it has a folder and a name: a slash that is neither the first nor the last character.
        private static bool TryCut(string path, out string folder)
        {
            int slash = path.LastIndexOf('/');
            folder = slash > 0 ? path.Substring(0, slash) : null;
            return folder != null && slash < path.Length - 1;
        }
    }
}
