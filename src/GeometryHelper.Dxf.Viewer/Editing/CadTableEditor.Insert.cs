using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Insert group of the Table Cell tab (a block, a field, a formula) and Data Format, and the working out of the
    /// formulas and fields of a table's cells. The formulas are those of <see cref="CadTableFormula"/>, written as AutoCAD's
    /// <c>SetFormula</c> writes them (a field of the evaluator AcExpr) and worked out again after every edit of the table.
    /// </summary>
    public static partial class CadTableEditor
    {
        /// <summary>
        /// Puts a block into a cell, as the Insert a Block in a Table Cell dialog does: the cell a block cell of the block, fitted
        /// to the cell (AutoFit) or at the scale given, turned, and its content aligned as given, one undo step. A block cell
        /// given another block keeps its scale and turn unless the dialog asks again.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="blockName">The name of a block of the drawing.</param>
        /// <param name="scale">The scale, above nought; not used where the block is fitted.</param>
        /// <param name="autoFit">Whether the block is fitted to the cell.</param>
        /// <param name="rotation">The turn, in degrees.</param>
        /// <param name="alignment">Where the block stands in the cell; null to keep what the cell has.</param>
        /// <returns>What came of it; refused where the block is none of the drawing's, or a drawing before R2007 holds no block cell, or the cell is locked.</returns>
        public static CadEditResult InsertBlock(
            CadDocument document,
            string handle,
            int row,
            int column,
            string blockName,
            double scale,
            bool autoFit,
            double rotation,
            DxfCellAlignment? alignment
        )
        {
            if (blockName == null)
            {
                throw new ArgumentNullException(nameof(blockName));
            }

            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            if (!InTable(table, row, column))
            {
                return CadEditResult.Refuse("The table has no cell " + CellName(row, column) + ".");
            }

            DxfDatabase database = document.Database;
            DxfBlockTableRecord block = database.BlockTable.Has(blockName) ? database.BlockTable[blockName] : null;

            if (block == null || block.IsLayout || block.IsAnonymous)
            {
                return CadEditResult.Refuse("The drawing has no block " + blockName + " to put in a cell.");
            }

            if (!database.File.Supports(1021))
            {
                return CadEditResult.Refuse("A drawing before R2007 holds no block cell: save it as R2007 or later.");
            }

            if (!autoFit && !(scale > 0.0 && !double.IsInfinity(scale)))
            {
                return CadEditResult.Refuse("A block is put in a cell at a scale above nought.");
            }

            var one = new DxfCellRange(row, column, row, column);
            refused = LockedIn(table, one, true, false);

            if (refused != null)
            {
                return refused;
            }

            return Edit(document, handle, "Insert block", CellName(row, column), blockName, t =>
            {
                t.SetBlockTableRecord(row, column, block, autoFit);

                if (!autoFit)
                {
                    t.SetBlockScale(row, column, scale);
                }

                t.SetBlockRotation(row, column, rotation * Math.PI / 180.0);

                if (alignment.HasValue)
                {
                    t.SetAlignment(row, column, alignment.Value);
                }
            });
        }

        /// <summary>
        /// Puts a field into a cell, as Insert Field does with the Field dialog's code: the cell shows the words the field comes
        /// to, and keeps the field to evaluate again, one undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="fieldCode">The field in its markers, as the Field dialog gives it: <c>%&lt;\AcVar Login&gt;%</c>.</param>
        /// <returns>What came of it; refused where the code is no field, the cell is a block cell or locked, or the drawing is one before R2004.</returns>
        public static CadEditResult InsertField(
            CadDocument document,
            string handle,
            int row,
            int column,
            string fieldCode
        )
        {
            if (fieldCode == null)
            {
                throw new ArgumentNullException(nameof(fieldCode));
            }

            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            if (!InTable(table, row, column))
            {
                return CadEditResult.Refuse("The table has no cell " + CellName(row, column) + ".");
            }

            refused = FieldRefusal(document, table, row, column);

            if (refused != null)
            {
                return refused;
            }

            DxfFieldDraft draft = CadFields.Parse(fieldCode);

            if (draft == null || draft.Children.Count != 1)
            {
                return CadEditResult.Refuse("The words " + fieldCode + " are not one field.");
            }

            return Edit(document, handle, "Insert field", CellName(row, column), fieldCode, t =>
                t.SetFieldCode(row, column, 0, fieldCode.Trim())
            );
        }

        /// <summary>
        /// Puts a formula into a cell, as Insert Formula does: <c>=Sum(A1:B3)</c>, <c>=Average(C2:C9)</c>, <c>=Count(A1:A5)</c>,
        /// a cell <c>=B2</c> or an equation <c>=A1*B1+2</c>; the cell shows what the formula comes to, and every formula of the
        /// table is worked out again after each edit, one undo step. A formula that cannot be worked out (a cell of words in
        /// it, a division by nought) shows <c>#####</c>.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="formula">The formula, from its <c>=</c>.</param>
        /// <returns>What came of it; refused where the formula is not one, the cell is a block cell or locked, or the drawing is one before R2004.</returns>
        public static CadEditResult InsertFormula(
            CadDocument document,
            string handle,
            int row,
            int column,
            string formula
        )
        {
            if (formula == null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            if (!InTable(table, row, column))
            {
                return CadEditResult.Refuse("The table has no cell " + CellName(row, column) + ".");
            }

            refused = FieldRefusal(document, table, row, column);

            if (refused != null)
            {
                return refused;
            }

            string text = formula.Trim();

            if (
                !text.StartsWith("=", StringComparison.Ordinal)
                || text.Length < 2
                || text.IndexOfAny(new[] { '\r', '\n' }) >= 0
            )
            {
                return CadEditResult.Refuse("A formula is written from its =, as =Sum(A1:B3), on one line.");
            }

            return Edit(document, handle, "Insert formula", CellName(row, column), text, t =>
                t.SetFormula(row, column, 0, text)
            );
        }

        private static CadEditResult FieldRefusal(CadDocument document, DxfTable table, int row, int column)
        {
            if (!document.Database.File.Supports(1018))
            {
                return CadEditResult.Refuse("A drawing before R2004 holds no field: save it as R2004 or later.");
            }

            if (table.CellType(row, column) == DxfTableCellType.BlockCell)
            {
                return CadEditResult.Refuse("The cell " + CellName(row, column) + " holds a block: empty it first.");
            }

            return LockedIn(table, new DxfCellRange(row, column, row, column), true, false);
        }

        /// <summary>
        /// Gives the cells of a range a kind of data and a format, as the Data Format dialog does: the kind (a number, a whole
        /// number, words) and its unit (a length, an angle, money, a percentage), then the format, as <c>%lu2%pr2%ps[$,]%th44</c>
        /// for money of two places with a dollar sign and commas; the values kept, shown anew. One undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="kind">The kind: <see cref="DxfDataType.Double"/>, <see cref="DxfDataType.Long"/>, <see cref="DxfDataType.String"/>; a date or a point keeps its kind and takes the format alone.</param>
        /// <param name="unit">The unit of the kind.</param>
        /// <param name="format">The format; empty for none.</param>
        /// <returns>What came of it; refused where a cell is locked in its format or holds a block.</returns>
        public static CadEditResult SetDataFormat(
            CadDocument document,
            string handle,
            DxfCellRange range,
            DxfDataType kind,
            DxfUnitType unit,
            string format
        )
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            refused = LockedIn(table, range, false, true);

            if (refused != null)
            {
                return refused;
            }

            return Edit(document, handle, "Data format", Name(range), format, t => Each(range, (r, c) =>
                {
                    if (t.CellType(r, c) == DxfTableCellType.BlockCell)
                    {
                        return;
                    }

                    DxfDataType now = t.DataType(r, c);
                    bool settable =
                        kind == DxfDataType.Double || kind == DxfDataType.Long || kind == DxfDataType.String;

                    if (settable && (now != kind || t.UnitType(r, c) != unit))
                    {
                        t.SetDataType(r, c, kind, unit);
                    }

                    t.SetFormat(r, c, format);
                }));
        }

        /// <summary>
        /// Works out again the formulas and fields of the cells of a table, in the order they depend on one another: each formula
        /// from the cells it reads, a field from the drawing, the words written into the cell and the formula or field kept.
        /// A formula that cannot be worked out shows <c>#####</c>. Nothing is written where a cell shows its result already.
        /// </summary>
        /// <param name="document">The document the fields are worked out against; null to work out formulas alone.</param>
        /// <param name="table">The table.</param>
        /// <param name="reason">One of <see cref="CadFieldContexts"/>: why the fields are worked out.</param>
        /// <returns>How many cells were changed.</returns>
        internal static int UpdateFields(CadDocument document, DxfTable table, int reason = CadFieldContexts.Demand)
        {
            IReadOnlyList<DxfCellRange> cells = table.GetFieldCells();

            if (cells.Count == 0)
            {
                return 0;
            }

            var changed = new HashSet<int>();
            int columns = table.NumColumns;

            // Each pass works every cell out from the values the last left; the passes stop when none changes a cell, or after
            // as many as there are cells, which a cell reading itself through the others never settles in.
            for (int pass = 0; pass <= cells.Count; pass++)
            {
                bool again = false;

                foreach (DxfCellRange cell in cells)
                {
                    object result = WorkOut(document, table, cell.TopRow, cell.LeftColumn, reason);

                    if (result == null)
                    {
                        continue;
                    }

                    object now = table.GetValue(cell.TopRow, cell.LeftColumn, 0);

                    if (!SameResult(now, result))
                    {
                        table.SetFieldValue(cell.TopRow, cell.LeftColumn, result);
                        changed.Add(cell.TopRow * columns + cell.LeftColumn);
                        again = true;
                    }
                }

                if (!again)
                {
                    break;
                }
            }

            return changed.Count;
        }

        private static bool SameResult(object now, object result)
        {
            if (result is string text)
            {
                return now is string shown && string.Equals(shown, text, StringComparison.Ordinal);
            }

            double want = Convert.ToDouble(result, CultureInfo.InvariantCulture);
            return (now is double || now is int)
                && Math.Abs(Convert.ToDouble(now, CultureInfo.InvariantCulture) - want) < 1E-9;
        }

        /// <summary>What a cell's formula or field comes to: a number, the words of a field, the error text; null where it holds none to work out.</summary>
        private static object WorkOut(CadDocument document, DxfTable table, int row, int column, int reason)
        {
            string formula = table.GetFormula(row, column, 0);

            if (formula != null)
            {
                bool ok = CadTableFormula.TryEvaluate(
                    formula.Substring(1),
                    (r, c) => NumberOf(table, r, c),
                    (r, c) => r >= 0 && c >= 0 && r < table.NumRows && c < table.NumColumns && !table.IsEmpty(r, c),
                    out double value,
                    out bool whole
                );

                if (!ok)
                {
                    return CadTableFormula.Error;
                }

                return whole && Math.Abs(value) < int.MaxValue ? (object)(int)Math.Round(value) : value;
            }

            string code = table.GetFieldCode(row, column, 0);

            if (code == null || document == null)
            {
                return null;
            }

            DxfFieldDraft draft = CadFields.Parse("%<" + code + ">%");

            if (draft == null)
            {
                return null;
            }

            try
            {
                return CadFields.Evaluate(draft, new CadFieldContext(document, reason), false);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>The number a cell holds: its value as a number, or its words where they read as one; null for a cell of words or of none, or a cell outside the table.</summary>
        private static double? NumberOf(DxfTable table, int row, int column)
        {
            if (row < 0 || column < 0 || row >= table.NumRows || column >= table.NumColumns)
            {
                return null;
            }

            object value = table.GetValue(row, column, 0);

            if (value is int whole)
            {
                return whole;
            }

            if (value is double real)
            {
                return real;
            }

            return
                value is string text
                && double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : (double?)null;
        }
    }
}
