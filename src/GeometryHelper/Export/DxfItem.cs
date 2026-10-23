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
    /// is the colour 0 and BYLAYER 256, where <see cref="DxfEntity.Colour"/> reads both as none; a lineweight by layer is
    /// -1; a layer is spelled with any space around it and any escape in it. Nothing is taken from a block: a LINE of a block
    /// on layer 0 is on layer 0.
    /// </para>
    /// </remarks>
    public sealed class DxfItem
    {
        private static readonly IReadOnlyList<object> NoShapes = new object[0];

        private readonly DxfFile _file;
        private readonly int _first;
        private readonly int _count;
        private readonly string _block;
        private DxfFile.Look _look;
        private IReadOnlyList<object> _shapes;

        internal DxfItem(DxfFile file, int first, int count, string type, string block)
        {
            _file = file;
            _first = first;
            _count = count;
            _block = block;
            Type = type;
        }

        /// <summary>
        /// Gets the type of the entity as the file names it, such as <c>LINE</c>, <c>LWPOLYLINE</c>, <c>HATCH</c> or
        /// <c>INSERT</c>.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Gets the handle of the entity (group 5), as the file writes it in hexadecimal, the spaces around it taken off; null
        /// where it has none, as in a file of R12.
        /// </summary>
        public string Handle => Look.Handle;

        /// <summary>
        /// Gets the layer of the entity (group 8) as its record writes it, any space around the name and any escape in it
        /// (<c>\U+00E9</c>) kept; null where the record gives none, which the reader takes as layer 0.
        /// </summary>
        public string Layer => Look.Layer;

        /// <summary>
        /// Gets the colour of the entity (group 62) as its record writes it: an AutoCAD colour index from 1 to 255, 0 for
        /// BYBLOCK and 256 for BYLAYER, below nought for a colour turned off; null where the record gives none, or one that is
        /// not a whole number.
        /// </summary>
        public int? Colour => Look.Colour;

        /// <summary>
        /// Gets the colour of the entity as red, green and blue (group 420) as its record writes it, 0xRRGGBB, with whatever
        /// the file puts above those 24 bits; null where the record gives none, or one that is not a whole number.
        /// </summary>
        public int? TrueColour => Look.TrueColour;

        /// <summary>
        /// Gets the name of the linetype of the entity (group 6) as its record writes it, such as <c>DASHED</c>,
        /// <c>BYLAYER</c> or <c>ByBlock</c>; null where the record gives none, which takes the linetype of the layer.
        /// </summary>
        public string Linetype => Look.Linetype;

        /// <summary>
        /// Gets the lineweight of the entity (group 370) as its record writes it, in hundredths of a millimetre: 25 for a line
        /// 0.25 wide, -1 by layer, -2 by block and -3 the default; null where the record gives none, or one that is not a
        /// whole number.
        /// </summary>
        public int? Lineweight => Look.Lineweight;

        /// <summary>
        /// Gets the thickness of the entity (group 39), how far it stands up along its extrusion; nought where the record
        /// gives none, or one that is not a finite number.
        /// </summary>
        public double Thickness => Look.Thickness;

        /// <summary>
        /// Gets every group of the entity's record, in the order of the file, after the group 0 that names its type, each
        /// value the text the file prints; read from the file on each call, a new list each time.
        /// </summary>
        /// <remarks>
        /// Of a POLYLINE they run on through its VERTEX records and its SEQEND, and of an INSERT with attributes through its
        /// ATTRIB records and its SEQEND, each opened by its own group 0, as <see cref="DxfEntity.Groups"/> has them. A comment
        /// (group 999) is not among them, though the file keeps it.
        /// </remarks>
        public IReadOnlyList<DxfGroup> Groups => _file.GroupsOf(_first, _count);

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
        /// shapes are read once all the same, or read twice and one reading kept: every thread gets the same list.
        /// </remarks>
        public IReadOnlyList<object> Shapes
        {
            get
            {
                IReadOnlyList<object> shapes = Volatile.Read(ref _shapes);

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
        /// Gets the one shape of the entity: the shape of <see cref="Shapes"/> where it has one, the same object each time;
        /// null where it has none or more than one.
        /// </summary>
        /// <remarks>
        /// A LINE is its <see cref="Geometry.GeoLine3"/>, an INSERT its <see cref="DxfInsert"/>, and a HATCH null. This is
        /// the shape a later release lets be set, the entity's record written anew from it.
        /// </remarks>
        public object Shape
        {
            get
            {
                IReadOnlyList<object> shapes = Shapes;
                return shapes.Count == 1 ? shapes[0] : null;
            }
        }

        /// <summary>The index of the entity's first record in the file.</summary>
        internal int First => _first;

        /// <summary>How many records the entity is made of: one, or a POLYLINE's or an INSERT's with those following it.</summary>
        internal int Count => _count;

        /// <summary>The name of the block the entity is in; null for an entity of the drawing.</summary>
        internal string Block => _block;

        private DxfFile.Look Look
        {
            get
            {
                DxfFile.Look look = Volatile.Read(ref _look);

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
    }
}
