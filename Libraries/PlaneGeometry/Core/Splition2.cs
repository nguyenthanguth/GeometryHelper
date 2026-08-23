using System;
using System.Collections.Generic;
using CommonGeometry;
using CommonGeometry.Enums;
using PlaneGeometry.Geometry;

namespace PlaneGeometry.Core
{
    /// <summary>
    /// Splits open geometry — line segments and polylines — at a position along it, or wherever a cutter
    /// meets it. A cutter may be a point, a line, a polyline or a polygon, singly or as an array, and
    /// several polygons behave as their union.
    /// <para>
    /// Pieces come back in order along the subject, so the first piece always holds its start point and
    /// the last piece always holds its end point. Splitting against a polygon sorts them by side instead,
    /// each side still in order. Geometry that closes back on itself is a
    /// <see cref="Geometry.GeoPolygon2"/> and is a cutter here, never a subject.
    /// </para>
    /// <para>
    /// A false return says nothing was cut, not that the call failed: the out parameters are left usable
    /// either way, holding the subject whole rather than nothing at all.
    /// </para>
    /// </summary>
    public static partial class Splition2
    {
        #region Split by point

        // ============================================================
        // LINE
        // ============================================================

        /// <summary>
        /// Splits a line segment at a point lying on it, using the default tolerance.
        /// </summary>
        /// <param name="line">The line segment to split.</param>
        /// <param name="point">The point to split at, which must lie on the segment.</param>
        /// <param name="first">The piece holding the start point of the segment.</param>
        /// <param name="second">The piece holding the end point of the segment.</param>
        /// <returns>true if the segment was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 line, GeoPoint2 point, out GeoLine2 first, out GeoLine2 second)
        {
            return TrySplitBy(line, point, out first, out second, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment at a point lying on it, within tolerance.
        /// </summary>
        /// <param name="line">The line segment to split.</param>
        /// <param name="point">The point to split at, which must lie on the segment.</param>
        /// <param name="first">The piece holding the start point of the segment.</param>
        /// <param name="second">The piece holding the end point of the segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the segment was split; false if the point is off the segment, or sits on one of its
        /// endpoints, where there is nothing to cut.
        /// </returns>
        public static bool TrySplitBy(GeoLine2 line, GeoPoint2 point, out GeoLine2 first, out GeoLine2 second, Tolerance tolerance)
        {
            first = default(GeoLine2);
            second = default(GeoLine2);

            if (!TryGetCutDistance(line, point, tolerance, out double distance))
            {
                return false;
            }

            return TrySplitAtDistance(line, distance, out first, out second, tolerance);
        }

        /// <summary>
        /// Splits a line segment at an arc length measured from its start point, using the default tolerance.
        /// </summary>
        /// <param name="line">The line segment to split.</param>
        /// <param name="distance">Arc length from the start point.</param>
        /// <param name="first">The piece holding the start point of the segment.</param>
        /// <param name="second">The piece holding the end point of the segment.</param>
        /// <returns>true if the segment was split; otherwise, false.</returns>
        public static bool TrySplitAtDistance(GeoLine2 line, double distance, out GeoLine2 first, out GeoLine2 second)
        {
            return TrySplitAtDistance(line, distance, out first, out second, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment at an arc length measured from its start point, within tolerance.
        /// </summary>
        /// <param name="line">The line segment to split.</param>
        /// <param name="distance">Arc length from the start point.</param>
        /// <param name="first">The piece holding the start point of the segment.</param>
        /// <param name="second">The piece holding the end point of the segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the segment was split; false if the distance falls outside the segment or lands on one
        /// of its endpoints.
        /// </returns>
        public static bool TrySplitAtDistance(GeoLine2 line, double distance, out GeoLine2 first, out GeoLine2 second, Tolerance tolerance)
        {
            first = default(GeoLine2);
            second = default(GeoLine2);

            double[] cuts = NormalizeCuts(line.Length, new[] { distance }, tolerance);
            if (cuts.Length != 1)
            {
                return false;
            }

            GeoLine2[] pieces = SplitLineAt(line, cuts);
            first = pieces[0];
            second = pieces[1];
            return true;
        }

        // ============================================================
        // POLYLINE
        // ============================================================

        /// <summary>
        /// Splits a polyline at a point lying on it, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline to split.</param>
        /// <param name="point">The point to split at, which must lie on the polyline.</param>
        /// <param name="first">The piece holding the start point of the polyline.</param>
        /// <param name="second">The piece holding the end point of the polyline.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 polyline, GeoPoint2 point, out GeoPolyline2 first, out GeoPolyline2 second)
        {
            return TrySplitBy(polyline, point, out first, out second, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline at a point lying on it, within tolerance.
        /// </summary>
        /// <param name="polyline">The polyline to split.</param>
        /// <param name="point">The point to split at, which must lie on the polyline.</param>
        /// <param name="first">The piece holding the start point of the polyline.</param>
        /// <param name="second">The piece holding the end point of the polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the polyline was split; false if the point is off the polyline, or sits on one of its
        /// endpoints, where there is nothing to cut.
        /// </returns>
        public static bool TrySplitBy(GeoPolyline2 polyline, GeoPoint2 point, out GeoPolyline2 first, out GeoPolyline2 second, Tolerance tolerance)
        {
            first = null;
            second = null;

            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            if (!TryGetCutDistance(polyline, point, tolerance, out double distance))
            {
                return false;
            }

            return TrySplitAtDistance(polyline, distance, out first, out second, tolerance);
        }

        /// <summary>
        /// Splits a polyline at an arc length measured from its first vertex, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline to split.</param>
        /// <param name="distance">Arc length from the first vertex.</param>
        /// <param name="first">The piece holding the start point of the polyline.</param>
        /// <param name="second">The piece holding the end point of the polyline.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitAtDistance(GeoPolyline2 polyline, double distance, out GeoPolyline2 first, out GeoPolyline2 second)
        {
            return TrySplitAtDistance(polyline, distance, out first, out second, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline at an arc length measured from its first vertex, within tolerance.
        /// </summary>
        /// <param name="polyline">The polyline to split.</param>
        /// <param name="distance">Arc length from the first vertex.</param>
        /// <param name="first">The piece holding the start point of the polyline.</param>
        /// <param name="second">The piece holding the end point of the polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the polyline was split; false if the distance falls outside the polyline or lands on one
        /// of its endpoints.
        /// </returns>
        public static bool TrySplitAtDistance(GeoPolyline2 polyline, double distance, out GeoPolyline2 first, out GeoPolyline2 second, Tolerance tolerance)
        {
            first = null;
            second = null;

            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double[] cuts = NormalizeCuts(polyline.Length, new[] { distance }, tolerance);
            if (cuts.Length != 1)
            {
                return false;
            }

            GeoPolyline2[] pieces = SplitPolylineAt(polyline, cuts, tolerance);
            if (pieces.Length != 2)
            {
                return false;
            }

            first = pieces[0];
            second = pieces[1];
            return true;
        }

        #endregion

        #region Split by line

        // ============================================================
        // LINE
        // ============================================================

        /// <summary>
        /// Splits a line segment where a cutting line segment crosses it, using the default tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutter">The cutting line segment.</param>
        /// <param name="first">The piece holding the start point of the subject.</param>
        /// <param name="second">The piece holding the end point of the subject.</param>
        /// <returns>true if the subject was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoLine2 cutter, out GeoLine2 first, out GeoLine2 second)
        {
            return TrySplitBy(subject, cutter, out first, out second, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment where a cutting line segment crosses it, within tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutter">The cutting line segment.</param>
        /// <param name="first">The piece holding the start point of the subject.</param>
        /// <param name="second">The piece holding the end point of the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the subject was split; false if the two segments miss each other, meet only at an
        /// endpoint of the subject, or are parallel — including the collinear overlap case, where there is
        /// no single position to cut at.
        /// </returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoLine2 cutter, out GeoLine2 first, out GeoLine2 second, Tolerance tolerance)
        {
            first = default(GeoLine2);
            second = default(GeoLine2);

            if (!Intersection2.TryIntersectWith(subject, cutter, out GeoPoint2 crossing, tolerance))
            {
                return false;
            }

            return TrySplitBy(subject, crossing, out first, out second, tolerance);
        }

        /// <summary>
        /// Splits a line segment everywhere a list of cutting line segments crosses it, using the default tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutters">The cutting line segments.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <returns>true if the subject was split by at least one cutter; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoLine2[] cutters, out GeoLine2[] pieces)
        {
            return TrySplitBy(subject, cutters, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment everywhere a list of cutting line segments crosses it, within tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutters">The cutting line segments.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the subject was split by at least one cutter; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoLine2[] cutters, out GeoLine2[] pieces, Tolerance tolerance)
        {
            if (cutters == null) throw new ArgumentNullException(nameof(cutters));

            var crossings = new List<GeoPoint2>();
            foreach (GeoLine2 cutter in cutters)
            {
                if (cutter != null && Intersection2.TryIntersectWith(subject, cutter, out GeoPoint2 crossing, tolerance))
                {
                    crossings.Add(crossing);
                }
            }

            if (crossings.Count == 0)
            {
                pieces = new[] { subject };
                return false;
            }

            double[] cuts = ToCutDistances(subject, crossings.ToArray(), tolerance);

            if (cuts.Length == 0)
            {
                pieces = new[] { subject };
                return false;
            }

            pieces = SplitLineAt(subject, cuts);
            return true;
        }

        /// <summary>
        /// Splits a line segment everywhere a cutting polyline crosses it, using the default tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutter">The cutting polyline.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <returns>true if the subject was split by the polyline; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoPolyline2 cutter, out GeoLine2[] pieces)
        {
            return TrySplitBy(subject, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment everywhere a cutting polyline crosses it, within tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutter">The cutting polyline.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the subject was split by the polyline; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoPolyline2 cutter, out GeoLine2[] pieces, Tolerance tolerance)
        {
            if (cutter == null) throw new ArgumentNullException(nameof(cutter));

            int edgeCount = cutter.EdgeCount;
            GeoLine2[] cutters = new GeoLine2[edgeCount];
            for (int i = 0; i < edgeCount; i++)
            {
                cutters[i] = cutter.GetEdgeAt(i);
            }

            return TrySplitBy(subject, cutters, out pieces, tolerance);
        }

        /// <summary>
        /// Splits a line segment everywhere a list of points lies on it, using the default tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="points">The points to split at.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <returns>true if the subject was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoPoint2[] points, out GeoLine2[] pieces)
        {
            return TrySplitBy(subject, points, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment everywhere a list of points lies on it, within tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="points">The points to split at.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the subject was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoPoint2[] points, out GeoLine2[] pieces, Tolerance tolerance)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));

            double[] cuts = ToCutDistancesFromPoints(subject, points, tolerance);

            if (cuts.Length == 0)
            {
                pieces = new[] { subject };
                return false;
            }

            pieces = SplitLineAt(subject, cuts);
            return true;
        }

        /// <summary>
        /// Splits a line segment everywhere a list of polygon boundaries crosses it, separating the parts that fall
        /// inside the polygons from those that fall outside, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine2 subject, GeoPolygon2[] cutters, out GeoLine2[] inside, out GeoLine2[] outside)
        {
            return TrySplitBy(subject, cutters, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment everywhere a list of polygon boundaries crosses it, separating the parts that fall
        /// inside the polygons from those that fall outside, using a specified tolerance.
        /// </summary>
        /// <param name="subject">The line segment to be split.</param>
        /// <param name="cutters">The array of polygons that cross the line segment.</param>
        /// <param name="inside">The portions of the line segment that fall inside the polygons.</param>
        /// <param name="outside">The portions of the line segment that fall outside the polygons.</param>
        /// <param name="tolerance">The tolerance value to use for calculations.</param>
        /// <returns>True if the line segment is successfully split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoPolygon2[] cutters, out GeoLine2[] inside, out GeoLine2[] outside, Tolerance tolerance)
        {
            if (cutters == null) throw new ArgumentNullException(nameof(cutters));

            double[] cuts = ToCutDistances(subject, CollectCrossings(cutters, subject, tolerance), tolerance);

            if (cuts.Length == 0)
            {
                bool whollyInside = IsInsideAny(cutters, subject.MidPoint, tolerance);
                inside = whollyInside ? new[] { subject } : NoLines;
                outside = whollyInside ? NoLines : new[] { subject };
                return false;
            }

            GeoLine2[] pieces = SplitLineAt(subject, cuts);

            var insideList = new List<GeoLine2>();
            var outsideList = new List<GeoLine2>();

            foreach (GeoLine2 piece in pieces)
            {
                // No crossing falls inside a piece, so its midpoint speaks for the whole of it. An
                // endpoint would not: every one of them sits on a boundary by construction.
                if (IsInsideAny(cutters, piece.MidPoint, tolerance))
                {
                    insideList.Add(piece);
                }
                else
                {
                    outsideList.Add(piece);
                }
            }

            inside = Merge2.ConsecutiveLines(insideList, tolerance);
            outside = Merge2.ConsecutiveLines(outsideList, tolerance);
            return true;
        }

        /// <summary>
        /// Splits a line segment everywhere a list of polylines crosses it, using the default tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutters">The cutting polylines.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <returns>true if at least one polyline crosses the subject; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoPolyline2[] cutters, out GeoLine2[] pieces)
        {
            return TrySplitBy(subject, cutters, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment everywhere a list of polylines crosses it, within tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutters">The cutting polylines.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if at least one polyline crosses the subject; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoPolyline2[] cutters, out GeoLine2[] pieces, Tolerance tolerance)
        {
            if (cutters == null) throw new ArgumentNullException(nameof(cutters));

            var edgeList = new List<GeoLine2>();
            foreach (GeoPolyline2 polyline in cutters)
            {
                if (polyline != null)
                {
                    for (int i = 0; i < polyline.EdgeCount; i++)
                    {
                        edgeList.Add(polyline.GetEdgeAt(i));
                    }
                }
            }

            return TrySplitBy(subject, edgeList.ToArray(), out pieces, tolerance);
        }

        // ============================================================
        // POLYLINE
        // ============================================================

        /// <summary>
        /// Splits a polyline everywhere a cutting line segment crosses it, using the default tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="cutter">The cutting line segment.</param>
        /// <param name="pieces">The pieces in order along the subject if split succeeds.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoLine2 cutter, out GeoPolyline2[] pieces)
        {
            return TrySplitBy(subject, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline everywhere a cutting line segment crosses it, within tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="cutter">The cutting line segment.</param>
        /// <param name="pieces">The pieces in order along the subject if split succeeds.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoLine2 cutter, out GeoPolyline2[] pieces, Tolerance tolerance)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));

            GeoPoint2[] crossings = Intersection2.GetIntersections(subject, cutter, tolerance);
            double[] cuts = ToCutDistances(subject, crossings, tolerance);
            pieces = SplitPolylineAt(subject, cuts, tolerance);

            // Every overload here leaves its out parameters usable whether or not anything was cut, so a
            // caller that ignores the return value still gets the subject back rather than a null array.
            return pieces.Length > 1;
        }

        /// <summary>
        /// Splits a polyline everywhere a list of points lies on it, using the default tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="points">The points to split at.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoPoint2[] points, out GeoPolyline2[] pieces)
        {
            return TrySplitBy(subject, points, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline everywhere a list of points lies on it, within tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="points">The points to split at.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoPoint2[] points, out GeoPolyline2[] pieces, Tolerance tolerance)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (points == null) throw new ArgumentNullException(nameof(points));

            double[] cuts = ToCutDistancesFromPoints(subject, points, tolerance);

            if (cuts.Length == 0)
            {
                pieces = new[] { subject };
                return false;
            }

            pieces = SplitPolylineAt(subject, cuts, tolerance);
            return true;
        }

        /// <summary>
        /// Splits a polyline everywhere a list of cutting line segments crosses it, using the default tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="cutters">The cutting line segments.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoLine2[] cutters, out GeoPolyline2[] pieces)
        {
            return TrySplitBy(subject, cutters, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline everywhere a list of cutting line segments crosses it, within tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="cutters">The cutting line segments.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoLine2[] cutters, out GeoPolyline2[] pieces, Tolerance tolerance)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (cutters == null) throw new ArgumentNullException(nameof(cutters));

            var crossings = new List<GeoPoint2>();
            foreach (GeoLine2 cutter in cutters)
            {
                if (cutter != null)
                {
                    GeoPoint2[] lineCrossings = Intersection2.GetIntersections(subject, cutter, tolerance);
                    crossings.AddRange(lineCrossings);
                }
            }

            if (crossings.Count == 0)
            {
                pieces = new[] { subject };
                return false;
            }

            double[] cuts = ToCutDistances(subject, crossings.ToArray(), tolerance);

            if (cuts.Length == 0)
            {
                pieces = new[] { subject };
                return false;
            }

            pieces = SplitPolylineAt(subject, cuts, tolerance);
            return true;
        }

        /// <summary>
        /// Splits a polyline everywhere a list of cutting polylines crosses it, using the default tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="cutters">The cutting polylines.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoPolyline2[] cutters, out GeoPolyline2[] pieces)
        {
            return TrySplitBy(subject, cutters, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline everywhere a list of cutting polylines crosses it, within tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="cutters">The cutting polylines.</param>
        /// <param name="pieces">The resulting pieces in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoPolyline2[] cutters, out GeoPolyline2[] pieces, Tolerance tolerance)
        {
            if (cutters == null) throw new ArgumentNullException(nameof(cutters));

            var edgeList = new List<GeoLine2>();
            foreach (GeoPolyline2 polyline in cutters)
            {
                if (polyline != null)
                {
                    for (int i = 0; i < polyline.EdgeCount; i++)
                    {
                        edgeList.Add(polyline.GetEdgeAt(i));
                    }
                }
            }

            return TrySplitBy(subject, edgeList.ToArray(), out pieces, tolerance);
        }

        /// <summary>
        /// Splits a polyline everywhere a list of polygon boundaries crosses it, separating the parts that fall
        /// inside the polygons from those that fall outside, using the default tolerance.
        /// </summary>
        /// <param name="subject">The polyline to be split.</param>
        /// <param name="cutters">The array of polygons that cross the polyline.</param>
        /// <param name="inside">The portions of the polyline that fall inside the polygons.</param>
        /// <param name="outside">The portions of the polyline that fall outside the polygons.</param>
        /// <returns>True if the polyline is successfully split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoPolygon2[] cutters, out GeoPolyline2[] inside, out GeoPolyline2[] outside)
        {
            return TrySplitBy(subject, cutters, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline everywhere a list of polygon boundaries crosses it, separating the parts that fall
        /// inside the polygons from those that fall outside, using a specified tolerance.
        /// </summary>
        /// <param name="subject">The polyline to be split.</param>
        /// <param name="cutters">The array of polygons that cross the polyline.</param>
        /// <param name="inside">The portions of the polyline that fall inside the polygons.</param>
        /// <param name="outside">The portions of the polyline that fall outside the polygons.</param>
        /// <param name="tolerance">The tolerance value to use for calculations.</param>
        /// <returns>True if the polyline is successfully split; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoPolygon2[] cutters, out GeoPolyline2[] inside, out GeoPolyline2[] outside, Tolerance tolerance)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (cutters == null) throw new ArgumentNullException(nameof(cutters));

            double[] cuts = ToCutDistances(subject, CollectCrossings(cutters, subject, tolerance), tolerance);

            if (cuts.Length == 0)
            {
                bool whollyInside = IsInsideAny(cutters, subject.MidPoint, tolerance);
                inside = whollyInside ? new[] { subject } : NoPolylines;
                outside = whollyInside ? NoPolylines : new[] { subject };
                return false;
            }

            GeoPolyline2[] pieces = SplitPolylineAt(subject, cuts, tolerance);

            var insideList = new List<GeoPolyline2>();
            var outsideList = new List<GeoPolyline2>();

            foreach (GeoPolyline2 piece in pieces)
            {
                if (IsInsideAny(cutters, piece.MidPoint, tolerance))
                {
                    insideList.Add(piece);
                }
                else
                {
                    outsideList.Add(piece);
                }
            }

            inside = Merge2.ConsecutivePolylines(insideList, tolerance);
            outside = Merge2.ConsecutivePolylines(outsideList, tolerance);
            return true;
        }

        #endregion

        #region Split by polygon

        // ============================================================
        // LINE
        // ============================================================

        /// <summary>
        /// Splits a line segment where a polygon boundary crosses it, separating the parts that fall
        /// inside the polygon from those that fall outside, using the default tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutter">The polygon to split against.</param>
        /// <param name="inside">The parts lying inside the polygon, in order along the subject.</param>
        /// <param name="outside">The parts lying outside the polygon, in order along the subject.</param>
        /// <returns>true if the polygon boundary crosses the subject; otherwise, false.</returns>
        public static bool TrySplitBy(GeoLine2 subject, GeoPolygon2 cutter, out GeoLine2[] inside, out GeoLine2[] outside)
        {
            return TrySplitBy(subject, cutter, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment where a polygon boundary crosses it, separating the parts that fall
        /// inside the polygon from those that fall outside, within tolerance.
        /// </summary>
        /// <param name="subject">The line segment to split.</param>
        /// <param name="cutter">The polygon to split against.</param>
        /// <param name="inside">The parts lying inside the polygon, in order along the subject.</param>
        /// <param name="outside">The parts lying outside the polygon, in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the polygon boundary crosses the subject; false when it does not, which is not a
        /// failure: one array then holds the whole subject and the other is empty, according to which
        /// side the subject lies on.
        /// </returns>
        /// <remarks>
        /// A part running along the polygon boundary counts as inside, matching
        /// <see cref="Containment2.Contains(GeoPolygon2, GeoPoint2, Tolerance)"/>, which treats the boundary
        /// as part of the polygon.
        /// </remarks>
        public static bool TrySplitBy(GeoLine2 subject, GeoPolygon2 cutter, out GeoLine2[] inside, out GeoLine2[] outside, Tolerance tolerance)
        {
            if (cutter == null) throw new ArgumentNullException(nameof(cutter));

            GeoPoint2[] crossings = Intersection2.GetIntersections(cutter, subject, tolerance);
            double[] cuts = ToCutDistances(subject, crossings, tolerance);
            cuts = KeepCrossings(
                subject.Length, cuts,
                distance => Containment2.Contains(cutter, Parametrization2.GetPointAtDistance(subject, distance), tolerance),
                out bool[] insideOfPiece);

            GeoLine2[] pieces = SplitLineAt(subject, cuts);

            var insideParts = new List<GeoLine2>();
            var outsideParts = new List<GeoLine2>();

            for (int i = 0; i < pieces.Length; i++)
            {
                var bucket = insideOfPiece[i] ? insideParts : outsideParts;
                bucket.Add(pieces[i]);
            }

            inside = insideParts.ToArray();
            outside = outsideParts.ToArray();
            return cuts.Length > 0;
        }

        // ============================================================
        // POLYLINE
        // ============================================================

        /// <summary>
        /// Splits a polyline where a polygon boundary crosses it, separating the parts that fall inside
        /// the polygon from those that fall outside, using the default tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="cutter">The polygon to split against.</param>
        /// <param name="inside">The sub-polylines lying inside the polygon, in order along the subject.</param>
        /// <param name="outside">The sub-polylines lying outside the polygon, in order along the subject.</param>
        /// <returns>true if the polygon boundary crosses the subject; otherwise, false.</returns>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoPolygon2 cutter, out GeoPolyline2[] inside, out GeoPolyline2[] outside)
        {
            return TrySplitBy(subject, cutter, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline where a polygon boundary crosses it, separating the parts that fall inside
        /// the polygon from those that fall outside, within tolerance.
        /// </summary>
        /// <param name="subject">The polyline to split.</param>
        /// <param name="cutter">The polygon to split against.</param>
        /// <param name="inside">The sub-polylines lying inside the polygon, in order along the subject.</param>
        /// <param name="outside">The sub-polylines lying outside the polygon, in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the polygon boundary crosses the subject; false when it does not, which is not a
        /// failure: one array then holds the subject itself and the other is empty.
        /// </returns>
        /// <remarks>
        /// <para>
        /// A segment running along the polygon boundary counts as inside, matching
        /// <see cref="Containment2.Contains(GeoPolygon2, GeoPoint2, Tolerance)"/>.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline2 subject, GeoPolygon2 cutter, out GeoPolyline2[] inside, out GeoPolyline2[] outside, Tolerance tolerance)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (cutter == null) throw new ArgumentNullException(nameof(cutter));

            GeoPoint2[] crossings = Intersection2.GetIntersections(subject, cutter, tolerance);
            double[] cuts = ToCutDistances(subject, crossings, tolerance);
            cuts = KeepCrossings(
                subject.Length, cuts,
                distance => Containment2.Contains(cutter, Parametrization2.GetPointAtDistance(subject, distance), tolerance),
                out bool[] insideOfPiece);

            GeoPolyline2[] pieces = SplitPolylineAt(subject, cuts, tolerance);

            var insideParts = new List<GeoPolyline2>();
            var outsideParts = new List<GeoPolyline2>();

            for (int i = 0; i < pieces.Length; i++)
            {
                var bucket = insideOfPiece[i] ? insideParts : outsideParts;
                bucket.Add(pieces[i]);
            }

            inside = insideParts.ToArray();
            outside = outsideParts.ToArray();
            return cuts.Length > 0;
        }

        #endregion

        #region Split by distance

        // ============================================================
        // LINE
        // ============================================================

        /// <summary>
        /// Splits a line segment at several arc lengths measured from its start point, using the default
        /// tolerance.
        /// </summary>
        /// <param name="line">The line segment to split.</param>
        /// <param name="distances">Arc lengths from the start point, in any order.</param>
        /// <returns>The pieces in order along the segment.</returns>
        public static GeoLine2[] SplitAtDistances(GeoLine2 line, IEnumerable<double> distances)
        {
            return SplitAtDistances(line, distances, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment at several arc lengths measured from its start point, within tolerance.
        /// </summary>
        /// <param name="line">The line segment to split.</param>
        /// <param name="distances">Arc lengths from the start point, in any order.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The pieces in order along the segment; a single element holding the segment itself when no
        /// distance is usable. Distances outside the segment, and duplicates closer together than the
        /// tolerance, are dropped rather than producing empty pieces.
        /// </returns>
        public static GeoLine2[] SplitAtDistances(GeoLine2 line, IEnumerable<double> distances, Tolerance tolerance)
        {
            double[] cuts = NormalizeCuts(line.Length, distances, tolerance);
            return SplitLineAt(line, cuts);
        }

        // ============================================================
        // POLYLINE
        // ============================================================

        /// <summary>
        /// Splits a polyline at several arc lengths measured from its first vertex, using the default
        /// tolerance.
        /// </summary>
        /// <param name="polyline">The polyline to split.</param>
        /// <param name="distances">Arc lengths from the first vertex, in any order.</param>
        /// <returns>The pieces in order along the polyline.</returns>
        public static GeoPolyline2[] SplitAtDistances(GeoPolyline2 polyline, IEnumerable<double> distances)
        {
            return SplitAtDistances(polyline, distances, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline at several arc lengths measured from its first vertex, within tolerance.
        /// </summary>
        /// <param name="polyline">The polyline to split.</param>
        /// <param name="distances">Arc lengths from the first vertex, in any order.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The pieces in order along the polyline; a single element holding the polyline itself when no
        /// distance is usable. N usable distances yield N + 1 pieces.
        /// </returns>
        public static GeoPolyline2[] SplitAtDistances(GeoPolyline2 polyline, IEnumerable<double> distances, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double[] cuts = NormalizeCuts(polyline.Length, distances, tolerance);
            return SplitPolylineAt(polyline, cuts, tolerance);
        }

        #endregion
    }
}
