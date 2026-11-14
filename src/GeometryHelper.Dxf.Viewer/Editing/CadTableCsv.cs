using System;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A table written as the comma separated values TABLEEXPORT makes of it: a line for each row, a field for each column, the
    /// text of each cell as it shows (its formatting codes taken off, as AutoCAD's <c>IgnoreMtextFormat</c> reads it), the fields
    /// of a block of merged cells but the first empty. A field with a comma, a double quote or a line break in it is put in
    /// double quotes, a quote inside doubled; lines end with a carriage return and a line feed.
    /// </summary>
    internal static class CadTableCsv
    {
        /// <summary>A stack of an MTEXT: <c>\S1/2;</c>, <c>\S1^2;</c> or <c>\S1#2;</c>.</summary>
        private static readonly Regex Stack = new Regex(@"\\S([^;\\^#/]*)[/^#]([^;]*);", RegexOptions.CultureInvariant);

        /// <summary>
        /// Writes the table. A table of two rows and two columns holding <c>Name</c>, <c>Qty</c>, <c>Nut, M8</c> and
        /// <c>12</c> gives <c>Name,Qty</c> then <c>"Nut, M8",12</c>, each line ended by a carriage return and a line feed.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <returns>The text of the file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the table is null.</exception>
        internal static string Of(DxfTable table)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            var text = new StringBuilder();

            for (int row = 0; row < table.NumRows; row++)
            {
                for (int column = 0; column < table.NumColumns; column++)
                {
                    if (column > 0)
                    {
                        text.Append(',');
                    }

                    text.Append(Field(CellText(table, row, column)));
                }

                text.Append("\r\n");
            }

            return text.ToString();
        }

        /// <summary>The text of a cell as it shows: nothing for a cell of a merged block that is not its first, else its text with the codes of its look taken off.</summary>
        private static string CellText(DxfTable table, int row, int column)
        {
            DxfCellRange block = table.GetMergeRange(row, column);

            if (block.TopRow != row || block.LeftColumn != column)
            {
                return string.Empty;
            }

            string codes = Stack.Replace(table.TextString(row, column) ?? string.Empty, "$1/$2");
            return CadMTextContent.Parse(codes, 1.0).PlainText;
        }

        private static string Field(string value) =>
            value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
