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

        /// <summary>From R2004 the number of objects of the class in the drawing, group 91; -1 before.</summary>
        public int Count = -1;

        /// <summary>
        /// The classes of a file of <paramref name="version"/>, read from the data of their section. From R2004 the data
        /// start with the highest class number and three values more, and each class carries the count of its objects and
        /// four values more; from R2007 they start with their size in bits, and their texts are UTF-16 in a stream at their
        /// end, found as an object finds its own.
        /// </summary>
        public static List<DwgClass> ReadAll(byte[] data, string version)
        {
            int number = DwgContainer.NumberOf(version);
            var classes = new List<DwgClass>();
            var reader = new DwgBitReader(data);
            DwgBitReader strings = null;
            long end = reader.End;

            if (number >= 1021)
            {
                long bits = (uint)reader.RawLong();

                if (bits <= 32 || bits > reader.End)
                {
                    throw new FormatException("The DWG classes name more data than they hold.");
                }

                end = DwgBitReader.StringStream(data, 32, bits - 1, out strings);
                reader = new DwgBitReader(data, 32, end);
                reader.BitLong();
                reader.Bit();
            }
            else if (number >= 1018)
            {
                reader.BitShort();
                reader.RawChar();
                reader.RawChar();
                reader.Bit();
            }

            // Before R2007 the data is padded to whole bytes; a class takes far more than the three bytes asked for here.
            while (strings != null ? reader.Remaining > 0 : reader.Remaining >= 24)
            {
                var one = new DwgClass();
                one.Number = reader.BitShort();
                one.ProxyFlags = reader.BitShort();
                one.Application =
                    strings != null ? strings.TextUnicode()
                    : number >= 1021 ? new byte[0]
                    : reader.Text();
                one.CppName =
                    strings != null ? strings.TextUnicode()
                    : number >= 1021 ? new byte[0]
                    : reader.Text();
                one.DxfName =
                    strings != null ? strings.TextUnicode()
                    : number >= 1021 ? new byte[0]
                    : reader.Text();
                one.WasZombie = reader.Bit();
                one.IsEntity = reader.BitShort() == 0x1F2;

                if (number >= 1018)
                {
                    one.Count = reader.BitLong();
                    reader.BitLong();
                    reader.BitLong();
                    reader.BitLong();
                    reader.BitLong();
                }

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

        private static readonly Dictionary<int, string> FixedTypes = new Dictionary<int, string>
        {
            { 0x01, "TEXT" },
            { 0x02, "ATTRIB" },
            { 0x03, "ATTDEF" },
            { 0x04, "BLOCK" },
            { 0x05, "ENDBLK" },
            { 0x06, "SEQEND" },
            { 0x07, "INSERT" },
            { 0x08, "INSERT" },
            { 0x0A, "VERTEX" },
            { 0x0B, "VERTEX" },
            { 0x0C, "VERTEX" },
            { 0x0D, "VERTEX" },
            { 0x0E, "VERTEX" },
            { 0x0F, "POLYLINE" },
            { 0x10, "POLYLINE" },
            { 0x11, "ARC" },
            { 0x12, "CIRCLE" },
            { 0x13, "LINE" },
            { 0x14, "DIMENSION" },
            { 0x15, "DIMENSION" },
            { 0x16, "DIMENSION" },
            { 0x17, "DIMENSION" },
            { 0x18, "DIMENSION" },
            { 0x19, "DIMENSION" },
            { 0x1A, "DIMENSION" },
            { 0x1B, "POINT" },
            { 0x1C, "3DFACE" },
            { 0x1D, "POLYLINE" },
            { 0x1E, "POLYLINE" },
            { 0x1F, "SOLID" },
            { 0x20, "TRACE" },
            { 0x21, "SHAPE" },
            { 0x22, "VIEWPORT" },
            { 0x23, "ELLIPSE" },
            { 0x24, "SPLINE" },
            { 0x25, "REGION" },
            { 0x26, "3DSOLID" },
            { 0x27, "BODY" },
            { 0x28, "RAY" },
            { 0x29, "XLINE" },
            { 0x2A, "DICTIONARY" },
            { 0x2B, "OLEFRAME" },
            { 0x2C, "MTEXT" },
            { 0x2D, "LEADER" },
            { 0x2E, "TOLERANCE" },
            { 0x2F, "MLINE" },
            { 0x30, "BLOCK_RECORD" },
            { 0x31, "BLOCK_RECORD" },
            { 0x32, "LAYER" },
            { 0x33, "LAYER" },
            { 0x34, "STYLE" },
            { 0x35, "STYLE" },
            { 0x38, "LTYPE" },
            { 0x39, "LTYPE" },
            { 0x3C, "VIEW" },
            { 0x3D, "VIEW" },
            { 0x3E, "UCS" },
            { 0x3F, "UCS" },
            { 0x40, "VPORT" },
            { 0x41, "VPORT" },
            { 0x42, "APPID" },
            { 0x43, "APPID" },
            { 0x44, "DIMSTYLE" },
            { 0x45, "DIMSTYLE" },
            { 0x46, "VP_ENT_HDR" },
            { 0x47, "VP_ENT_HDR" },
            { 0x48, "GROUP" },
            { 0x49, "MLINESTYLE" },
            { 0x4A, "OLE2FRAME" },
            { 0x4B, "DUMMY" },
            { 0x4C, "LONG_TRANSACTION" },
            { 0x4D, "LWPOLYLINE" },
            { 0x4E, "HATCH" },
            { 0x4F, "XRECORD" },
            { 0x50, "ACDBPLACEHOLDER" },
            { 0x51, "VBA_PROJECT" },
            { 0x52, "LAYOUT" },
        };

        private readonly byte[] _file;
        private readonly int _version;
        private readonly Dictionary<int, DwgClass> _classes = new Dictionary<int, DwgClass>();

        private readonly List<DwgClass> _classList;
        private readonly string _versionName;

        /// <summary>
        /// Where the object of a handle starts, from the object map, for the objects a decoder reads beside its own (the
        /// cell styles of a table's style); none where it is not set.
        /// </summary>
        public Func<ulong, long?> OffsetOf { get; set; }
        private DwgBitReader _data;
        private DwgBitReader _handles;
        private DwgBitReader _strings;
        private DwgObject _object;

        /// <summary>
        /// A reader of the objects in <paramref name="file"/>, a DWG of <paramref name="version"/> (such as <c>AC1018</c>),
        /// whose types of 500 and up are of <paramref name="classes"/>.
        /// </summary>
        public DwgObjectReader(byte[] file, List<DwgClass> classes, string version)
        {
            _file = file;
            _classList = classes;
            _versionName = version;
            _version = DwgContainer.NumberOf(version);

            foreach (DwgClass one in classes)
            {
                _classes[one.Number] = one;
            }
        }

        /// <summary>Whether the file is of R2004 (AC1018) or later.</summary>
        private bool R2004Plus => _version >= 1018;

        /// <summary>Whether the file is of R2007 (AC1021) or later: texts in a stream of their own, as UTF-16.</summary>
        private bool R2007Plus => _version >= 1021;

        /// <summary>Whether the file is of R2010 (AC1024) or later.</summary>
        private bool R2010Plus => _version >= 1024;

        /// <summary>Whether the file is of R2013 (AC1027) or later.</summary>
        private bool R2013Plus => _version >= 1027;

        /// <summary>Whether the file is of R2018 (AC1032) or later.</summary>
        private bool R2018Plus => _version >= 1032;

        /// <summary>A text of the object: before R2007 in the data as bytes of the code page, from R2007 in the string stream as UTF-16, returned as UTF-8.</summary>
        private byte[] T()
        {
            if (!R2007Plus)
            {
                return _data.Text();
            }

            return _strings == null ? new byte[0] : _strings.TextUnicode();
        }

        /// <summary>The lineweight in hundredths of a millimetre for the index a DWG stores: -1 by layer, -2 by block, -3 the default.</summary>
        public static int LineWeight(int index)
        {
            if (index >= 0 && index < LineWeights.Length)
            {
                return LineWeights[index];
            }

            return index == 30 ? -2
                : index == 31 ? -3
                : -1;
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
            long handleBits = R2010Plus ? head.ModularChar(false) : 0;
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

            long end = (start + size) * 8;
            var data = new DwgBitReader(_file, start * 8, end);
            int type;
            long handlesAt;

            if (R2010Plus)
            {
                // From R2010 the head gives the size of the handles, which close the object.
                type = data.ObjectType();
                handlesAt = end - handleBits;
            }
            else
            {
                type = data.BitShort();
                handlesAt = (start * 8) + (uint)data.RawLong();
            }

            if (handleBits < 0 || handlesAt < data.Position || handlesAt > end)
            {
                throw new FormatException("The object places its handles outside itself.");
            }

            long dataEnd = handlesAt;
            _strings = null;

            if (R2007Plus)
            {
                dataEnd = DwgBitReader.StringStream(_file, data.Position, handlesAt - 1, out _strings);
            }

            _data = new DwgBitReader(_file, data.Position, dataEnd);
            _handles = new DwgBitReader(_file, handlesAt, end);
            _object = new DwgObject { Type = type };
            _object.Handle = _data.Handle();

            if (_object.Handle != handle)
            {
                throw new FormatException(
                    "The object carries the handle "
                        + _object.Handle.ToString("X", CultureInfo.InvariantCulture)
                        + ", not the one the object map gives it."
                );
            }

            DwgClass ofClass = null;

            if (FixedTypes.TryGetValue(type, out string name))
            {
                _object.Name = name;
                _object.IsEntity =
                    type < 0x2A || (type >= 0x2B && type <= 0x2F) || type == 0x4A || type == 0x4D || type == 0x4E;
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
                bool noDictionary = ReadDictionaryFlags();
                _object.Owner = H();
                ReadReactors(reactors, noDictionary);
            }

            _object.Decoded = ReadBody(type);
            return _object;
        }

        /// <summary>
        /// The flags after the count of reactors: from R2004 a bit set where the object has no extension dictionary, from
        /// R2013 a bit for data kept in the data store. Returns whether the extension dictionary is left out.
        /// </summary>
        private bool ReadDictionaryFlags()
        {
            bool noDictionary = R2004Plus && _data.Bit();

            if (R2013Plus)
            {
                _data.Bit();
            }

            return noDictionary;
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

        private void ReadReactors(int count, bool noDictionary)
        {
            for (int i = 0; i < count; i++)
            {
                _object.Reactors.Add(H());
            }

            if (!noDictionary)
            {
                _object.XDictionary = H();
            }
        }

        private void ReadEntityCommon()
        {
            if (_data.Bit())
            {
                // The proxy graphics: a picture of the entity for the programs that cannot draw it.
                long graphics = R2010Plus ? _data.BitLongLong() : _data.RawLong();

                if (graphics < 0 || graphics * 8 > _data.Remaining)
                {
                    throw new FormatException("The proxy graphics of the entity run past its end.");
                }

                // DXF carries it too (groups 92 or 160, then 310), where AutoCAD saved it: the newer entities in an older file.
                _object.ProxyGraphics = _data.Bytes((int)graphics);
            }

            _object.EntityMode = _data.Bits2();
            int reactors = Count(_data.BitLong());
            bool noDictionary = ReadDictionaryFlags();
            bool noLinks = R2004Plus || _data.Bit();
            DwgColour colour = R2004Plus ? ReadEntityColour() : new DwgColour { Index = _data.BitShort() };
            double linetypeScale = _data.BitDouble();
            int linetype = _data.Bits2();
            int material = R2007Plus ? _data.Bits2() : 0;

            if (R2007Plus)
            {
                _data.RawChar();
            }

            int plotStyle = _data.Bits2();
            int visualStyles = 0;

            if (R2010Plus)
            {
                visualStyles = (_data.Bit() ? 1 : 0) + (_data.Bit() ? 1 : 0) + (_data.Bit() ? 1 : 0);
            }

            int invisible = _data.BitShort();
            int lineWeight = LineWeight(_data.RawChar());

            if (_object.EntityMode == 0)
            {
                _object.Owner = H();
            }

            ReadReactors(reactors, noDictionary);

            if (R2004Plus)
            {
                // From R2004 no entity names the next; the owner lists them.
                _object.Next = 0;
            }
            else if (noLinks)
            {
                _object.Next = _object.Handle + 1;
            }
            else
            {
                H();
                _object.Next = H();
            }

            if (colour.HasBook)
            {
                colour.Book = H();
            }

            _object.Layer = H();

            switch (linetype)
            {
                case 1:
                    _object.Common.Add(new DwgGroup(6, "ByBlock"));
                    break;
                case 2:
                    _object.Common.Add(new DwgGroup(6, "Continuous"));
                    break;
                case 3:
                    _object.Common.Add(new DwgGroup(6, new DwgName(H(), "ByLayer")));
                    break;
            }

            ulong materialHandle = material == 3 ? H() : 0;

            if (plotStyle == 3)
            {
                H();
            }

            for (int i = 0; i < visualStyles; i++)
            {
                H();
            }

            if (materialHandle != 0)
            {
                _object.Common.Add(new DwgGroup(347, materialHandle));
            }

            if (colour.HasBook)
            {
                // The index, the true colour and the name are those of the DBCOLOR the handle leads to; the transcoder writes them.
                _object.Common.Add(new DwgGroup(62, new DwgBookColour(colour.Book, colour.Index)));

                if (colour.Transparency != -1)
                {
                    _object.Common.Add(new DwgGroup(440, colour.Transparency));
                }
            }
            else
            {
                AddColour(_object.Common, colour, true);
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
        /// The colour of an entity from R2004 (ENC): a bit short whose low bits are the colour index and whose high bits say
        /// what follows: 0x8000 a true colour as a bit long, 0x4000 a colour of a book named in the handles, 0x2000 a
        /// transparency as a bit long.
        /// </summary>
        private DwgColour ReadEntityColour()
        {
            int value = _data.BitShort() & 0xFFFF;
            var colour = new DwgColour { Index = value & 0x1FF };
            colour.HasBook = (value & 0x4000) != 0;

            // A colour of a book sets 0x8000 too, but its true colour is in its DBCOLOR, not here.
            if ((value & 0x8000) != 0 && !colour.HasBook)
            {
                colour.Rgb = _data.BitLong() & 0xFFFFFF;
            }

            if ((value & 0x2000) != 0)
            {
                colour.Transparency = _data.BitLong();
            }

            return colour;
        }

        /// <summary>
        /// A colour of a table record or a style (CMC): before R2004 a bit short of the colour index; from R2004 a bit short
        /// of the index, a bit long whose high byte says how the colour is given (0xC0 by layer, 0xC1 by block, 0xC2 a true
        /// colour in the low three bytes, 0xC3 an index in the low byte), and a byte saying a colour name and a book name
        /// follow as texts.
        /// </summary>
        private DwgColour ReadColour() => ReadColour(R2004Plus);

        /// <summary>A CMC colour, of the form of R2004 where <paramref name="later"/> is set, as a type newer than R2004 keeps it in an older file.</summary>
        private DwgColour ReadColour(bool later)
        {
            var colour = new DwgColour { Index = _data.BitShort() };

            if (!later)
            {
                colour.Raw =
                    colour.Index == 256 ? unchecked((int)0xC0000000)
                    : colour.Index == 0 ? unchecked((int)0xC1000000)
                    : unchecked((int)0xC3000000) | (colour.Index & 0xFF);
                return colour;
            }

            int rgb = _data.BitLong();
            int names = _data.RawChar();
            colour.Raw = rgb;

            switch ((rgb >> 24) & 0xFF)
            {
                case 0xC0:
                    colour.Index = 256;
                    break;
                case 0xC1:
                    colour.Index = 0;
                    break;
                case 0xC2:
                    // DXF writes a true colour with the nearest index of the palette; the index the file holds beside it is not that.
                    colour.Rgb = rgb & 0xFFFFFF;
                    colour.Index = GeometryHelper.Dxf.DxfColor.OfRgb(colour.Rgb, null).ColorIndex;
                    break;
                case 0xC3:
                    colour.Index = rgb & 0xFF;
                    break;
            }

            if ((names & 1) != 0)
            {
                colour.Name = T();
            }

            if ((names & 2) != 0)
            {
                colour.BookName = T();
            }

            return colour;
        }

        /// <summary>
        /// Adds the groups of a colour: <paramref name="indexCode"/> the index (of an entity only where it is not by layer),
        /// <paramref name="rgbCode"/> a true colour, 430 the name of a colour of a book, 440 a transparency.
        /// </summary>
        private static void AddColour(
            List<DwgGroup> groups,
            DwgColour colour,
            bool entity,
            int indexCode = 62,
            int rgbCode = 420
        )
        {
            if (!entity || colour.Index != 256)
            {
                groups.Add(new DwgGroup(indexCode, colour.Index));
            }

            if (colour.Rgb >= 0 && rgbCode >= 0)
            {
                groups.Add(new DwgGroup(rgbCode, colour.Rgb));
            }

            if (colour.Name != null && colour.Name.Length > 0)
            {
                groups.Add(
                    new DwgGroup(
                        430,
                        colour.BookName != null && colour.BookName.Length > 0
                            ? Join(colour.BookName, colour.Name)
                            : colour.Name
                    )
                );
            }

            if (colour.Transparency != -1)
            {
                groups.Add(new DwgGroup(440, colour.Transparency));
            }
        }

        /// <summary>The name DXF gives a colour of a book: the book, a dollar sign, the colour.</summary>
        private static byte[] Join(byte[] book, byte[] name)
        {
            var joined = new byte[book.Length + 1 + name.Length];
            Array.Copy(book, joined, book.Length);
            joined[book.Length] = (byte)'$';
            Array.Copy(name, 0, joined, book.Length + 1, name.Length);
            return joined;
        }

        /// <summary>
        /// The extended data: for each application a size, the handle of its APPID and that many bytes of values, each a
        /// byte saying its kind (the DXF group less 1000) and the value. A text is a byte of length, a short of code page
        /// and the bytes before R2007, a short of length and UTF-16 characters from R2007.
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
                            if (R2007Plus)
                            {
                                _object.XData.Add(new DwgGroup(1000, values.TextUnicodeRaw()));
                                break;
                            }

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
                            throw new FormatException(
                                "The extended data of the object have a value of the unknown kind "
                                    + kind.ToString(CultureInfo.InvariantCulture)
                                    + "."
                            );
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

        private static bool IsDefaultExtrusion(double[] extrusion) =>
            extrusion[0] == 0.0 && extrusion[1] == 0.0 && extrusion[2] == 1.0;

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
                case 0x01:
                    return ReadText(0);
                case 0x02:
                    return ReadText(1);
                case 0x03:
                    return ReadText(2);
                case 0x04:
                    _object.RecordName = T();
                    return true;
                case 0x05:
                    _object.Add(100, "AcDbBlockEnd");
                    return true;
                case 0x06:
                    return true;
                case 0x07:
                    ReadInsert(false);
                    return true;
                case 0x08:
                    ReadInsert(true);
                    return true;
                case 0x0A:
                    ReadVertex2D();
                    return true;
                case 0x0B:
                    ReadVertex3D("AcDb3dPolylineVertex");
                    return true;
                case 0x0C:
                    ReadVertex3D("AcDbPolygonMeshVertex");
                    return true;
                case 0x0D:
                    ReadVertex3D("AcDbPolyFaceMeshVertex");
                    return true;
                case 0x0E:
                    ReadFaceRecord();
                    return true;
                case 0x0F:
                    ReadPolyline2D();
                    return true;
                case 0x10:
                    ReadPolyline3D();
                    return true;
                case 0x11:
                    ReadCircle(true);
                    return true;
                case 0x12:
                    ReadCircle(false);
                    return true;
                case 0x13:
                    ReadLine();
                    return true;
                case 0x14:
                case 0x15:
                case 0x16:
                case 0x17:
                case 0x18:
                case 0x19:
                case 0x1A:
                    ReadDimension(type);
                    return true;
                case 0x1B:
                    ReadPoint();
                    return true;
                case 0x1C:
                    ReadFace();
                    return true;
                case 0x1D:
                    ReadPolyfaceMesh();
                    return true;
                case 0x1E:
                    ReadPolygonMesh();
                    return true;
                case 0x1F:
                case 0x20:
                    ReadSolid();
                    return true;
                case 0x21:
                    ReadShape();
                    return true;
                case 0x22:
                    ReadViewport();
                    return true;
                case 0x23:
                    ReadEllipse();
                    return true;
                case 0x24:
                    ReadSpline();
                    return true;
                case 0x25:
                case 0x27:
                    return ReadModelerGeometry(false);
                case 0x26:
                    return ReadModelerGeometry(true);
                case 0x28:
                    ReadRay("AcDbRay");
                    return true;
                case 0x29:
                    ReadRay("AcDbXline");
                    return true;
                case 0x2A:
                    ReadDictionary(false);
                    return true;
                case 0x2C:
                    ReadMText();
                    return true;
                case 0x2D:
                    ReadLeader();
                    return true;
                case 0x2E:
                    ReadTolerance();
                    return true;
                case 0x2F:
                    ReadMLine();
                    return true;
                case 0x30:
                case 0x32:
                case 0x34:
                case 0x38:
                case 0x3C:
                case 0x3E:
                case 0x40:
                case 0x42:
                case 0x44:
                case 0x46:
                    ReadControl(type);
                    return true;
                case 0x31:
                    ReadBlockHeader();
                    return true;
                case 0x33:
                    ReadLayer();
                    return true;
                case 0x35:
                    ReadStyle();
                    return true;
                case 0x39:
                    ReadLinetype();
                    return true;
                case 0x3D:
                    ReadView();
                    return true;
                case 0x3F:
                    ReadUcs();
                    return true;
                case 0x41:
                    ReadVport();
                    return true;
                case 0x43:
                    ReadAppId();
                    return true;
                case 0x45:
                    ReadDimStyle();
                    return true;
                case 0x48:
                    ReadGroup();
                    return true;
                case 0x49:
                    ReadMLineStyle();
                    return true;
                case 0x4D:
                    ReadLwPolyline();
                    return true;
                case 0x4E:
                    ReadHatch();
                    return true;
                case 0x4F:
                    ReadXRecord();
                    return true;
                case 0x50:
                    return true;
                case 0x52:
                    ReadLayout();
                    return true;
            }

            switch (_object.Name)
            {
                case "ACDBDICTIONARYWDFLT":
                    ReadDictionary(true);
                    return true;
                case "DICTIONARYVAR":
                    ReadDictionaryVariable();
                    return true;
                case "SCALE":
                    ReadScale();
                    return true;
                case "ACDBPLACEHOLDER":
                    return true;
                case "LAYOUT":
                    ReadLayout();
                    return true;
                case "PLOTSETTINGS":
                    ReadPlotSettings();
                    return true;
                case "XRECORD":
                    ReadXRecord();
                    return true;
                case "LWPOLYLINE":
                    ReadLwPolyline();
                    return true;
                case "HATCH":
                    ReadHatch();
                    return true;
                case "MULTILEADER":
                    ReadMultiLeader();
                    return true;
                case "MLEADERSTYLE":
                    ReadMultiLeaderStyle();
                    return true;
                case "IMAGE":
                    ReadRasterImage("AcDbRasterImage");
                    return true;
                case "WIPEOUT":
                    ReadRasterImage("AcDbWipeout");
                    return true;
                case "IMAGEDEF":
                    ReadImageDefinition();
                    return true;
                case "IMAGEDEF_REACTOR":
                    ReadImageDefinitionReactor();
                    return true;
                case "DBCOLOR":
                    ReadBookColour();
                    return true;
                case "RASTERVARIABLES":
                    ReadRasterVariables();
                    return true;
                case "SPATIAL_FILTER":
                    ReadSpatialFilter();
                    return true;
                case "ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION":
                    ReadDynamicBlockPurgePreventer();
                    return true;
                case "ACAD_EVALUATION_GRAPH":
                    ReadEvaluationGraph();
                    return true;
                case "BLOCKGRIPLOCATIONCOMPONENT":
                    ReadBlockGripLocationComponent();
                    return true;
                case "BLOCKLINEARGRIP":
                    ReadBlockLinearGrip();
                    return true;
                case "BLOCKLINEARPARAMETER":
                    ReadBlockLinearParameter();
                    return true;
                case "BLOCKSTRETCHACTION":
                    ReadBlockStretchAction();
                    return true;
                case "BLOCKVISIBILITYPARAMETER":
                    ReadBlockVisibilityParameter();
                    return true;
                case "BLOCKVISIBILITYGRIP":
                    ReadPlainGrip("AcDbBlockVisibilityGrip");
                    return true;
                case "BLOCKFLIPPARAMETER":
                    ReadBlockFlipParameter();
                    return true;
                case "BLOCKFLIPGRIP":
                    ReadBlockFlipGrip();
                    return true;
                case "BLOCKFLIPACTION":
                    ReadBlockFlipAction();
                    return true;
                case "BLOCKROTATIONPARAMETER":
                    ReadBlockRotationParameter();
                    return true;
                case "BLOCKROTATIONGRIP":
                    ReadPlainGrip("AcDbBlockRotationGrip");
                    return true;
                case "BLOCKROTATEACTION":
                    ReadBlockRotateAction();
                    return true;
                case "BLOCKXYPARAMETER":
                    ReadBlockXYParameter();
                    return true;
                case "BLOCKXYGRIP":
                    ReadPlainGrip("AcDbBlockXYGrip");
                    return true;
                case "BLOCKPOLARPARAMETER":
                    ReadBlockPolarParameter();
                    return true;
                case "BLOCKPOLARGRIP":
                    ReadPlainGrip("AcDbBlockPolarGrip");
                    return true;
                case "BLOCKPOLARSTRETCHACTION":
                    ReadBlockPolarStretchAction();
                    return true;
                case "BLOCKMOVEACTION":
                    ReadBlockMoveAction();
                    return true;
                case "BLOCKSCALEACTION":
                    ReadBlockScaleAction();
                    return true;
                case "BLOCKARRAYACTION":
                    ReadBlockArrayAction();
                    return true;
                case "BLOCKLOOKUPPARAMETER":
                    ReadBlockLookupParameter();
                    return true;
                case "BLOCKLOOKUPGRIP":
                    ReadPlainGrip("AcDbBlockLookUpGrip");
                    return true;
                case "BLOCKLOOKUPACTION":
                    ReadBlockLookupAction();
                    return true;
                case "BLOCKALIGNMENTPARAMETER":
                    ReadBlockAlignmentParameter();
                    return true;
                case "BLOCKALIGNMENTGRIP":
                    ReadBlockAlignmentGrip();
                    return true;
                case "BLOCKPOINTPARAMETER":
                    ReadBlockPointParameter();
                    return true;
                case "BLOCKBASEPOINTPARAMETER":
                    ReadBlockBasePointParameter();
                    return true;
                case "ACDB_BLOCKREPRESENTATION_DATA":
                    ReadBlockRepresentationData();
                    return true;
                case "WIPEOUTVARIABLES":
                    ReadWipeoutVariables();
                    return true;
                case "ACAD_TABLE":
                    return ReadTable();
                case "TABLESTYLE":
                    return ReadTableStyle();
                case "TABLECONTENT":
                    return ReadTableContentObject();
                case "CELLSTYLEMAP":
                    ReadCellStyleMapObject();
                    return true;
                case "TABLEGEOMETRY":
                    ReadTableGeometry();
                    return true;
                case "DATATABLE":
                case "ACDBDATATABLE":
                    return ReadDataTable();
                case "MATERIAL":
                    ReadMaterial();
                    return true;
                case "VISUALSTYLE":
                    ReadVisualStyle();
                    return true;
                case "ACDBDETAILVIEWSTYLE":
                    ReadDetailViewStyle();
                    return true;
                case "ACDBSECTIONVIEWSTYLE":
                    ReadSectionViewStyle();
                    return true;
                case "SORTENTSTABLE":
                    ReadSortEntitiesTable();
                    return true;
                case "FIELDLIST":
                    ReadFieldList();
                    return true;
                case "FIELD":
                    ReadField();
                    return true;
                case "DATALINK":
                    ReadDataLink();
                    return true;
                case "ACDBASSOCPERSSUBENTMANAGER":
                    ReadAssocPersSubentManager();
                    return true;
                default:
                    return false;
            }
        }
    }
}
