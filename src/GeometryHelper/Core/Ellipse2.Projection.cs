using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The shortest segment joining the rim of an ellipse to another shape, and which edge of a shape lies nearest it.
    /// </summary>
    /// <remarks>
    /// The segment runs from boundary to boundary, as a circle's does: it starts on the rim and ends on the other shape's
    /// edge or curve, so a shape lying wholly inside the ellipse still gets the gap out to the rim, where
    /// <see cref="DistanceTo(GeoEllipse2, GeoLine2)"/> reads the region and answers nought. Where the two cross, the segment
    /// has no length and sits at the first crossing, in the order the circle's would come: along a segment, edge by edge
    /// round a polygon, by the eccentric angle round a circle, an arc or another ellipse.
    /// </remarks>
    public static partial class Ellipse2
    {
        #region Segments

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a segment, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoLine2 line) => GetShortestLineTo(ellipse, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a segment, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance the crossings are found with.</param>
        /// <returns>
        /// A segment starting on the rim and ending on the segment; of no length at the first crossing along the segment
        /// where they cross. From the ellipse 300 by 100 centred on the origin to the segment from (-100, 150) to
        /// (100, 150), it runs from (0, 100) to (0, 150).
        /// </returns>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoLine2 line, Tolerance tolerance)
        {
            GeoPoint2[] crossings = GetIntersections(ellipse, line, tolerance);

            if (crossings.Length > 0)
            {
                return new GeoLine2(crossings[0], crossings[0]);
            }

            Nearest(ellipse, line, out GeoPoint2 onRim, out GeoPoint2 onLine);
            return new GeoLine2(onRim, onLine);
        }

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoPolyline2 polyline) => GetShortestLineTo(ellipse, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a polyline, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment starting on the rim and ending on the polyline; the earlier edge wins a tie.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return ShortestToEdges(ellipse, polyline.GetEdges(), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoPolygon2 polygon) => GetShortestLineTo(ellipse, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to the boundary of a polygon, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment starting on the rim and ending on an edge of the polygon; the earlier edge wins a tie.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return ShortestToEdges(ellipse, polygon.GetEdges(), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to the boundary of a rectangle, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoRectangle2 rect) => GetShortestLineTo(ellipse, rect, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to the boundary of a rectangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment starting on the rim and ending on a side of the rectangle; the earlier side wins a tie.</returns>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoRectangle2 rect, Tolerance tolerance)
        {
            return ShortestToEdges(ellipse, rect.GetEdges(), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a triangle, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoTriangle2 triangle) => GetShortestLineTo(ellipse, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a triangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle; one with corners on each other is read as its longest edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment starting on the rim and ending on an edge of the triangle.</returns>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoTriangle2 triangle, Tolerance tolerance)
        {
            return Triangle2.TryAsPolygon(triangle, tolerance, out GeoPolygon2 polygon, out GeoLine2 hull)
                ? GetShortestLineTo(ellipse, polygon, tolerance)
                : GetShortestLineTo(ellipse, hull, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to the boundary of a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoFace2 face) => GetShortestLineTo(ellipse, face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to the boundary of a face, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="face">The face, whose boundary is its outline and the rim of every hole.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The shortest of the segments to the outline and to each hole, the outline winning a tie.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoFace2 face, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            GeoLine2 best = GetShortestLineTo(ellipse, face.Boundary, tolerance);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                GeoLine2 candidate = GetShortestLineTo(ellipse, hole, tolerance);

                if (candidate.Length < best.Length)
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to an edge, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoEdge2 edge) => GetShortestLineTo(ellipse, edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to an edge, within a tolerance: as a segment or as an arc,
        /// whichever the edge is.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoEdge2 edge, Tolerance tolerance)
        {
            return edge.IsArc
                ? GetShortestLineTo(ellipse, edge.ToArc(), tolerance)
                : GetShortestLineTo(ellipse, edge.ToLine(), tolerance);
        }

        #endregion

        #region Curves

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a circle, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoCircle2 circle) => GetShortestLineTo(ellipse, circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to the circumference of a circle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance the crossings are found with.</param>
        /// <returns>
        /// A segment starting on the rim and ending on the circumference, of no length at the first crossing where they
        /// cross. From the ellipse 300 by 100 centred on the origin to the circle of radius 50 about (0, 300), it runs from
        /// (0, 100) to (0, 250); to the circle of radius 400 about the origin, from (300, 0) out to (400, 0).
        /// </returns>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoPoint2[] crossings = GetIntersections(ellipse, circle, tolerance);

            if (crossings.Length > 0)
            {
                return new GeoLine2(crossings[0], crossings[0]);
            }

            NearestToCircle(ellipse, circle.Center, circle.Radius, out GeoPoint2 onRim, out GeoPoint2 onCircle);
            return new GeoLine2(onRim, onCircle);
        }

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to an arc, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoArc2 arc) => GetShortestLineTo(ellipse, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment starting on the rim and ending on the arc, of no length at the first crossing where they cross.</returns>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoArc2 arc, Tolerance tolerance)
        {
            GeoPoint2[] crossings = GetIntersections(ellipse, arc, tolerance);

            if (crossings.Length > 0)
            {
                return new GeoLine2(crossings[0], crossings[0]);
            }

            NearestToArc(ellipse, arc, tolerance, out GeoPoint2 onRim, out GeoPoint2 onArc);
            return new GeoLine2(onRim, onArc);
        }

        /// <summary>
        /// Gets the shortest segment joining the rims of two ellipses, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoEllipse2 other) => GetShortestLineTo(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rims of two ellipses, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse the segment starts on.</param>
        /// <param name="other">The ellipse the segment ends on.</param>
        /// <param name="tolerance">The tolerance the crossings are found with.</param>
        /// <returns>
        /// A segment starting on the rim of the first and ending on the rim of the second, of no length at the first crossing
        /// where they cross; one inside the other gets the gap between the two rims. From the ellipse 300 by 100 centred on
        /// the origin to the one 100 by 50 about (0, 250) along Y, it runs from (0, 100) to (0, 150).
        /// </returns>
        /// <remarks>
        /// Found as <see cref="DistanceTo(GeoEllipse2, GeoEllipse2, Tolerance)"/> finds the gap, by Brent's method from 36
        /// starts round the first rim; the end on the second is the point of its rim nearest the start.
        /// </remarks>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoEllipse2 other, Tolerance tolerance)
        {
            GeoPoint2[] crossings = GetIntersections(ellipse, other, tolerance);

            if (crossings.Length > 0)
            {
                return new GeoLine2(crossings[0], crossings[0]);
            }

            RimToRim(ellipse, other, out double angle);
            GeoPoint2 start = GetPointAtAngle(ellipse, angle);

            return new GeoLine2(start, GetClosestPointOnBoundary(other, start));
        }

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoPolylineArc2 chain) => GetShortestLineTo(ellipse, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chain">The chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment starting on the rim and ending on the chain, measured on the arcs themselves; the earlier edge wins a tie.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return ShortestToEdges(ellipse, chain.GetEdges(), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoPolygonArc2 loop) => GetShortestLineTo(ellipse, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the rim of an ellipse to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="loop">The loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment starting on the rim and ending on the loop, measured on the arcs themselves; the earlier edge wins a tie.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return ShortestToEdges(ellipse, loop.GetEdges(), tolerance);
        }

        #endregion

        #region Nearest edge

        /// <summary>
        /// Gets the edge of a polygon nearest an ellipse, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetClosestEdge(GeoEllipse2 ellipse, GeoPolygon2 polygon) => GetClosestEdge(ellipse, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the edge of a polygon nearest an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance; not read, since each edge is measured as a segment.</param>
        /// <returns>The edge least far from the region, the earlier one winning a tie, as every edge reaching into the ellipse is nought away.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetClosestEdge(GeoEllipse2 ellipse, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return ClosestEdge2.Among(polygon.GetEdges(), edge => DistanceTo(ellipse, edge, tolerance));
        }

        /// <summary>
        /// Gets the edge of a polyline nearest an ellipse, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetClosestEdge(GeoEllipse2 ellipse, GeoPolyline2 polyline) => GetClosestEdge(ellipse, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the edge of a polyline nearest an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance; not read, since each edge is measured as a segment.</param>
        /// <returns>The edge least far from the region, the earlier one winning a tie.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetClosestEdge(GeoEllipse2 ellipse, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return ClosestEdge2.Among(polyline.GetEdges(), edge => DistanceTo(ellipse, edge, tolerance));
        }

        /// <summary>
        /// Gets the side of a rectangle nearest an ellipse, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestEdge(GeoEllipse2 ellipse, GeoRectangle2 rect) => GetClosestEdge(ellipse, rect, Tolerance.Global);

        /// <summary>
        /// Gets the side of a rectangle nearest an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance; not read, since each side is measured as a segment.</param>
        /// <returns>The side least far from the region, the earlier one winning a tie.</returns>
        public static GeoLine2 GetClosestEdge(GeoEllipse2 ellipse, GeoRectangle2 rect, Tolerance tolerance)
        {
            return ClosestEdge2.Among(rect.GetEdges(), edge => DistanceTo(ellipse, edge, tolerance));
        }

        /// <summary>
        /// Gets the edge of a triangle nearest an ellipse, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestEdge(GeoEllipse2 ellipse, GeoTriangle2 triangle) => GetClosestEdge(ellipse, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the edge of a triangle nearest an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle; one with corners on each other is asked edge by edge.</param>
        /// <param name="tolerance">The tolerance the triangle is read with.</param>
        /// <returns>The edge least far from the region, the earlier one winning a tie.</returns>
        public static GeoLine2 GetClosestEdge(GeoEllipse2 ellipse, GeoTriangle2 triangle, Tolerance tolerance)
        {
            if (Triangle2.TryAsPolygon(triangle, tolerance, out GeoPolygon2 polygon, out _))
            {
                return GetClosestEdge(ellipse, polygon, tolerance);
            }

            GeoLine2 closest = triangle.GetEdgeAt(0);
            double best = DistanceTo(ellipse, closest, tolerance);

            for (int i = 1; i < 3; i++)
            {
                GeoLine2 edge = triangle.GetEdgeAt(i);
                double reach = DistanceTo(ellipse, edge, tolerance);

                if (reach < best)
                {
                    best = reach;
                    closest = edge;
                }
            }

            return closest;
        }

        /// <summary>
        /// Gets the edge of a curved loop nearest an ellipse, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoEdge2 GetClosestEdge(GeoEllipse2 ellipse, GeoPolygonArc2 loop) => GetClosestEdge(ellipse, loop, Tolerance.Global);

        /// <summary>
        /// Gets the edge of a curved loop nearest an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="loop">The loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge least far from the region, measured on the arcs themselves, the earlier one winning a tie.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoEdge2 GetClosestEdge(GeoEllipse2 ellipse, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return ClosestEdge2.Among(loop.GetEdges(), edge => DistanceTo(ellipse, edge, tolerance));
        }

        /// <summary>
        /// Gets the edge of a curved chain nearest an ellipse, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoEdge2 GetClosestEdge(GeoEllipse2 ellipse, GeoPolylineArc2 chain) => GetClosestEdge(ellipse, chain, Tolerance.Global);

        /// <summary>
        /// Gets the edge of a curved chain nearest an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chain">The chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge least far from the region, measured on the arcs themselves, the earlier one winning a tie.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoEdge2 GetClosestEdge(GeoEllipse2 ellipse, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return ClosestEdge2.Among(chain.GetEdges(), edge => DistanceTo(ellipse, edge, tolerance));
        }

        #endregion

        #region Shortest segment helpers

        /// <summary>
        /// Gets the shortest of the segments joining the rim to each of a run of straight edges, the earlier winning a tie and
        /// the search stopping at the first crossing.
        /// </summary>
        private static GeoLine2 ShortestToEdges(GeoEllipse2 ellipse, IEnumerable<GeoLine2> edges, Tolerance tolerance)
        {
            GeoLine2 best = default(GeoLine2);
            double bestLength = 0.0;
            bool found = false;

            foreach (GeoLine2 edge in edges)
            {
                GeoLine2 candidate = GetShortestLineTo(ellipse, edge, tolerance);
                double length = candidate.Length;

                if (!found || length < bestLength)
                {
                    found = true;
                    best = candidate;
                    bestLength = length;

                    if (!(bestLength > 0.0))
                    {
                        return best;
                    }
                }
            }

            if (!found)
            {
                throw new ArgumentException("Cannot measure to a shape that has no edges.", nameof(edges));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest of the segments joining the rim to each of a run of edges that may curve.
        /// </summary>
        private static GeoLine2 ShortestToEdges(GeoEllipse2 ellipse, IEnumerable<GeoEdge2> edges, Tolerance tolerance)
        {
            GeoLine2 best = default(GeoLine2);
            double bestLength = 0.0;
            bool found = false;

            foreach (GeoEdge2 edge in edges)
            {
                GeoLine2 candidate = GetShortestLineTo(ellipse, edge, tolerance);
                double length = candidate.Length;

                if (!found || length < bestLength)
                {
                    found = true;
                    best = candidate;
                    bestLength = length;

                    if (!(bestLength > 0.0))
                    {
                        return best;
                    }
                }
            }

            if (!found)
            {
                throw new ArgumentException("Cannot measure to a shape that has no edges.", nameof(edges));
            }

            return best;
        }

        #endregion
    }
}
