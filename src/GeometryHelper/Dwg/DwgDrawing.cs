using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// A DXF file taken apart for the DWG writer: its header variables, its classes, its tables with their records, its
    /// blocks with their entities, its entities and its objects, each a <see cref="DwgRecord"/>, with the indexes the
    /// encoders look things up in: every record by handle, the records of each table by name, the entities each block
    /// owns. Nothing follows the order of a dictionary: lists keep the order of the file.
    /// </summary>
    internal sealed partial class DwgDrawing
    {
        private readonly Dictionary<string, List<KeyValuePair<int, string>>> _header = new Dictionary<
            string,
            List<KeyValuePair<int, string>>
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, ulong>> _names = new Dictionary<
            string,
            Dictionary<string, ulong>
        >(StringComparer.Ordinal);

        private DwgDrawing() { }

        /// <summary>The header variables in the order of the file, each by its name without the dollar sign.</summary>
        public readonly List<string> HeaderNames = new List<string>();

        /// <summary>The classes, records of type CLASS, in the order of the file.</summary>
        public readonly List<DwgRecord> Classes = new List<DwgRecord>();

        /// <summary>The TABLE records in the order of the file, each with its <see cref="DwgRecord.Records"/>.</summary>
        public readonly List<DwgRecord> Tables = new List<DwgRecord>();

        /// <summary>The BLOCK records of the BLOCKS section, each with its <see cref="DwgRecord.Entities"/>, the ENDBLK last.</summary>
        public readonly List<DwgRecord> Blocks = new List<DwgRecord>();

        /// <summary>The entities of the ENTITIES section, the vertices, attributes and SEQEND under their owners.</summary>
        public readonly List<DwgRecord> Entities = new List<DwgRecord>();

        /// <summary>The records of the OBJECTS section, in the order of the file.</summary>
        public readonly List<DwgRecord> Objects = new List<DwgRecord>();

        /// <summary>Every record that has a handle, by handle; the first of a handle written twice.</summary>
        public readonly Dictionary<ulong, DwgRecord> ByHandle = new Dictionary<ulong, DwgRecord>();

        /// <summary>The records with handles in the order they were met.</summary>
        public readonly List<DwgRecord> All = new List<DwgRecord>();

        /// <summary>
        /// How many records of each type have no handle (a CLASS and an ENDTAB never have): the DWG cannot name them, so they
        /// are not in <see cref="All"/> and the writer leaves them out.
        /// </summary>
        public readonly SortedDictionary<string, int> WithoutHandle = new SortedDictionary<string, int>(
            StringComparer.Ordinal
        );

        /// <summary>The entities each block record owns, in order, by the handle of the block record; the BLOCK and ENDBLK not among them.</summary>
        public readonly Dictionary<ulong, List<DwgRecord>> Owned = new Dictionary<ulong, List<DwgRecord>>();

        /// <summary>Of each block record, its BLOCK and its ENDBLK.</summary>
        public readonly Dictionary<ulong, DwgRecord[]> BlockEnds = new Dictionary<ulong, DwgRecord[]>();

        /// <summary>
        /// The ACIS data of the section ACDSDATA, in the order of the file: of each ACDSRECORD the handle of its entity
        /// (320) and its SAB bytes (94 and 310), the first of a handle given twice.
        /// </summary>
        public readonly List<KeyValuePair<ulong, byte[]>> AcisData = new List<KeyValuePair<ulong, byte[]>>();

        /// <summary>
        /// The picture of the section THUMBNAILIMAGE, a bitmap of Windows without its file header or a PNG file, which the
        /// writer keeps as the preview of the DWG; null where the file has none.
        /// </summary>
        public byte[] Thumbnail { get; private set; }

        /// <summary>The version the file names, <c>$ACADVER</c>.</summary>
        public string Version => HeaderText("ACADVER", "AC1032");

        /// <summary>
        /// Takes apart the text of a DXF file: pairs of lines, a group code and a value. Throws
        /// <see cref="FormatException"/> where a group code is not a number.
        /// </summary>
        public static DwgDrawing Parse(string text) => Parse(text, null);

        /// <summary>
        /// Takes apart the text of a DXF file as <see cref="Parse(string)"/>, how far it has got told to
        /// <paramref name="progress"/> (null for none): the lines read to 0.45 of the whole, the records made of them to 0.55.
        /// </summary>
        public static DwgDrawing Parse(string text, ProgressSteps progress)
        {
            var drawing = new DwgDrawing();
            var groups = new List<KeyValuePair<int, string>>();
            int at = 0;
            progress?.Stage(0.0, 0.45);

            while (at < text.Length)
            {
                if ((groups.Count & 4095) == 0)
                {
                    progress?.Report(at, text.Length);
                }

                string codeLine = Line(text, ref at);

                if (at >= text.Length && codeLine.Trim().Length == 0)
                {
                    break;
                }

                if (!int.TryParse(codeLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                {
                    throw new FormatException(
                        "The DXF text has a group code that is not a number: " + codeLine.Trim() + "."
                    );
                }

                groups.Add(new KeyValuePair<int, string>(code, Line(text, ref at)));
            }

            progress?.Stage(0.45, 0.55);
            drawing.Read(groups);
            progress?.Report(1.0);
            return drawing;
        }

        private static string Line(string text, ref int at)
        {
            int end = text.IndexOf('\n', at);

            if (end < 0)
            {
                end = text.Length;
            }

            int stop = end > at && text[end - 1] == '\r' ? end - 1 : end;
            string line = text.Substring(at, stop - at);
            at = end + 1;
            return line;
        }

        private void Read(List<KeyValuePair<int, string>> groups)
        {
            int i = 0;

            while (i < groups.Count)
            {
                if (groups[i].Key != 0 || groups[i].Value.Trim() != "SECTION" || i + 1 >= groups.Count)
                {
                    i++;
                    continue;
                }

                string section = groups[i + 1].Value.Trim();
                i += 2;
                int end = i;

                while (end < groups.Count && !(groups[end].Key == 0 && groups[end].Value.Trim() == "ENDSEC"))
                {
                    end++;
                }

                if (section == "HEADER")
                {
                    ReadHeader(groups, i, end);
                }
                else if (section == "ACDSDATA")
                {
                    ReadAcisData(groups, i, end);
                }
                else if (section == "THUMBNAILIMAGE")
                {
                    ReadThumbnail(groups, i, end);
                }
                else
                {
                    List<DwgRecord> records = Records(groups, i, end);

                    switch (section)
                    {
                        case "CLASSES":
                            Classes.AddRange(records);
                            break;
                        case "TABLES":
                            ReadTables(records);
                            break;
                        case "BLOCKS":
                            ReadBlocks(records);
                            break;
                        case "ENTITIES":
                            Entities.AddRange(Nest(records));
                            break;
                        case "OBJECTS":
                            Objects.AddRange(records);
                            break;
                    }
                }

                i = end + 1;
            }

            foreach (DwgRecord entity in Entities)
            {
                Own(entity.Owner, entity);
            }

            foreach (DwgRecord block in Blocks)
            {
                ulong owner = block.Owner;
                BlockEnds[owner] = new[]
                {
                    block,
                    block.Entities.Count > 0 && block.Entities[block.Entities.Count - 1].Type == "ENDBLK"
                        ? block.Entities[block.Entities.Count - 1]
                        : null,
                };

                foreach (DwgRecord entity in block.Entities)
                {
                    if (entity.Type != "ENDBLK")
                    {
                        Own(owner, entity);
                    }
                }
            }
        }

        private void Own(ulong owner, DwgRecord entity)
        {
            if (!Owned.TryGetValue(owner, out List<DwgRecord> list))
            {
                list = new List<DwgRecord>();
                Owned[owner] = list;
            }

            entity.Container = owner;
            list.Add(entity);
        }

        private void ReadHeader(List<KeyValuePair<int, string>> groups, int from, int to)
        {
            List<KeyValuePair<int, string>> current = null;

            for (int i = from; i < to; i++)
            {
                if (groups[i].Key == 9)
                {
                    string name = groups[i].Value.Trim().TrimStart('$');
                    current = new List<KeyValuePair<int, string>>();

                    if (!_header.ContainsKey(name))
                    {
                        _header[name] = current;
                        HeaderNames.Add(name);
                    }
                }
                else if (current != null)
                {
                    current.Add(groups[i]);
                }
            }
        }

        /// <summary>
        /// The records of ACIS data of the section ACDSDATA: each ACDSRECORD that names an entity (320) and gives bytes (94,
        /// then 310 in hex); the schemas and the records of anything else are passed over.
        /// </summary>
        private void ReadAcisData(List<KeyValuePair<int, string>> groups, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                if (groups[i].Key != 0 || groups[i].Value.Trim() != "ACDSRECORD")
                {
                    continue;
                }

                ulong handle = 0;
                int size = -1;
                var bytes = new List<byte>();

                for (i++; i < to && groups[i].Key != 0; i++)
                {
                    switch (groups[i].Key)
                    {
                        case 320:
                            handle = DwgRecord.Hex(groups[i].Value);
                            break;
                        case 94:
                            size = DwgRecord.Int(groups[i].Value);
                            break;
                        case 310:
                            if (size >= 0)
                            {
                                bytes.AddRange(DwgObjectWriter.Unhex(groups[i].Value));
                            }

                            break;
                    }
                }

                i--;

                if (handle != 0 && size > 0 && bytes.Count >= size && !AcisData.Exists(data => data.Key == handle))
                {
                    AcisData.Add(new KeyValuePair<ulong, byte[]>(handle, bytes.GetRange(0, size).ToArray()));
                }
            }
        }

        /// <summary>
        /// The picture of the section THUMBNAILIMAGE: its size (90) and its bytes (310 in hex), the first such section of a
        /// file; a section whose bytes fall short of its size gives none.
        /// </summary>
        private void ReadThumbnail(List<KeyValuePair<int, string>> groups, int from, int to)
        {
            int size = -1;
            var bytes = new List<byte>();

            for (int i = from; i < to; i++)
            {
                if (groups[i].Key == 90 && size < 0)
                {
                    size = DwgRecord.Int(groups[i].Value);
                }
                else if (groups[i].Key == 310)
                {
                    bytes.AddRange(DwgObjectWriter.Unhex(groups[i].Value));
                }
            }

            if (Thumbnail == null && size > 0 && bytes.Count >= size)
            {
                Thumbnail = bytes.GetRange(0, size).ToArray();
            }
        }

        private List<DwgRecord> Records(List<KeyValuePair<int, string>> groups, int from, int to)
        {
            var records = new List<DwgRecord>();
            DwgRecord current = null;

            for (int i = from; i < to; i++)
            {
                if (groups[i].Key == 0)
                {
                    current = new DwgRecord { Type = groups[i].Value.Trim() };
                    records.Add(current);
                }
                else
                {
                    current?.Groups.Add(groups[i]);
                }
            }

            foreach (DwgRecord record in records)
            {
                record.Split();

                if (record.Handle != 0)
                {
                    All.Add(record);

                    if (!ByHandle.ContainsKey(record.Handle))
                    {
                        ByHandle[record.Handle] = record;
                    }
                }
                else if (record.Type != "CLASS" && record.Type != "ENDTAB")
                {
                    WithoutHandle.TryGetValue(record.Type, out int count);
                    WithoutHandle[record.Type] = count + 1;
                }
            }

            return records;
        }

        /// <summary>The records with the vertices and attributes that follow a POLYLINE or an INSERT put under it, up to its SEQEND.</summary>
        private static List<DwgRecord> Nest(List<DwgRecord> records)
        {
            var top = new List<DwgRecord>();
            DwgRecord holder = null;

            foreach (DwgRecord record in records)
            {
                if (holder != null && (record.Type == "VERTEX" || record.Type == "ATTRIB"))
                {
                    holder.Owned.Add(record);
                    record.Container = holder.Handle;
                    continue;
                }

                if (holder != null && record.Type == "SEQEND")
                {
                    holder.SeqEnd = record;
                    record.Container = holder.Handle;
                    holder = null;
                    continue;
                }

                holder = null;
                top.Add(record);

                if ((record.Type == "POLYLINE") || (record.Type == "INSERT" && record.Of(null).Int(66) == 1))
                {
                    holder = record;
                    record.Owned = new List<DwgRecord>();
                }
            }

            return top;
        }

        private void ReadTables(List<DwgRecord> records)
        {
            DwgRecord table = null;

            foreach (DwgRecord record in records)
            {
                if (record.Type == "TABLE")
                {
                    table = record;
                    table.Records = new List<DwgRecord>();
                    Tables.Add(table);

                    // The head of a TABLE is its name and its handle before the subclass marker.
                    foreach (KeyValuePair<int, string> group in record.Groups)
                    {
                        if (group.Key == 2)
                        {
                            record.Type = "TABLE:" + group.Value.Trim();
                            break;
                        }
                    }
                }
                else if (record.Type == "ENDTAB")
                {
                    table = null;
                }
                else if (table != null)
                {
                    table.Records.Add(record);
                    string name = record.Of(null).Text(2).Trim();

                    if (!_names.TryGetValue(record.Type, out Dictionary<string, ulong> names))
                    {
                        names = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                        _names[record.Type] = names;
                    }

                    if (!names.ContainsKey(name))
                    {
                        names[name] = record.Handle;
                    }
                }
            }
        }

        private void ReadBlocks(List<DwgRecord> records)
        {
            DwgRecord block = null;

            foreach (DwgRecord record in Nest(records))
            {
                if (record.Type == "BLOCK")
                {
                    block = record;
                    block.Entities = new List<DwgRecord>();
                    Blocks.Add(block);
                }
                else if (block != null)
                {
                    block.Entities.Add(record);

                    if (record.Type == "ENDBLK")
                    {
                        block = null;
                    }
                }
            }
        }

        /// <summary>The groups of the header variable <paramref name="name"/> (no dollar sign); null where the file has none.</summary>
        public List<KeyValuePair<int, string>> Header(string name) =>
            _header.TryGetValue(name, out List<KeyValuePair<int, string>> groups) ? groups : null;

        /// <summary>The first value of the header variable <paramref name="name"/>; <paramref name="otherwise"/> where the file has none.</summary>
        public string HeaderText(string name, string otherwise)
        {
            List<KeyValuePair<int, string>> groups = Header(name);
            return groups == null || groups.Count == 0 ? otherwise : groups[0].Value;
        }

        /// <summary>The handle of the record of the table <paramref name="table"/> (such as <c>LAYER</c>) named <paramref name="name"/>, in any case; 0 where there is none.</summary>
        public ulong HandleOf(string table, string name)
        {
            if (name == null || !_names.TryGetValue(table, out Dictionary<string, ulong> names))
            {
                return 0;
            }

            return names.TryGetValue(name.Trim(), out ulong handle) ? handle : 0;
        }

        /// <summary>The TABLE record of the table <paramref name="name"/> (such as <c>LAYER</c>); null where there is none.</summary>
        public DwgRecord Table(string name) => Tables.Find(table => table.Type == "TABLE:" + name);

        /// <summary>The record of <paramref name="handle"/>; null where there is none.</summary>
        public DwgRecord Find(ulong handle) =>
            handle != 0 && ByHandle.TryGetValue(handle, out DwgRecord record) ? record : null;
    }
}
