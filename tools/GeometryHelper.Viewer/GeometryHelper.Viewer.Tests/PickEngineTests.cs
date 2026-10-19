using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class PickEngineTests
    {
        private static GeoFace3 Plate(double x, double y, double z, double size)
            => new GeoFace3(new GeoPolygon3(new GeoPoint3(x, y, z), new GeoPoint3(x + size, y, z), new GeoPoint3(x + size, y + size, z), new GeoPoint3(x, y + size, z)));

        private static PickHit PickAt(ViewFixture view, Vec3 at, double offsetX = 0.0, double offsetY = 0.0, bool surfaces = true, Func<int, bool> visible = null)
        {
            (double x, double y) = view.Pixel(at);
            return PickEngine.Pick(view.Primitives, view.Geometry, view.Map, x + offsetX, y + offsetY, visible, surfaces);
        }

        [Fact]
        public void AFace_IsPickedWhereTheRayMeetsItsTriangles_AtThePointOfThePlane()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(Plate(0, 0, 3, 100)));

            PickHit hit = PickAt(view, new Vec3(30, 40, 3));

            Assert.Equal(0, hit.ItemIndex);
            Assert.Equal(PickKind.Surface, hit.Kind);
            ViewFixture.AssertNear(new Vec3(30, 40, 3), hit.Point, 1E-9);
        }

        [Fact]
        public void OfTwoFacesOneAboveTheOther_TheUpperIsPickedFromAbove()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(Plate(0, 0, 0, 100)).Add(Plate(20, 20, 10, 50)).Add(Plate(0, 0, -5, 100)));

            Assert.Equal(1, PickAt(view, new Vec3(40, 40, 10)).ItemIndex);
            Assert.Equal(0, PickAt(view, new Vec3(90, 90, 0)).ItemIndex);
        }

        [Fact]
        public void AnEdgeOnAFace_IsPickedInPreferenceToTheFace()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(Plate(0, 0, 0, 100)).Add(new GeoLine3(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0))));

            PickHit hit = PickAt(view, new Vec3(50, 50, 0), 0.0, 2.0);

            Assert.Equal(1, hit.ItemIndex);
            Assert.Equal(PickKind.Curve, hit.Kind);
            Assert.Equal(0, PickAt(view, new Vec3(50, 20, 0)).ItemIndex);
        }

        [Fact]
        public void AnArc_IsPickedOnTheExactCurve_WithinAFewPixels_AndNotFurther()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100.0, 0.0, Math.PI);
            var view = ViewFixture.Top(new SceneWriter().Add(arc));
            Vec3 on = new Vec3(100 * Math.Cos(1.1), 100 * Math.Sin(1.1), 0);

            PickHit near = PickAt(view, on, 3.0, 0.0);
            Assert.Equal(0, near.ItemIndex);
            Assert.Equal(PickKind.Curve, near.Kind);
            Assert.Equal(100.0, near.Point.Length, 12);

            Assert.Null(PickAt(view, on, 40.0, 0.0));
        }

        [Fact]
        public void ThePointsAndTheLabelsOfAScene_ArePickedByTheirPlaceOnTheScreen()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoPoint3(10, 10, 0)).Text(new GeoPoint3(80, 40, 0), "note").Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0))));

            Assert.Equal(PickKind.Point, PickAt(view, new Vec3(10, 10, 0), 4.0, 3.0).Kind);
            Assert.Equal(1, PickAt(view, new Vec3(80, 40, 0), -3.0, 2.0).ItemIndex);
            Assert.Null(PickAt(view, new Vec3(10, 10, 0), 30.0, 0.0));
        }

        [Fact]
        public void InWireframe_NoSurfaceIsPicked_ButItsEdgesAre()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(Plate(0, 0, 0, 100)).Add(new GeoPoint3(500, 500, 0)));

            Assert.Equal(0, PickAt(view, new Vec3(50, 50, 0)).ItemIndex);
            Assert.Null(PickAt(view, new Vec3(50, 50, 0), 0.0, 0.0, false));
            Assert.Equal(0, PickAt(view, new Vec3(100, 50, 0), 1.0, 0.0, false).ItemIndex);
        }

        [Fact]
        public void AnItemThatIsHidden_IsNotPicked()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(Plate(0, 0, 10, 100)).Add(Plate(0, 0, 0, 100)));

            Assert.Equal(0, PickAt(view, new Vec3(50, 50, 10)).ItemIndex);
            Assert.Equal(1, PickAt(view, new Vec3(50, 50, 10), 0.0, 0.0, true, item => item != 0).ItemIndex);
        }

        [Fact]
        public void InPerspective_TheNearerOfTwoFacesOnTheSameRayIsPicked()
        {
            var view = ViewFixture.Iso(new SceneWriter().Add(Plate(0, 0, 0, 100)).Add(Plate(0, 0, 40, 100)));

            // The upper face is nearer the camera, which looks down at the scene.
            PickHit hit = PickAt(view, new Vec3(50, 50, 40));
            Assert.Equal(1, hit.ItemIndex);
            ViewFixture.AssertNear(new Vec3(50, 50, 40), hit.Point, 1E-6);
        }

        [Fact]
        public void AResolvedPoint_IsTheSnapWhereOneIsOn_ElseTheSurface_ElseThePlaneBehind()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(Plate(0, 0, 0, 100)));
            double tolerance = view.Scene.EqualPoint;

            (double cx, double cy) = view.Pixel(new Vec3(100, 100, 0));
            ResolvedPoint corner = PickEngine.ResolvePoint(view.Primitives, view.Geometry, view.Map, cx + 2.0, cy - 2.0, SnapKinds.All, tolerance, null, true);
            Assert.Equal("end", corner.Source);
            Assert.Equal(new Vec3(100, 100, 0), corner.Point);

            (double sx, double sy) = view.Pixel(new Vec3(30, 70, 0));
            ResolvedPoint surface = PickEngine.ResolvePoint(view.Primitives, view.Geometry, view.Map, sx, sy, SnapKinds.None, tolerance, null, true);
            Assert.Equal("surface", surface.Source);
            ViewFixture.AssertNear(new Vec3(30, 70, 0), surface.Point, 1E-9);

            ResolvedPoint space = PickEngine.ResolvePoint(view.Primitives, view.Geometry, view.Map, sx, sy, SnapKinds.None, tolerance, null, false);
            Assert.Equal("plane z = 0", space.Source);
            ViewFixture.AssertNear(new Vec3(30, 70, 0), space.Point, 1E-9);
            Assert.Equal(-1, space.ItemIndex);
        }

        [Fact]
        public void TheRayThroughAPlace_ComesBackToThePlaceItWasMadeFrom_InBothViews()
        {
            var scene = new SceneWriter().Add(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 40, 60)));

            foreach (ViewFixture view in new[] { ViewFixture.Top(scene), ViewFixture.Iso(scene) })
            {
                var point = new Vec3(37.5, 15.0, 22.5);
                (double x, double y) = view.Pixel(point);

                view.Map.GetRay(x, y, out Vec3 origin, out Vec3 direction);

                // The point is on the ray: no further from it than the rounding of the numbers.
                Vec3 toPoint = point - origin;
                Assert.True((toPoint - direction * Vec3.Dot(toPoint, direction)).Length < 1E-9);
            }
        }
    }
}
