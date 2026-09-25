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
    /// This is the 3D counterpart of <c>Lengthen2</c> and follows the same rules. Every method that moves one
    /// end takes a <see cref="LineEnd"/>; the other end never moves, and the segment never reverses.
    /// <c>TryExtendTo</c> only ever lengthens, <c>TryTrimTo</c> only ever shortens, and an end already on
    /// the boundary satisfies both, so <c>TryExtendTo(...) || TryTrimTo(...)</c> fits an end to a boundary
    /// from either side the way Tekla Structures fits a part end to a plane.
    /// </para>
    /// <para>
    /// A boundary is met where the infinite line carrying the segment reaches it. A plane is met where the
    /// line pierces it. A polygon, face or solid is met where the line pierces its surface; a line lying in
    /// the plane of a polygon or face meets it at its edges instead, and one running parallel beside it
    /// never does. Another segment is met only where the line passes within the point tolerance of it: two
    /// lines in space usually pass each other, and then there is no crossing to extend to. A point is met at
    /// the foot of the perpendicular from it.
    /// </para>
    /// </summary>
    public static class Lengthen3
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
        public static GeoLine3 Extend(GeoLine3 line, double distance, LineEnd end) => Extend(line, distance, end, Tolerance.Global);

        /// <summary>
        /// Lengthens one end of a segment by a distance, within a tolerance. A negative distance shortens it.
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
        public static GeoLine3 Extend(GeoLine3 line, double distance, LineEnd end, Tolerance tolerance)
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
        public static GeoLine3 Extend(GeoLine3 line, double startDistance, double endDistance) => Extend(line, startDistance, endDistance, Tolerance.Global);

        /// <summary>
        /// Lengthens both ends of a segment, each by its own distance, within a tolerance. A negative distance
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
        public static GeoLine3 Extend(GeoLine3 line, double startDistance, double endDistance, Tolerance tolerance)
        {
            RequireFinite(startDistance, nameof(startDistance));
            RequireFinite(endDistance, nameof(endDistance));

            GeoVector3 unit = GetUnitDirection(line, tolerance);

            if (line.Length + startDistance + endDistance <= tolerance.EqualPoint)
            {
                throw new ArgumentOutOfRangeException(
                    startDistance < 0.0 ? nameof(startDistance) : nameof(endDistance),
                    "Shortening by this much would leave nothing of the segment, or turn it round.");
            }

            return new GeoLine3(
                line.StartPoint.Subtract(unit.Multiply(startDistance)),
                line.EndPoint.Add(unit.Multiply(endDistance)));
        }

        /// <summary>
        /// Moves one end of a segment so that the segment measures a given length, using the default
        /// tolerance. The other end stays, so a length shorter than the segment trims it.
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
        public static GeoLine3 ExtendToLength(GeoLine3 line, double length, LineEnd end) => ExtendToLength(line, length, end, Tolerance.Global);

        /// <summary>
        /// Moves one end of a segment so that the segment measures a given length, within a tolerance. The
        /// other end stays, so a length shorter than the segment trims it.
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
        public static GeoLine3 ExtendToLength(GeoLine3 line, double length, LineEnd end, Tolerance tolerance)
        {
            ValidateEnd(end);
            RequireFinite(length, nameof(length));

            if (length <= tolerance.EqualPoint)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "A segment must be longer than the point tolerance.");
            }

            GeoVector3 unit = GetUnitDirection(line, tolerance);

            return end == LineEnd.End
                ? new GeoLine3(line.StartPoint, line.StartPoint.Add(unit.Multiply(length)))
                : new GeoLine3(line.EndPoint.Subtract(unit.Multiply(length)), line.EndPoint);
        }

        #endregion

        #region Extend to a boundary

        /// <summary>
        /// Extends one end of a segment to the foot of the perpendicular from a point, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">The point the end is brought level with.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the foot lies at or beyond the end; false if reaching it would shorten the segment.</returns>
        public static bool TryExtendTo(GeoLine3 line, GeoPoint3 point, LineEnd end, out GeoLine3 result) => TryExtendTo(line, point, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment to the foot of the perpendicular from a point, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">The point the end is brought level with.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the foot lies at or beyond the end; false if reaching it would shorten the segment.</returns>
        public static bool TryExtendTo(GeoLine3 line, GeoPoint3 point, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, true, out result, (reach, found) => found.Add(reach.Anchor.GetVectorTo(point).DotProduct(reach.Unit)));
        }

        /// <summary>
        /// Extends one end of a segment until it meets another segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The boundary segment, which is not extended itself.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the extension meets the boundary; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine3 line, GeoLine3 boundary, LineEnd end, out GeoLine3 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it meets another segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The boundary segment, which is not extended itself.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the extension meets the boundary; otherwise, false.</returns>
        /// <remarks>
        /// The extension must pass within the point tolerance of the boundary. To bring an end level with an
        /// axis it passes at a distance, as Tekla's <c>Intersection.LineToLine</c> is used for, take the
        /// bridge from <see cref="Projection3.GetShortestLineTo(GeoLine3, GeoLine3, LineExtension, Tolerance)"/>
        /// with <see cref="LineExtension.Both"/> and extend to its start point.
        /// </remarks>
        public static bool TryExtendTo(GeoLine3 line, GeoLine3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddCrossing(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Extends one end of a segment until it pierces a plane, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the plane lies at or beyond the end; false if it is behind or parallel.</returns>
        public static bool TryExtendTo(GeoLine3 line, GeoPlane3 plane, LineEnd end, out GeoLine3 result) => TryExtendTo(line, plane, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it pierces a plane, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the plane lies at or beyond the end; false if it is behind or parallel.</returns>
        public static bool TryExtendTo(GeoLine3 line, GeoPlane3 plane, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddPlaneCrossing(found, reach, plane, tolerance));
        }

        /// <summary>
        /// Extends one end of a segment until it meets a polygon, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polygon.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the extension meets the polygon; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine3 line, GeoPolygon3 boundary, LineEnd end, out GeoLine3 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it meets a polygon, within a tolerance: where it pierces the
        /// surface, or at an edge when the segment lies in the plane of the polygon.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polygon.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the extension meets the polygon; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool TryExtendTo(GeoLine3 line, GeoPolygon3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddCrossings(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Extends one end of a segment until it meets a face, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The face.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the extension meets the face; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine3 line, GeoFace3 boundary, LineEnd end, out GeoLine3 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it meets a face, within a tolerance: where it pierces the
        /// material, so a line through a hole passes on, or at an edge of the boundary or of a hole when the
        /// segment lies in the plane of the face.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The face.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the extension meets the face; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool TryExtendTo(GeoLine3 line, GeoFace3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddCrossings(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Extends one end of a segment until it meets the surface of a solid, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The solid.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the extension meets the surface; otherwise, false.</returns>
        public static bool TryExtendTo(GeoLine3 line, GeoSolid3 boundary, LineEnd end, out GeoLine3 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends one end of a segment until it meets the surface of a solid, within a tolerance. An end
        /// outside runs in to the face it first reaches; an end inside runs out to where the line leaves.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The solid.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The extended segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the extension meets the surface; otherwise, false.</returns>
        /// <remarks>
        /// Only the faces of the body itself count. The openings carried by the solid are not read, so a line
        /// running into a hole stops at the body's outer face, not at the wall of the hole.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool TryExtendTo(GeoLine3 line, GeoSolid3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, true, out result, (reach, found) => AddCrossings(found, reach, boundary, tolerance));
        }

        #endregion

        #region Trim to a boundary

        /// <summary>
        /// Trims one end of a segment back to the foot of the perpendicular from a point, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">The point the end is brought level with.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the foot lies within the segment, short of the other end; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine3 line, GeoPoint3 point, LineEnd end, out GeoLine3 result) => TryTrimTo(line, point, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to the foot of the perpendicular from a point, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="point">The point the end is brought level with.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the foot lies within the segment, short of the other end; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine3 line, GeoPoint3 point, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, false, out result, (reach, found) => found.Add(reach.Anchor.GetVectorTo(point).DotProduct(reach.Unit)));
        }

        /// <summary>
        /// Trims one end of a segment back to where another segment crosses it, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The boundary segment.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the boundary crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine3 line, GeoLine3 boundary, LineEnd end, out GeoLine3 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to where another segment crosses it, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The boundary segment.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the boundary crosses the segment; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine3 line, GeoLine3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddCrossing(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Trims one end of a segment back to where it pierces a plane, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the plane crosses the segment short of the other end; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine3 line, GeoPlane3 plane, LineEnd end, out GeoLine3 result) => TryTrimTo(line, plane, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to where it pierces a plane, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the plane crosses the segment short of the other end; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine3 line, GeoPlane3 plane, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddPlaneCrossing(found, reach, plane, tolerance));
        }

        /// <summary>
        /// Trims one end of a segment back to the nearest place it meets a polygon, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polygon.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the polygon meets the segment short of the other end; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine3 line, GeoPolygon3 boundary, LineEnd end, out GeoLine3 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to the nearest place it meets a polygon, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The polygon.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polygon meets the segment short of the other end; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool TryTrimTo(GeoLine3 line, GeoPolygon3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddCrossings(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Trims one end of a segment back to the nearest place it meets a face, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The face.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the face meets the segment short of the other end; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine3 line, GeoFace3 boundary, LineEnd end, out GeoLine3 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to the nearest place it meets a face, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The face.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the face meets the segment short of the other end; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool TryTrimTo(GeoLine3 line, GeoFace3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddCrossings(found, reach, boundary, tolerance));
        }

        /// <summary>
        /// Trims one end of a segment back to the nearest place it meets the surface of a solid, using the
        /// default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The solid.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <returns>true if the surface meets the segment short of the other end; otherwise, false.</returns>
        public static bool TryTrimTo(GeoLine3 line, GeoSolid3 boundary, LineEnd end, out GeoLine3 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims one end of a segment back to the nearest place it meets the surface of a solid, within a
        /// tolerance. A segment running out of a body is cut where it leaves it.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="boundary">The solid.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="result">The trimmed segment, or the segment unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the surface meets the segment short of the other end; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool TryTrimTo(GeoLine3 line, GeoSolid3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));

            return TryMove(line, end, tolerance, false, out result, (reach, found) => AddCrossings(found, reach, boundary, tolerance));
        }

        #endregion

        #region Corner

        /// <summary>
        /// Trims or extends two segments so that they meet in a corner, using the default tolerance.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="result1">The first segment ending at the corner, or unchanged when the method returns false.</param>
        /// <param name="result2">The second segment ending at the corner, or unchanged when the method returns false.</param>
        /// <returns>true if the two lines meet; false if they are parallel, pass each other, or one is too short.</returns>
        public static bool TryTrimExtendToCorner(GeoLine3 line1, GeoLine3 line2, out GeoLine3 result1, out GeoLine3 result2)
            => TryTrimExtendToCorner(line1, line2, out result1, out result2, Tolerance.Global);

        /// <summary>
        /// Trims or extends two segments so that they meet in a corner, within a tolerance.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="result1">The first segment ending at the corner, or unchanged when the method returns false.</param>
        /// <param name="result2">The second segment ending at the corner, or unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two lines meet; false if they are parallel, pass each other, or one is too short.</returns>
        /// <remarks>
        /// The lines carrying the segments must meet: at their closest approach they may be at most the point
        /// tolerance apart, and the corner is placed half way across that gap so that both results end on the
        /// same point. Each segment keeps its end farther from the corner, as in <c>Lengthen2</c>.
        /// </remarks>
        public static bool TryTrimExtendToCorner(GeoLine3 line1, GeoLine3 line2, out GeoLine3 result1, out GeoLine3 result2, Tolerance tolerance)
        {
            result1 = line1;
            result2 = line2;

            if (line1.IsDegenerate(tolerance) || line2.IsDegenerate(tolerance) || Parallel3.IsParallel(line1, line2, tolerance))
            {
                return false;
            }

            GeoLine3 bridge = Projection3.GetShortestLineTo(line1, line2, LineExtension.Both, tolerance);

            if (bridge.Length > tolerance.EqualPoint)
            {
                return false;
            }

            GeoPoint3 corner = bridge.MidPoint;
            GeoLine3 moved1 = MoveNearerEnd(line1, corner);
            GeoLine3 moved2 = MoveNearerEnd(line2, corner);

            // The end that stays lies at least half the segment's length from the corner, so neither result
            // can collapse. The check guards that reasoning, not a case.
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
            public Reach(GeoPoint3 anchor, GeoVector3 unit, double length)
            {
                Anchor = anchor;
                Unit = unit;
                Length = length;
            }

            public GeoPoint3 Anchor { get; }

            public GeoVector3 Unit { get; }

            public double Length { get; }

            public GeoPoint3 At(double position) => Anchor.Add(Unit.Multiply(position));
        }

        /// <summary>
        /// Collects the positions along the reach where the line meets a boundary.
        /// </summary>
        private delegate void CrossingCollector(Reach reach, List<double> found);

        /// <summary>
        /// Moves one end of a segment to the nearest boundary position on the side asked for.
        /// </summary>
        private static bool TryMove(GeoLine3 line, LineEnd end, Tolerance tolerance, bool outward, out GeoLine3 result, CrossingCollector collect)
        {
            ValidateEnd(end);
            result = line;

            if (line.IsDegenerate(tolerance))
            {
                return false;
            }

            double length = line.Length;
            GeoPoint3 anchor = end == LineEnd.End ? line.StartPoint : line.EndPoint;
            GeoPoint3 moving = end == LineEnd.End ? line.EndPoint : line.StartPoint;
            Reach reach = new Reach(anchor, anchor.GetVectorTo(moving).Divide(length), length);

            List<double> found = new List<double>();
            collect(reach, found);

            // A crossing within tolerance of the end counts on both sides: the end already lies on the boundary
            // and is only snapped onto it. The end must stay clear of the one that does not move, so the
            // segment keeps a direction and never turns round.
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

            GeoPoint3 moved = reach.At(best);
            result = end == LineEnd.End ? new GeoLine3(line.StartPoint, moved) : new GeoLine3(moved, line.EndPoint);
            return true;
        }

        /// <summary>
        /// Adds the position where the line carrying the reach meets a boundary segment, if it passes within
        /// the point tolerance of it.
        /// </summary>
        private static void AddCrossing(List<double> found, Reach reach, GeoLine3 edge, Tolerance tolerance)
        {
            GeoVector3 edgeDirection = edge.Direction;
            double edgeLength = edgeDirection.Length;

            if (edgeLength <= tolerance.EqualPoint)
            {
                // An edge too short to have a direction is a point, met when the line passes within tolerance.
                GeoVector3 toPoint = reach.Anchor.GetVectorTo(edge.StartPoint);

                if (toPoint.CrossProduct(reach.Unit).Length <= tolerance.EqualPoint)
                {
                    found.Add(toPoint.DotProduct(reach.Unit));
                }

                return;
            }

            // The unit direction makes the cross product the edge length times the sine of the angle between
            // them, the same angular test Parallel3 applies. A parallel edge is missed or shared along a
            // stretch, and neither is a single place to stop.
            if (reach.Unit.CrossProduct(edgeDirection).Length <= tolerance.EqualAngleSin * edgeLength)
            {
                return;
            }

            GeoLine3 carrier = new GeoLine3(reach.Anchor, reach.Anchor.Add(reach.Unit));
            GeoLine3 bridge = Projection3.GetShortestLineTo(carrier, edge, LineExtension.First, tolerance);

            if (bridge.Length <= tolerance.EqualPoint)
            {
                found.Add(reach.Anchor.GetVectorTo(bridge.StartPoint).DotProduct(reach.Unit));
            }
        }

        /// <summary>
        /// Adds the position where the line carrying the reach pierces a plane, unless it runs parallel.
        /// </summary>
        private static void AddPlaneCrossing(List<double> found, Reach reach, GeoPlane3 plane, Tolerance tolerance)
        {
            double denominator = reach.Unit.DotProduct(plane.Normal);

            // Both are unit vectors, so this is the sine of the angle between the line and the plane, compared
            // against the angular threshold as Intersection3 does.
            if (Math.Abs(denominator) <= tolerance.EqualAngleSin)
            {
                return;
            }

            found.Add(reach.Anchor.GetVectorTo(plane.Origin).DotProduct(plane.Normal) / denominator);
        }

        /// <summary>
        /// Adds the positions where the line carrying the reach meets a flat region: where it pierces the
        /// region, or, for a line lying in the region's plane, where it crosses the region's edges.
        /// </summary>
        private static void AddRegionCrossings(
            List<double> found,
            Reach reach,
            GeoPlane3 plane,
            Func<GeoPoint3, bool> contains,
            IEnumerable<GeoLine3> edges,
            Tolerance tolerance)
        {
            double denominator = reach.Unit.DotProduct(plane.Normal);

            if (Math.Abs(denominator) > tolerance.EqualAngleSin)
            {
                double position = reach.Anchor.GetVectorTo(plane.Origin).DotProduct(plane.Normal) / denominator;

                if (contains(reach.At(position)))
                {
                    found.Add(position);
                }

                return;
            }

            // Running along the plane: a line lying in it meets the region where it crosses an edge, and one
            // beside it never meets it at all.
            if (Math.Abs(plane.SignedDistanceTo(reach.Anchor)) <= tolerance.EqualPlanar)
            {
                foreach (GeoLine3 edge in edges)
                {
                    AddCrossing(found, reach, edge, tolerance);
                }
            }
        }

        private static void AddCrossings(List<double> found, Reach reach, GeoPolygon3 polygon, Tolerance tolerance)
        {
            AddRegionCrossings(
                found,
                reach,
                polygon.GetPlane(),
                point => Containment3.Contains(polygon, point, tolerance),
                polygon.GetEdges(),
                tolerance);
        }

        private static void AddCrossings(List<double> found, Reach reach, GeoFace3 face, Tolerance tolerance)
        {
            List<GeoLine3> edges = new List<GeoLine3>(face.Boundary.GetEdges());

            foreach (GeoPolygon3 hole in face.Holes)
            {
                edges.AddRange(hole.GetEdges());
            }

            AddRegionCrossings(
                found,
                reach,
                face.GetPlane(),
                point => Containment3.Contains(face, point, tolerance),
                edges,
                tolerance);
        }

        private static void AddCrossings(List<double> found, Reach reach, GeoSolid3 solid, Tolerance tolerance)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                AddCrossings(found, reach, face, tolerance);
            }
        }

        /// <summary>
        /// Moves the end of a segment nearer to a point onto it; the end point on a tie.
        /// </summary>
        private static GeoLine3 MoveNearerEnd(GeoLine3 line, GeoPoint3 corner)
        {
            return corner.GetDistanceSquaredTo(line.StartPoint) < corner.GetDistanceSquaredTo(line.EndPoint)
                ? new GeoLine3(corner, line.EndPoint)
                : new GeoLine3(line.StartPoint, corner);
        }

        /// <summary>
        /// Gets the unit direction of a segment, refusing one too short to have a direction.
        /// </summary>
        private static GeoVector3 GetUnitDirection(GeoLine3 line, Tolerance tolerance)
        {
            if (line.IsDegenerate(tolerance))
            {
                throw new InvalidOperationException("The segment is too short to have a direction to lengthen along.");
            }

            return line.Direction.Divide(line.Length);
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
    }
}
