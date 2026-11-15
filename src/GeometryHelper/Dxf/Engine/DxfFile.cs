using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
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
        private IReadOnlyList<KeyValuePair<string, IReadOnlyList<DxfTypedValue>>> _headerInOrder;
        private string _namedPage;

        private DxfFile(
            byte[] bytes,
            int length,
            string source,
            DxfReader.TextDecoding decoding = null,
            CancellationToken stop = default
        )
        {
            if (StartsWith(bytes, 0, length, "AutoCAD Binary DXF"))
            {
                throw Binary();
            }

            var warnings = new List<string>();
            _bytes = bytes;
            _length = length;
            _decoding = decoding ?? DxfReader.TextDecoding.Of(bytes, length, warnings);
            Warnings = warnings.AsReadOnly();

            if (StartsWith(bytes, _decoding.Start, length, "AutoCAD Binary DXF"))
            {
                throw Binary();
            }

            var walk = new Walk(this, stop);

            // A face of a polyface mesh with AcDbVertex before its AcDbFaceRecord, as GeometryHelper 13.0.0 and 13.1.0 wrote
            // one, which AutoCAD 2021 refuses ("Class separator for class AcDbFaceRecord expected"), is read with its own
            // marker alone, as AutoCAD writes it, so that the file is saved so.
            if (walk.Extra.Count > 0)
            {
                _bytes = Without(bytes, length, walk.Extra, out _length);
                walk = new Walk(this, stop);
            }

            _starts = walk.Starts.ToArray();
            _types = walk.Types.ToArray();
            _sections = walk.Sections.ToArray();
            _walkedHandle = walk.MostHandle;
            _walkedPointed = walk.MostPointed;
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

            GeometryHelperLog.Info(
                "DxfFile: "
                    + Number(_starts.Length)
                    + " records opened from "
                    + source
                    + ", "
                    + Number(length)
                    + " bytes, version "
                    + (Version ?? "not given")
                    + (warnings.Count > 0 ? ": " + string.Join(" ", warnings) : ".")
            );
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
        public static DxfFile Open(string path) => Open(path, CancellationToken.None);

        /// <summary>
        /// Opens a DXF file from a path as <see cref="Open(string)"/> does, the walk over its records looking at a token every
        /// 4 096 records and giving the file up when it is stopped.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="stop">What stops the opening.</param>
        /// <returns>The file, as <see cref="Open(string)"/> gives it.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the token is stopped before the walk is done.</exception>
        internal static DxfFile Open(string path, CancellationToken stop)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            byte[] bytes = File.ReadAllBytes(path);
            return new DxfFile(bytes, bytes.Length, "'" + Path.GetFileName(path) + "'", null, stop);
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

        /// <summary>Opens a DXF file from bytes it then holds as its own, as <see cref="Open(Stream)"/> holds its copy of a stream.</summary>
        /// <param name="bytes">The bytes, not to be changed after.</param>
        /// <param name="length">How many of them the file has.</param>
        /// <returns>The file.</returns>
        internal static DxfFile Open(byte[] bytes, int length) => Open(bytes, length, CancellationToken.None);

        /// <summary>
        /// Opens a DXF file from bytes as <see cref="Open(byte[], int)"/> does, the walk over its records looking at a token every
        /// 4 096 records and giving the file up when it is stopped.
        /// </summary>
        /// <param name="bytes">The bytes, not to be changed after.</param>
        /// <param name="length">How many of them the file has.</param>
        /// <param name="stop">What stops the opening.</param>
        /// <returns>The file.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the token is stopped before the walk is done.</exception>
        internal static DxfFile Open(byte[] bytes, int length, CancellationToken stop) =>
            new DxfFile(bytes, length, "a stream", null, stop);

        /// <summary>
        /// Opens a DXF file from its text.
        /// </summary>
        /// <param name="text">The text of the file, as <see cref="ToText"/> or <see cref="DxfWriter.ToString"/> gives it.</param>
        /// <returns>The file, its text kept as the bytes AutoCAD would have saved it in.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="FormatException">Thrown when the text is not a DXF, is binary, or is cut short; the message names the line.</exception>
        /// <remarks>
        /// A text all ASCII, as <see cref="DxfWriter"/> writes one, or one of R2007 or later, or one that begins with a byte
        /// order mark, is kept as UTF-8, the mark as the mark of UTF-8, so <see cref="ToText"/> gives it back, the mark left out
        /// as it leaves out that of a file, and <see cref="Save(string)"/> writes it so. One of an earlier version with letters
        /// beyond ASCII is kept in the code page its header names (<c>$DWGCODEPAGE</c>, ANSI_1252 where it names none), as
        /// AutoCAD reads it, each letter the page lacks as its escape, and every letter beyond ASCII as its escape where the
        /// runtime has not the page: the text of R2004 of an MTEXT café Đ, its page ANSI_1252, is kept as caf, the byte E9
        /// and <c>\U+0110</c>, which every reader reads as café Đ, and <see cref="ToText"/> gives it with the escape.
        /// </remarks>
        public static DxfFile Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            byte[] bytes = DxfReader.TextDecoding.OfText(text, out DxfReader.TextDecoding decoding);
            return new DxfFile(bytes, bytes.Length, "a text", decoding);
        }

        /// <summary>
        /// Opens a DXF file from the bytes <see cref="Parse(string)"/> keeps of a text that is all ASCII or of R2007 or later: its
        /// UTF-8, which the file then holds as its own.
        /// </summary>
        /// <param name="bytes">The bytes, not to be changed after.</param>
        /// <param name="length">How many of them the file has.</param>
        /// <returns>The file.</returns>
        internal static DxfFile OpenText(byte[] bytes, int length) => new DxfFile(bytes, length, "a text");

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

            return item.Piece != null
                ? new List<Rec>(item.Piece.Records)
                : Current(item.First, item.First + item.Count);
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

            return _slots?[item.First] == null
                ? Original(item.First + index)
                : Current(item.First, item.First + item.Count)[index];
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

        /// <summary>
        /// The look of an entity as its record writes it, each group the first of its code, bar its handle, which every entity
        /// has of its own: the entities of a drawing have a few looks between them, and share the object of each.
        /// </summary>
        internal sealed class Look : IEquatable<Look>
        {
            internal string Layer { get; set; }

            internal int? Colour { get; set; }

            internal int? TrueColour { get; set; }

            internal string Linetype { get; set; }

            internal int? Lineweight { get; set; }

            internal double Thickness { get; set; }

            public bool Equals(Look other) =>
                other != null
                && string.Equals(Layer, other.Layer, StringComparison.Ordinal)
                && Colour == other.Colour
                && TrueColour == other.TrueColour
                && string.Equals(Linetype, other.Linetype, StringComparison.Ordinal)
                && Lineweight == other.Lineweight
                && BitConverter.DoubleToInt64Bits(Thickness) == BitConverter.DoubleToInt64Bits(other.Thickness);

            public override bool Equals(object obj) => Equals(obj as Look);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Layer == null ? 0 : StringComparer.Ordinal.GetHashCode(Layer);
                    hash = hash * 31 + Colour.GetHashCode();
                    hash = hash * 31 + TrueColour.GetHashCode();
                    hash = hash * 31 + (Linetype == null ? 0 : StringComparer.Ordinal.GetHashCode(Linetype));
                    hash = hash * 31 + Lineweight.GetHashCode();
                    return hash * 31 + Thickness.GetHashCode();
                }
            }
        }

        /// <summary>The most looks the file shares; a look beyond them is the entity's own, so that a drawing whose every entity has a look of its own costs no table.</summary>
        private const int MostSharedLooks = 4096;

        private readonly ConcurrentDictionary<Look, Look> _looks = new ConcurrentDictionary<Look, Look>();

        /// <summary>The look the file shares for this one: the same object for every entity of the same look, as long as the file has room for another.</summary>
        internal Look Shared(Look look)
        {
            TryShared(look, out Look shared);
            return shared;
        }

        /// <summary>The look the file shares for this one, and whether it is shared: false where the file had no room for another, and the look is the entity's own.</summary>
        private bool TryShared(Look look, out Look shared)
        {
            if (_looks.TryGetValue(look, out shared))
            {
                return true;
            }

            if (_looks.Count >= MostSharedLooks)
            {
                shared = look;
                return false;
            }

            shared = _looks.GetOrAdd(look, look);
            return true;
        }

        /// <summary>
        /// The look of an entity as the file shares it, and its handle: found from the bytes of the groups that make a look
        /// (layer, linetype, thickness, colour, lineweight, true colour, each the first of its code) among those of looks the
        /// file shared before, so that an entity of a look met already makes no string of them and no <see cref="Look"/>, as
        /// the million entities of a drawing with a few looks did. A look not among them is read from the record's groups
        /// and shared as before; the handle is the one string made. The same walk over the record settles whether the entity is
        /// hidden, as <see cref="IsHidden"/> reads it, which every entity drawn is asked, so that the record is walked once and
        /// not twice.
        /// </summary>
        internal Look LookOf(DxfItem item, out string handle) => LookOf(item, out handle, out _);

        /// <inheritdoc cref="LookOf(DxfItem, out string)"/>
        internal Look LookOf(DxfItem item, out string handle, out bool hidden)
        {
            Rec record = HeadOf(item);
            var spans = new LookSpans(-1);
            int handleStart = -1,
                handleEnd = 0,
                dimStart = -1,
                dimEnd = 0;
            var walk = new GroupWalk(record);

            // The visibility as IsHidden reads it: from the first group 60 of the part all entities have, outside the groups of
            // the applications, before the first subclass marker other than AcDbEntity and the extended data; -1 while none of
            // those is met, 1 for an entity that is shown and 0 for one that is hidden.
            int shown = -1;
            bool braced = false;

            while (walk.Next(out int code, out int start, out int end))
            {
                if (shown < 0)
                {
                    if (code == 1001)
                    {
                        shown = 1;
                    }
                    else if (code == 102)
                    {
                        braced = StartsWithBrace(record, start, end);
                    }
                    else if (!braced)
                    {
                        if (code == 100 && !ValueIs(record, new Pair(code, 0, start, end, 0), "AcDbEntity"))
                        {
                            shown = 1;
                        }
                        else if (code == 60)
                        {
                            shown =
                                (
                                    DxfFastNumber.TryWhole(record.Bytes, start, end, out int off)
                                    || int.TryParse(
                                        Text(record, start, end).Trim(),
                                        NumberStyles.Integer,
                                        CultureInfo.InvariantCulture,
                                        out off
                                    )
                                )
                                && off == 1
                                    ? 0
                                    : 1;
                        }
                    }
                }

                switch (code)
                {
                    case 5:
                        if (handleStart < 0)
                        {
                            handleStart = start;
                            handleEnd = end;
                        }

                        break;
                    case 105:
                        if (dimStart < 0)
                        {
                            dimStart = start;
                            dimEnd = end;
                        }

                        break;
                    case 8:
                        spans.SetFirst(ref spans.Layer, start, end);
                        break;
                    case 6:
                        spans.SetFirst(ref spans.Linetype, start, end);
                        break;
                    case 39:
                        spans.SetFirst(ref spans.Thickness, start, end);
                        break;
                    case 62:
                        spans.SetFirst(ref spans.Colour, start, end);
                        break;
                    case 370:
                        spans.SetFirst(ref spans.Lineweight, start, end);
                        break;
                    case 420:
                        spans.SetFirst(ref spans.TrueColour, start, end);
                        break;
                }
            }

            // A DIMSTYLE gives its handle as 105: its 5 is the name of its arrow block in a file of R12.
            bool dimStyle = record.Type == "DIMSTYLE";
            int from = dimStyle ? dimStart : handleStart;
            string text = from < 0 ? null : Text(record, from, dimStyle ? dimEnd : handleEnd).Trim();
            handle = string.IsNullOrEmpty(text) ? null : text;
            hidden = shown == 0;
            byte[] bytes = record.Bytes;
            int hash = spans.Hash(bytes);
            int slot = hash & (LookSlots - 1);
            LookEntry entry = Volatile.Read(ref _lookEntries[slot]);

            if (entry != null && entry.Hash == hash && spans.Matches(bytes, entry.Key))
            {
                return entry.Look;
            }

            Look look = LookFrom(record, spans);

            if (!TryShared(look, out Look shared))
            {
                return look;
            }

            Volatile.Write(ref _lookEntries[slot], new LookEntry(hash, spans.KeyOf(bytes), shared));
            return shared;
        }

        /// <summary>How many looks <see cref="LookOf(DxfItem, out string, out bool)"/> remembers by their bytes, found by the hash of them: a few more than the layers and colours of a usual drawing.</summary>
        private const int LookSlots = 64;

        private readonly LookEntry[] _lookEntries = new LookEntry[LookSlots];

        /// <summary>A look shared by the file with the bytes of the groups it was read from, kept for the next entity whose groups are the same bytes.</summary>
        private sealed class LookEntry
        {
            internal readonly int Hash;
            internal readonly byte[] Key;
            internal readonly Look Look;

            internal LookEntry(int hash, byte[] key, Look look)
            {
                Hash = hash;
                Key = key;
                Look = look;
            }
        }

        /// <summary>
        /// Where in the bytes of a record the groups of a look stand: the start and end of each value, the start -1 where the
        /// record has none, which is not an empty value.
        /// </summary>
        private struct LookSpans
        {
            internal Slice Layer,
                Linetype,
                Thickness,
                Colour,
                Lineweight,
                TrueColour;

            internal LookSpans(int none)
            {
                Layer = Linetype = Thickness = Colour = Lineweight = TrueColour = new Slice(none, 0);
            }

            internal struct Slice
            {
                internal int Start;
                internal int End;

                internal Slice(int start, int end)
                {
                    Start = start;
                    End = end;
                }

                internal int Length => Start < 0 ? 0 : End - Start;
            }

            internal void SetFirst(ref Slice span, int start, int end)
            {
                if (span.Start < 0)
                {
                    span = new Slice(start, end);
                }
            }

            private static int Mix(int hash, byte[] bytes, Slice span)
            {
                unchecked
                {
                    if (span.Start < 0)
                    {
                        return (hash ^ 0x5bd1e995) * 16777619;
                    }

                    hash = (hash ^ (span.End - span.Start)) * 16777619;

                    for (int i = span.Start; i < span.End; i++)
                    {
                        hash = (hash ^ bytes[i]) * 16777619;
                    }

                    return hash;
                }
            }

            internal int Hash(byte[] bytes)
            {
                unchecked
                {
                    int hash = (int)2166136261;
                    hash = Mix(hash, bytes, Layer);
                    hash = Mix(hash, bytes, Linetype);
                    hash = Mix(hash, bytes, Thickness);
                    hash = Mix(hash, bytes, Colour);
                    hash = Mix(hash, bytes, Lineweight);
                    return Mix(hash, bytes, TrueColour);
                }
            }

            private static bool Same(byte[] bytes, Slice span, byte[] key, ref int at)
            {
                int length = key[at] | (key[at + 1] << 8) | (key[at + 2] << 16) | (key[at + 3] << 24);
                at += 4;

                if (span.Start < 0)
                {
                    return length == -1;
                }

                if (length != span.End - span.Start)
                {
                    return false;
                }

                for (int i = 0; i < length; i++)
                {
                    if (key[at + i] != bytes[span.Start + i])
                    {
                        return false;
                    }
                }

                at += length;
                return true;
            }

            internal bool Matches(byte[] bytes, byte[] key)
            {
                int at = 0;
                return Same(bytes, Layer, key, ref at)
                    && Same(bytes, Linetype, key, ref at)
                    && Same(bytes, Thickness, key, ref at)
                    && Same(bytes, Colour, key, ref at)
                    && Same(bytes, Lineweight, key, ref at)
                    && Same(bytes, TrueColour, key, ref at);
            }

            private static void Put(byte[] key, Slice span, byte[] bytes, ref int at)
            {
                int length = span.Start < 0 ? -1 : span.End - span.Start;
                key[at] = (byte)length;
                key[at + 1] = (byte)(length >> 8);
                key[at + 2] = (byte)(length >> 16);
                key[at + 3] = (byte)(length >> 24);
                at += 4;

                if (length > 0)
                {
                    Buffer.BlockCopy(bytes, span.Start, key, at, length);
                    at += length;
                }
            }

            internal byte[] KeyOf(byte[] bytes)
            {
                var key = new byte[
                    24
                        + Layer.Length
                        + Linetype.Length
                        + Thickness.Length
                        + Colour.Length
                        + Lineweight.Length
                        + TrueColour.Length
                ];
                int at = 0;
                Put(key, Layer, bytes, ref at);
                Put(key, Linetype, bytes, ref at);
                Put(key, Thickness, bytes, ref at);
                Put(key, Colour, bytes, ref at);
                Put(key, Lineweight, bytes, ref at);
                Put(key, TrueColour, bytes, ref at);
                return key;
            }
        }

        /// <summary>The look of an entity made of the groups of its record that <see cref="LookOf(DxfItem, out string, out bool)"/> found, as strings, not yet shared.</summary>
        private Look LookFrom(Rec record, LookSpans spans)
        {
            // Each group the first of its code in the record, kept as it stands; null where the record has none.
            string TextOf(LookSpans.Slice slice) => slice.Start < 0 ? null : Text(record, slice.Start, slice.End);

            int? Whole(string value) =>
                value != null
                && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                    ? number
                    : (int?)null;

            string thickness = TextOf(spans.Thickness);
            double raised =
                thickness != null && Numbers.TryRead(thickness, out double read) && Guard.IsFinite(read) ? read : 0.0;

            return new Look
            {
                Layer = TextOf(spans.Layer),
                Colour = Whole(TextOf(spans.Colour)),
                TrueColour = Whole(TextOf(spans.TrueColour)),
                Linetype = TextOf(spans.Linetype),
                Lineweight = Whole(TextOf(spans.Lineweight)),
                Thickness = raised,
            };
        }

        internal List<object> ShapesOf(DxfItem item)
        {
            if (item.Type == "INSERT")
            {
                Rec? head = InsertRecord(item);
                DxfInsert insert = head == null ? null : DxfReader.InsertOf(Record(head.Value), () => Placed(item));
                return insert == null ? new List<object>() : new List<object> { insert };
            }

            return TryFastShape(item, out object fast)
                ? new List<object>(1) { fast }
                : DxfReader.ShapesOf(Records(item), Tolerance.Default);
        }

        /// <summary>
        /// The one shape of a LINE, ARC, CIRCLE or LWPOLYLINE read straight from the bytes of its record, as the reader reads
        /// it from the groups made strings, to the bit; false for any other entity, and where the fast read is not sure of
        /// following the reader, which <see cref="ShapesOf(DxfItem)"/> then asks.
        /// </summary>
        internal bool TryFastShape(DxfItem item, out object shape)
        {
            Rec own = HeadOf(item);
            return DxfReader.TryFastShape(
                own.Bytes,
                own.Start,
                own.End,
                own.Type,
                Tolerance.Default,
                _decoding,
                out shape
            );
        }

        /// <summary>
        /// The shapes of an entity other than an INSERT read the long way, from the groups made strings, as
        /// <see cref="ShapesOf(DxfItem)"/> reads those it has no fast read for: what that is checked against.
        /// </summary>
        internal List<object> ShapesReadLong(DxfItem item) => DxfReader.ShapesOf(Records(item), Tolerance.Default);

        /// <summary>
        /// The shapes of an entity other than an INSERT as a typed conversion of <see cref="DxfEntity"/> reads them: at a
        /// tolerance, of the plane where asked and the entity lies on the floor, and a polyline as the kind asked, as
        /// <see cref="DxfReader.ShapesOf(DxfReader.Record, Tolerance, bool, bool?, bool, out string)"/> reads them; the reason
        /// where there are none.
        /// </summary>
        internal List<object> ShapesOf(
            DxfItem item,
            Tolerance tolerance,
            bool planAs2D,
            bool? loop,
            bool arcs,
            out string why
        ) => DxfReader.ShapesOf(Records(item), tolerance, planAs2D, loop, arcs, out why);

        /// <summary>
        /// The record of an INSERT as it is placed: a copy in no file names the block it was copied with by that block's name
        /// now, so that a block renamed in the drawing the copy came from is still the one it places; null for a copy whose
        /// block was erased there since, which places nothing.
        /// </summary>
        private Rec? InsertRecord(DxfItem item)
        {
            Rec head = HeadOf(item);
            DxfFileBlock block = item.OriginBlock;

            if (block == null)
            {
                return head;
            }

            if (block.Head.IsRemoved)
            {
                return null;
            }

            return
                WithGroup(head, 2, block.Name, false, out Rec named)
                && !string.Equals(
                    DxfReader.NameOf(ValueOf(item, 2, true) ?? string.Empty),
                    block.Name,
                    StringComparison.Ordinal
                )
                ? named
                : head;
        }

        /// <summary>What the reader places of an INSERT, with the layer table and the blocks of the file.</summary>
        private DxfReader.Placing Placed(DxfItem insert)
        {
            // A copy in no file places its block as the drawing it was copied from has it now, edits and all: by that
            // drawing's placing, not by one of the bytes it was read from.
            DxfFile placer = _detached && _origin != null && _origin != Template ? _origin : this;
            DxfReader.Placing placing = placer._placer.Value.Place(
                Record(InsertRecord(insert) ?? HeadOf(insert)),
                insert.Block
            );

            // One line for the placing, outside the lock: Warn where an entity threw, Debug for what was left out as expected.
            if (placing.Warnings.Count > 0)
            {
                string line =
                    "DxfFile: the INSERT "
                    + (insert.Handle ?? "with no handle")
                    + (insert.Block != null ? " in the block " + insert.Block : string.Empty)
                    + " placed "
                    + Number(placing.Entities.Count)
                    + " entities: "
                    + string.Join(" ", placing.Warnings);

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
                    drawingLayers.Add(LayerOf(HeadOf(item)));
                }
            }

            var blocks = new List<List<DxfReader.Record>>();

            foreach (Section section in _sections)
            {
                if (section.Name == "BLOCKS")
                {
                    blocks.Add(Inside(section).ConvertAll(r => Record(r)));
                }
            }

            return new DxfReader.Placer(LayerRecords(), drawingLayers, blocks, Tolerance.Default);
        }

        /// <summary>
        /// The layer of an entity as the reader names it, <see cref="DxfReader.LayerOf(DxfReader.Record)"/> of its record, read from
        /// the first group 8 in the bytes with no record made of the rest.
        /// </summary>
        private string LayerOf(Rec head)
        {
            var walk = new GroupWalk(head);

            while (walk.Next(out int code, out int start, out int end))
            {
                if (code == 8)
                {
                    return DxfReader.LayerOf(Text(head, start, end));
                }
            }

            return DxfReader.LayerOf((string)null);
        }

        private Lazy<IReadOnlyList<DxfLayer>> NewLayers() =>
            new Lazy<IReadOnlyList<DxfLayer>>(() =>
            {
                LayerTableReadings++;
                return Array.AsReadOnly(DxfReader.LayerTableOf(LayerRecords()));
            });

        /// <summary>How many times the layer table was read, for the tests of the cost of adding layers.</summary>
        internal int LayerTableReadings { get; private set; }

        /// <summary>How many times what pointed at what was erased was seen to, for the tests of the cost of reading after erasing.</summary>
        internal int Cleanings { get; private set; }

        /// <summary>
        /// How many times the records of a section were read whole, for what points at a handle or for the root dictionary,
        /// for the tests of the cost of erasing fields. Counted one by one, as threads copying from the drawing at once read its
        /// root dictionary.
        /// </summary>
        internal int WholeReadings => System.Threading.Volatile.Read(ref _wholeReadings);

        private int _wholeReadings;

        /// <summary>
        /// The handles of the tables and styles of a drawing being completed that wait to be listed in its ACDB_RECOMPOSE_DATA
        /// (<see cref="DxfTable.EndRecompose"/>); null at any other time.
        /// </summary>
        internal List<string> RecomposeBatch { get; set; }

        private List<DxfReader.Record> LayerRecords()
        {
            var layers = new List<DxfReader.Record>();

            foreach (Section section in _sections)
            {
                if (section.Name == "TABLES")
                {
                    foreach (Rec record in Inside(section))
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
            var order = new List<KeyValuePair<string, IReadOnlyList<DxfTypedValue>>>();
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
                            order.Add(
                                new KeyValuePair<string, IReadOnlyList<DxfTypedValue>>(variable, current.AsReadOnly())
                            );
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
            _headerInOrder = order.AsReadOnly();

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

        private Drawing _drawingAhead;

        private Drawing ReadDrawing() => _drawingAhead ?? ReadDrawing(CancellationToken.None);

        /// <summary>
        /// Makes the entities of model space and paper space now, as the first of <see cref="ModelSpace"/> or
        /// <see cref="PaperSpace"/> makes them, looking at a token every 4 096 records and giving up when it is stopped: for the
        /// presenter, which asks it of a drawing it opens on a worker thread that Esc stops, a pass that takes 0.35 s at
        /// 1 000 000 entities. A drawing given up is not kept, and what is asked of the file after it is made as before.
        /// </summary>
        /// <exception cref="OperationCanceledException">Thrown when the token is stopped before the pass is done.</exception>
        internal void ReadDrawingAhead(CancellationToken stop)
        {
            if (_drawingAhead == null && !_drawing.IsValueCreated)
            {
                _drawingAhead = ReadDrawing(stop);
            }
        }

        private Drawing ReadDrawing(CancellationToken stop)
        {
            var model = new List<DxfItem>();
            var paper = new List<DxfItem>();

            foreach (Section section in _sections)
            {
                if (section.Name != "ENTITIES")
                {
                    continue;
                }

                foreach (DxfItem item in Items(section.First + 1, section.Last, null, stop))
                {
                    if (((model.Count + paper.Count) & 4095) == 0)
                    {
                        stop.ThrowIfCancellationRequested();
                    }

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

            return new Drawing
            {
                Model = new DxfSpace(this, model),
                Paper = new DxfSpace(this, paper, DxfSpaceKind.Paper),
            };
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

            return value != null
                && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int space)
                && space == 1;
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
                        blocks.Add(
                            name,
                            new DxfFileBlock(
                                this,
                                name,
                                basePoint,
                                flags,
                                items,
                                new DxfItem(this, i, 1, "BLOCK", null),
                                end
                            )
                        );
                    }

                    i = end;
                }
            }

            _blockIndex = blocks;
            return new ReadOnlyDictionary<string, DxfFileBlock>(blocks);
        }

        /// <summary>
        /// The entities of records, each gathered with those that belong to it as the reader gathers them: a POLYLINE with the
        /// VERTEX records after it and the SEQEND that ends them, and an INSERT with the ATTRIB records after it and their
        /// SEQEND.
        /// </summary>
        private List<DxfItem> Items(int first, int end, string block, CancellationToken stop = default)
        {
            var items = new List<DxfItem>();
            int i = first;

            while (i < end)
            {
                if ((items.Count & 4095) == 0)
                {
                    stop.ThrowIfCancellationRequested();
                }

                string type = _types[i];
                int next = i + 1;
                string part =
                    type == "POLYLINE" ? "VERTEX"
                    : type == "INSERT" ? "ATTRIB"
                    : null;

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
            // The token is looked at every 4 096 records, a record in a few groups of ten: 0.6 ms of the walk at 1 000 000
            // entities, so that the file is given up within a few milliseconds. Counted by record, not by group, the look costs
            // what a timing cannot tell from none.
            private const int StopEvery = 4096 - 1;

            private readonly DxfFile _file;
            private readonly CancellationToken _stop;
            private readonly Dictionary<string, string> _words = new Dictionary<string, string>(StringComparer.Ordinal);
            private int _at;
            private int _line;

            internal Walk(DxfFile file, CancellationToken stop)
            {
                _file = file;
                _stop = stop;
                _at = file._decoding.Start;
                Run();
            }

            internal List<int> Starts { get; } = new List<int>();

            internal List<string> Types { get; } = new List<string>();

            internal List<Section> Sections { get; } = new List<Section>();

            /// <summary>
            /// Where a face of a polyface mesh has the marker AcDbVertex straight before its AcDbFaceRecord: the bytes of that
            /// group, from the line of its code to the line of the next group.
            /// </summary>
            internal List<KeyValuePair<int, int>> Extra { get; } = new List<KeyValuePair<int, int>>();

            /// <summary>
            /// The greatest handle a record outside the HEADER has (its first group 5, or 105 for a DIMSTYLE, whose 5 is the name
            /// of its arrow block in a file of R12), read as hexadecimal: found as the walk goes by the groups, where a pass of
            /// its own over every group of every record took a quarter of the time a drawing of 1 000 000 entities takes to open.
            /// </summary>
            internal ulong MostHandle { get; private set; }

            /// <summary>
            /// The greatest handle a group of a pointer outside the HEADER points to (codes 320 to 369, 390 to 399, 480, 481 and
            /// 1005), though no record has it, so that nothing comes to point at a new entity by chance.
            /// </summary>
            internal ulong MostPointed { get; private set; }

            private void Run()
            {
                string section = null;
                int sectionLine = 0;
                int sectionFirst = 0;
                bool entities = false;
                bool ended = false;
                bool vertex = false;
                int vertexMarker = -1;
                int records = 0;
                bool scanning = false;
                bool handled = true;
                int wanted = 5;
                byte[] bytes = _file._bytes;

                while (Next(out int code, out int codeStart, out int valueStart, out int valueEnd, out int line))
                {
                    if (code != 0)
                    {
                        if (scanning)
                        {
                            if (!handled && (code == wanted || (wanted == 5 && code == 105)))
                            {
                                handled = true;

                                if (TryHex(bytes, valueStart, valueEnd, out ulong handle) && handle > MostHandle)
                                {
                                    MostHandle = handle;
                                }
                            }

                            if (
                                (code >= 320 && code <= 369)
                                || (code >= 390 && code <= 399)
                                || code == 480
                                || code == 481
                                || code == 1005
                            )
                            {
                                if (TryHex(bytes, valueStart, valueEnd, out ulong pointed) && pointed > MostPointed)
                                {
                                    MostPointed = pointed;
                                }
                            }
                        }

                        if (vertex && code == 100)
                        {
                            if (vertexMarker >= 0 && _file.IsWord(valueStart, valueEnd, "AcDbFaceRecord"))
                            {
                                Extra.Add(new KeyValuePair<int, int>(vertexMarker, codeStart));
                            }

                            vertexMarker = _file.IsWord(valueStart, valueEnd, "AcDbVertex") ? codeStart : -1;
                        }
                        else
                        {
                            vertexMarker = -1;
                        }

                        continue;
                    }

                    if ((++records & StopEvery) == 0)
                    {
                        _stop.ThrowIfCancellationRequested();
                    }

                    string word = Word(valueStart, valueEnd);
                    vertex = word == "VERTEX";
                    vertexMarker = -1;
                    handled = false;
                    wanted = word == "DIMSTYLE" ? 105 : 5;

                    if (word == "EOF")
                    {
                        if (section != null)
                        {
                            throw new FormatException(
                                "The DXF is cut short: the section "
                                    + section
                                    + " opened on line "
                                    + Number(sectionLine)
                                    + " has no ENDSEC before the EOF on line "
                                    + Number(line)
                                    + "."
                            );
                        }

                        Add(codeStart, word);
                        ended = true;
                        break;
                    }

                    if (word == "SECTION")
                    {
                        if (section != null)
                        {
                            throw new FormatException(
                                "This is not a DXF that can be read: a SECTION opens on line "
                                    + Number(line)
                                    + " inside the section "
                                    + section
                                    + " opened on line "
                                    + Number(sectionLine)
                                    + ", which has no ENDSEC."
                            );
                        }

                        Add(codeStart, word);

                        if (
                            !Next(out int nameCode, out _, out int nameStart, out int nameEnd, out int nameLine, true)
                            || nameCode != 2
                        )
                        {
                            throw new FormatException(
                                "This is not a DXF that can be read: the SECTION on line "
                                    + Number(line)
                                    + " is not named by a group 2 on line "
                                    + Number(nameLine)
                                    + "."
                            );
                        }

                        section = _file.Text(nameStart, nameEnd).Trim();
                        sectionLine = line;
                        sectionFirst = Starts.Count - 1;
                        entities |= section == "ENTITIES";
                        scanning = section != "HEADER";
                        continue;
                    }

                    if (word == "ENDSEC")
                    {
                        if (section == null)
                        {
                            throw new FormatException(
                                "This is not a DXF that can be read: the ENDSEC on line "
                                    + Number(line)
                                    + " closes no section."
                            );
                        }

                        Add(codeStart, word);
                        Sections.Add(new Section(section, sectionFirst, Starts.Count - 1));
                        section = null;
                        scanning = false;
                        continue;
                    }

                    if (section == null)
                    {
                        throw new FormatException(
                            "This is not a DXF that can be read: the "
                                + word
                                + " on line "
                                + Number(line)
                                + " stands in no section."
                        );
                    }

                    Add(codeStart, word);
                }

                if (!ended)
                {
                    throw new FormatException(
                        "The DXF is cut short: it ends on line "
                            + Number(_line)
                            + " without the EOF that closes a DXF"
                            + (
                                section != null
                                    ? ", inside the section " + section + " opened on line " + Number(sectionLine)
                                    : string.Empty
                            )
                            + "."
                    );
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

            // The words of the last records, one to a slot, by a key of the bytes of a word: a drawing is long runs of a few kinds
            // of record, and the word of each is found again by its bytes, with no string made of them (a string and a lookup
            // for each of the 100 000 records of a drawing took 4 of the 31 ms of its walk). Words of ASCII only, whose bytes
            // are the letters in every code page.
            private readonly string[] _recent = new string[32];

            /// <summary>The value of a group 0, trimmed as the reader trims it, kept once for each word.</summary>
            private string Word(int start, int end)
            {
                byte[] bytes = _file._bytes;
                int from = start;
                int to = end;

                while (from < to && (bytes[from] == (byte)' ' || bytes[from] == (byte)'\t'))
                {
                    from++;
                }

                while (to > from && (bytes[to - 1] == (byte)' ' || bytes[to - 1] == (byte)'\t'))
                {
                    to--;
                }

                int slot = to > from ? (to - from + 31 * bytes[from] + 7 * bytes[to - 1]) & 31 : 0;
                string held = _recent[slot];

                if (held != null && _file.IsWord(from, to, held))
                {
                    return held;
                }

                string word = _file.Text(start, end).Trim();

                if (!_words.TryGetValue(word, out string kept))
                {
                    _words.Add(word, word);
                    kept = word;
                }

                if (IsAscii(kept))
                {
                    _recent[slot] = kept;
                }

                return kept;
            }

            private static bool IsAscii(string word)
            {
                foreach (char c in word)
                {
                    if (c >= 0x80)
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Reads the next pair, a comment (999) passed over unless asked for, as the reader reads it; false at the end of
            /// the file. The line is that of the code.
            /// </summary>
            private bool Next(
                out int code,
                out int codeStart,
                out int valueStart,
                out int valueEnd,
                out int line,
                bool comments = false
            )
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

                    // A code line of the usual shape is read in one pass; any other is read, and refused, as it always was.
                    bool plain = TryPlainCode(_file._bytes, _at, _file._length, out code, out int following);

                    if (plain)
                    {
                        _at = following;
                    }

                    int codeEnd = plain ? 0 : _file.LineEnd(_at, out _at);
                    _line++;

                    if (!plain && !TryCode(_file._bytes, codeStart, codeEnd, out code))
                    {
                        string text = _file.Text(codeStart, codeEnd);
                        string said =
                            text.Substring(0, Math.Min(text.Length, 24)) + (text.Length > 24 ? "..." : string.Empty);
                        throw new FormatException(
                            "This is not a DXF that can be read: line "
                                + Number(line)
                                + " should be a group code, a whole number, and is '"
                                + said
                                + "'."
                        );
                    }

                    if (_at >= _file._length)
                    {
                        throw new FormatException(
                            "The DXF is cut short: the group code "
                                + Number(code)
                                + " on line "
                                + Number(line)
                                + ", the last, has no value after it."
                        );
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

        /// <summary>Whether the bytes from one place to another are a word of ASCII, spaces and tabs about it left out, as <see cref="Walk"/> trims a value.</summary>
        private bool IsWord(int start, int end, string word)
        {
            while (start < end && (_bytes[start] == (byte)' ' || _bytes[start] == (byte)'\t'))
            {
                start++;
            }

            while (end > start && (_bytes[end - 1] == (byte)' ' || _bytes[end - 1] == (byte)'\t'))
            {
                end--;
            }

            if (end - start != word.Length)
            {
                return false;
            }

            for (int i = 0; i < word.Length; i++)
            {
                if (_bytes[start + i] != (byte)word[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The bytes of a file with the runs given, each from where it begins to where it ends, in order and apart, taken out.</summary>
        private static byte[] Without(byte[] bytes, int length, List<KeyValuePair<int, int>> runs, out int left)
        {
            int cut = 0;

            foreach (KeyValuePair<int, int> run in runs)
            {
                cut += run.Value - run.Key;
            }

            left = length - cut;
            var kept = new byte[left];
            int from = 0;
            int to = 0;

            foreach (KeyValuePair<int, int> run in runs)
            {
                Buffer.BlockCopy(bytes, from, kept, to, run.Key - from);
                to += run.Key - from;
                from = run.Value;
            }

            Buffer.BlockCopy(bytes, from, kept, to, length - from);
            return kept;
        }

        /// <summary>The end of the line from a byte, a carriage return before its line feed left out, and where the next begins.</summary>
        private int LineEnd(int at, out int next) => LineEnd(_bytes, at, _length, out next);

        /// <summary>The end of a line of bytes that stop at a point, as <see cref="LineEnd(int, out int)"/> finds it.</summary>
        private static int LineEnd(byte[] bytes, int at, int length, out int next)
        {
            // A line of a record is a code, a number or a name, a few bytes: a plain loop over the first of them finds its end
            // before the vectorised search is set up, which a long line (a text, a block of binary) is left to.
            int feed = at;
            int stop = length - at > 24 ? at + 24 : length;

            while (feed < stop && bytes[feed] != (byte)'\n')
            {
                feed++;
            }

            if (feed == stop)
            {
                feed = stop == length ? -1 : Array.IndexOf(bytes, (byte)'\n', stop, length - stop);
            }

            int end = feed < 0 ? length : feed;
            next = feed < 0 ? length : feed + 1;
            return end > at && bytes[end - 1] == '\r' ? end - 1 : end;
        }

        /// <summary>
        /// A group code line of the usual shape: spaces, one to nine digits, and the end of the line (a line feed, a carriage
        /// return before it allowed), read in one pass where <see cref="LineEnd(byte[], int, int, out int)"/> and
        /// <see cref="TryCode"/> take two, with the same code and the same start of the next line. False for any other line
        /// (a sign, a tab, a longer number, a line that ends the file), which those two read.
        /// </summary>
        internal static bool TryPlainCode(byte[] bytes, int at, int length, out int code, out int next)
        {
            code = 0;
            next = at;
            int i = at;

            while (i < length && bytes[i] == (byte)' ')
            {
                i++;
            }

            int digits = 0;

            while (i < length && digits < 9 && bytes[i] >= (byte)'0' && bytes[i] <= (byte)'9')
            {
                code = code * 10 + (bytes[i] - (byte)'0');
                digits++;
                i++;
            }

            if (digits == 0 || i >= length)
            {
                return false;
            }

            if (bytes[i] == (byte)'\n')
            {
                next = i + 1;
                return true;
            }

            if (bytes[i] == (byte)'\r' && i + 1 < length && bytes[i + 1] == (byte)'\n')
            {
                next = i + 2;
                return true;
            }

            return false;
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

        private static FormatException Binary() =>
            new FormatException(
                "This is a binary DXF, which is not read: save the drawing as an ASCII DXF and read that."
            );

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        #endregion
    }
}
