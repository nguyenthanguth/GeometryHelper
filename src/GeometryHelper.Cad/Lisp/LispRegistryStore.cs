using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The keys and values <c>vl-registry-*</c> read and write: a store of the presenter's own, shaped like the Windows
    /// registry (keys under five roots, each key with named values that are strings or integers) and kept in memory or in a
    /// text file. The file holds one line per value, the key path, the value name, <c>S</c> or <c>I</c> and the value
    /// separated by tabs, with a tab, a line feed and a backslash inside a field written <c>\t</c>, <c>\n</c> and <c>\\</c>,
    /// and a key with no values is one line with its name and value kind left empty. Names below the root compare ignoring
    /// case, as Windows does, and keep the case they were first written in.
    /// </summary>
    internal sealed class LispRegistryStore
    {
        /// <summary>Held while a file is read, changed and written, by every document of the process.</summary>
        internal static readonly object Lock = new object();

        private static readonly string[] s_roots =
        {
            "HKEY_CURRENT_USER",
            "HKEY_LOCAL_MACHINE",
            "HKEY_CLASSES_ROOT",
            "HKEY_USERS",
            "HKEY_CURRENT_CONFIG",
        };

        private sealed class Key
        {
            internal string Path;
            internal readonly List<Entry> Values = new List<Entry>();
        }

        private sealed class Entry
        {
            internal string Name;
            internal object Value;
        }

        private readonly Dictionary<string, Key> _keys = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets whether a write or a delete changed anything since the store was made.</summary>
        internal bool Changed { get; private set; }

        /// <summary>Reads a key name the way the registry takes it: a root in capitals, then names between backslashes.</summary>
        /// <param name="key">The key as the routine wrote it.</param>
        /// <param name="path">The key without empty names, backslash between the names.</param>
        /// <returns>False when the root is none of the five.</returns>
        internal static bool TryPath(string key, out string path)
        {
            string[] names = key.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            path = null;

            if (names.Length == 0 || Array.IndexOf(s_roots, names[0]) < 0)
            {
                return false;
            }

            path = string.Join("\\", names);
            return true;
        }

        private static bool IsRoot(string path) => path.IndexOf('\\') < 0;

        /// <summary>Gets a value.</summary>
        /// <param name="path">The key, from <see cref="TryPath"/>.</param>
        /// <param name="name">The value's name; empty for the key's default value.</param>
        /// <returns>The string or integer; null when the key or the value is not there.</returns>
        internal object Read(string path, string name)
        {
            return _keys.TryGetValue(path, out Key key) ? Find(key, name)?.Value : null;
        }

        /// <summary>Sets a value, making the key and the keys above it.</summary>
        /// <param name="path">The key, from <see cref="TryPath"/>.</param>
        /// <param name="name">The value's name; empty for the key's default value.</param>
        /// <param name="value">A string or an integer.</param>
        internal void Write(string path, string name, object value)
        {
            Key key = Ensure(path);
            Entry entry = Find(key, name);

            if (entry == null)
            {
                key.Values.Add(new Entry { Name = name, Value = value });
            }
            else
            {
                entry.Value = value;
            }

            Changed = true;
        }

        /// <summary>Gets the names of the keys directly below a key, in descending order, ignoring case first.</summary>
        /// <param name="path">The key, from <see cref="TryPath"/>.</param>
        /// <returns>The names, none for a key that is not there.</returns>
        internal List<string> SubKeys(string path)
        {
            string prefix = path + "\\";
            var names = new List<string>();

            foreach (Key key in _keys.Values)
            {
                if (
                    key.Path.Length > prefix.Length
                    && key.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && key.Path.IndexOf('\\', prefix.Length) < 0
                )
                {
                    names.Add(key.Path.Substring(prefix.Length));
                }
            }

            names.Sort(
                (x, y) =>
                {
                    int order = string.Compare(y, x, StringComparison.OrdinalIgnoreCase);
                    return order != 0 ? order : string.CompareOrdinal(y, x);
                }
            );
            return names;
        }

        /// <summary>Gets the names of a key's values, the one written last first.</summary>
        /// <param name="path">The key, from <see cref="TryPath"/>.</param>
        /// <returns>The names, none for a key that is not there.</returns>
        internal List<string> ValueNames(string path)
        {
            var names = new List<string>();

            if (_keys.TryGetValue(path, out Key key))
            {
                for (int i = key.Values.Count - 1; i >= 0; i--)
                {
                    names.Add(key.Values[i].Name);
                }
            }

            return names;
        }

        /// <summary>Removes a value.</summary>
        /// <param name="path">The key, from <see cref="TryPath"/>.</param>
        /// <param name="name">The value's name; empty for the key's default value.</param>
        internal void DeleteValue(string path, string name)
        {
            if (_keys.TryGetValue(path, out Key key))
            {
                Entry entry = Find(key, name);

                if (entry != null)
                {
                    key.Values.Remove(entry);
                    Changed = true;
                }
            }
        }

        /// <summary>Removes a key with its values, unless it has keys below it.</summary>
        /// <param name="path">The key, from <see cref="TryPath"/>.</param>
        /// <returns>False when it has keys below it and stays; true otherwise, also for a key that was not there.</returns>
        internal bool DeleteKey(string path)
        {
            if (SubKeys(path).Count > 0)
            {
                return false;
            }

            if (_keys.Remove(path))
            {
                Changed = true;
            }

            return true;
        }

        private static Entry Find(Key key, string name) =>
            key.Values.Find(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        private Key Ensure(string path)
        {
            // A root is made only when a value is written to it; the keys above a new key are made, the root left out.
            int cut = path.IndexOf('\\');
            cut = cut < 0 ? -1 : path.IndexOf('\\', cut + 1);

            while (cut >= 0)
            {
                Make(path.Substring(0, cut));
                cut = path.IndexOf('\\', cut + 1);
            }

            return Make(path);
        }

        private Key Make(string path)
        {
            if (!_keys.TryGetValue(path, out Key key))
            {
                key = new Key { Path = path };
                _keys.Add(path, key);
                Changed = true;
            }

            return key;
        }

        /// <summary>Reads the keys of a file; a file that is missing or cannot be read gives an empty store, and a line that is not one is skipped.</summary>
        /// <param name="file">The file.</param>
        /// <returns>The store.</returns>
        internal static LispRegistryStore Load(string file)
        {
            var store = new LispRegistryStore();
            string text;

            try
            {
                text = File.ReadAllText(file, new UTF8Encoding(false));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return store;
            }

            foreach (string line in text.Split('\n'))
            {
                string[] fields = line.Split('\t');

                if (fields.Length < 3 || !TryPath(Unescape(fields[0]), out string path))
                {
                    continue;
                }

                Key key = store.Ensure(path);
                string kind = fields[2];

                if (kind.Length == 0 || fields.Length < 4)
                {
                    continue;
                }

                string value = Unescape(fields[3]);
                object parsed = kind == "S" ? value : null;

                if (
                    kind == "I"
                    && int.TryParse(
                        value,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out int number
                    )
                )
                {
                    parsed = number;
                }

                if (parsed != null)
                {
                    string name = Unescape(fields[1]);

                    if (Find(key, name) == null)
                    {
                        key.Values.Add(new Entry { Name = name, Value = parsed });
                    }
                }
            }

            store.Changed = false;
            return store;
        }

        /// <summary>Writes the store to a file, making its folder; the keys in order of their paths, the values of a key in the order they were written.</summary>
        /// <param name="file">The file.</param>
        internal void Save(string file)
        {
            var text = new StringBuilder();

            foreach (
                Key key in _keys
                    .Values.OrderBy(k => k.Path, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(k => k.Path, StringComparer.Ordinal)
            )
            {
                string path = Escape(key.Path);

                if (key.Values.Count == 0)
                {
                    text.Append(path).Append("\t\t\t\n");
                }

                foreach (Entry entry in key.Values)
                {
                    bool isText = entry.Value is string;
                    string value = isText
                        ? (string)entry.Value
                        : ((int)entry.Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    text.Append(path)
                        .Append('\t')
                        .Append(Escape(entry.Name))
                        .Append('\t')
                        .Append(isText ? 'S' : 'I')
                        .Append('\t')
                        .Append(Escape(value))
                        .Append('\n');
                }
            }

            string folder = Path.GetDirectoryName(Path.GetFullPath(file));

            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(file, text.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string text) =>
            text.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n");

        private static string Unescape(string text)
        {
            if (text.IndexOf('\\') < 0)
            {
                return text;
            }

            var result = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\\' && i + 1 < text.Length)
                {
                    char next = text[i + 1];

                    if (next == 't' || next == 'n' || next == '\\')
                    {
                        result.Append(
                            next == 't' ? '\t'
                            : next == 'n' ? '\n'
                            : '\\'
                        );
                        i++;
                        continue;
                    }
                }

                result.Append(c);
            }

            return result.ToString();
        }
    }
}
