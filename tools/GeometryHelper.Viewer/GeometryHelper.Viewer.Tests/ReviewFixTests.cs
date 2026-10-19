using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What a review of the second phase found, as tests: each failed before the change that follows it.
    /// </summary>
    public class ReviewFixTests
    {
        private static ExactGeometry Exact(SceneWriter writer) => ExactGeometry.Build(writer.Scene, Primitives.Build(writer.Scene));

        [Fact]
        public void ASegmentAHairOffThePlaneOfACircle_StillCrossesIt()
        {
            // Its ends are 1E-7 either side of the circle's plane, well within the tolerance of the scene: it is in the plane, and crosses at x = +-1.
            ExactGeometry g = Exact(new SceneWriter()
                .Add(new GeoLine3(new GeoPoint3(-2, 0, -1E-7), new GeoPoint3(2, 0, 1E-7)))
                .Add(new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.0)));

            Vec3[] hits = CurveIntersections.Find(g.Curves[0], g.Curves[1], 1E-3).OrderBy(h => h.X).ToArray();

            Assert.Equal(2, hits.Length);
            Assert.Equal(-1.0, hits[0].X, 6);
            Assert.Equal(1.0, hits[1].X, 6);
        }

        [Fact]
        public void TwoCirclesWhosePlanesDifferByAHair_CrossWhereTheyWouldIfTheyWereOnePlane()
        {
            // Radius 100, planes 1E-7 rad apart: at the crossings, x = 75 and y = +-66.14, they are 7.5E-6 apart, less than the tolerance.
            ExactGeometry g = Exact(new SceneWriter()
                .Add(new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100.0))
                .Add(new GeoCircle3(new GeoPoint3(150, 0, 0), new GeoVector3(1E-7, 0, 1), 100.0)));

            Vec3[] hits = CurveIntersections.Find(g.Curves[0], g.Curves[1], 1E-3).OrderBy(h => h.Y).ToArray();

            Assert.Equal(2, hits.Length);
            Assert.Equal(75.0, hits[0].X, 3);
            Assert.Equal(-Math.Sqrt(100.0 * 100.0 - 75.0 * 75.0), hits[0].Y, 3);
            Assert.Equal(Math.Sqrt(100.0 * 100.0 - 75.0 * 75.0), hits[1].Y, 3);
        }

        [Theory]
        [InlineData(15.0)]
        [InlineData(0.5)]
        [InlineData(0.01)]
        public void ASegmentInATiltedPlaneFarFromTheOrigin_CrossesTheCircleOfThatPlane(double half)
        {
            var random = new Random(11);
            int found = 0;

            for (int k = 0; k < 200; k++)
            {
                double a = 0.2 + random.NextDouble();
                Vec3 normal = new Vec3(0.1 * random.NextDouble(), Math.Sin(a), Math.Cos(a)).Normalized();
                Vec3 c = new Vec3(60000 + random.NextDouble(), 60000 + random.NextDouble(), 1000 + random.NextDouble());
                ArcTessellation_Axes(normal, out Vec3 u, out Vec3 v);
                double y0 = random.NextDouble() * 4 - 2;
                double x0 = Math.Sqrt(100.0 - y0 * y0);
                Vec3 p = c + u * (x0 - half) + v * y0;
                Vec3 q = c + u * (x0 + half) + v * (y0 + 0.3 * half);
                ExactGeometry g = Exact(new SceneWriter()
                    .Add(new GeoCircle3(new GeoPoint3(c.X, c.Y, c.Z), new GeoVector3(normal.X, normal.Y, normal.Z), 10.0))
                    .Add(new GeoLine3(new GeoPoint3(p.X, p.Y, p.Z), new GeoPoint3(q.X, q.Y, q.Z))));

                if (CurveIntersections.Find(g.Curves[0], g.Curves[1], 1E-3).Count >= 1)
                {
                    found++;
                }
            }

            Assert.Equal(200, found);
        }

        private static void ArcTessellation_Axes(Vec3 normal, out Vec3 u, out Vec3 v)
        {
            new GeoPlane3(new GeoPoint3(0.0, 0.0, 0.0), new GeoVector3(normal.X, normal.Y, normal.Z)).GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);
            u = new Vec3(uAxis.X, uAxis.Y, uAxis.Z);
            v = new Vec3(vAxis.X, vAxis.Y, vAxis.Z);
        }

        [Fact]
        public void ACrossingBoxOverASegmentThroughTheNearPlane_SelectsIt()
        {
            var random = new Random(7);
            int misses = 0;

            for (int k = 0; k < 400; k++)
            {
                double y0 = 60000 + random.NextDouble();
                Scene scene = new SceneWriter()
                    .Add(new GeoLine3(new GeoPoint3(60000.3, y0 - 5000, 1000.2), new GeoPoint3(60000.3, y0 + 5000, 1000.2)))
                    .Add(new GeoPoint3(60000, y0 - 6000, 1000)).Add(new GeoPoint3(60000, y0 + 6000, 1000)).Scene;
                Primitives primitives = Primitives.Build(scene);
                var camera = new ViewCamera { Azimuth = -Math.PI / 2, Elevation = random.NextDouble() * 1E-3, Distance = 50, Target = new Vec3(60000, y0 + 30, 1000) };
                var map = new ScreenMap(camera, primitives.Bounds, 1000, 800);

                if (!BoxSelect.Select(primitives, map, 999, 799, 1, 1, BoxMode.Crossing, i => i == 0).Contains(0))
                {
                    misses++;
                }
            }

            Assert.Equal(0, misses);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(0.0, 60000.0)]
        [InlineData(1E-12, 0.0)]
        [InlineData(1E-12, 60000.0)]
        [InlineData(1E-3, 60000.0)]
        public void TwoSegmentsThatCross_AtAnyTolerance_GiveTheirCrossing(double tolerance, double offset)
        {
            var random = new Random(3);

            for (int k = 0; k < 200; k++)
            {
                double ax = offset + random.NextDouble();
                double ay = offset + random.NextDouble();
                ExactGeometry g = Exact(new SceneWriter()
                    .Add(new GeoLine2(new GeoPoint2(ax, ay), new GeoPoint2(ax + 10.3, ay + 3.7)))
                    .Add(new GeoLine2(new GeoPoint2(ax, ay + 3.1), new GeoPoint2(ax + 10.1, ay + 0.3))));

                Assert.Single(CurveIntersections.Find(g.Curves[0], g.Curves[1], tolerance));
            }
        }

        [Fact]
        public void ALineWithinTheToleranceOfACircle_TouchesIt_AsTwoCirclesDo()
        {
            ExactGeometry g = Exact(new SceneWriter()
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 10.0))
                .Add(new GeoLine2(new GeoPoint2(-20, 10.0005), new GeoPoint2(20, 10.0005)))
                .Add(new GeoCircle2(new GeoPoint2(20.0005, 0), 10.0)));

            Assert.Single(CurveIntersections.Find(g.Curves[0], g.Curves[1], 1E-3));
            Assert.Single(CurveIntersections.Find(g.Curves[0], g.Curves[2], 1E-3));
            Assert.Empty(CurveIntersections.Find(g.Curves[0], g.Curves[1], 1E-4));
        }

        [Theory]
        [InlineData(1E-8)]
        [InlineData(1E-7)]
        [InlineData(1E-6)]
        [InlineData(1E-4)]
        public void ANearlyStraightBulgeEdge_CrossesAndIsNearestWhereTheArcIs(double bulge)
        {
            // A chord of 60 000 with a bulge of 1E-8 has its centre 1.5E12 away; the sagitta is bulge * 30 000, and the line is at the middle.
            ExactGeometry g = Exact(new SceneWriter()
                .Add(new GeoPolylineArc2(new[] { new GeoPoint2(60000, 60000), new GeoPoint2(120000, 60000) }, new[] { bulge, 0.0 }))
                .Add(new GeoLine2(new GeoPoint2(90000, 59000), new GeoPoint2(90000, 61000))));
            SceneCurve arc = g.Curves[0];
            Vec3 middle = new Vec3(90000.0, 60000.0 - bulge * 30000.0, 0.0);

            Assert.Equal(CurveShape.Conic, arc.Shape);
            Vec3 hit = Assert.Single(CurveIntersections.Find(arc, g.Curves[1], 1E-3));
            Assert.True((hit - middle).Length < 1E-9, "crossing " + (hit - middle).Length + " from the middle");
            Assert.True((arc.Middle.Value - middle).Length < 1E-9);
            Assert.True((arc.ClosestPointTo(new Vec3(90000, 61000, 0)) - middle).Length < 1E-9);
        }

        [Fact]
        public void ABulgeEdgeGivesTheSameArcWhetherNearlyStraightOrNot_AtTheEndsAndAlongTheChord()
        {
            ExactGeometry g = Exact(new SceneWriter().Add(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0) }, new[] { 0.5, 0.0 })));
            SceneCurve arc = g.Curves[0];

            Assert.Equal(new Vec3(0, 0, 0), arc.PointAt(arc.StartAngle));
            Assert.True((arc.PointAt(arc.StartAngle + arc.Sweep) - new Vec3(100, 0, 0)).Length < 1E-12);
            Assert.True((arc.PointAt(arc.StartAngle + arc.Sweep * 0.5) - new Vec3(50, -25, 0)).Length < 1E-12);
            Assert.Equal(arc.StartAngle + arc.Sweep * 0.5, arc.ParameterOf(new Vec3(50, -25, 0)), 12);
        }

        [Fact]
        public void TheCurvesOfASceneTooBigToDraw_AreCutAtOneCoarserChordTolerance_WithoutCuttingThemFinelyFirst()
        {
            // 300 circles of radius 60 000 would take 16 million points at the chord tolerance of 1E-4, 4 million at the cap.
            var writer = new SceneWriter();

            for (int i = 0; i < 300; i++)
            {
                writer.Add(new GeoCircle2(new GeoPoint2(i * 10.0, 0), 60000.0));
            }

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            Primitives primitives = Primitives.Build(writer.Scene);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            long points = primitives.Items.Sum(item => item.Polylines.Sum(line => (long)line.Points.Length));
            Assert.InRange(points, 3500000, Primitives.MaxPoints);

            // One warning, naming the chord tolerance: the fine drawing was never made (it is 16 million points of 24 bytes, 390 MB, before the cap is applied).
            string warning = Assert.Single(primitives.Warnings);
            Assert.Contains("chord tolerance of", warning, StringComparison.Ordinal);
            Assert.Contains("over the cap of 4000000", warning, StringComparison.Ordinal);
            Assert.True(allocated < 250L * 1024 * 1024, "allocated " + allocated / 1048576 + " MB");

            // Every circle has the same number of points: the one tolerance.
            Assert.Single(primitives.Items.Select(item => item.Polylines[0].Points.Length).Distinct());
        }

        [Fact]
        public void TheOneChordToleranceOfACappedScene_IsTheFinestThatFits()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 10.0))
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 60000.0))
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 500.0))
                .Scene;

            Primitives fine = Primitives.Build(scene);
            int total = fine.Items.Sum(item => item.Polylines[0].Points.Length);
            Primitives capped = Primitives.Build(scene, total - 1);

            int cappedTotal = capped.Items.Sum(item => item.Polylines[0].Points.Length);
            Assert.True(cappedTotal <= total - 1);
            Assert.True(cappedTotal > total / 2, "the finest that fits is only just coarser: " + cappedTotal + " of " + total);
            Assert.Single(capped.Warnings);
            Assert.DoesNotContain("still over", capped.Warnings[0], StringComparison.Ordinal);
        }

        [Fact]
        public void AnItemBeyondTheFarPlane_IsNeitherPickedNorSnappedNorSelected()
        {
            // A camera 2 mm from its target, so that the far plane is 20 m off; a point and a line 100 m beyond the target are behind it.
            Scene scene = new SceneWriter()
                .Add(new GeoPoint3(0, 0, 0))
                .Add(new GeoLine3(new GeoPoint3(-50, 100000, 0), new GeoPoint3(50, 100000, 0)))
                .Add(new GeoPoint3(0, 100000, 0))
                .Scene;
            Primitives primitives = Primitives.Build(scene);
            ExactGeometry geometry = ExactGeometry.Build(scene, primitives);
            var camera = new ViewCamera { Azimuth = -Math.PI / 2, Elevation = 0.0, Distance = 2.0, Target = new Vec3(0, 0, 0) };
            var map = new ScreenMap(camera, primitives.Bounds, 1000, 800);

            Assert.True(map.IsInDepth(new Vec3(0, 0, 0)));
            Assert.False(map.IsInDepth(new Vec3(0, 100000, 0)));
            Assert.False(map.TryProject(new Vec3(0, 100000, 0), out _, out _, out _));

            // The ray through the middle of the picture meets the far point and the line.
            Assert.Null(SnapEngine.Find(geometry, map, 500, 400, SnapKinds.All, SnapEngine.DefaultRadius, 1E-3, i => i != 0));
            Assert.Null(PickEngine.Pick(primitives, geometry, map, 500, 400, i => i != 0, true));
            Assert.Empty(BoxSelect.Select(primitives, map, 0, 0, 1000, 800, BoxMode.Crossing, i => i != 0));
            Assert.Equal(new[] { 0 }, BoxSelect.Select(primitives, map, 0, 0, 1000, 800, BoxMode.Crossing, null));
        }

        [Fact]
        public void ASegmentThatReachesBeyondTheFarPlane_IsSelectedByItsNearerPart()
        {
            Scene scene = new SceneWriter().Add(new GeoLine3(new GeoPoint3(0.2, -1, 0), new GeoPoint3(0.2, 100000, 0))).Scene;
            Primitives primitives = Primitives.Build(scene);
            var camera = new ViewCamera { Azimuth = -Math.PI / 2, Elevation = 0.0, Distance = 2.0, Target = new Vec3(0, 0, 0) };
            var map = new ScreenMap(camera, primitives.Bounds, 1000, 800);

            Assert.Equal(new[] { 0 }, BoxSelect.Select(primitives, map, 0, 0, 1000, 800, BoxMode.Crossing, null));
            Assert.True(map.ClipSegment(new Vec3(0.2, -1, 0), new Vec3(0.2, 100000, 0), out _, out Vec3 to));
            Assert.True(map.IsInDepth(to));
        }

        [Fact]
        public void TheOutlineOfAPlane_IsPickedAsALine()
        {
            Scene scene = new SceneWriter().Add(new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1))).Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0))).Scene;
            Primitives primitives = Primitives.Build(scene);
            ExactGeometry geometry = ExactGeometry.Build(scene, primitives);
            var camera = new ViewCamera();
            camera.SetView(StandardView.Top);
            camera.Fit(primitives.Bounds, 1.25);
            var map = new ScreenMap(camera, primitives.Bounds, 1000, 800);

            // An edge of the patch, seen from above with no surfaces to pick: it is a line of the plane.
            Assert.Contains(geometry.Curves, c => c.ItemIndex == 0);
            SceneCurve edge = geometry.Curves.First(c => c.ItemIndex == 0 && c.Shape == CurveShape.Segment);
            (double x, double y) = Project(map, (edge.Start + edge.End) * 0.5);
            PickHit hit = PickEngine.Pick(primitives, geometry, map, x + 1.0, y, i => i == 0, false);

            Assert.Equal(0, hit.ItemIndex);
            Assert.Equal(PickKind.Curve, hit.Kind);
        }

        private static (double, double) Project(ScreenMap map, Vec3 point)
        {
            Assert.True(map.TryProject(point, out double x, out double y, out _));
            return (x, y);
        }

        [Fact]
        public void AnEdgeTwoFacesShare_IsOneCurve_InTheOrderItWasFirstGiven()
        {
            GeoSolid3 box = GeoSolid3.Extrude(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0)), new GeoVector3(0, 0, 5));

            ExactGeometry a = Exact(new SceneWriter().Add(box));
            ExactGeometry b = Exact(new SceneWriter().Add(box));

            Assert.Equal(12, a.Curves.Count);
            Assert.Equal(a.Curves.Select(c => c.Start).ToArray(), b.Curves.Select(c => c.Start).ToArray());

            // Two triangles of a mesh that share an edge.
            var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            ExactGeometry mesh = Exact(new SceneWriter().Add(square.ToMesh(GeometryHelper.Meshing.MeshKind.Triangles)));
            Assert.Equal(5, mesh.Curves.Count);
        }

        [Fact]
        public void ACrossingAtAnEnd_IsStillACrossing_WhenEndsAreNotSnappedTo()
        {
            // The end of the stem lies on the bar.
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0)))
                .Add(new GeoLine2(new GeoPoint2(50, -40), new GeoPoint2(50, 0))));
            (double x, double y) = view.Pixel(new Vec3(50, 0, 0));

            SnapHit all = SnapEngine.Find(view.Geometry, view.Map, x + 1, y, SnapKinds.All, SnapEngine.DefaultRadius, 1E-3, null);
            SnapHit crossing = SnapEngine.Find(view.Geometry, view.Map, x + 1, y, SnapKinds.Intersection, SnapEngine.DefaultRadius, 1E-3, null);

            Assert.Equal("end", all.Name);
            Assert.Equal("intersection", crossing.Name);
            ViewFixture.AssertNear(new Vec3(50, 0, 0), crossing.Point);
        }

        [Fact]
        public void ThePointToTurnAbout_FoundOnNothing_IsOnThePlaneThroughTheTargetSquareToTheView_NotOnZZero()
        {
            var view = ViewFixture.Iso(new SceneWriter().Add(new GeoPoint3(0, 0, 500)).Add(new GeoPoint3(100, 100, 600)));
            ResolvedPoint ground = PickEngine.ResolvePoint(view.Primitives, view.Geometry, view.Map, 500, 100, SnapKinds.All, 1E-3, null, true);
            ResolvedPoint turning = PickEngine.ResolvePoint(view.Primitives, view.Geometry, view.Map, 500, 100, SnapKinds.All, 1E-3, null, true, false);

            Assert.Equal("plane z = 0", ground.Source);
            Assert.Equal("view plane", turning.Source);
            ViewFixture.AssertNear(view.Map.PointOnTargetPlane(500, 100), turning.Point, 1E-9);
        }
    }
}
