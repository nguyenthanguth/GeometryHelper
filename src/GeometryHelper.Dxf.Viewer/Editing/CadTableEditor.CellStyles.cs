using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The cell styles of the Table Cell tab: Title, Header and Data and those a user makes (Create New Cell Style, Manage Cell
    /// Styles), and the cell style a cell takes. A user's cell style lives in the table style's CELLSTYLEMAP; a cell takes one as
    /// AutoCAD does, the style's look written into the cell as its own overrides and its id into the cell's TABLECONTENT.
    /// </summary>
    public static partial class CadTableEditor
    {
        private const string CellStyleStep = "TABLESTYLE";

        /// <summary>Gets the table style a table is written in; null where the table names none.</summary>
        /// <param name="table">The table.</param>
        /// <returns>The style.</returns>
        public static DxfTableStyle StyleOf(DxfTable table) => table?.TableStyle as DxfTableStyle;

        /// <summary>
        /// Gets the name of the cell style a cell takes: that its TABLECONTENT names (Title, Header, Data or a user's), else that of
        /// its row (the title, the header or the data), as the Cell Styles box of the tab shows it.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The name.</returns>
        public static string CellStyleOf(DxfTable table, int row, int column)
        {
            int id = table.GetCellStyleId(row, column);
            string named = id == 0 ? null : StyleOf(table)?.GetCellStyleName(id);

            if (named != null)
            {
                return named;
            }

            DxfRowType kind = table.RowType(row);
            return kind == DxfRowType.TitleRow ? "Title"
                : kind == DxfRowType.HeaderRow ? "Header"
                : "Data";
        }

        /// <summary>
        /// Gives the cells of a range a cell style, as choosing one in the Cell Styles box does: its look (alignment, text height,
        /// text style and colour, fill and the four borders) written as the cells' own overrides over what they held, their
        /// contents kept, and its id written into their TABLECONTENT; <c>null</c> is By Row/Column: the cells name no style and keep
        /// what they hold. One undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="styleName">The name of a cell style of the table's style; null for By Row/Column.</param>
        /// <returns>What came of it; refused where the table style has no such cell style or a cell is locked in its format.</returns>
        public static CadEditResult SetCellStyle(
            CadDocument document,
            string handle,
            DxfCellRange range,
            string styleName
        )
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            DxfTableStyle style = StyleOf(table);

            if (styleName != null && style == null)
            {
                return CadEditResult.Refuse(
                    "The table is written in no table style, which holds the cell style " + styleName + "."
                );
            }

            DxfCellStyleLook look = styleName == null ? null : style.GetCellStyleLook(styleName);

            if (styleName != null && look == null)
            {
                return CadEditResult.Refuse("The table style has no cell style " + styleName + ".");
            }

            refused = LockedIn(table, range, false, true);

            if (refused != null)
            {
                return refused;
            }

            int id = styleName == null ? 0 : style.GetCellStyleId(styleName);
            bool holds = document.Database.File.Supports(1021);
            return Edit(document, handle, "Cell style", Name(range), styleName ?? "By Row/Column", t =>
                Each(range, (r, c) =>
                {
                    if (look != null)
                    {
                        t.RemoveAllOverrides(r, c);
                        ApplyLook(t, r, c, look);
                    }

                    if (holds)
                    {
                        t.SetCellStyleId(r, c, id);
                    }
                })
            );
        }

        private static void ApplyLook(DxfTable table, int row, int column, DxfCellStyleLook look)
        {
            table.SetAlignment(row, column, look.Alignment);

            if (look.TextHeight > 0.0)
            {
                table.SetTextHeight(row, column, look.TextHeight);
            }

            DxfDatabase database = table.Item.File.Database;

            if (look.TextStyleName != null && database.TextStyleTable.Has(look.TextStyleName))
            {
                table.SetTextStyle(row, column, database.TextStyleTable[look.TextStyleName]);
            }

            if (look.ContentColor != null)
            {
                table.SetContentColor(row, column, look.ContentColor);
            }

            if (look.BackgroundColor == null)
            {
                table.SetBackgroundColorNone(row, column, true);
            }
            else
            {
                table.SetBackgroundColor(row, column, look.BackgroundColor);
                table.SetBackgroundColorNone(row, column, false);
            }

            // The four edges of a cell are the style's top, bottom, left and right lines.
            int[] lines = { 0, 5, 2, 3 };

            for (int k = 0; k < 4; k++)
            {
                int line = lines[k];

                if (look.LineColors[line] != null)
                {
                    table.SetGridColor(row, column, EdgeLines[k], look.LineColors[line]);
                }

                if (look.LineWeights[line].HasValue)
                {
                    table.SetGridLineWeight(row, column, EdgeLines[k], look.LineWeights[line].Value);
                }

                if (look.LineShown[line].HasValue)
                {
                    table.SetGridVisibility(row, column, EdgeLines[k], look.LineShown[line].Value);
                }
            }
        }

        /// <summary>
        /// Makes a cell style of a table style, as Create New Cell Style does: of the name given, starting from the look of one
        /// of the style's. One undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="tableStyleName">The name of the table style.</param>
        /// <param name="name">The name of the new cell style.</param>
        /// <param name="startWith">The cell style it starts from: Title, Header, Data or a user's.</param>
        /// <returns>What came of it; refused for a name that is empty or taken, or a table style or start that is none.</returns>
        public static CadEditResult CreateCellStyle(
            CadDocument document,
            string tableStyleName,
            string name,
            string startWith
        )
        {
            string trimmed = (name ?? string.Empty).Trim();
            DxfTableStyle style = StyleNamed(document, tableStyleName, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            if (trimmed.Length == 0)
            {
                return CadEditResult.Refuse("A cell style needs a name.");
            }

            if (style.HasCellStyle(trimmed))
            {
                return CadEditResult.Refuse("The cell style \"" + trimmed + "\" already exists.");
            }

            if (!style.HasCellStyle(startWith ?? string.Empty))
            {
                return CadEditResult.Refuse("The table style has no cell style " + startWith + " to start from.");
            }

            return document.ApplyEdit(
                CellStyleStep,
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New cell style", null, trimmed) },
                s => StyleNamed(s.Document, tableStyleName, out _).AddCellStyle(trimmed, startWith)
            );
        }

        /// <summary>Takes a cell style a user made out of a table style, as Delete does in Manage Cell Styles; Title, Header and Data stay. One undo step.</summary>
        /// <param name="document">The document.</param>
        /// <param name="tableStyleName">The name of the table style.</param>
        /// <param name="name">The name of the cell style.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult DeleteCellStyle(CadDocument document, string tableStyleName, string name)
        {
            DxfTableStyle style = StyleNamed(document, tableStyleName, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            if (style.GetCellStyleId(name ?? string.Empty) < 101)
            {
                return CadEditResult.Refuse(
                    "Title, Header and Data are cell styles of every table style, and are not deleted."
                );
            }

            return document.ApplyEdit(
                CellStyleStep,
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete cell style", name, null) },
                s => StyleNamed(s.Document, tableStyleName, out _).RemoveCellStyle(name)
            );
        }

        /// <summary>Renames a cell style a user made, as Rename does in Manage Cell Styles. One undo step.</summary>
        /// <param name="document">The document.</param>
        /// <param name="tableStyleName">The name of the table style.</param>
        /// <param name="name">The name of the cell style.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult RenameCellStyle(
            CadDocument document,
            string tableStyleName,
            string name,
            string newName
        )
        {
            DxfTableStyle style = StyleNamed(document, tableStyleName, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            string trimmed = (newName ?? string.Empty).Trim();

            if (style.GetCellStyleId(name ?? string.Empty) < 101)
            {
                return CadEditResult.Refuse(
                    "Title, Header and Data are cell styles of every table style, and are not renamed."
                );
            }

            if (
                trimmed.Length == 0
                || (style.HasCellStyle(trimmed) && !string.Equals(trimmed, name, StringComparison.OrdinalIgnoreCase))
            )
            {
                return CadEditResult.Refuse("The cell style name \"" + newName + "\" is empty or taken.");
            }

            return document.ApplyEdit(
                CellStyleStep,
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Rename cell style", name, trimmed) },
                s => StyleNamed(s.Document, tableStyleName, out _).RenameCellStyle(name, trimmed)
            );
        }

        private static DxfTableStyle StyleNamed(CadDocument document, string name, out CadEditResult refused)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            DxfTableStyle style = CadTableStyleEditor.Find(document.Database, name ?? string.Empty);
            refused = style == null ? CadEditResult.Refuse("The drawing has no table style " + name + ".") : null;
            return style;
        }

        /// <summary>The names of the cell styles the table style of a table holds, Title, Header and Data first; the three where the table names no style.</summary>
        /// <param name="table">The table.</param>
        /// <returns>The names.</returns>
        public static IReadOnlyList<string> CellStyleNames(DxfTable table) =>
            StyleOf(table)?.GetCellStyleNames() ?? new[] { "Title", "Header", "Data" };
    }
}
