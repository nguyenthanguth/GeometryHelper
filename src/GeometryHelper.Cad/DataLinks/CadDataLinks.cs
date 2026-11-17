using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>Which cells of a workbook a data link takes, as the New Excel Data Link dialog offers them.</summary>
    public enum CadDataLinkRangeKind
    {
        /// <summary>Every cell of a sheet.</summary>
        EntireSheet = 0,

        /// <summary>The cells a name of the workbook stands for.</summary>
        NamedRange = 1,

        /// <summary>A block of cells of a sheet, <c>A1:D10</c>.</summary>
        CellRange = 2,
    }

    /// <summary>How the file of a data link is named in its connection string, as the data link dialog offers it.</summary>
    public enum CadDataLinkPathType
    {
        /// <summary>The whole path of the file.</summary>
        FullPath = 0,

        /// <summary>The path from the drawing's folder; only for a drawing saved.</summary>
        RelativePath = 1,

        /// <summary>The file's name alone, looked for in the drawing's folder.</summary>
        NoPath = 2,
    }

    /// <summary>
    /// What the New Excel Data Link and Modify Excel Link dialogs collect: the link's name, the file, how its path is kept,
    /// which cells it takes, and the options of "More Options", whether the table takes the workbook's formatting and whether
    /// the link may write the table back to the file. A test fills it with no window.
    /// </summary>
    public sealed class CadDataLinkSettings
    {
        /// <summary>Gets or sets the name of the link, as the Data Link Manager lists it.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the file, an .xlsx workbook or a .csv file, as a full path.</summary>
        public string File { get; set; } = string.Empty;

        /// <summary>Gets or sets how the file's path is written in the connection string.</summary>
        public CadDataLinkPathType PathType { get; set; }

        /// <summary>Gets or sets which cells the link takes.</summary>
        public CadDataLinkRangeKind RangeKind { get; set; }

        /// <summary>Gets or sets the sheet of the entire sheet or of the range; for a CSV file empty or its own name.</summary>
        public string Sheet { get; set; } = string.Empty;

        /// <summary>Gets or sets the name of the named range.</summary>
        public string NamedRange { get; set; } = string.Empty;

        /// <summary>Gets or sets the cells of the range, <c>A1:D10</c>, <c>A:A</c> or <c>A:D</c>.</summary>
        public string Range { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the table takes the workbook's alignment, fill and text colour; true as the dialog opens.</summary>
        public bool UseExcelFormatting { get; set; } = true;

        /// <summary>Gets or sets whether the link may write the table's cells back to the file; true as the dialog opens.</summary>
        public bool AllowWritingToSource { get; set; } = true;

        /// <summary>
        /// Gives the connection string the settings make, the file as its path type says and then the sheet, the named range or
        /// the sheet and the range.
        /// </summary>
        /// <param name="drawingFolder">The drawing's folder; null or empty for a drawing not saved, whose relative path is a full one.</param>
        /// <returns>The connection string.</returns>
        public string ConnectionString(string drawingFolder)
        {
            string file = File ?? string.Empty;

            if (PathType == CadDataLinkPathType.NoPath)
            {
                file = Path.GetFileName(file);
            }
            else if (
                PathType == CadDataLinkPathType.RelativePath
                && !string.IsNullOrEmpty(drawingFolder)
                && Path.IsPathRooted(file)
            )
            {
                file = CadDataLinks.RelativePath(drawingFolder, file);
            }

            switch (RangeKind)
            {
                case CadDataLinkRangeKind.NamedRange:
                    return CadDataSource.Compose(file, NamedRange, null);
                case CadDataLinkRangeKind.CellRange:
                    return CadDataSource.Compose(file, EffectiveSheet, Range);
                default:
                    return CadDataSource.Compose(file, EffectiveSheet, null);
            }
        }

        /// <summary>The sheet the connection string names: the one given, else the file's own name for a CSV file, else none (the workbook's first sheet).</summary>
        internal string EffectiveSheet =>
            !string.IsNullOrWhiteSpace(Sheet) ? Sheet.Trim()
            : (File ?? string.Empty).EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(File)
            : string.Empty;
    }

    /// <summary>A data link of the drawing as the Data Link Manager lists it.</summary>
    public sealed class CadDataLinkInfo
    {
        internal CadDataLinkInfo(DxfDataLink link)
        {
            Link = link;
            Name = link.Name ?? string.Empty;
            AdapterId = link.DataAdapterId;
            ConnectionString = DxfReader.Carets(link.ConnectionString);
            ToolTip = DxfReader.Carets(link.ToolTip);
            IsExtraction = string.Equals(AdapterId, CadDataLinks.ExtractionAdapter, StringComparison.Ordinal);
            UpdateOption = link.UpdateOption;
            Tables = link.GetTargets().Count;
            link.GetUpdateStatus(out DxfUpdateDirection direction, out DateTime time, out string message);
            LastUpdate = time == DateTime.MinValue ? (DateTime?)null : time;
            Message = message;
        }

        internal DxfDataLink Link { get; }

        /// <summary>Gets the name of the link.</summary>
        public string Name { get; }

        /// <summary>Gets the data adapter: <c>AcExcel</c> for a spreadsheet, the data extraction's for an extraction.</summary>
        public string AdapterId { get; }

        /// <summary>Gets where the data is.</summary>
        public string ConnectionString { get; }

        /// <summary>Gets the tooltip the linked cells show.</summary>
        public string ToolTip { get; }

        /// <summary>Gets whether the link is a data extraction's.</summary>
        public bool IsExtraction { get; }

        /// <summary>Gets the update options, the flags of AutoCAD's <c>UpdateOption</c> as one number.</summary>
        public int UpdateOption { get; }

        /// <summary>Gets how many tables the link fills; a link that fills one is in use and is not deleted.</summary>
        public int Tables { get; }

        /// <summary>Gets when the link was last updated; null for one never updated.</summary>
        public DateTime? LastUpdate { get; }

        /// <summary>Gets the message of the last update, empty where it went well.</summary>
        public string Message { get; }

        /// <summary>Gets the details the manager shows under the link: the file and what of it the link takes.</summary>
        public string Details
        {
            get
            {
                if (IsExtraction)
                {
                    return "Data extraction: " + ConnectionString;
                }

                CadDataSource.TryParse(ConnectionString, out string file, out _, out _);
                string how = CadDataSource.Describe(ConnectionString);
                return how.Length == 0 ? file : file + "\n" + how;
            }
        }

        /// <summary>Gives the link as its name.</summary>
        /// <returns>The name.</returns>
        public override string ToString() => Name;
    }

    /// <summary>What an update or a write of data links did: how many links went well and how many did not, and why.</summary>
    public sealed class CadDataLinkRun
    {
        internal CadDataLinkRun(
            CadEditResult edit,
            int succeeded,
            int failed,
            int refused,
            IReadOnlyList<string> messages
        )
        {
            Edit = edit;
            Succeeded = succeeded;
            Failed = failed;
            NotAllowed = refused;
            Messages = messages;
        }

        /// <summary>Gets the edit that put the data in the drawing; refused where a table could not be changed.</summary>
        public CadEditResult Edit { get; }

        /// <summary>Gets how many links went well.</summary>
        public int Succeeded { get; }

        /// <summary>Gets how many links could not be updated or written, their source being missing or unreadable.</summary>
        public int Failed { get; }

        /// <summary>Gets how many links do not allow their data to be written out (a write only).</summary>
        public int NotAllowed { get; }

        /// <summary>Gets what went wrong, one line for each link that did not go well.</summary>
        public IReadOnlyList<string> Messages { get; }
    }

    /// <summary>
    /// The data links of a drawing, as AutoCAD 2021's DATALINK and DATALINKUPDATE and the table's data link menus work them:
    /// a link to a spreadsheet (an .xlsx workbook or a .csv file) made, changed, renamed and deleted, a table's cells linked
    /// to it, filled from its source and written back to it. A link is a DATALINK object of the drawing's ACAD_DATALINK
    /// dictionary, written as AutoCAD 2021 writes the link of its own Excel adapter (<c>AcExcel</c>): its adapter, tooltip and
    /// connection string, its options and update options, when it was last updated, the tables it fills and its custom data
    /// (the update options, the connection string and the source file's date), so that AutoCAD opens the drawing and reads the
    /// link as its own. A drawing before AutoCAD 2007 (AC1021) has no data links.
    /// </summary>
    public static class CadDataLinks
    {
        /// <summary>The data adapter of a data extraction's link, as AutoCAD names it.</summary>
        public const string ExtractionAdapter = "Autodesk.AutoCAD.DataExtraction.DxDataLinkAdapter";

        /// <summary>The flag of AutoCAD's <c>UpdateOption</c> that leaves the workbook's formatting out of the table.</summary>
        public const int SkipFormat = 0x20000;

        /// <summary>The flag of AutoCAD's <c>UpdateOption</c> that lets the link write the table back to its source.</summary>
        public const int AllowSourceUpdate = 0x100000;

        private const int ExcelDefaults = 1 | 0x40000 | 0x80000;

        /// <summary>The path from a folder to a file, <c>..\data\parts.xlsx</c>; the file's full path where it is on another drive.</summary>
        /// <param name="folder">The folder.</param>
        /// <param name="file">The file's full path.</param>
        /// <returns>The relative path.</returns>
        public static string RelativePath(string folder, string file)
        {
            try
            {
                string from = Path.GetFullPath(folder);
                from = from.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? from
                    : from + Path.DirectorySeparatorChar;
                Uri relative = new Uri(from).MakeRelativeUri(new Uri(Path.GetFullPath(file)));

                return relative.IsAbsoluteUri
                    ? file
                    : Uri.UnescapeDataString(relative.ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is UriFormatException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                return file;
            }
        }

        /// <summary>The folder of the drawing's file; null for a drawing not saved.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The folder, or null.</returns>
        public static string FolderOf(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return document.HasFile && !string.IsNullOrEmpty(document.FileName)
                ? Path.GetDirectoryName(Path.GetFullPath(document.FileName))
                : null;
        }

        /// <summary>Whether the drawing can hold data links: a drawing of AutoCAD 2007 (AC1021) or later with a dictionary of objects.</summary>
        /// <param name="document">The document.</param>
        /// <returns>True when it can.</returns>
        public static bool CanHold(CadDocument document)
        {
            DxfDatabase database = document?.Database;
            return database != null
                && database.Version != null
                && string.CompareOrdinal(database.Version, "AC1021") >= 0;
        }

        /// <summary>The data links of the drawing, by name.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The links; none where the drawing has none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static IReadOnlyList<CadDataLinkInfo> List(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var found = new List<CadDataLinkInfo>();
            DxfDBDictionary dictionary = document.Database.DataLinkDictionary;

            if (dictionary != null)
            {
                foreach (DxfDBDictionaryEntry entry in dictionary)
                {
                    if (entry.Value is DxfDataLink link && !link.IsErased)
                    {
                        found.Add(new CadDataLinkInfo(link));
                    }
                }
            }

            found.Sort(
                (a, b) =>
                {
                    int by = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    return by != 0 ? by : string.CompareOrdinal(a.Name, b.Name);
                }
            );
            return found;
        }

        /// <summary>Finds a link by its name, letters of either case alike.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The name.</param>
        /// <returns>The link, or null.</returns>
        public static DxfDataLink Find(CadDocument document, string name)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return List(document)
                .FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                ?.Link;
        }

        /// <summary>
        /// Reads what a data link's settings link, for the dialog's preview and for the checks of <see cref="Create"/>.
        /// </summary>
        /// <param name="document">The document, whose folder a relative path is of.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>The cells, from the first of the range.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the settings are null.</exception>
        /// <exception cref="CadDataSourceException">Thrown when the source cannot be read.</exception>
        public static CadSheet Preview(CadDocument document, CadDataLinkSettings settings)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return CadDataSource.Read(settings.ConnectionString(FolderOf(document)), FolderOf(document));
        }

        /// <summary>
        /// Makes a data link, as the New Excel Data Link dialog does: one undo step. The name has to be new ("Data link name in
        /// use."), the source has to read, and the drawing has to be of AutoCAD 2007 or later.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the settings are null.</exception>
        public static CadEditResult Create(CadDocument document, CadDataLinkSettings settings)
        {
            CadEditResult refused = Check(document, settings, null);

            if (refused != null)
            {
                return refused;
            }

            string folder = FolderOf(document);
            string connection = settings.ConnectionString(folder);
            document.EnsureNamedObjectsDictionary();
            return document.ApplyEdit(
                "DATALINK",
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.TableChanged, null, "Data link", null, settings.Name.Trim()),
                },
                s => Make(s.Database, settings.Name.Trim(), ExcelValues(settings, connection, folder))
            );
        }

        /// <summary>
        /// Changes a data link, as Modify Excel Link does: its file, cells and options written anew, one undo step. The tables it
        /// fills are not changed until the link is updated.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The name of the link.</param>
        /// <param name="settings">The settings; the name in them is the link's new name.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the name or the settings are null.</exception>
        public static CadEditResult Edit(CadDocument document, string name, CadDataLinkSettings settings)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            DxfDataLink link = Find(document, name);

            if (link == null)
            {
                return CadEditResult.Refuse("Data link " + name + " does not exist.");
            }

            if (string.Equals(link.DataAdapterId, ExtractionAdapter, StringComparison.Ordinal))
            {
                return CadEditResult.Refuse("A data extraction link is changed in the Data Extraction wizard.");
            }

            CadEditResult refused = Check(document, settings, link);

            if (refused != null)
            {
                return refused;
            }

            string folder = FolderOf(document);
            string connection = settings.ConnectionString(folder);
            string handle = link.Handle;
            return document.ApplyEdit(
                "DATALINK",
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.TableChanged, null, "Data link", name, settings.Name.Trim()),
                },
                s =>
                {
                    DxfDataLink own = Linked(s.Database, handle);
                    Rewrite(own, ExcelValues(settings, connection, folder));

                    if (!string.Equals(own.Name, settings.Name.Trim(), StringComparison.Ordinal))
                    {
                        own.Name = settings.Name.Trim();
                    }
                }
            );
        }

        /// <summary>Renames a data link, one undo step.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The link's name.</param>
        /// <param name="newName">The new name, not another link's.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static CadEditResult Rename(CadDocument document, string name, string newName)
        {
            if (newName == null)
            {
                throw new ArgumentNullException(nameof(newName));
            }

            DxfDataLink link = Find(document, name ?? throw new ArgumentNullException(nameof(name)));

            if (link == null)
            {
                return CadEditResult.Refuse("Data link " + name + " does not exist.");
            }

            string target = newName.Trim();

            if (
                target.Length == 0
                || target.IndexOfAny(new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`' }) >= 0
            )
            {
                return CadEditResult.Refuse("Invalid data link name.");
            }

            if (!string.Equals(target, name, StringComparison.OrdinalIgnoreCase) && Find(document, target) != null)
            {
                return CadEditResult.Refuse("Data link name in use.");
            }

            string handle = link.Handle;
            return document.ApplyEdit(
                "DATALINK",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Data link", name, target) },
                s => Linked(s.Database, handle).Name = target
            );
        }

        /// <summary>
        /// Deletes a data link, one undo step. A link that fills a table is in use and is refused, "The data link is being used
        /// and cannot be deleted."; detach it from the table first.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The link's name.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static CadEditResult Delete(CadDocument document, string name)
        {
            DxfDataLink link = Find(document, name ?? throw new ArgumentNullException(nameof(name)));

            if (link == null)
            {
                return CadEditResult.Refuse("Data link " + name + " does not exist.");
            }

            if (link.GetTargets().Count > 0)
            {
                return CadEditResult.Refuse("The data link is being used and cannot be deleted.");
            }

            string handle = link.Handle;
            return document.ApplyEdit(
                "DATALINK",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Data link", name, null) },
                s => s.Database.DataLinkDictionary.Remove(Linked(s.Database, handle).Name)
            );
        }

        /// <summary>
        /// Links a table's cells to a data link, as Link Cell does and as a table made from a data link is: the data read from
        /// the source and put into the table from the cell given, rows and columns added where the table is too small to hold
        /// it, the cells linked, one undo step. A table of a drawing before AutoCAD 2007, or whose cells are linked already,
        /// is refused.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="tableHandle">The table's handle.</param>
        /// <param name="row">The top left cell's row, from nought.</param>
        /// <param name="column">The top left cell's column, from nought.</param>
        /// <param name="linkName">The data link's name.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static CadEditResult LinkCells(
            CadDocument document,
            string tableHandle,
            int row,
            int column,
            string linkName
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            DxfDataLink link = Find(document, linkName ?? throw new ArgumentNullException(nameof(linkName)));

            if (link == null)
            {
                return CadEditResult.Refuse("Data link not found.");
            }

            if (
                !(
                    document.FindEntity(tableHandle ?? throw new ArgumentNullException(nameof(tableHandle)))
                    is DxfTable table
                )
            )
            {
                return CadEditResult.Refuse("The object " + tableHandle + " is not a table.");
            }

            if (row < 0 || column < 0 || row >= table.NumRows || column >= table.NumColumns)
            {
                return CadEditResult.Refuse("The table has no cell " + CadTableEditor.CellName(row, column) + ".");
            }

            CadSheet data;

            try
            {
                data = SheetOf(link, FolderOf(document));
            }
            catch (CadDataSourceException exception)
            {
                return CadEditResult.Refuse(exception.Message);
            }

            for (int r = row; r < Math.Min(table.NumRows, row + data.RowCount); r++)
            {
                for (int c = column; c < Math.Min(table.NumColumns, column + data.ColumnCount); c++)
                {
                    if (table.GetDataLink(r, c) != null)
                    {
                        return CadEditResult.Refuse(
                            "The cells "
                                + CadTableEditor.CellName(r, c)
                                + " are linked to data already: detach the link first."
                        );
                    }
                }
            }

            string handle = link.Handle;
            return document.ApplyEdit(
                "TABLEDIT",
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.PropertyChanged, tableHandle, "Data link", null, linkName),
                },
                s =>
                {
                    if (!(s.Document.FindEntity(tableHandle) is DxfTable own))
                    {
                        return;
                    }

                    Place(s.Document, own, Linked(s.Database, handle), row, column, 0, 0, data);
                    MarkUpdated(Linked(s.Database, handle), DxfUpdateDirection.SourceToData, 0, string.Empty);
                }
            );
        }

        /// <summary>
        /// Takes the data link off a table's cells, as Detach Data Link does: the cells keep what they hold and are no longer
        /// linked, one undo step. The link stays in the drawing, in the Data Link Manager.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="tableHandle">The table's handle.</param>
        /// <param name="row">A cell of the linked range, from nought.</param>
        /// <param name="column">A cell of the linked range, from nought.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static CadEditResult Detach(CadDocument document, string tableHandle, int row, int column)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (
                !(
                    document.FindEntity(tableHandle ?? throw new ArgumentNullException(nameof(tableHandle)))
                    is DxfTable table
                )
            )
            {
                return CadEditResult.Refuse("The object " + tableHandle + " is not a table.");
            }

            if (
                row < 0
                || column < 0
                || row >= table.NumRows
                || column >= table.NumColumns
                || table.GetDataLink(row, column) == null
            )
            {
                return CadEditResult.Refuse("The cell is not linked to data.");
            }

            return document.ApplyEdit(
                "TABLEDIT",
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        tableHandle,
                        "Data link",
                        table.GetDataLink(row, column).Name,
                        null
                    ),
                },
                s =>
                {
                    if (s.Document.FindEntity(tableHandle) is DxfTable own)
                    {
                        own.RemoveDataLink(row, column);
                    }
                }
            );
        }

        /// <summary>
        /// Updates data links from their sources, as DATALINKUPDATE's Update data link does: each link's source read, and the
        /// cells of each table it fills made what the source holds, rows and columns added or taken out to its size, the link's
        /// time and status written; one undo step. A link whose source cannot be read is counted as failed with its message
        /// and its tables left as they were.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="links">The links; a data extraction's link is updated by <see cref="CadExtraction"/>.</param>
        /// <returns>How many went well and how many did not.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static CadDataLinkRun Update(CadDocument document, IEnumerable<DxfDataLink> links)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (links == null)
            {
                throw new ArgumentNullException(nameof(links));
            }

            string folder = FolderOf(document);
            var chosen = links.ToList();
            var data = new Dictionary<string, CadSheet>(StringComparer.OrdinalIgnoreCase);
            var failures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var messages = new List<string>();
            var planned = new List<CadEditRecord>();

            foreach (DxfDataLink link in chosen)
            {
                try
                {
                    data[link.Handle] = string.Equals(link.DataAdapterId, ExtractionAdapter, StringComparison.Ordinal)
                        ? CadExtraction.Rerun(document, link)
                        : SheetOf(link, folder);
                }
                catch (CadDataSourceException exception)
                {
                    failures[link.Handle] = exception.Message;
                    messages.Add(link.Name + ": " + exception.Message);
                }

                foreach (DxfTable table in link.GetTargets())
                {
                    planned.Add(
                        new CadEditRecord(
                            CadEditRecordKind.PropertyChanged,
                            table.Handle,
                            "Data link",
                            link.Name,
                            link.Name
                        )
                    );
                }
            }

            var handles = chosen.Select(l => l.Handle).ToList();
            CadEditResult edit = document.ApplyEdit("DATALINKUPDATE", planned, s =>
            {
                foreach (string handle in handles)
                {
                    DxfDataLink link = Linked(s.Database, handle);

                    if (failures.TryGetValue(handle, out string why))
                    {
                        MarkUpdated(link, DxfUpdateDirection.SourceToData, 1, why);
                        continue;
                    }

                    foreach (DxfTable table in link.GetTargets())
                    {
                        DxfCellRange range = RangeOf(table, link);

                        if (range != null)
                        {
                            Place(
                                s.Document,
                                table,
                                link,
                                range.TopRow,
                                range.LeftColumn,
                                range.BottomRow - range.TopRow + 1,
                                range.RightColumn - range.LeftColumn + 1,
                                data[handle]
                            );
                        }
                    }

                    MarkUpdated(link, DxfUpdateDirection.SourceToData, 0, string.Empty);
                }
            });

            return new CadDataLinkRun(
                edit,
                edit.Succeeded ? chosen.Count - failures.Count : 0,
                failures.Count,
                0,
                messages
            );
        }

        /// <summary>
        /// Writes data links to their sources, as DATALINKUPDATE's Write data link does: the cells of each table a link fills
        /// put back into the workbook or the CSV file, whose other cells stay as they are. A link that does not allow it (its
        /// update options lack <see cref="AllowSourceUpdate"/>) is counted apart; a source that cannot be written is failed.
        /// The status of each link is written, one undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="links">The links.</param>
        /// <returns>How many went well, how many did not and how many do not allow it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static CadDataLinkRun Write(CadDocument document, IEnumerable<DxfDataLink> links)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (links == null)
            {
                throw new ArgumentNullException(nameof(links));
            }

            string folder = FolderOf(document);
            var messages = new List<string>();
            var outcome = new Dictionary<string, string>();
            int succeeded = 0;
            int failed = 0;
            int notAllowed = 0;
            var chosen = links.ToList();

            foreach (DxfDataLink link in chosen)
            {
                if (
                    (link.UpdateOption & AllowSourceUpdate) == 0
                    || string.Equals(link.DataAdapterId, ExtractionAdapter, StringComparison.Ordinal)
                )
                {
                    notAllowed++;
                    continue;
                }

                try
                {
                    foreach (DxfTable table in link.GetTargets())
                    {
                        DxfCellRange range = RangeOf(table, link);

                        if (range != null)
                        {
                            CadDataSource.Write(link.ConnectionString, folder, CellsOf(table, range));
                        }
                    }

                    succeeded++;
                    outcome[link.Handle] = null;
                }
                catch (CadDataSourceException exception)
                {
                    failed++;
                    outcome[link.Handle] = exception.Message;
                    messages.Add(link.Name + ": " + exception.Message);
                }
            }

            CadEditResult edit = document.ApplyEdit(
                "DATALINKUPDATE",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Data link", null, "written") },
                s =>
                {
                    foreach (KeyValuePair<string, string> done in outcome)
                    {
                        MarkUpdated(
                            Linked(s.Database, done.Key),
                            DxfUpdateDirection.DataToSource,
                            done.Value == null ? 0 : 1,
                            done.Value ?? string.Empty
                        );
                    }
                }
            );

            return new CadDataLinkRun(edit, succeeded, failed, notAllowed, messages);
        }

        /// <summary>The cells a link fills in a table, or null for a link none of whose cells are in the table.</summary>
        /// <param name="table">The table.</param>
        /// <param name="link">The link.</param>
        /// <returns>The range, or null.</returns>
        internal static DxfCellRange RangeOf(DxfTable table, DxfDataLink link)
        {
            for (int r = 0; r < table.NumRows; r++)
            {
                for (int c = 0; c < table.NumColumns; c++)
                {
                    DxfDataLink at = table.GetDataLink(r, c);

                    if (at != null && string.Equals(at.Handle, link.Handle, StringComparison.OrdinalIgnoreCase))
                    {
                        return table.GetDataLinkRange(r, c);
                    }
                }
            }

            return null;
        }

        /// <summary>The cells of a range of a table as spreadsheet cells: a text that reads as a number a number, the others words.</summary>
        internal static CadSheet CellsOf(DxfTable table, DxfCellRange range)
        {
            var cells = new CadSheet(string.Empty);

            for (int r = range.TopRow; r <= range.BottomRow; r++)
            {
                for (int c = range.LeftColumn; c <= range.RightColumn; c++)
                {
                    string text = CadTableCsv.CellText(table, r, c);
                    cells.Set(
                        r - range.TopRow,
                        c - range.LeftColumn,
                        text.Length == 0 ? CadSheetCell.Empty
                            : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                            && !double.IsInfinity(number)
                            && !double.IsNaN(number)
                                ? new CadSheetCell(CadSheetCellKind.Number, text, number)
                            : CadSheetCell.OfText(text)
                    );
                }
            }

            cells.Size(range.BottomRow - range.TopRow + 1, range.RightColumn - range.LeftColumn + 1);
            return cells;
        }

        /// <summary>The cells a link's source holds now, from its connection string.</summary>
        internal static CadSheet SheetOf(DxfDataLink link, string folder) =>
            CadDataSource.Read(link.ConnectionString, folder);

        /// <summary>
        /// Puts data into a table from a cell, as a link fills its cells: the rows and columns the link filled before given up
        /// or added at the end of its block to the size of the data, the cells written, the table drawn anew, the block linked
        /// as one range. The link the cells had is taken away first.
        /// </summary>
        internal static void Place(
            CadDocument document,
            DxfTable table,
            DxfDataLink link,
            int top,
            int left,
            int oldRows,
            int oldColumns,
            CadSheet data
        )
        {
            int rows = Math.Max(1, data.RowCount);
            int columns = Math.Max(1, data.ColumnCount);

            if (oldRows > 0 && table.GetDataLink(top, left) != null)
            {
                table.RemoveDataLink(top, left);
            }

            // Rows and columns of the block the data does not need are taken out, those it needs are put in at its end (or at the
            // table's, for cells that run past it) as their last one, so that they take its look.
            if (oldRows > rows)
            {
                int give = Math.Min(oldRows - rows, table.NumRows - 1);

                if (give > 0 && table.CanDeleteRows(top + rows, give))
                {
                    table.DeleteRows(top + rows, give);
                }
            }

            int need = top + rows - table.NumRows;

            if (need > 0 && table.CanInsertRow(table.NumRows))
            {
                table.InsertRowsAndInherit(table.NumRows, table.NumRows - 1, need);
            }
            else if (oldRows > 0 && rows > oldRows && table.CanInsertRow(top + oldRows))
            {
                table.InsertRowsAndInherit(top + oldRows, top + oldRows - 1, rows - oldRows);
            }

            if (oldColumns > columns)
            {
                int give = Math.Min(oldColumns - columns, table.NumColumns - 1);

                if (give > 0 && table.CanDeleteColumns(left + columns, give))
                {
                    table.DeleteColumns(left + columns, give);
                }
            }

            int wide = left + columns - table.NumColumns;

            if (wide > 0 && table.CanInsertColumn(table.NumColumns))
            {
                table.InsertColumnsAndInherit(table.NumColumns, table.NumColumns - 1, wide);
            }
            else if (oldColumns > 0 && columns > oldColumns && table.CanInsertColumn(left + oldColumns))
            {
                table.InsertColumnsAndInherit(left + oldColumns, left + oldColumns - 1, columns - oldColumns);
            }

            bool formatting = (link.UpdateOption & SkipFormat) == 0;
            int lastRow = Math.Min(table.NumRows, top + rows);
            int lastColumn = Math.Min(table.NumColumns, left + columns);

            // The data gives each cell of its block its own words, so a block of merged cells it meets is made single cells again,
            // as a new table's merged title is when the data of a link is put in its first row.
            for (int r = top; r < lastRow; r++)
            {
                for (int c = left; c < lastColumn; c++)
                {
                    DxfCellRange merged = table.GetMergeRange(r, c);

                    if (
                        merged.TopRow == r
                        && merged.LeftColumn == c
                        && (merged.BottomRow > r || merged.RightColumn > c)
                    )
                    {
                        table.UnmergeCells(merged);
                    }
                }
            }

            for (int r = top; r < lastRow; r++)
            {
                for (int c = left; c < lastColumn; c++)
                {
                    CadSheetCell cell = data[r - top, c - left];
                    DxfCellRange block = table.GetMergeRange(r, c);

                    if (block.TopRow != r || block.LeftColumn != c)
                    {
                        continue;
                    }

                    if (cell.Kind == CadSheetCellKind.Empty)
                    {
                        if (!table.IsEmpty(r, c))
                        {
                            table.ResetValue(r, c);
                        }
                    }
                    else
                    {
                        if (!string.Equals(table.TextString(r, c), cell.Text, StringComparison.Ordinal))
                        {
                            table.SetTextString(r, c, Escape(cell.Text));
                        }
                    }

                    if (formatting)
                    {
                        Format(table, r, c, cell);
                    }
                }
            }

            CadTableEditor.Redraw(table, document);
            table.SetDataLink(new DxfCellRange(top, left, lastRow - 1, lastColumn - 1), link);
        }

        /// <summary>The words of a cell for a table's text: a backslash, brace or line break would be read as a code of MTEXT.</summary>
        private static string Escape(string text) =>
            text.Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("\r\n", "\\P")
                .Replace("\n", "\\P")
                .Replace("\r", "\\P");

        /// <summary>The look the workbook gives a cell, as far as a table's cell takes it: its alignment, its fill and the colour of its words.</summary>
        private static void Format(DxfTable table, int row, int column, CadSheetCell cell)
        {
            if (cell.Alignment != 0)
            {
                table.SetAlignment(
                    row,
                    column,
                    cell.Alignment == 1 ? DxfCellAlignment.MiddleLeft
                        : cell.Alignment == 2 ? DxfCellAlignment.MiddleCenter
                        : DxfCellAlignment.MiddleRight
                );
            }

            if (cell.Fill.HasValue)
            {
                table.SetBackgroundColor(row, column, Nearest(cell.Fill.Value));
                table.SetBackgroundColorNone(row, column, false);
            }

            if (cell.FontColor.HasValue)
            {
                table.SetContentColor(row, column, Nearest(cell.FontColor.Value));
            }
        }

        private static DxfColor[] _index;

        /// <summary>The colour of AutoCAD's index nearest a colour of red, green and blue (0xRRGGBB), as a cell takes only those; the first of equal distance.</summary>
        private static DxfColor Nearest(int rgb)
        {
            if (_index == null)
            {
                var all = new DxfColor[255];

                for (short i = 1; i <= 255; i++)
                {
                    all[i - 1] = DxfColor.FromColorIndex(DxfColorMethod.ByAci, i);
                }

                _index = all;
            }

            int red = (rgb >> 16) & 0xFF;
            int green = (rgb >> 8) & 0xFF;
            int blue = rgb & 0xFF;
            DxfColor best = _index[6];
            long least = long.MaxValue;

            foreach (DxfColor candidate in _index)
            {
                long distance =
                    (long)(candidate.Red - red) * (candidate.Red - red)
                    + (long)(candidate.Green - green) * (candidate.Green - green)
                    + (long)(candidate.Blue - blue) * (candidate.Blue - blue);

                if (distance < least)
                {
                    least = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private static CadEditResult Check(CadDocument document, CadDataLinkSettings settings, DxfDataLink own)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!CanHold(document))
            {
                return CadEditResult.Refuse("Data links need a drawing of AutoCAD 2007 or later.");
            }

            string name = (settings.Name ?? string.Empty).Trim();

            if (
                name.Length == 0
                || name.IndexOfAny(new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`' }) >= 0
            )
            {
                return CadEditResult.Refuse("Invalid data link name.");
            }

            DxfDataLink other = Find(document, name);

            if (
                other != null
                && (own == null || !string.Equals(other.Handle, own.Handle, StringComparison.OrdinalIgnoreCase))
            )
            {
                return CadEditResult.Refuse("Data link name in use.");
            }

            if (settings.PathType == CadDataLinkPathType.RelativePath && FolderOf(document) == null)
            {
                settings.PathType = CadDataLinkPathType.FullPath;
            }

            try
            {
                Preview(document, settings);
            }
            catch (CadDataSourceException exception)
            {
                return CadEditResult.Refuse(exception.Message);
            }

            return null;
        }

        #region The DATALINK object

        /// <summary>What a DATALINK holds that the library or the presenter sets.</summary>
        internal sealed class LinkValues
        {
            internal string Adapter = string.Empty;
            internal string Description = string.Empty;
            internal string ToolTip = string.Empty;
            internal string Connection = string.Empty;
            internal int Option;
            internal int Update;
            internal int Direction = 1;
            internal DateTime Time = DateTime.Now;
            internal int Status;
            internal string Message = string.Empty;
            internal string SourceFile;
        }

        /// <summary>The values of the data link of the Excel adapter the settings make, its tooltip as the Data Link Manager words it.</summary>
        private static LinkValues ExcelValues(CadDataLinkSettings settings, string connection, string folder)
        {
            string file = settings.File ?? string.Empty;
            string sheet = settings.EffectiveSheet;
            string details =
                settings.RangeKind == CadDataLinkRangeKind.NamedRange ? "Named range: " + settings.NamedRange
                : settings.RangeKind == CadDataLinkRangeKind.CellRange
                    ? "Range: " + (sheet.Length == 0 ? string.Empty : sheet + "!") + settings.Range
                : "Entire sheet: " + sheet;
            return new LinkValues
            {
                Adapter = CadDataSource.ExcelAdapter,
                ToolTip = "Data Link\n" + settings.Name.Trim() + "\n" + file + "\nLink details: " + details,
                Connection = connection,
                Update =
                    ExcelDefaults
                    | (settings.UseExcelFormatting ? 0 : SkipFormat)
                    | (settings.AllowWritingToSource ? AllowSourceUpdate : 0),
                SourceFile = CadDataSource.Resolve(
                    CadDataSource.TryParse(connection, out string named, out _, out _) ? named : file,
                    folder
                ),
            };
        }

        /// <summary>Makes a data link in the drawing's dictionary of them (made where the drawing has none), under a name.</summary>
        /// <returns>The link.</returns>
        internal static DxfDataLink Make(DxfDatabase database, string name, LinkValues values)
        {
            DxfDBDictionary root = database.NamedObjectsDictionary
                ?? throw new InvalidOperationException("The drawing has no dictionary of objects to hold a data link.");

            if (database.DataLinkDictionary == null)
            {
                root.SetAt("ACAD_DATALINK", new DxfDBDictionary());
                database.DataLinkDictionary.TreatElementsAsHard = true;
            }

            DxfDBDictionary dictionary = database.DataLinkDictionary;
            DxfFile file = dictionary.Item.File;
            file.AddClass("DATALINK", "AcDbDataLink", 1152);
            List<KeyValuePair<int, string>> groups = Groups(values, null);
            dictionary.Add(name, "DATALINK", b =>
            {
                b.Group(100, "AcDbDataLink");

                foreach (KeyValuePair<int, string> group in groups)
                {
                    b.Group(group.Key, group.Value);
                }
            });
            return (DxfDataLink)dictionary.GetAt(name);
        }

        /// <summary>Writes a link's values again, the objects it is tied to (its 94 and 330s, and its 360) as they are.</summary>
        internal static void Rewrite(DxfDataLink link, LinkValues values)
        {
            DxfFile file = link.Item.File;
            IReadOnlyList<DxfTypedValue> all = file.GroupsOf(link.Item);
            int marker = -1;

            for (int i = 0; i < all.Count && marker < 0; i++)
            {
                if (all[i].TypeCode == 100 && all[i].Value.Trim() == "AcDbDataLink")
                {
                    marker = i;
                }
            }

            if (marker < 0)
            {
                return;
            }

            List<DxfTypedValue> tail = file.TailOf(link.Item, marker + 1);
            int ties = tail.FindIndex(g => g.TypeCode == 94);
            int owner = tail.FindIndex(g => g.TypeCode == 360);
            var kept = new List<KeyValuePair<int, string>>();

            if (ties >= 0 && owner >= ties)
            {
                for (int i = ties; i <= owner; i++)
                {
                    kept.Add(new KeyValuePair<int, string>(tail[i].TypeCode, tail[i].Value));
                }
            }

            file.ReplaceTail(link.Item, marker + 1, Groups(values, kept));
        }

        /// <summary>
        /// The groups of a DATALINK after its marker, in the order AutoCAD 2021 writes them: its adapter, description, tooltip
        /// and connection string, its options and update options, which way and when it was last updated and how it went, the
        /// objects it is tied to (those given, or none), what it owns and its custom data.
        /// </summary>
        private static List<KeyValuePair<int, string>> Groups(LinkValues v, List<KeyValuePair<int, string>> ties)
        {
            var groups = new List<KeyValuePair<int, string>>();

            void Add(int code, string value) => groups.Add(new KeyValuePair<int, string>(code, value));

            string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

            Add(1, v.Adapter);
            Add(300, Caret(v.Description));
            Add(301, Caret(v.ToolTip));
            Add(302, Caret(v.Connection));
            Add(90, Number(v.Option));
            Add(91, Number(v.Update));
            Add(92, Number(v.Direction));
            Add(170, Number(v.Time.Year));
            Add(171, Number(v.Time.Month));
            Add(172, Number(v.Time.Day));
            Add(173, Number(v.Time.Hour));
            Add(174, Number(v.Time.Minute));
            Add(175, Number(v.Time.Second));
            Add(176, Number(v.Time.Millisecond));
            Add(177, Number((int)v.Time.DayOfWeek));
            Add(93, Number(v.Status));
            Add(304, Caret(v.Message));

            if (ties != null && ties.Count > 0)
            {
                groups.AddRange(ties);
            }
            else
            {
                Add(94, "0");
                Add(360, "0");
            }

            Add(305, "CUSTOMDATA");
            Add(1, "DATAMAP_BEGIN");

            if (string.Equals(v.Adapter, CadDataSource.ExcelAdapter, StringComparison.Ordinal))
            {
                Add(90, "3");
                Entry(groups, "ACEXCEL_UPDATEOPTIONS", 1, 91, Number(v.Update));
                Entry(groups, "ACEXCEL_CONNECTION_STRING", 4, 1, Caret(v.Connection));
                Entry(groups, "ACEXCEL_SOURCEDATE", 8, 310, SourceDate(v.SourceFile));
            }
            else
            {
                Add(90, "0");
            }

            Add(309, "DATAMAP_END");
            return groups;
        }

        /// <summary>One entry of a link's custom data: its name and an AcValue of a type, as AutoCAD 2021 writes the Excel adapter's three.</summary>
        private static void Entry(List<KeyValuePair<int, string>> groups, string name, int type, int code, string value)
        {
            groups.Add(new KeyValuePair<int, string>(300, name));
            groups.Add(new KeyValuePair<int, string>(301, "DATAMAP_VALUE"));
            groups.Add(new KeyValuePair<int, string>(93, "2"));
            groups.Add(new KeyValuePair<int, string>(90, type.ToString(CultureInfo.InvariantCulture)));

            if (code == 310)
            {
                groups.Add(new KeyValuePair<int, string>(92, "16"));
            }

            groups.Add(new KeyValuePair<int, string>(code, value));
            groups.Add(new KeyValuePair<int, string>(94, "0"));
            groups.Add(new KeyValuePair<int, string>(300, string.Empty));
            groups.Add(new KeyValuePair<int, string>(302, string.Empty));
            groups.Add(new KeyValuePair<int, string>(304, "ACVALUE_END"));
        }

        /// <summary>The date of a source file as the 16 bytes of a Windows SYSTEMTIME in hexadecimal (year, month, day of the week, day, hour, minute, second, millisecond; UTC), as AutoCAD keeps it.</summary>
        private static string SourceDate(string file)
        {
            DateTime time;

            try
            {
                time =
                    file != null && System.IO.File.Exists(file)
                        ? System.IO.File.GetLastWriteTimeUtc(file)
                        : DateTime.UtcNow;
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                )
            {
                time = DateTime.UtcNow;
            }

            var bytes = new StringBuilder();

            foreach (
                int part in new[]
                {
                    time.Year,
                    time.Month,
                    (int)time.DayOfWeek,
                    time.Day,
                    time.Hour,
                    time.Minute,
                    time.Second,
                    time.Millisecond,
                }
            )
            {
                bytes
                    .Append((part & 0xFF).ToString("X2", CultureInfo.InvariantCulture))
                    .Append(((part >> 8) & 0xFF).ToString("X2", CultureInfo.InvariantCulture));
            }

            return bytes.ToString();
        }

        /// <summary>Writes how a link was last updated: which way, when (now), how it went and its message; the rest of the link as it is.</summary>
        internal static void MarkUpdated(DxfDataLink link, DxfUpdateDirection direction, int status, string message)
        {
            DxfFile file = link.Item.File;
            IReadOnlyList<DxfTypedValue> all = file.GroupsOf(link.Item);
            int marker = -1;

            for (int i = 0; i < all.Count && marker < 0; i++)
            {
                if (all[i].TypeCode == 100 && all[i].Value.Trim() == "AcDbDataLink")
                {
                    marker = i;
                }
            }

            if (marker < 0)
            {
                return;
            }

            DateTime now = DateTime.Now;
            List<DxfTypedValue> tail = file.TailOf(link.Item, marker + 1);
            var groups = new List<KeyValuePair<int, string>>(tail.Count);
            bool ties = false;

            foreach (DxfTypedValue group in tail)
            {
                string value = group.Value;

                if (!ties)
                {
                    switch (group.TypeCode)
                    {
                        case 92:
                            value = direction == DxfUpdateDirection.DataToSource ? "2" : "1";
                            break;
                        case 170:
                            value = now.Year.ToString(CultureInfo.InvariantCulture);
                            break;
                        case 171:
                            value = now.Month.ToString(CultureInfo.InvariantCulture);
                            break;
                        case 172:
                            value = now.Day.ToString(CultureInfo.InvariantCulture);
                            break;
                        case 173:
                            value = now.Hour.ToString(CultureInfo.InvariantCulture);
                            break;
                        case 174:
                            value = now.Minute.ToString(CultureInfo.InvariantCulture);
                            break;
                        case 175:
                            value = now.Second.ToString(CultureInfo.InvariantCulture);
                            break;
                        case 176:
                            value = now.Millisecond.ToString(CultureInfo.InvariantCulture);
                            break;
                        case 177:
                            value = ((int)now.DayOfWeek).ToString(CultureInfo.InvariantCulture);
                            break;
                        case 93:
                            value = status.ToString(CultureInfo.InvariantCulture);
                            break;
                        case 304:
                            value = Caret(message);
                            break;
                    }

                    // The first 94 ends the part that is rewritten: what follows it are the tied objects and the custom data.
                    ties = group.TypeCode == 94;
                }

                groups.Add(new KeyValuePair<int, string>(group.TypeCode, value));
            }

            file.ReplaceTail(link.Item, marker + 1, groups);
        }

        /// <summary>The DATALINK of a handle in the drawing as it stands now (an edit may have read the drawing again).</summary>
        internal static DxfDataLink Linked(DxfDatabase database, string handle) =>
            (DxfDataLink)
                database
                    .DataLinkDictionary.Cast<DxfDBDictionaryEntry>()
                    .First(e =>
                        e.Value is DxfDataLink l && string.Equals(l.Handle, handle, StringComparison.OrdinalIgnoreCase)
                    )
                    .Value;

        /// <summary>A text as a DXF value keeps its line breaks, in the caret form of a control character.</summary>
        private static string Caret(string text) => (text ?? string.Empty).Replace("\r", "^M").Replace("\n", "^J");

        #endregion
    }
}
