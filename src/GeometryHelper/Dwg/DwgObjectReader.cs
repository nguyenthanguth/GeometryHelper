using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <summary>A class of the classes section: the names an object type of 500 and up stands for.</summary>
    internal sealed class DwgClass
    {
        /// <summary>The number of the class; an object of this class carries it as its type.</summary>
        public int Number;

        /// <summary>The proxy flags, group 90 of the DXF class.</summary>
        public int ProxyFlags;

        /// <summary>The name of the application, group 3.</summary>
        public byte[] Application;

        /// <summary>The name of the C++ class, group 2.</summary>
        public byte[] CppName;

        /// <summary>The name of the DXF record, group 1.</summary>
        public byte[] DxfName;

        /// <summary>Whether the class was a proxy when the file was written, group 280.</summary>
        public bool WasZombie;

        /// <summary>Whether objects of the class are entities, group 281.</summary>
        public bool IsEntity;

        /// <summary>The classes of a file, read from the data of their section.</summary>
        public static List<DwgClass> ReadAll(byte[] data)
        {
            var classes = new List<DwgClass>();
            var reader = new DwgBitReader(data);

            // The data is padded to whole bytes; a class takes far more than the three bytes asked for here.
            while (reader.Remaining >= 24)
            {
                var one = new DwgClass();
                one.Number = reader.BitShort();
                one.ProxyFlags = reader.BitShort();
                one.Application = reader.Text();
                one.CppName = reader.Text();
                one.DxfName = reader.Text();
                one.WasZombie = reader.Bit();
                one.IsEntity = reader.BitShort() == 0x1F2;
                classes.Add(one);
            }

            return classes;
        }
    }

    /// <summary>
    /// Decodes the objects of a DWG file of R2000 into <see cref="DwgObject"/> records. An object is its size, its type,
    /// the data every object carries, the data of its type, and at the end the handles the data refers to; the two runs
    /// are read side by side. The field orders follow the Open Design Specification for .dwg files and the reader of
    /// ACadSharp (MIT), whose notice stands in THIRD-PARTY-NOTICES.md.
    /// </summary>
    internal sealed partial class DwgObjectReader
    {
        private static readonly int[] LineWeights =
        {
            0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211,
        };

        private static readonly Dictionary<int, string> FixedTypes = new Dictionary<int, string>
        {
            { 0x01, "TEXT" }, { 0x02, "ATTRIB" }, { 0x03, "ATTDEF" }, { 0x04, "BLOCK" }, { 0x05, "ENDBLK" }, { 0x06, "SEQEND" },
            { 0x07, "INSERT" }, { 0x08, "INSERT" }, { 0x0A, "VERTEX" }, { 0x0B, "VERTEX" }, { 0x0C, "VERTEX" }, { 0x0D, "VERTEX" },
            { 0x0E, "VERTEX" }, { 0x0F, "POLYLINE" }, { 0x10, "POLYLINE" }, { 0x11, "ARC" }, { 0x12, "CIRCLE" }, { 0x13, "LINE" },
            { 0x14, "DIMENSION" }, { 0x15, "DIMENSION" }, { 0x16, "DIMENSION" }, { 0x17, "DIMENSION" }, { 0x18, "DIMENSION" },
            { 0x19, "DIMENSION" }, { 0x1A, "DIMENSION" }, { 0x1B, "POINT" }, { 0x1C, "3DFACE" }, { 0x1D, "POLYLINE" },
            { 0x1E, "POLYLINE" }, { 0x1F, "SOLID" }, { 0x20, "TRACE" }, { 0x21, "SHAPE" }, { 0x22, "VIEWPORT" }, { 0x23, "ELLIPSE" },
            { 0x24, "SPLINE" }, { 0x25, "REGION" }, { 0x26, "3DSOLID" }, { 0x27, "BODY" }, { 0x28, "RAY" }, { 0x29, "XLINE" },
            { 0x2A, "DICTIONARY" }, { 0x2B, "OLEFRAME" }, { 0x2C, "MTEXT" }, { 0x2D, "LEADER" }, { 0x2E, "TOLERANCE" },
            { 0x2F, "MLINE" }, { 0x30, "BLOCK_RECORD" }, { 0x31, "BLOCK_RECORD" }, { 0x32, "LAYER" }, { 0x33, "LAYER" },
            { 0x34, "STYLE" }, { 0x35, "STYLE" }, { 0x38, "LTYPE" }, { 0x39, "LTYPE" }, { 0x3C, "VIEW" }, { 0x3D, "VIEW" },
            { 0x3E, "UCS" }, { 0x3F, "UCS" }, { 0x40, "VPORT" }, { 0x41, "VPORT" }, { 0x42, "APPID" }, { 0x43, "APPID" },
            { 0x44, "DIMSTYLE" }, { 0x45, "DIMSTYLE" }, { 0x46, "VP_ENT_HDR" }, { 0x47, "VP_ENT_HDR" }, { 0x48, "GROUP" },
            { 0x49, "MLINESTYLE" }, { 0x4A, "OLE2FRAME" }, { 0x4B, "DUMMY" }, { 0x4C, "LONG_TRANSACTION" }, { 0x4D, "LWPOLYLINE" },
            { 0x4E, "HATCH" }, { 0x4F, "XRECORD" }, { 0x50, "ACDBPLACEHOLDER" }, { 0x51, "VBA_PROJECT" }, { 0x52, "LAYOUT" },
        };

        private readonly byte[] _file;
        private readonly Dictionary<int, DwgClass> _classes = new Dictionary<int, DwgClass>();
        private DwgBitReader _data;
        private DwgBitReader _handles;
        private DwgObject _object;

        /// <summary>A reader of the objects in <paramref name="file"/>, whose types of 500 and up are of <paramref name="classes"/>.</summary>
        public DwgObjectReader(byte[] file, List<DwgClass> classes)
        {
            _file = file;

            foreach (DwgClass one in classes)
            {
                _classes[one.Number] = one;
            }
        }

        /// <summary>The lineweight in hundredths of a millimetre for the index a DWG stores: -1 by layer, -2 by block, -3 the default.</summary>
        public static int LineWeight(int index)
        {
            if (index >= 0 && index < LineWeights.Length)
            {
                return LineWeights[index];
            }

            return index == 30 ? -2 : index == 31 ? -3 : -1;
        }

        /// <summary>
        /// Decodes the object that the object map places at <paramref name="offset"/> under <paramref name="handle"/>.
        /// Throws <see cref="FormatException"/> where the object is broken: it runs past the file, its check sum is wrong,
        /// it carries another handle, or its data end before the last value of its type.
        /// </summary>
        public DwgObject Read(ulong handle, long offset)
        {
            var head = new DwgBitReader(_file, offset * 8, _file.LongLength * 8);
            long size = head.ModularShort();
            long start = head.Position / 8;

            if (size <= 0 || start + size + 2 > _file.LongLength)
            {
                throw new FormatException("The object runs past the end of the file.");
            }

            int crc = _file[start + size] | (_file[start + size + 1] << 8);

            if (crc != DwgCrc.Of(_file, offset, start - offset + size, DwgCrc.Seed))
            {
                throw new FormatException("The object has a wrong check sum.");
            }

            var data = new DwgBitReader(_file, start * 8, (start + size) * 8);
            int type = data.BitShort();
            long handlesAt = (start * 8) + (uint)data.RawLong();

            if (handlesAt < data.Position || handlesAt > (start + size) * 8)
            {
                throw new FormatException("The object places its handles outside itself.");
            }

            _data = new DwgBitReader(_file, data.Position, handlesAt);
            _handles = new DwgBitReader(_file, handlesAt, (start + size) * 8);
            _object = new DwgObject { Type = type };
            _object.Handle = _data.Handle();

            if (_object.Handle != handle)
            {
                throw new FormatException("The object carries the handle " + _object.Handle.ToString("X", CultureInfo.InvariantCulture) + ", not the one the object map gives it.");
            }

            DwgClass ofClass = null;

            if (FixedTypes.TryGetValue(type, out string name))
            {
                _object.Name = name;
                _object.IsEntity = type < 0x2A || (type >= 0x2B && type <= 0x2F) || type == 0x4A || type == 0x4D || type == 0x4E;
            }
            else if (_classes.TryGetValue(type, out ofClass))
            {
                _object.Name = Encoding.ASCII.GetString(ofClass.DxfName);
                _object.IsEntity = ofClass.IsEntity;
            }
            else
            {
                _object.Name = "TYPE" + type.ToString(CultureInfo.InvariantCulture);
            }

            ReadExtendedData();

            if (_object.IsEntity)
            {
                ReadEntityCommon();
            }
            else
            {
                int reactors = Count(_data.BitLong());
                _object.Owner = H();
                ReadReactors(reactors);
            }

            _object.Decoded = ReadBody(type);
            return _object;
        }

        /// <summary>The next handle of the handles of the object, counted from the object's own where it is a relative one.</summary>
        private ulong H() => _handles.Handle(_object.Handle);

        /// <summary>A count read from the file, refused where the object cannot hold that many values of even one bit each.</summary>
        private int Count(long count)
        {
            if (count < 0 || count > _data.Remaining + _handles.Remaining)
            {
                throw new FormatException("The object names more values than it has room for.");
            }

            return (int)count;
        }

        private void ReadReactors(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _object.Reactors.Add(H());
            }

            _object.XDictionary = H();
        }

        private void ReadEntityCommon()
        {
            if (_data.Bit())
            {
                // The proxy graphics: a picture of the entity for the programs that cannot draw it.
                int graphics = Count(_data.RawLong());
                _data.Position += graphics * 8L;
            }

            _object.EntityMode = _data.Bits2();
            int reactors = Count(_data.BitLong());
            bool noLinks = _data.Bit();
            int colour = _data.BitShort();
            double linetypeScale = _data.BitDouble();
            int linetype = _data.Bits2();
            int plotStyle = _data.Bits2();
            int invisible = _data.BitShort();
            int lineWeight = LineWeight(_data.RawChar());

            if (_object.EntityMode == 0)
            {
                _object.Owner = H();
            }

            ReadReactors(reactors);

            if (noLinks)
            {
                _object.Next = _object.Handle + 1;
            }
            else
            {
                H();
                _object.Next = H();
            }

            _object.Layer = H();

            switch (linetype)
            {
                case 1: _object.Common.Add(new DwgGroup(6, "ByBlock")); break;
                case 2: _object.Common.Add(new DwgGroup(6, "Continuous")); break;
                case 3: _object.Common.Add(new DwgGroup(6, new DwgName(H(), "ByLayer"))); break;
            }

            if (plotStyle == 3)
            {
                H();
            }

            if (colour != 256)
            {
                _object.Common.Add(new DwgGroup(62, colour));
            }

            if (linetypeScale != 1.0)
            {
                _object.Common.Add(new DwgGroup(48, linetypeScale));
            }

            if ((invisible & 1) != 0)
            {
                _object.Common.Add(new DwgGroup(60, 1));
            }

            if (lineWeight != -1)
            {
                _object.Common.Add(new DwgGroup(370, lineWeight));
            }
        }

        /// <summary>
        /// The extended data: for each application a size, the handle of its APPID and that many bytes of values, each a
        /// byte saying its kind (the DXF group less 1000) and the value.
        /// </summary>
        private void ReadExtendedData()
        {
            while (true)
            {
                int size = _data.BitShort();

                if (size == 0)
                {
                    return;
                }

                ulong application = _data.Handle(_object.Handle);
                long end = _data.Position + (size * 8L);

                if (size < 0 || end > _data.End)
                {
                    throw new FormatException("The extended data of the object run past its end.");
                }

                var values = new DwgBitReader(_file, _data.Position, end);
                _data.Position = end;
                _object.XData.Add(new DwgGroup(1001, new DwgName(application)));

                while (values.Remaining >= 8)
                {
                    int kind = values.RawChar();

                    switch (kind)
                    {
                        case 0:
                            int length = values.RawChar();
                            values.RawShort();
                            _object.XData.Add(new DwgGroup(1000, values.Bytes(length)));
                            break;
                        case 2:
                            _object.XData.Add(new DwgGroup(1002, values.RawChar() == 0 ? "{" : "}"));
                            break;
                        case 3:
                            _object.XData.Add(new DwgGroup(1003, new DwgName(BigEndianHandle(values))));
                            break;
                        case 4:
                            _object.XData.Add(new DwgGroup(1004, Hex(values.Bytes(values.RawChar()))));
                            break;
                        case 5:
                            _object.XData.Add(new DwgGroup(1005, BigEndianHandle(values)));
                            break;
                        case 10:
                        case 11:
                        case 12:
                        case 13:
                            double[] point = values.RawPoint3();
                            _object.XData.Add(new DwgGroup(1000 + kind, point[0]));
                            _object.XData.Add(new DwgGroup(1010 + kind, point[1]));
                            _object.XData.Add(new DwgGroup(1020 + kind, point[2]));
                            break;
                        case 40:
                        case 41:
                        case 42:
                            _object.XData.Add(new DwgGroup(1000 + kind, values.RawDouble()));
                            break;
                        case 70:
                            _object.XData.Add(new DwgGroup(1070, values.RawShort()));
                            break;
                        case 71:
                            _object.XData.Add(new DwgGroup(1071, values.RawLong()));
                            break;
                        default:
                            throw new FormatException("The extended data of the object have a value of the unknown kind " + kind.ToString(CultureInfo.InvariantCulture) + ".");
                    }
                }
            }
        }

        private static ulong BigEndianHandle(DwgBitReader reader)
        {
            ulong handle = 0;

            for (int i = 0; i < 8; i++)
            {
                handle = (handle << 8) | reader.RawChar();
            }

            return handle;
        }

        private static string Hex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
            {
                text.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        private static double Degrees(double radians) => radians * 180.0 / Math.PI;

        private static bool IsDefaultExtrusion(double[] extrusion) => extrusion[0] == 0.0 && extrusion[1] == 0.0 && extrusion[2] == 1.0;

        /// <summary>Adds a group where <paramref name="value"/> is not the 0 that DXF leaves out.</summary>
        private void AddNonZero(int code, double value)
        {
            if (value != 0.0)
            {
                _object.Add(code, value);
            }
        }

        /// <summary>Adds a group where <paramref name="value"/> is not the 0 that DXF leaves out.</summary>
        private void AddNonZero(int code, int value)
        {
            if (value != 0)
            {
                _object.Add(code, value);
            }
        }

        private void AddExtrusion(double[] extrusion)
        {
            if (!IsDefaultExtrusion(extrusion))
            {
                _object.AddPoint(210, extrusion);
            }
        }

        /// <summary>Decodes the data of the type; false for a type that is not read.</summary>
        private bool ReadBody(int type)
        {
            switch (type)
            {
                case 0x01: ReadText(0); return true;
                case 0x02: ReadText(1); return true;
                case 0x03: ReadText(2); return true;
                case 0x04: _object.RecordName = _data.Text(); return true;
                case 0x05: _object.Add(100, "AcDbBlockEnd"); return true;
                case 0x06: return true;
                case 0x07: ReadInsert(false); return true;
                case 0x08: ReadInsert(true); return true;
                case 0x0A: ReadVertex2D(); return true;
                case 0x0B: ReadVertex3D("AcDb3dPolylineVertex"); return true;
                case 0x0C: ReadVertex3D("AcDbPolygonMeshVertex"); return true;
                case 0x0D: ReadVertex3D("AcDbPolyFaceMeshVertex"); return true;
                case 0x0E: ReadFaceRecord(); return true;
                case 0x0F: ReadPolyline2D(); return true;
                case 0x10: ReadPolyline3D(); return true;
                case 0x11: ReadCircle(true); return true;
                case 0x12: ReadCircle(false); return true;
                case 0x13: ReadLine(); return true;
                case 0x14:
                case 0x15:
                case 0x16:
                case 0x17:
                case 0x18:
                case 0x19:
                case 0x1A: ReadDimension(type); return true;
                case 0x1B: ReadPoint(); return true;
                case 0x1C: ReadFace(); return true;
                case 0x1D: ReadPolyfaceMesh(); return true;
                case 0x1E: ReadPolygonMesh(); return true;
                case 0x1F:
                case 0x20: ReadSolid(); return true;
                case 0x23: ReadEllipse(); return true;
                case 0x24: ReadSpline(); return true;
                case 0x28: ReadRay("AcDbRay"); return true;
                case 0x29: ReadRay("AcDbXline"); return true;
                case 0x2A: ReadDictionary(false); return true;
                case 0x2C: ReadMText(); return true;
                case 0x30:
                case 0x32:
                case 0x34:
                case 0x38:
                case 0x3C:
                case 0x3E:
                case 0x40:
                case 0x42:
                case 0x44:
                case 0x46: ReadControl(type); return true;
                case 0x31: ReadBlockHeader(); return true;
                case 0x33: ReadLayer(); return true;
                case 0x35: ReadStyle(); return true;
                case 0x39: ReadLinetype(); return true;
                case 0x3D: ReadView(); return true;
                case 0x3F: ReadUcs(); return true;
                case 0x41: ReadVport(); return true;
                case 0x43: ReadAppId(); return true;
                case 0x45: ReadDimStyle(); return true;
                case 0x48: ReadGroup(); return true;
                case 0x49: ReadMLineStyle(); return true;
                case 0x4D: ReadLwPolyline(); return true;
                case 0x4F: ReadXRecord(); return true;
                case 0x50: return true;
                case 0x52: ReadLayout(); return true;
            }

            switch (_object.Name)
            {
                case "ACDBDICTIONARYWDFLT": ReadDictionary(true); return true;
                case "DICTIONARYVAR": ReadDictionaryVariable(); return true;
                case "SCALE": ReadScale(); return true;
                case "ACDBPLACEHOLDER": return true;
                case "LAYOUT": ReadLayout(); return true;
                case "XRECORD": ReadXRecord(); return true;
                case "LWPOLYLINE": ReadLwPolyline(); return true;
                default: return false;
            }
        }
    }
}
