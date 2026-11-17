using System;
using System.IO;
using System.Linq;

namespace GeometryHelper.Cad
{
    /// <summary>Thrown when the source of a data link cannot be read: the file or its sheet or range is not there, or it is no workbook.</summary>
    public sealed class CadDataSourceException : Exception
    {
        /// <summary>Makes the exception.</summary>
        /// <param name="message">What went wrong, in AutoCAD's words where it has them.</param>
        public CadDataSourceException(string message)
            : base(message) { }

        /// <summary>Makes the exception with the one that caused it.</summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="inner">The cause.</param>
        public CadDataSourceException(string message, Exception inner)
            : base(message, inner) { }
    }

    /// <summary>
    /// Where the data of a data link is, as AutoCAD's Excel data adapter (<c>AcExcel</c>) words it in the connection string of
    /// a link: the file, then <c>!</c> and the sheet, then <c>!</c> and the range, <c>parts.xlsx!Sheet1!A1:C5</c>; the file and
    /// the sheet alone for the entire sheet, <c>parts.xlsx!Sheet1</c>; the file and a name for a named range,
    /// <c>parts.xlsx!Prices</c>. A file is an .xlsx workbook or a .csv file; a relative path is of the drawing's folder.
    /// </summary>
    public static class CadDataSource
    {
        /// <summary>The data adapter of a link to a spreadsheet, as AutoCAD names it.</summary>
        public const string ExcelAdapter = "AcExcel";

        private static readonly string[] Extensions = { ".xlsx", ".xlsm", ".xls", ".csv" };

        /// <summary>Puts a connection string together.</summary>
        /// <param name="file">The file, a full path or a relative one.</param>
        /// <param name="sheet">The sheet, or the named range where <paramref name="range"/> is empty; empty for none.</param>
        /// <param name="range">The cells, <c>A1:C5</c>; empty for the sheet or the named range.</param>
        /// <returns>The connection string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the file is null.</exception>
        public static string Compose(string file, string sheet, string range)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            return file
                + (string.IsNullOrEmpty(sheet) ? string.Empty : "!" + sheet)
                + (string.IsNullOrEmpty(range) ? string.Empty : "!" + range);
        }

        /// <summary>Takes a connection string apart: the file and what follows its extension.</summary>
        /// <param name="connection">The connection string.</param>
        /// <param name="file">The file.</param>
        /// <param name="sheet">The first part after the file, a sheet or a named range; empty for none.</param>
        /// <param name="range">The second part after the file, the cells; empty for none.</param>
        /// <returns>True when the string names a file with an extension of a workbook or a CSV file.</returns>
        public static bool TryParse(string connection, out string file, out string sheet, out string range)
        {
            file = string.Empty;
            sheet = string.Empty;
            range = string.Empty;

            if (string.IsNullOrWhiteSpace(connection))
            {
                return false;
            }

            int split = connection.Length;

            for (int i = 0; i < connection.Length; i++)
            {
                if (
                    connection[i] == '!'
                    && Extensions.Any(e => connection.Substring(0, i).EndsWith(e, StringComparison.OrdinalIgnoreCase))
                )
                {
                    split = i;
                    break;
                }
            }

            string named = connection.Substring(0, split).Trim();
            file = named;

            if (!Extensions.Any(e => named.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string[] parts =
                split < connection.Length ? connection.Substring(split + 1).Split(new[] { '!' }, 2) : new string[0];
            sheet = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            range = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            return true;
        }

        /// <summary>
        /// Where a file of a connection string is: itself where it is a full path, else in the folder of the drawing; null where
        /// it is relative and the drawing has no folder yet.
        /// </summary>
        /// <param name="file">The file as the connection string names it.</param>
        /// <param name="drawingFolder">The drawing's folder; null or empty for a drawing not saved.</param>
        /// <returns>The full path, or null.</returns>
        public static string Resolve(string file, string drawingFolder)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(file))
                {
                    return file;
                }

                return string.IsNullOrEmpty(drawingFolder) ? null : Path.GetFullPath(Path.Combine(drawingFolder, file));
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                return null;
            }
        }

        /// <summary>What a connection string links, in the words of the Data Link Manager's details: <c>Entire sheet: Sheet1</c>, <c>Named range: Prices</c> or <c>Range: A1:C5</c>.</summary>
        /// <param name="connection">The connection string.</param>
        /// <returns>The details; empty for a string that names no file.</returns>
        public static string Describe(string connection)
        {
            if (!TryParse(connection, out _, out string sheet, out string range))
            {
                return string.Empty;
            }

            return range.Length > 0 ? "Range: " + sheet + "!" + range
                : sheet.Length == 0 ? string.Empty
                : "Entire sheet: " + sheet;
        }

        /// <summary>
        /// Reads what a connection string links. A workbook's sheet by its name, else its named range, else (for a file of one
        /// sheet, a CSV file) the file's own sheet; the cells of a range cut from the sheet, whole columns to the sheet's last row.
        /// </summary>
        /// <param name="connection">The connection string.</param>
        /// <param name="drawingFolder">The drawing's folder, which a relative path is of.</param>
        /// <returns>The cells, from the first cell of the range.</returns>
        /// <exception cref="CadDataSourceException">Thrown when the file, the sheet, the named range or the range is not there or cannot be read.</exception>
        public static CadSheet Read(string connection, string drawingFolder)
        {
            Locate(connection, drawingFolder, out string path, out string sheet, out string range);
            bool csv = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

            try
            {
                if (csv)
                {
                    CadSheet all = CadCsvFile.Read(path);
                    return range.Length == 0 ? all : all.Cut(RangeOf(range));
                }

                CadWorkbook book = CadWorkbook.Open(path);

                if (range.Length > 0)
                {
                    string name = book.SheetNames.FirstOrDefault(n =>
                        string.Equals(n, sheet, StringComparison.OrdinalIgnoreCase)
                    );

                    if (name == null)
                    {
                        throw new CadDataSourceException(
                            "Excel Sheet Not Found: the linked Excel sheet cannot be found."
                        );
                    }

                    return book.ReadSheet(name).Cut(RangeOf(range));
                }

                string whole = book.SheetNames.FirstOrDefault(n =>
                    string.Equals(n, sheet, StringComparison.OrdinalIgnoreCase)
                );

                if (whole != null)
                {
                    return book.ReadSheet(whole);
                }

                if (sheet.Length == 0)
                {
                    return book.ReadSheet(book.SheetNames[0]);
                }

                if (book.NamedRanges.TryGetValue(sheet, out string reference))
                {
                    int bang = reference.LastIndexOf('!');
                    string owner = reference.Substring(0, bang).Trim('\'');
                    string cells = reference.Substring(bang + 1);
                    return book.ReadSheet(owner).Cut(RangeOf(cells));
                }

                throw new CadDataSourceException("Named range not found: " + sheet);
            }
            catch (InvalidDataException exception)
            {
                throw new CadDataSourceException("The file selected is not a valid Excel file.", exception);
            }
            catch (ArgumentException exception)
            {
                throw new CadDataSourceException(
                    "Excel Sheet Not Found: the linked Excel sheet cannot be found.",
                    exception
                );
            }
            catch (IOException exception)
            {
                throw new CadDataSourceException("Access to file denied: " + exception.Message, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new CadDataSourceException("Access to file denied: " + exception.Message, exception);
            }
        }

        /// <summary>
        /// Writes cells back to what a connection string links, from its first cell: a CSV file is written whole again (its
        /// other cells kept), a workbook has the cells put into its sheet and every other part left as it is.
        /// </summary>
        /// <param name="connection">The connection string.</param>
        /// <param name="drawingFolder">The drawing's folder, which a relative path is of.</param>
        /// <param name="values">The cells.</param>
        /// <returns>The number of cells written.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the values are null.</exception>
        /// <exception cref="CadDataSourceException">Thrown when the source is not there, is a binary .xls workbook, or cannot be written.</exception>
        public static int Write(string connection, string drawingFolder, CadSheet values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            Locate(connection, drawingFolder, out string path, out string sheet, out string range);
            int top = 0;
            int left = 0;

            try
            {
                string target = sheet;

                if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    if (range.Length > 0)
                    {
                        CadSheetRange cut = RangeOf(range);
                        top = cut.TopRow;
                        left = cut.LeftColumn;
                    }

                    CadSheet all = CadCsvFile.Read(path);
                    var merged = new CadSheet(all.Name);

                    for (int r = 0; r < all.RowCount; r++)
                    {
                        for (int c = 0; c < all.ColumnCount; c++)
                        {
                            merged.Set(r, c, all[r, c]);
                        }
                    }

                    for (int r = 0; r < values.RowCount; r++)
                    {
                        for (int c = 0; c < values.ColumnCount; c++)
                        {
                            merged.Set(top + r, left + c, values[r, c]);
                        }
                    }

                    merged.Size(all.RowCount, all.ColumnCount);
                    bool mark = CadCsvFile.HasByteOrderMark(path);
                    CadCsvFile.Write(path, merged, mark);
                    return values.RowCount * values.ColumnCount;
                }

                CadWorkbook book = CadWorkbook.Open(path);

                if (range.Length > 0)
                {
                    CadSheetRange cut = RangeOf(range);
                    top = cut.TopRow;
                    left = cut.LeftColumn;
                }
                else if (!book.SheetNames.Any(n => string.Equals(n, sheet, StringComparison.OrdinalIgnoreCase)))
                {
                    if (sheet.Length > 0 && book.NamedRanges.TryGetValue(sheet, out string reference))
                    {
                        int bang = reference.LastIndexOf('!');
                        target = reference.Substring(0, bang).Trim('\'');
                        CadSheetRange cut = RangeOf(reference.Substring(bang + 1));
                        top = cut.TopRow;
                        left = cut.LeftColumn;
                    }
                    else if (sheet.Length == 0)
                    {
                        target = book.SheetNames[0];
                    }
                    else
                    {
                        throw new CadDataSourceException("Named range not found: " + sheet);
                    }
                }

                return CadWorkbook.WriteBack(path, target, top, left, values);
            }
            catch (InvalidDataException exception)
            {
                throw new CadDataSourceException("The file selected is not a valid Excel file.", exception);
            }
            catch (ArgumentException exception)
            {
                throw new CadDataSourceException(
                    "Excel Sheet Not Found: the linked Excel sheet cannot be found.",
                    exception
                );
            }
            catch (IOException exception)
            {
                throw new CadDataSourceException("Access to file denied: " + exception.Message, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new CadDataSourceException("Access to file denied: " + exception.Message, exception);
            }
        }

        private static void Locate(
            string connection,
            string drawingFolder,
            out string path,
            out string sheet,
            out string range
        )
        {
            if (!TryParse(connection, out string file, out sheet, out range))
            {
                throw new CadDataSourceException(
                    "The file selected is not a valid Excel file. Please select a different file."
                );
            }

            if (file.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
            {
                throw new CadDataSourceException(
                    "The file is an earlier format that is no longer supported: save the workbook as .xlsx."
                );
            }

            path = Resolve(file, drawingFolder);

            if (path == null || !File.Exists(path))
            {
                throw new CadDataSourceException("The file \"" + file + "\" cannot be found.");
            }
        }

        private static CadSheetRange RangeOf(string text) =>
            CadSheetRange.TryParse(text, out CadSheetRange range)
                ? range
                : throw new CadDataSourceException(
                    "Invalid Range: the range entered is invalid. Enter A1:D10 for a rectangular region, A:A for one whole column, A:D for a set of columns."
                );
    }
}
