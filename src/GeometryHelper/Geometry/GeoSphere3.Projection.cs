using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a ball nearest another shape, and the shortest segment joining the two.
    /// </summary>
    /// <remarks>
    /// The segment leaves the surface of the ball, on the line from its centre to the point of the shape nearest the centre, and lands on that point, so
    /// it is as long as the distance to the shape. Where the shape reaches into the ball it has no length, and lies at the point of the shape nearest the
    /// centre, the centre itself where the shape holds it.
    /// </remarks>
    public readonly partial struct GeoSphere3
    {
        /// <summary>
        /// Gets the point of the surface closest to a target point: where the line from the centre through it leaves the ball.
        /// </summary>
        /// <remarks>
        /// From the ball of radius 50 about the origin, the point (0, 0, 200) gets (0, 0, 50), and so does (0, 0, 10). The centre is as near every point of the surface, and gets the first axis one radius from it, (50, 0, 0), so that the answer is the same each time.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Sphere3.ClosestOnSurface(this, point);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a point, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a point, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <remarks>
        /// From the ball of radius 50 about the origin the point (200, 0, 0) gets the segment from (50, 0, 0) to it, 150 long; a point inside the ball gets a segment of no length at the point.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
        {
            return Sphere3.Join(this, point, DistanceTo(point));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a segment, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => GetShortestLineTo(line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a segment, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <remarks>
        /// A segment no longer than the point tolerance is read as its start where it does not reach the ball, and joined
        /// where it reaches in where it does, as <see cref="DistanceTo(GeoLine3, Tolerance)"/> reads it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance)
        {
            if (Stub3.ReadsAsStart(line, tolerance))
            {
                GeoLine3 asItIs = GetShortestLineTo(line, Stub3.AsItIs(line, tolerance));

                if (Stub3.Meets(asItIs.Length, line))
                {
                    return asItIs;
                }
            }

            return Sphere3.Join(this, Projection3.ProjectToLine(line, Center, tolerance), DistanceTo(line, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a ray, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => GetShortestLineTo(ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a ray, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance)
        {
            return Sphere3.Join(this, Projection3.ProjectToRay(ray, Center), DistanceTo(ray));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an infinite line, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an infinite line, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline, Tolerance tolerance)
        {
            return Sphere3.Join(this, XLine3.Project(xline, Center), DistanceTo(xline, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an arc, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoArc3 arc) => GetShortestLineTo(arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an arc, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <remarks>
        /// The point of the arc is found at no tolerance, as the distance is, so a centre near the centre of the arc is not carried to the arc's start: at a
        /// tolerance of a thousandth, the ball of radius 49.99 about (0.0009, 0, 0) and the lower half of the circle of radius 50 about the origin, from
        /// (-50, 0, 0) through (0, -50, 0) to (50, 0, 0), are joined by the segment 0.0091 long that lands on (50, 0, 0).
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoArc3 arc, Tolerance tolerance)
        {
            return Sphere3.Join(this, arc.GetClosestPointOnBoundary(Center, default(Tolerance)), DistanceTo(arc, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a circle, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoCircle3 circle) => GetShortestLineTo(circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a circle, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        public GeoLine3 GetShortestLineTo(GeoCircle3 circle, Tolerance tolerance)
        {
            return Sphere3.Join(this, Projection3.ProjectToDisc(circle, Center, tolerance), DistanceTo(circle, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an ellipse, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoEllipse3 ellipse) => GetShortestLineTo(ellipse, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an ellipse, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        public GeoLine3 GetShortestLineTo(GeoEllipse3 ellipse, Tolerance tolerance)
        {
            return Sphere3.Join(this, Ellipse3.GetClosestPointOnSurface(ellipse, Center, tolerance), DistanceTo(ellipse, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an edge, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoEdge3 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an edge, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <remarks>
        /// The point of a bend is found at no tolerance, as the distance is, so a centre near the centre of its arc is not carried to the bend's start: at
        /// a tolerance of a thousandth, the ball of radius 49.99 about (0, -0.0009, 0) and the edge from (-50, 0, 0) to (50, 0, 0) bulging by 1 about Z,
        /// through (0, -50, 0), are joined by the segment 0.0091 long that lands on (0, -50, 0).
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoEdge3 edge, Tolerance tolerance)
        {
            if (Stub3.ReadsAsStart(edge, tolerance))
            {
                GeoLine3 asItIs = GetShortestLineTo(edge, Stub3.AsItIs(edge.ToLine(), tolerance));

                if (Stub3.Meets(asItIs.Length, edge.ToLine()))
                {
                    return asItIs;
                }
            }

            return Sphere3.Join(this, edge.GetMeasuredPoint(Center, tolerance), DistanceTo(edge, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline) => GetShortestLineTo(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a polyline, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return Sphere3.Join(this, Projection3.ProjectToPolyline(polyline, Center, default(Tolerance)), DistanceTo(polyline, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => GetShortestLineTo(polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a polygon, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <remarks>
        /// The point of the polygon nearest the centre is the one <see cref="GeometryHelper.Core.Projection3.ProjectToPolygon(GeoPolygon3, GeoPoint3, Tolerance)"/> gives, which keeps a foot that lands within the point tolerance past an edge: the segment may end up to that tolerance beyond the edge, as the shortest line of a polygon to a point does.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Sphere3.Join(this, Projection3.ProjectToPolygon(polygon, Center, tolerance, default(Tolerance)), DistanceTo(polygon, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolylineArc3 chain) => GetShortestLineTo(chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a curved chain, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <remarks>
        /// The point of a bend is found at no tolerance, as the distance is, so a centre near the centre of its arc is not carried to the bend's start, as
        /// <see cref="GetShortestLineTo(GeoEdge3, Tolerance)"/> says; so is the point of a straight edge, which is the edge it is however short.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolylineArc3 chain, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return Sphere3.Join(this, chain.GetMeasuredPoint(Center), DistanceTo(chain, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop) => GetShortestLineTo(loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a curved loop, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <remarks>
        /// The point of a bend is found at no tolerance, as the distance is, so a centre near the centre of its arc is not carried to the bend's start, as
        /// <see cref="GetShortestLineTo(GeoEdge3, Tolerance)"/> says; so is the point of a straight side, which is the side it is however short.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return Sphere3.Join(this, loop.GetMeasuredPoint(Center), DistanceTo(loop, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a triangle, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => GetShortestLineTo(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a triangle, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance)
        {
            return Sphere3.Join(this, Projection3.ProjectToTriangle(triangle, Center), DistanceTo(triangle));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => GetShortestLineTo(face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a face, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <remarks>
        /// The point of the face nearest the centre is the one <see cref="GeometryHelper.Core.Projection3.ProjectToFace(GeoFace3, GeoPoint3, Tolerance)"/> gives, which keeps a foot that lands within the point tolerance past an edge or a rim: the segment may end up to that tolerance beyond it, as the shortest line of a face to a point does.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Sphere3.Join(this, Projection3.ProjectToFace(face, Center, tolerance, default(Tolerance)), DistanceTo(face, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a plane, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => GetShortestLineTo(plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a plane, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <remarks>
        /// From the ball of radius 50 about (0, 0, 100) the floor gets the segment from (0, 0, 50) to (0, 0, 0), 50 long, square to the floor; the ball cutting the floor gets a segment of no length at the foot of its centre.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance)
        {
            return Sphere3.Join(this, Projection3.ProjectToPlane(plane, Center), DistanceTo(plane));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty, which has no point to land on.</exception>
        public GeoLine3 GetShortestLineTo(GeoAabb3 box) => GetShortestLineTo(box, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an axis-aligned box, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty, which has no point to land on.</exception>
        public GeoLine3 GetShortestLineTo(GeoAabb3 box, Tolerance tolerance)
        {
            return Sphere3.Join(this, box.GetClosestPointOnBoundary(Center), DistanceTo(box));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an oriented box, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoObb3 box) => GetShortestLineTo(box, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on an oriented box, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoObb3 box, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return Sphere3.Join(this, Projection3.ProjectToObb(box, Center), DistanceTo(box));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a solid, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => GetShortestLineTo(solid, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on a solid, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of the ball to the point of the shape nearest its centre, as long as the distance between them; of no length where they meet.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return Sphere3.Join(this, (Gap3.Holds(solid, Center) ? Center : Projection3.ProjectToSolid(solid, Center, tolerance, true)), DistanceTo(solid, tolerance));
        }

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on another ball, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoSphere3 other) => GetShortestLineTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ball and landing on another ball, within a tolerance.
        /// </summary>
        /// <returns>A segment from the surface of this ball to the surface of the other, along the line between their centres, as long as the gap between them; where they touch or overlap, of no length at the middle of the stretch they share along that line.</returns>
        /// <remarks>
        /// From the ball of radius 50 about the origin the ball of radius 10 about (200, 0, 0) gets the segment from (50, 0, 0) to (190, 0, 0), 140 long, and the same segment turned round is the one it gets in return, to the bit.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSphere3 other, Tolerance tolerance) => Sphere3.Join(this, other);
    }
}
