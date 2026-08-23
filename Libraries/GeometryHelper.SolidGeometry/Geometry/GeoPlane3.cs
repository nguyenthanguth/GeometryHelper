using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;

namespace GeometryHelper.SolidGeometry.Geometry
{
    /// <summary>
    /// Represents an infinite 3D plane defined by a point on the plane and a normal vector.
    /// <para>
    /// The plane is oriented: it distinguishes the side its normal points towards from the other side.
    /// That is what lets <see cref="SignedDistanceTo"/> and <see cref="GetSide(GeoPoint3)"/> answer at all,
    /// and it is why <see cref="Flip"/> is a meaningful operation rather than a no-op.
    /// </para>
    /// </summary>
    public readonly struct GeoPlane3 : IEquatable<GeoPlane3>
    {
        /// <summary>
        /// Gets a point lying on the plane.
        /// </summary>
        public GeoPoint3 Origin { get; }

        /// <summary>
        /// Gets the normal vector of the plane, always normalized.
        /// </summary>
        public GeoVector3 Normal { get; }

        /// <summary>
        /// Gets the world XY plane, with its normal along +Z.
        /// </summary>
        public static GeoPlane3 XY => new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);

        /// <summary>
        /// Gets the world XZ plane, with its normal along +Y.
        /// </summary>
        public static GeoPlane3 XZ => new GeoPlane3(GeoPoint3.Origin, GeoVector3.YAxis);

        /// <summary>
        /// Gets the world YZ plane, with its normal along +X.
        /// </summary>
        public static GeoPlane3 YZ => new GeoPlane3(GeoPoint3.Origin, GeoVector3.XAxis);

        /// <summary>
        /// Initializes a new plane.
        /// </summary>
        /// <param name="origin">A point on the plane.</param>
        /// <param name="normal">The normal vector; it is normalized on construction.</param>
        /// <exception cref="ArgumentException">Thrown when the normal has zero length.</exception>
        public GeoPlane3(GeoPoint3 origin, GeoVector3 normal)
        {
            if (!normal.TryGetNormal(out GeoVector3 unit))
            {
                throw new ArgumentException("A plane needs a normal of non-zero length.", nameof(normal));
            }

            Origin = origin;
            Normal = unit;
        }

        /// <summary>
        /// Creates the plane through three points, oriented by the right-hand rule so that the points run
        /// counter-clockwise when seen from the side the normal points towards.
        /// </summary>
        /// <param name="p1">First point.</param>
        /// <param name="p2">Second point.</param>
        /// <param name="p3">Third point.</param>
        /// <returns>The plane through the three points.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the three points are collinear or two of them coincide, since such a set does not
        /// pin down a plane.
        /// </exception>
        public static GeoPlane3 FromThreePoints(GeoPoint3 p1, GeoPoint3 p2, GeoPoint3 p3)
        {
            GeoVector3 normal = p1.GetVectorTo(p2).CrossProduct(p1.GetVectorTo(p3));

            if (normal.IsZeroLength())
            {
                throw new ArgumentException("Three collinear or coincident points do not define a plane.");
            }

            return new GeoPlane3(p1, normal);
        }

        /// <summary>
        /// Initializes a plane from a normal that is already normalized.
        /// </summary>
        /// <remarks>
        /// Normalizing a vector that is already of unit length is not the identity in floating point: the
        /// computed length comes back a bit either side of one, and dividing by it shifts the last digits.
        /// Operations that only move a plane around must not pay that, or a copy would come back unequal
        /// to its original and flipping twice would drift. This constructor is how they skip it.
        /// </remarks>
        private GeoPlane3(GeoPoint3 origin, GeoVector3 unitNormal, bool alreadyNormalized)
        {
            Origin = origin;
            Normal = unitNormal;
        }

        /// <summary>
        /// Creates a copy of this plane.
        /// </summary>
        /// <remarks>
        /// Plane is a readonly struct, so plain assignment already produces an independent copy and this
        /// method is not needed to avoid sharing. It exists so that every geometry type offers the same
        /// way to ask for a copy.
        /// </remarks>
        public GeoPlane3 Clone() => new GeoPlane3(Origin, Normal, true);

        /// <summary>
        /// Gets the same plane with its normal reversed, so the two sides swap roles.
        /// </summary>
        public GeoPlane3 Flip() => new GeoPlane3(Origin, Normal.Negate(), true);

        /// <summary>
        /// Gets the plane offset by a distance along its own normal.
        /// </summary>
        /// <param name="distance">How far to move the plane; negative moves it against the normal.</param>
        public GeoPlane3 Offset(double distance) => new GeoPlane3(Origin.Add(Normal.Multiply(distance)), Normal, true);

        /// <summary>
        /// Applies a transformation to this plane.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation is not invertible.</exception>
        /// <remarks>
        /// The normal follows the inverse transpose rather than the matrix itself, so it stays
        /// perpendicular to the surface even under a non-uniform scaling. See
        /// <see cref="GeoTransform3.Transform(GeoPlane3)"/> for why.
        /// </remarks>
        public GeoPlane3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Gets the signed distance from the world origin to the plane along its normal, that is the
        /// <c>d</c> of the plane equation <c>n · x = d</c>.
        /// </summary>
        public double DistanceFromWorldOrigin => Normal.DotProduct(Origin.ToVector());

        /// <summary>
        /// Gets a pair of orthonormal directions spanning the plane.
        /// </summary>
        /// <param name="uAxis">The first in-plane direction.</param>
        /// <param name="vAxis">The second in-plane direction, so that u × v equals the plane normal.</param>
        /// <remarks>
        /// A plane has no preferred pair of in-plane axes, so which pair comes back is unspecified beyond
        /// being orthonormal and right-handed with the normal. It is stable for a given plane.
        /// </remarks>
        public void GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis)
        {
            uAxis = Normal.GetPerpendicularVector();
            vAxis = Normal.CrossProduct(uAxis).Normalize();
        }

        #region Queries

        /// <summary>
        /// Projects a point onto the plane.
        /// </summary>
        public GeoPoint3 Project(GeoPoint3 point) => Projection3.ProjectToPlane(this, point);

        /// <summary>
        /// Projects a vector onto the plane, dropping the component along the normal.
        /// </summary>
        public GeoVector3 Project(GeoVector3 vector) => Projection3.ProjectOntoPlane(vector, Normal);

        /// <summary>
        /// Calculates the unsigned distance from a point to the plane.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from a line segment to the plane. A segment that crosses the
        /// plane is at distance zero.
        /// </summary>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(this, line);

        /// <summary>
        /// Calculates the signed distance from a point to the plane.
        /// </summary>
        /// <param name="point">The target point.</param>
        /// <returns>Positive if the point is on the side the normal points towards, negative otherwise.</returns>
        public double SignedDistanceTo(GeoPoint3 point) => Origin.GetVectorTo(point).DotProduct(Normal);

        /// <summary>
        /// Determines which side of the plane a point lies on, using the default tolerance.
        /// </summary>
        public PlaneSide GetSide(GeoPoint3 point) => Containment3.GetSide(this, point);

        /// <summary>
        /// Determines which side of the plane a point lies on, within a tolerance.
        /// </summary>
        public PlaneSide GetSide(GeoPoint3 point, Tolerance tolerance) => Containment3.GetSide(this, point, tolerance);

        /// <summary>
        /// Checks whether a point lies on the plane using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => Containment3.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on the plane within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => Containment3.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Checks whether every one of a set of points lies on the plane, using the default tolerance.
        /// </summary>
        public bool ContainsAll(IEnumerable<GeoPoint3> points) => ContainsAll(points, Tolerance.Global);

        /// <summary>
        /// Checks whether every one of a set of points lies on the plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The threshold used here is <see cref="Tolerance.EqualPlanar"/> rather than
        /// <see cref="Tolerance.EqualPoint"/>, because this is the coplanarity question rather than the
        /// coincidence question.
        /// </remarks>
        public bool ContainsAll(IEnumerable<GeoPoint3> points, Tolerance tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            foreach (GeoPoint3 point in points)
            {
                if (Math.Abs(SignedDistanceTo(point)) > tolerance.EqualPlanar)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether this plane is parallel to another plane using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoPlane3 other) => Parallel3.IsParallel(this, other);

        /// <summary>
        /// Checks whether this plane is parallel to another plane within a tolerance.
        /// </summary>
        public bool IsParallelTo(GeoPlane3 other, Tolerance tolerance) => Parallel3.IsParallel(this, other, tolerance);

        /// <summary>
        /// Checks whether this plane is perpendicular to another plane using the default tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoPlane3 other) => Parallel3.IsPerpendicular(this, other);

        /// <summary>
        /// Checks whether this plane is perpendicular to another plane within a tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoPlane3 other, Tolerance tolerance) => Parallel3.IsPerpendicular(this, other, tolerance);

        /// <summary>
        /// Tries to find the line where this plane meets another plane, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 other, out GeoRay3 intersection) => Intersection3.TryIntersectWith(this, other, out intersection);

        /// <summary>
        /// Tries to find the line where this plane meets another plane, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPlane3 other, out GeoRay3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(this, other, out intersection, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another plane has exactly the same origin and normal.
        /// </summary>
        /// <remarks>
        /// This compares the representation, not the set of points. Two planes describing the same flat
        /// surface with different origins on it are not equal here; <see cref="IsEqualTo(GeoPlane3)"/>
        /// answers the geometric question instead.
        /// </remarks>
        public bool Equals(GeoPlane3 other) => Origin.Equals(other.Origin) && Normal.Equals(other.Normal);

        /// <summary>
        /// Determines whether the specified object is equal to the current plane.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoPlane3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Origin.GetHashCode() * 397) ^ Normal.GetHashCode();
            }
        }

        /// <summary>
        /// Compares whether this plane describes the same oriented surface as another, using the default
        /// tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPlane3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this plane describes the same oriented surface as another, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Two planes count as equal when their normals point the same way and each origin lies on the
        /// other plane, so the origin may sit anywhere on the surface. Orientation still matters: a plane
        /// and its <see cref="Flip"/> are not equal.
        /// </remarks>
        public bool IsEqualTo(GeoPlane3 other, Tolerance tolerance)
        {
            return Normal.IsEqualTo(other.Normal, tolerance) &&
                   Math.Abs(SignedDistanceTo(other.Origin)) <= tolerance.EqualPlanar;
        }

        /// <summary>
        /// Checks if two planes have exactly the same origin and normal.
        /// </summary>
        public static bool operator ==(GeoPlane3 left, GeoPlane3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two planes differ in origin or normal.
        /// </summary>
        public static bool operator !=(GeoPlane3 left, GeoPlane3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current plane.
        /// </summary>
        public override string ToString() => $"Plane3(Origin: {Origin}, Normal: {Normal})";
    }
}
