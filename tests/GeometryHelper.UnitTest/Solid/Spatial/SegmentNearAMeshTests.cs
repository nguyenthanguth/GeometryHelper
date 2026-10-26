using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The two questions a clash bar asks an index about a piece of its centre line: which triangles it comes within a reach
    /// of, and how near it comes. Both are judged by the segment itself, not by its box, so a bar standing up through the rim
    /// of a cap, or running at a slant past a cylinder of many sides, opens only what lies near its line, and loses nothing.
    /// </summary>
    public class SegmentNearAMeshTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>
        /// A tolerance no rounding reaches, for measuring by hand: a crossing found within it is a real one.
        /// </summary>
        private static readonly Tolerance Exact = new Tolerance(1E-12, 1E-12, Tolerance.DefaultEqualAngleRad, 1E-12);

        private static GeoPreparedSolid3 Cylinder(int sides, double radius, double height)
        {
            var profile = new GeoPolygon2(Enumerable.Range(0, sides)
                .Select(k => new GeoPoint2(radius * Math.Cos(2 * Math.PI * k / sides), radius * Math.Sin(2 * Math.PI * k / sides)))
                .ToArray());

            return new GeoPreparedSolid3(GeoSolid3.Extrude(profile, new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), height, Tol), Tol);
        }

        [Fact]
        public void ABarStandingUpThroughTheRimOfACap_IsGivenTheTrianglesItComesNearAndFewOthers()
        {
            // A cylinder of radius 500 and 2 000 sides, 3 000 high. The bar stands half a thousandth outside the middle of a
            // side, from 100 below the floor to 100 above the top: it passes the caps at their rim, where hundreds of the
            // slivers they are cut into hold it in their boxes, and comes within a thousandth of only the triangles at the rim.
            const int sides = 2000;
            GeoPreparedSolid3 part = Cylinder(sides, 500, 3000);
            double angle = Math.PI / sides;
            double off = 500 * Math.Cos(Math.PI / sides) + 0.0005;
            var from = new GeoPoint3(off * Math.Cos(angle), off * Math.Sin(angle), -100);
            var to = new GeoPoint3(off * Math.Cos(angle), off * Math.Sin(angle), 3100);
            var bar = new GeoLine3(from, to);
            const double reach = 1E-3;

            List<GeoTriangle3> near = part.Index.TrianglesNear(from, to, reach);

            List<GeoTriangle3> within = part.Index.Triangles.Where(t => Projection3.GetShortestLineTo(bar, t, Exact).Length <= reach).ToList();
            List<GeoTriangle3> inBoxes = part.Index.Triangles.Where(t => t.GetAabb().DistanceTo(GeoAabb3.FromPoints(new[] { from, to })) <= reach).ToList();
            Assert.NotEmpty(within);
            Assert.All(within, t => Assert.Contains(t, near));
            Assert.True(inBoxes.Count > 100, $"only {inBoxes.Count} boxes hold the bar; the test needs more");
            Assert.True(near.Count <= 2 * within.Count, $"{near.Count} given, {within.Count} within the reach");
        }

        [Fact]
        public void TheTrianglesNearASegment_AreEveryOneWithinTheReachInTheOrderOfTheTree()
        {
            // Bars at a slant past and through a cylinder of 600 sides, at the origin and moved a million away and turned:
            // what is given holds every triangle within the reach, and keeps the order the box-only reading gave.
            var rng = new Random(7);
            GeoPreparedSolid3 here = Cylinder(600, 500, 3000);
            GeoTransform3 moved = GeoTransform3.Translation(new GeoVector3(1E6, -2E6, 3E5))
                .Multiply(GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(0.3, -0.5, 0.8), 0.7));
            var there = new GeoPreparedSolid3(here.Solid.TransformBy(moved), Tol);

            foreach (var (part, place) in new[] { (here, GeoTransform3.Identity), (there, moved) })
            {
                List<GeoTriangle3> order = part.Index.TrianglesNear(part.Index.Bounds.Min, part.Index.Bounds.Max, double.MaxValue);
                Assert.Equal(part.Index.TriangleCount, order.Count);

                for (int k = 0; k < 40; k++)
                {
                    double a = rng.NextDouble() * 2 * Math.PI;
                    double gap = new[] { -0.0005, 0.0, 0.0005, 0.002, 1.0, 30.0 }[k % 6];
                    var through = new GeoPoint3((500 + gap) * Math.Cos(a), (500 + gap) * Math.Sin(a), rng.NextDouble() * 3000);
                    var along = new GeoVector3(-Math.Sin(a), Math.Cos(a), rng.NextDouble() - 0.5).Add(new GeoVector3(Math.Cos(a), Math.Sin(a), 0).Multiply(k % 3 == 0 ? 0.0 : 0.1));
                    GeoPoint3 from = through.Add(along.Multiply(-400)).TransformBy(place);
                    GeoPoint3 to = through.Add(along.Multiply(400)).TransformBy(place);
                    double reach = new[] { 0.0, 1E-3, 1E-2, 1.0 }[k % 4];
                    var bar = new GeoLine3(from, to);

                    List<GeoTriangle3> near = part.Index.TrianglesNear(from, to, reach);

                    // Within the reach by the triangle's own measure, read a rounding wide off the origin.
                    foreach (GeoTriangle3 t in part.Index.Triangles.Where(t => Projection3.GetShortestLineTo(bar, t, Exact).Length <= reach))
                    {
                        Assert.Contains(t, near);
                    }

                    List<int> positions = near.Select(t => order.IndexOf(t)).ToList();
                    Assert.Equal(positions.OrderBy(p => p), positions);
                }
            }
        }

        [Fact]
        public void TheNearestApproachOfABarAtASlant_IsTheOneMeasuringEveryTriangleGives()
        {
            // Pieces of bars running at a slant past a cylinder of 1 000 sides, 20 to 30 clear of it, and some through it:
            // the walk passes over every branch the piece stands further from than the nearest so far, and finds the very
            // length the scan over every triangle finds.
            var rng = new Random(11);
            GeoPreparedSolid3 part = Cylinder(1000, 500, 3000);

            for (int k = 0; k < 60; k++)
            {
                double off = (k % 4 == 0 ? 690 : 735) + rng.NextDouble() * 15;
                var start = new GeoPoint3(-off, 0, 10 + rng.NextDouble() * 50);
                var end = new GeoPoint3(0, -off, 2990 - rng.NextDouble() * 50);
                double t0 = rng.NextDouble() * 0.9;
                var piece = new GeoLine3(Along(start, end, t0), Along(start, end, t0 + 0.1));
                double reach = k % 5 == 0 ? double.PositiveInfinity : 60.0;

                bool found = part.Index.TryGetShortestLineTo(piece, reach, Tol, out GeoLine3 walked);

                double scanned = part.Index.Triangles.Min(t => Projection3.GetShortestLineTo(piece, t, Tol).Length);
                Assert.Equal(scanned < reach, found);

                if (found)
                {
                    Assert.Equal(scanned, walked.Length);
                }
            }
        }

        [Fact]
        public void ATriangleAMillionAcrossBesideASegmentNearTheOrigin_IsGivenWhereTheSegmentComesWithinTheReach()
        {
            // The segment, a little over a unit long near the origin, comes within 3.4E-8 of the triangle, whose corners lie
            // some 400 000 to 2 000 000 out. Where it stands across a direction is rounded by the triangle's coordinates, by
            // up to 1E-10, and a reach read wider by the segment's alone, 2E-12, passed the triangle over.
            var triangle = new GeoTriangle3(new GeoPoint3(412059, 137757, -402096), new GeoPoint3(-412059, -137757, 402096), new GeoPoint3(2039422, -268345, 644028));
            var from = new GeoPoint3(-0.3929701205343008, -0.1313753081485629, 0.3834686167538166);
            var to = new GeoPoint3(-1.1789102908223867, -0.39412593375891447, 1.1504058726131916);
            var index = new GeoBvh3(new[] { triangle });

            List<GeoTriangle3> near = index.TrianglesNear(from, to, 3.38284055430353E-08);

            Assert.Single(near);
        }

        [Fact]
        public void TheNearestApproachToATriangleMillionsOut_IsNotPassedOverForANearerLookingOne()
        {
            // A small triangle near the segment and a large one whose corners lie three million out, nearer by 7.5E-12. The
            // walk passed the large one over, the rounding of where it stands across a direction being read by the
            // segment's coordinates alone, and gave the small one's length.
            var small = new GeoTriangle3(new GeoPoint3(12.108333693664171, -14.27560448266329, 7.310288222836693), new GeoPoint3(12.112635585006858, -14.288970593508525, 7.308602752057174), new GeoPoint3(12.099728127711115, -14.276969538158257, 7.299149064065796));
            var big = new GeoTriangle3(new GeoPoint3(-3232226, 3005249, -2367349), new GeoPoint3(3232226, -3005249, 2367349), new GeoPoint3(-19520, 253760, 107360));
            var piece = new GeoLine3(new GeoPoint3(6.289981842041016, -7.357057571411133, 3.827859878540039), new GeoPoint3(18.619945526123047, -18.8211727142334, 12.858579635620117));
            var index = new GeoBvh3(new[] { small, big });

            bool found = index.TryGetShortestLineTo(piece, double.PositiveInfinity, Tol, out GeoLine3 walked);

            double toBig = Projection3.GetShortestLineTo(piece, big, Tol).Length;
            Assert.True(found);
            Assert.True(toBig < Projection3.GetShortestLineTo(piece, small, Tol).Length, "the test needs the large triangle nearer");
            Assert.Equal(toBig, walked.Length);
        }

        private static GeoPoint3 Along(GeoPoint3 from, GeoPoint3 to, double fraction) => from.Add(from.GetVectorTo(to).Multiply(fraction));
    }
}
