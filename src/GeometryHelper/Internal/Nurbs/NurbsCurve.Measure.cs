using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    internal sealed partial class NurbsCurve
    {
        /// <summary>
        /// How close the length of a piece is worked out to, as a share of the length of its control polygon: the whole
        /// curve is then good to much better than the 1E-12 it is promised to.
        /// </summary>
        private const double LengthAccuracy = 1E-14;

        /// <summary>
        /// The most times an interval of a piece is cut in halves where the speed will not settle, which is where it
        /// falls to nought and the integrand has a corner.
        /// </summary>
        private const int MostLengthDepth = 50;

        private volatile double[] _cumulative;

        /// <summary>
        /// Gets the length of the curve: the integral of its speed, by Gauss-Legendre of ten points over each Bézier piece,
        /// the pieces cut in halves wherever the rule over a whole and over its two halves disagree.
        /// </summary>
        internal double Length
        {
            get
            {
                double[] cumulative = Cumulative();
                return cumulative[cumulative.Length - 1];
            }
        }

        private double[] Cumulative()
        {
            double[] cumulative = _cumulative;

            if (cumulative == null)
            {
                BezierPieces pieces = Pieces;
                cumulative = new double[pieces.Count];
                double sum = 0.0;
                var work = new double[(Degree + 1) * (Dim + 1)];

                for (int i = 0; i < pieces.Count; i++)
                {
                    sum += PieceLength(pieces, i, 1.0, work);
                    cumulative[i] = sum;
                }

                _cumulative = cumulative;
            }

            return cumulative;
        }

        /// <summary>
        /// Gets the length of a piece from its start to a share of its parameter.
        /// </summary>
        private double PieceLength(BezierPieces pieces, int piece, double upTo, double[] work)
        {
            if (!(upTo > 0.0))
            {
                return 0.0;
            }

            double accuracy = LengthAccuracy * PolygonLength(pieces, piece);
            double[] breaks = SpeedBreaks(pieces, piece);
            double total = 0.0;
            double from = 0.0;

            // The piece is integrated between the places where its speed is least or greatest, so that a place where the speed
            // all but vanishes, which makes a corner in the integrand, is at the end of an interval and not in the middle of one.
            for (int i = 0; i <= breaks.Length && from < upTo; i++)
            {
                double to = i < breaks.Length ? Math.Min(breaks[i], upTo) : upTo;

                if (!(to > from))
                {
                    continue;
                }

                double whole = Gauss(pieces, piece, from, to, work);

                // However the speed behaves, an interval is cut into no more than this many: where the speed is only as good as
                // rounding the rule over a whole and over its halves never agree to the accuracy asked, and cutting would go on
                // for ever.
                int budget = 4000;
                total += Refine(pieces, piece, from, to, whole, accuracy, 0, ref budget, work);
                from = to;
            }

            return total;
        }

        private volatile double[][] _speedBreaks;

        /// <summary>
        /// Gets the parameters inside a piece, as shares of it, where its speed is least or greatest: the roots of the derivative
        /// of the square of the speed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A rule of integration that samples the speed finds its way over a smooth function and is misled by a corner
        /// close to the end of an interval, where neither the rule over the whole nor the rule over the halves has a point
        /// beyond the corner to see it: a curve that runs along a line and turns back 5E-4 before the end of a piece has
        /// a speed that all but vanishes there, and was measured 5E-7 short. A piece is cut at the places its speed is
        /// least, which is where such a corner is, so that each part has a speed that is smooth to its ends.
        /// </para>
        /// <para>
        /// Most pieces have none, and that is known without finding a root: if the derivative control points of a piece
        /// (the numerator of its derivative, for a rational one) all lie within 60 degrees of one direction, the speed
        /// cannot fall below half of what that direction carries, and the piece is smooth. Only a piece whose derivative turns
        /// back, which is a loop, a cusp or a curve doubling on itself, has its roots found.
        /// </para>
        /// </remarks>
        private double[] SpeedBreaks(BezierPieces pieces, int piece)
        {
            double[][] all = _speedBreaks;

            if (all == null)
            {
                all = new double[pieces.Count][];
                _speedBreaks = all;
            }

            double[] breaks = all[piece];

            if (breaks == null)
            {
                breaks = FindSpeedBreaks(pieces, piece);
                all[piece] = breaks;
            }

            return breaks;
        }

        private static readonly double[] NoBreaks = new double[0];

        private static double[] FindSpeedBreaks(BezierPieces pieces, int piece)
        {
            int p = pieces.Degree;
            int dim = pieces.Dim;
            int at = pieces.PointOffset(piece);
            double[][] numerators;
            double[] weights;
            double[][] v;

            if (pieces.Rational)
            {
                var origin = new double[dim];

                for (int k = 0; k < dim; k++)
                {
                    origin[k] = pieces.Points[at + k];
                }

                Numerators(pieces, piece, origin, out numerators, out weights);
                v = new double[dim][];

                for (int k = 0; k < dim; k++)
                {
                    v[k] = CoordinateSlope(numerators[k], weights, out _);
                }
            }
            else
            {
                weights = null;
                v = new double[dim][];

                for (int k = 0; k < dim; k++)
                {
                    v[k] = new double[p];

                    for (int j = 0; j < p; j++)
                    {
                        v[k][j] = p * (pieces.Points[at + (j + 1) * dim + k] - pieces.Points[at + j * dim + k]);
                    }
                }
            }

            if (HodographLeansOneWay(v, dim))
            {
                return NoBreaks;
            }

            double[] square = null;

            for (int k = 0; k < dim; k++)
            {
                double[] term = Bernstein.Multiply(v[k], v[k]);

                if (square == null)
                {
                    square = term;
                }
                else
                {
                    for (int j = 0; j < square.Length; j++)
                    {
                        square[j] += term[j];
                    }
                }
            }

            double[] slope = Bernstein.Derivative(square);
            double scale = Bernstein.MaxAbs(square) * (square.Length - 1);

            if (weights != null)
            {
                // The square of the speed is |V|^2 over W^4, whose derivative has the sign of |V|^2' W - 4 |V|^2 W'.
                double[] weightSlope = Bernstein.Derivative(weights);
                double[] first = Bernstein.Multiply(slope, weights);
                double[] second = Bernstein.Multiply(square, weightSlope);

                for (int j = 0; j < first.Length; j++)
                {
                    first[j] -= 4.0 * second[j];
                }

                scale = Math.Max(Bernstein.MaxAbs(second) * 4.0, Bernstein.MaxAbs(first));
                slope = first;
            }

            var roots = new List<double>();
            Bernstein.FindRoots(slope, NoiseShare * scale, roots);
            roots.Sort();

            var breaks = new List<double>();

            foreach (double root in roots)
            {
                if (root > 0.0 && root < 1.0 && (breaks.Count == 0 || root > breaks[breaks.Count - 1]))
                {
                    breaks.Add(root);
                }
            }

            return breaks.ToArray();
        }

        /// <summary>
        /// Says whether a vector polynomial in Bernstein form has every coefficient within 60 degrees of the direction of their
        /// sum, so that it cannot vanish and its length stays above half of the mean of theirs.
        /// </summary>
        private static bool HodographLeansOneWay(double[][] v, int dim)
        {
            int count = v[0].Length;
            var sum = new double[dim];

            for (int j = 0; j < count; j++)
            {
                for (int k = 0; k < dim; k++)
                {
                    sum[k] += v[k][j];
                }
            }

            double length = 0.0;

            for (int k = 0; k < dim; k++)
            {
                length += sum[k] * sum[k];
            }

            length = Math.Sqrt(length);

            if (!(length > 0.0))
            {
                return false;
            }

            for (int j = 0; j < count; j++)
            {
                double along = 0.0;
                double size = 0.0;

                for (int k = 0; k < dim; k++)
                {
                    along += sum[k] / length * v[k][j];
                    size += v[k][j] * v[k][j];
                }

                if (along < 0.5 * Math.Sqrt(size))
                {
                    return false;
                }
            }

            return true;
        }

        private double Refine(BezierPieces pieces, int piece, double a, double b, double whole, double accuracy, int depth, ref int budget, double[] work)
        {
            double middle = 0.5 * (a + b);
            double left = Gauss(pieces, piece, a, middle, work);
            double right = Gauss(pieces, piece, middle, b, work);

            if (Math.Abs(whole - (left + right)) <= accuracy * (b - a) || depth >= MostLengthDepth || --budget <= 0)
            {
                return left + right;
            }

            return Refine(pieces, piece, a, middle, left, accuracy, depth + 1, ref budget, work)
                + Refine(pieces, piece, middle, b, right, accuracy, depth + 1, ref budget, work);
        }

        private double Gauss(BezierPieces pieces, int piece, double a, double b, double[] work)
        {
            double half = 0.5 * (b - a);
            double centre = 0.5 * (b + a);
            double sum = 0.0;

            for (int i = 0; i < GaussLegendre.Points; i++)
            {
                sum += GaussLegendre.Weights[i] * Speed(pieces, piece, centre + half * GaussLegendre.Nodes[i], work);
            }

            return sum * half;
        }

        private static double PolygonLength(BezierPieces pieces, int piece)
        {
            int p = pieces.Degree;
            int dim = pieces.Dim;
            int at = pieces.PointOffset(piece);
            double sum = 0.0;

            for (int j = 0; j < p; j++)
            {
                double square = 0.0;

                for (int k = 0; k < dim; k++)
                {
                    double d = pieces.Points[at + (j + 1) * dim + k] - pieces.Points[at + j * dim + k];
                    square += d * d;
                }

                sum += Math.Sqrt(square);
            }

            return sum;
        }

        /// <summary>
        /// Gets the speed of a piece at a share of its parameter: the length of its derivative with respect to that share.
        /// </summary>
        /// <param name="pieces">The pieces.</param>
        /// <param name="piece">Which piece.</param>
        /// <param name="s">The share of the piece's parameter, from 0 to 1.</param>
        /// <param name="work">Room for (Degree + 1) * (Dim + 1) numbers.</param>
        private static double Speed(BezierPieces pieces, int piece, double s, double[] work)
        {
            int p = pieces.Degree;
            int dim = pieces.Dim;
            int stride = pieces.Rational ? dim + 1 : dim;
            int at = pieces.PointOffset(piece);
            int weightAt = pieces.WeightOffset(piece);

            // The control points are measured from the first of them: the derivative does not change by moving the curve, and a
            // curve a hundred units from the origin that moves a tenth would otherwise lose its digits in the difference.
            for (int j = 0; j <= p; j++)
            {
                if (pieces.Rational)
                {
                    double w = pieces.Weights[weightAt + j];

                    for (int k = 0; k < dim; k++)
                    {
                        work[j * stride + k] = w * (pieces.Points[at + j * dim + k] - pieces.Points[at + k]);
                    }

                    work[j * stride + dim] = w;
                }
                else
                {
                    for (int k = 0; k < dim; k++)
                    {
                        work[j * stride + k] = pieces.Points[at + j * dim + k] - pieces.Points[at + k];
                    }
                }
            }

            double u = 1.0 - s;

            for (int level = 1; level < p; level++)
            {
                for (int j = 0; j <= p - level; j++)
                {
                    for (int k = 0; k < stride; k++)
                    {
                        work[j * stride + k] = u * work[j * stride + k] + s * work[(j + 1) * stride + k];
                    }
                }
            }

            double square = 0.0;

            if (!pieces.Rational)
            {
                for (int k = 0; k < dim; k++)
                {
                    double d = p * (work[stride + k] - work[k]);
                    square += d * d;
                }

                return Math.Sqrt(square);
            }

            double weight = u * work[dim] + s * work[stride + dim];
            double weightRate = p * (work[stride + dim] - work[dim]);

            for (int k = 0; k < dim; k++)
            {
                double position = (u * work[k] + s * work[stride + k]) / weight;
                double rate = p * (work[stride + k] - work[k]);
                double d = (rate - weightRate * position) / weight;
                square += d * d;
            }

            return Math.Sqrt(square);
        }

        /// <summary>
        /// Gets the length of the curve from its start to a knot value.
        /// </summary>
        internal double LengthTo(double knot)
        {
            double[] cumulative = Cumulative();
            BezierPieces pieces = Pieces;
            knot = Clamp(knot);

            int low = 0;
            int high = pieces.Count - 1;

            while (low < high)
            {
                int middle = (low + high + 1) >> 1;

                if (pieces.From[middle] <= knot)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            double width = pieces.To[low] - pieces.From[low];
            double share = width > 0.0 ? Math.Min(Math.Max((knot - pieces.From[low]) / width, 0.0), 1.0) : 0.0;
            var work = new double[(Degree + 1) * (Dim + 1)];

            return (low == 0 ? 0.0 : cumulative[low - 1]) + PieceLength(pieces, low, share, work);
        }

        /// <summary>
        /// Gets the parameter at which the curve has run a length from its start: the end of the domain when the length
        /// is the whole curve or more, the start when it is nought or less.
        /// </summary>
        internal double ParameterAtLength(double length)
        {
            double[] cumulative = Cumulative();
            double total = cumulative[cumulative.Length - 1];

            if (!(length > 0.0))
            {
                return Start;
            }

            if (length >= total)
            {
                return End;
            }

            int low = 0;
            int high = cumulative.Length - 1;

            while (low < high)
            {
                int middle = (low + high) >> 1;

                if (cumulative[middle] < length)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            BezierPieces pieces = Pieces;
            int piece = low;
            double before = piece == 0 ? 0.0 : cumulative[piece - 1];
            double pieceLength = cumulative[piece] - before;
            double wanted = length - before;
            var work = new double[(Degree + 1) * (Dim + 1)];

            double lower = 0.0;
            double upper = 1.0;
            double s = pieceLength > 0.0 ? wanted / pieceLength : 0.5;

            for (int step = 0; step < 100; step++)
            {
                double miss = PieceLength(pieces, piece, s, work) - wanted;

                // Near enough: the point is kept as it is, not moved by a step that rounding can no longer place.
                if (Math.Abs(miss) <= 4E-14 * pieceLength)
                {
                    break;
                }

                if (miss > 0.0)
                {
                    upper = s;
                }
                else
                {
                    lower = s;
                }

                double speed = Speed(pieces, piece, s, work);
                double next = speed > 0.0 ? s - miss / speed : double.NaN;

                if (!(next > lower && next < upper))
                {
                    next = 0.5 * (lower + upper);
                }

                s = next;

                if (!(upper - lower > 1E-16))
                {
                    break;
                }
            }

            double t = pieces.From[piece] + s * (pieces.To[piece] - pieces.From[piece]);

            return Math.Min(Math.Max(t, pieces.From[piece]), pieces.To[piece]);
        }
    }
}
