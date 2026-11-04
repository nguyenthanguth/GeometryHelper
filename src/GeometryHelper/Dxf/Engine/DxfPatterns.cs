using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The hatch pattern files of a drawing, found in the folders of its <see cref="DxfDatabase.PatternSearchPath"/> in their
    /// order and read once each, by the full path of the file; and the patterns in them as AutoCAD 2021 reads them (check
    /// P4c): ANSI31 of acadiso.pat, <c>45, 0,0, 0,3.175</c>, one line at 45 degrees through the origin whose next copy lies
    /// 3.175 across it.
    /// </summary>
    /// <remarks>
    /// A pattern is looked for in acadiso.pat for a drawing of millimetres and acad.pat for one of inches, and in a file named
    /// after it (<c>GRID.pat</c> for GRID): a pattern of the drawing's own file first and the other after, a custom one the
    /// other way round, as AutoCAD 2021 looks for them; each file in the first folder that has it, and the first pattern of the
    /// name in it. A pattern AutoCAD would refuse is refused: a line of fewer than five numbers or more than six dashes, a
    /// number it cannot read, or a pattern of no line. A pattern in neither file is said once at Debug; one refused, or a file
    /// that cannot be read, once at Warn. Nothing throws.
    /// </remarks>
    internal sealed class DxfPatterns
    {
        // AutoCAD 2021 refuses a line of a pattern of more than six dashes (check P4c, a line of seven refused).
        private const int MostDashes = 6;

        // The characters a number of a pattern file is written in.
        private const string NumberCharacters = "0123456789+-.eE";

        private readonly string[] _folders;
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, List<double[]>>> _files = new Dictionary<string, Dictionary<string, List<double[]>>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _said = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        /// <summary>The pattern files of the folders of a search path, separated by semicolons, those of no name passed over.</summary>
        internal DxfPatterns(string searchPath)
        {
            var folders = new List<string>();

            foreach (string folder in searchPath.Split(';'))
            {
                if (folder.Trim().Length > 0)
                {
                    folders.Add(folder.Trim());
                }
            }

            _folders = folders.ToArray();
        }

        /// <summary>
        /// The lines of a pattern as its file writes them, each its angle in degrees, base point, offset across and along it,
        /// and dashes, in the units of the file; null where it is in neither file or is refused, said in the log. Where it is
        /// found, whether it is in the drawing's own file (acadiso.pat or acad.pat), as AutoCAD 2021 makes it a pattern of that
        /// file (<see cref="DxfHatchPatternType.PreDefined"/>), and not one of a file of its name.
        /// </summary>
        internal List<double[]> Find(string name, bool custom, bool metric, out bool predefined)
        {
            predefined = false;
            string main = metric ? "acadiso.pat" : "acad.pat";
            string own = IsFileName(name) ? name + ".pat" : null;

            lock (_lock)
            {
                foreach (string file in custom ? new[] { own, main } : new[] { main, own })
                {
                    Dictionary<string, List<double[]>> patterns = file == null ? null : PatternsOf(file);

                    if (patterns == null || !patterns.TryGetValue(name, out List<double[]> lines))
                    {
                        continue;
                    }

                    if (lines == null)
                    {
                        Say(name + "|" + file, "DxfDatabase: the pattern " + name + " of " + _paths[file] + " has a line of fewer than five numbers, of more than six dashes or of a number that cannot be read, or no line, which AutoCAD refuses; the HATCH holds no lines of it.", true);
                        return null;
                    }

                    predefined = string.Equals(file, main, StringComparison.OrdinalIgnoreCase);
                    var copy = new List<double[]>(lines.Count);

                    foreach (double[] line in lines)
                    {
                        copy.Add((double[])line.Clone());
                    }

                    return copy;
                }

                Say(name + "|" + main, "DxfDatabase: the pattern " + name + " is in neither " + main + " nor " + (own ?? "a file of its name") + " in the folders of PatternSearchPath; the HATCH holds no lines of it, for AutoCAD to draw it from its own file.", false);
                return null;
            }
        }

        /// <summary>Whether a pattern's name is that of a file in a folder: none of the characters Windows refuses in one.</summary>
        private static bool IsFileName(string name)
        {
            foreach (char c in name)
            {
                if (c < ' ' || "\\/:*?\"<>|".IndexOf(c) >= 0)
                {
                    return false;
                }
            }

            return name.Length > 0;
        }

        /// <summary>The patterns of a file, read once from the first folder that has it; null where none has it or it cannot be read.</summary>
        private Dictionary<string, List<double[]>> PatternsOf(string file)
        {
            if (!_paths.TryGetValue(file, out string path))
            {
                path = Locate(file);
                _paths.Add(file, path);
            }

            if (path == null)
            {
                return null;
            }

            if (!_files.TryGetValue(path, out Dictionary<string, List<double[]>> patterns))
            {
                try
                {
                    patterns = Read(File.ReadAllBytes(path));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is NotSupportedException || e is OutOfMemoryException)
                {
                    Say(path, "DxfDatabase: the pattern file " + path + " could not be read (" + e.Message + "); its patterns are not drawn.", true);
                    patterns = null;
                }

                _files.Add(path, patterns);
            }

            return patterns;
        }

        /// <summary>The full path of a file in the first folder that has it; null where none has it.</summary>
        private string Locate(string file)
        {
            foreach (string folder in _folders)
            {
                try
                {
                    string path = Path.GetFullPath(Path.Combine(folder, file));

                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
                catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)
                {
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// The patterns of the bytes of a .pat file, by name whatever its case, the first of a name kept, as AutoCAD 2021 reads
        /// them (check P4c), each the list of its lines, or null where AutoCAD refuses it. A pattern begins at a line whose first
        /// character is <c>*</c>, its name all before the first comma, the comment (from a <c>;</c>) and the blanks at the end of
        /// the line left out, so that <c>*GRID , a grid</c> is named <c>GRID </c>; and it runs to the next such line. Each other
        /// line, its comment and blanks left out, is a line of the pattern unless nothing is left: numbers separated by commas,
        /// a number that blanks end taken without what follows (<c>2 1</c> read as 2), commas running together taken as one,
        /// and a comma at the end of the line a dash of nought, as AutoCAD reads them.
        /// </summary>
        internal static Dictionary<string, List<double[]>> Read(byte[] bytes)
        {
            var patterns = new Dictionary<string, List<double[]>>(StringComparer.OrdinalIgnoreCase);
            string name = null;
            List<double[]> lines = null;
            bool broken = false;
            int start = 0;

            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != '\n' && bytes[i] != '\r')
                {
                    continue;
                }

                string line = Line(bytes, start, i - start);
                start = i + 1;

                if (line.Length > 0 && line[0] == '*')
                {
                    Keep(patterns, name, lines, broken);
                    int comma = line.IndexOf(',');
                    name = comma < 0 ? line.Substring(1) : line.Substring(1, comma - 1);
                    lines = new List<double[]>();
                    broken = false;
                    continue;
                }

                if (name == null || broken || line.Trim().Length == 0)
                {
                    continue;
                }

                double[] numbers = Numbers(line);

                if (numbers == null)
                {
                    broken = true;
                }
                else
                {
                    lines.Add(numbers);
                }
            }

            Keep(patterns, name, lines, broken);
            return patterns;
        }

        /// <summary>A line of a file, each byte its own character, from its comment (a <c>;</c>) on and its blanks at the end left out.</summary>
        private static string Line(byte[] bytes, int start, int length)
        {
            var chars = new char[length];
            int end = 0;

            for (int i = 0; i < length; i++)
            {
                char c = (char)bytes[start + i];

                if (c == ';')
                {
                    break;
                }

                chars[end++] = c;
            }

            while (end > 0 && (chars[end - 1] == ' ' || chars[end - 1] == '\t'))
            {
                end--;
            }

            return new string(chars, 0, end);
        }

        /// <summary>A pattern read, kept where it is the first of its name: its lines, or null where it is refused or has none.</summary>
        private static void Keep(Dictionary<string, List<double[]>> patterns, string name, List<double[]> lines, bool broken)
        {
            if (name != null && !patterns.ContainsKey(name))
            {
                patterns.Add(name, broken || lines.Count == 0 ? null : lines);
            }
        }

        /// <summary>The numbers of a line of a pattern, its angle, base point, offset and dashes; null where AutoCAD refuses it.</summary>
        private static double[] Numbers(string line)
        {
            string[] fields = line.Split(',');
            var numbers = new List<double>(fields.Length);

            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Length == 0)
                {
                    // Commas running together are one; a comma at the end is a dash of nought.
                    if (i == fields.Length - 1)
                    {
                        numbers.Add(0.0);
                    }

                    continue;
                }

                string field = fields[i].TrimStart(' ', '\t');
                int end = 0;

                while (end < field.Length && NumberCharacters.IndexOf(field[end]) >= 0)
                {
                    end++;
                }

                if (end == 0 || (end < field.Length && field[end] != ' ' && field[end] != '\t')
                    || !double.TryParse(field.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !Guard.IsFinite(value))
                {
                    return null;
                }

                numbers.Add(value);
            }

            return numbers.Count < 5 || numbers.Count > 5 + MostDashes ? null : numbers.ToArray();
        }

        private void Say(string key, string message, bool warn)
        {
            if (!_said.Add(key))
            {
                return;
            }

            if (warn)
            {
                GeometryHelperLog.Warn(message);
            }
            else
            {
                GeometryHelperLog.Debug(message);
            }
        }
    }
}
