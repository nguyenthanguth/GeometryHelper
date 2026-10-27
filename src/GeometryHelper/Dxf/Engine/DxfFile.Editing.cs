using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        // The types whose entity the reader reads as a shape of the library, which can be given another.
        private static readonly HashSet<string> Shaped = new HashSet<string>(StringComparer.Ordinal)
        {
            "POINT", "LINE", "RAY", "XLINE", "CIRCLE", "ARC", "ELLIPSE", "SPLINE", "LWPOLYLINE", "POLYLINE", "3DFACE", "SOLID", "TRACE", "TEXT", "MTEXT",
        };

        // The types that have a thickness.
        private static readonly HashSet<string> Thick = new HashSet<string>(StringComparer.Ordinal)
        {
            "LINE", "POINT", "CIRCLE", "ARC", "LWPOLYLINE", "POLYLINE", "TEXT", "SOLID", "TRACE",
        };

        // The types AutoCAD has from R13 on, which a file of R12 cannot hold.
        private static readonly HashSet<string> SinceR13 = new HashSet<string>(StringComparer.Ordinal) { "SPLINE", "ELLIPSE", "MTEXT", "RAY", "XLINE" };

        // The groups of an entity's look, in the order AutoCAD writes them in the part all entities have.
        private static readonly int[] LookOrder = { 67, 410, 8, 6, 62, 370, 48, 60, 420 };

        private List<Piece>[] _slots;

        // How many changes the records have had, so that what is worked out of them all, as the layers in use, is kept until the next.
        private int _editCount;
        private Writing _writing;
        private bool _handlesKnown;
        private ulong _nextHandle;
        private ulong _seed;
        private ulong _mostHandle;
        private HashSet<ulong> _removed;
        private int _cleaned;
        private double[] _modelGrown;
        private double[] _paperGrown;
        private List<DxfItem> _growLater;
        private HashSet<DxfItem> _growingLater;
        private bool _headerStale;
        private bool _ownerKnown;
        private string _modelOwner;
        private List<string> _linetypes;

        #region What is written

        /// <summary>
        /// Records written in the place of one of the file, in the order they stand in: an entity's once it is changed or
        /// added, a layer added before the end of the layer table, a header with a variable set.
        /// </summary>
        internal sealed class Piece
        {
            internal Piece(List<Rec> records)
            {
                Records = records;
            }

            internal List<Rec> Records { get; set; }
        }

        /// <summary>
        /// A group as it stands in a record: its code, where its code line begins, where its value begins and ends (a carriage
        /// return at the end of the line left out), and where the next group begins.
        /// </summary>
        internal readonly struct Pair
        {
            internal Pair(int code, int start, int valueStart, int valueEnd, int end)
            {
                Code = code;
                Start = start;
                ValueStart = valueStart;
                ValueEnd = valueEnd;
                End = end;
            }

            internal int Code { get; }

            internal int Start { get; }

            internal int ValueStart { get; }

            internal int ValueEnd { get; }

            internal int End { get; }
        }

        /// <summary>The groups of a record as they stand, its group 0 the first and its comments among them.</summary>
        private static List<Pair> PairsOf(Rec record)
        {
            var pairs = new List<Pair>(16);
            int at = record.Start;

            while (at < record.End)
            {
                int codeEnd = LineEnd(record.Bytes, at, record.End, out int valueStart);
                TryCode(record.Bytes, at, codeEnd, out int code);
                int valueEnd = LineEnd(record.Bytes, valueStart, record.End, out int next);
                pairs.Add(new Pair(code, at, valueStart, valueEnd, next));
                at = next;
            }

            return pairs;
        }

        private string Text(Rec record, Pair pair) => Text(record, pair.ValueStart, pair.ValueEnd);

        /// <summary>
        /// How a file writes what is new in it, as found in it: the line end most of its records' first lines end in (a
        /// carriage return and a line feed where as many end in each), whether most of them pad the code to three places, as
        /// AutoCAD does, and its version.
        /// </summary>
        private sealed class Writing
        {
            internal byte[] LineEnd { get; set; }

            internal bool Padded { get; set; }

            internal int Version { get; set; }

            /// <summary>Subclass markers, owners and the types of R13: from R13 (AC1012) on.</summary>
            internal bool Markers => Version >= 1012;

            /// <summary>The lightweight polyline: from R14 (AC1014) on.</summary>
            internal bool Lightweight => Version >= 1014;

            internal bool Lineweights => Version >= 1015;

            internal bool TrueColours => Version >= 1018;
        }

        private Writing Style => _writing ?? (_writing = NewWriting());

        private Writing NewWriting()
        {
            int crlf = 0;
            int lf = 0;
            int padded = 0;
            int plain = 0;

            for (int i = 0; i < _starts.Length; i++)
            {
                int at = _starts[i];
                int end = LineEnd(at, out int next);

                if (next - end == 2)
                {
                    crlf++;
                }
                else if (next - end == 1)
                {
                    lf++;
                }

                if (at < end && (_bytes[at] == ' ' || _bytes[at] == '\t'))
                {
                    padded++;
                }
                else
                {
                    plain++;
                }
            }

            // A file with no $ACADVER is read by AutoCAD as one of R12, unless its records are of a later version: one whose
            // records carry subclass markers is written in the forms of R2000, as they are.
            int version = 1009;
            string named = Version;

            if (named != null && named.StartsWith("AC", StringComparison.Ordinal) && int.TryParse(named.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            {
                version = number;
            }
            else if (named == null && WritesMarkers())
            {
                version = 1015;
            }

            return new Writing
            {
                LineEnd = crlf >= lf ? new[] { (byte)'\r', (byte)'\n' } : new[] { (byte)'\n' },
                Padded = padded >= plain,
                Version = version,
            };
        }

        /// <summary>Whether a record of the file outside its header has a subclass marker (100).</summary>
        private bool WritesMarkers()
        {
            foreach (Section section in _sections)
            {
                for (int i = section.First; section.Name != "HEADER" && i <= section.Last; i++)
                {
                    var walk = new GroupWalk(Original(i));

                    while (walk.Next(out int code, out _, out _))
                    {
                        if (code == 100)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>The whole number of the first group of a variable of the header; null where there is none.</summary>
        private int? HeaderWhole(string variable)
            => _header.TryGetValue(variable, out IReadOnlyList<DxfTypedValue> groups) && groups.Count > 0 && int.TryParse(groups[0].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : (int?)null;

        /// <summary>
        /// The bytes of a text in the file: in its code page, a letter it cannot hold as its escape, as the reading of its text
        /// reads it back. A file all ASCII is read in its page, or in UTF-8, from the first letter beyond ASCII written into it.
        /// </summary>
        private byte[] Encoded(string text)
        {
            if (_decoding.CodePage == null)
            {
                foreach (char c in text)
                {
                    if (c >= 0x80)
                    {
                        _decoding = _decoding.WithLetters(Version, _namedPage) ?? _decoding;
                        break;
                    }
                }
            }

            byte[] bytes = _decoding.Encode(text);

            // Letters of a code page whose bytes read as UTF-8 would be read so by the next reader, which takes a file before
            // R2007 whose bytes beyond ASCII are all UTF-8 for one in UTF-8: they are written as their escapes instead.
            return _decoding.CodePage != "UTF-8" && ReadsAsUtf8(bytes) ? Escaped(text) : bytes;
        }

        /// <summary>Whether bytes have some beyond ASCII and are all UTF-8.</summary>
        private static bool ReadsAsUtf8(byte[] bytes)
        {
            bool beyond = false;

            foreach (byte b in bytes)
            {
                beyond |= b >= 0x80;
            }

            if (!beyond)
            {
                return false;
            }

            try
            {
                new UTF8Encoding(false, true).GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        /// <summary>A text in ASCII, each letter beyond it its escape, as <c>\U+00E9</c> for é.</summary>
        private static byte[] Escaped(string text)
        {
            var escaped = new StringBuilder(text.Length + 16);

            foreach (char c in text)
            {
                if (c < 0x80)
                {
                    escaped.Append(c);
                }
                else
                {
                    escaped.Append("\\U+").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                }
            }

            return Encoding.ASCII.GetBytes(escaped.ToString());
        }

        /// <summary>The bytes of a record written anew, in the file's own line end, padding and code page.</summary>
        internal sealed class Builder
        {
            private readonly DxfFile _file;
            private readonly MemoryStream _bytes = new MemoryStream(256);

            internal Builder(DxfFile file)
            {
                _file = file;
            }

            /// <summary>Bytes of a record as they stand.</summary>
            internal void Raw(Rec record, int start, int end) => _bytes.Write(record.Bytes, start, end - start);

            /// <summary>A group as it stands, its two lines and their ends.</summary>
            internal void Raw(Rec record, Pair pair) => Raw(record, pair.Start, pair.End);

            /// <summary>The bytes of a value, with no line end.</summary>
            internal void Value(string value)
            {
                byte[] bytes = _file.Encoded(value);
                _bytes.Write(bytes, 0, bytes.Length);
            }

            /// <summary>A group: its code, padded as the file pads one, and its value, each on a line.</summary>
            internal void Group(int code, string value)
            {
                Writing style = _file.Style;
                string number = code.ToString(CultureInfo.InvariantCulture);
                byte[] line = Encoding.ASCII.GetBytes(style.Padded ? number.PadLeft(3) : number);
                _bytes.Write(line, 0, line.Length);
                _bytes.Write(style.LineEnd, 0, style.LineEnd.Length);
                Value(value);
                _bytes.Write(style.LineEnd, 0, style.LineEnd.Length);
            }

            /// <summary>How many bytes are written so far.</summary>
            internal int Length => (int)_bytes.Length;

            internal Rec Done(string type)
            {
                byte[] bytes = _bytes.ToArray();
                return new Rec(bytes, 0, bytes.Length, type);
            }
        }

        #endregion

        #region Where it goes

        /// <summary>Puts records in the place of one of the file: the pieces given, in their order, nothing where none is given.</summary>
        private void Put(int record, List<Piece> pieces)
        {
            if (_slots == null)
            {
                _slots = new List<Piece>[_starts.Length];
            }

            _slots[record] = pieces;
            _editCount++;
        }

        /// <summary>Puts a piece before a record of the file, after what was put before it already; the list of pieces it is in.</summary>
        private List<Piece> PutBefore(int record, Piece piece)
        {
            List<Piece> slot = _slots?[record];

            if (slot == null)
            {
                slot = new List<Piece> { new Piece(new List<Rec> { Original(record) }) };
                Put(record, slot);
            }

            slot.Insert(slot.Count - 1, piece);
            _editCount++;
            return slot;
        }

        /// <summary>The records of an entity written anew: in the place of its first record, its others put out.</summary>
        private void SetRecords(DxfItem item, List<Rec> records)
        {
            TakePendingData(item, records);
            _editCount++;

            // A record that follows an entity is written in its place among the entity's records.
            if (item.Parent != null)
            {
                // In place where the entity's records are written anew already, as a VERTEX set one at a time would copy
                // all of them each time.
                List<Rec> all = item.Parent.Piece != null ? item.Parent.Piece.Records : RecordsOf(item.Parent);
                all[item.Index] = records[0];
                SetRecords(item.Parent, all);
                item.Parent.Changed(all[0].Type);
                return;
            }

            if (item.Piece != null)
            {
                item.Piece.Records = records;
                return;
            }

            var piece = new Piece(records);
            Put(item.First, new List<Piece> { piece });

            for (int i = item.First + 1; i < item.First + item.Count; i++)
            {
                Put(i, new List<Piece>());
            }

            item.Piece = piece;
        }

        /// <summary>The ENDSEC of the last ENTITIES section, before which new entities go.</summary>
        private int EntitiesEnd()
        {
            int end = -1;

            foreach (Section section in _sections)
            {
                if (section.Name == "ENTITIES")
                {
                    end = section.Last;
                }
            }

            return end;
        }

        /// <summary>
        /// Writes the file as it stands: the bytes as read, but where records were written in the place of others, each run
        /// of records not changed written at once.
        /// </summary>
        private void WriteEdited(Stream stream)
        {
            int run = 0;

            for (int i = 0; i < _starts.Length; i++)
            {
                List<Piece> slot = _slots[i];

                if (slot == null)
                {
                    continue;
                }

                stream.Write(_bytes, run, _starts[i] - run);

                foreach (Piece piece in slot)
                {
                    foreach (Rec record in piece.Records)
                    {
                        stream.Write(record.Bytes, record.Start, record.End - record.Start);
                    }
                }

                run = End(i);
            }

            stream.Write(_bytes, run, _length - run);
        }

        /// <summary>
        /// Sees to what changes leave for the saving: what pointed at an entity removed, and the header's <c>$HANDSEED</c>
        /// and extents.
        /// </summary>
        private void Flush()
        {
            DropEmptyGroups();

            if (_removed != null && _removed.Count > _cleaned)
            {
                Clean();
            }

            FlushHeader();
        }

        /// <summary>
        /// Sees to the header alone, its <c>$HANDSEED</c> and extents, for reading it: what pointed at what was erased is seen
        /// to when the file is saved or its text asked for, so that reading the header after each erasure costs nothing more.
        /// </summary>
        private void FlushHeader()
        {
            GrowPending();

            if (_headerStale)
            {
                _headerStale = false;
                UpdateHeader();
            }
        }

        #endregion

        #region Changing an entity

        /// <summary>An entity removed is not changed.</summary>
        private static void Editable(DxfItem item)
        {
            if (item.IsRemoved)
            {
                throw new InvalidOperationException("The " + item.Type + " was removed from its space, and is not changed.");
            }
        }

        /// <summary>What an entity's change leaves to be read again: the placing of the INSERTs, which reads the layers of the drawing.</summary>
        private void Edited()
        {
            _placer = new Lazy<DxfReader.Placer>(NewPlacer);
            _editCount++;
        }

        internal void SetLayer(DxfItem item, string value)
        {
            if (string.Equals(value, item.Layer, StringComparison.Ordinal))
            {
                return;
            }

            // DXF has every entity name its layer: none is refused where the record names one.
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), "An entity is on a layer: give one, 0 where it is to be on none of its own.");
            }

            Editable(item);

            if (value != null)
            {
                CheckName(value, nameof(value));
                EnsureLayer(value);
            }

            SetGroup(item, 8, value, true);
        }

        internal void SetColour(DxfItem item, int? value)
        {
            if (value == item.Colour)
            {
                return;
            }

            Editable(item);

            if (value.HasValue && (value.Value < 0 || value.Value > 256))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The colour of an entity is an AutoCAD colour index from 1 to 255, 0 for BYBLOCK or 256 for BYLAYER.");
            }

            SetGroup(item, 62, value?.ToString(CultureInfo.InvariantCulture), false);
        }

        internal void SetTrueColour(DxfItem item, int? value)
        {
            if (value == item.TrueColour)
            {
                return;
            }

            Editable(item);

            if (value.HasValue && (value.Value < 0 || value.Value > 0xFFFFFF))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A true colour is red, green and blue, 0 to 0xFFFFFF.");
            }

            if (value.HasValue && !Style.TrueColours)
            {
                throw new InvalidOperationException("A DXF " + VersionSaid() + " has no true colours, which came with R2004 (AC1018).");
            }

            SetGroup(item, 420, value?.ToString(CultureInfo.InvariantCulture), false);
        }

        internal void SetLinetype(DxfItem item, string value)
        {
            if (string.Equals(value, item.Linetype, StringComparison.Ordinal))
            {
                return;
            }

            Editable(item);

            if (value != null && !HasLinetype(value))
            {
                throw new ArgumentException("The linetype " + value + " is not in the file's linetype table: give one of it, BYLAYER or BYBLOCK.", nameof(value));
            }

            SetGroup(item, 6, value, false);
        }

        internal void SetLineweight(DxfItem item, int? value)
        {
            if (value == item.Lineweight)
            {
                return;
            }

            Editable(item);

            if (value.HasValue && !(value.Value >= -3 && value.Value <= -1) && !DxfShape.IsLineweight(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A lineweight is -3 for the default, -2 by block, -1 by layer, or one of AutoCAD's in hundredths of a millimetre, such as 25 or 35.");
            }

            if (value.HasValue && !Style.Lineweights)
            {
                throw new InvalidOperationException("A DXF " + VersionSaid() + " has no lineweights, which came with R2000 (AC1015).");
            }

            SetGroup(item, 370, value?.ToString(CultureInfo.InvariantCulture), false);
        }

        internal void SetThickness(DxfItem item, double value)
        {
            if (BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(item.Thickness))
            {
                return;
            }

            Editable(item);
            Guard.Finite(value, nameof(value), "A thickness has to be a finite number.");

            if (value != 0.0 && !Thick.Contains(item.Type))
            {
                throw new InvalidOperationException("A " + item.Type + " has no thickness: the LINE, POINT, CIRCLE, ARC, LWPOLYLINE, POLYLINE, TEXT, SOLID and TRACE have one.");
            }

            SetGroup(item, 39, value == 0.0 ? null : Numbers.Exact(value + 0.0), false);
        }

        /// <summary>
        /// Sets a group of the look of an entity's record: the value of the first group of its code in place, or a new group
        /// where AutoCAD writes it, or none where the value is null. A layer is set on the vertices and the end of a POLYLINE
        /// and the end of an INSERT's attributes too, where they name one.
        /// </summary>
        private void SetGroup(DxfItem item, int code, string value, bool following)
        {
            List<Rec> records = RecordsOf(item);
            bool changed = false;

            if (WithGroup(records[0], code, value, true, out Rec head))
            {
                records[0] = head;
                changed = true;
            }

            for (int i = 1; following && i < records.Count; i++)
            {
                bool belongs = records[0].Type == "POLYLINE" || records[i].Type == "SEQEND";

                if (belongs && WithGroup(records[i], code, value, false, out Rec next))
                {
                    records[i] = next;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            SetRecords(item, records);
            item.Changed(records[0].Type);
            Edited();
        }

        /// <summary>
        /// A record with the first group of a code, before its extended data and outside the groups of an application
        /// (102), given a value: in place, its code line and line end kept and a whole number padded as the old one was; put
        /// where AutoCAD writes it, where asked to and there is none; or taken out, the value being null. False where nothing
        /// changes.
        /// </summary>
        private bool WithGroup(Rec record, int code, string value, bool insert, out Rec changed)
        {
            changed = record;
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            int at = -1;
            bool braced = false;

            for (int i = 1; i < data && at < 0; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code == code)
                {
                    at = i;
                }
            }

            int put = -1;

            if (at < 0)
            {
                if (value == null || !insert)
                {
                    return false;
                }

                put = PlaceOf(record, pairs, code, data);
            }
            else if (value != null)
            {
                string was = Text(record, pairs[at]);

                if (string.Equals(was, value, StringComparison.Ordinal))
                {
                    return false;
                }

                // A whole number right-aligned, as AutoCAD writes its colours and flags, stays so.
                if (IsWhole(code) && was.Length > value.Length && was.StartsWith(" ", StringComparison.Ordinal))
                {
                    value = value.PadLeft(was.Length);
                }
            }

            var b = new Builder(this);

            for (int i = 0; i < pairs.Count; i++)
            {
                if (i == put)
                {
                    b.Group(code, value);
                }

                if (i != at)
                {
                    b.Raw(record, pairs[i]);
                }
                else if (value != null)
                {
                    b.Raw(record, pairs[i].Start, pairs[i].ValueStart);
                    b.Value(value);
                    b.Raw(record, pairs[i].ValueEnd, pairs[i].End);
                }
            }

            if (put == pairs.Count)
            {
                b.Group(code, value);
            }

            changed = b.Done(record.Type);
            return true;
        }

        /// <summary>
        /// Where a new group of the look goes in a record, as AutoCAD orders them: after the last of those it writes before it
        /// in the part all entities have (67, 410, 8, 6, 62, 370, 48, 60, 420), else after the handle, the groups of the
        /// applications, the owner and the marker AcDbEntity; a thickness first in the type's own part where it has one.
        /// </summary>
        private int PlaceOf(Rec record, List<Pair> pairs, int code, int data)
        {
            int body = BodyAt(record, pairs, data);

            if (code == 39 && body >= 0)
            {
                return body + 1;
            }

            // A transparency (440) goes after all of the look, the name of a colour book (430) among it.
            int rank = code == 39 || code == 440 ? LookOrder.Length : Array.IndexOf(LookOrder, code);
            int end = body >= 0 ? body : data;
            int after = -1;
            int lead = 1;
            bool braced = false;

            for (int i = 1; i < end; i++)
            {
                Pair pair = pairs[i];

                if (pair.Code == 102)
                {
                    string said = Text(record, pair).Trim();
                    braced = said.StartsWith("{", StringComparison.Ordinal);
                    lead = braced ? lead : i + 1;
                    continue;
                }

                if (braced)
                {
                    continue;
                }

                int order = code == 440 && pair.Code == 430 ? LookOrder.Length - 1 : Array.IndexOf(LookOrder, pair.Code);

                if (order >= 0 && order < rank)
                {
                    after = i;
                }

                if (pair.Code == 5 || pair.Code == 105 || pair.Code == 330 || (pair.Code == 100 && Text(record, pair).Trim() == "AcDbEntity"))
                {
                    lead = i + 1;
                }
            }

            return after >= 0 ? after + 1 : lead;
        }

        /// <summary>Where the extended data of a record begins: its first group 1001; the end of the record where it has none.</summary>
        private static int DataAt(Rec record, List<Pair> pairs)
        {
            for (int i = 1; i < pairs.Count; i++)
            {
                if (pairs[i].Code == 1001)
                {
                    return i;
                }
            }

            return pairs.Count;
        }

        /// <summary>
        /// Where the part of a record of its type's own begins: its first subclass marker other than AcDbEntity, outside the
        /// groups of the applications and before its extended data; -1 where it has none, as a record of R12 has none.
        /// </summary>
        private int BodyAt(Rec record, List<Pair> pairs, int data)
        {
            bool braced = false;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code == 100 && Text(record, pairs[i]).Trim() != "AcDbEntity")
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsWhole(int code)
            => (code >= 60 && code <= 99) || (code >= 170 && code <= 179) || (code >= 270 && code <= 289) || (code >= 370 && code <= 379) || (code >= 420 && code <= 429);

        /// <summary>Whether a name of a layer is one <see cref="CheckName"/> takes.</summary>
        internal static bool IsLayerName(string name)
        {
            if (name.Length == 0 || name.Trim().Length != name.Length)
            {
                return false;
            }

            foreach (char c in name)
            {
                if (c < ' ' || NotInNames.IndexOf(c) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        // The characters AutoCAD takes in no name of a layer, past the control characters.
        private const string NotInNames = "<>/\":;?*|=`";

        /// <summary>A name of a layer AutoCAD takes: not empty, no space at either end, no control character and none of &lt;&gt;/":;?*|=`.</summary>
        private static void CheckName(string name, string parameter)
        {
            if (name.Length == 0 || name.Trim().Length != name.Length)
            {
                throw new ArgumentException("The name of a layer has to be given, with no space at either end: '" + name + "' is not one.", parameter);
            }

            foreach (char c in name)
            {
                if (c < ' ' || NotInNames.IndexOf(c) >= 0)
                {
                    throw new ArgumentException("The name of a layer cannot have a control character or any of < > / \" : ; ? * | = ` in it, as AutoCAD does not take them: '" + name + "' has one.", parameter);
                }
            }
        }

        private string VersionSaid() => Version != null ? "of version " + Version : "with no version, which is one of R12";

        #endregion

        #region Setting the shape

        /// <summary>
        /// Writes an entity anew from a shape: the groups of its geometry from the shape, by the writer's overload of Add for
        /// it, and every other group kept as it was, in its order; its type changed where the shape is written as another,
        /// what the old type alone had then left out. A POLYLINE's vertices are written anew, its end kept. Where
        /// <paramref name="given"/> are given, they are written in the place of the shape's, the type and own groups of the
        /// entity followed by those of the records that follow it, as a polyline converted to the other kind takes the place of
        /// the old one, its handle and look kept.
        /// </summary>
        internal void SetShape(DxfItem item, object shape, List<KeyValuePair<string, List<DxfTypedValue>>> given = null)
        {
            if (shape == null && given == null)
            {
                // No shape where the entity has none is the value its getter gives, and changes nothing; so too for a LEADER or a
                // MULTILEADER, read as its lines since but given none, as before.
                if (item.Shape == null || item.Type == "LEADER" || item.Type == "MULTILEADER")
                {
                    return;
                }

                throw new ArgumentNullException("value");
            }

            IReadOnlyList<object> now = item.Shapes;

            if (given == null && now.Count == 1 && ReferenceEquals(now[0], shape))
            {
                return;
            }

            Editable(item);

            if (given == null && !Shaped.Contains(item.Type))
            {
                throw new InvalidOperationException("A " + item.Type + " has no shape of the library's to set: those of the types a shape is written as do, " + string.Join(", ", new SortedSet<string>(Shaped, StringComparer.Ordinal)) + "; an INSERT is placed as it stands, and a LEADER and a MULTILEADER are read as their lines but not written from a shape.");
            }

            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            List<Pair> pairs = PairsOf(head);
            int hidden = item.Type == "3DFACE" ? FirstWhole(head, pairs, 70) : 0;
            double thickness = item.Thickness;
            double[] extents = null;
            List<KeyValuePair<string, List<DxfTypedValue>>> written = given ?? Written(shape, hidden, thickness, "value", out extents);

            // A shape written as the entity is written now, group for group, is the shape it has: nothing to change.
            if (given == null && now.Count == 1 && SameWriting(written, now[0], hidden, thickness))
            {
                return;
            }

            string type = written[0].Key;
            bool same = type == head.Type;
            int data = DataAt(head, pairs);
            int body = BodyAt(head, pairs, data);
            var before = new List<int>();
            var after = new List<int>();
            int raised = -1;
            bool braced = false;
            bool embedded = false;

            for (int i = 1; i < data; i++)
            {
                Pair pair = pairs[i];
                bool inHead = body >= 0 && i < body;
                List<int> keep = inHead || body < 0 ? before : after;

                if (pair.Code == 102)
                {
                    braced = Text(head, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                    keep.Add(i);
                    continue;
                }

                if (braced)
                {
                    keep.Add(i);
                    continue;
                }

                // The thickness is written where the new type has one, as the file spelled it where it is the same.
                if (pair.Code == 39)
                {
                    raised = raised < 0 ? i : raised;
                    continue;
                }

                // An object embedded in the record, as an MTEXT's columns, runs to its extended data and goes with the geometry.
                if (pair.Code == 101 || embedded)
                {
                    embedded = true;
                    continue;
                }

                if (pair.Code == 999)
                {
                    keep.Add(i);
                    continue;
                }

                // Before a marker that comes late, the groups of the geometry are the old shape's, and go as those after it do.
                if (inHead)
                {
                    if (same ? !IsShapeGroup(head.Type, pair.Code) : IsCommon(pair.Code) || pair.Code == 100)
                    {
                        keep.Add(i);
                    }

                    continue;
                }

                if (pair.Code == 100)
                {
                    if (Text(head, pair).Trim() == "AcDbEntity")
                    {
                        keep.Add(i);
                    }

                    continue;
                }

                if (same ? !IsShapeGroup(head.Type, pair.Code) : IsCommon(pair.Code))
                {
                    keep.Add(i);
                }
            }

            var b = new Builder(this);

            if (same)
            {
                b.Raw(head, pairs[0]);
            }
            else
            {
                b.Group(0, type);
            }

            // A record of R13 or later with no marker AcDbEntity is given it where AutoCAD writes it, after its handle, the
            // groups of the applications and its owner, as a record with markers has to have it.
            int marked = Style.Markers && !HasEntityMarker(head, pairs, data) ? EntityMarkerAt(head, pairs, before) : -1;

            for (int n = 0; n < before.Count; n++)
            {
                if (n == marked)
                {
                    b.Group(100, "AcDbEntity");
                }

                b.Raw(head, pairs[before[n]]);
            }

            if (marked == before.Count)
            {
                b.Group(100, "AcDbEntity");
            }

            List<DxfTypedValue> groups = written[0].Value;
            int k = 0;

            if (groups.Count > 0 && groups[0].TypeCode == 100)
            {
                b.Group(100, groups[0].Value);
                k = 1;
            }

            // The justification of a text is kept: a TEXT's 73 after the second marker of its text, where AutoCAD writes it,
            // and an MTEXT's attachment in the place of the writer's.
            var late = new List<int>();
            var keptCodes = new HashSet<int>();

            foreach (int i in after)
            {
                keptCodes.Add(pairs[i].Code);

                if (same && type == "TEXT" && pairs[i].Code == 73)
                {
                    late.Add(i);
                    continue;
                }

                b.Raw(head, pairs[i]);
            }

            for (; k < groups.Count; k++)
            {
                if (same && type == "MTEXT" && (groups[k].TypeCode == 71 || groups[k].TypeCode == 72) && keptCodes.Contains(groups[k].TypeCode))
                {
                    continue;
                }

                if (groups[k].TypeCode == 39 && raised >= 0 && SameNumber(Text(head, pairs[raised]), groups[k].Value))
                {
                    b.Raw(head, pairs[raised]);
                }
                else
                {
                    b.Group(groups[k].TypeCode, groups[k].Value);
                }
            }

            foreach (int i in late)
            {
                b.Raw(head, pairs[i]);
            }

            for (int i = data; i < pairs.Count; i++)
            {
                b.Raw(head, pairs[i]);
            }

            Rec rewritten = b.Done(type);

            // A TEXT justified other than on the left of its baseline, aligned or fitted stands at its alignment point (11):
            // the new place is written there too, as ezdxf places one.
            if (same && type == "TEXT" && IsJustified(head, pairs))
            {
                var at = new Dictionary<int, string>();

                foreach (DxfTypedValue group in groups)
                {
                    if ((group.TypeCode == 10 || group.TypeCode == 20 || group.TypeCode == 30) && !at.ContainsKey(group.TypeCode))
                    {
                        at.Add(group.TypeCode, group.Value);
                    }
                }

                foreach (int code in new[] { 10, 20, 30 })
                {
                    if (at.TryGetValue(code, out string value) && WithOwnGroup(rewritten, code + 1, value, TextOrder, out Rec placed))
                    {
                        rewritten = placed;
                    }
                }
            }

            var result = new List<Rec> { rewritten };

            // The vertices are new; the end of a POLYLINE that stays one is kept, with its handle.
            string owner = item.Handle;
            bool endKept = false;
            Rec end = records[records.Count - 1];
            bool hasEnd = head.Type == "POLYLINE" && records.Count > 1 && end.Type == "SEQEND";

            for (int r = 1; r < written.Count; r++)
            {
                if (written[r].Key == "SEQEND" && hasEnd)
                {
                    result.Add(end);
                    endKept = true;
                    continue;
                }

                result.Add(Follower(written[r], owner, f => KeepSpaceAndLayer(f, head, pairs, data)));
            }

            for (int r = 1; r < records.Count; r++)
            {
                if (!(endKept && r == records.Count - 1))
                {
                    Retire(records[r]);
                }
            }

            RetireFollowers(item);
            SetRecords(item, result);
            item.Changed(type);

            if (given != null)
            {
                GrowBy(item);
            }
            else if (item.Block == null)
            {
                Grow(extents, item.IsPaper);
            }

            Edited();
        }

        /// <summary>
        /// The records a shape is written as in this file, checked to be one entity the file's version can hold: a POLYLINE
        /// with its vertices and end is one.
        /// </summary>
        private List<KeyValuePair<string, List<DxfTypedValue>>> Written(object shape, int hidden, double thickness, string parameter, out double[] extents)
        {
            Writing style = Style;
            List<KeyValuePair<string, List<DxfTypedValue>>> written = DxfWriter.RecordsOf(shape, hidden, thickness, style.Markers, style.Lightweight, out extents);
            int entities = 0;

            foreach (KeyValuePair<string, List<DxfTypedValue>> record in written)
            {
                entities += record.Key == "VERTEX" || record.Key == "SEQEND" ? 0 : 1;
            }

            string name = shape.GetType().Name;

            if (entities != 1)
            {
                throw new ArgumentException("A " + name + " is written as " + Number(entities) + " entities, where an entity is one: give each part as an entity of its own.", parameter);
            }

            if (!style.Markers && SinceR13.Contains(written[0].Key))
            {
                throw new ArgumentException("A " + name + " is written as a " + written[0].Key + ", which a DXF " + VersionSaid() + " cannot hold, and which has no form of R12 that is the same.", parameter);
            }

            return written;
        }

        /// <summary>Whether a shape the entity has now is written as the records given, group for group.</summary>
        private bool SameWriting(List<KeyValuePair<string, List<DxfTypedValue>>> written, object was, int hidden, double thickness)
        {
            List<KeyValuePair<string, List<DxfTypedValue>>> before;

            try
            {
                before = DxfWriter.RecordsOf(was, hidden, thickness, Style.Markers, Style.Lightweight, out _);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (before.Count != written.Count)
            {
                return false;
            }

            for (int r = 0; r < before.Count; r++)
            {
                if (before[r].Key != written[r].Key || before[r].Value.Count != written[r].Value.Count)
                {
                    return false;
                }

                for (int g = 0; g < before[r].Value.Count; g++)
                {
                    if (before[r].Value[g].TypeCode != written[r].Value[g].TypeCode || !string.Equals(before[r].Value[g].Value, written[r].Value[g].Value, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// A record that follows an entity, a VERTEX or a SEQEND: its type, a handle of its own, the entity as its owner, the
        /// marker AcDbEntity, what the entity's head gives it (its space and layer), then its own groups.
        /// </summary>
        private Rec Follower(KeyValuePair<string, List<DxfTypedValue>> written, string owner, Action<Builder> head)
        {
            Writing style = Style;
            var b = new Builder(this);
            b.Group(0, written.Key);

            if (UsesHandles())
            {
                b.Group(5, NewHandle());
            }

            if (style.Markers && owner != null)
            {
                b.Group(330, owner);
            }

            if (style.Markers)
            {
                b.Group(100, "AcDbEntity");
            }

            head(b);

            foreach (DxfTypedValue group in written.Value)
            {
                b.Group(group.TypeCode, group.Value);
            }

            return b.Done(written.Key);
        }

        /// <summary>The space (67) and the layer (8) of an entity's record, as it writes them, for a record that follows it.</summary>
        private void KeepSpaceAndLayer(Builder b, Rec head, List<Pair> pairs, int data)
        {
            foreach (int code in new[] { 67, 8 })
            {
                bool braced = false;

                for (int i = 1; i < data; i++)
                {
                    if (pairs[i].Code == 102)
                    {
                        braced = Text(head, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                    }
                    else if (!braced && pairs[i].Code == code)
                    {
                        b.Raw(head, pairs[i]);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Whether a group of a type is of its geometry, which a new shape writes anew: the points, the extrusion, the radii,
        /// angles, flags, counts and widths the type is drawn from, and a text's words, height and alignment.
        /// </summary>
        private static bool IsShapeGroup(string type, int code)
        {
            bool point = code == 10 || code == 20 || code == 30;
            bool second = code == 11 || code == 21 || code == 31;
            bool extrusion = code == 210 || code == 220 || code == 230;
            bool corners = (code >= 10 && code <= 13) || (code >= 20 && code <= 23) || (code >= 30 && code <= 33);

            switch (type)
            {
                case "POINT": return point || extrusion || code == 50;
                case "LINE": return point || second || extrusion;
                case "RAY":
                case "XLINE": return point || second;
                case "CIRCLE": return point || extrusion || code == 40;
                case "ARC": return point || extrusion || code == 40 || code == 50 || code == 51;
                case "ELLIPSE": return point || second || extrusion || code == 40 || code == 41 || code == 42;
                case "LWPOLYLINE": return code == 90 || code == 70 || code == 38 || code == 10 || code == 20 || (code >= 40 && code <= 43) || code == 91 || extrusion;
                case "SPLINE": return corners || extrusion || (code >= 40 && code <= 44) || (code >= 70 && code <= 74);
                case "3DFACE": return corners || code == 70;
                case "SOLID":
                case "TRACE": return corners || extrusion;
                case "TEXT": return point || extrusion || code == 1 || code == 40 || code == 41 || code == 50;
                case "MTEXT": return point || second || extrusion || code == 1 || code == 3 || (code >= 40 && code <= 43) || code == 46 || code == 50;
                case "POLYLINE": return point || extrusion || code == 66 || code == 70 || code == 40 || code == 41 || (code >= 71 && code <= 75);
                default: return false;
            }
        }

        /// <summary>Whether a group is of the part all entities have, which an entity keeps when its type changes.</summary>
        // The groups of AcDbText in the order AutoCAD writes them, its second marker before the vertical justification.
        private static readonly IReadOnlyList<int> TextOrder = Array.AsReadOnly(new[] { 100, 39, 10, 20, 30, 40, 1, 50, 41, 51, 7, 71, 72, 11, 21, 31, 210, 220, 230, 100, 73 });

        /// <summary>Whether a record has the marker AcDbEntity, outside the groups of the applications and before its extended data.</summary>
        private bool HasEntityMarker(Rec record, List<Pair> pairs, int data)
        {
            bool braced = false;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code == 100 && Text(record, pairs[i]).Trim() == "AcDbEntity")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Where among the groups kept before the type's own the marker AcDbEntity goes: after the handle, the groups of the applications and the owner.</summary>
        private int EntityMarkerAt(Rec record, List<Pair> pairs, List<int> kept)
        {
            int at = 0;
            bool braced = false;

            for (int n = 0; n < kept.Count; n++)
            {
                Pair pair = pairs[kept[n]];

                if (pair.Code == 102)
                {
                    braced = Text(record, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                    at = n + 1;
                }
                else if (braced || pair.Code == 5 || pair.Code == 105 || pair.Code == 330 || pair.Code == 360)
                {
                    at = n + 1;
                }
            }

            return at;
        }

        /// <summary>Whether a TEXT stands at its alignment point: justified other than on the left of its baseline, aligned or fitted.</summary>
        private bool IsJustified(Rec record, List<Pair> pairs)
        {
            int horizontal = FirstWhole(record, pairs, 72);
            int vertical = FirstWhole(record, pairs, 73);
            return (horizontal != 0 || vertical != 0) && horizontal != 3 && horizontal != 5;
        }

        private static bool IsCommon(int code)
            => code == 5 || code == 330 || code == 360 || code == 67 || code == 410 || code == 8 || code == 6 || code == 62 || code == 370 || code == 48
            || code == 60 || code == 92 || code == 310 || code == 420 || code == 430 || code == 440 || code == 390 || code == 284 || code == 347 || code == 348;

        private static bool SameNumber(string written, string value)
            => Numbers.TryRead(written, out double a) && Numbers.TryRead(value, out double b) && BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        private int FirstWhole(Rec record, List<Pair> pairs, int code)
        {
            for (int i = 1; i < pairs.Count; i++)
            {
                if (pairs[i].Code == code)
                {
                    return int.TryParse(Text(record, pairs[i]).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
                }
            }

            return 0;
        }

        /// <summary>Grows the extents the header is to hold by those of a shape written, of model space or of paper space.</summary>
        private void Grow(double[] extents, bool paper)
        {
            if (extents == null || _detached)
            {
                return;
            }

            double[] grown = paper ? _paperGrown : _modelGrown;

            if (grown == null)
            {
                grown = (double[])extents.Clone();
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    grown[i] = Math.Min(grown[i], extents[i]);
                    grown[i + 3] = Math.Max(grown[i + 3], extents[i + 3]);
                }
            }

            if (paper)
            {
                _paperGrown = grown;
            }
            else
            {
                _modelGrown = grown;
            }

            _headerStale = true;
        }

        #endregion

        #region Adding and removing

        /// <summary>A shape written as a new entity of model space, at the end of the ENTITIES section, with the look given.</summary>
        internal DxfItem Add(DxfSpace space, object shape, string layer, int? colour, int? trueColour, string linetype, int? lineweight, double thickness, int hidden)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            string onLayer = string.IsNullOrWhiteSpace(layer) ? "0" : layer;
            CheckName(onLayer, nameof(layer));
            Writing style = Style;

            if (lineweight.HasValue && !style.Lineweights)
            {
                throw new ArgumentException("A DXF " + VersionSaid() + " has no lineweights, which came with R2000 (AC1015).", nameof(lineweight));
            }

            if (trueColour.HasValue && !style.TrueColours)
            {
                throw new ArgumentException("A DXF " + VersionSaid() + " has no true colours, which came with R2004 (AC1018).", nameof(trueColour));
            }

            if (linetype != null && !HasLinetype(linetype))
            {
                throw new ArgumentException("The linetype " + linetype + " is not in the file's linetype table: give one of it, BYLAYER or BYBLOCK.", nameof(linetype));
            }

            List<KeyValuePair<string, List<DxfTypedValue>>> written = Written(shape, hidden, thickness, nameof(shape), out double[] extents);
            EnsureLayer(onLayer);

            string type = written[0].Key;
            string handle = UsesHandles() ? NewHandle() : null;
            string owner = style.Markers ? ModelOwner() : null;
            var b = new Builder(this);
            b.Group(0, type);

            if (handle != null)
            {
                b.Group(5, handle);
            }

            if (owner != null)
            {
                b.Group(330, owner);
            }

            if (style.Markers)
            {
                b.Group(100, "AcDbEntity");
            }

            b.Group(8, onLayer);

            if (linetype != null)
            {
                b.Group(6, linetype);
            }

            if (colour.HasValue)
            {
                b.Group(62, colour.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (lineweight.HasValue)
            {
                b.Group(370, lineweight.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (trueColour.HasValue)
            {
                b.Group(420, trueColour.Value.ToString(CultureInfo.InvariantCulture));
            }

            foreach (DxfTypedValue group in written[0].Value)
            {
                b.Group(group.TypeCode, group.Value);
            }

            var records = new List<Rec> { b.Done(type) };

            for (int r = 1; r < written.Count; r++)
            {
                records.Add(Follower(written[r], handle, f => f.Group(8, onLayer)));
            }

            var piece = new Piece(records);

            // A file that holds entities not yet in a file keeps none of them: each holds its own records.
            if (_detached)
            {
                Edited();
                return new DxfItem(this, piece, type);
            }

            var item = new DxfItem(this, piece, type) { Home = PutBefore(EntitiesEnd(), piece) };
            space.Append(item);
            Grow(extents, false);
            Edited();
            return item;
        }

        /// <summary>An entity removed with its records, their handles kept from being given again and what points at them seen to on saving.</summary>
        internal bool Remove(DxfSpace space, DxfItem item)
        {
            if (item.Space != space || item.IsRemoved)
            {
                return false;
            }

            List<Rec> records = RecordsOf(item);

            foreach (Rec record in records)
            {
                Retire(record);
            }

            if (item.First < 0)
            {
                item.Home.Remove(item.Piece);
            }
            else
            {
                for (int i = item.First; i < item.First + item.Count; i++)
                {
                    Put(i, new List<Piece>());
                }
            }

            // The entity is read still as it was, from records the file no longer holds.
            item.Piece = new Piece(records);
            item.IsRemoved = true;
            space.Removed();
            Edited();
            return true;
        }

        /// <summary>The handle of a record put out: never given again, and what points at it seen to on saving.</summary>
        private void Retire(Rec record)
        {
            if (HandleOf(record, out ulong handle))
            {
                if (_removed == null)
                {
                    _removed = new HashSet<ulong>();
                }

                _removed.Add(handle);
            }
        }

        #endregion

        #region Handles and tables

        /// <summary>
        /// The handle of a record: its first group 5, or 105 for a DIMSTYLE, whose 5 is the name of its arrow block in a file of
        /// R12, read as hexadecimal; false where it has none.
        /// </summary>
        private static bool HandleOf(Rec record, out ulong handle)
        {
            handle = 0;
            int at = record.Start;
            bool opening = true;
            int wanted = record.Type == "DIMSTYLE" ? 105 : 5;

            while (at < record.End)
            {
                int codeEnd = LineEnd(record.Bytes, at, record.End, out int valueStart);
                TryCode(record.Bytes, at, codeEnd, out int code);
                int valueEnd = LineEnd(record.Bytes, valueStart, record.End, out at);

                if (!opening && (code == wanted || (wanted == 5 && code == 105)))
                {
                    return TryHex(record.Bytes, valueStart, valueEnd, out handle);
                }

                opening = false;
            }

            return false;
        }

        private static bool TryHex(byte[] bytes, int start, int end, out ulong value)
        {
            value = 0;

            while (start < end && (bytes[start] == ' ' || bytes[start] == '\t'))
            {
                start++;
            }

            while (end > start && (bytes[end - 1] == ' ' || bytes[end - 1] == '\t'))
            {
                end--;
            }

            if (start == end || end - start > 16)
            {
                return false;
            }

            for (int i = start; i < end; i++)
            {
                int c = bytes[i];
                int digit = c >= '0' && c <= '9' ? c - '0' : c >= 'A' && c <= 'F' ? c - 'A' + 10 : c >= 'a' && c <= 'f' ? c - 'a' + 10 : -1;

                if (digit < 0)
                {
                    return false;
                }

                value = (value << 4) | (uint)digit;
            }

            return true;
        }

        private static bool TryHex(string text, out ulong value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            return TryHex(bytes, 0, bytes.Length, out value);
        }

        /// <summary>
        /// A new handle, in hexadecimal: the next above every handle of the file as it was read and above <c>$HANDSEED</c>
        /// less one, and above every one given before; <c>$HANDSEED</c> set above it on saving.
        /// </summary>
        private string NewHandle()
        {
            KnowHandles();
            _headerStale = true;
            return (_nextHandle++).ToString("X", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Whether what is written gets handles, as the file's own records have them: from R13 on where any record of the file
        /// has one or the header has a <c>$HANDSEED</c>, and in a file of R12 where <c>$HANDLING</c> is 1. A file written with
        /// none, whose reader gives each entity a handle of its own, is given none, which would stand in the way of those.
        /// </summary>
        private bool UsesHandles()
        {
            if (_detached)
            {
                return false;
            }

            KnowHandles();
            return Style.Markers ? _mostHandle > 0 || _seed > 0 : HeaderWhole("$HANDLING") == 1;
        }

        /// <summary>Whether a record added to the file is given a handle, as <see cref="UsesHandles"/> says.</summary>
        internal bool WritesHandles => UsesHandles();

        /// <summary>The greatest handle of the file as it was read, the header's left out, and its <c>$HANDSEED</c>: read once.</summary>
        private void KnowHandles()
        {
            if (_handlesKnown)
            {
                return;
            }

            ulong mostPointed = 0;

            foreach (Section section in _sections)
            {
                if (section.Name == "HEADER")
                {
                    continue;
                }

                for (int i = section.First; i <= section.Last; i++)
                {
                    Rec record = Original(i);

                    if (HandleOf(record, out ulong handle) && handle > _mostHandle)
                    {
                        _mostHandle = handle;
                    }

                    // A handle something points at is taken too, though no record has it, so that nothing comes to point at a
                    // new entity by chance.
                    foreach (Pair pair in PairsOf(record))
                    {
                        bool pointer = (pair.Code >= 320 && pair.Code <= 369) || (pair.Code >= 390 && pair.Code <= 399) || pair.Code == 480 || pair.Code == 481 || pair.Code == 1005;

                        if (pointer && TryHex(record.Bytes, pair.ValueStart, pair.ValueEnd, out ulong pointed) && pointed > mostPointed)
                        {
                            mostPointed = pointed;
                        }
                    }
                }
            }

            _seed = _header.TryGetValue("$HANDSEED", out IReadOnlyList<DxfTypedValue> seed) && seed.Count > 0 && TryHex(seed[0].Value, out ulong read) ? read : 0;
            _nextHandle = Math.Max(Math.Max(_seed > 0 ? _seed - 1 : 0, _mostHandle), mostPointed) + 1;
            _handlesKnown = true;
        }

        /// <summary>The handle of the BLOCK_RECORD of <c>*Model_Space</c>, the owner of the entities of model space; null where there is none.</summary>
        private string ModelOwner()
        {
            if (!_ownerKnown && !_detached)
            {
                _ownerKnown = true;

                foreach (Rec record in TableRecords("BLOCK_RECORD"))
                {
                    DxfReader.Record read = Record(record);

                    if (string.Equals(DxfReader.NameOf(read.Text(2) ?? string.Empty), "*Model_Space", StringComparison.OrdinalIgnoreCase))
                    {
                        _modelOwner = read.Text(5)?.Trim();
                        break;
                    }
                }
            }

            return _modelOwner;
        }

        /// <summary>The records of a type in the TABLES sections, as they stand.</summary>
        private List<Rec> TableRecords(string type)
        {
            var records = new List<Rec>();

            foreach (Section section in _sections)
            {
                if (section.Name == "TABLES")
                {
                    foreach (Rec record in Inside(section))
                    {
                        if (record.Type == type)
                        {
                            records.Add(record);
                        }
                    }
                }
            }

            return records;
        }

        /// <summary>The names of the linetype table, as written, the spaces around them taken off.</summary>
        private List<string> Linetypes()
        {
            if (_linetypes == null)
            {
                _linetypes = TableRecords("LTYPE").ConvertAll(r => (Record(r).Text(2) ?? string.Empty).Trim());
            }

            return _linetypes;
        }

        /// <summary>Whether a linetype is in the linetype table, letters of either case the same, or is BYLAYER or BYBLOCK.</summary>
        private bool HasLinetype(string name)
        {
            // An entity not yet in a file takes any linetype, which the file it is added to checks.
            if (_detached)
            {
                return true;
            }

            string plain = DxfReader.NameOf(name);

            if (string.Equals(plain, "BYLAYER", StringComparison.OrdinalIgnoreCase) || string.Equals(plain, "BYBLOCK", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (string linetype in Linetypes())
            {
                if (string.Equals(DxfReader.NameOf(linetype), plain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a layer to the layer table where it does not list it, letters of either case the same: a plain LAYER record,
        /// in the colour 7 with the linetype CONTINUOUS as the table spells it, the lineweight and plot style of layer 0, before
        /// the table's ENDTAB, the table's count raised by one; said in the log.
        /// </summary>
        private void EnsureLayer(string name)
        {
            // An entity not yet in a file is on any layer, which the file it is added to adds to its table.
            if (_detached)
            {
                return;
            }

            string plain = DxfReader.NameOf(name);

            if (LayerNames().Contains(plain))
            {
                return;
            }

            int table = -1;
            int end = -1;

            foreach (Section section in _sections)
            {
                for (int i = section.First + 1; section.Name == "TABLES" && i < section.Last && end < 0; i++)
                {
                    if (table < 0 && _types[i] == "TABLE" && string.Equals(DxfReader.NameOf(Record(Original(i)).Text(2) ?? string.Empty), "LAYER", StringComparison.OrdinalIgnoreCase))
                    {
                        table = i;
                    }
                    else if (table >= 0 && _types[i] == "ENDTAB")
                    {
                        end = i;
                    }
                }
            }

            if (end < 0)
            {
                GeometryHelperLog.Debug("DxfFile: the layer " + plain + " is not in a layer table, and the file has none to add it to.");
                return;
            }

            Writing style = Style;
            Rec header = Current(table, table + 1)[0];
            string tableHandle = Record(header).Text(5)?.Trim();
            string continuous = Linetypes().Find(l => string.Equals(DxfReader.NameOf(l), "CONTINUOUS", StringComparison.OrdinalIgnoreCase)) ?? "CONTINUOUS";
            var b = new Builder(this);
            b.Group(0, "LAYER");

            if (UsesHandles())
            {
                b.Group(5, NewHandle());
            }

            if (style.Markers)
            {
                if (!string.IsNullOrEmpty(tableHandle))
                {
                    b.Group(330, tableHandle);
                }

                b.Group(100, "AcDbSymbolTableRecord");
                b.Group(100, "AcDbLayerTableRecord");
            }

            b.Group(2, name);
            b.Group(70, "0");
            b.Group(62, "7");
            b.Group(6, continuous);

            // The lineweight and the plot style of layer 0, which a layer of R2000 on has.
            if (style.Markers)
            {
                ZeroLayerLook(b, true);
            }

            var added = new Piece(new List<Rec> { b.Done("LAYER") });
            List<Piece> home = PutBefore(end, added);
            Listed("LAYER", added, home);

            List<Pair> pairs = PairsOf(header);
            int count = FirstWhole(header, pairs, 70);

            if (WithGroup(header, 70, (count + 1).ToString(CultureInfo.InvariantCulture), false, out Rec counted))
            {
                Put(table, new List<Piece> { new Piece(new List<Rec> { counted }) });
            }

            GeometryHelperLog.Info("DxfFile: the layer " + plain + " was not in the layer table, so a LAYER record was added for it, in the colour 7 with the linetype " + continuous + ".");
        }

        #endregion

        #region Saving

        /// <summary>What a record becomes: false where it stays as it is; else the record written anew, or null where it goes.</summary>
        private delegate bool Rewrite(Rec record, out Rec? now);

        /// <summary>Goes through the records from one to the one before another as they stand, rewriting or taking out each as asked.</summary>
        private void Visit(int first, int end, Rewrite rewrite)
        {
            for (int i = first; i < end; i++)
            {
                List<Piece> slot = _slots?[i];

                if (slot == null)
                {
                    if (rewrite(Original(i), out Rec? now))
                    {
                        Put(i, now.HasValue ? new List<Piece> { new Piece(new List<Rec> { now.Value }) } : new List<Piece>());
                    }

                    continue;
                }

                foreach (Piece piece in slot)
                {
                    for (int k = 0; k < piece.Records.Count; k++)
                    {
                        if (!rewrite(piece.Records[k], out Rec? now))
                        {
                            continue;
                        }

                        if (now.HasValue)
                        {
                            piece.Records[k] = now.Value;
                        }
                        else
                        {
                            piece.Records.RemoveAt(k);
                            k--;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sees to what pointed at what was removed: an object it owned, by the owner of the object (330), goes with it, and
        /// what that owned with that; a GROUP no longer lists it (340), and one it leaves with no member goes, as AutoCAD and
        /// ezdxf drop an empty group; a list of reactors no longer holds it (330 in {ACAD_REACTORS), nor a SORTENTSTABLE (331
        /// with the 5 after it), nor a dictionary (350 or 360 with the name 3 before it); any other pointer at it is kept and
        /// said in the log.
        /// </summary>
        private void Clean()
        {
            Cleanings++;
            HashSet<ulong> gone = _removed;
            int owned = 0;
            int unlisted = 0;
            var left = new List<string>();
            int listed;

            do
            {
                bool more = true;

                while (more)
                {
                    more = false;

                    foreach (Section section in _sections)
                    {
                        if (section.Name != "OBJECTS")
                        {
                            continue;
                        }

                        VisitInside(section, (Rec record, out Rec? now) =>
                        {
                            now = null;

                            if (!OwnedBy(record, gone, out ulong handle))
                            {
                                return false;
                            }

                            ObjectGone(handle, record);
                            gone.Add(handle);
                            owned++;
                            more = true;
                            return true;
                        });
                    }
                }

                // A GROUP emptied goes too, and then what lists it: once more until nothing more goes.
                listed = gone.Count;
                left.Clear();

                foreach (Section section in _sections)
                {
                    Visit(section.First, section.Last + 1, (Rec record, out Rec? now) => Unlisted(record, gone, left, ref unlisted, out now));
                }
            }
            while (gone.Count > listed);

            _cleaned = gone.Count;

            if (owned > 0 || unlisted > 0)
            {
                GeometryHelperLog.Info("DxfFile: what was removed was taken out of " + Number(unlisted) + " lists that held it (GROUPs, reactors, SORTENTSTABLEs and dictionaries), and " + Number(owned) + " objects it owned were removed with it.");
            }

            // Each pointer left is named once, at the first saving that finds it, not again at every saving after.
            _leftSaid = _leftSaid ?? new HashSet<string>(StringComparer.Ordinal);
            left.RemoveAll(said => !_leftSaid.Add(said));

            if (left.Count > 0)
            {
                GeometryHelperLog.Warn("DxfFile: " + Number(left.Count) + " pointers at what was removed are left where they stand, no list holding them: " + string.Join(", ", left.GetRange(0, Math.Min(left.Count, 10))) + (left.Count > 10 ? ", ..." : ".") );
            }
        }

        // The pointers at what was removed that the log has named, each as "TYPE handle (code) at handle".
        private HashSet<string> _leftSaid;

        /// <summary>Whether a record is an object owned by something removed (its first 330 outside the groups of the applications), and its handle.</summary>
        private bool OwnedBy(Rec record, HashSet<ulong> gone, out ulong handle)
        {
            handle = 0;
            bool own = false;
            bool braced = false;
            List<Pair> pairs = PairsOf(record);

            for (int i = 1; i < pairs.Count; i++)
            {
                Pair pair = pairs[i];

                if (pair.Code == 102)
                {
                    braced = Text(record, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                if (braced)
                {
                    continue;
                }

                if (pair.Code == 5 && !own)
                {
                    own = TryHex(record.Bytes, pair.ValueStart, pair.ValueEnd, out handle);

                    if (own && gone.Contains(handle))
                    {
                        return false;
                    }
                }
                else if (pair.Code == 330)
                {
                    return own && TryHex(record.Bytes, pair.ValueStart, pair.ValueEnd, out ulong owner) && gone.Contains(owner);
                }
            }

            return false;
        }

        /// <summary>A record with what was removed taken out of its lists; the other pointers at it noted.</summary>
        private bool Unlisted(Rec record, HashSet<ulong> gone, List<string> left, ref int unlisted, out Rec? now)
        {
            now = null;
            List<Pair> pairs = PairsOf(record);
            var drop = new bool[pairs.Count];
            bool any = false;
            bool braced = false;
            bool reactors = false;
            int open = -1;
            int kept = 0;
            int taken = 0;

            for (int i = 1; i < pairs.Count; i++)
            {
                Pair pair = pairs[i];

                if (pair.Code == 102)
                {
                    string said = Text(record, pair).Trim();

                    if (said.StartsWith("{", StringComparison.Ordinal))
                    {
                        braced = true;
                        reactors = said == "{ACAD_REACTORS";
                        open = i;
                        kept = 0;
                        taken = 0;
                    }
                    else
                    {
                        // A list of reactors left empty goes, as AutoCAD writes none.
                        if (reactors && kept == 0 && taken > 0)
                        {
                            drop[open] = true;
                            drop[i] = true;
                        }

                        braced = false;
                        reactors = false;
                    }

                    continue;
                }

                bool pointer = (pair.Code >= 320 && pair.Code <= 369) || (pair.Code >= 390 && pair.Code <= 399) || pair.Code == 480 || pair.Code == 481 || pair.Code == 1005;

                if (!pointer || !TryHex(record.Bytes, pair.ValueStart, pair.ValueEnd, out ulong handle) || !gone.Contains(handle))
                {
                    kept += braced ? 1 : 0;
                    continue;
                }

                if (reactors && pair.Code == 330)
                {
                    drop[i] = true;
                    taken++;
                }
                else if (!braced && record.Type == "GROUP" && pair.Code == 340)
                {
                    drop[i] = true;
                }
                else if (!braced && record.Type == "SORTENTSTABLE" && pair.Code == 331)
                {
                    drop[i] = true;

                    if (i + 1 < pairs.Count && pairs[i + 1].Code == 5)
                    {
                        drop[i + 1] = true;
                        i++;
                    }
                }
                else if (!braced && (record.Type == "DICTIONARY" || record.Type == "ACDBDICTIONARYWDFLT") && (pair.Code == 350 || pair.Code == 360))
                {
                    drop[i] = true;

                    if (pairs[i - 1].Code == 3)
                    {
                        drop[i - 1] = true;
                    }
                }
                else
                {
                    kept += braced ? 1 : 0;
                    HandleOf(record, out ulong self);
                    left.Add(record.Type + " " + self.ToString("X", CultureInfo.InvariantCulture) + " (" + Number(pair.Code) + ") at " + handle.ToString("X", CultureInfo.InvariantCulture));
                    continue;
                }

                any = true;
                unlisted++;
            }

            if (!any)
            {
                return false;
            }

            // A GROUP left with no member goes, its handle with what was removed.
            if (record.Type == "GROUP")
            {
                bool member = false;
                braced = false;

                for (int i = 1; i < pairs.Count && !member; i++)
                {
                    if (pairs[i].Code == 102)
                    {
                        braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                    }
                    else
                    {
                        member = !braced && pairs[i].Code == 340 && !drop[i];
                    }
                }

                if (!member && HandleOf(record, out ulong group))
                {
                    ObjectGone(group, record);
                    gone.Add(group);
                    return true;
                }
            }

            var b = new Builder(this);

            for (int i = 0; i < pairs.Count; i++)
            {
                if (!drop[i])
                {
                    b.Raw(record, pairs[i]);
                }
            }

            now = b.Done(record.Type);
            return true;
        }

        /// <summary>
        /// Sets the header to what the changes need, each value in place and every other byte of it as it was: <c>$HANDSEED</c>
        /// above every handle given, and <c>$EXTMIN</c> and <c>$EXTMAX</c> (<c>$PEXTMIN</c> and <c>$PEXTMAX</c> for paper
        /// space) grown to hold the shapes written since it was last brought up to date, each axis on its own and never shrunk.
        /// A variable the header does not have is not added.
        /// </summary>
        private void UpdateHeader()
        {
            var wanted = new Dictionary<string, Func<int, string, string>>(StringComparer.Ordinal);

            if (_handlesKnown && _nextHandle > _seed)
            {
                wanted["$HANDSEED"] = (code, text) => code == 5 && TryHex(text, out ulong seed) && seed < _nextHandle ? _nextHandle.ToString("X", CultureInfo.InvariantCulture) : null;
            }

            // What was grown since the header was last brought up to date: taken here once, so that what is written after it
            // grows the header as it then stands, extents set or erased since not brought back by what came before.
            double[] model = _modelGrown;
            double[] paper = _paperGrown;
            _modelGrown = null;
            _paperGrown = null;

            if (model != null && HoldsExtents("$EXTMIN", "$EXTMAX"))
            {
                wanted["$EXTMIN"] = Bound(model, 0, true);
                wanted["$EXTMAX"] = Bound(model, 3, false);
            }

            if (paper != null && HoldsExtents("$PEXTMIN", "$PEXTMAX"))
            {
                wanted["$PEXTMIN"] = Bound(paper, 0, true);
                wanted["$PEXTMAX"] = Bound(paper, 3, false);
            }

            if (wanted.Count == 0)
            {
                return;
            }

            // Where each is last given, as the reader reads it: the record and the groups after its group 9.
            var last = new Dictionary<string, KeyValuePair<int, List<int>>>(StringComparer.Ordinal);

            foreach (Section section in _sections)
            {
                for (int i = section.First; section.Name == "HEADER" && i < section.Last; i++)
                {
                    List<Rec> now = Current(i, i + 1);

                    if (now.Count != 1)
                    {
                        continue;
                    }

                    List<Pair> pairs = PairsOf(now[0]);
                    List<int> groups = null;

                    for (int p = 1; p < pairs.Count; p++)
                    {
                        if (pairs[p].Code == 9)
                        {
                            string name = Text(now[0], pairs[p]).Trim();
                            groups = wanted.ContainsKey(name) ? new List<int>() : null;

                            if (groups != null)
                            {
                                last[name] = new KeyValuePair<int, List<int>>(i, groups);
                            }
                        }
                        else
                        {
                            groups?.Add(p);
                        }
                    }
                }
            }

            var changes = new SortedDictionary<int, SortedDictionary<int, string>>();

            foreach (KeyValuePair<string, KeyValuePair<int, List<int>>> variable in last)
            {
                Rec record = Current(variable.Value.Key, variable.Value.Key + 1)[0];
                List<Pair> pairs = PairsOf(record);

                foreach (int p in variable.Value.Value)
                {
                    string value = wanted[variable.Key](pairs[p].Code, Text(record, pairs[p]));

                    if (value == null)
                    {
                        continue;
                    }

                    if (!changes.TryGetValue(variable.Value.Key, out SortedDictionary<int, string> atRecord))
                    {
                        atRecord = new SortedDictionary<int, string>();
                        changes.Add(variable.Value.Key, atRecord);
                    }

                    atRecord[p] = value;
                }
            }

            foreach (KeyValuePair<int, SortedDictionary<int, string>> change in changes)
            {
                Rec record = Current(change.Key, change.Key + 1)[0];
                List<Pair> pairs = PairsOf(record);
                var b = new Builder(this);

                for (int p = 0; p < pairs.Count; p++)
                {
                    if (change.Value.TryGetValue(p, out string value))
                    {
                        b.Raw(record, pairs[p].Start, pairs[p].ValueStart);
                        b.Value(value);
                        b.Raw(record, pairs[p].ValueEnd, pairs[p].End);
                    }
                    else
                    {
                        b.Raw(record, pairs[p]);
                    }
                }

                Put(change.Key, new List<Piece> { new Piece(new List<Rec> { b.Done(record.Type) }) });
            }

            if (changes.Count > 0)
            {
                ReadHeader();
            }
        }

        /// <summary>
        /// Whether the header holds extents to grow: both variables, each with its x, y and z. Each axis is grown on its own,
        /// so that extents left empty, the least above the most as the 1E+20 and -1E+20 ezdxf writes for a drawing it has not
        /// measured, become the box of the shapes written, as AutoCAD grows them from empty, and no axis is ever shrunk.
        /// </summary>
        private bool HoldsExtents(string least, string most)
        {
            if (!_header.TryGetValue(least, out IReadOnlyList<DxfTypedValue> low) || !_header.TryGetValue(most, out IReadOnlyList<DxfTypedValue> high))
            {
                return false;
            }

            for (int code = 10; code <= 30; code += 10)
            {
                double a = double.NaN;
                double b = double.NaN;

                foreach (DxfTypedValue group in low)
                {
                    a = group.TypeCode == code && Numbers.TryRead(group.Value, out double read) ? read : a;
                }

                foreach (DxfTypedValue group in high)
                {
                    b = group.TypeCode == code && Numbers.TryRead(group.Value, out double read) ? read : b;
                }

                if (double.IsNaN(a) || double.IsNaN(b))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The value of a group 10, 20 or 30 of an extent grown to hold a box: lower where it is the least, higher where the most; null where it holds it already.</summary>
        private static Func<int, string, string> Bound(double[] grown, int offset, bool least)
            => (code, text) =>
            {
                int axis = code == 10 ? 0 : code == 20 ? 1 : code == 30 ? 2 : -1;

                if (axis < 0 || !Numbers.TryRead(text, out double now))
                {
                    return null;
                }

                double bound = grown[offset + axis];
                return (least ? bound < now : bound > now) ? Numbers.Exact(bound + 0.0) : null;
            };

        #endregion
    }
}
