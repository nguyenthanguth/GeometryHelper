using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What DATALINK gives the Data Link Manager: the drawing whose links it lists, makes, changes and deletes.</summary>
    public sealed class CadDataLinkManagerRequest
    {
        /// <summary>Makes the request of a document.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadDataLinkManagerRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }
    }

    /// <summary>
    /// What DATAEXTRACTION gives the Data Extraction wizard: the settings it fills, the .dxe file they are saved in, and what was
    /// pressed. Select objects in the wizard closes it with <see cref="IsSelecting"/> set; the command asks "Select objects:" and
    /// shows it again with the objects in <see cref="CadExtractionSettings.SelectedHandles"/>. A test fills it with no window.
    /// </summary>
    public sealed class CadDataExtractionRequest
    {
        /// <summary>Makes the request of a document.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadDataExtractionRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            string folder = CadDataLinks.FolderOf(document)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DxeFile = Path.Combine(folder, Path.GetFileNameWithoutExtension(document.Title ?? "Drawing") + ".dxe");
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets or sets the settings the wizard collects; new ones as it opens.</summary>
        public CadExtractionSettings Settings { get; set; } = new CadExtractionSettings();

        /// <summary>Gets or sets the .dxe file the settings are saved in, offered as the drawing's name in its folder.</summary>
        public string DxeFile { get; set; }

        /// <summary>Gets or sets whether the wizard closed for the objects to be selected in the drawing.</summary>
        public bool IsSelecting { get; set; }

        /// <summary>Gets or sets the page the wizard opens at, from nought (Begin) to 7 (Finish); the page it was closed at for the objects to be selected, so that it is shown again there.</summary>
        public int Page { get; set; }

        /// <summary>Gets or sets what was pressed: Finish is OK, Cancel is Cancel.</summary>
        public CadDialogAction Action { get; set; }
    }

    /// <summary>
    /// DATALINK, DATALINKUPDATE, DATAEXTRACTION and -DATAEXTRACTION, as AutoCAD 2021's: the Data Link Manager, the update and
    /// the write of data links, and the Data Extraction wizard or its command line; and a table made from a data link, which
    /// TABLE's dialog and -TABLE's data Link option make.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddDataLinksTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("DATALINK", DataLinkManager));
            registry.Add(new CadCommand("DATALINKUPDATE", DataLinkUpdate));
            registry.Add(new CadCommand("DATAEXTRACTION", DataExtraction));
            registry.Add(new CadCommand("-DATAEXTRACTION", DataExtractionTyped));
        }

        /// <summary>DATALINK: the window's Data Link Manager; with no window the links of the drawing are listed as DATALINKUPDATE's ? lists them.</summary>
        private static IEnumerable<CadPrompt> DataLinkManager(CadCommandContext c)
        {
            if (!c.Session.Request("DATALINK", c.Document, new CadDataLinkManagerRequest(c.Document)))
            {
                ListDataLinks(c);
                c.Write("The Data Link Manager dialog is shown by the window.");
            }

            yield break;
        }

        private static void ListDataLinks(CadCommandContext c)
        {
            c.Write("Data links in Drawing:");
            c.Write("----------------------");

            foreach (CadDataLinkInfo link in CadDataLinks.List(c.Document))
            {
                c.Write(link.Name);
            }
        }

        /// <summary>
        /// DATALINKUPDATE: "Select an option [Update data link/Write data link] &lt;Update&gt;:", then "Select objects or [Data
        /// link/all data linKs]:", the tables picked, or "Enter data link name or [?]:" for one link, or all of them. "n data
        /// link(s) updated successfully." (or "written out successfully", "could not be updated", "could not be written out",
        /// "do not allow their data to be written out") ends it; one undo step. A drawing with no link says "No data links exist
        /// in the current drawing. To create a data link, use the DATALINK command."
        /// </summary>
        private static IEnumerable<CadPrompt> DataLinkUpdate(CadCommandContext c)
        {
            if (CadDataLinks.List(c.Document).Count == 0)
            {
                c.Write("No data links exist in the current drawing.");
                c.Write("To create a data link, use the DATALINK command.");
                yield break;
            }

            string option = c.Remembered("datalinkupdate.write", 0.0) > 0.5 ? "Write" : "Update";
            yield return CadPrompt.ForKeyword(
                "Select an option",
                new[] { "Update data link", "Write data link" },
                option
            );

            if (c.Input.Kind != CadInputKind.Keyword && !c.Input.IsNone)
            {
                yield break;
            }

            bool write =
                c.Input.Kind == CadInputKind.Keyword
                    && c.Input.Text.StartsWith("Write", StringComparison.OrdinalIgnoreCase)
                || c.Input.IsNone && option == "Write";
            c.Remember("datalinkupdate.write", write ? 1.0 : 0.0);
            var links = new List<DxfDataLink>();
            int objects = 0;
            int notLinked = 0;

            while (true)
            {
                yield return CadPrompt.ForSelection("Select objects", new[] { "Data link", "all data linKs" });

                if (c.Input.Is("Data link"))
                {
                    DxfDataLink named = null;

                    while (named == null)
                    {
                        yield return CadPrompt.ForKeywordOrText("Enter data link name or", new[] { "?" }, null);

                        if (c.Input.Is("?"))
                        {
                            ListDataLinks(c);
                            continue;
                        }

                        string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                        if (typed.Length == 0)
                        {
                            break;
                        }

                        named = CadDataLinks.Find(c.Document, typed);

                        if (named == null)
                        {
                            c.Write("Data link " + typed + " does not exist.");
                        }
                    }

                    if (named != null)
                    {
                        links.Add(named);
                    }

                    break;
                }

                if (c.Input.Is("all data linKs"))
                {
                    links.AddRange(CadDataLinks.List(c.Document).Select(l => l.Link));
                    break;
                }

                if (c.Input.Kind == CadInputKind.Selection)
                {
                    foreach (string handle in c.Input.Handles ?? Array.Empty<string>())
                    {
                        objects++;

                        if (c.Document.FindEntity(handle) is DxfTable table && table.GetDataLink().Count > 0)
                        {
                            links.AddRange(table.GetDataLink());
                        }
                        else
                        {
                            notLinked++;
                        }
                    }

                    c.Write(objects.ToString(CultureInfo.InvariantCulture) + " object(s) found.");
                }

                break;
            }

            var chosen = links
                .GroupBy(l => l.Handle, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(l => l.Name, StringComparer.Ordinal)
                .ToList();

            if (chosen.Count == 0)
            {
                if (notLinked > 0)
                {
                    c.Write(notLinked.ToString(CultureInfo.InvariantCulture) + " object(s) could not be updated.");
                }

                yield break;
            }

            c.OpenUndoStep();
            CadDataLinkRun run = write
                ? CadDataLinks.Write(c.Document, chosen)
                : CadDataLinks.Update(c.Document, chosen);
            c.CloseUndoStep();

            foreach (string message in run.Messages)
            {
                c.Write(message);
            }

            if (!run.Edit.Succeeded)
            {
                c.Write(run.Edit.Message ?? "The data links could not be changed.");
            }

            string n(int count) => count.ToString(CultureInfo.InvariantCulture);

            if (write)
            {
                if (run.NotAllowed > 0)
                {
                    c.Write(n(run.NotAllowed) + " data link(s) do not allow their data to be written out.");
                }

                if (run.Failed > 0)
                {
                    c.Write(n(run.Failed) + " data link(s) could not be written out.");
                }

                if (run.Succeeded > 0)
                {
                    c.Write(n(run.Succeeded) + " data link(s) written out successfully.");
                }
            }
            else
            {
                if (run.Failed > 0 || !run.Edit.Succeeded)
                {
                    c.Write(n(run.Edit.Succeeded ? run.Failed : chosen.Count) + " data link(s) could not be updated.");
                }

                if (run.Succeeded > 0)
                {
                    c.Write(n(run.Succeeded) + " data link(s) updated successfully.");
                }
            }

            if (notLinked > 0)
            {
                c.Write(n(notLinked) + " object(s) could not be updated.");
            }
        }

        /// <summary>
        /// A table made from a data link, as the Insert Table dialog's From a data link makes it: the link's data read, the
        /// insertion point asked, and a table of the data's size made in the style, its cells linked, one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> TableFromDataLink(CadCommandContext c, string styleName, string linkName)
        {
            DxfDataLink link = CadDataLinks.Find(c.Document, linkName);

            if (link == null)
            {
                c.Write("Data link not found.");
                yield break;
            }

            CadSheet data;

            try
            {
                data = CadDataLinks.SheetOf(link, CadDataLinks.FolderOf(c.Document));
            }
            catch (CadDataSourceException exception)
            {
                c.Write(exception.Message);
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify insertion point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            string handle = link.Handle;
            MakeDataTable(
                c,
                styleName,
                c.Input.Point,
                data,
                0,
                database => CadDataLinks.Linked(database, handle),
                false
            );
        }

        /// <summary>
        /// Makes a table of the cells given, in a style, at a corner: as many rows and columns as the cells, the columns as wide
        /// as their longest words need (within 20 and 100), the cells from a row linked to the data link the function gives; the
        /// first row merged across the table where it is a title. One undo step. Gives the table's handle, or null where it was
        /// not made.
        /// </summary>
        internal static string MakeDataTable(
            CadCommandContext c,
            string styleName,
            GeoPoint2 corner,
            CadSheet cells,
            int linkTop,
            Func<DxfDatabase, DxfDataLink> link,
            bool mergeTitle
        )
        {
            c.Document.EnsureNamedObjectsDictionary();
            c.OpenUndoStep();
            string handle = null;
            CadEditResult result = c.Apply(null, s =>
            {
                CadTableStyleEditor.EnsureStandard(s.Database);
                DxfTableStyle style = CadTableStyleEditor.Find(s.Database, styleName)
                    ?? CadTableStyleEditor.Find(s.Database, "Standard");
                int rows = Math.Max(1, cells.RowCount);
                int columns = Math.Max(1, cells.ColumnCount);

                if (
                    !TableLayout(
                        style,
                        corner,
                        null,
                        columns,
                        63.5,
                        Math.Max(1, rows - 2),
                        1,
                        out GeoPoint2 top,
                        out _,
                        out double[] heights
                    )
                )
                {
                    throw new InvalidOperationException("The table has no size.");
                }

                var table = new DxfTable(P3(top), rows, columns, heights[heights.Length - 1], 63.5);
                s.Append(c.Space, Current(c, table));
                table.TableStyle = style;

                for (int r = 0; r < Math.Min(2, rows); r++)
                {
                    table.SetRowHeight(r, heights[r]);
                }

                double letter = style.TextHeight(DxfRowType.DataRow) * 0.8;

                for (int column = 0; column < columns; column++)
                {
                    int longest = 1;

                    for (int r = 0; r < rows; r++)
                    {
                        if (!(mergeTitle && r == 0))
                        {
                            longest = Math.Max(longest, Longest(cells[r, column].Text));
                        }
                    }

                    table.SetColumnWidth(
                        column,
                        Math.Max(20.0, Math.Min(100.0, longest * letter + 2.0 * style.HorizontalCellMargin))
                    );
                }

                if (mergeTitle && columns > 1)
                {
                    table.MergeCells(new DxfCellRange(0, 0, 0, columns - 1));
                }

                for (int column = 0; column < (mergeTitle ? 1 : 0); column++)
                {
                    if (cells[0, column].Kind != CadSheetCellKind.Empty)
                    {
                        table.SetTextString(0, column, cells[0, column].Text);
                    }
                }

                DxfDataLink made = link?.Invoke(s.Database);
                var rest = new CadSheet(string.Empty);

                for (int r = linkTop; r < rows; r++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        rest.Set(r - linkTop, column, cells[r, column]);
                    }
                }

                rest.Size(rows - linkTop, columns);

                if (made != null)
                {
                    CadDataLinks.Place(s.Document, table, made, linkTop, 0, 0, 0, rest);
                    CadDataLinks.MarkUpdated(made, DxfUpdateDirection.SourceToData, 0, string.Empty);
                }
                else
                {
                    for (int r = linkTop; r < rows; r++)
                    {
                        for (int column = 0; column < columns; column++)
                        {
                            if (cells[r, column].Kind != CadSheetCellKind.Empty)
                            {
                                table.SetTextString(r, column, cells[r, column].Text);
                            }
                        }
                    }

                    CadTableEditor.Redraw(table, s.Document);
                }

                handle = table.Handle;
            });
            c.CloseUndoStep();

            if (!result.Succeeded)
            {
                c.Write(result.Message ?? "The table could not be made.");
            }

            return result.Succeeded ? handle : null;
        }

        /// <summary>The longest line of a text, in letters.</summary>
        private static int Longest(string text) => (text ?? string.Empty).Split('\n').Max(line => line.Length);

        #region Data extraction

        /// <summary>
        /// DATAEXTRACTION: the window's Data Extraction wizard, shown again after its Select objects button has had the objects
        /// picked; Finish saves the settings to their .dxe file, writes the file output, and asks the insertion point of the
        /// table. With no window, -DATAEXTRACTION's questions.
        /// </summary>
        private static IEnumerable<CadPrompt> DataExtraction(CadCommandContext c)
        {
            var request = new CadDataExtractionRequest(c.Document);

            while (true)
            {
                request.IsSelecting = false;

                if (!c.Session.Request("DATAEXTRACTION", c.Document, request))
                {
                    foreach (CadPrompt prompt in DataExtractionTyped(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (!request.IsSelecting)
                {
                    break;
                }

                yield return CadPrompt.ForSelection("Select objects");

                if (c.Input.Kind == CadInputKind.Selection)
                {
                    request.Settings.SelectedHandles = (c.Input.Handles ?? Array.Empty<string>()).ToList();
                }
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            foreach (CadPrompt prompt in FinishExtraction(c, request.Settings, request.DxeFile))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// -DATAEXTRACTION: a data extraction file to read or [New], what to extract from (the drawing or objects selected),
        /// the object types and the properties (their names separated by commas, or All, or ? for the list), whether identical
        /// rows are combined and the Count and Name columns shown, a column to sort by, whether the table is inserted and its
        /// title, a file to write (.xlsx, .csv or .txt) and the name of the .dxe file the settings are saved in.
        /// </summary>
        private static IEnumerable<CadPrompt> DataExtractionTyped(CadCommandContext c)
        {
            var settings = new CadExtractionSettings();
            string folder = Prefix(c.Document);
            string title = Path.GetFileNameWithoutExtension(c.Document.Title ?? "Drawing");
            string loaded = null;

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(
                    "Enter name of an existing data extraction file or",
                    new[] { "New" },
                    "New"
                );

                if (c.Input.Kind != CadInputKind.Text)
                {
                    if (c.Input.Kind == CadInputKind.Keyword || c.Input.IsNone)
                    {
                        break;
                    }

                    yield break;
                }

                string path = ExtractPath(c.Document, c.Input.Text.Trim().Trim('"'), ".dxe");

                if (!File.Exists(path))
                {
                    c.Write("\"" + Path.GetFileName(path) + "\": Can't find file.");
                    continue;
                }

                try
                {
                    settings = CadExtractionSettings.Load(path);
                    loaded = path;
                }
                catch (Exception exception)
                    when (exception is InvalidDataException
                        || exception is IOException
                        || exception is UnauthorizedAccessException
                    )
                {
                    c.Write("Cannot read \"" + path + "\": " + exception.Message);
                    continue;
                }

                break;
            }

            if (loaded == null)
            {
                yield return CadPrompt.ForKeyword("Extract from", new[] { "Drawing", "Selection" }, "Drawing");

                if (c.Input.Is("Selection"))
                {
                    yield return CadPrompt.ForSelection("Select objects");
                    settings.SelectedHandles = (c.Input.Handles ?? Array.Empty<string>()).ToList();
                }
            }

            IReadOnlyList<CadExtractionType> types = CadExtraction.Types(c.Document, settings, out _);

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(
                    "Enter the object types to extract, separated by commas, or",
                    new[] { "All", "?" },
                    settings.SelectedTypes == null ? "All" : string.Join(",", settings.SelectedTypes)
                );

                if (c.Input.Is("All"))
                {
                    settings.SelectedTypes = null;
                    break;
                }

                if (c.Input.Is("?"))
                {
                    foreach (CadExtractionType type in types)
                    {
                        c.Write("  " + type.Name + " (" + type.Count.ToString(CultureInfo.InvariantCulture) + ")");
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    break;
                }

                var chosen = new List<string>();
                string unknown = null;

                foreach (string word in c.Input.Text.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0))
                {
                    CadExtractionType found = types.FirstOrDefault(t =>
                        string.Equals(t.Name, word, StringComparison.OrdinalIgnoreCase)
                    );

                    if (found == null)
                    {
                        unknown = word;
                        break;
                    }

                    chosen.Add(found.Name);
                }

                if (unknown != null)
                {
                    c.Write("Object type \"" + unknown + "\" not found.");
                    continue;
                }

                settings.SelectedTypes = chosen;
                break;
            }

            IReadOnlyList<CadExtractionProperty> properties = CadExtraction.Properties(c.Document, settings);

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(
                    "Enter the properties to extract, separated by commas, or",
                    new[] { "All", "?" },
                    settings.SelectedProperties == null ? "All" : string.Join(",", settings.SelectedProperties)
                );

                if (c.Input.Is("All"))
                {
                    settings.SelectedProperties = null;
                    break;
                }

                if (c.Input.Is("?"))
                {
                    foreach (CadExtractionProperty property in properties)
                    {
                        c.Write("  " + property.Key);
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    break;
                }

                var chosen = new List<string>();
                string unknown = null;

                foreach (string word in c.Input.Text.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0))
                {
                    CadExtractionProperty found = properties.FirstOrDefault(p =>
                        string.Equals(p.Key, word, StringComparison.OrdinalIgnoreCase)
                    )
                        ?? properties.FirstOrDefault(p =>
                            string.Equals(p.Name, word, StringComparison.OrdinalIgnoreCase)
                        );

                    if (found == null)
                    {
                        unknown = word;
                        break;
                    }

                    chosen.Add(found.Key);
                }

                if (unknown != null)
                {
                    c.Write("Property \"" + unknown + "\" not found.");
                    continue;
                }

                settings.SelectedProperties = chosen;
                break;
            }

            string Yes(bool on) => on ? "Yes" : "No";
            yield return CadPrompt.ForKeyword(
                "Combine identical rows",
                new[] { "Yes", "No" },
                Yes(settings.CombineIdenticalRows)
            );
            settings.CombineIdenticalRows = c.Input.Is("Yes") || (c.Input.IsNone && settings.CombineIdenticalRows);
            yield return CadPrompt.ForKeyword("Show Count column", new[] { "Yes", "No" }, Yes(settings.ShowCount));
            settings.ShowCount = c.Input.Is("Yes") || (c.Input.IsNone && settings.ShowCount);
            yield return CadPrompt.ForKeyword("Show Name column", new[] { "Yes", "No" }, Yes(settings.ShowName));
            settings.ShowName = c.Input.Is("Yes") || (c.Input.IsNone && settings.ShowName);

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(
                    "Enter the column to sort by (Count, Name or a property) or",
                    new[] { "None" },
                    "None"
                );

                if (c.Input.Kind != CadInputKind.Text)
                {
                    break;
                }

                string word = c.Input.Text.Trim();
                string key =
                    string.Equals(word, "Count", StringComparison.OrdinalIgnoreCase) ? "Count"
                    : string.Equals(word, "Name", StringComparison.OrdinalIgnoreCase) ? "Name"
                    : (
                        properties.FirstOrDefault(p => string.Equals(p.Key, word, StringComparison.OrdinalIgnoreCase))
                        ?? properties.FirstOrDefault(p =>
                            string.Equals(p.Name, word, StringComparison.OrdinalIgnoreCase)
                        )
                    )?.Key;

                if (key == null)
                {
                    c.Write("Column \"" + word + "\" not found.");
                    continue;
                }

                yield return CadPrompt.ForKeyword("Sort in descending order", new[] { "Yes", "No" }, "No");
                settings.Sorts.Clear();
                settings.Sorts.Add(new CadExtractionSort(key, c.Input.Is("Yes")));
                break;
            }

            yield return CadPrompt.ForKeyword(
                "Insert data extraction table into drawing",
                new[] { "Yes", "No" },
                Yes(settings.InsertTable)
            );
            settings.InsertTable = c.Input.Is("Yes") || (c.Input.IsNone && settings.InsertTable);

            if (settings.InsertTable)
            {
                yield return CadPrompt.ForString("Enter a title for the table", settings.Title);
                settings.Title = Typed(c, settings.Title ?? string.Empty);
            }

            yield return CadPrompt.ForKeywordOrText(
                "Enter an output file name (.xlsx, .csv, .txt) or",
                new[] { "None" },
                "None"
            );

            if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0)
            {
                settings.OutputToFile = true;
                settings.OutputFile = ExtractPath(c.Document, c.Input.Text.Trim().Trim('"'), ".xlsx");
            }

            string offered = loaded ?? ExtractPath(c.Document, title, ".dxe");

            while (true)
            {
                yield return CadPrompt.ForString("Enter a name for the data extraction file", offered);
                string typed = Typed(c, offered).Trim('"');

                if (typed.Length == 0 || typed.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    c.Write("Invalid file name.");
                    continue;
                }

                offered = ExtractPath(c.Document, typed, ".dxe");
                break;
            }

            foreach (CadPrompt prompt in FinishExtraction(c, settings, offered))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// Finishes a data extraction: the settings saved to the .dxe file, the extraction run, the file written where one is
        /// asked for, and the table inserted at the point asked, its cells linked to the extraction's data link. "n objects
        /// extracted." is said; a file that cannot be written is said and stops nothing else.
        /// </summary>
        private static IEnumerable<CadPrompt> FinishExtraction(
            CadCommandContext c,
            CadExtractionSettings settings,
            string dxe
        )
        {
            string file = (dxe ?? string.Empty).Trim().Trim('"');

            if (file.Length == 0)
            {
                c.Write("Enter a name for the data extraction file.");
                yield break;
            }

            file = ExtractPath(c.Document, file, ".dxe");

            try
            {
                settings.Save(file);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                c.Write("Error writing file \"" + file + "\".");
                yield break;
            }

            CadExtractionResult result = CadExtraction.Run(c.Document, settings);

            foreach (string missing in result.MissingFiles)
            {
                c.Write("The drawing \"" + missing + "\" could not be opened.");
            }

            c.Write(result.Objects.ToString(CultureInfo.InvariantCulture) + " objects extracted.");

            if (settings.OutputToFile && (settings.OutputFile ?? string.Empty).Trim().Length > 0)
            {
                try
                {
                    CadExtraction.WriteFile(settings.OutputFile, result.Sheet);
                }
                catch (Exception exception)
                    when (exception is IOException
                        || exception is UnauthorizedAccessException
                        || exception is NotSupportedException
                        || exception is ArgumentException
                    )
                {
                    c.Write(
                        exception is NotSupportedException
                            ? exception.Message
                            : "Error writing file \"" + settings.OutputFile + "\"."
                    );
                }
            }

            if (!settings.InsertTable)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify insertion point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            CadSheet cells = CadExtraction.TableCells(settings.Title, result.Sheet);
            string folder = CadDataLinks.FolderOf(c.Document);
            string style = string.IsNullOrWhiteSpace(settings.TableStyle)
                ? CadTableStyleEditor.Current(c.Database)
                : settings.TableStyle;
            bool linked = CadDataLinks.CanHold(c.Document);
            MakeDataTable(
                c,
                style,
                c.Input.Point,
                cells,
                1,
                linked
                    ? (Func<DxfDatabase, DxfDataLink>)(database => CadExtraction.LinkFor(database, file, folder))
                    : null,
                true
            );

            if (!linked)
            {
                c.Write("The drawing's format has no data links: the table was inserted without one.");
            }
        }

        #endregion
    }
}
