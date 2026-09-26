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
        /// <summary>
        /// Tries to find where this face meets an arc.
        /// </summary>
        /// <remarks>
        /// This is <see cref="GetIntersections(GeoArc2, Tolerance)"/> read a second way -- the list, plus
        /// whether it is empty -- so a caller who only wants to know <i>whether</i> need not measure the array.
        /// It is built here rather than forwarded to <c>Core</c> because there is no arithmetic in it: the
        /// crossing itself is worked out in one place and this only reports on it.
        /// </remarks>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections)
            => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this face meets an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this face meets a circle.
        /// </summary>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections)
            => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this face meets a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this face meets an edge.
        /// </summary>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections)
            => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this face meets an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(edge, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this face meets a segment.
        /// </summary>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections)
            => TryIntersectWith(line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this face meets a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this face meets a polygon.
        /// </summary>
        public bool TryIntersectWith(GeoPolygon2 polygon, out GeoPoint2[] intersections)
            => TryIntersectWith(polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this face meets a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolygon2 polygon, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this face meets a loop that may curve.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections)
            => TryIntersectWith(loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this face meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <param name="loop">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this face meets a chain.
        /// </summary>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections)
            => TryIntersectWith(polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this face meets a chain, within a tolerance.
        /// </summary>
        /// <param name="polyline">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this face meets a chain that may curve.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections)
            => TryIntersectWith(chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this face meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <param name="chain">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find where this face meets a rectangle.
        /// </summary>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections)
            => TryIntersectWith(rect, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this face meets a rectangle, within a tolerance.
        /// </summary>
        /// <param name="rect">The shape to test against.</param>
        /// <param name="intersections">The places they meet when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(rect, tolerance);

            return intersections.Length > 0;
        }

    }
}
