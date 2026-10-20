using GeometryHelper.Core;
using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a spline is from a point, and whether a point lies on it, and how far it is from every other shape in the plane.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A shape is read as the other curves read it: a closed one, a polygon, a rectangle, a triangle, a face, a circle, an ellipse or a
    /// curved loop, is a region, and a spline inside it is nought from it; the spline itself is a curve, with no inside. The distance
    /// is nought only where the two truly meet, a crossing or a touch found to rounding, and otherwise the gap, measured exactly:
    /// it is never read through the tolerance. The pairs with a segment, a ray, a line without ends, an arc or a circle are solved
    /// as polynomials, to rounding; with an ellipse or another spline the nearest approach is found by cutting both in halves while
    /// their hulls are near enough to matter, and bringing the nearest pair to rounding by Newton's method. The parts are taken
    /// nearest bound first, and the cutting ends when the nearest bound left is no nearer than the best found less a hair, the
    /// hair being a billionth of the size of the shapes (0.001 at a size of a million) and never more than a thousandth of the
    /// distance itself, so a distance of 0.0009 at a million is never read above the point tolerance; the answer is that of a
    /// real pair of points, no nearer than the truth and within the hair of it, and to rounding wherever Newton's method settles,
    /// which is wherever the curves cross or the nearest points are not parallel.
    /// </para>
    /// </remarks>
    public sealed partial class GeoSpline2
    {
        /// <summary>
        /// Gets the distance from this spline to a point, using the default tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// The distance from the point to the nearest point of the curve, which is a line and not a region: a point inside a
        /// closed spline is as far from it as from the nearest of its points. Nought only where the point truly lies on it,
        /// otherwise the gap, measured exactly; exact to rounding, however the curve bends.
        /// </returns>
        public double DistanceTo(GeoPoint2 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this spline to a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>
        /// The distance from the point to the nearest point of the curve, measured exactly. The circle of radius 100 about the
        /// origin, as <see cref="FromCircle(GeoCircle2)"/> makes it, is 60 from (160, 0) and 100 from its own centre.
        /// </returns>
        /// <remarks>
        /// The nearest point is found as <see cref="GetKnotAtPoint(GeoPoint2)"/> finds it: on each span the parameters
        /// where the curve stops coming nearer, all of them, from the roots of a polynomial in Bernstein form, and the ends;
        /// whichever span cannot be nearer than the best found, by the box of its control points, is not solved. The boxes are
        /// kept in a tree, built once, so the spans near the point are reached without looking at the rest: a query costs time in
        /// the logarithm of the number of spans and the number whose boxes are near the point, about 10 microseconds on a spiral of
        /// 100 000 spans where it was 0.5 ms before the tree, and a span solved costs about a microsecond. A spline of up to 24 spans
        /// looks at its boxes one by one, which is faster for so few.
        /// </remarks>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance)
        {
            _curve.ClosestParameter(new[] { point.X, point.Y }, out double distance);

            return distance;
        }

        /// <summary>
        /// Determines whether a point lies on the spline, within the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on the spline, within a tolerance: within the point tolerance of its nearest point.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => DistanceTo(point, tolerance) <= tolerance.EqualPoint;

        #region Against the other shapes in the plane

        /// <summary>
        /// Calculates the shortest distance from this spline to a line segment, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine2 line) => DistanceTo(line, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, line));

        /// <summary>
        /// Calculates the shortest distance from this spline to a ray, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay2 ray) => DistanceTo(ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay2 ray, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, ray));

        /// <summary>
        /// Calculates the shortest distance from this spline to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine2 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine2 xline, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, xline));

        /// <summary>
        /// Calculates the shortest distance from this spline to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline) => DistanceTo(polyline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, polyline));

        /// <summary>
        /// Calculates the shortest distance from this spline to a polygon, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 polygon) => DistanceTo(polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A polygon is a region here, as every curve reads it: a spline lying inside it is nought from it, though it crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 polygon, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, polygon));

        /// <summary>
        /// Calculates the shortest distance from this spline to a rectangle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRectangle2 rect) => DistanceTo(rect, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a rectangle, within a tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A rectangle is a region here, as every curve reads it: a spline lying inside it is nought from it, though it crosses nothing.
        /// </remarks>
        public double DistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, rect));

        /// <summary>
        /// Calculates the shortest distance from this spline to a triangle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle2 triangle) => DistanceTo(triangle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A triangle is a region here, as every curve reads it: a spline lying inside it is nought from it, though it crosses nothing.
        /// </remarks>
        public double DistanceTo(GeoTriangle2 triangle, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, triangle));

        /// <summary>
        /// Calculates the shortest distance from this spline to a face, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face) => DistanceTo(face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A face is a region here, as every curve reads it: a spline lying inside it is nought from it, though it crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, face));

        /// <summary>
        /// Calculates the shortest distance from this spline to an arc, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc2 arc) => DistanceTo(arc, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, arc));

        /// <summary>
        /// Calculates the shortest distance from this spline to a circle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoCircle2 circle) => DistanceTo(circle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A circle is a region here, as every curve reads it: a spline lying inside it is nought from it, though it crosses nothing.
        /// </remarks>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, circle));

        /// <summary>
        /// Calculates the shortest distance from this spline to an ellipse, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEllipse2 ellipse) => DistanceTo(ellipse, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// An ellipse is a region here, as every curve reads it: a spline lying inside it is nought from it, though it crosses nothing.
        /// </remarks>
        public double DistanceTo(GeoEllipse2 ellipse, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, ellipse));

        /// <summary>
        /// Calculates the shortest distance from this spline to an edge, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEdge2 edge) => DistanceTo(edge, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, edge));

        /// <summary>
        /// Calculates the shortest distance from this spline to a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain) => DistanceTo(chain, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, chain));

        /// <summary>
        /// Calculates the shortest distance from this spline to a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop) => DistanceTo(loop, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A curved loop is a region here, as every curve reads it: a spline lying inside it is nought from it, though it crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Spline2.DistanceTo(this, Spline2.Of(this, loop));

        #endregion
    }
}
