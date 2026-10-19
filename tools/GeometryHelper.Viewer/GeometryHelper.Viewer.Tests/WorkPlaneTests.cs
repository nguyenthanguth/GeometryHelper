using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class WorkPlaneTests
    {
        private static Vec3 RandomPoint(Random random, double scale)
            => new Vec3((random.NextDouble() * 2.0 - 1.0) * scale, (random.NextDouble() * 2.0 - 1.0) * scale, (random.NextDouble() * 2.0 - 1.0) * scale);

        private static void AssertNear(Vec3 expected, Vec3 actual, double tolerance)
        {
            double gap = (expected - actual).Length;
            Assert.True(gap <= tolerance, expected + " expected, " + actual + " found, " + gap + " apart");
        }

        [Fact]
        public void APlaneMadeOfThreePoints_HasPerpendicularUnitAxes_AndTheOriginAndTheXPointWhereTheyWereGiven()
        {
            var origin = new Vec3(120.5, -40.25, 33.125);
            var onX = new Vec3(220.5, 60.0, 33.125);
            var inPlane = new Vec3(100.0, -40.25, 133.0);

            WorkPlane plane = WorkPlane.FromThreePoints(origin, onX, inPlane);

            Assert.Equal(1.0, plane.XAxis.Length, 14);
            Assert.Equal(1.0, plane.YAxis.Length, 14);
            Assert.Equal(1.0, plane.ZAxis.Length, 14);
            Assert.Equal(0.0, Vec3.Dot(plane.XAxis, plane.YAxis), 14);
            Assert.Equal(0.0, Vec3.Dot(plane.XAxis, plane.ZAxis), 14);
            Assert.Equal(0.0, Vec3.Dot(plane.YAxis, plane.ZAxis), 14);

            // Right-handed: x cross y is z.
            AssertNear(plane.ZAxis, Vec3.Cross(plane.XAxis, plane.YAxis), 1E-14);

            AssertNear(default(Vec3), plane.ToLocal(origin), 1E-12);
            Vec3 x = plane.ToLocal(onX);
            Assert.Equal((onX - origin).Length, x.X, 12);
            Assert.Equal(0.0, x.Y, 12);
            Assert.Equal(0.0, x.Z, 12);

            // The third point is in the plane, on the side the y axis points to.
            Vec3 third = plane.ToLocal(inPlane);
            Assert.Equal(0.0, third.Z, 12);
            Assert.True(third.Y > 0.0);
        }

        [Fact]
        public void APointTakenToAPlaneAndBack_IsWhereItWas_To1EMinus12()
        {
            var random = new Random(20260519);
            double worst = 0.0;

            for (int i = 0; i < 400; i++)
            {
                WorkPlane plane = WorkPlane.FromThreePoints(RandomPoint(random, 1000.0), RandomPoint(random, 1000.0), RandomPoint(random, 1000.0));
                Vec3 world = RandomPoint(random, 1000.0);

                worst = Math.Max(worst, (plane.ToWorld(plane.ToLocal(world)) - world).Length);
                worst = Math.Max(worst, (plane.DirectionToWorld(plane.DirectionToLocal(world)) - world).Length);
                worst = Math.Max(worst, (plane.ToLocal(plane.ToWorld(world)) - world).Length);
            }

            Assert.True(worst <= 1E-12, "the worst round trip was " + worst);
        }

        [Fact]
        public void APlaneMadeOfThreePointsOnALine_OrOfTwoThatAreOne_IsRefused()
        {
            var a = new Vec3(0, 0, 0);

            Assert.Throws<ArgumentException>(() => WorkPlane.FromThreePoints(a, a, new Vec3(0, 5, 0)));
            Assert.Throws<ArgumentException>(() => WorkPlane.FromThreePoints(a, new Vec3(10, 0, 0), new Vec3(-30, 0, 0)));
            Assert.Throws<ArgumentException>(() => WorkPlane.FromThreePoints(a, new Vec3(10, 0, 0), new Vec3(5, 1E-12, 0)));
            Assert.Throws<ArgumentException>(() => WorkPlane.FromThreePoints(a, new Vec3(10, 0, 0), new Vec3(double.NaN, 0, 0)));
        }

        [Fact]
        public void TheGlobalPlane_IsTheAxesOfTheScene_AndTakesAPointToItselfExactly()
        {
            WorkPlane global = WorkPlane.Global;
            var p = new Vec3(0.1, 1E-300, 123456789.12345678);

            Assert.True(global.IsGlobal);
            Assert.Equal(p, global.ToLocal(p));
            Assert.Equal(p, global.ToWorld(p));
            Assert.Equal("global (z = 0)", global.ToString());
            Assert.True(WorkPlane.FromThreePoints(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0)).IsGlobal);
        }

        [Fact]
        public void APlaneOnAFace_HasTheNormalOfTheFaceAsItsZ_AndTheXAxisNearTheHint()
        {
            var normal = new Vec3(0, 0, -2);
            WorkPlane plane = WorkPlane.FromNormal(new Vec3(5, 6, 7), normal, new Vec3(1, 1, 0));

            AssertNear(new Vec3(0, 0, -1), plane.ZAxis, 1E-14);
            AssertNear(new Vec3(1, 1, 0).Normalized(), plane.XAxis, 1E-14);
            Assert.Equal(1.0, plane.YAxis.Length, 14);
            Assert.Equal("face", plane.Description);

            // A hint square to the plane is no direction in it: the axes of the scene are tried.
            WorkPlane floor = WorkPlane.FromNormal(default(Vec3), new Vec3(0, 0, 1), new Vec3(0, 0, 5));
            AssertNear(new Vec3(1, 0, 0), floor.XAxis, 1E-14);
            WorkPlane wall = WorkPlane.FromNormal(default(Vec3), new Vec3(1, 0, 0), default(Vec3));
            AssertNear(new Vec3(0, 1, 0), wall.XAxis, 1E-14);

            Assert.Throws<ArgumentException>(() => WorkPlane.FromNormal(default(Vec3), default(Vec3), new Vec3(1, 0, 0)));
        }

        [Fact]
        public void APlaneOnTheView_HasTheRightOfTheScreenAsX_TheUpOfItAsY_AndZTowardsTheEye()
        {
            var camera = new ViewCamera();
            camera.SetView(StandardView.Front);

            WorkPlane plane = WorkPlane.FromView(new Vec3(1, 2, 3), camera.Right, camera.Up);

            AssertNear(camera.Right, plane.XAxis, 1E-14);
            AssertNear(camera.Up, plane.YAxis, 1E-14);

            // The camera looks along +Y from the front, so the plane faces it: its z is -forward.
            AssertNear(-camera.Forward, plane.ZAxis, 1E-14);
            Assert.Throws<ArgumentException>(() => WorkPlane.FromView(default(Vec3), new Vec3(1, 0, 0), new Vec3(2, 0, 0)));
        }

        [Fact]
        public void ARayMeetsThePlane_WhereItIs_AndNotWhenItRunsAlongItOrAwayFromIt()
        {
            WorkPlane plane = WorkPlane.FromNormal(new Vec3(0, 0, 7), new Vec3(0, 0, 1), new Vec3(1, 0, 0));

            Assert.True(plane.TryIntersect(new Vec3(3, 4, 50), new Vec3(0, 0, -1), false, out Vec3 hit));
            AssertNear(new Vec3(3, 4, 7), hit, 1E-12);

            // Going the wrong way: refused for a perspective ray, found for an orthographic one that runs both ways.
            Assert.False(plane.TryIntersect(new Vec3(3, 4, 50), new Vec3(0, 0, 1), false, out _));
            Assert.True(plane.TryIntersect(new Vec3(3, 4, 50), new Vec3(0, 0, 1), true, out Vec3 behind));
            AssertNear(new Vec3(3, 4, 7), behind, 1E-12);

            // Along the plane.
            Assert.False(plane.TryIntersect(new Vec3(0, 0, 9), new Vec3(1, 0, 0), true, out _));
        }

        [Fact]
        public void TheGridOfTheGlobalPlane_IsTheGridOfThePlaneZEqualsNoughtThatTheViewerAlwaysDrew()
        {
            // The grid as it was written before the work plane was: from the box, 20 lines across, a round step.
            var box = new Bounds3(new Vec3(-12.5, 3.25, 0), new Vec3(87.5, 63.25, 40));
            double span = Math.Max(box.Size.X, box.Size.Y);
            double step = WorkPlane.NiceStep(Math.Max(span, 1.0) / 20.0);
            double minX = Math.Floor(box.Min.X / step - 1.0) * step;
            double maxX = Math.Ceiling(box.Max.X / step + 1.0) * step;
            double minY = Math.Floor(box.Min.Y / step - 1.0) * step;
            double maxY = Math.Ceiling(box.Max.Y / step + 1.0) * step;
            var expected = new List<Vec3[]>();

            for (double x = minX; x <= maxX + step * 0.5; x += step)
            {
                expected.Add(new[] { new Vec3(x, minY, 0.0), new Vec3(x, maxY, 0.0) });
            }

            for (double y = minY; y <= maxY + step * 0.5; y += step)
            {
                expected.Add(new[] { new Vec3(minX, y, 0.0), new Vec3(maxX, y, 0.0) });
            }

            List<DrawPolyline> lines = WorkPlane.Global.GridLines(box);

            Assert.Equal(expected.Count, lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                Assert.Equal(expected[i][0], lines[i].Points[0]);
                Assert.Equal(expected[i][1], lines[i].Points[1]);
            }

            Assert.Empty(WorkPlane.Global.GridLines(default(Bounds3)));
        }

        [Fact]
        public void TheGridOfATiltedPlane_LiesInThePlane_OnRoundSteps_AndCoversTheScene()
        {
            WorkPlane plane = WorkPlane.FromThreePoints(new Vec3(10, 20, 30), new Vec3(10, 120, 30), new Vec3(-5, 20, 30));
            var box = new Bounds3(new Vec3(0, 0, 0), new Vec3(100, 60, 40));

            List<DrawPolyline> lines = plane.GridLines(box);

            Assert.NotEmpty(lines);

            foreach (DrawPolyline line in lines)
            {
                foreach (Vec3 end in line.Points)
                {
                    Assert.True(Math.Abs(plane.SignedDistance(end)) < 1E-9, "a grid line left the plane");
                }

                Vec3 a = plane.ToLocal(line.Points[0]);
                Vec3 b = plane.ToLocal(line.Points[1]);
                bool alongX = Math.Abs(a.Y - b.Y) < 1E-9;

                // Each line runs along one axis of the plane.
                Assert.True(alongX || Math.Abs(a.X - b.X) < 1E-9);
                double along = Math.Abs(alongX ? b.X - a.X : b.Y - a.Y);
                Assert.True(along > 0.0);
            }

            // The lines reach past the scene on both sides: the corner of the box nearest the origin of the plane and the farthest are inside the grid.
            double lowX = lines.SelectMany(l => l.Points).Min(p => plane.ToLocal(p).X);
            double highX = lines.SelectMany(l => l.Points).Max(p => plane.ToLocal(p).X);
            var corners = new[] { box.Min, box.Max, new Vec3(box.Min.X, box.Max.Y, box.Min.Z), new Vec3(box.Max.X, box.Min.Y, box.Max.Z) };

            Assert.All(corners, c => Assert.InRange(plane.ToLocal(c).X, lowX, highX));
        }

        [Fact]
        public void ADistanceOnAWorkPlane_IsGivenAlongItsAxes_AndTheLengthIsTheSame()
        {
            WorkPlane plane = WorkPlane.FromThreePoints(new Vec3(0, 0, 0), new Vec3(0, 1, 0), new Vec3(-1, 0, 0));
            var a = new Vec3(0, 0, 0);
            var b = new Vec3(3, 4, 12);

            MeasureResult global = Measurements.Distance(a, b);
            MeasureResult onPlane = Measurements.Distance(a, b, plane);

            Assert.Equal(13.0, global.Value, 12);
            Assert.Equal(13.0, onPlane.Value, 12);
            Assert.Equal(5.0, global.SecondValue, 12);
            Assert.Equal(5.0, onPlane.SecondValue, 12);
            Assert.Contains("dx 3, dy 4, dz 12", global.Lines[0], StringComparison.Ordinal);

            // The plane's x is the scene's Y, its y is minus the scene's X: dx 4, dy -3, dz 12.
            Assert.Contains("dx 4, dy -3, dz 12", onPlane.Lines[0], StringComparison.Ordinal);
            Assert.Contains("work plane", onPlane.Lines[0], StringComparison.Ordinal);

            // The global plane given is the same as none, to the bit.
            Assert.Equal(global.Lines[0], Measurements.Distance(a, b, WorkPlane.Global).Lines[0]);
        }

        [Fact]
        public void AClickOnNothing_LandsOnTheWorkPlane_AndOnZEqualsNoughtWhereThereIsNone()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0))).Add(new GeoPoint3(100, 80, 0)));
            (double x, double y) = view.Pixel(new Vec3(50, 40, 0));
            WorkPlane raised = WorkPlane.FromNormal(new Vec3(0, 0, 7), new Vec3(0, 0, 1), new Vec3(1, 0, 0));

            ResolvedPoint ground = PickEngine.ResolvePoint(view.Primitives, view.Geometry, view.Map, x, y, SnapKinds.None, 1E-3, null, true);
            ResolvedPoint onWork = PickEngine.ResolvePoint(view.Primitives, view.Geometry, view.Map, x, y, SnapKinds.None, 1E-3, null, true, true, raised);
            ResolvedPoint globalGiven = PickEngine.ResolvePoint(view.Primitives, view.Geometry, view.Map, x, y, SnapKinds.None, 1E-3, null, true, true, WorkPlane.Global);

            Assert.Equal("plane z = 0", ground.Source);
            Assert.Equal(0.0, ground.Point.Z, 12);
            Assert.Equal("work plane", onWork.Source);
            ViewFixture.AssertNear(new Vec3(50, 40, 7), onWork.Point, 1E-9);
            Assert.Equal(ground.Source, globalGiven.Source);
            Assert.Equal(ground.Point, globalGiven.Point);

            // A point to turn about is on the plane square to the view, whatever the work plane is.
            ResolvedPoint centre = PickEngine.ResolvePoint(view.Primitives, view.Geometry, view.Map, x, y, SnapKinds.None, 1E-3, null, true, false, raised);
            Assert.Equal("view plane", centre.Source);
        }
    }
}
