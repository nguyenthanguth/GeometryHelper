using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Splines of degree 11 to 25, which AutoCAD allows and the library stopped short of at 10. The checks are against exact
    /// references: a cubic raised to a higher degree is the same curve with more control points, so its points, length,
    /// bounds, closest points and crossings must be the cubic's to a millionth of a millimetre; a quarter circle raised the
    /// same way must have the length of the arc; and a long spline of degree 20 must have the length of a polyline of 200 000
    /// chords laid along it.
    /// </summary>
    public class SplineHighDegreeTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-6, 1E-6);

        private static readonly GeoPoint3[] Arch =
        {
            new GeoPoint3(0, 0, 0),
            new GeoPoint3(100, 150, 60),
            new GeoPoint3(200, 150, -40),
            new GeoPoint3(300, 0, 0),
        };

        private static readonly double[] ArchWeights = { 1.0, 2.0, 0.5, 1.5 };

        private static double[] Clamped(int degree) =>
            Enumerable.Repeat(0.0, degree + 1).Concat(Enumerable.Repeat(1.0, degree + 1)).ToArray();

        /// <summary>
        /// Raises a Bézier curve (one span) to a higher degree exactly, in homogeneous coordinates: each step takes
        /// Q(i) = i / (n + 1) P(i - 1) + (1 - i / (n + 1)) P(i).
        /// </summary>
        private static GeoSpline3 Raised(GeoPoint3[] points, double[] weights, int degree)
        {
            var homogeneous = new List<double[]>();

            for (int i = 0; i < points.Length; i++)
            {
                double w = weights == null ? 1.0 : weights[i];
                homogeneous.Add(new[] { points[i].X * w, points[i].Y * w, points[i].Z * w, w });
            }

            while (homogeneous.Count - 1 < degree)
            {
                int n = homogeneous.Count - 1;
                var next = new List<double[]>();

                for (int i = 0; i <= n + 1; i++)
                {
                    var q = new double[4];
                    double a = i / (double)(n + 1);

                    for (int k = 0; k < 4; k++)
                    {
                        q[k] =
                            (i > 0 ? a * homogeneous[i - 1][k] : 0.0) + (i <= n ? (1.0 - a) * homogeneous[i][k] : 0.0);
                    }

                    next.Add(q);
                }

                homogeneous = next;
            }

            GeoPoint3[] raised = homogeneous
                .Select(h => new GeoPoint3(h[0] / h[3], h[1] / h[3], h[2] / h[3]))
                .ToArray();
            double[] raisedWeights = weights == null ? null : homogeneous.Select(h => h[3]).ToArray();

            return new GeoSpline3(degree, raised, Clamped(degree), raisedWeights);
        }

        private static GeoSpline3 Original(bool rational) =>
            new GeoSpline3(3, Arch, Clamped(3), rational ? ArchWeights : null);

        private static void SamePoint(GeoPoint3 expected, GeoPoint3 actual, string what)
        {
            Assert.True(expected.IsEqualTo(actual, Tol), what + ": expected " + expected + ", got " + actual);
        }

        [Theory]
        [InlineData(11, false)]
        [InlineData(11, true)]
        [InlineData(17, false)]
        [InlineData(17, true)]
        [InlineData(25, false)]
        [InlineData(25, true)]
        public void ACubicRaisedPastTen_IsTheSameCurve_ItsPointsTangentsAndLengthTheCubics(int degree, bool rational)
        {
            GeoSpline3 cubic = Original(rational);
            GeoSpline3 raised = Raised(Arch, rational ? ArchWeights : null, degree);

            Assert.Equal(degree, raised.Degree);
            Assert.Equal(rational, raised.IsRational);

            for (int i = 0; i <= 40; i++)
            {
                double t = i / 40.0;
                SamePoint(cubic.GetPointAtParameter(t), raised.GetPointAtParameter(t), "point at " + t);
                Assert.True(
                    cubic
                        .GetTangentAtParameter(t)
                        .IsEqualTo(raised.GetTangentAtParameter(t), new Tolerance(1E-6, 1E-6)),
                    "tangent at " + t
                );
            }

            Assert.Equal(cubic.Length, raised.Length, 6);
            Assert.Equal(cubic.GetDistanceAtParameter(0.3), raised.GetDistanceAtParameter(0.3), 6);
            Assert.Equal(
                cubic.GetParameterAtDistance(0.4 * cubic.Length),
                raised.GetParameterAtDistance(0.4 * cubic.Length),
                8
            );
        }

        [Theory]
        [InlineData(11, false)]
        [InlineData(25, false)]
        [InlineData(25, true)]
        public void ACubicRaisedPastTen_HasTheCubicsBounds(int degree, bool rational)
        {
            GeoAabb3 cubic = Original(rational).GetAabb();
            GeoAabb3 raised = Raised(Arch, rational ? ArchWeights : null, degree).GetAabb();

            SamePoint(cubic.Min, raised.Min, "minimum of the box");
            SamePoint(cubic.Max, raised.Max, "maximum of the box");
        }

        [Theory]
        [InlineData(11, false)]
        [InlineData(17, true)]
        [InlineData(25, false)]
        [InlineData(25, true)]
        public void ACubicRaisedPastTen_HasTheCubicsClosestPoints(int degree, bool rational)
        {
            GeoSpline3 cubic = Original(rational);
            GeoSpline3 raised = Raised(Arch, rational ? ArchWeights : null, degree);
            GeoPoint3[] queries =
            {
                new GeoPoint3(150, 300, 10),
                new GeoPoint3(-30, -20, 15),
                new GeoPoint3(310, 40, -20),
                new GeoPoint3(120, 90, 30),
            };

            foreach (GeoPoint3 query in queries)
            {
                SamePoint(
                    cubic.GetClosestPointOnBoundary(query),
                    raised.GetClosestPointOnBoundary(query),
                    "closest to " + query
                );
                Assert.Equal(cubic.DistanceTo(query, Tol), raised.DistanceTo(query, Tol), 6);
            }
        }

        [Theory]
        [InlineData(11, false)]
        [InlineData(25, false)]
        [InlineData(25, true)]
        public void ACubicRaisedPastTen_IsCrossedByAPlaneAndALineWhereTheCubicIs(int degree, bool rational)
        {
            GeoSpline3 cubic = Original(rational);
            GeoSpline3 raised = Raised(Arch, rational ? ArchWeights : null, degree);
            var plane = new GeoPlane3(new GeoPoint3(0, 100, 0), new GeoVector3(0, 1, 0));
            var line = new GeoLine3(new GeoPoint3(-50, 80, -100), new GeoPoint3(350, 80, 100));

            GeoPoint3[] before = cubic.GetIntersections(plane, Tol).OrderBy(p => p.X).ToArray();
            GeoPoint3[] after = raised.GetIntersections(plane, Tol).OrderBy(p => p.X).ToArray();
            Assert.Equal(2, before.Length);
            Assert.Equal(before.Length, after.Length);

            for (int i = 0; i < before.Length; i++)
            {
                SamePoint(before[i], after[i], "plane crossing " + i);
            }

            Assert.Equal(cubic.DistanceTo(line, Tol), raised.DistanceTo(line, Tol), 6);
        }

        [Theory]
        [InlineData(11)]
        [InlineData(17)]
        [InlineData(25)]
        public void ACubicRaisedPastTen_MeetsAnotherSplineWhereTheCubicDoes(int degree)
        {
            GeoSpline3 other = new GeoSpline3(
                3,
                new[]
                {
                    new GeoPoint3(0, 100, 0),
                    new GeoPoint3(100, -50, 20),
                    new GeoPoint3(200, -50, -60),
                    new GeoPoint3(300, 100, 0),
                },
                Clamped(3)
            );
            GeoSpline3 cubic = Original(false);
            GeoSpline3 raised = Raised(Arch, null, degree);

            double gap = cubic.DistanceTo(other, Tol);
            Assert.Equal(gap, raised.DistanceTo(other, Tol), 6);
            Assert.Equal(gap, other.DistanceTo(raised, Tol), 6);
        }

        [Theory]
        [InlineData(11)]
        [InlineData(17)]
        [InlineData(25)]
        public void AQuarterCircleRaisedPastTen_HasTheLengthOfTheArc_AndIsAtTheRadius(int degree)
        {
            double r = 100.0;
            double s = Math.Sqrt(0.5);
            var points = new[] { new GeoPoint3(r, 0, 0), new GeoPoint3(r, r, 0), new GeoPoint3(0, r, 0) };
            GeoSpline3 arc = Raised(points, new[] { 1.0, s, 1.0 }, degree);

            Assert.Equal(Math.PI * r / 2.0, arc.Length, 9);

            for (int i = 0; i <= 20; i++)
            {
                GeoPoint3 p = arc.GetPointAtParameter(i / 20.0);
                Assert.Equal(r, Math.Sqrt(p.X * p.X + p.Y * p.Y), 9);
            }

            GeoPoint3 near = arc.GetClosestPointOnBoundary(new GeoPoint3(200, 200, 0));
            SamePoint(new GeoPoint3(r * s, r * s, 0), near, "closest to (200, 200)");

            GeoPoint3[] crossings = arc.GetIntersections(
                new GeoCircle3(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 80.0),
                Tol
            );
            Assert.Empty(crossings);
            crossings = arc.GetIntersections(new GeoCircle3(new GeoPoint3(r, 0, 0), GeoVector3.ZAxis, r), Tol);
            Assert.Single(crossings);
            SamePoint(new GeoPoint3(r * 0.5, r * Math.Sqrt(0.75), 0), crossings[0], "the circles meet");
        }

        [Theory]
        [InlineData(20, false)]
        [InlineData(20, true)]
        [InlineData(25, true)]
        public void ALongSplineOfDegreeTwentyOrMore_HasTheLengthOfAFinePolylineLaidAlongIt(int degree, bool rational)
        {
            const int Count = 32;
            var points = new GeoPoint3[Count];
            var weights = new double[Count];

            for (int i = 0; i < Count; i++)
            {
                double a = i * 0.45;
                points[i] = new GeoPoint3(
                    40.0 * i + 15.0 * Math.Sin(a * 2.0),
                    60.0 * Math.Sin(a),
                    25.0 * Math.Cos(a * 1.3)
                );
                weights[i] = 1.0 + 0.8 * Math.Sin(i * 1.7);
            }

            int inner = Count - degree - 1;
            var knots = new List<double>(Enumerable.Repeat(0.0, degree + 1));

            for (int i = 1; i <= inner; i++)
            {
                knots.Add(i / (double)(inner + 1));
            }

            knots.AddRange(Enumerable.Repeat(1.0, degree + 1));
            var spline = new GeoSpline3(degree, points, knots, rational ? weights : null);

            const int Chords = 200000;
            double polyline = 0.0;
            GeoPoint3 last = spline.GetPointAtParameter(0.0);

            for (int i = 1; i <= Chords; i++)
            {
                GeoPoint3 next = spline.GetPointAtParameter(i / (double)Chords);
                polyline += Math.Sqrt(
                    (next.X - last.X) * (next.X - last.X)
                        + (next.Y - last.Y) * (next.Y - last.Y)
                        + (next.Z - last.Z) * (next.Z - last.Z)
                );
                last = next;
            }

            Assert.InRange(spline.Length, polyline, polyline * (1.0 + 1E-9));

            // The parameter and the distance are each other's inverse along it.
            double at = spline.GetParameterAtDistance(0.37 * spline.Length);
            Assert.Equal(0.37 * spline.Length, spline.GetDistanceAtParameter(at), 6);

            // A point of the spline is its own closest point, and the box holds every sample.
            GeoPoint3 sample = spline.GetPointAtParameter(0.61);
            SamePoint(sample, spline.GetClosestPointOnBoundary(sample), "closest to a point of it");
            GeoAabb3 box = spline.GetAabb();

            for (int i = 0; i <= 2000; i++)
            {
                GeoPoint3 p = spline.GetPointAtParameter(i / 2000.0);
                Assert.True(
                    p.X >= box.Min.X - 1E-6
                        && p.X <= box.Max.X + 1E-6
                        && p.Y >= box.Min.Y - 1E-6
                        && p.Y <= box.Max.Y + 1E-6
                        && p.Z >= box.Min.Z - 1E-6
                        && p.Z <= box.Max.Z + 1E-6,
                    "outside the box at " + i
                );
            }
        }

        [Theory]
        [InlineData(17, 1)]
        [InlineData(21, 2)]
        [InlineData(25, 3)]
        [InlineData(25, 4)]
        public void ARationalSplineOfDegree17To25ThatLoopsBackOnItself_HasTheLengthOfAFinePolyline(int degree, int seed)
        {
            // Control points scattered through a box turn the derivative of the spline back on itself, which is where the speed
            // breaks are looked for: the products there are of the highest degree, 122 at degree 25.
            var random = new Random(seed);
            int count = degree + 1 + 2 * seed;
            var points = new GeoPoint3[count];
            var weights = new double[count];

            for (int i = 0; i < count; i++)
            {
                points[i] = new GeoPoint3(
                    200.0 * random.NextDouble(),
                    200.0 * random.NextDouble(),
                    200.0 * random.NextDouble()
                );
                weights[i] = 0.3 + 3.0 * random.NextDouble();
            }

            int inner = count - degree - 1;
            var knots = new List<double>(Enumerable.Repeat(0.0, degree + 1));

            for (int i = 1; i <= inner; i++)
            {
                knots.Add(i / (double)(inner + 1));
            }

            knots.AddRange(Enumerable.Repeat(1.0, degree + 1));
            var spline = new GeoSpline3(degree, points, knots, weights);

            const int Chords = 400000;
            double polyline = 0.0;
            GeoPoint3 last = spline.GetPointAtParameter(0.0);

            for (int i = 1; i <= Chords; i++)
            {
                GeoPoint3 next = spline.GetPointAtParameter(i / (double)Chords);
                polyline += Math.Sqrt(
                    (next.X - last.X) * (next.X - last.X)
                        + (next.Y - last.Y) * (next.Y - last.Y)
                        + (next.Z - last.Z) * (next.Z - last.Z)
                );
                last = next;
            }

            // The chords lie inside the curve, a share of 1E-9 short of it at most for chords this fine.
            Assert.InRange(spline.Length, polyline, polyline * (1.0 + 1E-8));
        }

        [Fact]
        public void TheDerivativesOfABezierCurveOfDegree25_AreTheForwardDifferencesOfItsControlPointsAtTheStart()
        {
            // At the start of a Bézier curve the k-th derivative is n! / (n - k)! times the k-th forward difference of the points,
            // and with whole coordinates the differences are exact.
            const int N = 25;
            GeoPoint3[] points = Enumerable
                .Range(0, N + 1)
                .Select(i => new GeoPoint3(10 * i, (i * i) % 17 * 10, (i * 7) % 11 * 10))
                .ToArray();
            var spline = new GeoSpline3(N, points, Clamped(N));

            for (int k = 1; k <= N; k++)
            {
                double factor = 1.0;

                for (int i = 0; i < k; i++)
                {
                    factor *= N - i;
                }

                var expected = new double[3];

                for (int i = 0; i <= k; i++)
                {
                    double sign = (k - i) % 2 == 0 ? 1.0 : -1.0;
                    double choose = 1.0;

                    for (int j = 1; j <= i; j++)
                    {
                        choose = choose * (k - j + 1) / j;
                    }

                    expected[0] += sign * choose * points[i].X;
                    expected[1] += sign * choose * points[i].Y;
                    expected[2] += sign * choose * points[i].Z;
                }

                GeoVector3 derivative = spline.GetDerivativeAtKnot(0.0, k);
                double size =
                    Math.Abs(factor) * (Math.Abs(expected[0]) + Math.Abs(expected[1]) + Math.Abs(expected[2]) + 1.0);

                Assert.InRange(derivative.X - factor * expected[0], -1E-9 * size, 1E-9 * size);
                Assert.InRange(derivative.Y - factor * expected[1], -1E-9 * size, 1E-9 * size);
                Assert.InRange(derivative.Z - factor * expected[2], -1E-9 * size, 1E-9 * size);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetDerivativeAtKnot(0.0, N + 1));
        }

        [Fact]
        public void ASplineOfDegree25_IsMadeFitAndFromControlPoints_ADegreeOf26IsRefused()
        {
            GeoPoint3[] points = Enumerable
                .Range(0, 40)
                .Select(i => new GeoPoint3(10.0 * i, 30.0 * Math.Sin(i * 0.4), i))
                .ToArray();

            Assert.Equal(25, GeoSpline3.FromControlPoints(25, points).Degree);
            Assert.Equal(25, GeoSpline3.Interpolate(points, 25).Degree);
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSpline3.FromControlPoints(26, points));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline3(26, points, Clamped(26)));
            Assert.Equal(25, GeoSpline2.FromControlPoints(25, points.Select(p => new GeoPoint2(p.X, p.Y))).Degree);
        }
    }
}
