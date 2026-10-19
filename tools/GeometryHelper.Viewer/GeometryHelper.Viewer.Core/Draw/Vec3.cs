using System;
using System.Globalization;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A point or a direction in space in double precision, as the viewer's drawing and camera maths hold them. It is the
    /// viewer's own type so that the drawing primitives carry no library type: the library objects are rebuilt from a scene
    /// only where a number is wanted from them.
    /// </summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        /// <summary>
        /// Initializes a vector.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Gets the X component.
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Gets the Y component.
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// Gets the Z component.
        /// </summary>
        public double Z { get; }

        /// <summary>
        /// Gets the length of the vector.
        /// </summary>
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>
        /// Gets whether all three components are finite numbers.
        /// </summary>
        public bool IsFinite => !double.IsNaN(X) && !double.IsInfinity(X) && !double.IsNaN(Y) && !double.IsInfinity(Y) && !double.IsNaN(Z) && !double.IsInfinity(Z);

        /// <summary>
        /// Adds two vectors.
        /// </summary>
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        /// <summary>
        /// Subtracts one vector from another.
        /// </summary>
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        /// <summary>
        /// Negates a vector.
        /// </summary>
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);

        /// <summary>
        /// Scales a vector.
        /// </summary>
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        /// <summary>
        /// Gets the dot product of two vectors.
        /// </summary>
        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>
        /// Gets the cross product of two vectors.
        /// </summary>
        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        /// <summary>
        /// Gets the vector scaled to length one, or the zero vector where it has no length.
        /// </summary>
        public Vec3 Normalized()
        {
            double length = Length;
            return length > 0.0 && !double.IsInfinity(length) ? new Vec3(X / length, Y / length, Z / length) : default(Vec3);
        }

        /// <summary>
        /// Determines whether another vector has exactly the same components.
        /// </summary>
        public bool Equals(Vec3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        /// <summary>
        /// Determines whether an object is a vector with exactly the same components.
        /// </summary>
        public override bool Equals(object obj) => obj is Vec3 other && Equals(other);

        /// <summary>
        /// Gets a hash code of the three components.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = hash * 397 ^ Y.GetHashCode();
                return hash * 397 ^ Z.GetHashCode();
            }
        }

        /// <summary>
        /// Describes the vector with its components in the invariant culture, to ten significant digits.
        /// </summary>
        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "({0:G10}, {1:G10}, {2:G10})", X, Y, Z);
    }

    /// <summary>
    /// An axis-aligned box in double precision, empty until a point is added.
    /// </summary>
    public readonly struct Bounds3
    {
        /// <summary>
        /// Initializes a box from two corners; the smaller of each coordinate is the minimum.
        /// </summary>
        public Bounds3(Vec3 a, Vec3 b)
        {
            Min = new Vec3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
            Max = new Vec3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
            HasValue = true;
        }

        /// <summary>
        /// Gets the corner with the smallest coordinates.
        /// </summary>
        public Vec3 Min { get; }

        /// <summary>
        /// Gets the corner with the largest coordinates.
        /// </summary>
        public Vec3 Max { get; }

        /// <summary>
        /// Gets whether the box holds a point; an empty box has no size and no centre.
        /// </summary>
        public bool HasValue { get; }

        /// <summary>
        /// Gets whether the box holds no point.
        /// </summary>
        public bool IsEmpty => !HasValue;

        /// <summary>
        /// Gets the middle of the box; the origin where the box is empty.
        /// </summary>
        public Vec3 Center => HasValue ? new Vec3((Min.X + Max.X) * 0.5, (Min.Y + Max.Y) * 0.5, (Min.Z + Max.Z) * 0.5) : default(Vec3);

        /// <summary>
        /// Gets the extent along each axis; zero where the box is empty.
        /// </summary>
        public Vec3 Size => HasValue ? Max - Min : default(Vec3);

        /// <summary>
        /// Gets half the length of the diagonal, the radius of the sphere round the box; zero where the box is empty.
        /// </summary>
        public double Radius => Size.Length * 0.5;

        /// <summary>
        /// Gets the box grown to take in a point; a point that is not finite is ignored.
        /// </summary>
        public Bounds3 Include(Vec3 point)
        {
            if (!point.IsFinite)
            {
                return this;
            }

            if (!HasValue)
            {
                return new Bounds3(point, point);
            }

            return new Bounds3(
                new Vec3(Math.Min(Min.X, point.X), Math.Min(Min.Y, point.Y), Math.Min(Min.Z, point.Z)),
                new Vec3(Math.Max(Max.X, point.X), Math.Max(Max.Y, point.Y), Math.Max(Max.Z, point.Z)));
        }

        /// <summary>
        /// Gets the box grown to take in another box.
        /// </summary>
        public Bounds3 Include(Bounds3 other)
        {
            if (!other.HasValue)
            {
                return this;
            }

            return Include(other.Min).Include(other.Max);
        }

        /// <summary>
        /// Describes the box.
        /// </summary>
        public override string ToString() => HasValue ? Min + " to " + Max : "empty";
    }
}
