using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    internal sealed partial class NurbsCurve
    {
        /// <summary>
        /// How far apart, as a share of the distance and of the size of the curve, two distances may be and still count
        /// as the same: the nearest of two equally near points is then the one with the lower parameter, whichever way
        /// rounding tipped them.
        /// </summary>
        private const double TieShare = 1E-15;

        /// <summary>
        /// Gets the parameter of the point of the curve nearest a point, and how far it is.
        /// </summary>
        /// <param name="query">The point, Dim numbers.</param>
        /// <param name="distance">The distance from the point to the curve.</param>
        /// <returns>
        /// The parameter. Where two points of the curve are equally near, within rounding, the lower parameter.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The distance is smallest at an end of the curve or where the curve's derivative is square to the line to the
        /// point, which is where (C(t) - q) . C'(t) is nought. On each Bézier piece that is a polynomial, found in
        /// Bernstein form from the control points measured from q, and its roots are all found, none missed
        /// (<see cref="Bernstein.FindRoots"/>); the nearest of them and the ends is the answer, to rounding, whatever the
        /// shape: a curve that doubles back, a point inside a loop, a point at the centre of an arc.
        /// </para>
        /// <para>
        /// Pieces are not all solved. A piece lies in the hull of its control points, so it is no nearer than the box of
        /// them: the piece whose box is nearest is solved first, and every other is solved only if its box is nearer
        /// than the best found. The boxes of all of them are looked at, though, so a query is linear in the number of pieces
        /// (0.4 to 0.7 ms at 100 000), and an index over the pieces would make it logarithmic.
        /// </para>
        /// </remarks>
        internal double ClosestParameter(double[] query, out double distance)
        {
            BezierPieces pieces = Pieces;
            int dim = pieces.Dim;
            int count = pieces.Count;
            int nearest = 0;
            double nearestBound = double.PositiveInfinity;

            for (int piece = 0; piece < count; piece++)
            {
                double bound = BoxDistance(pieces, piece, query);

                if (bound < nearestBound)
                {
                    nearestBound = bound;
                    nearest = piece;
                }
            }

            double bestDistance = double.PositiveInfinity;
            double bestParameter = Start;
            var roots = new List<double>();

            Solve(pieces, nearest, query, roots, ref bestDistance, ref bestParameter);

            for (int piece = 0; piece < count; piece++)
            {
                if (piece == nearest || BoxDistance(pieces, piece, query) > bestDistance + TieShare * (bestDistance + Scale))
                {
                    continue;
                }

                Solve(pieces, piece, query, roots, ref bestDistance, ref bestParameter);
            }

            var point = new double[dim];
            PointAt(bestParameter, point);
            double square2 = 0.0;

            for (int k = 0; k < dim; k++)
            {
                double d = point[k] - query[k];
                square2 += d * d;
            }

            distance = Math.Sqrt(square2);
            return bestParameter;
        }

        /// <summary>
        /// Gets how far a point is from the box of the control points of a piece, which the piece lies inside.
        /// </summary>
        private static double BoxDistance(BezierPieces pieces, int piece, double[] query)
        {
            int dim = pieces.Dim;
            double square = 0.0;

            for (int k = 0; k < dim; k++)
            {
                double low = pieces.BoxMin[piece * dim + k];
                double high = pieces.BoxMax[piece * dim + k];
                double gap = query[k] < low ? low - query[k] : query[k] > high ? query[k] - high : 0.0;
                square += gap * gap;
            }

            return Math.Sqrt(square);
        }

        /// <summary>
        /// Takes a candidate for the nearest point if it is nearer than the best by more than rounding, or as near and
        /// with a lower parameter.
        /// </summary>
        private void Consider(double distance, double parameter, ref double bestDistance, ref double bestParameter)
        {
            double band = TieShare * ((double.IsInfinity(bestDistance) ? 0.0 : bestDistance) + Scale);

            if (distance < bestDistance - band || (distance <= bestDistance + band && parameter < bestParameter))
            {
                bestDistance = distance;
                bestParameter = parameter;
            }
        }

        /// <summary>
        /// Finds the nearest point of one piece to a point and offers it, with the ends of the piece, to the best so far.
        /// </summary>
        private void Solve(BezierPieces pieces, int piece, double[] query, List<double> roots, ref double bestDistance, ref double bestParameter)
        {
            int p = pieces.Degree;
            int dim = pieces.Dim;
            int at = pieces.PointOffset(piece);
            double from = pieces.From[piece];
            double span = pieces.To[piece] - from;

            double[][] numerators;
            double[] weights;
            Numerators(pieces, piece, query, out numerators, out weights);

            // The derivative of half the squared distance, as a polynomial of the piece: its roots, in the share of the
            // piece's parameter, are the points where the curve stops coming nearer and starts going away.
            double[] slope = null;
            double scale = 0.0;

            for (int k = 0; k < dim; k++)
            {
                double[] term;
                double termScale;

                if (weights == null)
                {
                    double[] derivative = new double[p];

                    for (int j = 0; j < p; j++)
                    {
                        derivative[j] = p * (pieces.Points[at + (j + 1) * dim + k] - pieces.Points[at + j * dim + k]);
                    }

                    term = Bernstein.Multiply(numerators[k], derivative);
                    termScale = Bernstein.MaxAbs(numerators[k]) * Bernstein.MaxAbs(derivative);
                }
                else
                {
                    double[] rate = CoordinateSlope(numerators[k], weights, out double rateScale);
                    term = Bernstein.Multiply(numerators[k], rate);
                    termScale = Bernstein.MaxAbs(numerators[k]) * rateScale;
                }

                scale += termScale;

                if (slope == null)
                {
                    slope = term;
                }
                else
                {
                    for (int j = 0; j < slope.Length; j++)
                    {
                        slope[j] += term[j];
                    }
                }
            }

            roots.Clear();
            Bernstein.FindRoots(slope, NoiseShare * scale, roots);
            roots.Sort();

            ConsiderEnd(pieces, piece, 0, query, from, ref bestDistance, ref bestParameter);

            for (int i = 0; i < roots.Count; i++)
            {
                double s = roots[i];

                if (!(s > 0.0 && s < 1.0))
                {
                    continue;
                }

                double square = 0.0;
                double w = weights == null ? 1.0 : Bernstein.Evaluate(weights, s);

                for (int k = 0; k < dim; k++)
                {
                    double d = Bernstein.Evaluate(numerators[k], s) / w;
                    square += d * d;
                }

                Consider(Math.Sqrt(square), from + s * span, ref bestDistance, ref bestParameter);
            }

            ConsiderEnd(pieces, piece, p, query, pieces.To[piece], ref bestDistance, ref bestParameter);
        }

        private void ConsiderEnd(BezierPieces pieces, int piece, int index, double[] query, double parameter, ref double bestDistance, ref double bestParameter)
        {
            int dim = pieces.Dim;
            int at = pieces.PointOffset(piece) + index * dim;
            double square = 0.0;

            for (int k = 0; k < dim; k++)
            {
                double d = pieces.Points[at + k] - query[k];
                square += d * d;
            }

            Consider(Math.Sqrt(square), parameter, ref bestDistance, ref bestParameter);
        }
    }
}
