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

        /// <summary>The name of the record at <paramref name="handle"/>, or <paramref name="fallback"/>; not written where it is <paramref name="omitWhen"/>.</summary>
        public DwgName(ulong handle, string fallback = "", string omitWhen = null)
        {
            Handle = handle;
            Fallback = fallback;
            OmitWhen = omitWhen;
        }
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

        /// <summary>The name of a table record, a block or a named object, as the bytes of the file.</summary>
        public byte[] RecordName;

        /// <summary>Of a control object: the handles of its records.</summary>
        public List<ulong> Entries = new List<ulong>();

        /// <summary>The first owned entity: of a block its first entity, of a polyline its first vertex, of an insert its first attribute.</summary>
        public ulong First;

        /// <summary>The last owned entity, as <see cref="First"/>.</summary>
        public ulong Last;

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
