using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a polygon nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoPolygon2
    {
        /// <summary>
        /// Gets the shortest segment joining this polygon to an arc.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Projection2.GetShortestLineTo(this, arc);

        /// <summary>
        /// Gets the shortest segment joining this polygon to an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Projection2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a curved loop.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => Projection2.GetShortestLineTo(this, loop);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a curved loop, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Projection2.GetShortestLineTo(this, loop, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a curved chain.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Projection2.GetShortestLineTo(this, chain);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a curved chain, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Projection2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Gets the edge of this polygon nearest a point.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoPoint2 point) => ClosestEdge2.GetClosestEdge(this, point);

        /// <summary>
        /// Gets the edge of this polygon nearest a segment.
        /// </summary>
        /// <remarks>
        /// The answer is one of this polygon's own edges, not a segment joining the two shapes.
        /// For that, see <see cref="GetShortestLineTo(GeoLine2)"/>.
        /// </remarks>
        public GeoLine2 GetClosestEdge(GeoLine2 line) => ClosestEdge2.GetClosestEdge(this, line);

        /// <summary>
        /// Gets the edge of this polygon nearest a circle.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoCircle2 circle) => ClosestEdge2.GetClosestEdge(this, circle);

        /// <summary>
        /// Gets the closest point on the boundary of this polygon to a target point, including for points
        /// inside the polygon.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Projection2.ProjectToPolygon(this, point);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this polygon and landing on a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// It starts where <see cref="GetClosestPointOnBoundary(GeoPoint2)"/> lands, even for a point inside, so inside
        /// it is as long as the depth to the nearest edge; <see cref="DistanceTo(GeoPoint2)"/> reads the polygon as filled and
        /// answers nought there. Outside, the two agree, beside a side no longer than the tolerance too.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point) => Projection2.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this polygon and landing on a point, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point, Tolerance tolerance) => Projection2.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on a line segment using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Projection2.GetShortestLineTo(this, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on a line segment within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Projection2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the circumference of a circle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => Projection2.GetShortestLineTo(this, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the circumference of a circle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Projection2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the boundary of a rectangle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => Projection2.GetShortestLineTo(this, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the boundary of a rectangle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Projection2.GetShortestLineTo(this, rect, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on a polyline using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Projection2.GetShortestLineTo(this, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on a polyline within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the boundary of another polygon using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 other) => Projection2.GetShortestLineTo(this, other, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the boundary of another polygon within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 other, Tolerance tolerance) => Projection2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this polygon and landing on the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => Face2.GetShortestLineTo(face, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this polygon and landing on the boundary of a face, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Face2.GetShortestLineTo(face, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this polygon and landing on an edge.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this polygon and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetShortestLineTo(edge.ToArc(), tolerance)
                : GetShortestLineTo(edge.ToLine(), tolerance);

        #region Triangles

        /// <summary>
        /// Gets the shortest segment leaving this polygon and landing on a triangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle) => Triangle2.GetShortestLineTo(triangle, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this polygon and landing on a triangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.GetShortestLineTo(triangle, this, tolerance).Reverse();

        #endregion

        #region Rays

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this polygon and landing on a ray, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRay2 ray) => Projection2.GetShortestLineTo(this, ray);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this polygon and landing on a ray, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRay2 ray, Tolerance tolerance) => Projection2.GetShortestLineTo(this, ray, tolerance);

        #endregion

        #region XLines

        /// <summary>
        /// Gets the shortest segment leaving this polygon and landing on an infinite line, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoXLine2 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this polygon and landing on an infinite line, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoXLine2 xline, Tolerance tolerance) => Projection2.GetShortestLineTo(this, xline, tolerance);

        #endregion
    }
}
