using System;
using System.Globalization;

namespace CommonGeometry.Datatype
{
    /// <summary>
    /// Represents an angle as a single value that can be read as either radians or degrees.
    /// <para>
    /// The value is stored in radians because every angular API built on this type works in radians:
    /// <c>GeoVector2.GetAngleTo</c> and <c>GeoRectangle2.AngleRad</c> in the plane,
    /// <c>GeoVector3.GetAngleTo</c> and <c>GeoTransform3.RotationX</c> in space, and
    /// <c>Tolerance.EqualAngleRad</c> in both. Degrees are a view over that value, converted on read.
    /// </para>
    /// <para>
    /// There is deliberately no public constructor taking a bare double. The reason this type exists is
    /// that a number on its own does not say which unit it is in, so the unit has to be named at the
    /// point of creation: use <see cref="FromRadians"/> or <see cref="FromDegrees"/>.
    /// </para>
    /// </summary>
    public readonly struct Angle : IEquatable<Angle>, IComparable<Angle>
    {
        #region Constants

        /// <summary>
        /// Number of degrees in one radian.
        /// </summary>
        public const double DegreesPerRadian = 180.0 / Math.PI;

        /// <summary>
        /// Number of radians in one degree.
        /// </summary>
        public const double RadiansPerDegree = Math.PI / 180.0;

        /// <summary>
        /// One full turn expressed in radians.
        /// </summary>
        public const double FullTurnRadians = 2.0 * Math.PI;

        /// <summary>
        /// One full turn expressed in degrees.
        /// </summary>
        public const double FullTurnDegrees = 360.0;

        #endregion

        #region Construction

        /// <summary>
        /// Initializes an angle from a value already expressed in radians.
        /// </summary>
        private Angle(double radians)
        {
            Radians = radians;
        }

        /// <summary>
        /// Creates an angle from a value in radians.
        /// </summary>
        /// <param name="radians">The angle in radians.</param>
        /// <returns>The corresponding angle.</returns>
        public static Angle FromRadians(double radians) => new Angle(radians);

        /// <summary>
        /// Creates an angle from a value in degrees.
        /// </summary>
        /// <param name="degrees">The angle in degrees.</param>
        /// <returns>The corresponding angle.</returns>
        public static Angle FromDegrees(double degrees) => new Angle(degrees * RadiansPerDegree);

        #endregion

        #region Well-known values

        /// <summary>
        /// The zero angle (0 degrees).
        /// </summary>
        public static Angle Zero => new Angle(0.0);

        /// <summary>
        /// A right angle (90 degrees).
        /// </summary>
        public static Angle Right => new Angle(Math.PI * 0.5);

        /// <summary>
        /// A straight angle (180 degrees).
        /// </summary>
        public static Angle Straight => new Angle(Math.PI);

        /// <summary>
        /// A full turn (360 degrees).
        /// </summary>
        public static Angle FullTurn => new Angle(FullTurnRadians);

        #endregion

        #region Values

        /// <summary>
        /// Gets the angle in radians. This is the stored value.
        /// </summary>
        public double Radians { get; }

        /// <summary>
        /// Gets the angle in degrees, converted from the stored radian value on each read.
        /// </summary>
        public double Degrees => Radians * DegreesPerRadian;

        #endregion

        #region Static conversion of raw values

        /// <summary>
        /// Converts a raw radian value to degrees, without going through an <see cref="Angle"/> instance.
        /// </summary>
        public static double ToDegrees(double radians) => radians * DegreesPerRadian;

        /// <summary>
        /// Converts a raw degree value to radians, without going through an <see cref="Angle"/> instance.
        /// </summary>
        public static double ToRadians(double degrees) => degrees * RadiansPerDegree;

        #endregion

        #region Normalization

        /// <summary>
        /// Wraps the angle into the range [0, 2*PI), the form used to express a rotation as a full turn.
        /// A rotation of -90 degrees normalizes to 270 degrees.
        /// </summary>
        /// <returns>The equivalent angle in [0, 2*PI).</returns>
        public Angle Normalize()
        {
            double wrapped = Radians % FullTurnRadians;

            if (wrapped < 0.0)
            {
                wrapped += FullTurnRadians;

                // A tiny negative input rounds up to exactly one full turn when shifted, which would fall
                // outside the half-open range this method promises.
                if (wrapped >= FullTurnRadians)
                {
                    wrapped = 0.0;
                }
            }

            return new Angle(wrapped);
        }

        /// <summary>
        /// Wraps the angle into the range (-PI, PI], the form used to express the shortest rotation to a
        /// direction. A rotation of 270 degrees normalizes to -90 degrees.
        /// </summary>
        /// <returns>The equivalent angle in (-PI, PI].</returns>
        public Angle NormalizeSigned()
        {
            double wrapped = Radians % FullTurnRadians;

            if (wrapped > Math.PI)
            {
                wrapped -= FullTurnRadians;
            }
            else if (wrapped <= -Math.PI)
            {
                wrapped += FullTurnRadians;
            }

            return new Angle(wrapped);
        }

        #endregion

        #region Arithmetic

        /// <summary>
        /// Adds another angle to this one.
        /// </summary>
        public Angle Add(Angle other) => new Angle(Radians + other.Radians);

        /// <summary>
        /// Subtracts another angle from this one.
        /// </summary>
        public Angle Subtract(Angle other) => new Angle(Radians - other.Radians);

        /// <summary>
        /// Multiplies the angle by a scalar factor.
        /// </summary>
        public Angle Multiply(double factor) => new Angle(Radians * factor);

        /// <summary>
        /// Divides the angle by a scalar divisor.
        /// </summary>
        public Angle Divide(double divisor) => new Angle(Radians / divisor);

        /// <summary>
        /// Gets the angle with the opposite sign.
        /// </summary>
        public Angle Negate() => new Angle(-Radians);

        /// <summary>
        /// Gets the angle with its magnitude only.
        /// </summary>
        public Angle Abs() => new Angle(Math.Abs(Radians));

        #endregion

        #region Comparison

        /// <summary>
        /// Compares whether this angle equals another angle using the default angular tolerance.
        /// </summary>
        /// <remarks>
        /// The comparison is made on the stored values, not on the directions they represent, so 0 and
        /// one full turn are not equal here. Call <see cref="Normalize"/> on both sides first when the
        /// question is about direction rather than about rotation.
        /// </remarks>
        public bool IsEqualTo(Angle other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this angle equals another angle within an angular tolerance.
        /// </summary>
        /// <remarks>
        /// The comparison is made on the stored values, not on the directions they represent, so 0 and
        /// one full turn are not equal here. Call <see cref="Normalize"/> on both sides first when the
        /// question is about direction rather than about rotation.
        /// </remarks>
        public bool IsEqualTo(Angle other, Tolerance tolerance)
        {
            return Math.Abs(Radians - other.Radians) <= tolerance.EqualAngleRad;
        }

        /// <summary>
        /// Compares this angle with another angle by stored value.
        /// </summary>
        public int CompareTo(Angle other) => Radians.CompareTo(other.Radians);

        /// <summary>
        /// Indicates whether this angle has exactly the same stored value as another angle.
        /// </summary>
        public bool Equals(Angle other) => Radians.Equals(other.Radians);

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        public override bool Equals(object obj) => obj is Angle other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode() => Radians.GetHashCode();

        #endregion

        #region Operators

        /// <summary>
        /// Adds two angles.
        /// </summary>
        public static Angle operator +(Angle left, Angle right) => left.Add(right);

        /// <summary>
        /// Subtracts one angle from another.
        /// </summary>
        public static Angle operator -(Angle left, Angle right) => left.Subtract(right);

        /// <summary>
        /// Negates an angle.
        /// </summary>
        public static Angle operator -(Angle angle) => angle.Negate();

        /// <summary>
        /// Multiplies an angle by a scalar factor.
        /// </summary>
        public static Angle operator *(Angle angle, double factor) => angle.Multiply(factor);

        /// <summary>
        /// Multiplies an angle by a scalar factor.
        /// </summary>
        public static Angle operator *(double factor, Angle angle) => angle.Multiply(factor);

        /// <summary>
        /// Divides an angle by a scalar divisor.
        /// </summary>
        public static Angle operator /(Angle angle, double divisor) => angle.Divide(divisor);

        /// <summary>
        /// Compares two angles for exact equality of the stored value.
        /// </summary>
        public static bool operator ==(Angle left, Angle right) => left.Equals(right);

        /// <summary>
        /// Compares two angles for inequality of the stored value.
        /// </summary>
        public static bool operator !=(Angle left, Angle right) => !left.Equals(right);

        /// <summary>
        /// Determines whether one angle is smaller than another.
        /// </summary>
        public static bool operator <(Angle left, Angle right) => left.Radians < right.Radians;

        /// <summary>
        /// Determines whether one angle is greater than another.
        /// </summary>
        public static bool operator >(Angle left, Angle right) => left.Radians > right.Radians;

        /// <summary>
        /// Determines whether one angle is smaller than or equal to another.
        /// </summary>
        public static bool operator <=(Angle left, Angle right) => left.Radians <= right.Radians;

        /// <summary>
        /// Determines whether one angle is greater than or equal to another.
        /// </summary>
        public static bool operator >=(Angle left, Angle right) => left.Radians >= right.Radians;

        #endregion

        /// <summary>
        /// Represents the angle in degrees.
        /// </summary>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.000} deg ({1:0.0000} rad)",
                Degrees,
                Radians);
        }
    }
}
