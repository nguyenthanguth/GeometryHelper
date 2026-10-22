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
        /// Points, radii, elevations and the lengths of axes are scaled; angles, bulges, ratios, weights, knots and
        /// directions are not, having no unit. A file in millimetres is read to the bit either way. A file with no unit,
        /// or one the DXF reference does not list, is not scaled, and a warning says so.
        /// </remarks>
        public bool ToMillimetres { get; set; }

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
        /// vertices of a closed 3D POLYLINE and the faces of a mesh lie in one plane. It is used for nothing else.
        /// </para>
        /// <para>
        /// A LINE 0.0004 long is no segment within the default tolerance and is left out; within 1E-5 it is a segment. A
        /// 3DFACE whose four corners stand off one plane by more than the planar tolerance is two triangles.
        /// </para>
        /// </remarks>
        public Tolerance Tolerance { get; set; } = Tolerance.Default;

        /// <summary>
        /// Gets or sets whether each entity keeps every group of its record as the file has it, in
        /// <see cref="DxfEntity.Groups"/>; true by default. False leaves them empty, which a large file reads in less memory:
        /// the shapes and the look of the entities are read either way.
        /// </summary>
        /// <remarks>
        /// Of 100 000 entities of eight kinds, a file of 34 MB, the document holds about four times as much with the groups as
        /// without; the guide gives the numbers.
        /// </remarks>
        public bool KeepGroups { get; set; } = true;
    }
}
