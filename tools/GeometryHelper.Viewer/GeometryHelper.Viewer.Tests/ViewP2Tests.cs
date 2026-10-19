using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ViewP2Tests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void APointOfTheScene_ShowsWhereTheRayThroughThatPlaceComesBackToIt(bool orthographic)
        {
            var box = new Bounds3(new Vec3(0, 0, 0), new Vec3(1000, 600, 300));
            var camera = new ViewCamera { Orthographic = orthographic, Azimuth = 0.7, Elevation = 0.5 };
            camera.Fit(box, 1.25);
            var map = new ScreenMap(camera, box, 1000.0, 800.0);

            foreach (var point in new[] { new Vec3(100, 200, 50), new Vec3(900, 50, 250), new Vec3(500, 300, 150) })
            {
                Assert.True(map.TryProject(point, out double x, out double y, out double depth));
                Assert.True(depth > 0.0);
                map.GetRay(x, y, out Vec3 origin, out Vec3 direction);
                Vec3 toPoint = point - origin;
                Assert.True((toPoint - direction * Vec3.Dot(toPoint, direction)).Length < 1E-9, "the ray misses " + point);
            }

            // The target is the middle of the picture.
            Assert.True(map.TryProject(camera.Target, out double cx, out double cy, out _));
            Assert.Equal(500.0, cx, 9);
            Assert.Equal(400.0, cy, 9);
        }

        [Fact]
        public void InPerspective_APointBehindTheCameraIsNotInThePicture_AndASegmentIsCutAtTheNearPlane()
        {
            var box = new Bounds3(new Vec3(-1000, -1000, -1000), new Vec3(1000, 1000, 1000));
            var camera = new ViewCamera { Azimuth = 0.0, Elevation = 0.0, Distance = 100.0, Target = new Vec3(0, 0, 0) };
            var map = new ScreenMap(camera, box, 800.0, 600.0);

            // The camera stands at x = 100 looking along -X.
            Assert.False(map.TryProject(new Vec3(500, 0, 0), out _, out _, out _));
            Assert.True(map.TryProject(new Vec3(-500, 0, 0), out _, out _, out _));

            Assert.True(map.ClipSegment(new Vec3(500, 0, 0), new Vec3(-500, 0, 0), out Vec3 from, out Vec3 to));
            Assert.Equal(100.0 - map.ClipDepth, from.X, 9);
            Assert.True(map.TryProject(from, out _, out _, out _), "the end of a segment cut at the near plane is in the picture");
            Assert.Equal(-500.0, to.X);
            Assert.False(map.ClipSegment(new Vec3(500, 0, 0), new Vec3(300, 0, 0), out _, out _));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TurningAboutAPoint_KeepsThatPointWhereItIsOnTheScreen(bool orthographic)
        {
            var box = new Bounds3(new Vec3(0, 0, 0), new Vec3(1000, 600, 300));
            var camera = new ViewCamera { Orthographic = orthographic, Azimuth = 0.4, Elevation = 0.3 };
            camera.Fit(box, 1.25);
            Vec3 centre = new Vec3(900, 100, 250);

            camera.PanBy(0.1, -0.05, 1.25);
            new ScreenMap(camera, box, 1000.0, 800.0).TryProject(centre, out double x0, out double y0, out double d0);
            Vec3 target = camera.Target;

            camera.OrbitAbout(centre, 120.0, -45.0);
            camera.RotateAbout(centre, 0.9, 0.2);

            Assert.NotEqual(target, camera.Target);
            Assert.True(new ScreenMap(camera, box, 1000.0, 800.0).TryProject(centre, out double x1, out double y1, out double d1));
            Assert.Equal(x0, x1, 7);
            Assert.Equal(y0, y1, 7);

            // In perspective the point also stays as far from the eye; an orthographic eye stands where the depth of the scene puts it, which a turn changes.
            if (!orthographic)
            {
                Assert.Equal(d0, d1, 7);
            }
        }

        [Fact]
        public void TurningAboutTheTarget_IsTurningTheCameraAsItWas()
        {
            var turned = new ViewCamera { Target = new Vec3(10, 20, 30), Distance = 500.0 };
            var plain = turned.Clone();

            turned.OrbitAbout(turned.Target, 50.0, 20.0);
            plain.Orbit(50.0, 20.0);

            Assert.Equal(plain.Azimuth, turned.Azimuth);
            Assert.Equal(plain.Elevation, turned.Elevation);
            Assert.True((plain.Target - turned.Target).Length < 1E-9);
        }

        [Fact]
        public void ZoomedDeepIntoALargeScene_ThePerspectiveDepthRangeIsBounded_AndTheTargetIsStillSeen()
        {
            var box = new Bounds3(new Vec3(0, 0, 0), new Vec3(60000, 30000, 12000));

            foreach (double distance in new[] { 0.5, 2.0, 50.0, 3000.0 })
            {
                var camera = new ViewCamera { Target = new Vec3(100, 100, 100), Distance = distance };
                camera.ClipPlanes(box, out double near, out double far);

                Assert.True(far / near <= ViewCamera.MaxDepthRatio * (1.0 + 1E-12), "ratio " + far / near + " at " + distance);
                Assert.True(near <= distance * 0.01 + 1E-15);
                Assert.True(far >= distance * 2.0, "the target and what is behind it up to its own distance are kept");
            }

            // 2 mm from the target the far plane is 20 m away, so the 60 m box is cut there.
            var close = new ViewCamera { Target = new Vec3(100, 100, 100), Distance = 2.0, Azimuth = Math.PI, Elevation = 0.0 };
            close.ClipPlanes(box, out _, out double farOfClose);
            Assert.Equal(2.0 * 0.01 * ViewCamera.MaxDepthRatio, farOfClose, 6);
        }

        [Fact]
        public void ThePerspectiveFarPlane_ReachesTheFarthestCornerInFront_NotTheSphereAroundTheScene()
        {
            // The camera stands inside a box 1000 across, 500 from the far wall: the sphere round the box reaches 909, the far wall 500.
            var box = new Bounds3(new Vec3(0, 0, 0), new Vec3(1000, 1000, 1000));
            var camera = new ViewCamera { Azimuth = 0.0, Elevation = 0.0, Distance = 100.0, Target = new Vec3(400, 500, 500) };

            camera.ClipPlanes(box, out double near, out double far);

            Vec3 eye = camera.Position;
            double deepest = new[] { 0.0, 1000.0 }.SelectMany(x => new[] { 0.0, 1000.0 }.SelectMany(y => new[] { 0.0, 1000.0 }.Select(z => Vec3.Dot(new Vec3(x, y, z) - eye, camera.Forward)))).Max();
            Assert.Equal(deepest * 1.05, far, 9);
            Assert.True(far < 600.0);
            Assert.True(near > 0.0);
        }

        [Fact]
        public void ASceneBehindTheCamera_AddsNothingToTheFarPlane()
        {
            var behind = new Bounds3(new Vec3(200, -50, -50), new Vec3(300, 50, 50));
            var camera = new ViewCamera { Azimuth = 0.0, Elevation = 0.0, Distance = 100.0, Target = new Vec3(0, 0, 0) };

            camera.ClipPlanes(behind, out _, out double far);

            Assert.Equal(100.0 * 2.0 * 1.05, far, 9);
        }

        [Fact]
        public void ASceneTooBigToDrawAtItsChordTolerance_HasItsCurvesCutAtOneCoarserTolerance()
        {
            // Radius 60 000 takes 54 414 pieces at the default chord tolerance, 500 takes 4 968, and 10 takes 703 (the sagitta of a 2 degree piece of it, 1.5E-3, is over the tolerance).
            Scene scene = new SceneWriter()
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 10.0), name: "small")
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 60000.0), name: "huge")
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 500.0), name: "medium")
                .Scene;

            Primitives within = Primitives.Build(scene);
            Assert.Equal(54414, within.Items[1].Polylines[0].Points.Length);
            Assert.Equal(4968, within.Items[2].Polylines[0].Points.Length);
            Assert.Empty(within.Warnings);

            // A cap the fine scene breaks: all the circles are cut at the one tolerance, the finest that fits, and the warning names it.
            Primitives capped = Primitives.Build(scene, 20000);

            int total = capped.Items.Sum(item => item.Polylines[0].Points.Length);
            Assert.InRange(total, 19000, 20000);
            Assert.InRange(capped.Items[0].Polylines[0].Points.Length, 180, within.Items[0].Polylines[0].Points.Length - 1);
            Assert.True(capped.Items[1].Polylines[0].Points.Length < 54414 && capped.Items[1].Polylines[0].Points.Length > 180);
            Assert.True(capped.Items[2].Polylines[0].Points.Length <= 4968);
            string warning = Assert.Single(capped.Warnings);
            Assert.Contains("over the cap of 20000", warning, StringComparison.Ordinal);
            Assert.Contains("chord tolerance of", warning, StringComparison.Ordinal);

            // The box round the scene is the exact curve's all the same.
            Assert.Equal(within.Bounds.Min, capped.Bounds.Min);
            Assert.Equal(within.Bounds.Max, capped.Bounds.Max);
        }
        [Fact]
        public void ACapTheCurvesCannotMeet_CutsEveryCurveAndSaysThePointsAreStillOver()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 500.0))
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 600.0))
                .Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10)))
                .Scene;

            Primitives capped = Primitives.Build(scene, 100);

            Assert.Equal(180, capped.Items[0].Polylines[0].Points.Length);
            Assert.Equal(180, capped.Items[1].Polylines[0].Points.Length);
            Assert.Equal(4, capped.Items[2].Polylines[0].Points.Length);
            Assert.Contains(capped.Warnings, w => w.Contains("still over the cap", StringComparison.Ordinal));
        }

        [Fact]
        public void TheDefaultCap_IsFourMillionPoints()
        {
            Assert.Equal(4000000, Primitives.MaxPoints);
        }
    }
}
