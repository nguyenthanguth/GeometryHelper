using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The TrueType fonts of the machine by their family, as AutoCAD's Text Style dialog lists them and writes a style of one:
    /// the file of its regular face (group 3, <c>arial.ttf</c>) beside the family (the style's extended data, Arial). Each
    /// font file of the Fonts folder of Windows and of the user's is read once, for its family and face in its name table,
    /// and for the other names the family has in other languages (<see cref="LocalNamesOf"/>).
    /// </summary>
    public static class CadTrueTypeFonts
    {
        private static readonly Lazy<SortedDictionary<string, string>> Files = new Lazy<
            SortedDictionary<string, string>
        >(Scan);

        // The path of each font file by "/" and its name, and of each family's faces by the family, "|" and 1 or 0 for bold and for italic; written by the scan.
        private static Dictionary<string, string> _paths = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>
        /// Gets or sets how wide the canvas draws letters of a TrueType font one unit high at their own width: given the family
        /// (null for the canvas's own font), whether bold, whether italic and the letters, the width from the start of the first
        /// to the end of the last. The runs of an MTEXT in several looks are put one after another by it; where it is null, as in
        /// a test with no canvas, a letter is taken as 0.6 of the height wide, as a text's picking box takes it. The window sets
        /// it to the canvas's own measure.
        /// </summary>
        public static Func<string, bool, bool, string, double> MeasureWidth { get; set; }

        // The other names of each family (Korean, Japanese, Chinese ...) by the family; written by the scan.
        private static Dictionary<string, string[]> _others = new Dictionary<string, string[]>(
            StringComparer.OrdinalIgnoreCase
        );

        private static readonly Lazy<Task> Warming = new Lazy<Task>(() =>
            CadWarmUp.Run("the TrueType fonts", () => _ = Files.Value)
        );

        /// <summary>
        /// Reads the fonts of the machine ahead (the scan of the Fonts folders that the first of <see cref="Families"/>,
        /// <see cref="FileOf"/>, <see cref="FamilyOf"/> or <see cref="LocalNamesOf"/> makes: some 2 500 files on a machine with
        /// many fonts, a second or more from a cold disk), on a thread of its own at below normal priority, and gives back at
        /// once. The app calls it after its window has drawn, so that the Text Style dialog and the MTEXT editor find the
        /// list read. A call that comes while the scan runs waits for it and does not repeat it, and the lists are the same
        /// whether or not this was called. Calling it again gives the same task.
        /// </summary>
        /// <returns>The task, complete when the fonts are read.</returns>
        public static Task WarmUp() => Warming.Value;

        /// <summary>Gets the families found, in ordinal order, letters of either case the same: Arial, Calibri, ...</summary>
        public static IReadOnlyList<string> Families => Files.Value.Keys.ToList();

        /// <summary>
        /// Gets the file a style of a family is written with: the regular face's file name, as arial.ttf for Arial; the family's
        /// name with .ttf where no file of it is found.
        /// </summary>
        /// <param name="family">The family, as Arial.</param>
        /// <returns>The file name, without its folder.</returns>
        public static string FileOf(string family)
        {
            string name = (family ?? string.Empty).Trim();
            return Files.Value.TryGetValue(name, out string file)
                ? file
                : name.Replace(" ", string.Empty).ToLowerInvariant() + ".ttf";
        }

        /// <summary>
        /// Gets the names a family has besides its English one, as its name table holds them (family name 1, and the typographic
        /// family name 16 where there is one), in every language: DotumChe has 돋움체, Malgun Gothic 맑은 고딕, MS Gothic
        /// ＭＳ ゴシック, Arial none. A name equal to the English one, or to another, letters of either case the same, is left
        /// out; the names come in the order of their language ids, so the same fonts give the same list on every read.
        /// </summary>
        /// <param name="family">The family by its English name, as <see cref="Families"/> lists it, letters of either case the same.</param>
        /// <returns>The other names; empty for a family with one name and for a name that is no family found.</returns>
        public static IReadOnlyList<string> LocalNamesOf(string family)
        {
            string name = (family ?? string.Empty).Trim();
            return name.Length > 0 && Files.Value != null && _others.TryGetValue(name, out string[] names)
                ? names
                : Array.Empty<string>();
        }

        /// <summary>Gets the family of a font file a style names (arial.ttf gives Arial); null where none found has that file.</summary>
        /// <param name="fileName">The file, with or without its folder.</param>
        /// <returns>The family, or null.</returns>
        public static string FamilyOf(string fileName)
        {
            string file = Path.GetFileName(fileName ?? string.Empty);
            return Files
                .Value.Where(p => string.Equals(p.Value, file, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Key)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the path of the font file a face is read from, for measuring its letters: a file by its path, or in the Fonts
        /// folders by its name; a family by the file of its bold, italic or regular face as asked, the regular one where it has
        /// no such face; Arial for no name. Null where no file is found.
        /// </summary>
        internal static string PathOf(string name, bool bold, bool italic)
        {
            SortedDictionary<string, string> files = Files.Value;
            string family = string.IsNullOrWhiteSpace(name) ? "Arial" : name.Trim();
            string extension = Path.GetExtension(family).ToLowerInvariant();

            if (extension == ".ttf" || extension == ".ttc" || extension == ".otf")
            {
                string styled = bold || italic ? FamilyOf(family) : null;

                if (
                    styled != null
                    && _paths.TryGetValue(styled + (bold ? "|1" : "|0") + (italic ? "1" : "0"), out string face)
                )
                {
                    return face;
                }

                if (Path.IsPathRooted(family) && File.Exists(family))
                {
                    return family;
                }

                return _paths.TryGetValue("/" + Path.GetFileName(family), out string byFile) ? byFile : null;
            }

            if (
                _paths.TryGetValue(family + (bold ? "|1" : "|0") + (italic ? "1" : "0"), out string found)
                || _paths.TryGetValue(family + "|00", out found)
            )
            {
                return found;
            }

            return files.TryGetValue(family, out string regular) && _paths.TryGetValue("/" + regular, out found)
                ? found
                : null;
        }

        internal static SortedDictionary<string, string> Scan()
        {
            // For each family the file of its regular face, else of the shortest name, ties by the name's letters.
            var best = new SortedDictionary<string, KeyValuePair<int, string>>(StringComparer.OrdinalIgnoreCase);
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var others = new Dictionary<string, List<KeyValuePair<int, string>>>(StringComparer.OrdinalIgnoreCase);
            var folders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "Windows",
                    "Fonts"
                ),
            };

            foreach (string folder in folders.Where(f => !string.IsNullOrEmpty(f) && Directory.Exists(f)))
            {
                string[] found;

                try
                {
                    found = Directory.GetFiles(folder);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Warn(
                        "CadTrueTypeFonts: the folder " + folder + " could not be listed.",
                        exception
                    );
                    continue;
                }

                Array.Sort(found, StringComparer.OrdinalIgnoreCase);

                foreach (string path in found)
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();

                    if (
                        (extension != ".ttf" && extension != ".ttc" && extension != ".otf")
                        || !TryNames(path, out string family, out string face, out var named)
                    )
                    {
                        continue;
                    }

                    if (!others.TryGetValue(family, out List<KeyValuePair<int, string>> listed))
                    {
                        others[family] = listed = new List<KeyValuePair<int, string>>();
                    }

                    listed.AddRange(named);
                    string file = Path.GetFileName(path);
                    string styleName = face ?? string.Empty;
                    bool bold = styleName.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool italic =
                        styleName.IndexOf("Italic", StringComparison.OrdinalIgnoreCase) >= 0
                        || styleName.IndexOf("Oblique", StringComparison.OrdinalIgnoreCase) >= 0;
                    string styled = family + (bold ? "|1" : "|0") + (italic ? "1" : "0");
                    bool plain =
                        styleName.Length == 0
                        || new[] { "Regular", "Bold", "Italic", "Bold Italic", "Oblique", "Bold Oblique" }.Contains(
                            styleName,
                            StringComparer.OrdinalIgnoreCase
                        );
                    int faceRank = (plain ? 0 : 1000) + file.Length;

                    if (!paths.ContainsKey("/" + file))
                    {
                        paths["/" + file] = path;
                    }

                    if (
                        !ranks.TryGetValue(styled, out int keptRank)
                        || faceRank < keptRank
                        || (faceRank == keptRank && string.CompareOrdinal(path, paths[styled]) < 0)
                    )
                    {
                        ranks[styled] = faceRank;
                        paths[styled] = path;
                    }

                    int rank =
                        (string.Equals(face, "Regular", StringComparison.OrdinalIgnoreCase) ? 0 : 1000) + file.Length;

                    if (
                        !best.TryGetValue(family, out KeyValuePair<int, string> kept)
                        || rank < kept.Key
                        || (rank == kept.Key && string.CompareOrdinal(file, kept.Value) < 0)
                    )
                    {
                        best[family] = new KeyValuePair<int, string>(rank, file);
                    }
                }
            }

            var files = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, KeyValuePair<int, string>> pair in best)
            {
                files[pair.Key] = pair.Value.Value;
            }

            _paths = paths;
            _others = OtherNames(others);
            return files;
        }

        /// <summary>
        /// The other names of each family: those of every face it has, an English one (any English language) and a repeat
        /// left out, in the order of their language ids and then of their letters, so that the faces' file order never shows.
        /// </summary>
        private static Dictionary<string, string[]> OtherNames(
            Dictionary<string, List<KeyValuePair<int, string>>> named
        )
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<KeyValuePair<int, string>>> pair in named)
            {
                var distinct = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { pair.Key };

                foreach (
                    KeyValuePair<int, string> other in pair
                        .Value.Where(o => (o.Key & 0x3FF) != 9)
                        .OrderBy(o => o.Key)
                        .ThenBy(o => o.Value, StringComparer.Ordinal)
                )
                {
                    if (seen.Add(other.Value))
                    {
                        distinct.Add(other.Value);
                    }
                }

                if (distinct.Count > 0)
                {
                    result[pair.Key] = distinct.ToArray();
                }
            }

            return result;
        }

        /// <summary>
        /// Reads the family (name 1) and the face (name 2) of the first font of a file from its name table, in English of the
        /// Windows platform where it has them, else the first of the Windows platform; and every family name (name 1 and the
        /// typographic family name 16) of the platform in any language, each with its language id.
        /// </summary>
        private static bool TryNames(
            string path,
            out string family,
            out string face,
            out List<KeyValuePair<int, string>> named
        )
        {
            family = null;
            face = null;
            named = new List<KeyValuePair<int, string>>();

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    long font = 0;

                    if (Tag(reader) == "ttcf")
                    {
                        stream.Position = 12;
                        font = Big32(reader);
                    }

                    stream.Position = font + 4;
                    int tables = Big16(reader);
                    long table = -1;

                    for (int i = 0; i < tables; i++)
                    {
                        stream.Position = font + 12 + 16 * i;
                        string tag = Tag(reader);
                        Big32(reader);
                        long offset = Big32(reader);

                        if (tag == "name")
                        {
                            table = offset;
                            break;
                        }
                    }

                    if (table < 0)
                    {
                        return false;
                    }

                    stream.Position = table + 2;
                    int count = Big16(reader);
                    long strings = table + Big16(reader);
                    var names = new Dictionary<int, string>();

                    for (int i = 0; i < count; i++)
                    {
                        stream.Position = table + 6 + 12 * i;
                        int platform = Big16(reader);
                        Big16(reader);
                        int language = Big16(reader);
                        int id = Big16(reader);
                        int length = Big16(reader);
                        int offset = Big16(reader);

                        if (platform != 3 || (id != 1 && id != 2 && id != 16))
                        {
                            continue;
                        }

                        bool keep = id != 16 && (!names.ContainsKey(id) || language == 0x409);

                        if (!keep && id == 2)
                        {
                            continue;
                        }

                        stream.Position = strings + offset;
                        string text = Encoding.BigEndianUnicode.GetString(reader.ReadBytes(length));

                        if (keep)
                        {
                            names[id] = text;
                        }

                        if (id != 2 && text.Trim().Length > 0)
                        {
                            named.Add(new KeyValuePair<int, string>(language, text.Trim()));
                        }
                    }

                    names.TryGetValue(1, out family);
                    names.TryGetValue(2, out face);
                    return !string.IsNullOrWhiteSpace(family);
                }
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                )
            {
                return false;
            }
        }

        private static string Tag(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));

        private static int Big16(BinaryReader reader)
        {
            byte[] b = reader.ReadBytes(2);
            return b.Length < 2 ? 0 : (b[0] << 8) | b[1];
        }

        private static long Big32(BinaryReader reader)
        {
            byte[] b = reader.ReadBytes(4);
            return b.Length < 4 ? 0 : ((long)b[0] << 24) | ((long)b[1] << 16) | ((long)b[2] << 8) | b[3];
        }
    }
}
