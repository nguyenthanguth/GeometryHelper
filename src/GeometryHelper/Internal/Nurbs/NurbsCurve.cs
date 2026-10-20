using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// A NURBS curve in two or three dimensions held as flat arrays of numbers: the one engine under
    /// <see cref="Geometry.GeoSpline2"/> and <see cref="Geometry.GeoSpline3"/>, so that the mathematics is written, and
    /// checked, once.
    /// <para>
    /// The curve is clamped: its first and last <see cref="Degree"/> + 1 knots are equal, so it starts at its first
    /// control point and ends at its last. It is immutable; every operation gives a new curve and leaves this one as
    /// it was, so the arrays are shared freely between a curve and the ones made from it.
    /// </para>
    /// <para>
    /// A rational curve is worked in homogeneous coordinates, a control point at (w x, w y, w z, w), where the points
    /// move in a space one dimension up and are divided by w at the end; with positive weights every step is a mean, and
    /// so cannot go wrong by more than rounding. A curve whose weights are all one has no w at all: the weight is
    /// neither stored nor divided by, so that a plain B-spline is worked on exactly its own numbers.
    /// </para>
    /// </summary>
    internal sealed partial class NurbsCurve
    {
        /// <summary>The most any spline's degree can be.</summary>
        internal const int MostDegree = 10;

        /// <summary>
        /// How far apart the largest and the smallest weight of a spline may be, as a ratio. The length is right to 1E-12 up to a ratio
        /// of 1E15; one of 1E16 comes out 1E-7 short and one of 1E20 a third short, so a ratio past this, which leaves a margin, is
        /// refused.
        /// </summary>
        internal const double MostWeightRatio = 1E12;

        /// <summary>The number of coordinates of a point: 2 or 3.</summary>
        internal readonly int Dim;

        /// <summary>The degree of the curve.</summary>
        internal readonly int Degree;

        /// <summary>The number of control points.</summary>
        internal readonly int Count;

        /// <summary>Whether any weight is other than one.</summary>
        internal readonly bool Rational;

        /// <summary>The numbers a control point takes in homogeneous form: Dim + 1 when rational, Dim when not.</summary>
        internal readonly int Stride;

        /// <summary>The knots, Count + Degree + 1 of them.</summary>
        internal readonly double[] Knots;

        /// <summary>The control points, Dim numbers each.</summary>
        internal readonly double[] Points;

        /// <summary>The weights, one per control point, all one when the curve is not rational.</summary>
        internal readonly double[] Weights;

        /// <summary>The control points in homogeneous form, Stride numbers each; the points themselves when not rational.</summary>
        internal readonly double[] Homogeneous;

        private double _scale = -1.0;
        private double _extent = -1.0;

        /// <summary>
        /// Takes arrays that have been checked, which the curve then owns: nobody may write to them again.
        /// </summary>
        internal NurbsCurve(int dim, int degree, double[] points, double[] knots, double[] weights)
        {
            Dim = dim;
            Degree = degree;
            Count = points.Length / dim;
            Knots = knots;
            Points = points;

            bool rational = false;

            if (weights != null)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] != 1.0)
                    {
                        rational = true;
                        break;
                    }
                }
            }

            Rational = rational;
            Stride = rational ? dim + 1 : dim;
            Weights = weights ?? Ones(Count);

            if (rational)
            {
                Homogeneous = new double[Count * Stride];

                for (int i = 0; i < Count; i++)
                {
                    for (int k = 0; k < dim; k++)
                    {
                        Homogeneous[i * Stride + k] = Weights[i] * points[i * dim + k];
                    }

                    Homogeneous[i * Stride + dim] = Weights[i];
                }
            }
            else
            {
                Homogeneous = points;
            }
        }

        private static double[] Ones(int count)
        {
            var ones = new double[count];

            for (int i = 0; i < count; i++)
            {
                ones[i] = 1.0;
            }

            return ones;
        }

        /// <summary>The first parameter of the curve.</summary>
        internal double Start => Knots[Degree];

        /// <summary>The last parameter of the curve.</summary>
        internal double End => Knots[Count];

        /// <summary>
        /// The size of the largest coordinate of any control point, which is the scale the rounding of the curve's
        /// numbers goes by.
        /// </summary>
        internal double Scale
        {
            get
            {
                if (_scale < 0.0)
                {
                    double largest = 0.0;

                    for (int i = 0; i < Points.Length; i++)
                    {
                        largest = Math.Max(largest, Math.Abs(Points[i]));
                    }

                    _scale = largest;
                }

                return _scale;
            }
        }

        /// <summary>
        /// Makes the same curve with other control points, the knots and weights shared: the curve under an affine map,
        /// which is the map of its control points.
        /// </summary>
        internal NurbsCurve WithPoints(double[] points) => new NurbsCurve(Dim, Degree, points, Knots, Rational ? Weights : null);

        #region Checking

        /// <summary>
        /// Says whether every point of a list is the first one, to the last bit.
        /// </summary>
        internal static bool AllTheSame(double[] points, int dim)
        {
            for (int i = dim; i < points.Length; i++)
            {
                if (!points[i].Equals(points[i % dim]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the size of the largest span the control points have along any one axis: the size of the curve, by
        /// which what counts as no movement is measured.
        /// </summary>
        internal double Extent
        {
            get
            {
                if (_extent < 0.0)
                {
                    double extent = 0.0;

                    for (int k = 0; k < Dim; k++)
                    {
                        double low = double.PositiveInfinity;
                        double high = double.NegativeInfinity;

                        for (int i = 0; i < Count; i++)
                        {
                            low = Math.Min(low, Points[i * Dim + k]);
                            high = Math.Max(high, Points[i * Dim + k]);
                        }

                        extent = Math.Max(extent, high - low);
                    }

                    _extent = extent;
                }

                return _extent;
            }
        }

        /// <summary>
        /// Checks the numbers a curve is to be made of, refusing whatever would make it something else than a clamped
        /// curve with control points that are not all one point.
        /// </summary>
        /// <param name="degree">The degree.</param>
        /// <param name="points">The control points as flat coordinates, Dim each.</param>
        /// <param name="dim">The number of coordinates of a point.</param>
        /// <param name="knots">The knots.</param>
        /// <param name="weights">The weights, or null for all one.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10, or a weight is not a positive number.</exception>
        /// <exception cref="ArgumentException">Thrown when the points, knots or weights are not as a clamped spline needs them.</exception>
        internal static void Check(int degree, double[] points, int dim, double[] knots, double[] weights)
        {
            if (degree < 1 || degree > MostDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, "The degree of a spline is from 1 to " + MostDegree + ".");
            }

            int count = points.Length / dim;

            if (count < degree + 1)
            {
                throw new ArgumentException("A spline of degree " + degree + " needs at least " + (degree + 1) + " control points; " + count + " were given.", "controlPoints");
            }

            for (int i = 0; i < points.Length; i++)
            {
                if (!Guard.IsFinite(points[i]))
                {
                    throw new ArgumentException("Control point " + (i / dim) + " is not a finite point.", "controlPoints");
                }
            }

            if (AllTheSame(points, dim))
            {
                throw new ArgumentException("The control points are all one point, which no curve runs along.", "controlPoints");
            }

            if (knots.Length != count + degree + 1)
            {
                throw new ArgumentException(
                    "A spline of degree " + degree + " with " + count + " control points has " + (count + degree + 1) + " knots; " + knots.Length + " were given.",
                    nameof(knots));
            }

            for (int i = 0; i < knots.Length; i++)
            {
                if (!Guard.IsFinite(knots[i]))
                {
                    throw new ArgumentException("Knot " + i + " is not a finite number.", nameof(knots));
                }

                if (i > 0 && knots[i] < knots[i - 1])
                {
                    throw new ArgumentException("The knots must not decrease; knot " + i + " is below the one before it.", nameof(knots));
                }
            }

            double first = knots[0];
            double last = knots[knots.Length - 1];

            for (int i = 1; i <= degree; i++)
            {
                if (!knots[i].Equals(first) || !knots[knots.Length - 1 - i].Equals(last))
                {
                    throw new ArgumentException("A clamped spline repeats its first and its last knot " + (degree + 1) + " times, so that it starts and ends at its end control points.", nameof(knots));
                }
            }

            if (!(last > first))
            {
                throw new ArgumentException("The last knot must be above the first.", nameof(knots));
            }

            if (knots[degree + 1].Equals(first) || knots[knots.Length - 2 - degree].Equals(last))
            {
                throw new ArgumentException("The first and the last knot may be repeated " + (degree + 1) + " times and no more.", nameof(knots));
            }

            int run = 1;

            for (int i = degree + 2; i < knots.Length - degree - 1; i++)
            {
                run = knots[i].Equals(knots[i - 1]) ? run + 1 : 1;

                if (run > degree)
                {
                    throw new ArgumentException("An inner knot may be repeated " + degree + " times at most, or the curve would break in two there.", nameof(knots));
                }
            }

            if (weights != null)
            {
                if (weights.Length != count)
                {
                    throw new ArgumentException("There has to be one weight for each of the " + count + " control points; " + weights.Length + " were given.", nameof(weights));
                }

                double lightest = double.PositiveInfinity;
                double heaviest = 0.0;

                for (int i = 0; i < weights.Length; i++)
                {
                    Guard.Positive(weights[i], nameof(weights), "A weight has to be a positive number.");
                    lightest = Math.Min(lightest, weights[i]);
                    heaviest = Math.Max(heaviest, weights[i]);
                }

                if (heaviest > MostWeightRatio * lightest)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(weights),
                        heaviest / lightest,
                        "The weights of a spline are no more than " + MostWeightRatio.ToString("0E+0", System.Globalization.CultureInfo.InvariantCulture) + " to one apart: past that the length of the spline cannot be worked out right.");
                }
            }
        }

        #endregion

        #region Evaluating

        /// <summary>
        /// Finds the knot span of a parameter: the index i with knot i no larger than it and knot i + 1 above it, the
        /// last span for the end of the curve.
        /// </summary>
        internal int FindSpan(double t) => FindSpan(Knots, Degree, Count, t);

        /// <summary>
        /// Finds the knot span of a parameter in a knot vector of a curve of a degree and a number of control points.
        /// </summary>
        internal static int FindSpan(double[] knots, int degree, int count, double t)
        {
            int n = count - 1;

            if (t >= knots[n + 1])
            {
                return n;
            }

            if (t <= knots[degree])
            {
                return degree;
            }

            int low = degree;
            int high = n + 1;

            while (high - low > 1)
            {
                int middle = (low + high) >> 1;

                if (t < knots[middle])
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }

            return low;
        }

        /// <summary>
        /// Puts a parameter inside the domain of the curve.
        /// </summary>
        internal double Clamp(double t) => t <= Start ? Start : t >= End ? End : t;

        /// <summary>
        /// Gets the knot value at a share of the domain, from 0 for the first knot to 1 for the last, which are the first and
        /// last knot to the last bit.
        /// </summary>
        internal double KnotAtParameter(double parameter)
        {
            if (!(parameter > 0.0))
            {
                return Start;
            }

            if (!(parameter < 1.0))
            {
                return End;
            }

            return Clamp(Start + parameter * (End - Start));
        }

        /// <summary>
        /// Gets the share of the domain at a knot value, from 0 for the first knot to 1 for the last.
        /// </summary>
        internal double ParameterAtKnot(double knot)
        {
            if (!(knot > Start))
            {
                return 0.0;
            }

            if (!(knot < End))
            {
                return 1.0;
            }

            return (knot - Start) / (End - Start);
        }

        [ThreadStatic]
        private static double[] _scratch;

        private static double[] Scratch(int length)
        {
            double[] scratch = _scratch;

            if (scratch == null || scratch.Length < length)
            {
                scratch = new double[Math.Max(length, 128)];
                _scratch = scratch;
            }

            return scratch;
        }

        /// <summary>
        /// Gets the point of the curve at a parameter, by de Boor's scheme in homogeneous coordinates.
        /// </summary>
        /// <param name="t">The parameter, which is put inside the domain.</param>
        /// <param name="result">Dim numbers to write the point to.</param>
        internal void PointAt(double t, double[] result)
        {
            // A clamped curve passes through its end control points; dividing by a weight off one there would
            // leave the point a last digit off them.
            if (t <= Start || t >= End)
            {
                Array.Copy(Points, t <= Start ? 0 : (Count - 1) * Dim, result, 0, Dim);
                return;
            }

            double[] h = Scratch((Degree + 1) * Stride + Stride);
            HomogeneousPointAt(t, h);

            if (Rational)
            {
                double w = h[Dim];

                for (int k = 0; k < Dim; k++)
                {
                    result[k] = h[k] / w;
                }
            }
            else
            {
                for (int k = 0; k < Dim; k++)
                {
                    result[k] = h[k];
                }
            }
        }

        /// <summary>
        /// Gets the homogeneous point at a parameter into the first Stride numbers of an array that has room for
        /// (Degree + 1) * Stride numbers.
        /// </summary>
        private void HomogeneousPointAt(double t, double[] h)
        {
            t = Clamp(t);
            int p = Degree;
            int s = FindSpan(t);
            int stride = Stride;

            for (int j = 0; j <= p; j++)
            {
                Array.Copy(Homogeneous, (s - p + j) * stride, h, j * stride, stride);
            }

            for (int r = 1; r <= p; r++)
            {
                for (int j = p; j >= r; j--)
                {
                    int i = s - p + j;
                    double alpha = (t - Knots[i]) / (Knots[i + p + 1 - r] - Knots[i]);
                    double beta = 1.0 - alpha;

                    for (int k = 0; k < stride; k++)
                    {
                        h[j * stride + k] = beta * h[(j - 1) * stride + k] + alpha * h[j * stride + k];
                    }
                }
            }

            if (p > 0)
            {
                Array.Copy(h, p * stride, h, 0, stride);
            }
        }

        /// <summary>
        /// Gets the derivatives of the curve up to an order at a parameter, as Dim numbers each.
        /// </summary>
        /// <param name="t">The parameter, put inside the domain. At a knot where the curve is not smooth the derivatives of the span after it are given, and those of the span before it at the end of the curve.</param>
        /// <param name="order">The highest derivative wanted; at least one.</param>
        /// <returns>An array of order + 1 rows: the point, then each derivative.</returns>
        internal double[][] Derivatives(double t, int order)
        {
            t = Clamp(t);
            int p = Degree;
            int s = FindSpan(t);
            int stride = Stride;

            // The homogeneous derivatives of the numerator, from the differences of the control points taken one level
            // at a time, each evaluated by de Boor's scheme at the degree that is left.
            var homogeneous = new double[order + 1][];
            var level = new double[(p + 1) * stride];
            var work = new double[(p + 1) * stride];

            for (int j = 0; j <= p; j++)
            {
                Array.Copy(Homogeneous, (s - p + j) * stride, level, j * stride, stride);
            }

            // The control points are measured from the first of them, so that a curve far from the origin keeps its digits in
            // the differences: moving a curve changes no derivative, only the point, which is moved back at the end.
            var origin = new double[Dim];

            for (int c = 0; c < Dim; c++)
            {
                origin[c] = Points[(s - p) * Dim + c];

                for (int j = 0; j <= p; j++)
                {
                    level[j * stride + c] -= Rational ? origin[c] * level[j * stride + Dim] : origin[c];
                }
            }

            for (int k = 0; k <= order; k++)
            {
                homogeneous[k] = new double[stride];

                if (k > p)
                {
                    continue;
                }

                if (k > 0)
                {
                    for (int j = 0; j <= p - k; j++)
                    {
                        double factor = (p - k + 1) / (Knots[s + j + 1] - Knots[s - p + j + k]);

                        for (int c = 0; c < stride; c++)
                        {
                            level[j * stride + c] = factor * (level[(j + 1) * stride + c] - level[j * stride + c]);
                        }
                    }
                }

                int q = p - k;
                Array.Copy(level, work, (q + 1) * stride);

                for (int r = 1; r <= q; r++)
                {
                    for (int j = q; j >= r; j--)
                    {
                        int low = s - p + j + k;
                        double alpha = (t - Knots[low]) / (Knots[s + j + 1 - r] - Knots[low]);
                        double beta = 1.0 - alpha;

                        for (int c = 0; c < stride; c++)
                        {
                            work[j * stride + c] = beta * work[(j - 1) * stride + c] + alpha * work[j * stride + c];
                        }
                    }
                }

                Array.Copy(work, q * stride, homogeneous[k], 0, stride);
            }

            var result = new double[order + 1][];

            if (!Rational)
            {
                for (int k = 0; k <= order; k++)
                {
                    result[k] = homogeneous[k];
                }

                for (int c = 0; c < Dim; c++)
                {
                    result[0][c] += origin[c];
                }

                return result;
            }

            // The quotient rule, C(k) = (A(k) - sum of binomial(k, i) w(i) C(k - i)) / w, taken order by order.
            double w = homogeneous[0][Dim];

            for (int k = 0; k <= order; k++)
            {
                var row = new double[Dim];

                for (int c = 0; c < Dim; c++)
                {
                    double value = homogeneous[k][c];

                    for (int i = 1; i <= k; i++)
                    {
                        value -= Bernstein.Choose(k, i) * homogeneous[i][Dim] * result[k - i][c];
                    }

                    row[c] = value / w;
                }

                result[k] = row;
            }

            for (int c = 0; c < Dim; c++)
            {
                result[0][c] += origin[c];
            }

            return result;
        }

        #endregion
    }
}
