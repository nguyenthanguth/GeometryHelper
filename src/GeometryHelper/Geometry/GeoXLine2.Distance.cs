using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an infinite line is from another shape.
    /// </summary>
    /// <remarks>
    /// A closed shape is read as filled, as <see cref="GeometryHelper.Core.Distance2"/> reads it: a line through a
    /// polygon is nought from it. The line through (-50, 150) along X passes 50 above the square 100 by 100 with a
    /// corner at the origin.
    /// </remarks>
    public readonly partial struct GeoXLine2
    {
        /// <summary>
        /// Calculates the shortest distance from this infinite line to a point, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint2 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a point, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// From the line through the origin along X, the points (50, 40) and (-30, 40) are both 40 away: there is no
        /// start to stop the foot at.
        /// </remarks>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a line segment, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine2 line) => DistanceTo(line, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a line segment, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Distance2.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a ray, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay2 ray) => DistanceTo(ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a ray, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The line through the origin along X and the ray from (50, 30) along Y are 30 apart, the origin of the ray
        /// standing over the line; the ray from (50, 30) along -Y crosses the line at (50, 0) and is nought from it.
        /// </remarks>
        public double DistanceTo(GeoRay2 ray, Tolerance tolerance) => Distance2.DistanceTo(this, ray, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to another infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine2 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to another infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// Nought where the two cross, at any angle; side by side, the gap between them: the line through the origin
        /// along X and the one through (0, 30) along -X are 30 apart.
        /// </remarks>
        public double DistanceTo(GeoXLine2 other, Tolerance tolerance) => Distance2.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an arc, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc2 arc) => DistanceTo(arc, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an arc, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Arc2.DistanceTo(arc, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a circle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoCircle2 circle) => DistanceTo(circle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a circle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance) => Distance2.DistanceTo(circle, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an ellipse, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEllipse2 ellipse) => DistanceTo(ellipse, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an ellipse, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEllipse2 ellipse, Tolerance tolerance) => Ellipse2.DistanceTo(ellipse, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an edge, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEdge2 edge) => DistanceTo(edge, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an edge, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance) => edge.DistanceTo(this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline) => DistanceTo(polyline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a polyline, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline, Tolerance tolerance) => Distance2.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a polygon, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 poly) => DistanceTo(poly, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a polygon, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The line through (-50, 150) along X passes 50 above the square 100 by 100 with a corner at the origin, and
        /// so does the one through (-50, 150) along -X; the line through (-500, 50) along X runs into the square and is
        /// nought from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 poly, Tolerance tolerance) => Distance2.DistanceTo(poly, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain) => DistanceTo(chain, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a curved chain, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(chain, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop) => DistanceTo(loop, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a curved loop, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(loop, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a rectangle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRectangle2 rect) => DistanceTo(rect, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a rectangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Distance2.DistanceTo(rect, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a triangle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle2 triangle) => DistanceTo(triangle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a triangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.DistanceTo(triangle, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a face, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face) => DistanceTo(face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a face, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face, Tolerance tolerance) => Face2.DistanceTo(face, this, tolerance);
    }
}
