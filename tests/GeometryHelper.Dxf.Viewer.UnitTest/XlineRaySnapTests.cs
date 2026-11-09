using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Object snaps on XLINE and RAY as AutoCAD takes them: nearest, perpendicular and intersections anywhere along them, the
    /// base point of a ray as its endpoint and the root point of an xline as its midpoint, and Extension, Parallel and
    /// Apparent Intersection where a straight line gives them.
    /// </summary>
    public class XlineRaySnapTests
    {
        private static string Xline(double x, double y, double dx, double dy, string handle = null)
            => TestDrawings.Entity("XLINE", "0", 256, handle, 100, "AcDbXline", 10, x, 20, y, 30, 0.0, 11, dx, 21, dy, 31, 0.0);

        private static string Ray(double x, double y, double dx, double dy, string handle = null)
            => TestDrawings.Entity("RAY", "0", 256, handle, 100, "AcDbRay", 10, x, 20, y, 30, 0.0, 11, dx, 21, dy, 31, 0.0);

        private static PickIndex Index(params string[] entities)
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, entities), "rays.dxf");
            return document.GetPickIndex(document.ModelSpace);
        }

        private static void Snapped(CadSnapResult result, CadSnapKind kind, double x, double y)
        {
            Assert.Equal(kind, result.Kind);
            Near(P(x, y), result.Point, 1E-9);
        }

        [Fact]
        public void NearestFindsAnXlineAndARayFarFromTheirBasePoints_ButNotBehindTheRay()
        {
            // An xline level through (200, 300) and a ray up from (-50, -20).
            PickIndex index = Index(Xline(200, 300, 1, 0, "B1"), Ray(-50, -20, 0, 1, "B2"));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Nearest };

            CadSnapResult far = snap.Snap(index, P(5000, 300.4), 1.0);
            Snapped(far, CadSnapKind.Nearest, 5000, 300);
            Assert.Equal("B1", far.Handle);
            Snapped(snap.Snap(index, P(-4000, 299.7), 1.0), CadSnapKind.Nearest, -4000, 300);
            Snapped(snap.Snap(index, P(-49.6, 7000), 1.0), CadSnapKind.Nearest, -50, 7000);
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(-49.6, -1000), 1.0).Kind);
        }

        [Fact]
        public void TheBaseOfARayIsAnEndpoint_AndTheRootOfAnXlineAMidpoint_AsInAutoCAD()
        {
            PickIndex index = Index(Xline(200, 300, 1, 0, "B1"), Ray(-50, -20, 0, 1, "B2"));

            Snapped(new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint }.Snap(index, P(-49.7, -19.8), 1.0), CadSnapKind.Endpoint, -50, -20);
            Assert.Equal(CadSnapKind.None, new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint }.Snap(index, P(200.3, 300.2), 1.0).Kind);

            CadSnapResult root = new ObjectSnap(Tight) { Modes = CadSnapModes.Midpoint }.Snap(index, P(200.3, 300.2), 1.0);
            Snapped(root, CadSnapKind.Midpoint, 200, 300);
            Assert.Equal("B1", root.Handle);
            Assert.Equal(CadSnapKind.None, new ObjectSnap(Tight) { Modes = CadSnapModes.Midpoint }.Snap(index, P(-49.7, -19.8), 1.0).Kind);
        }

        [Fact]
        public void PerpendicularFromTheBasePointFallsOnAnXlineAnywhere_AndOnARayOnlyAheadOfItsBase()
        {
            // An xline level through (200, 300); a ray along +X from (100, -500).
            PickIndex index = Index(Xline(200, 300, 1, 0, "B1"), Ray(100, -500, 1, 0, "B2"));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Perpendicular };

            Snapped(snap.Snap(index, P(5000, 300.3), 1.0, P(-7000, 40)), CadSnapKind.Perpendicular, -7000, 300);
            Snapped(snap.Snap(index, P(800, -499.7), 1.0, P(500, 80)), CadSnapKind.Perpendicular, 500, -500);

            // From (0, 80) the foot (0, -500) lies behind the ray's base: no perpendicular.
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(800, -499.7), 1.0, P(0, 80)).Kind);
        }

        [Fact]
        public void AnXlineAndARayCrossLinesArcsCirclesAndEachOther()
        {
            // An xline along (1, 1) through the origin crosses the line x = 100 at (100, 100); a level xline through (0, 5)
            // crosses the circle of radius 10 about (1000, 0) at (1000 +- 8.660, 5); a ray along +X from (0, 2000) and a ray up
            // from (3000, 0) cross at (3000, 2000); the ray up stops short of the level xline through (0, -0.5) half a unit
            // below its base, and crosses the left half of the circle of radius 10 about (3005, 500) at (3000, 508.660).
            PickIndex index = Index(
                Xline(0, 0, 1, 1, "B1"),
                TestDrawings.Line(100, 0, 100, 500, handle: "A1"),
                Xline(0, 5, 1, 0, "B2"),
                TestDrawings.Circle(1000, 0, 10, handle: "A2"),
                Ray(0, 2000, 1, 0, "B3"),
                Ray(3000, 0, 0, 1, "B4"),
                Xline(0, -0.5, 1, 0, "B5"),
                TestDrawings.Arc(3005, 500, 10, 90, 270, handle: "A3"));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Intersection };

            CadSnapResult xlineLine = snap.Snap(index, P(100.3, 99.8), 1.0);
            Snapped(xlineLine, CadSnapKind.Intersection, 100, 100);
            Assert.Equal("A1", xlineLine.Handle);
            Snapped(snap.Snap(index, P(1008.4, 5.3), 1.0), CadSnapKind.Intersection, 1000 + Math.Sqrt(75.0), 5);
            Snapped(snap.Snap(index, P(991.6, 4.8), 1.0), CadSnapKind.Intersection, 1000 - Math.Sqrt(75.0), 5);
            Snapped(snap.Snap(index, P(2999.6, 2000.3), 1.0), CadSnapKind.Intersection, 3000, 2000);
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(3000.2, -0.3), 1.0).Kind);
            Snapped(snap.Snap(index, P(3000.3, 508.4), 1.0), CadSnapKind.Intersection, 3000, 500 + Math.Sqrt(75.0));
        }

        [Fact]
        public void ExtensionRunsOnBackFromTheBaseOfARay_AndParallelFollowsAnXline()
        {
            // A ray along +X from (100, 0): its base rested on, the run goes back along -X.
            PickIndex rays = Index(Ray(100, 0, 1, 0, "B1"));
            var extension = new ObjectSnap(Tight) { Modes = CadSnapModes.Extension };

            Assert.True(extension.Acquire(rays, P(100.2, 0.3), 1.0));
            CadSnapResult back = extension.Snap(rays, P(40, 0.4), 1.0);
            Snapped(back, CadSnapKind.Extension, 40, 0);
            IReadOnlyList<GeoPoint2> track = Assert.Single(back.Tracks);
            Near(P(100, 0), track[0], 1E-9);

            // An xline along (2, 1) rested on far from its root, with the rubber band from (200, 0).
            PickIndex xlines = Index(Xline(0, 0, 2, 1, "B2"));
            var parallel = new ObjectSnap(Tight) { Modes = CadSnapModes.Parallel };
            GeoPoint2 from = P(200, 0);

            Assert.True(parallel.Acquire(xlines, P(4000, 2000.3), 1.0, from));
            Assert.Equal(CadSnapKind.Parallel, Assert.Single(parallel.Acquired).Kind);
            Snapped(parallel.Snap(xlines, P(260, 30.6), 1.0, from), CadSnapKind.Parallel, 260.24, 30.12);
        }

        [Fact]
        public void ARayStoppingShortOfALine_GivesTheirApparentIntersection()
        {
            // A ray along -X from (98, 0) points away from the line x = 100 that starts at (100, 2): run on, they meet at (100, 0).
            PickIndex index = Index(Ray(98, 0, -1, 0, "B1"), TestDrawings.Line(100, 2, 100, 50, handle: "A1"));

            CadSnapResult apparent = new ObjectSnap(Tight) { Modes = CadSnapModes.ApparentIntersection }.Snap(index, P(99.5, 0.6), 2.5);
            Snapped(apparent, CadSnapKind.ApparentIntersection, 100, 0);
            Assert.Equal(CadSnapKind.None, new ObjectSnap(Tight) { Modes = CadSnapModes.Intersection }.Snap(index, P(99.5, 0.6), 2.5).Kind);
        }
    }
}
