using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class SectionTests
    {
        private static ItemPrimitives Drawn(object shape)
            => Primitives.Build(new SceneWriter().Add(shape).Scene).Items[0];

        private static ItemPrimitives Box(double x, double y, double z, bool withOpening = false)
            => Drawn(SampleShapes.Box(x, y, z, withOpening));

        /// <summary>
        /// A prism over a regular polygon of the given number of sides, its corners on a circle of the given radius, the first at angle nought.
        /// </summary>
        private static GeoSolid3 Prism(int sides, double radius, double height)
        {
            var corners = Enumerable.Range(0, sides).Select(k => new GeoPoint3(radius * Math.Cos(2.0 * Math.PI * k / sides), radius * Math.Sin(2.0 * Math.PI * k / sides), 0.0)).ToList();
            return GeoSolid3.Extrude(new GeoPolygon3(corners), new GeoVector3(0, 0, height));
        }

        private static ClipPlane Plane(Vec3 through, Vec3 normal) => new ClipPlane(through, normal);

        private static double TriangleArea(DrawTriangle t) => Vec3.Cross(t.B - t.A, t.C - t.A).Length * 0.5;

        [Fact]
        public void ABoxCutAtHalfItsHeight_GivesOneClosedLineOfFourCorners_AsLongAsItsPerimeter()
        {
            ItemPrimitives box = Box(30, 20, 10);

            List<DrawPolyline> chains = SectionBuilder.Chains(box, Plane(new Vec3(0, 0, 5), new Vec3(0, 0, 1)), 1E-9);

            DrawPolyline only = Assert.Single(chains);
            Assert.True(only.Closed);
            Assert.Equal(4, only.Points.Length);
            Assert.All(only.Points, p => Assert.Equal(5.0, p.Z, 12));
            Assert.Equal(100.0, SectionBuilder.Length(chains), 10);
        }

        [Fact]
        public void ABoxCutAcrossItsLength_HasTheSectionOfItsWidthAndHeight()
        {
            ItemPrimitives box = Box(30, 20, 10);

            List<DrawPolyline> chains = SectionBuilder.Chains(box, Plane(new Vec3(10, 0, 0), new Vec3(1, 0, 0)), 1E-9);

            Assert.Single(chains);
            Assert.True(chains[0].Closed);
            Assert.Equal(4, chains[0].Points.Length);
            Assert.Equal(60.0, SectionBuilder.Length(chains), 10);
        }

        [Fact]
        public void ABoxCutAtItsTopFace_GivesItsOutline_OnceAroundAndNotTwice()
        {
            ItemPrimitives box = Box(30, 20, 10);

            // The plane is the plane of the top: the triangles of the top lie in it and add nothing; the edges of the sides in it are taken once each.
            List<DrawPolyline> chains = SectionBuilder.Chains(box, Plane(new Vec3(0, 0, 10), new Vec3(0, 0, -1)), 1E-9);

            Assert.Single(chains);
            Assert.Equal(4, chains[0].Points.Length);
            Assert.Equal(100.0, SectionBuilder.Length(chains), 10);
        }

        [Fact]
        public void ABoxCutAslant_ThroughTwoOfItsEdges_GivesTheRectangleOfTheDiagonalPlane()
        {
            ItemPrimitives box = Box(30, 20, 10);

            // The plane through the front bottom edge and the back top edge: a rectangle 30 long and the diagonal of the 20 by 10 end, sqrt(500), across.
            Vec3 normal = new Vec3(0, -10, 20);
            List<DrawPolyline> chains = SectionBuilder.Chains(box, Plane(new Vec3(0, 0, 0), normal), 1E-9);

            Assert.Single(chains);
            Assert.True(chains[0].Closed);
            Assert.Equal(4, chains[0].Points.Length);
            Assert.Equal(2.0 * (30.0 + Math.Sqrt(500.0)), SectionBuilder.Length(chains), 9);
        }

        [Fact]
        public void ABoxMissedByThePlane_HasNoSection()
        {
            ItemPrimitives box = Box(30, 20, 10);

            Assert.Empty(SectionBuilder.Chains(box, Plane(new Vec3(0, 0, 10.5), new Vec3(0, 0, 1)), 1E-9));
            Assert.Empty(SectionBuilder.Chains(box, Plane(new Vec3(-3, 0, 0), new Vec3(1, 0, 0)), 1E-9));
            Assert.Empty(SectionBuilder.Chains(new ItemPrimitives(), Plane(default(Vec3), new Vec3(1, 0, 0)), 1E-9));
        }

        [Fact]
        public void ABoxWithAThroughHole_HasTwoLinesInASection_TheOutsideAndTheHole()
        {
            ItemPrimitives box = Box(30, 20, 10, true);

            List<DrawPolyline> chains = SectionBuilder.Chains(box, Plane(new Vec3(0, 0, 5), new Vec3(0, 0, 1)), 1E-9);

            Assert.Equal(2, chains.Count);
            Assert.All(chains, c => Assert.True(c.Closed));
            Assert.Equal(new[] { 4, 4 }, chains.Select(c => c.Points.Length));

            // The box is 100 round and the hole, 10 by 10, 40.
            Assert.Equal(new[] { 40.0, 100.0 }, chains.Select(c => Math.Round(SectionBuilder.Length(new[] { c }), 9)).OrderBy(v => v));
        }

        [Fact]
        public void APrismOf64Sides_CutAcrossItsAxis_HasAClosedLineOfTheLengthOfItsPerimeter()
        {
            const int Sides = 64;
            const double Radius = 50.0;
            ItemPrimitives prism = Drawn(Prism(Sides, Radius, 40.0));

            List<DrawPolyline> chains = SectionBuilder.Chains(prism, Plane(new Vec3(0, 0, 13), new Vec3(0, 0, 1)), 1E-9);

            DrawPolyline only = Assert.Single(chains);
            Assert.True(only.Closed);
            Assert.Equal(Sides, only.Points.Length);
            Assert.Equal(Sides * 2.0 * Radius * Math.Sin(Math.PI / Sides), SectionBuilder.Length(chains), 9);
        }

        [Fact]
        public void APrismOf64Sides_CutAlongItsAxisThroughTwoOppositeCorners_HasARectangleOfTheDiameterAndTheHeight()
        {
            const int Sides = 64;
            const double Radius = 50.0;
            ItemPrimitives prism = Drawn(Prism(Sides, Radius, 40.0));

            // The plane y = 0 holds the corners at angle 0 and at pi, so it runs along two of the edges of the prism.
            List<DrawPolyline> chains = SectionBuilder.Chains(prism, Plane(new Vec3(0, 0, 0), new Vec3(0, 1, 0)), 1E-9);

            DrawPolyline only = Assert.Single(chains);
            Assert.True(only.Closed);
            Assert.Equal(4, only.Points.Length);
            Assert.Equal(2.0 * (2.0 * Radius + 40.0), SectionBuilder.Length(chains), 8);
        }

        [Fact]
        public void APrismOf64Sides_CutAlongItsAxisOffTheCorners_HasARectangleOfTheChordAndTheHeight()
        {
            const int Sides = 64;
            const double Radius = 50.0;
            const double Offset = 17.5;
            ItemPrimitives prism = Drawn(Prism(Sides, Radius, 40.0));

            // The chord of the polygon on the line y = 17.5, worked out edge by edge on its own: a convex polygon is met by a line in two points.
            var crossings = new List<double>();

            for (int k = 0; k < Sides; k++)
            {
                double ax = Radius * Math.Cos(2.0 * Math.PI * k / Sides);
                double ay = Radius * Math.Sin(2.0 * Math.PI * k / Sides);
                double bx = Radius * Math.Cos(2.0 * Math.PI * (k + 1) / Sides);
                double by = Radius * Math.Sin(2.0 * Math.PI * (k + 1) / Sides);

                if ((ay - Offset) * (by - Offset) < 0.0)
                {
                    crossings.Add(ax + (bx - ax) * (Offset - ay) / (by - ay));
                }
            }

            Assert.Equal(2, crossings.Count);
            double chord = Math.Abs(crossings[0] - crossings[1]);

            List<DrawPolyline> chains = SectionBuilder.Chains(prism, Plane(new Vec3(0, Offset, 0), new Vec3(0, -1, 0)), 1E-9);

            DrawPolyline only = Assert.Single(chains);
            Assert.Equal(4, only.Points.Length);
            Assert.Equal(2.0 * (chord + 40.0), SectionBuilder.Length(chains), 8);
        }

        [Fact]
        public void TheSectionOfAPrismByAnyPlane_IsTheSameWhichWayRoundThePlaneFaces()
        {
            ItemPrimitives prism = Drawn(Prism(32, 30.0, 25.0));
            var through = new Vec3(3, -2, 7);
            Vec3 normal = new Vec3(0.3, 0.2, 1.0);

            double one = SectionBuilder.Length(SectionBuilder.Chains(prism, Plane(through, normal), 1E-9));
            double other = SectionBuilder.Length(SectionBuilder.Chains(prism, Plane(through, -normal), 1E-9));

            Assert.True(one > 0.0);
            Assert.Equal(one, other, 9);
        }

        [Fact]
        public void TheChainsOfASection_FollowTheTrianglesInAFixedOrder()
        {
            ItemPrimitives prism = Drawn(Prism(32, 30.0, 25.0));
            ClipPlane plane = Plane(new Vec3(1, 2, 12.5), new Vec3(0.1, 0.0, 1.0));

            List<DrawPolyline> first = SectionBuilder.Chains(prism, plane, 1E-9);
            List<DrawPolyline> second = SectionBuilder.Chains(prism, plane, 1E-9);

            Assert.Equal(first.Count, second.Count);

            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].Points, second[i].Points);
                Assert.Equal(first[i].Closed, second[i].Closed);
            }
        }

        [Fact]
        public void TwoPlanesCutASectionEachByTheOther_SoOnlyWhatIsInsideTheClippedRegionIsDrawn()
        {
            Primitives box = Primitives.Build(new SceneWriter().Add(SampleShapes.Box(30, 20, 10, false)).Scene);

            // Keep what is above z = 5 and beyond x = 10.
            ClipSet clips = new ClipSet(new[] { Plane(new Vec3(0, 0, 5), new Vec3(0, 0, 1)), Plane(new Vec3(10, 0, 0), new Vec3(1, 0, 0)) });
            ClipResult result = ClipResult.Build(box, clips);

            // The cut at z = 5, 20 along x and 20 across, kept for x from 10 to 30 and so 20 + 20 + 20 round three sides, and the cut at x = 10, 20 across
            // and 10 up, kept for z from 5 to 10, 5 + 20 + 5 round three sides.
            Assert.Equal(90.0, SectionBuilder.Length(result.Sections[0]), 11);
        }

        [Fact]
        public void APlaneTangentToABoreAlongAnEdge_GivesChainsThatEndWhereThreeMeet_AndNoneIsCalledClosedThatDoesNotReturnToItsStart()
        {
            // The block 100 by 60 by 40 with a bore of radius 10 along Y at z = 20: the plane z = 30 touches the top of the bore along a line. The section is the outline
            // of the block with a line across it where the plane touches, which is three lines meeting at two points; it is reported as the pieces between them,
            // two of 160 and one of 60, none closed, 380 in all, and not as a closed outline that is not one.
            var floor = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0), new GeoPoint3(0, 60, 0));
            GeoSolid3 block = GeoSolid3.Extrude(floor, new GeoVector3(0, 0, 40));
            GeoSolid3 bore = GeoSolid3.Cylinder(new GeoPoint3(50, -5, 20), new GeoPoint3(50, 65, 20), 10, 48);
            Primitives scene = Primitives.Build(new SceneWriter().Add(block.WithOpenings(new[] { bore }), name: "block").Scene);
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(50, 30, 30), new Vec3(1E-12, 0, 1)) });

            IReadOnlyList<DrawPolyline> lines = ClipResult.Build(scene, clips).Sections[0];

            Assert.Equal(3, lines.Count);
            Assert.All(lines, l => Assert.False(l.Closed));
            Assert.Equal(new[] { 60.0, 160.0, 160.0 }, lines.Select(l => Math.Round(SectionBuilder.Length(new[] { l }), 6)).OrderBy(v => v));
            Assert.Equal(380.0, SectionBuilder.Length(lines), 6);
        }
        [Fact]
        public void ASectionOfAPlaneThatIsOff_IsNotMade_AndNothingIsCutWhenNoPlaneIsOn()
        {
            Primitives box = Primitives.Build(new SceneWriter().Add(SampleShapes.Box(30, 20, 10, false)).Scene);
            ClipSet off = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 5), new Vec3(0, 0, 1), 0.0, false) });

            ClipResult result = ClipResult.Build(box, off);

            Assert.Same(box, result.Clipped);
            Assert.Empty(result.Sections[0]);
            Assert.Same(box, ClipResult.Build(box, ClipSet.Empty).Clipped);
        }

        [Fact]
        public void ACutBody_KeepsItsTriangles_AsWholeAsThePlaneLeavesThem_AndTheAreaIsWhatIsLeft()
        {
            Primitives box = Primitives.Build(new SceneWriter().Add(SampleShapes.Box(30, 20, 10, false)).Scene);
            double whole = box.Items[0].Triangles.Sum(TriangleArea);
            ClipSet top = new ClipSet(new[] { Plane(new Vec3(0, 0, 5), new Vec3(0, 0, 1)) });

            ItemPrimitives cut = top.Apply(box.Items[0]);

            // The box has 2 * (600 + 300 + 200) = 2200 of surface; with the lower half taken away, 600 of top and half of each of the four sides are left: 1100.
            Assert.Equal(2200.0, whole, 9);
            Assert.Equal(1100.0, cut.Triangles.Sum(TriangleArea), 9);
            Assert.All(cut.Triangles, t => Assert.True(t.A.Z >= 5.0 - 1E-12 && t.B.Z >= 5.0 - 1E-12 && t.C.Z >= 5.0 - 1E-12));

            // A piece of a triangle is lit as the whole was: its normal is one of the six of the box, to the bit.
            var normals = new HashSet<Vec3>(box.Items[0].Triangles.Select(t => t.Normal));
            Assert.All(cut.Triangles, t => Assert.Contains(t.Normal, normals));
        }

        [Fact]
        public void ALineIsCutToTheRunsInsideThePlanes_AndALineThatIsWhollyInsideIsTheSameLine()
        {
            ClipSet keep = new ClipSet(new[] { Plane(new Vec3(10, 0, 0), new Vec3(1, 0, 0)) });
            var bent = new DrawPolyline(new[] { new Vec3(0, 0, 0), new Vec3(30, 0, 0), new Vec3(30, 20, 0) }, false);
            var whole = new DrawPolyline(new[] { new Vec3(15, 0, 0), new Vec3(30, 0, 0), new Vec3(30, 20, 0) }, true);
            var item = new ItemPrimitives();
            item.Polylines.Add(bent);
            item.Polylines.Add(whole);

            ItemPrimitives cut = keep.Apply(item);

            Assert.Equal(2, cut.Polylines.Count);

            // The bent one is one run, from where the plane cuts the first piece, with no break at the corner.
            Vec3[] run = cut.Polylines[0].Points;
            Assert.Equal(3, run.Length);
            ViewFixture.AssertNear(new Vec3(10, 0, 0), run[0], 1E-12);

            // The corner and the end were inside: they are the points they were, to the bit.
            Assert.Equal(new Vec3(30, 0, 0), run[1]);
            Assert.Equal(new Vec3(30, 20, 0), run[2]);
            Assert.False(cut.Polylines[0].Closed);

            // The one that is all inside is not copied: it is itself, closed.
            Assert.Same(whole, cut.Polylines[1]);
        }

        [Fact]
        public void APlaneThatLeavesAClosedLineInTwoPieces_GivesTwoRuns_AndAClosedLineIsCutOpen()
        {
            ClipSet keep = new ClipSet(new[] { Plane(new Vec3(5, 0, 0), new Vec3(1, 0, 0)), Plane(new Vec3(25, 0, 0), new Vec3(-1, 0, 0)) });
            var ring = new DrawPolyline(new[] { new Vec3(0, 0, 0), new Vec3(30, 0, 0), new Vec3(30, 10, 0), new Vec3(0, 10, 0) }, true);
            var item = new ItemPrimitives();
            item.Polylines.Add(ring);

            ItemPrimitives cut = keep.Apply(item);

            // Of the rectangle 30 by 10, the strip between x = 5 and x = 25 keeps 20 of the bottom and 20 of the top, in two runs; the ends are cut away.
            Assert.Equal(2, cut.Polylines.Count);
            Assert.All(cut.Polylines, p => Assert.False(p.Closed));
            Assert.Equal(40.0, SectionBuilder.Length(cut.Polylines), 12);
        }

        [Fact]
        public void PointsAndLabelsOutsideThePlanes_AreDropped()
        {
            ClipSet keep = new ClipSet(new[] { Plane(new Vec3(0, 0, 0), new Vec3(1, 0, 0)) });
            var item = new ItemPrimitives();
            item.Points.Add(new Vec3(1, 0, 0));
            item.Points.Add(new Vec3(-1, 0, 0));
            item.Points.Add(new Vec3(0, 5, 0));
            item.Labels.Add(new DrawLabel(new Vec3(-4, 0, 0), "out", 0.0));
            item.Labels.Add(new DrawLabel(new Vec3(4, 0, 0), "in", 0.0));

            ItemPrimitives cut = keep.Apply(item);

            Assert.Equal(new[] { new Vec3(1, 0, 0), new Vec3(0, 5, 0) }, cut.Points);
            Assert.Equal("in", Assert.Single(cut.Labels).Text);
        }
    }
}
