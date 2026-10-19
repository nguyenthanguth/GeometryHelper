using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// A face that lies in a clip plane is on it, whatever the rounding of its corners: one slack decides it for the drawing, the picking, the snaps, the box
    /// select and the sections.
    /// </summary>
    public class ClipSlackTests
    {
        /// <summary>
        /// A point of a box 300 by 200 by 100 turned about all three axes, and moved by an offset along the way, as a model far from the origin is.
        /// </summary>
        private static GeoPoint3 Skew(double x, double y, double z, double offset)
        {
            const double A = 0.37;
            const double B = 0.91;
            const double C = 1.3;
            double y1 = y * Math.Cos(A) - z * Math.Sin(A);
            double z1 = y * Math.Sin(A) + z * Math.Cos(A);
            double x2 = x * Math.Cos(B) + z1 * Math.Sin(B);
            double z2 = -x * Math.Sin(B) + z1 * Math.Cos(B);
            double x3 = x2 * Math.Cos(C) - y1 * Math.Sin(C);
            double y3 = x2 * Math.Sin(C) + y1 * Math.Cos(C);
            return new GeoPoint3(x3 + offset, y3 - offset * 0.7, z2 + offset * 0.3);
        }

        private static Primitives SkewedBox(double offset)
        {
            var floor = new GeoPolygon3(Skew(0, 0, 0, offset), Skew(300, 0, 0, offset), Skew(300, 200, 0, offset), Skew(0, 200, 0, offset));
            GeoPoint3 o = Skew(0, 0, 0, offset);
            GeoPoint3 up = Skew(0, 0, 100, offset);
            GeoSolid3 solid = GeoSolid3.Extrude(floor, new GeoVector3(up.X - o.X, up.Y - o.Y, up.Z - o.Z));
            return Primitives.Build(new SceneWriter("skewed").Add(solid, name: "box").Scene);
        }

        private static double Area(DrawTriangle t) => Vec3.Cross(t.B - t.A, t.C - t.A).Length * 0.5;

        [Theory]
        [InlineData(0.0)]
        [InlineData(5E3)]
        [InlineData(5E5)]
        public void ACtrlKPlaneOnAFaceOfASkewedBox_LeavesEveryFaceWhole_AndThePickAndTheDrawingAgree(double offset)
        {
            Primitives scene = SkewedBox(offset);
            ItemPrimitives item = scene.Items[0];
            var random = new Random(7);
            int trials = 0;

            foreach (DrawTriangle t in item.Triangles)
            {
                List<DrawTriangle> face = item.Triangles.Where(u => (u.Normal - t.Normal).Length < 1E-9).ToList();
                double faceArea = face.Sum(Area);

                for (int k = 0; k < 40; k++)
                {
                    double u = random.NextDouble();
                    double v = random.NextDouble();

                    if (u + v > 1.0)
                    {
                        u = 1.0 - u;
                        v = 1.0 - v;
                    }

                    Vec3 point = t.A + (t.B - t.A) * u + (t.C - t.A) * v;

                    // Looking at the face square on, as Ctrl+K is done, and clicking where the point shows.
                    ViewCamera camera = new SavedView("face", point + t.Normal * 500.0, point, new Vec3(0, 0, 1), true, 400.0).ToCamera(800.0 / 600.0);
                    var map = new ScreenMap(camera, scene.Bounds, 800.0, 600.0);
                    Assert.True(map.TryProject(point, out double x, out double y, out _));
                    PickHit hit = PickEngine.Pick(scene, null, map, x, y, null, true);
                    Assert.NotNull(hit);
                    Assert.Equal(PickKind.Surface, hit.Kind);

                    Vec3 away = Vec3.Dot(hit.Normal, camera.Forward) < 0.0 ? -hit.Normal : hit.Normal;
                    var clips = new ClipSet(new[] { new ClipPlane(hit.Point, away) });
                    ClipResult result = ClipResult.Build(scene, clips);

                    double kept = result.Clipped.Items[0].Triangles.Where(q => (q.Normal - t.Normal).Length < 1E-9).Sum(Area);
                    Assert.True(Math.Abs(kept - faceArea) <= 1E-9 * faceArea, "the face lost " + (faceArea - kept) + " of " + faceArea + " at offset " + offset);

                    // What is drawn is what is picked: the click, through the cut scene, is on the same face again.
                    var cutMap = new ScreenMap(camera, scene.Bounds, 800.0, 600.0, clips);
                    PickHit again = PickEngine.Pick(scene, null, cutMap, x, y, null, true);
                    Assert.NotNull(again);
                    Assert.True((again.Point - hit.Point).Length < 1E-6);
                    trials++;
                }
            }

            Assert.Equal(12 * 40, trials);
        }

        [Fact]
        public void TheDrawingAndThePickingAndTheSections_AreCutWithOneSlack_ThatOfTheScene()
        {
            Primitives scene = SkewedBox(5E3);
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 0), new Vec3(0, 0, 1)) });
            ViewCamera camera = new ViewCamera();
            camera.SetView(StandardView.Top);

            ClipResult result = ClipResult.Build(scene, clips);
            var map = new ScreenMap(camera, scene.Bounds, 800.0, 600.0, clips);

            Assert.True(result.Clips.Slack > 0.0);
            Assert.Equal(ClipSet.SlackFor(scene.Bounds), result.Clips.Slack);
            Assert.Equal(result.Clips.Slack, map.Clip.Slack);

            // A set made with no scene takes the slack of the box of what it cuts, and a set made for a scene keeps its own.
            Assert.Equal(0.0, clips.Slack);
            Assert.Equal(0.5, clips.WithSlack(0.5).Add(new ClipPlane(default(Vec3), new Vec3(1, 0, 0))).Slack);
            Assert.Equal(0.0, ClipSet.SlackFor(default(Bounds3)));
            Assert.Throws<ArgumentException>(() => clips.WithSlack(-1.0));
        }

        [Fact]
        public void AFaceOffThePlaneByMoreThanTheSlack_IsCutAsItWas_AndOneOffByLessIsOnIt()
        {
            Primitives box = Primitives.Build(new SceneWriter().Add(SampleShapes.Box(30, 20, 10, false)).Scene);
            double slack = ClipSet.SlackFor(box.Bounds);

            // Keep what is below z = 10 - 0.001: the top face is 0.001 above it, a million times the slack, and is cut away.
            ClipResult above = ClipResult.Build(box, new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 10.0 - 1E-3), new Vec3(0, 0, -1)) }));
            Assert.DoesNotContain(above.Clipped.Items[0].Triangles, t => t.A.Z > 10.0 - 1E-3 + 1E-9 && t.B.Z > 10.0 - 1E-3 + 1E-9 && t.C.Z > 10.0 - 1E-3 + 1E-9);

            // Keep what is below z = 10 - slack/2: the top face is on the plane to the slack, and is kept whole.
            ClipResult on = ClipResult.Build(box, new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 10.0 - slack / 2.0), new Vec3(0, 0, -1)) }));
            Assert.Equal(box.Items[0].Triangles.Count, on.Clipped.Items[0].Triangles.Count);
            Assert.Equal(box.Items[0].Triangles.Sum(Area), on.Clipped.Items[0].Triangles.Sum(Area), 9);
        }

        [Fact]
        public void ALineCutByAPlane_IsAsLongAsThePlaneLeavesIt_NotLongerByTheSlack()
        {
            // The section of a box cut by z = 5, cut again by x >= 10.0000004 (a slack and a bit beyond the box's own corner): its length is exact.
            Primitives box = Primitives.Build(new SceneWriter().Add(SampleShapes.Box(30, 20, 10, false)).Scene);
            ClipSet clips = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 5), new Vec3(0, 0, 1)), new ClipPlane(new Vec3(10.0000004, 0, 0), new Vec3(1, 0, 0)) });

            ClipResult result = ClipResult.Build(box, clips);

            // z = 5 cut at x = 10.0000004: three sides of 19.9999996, 20, 19.9999996; and x = 10.0000004 cut at z = 5: 5, 20, 5.
            double expected = 19.9999996 + 20.0 + 19.9999996 + 5.0 + 20.0 + 5.0;
            Assert.Equal(expected, SectionBuilder.Length(result.Sections[0]), 11);
        }

        [Fact]
        public void ACancelledCut_StopsInsideAnItemOfManyTriangles_NotOnlyBetweenItems()
        {
            var writer = new SceneWriter();
            writer.Add(GeoSolid3.Cylinder(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0), 20.0, 800), name: "many");
            Primitives scene = Primitives.Build(writer.Scene);
            Assert.True(scene.Items[0].Triangles.Count > 2048);
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(0, 50, 0), new Vec3(0, 1, 0)) });
            ClipSet forScene = clips.ForScene(scene.Bounds);
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();

                Assert.Throws<OperationCanceledException>(() => forScene.Apply(scene.Items[0], cancelled.Token));
                Assert.Throws<OperationCanceledException>(() => SectionBuilder.Chains(scene.Items[0], clips.Planes[0], 1E-9, cancelled.Token));
                Assert.Throws<OperationCanceledException>(() => ClipResult.Build(scene, clips, cancelled.Token));
            }

            // The same item cut with no token is cut.
            Assert.NotEmpty(forScene.Apply(scene.Items[0]).Triangles);
        }
    }
}
