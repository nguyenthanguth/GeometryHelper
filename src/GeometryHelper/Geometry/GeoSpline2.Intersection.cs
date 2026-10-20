using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a spline crosses another shape in the plane.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The crossings are those of the boundary of a closed shape, in order along the spline from its start. A place is where the two
    /// meet (a crossing, or a touch found to rounding), or where the distance of the spline from the shape is least within the point
    /// tolerance, as a touch a half of a thousandth off is, or an end of the spline or of the shape comes near the other: a stationary
    /// point that is a greatest, as lies between two crossings of a line that passes a hair inside a circle, is none, and nor is a point
    /// on a slope. Places no further apart than the point tolerance, one from the next, are one place; so are near touches (not
    /// crossings) next to each other in one basin of the distance within ten times it, however many the rounding of the numbers
    /// leaves where a curve touches another. Two crossings are never one place unless they are within the point tolerance of
    /// each other, as for the exact curves: a line that cuts a circle of radius 30 a million from the origin in a chord of 0.009
    /// meets it twice. The spline of a circle of radius 100 about the origin, from <see cref="FromCircle(GeoCircle2)"/>, crosses
    /// the line along X through the origin at (100, 0) and (-100, 0), in that order, and at (100, 0) again where it ends, which is
    /// the same place as its start and is not given twice, and a line tangent to a circle touches it once (a million from the
    /// origin, where the numbers are 1.2E-10 apart, up to three places within a hundredth of a millimetre).
    /// </para>
    /// <para>
    /// Where the spline lies along the shape for a length, to rounding, no place is given along the stretch. Against an ellipse or
    /// another spline, the same is so where the two stay within the point tolerance of each other, level, for a quarter of the size
    /// of the smaller; see <see cref="DistanceTo(GeoSpline2)"/>.
    /// </para>
    /// </remarks>
    public sealed partial class GeoSpline2
    {
        /// <summary>
        /// Gets the points where this spline crosses a line segment, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoLine2, Tolerance)"/>.</returns>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, line), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses a line segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections) => TryIntersectWith(line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses a ray, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoRay2, Tolerance)"/>.</returns>
        public GeoPoint2[] GetIntersections(GeoRay2 ray) => GetIntersections(ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint2[] GetIntersections(GeoRay2 ray, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, ray), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRay2 ray, out GeoPoint2[] intersections) => TryIntersectWith(ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay2 ray, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ray, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoXLine2, Tolerance)"/>.</returns>
        public GeoPoint2[] GetIntersections(GeoXLine2 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint2[] GetIntersections(GeoXLine2 xline, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, xline), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoXLine2 xline, out GeoPoint2[] intersections) => TryIntersectWith(xline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoXLine2 xline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(xline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses a polyline, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoPolyline2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => GetIntersections(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, polyline), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections) => TryIntersectWith(polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoPolygon2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon) => GetIntersections(polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, polygon), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon2 polygon, out GeoPoint2[] intersections) => TryIntersectWith(polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon2 polygon, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a rectangle, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoRectangle2, Tolerance)"/>.</returns>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => GetIntersections(rect, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a rectangle, within a tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, rect), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a rectangle, using the default tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections) => TryIntersectWith(rect, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a rectangle, within a tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(rect, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a triangle, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoTriangle2, Tolerance)"/>.</returns>
        public GeoPoint2[] GetIntersections(GeoTriangle2 triangle) => GetIntersections(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint2[] GetIntersections(GeoTriangle2 triangle, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, triangle), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoTriangle2 triangle, out GeoPoint2[] intersections) => TryIntersectWith(triangle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoTriangle2 triangle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(triangle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a face, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoFace2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => GetIntersections(face, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, face), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace2 face, out GeoPoint2[] intersections) => TryIntersectWith(face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace2 face, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(face, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses an arc, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoArc2, Tolerance)"/>.</returns>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => GetIntersections(arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, arc), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections) => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a circle, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoCircle2, Tolerance)"/>.</returns>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => GetIntersections(circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, circle), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections) => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the boundary of an ellipse, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoEllipse2, Tolerance)"/>.</returns>
        public GeoPoint2[] GetIntersections(GeoEllipse2 ellipse) => GetIntersections(ellipse, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the boundary of an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, ellipse), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of an ellipse, using the default tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEllipse2 ellipse, out GeoPoint2[] intersections) => TryIntersectWith(ellipse, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEllipse2 ellipse, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses an edge, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoEdge2, Tolerance)"/>.</returns>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, edge), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses an edge, using the default tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections) => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(edge, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoPolylineArc2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => GetIntersections(chain, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, chain), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections) => TryIntersectWith(chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoPolygonArc2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => GetIntersections(loop, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the boundary of a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Spline2.GetIntersections(this, Spline2.Of(this, loop), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections) => TryIntersectWith(loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the boundary of a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, tolerance);

            return intersections.Length > 0;
        }
    }
}
