using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The fonts a document's text is drawn in, found as AutoCAD finds them: a text style's SHX font (romans.shx, txt read as
    /// txt.shx) and big font in the folder of the drawing and then in <see cref="Folders"/> in their order, read once and
    /// kept; a TrueType typeface or file as the canvas has it; a font named in <see cref="Substitutions"/> looked for as the
    /// font it is given for, as AutoCAD's font mapping file does; and one not found replaced by <see cref="Replacement"/>, as
    /// AutoCAD's FONTALT replaces it, said once a font through <see cref="CadDocument.FontMissing"/>. No font is shipped: the
    /// fonts are those of the user's machine.
    /// </summary>
    /// <remarks>
    /// Fonts of no folders and no replacement are off: every style is drawn as before fonts were read, its TrueType typeface
    /// or file by the canvas and an SHX font in the canvas's own font, and nothing is said. A file found that is no SHX font,
    /// or cannot be read, is said once at Debug and taken as not found; nothing throws. The fonts read are kept for every
    /// document by their path, the size and time of their file, at most 48 at once, the one used longest ago let go first.
    /// </remarks>
    public sealed class CadFonts
    {
        private const int MostFonts = 48;

        // The code page of each big font AutoCAD knows by its name, as the library's measuring reads them: 中 is D6D0 in
        // gbcbig.shx (GBK), A4A4 in chineset.shx (Big5) and 9286 in extfont.shx (Shift-JIS).
        private static readonly Dictionary<string, int> BigPages = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "bigfont", 932 }, { "extfont", 932 }, { "extfont2", 932 }, { "@extfont2", 932 }, { "gbcbig", 936 },
            { "whgdtxt", 949 }, { "whgtxt", 949 }, { "whtgtxt", 949 }, { "whtmtxt", 949 }, { "chineset", 950 },
        };

        private static readonly Dictionary<string, Kept> Loaded = new Dictionary<string, Kept>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Unread = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Lazy<IReadOnlyList<string>> Defaults = new Lazy<IReadOnlyList<string>>(FindDefaultFolders);
        private static long _clock;

        private readonly Func<string, bool> _familyExists;
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Makes the fonts of a document.</summary>
        /// <param name="folders">The folders fonts are looked for in, in order, after the drawing's own; null or empty for none.</param>
        /// <param name="replacement">The font a font not found is drawn in: an SHX file (simplex.shx), a TrueType file or a typeface; null or empty for the canvas's own.</param>
        /// <param name="substitutions">Fonts drawn in others, each name to the name it is drawn in (romans.shx to isocp.shx); null for none.</param>
        /// <param name="familyExists">Whether the canvas has a TrueType typeface of a name; null to take every typeface as there.</param>
        public CadFonts(IEnumerable<string> folders, string replacement, IEnumerable<KeyValuePair<string, string>> substitutions, Func<string, bool> familyExists = null)
        {
            Folders = (folders ?? Enumerable.Empty<string>()).Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToList();
            Replacement = (replacement ?? string.Empty).Trim();
            var table = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> pair in substitutions ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    table[pair.Key.Trim()] = pair.Value.Trim();
                }
            }

            Substitutions = table;
            _familyExists = familyExists;
        }

        /// <summary>
        /// Gets the folders AutoCAD's fonts are likely in on this machine: the Fonts folder of each AutoCAD found under
        /// Program Files\Autodesk or a drive's Autodesk folder, the newest first (D:\Autodesk\AutoCAD\AutoCAD 2021\Fonts), then
        /// the Fonts folder of Windows. Looked for once.
        /// </summary>
        public static IReadOnlyList<string> DefaultFolders => Defaults.Value;

        /// <summary>Gets the folders fonts are looked for in, in order, after the drawing's own folder.</summary>
        public IReadOnlyList<string> Folders { get; }

        /// <summary>Gets the font a font not found is drawn in; empty for the canvas's own.</summary>
        public string Replacement { get; }

        /// <summary>Gets the fonts drawn in others, each name to the name it is drawn in, in the order of their names.</summary>
        public IReadOnlyDictionary<string, string> Substitutions { get; }

        /// <summary>Gets whether fonts are off: no folders and no replacement.</summary>
        public bool IsOff => Folders.Count == 0 && Replacement.Length == 0;

        /// <summary>Gets whether these fonts are set as another's: the same folders, replacement and substitutions.</summary>
        /// <param name="other">The other fonts.</param>
        /// <returns>True where they are.</returns>
        public bool IsSetAs(CadFonts other)
            => other != null && Folders.SequenceEqual(other.Folders, StringComparer.OrdinalIgnoreCase) && string.Equals(Replacement, other.Replacement, StringComparison.OrdinalIgnoreCase)
               && Substitutions.Count == other.Substitutions.Count && Substitutions.All(p => other.Substitutions.TryGetValue(p.Key, out string v) && string.Equals(v, p.Value, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Finds a font's file: the path as written where it is a full path that is there, else its file name in the drawing's
        /// folder and then in <see cref="Folders"/>, <c>.shx</c> added to a name of no extension (txt is txt.shx).
        /// </summary>
        /// <param name="name">The font's name or file.</param>
        /// <param name="drawingFolder">The drawing's folder; null for none.</param>
        /// <returns>The full path, or null where no folder has it.</returns>
        public string Locate(string name, string drawingFolder)
        {
            string key = (drawingFolder ?? string.Empty) + "|" + (name ?? string.Empty).Trim();

            lock (_paths)
            {
                if (!_paths.TryGetValue(key, out string path))
                {
                    path = Search((name ?? string.Empty).Trim(), drawingFolder);
                    _paths.Add(key, path);
                }

                return path;
            }
        }

        /// <summary>
        /// Reads the SHX font of a file, once for every document while its file is unchanged; null, said once at Debug, where
        /// it cannot be read or is no SHX font.
        /// </summary>
        /// <param name="path">The full path of the file.</param>
        /// <returns>The font, or null.</returns>
        public static ShxFont Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string key;

            try
            {
                var file = new FileInfo(path);
                key = file.FullName + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException || e is System.Security.SecurityException)
            {
                return null;
            }

            lock (Loaded)
            {
                if (Loaded.TryGetValue(key, out Kept kept))
                {
                    kept.Used = ++_clock;
                    return kept.Font;
                }
            }

            ShxFont font = null;
            string why = "is no SHX font of shapes 1.0 or 1.1, unifont 1.0 or bigfont 1.0, or is cut short";

            try
            {
                font = ShxFont.Read(File.ReadAllBytes(path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException || e is System.Security.SecurityException || e is OutOfMemoryException)
            {
                why = "could not be read (" + e.Message + ")";
            }

            lock (Loaded)
            {
                if (font == null)
                {
                    if (Unread.Add(key))
                    {
                        GeometryHelperLog.Debug("CadFonts: the font " + path + " " + why + "; it is taken as not found.");
                    }

                    return null;
                }

                if (Loaded.Count >= MostFonts)
                {
                    Loaded.Remove(Loaded.Aggregate((a, b) => a.Value.Used <= b.Value.Used ? a : b).Key);
                }

                Loaded[key] = new Kept(font, ++_clock);
            }

            return font;
        }

        /// <summary>
        /// The face a text style is drawn in, and the line to say where its font is not found and is replaced, given once a
        /// font; with fonts off, the style's typeface or file as the canvas takes it.
        /// </summary>
        internal CadFontFace Resolve(string fileName, string bigFontFileName, string typeface, string drawingFolder, out string report)
        {
            report = null;
            bool isFamily = !string.IsNullOrEmpty(typeface);
            string raw = isFamily ? typeface : fileName;
            string named = (raw ?? string.Empty).Trim();

            if (IsOff || named.Length == 0)
            {
                return new CadFontFace(string.IsNullOrEmpty(raw) ? null : raw, null, null, null);
            }

            string wanted = Substitute(named);
            bool substituted = !string.Equals(wanted, named, StringComparison.OrdinalIgnoreCase);
            CadFontFace face = Find(wanted, drawingFolder, isFamily || substituted, !substituted && !isFamily);

            if (face == null)
            {
                CadFontFace replaced = Replacement.Length == 0 ? null : Find(Replacement, drawingFolder, true, false);
                string what = named + (substituted ? " (as " + wanted + ")" : string.Empty);

                lock (_reported)
                {
                    if (_reported.Add(what))
                    {
                        report = "Font " + what + " not found: using " + (replaced == null ? "the drawing font" : Replacement) + ".";
                        GeometryHelperLog.Debug("CadFonts: " + report);
                    }
                }

                face = replaced ?? new CadFontFace(named, null, null, null);
            }

            if (face.Shx == null || string.IsNullOrWhiteSpace(bigFontFileName))
            {
                return face;
            }

            string big = bigFontFileName.Trim();
            ShxFont bigFont = Load(Locate(big, drawingFolder));

            if (bigFont == null || bigFont.Kind != ShxFontKind.BigFont)
            {
                lock (_reported)
                {
                    if (_reported.Add(big))
                    {
                        report = (report == null ? string.Empty : report + " ") + "Big font " + big + " not found: its letters are drawn as question marks.";
                        GeometryHelperLog.Debug("CadFonts: big font " + big + " not found or no big font.");
                    }
                }

                return face;
            }

            return new CadFontFace(face.Name, face.Shx, bigFont, PageOf(big));
        }

        /// <summary>A name as <see cref="Substitutions"/> gives it, by its whole name or its name without the extension; the name itself where none is given.</summary>
        private string Substitute(string name)
        {
            if (Substitutions.TryGetValue(name, out string to))
            {
                return to;
            }

            string bare = Path.GetFileNameWithoutExtension(name);
            return bare.Length > 0 && Substitutions.TryGetValue(bare, out to) ? to : name;
        }

        /// <summary>
        /// The face of a name found: an SHX file, a TrueType file, or with <paramref name="family"/> a typeface the canvas has;
        /// a name of no extension an SHX file first, then a typeface, but a style's own file name, which AutoCAD reads as SHX
        /// alone. Null where none is found.
        /// </summary>
        private CadFontFace Find(string name, string drawingFolder, bool family, bool shxOnly)
        {
            string extension = Path.GetExtension(name).ToLowerInvariant();

            if (extension == ".ttf" || extension == ".ttc" || extension == ".otf")
            {
                string path = Locate(name, drawingFolder);
                string bare = Path.GetFileNameWithoutExtension(name);
                return path != null ? new CadFontFace(path, null, null, null) : HasFamily(bare) ? new CadFontFace(name, null, null, null) : null;
            }

            if (family && extension.Length == 0 && _familyExists != null && _familyExists(name))
            {
                return new CadFontFace(name, null, null, null);
            }

            if (extension.Length == 0 || extension == ".shx")
            {
                ShxFont font = Load(Locate(name, drawingFolder));

                if (font != null && font.Kind != ShxFontKind.BigFont)
                {
                    return new CadFontFace(extension.Length == 0 ? name + ".shx" : name, font, null, null);
                }
            }

            return !shxOnly && family && extension.Length == 0 && HasFamily(name) ? new CadFontFace(name, null, null, null) : null;
        }

        private bool HasFamily(string name) => _familyExists == null || _familyExists(name);

        private string Search(string name, string drawingFolder)
        {
            if (name.Length == 0)
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(name) && File.Exists(name))
                {
                    return Path.GetFullPath(name);
                }

                string file = Path.GetFileName(name);
                file = Path.GetExtension(file).Length == 0 ? file + ".shx" : file;

                foreach (string folder in new[] { drawingFolder }.Concat(Folders))
                {
                    string path = string.IsNullOrWhiteSpace(folder) ? null : Path.Combine(folder, file);

                    if (path != null && File.Exists(path))
                    {
                        return Path.GetFullPath(path);
                    }
                }
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)
            {
                return null;
            }

            return null;
        }

        /// <summary>The code page AutoCAD reads a big font's letters in, by its bare name; null for a name it knows not, one written with its folder, or a page the runtime has not.</summary>
        private static Encoding PageOf(string name)
        {
            string bare = name.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;

            if (name.IndexOfAny(new[] { '\\', '/', ':' }) >= 0 || !BigPages.TryGetValue(bare, out int number))
            {
                return null;
            }

            try
            {
                return Encoding.GetEncoding(number, new EncoderReplacementFallback(string.Empty), DecoderFallback.ReplacementFallback);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
                return null;
            }
        }

        private static IReadOnlyList<string> FindDefaultFolders()
        {
            var bases = new List<string>();
            var folders = new List<string>();

            try
            {
                bases.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk"));

                foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    bases.Add(Path.Combine(drive.Name, "Autodesk"));
                    bases.Add(Path.Combine(drive.Name, "Autodesk", "AutoCAD"));
                }

                foreach (string root in bases.Where(Directory.Exists))
                {
                    foreach (string install in Directory.GetDirectories(root, "AutoCAD*").OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
                    {
                        string fonts = Path.Combine(install, "Fonts");

                        if (Directory.Exists(fonts) && !folders.Contains(fonts, StringComparer.OrdinalIgnoreCase))
                        {
                            folders.Add(fonts);
                        }
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is System.Security.SecurityException)
            {
                GeometryHelperLog.Debug("CadFonts: AutoCAD's Fonts folder was not looked for to the end (" + e.Message + ").");
            }

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            if (windows.Length > 0)
            {
                folders.Add(windows);
            }

            return folders;
        }

        private sealed class Kept
        {
            public Kept(ShxFont font, long used)
            {
                Font = font;
                Used = used;
            }

            public ShxFont Font { get; }

            public long Used { get; set; }
        }
    }

    /// <summary>What a text style is drawn in: an SHX font with the big font beside it and its code page, or the name the canvas takes.</summary>
    internal sealed class CadFontFace
    {
        public CadFontFace(string name, ShxFont shx, ShxFont big, Encoding page)
        {
            Name = name;
            Shx = shx;
            Big = big;
            Page = page;
        }

        /// <summary>The font's name: the SHX file drawn, a TrueType file's path or a typeface; null for the canvas's own.</summary>
        public string Name { get; }

        public ShxFont Shx { get; }

        public ShxFont Big { get; }

        public Encoding Page { get; }
    }
}
