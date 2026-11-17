using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a cell of a <see cref="CadSheet"/> holds.</summary>
    public enum CadSheetCellKind
    {
        /// <summary>Nothing.</summary>
        Empty = 0,

        /// <summary>Words.</summary>
        Text = 1,

        /// <summary>A number.</summary>
        Number = 2,

        /// <summary>A date or a time, as the days since 30 December 1899 in <see cref="CadSheetCell.Number"/>.</summary>
        Date = 3,

        /// <summary>TRUE or FALSE, 1 or 0 in <see cref="CadSheetCell.Number"/>.</summary>
        Boolean = 4,
    }

    /// <summary>
    /// One cell of a spreadsheet as a data link reads it: its value, the words it shows (a number as its format makes it, 0.5
    /// in the format 0% shown 50%), and, from a workbook, the few marks of its look a table takes over (<see cref="Alignment"/>,
    /// <see cref="Fill"/> and <see cref="FontColor"/>).
    /// </summary>
    public sealed class CadSheetCell
    {
        /// <summary>A cell with nothing in it.</summary>
        public static CadSheetCell Empty { get; } = new CadSheetCell(CadSheetCellKind.Empty, string.Empty, 0.0);

        /// <summary>Makes a cell.</summary>
        /// <param name="kind">What it holds.</param>
        /// <param name="text">The words it shows.</param>
        /// <param name="number">The number of a number, a date or a boolean.</param>
        public CadSheetCell(CadSheetCellKind kind, string text, double number)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            Number = number;
        }

        /// <summary>Gets what the cell holds.</summary>
        public CadSheetCellKind Kind { get; }

        /// <summary>Gets the words the cell shows; empty for an empty cell.</summary>
        public string Text { get; }

        /// <summary>Gets the value of a number, a date or a boolean; nought for the rest.</summary>
        public double Number { get; }

        /// <summary>Gets or sets the horizontal alignment the workbook gives the cell: 0 general, 1 left, 2 centre, 3 right.</summary>
        public int Alignment { get; set; }

        /// <summary>Gets or sets the fill the workbook gives the cell as 0xRRGGBB; null for none.</summary>
        public int? Fill { get; set; }

        /// <summary>Gets or sets the colour of the cell's words as 0xRRGGBB; null where the workbook gives none.</summary>
        public int? FontColor { get; set; }

        /// <summary>Gives a cell of words.</summary>
        /// <param name="text">The words; null or empty gives an empty cell.</param>
        /// <returns>The cell.</returns>
        public static CadSheetCell OfText(string text) =>
            string.IsNullOrEmpty(text) ? Empty : new CadSheetCell(CadSheetCellKind.Text, text, 0.0);

        /// <summary>Gives a cell of a number, shown as <c>R</c> shows it (12 as 12, 0.25 as 0.25).</summary>
        /// <param name="value">The number.</param>
        /// <returns>The cell.</returns>
        public static CadSheetCell OfNumber(double value) =>
            new CadSheetCell(CadSheetCellKind.Number, value.ToString("R", CultureInfo.InvariantCulture), value);

        /// <summary>Gives the cell as a line for debugging: its words.</summary>
        /// <returns>The words.</returns>
        public override string ToString() => Text;
    }

    /// <summary>
    /// A block of spreadsheet cells, a sheet or a range of one, as a data link reads it: its rows and columns, from the first
    /// cell of the block, and what each holds. A row shorter than the widest reads as empty beyond its end.
    /// </summary>
    public sealed class CadSheet
    {
        private readonly List<CadSheetCell[]> _rows = new List<CadSheetCell[]>();

        /// <summary>Makes a sheet.</summary>
        /// <param name="name">The sheet's name; the file's name for a CSV file.</param>
        public CadSheet(string name)
        {
            Name = name ?? string.Empty;
        }

        /// <summary>Gets the name of the sheet.</summary>
        public string Name { get; }

        /// <summary>Gets the number of rows.</summary>
        public int RowCount => _rows.Count;

        /// <summary>Gets the number of columns: those of the widest row.</summary>
        public int ColumnCount { get; private set; }

        /// <summary>Gets the cell at a row and a column, from nought; an empty cell where the sheet has none there.</summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the row or the column is below nought.</exception>
        public CadSheetCell this[int row, int column]
        {
            get
            {
                if (row < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(row));
                }

                if (column < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(column));
                }

                return row < _rows.Count && column < _rows[row].Length
                    ? _rows[row][column] ?? CadSheetCell.Empty
                    : CadSheetCell.Empty;
            }
        }

        /// <summary>Puts a cell, the sheet growing to hold it.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="cell">The cell.</param>
        /// <exception cref="ArgumentNullException">Thrown when the cell is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the row or the column is below nought.</exception>
        public void Set(int row, int column, CadSheetCell cell)
        {
            if (cell == null)
            {
                throw new ArgumentNullException(nameof(cell));
            }

            if (row < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (column < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }

            while (_rows.Count <= row)
            {
                _rows.Add(Array.Empty<CadSheetCell>());
            }

            CadSheetCell[] cells = _rows[row];

            if (column >= cells.Length)
            {
                Array.Resize(ref cells, column + 1);
                _rows[row] = cells;
            }

            cells[column] = cell;
            ColumnCount = Math.Max(ColumnCount, column + 1);
        }

        /// <summary>
        /// Gives a block of the sheet, as a data link's range cuts one: its rows and columns, an end below nought meaning the
        /// sheet's last row or column.
        /// </summary>
        /// <param name="range">The range.</param>
        /// <returns>The block, a new sheet of the same name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the range is null.</exception>
        public CadSheet Cut(CadSheetRange range)
        {
            if (range == null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            int bottom = range.BottomRow >= 0 ? range.BottomRow : RowCount - 1;
            int right = range.RightColumn >= 0 ? range.RightColumn : ColumnCount - 1;
            var cut = new CadSheet(Name);

            for (int r = range.TopRow; r <= bottom; r++)
            {
                for (int c = range.LeftColumn; c <= right; c++)
                {
                    cut.Set(r - range.TopRow, c - range.LeftColumn, this[r, c]);
                }
            }

            // A block cut to a range of its own size keeps that size even where its last cells are empty.
            cut.Size(Math.Max(0, bottom - range.TopRow + 1), Math.Max(0, right - range.LeftColumn + 1));
            return cut;
        }

        /// <summary>Makes the sheet at least a number of rows and columns, as a range of empty cells at its end is still part of it.</summary>
        internal void Size(int rows, int columns)
        {
            while (_rows.Count < rows)
            {
                _rows.Add(Array.Empty<CadSheetCell>());
            }

            ColumnCount = Math.Max(ColumnCount, columns);
        }
    }

    /// <summary>
    /// A range of cells in the words of a spreadsheet: <c>A1:D10</c> a block, <c>B2</c> one cell, <c>A:A</c> a whole column,
    /// <c>A:D</c> a set of columns. Rows and columns count from nought; an end below nought is the sheet's own end.
    /// </summary>
    public sealed class CadSheetRange
    {
        /// <summary>Makes a range.</summary>
        /// <param name="topRow">The first row, from nought.</param>
        /// <param name="leftColumn">The first column, from nought.</param>
        /// <param name="bottomRow">The last row; below nought for the sheet's last.</param>
        /// <param name="rightColumn">The last column; below nought for the sheet's last.</param>
        public CadSheetRange(int topRow, int leftColumn, int bottomRow, int rightColumn)
        {
            TopRow = topRow;
            LeftColumn = leftColumn;
            BottomRow = bottomRow;
            RightColumn = rightColumn;
        }

        /// <summary>Gets the range of the whole sheet.</summary>
        public static CadSheetRange Everything { get; } = new CadSheetRange(0, 0, -1, -1);

        /// <summary>Gets the first row, from nought.</summary>
        public int TopRow { get; }

        /// <summary>Gets the first column, from nought.</summary>
        public int LeftColumn { get; }

        /// <summary>Gets the last row, from nought; below nought for the sheet's last.</summary>
        public int BottomRow { get; }

        /// <summary>Gets the last column, from nought; below nought for the sheet's last.</summary>
        public int RightColumn { get; }

        /// <summary>
        /// Reads a range: <c>A1:D10</c>, <c>$A$1:$D$10</c>, <c>B2</c>, <c>A:A</c> or <c>A:D</c>, letters of either case, the two
        /// ends in either order.
        /// </summary>
        /// <param name="text">The words.</param>
        /// <param name="range">The range, or null.</param>
        /// <returns>True when the words are a range.</returns>
        public static bool TryParse(string text, out CadSheetRange range)
        {
            range = null;
            string[] ends = (text ?? string.Empty).Trim().Replace("$", string.Empty).Split(':');

            if (ends.Length < 1 || ends.Length > 2 || ends[0].Length == 0)
            {
                return false;
            }

            if (!TryEnd(ends[0], out int row1, out int column1))
            {
                return false;
            }

            int row2 = row1;
            int column2 = column1;

            if (ends.Length == 2 && !TryEnd(ends[1], out row2, out column2))
            {
                return false;
            }

            // A whole column has no row on either end.
            bool wholeColumns = row1 < 0 && row2 < 0;

            if (!wholeColumns && (row1 < 0 || row2 < 0))
            {
                return false;
            }

            range = new CadSheetRange(
                wholeColumns ? 0 : Math.Min(row1, row2),
                Math.Min(column1, column2),
                wholeColumns ? -1 : Math.Max(row1, row2),
                Math.Max(column1, column2)
            );
            return true;
        }

        /// <summary>The name of a column, A for nought, Z for 25, AA for 26.</summary>
        /// <param name="column">The column, from nought.</param>
        /// <returns>Its letters.</returns>
        public static string ColumnName(int column)
        {
            var letters = new StringBuilder();

            for (int n = column + 1; n > 0; n = (n - 1) / 26)
            {
                letters.Insert(0, (char)('A' + (n - 1) % 26));
            }

            return letters.ToString();
        }

        /// <summary>Gives the range in a spreadsheet's words, <c>A1:D10</c>; <c>A:D</c> where it takes whole columns.</summary>
        /// <returns>The words.</returns>
        public override string ToString()
        {
            string left = ColumnName(LeftColumn);
            string right = ColumnName(RightColumn < 0 ? LeftColumn : RightColumn);

            if (BottomRow < 0)
            {
                return left + ":" + right;
            }

            string first = left + (TopRow + 1).ToString(CultureInfo.InvariantCulture);
            string last = right + (BottomRow + 1).ToString(CultureInfo.InvariantCulture);
            return first == last ? first : first + ":" + last;
        }

        /// <summary>One end of a range: letters, then digits or none; the row is below nought where there are no digits.</summary>
        private static bool TryEnd(string end, out int row, out int column)
        {
            row = -1;
            column = 0;
            int i = 0;

            while (i < end.Length && char.IsLetter(end[i]) && end[i] < 128)
            {
                column = column * 26 + (char.ToUpperInvariant(end[i]) - 'A' + 1);
                i++;
            }

            if (i == 0 || i > 3)
            {
                return false;
            }

            column--;

            if (i == end.Length)
            {
                return true;
            }

            if (
                !int.TryParse(end.Substring(i), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                || number < 1
            )
            {
                return false;
            }

            row = number - 1;
            return true;
        }
    }
}
