using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A font of a PDF page, reduced to what a drawing of it needs: which characters the codes of a string are, how far each
    /// moves the pen, and the outline of each where the font embeds a TrueType program. A font that embeds none, or one of
    /// another kind (CFF, Type 1), has no outlines and its text is drawn in the font of that name on the machine.
    /// </summary>
    internal sealed class PdfFont
    {
        private static readonly int[] WinAnsiHigh =
        {
            0x20AC,
            0x2022,
            0x201A,
            0x0192,
            0x201E,
            0x2026,
            0x2020,
            0x2021,
            0x02C6,
            0x2030,
            0x0160,
            0x2039,
            0x0152,
            0x2022,
            0x017D,
            0x2022,
            0x2022,
            0x2018,
            0x2019,
            0x201C,
            0x201D,
            0x2022,
            0x2013,
            0x2014,
            0x02DC,
            0x2122,
            0x0161,
            0x203A,
            0x0153,
            0x2022,
            0x017E,
            0x0178,
        };

        private static readonly int[] MacRomanHigh =
        {
            0xC4,
            0xC5,
            0xC7,
            0xC9,
            0xD1,
            0xD6,
            0xDC,
            0xE1,
            0xE0,
            0xE2,
            0xE4,
            0xE3,
            0xE5,
            0xE7,
            0xE9,
            0xE8,
            0xEA,
            0xEB,
            0xED,
            0xEC,
            0xEE,
            0xEF,
            0xF1,
            0xF3,
            0xF2,
            0xF4,
            0xF6,
            0xF5,
            0xFA,
            0xF9,
            0xFB,
            0xFC,
            0x2020,
            0xB0,
            0xA2,
            0xA3,
            0xA7,
            0x2022,
            0xB6,
            0xDF,
            0xAE,
            0xA9,
            0x2122,
            0xB4,
            0xA8,
            0x2260,
            0xC6,
            0xD8,
            0x221E,
            0xB1,
            0x2264,
            0x2265,
            0xA5,
            0xB5,
            0x2202,
            0x2211,
            0x220F,
            0x3C0,
            0x222B,
            0xAA,
            0xBA,
            0x3A9,
            0xE6,
            0xF8,
            0xBF,
            0xA1,
            0xAC,
            0x221A,
            0x192,
            0x2248,
            0x2206,
            0xAB,
            0xBB,
            0x2026,
            0xA0,
            0xC0,
            0xC3,
            0xD5,
            0x152,
            0x153,
            0x2013,
            0x2014,
            0x201C,
            0x201D,
            0x2018,
            0x2019,
            0xF7,
            0x25CA,
            0xFF,
            0x178,
            0x2044,
            0x20AC,
            0x2039,
            0x203A,
            0xFB01,
            0xFB02,
            0x2021,
            0xB7,
            0x201A,
            0x201E,
            0x2030,
            0xC2,
            0xCA,
            0xC1,
            0xCB,
            0xC8,
            0xCD,
            0xCE,
            0xCF,
            0xCC,
            0xD3,
            0xD4,
            0xF8FF,
            0xD2,
            0xDA,
            0xDB,
            0xD9,
            0x131,
            0x2C6,
            0x2DC,
            0xAF,
            0x2D8,
            0x2D9,
            0x2DA,
            0xB8,
            0x2DD,
            0x2DB,
            0x2C7,
        };

        private static readonly Dictionary<string, int> GlyphNames = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["space"] = 32,
            ["exclam"] = 33,
            ["quotedbl"] = 34,
            ["numbersign"] = 35,
            ["dollar"] = 36,
            ["percent"] = 37,
            ["ampersand"] = 38,
            ["quotesingle"] = 39,
            ["parenleft"] = 40,
            ["parenright"] = 41,
            ["asterisk"] = 42,
            ["plus"] = 43,
            ["comma"] = 44,
            ["hyphen"] = 45,
            ["minus"] = 0x2212,
            ["period"] = 46,
            ["slash"] = 47,
            ["colon"] = 58,
            ["semicolon"] = 59,
            ["less"] = 60,
            ["equal"] = 61,
            ["greater"] = 62,
            ["question"] = 63,
            ["at"] = 64,
            ["bracketleft"] = 91,
            ["backslash"] = 92,
            ["bracketright"] = 93,
            ["asciicircum"] = 94,
            ["underscore"] = 95,
            ["grave"] = 96,
            ["braceleft"] = 123,
            ["bar"] = 124,
            ["braceright"] = 125,
            ["asciitilde"] = 126,
            ["bullet"] = 0x2022,
            ["endash"] = 0x2013,
            ["emdash"] = 0x2014,
            ["quoteleft"] = 0x2018,
            ["quoteright"] = 0x2019,
            ["quotedblleft"] = 0x201C,
            ["quotedblright"] = 0x201D,
            ["degree"] = 0xB0,
            ["plusminus"] = 0xB1,
            ["multiply"] = 0xD7,
            ["divide"] = 0xF7,
            ["section"] = 0xA7,
            ["copyright"] = 0xA9,
            ["registered"] = 0xAE,
            ["Euro"] = 0x20AC,
            ["fi"] = 0xFB01,
            ["fl"] = 0xFB02,
            ["ellipsis"] = 0x2026,
            ["zero"] = 48,
            ["one"] = 49,
            ["two"] = 50,
            ["three"] = 51,
            ["four"] = 52,
            ["five"] = 53,
            ["six"] = 54,
            ["seven"] = 55,
            ["eight"] = 56,
            ["nine"] = 57,
            ["mu"] = 0xB5,
            ["Omega"] = 0x3A9,
            ["Delta"] = 0x394,
            ["pi"] = 0x3C0,
            ["alpha"] = 0x3B1,
            ["beta"] = 0x3B2,
            ["diameter"] = 0xF8,
        };

        private readonly PdfFile _file;
        private readonly int[] _unicode = new int[256];
        private readonly double[] _widths = new double[256];
        private readonly Dictionary<int, double> _cidWidths = new Dictionary<int, double>();
        private readonly Dictionary<int, string> _toUnicode = new Dictionary<int, string>();
        private PdfTrueType _program;
        private byte[] _cidToGid;
        private double _defaultWidth = 1000.0;
        private bool _symbolic;
        private bool _hasEncoding;
        private bool _unicodeCodes;

        private PdfFont(PdfFile file)
        {
            _file = file;
        }

        /// <summary>Gets the font's name as the PDF gives it, without the six letters and the plus of a subset.</summary>
        public string BaseFont { get; private set; } = string.Empty;

        /// <summary>Gets the name of a font of the machine that stands in for the font where it has no outlines.</summary>
        public string Family { get; private set; } = "Arial";

        /// <summary>Gets whether the codes of a string are two bytes each (a Type 0 font).</summary>
        public bool IsTwoByte { get; private set; }

        /// <summary>Gets whether the font has outlines to draw: an embedded TrueType program.</summary>
        public bool HasOutlines => _program != null;

        /// <summary>Gets the size of an em in the units of <see cref="Outline"/>.</summary>
        public double UnitsPerEm => _program == null ? 1000.0 : _program.UnitsPerEm;

        /// <summary>Gets whether the font is a Type 3 font, whose glyphs are small content streams: not drawn.</summary>
        public bool IsType3 { get; private set; }

        /// <summary>Reads a font dictionary; a font that cannot be read gives a plain one that draws its text in Arial.</summary>
        public static PdfFont Load(PdfFile file, PdfDict dictionary)
        {
            var font = new PdfFont(file);

            try
            {
                font.Read(dictionary);
            }
            catch (PdfException)
            {
                // A font that cannot be read draws as the default.
            }

            return font;
        }

        private void Read(PdfDict dictionary)
        {
            string subtype = _file.Name(_file.Get(dictionary, "Subtype"));
            string baseFont = _file.Name(_file.Get(dictionary, "BaseFont")) ?? string.Empty;
            IsType3 = subtype == "Type3";

            for (int i = 0; i < 256; i++)
            {
                _unicode[i] = i;
            }

            if (subtype == "Type0")
            {
                ReadType0(dictionary, baseFont);
                return;
            }

            SetName(baseFont);
            PdfDict descriptor = _file.Dict(_file.Get(dictionary, "FontDescriptor"));
            int flags = (int)_file.Number(_file.Get(descriptor, "Flags"));
            _symbolic = (flags & 4) != 0 && (flags & 32) == 0;
            double missing = _file.Number(_file.Get(descriptor, "MissingWidth"), 0.0);
            int first = (int)_file.Number(_file.Get(dictionary, "FirstChar"));
            List<object> widths = _file.Array(_file.Get(dictionary, "Widths"));
            double fallback =
                baseFont.IndexOf("Courier", StringComparison.OrdinalIgnoreCase) >= 0 ? 600.0
                : widths == null ? 556.0
                : missing;

            for (int i = 0; i < 256; i++)
            {
                _widths[i] = fallback;
            }

            if (widths != null)
            {
                for (int i = 0; i < widths.Count && first + i < 256; i++)
                {
                    if (first + i >= 0)
                    {
                        _widths[first + i] = _file.Number(widths[i], missing);
                    }
                }
            }

            ReadEncoding(_file.Get(dictionary, "Encoding"));
            ReadToUnicode(dictionary);
            LoadProgram(descriptor);
        }

        private void ReadType0(PdfDict dictionary, string baseFont)
        {
            IsTwoByte = true;

            // The codes of an Identity encoding are glyph numbers, which say no character; those of a Unicode CMap (UniJIS-UCS2-H) are the characters.
            string encoding = _file.Name(_file.Get(dictionary, "Encoding"));
            _unicodeCodes = encoding != null && !encoding.StartsWith("Identity", StringComparison.Ordinal);
            SetName(baseFont);
            List<object> descendants = _file.Array(_file.Get(dictionary, "DescendantFonts"));
            PdfDict cid = descendants != null && descendants.Count > 0 ? _file.Dict(descendants[0]) : null;

            if (cid == null)
            {
                return;
            }

            if (baseFont.Length == 0)
            {
                SetName(_file.Name(_file.Get(cid, "BaseFont")) ?? string.Empty);
            }

            PdfDict descriptor = _file.Dict(_file.Get(cid, "FontDescriptor"));
            _defaultWidth = _file.Number(_file.Get(cid, "DW"), 1000.0);
            List<object> w = _file.Array(_file.Get(cid, "W"));

            for (int i = 0; w != null && i < w.Count; )
            {
                int start = (int)_file.Number(w[i]);

                if (i + 1 < w.Count && _file.Resolve(w[i + 1]) is List<object> run)
                {
                    for (int k = 0; k < run.Count; k++)
                    {
                        _cidWidths[start + k] = _file.Number(run[k]);
                    }

                    i += 2;
                }
                else if (i + 2 < w.Count)
                {
                    int end = Math.Min((int)_file.Number(w[i + 1]), start + 65535);
                    double width = _file.Number(w[i + 2]);

                    for (int c = start; c <= end; c++)
                    {
                        _cidWidths[c] = width;
                    }

                    i += 3;
                }
                else
                {
                    break;
                }
            }

            object map = _file.Resolve(_file.Get(cid, "CIDToGIDMap"));

            if (map is PdfStream stream)
            {
                _cidToGid = _file.Decode(stream);
            }

            ReadToUnicode(dictionary);
            LoadProgram(descriptor);
        }

        private void SetName(string baseFont)
        {
            string name = baseFont;
            int plus = name.IndexOf('+');

            if (plus == 6)
            {
                name = name.Substring(7);
            }

            BaseFont = name;
            string family = name;

            foreach (char stop in new[] { '-', ',' })
            {
                int at = family.IndexOf(stop);

                if (at > 0)
                {
                    family = family.Substring(0, at);
                }
            }

            string lower = family.ToLowerInvariant();
            Family =
                lower.StartsWith("arial", StringComparison.Ordinal)
                || lower.StartsWith("helvetica", StringComparison.Ordinal)
                    ? "Arial"
                : lower.StartsWith("timesnewroman", StringComparison.Ordinal)
                || lower.StartsWith("times", StringComparison.Ordinal)
                    ? "Times New Roman"
                : lower.StartsWith("courier", StringComparison.Ordinal) ? "Courier New"
                : lower.StartsWith("symbol", StringComparison.Ordinal) ? "Symbol"
                : family.Length == 0 ? "Arial"
                : family;
        }

        private void LoadProgram(PdfDict descriptor)
        {
            PdfStream stream = _file.Stream(_file.Get(descriptor, "FontFile2"));

            if (stream == null)
            {
                return;
            }

            try
            {
                _program = PdfTrueType.Read(_file.Decode(stream));
            }
            catch (PdfException)
            {
                _program = null;
            }
        }

        private void ReadEncoding(object spec)
        {
            object resolved = _file.Resolve(spec);
            string name = (resolved as PdfName)?.Value;
            PdfDict dictionary = resolved as PdfDict;

            if (dictionary != null)
            {
                name = _file.Name(_file.Get(dictionary, "BaseEncoding"));
            }

            _hasEncoding = resolved != null;

            if (name == "MacRomanEncoding")
            {
                for (int i = 0x80; i < 256; i++)
                {
                    _unicode[i] = MacRomanHigh[i - 0x80];
                }
            }
            else if (
                name == "WinAnsiEncoding"
                || (name == null && !_symbolic)
                || name == "StandardEncoding"
                || name == "MacExpertEncoding"
            )
            {
                for (int i = 0x80; i < 0xA0; i++)
                {
                    _unicode[i] = WinAnsiHigh[i - 0x80];
                }
            }

            List<object> differences = dictionary == null ? null : _file.Array(_file.Get(dictionary, "Differences"));

            if (differences == null)
            {
                return;
            }

            int code = 0;

            foreach (object item in differences)
            {
                object value = _file.Resolve(item);

                if (value is double number)
                {
                    code = (int)number;
                }
                else if (value is PdfName glyph)
                {
                    if (code >= 0 && code < 256)
                    {
                        _unicode[code] = UnicodeOfGlyph(glyph.Value, _unicode[code]);
                    }

                    code++;
                }
            }
        }

        private static int UnicodeOfGlyph(string name, int fallback)
        {
            if (name.Length == 1)
            {
                return name[0];
            }

            if (GlyphNames.TryGetValue(name, out int known))
            {
                return known;
            }

            if (
                name.Length == 7
                && name.StartsWith("uni", StringComparison.Ordinal)
                && int.TryParse(name.Substring(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex)
            )
            {
                return hex;
            }

            if (
                name.Length >= 2
                && name[0] == 'u'
                && int.TryParse(name.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int plain)
                && name.Length <= 7
            )
            {
                return plain;
            }

            return fallback;
        }

        private void ReadToUnicode(PdfDict dictionary)
        {
            PdfStream stream = _file.Stream(_file.Get(dictionary, "ToUnicode"));

            if (stream == null)
            {
                return;
            }

            byte[] bytes;

            try
            {
                bytes = _file.Decode(stream);
            }
            catch (PdfException)
            {
                return;
            }

            var parser = new PdfParser(bytes, 0, bytes.Length, false);
            var items = new List<object>();
            string mode = null;

            while (!parser.AtEnd)
            {
                object item = parser.Read();

                if (item is PdfKeyword keyword)
                {
                    switch (keyword.Value)
                    {
                        case "beginbfchar":
                            mode = "char";
                            items.Clear();
                            continue;
                        case "beginbfrange":
                            mode = "range";
                            items.Clear();
                            continue;
                        case "endbfchar":
                        case "endbfrange":
                            mode = null;
                            continue;
                    }
                }

                if (mode == null)
                {
                    continue;
                }

                items.Add(item);

                if (mode == "char" && items.Count == 2)
                {
                    if (items[0] is PdfString src && items[1] is PdfString dst)
                    {
                        _toUnicode[Code(src.Bytes)] = Utf16(dst.Bytes);
                    }

                    items.Clear();
                }
                else if (mode == "range" && items.Count == 3)
                {
                    if (items[0] is PdfString lo && items[1] is PdfString hi)
                    {
                        int from = Code(lo.Bytes);
                        int to = Math.Min(Code(hi.Bytes), from + 65535);

                        if (items[2] is PdfString start)
                        {
                            string text = Utf16(start.Bytes);

                            for (int c = from; c <= to && text.Length > 0; c++)
                            {
                                _toUnicode[c] =
                                    text.Substring(0, text.Length - 1) + (char)(text[text.Length - 1] + (c - from));
                            }
                        }
                        else if (items[2] is List<object> list)
                        {
                            for (int c = from; c <= to && c - from < list.Count; c++)
                            {
                                if (list[c - from] is PdfString each)
                                {
                                    _toUnicode[c] = Utf16(each.Bytes);
                                }
                            }
                        }
                    }

                    items.Clear();
                }
            }
        }

        private static int Code(byte[] bytes)
        {
            int code = 0;

            foreach (byte b in bytes)
            {
                code = (code << 8) | b;
            }

            return code;
        }

        private static string Utf16(byte[] bytes) =>
            bytes.Length < 2
                ? (bytes.Length == 1 ? ((char)bytes[0]).ToString() : string.Empty)
                : Encoding.BigEndianUnicode.GetString(bytes);

        /// <summary>Splits the bytes of a string shown into the codes of the font: one byte each, or two for a Type 0 font.</summary>
        public IEnumerable<int> Codes(byte[] bytes)
        {
            if (!IsTwoByte)
            {
                foreach (byte b in bytes)
                {
                    yield return b;
                }

                yield break;
            }

            for (int i = 0; i + 1 < bytes.Length; i += 2)
            {
                yield return (bytes[i] << 8) | bytes[i + 1];
            }
        }

        /// <summary>Gets the advance of a code in thousandths of an em.</summary>
        public double Width(int code)
        {
            if (IsTwoByte)
            {
                return _cidWidths.TryGetValue(code, out double w) ? w : _defaultWidth;
            }

            double width = code >= 0 && code < 256 ? _widths[code] : 0.0;

            if (width <= 0.0 && _program != null)
            {
                int glyph = GlyphOf(code);
                double advance = _program.Advance(glyph);

                if (advance > 0.0)
                {
                    return advance * 1000.0 / _program.UnitsPerEm;
                }
            }

            return width;
        }

        /// <summary>Gets the text a code stands for: its ToUnicode entry, else its character by the encoding.</summary>
        public string TextOf(int code)
        {
            if (_toUnicode.TryGetValue(code, out string text))
            {
                return text;
            }

            if (IsTwoByte)
            {
                return _unicodeCodes && code >= 32 && code < 0xD800 ? ((char)code).ToString() : string.Empty;
            }

            int u = code >= 0 && code < 256 ? _unicode[code] : 0;
            return u > 0 && u < 0x10000 ? ((char)u).ToString() : string.Empty;
        }

        private int GlyphOf(int code)
        {
            if (_program == null)
            {
                return 0;
            }

            if (IsTwoByte)
            {
                if (_cidToGid != null)
                {
                    return 2 * code + 1 < _cidToGid.Length ? (_cidToGid[2 * code] << 8) | _cidToGid[2 * code + 1] : 0;
                }

                return code;
            }

            int glyph = 0;

            if (_hasEncoding && !_symbolic)
            {
                glyph = _program.GlyphOfUnicode(code < 256 ? _unicode[code] : code);
            }

            if (glyph == 0)
            {
                glyph = _program.GlyphOfCode(code);
            }

            if (glyph == 0 && _program.GlyphOfUnicode(code) is int byUnicode && byUnicode > 0)
            {
                glyph = byUnicode;
            }

            return glyph;
        }

        /// <summary>Gives the outline of the glyph of a code in the font's units, each contour as x and y in turn, y up; null where the font has none.</summary>
        public IReadOnlyList<double[]> Outline(int code) => _program?.Outline(GlyphOf(code));
    }
}
