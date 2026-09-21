using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static methods that change how long a line segment is while keeping it on the line that
    /// carries it: lengthening or shortening it by a distance, extending an end out to a boundary, trimming
    /// an end back to one, and trimming or extending two segments until they meet in a corner.
    /// <para>
    /// The operations follow AutoCAD's LENGTHEN, EXTEND and TRIM commands, and FILLET with a radius of zero.
    /// A segment has no picked point to say which end is meant, so every method that moves one end takes a
    /// <see cref="LineEnd"/>; the other end never moves, and the direction of the segment never reverses.
    /// </para>
    /// <para>
    /// <c>TryExtendTo</c> only ever lengthens: the end moves outward to the nearest place where the line
    /// meets the boundary. <c>TryTrimTo</c> only ever shortens: the end moves back to the nearest place,
    /// still within the segment, where the boundary crosses it. An end that already lies on the boundary,
    /// within tolerance, satisfies both and is left there, so either call can be repeated without moving the
    /// end again, and <c>TryExtendTo(...) || TryTrimTo(...)</c> fits an end to a boundary whichever side of
    /// it the end starts on.
    /// </para>
    /// <para>
    /// A boundary is met where the infinite line carrying the segment crosses it. A boundary segment, polyline
    /// or polygon edge running parallel to the line is not a crossing, since it is either missed or shared
    /// along a whole stretch. A point is met where the line passes closest to it, at the foot of the
    /// perpendicular, so a point to one side of the line still pulls the end level with it.
    /// </para>
    /// </summary>
    public static class Lengthen2
    {
        #region By distance

        /// <summary>
        /// Lengthens one end of a segment by a distance, using the default tolerance. A negative distance
        /// shortens it.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="distance">How far the end moves outward; negative moves it back.</param>
        /// <param name="end">The end that moves.</param>
        /// <returns>The segment with the end moved along its own direction.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the distance is not a finite number, the end is not a defined value, or the segment
        /// would be shortened to nothing or past its other end.
        /// </exception>
        public static GeoLine2 Extend(GeoLine2 line, double distance, LineEnd end) => Extend(line, distance, end, Tolerance.Global);

        /// <summary>
        /// Lengthens one end of a segment by a distance, within tolerance. A negative distance shortens it.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="distance">How far the end moves outward; negative moves it back.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="tolerance">The tolerance deciding when the segment is too short.</param>
        /// <returns>The segment with the end moved along its own direction.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the distance is not a finite number, the end is not a defined value, or the segment
        /// would be shortened to nothing or past its other end.
        /// </exception>
        public static GeoLine2 Extend(GeoLine2 line, double distance, LineEnd end, Tolerance tolerance)
        {
            ValidateEnd(end);

            return end == LineEnd.Start
                ? Extend(line, distance, 0.0, tolerance)
                : Extend(line, 0.0, distance, tolerance);
        }

        /// <summary>
        /// Lengthens both ends of a segment, each by its own distance, using the default tolerance. A negative
        /// distance shortens that end. This is the end offset Tekla Structures applies along a beam's axis.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="startDistance">How far the start point moves outward, away from the end point.</param>
        /// <param name="endDistance">How far the end point moves outward, away from the start point.</param>
        /// <returns>The segment with both ends moved along its own direction.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a distance is not a finite number, or the segment would be shortened to nothing or past
        /// itself.
        /// </exception>
        public static GeoLine2 Extend(GeoLine2 line, double startDistance, double endDistance) => Extend(line, startDistance, endDistance, Tolerance.Global);

        /// <summary>
        /// Lengthens both ends of a segment, each by its own distance, within tolerance. A negative distance
        /// shortens that end.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="startDistance">How far the start point moves outward, away from the end point.</param>
        /// <param name="endDistance">How far the end point moves outward, away from the start point.</param>
        /// <param name="tolerance">The tolerance deciding when a segment is too short.</param>
        /// <returns>The segment with both ends moved along its own direction.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a distance is not a finite number, or the segment would be shortened to nothing or past
        /// itself.
        /// </exception>
        public static GeoLine2 Extend(GeoLine2 line, double startDistance, double endDistance, Tolerance tolerance)
        {
            RequireFinite(startDistance, nameof(startDistance));
            RequireFinite(endDistance, nameof(endDistance));

            GeoVector2 unit = GetUnitDirection(line, tolerance);

            if (line.Length + startDistance + endDistance <= tolerance.EqualPoint)
            {
                throw new ArgumentOutOfRangeException(
                    startDistance < 0.0 ? nameof(startDistance) : nameof(endDistance),
                    "Shortening by this much would leave nothing of the segment, or turn it round.");
            }

            return new GeoLine2(
                line.StartPoint.Subtract(unit.Multiply(startDistance)),
                line.EndPoint.Add(unit.Multiply(endDistance)));
        }

        /// <summary>
        /// Moves one end of a segment so that the segment measures a given length, using the default
        /// tolerance. The other end stays, so a length shorter than the segment trims it. This is LENGTHEN
        /// with its Total option.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="length">The length the segment should have.</param>
        /// <param name="end">The end that moves.</param>
        /// <returns>The segment of the requested length, running the same way from the end that stayed.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length is not a finite number greater than the tolerance, or the end is not a defined
        /// value.
        /// </exception>
        public static GeoLine2 ExtendToLength(GeoLine2 line, double length, LineEnd end) => ExtendToLength(line, length, end, Tolerance.Global);

        /// <summary>
        /// Moves one end of a segment so that the segment measures a given length, within tolerance. The other
        /// end stays, so a length shorter than the segment trims it.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="length">The length the segment should have.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="tolerance">The tolerance deciding when a segment is too short.</param>
        /// <returns>The segment of the requested length, running the same way from the end that stayed.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length is not a finite number greater than the tolerance, or the end is not a defined
        /// value.
        /// </exception>
        public static GeoLine2 ExtendToLength(GeoLine2 line, double length, LineEnd end, Tolerance tolerance)
        {
            ValidateEnd(end);
            RequireFinite(length, nameof(length));

            if (length <= tolerance.EqualPoint)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "A segment must be longer than the point tolerance.");
            }

            GeoVector2 unit = GetUnitDirection(line, tolerance);

            return end == LineEnd.End
                ? new GeoLine2(line.StartPoint, line.StartPoint.Add(unit.Multiply(length)))
                : new GeoLine2(line.EndPoint.Subtract(unit.Multiply(length)), line.EndPoint);
        }

        #endregion

        #region Extend to a boundary

        /// <summary>
        /// Extends one end of a segment to the foot of the perpendicular from a point, using the default
        /// tolerance. This is <c>Curve.Extend(bool, Point3d)</c> in AutoCAD, for a point off the line as well.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">The point the end is brought level with.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the foot lies at or beyond the end; false if reaching it would shorten the segment.</returns>
        public static bool TryExtendTo(GeoLine2 line, GeoPoint2 point, LineEnd end, out GeoLine2 result) => TryExtendTo(line, point, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment to the foot of the perpendicular from a point, within tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">The point the end is brought level with.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the foot lies at or beyond the end; false if reaching it would shorten the segment.</returns>
        public static bool TryExtendTo(GeoLine2 line, GeoPoint2 point, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, true, out result, (reach, found) => found.Add(reach.Anchor.GetVectorTo(point).DotProduct(reach.Unit)));
        }

        /// <summary>
        /// Extends one end of a segment until it meets a boundary segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The boundary segment, which is not extended itself.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the extension meets the boundary; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine2 line, GeoLine2 boundary, LineEnd end, out GeoLine2 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it meets a boundary segment, within tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The boundary segment, which is not extended itself.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the extension meets the boundary; otherwise, false.</returns>
        /// <remarks>
        /// The boundary counts only where it is drawn, as with AutoCAD's default EDGEMODE. To extend to the
        /// line carrying a boundary however short it is drawn, find that point with
        /// <see cref="Intersection2.TryIntersectWith(GeoLine2, GeoLine2, LineExtension, out GeoPoint2, Tolerance)"/>
        /// and <see cref="LineExtension.Both"/>, and extend to the point.
        /// </remarks>
        public static bool TryExtendTo(GeoLine2 line, GeoLine2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddCrossing(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Extends one end of a segment until it meets a polyline, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polyline.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the extension meets the polyline; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine2 line, GeoPolyline2 boundary, LineEnd end, out GeoLine2 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it meets a polyline, within tolerance. The end stops at the
        /// nearest crossing beyond it.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polyline.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the extension meets the polyline; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static bool TryExtendTo(GeoLine2 line, GeoPolyline2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddCrossings(found, reach, boundary.GetEdges(), tolerance));
        }

        /// <summary>
        /// Extends one end of a segment until it meets the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polygon.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the extension meets the polygon boundary; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine2 line, GeoPolygon2 boundary, LineEnd end, out GeoLine2 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it meets the boundary of a polygon, within tolerance. The end
        /// stops at the nearest edge beyond it, so an end inside the polygon runs out to where the line leaves
        /// it and an end outside runs in to where the line enters.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polygon.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the extension meets the polygon boundary; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool TryExtendTo(GeoLine2 line, GeoPolygon2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddCrossings(found, reach, boundary.GetEdges(), tolerance));
        }

        /// <summary>
        /// Extends one end of a segment until it meets a circle, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The circle.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the extension meets the circumference; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine2 line, GeoCircle2 boundary, LineEnd end, out GeoLine2 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it meets a circle, within tolerance. A line passing within
        /// tolerance of tangency touches the circle once, at the foot of the perpendicular from its centre.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The circle.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the extension meets the circumference; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine2 line, GeoCircle2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddCrossings(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Extends one end of a segment until it meets the boundary of a rectangle, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The rectangle.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the extension meets the rectangle boundary; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine2 line, GeoRectangle2 boundary, LineEnd end, out GeoLine2 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it meets the boundary of a rectangle, within tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The rectangle.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the extension meets the rectangle boundary; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine2 line, GeoRectangle2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddCrossings(found, reach, boundary.GetEdges(), tolerance));
        }

        #endregion

        #region Trim to a boundary

        /// <summary>
        /// Trims one end of a segment back to the foot of the perpendicular from a point, using the default
        /// tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">The point the end is brought level with.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the foot lies within the segment, short of the other end; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoPoint2 point, LineEnd end, out GeoLine2 result) => TryTrimTo(line, point, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to the foot of the perpendicular from a point, within tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">The point the end is brought level with.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the foot lies within the segment, short of the other end; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoPoint2 point, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, false, out result, (reach, found) => found.Add(reach.Anchor.GetVectorTo(point).DotProduct(reach.Unit)));
        }

        /// <summary>
        /// Trims one end of a segment back to where a boundary segment crosses it, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The boundary segment.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the boundary crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoLine2 boundary, LineEnd end, out GeoLine2 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to where a boundary segment crosses it, within tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The boundary segment.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the boundary crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoLine2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddCrossing(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Trims one end of a segment back to the nearest place a polyline crosses it, using the default
        /// tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polyline.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the polyline crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoPolyline2 boundary, LineEnd end, out GeoLine2 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to the nearest place a polyline crosses it, within tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polyline.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polyline crosses the segment; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static bool TryTrimTo(GeoLine2 line, GeoPolyline2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddCrossings(found, reach, boundary.GetEdges(), tolerance));
        }

        /// <summary>
        /// Trims one end of a segment back to the nearest place the boundary of a polygon crosses it, using the
        /// default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polygon.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the polygon boundary crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoPolygon2 boundary, LineEnd end, out GeoLine2 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to the nearest place the boundary of a polygon crosses it, within
        /// tolerance. A segment running out of a polygon is cut where it leaves.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polygon.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polygon boundary crosses the segment; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool TryTrimTo(GeoLine2 line, GeoPolygon2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddCrossings(found, reach, boundary.GetEdges(), tolerance));
        }

        /// <summary>
        /// Trims one end of a segment back to the nearest place a circle crosses it, using the default
        /// tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The circle.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the circumference crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoCircle2 boundary, LineEnd end, out GeoLine2 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to the nearest place a circle crosses it, within tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The circle.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the circumference crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoCircle2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddCrossings(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Trims one end of a segment back to the nearest place the boundary of a rectangle crosses it, using
        /// the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The rectangle.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the rectangle boundary crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoRectangle2 boundary, LineEnd end, out GeoLine2 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to the nearest place the boundary of a rectangle crosses it, within
        /// tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The rectangle.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rectangle boundary crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine2 line, GeoRectangle2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddCrossings(found, reach, boundary.GetEdges(), tolerance));
        }

        #endregion

        #region Corner

        /// <summary>
        /// Trims or extends two segments so that they meet in a corner, using the default tolerance. This is
        /// FILLET with a radius of zero.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="result1">The first segment ending at the corner, or unchanged when the method returns false.</param>
        /// <param name="result2">The second segment ending at the corner, or unchanged when the method returns false.</param>
        /// <returns>true if the two lines cross; false if they are parallel or one is too short to have a direction.</returns>
        public static bool TryTrimExtendToCorner(GeoLine2 line1, GeoLine2 line2, out GeoLine2 result1, out GeoLine2 result2)
            => TryTrimExtendToCorner(line1, line2, out result1, out result2, Tolerance.Global);

        /// <summary>
        /// Trims or extends two segments so that they meet in a corner, within tolerance.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="result1">The first segment ending at the corner, or unchanged when the method returns false.</param>
        /// <param name="result2">The second segment ending at the corner, or unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two lines cross; false if they are parallel or one is too short to have a direction.</returns>
        /// <remarks>
        /// The corner is where the lines carrying the two segments cross. Each segment keeps its end farther
        /// from the corner and has its nearer end moved onto it: extended if the corner lies beyond, trimmed if
        /// the segments cross each other, so that the longer part of each is what remains. AutoCAD asks the
        /// user to pick the part to keep; keeping the longer one is what that pick would be in the usual case.
        /// When the corner lies exactly half way along a segment its end point moves.
        /// </remarks>
        public static bool TryTrimExtendToCorner(GeoLine2 line1, GeoLine2 line2, out GeoLine2 result1, out GeoLine2 result2, Tolerance tolerance)
        {
            result1 = line1;
            result2 = line2;

            if (line1.IsDegenerate(tolerance) || line2.IsDegenerate(tolerance))
            {
                return false;
            }

            if (!Intersection2.TryIntersectWith(line1, line2, LineExtension.Both, out GeoPoint2 corner, tolerance))
            {
                return false;
            }

            GeoLine2 moved1 = MoveNearerEnd(line1, corner);
            GeoLine2 moved2 = MoveNearerEnd(line2, corner);

            // The end that stays is the one farther from the corner, so it lies at least half the segment's
            // length away and neither result can collapse. The check guards that reasoning, not a case.
            if (moved1.IsDegenerate(tolerance) || moved2.IsDegenerate(tolerance))
            {
                return false;
            }

            result1 = moved1;
            result2 = moved2;
            return true;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// A segment seen from the end that stays: that end, the unit direction towards the end that moves,
        /// and the distance between them. Positions along it are measured from the end that stays.
        /// </summary>
        private readonly struct Reach
        {
            public Reach(GeoPoint2 anchor, GeoVector2 unit, double length)
            {
                Anchor = anchor;
                Unit = unit;
                Length = length;
            }

            public GeoPoint2 Anchor { get; }

            public GeoVector2 Unit { get; }

            public double Length { get; }

            public GeoPoint2 At(double position) => Anchor.Add(Unit.Multiply(position));
        }

        /// <summary>
        /// Collects the positions along the reach where the line meets a boundary.
        /// </summary>
        private delegate void CrossingCollector(Reach reach, List<double> found);

        /// <summary>
        /// Moves one end of a segment to the nearest boundary position on the side asked for.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="outward">true to extend past the end; false to trim back inside the segment.</param>
        /// <param name="result">The moved segment, or the segment unchanged on failure.</param>
        /// <param name="collect">Adds the positions where the line meets the boundary.</param>
        private static bool TryMove(GeoLine2 line, LineEnd end, Tolerance tolerance, bool outward, out GeoLine2 result, CrossingCollector collect)
        {
            ValidateEnd(end);
            result = line;

            if (line.IsDegenerate(tolerance))
            {
                return false;
            }

            double length = line.Length;
            GeoPoint2 anchor = end == LineEnd.End ? line.StartPoint : line.EndPoint;
            GeoPoint2 moving = end == LineEnd.End ? line.EndPoint : line.StartPoint;
            Reach reach = new Reach(anchor, anchor.GetVectorTo(moving).Multiply(1.0 / length), length);

            List<double> found = new List<double>();
            collect(reach, found);

            // A crossing within tolerance of the end counts on both sides: the end already lies on the boundary
            // and is only snapped onto it. Whatever the side, the end must stay clear of the one that does not
            // move, so the segment keeps a direction and never turns round.
            bool picked = false;
            double best = 0.0;

            foreach (double position in found)
            {
                if (double.IsNaN(position) || position <= tolerance.EqualPoint)
                {
                    continue;
                }

                bool onSide = outward
                    ? position >= length - tolerance.EqualPoint
                    : position <= length + tolerance.EqualPoint;

                if (onSide && (!picked || (outward ? position < best : position > best)))
                {
                    best = position;
                    picked = true;
                }
            }

            if (!picked)
            {
                return false;
            }

            GeoPoint2 moved = reach.At(best);
            result = end == LineEnd.End ? new GeoLine2(line.StartPoint, moved) : new GeoLine2(moved, line.EndPoint);
            return true;
        }

        /// <summary>
        /// Adds the position where the line carrying the reach crosses a boundary segment, if it does.
        /// </summary>
        private static void AddCrossing(List<double> found, Reach reach, GeoLine2 edge, Tolerance tolerance)
        {
            GeoVector2 edgeDirection = edge.Direction;
            double edgeLength = edgeDirection.Length;
            GeoVector2 toEdge = reach.Anchor.GetVectorTo(edge.StartPoint);

            if (edgeLength <= tolerance.EqualPoint)
            {
                // An edge too short to have a direction is a point, met when the line passes within tolerance.
                if (Math.Abs(reach.Unit.CrossProduct(toEdge)) <= tolerance.EqualPoint)
                {
                    found.Add(toEdge.DotProduct(reach.Unit));
                }

                return;
            }

            // The unit direction makes the cross product the edge length times the sine of the angle between
            // them, so this is the same angular test Intersection2 and Parallel2 apply.
            double denominator = reach.Unit.CrossProduct(edgeDirection);

            if (Math.Abs(denominator) <= tolerance.EqualAngleSin * edgeLength)
            {
                return;
            }

            double position = toEdge.CrossProduct(edgeDirection) / denominator;
            double along = toEdge.CrossProduct(reach.Unit) / denominator;
            double slack = tolerance.EqualPoint / edgeLength;

            if (along >= -slack && along <= 1.0 + slack)
            {
                found.Add(position);
            }
        }

        /// <summary>
        /// Adds the positions where the line carrying the reach crosses any of a set of boundary edges.
        /// </summary>
        private static void AddCrossings(List<double> found, Reach reach, IEnumerable<GeoLine2> edges, Tolerance tolerance)
        {
            foreach (GeoLine2 edge in edges)
            {
                AddCrossing(found, reach, edge, tolerance);
            }
        }

        /// <summary>
        /// Adds the positions where the line carrying the reach crosses a circle. The tangency band matches
        /// <see cref="Intersection2"/>: within tolerance of touching, the line meets the circle once.
        /// </summary>
        private static void AddCrossings(List<double> found, Reach reach, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoVector2 toCenter = reach.Anchor.GetVectorTo(circle.Center);
            double foot = toCenter.DotProduct(reach.Unit);
            double offset = Math.Abs(reach.Unit.CrossProduct(toCenter));

            if (offset > circle.Radius + tolerance.EqualPoint)
            {
                return;
            }

            if (offset >= circle.Radius - tolerance.EqualPoint)
            {
                found.Add(foot);
                return;
            }

            double halfChord = Math.Sqrt(circle.Radius * circle.Radius - offset * offset);
            found.Add(foot - halfChord);
            found.Add(foot + halfChord);
        }

        /// <summary>
        /// Moves the end of a segment nearer to a point onto it; the end point on a tie.
        /// </summary>
        private static GeoLine2 MoveNearerEnd(GeoLine2 line, GeoPoint2 corner)
        {
            return corner.GetDistanceSquaredTo(line.StartPoint) < corner.GetDistanceSquaredTo(line.EndPoint)
                ? new GeoLine2(corner, line.EndPoint)
                : new GeoLine2(line.StartPoint, corner);
        }

        /// <summary>
        /// Gets the unit direction of a segment, refusing one too short to have a direction.
        /// </summary>
        private static GeoVector2 GetUnitDirection(GeoLine2 line, Tolerance tolerance)
        {
            if (line.IsDegenerate(tolerance))
            {
                throw new InvalidOperationException("The segment is too short to have a direction to lengthen along.");
            }

            return line.Direction.Multiply(1.0 / line.Length);
        }

        private static void ValidateEnd(LineEnd end)
        {
            if (end != LineEnd.Start && end != LineEnd.End)
            {
                throw new ArgumentOutOfRangeException(nameof(end), "Unknown line end.");
            }
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "The distance must be a finite number.");
            }
        }

        #endregion

        #region Fillet a corner

        /// <summary>
        /// Rounds the corner between two segments with an arc of a given radius, using the default tolerance.
        /// </summary>
        public static bool TryFilletCorner(GeoLine2 line1, GeoLine2 line2, double radius, out GeoArc2 arc, out GeoLine2 trimmed1, out GeoLine2 trimmed2)
        {
            return TryFilletCorner(line1, line2, radius, out arc, out trimmed1, out trimmed2, Tolerance.Global);
        }

        /// <summary>
        /// Rounds the corner between two segments with an arc of a given radius, within a tolerance.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="radius">The radius of the arc; it must be positive.</param>
        /// <param name="arc">The arc tangent to both segments, running from the first to the second, or an arc of no meaning when the method returns false.</param>
        /// <param name="trimmed1">The first segment ending where the arc begins.</param>
        /// <param name="trimmed2">The second segment beginning where the arc ends.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the corner had room for an arc of that radius; otherwise, false.</returns>
        /// <remarks>
        /// This is AutoCAD's FILLET with two objects picked. The corner is where the lines carrying the two
        /// segments cross, and the arc touches each of them at <c>radius / tan(half the corner)</c> from it.
        /// Each segment keeps its part on the far side of the corner, as
        /// <see cref="TryTrimExtendToCorner(GeoLine2, GeoLine2, out GeoLine2, out GeoLine2)"/> does, and gives
        /// up the part the arc replaces.
        /// <para>
        /// The corner needs room for the arc: the touching points must lie within the segments once they are
        /// trimmed or extended to it. Where they do not, or the segments are parallel, or one of them is too
        /// short to have a direction, the answer is false rather than an arc that overshoots.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number.</exception>
        public static bool TryFilletCorner(GeoLine2 line1, GeoLine2 line2, double radius, out GeoArc2 arc, out GeoLine2 trimmed1, out GeoLine2 trimmed2, Tolerance tolerance)
        {
            if (double.IsNaN(radius) || double.IsInfinity(radius) || radius <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "A fillet radius must be a positive number.");
            }

            arc = default;
            trimmed1 = line1;
            trimmed2 = line2;

            // The corner is where the lines carrying the two segments cross, whether or not either segment
            // reaches it, as a fillet of no radius finds it.
            if (!Intersection2.TryIntersectWith(line1, line2, LineExtension.Both, out GeoPoint2 corner, tolerance))
            {
                return false;
            }

            // Each segment keeps its part on the far side of the corner, so the direction that matters is
            // the one towards whichever of its ends lies further from the corner. Which way round the
            // segment was given does not come into it.
            GeoPoint2 far1 = FurtherFrom(corner, line1);
            GeoPoint2 far2 = FurtherFrom(corner, line2);

            GeoVector2 back = corner.GetVectorTo(far1);
            GeoVector2 forward = corner.GetVectorTo(far2);

            double backLength = back.Length;
            double forwardLength = forward.Length;

            if (backLength <= tolerance.EqualPoint || forwardLength <= tolerance.EqualPoint)
            {
                return false;
            }

            GeoVector2 unitBack = back.Multiply(1.0 / backLength);
            GeoVector2 unitForward = forward.Multiply(1.0 / forwardLength);

            double cosine = Math.Max(-1.0, Math.Min(1.0, unitBack.DotProduct(unitForward)));
            double corner_ = Math.Acos(cosine);
            double half = corner_ * 0.5;

            // A corner that does not turn, or folds straight back on itself, has no arc to fit in it.
            if (Math.Sin(half) <= Math.Sin(tolerance.EqualAngleRad) || Math.Tan(half) <= 0.0)
            {
                return false;
            }

            double along = radius / Math.Tan(half);

            if (along > backLength || along > forwardLength)
            {
                return false;
            }

            GeoPoint2 touch1 = corner.Add(unitBack.Multiply(along));
            GeoPoint2 touch2 = corner.Add(unitForward.Multiply(along));

            // The centre sits on the bisector, radius / sin(half) from the corner.
            GeoVector2 bisector = unitBack.Add(unitForward);

            if (!bisector.TryGetNormal(out GeoVector2 unitBisector, tolerance))
            {
                return false;
            }

            GeoPoint2 center = corner.Add(unitBisector.Multiply(radius / Math.Sin(half)));

            // The arc runs from the first segment to the second, the short way round.
            GeoVector2 toTouch1 = center.GetVectorTo(touch1);
            GeoVector2 toTouch2 = center.GetVectorTo(touch2);
            bool clockwise = toTouch1.CrossProduct(toTouch2) < 0.0;

            arc = new GeoArc2(
                center,
                radius,
                Math.Atan2(toTouch1.Y, toTouch1.X),
                Math.Atan2(toTouch2.Y, toTouch2.X),
                clockwise);

            trimmed1 = new GeoLine2(far1, touch1);
            trimmed2 = new GeoLine2(touch2, far2);
            return true;
        }

        /// <summary>
        /// Gets the end of a segment lying further from a point.
        /// </summary>
        private static GeoPoint2 FurtherFrom(GeoPoint2 point, GeoLine2 line)
        {
            return point.GetDistanceSquaredTo(line.StartPoint) >= point.GetDistanceSquaredTo(line.EndPoint)
                ? line.StartPoint
                : line.EndPoint;
        }

        #endregion
    }
}
