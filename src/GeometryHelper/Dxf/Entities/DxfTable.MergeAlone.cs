using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfTable
    {
        /// <summary>
        /// The groups of the cells of a table by their index, row by row, for the edit of the cells a merge changes: all of them
        /// held in an array (the record read whole), or read one by one as they are asked for from the cell's own bytes, each
        /// kept as it was read as well as changed, so that the merge reads and writes the few cells it changes and not every one.
        /// </summary>
        private sealed class CellSlices
        {
            private readonly List<KeyValuePair<int, string>>[] _all;
            private readonly Func<int, List<KeyValuePair<int, string>>> _load;
            private readonly Dictionary<int, List<KeyValuePair<int, string>>> _held;
            private readonly Dictionary<int, List<KeyValuePair<int, string>>> _original;
            private readonly List<int> _set = new List<int>();

            /// <summary>The cells of an array, read and changed in it.</summary>
            internal CellSlices(List<KeyValuePair<int, string>>[] all)
            {
                _all = all;
            }

            /// <summary>The cells read by <paramref name="load"/> as they are first asked for.</summary>
            internal CellSlices(Func<int, List<KeyValuePair<int, string>>> load)
            {
                _load = load;
                _held = new Dictionary<int, List<KeyValuePair<int, string>>>();
                _original = new Dictionary<int, List<KeyValuePair<int, string>>>();
            }

            internal List<KeyValuePair<int, string>> this[int index]
            {
                get
                {
                    if (_all != null)
                    {
                        return _all[index];
                    }

                    if (!_held.TryGetValue(index, out List<KeyValuePair<int, string>> groups))
                    {
                        groups = _load(index);
                        _original.Add(index, new List<KeyValuePair<int, string>>(groups));
                        _held.Add(index, groups);
                    }

                    return groups;
                }
                set
                {
                    if (_all != null)
                    {
                        _all[index] = value;
                        return;
                    }

                    _ = this[index];
                    _held[index] = value;

                    if (!_set.Contains(index))
                    {
                        _set.Add(index);
                    }
                }
            }

            /// <summary>The groups of a cell as they were read, before any change.</summary>
            internal List<KeyValuePair<int, string>> OriginalOf(int index)
            {
                _ = this[index];
                return _original[index];
            }

            /// <summary>The cells given other groups, in the order they were given.</summary>
            internal List<int> Set => _set;
        }

        /// <summary>
        /// The places of the cells where a merge or an unmerge may change only the cells it meets and the table's content: the
        /// cells' own bytes are known (<see cref="AloneCells"/>), the table's record holds its cells, heights and widths, and
        /// every cell is as a writing of the whole record leaves it, its merging as the blocks of merged cells say and its link
        /// as the links the table's cells name say (<see cref="Rebuilt"/>), so that a cell the merge does not meet is left as it
        /// is. The blocks of merged cells are read once for the places and kept (<see cref="CellBytes.Blocks"/>). Null where any
        /// of these is not so: the record is then written whole, which leaves it so.
        /// </summary>
        private CellBytes AloneCellsToMerge()
        {
            CellBytes cells = AloneCells(out _);

            if (cells == null || cells.Blocks != null)
            {
                return cells;
            }

            // The cells written and not yet put in the record are put in it by the reading, the places of the cells moved with them.
            Layout layout = Read();
            List<DxfCellRange> blocks = LeftAsRebuilt(layout);
            cells = blocks == null ? null : AloneCells(out _);

            if (cells == null || cells.Rows != layout.Rows || cells.Columns != layout.Columns)
            {
                return null;
            }

            cells.Blocks = blocks;
            return cells;
        }

        /// <summary>
        /// The blocks of merged cells of a layout where each cell stands as <see cref="Rebuild"/> would write it, its merging
        /// and its link as the blocks and links of the cells say; null where the layout does not hold its cells, heights and
        /// widths, or a cell would be written otherwise.
        /// </summary>
        private static List<DxfCellRange> LeftAsRebuilt(Layout layout)
        {
            if (!Holds(layout) || layout.Columns < 1)
            {
                return null;
            }

            int columns = layout.Columns;
            List<DxfCellRange> blocks = Blocks(layout);
            int[] owner = Owners(layout.Rows, columns, blocks);
            LinkedRange[] anchored = Anchors(layout.CellStart.Count, columns, Links(layout));

            for (int k = 0; k < owner.Length; k++)
            {
                List<KeyValuePair<int, string>> cell = Slice(layout, k);

                if (
                    !ReferenceEquals(
                        Rebuilt(cell, k, owner[k] < 0 ? null : blocks[owner[k]], columns, anchored[k]),
                        cell
                    )
                )
                {
                    return null;
                }
            }

            return blocks;
        }

        /// <summary>
        /// A cell's groups as <see cref="Rebuild"/> writes them: its merging as its block says (none for a cell of no block, the
        /// block's width and height for its top left cell, merged for the others), and a text cell the link given, or none,
        /// where a block cell keeps its 340 and a link it is the top left cell of goes. The same list where they stand so.
        /// </summary>
        private static List<KeyValuePair<int, string>> Rebuilt(
            List<KeyValuePair<int, string>> cell,
            int index,
            DxfCellRange block,
            int columns,
            LinkedRange link
        )
        {
            if (block == null)
            {
                cell = Merged(cell, 0, 0, 0);
            }
            else
            {
                bool corner = block.TopRow * columns + block.LeftColumn == index;
                cell = corner
                    ? Merged(cell, 1, block.RightColumn - block.LeftColumn + 1, block.BottomRow - block.TopRow + 1)
                    : Merged(cell, 1, 0, 0);
            }

            // A link is named by a text cell alone: a block cell keeps its 340, its block, and a link it is the top left cell of goes.
            return IsTextCell(cell, 0) ? Linked(cell, link) : cell;
        }

        /// <summary>The first of the blocks of merged cells that holds a cell; null where it is in none.</summary>
        private static DxfCellRange BlockAt(List<DxfCellRange> blocks, int row, int column)
        {
            foreach (DxfCellRange b in blocks)
            {
                if (row >= b.TopRow && row <= b.BottomRow && column >= b.LeftColumn && column <= b.RightColumn)
                {
                    return b;
                }
            }

            return null;
        }

        /// <summary>
        /// What of a cell the blocks of merged cells and the links of the table read: its kind, its flags, its merging and its
        /// link, the groups of its head before its value that give them. A cell edited whose signature changes leaves the blocks
        /// kept for the places (<see cref="CellBytes.Blocks"/>) unknown.
        /// </summary>
        private static string MergeSignature(List<KeyValuePair<int, string>> groups)
        {
            var signature = new StringBuilder();

            for (int i = 0; i < groups.Count; i++)
            {
                int key = groups[i].Key;

                if (key == 301 && groups[i].Value.Trim() == "CELL_VALUE")
                {
                    break;
                }

                if (
                    key == 171
                    || key == 92
                    || key == 173
                    || key == 175
                    || key == 176
                    || key == 340
                    || key == 93
                    || key == 94
                    || key == 96
                )
                {
                    signature.Append(key).Append('=').Append(groups[i].Value).Append(';');
                }
            }

            return signature.ToString();
        }

        /// <summary>The handles of the fields a cell's groups point at (344), each once.</summary>
        private static List<string> FieldsOfCell(List<KeyValuePair<int, string>> groups)
        {
            var fields = new List<string>();

            foreach (KeyValuePair<int, string> group in groups)
            {
                string handle = group.Key == 344 ? group.Value.Trim() : string.Empty;

                if (handle.Length > 0 && !fields.Contains(handle, StringComparer.OrdinalIgnoreCase))
                {
                    fields.Add(handle);
                }
            }

            return fields;
        }

        /// <summary>
        /// <see cref="MergeCells"/> where the cells' own bytes are known (<see cref="AloneCellsToMerge"/>): the cells the merge
        /// changes are read from their bytes and written into their places, as the setters of one cell write theirs, and the
        /// blocks of merged cells kept from the call before, not read from every cell; the record is the one the whole writing
        /// would give.
        /// </summary>
        private void MergeAlone(CellBytes cells, DxfCellRange range)
        {
            int rows = cells.Rows;
            int columns = cells.Columns;
            CheckRange(range, rows, columns, nameof(range));

            if (range.TopRow == range.BottomRow && range.LeftColumn == range.RightColumn)
            {
                return;
            }

            var blocks = new List<DxfCellRange>();
            bool changed = false;

            foreach (DxfCellRange b in cells.Blocks)
            {
                if (!Meets(b, range))
                {
                    blocks.Add(b);
                }
                else if (Within(b, range))
                {
                    changed |= !b.Equals(range);
                }
                else
                {
                    throw new ArgumentException(
                        "The block of merged cells "
                            + b
                            + " reaches across the edge of the range "
                            + range
                            + ": unmerge it first, or merge a range that holds it whole.",
                        nameof(range)
                    );
                }
            }

            changed |= !cells.Blocks.Contains(
                new DxfCellRange(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn)
            );
            AddBlock(blocks, range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn);

            if (!changed)
            {
                return;
            }

            var slices = new CellSlices(index => GroupsOfCell(cells, cells.Pending, index));
            EmptyMerged(rows, columns, slices, Read, range, Item.File.Supports(1021));
            var touched = new List<int>(slices.Set);

            for (int r = range.TopRow; r <= range.BottomRow; r++)
            {
                for (int c = range.LeftColumn; c <= range.RightColumn; c++)
                {
                    int index = r * columns + c;

                    if (!touched.Contains(index))
                    {
                        touched.Add(index);
                    }

                    // The cells it empties are emptied in the session, of no value or not before, on load too (D33c r10, D33d a01,
                    // a06).
                    if (r != range.TopRow || c != range.LeftColumn)
                    {
                        MarkCell(rows, columns, index, SessionEmptied);
                    }
                }
            }

            touched.Sort();
            var dropped = new List<string>();

            foreach (int index in touched)
            {
                int row = index / columns;
                int column = index % columns;
                List<KeyValuePair<int, string>> original = slices.OriginalOf(index);
                LinkedRange link = IsTextCell(original, 0)
                    ? LinkOf(original, 0, original.Count, row, column, rows, columns)
                    : null;
                List<KeyValuePair<int, string>> made = Rebuilt(
                    slices[index],
                    index,
                    BlockAt(blocks, row, column),
                    columns,
                    link
                );

                if (SameGroups(made, original))
                {
                    continue;
                }

                List<string> after = FieldsOfCell(made);
                dropped.AddRange(
                    FieldsOfCell(original).FindAll(h => !after.Contains(h, StringComparer.OrdinalIgnoreCase))
                );
                SpliceCell(cells, index, made);
            }

            cells.Blocks = blocks;

            if (dropped.Count > 0)
            {
                EraseFields(dropped);
            }

            ContentMerged(range, true, true);
        }

        /// <summary><see cref="UnmergeCells"/> where the cells' own bytes are known, as <see cref="MergeAlone"/> is of <see cref="MergeCells"/>.</summary>
        private void UnmergeAlone(CellBytes cells, DxfCellRange range)
        {
            int columns = cells.Columns;
            CheckRange(range, cells.Rows, columns, nameof(range));
            var kept = new List<DxfCellRange>();
            var indices = new List<int>();

            foreach (DxfCellRange b in cells.Blocks)
            {
                if (!Meets(b, range))
                {
                    kept.Add(b);
                    continue;
                }

                for (int r = b.TopRow; r <= b.BottomRow; r++)
                {
                    for (int c = b.LeftColumn; c <= b.RightColumn; c++)
                    {
                        indices.Add(r * columns + c);
                    }
                }
            }

            if (indices.Count == 0)
            {
                return;
            }

            indices.Sort();

            foreach (int index in indices)
            {
                int row = index / columns;
                int column = index % columns;
                List<KeyValuePair<int, string>> original = GroupsOfCell(cells, cells.Pending, index);
                LinkedRange link = IsTextCell(original, 0)
                    ? LinkOf(original, 0, original.Count, row, column, cells.Rows, columns)
                    : null;
                List<KeyValuePair<int, string>> made = Rebuilt(
                    new List<KeyValuePair<int, string>>(original),
                    index,
                    BlockAt(kept, row, column),
                    columns,
                    link
                );

                if (!SameGroups(made, original))
                {
                    SpliceCell(cells, index, made);
                }
            }

            cells.Blocks = kept;
            ContentMerged(range, false);
        }
    }
}
