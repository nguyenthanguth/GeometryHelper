using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a point nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoPoint2
    {
        /// <summary>
        /// Gets the closest point on a line segment to this point, clamped to its endpoints.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoLine2 line) => Projection2.ProjectToLine(line, this);

        /// <summary>
        /// Gets the closest point on the circumference of a circle to this point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoCircle2 circle) => Projection2.ProjectToCircle(circle, this);

        /// <summary>
        /// Gets the closest point on the boundary of a rectangle to this point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoRectangle2 rect) => Projection2.ProjectToRectangle(rect, this);

        /// <summary>
        /// Gets the closest point on the boundary of a polygon to this point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPolygon2 poly) => Projection2.ProjectToPolygon(poly, this);

        /// <summary>
        /// Gets the closest point on the path of a polyline to this point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPolyline2 polyline) => Projection2.ProjectToPolyline(polyline, this);

        /// <summary>
        /// Gets the point of a arc nearest this point, using the default tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoArc2 arc) => Arc2.ProjectToArc(arc, this);

        /// <summary>
        /// Gets the point of a arc nearest this point, within a tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoArc2 arc, Tolerance tolerance) => Arc2.ProjectToArc(arc, this, tolerance);

        /// <summary>
        /// Gets the point of a face nearest this point, using the default tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoFace2 face) => Projection2.ProjectToFace(face, this);

        /// <summary>
        /// Gets the point of a face nearest this point, within a tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoFace2 face, Tolerance tolerance) => Projection2.ProjectToFace(face, this, tolerance);

        /// <summary>
        /// Gets the point of a curved loop nearest this point, using the default tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPolygonArc2 loop) => Projection2.ProjectToPolygonArc(loop, this);

        /// <summary>
        /// Gets the point of a curved loop nearest this point, within a tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPolygonArc2 loop, Tolerance tolerance) => Projection2.ProjectToPolygonArc(loop, this, tolerance);

        /// <summary>
        /// Gets the point of a curved chain nearest this point, using the default tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPolylineArc2 chain) => Projection2.ProjectToPolylineArc(chain, this);

        /// <summary>
        /// Gets the point of a curved chain nearest this point, within a tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPolylineArc2 chain, Tolerance tolerance) => Projection2.ProjectToPolylineArc(chain, this, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on an arc, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Arc2.GetShortestLineTo(arc, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Arc2.GetShortestLineTo(arc, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => Face2.GetShortestLineTo(face, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on the boundary of a face, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Face2.GetShortestLineTo(face, this, tolerance).Reverse();

        /// <summary>
        /// Gets the edge of a polygon nearest this point, using the default tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoPolygon2 poly) => ClosestEdge2.GetClosestEdge(poly, this);

        /// <summary>
        /// Gets the edge of a polyline nearest this point, using the default tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoPolyline2 polyline) => ClosestEdge2.GetClosestEdge(polyline, this);

        /// <summary>
        /// Gets the edge of a rectangle nearest this point, using the default tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoRectangle2 rect) => ClosestEdge2.GetClosestEdge(rect, this);

        /// <summary>
        /// Gets the edge of a curved loop nearest this point, using the default tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop) => ClosestEdge2.GetClosestEdge(loop, this);

        /// <summary>
        /// Gets the edge of a curved loop nearest this point, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(loop, this, tolerance);

        /// <summary>
        /// Gets the edge of a curved chain nearest this point, using the default tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain) => ClosestEdge2.GetClosestEdge(chain, this);

        /// <summary>
        /// Gets the edge of a curved chain nearest this point, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(chain, this, tolerance);

        /// <summary>
        /// Gets the point of an edge nearest this point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoEdge2 edge)
            => edge.IsArc
                ? GetClosestPointOnBoundary(edge.ToArc())
                : GetClosestPointOnBoundary(edge.ToLine());
    }
}
