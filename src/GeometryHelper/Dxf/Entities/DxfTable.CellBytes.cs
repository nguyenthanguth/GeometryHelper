using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfTable
    {
        // Where each cell stands in the bytes of the record the table wrote last whole (SetLines, SetTextString and the setters
        // of a cell's look), kept up with the edits that change one cell alone (CellAlone). Null until such a cell is edited.
        private CellBytes _cellBytes;

        // The name of the block the table was given when it was added to its drawing, till it is drawn there (Appending,
        // Appended): a name no other entity of the drawing has, as it was made a moment ago.
        private string _freshPicture;

        /// <summary>
        /// The places of the cells in the bytes of a record: where each cell begins, counted from the record's start, the next
        /// cell's place being where it ends, and one place more where the last cell ends and the table's own part begins, as
        /// <see cref="DxfFile.OwnGroupsOf(DxfItem, int, List{int})"/> places the groups.
        /// </summary>
        private sealed class CellBytes
        {
            internal CellBytes(DxfFile.Rec record, int rows, int columns, int[] from, int headFrom, int trailing)
            {
                Rows = rows;
                Columns = columns;
                From = from;
                HeadFrom = headFrom;
                Trailing = trailing;
                Held(record);
            }

            internal int Rows { get; }

            internal int Columns { get; }

            /// <summary>Where the table's own groups begin, those after its first subclass marker, counted from the record's start; the head they make runs on to the first cell.</summary>
            internal int HeadFrom { get; }

            /// <summary>How many bytes the record holds after its last own group (extended data, groups of an application): what no write of the table's own groups or cells moves.</summary>
            internal int Trailing { get; }

            /// <summary>Where each cell of the record begins, from its start, and where the last ends; moved on with every cell written, or, while cells are pending, when they are put in the record.</summary>
            internal int[] From { get; }

            /// <summary>The cells written and not yet put in the record (<see cref="DxfFile.PendingCells"/>), the places in <see cref="From"/> being those of the record before them; null where there are none.</summary>
            internal DxfFile.PendingCells Pending { get; set; }

            internal byte[] Bytes { get; private set; }

            internal int Start { get; private set; }

            internal int End { get; private set; }

            /// <summary>Takes the record as the one the places are of.</summary>
            internal void Held(DxfFile.Rec record)
            {
                Bytes = record.Bytes;
                Start = record.Start;
                End = record.End;
            }

            /// <summary>Whether the places are of the record as it stands: its very bytes.</summary>
            internal bool Holds(DxfFile.Rec record) =>
                ReferenceEquals(Bytes, record.Bytes) && Start == record.Start && End == record.End;
        }

        /// <summary>The cells of a table as the edit of its lines reads them: those of a layout, or those of the record's bytes, read one by one (<see cref="AloneCellSource"/>).</summary>
        private interface ICells
        {
            int Rows { get; }

            int Columns { get; }

            /// <summary>The number of cells the record holds.</summary>
            int Count { get; }

            CellView CellAt(int row, int col);
        }

        /// <summary>
        /// The cells of the record read from their own bytes, each once for the edit that holds the source: a cell asked for
        /// again is the one read. The cells are the groups the record holds, those written and not yet put in it among them.
        /// </summary>
        private sealed class AloneCellSource : ICells
        {
            private readonly DxfTable _table;
            private readonly CellBytes _cells;
            private readonly DxfFile.PendingCells _pending;
            private readonly Dictionary<int, CellView> _views = new Dictionary<int, CellView>();

            internal AloneCellSource(DxfTable table, CellBytes cells, DxfFile.PendingCells pending)
            {
                _table = table;
                _cells = cells;
                _pending = pending;
            }

            public int Rows => _cells.Rows;

            public int Columns => _cells.Columns;

            public int Count => _cells.From.Length - 1;

            public CellView CellAt(int row, int col)
            {
                int index = row * _cells.Columns + col;

                if (!_views.TryGetValue(index, out CellView view))
                {
                    List<KeyValuePair<int, string>> groups = _table.GroupsOfCell(_cells, _pending, index);
                    view = new CellView(new DxfDimGroups(groups), 0, groups.Count);
                    _views.Add(index, view);
                }

                return view;
            }
        }

        /// <summary>
        /// The own groups of the table's layout, as <see cref="Own"/> reads them, that note the record they write as one whose
        /// every own group stands as the file writes a group, which is what <see cref="CellAlone"/> splices into.
        /// </summary>
        private DxfDimGroups OwnNoting() => new DxfDimGroups(Item) { Rewrote = NoteWritten };

        private void NoteWritten(DxfFile.Rec record)
        {
            _writtenBytes = record.Bytes;
            _writtenStart = record.Start;
            _writtenEnd = record.End;
            _writtenPlaces = null;
        }

        /// <summary>The table's layout read anew to be changed and written, as <see cref="ReadToWrite"/> reads it, noting what it writes.</summary>
        private Layout ReadToEdit() => MergeAsLoaded() ?? new Layout(OwnNoting());

        /// <summary>
        /// A cell to be changed and written: read from its own bytes alone where the record is one this table wrote whole, the
        /// groups it changes put back into the place of those bytes and the rest of the record copied as it is, so that setting
        /// the text of every cell of a table of 1 000 rows costs a copy of the record's bytes each, not a reading and a writing
        /// of every group of it; else as <see cref="CellToWrite"/> reads it, the record written whole, which a record not
        /// written whole before is.
        /// </summary>
        private CellView CellToEdit(int row, int col) => CellAlone(row, col) ?? ReadToEdit().CellOf(row, col);

        /// <summary>
        /// The places of the cells in the record as it stands, null where they are not known: the record is not one this table
        /// wrote whole (its groups may stand otherwise than the file writes them), or holds bytes among its groups that are none
        /// of theirs. <paramref name="pending"/> is the cells written and not yet put in the record, which the places are
        /// those of the record before; null where there are none.
        /// </summary>
        private CellBytes AloneCells(out DxfFile.PendingCells pending)
        {
            pending = null;

            if (!_mergedAsLoaded || IsErased)
            {
                return null;
            }

            DxfFile file = Item.File;
            pending = file.PendingOf(Item);
            CellBytes cells = _cellBytes;

            // Cells this table wrote are kept apart from the record, which is not read: its places are those of the record they
            // were counted from. Else the record is read, as it stands, which puts in the cells any other kept.
            if (pending == null || cells == null || !ReferenceEquals(cells.Pending, pending))
            {
                DxfFile.Rec record = file.RecordAt(Item, 0);
                pending = null;

                if (!IsWritten(record))
                {
                    return null;
                }

                if (cells == null || cells.Pending != null || !cells.Holds(record))
                {
                    cells = PlacesOfCells(record);
                    _cellBytes = cells;
                }
            }

            return cells;
        }

        /// <summary>The groups of a cell, read from its own bytes: those given it where cells are pending, else those of the record's.</summary>
        private List<KeyValuePair<int, string>> GroupsOfCell(CellBytes cells, DxfFile.PendingCells pending, int index)
        {
            DxfFile file = Item.File;

            if (pending != null)
            {
                DxfFile.Rec held = pending.Held(index);
                return file.GroupsBetween(held, 0, held.End - held.Start);
            }

            return file.GroupsBetween(Item, 0, cells.From[index], cells.From[index + 1]);
        }

        /// <summary>
        /// The cell read from its own bytes, null where they are not known (<see cref="AloneCells"/>). The last cell ends where
        /// the table's own groups begin, which its flags decide as they stand, and an edit of the cell may change them: the place
        /// kept for its end is found again once it is written (<see cref="EndOfLastCell"/>). The row and the column are checked
        /// as <see cref="Layout.CellOf"/> checks them.
        /// </summary>
        private CellView CellAlone(int row, int col)
        {
            CellBytes cells = AloneCells(out DxfFile.PendingCells pending);

            if (cells == null)
            {
                return null;
            }

            CheckCell(row, col, cells.Rows, cells.Columns, cells.From.Length - 1);
            int index = row * cells.Columns + col;
            List<KeyValuePair<int, string>> groups = GroupsOfCell(cells, pending, index);
            return new CellView(new DxfDimGroups(groups, made => SpliceCell(cells, index, made)), 0, groups.Count);
        }

        /// <summary>Where the cells of the record stand, read from its layout and the places of its groups; null where the places are not known.</summary>
        private CellBytes PlacesOfCells(DxfFile.Rec record)
        {
            List<int> places = _writtenPlaces;

            if (places == null)
            {
                places = _writtenPlaces = PlacesOf(Read());
            }

            if (places.Count == 0)
            {
                return null;
            }

            Layout layout = Read();
            int first = layout.Groups.Marked ? 1 : 0;
            int cells = layout.CellStart.Count;
            var from = new int[cells + 1];

            for (int k = 0; k < cells; k++)
            {
                from[k] = places[2 * (layout.CellStart[k] + first)];
            }

            // Where the last cell ends: at the first group of the table's own part after the cells, or the end of the last group.
            int end = layout.CellsEnd + first;
            from[cells] = 2 * end < places.Count ? places[2 * end] : places[places.Count - 1];
            return new CellBytes(
                record,
                layout.Rows,
                layout.Columns,
                from,
                2 * first < places.Count ? places[2 * first] : places[places.Count - 1],
                record.End - record.Start - places[places.Count - 1]
            );
        }

        /// <summary>
        /// Writes the groups of one cell into the place of the bytes it was read from, the rest of the record copied as it is,
        /// and moves the places of the cells after it on by what it grew or shrank by. The bytes are those a writing of every
        /// group of the record would give, as the groups left as they are stand as the file writes them.
        /// </summary>
        private void SpliceCell(CellBytes cells, int index, List<KeyValuePair<int, string>> groups)
        {
            DxfFile file = Item.File;
            bool last = index == cells.From.Length - 2;

            // Kept apart from the record, which is written once when it is next read, so that a run of cells set costs the bytes
            // of each, not a copy of the record for each; where the file cannot take the cell so, it is written in its place,
            // and so is the last cell, whose end is found again from the record as it is left.
            if (!last && file.CanDefer(Item))
            {
                DxfFile.PendingCells pending = cells.Pending;

                if (pending == null || !ReferenceEquals(file.PendingOf(Item), pending))
                {
                    cells.Pending = null;

                    // Whether the layout read last is that of the record the cells are written into: the cells are then noted.
                    DxfFile.Rec held = file.RecordAt(Item, 0);
                    _noting = HeldLayout(held) || HeldPatched(held);
                    pending = file.BeginPending(Item, cells.From, (settled, from) => Settled(cells, settled, from));
                    cells.Pending = pending;
                }

                if (file.PutCell(Item, pending, index, file.BytesOfGroups(groups)))
                {
                    Note(index, groups);
                    return;
                }
            }

            // The cells kept apart are put in the record, and the places of the cells moved with them, before it is written in.
            if (cells.Pending != null)
            {
                file.RecordAt(Item, 0);
            }

            DxfFile.Rec before = file.RecordAt(Item, 0);
            bool layoutHeld = HeldLayout(before) || HeldPatched(before);
            int shift =
                file.SpliceRecord(Item, 0, cells.From[index], cells.From[index + 1], groups, false)
                - (cells.From[index + 1] - cells.From[index]);

            for (int k = index + 1; shift != 0 && k < cells.From.Length; k++)
            {
                cells.From[k] += shift;
            }

            DxfFile.Rec record = file.RecordAt(Item, 0);

            if (last)
            {
                cells.From[index + 1] = EndOfLastCell(cells, index, groups, record);
            }

            cells.Held(record);
            NoteWritten(record);

            // The layout read before, if it is of the record as it stood: the cell's groups are noted, to be put into it when it
            // is next read, so that reading it does not read every group of the record again, and a run of cells written with no
            // reading between them costs it a note each.
            _noting = layoutHeld;

            if (Note(index, groups))
            {
                NotedIn(record);
            }
        }

        /// <summary>
        /// Where the last cell ends once the groups given are written in its place, as <see cref="Layout"/> places it for the
        /// record as it is left: the flags of the cell decide which of the groups that follow it are its own, and an edit may
        /// change the flags (a text set in the last cell of a table of 2 rows and 2 columns, after its overrides were set,
        /// went into the table's own value when the end was kept from before the edit). Counted from the record's start.
        /// </summary>
        private int EndOfLastCell(
            CellBytes cells,
            int index,
            List<KeyValuePair<int, string>> groups,
            DxfFile.Rec record
        )
        {
            DxfFile file = Item.File;
            int tailFrom = cells.From[index + 1];
            List<KeyValuePair<int, string>> tail = file.GroupsBetween(record, tailFrom, record.End - record.Start);
            var all = new List<KeyValuePair<int, string>>(groups.Count + tail.Count);
            all.AddRange(groups);
            all.AddRange(tail);
            int end = Layout.EndOfLast(all, 0, all.Count);

            // The groups of a record written whole stand as the file writes them, so that their lengths are the bytes they take.
            int from = end < groups.Count ? 0 : groups.Count;
            int at = end < groups.Count ? cells.From[index] : tailFrom;

            for (int i = from; i < end; i++)
            {
                at += file.GroupLength(all[i].Key, all[i].Value);
            }

            return at;
        }

        // Whether the cells written are being noted for the layout (SpliceCell, Note): the layout read last is that of the record
        // they were written into, and no more than MostPending have been noted.
        private bool _noting;

        /// <summary>Notes a cell written, its place and its groups, to be put into the layout read last when it is next read; false, the notes dropped, where the layout is not the record's or there are too many.</summary>
        private bool Note(int index, List<KeyValuePair<int, string>> groups)
        {
            if (!_noting || index >= _layout.CellStart.Count || _pending.Count >= MostPending)
            {
                _noting = false;
                _pending.Clear();
                return false;
            }

            _pending.Add(
                new KeyValuePair<int, List<KeyValuePair<int, string>>>(
                    index,
                    new List<KeyValuePair<int, string>>(groups)
                )
            );
            return true;
        }

        /// <summary>Takes the record as the one the cells noted were written into.</summary>
        private void NotedIn(DxfFile.Rec record)
        {
            _pendingBytes = record.Bytes;
            _pendingStart = record.Start;
            _pendingEnd = record.End;
        }

        /// <summary>Whether the layout read last is that of a record's very bytes.</summary>
        private bool HeldLayout(DxfFile.Rec record) =>
            _layout != null
            && ReferenceEquals(_layoutBytes, record.Bytes)
            && _layoutStart == record.Start
            && _layoutEnd == record.End;

        /// <summary>Whether the layout read last, with the cells written since noted in <see cref="_pending"/>, is that of a record's very bytes.</summary>
        private bool HeldPatched(DxfFile.Rec record) =>
            _layout != null
            && _pending.Count > 0
            && ReferenceEquals(_pendingBytes, record.Bytes)
            && _pendingStart == record.Start
            && _pendingEnd == record.End;

        /// <summary>
        /// The layout read last with the cells written since put into it, in the order they were written; null where the record
        /// is not the one they were written into. The groups of every cell are copied once into a list of their own, as the
        /// layout read before is still held by whoever read it.
        /// </summary>
        private Layout Patched(DxfFile.Rec record)
        {
            if (!HeldPatched(record))
            {
                _pending.Clear();
                return null;
            }

            DxfDimGroups groups = _layout.Groups;
            var list = new List<KeyValuePair<int, string>>(groups.List);
            int cells = _layout.CellStart.Count;
            var starts = new int[cells + 1];
            _layout.CellStart.CopyTo(starts);
            starts[cells] = _layout.CellsEnd;

            foreach (KeyValuePair<int, List<KeyValuePair<int, string>>> cell in _pending)
            {
                int from = starts[cell.Key];
                int to = starts[cell.Key + 1];
                list.RemoveRange(from, to - from);
                list.InsertRange(from, cell.Value);

                for (int k = cell.Key + 1; k <= cells; k++)
                {
                    starts[k] += cell.Value.Count - (to - from);
                }
            }

            _pending.Clear();
            return new Layout(new DxfDimGroups(groups, list));
        }

        /// <summary>The cells written are put in the record: the places of the cells, and the record, are those they now stand in.</summary>
        private void Settled(CellBytes cells, DxfFile.Rec record, int[] from)
        {
            Array.Copy(from, cells.From, from.Length);
            cells.Pending = null;
            cells.Held(record);
            NoteWritten(record);

            // The cells noted are those the record now holds: the layout read last is patched with them when it is next read.
            if (_noting && _pending.Count > 0)
            {
                NotedIn(record);
            }
            else
            {
                _pending.Clear();
            }
        }
    }
}
