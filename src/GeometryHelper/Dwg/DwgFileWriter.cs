using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Writes the DWG file of a DXF drawing: the header variables, the classes, the objects with their map, the small
    /// sections a file of R2004 and later carries, and the container around them. The records of the DXF are encoded by
    /// <see cref="DwgObjectWriter"/>; a record of a type it does not write yet is left out, every handle to it written as
    /// 0, and counted by type.
    /// </summary>
    internal sealed class DwgFileWriter
    {
        /// <summary>The versions written: AC1018 (R2004), AC1024 (R2010), AC1027 (R2013) and AC1032 (R2018).</summary>
        public static readonly string[] Versions = { "AC1018", "AC1024", "AC1027", "AC1032" };

        private static readonly byte[] HeaderStart =
        {
            0xCF,
            0x7B,
            0x1F,
            0x23,
            0xFD,
            0xDE,
            0x38,
            0xA9,
            0x5F,
            0x7C,
            0x68,
            0xB8,
            0x4E,
            0x6D,
            0x33,
            0x5F,
        };
        private static readonly byte[] HeaderEnd =
        {
            0x30,
            0x84,
            0xE0,
            0xDC,
            0x02,
            0x21,
            0xC7,
            0x56,
            0xA0,
            0x83,
            0x97,
            0x47,
            0xB1,
            0x92,
            0xCC,
            0xA0,
        };
        private static readonly byte[] ClassesStart =
        {
            0x8D,
            0xA1,
            0xC4,
            0xB8,
            0xC4,
            0xA9,
            0xF8,
            0xC5,
            0xC0,
            0xDC,
            0xF4,
            0x5F,
            0xE7,
            0xCF,
            0xB6,
            0x8A,
        };
        private static readonly byte[] ClassesEnd =
        {
            0x72,
            0x5E,
            0x3B,
            0x47,
            0x3B,
            0x56,
            0x07,
            0x3A,
            0x3F,
            0x23,
            0x0B,
            0xA0,
            0x18,
            0x30,
            0x49,
            0x75,
        };
        private static readonly byte[] PreviewStart =
        {
            0x1F,
            0x25,
            0x6D,
            0x07,
            0xD4,
            0x36,
            0x28,
            0x28,
            0x9D,
            0x57,
            0xCA,
            0x3F,
            0x9D,
            0x44,
            0x10,
            0x2B,
        };
        private static readonly byte[] PreviewEnd =
        {
            0xE0,
            0xDA,
            0x92,
            0xF8,
            0x2B,
            0xC9,
            0xD7,
            0xD7,
            0x62,
            0xA8,
            0x35,
            0xC0,
            0x62,
            0xBB,
            0xEF,
            0xD4,
        };

        /// <summary>
        /// The values the specification does not name, as AutoCAD 2021 writes them in every drawing of R2018: by the name
        /// <see cref="DwgHeaderVariables"/> gives them.
        /// </summary>
        private static readonly Dictionary<string, object> Unnamed = new Dictionary<string, object>(
            StringComparer.Ordinal
        )
        {
            { "?1", 412148564080.0 },
            { "?2", 1.0 },
            { "?3", 1.0 },
            { "?4", 1.0 },
            { "?5", "m" },
            { "?6", string.Empty },
            { "?7", string.Empty },
            { "?8", string.Empty },
            { "?9", 0 },
            { "?10", 0 },
            { "?11", 0 },
            { "?12", 808464432 },
            { "?13", 486539264 },
            { "?14", 1295003696 },
            { "?15", 1295003696 },
            { "?16", 486539264 },
            { "?17", 808464432 },
            { "?18", 100.0 },
            { "?19", string.Empty },
            { "?20", 100.0 },
            { "?21", string.Empty },
            { "?22", 0UL },
            { "?23", 0 },
            { "?24", 10 },
            { "?25", 1.0 },
        };

        /// <summary>
        /// The values AutoCAD 2021 gives the header variables of a new drawing, in the units of a DWG (angles in radians),
        /// for those a DXF leaves out: <c>ISOLINES</c>, <c>TEXTQLTY</c>, <c>FACETRES</c>, <c>TSTACKALIGN</c> and
        /// <c>TSTACKSIZE</c> are in no DXF, and the writer's own drawings give few variables.
        /// </summary>
        private const string Defaults =
            "DIMASO=1;DIMSHO=1;REGENMODE=1;FILLMODE=1;PSLTSCALE=1;USRTIMER=1;WORLDVIEW=1;TILEMODE=1;VISRETAIN=1;PROXYGRAPHICS=1;"
            + "TREEDEPTH=3020;LUNITS=2;LUPREC=4;ATTMODE=1;SPLINESEGS=8;SURFU=6;SURFV=6;SURFTYPE=6;SURFTAB1=6;SURFTAB2=6;"
            + "SPLINETYPE=6;SHADEDGE=3;SHADEDIF=70;MAXACTVP=64;ISOLINES=8;TEXTQLTY=50;LTSCALE=1;TEXTSIZE=0.2;TRACEWID=0.05;"
            + "SKETCHINC=0.1;FACETRES=0.5;CMLSCALE=1;CELTSCALE=1;CECOLOR=256;DIMSCALE=1;DIMASZ=0.18;DIMEXO=0.0625;DIMDLI=0.38;"
            + "DIMEXE=0.18;DIMFXL=1;DIMJOGANG=0.78539816339744828;DIMTIH=1;DIMTOH=1;DIMTXT=0.18;DIMCEN=0.09;DIMALTF=25.4;"
            + "DIMLFAC=1;DIMTFAC=1;DIMGAP=0.09;DIMALTD=2;DIMDEC=4;DIMTDEC=4;DIMALTU=2;DIMALTTD=2;DIMLUNIT=2;DIMDSEP=46;"
            + "DIMTOLJ=1;DIMATFIT=3;DIMLWD=-2;DIMLWE=-2;TSTACKALIGN=1;TSTACKSIZE=70;SORTENTS=127;HIDETEXT=1;XCLIPFRAME=2;"
            + "DIMASSOC=2;OBSCOLOR=257;INTERSECTIONCOLOR=257;STEPSPERSEC=2;STEPSIZE=6;3DDWFPREC=2;LENSLENGTH=50;SHOWHIST=1;"
            + "PSOLWIDTH=0.25;PSOLHEIGHT=4;LOFTANG1=1.5707963267948966;LOFTANG2=1.5707963267948966;LOFTPARAM=7;LOFTNORMALS=1;"
            + "LATITUDE=37.795;LONGITUDE=-122.394;TIMEZONE=-8000;LIGHTGLYPHDISPLAY=1;TILEMODELIGHTSYNCH=1;DWFFRAME=2;"
            + "REALWORLDSCALE=1;INTERFERECOLOR=1";

        private static readonly Dictionary<string, string> DefaultValues = ParseDefaults();

        private readonly DwgDrawing _drawing;
        private readonly string _version;
        private readonly int _number;
        private readonly List<string> _classNames = new List<string>();
        private readonly HashSet<ulong> _written = new HashSet<ulong>();
        private readonly SortedDictionary<string, int> _left = new SortedDictionary<string, int>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _classCounts = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase
        );
        private DwgObjectWriter _objects;
        private ulong _handseed;

        private DwgFileWriter(DwgDrawing drawing, string version)
        {
            _drawing = drawing;
            _version = version;
            _number = DwgContainer.NumberOf(version);
        }

        /// <summary>The types left out because they are not written yet, with the count of each, by name.</summary>
        public IDictionary<string, int> LeftOut => _left;

        /// <summary>The number of objects written.</summary>
        public int Written => _written.Count;

        /// <summary>The bytes of the DWG file of <paramref name="drawing"/> in <paramref name="version"/>.</summary>
        public static byte[] Write(DwgDrawing drawing, string version, out DwgFileWriter writer)
        {
            writer = new DwgFileWriter(drawing, version);
            return writer.Write();
        }

        private byte[] Write()
        {
            foreach (DwgRecord one in _drawing.Classes)
            {
                _classNames.Add(one.Of(null).Text(1).Trim());
            }

            // Which records are written: those of a type an encoder knows, the first of each handle, but entities owned by
            // what is left out.
            _objects = new DwgObjectWriter(_drawing, _number, _classNames, _written);
            var records = new List<DwgRecord>();

            foreach (DwgRecord record in _drawing.All)
            {
                if (_drawing.Find(record.Handle) != record || record.Type == "ENDTAB")
                {
                    continue;
                }

                if (_objects.TypeOf(record) < 0)
                {
                    _left.TryGetValue(record.Type, out int count);
                    _left[record.Type] = count + 1;
                    continue;
                }

                records.Add(record);
                _written.Add(record.Handle);
                _handseed = Math.Max(_handseed, record.Handle);
            }

            // Vertices, attributes and SEQENDs whose owner is left out go with it.
            records.RemoveAll(record =>
                (record.Type == "VERTEX" || record.Type == "ATTRIB" || record.Type == "SEQEND")
                && record.Container != 0
                && !_written.Contains(record.Container)
                && _written.Remove(record.Handle)
            );
            records.Sort((a, b) => a.Handle.CompareTo(b.Handle));

            var objects = new MemoryStream();
            objects.Write(new byte[] { 0xCA, 0x0D, 0x00, 0x00 }, 0, 4);
            var map = new List<KeyValuePair<ulong, long>>();

            foreach (DwgRecord record in records)
            {
                byte[] bytes = _objects.Write(record);
                map.Add(new KeyValuePair<ulong, long>(record.Handle, objects.Position));
                objects.Write(bytes, 0, bytes.Length);

                if (_classNames.Contains(record.Type))
                {
                    _classCounts.TryGetValue(record.Type, out int count);
                    _classCounts[record.Type] = count + 1;
                }
            }

            ulong seed = DwgRecord.Hex(_drawing.HeaderText("HANDSEED", "0"));
            _handseed = Math.Max(_handseed + 1, seed);
            int codePage = CodePageNumber(_drawing.HeaderText("DWGCODEPAGE", "ANSI_1252").Trim());
            int maintenance = _number >= 1032 ? 174 : 0;
            var container = new DwgContainerWriter(_version, maintenance, codePage);
            byte[] objectBytes = objects.ToArray();
            container.Add("AcDb:Preview", Preview(), 9, 0x400, false);
            container.Add("AcDb:AppInfo", AppInfo(), 10, 0x180, false);
            container.Add(
                "AcDb:RevHistory",
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 },
                8,
                0x7400,
                true
            );
            container.Add("AcDb:AcDbObjects", objectBytes, 7, 0x7400, true);
            container.Add("AcDb:ObjFreeSpace", FreeSpace(map.Count), 6, 0x7400, true);
            container.Add("AcDb:Template", Template(), 5, 0x7400, true);
            container.Add("AcDb:Handles", ObjectMap(map), 4, 0x7400, true);
            container.Add("AcDb:Classes", Classes(), 3, 0x7400, true);
            container.Add("AcDb:AuxHeader", AuxHeader(maintenance), 2, 0x7400, true);
            container.Add("AcDb:Header", Header(), 1, 0x7400, true);
            return container.ToArray();
        }

        private static int CodePageNumber(string name)
        {
            switch (name.ToUpperInvariant())
            {
                case "ANSI_1250":
                    return 28;
                case "ANSI_1251":
                    return 29;
                case "ANSI_1253":
                    return 32;
                case "ANSI_1254":
                    return 33;
                case "ANSI_1255":
                    return 34;
                case "ANSI_1256":
                    return 35;
                case "ANSI_1257":
                    return 36;
                case "ANSI_874":
                    return 37;
                case "ANSI_932":
                    return 38;
                case "ANSI_936":
                    return 39;
                case "ANSI_949":
                    return 40;
                case "ANSI_950":
                    return 41;
                case "ANSI_1258":
                    return 44;
                default:
                    return 30;
            }
        }

        /// <summary>
        /// A section between two sentinels: the start sentinel, the size of the data, from R2010 a long of 0 more (from
        /// R2018, or where the maintenance release is past 3), the data, the check sum of the size and the data, the end
        /// sentinel.
        /// </summary>
        private byte[] Framed(byte[] start, byte[] data, byte[] end, bool trailer)
        {
            var section = new MemoryStream();
            section.Write(start, 0, 16);
            var sized = new MemoryStream();
            Long(sized, data.Length);

            if (_number >= 1032)
            {
                Long(sized, 0);
            }

            sized.Write(data, 0, data.Length);
            byte[] summed = sized.ToArray();
            int crc = DwgCrc.Of(summed, 0, summed.Length, DwgCrc.Seed);
            section.Write(summed, 0, summed.Length);
            section.WriteByte((byte)crc);
            section.WriteByte((byte)(crc >> 8));
            section.Write(end, 0, 16);

            if (trailer)
            {
                section.Write(new byte[8], 0, 8);
            }

            return section.ToArray();
        }

        private static void Long(Stream stream, int value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        private static void Long64(Stream stream, long value)
        {
            Long(stream, (int)value);
            Long(stream, (int)(value >> 32));
        }

        /// <summary>
        /// The data of a section from R2007 as an object holds it: the size in bits up to the handles, the data, the texts in
        /// their stream with its size and flag, then the handles; padded to whole bytes.
        /// </summary>
        private static byte[] Merged(DwgBitWriter data, DwgBitWriter strings, DwgBitWriter handles)
        {
            var body = new DwgBitWriter();
            body.Append(data);
            long size = strings.Position;

            if (size > 0)
            {
                body.Append(strings);

                if (size >= 0x8000)
                {
                    body.RawShort((int)(size >> 15));
                    body.RawShort((int)(size & 0x7FFF) | 0x8000);
                }
                else
                {
                    body.RawShort((int)size);
                }
            }

            body.Bit(size > 0);
            var whole = new DwgBitWriter();
            whole.RawLong((int)(body.Position + 32));
            whole.Append(body);

            if (handles != null)
            {
                whole.Append(handles);
            }

            whole.PadToByte();
            return whole.ToArray();
        }

        private byte[] Classes()
        {
            var data = new DwgBitWriter();
            var strings = new DwgBitWriter();
            bool r2007 = _number >= 1021;
            int highest = 499 + _drawing.Classes.Count;

            if (r2007)
            {
                data.BitLong(highest);
                data.Bit(true);
            }
            else
            {
                data.BitShort(highest);
                data.RawChar(0);
                data.RawChar(0);
                data.Bit(true);
            }

            for (int i = 0; i < _drawing.Classes.Count; i++)
            {
                DwgGroups one = _drawing.Classes[i].Of(null);
                string name = one.Text(1).Trim();
                data.BitShort(500 + i);
                data.BitShort(one.Int(90));
                Text(data, strings, one.Text(3));
                Text(data, strings, one.Text(2));
                Text(data, strings, name);
                data.Bit(one.Int(280) != 0);
                data.BitShort(one.Int(281) != 0 ? 0x1F2 : 0x1F3);
                _classCounts.TryGetValue(name, out int count);
                data.BitLong(count);
                int[] release = ClassRelease(name);
                data.BitLong(release[0]);
                data.BitLong(release[1]);
                data.BitLong(0);
                data.BitLong(0);
            }

            byte[] bytes = r2007 ? Merged(data, strings, null) : Padded(data);
            return Framed(ClassesStart, bytes, ClassesEnd, true);
        }

        /// <summary>
        /// The version and maintenance release a class carries, as AutoCAD 2021 writes them for the classes of its test
        /// drawings (the release that brought the class); for another class those of the file.
        /// </summary>
        private int[] ClassRelease(string name)
        {
            switch (name.ToUpperInvariant())
            {
                case "ACDBDICTIONARYWDFLT":
                    return new[] { 22, 42 };
                case "DICTIONARYVAR":
                case "IMAGE":
                case "IMAGEDEF":
                case "IMAGEDEF_REACTOR":
                    return new[] { 20, 0 };
                case "MATERIAL":
                case "DBCOLOR":
                    return new[] { 23, 14 };
                case "VISUALSTYLE":
                    return new[] { 26, 0 };
                case "SCALE":
                case "TABLEGEOMETRY":
                    return new[] { 27, 1 };
                case "ACAD_TABLE":
                case "TABLESTYLE":
                    return new[] { 25, 20 };
                case "TABLECONTENT":
                    return new[] { 25, 21 };
                case "ACDBASSOCPERSSUBENTMANAGER":
                    return new[] { 27, 174 };
                default:
                    return new[] { DwgContainerWriter.DwgVersionNumber(_version), _number >= 1032 ? 174 : 0 };
            }
        }

        private static byte[] Padded(DwgBitWriter data)
        {
            data.PadToByte();
            return data.ToArray();
        }

        private void Text(DwgBitWriter data, DwgBitWriter strings, string text)
        {
            text = text ?? string.Empty;

            if (_number >= 1021)
            {
                strings.TextUnicode(text);
            }
            else
            {
                data.Text(Encoding.UTF8.GetBytes(text));
            }
        }

        /// <summary>
        /// The object map: pages of at most 2032 bytes, each its size as a big-endian short, then for each object the step
        /// of its handle and of its offset from the last of the page as modular chars, then the check sum big-endian; an
        /// empty page ends it.
        /// </summary>
        private static byte[] ObjectMap(List<KeyValuePair<ulong, long>> map)
        {
            var section = new MemoryStream();
            int at = 0;

            while (true)
            {
                var page = new DwgBitWriter();
                page.RawChar(0);
                page.RawChar(0);
                ulong handle = 0;
                long offset = 0;

                while (at < map.Count)
                {
                    var entry = new DwgBitWriter();
                    entry.ModularChar((long)(map[at].Key - handle), false);
                    entry.ModularChar(map[at].Value - offset);

                    if ((page.Position / 8) + (entry.Position / 8) > 2030)
                    {
                        break;
                    }

                    page.Append(entry);
                    handle = map[at].Key;
                    offset = map[at].Value;
                    at++;
                }

                byte[] bytes = page.ToArray();
                bytes[0] = (byte)(bytes.Length >> 8);
                bytes[1] = (byte)bytes.Length;
                int crc = DwgCrc.Of(bytes, 0, bytes.Length, DwgCrc.Seed);
                section.Write(bytes, 0, bytes.Length);
                section.WriteByte((byte)(crc >> 8));
                section.WriteByte((byte)crc);

                if (bytes.Length == 2)
                {
                    return section.ToArray();
                }
            }
        }

        private byte[] Preview()
        {
            var preview = new MemoryStream();
            preview.Write(PreviewStart, 0, 16);
            Long(preview, 1);
            preview.WriteByte(0);
            preview.Write(PreviewEnd, 0, 16);
            return preview.ToArray();
        }

        private static byte[] AppInfo()
        {
            string version = typeof(DwgFileWriter).Assembly.GetName().Version.ToString();
            var info = new MemoryStream();
            Long(info, 3);
            Unicode(info, "AppInfoDataList");
            Long(info, 3);
            info.Write(new byte[16], 0, 16);
            Unicode(info, version);
            info.Write(new byte[16], 0, 16);
            Unicode(info, "Written by GeometryHelper");
            info.Write(new byte[16], 0, 16);
            Unicode(
                info,
                "<ProductInformation name =\"GeometryHelper\" build_version=\""
                    + version
                    + "\" registry_version=\""
                    + version
                    + "\" install_id_string=\"GeometryHelper\" registry_localeID=\"1033\"/>"
            );
            return info.ToArray();
        }

        /// <summary>A text as the small sections hold it: a short of the count of UTF-16 characters with the closing zero, the characters and the zero.</summary>
        private static void Unicode(Stream stream, string text)
        {
            stream.WriteByte((byte)(text.Length + 1));
            stream.WriteByte((byte)((text.Length + 1) >> 8));
            byte[] bytes = Encoding.Unicode.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
            stream.WriteByte(0);
        }

        private byte[] FreeSpace(int count)
        {
            var space = new MemoryStream();
            Julian(_drawing.HeaderText("TDUUPDATE", "0"), out int day, out int milliseconds);
            Long64(space, 0);
            Long64(space, count);
            Long(space, day);
            Long(space, milliseconds);
            space.WriteByte(4);

            foreach (long value in new long[] { 0x32, 0, 0x64, 0, 0x200, 0, 0xFFFFFFFF, 0 })
            {
                Long64(space, value);
            }

            return space.ToArray();
        }

        private static void Julian(string value, out int day, out int milliseconds)
        {
            double julian = DwgRecord.Double(value);
            day = (int)Math.Floor(julian);
            milliseconds = (int)Math.Round((julian - day) * 86400000.0);
        }

        private byte[] Template()
        {
            var template = new MemoryStream();
            int measurement = DwgRecord.Int(_drawing.HeaderText("MEASUREMENT", "1"));

            if (_number >= 1021)
            {
                template.WriteByte(1);
                template.WriteByte(0);
                template.WriteByte(0);
                template.WriteByte(0);
            }
            else
            {
                template.WriteByte(0);
                template.WriteByte(0);
            }

            template.WriteByte((byte)measurement);
            template.WriteByte(0);
            return template.ToArray();
        }

        private byte[] AuxHeader(int maintenance)
        {
            var aux = new MemoryStream();
            int version = DwgContainerWriter.DwgVersionNumber(_version);
            Action maint = () =>
            {
                if (_number > 1027)
                {
                    Long(aux, maintenance);
                }
                else
                {
                    aux.WriteByte((byte)maintenance);
                    aux.WriteByte(0);
                }
            };
            aux.WriteByte(0xFF);
            aux.WriteByte(0x77);
            aux.WriteByte(0x01);
            Short(aux, version);
            maint();
            Long(aux, 1);
            Long(aux, -1);
            Short(aux, 1);
            Short(aux, 0);
            Long(aux, 0);
            Short(aux, version);
            maint();
            Short(aux, version);
            maint();
            // Where the specification has the shorts 5, 0x893, 5, 0x893 AutoCAD 2021 writes 0; then 0 and 1, and five longs
            // of which it sets three as below.
            Short(aux, 0);
            Short(aux, 0);
            Short(aux, 0);
            Short(aux, 0);
            Short(aux, 0);
            Short(aux, 1);
            Long(aux, 0);
            Long(aux, 0);
            Long(aux, 0);
            Long(aux, 0x100);
            Long(aux, 0x10001);
            Julian(_drawing.HeaderText("TDUCREATE", "0"), out int createDay, out int createMs);
            Julian(_drawing.HeaderText("TDUUPDATE", "0"), out int updateDay, out int updateMs);
            Long(aux, createDay);
            Long(aux, createMs);
            Long(aux, updateDay);
            Long(aux, updateMs);
            Long(aux, _handseed <= 0x7FFFFFFF ? (int)_handseed : -1);
            Long(aux, 0);
            Short(aux, 0);
            Short(aux, 1);
            Long(aux, 0);
            Long(aux, 0);
            Long(aux, 0);
            Long(aux, 1);

            for (int i = 0; i < 4; i++)
            {
                Long(aux, 0);
            }

            return aux.ToArray();
        }

        private static void Short(Stream stream, int value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
        }

        /// <summary>The header variables in the order of <see cref="DwgHeaderVariables.Layout"/>, each from the DXF header.</summary>
        private byte[] Header()
        {
            var data = new DwgBitWriter();
            var strings = new DwgBitWriter();
            var handles = new DwgBitWriter();
            bool r2007 = _number >= 1021;
            DwgBitWriter handleStream = r2007 ? handles : data;
            var values = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string item in DwgHeaderVariables.Layout.Split(';'))
            {
                string entry = item;
                string guard = entry.Substring(0, entry.IndexOf(' '));

                if (guard[0] == '-' || guard[guard.Length - 1] == '+')
                {
                    entry = entry.Substring(guard.Length + 1);
                    bool from = guard[guard.Length - 1] == '+';
                    int bound = int.Parse(
                        from ? guard.Substring(0, guard.Length - 1) : guard.Substring(1),
                        CultureInfo.InvariantCulture
                    );

                    if (from ? _number < bound : _number >= bound)
                    {
                        continue;
                    }
                }

                int split = entry.IndexOf(' ');
                string code = entry.Substring(0, split);
                string name = entry.Substring(split + 1);
                int condition = code.IndexOf('?');

                if (condition > 0)
                {
                    string[] parts = code.Substring(condition + 1).Split('=');
                    code = code.Substring(0, condition);

                    if (
                        !values.TryGetValue(parts[0], out int given)
                        || given != int.Parse(parts[1], CultureInfo.InvariantCulture)
                    )
                    {
                        continue;
                    }
                }

                switch (code)
                {
                    case "B":
                        data.Bit(Int(name) != 0);
                        break;
                    case "BS":
                        values[name] = Int(name);
                        data.BitShort(values[name]);
                        break;
                    case "BL":
                        data.BitLong(name == "_FLAGS" ? Flags() : Int(name));
                        break;
                    case "BLL":
                        long.TryParse(
                            Value(name, "0").Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out long wide
                        );
                        data.BitLongLong(wide);
                        break;
                    case "RC":
                        data.RawChar(Int(name));
                        break;
                    case "BD":
                        data.BitDouble(Real(name));
                        break;
                    case "TV":
                        Text(
                            data,
                            strings,
                            Unnamed.TryGetValue(name, out object text) ? (string)text : Value(name, string.Empty)
                        );
                        break;
                    case "H":
                        handleStream.Handle(5, Reference(name));
                        break;
                    case "HS":
                        data.Handle(0, _handseed);
                        break;
                    case "CMC":
                        WriteColour(data, Int(name));
                        break;
                    case "3BD":
                        data.Point3(Point(name, 3));
                        break;
                    case "2RD":
                        data.RawPoint2(Point(name, 2));
                        break;
                    case "DT":
                        Julian(Value(name, "0"), out int day, out int milliseconds);
                        data.BitLong(day);
                        data.BitLong(milliseconds);
                        break;
                }
            }

            // What AutoCAD writes after the last value the specification names.
            if (_number >= 1018)
            {
                data.BitShort(-1);
                data.BitShort(-1);
                data.BitShort(-1);
                data.BitShort(-1);
                data.BitLong(0);
                data.BitLong(0);
                data.Bit(false);
            }

            byte[] bytes = r2007 ? Merged(data, strings, handles) : Padded(data);
            return Framed(HeaderStart, bytes, HeaderEnd, false);
        }

        private void WriteColour(DwgBitWriter data, int index)
        {
            if (_number < 1018)
            {
                data.BitShort(index);
                return;
            }

            data.BitShort(0);
            data.BitLong(
                index == 256 ? unchecked((int)0xC0000000)
                : index == 0 ? unchecked((int)0xC1000000)
                : unchecked((int)0xC3000000) | (index & 0xFF)
            );
            data.RawChar(0);
        }

        private string Value(string name, string otherwise) => _drawing.HeaderText(name, otherwise);

        private int Int(string name)
        {
            if (Unnamed.TryGetValue(name, out object value))
            {
                return value is int number ? number : 0;
            }

            return DwgRecord.Int(Value(name, Default(name)));
        }

        private double Real(string name)
        {
            if (Unnamed.TryGetValue(name, out object value))
            {
                return value is double number ? number : 0.0;
            }

            if (_drawing.Header(name) == null)
            {
                return DwgRecord.Double(Default(name));
            }

            double real = DwgRecord.Double(Value(name, "0"));
            return name == "ANGBASE" ? real * Math.PI / 180.0 : real;
        }

        /// <summary>The value of a variable the DXF does not give, as AutoCAD 2021 gives a new drawing; 0 where it has none.</summary>
        private static string Default(string name) => DefaultValues.TryGetValue(name, out string value) ? value : "0";

        private static Dictionary<string, string> ParseDefaults()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string pair in Defaults.Split(';'))
            {
                int split = pair.IndexOf('=');
                values[pair.Substring(0, split)] = pair.Substring(split + 1);
            }

            return values;
        }

        private double[] Point(string name, int count)
        {
            var point = new double[count];
            List<KeyValuePair<int, string>> groups = _drawing.Header(name);

            if (groups != null)
            {
                for (int i = 0; i < Math.Min(count, groups.Count); i++)
                {
                    point[i] = DwgRecord.Double(groups[i].Value);
                }

                return point;
            }

            // A point the DXF does not give, as a new drawing has it: the axes of the world, empty extents, the limits of
            // 12 by 9.
            switch (name)
            {
                case "UCSXDIR":
                case "PUCSXDIR":
                    point[0] = 1.0;
                    break;
                case "UCSYDIR":
                case "PUCSYDIR":
                    point[1] = 1.0;
                    break;
                case "EXTMIN":
                case "PEXTMIN":
                    point[0] = point[1] = point[2] = 1E20;
                    break;
                case "EXTMAX":
                case "PEXTMAX":
                    point[0] = point[1] = point[2] = -1E20;
                    break;
                case "LIMMAX":
                case "PLIMMAX":
                    point[0] = 12.0;
                    point[1] = 9.0;
                    break;
            }

            return point;
        }

        /// <summary>The flags of the lineweight, the end caps and joins, and the switches that DXF gives as variables of their own.</summary>
        private int Flags()
        {
            int flags = DwgObjectWriter.LineWeightIndex(DwgRecord.Int(Value("CELWEIGHT", "-1")));
            flags |= (DwgRecord.Int(Value("ENDCAPS", "0")) & 3) << 5;
            flags |= (DwgRecord.Int(Value("JOINSTYLE", "0")) & 3) << 7;
            flags |= DwgRecord.Int(Value("LWDISPLAY", "0")) == 0 ? 0x200 : 0;
            flags |= DwgRecord.Int(Value("XEDIT", "1")) == 0 ? 0x400 : 0;
            flags |= DwgRecord.Int(Value("EXTNAMES", "1")) != 0 ? 0x800 : 0;
            flags |= DwgRecord.Int(Value("PSTYLEMODE", "1")) != 0 ? 0x2000 : 0;
            flags |= DwgRecord.Int(Value("OLESTARTUP", "0")) != 0 ? 0x4000 : 0;
            return flags;
        }

        /// <summary>The handle a header variable names: a table record by name, a table, a dictionary of the root, a block; 0 where it names none written.</summary>
        private ulong Reference(string name)
        {
            ulong handle = 0;

            switch (name)
            {
                case "CLAYER":
                    handle = _drawing.HandleOf("LAYER", Value(name, "0"));
                    break;
                case "TEXTSTYLE":
                case "DIMTXSTY":
                    handle = _drawing.HandleOf("STYLE", Value(name, "Standard"));
                    break;
                case "CELTYPE":
                case "DIMLTYPE":
                case "DIMLTEX1":
                case "DIMLTEX2":
                    handle = _drawing.HandleOf("LTYPE", Value(name, string.Empty));
                    break;
                case "DIMSTYLE":
                    handle = _drawing.HandleOf("DIMSTYLE", Value(name, "Standard"));
                    break;
                case "CMLSTYLE":
                    handle = Entry("ACAD_MLINESTYLE", Value(name, "Standard"));
                    break;
                case "UCSNAME":
                case "PUCSNAME":
                case "UCSORTHOREF":
                case "PUCSORTHOREF":
                case "UCSBASE":
                case "PUCSBASE":
                    handle = _drawing.HandleOf("UCS", Value(name, string.Empty));
                    break;
                case "DIMLDRBLK":
                case "DIMBLK":
                case "DIMBLK1":
                case "DIMBLK2":
                    handle = _drawing.HandleOf("BLOCK_RECORD", Value(name, string.Empty));
                    break;
                case "CMATERIAL":
                case "INTERFEREOBJVS":
                case "INTERFEREVPVS":
                case "CPSNID":
                    handle = DwgRecord.Hex(Value(name, "0"));
                    break;
                case "_BLOCKS":
                    handle = _drawing.Table("BLOCK_RECORD")?.Handle ?? 0;
                    break;
                case "_LAYERS":
                    handle = _drawing.Table("LAYER")?.Handle ?? 0;
                    break;
                case "_STYLES":
                    handle = _drawing.Table("STYLE")?.Handle ?? 0;
                    break;
                case "_LTYPES":
                    handle = _drawing.Table("LTYPE")?.Handle ?? 0;
                    break;
                case "_VIEWS":
                    handle = _drawing.Table("VIEW")?.Handle ?? 0;
                    break;
                case "_UCSS":
                    handle = _drawing.Table("UCS")?.Handle ?? 0;
                    break;
                case "_VPORTS":
                    handle = _drawing.Table("VPORT")?.Handle ?? 0;
                    break;
                case "_APPIDS":
                    handle = _drawing.Table("APPID")?.Handle ?? 0;
                    break;
                case "_DIMSTYLES":
                    handle = _drawing.Table("DIMSTYLE")?.Handle ?? 0;
                    break;
                case "_NAMEDOBJECTS":
                    handle = Root()?.Handle ?? 0;
                    break;
                case "_GROUPS":
                    handle = Entry(null, "ACAD_GROUP");
                    break;
                case "_MLINESTYLES":
                    handle = Entry(null, "ACAD_MLINESTYLE");
                    break;
                case "_LAYOUTS":
                    handle = Entry(null, "ACAD_LAYOUT");
                    break;
                case "_PLOTSETTINGS":
                    handle = Entry(null, "ACAD_PLOTSETTINGS");
                    break;
                case "_PLOTSTYLES":
                    handle = Entry(null, "ACAD_PLOTSTYLENAME");
                    break;
                case "_MATERIALS":
                    handle = Entry(null, "ACAD_MATERIAL");
                    break;
                case "_COLORS":
                    handle = Entry(null, "ACAD_COLOR");
                    break;
                case "_VISUALSTYLES":
                    handle = Entry(null, "ACAD_VISUALSTYLE");
                    break;
                case "_PAPERSPACE":
                    handle = _drawing.HandleOf("BLOCK_RECORD", "*Paper_Space");
                    break;
                case "_MODELSPACE":
                    handle = _drawing.HandleOf("BLOCK_RECORD", "*Model_Space");
                    break;
                case "_BYLAYER":
                    handle = _drawing.HandleOf("LTYPE", "ByLayer");
                    break;
                case "_BYBLOCK":
                    handle = _drawing.HandleOf("LTYPE", "ByBlock");
                    break;
                case "_CONTINUOUS":
                    handle = _drawing.HandleOf("LTYPE", "Continuous");
                    break;
            }

            return handle != 0 && _written.Contains(handle) ? handle : 0;
        }

        /// <summary>The root dictionary: the first DICTIONARY of the objects that no object owns.</summary>
        private DwgRecord Root()
        {
            return _drawing.Objects.Find(record => record.Type == "DICTIONARY" && record.Owner == 0);
        }

        /// <summary>The handle of the entry <paramref name="name"/> of the dictionary that the root names <paramref name="dictionary"/>, or of the root itself where that is null.</summary>
        private ulong Entry(string dictionary, string name)
        {
            DwgRecord holder = Root();

            if (holder != null && dictionary != null)
            {
                holder = _drawing.Find(EntryOf(holder, dictionary));
            }

            return holder == null ? 0 : EntryOf(holder, name);
        }

        private static ulong EntryOf(DwgRecord dictionary, string name)
        {
            string key = null;

            foreach (KeyValuePair<int, string> group in dictionary.Body)
            {
                if (group.Key == 3)
                {
                    key = group.Value;
                }
                else if ((group.Key == 350 || group.Key == 360) && key != null)
                {
                    if (string.Equals(key.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return DwgRecord.Hex(group.Value);
                    }

                    key = null;
                }
            }

            return 0;
        }
    }
}
