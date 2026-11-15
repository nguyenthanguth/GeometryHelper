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
            internal ContentBytes(DxfFile.Rec record, ContentGrid grid, int gridAt, int gridEnd)
            {
                Record = record;
                Grid = grid;
                GridAt = gridAt;
                GridEnd = gridEnd;
            }

            internal DxfFile.Rec Record { get; set; }

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
            ContentGrid grid = ContentGrid.Read(map.Groups);

            if (grid == null || !file.TailStandsAsWritten(map, grid.From))
            {
                _contentUnfit = true;
                return null;
            }

            grid.HeadStartByte = map.Places[2 * grid.From];

            foreach (ContentRow row in grid.Rows)
            {
                row.Start = map.Places[2 * (grid.From + row.GroupFrom)];
                row.End = map.Places[2 * (grid.From + row.GroupTo) - 1];

                foreach (ContentGridCell cell in row.Cells)
                {
                    cell.Start = map.Places[2 * (grid.From + cell.GroupFrom)];
                    cell.End = map.Places[2 * (grid.From + cell.GroupTo) - 1];
                }
            }

            grid.HeadEndByte = grid.Rows.Count > 0 ? grid.Rows[0].Start : -1;
            int tailAt = grid.From + grid.TailAt;
            grid.TailStartByte = tailAt < map.Groups.Count ? map.Places[2 * tailAt] : map.DataEnd;

            if (grid.Merges != null)
            {
                grid.MergesStartByte = map.Places[2 * (grid.From + grid.MergesFrom)];
                grid.MergesEndByte = map.Places[2 * (grid.From + grid.MergesTo) - 1];
            }

            _contentBytes = new ContentBytes(record, grid, map.Places[2 * grid.From], map.DataEnd);
            return _contentBytes;
        }
    }
}
