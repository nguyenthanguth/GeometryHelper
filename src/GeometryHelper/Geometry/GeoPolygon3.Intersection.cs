using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a polygon crosses another shape.
    /// </summary>
    public sealed partial class GeoPolygon3
    {
        /// <summary>
        /// Tries to find the point where a line segment crosses this polygon, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(line, this, out intersection);

        /// <summary>
        /// Tries to find the point where a line segment crosses this polygon, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(line, this, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where a ray crosses this polygon, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(ray, this, out intersection);

        /// <summary>
        /// Tries to find the point where a ray crosses this polygon, within a tolerance: the first point
        /// <see cref="GeoRay3.GetIntersections(GeoPolygon3, Tolerance)"/> gives, and false where it gives none.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(ray, this, out intersection, tolerance);
        /// <summary>
        /// Gets every point where an arc crosses this polygon, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => Arc3.GetIntersections(arc, this);

        /// <summary>
        /// Gets every point where an arc crosses this polygon, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Arc3.GetIntersections(arc, this, tolerance);

        /// <summary>
        /// Tries to find where an arc crosses this polygon, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(arc, this, out intersections);

        /// <summary>
        /// Tries to find where an arc crosses this polygon, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses this polygon, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => Arc3.GetIntersections(circle, this);

        /// <summary>
        /// Gets every point where a circle crosses this polygon, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Arc3.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses this polygon, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(circle, this, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses this polygon, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a segment crosses this polygon.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets every point where a segment crosses this polygon, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>The same points as <see cref="GeoLine3.GetIntersections(GeoPolygon3, Tolerance)"/>: the crossing, or the point of a touch.</remarks>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Intersection3.MeetingOfGiven(line, this, tolerance);

        /// <summary>
        /// Gets every point where an edge crosses this polygon.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where an edge crosses this polygon, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);

        /// <summary>
        /// Gets every point where a curved chain crosses this polygon.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => ArcChain3.GetIntersections(chain, this);

        /// <summary>
        /// Gets every point where a curved chain crosses this polygon, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets every point where a curved loop crosses this polygon.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => ArcChain3.GetIntersections(loop, this);

        /// <summary>
        /// Gets every point where a curved loop crosses this polygon, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets every point where this polygon crosses a straight chain: the same points, in the same order, as
        /// <see cref="GeoPolyline3.GetIntersections(GeoPolygon3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline) => Intersection3.GetIntersections(polyline, this);

        /// <summary>
        /// Gets every point where this polygon crosses a straight chain, within a tolerance: the same points, in the
        /// same order, as <see cref="GeoPolyline3.GetIntersections(GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline, Tolerance tolerance)
            => Intersection3.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Tries to find where this polygon crosses a straight chain: the same answer, and the same points in the same
        /// order, as <see cref="GeoPolyline3.TryIntersectWith(GeoPolygon3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(polyline, this, out intersections);

        /// <summary>
        /// Tries to find where this polygon crosses a straight chain, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="GeoPolyline3.TryIntersectWith(GeoPolygon3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Gets the points where this polygon and a triangle cross: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoTriangle3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => Intersection3.GetIntersections(triangle, this);

        /// <summary>
        /// Gets the points where this polygon and a triangle cross, within a tolerance: the same points, in the same order,
        /// as <see cref="Intersection3.GetIntersections(GeoTriangle3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Intersection3.GetIntersections(triangle, this, tolerance);

        /// <summary>
        /// Tries to find where this polygon and a triangle cross: the same answer, and the same points in the same order,
        /// as <see cref="Intersection3.TryIntersectWith(GeoTriangle3, GeoPolygon3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(triangle, this, out intersections);

        /// <summary>
        /// Tries to find where this polygon and a triangle cross, within a tolerance: the same answer, and the same points
        /// in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoTriangle3, GeoPolygon3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(triangle, this, out intersections, tolerance);

        /// <summary>
        /// Gets the points where this polygon and another cross: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="other">The polygon.</param>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along y =
        /// 50 from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0); two squares in the floor, the second moved 50 along
        /// X and 50 along Y, cross at (100, 50, 0) and (50, 100, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 other) => Intersection3.GetIntersections(this, other);

        /// <summary>
        /// Gets the points where this polygon and another cross, within a tolerance: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="other">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along y =
        /// 50 from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0); two squares in the floor, the second moved 50 along
        /// X and 50 along Y, cross at (100, 50, 0) and (50, 100, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 other, Tolerance tolerance) => Intersection3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where this polygon and another cross: the same answer, and the same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoPolygon3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="other">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along y =
        /// 50 from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0); two squares in the floor, the second moved 50 along
        /// X and 50 along Y, cross at (100, 50, 0) and (50, 100, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 other, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where this polygon and another cross, within a tolerance: the same answer, and the same points in
        /// the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoPolygon3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="other">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along y =
        /// 50 from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0); two squares in the floor, the second moved 50 along
        /// X and 50 along Y, cross at (100, 50, 0) and (50, 100, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, other, out intersections, tolerance);

        /// <summary>
        /// Gets the points where this polygon and the material of a face cross: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoFace3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => Intersection3.GetIntersections(face, this);

        /// <summary>
        /// Gets the points where this polygon and the material of a face cross, within a tolerance: the same points, in the
        /// same order, as <see cref="Intersection3.GetIntersections(GeoFace3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => Intersection3.GetIntersections(face, this, tolerance);

        /// <summary>
        /// Tries to find where this polygon and the material of a face cross: the same answer, and the same points in the
        /// same order, as <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoPolygon3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(face, this, out intersections);

        /// <summary>
        /// Tries to find where this polygon and the material of a face cross, within a tolerance: the same answer, and the
        /// same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoPolygon3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(face, this, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the edges of this polygon cross a plane: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPlane3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => Intersection3.GetIntersections(plane, this);

        /// <summary>
        /// Gets the points where the edges of this polygon cross a plane, within a tolerance: the same points, in the same
        /// order, as <see cref="Intersection3.GetIntersections(GeoPlane3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => Intersection3.GetIntersections(plane, this, tolerance);

        /// <summary>
        /// Tries to find where the edges of this polygon cross a plane: the same answer, and the same points in the same
        /// order, as <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoPolygon3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(plane, this, out intersections);

        /// <summary>
        /// Tries to find where the edges of this polygon cross a plane, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoPolygon3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(plane, this, out intersections, tolerance);

        /// <summary>
        /// Gets where a ray crosses this polygon, as a list: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoRay3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <remarks>
        /// A ray from (20, 20, 10) pointing straight down crosses the square 100 by 100 in the floor with a corner at the
        /// origin at (20, 20, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Intersection3.GetIntersections(ray, this);

        /// <summary>
        /// Gets where a ray crosses this polygon, as a list, within a tolerance: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoRay3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A ray from (20, 20, 10) pointing straight down crosses the square 100 by 100 in the floor with a corner at the
        /// origin at (20, 20, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Intersection3.GetIntersections(ray, this, tolerance);

        /// <summary>
        /// Gets the points where this polygon and the surface of a solid cross: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoSolid3)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0, 100, 50).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Intersection3.GetIntersections(this, solid);

        /// <summary>
        /// Gets the points where this polygon and the surface of a solid cross, within a tolerance: the same points, in the
        /// same order, as <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0, 100, 50).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Intersection3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find where this polygon and the surface of a solid cross: the same answer, and the same points in the
        /// same order, as <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoSolid3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0, 100, 50).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, solid, out intersections);

        /// <summary>
        /// Tries to find where this polygon and the surface of a solid cross, within a tolerance: the same answer, and the
        /// same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoSolid3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0, 100, 50).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, solid, out intersections, tolerance);

        /// <summary>
        /// Gets the points where this polygon and the surface of an oriented box cross: the same points, in the same order,
        /// as <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoObb3)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets the points where this polygon and the surface of an oriented box cross, within a tolerance: the same
        /// points, in the same order, as <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this polygon and the surface of an oriented box cross: the same answer, and the same points
        /// in the same order, as <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoObb3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this polygon and the surface of an oriented box cross, within a tolerance: the same answer,
        /// and the same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoObb3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets the points where this polygon and the surface of an axis-aligned box cross: the same points, in the same
        /// order, as <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets the points where this polygon and the surface of an axis-aligned box cross, within a tolerance: the same
        /// points, in the same order, as <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this polygon and the surface of an axis-aligned box cross: the same answer, and the same
        /// points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoAabb3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this polygon and the surface of an axis-aligned box cross, within a tolerance: the same
        /// answer, and the same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPolygon3, GeoAabb3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, box, out intersections, tolerance);
    }
}
