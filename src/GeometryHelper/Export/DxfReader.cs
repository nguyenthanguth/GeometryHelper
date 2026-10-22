using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeometryHelper.Export
{
    /// <summary>
    /// Reads a DXF file into the library's shapes: what <see cref="DxfWriter"/> writes, back to the bit where the file holds
    /// the number, and what AutoCAD and the other CAD programs write of the same kinds, from R12 to R2018.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The entities of the drawing are read in the order of the file, each with its layer, its colour and its handle: a
    /// POINT, LINE, RAY and XLINE as the point, segment, ray and line without ends they are; a LWPOLYLINE and a POLYLINE of
    /// the plane as a chain or a polygon, with arcs where an edge has a bulge; a 3D POLYLINE as a chain or a polygon in
    /// space; a polyface or polygon mesh as a body of its faces; a CIRCLE, ARC and whole ELLIPSE as themselves; a part of an
    /// ELLIPSE and a SPLINE as a spline, exactly; a 3DFACE as a triangle or a face, and a SOLID or TRACE as a polygon.
    /// Entities of space are read as shapes of space unless <see cref="DxfReadOptions.PlanAs2D"/> asks for those lying on
    /// the floor as shapes of the plane. An INSERT is read as the entities of its block, each placed where the INSERT puts
    /// it, with the look it takes from the INSERT and the names of the blocks it was placed through
    /// (<see cref="DxfReadOptions.ExpandBlocks"/>); the blocks themselves are in <see cref="DxfDocument.Blocks"/>. TEXT,
    /// MTEXT, DIMENSION, HATCH and the other types are left out and counted in <see cref="DxfDocument.Skipped"/>, and so is
    /// an entity with no shape to it, a LINE of no length among them; the entities of paper space are not read, and a
    /// warning counts them.
    /// </para>
    /// <para>
    /// Every number is read as the file prints it, the double nearest it on either runtime, so a file the library wrote
    /// gives back every coordinate, radius, bulge, knot and weight it holds to the bit: a polyline of the plane read with
    /// <see cref="DxfReadOptions.PlanAs2D"/> is the polyline written. What the file holds in another form comes back to the
    /// last place or two: the angles of an ARC are degrees in the file, an ELLIPSE keeps the end of its major axis and the
    /// ratio of its radii, and a circle, arc or polyline in a tilted plane keeps its coordinates in the plane's own system.
    /// </para>
    /// <para>
    /// A file that is not a DXF, or is cut short, is refused with a <see cref="FormatException"/> that names the line; a
    /// binary DXF is refused the same way. One entity that cannot be read never stops the others.
    /// </para>
    /// <code>
    /// DxfDocument drawing = DxfReader.Read("plan.dxf", new DxfReadOptions { PlanAs2D = true });
    /// foreach (GeoPolygonArc2 outline in drawing.OfType&lt;GeoPolygonArc2&gt;()) { ... }
    /// </code>
    /// </remarks>
    public static partial class DxfReader
    {
        private static readonly byte[] BinaryMark = Encoding.ASCII.GetBytes("AutoCAD Binary DXF");

        /// <summary>
        /// Reads a DXF file from a path.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="options">How to read it; null reads it with the defaults of <see cref="DxfReadOptions"/>.</param>
        /// <returns>The entities of the file and what it says of itself.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a DXF, is binary, or is cut short; the message names the line.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <remarks>
        /// The text of a file from R2007 on is UTF-8; that of an older one is in the code page its header names
        /// (<c>$DWGCODEPAGE</c>, ANSI_1252 when it names none), where the runtime has it. Letters beyond ASCII written as
        /// escapes, <c>\U+00E9</c> for an e acute as <see cref="DxfWriter"/> writes them, are read as the letters.
        /// </remarks>
        public static DxfDocument Read(string path, DxfReadOptions options = null)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return FromBytes(File.ReadAllBytes(path), options, "'" + Path.GetFileName(path) + "'");
        }

        /// <summary>
        /// Reads a DXF file from a stream, from where the stream stands to its end.
        /// </summary>
        /// <param name="stream">The stream; it is read to its end and left open.</param>
        /// <param name="options">How to read it; null reads it with the defaults of <see cref="DxfReadOptions"/>.</param>
        /// <returns>The entities of the file and what it says of itself.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a DXF, is binary, or is cut short; the message names the line.</exception>
        /// <remarks>The text is decoded as <see cref="Read(string, DxfReadOptions)"/> decodes a file's.</remarks>
        public static DxfDocument Read(Stream stream, DxfReadOptions options = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                return FromBytes(copy.ToArray(), options, "a stream");
            }
        }

        /// <summary>
        /// Reads a DXF file from its text.
        /// </summary>
        /// <param name="text">The text of the file, as <see cref="DxfWriter.ToString"/> gives it; lines may end in a line feed or in a carriage return and a line feed.</param>
        /// <param name="options">How to read it; null reads it with the defaults of <see cref="DxfReadOptions"/>.</param>
        /// <returns>The entities of the file and what it says of itself.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="FormatException">Thrown when the text is not a DXF, is binary, or is cut short; the message names the line.</exception>
        public static DxfDocument Parse(string text, DxfReadOptions options = null)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new Reading(options ?? new DxfReadOptions()).Run(text.Length > 0 && text[0] == '﻿' ? text.Substring(1) : text, "a text", null);
        }

        #region Decoding

        private static DxfDocument FromBytes(byte[] bytes, DxfReadOptions options, string source)
        {
            if (StartsWith(bytes, 0, BinaryMark))
            {
                throw Binary();
            }

            bool marked = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            int start = marked ? 3 : 0;
            bool ascii = true;

            for (int i = start; i < bytes.Length && ascii; i++)
            {
                ascii = bytes[i] < 0x80;
            }

            string latin = Latin1.GetString(bytes, start, bytes.Length - start);
            var warnings = new List<string>();
            string text = latin;

            if (!ascii)
            {
                text = Decode(bytes, start, marked, latin, warnings);
            }

            return new Reading(options ?? new DxfReadOptions()).Run(text, source, warnings);
        }

        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        /// <summary>
        /// The text of a file with bytes beyond ASCII in it, the same on either runtime: UTF-8 where it has the mark or is of
        /// R2007 or later; else UTF-8 where its bytes are UTF-8, which a text in a code page with a letter beyond ASCII in it
        /// almost never is, said in a warning; else the code page of its header, Windows-1252 by a table of its own, since
        /// .NET 10 does not have it, and any other where the runtime has it, Latin-1 where it does not, said in a warning.
        /// </summary>
        private static string Decode(byte[] bytes, int start, bool marked, string latin, List<string> warnings)
        {
            Header(latin, out string version, out string codePage);

            if (marked || string.CompareOrdinal(version ?? string.Empty, "AC1021") >= 0)
            {
                return Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
            }

            try
            {
                string utf8 = new UTF8Encoding(false, true).GetString(bytes, start, bytes.Length - start);
                warnings.Add("The file is of a version before R2007, whose text is in a code page, but its text is UTF-8 and was read as such.");
                return utf8;
            }
            catch (DecoderFallbackException)
            {
                // Not UTF-8: the code page it names.
            }

            int number = CodePageNumber(codePage);

            if (number == 1252)
            {
                return Windows1252(latin);
            }

            try
            {
                return Encoding.GetEncoding(number).GetString(bytes, start, bytes.Length - start);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
                warnings.Add("The file is in the code page " + (codePage ?? "ANSI_1252") + " (" + Number(number) + "), which this runtime does not have, so its text was read as Latin-1 and a letter beyond ASCII in a name may be another; save the drawing as a DXF of R2007 or later, whose text is UTF-8, to read it right.");
                return latin;
            }
        }

        // The letters Windows-1252 has at 0x80 to 0x9F, where Latin-1 has control characters; the five it leaves out are
        // those characters, as .NET Framework reads them.
        private static readonly char[] High1252 =
        {
            '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021', '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
            '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014', '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178',
        };

        /// <summary>The text of Windows-1252 bytes, from their Latin-1 reading, in which every other byte is already its letter.</summary>
        private static string Windows1252(string latin)
        {
            var text = new StringBuilder(latin.Length);

            foreach (char c in latin)
            {
                text.Append(c >= '\u0080' && c <= '\u009F' ? High1252[c - 0x80] : c);
            }

            return text.ToString();
        }

        /// <summary>The number of a code page as <c>$DWGCODEPAGE</c> names it: ANSI_1252 is 1252; 1252 where it names none.</summary>
        private static int CodePageNumber(string codePage)
        {
            if (codePage == null)
            {
                return 1252;
            }

            int end = codePage.Length;
            int at = end;

            while (at > 0 && char.IsDigit(codePage[at - 1]))
            {
                at--;
            }

            return at < end && int.TryParse(codePage.Substring(at), NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number > 0 ? number : 1252;
        }

        /// <summary>
        /// The version and the code page a header names, read from the text as Latin-1 gives it, which keeps every byte of
        /// ASCII where it is; null for one it does not name, and both null where the text is not a DXF.
        /// </summary>
        private static void Header(string text, out string version, out string codePage)
        {
            version = null;
            codePage = null;

            try
            {
                var pairs = new Pairs(text);
                string variable = null;
                bool inHeader = false;

                while (pairs.Next(out int code, out string value, out _))
                {
                    if (code == 0)
                    {
                        if (inHeader)
                        {
                            return;
                        }

                        if (value.Trim() == "SECTION" && pairs.Next(out int nameCode, out string name, out _) && nameCode == 2)
                        {
                            inHeader = name.Trim() == "HEADER";

                            if (!inHeader)
                            {
                                return;
                            }
                        }

                        continue;
                    }

                    if (!inHeader)
                    {
                        continue;
                    }

                    if (code == 9)
                    {
                        variable = value.Trim();
                    }
                    else if (code == 1 && variable == "$ACADVER")
                    {
                        version = value.Trim();
                    }
                    else if (code == 3 && variable == "$DWGCODEPAGE")
                    {
                        codePage = value.Trim();
                    }
                }
            }
            catch (FormatException)
            {
                // Not a DXF: the reading proper says so, with the line.
            }
        }

        private static bool StartsWith(byte[] bytes, int start, byte[] mark)
        {
            if (bytes.Length - start < mark.Length)
            {
                return false;
            }

            for (int i = 0; i < mark.Length; i++)
            {
                if (bytes[start + i] != mark[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static FormatException Binary()
            => new FormatException("This is a binary DXF, which is not read: save the drawing as an ASCII DXF and read that.");

        #endregion

        #region The file

        /// <summary>
        /// The pairs of lines a DXF is made of, a group code and its value, read one after another with the number of the
        /// line the code is on. A code may be padded with spaces, as the writers pad it to three places; a line may end in
        /// a carriage return and a line feed.
        /// </summary>
        private sealed class Pairs
        {
            private readonly string _text;
            private int _at;
            private int _line;

            internal Pairs(string text)
            {
                _text = text;
            }

            /// <summary>The number of the last line read.</summary>
            internal int Line => _line;

            /// <summary>
            /// Reads the next pair; false at the end of the text.
            /// </summary>
            /// <exception cref="FormatException">Thrown when a code is not a whole number, or the text ends after a code with no value.</exception>
            internal bool Next(out int code, out string value, out int line)
            {
                code = 0;
                value = null;
                line = _line + 1;

                if (!NextLine(out int start, out int end))
                {
                    return false;
                }

                if (!TryCode(_text, start, end, out code))
                {
                    string said = _text.Substring(start, Math.Min(end - start, 24)) + (end - start > 24 ? "..." : string.Empty);
                    throw new FormatException("This is not a DXF that can be read: line " + Number(line) + " should be a group code, a whole number, and is '" + said + "'.");
                }

                if (!NextLine(out start, out end))
                {
                    throw new FormatException("The DXF is cut short: the group code " + Number(code) + " on line " + Number(line) + ", the last, has no value after it.");
                }

                value = _text.Substring(start, end - start);
                return true;
            }

            private bool NextLine(out int start, out int end)
            {
                start = _at;
                end = _at;

                if (_at >= _text.Length)
                {
                    return false;
                }

                int feed = _text.IndexOf('\n', _at);
                end = feed < 0 ? _text.Length : feed;
                _at = feed < 0 ? _text.Length : feed + 1;
                _line++;

                if (end > start && _text[end - 1] == '\r')
                {
                    end--;
                }

                return true;
            }

            private static bool TryCode(string text, int start, int end, out int code)
            {
                code = 0;

                while (start < end && (text[start] == ' ' || text[start] == '\t'))
                {
                    start++;
                }

                while (end > start && (text[end - 1] == ' ' || text[end - 1] == '\t'))
                {
                    end--;
                }

                bool negative = start < end && text[start] == '-';

                if (negative)
                {
                    start++;
                }

                if (start == end || end - start > 9)
                {
                    return false;
                }

                for (int i = start; i < end; i++)
                {
                    char c = text[i];

                    if (c < '0' || c > '9')
                    {
                        return false;
                    }

                    code = code * 10 + (c - '0');
                }

                code = negative ? -code : code;
                return true;
            }
        }

        /// <summary>
        /// One record of the file: an entity, a table entry, a block's own header; the groups after its 0, with the reals
        /// among them read once.
        /// </summary>
        private sealed class Record
        {
            private int[] _codes = new int[16];
            private string[] _values = new string[16];
            private double[] _numbers;
            private string _unreadable;
            private IReadOnlyList<DxfGroup> _groups;

            internal Record(string type, int line)
            {
                Type = type;
                Line = line;
            }

            internal string Type { get; }

            internal int Line { get; }

            internal int Count { get; private set; }

            internal void Add(int code, string value)
            {
                if (Count == _codes.Length)
                {
                    Array.Resize(ref _codes, Count * 2);
                    Array.Resize(ref _values, Count * 2);
                }

                _codes[Count] = code;
                _values[Count] = value;
                Count++;
            }

            /// <summary>The records that belong to this one and follow it: the VERTEX records and the SEQEND of a POLYLINE.</summary>
            internal List<Record> Following { get; set; }

            /// <summary>
            /// The groups of the record as the file has them, those of the records following it after each one's group 0;
            /// made once, and shared by every entity made of the record.
            /// </summary>
            internal IReadOnlyList<DxfGroup> Groups()
            {
                if (_groups == null)
                {
                    var groups = new List<DxfGroup>(Count);

                    for (int i = 0; i < Count; i++)
                    {
                        groups.Add(new DxfGroup(_codes[i], _values[i]));
                    }

                    if (Following != null)
                    {
                        foreach (Record next in Following)
                        {
                            groups.Add(new DxfGroup(0, next.Type));

                            for (int i = 0; i < next.Count; i++)
                            {
                                groups.Add(new DxfGroup(next._codes[i], next._values[i]));
                            }
                        }
                    }

                    _groups = groups.AsReadOnly();
                }

                return _groups;
            }

            internal int CodeAt(int index) => _codes[index];

            /// <summary>The real at an index, read by <see cref="Unreadable"/>.</summary>
            internal double NumberAt(int index) => _numbers[index];

            internal int IndexOf(int code)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (_codes[i] == code)
                    {
                        return i;
                    }
                }

                return -1;
            }

            internal bool Has(int code) => IndexOf(code) >= 0;

            internal string Text(int code)
            {
                int at = IndexOf(code);
                return at < 0 ? null : _values[at];
            }

            /// <summary>The real of the first group of a code; false where there is none.</summary>
            internal bool TryReal(int code, out double value)
            {
                int at = IndexOf(code);
                value = at < 0 ? 0.0 : _numbers[at];
                return at >= 0;
            }

            internal double Real(int code, double fallback) => TryReal(code, out double value) ? value : fallback;

            internal bool TryInt(int code, out int value)
            {
                value = 0;
                string text = Text(code);
                return text != null && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            internal int Int(int code, int fallback)
            {
                string text = Text(code);
                return text != null && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
            }

            /// <summary>
            /// Reads every real of the record, and says which group, if any, holds a number that is not one: NaN, an
            /// infinity, a word. The groups of 10 to 59, 110 to 149 and 210 to 239 are reals, and those of 60 to 99, 170 to
            /// 179, 270 to 289, 370 to 379 and 420 to 429 whole numbers. Read once: a record of a block is read again for
            /// every INSERT that places it.
            /// </summary>
            internal string Unreadable()
            {
                if (_numbers == null)
                {
                    _unreadable = ReadNumbers();
                }

                return _unreadable;
            }

            private string ReadNumbers()
            {
                _numbers = new double[Count];

                for (int i = 0; i < Count; i++)
                {
                    int c = _codes[i];
                    bool real = (c >= 10 && c <= 59) || (c >= 110 && c <= 149) || (c >= 210 && c <= 239);
                    bool whole = (c >= 60 && c <= 99) || (c >= 170 && c <= 179) || (c >= 270 && c <= 289) || (c >= 370 && c <= 379) || (c >= 420 && c <= 429);

                    if (real)
                    {
                        if (!Numbers.TryRead(_values[i], out _numbers[i]))
                        {
                            // The thickness is of the look: one that cannot be read is left out, not the entity.
                            if (c == 39)
                            {
                                _numbers[i] = double.NaN;
                                continue;
                            }

                            return Said(_values[i]) + " in group " + Number(c);
                        }
                    }
                    else if (whole && c != 62 && c != 370 && c != 420 && !int.TryParse(_values[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        return Said(_values[i]) + " in group " + Number(c);
                    }
                }

                return null;
            }

            private static string Said(string value)
            {
                string trimmed = value.Trim();
                return "'" + (trimmed.Length <= 24 ? trimmed : trimmed.Substring(0, 24) + "...") + "'";
            }
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        #endregion

        /// <summary>
        /// One reading of a file: the sections walked, the layers and the header taken, the entities made shapes.
        /// </summary>
        private sealed partial class Reading
        {
            private readonly bool _planAs2D;
            private readonly bool _toMillimetres;
            private readonly Tolerance _tolerance;
            private readonly bool _keepGroups;
            private readonly bool _expandBlocks;
            private readonly Dictionary<string, string> _interned = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly List<DxfEntity> _entities = new List<DxfEntity>();
            private readonly List<string> _layers = new List<string>();
            private readonly Dictionary<string, string> _layerNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly SortedDictionary<string, int> _skipped = new SortedDictionary<string, int>(StringComparer.Ordinal);
            private readonly SortedDictionary<string, SortedDictionary<string, int>> _reasons = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);
            private readonly List<string> _warnings = new List<string>();
            private string _version;
            private int _unitCode = -1;
            private double _scale = 1.0;
            private int _paperEntities;

            internal Reading(DxfReadOptions options)
            {
                _planAs2D = options.PlanAs2D;
                _toMillimetres = options.ToMillimetres;
                _tolerance = options.Tolerance;
                _keepGroups = options.KeepGroups;
                _expandBlocks = options.ExpandBlocks;
                _budget = new Budget(options.MaxPlacedEntities);
                _unsized = options.UnsizedBlocks;
                _into = _entities;
            }

            internal DxfDocument Run(string text, string source, List<string> decoding)
            {
                if (text.StartsWith("AutoCAD Binary DXF", StringComparison.Ordinal))
                {
                    throw Binary();
                }

                if (decoding != null)
                {
                    _warnings.AddRange(decoding);
                }

                Walk(text);
                return Finish(source);
            }

            /// <summary>
            /// Walks the sections of the file. A record begins at a group 0 and belongs to the section it is in; the header's
            /// variables are read as they come, the layer table and the entities as each record ends.
            /// </summary>
            private void Walk(string text)
            {
                var pairs = new Pairs(text);
                string section = null;
                int sectionLine = 0;
                Record current = null;
                bool entities = false;
                bool ended = false;
                string variable = null;

                while (pairs.Next(out int code, out string value, out int line))
                {
                    if (code == 999)
                    {
                        continue;
                    }

                    if (code != 0)
                    {
                        if (section == "HEADER")
                        {
                            HeaderGroup(ref variable, code, value);
                        }
                        else
                        {
                            current?.Add(code, _keepGroups && (code == 100 || code == 330 || code == 8 || code == 6) ? Interned(value) : value);
                        }

                        continue;
                    }

                    Done(current, section);
                    current = null;
                    string word = value.Trim();

                    if (word == "EOF")
                    {
                        if (section != null)
                        {
                            throw new FormatException("The DXF is cut short: the section " + section + " opened on line " + Number(sectionLine) + " has no ENDSEC before the EOF on line " + Number(line) + ".");
                        }

                        ended = true;
                        break;
                    }

                    if (word == "SECTION")
                    {
                        if (section != null)
                        {
                            throw new FormatException("This is not a DXF that can be read: a SECTION opens on line " + Number(line) + " inside the section " + section + " opened on line " + Number(sectionLine) + ", which has no ENDSEC.");
                        }

                        if (!pairs.Next(out int nameCode, out string name, out int nameLine) || nameCode != 2)
                        {
                            throw new FormatException("This is not a DXF that can be read: the SECTION on line " + Number(line) + " is not named by a group 2 on line " + Number(nameLine) + ".");
                        }

                        section = name.Trim();
                        sectionLine = line;
                        entities |= section == "ENTITIES";
                        continue;
                    }

                    if (word == "ENDSEC")
                    {
                        if (section == null)
                        {
                            throw new FormatException("This is not a DXF that can be read: the ENDSEC on line " + Number(line) + " closes no section.");
                        }

                        if (section == "ENTITIES")
                        {
                            EndOfEntities();
                        }
                        else if (section == "BLOCKS")
                        {
                            EndOfBlock();
                        }

                        section = null;
                        continue;
                    }

                    if (section == null)
                    {
                        throw new FormatException("This is not a DXF that can be read: the " + word + " on line " + Number(line) + " stands in no section.");
                    }

                    current = new Record(word, line);
                }

                if (!ended)
                {
                    throw new FormatException("The DXF is cut short: it ends on line " + Number(pairs.Line) + " without the EOF that closes a DXF" + (section != null ? ", inside the section " + section + " opened on line " + Number(sectionLine) : string.Empty) + ".");
                }

                if (!entities)
                {
                    throw new FormatException("This is not a DXF that can be read: it has no ENTITIES section.");
                }
            }

            private void HeaderGroup(ref string variable, int code, string value)
            {
                if (code == 9)
                {
                    variable = value.Trim();
                    return;
                }

                if (variable == "$ACADVER" && code == 1)
                {
                    _version = value.Trim();
                }
                else if (variable == "$INSUNITS" && code == 70 && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int units))
                {
                    _unitCode = units;
                }
            }

            /// <summary>
            /// Takes a record that has ended: a layer of the layer table, an entity of a block (counted), an entity of the
            /// drawing (read).
            /// </summary>
            private void Done(Record record, string section)
            {
                if (record == null)
                {
                    return;
                }

                if (section == "TABLES")
                {
                    if (record.Type == "LAYER")
                    {
                        string name = Decode((record.Text(2) ?? string.Empty).Trim());

                        if (name.Length > 0)
                        {
                            LayerName(name);
                        }
                    }
                }
                else if (section == "BLOCKS")
                {
                    BlockRecord(record);
                }
                else if (section == "ENTITIES")
                {
                    if (_scale == 1.0 && _toMillimetres && _unitCode >= 0)
                    {
                        _scale = Factor(_unitCode);
                    }

                    Entity(record);
                }
            }

            /// <summary>
            /// The name a layer is known by: the first spelling of a name met, those differing only in case being one.
            /// </summary>
            private string LayerName(string name)
            {
                if (_layerNames.TryGetValue(name, out string known))
                {
                    return known;
                }

                _layerNames.Add(name, name);
                _layers.Add(name);
                return name;
            }

            private DxfDocument Finish(string source)
            {
                if (_scale == 1.0 && _toMillimetres && _unitCode >= 0)
                {
                    _scale = Factor(_unitCode);
                }

                int placed = ExpandPending();
                DxfBlock[] blocks = ReadBlocks();
                DxfUnits units = Units(out bool listed);

                if (_version != null && (string.CompareOrdinal(_version, "AC1009") < 0 || string.CompareOrdinal(_version, "AC1032") > 0))
                {
                    _warnings.Insert(0, "The file is of version " + _version + ", outside R12 (AC1009) to R2018 (AC1032); it was read as those are.");
                }

                if (_toMillimetres && (units == DxfUnits.Unitless))
                {
                    _warnings.Add(listed
                        ? (_unitCode < 0 ? "The file names no unit, so its numbers were kept as they are and not scaled to millimetres." : "The file says it has no unit, so its numbers were kept as they are and not scaled to millimetres.")
                        : "The file gives the unit " + Number(_unitCode) + ", which the DXF reference does not list, so its numbers were kept as they are and not scaled to millimetres.");
                }

                Warn(_paperEntities, " entity of paper space was not read: the reader reads model space.", " entities of paper space were not read: the reader reads model space.");
                BlockWarnings();
                EntityWarnings();

                int skipped = 0;
                var detail = new StringBuilder();

                foreach (KeyValuePair<string, int> pair in _skipped)
                {
                    skipped += pair.Value;
                    detail.Append(detail.Length == 0 ? " (" : ", ").Append(pair.Key).Append(' ').Append(Number(pair.Value));
                }

                foreach (KeyValuePair<string, SortedDictionary<string, int>> type in _reasons)
                {
                    var why = new StringBuilder();

                    foreach (KeyValuePair<string, int> reason in type.Value)
                    {
                        why.Append(why.Length == 0 ? string.Empty : ", ").Append(Number(reason.Value)).Append(' ').Append(reason.Key);
                    }

                    GeometryHelperLog.Debug("DxfReader: " + Number(_skipped[type.Key]) + " " + type.Key + " not read: " + why + ".");
                }

                GeometryHelperLog.Info("DxfReader: " + Number(_into.Count) + " entities read from " + source + (placed > 0 ? ", " + Number(placed) + " of them placed by INSERTs" : string.Empty) + ", " + Number(skipped) + " skipped" + (detail.Length > 0 ? detail.Append(')').ToString() : string.Empty) + ", " + Number(_warnings.Count) + " warnings.");

                return new DxfDocument(_into.ToArray(), _layers.ToArray(), blocks, units, _version, new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(_skipped, StringComparer.Ordinal)), _warnings.ToArray());
            }

            /// <summary>
            /// A value kept once for every group that has it, for the groups whose values repeat from entity to entity and
            /// every entity has: the subclass markers (100), the owner (330), the layer (8) and the linetype (6). At most
            /// 65 536 values are kept so.
            /// </summary>
            private string Interned(string value)
            {
                if (_interned.TryGetValue(value, out string kept))
                {
                    return kept;
                }

                if (_interned.Count < 65536)
                {
                    _interned.Add(value, value);
                }

                return value;
            }

            private void Warn(int count, string one, string many)
            {
                if (count > 0)
                {
                    _warnings.Add(Number(count) + (count == 1 ? one : many));
                }
            }

            private DxfUnits Units(out bool listed)
            {
                listed = _unitCode <= 0 || _unitCode <= (int)DxfUnits.UsSurveyMiles;
                return _unitCode > 0 && _unitCode <= (int)DxfUnits.UsSurveyMiles ? (DxfUnits)_unitCode : DxfUnits.Unitless;
            }

            /// <summary>The millimetres in a unit of <c>$INSUNITS</c>; 1 for none, or one not listed.</summary>
            private static double Factor(int code)
            {
                const double SurveyFoot = 1200.0 / 3937.0 * 1000.0;

                switch (code)
                {
                    case 1: return 25.4;
                    case 2: return 304.8;
                    case 3: return 1609344.0;
                    case 5: return 10.0;
                    case 6: return 1000.0;
                    case 7: return 1E6;
                    case 8: return 2.54E-5;
                    case 9: return 0.0254;
                    case 10: return 914.4;
                    case 11: return 1E-7;
                    case 12: return 1E-6;
                    case 13: return 1E-3;
                    case 14: return 100.0;
                    case 15: return 1E4;
                    case 16: return 1E5;
                    case 17: return 1E12;
                    case 18: return 1.495978707E14;
                    case 19: return 9.4607304725808E18;
                    case 20: return 3.0856775814913673E19;
                    case 21: return SurveyFoot;
                    case 22: return SurveyFoot / 12.0;
                    case 23: return SurveyFoot * 3.0;
                    case 24: return SurveyFoot * 5280.0;
                    default: return 1.0;
                }
            }

            /// <summary>Counts an entity that is not read, under its type and the reason, for <see cref="DxfDocument.Skipped"/> and the log.</summary>
            private void Skip(string type, string reason)
            {
                _skipped.TryGetValue(type, out int count);
                _skipped[type] = count + 1;

                if (!_reasons.TryGetValue(type, out SortedDictionary<string, int> reasons))
                {
                    reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
                    _reasons.Add(type, reasons);
                }

                reasons.TryGetValue(reason, out int times);
                reasons[reason] = times + 1;
            }
        }

        /// <summary>
        /// A name with the escapes of R2000 read as the letters they stand for: <c>\U+00E9</c> is an e acute.
        /// </summary>
        private static string Decode(string name)
        {
            int at = name.IndexOf("\\U+", StringComparison.Ordinal);

            if (at < 0)
            {
                return name;
            }

            var plain = new StringBuilder(name.Length);
            int from = 0;

            while (at >= 0)
            {
                if (at + 7 <= name.Length && int.TryParse(name.Substring(at + 3, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int letter))
                {
                    plain.Append(name, from, at - from).Append((char)letter);
                    from = at + 7;
                }
                else
                {
                    plain.Append(name, from, at + 3 - from);
                    from = at + 3;
                }

                at = name.IndexOf("\\U+", from, StringComparison.Ordinal);
            }

            return plain.Append(name, from, name.Length - from).ToString();
        }
    }
}
