using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// A long curve that runs on past the far plane of a perspective view, or in from behind the near plane, is picked and snapped to by the part of it that shows.
    /// </summary>
    public class LongCurvePickTests
    {
        private static ViewCamera Camera() => new ViewCamera { Target = new Vec3(0, 0, 0), Distance = 10.0, Azimuth = -Math.PI / 4.0, Elevation = 0.3 };

        /// <summary>
        /// A segment parallel to the line of sight, 100 to one side of it, from 1 in front of the target to a million times the distance of the view away.
        /// </summary>
        private static (ScreenMap Map, Primitives Primitives, ExactGeometry Geometry, ViewCamera Camera) ParallelSegment()
        {
            ViewCamera camera = Camera();
            Vec3 a = camera.Target + camera.Right * 100.0 + camera.Forward * 1.0;
            Vec3 b = camera.Target + camera.Right * 100.0 + camera.Forward * 1E7;
            Scene scene = new SceneWriter().Add(new GeoLine3(new GeoPoint3(a.X, a.Y, a.Z), new GeoPoint3(b.X, b.Y, b.Z))).Scene;
            Primitives primitives = Primitives.Build(scene);
            ExactGeometry geometry = ExactGeometry.Build(scene, primitives);
            return (new ScreenMap(camera, primitives.Bounds, 1000.0, 800.0), primitives, geometry, camera);
        }

        [Fact]
        public void ASegmentThatRunsPastTheFarPlane_IsPickedByItsVisiblePart_WhereItComesWithinFivePixelsOfTheCursor()
        {
            var view = ParallelSegment();

            // The cursor is at the vanishing point of the segment: its image runs into that point, and is within five pixels of it from about 19 000 on.
            PickHit hit = PickEngine.Pick(view.Primitives, view.Geometry, view.Map, 500.0, 400.0, null, true);

            Assert.NotNull(hit);
            Assert.Equal(PickKind.Curve, hit.Kind);
            Assert.True(view.Map.IsInDepth(hit.Point), "the hit is at depth " + view.Map.DepthOf(hit.Point) + " of " + view.Map.Far);
            Assert.True(hit.Depth > 15000.0, "depth " + hit.Depth);
        }

        [Fact]
        public void ASegmentThatRunsPastTheFarPlane_IsSnappedToByItsVisiblePart_AsTheNearestPoint()
        {
            var view = ParallelSegment();

            SnapHit snap = SnapEngine.Find(view.Geometry, view.Map, 500.0, 400.0, SnapKinds.Nearest, SnapEngine.DefaultRadius, 1E-3, null);

            Assert.NotNull(snap);
            Assert.Equal("nearest", snap.Name);
            Assert.True(view.Map.IsInDepth(snap.Point));
        }

        [Fact]
        public void ASegmentWhoseVisiblePartIsFarFromTheCursor_IsStillNotPicked()
        {
            var view = ParallelSegment();

            // 300 pixels below the vanishing point the image of the segment is nowhere near: there is nothing to pick.
            PickHit hit = PickEngine.Pick(view.Primitives, view.Geometry, view.Map, 500.0, 700.0, null, true);

            Assert.Null(hit);
        }

        [Fact]
        public void ASegmentThatPassesThroughTheEye_IsPickedByThePartInFrontOfTheNearPlane()
        {
            ViewCamera camera = Camera();
            Vec3 eye = camera.Position;
            Vec3 a = eye - camera.Forward * 5.0;
            Vec3 b = eye + camera.Forward * 20.0;
            Scene scene = new SceneWriter().Add(new GeoLine3(new GeoPoint3(a.X, a.Y, a.Z), new GeoPoint3(b.X, b.Y, b.Z))).Add(new GeoPoint3(0, 0, 0)).Scene;
            Primitives primitives = Primitives.Build(scene);
            ExactGeometry geometry = ExactGeometry.Build(scene, primitives);
            var map = new ScreenMap(camera, primitives.Bounds, 1000.0, 800.0);

            // The nearest point of the line to a ray 3 pixels off the middle is the eye itself, which is behind the near plane; the part in front of it is within 3 pixels.
            PickHit hit = PickEngine.Pick(primitives, geometry, map, 503.0, 400.0, i => i == 0, true);

            Assert.NotNull(hit);
            Assert.Equal(0, hit.ItemIndex);
            Assert.True(map.IsInDepth(hit.Point));
        }
    }
}
