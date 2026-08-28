using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Whether an intersection search finds everything there is to find.
    /// <para>
    /// The sweeps written earlier checked that every reported crossing really lies on both shapes. That
    /// catches a wrong answer but not a missing one: a method that always came back empty would have
    /// passed every one of them. What is checked here is the other direction. The subject is walked in
    /// small steps and the crossings are counted from the outside, by watching where the walk passes from
    /// one side of the other shape to the other; the search has to report at least that many.
    /// </para>
    /// <para>
    /// At least, rather than exactly, because a walk cannot see a crossing that touches and turns back —
    /// it goes in and out between two samples, or grazes without ever going in. Those are crossings the
    /// search may legitimately report and the walk cannot count, so counting them as failures would be
    /// wrong. Everything the walk does see must be found.
    /// </para>
    /// </summary>
    public class IntersectionCompletenessTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        private const int Steps = 800;

        private static GeoPoint3[] Star(Random r, int n)
        {
            var pts = new GeoPoint3[n];
            for (int i = 0; i < n; i++)
            {
                double a = 2 * Math.PI * i / n;
                double rad = 2 + r.NextDouble() * 6;
                pts[i] = new GeoPoint3(Math.Round(rad * Math.Cos(a)), Math.Round(rad * Math.Sin(a)), 0);
            }
            return pts;
        }

        private static GeoSolid3 Prism(GeoPoint3[] p, double h)
        {
            var faces = new List<GeoFace3>();
            var top = new GeoPoint3[p.Length];
            var bottom = new GeoPoint3[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                top[i] = new GeoPoint3(p[i].X, p[i].Y, p[i].Z + h);
                bottom[i] = p[p.Length - 1 - i];
            }
            faces.Add(new GeoFace3(new GeoPolygon3(bottom)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));
            for (int i = 0; i < p.Length; i++)
            {
                int j = (i + 1) % p.Length;
                faces.Add(new GeoFace3(new GeoPolygon3(p[i], p[j], top[j], top[i])));
            }
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// Counts the crossings a walk along a segment can actually vouch for.
        /// </summary>
        /// <remarks>
        /// Counting every change of state would count a graze twice. A segment that touches the corner of
        /// a box passes through one point, but the tolerance band around that point is wide enough for a
        /// sample or two to fall inside it, so the walk sees out, in, out and would demand two crossings
        /// where there is one. Only a stretch the walk stays inside for a good many samples is a real
        /// passage, and each end of such a stretch is a crossing unless it runs off the end of the
        /// segment, where there is nothing to cross.
        /// </remarks>
        private static int CountSustainedCrossings(GeoLine3 line, Func<GeoPoint3, bool> holds)
        {
            bool[] inside = new bool[Steps + 1];

            for (int i = 0; i <= Steps; i++)
            {
                inside[i] = holds(Parametrization3.GetPointAtParameter(line, (double)i / Steps));
            }

            const int Sustained = 8;

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
        public void ASegmentCrossingAPlaneIsAlwaysFound()
        {
            Random rng = new Random(1001);
            int crossings = 0, missed = 0;

            for (int t = 0; t < 2000; t++)
            {
                var line = new GeoLine3(
                    new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10)),
                    new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10)));

                if (line.Length < 1E-6) { continue; }

                var plane = new GeoPlane3(
                    new GeoPoint3(rng.Next(-5, 6), rng.Next(-5, 6), rng.Next(-5, 6)),
                    new GeoVector3(rng.Next(-3, 4) + 0.3, rng.Next(-3, 4) + 0.7, rng.Next(-3, 4) + 0.1));

                double atStart = plane.SignedDistanceTo(line.StartPoint);
                double atEnd = plane.SignedDistanceTo(line.EndPoint);

                // Strictly on opposite sides: the segment has to pass through, and the crossing is not a
                // graze that could be argued away.
                if (atStart > Tol.EqualPlanar && atEnd < -Tol.EqualPlanar ||
                    atStart < -Tol.EqualPlanar && atEnd > Tol.EqualPlanar)
                {
                    crossings++;

                    if (!Intersection3.TryIntersectWith(line, plane, out GeoPoint3 _, Tol)) { missed++; }
                }
            }

            Assert.True(crossings > 500, $"only {crossings} segments actually straddled a plane");
            Assert.Equal(0, missed);
        }

        [Fact]
        public void ASegmentCrossingATriangleIsAlwaysFound()
        {
            Random rng = new Random(2002);
            int crossings = 0, missed = 0;

            for (int t = 0; t < 4000; t++)
            {
                var triangle = new GeoTriangle3(
                    new GeoPoint3(rng.Next(-6, 7), rng.Next(-6, 7), 0),
                    new GeoPoint3(rng.Next(-6, 7), rng.Next(-6, 7), 0),
                    new GeoPoint3(rng.Next(-6, 7), rng.Next(-6, 7), 0));

                if (triangle.IsDegenerate(Tol)) { continue; }

                // Aimed through the triangle so that crossings are common.
                GeoPoint3 target = triangle.Centroid;
                var line = new GeoLine3(
                    new GeoPoint3(target.X + rng.Next(-3, 4), target.Y + rng.Next(-3, 4), -5),
                    new GeoPoint3(target.X + rng.Next(-3, 4), target.Y + rng.Next(-3, 4), 5));

                GeoPlane3 plane = triangle.GetPlane();

                double atStart = plane.SignedDistanceTo(line.StartPoint);
                double atEnd = plane.SignedDistanceTo(line.EndPoint);

                if (!(atStart > Tol.EqualPlanar && atEnd < -Tol.EqualPlanar ||
                      atStart < -Tol.EqualPlanar && atEnd > Tol.EqualPlanar))
                {
                    continue;
                }

                // Where the segment meets the plane, worked out independently of the triangle test.
                if (!Intersection3.TryIntersectWith(line, plane, out GeoPoint3 onPlane, Tol)) { continue; }

                // Comfortably inside the triangle, so the answer cannot turn on a tolerance.
                if (Distance3.DistanceTo(triangle, onPlane) > 1E-6) { continue; }

                bool nearAnEdge = false;
                for (int e = 0; e < 3; e++)
                {
                    if (Distance3.DistanceTo(triangle.GetEdgeAt(e), onPlane, Tol) < 1E-3) { nearAnEdge = true; }
                }
                if (nearAnEdge) { continue; }

                crossings++;

                if (!Intersection3.TryIntersectWith(line, triangle, out GeoPoint3 _, Tol)) { missed++; }
            }

            Assert.True(crossings > 300, $"only {crossings} segments actually pierced a triangle");
            Assert.Equal(0, missed);
        }

        [Fact]
        public void EveryTimeASegmentEntersOrLeavesASolidThereIsACrossing()
        {
            Random rng = new Random(3003);
            int cases = 0, withTransitions = 0, totalTransitions = 0, short_ = 0;

            for (int t = 0; t < 60; t++)
            {
                GeoSolid3 body;
                try { body = Prism(Star(rng, 4 + rng.Next(5)), 6.0); }
                catch (ArgumentException) { continue; }

                for (int q = 0; q < 4; q++)
                {
                    var line = new GeoLine3(
                        new GeoPoint3(rng.Next(-14, 15), rng.Next(-14, 15), rng.Next(-2, 9)),
                        new GeoPoint3(rng.Next(-14, 15), rng.Next(-14, 15), rng.Next(-2, 9)));

                    if (line.Length < 1E-6) { continue; }

                    cases++;

                    int crossings = CountSustainedCrossings(line, p => Containment3.Locate(body, p, Tol) == PointLocation.Inside);

                    if (crossings > 0) { withTransitions++; totalTransitions += crossings; }

                    GeoPoint3[] found = Intersection3.GetIntersections(line, body, Tol);

                    // Every passage from inside to outside or back has to have been noticed.
                    if (found.Length < crossings) { short_++; }
                }
            }

            Assert.True(cases > 180, $"only {cases} segments");
            Assert.True(withTransitions > 25, $"only {withTransitions} segments entered or left a body");
            Assert.True(totalTransitions > 40, $"only {totalTransitions} passages in all");
            Assert.Equal(0, short_);
        }

        [Fact]
        public void EveryTimeASegmentEntersOrLeavesABoxThereIsACrossing()
        {
            Random rng = new Random(4004);
            int cases = 0, withTransitions = 0, shortObb = 0, shortAabb = 0;

            for (int t = 0; t < 400; t++)
            {
                var lo = new GeoPoint3(rng.Next(-8, 3), rng.Next(-8, 3), rng.Next(-8, 3));
                var hi = new GeoPoint3(lo.X + 1 + rng.Next(6), lo.Y + 1 + rng.Next(6), lo.Z + 1 + rng.Next(6));

                var aabb = new GeoAabb3(lo, hi);
                GeoObb3 obb = aabb.ToObb();

                GeoPoint3 middle = aabb.Center;

                for (int q = 0; q < 3; q++)
                {
                    // Thrown through the middle of the box, so that entering and leaving it is the common
                    // case rather than a rare one.
                    var line = new GeoLine3(
                        new GeoPoint3(middle.X + rng.Next(-9, 10), middle.Y + rng.Next(-9, 10), middle.Z + rng.Next(-9, 10)),
                        new GeoPoint3(middle.X + rng.Next(-9, 10), middle.Y + rng.Next(-9, 10), middle.Z + rng.Next(-9, 10)));

                    if (line.Length < 1E-6) { continue; }

                    cases++;

                    int crossings = CountSustainedCrossings(line, p => aabb.Contains(p, Tol));
                    if (crossings > 0) { withTransitions++; }

                    if (Intersection3.GetIntersections(line, aabb, Tol).Length < crossings) { shortAabb++; }
                    if (Intersection3.GetIntersections(line, obb, Tol).Length < crossings) { shortObb++; }
                }
            }

            Assert.True(cases > 800, $"only {cases} segments");
            Assert.True(withTransitions > 100, $"only {withTransitions} segments entered or left a box");
            Assert.Equal(0, shortAabb);
            Assert.Equal(0, shortObb);
        }

        [Fact]
        public void ASegmentPiercingAFaceIsAlwaysFound()
        {
            Random rng = new Random(5005);
            int crossings = 0, missedPolygon = 0, missedFace = 0;

            for (int t = 0; t < 1500; t++)
            {
                GeoPolygon3 plate;
                try { plate = new GeoPolygon3(Star(rng, 4 + rng.Next(5))); }
                catch (ArgumentException) { continue; }

                GeoPoint3 target = plate.Centroid;
                var line = new GeoLine3(
                    new GeoPoint3(target.X + rng.Next(-4, 5), target.Y + rng.Next(-4, 5), -5),
                    new GeoPoint3(target.X + rng.Next(-4, 5), target.Y + rng.Next(-4, 5), 5));

                if (!Intersection3.TryIntersectWith(line, plate.GetPlane(), out GeoPoint3 onPlane, Tol)) { continue; }

                // Comfortably inside the outline, well clear of every edge.
                if (Containment3.Locate(plate, onPlane, Tol) != PointLocation.Inside) { continue; }
                if (Projection3.ProjectToPolygonBoundary(plate, onPlane, Tol).DistanceTo(onPlane) < 1E-3) { continue; }

                crossings++;

                if (!Intersection3.TryIntersectWith(line, plate, out GeoPoint3 _, Tol)) { missedPolygon++; }
                if (!Intersection3.TryIntersectWith(line, new GeoFace3(plate), out GeoPoint3 _, Tol)) { missedFace++; }
            }

            Assert.True(crossings > 200, $"only {crossings} segments actually pierced a plate");
            Assert.Equal(0, missedPolygon);
            Assert.Equal(0, missedFace);
        }

        [Fact]
        public void TwoPlanesThatAreNotParallelAlwaysMeet()
        {
            Random rng = new Random(6006);
            int pairs = 0, missed = 0, offLine = 0;

            for (int t = 0; t < 2000; t++)
            {
                var one = new GeoPlane3(
                    new GeoPoint3(rng.Next(-6, 7), rng.Next(-6, 7), rng.Next(-6, 7)),
                    new GeoVector3(rng.Next(-3, 4) + 0.3, rng.Next(-3, 4) + 0.7, rng.Next(-3, 4) + 0.1));

                var two = new GeoPlane3(
                    new GeoPoint3(rng.Next(-6, 7), rng.Next(-6, 7), rng.Next(-6, 7)),
                    new GeoVector3(rng.Next(-3, 4) + 0.9, rng.Next(-3, 4) + 0.2, rng.Next(-3, 4) + 0.5));

                // Not parallel, so they must meet in a line.
                if (Parallel3.IsParallel(one.Normal, two.Normal, Tol)) { continue; }

                pairs++;

                if (!Intersection3.TryIntersectWith(one, two, out GeoRay3 seam, Tol))
                {
                    missed++;
                    continue;
                }

                // Every point of that line lies on both planes.
                for (int k = -3; k <= 3; k++)
                {
                    GeoPoint3 along = seam.GetPointAtDistance(k * 7.0);

                    if (Math.Abs(one.SignedDistanceTo(along)) > Tol.EqualPlanar) { offLine++; }
                    if (Math.Abs(two.SignedDistanceTo(along)) > Tol.EqualPlanar) { offLine++; }
                }
            }

            Assert.True(pairs > 1000, $"only {pairs} non-parallel pairs");
            Assert.Equal(0, missed);
            Assert.Equal(0, offLine);
        }
    }
}
