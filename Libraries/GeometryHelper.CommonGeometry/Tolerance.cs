using System;
using System.Globalization;

namespace GeometryHelper.CommonGeometry
{
    /// <summary>
    /// Tolerance used for geometric comparisons.
    /// <para>
    /// Every comparison in PlaneGeometry and SolidGeometry that can be affected by floating point error
    /// takes a tolerance, and every such method has an overload without one that reads
    /// <see cref="Global"/>. Neither library compares coordinates with <c>==</c>.
    /// </para>
    /// <para>
    /// <see cref="Global"/> is one setting shared by both libraries. Changing it for a drawing in the
    /// plane changes it for a model in space as well.
    /// </para>
    /// </summary>
    public readonly struct Tolerance : IEquatable<Tolerance>
    {
        /// <summary>
        /// Default tolerance when comparing points.
        /// </summary>
        public const double DefaultEqualPoint = 1E-4;

        /// <summary>
        /// Default tolerance when comparing vectors.
        /// </summary>
        public const double DefaultEqualVector = 1E-4;

        /// <summary>
        /// Default tolerance when comparing angles for parallelism / perpendicularity, in radians (1 degree in radians).
        /// </summary>
        public const double DefaultEqualAngleRad = Math.PI / 180.0;

        /// <summary>
        /// Default distance threshold for deciding whether a set of points lies on a common plane.
        /// </summary>
        public const double DefaultEqualPlanar = 1E-4;

        /// <summary>
        /// Tolerance applied for overloads without explicit tolerance.
        /// </summary>
        /// <remarks>
        /// This is process-wide mutable state, and it is not synchronized. Set it once while starting up,
        /// before any geometry is built, and read it thereafter. Changing it while other threads are
        /// working means they may read the old value, the new one, or a mix across the several comparisons
        /// of one operation — and it changes the answers in the plane and in space alike, since both
        /// libraries read this one setting.
        /// <para>
        /// Where a tolerance has to vary — per drawing, per model, per thread — pass it explicitly. Every
        /// affected method takes one, and that overload touches nothing shared.
        /// </para>
        /// </remarks>
        public static Tolerance Global { get; set; } =
            new Tolerance(DefaultEqualPoint, DefaultEqualVector, DefaultEqualAngleRad, DefaultEqualPlanar);

        /// <summary>
        /// Initializes a tolerance instance with thresholds for points and vectors, using the default
        /// angular and planar thresholds.
        /// </summary>
        /// <param name="equalPoint">Distance threshold when comparing two points.</param>
        /// <param name="equalVector">Threshold when comparing two vectors.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when one of the thresholds is negative.</exception>
        public Tolerance(double equalPoint, double equalVector)
            : this(equalPoint, equalVector, DefaultEqualAngleRad, DefaultEqualPlanar)
        {
        }

        /// <summary>
        /// Initializes a tolerance instance with thresholds for points, vectors and angles. The planar
        /// threshold follows <paramref name="equalPoint"/>, since coplanarity is measured as a distance.
        /// </summary>
        /// <param name="equalPoint">Distance threshold when comparing two points.</param>
        /// <param name="equalVector">Threshold when comparing two vectors.</param>
        /// <param name="equalAngleRad">Angular threshold when comparing angles or parallelism, in radians.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when one of the thresholds is negative.</exception>
        public Tolerance(double equalPoint, double equalVector, double equalAngleRad)
            : this(equalPoint, equalVector, equalAngleRad, equalPoint)
        {
        }

        /// <summary>
        /// Initializes a tolerance instance with thresholds for points, vectors, angles and coplanarity.
        /// </summary>
        /// <param name="equalPoint">Distance threshold when comparing two points.</param>
        /// <param name="equalVector">Threshold when comparing two vectors.</param>
        /// <param name="equalAngleRad">Angular threshold when comparing angles or parallelism, in radians.</param>
        /// <param name="equalPlanar">Distance threshold when deciding whether points share a plane.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when one of the thresholds is negative.</exception>
        public Tolerance(double equalPoint, double equalVector, double equalAngleRad, double equalPlanar)
        {
            if (equalPoint < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(equalPoint), "Tolerance cannot be negative.");
            }

            if (equalVector < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(equalVector), "Tolerance cannot be negative.");
            }

            if (equalAngleRad < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(equalAngleRad), "Tolerance cannot be negative.");
            }

            if (equalPlanar < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(equalPlanar), "Tolerance cannot be negative.");
            }

            EqualPoint = equalPoint;
            EqualVector = equalVector;
            EqualAngleRad = equalAngleRad;
            EqualPlanar = equalPlanar;
            EqualAngleSin = Math.Sin(equalAngleRad);
        }

        /// <summary>
        /// Distance threshold to consider two points as coincident, in drawing units.
        /// </summary>
        public double EqualPoint { get; }

        /// <summary>
        /// Threshold to consider two vectors as equal.
        /// </summary>
        public double EqualVector { get; }

        /// <summary>
        /// Angular threshold to consider two directions / lines as parallel or perpendicular, in radians.
        /// </summary>
        public double EqualAngleRad { get; }

        /// <summary>
        /// Distance threshold to consider a point as lying on a plane, in drawing units.
        /// <para>
        /// This is separate from <see cref="EqualPoint"/> because coplanarity is checked far from the
        /// reference point: a polygon several metres across turns a hundredth of a degree of tilt into a
        /// deviation of nearly a millimetre, so the threshold that decides whether two points coincide is
        /// the wrong one to decide whether a face is flat.
        /// </para>
        /// </summary>
        public double EqualPlanar { get; }

        /// <summary>
        /// Sine of <see cref="EqualAngleRad"/>, computed once here because the angular comparisons in
        /// Intersection and Parallel sit inside nested edge loops, where a transcendental call per
        /// comparison is a measurable share of the total cost.
        /// <para>
        /// The default struct value leaves this at 0, which is exactly Sin(0) for the matching
        /// EqualAngleRad of 0, so an uninitialized Tolerance stays self-consistent.
        /// </para>
        /// </summary>
        internal double EqualAngleSin { get; }

        /// <summary>
        /// Compares two tolerance instances exactly.
        /// </summary>
        public bool Equals(Tolerance other)
        {
            return EqualPoint.Equals(other.EqualPoint) &&
                   EqualVector.Equals(other.EqualVector) &&
                   EqualAngleRad.Equals(other.EqualAngleRad) &&
                   EqualPlanar.Equals(other.EqualPlanar);
        }

        /// <summary>
        /// Compares with an arbitrary object.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is Tolerance other && Equals(other);
        }

        /// <summary>
        /// Hash code built from thresholds.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + EqualPoint.GetHashCode();
                hash = hash * 31 + EqualVector.GetHashCode();
                hash = hash * 31 + EqualAngleRad.GetHashCode();
                hash = hash * 31 + EqualPlanar.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Compares two Tolerance instances for equality.
        /// </summary>
        public static bool operator ==(Tolerance tolerance1, Tolerance tolerance2) => tolerance1.Equals(tolerance2);

        /// <summary>
        /// Compares two Tolerance instances for inequality.
        /// </summary>
        public static bool operator !=(Tolerance tolerance1, Tolerance tolerance2) => !tolerance1.Equals(tolerance2);

        /// <summary>
        /// Represents the thresholds as a string.
        /// </summary>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "(EqualPoint: {0}, EqualVector: {1}, EqualAngleRad: {2:0.000}, EqualPlanar: {3})",
                EqualPoint,
                EqualVector,
                EqualAngleRad,
                EqualPlanar);
        }
    }
}
