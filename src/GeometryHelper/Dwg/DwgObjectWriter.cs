using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Encodes the records of a DXF file into the objects of a DWG file, the reverse of <see cref="DwgObjectReader"/> and
    /// the transcoder: each record's groups are put back into the fields of its object in the order of the file, its
    /// handles into the handle stream, its texts from R2007 into the string stream. An object is its size, from R2010 the
    /// size of its handles, its data and its check sum. The field orders follow the Open Design Specification for .dwg
    /// files and the writer of ACadSharp (MIT), whose notice stands in THIRD-PARTY-NOTICES.md; each is the mirror of the
    /// reader's, which the round trip of the tests holds them to.
    /// </summary>
    internal sealed partial class DwgObjectWriter
    {
        private static readonly int[] LineWeights =
        {
            0,
            5,
            9,
            13,
            15,
            18,
            20,
            25,
            30,
            35,
            40,
            50,
            53,
            60,
            70,
            80,
            90,
            100,
            106,
            120,
            140,
            158,
            200,
            211,
        };

        private readonly DwgDrawing _drawing;
        private readonly int _version;
        private readonly Dictionary<string, int> _classes = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly HashSet<ulong> _written;
        private readonly ulong _modelSpace;
        private readonly ulong _paperSpace;
        private DwgBitWriter _data;
        private DwgBitWriter _strings;
        private DwgBitWriter _handles;
        private DwgRecord _record;
        private Dictionary<ulong, DwgRecord> _leaders;

        /// <summary>
        /// An encoder of the records of <paramref name="drawing"/> into objects of <paramref name="version"/> (such as 1032),
        /// whose types of 500 and up are numbered by <paramref name="classes"/> (the DXF names in their order), and which
        /// writes the handles of <paramref name="written"/> alone, 0 for any other.
        /// </summary>
        public DwgObjectWriter(DwgDrawing drawing, int version, IList<string> classes, HashSet<ulong> written)
        {
            _drawing = drawing;
            _version = version;
            _written = written;

            for (int i = 0; i < classes.Count; i++)
            {
                if (!_classes.ContainsKey(classes[i]))
                {
                    _classes[classes[i]] = 500 + i;
                }
            }

            _modelSpace = drawing.HandleOf("BLOCK_RECORD", "*Model_Space");
            _paperSpace = drawing.HandleOf("BLOCK_RECORD", "*Paper_Space");
        }

        private bool R2004Plus => _version >= 1018;

        private bool R2007Plus => _version >= 1021;

        private bool R2010Plus => _version >= 1024;

        private bool R2013Plus => _version >= 1027;

        private bool R2018Plus => _version >= 1032;

        /// <summary>The index a DWG stores for a lineweight in hundredths of a millimetre: 29 by layer, 30 by block, 31 the default.</summary>
        public static int LineWeightIndex(int lineWeight)
        {
            switch (lineWeight)
            {
                case -2:
                    return 30;
                case -3:
                    return 31;
            }

            int index = Array.IndexOf(LineWeights, lineWeight);
            return index >= 0 ? index : 29;
        }

        /// <summary>
        /// The type number of <paramref name="record"/> in the file, a fixed number or that of its class; -1 where its
        /// type is not written yet.
        /// </summary>
        public int TypeOf(DwgRecord record)
        {
            string type = record.Type;

            if (type.StartsWith("TABLE:", StringComparison.Ordinal))
            {
                switch (type.Substring(6))
                {
                    case "BLOCK_RECORD":
                        return 0x30;
                    case "LAYER":
                        return 0x32;
                    case "STYLE":
                        return 0x34;
                    case "LTYPE":
                        return 0x38;
                    case "VIEW":
                        return 0x3C;
                    case "UCS":
                        return 0x3E;
                    case "VPORT":
                        return 0x40;
                    case "APPID":
                        return 0x42;
                    case "DIMSTYLE":
                        return 0x44;
                    default:
                        return -1;
                }
            }

            if (_classes.TryGetValue(type, out int number) && Encoder(type) != null)
            {
                return type == "VISUALSTYLE" && !IsListedVisualStyle(record) && RoundTripOfVisualStyle(record) == null
                    ? -1
                    : number;
            }

            switch (type)
            {
                case "TEXT":
                    return 0x01;
                case "ATTRIB":
                    return Embedded(record) && !R2018Plus ? -1 : 0x02;
                case "ATTDEF":
                    return Embedded(record) && !R2018Plus ? -1 : 0x03;
                case "BLOCK":
                    return 0x04;
                case "ENDBLK":
                    return 0x05;
                case "SEQEND":
                    return 0x06;
                case "INSERT":
                    return record.Body.Exists(g => g.Key == 100 && g.Value.Trim() == "AcDbMInsertBlock") ? 0x08 : 0x07;
                case "VERTEX":
                    return VertexType(record);
                case "POLYLINE":
                    return PolylineType(record);
                case "DIMENSION":
                    return DimensionType(record);
                case "ARC":
                    return 0x11;
                case "CIRCLE":
                    return 0x12;
                case "LINE":
                    return 0x13;
                case "POINT":
                    return 0x1B;
                case "3DFACE":
                    return 0x1C;
                case "SOLID":
                    return 0x1F;
                case "TRACE":
                    return 0x20;
                case "SHAPE":
                    return ShapeNumber(record) > 0 && ShapeStyle() != 0 ? 0x21 : -1;
                case "VIEWPORT":
                    return 0x22;
                case "REGION":
                    return HasAcisData(record) ? 0x25 : -1;
                case "3DSOLID":
                    return HasAcisData(record) ? 0x26 : -1;
                case "BODY":
                    return HasAcisData(record) ? 0x27 : -1;
                case "LEADER":
                    return 0x2D;
                case "TOLERANCE":
                    return 0x2E;
                case "MLINE":
                    return 0x2F;
                case "HATCH":
                    return 0x4E;
                case "ELLIPSE":
                    return 0x23;
                case "SPLINE":
                    return 0x24;
                case "RAY":
                    return 0x28;
                case "XLINE":
                    return 0x29;
                case "DICTIONARY":
                    return 0x2A;
                case "MTEXT":
                    return 0x2C;
                case "BLOCK_RECORD":
                    return 0x31;
                case "LAYER":
                    return 0x33;
                case "STYLE":
                    return 0x35;
                case "LTYPE":
                    return 0x39;
                case "VIEW":
                    return 0x3D;
                case "UCS":
                    return 0x3F;
                case "VPORT":
                    return 0x41;
                case "APPID":
                    return 0x43;
                case "DIMSTYLE":
                    return 0x45;
                case "GROUP":
                    return 0x48;
                case "MLINESTYLE":
                    return 0x49;
                case "LWPOLYLINE":
                    return 0x4D;
                case "XRECORD":
                    return 0x4F;
                case "ACDBPLACEHOLDER":
                    return 0x50;
                case "LAYOUT":
                    return 0x52;
                default:
                    return -1;
            }
        }

        private static int VertexType(DwgRecord record)
        {
            foreach (KeyValuePair<int, string> group in record.Body)
            {
                if (group.Key != 100)
                {
                    continue;
                }

                switch (group.Value.Trim())
                {
                    case "AcDb2dVertex":
                        return 0x0A;
                    case "AcDb3dPolylineVertex":
                        return 0x0B;
                    case "AcDbPolygonMeshVertex":
                        return 0x0C;
                    case "AcDbPolyFaceMeshVertex":
                        return 0x0D;
                    case "AcDbFaceRecord":
                        return 0x0E;
                }
            }

            return 0x0A;
        }

        /// <summary>Whether an attribute is one of several lines, which holds an MTEXT of its own, written from R2018.</summary>
        private static bool Embedded(DwgRecord record) => record.Body.Exists(g => g.Key == 101);

        private static int DimensionType(DwgRecord record)
        {
            foreach (KeyValuePair<int, string> group in record.Body)
            {
                if (group.Key != 100)
                {
                    continue;
                }

                switch (group.Value.Trim())
                {
                    case "AcDbOrdinateDimension":
                        return 0x14;
                    case "AcDbRotatedDimension":
                        return 0x15;
                    case "AcDb3PointAngularDimension":
                        return 0x17;
                    case "AcDb2LineAngularDimension":
                        return 0x18;
                    case "AcDbRadialDimension":
                        return 0x19;
                    case "AcDbDiametricDimension":
                        return 0x1A;
                }
            }

            return record.Body.Exists(g => g.Key == 100 && g.Value.Trim() == "AcDbAlignedDimension") ? 0x16 : -1;
        }

        private static int PolylineType(DwgRecord record)
        {
            foreach (KeyValuePair<int, string> group in record.Body)
            {
                if (group.Key != 100)
                {
                    continue;
                }

                switch (group.Value.Trim())
                {
                    case "AcDb2dPolyline":
                        return 0x0F;
                    case "AcDb3dPolyline":
                        return 0x10;
                    case "AcDbPolyFaceMesh":
                        return 0x1D;
                    case "AcDbPolygonMesh":
                        return 0x1E;
                }
            }

            return 0x0F;
        }

        /// <summary>The encoder of the data of a type by its DXF name, for the types written by class; null for one that is not.</summary>
        private Action Encoder(string type)
        {
            switch (type)
            {
                case "ACDBDICTIONARYWDFLT":
                    return () => WriteDictionary(true);
                case "DICTIONARYVAR":
                    return WriteDictionaryVariable;
                case "SCALE":
                    return WriteScale;
                case "ACDBPLACEHOLDER":
                    return () => { };
                case "LAYOUT":
                    return WriteLayout;
                case "PLOTSETTINGS":
                    return WritePlotSettings;
                case "XRECORD":
                    return WriteXRecord;
                case "LWPOLYLINE":
                    return WriteLwPolyline;
                case "DBCOLOR":
                    return WriteBookColour;
                case "IMAGE":
                    return () => WriteRasterImage("AcDbRasterImage");
                case "WIPEOUT":
                    return () => WriteRasterImage("AcDbWipeout");
                case "IMAGEDEF":
                    return WriteImageDefinition;
                case "RASTERVARIABLES":
                    return WriteRasterVariables;
                case "SPATIAL_FILTER":
                    return WriteSpatialFilter;
                case "WIPEOUTVARIABLES":
                    return () => _data.BitShort(_record.Of("AcDbWipeoutVariables").Int(70, 1));
                case "ACAD_TABLE":
                    return R2010Plus ? WriteTable : (Action)null;
                case "TABLESTYLE":
                    return R2010Plus ? WriteTableStyle : (Action)null;
                case "TABLECONTENT":
                    return R2010Plus ? WriteTableContentObject : (Action)null;
                case "TABLEGEOMETRY":
                    return R2010Plus ? WriteTableGeometry : (Action)null;
                case "MULTILEADER":
                    return WriteMultiLeader;
                case "MLEADERSTYLE":
                    return WriteMultiLeaderStyle;
                case "IMAGEDEF_REACTOR":
                    return () => _data.BitLong(_record.Of("AcDbRasterImageDefReactor").Int(90, 2));
                case "MATERIAL":
                    return R2007Plus ? WriteMaterial : (Action)null;
                case "VISUALSTYLE":
                    return R2013Plus ? WriteVisualStyle : (Action)null;
                case "ACDBDETAILVIEWSTYLE":
                    return R2018Plus ? WriteDetailViewStyle : (Action)null;
                case "ACDBSECTIONVIEWSTYLE":
                    return R2018Plus ? WriteSectionViewStyle : (Action)null;
                case "SORTENTSTABLE":
                    return WriteSortEntitiesTable;
                case "FIELDLIST":
                    return WriteFieldList;
                case "FIELD":
                    return WriteField;
                case "DATALINK":
                    return WriteDataLink;
                case "ACDBASSOCPERSSUBENTMANAGER":
                    return WriteAssocPersSubentManager;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The bytes of the object of <paramref name="record"/>: its size as a modular short, from R2010 the size of its
        /// handles as a modular char, its data and its check sum. Null where its type is not written yet.
        /// </summary>
        public byte[] Write(DwgRecord record)
        {
            int type = TypeOf(record);

            if (type < 0)
            {
                return null;
            }

            _record = record;
            _data = new DwgBitWriter();
            _strings = new DwgBitWriter();
            _handles = new DwgBitWriter();

            if (R2010Plus)
            {
                _data.ObjectType(type);
            }
            else
            {
                _data.BitShort(type);

                // The size of the data in bits, before the handles: written when the object is done.
                _data.RawLong(0);
            }

            _data.Handle(0, record.Handle);
            WriteExtendedData();
            // An object of a class is an entity where its record is one (IMAGE, WIPEOUT); a fixed type says it by its number.
            bool entity =
                type < 500 ? IsEntity(type) : record.Body.Exists(g => g.Key == 100 && g.Value.Trim() == "AcDbEntity");

            if (entity)
            {
                WriteEntityCommon();
            }
            else
            {
                _data.BitLong(Written(record.Reactors).Count);

                if (R2004Plus)
                {
                    _data.Bit(!_written.Contains(record.XDictionary));
                }

                if (R2013Plus)
                {
                    _data.Bit(false);
                }

                Owner(record.Owner);
                WriteReactors();
            }

            WriteBody(type);
            return Finish();
        }

        private static bool IsEntity(int type) =>
            type < 0x2A || (type >= 0x2B && type <= 0x2F) || type == 0x4A || type == 0x4D || type == 0x4E;

        /// <summary>The data, the strings and the handles put together, with the sizes and the check sum around them.</summary>
        private byte[] Finish()
        {
            var body = new DwgBitWriter();
            body.Append(_data);

            if (R2007Plus)
            {
                long size = _strings.Position;

                if (size > 0)
                {
                    body.Append(_strings);

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
            }

            long handlesAt = body.Position;
            body.Append(_handles);
            body.PadToByte(true);
            byte[] bytes = body.ToArray();

            if (!R2010Plus)
            {
                // The raw long after the type: the bits of the data, from the start of the object, before the handles.
                var size = new DwgBitWriter();
                size.RawLong((int)handlesAt);
                byte[] value = size.ToArray();
                int typeBits = 0;
                var probe = new DwgBitReader(bytes);
                probe.BitShort();
                typeBits = (int)probe.Position;
                var patched = new DwgBitWriter();
                var reader = new DwgBitReader(bytes);
                patched.Bits(reader.Bits(typeBits), typeBits);
                reader.Bits(32);
                patched.Bytes(value);

                while (reader.Remaining > 0)
                {
                    patched.Bit(reader.Bit());
                }

                bytes = patched.ToArray();
            }

            var output = new DwgBitWriter();
            output.ModularShort(bytes.Length);

            if (R2010Plus)
            {
                output.ModularChar((bytes.Length * 8L) - handlesAt, false);
            }

            output.Bytes(bytes);
            byte[] head = output.ToArray();
            int crc = DwgCrc.Of(head, 0, head.Length, DwgCrc.Seed);
            var result = new byte[head.Length + 2];
            Array.Copy(head, result, head.Length);
            result[head.Length] = (byte)crc;
            result[head.Length + 1] = (byte)(crc >> 8);
            return result;
        }

        /// <summary>A handle in the handle stream, of <paramref name="code"/>; 0 where the object it names is not written.</summary>
        private void H(int code, ulong handle)
        {
            _handles.Handle(code, handle != 0 && _written.Contains(handle) ? handle : 0);
        }

        /// <summary>
        /// The owner, as AutoCAD writes it: by its step from the object's own handle where that is one or the smaller number
        /// to write (codes 6 and 8 for one after and one before, 0xA and 0xC for more), else as itself (code 4): the owner of
        /// the block record that follows its table is written 8, one before, though its own handle 1 is as short.
        /// </summary>
        private void Owner(ulong owner)
        {
            ulong own = _record.Handle;

            if (owner == 0 || !_written.Contains(owner))
            {
                _handles.Handle(4, 0);
                return;
            }

            ulong step = owner > own ? owner - own : own - owner;

            if (step == 1 || step < owner)
            {
                _handles.RelativeHandle(own, owner);
            }
            else
            {
                _handles.Handle(4, owner);
            }
        }

        private List<ulong> Written(List<ulong> handles)
        {
            return handles.FindAll(handle => _written.Contains(handle));
        }

        private void WriteReactors()
        {
            foreach (ulong reactor in Written(_record.Reactors))
            {
                H(4, reactor);
            }

            if (!R2004Plus || _written.Contains(_record.XDictionary))
            {
                H(3, _record.XDictionary);
            }
        }

        /// <summary>A text of the object: before R2007 in the data in the drawing's code page, from R2007 in the string stream as UTF-16.</summary>
        private void T(string text)
        {
            text = text ?? string.Empty;

            if (R2007Plus)
            {
                _strings.TextUnicode(text);
            }
            else
            {
                _data.Text(Encoding.UTF8.GetBytes(text));
            }
        }

        /// <summary>
        /// Whether the application at <paramref name="start"/> is the stamp <c>AcDbSavedByObjectVersion</c> of ACAD on a visual
        /// style of R2010 or before that is written as R2013 or later: AutoCAD brings the style up to its own version and keeps
        /// no stamp.
        /// </summary>
        private bool IsVersionStampOfConvertedStyle(List<KeyValuePair<int, string>> groups, int start) =>
            start + 1 < groups.Count
            && groups[start].Value.Trim() == "ACAD"
            && groups[start + 1].Value.Trim() == "AcDbSavedByObjectVersion"
            && RoundTripOfVisualStyle(_record) != null;

        /// <summary>
        /// The extended data: for each application its size in bytes, the handle of its APPID and its values, each a byte of
        /// its DXF group less 1000 and the value; then a size of 0.
        /// </summary>
        private void WriteExtendedData()
        {
            List<KeyValuePair<int, string>> groups = _record.XData;
            int i = 0;

            while (i < groups.Count)
            {
                if (groups[i].Key != 1001)
                {
                    i++;
                    continue;
                }

                ulong application = _drawing.HandleOf("APPID", groups[i].Value);
                int start = i;
                var values = new System.IO.MemoryStream();

                for (i++; i < groups.Count && groups[i].Key != 1001; i++)
                {
                    WriteExtendedValue(values, groups, ref i);
                }

                if (
                    application == 0
                    || !_written.Contains(application)
                    || IsVersionStampOfConvertedStyle(groups, start)
                )
                {
                    continue;
                }

                byte[] bytes = values.ToArray();
                _data.BitShort(bytes.Length);
                _data.Handle(5, application);
                _data.Bytes(bytes);
            }

            _data.BitShort(0);
        }

        private void WriteExtendedValue(System.IO.Stream values, List<KeyValuePair<int, string>> groups, ref int i)
        {
            int code = groups[i].Key;
            string value = groups[i].Value;

            switch (code)
            {
                case 1000:
                    values.WriteByte(0);

                    if (R2007Plus)
                    {
                        string text = value;
                        values.WriteByte((byte)text.Length);
                        values.WriteByte((byte)(text.Length >> 8));
                        byte[] utf16 = Encoding.Unicode.GetBytes(text);
                        values.Write(utf16, 0, utf16.Length);
                    }
                    else
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(value);
                        values.WriteByte((byte)bytes.Length);
                        values.WriteByte(30);
                        values.WriteByte(0);
                        values.Write(bytes, 0, bytes.Length);
                    }

                    break;
                case 1002:
                    values.WriteByte(2);
                    values.WriteByte((byte)(value.Trim() == "}" ? 1 : 0));
                    break;
                case 1003:
                    values.WriteByte(3);
                    BigEndian(values, _drawing.HandleOf("LAYER", value));
                    break;
                case 1004:
                    values.WriteByte(4);
                    byte[] chunk = Unhex(value);
                    values.WriteByte((byte)chunk.Length);
                    values.Write(chunk, 0, chunk.Length);
                    break;
                case 1005:
                    values.WriteByte(5);
                    BigEndian(values, DwgRecord.Hex(value));
                    break;
                case 1010:
                case 1011:
                case 1012:
                case 1013:
                    values.WriteByte((byte)(code - 1000));
                    double x = DwgRecord.Double(value);
                    double y =
                        i + 1 < groups.Count && groups[i + 1].Key == code + 10
                            ? DwgRecord.Double(groups[++i].Value)
                            : 0.0;
                    double z =
                        i + 1 < groups.Count && groups[i + 1].Key == code + 20
                            ? DwgRecord.Double(groups[++i].Value)
                            : 0.0;
                    RawDouble(values, x);
                    RawDouble(values, y);
                    RawDouble(values, z);
                    break;
                case 1040:
                case 1041:
                case 1042:
                    values.WriteByte((byte)(code - 1000));
                    RawDouble(values, DwgRecord.Double(value));
                    break;
                case 1070:
                    values.WriteByte(70);
                    int s = DwgRecord.Int(value);
                    values.WriteByte((byte)s);
                    values.WriteByte((byte)(s >> 8));
                    break;
                case 1071:
                    values.WriteByte(71);
                    int l = DwgRecord.Int(value);
                    values.WriteByte((byte)l);
                    values.WriteByte((byte)(l >> 8));
                    values.WriteByte((byte)(l >> 16));
                    values.WriteByte((byte)(l >> 24));
                    break;
            }
        }

        private static void BigEndian(System.IO.Stream values, ulong handle)
        {
            for (int k = 7; k >= 0; k--)
            {
                values.WriteByte((byte)(handle >> (8 * k)));
            }
        }

        private static void RawDouble(System.IO.Stream values, double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            values.Write(bytes, 0, 8);
        }

        /// <summary>A run of bytes written in hex, as DXF writes the groups 310 and 1004.</summary>
        internal static byte[] Unhex(string hex)
        {
            hex = hex.Trim();
            var bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                byte.TryParse(
                    hex.Substring(2 * i, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out bytes[i]
                );
            }

            return bytes;
        }

        /// <summary>
        /// What every entity carries after its extended data: proxy graphics, how it is owned, its reactors and extension
        /// dictionary, its colour, linetype scale, linetype, material, plot style, visibility and lineweight, with their
        /// handles.
        /// </summary>
        private void WriteEntityCommon()
        {
            DwgGroups all = _record.Of("AcDbEntity");
            byte[] graphics = ProxyGraphics(all);
            _data.Bit(graphics != null);

            if (graphics != null)
            {
                if (R2010Plus)
                {
                    _data.BitLongLong(graphics.Length);
                }
                else
                {
                    _data.RawLong(graphics.Length);
                }

                _data.Bytes(graphics);
            }

            ulong owner = _record.Container != 0 ? _record.Container : _record.Owner;
            int mode =
                owner == _modelSpace && owner != 0 ? 2
                : owner == _paperSpace && owner != 0 ? 1
                : 0;

            if (_record.Type == "VERTEX" || _record.Type == "ATTRIB" || _record.Type == "SEQEND")
            {
                mode = 0;
            }

            _data.Bits2(mode);
            _data.BitLong(Written(_record.Reactors).Count);

            if (R2004Plus)
            {
                _data.Bit(!_written.Contains(_record.XDictionary));
            }

            if (R2013Plus)
            {
                // Whether the entity's data is in the data store: an ACIS entity's is.
                _data.Bit(_record.Type == "REGION" || _record.Type == "3DSOLID" || _record.Type == "BODY");
            }

            if (!R2004Plus)
            {
                // No links: AutoCAD finds the next entity of an owner from the owner's list from R2004, and before from these.
                _data.Bit(false);
            }

            if (mode == 0)
            {
                Owner(owner);
            }

            WriteReactors();

            if (!R2004Plus)
            {
                _handles.Handle(4, 0);
                _handles.Handle(4, 0);
            }

            WriteEntityColour(all);
            _data.BitDouble(all.Double(48, 1.0));
            string linetype = all.Text(6, "ByLayer").Trim();
            int linetypeKind =
                linetype.Equals("ByLayer", StringComparison.OrdinalIgnoreCase) ? 0
                : linetype.Equals("ByBlock", StringComparison.OrdinalIgnoreCase) ? 1
                : linetype.Equals("Continuous", StringComparison.OrdinalIgnoreCase) ? 2
                : 3;
            ulong linetypeHandle = linetypeKind == 3 ? _drawing.HandleOf("LTYPE", linetype) : 0;

            if (linetypeKind == 3 && !_written.Contains(linetypeHandle))
            {
                linetypeKind = 0;
            }

            _data.Bits2(linetypeKind);
            ulong material = all.Handle(347);
            bool hasMaterial = material != 0 && _written.Contains(material);

            if (R2007Plus)
            {
                _data.Bits2(hasMaterial ? 3 : 0);
                _data.RawChar(all.Int(284, 0));
            }

            ulong plotStyle = all.Handle(390);
            bool hasPlotStyle = plotStyle != 0 && _written.Contains(plotStyle);

            // AutoCAD gives a SEQEND a plot style of its own, a null handle.
            bool nullPlotStyle = _record.Type == "SEQEND";
            _data.Bits2(hasPlotStyle || nullPlotStyle ? 3 : 0);

            if (R2010Plus)
            {
                _data.Bit(false);
                _data.Bit(false);
                _data.Bit(false);
            }

            _data.BitShort(all.Int(60, 0));
            _data.RawChar(LineWeightIndex(all.Int(370, -1)));
            H(5, _drawing.HandleOf("LAYER", all.Text(8, "0")));

            if (linetypeKind == 3)
            {
                H(5, linetypeHandle);
            }

            if (R2007Plus && hasMaterial)
            {
                H(5, material);
            }

            if (hasPlotStyle || nullPlotStyle)
            {
                H(5, hasPlotStyle ? plotStyle : 0);
            }
        }

        /// <summary>The proxy graphics of an entity: the size under 92 (from R2010 160), then pieces in hex under 310; null for none.</summary>
        private static byte[] ProxyGraphics(DwgGroups all)
        {
            int size = all.Int(160, all.Int(92, 0));

            if (size <= 0)
            {
                return null;
            }

            var bytes = new List<byte>(size);

            foreach (string piece in all.Each(310))
            {
                bytes.AddRange(Unhex(piece));
            }

            return bytes.Count >= size ? bytes.GetRange(0, size).ToArray() : bytes.ToArray();
        }

        /// <summary>
        /// The colour of an entity: before R2004 a bit short of the index; from R2004 (ENC) a bit short of the index with
        /// 0x8000 for a true colour (a bit long follows) and 0x2000 for a transparency (a bit long follows).
        /// </summary>
        private void WriteEntityColour(DwgGroups all)
        {
            int index = all.Int(62, 256);

            if (!R2004Plus)
            {
                _data.BitShort(index);
                return;
            }

            bool rgb = all.Has(420);
            bool transparency = all.Has(440);
            ulong book = all.Has(430) ? BookColourOf(all.Text(430)) : 0;

            if (book != 0)
            {
                // A colour of a book: its DBCOLOR, named in the handles, holds the true colour and the names.
                _data.BitShort((index & 0x1FF) | 0x8000 | 0x4000 | (transparency ? 0x2000 : 0));

                if (transparency)
                {
                    _data.BitLong(all.Int(440));
                }

                H(5, book);
                return;
            }

            int value = (index & 0x1FF) | (rgb ? 0x8000 : 0) | (transparency ? 0x2000 : 0);
            _data.BitShort(value);

            if (rgb)
            {
                _data.BitLong(unchecked((int)0xC2000000) | (all.Int(420) & 0xFFFFFF));
            }

            if (transparency)
            {
                _data.BitLong(all.Int(440));
            }
        }

        /// <summary>
        /// A colour of a table record or a style (CMC): before R2004 a bit short of the index; from R2004 a bit short of 0,
        /// a bit long whose high byte says how it is given (0xC0 by layer, 0xC1 by block, 0xC2 a true colour, 0xC3 an
        /// index) and a byte of 0, no names following.
        /// </summary>
        private void WriteColour(int index, int rgb = -1, string bookName = null)
        {
            if (!R2004Plus)
            {
                _data.BitShort(index);
                return;
            }

            _data.BitShort(0);
            int value =
                rgb >= 0 ? unchecked((int)0xC2000000) | (rgb & 0xFFFFFF)
                : index == 256 ? unchecked((int)0xC0000000)
                : index == 0 ? unchecked((int)0xC1000000)
                : unchecked((int)0xC3000000) | (index & 0xFF);
            _data.BitLong(value);
            int split = string.IsNullOrEmpty(bookName) ? -1 : bookName.IndexOf('$');

            if (split < 0)
            {
                _data.RawChar(0);
                return;
            }

            // The names of a colour of a book, group 430 as book$colour: the colour's name, then the book's.
            _data.RawChar(3);
            T(bookName.Substring(split + 1));
            T(bookName.Substring(0, split));
        }

        /// <summary>A DBCOLOR, a colour of a book: its colour with its name and its book.</summary>
        private void WriteBookColour()
        {
            DwgGroups colour = _record.Of("AcDbColor");

            if (!R2004Plus)
            {
                _data.BitShort(colour.Int(62));
                return;
            }

            WriteColour(colour.Int(62), colour.Has(420) ? colour.Int(420) : -1, colour.Text(430));
        }

        /// <summary>The written DBCOLOR whose name (group 430) is <paramref name="name"/>; 0 for none.</summary>
        private ulong BookColourOf(string name)
        {
            DwgRecord found = _drawing.Objects.Find(record =>
                record.Type == "DBCOLOR"
                && _written.Contains(record.Handle)
                && string.Equals(record.Of("AcDbColor").Text(430).Trim(), name.Trim(), StringComparison.Ordinal)
            );
            return found?.Handle ?? 0;
        }

        private static double Radians(double degrees) => degrees * Math.PI / 180.0;

        private static bool IsDefaultExtrusion(double[] extrusion) =>
            extrusion[0] == 0.0 && extrusion[1] == 0.0 && extrusion[2] == 1.0;

        private void WriteBody(int type)
        {
            switch (type)
            {
                case 0x01:
                    WriteText(0);
                    return;
                case 0x02:
                    WriteText(1);
                    return;
                case 0x03:
                    WriteText(2);
                    return;
                case 0x04:
                    T(_record.Of("AcDbBlockBegin").Text(2));
                    return;
                case 0x05:
                case 0x06:
                    return;
                case 0x07:
                    WriteInsert(false);
                    return;
                case 0x08:
                    WriteInsert(true);
                    return;
                case 0x0A:
                    WriteVertex2D();
                    return;
                case 0x0B:
                case 0x0C:
                case 0x0D:
                    WriteVertex3D();
                    return;
                case 0x0E:
                    WriteFaceRecord();
                    return;
                case 0x0F:
                    WritePolyline2D();
                    return;
                case 0x10:
                    WritePolyline3D();
                    return;
                case 0x11:
                    WriteCircle(true);
                    return;
                case 0x12:
                    WriteCircle(false);
                    return;
                case 0x13:
                    WriteLine();
                    return;
                case 0x14:
                case 0x15:
                case 0x16:
                case 0x17:
                case 0x18:
                case 0x19:
                case 0x1A:
                    WriteDimension(type);
                    return;
                case 0x1B:
                    WritePoint();
                    return;
                case 0x1C:
                    WriteFace();
                    return;
                case 0x1D:
                    WritePolyfaceMesh();
                    return;
                case 0x1E:
                    WritePolygonMesh();
                    return;
                case 0x1F:
                case 0x20:
                    WriteSolid();
                    return;
                case 0x21:
                    WriteShape();
                    return;
                case 0x22:
                    WriteViewport();
                    return;
                case 0x25:
                case 0x27:
                    WriteModelerGeometry(false);
                    return;
                case 0x26:
                    WriteModelerGeometry(true);
                    return;
                case 0x2D:
                    WriteLeader();
                    return;
                case 0x2E:
                    WriteTolerance();
                    return;
                case 0x2F:
                    WriteMLine();
                    return;
                case 0x4E:
                    WriteHatch();
                    return;
                case 0x23:
                    WriteEllipse();
                    return;
                case 0x24:
                    WriteSpline();
                    return;
                case 0x28:
                case 0x29:
                    WriteRay();
                    return;
                case 0x2A:
                    WriteDictionary(false);
                    return;
                case 0x2C:
                    WriteMText();
                    return;
                case 0x30:
                case 0x32:
                case 0x34:
                case 0x38:
                case 0x3C:
                case 0x3E:
                case 0x40:
                case 0x42:
                case 0x44:
                    WriteControl(type);
                    return;
                case 0x31:
                    WriteBlockHeader();
                    return;
                case 0x33:
                    WriteLayer();
                    return;
                case 0x35:
                    WriteStyle();
                    return;
                case 0x39:
                    WriteLinetype();
                    return;
                case 0x3D:
                    WriteView();
                    return;
                case 0x3F:
                    WriteUcs();
                    return;
                case 0x41:
                    WriteVport();
                    return;
                case 0x43:
                    WriteAppId();
                    return;
                case 0x45:
                    WriteDimStyle();
                    return;
                case 0x48:
                    WriteGroup();
                    return;
                case 0x49:
                    WriteMLineStyle();
                    return;
                case 0x4D:
                    WriteLwPolyline();
                    return;
                case 0x4F:
                    WriteXRecord();
                    return;
                case 0x50:
                    return;
                case 0x52:
                    WriteLayout();
                    return;
            }

            Encoder(_record.Type)?.Invoke();
        }
    }
}
