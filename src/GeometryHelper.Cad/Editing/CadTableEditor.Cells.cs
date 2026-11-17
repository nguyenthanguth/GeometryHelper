using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// Cells of a table held on the clipboard of the Table Cell tab: what Copy and Cut take, and Paste and Match Cell give, each
    /// cell with its content (words, number, block, field or formula) and its look (alignment, text, colours, borders), and the
    /// blocks of merged cells within them. It is a snapshot: the cells copied can be edited, or the table erased, before it is
    /// pasted.
    /// </summary>
    public sealed class CadTableCellClip
    {
        internal CadTableCellClip(int rows, int columns, bool isCut)
        {
            Rows = rows;
            Columns = columns;
            IsCut = isCut;
            Cells = new CadTableClipCell[rows, columns];
        }

        /// <summary>Gets how many rows the cells copied have.</summary>
        public int Rows { get; }

        /// <summary>Gets how many columns the cells copied have.</summary>
        public int Columns { get; }

        /// <summary>Gets whether the cells were cut: their contents taken out of the table when they were taken.</summary>
        public bool IsCut { get; }

        /// <summary>Gets the name of the cells copied, as B2:C3, in the table they came from.</summary>
        public string Name { get; internal set; }

        internal CadTableClipCell[,] Cells { get; }

        /// <summary>The row and column, from nought, of the corner the cells were copied from, which the references of a formula pasted move from.</summary>
        internal int SourceTop { get; set; }

        internal int SourceLeft { get; set; }

        /// <summary>The ranges of merged cells within the cells copied, each its top row, left column, bottom row and right column from the clip's own corner.</summary>
        internal List<int[]> Merges { get; } = new List<int[]>();
    }

    /// <summary>One cell of a <see cref="CadTableCellClip"/>.</summary>
    internal sealed class CadTableClipCell
    {
        internal bool IsBlock;
        internal string BlockName;
        internal double BlockScale = 1.0;
        internal bool AutoFit;
        internal Dictionary<string, string> Attributes = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );
        internal bool IsEmpty;
        internal string Text = string.Empty;
        internal object Value;
        internal DxfDataType Kind;
        internal DxfUnitType Unit;
        internal string Format = string.Empty;
        internal string Formula;
        internal string FieldCode;
        internal double Rotation;
        internal DxfCellAlignment? Alignment;
        internal double? TextHeight;
        internal string TextStyle;
        internal DxfColor ContentColor;
        internal DxfColor Background;
        internal bool? NoBackground;
        internal DxfColor[] EdgeColors = new DxfColor[4];
        internal DxfLineWeight?[] EdgeWeights = new DxfLineWeight?[4];
        internal bool?[] EdgeShown = new bool?[4];
    }

    /// <summary>
    /// Cut, Copy, Paste, Match Cell, Locking, Data Format, and Insert Block, Field and Formula of the cells of a table, as the
    /// Table Cell tab and the right-click menu of AutoCAD 2021 make them; each edit one undo step, "TABLEDIT". A cell locked
    /// (<see cref="DxfCellState"/>) in its content refuses to be typed into, emptied, cut, pasted over or given a block, field
    /// or formula; one locked in its format refuses its alignment, fill, borders, data format and paste.
    /// </summary>
    public static partial class CadTableEditor
    {
        private static readonly DxfGridLineType[] EdgeLines =
        {
            DxfGridLineType.HorizontalTop,
            DxfGridLineType.VerticalRight,
            DxfGridLineType.HorizontalBottom,
            DxfGridLineType.VerticalLeft,
        };

        private static readonly DxfCellEdgeMasks[] EdgeMasks =
        {
            DxfCellEdgeMasks.TopMask,
            DxfCellEdgeMasks.RightMask,
            DxfCellEdgeMasks.BottomMask,
            DxfCellEdgeMasks.LeftMask,
        };

        #region Locking

        /// <summary>Refuses a range holding a cell locked in what an edit changes: its content, its format, or either; null where none is.</summary>
        /// <param name="table">The table.</param>
        /// <param name="range">The cells.</param>
        /// <param name="content">Whether the edit changes the content.</param>
        /// <param name="format">Whether the edit changes the format.</param>
        /// <returns>The refusal, naming the first cell; null where the edit is allowed.</returns>
        internal static CadEditResult LockedIn(DxfTable table, DxfCellRange range, bool content, bool format)
        {
            DxfCellState wanted =
                (content ? DxfCellState.ContentLocked | DxfCellState.ContentReadOnly : DxfCellState.None)
                | (format ? DxfCellState.FormatLocked | DxfCellState.FormatReadOnly : DxfCellState.None);

            if (!table.TryFindCellState(range, wanted, out int row, out int column))
            {
                return null;
            }

            DxfCellState state = table.GetCellState(row, column);
            bool what = (state & (DxfCellState.ContentLocked | DxfCellState.ContentReadOnly)) != 0 && content;
            return CadEditResult.Refuse(
                "The cell "
                    + CellName(row, column)
                    + " is locked: its "
                    + (what ? "content" : "format")
                    + " is not changed."
            );
        }

        /// <summary>
        /// Locks or unlocks the cells of a range, as the Cell Locking menu does: Unlocked (<see cref="DxfCellState.None"/>),
        /// Content Locked, Format Locked, or Content and Format Locked, one undo step. The flag of a linked cell is kept.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="state">The state: the flags <see cref="DxfCellState.ContentLocked"/> and <see cref="DxfCellState.FormatLocked"/>.</param>
        /// <returns>What came of it; refused where the drawing is one before R2007, which holds no state.</returns>
        public static CadEditResult SetLocking(
            CadDocument document,
            string handle,
            DxfCellRange range,
            DxfCellState state
        )
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            if (!document.Database.File.Supports(1021))
            {
                return CadEditResult.Refuse(
                    "A drawing before R2007 holds no state to lock a cell by: save it as R2007 or later."
                );
            }

            state &= DxfCellState.ContentLocked | DxfCellState.FormatLocked;
            return Edit(document, handle, "Cell locking", Name(range), state.ToString(), t =>
                Each(range, (r, c) => t.SetCellState(r, c, state))
            );
        }

        #endregion

        #region Borders

        /// <summary>
        /// Gives lines of a range of cells properties, as OK of the Cell Border Properties dialog does: the lines (All, Outside,
        /// Inside, Bottom, Left, Top or Right, or all of them hidden by No Border) drawn or not, and, where they are drawn, their
        /// lineweight and colour; one undo step. A range of one row or column has no lines inside it of that way.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="lines">The lines of the range, one or more of the six.</param>
        /// <param name="visible">Whether they are drawn.</param>
        /// <param name="weight">The lineweight they are given where they are drawn.</param>
        /// <param name="color">The colour they are given where they are drawn: of the index, by layer or by block.</param>
        /// <returns>What came of it; refused where a cell is locked in its format, or the colour is a true colour.</returns>
        public static CadEditResult SetCellBorders(
            CadDocument document,
            string handle,
            DxfCellRange range,
            DxfGridLineType lines,
            bool visible,
            DxfLineWeight weight,
            DxfColor color
        )
        {
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

            if (
                visible
                && (
                    color == null || (color.ColorMethod != DxfColorMethod.ByAci && !color.IsByBlock && !color.IsByLayer)
                )
            )
            {
                return CadEditResult.Refuse("A border is drawn in a colour of AutoCAD's index.");
            }

            DxfGridLineType inRange = lines;

            if (range.TopRow == range.BottomRow)
            {
                inRange &= ~DxfGridLineType.HorizontalInside;
            }

            if (range.LeftColumn == range.RightColumn)
            {
                inRange &= ~DxfGridLineType.VerticalInside;
            }

            if (inRange == DxfGridLineType.InvalidGridLine)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            var property = new DxfGridPropertyParameter
            {
                PropertyMask = DxfGridProperties.Visibility,
                Visible = visible,
            };

            if (visible)
            {
                property.PropertyMask |= DxfGridProperties.LineWeight | DxfGridProperties.Color;
                property.LineWeight = weight;
                property.Color = color;
            }

            return Edit(document, handle, "Cell borders", Name(range), (visible ? "Show " : "Hide ") + inRange, t =>
                t.SetGridProperty(
                    new DxfCellRange(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn),
                    inRange,
                    property
                )
            );
        }

        #endregion

        #region Copy, Cut and Paste

        /// <summary>Takes the cells of a range as a snapshot, with their contents and looks: what Copy puts on the clipboard.</summary>
        /// <param name="table">The table.</param>
        /// <param name="range">The cells.</param>
        /// <returns>The cells.</returns>
        public static CadTableCellClip Copy(DxfTable table, DxfCellRange range)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (range == null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            return Snapshot(table, range, false);
        }

        /// <summary>
        /// Cuts the cells of a range, as Cut does: they are copied, then their contents taken out as Delete takes them, their
        /// looks and merges kept, in one undo step. Refused where a cell is locked in its content.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="clip">The cells cut; null where the cut was refused.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Cut(
            CadDocument document,
            string handle,
            DxfCellRange range,
            out CadTableCellClip clip
        )
        {
            clip = null;
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            refused = LockedIn(table, range, true, false);

            if (refused != null)
            {
                return refused;
            }

            CadTableCellClip taken = Snapshot(table, range, true);
            CadEditResult result = ClearContents(document, handle, range);

            if (result.Succeeded)
            {
                clip = taken;
            }

            return result;
        }

        /// <summary>
        /// Pastes cells at the top left cell of a range, as Paste does: each cell given the content and the look of the one
        /// copied (a number with its format, words as they were typed, a block with its scale and attribute values, a field,
        /// a formula with its references moved by the rows and columns it moved), the merges within the cells copied made again
        /// and those the paste meets taken out. A range of whole multiples of the cells copied is filled with them side by side.
        /// What falls past the table's last row or column is left out. One undo step; refused where a cell pasted over is
        /// locked.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">Where: its top left cell is where the cells go, its size how often.</param>
        /// <param name="clip">The cells.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Paste(
            CadDocument document,
            string handle,
            DxfCellRange range,
            CadTableCellClip clip
        )
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            int across =
                (range.RightColumn - range.LeftColumn + 1) % clip.Columns == 0
                    ? (range.RightColumn - range.LeftColumn + 1) / clip.Columns
                    : 1;
            int down =
                (range.BottomRow - range.TopRow + 1) % clip.Rows == 0
                    ? (range.BottomRow - range.TopRow + 1) / clip.Rows
                    : 1;
            int bottom = Math.Min(table.NumRows - 1, range.TopRow + down * clip.Rows - 1);
            int right = Math.Min(table.NumColumns - 1, range.LeftColumn + across * clip.Columns - 1);
            var area = new DxfCellRange(range.TopRow, range.LeftColumn, bottom, right);
            refused = LockedIn(table, area, true, true);

            if (refused != null)
            {
                return refused;
            }

            return Edit(document, handle, "Paste", clip.Name ?? "cells", Name(area), t =>
            {
                for (int tr = 0; tr < down; tr++)
                {
                    for (int tc = 0; tc < across; tc++)
                    {
                        PasteAt(document, t, clip, range.TopRow + tr * clip.Rows, range.LeftColumn + tc * clip.Columns);
                    }
                }
            });
        }

        /// <summary>Matches cells to another, as Match Cell does: the look of the source cell (its alignment, text height, style and colour, fill, borders and data format) given each cell of the range, their contents kept; one undo step.</summary>
        /// <param name="document">The document.</param>
        /// <param name="sourceHandle">The handle of the table the cell matched from is in.</param>
        /// <param name="sourceRow">Its row, from nought.</param>
        /// <param name="sourceColumn">Its column, from nought.</param>
        /// <param name="handle">The handle of the table matched.</param>
        /// <param name="range">The cells matched.</param>
        /// <returns>What came of it; refused where a cell matched is locked in its format.</returns>
        public static CadEditResult MatchCell(
            CadDocument document,
            string sourceHandle,
            int sourceRow,
            int sourceColumn,
            string handle,
            DxfCellRange range
        )
        {
            DxfTable source = Find(document, sourceHandle, out CadEditResult refused);

            if (source == null)
            {
                return refused;
            }

            if (!InTable(source, sourceRow, sourceColumn))
            {
                return CadEditResult.Refuse("The table has no cell " + CellName(sourceRow, sourceColumn) + ".");
            }

            DxfTable table = Find(document, handle, out refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            refused = LockedIn(table, range, false, true);

            if (refused != null)
            {
                return refused;
            }

            CadTableClipCell look = SnapshotCell(source, sourceRow, sourceColumn);
            return Edit(document, handle, "Match Cell", CellName(sourceRow, sourceColumn), Name(range), t =>
                Each(range, (r, c) => MatchLook(t, r, c, look))
            );
        }

        private static CadTableCellClip Snapshot(DxfTable table, DxfCellRange range, bool isCut)
        {
            int rows = range.BottomRow - range.TopRow + 1;
            int columns = range.RightColumn - range.LeftColumn + 1;
            var clip = new CadTableCellClip(rows, columns, isCut)
            {
                Name = Name(range),
                SourceTop = range.TopRow,
                SourceLeft = range.LeftColumn,
            };

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    clip.Cells[r, c] = SnapshotCell(table, range.TopRow + r, range.LeftColumn + c);
                }
            }

            for (int r = range.TopRow; r <= range.BottomRow; r++)
            {
                for (int c = range.LeftColumn; c <= range.RightColumn; c++)
                {
                    if (
                        table.IsMergedCell(r, c, out DxfCellRange merged)
                        && merged.TopRow == r
                        && merged.LeftColumn == c
                    )
                    {
                        if (merged.BottomRow <= range.BottomRow && merged.RightColumn <= range.RightColumn)
                        {
                            clip.Merges.Add(
                                new[]
                                {
                                    r - range.TopRow,
                                    c - range.LeftColumn,
                                    merged.BottomRow - range.TopRow,
                                    merged.RightColumn - range.LeftColumn,
                                }
                            );
                        }
                    }
                }
            }

            return clip;
        }

        private static CadTableClipCell SnapshotCell(DxfTable table, int row, int column)
        {
            var cell = new CadTableClipCell { IsEmpty = table.IsEmpty(row, column) };

            if (table.CellType(row, column) == DxfTableCellType.BlockCell)
            {
                DxfBlockTableRecord block = table.GetBlockTableRecord(row, column);
                cell.IsBlock = block != null;
                cell.BlockName = block?.Name;
                cell.BlockScale = table.BlockScale(row, column);
                cell.AutoFit = table.IsAutoScale(row, column);

                foreach (DxfEntity entity in block ?? Enumerable.Empty<DxfEntity>())
                {
                    if (entity is DxfAttributeDefinition definition && !definition.Constant)
                    {
                        string value = table.GetBlockAttributeValue(row, column, definition);

                        if (value != null)
                        {
                            cell.Attributes[definition.Tag] = value;
                        }
                    }
                }
            }
            else
            {
                cell.Text = table.TextString(row, column);
                cell.Value = table.Value(row, column);
                cell.Kind = table.DataType(row, column);
                cell.Unit = table.UnitType(row, column);
                cell.Format = table.Format(row, column);
                cell.Formula = table.GetFormula(row, column, 0);
                cell.FieldCode = cell.Formula == null ? table.GetFieldCode(row, column, 0) : null;
            }

            cell.Rotation = table.GetRotation(row, column, 0);
            cell.Alignment = table.Alignment(row, column);
            cell.TextHeight = table.TextHeight(row, column);
            cell.TextStyle = table.TextStyle(row, column)?.Name;
            cell.ContentColor = table.ContentColor(row, column);
            cell.Background = table.BackgroundColor(row, column);
            cell.NoBackground = table.IsBackgroundColorNone(row, column);

            for (int k = 0; k < 4; k++)
            {
                cell.EdgeColors[k] = table.GridColor(row, column, EdgeMasks[k]);
                cell.EdgeWeights[k] = table.GridLineWeight(row, column, EdgeMasks[k]);
                cell.EdgeShown[k] = table.GridVisibility(row, column, EdgeMasks[k]);
            }

            return cell;
        }

        private static void PasteAt(CadDocument document, DxfTable table, CadTableCellClip clip, int top, int left)
        {
            for (int r = 0; r < clip.Rows && top + r < table.NumRows; r++)
            {
                for (int c = 0; c < clip.Columns && left + c < table.NumColumns; c++)
                {
                    if (table.IsMergedCell(top + r, left + c, out DxfCellRange merged))
                    {
                        table.UnmergeCells(merged);
                    }
                }
            }

            for (int r = 0; r < clip.Rows && top + r < table.NumRows; r++)
            {
                for (int c = 0; c < clip.Columns && left + c < table.NumColumns; c++)
                {
                    PasteCell(
                        document,
                        table,
                        top + r,
                        left + c,
                        clip.Cells[r, c],
                        top - clip.SourceTop,
                        left - clip.SourceLeft
                    );
                }
            }

            foreach (int[] merge in clip.Merges)
            {
                if (top + merge[2] < table.NumRows && left + merge[3] < table.NumColumns)
                {
                    table.MergeCells(
                        new DxfCellRange(top + merge[0], left + merge[1], top + merge[2], left + merge[3])
                    );
                }
            }
        }

        private static void PasteCell(
            CadDocument document,
            DxfTable table,
            int row,
            int column,
            CadTableClipCell cell,
            int shiftRows,
            int shiftColumns
        )
        {
            table.RemoveAllOverrides(row, column);

            if (cell.IsBlock)
            {
                DxfBlockTableRecord block = document.Database.BlockTable.Has(cell.BlockName)
                    ? document.Database.BlockTable[cell.BlockName]
                    : null;

                if (block != null)
                {
                    table.SetBlockTableRecord(row, column, block, cell.AutoFit);

                    if (!cell.AutoFit)
                    {
                        table.SetBlockScale(row, column, cell.BlockScale);
                    }

                    table.SetBlockRotation(row, column, cell.Rotation);

                    foreach (DxfEntity entity in block)
                    {
                        if (
                            entity is DxfAttributeDefinition definition
                            && !definition.Constant
                            && cell.Attributes.TryGetValue(definition.Tag, out string value)
                        )
                        {
                            table.SetBlockAttributeValue(row, column, definition, value);
                        }
                    }
                }
            }
            else
            {
                if (table.CellType(row, column) == DxfTableCellType.BlockCell)
                {
                    table.SetCellType(row, column, DxfTableCellType.TextCell);
                }

                PasteContent(table, row, column, cell, shiftRows, shiftColumns);
            }

            MatchLook(table, row, column, cell, true);
        }

        private static void PasteContent(
            DxfTable table,
            int row,
            int column,
            CadTableClipCell cell,
            int shiftRows,
            int shiftColumns
        )
        {
            if (cell.IsEmpty)
            {
                table.ResetValue(row, column);
            }
            else if (cell.Formula != null && table.Item.File.Supports(1018))
            {
                // The references move as the cell moved from the corner of the cells copied to its place.
                table.SetFormula(row, column, 0, CadTableFormula.Shift(cell.Formula, shiftRows, shiftColumns));
                table.SetFieldValue(row, column, cell.Text);
            }
            else if (cell.FieldCode != null && table.Item.File.Supports(1018))
            {
                table.SetFieldCode(row, column, 0, "%<" + cell.FieldCode + ">%");
                table.SetFieldValue(row, column, cell.Text);
            }
            else if (cell.Value is int || cell.Value is double)
            {
                table.SetValue(row, column, cell.Value);

                if (cell.Kind == DxfDataType.Long || cell.Kind == DxfDataType.Double)
                {
                    try
                    {
                        table.SetDataType(row, column, cell.Kind, cell.Unit);
                    }
                    catch (Exception exception)
                        when (exception is ArgumentException || exception is InvalidOperationException)
                    {
                        // A cell that takes no unit keeps its kind.
                    }
                }

                if (cell.Format.Length != 0)
                {
                    table.SetFormat(row, column, cell.Format);
                }
            }
            else if (
                cell.Kind == DxfDataType.Date
                || cell.Kind == DxfDataType.Point3d
                || cell.Kind == DxfDataType.Point
            )
            {
                if (!table.SetValueFromText(row, column, cell.Text))
                {
                    table.SetTextString(row, column, cell.Text);
                }
            }
            else
            {
                table.SetTextString(row, column, cell.Text);
            }
        }

        /// <summary>Gives a cell the look of a snapshot: the overrides it had (alignment, text height, style, colours, fill, borders) in place of the cell's own.</summary>
        private static void MatchLook(DxfTable table, int row, int column, CadTableClipCell look, bool pasted = false)
        {
            if (!pasted)
            {
                table.RemoveAllOverrides(row, column);
            }

            if (look.Alignment.HasValue)
            {
                table.SetAlignment(row, column, look.Alignment.Value);
            }

            if (look.TextHeight.HasValue)
            {
                table.SetTextHeight(row, column, look.TextHeight.Value);
            }

            DxfDatabase database = table.Item.File.Database;

            if (look.TextStyle != null && database.TextStyleTable.Has(look.TextStyle))
            {
                table.SetTextStyle(row, column, database.TextStyleTable[look.TextStyle]);
            }

            if (look.ContentColor != null)
            {
                table.SetContentColor(row, column, look.ContentColor);
            }

            if (look.Background != null)
            {
                table.SetBackgroundColor(row, column, look.Background);
            }

            if (look.NoBackground.HasValue)
            {
                table.SetBackgroundColorNone(row, column, look.NoBackground.Value);
            }

            for (int k = 0; k < 4; k++)
            {
                if (look.EdgeColors[k] != null)
                {
                    table.SetGridColor(row, column, EdgeLines[k], look.EdgeColors[k]);
                }

                if (look.EdgeWeights[k].HasValue)
                {
                    table.SetGridLineWeight(row, column, EdgeLines[k], look.EdgeWeights[k].Value);
                }

                if (look.EdgeShown[k].HasValue)
                {
                    table.SetGridVisibility(row, column, EdgeLines[k], look.EdgeShown[k].Value);
                }
            }

            if (
                !pasted
                && !look.IsBlock
                && look.Format.Length != 0
                && (look.Kind == DxfDataType.Long || look.Kind == DxfDataType.Double)
            )
            {
                DxfDataType kind = table.DataType(row, column);

                if (kind == DxfDataType.Long || kind == DxfDataType.Double)
                {
                    table.SetFormat(row, column, look.Format);
                }
            }
        }

        #endregion
    }
}
