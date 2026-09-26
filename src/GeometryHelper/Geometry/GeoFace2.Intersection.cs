using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a face crosses another shape.
    /// </summary>
    public sealed partial class GeoFace2
    {
        /// <summary>
        /// Gets every point where a segment crosses the boundary of this face.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => Face2.GetIntersections(this, line);

        /// <summary>
        /// Gets every point where a segment crosses the boundary of this face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Face2.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets every point where a arc crosses the boundary of this face.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => Face2.GetIntersections(this, arc);

        /// <summary>
        /// Gets every point where a arc crosses the boundary of this face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Face2.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses the boundary of this face.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Face2.GetIntersections(this, circle);

        /// <summary>
        /// Gets every point where a circle crosses the boundary of this face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Face2.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Gets every point where a rectangle crosses the boundary of this face.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Face2.GetIntersections(this, rect);

        /// <summary>
        /// Gets every point where a rectangle crosses the boundary of this face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Face2.GetIntersections(this, rect, tolerance);

        /// <summary>
        /// Gets every point where a polyline crosses the boundary of this face.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Face2.GetIntersections(this, polyline);

        /// <summary>
        /// Gets every point where a polyline crosses the boundary of this face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Face2.GetIntersections(this, polyline, tolerance);

        /// <summary>
        /// Gets every point where a polygon crosses the boundary of this face.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon) => Face2.GetIntersections(this, polygon);

        /// <summary>
        /// Gets every point where a polygon crosses the boundary of this face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon, Tolerance tolerance) => Face2.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Gets every point where a curved loop crosses the boundary of this face.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => Face2.GetIntersections(this, loop);

        /// <summary>
        /// Gets every point where a curved loop crosses the boundary of this face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Face2.GetIntersections(this, loop, tolerance);

        /// <summary>
        /// Gets every point where a curved chain crosses the boundary of this face.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => Face2.GetIntersections(this, chain);

        /// <summary>
        /// Gets every point where a curved chain crosses the boundary of this face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Face2.GetIntersections(this, chain, tolerance);

        /// <summary>
        /// Gets every point where this face crosses an edge.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where this face crosses an edge, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);
    }
}
