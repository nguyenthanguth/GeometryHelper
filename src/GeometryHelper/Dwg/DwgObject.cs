using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// One group of a DXF record made from a DWG object: the group code and the value. The value is an int or a long for a
    /// whole number, a double, a string of plain ASCII, the bytes of a text in the drawing's code page, a ulong for a
    /// handle written in hex, or a <see cref="DwgName"/> for the name of the table record a handle leads to. A double or
    /// an int made by <see cref="Real"/> or <see cref="Whole"/> is kept in the group itself and not as an object of its own
    /// (24 bytes less for each, and one object less for the collector to walk): a LINE keeps six of its seven groups so, and
    /// <see cref="Value"/> gives them boxed as it always did.
    /// </summary>
    internal readonly struct DwgGroup
    {
        /// <summary>Marks a group that holds a double in its number.</summary>
        private static readonly object DoubleKind = new object();

        /// <summary>Marks a group that holds an int in its number.</summary>
        private static readonly object IntKind = new object();

        private readonly object _value;
        private readonly long _number;

        /// <summary>The group code.</summary>
        public readonly int Code;

        /// <summary>A group of <paramref name="code"/> and <paramref name="value"/>.</summary>
        public DwgGroup(int code, object value)
        {
            Code = code;
            _value = value;
            _number = 0;
        }

        private DwgGroup(int code, object kind, long number)
        {
            Code = code;
            _value = kind;
            _number = number;
        }

        /// <summary>A group of <paramref name="code"/> that holds the double <paramref name="value"/> as its number.</summary>
        public static DwgGroup Real(int code, double value) =>
            new DwgGroup(code, DoubleKind, BitConverter.DoubleToInt64Bits(value));

        /// <summary>A group of <paramref name="code"/> that holds the int <paramref name="value"/> as its number.</summary>
        public static DwgGroup Whole(int code, int value) => new DwgGroup(code, IntKind, value);

        /// <summary>Whether the value is a double.</summary>
        public bool IsReal => ReferenceEquals(_value, DoubleKind);

        /// <summary>Whether the value is an int.</summary>
        public bool IsWhole => ReferenceEquals(_value, IntKind);

        /// <summary>The double a group of <see cref="IsReal"/> holds.</summary>
        public double RealValue => BitConverter.Int64BitsToDouble(_number);

        /// <summary>The int a group of <see cref="IsWhole"/> holds.</summary>
        public int WholeValue => (int)_number;

        /// <summary>The value, of one of the kinds the type names.</summary>
        public object Value =>
            ReferenceEquals(_value, DoubleKind) ? (object)RealValue
            : ReferenceEquals(_value, IntKind) ? (object)WholeValue
            : _value;
    }

    /// <summary>
    /// The name of a table record given by its handle, looked up when the record is written: a layer, a linetype, a text
    /// style, a block and so on. Where the handle leads to no record with a name, <see cref="Fallback"/> is written.
    /// </summary>
    internal sealed class DwgName
    {
        /// <summary>The handle of the table record.</summary>
        public readonly ulong Handle;

        /// <summary>The name written where the handle leads to no named record.</summary>
        public readonly string Fallback;

        /// <summary>The name DXF leaves out, such as the text style <c>Standard</c>: the group is not written where the record has it. Null to write every name.</summary>
        public readonly string OmitWhen;

        /// <summary>Whether the linetypes ByLayer and ByBlock are written in capitals, as an MLINESTYLE writes them.</summary>
        public bool Capitals;

        /// <summary>The name of the record at <paramref name="handle"/>, or <paramref name="fallback"/>; not written where it is <paramref name="omitWhen"/>.</summary>
        public DwgName(ulong handle, string fallback = "", string omitWhen = null)
        {
            Handle = handle;
            Fallback = fallback;
            OmitWhen = omitWhen;
        }
    }

    /// <summary>
    /// The shape of a SHAPE, which a DWG keeps as its number in the file of shapes of a text style: the transcoder writes
    /// its name (group 2) where that file is found in the support folders and names the number, else the number (70), as
    /// AutoCAD writes a shape whose file it does not find.
    /// </summary>
    internal sealed class DwgShapeName
    {
        /// <summary>The handle of the text style of the file of shapes.</summary>
        public readonly ulong Style;

        /// <summary>The number of the shape in that file.</summary>
        public readonly int Number;

        /// <summary>The shape numbered <paramref name="number"/> in the file of shapes of the style at <paramref name="style"/>.</summary>
        public DwgShapeName(ulong style, int number)
        {
            Style = style;
            Number = number;
        }
    }

    /// <summary>
    /// The colour of an entity that is a colour of a book: the transcoder writes the index, the true colour and the name
    /// (62, 420, 430) of the DBCOLOR object it names, or the index alone where that object is not there.
    /// </summary>
    internal sealed class DwgBookColour
    {
        /// <summary>The handle of the DBCOLOR object.</summary>
        public readonly ulong Book;

        /// <summary>The colour index the entity holds beside it.</summary>
        public readonly int Index;

        /// <summary>A colour of the book at <paramref name="book"/>, <paramref name="index"/> where the book is not there.</summary>
        public DwgBookColour(ulong book, int index)
        {
            Book = book;
            Index = index;
        }
    }

    /// <summary>
    /// A linetype given by its place among the records of the linetype table, as an MLINESTYLE before R2018 gives the
    /// linetype of an element; the transcoder writes its name.
    /// </summary>
    internal sealed class DwgLinetypeIndex
    {
        /// <summary>The place of the linetype among the records of the table, from 0.</summary>
        public readonly int Index;

        /// <summary>The linetype at <paramref name="index"/>.</summary>
        public DwgLinetypeIndex(int index)
        {
            Index = index;
        }
    }

    /// <summary>
    /// The number of a viewport (group 69) or its status (group 68), which a DWG does not hold: the transcoder gives them
    /// from the place of the viewport among those of its layout.
    /// </summary>
    internal sealed class DwgViewportNumber
    {
        /// <summary>The handle of the viewport.</summary>
        public readonly ulong Viewport;

        /// <summary>Whether the group is the status (68) rather than the number (69).</summary>
        public readonly bool Status;

        /// <summary>
        /// The handle of the viewport entity header of a file of R2000, which AutoCAD keeps for each viewport it has numbered;
        /// 0 for none, and in every later version.
        /// </summary>
        public ulong Header;

        /// <summary>The number of <paramref name="viewport"/>, or its status where <paramref name="status"/> is set.</summary>
        public DwgViewportNumber(ulong viewport, bool status)
        {
            Viewport = viewport;
            Status = status;
        }
    }

    /// <summary>
    /// A colour as a DWG keeps it: the colour index (256 by layer, 0 by block), and from R2004 a true colour, the name of a
    /// colour of a book and a transparency.
    /// </summary>
    internal sealed class DwgColour
    {
        /// <summary>The colour index, group 62.</summary>
        public int Index;

        /// <summary>The true colour as 0xRRGGBB, group 420; -1 for none.</summary>
        public int Rgb = -1;

        /// <summary>The name of the colour in its book; null for none.</summary>
        public byte[] Name;

        /// <summary>The name of the book of the colour; null for none.</summary>
        public byte[] BookName;

        /// <summary>The transparency as group 440 carries it; -1 for none, which is by layer.</summary>
        public int Transparency = -1;

        /// <summary>
        /// Of a colour of a style (CMC): the value DXF writes where it gives the colour as one number, as a multileader does:
        /// the high byte 0xC0 by layer, 0xC1 by block, 0xC2 a true colour in the low three bytes, 0xC3 an index in the low
        /// byte. Before R2004, where the file holds the index alone, made from it.
        /// </summary>
        public int Raw;

        /// <summary>Whether the colour is one of a book, whose DBCOLOR object the handles name.</summary>
        public bool HasBook;

        /// <summary>The handle of the DBCOLOR object of a colour of a book.</summary>
        public ulong Book;
    }

    /// <summary>
    /// A DWG object decoded into the parts of its DXF record: what every object carries (handle, owner, reactors, extension
    /// dictionary, extended data), what every entity carries, the groups of its own data in the order DXF writes them, and
    /// the handles that tie it to the objects it owns or follows.
    /// </summary>
    internal sealed class DwgObject
    {
        /// <summary>The handle.</summary>
        public ulong Handle;

        /// <summary>The object type code of the file: a fixed type below 500, else 500 plus the index of a class.</summary>
        public int Type;

        /// <summary>The name of the DXF record, such as <c>LINE</c> or <c>LAYER</c>; for a control object the name of its table.</summary>
        public string Name;

        /// <summary>Whether the object is an entity.</summary>
        public bool IsEntity;

        /// <summary>Whether the object is the control object of a table.</summary>
        public bool IsControl;

        /// <summary>Whether the object is a record of a table.</summary>
        public bool IsTableRecord;

        /// <summary>Whether the data of the object were decoded; false for a type that is not read, of which only the common part is known.</summary>
        public bool Decoded;

        /// <summary>The handle of the owner; 0 for an entity that names its space by <see cref="EntityMode"/> alone.</summary>
        public ulong Owner;

        /// <summary>The handles of the persistent reactors.</summary>
        public List<ulong> Reactors
        {
            get => _reactors ?? (_reactors = new List<ulong>());
            set => _reactors = value;
        }

        /// <summary>Whether the object has a persistent reactor, which asks for no list to be made where it has none.</summary>
        public bool HasReactors => _reactors != null && _reactors.Count > 0;

        private List<ulong> _reactors;

        /// <summary>The handle of the extension dictionary; 0 for none.</summary>
        public ulong XDictionary;

        /// <summary>Of an entity: 0 owned by <see cref="Owner"/>, 1 in paper space, 2 in model space.</summary>
        public int EntityMode;

        /// <summary>Of an entity: the handle of the entity after it in its owner; 0 at the end.</summary>
        public ulong Next;

        /// <summary>Of an entity: the groups every entity carries after its layer (linetype, colour, scale, visibility, lineweight).</summary>
        public List<DwgGroup> Common
        {
            get => _common ?? (_common = new List<DwgGroup>());
            set => _common = value;
        }

        /// <summary>The groups every entity carries, null where it has none: a list is made only for an object that has some, 300 000 of them kept by a drawing of 300 000 entities.</summary>
        public List<DwgGroup> CommonIfAny => _common;

        private List<DwgGroup> _common;

        /// <summary>Of an entity: the handle of its layer.</summary>
        public ulong Layer;

        /// <summary>The groups of the object's own data, in the order DXF writes them.</summary>
        public List<DwgGroup> Body
        {
            get => _body ?? (_body = new List<DwgGroup>());
            set => _body = value;
        }

        /// <summary>The groups of the object's own data, null where it has none.</summary>
        public List<DwgGroup> BodyIfAny => _body;

        private List<DwgGroup> _body;

        /// <summary>The extended data as groups 1001 and up.</summary>
        public List<DwgGroup> XData
        {
            get => _xData ?? (_xData = new List<DwgGroup>());
            set => _xData = value;
        }

        /// <summary>The extended data, null where the object has none.</summary>
        public List<DwgGroup> XDataIfAny => _xData;

        private List<DwgGroup> _xData;

        // What only some objects carry is kept in an object of its own, made when the first of it is set: an entity of a
        // drawing has none of it, and the fields would take some 110 bytes of each of the 300 000 of a drawing of that many (an object is 120 bytes where it was 392, with its five lists made empty).
        private DwgObjectExtra _extra;

        private DwgObjectExtra Extra => _extra ?? (_extra = new DwgObjectExtra());

        /// <summary>
        /// Of a table style of cell styles of its own (101 on): the CELLSTYLEMAP AutoCAD writes for it in DXF alone, which
        /// the reader adds under its extension dictionary; null for none.
        /// </summary>
        public DwgObject CellStyleMap
        {
            get => _extra?.CellStyleMap;
            set
            {
                if (value != null || _extra != null)
                {
                    Extra.CellStyleMap = value;
                }
            }
        }

        /// <summary>The name of a table record, a block or a named object, as the bytes of the file.</summary>
        public byte[] RecordName
        {
            get => _extra?.RecordName;
            set
            {
                if (value != null || _extra != null)
                {
                    Extra.RecordName = value;
                }
            }
        }

        /// <summary>Of a control object: the handles of its records.</summary>
        public List<ulong> Entries
        {
            get => Extra.Entries ?? (Extra.Entries = new List<ulong>());
            set => Extra.Entries = value;
        }

        /// <summary>The first owned entity: of a block its first entity, of a polyline its first vertex, of an insert its first attribute.</summary>
        public ulong First
        {
            get => _extra == null ? 0 : _extra.First;
            set
            {
                if (value != 0 || _extra != null)
                {
                    Extra.First = value;
                }
            }
        }

        /// <summary>The last owned entity, as <see cref="First"/>.</summary>
        public ulong Last
        {
            get => _extra == null ? 0 : _extra.Last;
            set
            {
                if (value != 0 || _extra != null)
                {
                    Extra.Last = value;
                }
            }
        }

        /// <summary>
        /// From R2004, where no entity names the next: the owned entities in their order (of a block its entities, of a
        /// polyline its vertices, of an insert its attributes); null before R2004, where <see cref="First"/> and
        /// <see cref="Last"/> give them.
        /// </summary>
        public List<ulong> Owned
        {
            get => _extra?.Owned;
            set
            {
                if (value != null || _extra != null)
                {
                    Extra.Owned = value;
                }
            }
        }

        /// <summary>Of a polyline or an insert with attributes: the handle of the SEQEND that closes it.</summary>
        public ulong SeqEnd
        {
            get => _extra == null ? 0 : _extra.SeqEnd;
            set
            {
                if (value != 0 || _extra != null)
                {
                    Extra.SeqEnd = value;
                }
            }
        }

        /// <summary>Of a block header: the handle of its BLOCK entity.</summary>
        public ulong BlockEntity
        {
            get => _extra == null ? 0 : _extra.BlockEntity;
            set
            {
                if (value != 0 || _extra != null)
                {
                    Extra.BlockEntity = value;
                }
            }
        }

        /// <summary>Of a block header: the handle of its ENDBLK entity.</summary>
        public ulong EndBlock
        {
            get => _extra == null ? 0 : _extra.EndBlock;
            set
            {
                if (value != 0 || _extra != null)
                {
                    Extra.EndBlock = value;
                }
            }
        }

        /// <summary>Of a block header: the block flags of group 70 of its BLOCK. Of a control object: the count of records it gives for group 70.</summary>
        public int BlockFlags
        {
            get => _extra == null ? 0 : _extra.BlockFlags;
            set
            {
                if (value != 0 || _extra != null)
                {
                    Extra.BlockFlags = value;
                }
            }
        }

        /// <summary>Of a block header: the groups of the BLOCK entity that the header keeps (base point, path of the reference).</summary>
        public List<DwgGroup> BlockBody
        {
            get => _extra?.BlockBody;
            set
            {
                if (value != null || _extra != null)
                {
                    Extra.BlockBody = value;
                }
            }
        }

        /// <summary>Of a DBCOLOR from R2004: its colour; null for another type.</summary>
        public DwgColour BookColour
        {
            get => _extra?.BookColour;
            set
            {
                if (value != null || _extra != null)
                {
                    Extra.BookColour = value;
                }
            }
        }

        /// <summary>Of an entity: the picture of it kept for programs that cannot draw it (proxy graphics); null for none.</summary>
        public byte[] ProxyGraphics
        {
            get => _extra?.ProxyGraphics;
            set
            {
                if (value != null || _extra != null)
                {
                    Extra.ProxyGraphics = value;
                }
            }
        }

        /// <summary>Of an MTEXT: the height and the width of the extents of its text; null for another type.</summary>
        public double[] TextExtents
        {
            get => _extra?.TextExtents;
            set
            {
                if (value != null || _extra != null)
                {
                    Extra.TextExtents = value;
                }
            }
        }

        /// <summary>Of a LEADER: the handle of its annotation; 0 for none.</summary>
        public ulong Annotation
        {
            get => _extra == null ? 0 : _extra.Annotation;
            set
            {
                if (value != 0 || _extra != null)
                {
                    Extra.Annotation = value;
                }
            }
        }

        /// <summary>Of a layout from R2004: its viewports in their order; null before R2004.</summary>
        public List<ulong> Viewports
        {
            get => _extra?.Viewports;
            set
            {
                if (value != null || _extra != null)
                {
                    Extra.Viewports = value;
                }
            }
        }

        /// <summary>
        /// Makes room in <see cref="Body"/> for <paramref name="groups"/> groups in one array, where an entity knows how many
        /// it will add: a list grown by adding holds twice what it was given as it passes 4, 8, 16, and the room it does not use
        /// is kept by each of the objects of a drawing.
        /// </summary>
        public void ReserveBody(int groups)
        {
            if (Body.Capacity < groups)
            {
                Body.Capacity = groups;
            }
        }

        /// <summary>Adds a group to <see cref="Body"/>.</summary>
        public void Add(int code, object value) => Body.Add(new DwgGroup(code, value));

        /// <summary>Adds a group that holds a double to <see cref="Body"/>, kept in the group and not as an object of its own.</summary>
        public void Add(int code, double value) => Body.Add(DwgGroup.Real(code, value));

        /// <summary>Adds a group that holds an int to <see cref="Body"/>, kept in the group and not as an object of its own.</summary>
        public void Add(int code, int value) => Body.Add(DwgGroup.Whole(code, value));

        // The other whole numbers are kept as the objects they were, and not turned into doubles by the overload above.

        /// <summary>Adds a group that holds a long to <see cref="Body"/>.</summary>
        public void Add(int code, long value) => Body.Add(new DwgGroup(code, (object)value));

        /// <summary>Adds a group that holds an unsigned long, a handle, to <see cref="Body"/>.</summary>
        public void Add(int code, ulong value) => Body.Add(new DwgGroup(code, (object)value));

        /// <summary>Adds a group that holds an unsigned int to <see cref="Body"/>.</summary>
        public void Add(int code, uint value) => Body.Add(new DwgGroup(code, (object)value));

        /// <summary>Adds a point to <see cref="Body"/>: its coordinates under <paramref name="code"/>, the code plus 10 and, of three, plus 20.</summary>
        public void AddPoint(int code, double[] point)
        {
            for (int i = 0; i < point.Length; i++)
            {
                Body.Add(DwgGroup.Real(code + (10 * i), point[i]));
            }
        }
    }

    /// <summary>What only some of the objects carry, kept by a <see cref="DwgObject"/> that has some of it.</summary>
    internal sealed class DwgObjectExtra
    {
        public DwgObject CellStyleMap;
        public byte[] RecordName;
        public List<ulong> Entries;
        public ulong First;
        public ulong Last;
        public List<ulong> Owned;
        public ulong SeqEnd;
        public ulong BlockEntity;
        public ulong EndBlock;
        public int BlockFlags;
        public List<DwgGroup> BlockBody;
        public DwgColour BookColour;
        public byte[] ProxyGraphics;
        public double[] TextExtents;
        public ulong Annotation;
        public List<ulong> Viewports;
    }
}
