using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The caps of a section: the closed lines a clip plane leaves on a body, filled, with the holes of the bore left open.
    /// </summary>
    public class SectionCapsTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static double Area(IEnumerable<DrawTriangle> triangles) => triangles.Sum(t => Vec3.Cross(t.B - t.A, t.C - t.A).Length * 0.5);

        /// <summary>
        /// A block 100 by 60 by 40, with a bore of radius 10 down its middle along Z, cut into 48 flat sides.
        /// </summary>
        private static Primitives BlockWithABore()
        {
            var floor = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0), new GeoPoint3(0, 60, 0));
            GeoSolid3 block = GeoSolid3.Extrude(floor, new GeoVector3(0, 0, 40));
            GeoSolid3 bore = GeoSolid3.Cylinder(new GeoPoint3(50, 30, -5), new GeoPoint3(50, 30, 45), 10, 48);
            return Primitives.Build(new SceneWriter().Add(block.WithOpenings(new[] { bore }), name: "block").Scene);
        }

        private static readonly double BoreArea = 0.5 * 48 * 100.0 * Math.Sin(2.0 * Math.PI / 48);

        [Fact]
        public void ABlockWithABoreCutAcrossIt_HasACapOfItsAreaLessTheBore()
        {
            Primitives block = BlockWithABore();
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 20), new Vec3(0, 0, 1)) });

            ClipResult result = ClipResult.Build(block, clips, true);

            IReadOnlyList<DrawTriangle> cap = result.Caps[0];
            Assert.NotEmpty(cap);
            Assert.Equal(6000.0 - BoreArea, Area(cap), 6);
            Assert.Equal(0, result.CapsRefused);
            Assert.All(cap, t =>
            {
                Assert.Equal(20.0, t.A.Z, 9);
                Assert.Equal(20.0, t.B.Z, 9);
                Assert.Equal(20.0, t.C.Z, 9);
                Assert.Equal(new Vec3(0, 0, -1), t.Normal);
            });

            // Not one triangle covers the bore: the middle of each is outside the circle of the bore, to the flat side of its polygon.
            double inner = 10.0 * Math.Cos(Math.PI / 48) - 1E-9;
            Assert.All(cap, t =>
            {
                Vec3 middle = (t.A + t.B + t.C) * (1.0 / 3.0);
                Assert.True(new Vec3(middle.X - 50, middle.Y - 30, 0).Length > inner, "a triangle with its middle at " + middle + " is over the bore");
            });
        }

        [Fact]
        public void TheCapOfASectionThatAnotherPlaneCutsAcross_IsCutByIt()
        {
            Primitives block = BlockWithABore();
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 20), new Vec3(0, 0, 1)), new ClipPlane(new Vec3(70, 0, 0), new Vec3(1, 0, 0)) });

            ClipResult result = ClipResult.Build(block, clips, true);

            // The cap in z = 20 is kept for x from 70 to 100 over the whole width of 60: the bore ends at x = 60. The cap in x = 70 is 60 across and kept from z = 20 up: 20 high.
            DrawTriangle[] level = result.Caps[0].Where(t => Math.Abs(t.A.Z - 20.0) < 1E-9 && Math.Abs(t.B.Z - 20.0) < 1E-9 && Math.Abs(t.C.Z - 20.0) < 1E-9).ToArray();
            DrawTriangle[] upright = result.Caps[0].Where(t => Math.Abs(t.A.X - 70.0) < 1E-9 && Math.Abs(t.B.X - 70.0) < 1E-9 && Math.Abs(t.C.X - 70.0) < 1E-9).ToArray();

            Assert.Equal(1800.0, Area(level), 6);
            Assert.Equal(1200.0, Area(upright), 6);
            Assert.Equal(3000.0, Area(result.Caps[0]), 6);
            Assert.All(result.Caps[0].SelectMany(t => new[] { t.A, t.B, t.C }), p => Assert.True(p.X >= 70.0 - 1E-9 && p.Z >= 20.0 - 1E-9));
        }

        [Fact]
        public void ABlockCutAlongTheAxisOfItsBore_HasTwoCapsOfTheBlockEitherSideOfIt_AndNoHole()
        {
            // The plane y = 30 through the axis of the bore cuts the block 100 wide and 40 high, less the bore 20 wide: two pieces of 40 by 40.
            Primitives block = BlockWithABore();
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(50, 30, 0), new Vec3(0, 1, 0)) });

            ClipResult result = ClipResult.Build(block, clips, true);

            Assert.Equal(3200.0, Area(result.Caps[0]), 6);
            Assert.Equal(0, result.CapsRefused);
        }

        [Fact]
        public void Caps_AreNotMadeUnlessAskedFor_AndNoPlaneOnMakesNone()
        {
            Primitives block = BlockWithABore();
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 20), new Vec3(0, 0, 1)) });

            Assert.Empty(ClipResult.Build(block, clips).Caps[0]);
            Assert.Empty(ClipResult.Build(block, new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 20), new Vec3(0, 0, 1), 0.0, false) }), true).Caps[0]);
            Assert.Empty(ClipResult.Build(block, ClipSet.Empty, true).Caps[0]);
        }

        [Fact]
        public void ALoopInsideAHoleInsideALoop_IsFilledAgain_AsAnIslandInTheBore()
        {
            // Three squares one inside the other, 100, 60 and 20 across: cap = 100^2 - 60^2 + 20^2.
            var chains = new List<DrawPolyline>();

            foreach (double half in new[] { 50.0, 30.0, 10.0 })
            {
                chains.Add(new DrawPolyline(new[] { new Vec3(-half, -half, 0), new Vec3(half, -half, 0), new Vec3(half, half, 0), new Vec3(-half, half, 0) }, true));
            }

            List<DrawTriangle> cap = SectionCaps.Build(chains, new ClipPlane(new Vec3(0, 0, 0), new Vec3(0, 0, 1)), Tol, out int refused);

            Assert.Equal(0, refused);
            Assert.Equal(10000.0 - 3600.0 + 400.0, Area(cap), 9);
        }

        [Fact]
        public void ALineThatDoesNotClose_HasNoCap()
        {
            var open = new List<DrawPolyline> { new DrawPolyline(new[] { new Vec3(0, 0, 0), new Vec3(10, 0, 0), new Vec3(10, 10, 0) }, false) };

            Assert.Empty(SectionCaps.Build(open, new ClipPlane(new Vec3(0, 0, 0), new Vec3(0, 0, 1)), Tol, out int refused));
            Assert.Equal(0, refused);
        }

        private static GeoSolid3 FourWalls()
        {
            var floor = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0), new GeoPoint3(0, 60, 0) };
            var faces = new List<GeoFace3>();

            for (int i = 0; i < 4; i++)
            {
                GeoPoint3 a = floor[i];
                GeoPoint3 b = floor[(i + 1) % 4];
                faces.Add(new GeoFace3(new GeoPolygon3(a, b, new GeoPoint3(b.X, b.Y, 40), new GeoPoint3(a.X, a.Y, 40)), null));
            }

            return new GeoSolid3(faces);
        }

        [Fact]
        public void ASolidOfTheFourWallsOfABlock_IsNotClosed_AndHasASectionLineButNoCap()
        {
            Primitives walls = Primitives.Build(new SceneWriter().Add(FourWalls()).Scene);
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 20), new Vec3(0, 0, 1)) });

            ClipResult result = ClipResult.Build(walls, clips, true);

            Assert.False(walls.Items[0].IsClosedBody);
            Assert.Contains(result.Sections[0], line => line.Closed);
            Assert.Empty(result.Caps[0]);
            Assert.Equal(0, result.CapsRefused);
        }

        [Fact]
        public void TheSameBlockWithItsTopAndBottom_IsClosed_AndHasACapOfItsWholeSection()
        {
            var floor = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0), new GeoPoint3(0, 60, 0));
            Primitives block = Primitives.Build(new SceneWriter().Add(GeoSolid3.Extrude(floor, new GeoVector3(0, 0, 40))).Scene);

            ClipResult result = ClipResult.Build(block, new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 20), new Vec3(0, 0, 1)) }), true);

            Assert.True(block.Items[0].IsClosedBody);
            Assert.Equal(6000.0, Area(result.Caps[0]), 6);
        }

        [Fact]
        public void AMeshOfSixSidesIsClosed_AndOneOfFiveIsNot_AndOnlyTheClosedOneHasACap()
        {
            string points = Diag.Pts3(0, 0, 0, 100, 0, 0, 100, 60, 0, 0, 60, 0, 0, 0, 40, 100, 0, 40, 100, 60, 40, 0, 60, 40);
            string sides = "[0,3,2,1],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]";
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 20), new Vec3(0, 0, 1)) });

            Primitives closed = Primitives.Build(Diag.SceneOf(Diag.Raw("GeoMesh3", "{\"kind\":\"Convex\",\"points\":" + points + ",\"faces\":[" + sides + "]}", "six")));
            Primitives open = Primitives.Build(Diag.SceneOf(Diag.Raw("GeoMesh3", "{\"kind\":\"Convex\",\"points\":" + points + ",\"faces\":[" + sides.Substring(sides.IndexOf(']') + 2) + "]}", "five")));

            Assert.True(closed.Items[0].IsClosedBody);
            Assert.False(open.Items[0].IsClosedBody);
            Assert.Equal(6000.0, Area(ClipResult.Build(closed, clips, true).Caps[0]), 6);
            Assert.Empty(ClipResult.Build(open, clips, true).Caps[0]);
        }

        [Fact]
        public void AFaceAndAPolygon_AreNeverClosedBodies()
        {
            Scene scene = Diag.SceneOf(Diag.Face3("face", 0, 0, 0, 100, 0, 0, 100, 60, 0, 0, 60, 0), Diag.Polygon2("polygon", 0, 0, 100, 0, 100, 60, 0, 60));

            Primitives primitives = Primitives.Build(scene);

            Assert.All(primitives.Items, item => Assert.False(item.IsClosedBody));
        }
    }
}
