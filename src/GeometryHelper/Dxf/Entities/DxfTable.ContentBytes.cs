using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfTable
    {
        // The TABLECONTENT of the table as it was read for the merge before, with where each of its cells and its list of merged
        // ranges stand in its bytes (ContentMergedAlone); null until a merge reads it.
        private ContentBytes _contentBytes;

        // Whether the TABLECONTENT was found not to stand as the file writes its groups, so that a writing of the groups a merge
        // changes in their places would not give the bytes a writing of every group gives: it is then written whole, every time.
        private bool _contentUnfit;

        /// <summary>
        /// A TABLECONTENT read, and the places of its cells and of its list of merged ranges in the bytes of its record, kept up
        /// with the edits of <see cref="ContentMergedAlone"/>; the places are those of the record <see cref="Holds"/> says.
        /// </summary>
        private sealed class ContentBytes
        {
            internal ContentBytes(
                DxfFile.Rec record,
                ContentGrid grid,
                int[][] starts,
                int[][] ends,
                int mergesStart,
                int mergesEnd
            )
            {
                Record = record;
                Grid = grid;
                Starts = starts;
                Ends = ends;
                MergesStart = mergesStart;
                MergesEnd = mergesEnd;
            }

            internal DxfFile.Rec Record { get; set; }

            internal ContentGrid Grid { get; }

            /// <summary>Where each cell begins and ends, by row and column, counted from the record's start.</summary>
            internal int[][] Starts { get; }

            internal int[][] Ends { get; }

            /// <summary>Where the count of merged ranges and the ranges begin and end; nought where the ranges were not read.</summary>
            internal int MergesStart { get; set; }

            internal int MergesEnd { get; set; }

            /// <summary>Whether the places are of the record as it stands: its very bytes.</summary>
            internal bool Holds(DxfFile.Rec record) =>
                ReferenceEquals(Record.Bytes, record.Bytes) && Record.Start == record.Start && Record.End == record.End;
        }

        /// <summary>
        /// The TABLECONTENT with the places of its cells, read where the kept one is not of the record as it stands; null where
        /// it is not in the form AutoCAD writes, or its groups do not stand as the file writes them (<see cref="_contentUnfit"/>).
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

            if (grid == null)
            {
                return null;
            }

            if (!file.TailStandsAsWritten(map, grid.From))
            {
                _contentUnfit = true;
                return null;
            }

            var starts = new int[grid.Rows.Count][];
            var ends = new int[grid.Rows.Count][];

            for (int r = 0; r < starts.Length; r++)
            {
                starts[r] = new int[grid.Rows[r].Cells.Count];
                ends[r] = new int[starts[r].Length];

                for (int c = 0; c < starts[r].Length; c++)
                {
                    ContentGridCell cell = grid.Rows[r].Cells[c];
                    starts[r][c] = map.Places[2 * (grid.From + cell.GroupFrom)];
                    ends[r][c] = map.Places[2 * (grid.From + cell.GroupTo) - 1];
                }
            }

            int mergesStart = 0;
            int mergesEnd = 0;

            if (grid.Merges != null)
            {
                mergesStart = map.Places[2 * (grid.From + grid.MergesFrom)];
                mergesEnd = map.Places[2 * (grid.From + grid.MergesTo) - 1];
            }

            _contentBytes = new ContentBytes(record, grid, starts, ends, mergesStart, mergesEnd);
            return _contentBytes;
        }

        /// <summary>
        /// <see cref="ContentMerged"/> written into the bytes of the cells it empties and of the list of merged ranges, the rest
        /// of the TABLECONTENT copied as it is, where the TABLECONTENT is one that is read once and kept: a merge of a table of
        /// 931 cells costs the copy of its bytes, not a reading and a writing of its 100 000 groups. The bytes are those the
        /// whole writing gives, as the groups it leaves stand as the file writes them (<see cref="ContentBytesOf"/>). False,
        /// nothing changed, where the content is not so, or a cell it empties holds a field (the fields it lets go of are erased
        /// by the whole writing).
        /// </summary>
        private bool ContentMergedAlone(DxfCellRange range, bool merged, bool empty)
        {
            if (_contentUnfit || IsErased)
            {
                return false;
            }

            DxfItem content = IsResident ? ContentItem() : null;

            if (content == null)
            {
                return true;
            }

            ContentBytes kept = ContentBytesOf(content);

            if (kept == null)
            {
                return false;
            }

            ContentGrid grid = kept.Grid;
            var emptied = new List<int[]>();

            for (int r = range.TopRow; empty && r <= range.BottomRow && r < grid.Rows.Count; r++)
            {
                for (int c = range.LeftColumn; c <= range.RightColumn && c < grid.Rows[r].Cells.Count; c++)
                {
                    if (
                        (r != range.TopRow || c != range.LeftColumn)
                        && FieldsOf(grid.Rows[r].Cells[c].Groups).Count > 0
                    )
                    {
                        return false;
                    }
                }
            }

            for (int r = range.TopRow; empty && r <= range.BottomRow && r < grid.Rows.Count; r++)
            {
                for (int c = range.LeftColumn; c <= range.RightColumn && c < grid.Rows[r].Cells.Count; c++)
                {
                    if (
                        (r != range.TopRow || c != range.LeftColumn) && EmptyContent(grid, grid.Rows[r].Cells[c].Groups)
                    )
                    {
                        emptied.Add(new[] { r, c });
                    }
                }
            }

            bool rangesChanged = false;

            if (grid.Merges != null)
            {
                var ranges = grid.Merges.FindAll(m =>
                    m[0] > range.BottomRow || m[2] < range.TopRow || m[1] > range.RightColumn || m[3] < range.LeftColumn
                );

                if (merged)
                {
                    ranges.Add(new[] { range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn });
                    ranges.Sort(CompareRanges);
                }

                rangesChanged = !SameRanges(ranges, grid.Merges);
                grid.Merges = ranges;
            }

            if (emptied.Count == 0 && !rangesChanged)
            {
                return true;
            }

            DxfFile file = Item.File;
            var from = new List<int>();
            var to = new List<int>();
            var middle = new List<byte[]>();

            foreach (int[] cell in emptied)
            {
                from.Add(kept.Starts[cell[0]][cell[1]]);
                to.Add(kept.Ends[cell[0]][cell[1]]);
                middle.Add(file.BytesOfGroups(grid.Rows[cell[0]].Cells[cell[1]].Groups));
            }

            if (rangesChanged)
            {
                from.Add(kept.MergesStart);
                to.Add(kept.MergesEnd);
                middle.Add(file.BytesOfGroups(grid.MergeGroups()));
            }

            // The cells and ranges read are changed already: kept again once the record is written, not if it is not.
            _contentBytes = null;
            DxfFile.Rec written = file.SpliceBytes(content, from, to, middle);

            // The places of what stands after each edit move on by what the edit grew or shrank by.
            int shift = 0;
            int next = 0;

            for (int r = 0; r < kept.Starts.Length; r++)
            {
                for (int c = 0; c < kept.Starts[r].Length; c++)
                {
                    bool own = next < emptied.Count && emptied[next][0] == r && emptied[next][1] == c;
                    kept.Starts[r][c] += shift;

                    if (own)
                    {
                        shift += middle[next].Length - (to[next] - from[next]);
                        next++;
                    }

                    kept.Ends[r][c] += shift;
                }
            }

            kept.MergesStart += shift;
            kept.MergesEnd += shift;

            if (rangesChanged)
            {
                kept.MergesEnd = kept.MergesStart + middle[middle.Count - 1].Length;
            }

            kept.Record = written;
            _contentBytes = kept;
            return true;
        }
    }
}
