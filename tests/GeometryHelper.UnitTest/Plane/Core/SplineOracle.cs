using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// An exact fraction of two integers, for working a curve out without a single rounding: every double is a fraction whose
    /// denominator is a power of two, so the control points, weights, knots and parameters of a spline are all of them
    /// exact fractions, and the Cox-de Boor recursion over them is exact.
    /// </summary>
    internal readonly struct SplRat
    {
        internal readonly BigInteger Num;
        internal readonly BigInteger Den;

        internal SplRat(BigInteger num, BigInteger den)
        {
            if (den.Sign < 0)
            {
                num = -num;
                den = -den;
            }

            BigInteger gcd = BigInteger.GreatestCommonDivisor(num, den);

            if (!gcd.IsOne && !gcd.IsZero)
            {
                num /= gcd;
                den /= gcd;
            }

            Num = num;
            Den = den;
        }

        internal static readonly SplRat Zero = new SplRat(BigInteger.Zero, BigInteger.One);
        internal static readonly SplRat One = new SplRat(BigInteger.One, BigInteger.One);

        /// <summary>Gets the fraction a double is, exactly.</summary>
        internal static SplRat FromDouble(double value)
        {
            if (value == 0.0)
            {
                return Zero;
            }

            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;

            if (exponent == 0)
            {
                exponent++;
            }
            else
            {
                mantissa |= 1L << 52;
            }

            exponent -= 1075;
            BigInteger num = negative ? -mantissa : mantissa;

            return exponent >= 0
                ? new SplRat(num << exponent, BigInteger.One)
                : new SplRat(num, BigInteger.One << -exponent);
        }

        /// <summary>Gets the number of bits of a positive integer.</summary>
        internal static int BitLength(BigInteger value)
        {
            byte[] bytes = value.ToByteArray();
            int top = bytes.Length - 1;

            while (top > 0 && bytes[top] == 0)
            {
                top--;
            }

            int bits = top * 8;

            for (int b = bytes[top]; b > 0; b >>= 1)
            {
                bits++;
            }

            return bits;
        }

        /// <summary>Gets the double nearest the fraction, ties to even.</summary>
        internal double ToDouble()
        {
            if (Num.IsZero)
            {
                return 0.0;
            }

            bool negative = Num.Sign < 0;
            BigInteger a = BigInteger.Abs(Num);
            BigInteger b = Den;
            int k = 66 - (BitLength(a) - BitLength(b));
            BigInteger q = k >= 0 ? (a << k) / b : a / (b << -k);
            int bits = BitLength(q);
            int drop = bits - 53;
            BigInteger top = q >> drop;
            BigInteger rest = q - (top << drop);
            BigInteger half = BigInteger.One << (drop - 1);

            if (rest > half || (rest == half && !top.IsEven))
            {
                top += 1;
            }

            double value = (double)(ulong)top * Math.Pow(2.0, drop - k);

            return negative ? -value : value;
        }

        public static SplRat operator +(SplRat a, SplRat b) => new SplRat(a.Num * b.Den + b.Num * a.Den, a.Den * b.Den);

        public static SplRat operator -(SplRat a, SplRat b) => new SplRat(a.Num * b.Den - b.Num * a.Den, a.Den * b.Den);

        public static SplRat operator *(SplRat a, SplRat b) => new SplRat(a.Num * b.Num, a.Den * b.Den);

        public static SplRat operator /(SplRat a, SplRat b) => new SplRat(a.Num * b.Den, a.Den * b.Num);

        public static bool operator <(SplRat a, SplRat b) => a.Num * b.Den < b.Num * a.Den;

        public static bool operator >(SplRat a, SplRat b) => a.Num * b.Den > b.Num * a.Den;

        public static bool operator <=(SplRat a, SplRat b) => a.Num * b.Den <= b.Num * a.Den;

        public static bool operator >=(SplRat a, SplRat b) => a.Num * b.Den >= b.Num * a.Den;

        internal bool IsZero => Num.IsZero;
    }

    /// <summary>
    /// A spline as the oracle reads it: plain arrays, and a way to work its points, derivatives, nearest points, length and
    /// box out that shares no code with the library. Points and derivatives are worked out exactly, as fractions, from the
    /// Cox-de Boor recursion that defines the basis; distances, lengths and boxes are searched for on a dense sampling and
    /// polished in <c>decimal</c>, which holds 28 digits.
    /// </summary>
    internal sealed class SplineOracle
    {
        internal readonly int Dim;
        internal readonly int Degree;
        internal readonly double[] Knots;

        /// <summary>The control points as flat coordinates, Dim each.</summary>
        internal readonly double[] Points;

        /// <summary>The weights; all one for a spline that is not rational.</summary>
        internal readonly double[] Weights;

        private readonly SplRat[] _knots;
        private readonly SplRat[] _points;
        private readonly SplRat[] _weights;
        private readonly decimal[] _decimalKnots;
        private readonly decimal[] _decimalPoints;
        private readonly decimal[] _decimalWeights;

        internal SplineOracle(int dim, int degree, double[] knots, double[] points, double[] weights)
        {
            Dim = dim;
            Degree = degree;
            Knots = knots;
            Points = points;
            Weights = weights ?? Enumerable.Repeat(1.0, points.Length / dim).ToArray();

            _knots = knots.Select(SplRat.FromDouble).ToArray();
            _points = points.Select(SplRat.FromDouble).ToArray();
            _weights = Weights.Select(SplRat.FromDouble).ToArray();
            _decimalKnots = knots.Select(ToDecimal).ToArray();
            _decimalPoints = points.Select(ToDecimal).ToArray();
            _decimalWeights = Weights.Select(ToDecimal).ToArray();
        }

        internal int Count => Points.Length / Dim;

        internal double Start => Knots[Degree];

        internal double End => Knots[Count];

        /// <summary>The size of the largest coordinate: what the rounding of the numbers is a share of.</summary>
        internal double Scale => Math.Max(1.0, Points.Max(Math.Abs));

        internal static decimal ToDecimal(double value) => decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>The distinct knot values inside and at the ends of the domain.</summary>
        internal double[] Breaks() => Knots.Skip(Degree).Take(Count - Degree + 1).Distinct().OrderBy(k => k).ToArray();

        #region Exact

        /// <summary>
        /// Gets the derivatives of the basis functions, order by order, at a parameter by the recursion that defines them:
        /// <c>result[k][i]</c> is the k-th derivative of the i-th function of the degree.
        /// </summary>
        private SplRat[][] BasisDerivatives(SplRat t, int order)
        {
            int m = _knots.Length - 1;
            int p = Degree;
            bool atEnd = t >= _knots[m];

            // table[d][k][i]: degree d, derivative k, function i.
            var table = new SplRat[p + 1][][];
            table[0] = new SplRat[1][];
            table[0][0] = new SplRat[m];

            int last = -1;

            for (int i = 0; i < m; i++)
            {
                if (_knots[i] < _knots[i + 1])
                {
                    bool inside = atEnd ? _knots[i + 1] >= t : _knots[i] <= t && t < _knots[i + 1];

                    if (inside)
                    {
                        last = i;
                    }
                }
            }

            for (int i = 0; i < m; i++)
            {
                table[0][0][i] = i == last ? SplRat.One : SplRat.Zero;
            }

            for (int d = 1; d <= p; d++)
            {
                int width = m - d;
                int orders = Math.Min(order, d);
                table[d] = new SplRat[orders + 1][];

                for (int k = 0; k <= orders; k++)
                {
                    table[d][k] = new SplRat[width];

                    for (int i = 0; i < width; i++)
                    {
                        SplRat leftDen = _knots[i + d] - _knots[i];
                        SplRat rightDen = _knots[i + d + 1] - _knots[i + 1];
                        SplRat left = SplRat.Zero;
                        SplRat right = SplRat.Zero;

                        if (!leftDen.IsZero)
                        {
                            left = k == 0
                                ? (t - _knots[i]) / leftDen * table[d - 1][0][i]
                                : new SplRat(d, 1) * table[d - 1][k - 1][i] / leftDen;
                        }

                        if (!rightDen.IsZero)
                        {
                            right = k == 0
                                ? (_knots[i + d + 1] - t) / rightDen * table[d - 1][0][i + 1]
                                : new SplRat(d, 1) * table[d - 1][k - 1][i + 1] / rightDen;
                        }

                        table[d][k][i] = k == 0 ? left + right : left - right;
                    }
                }
            }

            var result = new SplRat[order + 1][];

            for (int k = 0; k <= order; k++)
            {
                result[k] = k < table[p].Length ? table[p][k] : new SplRat[Count].Select(_ => SplRat.Zero).ToArray();
            }

            return result;
        }

        /// <summary>
        /// Gets the point and the derivatives of the spline up to an order at a parameter, as exact fractions, by the
        /// quotient rule over the weighted sums of the basis functions and their derivatives.
        /// </summary>
        internal SplRat[][] ExactDerivatives(double t, int order)
        {
            SplRat[][] basis = BasisDerivatives(SplRat.FromDouble(t), order);
            int n = Count;
            var numerator = new SplRat[order + 1][];
            var weight = new SplRat[order + 1];

            for (int k = 0; k <= order; k++)
            {
                numerator[k] = new SplRat[Dim];
                weight[k] = SplRat.Zero;

                for (int c = 0; c < Dim; c++)
                {
                    numerator[k][c] = SplRat.Zero;
                }

                for (int i = 0; i < n; i++)
                {
                    SplRat nw = basis[k][i] * _weights[i];
                    weight[k] += nw;

                    for (int c = 0; c < Dim; c++)
                    {
                        numerator[k][c] += nw * _points[i * Dim + c];
                    }
                }
            }

            var result = new SplRat[order + 1][];

            for (int k = 0; k <= order; k++)
            {
                result[k] = new SplRat[Dim];

                for (int c = 0; c < Dim; c++)
                {
                    SplRat value = numerator[k][c];
                    SplRat choose = SplRat.One;

                    for (int i = 1; i <= k; i++)
                    {
                        choose = choose * new SplRat(k - i + 1, 1) / new SplRat(i, 1);
                        value -= choose * weight[i] * result[k - i][c];
                    }

                    result[k][c] = value / weight[0];
                }
            }

            return result;
        }

        /// <summary>Gets the point at a parameter, exactly.</summary>
        internal SplRat[] ExactPoint(double t) => ExactDerivatives(t, 0)[0];

        /// <summary>Gets the point at a parameter as the double nearest the exact one.</summary>
        internal double[] Point(double t) => ExactPoint(t).Select(r => r.ToDouble()).ToArray();

        #endregion

        #region Double and decimal

        /// <summary>
        /// Gets the point at a parameter in plain doubles by the Cox-de Boor triangle over all the functions, with no
        /// help from the library: fast, and good to a few units in the last place.
        /// </summary>
        internal double[] FastPoint(double t)
        {
            t = Math.Min(Math.Max(t, Start), End);
            int m = Knots.Length - 1;
            var basis = new double[m];
            int last = -1;

            for (int i = 0; i < m; i++)
            {
                if (Knots[i] < Knots[i + 1] && (t >= End ? Knots[i + 1] >= t : Knots[i] <= t && t < Knots[i + 1]))
                {
                    last = i;
                }
            }

            if (last >= 0)
            {
                basis[last] = 1.0;
            }

            for (int d = 1; d <= Degree; d++)
            {
                for (int i = 0; i < m - d; i++)
                {
                    double left = Knots[i + d] - Knots[i];
                    double right = Knots[i + d + 1] - Knots[i + 1];
                    basis[i] = (left > 0.0 ? (t - Knots[i]) / left * basis[i] : 0.0) + (right > 0.0 ? (Knots[i + d + 1] - t) / right * basis[i + 1] : 0.0);
                }
            }

            var sum = new double[Dim];
            double weight = 0.0;

            for (int i = 0; i < Count; i++)
            {
                double nw = basis[i] * Weights[i];
                weight += nw;

                for (int c = 0; c < Dim; c++)
                {
                    sum[c] += nw * Points[i * Dim + c];
                }
            }

            for (int c = 0; c < Dim; c++)
            {
                sum[c] /= weight;
            }

            return sum;
        }

        /// <summary>
        /// Gets the larger of the speeds on either side of a parameter, the one side at the ends of the domain.
        /// </summary>
        internal double SpeedAt(double t)
        {
            if (t <= Start)
            {
                return FastSpeed(Start);
            }

            if (t >= End)
            {
                return FastSpeed(End);
            }

            return Math.Max(FastSpeed(t), FastSpeed(t, true));
        }

        /// <summary>
        /// Gets the speed at a parameter in plain doubles, from the derivative of the basis functions: the speed of the span
        /// after the parameter, or of the span before it when asked from the left.
        /// </summary>
        internal double FastSpeed(double t, bool fromLeft = false)
        {
            int m = Knots.Length - 1;
            int p = Degree;
            var lower = new double[m];
            int last = -1;

            for (int i = 0; i < m; i++)
            {
                if (Knots[i] < Knots[i + 1] && (fromLeft ? Knots[i] < t && t <= Knots[i + 1] : t >= End ? Knots[i + 1] >= t : Knots[i] <= t && t < Knots[i + 1]))
                {
                    last = i;
                }
            }

            if (last >= 0)
            {
                lower[last] = 1.0;
            }

            // The basis one degree down, then the degree: N, and N' from the two lower ones.
            var basis = (double[])lower.Clone();
            double[] previous = null;

            for (int d = 1; d <= p; d++)
            {
                previous = (double[])basis.Clone();

                for (int i = 0; i < m - d; i++)
                {
                    double left = Knots[i + d] - Knots[i];
                    double right = Knots[i + d + 1] - Knots[i + 1];
                    basis[i] = (left > 0.0 ? (t - Knots[i]) / left * previous[i] : 0.0) + (right > 0.0 ? (Knots[i + d + 1] - t) / right * previous[i + 1] : 0.0);
                }
            }

            var derivative = new double[m];

            for (int i = 0; i < Count; i++)
            {
                double left = Knots[i + p] - Knots[i];
                double right = Knots[i + p + 1] - Knots[i + 1];
                derivative[i] = p * ((left > 0.0 ? previous[i] / left : 0.0) - (right > 0.0 ? previous[i + 1] / right : 0.0));
            }

            var a = new double[Dim];
            var da = new double[Dim];
            double w = 0.0;
            double dw = 0.0;

            for (int i = 0; i < Count; i++)
            {
                w += basis[i] * Weights[i];
                dw += derivative[i] * Weights[i];

                for (int c = 0; c < Dim; c++)
                {
                    a[c] += basis[i] * Weights[i] * (Points[i * Dim + c] - Points[c]);
                    da[c] += derivative[i] * Weights[i] * (Points[i * Dim + c] - Points[c]);
                }
            }

            double square = 0.0;

            for (int c = 0; c < Dim; c++)
            {
                double position = a[c] / w;
                double d = (da[c] - dw * position) / w;
                square += d * d;
            }

            return Math.Sqrt(square);
        }

        /// <summary>Gets the point at a parameter in decimal, by the same triangle, to 28 digits.</summary>
        internal decimal[] DecimalPoint(decimal t)
        {
            int m = _decimalKnots.Length - 1;
            var basis = new decimal[m];
            decimal end = _decimalKnots[Count];
            int last = -1;

            for (int i = 0; i < m; i++)
            {
                if (_decimalKnots[i] < _decimalKnots[i + 1] && (t >= end ? _decimalKnots[i + 1] >= t : _decimalKnots[i] <= t && t < _decimalKnots[i + 1]))
                {
                    last = i;
                }
            }

            if (last >= 0)
            {
                basis[last] = 1m;
            }

            for (int d = 1; d <= Degree; d++)
            {
                for (int i = 0; i < m - d; i++)
                {
                    decimal left = _decimalKnots[i + d] - _decimalKnots[i];
                    decimal right = _decimalKnots[i + d + 1] - _decimalKnots[i + 1];
                    basis[i] = (left > 0m ? (t - _decimalKnots[i]) / left * basis[i] : 0m) + (right > 0m ? (_decimalKnots[i + d + 1] - t) / right * basis[i + 1] : 0m);
                }
            }

            var sum = new decimal[Dim];
            decimal weight = 0m;

            for (int i = 0; i < Count; i++)
            {
                decimal nw = basis[i] * _decimalWeights[i];
                weight += nw;

                for (int c = 0; c < Dim; c++)
                {
                    sum[c] += nw * _decimalPoints[i * Dim + c];
                }
            }

            for (int c = 0; c < Dim; c++)
            {
                sum[c] /= weight;
            }

            return sum;
        }

        private static decimal Sqrt(decimal value)
        {
            if (value <= 0m)
            {
                return 0m;
            }

            decimal x = (decimal)Math.Sqrt((double)value);

            for (int i = 0; i < 6; i++)
            {
                x = (x + value / x) / 2m;
            }

            return x;
        }

        private decimal SquaredDistance(decimal t, decimal[] q)
        {
            decimal[] p = DecimalPoint(t);
            decimal sum = 0m;

            for (int c = 0; c < Dim; c++)
            {
                decimal d = p[c] - q[c];
                sum += d * d;
            }

            return sum;
        }

        #endregion

        #region Searching

        /// <summary>
        /// Gets the least distance from a point to the curve, and a parameter that has it: the curve sampled densely in
        /// doubles, every local minimum of the samples (and both ends) polished by golden-section in decimal, the least taken.
        /// </summary>
        /// <param name="query">The point.</param>
        /// <param name="parameter">A parameter of a nearest point.</param>
        /// <param name="perSpan">How many samples a span of the knots gets.</param>
        internal double Nearest(double[] query, out double parameter, int perSpan = 240)
        {
            double[] breaks = Breaks();
            var ts = new List<double>();

            for (int s = 0; s + 1 < breaks.Length; s++)
            {
                for (int i = 0; i < perSpan; i++)
                {
                    ts.Add(breaks[s] + (breaks[s + 1] - breaks[s]) * i / perSpan);
                }
            }

            ts.Add(breaks[breaks.Length - 1]);

            var d2 = new double[ts.Count];

            for (int i = 0; i < ts.Count; i++)
            {
                double[] p = FastPoint(ts[i]);
                double sum = 0.0;

                for (int c = 0; c < Dim; c++)
                {
                    sum += (p[c] - query[c]) * (p[c] - query[c]);
                }

                d2[i] = sum;
            }

            decimal[] q = query.Select(ToDecimal).ToArray();
            decimal best = decimal.MaxValue;
            double bestT = ts[0];

            for (int i = 0; i < ts.Count; i++)
            {
                bool minimum = (i == 0 || d2[i] < d2[i - 1]) && (i == ts.Count - 1 || d2[i] <= d2[i + 1]);

                if (!minimum)
                {
                    continue;
                }

                decimal lo = ToDecimal(ts[Math.Max(i - 1, 0)]);
                decimal hi = ToDecimal(ts[Math.Min(i + 1, ts.Count - 1)]);
                decimal found = Golden(lo, hi, q, out decimal at);

                if (found < best)
                {
                    best = found;
                    bestT = (double)at;
                }
            }

            parameter = bestT;

            return (double)Sqrt(best);
        }

        private decimal Golden(decimal lo, decimal hi, decimal[] q, out decimal at)
        {
            const decimal Ratio = 0.6180339887498948482045868344m;
            decimal a = lo;
            decimal b = hi;
            decimal c = b - Ratio * (b - a);
            decimal d = a + Ratio * (b - a);
            decimal fc = SquaredDistance(c, q);
            decimal fd = SquaredDistance(d, q);

            for (int i = 0; i < 110; i++)
            {
                if (fc < fd)
                {
                    b = d;
                    d = c;
                    fd = fc;
                    c = b - Ratio * (b - a);
                    fc = SquaredDistance(c, q);
                }
                else
                {
                    a = c;
                    c = d;
                    fc = fd;
                    d = a + Ratio * (b - a);
                    fd = SquaredDistance(d, q);
                }
            }

            // The ends of the bracket are candidates too: a minimum at an end of the curve sits on one.
            decimal best = fc < fd ? fc : fd;
            at = fc < fd ? c : d;
            decimal fa = SquaredDistance(lo, q);
            decimal fb = SquaredDistance(hi, q);

            if (fa < best)
            {
                best = fa;
                at = lo;
            }

            if (fb < best)
            {
                best = fb;
                at = hi;
            }

            return best;
        }

        /// <summary>
        /// Gets the smallest and the largest value of each coordinate: the curve sampled densely in doubles, the best
        /// sample of each extreme polished by golden-section in decimal.
        /// </summary>
        internal void Box(out double[] min, out double[] max, int perSpan = 400)
        {
            double[] breaks = Breaks();
            var ts = new List<double>();

            for (int s = 0; s + 1 < breaks.Length; s++)
            {
                for (int i = 0; i < perSpan; i++)
                {
                    ts.Add(breaks[s] + (breaks[s + 1] - breaks[s]) * i / perSpan);
                }
            }

            ts.Add(breaks[breaks.Length - 1]);
            min = new double[Dim];
            max = new double[Dim];

            double[][] samples = ts.Select(FastPoint).ToArray();

            for (int c = 0; c < Dim; c++)
            {
                int lowAt = 0;
                int highAt = 0;

                for (int i = 1; i < ts.Count; i++)
                {
                    if (samples[i][c] < samples[lowAt][c])
                    {
                        lowAt = i;
                    }

                    if (samples[i][c] > samples[highAt][c])
                    {
                        highAt = i;
                    }
                }

                min[c] = (double)Extreme(ts, lowAt, c, 1);
                max[c] = (double)Extreme(ts, highAt, c, -1);
            }
        }

        private decimal Extreme(List<double> ts, int at, int coordinate, int sign)
        {
            const decimal Ratio = 0.6180339887498948482045868344m;
            decimal lo = ToDecimal(ts[Math.Max(at - 1, 0)]);
            decimal hi = ToDecimal(ts[Math.Min(at + 1, ts.Count - 1)]);
            decimal a = lo;
            decimal b = hi;
            decimal c = b - Ratio * (b - a);
            decimal d = a + Ratio * (b - a);
            decimal fc = sign * DecimalPoint(c)[coordinate];
            decimal fd = sign * DecimalPoint(d)[coordinate];

            for (int i = 0; i < 110; i++)
            {
                if (fc < fd)
                {
                    b = d;
                    d = c;
                    fd = fc;
                    c = b - Ratio * (b - a);
                    fc = sign * DecimalPoint(c)[coordinate];
                }
                else
                {
                    a = c;
                    c = d;
                    fc = fd;
                    d = a + Ratio * (b - a);
                    fd = sign * DecimalPoint(d)[coordinate];
                }
            }

            decimal best = Math.Min(fc, fd);
            best = Math.Min(best, sign * DecimalPoint(lo)[coordinate]);
            best = Math.Min(best, sign * DecimalPoint(hi)[coordinate]);

            return sign * best;
        }

        /// <summary>
        /// Gets the length of the curve: the speed integrated by adaptive Romberg extrapolation of the trapezoid rule, a span
        /// of the knots halved wherever nine rounds of doubling do not settle it to a hundred-billionth of a share.
        /// </summary>
        internal double Length() => Length(Start, End);

        /// <summary>
        /// Gets the length of the part of the curve between two parameters, as <see cref="Length()"/> does.
        /// </summary>
        /// <remarks>
        /// Romberg alone is misled by a span where the speed all but vanishes, a near cusp: the trapezoid sums come down by
        /// a quarter each round, as they do where nothing is wrong, until the step is smaller than the cusp is wide. So an
        /// interval that has not settled is cut and each half is tried again, as far down as it takes.
        /// </remarks>
        internal double Length(double from, double to)
        {
            double[] breaks = new[] { from }.Concat(Breaks().Where(k => k > from && k < to)).Concat(new[] { to }).ToArray();
            double total = 0.0;

            // What a unit of parameter may be wrong by: a hundred-billionth of a share of the control polygon's length
            // over the whole domain, which the length is never above. It lets an interval that is noise stop.
            double polygon = 0.0;

            for (int i = 1; i < Count; i++)
            {
                double square = 0.0;

                for (int c = 0; c < Dim; c++)
                {
                    square += (Points[i * Dim + c] - Points[(i - 1) * Dim + c]) * (Points[i * Dim + c] - Points[(i - 1) * Dim + c]);
                }

                polygon += Math.Sqrt(square);
            }

            double rate = 1E-14 * polygon / (End - Start);

            for (int s = 0; s + 1 < breaks.Length; s++)
            {
                int budget = 3000;
                total += Romberg(breaks[s], breaks[s + 1], 0, rate, ref budget);
            }

            return total;
        }

        private double Romberg(double a, double b, int depth, double rate, ref int budget)
        {
            const int Levels = 10;
            var table = new double[Levels][];
            int panels = 1;
            double sum = 0.5 * (FastSpeed(a) + FastSpeed(b, true));
            double previous = double.NaN;
            int agree = 0;
            double estimate = 0.0;

            for (int level = 0; level < Levels; level++)
            {
                if (level > 0)
                {
                    double h = (b - a) / panels;

                    for (int i = 0; i < panels; i++)
                    {
                        sum += FastSpeed(a + (i + 0.5) * h);
                    }

                    panels *= 2;
                }

                var row = new double[level + 1];
                row[0] = sum * (b - a) / panels;

                for (int k = 1; k <= level; k++)
                {
                    double factor = Math.Pow(4.0, k);
                    row[k] = (factor * row[k - 1] - table[level - 1][k - 1]) / (factor - 1.0);
                }

                table[level] = row;
                estimate = row[level];

                // Three rounds in a row must agree: two can by luck.
                agree = level >= 4 && Math.Abs(estimate - previous) <= Math.Max(2E-15 * Math.Abs(estimate), rate * (b - a)) ? agree + 1 : 0;

                if (agree >= 3)
                {
                    return estimate;
                }

                previous = estimate;
            }

            if (depth >= 40 || --budget <= 0)
            {
                return estimate;
            }

            double middle = 0.5 * (a + b);

            return Romberg(a, middle, depth + 1, rate, ref budget) + Romberg(middle, b, depth + 1, rate, ref budget);
        }

        #endregion
    }
}
