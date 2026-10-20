using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a spline nearest a point, where on the spline it lies, and the shortest segment joining the two; and the shortest
    /// segment from a spline to every other shape in the plane.
    /// </summary>
    /// <remarks>
    /// The segment runs from the nearest point of the spline to the nearest point of the boundary of the shape, so it has no length
    /// where the two meet, and where a spline lies inside a closed shape, which is nought from it, it is as long as the gap out to
    /// the outline, as it is for the other curves. The shape asked the same question gives the same segment turned round, to the bit.
    /// </remarks>
    public sealed partial class GeoSpline2
    {
        /// <summary>
        /// Gets the value of the knots at the point of the spline nearest a point.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>
        /// A value of the domain, from <see cref="DomainStart"/> to <see cref="DomainEnd"/>: the knot value, which
        /// <see cref="GetPointAtKnot(double)"/> turns back into the point. Where two points of the spline are equally near,
        /// the lower one.
        /// </returns>
        /// <remarks>
        /// The nearest point is a point where the spline stops coming nearer, or an end. On each span that is a root of a
        /// polynomial in Bernstein form, and all of them are found: a point inside a loop of the curve, a point on the axis of
        /// a symmetric one, the centre of an arc, a point at a cusp, all give the nearest of them, to rounding. A circle
        /// as <see cref="FromCircle(GeoCircle2)"/> makes it has every point equally near its centre, and answers
        /// the start.
        /// </remarks>
        public double GetKnotAtPoint(GeoPoint2 point) => _curve.ClosestParameter(new[] { point.X, point.Y }, out _);

        /// <summary>
        /// Gets the normalised parameter, from 0 to 1, of the point of the spline nearest a point, using the default tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>The share of the domain: 0 at the start and 1 at the end; <see cref="GetKnotAtPoint(GeoPoint2)"/> gives the knot value.</returns>
        public double GetParameterAtPoint(GeoPoint2 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the normalised parameter, from 0 to 1, of the point of the spline nearest a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>The share of the domain: 0 at the start and 1 at the end.</returns>
        public double GetParameterAtPoint(GeoPoint2 point, Tolerance tolerance) => _curve.ParameterAtKnot(GetKnotAtPoint(point));

        /// <summary>
        /// Gets how far along the spline the point nearest a point lies, using the default tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>The length from the start of the spline to its nearest point.</returns>
        public double GetDistanceAtPoint(GeoPoint2 point) => GetDistanceAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets how far along the spline the point nearest a point lies, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>The length from the start of the spline to its nearest point.</returns>
        public double GetDistanceAtPoint(GeoPoint2 point, Tolerance tolerance) => _curve.LengthTo(GetKnotAtPoint(point));

        /// <summary>
        /// Gets the point of this spline nearest a point.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>The spline's point at <see cref="GetKnotAtPoint(GeoPoint2)"/>. A spline is a curve with no inside, so a point within a closed one is not its own nearest point.</returns>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => GetPointAtKnot(GetKnotAtPoint(point));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a point, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline's nearest point to the point; as long as <see cref="DistanceTo(GeoPoint2)"/>, to rounding.</returns>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point, Tolerance tolerance) => new GeoLine2(GetClosestPointOnBoundary(point), point);

        #region Against the other shapes in the plane

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a line segment, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a line segment; see <see cref="GetShortestLineTo(GeoLine2, Tolerance)"/>.</returns>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => GetShortestLineTo(line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of line segment nearest it, as long as <see cref="DistanceTo(GeoLine2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, line));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a ray, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a ray; see <see cref="GetShortestLineTo(GeoRay2, Tolerance)"/>.</returns>
        public GeoLine2 GetShortestLineTo(GeoRay2 ray) => GetShortestLineTo(ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of ray nearest it, as long as <see cref="DistanceTo(GeoRay2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        public GeoLine2 GetShortestLineTo(GeoRay2 ray, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, ray));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an infinite line; see <see cref="GetShortestLineTo(GeoXLine2, Tolerance)"/>.</returns>
        public GeoLine2 GetShortestLineTo(GeoXLine2 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of infinite line nearest it, as long as <see cref="DistanceTo(GeoXLine2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        public GeoLine2 GetShortestLineTo(GeoXLine2 xline, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, xline));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a polyline, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a polyline; see <see cref="GetShortestLineTo(GeoPolyline2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => GetShortestLineTo(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of polyline nearest it, as long as <see cref="DistanceTo(GeoPolyline2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, polyline));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a polygon, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a polygon; see <see cref="GetShortestLineTo(GeoPolygon2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon) => GetShortestLineTo(polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of polygon nearest it, as long as <see cref="DistanceTo(GeoPolygon2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, polygon));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a rectangle, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a rectangle; see <see cref="GetShortestLineTo(GeoRectangle2, Tolerance)"/>.</returns>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => GetShortestLineTo(rect, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a rectangle, within a tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of rectangle nearest it, as long as <see cref="DistanceTo(GeoRectangle2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, rect));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a triangle, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a triangle; see <see cref="GetShortestLineTo(GeoTriangle2, Tolerance)"/>.</returns>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle) => GetShortestLineTo(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of triangle nearest it, as long as <see cref="DistanceTo(GeoTriangle2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, triangle));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a face, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a face; see <see cref="GetShortestLineTo(GeoFace2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => GetShortestLineTo(face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of face nearest it, as long as <see cref="DistanceTo(GeoFace2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, face));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an arc, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an arc; see <see cref="GetShortestLineTo(GeoArc2, Tolerance)"/>.</returns>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => GetShortestLineTo(arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of arc nearest it, as long as <see cref="DistanceTo(GeoArc2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, arc));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a circle, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a circle; see <see cref="GetShortestLineTo(GeoCircle2, Tolerance)"/>.</returns>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => GetShortestLineTo(circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of circle nearest it, as long as <see cref="DistanceTo(GeoCircle2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, circle));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an ellipse, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an ellipse; see <see cref="GetShortestLineTo(GeoEllipse2, Tolerance)"/>.</returns>
        public GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse) => GetShortestLineTo(ellipse, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of ellipse nearest it, as long as <see cref="DistanceTo(GeoEllipse2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        public GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, ellipse));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an edge, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an edge; see <see cref="GetShortestLineTo(GeoEdge2, Tolerance)"/>.</returns>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of edge nearest it, as long as <see cref="DistanceTo(GeoEdge2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, edge));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a curved chain; see <see cref="GetShortestLineTo(GeoPolylineArc2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => GetShortestLineTo(chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of curved chain nearest it, as long as <see cref="DistanceTo(GeoPolylineArc2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, chain));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a curved loop; see <see cref="GetShortestLineTo(GeoPolygonArc2, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => GetShortestLineTo(loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of curved loop nearest it, as long as <see cref="DistanceTo(GeoPolygonArc2, Tolerance)"/>
        /// but where a closed shape holds the spline, which it reads as the gap out to the boundary; of no length where the two cross.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Spline2.GetShortestLineTo(this, Spline2.Of(this, loop));

        #endregion
    }
}
