namespace GeometryHelper.Export
{
    /// <summary>
    /// How <see cref="DxfReader"/> reads a DXF file: whether a drawing in the plan comes back as shapes of the plane, whether
    /// its numbers are scaled to millimetres, and the tolerance that says which entities have no shape to them.
    /// </summary>
    /// <remarks>
    /// The defaults read a file as it is: every shape of space, the numbers as the file prints them, and the default
    /// tolerance. <c>new DxfReadOptions { PlanAs2D = true }</c> reads a plan drawn in AutoCAD as polylines, arcs and circles
    /// of the plane.
    /// </remarks>
    public sealed class DxfReadOptions
    {
        /// <summary>
        /// Gets or sets whether an entity that lies on the floor is read as the shape of the plane it is. False by default,
        /// which reads every entity as the shape of space it is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An entity in world coordinates (POINT, LINE, RAY, XLINE, SPLINE, 3D POLYLINE) lies on the floor where every height
        /// of it is nought, whatever its extrusion, which for a POINT or a LINE says only which way its thickness stands. One
        /// in the coordinates of its extrusion's plane (CIRCLE, ARC, LWPOLYLINE, 2D POLYLINE, SOLID, TRACE) and an ELLIPSE lie
        /// on it where the extrusion is (0, 0, 1) or (0, 0, -1) exactly, or not given, and every height is nought. On
        /// (0, 0, -1), as AutoCAD writes a mirrored entity, x is turned over and a bulge negated, the vertices kept in their
        /// order, and an ARC from a to b runs clockwise from pi - a to pi - b, from the start the file gives as the arc read in
        /// space does: the ARC of radius 10 about the origin from 0 to 90 degrees on it is the clockwise
        /// <see cref="Geometry.GeoArc2"/> from (-10, 0) to (0, 10). A part of an ELLIPSE and a SPLINE keep the direction of
        /// the file too.
        /// </para>
        /// <para>
        /// A file <see cref="DxfWriter"/> wrote from shapes of the plane reads back so as those shapes: a closed LWPOLYLINE
        /// with bulges at no elevation is a <see cref="Geometry.GeoPolygonArc2"/>, and the same polyline at an elevation of
        /// 5 a <see cref="Geometry.GeoPolygonArc3"/> at z = 5. A 3DFACE and a mesh are of space either way.
        /// </para>
        /// </remarks>
        public bool PlanAs2D { get; set; }

        /// <summary>
        /// Gets or sets whether the coordinates and sizes are scaled to millimetres by the unit the file says it is in
        /// (<see cref="DxfDocument.Units"/>): a drawing in metres has its numbers times 1 000. False by default, which keeps
        /// the numbers as the file prints them.
        /// </summary>
        /// <remarks>
        /// Points, radii, elevations and the lengths of axes are scaled, and so are the insertion point of an INSERT, the
        /// base point of a block and the spacings of a MINSERT; angles, bulges, ratios, weights, knots, directions and the
        /// scales of an INSERT are not, having no unit. A file in millimetres is read to the bit either way. A file with no unit,
        /// or one the DXF reference does not list, is not scaled, and a warning says so.
        /// </remarks>
        public bool ToMillimeters { get; set; }

        /// <summary>
        /// Gets or sets the tolerance that says which entities have no shape to them and which four corners of a 3DFACE
        /// lie in one plane; <see cref="Tolerance.Default"/> unless set.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The global tolerance is not read. The point tolerance says that a LINE is of no length, a CIRCLE, ARC or ELLIPSE of
        /// no radius or axis, that two neighbouring vertices of a polyline are one (as the polyline constructors drop such a
        /// vertex), that two fit points of a SPLINE are one, and which corners of a polygon, a face or a mesh are one. The
        /// vector tolerance says that a RAY or XLINE has no direction, and, read as an area as a polygon reads it, that a
        /// closed polyline with arcs encloses no area. The planar tolerance says whether the four corners of a 3DFACE, the
        /// vertices of a closed 3D POLYLINE and the faces of a mesh lie in one plane. The point tolerance also says whether an
        /// INSERT's scaling is so nearly even that a circle, an arc or a chain with arcs of its block stays one
        /// (<see cref="ExpandBlocks"/>). It is used for nothing else.
        /// </para>
        /// <para>
        /// A LINE 0.0004 long is no segment within the default tolerance and is left out; within 1E-5 it is a segment. A
        /// 3DFACE whose four corners stand off one plane by more than the planar tolerance is two triangles.
        /// </para>
        /// </remarks>
        public Tolerance Tolerance { get; set; } = Tolerance.Default;

        /// <summary>
        /// Gets or sets whether each entity keeps every group of its record as the file has it, in
        /// <see cref="DxfShape.Groups"/>; true by default. False leaves them empty, which a large file reads in less memory:
        /// the shapes and the look of the entities are read either way.
        /// </summary>
        /// <remarks>
        /// Of 100 000 entities of eight kinds, a file of 34 MB, the document holds about four times as much with the groups as
        /// without; the guide gives the numbers.
        /// </remarks>
        public bool KeepGroups { get; set; } = true;

        /// <summary>
        /// Gets or sets whether each INSERT is read as the entities of its block, placed where it puts them; true by
        /// default. False leaves the INSERTs out, counted in <see cref="DxfDocument.Skipped"/>: the blocks are read into
        /// <see cref="DxfDocument.Blocks"/> either way.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An entity of a block is placed by the INSERT's own system: the block's base point taken to the origin, the scales
        /// along x, y and z, the turn about z, the insertion point, then the system of the INSERT's extrusion; a MINSERT's
        /// cells, row after row, each shifted along the turned axes by its column and row spacing, not scaled. An INSERT
        /// inside a block is placed within it, the two placements composed, to a depth of 64; a block that inserts itself,
        /// at once or through others, is not placed again, and a warning says so. An entity on layer 0 takes the layer of
        /// its INSERT, and a colour, linetype or lineweight BYBLOCK the INSERT's.
        /// </para>
        /// <para>
        /// Points, segments, rays, lines without ends, chains and polygons without arcs, faces, meshes and splines are
        /// carried point by point, a spline by its control points, which any placement carries exactly, and an ELLIPSE by
        /// the images of its semi-axes. A turn, an even scaling and a mirror place a circle, an arc and a chain with arcs as
        /// the same shape, the arc running the way its image runs, so a mirrored arc runs the other way round. An uneven
        /// scaling places a circle as the ellipse it becomes, an arc as the spline that is exactly that part of the ellipse,
        /// and a chain with arcs as a segment for each straight edge and such a spline for each arc edge, each an entity of
        /// its own, in the order of the chain: the circle of radius 10 scaled by 3 along x is the
        /// <see cref="Geometry.GeoEllipse3"/> 30 by 10. A scaling so nearly even that the circle placed whole lands within the
        /// point tolerance of <see cref="Tolerance"/> of where the scaling puts each of its points keeps it a circle, by the
        /// rule of <see cref="Geometry.GeoTransform3.Transform(Geometry.GeoCircle3)"/>: the circle of radius 10 scaled by
        /// 1.0000001 along y is the circle of radius 10, and by 1.001 the ellipse 10.01 by 10.
        /// </para>
        /// </remarks>
        public bool ExpandBlocks { get; set; } = true;

        private int _maxPlacedEntities = 2000000;

        /// <summary>
        /// Gets or sets whether the size of a block is taken as nought before its INSERT is placed, so that only the count of
        /// what is placed holds the bound of <see cref="MaxPlacedEntities"/>; for the tests of that count alone.
        /// </summary>
        internal bool UnsizedBlocks { get; set; }

        /// <summary>
        /// Gets or sets how many blocks are worked out, in all, for the sizes of the INSERTs of one reading, the drawing's and
        /// those of the blocks read on their own; 1 000 000, and smaller for the tests of that bound alone.
        /// </summary>
        internal int MostSizedBlocks { get; set; } = 1000000;

        /// <summary>
        /// Gets or sets how many entities the INSERTs may place from blocks in one reading, counted across the drawing and
        /// the blocks read for <see cref="DxfDocument.Blocks"/>; 2 000 000 by default.
        /// </summary>
        /// <remarks>
        /// Blocks placed inside blocks multiply: a block of 100 INSERTs of a block of 100 INSERTs of a block of 100 INSERTs of a
        /// line is a file of 18 KB and 100 000 000 lines. An INSERT is placed whole or not at all: where the entities it would
        /// place, worked out from its block before any is placed, would take the count past this number, it places none, is
        /// counted in <see cref="DxfDocument.Skipped"/>, and a warning names the number and how many entities the INSERTs
        /// left out would have placed. The INSERTs of the drawing are placed first, in the order of the file. Whatever the
        /// working out says, of blocks that insert each other among them, the count of what is placed holds the bound too: an
        /// INSERT whose entities would pass it is taken back whole, with what its placing counted.
        /// </remarks>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the number set is not above nought.</exception>
        public int MaxPlacedEntities
        {
            get => _maxPlacedEntities;
            set
            {
                Guard.Positive(value, nameof(value), "The number of entities the INSERTs may place has to be above nought.");
                _maxPlacedEntities = value;
            }
        }
    }
}
