using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a 3D line segment defined by a start point and an end point.
    /// </summary>
    public readonly partial struct GeoLine3 : IEquatable<GeoLine3>
    {
        /// <summary>
        /// Gets the start point of the line segment.
        /// </summary>
        public GeoPoint3 StartPoint { get; }

        /// <summary>
        /// Gets the end point of the line segment.
        /// </summary>
        public GeoPoint3 EndPoint { get; }

        /// <summary>
        /// Initializes a new line segment from its endpoints.
        /// </summary>
        /// <param name="startPoint">Start point.</param>
        /// <param name="endPoint">End point.</param>
        public GeoLine3(GeoPoint3 startPoint, GeoPoint3 endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        /// <summary>
        /// Initializes a new line segment from the coordinates of its endpoints.
        /// </summary>
        public GeoLine3(double startX, double startY, double startZ, double endX, double endY, double endZ)
            : this(new GeoPoint3(startX, startY, startZ), new GeoPoint3(endX, endY, endZ))
        {
        }

        /// <summary>
        /// Creates a copy of this line segment.
        /// </summary>
        /// <remarks>
        /// Line segment is a readonly struct, so plain assignment already produces an independent copy and
        /// this method is not needed to avoid sharing. It exists so that every geometry type offers the
        /// same way to ask for a copy.
        /// </remarks>
        public GeoLine3 Clone() => new GeoLine3(StartPoint, EndPoint);

        /// <summary>
        /// Gets the vector pointing from the start point to the end point. Its length is the length of
        /// the segment.
        /// </summary>
        public GeoVector3 Direction => StartPoint.GetVectorTo(EndPoint);

        /// <summary>
        /// Gets the length of the line segment.
        /// </summary>
        public double Length => StartPoint.DistanceTo(EndPoint);

        /// <summary>
        /// Gets the squared length of the line segment.
        /// </summary>
        public double LengthSquared => StartPoint.GetDistanceSquaredTo(EndPoint);

        /// <summary>
        /// Gets the midpoint of the line segment.
        /// </summary>
        public GeoPoint3 MidPoint => StartPoint.GetMiddlePoint(EndPoint);

        /// <summary>
        /// Gets the line segment running the other way.
        /// </summary>
        public GeoLine3 Reverse() => new GeoLine3(EndPoint, StartPoint);

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this segment.
        /// </summary>
        public GeoAabb3 GetAabb() => new GeoAabb3(StartPoint, EndPoint);

        /// <summary>
        /// Moves the line segment by a vector.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The line segment in its new place.</returns>
        public GeoLine3 Translate(GeoVector3 vector) => new GeoLine3(StartPoint.Add(vector), EndPoint.Add(vector));

        /// <summary>
        /// Applies a transformation to this segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoLine3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Gets the infinite plane through this segment carrying a given in-plane direction.
        /// </summary>
        /// <param name="inPlaneDirection">A second direction the plane must contain.</param>
        /// <returns>The plane through the start point spanned by this segment and the supplied direction.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the segment is degenerate or the two directions are parallel, since neither case
        /// pins down a plane.
        /// </exception>
        public GeoPlane3 GetPlaneWith(GeoVector3 inPlaneDirection)
        {
            return new GeoPlane3(StartPoint, Direction.CrossProduct(inPlaneDirection));
        }

        #region Parametrization

        /// <summary>
        /// Gets the point at a normalized parameter along the segment. 0 is the start point and 1 the end
        /// point; values outside that range extrapolate along the infinite line carrying the segment.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter) => Parametrization3.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter of the point on the infinite line carrying this segment that is
        /// closest to the supplied point. The point need not lie on the segment, so the result may fall
        /// outside [0, 1].
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => Parametrization3.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the point at an arc length measured from the start point.
        /// </summary>
        public GeoPoint3 GetPointAtDistance(double distance) => Parametrization3.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the arc length from the start point to the point on this segment closest to the supplied point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => Parametrization3.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the start point to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization3.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start point.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization3.GetParameterAtDistance(this, distance);

        #endregion

        #region Predicates and intersection

        /// <summary>
        /// Checks whether the segment is shorter than the default tolerance, so it has no usable direction.
        /// </summary>
        public bool IsDegenerate() => IsDegenerate(Tolerance.Global);

        /// <summary>
        /// Checks whether the segment is shorter than a tolerance, so it has no usable direction.
        /// </summary>
        public bool IsDegenerate(Tolerance tolerance) => Direction.IsZeroLength(tolerance);

        /// <summary>
        /// Checks whether a point lies on this segment using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => Containment3.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on this segment within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => Containment3.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Checks whether this segment is parallel to another segment using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine3 other) => Parallel3.IsParallel(this, other);

        /// <summary>
        /// Checks whether this segment is parallel to another segment within a tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine3 other, Tolerance tolerance) => Parallel3.IsParallel(this, other, tolerance);

        /// <summary>
        /// Checks whether this segment is perpendicular to another segment using the default tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoLine3 other) => Parallel3.IsPerpendicular(this, other);

        /// <summary>
        /// Checks whether this segment is perpendicular to another segment within a tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoLine3 other, Tolerance tolerance) => Parallel3.IsPerpendicular(this, other, tolerance);

        /// <summary>
        /// Checks whether this segment runs parallel to a plane using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoPlane3 plane) => Parallel3.IsParallel(this, plane);

        /// <summary>
        /// Checks whether this segment runs parallel to a plane within a tolerance.
        /// </summary>
        public bool IsParallelTo(GeoPlane3 plane, Tolerance tolerance) => Parallel3.IsParallel(this, plane, tolerance);

        /// <summary>
        /// Checks whether this segment and another segment lie on a common plane, using the default tolerance.
        /// </summary>
        public bool IsCoplanarWith(GeoLine3 other) => Parallel3.IsCoplanar(this, other);

        /// <summary>
        /// Checks whether this segment and another segment lie on a common plane, within a tolerance.
        /// </summary>
        public bool IsCoplanarWith(GeoLine3 other, Tolerance tolerance) => Parallel3.IsCoplanar(this, other, tolerance);

        #endregion

        #region Lengthening and trimming

        /// <summary>
        /// Lengthens one end of this segment by a distance, using the default tolerance. A negative distance
        /// shortens it. See <see cref="Lengthen3"/>.
        /// </summary>
        /// <param name="distance">How far the end moves outward; negative moves it back.</param>
        /// <param name="end">The end that moves.</param>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite or would leave nothing of the segment.</exception>
        public GeoLine3 Extend(double distance, LineEnd end) => Lengthen3.Extend(this, distance, end);

        /// <summary>
        /// Lengthens one end of this segment by a distance, within a tolerance. A negative distance shortens it.
        /// </summary>
        /// <param name="distance">How far the end moves outward; negative moves it back.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite or would leave nothing of the segment.</exception>
        public GeoLine3 Extend(double distance, LineEnd end, Tolerance tolerance) => Lengthen3.Extend(this, distance, end, tolerance);

        /// <summary>
        /// Lengthens both ends of this segment, each by its own distance, using the default tolerance. A
        /// negative distance shortens that end.
        /// </summary>
        /// <param name="startDistance">How far the start point moves outward.</param>
        /// <param name="endDistance">How far the end point moves outward.</param>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a distance is not finite or would leave nothing of the segment.</exception>
        public GeoLine3 Extend(double startDistance, double endDistance) => Lengthen3.Extend(this, startDistance, endDistance);

        /// <summary>
        /// Lengthens both ends of this segment, each by its own distance, within a tolerance. A negative
        /// distance shortens that end.
        /// </summary>
        /// <param name="startDistance">How far the start point moves outward.</param>
        /// <param name="endDistance">How far the end point moves outward.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a distance is not finite or would leave nothing of the segment.</exception>
        public GeoLine3 Extend(double startDistance, double endDistance, Tolerance tolerance) => Lengthen3.Extend(this, startDistance, endDistance, tolerance);

        /// <summary>
        /// Moves one end of this segment so that it measures a given length, using the default tolerance.
        /// </summary>
        /// <param name="length">The length the segment should have.</param>
        /// <param name="end">The end that moves.</param>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is not a finite number above the tolerance.</exception>
        public GeoLine3 ExtendToLength(double length, LineEnd end) => Lengthen3.ExtendToLength(this, length, end);

        /// <summary>
        /// Moves one end of this segment so that it measures a given length, within a tolerance.
        /// </summary>
        /// <param name="length">The length the segment should have.</param>
        /// <param name="end">The end that moves.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is not a finite number above the tolerance.</exception>
        public GeoLine3 ExtendToLength(double length, LineEnd end, Tolerance tolerance) => Lengthen3.ExtendToLength(this, length, end, tolerance);

        /// <summary>
        /// Extends one end of this segment to the foot of the perpendicular from a point, using the default tolerance.
        /// </summary>
        public bool TryExtendTo(GeoPoint3 point, LineEnd end, out GeoLine3 result) => Lengthen3.TryExtendTo(this, point, end, out result);

        /// <summary>
        /// Extends one end of this segment to the foot of the perpendicular from a point, within a tolerance.
        /// </summary>
        public bool TryExtendTo(GeoPoint3 point, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryExtendTo(this, point, end, out result, tolerance);

        /// <summary>
        /// Extends one end of this segment until it meets another segment, using the default tolerance.
        /// </summary>
        public bool TryExtendTo(GeoLine3 boundary, LineEnd end, out GeoLine3 result) => Lengthen3.TryExtendTo(this, boundary, end, out result);

        /// <summary>
        /// Extends one end of this segment until it meets another segment, within a tolerance.
        /// </summary>
        public bool TryExtendTo(GeoLine3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryExtendTo(this, boundary, end, out result, tolerance);

        /// <summary>
        /// Extends one end of this segment until it pierces a plane, using the default tolerance.
        /// </summary>
        public bool TryExtendTo(GeoPlane3 plane, LineEnd end, out GeoLine3 result) => Lengthen3.TryExtendTo(this, plane, end, out result);

        /// <summary>
        /// Extends one end of this segment until it pierces a plane, within a tolerance.
        /// </summary>
        public bool TryExtendTo(GeoPlane3 plane, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryExtendTo(this, plane, end, out result, tolerance);

        /// <summary>
        /// Extends one end of this segment until it meets a polygon, using the default tolerance.
        /// </summary>
        public bool TryExtendTo(GeoPolygon3 boundary, LineEnd end, out GeoLine3 result) => Lengthen3.TryExtendTo(this, boundary, end, out result);

        /// <summary>
        /// Extends one end of this segment until it meets a polygon, within a tolerance.
        /// </summary>
        public bool TryExtendTo(GeoPolygon3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryExtendTo(this, boundary, end, out result, tolerance);

        /// <summary>
        /// Extends one end of this segment until it meets a face, using the default tolerance.
        /// </summary>
        public bool TryExtendTo(GeoFace3 boundary, LineEnd end, out GeoLine3 result) => Lengthen3.TryExtendTo(this, boundary, end, out result);

        /// <summary>
        /// Extends one end of this segment until it meets a face, within a tolerance.
        /// </summary>
        public bool TryExtendTo(GeoFace3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryExtendTo(this, boundary, end, out result, tolerance);

        /// <summary>
        /// Extends one end of this segment until it meets the surface of a solid, using the default tolerance.
        /// </summary>
        public bool TryExtendTo(GeoSolid3 boundary, LineEnd end, out GeoLine3 result) => Lengthen3.TryExtendTo(this, boundary, end, out result);

        /// <summary>
        /// Extends one end of this segment until it meets the surface of a solid, within a tolerance.
        /// </summary>
        public bool TryExtendTo(GeoSolid3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryExtendTo(this, boundary, end, out result, tolerance);

        /// <summary>
        /// Trims one end of this segment back to the foot of the perpendicular from a point, using the default tolerance.
        /// </summary>
        public bool TryTrimTo(GeoPoint3 point, LineEnd end, out GeoLine3 result) => Lengthen3.TryTrimTo(this, point, end, out result);

        /// <summary>
        /// Trims one end of this segment back to the foot of the perpendicular from a point, within a tolerance.
        /// </summary>
        public bool TryTrimTo(GeoPoint3 point, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryTrimTo(this, point, end, out result, tolerance);

        /// <summary>
        /// Trims one end of this segment back to where another segment crosses it, using the default tolerance.
        /// </summary>
        public bool TryTrimTo(GeoLine3 boundary, LineEnd end, out GeoLine3 result) => Lengthen3.TryTrimTo(this, boundary, end, out result);

        /// <summary>
        /// Trims one end of this segment back to where another segment crosses it, within a tolerance.
        /// </summary>
        public bool TryTrimTo(GeoLine3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryTrimTo(this, boundary, end, out result, tolerance);

        /// <summary>
        /// Trims one end of this segment back to where it pierces a plane, using the default tolerance.
        /// </summary>
        public bool TryTrimTo(GeoPlane3 plane, LineEnd end, out GeoLine3 result) => Lengthen3.TryTrimTo(this, plane, end, out result);

        /// <summary>
        /// Trims one end of this segment back to where it pierces a plane, within a tolerance.
        /// </summary>
        public bool TryTrimTo(GeoPlane3 plane, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryTrimTo(this, plane, end, out result, tolerance);

        /// <summary>
        /// Trims one end of this segment back to the nearest place it meets a polygon, using the default tolerance.
        /// </summary>
        public bool TryTrimTo(GeoPolygon3 boundary, LineEnd end, out GeoLine3 result) => Lengthen3.TryTrimTo(this, boundary, end, out result);

        /// <summary>
        /// Trims one end of this segment back to the nearest place it meets a polygon, within a tolerance.
        /// </summary>
        public bool TryTrimTo(GeoPolygon3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryTrimTo(this, boundary, end, out result, tolerance);

        /// <summary>
        /// Trims one end of this segment back to the nearest place it meets a face, using the default tolerance.
        /// </summary>
        public bool TryTrimTo(GeoFace3 boundary, LineEnd end, out GeoLine3 result) => Lengthen3.TryTrimTo(this, boundary, end, out result);

        /// <summary>
        /// Trims one end of this segment back to the nearest place it meets a face, within a tolerance.
        /// </summary>
        public bool TryTrimTo(GeoFace3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryTrimTo(this, boundary, end, out result, tolerance);

        /// <summary>
        /// Trims one end of this segment back to the nearest place it meets the surface of a solid, using the default tolerance.
        /// </summary>
        public bool TryTrimTo(GeoSolid3 boundary, LineEnd end, out GeoLine3 result) => Lengthen3.TryTrimTo(this, boundary, end, out result);

        /// <summary>
        /// Trims one end of this segment back to the nearest place it meets the surface of a solid, within a tolerance.
        /// </summary>
        public bool TryTrimTo(GeoSolid3 boundary, LineEnd end, out GeoLine3 result, Tolerance tolerance) => Lengthen3.TryTrimTo(this, boundary, end, out result, tolerance);

        /// <summary>
        /// Trims or extends this segment and another so that they meet in a corner, using the default tolerance.
        /// </summary>
        public bool TryTrimExtendToCorner(GeoLine3 other, out GeoLine3 trimmedThis, out GeoLine3 trimmedOther) => Lengthen3.TryTrimExtendToCorner(this, other, out trimmedThis, out trimmedOther);

        /// <summary>
        /// Trims or extends this segment and another so that they meet in a corner, within a tolerance.
        /// </summary>
        public bool TryTrimExtendToCorner(GeoLine3 other, out GeoLine3 trimmedThis, out GeoLine3 trimmedOther, Tolerance tolerance) => Lengthen3.TryTrimExtendToCorner(this, other, out trimmedThis, out trimmedOther, tolerance);

        #endregion

        #region Offsetting

        /// <summary>
        /// Gets the segment parallel to this one at a distance to its left within a plane, using the default
        /// tolerance. See <see cref="Offset3"/>.
        /// </summary>
        /// <param name="distance">How far to move it, to its left seen from the tip of the normal when positive.</param>
        /// <param name="planeNormal">The normal of the plane to offset within.</param>
        public GeoLine3 OffsetInPlane(double distance, GeoVector3 planeNormal) => Offset3.OffsetInPlane(this, distance, planeNormal);

        /// <summary>
        /// Gets the segment parallel to this one at a distance to its left within a plane, within a tolerance.
        /// </summary>
        /// <param name="distance">How far to move it, to its left seen from the tip of the normal when positive.</param>
        /// <param name="planeNormal">The normal of the plane to offset within.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoLine3 OffsetInPlane(double distance, GeoVector3 planeNormal, Tolerance tolerance) => Offset3.OffsetInPlane(this, distance, planeNormal, tolerance);

        /// <summary>
        /// Gets the segment parallel to this one at a distance towards a direction, using the default tolerance.
        /// </summary>
        /// <param name="distance">How far to move it.</param>
        /// <param name="direction">The side to move it to; only its part square to the segment counts.</param>
        public GeoLine3 Offset(double distance, GeoVector3 direction) => Offset3.Offset(this, distance, direction);

        /// <summary>
        /// Gets the segment parallel to this one at a distance towards a direction, within a tolerance.
        /// </summary>
        /// <param name="distance">How far to move it.</param>
        /// <param name="direction">The side to move it to; only its part square to the segment counts.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoLine3 Offset(double distance, GeoVector3 direction, Tolerance tolerance) => Offset3.Offset(this, distance, direction, tolerance);

        /// <summary>
        /// Gets the segment parallel to this one, moved sideways and upward in the frame an up direction fixes
        /// for it, using the default tolerance.
        /// </summary>
        /// <param name="lateral">How far to move it to its left, seen from above.</param>
        /// <param name="vertical">How far to move it up.</param>
        /// <param name="up">Which way is up; only its part square to the segment counts.</param>
        public GeoLine3 Offset(double lateral, double vertical, GeoVector3 up) => Offset3.Offset(this, lateral, vertical, up);

        /// <summary>
        /// Gets the segment parallel to this one, moved sideways and upward in the frame an up direction fixes
        /// for it, within a tolerance.
        /// </summary>
        /// <param name="lateral">How far to move it to its left, seen from above.</param>
        /// <param name="vertical">How far to move it up.</param>
        /// <param name="up">Which way is up; only its part square to the segment counts.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoLine3 Offset(double lateral, double vertical, GeoVector3 up, Tolerance tolerance) => Offset3.Offset(this, lateral, vertical, up, tolerance);

        /// <summary>
        /// Gets the segment parallel to this one that passes through a point, using the default tolerance.
        /// </summary>
        public GeoLine3 OffsetThrough(GeoPoint3 point) => Offset3.OffsetThrough(this, point);

        /// <summary>
        /// Gets the segment parallel to this one that passes through a point, within a tolerance.
        /// </summary>
        public GeoLine3 OffsetThrough(GeoPoint3 point, Tolerance tolerance) => Offset3.OffsetThrough(this, point, tolerance);

        #endregion

        #region Splitting

        /// <summary>
        /// Splits this segment at an arc length from its start, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// Splitting sits on the subject rather than on the cutter, since <c>plane.Split(line)</c> would
        /// not say which of the two comes back in pieces.
        /// </remarks>
        public bool TrySplitAtDistance(double distance, out GeoLine3[] pieces) => Splition3.TrySplitAtDistance(this, distance, out pieces);

        /// <summary>
        /// Splits this segment at an arc length from its start, within a tolerance.
        /// </summary>
        public bool TrySplitAtDistance(double distance, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitAtDistance(this, distance, out pieces, tolerance);

        /// <summary>
        /// Splits this segment at a point on it, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPoint3 point, out GeoLine3[] pieces) => Splition3.TrySplitBy(this, point, out pieces);

        /// <summary>
        /// Splits this segment at a point on it, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPoint3 point, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, point, out pieces, tolerance);

        /// <summary>
        /// Splits this segment where a plane crosses it, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoLine3[] pieces) => Splition3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Splits this segment where a plane crosses it, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Splits this segment at several arc lengths at once, using the default tolerance.
        /// </summary>
        public GeoLine3[] SplitAtDistances(System.Collections.Generic.IEnumerable<double> distances) => Splition3.SplitAtDistances(this, distances);

        /// <summary>
        /// Splits this segment at several arc lengths at once, within a tolerance.
        /// </summary>
        public GeoLine3[] SplitAtDistances(System.Collections.Generic.IEnumerable<double> distances, Tolerance tolerance) => Splition3.SplitAtDistances(this, distances, tolerance);

        /// <summary>
        /// Splits this segment by a solid, sorting the pieces into those inside it and those outside,
        /// using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoLine3[] inside, out GeoLine3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this segment by a solid, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment by several solids taken together as their union, using the default
        /// tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside) => Splition3.TrySplitBy(this, cutters, out inside, out outside);

        /// <summary>
        /// Splits this segment by several solids taken together as their union, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutters, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment by an oriented box, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this segment by an oriented box, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment by an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this segment by an axis-aligned box, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment by several oriented boxes taken together as their union, using the default
        /// tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside) => Splition3.TrySplitBy(this, cutters, out inside, out outside);

        /// <summary>
        /// Splits this segment by several oriented boxes taken together as their union, within a
        /// tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutters, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment by several axis-aligned boxes taken together as their union, using the
        /// default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside) => Splition3.TrySplitBy(this, cutters, out inside, out outside);

        /// <summary>
        /// Splits this segment by several axis-aligned boxes taken together as their union, within a
        /// tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutters, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment by a plane and sorts the pieces by side, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoLine3[] above, out GeoLine3[] below) => Splition3.TrySplitBy(this, cutter, out above, out below);

        /// <summary>
        /// Splits this segment by a plane and sorts the pieces by side, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoLine3[] above, out GeoLine3[] below, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out above, out below, tolerance);

        /// <summary>
        /// Splits this segment wherever it passes through a polygon, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolygon3 cutter, out GeoLine3[] pieces) => Splition3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Splits this segment wherever it passes through a polygon, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolygon3 cutter, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Splits this segment wherever it passes through a face, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoFace3 cutter, out GeoLine3[] pieces) => Splition3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Splits this segment wherever it passes through a face, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoFace3 cutter, out GeoLine3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out pieces, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another segment has exactly the same endpoints, in the same order.
        /// </summary>
        public bool Equals(GeoLine3 other) => StartPoint.Equals(other.StartPoint) && EndPoint.Equals(other.EndPoint);

        /// <summary>
        /// Determines whether the specified object is equal to the current segment.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoLine3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (StartPoint.GetHashCode() * 397) ^ EndPoint.GetHashCode();
            }
        }

        /// <summary>
        /// Compares whether this segment equals another segment using the default tolerance, ignoring
        /// which way round the endpoints are given.
        /// </summary>
        public bool IsEqualTo(GeoLine3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this segment equals another segment within a tolerance, ignoring which way
        /// round the endpoints are given.
        /// </summary>
        public bool IsEqualTo(GeoLine3 other, Tolerance tolerance)
        {
            return (StartPoint.IsEqualTo(other.StartPoint, tolerance) && EndPoint.IsEqualTo(other.EndPoint, tolerance)) ||
                   (StartPoint.IsEqualTo(other.EndPoint, tolerance) && EndPoint.IsEqualTo(other.StartPoint, tolerance));
        }

        /// <summary>
        /// Checks if two segments have exactly the same endpoints, in the same order.
        /// </summary>
        public static bool operator ==(GeoLine3 left, GeoLine3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two segments differ in either endpoint.
        /// </summary>
        public static bool operator !=(GeoLine3 left, GeoLine3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current line segment.
        /// </summary>
        public override string ToString() => $"{StartPoint} -> {EndPoint}";

        /// <summary>
        /// Lays this segment out in a frame, dropping each end's distance from the plane of that frame.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The segment in the plane of the frame.</returns>
        public GeoLine2 ProjectToLine2(GeoCoordinateSystem3 frame) => PlanarMap.ProjectToLine2(frame, this);
    }
}
