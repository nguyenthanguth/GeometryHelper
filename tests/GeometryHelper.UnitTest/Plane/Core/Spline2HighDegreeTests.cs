using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Splines of degree 11 to 25 in the plane, checked by brute force: a spline of random control points, plain and rational,
    /// has its closest points, crossings with a line and a circle, and crossings with another spline compared with what a
    /// dense sample of the curve shows.
    /// </summary>
    public class Spline2HighDegreeTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-6, 1E-6);

        private const string P = @"D:\GitHub\GeometryHelper\.claude\scratch\spl11\counts.txt";

        private static GeoSpline2 Random(int seed, int degree, int count, bool rational)
        {
            var random = new Random(seed);
            var points = new GeoPoint2[count];
            var weights = new double[count];

            for (int i = 0; i < count; i++)
            {
                points[i] = new GeoPoint2(
                    30.0 * i + 40.0 * (random.NextDouble() - 0.5),
                    100.0 * (random.NextDouble() - 0.5)
                );
                weights[i] = 0.5 + 2.0 * random.NextDouble();
            }

            int inner = count - degree - 1;
            var knots = new List<double>(Enumerable.Repeat(0.0, degree + 1));

            for (int i = 1; i <= inner; i++)
            {
                knots.Add(i / (double)(inner + 1));
            }

            knots.AddRange(Enumerable.Repeat(1.0, degree + 1));

            return new GeoSpline2(degree, points, knots, rational ? weights : null);
        }

        private static GeoPoint2[] Sample(GeoSpline2 spline, int count) =>
            Enumerable.Range(0, count + 1).Select(i => spline.GetPointAtParameter(i / (double)count)).ToArray();

        private static double Gap(GeoPoint2 a, GeoPoint2 b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        public static IEnumerable<object[]> Cases()
        {
            int seed = 1;

            foreach (int degree in new[] { 11, 14, 18, 21, 25 })
            {
                foreach (bool rational in new[] { false, true })
                {
                    yield return new object[] { seed++, degree, degree + 1, rational };
                    yield return new object[] { seed++, degree, degree + 9, rational };
                }
            }
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void ASplineOfDegree11To25_HasTheClosestPointABruteForceSampleFinds(
            int seed,
            int degree,
            int count,
            bool rational
        )
        {
            GeoSpline2 spline = Random(seed, degree, count, rational);
            GeoPoint2[] samples = Sample(spline, 20000);
            var random = new Random(seed * 7);

            for (int q = 0; q < 6; q++)
            {
                var query = new GeoPoint2(30.0 * count * random.NextDouble(), 200.0 * (random.NextDouble() - 0.5));
                double sampled = samples.Min(s => Gap(s, query));
                double found = spline.DistanceTo(query, Tol);
                GeoPoint2 near = spline.GetClosestPointOnBoundary(query);

                // The true distance is at most the sampled one, and no more than a chord's sag below it.
                Assert.InRange(found, sampled - 0.05, sampled + 1E-6);
                Assert.Equal(found, Gap(near, query), 6);
                Assert.True(spline.IsPointOn(near, new Tolerance(1E-5, 1E-5)), "the closest point is on the spline");
            }
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void ASplineOfDegree11To25_IsCrossedByALineWhereASampleChangesSide(
            int seed,
            int degree,
            int count,
            bool rational
        )
        {
            GeoSpline2 spline = Random(seed, degree, count, rational);
            GeoPoint2[] samples = Sample(spline, 20000);
            double top = 15.0 * count;
            var line = new GeoLine2(new GeoPoint2(-100, 5), new GeoPoint2(30.0 * count + 100, 5.0 + 0.3 * top));

            double Side(GeoPoint2 p) =>
                (line.EndPoint.X - line.StartPoint.X) * (p.Y - line.StartPoint.Y)
                - (line.EndPoint.Y - line.StartPoint.Y) * (p.X - line.StartPoint.X);

            int changes = 0;

            for (int i = 1; i < samples.Length; i++)
            {
                if ((Side(samples[i - 1]) < 0.0) != (Side(samples[i]) < 0.0))
                {
                    changes++;
                }
            }

            GeoPoint2[] crossings = spline.GetIntersections(line, Tol);

            Assert.Equal(changes, crossings.Length);

            foreach (GeoPoint2 crossing in crossings)
            {
                Assert.True(spline.IsPointOn(crossing, new Tolerance(1E-5, 1E-5)));
                Assert.InRange(Math.Abs(Side(crossing)) / Gap(line.StartPoint, line.EndPoint), 0.0, 1E-5);
            }
        }

        [Theory]
        [InlineData(11, false)]
        [InlineData(18, true)]
        [InlineData(25, false)]
        [InlineData(25, true)]
        public void ASplineOfDegree11To25_IsCrossedByACircleWhereASampleChangesSide(int degree, bool rational)
        {
            GeoSpline2 spline = Random(degree * 3, degree, degree + 6, rational);
            GeoPoint2[] samples = Sample(spline, 20000);
            var circle = new GeoCircle2(new GeoPoint2(15.0 * (degree + 6), 0), 40.0);
            int changes = 0;

            for (int i = 1; i < samples.Length; i++)
            {
                if (
                    (Gap(samples[i - 1], circle.Center) < circle.Radius)
                    != (Gap(samples[i], circle.Center) < circle.Radius)
                )
                {
                    changes++;
                }
            }

            GeoPoint2[] crossings = spline.GetIntersections(circle, Tol);

            Assert.Equal(changes, crossings.Length);

            foreach (GeoPoint2 crossing in crossings)
            {
                Assert.Equal(circle.Radius, Gap(crossing, circle.Center), 5);
            }
        }

        [Theory]
        [InlineData(11, 11, false)]
        [InlineData(12, 18, true)]
        [InlineData(25, 25, false)]
        [InlineData(25, 25, true)]
        public void TwoSplinesOfDegree11To25_CrossWhereTheirSamplesCross(int first, int second, bool rational)
        {
            GeoSpline2 a = Random(first, first, first + 7, rational);
            GeoSpline2 b = Random(second + 100, second, second + 7, rational);
            GeoPoint2[] sa = Sample(a, 2000);
            GeoPoint2[] sb = Sample(b, 2000);
            int crossed = 0;

            for (int i = 1; i < sa.Length; i++)
            {
                for (int j = 1; j < sb.Length; j++)
                {
                    if (Crosses(sa[i - 1], sa[i], sb[j - 1], sb[j]))
                    {
                        crossed++;
                    }
                }
            }

            GeoPoint2[] meets = a.GetIntersections(b, Tol);

            Assert.Equal(crossed, meets.Length);

            foreach (GeoPoint2 meet in meets)
            {
                Assert.True(a.IsPointOn(meet, new Tolerance(1E-5, 1E-5)));
                Assert.True(b.IsPointOn(meet, new Tolerance(1E-5, 1E-5)));
            }
        }

        private static bool Crosses(GeoPoint2 p, GeoPoint2 q, GeoPoint2 r, GeoPoint2 s)
        {
            double d1 = Cross(p, q, r);
            double d2 = Cross(p, q, s);
            double d3 = Cross(r, s, p);
            double d4 = Cross(r, s, q);

            return (d1 < 0.0) != (d2 < 0.0) && (d3 < 0.0) != (d4 < 0.0);
        }

        private static double Cross(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }
}
