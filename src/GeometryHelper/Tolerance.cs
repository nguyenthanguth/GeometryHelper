using System;
using System.Globalization;
using System.Threading;

namespace GeometryHelper
{
    /// <summary>
    /// Tolerance used for geometric comparisons.
    /// <para>
    /// Every comparison in this library that can be affected by floating point error
    /// takes a tolerance, and every such method has an overload without one that reads
    /// <see cref="Global"/>. Neither library compares coordinates with <c>==</c>.
    /// </para>
    /// <para>
    /// <see cref="Global"/> is one setting shared by both libraries. Changing it for a drawing in the
    /// plane changes it for a model in space as well.
    /// </para>
    /// <para>
    /// Make one with a constructor, or take <see cref="Default"/> or <see cref="Global"/>. <c>new Tolerance()</c> is
    /// not the default tolerance: like <c>default(Tolerance)</c> it runs no constructor and has every threshold at 0,
    /// under which only an exact match is equal and a face out of flat by rounding alone is refused.
    /// </para>
    /// </summary>
    public readonly struct Tolerance : IEquatable<Tolerance>
    {
        /// <summary>
        /// Default tolerance when comparing points: a hundredth of a millimetre in a model in millimetres.
        /// </summary>
        public const double DefaultEqualPoint = 1E-2;

        /// <summary>
        /// Default tolerance when comparing vectors.
        /// </summary>
        public const double DefaultEqualVector = 1E-2;

        /// <summary>
        /// Default tolerance when comparing angles for parallelism / perpendicularity, in radians (1 degree in radians).
        /// </summary>
        public const double DefaultEqualAngleRad = Math.PI / 180.0;

        /// <summary>
        /// Default distance threshold for deciding whether a set of points lies on a common plane: five hundredths of a
        /// millimetre in a model in millimetres.
        /// </summary>
        /// <remarks>
        /// A modeller's own cuts leave faces a little off flat: a concrete beam Tekla Structures cut a notch into came
        /// with the top face beside the notch 0.04 mm out. A face refused as not flat is a hole in the body it belongs to,
        /// and a body with a hole gives no section and the wrong volume, so the default lets such a face through.
        /// </remarks>
        public const double DefaultEqualPlanar = 0.05;

        /// <summary>
        /// Gets the default tolerance: <see cref="DefaultEqualPoint"/>, <see cref="DefaultEqualVector"/>,
        /// <see cref="DefaultEqualAngleRad"/> and <see cref="DefaultEqualPlanar"/> together.
        /// </summary>
        /// <remarks>
        /// <see cref="Global"/> starts as this tolerance, and this stays the defaults whatever <see cref="Global"/> is
        /// set to or a scope opened with <see cref="Use(Tolerance)"/> makes it. <c>new Tolerance()</c> is not it: that
        /// has every threshold at 0.
        /// </remarks>
        public static Tolerance Default => new Tolerance(DefaultEqualPoint, DefaultEqualVector, DefaultEqualAngleRad, DefaultEqualPlanar);

        /// <summary>
        /// The process-wide setting, held whole so that it is swapped in one step: a tolerance is several
        /// numbers, and a struct written while another thread reads it can be read half old and half new.
        /// </summary>
        private sealed class Held
        {
            internal readonly Tolerance Value;

            internal Held(Tolerance value)
            {
                Value = value;
            }
        }

        private static volatile Held _global = new Held(Default);

        [ThreadStatic]
        private static Held _scoped;

        /// <summary>
        /// Tolerance applied for overloads without explicit tolerance. It starts as <see cref="Default"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is one setting for the process, read by the plane and by space alike. Setting it replaces it
        /// whole, so a thread reading it while another sets it sees the old tolerance or the new one, never a
        /// mix; but an operation already under way may read it more than once, so set it while starting up,
        /// before geometry is built.
        /// </para>
        /// <para>
        /// Where a tolerance has to vary for a while on one thread — one drawing, one model — open a scope with
        /// <see cref="Use(Tolerance)"/>: this property returns the scope's tolerance on that thread until the
        /// scope is disposed, and every other thread keeps the process-wide one. Where it has to vary per call,
        /// pass it: every affected method takes one, and that overload touches nothing shared.
        /// </para>
        /// </remarks>
        public static Tolerance Global
        {
            get => (_scoped ?? _global).Value;
            set => _global = new Held(value);
        }

        /// <summary>
        /// Makes a tolerance the one every overload without a tolerance uses on this thread, until the returned
        /// scope is disposed.
        /// </summary>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The scope; disposing it puts back whatever this thread used before.</returns>
        /// <remarks>
        /// <code>
        /// using (Tolerance.Use(new Tolerance(1E-1, 1E-1)))
        /// {
        ///     plate.CollidesWith(bolt);   // within a tenth, on this thread only
        /// }
        /// </code>
        /// Scopes nest: each one disposed puts back the one it replaced. The scope belongs to the thread that
        /// opened it — work handed to other threads, by <c>Parallel</c> or a task, sees the process-wide
        /// setting, which is why the methods that spread their work, such as <c>Clash3.Find</c>, take the
        /// tolerance as it stands when they are called and pass it on.
        /// </remarks>
        public static IDisposable Use(Tolerance tolerance)
        {
            var scope = new Scope(_scoped);
            _scoped = new Held(tolerance);
            return scope;
        }

        /// <summary>
        /// A tolerance scope, putting back the tolerance it replaced when it is disposed.
        /// </summary>
        private sealed class Scope : IDisposable
        {
            private readonly Held _previous;
            private readonly int _thread;
            private bool _disposed;

            internal Scope(Held previous)
            {
                _previous = previous;
                _thread = Thread.CurrentThread.ManagedThreadId;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                if (Thread.CurrentThread.ManagedThreadId != _thread)
                {
                    throw new InvalidOperationException("A tolerance scope has to be closed on the thread that opened it.");
                }

                _scoped = _previous;
                _disposed = true;
            }
        }

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
            Guard.NonNegative(equalPoint, nameof(equalPoint), "A tolerance has to be a number, and cannot be negative.");

            Guard.NonNegative(equalVector, nameof(equalVector), "A tolerance has to be a number, and cannot be negative.");

            Guard.NonNegative(equalAngleRad, nameof(equalAngleRad), "A tolerance has to be a number, and cannot be negative.");

            Guard.NonNegative(equalPlanar, nameof(equalPlanar), "A tolerance has to be a number, and cannot be negative.");

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
