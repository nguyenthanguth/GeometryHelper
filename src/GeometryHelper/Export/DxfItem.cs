using System.Collections.Generic;
using System.Threading;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An entity of a <see cref="DxfFile"/>: its type, its look as its record writes it, every group of it as the file has it,
    /// and the library's shapes for it, read from the file the first time they are asked for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Shapes"/> is what <see cref="DxfReader"/> reads of the entity, with its options left at their defaults and
    /// the INSERT placing nothing: a <see cref="Geometry.GeoLine3"/> for a LINE, a <see cref="Geometry.GeoPolygonArc3"/> for
    /// a closed LWPOLYLINE with bulges, a <see cref="DxfText"/> for a TEXT, a <see cref="DxfInsert"/> for an INSERT, and none
    /// for a HATCH, a DIMENSION and the other types the reader does not read, which the file keeps as they are.
    /// <see cref="Shape"/> is the one shape of an entity read as one, which is nearly every entity read.
    /// </para>
    /// <para>
    /// The look is the groups of the record as written, so that what is read can be written back as it was: a colour BYBLOCK
    /// is the colour 0 and BYLAYER 256, where <see cref="DxfShape.Color"/> reads both as none; a lineweight by layer is
    /// -1; a layer is spelled with any space around it and any escape in it. Nothing is taken from a block: a LINE of a block
    /// on layer 0 is on layer 0.
    /// </para>
    /// <para>
    /// An entity of model space or paper space can be changed: its <see cref="Shape"/>, its layer, colour, true colour,
    /// linetype, lineweight and thickness are set, and its record is written anew, every group the change is not about kept
    /// as the file had it. A setter given the value its getter gives changes nothing. An entity of a block cannot be changed
    /// yet, nor one removed from its space.
    /// </para>
    /// </remarks>
    internal sealed class DxfItem
    {
        private static readonly IReadOnlyList<object> NoShapes = new object[0];

        private readonly DxfFile _ownFile;
        private readonly int _first;
        private readonly int _count;
        private string _block;
        private object _wrapper;
        private bool _removed;
        private DxfFile.Look _look;
        private IReadOnlyList<object> _shapes;

        internal DxfItem(DxfFile file, int first, int count, string type, string block)
        {
            _ownFile = file;
            _first = first;
            _count = count;
            _block = block;
            Type = type;
        }

        /// <summary>An entity added to the file, its records its own.</summary>
        internal DxfItem(DxfFile file, DxfFile.Piece piece, string type)
        {
            _ownFile = file;
            _first = -1;
            Piece = piece;
            Type = type;
        }

        /// <summary>
        /// A record that follows an entity, an ATTRIB of an INSERT, seen as an entity of its own: its one record is that record
        /// of the entity's, read and written through the entity, and it is erased with it.
        /// </summary>
        internal DxfItem(DxfItem parent, int index)
        {
            _ownFile = parent._file;
            _first = -1;
            _block = parent._block;
            Parent = parent;
            Index = index;
            IsPaper = parent.IsPaper;
            Type = _file.RecordType(parent, index);
        }

        /// <summary>
        /// The file the item's records are in: a record that follows an entity is in the entity's, and goes with it when an
        /// entity made in code is added to a file.
        /// </summary>
        private DxfFile _file => Parent != null ? Parent._file : _ownFile;

        /// <summary>
        /// The entity whose record this is, for a record that follows one; null otherwise. The entity's item once it is added to
        /// a file, as the followers of an entity made in code go with it.
        /// </summary>
        internal DxfItem Parent { get; set; }

        /// <summary>Which record of <see cref="Parent"/> this is: 1 for the first that follows it; kept as followers are put in or taken out before it.</summary>
        internal int Index { get; set; }

        /// <summary>
        /// The items of the records that follow the entity, by the index of the record (none at 0), each made the first time it is
        /// asked for by <see cref="DxfFile.FollowerOf"/>; null until one is.
        /// </summary>
        internal List<DxfItem> Followers { get; set; }

        /// <summary>
        /// Gets the type of the entity as the file names it, such as <c>LINE</c>, <c>LWPOLYLINE</c>, <c>HATCH</c> or
        /// <c>INSERT</c>; the type of its new shape once <see cref="Shape"/> is set to one written as another, as a LINE given
        /// a circle is a CIRCLE.
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// Gets the handle of the entity (group 5), as the file writes it in hexadecimal, the spaces around it taken off; null
        /// where it has none, as in a file of R12. A new shape keeps it.
        /// </summary>
        public string Handle => Look.Handle;

        /// <summary>
        /// Gets or sets the layer of the entity (group 8) as its record writes it, any space around the name and any escape in
        /// it (<c>\U+00E9</c>) kept; null where the record gives none, which the reader takes as layer 0.
        /// </summary>
        /// <value>
        /// Set, the name as it is to be written, letters beyond ASCII in the file's code page, or as their escapes where the
        /// page has them not; null takes the group out. A layer the layer table does not list, letters of either case the same,
        /// is added to it, in the colour 7 with the linetype CONTINUOUS, and the log says so. The vertices and the end of a
        /// POLYLINE, and the end of an INSERT's attributes, are put on it too, as AutoCAD keeps them.
        /// </value>
        /// <exception cref="System.ArgumentException">Thrown when the name set is empty, has a space at either end, or has a
        /// control character or one of &lt; &gt; / " : ; ? * | = ` in it, which AutoCAD does not take in a name.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity is of a block or removed.</exception>
        public string Layer
        {
            get => Look.Layer;
            set => _file.SetLayer(this, value);
        }

        /// <summary>
        /// Gets or sets the colour of the entity (group 62) as its record writes it: an AutoCAD colour index from 1 to 255, 0
        /// for BYBLOCK and 256 for BYLAYER, below nought for a colour turned off; null where the record gives none, or one that
        /// is not a whole number.
        /// </summary>
        /// <value>Set, from 0 to 256; null takes the group out, which leaves the colour to the layer.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the colour set is below 0 or above 256.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity is of a block or removed.</exception>
        public int? Colour
        {
            get => Look.Colour;
            set => _file.SetColour(this, value);
        }

        /// <summary>
        /// Gets or sets the colour of the entity as red, green and blue (group 420) as its record writes it, 0xRRGGBB, with
        /// whatever the file puts above those 24 bits; null where the record gives none, or one that is not a whole number.
        /// </summary>
        /// <value>Set, from 0 to 0xFFFFFF; null takes the group out.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the colour set is below 0 or above 0xFFFFFF.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity is of a block or removed, or a colour is
        /// set in a file older than R2004 (AC1018), which has no true colours.</exception>
        public int? TrueColour
        {
            get => Look.TrueColour;
            set => _file.SetTrueColour(this, value);
        }

        /// <summary>
        /// Gets or sets the name of the linetype of the entity (group 6) as its record writes it, such as <c>DASHED</c>,
        /// <c>BYLAYER</c> or <c>ByBlock</c>; null where the record gives none, which takes the linetype of the layer.
        /// </summary>
        /// <value>Set, a linetype of the file's linetype table, letters of either case the same, or BYLAYER or BYBLOCK; null
        /// takes the group out.</value>
        /// <exception cref="System.ArgumentException">Thrown when the linetype set is not in the file's linetype table.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity is of a block or removed.</exception>
        public string Linetype
        {
            get => Look.Linetype;
            set => _file.SetLinetype(this, value);
        }

        /// <summary>
        /// Gets or sets the lineweight of the entity (group 370) as its record writes it, in hundredths of a millimetre: 25 for
        /// a line 0.25 wide, -1 by layer, -2 by block and -3 the default; null where the record gives none, or one that is not
        /// a whole number.
        /// </summary>
        /// <value>Set, one of AutoCAD's: -3, -2, -1, 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106,
        /// 120, 140, 158, 200 or 211; null takes the group out.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the lineweight set is not one of AutoCAD's.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity is of a block or removed, or a lineweight is
        /// set in a file older than R2000 (AC1015), which has no lineweights.</exception>
        public int? Lineweight
        {
            get => Look.Lineweight;
            set => _file.SetLineweight(this, value);
        }

        /// <summary>
        /// Gets or sets the thickness of the entity (group 39), how far it stands up along its extrusion; nought where the
        /// record gives none, or one that is not a finite number.
        /// </summary>
        /// <value>Set, a finite number; nought takes the group out.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity is of a block or removed, or a thickness
        /// other than nought is set on a type that has none: those that have one are the LINE, POINT, CIRCLE, ARC, LWPOLYLINE,
        /// POLYLINE, TEXT, SOLID and TRACE.</exception>
        public double Thickness
        {
            get => Look.Thickness;
            set => _file.SetThickness(this, value);
        }

        /// <summary>
        /// Gets every group of the entity's record, in the order of the file, after the group 0 that names its type, each
        /// value the text the file prints; read from the file on each call, a new list each time.
        /// </summary>
        /// <remarks>
        /// Of a POLYLINE they run on through its VERTEX records and its SEQEND, and of an INSERT with attributes through its
        /// ATTRIB records and its SEQEND, each opened by its own group 0, as <see cref="DxfShape.Groups"/> has them. A comment
        /// (group 999) is not among them, though the file keeps it.
        /// </remarks>
        public IReadOnlyList<DxfTypedValue> Groups => _file.GroupsOf(this);

        /// <summary>
        /// Gets the library's shapes for the entity, read from the file the first time they are asked for and kept: what
        /// <see cref="DxfReader"/> reads of it with its default options, in the coordinates of the file, the entity's own where
        /// it is in a block; for an INSERT, its <see cref="DxfInsert"/>.
        /// </summary>
        /// <remarks>
        /// Nearly every entity read is one shape. The reader reads two of a 3DFACE whose four corners are not in one plane,
        /// the triangles it is cut into, and of a polyface mesh of fewer than the four faces a body needs, each face; none of
        /// a type it does not read, such as HATCH, DIMENSION or ATTRIB, nor of an entity with no shape to it, such as a LINE of
        /// no length. An entity of paper space is read as one of model space is. Asked for from several threads at once, the
        /// shapes are read once all the same, or read twice and one reading kept: every thread gets the same list. Once the
        /// entity is changed, they are read again from its new record.
        /// </remarks>
        public IReadOnlyList<object> Shapes
        {
            get
            {
                IReadOnlyList<object> shapes = Volatile.Read(ref _shapes);

                // A record that follows an entity can be written through the entity as well: it is read each time.
                if (Parent != null)
                {
                    List<object> now = _file.ShapesOf(this);
                    return now.Count == 0 ? NoShapes : now.AsReadOnly();
                }

                if (shapes == null)
                {
                    List<object> read = _file.ShapesOf(this);
                    Interlocked.CompareExchange(ref _shapes, read.Count == 0 ? NoShapes : read.AsReadOnly(), null);
                    shapes = Volatile.Read(ref _shapes);
                }

                return shapes;
            }
        }

        /// <summary>
        /// Gets or sets the one shape of the entity: the shape of <see cref="Shapes"/> where it has one, the same object each
        /// time; null where it has none or more than one.
        /// </summary>
        /// <value>
        /// <para>
        /// Set, the shape the entity is written from anew, by the overload of <see cref="DxfWriter"/>'s Add for it: a LINE given
        /// a <see cref="Geometry.GeoLine3"/> from (0, 0, 0) to (100, 0, 0) is that segment, its handle, owner, layer, look,
        /// reactors, extension dictionary and extended data kept as they were and in their order, and every group the reader
        /// does not take for its geometry kept with them. A shape written as another type changes the type, the handle staying:
        /// a LINE given a <see cref="Geometry.GeoCircle3"/> is a CIRCLE, and what the old type alone has, such as the style of a
        /// TEXT, is left out. A TEXT or an MTEXT takes a <see cref="DxfText"/>. The thickness is kept where the new type has one;
        /// the widths of a polyline are not kept. The shape is written as the file's version holds it: in a file of R12, a
        /// chain or loop of the plane as a POLYLINE with its VERTEX records, read back exactly as the lightweight polyline is,
        /// and no subclass markers. The extents of the header grow to hold it.
        /// </para>
        /// <para>
        /// The shape is read back from the new record the next time it is asked for: the segment of the LINE to the bit, the
        /// shapes of the plane as the shapes of space the reader gives.
        /// </para>
        /// </value>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape set is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the shape set is not one <see cref="DxfWriter"/> writes without
        /// a tolerance, is written as more than one entity (a face with holes, a body, a box), or as a type the file's version
        /// cannot hold and has no exact form of its own for: a SPLINE, ELLIPSE, MTEXT, RAY or XLINE in a file of R12.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity is of a type that has no shape of the
        /// library's to set (HATCH, DIMENSION, INSERT and the other types the reader does not read), of a block, or removed.</exception>
        public object Shape
        {
            get
            {
                IReadOnlyList<object> shapes = Shapes;
                return shapes.Count == 1 ? shapes[0] : null;
            }

            set => _file.SetShape(this, value);
        }

        /// <summary>The index of the entity's first record in the file; -1 for an entity added, whose records are its own.</summary>
        internal int First => _first;

        /// <summary>How many records the entity is made of: one, or a POLYLINE's or an INSERT's with those following it.</summary>
        internal int Count => _count;

        /// <summary>The name of the block the entity is in; null for an entity of the drawing.</summary>
        internal string Block
        {
            get => _block;
            set => _block = value;
        }

        /// <summary>The file the entity is of.</summary>
        internal DxfFile File => _file;

        /// <summary>The list of pieces the records of an entity added are in, to be taken out of when it is removed; null for one as the file was read.</summary>
        internal List<DxfFile.Piece> Home { get; set; }

        /// <summary>
        /// The object of the editing model that stands for the record, a <see cref="DxfEntity"/> or a table's record, made the
        /// first time it is asked for: one object for one record, whichever thread asks first.
        /// </summary>
        internal object Wrapper => Volatile.Read(ref _wrapper);

        /// <summary>Keeps the object that stands for the record, where none is kept yet; the one kept either way.</summary>
        internal object KeepWrapper(object wrapper) => Interlocked.CompareExchange(ref _wrapper, wrapper, null) ?? wrapper;

        /// <summary>Makes another object stand for the record, as a converted polyline takes the place of the one it was made from.</summary>
        internal void ReplaceWrapper(object wrapper) => Volatile.Write(ref _wrapper, wrapper);

        /// <summary>
        /// The extended data records of the entity are to be given at their next writing, each by the index of its record (0
        /// the entity's own) and that record's type, as an entity moved has the world points of its extended data, and of its
        /// vertices' and attributes', moved in the same writing of its records; null where there is none to give.
        /// </summary>
        internal List<PendingData> PendingRecords { get; set; }

        /// <summary>Whether the entity is being moved with the records that follow it, whose extended data it moves itself: a record that follows it, moved by it, moves none of its own.</summary>
        internal bool CarryingFollowers { get; set; }

        /// <summary>
        /// The block of the drawing a copy of an INSERT in no file was copied from, which it places whatever it is renamed: its
        /// name is read as the block's; null for any other item, and once the copy's name is set.
        /// </summary>
        internal DxfFileBlock OriginBlock { get; set; }

        /// <summary>The records the entity's own, once it is changed or added; null for an entity as the file was read.</summary>
        internal DxfFile.Piece Piece { get; set; }

        /// <summary>The space of the drawing the entity is in, to be removed from; null for one of paper space or a block.</summary>
        internal DxfSpace Space { get; set; }

        /// <summary>Whether the entity is in paper space.</summary>
        internal bool IsPaper { get; set; }

        /// <summary>Whether the entity has been removed from its space; a record that follows one, with it.</summary>
        internal bool IsRemoved
        {
            get => _removed || (Parent != null && Parent.IsRemoved);
            set => _removed = value;
        }

        /// <summary>The entity's record has been written anew: its type is that of the new record, and its look and shapes are read again.</summary>
        internal void Changed(string type)
        {
            Type = type;
            Volatile.Write(ref _look, null);
            Volatile.Write(ref _shapes, null);
        }

        private DxfFile.Look Look
        {
            get
            {
                DxfFile.Look look = Volatile.Read(ref _look);

                if (Parent != null)
                {
                    return _file.LookOf(this);
                }

                if (look == null)
                {
                    look = _file.LookOf(this);
                    Interlocked.CompareExchange(ref _look, look, null);
                }

                return look;
            }
        }

        /// <summary>
        /// Gives the entity as a line for debugging: its type, its handle and its layer.
        /// </summary>
        /// <returns>The entity as a line, such as <c>LINE 2F on steel</c>.</returns>
        public override string ToString() => Type + (Handle != null ? " " + Handle : string.Empty) + " on " + (Layer ?? "0");

        /// <summary>Extended data a record of an entity is to be given: the index of the record, its type, and the groups.</summary>
        internal sealed class PendingData
        {
            internal PendingData(int index, string type, IReadOnlyList<DxfTypedValue> groups)
            {
                Index = index;
                Type = type;
                Groups = groups;
            }

            internal int Index { get; }

            internal string Type { get; }

            internal IReadOnlyList<DxfTypedValue> Groups { get; }
        }
    }
}
