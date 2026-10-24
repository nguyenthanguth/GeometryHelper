using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A DXF file opened to be saved again, with nothing of it lost: every byte of it is kept, and what it holds is read
    /// from them when asked for, its entities with the library's shapes for them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opened and saved with nothing changed, a file is written back byte for byte as it was read: its version, its line
    /// ends, its group codes padded or not, its mark of UTF-8 or none, its code page, its comments, and every section, table,
    /// entity and object of it, those the library does not read (HATCH, DIMENSION, LAYOUT, DICTIONARY, extended data and the
    /// rest) as well as those it does. A file that <see cref="DxfReader"/> refuses is refused the same way, a binary DXF and
    /// one with no EOF among them.
    /// </para>
    /// <para>
    /// The entities of the drawing are in <see cref="ModelSpace"/> and <see cref="PaperSpace"/>, those of the blocks in
    /// <see cref="Blocks"/>, each a <see cref="DxfItem"/> whose <see cref="DxfItem.Shapes"/> are what <see cref="DxfReader"/>
    /// reads of it with its default options: in the coordinates of the file, as it prints them, shapes of space, no INSERT
    /// placed and no unit scaled. <see cref="DxfReader"/> is the faster way to read a drawing into shapes alone; this class
    /// is the way to save it again.
    /// </para>
    /// <para>
    /// The entities of the drawing can be changed, added and removed: an entity given a new <see cref="DxfItem.Shape"/> or a
    /// new look has its record written anew, every group the change is not about kept as it was, and every other record of
    /// the file is saved with its own bytes. Editing is for one thread at a time.
    /// </para>
    /// <code>
    /// DxfFile file = DxfFile.Open("plan.dxf");
    /// foreach (DxfItem item in file.ModelSpace) { ... }   // Type, Handle, Layer, Groups, Shape
    /// file.ModelSpace[0].Colour = 1;                      // that record written anew
    /// file.ModelSpace.Add(new GeoCircle2(new GeoPoint2(0, 0), 5), "holes");
    /// file.Save("copy.dxf");                              // every other record the same bytes
    /// </code>
    /// </remarks>
    internal sealed partial class DxfFile
    {
        private readonly byte[] _bytes;
        private readonly int _length;
        private readonly int[] _starts;
        private readonly string[] _types;
        private readonly Section[] _sections;
        private readonly Lazy<Drawing> _drawing;
        private readonly Lazy<IReadOnlyDictionary<string, DxfFileBlock>> _blocks;
        private DxfReader.TextDecoding _decoding;
        private Lazy<IReadOnlyList<DxfLayer>> _layers;
        private Lazy<DxfReader.Placer> _placer;
        private IReadOnlyDictionary<string, IReadOnlyList<DxfTypedValue>> _header;
        private string _namedPage;

        private DxfFile(byte[] bytes, int length, string source)
        {
            if (StartsWith(bytes, 0, length, "AutoCAD Binary DXF"))
            {
                throw Binary();
            }

            var warnings = new List<string>();
            _bytes = bytes;
            _length = length;
            _decoding = DxfReader.TextDecoding.Of(bytes, length, warnings);
            Warnings = warnings.AsReadOnly();

            if (StartsWith(bytes, _decoding.Start, length, "AutoCAD Binary DXF"))
            {
                throw Binary();
            }

            var walk = new Walk(this);
            _starts = walk.Starts.ToArray();
            _types = walk.Types.ToArray();
            _sections = walk.Sections.ToArray();
            ReadHeader();

            _drawing = new Lazy<Drawing>(ReadDrawing);
            _blocks = new Lazy<IReadOnlyDictionary<string, DxfFileBlock>>(ReadBlocks);
            _layers = NewLayers();
            _placer = new Lazy<DxfReader.Placer>(NewPlacer);

            // The template of the entities not yet in a file is opened once, unsaid.
            if (source == null)
            {
                return;
            }

            GeometryHelperLog.Info("DxfFile: " + Number(_starts.Length) + " records opened from " + source + ", " + Number(length) + " bytes, version " + (Version ?? "not given") + (warnings.Count > 0 ? ": " + string.Join(" ", warnings) : "."));
        }

        #region Opening and saving

        /// <summary>
        /// Opens a DXF file from a path.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The file, every byte of it kept.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a DXF, is binary, or is cut short, the message naming the line; or when its header names a code page that does not read each byte of ASCII as itself, the message naming the page; as <see cref="DxfReader"/> refuses it.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <remarks>
        /// Its text is read as <see cref="DxfReader.Read(string, DxfReadOptions)"/> reads it: UTF-8 from R2007 on, the code page
        /// of its header before. The bytes are kept whatever they are, so a letter the runtime cannot read is saved as it was.
        /// </remarks>
        public static DxfFile Open(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            byte[] bytes = File.ReadAllBytes(path);
            return new DxfFile(bytes, bytes.Length, "'" + Path.GetFileName(path) + "'");
        }

        /// <summary>
        /// Opens a DXF file from a stream, from where the stream stands to its end.
        /// </summary>
        /// <param name="stream">The stream; it is read to its end and left open.</param>
        /// <returns>The file, every byte of it kept.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a DXF, is binary, or is cut short, the message naming the line; or when its header names a code page that does not read each byte of ASCII as itself, the message naming the page.</exception>
        public static DxfFile Open(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // Sized to what is left of a stream that can say, so that the copy is made once and kept where it lies.
            int left = stream.CanSeek ? (int)Math.Min(int.MaxValue, Math.Max(0L, stream.Length - stream.Position)) : 0;
            var copy = new MemoryStream(left);
            stream.CopyTo(copy);
            return new DxfFile(copy.GetBuffer(), (int)copy.Length, "a stream");
        }

        /// <summary>
        /// Opens a DXF file from its text.
        /// </summary>
        /// <param name="text">The text of the file, as <see cref="ToText"/> or <see cref="DxfWriter.ToString"/> gives it.</param>
        /// <returns>The file, its text kept as the bytes of UTF-8.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="FormatException">Thrown when the text is not a DXF, is binary, or is cut short; the message names the line.</exception>
        /// <remarks>
        /// The text is kept as UTF-8, a byte order mark at its start as the mark of UTF-8, so <see cref="ToText"/> gives it
        /// back, the mark left out as it leaves out that of a file, and <see cref="Save(string)"/> writes it so, the mark as
        /// the three bytes of UTF-8's: a file of R2007 or later is UTF-8 as it should be, and one of an
        /// earlier version whose text is all ASCII, as <see cref="DxfWriter"/> writes it, is the same in any code page. One of
        /// an earlier version with letters beyond ASCII is read back so by <see cref="DxfReader"/>, which says so in a warning,
        /// but AutoCAD reads such a file in its code page: open it from its bytes to keep them as they are.
        /// </remarks>
        public static DxfFile Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            return new DxfFile(bytes, bytes.Length, "a text");
        }

        /// <summary>
        /// Saves the file to a path, replacing whatever is there.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        /// <remarks>
        /// Nothing having been changed, the bytes written are those that were read. Changed, each record not changed, added or
        /// removed is written with its own bytes, the header's <c>$HANDSEED</c> and extents set as the changes need them.
        /// </remarks>
        public void Save(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Save(file);
            }
        }

        /// <summary>
        /// Saves the file to a stream, where it stands.
        /// </summary>
        /// <param name="stream">The stream; it is written to and left open.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <remarks>
        /// Nothing having been changed, the bytes written are those that were read. Changed, each record not changed, added or
        /// removed is written with its own bytes.
        /// </remarks>
        public void Save(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            Flush();

            if (_slots == null)
            {
                stream.Write(_bytes, 0, _length);
                return;
            }

            WriteEdited(stream);
        }

        /// <summary>
        /// Gets the text of the file, as <see cref="DxfReader"/> reads it from its bytes: UTF-8 from R2007 on, the code page of
        /// its header before, the mark of UTF-8 at its start left out; with what was changed, as <see cref="Save(Stream)"/>
        /// writes it.
        /// </summary>
        /// <returns>The text of the file, its line ends as the file has them.</returns>
        public string ToText()
        {
            Flush();

            if (_slots == null)
            {
                return _decoding.GetString(_bytes, _decoding.Start, _length - _decoding.Start);
            }

            using (var saved = new MemoryStream(_length))
            {
                WriteEdited(saved);
                return _decoding.GetString(saved.GetBuffer(), _decoding.Start, (int)saved.Length - _decoding.Start);
            }
        }

        #endregion

        #region What the file holds

        /// <summary>
        /// Gets the version of AutoCAD the file is written for (<c>$ACADVER</c>), such as <c>AC1015</c> for R2000; null where
        /// the file does not say, as <see cref="DxfDocument.Version"/> gives it.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// Gets the code page the text of the file is read in: <c>UTF-8</c> for a file of R2007 or later, one with the mark of
        /// UTF-8, and one before R2007 whose bytes are UTF-8; else the page the header names (<c>$DWGCODEPAGE</c>) as
        /// <c>ANSI_</c> and its number, <c>ANSI_1252</c> where it names none; <c>ISO-8859-1</c> where the runtime has not the
        /// page it names, and a warning says so. A file all ASCII reads the same in any: its page is the one its letters
        /// beyond ASCII would be read in. The header's own words are in <see cref="Header"/>.
        /// </summary>
        public string CodePage { get; private set; }

        /// <summary>
        /// Gets what was said of reading the text of the file, each a sentence: a file before R2007 whose text is UTF-8, one
        /// of R2007 or later with bytes that are not, or one in a code page the runtime has not. Empty for most files.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Gets the variables of the header, by name in the order of the names: for each, such as <c>$INSUNITS</c> or
        /// <c>$HANDSEED</c>, the groups after its group 9 as the file has them, the unit 4 of millimetres as the one group
        /// 70 of value <c>4</c>, a point as its groups 10, 20 and 30. A variable named twice is as last given, as the reader
        /// reads the version and the code page. After a change, the header as the file is saved, <c>$HANDSEED</c> and the
        /// extents set as the change needs them.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<DxfTypedValue>> Header
        {
            get
            {
                FlushHeader();
                return _header;
            }
        }

        /// <summary>
        /// Gets the entities of model space in the order of the file: those of the ENTITIES section not in paper space, and
        /// those added after them. A POLYLINE is one entity with its vertices, and an INSERT one with its attributes. Shapes
        /// are added to it and entities removed from it, as <see cref="DxfSpace"/> says.
        /// </summary>
        public DxfSpace ModelSpace => _drawing.Value.Model;

        /// <summary>
        /// Gets the entities of the ENTITIES section in paper space (group 67 of 1), in the order of the file: those of the
        /// layout last shown. Those of the other layouts are in the blocks <c>*Paper_Space0</c>, <c>*Paper_Space1</c> and so
        /// on, in <see cref="Blocks"/>.
        /// </summary>
        public DxfSpace PaperSpace => _drawing.Value.Paper;

        /// <summary>
        /// Gets the blocks of the file by name, in the order of the names, letters of either case the same as in AutoCAD:
        /// for each, its name, base point and flags and the entities between its BLOCK and its ENDBLK, in the order of the
        /// file. The blocks of the layouts, <c>*Model_Space</c> and <c>*Paper_Space</c>, are among them. A block defined again
        /// under a name already defined is kept in the file and left out here, the first kept, as <see cref="DxfReader"/>
        /// keeps it.
        /// </summary>
        public IReadOnlyDictionary<string, DxfFileBlock> Blocks => _blocks.Value;

        /// <summary>
        /// Gets the layers of the layer table, in the order of the table, as <see cref="DxfDocument.LayerTable"/> reads them.
        /// </summary>
        public IReadOnlyList<DxfLayer> LayerTable => _layers.Value;

        #endregion

        #region Records

        /// <summary>A section of the file: its name, and the indices of its SECTION record and of its ENDSEC.</summary>
        private readonly struct Section
        {
            internal Section(string name, int first, int last)
            {
                Name = name;
                First = first;
                Last = last;
            }

            internal string Name { get; }

            internal int First { get; }

            internal int Last { get; }
        }

        /// <summary>
        /// A record as it stands: the bytes it is in, where in them it begins, at the line of its group 0, and ends, at the
        /// line of the next record, and its type. A record of the file as read is in the file's bytes; one written anew in its
        /// own.
        /// </summary>
        internal readonly struct Rec
        {
            internal Rec(byte[] bytes, int start, int end, string type)
            {
                Bytes = bytes;
                Start = start;
                End = end;
                Type = type;
            }

            internal byte[] Bytes { get; }

            internal int Start { get; }

            internal int End { get; }

            internal string Type { get; }
        }

        /// <summary>The bytes of a record: from the line of its group 0 to that of the next record.</summary>
        private int End(int record) => record + 1 < _starts.Length ? _starts[record + 1] : _length;

        /// <summary>A record of the file as it was read.</summary>
        private Rec Original(int record) => new Rec(_bytes, _starts[record], End(record), _types[record]);

        /// <summary>
        /// The records of the file from one to the one before another as they stand: each as read where nothing has changed
        /// it, else what was written in its place, which may be nothing, or more records than one.
        /// </summary>
        private List<Rec> Current(int first, int end)
        {
            var records = new List<Rec>(end - first);

            for (int i = first; i < end; i++)
            {
                List<Piece> slot = _slots?[i];

                if (slot == null)
                {
                    records.Add(Original(i));
                    continue;
                }

                foreach (Piece piece in slot)
                {
                    records.AddRange(piece.Records);
                }
            }

            return records;
        }

        /// <summary>The records of an entity as they stand: its own, then a POLYLINE's vertices and SEQEND, an INSERT's attributes.</summary>
        internal List<Rec> RecordsOf(DxfItem item)
        {
            if (item.Parent != null)
            {
                return new List<Rec> { RecordAt(item.Parent, item.Index) };
            }

            return item.Piece != null ? new List<Rec>(item.Piece.Records) : Current(item.First, item.First + item.Count);
        }

        /// <summary>One record of an entity as it stands, its own (0) or one that follows it, read without copying the others.</summary>
        internal Rec RecordAt(DxfItem item, int index)
        {
            if (item.Parent != null)
            {
                return RecordAt(item.Parent, item.Index + index);
            }

            if (item.Piece != null)
            {
                return item.Piece.Records[index];
            }

            return _slots?[item.First] == null ? Original(item.First + index) : Current(item.First, item.First + item.Count)[index];
        }

        /// <summary>The first record of an entity as it stands, its own, which holds its look.</summary>
        private Rec HeadOf(DxfItem item) => RecordAt(item, 0);

        /// <summary>
        /// Walks the groups of a record after its group 0, comments left out: for each, the code and the bytes of the value,
        /// a carriage return at the end of its line taken off.
        /// </summary>
        private static void EachGroup(Rec record, Action<int, int, int> take)
        {
            var walk = new GroupWalk(record);

            while (walk.Next(out int code, out int valueStart, out int valueEnd))
            {
                take(code, valueStart, valueEnd);
            }
        }

        /// <summary>The groups of a record after its group 0, one at a time, comments left out, as <see cref="EachGroup"/> gives them.</summary>
        private struct GroupWalk
        {
            private readonly byte[] _bytes;
            private readonly int _end;
            private int _at;
            private bool _opened;

            internal GroupWalk(Rec record)
            {
                _bytes = record.Bytes;
                _end = record.End;
                _at = record.Start;
                _opened = false;
            }

            internal bool Next(out int code, out int valueStart, out int valueEnd)
            {
                byte[] bytes = _bytes;
                int end = _end;
                int at = _at;

                while (at < end)
                {
                    int codeEnd = LineEnd(bytes, at, end, out valueStart);
                    TryCode(bytes, at, codeEnd, out code);
                    valueEnd = LineEnd(bytes, valueStart, end, out at);

                    if (!_opened)
                    {
                        _opened = true;
                        continue;
                    }

                    if (code != 999)
                    {
                        _at = at;
                        return true;
                    }
                }

                _at = at;
                code = 0;
                valueStart = 0;
                valueEnd = 0;
                return false;
            }
        }

        private string Text(Rec record, int start, int end) => _decoding.GetString(record.Bytes, start, end - start);

        private string Text(int start, int end) => _decoding.GetString(_bytes, start, end - start);

        /// <summary>The groups of an entity's records, as <see cref="DxfItem.Groups"/> gives them.</summary>
        internal IReadOnlyList<DxfTypedValue> GroupsOf(DxfItem item)
        {
            var groups = new List<DxfTypedValue>();
            bool first = true;

            foreach (Rec record in RecordsOf(item))
            {
                if (!first)
                {
                    groups.Add(DxfTypedValue.Read(0, record.Type));
                }

                first = false;
                EachGroup(record, (code, start, end) => groups.Add(DxfTypedValue.Read(code, Text(record, start, end))));
            }

            return groups.AsReadOnly();
        }

        /// <summary>A record as the reader takes it: its type and its groups, comments left out.</summary>
        private DxfReader.Record Record(Rec record)
        {
            var read = new DxfReader.Record(record.Type, 0);
            EachGroup(record, (code, start, end) => read.Add(code, Text(record, start, end)));
            return read;
        }

        /// <summary>The records of an entity as the reader takes them: a POLYLINE's vertices and SEQEND following it.</summary>
        private DxfReader.Record Records(DxfItem item)
        {
            Rec head = HeadOf(item);
            DxfReader.Record record = Record(head);

            if (head.Type == "POLYLINE")
            {
                List<Rec> records = RecordsOf(item);
                var following = new List<DxfReader.Record>(records.Count - 1);

                for (int i = 1; i < records.Count; i++)
                {
                    following.Add(Record(records[i]));
                }

                record.Following = following;
            }

            return record;
        }

        /// <summary>The look of an entity as its record writes it, each group the first of its code.</summary>
        internal sealed class Look
        {
            internal string Handle { get; set; }

            internal string Layer { get; set; }

            internal int? Colour { get; set; }

            internal int? TrueColour { get; set; }

            internal string Linetype { get; set; }

            internal int? Lineweight { get; set; }

            internal double Thickness { get; set; }
        }

        internal Look LookOf(DxfItem item)
        {
            var values = new Dictionary<int, string>();
            Rec record = HeadOf(item);
            var walk = new GroupWalk(record);

            while (walk.Next(out int code, out int start, out int end))
            {
                if ((code == 5 || code == 8 || code == 6 || code == 39 || code == 62 || code == 370 || code == 420) && !values.ContainsKey(code))
                {
                    values.Add(code, Text(record, start, end));
                }
            }

            string Value(int code) => values.TryGetValue(code, out string value) ? value : null;

            int? Whole(int code) => values.TryGetValue(code, out string value) && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : (int?)null;

            string handle = Value(5)?.Trim();
            double thickness = values.TryGetValue(39, out string raised) && Numbers.TryRead(raised, out double read) && Guard.IsFinite(read) ? read : 0.0;

            return new Look
            {
                Handle = string.IsNullOrEmpty(handle) ? null : handle,
                Layer = Value(8),
                Colour = Whole(62),
                TrueColour = Whole(420),
                Linetype = Value(6),
                Lineweight = Whole(370),
                Thickness = thickness,
            };
        }

        internal List<object> ShapesOf(DxfItem item)
        {
            if (item.Type == "INSERT")
            {
                DxfInsert insert = DxfReader.InsertOf(Record(HeadOf(item)), () => Placed(item));
                return insert == null ? new List<object>() : new List<object> { insert };
            }

            return DxfReader.ShapesOf(Records(item), Tolerance.Default);
        }

        /// <summary>What the reader places of an INSERT, with the layer table and the blocks of the file.</summary>
        private DxfReader.Placing Placed(DxfItem insert)
        {
            DxfReader.Placing placing = _placer.Value.Place(Record(HeadOf(insert)), insert.Block);

            // One line for the placing, outside the lock: Warn where an entity threw, Debug for what was left out as expected.
            if (placing.Warnings.Count > 0)
            {
                string line = "DxfFile: the INSERT " + (insert.Handle ?? "with no handle") + (insert.Block != null ? " in the block " + insert.Block : string.Empty)
                    + " placed " + Number(placing.Entities.Count) + " entities: " + string.Join(" ", placing.Warnings);

                if (placing.Failure != null)
                {
                    GeometryHelperLog.Warn(line, placing.Failure);
                }
                else
                {
                    GeometryHelperLog.Debug(line);
                }
            }

            return placing;
        }

        /// <summary>
        /// The placing of the INSERTs, with the layer table and the blocks of the file read once, and the layers of the
        /// entities of the drawing the reader reads, in their order, as the reader has met them when it places the INSERTs.
        /// </summary>
        private DxfReader.Placer NewPlacer()
        {
            var drawingLayers = new List<string>();

            foreach (DxfItem item in ModelSpace)
            {
                if (item.Type != "INSERT" && item.Shapes.Count > 0)
                {
                    drawingLayers.Add(DxfReader.LayerOf(Record(HeadOf(item))));
                }
            }

            var blocks = new List<List<DxfReader.Record>>();

            foreach (Section section in _sections)
            {
                if (section.Name == "BLOCKS")
                {
                    blocks.Add(Current(section.First + 1, section.Last).ConvertAll(r => Record(r)));
                }
            }

            return new DxfReader.Placer(LayerRecords(), drawingLayers, blocks, Tolerance.Default);
        }

        private Lazy<IReadOnlyList<DxfLayer>> NewLayers() => new Lazy<IReadOnlyList<DxfLayer>>(() =>
        {
            LayerTableReadings++;
            return Array.AsReadOnly(DxfReader.LayerTableOf(LayerRecords()));
        });

        /// <summary>How many times the layer table was read, for the tests of the cost of adding layers.</summary>
        internal int LayerTableReadings { get; private set; }

        /// <summary>How many times what pointed at what was erased was seen to, for the tests of the cost of reading after erasing.</summary>
        internal int Cleanings { get; private set; }

        private List<DxfReader.Record> LayerRecords()
        {
            var layers = new List<DxfReader.Record>();

            foreach (Section section in _sections)
            {
                if (section.Name == "TABLES")
                {
                    foreach (Rec record in Current(section.First + 1, section.Last))
                    {
                        if (record.Type == "LAYER")
                        {
                            layers.Add(Record(record));
                        }
                    }
                }
            }

            return layers;
        }

        /// <summary>
        /// Reads the variables of the header: in a HEADER section, each group 9 names one and the groups after it are its,
        /// a group 0 or a comment left out, as the reader reads them; the version and code page as the reader takes them, the
        /// last given.
        /// </summary>
        private void ReadHeader()
        {
            var variables = new SortedDictionary<string, IReadOnlyList<DxfTypedValue>>(StringComparer.Ordinal);
            List<DxfTypedValue> current = null;
            string variable = null;
            string version = null;
            string named = null;
            bool first = true;

            foreach (Section section in _sections)
            {
                if (section.Name != "HEADER")
                {
                    continue;
                }

                first = true;

                foreach (Rec record in Current(section.First, section.Last))
                {
                    EachGroup(record, (code, start, end) =>
                    {
                        // The name of the section is the first group of its SECTION record.
                        if (first)
                        {
                            first = false;
                            return;
                        }

                        string value = Text(record, start, end);

                        if (code == 9)
                        {
                            // A variable named again is as last given, as the reader reads the version and the code page.
                            variable = value.Trim();
                            current = new List<DxfTypedValue>();
                            variables[variable] = current.AsReadOnly();
                            return;
                        }

                        current?.Add(DxfTypedValue.Read(code, value));

                        if (variable == "$ACADVER" && code == 1)
                        {
                            version = value.Trim();
                        }
                        else if (variable == "$DWGCODEPAGE" && code == 3)
                        {
                            named = value.Trim();
                        }
                    });
                }
            }

            _header = new ReadOnlyDictionary<string, IReadOnlyList<DxfTypedValue>>(variables);

            if (CodePage == null)
            {
                Version = version;
                _namedPage = named;
                CodePage = _decoding.CodePageFor(Version, named);
            }
        }

        /// <summary>The entities of model space and of paper space.</summary>
        private sealed class Drawing
        {
            internal DxfSpace Model { get; set; }

            internal DxfSpace Paper { get; set; }
        }

        private Drawing ReadDrawing()
        {
            var model = new List<DxfItem>();
            var paper = new List<DxfItem>();

            foreach (Section section in _sections)
            {
                if (section.Name != "ENTITIES")
                {
                    continue;
                }

                foreach (DxfItem item in Items(section.First + 1, section.Last, null))
                {
                    if (InPaperSpace(Original(item.First)))
                    {
                        item.IsPaper = true;
                        paper.Add(item);
                    }
                    else
                    {
                        model.Add(item);
                    }
                }
            }

            return new Drawing { Model = new DxfSpace(this, model), Paper = new DxfSpace(this, paper, DxfSpaceKind.Paper) };
        }

        /// <summary>Whether a record is in paper space: its first group 67 is 1, as the reader reads it.</summary>
        private bool InPaperSpace(Rec record)
        {
            string value = null;
            var walk = new GroupWalk(record);

            while (value == null && walk.Next(out int code, out int start, out int end))
            {
                if (code == 67)
                {
                    value = Text(record, start, end);
                }
            }

            return value != null && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int space) && space == 1;
        }

        private IReadOnlyDictionary<string, DxfFileBlock> ReadBlocks()
        {
            var blocks = new SortedDictionary<string, DxfFileBlock>(StringComparer.OrdinalIgnoreCase);

            foreach (Section section in _sections)
            {
                if (section.Name != "BLOCKS")
                {
                    continue;
                }

                int i = section.First + 1;

                while (i < section.Last)
                {
                    if (_types[i] != "BLOCK")
                    {
                        i++;
                        continue;
                    }

                    DxfReader.Record header = Record(Original(i));
                    string name = DxfReader.BlockNameOf(header);
                    int end = i + 1;

                    while (end < section.Last && _types[end] != "ENDBLK" && _types[end] != "BLOCK")
                    {
                        end++;
                    }

                    if (!blocks.ContainsKey(name))
                    {
                        List<DxfItem> items = Items(i + 1, end, name);
                        DxfReader.BlockOf(header, out GeoPoint3 basePoint, out int flags);
                        blocks.Add(name, new DxfFileBlock(this, name, basePoint, flags, items, new DxfItem(this, i, 1, "BLOCK", null), end));
                    }

                    i = end;
                }
            }

            return new ReadOnlyDictionary<string, DxfFileBlock>(blocks);
        }

        /// <summary>
        /// The entities of records, each gathered with those that belong to it as the reader gathers them: a POLYLINE with the
        /// VERTEX records after it and the SEQEND that ends them, and an INSERT with the ATTRIB records after it and their
        /// SEQEND.
        /// </summary>
        private List<DxfItem> Items(int first, int end, string block)
        {
            var items = new List<DxfItem>();
            int i = first;

            while (i < end)
            {
                string type = _types[i];
                int next = i + 1;
                string part = type == "POLYLINE" ? "VERTEX" : type == "INSERT" ? "ATTRIB" : null;

                if (part != null)
                {
                    while (next < end && _types[next] == part)
                    {
                        next++;
                    }

                    if (next < end && _types[next] == "SEQEND")
                    {
                        next++;
                    }
                }

                items.Add(new DxfItem(this, i, next - i, type, block));
                i = next;
            }

            return items;
        }

        #endregion

        #region The walk

        /// <summary>
        /// The records of a file: where each begins, at the line of its group 0, and its type, and the sections they are in;
        /// the file checked as <see cref="DxfReader"/> checks it, with the same messages.
        /// </summary>
        private sealed class Walk
        {
            private readonly DxfFile _file;
            private readonly Dictionary<string, string> _words = new Dictionary<string, string>(StringComparer.Ordinal);
            private int _at;
            private int _line;

            internal Walk(DxfFile file)
            {
                _file = file;
                _at = file._decoding.Start;
                Run();
            }

            internal List<int> Starts { get; } = new List<int>();

            internal List<string> Types { get; } = new List<string>();

            internal List<Section> Sections { get; } = new List<Section>();

            private void Run()
            {
                string section = null;
                int sectionLine = 0;
                int sectionFirst = 0;
                bool entities = false;
                bool ended = false;

                while (Next(out int code, out int codeStart, out int valueStart, out int valueEnd, out int line))
                {
                    if (code != 0)
                    {
                        continue;
                    }

                    string word = Word(valueStart, valueEnd);

                    if (word == "EOF")
                    {
                        if (section != null)
                        {
                            throw new FormatException("The DXF is cut short: the section " + section + " opened on line " + Number(sectionLine) + " has no ENDSEC before the EOF on line " + Number(line) + ".");
                        }

                        Add(codeStart, word);
                        ended = true;
                        break;
                    }

                    if (word == "SECTION")
                    {
                        if (section != null)
                        {
                            throw new FormatException("This is not a DXF that can be read: a SECTION opens on line " + Number(line) + " inside the section " + section + " opened on line " + Number(sectionLine) + ", which has no ENDSEC.");
                        }

                        Add(codeStart, word);

                        if (!Next(out int nameCode, out _, out int nameStart, out int nameEnd, out int nameLine, true) || nameCode != 2)
                        {
                            throw new FormatException("This is not a DXF that can be read: the SECTION on line " + Number(line) + " is not named by a group 2 on line " + Number(nameLine) + ".");
                        }

                        section = _file.Text(nameStart, nameEnd).Trim();
                        sectionLine = line;
                        sectionFirst = Starts.Count - 1;
                        entities |= section == "ENTITIES";
                        continue;
                    }

                    if (word == "ENDSEC")
                    {
                        if (section == null)
                        {
                            throw new FormatException("This is not a DXF that can be read: the ENDSEC on line " + Number(line) + " closes no section.");
                        }

                        Add(codeStart, word);
                        Sections.Add(new Section(section, sectionFirst, Starts.Count - 1));
                        section = null;
                        continue;
                    }

                    if (section == null)
                    {
                        throw new FormatException("This is not a DXF that can be read: the " + word + " on line " + Number(line) + " stands in no section.");
                    }

                    Add(codeStart, word);
                }

                if (!ended)
                {
                    throw new FormatException("The DXF is cut short: it ends on line " + Number(_line) + " without the EOF that closes a DXF" + (section != null ? ", inside the section " + section + " opened on line " + Number(sectionLine) : string.Empty) + ".");
                }

                if (!entities)
                {
                    throw new FormatException("This is not a DXF that can be read: it has no ENTITIES section.");
                }
            }

            private void Add(int start, string type)
            {
                Starts.Add(start);
                Types.Add(type);
            }

            /// <summary>The value of a group 0, trimmed as the reader trims it, kept once for each word.</summary>
            private string Word(int start, int end)
            {
                string word = _file.Text(start, end).Trim();

                if (_words.TryGetValue(word, out string kept))
                {
                    return kept;
                }

                _words.Add(word, word);
                return word;
            }

            /// <summary>
            /// Reads the next pair, a comment (999) passed over unless asked for, as the reader reads it; false at the end of
            /// the file. The line is that of the code.
            /// </summary>
            private bool Next(out int code, out int codeStart, out int valueStart, out int valueEnd, out int line, bool comments = false)
            {
                while (true)
                {
                    code = 0;
                    codeStart = _at;
                    valueStart = 0;
                    valueEnd = 0;
                    line = _line + 1;

                    if (_at >= _file._length)
                    {
                        return false;
                    }

                    int codeEnd = _file.LineEnd(_at, out _at);
                    _line++;

                    if (!TryCode(_file._bytes, codeStart, codeEnd, out code))
                    {
                        string text = _file.Text(codeStart, codeEnd);
                        string said = text.Substring(0, Math.Min(text.Length, 24)) + (text.Length > 24 ? "..." : string.Empty);
                        throw new FormatException("This is not a DXF that can be read: line " + Number(line) + " should be a group code, a whole number, and is '" + said + "'.");
                    }

                    if (_at >= _file._length)
                    {
                        throw new FormatException("The DXF is cut short: the group code " + Number(code) + " on line " + Number(line) + ", the last, has no value after it.");
                    }

                    valueStart = _at;
                    valueEnd = _file.LineEnd(_at, out _at);
                    _line++;

                    if (code != 999 || comments)
                    {
                        return true;
                    }
                }
            }
        }

        /// <summary>The end of the line from a byte, a carriage return before its line feed left out, and where the next begins.</summary>
        private int LineEnd(int at, out int next) => LineEnd(_bytes, at, _length, out next);

        /// <summary>The end of a line of bytes that stop at a point, as <see cref="LineEnd(int, out int)"/> finds it.</summary>
        private static int LineEnd(byte[] bytes, int at, int length, out int next)
        {
            int feed = Array.IndexOf(bytes, (byte)'\n', at, length - at);
            int end = feed < 0 ? length : feed;
            next = feed < 0 ? length : feed + 1;
            return end > at && bytes[end - 1] == '\r' ? end - 1 : end;
        }

        /// <summary>A group code: a whole number of up to nine digits, a sign before it, padded with spaces or tabs, as the reader reads it.</summary>
        private static bool TryCode(byte[] bytes, int start, int end, out int code)
        {
            code = 0;

            while (start < end && (bytes[start] == ' ' || bytes[start] == '\t'))
            {
                start++;
            }

            while (end > start && (bytes[end - 1] == ' ' || bytes[end - 1] == '\t'))
            {
                end--;
            }

            bool negative = start < end && bytes[start] == '-';

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
                byte c = bytes[i];

                if (c < '0' || c > '9')
                {
                    return false;
                }

                code = code * 10 + (c - '0');
            }

            code = negative ? -code : code;
            return true;
        }

        private static bool StartsWith(byte[] bytes, int start, int length, string mark)
        {
            if (length - start < mark.Length)
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

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        #endregion
    }
}
