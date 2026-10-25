using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The own groups of the record of a dimension, a dimension style or a table, read as a list to be changed and written
    /// back in one writing: the groups after the first subclass marker of the type (AcDbDimension, AcDbSymbolTableRecord,
    /// AcDbBlockReference), the later markers among them, or from the start in a file of R12.
    /// </summary>
    /// <remarks>
    /// Those records use one code for two things in two of their parts, as a dimension's 70 (its type in AcDbDimension, whether
    /// an arc length is partial in AcDbArcDimension) or a dimension style's 70 (its flags, then DIMTFILLCLR): a group is found by
    /// its code in a part of the list, its <see cref="Base"/> or its <see cref="Sub"/>, not by the first of its code.
    /// </remarks>
    internal sealed class DxfDimGroups
    {
        private readonly DxfItem _item;
        private readonly HashSet<int> _codes = new HashSet<int>();
        private readonly List<KeyValuePair<int, string>> _was = new List<KeyValuePair<int, string>>();

        /// <summary>The own groups of a record of a file, or of an object made in code that holds its record in a file of its own.</summary>
        internal DxfDimGroups(DxfItem item)
        {
            _item = item;
            IReadOnlyList<DxfTypedValue> own = item.File.OwnGroupsOf(item, 0);
            int first = own.Count > 0 && own[0].TypeCode == 100 ? 1 : 0;
            Marked = first == 1;

            for (int i = first; i < own.Count; i++)
            {
                var group = new KeyValuePair<int, string>(own[i].TypeCode, own[i].Value);
                List.Add(group);
                _was.Add(group);
                _codes.Add(own[i].TypeCode);
            }

            Split = Marked ? List.FindIndex(g => g.Key == 100) : -1;

            if (Marked && Split < 0)
            {
                Split = List.Count;
            }
        }

        /// <summary>The groups, to be changed in place.</summary>
        internal List<KeyValuePair<int, string>> List { get; } = new List<KeyValuePair<int, string>>();

        /// <summary>Whether the record has subclass markers, from R13 on.</summary>
        internal bool Marked { get; }

        /// <summary>Where the second subclass marker stands (the first after the type's own), the end where there is none; -1 in a file of R12.</summary>
        internal int Split { get; private set; }

        /// <summary>The part of the type's first subclass: from the start to the second marker; the whole list in a file of R12.</summary>
        internal Range Base => Split < 0 ? new Range(0, List.Count) : new Range(0, Split);

        /// <summary>The parts of the later subclasses: from the second marker to the end; the whole list in a file of R12.</summary>
        internal Range Sub => Split < 0 ? new Range(0, List.Count) : new Range(Split, List.Count);

        /// <summary>The whole list.</summary>
        internal Range All => new Range(0, List.Count);

        /// <summary>The index of the nth group of a code within a part (n from nought); -1 where there is none.</summary>
        internal int Find(int code, Range part, int nth = 0)
        {
            for (int i = part.From; i < part.To && i < List.Count; i++)
            {
                if (List[i].Key == code && nth-- == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The text of the first group of a code within a part; null where there is none.</summary>
        internal string Text(int code, Range part)
        {
            int at = Find(code, part);
            return at < 0 ? null : List[at].Value;
        }

        /// <summary>The number of the first group of a code within a part, as the reader reads one; the fallback where there is none, or not a finite number.</summary>
        internal double Real(int code, Range part, double fallback)
            => Numbers.TryRead(Text(code, part), out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>The whole number of the first group of a code within a part; the fallback where there is none, or not a whole number.</summary>
        internal int Whole(int code, Range part, int fallback)
            => int.TryParse(Text(code, part)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        /// <summary>
        /// The point of a code and the codes 10 and 20 above it within a part, as the reader reads one: a coordinate the record
        /// leaves out is nought, and the fallback where it has none of the three.
        /// </summary>
        internal GeoPoint3 Point(int code, Range part, GeoPoint3 fallback)
        {
            bool x = Numbers.TryRead(Text(code, part), out double px) && Guard.IsFinite(px);
            bool y = Numbers.TryRead(Text(code + 10, part), out double py) && Guard.IsFinite(py);
            bool z = Numbers.TryRead(Text(code + 20, part), out double pz) && Guard.IsFinite(pz);
            return x || y || z ? new GeoPoint3(x ? px : 0.0, y ? py : 0.0, z ? pz : 0.0) : fallback;
        }

        /// <summary>
        /// Gives the first group of a code within a part a text: in place where it has one, else put in after the last group of
        /// the part whose code comes before it in the order AutoCAD writes them; null takes it out.
        /// </summary>
        internal void Set(int code, string value, Range part, IReadOnlyList<int> order)
        {
            int at = Find(code, part);

            if (at >= 0)
            {
                if (value == null)
                {
                    RemoveAt(at);
                }
                else
                {
                    List[at] = new KeyValuePair<int, string>(code, value);
                }

                return;
            }

            if (value != null)
            {
                Insert(Place(code, part, order), new KeyValuePair<int, string>(code, value));
            }
        }

        /// <summary>
        /// Gives a point of a code and the codes 10 and 20 above it within a part, each coordinate that differs from the one read,
        /// to the bit, written as the file writes a number, and those the record leaves out with them; nothing where none differs.
        /// </summary>
        internal void SetPoint(int code, GeoPoint3 value, Range part, IReadOnlyList<int> order, GeoPoint3 fallback)
        {
            GeoPoint3 now = Point(code, part, fallback);
            double[] values = { value.X, value.Y, value.Z };
            double[] was = { now.X, now.Y, now.Z };
            bool differs = false;

            for (int i = 0; i < 3; i++)
            {
                differs |= BitConverter.DoubleToInt64Bits(values[i]) != BitConverter.DoubleToInt64Bits(was[i]);
            }

            if (!differs)
            {
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                int at = code + 10 * i;

                if (BitConverter.DoubleToInt64Bits(values[i]) != BitConverter.DoubleToInt64Bits(was[i]) || Find(at, Part(part)) < 0)
                {
                    Set(at, Numbers.Exact(values[i] + 0.0), Part(part), order);
                }
            }
        }

        /// <summary>Puts a group in at an index, the parts after it moved on.</summary>
        internal void Insert(int index, KeyValuePair<int, string> group)
        {
            List.Insert(index, group);

            if (Split >= 0 && index <= Split)
            {
                Split++;
            }
        }

        /// <summary>Takes a group out at an index, the parts after it moved back.</summary>
        internal void RemoveAt(int index)
        {
            List.RemoveAt(index);

            if (Split > index)
            {
                Split--;
            }
        }

        /// <summary>
        /// Where a group of a code goes within a part: after the last group of the part whose code comes before it in the order,
        /// else at the start of the part, after its marker.
        /// </summary>
        internal int Place(int code, Range part, IReadOnlyList<int> order)
        {
            int rank = IndexIn(order, code);
            int after = -1;

            for (int i = part.From; i < part.To && i < List.Count; i++)
            {
                int their = IndexIn(order, List[i].Key);

                if (List[i].Key != 100 && their >= 0 && their < rank)
                {
                    after = i;
                }
            }

            if (after >= 0)
            {
                return after + 1;
            }

            return part.From < List.Count && part.From < part.To && List[part.From].Key == 100 ? part.From + 1 : part.From;
        }

        /// <summary>
        /// Writes the groups back into the record in one writing, where they differ from those read; the extents of the
        /// drawing are not grown, as none of these types has a shape the library reads.
        /// </summary>
        /// <returns>Whether anything was written.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the record is erased.</exception>
        internal bool Write(IReadOnlyList<int> order)
        {
            if (Same())
            {
                return false;
            }

            var codes = new HashSet<int>(_codes);
            _item.File.ReplaceOwn(_item, 0, codes, List.ToArray(), order, false);
            return true;
        }

        /// <summary>Whether the groups are those read, code for code and text for text.</summary>
        internal bool Same()
        {
            if (List.Count != _was.Count)
            {
                return false;
            }

            for (int i = 0; i < List.Count; i++)
            {
                if (List[i].Key != _was[i].Key || !string.Equals(List[i].Value, _was[i].Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The part a range stands for once the list has changed: the base or the later subclasses, as given.</summary>
        private Range Part(Range part) => Split < 0 ? All : part.From == 0 ? Base : Sub;

        private static int IndexIn(IReadOnlyList<int> order, int code)
        {
            for (int k = 0; k < order.Count; k++)
            {
                if (order[k] == code)
                {
                    return k;
                }
            }

            return -1;
        }

        /// <summary>A part of the list: from an index up to another, that one left out.</summary>
        internal readonly struct Range
        {
            internal Range(int from, int to)
            {
                From = from;
                To = to;
            }

            internal int From { get; }

            internal int To { get; }
        }
    }
}
