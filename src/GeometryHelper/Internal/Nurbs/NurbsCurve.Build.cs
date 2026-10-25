using System;

namespace GeometryHelper.Internal.Nurbs
{
    internal sealed partial class NurbsCurve
    {
        /// <summary>
        /// Gets the knots of the clamped curve of a degree through a number of control points with the same span between
        /// each pair of different ones, over [0, 1].
        /// </summary>
        internal static double[] UniformKnots(int count, int degree)
        {
            var knots = new double[count + degree + 1];
            int spans = count - degree;

            for (int i = 0; i <= degree; i++)
            {
                knots[i] = 0.0;
                knots[count + i] = 1.0;
            }

            for (int j = 1; j < spans; j++)
            {
                knots[degree + j] = (double)j / spans;
            }

            return knots;
        }

        /// <summary>
        /// Makes the curve of degree one along a chain of points, over [0, 1] with each point at the share of the chain's
        /// length that comes before it.
        /// </summary>
        /// <param name="dim">The number of coordinates of a point.</param>
        /// <param name="points">The vertices as flat coordinates, at least two and no two neighbours the same.</param>
        internal static NurbsCurve FromChain(int dim, double[] points)
        {
            int count = points.Length / dim;
            var knots = new double[count + 2];
            double[] shares = ChordShares(dim, points, out double total);

            knots[0] = 0.0;

            for (int i = 0; i < count; i++)
            {
                knots[i + 1] = shares[i];
            }

            knots[count + 1] = 1.0;

            if (!(total > 0.0))
            {
                throw new ArgumentException("The vertices are all one point, which no curve runs along.", "vertices");
            }

            Check(1, points, dim, knots, null);
            return new NurbsCurve(dim, 1, points, knots, null);
        }

        /// <summary>
        /// Gets the share of the length of a chain of points that comes before each of them, from 0 for the first to
        /// exactly 1 for the last.
        /// </summary>
        private static double[] ChordShares(int dim, double[] points, out double total)
        {
            int count = points.Length / dim;
            var cumulative = new double[count];
            total = 0.0;

            for (int i = 1; i < count; i++)
            {
                double square = 0.0;

                for (int k = 0; k < dim; k++)
                {
                    double d = points[i * dim + k] - points[(i - 1) * dim + k];
                    square += d * d;
                }

                total += Math.Sqrt(square);
                cumulative[i] = total;
            }

            var shares = new double[count];

            if (total > 0.0)
            {
                for (int i = 1; i < count; i++)
                {
                    shares[i] = cumulative[i] / total;
                }

                shares[count - 1] = 1.0;
            }

            return shares;
        }

        /// <summary>
        /// Makes the curve of degree two that runs through a chain of arcs of a circle, each one a rational quadratic
        /// Bézier piece that is exactly the arc.
        /// </summary>
        /// <param name="dim">The number of coordinates of a point.</param>
        /// <param name="ends">The ends of the pieces as flat coordinates: one more point than there are pieces.</param>
        /// <param name="apexes">The control point of each piece between its two ends, where the tangents at the ends meet.</param>
        /// <param name="apexWeights">The weight of each apex: the cosine of half the angle of the piece.</param>
        /// <remarks>
        /// A rational quadratic piece with the weights 1, cos(a / 2), 1 and the middle control point where the tangents
        /// meet is the arc of angle a of a circle, exactly, for any a below a half turn. The curve is over [0, 1], each
        /// piece covering an equal share of it.
        /// </remarks>
        internal static NurbsCurve FromArcPieces(int dim, double[] ends, double[] apexes, double[] apexWeights)
        {
            int pieces = apexWeights.Length;
            int count = 2 * pieces + 1;
            var points = new double[count * dim];
            var weights = new double[count];
            var knots = new double[count + 3];

            for (int i = 0; i <= pieces; i++)
            {
                Array.Copy(ends, i * dim, points, 2 * i * dim, dim);
                weights[2 * i] = 1.0;
            }

            for (int i = 0; i < pieces; i++)
            {
                Array.Copy(apexes, i * dim, points, (2 * i + 1) * dim, dim);
                weights[2 * i + 1] = apexWeights[i];
            }

            knots[0] = 0.0;
            knots[1] = 0.0;
            knots[2] = 0.0;

            for (int i = 1; i < pieces; i++)
            {
                double value = (double)i / pieces;
                knots[2 * i + 1] = value;
                knots[2 * i + 2] = value;
            }

            knots[count] = 1.0;
            knots[count + 1] = 1.0;
            knots[count + 2] = 1.0;

            Check(2, points, dim, knots, weights);
            return new NurbsCurve(dim, 2, points, knots, weights);
        }

        #region Interpolating

        /// <summary>
        /// Makes the curve of a degree that runs through a chain of fit points, by global interpolation (Piegl and Tiller,
        /// The NURBS Book, 9.2.1): the parameter of each point is the share of the chain's length before it, the knots
        /// are the averages of the parameters of the degree before each, and the control points are what the banded
        /// system of the curve passing through every fit point has for its solution.
        /// </summary>
        /// <param name="dim">The number of coordinates of a point.</param>
        /// <param name="fit">The fit points as flat coordinates; at least two, no two neighbours the same.</param>
        /// <param name="degree">The degree; no more than the number of fit points less one, or with end tangents no more than the number of fit points plus one.</param>
        /// <param name="startTangent">The direction the curve leaves the first point in, Dim numbers, or null for none.</param>
        /// <param name="endTangent">The direction the curve arrives at the last point in, Dim numbers, or null for none.</param>
        /// <remarks>
        /// A tangent gives a direction and nothing else. The curve is over [0, 1] and is asked to leave at a speed, in
        /// drawing units for the whole of it, equal to the length of the chain through the fit points, the speed it
        /// keeps on average: a curve through three points 100 apart along a line, given the tangent along that line at
        /// each end, comes out as that line, run at an even speed.
        /// </remarks>
        internal static NurbsCurve Interpolate(int dim, double[] fit, int degree, double[] startTangent, double[] endTangent)
            => Interpolate(dim, fit, degree, startTangent, endTangent, false);

        /// <summary>
        /// Makes the curve as <see cref="Interpolate(int, double[], int, double[], double[])"/> does; with tangents and
        /// <paramref name="natural"/>, its inner knots are the parameters of the inner fit points themselves rather than their
        /// averages, as ezdxf's <c>fit_points_to_cad_cv</c> places them for a spline of an odd degree ("natural").
        /// </summary>
        internal static NurbsCurve Interpolate(int dim, double[] fit, int degree, double[] startTangent, double[] endTangent, bool natural)
        {
            int count = fit.Length / dim;
            int n = count - 1;
            int p = degree;
            bool tangents = startTangent != null;
            double[] u = ChordShares(dim, fit, out double total);
            int points = tangents ? count + 2 : count;
            var knots = new double[points + p + 1];

            for (int i = 0; i <= p; i++)
            {
                knots[points + i] = 1.0;
            }

            if (tangents)
            {
                for (int j = 0; j <= n - p + 1; j++)
                {
                    knots[p + 1 + j] = natural ? u[j + 1] : Average(u, j, p);
                }
            }
            else
            {
                for (int j = 1; j <= n - p; j++)
                {
                    knots[j + p] = Average(u, j, p);
                }
            }

            var control = new double[points * dim];
            int unknownFirst = tangents ? 2 : 0;
            int unknowns = tangents ? count - 2 : count;
            int rows = unknowns;

            var matrix = new double[rows][];
            var rhs = new double[rows][];
            var basis = new double[p + 1];

            if (tangents)
            {
                for (int k = 0; k < dim; k++)
                {
                    control[k] = fit[k];
                    control[dim + k] = fit[k] + knots[p + 1] / p * startTangent[k] * total;
                    control[(points - 1) * dim + k] = fit[n * dim + k];
                    control[(points - 2) * dim + k] = fit[n * dim + k] - (1.0 - knots[points - 1]) / p * endTangent[k] * total;
                }
            }

            int width = 3 * p + 1;

            for (int row = 0; row < rows; row++)
            {
                int fitRow = tangents ? row + 1 : row;
                double t = u[fitRow];
                int span = FindSpan(knots, p, points, t);
                BasisFunctions(knots, p, span, t, basis);

                matrix[row] = new double[width];
                rhs[row] = new double[dim];

                for (int k = 0; k < dim; k++)
                {
                    rhs[row][k] = fit[fitRow * dim + k];
                }

                for (int j = 0; j <= p; j++)
                {
                    int column = span - p + j;
                    int unknown = column - unknownFirst;

                    if (unknown < 0 || unknown >= unknowns)
                    {
                        for (int k = 0; k < dim; k++)
                        {
                            rhs[row][k] -= basis[j] * control[column * dim + k];
                        }

                        continue;
                    }

                    int offset = unknown - row + p;

                    if (offset < 0 || offset > 2 * p)
                    {
                        throw new InvalidOperationException("The fit points do not make a banded system of the width a spline of this degree has.");
                    }

                    matrix[row][offset] = basis[j];
                }
            }

            SolveBanded(matrix, rhs, rows, p, p, dim);

            for (int i = 0; i < unknowns; i++)
            {
                for (int k = 0; k < dim; k++)
                {
                    control[(unknownFirst + i) * dim + k] = rhs[i][k];
                }
            }

            if (!tangents)
            {
                // Without tangents there is no point set apart from the rest: the fit points at the ends are the ends.
                for (int k = 0; k < dim; k++)
                {
                    control[k] = fit[k];
                    control[(points - 1) * dim + k] = fit[n * dim + k];
                }
            }

            Check(p, control, dim, knots, null);
            return new NurbsCurve(dim, p, control, knots, null);
        }

        /// <summary>
        /// Gets the mean of a degree's worth of the parameters from an index.
        /// </summary>
        private static double Average(double[] parameters, int from, int degree)
        {
            double sum = 0.0;

            for (int i = from; i < from + degree; i++)
            {
                sum += parameters[i];
            }

            return sum / degree;
        }

        /// <summary>
        /// Gets the degree + 1 basis functions that are above nought at a parameter (Piegl and Tiller, algorithm A2.2).
        /// </summary>
        private static void BasisFunctions(double[] knots, int degree, int span, double t, double[] basis)
        {
            var left = new double[degree + 1];
            var right = new double[degree + 1];
            basis[0] = 1.0;

            for (int j = 1; j <= degree; j++)
            {
                left[j] = t - knots[span + 1 - j];
                right[j] = knots[span + j] - t;
                double saved = 0.0;

                for (int r = 0; r < j; r++)
                {
                    double temp = basis[r] / (right[r + 1] + left[j - r]);
                    basis[r] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }

                basis[j] = saved;
            }
        }

        /// <summary>
        /// Solves a system whose matrix has no entry further than a number of places below or above its diagonal, by
        /// Gaussian elimination with the largest entry of the column as pivot: the work is a few multiplications for
        /// each row however many there are.
        /// </summary>
        /// <param name="matrix">
        /// The rows, each with kl + ku + kl + 1 places: the entry of row i and column j is at place j - i + kl, and room
        /// is left to the right for what pivoting spreads.
        /// </param>
        /// <param name="rhs">The right-hand sides, one array of the given width per row; overwritten with the solution.</param>
        /// <param name="size">The number of rows.</param>
        /// <param name="kl">How many places below the diagonal an entry may be.</param>
        /// <param name="ku">How many places above the diagonal an entry may be.</param>
        /// <param name="columns">The number of right-hand sides.</param>
        private static void SolveBanded(double[][] matrix, double[][] rhs, int size, int kl, int ku, int columns)
        {

            // The rows are held so that the entry of row i and column j is at j - i + kl. A matrix built with p each side
            // has its entries in places 0 to 2p, which is inside the width.
            for (int c = 0; c < size; c++)
            {
                int pivot = c;
                double largest = Math.Abs(matrix[c][kl]);
                int last = Math.Min(c + kl, size - 1);

                for (int r = c + 1; r <= last; r++)
                {
                    double candidate = Math.Abs(matrix[r][c - r + kl]);

                    if (candidate > largest)
                    {
                        largest = candidate;
                        pivot = r;
                    }
                }

                if (!(largest > 0.0))
                {
                    throw new InvalidOperationException("The fit points make a singular system, so no curve of this degree runs through them.");
                }

                int reach = Math.Min(c + kl + ku, size - 1);

                if (pivot != c)
                {
                    for (int j = c; j <= reach; j++)
                    {
                        double held = matrix[c][j - c + kl];
                        matrix[c][j - c + kl] = matrix[pivot][j - pivot + kl];
                        matrix[pivot][j - pivot + kl] = held;
                    }

                    double[] swap = rhs[c];
                    rhs[c] = rhs[pivot];
                    rhs[pivot] = swap;
                }

                for (int r = c + 1; r <= last; r++)
                {
                    double entry = matrix[r][c - r + kl];

                    if (entry == 0.0)
                    {
                        continue;
                    }

                    double factor = entry / matrix[c][kl];
                    matrix[r][c - r + kl] = 0.0;

                    for (int j = c + 1; j <= reach; j++)
                    {
                        matrix[r][j - r + kl] -= factor * matrix[c][j - c + kl];
                    }

                    for (int k = 0; k < columns; k++)
                    {
                        rhs[r][k] -= factor * rhs[c][k];
                    }
                }
            }

            for (int i = size - 1; i >= 0; i--)
            {
                int reach = Math.Min(i + kl + ku, size - 1);

                for (int k = 0; k < columns; k++)
                {
                    double value = rhs[i][k];

                    for (int j = i + 1; j <= reach; j++)
                    {
                        value -= matrix[i][j - i + kl] * rhs[j][k];
                    }

                    rhs[i][k] = value / matrix[i][kl];
                }
            }
        }

        #endregion
    }
}
