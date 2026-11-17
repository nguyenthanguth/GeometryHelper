using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// MATCHCELL and EDITTABLECELL of the Table Cell tab, as AutoCAD 2021's. MATCHCELL asks for a source cell, then for
    /// destination cells one after another until Enter, giving each the source's look (<see cref="CadTableEditor.MatchCell"/>).
    /// EDITTABLECELL, of which the viewer offers the formula part (<c>_EditTableCell _ExecuteOneAction _Operation _InsertFormula</c>
    /// is what the Insert Formula menu runs), asks for a cell, then for a kind of formula: Sum, Average and Count ask for the
    /// corners of a range of cells and write <c>=Sum(A1:B3)</c>; Cell asks for a cell and writes <c>=B2</c>; Equation asks for the
    /// words of the formula. Each cell given a formula or matched is one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddTableCellsTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("MATCHCELL", MatchCell));
            registry.Add(new CadCommand("EDITTABLECELL", EditTableCell));
        }

        /// <summary>MATCHCELL: "Select source cell:" then "Select destination cell:" until Enter.</summary>
        private static IEnumerable<CadPrompt> MatchCell(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Select source cell");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            if (
                !CadTableEditor.TryCellAt(
                    c.Space,
                    c.Input.Point,
                    out string source,
                    out int sourceRow,
                    out int sourceColumn
                )
            )
            {
                c.Write("No table cell was picked.");
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForPoint("Select destination cell");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                if (!CadTableEditor.TryCellAt(c.Space, c.Input.Point, out string handle, out int row, out int column))
                {
                    c.Write("No table cell was picked.");
                    continue;
                }

                c.OpenUndoStep();
                CadEditResult result = CadTableEditor.MatchCell(
                    c.Document,
                    source,
                    sourceRow,
                    sourceColumn,
                    handle,
                    new DxfCellRange(row, column, row, column)
                );
                c.CloseUndoStep();

                if (!result.Succeeded)
                {
                    c.Write(result.Message);
                }
            }
        }

        /// <summary>EDITTABLECELL, the formula part: a cell, then Sum, Average, Count, Cell or Equation.</summary>
        private static IEnumerable<CadPrompt> EditTableCell(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Select a table cell");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            if (!CadTableEditor.TryCellAt(c.Space, c.Input.Point, out string handle, out int row, out int column))
            {
                c.Write("No table cell was picked.");
                yield break;
            }

            yield return CadPrompt.ForKeyword(
                "Enter a formula [Sum/Average/Count/Cell/Equation]",
                new[] { "Sum", "Average", "Count", "Cell", "Equation" }
            );

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string function =
                c.Input.Is("Sum") ? "Sum"
                : c.Input.Is("Average") ? "Average"
                : "Count";
            string formula = null;

            if (c.Input.Is("Equation"))
            {
                yield return CadPrompt.ForString("Enter the formula, as =A1+B2");
                formula = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : null;
            }
            else if (c.Input.Is("Cell"))
            {
                yield return CadPrompt.ForPoint("Select table cell");

                if (!TryCorner(c, handle, out int pickedRow, out int pickedColumn))
                {
                    yield break;
                }

                formula = "=" + CadTableEditor.CellName(pickedRow, pickedColumn);
            }
            else
            {
                yield return CadPrompt.ForPoint("Select first corner of table cell range");

                if (!TryCorner(c, handle, out int firstRow, out int firstColumn))
                {
                    yield break;
                }

                yield return CadPrompt.ForPoint("Select second corner of table cell range");

                if (!TryCorner(c, handle, out int secondRow, out int secondColumn))
                {
                    yield break;
                }

                formula =
                    "="
                    + function
                    + "("
                    + CadTableEditor.CellName(
                        System.Math.Min(firstRow, secondRow),
                        System.Math.Min(firstColumn, secondColumn)
                    )
                    + ":"
                    + CadTableEditor.CellName(
                        System.Math.Max(firstRow, secondRow),
                        System.Math.Max(firstColumn, secondColumn)
                    )
                    + ")";
            }

            if (string.IsNullOrEmpty(formula))
            {
                yield break;
            }

            if (!formula.StartsWith("=", System.StringComparison.Ordinal))
            {
                formula = "=" + formula;
            }

            c.OpenUndoStep();
            CadEditResult result = CadTableEditor.InsertFormula(c.Document, handle, row, column, formula);
            c.CloseUndoStep();

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>The cell a point just answered falls in, of the table the formula goes in; said where it is of no cell or of another table.</summary>
        private static bool TryCorner(CadCommandContext c, string handle, out int row, out int column)
        {
            row = column = 0;

            if (!c.Input.IsPoint)
            {
                return false;
            }

            if (
                !CadTableEditor.TryCellAt(c.Space, c.Input.Point, out string other, out row, out column)
                || !string.Equals(other, handle, System.StringComparison.OrdinalIgnoreCase)
            )
            {
                c.Write("Pick a cell of the table the formula goes in.");
                return false;
            }

            return true;
        }
    }
}
