using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What the Table Cell tab of AutoCAD 2021 sets of a single cell beyond its text and look: its state (Cell Locking), a block
    /// put into it (Insert Block) and a field or formula it shows (Insert Field, Insert Formula).
    /// </summary>
    public sealed partial class DxfTable
    {
        #region The state of a cell

        /// <summary>
        /// Gets the state of a cell, as AutoCAD's <c>GetCellState(row, column)</c>: the 90 after the LINKEDTABLEDATACELL_BEGIN of
        /// its cell in the TABLECONTENT, where Content Locked is 1 and Format Locked 16. A table with no TABLECONTENT, or one
        /// with no such cell, holds every cell unlocked.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The state; <see cref="DxfCellState.None"/> for a cell never locked.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfCellState GetCellState(int row, int column)
        {
            Read().CellOf(row, column);
            int state = 0;

            if (IsResident)
            {
                EditContent(true, null, grid =>
                {
                    if (row < grid.Rows.Count && column < grid.Rows[row].Cells.Count)
                    {
                        List<KeyValuePair<int, string>> groups = grid.Rows[row].Cells[column].Groups;
                        int at = StateAt(groups);

                        if (
                            at >= 0
                            && int.TryParse(
                                groups[at].Value.Trim(),
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out int read
                            )
                        )
                        {
                            state = read;
                        }
                    }
                });
            }

            return (DxfCellState)state;
        }

        /// <summary>
        /// Sets the state of a cell, as AutoCAD's <c>SetCellState(row, column, state)</c>: Content Locked (1) and Format Locked
        /// (16) written into the cell's state in the TABLECONTENT, the flag <see cref="DxfCellState.Linked"/> of a linked cell
        /// kept as it is whatever the state given says. A table of no TABLECONTENT is given one first, of cells of no content,
        /// as <see cref="SetDataLink(DxfCellRange, DxfDataLink)"/> gives it one: the XRECORD of
        /// ACAD_ROUNDTRIP_2008_TABLE_ENTITY, the table listed in ACDB_RECOMPOSE_DATA, without which AutoCAD reads none. Nothing
        /// is written where the cell is in that state already. A table, its cell and the state it keeps are written to a DXF or a
        /// DWG as they stand; the library itself refuses nothing a locked cell holds, so that its own edits, and Excel-style
        /// ones such as the viewer's, check <see cref="GetCellState"/> first.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="state">The state.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased or not in a drawing, the drawing is one before R2007 (AC1021), which holds no TABLECONTENT, or the TABLECONTENT has no such cell.</exception>
        public void SetCellState(int row, int column, DxfCellState state)
        {
            Read().CellOf(row, column);
            CheckEditable();

            if (!IsResident || !Item.File.Supports(1021))
            {
                throw new InvalidOperationException(
                    "The table holds no TABLECONTENT to keep the state of a cell in: it is not in a drawing, or the drawing is one before R2007 (AC1021)."
                );
            }

            DxfCellState held = GetCellState(row, column);

            if (held == ((state & ~DxfCellState.Linked) | (held & DxfCellState.Linked)))
            {
                return;
            }

            MergeAsLoaded();
            KeepContent();
            bool listed = ContentItem() == null || Listed();

            if (ContentItem() == null)
            {
                NewContent();
            }

            EditContent(true, null, grid =>
            {
                if (row >= grid.Rows.Count || column >= grid.Rows[row].Cells.Count)
                {
                    throw new InvalidOperationException(
                        "The TABLECONTENT of the table "
                            + Handle
                            + " has no cell ("
                            + row.ToString(CultureInfo.InvariantCulture)
                            + ", "
                            + column.ToString(CultureInfo.InvariantCulture)
                            + ")."
                    );
                }

                ContentGridCell cell = grid.Rows[row].Cells[column];
                List<KeyValuePair<int, string>> groups = cell.Groups;
                int at = StateAt(groups);

                if (at < 0)
                {
                    throw new InvalidOperationException(
                        "The cell ("
                            + row.ToString(CultureInfo.InvariantCulture)
                            + ", "
                            + column.ToString(CultureInfo.InvariantCulture)
                            + ") of the TABLECONTENT of the table "
                            + Handle
                            + " has no state (90) after its LINKEDTABLEDATACELL_BEGIN, as AutoCAD writes one."
                    );
                }

                int.TryParse(groups[at].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int old);
                int now = ((int)state & ~(int)DxfCellState.Linked) | (old & (int)DxfCellState.Linked);

                if (now != old)
                {
                    groups[at] = new KeyValuePair<int, string>(90, now.ToString(CultureInfo.InvariantCulture));
                    cell.Dirty = true;
                    grid.Changed = true;
                }
            });

            if (!listed)
            {
                Recompose(StyleHandle());
            }
        }

        /// <summary>
        /// Finds the first cell of a range, row by row, whose state holds any of the flags given, as a check of a whole range in one
        /// reading of the TABLECONTENT where <see cref="GetCellState"/> reads it once for each cell: the Table Cell tab asks it
        /// before it edits cells. A table with no TABLECONTENT holds no state, and is answered at once.
        /// </summary>
        /// <param name="range">The cells, within the table.</param>
        /// <param name="flags">The flags looked for.</param>
        /// <param name="row">The row of the cell found; -1 where none is.</param>
        /// <param name="column">The column of the cell found; -1 where none is.</param>
        /// <returns>Whether a cell was found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the range is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the range reaches past the table's rows or columns.</exception>
        public bool TryFindCellState(DxfCellRange range, DxfCellState flags, out int row, out int column)
        {
            if (range == null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            CheckRange(range, Read(), nameof(range));
            int foundRow = -1;
            int foundColumn = -1;

            if (IsResident && ContentItem() != null)
            {
                EditContent(true, null, grid =>
                {
                    for (int r = range.TopRow; r <= range.BottomRow && foundRow < 0 && r < grid.Rows.Count; r++)
                    {
                        for (int c = range.LeftColumn; c <= range.RightColumn && c < grid.Rows[r].Cells.Count; c++)
                        {
                            List<KeyValuePair<int, string>> groups = grid.Rows[r].Cells[c].Groups;
                            int at = StateAt(groups);

                            if (
                                at >= 0
                                && int.TryParse(
                                    groups[at].Value.Trim(),
                                    NumberStyles.Integer,
                                    CultureInfo.InvariantCulture,
                                    out int read
                                )
                                && (read & (int)flags) != 0
                            )
                            {
                                foundRow = r;
                                foundColumn = c;
                                break;
                            }
                        }
                    }
                });
            }

            row = foundRow;
            column = foundColumn;
            return foundRow >= 0;
        }

        /// <summary>The place of the state (the 90 after LINKEDTABLEDATACELL_BEGIN) among the groups of a cell of the TABLECONTENT; -1 where it has none.</summary>
        private static int StateAt(List<KeyValuePair<int, string>> groups)
        {
            int begin = groups.FindIndex(g => g.Key == 1 && g.Value.Trim() == "LINKEDTABLEDATACELL_BEGIN");
            return begin >= 0 && begin + 1 < groups.Count && groups[begin + 1].Key == 90 ? begin + 1 : -1;
        }

        #endregion

        #region The cell style of a cell

        /// <summary>
        /// Gets the id of the cell style a cell takes, as AutoCAD's <c>CellStyle</c> names it: the 90 after the TABLECELL_BEGIN of
        /// its cell in the TABLECONTENT, 0 where the cell names none and takes its row's, as the cells of a table the library made
        /// do. The ids are those of <see cref="DxfTableStyle.GetCellStyleId"/>: 1 Title, 2 Header, 3 Data, 101 on those a user made.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The id; 0 for none.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public int GetCellStyleId(int row, int column)
        {
            Read().CellOf(row, column);
            int id = 0;

            if (IsResident)
            {
                EditContent(true, null, grid =>
                {
                    if (row < grid.Rows.Count && column < grid.Rows[row].Cells.Count)
                    {
                        List<KeyValuePair<int, string>> groups = grid.Rows[row].Cells[column].Groups;
                        int at = StyleIdAt(groups);

                        if (at >= 0)
                        {
                            int.TryParse(
                                groups[at].Value.Trim(),
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out id
                            );
                        }
                    }
                });
            }

            return id;
        }

        /// <summary>
        /// Gives a cell a cell style, as AutoCAD's <c>SetCellStyle</c> does: the id written into the cell's TABLECELL_BEGIN in the
        /// TABLECONTENT, which a table of none is given first (<see cref="SetCellState"/>). The look the style gives is not
        /// written here: it is the cell's own overrides, which the caller sets.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="id">The id of a cell style of the table's style; 0 to take the row's.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased or not in a drawing, the drawing is one before R2007, or the TABLECONTENT has no such cell.</exception>
        public void SetCellStyleId(int row, int column, int id)
        {
            Read().CellOf(row, column);
            CheckEditable();

            if (!IsResident || !Item.File.Supports(1021))
            {
                throw new InvalidOperationException(
                    "The table holds no TABLECONTENT to keep the cell style of a cell in: it is not in a drawing, or the drawing is one before R2007 (AC1021)."
                );
            }

            if (GetCellStyleId(row, column) == id)
            {
                return;
            }

            MergeAsLoaded();
            KeepContent();
            bool listed = ContentItem() == null || Listed();

            if (ContentItem() == null)
            {
                NewContent();
            }

            EditContent(true, null, grid =>
            {
                if (row >= grid.Rows.Count || column >= grid.Rows[row].Cells.Count)
                {
                    throw new InvalidOperationException(
                        "The TABLECONTENT of the table " + Handle + " has no such cell."
                    );
                }

                ContentGridCell cell = grid.Rows[row].Cells[column];
                int at = StyleIdAt(cell.Groups);

                if (at < 0)
                {
                    throw new InvalidOperationException(
                        "A cell of the TABLECONTENT of the table "
                            + Handle
                            + " has no style (90) after its TABLECELL_BEGIN."
                    );
                }

                cell.Groups[at] = new KeyValuePair<int, string>(90, id.ToString(CultureInfo.InvariantCulture));
                cell.Dirty = true;
                grid.Changed = true;
            });

            if (!listed)
            {
                Recompose(StyleHandle());
            }
        }

        /// <summary>The place of the cell style (the 90 after TABLECELL_BEGIN) among the groups of a cell of the TABLECONTENT; -1 where it has none.</summary>
        private static int StyleIdAt(List<KeyValuePair<int, string>> groups)
        {
            int begin = groups.FindIndex(g => g.Key == 1 && g.Value.Trim() == "TABLECELL_BEGIN");
            return begin >= 0 && begin + 1 < groups.Count && groups[begin + 1].Key == 90 ? begin + 1 : -1;
        }

        #endregion
        #region A block in a cell

        /// <summary>
        /// Puts a block into a cell, as AutoCAD's <c>SetBlockTableRecordId(row, column, blockId, autoFit)</c>: a text cell made a
        /// block cell (171 of 2), its text, value and field taken out (<see cref="ResetValue"/>), its block (340), its scale (144,
        /// 1), the count of its attributes (179) and, for each attribute definition of the block that is not constant, the
        /// definition (331) and the value the cell gives it (300, the definition's own text), put before the value as AutoCAD 2021
        /// writes them; a block cell given another block takes the new one's attributes in place of the old one's, its scale and
        /// turn kept. The cell is fitted to its block (the flag 174) where <paramref name="autoFit"/> asks. The cell's content in
        /// the TABLECONTENT, where the table has one, is emptied as <see cref="ResetValue"/> empties a block cell. The block AutoCAD
        /// drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="block">The block, one of the table's drawing.</param>
        /// <param name="autoFit">Whether the block is fitted to the cell.</param>
        /// <exception cref="ArgumentNullException">Thrown when the block is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="ArgumentException">Thrown when the block is not one of the table's drawing, or a layout.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased or not in a drawing, or is of a drawing before R2007, whose cell holds its text in groups a block cell has no place for.</exception>
        public void SetBlockTableRecord(int row, int column, DxfBlockTableRecord block, bool autoFit)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            Read().CellOf(row, column);
            CheckEditable();

            if (!IsResident || !ReferenceEquals(block.Item?.File, Item.File) || block.Handle == null)
            {
                throw new ArgumentException(
                    "The block is not one of the table's drawing: a block cell names a block of the drawing it is in.",
                    nameof(block)
                );
            }

            if (block.IsLayout || block.IsAnonymous)
            {
                throw new ArgumentException("A layout or an anonymous block is not put into a cell.", nameof(block));
            }

            CellView cell = CellToEdit(row, column);

            if (cell.ValueFrom < 0)
            {
                throw new InvalidOperationException(
                    "The cell ("
                        + row.ToString(CultureInfo.InvariantCulture)
                        + ", "
                        + column.ToString(CultureInfo.InvariantCulture)
                        + ") holds its text in groups of a drawing before R2007: a block cell is made in a drawing of R2007 or later."
                );
            }

            bool held = cell.Whole(171, 1) == (int)DxfTableCellType.BlockCell;

            // A text cell is emptied first, its field erased; the cell is read again after each writing.
            if (!held)
            {
                string field = cell.Head(344);

                if (!cell.HoldsNoValue())
                {
                    cell.SetNoValue();
                    cell.Groups.Write(Order);
                }

                EraseFields(new[] { field });
                cell = CellToEdit(row, column);
            }

            double scale = held && Numbers.TryRead(cell.Head(144), out double read) ? read : 1.0;
            int end = cell.ValueFrom;

            for (int i = end - 1; i > cell.From; i--)
            {
                int code = cell.Groups.List[i].Key;

                if (code == 340 || code == 144 || code == 179 || code == 331 || code == 300)
                {
                    cell.Groups.RemoveAt(i);
                    end--;
                }
            }

            var put = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(340, block.Handle),
                new KeyValuePair<int, string>(144, AsReal(scale)),
            };
            var attributes = new List<DxfAttributeDefinition>();

            foreach (DxfEntity entity in block)
            {
                if (entity is DxfAttributeDefinition definition && !definition.Constant && !definition.IsErased)
                {
                    attributes.Add(definition);
                }
            }

            put.Add(new KeyValuePair<int, string>(179, attributes.Count.ToString(CultureInfo.InvariantCulture)));

            foreach (DxfAttributeDefinition definition in attributes)
            {
                put.Add(new KeyValuePair<int, string>(331, definition.Handle));
                put.Add(new KeyValuePair<int, string>(300, definition.TextString ?? string.Empty));
            }

            for (int k = 0; k < put.Count; k++)
            {
                cell.Groups.Insert(end + k, put[k]);
            }

            cell.Groups.List[cell.From] = new KeyValuePair<int, string>(171, "2");
            int fit = -1;

            for (int i = cell.From; i < end && fit < 0; i++)
            {
                fit = cell.Groups.List[i].Key == 174 ? i : -1;
            }

            if (fit >= 0)
            {
                cell.Groups.List[fit] = new KeyValuePair<int, string>(174, autoFit ? "1" : "0");
            }

            cell.Groups.Write(Order);
            ContentEmptied(row, column);
        }

        #endregion

        #region A field in a cell

        /// <summary>
        /// Gets the code of the field a cell shows (its 344), as AutoCAD's <c>GetFieldId</c> then <c>GetFieldCode()</c> read it:
        /// <c>\AcVar Login</c>, or <c>\AcExpr (A1+B1)</c> for a formula; null for a cell that holds none, or whose field the
        /// drawing has not.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The code, without its markers; or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public string GetFieldCode(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            string handle = Cell(row, column).Head(344)?.Trim();

            if (string.IsNullOrEmpty(handle) || !IsResident)
            {
                return null;
            }

            DxfItem field = Item.File.ObjectItem(handle);
            return field != null && string.Equals(field.Type, "FIELD", StringComparison.OrdinalIgnoreCase)
                ? new DxfField(field).GetFieldCode()
                : null;
        }

        /// <summary>
        /// Gives a text cell a field, as AutoCAD's <c>SetFieldId(row, column, fieldId, flag)</c> does: a FIELD of the code, owned by
        /// the table, the cell's 344 pointed at it, as <see cref="SetFormula"/> writes a formula, the field the cell had before
        /// erased where nothing else holds it. The cell's value is left as it is, for <see cref="SetFieldValue"/> to give it the
        /// words the field comes to.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="fieldCode">The field, in its markers: <c>%&lt;\AcVar Login&gt;%</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the code is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        /// <exception cref="ArgumentException">Thrown when the code is not one field in its markers.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell, the file is older than R2004 (AC1018), or the table is erased.</exception>
        public void SetFieldCode(int row, int column, int contentIndex, string fieldCode)
        {
            CheckContent(contentIndex);

            if (fieldCode == null)
            {
                throw new ArgumentNullException(nameof(fieldCode));
            }

            string trimmed = fieldCode.Trim();

            if (
                !trimmed.StartsWith("%<", StringComparison.Ordinal) || !trimmed.EndsWith(">%", StringComparison.Ordinal)
            )
            {
                throw new ArgumentException(
                    "A field is written in its markers, as %<\\AcVar Login>%: " + fieldCode + " is not.",
                    nameof(fieldCode)
                );
            }

            CellView cell = CellToEdit(row, column);
            CheckText(cell, row, column);

            if (!Item.File.Supports(1018))
            {
                throw new InvalidOperationException(
                    "A DXF older than R2004 (AC1018) has no FIELD to hold a field: fields came with it."
                );
            }

            // Written as a formula is, from the code between its markers.
            WriteFormula(row, column, trimmed.Substring(2, trimmed.Length - 4));
        }

        /// <summary>
        /// Sets what a cell with a field (<see cref="SetFieldCode"/>, <see cref="SetFormula"/>) shows, as AutoCAD 2021 keeps the
        /// words a field came to in its cell: the value written as <see cref="SetValue(int, int, object)"/> writes one, a number a
        /// <see cref="DxfDataType.Double"/> or <see cref="DxfDataType.Long"/>, any other words a text, the cell's 344 left as it
        /// is, so that the field is still there to be evaluated again. Nothing is written where the cell shows that already.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="value">The value: a <see cref="string"/>, an <see cref="int"/> or a <see cref="double"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or a number is not finite.</exception>
        /// <exception cref="ArgumentException">Thrown when the value is of another type, or a text with a line break.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell or holds no field, or the table is erased.</exception>
        public void SetFieldValue(int row, int column, object value)
        {
            Written written = ValueToWrite(value, nameof(value));
            CellView cell = CellToEdit(row, column);
            CheckText(cell, row, column);

            if (cell.Head(344) == null)
            {
                throw new InvalidOperationException(
                    "The cell ("
                        + row.ToString(CultureInfo.InvariantCulture)
                        + ", "
                        + column.ToString(CultureInfo.InvariantCulture)
                        + ") holds no field: a field is given by SetFieldCode or SetFormula."
                );
            }

            string shown = written.Value is double real && cell.ValueFrom >= 0 ? RealText(real, null) : written.Text;

            if (cell.ValueFrom < 0)
            {
                if (cell.Holds(shown))
                {
                    return;
                }

                CheckEditable();
                cell.SetText(shown);
                cell.Groups.Write(Order);
                return;
            }

            object now = cell.Value();

            if (
                KindOf(cell) == written.Kind
                && (
                    SameValue(now, written.Value)
                    || (now is string text && SameValue(DxfReader.Decode(text), written.Value))
                )
            )
            {
                return;
            }

            CheckEditable();
            string field = cell.Head(344);
            Typed(cell);
            cell.WriteValue(
                ((int)written.Kind).ToString(CultureInfo.InvariantCulture),
                written.Data,
                shown,
                null,
                string.Empty,
                true
            );
            cell.Groups.Write(Order);
            _ = field;
        }

        /// <summary>
        /// Gets the cells that show a field or a formula (their 344 names a record): the ones to evaluate again where the table
        /// is edited. Each is a range of the one cell.
        /// </summary>
        /// <returns>The cells, row by row.</returns>
        public IReadOnlyList<DxfCellRange> GetFieldCells()
        {
            Layout layout = Read();
            var found = new List<DxfCellRange>();

            // One pass over the groups from the first cell on for a pointer to a field at all, so that a table of none (nearly
            // every table) costs no view of its cells.
            List<KeyValuePair<int, string>> groups = layout.Groups.List;
            bool pointed = false;

            for (
                int i = layout.CellStart.Count > 0 ? layout.CellStart[0] : groups.Count;
                i < groups.Count && !pointed;
                i++
            )
            {
                pointed = groups[i].Key == 344;
            }

            if (!pointed)
            {
                return found;
            }

            for (int r = 0; r < layout.Rows; r++)
            {
                for (int c = 0; c < layout.Columns; c++)
                {
                    if (layout.CellAt(r, c).Head(344) != null)
                    {
                        found.Add(new DxfCellRange(r, c, r, c));
                    }
                }
            }

            return found;
        }

        #endregion
    }
}
