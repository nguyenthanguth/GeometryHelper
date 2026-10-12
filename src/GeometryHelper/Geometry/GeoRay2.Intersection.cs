using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a ray crosses another shape.
    /// </summary>
    /// <remarks>
    /// The crossings are those of the boundary, in the order and with the repeats left out that a segment's are. The
    /// ray from (-50, 50) along X crosses the square 100 by 100 with a corner at the origin at (0, 50) and at (100, 50);
    /// the ray from (50, 50) along X starts inside it and crosses only at (100, 50).
    /// </remarks>
    public readonly partial struct GeoRay2
    {
        /// <summary>
        /// Gets the points where this ray meets an arc.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => Intersection2.GetIntersections(this, arc);

        /// <summary>
        /// Gets the points where this ray meets an arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Intersection2.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Gets the points where this ray meets a curved loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => Intersection2.GetIntersections(loop, this);

        /// <summary>
        /// Gets the points where this ray meets a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Intersection2.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets the points where this ray meets a curved chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => Intersection2.GetIntersections(chain, this);

        /// <summary>
        /// Gets the points where this ray meets a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Intersection2.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets every point where this ray crosses the boundary of a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => Face2.GetIntersections(face, this);

        /// <summary>
        /// Gets every point where this ray crosses the boundary of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The rim of every hole is boundary too: over the square 100 by 100 with a corner at the origin and a hole 40 by
        /// 40 in its middle, the ray from (-50, 50) along X crosses at four points: (0, 50) and (100, 50) on the outline,
        /// (30, 50) and (70, 50) on the rim of the hole.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Face2.GetIntersections(face, this, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses a line segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2 intersection) => Intersection2.TryIntersectWith(this, line, out intersection);

        /// <summary>
        /// Tries to find where this ray crosses a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersection">The crossing point if successful, placed on this ray, never behind its origin.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A segment parallel to the ray is refused, as two parallel segments are, even where it lies along the ray.
        /// </remarks>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2 intersection, Tolerance tolerance) => Intersection2.TryIntersectWith(this, line, out intersection, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses another ray, using the default tolerance.
        /// </summary>
        /// <param name="other">The other ray.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        public bool TryIntersectWith(GeoRay2 other, out GeoPoint2 intersection) => Intersection2.TryIntersectWith(this, other, out intersection);

        /// <summary>
        /// Tries to find where this ray crosses another ray, within a tolerance.
        /// </summary>
        /// <param name="other">The other ray.</param>
        /// <param name="intersection">The crossing point if successful, placed on this ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// Two rays parallel by angle are refused, as neither has a length to measure a drift over. The ray from the
        /// origin along X and the one from (50, -50) along Y cross at (50, 0); the one from (50, 50) along Y does not.
        /// </remarks>
        public bool TryIntersectWith(GeoRay2 other, out GeoPoint2 intersection, Tolerance tolerance) => Intersection2.TryIntersectWith(this, other, out intersection, tolerance);

        /// <summary>
        /// Gets all intersection points with a rectangle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Intersection2.GetIntersections(rect, this);

        /// <summary>
        /// Gets all intersection points with a rectangle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Intersection2.GetIntersections(rect, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a polygon using default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly) => Intersection2.GetIntersections(poly, this);

        /// <summary>
        /// Gets all intersection points with a polygon within tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly, Tolerance tolerance) => Intersection2.GetIntersections(poly, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a circle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Intersection2.GetIntersections(circle, this);

        /// <summary>
        /// Gets all intersection points with a circle within tolerance.
        /// </summary>
        /// <remarks>
        /// The ray from (-100, 0) along X crosses the circle of radius 50 round the origin at (-50, 0) and (50, 0); the ray
        /// from the centre crosses it once, at (50, 0).
        /// </remarks>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Intersection2.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a polyline using default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Intersection2.GetIntersections(polyline, this);

        /// <summary>
        /// Gets all intersection points with a polyline within tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections) => Arc2.TryIntersectWith(arc, this, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance) => Arc2.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(circle, this, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(loop, this, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(loop, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(polyline, this, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this ray crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(chain, this, out intersections);

        /// <summary>
        /// Tries to find where this ray crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(chain, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this ray crosses an edge.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where this ray crosses an edge, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A straight edge meets the ray at a single point rather than at a list of them, so that answer is wrapped to
        /// match the curved case, as <see cref="GeoLine2.GetIntersections(GeoEdge2, Tolerance)"/> does.
        /// </remarks>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance)
        {
            if (edge.IsArc)
            {
                return GetIntersections(edge.ToArc(), tolerance);
            }

            return Intersection2.TryIntersectWith(this, edge.ToLine(), out GeoPoint2 meeting, tolerance)
                ? new[] { meeting }
                : new GeoPoint2[0];
        }

        #region Triangles

        /// <summary>
        /// Gets every point where this ray meets the edges of a triangle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoTriangle2 triangle) => Triangle2.GetIntersections(triangle, this);

        /// <summary>
        /// Gets every point where this ray meets the edges of a triangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.GetIntersections(triangle, this, tolerance);

        #endregion

        /// <summary>
        /// Tries to find where this ray meets a face: the same answer, and the same points in the same order, as
        /// <see cref="GeoFace2.TryIntersectWith(GeoRay2, out GeoPoint2[])"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace2 face, out GeoPoint2[] intersections)
            => (face ?? throw new ArgumentNullException(nameof(face))).TryIntersectWith(this, out intersections);

        /// <summary>
        /// Tries to find where this ray meets a face, within a tolerance: the same answer, and the same points in the
        /// same order, as <see cref="GeoFace2.TryIntersectWith(GeoRay2, out GeoPoint2[], Tolerance)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace2 face, out GeoPoint2[] intersections, Tolerance tolerance)
            => (face ?? throw new ArgumentNullException(nameof(face))).TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this ray meets a rectangle: the same answer, and the same points in the same order, as
        /// <see cref="GeoRectangle2.TryIntersectWith(GeoRay2, out GeoPoint2[])"/>.
        /// </summary>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections)
            => rect.TryIntersectWith(this, out intersections);

        /// <summary>
        /// Tries to find where this ray meets a rectangle, within a tolerance: the same answer, and the same points in
        /// the same order, as <see cref="GeoRectangle2.TryIntersectWith(GeoRay2, out GeoPoint2[], Tolerance)"/>.
        /// </summary>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections, Tolerance tolerance)
            => rect.TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this ray meets a triangle: the same answer, and the same points in the same order, as
        /// <see cref="GeoTriangle2.TryIntersectWith(GeoRay2, out GeoPoint2[])"/>.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle2 triangle, out GeoPoint2[] intersections)
            => triangle.TryIntersectWith(this, out intersections);

        /// <summary>
        /// Tries to find where this ray meets a triangle, within a tolerance: the same answer, and the same points in
        /// the same order, as <see cref="GeoTriangle2.TryIntersectWith(GeoRay2, out GeoPoint2[], Tolerance)"/>.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle2 triangle, out GeoPoint2[] intersections, Tolerance tolerance)
            => triangle.TryIntersectWith(this, out intersections, tolerance);
    }
}
