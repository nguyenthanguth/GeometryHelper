using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a face nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoFace2
    {
        /// <summary>
        /// Gets the point of the boundary of this face nearest a target point.
        /// </summary>
        /// <remarks>
        /// The boundary of a face is its outline and the rim of every hole, so a point sitting in a hole is
        /// answered with a point of that rim. The answer is always on the boundary, even for a point on the
        /// material, where <see cref="DistanceTo(GeoPoint2)"/> reports nothing at all.
        /// </remarks>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Projection2.ProjectToFace(this, point);

        /// <summary>
        /// Gets the point of the boundary of this face nearest a target point, within a tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point, Tolerance tolerance) => Projection2.ProjectToFace(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a point.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point) => Face2.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a point, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point, Tolerance tolerance) => Face2.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a segment.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Face2.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a segment, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Face2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on an arc.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Face2.GetShortestLineTo(this, arc);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Face2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a circle.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => Face2.GetShortestLineTo(this, circle);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a circle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Face2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a rectangle.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => Face2.GetShortestLineTo(this, rect);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a rectangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Face2.GetShortestLineTo(this, rect, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a polyline.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Face2.GetShortestLineTo(this, polyline);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a polyline, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Face2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a polygon.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon) => Face2.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a polygon, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon, Tolerance tolerance) => Face2.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a curved loop.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => Face2.GetShortestLineTo(this, loop);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a curved loop, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Face2.GetShortestLineTo(this, loop, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a curved chain.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Face2.GetShortestLineTo(this, chain);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of this face and landing on a curved chain, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Face2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this face and landing on an edge.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this face and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetShortestLineTo(edge.ToArc(), tolerance)
                : GetShortestLineTo(edge.ToLine(), tolerance);
    }
}
