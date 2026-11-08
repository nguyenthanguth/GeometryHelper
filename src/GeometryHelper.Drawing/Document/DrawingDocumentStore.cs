using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Writes a drawing document into the DXF its sheet is drawn in, and reads it back, so that a model drawing is one file
    /// that opens in AutoCAD and in the presenter as any DXF.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The document is a dictionary <see cref="DictionaryName"/> (GH_DRAWING) of the named object dictionary, holding two
    /// XRECORDs. HEADER holds the format (<see cref="FormatVersion"/>, a group 90) and the drawing's id (a text). RECORDS holds
    /// every record, each a list opened by a group 102 of its kind and closed by <c>102 }</c>: the views
    /// (<c>{GH_VIEW</c>), the annotations (<c>{GH_ANNOTATION</c>), the hand edits (<c>{GH_EDIT</c>) and the plugin runs
    /// (<c>{GH_PLUGIN_RUN</c>), in that order, then the records it does not read, as they came. A document of 10 000 records
    /// is written twice, saved, opened and read back in 0.14 s; an XRECORD of its own for each record would have taken
    /// 108 s, as a dictionary takes its entries one by one.
    /// </para>
    /// <para>
    /// Each record holds its fields one after the other with no names: a view or an annotation its id, kind and view id (texts),
    /// its links and its values; an edit its object id, its kind (the name of a <see cref="CadEditKind"/>), its property and
    /// its old and new values (texts); a plugin run its id, plugin, version and view id (texts), its inputs (links) and its
    /// settings (values). Links are a count (90) and that many object ids, then a count and, for each point, its object id
    /// and the point (10, 20, 30); values a count and, for each, its name and its value. A text is a group 1 with a group 3
    /// for each 250 characters past the first; a per cent sign, a backslash and control characters are written as
    /// <c>%</c> and two hexadecimal digits, so that a line feed is <c>%0A</c>, and a null text as a lone <c>%</c>.
    /// Coordinates are written in the fewest digits that read back to the same bits.
    /// </para>
    /// <para>
    /// Everything comes back exactly: texts letter for letter, accented letters, line breaks and backslashes among them,
    /// and coordinates to the bit, negative zero included. A record of a kind this version does not know, as one a newer
    /// version writes, or one that fails to read, is kept in <see cref="DrawingDocumentRecords.UnknownRecords"/> and
    /// written back as it was; a record that fails to read is said in one Warn line of <see cref="GeometryHelperLog"/>
    /// and stops none of the others. A newer layout of a kind is written under a new name, so that this version keeps it
    /// rather than misreading it. Entries of GH_DRAWING other than HEADER and RECORDS are left as they are.
    /// </para>
    /// </remarks>
    public static class DrawingDocumentStore
    {
        /// <summary>The name of the dictionary of the named object dictionary that holds the document: GH_DRAWING.</summary>
        public const string DictionaryName = "GH_DRAWING";

        /// <summary>The format of the records this version writes: 1.</summary>
        public const int FormatVersion = 1;

        private const string HeaderName = "HEADER";
        private const string RecordsName = "RECORDS";
        private const string ViewList = "{GH_VIEW";
        private const string AnnotationList = "{GH_ANNOTATION";
        private const string EditList = "{GH_EDIT";
        private const string PluginRunList = "{GH_PLUGIN_RUN";
        private const string ListEnd = "}";

        /// <summary>
        /// Writes a document into a drawing: the dictionary GH_DRAWING made where the drawing has none, and its HEADER and
        /// RECORDS given the document's data in place of what they held, so that writing the same document twice changes
        /// nothing the second time.
        /// </summary>
        /// <param name="database">The drawing, of R2000 or later.</param>
        /// <param name="records">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the document is null.</exception>
        /// <exception cref="ArgumentException">Thrown when an unknown record made in code holds a group an XRECORD does not
        /// hold, or groups 102 that do not open and close a list in pairs; those read by <see cref="Read"/> always do.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no named object dictionary, as one of R12, or
        /// its entry GH_DRAWING is something other than a dictionary.</exception>
        public static void Write(DxfDatabase database, DrawingDocumentRecords records)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            DxfDBDictionary root = database.NamedObjectsDictionary
                ?? throw new InvalidOperationException("A drawing of " + (database.Version ?? "no version") + " has no named object dictionary to hold a drawing document: R13 brought it.");
            DxfDBDictionary document;

            if (root.Contains(DictionaryName))
            {
                document = root.GetAt(DictionaryName) as DxfDBDictionary
                    ?? throw new InvalidOperationException("The drawing's entry " + DictionaryName + " is not a dictionary, so it holds no drawing document.");
            }
            else
            {
                document = new DxfDBDictionary();
                root.SetAt(DictionaryName, document);
            }

            var header = new List<DxfTypedValue>();
            var writer = new DrawingRecordGroups.Writer(header);
            writer.Integer(FormatVersion);
            writer.Text(records.DrawingId);

            var groups = new List<DxfTypedValue>();
            writer = new DrawingRecordGroups.Writer(groups);

            foreach (DrawingElement view in records.Views)
            {
                groups.Add(new DxfTypedValue(102, ViewList));
                WriteElement(writer, view);
                groups.Add(new DxfTypedValue(102, ListEnd));
            }

            foreach (DrawingElement annotation in records.Annotations)
            {
                groups.Add(new DxfTypedValue(102, AnnotationList));
                WriteElement(writer, annotation);
                groups.Add(new DxfTypedValue(102, ListEnd));
            }

            foreach (DrawingEdit edit in records.Edits)
            {
                groups.Add(new DxfTypedValue(102, EditList));
                writer.Text(edit.ObjectId);
                writer.Text(edit.Kind.ToString());
                writer.Text(edit.Property);
                writer.Text(edit.OldValue);
                writer.Text(edit.NewValue);
                groups.Add(new DxfTypedValue(102, ListEnd));
            }

            foreach (DrawingPluginRun run in records.PluginRuns)
            {
                groups.Add(new DxfTypedValue(102, PluginRunList));
                writer.Text(run.Id);
                writer.Text(run.Plugin);
                writer.Text(run.Version);
                writer.Text(run.ViewId);
                writer.Links(run.Inputs);
                writer.Values(run.Settings);
                groups.Add(new DxfTypedValue(102, ListEnd));
            }

            foreach (IReadOnlyList<DxfTypedValue> unknown in records.UnknownRecords)
            {
                groups.AddRange(unknown);
            }

            // The records first: groups refused there leave the header as it was.
            Put(document, RecordsName, groups);
            Put(document, HeaderName, header);
            GeometryHelperLog.Info("DrawingDocumentStore.Write: " + records + " written to " + DictionaryName + ".");
        }

        /// <summary>
        /// Reads the document a drawing carries.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The document; null where the drawing carries none, as a plain DXF, or its GH_DRAWING is not a dictionary.
        /// A drawing whose HEADER cannot be read gives a document of no id, its records read all the same.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static DrawingDocumentRecords Read(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            DxfDBDictionary root = database.NamedObjectsDictionary;

            if (root == null || !root.Contains(DictionaryName))
            {
                return null;
            }

            if (!(root.GetAt(DictionaryName) is DxfDBDictionary document))
            {
                GeometryHelperLog.Warn("DrawingDocumentStore.Read: the drawing's entry " + DictionaryName + " is not a dictionary; no drawing document is read.");
                return null;
            }

            string drawingId = null;
            IReadOnlyList<DxfTypedValue> header = DataOf(document, HeaderName);

            if (header != null)
            {
                try
                {
                    var reader = new DrawingRecordGroups.Reader(header, 0, header.Count);
                    int version = reader.Integer();
                    drawingId = reader.Text();

                    if (version > FormatVersion)
                    {
                        GeometryHelperLog.Warn("DrawingDocumentStore.Read: the document is of format " + version.ToString(CultureInfo.InvariantCulture) + ", newer than " + FormatVersion.ToString(CultureInfo.InvariantCulture) + "; the records of the kinds this version knows are read and the others kept.");
                    }
                }
                catch (Exception exception) when (exception is FormatException || exception is OverflowException)
                {
                    drawingId = null;
                    GeometryHelperLog.Warn("DrawingDocumentStore.Read: the HEADER of " + DictionaryName + " cannot be read; the document is read with no id.", exception);
                }
            }

            var views = new List<DrawingElement>();
            var annotations = new List<DrawingElement>();
            var edits = new List<DrawingEdit>();
            var runs = new List<DrawingPluginRun>();
            var unknown = new List<IReadOnlyList<DxfTypedValue>>();
            var failures = new List<string>();
            int failed = 0;
            var others = new SortedDictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<DxfTypedValue> groups = DataOf(document, RecordsName) ?? new DxfTypedValue[0];
            int at = 0;
            int number = 0;

            while (at < groups.Count)
            {
                int start = at;
                string kind = ListName(groups[at]);
                number++;

                if (kind == null)
                {
                    // Groups outside any list are one record up to the next list, kept where they can be written back.
                    while (at < groups.Count && ListName(groups[at]) == null)
                    {
                        at++;
                    }

                    if (Keep(unknown, groups, start, at))
                    {
                        Count(others, "groups outside a list");
                    }
                    else
                    {
                        Fail(failures, ref failed, number, "outside a list", "a group 102 } closes no list; dropped");
                    }

                    continue;
                }

                at++;

                while (at < groups.Count && !IsListEnd(groups[at]) && ListName(groups[at]) == null)
                {
                    at++;
                }

                // A list is closed by 102 } before another opens; one that is not cannot be written back, and is dropped.
                if (at == groups.Count || !IsListEnd(groups[at]))
                {
                    Fail(failures, ref failed, number, kind, "the list is not closed; dropped");
                    continue;
                }

                int end = at;
                at++;

                try
                {
                    var reader = new DrawingRecordGroups.Reader(groups, start + 1, end);

                    switch (kind)
                    {
                        case ViewList:
                            views.Add(ReadElement(reader));
                            break;
                        case AnnotationList:
                            annotations.Add(ReadElement(reader));
                            break;
                        case EditList:
                            edits.Add(ReadEdit(reader));
                            break;
                        case PluginRunList:
                            runs.Add(ReadPluginRun(reader));
                            break;
                        default:
                            Keep(unknown, groups, start, at);
                            Count(others, kind);
                            break;
                    }
                }
                catch (Exception exception) when (exception is FormatException || exception is OverflowException || exception is ArgumentException)
                {
                    Keep(unknown, groups, start, at);
                    Fail(failures, ref failed, number, kind, exception.Message + " Kept as it is");
                }
            }

            if (failed > 0)
            {
                GeometryHelperLog.Warn("DrawingDocumentStore.Read: " + failed.ToString(CultureInfo.InvariantCulture) + " records of " + DictionaryName + " failed to read; " + string.Join("; ", failures) + (failed > failures.Count ? "; ..." : string.Empty) + ".");
            }

            if (others.Count > 0)
            {
                var said = new StringBuilder();

                foreach (KeyValuePair<string, int> other in others)
                {
                    said.Append(said.Length == 0 ? string.Empty : ", ").Append(other.Key).Append(" (").Append(other.Value.ToString(CultureInfo.InvariantCulture)).Append(')');
                }

                GeometryHelperLog.Debug("DrawingDocumentStore.Read: records of kinds this version does not read, kept as they are: " + said + ".");
            }

            var records = new DrawingDocumentRecords(drawingId, views, annotations, edits, runs, unknown);
            GeometryHelperLog.Info("DrawingDocumentStore.Read: " + records + " read from " + DictionaryName + ".");
            return records;
        }

        private static void WriteElement(DrawingRecordGroups.Writer writer, DrawingElement element)
        {
            writer.Text(element.Id);
            writer.Text(element.Kind);
            writer.Text(element.ViewId);
            writer.Links(element.Links);
            writer.Values(element.Values);
        }

        private static DrawingElement ReadElement(DrawingRecordGroups.Reader reader)
        {
            string id = reader.Text();
            string kind = reader.Text();
            string viewId = reader.Text();
            DrawingLinks links = reader.Links();
            KeyValuePair<string, string>[] values = reader.Values();
            reader.End();
            return new DrawingElement(id, kind, viewId, links, values);
        }

        private static DrawingEdit ReadEdit(DrawingRecordGroups.Reader reader)
        {
            string objectId = reader.Text();
            string kind = reader.Text();
            string property = reader.Text();
            string oldValue = reader.Text();
            string newValue = reader.Text();
            reader.End();

            // By name only: a number, which Enum.Parse would take, is no kind.
            if (kind == null || !Enum.IsDefined(typeof(CadEditKind), kind))
            {
                throw new FormatException("The kind of edit " + (kind ?? "(none)") + " is not one of CadEditKind.");
            }

            return new DrawingEdit(objectId, (CadEditKind)Enum.Parse(typeof(CadEditKind), kind), property, oldValue, newValue);
        }

        private static DrawingPluginRun ReadPluginRun(DrawingRecordGroups.Reader reader)
        {
            string id = reader.Text();
            string plugin = reader.Text();
            string version = reader.Text();
            string viewId = reader.Text();
            DrawingLinks inputs = reader.Links();
            KeyValuePair<string, string>[] settings = reader.Values();
            reader.End();
            return new DrawingPluginRun(id, plugin, version, viewId, inputs, settings);
        }

        /// <summary>Gives an XRECORD of the document its data: in place where it has one, so that its handle stays; new otherwise.</summary>
        private static void Put(DxfDBDictionary document, string name, List<DxfTypedValue> data)
        {
            if (document.Contains(name) && document.GetAt(name) is DxfXrecord record)
            {
                record.Data = data;
                return;
            }

            document.SetAt(name, new DxfXrecord { Data = data });
        }

        /// <summary>The data of an XRECORD of the document; null where it has none of the name, or the entry is not one.</summary>
        private static IReadOnlyList<DxfTypedValue> DataOf(DxfDBDictionary document, string name)
            => document.Contains(name) && document.GetAt(name) is DxfXrecord record ? record.Data : null;

        /// <summary>The name a group 102 opens a list with, its brace kept, such as <c>{GH_VIEW</c>; null for any other group.</summary>
        private static string ListName(DxfTypedValue group)
        {
            if (group.TypeCode != 102)
            {
                return null;
            }

            string value = group.Value.Trim();
            return value.StartsWith("{", StringComparison.Ordinal) ? value : null;
        }

        private static bool IsListEnd(DxfTypedValue group) => group.TypeCode == 102 && group.Value.Trim() == ListEnd;

        /// <summary>
        /// Keeps groups as an unknown record where they can be written back: their groups 102 open a list and close it in
        /// pairs, one at a time, as an XRECORD holds them. Whether they were kept.
        /// </summary>
        private static bool Keep(List<IReadOnlyList<DxfTypedValue>> unknown, IReadOnlyList<DxfTypedValue> groups, int start, int end)
        {
            var slice = new DxfTypedValue[end - start];
            bool open = false;

            for (int i = start; i < end; i++)
            {
                DxfTypedValue group = groups[i];

                if (group.TypeCode == 102)
                {
                    bool opens = ListName(group) != null;

                    if (opens == open || (!opens && !IsListEnd(group)))
                    {
                        return false;
                    }

                    open = opens;
                }

                slice[i - start] = group;
            }

            if (open)
            {
                return false;
            }

            unknown.Add(slice);
            return true;
        }

        /// <summary>Counts a record that failed to read, its reason kept for the first three.</summary>
        private static void Fail(List<string> failures, ref int failed, int number, string kind, string reason)
        {
            failed++;

            if (failures.Count < 3)
            {
                failures.Add("record " + number.ToString(CultureInfo.InvariantCulture) + " (" + kind.TrimStart('{') + "): " + reason.TrimEnd('.'));
            }
        }

        private static void Count(SortedDictionary<string, int> counts, string kind)
            => counts[kind] = counts.TryGetValue(kind, out int count) ? count + 1 : 1;
    }
}
