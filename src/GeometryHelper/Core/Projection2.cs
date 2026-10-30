using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static calculation methods for geometric projections of points and vectors onto lines,
    /// circles, rectangles, polygons, and polylines.
    /// <para>
    /// Projecting onto a closed shape lands on its <b>boundary</b>: the circumference of a circle, or one
    /// of the edges of a rectangle, polygon, or polyline. That holds even when the point is already inside,
    /// which is carried out to the nearest edge rather than returned unchanged. The geometry types expose
    /// the same operation as <c>GetClosestPointOnBoundary</c>.
    /// </para>
    /// <para>
    /// <see cref="Distance2"/> does not follow this rule at all: it treats a closed shape as a filled
    /// region, so the distance to a point inside it is zero.
    /// </para>
    /// <para>
    /// <c>GetShortestLineTo</c> extends the same idea to a pair of shapes and hands back the shortest
    /// segment bridging their two boundaries, with its start point on the first shape and its end point
    /// on the second. Overlapping shapes give a zero length segment sitting on a point where the two
    /// outlines cross. Where a whole stretch of pairs is equally close, as happens when two edges run
    /// alongside each other, the segment is anchored at the middle of that stretch rather than at either
    /// end of it.
    /// </para>
    /// <para>
    /// Against anything but a point, every side of a rectangle, a polygon or a polyline is joined as it is however short, as
    /// the distance measures it: at a hundredth, the polygon (0, 0), (10, 0), (10, 0.006), (0, 10) is joined to the segment
    /// from (10.03, 0.003) to (10.05, 0.003) by the line from (10, 0.003), 0.03 long, where read as its start its side 0.006
    /// long gave one from a corner 0.0301496 long. A point beside such a side is still carried to the side's start. Where two
    /// shapes come within the tolerance without meeting, the segment may be one of no length, a touch, and only there: two
    /// sides whose lines cross within the tolerance past the ends of both can be twice as far apart, and are joined by the
    /// gap. Two squares 100 by 100 whose corners are 0.00085 apart in x and in y are joined at a thousandth by the line
    /// between the corners, 0.0012 long. A segment you give no longer than the tolerance is read as its start where it does
    /// not truly meet the other shape, and is joined from there, as a point is.
    /// </para>
    /// <para>
    /// The tolerance is used for the projections themselves and for the endpoint slack when deciding
    /// whether two edges meet. It is deliberately not used as a cutoff for the edge scans: those stop
    /// early only on an exact zero, so widening the tolerance cannot make a search settle for a nearer
    /// edge it has not looked at yet.
    /// </para>
    /// </summary>
    public static partial class Projection2
    {
        /// <summary>
        /// A tolerance of nought, for joining a segment read as its start to a shape as the segment it is.
        /// </summary>
        private static Tolerance Exactly => default(Tolerance);

        #region Point on Line

        /// <summary>
        /// Projects a point orthogonally onto a line segment using default tolerance, clamping the result
        /// to [StartPoint, EndPoint].
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The closest point on the line segment.</returns>
        public static GeoPoint2 ProjectToLine(GeoLine2 line, GeoPoint2 point)
        {
            return ProjectToLine(line, point, Tolerance.Global);
        }

        /// <summary>
        /// Projects a point orthogonally onto a line segment within tolerance, clamping the result to
        /// [StartPoint, EndPoint]. The result never lies beyond either endpoint.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The closest point on the line segment.</returns>
        public static GeoPoint2 ProjectToLine(GeoLine2 line, GeoPoint2 point, Tolerance tolerance)
        {
            double t = Parametrization2.GetParameterAtPoint(line, point, tolerance);
            if (t <= 0.0) return line.StartPoint;
            if (t >= 1.0) return line.EndPoint;
            return line.GetPointAtParameter(t);
        }

        #endregion

        #region Point on Circle

        /// <summary>
        /// Projects a point onto the circumference of a circle using default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The closest point on the circle boundary.</returns>
        public static GeoPoint2 ProjectToCircle(GeoCircle2 circle, GeoPoint2 point)
        {
            return ProjectToCircle(circle, point, Tolerance.Global);
        }

        /// <summary>
        /// Projects a point onto the circumference of a circle within tolerance. The result always lies on
        /// the circumference, including for points inside the circle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The closest point on the circle boundary.</returns>
        public static GeoPoint2 ProjectToCircle(GeoCircle2 circle, GeoPoint2 point, Tolerance tolerance)
        {
            GeoVector2 dir = circle.Center.GetVectorTo(point);
            if (!dir.TryGetNormal(out GeoVector2 normal, tolerance))
            {
                // If point is coincident with circle center, project to angle 0 on circumference
                return new GeoPoint2(circle.Center.X + circle.Radius, circle.Center.Y);
            }

            return circle.Center.Add(normal.Multiply(circle.Radius));
        }

        #endregion

        #region Point on Rectangle

        /// <summary>
        /// Projects a point onto the boundary of a rotated rectangle (GeoRectangle2 OBB). The result always
        /// lies on one of the four edges, including for points inside the rectangle.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The closest point on the rectangle boundary.</returns>
        public static GeoPoint2 ProjectToRectangle(GeoRectangle2 rect, GeoPoint2 point)
        {
            double dx = point.X - rect.Center.X;
            double dy = point.Y - rect.Center.Y;

            double cos = Math.Cos(rect.AngleRad);
            double sin = Math.Sin(rect.AngleRad);

            // Project point onto local rectangle coordinate system
            double localX = dx * cos + dy * sin;
            double localY = -dx * sin + dy * cos;

            double halfW = rect.Width * 0.5;
            double halfH = rect.Height * 0.5;

            if (Math.Abs(localX) > halfW || Math.Abs(localY) > halfH)
            {
                // Outside: clamping into the box lands on the nearest edge or corner.
                localX = Math.Max(-halfW, Math.Min(halfW, localX));
                localY = Math.Max(-halfH, Math.Min(halfH, localY));
            }
            else
            {
                // Inside: clamping would return the point unchanged, so push it out to whichever edge is
                // nearest instead. A point on the diagonal is equidistant from two edges; snapping to the
                // horizontal one keeps the choice deterministic.
                double toVerticalEdge = halfW - Math.Abs(localX);
                double toHorizontalEdge = halfH - Math.Abs(localY);

                if (toVerticalEdge < toHorizontalEdge)
                {
                    localX = localX >= 0.0 ? halfW : -halfW;
                }
                else
                {
                    localY = localY >= 0.0 ? halfH : -halfH;
                }
            }

            // Transform back to world space
            double worldX = rect.Center.X + localX * cos - localY * sin;
            double worldY = rect.Center.Y + localX * sin + localY * cos;

            return new GeoPoint2(worldX, worldY);
        }

        #endregion

        #region Point on Polygon

        /// <summary>
        /// Projects a point onto the boundary of a polygon using default tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The closest point on the polygon boundary.</returns>
        public static GeoPoint2 ProjectToPolygon(GeoPolygon2 poly, GeoPoint2 point)
        {
            return ProjectToPolygon(poly, point, Tolerance.Global);
        }

        /// <summary>
        /// Projects a point onto the boundary of a polygon within tolerance (finds the closest point on any
        /// edge). The result always lies on the boundary, including for points inside the polygon.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The closest point on the polygon boundary.</returns>
        /// <remarks>
        /// A side no longer than the tolerance offers its start, as a segment that short does on its own, so beside it the
        /// closest point can be further than the distance by up to the side's length: at a hundredth, the polygon (0, 0),
        /// (10, 0), (10, 0.006), (0, 10) is 0.03 from (10.03, 0.003), and the closest point is (10, 0), 0.0301496 away.
        /// </remarks>
        public static GeoPoint2 ProjectToPolygon(GeoPolygon2 poly, GeoPoint2 point, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            double minDistanceSq = double.MaxValue;
            GeoPoint2 closestPoint = poly[0];

            for (int i = 0; i < poly.EdgeCount; i++)
            {
                GeoLine2 edge = poly.GetEdgeAt(i);
                GeoPoint2 proj = ProjectToLine(edge, point, tolerance);
                double dSq = Distance2.GetDistanceSquaredTo(point, proj);
                if (dSq < minDistanceSq)
                {
                    minDistanceSq = dSq;
                    closestPoint = proj;
                }
            }

            return closestPoint;
        }

        #endregion

        #region Point on Polyline

        /// <summary>
        /// Gets the point of the boundary of a face nearest another point.
        /// </summary>
        public static GeoPoint2 ProjectToFace(GeoFace2 face, GeoPoint2 point) => ProjectToFace(face, point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the boundary of a face nearest another point, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point">The point; it may lie on the material, in a hole, or off the face altogether.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The nearest point of the outline or of a hole rim, whichever is nearer.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <remarks>
        /// The boundary of a face is its outline <b>and</b> the rim of every hole, so a point sitting in a
        /// hole is answered with a point of that rim. As with a polygon, the answer is always on the boundary,
        /// which is where this parts company with <see cref="Distance2"/>: that reads a face as filled and
        /// calls a point on the material nought away.
        /// </remarks>
        public static GeoPoint2 ProjectToFace(GeoFace2 face, GeoPoint2 point, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            GeoPoint2 best = ProjectToPolygon(face.Boundary, point, tolerance);
            double reach = point.DistanceTo(best);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                GeoPoint2 candidate = ProjectToPolygon(hole, point, tolerance);
                double distance = point.DistanceTo(candidate);

                if (distance < reach)
                {
                    reach = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Projects a point orthogonally onto a polyline using default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The closest point on the polyline.</returns>
        public static GeoPoint2 ProjectToPolyline(GeoPolyline2 polyline, GeoPoint2 point)
        {
            return ProjectToPolyline(polyline, point, Tolerance.Global);
        }

        /// <summary>
        /// Projects a point orthogonally onto a polyline within tolerance (finds the closest point across
        /// all segments). The result always lies on the path.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The closest point on the polyline.</returns>
        /// <remarks>
        /// A leg no longer than the tolerance offers its start, as a segment that short does on its own, so beside it the
        /// closest point can be further than the distance by up to the leg's length: at a hundredth, the polyline (0, 0),
        /// (10, 0), (10, 0.006), (0, 0.006) is 0.03 from (10.03, 0.003), and the closest point is (10, 0), 0.0301496 away.
        /// </remarks>
        public static GeoPoint2 ProjectToPolyline(GeoPolyline2 polyline, GeoPoint2 point, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double minDistanceSq = double.MaxValue;
            GeoPoint2 closestPoint = polyline[0];

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 edge = polyline.GetEdgeAt(i);
                GeoPoint2 proj = ProjectToLine(edge, point, tolerance);
                double dSq = Distance2.GetDistanceSquaredTo(point, proj);
                if (dSq < minDistanceSq)
                {
                    minDistanceSq = dSq;
                    closestPoint = proj;
                }
            }

            return closestPoint;
        }

        #endregion

        #region Vector Projection2

        /// <summary>
        /// Projects a vector onto another axis vector using default tolerance.
        /// </summary>
        /// <param name="vector">The vector to project.</param>
        /// <param name="axis">The axis vector.</param>
        /// <returns>The vector projection along the axis.</returns>
        public static GeoVector2 Project(GeoVector2 vector, GeoVector2 axis)
        {
            return Project(vector, axis, Tolerance.Global);
        }

        /// <summary>
        /// Projects a vector onto another axis vector within tolerance.
        /// </summary>
        /// <param name="vector">The vector to project.</param>
        /// <param name="axis">The axis vector.</param>
        /// <param name="tolerance">The tolerance used to detect a zero-length axis.</param>
        /// <returns>The vector projection along the axis, or the zero vector for a degenerate axis.</returns>
        public static GeoVector2 Project(GeoVector2 vector, GeoVector2 axis, Tolerance tolerance)
        {
            double lenSq = axis.LengthSquared;
            if (lenSq <= tolerance.EqualVector * tolerance.EqualVector)
            {
                return GeoVector2.Zero;
            }

            double scale = vector.DotProduct(axis) / lenSq;
            return axis.Multiply(scale);
        }

        #endregion

        #region Closest Segment From a Curve to a Point

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to <paramref name="point"/> using default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is <paramref name="point"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPoint2 point) => GetShortestLineTo(line, point, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to <paramref name="point"/> within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A <see cref="GeoLine2"/> from the point <see cref="ProjectToLine(GeoLine2, GeoPoint2, Tolerance)"/> gives to
        /// <paramref name="point"/>, so it is exactly as long as <see cref="Distance2.DistanceTo(GeoLine2, GeoPoint2)"/>. From the
        /// segment (0, 0) to (100, 0) to the point (130, 40), it runs from (100, 0) and is 50 long.
        /// </returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPoint2 point, Tolerance tolerance) => new GeoLine2(ProjectToLine(line, point, tolerance), point);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the path of <paramref name="polyline"/> to <paramref name="point"/> using default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="polyline"/> and end point is <paramref name="point"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoPoint2 point) => GetShortestLineTo(polyline, point, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the path of <paramref name="polyline"/> to <paramref name="point"/> within tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A <see cref="GeoLine2"/> from the point <see cref="ProjectToPolyline(GeoPolyline2, GeoPoint2, Tolerance)"/> gives to
        /// <paramref name="point"/>, so it is as long as <see cref="Distance2.DistanceTo(GeoPolyline2, GeoPoint2)"/> but beside
        /// a leg no longer than the tolerance, which carries the point to the leg's start: at a hundredth, the polyline (0, 0),
        /// (10, 0), (10, 0.006), (0, 0.006) is 0.03 from (10.03, 0.003), and the line runs from (10, 0), 0.0301496 long. Only
        /// the path counts: a point in the mouth of a horseshoe gets the line out to the nearest leg, never one of no length.
        /// Where two legs are equally near, the first of them along the polyline is taken.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoPoint2 point, Tolerance tolerance) => new GeoLine2(ProjectToPolyline(polyline, point, tolerance), point);

        #endregion

        #region Closest Segment Between Line and Shapes

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line1"/> to a point on <paramref name="line2"/> using default tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line1"/> and end point is on <paramref name="line2"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line1, GeoLine2 line2)
        {
            return GetShortestLineTo(line1, line2, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line1"/> to a point on <paramref name="line2"/> within tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line1"/> and end point is on <paramref name="line2"/>.</returns>
        /// <remarks>
        /// Two segments whose lines cross within the tolerance past the end of either are read as touching, and the segment
        /// has no length; past the ends of both they can be up to twice the tolerance apart, and touch only where they come
        /// within it. At a thousandth, the segment from the origin to (10, 0) and the one leaving (10.00135, 0.00078) at 60
        /// degrees, whose lines cross 0.0009 past the end of each, are joined by the line between those two ends, 0.00156
        /// long. A segment no longer than the point tolerance is read as its start where it does not truly meet the other,
        /// as <see cref="Distance2.DistanceTo(GeoLine2, GeoLine2, Tolerance)"/> reads it: at a thousandth, the line from the
        /// segment from the origin to (10, 0) to the one from (5, 0.0015) to (5, 0.0006) runs to (5, 0.0015).
        /// </remarks>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance)
        {
            if (Stub2.ReadsAsStart(line1, tolerance) || Stub2.ReadsAsStart(line2, tolerance))
            {
                return GetShortestLineToStub(line1, line2, tolerance);
            }

            return GetShortestLineTo(line1, line2, tolerance, out _);
        }

        /// <summary>
        /// Finds the shortest segment between two segments, one or both no longer than the tolerance and read as their
        /// starts unless the two truly meet, as <see cref="Distance2.DistanceTo(GeoLine2, GeoLine2, Tolerance)"/> reads them.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static GeoLine2 GetShortestLineToStub(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance)
        {
            GeoLine2 asItIs = GetShortestLineTo(line1, line2, Exactly, out _);

            if (Stub2.Meets(asItIs.Length, line1, line2))
            {
                return asItIs;
            }

            bool short1 = Stub2.ReadsAsStart(line1, tolerance);
            bool short2 = Stub2.ReadsAsStart(line2, tolerance);

            return short1 && short2 ? new GeoLine2(line1.StartPoint, line2.StartPoint)
                : short1 ? GetShortestLineTo(line2, line1.StartPoint, tolerance).Reverse()
                : GetShortestLineTo(line1, line2.StartPoint, tolerance);
        }

        /// <summary>
        /// Same as <see cref="GetShortestLineTo(GeoLine2, GeoLine2, Tolerance)"/>, but also reports how long
        /// the stretch of equally close pairs is.
        /// </summary>
        /// <remarks>
        /// Two segments running alongside each other are equally close all along their overlap, and every
        /// pair in it is a valid answer. The edge loops use this length to break ties: where a face
        /// contact and a corner contact are the same distance away, the face is the one a leader line is
        /// expected to use, and only the face reports a non-zero extent.
        /// </remarks>
        private static GeoLine2 GetShortestLineTo(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance, out double contactExtent)
        {
            contactExtent = 0.0;

            if (TryGetCrossingPoint(line1, line2, tolerance, true, out GeoPoint2 crossing))
            {
                return new GeoLine2(crossing, crossing);
            }

            // Two segments that do not cross attain their minimum with at least one of the four
            // endpoints involved, so projecting each endpoint onto the other segment covers every case.
            GeoPoint2 p1A = ProjectToLine(line1, line2.StartPoint, tolerance);
            GeoPoint2 p2A = line2.StartPoint;
            double dSqA = Distance2.GetDistanceSquaredTo(p1A, p2A);

            GeoPoint2 p1B = ProjectToLine(line1, line2.EndPoint, tolerance);
            GeoPoint2 p2B = line2.EndPoint;
            double dSqB = Distance2.GetDistanceSquaredTo(p1B, p2B);

            GeoPoint2 p1C = line1.StartPoint;
            GeoPoint2 p2C = ProjectToLine(line2, line1.StartPoint, tolerance);
            double dSqC = Distance2.GetDistanceSquaredTo(p1C, p2C);

            GeoPoint2 p1D = line1.EndPoint;
            GeoPoint2 p2D = ProjectToLine(line2, line1.EndPoint, tolerance);
            double dSqD = Distance2.GetDistanceSquaredTo(p1D, p2D);

            double minSq = dSqA;
            GeoPoint2 bestP1 = p1A;
            GeoPoint2 bestP2 = p2A;

            if (dSqB < minSq)
            {
                minSq = dSqB;
                bestP1 = p1B;
                bestP2 = p2B;
            }

            if (dSqC < minSq)
            {
                minSq = dSqC;
                bestP1 = p1C;
                bestP2 = p2C;
            }

            if (dSqD < minSq)
            {
                minSq = dSqD;
                bestP1 = p1D;
                bestP2 = p2D;
            }

            // Segments running alongside each other have a whole interval of equally close pairs, and
            // the four candidates above always land on one end of it. Take the middle of the overlap
            // instead, which is where a leader line drawn between the two is expected to sit, but only
            // once it is confirmed to be just as close, so a merely near-parallel pair keeps its exact
            // endpoint answer.
            if (TryGetOverlapMidSegment(line1, line2, tolerance, out GeoLine2 midSegment, out double overlapLength))
            {
                double midSq = Distance2.GetDistanceSquaredTo(midSegment.StartPoint, midSegment.EndPoint);
                if (midSq <= minSq + minSq * 1E-9 + 1E-18)
                {
                    contactExtent = overlapLength;
                    return midSegment;
                }
            }

            return new GeoLine2(bestP1, bestP2);
        }

        /// <summary>
        /// Ranks one candidate segment against the best one found so far, by distance first and by
        /// contact extent when the two are the same distance apart.
        /// </summary>
        private static bool IsBetterCandidate(double distanceSquared, double contactExtent, double bestDistanceSquared, double bestContactExtent)
        {
            // Squared distances are compared, so the slack has to be relative to keep the comparison
            // meaningful at every scale.
            double slack = bestDistanceSquared * 1E-9 + 1E-18;

            if (distanceSquared < bestDistanceSquared - slack)
            {
                return true;
            }

            if (distanceSquared > bestDistanceSquared + slack)
            {
                return false;
            }

            return contactExtent > bestContactExtent;
        }

        /// <summary>
        /// Reports whether two segments cross, and where.
        /// </summary>
        /// <remarks>
        /// This deliberately does not call <see cref="Intersection2.TryIntersectWith(GeoLine2, GeoLine2, out GeoPoint2, Tolerance)"/>,
        /// which treats any pair meeting at less than the angular tolerance as parallel and reports no
        /// intersection. That is the right call when the intersection point itself is wanted, because a
        /// near-parallel crossing pins it down very poorly. Here only the distance matters and it is
        /// exactly zero, so applying the angular gate would make a crossing pair report the gap between
        /// their endpoints instead - an error that grows with segment length.
        /// <para>
        /// With <paramref name="pastEnds"/> a crossing of the two lines up to the point tolerance past the end of either
        /// counts, as it does for that method, and the point is clamped onto the first: the shortest line reads two
        /// segments meeting so as touching. Past the ends of both it counts only where the two come within the tolerance.
        /// Without it the crossing has to lie on both, which is how the distance and the collision read them: the segment
        /// ending at (10, 0) and the one leaving (10.00135, 0.00078) at 60 degrees, whose lines cross at (10.0009, 0), cross
        /// nowhere, and are 0.00156 apart, too far for a touch either way.
        /// </para>
        /// </remarks>
        internal static bool TryGetCrossingPoint(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance, bool pastEnds, out GeoPoint2 crossing)
        {
            crossing = new GeoPoint2(0.0, 0.0);

            GeoVector2 d1 = line1.Direction;
            GeoVector2 d2 = line2.Direction;

            double len1 = d1.Length;
            double len2 = d2.Length;

            // A degenerate segment has no direction, so no crossing parameter can be derived.
            if (len1 <= tolerance.EqualPoint || len2 <= tolerance.EqualPoint)
            {
                return false;
            }

            double denom = d1.CrossProduct(d2);

            // Guards the division only, and sits at machine precision rather than at a geometric
            // threshold: below it the two directions agree to about 1E-12 rad, where the endpoint
            // candidates already answer to full accuracy anyway.
            if (Math.Abs(denom) <= 1E-12 * len1 * len2)
            {
                return false;
            }

            GeoVector2 qMinusP = line1.StartPoint.GetVectorTo(line2.StartPoint);
            double t = qMinusP.CrossProduct(d2) / denom;
            double u = qMinusP.CrossProduct(d1) / denom;

            // Same endpoint slack as Intersection2.TryIntersectWith, where it is asked for: the allowance is a
            // distance, so it has to be carried into each segment's parameter space before it can be applied.
            double tTolerance = pastEnds ? tolerance.EqualPoint / len1 : 0.0;
            double uTolerance = pastEnds ? tolerance.EqualPoint / len2 : 0.0;

            if (t < -tTolerance || t > 1.0 + tTolerance || u < -uTolerance || u > 1.0 + uTolerance)
            {
                return false;
            }

            // Past the end of one only, the end lies within the tolerance of the other. Past the ends of both, the two can be
            // up to twice the tolerance apart, and touch only where they truly come within it.
            if ((t < 0.0 || t > 1.0) && (u < 0.0 || u > 1.0) && !(GapPastTheEnds(line1, line2) <= tolerance.EqualPoint))
            {
                return false;
            }

            crossing = line1.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, t)));
            return true;
        }

        /// <summary>
        /// Gets the exact distance between two segments whose lines cross past an end of both, so that it is the distance
        /// from an end of one to the other.
        /// </summary>
        private static double GapPastTheEnds(GeoLine2 line1, GeoLine2 line2)
        {
            Tolerance exactly = default(Tolerance);

            return Math.Min(
                Math.Min(Distance2.DistanceTo(line1, line2.StartPoint, exactly), Distance2.DistanceTo(line1, line2.EndPoint, exactly)),
                Math.Min(Distance2.DistanceTo(line2, line1.StartPoint, exactly), Distance2.DistanceTo(line2, line1.EndPoint, exactly)));
        }

        /// <summary>
        /// Builds the candidate segment anchored at the middle of the stretch over which
        /// <paramref name="line2"/> runs alongside <paramref name="line1"/>, or returns false when their
        /// projections onto <paramref name="line1"/> do not overlap at all.
        /// </summary>
        private static bool TryGetOverlapMidSegment(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance, out GeoLine2 segment, out double overlapLength)
        {
            segment = new GeoLine2(line1.StartPoint, line2.StartPoint);
            overlapLength = 0.0;

            double tStart = Parametrization2.GetParameterAtPoint(line1, line2.StartPoint, tolerance);
            double tEnd = Parametrization2.GetParameterAtPoint(line1, line2.EndPoint, tolerance);

            double low = Math.Max(0.0, Math.Min(tStart, tEnd));
            double high = Math.Min(1.0, Math.Max(tStart, tEnd));

            if (low > high)
            {
                return false;
            }

            GeoPoint2 onLine1 = line1.GetPointAtParameter((low + high) * 0.5);
            segment = new GeoLine2(onLine1, ProjectToLine(line2, onLine1, tolerance));
            overlapLength = (high - low) * line1.Length;
            return true;
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the circumference of <paramref name="circle"/> using default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="circle">The circle.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on the circumference of <paramref name="circle"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoCircle2 circle)
        {
            return GetShortestLineTo(line, circle, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the circumference of <paramref name="circle"/> within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on the circumference of <paramref name="circle"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoCircle2 circle, Tolerance tolerance)
        {
            if (Stub2.ReadsAsStart(line, tolerance))
            {
                return Stub2.Nearest(line, GetShortestLineTo(line, circle, Stub2.AsItIs(line, tolerance)), GetShortestLineTo(circle, line.StartPoint, tolerance).Reverse());
            }

            if (Intersection2.TryIntersectWith(circle, line, out GeoPoint2[] intersections, tolerance) && intersections.Length > 0)
            {
                return new GeoLine2(intersections[0], intersections[0]);
            }

            // If line is outside circle
            GeoPoint2 pOnLine = ProjectToLine(line, circle.Center, tolerance);
            double distCenterToLineSq = Distance2.GetDistanceSquaredTo(circle.Center, pOnLine);

            if (distCenterToLineSq >= circle.Radius * circle.Radius)
            {
                GeoPoint2 pOnCircle = ProjectToCircle(circle, pOnLine, tolerance);
                return new GeoLine2(pOnLine, pOnCircle);
            }

            // Line is strictly inside circle: the point on line closest to circumference is the endpoint farthest from Center
            double distStartSq = Distance2.GetDistanceSquaredTo(circle.Center, line.StartPoint);
            double distEndSq = Distance2.GetDistanceSquaredTo(circle.Center, line.EndPoint);

            GeoPoint2 closestInside = distStartSq >= distEndSq ? line.StartPoint : line.EndPoint;
            GeoPoint2 pCircle = ProjectToCircle(circle, closestInside, tolerance);
            return new GeoLine2(closestInside, pCircle);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the boundary of <paramref name="rect"/> using default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="rect">The rectangle.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on the boundary of <paramref name="rect"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoRectangle2 rect)
        {
            return GetShortestLineTo(line, rect, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the boundary of <paramref name="rect"/> within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on the boundary of <paramref name="rect"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoRectangle2 rect, Tolerance tolerance)
        {
            if (Stub2.ReadsAsStart(line, tolerance))
            {
                return Stub2.Nearest(line, GetShortestLineTo(line, rect, Stub2.AsItIs(line, tolerance)), GetShortestLineTo(rect, line.StartPoint, tolerance).Reverse());
            }

            GeoLine2 bestSegment = new GeoLine2(line.StartPoint, line.StartPoint);
            double minDistanceSq = 0.0;
            double bestExtent = 0.0;
            bool found = false;

            GeoLine2[] edges = rect.GetEdges();
            for (int i = 0; i < edges.Length; i++)
            {
                GeoLine2 seg = GetShortestLineTo(line, edges[i], ArcChain2.ForSide(edges[i], tolerance), out double extent);
                double dSq = Distance2.GetDistanceSquaredTo(seg.StartPoint, seg.EndPoint);
                if (!found || IsBetterCandidate(dSq, extent, minDistanceSq, bestExtent))
                {
                    found = true;
                    minDistanceSq = dSq;
                    bestExtent = extent;
                    bestSegment = seg;

                    // A crossing yields an exactly degenerate segment, so this stops the scan as soon as
                    // the two shapes are known to meet. The test is on zero rather than on the tolerance
                    // so that a loose tolerance cannot cut the search short and leave a nearer edge unseen.
                    if (minDistanceSq <= 0.0)
                    {
                        break;
                    }
                }
            }

            return bestSegment;
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the polyline path of <paramref name="polyline"/> using default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="polyline">The polyline.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on <paramref name="polyline"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPolyline2 polyline)
        {
            return GetShortestLineTo(line, polyline, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the polyline path of <paramref name="polyline"/> within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on <paramref name="polyline"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            if (Stub2.ReadsAsStart(line, tolerance))
            {
                return Stub2.Nearest(line, GetShortestLineTo(line, polyline, Stub2.AsItIs(line, tolerance)), GetShortestLineTo(polyline, line.StartPoint, tolerance).Reverse());
            }

            GeoLine2 bestSegment = new GeoLine2(line.StartPoint, line.StartPoint);
            double minDistanceSq = 0.0;
            double bestExtent = 0.0;
            bool found = false;

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 edge = polyline.GetEdgeAt(i);
                GeoLine2 seg = GetShortestLineTo(line, edge, ArcChain2.ForSide(edge, tolerance), out double extent);
                double dSq = Distance2.GetDistanceSquaredTo(seg.StartPoint, seg.EndPoint);
                if (!found || IsBetterCandidate(dSq, extent, minDistanceSq, bestExtent))
                {
                    found = true;
                    minDistanceSq = dSq;
                    bestExtent = extent;
                    bestSegment = seg;

                    if (minDistanceSq <= 0.0)
                    {
                        break;
                    }
                }
            }

            return bestSegment;
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the boundary of <paramref name="poly"/> using default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="poly">The polygon.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on the boundary of <paramref name="poly"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPolygon2 poly)
        {
            return GetShortestLineTo(line, poly, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the boundary of <paramref name="poly"/> within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="poly">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on the boundary of <paramref name="poly"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            if (Stub2.ReadsAsStart(line, tolerance))
            {
                return Stub2.Nearest(line, GetShortestLineTo(line, poly, Stub2.AsItIs(line, tolerance)), GetShortestLineTo(poly, line.StartPoint, tolerance).Reverse());
            }

            GeoLine2 bestSegment = new GeoLine2(line.StartPoint, line.StartPoint);
            double minDistanceSq = 0.0;
            double bestExtent = 0.0;
            bool found = false;

            for (int i = 0; i < poly.EdgeCount; i++)
            {
                GeoLine2 edge = poly.GetEdgeAt(i);
                GeoLine2 seg = GetShortestLineTo(line, edge, ArcChain2.ForSide(edge, tolerance), out double extent);
                double dSq = Distance2.GetDistanceSquaredTo(seg.StartPoint, seg.EndPoint);
                if (!found || IsBetterCandidate(dSq, extent, minDistanceSq, bestExtent))
                {
                    found = true;
                    minDistanceSq = dSq;
                    bestExtent = extent;
                    bestSegment = seg;

                    if (minDistanceSq <= 0.0)
                    {
                        break;
                    }
                }
            }

            return bestSegment;
        }

        #endregion

        #region Closest Segment Between Circle and Shapes

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to <paramref name="point"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPoint2 point) => GetShortestLineTo(circle, point, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to <paramref name="point"/> within tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance <see cref="ProjectToCircle(GeoCircle2, GeoPoint2, Tolerance)"/> tells the centre with.</param>
        /// <returns>
        /// A <see cref="GeoLine2"/> from the point <see cref="ProjectToCircle(GeoCircle2, GeoPoint2, Tolerance)"/> gives to
        /// <paramref name="point"/>. Like every shortest line in this class it runs from boundary to boundary, so a
        /// point inside is carried out to the nearest edge and the segment is as long as the depth to it, where
        /// <see cref="Distance2.DistanceTo(GeoCircle2, GeoPoint2)"/> reads the circle as filled and answers nought.
        /// Outside, it is as long as <see cref="Distance2.DistanceTo(GeoCircle2, GeoPoint2)"/>; on the boundary it has
        /// no length.
        /// From the circle of radius 50 round the origin, the point (30, 0) gets the segment from (50, 0), 20 long, and
        /// the centre gets the one from (50, 0).
        /// </returns>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPoint2 point, Tolerance tolerance) => new GeoLine2(ProjectToCircle(circle, point, tolerance), point);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on <paramref name="line"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoLine2 line) => GetShortestLineTo(circle, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on <paramref name="line"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(line, circle, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="c1"/> to a point on the circumference of <paramref name="c2"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 c1, GeoCircle2 c2) => GetShortestLineTo(c1, c2, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="c1"/> to a point on the circumference of <paramref name="c2"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 c1, GeoCircle2 c2, Tolerance tolerance)
        {
            if (Intersection2.TryIntersectWith(c1, c2, out GeoPoint2[] intersections, tolerance) && intersections.Length > 0)
            {
                return new GeoLine2(intersections[0], intersections[0]);
            }

            double distCenters = Distance2.DistanceTo(c1.Center, c2.Center);
            GeoVector2 dir = c1.Center.GetVectorTo(c2.Center);

            if (!dir.TryGetNormal(out GeoVector2 normal, tolerance))
            {
                normal = new GeoVector2(1.0, 0.0);
            }

            if (distCenters >= c1.Radius + c2.Radius)
            {
                GeoPoint2 p1 = c1.Center.Add(normal.Multiply(c1.Radius));
                GeoPoint2 p2 = c2.Center.Subtract(normal.Multiply(c2.Radius));
                return new GeoLine2(p1, p2);
            }
            else if (c1.Radius >= distCenters + c2.Radius)
            {
                GeoPoint2 p1 = c1.Center.Add(normal.Multiply(c1.Radius));
                GeoPoint2 p2 = c2.Center.Add(normal.Multiply(c2.Radius));
                return new GeoLine2(p1, p2);
            }
            else
            {
                GeoPoint2 p1 = c1.Center.Subtract(normal.Multiply(c1.Radius));
                GeoPoint2 p2 = c2.Center.Subtract(normal.Multiply(c2.Radius));
                return new GeoLine2(p1, p2);
            }
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the boundary of <paramref name="rect"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoRectangle2 rect) => GetShortestLineTo(circle, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the boundary of <paramref name="rect"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoRectangle2 rect, Tolerance tolerance) => GetShortestLineCircleToEdges(circle, rect.GetEdges(), tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the polyline path of <paramref name="polyline"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPolyline2 polyline) => GetShortestLineTo(circle, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the polyline path of <paramref name="polyline"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            return GetShortestLineCircleToEdges(circle, polyline.GetEdges(), tolerance);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the boundary of <paramref name="poly"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPolygon2 poly) => GetShortestLineTo(circle, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the boundary of <paramref name="poly"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            return GetShortestLineCircleToEdges(circle, poly.GetEdges(), tolerance);
        }

        #endregion

        #region Closest Segment Between Rectangle and Shapes

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to <paramref name="point"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPoint2 point) => GetShortestLineTo(rect, point, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to <paramref name="point"/>.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance; not read, since <see cref="ProjectToRectangle(GeoRectangle2, GeoPoint2)"/> takes none.</param>
        /// <returns>
        /// A <see cref="GeoLine2"/> from the point <see cref="ProjectToRectangle(GeoRectangle2, GeoPoint2)"/> gives to
        /// <paramref name="point"/>. Like every shortest line in this class it runs from boundary to boundary, so a
        /// point inside is carried out to the nearest edge and the segment is as long as the depth to it, where
        /// <see cref="Distance2.DistanceTo(GeoRectangle2, GeoPoint2)"/> reads the rectangle as filled and answers
        /// nought. Outside, it is as long as <see cref="Distance2.DistanceTo(GeoRectangle2, GeoPoint2)"/>; on the
        /// boundary it has no length.
        /// A point inside as near two edges, on the line at 45 degrees from a corner, is carried to the edge along the
        /// rectangle's width: inside the rectangle 200 by 100 about the origin, (75, 25) goes to (75, 50).
        /// </returns>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPoint2 point, Tolerance tolerance) => new GeoLine2(ProjectToRectangle(rect, point), point);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on <paramref name="line"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoLine2 line) => GetShortestLineTo(rect, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on <paramref name="line"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(line, rect, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the circumference of <paramref name="circle"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoCircle2 circle) => GetShortestLineTo(rect, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the circumference of <paramref name="circle"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(circle, rect, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="r1"/> to a point on the boundary of <paramref name="r2"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 r1, GeoRectangle2 r2) => GetShortestLineTo(r1, r2, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="r1"/> to a point on the boundary of <paramref name="r2"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 r1, GeoRectangle2 r2, Tolerance tolerance) => GetShortestLineBetweenEdgeSets(r1.GetEdges(), r2.GetEdges(), tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the polyline path of <paramref name="polyline"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPolyline2 polyline) => GetShortestLineTo(rect, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the polyline path of <paramref name="polyline"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            return GetShortestLineBetweenEdgeSets(rect.GetEdges(), polyline.GetEdges(), tolerance);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the boundary of <paramref name="poly"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPolygon2 poly) => GetShortestLineTo(rect, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the boundary of <paramref name="poly"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            return GetShortestLineBetweenEdgeSets(rect.GetEdges(), poly.GetEdges(), tolerance);
        }

        #endregion

        #region Closest Segment Between Polyline and Shapes

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on <paramref name="line"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoLine2 line) => GetShortestLineTo(polyline, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on <paramref name="line"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(line, polyline, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the circumference of <paramref name="circle"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoCircle2 circle) => GetShortestLineTo(polyline, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the circumference of <paramref name="circle"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(circle, polyline, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the boundary of <paramref name="rect"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoRectangle2 rect) => GetShortestLineTo(polyline, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the boundary of <paramref name="rect"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoRectangle2 rect, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(rect, polyline, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="p1"/> to a point on <paramref name="p2"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 p1, GeoPolyline2 p2) => GetShortestLineTo(p1, p2, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="p1"/> to a point on <paramref name="p2"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 p1, GeoPolyline2 p2, Tolerance tolerance)
        {
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (p2 == null) throw new ArgumentNullException(nameof(p2));
            return GetShortestLineBetweenEdgeSets(p1.GetEdges(), p2.GetEdges(), tolerance);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the boundary of <paramref name="poly"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoPolygon2 poly) => GetShortestLineTo(polyline, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the boundary of <paramref name="poly"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            return GetShortestLineBetweenEdgeSets(polyline.GetEdges(), poly.GetEdges(), tolerance);
        }

        #endregion

        #region Closest Segment Between Polygon and Shapes

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to <paramref name="point"/> using default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoPoint2 point) => GetShortestLineTo(poly, point, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to <paramref name="point"/> within tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A <see cref="GeoLine2"/> from the point <see cref="ProjectToPolygon(GeoPolygon2, GeoPoint2, Tolerance)"/> gives to
        /// <paramref name="point"/>. Like every shortest line in this class it runs from boundary to boundary, so a
        /// point inside is carried out to the nearest edge and the segment is as long as the depth to it, where
        /// <see cref="Distance2.DistanceTo(GeoPolygon2, GeoPoint2)"/> reads the polygon as filled and answers nought.
        /// Outside, by more than the tolerance, it is as long as <see cref="Distance2.DistanceTo(GeoPolygon2, GeoPoint2)"/>,
        /// which takes a point within the tolerance outside as in; on the boundary it has no length.
        /// Where two edges are equally near, the first of them round the polygon is taken. Inside the square 100 by 100 on
        /// the origin, the point (50, 30) gets the segment from (50, 0), 30 long. Beside a side no longer than the tolerance
        /// the point is carried to the side's start, so the segment can be longer than the distance by up to the side's
        /// length: at a hundredth, the polygon (0, 0), (10, 0), (10, 0.006), (0, 10) is 0.03 from (10.03, 0.003), and the
        /// segment runs from (10, 0), 0.0301496 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoPoint2 point, Tolerance tolerance) => new GeoLine2(ProjectToPolygon(poly, point, tolerance), point);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on <paramref name="line"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoLine2 line) => GetShortestLineTo(poly, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on <paramref name="line"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(line, poly, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on the circumference of <paramref name="circle"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoCircle2 circle) => GetShortestLineTo(poly, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on the circumference of <paramref name="circle"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(circle, poly, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on the boundary of <paramref name="rect"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoRectangle2 rect) => GetShortestLineTo(poly, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on the boundary of <paramref name="rect"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoRectangle2 rect, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(rect, poly, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on <paramref name="polyline"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoPolyline2 polyline) => GetShortestLineTo(poly, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on <paramref name="polyline"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoPolyline2 polyline, Tolerance tolerance)
        {
            GeoLine2 seg = GetShortestLineTo(polyline, poly, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="p1"/> to a point on the boundary of <paramref name="p2"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 p1, GeoPolygon2 p2) => GetShortestLineTo(p1, p2, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="p1"/> to a point on the boundary of <paramref name="p2"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 p1, GeoPolygon2 p2, Tolerance tolerance)
        {
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (p2 == null) throw new ArgumentNullException(nameof(p2));
            return GetShortestLineBetweenEdgeSets(p1.GetEdges(), p2.GetEdges(), tolerance);
        }

        #endregion

        #region Helper Methods for Closest Segments

        private static GeoLine2 GetShortestLineBetweenEdgeSets(IEnumerable<GeoLine2> edges1, IEnumerable<GeoLine2> edges2, Tolerance tolerance)
        {
            GeoLine2 bestSegment = default;
            double minDistanceSq = 0.0;
            double bestExtent = 0.0;
            bool found = false;

            foreach (var e1 in edges1)
            {
                foreach (var e2 in edges2)
                {
                    GeoLine2 seg = GetShortestLineTo(e1, e2, ArcChain2.ForSides(e1, e2, tolerance), out double extent);
                    double dSq = Distance2.GetDistanceSquaredTo(seg.StartPoint, seg.EndPoint);
                    if (!found || IsBetterCandidate(dSq, extent, minDistanceSq, bestExtent))
                    {
                        found = true;
                        minDistanceSq = dSq;
                        bestExtent = extent;
                        bestSegment = seg;

                        // A crossing yields an exactly degenerate segment, so this stops the scan as soon
                        // as the two outlines are known to meet. The test is on zero rather than on the
                        // tolerance so that a loose tolerance cannot cut the search short and leave a
                        // nearer edge pair unseen.
                        if (minDistanceSq <= 0.0)
                        {
                            return bestSegment;
                        }
                    }
                }
            }

            if (!found)
            {
                throw new ArgumentException("Cannot measure to a shape that has no edges.", nameof(edges1));
            }

            return bestSegment;
        }

        private static GeoLine2 GetShortestLineCircleToEdges(GeoCircle2 circle, IEnumerable<GeoLine2> edges, Tolerance tolerance)
        {
            GeoLine2 bestSegment = default;
            double minDistanceSq = 0.0;
            bool found = false;

            foreach (var edge in edges)
            {
                GeoLine2 seg = GetShortestLineTo(edge, circle, ArcChain2.ForSide(edge, tolerance));
                double dSq = Distance2.GetDistanceSquaredTo(seg.StartPoint, seg.EndPoint);
                if (!found || dSq < minDistanceSq)
                {
                    found = true;
                    minDistanceSq = dSq;
                    bestSegment = new GeoLine2(seg.EndPoint, seg.StartPoint);

                    if (minDistanceSq <= 0.0)
                    {
                        return bestSegment;
                    }
                }
            }

            if (!found)
            {
                throw new ArgumentException("Cannot measure to a shape that has no edges.", nameof(edges));
            }

            return bestSegment;
        }

        #endregion

        /// <summary>
        /// Gets the foot of the perpendicular from a point to the infinite line carrying a segment, using the
        /// default tolerance.
        /// </summary>
        /// <param name="line">The segment, read as the infinite line carrying it.</param>
        /// <param name="point">The point.</param>
        /// <returns>The point of the line closest to the given point, which may lie beyond either end.</returns>
        public static GeoPoint2 ProjectToInfiniteLine(GeoLine2 line, GeoPoint2 point) => ProjectToInfiniteLine(line, point, Tolerance.Global);

        /// <summary>
        /// Gets the foot of the perpendicular from a point to the infinite line carrying a segment, within a
        /// tolerance.
        /// </summary>
        /// <param name="line">The segment, read as the infinite line carrying it.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The point of the line closest to the given point, which may lie beyond either end.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <remarks>
        /// <see cref="ProjectToLine(GeoLine2, GeoPoint2)"/> keeps the answer within the segment; this one does
        /// not, which is what to use when the segment stands for the line it lies on.
        /// </remarks>
        public static GeoPoint2 ProjectToInfiniteLine(GeoLine2 line, GeoPoint2 point, Tolerance tolerance)
        {
            GeoVector2 along = line.StartPoint.GetVectorTo(line.EndPoint);
            double lengthSquared = along.LengthSquared;

            if (lengthSquared <= tolerance.EqualPoint * tolerance.EqualPoint)
            {
                throw new InvalidOperationException("A segment shorter than the tolerance has no direction, so it carries no line.");
            }

            double t = line.StartPoint.GetVectorTo(point).DotProduct(along) / lengthSquared;

            return line.StartPoint.Add(along.Multiply(t));
        }
        #region Chains that may curve

        /// <summary>
        /// Gets the point of a chain that may curve nearest a point.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2 ProjectToPolylineArc(GeoPolylineArc2 chain, GeoPoint2 point) => ProjectToPolylineArc(chain, point, Tolerance.Global);

        /// <summary>
        /// Gets the point of a chain that may curve nearest a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer lies on the arcs themselves, not on the chords that stand in for them elsewhere.
        /// A point within the tolerance of the centre of an arc is read as at the centre, which has no direction, and is carried to the arc's start; the distance to the point is the exact one, so the line can be longer than it, by at most twice the point's offset from the centre. At a tolerance of a thousandth, the point (0, -0.0009) and the arc of radius 50 about the origin from (-50, 0) to (50, 0) through (0, -50): the closest point is (-50, 0), 50.00000001 away, where the distance is 49.9991.
        /// A straight edge no longer than the tolerance offers its start, though the distance measures it as it is, so beside it the closest point can be further than the distance by up to the edge's length: at a hundredth, the loop (0, 0), (10, 0), (10, 0.006), (0, 10) is 0.03 from (10.03, 0.003), and the closest point is (10, 0), 0.0301496 away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2 ProjectToPolylineArc(GeoPolylineArc2 chain, GeoPoint2 point, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return ArcChain2.ClosestPoint(ArcChain2.EdgesOf(chain), point, tolerance);
        }

        /// <summary>
        /// Gets the point of a loop that may curve nearest a point.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2 ProjectToPolygonArc(GeoPolygonArc2 loop, GeoPoint2 point) => ProjectToPolygonArc(loop, point, Tolerance.Global);

        /// <summary>
        /// Gets the point of a loop that may curve nearest a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer lies on the arcs themselves, not on the chords that stand in for them elsewhere.
        /// A point within the tolerance of the centre of an arc is read as at the centre, which has no direction, and is carried to the arc's start; the distance to the point is the exact one, so the line can be longer than it, by at most twice the point's offset from the centre. At a tolerance of a thousandth, the point (0, -0.0009) and the arc of radius 50 about the origin from (-50, 0) to (50, 0) through (0, -50): the closest point is (-50, 0), 50.00000001 away, where the distance is 49.9991.
        /// A straight edge no longer than the tolerance offers its start, though the distance measures it as it is, so beside it the closest point can be further than the distance by up to the edge's length: at a hundredth, the loop (0, 0), (10, 0), (10, 0.006), (0, 10) is 0.03 from (10.03, 0.003), and the closest point is (10, 0), 0.0301496 away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2 ProjectToPolygonArc(GeoPolygonArc2 loop, GeoPoint2 point, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return ArcChain2.ClosestPoint(ArcChain2.EdgesOf(loop), point, tolerance);
        }

        #endregion

    }
}
