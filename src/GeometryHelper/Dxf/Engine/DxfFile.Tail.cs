using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        #region The data of an object kept with its places

        /// <summary>
        /// The groups of a record as <see cref="TailOf"/> reads them, from its first to its extended data, found without making
        /// a string of any: where each stands in the record's bytes, two numbers a group, where its code line begins and where
        /// its value line ends with its line end, counted from the record's start, its value made when it is asked for; for an
        /// edit of a few groups that costs the bytes of the record and not a reading of every group of it
        /// (<see cref="SpliceBytes"/>).
        /// </summary>
        internal sealed class TailMap
        {
            private readonly DxfFile _file;
            private readonly Pair[] _pairs;

            internal TailMap(DxfFile file, Rec record, Pair[] pairs, int dataEnd)
            {
                _file = file;
                Record = record;
                _pairs = pairs;
                DataEnd = dataEnd;
            }

            /// <summary>The record the places are of.</summary>
            internal Rec Record { get; }

            /// <summary>How many groups there are.</summary>
            internal int Count => _pairs.Length;

            /// <summary>Where the groups end and the extended data begins (the record's end where it has none), counted from the record's start; a comment after the last group stands before it.</summary>
            internal int DataEnd { get; }

            /// <summary>The code of a group.</summary>
            internal int Code(int index) => _pairs[index].Code;

            /// <summary>Where a group's code line begins, counted from the record's start.</summary>
            internal int Start(int index) => _pairs[index].Start - Record.Start;

            /// <summary>Where a group's value line ends, its line end with it, counted from the record's start.</summary>
            internal int End(int index) => _pairs[index].End - Record.Start;

            /// <summary>The value of a group as <see cref="TailOf"/> reads it.</summary>
            internal string Value(int index) => _file.TailText(Record, _pairs[index]);

            /// <summary>A group as <see cref="TailOf"/> reads it.</summary>
            internal DxfTypedValue Group(int index) => DxfTypedValue.Read(_pairs[index].Code, Value(index));

            /// <summary>
            /// Whether a group is of the code given and its value, its spaces taken off, is the text given: found in the bytes
            /// where the value is of printable letters and digits of ASCII, where no string is made for it.
            /// </summary>
            internal bool Is(int index, int code, string text)
            {
                Pair pair = _pairs[index];

                if (pair.Code != code)
                {
                    return false;
                }

                byte[] bytes = Record.Bytes;
                int start = pair.ValueStart;
                int end = pair.ValueEnd;

                while (start < end && IsSpace(bytes[start]))
                {
                    start++;
                }

                while (end > start && IsSpace(bytes[end - 1]))
                {
                    end--;
                }

                for (int i = start; i < end; i++)
                {
                    if (bytes[i] <= 0x20 || bytes[i] >= 0x7F || bytes[i] == (byte)'\\')
                    {
                        return Value(index).Trim() == text;
                    }
                }

                if (end - start != text.Length)
                {
                    return false;
                }

                for (int i = 0; i < text.Length; i++)
                {
                    if (bytes[start + i] != text[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool IsSpace(byte b) => b == 0x20 || (b >= 0x09 && b <= 0x0D);
        }

        /// <summary>
        /// The groups of the first record of an item as <see cref="TailOf"/> reads them from the first (<c>TailOf(item, 0)</c>),
        /// each with the place it stands in the record's bytes, their values not read until they are asked for.
        /// </summary>
        internal TailMap MapTail(DxfItem item)
        {
            Rec head = RecordAt(item, 0);
            List<Pair> pairs = PairsOfSized(head, (head.End - head.Start) / 16);
            int data = DataAt(head, pairs);
            var kept = new Pair[Math.Max(data - 1, 0)];
            int count = 0;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code != 999)
                {
                    kept[count++] = pairs[i];
                }
            }

            Array.Resize(ref kept, count);
            int dataEnd = data < pairs.Count ? pairs[data].Start - head.Start : head.End - head.Start;
            return new TailMap(this, head, kept, dataEnd);
        }

        /// <summary>
        /// Whether the groups of a map from the one given on stand in the record as the file writes a group, one after another
        /// with nothing between them or after the last (no comment, no other spelling of a number), so that writing them anew
        /// gives the very bytes they stand in: what a splice that leaves the bytes around its own as they are depends on. A
        /// group is looked at in its bytes, its code line as the file pads one and its value a text of ASCII with no escape
        /// (<c>\U+</c>, <c>\M+</c>) in it, which is the same bytes however it is written; any other is written and compared.
        /// </summary>
        internal bool TailStandsAsWritten(TailMap map, int from)
        {
            int count = map.Count;

            if (from >= count)
            {
                return true;
            }

            Writing style = Style;
            byte[] bytes = map.Record.Bytes;
            int origin = map.Record.Start;

            for (int k = from; k < count; k++)
            {
                int start = origin + map.Start(k);
                int end = origin + map.End(k);

                if (k > from && start != origin + map.End(k - 1))
                {
                    return false;
                }

                if (StandsAsWritten(map, k, style, bytes, start, end))
                {
                    continue;
                }

                var written = new Builder(this);
                written.Group(map.Code(k), map.Value(k));
                byte[] again = written.ToArray();

                if (again.Length != end - start)
                {
                    return false;
                }

                for (int i = 0; i < again.Length; i++)
                {
                    if (bytes[start + i] != again[i])
                    {
                        return false;
                    }
                }
            }

            return map.End(count - 1) == map.DataEnd;
        }

        /// <summary>Whether a group, from the first byte of its code line to the end of its value line, is the bytes the file writes it as, where its value is a text of ASCII with no escape in it; false where it is not or the value is of another kind, to be written and compared.</summary>
        private static bool StandsAsWritten(TailMap map, int index, Writing style, byte[] bytes, int start, int end)
        {
            int code = map.Code(index);

            if (code < 0 || code >= 1000)
            {
                return false;
            }

            // The code line, as the file pads one, and its line end; then the value, ASCII with no escape where the code is one
            // of a text, and its line end.
            byte[] line = Builder.CodeLine(code, style.Padded);
            byte[] lineEnd = style.LineEnd;
            int at = start + line.Length + lineEnd.Length;
            int valueEnd = end - lineEnd.Length;

            if (
                !Matches(bytes, start, line)
                || !Matches(bytes, start + line.Length, lineEnd)
                || valueEnd < at
                || !Matches(bytes, valueEnd, lineEnd)
            )
            {
                return false;
            }

            bool text = (code >= 1 && code <= 9) || (code >= 300 && code <= 309);

            for (int i = at; i < valueEnd; i++)
            {
                byte b = bytes[i];

                if (b >= 0x80 || b == (byte)'\n' || b == (byte)'\r')
                {
                    return false;
                }

                if (text && b == (byte)'\\' && i + 2 < valueEnd)
                {
                    byte kind = bytes[i + 1];

                    if ((kind == 'U' || kind == 'u' || kind == 'M' || kind == 'm') && bytes[i + 2] == (byte)'+')
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Whether bytes from a place are those given.</summary>
        private static bool Matches(byte[] bytes, int at, byte[] expected)
        {
            if (at < 0 || at + expected.Length > bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (bytes[at + i] != expected[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Writes bytes in the place of others in the first record of an item, the places counted from the record's start, each
        /// from where the bytes replaced begin to where they end, in the order they stand and none inside another; the bytes
        /// before, between and after copied as they are, in one array of the record's new length. The item is changed as
        /// <see cref="ReplaceTail"/> changes it.
        /// </summary>
        /// <returns>The record as it stands now.</returns>
        internal Rec SpliceBytes(DxfItem item, IList<int> from, IList<int> to, IList<byte[]> middle)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            int original = head.End - head.Start;
            int length = original;

            for (int k = 0; k < from.Count; k++)
            {
                if (from[k] < 0 || to[k] < from[k] || to[k] > original || (k > 0 && from[k] < to[k - 1]))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(from),
                        from[k],
                        "The bytes are not within the " + Number(original) + " of the record, or are not in order."
                    );
                }

                length += middle[k].Length - (to[k] - from[k]);
            }

            var bytes = new byte[length];
            int at = 0;
            int taken = 0;

            for (int k = 0; k < from.Count; k++)
            {
                Buffer.BlockCopy(head.Bytes, head.Start + taken, bytes, at, from[k] - taken);
                at += from[k] - taken;
                Buffer.BlockCopy(middle[k], 0, bytes, at, middle[k].Length);
                at += middle[k].Length;
                taken = to[k];
            }

            Buffer.BlockCopy(head.Bytes, head.Start + taken, bytes, at, head.End - head.Start - taken);
            records[0] = new Rec(bytes, 0, bytes.Length, head.Type);
            SetRecords(item, records);
            item.Changed(head.Type);
            Edited();
            return records[0];
        }

        /// <summary>
        /// The values, spaces taken off, of the groups of a code in the first record of an item between two places counted from
        /// the record's start, in their order, the places those of a group's first byte and the end of another's: the handles
        /// a table's cells hold, found by walking the bytes of the cells and not by reading their groups.
        /// </summary>
        internal List<string> ValuesBetween(DxfItem item, int from, int to, int code)
        {
            Rec head = RecordAt(item, 0);
            var found = new List<string>();
            int at = head.Start + from;
            int end = Math.Min(head.Start + to, head.End);

            while (at < end)
            {
                int codeEnd = LineEnd(head.Bytes, at, end, out int valueStart);
                bool coded = TryCode(head.Bytes, at, codeEnd, out int read);
                int valueEnd = LineEnd(head.Bytes, valueStart, end, out at);

                if (coded && read == code)
                {
                    found.Add(Text(head, valueStart, valueEnd).Trim());
                }
            }

            return found;
        }

        /// <summary>
        /// The bytes of a record made of stretches of another record, copied as they are, and of groups written new, in their
        /// order: the stretches are not copied until the record is made (<see cref="Done"/>), in one array of its length, as
        /// <see cref="SpliceBytes"/> makes one, where a builder grows a buffer and copies it again for its array.
        /// </summary>
        internal sealed class Pieces
        {
            private readonly DxfFile _file;
            private readonly List<byte[]> _bytes = new List<byte[]>();
            private readonly List<int> _from = new List<int>();
            private readonly List<int> _to = new List<int>();
            private Builder _written;

            internal Pieces(DxfFile file)
            {
                _file = file;
            }

            /// <summary>How many bytes are in the pieces so far.</summary>
            internal int Length { get; private set; }

            /// <summary>Bytes of a record as they stand, from a place to another (the record's own, <c>Start</c> included); one stretch with the one before where they follow on from it.</summary>
            internal void Raw(Rec record, int start, int end)
            {
                Flush();
                int last = _bytes.Count - 1;

                if (last >= 0 && ReferenceEquals(_bytes[last], record.Bytes) && _to[last] == start)
                {
                    _to[last] = end;
                }
                else
                {
                    _bytes.Add(record.Bytes);
                    _from.Add(start);
                    _to.Add(end);
                }

                Length += end - start;
            }

            /// <summary>A group as the file writes it.</summary>
            internal void Group(int code, string value)
            {
                _written = _written ?? new Builder(_file);
                int before = _written.Length;
                _written.Group(code, value);
                Length += _written.Length - before;
            }

            private void Flush()
            {
                if (_written == null)
                {
                    return;
                }

                byte[] bytes = _written.ToArray();
                _bytes.Add(bytes);
                _from.Add(0);
                _to.Add(bytes.Length);
                _written = null;
            }

            /// <summary>The record the pieces make.</summary>
            internal Rec Done(string type)
            {
                Flush();
                var all = new byte[Length];
                int at = 0;

                for (int k = 0; k < _bytes.Count; k++)
                {
                    Buffer.BlockCopy(_bytes[k], _from[k], all, at, _to[k] - _from[k]);
                    at += _to[k] - _from[k];
                }

                return new Rec(all, 0, all.Length, type);
            }
        }

        /// <summary>
        /// Writes the first record of an item anew through pieces the caller fills, given the record as it stands (stretches of
        /// its bytes copied into them with <see cref="Pieces.Raw(Rec, int, int)"/>, groups written with
        /// <see cref="Pieces.Group(int, string)"/>), so that what stays as it is costs a copy of its bytes. The item is changed
        /// as <see cref="SpliceBytes"/> changes it.
        /// </summary>
        /// <returns>The record as it stands now.</returns>
        internal Rec RewriteRecord(DxfItem item, Action<Pieces, Rec> write)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            var pieces = new Pieces(this);
            write(pieces, head);
            Rec made = pieces.Done(head.Type);
            records[0] = made;
            SetRecords(item, records);
            item.Changed(head.Type);
            Edited();
            return made;
        }

        #endregion
    }
}
