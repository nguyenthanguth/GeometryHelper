using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where an infinite line crosses another shape in space.
    /// </summary>
    /// <remarks>
    /// The crossings are those of the surface or the boundary, in the order and with the repeats left out that a segment's
    /// are. The line through (-50, 50, 10) along X crosses the box 100 by 100 by 20 with a corner at the origin at
    /// (0, 50, 10) and at (100, 50, 10), in that order, and a line through the middle of the box gives the same two whatever
    /// its origin. A flat shape is crossed at one point, or nowhere: a line lying in its plane names no place.
    /// </remarks>
    public readonly partial struct GeoXLine3
    {
        /// <summary>
        /// Gets the points where this infinite line crosses an axis-aligned box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => GetIntersections(box, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses an axis-aligned box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses an oriented box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => GetIntersections(box, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses an oriented box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Intersection3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses the surface of a solid, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => GetIntersections(solid, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Intersection3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Gets the point where this infinite line crosses a plane, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => GetIntersections(plane, Tolerance.Global);

        /// <summary>
        /// Gets the point where this infinite line crosses a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The line through (0, 0, 10) along (1, 0, -1) crosses the floor at (10, 0, 0); one lying in the floor crosses it nowhere
        /// in particular and gives none, and so does one parallel to it above it.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => Intersection3.GetIntersections(this, plane, tolerance);

        /// <summary>
        /// Gets the point where this infinite line crosses a triangle, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => GetIntersections(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the point where this infinite line crosses a triangle, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Intersection3.GetIntersections(this, triangle, tolerance);

        /// <summary>
        /// Gets the point where this infinite line crosses a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => GetIntersections(polygon, Tolerance.Global);

        /// <summary>
        /// Gets the point where this infinite line crosses a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => Intersection3.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Gets the point where this infinite line crosses a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => GetIntersections(face, Tolerance.Global);

        /// <summary>
        /// Gets the point where this infinite line crosses a face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => Intersection3.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Gets the point where this infinite line crosses a line segment, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets the point where this infinite line crosses a line segment, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Intersection3.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets the point where this infinite line crosses a ray, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => GetIntersections(ray, Tolerance.Global);

        /// <summary>
        /// Gets the point where this infinite line crosses a ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// They cross where the shortest segment between them is no longer than the point tolerance; the point is placed on
        /// the ray, never behind its origin, and a ray parallel to the line within the angle tolerance gives none, even
        /// where it lies along the line.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Intersection3.GetIntersections(this, ray, tolerance);

        /// <summary>
        /// Gets the point where this infinite line crosses another infinite line, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 other) => GetIntersections(other, Tolerance.Global);

        /// <summary>
        /// Gets the point where this infinite line crosses another infinite line, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Two lines in space cross where the common perpendicular between them is no longer than the point tolerance, and the
        /// point is the middle of it, the same to the bit whichever of the two asks. The line through the origin along X and
        /// the one through (50, -50, 0) along Y cross at (50, 0, 0); the one through (50, -50, 10) along Y passes over it and
        /// gives none. Two lines parallel within the angle tolerance give none, even where they are the same line.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoXLine3 other, Tolerance tolerance) => Intersection3.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses an arc, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => GetIntersections(arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses an arc, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Arc3.GetIntersections(arc, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a circle, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => GetIntersections(circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses the rim of a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The line through (-100, 0, 0) along X crosses the circle of radius 50 lying in the floor about the origin at
        /// (-50, 0, 0) and (50, 0, 0), and so does the line through (100, 0, 0) along X.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Arc3.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses an ellipse, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEllipse3 ellipse) => GetIntersections(ellipse, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses the rim of an ellipse, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, Tolerance tolerance) => Ellipse3.GetIntersections(ellipse, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses an edge, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses an edge, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance) => edge.GetIntersections(this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline) => GetIntersections(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline, Tolerance tolerance) => Intersection3.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => GetIntersections(chain, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => GetIntersections(loop, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Tries to find the point where this infinite line crosses a plane, using the default tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection) => TryIntersectWith(plane, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find the point where this infinite line crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A line crosses a plane at one point where that point can be placed: at a sine of 1E-9 or less a crossing further
        /// out than a double can place within the point tolerance is refused (the line through (0, 0, 100) along
        /// (1, 0, -1E-11) gives none), and a line lying in the plane, level with it to a trillionth of a radian within the
        /// planar tolerance of it, crosses everywhere and names no point. The line through (5, 5, 0.0004) along
        /// (1, 0, -0.01) crosses the floor at (5.04, 5, 0).
        /// </remarks>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, plane, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this infinite line crosses a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection) => TryIntersectWith(triangle, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find the point where this infinite line crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, triangle, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this infinite line crosses a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3 intersection) => TryIntersectWith(polygon, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find the point where this infinite line crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, polygon, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this infinite line crosses a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3 intersection) => TryIntersectWith(face, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find the point where this infinite line crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, face, out intersection, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a line segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersections">The crossing point when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections) => TryIntersectWith(line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersections">The crossing point when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance) => Intersection3.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing point when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections) => TryIntersectWith(ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing point when the method returns true; empty otherwise, placed on the ray, never behind its origin.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance) => Intersection3.TryIntersectWith(this, ray, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses another infinite line, using the default tolerance.
        /// </summary>
        /// <param name="other">The other line.</param>
        /// <param name="intersections">The crossing point when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoXLine3 other, out GeoPoint3[] intersections) => TryIntersectWith(other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses another infinite line, within a tolerance.
        /// </summary>
        /// <param name="other">The other line.</param>
        /// <param name="intersections">The crossing point when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// Two lines parallel within the angle tolerance give no point, as neither has a length to measure a drift over, even
        /// where they are the same line. The point is the same to the bit whichever of the two asks.
        /// </remarks>
        public bool TryIntersectWith(GeoXLine3 other, out GeoPoint3[] intersections, Tolerance tolerance) => Intersection3.TryIntersectWith(this, other, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses the rim of a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance) => Arc3.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses an ellipse, using the default tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEllipse3 ellipse, out GeoPoint3[] intersections) => TryIntersectWith(ellipse, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses the rim of an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEllipse3 ellipse, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(ellipse, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses an edge, using the default tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections) => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections, Tolerance tolerance) => edge.TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections) => TryIntersectWith(polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections, Tolerance tolerance) => Intersection3.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections) => TryIntersectWith(chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections, Tolerance tolerance) => ArcChain3.TryIntersectWith(chain, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections) => TryIntersectWith(loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections, Tolerance tolerance) => ArcChain3.TryIntersectWith(loop, this, out intersections, tolerance);
    }
}
