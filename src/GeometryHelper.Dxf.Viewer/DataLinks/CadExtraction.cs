using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>An object type of the Select Objects page: a kind of object (Line, Circle) or a block's name, with how many there are.</summary>
    public sealed class CadExtractionType
    {
        internal CadExtractionType(string name, bool isBlock, bool hasAttributes, int count)
        {
            Name = name;
            IsBlock = isBlock;
            HasAttributes = hasAttributes;
            Count = count;
        }

        /// <summary>Gets the type's name as the page lists it: Line, Polyline, or a block's name.</summary>
        public string Name { get; }

        /// <summary>Gets whether the type is a block.</summary>
        public bool IsBlock { get; }

        /// <summary>Gets whether a block of the type has attributes.</summary>
        public bool HasAttributes { get; }

        /// <summary>Gets how many objects of the type the sources hold; nought for a block defined and not inserted.</summary>
        public int Count { get; }

        /// <summary>Gives the type as its name.</summary>
        /// <returns>The name.</returns>
        public override string ToString() => Name;
    }

    /// <summary>A property of the Select Properties page: a category and a name, such as General and Layer.</summary>
    public sealed class CadExtractionProperty
    {
        internal CadExtractionProperty(string category, string name)
        {
            Category = category;
            Name = name;
        }

        /// <summary>Gets the category: Attribute, Dynamic Block, Drawing, General, Geometry or Misc.</summary>
        public string Category { get; }

        /// <summary>Gets the property's name: Layer, Position X, an attribute's tag.</summary>
        public string Name { get; }

        /// <summary>Gets the key the settings name the property by, <c>General/Layer</c>.</summary>
        public string Key => Category + "/" + Name;

        /// <summary>Gives the property as its key.</summary>
        /// <returns>The key.</returns>
        public override string ToString() => Key;
    }

    /// <summary>What an extraction gives: its cells (the header row, then a row for each object or each group of alike), the columns' keys and how many objects of the types chosen it read.</summary>
    public sealed class CadExtractionResult
    {
        internal CadExtractionResult(
            CadSheet sheet,
            IReadOnlyList<string> columns,
            int objects,
            IReadOnlyList<string> missing
        )
        {
            Sheet = sheet;
            Columns = columns;
            Objects = objects;
            MissingFiles = missing;
        }

        /// <summary>Gets the cells: the header row first, then the data rows.</summary>
        public CadSheet Sheet { get; }

        /// <summary>Gets the key of each column, <c>Count</c>, <c>Name</c> or a property's key.</summary>
        public IReadOnlyList<string> Columns { get; }

        /// <summary>Gets how many objects of the types chosen were read, before rows were combined or filtered.</summary>
        public int Objects { get; }

        /// <summary>Gets the drawing files that could not be opened, named in the settings.</summary>
        public IReadOnlyList<string> MissingFiles { get; }
    }

    /// <summary>
    /// The Data Extraction of AutoCAD 2021 as the presenter runs it: the objects of the current drawing (all, or those
    /// selected) and of other drawing files are read, the object types and properties chosen are kept, identical rows are
    /// combined into one with a Count, the rows are filtered and sorted, and the result goes to a table in the drawing, whose
    /// cells are linked to a data link of the data extraction's own, and to a file of .xlsx, .csv or .txt. The table is
    /// updated by DATALINKUPDATE (or Update Table Data Links) by running the extraction again from its .dxe file.
    /// </summary>
    public static class CadExtraction
    {
        /// <summary>The categories of properties, in the order the Select Properties page lists them.</summary>
        public static IReadOnlyList<string> Categories { get; } =
            new[] { "Attribute", "Dynamic Block", "Drawing", "General", "Geometry", "Misc" };

        private const string CountKey = "Count";
        private const string NameKey = "Name";
        private const int DataExtractionUpdate = 0x20000 | 0x40000;

        /// <summary>One object read from a source, with the type it is listed under.</summary>
        private sealed class Found
        {
            internal string Type;
            internal bool IsBlock;
            internal DxfEntity Entity;
            internal CadDocument Source;
            internal string File;
        }

        /// <summary>The drawing files a settings' files and folders name, each once, in the order of their names.</summary>
        private static List<string> FilesOf(CadExtractionSettings settings, string drawingFolder)
        {
            var files = new List<string>();

            foreach (string file in settings.Files)
            {
                string full = CadDataSource.Resolve(file, drawingFolder) ?? file;

                if (!files.Contains(full, StringComparer.OrdinalIgnoreCase))
                {
                    files.Add(full);
                }
            }

            foreach (string folder in settings.Folders)
            {
                string full = CadDataSource.Resolve(folder, drawingFolder) ?? folder;

                if (!Directory.Exists(full))
                {
                    files.Add(full);
                    continue;
                }

                var inFolder = new List<string>();

                foreach (string pattern in new[] { "*.dwg", "*.dxf" })
                {
                    inFolder.AddRange(
                        Directory.GetFiles(
                            full,
                            pattern,
                            settings.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
                        )
                    );
                }

                inFolder.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (string file in inFolder)
                {
                    if (!files.Contains(file, StringComparer.OrdinalIgnoreCase))
                    {
                        files.Add(file);
                    }
                }
            }

            return files;
        }

        /// <summary>Every object the sources hold, in the order of the sources, the current drawing first.</summary>
        private static List<Found> Gather(
            CadDocument current,
            CadExtractionSettings settings,
            List<string> missing,
            bool includeDefinitions
        )
        {
            var found = new List<Found>();
            string folder = CadDataLinks.FolderOf(current);

            if (settings.IncludeCurrentDrawing)
            {
                GatherFrom(current, settings, settings.SelectedHandles, found, current.FileName);
            }

            foreach (string file in FilesOf(settings, folder))
            {
                CadDocument other;

                try
                {
                    other = CadDocument.Open(file);
                }
                catch (Exception exception) when (!(exception is OutOfMemoryException))
                {
                    missing.Add(file);
                    continue;
                }

                GatherFrom(other, settings, null, found, file);
            }

            return found;
        }

        private static void GatherFrom(
            CadDocument source,
            CadExtractionSettings settings,
            IReadOnlyCollection<string> only,
            List<Found> found,
            string file
        )
        {
            HashSet<string> chosen = only == null ? null : new HashSet<string>(only, StringComparer.OrdinalIgnoreCase);

            foreach (CadSpace space in source.Spaces)
            {
                if (settings.ModelSpaceOnly && !space.IsModel)
                {
                    continue;
                }

                foreach (DxfEntity entity in space.Block)
                {
                    if (entity.IsErased || (chosen != null && !chosen.Contains(entity.Handle)))
                    {
                        continue;
                    }

                    Take(entity, source, file, settings.ExtractFromBlocks, 0, found);
                }
            }
        }

        private static void Take(
            DxfEntity entity,
            CadDocument source,
            string file,
            bool inside,
            int depth,
            List<Found> found
        )
        {
            if (entity is DxfAttributeDefinition || entity is DxfAttributeReference || entity is DxfViewport)
            {
                return;
            }

            bool block = entity is DxfBlockReference;
            string type = block ? ((DxfBlockReference)entity).Name : CadProperties.KindOf(entity);

            if (block && string.IsNullOrEmpty(type))
            {
                return;
            }

            found.Add(
                new Found
                {
                    Type = type,
                    IsBlock = block,
                    Entity = entity,
                    Source = source,
                    File = file,
                }
            );

            if (!block || !inside || depth >= 4)
            {
                return;
            }

            DxfBlockTableRecord definition = ((DxfBlockReference)entity).BlockTableRecord;

            if (definition == null)
            {
                return;
            }

            foreach (DxfEntity child in definition)
            {
                if (!child.IsErased)
                {
                    Take(child, source, file, inside, depth + 1, found);
                }
            }
        }

        /// <summary>
        /// The object types the sources hold, as the Select Objects page lists them, after its display options: every kind of
        /// object and every block by name, with how many; the blocks of the current drawing that are defined and not inserted
        /// too (count nought) unless <see cref="CadExtractionSettings.UsedOnly"/> leaves them out. By name, letters of either case alike.
        /// </summary>
        /// <param name="current">The current drawing.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="missing">The drawing files that could not be opened.</param>
        /// <returns>The types.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static IReadOnlyList<CadExtractionType> Types(
            CadDocument current,
            CadExtractionSettings settings,
            out IReadOnlyList<string> missing
        )
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var lost = new List<string>();
            List<Found> all = Gather(current, settings, lost, true);
            missing = lost;
            var types = new Dictionary<string, (bool Block, bool Attributes, int Count)>(StringComparer.Ordinal);

            foreach (Found item in all)
            {
                bool attributes =
                    item.Entity is DxfBlockReference insert && insert.AttributeCollection.Any(a => !a.IsErased);
                types.TryGetValue(item.Type, out var held);
                types[item.Type] = (item.IsBlock, held.Attributes || attributes, held.Count + 1);
            }

            if (!settings.UsedOnly && settings.IncludeCurrentDrawing)
            {
                foreach (DxfBlockTableRecord block in current.Database.BlockTable)
                {
                    if (
                        block.IsAnonymous
                        || block.IsLayout
                        || block.IsFromExternalReference
                        || string.IsNullOrEmpty(block.Name)
                        || types.ContainsKey(block.Name)
                    )
                    {
                        continue;
                    }

                    types[block.Name] = (true, block.Any(e => e is DxfAttributeDefinition), 0);
                }
            }

            IEnumerable<KeyValuePair<string, (bool Block, bool Attributes, int Count)>> shown = types;

            if (settings.Display == CadExtractionDisplay.BlocksOnly)
            {
                shown = shown.Where(t => t.Value.Block);
            }
            else if (settings.Display == CadExtractionDisplay.NonBlocksOnly)
            {
                shown = shown.Where(t => !t.Value.Block);
            }

            if (settings.BlocksWithAttributesOnly)
            {
                shown = shown.Where(t => t.Value.Block && t.Value.Attributes);
            }

            return shown
                .OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.Key, StringComparer.Ordinal)
                .Select(t => new CadExtractionType(t.Key, t.Value.Block, t.Value.Attributes, t.Value.Count))
                .ToList();
        }

        /// <summary>
        /// The properties of the objects of the types chosen, as the Select Properties page lists them: the General, Geometry,
        /// Misc and Drawing properties the objects have, an Attribute for each tag of the blocks, a Dynamic Block for each
        /// dynamic property; by category (as <see cref="Categories"/> orders them) and name. The categories the settings leave
        /// out are not listed.
        /// </summary>
        /// <param name="current">The current drawing.</param>
        /// <param name="settings">The settings, whose types are the ones the objects are of.</param>
        /// <returns>The properties.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static IReadOnlyList<CadExtractionProperty> Properties(
            CadDocument current,
            CadExtractionSettings settings
        )
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var seen = new Dictionary<string, CadExtractionProperty>(StringComparer.Ordinal);

            foreach (Found item in Chosen(Gather(current, settings, new List<string>(), false), settings))
            {
                foreach (KeyValuePair<CadExtractionProperty, object> value in ValuesOf(item))
                {
                    if (!settings.UnselectedCategories.Contains(value.Key.Category) && !seen.ContainsKey(value.Key.Key))
                    {
                        seen[value.Key.Key] = value.Key;
                    }
                }
            }

            return seen
                .Values.OrderBy(p => Array.IndexOf(Categories.ToArray(), p.Category))
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Runs the extraction: the objects read, those of the types chosen kept, a column for each property chosen (and the
        /// Count and Name columns the settings show), identical rows combined with their count, the filters applied and the
        /// sorts, the first the main one, the rows not moved by a sort kept in the order they were read.
        /// </summary>
        /// <param name="current">The current drawing.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>The cells and what was read.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static CadExtractionResult Run(CadDocument current, CadExtractionSettings settings)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var missing = new List<string>();
            List<Found> read = Gather(current, settings, missing, false);
            List<Found> chosen = Chosen(read, settings).ToList();
            var rows = new List<Dictionary<string, object>>();
            var properties = new List<CadExtractionProperty>();
            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (Found item in chosen)
            {
                var row = new Dictionary<string, object>(StringComparer.Ordinal) { [NameKey] = item.Type };

                foreach (KeyValuePair<CadExtractionProperty, object> value in ValuesOf(item))
                {
                    if (settings.UnselectedCategories.Contains(value.Key.Category))
                    {
                        continue;
                    }

                    row[value.Key.Key] = value.Value;

                    if (keys.Add(value.Key.Key))
                    {
                        properties.Add(value.Key);
                    }
                }

                rows.Add(row);
            }

            // The columns: the properties chosen in their order, else every property found by category and name.
            List<string> propertyKeys =
                settings.SelectedProperties != null
                    ? settings.SelectedProperties.Where(k => keys.Contains(k) || true).ToList()
                    : properties
                        .OrderBy(p => Array.IndexOf(Categories.ToArray(), p.Category))
                        .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(p => p.Name, StringComparer.Ordinal)
                        .Select(p => p.Key)
                        .ToList();
            var columns = new List<string>();

            if (settings.ShowCount)
            {
                columns.Add(CountKey);
            }

            if (settings.ShowName)
            {
                columns.Add(NameKey);
            }

            columns.AddRange(propertyKeys);

            if (settings.ColumnOrder != null)
            {
                var ordered = settings.ColumnOrder.Where(k => columns.Contains(k)).ToList();
                ordered.AddRange(columns.Where(k => !ordered.Contains(k)));
                columns = ordered;
            }

            // Rows alike are one; each keeps the position of the first of them.
            var groups = new List<(Dictionary<string, object> Row, int Count)>();

            if (settings.CombineIdenticalRows)
            {
                var index = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (Dictionary<string, object> row in rows)
                {
                    string signature = string.Join(
                        "\u0001",
                        columns
                            .Where(c => c != CountKey)
                            .Select(c => row.TryGetValue(c, out object v) ? Show(v) : string.Empty)
                    );

                    if (index.TryGetValue(signature, out int at))
                    {
                        groups[at] = (groups[at].Row, groups[at].Count + 1);
                    }
                    else
                    {
                        index[signature] = groups.Count;
                        groups.Add((row, 1));
                    }
                }
            }
            else
            {
                groups.AddRange(rows.Select(r => (r, 1)));
            }

            List<(Dictionary<string, object> Row, int Count)> kept = groups
                .Where(g => settings.Filters.All(f => Passes(f, g.Row, g.Count)))
                .ToList();
            IOrderedEnumerable<(Dictionary<string, object> Row, int Count)> ordering = null;

            foreach (CadExtractionSort sort in settings.Sorts.Where(s => columns.Contains(s.Column)))
            {
                string column = sort.Column;
                var comparer = new ValueComparer();
                Func<(Dictionary<string, object> Row, int Count), object> key = g =>
                    column == CountKey ? g.Count
                    : g.Row.TryGetValue(column, out object v) ? v
                    : string.Empty;
                ordering =
                    ordering == null
                        ? (sort.Descending ? kept.OrderByDescending(key, comparer) : kept.OrderBy(key, comparer))
                        : (sort.Descending ? ordering.ThenByDescending(key, comparer) : ordering.ThenBy(key, comparer));
            }

            List<(Dictionary<string, object> Row, int Count)> finished = ordering == null ? kept : ordering.ToList();
            var sheet = new CadSheet("Sheet1");

            for (int c = 0; c < columns.Count; c++)
            {
                sheet.Set(0, c, CadSheetCell.OfText(Header(columns[c], settings)));
            }

            for (int r = 0; r < finished.Count; r++)
            {
                for (int c = 0; c < columns.Count; c++)
                {
                    object value =
                        columns[c] == CountKey ? finished[r].Count
                        : finished[r].Row.TryGetValue(columns[c], out object held) ? held
                        : null;
                    sheet.Set(r + 1, c, CellOf(value));
                }
            }

            sheet.Size(finished.Count + 1, columns.Count);
            return new CadExtractionResult(sheet, columns, chosen.Count, missing);
        }

        /// <summary>
        /// Runs a data extraction again from its link, as updating a data extraction table does: the link's connection string
        /// is the .dxe file (a relative one is of the drawing's folder), whose settings are run on the drawing as it stands.
        /// </summary>
        /// <param name="document">The current drawing.</param>
        /// <param name="link">The data link of the extraction.</param>
        /// <returns>The cells: the header row, then the data rows.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="CadDataSourceException">Thrown when the .dxe file is not there or cannot be read.</exception>
        public static CadSheet Rerun(CadDocument document, DxfDataLink link)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            string path = CadDataSource.Resolve(link.ConnectionString, CadDataLinks.FolderOf(document));

            if (path == null || !File.Exists(path))
            {
                throw new CadDataSourceException(
                    "The data extraction file \"" + link.ConnectionString + "\" cannot be found."
                );
            }

            try
            {
                return Run(document, CadExtractionSettings.Load(path)).Sheet;
            }
            catch (Exception exception)
                when (exception is InvalidDataException
                    || exception is IOException
                    || exception is UnauthorizedAccessException
                )
            {
                throw new CadDataSourceException(
                    "The data extraction file cannot be read: " + exception.Message,
                    exception
                );
            }
        }

        /// <summary>
        /// Writes cells to a file by its extension: <c>.xlsx</c> a workbook of one sheet (numbers as numbers), <c>.csv</c>
        /// comma separated values, <c>.txt</c> tab separated; any other extension, as the .xls and .mdb AutoCAD also writes, is
        /// refused.
        /// </summary>
        /// <param name="path">The file; one there is written over.</param>
        /// <param name="sheet">The cells.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the extension is none of the three.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public static void WriteFile(string path, CadSheet sheet)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();

            if (extension == ".xlsx")
            {
                CadWorkbook.Write(path, "Sheet1", sheet);
            }
            else if (extension == ".csv")
            {
                CadCsvFile.Write(path, sheet);
            }
            else if (extension == ".txt")
            {
                var text = new StringBuilder();

                for (int r = 0; r < sheet.RowCount; r++)
                {
                    for (int c = 0; c < sheet.ColumnCount; c++)
                    {
                        text.Append(c > 0 ? "\t" : string.Empty)
                            .Append(sheet[r, c].Text.Replace('\t', ' ').Replace("\r", " ").Replace("\n", " "));
                    }

                    text.Append("\r\n");
                }

                File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            }
            else
            {
                throw new NotSupportedException(
                    "A data extraction is written to .xlsx, .csv or .txt; \"" + extension + "\" is not one of them."
                );
            }
        }

        /// <summary>The data link of a data extraction in the drawing, made where it has none: its name is <c>Data Extraction - </c> and the file's name, its connection string the .dxe file.</summary>
        /// <param name="database">The drawing.</param>
        /// <param name="dxe">The settings file's full path.</param>
        /// <param name="folder">The drawing's folder; a file in it is named by its own name, as AutoCAD names its own.</param>
        /// <returns>The link.</returns>
        internal static DxfDataLink LinkFor(DxfDatabase database, string dxe, string folder)
        {
            string file = Path.GetFileName(dxe);
            string name = "Data Extraction - " + file;
            DxfDBDictionary dictionary = database.DataLinkDictionary;

            if (dictionary != null && dictionary.Contains(name) && dictionary.GetAt(name) is DxfDataLink own)
            {
                return own;
            }

            bool same =
                !string.IsNullOrEmpty(folder)
                && string.Equals(
                    Path.GetDirectoryName(Path.GetFullPath(dxe)),
                    folder.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase
                );
            return CadDataLinks.Make(
                database,
                name,
                new CadDataLinks.LinkValues
                {
                    Adapter = CadDataLinks.ExtractionAdapter,
                    ToolTip = "Created from Data Extraction:\r\n" + Path.GetFileNameWithoutExtension(file),
                    Connection = same ? file : dxe,
                    Option = 5,
                    Update = DataExtractionUpdate,
                }
            );
        }

        /// <summary>The cells of a whole table of an extraction: the title in its first row, then the header and data rows of the result.</summary>
        /// <param name="title">The title; empty for a first row of no words.</param>
        /// <param name="result">The result of the extraction.</param>
        /// <returns>The table's cells.</returns>
        internal static CadSheet TableCells(string title, CadSheet result)
        {
            var cells = new CadSheet(string.Empty);

            if (!string.IsNullOrEmpty(title))
            {
                cells.Set(0, 0, CadSheetCell.OfText(title));
            }

            for (int r = 0; r < result.RowCount; r++)
            {
                for (int c = 0; c < result.ColumnCount; c++)
                {
                    cells.Set(r + 1, c, result[r, c]);
                }
            }

            cells.Size(result.RowCount + 1, Math.Max(1, result.ColumnCount));
            return cells;
        }

        private static IEnumerable<Found> Chosen(List<Found> items, CadExtractionSettings settings)
        {
            HashSet<string> types =
                settings.SelectedTypes == null
                    ? null
                    : new HashSet<string>(settings.SelectedTypes, StringComparer.Ordinal);
            return items.Where(i => types == null || types.Contains(i.Type));
        }

        private static string Header(string key, CadExtractionSettings settings)
        {
            string name =
                key == CountKey ? CountKey
                : key == NameKey ? NameKey
                : key.Substring(key.IndexOf('/') + 1);

            if (!settings.ColumnNames.TryGetValue(key, out string alias) || alias.Length == 0)
            {
                return name;
            }

            return settings.UsePropertyNamesAsHeaders && alias != name ? alias + "\n" + name : alias;
        }

        private static CadSheetCell CellOf(object value)
        {
            if (value is double number)
            {
                return new CadSheetCell(CadSheetCellKind.Number, Show(number), number);
            }

            if (value is int whole)
            {
                return new CadSheetCell(CadSheetCellKind.Number, whole.ToString(CultureInfo.InvariantCulture), whole);
            }

            return CadSheetCell.OfText(value as string ?? string.Empty);
        }

        /// <summary>The words of a value: a number to four places with no trailing noughts, a text as it is.</summary>
        private static string Show(object value)
        {
            if (value is double number)
            {
                double rounded = Math.Round(number, 4, MidpointRounding.AwayFromZero);
                return (rounded == 0.0 ? 0.0 : rounded).ToString("0.####", CultureInfo.InvariantCulture);
            }

            return value is int whole ? whole.ToString(CultureInfo.InvariantCulture) : value as string ?? string.Empty;
        }

        private static bool Passes(CadExtractionFilter filter, Dictionary<string, object> row, int count)
        {
            if (filter.Condition == CadFilterCondition.None)
            {
                return true;
            }

            object held =
                filter.Column == CountKey ? count
                : row.TryGetValue(filter.Column, out object v) ? v
                : string.Empty;
            string text = Show(held);
            double left = 0.0;
            double right = 0.0;
            bool numbers =
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out left)
                && double.TryParse(filter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out right);
            int compare = numbers
                ? left.CompareTo(right)
                : string.Compare(text, filter.Value, StringComparison.OrdinalIgnoreCase);

            switch (filter.Condition)
            {
                case CadFilterCondition.Greater:
                    return compare > 0;
                case CadFilterCondition.GreaterOrEqual:
                    return compare >= 0;
                case CadFilterCondition.Less:
                    return compare < 0;
                case CadFilterCondition.LessOrEqual:
                    return compare <= 0;
                case CadFilterCondition.Equal:
                    return compare == 0;
                case CadFilterCondition.NotEqual:
                    return compare != 0;
                case CadFilterCondition.Contains:
                    return text.IndexOf(filter.Value, StringComparison.OrdinalIgnoreCase) >= 0;
                case CadFilterCondition.BeginsWith:
                    return text.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase);
                case CadFilterCondition.Between:
                case CadFilterCondition.Outside:
                    double second = 0.0;
                    bool both =
                        numbers
                        && double.TryParse(filter.Second, NumberStyles.Float, CultureInfo.InvariantCulture, out second);
                    bool within = both
                        ? left >= Math.Min(right, second) && left <= Math.Max(right, second)
                        : string.Compare(text, filter.Value, StringComparison.OrdinalIgnoreCase) >= 0
                            && string.Compare(text, filter.Second, StringComparison.OrdinalIgnoreCase) <= 0;
                    return filter.Condition == CadFilterCondition.Between ? within : !within;
                default:
                    return true;
            }
        }

        /// <summary>Numbers before words, numbers by value, words by their letters (either case alike).</summary>
        private sealed class ValueComparer : IComparer<object>
        {
            public int Compare(object x, object y)
            {
                bool nx = x is double || x is int;
                bool ny = y is double || y is int;

                if (nx && ny)
                {
                    return Convert
                        .ToDouble(x, CultureInfo.InvariantCulture)
                        .CompareTo(Convert.ToDouble(y, CultureInfo.InvariantCulture));
                }

                if (nx != ny)
                {
                    return nx ? -1 : 1;
                }

                int by = string.Compare(Show(x), Show(y), StringComparison.OrdinalIgnoreCase);
                return by != 0 ? by : string.CompareOrdinal(Show(x), Show(y));
            }
        }

        #region The properties of an object

        /// <summary>The properties an object has with their values, in the order they are listed in.</summary>
        private static List<KeyValuePair<CadExtractionProperty, object>> ValuesOf(Found item)
        {
            var values = new List<KeyValuePair<CadExtractionProperty, object>>();
            DxfEntity entity = item.Entity;

            void Add(string category, string name, object value) =>
                values.Add(
                    new KeyValuePair<CadExtractionProperty, object>(new CadExtractionProperty(category, name), value)
                );

            DxfDatabase database = item.Source.Database;
            DxfDatabaseSummaryInfo summary = database.SummaryInfo;
            string file = item.File ?? string.Empty;
            Add("Drawing", "File Name", string.IsNullOrEmpty(file) ? string.Empty : Path.GetFileName(file));
            Add(
                "Drawing",
                "File Location",
                string.IsNullOrEmpty(file) ? string.Empty : Path.GetDirectoryName(Path.GetFullPath(file))
            );
            Add("Drawing", "Title", summary?.Title ?? string.Empty);
            Add("Drawing", "Subject", summary?.Subject ?? string.Empty);
            Add("Drawing", "Author", summary?.Author ?? string.Empty);
            Add("Drawing", "Comments", summary?.Comments ?? string.Empty);
            Add("Drawing", "Keywords", summary?.Keywords ?? string.Empty);

            Add("General", "Color", ColorText(entity.Color));
            Add("General", "Layer", entity.Layer ?? string.Empty);
            Add("General", "Linetype", entity.Linetype ?? string.Empty);
            Add("General", "Linetype scale", entity.LinetypeScale);
            Add("General", "Lineweight", WeightText(entity.LineWeight));
            Add("General", "Transparency", entity.Transparency.ToString());
            Add(
                "General",
                "Hyperlink",
                entity.Hyperlinks.Count > 0 ? entity.Hyperlinks[0].DisplayString : string.Empty
            );
            Add("Misc", "Handle", entity.Handle ?? string.Empty);

            switch (entity)
            {
                case DxfBlockReference insert:
                    Add("Geometry", "Position X", insert.Position.X);
                    Add("Geometry", "Position Y", insert.Position.Y);
                    Add("Geometry", "Position Z", insert.Position.Z);
                    Add("Geometry", "Rotation", Math.Round(insert.Rotation * 180.0 / Math.PI, 9));
                    Add("Geometry", "Scale X", insert.ScaleFactors.X);
                    Add("Geometry", "Scale Y", insert.ScaleFactors.Y);
                    Add("Geometry", "Scale Z", insert.ScaleFactors.Z);

                    foreach (DxfAttributeReference attribute in insert.AttributeCollection)
                    {
                        if (!attribute.IsErased && !string.IsNullOrEmpty(attribute.Tag))
                        {
                            Add("Attribute", attribute.Tag, attribute.TextString ?? string.Empty);
                        }
                    }

                    if (insert.IsDynamicBlock)
                    {
                        foreach (
                            DxfDynamicBlockReferenceProperty property in insert.DynamicBlockReferencePropertyCollection
                        )
                        {
                            Add(
                                "Dynamic Block",
                                property.PropertyName,
                                property.Value is double d
                                    ? (object)d
                                    : Convert.ToString(property.Value, CultureInfo.InvariantCulture) ?? string.Empty
                            );
                        }
                    }

                    break;
                case DxfLine line:
                    Add("Geometry", "Start X", line.StartPoint.X);
                    Add("Geometry", "Start Y", line.StartPoint.Y);
                    Add("Geometry", "Start Z", line.StartPoint.Z);
                    Add("Geometry", "End X", line.EndPoint.X);
                    Add("Geometry", "End Y", line.EndPoint.Y);
                    Add("Geometry", "End Z", line.EndPoint.Z);
                    Add("Geometry", "Length", line.Length);
                    Add("Geometry", "Angle", Math.Round(line.Angle * 180.0 / Math.PI, 9));
                    break;
                case DxfCircle circle:
                    Add("Geometry", "Center X", circle.Center.X);
                    Add("Geometry", "Center Y", circle.Center.Y);
                    Add("Geometry", "Center Z", circle.Center.Z);
                    Add("Geometry", "Radius", circle.Radius);
                    Add("Geometry", "Diameter", circle.Diameter);
                    Add("Geometry", "Circumference", circle.Circumference);
                    Add("Geometry", "Area", Math.PI * circle.Radius * circle.Radius);
                    break;
                case DxfArc arc:
                    Add("Geometry", "Center X", arc.Center.X);
                    Add("Geometry", "Center Y", arc.Center.Y);
                    Add("Geometry", "Center Z", arc.Center.Z);
                    Add("Geometry", "Radius", arc.Radius);
                    Add("Geometry", "Start angle", Math.Round(arc.StartAngle * 180.0 / Math.PI, 9));
                    Add("Geometry", "End angle", Math.Round(arc.EndAngle * 180.0 / Math.PI, 9));
                    Add("Geometry", "Total angle", Math.Round(arc.TotalAngle * 180.0 / Math.PI, 9));
                    Add("Geometry", "Length", arc.Length);
                    break;
                case DxfEllipse ellipse:
                    Add("Geometry", "Center X", ellipse.Center.X);
                    Add("Geometry", "Center Y", ellipse.Center.Y);
                    Add("Geometry", "Center Z", ellipse.Center.Z);
                    Add("Geometry", "Major radius", ellipse.MajorRadius);
                    Add("Geometry", "Minor radius", ellipse.MinorRadius);
                    break;
                case DxfPolyline polyline:
                    Add("Geometry", "Closed", polyline.Closed ? "Yes" : "No");
                    Add("Geometry", "Length", polyline.Length);
                    Add("Geometry", "Area", polyline.Closed ? polyline.Area : 0.0);
                    Add("Geometry", "Number of vertices", polyline.NumberOfVertices);
                    Add("Geometry", "Elevation", polyline.Elevation);
                    break;
                case DxfDBText text:
                    Add("Geometry", "Position X", text.Position.X);
                    Add("Geometry", "Position Y", text.Position.Y);
                    Add("Geometry", "Position Z", text.Position.Z);
                    Add("Geometry", "Rotation", Math.Round(text.Rotation * 180.0 / Math.PI, 9));
                    Add("Misc", "Contents", text.TextString ?? string.Empty);
                    Add("Misc", "Text height", text.Height);
                    Add("Misc", "Style", text.TextStyleName ?? string.Empty);
                    break;
                case DxfMText mtext:
                    Add("Geometry", "Position X", mtext.Location.X);
                    Add("Geometry", "Position Y", mtext.Location.Y);
                    Add("Geometry", "Position Z", mtext.Location.Z);
                    Add("Geometry", "Rotation", Math.Round(mtext.Rotation * 180.0 / Math.PI, 9));
                    Add("Misc", "Contents", mtext.Text ?? string.Empty);
                    Add("Misc", "Text height", mtext.TextHeight);
                    Add("Misc", "Style", mtext.TextStyleName ?? string.Empty);
                    break;
                case DxfDBPoint point:
                    Add("Geometry", "Position X", point.Position.X);
                    Add("Geometry", "Position Y", point.Position.Y);
                    Add("Geometry", "Position Z", point.Position.Z);
                    break;
                case DxfHatch hatch:
                    Add("Misc", "Pattern name", hatch.PatternName ?? string.Empty);
                    Add("Misc", "Pattern scale", hatch.PatternScale);
                    Add("Misc", "Pattern angle", Math.Round(hatch.PatternAngle * 180.0 / Math.PI, 9));
                    break;
                case DxfDimension dimension:
                    Add("Misc", "Measurement", dimension.Measurement);
                    Add("Misc", "Dim style", dimension.DimensionStyleName ?? string.Empty);
                    Add("Misc", "Text override", dimension.DimensionText ?? string.Empty);
                    break;
            }

            return values;
        }

        private static string ColorText(DxfColor color)
        {
            if (color == null || color.IsByLayer)
            {
                return "ByLayer";
            }

            if (color.IsByBlock)
            {
                return "ByBlock";
            }

            if (color.IsByColor)
            {
                return color.Red.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + color.Green.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + color.Blue.ToString(CultureInfo.InvariantCulture);
            }

            string[] names = { "ByBlock", "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };
            return color.ColorIndex >= 1 && color.ColorIndex <= 7
                ? names[color.ColorIndex]
                : color.ColorIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static string WeightText(DxfLineWeight weight)
        {
            int hundredths = (int)weight;
            return hundredths >= 0
                ? (hundredths / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm"
                : weight.ToString();
        }

        #endregion
    }
}
