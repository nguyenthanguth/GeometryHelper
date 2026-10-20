using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// A curve cut into the pieces between its distinct knots, each written as a Bézier curve of the curve's degree:
    /// the form every question about a spline's shape is asked in, since a Bézier piece lies inside the hull of its
    /// control points and cuts in halves by plain means.
    /// </summary>
    internal sealed class BezierPieces
    {
        /// <summary>The number of pieces.</summary>
        internal int Count;

        /// <summary>The degree of every piece.</summary>
        internal int Degree;

        /// <summary>The number of coordinates of a point.</summary>
        internal int Dim;

        /// <summary>Whether the pieces are rational.</summary>
        internal bool Rational;

        /// <summary>The parameter each piece starts at.</summary>
        internal double[] From;

        /// <summary>The parameter each piece ends at.</summary>
        internal double[] To;

        /// <summary>The control points of each piece, projected: (Degree + 1) * Dim numbers a piece.</summary>
        internal double[] Points;

        /// <summary>The weights of the control points of each piece: Degree + 1 a piece; null when not rational.</summary>
        internal double[] Weights;

        /// <summary>The smallest coordinates of the control points of each piece, Dim a piece.</summary>
        internal double[] BoxMin;

        /// <summary>The largest coordinates of the control points of each piece, Dim a piece.</summary>
        internal double[] BoxMax;

        /// <summary>
        /// Gets where the control points of a piece start in <see cref="Points"/>.
        /// </summary>
        internal int PointOffset(int piece) => piece * (Degree + 1) * Dim;

        /// <summary>
        /// Gets where the weights of a piece start in <see cref="Weights"/>.
        /// </summary>
        internal int WeightOffset(int piece) => piece * (Degree + 1);
    }

    internal sealed partial class NurbsCurve
    {
        private volatile BezierPieces _pieces;

        /// <summary>
        /// Gets the curve cut into Bézier pieces, worked out once.
        /// </summary>
        internal BezierPieces Pieces
        {
            get
            {
                BezierPieces pieces = _pieces;

                if (pieces == null)
                {
                    pieces = Decompose();
                    _pieces = pieces;
                }

                return pieces;
            }
        }

        /// <summary>
        /// Cuts the curve into Bézier pieces by raising every inner knot to the degree (Piegl and Tiller, algorithm
        /// A5.6), which costs a few means for each piece however long the curve.
        /// </summary>
        private BezierPieces Decompose()
        {
            int p = Degree;
            int stride = Stride;
            int m = Count + p;
            int width = (p + 1) * stride;
            double[] u = Knots;

            var q = new double[(Count - p + 1) * width];
            var from = new double[Count - p + 1];
            var to = new double[Count - p + 1];
            var alphas = new double[p];

            int a = p;
            int b = p + 1;
            int nb = 0;

            Array.Copy(Homogeneous, 0, q, 0, width);

            while (b < m)
            {
                int i = b;

                while (b < m && u[b + 1].Equals(u[b]))
                {
                    b++;
                }

                int mult = b - i + 1;

                if (mult < p)
                {
                    double numerator = u[b] - u[a];

                    for (int j = p; j > mult; j--)
                    {
                        alphas[j - mult - 1] = numerator / (u[a + j] - u[a]);
                    }

                    int r = p - mult;

                    for (int j = 1; j <= r; j++)
                    {
                        int save = r - j;
                        int s = mult + j;

                        for (int k = p; k >= s; k--)
                        {
                            double alpha = alphas[k - s];

                            for (int c = 0; c < stride; c++)
                            {
                                q[nb * width + k * stride + c] = alpha * q[nb * width + k * stride + c] + (1.0 - alpha) * q[nb * width + (k - 1) * stride + c];
                            }
                        }

                        if (b < m)
                        {
                            Array.Copy(q, nb * width + p * stride, q, (nb + 1) * width + save * stride, stride);
                        }
                    }
                }

                from[nb] = u[a];
                to[nb] = u[b];
                nb++;

                if (b < m)
                {
                    for (int k = p - mult; k <= p; k++)
                    {
                        Array.Copy(Homogeneous, (b - p + k) * stride, q, nb * width + k * stride, stride);
                    }

                    a = b;
                    b++;
                }
            }

            // A rational piece whose weights are more than two to one apart is cut in halves until each part is not: where the
            // weights change that fast the speed of the curve does, in a width of a thousandth of the piece or less, and no
            // rule of integration that samples it can be trusted not to step over such a place. Cut, every part is as smooth as a
            // piece with nearly equal weights is, and the control points of the parts are the same curve to rounding.
            if (Rational)
            {
                var balancedPoints = new List<double[]>(nb);
                var balancedFrom = new List<double>(nb);
                var balancedTo = new List<double>(nb);

                for (int piece = 0; piece < nb; piece++)
                {
                    var hw = new double[width];
                    Array.Copy(q, piece * width, hw, 0, width);
                    Balance(hw, from[piece], to[piece], balancedPoints, balancedFrom, balancedTo, 0);
                }

                if (balancedPoints.Count > nb)
                {
                    nb = balancedPoints.Count;
                    q = new double[nb * width];
                    from = balancedFrom.ToArray();
                    to = balancedTo.ToArray();

                    for (int piece = 0; piece < nb; piece++)
                    {
                        Array.Copy(balancedPoints[piece], 0, q, piece * width, width);
                    }
                }
            }

            var pieces = new BezierPieces
            {
                Count = nb,
                Degree = p,
                Dim = Dim,
                Rational = Rational,
                From = new double[nb],
                To = new double[nb],
                Points = new double[nb * (p + 1) * Dim],
                Weights = Rational ? new double[nb * (p + 1)] : null,
                BoxMin = new double[nb * Dim],
                BoxMax = new double[nb * Dim],
            };

            Array.Copy(from, pieces.From, nb);
            Array.Copy(to, pieces.To, nb);

            for (int piece = 0; piece < nb; piece++)
            {
                for (int k = 0; k < Dim; k++)
                {
                    pieces.BoxMin[piece * Dim + k] = double.PositiveInfinity;
                    pieces.BoxMax[piece * Dim + k] = double.NegativeInfinity;
                }

                for (int j = 0; j <= p; j++)
                {
                    int at = piece * width + j * stride;
                    double w = Rational ? q[at + Dim] : 1.0;

                    if (Rational)
                    {
                        pieces.Weights[piece * (p + 1) + j] = w;
                    }

                    for (int k = 0; k < Dim; k++)
                    {
                        double value = Rational ? q[at + k] / w : q[at + k];
                        pieces.Points[(piece * (p + 1) + j) * Dim + k] = value;
                        pieces.BoxMin[piece * Dim + k] = Math.Min(pieces.BoxMin[piece * Dim + k], value);
                        pieces.BoxMax[piece * Dim + k] = Math.Max(pieces.BoxMax[piece * Dim + k], value);
                    }
                }
            }

            return pieces;
        }

        /// <summary>
        /// Cuts a rational piece in halves, recursively, until the weights of each part are no more than two to one apart,
        /// and adds the parts in order.
        /// </summary>
        private void Balance(double[] hw, double from, double to, List<double[]> pieces, List<double> froms, List<double> tos, int depth)
        {
            int p = Degree;
            int stride = Stride;
            double low = double.PositiveInfinity;
            double high = 0.0;

            for (int j = 0; j <= p; j++)
            {
                double w = hw[j * stride + Dim];
                low = Math.Min(low, w);
                high = Math.Max(high, w);
            }

            if (high <= 2.0 * low || depth >= 40)
            {
                pieces.Add(hw);
                froms.Add(from);
                tos.Add(to);
                return;
            }

            SplitHomogeneous(hw, p, stride, out double[] left, out double[] right);
            double middle = 0.5 * (from + to);
            Balance(left, from, middle, pieces, froms, tos, depth + 1);
            Balance(right, middle, to, pieces, froms, tos, depth + 1);
        }

        /// <summary>
        /// Cuts a Bézier piece in halves by de Casteljau's scheme, in homogeneous form for a rational one: the control points
        /// of the same curve over the first half and over the second.
        /// </summary>
        internal static void SplitHomogeneous(double[] hw, int p, int stride, out double[] left, out double[] right)
        {
            int width = (p + 1) * stride;
            left = new double[width];
            right = new double[width];
            var work = new double[width];
            Array.Copy(hw, work, width);

            Array.Copy(work, 0, left, 0, stride);
            Array.Copy(work, p * stride, right, p * stride, stride);

            for (int level = 1; level <= p; level++)
            {
                for (int j = 0; j <= p - level; j++)
                {
                    for (int k = 0; k < stride; k++)
                    {
                        work[j * stride + k] = 0.5 * (work[j * stride + k] + work[(j + 1) * stride + k]);
                    }
                }

                Array.Copy(work, 0, left, level * stride, stride);
                Array.Copy(work, (p - level) * stride, right, (p - level) * stride, stride);
            }
        }

        /// <summary>
        /// Gets the same curve with a knot inserted a number of times, by Boehm's algorithm in homogeneous coordinates
        /// (Piegl and Tiller, algorithm A5.1): the curve does not move, and its control polygon is cut at the corners.
        /// </summary>
        /// <param name="u">The parameter to insert, inside the domain.</param>
        /// <param name="times">How many times, at most as many as keep the knot's multiplicity to the degree.</param>
        /// <param name="knots">The knots of the new curve.</param>
        /// <param name="homogeneous">The control points of the new curve in homogeneous form.</param>
        private void InsertKnot(double u, int times, out double[] knots, out double[] homogeneous)
        {
            int p = Degree;
            int stride = Stride;
            int np = Count - 1;
            int mp = np + p + 1;
            int k = FindSpan(u);
            int multiplicity = 0;

            for (int i = 0; i < Knots.Length; i++)
            {
                if (Knots[i].Equals(u))
                {
                    multiplicity++;
                }
            }

            int r = times;
            knots = new double[Knots.Length + r];
            homogeneous = new double[(Count + r) * stride];

            for (int i = 0; i <= k; i++)
            {
                knots[i] = Knots[i];
            }

            for (int i = 1; i <= r; i++)
            {
                knots[k + i] = u;
            }

            for (int i = k + 1; i <= mp; i++)
            {
                knots[i + r] = Knots[i];
            }

            for (int i = 0; i <= k - p; i++)
            {
                Array.Copy(Homogeneous, i * stride, homogeneous, i * stride, stride);
            }

            for (int i = k - multiplicity; i <= np; i++)
            {
                Array.Copy(Homogeneous, i * stride, homogeneous, (i + r) * stride, stride);
            }

            var work = new double[(p + 1) * stride];

            for (int i = 0; i <= p - multiplicity; i++)
            {
                Array.Copy(Homogeneous, (k - p + i) * stride, work, i * stride, stride);
            }

            int last = k - p;

            for (int j = 1; j <= r; j++)
            {
                last = k - p + j;

                for (int i = 0; i <= p - j - multiplicity; i++)
                {
                    double alpha = (u - Knots[last + i]) / (Knots[i + k + 1] - Knots[last + i]);

                    for (int c = 0; c < stride; c++)
                    {
                        work[i * stride + c] = alpha * work[(i + 1) * stride + c] + (1.0 - alpha) * work[i * stride + c];
                    }
                }

                Array.Copy(work, 0, homogeneous, last * stride, stride);
                Array.Copy(work, (p - j - multiplicity) * stride, homogeneous, (k + r - j - multiplicity) * stride, stride);
            }

            for (int i = last + 1; i < k - multiplicity; i++)
            {
                Array.Copy(work, (i - last) * stride, homogeneous, i * stride, stride);
            }
        }

        /// <summary>
        /// Gets how many times a parameter is a knot of the curve.
        /// </summary>
        internal int Multiplicity(double t)
        {
            int count = 0;

            for (int i = 0; i < Knots.Length; i++)
            {
                if (Knots[i].Equals(t))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Cuts the curve in two at a parameter inside its domain, by raising the knot there to the degree, which cuts
        /// the control polygon where the curve passes through a control point of its own: the two curves are exactly the
        /// two parts, and meet at that point to the last bit.
        /// </summary>
        /// <param name="t">The parameter, above the first knot and below the last.</param>
        /// <param name="first">The part up to the parameter, over [start, t].</param>
        /// <param name="second">The part from the parameter on, over [t, end].</param>
        internal void Split(double t, out NurbsCurve first, out NurbsCurve second)
        {
            int p = Degree;
            int stride = Stride;
            int existing = Multiplicity(t);
            int times = p - existing;

            double[] knots;
            double[] homogeneous;
            int count;

            if (times > 0)
            {
                InsertKnot(t, times, out knots, out homogeneous);
                count = Count + times;
            }
            else
            {
                knots = Knots;
                homogeneous = Homogeneous;
                count = Count;
            }

            // The first of the knots at t; the point where the curve breaks is the control point before it.
            int f = 0;

            while (!knots[f].Equals(t))
            {
                f++;
            }

            first = Piece(0, f - 1, knots, homogeneous, 0, t, true);
            second = Piece(f - 1, count - 1, knots, homogeneous, f, t, false);
        }

        /// <summary>
        /// Builds one of the two parts of a cut curve from a range of its control points and its knots, the knot at the
        /// cut repeated one time more than the curve has it so that the part starts or ends there.
        /// </summary>
        private NurbsCurve Piece(int fromPoint, int toPoint, double[] knots, double[] homogeneous, int fromKnot, double t, bool endsAtT)
        {
            int pointCount = toPoint - fromPoint + 1;
            int stride = Stride;
            var points = new double[pointCount * Dim];
            double[] weights = Rational ? new double[pointCount] : null;

            for (int i = 0; i < pointCount; i++)
            {
                int at = (fromPoint + i) * stride;

                if (Rational)
                {
                    double w = homogeneous[at + Dim];
                    weights[i] = w;

                    for (int k = 0; k < Dim; k++)
                    {
                        points[i * Dim + k] = homogeneous[at + k] / w;
                    }
                }
                else
                {
                    for (int k = 0; k < Dim; k++)
                    {
                        points[i * Dim + k] = homogeneous[at + k];
                    }
                }
            }

            int knotCount = pointCount + Degree + 1;
            var cut = new double[knotCount];

            if (endsAtT)
            {
                Array.Copy(knots, fromKnot, cut, 0, knotCount - 1);
                cut[knotCount - 1] = t;
            }
            else
            {
                cut[0] = t;
                Array.Copy(knots, fromKnot, cut, 1, knotCount - 1);
            }

            return new NurbsCurve(Dim, Degree, points, cut, weights);
        }

        /// <summary>
        /// Gets the same curve run the other way: the control points in the opposite order and the knots mirrored about
        /// the middle of the domain, so that it covers the same range of parameters.
        /// </summary>
        internal NurbsCurve Reversed()
        {
            var points = new double[Points.Length];
            double[] weights = Rational ? new double[Count] : null;

            for (int i = 0; i < Count; i++)
            {
                Array.Copy(Points, (Count - 1 - i) * Dim, points, i * Dim, Dim);

                if (Rational)
                {
                    weights[i] = Weights[Count - 1 - i];
                }
            }

            double[] knots = MirroredKnots();

            Check(Degree, points, Dim, knots, weights);

            return new NurbsCurve(Dim, Degree, points, knots, weights);
        }

        /// <summary>
        /// Gets the knots mirrored about the middle of the domain, a + b - k, each distinct inner knot once, so that knots that
        /// were equal stay equal and knots that were apart stay apart: where rounding would put two on one number the later is
        /// moved one unit in the last place on, and one that would reach the end of the domain is moved one back, in order.
        /// </summary>
        private double[] MirroredKnots()
        {
            int total = Knots.Length;
            int p = Degree;
            double sum = Start + End;
            var knots = new double[total];

            // The distinct inner knots from the last to the first, which is ascending once mirrored, with how often each is repeated.
            var values = new List<double>();
            var runs = new List<int>();
            int i = total - p - 2;

            while (i >= p + 1)
            {
                int j = i;

                while (j - 1 >= p + 1 && Knots[j - 1].Equals(Knots[i]))
                {
                    j--;
                }

                values.Add(sum - Knots[i]);
                runs.Add(i - j + 1);
                i = j - 1;
            }

            double previous = Start;

            for (int g = 0; g < values.Count; g++)
            {
                if (!(values[g] > previous))
                {
                    values[g] = NextUp(previous);
                }

                previous = values[g];
            }

            double ceiling = End;

            for (int g = values.Count - 1; g >= 0; g--)
            {
                if (!(values[g] < ceiling))
                {
                    values[g] = NextDown(ceiling);
                }

                ceiling = values[g];
            }

            for (int k = 0; k <= p; k++)
            {
                knots[k] = Start;
                knots[total - 1 - k] = End;
            }

            int at = p + 1;

            for (int g = 0; g < values.Count; g++)
            {
                for (int r = 0; r < runs[g]; r++)
                {
                    knots[at++] = values[g];
                }
            }

            return knots;
        }

        private static double NextUp(double value)
        {
            if (value == 0.0)
            {
                return double.Epsilon;
            }

            long bits = BitConverter.DoubleToInt64Bits(value);

            return BitConverter.Int64BitsToDouble(value > 0.0 ? bits + 1 : bits - 1);
        }

        private static double NextDown(double value) => -NextUp(-value);
    }
}
