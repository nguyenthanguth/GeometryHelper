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
    /// kept; a TrueType typeface or file as the canvas has it, a file also in the Fonts folder of Windows; a font named in
    /// <see cref="Substitutions"/> looked for as the font it is given for, as AutoCAD's font mapping file does; and one not
    /// found replaced by <see cref="Replacement"/>, as AutoCAD's FONTALT replaces it, said once a font through
    /// <see cref="CadDocument.FontMissing"/>. The one font carried is the presenter's own single-stroke font,
    /// <see cref="BuiltIn"/>; no folder of AutoCAD's is looked in unless the user lists it.
    /// </summary>
    /// <remarks>
    /// Fonts of no folders and no replacement are off: every style is drawn as before fonts were read, its TrueType typeface
    /// or file by the canvas and an SHX font in the canvas's own font, and nothing is said. A file found that is no SHX font,
    /// or cannot be read, is said once at Debug and taken as not found; nothing throws. The fonts read are kept for every
    /// document by their path, the size and time of their file, at most 48 at once, the one used longest ago let go first.
    /// </remarks>
    public sealed class CadFonts
    {
        /// <summary>
        /// The name the presenter's own font goes by, as a replacement or a style's font: ghsimplex, with or without .shx. No
        /// file has it on disk; a file of that name in a folder looked in is passed over for it.
        /// </summary>
        public const string BuiltInName = "ghsimplex";

        /// <summary>The title of the presenter's own font, as the line saying a font not found names it: GeometryHelper Simplex.</summary>
        public const string BuiltInTitle = "GeometryHelper Simplex";

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
        private static readonly Lazy<ShxFont> Own = new Lazy<ShxFont>(() => ShxFont.Read(HersheySimplex.Unifont()));
        private static long _clock;

        private readonly Func<string, bool> _familyExists;
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Makes the fonts of a document.</summary>
        /// <param name="folders">The folders fonts are looked for in, in order, after the drawing's own; null or empty for none.</param>
        /// <param name="replacement">The font a font not found is drawn in, as FONTALT: <see cref="BuiltInName"/>, an SHX file (simplex.shx), a TrueType file or a typeface, and after a comma the big font a big font not found is drawn in (simplex.shx,gbcbig.shx); null or empty for the canvas's own.</param>
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
        /// Gets the folders a drawing's fonts start with: none. SHX fonts are read from the folder of the drawing and from the
        /// folders the user adds in Global settings' Fonts page; a TrueType file is looked for in the Fonts folder of Windows as
        /// well. AutoCAD's Fonts folders are not looked for.
        /// </summary>
        public static IReadOnlyList<string> DefaultFolders { get; } = Array.Empty<string>();

        /// <summary>
        /// Gets the presenter's own single-stroke font, GeometryHelper Simplex (<see cref="BuiltInName"/>), drawn as an SHX font
        /// is: the Roman Simplex letters of the Hershey fonts of the U.S. National Bureau of Standards, 1967, ASCII 32 to 126
        /// with the degree, plus-minus and diameter signs, 98 letters. Its capitals reach <see cref="ShxFont.Above"/> 21 units
        /// above the baseline and its descenders 7 below, as romans.shx's: "ABC123" 10 high is 10 high and about 42 wide.
        /// </summary>
        public static ShxFont BuiltIn => Own.Value;

        /// <summary>Gets the folders fonts are looked for in, in order, after the drawing's own folder.</summary>
        public IReadOnlyList<string> Folders { get; }

        /// <summary>Gets the font a font not found is drawn in, and after a comma the big font a big font not found is drawn in; empty for the canvas's own.</summary>
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
        /// Gets these fonts with one more font drawn in another, as a font missing is given its own replacement: the same
        /// folders and replacement, the name added to <see cref="Substitutions"/> or its font changed.
        /// </summary>
        /// <param name="name">The font a style names: romans.shx.</param>
        /// <param name="drawnIn">The font it is drawn in: isocp.shx.</param>
        /// <returns>The fonts.</returns>
        /// <exception cref="ArgumentException">Thrown when either name is null or blank.</exception>
        public CadFonts WithSubstitution(string name, string drawnIn)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A font's name is needed.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(drawnIn))
            {
                throw new ArgumentException("The font it is drawn in is needed.", nameof(drawnIn));
            }

            var table = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> pair in Substitutions)
            {
                table[pair.Key] = pair.Value;
            }

            table[name.Trim()] = drawnIn.Trim();
            return new CadFonts(Folders, Replacement, table, _familyExists);
        }

        /// <summary>
        /// Reads a font mapping file as AutoCAD's FONTMAP reads acad.fmp: a line <c>oldfont;newfont</c> for each font drawn in
        /// another (romans.shx;isocp.shx), blank lines and lines of no semicolon or a blank half passed over, in the order of
        /// the file. A file that is not there or cannot be read gives none, said once at Debug; nothing throws.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The pairs, each name to the name it is drawn in.</returns>
        public static IReadOnlyList<KeyValuePair<string, string>> ReadFontMap(string path)
        {
            var pairs = new List<KeyValuePair<string, string>>();

            if (string.IsNullOrWhiteSpace(path))
            {
                return pairs;
            }

            string[] lines;

            try
            {
                lines = File.ReadAllLines(path.Trim());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException || e is System.Security.SecurityException)
            {
                lock (Loaded)
                {
                    if (Unread.Add("fmp|" + path.Trim()))
                    {
                        GeometryHelperLog.Debug("CadFonts: the font mapping file " + path + " could not be read (" + e.Message + "); no font is mapped by it.");
                    }
                }

                return pairs;
            }

            foreach (string line in lines)
            {
                int semicolon = line.IndexOf(';');

                if (semicolon > 0 && line.Substring(0, semicolon).Trim().Length > 0 && line.Substring(semicolon + 1).Trim().Length > 0)
                {
                    pairs.Add(new KeyValuePair<string, string>(line.Substring(0, semicolon).Trim(), line.Substring(semicolon + 1).Trim()));
                }
            }

            return pairs;
        }

        /// <summary>
        /// Finds a font's file: the path as written where it is a full path that is there, else its file name in the drawing's
        /// folder and then in <see cref="Folders"/>, <c>.shx</c> added to a name of no extension (txt is txt.shx); a TrueType
        /// file (.ttf, .ttc, .otf) last in the Fonts folder of Windows, as the system's fonts.
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
            CadFontFace face = Face(fileName, bigFontFileName, typeface, drawingFolder, out CadMissingFont missing, out CadMissingFont missingBig);
            report = null;

            lock (_reported)
            {
                foreach (CadMissingFont font in new[] { missing, missingBig })
                {
                    if (font != null && _reported.Add(font.Key))
                    {
                        report = report == null ? font.Line : report + " " + font.Line;
                    }
                }
            }

            if (report != null)
            {
                GeometryHelperLog.Debug("CadFonts: " + report);
            }

            return face;
        }

        /// <summary>
        /// The SHX fonts and big fonts the text styles of a drawing name that are not found, as AutoCAD lists its missing SHX
        /// files on opening a drawing, each with what it is drawn in, in the order of their names; a style of shapes and one of
        /// a TrueType font left out. With <paramref name="mark"/> each is taken as said, so that drawing it says it no more.
        /// </summary>
        internal IReadOnlyList<CadMissingFont> Missing(DxfDatabase database, string drawingFolder, bool mark)
        {
            var found = new List<CadMissingFont>();

            if (database == null || IsOff)
            {
                return found;
            }

            foreach (DxfTextStyleTableRecord style in database.TextStyleTable)
            {
                if (style.IsShapeFile)
                {
                    continue;
                }

                Face(style.FileName, style.BigFontFileName, style.Font.TypeFace, drawingFolder, out CadMissingFont missing, out CadMissingFont missingBig);

                foreach (CadMissingFont font in new[] { missing, missingBig })
                {
                    if (font != null && font.IsShx && !found.Any(f => string.Equals(f.Key, font.Key, StringComparison.Ordinal)))
                    {
                        found.Add(font);
                    }
                }
            }

            found = found.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.IsBigFont).ThenBy(f => f.Name, StringComparer.Ordinal).ToList();

            if (mark)
            {
                lock (_reported)
                {
                    foreach (CadMissingFont font in found)
                    {
                        _reported.Add(font.Key);
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// The face a text style is drawn in, and its font and big font where they are not found: a font not found drawn in
        /// the first half of <see cref="Replacement"/>, a big font not found in its half after the comma, else its letters as
        /// question marks. With fonts off, the style's typeface or file as the canvas takes it.
        /// </summary>
        private CadFontFace Face(string fileName, string bigFontFileName, string typeface, string drawingFolder, out CadMissingFont missing, out CadMissingFont missingBig)
        {
            missing = null;
            missingBig = null;
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
                string alternate = Half(0);
                CadFontFace replaced = alternate.Length == 0 ? null : Find(alternate, drawingFolder, true, false);
                string drawnIn = replaced == null ? string.Empty : IsBuiltIn(alternate) ? BuiltInTitle : alternate;
                string what = named + (substituted ? " (as " + wanted + ")" : string.Empty);
                string extension = Path.GetExtension(named).ToLowerInvariant();
                string line = "Font " + what + " not found: using " + (drawnIn.Length == 0 ? "the drawing font" : drawnIn) + ".";
                missing = new CadMissingFont(named, false, drawnIn, line, !isFamily && (extension.Length == 0 || extension == ".shx"));
                face = replaced ?? new CadFontFace(named, null, null, null);
            }

            if (face.Shx == null || string.IsNullOrWhiteSpace(bigFontFileName))
            {
                return face;
            }

            string big = bigFontFileName.Trim();
            string used = Substitute(big);
            ShxFont bigFont = LoadBig(used, drawingFolder);

            if (bigFont == null)
            {
                used = Half(1);
                bigFont = used.Length == 0 ? null : LoadBig(used, drawingFolder);
                string line = "Big font " + big + " not found: " + (bigFont == null ? "its letters are drawn as question marks." : "using " + used + ".");
                missingBig = new CadMissingFont(big, true, bigFont == null ? string.Empty : used, line, true);

                if (bigFont == null)
                {
                    return face;
                }
            }

            return new CadFontFace(face.Name, face.Shx, bigFont, PageOf(used));
        }

        /// <summary>The big font of a name, found as a font is; null where none is found or the file is no big font.</summary>
        private ShxFont LoadBig(string name, string drawingFolder)
        {
            ShxFont font = Load(Locate(name, drawingFolder));
            return font != null && font.Kind == ShxFontKind.BigFont ? font : null;
        }

        /// <summary>A half of <see cref="Replacement"/>: 0 the font before the comma, 1 the big font after it; empty where it has none.</summary>
        private string Half(int which)
        {
            int comma = Replacement.IndexOf(',');
            return comma < 0 ? (which == 0 ? Replacement : string.Empty) : (which == 0 ? Replacement.Substring(0, comma) : Replacement.Substring(comma + 1)).Trim();
        }

        private static bool IsBuiltIn(string name)
            => string.Equals(name, BuiltInName, StringComparison.OrdinalIgnoreCase) || string.Equals(name, BuiltInName + ".shx", StringComparison.OrdinalIgnoreCase);

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
        /// alone; <see cref="BuiltInName"/> the presenter's own font. Null where none is found.
        /// </summary>
        private CadFontFace Find(string name, string drawingFolder, bool family, bool shxOnly)
        {
            if (IsBuiltIn(name))
            {
                return new CadFontFace(BuiltInName, BuiltIn, null, null);
            }

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
                string extension = Path.GetExtension(file).ToLowerInvariant();
                bool trueType = extension == ".ttf" || extension == ".ttc" || extension == ".otf";
                string system = trueType ? Environment.GetFolderPath(Environment.SpecialFolder.Fonts) : null;

                foreach (string folder in new[] { drawingFolder }.Concat(Folders).Concat(new[] { system }))
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

    /// <summary>
    /// An SHX font or big font a text style names that is not found, as AutoCAD's list of missing SHX files gives it: romans.shx
    /// drawn in GeometryHelper Simplex, gbcbig.shx a big font whose letters are drawn as question marks.
    /// </summary>
    public sealed class CadMissingFont
    {
        internal CadMissingFont(string name, bool isBigFont, string drawnIn, string line, bool isShx)
        {
            Name = name;
            IsBigFont = isBigFont;
            DrawnIn = drawnIn;
            Line = line;
            IsShx = isShx;
        }

        /// <summary>Gets the font's name as the style writes it: romans.shx, or txt for txt.shx.</summary>
        public string Name { get; }

        /// <summary>Gets whether it is a style's big font.</summary>
        public bool IsBigFont { get; }

        /// <summary>
        /// Gets the font it is drawn in, as the replacement names it (GeometryHelper Simplex, simplex.shx, gbcbig.shx); empty
        /// where none is found, a font then drawn in the drawing font and a big font's letters as question marks.
        /// </summary>
        public string DrawnIn { get; }

        /// <summary>The line said where it is met as the text is drawn.</summary>
        internal string Line { get; }

        /// <summary>What it is said once by: an SHX font by its name without .shx, so that a style's isocp.shx and an MTEXT's \Fisocp are one; else its line.</summary>
        internal string Key => IsShx ? (IsBigFont ? "big|" : "shx|") + (Name.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) ? Name.Substring(0, Name.Length - 4) : Name).ToUpperInvariant() + "|" + DrawnIn : Line;

        /// <summary>Whether the style names it as an SHX file rather than a TrueType file or typeface.</summary>
        internal bool IsShx { get; }

        /// <summary>Gets the font as the list of missing SHX files gives it: "romans.shx drawn in GeometryHelper Simplex".</summary>
        /// <returns>The text.</returns>
        public override string ToString()
            => Name + (IsBigFont ? " (big font)" : string.Empty) + (DrawnIn.Length > 0 ? " drawn in " + DrawnIn : IsBigFont ? " drawn as question marks" : " drawn in the drawing font");
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
