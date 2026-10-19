using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents an infinite line in space: a point on it and a direction it runs along both ways for ever, the
    /// counterpart of <see cref="GeoRay3"/> with no start, of <see cref="GeoXLine2"/> in the plane, and of the XLINE of AutoCAD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The line from (-50, 50, 10) along X passes through the box 100 by 100 by 20 with a corner at the origin, entering it at
    /// (0, 50, 10) and leaving it at (100, 50, 10); a ray from the same point turned round runs away from the box, but this
    /// line does not leave it behind. The same line is also the line from (500, 50, 10) along -X:
    /// <see cref="Equals(GeoXLine3)"/> reads the fields and tells the two apart, <see cref="IsSameLineAs(GeoXLine3)"/> reads the
    /// line and does not.
    /// </para>
    /// <para>
    /// It is asked what a segment is asked, as <see cref="GeoRay3"/> is, and a closed shape is read as filled. Two lines in space
    /// are skew unless they happen to cross: they are as far apart as the common perpendicular between them is long, and nought
    /// apart where they meet.
    /// </para>
    /// </remarks>
    public readonly partial struct GeoXLine3 : IEquatable<GeoXLine3>
    {
        /// <summary>
        /// Gets a point on the line, from which distances along it are measured.
        /// </summary>
        public GeoPoint3 Origin { get; }

        /// <summary>
        /// Gets the direction of the line, always normalized. The line runs along it and against it from the origin.
        /// </summary>
        public GeoVector3 Direction { get; }

        /// <summary>
        /// Initializes a new infinite line.
        /// </summary>
        /// <param name="origin">A point on the line.</param>
        /// <param name="direction">The direction vector; it is normalized on construction.</param>
        /// <exception cref="ArgumentException">Thrown when the direction has zero length.</exception>
        public GeoXLine3(GeoPoint3 origin, GeoVector3 direction)
        {
            if (!direction.TryGetNormal(out GeoVector3 unit))
            {
                throw new ArgumentException("An infinite line needs a direction of non-zero length.", nameof(direction));
            }

            Origin = origin;
            Direction = unit;
        }

        /// <summary>
        /// Initializes a new infinite line through two points.
        /// </summary>
        /// <param name="origin">A point on the line.</param>
        /// <param name="through">Another point on the line; the direction runs from the first to this one.</param>
        /// <exception cref="ArgumentException">Thrown when the two points coincide.</exception>
        public GeoXLine3(GeoPoint3 origin, GeoPoint3 through)
            : this(origin, origin.GetVectorTo(through))
        {
        }

        /// <summary>
        /// Initializes a line from a direction that is already normalized.
        /// </summary>
        /// <remarks>
        /// Normalizing a vector that is already of unit length is not the identity in floating point: the
        /// computed length comes back a bit either side of one, and dividing by it shifts the last digits.
        /// Operations that only move a line around, or turn a ray into one, must not pay that, or a copy would come back
        /// unequal to its original and reversing twice would drift. This constructor is how they skip it.
        /// </remarks>
        internal GeoXLine3(GeoPoint3 origin, GeoVector3 unitDirection, bool alreadyNormalized)
        {
            Origin = origin;
            Direction = unitDirection;
        }

        /// <summary>
        /// Creates a copy of this line.
        /// </summary>
        /// <remarks>
        /// An infinite line is a readonly struct, so plain assignment already produces an independent copy and this
        /// method is not needed to avoid sharing. It exists so that every geometry type offers the same
        /// way to ask for a copy.
        /// </remarks>
        public GeoXLine3 Clone() => new GeoXLine3(Origin, Direction, true);

        /// <summary>
        /// Gets the same line running the opposite way.
        /// </summary>
        /// <remarks>
        /// The points are the same; only the direction, and so the sign of every distance along the line, is turned round.
        /// </remarks>
        public GeoXLine3 Reverse() => new GeoXLine3(Origin, Direction.Negate(), true);

        /// <summary>
        /// Moves the line by a vector.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The line in its new place.</returns>
        public GeoXLine3 Translate(GeoVector3 vector) => new GeoXLine3(Origin.Add(vector), Direction, true);

        /// <summary>
        /// Applies a transformation to this line.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed line, its direction normalized again.</returns>
        /// <remarks>
        /// The origin is moved as a point and the direction as a vector, so an uneven scaling turns the line as well as
        /// moving it: the line through the origin at 45 degrees in the floor, scaled by 2 along X only, runs along (2, 1, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation flattens the direction to nothing.</exception>
        public GeoXLine3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Gets the point at a signed distance measured from the origin along the line direction.
        /// </summary>
        /// <param name="distance">The distance from the origin; negative runs against the direction.</param>
        /// <returns>The point at that distance.</returns>
        /// <remarks>
        /// Because the direction is normalized, the parameter and the arc length are the same number here. Unlike a ray, a
        /// line has points behind its origin: from the line through (10, 20, 30) along X, the distance -30 is the point
        /// (-20, 20, 30).
        /// </remarks>
        public GeoPoint3 GetPointAtDistance(double distance) => Origin.Add(Direction.Multiply(distance));

        /// <summary>
        /// Gets the signed distance from the origin to the foot of a point on the line: positive ahead of the origin and
        /// negative behind it.
        /// </summary>
        /// <remarks>
        /// From the line through the origin along X, the point (50, 40, 30) is at 50 and the point (-30, 40, 30) at -30.
        /// </remarks>
        public double GetDistanceAtPoint(GeoPoint3 point) => Origin.GetVectorTo(point).DotProduct(Direction);

        /// <summary>
        /// Gets the line segment covering the line between two signed distances from its origin.
        /// </summary>
        /// <param name="from">The distance at which the segment starts; negative is behind the origin.</param>
        /// <param name="to">The distance at which the segment ends.</param>
        /// <returns>The segment from the point at <paramref name="from"/> to the point at <paramref name="to"/>; it runs backwards when <paramref name="to"/> is the smaller.</returns>
        /// <remarks>
        /// The line through (10, 20, 30) along X, between -30 and 70, is the segment from (-20, 20, 30) to (80, 20, 30).
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either distance is NaN or infinite.</exception>
        public GeoLine3 ToLine(double from, double to)
        {
            Guard.Finite(from, nameof(from), "An infinite line is sampled between distances that are numbers.");
            Guard.Finite(to, nameof(to), "An infinite line is sampled between distances that are numbers.");

            return new GeoLine3(GetPointAtDistance(from), GetPointAtDistance(to));
        }

        /// <summary>
        /// Gets the ray that starts at the origin of this line and runs along its direction: the half of the line ahead of the origin.
        /// </summary>
        /// <remarks>
        /// The half behind the origin is <c>Reverse().ToRay()</c>.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the line has no direction, as a <c>default</c> one has.</exception>
        public GeoRay3 ToRay()
        {
            if (!Guard.IsUnit(Direction))
            {
                throw new ArgumentException("A line with no direction has no ray.");
            }

            return new GeoRay3(Origin, Direction, true);
        }

        /// <summary>
        /// Lays this line out in a frame: its origin dropped onto the plane of that frame and its direction laid along it.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The line in the plane of the frame.</returns>
        /// <remarks>
        /// In the floor frame, the line through (0, 0, 10) along (1, 0, -1), heading down at 45 degrees, is laid out as the line
        /// through the origin along X. <see cref="GeoXLine2.ToXLine3(GeoCoordinateSystem3)"/> puts a line of the plane back.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the line runs along the normal of the frame, so that its direction has no length in the plane.
        /// </exception>
        public GeoXLine2 ProjectToXLine2(GeoCoordinateSystem3 frame) => PlanarMap.ProjectToXLine2(frame, this);

        /// <summary>
        /// Gets the part of this line inside an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <returns>The segment where the line is inside the box, running along the direction of the line; null when it misses.</returns>
        public GeoLine3? ClipTo(GeoAabb3 box) => ClipTo(box, Tolerance.Global);

        /// <summary>
        /// Gets the part of this line inside an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance: a line that misses the box by no more than the point tolerance is clipped to the box grown by it.</param>
        /// <returns>The segment where the line is inside the box, running along the direction of the line; null when it misses or the box is empty.</returns>
        /// <remarks>
        /// <para>
        /// Meant for drawing: the line through (-50, 50, 10) along X, clipped to the box 100 by 100 by 20 with a corner at the
        /// origin, is the segment from (0, 50, 10) to (100, 50, 10); the line along Y through (150, 0, 10) misses it and gives null.
        /// </para>
        /// <para>
        /// A line that crosses the box is clipped to it exactly. One that misses it by no more than the point tolerance, the
        /// line along X through (0, 50, 20.0005) over that box, touches it as <see cref="CollidesWith(GeoAabb3)"/> says, and is
        /// clipped to the box grown by the tolerance: from (-0.001, 50, 20.0005) to (100.001, 50, 20.0005), its ends no more than
        /// the tolerance outside the box on any axis. One that misses it by more gives null.
        /// </para>
        /// </remarks>
        public GeoLine3? ClipTo(GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return null;
            }

            if (TryClip(box, 0.0, out double near, out double far))
            {
                return ToLine(near, far);
            }

            // Past the tolerance it is no touch, and the box grown by it is not a place for the line to be given. Written as
            // what has to hold, so that a NaN fails it.
            if (!(DistanceTo(box, tolerance) <= tolerance.EqualPoint))
            {
                return null;
            }

            if (TryClip(box, tolerance.EqualPoint, out near, out far))
            {
                return ToLine(near, far);
            }

            // Within the tolerance by rounding alone: the box grown by a little more than it holds the stretch.
            if (TryClip(box, 2.0 * tolerance.EqualPoint, out near, out far))
            {
                return ToLine(near, far);
            }

            return null;
        }

        /// <summary>
        /// Finds the stretch of the line inside a box grown by a margin, as distances along the line.
        /// </summary>
        /// <returns>false when the line misses the grown box, or has no stretch for want of a number.</returns>
        private bool TryClip(GeoAabb3 box, double margin, out double near, out double far)
        {
            near = double.NegativeInfinity;
            far = double.PositiveInfinity;

            if (!Slab(Origin.X, Direction.X, box.Min.X - margin, box.Max.X + margin, ref near, ref far)
                || !Slab(Origin.Y, Direction.Y, box.Min.Y - margin, box.Max.Y + margin, ref near, ref far)
                || !Slab(Origin.Z, Direction.Z, box.Min.Z - margin, box.Max.Z + margin, ref near, ref far))
            {
                return false;
            }

            // Written as what has to hold, so that a NaN fails it.
            return Guard.IsFinite(near) && Guard.IsFinite(far) && near <= far;
        }

        /// <summary>
        /// Narrows the stretch of the line inside a box by one pair of its sides, the slab between them.
        /// </summary>
        /// <returns>false when the line is parallel to the slab and outside it, so that it misses the box.</returns>
        private static bool Slab(double start, double speed, double low, double high, ref double near, ref double far)
        {
            // Running along the slab, the line is in it or out of it for its whole length. Written as what has to hold, so
            // that a NaN fails it.
            if (!(Math.Abs(speed) > RoundingSine))
            {
                return start >= low && start <= high;
            }

            double enter = (low - start) / speed;
            double leave = (high - start) / speed;

            near = Math.Max(near, Math.Min(enter, leave));
            far = Math.Min(far, Math.Max(enter, leave));
            return true;
        }

        /// <summary>
        /// The sine below which a direction is parallel to the side of a box by rounding alone.
        /// </summary>
        private const double RoundingSine = 1E-15;

        #region Predicates

        /// <summary>
        /// Checks whether a point lies on this line using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on this line within a tolerance.
        /// </summary>
        /// <remarks>
        /// Of the line through the origin along X, the points (100, 0, 0) and (-1, 0, 0) are on it, and the point (5, 0, 0.002)
        /// is not at a tolerance of a thousandth.
        /// </remarks>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => DistanceTo(point, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Says where a point lies with respect to this line.
        /// </summary>
        /// <remarks>
        /// A line is a curve and encloses nothing. There is no inside, so the answer is only ever <c>OnSide</c> or <c>OutSide</c>;
        /// it is offered so that a caller holding any shape can ask the one question, and it reads the same way as
        /// <see cref="GeoRay3.Locate(GeoPoint3)"/> does.
        /// </remarks>
        public PointLocation Locate(GeoPoint3 point) => Locate(point, Tolerance.Global);

        /// <summary>
        /// Says where a point lies with respect to this line, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance that decides what counts as on it.</param>
        /// <returns><c>OnSide</c> when the point is on the line; otherwise, <c>OutSide</c>.</returns>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => IsPointOn(point, tolerance) ? PointLocation.OnSide : PointLocation.OutSide;

        /// <summary>
        /// Checks whether this line runs parallel to a plane using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoPlane3 plane) => IsParallelTo(plane, Tolerance.Global);

        /// <summary>
        /// Checks whether this line runs parallel to a plane within a tolerance.
        /// </summary>
        /// <remarks>
        /// Only the direction is read, so a line lying in a plane is parallel to it, and so is one a metre above it.
        /// </remarks>
        public bool IsParallelTo(GeoPlane3 plane, Tolerance tolerance) => Parallel3.IsPerpendicular(Direction, plane.Normal, tolerance);

        /// <summary>
        /// Checks whether this line is parallel to a line segment using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine3 line) => IsParallelTo(line, Tolerance.Global);

        /// <summary>
        /// Checks whether this line is parallel to a line segment within a tolerance.
        /// </summary>
        /// <remarks>
        /// Only the directions are compared, either way round: the line from the origin along X is parallel to the segment
        /// from (0, 10, 0) to (-100, 10, 0).
        /// </remarks>
        public bool IsParallelTo(GeoLine3 line, Tolerance tolerance) => Parallel3.IsParallel(Direction, line.Direction, tolerance);

        /// <summary>
        /// Checks whether this line is parallel to another using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoXLine3 other) => IsParallelTo(other, Tolerance.Global);

        /// <summary>
        /// Checks whether this line is parallel to another within a tolerance.
        /// </summary>
        /// <remarks>
        /// Only the directions are compared, either way round, so a line is parallel to itself reversed and to any line it
        /// is the same line as.
        /// </remarks>
        public bool IsParallelTo(GeoXLine3 other, Tolerance tolerance) => Parallel3.IsParallel(Direction, other.Direction, tolerance);

        /// <summary>
        /// Checks whether another line is the same infinite line as this one, using the default tolerance.
        /// </summary>
        public bool IsSameLineAs(GeoXLine3 other) => IsSameLineAs(other, Tolerance.Global);

        /// <summary>
        /// Checks whether another line is the same infinite line as this one, whatever its origin and its direction, within a tolerance.
        /// </summary>
        /// <param name="other">The other line.</param>
        /// <param name="tolerance">The tolerance: the directions must be the same or opposite within the vector tolerance, and each origin within the point tolerance of the other line.</param>
        /// <remarks>
        /// Stricter than <see cref="IsParallelTo(GeoXLine3, Tolerance)"/>, which reads a degree as parallel: a line has no length
        /// to measure a drift over, so the directions are held to the vector tolerance. The line from the origin along X is the
        /// same line as the one from (500, 0, 0) along -X and as the one from (-20, 0.0004, 0) along X at a tolerance of a
        /// thousandth; it is not the one from (0, 0, 0.002) along X, nor the one from the origin along (1, 0.01, 0).
        /// </remarks>
        public bool IsSameLineAs(GeoXLine3 other, Tolerance tolerance)
        {
            // The directions are of unit length, so the length of their cross product is the sine of the angle between them.
            // Written as what has to hold, so that a NaN fails it.
            return Direction.CrossProduct(other.Direction).Length <= tolerance.EqualVector
                && DistanceTo(other.Origin, tolerance) <= tolerance.EqualPoint
                && other.DistanceTo(Origin, tolerance) <= tolerance.EqualPoint;
        }

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another line has exactly the same origin and direction.
        /// </summary>
        /// <remarks>
        /// The fields are compared, not the line: the same line from another of its points, or running the other way, is
        /// not equal. <see cref="IsSameLineAs(GeoXLine3)"/> asks whether it is the same line.
        /// </remarks>
        public bool Equals(GeoXLine3 other) => Origin.Equals(other.Origin) && Direction.Equals(other.Direction);

        /// <summary>
        /// Determines whether the specified object is equal to the current line.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoXLine3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Origin.GetHashCode() * 397) ^ Direction.GetHashCode();
            }
        }

        /// <summary>
        /// Compares whether this line equals another line using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoXLine3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this line equals another line within a tolerance.
        /// </summary>
        /// <remarks>
        /// The origins are compared within the point tolerance and the directions within the vector tolerance.
        /// </remarks>
        public bool IsEqualTo(GeoXLine3 other, Tolerance tolerance)
        {
            return Origin.IsEqualTo(other.Origin, tolerance) && Direction.IsEqualTo(other.Direction, tolerance);
        }

        /// <summary>
        /// Checks if two lines have exactly the same origin and direction.
        /// </summary>
        public static bool operator ==(GeoXLine3 left, GeoXLine3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two lines differ in origin or direction.
        /// </summary>
        public static bool operator !=(GeoXLine3 left, GeoXLine3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Gets whether this is a shape a constructor could have made: the origin is a valid point and the direction of unit length.
        /// </summary>
        /// <remarks>
        /// A value made by a constructor always is. A <c>default</c> one — an element of a new array, or what
        /// the <c>out</c> of a <c>Try</c> method holds when the method said false — may not be, and answers
        /// questions without complaint all the same. A default one has no direction, so it is only a point.
        /// </remarks>
        public bool IsValid => Origin.IsValid && Guard.IsUnit(Direction);

        /// <summary>
        /// Returns a string that represents the current line.
        /// </summary>
        public override string ToString() => $"XLine3(Origin: {Origin}, Direction: {Direction})";
    }
}
