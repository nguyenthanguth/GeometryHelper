using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Turns the text of a DXF file into the text of the same drawing in another version, as AutoCAD 2021's <c>SaveAs</c> to
    /// that version does: the header variables, the groups of each record and the records themselves limited to what the
    /// version holds, and what a newer version added left out or given its older form.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AutoCAD keeps every kind of entity and object it has when it saves an older version (a drawing with 15 multileaders,
    /// 2 tables and 2 meshes saved as R2000 has 15, 2 and 2), and trims what each writes to what the version holds; so does
    /// this: a group a subclass of the version has not is taken out (<see cref="DxfVersions"/>), a pointer to a record taken
    /// out with it, and the dictionary left empty by it.
    /// </para>
    /// <para>
    /// What the version holds in another form is given that form, as AutoCAD 2021 does: the ACIS body of a solid, binary in the
    /// data store from R2013 and text in the entity before it, is turned into the other (<c>Solids</c>, and <c>Regions</c> for a
    /// region of flat faces, which is written again); the cells of a table of plain text, written in two forms on either side of
    /// R2007, from one to the other (<c>TableDown</c>, <c>TableUp</c>); the visual styles of R2007, R2010 and R2013, each in a
    /// form of its own (<c>VisualStyle</c>, with the round trip AutoCAD keeps of what the older form has no place for); and the columns of an MTEXT, in its extended data to R2013 and in an embedded object
    /// from R2018 (<c>Columns</c>, <c>ColumnsUp</c>). What cannot be given its form is left out or replaced, and said: a table
    /// whose cells hold more than their text becomes the block it is drawn with, and a body whose records the converter does not
    /// place (the splines of the ACIS 4.00 text of R2000, a solid made by a sweep) is kept as it is between the versions that hold text, and
    /// left out going to R2013 or later (AutoCAD reads a file whose entity holds text there as no drawing) and, its data being
    /// binary, going from R2013 to R2010 or before.
    /// </para>
    /// <para>
    /// R12 (AC1009) is written by rules of its own (DxfVersionConverter.R12.cs and <see cref="DxfR12Explosion"/>): what R12 lacks is
    /// first broken into what it holds, then the records are given the groups, tables and names R12 has.
    /// </para>
    /// </remarks>
    internal sealed partial class DxfVersionConverter
    {
        // The records in one place hold the 1 300 groups of AutoCAD's own drawing in some 40 MB of text: a group is a pair.
        private readonly struct Group
        {
            internal Group(int code, string value)
            {
                Code = code;
                Value = value;
            }

            internal int Code { get; }

            internal string Value { get; }
        }

        private sealed class Record
        {
            internal Record()
            {
                Groups = new List<Group>(16);
            }

            internal List<Group> Groups { get; set; }

            internal bool Dropped { get; set; }

            internal bool Lost { get; set; }

            /// <summary>Whether the ACIS data of the record was written anew for the target version (a region of flat faces).</summary>
            internal bool Rebuilt { get; set; }

            internal string Type => Groups[0].Value;
        }

        private sealed class Section
        {
            internal Section(string name)
            {
                Name = name;
                Records = new List<Record>();
            }

            internal string Name { get; }

            internal List<Record> Records { get; }
        }

        private readonly int _target;
        private readonly List<Group> _prefix = new List<Group>();
        private readonly List<Section> _sections = new List<Section>();
        private readonly HashSet<string> _dropped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SortedDictionary<string, int> _left = new SortedDictionary<string, int>(
            StringComparer.Ordinal
        );
        private readonly SortedDictionary<string, int> _replaced = new SortedDictionary<string, int>(
            StringComparer.Ordinal
        );

        // What goes with what was left out or replaced, which is not told: what an owner owns.
        private readonly SortedDictionary<string, int> _quiet = new SortedDictionary<string, int>(
            StringComparer.Ordinal
        );
        private readonly List<string> _owning = new List<string>();
        private string _lineEnd = "\r\n";
        private bool _padded = true;
        private int _source;

        private DxfVersionConverter(int target)
        {
            _target = target;
        }

        /// <summary>
        /// Gives the text of a DXF file in another version.
        /// </summary>
        /// <param name="text">The text of the file, of R2000 to R2018.</param>
        /// <param name="target">The version, as 1015 for AC1015.</param>
        /// <param name="said">Where what was left out or replaced is told, a sentence each; null for nowhere.</param>
        /// <param name="fontSearchPath">The folders of the fonts an MTEXT broken into texts for R12 is laid out in, as <see cref="DxfDatabase.FontSearchPath"/>; null for none.</param>
        /// <returns>The text, in the line ends and the padding of the codes of the text given.</returns>
        /// <exception cref="NotSupportedException">Thrown when the file is older than R2000, or names no version.</exception>
        /// <exception cref="FormatException">Thrown when the text is not groups of a code and a value.</exception>
        internal static string Convert(string text, int target, IList<string> said, string fontSearchPath = null)
        {
            var converter = new DxfVersionConverter(target);

            if (target == DxfVersions.R12)
            {
                text = converter.R12Break(text, fontSearchPath);
            }

            converter.Read(text);
            converter.Run();

            if (said != null)
            {
                converter.Say(said);
            }

            return converter.Write();
        }

        #region Reading and writing

        /// <summary>Reads the groups of the text into sections of records: those of the header by their group 9, the others by their 0.</summary>
        private void Read(string text)
        {
            int at = 0;
            int lineNumber = 0;
            Section section = null;
            Record record = null;
            bool header = false;
            bool first = true;

            while (true)
            {
                string codeLine = NextLine(text, ref at);

                if (codeLine == null)
                {
                    break;
                }

                string value = NextLine(text, ref at);
                lineNumber += 2;

                if (first)
                {
                    first = false;
                    _lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
                    _padded = codeLine.Length > 0 && codeLine[0] == ' ';
                }

                if (
                    value == null
                    || !int.TryParse(codeLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code)
                )
                {
                    throw new FormatException(
                        "Line " + (lineNumber - 1).ToString(CultureInfo.InvariantCulture) + " is not a group code."
                    );
                }

                if (code == 0 && value == "SECTION")
                {
                    string nameLine = NextLine(text, ref at);
                    string name = NextLine(text, ref at);
                    lineNumber += 2;

                    if (name == null || nameLine == null)
                    {
                        throw new FormatException("A section has no name.");
                    }

                    section = new Section(name);
                    _sections.Add(section);
                    header = name == "HEADER";
                    record = null;
                    continue;
                }

                if (code == 0 && value == "ENDSEC")
                {
                    section = null;
                    record = null;
                    continue;
                }

                if (code == 0 && value == "EOF")
                {
                    break;
                }

                if (section == null)
                {
                    _prefix.Add(new Group(code, value));
                    continue;
                }

                if (record == null || (header ? code == 9 : code == 0))
                {
                    record = new Record();
                    section.Records.Add(record);
                }

                record.Groups.Add(new Group(code, value));
            }
        }

        private static string NextLine(string text, ref int at)
        {
            if (at >= text.Length)
            {
                return null;
            }

            int end = text.IndexOf('\n', at);
            int next = end < 0 ? text.Length : end + 1;

            if (end < 0)
            {
                end = text.Length;
            }

            int stop = end > at && text[end - 1] == '\r' ? end - 1 : end;
            string line = text.Substring(at, stop - at);
            at = next;
            return line;
        }

        /// <summary>The text of the sections, as AutoCAD lays a file out: each section between its SECTION and ENDSEC, the file closed by EOF.</summary>
        private string Write()
        {
            var text = new StringBuilder(1 << 16);

            foreach (Group group in _prefix)
            {
                Append(text, group);
            }

            foreach (Section section in _sections)
            {
                Append(text, 0, "SECTION");
                Append(text, 2, section.Name);

                foreach (Record record in section.Records)
                {
                    if (record.Dropped)
                    {
                        continue;
                    }

                    foreach (Group group in record.Groups)
                    {
                        Append(text, group);
                    }
                }

                Append(text, 0, "ENDSEC");
            }

            Append(text, 0, "EOF");
            return text.ToString();
        }

        private void Append(StringBuilder text, Group group) => Append(text, group.Code, group.Value);

        private void Append(StringBuilder text, int code, string value)
        {
            string number = code.ToString(CultureInfo.InvariantCulture);

            if (_padded)
            {
                text.Append(' ', Math.Max(0, 3 - number.Length));
            }

            text.Append(number).Append(_lineEnd).Append(value).Append(_lineEnd);
        }

        /// <summary>A whole number given to a group as AutoCAD writes it: the width of its range of codes where the file pads.</summary>
        private Group Whole(int code, int value)
        {
            string number = value.ToString(CultureInfo.InvariantCulture);
            int width =
                !_padded ? 0
                : (code >= 90 && code <= 99) || code == 1071 ? 9
                : 6;
            return new Group(code, number.PadLeft(width));
        }

        #endregion

        #region The run

        private void Run()
        {
            if (_target == DxfVersions.R12)
            {
                RunR12();
                return;
            }

            Section header = Find("HEADER");
            _source = header == null ? -1 : VersionOf(header);

            if (_source < 1015)
            {
                throw new NotSupportedException(
                    "A drawing of R12 to R14 is saved as it is: its records are not those of R2000 and later."
                );
            }

            ConvertHeader(header);
            Regions();
            Solids();
            Plan();
            Own();

            // The data store of the file holds the bodies of R2013 and later, binary; no version before has it.
            if (_target < 1027)
            {
                _sections.RemoveAll(s => s.Name == "ACDSDATA");
            }

            foreach (string name in new[] { "TABLES", "BLOCKS", "ENTITIES", "OBJECTS" })
            {
                Section section = Find(name);

                if (section == null)
                {
                    continue;
                }

                foreach (Record record in section.Records)
                {
                    if (!record.Dropped)
                    {
                        Reform(record);
                    }
                }
            }

            // The objects made on the way (the round trip of a style going down) join the others.
            Find("OBJECTS")?.Records.AddRange(_added);
            Clean();
            Classes();
        }

        private Section Find(string name)
        {
            foreach (Section section in _sections)
            {
                if (section.Name == name)
                {
                    return section;
                }
            }

            return null;
        }

        /// <summary>The number of the version a header names (<c>$ACADVER</c>); -1 where it names none.</summary>
        private static int VersionOf(Section header)
        {
            foreach (Record record in header.Records)
            {
                if (record.Groups[0].Value == "$ACADVER" && record.Groups.Count > 1)
                {
                    return DxfVersions.Number(record.Groups[1].Value);
                }
            }

            return -1;
        }

        /// <summary>The header with the variables the version has not left out and its <c>$ACADVER</c> the version's.</summary>
        private void ConvertHeader(Section header)
        {
            var kept = new List<Record>(header.Records.Count);

            foreach (Record record in header.Records)
            {
                string name = record.Groups[0].Value;

                if (name == "$ACADVER" && record.Groups.Count > 1)
                {
                    record.Groups[1] = new Group(record.Groups[1].Code, DxfVersions.Name(_target));
                }
                else if (name == "$ACADMAINTVER" && record.Groups.Count > 1)
                {
                    // The maintenance version of the drawing's format, as AutoCAD 2021 writes it for each: a 90 from R2018 on.
                    record.Groups[1] =
                        _target >= 1032
                            ? Whole(90, DxfVersions.Maintenance(_target))
                            : Whole(70, DxfVersions.Maintenance(_target));
                }
                else if (name == "$XCLIPFRAME" && record.Groups.Count > 1)
                {
                    // Whether the frame of an xref clip shows: a flag (290) before R2010, a number (280) of nought, one or two
                    // from then on, where AutoCAD writes the frame that shows and does not plot, 2, as nought before it.
                    Number(record.Groups[1].Value, out int frame);
                    record.Groups[1] = _target >= 1024 ? Whole(280, frame) : Whole(290, frame == 1 ? 1 : 0);
                }

                if (DxfVersions.SinceOfVariable(name) <= _target)
                {
                    kept.Add(record);
                }
            }

            header.Records.Clear();
            header.Records.AddRange(kept);
        }

        #endregion

        #region What goes

        // The kinds of entity AutoCAD writes an ACIS body in: its own text up to R2010, binary from R2013.
        private static readonly HashSet<string> Bodies = new HashSet<string>(StringComparer.Ordinal)
        {
            "3DSOLID",
            "REGION",
            "BODY",
            "SURFACE",
            "EXTRUDEDSURFACE",
            "LOFTEDSURFACE",
            "REVOLVEDSURFACE",
            "SWEPTSURFACE",
            "PLANESURFACE",
            "NURBSURFACE",
        };

        /// <summary>
        /// Picks the records the version cannot hold, and the tables it holds in another form: the visual styles unless the
        /// versions both are R2013 or later, whose form is one; the history of a solid before R2007 and the context data of a
        /// text before R2010; a body in binary before R2013; and a table on the other side of R2007.
        /// </summary>
        private void Plan()
        {
            bool tables = (_source >= 1021) != (_target >= 1021);

            if (_target >= 1032 && _source < 1032)
            {
                JoinColumns();
            }

            foreach (string name in new[] { "BLOCKS", "ENTITIES", "OBJECTS" })
            {
                Section section = Find(name);

                if (section == null)
                {
                    continue;
                }

                foreach (Record record in section.Records)
                {
                    string type = record.Type;

                    if (type == "VISUALSTYLE" && Style(_source) != Style(_target))
                    {
                        if (!VisualStyle(record))
                        {
                            Drop(record, _left);
                        }
                    }
                    else if (
                        (_target < 1021 && type.StartsWith("ACSH_", StringComparison.Ordinal))
                        || (_target < 1024 && type == "ACDB_TEXTOBJECTCONTEXTDATA_CLASS")
                        || (_target < 1027 && Bodies.Contains(type) && IsBinaryBody(record))
                    )
                    {
                        Drop(record, _left);
                    }
                    else if (tables && type == "ACAD_TABLE" && !(_target < 1021 ? TableDown(record) : TableUp(record)))
                    {
                        Block(record);
                    }
                }
            }
        }

        /// <summary>
        /// The form of the visual styles of a version: one for R2000 to R2007, which differ by a group, another for R2010, and a
        /// third for R2013 and R2018, in which every property is written another way.
        /// </summary>
        private static int Style(int version) =>
            version < 1024 ? 0
            : version < 1027 ? 1
            : 2;

        /// <summary>
        /// Whether the ACIS body of a record is the binary of R2013 and later, which the record does not hold: it has the
        /// body's identity (2) in the data store of the file (ACDSDATA), or binary groups (310) of its own, and none of the lines
        /// of text (1) of R2010 and before.
        /// </summary>
        private static bool IsBinaryBody(Record record)
        {
            string subclass = string.Empty;
            bool binary = false;

            foreach (Group group in record.Groups)
            {
                if (group.Code == 1001)
                {
                    break;
                }

                if (group.Code == 100)
                {
                    subclass = group.Value;
                }
                else if (subclass == "AcDbModelerGeometry")
                {
                    if (group.Code == 1)
                    {
                        return false;
                    }

                    binary = binary || group.Code == 2 || group.Code == 310;
                }
            }

            return binary;
        }

        /// <summary>Marks a record as left out, its handle with it, counted under its type in a list.</summary>
        private void Drop(Record record, IDictionary<string, int> list)
        {
            record.Dropped = true;
            string handle = HandleOf(record);

            if (handle != null)
            {
                _dropped.Add(handle);
            }

            string type = record.Type;
            list[type] = list.TryGetValue(type, out int count) ? count + 1 : 1;
        }

        /// <summary>The handle of a record (group 5, or 105 of a dimension style), read before its first subclass marker; null where it has none.</summary>
        private static string HandleOf(Record record)
        {
            for (int i = 1; i < record.Groups.Count; i++)
            {
                Group group = record.Groups[i];

                if (group.Code == 100)
                {
                    return null;
                }

                if (group.Code == 5 || group.Code == 105)
                {
                    return group.Value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// The handle of the record that owns a record: the first group 330 outside the lists of reactors and the extension
        /// dictionary (between <c>102 {</c> and <c>102 }</c>), read before its first subclass marker; null where there is none.
        /// </summary>
        private static string OwnerOf(Record record)
        {
            bool listed = false;

            for (int i = 1; i < record.Groups.Count; i++)
            {
                Group group = record.Groups[i];

                if (group.Code == 100)
                {
                    return null;
                }

                if (group.Code == 102)
                {
                    listed = group.Value.StartsWith("{", StringComparison.Ordinal);
                }
                else if (group.Code == 330 && !listed)
                {
                    return group.Value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Leaves out the objects that belong to the objects left out, those of a dictionary and the dictionaries below it, and
        /// so on down, as AutoCAD erases what an owner owns with it.
        /// </summary>
        private void Own()
        {
            Section objects = Find("OBJECTS");

            if (objects == null || (_dropped.Count == 0 && _owning.Count == 0))
            {
                return;
            }

            var owned = new Dictionary<string, List<Record>>(StringComparer.OrdinalIgnoreCase);
            var named = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);

            foreach (Record record in objects.Records)
            {
                string handle = HandleOf(record);

                if (handle != null)
                {
                    named[handle] = record;
                }

                string owner = OwnerOf(record);

                if (owner == null)
                {
                    continue;
                }

                if (!owned.TryGetValue(owner, out List<Record> list))
                {
                    owned[owner] = list = new List<Record>();
                }

                list.Add(record);
            }

            var pending = new Queue<string>(_dropped);

            // The extension dictionaries of the tables made blocks go with what they hold.
            foreach (string handle in _owning)
            {
                if (named.TryGetValue(handle, out Record dictionary) && !dictionary.Dropped)
                {
                    Drop(dictionary, _quiet);
                    pending.Enqueue(handle);
                }
            }

            while (pending.Count > 0)
            {
                if (!owned.TryGetValue(pending.Dequeue(), out List<Record> list))
                {
                    continue;
                }

                foreach (Record record in list)
                {
                    if (!record.Dropped)
                    {
                        Drop(record, _quiet);
                        string handle = HandleOf(record);

                        if (handle != null)
                        {
                            pending.Enqueue(handle);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Takes the pointers to the records left out out of every record, and the dictionaries they leave empty with the
        /// pointers to those, until nothing points at what is gone.
        /// </summary>
        private void Clean()
        {
            if (_dropped.Count == 0)
            {
                return;
            }

            var gone = new HashSet<string>(_dropped, StringComparer.OrdinalIgnoreCase);

            while (gone.Count > 0)
            {
                var next = new List<Record>();

                foreach (Section section in _sections)
                {
                    if (section.Name == "HEADER" || section.Name == "CLASSES")
                    {
                        continue;
                    }

                    foreach (Record record in section.Records)
                    {
                        if (!record.Dropped && Unpoint(record, gone) && IsEmptyDictionary(record))
                        {
                            next.Add(record);
                        }
                    }
                }

                gone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (Record record in next)
                {
                    string handle = HandleOf(record);

                    if (handle != null && OwnerOf(record) != "0")
                    {
                        Drop(record, _quiet);
                        gone.Add(handle);
                    }
                }
            }
        }

        /// <summary>Whether a record is a dictionary with no entry left.</summary>
        private static bool IsEmptyDictionary(Record record)
        {
            if (record.Type != "DICTIONARY")
            {
                return false;
            }

            foreach (Group group in record.Groups)
            {
                if (group.Code == 3)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Takes the pointers to records that are gone out of a record: a group of the codes of a pointer (330 to 369, 390 to
        /// 399, 480, 481) naming one, with the name before it in a dictionary, and the whole of the list of the extension
        /// dictionary where its one pointer goes. True where it left a dictionary with nothing in it.
        /// </summary>
        private bool Unpoint(Record record, HashSet<string> gone)
        {
            List<Group> groups = record.Groups;
            List<Group> kept = null;
            bool data = false;
            bool dictionary = record.Type == "DICTIONARY" || record.Type == "ACDBDICTIONARYWDFLT";

            for (int i = 1; i < groups.Count; i++)
            {
                Group group = groups[i];
                data = data || group.Code == 1001;

                if (data || !IsPointer(group.Code) || !gone.Contains(group.Value.Trim()))
                {
                    kept?.Add(group);
                    continue;
                }

                if (kept == null)
                {
                    kept = groups.GetRange(0, i);
                }

                Group before = kept[kept.Count - 1];

                if (dictionary && (group.Code == 350 || group.Code == 360) && before.Code == 3)
                {
                    // An entry of a dictionary: its name (3) before its pointer goes with it.
                    kept.RemoveAt(kept.Count - 1);
                    record.Lost = true;
                }
                else if (
                    group.Code == 360
                    && before.Code == 102
                    && before.Value == "{ACAD_XDICTIONARY"
                    && i + 1 < groups.Count
                    && groups[i + 1].Code == 102
                    && groups[i + 1].Value == "}"
                )
                {
                    // The extension dictionary has the one pointer: the list goes whole.
                    kept.RemoveAt(kept.Count - 1);
                    i++;
                }
            }

            if (kept != null)
            {
                record.Groups = kept;
            }

            return record.Lost;
        }

        private static bool IsPointer(int code) =>
            (code >= 330 && code <= 369) || (code >= 390 && code <= 399) || code == 480 || code == 481;

        #endregion

        #region The classes

        /// <summary>
        /// The classes with how many objects of each the file has, from R2004 on (91), as AutoCAD counts them, which it opens no
        /// drawing without; none before.
        /// </summary>
        private void Classes()
        {
            Section classes = Find("CLASSES");

            if (classes == null)
            {
                return;
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string name in new[] { "BLOCKS", "ENTITIES", "OBJECTS" })
            {
                Section section = Find(name);

                if (section == null)
                {
                    continue;
                }

                foreach (Record record in section.Records)
                {
                    if (!record.Dropped)
                    {
                        string type = record.Type;
                        counts[type] = counts.TryGetValue(type, out int count) ? count + 1 : 1;
                    }
                }
            }

            // R2000 lists the classes of its layouts and place holders; R2004 and later have them built in.
            var builtIn = new[] { new[] { "ACDBPLACEHOLDER", "AcDbPlaceHolder" }, new[] { "LAYOUT", "AcDbLayout" } };

            foreach (string[] kind in builtIn)
            {
                int listed = classes.Records.FindIndex(r => r.Groups.Exists(g => g.Code == 1 && g.Value == kind[0]));

                if (_target >= 1018 && listed >= 0)
                {
                    classes.Records.RemoveAt(listed);
                }
                else if (_target < 1018 && listed < 0 && counts.ContainsKey(kind[0]))
                {
                    var made = new Record();
                    made.Groups.Add(new Group(0, "CLASS"));
                    made.Groups.Add(new Group(1, kind[0]));
                    made.Groups.Add(new Group(2, kind[1]));
                    made.Groups.Add(new Group(3, "ObjectDBX Classes"));
                    made.Groups.Add(Whole(90, 0));
                    made.Groups.Add(Whole(280, 0));
                    made.Groups.Add(Whole(281, 0));
                    classes.Records.Add(made);
                }
            }

            // The data tables made for the cells of a table going to R2000 or R2004 need their class.
            string dataTable = _target < 1018 ? "ACDBDATATABLE" : "DATATABLE";

            if (
                counts.ContainsKey(dataTable)
                && !classes.Records.Exists(r =>
                    r.Groups.Exists(g => g.Code == 1 && (g.Value == "ACDBDATATABLE" || g.Value == "DATATABLE"))
                )
            )
            {
                var made = new Record();
                made.Groups.Add(new Group(0, "CLASS"));
                made.Groups.Add(new Group(1, dataTable));
                made.Groups.Add(new Group(2, "AcDbDataTable"));
                made.Groups.Add(new Group(3, "ObjectDBX Classes"));
                made.Groups.Add(Whole(90, 0));
                made.Groups.Add(Whole(280, 0));
                made.Groups.Add(Whole(281, 0));
                classes.Records.Add(made);
            }

            foreach (Record record in classes.Records)
            {
                if (record.Dropped)
                {
                    continue;
                }

                string dxfName = null;
                int counted = -1;
                int proxy = -1;

                for (int i = 0; i < record.Groups.Count; i++)
                {
                    Group group = record.Groups[i];

                    if (group.Code == 1)
                    {
                        dxfName = group.Value;

                        // The class of a table's round trip is named another way in R2000 than from R2004.
                        if (dxfName == "ACDBDATATABLE" || dxfName == "DATATABLE")
                        {
                            dxfName = _target < 1018 ? "ACDBDATATABLE" : "DATATABLE";
                            record.Groups[i] = new Group(1, dxfName);
                        }
                    }
                    else if (group.Code == 90)
                    {
                        proxy = i;
                    }
                    else if (group.Code == 91)
                    {
                        counted = i;
                    }
                }

                if (_target < 1018)
                {
                    if (counted >= 0)
                    {
                        record.Groups.RemoveAt(counted);
                    }

                    continue;
                }

                counts.TryGetValue(dxfName ?? string.Empty, out int total);
                Group made = Whole(91, total);

                if (counted >= 0)
                {
                    record.Groups[counted] = made;
                }
                else
                {
                    record.Groups.Insert(proxy >= 0 ? proxy + 1 : record.Groups.Count, made);
                }
            }
        }

        #endregion

        #region What was said

        /// <summary>Tells what was left out and what was replaced, a sentence each, the kinds in order.</summary>
        private void Say(IList<string> said)
        {
            if (_left.Count > 0)
            {
                said.Add(
                    "Left out of the drawing, which "
                        + DxfVersions.Name(_target)
                        + " cannot hold as it is: "
                        + string.Join(", ", Counted(_left))
                        + "."
                );
            }

            if (_broken.Count > 0)
            {
                said.Add(
                    "Written as the lines, texts and polylines each breaks into, which "
                        + DxfVersions.Name(_target)
                        + " holds: "
                        + string.Join(", ", Counted(_broken))
                        + "."
                );
            }

            if (_plan != null && _plan.Renamed.Count > 0)
            {
                said.Add(
                    "Dynamic blocks, which "
                        + DxfVersions.Name(_target)
                        + " does not have, are written as the anonymous blocks they are drawn with: "
                        + string.Join(
                            ", ",
                            _plan
                                .Renamed.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
                                .Select(r => r.Key + " as " + r.Value)
                        )
                        + "."
                );
            }

            if (_plan != null && _plan.Skipped.Count > 0)
            {
                said.Add(
                    "Edges that are splines, which the lines of a REGION or a 3DSOLID in "
                        + DxfVersions.Name(_target)
                        + " do not hold, are left out of them: "
                        + string.Join(", ", Counted(_plan.Skipped))
                        + "."
                );
            }

            if (_replaced.Count > 0)
            {
                said.Add(
                    "Replaced by the block each is drawn with, written as "
                        + DxfVersions.Name(_target)
                        + " cannot write it: "
                        + string.Join(", ", Counted(_replaced))
                        + "."
                );
            }
        }

        private static IEnumerable<string> Counted(IDictionary<string, int> list)
        {
            foreach (KeyValuePair<string, int> pair in list)
            {
                yield return pair.Key + " " + pair.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        #endregion
    }
}
