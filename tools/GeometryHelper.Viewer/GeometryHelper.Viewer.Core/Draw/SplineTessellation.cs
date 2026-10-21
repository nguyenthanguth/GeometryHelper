using System;
using System.Collections.Generic;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Cuts splines into the chords the viewer draws. It is the viewer's own, as <see cref="ArcTessellation"/> is, and never the
    /// library's <c>Flatten</c>: the spline is cut at its knots into Bézier pieces in one pass over it (Piegl and Tiller's
    /// decomposition, in homogeneous coordinates), and each piece is halved by de Casteljau's rule until it keeps to the
    /// <see cref="ChordRule"/>.
    /// </summary>
    /// <remarks>
    /// A piece keeps to the rule when it is within the chord tolerance of the chord from its start to its end, by either of two proofs:
    /// every one of its control points is within it, which holds the whole piece within it, since a piece with weights above nought
    /// lies inside the hull of its control points; or, for a piece whose weights are all one, p (p - 1) / 8 of the largest second
    /// difference of its control points is, which bounds how far it strays from the chord at the same parameter. And it keeps to the
    /// rule when its control polygon turns through at most 2 degrees in all, or is no longer than the chord tolerance. Under
    /// the rule that cuts by the angle alone only the turn counts. A Bézier piece is halved at most 16 times, into 65 536 chords; past
    /// that the rule is marked as capped. The cubic of control points (0, 0), (100, 150), (200, 150), (300, 0), a parabola of radius 100
    /// at its top, is 1 722 chords at a chord tolerance of 1E-4, a tenth of the default point tolerance, the count the library's
    /// <c>Flatten</c> gives too, both proofs of a piece being looser than the curve: the arc rule cuts the arc of radius 100 that turns
    /// as far, 112.6 degrees, in 695. The cutting costs the same for each piece, however many there are: a cubic of 40 000 control
    /// points takes about as long as the library's <c>Flatten</c> does.
    /// </remarks>
    internal static class SplineTessellation
    {
        /// <summary>The most times a Bézier piece is halved: 2^16 = 65 536 chords.</summary>
        internal const int MostHalvings = 16;

        /// <summary>
        /// Gets the points along a spline, the first its first control point and the last its last, exactly.
        /// </summary>
        /// <param name="rule">How finely to cut.</param>
        /// <param name="degree">The degree.</param>
        /// <param name="points">The control points.</param>
        /// <param name="knots">The knots: points + degree + 1 of them, not decreasing, clamped at both ends.</param>
        /// <param name="weights">The weights, one to a control point and above nought, or null for all of them one.</param>
        /// <exception cref="FormatException">Thrown when the numbers do not make a clamped spline.</exception>
        internal static Vec3[] Points(ChordRule rule, int degree, IReadOnlyList<Vec3> points, IReadOnlyList<double> knots, IReadOnlyList<double> weights)
            => SplineCut.Of(degree, points, knots, weights).Points(rule);

        /// <summary>
        /// Checks the numbers of a spline and cuts it into its Bézier pieces, each degree + 1 homogeneous points (x w, y w, z w, w), in one
        /// pass: each inner knot is raised to the degree as the pass reaches it, from the control points of the piece before (Piegl and
        /// Tiller, algorithm A5.6).
        /// </summary>
        internal static List<double[]> Pieces(int degree, IReadOnlyList<Vec3> points, IReadOnlyList<double> knots, IReadOnlyList<double> weights)
        {
            int p = degree;
            int n = points.Count;

            if (p < 1 || n < p + 1)
            {
                throw new FormatException("A spline of degree " + p + " has at least " + (p + 1) + " control points; it has " + n + ".");
            }

            if (knots.Count != n + p + 1)
            {
                throw new FormatException("A spline of " + n + " control points and degree " + p + " has " + (n + p + 1) + " knots; it has " + knots.Count + ".");
            }

            if (weights != null && weights.Count != n)
            {
                throw new FormatException("A spline has one weight to a control point: " + n + "; it has " + weights.Count + ".");
            }

            for (int i = 0; i < knots.Count; i++)
            {
                if (double.IsNaN(knots[i]) || double.IsInfinity(knots[i]) || (i > 0 && knots[i] < knots[i - 1]))
                {
                    throw new FormatException("The knots of a spline are numbers that do not fall.");
                }
            }

            for (int i = 1; i <= p; i++)
            {
                if (knots[i] != knots[0] || knots[knots.Count - 1 - i] != knots[knots.Count - 1])
                {
                    throw new FormatException("The knots of a spline are clamped: its first degree + 1 are one value, and so are its last.");
                }
            }

            if (knots[p + 1] == knots[0] || knots[knots.Count - 2 - p] == knots[knots.Count - 1])
            {
                throw new FormatException("The knots of a spline are repeated degree + 1 times at each end and no more.");
            }

            for (int i = p + 1, run = 1; i < knots.Count - p - 1; i++)
            {
                run = knots[i] == knots[i - 1] ? run + 1 : 1;

                if (run > p)
                {
                    throw new FormatException("No inner knot of a spline is repeated more than degree times.");
                }
            }

            var pw = new double[n * 4];

            for (int i = 0; i < n; i++)
            {
                double w = weights == null ? 1.0 : weights[i];

                if (!(w > 0.0) || double.IsInfinity(w))
                {
                    throw new FormatException("The weights of a spline are numbers above nought.");
                }

                pw[i * 4] = points[i].X * w;
                pw[i * 4 + 1] = points[i].Y * w;
                pw[i * 4 + 2] = points[i].Z * w;
                pw[i * 4 + 3] = w;
            }

            int m = n + p;
            int width = (p + 1) * 4;
            var alphas = new double[p];
            var pieces = new List<double[]>();
            var piece = new double[width];
            Array.Copy(pw, 0, piece, 0, width);
            int a = p;
            int b = p + 1;

            while (b < m)
            {
                int first = b;

                while (b < m && knots[b + 1] == knots[b])
                {
                    b++;
                }

                int mult = b - first + 1;
                double[] next = b < m ? new double[width] : null;

                if (mult < p)
                {
                    double numerator = knots[b] - knots[a];

                    for (int j = p; j > mult; j--)
                    {
                        alphas[j - mult - 1] = numerator / (knots[a + j] - knots[a]);
                    }

                    int r = p - mult;

                    for (int j = 1; j <= r; j++)
                    {
                        int save = r - j;
                        int s = mult + j;

                        for (int k = p; k >= s; k--)
                        {
                            double alpha = alphas[k - s];

                            for (int c = 0; c < 4; c++)
                            {
                                piece[k * 4 + c] = alpha * piece[k * 4 + c] + (1.0 - alpha) * piece[(k - 1) * 4 + c];
                            }
                        }

                        if (next != null)
                        {
                            Array.Copy(piece, p * 4, next, save * 4, 4);
                        }
                    }
                }

                pieces.Add(piece);

                if (next != null)
                {
                    for (int k = p - mult; k <= p; k++)
                    {
                        Array.Copy(pw, (b - p + k) * 4, next, k * 4, 4);
                    }

                    piece = next;
                    a = b;
                    b++;
                }
            }

            return pieces;
        }

        /// <summary>
        /// Adds the end of each part of a piece that keeps to the rule, halving the piece until it does.
        /// </summary>
        internal static void Cut(ChordRule rule, int p, double[] piece, int depth, List<Vec3> into)
        {
            var stack = new Stack<KeyValuePair<double[], int>>();
            stack.Push(new KeyValuePair<double[], int>(piece, depth));

            while (stack.Count > 0)
            {
                KeyValuePair<double[], int> top = stack.Pop();
                double[] h = top.Key;

                if (top.Value >= MostHalvings || Keeps(rule, p, h))
                {
                    if (top.Value >= MostHalvings && !Keeps(rule, p, h))
                    {
                        rule.MarkCapped();
                    }

                    into.Add(Projected(h, p));
                    continue;
                }

                Halve(h, p, out double[] left, out double[] right);
                stack.Push(new KeyValuePair<double[], int>(right, top.Value + 1));
                stack.Push(new KeyValuePair<double[], int>(left, top.Value + 1));
            }
        }

        private static Vec3 Projected(double[] h, int j) => new Vec3(h[j * 4] / h[j * 4 + 3], h[j * 4 + 1] / h[j * 4 + 3], h[j * 4 + 2] / h[j * 4 + 3]);

        /// <summary>
        /// Whether a piece keeps to the rule: its control points within the chord tolerance of its chord, and its control polygon turning
        /// through at most 2 degrees or no longer than the tolerance.
        /// </summary>
        private static bool Keeps(ChordRule rule, int p, double[] h)
        {
            var c = new Vec3[p + 1];

            for (int j = 0; j <= p; j++)
            {
                c[j] = Projected(h, j);
            }

            double tolerance = rule.ChordTolerance;

            if (!rule.AngleOnly && !WithinHull(c, p, tolerance) && !WithinSecondDifferences(c, h, p, tolerance))
            {
                return false;
            }

            double length = 0.0;
            double turn = 0.0;
            Vec3 before = default(Vec3);
            bool any = false;

            for (int j = 1; j <= p; j++)
            {
                Vec3 leg = c[j] - c[j - 1];
                double size = leg.Length;
                length += size;

                if (!(size > 0.0))
                {
                    continue;
                }

                if (any)
                {
                    turn += Math.Atan2(Vec3.Cross(before, leg).Length, Vec3.Dot(before, leg));
                }

                before = leg;
                any = true;
            }

            return turn <= ChordRule.MaxPieceAngle || length <= tolerance;
        }

        /// <summary>
        /// Whether every control point of a piece is within the tolerance of its chord, which holds the piece within it.
        /// </summary>
        private static bool WithinHull(Vec3[] c, int p, double tolerance)
        {
            for (int j = 1; j < p; j++)
            {
                if (!(DistanceToSegment(c[j], c[0], c[p]) <= tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a piece whose weights are all one strays from its chord by no more than the tolerance by the bound of its second differences:
        /// the curve is within p (p - 1) / 8 of the largest of them of the chord's point at the same parameter.
        /// </summary>
        private static bool WithinSecondDifferences(Vec3[] c, double[] h, int p, double tolerance)
        {
            for (int j = 0; j <= p; j++)
            {
                if (h[j * 4 + 3] != 1.0)
                {
                    return false;
                }
            }

            double largest = 0.0;

            for (int j = 1; j < p; j++)
            {
                largest = Math.Max(largest, (c[j - 1] - c[j] * 2.0 + c[j + 1]).Length);
            }

            return p * (p - 1) / 8.0 * largest <= tolerance;
        }

        private static double DistanceToSegment(Vec3 point, Vec3 a, Vec3 b)
        {
            Vec3 e = b - a;
            double ee = Vec3.Dot(e, e);
            double s = ee > 0.0 ? Math.Max(0.0, Math.Min(1.0, Vec3.Dot(point - a, e) / ee)) : 0.0;
            return (point - (a + e * s)).Length;
        }

        /// <summary>
        /// Halves a Bézier piece at the middle of its parameter by de Casteljau's rule, in homogeneous coordinates.
        /// </summary>
        private static void Halve(double[] h, int p, out double[] left, out double[] right)
        {
            left = new double[h.Length];
            right = new double[h.Length];
            var work = (double[])h.Clone();

            for (int c = 0; c < 4; c++)
            {
                left[c] = work[c];
                right[p * 4 + c] = work[p * 4 + c];
            }

            for (int level = 1; level <= p; level++)
            {
                for (int j = 0; j <= p - level; j++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        work[j * 4 + c] = 0.5 * (work[j * 4 + c] + work[(j + 1) * 4 + c]);
                    }
                }

                for (int c = 0; c < 4; c++)
                {
                    left[level * 4 + c] = work[c];
                    right[(p - level) * 4 + c] = work[(p - level) * 4 + c];
                }
            }
        }
    }

    /// <summary>
    /// A spline cut into its Bézier pieces once, to be cut into chords at as many rules as a scene asks of it: the points of each rule are
    /// kept, so that the count of a scene's points and its drawing at the same rule cut the spline once.
    /// </summary>
    internal sealed class SplineCut
    {
        private readonly int _degree;
        private readonly List<double[]> _pieces;
        private readonly Vec3 _first;
        private readonly Vec3 _last;
        private readonly List<Cut> _kept = new List<Cut>();

        private SplineCut(int degree, List<double[]> pieces, Vec3 first, Vec3 last)
        {
            _degree = degree;
            _pieces = pieces;
            _first = first;
            _last = last;
        }

        /// <summary>
        /// Checks the numbers of a spline and cuts it into its Bézier pieces.
        /// </summary>
        /// <exception cref="FormatException">Thrown when the numbers do not make a clamped spline.</exception>
        internal static SplineCut Of(int degree, IReadOnlyList<Vec3> points, IReadOnlyList<double> knots, IReadOnlyList<double> weights)
            => new SplineCut(degree, SplineTessellation.Pieces(degree, points, knots, weights), points[0], points[points.Count - 1]);

        /// <summary>
        /// Gets the points along the spline at a rule, the first its first control point and the last its last, exactly; where a piece had
        /// to be cut short of the rule, the rule is marked as capped. The array is the one kept for the rule, and is not to be changed.
        /// </summary>
        internal Vec3[] Points(ChordRule rule)
        {
            Cut cut = _kept.Find(k => k.Tolerance == rule.ChordTolerance && k.AngleOnly == rule.AngleOnly);

            if (cut == null)
            {
                var own = new ChordRule(rule.ChordTolerance, rule.AngleOnly);
                var points = new List<Vec3> { _first };

                foreach (double[] piece in _pieces)
                {
                    SplineTessellation.Cut(own, _degree, piece, 0, points);
                }

                points[points.Count - 1] = _last;
                cut = new Cut { Tolerance = rule.ChordTolerance, AngleOnly = rule.AngleOnly, Points = points.ToArray(), Capped = own.Capped };
                _kept.Add(cut);
            }

            if (cut.Capped)
            {
                rule.MarkCapped();
            }

            return cut.Points;
        }

        private sealed class Cut
        {
            internal double Tolerance;
            internal bool AngleOnly;
            internal Vec3[] Points;
            internal bool Capped;
        }
    }
}
