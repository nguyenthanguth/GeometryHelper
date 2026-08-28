using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Core
{
    /// <summary>
    /// Whether an intersection search in the plane finds everything there is to find.
    /// <para>
    /// The earlier sweeps checked that every reported crossing lies on both shapes, which catches a wrong
    /// answer but not a missing one — a method that always came back empty would have passed them all.
    /// Here the segment is walked in small steps and the passages are counted from the outside; the
    /// search has to report at least that many.
    /// </para>
    /// <para>
    /// Only a stretch the walk stays inside for a good many samples counts. A segment that grazes a
    /// corner passes through one point, but the tolerance band around it is wide enough for a sample or
    /// two to fall inside, and counting that as two passages would demand a crossing that is not there.
    /// </para>
    /// </summary>
    public class IntersectionCompletenessTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        private const int Steps = 2000;
        private const int Sustained = 8;

        private static GeoPoint2[] Star(Random r, int n, double dx = 0, double dy = 0)
        {
            var pts = new GeoPoint2[n];
            for (int i = 0; i < n; i++)
            {
                double a = 2 * Math.PI * i / n;
                double rad = 2 + r.NextDouble() * 6;
                pts[i] = new GeoPoint2(Math.Round(rad * Math.Cos(a)) + dx, Math.Round(rad * Math.Sin(a)) + dy);
            }
            return pts;
        }

        private static int CountSustainedCrossings(GeoLine2 line, Func<GeoPoint2, bool> holds)
        {
            bool[] inside = new bool[Steps + 1];

            for (int i = 0; i <= Steps; i++)
            {
                inside[i] = holds(Parametrization2.GetPointAtParameter(line, (double)i / Steps));
            }

            int crossings = 0;
            int index = 0;

            while (index <= Steps)
            {
                if (!inside[index]) { index++; continue; }

                int first = index;
                while (index <= Steps && inside[index]) { index++; }
                int last = index - 1;

                if (last - first + 1 < Sustained) { continue; }

                if (first > 0) { crossings++; }
                if (last < Steps) { crossings++; }
            }

            return crossings;
        }

        [Fact]
        public void EveryTimeASegmentEntersOrLeavesACircleThereIsACrossing()
        {
            Random rng = new Random(7001);
            int cases = 0, withCrossings = 0, short_ = 0;

            for (int t = 0; t < 600; t++)
            {
                var circle = new GeoCircle2(new GeoPoint2(rng.Next(-6, 7), rng.Next(-6, 7)), 1 + rng.Next(1, 6));

                // Thrown through the middle, so entering and leaving is the common case.
                var line = new GeoLine2(
                    new GeoPoint2(circle.Center.X + rng.Next(-9, 10), circle.Center.Y + rng.Next(-9, 10)),
                    new GeoPoint2(circle.Center.X + rng.Next(-9, 10), circle.Center.Y + rng.Next(-9, 10)));

                if (line.Length < 1E-6) { continue; }

                cases++;

                int crossings = CountSustainedCrossings(line, p => p.DistanceTo(circle.Center) < circle.Radius);
                if (crossings > 0) { withCrossings++; }

                if (Intersection2.GetIntersections(circle, line, Tol).Length < crossings) { short_++; }
            }

            Assert.True(cases > 400, $"only {cases} segments");
            Assert.True(withCrossings > 150, $"only {withCrossings} segments entered or left a circle");
            Assert.Equal(0, short_);
        }

        [Fact]
        public void EveryTimeASegmentEntersOrLeavesARectangleThereIsACrossing()
        {
            Random rng = new Random(7002);
            int cases = 0, withCrossings = 0, short_ = 0;

            for (int t = 0; t < 600; t++)
            {
                var rect = new GeoRectangle2(new GeoPoint2(rng.Next(-6, 7), rng.Next(-6, 7)), 2 + rng.Next(1, 6), 2 + rng.Next(1, 6));

                var line = new GeoLine2(
                    new GeoPoint2(rect.Center.X + rng.Next(-9, 10), rect.Center.Y + rng.Next(-9, 10)),
                    new GeoPoint2(rect.Center.X + rng.Next(-9, 10), rect.Center.Y + rng.Next(-9, 10)));

                if (line.Length < 1E-6) { continue; }

                cases++;

                int crossings = CountSustainedCrossings(line, p => Containment2.Contains(rect, p, Tol));
                if (crossings > 0) { withCrossings++; }

                if (Intersection2.GetIntersections(rect, line, Tol).Length < crossings) { short_++; }
            }

            Assert.True(cases > 400, $"only {cases} segments");
            Assert.True(withCrossings > 150, $"only {withCrossings} segments entered or left a rectangle");
            Assert.Equal(0, short_);
        }

        [Fact]
        public void EveryTimeASegmentEntersOrLeavesAPolygonThereIsACrossing()
        {
            Random rng = new Random(7003);
            int cases = 0, withCrossings = 0, short_ = 0, shortChain = 0;

            for (int t = 0; t < 400; t++)
            {
                GeoPolygon2 poly;
                GeoPolyline2 chain;
                try
                {
                    GeoPoint2[] profile = Star(rng, 4 + rng.Next(5));
                    poly = new GeoPolygon2(profile);

                    // The same outline as an open chain, closed by hand so the two describe one boundary.
                    var closed = new List<GeoPoint2>(profile) { profile[0] };
                    chain = new GeoPolyline2(closed);
                }
                catch (ArgumentException) { continue; }

                var line = new GeoLine2(
                    new GeoPoint2(rng.Next(-12, 13), rng.Next(-12, 13)),
                    new GeoPoint2(rng.Next(-12, 13), rng.Next(-12, 13)));

                if (line.Length < 1E-6) { continue; }

                cases++;

                int crossings = CountSustainedCrossings(line, p => Containment2.Contains(poly, p, Tol));
                if (crossings > 0) { withCrossings++; }

                if (Intersection2.GetIntersections(poly, line, Tol).Length < crossings) { short_++; }

                // The outline as a chain must find the same crossings the outline as a region does.
                if (Intersection2.GetIntersections(chain, line, Tol).Length < crossings) { shortChain++; }
            }

            Assert.True(cases > 250, $"only {cases} segments");
            Assert.True(withCrossings > 60, $"only {withCrossings} segments entered or left a polygon");
            Assert.Equal(0, short_);
            Assert.Equal(0, shortChain);
        }

        [Fact]
        public void TwoCirclesThatOverlapAlwaysMeet()
        {
            Random rng = new Random(7004);
            int overlapping = 0, missed = 0, offRim = 0;

            for (int t = 0; t < 2000; t++)
            {
                var one = new GeoCircle2(new GeoPoint2(rng.Next(-8, 9), rng.Next(-8, 9)), 1 + rng.Next(1, 7));
                var two = new GeoCircle2(new GeoPoint2(rng.Next(-8, 9), rng.Next(-8, 9)), 1 + rng.Next(1, 7));

                double apart = one.Center.DistanceTo(two.Center);

                // Two rims cross when the centres are further apart than the difference of the radii and
                // closer than their sum. Kept well clear of both limits so no tangency is involved.
                double lower = Math.Abs(one.Radius - two.Radius);
                double upper = one.Radius + two.Radius;

                if (apart <= lower + 1E-3 || apart >= upper - 1E-3) { continue; }

                overlapping++;

                GeoPoint2[] found = Intersection2.GetIntersections(one, two, Tol);

                // Two rims that properly cross meet at two points.
                if (found.Length < 2) { missed++; continue; }

                foreach (GeoPoint2 hit in found)
                {
                    if (Math.Abs(hit.DistanceTo(one.Center) - one.Radius) > 1E-6) { offRim++; }
                    if (Math.Abs(hit.DistanceTo(two.Center) - two.Radius) > 1E-6) { offRim++; }
                }
            }

            Assert.True(overlapping > 300, $"only {overlapping} pairs properly crossed");
            Assert.Equal(0, missed);
            Assert.Equal(0, offRim);
        }

        [Fact]
        public void TwoSegmentsThatCrossAlwaysMeet()
        {
            Random rng = new Random(7005);
            int crossing = 0, missed = 0, offSegment = 0, nearlyParallel = 0, inconsistent = 0;

            for (int t = 0; t < 4000; t++)
            {
                var a = new GeoLine2(
                    new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10)),
                    new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10)));

                var b = new GeoLine2(
                    new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10)),
                    new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10)));

                if (a.Length < 1E-6 || b.Length < 1E-6) { continue; }

                // Each segment strictly straddles the line the other runs along, worked out from the
                // signs of the cross products rather than from anything the library computes.
                double d1 = Side(a, b.StartPoint);
                double d2 = Side(a, b.EndPoint);
                double d3 = Side(b, a.StartPoint);
                double d4 = Side(b, a.EndPoint);

                if (!(d1 * d2 < 0 && d3 * d4 < 0)) { continue; }
                if (Math.Min(Math.Min(Math.Abs(d1), Math.Abs(d2)), Math.Min(Math.Abs(d3), Math.Abs(d4))) < 1E-3) { continue; }

                // Two segments can cross geometrically and still be parallel as far as this library is
                // concerned: the default angular threshold is a whole degree, and the meeting point of
                // two segments that close together is not a number worth reporting. Those are refused on
                // purpose, so what is checked for them is that the refusal is the parallel one.
                if (Parallel2.IsParallel(a.Direction, b.Direction, Tol))
                {
                    nearlyParallel++;

                    if (Intersection2.TryIntersectWith(a, b, out GeoPoint2 _, Tol)) { inconsistent++; }
                    continue;
                }

                crossing++;

                if (!Intersection2.TryIntersectWith(a, b, out GeoPoint2 hit, Tol)) { missed++; continue; }

                if (!Containment2.IsPointOn(a, hit, Tol) || !Containment2.IsPointOn(b, hit, Tol)) { offSegment++; }
            }

            Assert.True(crossing > 300, $"only {crossing} pairs properly crossed");
            Assert.Equal(0, missed);
            Assert.Equal(0, offSegment);

            // The refusal has to be the parallel one and nothing else: a pair refused here must be a pair
            // Parallel2 also calls parallel, or the two are disagreeing about the same question.
            Assert.True(nearlyParallel > 0, "no crossing pair was near enough to parallel to be refused");
            Assert.Equal(0, inconsistent);
        }

        /// <summary>Twice the signed area of the triangle the segment makes with a point.</summary>
        private static double Side(GeoLine2 line, GeoPoint2 point)
        {
            return (line.EndPoint.X - line.StartPoint.X) * (point.Y - line.StartPoint.Y)
                 - (line.EndPoint.Y - line.StartPoint.Y) * (point.X - line.StartPoint.X);
        }
    }
}
