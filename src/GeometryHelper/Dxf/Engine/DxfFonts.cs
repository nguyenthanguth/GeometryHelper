using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The SHX and TrueType fonts of a drawing, found in the folders of its <see cref="DxfDatabase.FontSearchPath"/> in their
    /// order and read once each, by the full path of the file; and the lines of words measured in them as AutoCAD 2021
    /// measures them (checks F1a and F2): "ABC" of txt.shx 4 high is inked from 0 to 10.667, the advances of A and B, 6 each,
    /// and the ink of C, 4, times 4/6; of arial.ttf from -0.008 to 11.269, its units 4/1466 of the drawing's, 1466 the top of
    /// its A.
    /// </summary>
    /// <remarks>
    /// A style is measured where its font is an SHX font of letters (<c>txt</c> read as <c>txt.shx</c>), or a TrueType font
    /// of a .ttf, .ttc or .otf file of glyf outlines that is the face AutoCAD draws the style in, found in those folders; not
    /// where it is a font of shapes, one written down the page, a TrueType font of CFF outlines or of symbols, one named by
    /// its typeface alone, or one not found, each of which the drawing's entities lay out as they did with no fonts, the log
    /// saying so once a file at Debug; a file found but not read as a font is said once at Warn. A style of a big font is
    /// measured where every letter is its main font's, which AutoCAD draws first.
    /// </remarks>
    internal sealed class DxfFonts
    {
        private readonly string[] _folders;
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DxfFont> _fonts = new Dictionary<string, DxfFont>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _said = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        /// <summary>The fonts of the folders of a search path, separated by semicolons, those of no name passed over.</summary>
        internal DxfFonts(string searchPath)
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
        /// The font of a style, and whether the style has a big font beside it; null where it is not measured: a font of
        /// shapes, one written down the page, one not found or not read, and a TrueType font that is not the face AutoCAD 2021
        /// draws the style in (check F2): one of another typeface, bold or italic than the style's <see cref="DxfFontDescriptor"/>
        /// (arial.ttf for Arial bold, which AutoCAD draws in arialbd.ttf), or a collection of a style of no typeface, which
        /// AutoCAD does not load.
        /// </summary>
        internal DxfFont FontOf(DxfTextStyleTableRecord style, out bool bigFont)
        {
            bigFont = false;

            if (style == null || style.IsShapeFile || style.IsVertical)
            {
                return null;
            }

            bigFont = style.BigFontFileName.Trim().Length > 0;
            DxfFont font = Find(style.FileName);

            if (!(font is DxfTrueTypeFont trueType))
            {
                return font;
            }

            DxfFontDescriptor face = style.Font;

            if (!trueType.IsSymbols && (face.TypeFace.Length == 0 ? !trueType.IsCollection : trueType.IsFace(face.TypeFace, face.Bold, face.Italic)))
            {
                return trueType;
            }

            string file = style.FileName.Trim();
            string named = face.TypeFace + (face.Bold ? " bold" : string.Empty) + (face.Italic ? " italic" : string.Empty);

            lock (_lock)
            {
                Say(file + "|" + named, trueType.IsSymbols
                    ? "DxfDatabase: the font " + file + " is a font of symbols, some of whose letters AutoCAD 2021 draws from other fonts; its texts are laid out as they are without fonts."
                    : face.TypeFace.Length == 0
                    ? "DxfDatabase: a style names the collection " + file + " but no typeface, which AutoCAD does not load; its texts are laid out as they are without fonts."
                    : "DxfDatabase: a style names the typeface " + named + ", which the font " + file + " (" + trueType.TypeFace + ") is not, and which AutoCAD draws it in; its texts are laid out as they are without fonts.", false);
            }

            return null;
        }

        /// <summary>The font of a file's name, <c>.shx</c> added where it has no extension, from the first folder that has it; null where none has it, it is no SHX or TrueType file, or it is no font.</summary>
        internal DxfFont Find(string fileName)
        {
            string name = (fileName ?? string.Empty).Trim();

            lock (_lock)
            {
                if (!_paths.TryGetValue(name, out string path))
                {
                    path = Locate(name);
                    _paths.Add(name, path);
                }

                if (path == null)
                {
                    return null;
                }

                if (!_fonts.TryGetValue(path, out DxfFont font))
                {
                    font = Load(path);
                    _fonts.Add(path, font);
                }

                return font;
            }
        }

        /// <summary>The full path of a font's file in the first folder that has it; null, said once at Debug, where it is no SHX or TrueType file (.shx, .ttf, .ttc, .otf) or none has it.</summary>
        private string Locate(string name)
        {
            string file;

            try
            {
                file = Path.GetFileName(name);

                if (file.Length > 0 && Path.GetExtension(file).Length == 0)
                {
                    file += ".shx";
                }
            }
            catch (ArgumentException)
            {
                file = string.Empty;
            }

            if (file.Length == 0 || !(IsShx(file) || IsTrueType(file)))
            {
                Say(name, "DxfDatabase: the font " + (name.Length == 0 ? "of no name" : name) + " is not of an SHX or TrueType file (.shx, .ttf, .ttc, .otf), which are all the library measures; its texts are laid out as they are without fonts.", false);
                return null;
            }

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

            Say(name, "DxfDatabase: the font " + file + " is in none of the folders of FontSearchPath; its texts are laid out as they are without fonts.", false);
            return null;
        }

        /// <summary>
        /// The font of a file, read: an SHX font of a .shx file, a TrueType font of any other; null, said once at Warn, where it
        /// cannot be read or is no SHX font of letters or TrueType font, and said once at Debug where it is a TrueType font of
        /// CFF outlines.
        /// </summary>
        private DxfFont Load(string path)
        {
            DxfFont font = null;
            bool shx = IsShx(path);
            string why = shx ? "is no SHX font of letters of shapes 1.0 or 1.1 or of a unifont, or is cut short" : "is no TrueType font of glyf outlines and a letter A, or is cut short";

            try
            {
                byte[] bytes = File.ReadAllBytes(path);

                if (!shx && DxfTrueTypeFont.IsCff(bytes))
                {
                    Say(path, "DxfDatabase: the font " + path + " is of CFF outlines, whose ink the library does not read; its texts are laid out as they are without fonts.", false);
                    return null;
                }

                font = shx ? (DxfFont)DxfShxFont.Read(bytes) : DxfTrueTypeFont.Read(bytes);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is NotSupportedException || e is OutOfMemoryException)
            {
                why = "could not be read (" + e.Message + ")";
            }

            if (font == null)
            {
                Say(path, "DxfDatabase: the font " + path + " " + why + "; its texts are laid out as they are without fonts.", true);
            }

            return font;
        }

        private static bool IsShx(string file) => string.Equals(Path.GetExtension(file), ".shx", StringComparison.OrdinalIgnoreCase);

        private static bool IsTrueType(string file)
        {
            string extension = Path.GetExtension(file);
            return string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".ttc", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase);
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

        #region Measuring

        /// <summary>
        /// The letters of the words of a TEXT, ATTRIB or ATTDEF measured in a font, as AutoCAD 2021 lays them out: %%c, %%d and
        /// %%p the diameter, degree and plus-minus signs (U+2205, U+00B0 and U+00B1 of a unifont, 129, 127 and 128 of a font of
        /// shapes, and of a TrueType font U+00B0, U+00B1 and its U+2205, else its U+00D8), %%% a per cent sign, %%nnn the letter
        /// of that number; a letter the font has not drawn as its question mark. Null where a big font beside it would draw a
        /// letter, or a TrueType font has not one, and for words of %%u, %%o or %%k, whose lines AutoCAD 2021 boxes them by (txt
        /// "%%u%%oA" 4 high centred on -1.4, not on the -1.333 of its letter), which are not measured; but those of %%u and
        /// %%o of a TrueType font, which AutoCAD does not box them by (check F1a, Arial "%%u%%oA" centred as "A").
        /// </summary>
        internal static DxfTextLine Text(DxfFont font, bool bigFont, string words, double height, double widthFactor)
        {
            var line = new DxfTextLine(font);
            int i = 0;

            while (i < words.Length)
            {
                int number;

                if (words[i] == '%' && i + 2 < words.Length && words[i + 1] == '%')
                {
                    char code = char.ToLowerInvariant(words[i + 2]);

                    if ((code == 'u' || code == 'o') && font is DxfTrueTypeFont)
                    {
                        i += 3;
                        continue;
                    }

                    if (code == 'u' || code == 'o' || code == 'k')
                    {
                        return null;
                    }

                    if (code == 'c' || code == 'd' || code == 'p' || code == '%')
                    {
                        number = code == '%' ? '%' : Sign(font, code);
                        i += 3;
                    }
                    else if (i + 4 < words.Length && IsDigit(words[i + 2]) && IsDigit(words[i + 3]) && IsDigit(words[i + 4]))
                    {
                        number = ((words[i + 2] - '0') * 100) + ((words[i + 3] - '0') * 10) + (words[i + 4] - '0');
                        i += 5;
                    }
                    else
                    {
                        number = '%';
                        i++;
                    }
                }
                else
                {
                    number = NumberOf(font, words, ref i);
                }

                if (!line.Add(number, widthFactor, height, bigFont))
                {
                    return null;
                }
            }

            return line;
        }

        /// <summary>
        /// The words of an MTEXT of one line measured in a font, as AutoCAD 2021 lays them out: plain letters, %%c, %%d and %%p
        /// as a TEXT reads them, the width of the letters set by <c>\W0.7;</c> and their height by <c>\H8;</c> or <c>\H2x;</c>
        /// from where each stands, braces keeping a change within them, and <c>\\</c>, <c>\{</c> and <c>\}</c> the letter. Null
        /// for any other code, a new line among them, and a letter the font has not, which AutoCAD 2021 draws from another
        /// font in an MTEXT (txt "中" 4.335 wide, not the 2 of its question mark); null for a font of shapes, whose MTEXT
        /// AutoCAD 2021 measures otherwise than its TEXT (times.shx "A" 3.667 wide, its ink 3.933); and of a TrueType font, null
        /// for a change of width or height, or a brace, after the first letter (check F2).
        /// </summary>
        internal static DxfTextLine MText(DxfFont font, string contents, double height)
        {
            if (!ByUnicode(font))
            {
                return null;
            }

            string words = DxfReader.Decode(DxfReader.Carets(contents), true);
            var line = new DxfTextLine(font);
            var kept = new Stack<KeyValuePair<double, double>>();
            double width = 1.0;
            double high = height;
            int i = 0;

            while (i < words.Length)
            {
                char c = words[i];
                int number;

                // A change of the letters' width or height after a letter of TrueType is not measured: AutoCAD 2021 sets the
                // letters after it on from the ink of those before (check F2), Arial "A\H2x;BC" 4 high 18.819 wide, not 18.810.
                if ((c == '{' || c == '}' || (c == '\\' && i + 1 < words.Length && (words[i + 1] == 'W' || words[i + 1] == 'H'))) && line.Count > 0 && font is DxfTrueTypeFont)
                {
                    return null;
                }

                if (c == '{')
                {
                    kept.Push(new KeyValuePair<double, double>(width, high));
                    i++;
                    continue;
                }

                if (c == '}')
                {
                    if (kept.Count > 0)
                    {
                        KeyValuePair<double, double> was = kept.Pop();
                        width = was.Key;
                        high = was.Value;
                    }

                    i++;
                    continue;
                }

                if (c == '\\')
                {
                    char code = i + 1 < words.Length ? words[i + 1] : '\0';

                    if (code == '\\' || code == '{' || code == '}')
                    {
                        number = NumberOf(font, code);
                        i += 2;
                    }
                    else if (code == 'W' || code == 'H')
                    {
                        int end = words.IndexOf(';', i);
                        string value = end < 0 ? string.Empty : words.Substring(i + 2, end - i - 2).Trim();
                        bool times = code == 'H' && value.EndsWith("x", StringComparison.OrdinalIgnoreCase);

                        if (!double.TryParse(times ? value.Substring(0, value.Length - 1) : value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || !Guard.IsFinite(v) || !(v > 0.0))
                        {
                            return null;
                        }

                        if (code == 'W')
                        {
                            width = v;
                        }
                        else
                        {
                            high = times ? high * v : v;
                        }

                        i = end + 1;
                        continue;
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (c == '%' && i + 2 < words.Length && words[i + 1] == '%' && "cdpCDP".IndexOf(words[i + 2]) >= 0)
                {
                    number = Sign(font, char.ToLowerInvariant(words[i + 2]));
                    i += 3;
                }
                else if (c == '\n' || c == '\r')
                {
                    return null;
                }
                else
                {
                    number = NumberOf(font, words, ref i);
                }

                if (!line.Add(number, width, high, true))
                {
                    return null;
                }
            }

            return line;
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        /// <summary>The number of a sign of %%c, %%d or %%p in a font: U+2205, U+00B0 and U+00B1 of a unifont, 129, 127 and 128 of a font of shapes, and of a TrueType font U+2205 where it has one and U+00D8 where it has not, as AutoCAD 2021 draws them (checks F1a and F2: Arial's U+00D8, isocpeur.ttf's U+2205).</summary>
        private static int Sign(DxfFont font, char code)
        {
            bool unicode = ByUnicode(font);

            switch (code)
            {
                case 'c': return font is DxfTrueTypeFont && font.GlyphOf(0x2205) == null ? 0xD8 : unicode ? 0x2205 : 129;
                case 'd': return unicode ? 0xB0 : 127;
                default: return unicode ? 0xB1 : 128;
            }
        }

        /// <summary>Whether a font's letters are numbered by Unicode: a TrueType font's and a unifont's, not those of a font of shapes, by their bytes.</summary>
        private static bool ByUnicode(DxfFont font) => !(font is DxfShxFont shx) || shx.IsUnifont;

        /// <summary>The number of the letter at a place of words, read on past it: its Unicode number in a unifont, the two halves of a letter beyond U+FFFF taken together; its byte in a font of shapes, a question mark beyond a byte.</summary>
        private static int NumberOf(DxfFont font, string words, ref int i)
        {
            if (char.IsHighSurrogate(words[i]) && i + 1 < words.Length && char.IsLowSurrogate(words[i + 1]))
            {
                int pair = char.ConvertToUtf32(words[i], words[i + 1]);
                i += 2;
                return ByUnicode(font) ? pair : '?';
            }

            return NumberOf(font, words[i++]);
        }

        private static int NumberOf(DxfFont font, char c) => ByUnicode(font) || c < 256 ? c : '?';

        #endregion
    }
}
