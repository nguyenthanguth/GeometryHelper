using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// One group of a DXF record made from a DWG object: the group code and the value. The value is an int or a long for a
    /// whole number, a double, a string of plain ASCII, the bytes of a text in the drawing's code page, a ulong for a
    /// handle written in hex, or a <see cref="DwgName"/> for the name of the table record a handle leads to.
    /// </summary>
    internal struct DwgGroup
    {
        /// <summary>The group code.</summary>
        public readonly int Code;

        /// <summary>The value, of one of the kinds the type names.</summary>
        public readonly object Value;

        /// <summary>A group of <paramref name="code"/> and <paramref name="value"/>.</summary>
        public DwgGroup(int code, object value)
        {
            Code = code;
            Value = value;
        }
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
        public List<ulong> Reactors = new List<ulong>();

        /// <summary>The handle of the extension dictionary; 0 for none.</summary>
        public ulong XDictionary;

        /// <summary>Of an entity: 0 owned by <see cref="Owner"/>, 1 in paper space, 2 in model space.</summary>
        public int EntityMode;

        /// <summary>Of an entity: the handle of the entity after it in its owner; 0 at the end.</summary>
        public ulong Next;

        /// <summary>Of an entity: the groups every entity carries after its layer (linetype, colour, scale, visibility, lineweight).</summary>
        public List<DwgGroup> Common = new List<DwgGroup>();

        /// <summary>Of an entity: the handle of its layer.</summary>
        public ulong Layer;

        /// <summary>The groups of the object's own data, in the order DXF writes them.</summary>
        public List<DwgGroup> Body = new List<DwgGroup>();

        /// <summary>The extended data as groups 1001 and up.</summary>
        public List<DwgGroup> XData = new List<DwgGroup>();

        /// <summary>
        /// Of a table style of cell styles of its own (101 on): the CELLSTYLEMAP AutoCAD writes for it in DXF alone, which
        /// the reader adds under its extension dictionary; null for none.
        /// </summary>
        public DwgObject CellStyleMap;

        /// <summary>The name of a table record, a block or a named object, as the bytes of the file.</summary>
        public byte[] RecordName;

        /// <summary>Of a control object: the handles of its records.</summary>
        public List<ulong> Entries = new List<ulong>();

        /// <summary>The first owned entity: of a block its first entity, of a polyline its first vertex, of an insert its first attribute.</summary>
        public ulong First;

        /// <summary>The last owned entity, as <see cref="First"/>.</summary>
        public ulong Last;

        /// <summary>
        /// From R2004, where no entity names the next: the owned entities in their order (of a block its entities, of a
        /// polyline its vertices, of an insert its attributes); null before R2004, where <see cref="First"/> and
        /// <see cref="Last"/> give them.
        /// </summary>
        public List<ulong> Owned;

        /// <summary>Of a polyline or an insert with attributes: the handle of the SEQEND that closes it.</summary>
        public ulong SeqEnd;

        /// <summary>Of a block header: the handle of its BLOCK entity.</summary>
        public ulong BlockEntity;

        /// <summary>Of a block header: the handle of its ENDBLK entity.</summary>
        public ulong EndBlock;

        /// <summary>Of a block header: the block flags of group 70 of its BLOCK. Of a control object: the count of records it gives for group 70.</summary>
        public int BlockFlags;

        /// <summary>Of a block header: the groups of the BLOCK entity that the header keeps (base point, path of the reference).</summary>
        public List<DwgGroup> BlockBody;

        /// <summary>Of a DBCOLOR from R2004: its colour; null for another type.</summary>
        public DwgColour BookColour;

        /// <summary>Of an entity: the picture of it kept for programs that cannot draw it (proxy graphics); null for none.</summary>
        public byte[] ProxyGraphics;

        /// <summary>Of an MTEXT: the height and the width of the extents of its text; null for another type.</summary>
        public double[] TextExtents;

        /// <summary>Of a LEADER: the handle of its annotation; 0 for none.</summary>
        public ulong Annotation;

        /// <summary>Of a layout from R2004: its viewports in their order; null before R2004.</summary>
        public List<ulong> Viewports;

        /// <summary>Adds a group to <see cref="Body"/>.</summary>
        public void Add(int code, object value) => Body.Add(new DwgGroup(code, value));

        /// <summary>Adds a point to <see cref="Body"/>: its coordinates under <paramref name="code"/>, the code plus 10 and, of three, plus 20.</summary>
        public void AddPoint(int code, double[] point)
        {
            for (int i = 0; i < point.Length; i++)
            {
                Body.Add(new DwgGroup(code + (10 * i), point[i]));
            }
        }
    }
}
