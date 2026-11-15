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
        /// cell's place being where it ends, as <see cref="DxfFile.OwnGroupsOf(DxfItem, int, List{int})"/> places the groups.
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

            /// <summary>Where each cell of the record begins, from its start; moved on with every cell written.</summary>
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
        /// groups may stand otherwise than the file writes them), holds bytes among its groups that are none of theirs, or the
        /// cell is the last, whose end is where the table's own groups begin, which the cell's flags decide. The row and the
        /// column are checked as <see cref="Layout.CellOf"/> checks them.
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

            if (index >= cells.From.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(row),
                    row,
                    "The table's record holds "
                        + cells.From.Length.ToString(CultureInfo.InvariantCulture)
                        + " cells, fewer than its rows by its columns."
                );
            }

            if (index == cells.From.Length - 1)
            {
                return null;
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
            var from = new int[layout.CellStart.Count];

            for (int k = 0; k < from.Length; k++)
            {
                from[k] = places[2 * (layout.CellStart[k] + first)];
            }

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
        }
    }
}
