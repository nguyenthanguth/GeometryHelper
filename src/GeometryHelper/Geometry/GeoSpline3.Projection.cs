using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a spline nearest a point, where on the spline it lies, and the shortest segment joining the two.
    /// </summary>
    public sealed partial class GeoSpline3
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
        /// polynomial in Bernstein form, and all of them are found, to rounding.
        /// </remarks>
        public double GetKnotAtPoint(GeoPoint3 point) => _curve.ClosestParameter(new[] { point.X, point.Y, point.Z }, out _);

        /// <summary>
        /// Gets the normalised parameter, from 0 to 1, of the point of the spline nearest a point, using the default tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>The share of the domain: 0 at the start and 1 at the end; <see cref="GetKnotAtPoint(GeoPoint3)"/> gives the knot value.</returns>
        public double GetParameterAtPoint(GeoPoint3 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the normalised parameter, from 0 to 1, of the point of the spline nearest a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>The share of the domain: 0 at the start and 1 at the end.</returns>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance) => _curve.ParameterAtKnot(GetKnotAtPoint(point));

        /// <summary>
        /// Gets how far along the spline the point nearest a point lies, using the default tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>The length from the start of the spline to its nearest point.</returns>
        public double GetDistanceAtPoint(GeoPoint3 point) => GetDistanceAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets how far along the spline the point nearest a point lies, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>The length from the start of the spline to its nearest point.</returns>
        public double GetDistanceAtPoint(GeoPoint3 point, Tolerance tolerance) => _curve.LengthTo(GetKnotAtPoint(point));

        /// <summary>
        /// Gets the point of this spline nearest a point.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>The spline's point at <see cref="GetKnotAtPoint(GeoPoint3)"/>.</returns>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetPointAtKnot(GetKnotAtPoint(point));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a point, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline's nearest point to the point; as long as <see cref="DistanceTo(GeoPoint3)"/>, to rounding.</returns>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance) => new GeoLine3(GetClosestPointOnBoundary(point), point);

        #region Against the other shapes in space

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a line segment, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a line segment; see <see cref="GetShortestLineTo(GeoLine3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => GetShortestLineTo(line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of the spline to the point of the line segment nearest it, as long as <see cref="DistanceTo(GeoLine3, Tolerance)"/>; of no length where the two cross.</returns>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, line));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a ray, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a ray; see <see cref="GetShortestLineTo(GeoRay3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => GetShortestLineTo(ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of the spline to the point of the ray nearest it, as long as <see cref="DistanceTo(GeoRay3, Tolerance)"/>; of no length where the two cross.</returns>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, ray));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an infinite line; see <see cref="GetShortestLineTo(GeoXLine3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of the spline to the point of the infinite line nearest it, as long as <see cref="DistanceTo(GeoXLine3, Tolerance)"/>; of no length where the two cross.</returns>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, xline));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a plane, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a plane; see <see cref="GetShortestLineTo(GeoPlane3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => GetShortestLineTo(plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of the spline to the point of the plane nearest it, as long as <see cref="DistanceTo(GeoPlane3, Tolerance)"/>; of no length where the two cross.</returns>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, plane));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a polyline, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a polyline; see <see cref="GetShortestLineTo(GeoPolyline3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline) => GetShortestLineTo(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of the spline to the point of the polyline nearest it, as long as <see cref="DistanceTo(GeoPolyline3, Tolerance)"/>; of no length where the two cross.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, polyline));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a polygon, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a polygon; see <see cref="GetShortestLineTo(GeoPolygon3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => GetShortestLineTo(polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of the polygon nearest it, as long as <see cref="DistanceTo(GeoPolygon3, Tolerance)"/>;
        /// of no length where the spline pierces it or lies in it.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, polygon));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a triangle, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a triangle; see <see cref="GetShortestLineTo(GeoTriangle3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => GetShortestLineTo(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of the triangle nearest it, as long as <see cref="DistanceTo(GeoTriangle3, Tolerance)"/>;
        /// of no length where the spline pierces it or lies in it.
        /// </returns>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, triangle));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a face, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a face; see <see cref="GetShortestLineTo(GeoFace3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => GetShortestLineTo(face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of the face nearest it, as long as <see cref="DistanceTo(GeoFace3, Tolerance)"/>;
        /// of no length where the spline pierces it or lies in it.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, face));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an axis-aligned box; see <see cref="GetShortestLineTo(GeoAabb3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoAabb3 box) => GetShortestLineTo(box, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of the skin nearest it, as long as <see cref="DistanceTo(GeoAabb3, Tolerance)"/>
        /// but where the box holds the spline, which it reads as the gap out to the skin; of no length where the two cross.
        /// </returns>
        public GeoLine3 GetShortestLineTo(GeoAabb3 box, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, box));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an oriented box, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an oriented box; see <see cref="GetShortestLineTo(GeoObb3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoObb3 box) => GetShortestLineTo(box, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of the skin nearest it, as long as <see cref="DistanceTo(GeoObb3, Tolerance)"/>
        /// but where the box holds the spline, which it reads as the gap out to the skin; of no length where the two cross.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoObb3 box, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, box));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a solid, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a solid; see <see cref="GetShortestLineTo(GeoSolid3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => GetShortestLineTo(solid, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance; read only to cut the solid's openings in, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of the skin nearest it, as long as <see cref="DistanceTo(GeoSolid3, Tolerance)"/>
        /// but where the solid holds the spline, which it reads as the gap out to the skin; of no length where the two cross.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, solid, tolerance, true));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a ball, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a ball; see <see cref="GetShortestLineTo(GeoSphere3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere) => GetShortestLineTo(sphere, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a ball, within a tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of the skin nearest it, as long as <see cref="DistanceTo(GeoSphere3, Tolerance)"/>
        /// but where the ball holds the spline, which it reads as the gap out to the skin; of no length where the two cross.
        /// </returns>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, sphere));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an arc, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an arc; see <see cref="GetShortestLineTo(GeoArc3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoArc3 arc) => GetShortestLineTo(arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of the spline to the point of the arc nearest it, as long as <see cref="DistanceTo(GeoArc3, Tolerance)"/>; of no length where the two cross.</returns>
        public GeoLine3 GetShortestLineTo(GeoArc3 arc, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, arc));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a circle, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a circle; see <see cref="GetShortestLineTo(GeoCircle3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoCircle3 circle) => GetShortestLineTo(circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of the circle nearest it, as long as <see cref="DistanceTo(GeoCircle3, Tolerance)"/>;
        /// of no length where the spline pierces it or lies in it.
        /// </returns>
        public GeoLine3 GetShortestLineTo(GeoCircle3 circle, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, circle));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an ellipse, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an ellipse; see <see cref="GetShortestLineTo(GeoEllipse3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoEllipse3 ellipse) => GetShortestLineTo(ellipse, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>
        /// The segment from the point of the spline to the point of the ellipse nearest it, as long as <see cref="DistanceTo(GeoEllipse3, Tolerance)"/>;
        /// of no length where the spline pierces it or lies in it.
        /// </returns>
        public GeoLine3 GetShortestLineTo(GeoEllipse3 ellipse, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, ellipse));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an edge, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to an edge; see <see cref="GetShortestLineTo(GeoEdge3, Tolerance)"/>.</returns>
        public GeoLine3 GetShortestLineTo(GeoEdge3 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of the spline to the point of the edge nearest it, as long as <see cref="DistanceTo(GeoEdge3, Tolerance)"/>; of no length where the two cross.</returns>
        public GeoLine3 GetShortestLineTo(GeoEdge3 edge, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, edge));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a curved chain; see <see cref="GetShortestLineTo(GeoPolylineArc3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolylineArc3 chain) => GetShortestLineTo(chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of the spline to the point of the curved chain nearest it, as long as <see cref="DistanceTo(GeoPolylineArc3, Tolerance)"/>; of no length where the two cross.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolylineArc3 chain, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, chain));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline to a curved loop; see <see cref="GetShortestLineTo(GeoPolygonArc3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop) => GetShortestLineTo(loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of the spline to the point of the curved loop nearest it, as long as <see cref="DistanceTo(GeoPolygonArc3, Tolerance)"/>; of no length where the two cross.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop, Tolerance tolerance) => Spline3.GetShortestLineTo(this, Spline3.Of(this, loop));

        #endregion
    }
}
