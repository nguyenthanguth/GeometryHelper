using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A comma separated file as a data link reads and writes it: a line for each row, a field for each column, a field in
    /// double quotes where it holds a comma, a quote or a line break, a quote inside doubled. A field that reads as a number
    /// is a number of the words it shows (007 stays 007); every other is text.
    /// </summary>
    public static class CadCsvFile
    {
        /// <summary>Reads a file. A file of UTF-8 or UTF-16 with a byte order mark is read as that, any other as UTF-8, the system's code page where it is not.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The sheet, named for the file without its extension.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static CadSheet Read(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            byte[] bytes;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                bytes = copy.ToArray();
            }

            return Parse(Decode(bytes), Path.GetFileNameWithoutExtension(path));
        }

        /// <summary>Reads the words of a file.</summary>
        /// <param name="text">The text.</param>
        /// <param name="name">The sheet's name.</param>
        /// <returns>The sheet.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        public static CadSheet Parse(string text, string name)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var sheet = new CadSheet(name);
            var field = new StringBuilder();
            var row = new List<string>();
            bool quoted = false;
            bool wasQuoted = false;
            int rowNumber = 0;

            void EndField()
            {
                row.Add(field.ToString());
                field.Clear();
                wasQuoted = false;
            }

            void EndRow()
            {
                EndField();

                for (int c = 0; c < row.Count; c++)
                {
                    sheet.Set(rowNumber, c, Cell(row[c]));
                }

                sheet.Size(rowNumber + 1, row.Count);
                row.Clear();
                rowNumber++;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (quoted)
                {
                    if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else if (ch == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        field.Append(ch);
                    }

                    continue;
                }

                if (ch == '"' && field.Length == 0 && !wasQuoted)
                {
                    quoted = true;
                    wasQuoted = true;
                }
                else if (ch == ',')
                {
                    EndField();
                }
                else if (ch == '\r' || ch == '\n')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    EndRow();
                }
                else
                {
                    field.Append(ch);
                }
            }

            // A last line with no line break after it still counts, an empty one does not.
            if (field.Length > 0 || row.Count > 0 || wasQuoted)
            {
                EndRow();
            }

            return sheet;
        }

        /// <summary>
        /// Writes cells as a file: the words each shows, a field with a comma, a quote or a line break in double quotes, lines
        /// ending in a carriage return and a line feed, the file in UTF-8 with no byte order mark.
        /// </summary>
        /// <param name="path">The file; one there is written over.</param>
        /// <param name="sheet">The cells.</param>
        /// <param name="byteOrderMark">Whether the file begins with the UTF-8 mark, as a file Excel saved does.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path or the sheet is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public static void Write(string path, CadSheet sheet, bool byteOrderMark = false)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            File.WriteAllText(path, ToText(sheet), new UTF8Encoding(byteOrderMark));
        }

        /// <summary>The words of a file for cells.</summary>
        /// <param name="sheet">The cells.</param>
        /// <returns>The text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sheet is null.</exception>
        public static string ToText(CadSheet sheet)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            var text = new StringBuilder();

            for (int r = 0; r < sheet.RowCount; r++)
            {
                for (int c = 0; c < sheet.ColumnCount; c++)
                {
                    if (c > 0)
                    {
                        text.Append(',');
                    }

                    string value = sheet[r, c].Text;
                    text.Append(
                        value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
                            ? value
                            : "\"" + value.Replace("\"", "\"\"") + "\""
                    );
                }

                text.Append("\r\n");
            }

            return text.ToString();
        }

        /// <summary>Whether a file begins with the UTF-8 byte order mark, which a rewrite of it keeps.</summary>
        /// <param name="path">The file.</param>
        /// <returns>True when it does.</returns>
        public static bool HasByteOrderMark(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var mark = new byte[3];
                    return stream.Read(mark, 0, 3) == 3 && mark[0] == 0xEF && mark[1] == 0xBB && mark[2] == 0xBF;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static CadSheetCell Cell(string field)
        {
            if (field.Length == 0)
            {
                return CadSheetCell.Empty;
            }

            return
                double.TryParse(
                    field.Trim(),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out double number
                )
                && !double.IsInfinity(number)
                && !double.IsNaN(number)
                ? new CadSheetCell(CadSheetCellKind.Number, field, number)
                : new CadSheetCell(CadSheetCellKind.Text, field, 0.0);
        }

        private static string Decode(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (ArgumentException)
            {
                return Encoding.Default.GetString(bytes);
            }
        }
    }
}
