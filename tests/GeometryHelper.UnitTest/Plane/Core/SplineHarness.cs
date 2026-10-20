using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Random splines in two or three dimensions, drawn on a seeded generator, with the spline under test and the oracle of
    /// it made from the same numbers.
    /// </summary>
    internal static class SplineGen
    {
        private static double Between(Random r, double lo, double hi) => lo + (hi - lo) * r.NextDouble();

        /// <summary>
        /// Draws the knots of a clamped spline: the domain, and inner knots in runs of one to degree, sometimes spans a
        /// thousand times narrower than the rest.
        /// </summary>
        internal static double[] Knots(Random r, int degree, int count, bool multiples, bool thinSpans)
        {
            int inner = count - degree - 1;
            double a = r.Next(3) == 0 ? 0.0 : Math.Round(Between(r, -5, 5), 3);
            double b = r.Next(3) == 0 ? a + 1.0 : a + Math.Round(Between(r, 0.5, 12), 3);
            var values = new List<double>();
            int remaining = inner;
            var distinct = new List<double>();

            while (remaining > 0)
            {
                int run = multiples && r.Next(3) == 0 ? 1 + r.Next(Math.Min(degree, remaining)) : 1;
                distinct.Add(run);
                remaining -= run;
            }

            // Distinct values, in order, with the gaps drawn so that none is the whole domain.
            int groups = distinct.Count;
            var gaps = new double[groups + 1];
            double total = 0.0;

            for (int i = 0; i < gaps.Length; i++)
            {
                gaps[i] = thinSpans && r.Next(4) == 0 ? Between(r, 0.001, 0.01) : Between(r, 0.2, 1.0);
                total += gaps[i];
            }

            double at = 0.0;

            for (int i = 0; i < groups; i++)
            {
                at += gaps[i];
                double value = a + (b - a) * at / total;

                for (int k = 0; k < (int)distinct[i]; k++)
                {
                    values.Add(value);
                }
            }

            var knots = new double[count + degree + 1];

            for (int i = 0; i <= degree; i++)
            {
                knots[i] = a;
                knots[count + i] = b;
            }

            for (int i = 0; i < inner; i++)
            {
                knots[degree + 1 + i] = values[i];
            }

            return knots;
        }

        /// <summary>
        /// Draws a spline: a degree, control points in a box of a random size anywhere near the origin, inner knots,
        /// and, for half of them, weights.
        /// </summary>
        internal static SplineOracle Draw(Random r, int dim, int maxDegree = 5, bool rational = true, bool multiples = true, bool thinSpans = false, int extra = 7)
        {
            int degree = 1 + r.Next(maxDegree);
            int count = degree + 1 + r.Next(extra);
            double size = new[] { 10.0, 100.0, 1000.0 }[r.Next(3)];
            double offset = r.Next(2) == 0 ? 0.0 : Between(r, -size, size);
            var points = new double[count * dim];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = Math.Round(offset + Between(r, -size, size), 4);
            }

            double[] weights = null;

            if (rational && r.Next(2) == 0)
            {
                weights = new double[count];

                for (int i = 0; i < count; i++)
                {
                    weights[i] = Math.Round(Between(r, 0.2, 5.0), 3);
                }
            }

            return new SplineOracle(dim, degree, Knots(r, degree, count, multiples, thinSpans), points, weights);
        }

        /// <summary>The kinds of near-degenerate spline <see cref="Nasty"/> draws.</summary>
        internal static readonly string[] NastyKinds =
        {
            "FarFromTheOrigin", "RepeatedControlPoints", "AllOnALine", "SpansAMillionthWide", "HighDegree", "WeightsAThousandfoldApart", "ACusp"
        };

        /// <summary>
        /// Draws a spline that real models give and a random one rarely does: a million away from the origin, with control
        /// points repeated, all on one line, spans a millionth as wide as the rest, of a degree up to ten, with weights a
        /// thousandfold apart, or with a cusp.
        /// </summary>
        internal static SplineOracle Nasty(Random r, int dim, string kind)
        {
            int degree = 1 + r.Next(5);
            int count = degree + 1 + r.Next(6);
            double size = new[] { 10.0, 100.0, 1000.0 }[r.Next(3)];
            var points = new double[count * dim];
            double[] weights = null;
            double[] knots = null;

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = Math.Round(Between(r, -size, size), 4);
            }

            switch (kind)
            {
                case "FarFromTheOrigin":
                    double far = r.Next(2) == 0 ? 1E6 : -3E7;

                    for (int i = 0; i < points.Length; i++)
                    {
                        points[i] = Math.Round(far + points[i] * 0.1, 4);
                    }

                    break;

                case "RepeatedControlPoints":
                    for (int i = 0; i + 1 < count; i++)
                    {
                        if (r.Next(3) == 0)
                        {
                            for (int c = 0; c < dim; c++)
                            {
                                points[(i + 1) * dim + c] = points[i * dim + c];
                            }
                        }
                    }

                    // Not all of them one point.
                    points[(count - 1) * dim] += size;
                    break;

                case "AllOnALine":
                    var direction = new double[dim];
                    var origin = new double[dim];

                    for (int c = 0; c < dim; c++)
                    {
                        direction[c] = Between(r, -1, 1);
                        origin[c] = Between(r, -size, size);
                    }

                    for (int i = 0; i < count; i++)
                    {
                        double along = Between(r, -size, size);

                        for (int c = 0; c < dim; c++)
                        {
                            points[i * dim + c] = Math.Round(origin[c] + along * direction[c], 4);
                        }
                    }

                    break;

                case "SpansAMillionthWide":
                    knots = Knots(r, degree, count, false, false);
                    double domain = knots[knots.Length - 1] - knots[0];
                    int inner = count - degree - 1;

                    if (inner >= 2)
                    {
                        // The second inner knot a millionth of the domain after the first, the ones after it kept in order.
                        knots[degree + 2] = knots[degree + 1] + domain * 1E-6;

                        for (int j = degree + 3; j < knots.Length - degree - 1 && knots[j] <= knots[j - 1]; j++)
                        {
                            knots[j] = knots[j - 1] + domain * 1E-6;
                        }
                    }
                    else if (inner == 1)
                    {
                        knots[degree + 1] = knots[knots.Length - 1] - domain * 1E-6;
                    }

                    break;

                case "HighDegree":
                    degree = 6 + r.Next(5);
                    count = degree + 1 + r.Next(4);
                    points = new double[count * dim];

                    for (int i = 0; i < points.Length; i++)
                    {
                        points[i] = Math.Round(Between(r, -size, size), 4);
                    }

                    break;

                case "WeightsAThousandfoldApart":
                    weights = new double[count];

                    for (int i = 0; i < count; i++)
                    {
                        weights[i] = r.Next(2) == 0 ? 0.001 : 1000.0;
                    }

                    break;

                default:
                    // A cusp: the control polygon turns straight back on itself, so the speed falls to nought.
                    count = Math.Max(count, 4);
                    degree = Math.Min(3, count - 1);
                    points = new double[count * dim];

                    for (int c = 0; c < dim; c++)
                    {
                        points[c] = 0.0;
                        points[dim + c] = c == 0 ? size : size * 0.3 * c;
                        points[2 * dim + c] = c == 0 ? -size : size * 0.3 * c;
                        points[3 * dim + c] = c == 0 ? size * 0.1 : -size * 0.2 * c;
                    }

                    for (int i = 4; i < count; i++)
                    {
                        for (int c = 0; c < dim; c++)
                        {
                            points[i * dim + c] = Math.Round(Between(r, -size, size), 4);
                        }
                    }

                    break;
            }

            if (knots == null || knots.Length != count + degree + 1)
            {
                knots = Knots(r, degree, count, true, false);
            }

            return new SplineOracle(dim, degree, knots, points, weights);
        }

        /// <summary>Draws a parameter: the ends, a knot, or anywhere in the domain.</summary>
        internal static double Parameter(Random r, SplineOracle o)
        {
            switch (r.Next(6))
            {
                case 0: return o.Start;
                case 1: return o.End;
                case 2: return o.Knots[o.Degree + r.Next(o.Count - o.Degree + 1)];
                default: return o.Start + (o.End - o.Start) * r.NextDouble();
            }
        }

        /// <summary>Draws a point to ask about: on the curve, near it, or well away.</summary>
        internal static double[] Query(Random r, SplineOracle o)
        {
            double scale = o.Scale;
            double[] on = o.FastPoint(Parameter(r, o));
            var q = new double[o.Dim];

            double reach;

            switch (r.Next(4))
            {
                case 0: reach = 0.0; break;
                case 1: reach = scale * 0.01; break;
                case 2: reach = scale * 0.3; break;
                default: reach = scale * 3.0; break;
            }

            for (int c = 0; c < o.Dim; c++)
            {
                q[c] = on[c] + Between(r, -reach, reach);
            }

            return q;
        }
    }

    /// <summary>
    /// A spline of either dimension seen through the same set of questions, so that one test body runs on both with the
    /// arrays of numbers the oracle has: it is made through the public constructors, and every answer comes from the public
    /// members.
    /// </summary>
    internal sealed class SplineUnderTest
    {
        internal int Dim;
        internal object Spline;
        internal int Degree;
        internal double[] Knots;
        internal double[] Weights;
        internal double[] ControlPoints;
        internal Func<double, double[]> PointAt;
        internal Func<double[], double> DistanceTo;
        internal Func<double[], double> DistanceFromPoint;
        internal Func<double[], double> ParameterAtPoint;
        internal Func<double[], double[]> ClosestPoint;
        internal Func<double[], double[][]> ShortestLine;
        internal Func<double[], double[][]> ShortestLineFromPoint;
        internal Func<double[], bool> IsPointOn;
        internal Func<double> Length;
        internal Func<double[][]> Aabb;
        internal Func<double, SplineUnderTest[]> Split;
        internal Func<SplineUnderTest> Reverse;
        internal Func<double, double[][]> Flatten;
        internal Func<double, double> ParameterAtLength;
        internal Func<double, int, double[]> Derivative;
        internal Func<double, double[]> Tangent;
        internal Func<double, double> Curvature;
        internal Func<double> DomainStart;
        internal Func<double> DomainEnd;
        internal Func<bool> IsRational;

        private static double[] Flat(GeoPoint2 p) => new[] { p.X, p.Y };

        private static double[] Flat(GeoPoint3 p) => new[] { p.X, p.Y, p.Z };

        private static double[] Flat(GeoVector2 v) => new[] { v.X, v.Y };

        private static double[] Flat(GeoVector3 v) => new[] { v.X, v.Y, v.Z };

        private static double[][] Flat(GeoLine2 l) => new[] { Flat(l.StartPoint), Flat(l.EndPoint) };

        private static double[][] Flat(GeoLine3 l) => new[] { Flat(l.StartPoint), Flat(l.EndPoint) };

        private static GeoPoint2 P2(double[] q) => new GeoPoint2(q[0], q[1]);

        private static GeoPoint3 P3(double[] q) => new GeoPoint3(q[0], q[1], q[2]);

        /// <summary>Makes the spline of the oracle's numbers through the public constructor.</summary>
        internal static SplineUnderTest Make(SplineOracle o)
        {
            if (o.Dim == 2)
            {
                var points = new GeoPoint2[o.Count];

                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = new GeoPoint2(o.Points[2 * i], o.Points[2 * i + 1]);
                }

                bool rational = o.Weights.Any(w => w != 1.0);

                return Wrap(new GeoSpline2(o.Degree, points, o.Knots, rational ? o.Weights : null));
            }
            else
            {
                var points = new GeoPoint3[o.Count];

                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = new GeoPoint3(o.Points[3 * i], o.Points[3 * i + 1], o.Points[3 * i + 2]);
                }

                bool rational = o.Weights.Any(w => w != 1.0);

                return Wrap(new GeoSpline3(o.Degree, points, o.Knots, rational ? o.Weights : null));
            }
        }

        /// <summary>Wraps a spline in the plane.</summary>
        internal static SplineUnderTest Wrap(GeoSpline2 s)
        {
            var t = new SplineUnderTest
            {
                Dim = 2,
                Spline = s,
                Degree = s.Degree,
                Knots = s.Knots.ToArray(),
                Weights = s.Weights.ToArray(),
                ControlPoints = s.ControlPoints.SelectMany(Flat).ToArray(),
                PointAt = u => Flat(s.GetPointAtKnot(u)),
                DistanceTo = q => s.DistanceTo(P2(q)),
                DistanceFromPoint = q => P2(q).DistanceTo(s),
                ParameterAtPoint = q => s.GetKnotAtPoint(P2(q)),
                ClosestPoint = q => Flat(s.GetClosestPointOnBoundary(P2(q))),
                ShortestLine = q => Flat(s.GetShortestLineTo(P2(q))),
                ShortestLineFromPoint = q => Flat(P2(q).GetShortestLineTo(s)),
                IsPointOn = q => s.IsPointOn(P2(q)),
                Length = () => s.Length,
                Aabb = () =>
                {
                    GeoRectangle2 box = s.GetAabb();
                    return new[] { new[] { box.Center.X - box.Width * 0.5, box.Center.Y - box.Height * 0.5 }, new[] { box.Center.X + box.Width * 0.5, box.Center.Y + box.Height * 0.5 } };
                },
                Split = u => s.SplitAtKnot(u).Select(Wrap).ToArray(),
                Reverse = () => Wrap(s.Reverse()),
                Flatten = tol => s.Flatten(tol).Vertices.Select(Flat).ToArray(),
                ParameterAtLength = l => s.GetKnotAtParameter(s.GetParameterAtDistance(l)),
                Derivative = (u, order) => Flat(s.GetDerivativeAtKnot(u, order)),
                Tangent = u => Flat(s.GetTangentAtKnot(u)),
                Curvature = u => s.GetCurvatureAtKnot(u),
                DomainStart = () => s.DomainStart,
                DomainEnd = () => s.DomainEnd,
                IsRational = () => s.IsRational,
            };

            return t;
        }

        /// <summary>Wraps a spline in space.</summary>
        internal static SplineUnderTest Wrap(GeoSpline3 s)
        {
            var t = new SplineUnderTest
            {
                Dim = 3,
                Spline = s,
                Degree = s.Degree,
                Knots = s.Knots.ToArray(),
                Weights = s.Weights.ToArray(),
                ControlPoints = s.ControlPoints.SelectMany(Flat).ToArray(),
                PointAt = u => Flat(s.GetPointAtKnot(u)),
                DistanceTo = q => s.DistanceTo(P3(q)),
                DistanceFromPoint = q => P3(q).DistanceTo(s),
                ParameterAtPoint = q => s.GetKnotAtPoint(P3(q)),
                ClosestPoint = q => Flat(s.GetClosestPointOnBoundary(P3(q))),
                ShortestLine = q => Flat(s.GetShortestLineTo(P3(q))),
                ShortestLineFromPoint = q => Flat(P3(q).GetShortestLineTo(s)),
                IsPointOn = q => s.IsPointOn(P3(q)),
                Length = () => s.Length,
                Aabb = () =>
                {
                    GeoAabb3 box = s.GetAabb();
                    return new[] { Flat(box.Min), Flat(box.Max) };
                },
                Split = u => s.SplitAtKnot(u).Select(Wrap).ToArray(),
                Reverse = () => Wrap(s.Reverse()),
                Flatten = tol => s.Flatten(tol).Vertices.Select(Flat).ToArray(),
                ParameterAtLength = l => s.GetKnotAtParameter(s.GetParameterAtDistance(l)),
                Derivative = (u, order) => Flat(s.GetDerivativeAtKnot(u, order)),
                Tangent = u => Flat(s.GetTangentAtKnot(u)),
                Curvature = u => s.GetCurvatureAtKnot(u),
                DomainStart = () => s.DomainStart,
                DomainEnd = () => s.DomainEnd,
                IsRational = () => s.IsRational,
            };

            return t;
        }

        /// <summary>Gets the oracle of the numbers this spline holds.</summary>
        internal SplineOracle Oracle() => new SplineOracle(Dim, Degree, Knots, ControlPoints, Weights);

        internal static double Distance(double[] a, double[] b)
        {
            double sum = 0.0;

            for (int i = 0; i < a.Length; i++)
            {
                sum += (a[i] - b[i]) * (a[i] - b[i]);
            }

            return Math.Sqrt(sum);
        }
    }
}
