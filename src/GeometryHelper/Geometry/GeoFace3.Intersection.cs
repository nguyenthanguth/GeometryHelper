using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a face crosses another shape.
    /// </summary>
    public sealed partial class GeoFace3
    {
        /// <summary>
        /// Tries to find the point where a line segment crosses this face, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(line, this, out intersection);

        /// <summary>
        /// Tries to find the point where a line segment crosses this face, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(line, this, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where a ray crosses this face, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(ray, this, out intersection);

        /// <summary>
        /// Tries to find the point where a ray crosses this face, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(ray, this, out intersection, tolerance);
        /// <summary>
        /// Gets every point where an arc crosses this face, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => Arc3.GetIntersections(arc, this);

        /// <summary>
        /// Gets every point where an arc crosses this face, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Arc3.GetIntersections(arc, this, tolerance);

        /// <summary>
        /// Tries to find where an arc crosses this face, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(arc, this, out intersections);

        /// <summary>
        /// Tries to find where an arc crosses this face, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses this face, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => Arc3.GetIntersections(circle, this);

        /// <summary>
        /// Gets every point where a circle crosses this face, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Arc3.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses this face, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(circle, this, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses this face, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a segment crosses this face.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets every point where a segment crosses this face, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance)
        {
            return Intersection3.TryIntersectWith(line, this, out GeoPoint3 crossing, tolerance)
                ? new[] { crossing }
                : new GeoPoint3[0];
        }

        /// <summary>
        /// Gets every point where an edge crosses this face.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where an edge crosses this face, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);

        /// <summary>
        /// Gets every point where a curved chain crosses this face.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => ArcChain3.GetIntersections(chain, this);

        /// <summary>
        /// Gets every point where a curved chain crosses this face, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets every point where a curved loop crosses this face.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => ArcChain3.GetIntersections(loop, this);

        /// <summary>
        /// Gets every point where a curved loop crosses this face, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets every point where this face crosses a straight chain: the same points, in the same order, as
        /// <see cref="GeoPolyline3.GetIntersections(GeoFace3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline) => Intersection3.GetIntersections(polyline, this);

        /// <summary>
        /// Gets every point where this face crosses a straight chain, within a tolerance: the same points, in the same
        /// order, as <see cref="GeoPolyline3.GetIntersections(GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline, Tolerance tolerance)
            => Intersection3.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Tries to find where this face crosses a straight chain: the same answer, and the same points in the same
        /// order, as <see cref="GeoPolyline3.TryIntersectWith(GeoFace3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(polyline, this, out intersections);

        /// <summary>
        /// Tries to find where this face crosses a straight chain, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="GeoPolyline3.TryIntersectWith(GeoFace3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the material of this face and a triangle cross: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoTriangle3, GeoFace3)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle and the triangle with corners (40, 50,
        /// -10), (60, 50, -10) and (50, 50, 10), standing in the hole, do not meet; moved 30 along X, onto the material,
        /// they do.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => Intersection3.GetIntersections(triangle, this);

        /// <summary>
        /// Gets the points where the material of this face and a triangle cross, within a tolerance: the same points, in
        /// the same order, as <see cref="Intersection3.GetIntersections(GeoTriangle3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle and the triangle with corners (40, 50,
        /// -10), (60, 50, -10) and (50, 50, 10), standing in the hole, do not meet; moved 30 along X, onto the material,
        /// they do.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Intersection3.GetIntersections(triangle, this, tolerance);

        /// <summary>
        /// Tries to find where the material of this face and a triangle cross: the same answer, and the same points in the
        /// same order, as <see cref="Intersection3.TryIntersectWith(GeoTriangle3, GeoFace3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle and the triangle with corners (40, 50,
        /// -10), (60, 50, -10) and (50, 50, 10), standing in the hole, do not meet; moved 30 along X, onto the material,
        /// they do.
        /// </remarks>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(triangle, this, out intersections);

        /// <summary>
        /// Tries to find where the material of this face and a triangle cross, within a tolerance: the same answer, and the
        /// same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoTriangle3, GeoFace3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle and the triangle with corners (40, 50,
        /// -10), (60, 50, -10) and (50, 50, 10), standing in the hole, do not meet; moved 30 along X, onto the material,
        /// they do.
        /// </remarks>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(triangle, this, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the material of this face and a polygon cross: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoFace3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => Intersection3.GetIntersections(this, polygon);

        /// <summary>
        /// Gets the points where the material of this face and a polygon cross, within a tolerance: the same points, in the
        /// same order, as <see cref="Intersection3.GetIntersections(GeoFace3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => Intersection3.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Tries to find where the material of this face and a polygon cross: the same answer, and the same points in the
        /// same order, as <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoPolygon3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, polygon, out intersections);

        /// <summary>
        /// Tries to find where the material of this face and a polygon cross, within a tolerance: the same answer, and the
        /// same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoPolygon3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0), (70, 50, 0)
        /// and (30, 50, 0).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, polygon, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the materials of this face and another cross: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoFace3, GeoFace3)"/>.
        /// </summary>
        /// <param name="other">The face, read by its material.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by a square 20 by 20 about
        /// (50, 50) lying in its hole, and is by one about (15, 50); a face standing wholly in the hole crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 other) => Intersection3.GetIntersections(this, other);

        /// <summary>
        /// Gets the points where the materials of this face and another cross, within a tolerance: the same points, in the
        /// same order, as <see cref="Intersection3.GetIntersections(GeoFace3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="other">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by a square 20 by 20 about
        /// (50, 50) lying in its hole, and is by one about (15, 50); a face standing wholly in the hole crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 other, Tolerance tolerance) => Intersection3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where the materials of this face and another cross: the same answer, and the same points in the
        /// same order, as <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoFace3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="other">The face, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by a square 20 by 20 about
        /// (50, 50) lying in its hole, and is by one about (15, 50); a face standing wholly in the hole crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other face is null.</exception>
        public bool TryIntersectWith(GeoFace3 other, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where the materials of this face and another cross, within a tolerance: the same answer, and the
        /// same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoFace3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="other">The face, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by a square 20 by 20 about
        /// (50, 50) lying in its hole, and is by one about (15, 50); a face standing wholly in the hole crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the other face is null.</exception>
        public bool TryIntersectWith(GeoFace3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, other, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the edges of this face cross a plane: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPlane3, GeoFace3)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => Intersection3.GetIntersections(plane, this);

        /// <summary>
        /// Gets the points where the edges of this face cross a plane, within a tolerance: the same points, in the same
        /// order, as <see cref="Intersection3.GetIntersections(GeoPlane3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => Intersection3.GetIntersections(plane, this, tolerance);

        /// <summary>
        /// Tries to find where the edges of this face cross a plane: the same answer, and the same points in the same
        /// order, as <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoFace3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(plane, this, out intersections);

        /// <summary>
        /// Tries to find where the edges of this face cross a plane, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoFace3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(plane, this, out intersections, tolerance);

        /// <summary>
        /// Gets where a ray crosses the material of this face, as a list: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoRay3, GeoFace3)"/>.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <remarks>
        /// A ray from (50, 50, 10) pointing straight down passes through the hole 40 by 40 in the middle of the square 100
        /// by 100 in the floor and crosses nothing; from (20, 20, 10) it crosses the material at (20, 20, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Intersection3.GetIntersections(ray, this);

        /// <summary>
        /// Gets where a ray crosses the material of this face, as a list, within a tolerance: the same points, in the same
        /// order, as <see cref="Intersection3.GetIntersections(GeoRay3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A ray from (50, 50, 10) pointing straight down passes through the hole 40 by 40 in the middle of the square 100
        /// by 100 in the floor and crosses nothing; from (20, 20, 10) it crosses the material at (20, 20, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Intersection3.GetIntersections(ray, this, tolerance);

        /// <summary>
        /// Gets the points where the material of this face and the surface of a solid cross: the same points, in the same
        /// order, as <see cref="Intersection3.GetIntersections(GeoFace3, GeoSolid3)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Intersection3.GetIntersections(this, solid);

        /// <summary>
        /// Gets the points where the material of this face and the surface of a solid cross, within a tolerance: the same
        /// points, in the same order, as <see cref="Intersection3.GetIntersections(GeoFace3, GeoSolid3, Tolerance)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Intersection3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find where the material of this face and the surface of a solid cross: the same answer, and the same
        /// points in the same order, as <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoSolid3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, solid, out intersections);

        /// <summary>
        /// Tries to find where the material of this face and the surface of a solid cross, within a tolerance: the same
        /// answer, and the same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoSolid3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, solid, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the material of this face and the surface of an oriented box cross: the same points, in
        /// the same order, as <see cref="Intersection3.GetIntersections(GeoFace3, GeoObb3)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets the points where the material of this face and the surface of an oriented box cross, within a tolerance:
        /// the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoFace3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where the material of this face and the surface of an oriented box cross: the same answer, and the
        /// same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoObb3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where the material of this face and the surface of an oriented box cross, within a tolerance: the
        /// same answer, and the same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoObb3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the material of this face and the surface of an axis-aligned box cross: the same points,
        /// in the same order, as <see cref="Intersection3.GetIntersections(GeoFace3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets the points where the material of this face and the surface of an axis-aligned box cross, within a
        /// tolerance: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoFace3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where the material of this face and the surface of an axis-aligned box cross: the same answer, and
        /// the same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoAabb3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where the material of this face and the surface of an axis-aligned box cross, within a tolerance:
        /// the same answer, and the same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoFace3, GeoAabb3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing and touches nothing; without the hole it crosses
        /// the box at four points at height 50.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, box, out intersections, tolerance);
    }
}
