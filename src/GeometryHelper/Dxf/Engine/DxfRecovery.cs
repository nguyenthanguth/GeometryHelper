using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The tolerant reading of a damaged DXF, as <see cref="DxfDatabase.Recover(string, out DxfRepairReport)"/> reads one: the
    /// bytes taken as pairs of lines however they are broken, the sections, blocks and polylines closed, the numbers, handles and
    /// owners set right, and the file written again where anything was, so that <see cref="DxfFile"/> reads it as it reads a
    /// sound one. A sound file is given back as its own bytes. The ideas are ezdxf's <c>recover</c> module (MIT): pairs read
    /// on past a line that is not a group code, records outside every section gathered, and a number read from what it begins
    /// with; the code is the library's own.
    /// </summary>
    internal sealed class DxfRecovery
    {
        // AutoCAD's own entities, by the name of their group 0; any other is known only by the CLASSES section.
        private static readonly HashSet<string> Entities = new HashSet<string>(StringComparer.Ordinal)
        {
            "3DFACE", "3DSOLID", "ACAD_PROXY_ENTITY", "ACAD_TABLE", "ARC", "ARC_DIMENSION", "ATTDEF", "ATTRIB", "BODY", "CIRCLE",
            "DGNUNDERLAY", "DIMENSION", "DWFUNDERLAY", "ELLIPSE", "EXTRUDEDSURFACE", "HATCH", "HELIX", "IMAGE", "INSERT",
            "LARGE_RADIAL_DIMENSION", "LEADER", "LIGHT", "LINE", "LOFTEDSURFACE", "LWPOLYLINE", "MESH", "MLINE", "MPOLYGON",
            "MTEXT", "MULTILEADER", "NURBSURFACE", "OLE2FRAME", "OLEFRAME", "PDFUNDERLAY", "PLANESURFACE", "POINT", "POINTCLOUD",
            "POINTCLOUDEX", "POLYLINE", "RAY", "REGION", "REVOLVEDSURFACE", "SEQEND", "SHAPE", "SOLID", "SPLINE", "SUN",
            "SURFACE", "SWEPTSURFACE", "TEXT", "TOLERANCE", "TRACE", "VERTEX", "VIEWPORT", "WIPEOUT", "XLINE",
        };

        private static readonly string[] Order = { "HEADER", "CLASSES", "TABLES", "BLOCKS", "ENTITIES", "OBJECTS" };

        /// <summary>Whether a type is one of AutoCAD's own entities.</summary>
        internal static bool IsEntity(string type) => Entities.Contains(type);

        private readonly byte[] _bytes;
        private readonly int _length;
        private readonly int _start;
        private readonly bool _fix;
        private readonly bool _values;
        private readonly List<DxfRepair> _repairs;
        private readonly List<Tag> _tags = new List<Tag>();
        private readonly Dictionary<int, byte[]> _own = new Dictionary<int, byte[]>();
        private readonly List<Section> _sections = new List<Section>();
        private readonly Dictionary<Record, ulong> _renamed = new Dictionary<Record, ulong>();
        private bool _changed;
        private bool _crlf;
        private bool _padded;
        private bool _markers;
        private bool _handles;
        private ulong _next;
        private ulong _most;

        private DxfRecovery(byte[] bytes, int length, bool fix, bool values, List<DxfRepair> repairs)
        {
            _bytes = bytes;
            _length = length;
            _start = length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            _fix = fix;
            _values = values;
            _repairs = repairs;
        }

        /// <summary>The handles of the records as the file stands after the repairs, to tell whether a pointer names a record.</summary>
        internal HashSet<ulong> Handles { get; } = new HashSet<ulong>();

        /// <summary>The bytes to be read: the file's own where nothing was repaired.</summary>
        internal byte[] Bytes { get; private set; }

        /// <summary>How many of <see cref="Bytes"/> are the file.</summary>
        internal int Length { get; private set; }

        /// <summary>
        /// Reads a file tolerantly and adds what it found to the repairs, each fixed or only found; the bytes written again where
        /// anything was fixed. The numbers are looked at only where asked, as <see cref="DxfDatabase.Audit"/> looks at those of
        /// the entities itself.
        /// </summary>
        internal static DxfRecovery Run(byte[] bytes, int length, bool fix, bool values, List<DxfRepair> repairs)
        {
            var recovery = new DxfRecovery(bytes, length, fix, values, repairs);
            recovery.ReadPairs();
            recovery.ReadEncoding();
            recovery.ReadSections();

            if (values)
            {
                recovery.CheckValues();
            }

            recovery.CheckHandles();
            recovery.CloseBlocks();
            recovery.CheckOwners();
            recovery.CheckSeed();

            if (fix && recovery._changed)
            {
                recovery.Write();
            }
            else
            {
                recovery.Bytes = bytes;
                recovery.Length = length;
            }

            return recovery;
        }

        #region What a record is made of

        private readonly struct Tag
        {
            internal Tag(int code, int start, int end, int line)
            {
                Code = code;
                Start = start;
                End = end;
                Line = line;
            }

            internal int Code { get; }

            internal int Start { get; }

            internal int End { get; }

            internal int Line { get; }
        }

        private sealed class Record
        {
            internal Record(string type, int line)
            {
                Type = type;
                Line = line;
            }

            internal string Type { get; }

            internal int Line { get; }

            internal List<int> Tags { get; } = new List<int>();

            internal ulong Handle { get; set; }

            internal bool Made { get; set; }
        }

        private sealed class Section
        {
            internal Section(string name, Record head)
            {
                Name = name;
                Head = head;
            }

            internal string Name { get; }

            internal Record Head { get; }

            internal List<Record> Records { get; } = new List<Record>();
        }

        private string Text(int tag)
        {
            if (_own.TryGetValue(tag, out byte[] own))
            {
                return Ascii(own, 0, own.Length);
            }

            Tag t = _tags[tag];
            return Ascii(_bytes, t.Start, t.End);
        }

        private static string Ascii(byte[] bytes, int start, int end)
        {
            var chars = new char[end - start];

            for (int i = start; i < end; i++)
            {
                chars[i - start] = (char)bytes[i];
            }

            return new string(chars);
        }

        private int NewTag(int code, string value, int line)
        {
            _tags.Add(new Tag(code, -1, -1, line));
            _own[_tags.Count - 1] = Encoding.ASCII.GetBytes(value);
            return _tags.Count - 1;
        }

        private void SetValue(int tag, string value)
        {
            _own[tag] = Encoding.ASCII.GetBytes(value);
            _changed = true;
        }

        /// <summary>The first group of a code in a record, after its group 0, before its first subclass marker where asked; -1 where there is none.</summary>
        private int Find(Record record, int code, bool beforeMarker = false)
        {
            for (int i = 1; i < record.Tags.Count; i++)
            {
                int tag = record.Tags[i];
                int found = _tags[tag].Code;

                if (found == code)
                {
                    return tag;
                }

                if (beforeMarker && found == 100)
                {
                    break;
                }
            }

            return -1;
        }

        private string Value(Record record, int code)
        {
            int tag = Find(record, code);
            return tag < 0 ? null : Text(tag).Trim();
        }

        private void Report(DxfRepairKind kind, string problem, string fix, int line, string handle = null, bool fixable = true)
        {
            bool done = _fix && fixable;
            _repairs.Add(new DxfRepair(kind, problem + (done && fix != null ? "; " + fix : string.Empty) + ".", line, handle, done));
        }

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Hex(ulong value) => value.ToString("X", CultureInfo.InvariantCulture);

        private static string Said(string text) => "'" + (text.Length > 24 ? text.Substring(0, 24) + "..." : text) + "'";

        #endregion

        #region Lines, pairs and sections

        /// <summary>
        /// Reads the lines as pairs of a group code and its value. A line where a code should stand that is not one is left out
        /// and the next read as the code, as ezdxf's <c>synced_bytes_loader</c> reads on; a run of such lines is said once.
        /// </summary>
        private void ReadPairs()
        {
            bool feeds = Array.IndexOf(_bytes, (byte)'\n', _start, _length - _start) >= 0;
            byte end = feeds ? (byte)'\n' : (byte)'\r';
            int at = _start;
            int line = 0;
            int badFirst = 0;
            int badLast = 0;
            string badText = null;

            if (!feeds && Array.IndexOf(_bytes, (byte)'\r', _start, _length - _start) >= 0)
            {
                Report(DxfRepairKind.LineEnds, "The lines of the file end with a carriage return alone", "they were read as lines and written with line feeds", 0);
                _changed = true;
            }

            bool NextLine(out int start, out int stop)
            {
                start = at;
                stop = at;

                if (at >= _length)
                {
                    return false;
                }

                int found = Array.IndexOf(_bytes, end, at, _length - at);
                stop = found < 0 ? _length : found;
                at = found < 0 ? _length : found + 1;
                line++;

                if (feeds && stop > start && _bytes[stop - 1] == '\r')
                {
                    stop--;
                    _crlf = true;
                }

                return true;
            }

            void SayBad()
            {
                if (badFirst > 0)
                {
                    Report(DxfRepairKind.BadGroupCode, badFirst == badLast ? "Line " + Number(badFirst) + " is not a group code (" + badText + ")" : "Lines " + Number(badFirst) + " to " + Number(badLast) + " are not group codes, the first " + badText, badFirst == badLast ? "it was left out" : "they were left out", badFirst);
                    badFirst = 0;
                    _changed = true;
                }
            }

            while (NextLine(out int codeStart, out int codeEnd))
            {
                int codeLine = line;

                if (!TryCode(codeStart, codeEnd, out int code))
                {
                    if (badFirst == 0)
                    {
                        badFirst = codeLine;
                        badText = Said(Ascii(_bytes, codeStart, codeEnd));
                    }

                    badLast = codeLine;
                    continue;
                }

                SayBad();

                if (_tags.Count == 0)
                {
                    _padded = codeStart < codeEnd && _bytes[codeStart] == ' ';
                }

                if (!NextLine(out int valueStart, out int valueEnd))
                {
                    Report(DxfRepairKind.MissingValue, "The group code " + Number(code) + " on line " + Number(codeLine) + ", the last line, has no value", "it was left out", codeLine);
                    _changed = true;
                    break;
                }

                _tags.Add(new Tag(code, valueStart, valueEnd, codeLine));
            }

            SayBad();
        }

        /// <summary>A group code: a whole number from 0 to 1071, spaces or tabs about it, as AutoCAD writes them.</summary>
        private bool TryCode(int start, int end, out int code)
        {
            code = 0;

            while (start < end && (_bytes[start] == ' ' || _bytes[start] == '\t'))
            {
                start++;
            }

            while (end > start && (_bytes[end - 1] == ' ' || _bytes[end - 1] == '\t'))
            {
                end--;
            }

            if (start == end || end - start > 4)
            {
                return false;
            }

            for (int i = start; i < end; i++)
            {
                if (_bytes[i] < '0' || _bytes[i] > '9')
                {
                    return false;
                }

                code = code * 10 + (_bytes[i] - '0');
            }

            return code <= 1071;
        }

        /// <summary>
        /// A file of R2007 or later, or one marked as UTF-8, whose bytes are not UTF-8: its text read in the code page its header
        /// names, Latin-1 where the runtime has not the page, and written as UTF-8, as it should have been.
        /// </summary>
        private void ReadEncoding()
        {
            string version = null;
            string page = null;

            for (int i = 0; i + 1 < _tags.Count && (version == null || page == null); i++)
            {
                if (_tags[i].Code == 9 && _tags[i + 1].Code == 1 && Text(i).Trim() == "$ACADVER")
                {
                    version = Text(i + 1).Trim();
                }
                else if (_tags[i].Code == 9 && _tags[i + 1].Code == 3 && Text(i).Trim() == "$DWGCODEPAGE")
                {
                    page = Text(i + 1).Trim();
                }
            }

            if (_start == 0 && string.CompareOrdinal(version ?? string.Empty, "AC1021") < 0)
            {
                return;
            }

            try
            {
                new UTF8Encoding(false, true).GetCharCount(_bytes, _start, _length - _start);
                return;
            }
            catch (DecoderFallbackException)
            {
            }

            Encoding read = Encoding.GetEncoding(28591);
            string named = page != null && page.StartsWith("ANSI_", StringComparison.OrdinalIgnoreCase) ? page.Substring(5) : "1252";

            try
            {
                if (int.TryParse(named, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number != 1252 && number != 65001)
                {
                    read = Encoding.GetEncoding(number);
                }
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
            }

            int count = 0;

            for (int i = 0; i < _tags.Count; i++)
            {
                Tag tag = _tags[i];

                for (int b = tag.Start; b < tag.End; b++)
                {
                    if (_bytes[b] >= 0x80)
                    {
                        _own[i] = Encoding.UTF8.GetBytes(read.GetString(_bytes, tag.Start, tag.End - tag.Start));
                        count++;
                        break;
                    }
                }
            }

            Report(DxfRepairKind.Encoding, "The file is of " + (version ?? "a version") + ", whose text is UTF-8, but " + Number(count) + " of its values are not UTF-8", "they were read in the code page " + (read.CodePage == 28591 ? "ISO-8859-1" : "ANSI_" + Number(read.CodePage)) + " and written as UTF-8", 0);
            _changed = true;
        }

        /// <summary>
        /// Gathers the pairs into records and the records into sections, as ezdxf's <c>rebuild_sections</c> does: a section not
        /// closed is closed where the next opens or the file ends, an ENDSEC that closes nothing is left out, entities outside
        /// every section are moved into ENTITIES and other records there left out; then the sections put in AutoCAD's order, and
        /// ENTITIES added where there is none.
        /// </summary>
        private void ReadSections()
        {
            var records = new List<Record>();
            int before = 0;

            for (int i = 0; i < _tags.Count; i++)
            {
                if (_tags[i].Code == 0)
                {
                    records.Add(new Record(Text(i).Trim(), _tags[i].Line));
                }
                else if (records.Count == 0)
                {
                    before += _tags[i].Code == 999 ? 0 : 1;
                    continue;
                }

                if (records.Count > 0)
                {
                    records[records.Count - 1].Tags.Add(i);
                }
            }

            if (before > 0)
            {
                Report(DxfRepairKind.SectionStructure, Number(before) + " groups stand before the first record of the file", "they were left out", 1);
                _changed = true;
            }

            if (records.Count == 0)
            {
                Report(DxfRepairKind.Unreadable, "Nothing of a DXF was found in the file, not one record: the drawing is empty", null, 0, null, false);
            }

            Section open = null;
            var orphans = new List<Record>();
            bool ended = false;

            for (int r = 0; r < records.Count && !ended; r++)
            {
                Record record = records[r];

                switch (record.Type)
                {
                    case "SECTION":
                        if (open != null)
                        {
                            Report(DxfRepairKind.SectionStructure, "The section " + open.Name + " opened on line " + Number(open.Head.Line) + " has no ENDSEC before the SECTION on line " + Number(record.Line), "it was closed there", record.Line);
                            _changed = true;
                        }

                        open = new Section(SectionName(record, r + 1 < records.Count ? records[r + 1] : null), record);
                        _sections.Add(open);
                        break;
                    case "ENDSEC":
                        if (open == null)
                        {
                            Report(DxfRepairKind.SectionStructure, "The ENDSEC on line " + Number(record.Line) + " closes no section", "it was left out", record.Line);
                            _changed = true;
                        }

                        open = null;
                        break;
                    case "EOF":
                        ended = true;
                        break;
                    default:
                        if (open != null)
                        {
                            open.Records.Add(record);
                        }
                        else
                        {
                            bool entity = Entities.Contains(record.Type);
                            Report(DxfRepairKind.SectionStructure, "The " + record.Type + " on line " + Number(record.Line) + " stands in no section", entity ? "it was moved into ENTITIES" : "it was left out", record.Line);
                            _changed = true;

                            if (entity)
                            {
                                orphans.Add(record);
                            }
                        }

                        break;
                }
            }

            if (!ended)
            {
                int last = _tags.Count > 0 ? _tags[_tags.Count - 1].Line + 1 : 0;
                Report(DxfRepairKind.Truncated, "The file ends on line " + Number(last) + " without the EOF that closes a DXF" + (open != null ? ", inside the section " + open.Name + " opened on line " + Number(open.Head.Line) : string.Empty), open != null ? "the section was closed and the EOF written" : "the EOF was written", last);
                _changed = true;
            }
            else if (open != null)
            {
                Report(DxfRepairKind.SectionStructure, "The section " + open.Name + " opened on line " + Number(open.Head.Line) + " has no ENDSEC before the EOF", "it was closed there", open.Head.Line);
                _changed = true;
            }

            _sections.RemoveAll(s => s.Name == null);
            _markers = records.Exists(r => Find(r, 100) >= 0);
            OrderSections(orphans);
        }

        /// <summary>The name of a section from its group 2; where it has none, the name of what its first record is, as ezdxf guesses it; null where nothing tells.</summary>
        private string SectionName(Record head, Record first)
        {
            int named = head.Tags.Count > 1 && _tags[head.Tags[1]].Code == 2 ? head.Tags[1] : -1;

            if (named >= 0)
            {
                return Text(named).Trim();
            }

            string type = first?.Type;
            string name = Find(head, 9) >= 0 ? "HEADER" : type == "TABLE" ? "TABLES" : type == "BLOCK" ? "BLOCKS" : type == "CLASS" ? "CLASSES"
                : type == "DICTIONARY" ? "OBJECTS" : type != null && Entities.Contains(type) ? "ENTITIES" : null;
            Report(DxfRepairKind.SectionStructure, "The SECTION on line " + Number(head.Line) + " has no name", name != null ? "it was named " + name + " from what it holds" : "it was left out with what it holds", head.Line);
            _changed = true;

            if (name != null)
            {
                head.Tags.Insert(1, NewTag(2, name, head.Line));
            }

            return name;
        }

        private void OrderSections(List<Record> orphans)
        {
            Section entities = _sections.Find(s => s.Name == "ENTITIES");

            if (entities == null)
            {
                Report(DxfRepairKind.MissingSection, "The file has no ENTITIES section", "an empty one was added", 0);
                entities = MadeSection("ENTITIES");
                _changed = true;
            }

            entities.Records.AddRange(orphans);

            if (_markers && !_sections.Exists(s => s.Name == "HEADER"))
            {
                Report(DxfRepairKind.MissingSection, "The file has no HEADER, though its records are of R13 or later", "one naming R2000 (AC1015) was added", 0);
                Section header = MadeSection("HEADER");
                header.Head.Tags.Add(NewTag(9, "$ACADVER", 0));
                header.Head.Tags.Add(NewTag(1, "AC1015", 0));
                _changed = true;
            }

            if (_markers)
            {
                foreach (string name in new[] { "TABLES", "BLOCKS", "OBJECTS" })
                {
                    if (!_sections.Exists(s => s.Name == name))
                    {
                        Report(DxfRepairKind.MissingSection, "The file has no " + name + " section, though its records are of R13 or later: what it held is lost", null, 0, null, false);
                    }
                }
            }

            // Sections AutoCAD does not name in its order (THUMBNAILIMAGE, ACDSDATA) may stand anywhere; once the others are put
            // in order, they follow them.
            int Rank(Section s) => Array.IndexOf(Order, s.Name) < 0 ? Order.Length : Array.IndexOf(Order, s.Name);
            bool ordered = true;
            int before = -1;

            foreach (Section section in _sections)
            {
                if (Rank(section) < Order.Length)
                {
                    ordered &= Rank(section) >= before;
                    before = Rank(section);
                }
            }

            if (!ordered)
            {
                var sorted = new List<KeyValuePair<int, Section>>();

                for (int i = 0; i < _sections.Count; i++)
                {
                    sorted.Add(new KeyValuePair<int, Section>(i, _sections[i]));
                }

                sorted.Sort((a, b) => Rank(a.Value) != Rank(b.Value) ? Rank(a.Value).CompareTo(Rank(b.Value)) : a.Key.CompareTo(b.Key));
                _sections.Clear();
                sorted.ForEach(p => _sections.Add(p.Value));
                Report(DxfRepairKind.SectionOrder, "The sections are not in AutoCAD's order", "they were put as HEADER, CLASSES, TABLES, BLOCKS, ENTITIES, OBJECTS", 0);
                _changed = true;
            }
        }

        private Section MadeSection(string name)
        {
            var head = new Record("SECTION", 0) { Made = true };
            head.Tags.Add(NewTag(0, "SECTION", 0));
            head.Tags.Add(NewTag(2, name, 0));
            var section = new Section(name, head);
            int at = 0;

            while (at < _sections.Count && Array.IndexOf(Order, _sections[at].Name) >= 0 && Array.IndexOf(Order, _sections[at].Name) < Array.IndexOf(Order, name))
            {
                at++;
            }

            _sections.Insert(at, section);
            return section;
        }

        #endregion

        #region Numbers

        /// <summary>Whether a group code holds a number: 1 a real, 2 a whole number, 0 not, as the DXF reference types them.</summary>
        internal static int NumberKind(int code)
        {
            if ((code >= 10 && code <= 59) || (code >= 110 && code <= 149) || (code >= 210 && code <= 239) || (code >= 460 && code <= 469) || (code >= 1010 && code <= 1059))
            {
                return 1;
            }

            return (code >= 60 && code <= 79) || (code >= 90 && code <= 99) || (code >= 160 && code <= 179) || (code >= 270 && code <= 299) || (code >= 370 && code <= 389)
                || (code >= 400 && code <= 409) || (code >= 420 && code <= 429) || (code >= 440 && code <= 459) || (code >= 1060 && code <= 1071) ? 2 : 0;
        }

        /// <summary>
        /// Whether bytes are plainly a number of the kind, without reading it: a sign, digits, and for a real one point; one with
        /// an exponent, or anything else, is read by <see cref="Classify"/>.
        /// </summary>
        internal static bool IsPlainNumber(byte[] bytes, int start, int end, bool real)
        {
            while (start < end && (bytes[start] == ' ' || bytes[start] == '\t'))
            {
                start++;
            }

            while (end > start && (bytes[end - 1] == ' ' || bytes[end - 1] == '\t'))
            {
                end--;
            }

            if (start < end && (bytes[start] == '-' || bytes[start] == '+'))
            {
                start++;
            }

            int digits = 0;
            bool point = false;

            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];

                if (c >= '0' && c <= '9')
                {
                    digits++;
                }
                else if (c == '.' && real && !point)
                {
                    point = true;
                }
                else
                {
                    return false;
                }
            }

            return digits > 0;
        }

        /// <summary>
        /// Reads the value of a group of a number: 0 where it is one, 1 where it is no number, given the number it holds as C's
        /// <c>strtod</c> finds one in it, as ezdxf recovers it, or nought; 2 where it is not finite (NaN, an infinity, or too
        /// large for a double), given nought.
        /// </summary>
        internal static int Classify(string text, bool real, out string value)
        {
            string trimmed = text.Trim();
            value = "0";

            if (real ? Numbers.TryRead(trimmed, out double read) : long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            {
                value = trimmed;
                return 0;
            }

            string upper = trimmed.ToUpperInvariant();

            if (real && (upper.Contains("NAN") || upper.Contains("INF") || upper.Contains("#IND") || IsNumberText(trimmed)))
            {
                return 2;
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                if (char.IsDigit(trimmed[i]) && trimmed[i] < 128)
                {
                    int from = i > 0 && (trimmed[i - 1] == '-' || trimmed[i - 1] == '+') ? i - 1 : i;
                    int to = i;

                    while (to < trimmed.Length && ((trimmed[to] >= '0' && trimmed[to] <= '9') || (real && trimmed[to] == '.' && trimmed.IndexOf('.', from, to - from) < 0)))
                    {
                        to++;
                    }

                    string found = trimmed.Substring(from, to - from);

                    if (real ? Numbers.TryRead(found, out _) : long.TryParse(found, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                    {
                        value = found;
                    }

                    break;
                }
            }

            return 1;
        }

        /// <summary>Whether a text is written as a number with an exponent, one that <see cref="Numbers.TryRead"/> refuses only as too large.</summary>
        private static bool IsNumberText(string text)
        {
            int e = text.IndexOfAny(new[] { 'e', 'E' });

            if (e <= 0 || e == text.Length - 1)
            {
                return false;
            }

            byte[] mantissa = Encoding.ASCII.GetBytes(text.Substring(0, e));
            byte[] exponent = Encoding.ASCII.GetBytes(text.Substring(e + 1));
            return IsPlainNumber(mantissa, 0, mantissa.Length, true) && IsPlainNumber(exponent, 0, exponent.Length, false);
        }

        /// <summary>
        /// The count a record keeps of its own parts, where it is not the number of them: of a LWPOLYLINE its group 90 against
        /// its vertices (group 10), of a HATCH its group 91 against its boundary paths (each opened by a group 92 before the
        /// style, group 75). The index among the groups given and the number it should hold; -1 where the count is right.
        /// </summary>
        internal static int WrongCount(string type, IReadOnlyList<int> codes, IReadOnlyList<string> values, out int should)
        {
            should = 0;
            int countCode = type == "LWPOLYLINE" ? 90 : type == "HATCH" ? 91 : 0;
            int at = -1;

            for (int i = 0; i < codes.Count && countCode != 0; i++)
            {
                if (at < 0 && codes[i] == countCode)
                {
                    at = i;
                }
                else if (type == "LWPOLYLINE" && codes[i] == 10)
                {
                    should++;
                }
                else if (type == "HATCH" && at >= 0 && codes[i] == 75)
                {
                    break;
                }
                else if (type == "HATCH" && at >= 0 && codes[i] == 92)
                {
                    should++;
                }
            }

            return at >= 0 && (!int.TryParse(values[at].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int given) || given != should) ? at : -1;
        }

        /// <summary>
        /// Reads every number of the records of HEADER, TABLES, BLOCKS, ENTITIES and OBJECTS: one that is no number given the
        /// number it holds or nought; one not finite gives nought, and an entity holding one is left out, a POLYLINE or an INSERT
        /// with its vertices or attributes, a vertex or an attribute alone; then the counts of LWPOLYLINE and HATCH set right.
        /// </summary>
        private void CheckValues()
        {
            foreach (Section section in _sections)
            {
                if (Array.IndexOf(Order, section.Name) < 0 || section.Name == "CLASSES")
                {
                    continue;
                }

                bool drawing = section.Name == "BLOCKS" || section.Name == "ENTITIES";
                CheckRecord(section.Head, false);

                for (int r = 0; r < section.Records.Count; r++)
                {
                    Record record = section.Records[r];
                    if (!CheckRecord(record, drawing && record.Type != "BLOCK" && record.Type != "ENDBLK"))
                    {
                        continue;
                    }

                    int last = r;

                    if (record.Type == "POLYLINE" || record.Type == "INSERT")
                    {
                        string part = record.Type == "POLYLINE" ? "VERTEX" : "ATTRIB";

                        while (last + 1 < section.Records.Count && section.Records[last + 1].Type == part)
                        {
                            last++;
                        }

                        if (last + 1 < section.Records.Count && section.Records[last + 1].Type == "SEQEND")
                        {
                            last++;
                        }
                    }

                    if (_fix)
                    {
                        section.Records.RemoveRange(r, last - r + 1);
                        r--;
                    }
                }
            }
        }

        /// <summary>Reads the numbers of a record and sets them right; true where it is an entity holding a number not finite, to be left out.</summary>
        private bool CheckRecord(Record record, bool entity)
        {
            string handle = record.Type == "SECTION" ? null : Value(record, record.Type == "DIMSTYLE" ? 105 : 5);
            string name = record.Type == "SECTION" ? "header" : record.Type + (handle != null ? " " + handle : string.Empty);

            for (int i = 1; i < record.Tags.Count; i++)
            {
                int tag = record.Tags[i];
                Tag t = _tags[tag];
                int kind = NumberKind(t.Code);

                if (kind == 0 || (!_own.ContainsKey(tag) && IsPlainNumber(_bytes, t.Start, t.End, kind == 1)))
                {
                    continue;
                }

                string text = Text(tag);
                int verdict = Classify(text, kind == 1, out string value);

                if (verdict == 2 && entity)
                {
                    Report(DxfRepairKind.NonFiniteNumber, "The " + name + " on line " + Number(record.Line) + " has a number that is not finite, " + Said(text) + " in its group " + Number(t.Code), "it was left out" + (record.Type == "POLYLINE" ? " with its vertices" : record.Type == "INSERT" ? " with its attributes" : string.Empty), t.Line, handle);
                    _changed = true;
                    return true;
                }

                if (verdict != 0)
                {
                    Report(verdict == 2 ? DxfRepairKind.NonFiniteNumber : DxfRepairKind.BadNumber, "The group " + Number(t.Code) + " of the " + name + " on line " + Number(t.Line) + " is " + (verdict == 2 ? "not a finite number, " : "no number, ") + Said(text), "it was read as " + value, t.Line, handle);
                    SetValue(tag, value);
                }
            }

            if (record.Type == "LWPOLYLINE" || record.Type == "HATCH")
            {
                var codes = new List<int>(record.Tags.Count);
                var values = new List<string>(record.Tags.Count);

                for (int i = 1; i < record.Tags.Count; i++)
                {
                    codes.Add(_tags[record.Tags[i]].Code);
                    values.Add(_tags[record.Tags[i]].Code == 90 || _tags[record.Tags[i]].Code == 91 ? Text(record.Tags[i]) : null);
                }

                int at = WrongCount(record.Type, codes, values, out int should);

                if (at >= 0)
                {
                    int tag = record.Tags[at + 1];
                    Report(record.Type == "HATCH" ? DxfRepairKind.WrongLoopCount : DxfRepairKind.WrongVertexCount, "The " + name + " on line " + Number(record.Line) + " counts " + Text(tag).Trim() + (record.Type == "HATCH" ? " boundary paths and has " : " vertices and has ") + Number(should), "the count was set to " + Number(should), _tags[tag].Line, handle);
                    SetValue(tag, Number(should));
                }
            }

            return false;
        }

        #endregion

        #region Handles and owners

        /// <summary>
        /// Reads the handles of the records of TABLES, BLOCKS, ENTITIES and OBJECTS: one that two records have is given to the
        /// first and a new one to the later, the records the later owns pointed at it after (<see cref="CheckOwners"/>); a record
        /// of a file of R13 or later with none, or one not hexadecimal, given a new one after its group 0.
        /// </summary>
        private void CheckHandles()
        {
            var records = new List<Record>();

            foreach (Section section in _sections)
            {
                if (section.Name == "TABLES" || section.Name == "BLOCKS" || section.Name == "ENTITIES" || section.Name == "OBJECTS")
                {
                    records.AddRange(section.Records.FindAll(r => r.Type != "ENDTAB"));
                }
            }

            ulong most = 0;
            var tags = new int[records.Count];

            for (int i = 0; i < records.Count; i++)
            {
                Record record = records[i];
                tags[i] = Find(record, record.Type == "DIMSTYLE" ? 105 : 5, true);

                if (tags[i] >= 0 && TryHex(Text(tags[i]), out ulong handle) && handle != 0)
                {
                    record.Handle = handle;
                    most = Math.Max(most, handle);
                }
            }

            _handles = _markers || most > 0;
            ulong seed = TryHex(HeaderValue("$HANDSEED", 5, out _) ?? string.Empty, out ulong given) ? given : 0;
            _most = most;
            _next = Math.Max(most + 1, Math.Max(seed, 1));

            for (int i = 0; i < records.Count; i++)
            {
                Record record = records[i];
                string name = record.Type + " on line " + Number(record.Line);

                if (record.Handle != 0 && Handles.Add(record.Handle))
                {
                    continue;
                }

                if (record.Handle != 0)
                {
                    ulong old = record.Handle;
                    record.Handle = _next++;
                    _renamed[record] = old;
                    Handles.Add(record.Handle);
                    Report(DxfRepairKind.DuplicateHandle, "The handle " + Hex(old) + " of the " + name + " is that of a record before it", "it was given the handle " + Hex(record.Handle), record.Line, Hex(old));
                    SetValue(tags[i], Hex(record.Handle));
                    continue;
                }

                if (!_markers)
                {
                    continue;
                }

                record.Handle = _next++;
                Handles.Add(record.Handle);
                Report(DxfRepairKind.MissingHandle, "The " + name + (tags[i] >= 0 ? " has a handle that is not hexadecimal, " + Said(Text(tags[i])) : " has no handle"), "it was given the handle " + Hex(record.Handle), record.Line);

                if (tags[i] >= 0)
                {
                    SetValue(tags[i], Hex(record.Handle));
                }
                else
                {
                    record.Tags.Insert(1, NewTag(record.Type == "DIMSTYLE" ? 105 : 5, Hex(record.Handle), record.Line));
                    _changed = true;
                }
            }
        }

        /// <summary>
        /// Writes the ENDBLK of a BLOCK that has none before the next BLOCK or the end of its section, and the SEQEND of a POLYLINE,
        /// or of an INSERT with attributes (66 of 1), that has none after its vertices or attributes; each on the layer of what it
        /// closes, with a handle of its own where the file gives handles.
        /// </summary>
        private void CloseBlocks()
        {
            foreach (Section section in _sections)
            {
                if (section.Name != "BLOCKS" && section.Name != "ENTITIES")
                {
                    continue;
                }

                List<Record> records = section.Records;
                Record block = null;

                for (int r = 0; r <= records.Count; r++)
                {
                    Record record = r < records.Count ? records[r] : null;

                    if (section.Name == "BLOCKS" && (record == null || record.Type == "BLOCK") && block != null)
                    {
                        Report(DxfRepairKind.MissingEndBlk, "The BLOCK " + (Value(block, 2) ?? string.Empty) + " on line " + Number(block.Line) + " has no ENDBLK", "one was written", block.Line, block.Handle != 0 ? Hex(block.Handle) : null);
                        records.Insert(r++, Made("ENDBLK", block, Value(block, 330), "AcDbBlockEnd"));
                        _changed = true;
                    }

                    if (record == null)
                    {
                        break;
                    }

                    block = record.Type == "BLOCK" ? record : record.Type == "ENDBLK" ? null : block;
                    string part = record.Type == "POLYLINE" ? "VERTEX" : record.Type == "INSERT" && Value(record, 66) == "1" ? "ATTRIB" : null;

                    if (part == null)
                    {
                        continue;
                    }

                    int next = r + 1;

                    while (next < records.Count && records[next].Type == part)
                    {
                        next++;
                    }

                    if (next >= records.Count || records[next].Type != "SEQEND")
                    {
                        Report(DxfRepairKind.MissingSeqEnd, "The " + record.Type + " on line " + Number(record.Line) + " has no SEQEND after its " + (part == "VERTEX" ? "vertices" : "attributes"), "one was written", record.Line, record.Handle != 0 ? Hex(record.Handle) : null);
                        records.Insert(next, Made("SEQEND", record, record.Handle != 0 ? Hex(record.Handle) : null, null));
                        _changed = true;
                    }

                    // On to the SEQEND, read next as a record of no parts.
                    r = next - 1;
                }
            }
        }

        /// <summary>A record written to close another: its handle, its owner and its layer, with the markers of a file of R13 or later.</summary>
        private Record Made(string type, Record of, string owner, string marker)
        {
            var made = new Record(type, of.Line) { Made = true };
            made.Tags.Add(NewTag(0, type, of.Line));

            if (_handles)
            {
                made.Handle = _next++;
                Handles.Add(made.Handle);
                made.Tags.Add(NewTag(5, Hex(made.Handle), of.Line));
            }

            if (_markers && owner != null)
            {
                made.Tags.Add(NewTag(330, owner, of.Line));
            }

            if (_markers)
            {
                made.Tags.Add(NewTag(100, "AcDbEntity", of.Line));
            }

            int layer = Find(of, 8);
            made.Tags.Add(layer >= 0 ? layer : NewTag(8, "0", of.Line));

            if (_markers && marker != null)
            {
                made.Tags.Add(NewTag(100, marker, of.Line));
            }

            return made;
        }

        /// <summary>
        /// Reads the owner of each record (its group 330 before its first subclass marker, outside the lists in braces): one that
        /// names no record is set to the record that holds it, as the place of the record says (the table of an entry, the block
        /// of an entity, the polyline of a vertex), where the place says; and one that names a record given a new handle as a
        /// duplicate, by the handle that record had, is set to its new handle.
        /// </summary>
        private void CheckOwners()
        {
            if (!_markers)
            {
                return;
            }

            var blockRecords = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);

            foreach (Section section in _sections)
            {
                foreach (Record record in section.Name == "TABLES" ? section.Records : new List<Record>())
                {
                    string name = record.Type == "BLOCK_RECORD" ? Value(record, 2) : null;

                    if (name != null && !blockRecords.ContainsKey(name))
                    {
                        blockRecords.Add(name, record);
                    }
                }
            }

            Record Block(string name)
            {
                name = name ?? string.Empty;
                name = string.Equals(name, "$MODEL_SPACE", StringComparison.OrdinalIgnoreCase) ? "*Model_Space" : string.Equals(name, "$PAPER_SPACE", StringComparison.OrdinalIgnoreCase) ? "*Paper_Space" : name;
                return blockRecords.TryGetValue(name, out Record found) ? found : null;
            }

            foreach (Section section in _sections)
            {
                Record holder = null;
                Record head = null;

                foreach (Record record in section.Records)
                {
                    Record place = null;

                    if (section.Name == "TABLES")
                    {
                        holder = record.Type == "TABLE" ? record : record.Type == "ENDTAB" ? null : holder;
                        place = record.Type == "TABLE" ? null : holder;
                    }
                    else if (section.Name == "BLOCKS" || section.Name == "ENTITIES")
                    {
                        holder = record.Type == "BLOCK" ? Block(Value(record, 2)) : holder;
                        bool follows = head != null && (record.Type == "VERTEX" || record.Type == "ATTRIB" || record.Type == "SEQEND");
                        place = follows ? head : section.Name == "BLOCKS" ? holder : Block(Value(record, 67) == "1" ? "*Paper_Space" : "*Model_Space");
                        head = record.Type == "POLYLINE" || record.Type == "INSERT" ? record : follows && record.Type != "SEQEND" ? head : null;
                    }

                    CheckOwner(record, place);

                    if (section.Name == "BLOCKS" && record.Type == "ENDBLK")
                    {
                        holder = null;
                    }
                }
            }
        }

        private void CheckOwner(Record record, Record place)
        {
            int depth = 0;
            int tag = -1;

            for (int i = 1; i < record.Tags.Count && tag < 0; i++)
            {
                Tag t = _tags[record.Tags[i]];

                if (t.Code == 100)
                {
                    break;
                }

                depth += t.Code == 102 ? (Text(record.Tags[i]).Trim().StartsWith("{", StringComparison.Ordinal) ? 1 : -1) : 0;
                tag = t.Code == 330 && depth == 0 ? record.Tags[i] : -1;
            }

            if (tag < 0 || !TryHex(Text(tag), out ulong owner) || owner == 0)
            {
                return;
            }

            string name = record.Type + (record.Handle != 0 ? " " + Hex(record.Handle) : string.Empty) + " on line " + Number(record.Line);

            if (Handles.Contains(owner))
            {
                if (place != null && _renamed.TryGetValue(place, out ulong old) && old == owner && place.Handle != owner)
                {
                    Report(DxfRepairKind.BadOwner, "The " + name + " names as its owner the handle " + Hex(old) + ", which its " + place.Type + " shared with a record before it", "it now names " + Hex(place.Handle) + ", the " + place.Type + "'s new handle", record.Line, record.Handle != 0 ? Hex(record.Handle) : null);
                    SetValue(tag, Hex(place.Handle));
                }

                return;
            }

            bool known = place != null && place.Handle != 0;
            Report(DxfRepairKind.BadOwner, "The " + name + " names as its owner the handle " + Text(tag).Trim() + ", which no record has", known ? "it now names " + Hex(place.Handle) + ", its " + place.Type : null, record.Line, record.Handle != 0 ? Hex(record.Handle) : null, known);

            if (known)
            {
                SetValue(tag, Hex(place.Handle));
            }
        }

        /// <summary>Sets <c>$HANDSEED</c> above every handle of the file, adding it to the header where it has none.</summary>
        private void CheckSeed()
        {
            Section header = _sections.Find(s => s.Name == "HEADER");

            if (!_handles || header == null)
            {
                return;
            }

            ulong most = 0;
            ulong seed = 0;

            foreach (ulong handle in Handles)
            {
                most = Math.Max(most, handle);
            }

            string given = HeaderValue("$HANDSEED", 5, out int tag);
            bool read = given != null && TryHex(given, out seed);

            if (read && seed > most)
            {
                return;
            }

            // A seed above every handle the file had is raised above those the repairs gave without a word: it was right.
            if (!read || seed <= _most)
            {
                Report(DxfRepairKind.HandseedTooLow, given == null ? "The header has no $HANDSEED" : "The $HANDSEED " + given + " of the header is not above every handle of the file, the greatest " + Hex(_most), "it was set to " + Hex(most + 1), 0);
            }

            if (tag >= 0)
            {
                SetValue(tag, Hex(most + 1));
            }
            else
            {
                header.Head.Tags.Add(NewTag(9, "$HANDSEED", 0));
                header.Head.Tags.Add(NewTag(5, Hex(most + 1), 0));
                _changed = true;
            }
        }

        /// <summary>The value of the first group of a code after a variable of the header; null where there is none.</summary>
        private string HeaderValue(string variable, int code, out int tag)
        {
            tag = -1;
            Section header = _sections.Find(s => s.Name == "HEADER");
            List<int> tags = header?.Head.Tags ?? new List<int>();

            for (int i = 0; i + 1 < tags.Count; i++)
            {
                if (_tags[tags[i]].Code == 9 && Text(tags[i]).Trim() == variable)
                {
                    for (int k = i + 1; k < tags.Count && _tags[tags[k]].Code != 9; k++)
                    {
                        if (_tags[tags[k]].Code == code)
                        {
                            tag = tags[k];
                            return Text(tag).Trim();
                        }
                    }
                }
            }

            return null;
        }

        internal static bool TryHex(string text, out ulong value)
        {
            value = 0;
            text = text.Trim();

            if (text.Length == 0 || text.Length > 16)
            {
                return false;
            }

            return ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
        }

        #endregion

        #region Writing

        /// <summary>Writes the file again: the mark of UTF-8 where it had one, each section with its records, ENDSEC after each and EOF at the end, every line ended as the file ended them.</summary>
        private void Write()
        {
            var output = new MemoryStream(_length + 1024);
            byte[] end = _crlf ? new[] { (byte)'\r', (byte)'\n' } : new[] { (byte)'\n' };
            output.Write(_bytes, 0, _start);

            void Pair(int code, int tag, byte[] value)
            {
                string written = _padded ? code.ToString(CultureInfo.InvariantCulture).PadLeft(3) : code.ToString(CultureInfo.InvariantCulture);
                byte[] codeBytes = Encoding.ASCII.GetBytes(written);
                output.Write(codeBytes, 0, codeBytes.Length);
                output.Write(end, 0, end.Length);

                if (value != null)
                {
                    output.Write(value, 0, value.Length);
                }
                else if (_own.TryGetValue(tag, out byte[] own))
                {
                    output.Write(own, 0, own.Length);
                }
                else
                {
                    output.Write(_bytes, _tags[tag].Start, _tags[tag].End - _tags[tag].Start);
                }

                output.Write(end, 0, end.Length);
            }

            void Records(Record record)
            {
                foreach (int tag in record.Tags)
                {
                    Pair(_tags[tag].Code, tag, null);
                }
            }

            foreach (Section section in _sections)
            {
                Records(section.Head);
                section.Records.ForEach(Records);
                Pair(0, -1, Encoding.ASCII.GetBytes("ENDSEC"));
            }

            Pair(0, -1, Encoding.ASCII.GetBytes("EOF"));
            Bytes = output.GetBuffer();
            Length = (int)output.Length;
        }

        /// <summary>
        /// The file with no section but ENTITIES, every other record left out, for a file that could not be read after the
        /// repairs; empty where there is none.
        /// </summary>
        internal byte[] EntitiesOnly(out int length)
        {
            _sections.RemoveAll(s => s.Name != "ENTITIES");

            if (_sections.Count == 0)
            {
                MadeSection("ENTITIES");
            }

            Write();
            length = Length;
            return Bytes;
        }

        /// <summary>The file with the code page its header names set to ANSI_1252, for a page the reader refuses.</summary>
        internal bool WithoutCodePage()
        {
            HeaderValue("$DWGCODEPAGE", 3, out int tag);

            if (tag < 0)
            {
                return false;
            }

            SetValue(tag, "ANSI_1252");
            Write();
            return true;
        }

        #endregion
    }
}
