using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a plane crosses another shape.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        /// <summary>
        /// Tries to find the line where this plane meets another plane, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 other, out GeoRay3 intersection) => Intersection3.TryIntersectWith(this, other, out intersection);

        /// <summary>
        /// Tries to find the line where this plane meets another plane, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 other, out GeoRay3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, other, out intersection, tolerance);

        /// <summary>
        /// Gets every point where the edges of a solid cross this plane, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => Intersection3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where the edges of a solid cross this plane, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Intersection3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Tries to find the point where a segment crosses this plane, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(line, this, out intersection);

        /// <summary>
        /// Tries to find the point where a segment crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(line, this, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where a ray crosses this plane, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(ray, this, out intersection);

        /// <summary>
        /// Tries to find the point where a ray crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(ray, this, out intersection, tolerance);
        /// <summary>
        /// Gets every point where an arc crosses this plane, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => Arc3.GetIntersections(arc, this);

        /// <summary>
        /// Gets every point where an arc crosses this plane, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Arc3.GetIntersections(arc, this, tolerance);

        /// <summary>
        /// Tries to find where an arc crosses this plane, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(arc, this, out intersections);

        /// <summary>
        /// Tries to find where an arc crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses this plane, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => Arc3.GetIntersections(circle, this);

        /// <summary>
        /// Gets every point where a circle crosses this plane, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Arc3.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses this plane, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => Arc3.TryIntersectWith(circle, this, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a segment crosses this plane.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets every point where a segment crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The same points as <see cref="GeoLine3.GetIntersections(GeoPlane3, Tolerance)"/>, the point of a touch among them.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => line.GetIntersections(this, tolerance);

        /// <summary>
        /// Gets every point where an edge crosses this plane.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where an edge crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);

        /// <summary>
        /// Gets every point where a curved chain crosses this plane.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => ArcChain3.GetIntersections(chain, this);

        /// <summary>
        /// Gets every point where a curved chain crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets every point where a curved loop crosses this plane.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => ArcChain3.GetIntersections(loop, this);

        /// <summary>
        /// Gets every point where a curved loop crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets every point where this plane crosses a straight chain: the same points, in the same order, as
        /// <see cref="GeoPolyline3.GetIntersections(GeoPlane3)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline) => Intersection3.GetIntersections(polyline, this);

        /// <summary>
        /// Gets every point where this plane crosses a straight chain, within a tolerance: the same points, in the same
        /// order, as <see cref="GeoPolyline3.GetIntersections(GeoPlane3, Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline, Tolerance tolerance)
            => Intersection3.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Tries to find where this plane crosses a straight chain: the same answer, and the same points in the same
        /// order, as <see cref="GeoPolyline3.TryIntersectWith(GeoPlane3, out GeoPoint3[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(polyline, this, out intersections);

        /// <summary>
        /// Tries to find where this plane crosses a straight chain, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="GeoPolyline3.TryIntersectWith(GeoPlane3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the edges of a triangle cross this plane: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPlane3, GeoTriangle3)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing across it
        /// cross at (50, 50, 0) and (0, 50, 0); a triangle lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => Intersection3.GetIntersections(this, triangle);

        /// <summary>
        /// Gets the points where the edges of a triangle cross this plane, within a tolerance: the same points, in the same
        /// order, as <see cref="Intersection3.GetIntersections(GeoPlane3, GeoTriangle3, Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing across it
        /// cross at (50, 50, 0) and (0, 50, 0); a triangle lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Intersection3.GetIntersections(this, triangle, tolerance);

        /// <summary>
        /// Tries to find where the edges of a triangle cross this plane: the same answer, and the same points in the same
        /// order, as <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoTriangle3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing across it
        /// cross at (50, 50, 0) and (0, 50, 0); a triangle lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, triangle, out intersections);

        /// <summary>
        /// Tries to find where the edges of a triangle cross this plane, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoTriangle3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing across it
        /// cross at (50, 50, 0) and (0, 50, 0); a triangle lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, triangle, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the edges of a polygon cross this plane: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPlane3, GeoPolygon3)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => Intersection3.GetIntersections(this, polygon);

        /// <summary>
        /// Gets the points where the edges of a polygon cross this plane, within a tolerance: the same points, in the same
        /// order, as <see cref="Intersection3.GetIntersections(GeoPlane3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => Intersection3.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Tries to find where the edges of a polygon cross this plane: the same answer, and the same points in the same
        /// order, as <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoPolygon3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => Intersection3.TryIntersectWith(this, polygon, out intersections);

        /// <summary>
        /// Tries to find where the edges of a polygon cross this plane, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoPolygon3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at (100, 50, 0)
        /// and (0, 50, 0); a square lying in the floor touches it and crosses it nowhere.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, polygon, out intersections, tolerance);

        /// <summary>
        /// Gets the points where the edges of a face cross this plane: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPlane3, GeoFace3)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => Intersection3.GetIntersections(this, face);

        /// <summary>
        /// Gets the points where the edges of a face cross this plane, within a tolerance: the same points, in the same
        /// order, as <see cref="Intersection3.GetIntersections(GeoPlane3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => Intersection3.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Tries to find where the edges of a face cross this plane: the same answer, and the same points in the same
        /// order, as <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoFace3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(this, face, out intersections);

        /// <summary>
        /// Tries to find where the edges of a face cross this plane, within a tolerance: the same answer, and the same
        /// points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoFace3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50 from z
        /// = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on the rim
        /// of the hole.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, face, out intersections, tolerance);

        /// <summary>
        /// Gets where a ray crosses this plane, as a list: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoRay3, GeoPlane3)"/>.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <remarks>
        /// A ray from (0, 0, 10) pointing straight down crosses the floor plane at the origin; one lying in the floor
        /// touches it everywhere and crosses it nowhere.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Intersection3.GetIntersections(ray, this);

        /// <summary>
        /// Gets where a ray crosses this plane, as a list, within a tolerance: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoRay3, GeoPlane3, Tolerance)"/>.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A ray from (0, 0, 10) pointing straight down crosses the floor plane at the origin; one lying in the floor
        /// touches it everywhere and crosses it nowhere.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Intersection3.GetIntersections(ray, this, tolerance);

        /// <summary>
        /// Gets the points where this plane cuts the edges of an oriented box: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPlane3, GeoObb3)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets the points where this plane cuts the edges of an oriented box, within a tolerance: the same points, in the
        /// same order, as <see cref="Intersection3.GetIntersections(GeoPlane3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this plane cuts the edges of an oriented box: the same answer, and the same points in the
        /// same order, as <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoObb3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this plane cuts the edges of an oriented box, within a tolerance: the same answer, and the
        /// same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoObb3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, box, out intersections, tolerance);

        /// <summary>
        /// Gets the points where this plane cuts the edges of an axis-aligned box: the same points, in the same order, as
        /// <see cref="Intersection3.GetIntersections(GeoPlane3, GeoAabb3)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => Intersection3.GetIntersections(this, box);

        /// <summary>
        /// Gets the points where this plane cuts the edges of an axis-aligned box, within a tolerance: the same points, in
        /// the same order, as <see cref="Intersection3.GetIntersections(GeoPlane3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Tries to find where this plane cuts the edges of an axis-aligned box: the same answer, and the same points in
        /// the same order, as <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoAabb3, out GeoPoint3[])"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections) => Intersection3.TryIntersectWith(this, box, out intersections);

        /// <summary>
        /// Tries to find where this plane cuts the edges of an axis-aligned box, within a tolerance: the same answer, and
        /// the same points in the same order, as
        /// <see cref="Intersection3.TryIntersectWith(GeoPlane3, GeoAabb3, out GeoPoint3[], Tolerance)"/>.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50; the
        /// plane z = 101 misses it.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when the box is empty.</exception>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => Intersection3.TryIntersectWith(this, box, out intersections, tolerance);
    }
}
