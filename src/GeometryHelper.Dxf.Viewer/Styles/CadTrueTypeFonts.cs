using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The TrueType fonts of the machine by their family, as AutoCAD's Text Style dialog lists them and writes a style of one:
    /// the file of its regular face (group 3, <c>arial.ttf</c>) beside the family (the style's extended data, Arial). Each
    /// font file of the Fonts folder of Windows and of the user's is read once, for its family and face in its name table.
    /// </summary>
    public static class CadTrueTypeFonts
    {
        private static readonly Lazy<SortedDictionary<string, string>> Files = new Lazy<SortedDictionary<string, string>>(Scan);

        // The path of each font file by "/" and its name, and of each family's faces by the family, "|" and 1 or 0 for bold and for italic; written by the scan.
        private static Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets how wide the canvas draws letters of a TrueType font one unit high at their own width: given the family
        /// (null for the canvas's own font), whether bold, whether italic and the letters, the width from the start of the first
        /// to the end of the last. The runs of an MTEXT in several looks are put one after another by it; where it is null, as in
        /// a test with no canvas, a letter is taken as 0.6 of the height wide, as a text's picking box takes it. The window sets
        /// it to the canvas's own measure.
        /// </summary>
        public static Func<string, bool, bool, string, double> MeasureWidth { get; set; }

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
            return Files.Value.TryGetValue(name, out string file) ? file : name.Replace(" ", string.Empty).ToLowerInvariant() + ".ttf";
        }

        /// <summary>Gets the family of a font file a style names (arial.ttf gives Arial); null where none found has that file.</summary>
        /// <param name="fileName">The file, with or without its folder.</param>
        /// <returns>The family, or null.</returns>
        public static string FamilyOf(string fileName)
        {
            string file = Path.GetFileName(fileName ?? string.Empty);
            return Files.Value.Where(p => string.Equals(p.Value, file, StringComparison.OrdinalIgnoreCase)).Select(p => p.Key).FirstOrDefault();
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

                if (styled != null && _paths.TryGetValue(styled + (bold ? "|1" : "|0") + (italic ? "1" : "0"), out string face))
                {
                    return face;
                }

                if (Path.IsPathRooted(family) && File.Exists(family))
                {
                    return family;
                }

                return _paths.TryGetValue("/" + Path.GetFileName(family), out string byFile) ? byFile : null;
            }

            if (_paths.TryGetValue(family + (bold ? "|1" : "|0") + (italic ? "1" : "0"), out string found) || _paths.TryGetValue(family + "|00", out found))
            {
                return found;
            }

            return files.TryGetValue(family, out string regular) && _paths.TryGetValue("/" + regular, out found) ? found : null;
        }

        private static SortedDictionary<string, string> Scan()
        {
            // For each family the file of its regular face, else of the shortest name, ties by the name's letters.
            var best = new SortedDictionary<string, KeyValuePair<int, string>>(StringComparer.OrdinalIgnoreCase);
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var folders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
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
                    GeometryHelperLog.Warn("CadTrueTypeFonts: the folder " + folder + " could not be listed.", exception);
                    continue;
                }

                Array.Sort(found, StringComparer.OrdinalIgnoreCase);

                foreach (string path in found)
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();

                    if ((extension != ".ttf" && extension != ".ttc" && extension != ".otf") || !TryNames(path, out string family, out string face))
                    {
                        continue;
                    }

                    string file = Path.GetFileName(path);
                    string styleName = face ?? string.Empty;
                    bool bold = styleName.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool italic = styleName.IndexOf("Italic", StringComparison.OrdinalIgnoreCase) >= 0 || styleName.IndexOf("Oblique", StringComparison.OrdinalIgnoreCase) >= 0;
                    string styled = family + (bold ? "|1" : "|0") + (italic ? "1" : "0");
                    bool plain = styleName.Length == 0 || new[] { "Regular", "Bold", "Italic", "Bold Italic", "Oblique", "Bold Oblique" }.Contains(styleName, StringComparer.OrdinalIgnoreCase);
                    int faceRank = (plain ? 0 : 1000) + file.Length;

                    if (!paths.ContainsKey("/" + file))
                    {
                        paths["/" + file] = path;
                    }

                    if (!ranks.TryGetValue(styled, out int keptRank) || faceRank < keptRank || (faceRank == keptRank && string.CompareOrdinal(path, paths[styled]) < 0))
                    {
                        ranks[styled] = faceRank;
                        paths[styled] = path;
                    }

                    int rank = (string.Equals(face, "Regular", StringComparison.OrdinalIgnoreCase) ? 0 : 1000) + file.Length;

                    if (!best.TryGetValue(family, out KeyValuePair<int, string> kept) || rank < kept.Key || (rank == kept.Key && string.CompareOrdinal(file, kept.Value) < 0))
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
            return files;
        }

        /// <summary>
        /// Reads the family (name 1) and the face (name 2) of the first font of a file from its name table, in English of the
        /// Windows platform where it has them, else the first of the Windows platform.
        /// </summary>
        private static bool TryNames(string path, out string family, out string face)
        {
            family = null;
            face = null;

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

                        if (platform != 3 || (id != 1 && id != 2) || (names.ContainsKey(id) && language != 0x409))
                        {
                            continue;
                        }

                        stream.Position = strings + offset;
                        names[id] = Encoding.BigEndianUnicode.GetString(reader.ReadBytes(length));
                    }

                    names.TryGetValue(1, out family);
                    names.TryGetValue(2, out face);
                    return !string.IsNullOrWhiteSpace(family);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
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
