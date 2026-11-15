using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        #region The data of an object kept with its places

        /// <summary>
        /// The groups of a record as <see cref="TailOf"/> reads them, from its first to its extended data, and where each stands
        /// in the record's bytes: two numbers a group, where its code line begins and where its value line ends with its line
        /// end, counted from the record's start; for an edit of a few groups that costs the bytes of the record and not a
        /// reading of every group of it (<see cref="SpliceBytes"/>).
        /// </summary>
        internal sealed class TailMap
        {
            internal TailMap(Rec record, List<DxfTypedValue> groups, int[] places)
            {
                Record = record;
                Groups = groups;
                Places = places;
            }

            /// <summary>The record the places are of.</summary>
            internal Rec Record { get; }

            /// <summary>The groups, each as <see cref="TailOf"/> reads it.</summary>
            internal List<DxfTypedValue> Groups { get; }

            /// <summary>Where each group stands, two numbers a group, counted from the record's start.</summary>
            internal int[] Places { get; }
        }

        /// <summary>
        /// The groups of the first record of an item as <see cref="TailOf"/> reads them from the first (<c>TailOf(item, 0)</c>),
        /// each with the place it stands in the record's bytes.
        /// </summary>
        internal TailMap MapTail(DxfItem item)
        {
            Rec head = RecordAt(item, 0);
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            var groups = new List<DxfTypedValue>(data);
            var places = new List<int>(2 * data);

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code != 999)
                {
                    groups.Add(DxfTypedValue.Read(pairs[i].Code, TailText(head, pairs[i])));
                    places.Add(pairs[i].Start - head.Start);
                    places.Add(pairs[i].End - head.Start);
                }
            }

            return new TailMap(head, groups, places.ToArray());
        }

        /// <summary>
        /// Whether the groups of a map from the one given on stand in the record as the file writes a group, one after another
        /// with nothing between them (no comment, no other spelling of a number), so that writing them anew gives the very bytes
        /// they stand in: what a splice that leaves the bytes around its own as they are depends on.
        /// </summary>
        internal bool TailStandsAsWritten(TailMap map, int from)
        {
            int count = map.Groups.Count;

            if (from >= count)
            {
                return true;
            }

            var b = new Builder(this);

            for (int k = from; k < count; k++)
            {
                b.Group(map.Groups[k].TypeCode, map.Groups[k].Value);
            }

            byte[] written = b.ToArray();
            int start = map.Record.Start + map.Places[2 * from];
            int end = map.Record.Start + map.Places[2 * count - 1];

            if (end - start != written.Length)
            {
                return false;
            }

            for (int i = 0; i < written.Length; i++)
            {
                if (map.Record.Bytes[start + i] != written[i])
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

        #endregion
    }
}
