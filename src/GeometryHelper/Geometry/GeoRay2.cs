using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a ray in the plane: a half-line starting at an origin and running to infinity in one direction, the
    /// counterpart of <see cref="GeoRay3"/> in space.
    /// </summary>
    /// <remarks>
    /// The ray from (-50, 50) along X runs into the square 100 by 100 with a corner at the origin at (0, 50) and leaves it
    /// at (100, 50); turned round, it runs away from the square and meets nothing.
    /// </remarks>
    public readonly partial struct GeoRay2 : IEquatable<GeoRay2>
    {
        /// <summary>
        /// Gets the origin point of the ray.
        /// </summary>
        public GeoPoint2 Origin { get; }

        /// <summary>
        /// Gets the direction of the ray, always normalized.
        /// </summary>
        public GeoVector2 Direction { get; }

        /// <summary>
        /// Initializes a new ray.
        /// </summary>
        /// <param name="origin">The origin point.</param>
        /// <param name="direction">The direction vector; it is normalized on construction.</param>
        /// <exception cref="ArgumentException">Thrown when the direction has zero length.</exception>
        public GeoRay2(GeoPoint2 origin, GeoVector2 direction)
        {
            if (!direction.TryGetNormal(out GeoVector2 unit))
            {
                throw new ArgumentException("A ray needs a direction of non-zero length.", nameof(direction));
            }

            Origin = origin;
            Direction = unit;
        }

        /// <summary>
        /// Initializes a new ray running from one point through another.
        /// </summary>
        /// <param name="origin">The origin point.</param>
        /// <param name="through">A point the ray passes through.</param>
        /// <exception cref="ArgumentException">Thrown when the two points coincide.</exception>
        public GeoRay2(GeoPoint2 origin, GeoPoint2 through)
            : this(origin, origin.GetVectorTo(through))
        {
        }

        /// <summary>
        /// Initializes a ray from a direction that is already normalized.
        /// </summary>
        /// <remarks>
        /// Normalizing a vector that is already of unit length is not the identity in floating point: the
        /// computed length comes back a bit either side of one, and dividing by it shifts the last digits.
        /// Operations that only move a ray around must not pay that, or a copy would come back unequal to
        /// its original and reversing twice would drift. This constructor is how they skip it.
        /// </remarks>
        private GeoRay2(GeoPoint2 origin, GeoVector2 unitDirection, bool alreadyNormalized)
        {
            Origin = origin;
            Direction = unitDirection;
        }

        /// <summary>
        /// Creates a copy of this ray.
        /// </summary>
        /// <remarks>
        /// Ray is a readonly struct, so plain assignment already produces an independent copy and this
        /// method is not needed to avoid sharing. It exists so that every geometry type offers the same
        /// way to ask for a copy.
        /// </remarks>
        public GeoRay2 Clone() => new GeoRay2(Origin, Direction, true);

        /// <summary>
        /// Gets the ray running the opposite way from the same origin.
        /// </summary>
        public GeoRay2 Reverse() => new GeoRay2(Origin, -Direction, true);

        /// <summary>
        /// Moves the ray by a vector.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The ray in its new place.</returns>
        public GeoRay2 Translate(GeoVector2 vector) => new GeoRay2(Origin.Add(vector), Direction, true);

        /// <summary>
        /// Applies a transformation to this ray.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed ray, its direction normalized again.</returns>
        /// <remarks>
        /// The origin is moved as a point and the direction as a vector, so an uneven scaling turns the ray as well as
        /// moving it: the ray from the origin at 45 degrees, scaled by 2 along X only, runs towards (2, 1).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation flattens the direction to nothing.</exception>
        public GeoRay2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Gets the point at a distance measured from the origin along the ray direction.
        /// </summary>
        /// <param name="distance">The distance from the origin.</param>
        /// <returns>The point at that distance.</returns>
        /// <remarks>
        /// Because the direction is normalized, the parameter and the arc length are the same number here.
        /// A negative distance extrapolates behind the origin, off the ray itself; this matches
        /// <see cref="GeoLine2.GetPointAtParameter"/>, which likewise follows the infinite carrier rather
        /// than clamping. Ask <see cref="IsPointOn(GeoPoint2)"/> when the question is whether a position
        /// is actually on the ray.
        /// </remarks>
        public GeoPoint2 GetPointAtDistance(double distance)
        {
            return Origin.Add(Direction.Multiply(distance));
        }

        /// <summary>
        /// Gets the distance from the origin to the point on the ray's carrier line closest to the
        /// supplied point. The result is negative when that point falls behind the origin.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point)
        {
            return Origin.GetVectorTo(point).DotProduct(Direction);
        }

        /// <summary>
        /// Gets the line segment covering the ray from its origin out to a given distance.
        /// </summary>
        /// <param name="distance">How far along the ray the segment should reach.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is negative, NaN or infinite.</exception>
        public GeoLine2 ToLine(double distance)
        {
            Guard.NonNegative(distance, nameof(distance), "A ray is sampled forwards, over a distance that is a number.");

            return new GeoLine2(Origin, GetPointAtDistance(distance));
        }

        /// <summary>
        /// Puts this ray of the plane back into space, on the plane of a frame.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <returns>The ray in space: its origin on the plane of the frame and its direction along it.</returns>
        /// <exception cref="ArgumentException">Thrown when the ray has no direction, as a <c>default</c> one has.</exception>
        public GeoRay3 ToRay3(GeoCoordinateSystem3 frame) => PlanarMap.ToRay3(frame, this);

        #region Predicates

        /// <summary>
        /// Checks whether a point lies on this ray using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => Containment2.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on this ray within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => Containment2.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Checks whether this ray is parallel to a line segment using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine2 line) => IsParallelTo(line, Tolerance.Global);

        /// <summary>
        /// Checks whether this ray is parallel to a line segment within a tolerance.
        /// </summary>
        /// <remarks>
        /// Only the directions are compared, either way round: the ray from the origin along X is parallel to the segment
        /// from (0, 10) to (-100, 10).
        /// </remarks>
        public bool IsParallelTo(GeoLine2 line, Tolerance tolerance)
        {
            return Parallel2.IsParallel(Direction, line.Direction, tolerance);
        }

        /// <summary>
        /// Says where a point lies with respect to this ray.
        /// </summary>
        /// <remarks>
        /// A ray is a curve and encloses nothing. There is no inside, so the answer is only ever <c>OnSide</c> or <c>OutSide</c>; it is
        /// offered so that a caller holding any shape can ask the one question, and it reads the same way as
        /// <see cref="GeoLine2.Locate(GeoPoint2)"/> does.
        /// </remarks>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point);

        /// <summary>
        /// Says where a point lies with respect to this ray, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance that decides what counts as on it.</param>
        /// <returns><c>OnSide</c> when the point is on the ray; otherwise, <c>OutSide</c>.</returns>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another ray has exactly the same origin and direction.
        /// </summary>
        public bool Equals(GeoRay2 other) => Origin.Equals(other.Origin) && Direction.Equals(other.Direction);

        /// <summary>
        /// Determines whether the specified object is equal to the current ray.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoRay2 other && Equals(other);

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
        /// Compares whether this ray equals another ray using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoRay2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this ray equals another ray within a tolerance.
        /// </summary>
        /// <remarks>
        /// The origins are compared within the point tolerance and the directions within the vector tolerance.
        /// </remarks>
        public bool IsEqualTo(GeoRay2 other, Tolerance tolerance)
        {
            return Origin.IsEqualTo(other.Origin, tolerance) && Direction.IsEqualTo(other.Direction, tolerance);
        }

        /// <summary>
        /// Checks if two rays have exactly the same origin and direction.
        /// </summary>
        public static bool operator ==(GeoRay2 left, GeoRay2 right) => left.Equals(right);

        /// <summary>
        /// Checks if two rays differ in origin or direction.
        /// </summary>
        public static bool operator !=(GeoRay2 left, GeoRay2 right) => !left.Equals(right);

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
        /// Returns a string that represents the current ray.
        /// </summary>
        public override string ToString() => $"Ray2(Origin: {Origin}, Direction: {Direction})";
    }
}
