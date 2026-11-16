using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfTable
    {
        // The TABLECONTENT of the table as it was read for the edit before, each of its cells with where it stands in its bytes
        // (EditContent); null until an edit reads it.
        private ContentBytes _contentBytes;

        // Whether the TABLECONTENT was found not to stand as the file writes its groups, or not to be in the form AutoCAD writes,
        // so that writing the groups an edit changes beside the bytes of those it does not change would not give the bytes a
        // writing of every group gives: it is then read and written whole, every time.
        private bool _contentUnfit;

        /// <summary>
        /// A TABLECONTENT read, with the place of each of its cells in the bytes of its record (<see cref="ContentGridCell.Start"/>),
        /// kept with the edits of <see cref="EditContent"/>; the places are those of the record <see cref="Holds"/> says.
        /// </summary>
        private sealed class ContentBytes
        {
            internal ContentBytes(
                DxfFile.Rec record,
                ContentGrid grid,
                int gridAt,
                int gridEnd,
                List<KeyValuePair<int, string>> head
            )
            {
                Record = record;
                Grid = grid;
                GridAt = gridAt;
                GridEnd = gridEnd;
                Head = head;
            }

            internal DxfFile.Rec Record { get; set; }

            /// <summary>The groups before the ones the grid holds (those of AcDbObject and AcDbLinkedData), as <see cref="DxfFile.TailOf"/> reads them.</summary>
            internal List<KeyValuePair<int, string>> Head { get; }

            /// <summary>The groups of the record as one list (<see cref="ContentFlat"/>), made for the links and kept until the grid is changed; null until made.</summary>
            internal ContentFlat Flat { get; set; }

            /// <summary>Whether the record holds no extended data after its groups, so that the groups the grid holds, after <see cref="Head"/>, are all of the record's.</summary>
            internal bool HoldsAll => GridEnd == Record.End - Record.Start;

            internal ContentGrid Grid { get; }

            /// <summary>Where the groups the grid holds begin, counted from the record's start; the bytes before are kept as they are.</summary>
            internal int GridAt { get; }

            /// <summary>Where they end, counted from the record's start; the bytes after (the extended data) are kept as they are.</summary>
            internal int GridEnd { get; set; }

            /// <summary>Whether the places are of the record as it stands: its very bytes.</summary>
            internal bool Holds(DxfFile.Rec record) =>
                ReferenceEquals(Record.Bytes, record.Bytes) && Record.Start == record.Start && Record.End == record.End;
        }

        /// <summary>
        /// The TABLECONTENT with the places of its cells, read where the kept one is not of the record as it stands; null where
        /// it is not in the form AutoCAD writes, or its groups do not stand as the file writes them (<see cref="_contentUnfit"/>,
        /// set then, so that a record of that kind is not read twice by an edit that reads it whole).
        /// </summary>
        private ContentBytes ContentBytesOf(DxfItem content)
        {
            DxfFile file = Item.File;
            DxfFile.Rec record = file.RecordAt(content, 0);

            if (_contentBytes != null && _contentBytes.Holds(record))
            {
                return _contentBytes;
            }

            _contentBytes = null;
            DxfFile.TailMap map = file.MapTail(content);
            ContentGrid grid = ContentGrid.Read(map);

            if (grid == null || !file.TailStandsAsWritten(map, grid.From))
            {
                _contentUnfit = true;
                return null;
            }

            grid.HeadStartByte = map.Start(grid.From);

            foreach (ContentRow row in grid.Rows)
            {
                row.Start = map.Start(grid.From + row.GroupFrom);
                row.End = map.End(grid.From + row.GroupTo - 1);

                foreach (ContentGridCell cell in row.Cells)
                {
                    cell.Start = map.Start(grid.From + cell.GroupFrom);
                    cell.End = map.End(grid.From + cell.GroupTo - 1);
                }
            }

            grid.HeadEndByte = grid.Rows.Count > 0 ? grid.Rows[0].Start : -1;
            int tailAt = grid.From + grid.TailAt;
            grid.TailStartByte = tailAt < map.Count ? map.Start(tailAt) : map.DataEnd;

            if (grid.Merges != null)
            {
                grid.MergesStartByte = map.Start(grid.From + grid.MergesFrom);
                grid.MergesEndByte = map.End(grid.From + grid.MergesTo - 1);
            }

            var head = new List<KeyValuePair<int, string>>(grid.From);

            for (int k = 0; k < grid.From; k++)
            {
                head.Add(new KeyValuePair<int, string>(map.Code(k), map.Value(k)));
            }

            _contentBytes = new ContentBytes(record, grid, map.Start(grid.From), map.DataEnd, head);
            return _contentBytes;
        }

        /// <summary>
        /// The TABLECONTENT with the places of its cells where it is kept for the record as it stands (<see cref="ContentBytesOf"/>
        /// read it for an edit) or, where <paramref name="read"/> asks, read now; null where it is not in the form AutoCAD writes,
        /// or its groups do not stand as the file writes them, or it holds extended data, so that its cells are not all its groups.
        /// </summary>
        private ContentBytes HeldContent(DxfItem content, bool read)
        {
            if (content == null || _contentUnfit || IsErased)
            {
                return null;
            }

            DxfFile.Rec record = Item.File.RecordAt(content, 0);
            ContentBytes kept =
                _contentBytes != null && _contentBytes.Holds(record) ? _contentBytes
                : read ? ContentBytesOf(content)
                : null;
            return kept != null && kept.HoldsAll ? kept : null;
        }

        /// <summary>
        /// Writes the record of the TABLECONTENT anew from the grid kept, the cells it did not change copied as the bytes they
        /// stand in: the bytes before the groups and after them as they are, and the kept places made those of the record written.
        /// </summary>
        private void WriteContent(DxfItem content, ContentBytes kept)
        {
            int gridEnd = kept.GridEnd;
            int gridAt = kept.GridAt;
            kept.Flat = null;
            kept.Record = Item.File.RewriteRecord(content, (b, old) =>
            {
                b.Raw(old, old.Start, old.Start + gridAt);
                kept.Grid.WriteTo(b, old, gridEnd);
                kept.GridEnd = b.Length;
                b.Raw(old, old.Start + gridEnd, old.End);
            });
            _contentBytes = kept;
        }

        /// <summary>The groups a TABLECONTENT is read from, counted from the first after its group 0: held as <see cref="DxfFile.TailOf"/> reads them, or in a map of the record's bytes whose values are made when they are asked for.</summary>
        private abstract class ContentSource
        {
            internal abstract int Count { get; }

            internal abstract int Code(int index);

            internal abstract string Value(int index);

            /// <summary>Whether a group is of the code given and its value, its spaces taken off, the text given.</summary>
            internal abstract bool Is(int index, int code, string text);

            internal KeyValuePair<int, string> Pair(int index) =>
                new KeyValuePair<int, string>(Code(index), Value(index));

            /// <summary>The groups from one to before another, as a list.</summary>
            internal List<KeyValuePair<int, string>> Range(int from, int to)
            {
                var groups = new List<KeyValuePair<int, string>>(to - from);

                for (int k = from; k < to; k++)
                {
                    groups.Add(Pair(k));
                }

                return groups;
            }
        }

        private sealed class ListSource : ContentSource
        {
            private readonly List<DxfTypedValue> _groups;

            internal ListSource(List<DxfTypedValue> groups) => _groups = groups;

            internal override int Count => _groups.Count;

            internal override int Code(int index) => _groups[index].TypeCode;

            internal override string Value(int index) => _groups[index].Value;

            internal override bool Is(int index, int code, string text) =>
                _groups[index].TypeCode == code && _groups[index].Value.Trim() == text;
        }

        private sealed class MapSource : ContentSource
        {
            private readonly DxfFile.TailMap _map;

            internal MapSource(DxfFile.TailMap map) => _map = map;

            internal override int Count => _map.Count;

            internal override int Code(int index) => _map.Code(index);

            internal override string Value(int index) => _map.Value(index);

            internal override bool Is(int index, int code, string text) => _map.Is(index, code, text);
        }

        /// <summary>
        /// The groups of a kept TABLECONTENT as <see cref="DxfFile.GroupsOf"/> gives them, in one list, and where each cell's groups
        /// stand in it, so that a group is found by its place and the cell it is in changed by itself.
        /// </summary>
        private sealed class ContentFlat : IReadOnlyList<DxfTypedValue>
        {
            private readonly List<KeyValuePair<int, string>> _groups;
            private readonly List<ContentSpan> _cells;

            internal ContentFlat(ContentBytes kept)
            {
                Kept = kept;
                _groups = new List<KeyValuePair<int, string>>(kept.Head.Count + kept.Grid.CountGroups());
                _groups.AddRange(kept.Head);
                _cells = new List<ContentSpan>(kept.Grid.Rows.Count * (kept.Grid.Columns.Count + 1));
                kept.Grid.AddGroupsTo(_groups, _cells);
            }

            /// <summary>The kept TABLECONTENT the groups are those of.</summary>
            internal ContentBytes Kept { get; }

            public int Count => _groups.Count;

            public DxfTypedValue this[int index]
            {
                get
                {
                    KeyValuePair<int, string> group = _groups[index];
                    return DxfTypedValue.Read(group.Key, group.Value);
                }
            }

            public IEnumerator<DxfTypedValue> GetEnumerator()
            {
                for (int i = 0; i < _groups.Count; i++)
                {
                    yield return this[i];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>The cell whose groups hold the place given, and where they begin and end in the list; null where no cell's do.</summary>
            internal ContentGridCell CellAt(int index, out int start, out int end)
            {
                int low = 0;
                int high = _cells.Count - 1;

                while (low <= high)
                {
                    int middle = (low + high) / 2;
                    ContentSpan span = _cells[middle];

                    if (index < span.Start)
                    {
                        high = middle - 1;
                    }
                    else if (index >= span.End)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        start = span.Start;
                        end = span.End;
                        return span.Cell;
                    }
                }

                start = end = -1;
                return null;
            }
        }

        /// <summary>Where the groups of a cell of the TABLECONTENT stand among all the groups of its record: from <see cref="Start"/> to before <see cref="End"/>.</summary>
        private readonly struct ContentSpan
        {
            internal ContentSpan(int start, int end, ContentGridCell cell)
            {
                Start = start;
                End = end;
                Cell = cell;
            }

            internal int Start { get; }

            internal int End { get; }

            internal ContentGridCell Cell { get; }
        }
    }
}
