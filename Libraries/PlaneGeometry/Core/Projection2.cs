using System;
using System.Collections.Generic;
using CommonGeometry;
using PlaneGeometry.Geometry;

namespace PlaneGeometry.Core
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
    /// <c>GetClosestSegment</c> extends the same idea to a pair of shapes and hands back the shortest
    /// segment bridging their two boundaries, with its start point on the first shape and its end point
    /// on the second. Overlapping shapes give a zero length segment sitting on a point where the two
    /// outlines cross. Where a whole stretch of pairs is equally close, as happens when two edges run
    /// alongside each other, the segment is anchored at the middle of that stretch rather than at either
    /// end of it.
    /// </para>
    /// <para>
    /// The tolerance is used for the projections themselves and for the endpoint slack when deciding
    /// whether two edges meet. It is deliberately not used as a cutoff for the edge scans: those stop
    /// early only on an exact zero, so widening the tolerance cannot make a search settle for a nearer
    /// edge it has not looked at yet.
    /// </para>
    /// </summary>
    public static class Projection2
    {
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

        #region Closest Segment Between Line and Shapes

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line1"/> to a point on <paramref name="line2"/> using default tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line1"/> and end point is on <paramref name="line2"/>.</returns>
        public static GeoLine2 GetClosestSegment(GeoLine2 line1, GeoLine2 line2)
        {
            return GetClosestSegment(line1, line2, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line1"/> to a point on <paramref name="line2"/> within tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line1"/> and end point is on <paramref name="line2"/>.</returns>
        public static GeoLine2 GetClosestSegment(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance)
        {
            return GetClosestSegment(line1, line2, tolerance, out _);
        }

        /// <summary>
        /// Same as <see cref="GetClosestSegment(GeoLine2, GeoLine2, Tolerance)"/>, but also reports how long
        /// the stretch of equally close pairs is.
        /// </summary>
        /// <remarks>
        /// Two segments running alongside each other are equally close all along their overlap, and every
        /// pair in it is a valid answer. The edge loops use this length to break ties: where a face
        /// contact and a corner contact are the same distance away, the face is the one a leader line is
        /// expected to use, and only the face reports a non-zero extent.
        /// </remarks>
        private static GeoLine2 GetClosestSegment(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance, out double contactExtent)
        {
            contactExtent = 0.0;

            if (TryGetCrossingPoint(line1, line2, tolerance, out GeoPoint2 crossing))
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
        /// </remarks>
        internal static bool TryGetCrossingPoint(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance, out GeoPoint2 crossing)
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

            // Same endpoint slack as Intersection2.TryIntersectWith: the allowance is a distance, so it
            // has to be carried into each segment's parameter space before it can be applied.
            double tTolerance = tolerance.EqualPoint / len1;
            double uTolerance = tolerance.EqualPoint / len2;

            if (t < -tTolerance || t > 1.0 + tTolerance || u < -uTolerance || u > 1.0 + uTolerance)
            {
                return false;
            }

            crossing = line1.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, t)));
            return true;
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
        public static GeoLine2 GetClosestSegment(GeoLine2 line, GeoCircle2 circle)
        {
            return GetClosestSegment(line, circle, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the circumference of <paramref name="circle"/> within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on the circumference of <paramref name="circle"/>.</returns>
        public static GeoLine2 GetClosestSegment(GeoLine2 line, GeoCircle2 circle, Tolerance tolerance)
        {
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
        public static GeoLine2 GetClosestSegment(GeoLine2 line, GeoRectangle2 rect)
        {
            return GetClosestSegment(line, rect, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the boundary of <paramref name="rect"/> within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on the boundary of <paramref name="rect"/>.</returns>
        public static GeoLine2 GetClosestSegment(GeoLine2 line, GeoRectangle2 rect, Tolerance tolerance)
        {
            GeoLine2 bestSegment = new GeoLine2(line.StartPoint, line.StartPoint);
            double minDistanceSq = 0.0;
            double bestExtent = 0.0;
            bool found = false;

            GeoLine2[] edges = rect.GetEdges();
            for (int i = 0; i < edges.Length; i++)
            {
                GeoLine2 seg = GetClosestSegment(line, edges[i], tolerance, out double extent);
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
        public static GeoLine2 GetClosestSegment(GeoLine2 line, GeoPolyline2 polyline)
        {
            return GetClosestSegment(line, polyline, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the polyline path of <paramref name="polyline"/> within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on <paramref name="polyline"/>.</returns>
        public static GeoLine2 GetClosestSegment(GeoLine2 line, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            GeoLine2 bestSegment = new GeoLine2(line.StartPoint, line.StartPoint);
            double minDistanceSq = 0.0;
            double bestExtent = 0.0;
            bool found = false;

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 edge = polyline.GetEdgeAt(i);
                GeoLine2 seg = GetClosestSegment(line, edge, tolerance, out double extent);
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
        public static GeoLine2 GetClosestSegment(GeoLine2 line, GeoPolygon2 poly)
        {
            return GetClosestSegment(line, poly, Tolerance.Global);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="line"/> to a point on the boundary of <paramref name="poly"/> within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="poly">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> whose start point is on <paramref name="line"/> and end point is on the boundary of <paramref name="poly"/>.</returns>
        public static GeoLine2 GetClosestSegment(GeoLine2 line, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            GeoLine2 bestSegment = new GeoLine2(line.StartPoint, line.StartPoint);
            double minDistanceSq = 0.0;
            double bestExtent = 0.0;
            bool found = false;

            for (int i = 0; i < poly.EdgeCount; i++)
            {
                GeoLine2 edge = poly.GetEdgeAt(i);
                GeoLine2 seg = GetClosestSegment(line, edge, tolerance, out double extent);
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
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on <paramref name="line"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoCircle2 circle, GeoLine2 line) => GetClosestSegment(circle, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on <paramref name="line"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoCircle2 circle, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(line, circle, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="c1"/> to a point on the circumference of <paramref name="c2"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoCircle2 c1, GeoCircle2 c2) => GetClosestSegment(c1, c2, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="c1"/> to a point on the circumference of <paramref name="c2"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoCircle2 c1, GeoCircle2 c2, Tolerance tolerance)
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
        public static GeoLine2 GetClosestSegment(GeoCircle2 circle, GeoRectangle2 rect) => GetClosestSegment(circle, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the boundary of <paramref name="rect"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoCircle2 circle, GeoRectangle2 rect, Tolerance tolerance) => GetClosestSegmentCircleToEdges(circle, rect.GetEdges(), tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the polyline path of <paramref name="polyline"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoCircle2 circle, GeoPolyline2 polyline) => GetClosestSegment(circle, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the polyline path of <paramref name="polyline"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoCircle2 circle, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            return GetClosestSegmentCircleToEdges(circle, polyline.GetEdges(), tolerance);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the boundary of <paramref name="poly"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoCircle2 circle, GeoPolygon2 poly) => GetClosestSegment(circle, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the circumference of <paramref name="circle"/> to a point on the boundary of <paramref name="poly"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoCircle2 circle, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            return GetClosestSegmentCircleToEdges(circle, poly.GetEdges(), tolerance);
        }

        #endregion

        #region Closest Segment Between Rectangle and Shapes

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on <paramref name="line"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 rect, GeoLine2 line) => GetClosestSegment(rect, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on <paramref name="line"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 rect, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(line, rect, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the circumference of <paramref name="circle"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 rect, GeoCircle2 circle) => GetClosestSegment(rect, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the circumference of <paramref name="circle"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 rect, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(circle, rect, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="r1"/> to a point on the boundary of <paramref name="r2"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 r1, GeoRectangle2 r2) => GetClosestSegment(r1, r2, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="r1"/> to a point on the boundary of <paramref name="r2"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 r1, GeoRectangle2 r2, Tolerance tolerance) => GetClosestSegmentBetweenEdgeSets(r1.GetEdges(), r2.GetEdges(), tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the polyline path of <paramref name="polyline"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 rect, GeoPolyline2 polyline) => GetClosestSegment(rect, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the polyline path of <paramref name="polyline"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 rect, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            return GetClosestSegmentBetweenEdgeSets(rect.GetEdges(), polyline.GetEdges(), tolerance);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the boundary of <paramref name="poly"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 rect, GeoPolygon2 poly) => GetClosestSegment(rect, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="rect"/> to a point on the boundary of <paramref name="poly"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoRectangle2 rect, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            return GetClosestSegmentBetweenEdgeSets(rect.GetEdges(), poly.GetEdges(), tolerance);
        }

        #endregion

        #region Closest Segment Between Polyline and Shapes

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on <paramref name="line"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 polyline, GeoLine2 line) => GetClosestSegment(polyline, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on <paramref name="line"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 polyline, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(line, polyline, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the circumference of <paramref name="circle"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 polyline, GeoCircle2 circle) => GetClosestSegment(polyline, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the circumference of <paramref name="circle"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 polyline, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(circle, polyline, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the boundary of <paramref name="rect"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 polyline, GeoRectangle2 rect) => GetClosestSegment(polyline, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the boundary of <paramref name="rect"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 polyline, GeoRectangle2 rect, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(rect, polyline, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="p1"/> to a point on <paramref name="p2"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 p1, GeoPolyline2 p2) => GetClosestSegment(p1, p2, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="p1"/> to a point on <paramref name="p2"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 p1, GeoPolyline2 p2, Tolerance tolerance)
        {
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (p2 == null) throw new ArgumentNullException(nameof(p2));
            return GetClosestSegmentBetweenEdgeSets(p1.GetEdges(), p2.GetEdges(), tolerance);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the boundary of <paramref name="poly"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 polyline, GeoPolygon2 poly) => GetClosestSegment(polyline, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on <paramref name="polyline"/> to a point on the boundary of <paramref name="poly"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolyline2 polyline, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            return GetClosestSegmentBetweenEdgeSets(polyline.GetEdges(), poly.GetEdges(), tolerance);
        }

        #endregion

        #region Closest Segment Between Polygon and Shapes

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on <paramref name="line"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 poly, GeoLine2 line) => GetClosestSegment(poly, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on <paramref name="line"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 poly, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(line, poly, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on the circumference of <paramref name="circle"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 poly, GeoCircle2 circle) => GetClosestSegment(poly, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on the circumference of <paramref name="circle"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 poly, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(circle, poly, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on the boundary of <paramref name="rect"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 poly, GeoRectangle2 rect) => GetClosestSegment(poly, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on the boundary of <paramref name="rect"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 poly, GeoRectangle2 rect, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(rect, poly, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on <paramref name="polyline"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 poly, GeoPolyline2 polyline) => GetClosestSegment(poly, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="poly"/> to a point on <paramref name="polyline"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 poly, GeoPolyline2 polyline, Tolerance tolerance)
        {
            GeoLine2 seg = GetClosestSegment(polyline, poly, tolerance);
            return new GeoLine2(seg.EndPoint, seg.StartPoint);
        }

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="p1"/> to a point on the boundary of <paramref name="p2"/> using default tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 p1, GeoPolygon2 p2) => GetClosestSegment(p1, p2, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of <paramref name="p1"/> to a point on the boundary of <paramref name="p2"/> within tolerance.
        /// </summary>
        public static GeoLine2 GetClosestSegment(GeoPolygon2 p1, GeoPolygon2 p2, Tolerance tolerance)
        {
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (p2 == null) throw new ArgumentNullException(nameof(p2));
            return GetClosestSegmentBetweenEdgeSets(p1.GetEdges(), p2.GetEdges(), tolerance);
        }

        #endregion

        #region Helper Methods for Closest Segments

        private static GeoLine2 GetClosestSegmentBetweenEdgeSets(IEnumerable<GeoLine2> edges1, IEnumerable<GeoLine2> edges2, Tolerance tolerance)
        {
            GeoLine2 bestSegment = default;
            double minDistanceSq = 0.0;
            double bestExtent = 0.0;
            bool found = false;

            foreach (var e1 in edges1)
            {
                foreach (var e2 in edges2)
                {
                    GeoLine2 seg = GetClosestSegment(e1, e2, tolerance, out double extent);
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

        private static GeoLine2 GetClosestSegmentCircleToEdges(GeoCircle2 circle, IEnumerable<GeoLine2> edges, Tolerance tolerance)
        {
            GeoLine2 bestSegment = default;
            double minDistanceSq = 0.0;
            bool found = false;

            foreach (var edge in edges)
            {
                GeoLine2 seg = GetClosestSegment(edge, circle, tolerance);
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
    }
}
