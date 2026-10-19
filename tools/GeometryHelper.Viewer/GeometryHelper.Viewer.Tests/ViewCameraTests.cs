using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ViewCameraTests
    {
        private static void AssertVector(Vec3 expected, Vec3 actual, double tolerance = 1E-9)
        {
            Assert.True((expected - actual).Length < tolerance, expected + " expected, " + actual + " found");
        }

        private static IEnumerable<Vec3> Corners(Bounds3 b)
        {
            for (int i = 0; i < 8; i++)
            {
                yield return new Vec3((i & 1) == 0 ? b.Min.X : b.Max.X, (i & 2) == 0 ? b.Min.Y : b.Max.Y, (i & 4) == 0 ? b.Min.Z : b.Max.Z);
            }
        }

        /// <summary>
        /// Gets where a point shows on the screen, from -1 to 1 across and up the view.
        /// </summary>
        private static (double X, double Y) Project(ViewCamera camera, Vec3 point, double aspect, Bounds3 scene)
        {
            double tan = camera.HalfHeightRatio;
            Vec3 eye = camera.EyePosition(scene);
            Vec3 d = point - eye;
            double depth = Vec3.Dot(d, camera.Forward);

            if (camera.Orthographic)
            {
                double half = camera.Distance * tan;
                return (Vec3.Dot(d, camera.Right) / (half * aspect), Vec3.Dot(d, camera.Up) / half);
            }

            return (Vec3.Dot(d, camera.Right) / (depth * tan * aspect), Vec3.Dot(d, camera.Up) / (depth * tan));
        }

        [Fact]
        public void TheStandardViews_LookAlongTheAxesWithTheirTopsUp()
        {
            var camera = new ViewCamera();

            camera.SetView(StandardView.Top);
            AssertVector(new Vec3(0, 0, -1), camera.Forward);
            AssertVector(new Vec3(0, 1, 0), camera.Up);
            AssertVector(new Vec3(1, 0, 0), camera.Right);
            Assert.True(camera.Orthographic);

            camera.SetView(StandardView.Front);
            AssertVector(new Vec3(0, 1, 0), camera.Forward);
            AssertVector(new Vec3(0, 0, 1), camera.Up);
            AssertVector(new Vec3(1, 0, 0), camera.Right);

            camera.SetView(StandardView.Right);
            AssertVector(new Vec3(-1, 0, 0), camera.Forward);
            AssertVector(new Vec3(0, 0, 1), camera.Up);
            AssertVector(new Vec3(0, 1, 0), camera.Right);

            camera.SetView(StandardView.Iso);
            Assert.False(camera.Orthographic);
            // The camera stands at +X, -Y and above, so it looks towards -X, +Y and down.
            Assert.True(camera.Forward.Z < 0.0 && camera.Forward.X < 0.0 && camera.Forward.Y > 0.0);
            Assert.True(camera.Up.Z > 0.0);
        }

        [Fact]
        public void TheAxesOfTheView_AreSquareToEachOtherAtAnyAngle()
        {
            var camera = new ViewCamera();

            foreach (double azimuth in new[] { -3.0, -1.0, 0.0, 0.5, 2.0, 3.1 })
            {
                foreach (double elevation in new[] { -Math.PI / 2.0, -1.0, 0.0, 0.7, Math.PI / 2.0 })
                {
                    camera.Azimuth = azimuth;
                    camera.Elevation = elevation;

                    Assert.Equal(1.0, camera.Forward.Length, 12);
                    Assert.Equal(1.0, camera.Up.Length, 12);
                    Assert.Equal(1.0, camera.Right.Length, 12);
                    Assert.Equal(0.0, Vec3.Dot(camera.Forward, camera.Up), 12);
                    Assert.Equal(0.0, Vec3.Dot(camera.Forward, camera.Right), 12);
                    Assert.Equal(0.0, Vec3.Dot(camera.Up, camera.Right), 12);
                }
            }
        }

        [Fact]
        public void TheElevation_IsKeptWithinAQuarterTurnEitherWay()
        {
            var camera = new ViewCamera { Elevation = 10.0 };
            Assert.Equal(Math.PI / 2.0, camera.Elevation);

            camera.Elevation = -10.0;
            Assert.Equal(-Math.PI / 2.0, camera.Elevation);
        }

        [Theory]
        [InlineData(false, 1.6)]
        [InlineData(true, 1.6)]
        [InlineData(false, 0.5)]
        [InlineData(true, 0.5)]
        public void Fit_TakesInEveryCornerOfTheBoxAndNoMoreThanAFewPercentOfRoom(bool orthographic, double aspect)
        {
            var box = new Bounds3(new Vec3(-300, 120, 40), new Vec3(5200, 900, 380));
            var camera = new ViewCamera { Orthographic = orthographic, Azimuth = -0.6, Elevation = 0.5 };

            camera.Fit(box, aspect);

            var shown = Corners(box).Select(c => Project(camera, c, aspect, box)).ToList();
            double reach = shown.Max(s => Math.Max(Math.Abs(s.X), Math.Abs(s.Y)));

            Assert.InRange(reach, 0.8, 1.0000001);
            AssertVector(box.Center, camera.Target);
        }

        [Fact]
        public void Fit_OfAPointOrNothing_LeavesASaneViewOrNone()
        {
            var camera = new ViewCamera { Distance = 777.0 };

            camera.Fit(default(Bounds3), 1.5);
            Assert.Equal(777.0, camera.Distance);

            camera.Fit(new Bounds3(new Vec3(4, 5, 6), new Vec3(4, 5, 6)), 1.5);
            AssertVector(new Vec3(4, 5, 6), camera.Target);
            Assert.True(camera.Distance > 0.0);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ZoomAt_KeepsThePointUnderTheCursorWhereItWas(bool orthographic)
        {
            var camera = new ViewCamera { Orthographic = orthographic, Azimuth = 0.4, Elevation = 0.8, Distance = 900.0, Target = new Vec3(10, 20, 30) };
            Vec3 under = camera.PointOnTargetPlane(0.3, -0.4, 1.7);

            camera.ZoomAt(1.25, 0.3, -0.4, 1.7);

            Assert.True(camera.Distance < 900.0);
            AssertVector(under, camera.PointOnTargetPlane(0.3, -0.4, 1.7));

            camera.ZoomAt(0.5, 0.3, -0.4, 1.7);
            AssertVector(under, camera.PointOnTargetPlane(0.3, -0.4, 1.7));
        }

        [Fact]
        public void ZoomAt_TheMiddleOfTheView_OnlyChangesTheDistance()
        {
            var camera = new ViewCamera { Distance = 800.0, Target = new Vec3(1, 2, 3) };

            camera.ZoomAt(2.0, 0.0, 0.0, 1.5);

            Assert.Equal(400.0, camera.Distance, 9);
            AssertVector(new Vec3(1, 2, 3), camera.Target);
        }

        [Fact]
        public void Pan_MovesTheSceneWithTheMouse()
        {
            var camera = new ViewCamera { Azimuth = 0.3, Elevation = 0.4, Distance = 1500.0, Target = new Vec3(100, 200, 300) };
            const double Aspect = 1.5;
            const double Height = 600.0;
            Vec3 under = camera.PointOnTargetPlane(0.2, 0.1, Aspect);

            camera.Pan(120.0, -45.0, Height);

            // The point that was under the cursor is now 120 pixels to the right and 45 pixels up of where it was.
            double ndcX = 0.2 + 2.0 * 120.0 / (Aspect * Height);
            double ndcY = 0.1 + 2.0 * 45.0 / Height;
            AssertVector(under, camera.PointOnTargetPlane(ndcX, ndcY, Aspect));
        }

        [Fact]
        public void PanBy_MovesTheViewByAShareOfItsSize()
        {
            var camera = new ViewCamera { Distance = 1000.0, Azimuth = -Math.PI / 2.0, Elevation = 0.0 };
            double height = 2.0 * 1000.0 * camera.HalfHeightRatio;

            camera.PanBy(0.1, 0.0, 2.0);

            AssertVector(new Vec3(0.1 * height * 2.0, 0, 0), camera.Target);
        }

        [Fact]
        public void Orbit_DraggingRightLowersTheAzimuth_AndDraggingDownRaisesTheElevation()
        {
            var camera = new ViewCamera { Azimuth = 0.0, Elevation = 0.0 };

            camera.Orbit(100.0, 50.0);

            Assert.Equal(-100.0 * 0.4 * Math.PI / 180.0, camera.Azimuth, 12);
            Assert.Equal(50.0 * 0.4 * Math.PI / 180.0, camera.Elevation, 12);
        }

        [Fact]
        public void TheRayThroughTheMiddleOfTheView_PassesThroughTheTarget()
        {
            var box = new Bounds3(new Vec3(-1000, -1000, -1000), new Vec3(1000, 1000, 1000));

            foreach (bool orthographic in new[] { false, true })
            {
                var camera = new ViewCamera { Orthographic = orthographic, Azimuth = 1.0, Elevation = 0.3, Distance = 500.0, Target = new Vec3(5, 6, 7) };

                camera.GetRay(0.0, 0.0, 1.3, box, out Vec3 origin, out Vec3 direction);

                AssertVector(camera.Forward, direction);

                // A perspective ray starts at the eye; an orthographic one at the target plane, and runs both ways.
                AssertVector(camera.Target, orthographic ? origin : origin + direction * camera.EyeDistance(box));
            }
        }

        [Fact]
        public void TheRayThroughACursor_PassesThroughThePointOnTheTargetPlane()
        {
            var box = new Bounds3(new Vec3(-1000, -1000, -1000), new Vec3(1000, 1000, 1000));

            foreach (bool orthographic in new[] { false, true })
            {
                var camera = new ViewCamera { Orthographic = orthographic, Azimuth = 1.0, Elevation = 0.3, Distance = 500.0, Target = new Vec3(5, 6, 7) };
                Vec3 onPlane = camera.PointOnTargetPlane(-0.6, 0.35, 1.3);

                camera.GetRay(-0.6, 0.35, 1.3, box, out Vec3 origin, out Vec3 direction);

                Vec3 toPoint = onPlane - origin;
                AssertVector(direction * Vec3.Dot(toPoint, direction), toPoint);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheClipPlanes_HoldTheWholeSceneInFrontOfTheCamera(bool orthographic)
        {
            var box = new Bounds3(new Vec3(0, 0, 0), new Vec3(60000, 30000, 12000));
            var camera = new ViewCamera { Orthographic = orthographic };
            camera.Fit(box, 1.6);

            camera.ClipPlanes(box, out double near, out double far);

            Assert.True(near > 0.0);
            Assert.True(far > near);
            Assert.True(far / near < 1E7);

            Vec3 eye = camera.EyePosition(box);

            foreach (Vec3 corner in Corners(box))
            {
                double depth = Vec3.Dot(corner - eye, camera.Forward);
                Assert.InRange(depth, near, far);
            }
        }

        [Fact]
        public void TheClipPlanes_StayBehindAPointZoomedInOn()
        {
            var box = new Bounds3(new Vec3(0, 0, 0), new Vec3(60000, 30000, 12000));
            var camera = new ViewCamera { Target = new Vec3(100, 100, 100), Distance = 2.0 };

            camera.ClipPlanes(box, out double near, out _);

            Assert.True(near <= camera.Distance * 0.1 + 1E-12);
        }

        [Fact]
        public void TheDistance_IsKeptBetweenItsBounds()
        {
            var camera = new ViewCamera { Distance = -5.0 };
            Assert.Equal(ViewCamera.MinDistance, camera.Distance);

            camera.Distance = 1E30;
            Assert.Equal(ViewCamera.MaxDistance, camera.Distance);

            camera.Distance = 10.0;
            camera.Zoom(0.0);
            camera.Zoom(double.NaN);
            Assert.Equal(10.0, camera.Distance);
        }

        [Fact]
        public void Bounds_IncludeSkipsAPointThatIsNotANumber_AndAnEmptyBoxHasNoCentre()
        {
            Bounds3 box = default(Bounds3).Include(new Vec3(double.NaN, 0, 0)).Include(new Vec3(1, 2, 3)).Include(new Vec3(-1, 5, 0));

            Assert.False(box.IsEmpty);
            Assert.Equal(new Vec3(-1, 2, 0), box.Min);
            Assert.Equal(new Vec3(1, 5, 3), box.Max);
            Assert.True(default(Bounds3).IsEmpty);
            Assert.Equal(default(Vec3), default(Bounds3).Center);
        }
    }
}
