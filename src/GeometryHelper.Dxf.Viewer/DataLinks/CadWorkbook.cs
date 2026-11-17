using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// An Excel workbook of the .xlsx kind (Office Open XML), read and written by the presenter's own small code, with no
    /// Excel and no other package: its sheets and named ranges, the values the file holds (a formula gives the value last
    /// worked out and kept with it, no formula is evaluated), shared and inline texts, numbers shown as their formats show
    /// them, dates, and a cell's alignment, fill and font colour. A workbook of the older .xls kind is not read.
    /// </summary>
    public sealed class CadWorkbook
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace Pack = "http://schemas.openxmlformats.org/package/2006/relationships";

        private readonly byte[] _bytes;
        private readonly Dictionary<string, string> _parts = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _names = new List<string>();
        private readonly Dictionary<string, string> _ranges = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );
        private bool _date1904;
        private List<string> _strings;
        private List<Look> _looks;

        private CadWorkbook(byte[] bytes)
        {
            _bytes = bytes;
        }

        /// <summary>Gets the names of the sheets, in the order of the workbook's tabs.</summary>
        public IReadOnlyList<string> SheetNames => _names;

        /// <summary>
        /// Gets the named ranges of the workbook, each name with the cells it names as the workbook writes them,
        /// <c>Sheet1!$A$1:$C$5</c>; a name of a formula or of a sheet's own scope is not among them.
        /// </summary>
        public IReadOnlyDictionary<string, string> NamedRanges => _ranges;

        /// <summary>Opens a workbook.</summary>
        /// <param name="path">The .xlsx file.</param>
        /// <returns>The workbook.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="InvalidDataException">Thrown when the file is no .xlsx workbook: not a zip, or without the parts of one.</exception>
        public static CadWorkbook Open(string path)
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

            var book = new CadWorkbook(bytes);
            book.Load();
            return book;
        }

        /// <summary>
        /// Reads a sheet.
        /// </summary>
        /// <param name="name">The sheet's name, letters of either case alike.</param>
        /// <returns>The sheet, its first cell the workbook's A1.</returns>
        /// <exception cref="ArgumentException">Thrown when the workbook has no such sheet.</exception>
        /// <exception cref="InvalidDataException">Thrown when the sheet's part is missing or is no XML.</exception>
        public CadSheet ReadSheet(string name)
        {
            string sheet = _names.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

            if (sheet == null || !_parts.TryGetValue(sheet, out string part))
            {
                throw new ArgumentException("The workbook has no sheet \"" + name + "\".", nameof(name));
            }

            using (var archive = new ZipArchive(new MemoryStream(_bytes), ZipArchiveMode.Read))
            {
                EnsureStyles(archive);
                XDocument document = Part(archive, part);
                var cells = new CadSheet(sheet);

                foreach (XElement cell in document.Descendants(Main + "c"))
                {
                    if (!TryRef((string)cell.Attribute("r"), out int row, out int column))
                    {
                        continue;
                    }

                    CadSheetCell value = ValueOf(cell);

                    if (value != null)
                    {
                        cells.Set(row, column, value);
                    }
                }

                return cells;
            }
        }

        /// <summary>
        /// Writes a new workbook of one sheet, as a data extraction writes its .xlsx file: texts as inline strings, numbers and
        /// booleans as values, a date as its number under the date format of the workbook's own styles.
        /// </summary>
        /// <param name="path">The file; one there is written over.</param>
        /// <param name="sheetName">The sheet's name, up to 31 letters without <c>[ ] : * ? / \</c>.</param>
        /// <param name="sheet">The cells.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path or the sheet is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the sheet's name is not one a workbook takes.</exception>
        public static void Write(string path, string sheetName, CadSheet sheet)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            if (
                string.IsNullOrWhiteSpace(sheetName)
                || sheetName.Length > 31
                || sheetName.IndexOfAny(new[] { '[', ']', ':', '*', '?', '/', '\\' }) >= 0
            )
            {
                throw new ArgumentException(
                    "A sheet's name is up to 31 letters without [ ] : * ? / \\",
                    nameof(sheetName)
                );
            }

            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Put(
                    archive,
                    "[Content_Types].xml",
                    new XDocument(
                        new XElement(
                            XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types") + "Types",
                            Content(
                                "Default",
                                "Extension",
                                "rels",
                                "application/vnd.openxmlformats-package.relationships+xml"
                            ),
                            Content("Default", "Extension", "xml", "application/xml"),
                            Content(
                                "Override",
                                "PartName",
                                "/xl/workbook.xml",
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"
                            ),
                            Content(
                                "Override",
                                "PartName",
                                "/xl/worksheets/sheet1.xml",
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"
                            ),
                            Content(
                                "Override",
                                "PartName",
                                "/xl/styles.xml",
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"
                            )
                        )
                    )
                );
                Put(
                    archive,
                    "_rels/.rels",
                    new XDocument(
                        new XElement(
                            Pack + "Relationships",
                            new XElement(
                                Pack + "Relationship",
                                new XAttribute("Id", "rId1"),
                                new XAttribute(
                                    "Type",
                                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"
                                ),
                                new XAttribute("Target", "xl/workbook.xml")
                            )
                        )
                    )
                );
                Put(
                    archive,
                    "xl/workbook.xml",
                    new XDocument(
                        new XElement(
                            Main + "workbook",
                            new XAttribute(XNamespace.Xmlns + "r", Rel.NamespaceName),
                            new XElement(
                                Main + "sheets",
                                new XElement(
                                    Main + "sheet",
                                    new XAttribute("name", sheetName),
                                    new XAttribute("sheetId", "1"),
                                    new XAttribute(Rel + "id", "rId1")
                                )
                            )
                        )
                    )
                );
                Put(
                    archive,
                    "xl/_rels/workbook.xml.rels",
                    new XDocument(
                        new XElement(
                            Pack + "Relationships",
                            new XElement(
                                Pack + "Relationship",
                                new XAttribute("Id", "rId1"),
                                new XAttribute(
                                    "Type",
                                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"
                                ),
                                new XAttribute("Target", "worksheets/sheet1.xml")
                            ),
                            new XElement(
                                Pack + "Relationship",
                                new XAttribute("Id", "rId2"),
                                new XAttribute(
                                    "Type",
                                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"
                                ),
                                new XAttribute("Target", "styles.xml")
                            )
                        )
                    )
                );
                Put(
                    archive,
                    "xl/styles.xml",
                    new XDocument(
                        new XElement(
                            Main + "styleSheet",
                            new XElement(
                                Main + "fonts",
                                new XAttribute("count", "1"),
                                new XElement(
                                    Main + "font",
                                    new XElement(Main + "sz", new XAttribute("val", "11")),
                                    new XElement(Main + "name", new XAttribute("val", "Calibri"))
                                )
                            ),
                            new XElement(
                                Main + "fills",
                                new XAttribute("count", "2"),
                                new XElement(
                                    Main + "fill",
                                    new XElement(Main + "patternFill", new XAttribute("patternType", "none"))
                                ),
                                new XElement(
                                    Main + "fill",
                                    new XElement(Main + "patternFill", new XAttribute("patternType", "gray125"))
                                )
                            ),
                            new XElement(
                                Main + "borders",
                                new XAttribute("count", "1"),
                                new XElement(
                                    Main + "border",
                                    new XElement(Main + "left"),
                                    new XElement(Main + "right"),
                                    new XElement(Main + "top"),
                                    new XElement(Main + "bottom"),
                                    new XElement(Main + "diagonal")
                                )
                            ),
                            new XElement(
                                Main + "cellStyleXfs",
                                new XAttribute("count", "1"),
                                new XElement(
                                    Main + "xf",
                                    new XAttribute("numFmtId", "0"),
                                    new XAttribute("fontId", "0"),
                                    new XAttribute("fillId", "0"),
                                    new XAttribute("borderId", "0")
                                )
                            ),
                            new XElement(
                                Main + "cellXfs",
                                new XAttribute("count", "2"),
                                new XElement(
                                    Main + "xf",
                                    new XAttribute("numFmtId", "0"),
                                    new XAttribute("fontId", "0"),
                                    new XAttribute("fillId", "0"),
                                    new XAttribute("borderId", "0"),
                                    new XAttribute("xfId", "0")
                                ),
                                new XElement(
                                    Main + "xf",
                                    new XAttribute("numFmtId", "22"),
                                    new XAttribute("fontId", "0"),
                                    new XAttribute("fillId", "0"),
                                    new XAttribute("borderId", "0"),
                                    new XAttribute("xfId", "0"),
                                    new XAttribute("applyNumberFormat", "1")
                                )
                            )
                        )
                    )
                );

                var data = new XElement(Main + "sheetData");

                for (int r = 0; r < sheet.RowCount; r++)
                {
                    var row = new XElement(
                        Main + "row",
                        new XAttribute("r", (r + 1).ToString(CultureInfo.InvariantCulture))
                    );

                    for (int c = 0; c < sheet.ColumnCount; c++)
                    {
                        XElement cell = CellOf(sheet[r, c], r, c);

                        if (cell != null)
                        {
                            row.Add(cell);
                        }
                    }

                    data.Add(row);
                }

                Put(archive, "xl/worksheets/sheet1.xml", new XDocument(new XElement(Main + "worksheet", data)));
            }
        }

        /// <summary>
        /// Writes cells into a sheet of a workbook, as AutoCAD writes a data link's cells back to its source: each cell of
        /// <paramref name="values"/> put at its place from the first cell given, a text as an inline string and a number as a
        /// value; every other part of the workbook, its styles, its other sheets and its formulas, kept as it was. A cell that
        /// holds a formula is left as it is.
        /// </summary>
        /// <param name="path">The workbook's file.</param>
        /// <param name="sheetName">The sheet.</param>
        /// <param name="topRow">The row of the first cell, from nought.</param>
        /// <param name="leftColumn">The column of the first cell, from nought.</param>
        /// <param name="values">The cells.</param>
        /// <returns>The number of cells written.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path, the sheet's name or the values are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the workbook has no such sheet.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written, as one open in Excel cannot.</exception>
        public static int WriteBack(string path, string sheetName, int topRow, int leftColumn, CadSheet values)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (sheetName == null)
            {
                throw new ArgumentNullException(nameof(sheetName));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            CadWorkbook book = Open(path);
            string sheet = book._names.FirstOrDefault(n =>
                string.Equals(n, sheetName, StringComparison.OrdinalIgnoreCase)
            );

            if (sheet == null || !book._parts.TryGetValue(sheet, out string part))
            {
                throw new ArgumentException("The workbook has no sheet \"" + sheetName + "\".", nameof(sheetName));
            }

            int written = 0;

            using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Update))
            {
                XDocument document = Part(archive, part);
                XElement data = document.Root?.Element(Main + "sheetData");

                if (data == null)
                {
                    data = new XElement(Main + "sheetData");
                    document.Root?.Add(data);
                }

                for (int r = 0; r < values.RowCount; r++)
                {
                    for (int c = 0; c < values.ColumnCount; c++)
                    {
                        CadSheetCell value = values[r, c];
                        XElement cell = FindCell(data, topRow + r, leftColumn + c, true);

                        if (cell.Element(Main + "f") != null)
                        {
                            continue;
                        }

                        string reference =
                            CadSheetRange.ColumnName(leftColumn + c)
                            + (topRow + r + 1).ToString(CultureInfo.InvariantCulture);
                        XAttribute style = cell.Attribute("s");
                        XElement fresh =
                            value.Kind == CadSheetCellKind.Empty
                                ? new XElement(Main + "c")
                                : CellOf(value, topRow + r, leftColumn + c) ?? new XElement(Main + "c");
                        cell.RemoveAll();
                        cell.SetAttributeValue("r", reference);

                        if (style != null)
                        {
                            cell.SetAttributeValue("s", style.Value);
                        }

                        foreach (XAttribute attribute in fresh.Attributes().Where(a => a.Name != "r" && a.Name != "s"))
                        {
                            cell.SetAttributeValue(attribute.Name, attribute.Value);
                        }

                        cell.Add(fresh.Elements());
                        written++;
                    }
                }

                archive.GetEntry(part)?.Delete();
                Put(archive, part, document);
            }

            return written;
        }

        private void Load()
        {
            try
            {
                using (var archive = new ZipArchive(new MemoryStream(_bytes), ZipArchiveMode.Read))
                {
                    XDocument book = Part(archive, "xl/workbook.xml");
                    XDocument rels = Part(archive, "xl/_rels/workbook.xml.rels");
                    var targets = rels.Descendants(Pack + "Relationship")
                        .Where(r => r.Attribute("Id") != null && r.Attribute("Target") != null)
                        .ToDictionary(r => (string)r.Attribute("Id"), r => (string)r.Attribute("Target"));
                    _date1904 =
                        string.Equals(
                            (string)book.Descendants(Main + "workbookPr").FirstOrDefault()?.Attribute("date1904"),
                            "1",
                            StringComparison.Ordinal
                        )
                        || string.Equals(
                            (string)book.Descendants(Main + "workbookPr").FirstOrDefault()?.Attribute("date1904"),
                            "true",
                            StringComparison.OrdinalIgnoreCase
                        );

                    foreach (XElement sheet in book.Descendants(Main + "sheet"))
                    {
                        string name = (string)sheet.Attribute("name");
                        string id = (string)sheet.Attribute(Rel + "id");

                        if (
                            name != null
                            && id != null
                            && targets.TryGetValue(id, out string target)
                            && !_parts.ContainsKey(name)
                        )
                        {
                            _names.Add(name);
                            _parts[name] = target.StartsWith("/", StringComparison.Ordinal)
                                ? target.Substring(1)
                                : "xl/" + target;
                        }
                    }

                    foreach (XElement defined in book.Descendants(Main + "definedName"))
                    {
                        string name = (string)defined.Attribute("name");
                        string reference = defined.Value.Trim();

                        if (
                            name != null
                            && defined.Attribute("localSheetId") == null
                            && !name.StartsWith("_xlnm", StringComparison.OrdinalIgnoreCase)
                            && reference.IndexOf('!') > 0
                            && !_ranges.ContainsKey(name)
                        )
                        {
                            _ranges[name] = reference;
                        }
                    }
                }
            }
            catch (Exception exception)
                when (exception is InvalidDataException
                    || exception is System.Xml.XmlException
                    || exception is NullReferenceException
                )
            {
                throw new InvalidDataException("The file is not an Excel workbook of the .xlsx kind.", exception);
            }

            if (_names.Count == 0)
            {
                throw new InvalidDataException("The file is not an Excel workbook of the .xlsx kind: it has no sheet.");
            }
        }

        private static XDocument Part(ZipArchive archive, string name)
        {
            ZipArchiveEntry entry = archive.GetEntry(name);

            if (entry == null)
            {
                throw new InvalidDataException("The workbook has no part " + name + ".");
            }

            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static void Put(ZipArchive archive, string name, XDocument document)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);

            using (Stream stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                document.Save(writer, SaveOptions.DisableFormatting);
            }
        }

        private static XElement Content(string kind, string key, string value, string type) =>
            new XElement(
                XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types") + kind,
                new XAttribute(key, value),
                new XAttribute("ContentType", type)
            );

        /// <summary>The element of a cell for the sheet it is written into; null for an empty cell.</summary>
        private static XElement CellOf(CadSheetCell cell, int row, int column)
        {
            if (cell.Kind == CadSheetCellKind.Empty)
            {
                return null;
            }

            string reference = CadSheetRange.ColumnName(column) + (row + 1).ToString(CultureInfo.InvariantCulture);
            var element = new XElement(Main + "c", new XAttribute("r", reference));

            switch (cell.Kind)
            {
                case CadSheetCellKind.Number:
                    element.Add(new XElement(Main + "v", cell.Number.ToString("R", CultureInfo.InvariantCulture)));
                    break;
                case CadSheetCellKind.Date:
                    element.Add(
                        new XAttribute("s", "1"),
                        new XElement(Main + "v", cell.Number.ToString("R", CultureInfo.InvariantCulture))
                    );
                    break;
                case CadSheetCellKind.Boolean:
                    element.Add(new XAttribute("t", "b"), new XElement(Main + "v", cell.Number != 0.0 ? "1" : "0"));
                    break;
                default:
                    element.Add(
                        new XAttribute("t", "inlineStr"),
                        new XElement(
                            Main + "is",
                            new XElement(Main + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), cell.Text)
                        )
                    );
                    break;
            }

            return element;
        }

        /// <summary>The cell element of a place, the row and the cell made in their order where the sheet has none; null where absent and not asked for.</summary>
        private static XElement FindCell(XElement data, int row, int column, bool make)
        {
            string number = (row + 1).ToString(CultureInfo.InvariantCulture);
            XElement line = data.Elements(Main + "row").FirstOrDefault(r => (string)r.Attribute("r") == number);

            if (line == null)
            {
                if (!make)
                {
                    return null;
                }

                line = new XElement(Main + "row", new XAttribute("r", number));
                XElement after = data.Elements(Main + "row").LastOrDefault(r => RowOf(r) < row + 1);

                if (after == null)
                {
                    data.AddFirst(line);
                }
                else
                {
                    after.AddAfterSelf(line);
                }
            }

            foreach (XElement cell in line.Elements(Main + "c"))
            {
                if (TryRef((string)cell.Attribute("r"), out _, out int at) && at == column)
                {
                    return cell;
                }
            }

            if (!make)
            {
                return null;
            }

            XElement made = new XElement(Main + "c", new XAttribute("r", CadSheetRange.ColumnName(column) + number));
            XElement before = line.Elements(Main + "c")
                .LastOrDefault(c => TryRef((string)c.Attribute("r"), out _, out int at) && at < column);

            if (before == null)
            {
                line.AddFirst(made);
            }
            else
            {
                before.AddAfterSelf(made);
            }

            return made;
        }

        private static int RowOf(XElement row) =>
            int.TryParse((string)row.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                ? number
                : 0;

        /// <summary>A cell's place, <c>B3</c> as row 2 and column 1.</summary>
        private static bool TryRef(string reference, out int row, out int column)
        {
            row = 0;
            column = 0;
            int i = 0;

            while (reference != null && i < reference.Length && char.IsLetter(reference[i]))
            {
                column = column * 26 + (char.ToUpperInvariant(reference[i]) - 'A' + 1);
                i++;
            }

            if (
                reference == null
                || i == 0
                || i == reference.Length
                || !int.TryParse(reference.Substring(i), NumberStyles.None, CultureInfo.InvariantCulture, out row)
                || row < 1
            )
            {
                return false;
            }

            column--;
            row--;
            return true;
        }

        private void EnsureStyles(ZipArchive archive)
        {
            if (_strings != null)
            {
                return;
            }

            _strings = new List<string>();
            _looks = new List<Look>();
            ZipArchiveEntry shared = archive.GetEntry("xl/sharedStrings.xml");

            if (shared != null)
            {
                using (Stream stream = shared.Open())
                {
                    foreach (XElement item in XDocument.Load(stream).Descendants(Main + "si"))
                    {
                        _strings.Add(
                            string.Concat(
                                item.Descendants(Main + "t")
                                    .Where(t => t.Parent?.Name != Main + "rPh")
                                    .Select(t => t.Value)
                            )
                        );
                    }
                }
            }

            ZipArchiveEntry styles = archive.GetEntry("xl/styles.xml");

            if (styles == null)
            {
                return;
            }

            XDocument sheet;

            using (Stream stream = styles.Open())
            {
                sheet = XDocument.Load(stream);
            }

            var codes = new Dictionary<int, string>();

            foreach (XElement format in sheet.Descendants(Main + "numFmt"))
            {
                if (
                    int.TryParse(
                        (string)format.Attribute("numFmtId"),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int id
                    )
                )
                {
                    codes[id] = (string)format.Attribute("formatCode");
                }
            }

            List<XElement> fonts = sheet.Root?.Element(Main + "fonts")?.Elements(Main + "font").ToList()
                ?? new List<XElement>();
            List<XElement> fills = sheet.Root?.Element(Main + "fills")?.Elements(Main + "fill").ToList()
                ?? new List<XElement>();

            foreach (
                XElement xf in sheet.Root?.Element(Main + "cellXfs")?.Elements(Main + "xf")
                    ?? Enumerable.Empty<XElement>()
            )
            {
                int.TryParse(
                    (string)xf.Attribute("numFmtId"),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int formatId
                );
                int.TryParse(
                    (string)xf.Attribute("fontId"),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int fontId
                );
                int.TryParse(
                    (string)xf.Attribute("fillId"),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int fillId
                );
                string code = codes.TryGetValue(formatId, out string custom)
                    ? custom
                    : CadNumberFormat.CodeOf(formatId);
                string horizontal = (string)xf.Element(Main + "alignment")?.Attribute("horizontal");
                XElement fill =
                    fillId >= 2 && fillId < fills.Count ? fills[fillId].Element(Main + "patternFill") : null;
                bool solid = string.Equals(
                    (string)fill?.Attribute("patternType"),
                    "solid",
                    StringComparison.OrdinalIgnoreCase
                );
                _looks.Add(
                    new Look
                    {
                        Code = code,
                        IsDate = CadNumberFormat.IsDate(code),
                        Alignment =
                            horizontal == "left" ? 1
                            : horizontal == "center" || horizontal == "centerContinuous" ? 2
                            : horizontal == "right" ? 3
                            : 0,
                        Fill = solid ? Rgb((string)fill.Element(Main + "fgColor")?.Attribute("rgb")) : null,
                        FontColor =
                            fontId < fonts.Count
                                ? Rgb((string)fonts[fontId].Element(Main + "color")?.Attribute("rgb"))
                                : null,
                    }
                );
            }
        }

        private static int? Rgb(string argb)
        {
            return
                argb != null
                && argb.Length >= 6
                && int.TryParse(
                    argb.Substring(argb.Length - 6),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out int rgb
                )
                ? rgb
                : (int?)null;
        }

        /// <summary>The value of a cell element with the look its style gives; null for a cell with no value and no look.</summary>
        private CadSheetCell ValueOf(XElement element)
        {
            string type = (string)element.Attribute("t");
            string raw = (string)element.Element(Main + "v");
            Look look =
                int.TryParse(
                    (string)element.Attribute("s"),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int index
                )
                && index < _looks.Count
                    ? _looks[index]
                    : null;
            CadSheetCell cell;

            if (type == "inlineStr")
            {
                cell = CadSheetCell.OfText(
                    string.Concat(
                        element.Element(Main + "is")?.Descendants(Main + "t").Select(t => t.Value)
                            ?? Enumerable.Empty<string>()
                    )
                );
            }
            else if (raw == null)
            {
                cell = CadSheetCell.Empty;
            }
            else if (type == "s")
            {
                cell =
                    int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int at)
                    && at < _strings.Count
                        ? CadSheetCell.OfText(_strings[at])
                        : CadSheetCell.Empty;
            }
            else if (type == "str" || type == "e")
            {
                cell = CadSheetCell.OfText(raw);
            }
            else if (type == "b")
            {
                cell = new CadSheetCell(
                    CadSheetCellKind.Boolean,
                    raw == "1" ? "TRUE" : "FALSE",
                    raw == "1" ? 1.0 : 0.0
                );
            }
            else if (
                type == "d"
                && DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime moment)
            )
            {
                double days = (moment - new DateTime(1899, 12, 30)).TotalDays;
                cell = new CadSheetCell(CadSheetCellKind.Date, CadNumberFormat.Format(days, "m/d/yyyy", false), days);
            }
            else if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                bool date = look != null && look.IsDate;
                cell = new CadSheetCell(
                    date ? CadSheetCellKind.Date : CadSheetCellKind.Number,
                    CadNumberFormat.Format(number, look?.Code, _date1904),
                    number
                );
            }
            else
            {
                cell = CadSheetCell.OfText(raw);
            }

            if (look == null || (look.Alignment == 0 && look.Fill == null && look.FontColor == null))
            {
                return cell.Kind == CadSheetCellKind.Empty ? null : cell;
            }

            return new CadSheetCell(cell.Kind, cell.Text, cell.Number)
            {
                Alignment = look.Alignment,
                Fill = look.Fill,
                FontColor = look.FontColor,
            };
        }

        /// <summary>What a cell style of the workbook says: the number format, its alignment, fill and font colour.</summary>
        private sealed class Look
        {
            internal string Code;
            internal bool IsDate;
            internal int Alignment;
            internal int? Fill;
            internal int? FontColor;
        }
    }
}
