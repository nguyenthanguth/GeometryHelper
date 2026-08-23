using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Core;

namespace GeometryHelper.SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a 3D vector with double precision components.
    /// </summary>
    public readonly struct GeoVector3 : IEquatable<GeoVector3>
    {
        /// <summary>
        /// Gets the X component of the vector.
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Gets the Y component of the vector.
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// Gets the Z component of the vector.
        /// </summary>
        public double Z { get; }

        /// <summary>
        /// Gets a vector with all components set to zero.
        /// </summary>
        public static GeoVector3 Zero => new GeoVector3(0.0, 0.0, 0.0);

        /// <summary>
        /// Gets the X unit vector (1, 0, 0).
        /// </summary>
        public static GeoVector3 XAxis => new GeoVector3(1.0, 0.0, 0.0);

        /// <summary>
        /// Gets the Y unit vector (0, 1, 0).
        /// </summary>
        public static GeoVector3 YAxis => new GeoVector3(0.0, 1.0, 0.0);

        /// <summary>
        /// Gets the Z unit vector (0, 0, 1).
        /// </summary>
        public static GeoVector3 ZAxis => new GeoVector3(0.0, 0.0, 1.0);

        /// <summary>
        /// Initializes a new vector.
        /// </summary>
        /// <param name="x">X component.</param>
        /// <param name="y">Y component.</param>
        /// <param name="z">Z component.</param>
        public GeoVector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Initializes a new vector from another vector.
        /// </summary>
        /// <param name="vector">Source vector.</param>
        public GeoVector3(GeoVector3 vector)
            : this(vector.X, vector.Y, vector.Z)
        {
        }

        /// <summary>
        /// Creates a copy of this vector.
        /// </summary>
        /// <remarks>
        /// Vector is a readonly struct, so plain assignment already produces an independent copy and
        /// this method is not needed to avoid sharing. It exists so that every geometry type offers the
        /// same way to ask for a copy.
        /// </remarks>
        /// <returns>A new vector with the same components.</returns>
        public GeoVector3 Clone() => new GeoVector3(X, Y, Z);

        /// <summary>
        /// Gets the magnitude (length) of the vector.
        /// </summary>
        public double Length => Math.Sqrt(LengthSquared);

        /// <summary>
        /// Gets the squared magnitude of the vector.
        /// </summary>
        public double LengthSquared => X * X + Y * Y + Z * Z;

        #region Arithmetic

        /// <summary>
        /// Adds another vector to this vector.
        /// </summary>
        public GeoVector3 Add(GeoVector3 other) => new GeoVector3(X + other.X, Y + other.Y, Z + other.Z);

        /// <summary>
        /// Subtracts another vector from this vector.
        /// </summary>
        public GeoVector3 Subtract(GeoVector3 other) => new GeoVector3(X - other.X, Y - other.Y, Z - other.Z);

        /// <summary>
        /// Multiplies the vector by a scalar.
        /// </summary>
        public GeoVector3 Multiply(double scalar) => new GeoVector3(X * scalar, Y * scalar, Z * scalar);

        /// <summary>
        /// Divides the vector by a scalar.
        /// </summary>
        /// <param name="scalar">The divisor.</param>
        /// <returns>The scaled vector.</returns>
        /// <exception cref="DivideByZeroException">Thrown when the divisor is zero.</exception>
        /// <remarks>
        /// Dividing by zero is rejected rather than allowed to produce infinities, because a vector of
        /// infinities poisons every later calculation silently: it compares unequal to everything, its
        /// length is infinite, and normalizing it yields NaN. Failing at the division names the cause.
        /// </remarks>
        public GeoVector3 Divide(double scalar)
        {
            if (scalar == 0.0)
            {
                throw new DivideByZeroException("Cannot divide a vector by zero.");
            }

            return new GeoVector3(X / scalar, Y / scalar, Z / scalar);
        }

        /// <summary>
        /// Gets the vector pointing in the opposite direction.
        /// </summary>
        public GeoVector3 Negate() => new GeoVector3(-X, -Y, -Z);

        /// <summary>
        /// Applies a transformation to this vector.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoVector3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Calculates the dot product of this vector and another vector.
        /// </summary>
        public double DotProduct(GeoVector3 other) => X * other.X + Y * other.Y + Z * other.Z;

        /// <summary>
        /// Calculates the cross product of this vector and another vector.
        /// </summary>
        /// <remarks>
        /// The result is perpendicular to both operands, follows the right-hand rule, and has a length
        /// equal to the area of the parallelogram they span. It is the zero vector when the operands are
        /// parallel.
        /// </remarks>
        public GeoVector3 CrossProduct(GeoVector3 other)
        {
            return new GeoVector3(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X);
        }

        /// <summary>
        /// Calculates the scalar triple product of this vector with two others, that is
        /// <c>this · (v1 × v2)</c>.
        /// </summary>
        /// <remarks>
        /// The result is the signed volume of the parallelepiped spanned by the three vectors, so it is
        /// zero exactly when they are coplanar. This is the test coplanarity checks are built on.
        /// </remarks>
        public double TripleProduct(GeoVector3 v1, GeoVector3 v2) => DotProduct(v1.CrossProduct(v2));

        #endregion

        #region Normalization

        /// <summary>
        /// Returns a normalized (unit length) copy of this vector.
        /// </summary>
        /// <returns>The unit vector pointing in the same direction.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the vector has zero length.</exception>
        /// <remarks>
        /// A zero-length vector has no direction, so there is no answer to return. Use
        /// <see cref="TryGetNormal(out GeoVector3)"/> where a degenerate input is expected and should be
        /// handled rather than thrown on.
        /// </remarks>
        public GeoVector3 Normalize()
        {
            if (!TryGetNormal(out GeoVector3 normal))
            {
                throw new InvalidOperationException("Cannot normalize a zero-length vector.");
            }

            return normal;
        }

        /// <summary>
        /// Tries to normalize the vector without throwing, using the default tolerance.
        /// </summary>
        /// <param name="normal">The unit vector, or <see cref="Zero"/> when the vector is degenerate.</param>
        /// <returns>true if the vector could be normalized; otherwise, false.</returns>
        public bool TryGetNormal(out GeoVector3 normal) => TryGetNormal(out normal, Tolerance.Global);

        /// <summary>
        /// Tries to normalize the vector without throwing, within a tolerance.
        /// </summary>
        /// <param name="normal">The unit vector, or <see cref="Zero"/> when the vector is degenerate.</param>
        /// <param name="tolerance">The tolerance deciding what counts as zero length.</param>
        /// <returns>true if the vector could be normalized; otherwise, false.</returns>
        public bool TryGetNormal(out GeoVector3 normal, Tolerance tolerance)
        {
            double length = Length;
            if (length <= tolerance.EqualVector)
            {
                normal = Zero;
                return false;
            }

            normal = new GeoVector3(X / length, Y / length, Z / length);
            return true;
        }

        /// <summary>
        /// Gets an arbitrary unit vector perpendicular to this one.
        /// </summary>
        /// <returns>A unit vector perpendicular to this vector.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the vector has zero length.</exception>
        /// <remarks>
        /// In 3D a vector has a whole plane of perpendiculars and no way to prefer one, so which of them
        /// comes back is unspecified beyond being perpendicular and of unit length. It is stable for a
        /// given input. The axis crossed against is the one this vector leans on least, which keeps the
        /// cross product well away from zero and the result well conditioned.
        /// </remarks>
        public GeoVector3 GetPerpendicularVector()
        {
            if (IsZeroLength())
            {
                throw new InvalidOperationException("A zero-length vector has no perpendicular direction.");
            }

            double ax = Math.Abs(X);
            double ay = Math.Abs(Y);
            double az = Math.Abs(Z);

            GeoVector3 leastAligned = ax <= ay && ax <= az
                ? XAxis
                : ay <= az ? YAxis : ZAxis;

            return CrossProduct(leastAligned).Normalize();
        }

        /// <summary>
        /// Rotates this vector around an axis through the origin.
        /// </summary>
        /// <param name="angleRad">The rotation angle in radians, counter-clockwise seen from the axis tip.</param>
        /// <param name="axis">The rotation axis; it need not be normalized.</param>
        /// <returns>The rotated vector.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the axis has zero length.</exception>
        public GeoVector3 RotateBy(double angleRad, GeoVector3 axis)
        {
            if (!axis.TryGetNormal(out GeoVector3 unitAxis))
            {
                throw new InvalidOperationException("Cannot rotate around a zero-length axis.");
            }

            // Rodrigues' rotation formula: v*cos + (k x v)*sin + k*(k.v)*(1 - cos).
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            return Multiply(cos)
                .Add(unitAxis.CrossProduct(this).Multiply(sin))
                .Add(unitAxis.Multiply(unitAxis.DotProduct(this) * (1.0 - cos)));
        }

        #endregion

        #region Angles

        /// <summary>
        /// Gets the unsigned angle to another vector in radians, in the range [0, PI].
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when either vector has zero length.</exception>
        public double GetAngleTo(GeoVector3 other)
        {
            if (IsZeroLength() || other.IsZeroLength())
            {
                throw new InvalidOperationException("Cannot calculate the angle to or from a zero-length vector.");
            }

            // Use Atan2(|cross|, dot) instead of Acos(dot). Acos loses accuracy severely when two vectors
            // are nearly collinear or nearly opposite: its derivative approaches infinity near ±1, so
            // rounding errors around 1e-16 of the dot product are amplified to angle errors around 1e-8.
            // Atan2 is stable across the entire range and does not require normalization: both arguments
            // are proportional to |a|*|b|.
            return Math.Atan2(CrossProduct(other).Length, DotProduct(other));
        }

        /// <summary>
        /// Gets the signed angle to another vector in radians, in the range (-PI, PI], measured
        /// counter-clockwise around a reference axis.
        /// </summary>
        /// <param name="other">The vector to measure to.</param>
        /// <param name="referenceNormal">
        /// The axis the rotation is measured around, which fixes what counts as the positive direction.
        /// </param>
        /// <exception cref="InvalidOperationException">Thrown when any of the three vectors has zero length.</exception>
        /// <remarks>
        /// An angle between two vectors in 3D has no sign of its own: the rotation that takes one to the
        /// other looks counter-clockwise from one side and clockwise from the other. The reference axis
        /// is what picks the side, which is why this overload needs one and the 2D case does not.
        /// </remarks>
        public double GetSignedAngleTo(GeoVector3 other, GeoVector3 referenceNormal)
        {
            if (referenceNormal.IsZeroLength())
            {
                throw new InvalidOperationException("Cannot measure a signed angle around a zero-length axis.");
            }

            GeoVector3 cross = CrossProduct(other);
            double magnitude = GetAngleTo(other);

            return cross.DotProduct(referenceNormal) < 0.0 ? -magnitude : magnitude;
        }

        #endregion

        #region Predicates

        /// <summary>
        /// Checks whether the vector has zero length using the default tolerance.
        /// </summary>
        public bool IsZeroLength() => IsZeroLength(Tolerance.Global);

        /// <summary>
        /// Checks whether the vector has zero length within a tolerance.
        /// </summary>
        public bool IsZeroLength(Tolerance tolerance) => LengthSquared <= tolerance.EqualVector * tolerance.EqualVector;

        /// <summary>
        /// Checks whether the vector has unit length using the default tolerance.
        /// </summary>
        public bool IsUnitLength() => IsUnitLength(Tolerance.Global);

        /// <summary>
        /// Checks whether the vector has unit length within a tolerance.
        /// </summary>
        public bool IsUnitLength(Tolerance tolerance) => Math.Abs(Length - 1.0) <= tolerance.EqualVector;

        /// <summary>
        /// Compares whether this vector equals another vector using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoVector3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this vector equals another vector within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoVector3 other, Tolerance tolerance)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz <= tolerance.EqualVector * tolerance.EqualVector;
        }

        /// <summary>
        /// Checks whether this vector is parallel or anti-parallel to another vector using the default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoVector3 other) => Parallel3.IsParallel(this, other);

        /// <summary>
        /// Checks whether this vector is parallel or anti-parallel to another vector within tolerance.
        /// </summary>
        public bool IsParallelTo(GeoVector3 other, Tolerance tolerance) => Parallel3.IsParallel(this, other, tolerance);

        /// <summary>
        /// Checks whether this vector points the same way as another vector using the default tolerance.
        /// </summary>
        public bool IsCodirectionalTo(GeoVector3 other) => Parallel3.IsCodirectional(this, other);

        /// <summary>
        /// Checks whether this vector points the same way as another vector within tolerance.
        /// </summary>
        public bool IsCodirectionalTo(GeoVector3 other, Tolerance tolerance) => Parallel3.IsCodirectional(this, other, tolerance);

        /// <summary>
        /// Checks whether this vector is perpendicular to another vector using the default tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoVector3 other) => Parallel3.IsPerpendicular(this, other);

        /// <summary>
        /// Checks whether this vector is perpendicular to another vector within tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoVector3 other, Tolerance tolerance) => Parallel3.IsPerpendicular(this, other, tolerance);

        /// <summary>
        /// Checks whether this vector lies in the plane spanned by two other vectors, using the default tolerance.
        /// </summary>
        public bool IsCoplanarWith(GeoVector3 v1, GeoVector3 v2) => Parallel3.IsCoplanar(this, v1, v2);

        /// <summary>
        /// Checks whether this vector lies in the plane spanned by two other vectors, within tolerance.
        /// </summary>
        public bool IsCoplanarWith(GeoVector3 v1, GeoVector3 v2, Tolerance tolerance) => Parallel3.IsCoplanar(this, v1, v2, tolerance);

        #endregion

        #region Projection

        /// <summary>
        /// Projects this vector onto an axis vector.
        /// </summary>
        public GeoVector3 ProjectOnto(GeoVector3 axis) => Projection3.Project(this, axis);

        /// <summary>
        /// Projects this vector onto a plane through the origin with the given normal.
        /// </summary>
        public GeoVector3 ProjectOntoPlane(GeoVector3 planeNormal) => Projection3.ProjectOntoPlane(this, planeNormal);

        #endregion

        #region Equality and operators

        /// <summary>
        /// Determines whether the specified vector has exactly the same components as this vector.
        /// </summary>
        public bool Equals(GeoVector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        /// <summary>
        /// Determines whether the specified object is equal to the current vector.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoVector3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Z.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Adds two vectors.
        /// </summary>
        public static GeoVector3 operator +(GeoVector3 left, GeoVector3 right) => left.Add(right);

        /// <summary>
        /// Subtracts one vector from another.
        /// </summary>
        public static GeoVector3 operator -(GeoVector3 left, GeoVector3 right) => left.Subtract(right);

        /// <summary>
        /// Negates a vector.
        /// </summary>
        public static GeoVector3 operator -(GeoVector3 vector) => vector.Negate();

        /// <summary>
        /// Multiplies a vector by a scalar.
        /// </summary>
        public static GeoVector3 operator *(GeoVector3 vector, double scalar) => vector.Multiply(scalar);

        /// <summary>
        /// Multiplies a vector by a scalar.
        /// </summary>
        public static GeoVector3 operator *(double scalar, GeoVector3 vector) => vector.Multiply(scalar);

        /// <summary>
        /// Divides a vector by a scalar.
        /// </summary>
        public static GeoVector3 operator /(GeoVector3 vector, double scalar) => vector.Divide(scalar);

        /// <summary>
        /// Checks if two vectors have exactly the same components.
        /// </summary>
        public static bool operator ==(GeoVector3 left, GeoVector3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two vectors differ in any component.
        /// </summary>
        public static bool operator !=(GeoVector3 left, GeoVector3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Returns a string that represents the current vector.
        /// </summary>
        public override string ToString() => $"[{X:0.###}, {Y:0.###}, {Z:0.###}]";
    }
}
