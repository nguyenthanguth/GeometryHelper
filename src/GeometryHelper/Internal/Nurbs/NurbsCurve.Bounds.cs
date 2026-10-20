using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    internal sealed partial class NurbsCurve
    {
        /// <summary>
        /// A polynomial whose coefficients are all within this share of the size of the numbers it was worked out from
        /// is rounding noise round nought.
        /// </summary>
        private const double NoiseShare = 1E-14;

        /// <summary>
        /// Gets the Bernstein coefficients of the numerator of each coordinate of a piece, and of its weight, measured
        /// from a point: the coordinate is the numerator over the weight.
        /// </summary>
        /// <param name="pieces">The pieces.</param>
        /// <param name="piece">Which piece.</param>
        /// <param name="origin">The point to measure from, Dim numbers, or null for the origin of the drawing.</param>
        /// <param name="numerators">One array of Degree + 1 coefficients per coordinate.</param>
        /// <param name="weights">The weight's Degree + 1 coefficients, or null when the piece is not rational.</param>
        private static void Numerators(BezierPieces pieces, int piece, double[] origin, out double[][] numerators, out double[] weights)
        {
            int p = pieces.Degree;
            int dim = pieces.Dim;
            int at = pieces.PointOffset(piece);
            numerators = new double[dim][];
            weights = null;

            if (pieces.Rational)
            {
                weights = new double[p + 1];
                Array.Copy(pieces.Weights, pieces.WeightOffset(piece), weights, 0, p + 1);
            }

            for (int k = 0; k < dim; k++)
            {
                var x = new double[p + 1];

                for (int j = 0; j <= p; j++)
                {
                    double value = pieces.Points[at + j * dim + k];

                    if (origin != null)
                    {
                        value -= origin[k];
                    }

                    x[j] = pieces.Rational ? weights[j] * value : value;
                }

                numerators[k] = x;
            }
        }

        /// <summary>
        /// Gets the numerator of the derivative of one coordinate of a piece: the polynomial whose roots are where the
        /// coordinate stops rising or falling. For a rational piece it is x' w - x w', the numerator of the quotient's
        /// derivative, since the weight squared is above nought.
        /// </summary>
        private static double[] CoordinateSlope(double[] numerator, double[] weights, out double scale)
        {
            double[] slope = Bernstein.Derivative(numerator);

            if (weights == null)
            {
                scale = Bernstein.MaxAbs(slope);
                return slope;
            }

            double[] weightSlope = Bernstein.Derivative(weights);
            scale = Bernstein.MaxAbs(slope) * Bernstein.MaxAbs(weights) + Bernstein.MaxAbs(numerator) * Bernstein.MaxAbs(weightSlope);

            return Bernstein.Subtract(Bernstein.Multiply(slope, weights), Bernstein.Multiply(numerator, weightSlope));
        }

        /// <summary>
        /// Gets the box the curve fills exactly: the smallest and largest value of each coordinate, which are at the ends
        /// of a piece or where the coordinate's derivative is nought.
        /// </summary>
        /// <param name="min">Dim numbers to write the smallest coordinates to.</param>
        /// <param name="max">Dim numbers to write the largest coordinates to.</param>
        /// <remarks>
        /// The ends of every piece are taken first, which are on the curve and nearly always most of the box; then a
        /// piece whose control points reach past it in a coordinate has the roots of that coordinate's derivative
        /// found, as <see cref="Bernstein.FindRoots"/> finds them, and the coordinate taken there. A piece whose control
        /// points lie inside the box cannot reach past it, since the piece lies in their hull, and costs nothing.
        /// </remarks>
        internal void Bounds(double[] min, double[] max)
        {
            BezierPieces pieces = Pieces;
            int p = pieces.Degree;
            int dim = pieces.Dim;

            for (int k = 0; k < dim; k++)
            {
                min[k] = double.PositiveInfinity;
                max[k] = double.NegativeInfinity;
            }

            for (int piece = 0; piece < pieces.Count; piece++)
            {
                int at = pieces.PointOffset(piece);

                for (int k = 0; k < dim; k++)
                {
                    double first = pieces.Points[at + k];
                    double last = pieces.Points[at + p * dim + k];
                    min[k] = Math.Min(min[k], Math.Min(first, last));
                    max[k] = Math.Max(max[k], Math.Max(first, last));
                }
            }

            var roots = new List<double>();
            var origin = new double[dim];

            for (int piece = 0; piece < pieces.Count; piece++)
            {
                // Each piece is measured from its first control point, so that a curve far from the origin keeps its digits.
                for (int k = 0; k < dim; k++)
                {
                    origin[k] = pieces.Points[pieces.PointOffset(piece) + k];
                }

                Numerators(pieces, piece, origin, out double[][] numerators, out double[] weights);

                for (int k = 0; k < dim; k++)
                {
                    int box = piece * dim + k;

                    if (pieces.BoxMin[box] >= min[k] && pieces.BoxMax[box] <= max[k])
                    {
                        continue;
                    }

                    double[] slope = CoordinateSlope(numerators[k], weights, out double scale);
                    roots.Clear();
                    Bernstein.FindRoots(slope, NoiseShare * scale, roots);

                    for (int i = 0; i < roots.Count; i++)
                    {
                        double s = roots[i];
                        double value = Bernstein.Evaluate(numerators[k], s);

                        if (weights != null)
                        {
                            value /= Bernstein.Evaluate(weights, s);
                        }

                        value += origin[k];
                        min[k] = Math.Min(min[k], value);
                        max[k] = Math.Max(max[k], value);
                    }
                }
            }
        }
    }
}
