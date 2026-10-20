using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// Polynomials in Bernstein form on [0, 1]: the coefficients of a polynomial of degree n, as an array of n + 1
    /// numbers, are the values its control polygon has, and the polynomial stays inside the hull of them.
    /// <para>
    /// Everything a spline needs from a polynomial is done here without leaving that form: the product of two, the
    /// derivative, one cut in halves, one evaluated, and every root in [0, 1] found. The roots are found by the
    /// variation-diminishing property: a polynomial whose coefficients never change sign has no root, one whose
    /// coefficients change sign once has exactly one, and anything else is cut in halves. That is why none is missed,
    /// however close two lie, as a search that steps along the interval and looks for a change of sign can.
    /// </para>
    /// </summary>
    internal static class Bernstein
    {
        /// <summary>
        /// The deepest a root search cuts one interval in halves, and the width at which what is left of it is taken as
        /// the root.
        /// </summary>
        private const int MostDepth = 52;

        /// <summary>
        /// The most intervals one search looks at. It is never reached by an honest polynomial; it stops a
        /// polynomial that is rounding noise from cutting itself to pieces.
        /// </summary>
        private const int MostIntervals = 4096;

        private static readonly double[][] Pascal = BuildPascal(64);

        [ThreadStatic]
        private static double[] _work;

        private static double[][] BuildPascal(int size)
        {
            var rows = new double[size + 1][];

            for (int n = 0; n <= size; n++)
            {
                rows[n] = new double[n + 1];
                rows[n][0] = 1.0;
                rows[n][n] = 1.0;

                for (int k = 1; k < n; k++)
                {
                    rows[n][k] = rows[n - 1][k - 1] + rows[n - 1][k];
                }
            }

            return rows;
        }

        private static double[] Work(int length)
        {
            double[] work = _work;

            if (work == null || work.Length < length)
            {
                work = new double[Math.Max(length, 64)];
                _work = work;
            }

            return work;
        }

        /// <summary>
        /// Gets the binomial coefficient n over k, which is exact as a double up to n = 56 and good to a unit in the
        /// last place beyond.
        /// </summary>
        internal static double Choose(int n, int k) => Pascal[n][k];

        /// <summary>
        /// Gets the product of two polynomials: the degrees add, and each coefficient is the weighted mean of the
        /// products of the pairs of coefficients that make its index.
        /// </summary>
        internal static double[] Multiply(double[] a, double[] b)
        {
            int m = a.Length - 1;
            int n = b.Length - 1;
            var c = new double[m + n + 1];

            for (int k = 0; k <= m + n; k++)
            {
                int low = Math.Max(0, k - n);
                int high = Math.Min(m, k);
                double sum = 0.0;

                for (int i = low; i <= high; i++)
                {
                    sum += Pascal[m][i] * Pascal[n][k - i] * a[i] * b[k - i];
                }

                c[k] = sum / Pascal[m + n][k];
            }

            return c;
        }

        /// <summary>
        /// Gets the derivative of a polynomial, one degree lower: n times the differences of its coefficients.
        /// </summary>
        internal static double[] Derivative(double[] c)
        {
            int n = c.Length - 1;
            var d = new double[Math.Max(n, 1)];

            for (int i = 0; i < n; i++)
            {
                d[i] = n * (c[i + 1] - c[i]);
            }

            return d;
        }

        /// <summary>
        /// Gets the difference of two polynomials of the same degree.
        /// </summary>
        internal static double[] Subtract(double[] a, double[] b)
        {
            var c = new double[a.Length];

            for (int i = 0; i < c.Length; i++)
            {
                c[i] = a[i] - b[i];
            }

            return c;
        }

        /// <summary>
        /// Gets the largest coefficient in size.
        /// </summary>
        internal static double MaxAbs(double[] c)
        {
            double largest = 0.0;

            for (int i = 0; i < c.Length; i++)
            {
                largest = Math.Max(largest, Math.Abs(c[i]));
            }

            return largest;
        }

        /// <summary>
        /// Evaluates a polynomial by de Casteljau's cuts, which only ever take means of the coefficients and so keep
        /// the rounding of each result to a few units in the last place of the largest coefficient.
        /// </summary>
        internal static double Evaluate(double[] c, double s) => Evaluate(c, s, out _);

        /// <summary>
        /// Evaluates a polynomial and its slope together.
        /// </summary>
        internal static double Evaluate(double[] c, double s, out double slope)
        {
            int n = c.Length - 1;

            if (n == 0)
            {
                slope = 0.0;
                return c[0];
            }

            double[] work = Work(n + 1);
            Array.Copy(c, work, n + 1);
            double u = 1.0 - s;

            for (int level = 1; level < n; level++)
            {
                for (int i = 0; i <= n - level; i++)
                {
                    work[i] = u * work[i] + s * work[i + 1];
                }
            }

            slope = n * (work[1] - work[0]);
            return u * work[0] + s * work[1];
        }

        /// <summary>
        /// Cuts a polynomial in halves: the same polynomial over [0, 1/2] and over [1/2, 1], each rescaled to [0, 1].
        /// </summary>
        internal static void Split(double[] c, out double[] left, out double[] right)
        {
            int n = c.Length - 1;
            left = new double[n + 1];
            right = new double[n + 1];
            double[] work = Work(n + 1);
            Array.Copy(c, work, n + 1);

            left[0] = work[0];
            right[n] = work[n];

            for (int level = 1; level <= n; level++)
            {
                for (int i = 0; i <= n - level; i++)
                {
                    work[i] = 0.5 * (work[i] + work[i + 1]);
                }

                left[level] = work[0];
                right[n - level] = work[n - level];
            }
        }

        /// <summary>
        /// Finds every root of a polynomial in [0, 1] and adds each to a list, in no particular order and some twice.
        /// </summary>
        /// <param name="c">The coefficients.</param>
        /// <param name="negligible">
        /// How large a coefficient has to be to count: a polynomial all of whose coefficients are no larger than this
        /// is rounding noise round zero, and its middle is given as the one root where it stands, since all of it is
        /// as good. The caller knows the size of the numbers the coefficients were worked out from.
        /// </param>
        /// <param name="roots">The list to add to.</param>
        /// <remarks>
        /// <para>
        /// Only a root where the polynomial changes sign is looked for, which is the kind that matters here: a
        /// distance or a coordinate is smallest or largest where its derivative crosses nought. A root of even
        /// multiplicity, where the polynomial touches nought and turns back, is where the derivative of a distance
        /// has a flat spot and no extreme, so it is not given. A pair of roots closer together than a double can
        /// tell apart is given as one, at the middle of the interval round it, which is as good to rounding as either
        /// of them, since all that is asked of a root is the value it gives.
        /// </para>
        /// <para>
        /// A coefficient of exactly nought at an end of an interval is a root at that end, and is given; the signs
        /// are then counted without it, so that the rest of the interval is searched as it was.
        /// </para>
        /// </remarks>
        internal static void FindRoots(double[] c, double negligible, List<double> roots)
        {
            var stack = new Stack<Interval>();
            stack.Push(new Interval(0.0, 1.0, (double[])c.Clone(), 0));
            int looked = 0;

            while (stack.Count > 0)
            {
                Interval interval = stack.Pop();
                double[] k = interval.Coefficients;
                int n = k.Length - 1;
                looked++;

                if (looked > MostIntervals)
                {
                    roots.Add(0.5 * (interval.Low + interval.High));
                    continue;
                }

                double largest = MaxAbs(k);

                if (double.IsNaN(largest))
                {
                    continue;
                }

                if (largest <= negligible)
                {
                    roots.Add(0.5 * (interval.Low + interval.High));
                    continue;
                }

                if (k[0] == 0.0)
                {
                    roots.Add(interval.Low);
                }

                if (k[n] == 0.0)
                {
                    roots.Add(interval.High);
                }

                int changes = 0;
                int signLeft = 0;
                int signRight = 0;

                for (int i = 0; i <= n; i++)
                {
                    int sign = k[i] > 0.0 ? 1 : k[i] < 0.0 ? -1 : 0;

                    if (sign == 0)
                    {
                        continue;
                    }

                    if (signRight != 0 && sign != signRight)
                    {
                        changes++;
                    }

                    if (signLeft == 0)
                    {
                        signLeft = sign;
                    }

                    signRight = sign;
                }

                if (changes == 0)
                {
                    continue;
                }

                if (changes == 1)
                {
                    double local = SingleRoot(k, signLeft);
                    roots.Add(interval.Low + local * (interval.High - interval.Low));
                    continue;
                }

                if (interval.Depth >= MostDepth)
                {
                    roots.Add(0.5 * (interval.Low + interval.High));
                    continue;
                }

                Split(k, out double[] left, out double[] right);
                double middle = 0.5 * (interval.Low + interval.High);
                stack.Push(new Interval(middle, interval.High, right, interval.Depth + 1));
                stack.Push(new Interval(interval.Low, middle, left, interval.Depth + 1));
            }
        }

        /// <summary>
        /// Finds the one root inside an interval whose coefficients change sign once, by Newton's method held inside
        /// the bracket that the sign gives, which bisects whenever a step would leave it.
        /// </summary>
        /// <param name="c">The coefficients, over [0, 1].</param>
        /// <param name="signLeft">The sign of the polynomial just after nought: that of the first coefficient that is not nought.</param>
        /// <returns>The root, in (0, 1) unless rounding put it on an end.</returns>
        private static double SingleRoot(double[] c, int signLeft)
        {
            double low = 0.0;
            double high = 1.0;
            double s = 0.5;

            for (int step = 0; step < 200; step++)
            {
                double value = Evaluate(c, s, out double slope);

                if (value == 0.0)
                {
                    return s;
                }

                if ((value > 0.0) == (signLeft > 0))
                {
                    low = s;
                }
                else
                {
                    high = s;
                }

                double next = s - value / slope;

                if (!(next > low && next < high))
                {
                    next = 0.5 * (low + high);
                }

                if (Math.Abs(next - s) <= 1E-16 * Math.Max(1.0, Math.Abs(next)) || !(high - low > 2E-16))
                {
                    return next;
                }

                s = next;
            }

            return s;
        }

        private struct Interval
        {
            internal readonly double Low;
            internal readonly double High;
            internal readonly double[] Coefficients;
            internal readonly int Depth;

            internal Interval(double low, double high, double[] coefficients, int depth)
            {
                Low = low;
                High = high;
                Coefficients = coefficients;
                Depth = depth;
            }
        }
    }
}
