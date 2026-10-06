using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where the rim of an ellipse crosses another shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A segment is read in the ellipse's own frame, scaled so that the ellipse is the unit circle: there the segment is
    /// still straight, and where it crosses the rim is a quadratic, solved exactly. Whether it only grazes the rim is
    /// judged as a distance: the rim point whose tangent runs along the segment is within the point tolerance of it, as a
    /// circle judges a segment by how near its centre's foot comes to the circumference.
    /// </para>
    /// <para>
    /// A circle, an arc or another ellipse is read along the rim: its equation at the point of the rim at angle t is a
    /// function of t made of two harmonics, a polynomial of degree four in z = e^{it}. Its turning points come from the
    /// roots of that polynomial, and between two of them it runs one way, so each crossing is found inside a bracket and
    /// to the last digit. A rim that comes within the point tolerance without crossing touches it, and gives one point,
    /// midway between the two curves; two crossings no further apart than twice the point tolerance, their half chord within
    /// it, are one point at the middle of the chord, as two circles' are.
    /// </para>
    /// <para>
    /// The order is the circle's. Crossings of a circle, an arc or another ellipse come in the order of the eccentric angle of
    /// this ellipse, from the end of its major axis round counter-clockwise, ties broken on X and then Y. Those of a segment
    /// run along the segment; those of a polyline, a polygon, a rectangle, a triangle or a curved chain or loop come edge by
    /// edge in the shape's own order, each edge's in its own order; those of a face come from the outline, then from each
    /// hole.
    /// </para>
    /// </remarks>
    public static partial class Ellipse2
    {
        #region Segments

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a segment, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoLine2 line) => GetIntersections(ellipse, line, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance: a segment passing within its point tolerance of the rim touches it.</param>
        /// <returns>
        /// The crossings in order along the segment, as a circle gives them: two where it cuts across, one where it only
        /// touches, none where it misses or lies wholly inside. The segment from (-400, 50) to (400, 50) crosses the ellipse
        /// 300 by 100 centred on the origin at (-259.8, 50), then at (259.8, 50).
        /// </returns>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoLine2 line, Tolerance tolerance)
        {
            return Crossings(ellipse, line, tolerance);
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses a segment, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoLine2 line, out GeoPoint2[] intersections) => TryIntersectWith(ellipse, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossings in order along the segment; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the segment crosses or touches the rim; otherwise, false, a segment wholly inside included.</returns>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, line, tolerance);
            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoPolyline2 polyline) => GetIntersections(ellipse, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings edge by edge, in the polyline's order, a point two edges share given once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return AlongEdges(ellipse, polyline.GetEdges(), tolerance);
        }

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoPolygon2 polygon) => GetIntersections(ellipse, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses the boundary of a polygon, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings edge by edge, in the polygon's order, a point two edges share given once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return AlongEdges(ellipse, polygon.GetEdges(), tolerance);
        }

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses the boundary of a rectangle, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoRectangle2 rect) => GetIntersections(ellipse, rect, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses the boundary of a rectangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings side by side, in the rectangle's order, a corner two sides share given once.</returns>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoRectangle2 rect, Tolerance tolerance)
        {
            return AlongEdges(ellipse, rect.GetEdges(), tolerance);
        }

        /// <summary>
        /// Gets the points where the rim of an ellipse meets the edges of a triangle, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoTriangle2 triangle) => GetIntersections(ellipse, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse meets the edges of a triangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle; one with corners on each other is read as its longest edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings edge by edge, in the triangle's order.</returns>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoTriangle2 triangle, Tolerance tolerance)
        {
            return Triangle2.TryAsPolygon(triangle, tolerance, out GeoPolygon2 polygon, out GeoLine2 hull)
                ? GetIntersections(ellipse, polygon, tolerance)
                : GetIntersections(ellipse, hull, tolerance);
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse crosses the boundary of a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoFace2 face) => GetIntersections(ellipse, face, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse crosses the boundary of a face, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="face">The face, whose boundary is its outline and the rim of every hole.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings of the outline, then those of each hole in turn, as a circle gives them.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoFace2 face, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            var found = new List<GeoPoint2>(GetIntersections(ellipse, face.Boundary, tolerance));

            foreach (GeoPolygon2 hole in face.Holes)
            {
                found.AddRange(GetIntersections(ellipse, hole, tolerance));
            }

            return found.ToArray();
        }

        #endregion

        #region Curves

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a circle, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoCircle2 circle) => GetIntersections(ellipse, circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance: a circle that comes within its point tolerance of the rim touches it, and two crossings no further apart than twice it are one.</param>
        /// <returns>
        /// Up to four points, in the order of the eccentric angle of the ellipse; none when the two lie on each other.
        /// The circle of radius 150 about the origin crosses the ellipse 300 by 100 centred there at four points, the first
        /// at (118.6, 91.9).
        /// </returns>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoCircle2 circle, Tolerance tolerance)
        {
            if (OnEachOther(ellipse, circle, tolerance))
            {
                return Array.Empty<GeoPoint2>();
            }

            return Meet(ellipse, AlongCircle(ellipse, circle.Center, circle.Radius), Other.Of(circle), tolerance);
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses a circle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoCircle2 circle, out GeoPoint2[] intersections) => TryIntersectWith(ellipse, circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossings in the order of the eccentric angle; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two cross or touch; otherwise, false, for a circle wholly inside or outside, or lying on the rim.</returns>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, circle, tolerance);
            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses an arc, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoArc2 arc) => GetIntersections(ellipse, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings of the circle carrying the arc that the arc reaches, within the point tolerance of its ends, in the order of the eccentric angle.</returns>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoArc2 arc, Tolerance tolerance)
        {
            GeoPoint2[] onCircle = GetIntersections(ellipse, arc.GetCircle(), tolerance);

            if (onCircle.Length == 0)
            {
                return onCircle;
            }

            var kept = new List<GeoPoint2>(onCircle.Length);

            foreach (GeoPoint2 point in onCircle)
            {
                if (Arc2.IsPointOn(arc, point, tolerance))
                {
                    kept.Add(point);
                }
            }

            return kept.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses an arc, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoArc2 arc, out GeoPoint2[] intersections) => TryIntersectWith(ellipse, arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossings in the order of the eccentric angle; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the arc crosses or touches the rim; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, arc, tolerance);
            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the rims of two ellipses cross, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoEllipse2 other) => GetIntersections(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rims of two ellipses cross, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse whose eccentric angle orders the points.</param>
        /// <param name="other">The other ellipse.</param>
        /// <param name="tolerance">The tolerance: rims that come within its point tolerance of each other touch, and two crossings no further apart than twice it are one.</param>
        /// <returns>
        /// Up to four points, in the order of the eccentric angle of the first ellipse; none when the two are the same
        /// ellipse. The ellipse 300 by 100 and the same ellipse turned a quarter, both centred on the origin, cross at
        /// (94.9, 94.9) first, then at the three points mirrored from it.
        /// </returns>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoEllipse2 other, Tolerance tolerance)
        {
            if (IsEqualTo(ellipse, other, tolerance))
            {
                return Array.Empty<GeoPoint2>();
            }

            Wave wave = Along(ellipse.Center, ellipse.MajorAxis, ellipse.MajorRadius, ellipse.MinorRadius, other);

            return Meet(ellipse, wave, Other.Of(other), tolerance);
        }

        /// <summary>
        /// Tries to find where the rims of two ellipses cross, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoEllipse2 other, out GeoPoint2[] intersections) => TryIntersectWith(ellipse, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rims of two ellipses cross, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse whose eccentric angle orders the points.</param>
        /// <param name="other">The other ellipse.</param>
        /// <param name="intersections">The crossings; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rims cross or touch; otherwise, false, for one wholly inside the other, apart, or the same.</returns>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoEllipse2 other, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, other, tolerance);
            return intersections.Length > 0;
        }

        #endregion

        #region Edges and curved chains

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses an edge, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoEdge2 edge) => GetIntersections(ellipse, edge, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses an edge, within a tolerance: as a segment or as an arc, whichever
        /// the edge is.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoEdge2 edge, Tolerance tolerance)
        {
            return edge.IsArc
                ? GetIntersections(ellipse, edge.ToArc(), tolerance)
                : GetIntersections(ellipse, edge.ToLine(), tolerance);
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses an edge, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoEdge2 edge, out GeoPoint2[] intersections) => TryIntersectWith(ellipse, edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossings; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the edge crosses or touches the rim; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoEdge2 edge, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, edge, tolerance);
            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoPolylineArc2 chain) => GetIntersections(ellipse, chain, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chain">The chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings edge by edge, on the arcs themselves, a point two edges share given once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return AlongEdges(ellipse, chain.GetEdges(), tolerance);
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoPolylineArc2 chain, out GeoPoint2[] intersections) => TryIntersectWith(ellipse, chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chain">The chain.</param>
        /// <param name="intersections">The crossings; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the chain crosses or touches the rim anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, chain, tolerance);
            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoPolygonArc2 loop) => GetIntersections(ellipse, loop, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of an ellipse crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="loop">The loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings edge by edge, on the arcs themselves, a point two edges share given once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return AlongEdges(ellipse, loop.GetEdges(), tolerance);
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoPolygonArc2 loop, out GeoPoint2[] intersections) => TryIntersectWith(ellipse, loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="loop">The loop.</param>
        /// <param name="intersections">The crossings; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the loop crosses or touches the rim anywhere; otherwise, false, one wholly inside the other included.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, loop, tolerance);
            return intersections.Length > 0;
        }

        #endregion

        #region Crossing helpers

        /// <summary>
        /// Gathers the crossings of the rim with a run of straight edges, edge by edge, a point two edges share given once.
        /// </summary>
        private static GeoPoint2[] AlongEdges(GeoEllipse2 ellipse, IEnumerable<GeoLine2> edges, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoLine2 edge in edges)
            {
                foreach (GeoPoint2 point in Crossings(ellipse, edge, tolerance))
                {
                    ArcChain2.Keep(found, point, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Gathers the crossings of the rim with a run of edges that may curve, edge by edge, a point two edges share given
        /// once.
        /// </summary>
        private static GeoPoint2[] AlongEdges(GeoEllipse2 ellipse, IEnumerable<GeoEdge2> edges, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 edge in edges)
            {
                foreach (GeoPoint2 point in GetIntersections(ellipse, edge, tolerance))
                {
                    ArcChain2.Keep(found, point, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Gets the crossings of the rim with a segment, in order along the segment.
        /// </summary>
        /// <remarks>
        /// In the frame where the ellipse is the unit circle the segment is s ↦ U0 + s dU, and it meets the rim where
        /// |U0 + s dU|² = 1. The rim point whose tangent runs along the segment, on the segment's side, is how deep the
        /// segment cuts or how near it passes, measured in drawing units: within the point tolerance it touches, at the foot
        /// of that point on the segment, which is the foot of the centre's perpendicular when the ellipse is a circle.
        /// </remarks>
        private static GeoPoint2[] Crossings(GeoEllipse2 ellipse, GeoLine2 line, Tolerance tolerance)
        {
            GeoVector2 direction = line.Direction;
            double length = direction.Length;

            if (length <= tolerance.EqualPoint)
            {
                return IsPointOn(ellipse, line.StartPoint, tolerance) ? new[] { line.StartPoint } : Array.Empty<GeoPoint2>();
            }

            Across(ellipse, line, length, out double along0, out double across0, out double alongStep, out double acrossStep, out double sideAlong, out double sideAcross, out double side);

            // The rim point whose tangent runs along the segment, on the segment's side, and how far it is from the segment.
            double depth = Math.Abs((sideAlong - along0) * acrossStep - (sideAcross - across0) * alongStep) / length;
            var points = new List<GeoPoint2>(2);

            if (depth <= tolerance.EqualPoint)
            {
                double foot = ((sideAlong - along0) * alongStep + (sideAcross - across0) * acrossStep) / (length * length);
                OnSegment(points, line, foot, length, tolerance);
            }
            else if (side < 1.0)
            {
                double a = ellipse.MajorRadius;
                double b = ellipse.MinorRadius;
                double u0 = along0 / a;
                double v0 = across0 / b;
                double du = alongStep / a;
                double dv = acrossStep / b;
                double speed = du * du + dv * dv;
                double middle = -(u0 * du + v0 * dv) / speed;
                double half = Math.Sqrt((1.0 - side) * (1.0 + side) / speed);

                if (half * length <= tolerance.EqualPoint)
                {
                    // Two crossings whose half chord is within the tolerance, across a rim narrower than twice it, are one,
                    // at the middle of the chord, as two circles' are.
                    OnSegment(points, line, middle, length, tolerance);
                }
                else
                {
                    OnSegment(points, line, middle - half, length, tolerance);
                    OnSegment(points, line, middle + half, length, tolerance);
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Reads a segment in the ellipse's own frame: where it starts, its step, and the rim point whose tangent runs along it
        /// on its side, with how far the line lies from the centre in the frame where the ellipse is the unit circle.
        /// </summary>
        private static void Across(
            GeoEllipse2 ellipse,
            GeoLine2 line,
            double length,
            out double along0,
            out double across0,
            out double alongStep,
            out double acrossStep,
            out double sideAlong,
            out double sideAcross,
            out double side)
        {
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            GeoVector2 major = ellipse.MajorAxis;
            GeoVector2 direction = line.Direction;

            Frame(ellipse, line.StartPoint, out along0, out across0);
            alongStep = direction.X * major.X + direction.Y * major.Y;
            acrossStep = direction.Y * major.X - direction.X * major.Y;

            double du = alongStep / a;
            double dv = acrossStep / b;
            double speed = Math.Sqrt(du * du + dv * dv);
            double nu = -dv / speed;
            double nv = du / speed;

            side = (along0 / a) * nu + (across0 / b) * nv;

            if (side < 0.0)
            {
                nu = -nu;
                nv = -nv;
                side = -side;
            }

            sideAlong = a * nu;
            sideAcross = b * nv;
        }

        /// <summary>
        /// Adds the point at a parameter of a segment, when the parameter falls on it within the tolerance.
        /// </summary>
        private static void OnSegment(List<GeoPoint2> points, GeoLine2 line, double parameter, double length, Tolerance tolerance)
        {
            double slack = tolerance.EqualPoint / length;

            if (parameter < -slack || parameter > 1.0 + slack)
            {
                return;
            }

            points.Add(line.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, parameter))));
        }

        /// <summary>
        /// Says whether an ellipse and a circle draw the same curve within the tolerance.
        /// </summary>
        private static bool OnEachOther(GeoEllipse2 ellipse, GeoCircle2 circle, Tolerance tolerance)
        {
            return ellipse.Center.IsEqualTo(circle.Center, tolerance)
                && Math.Abs(ellipse.MajorRadius - circle.Radius) <= tolerance.EqualPoint
                && Math.Abs(ellipse.MinorRadius - circle.Radius) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// The shape another rim is measured against: a circle, or an ellipse.
        /// </summary>
        private readonly struct Other
        {
            private Other(bool isCircle, GeoPoint2 center, double radius, GeoEllipse2 ellipse)
            {
                IsCircle = isCircle;
                Center = center;
                Radius = radius;
                Ellipse = ellipse;
            }

            internal bool IsCircle { get; }

            internal GeoPoint2 Center { get; }

            internal double Radius { get; }

            internal GeoEllipse2 Ellipse { get; }

            internal static Other Of(GeoCircle2 circle) => new Other(true, circle.Center, circle.Radius, default(GeoEllipse2));

            internal static Other Of(GeoEllipse2 ellipse) => new Other(false, ellipse.Center, 0.0, ellipse);

            /// <summary>
            /// Gets the distance from a point to the rim of the shape.
            /// </summary>
            internal double GapTo(GeoPoint2 point)
            {
                if (IsCircle)
                {
                    return Math.Abs(Center.DistanceTo(point) - Radius);
                }

                Frame(Ellipse, point, out double along, out double across);
                return Gap(Ellipse, along, across);
            }

            /// <summary>
            /// Gets the point of the shape's rim nearest a point.
            /// </summary>
            internal GeoPoint2 Nearest(GeoPoint2 point)
            {
                return IsCircle ? Outward(Center, Radius, point) : GetClosestPointOnBoundary(Ellipse, point);
            }

            /// <summary>
            /// Gets the shape's own equation at a point, read from its own centre so that it keeps its digits by the rim,
            /// and how fast it changes along a direction: for a circle the distance from the centre less the radius, for
            /// an ellipse u² + v² - 1 in its unit frame.
            /// </summary>
            internal double Level(GeoPoint2 point, double towardX, double towardY, out double rate)
            {
                double dx = point.X - Center.X;
                double dy = point.Y - Center.Y;

                if (IsCircle)
                {
                    double reach = Math.Sqrt(dx * dx + dy * dy);
                    rate = reach > 0.0 ? (dx * towardX + dy * towardY) / reach : 0.0;
                    return reach - Radius;
                }

                GeoVector2 m = Ellipse.MajorAxis;
                double a = Ellipse.MajorRadius;
                double b = Ellipse.MinorRadius;
                double u = (dx * m.X + dy * m.Y) / a;
                double v = (dy * m.X - dx * m.Y) / b;
                double du = (towardX * m.X + towardY * m.Y) / a;
                double dv = (towardY * m.X - towardX * m.Y) / b;

                rate = 2.0 * (u * du + v * dv);
                return u * u + v * v - 1.0;
            }
        }

        /// <summary>
        /// Polishes a crossing found on the wave by Newton's method on the other shape's own equation, which keeps the digits
        /// the wave's expanded terms lose when the shape is small and far from the centre, within the bracket it was found in.
        /// </summary>
        private static double Settle(GeoEllipse2 ellipse, Other other, double angle, double low, double high)
        {
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            GeoVector2 m = ellipse.MajorAxis;
            double best = angle;
            double bestValue = double.PositiveInfinity;

            for (int step = 0; step < 4; step++)
            {
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                double alongRate = -a * sin;
                double acrossRate = b * cos;
                double value = other.Level(
                    OnRim(ellipse, a * cos, b * sin),
                    alongRate * m.X - acrossRate * m.Y,
                    alongRate * m.Y + acrossRate * m.X,
                    out double rate);

                if (Math.Abs(value) < bestValue)
                {
                    best = angle;
                    bestValue = Math.Abs(value);
                }
                else
                {
                    break;
                }

                if (value == 0.0 || rate == 0.0)
                {
                    break;
                }

                double next = angle - value / rate;

                if (!(next > low && next < high))
                {
                    break;
                }

                angle = next;
            }

            return best;
        }

        /// <summary>
        /// Gets the equation of an ellipse, u² + v² - 1 in its own unit frame, read along a curve
        /// K + A cos t U + B sin t V, where V is U turned a quarter counter-clockwise.
        /// </summary>
        private static Wave Along(GeoPoint2 center, GeoVector2 axis, double major, double minor, GeoEllipse2 ellipse)
        {
            GeoVector2 m = ellipse.MajorAxis;
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double wx = center.X - ellipse.Center.X;
            double wy = center.Y - ellipse.Center.Y;

            // The curve's own axes, read along the ellipse's major and minor axes.
            double uAlong = axis.X * m.X + axis.Y * m.Y;
            double uAcross = axis.Y * m.X - axis.X * m.Y;
            double vAlong = -uAcross;
            double vAcross = uAlong;

            double alpha0 = (wx * m.X + wy * m.Y) / a;
            double alpha1 = major * uAlong / a;
            double alpha2 = minor * vAlong / a;
            double beta0 = (wy * m.X - wx * m.Y) / b;
            double beta1 = major * uAcross / b;
            double beta2 = minor * vAcross / b;

            return new Wave(
                alpha0 * alpha0 + beta0 * beta0 + 0.5 * (alpha1 * alpha1 + alpha2 * alpha2 + beta1 * beta1 + beta2 * beta2) - 1.0,
                2.0 * (alpha0 * alpha1 + beta0 * beta1),
                2.0 * (alpha0 * alpha2 + beta0 * beta2),
                0.5 * ((alpha1 - alpha2) * (alpha1 + alpha2) + (beta1 - beta2) * (beta1 + beta2)),
                alpha1 * alpha2 + beta1 * beta2);
        }

        /// <summary>
        /// Gets |P(t) - c|² - r², the equation of a circle read along the rim of an ellipse; with no radius, the square of the
        /// distance from the rim to a point, whose turning points are the feet of the normals from it.
        /// </summary>
        private static Wave AlongCircle(GeoEllipse2 ellipse, GeoPoint2 center, double radius)
        {
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            GeoVector2 m = ellipse.MajorAxis;
            double wx = ellipse.Center.X - center.X;
            double wy = ellipse.Center.Y - center.Y;
            double wAlong = wx * m.X + wy * m.Y;
            double wAcross = wy * m.X - wx * m.Y;

            return new Wave(
                wx * wx + wy * wy + 0.5 * (a * a + b * b) - radius * radius,
                2.0 * a * wAlong,
                2.0 * b * wAcross,
                0.5 * (a - b) * (a + b),
                0.0);
        }

        /// <summary>
        /// Gets where the rim crosses or touches the curve whose equation along it is a wave.
        /// </summary>
        /// <remarks>
        /// Between two turning points the wave runs one way, so a change of sign there is one crossing, found to the last
        /// digit. A turning point with no crossing on either side is where the curve comes nearest without crossing; within
        /// the point tolerance it touches, at the point of the rim nearest the curve.
        /// </remarks>
        private static GeoPoint2[] Meet(GeoEllipse2 ellipse, Wave wave, Other other, Tolerance tolerance)
        {
            var turns = new double[4];
            int count = Turns(wave, turns);

            if (count == 0)
            {
                return Array.Empty<GeoPoint2>();
            }

            if (count == 1)
            {
                // A wave turns at least twice round; the turn opposite stands in for one the roots lost to rounding.
                turns[1] = turns[0] < Math.PI ? turns[0] + Math.PI : turns[0] - Math.PI;
                Array.Sort(turns, 0, 2);
                count = 2;
            }

            var values = new double[count];

            for (int i = 0; i < count; i++)
            {
                values[i] = wave.At(turns[i]);
            }

            var angles = new List<double>(4);
            var points = new List<GeoPoint2>(4);
            var crossedAfter = new bool[count];

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                double low = turns[i];
                double high = next > i ? turns[next] : turns[next] + FullTurn;

                if (values[i] == 0.0)
                {
                    angles.Add(low);
                    points.Add(GetPointAtAngle(ellipse, low));
                    crossedAfter[i] = true;
                    crossedAfter[(i + count - 1) % count] = true;
                }
                else if (values[next] != 0.0 && (values[i] < 0.0) != (values[next] < 0.0))
                {
                    double crossing = Settle(ellipse, other, Root(wave, low, high, values[i], values[next]), low, high);
                    angles.Add(crossing);
                    points.Add(GetPointAtAngle(ellipse, crossing));
                    crossedAfter[i] = true;
                }
            }

            for (int i = 0; i < count; i++)
            {
                int before = (i + count - 1) % count;

                if (values[i] == 0.0 || crossedAfter[i] || crossedAfter[before])
                {
                    continue;
                }

                double at = turns[i];
                double reach = other.GapTo(GetPointAtAngle(ellipse, at));

                // The equation of an ellipse turns a little off the point of the rim nearest it, so near a touching the
                // distance itself is brought down; a circle's equation turns exactly there.
                if (!other.IsCircle && reach <= 10.0 * tolerance.EqualPoint)
                {
                    int next = (i + 1) % count;
                    double low = at - Positive(at - turns[before]);
                    double high = at + Positive(turns[next] - at);
                    GeoEllipse2 rim = ellipse;
                    Other against = other;

                    at = Least(angle => against.GapTo(GetPointAtAngle(rim, angle)), low, at, high, out reach);
                }

                if (reach <= tolerance.EqualPoint)
                {
                    // Midway between the rim and the curve it touches, as two circles touch at a point between them.
                    GeoPoint2 onRim = GetPointAtAngle(ellipse, at);
                    GeoPoint2 onOther = other.Nearest(onRim);
                    angles.Add(at);
                    points.Add(new GeoPoint2(0.5 * (onRim.X + onOther.X), 0.5 * (onRim.Y + onOther.Y)));
                }
            }

            return Gathered(angles, points, tolerance);
        }

        /// <summary>
        /// Gets an angle as the part of a turn it runs forward, a whole turn for nought.
        /// </summary>
        private static double Positive(double angle)
        {
            double forward = angle - FullTurn * Math.Floor(angle / FullTurn);
            return forward > 0.0 ? forward : FullTurn;
        }

        /// <summary>
        /// Puts the points found round the rim in the order of their eccentric angles, ties broken on X and then Y, two no
        /// further apart than twice the tolerance made one at the middle of the chord between them.
        /// </summary>
        /// <remarks>
        /// Two circles whose crossings have a half chord within the tolerance give one point, at the middle of the chord, and
        /// the rule is the same here. A run of points each that near the one before is one point, midway between the first and
        /// the last; the run that ends the turn may carry on into the one that starts it.
        /// </remarks>
        private static GeoPoint2[] Gathered(List<double> angles, List<GeoPoint2> points, Tolerance tolerance)
        {
            int count = angles.Count;

            if (count == 0)
            {
                return Array.Empty<GeoPoint2>();
            }

            var wrapped = new double[count];
            var placed = new GeoPoint2[count];

            for (int i = 0; i < count; i++)
            {
                double angle = angles[i] - FullTurn * Math.Floor(angles[i] / FullTurn);
                wrapped[i] = angle < FullTurn ? angle : 0.0;
                placed[i] = points[i];
            }

            SortByAngle(wrapped, placed);

            double reach = 2.0 * tolerance.EqualPoint;
            double reachSquared = reach * reach;
            var firsts = new List<int>(count) { 0 };
            var lasts = new List<int>(count) { 0 };

            for (int i = 1; i < count; i++)
            {
                if (placed[i].GetDistanceSquaredTo(placed[i - 1]) <= reachSquared)
                {
                    lasts[lasts.Count - 1] = i;
                }
                else
                {
                    firsts.Add(i);
                    lasts.Add(i);
                }
            }

            bool wraps = firsts.Count > 1 && placed[0].GetDistanceSquaredTo(placed[count - 1]) <= reachSquared;
            int runs = wraps ? firsts.Count - 1 : firsts.Count;
            var middles = new double[runs];
            var gathered = new GeoPoint2[runs];

            for (int r = 0; r < runs; r++)
            {
                int first = wraps && r == 0 ? firsts[firsts.Count - 1] : firsts[r];
                int last = lasts[r];
                double from = wraps && r == 0 ? wrapped[first] - FullTurn : wrapped[first];
                double middle = 0.5 * (from + wrapped[last]);
                middle -= FullTurn * Math.Floor(middle / FullTurn);

                middles[r] = middle < FullTurn ? middle : 0.0;
                gathered[r] = first == last
                    ? placed[first]
                    : new GeoPoint2(0.5 * (placed[first].X + placed[last].X), 0.5 * (placed[first].Y + placed[last].Y));
            }

            SortByAngle(middles, gathered);
            return gathered;
        }

        /// <summary>
        /// Sorts points by their angles, ties broken on X and then Y.
        /// </summary>
        private static void SortByAngle(double[] angles, GeoPoint2[] points)
        {
            for (int i = 1; i < points.Length; i++)
            {
                for (int j = i; j > 0 && (angles[j] < angles[j - 1] || (angles[j] == angles[j - 1] && ByCoordinates(points[j], points[j - 1]) < 0)); j--)
                {
                    double angle = angles[j];
                    angles[j] = angles[j - 1];
                    angles[j - 1] = angle;

                    GeoPoint2 point = points[j];
                    points[j] = points[j - 1];
                    points[j - 1] = point;
                }
            }
        }

        private static int ByCoordinates(GeoPoint2 first, GeoPoint2 second)
        {
            int byX = first.X.CompareTo(second.X);
            return byX != 0 ? byX : first.Y.CompareTo(second.Y);
        }

        #endregion
    }
}
