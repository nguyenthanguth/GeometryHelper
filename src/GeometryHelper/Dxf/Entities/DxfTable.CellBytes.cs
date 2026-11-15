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
            internal CellBytes(DxfFile.Rec record, int rows, int columns, int[] from)
            {
                Rows = rows;
                Columns = columns;
                From = from;
                Held(record);
            }

            internal int Rows { get; }

            internal int Columns { get; }

            /// <summary>Where each cell of the record begins, from its start, and where the last ends; moved on with every cell written.</summary>
            internal int[] From { get; }

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
        /// The cell read from its own bytes, null where they are not known: the record is not one this table wrote whole (its
        /// groups may stand otherwise than the file writes them), or holds bytes among its groups that are none of theirs. The
        /// last cell ends where the table's own groups begin, which its flags decide as it stands. The row and the column are
        /// checked as <see cref="Layout.CellOf"/> checks them.
        /// </summary>
        private CellView CellAlone(int row, int col)
        {
            if (!_mergedAsLoaded || IsErased)
            {
                return null;
            }

            DxfFile file = Item.File;
            DxfFile.Rec record = file.RecordAt(Item, 0);

            if (!IsWritten(record))
            {
                return null;
            }

            CellBytes cells = _cellBytes;

            if (cells == null || !cells.Holds(record))
            {
                cells = PlacesOfCells(record);
                _cellBytes = cells;
            }

            if (cells == null)
            {
                return null;
            }

            CheckIndex(row, cells.Rows, nameof(row), "row");
            CheckIndex(col, cells.Columns, nameof(col), "column");
            int index = row * cells.Columns + col;

            if (index >= cells.From.Length - 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(row),
                    row,
                    "The table's record holds "
                        + (cells.From.Length - 1).ToString(CultureInfo.InvariantCulture)
                        + " cells, fewer than its rows by its columns."
                );
            }

            List<KeyValuePair<int, string>> groups = file.GroupsBetween(
                Item,
                0,
                cells.From[index],
                cells.From[index + 1]
            );
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
            return new CellBytes(record, layout.Rows, layout.Columns, from);
        }

        /// <summary>
        /// Writes the groups of one cell into the place of the bytes it was read from, the rest of the record copied as it is,
        /// and moves the places of the cells after it on by what it grew or shrank by. The bytes are those a writing of every
        /// group of the record would give, as the groups left as they are stand as the file writes them.
        /// </summary>
        private void SpliceCell(CellBytes cells, int index, List<KeyValuePair<int, string>> groups)
        {
            DxfFile file = Item.File;
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
            cells.Held(record);
            NoteWritten(record);

            // The layout read before, if it is of the record as it stood: the cell's groups are noted, to be put into it when it
            // is next read, so that reading it does not read every group of the record again, and a run of cells written with no
            // reading between them costs it a note each.
            if (!layoutHeld || index >= _layout.CellStart.Count || _pending.Count >= MostPending)
            {
                _pending.Clear();
                return;
            }

            _pending.Add(
                new KeyValuePair<int, List<KeyValuePair<int, string>>>(
                    index,
                    new List<KeyValuePair<int, string>>(groups)
                )
            );
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
    }
}
