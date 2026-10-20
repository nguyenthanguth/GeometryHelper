using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    internal sealed partial class NurbsCurve
    {
        /// <summary>
        /// The deepest a piece is cut in halves while it is made flat: a piece of 2 to the minus 60 of a span is as narrow
        /// as a double can tell from a point.
        /// </summary>
        private const int MostFlattenDepth = 60;

        /// <summary>
        /// Cuts the curve into straight pieces that stray no further than a chord tolerance from it, and adds the corners
        /// to a list as Dim numbers each, the start of the curve first and its end last.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between the curve and the straight pieces; above nought.</param>
        /// <param name="corners">The list the coordinates are added to.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance would need more than 1 048 576 pieces.</exception>
        /// <remarks>
        /// <para>
        /// A piece is flat enough when any one of three proofs holds, each of which is a guarantee that every point of the
        /// piece is within the tolerance of the chord, from the start of the piece to its end. First, a Bézier piece lies in
        /// the hull of its control points, and the distance from a segment is a convex function, so if every control point is
        /// within the tolerance of the chord then so is the whole piece. Second, for a piece that is not rational, the curve
        /// strays from the chord, at the same parameter, by no more than p (p - 1) / 8 times the largest second difference
        /// of its control points (the error of a straight line through the ends, with the second derivative bounded by the
        /// second differences). Third, for a rational quadratic, a conic, the farthest the curve stands from the chord's
        /// line is exactly w / (1 + w) of the height of its middle control point, where w is its weight with the weights of
        /// the ends taken as one; and when that control point lies between the ends along the chord the curve does not run
        /// past them, so the distance from the segment is the distance from the line.
        /// </para>
        /// <para>
        /// A piece that passes none is cut in halves by de Casteljau at the middle of its parameter, rational pieces in
        /// homogeneous coordinates, and each half is tried in turn. The corners are the ends of the pieces that pass, so
        /// they are points of the curve to rounding, and the promise holds however sharp the bend, whatever the weights,
        /// and at a cusp or a loop. Only the first proof is used for a rational piece of a degree above two, which is the
        /// safe one and not the tight one: it cuts such a piece finer than it need be.
        /// </para>
        /// </remarks>
        internal void Flatten(double chordTolerance, List<double> corners)
        {
            BezierPieces pieces = Pieces;
            int p = pieces.Degree;
            int dim = pieces.Dim;
            int stride = Rational ? dim + 1 : dim;
            int width = (p + 1) * stride;
            int leaves = 0;

            for (int k = 0; k < dim; k++)
            {
                corners.Add(pieces.Points[k]);
            }

            var stack = new Stack<Cut>();
            var points = new double[(p + 1) * dim];

            for (int piece = pieces.Count - 1; piece >= 0; piece--)
            {
                // Pieces are pushed last first, so the first comes off first.
                var hw = new double[width];
                int at = pieces.PointOffset(piece);

                for (int j = 0; j <= p; j++)
                {
                    double w = Rational ? pieces.Weights[pieces.WeightOffset(piece) + j] : 1.0;

                    for (int k = 0; k < dim; k++)
                    {
                        double value = pieces.Points[at + j * dim + k];
                        hw[j * stride + k] = Rational ? w * value : value;
                    }

                    if (Rational)
                    {
                        hw[j * stride + dim] = w;
                    }
                }

                // A piece's first and last control points are the points of the curve there as they are held, so that
                // the corners of neighbouring pieces are one and the same number.
                stack.Push(new Cut(hw, 0));
            }

            while (stack.Count > 0)
            {
                Cut cut = stack.Pop();
                double[] hw = cut.Control;

                for (int j = 0; j <= p; j++)
                {
                    for (int k = 0; k < dim; k++)
                    {
                        points[j * dim + k] = Rational ? hw[j * stride + k] / hw[j * stride + dim] : hw[j * stride + k];
                    }
                }

                if (cut.Depth >= MostFlattenDepth || IsFlat(points, hw, Rational, p, dim, chordTolerance))
                {
                    leaves++;

                    if (leaves > Tessellation.MostSegments)
                    {
                        throw Tessellation.TooMany(chordTolerance);
                    }

                    for (int k = 0; k < dim; k++)
                    {
                        corners.Add(points[p * dim + k]);
                    }

                    continue;
                }

                SplitHomogeneous(hw, p, stride, out double[] left, out double[] right);

                stack.Push(new Cut(right, cut.Depth + 1));
                stack.Push(new Cut(left, cut.Depth + 1));
            }
        }

        /// <summary>
        /// Says whether a piece is within a tolerance of the segment from its first control point to its last, by any of
        /// the three proofs set out for <see cref="Flatten"/>.
        /// </summary>
        /// <param name="points">The projected control points of the piece.</param>
        /// <param name="hw">The piece's control points in homogeneous form; the weights are the last number of each when rational.</param>
        /// <param name="rational">Whether the piece is rational.</param>
        /// <param name="p">The degree.</param>
        /// <param name="dim">The number of coordinates of a point.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool IsFlat(double[] points, double[] hw, bool rational, int p, int dim, double tolerance)
        {
            if (HullIsFlat(points, p, dim, tolerance))
            {
                return true;
            }

            if (!rational)
            {
                return p >= 2 && SecondDifferenceBound(points, p, dim) <= tolerance;
            }

            return p == 2 && ConicIsFlat(points, hw, dim, tolerance);
        }

        /// <summary>
        /// Gets the bound p (p - 1) / 8 times the largest second difference of the control points, on how far a polynomial
        /// piece strays from the straight line through its ends at the same parameter.
        /// </summary>
        private static double SecondDifferenceBound(double[] points, int p, int dim)
        {
            double largest = 0.0;

            for (int j = 0; j + 2 <= p; j++)
            {
                double square = 0.0;

                for (int k = 0; k < dim; k++)
                {
                    double d = points[j * dim + k] - 2.0 * points[(j + 1) * dim + k] + points[(j + 2) * dim + k];
                    square += d * d;
                }

                largest = Math.Max(largest, square);
            }

            return p * (p - 1) / 8.0 * Math.Sqrt(largest);
        }

        /// <summary>
        /// Says whether a rational quadratic piece is within a tolerance of the segment between its ends: the farthest the
        /// conic stands from the line through them is the height of its middle control point times w / (1 + w), with w the
        /// weight of that point over the root of the product of the end weights, provided the control point lies between
        /// the ends along the chord.
        /// </summary>
        private static bool ConicIsFlat(double[] points, double[] hw, int dim, double tolerance)
        {
            int stride = dim + 1;
            double normal = hw[stride + dim] / Math.Sqrt(hw[dim] * hw[2 * stride + dim]);
            double lengthSquared = 0.0;
            double along = 0.0;

            for (int k = 0; k < dim; k++)
            {
                double e = points[2 * dim + k] - points[k];
                lengthSquared += e * e;
                along += (points[dim + k] - points[k]) * e;
            }

            if (!(lengthSquared > 0.0) || along < 0.0 || along > lengthSquared)
            {
                return false;
            }

            // The height of the middle control point over the chord's line, from the part of its offset square to the chord.
            double heightSquared = 0.0;

            for (int k = 0; k < dim; k++)
            {
                double d = points[dim + k] - points[k] - along / lengthSquared * (points[2 * dim + k] - points[k]);
                heightSquared += d * d;
            }

            return Math.Sqrt(heightSquared) * normal / (1.0 + normal) <= tolerance;
        }

        /// <summary>
        /// Says whether every control point of a piece lies within a tolerance of the segment from its first to its last.
        /// </summary>
        private static bool HullIsFlat(double[] points, int p, int dim, double tolerance)
        {
            double lengthSquared = 0.0;

            for (int k = 0; k < dim; k++)
            {
                double e = points[p * dim + k] - points[k];
                lengthSquared += e * e;
            }

            double limit = tolerance * tolerance;

            for (int j = 1; j < p; j++)
            {
                double along = 0.0;

                for (int k = 0; k < dim; k++)
                {
                    along += (points[j * dim + k] - points[k]) * (points[p * dim + k] - points[k]);
                }

                double share = lengthSquared > 0.0 ? Math.Min(Math.Max(along / lengthSquared, 0.0), 1.0) : 0.0;
                double square = 0.0;

                for (int k = 0; k < dim; k++)
                {
                    double d = points[j * dim + k] - (points[k] + share * (points[p * dim + k] - points[k]));
                    square += d * d;
                }

                if (square > limit)
                {
                    return false;
                }
            }

            return true;
        }

        private struct Cut
        {
            internal readonly double[] Control;
            internal readonly int Depth;

            internal Cut(double[] control, int depth)
            {
                Control = control;
                Depth = depth;
            }
        }
    }
}
