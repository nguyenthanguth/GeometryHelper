using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    /// saying so once a file at Debug; a file found but not read as a font is said once at Warn. A style of an SHX font and a
    /// big font (<see cref="DxfTextStyleTableRecord.BigFontFileName"/>) found in those folders is measured as AutoCAD 2021
    /// draws it (check F3), each letter from one font or the other as <see cref="Text"/> says; one whose big font is not
    /// found or read only where every letter is its main font's; and a TrueType style's big font is not drawn at all.
    /// </remarks>
    internal sealed class DxfFonts
    {
        // The code page of each big font AutoCAD 2021 knows by its name (check F3), the letters of a text read in it to the
        // big font's numbers: 中 is D6D0 in gbcbig.shx (GBK), A4A4 in chineset.shx (Big5) and 9286 in extfont.shx (Shift-JIS).
        // AutoCAD reads a big font of another name, or one named with its folder, in none of them.
        private static readonly Dictionary<string, int> BigPages = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "bigfont", 932 }, { "extfont", 932 }, { "extfont2", 932 }, { "@extfont2", 932 }, { "gbcbig", 936 },
            { "whgdtxt", 949 }, { "whgtxt", 949 }, { "whtgtxt", 949 }, { "whtmtxt", 949 }, { "chineset", 950 },
        };

        // The byte of each ASCII letter, which is its own in every code page, read but never written.
        private static readonly byte[][] Ascii = Enumerable.Range(0, 0x80).Select(c => new[] { (byte)c }).ToArray();

        // The code pages of the big fonts, got once a set of the path, so that a page registered after one set is got at the next.
        private readonly Dictionary<int, Encoding> _pages = new Dictionary<int, Encoding>();
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
        /// The font of a style, and the big font beside it, null where the style names none or its font is a TrueType font,
        /// whose big font AutoCAD 2021 does not draw (check F3); null where it is not measured: a font of shapes, one written
        /// down the page, one not found or not read, a big font, and a TrueType font that is not the face AutoCAD 2021 draws
        /// the style in (check F2): one of another typeface, bold or italic than the style's <see cref="DxfFontDescriptor"/>
        /// (arial.ttf for Arial bold, which AutoCAD draws in arialbd.ttf), or a collection of a style of no typeface, which
        /// AutoCAD does not load.
        /// </summary>
        internal DxfFont FontOf(DxfTextStyleTableRecord style, out Big big)
        {
            big = null;

            if (style == null || style.IsShapeFile || style.IsVertical)
            {
                return null;
            }

            string bigName = style.BigFontFileName.Trim();
            DxfFont font = Find(style.FileName);

            if (font is DxfShxFont shx && shx.IsBig)
            {
                lock (_lock)
                {
                    Say(style.FileName.Trim() + "|main", "DxfDatabase: the font " + style.FileName.Trim() + " is a big font, which a style draws beside its font and not as it; its texts are laid out as they are without fonts.", true);
                }

                return null;
            }

            if (!(font is DxfTrueTypeFont trueType))
            {
                big = font != null && bigName.Length > 0 ? BigOf(bigName) : null;
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

        /// <summary>
        /// The big font of a style's file name, found as its font is, and the code page AutoCAD 2021 reads a text's letters in
        /// for it: of a name it knows, bare, its extension or not and of any case (<c>gbcbig</c>, <c>GBCBIG.SHX</c>), and none
        /// for another, or one written with its folder; its font null where it is not found or is no big font, said once at
        /// Debug, as is a code page the runtime has not got (on .NET without <c>CodePagesEncodingProvider</c> registered).
        /// </summary>
        private Big BigOf(string name)
        {
            DxfFont found = Find(name);

            if (!(found is DxfShxFont shx) || !shx.IsBig)
            {
                if (found != null)
                {
                    lock (_lock)
                    {
                        Say(name + "|big", "DxfDatabase: the big font " + name + " is no big font; its texts are laid out as they are without fonts where a letter is not their font's.", false);
                    }
                }

                return new Big(null, null);
            }

            Encoding page = null;
            string bare = name.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;

            if (name.IndexOfAny(new[] { '\\', '/', ':' }) < 0 && BigPages.TryGetValue(bare, out int number))
            {
                lock (_lock)
                {
                    if (!_pages.TryGetValue(number, out page))
                    {
                        try
                        {
                            // No best fit: a letter the page has not is none (check F3, the yen sign of chineset.shx drawn from txt.shx).
                            page = Encoding.GetEncoding(number, new EncoderReplacementFallback(string.Empty), DecoderFallback.ReplacementFallback);
                        }
                        catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
                        {
                            page = null;
                        }

                        _pages.Add(number, page);
                    }
                }

                if (page == null)
                {
                    lock (_lock)
                    {
                        Say(name + "|page", "DxfDatabase: the code page " + number.ToString(CultureInfo.InvariantCulture) + " of the big font " + name + " is not in the runtime (register CodePagesEncodingProvider); its texts are measured where every letter is of ASCII.", false);
                    }
                }
            }

            return new Big(shx, page);
        }

        /// <summary>A style's big font and the code page AutoCAD 2021 reads its letters in: the font null where it is not found or read, the page null where AutoCAD reads it in none (a font of another name) or the runtime has it not.</summary>
        internal sealed class Big
        {
            internal Big(DxfShxFont font, Encoding page)
            {
                Font = font;
                Page = page;
            }

            /// <summary>The big font; null where it is not found or read.</summary>
            internal DxfShxFont Font { get; }

            /// <summary>The code page its letters are read in; null for none.</summary>
            internal Encoding Page { get; }
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
            string why = shx ? "is no SHX font of shapes 1.0 or 1.1, unifont or big font, or is cut short" : "is no TrueType font of glyf outlines and a letter A, or is cut short";

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
        /// of that number; a letter the font has not drawn as its question mark. Beside a big font that is read, each letter
        /// from the font or the big font as <see cref="Pick"/> says, a letter neither has drawn as the big font's question mark
        /// where it has one and else the font's, and the first letter of the font after letters of the big font that puts its
        /// pen down where it starts inked too where they began, as AutoCAD 2021 boxes it (check F3): txt and gbcbig "中A" 4
        /// high from 0, not from 中's ink at 0.529, and " 中A" from 4. Null where a big font beside it that is not read would
        /// draw a letter, a TrueType font has not one, and for words of %%u, %%o or %%k, whose lines AutoCAD 2021 boxes them by
        /// (txt "%%u%%oA" 4 high centred on -1.4, not on the -1.333 of its letter), which are not measured; but those of %%u
        /// and %%o of a TrueType font, which AutoCAD does not box them by (check F1a, Arial "%%u%%oA" centred as "A").
        /// </summary>
        internal static DxfTextLine Text(DxfFont font, Big big, string words, double height, double widthFactor)
        {
            var line = new DxfTextLine(font);
            bool drawn = big?.Font != null;
            List<int> numbers = drawn ? new List<int>() : null;
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
                    number = drawn ? CodePoint(words, ref i) : NumberOf(font, words, ref i);
                }

                if (drawn)
                {
                    numbers.Add(number);
                }
                else if (!line.Add(number, widthFactor, height, big != null))
                {
                    return null;
                }
            }

            return !drawn || Drawn(line, font, big, numbers, null, widthFactor, height, true) ? line : null;
        }

        /// <summary>
        /// Adds letters to a line, each drawn from a font or the big font beside it as <see cref="Pick"/> says, at a width
        /// factor and height, or as each letter of an MTEXT is drawn where they are given; false where one is not measured. A
        /// letter neither font has is, in a TEXT, the big font's question mark where it has one, else the font's; and a letter of
        /// the font after letters of the big font that puts its pen down where it starts is inked where they began too, as
        /// AutoCAD 2021 boxes a TEXT (check F3).
        /// </summary>
        private static bool Drawn(DxfTextLine line, DxfFont font, Big big, List<int> numbers, List<Letter> drawn, double widthFactor, double height, bool text)
        {
            bool afterBig = false;
            double began = 0.0;
            int k = 0;

            while (k < numbers.Count)
            {
                // An MTEXT's ideographic space is a space of its own (check F3: txt and extfont.shx U+3000 and 中 4 high 8.267
                // wide, from the start of the line), which is not measured.
                if ((!text && numbers[k] == 0x3000) || !Pick(font, big, numbers, k, out DxfFont from, out int number, out int taken))
                {
                    return false;
                }

                if (from == null)
                {
                    if (!text)
                    {
                        return false;
                    }

                    from = big.Font.Has('?') ? big.Font : font;
                    number = '?';
                }

                DxfFont.Glyph glyph = from.GlyphOf(number);

                if (glyph != null && !glyph.IsWhole)
                {
                    return false;
                }

                if (from == big.Font)
                {
                    began = afterBig ? began : line.Advance;
                    afterBig = true;
                }
                else
                {
                    if (text && afterBig && glyph != null && glyph.InksOrigin)
                    {
                        line.Dot(began, 0.0);
                    }

                    afterBig = false;
                }

                // The letter taken last is the one a space is told by: special.shx's "~ " ends in a space, though drawn as one
                // letter; the ideographic space drawn as a space is none (check F3).
                if (drawn == null)
                {
                    line.Add(from, glyph, numbers[k + taken - 1], widthFactor, height);
                }
                else
                {
                    line.Add(from, glyph, numbers[k + taken - 1], drawn[k].Width, drawn[k].Height, drawn[k].Track, drawn[k].Marks);
                }

                k += taken;
            }

            line.BesideBig();
            return true;
        }

        /// <summary>
        /// The font a letter of words is drawn from beside a big font that is read, and its number there, as AutoCAD 2021 draws
        /// it (check F3), and how many letters it takes; the font null for a letter neither has, and false where it is not
        /// measured. The letter is read as bytes in the big font's code page (ASCII as itself), and drawn:
        /// <list type="bullet">
        /// <item>°, ± and ∅, the signs of %%d, %%p and %%c, from the font where it has them;</item>
        /// <item>a byte that opens a letter of two in the big font, with the next letter's byte, as that letter of the big font
        /// (special.shx's "~!");</item>
        /// <item>a space as the big font's where it has one (extfont.shx's, 9 of 15 wide), else the font's;</item>
        /// <item>another byte alone as the font's letter, else the big font's;</item>
        /// <item>a letter of two bytes as the big font's (中, Ж beside extfont.shx), else the font's (Ж beside gbcbig.shx), and
        /// the ideographic space a space;</item>
        /// <item>a letter the page has not as the font's.</item>
        /// </list>
        /// Not measured: a letter beyond ASCII beside a big font of no code page or a font of shapes, and a byte that opens a
        /// letter of two before one of two bytes.
        /// </summary>
        private static bool Pick(DxfFont font, Big big, List<int> numbers, int k, out DxfFont from, out int number, out int taken)
        {
            DxfShxFont bigFont = big.Font;
            int c = numbers[k];
            from = null;
            number = c;
            taken = 1;

            // A font of shapes beside a big font is not measured (check F3: times.shx "~!" 4 high beside gbcbig.shx inked a
            // third of a unit higher than its glyphs are read).
            if (!ByUnicode(font) || (c >= 0x80 && big.Page == null))
            {
                return false;
            }

            // °, ± and ∅ are the font's (check F3: txt.shx's degree sign beside gbcbig.shx's A1E3).
            if ((c == 0xB0 || c == 0xB1 || c == 0x2205) && font.GlyphOf(c) != null)
            {
                from = font;
                return true;
            }

            byte[] bytes = BytesOf(big.Page, c);

            if (bytes == null || bytes.Length > 2)
            {
                return false;
            }

            if (bytes.Length == 1 && bigFont.Opens(bytes[0]) && k + 1 < numbers.Count)
            {
                byte[] next = BytesOf(big.Page, numbers[k + 1]);

                if (next == null || next.Length != 1)
                {
                    return false;
                }

                taken = 2;
                number = (bytes[0] << 8) | next[0];
                from = bigFont.Has(number) ? bigFont : null;
                return true;
            }

            if (bytes.Length == 1 && bytes[0] == ' ')
            {
                from = bigFont.Has(' ') ? bigFont : font;
                return true;
            }

            // Whatever the big font's ranges: gbcbig.shx's 9DAE (潁), whose 9D its ranges A1 to FE leave out, is drawn (check F3).
            if (bytes.Length == 2 && bigFont.Has((bytes[0] << 8) | bytes[1]))
            {
                from = bigFont;
                number = (bytes[0] << 8) | bytes[1];
                return true;
            }

            if (font.GlyphOf(c) != null)
            {
                from = font;
                return true;
            }

            if (bytes.Length == 1 && bigFont.Has(bytes[0]))
            {
                from = bigFont;
                number = bytes[0];
            }
            else if (c == 0x3000)
            {
                from = bigFont.Has(' ') ? bigFont : font;
                number = ' ';
            }

            return true;
        }

        /// <summary>The bytes of a letter in a code page, an ASCII letter its own; none where the page has it not, and null where it is no letter.</summary>
        private static byte[] BytesOf(Encoding page, int c)
        {
            if (c < 0x80)
            {
                return Ascii[c];
            }

            if (page == null || c > 0x10FFFF || (c >= 0xD800 && c <= 0xDFFF))
            {
                return null;
            }

            return page.GetBytes(char.ConvertFromUtf32(c));
        }

        /// <summary>The Unicode number of the letter at a place of words, read on past it, the two halves of a letter beyond U+FFFF taken together.</summary>
        private static int CodePoint(string words, ref int i)
        {
            if (char.IsHighSurrogate(words[i]) && i + 1 < words.Length && char.IsLowSurrogate(words[i + 1]))
            {
                int pair = char.ConvertToUtf32(words[i], words[i + 1]);
                i += 2;
                return pair;
            }

            return words[i++];
        }

        /// <summary>
        /// The words of an MTEXT of one line measured in a font, as AutoCAD 2021 lays them out: plain letters, %%c, %%d and %%p
        /// as a TEXT reads them, the width of the letters set by <c>\W0.7;</c> and their height by <c>\H8;</c> or <c>\H2x;</c>
        /// from where each stands, braces keeping a change within them, <c>\\</c>, <c>\{</c> and <c>\}</c> the letter, and
        /// the codes <see cref="MTextLetters"/> reads besides (check F4). Null for any other code, a new paragraph or line among
        /// them (laid out by <see cref="DxfMTextLayout"/>), letters lined under or over, and a letter the font has not, which
        /// AutoCAD 2021 draws from another
        /// font in an MTEXT (txt "中" 4.335 wide, not the 2 of its question mark); null for a font of shapes, whose MTEXT
        /// AutoCAD 2021 measures otherwise than its TEXT (times.shx "A" 3.667 wide, its ink 3.933); and of a TrueType font, null
        /// for a change of width or height, or a brace, after the first letter (check F2). Beside a big font that is read, each
        /// letter from the font or the big font as a TEXT draws it (<see cref="Pick"/>), a letter neither has null, and no
        /// letter inked where letters of the big font before it began (check F3: txt and gbcbig "中A" 4 high 5.049 wide, from
        /// 中's ink at 0.529).
        /// </summary>
        internal static DxfTextLine MText(DxfFont font, Big big, string contents, double height)
        {
            // Words lined under or over are not measured for a multileader, which F4 did not check.
            List<Letter> letters = MTextLetters(font, big, contents, height);
            return letters == null || letters.Exists(l => l.Number < 0 || (l.Marks & 3) != 0) ? null : Measure(font, big, letters, 0, letters.Count, height);
        }

        /// <summary>
        /// A letter of an MTEXT's words, as <see cref="MTextLetters"/> reads it: its number in its font, the width factor and the
        /// height it is drawn at, and whether it is a space it may break at; a number below nought for a new paragraph.
        /// </summary>
        internal struct Letter
        {
            internal Letter(int number, double width, double height, double track = 1.0, int align = 0, int marks = 0, int tint = 0)
            {
                Number = number;
                Width = width;
                Height = height;
                Track = track;
                Align = align;
                Marks = marks;
                Tint = tint;
            }

            /// <summary>The number of the letter in its font, <see cref="Paragraph"/> for a new paragraph.</summary>
            internal int Number { get; }

            /// <summary>The width factor it is drawn at.</summary>
            internal double Width { get; }

            /// <summary>The height it is drawn at.</summary>
            internal double Height { get; }

            /// <summary>The factor its pen's move is spread by (<c>\T</c>).</summary>
            internal double Track { get; }

            /// <summary>How it is held in a line of taller letters (<c>\A</c>): 0 on the baseline, 1 at the middle, 2 at the top.</summary>
            internal int Align { get; }

            /// <summary>The lines drawn with it: 1 under it (<c>\L</c>), 2 over it (<c>\O</c>), 4 through it (<c>\K</c>).</summary>
            internal int Marks { get; }

            /// <summary>Which colour of the words it is in, counted from nought for the MTEXT's own, one more at each <c>\C</c> or <c>\c</c>.</summary>
            internal int Tint { get; }

            /// <summary>Whether it begins a new paragraph (<c>\P</c>).</summary>
            internal bool IsParagraph => Number == Paragraph;

            /// <summary>Whether it is a space a line breaks at (U+0020, not the space of <c>\~</c>, which does not break).</summary>
            internal bool IsSpace => Number == ' ';
        }

        /// <summary>The number <see cref="Letter"/> gives a new paragraph.</summary>
        internal const int Paragraph = -1;

        /// <summary>
        /// The letters of an MTEXT's words, read as <see cref="MText"/> reads them, a new paragraph (<c>\P</c>) a letter of
        /// <see cref="Paragraph"/>, and the space of <c>\~</c>, which does not break a line, the font's U+00A0; and as AutoCAD
        /// 2021 draws them (check F4) the codes that spread the pen's moves (<c>\T1.5;</c>, not of a TrueType font), hold letters
        /// at the middle or top of a line of taller letters (<c>\A1;</c>, <c>\A2;</c>), line them under, over and through
        /// (<c>\L</c>, <c>\O</c>, <c>\K</c> and their ends <c>\l</c>, <c>\o</c>, <c>\k</c>) and colour them (<c>\C1;</c>,
        /// <c>\c255;</c>), which changes no place. Null where they are not measured, as <see cref="MText"/> says, and for slanted
        /// letters (<c>\Q</c>), which AutoCAD 2021 boxes otherwise in an MTEXT than in a TEXT (check F4: txt "\Q12;A/B jig" and
        /// three lines more, 45 wide, 27.758 wide, not the 27.900 of letters slanted as a TEXT's), a font switch (<c>\f</c>,
        /// <c>\F</c>), a stack (<c>\S</c>), a paragraph's indents and tabs (<c>\p</c>), a column break (<c>\N</c>) and any other
        /// code.
        /// </summary>
        internal static List<Letter> MTextLetters(DxfFont font, Big big, string contents, double height)
        {
            if (!ByUnicode(font))
            {
                return null;
            }

            bool drawn = big?.Font != null;

            // A multibyte code beside a big font is drawn from another font (check F3: chineset.shx "\M+5D6D0" 4 high 4.335
            // wide, not its own 中), and is not measured.
            if (drawn && (contents.IndexOf("\\M+", StringComparison.Ordinal) >= 0 || contents.IndexOf("\\m+", StringComparison.Ordinal) >= 0))
            {
                return null;
            }

            string words = DxfReader.Decode(DxfReader.Carets(contents), true);
            var letters = new List<Letter>();
            var kept = new Stack<Letter>();
            double width = 1.0;
            double high = height;
            double track = 1.0;
            int align = 0;
            int marks = 0;
            int tint = 0;
            int tints = 0;
            int i = 0;

            while (i < words.Length)
            {
                char c = words[i];
                int number;

                // A change of the letters' width or height after a letter of TrueType is not measured: AutoCAD 2021 sets the
                // letters after it on from the ink of those before (check F2), Arial "A\H2x;BC" 4 high 18.819 wide, not 18.810.
                if ((c == '{' || c == '}' || (c == '\\' && i + 1 < words.Length && (words[i + 1] == 'W' || words[i + 1] == 'H'))) && letters.Count > 0 && font is DxfTrueTypeFont)
                {
                    return null;
                }

                if (c == '{')
                {
                    kept.Push(new Letter(0, width, high, track, align, marks, tint));
                    i++;
                    continue;
                }

                if (c == '}')
                {
                    if (kept.Count > 0)
                    {
                        Letter was = kept.Pop();
                        width = was.Width;
                        high = was.Height;
                        track = was.Track;
                        align = was.Align;
                        marks = was.Marks;
                        tint = was.Tint;
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
                    else if (code == 'P')
                    {
                        number = Paragraph;
                        i += 2;
                    }
                    else if (code == '~')
                    {
                        number = 0xA0;
                        i += 2;
                    }
                    else if (code == 'W' || code == 'H' || code == 'T' || code == 'A')
                    {
                        // A TrueType font's spread is not measured: AutoCAD 2021 spreads it otherwise than its advances say
                        // (check F4: Arial "\T2;ABC DEF" 4 high 41.228 wide, not the 44.767 of its advances spread).
                        if (code == 'T' && font is DxfTrueTypeFont)
                        {
                            return null;
                        }

                        int end = words.IndexOf(';', i);
                        string value = end < 0 ? string.Empty : words.Substring(i + 2, end - i - 2).Trim();
                        bool times = code == 'H' && value.EndsWith("x", StringComparison.OrdinalIgnoreCase);

                        if (!double.TryParse(times ? value.Substring(0, value.Length - 1) : value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || !Guard.IsFinite(v) || !(v > 0.0 || (code == 'A' && v == 0.0)))
                        {
                            return null;
                        }

                        if (code == 'W')
                        {
                            width = v;
                        }
                        else if (code == 'H')
                        {
                            high = times ? high * v : v;
                        }
                        else if (code == 'T')
                        {
                            track = v;
                        }
                        else if (v == 0.0 || v == 1.0 || v == 2.0)
                        {
                            align = (int)v;
                        }
                        else
                        {
                            return null;
                        }

                        i = end + 1;
                        continue;
                    }
                    else if (code == 'C' || code == 'c')
                    {
                        // A colour changes no place, but begins a run of its own (check F4); its value is passed over.
                        int end = words.IndexOf(';', i);

                        if (end < 0)
                        {
                            return null;
                        }

                        tint = ++tints;
                        i = end + 1;
                        continue;
                    }
                    else if ("LlOoKk".IndexOf(code) >= 0)
                    {
                        int mark = code == 'L' || code == 'l' ? 1 : code == 'O' || code == 'o' ? 2 : 4;
                        marks = char.IsUpper(code) ? marks | mark : marks & ~mark;
                        i += 2;
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

                letters.Add(new Letter(number, width, high, track, align, marks, tint));
            }

            return letters;
        }

        /// <summary>
        /// The letters of an MTEXT from one place up to another measured as a line in a font, each from the font or the big
        /// font beside it as <see cref="MText"/> says; null where one is not measured. A new paragraph among them is not.
        /// </summary>
        internal static DxfTextLine Measure(DxfFont font, Big big, List<Letter> letters, int from, int to, double height)
        {
            var line = new DxfTextLine(font);

            if (big?.Font == null)
            {
                for (int k = from; k < to; k++)
                {
                    Letter letter = letters[k];

                    if (!line.Add(letter.Number, letter.Width, letter.Height, true, letter.Track, letter.Marks))
                    {
                        return null;
                    }
                }

                return line;
            }

            var numbers = new List<int>(to - from);
            List<Letter> drawn = letters.GetRange(from, to - from);

            for (int k = from; k < to; k++)
            {
                numbers.Add(letters[k].Number);
            }

            return Drawn(line, font, big, numbers, drawn, 1.0, height, false) ? line : null;
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
