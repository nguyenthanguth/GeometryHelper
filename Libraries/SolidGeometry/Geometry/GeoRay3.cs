using System;
using CommonGeometry;
using SolidGeometry.Core;

namespace SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a 3D ray: a half-line starting at an origin and running to infinity in one direction.
    /// </summary>
    public readonly struct GeoRay3 : IEquatable<GeoRay3>
    {
        /// <summary>
        /// Gets the origin point of the ray.
        /// </summary>
        public GeoPoint3 Origin { get; }

        /// <summary>
        /// Gets the direction of the ray, always normalized.
        /// </summary>
        public GeoVector3 Direction { get; }

        /// <summary>
        /// Initializes a new ray.
        /// </summary>
        /// <param name="origin">The origin point.</param>
        /// <param name="direction">The direction vector; it is normalized on construction.</param>
        /// <exception cref="ArgumentException">Thrown when the direction has zero length.</exception>
        public GeoRay3(GeoPoint3 origin, GeoVector3 direction)
        {
            if (!direction.TryGetNormal(out GeoVector3 unit))
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
        public GeoRay3(GeoPoint3 origin, GeoPoint3 through)
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
        private GeoRay3(GeoPoint3 origin, GeoVector3 unitDirection, bool alreadyNormalized)
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
        public GeoRay3 Clone() => new GeoRay3(Origin, Direction, true);

        /// <summary>
        /// Gets the ray running the opposite way from the same origin.
        /// </summary>
        public GeoRay3 Reverse() => new GeoRay3(Origin, Direction.Negate(), true);

        /// <summary>
        /// Applies a transformation to this ray.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoRay3 TransformBy(GeoTransform3 transform)
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
        /// <see cref="GeoLine3.GetPointAtParameter"/>, which likewise follows the infinite carrier rather
        /// than clamping. Ask <see cref="IsPointOn(GeoPoint3)"/> when the question is whether a position
        /// is actually on the ray.
        /// </remarks>
        public GeoPoint3 GetPointAtDistance(double distance) => Origin.Add(Direction.Multiply(distance));

        /// <summary>
        /// Gets the distance from the origin to the point on the ray's carrier line closest to the
        /// supplied point. The result is negative when that point falls behind the origin.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => Origin.GetVectorTo(point).DotProduct(Direction);

        /// <summary>
        /// Gets the line segment covering the ray from its origin out to a given distance.
        /// </summary>
        /// <param name="distance">How far along the ray the segment should reach.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is negative.</exception>
        public GeoLine3 ToLine(double distance)
        {
            if (distance < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "A ray cannot be sampled backwards into a segment.");
            }

            return new GeoLine3(Origin, GetPointAtDistance(distance));
        }

        #region Distance and projection

        /// <summary>
        /// Calculates the shortest distance from this ray to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance between this ray and a line segment.
        /// </summary>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(this, line);

        /// <summary>
        /// Gets the closest point on this ray to a target point, clamped to the origin.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToRay(this, point);

        #endregion

        #region Predicates and intersection

        /// <summary>
        /// Checks whether a point lies on this ray using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => Containment3.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on this ray within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => Containment3.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Checks whether this ray runs parallel to a plane using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoPlane3 plane) => Parallel3.IsParallel(this, plane);

        /// <summary>
        /// Checks whether this ray runs parallel to a plane within a tolerance.
        /// </summary>
        public bool IsParallelTo(GeoPlane3 plane, Tolerance tolerance) => Parallel3.IsParallel(this, plane, tolerance);

        /// <summary>
        /// Checks whether this ray is parallel to a line segment using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine3 line) => Parallel3.IsParallel(Direction, line.Direction);

        /// <summary>
        /// Checks whether this ray is parallel to a line segment within a tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine3 line, Tolerance tolerance) => Parallel3.IsParallel(Direction, line.Direction, tolerance);

        /// <summary>
        /// Tries to find the point where this ray crosses a plane, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, plane, out intersection);

        /// <summary>
        /// Tries to find the point where this ray crosses a plane, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, plane, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where this ray crosses a triangle, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(this, triangle, out intersection);

        /// <summary>
        /// Tries to find the point where this ray crosses a triangle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, triangle, out intersection, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another ray has exactly the same origin and direction.
        /// </summary>
        public bool Equals(GeoRay3 other) => Origin.Equals(other.Origin) && Direction.Equals(other.Direction);

        /// <summary>
        /// Determines whether the specified object is equal to the current ray.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoRay3 other && Equals(other);

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
        public bool IsEqualTo(GeoRay3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this ray equals another ray within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoRay3 other, Tolerance tolerance)
        {
            return Origin.IsEqualTo(other.Origin, tolerance) && Direction.IsEqualTo(other.Direction, tolerance);
        }

        /// <summary>
        /// Checks if two rays have exactly the same origin and direction.
        /// </summary>
        public static bool operator ==(GeoRay3 left, GeoRay3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two rays differ in origin or direction.
        /// </summary>
        public static bool operator !=(GeoRay3 left, GeoRay3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current ray.
        /// </summary>
        public override string ToString() => $"Ray3(Origin: {Origin}, Direction: {Direction})";
    }
}
