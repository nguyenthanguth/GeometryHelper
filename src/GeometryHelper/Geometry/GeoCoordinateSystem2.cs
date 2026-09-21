using System;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A local coordinate system in the plane: an origin and two orthonormal axes, the counterpart of
    /// <see cref="GeoCoordinateSystem3"/> in space.
    /// <para>
    /// It is the place a drawing sits rather than something done to a drawing. <see cref="GeoTransform2"/>
    /// can say the same thing, and <see cref="ToTransform"/> hands it over in that form, but a
    /// transformation may also scale, mirror or shear, and reading one backwards means inverting a matrix.
    /// A frame is rigid by construction, and reading it backwards is turning the axes round, which costs
    /// nothing and loses nothing.
    /// </para>
    /// </summary>
    public readonly struct GeoCoordinateSystem2 : IEquatable<GeoCoordinateSystem2>
    {
        /// <summary>
        /// Gets where the origin of the system sits.
        /// </summary>
        public GeoPoint2 Origin { get; }

        /// <summary>
        /// Gets the X axis of the system, of unit length.
        /// </summary>
        public GeoVector2 XAxis { get; }

        /// <summary>
        /// Gets the Y axis of the system, of unit length and a quarter turn counter-clockwise from the X
        /// axis.
        /// </summary>
        public GeoVector2 YAxis { get; }

        /// <summary>
        /// Gets the system the whole drawing is held in: the origin, with the axes along X and Y.
        /// </summary>
        public static GeoCoordinateSystem2 Global => new GeoCoordinateSystem2(GeoPoint2.Origin, GeoVector2.XAxis, GeoVector2.YAxis);

        /// <summary>
        /// Initializes a system at an origin, with its X axis along a direction.
        /// </summary>
        /// <param name="origin">Where the origin sits.</param>
        /// <param name="xAxis">Which way the X axis points; its length does not matter.</param>
        /// <remarks>
        /// The Y axis follows from the X axis, a quarter turn counter-clockwise from it, so a system in the
        /// plane can never come out skewed: unlike in space, there is nothing to correct.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the axis has no length.</exception>
        public GeoCoordinateSystem2(GeoPoint2 origin, GeoVector2 xAxis)
        {
            if (!xAxis.TryGetNormal(out GeoVector2 unit))
            {
                throw new ArgumentException("A coordinate system needs an X axis with a length.", nameof(xAxis));
            }

            Origin = origin;
            XAxis = unit;
            YAxis = new GeoVector2(-unit.Y, unit.X);
        }

        /// <summary>
        /// Initializes a system at an origin, turned by an angle.
        /// </summary>
        /// <param name="origin">Where the origin sits.</param>
        /// <param name="angleRad">How far the X axis is turned from the drawing's own X axis, counter-clockwise.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is not a number.</exception>
        public GeoCoordinateSystem2(GeoPoint2 origin, double angleRad)
        {
            if (double.IsNaN(angleRad) || double.IsInfinity(angleRad))
            {
                throw new ArgumentOutOfRangeException(nameof(angleRad), "An angle must be a number.");
            }

            Origin = origin;
            XAxis = new GeoVector2(Math.Cos(angleRad), Math.Sin(angleRad));
            YAxis = new GeoVector2(-XAxis.Y, XAxis.X);
        }

        private GeoCoordinateSystem2(GeoPoint2 origin, GeoVector2 xAxis, GeoVector2 yAxis)
        {
            Origin = origin;
            XAxis = xAxis;
            YAxis = yAxis;
        }

        /// <summary>
        /// Gets how far the X axis is turned from the drawing's own X axis, counter-clockwise.
        /// </summary>
        public double AngleRad => Math.Atan2(XAxis.Y, XAxis.X);

        /// <summary>
        /// Creates a copy of this system.
        /// </summary>
        public GeoCoordinateSystem2 Clone() => new GeoCoordinateSystem2(Origin, XAxis, YAxis);

        /// <summary>
        /// Gets a point of the drawing read in this system.
        /// </summary>
        /// <param name="point">The point, in the drawing's own coordinates.</param>
        /// <returns>The same point, in this system's coordinates.</returns>
        public GeoPoint2 ToLocal(GeoPoint2 point)
        {
            GeoVector2 offset = Origin.GetVectorTo(point);

            return new GeoPoint2(offset.DotProduct(XAxis), offset.DotProduct(YAxis));
        }

        /// <summary>
        /// Gets a point of this system put back into the drawing.
        /// </summary>
        /// <param name="point">The point, in this system's coordinates.</param>
        /// <returns>The same point, in the drawing's own coordinates.</returns>
        public GeoPoint2 ToGlobal(GeoPoint2 point)
        {
            return new GeoPoint2(
                Origin.X + XAxis.X * point.X + YAxis.X * point.Y,
                Origin.Y + XAxis.Y * point.X + YAxis.Y * point.Y);
        }

        /// <summary>
        /// Gets a vector of the drawing read in this system.
        /// </summary>
        /// <remarks>A vector has no place, so only the axes turn it; the origin does not move it.</remarks>
        public GeoVector2 ToLocal(GeoVector2 vector)
        {
            return new GeoVector2(vector.DotProduct(XAxis), vector.DotProduct(YAxis));
        }

        /// <summary>
        /// Gets a vector of this system put back into the drawing.
        /// </summary>
        public GeoVector2 ToGlobal(GeoVector2 vector)
        {
            return new GeoVector2(
                XAxis.X * vector.X + YAxis.X * vector.Y,
                XAxis.Y * vector.X + YAxis.Y * vector.Y);
        }

        /// <summary>
        /// Gets the system as the transformation that places geometry built about the origin.
        /// </summary>
        public GeoTransform2 ToTransform() => GeoTransform2.FromCoordinateSystem(this);

        /// <summary>
        /// Gets this system under a transformation.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The system where the transformation puts it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation leaves the axes with no length.</exception>
        public GeoCoordinateSystem2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoVector2 moved = transform.Transform(XAxis);

            if (!moved.TryGetNormal(out GeoVector2 unit))
            {
                throw new InvalidOperationException("This transformation leaves the coordinate system with no axes.");
            }

            return new GeoCoordinateSystem2(transform.Transform(Origin), unit);
        }

        /// <summary>
        /// Determines whether another system holds exactly the same origin and axes.
        /// </summary>
        public bool Equals(GeoCoordinateSystem2 other)
        {
            return Origin.Equals(other.Origin) && XAxis.Equals(other.XAxis) && YAxis.Equals(other.YAxis);
        }

        /// <summary>
        /// Determines whether the specified object is an equal system.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoCoordinateSystem2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this system.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Origin.GetHashCode();
                hash = hash * 397 ^ XAxis.GetHashCode();
                hash = hash * 397 ^ YAxis.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Determines whether another system sits in the same place, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoCoordinateSystem2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another system sits in the same place, within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoCoordinateSystem2 other, Tolerance tolerance)
        {
            return Origin.IsEqualTo(other.Origin, tolerance)
                && XAxis.IsEqualTo(other.XAxis, tolerance)
                && YAxis.IsEqualTo(other.YAxis, tolerance);
        }

        /// <summary>
        /// Determines whether two systems hold exactly the same values.
        /// </summary>
        public static bool operator ==(GeoCoordinateSystem2 left, GeoCoordinateSystem2 right) => left.Equals(right);

        /// <summary>
        /// Determines whether two systems hold different values.
        /// </summary>
        public static bool operator !=(GeoCoordinateSystem2 left, GeoCoordinateSystem2 right) => !left.Equals(right);

        /// <summary>
        /// Describes the system.
        /// </summary>
        public override string ToString() => $"LCS(Origin: {Origin}, X: {XAxis}, Y: {YAxis})";
    }
}
