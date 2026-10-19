using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class BoxSelectTests
    {
        private static GeoPolygon2 Square(double x, double y, double size)
            => new GeoPolygon2(new GeoPoint2(x, y), new GeoPoint2(x + size, y), new GeoPoint2(x + size, y + size), new GeoPoint2(x, y + size));

        /// <summary>
        /// Three items in a row: a square at 0, a square at 20, and a line from the middle of the first to the middle of the second.
        /// </summary>
        private static ViewFixture Row()
            => ViewFixture.Top(new SceneWriter()
                .Add(Square(0, 0, 10), name: "first")
                .Add(Square(20, 0, 10), name: "second")
                .Add(new GeoLine2(new GeoPoint2(5, 5), new GeoPoint2(25, 5)), name: "between"));

        private static (double X, double Y, double X2, double Y2) Around(ViewFixture view, Vec3 low, Vec3 high)
        {
            (double x1, double y1) = view.Pixel(low);
            (double x2, double y2) = view.Pixel(high);
            return (x1, y1, x2, y2);
        }

        [Fact]
        public void ADragFromLeftToRight_IsAWindow_AndFromRightToLeftACrossing()
        {
            Assert.Equal(BoxMode.Window, BoxSelect.ModeOf(100.0, 300.0));
            Assert.Equal(BoxMode.Crossing, BoxSelect.ModeOf(300.0, 100.0));
        }

        [Fact]
        public void AWindow_SelectsWhatIsWhollyInsideIt()
        {
            ViewFixture view = Row();
            var box = Around(view, new Vec3(-1, -1, 0), new Vec3(11, 11, 0));

            // The line goes out of the window, so only the first square is inside it.
            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, view.Map, box.X, box.Y, box.X2, box.Y2, BoxMode.Window, null));
        }

        [Fact]
        public void ACrossing_SelectsWhatTouchesIt_ThoughNoCornerOfItIsInside()
        {
            ViewFixture view = Row();
            var box = Around(view, new Vec3(-1, -1, 0), new Vec3(11, 11, 0));

            // The line starts inside the box, and the second square is beyond it.
            Assert.Equal(new[] { 0, 2 }, BoxSelect.Select(view.Primitives, view.Map, box.X, box.Y, box.X2, box.Y2, BoxMode.Crossing, null));

            // A box that holds none of the line's ends and none of the corners, crossing its middle only.
            var middle = Around(view, new Vec3(14, 3, 0), new Vec3(16, 7, 0));
            Assert.Equal(new[] { 2 }, BoxSelect.Select(view.Primitives, view.Map, middle.X, middle.Y, middle.X2, middle.Y2, BoxMode.Crossing, null));
            Assert.Empty(BoxSelect.Select(view.Primitives, view.Map, middle.X, middle.Y, middle.X2, middle.Y2, BoxMode.Window, null));
        }

        [Fact]
        public void AWindowHoldingEverything_SelectsEverything_AndTheCornersMayBeGivenInAnyOrder()
        {
            ViewFixture view = Row();
            var box = Around(view, new Vec3(-1, -1, 0), new Vec3(31, 11, 0));

            Assert.Equal(new[] { 0, 1, 2 }, BoxSelect.Select(view.Primitives, view.Map, box.X, box.Y, box.X2, box.Y2, BoxMode.Window, null));
            Assert.Equal(new[] { 0, 1, 2 }, BoxSelect.Select(view.Primitives, view.Map, box.X2, box.Y2, box.X, box.Y, BoxMode.Window, null));
        }

        [Fact]
        public void ABoxAroundNothing_SelectsNothing_AndTheHiddenAreLeftOut()
        {
            ViewFixture view = Row();
            var empty = Around(view, new Vec3(12, 8, 0), new Vec3(18, 10, 0));
            var all = Around(view, new Vec3(-1, -1, 0), new Vec3(31, 11, 0));

            Assert.Empty(BoxSelect.Select(view.Primitives, view.Map, empty.X, empty.Y, empty.X2, empty.Y2, BoxMode.Crossing, null));
            Assert.Equal(new[] { 0, 2 }, BoxSelect.Select(view.Primitives, view.Map, all.X, all.Y, all.X2, all.Y2, BoxMode.Window, item => item != 1));
        }

        [Fact]
        public void ACrossingInsideAFace_SelectsTheFace_ThoughItTouchesNeitherAnEdgeNorACorner()
        {
            var face = new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0)));
            var view = ViewFixture.Top(new SceneWriter().Add(face));
            var inside = Around(view, new Vec3(40, 40, 0), new Vec3(60, 60, 0));

            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, view.Map, inside.X, inside.Y, inside.X2, inside.Y2, BoxMode.Crossing, null));
            Assert.Empty(BoxSelect.Select(view.Primitives, view.Map, inside.X, inside.Y, inside.X2, inside.Y2, BoxMode.Window, null));
        }

        [Fact]
        public void ACrossingAcrossTheCornerOfAFace_SelectsItWhereTheBoxHoldsOnlyAStretchOfAnEdge()
        {
            var face = new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0)));
            var view = ViewFixture.Top(new SceneWriter().Add(face).Add(new GeoPoint3(500, 500, 0)));
            var past = Around(view, new Vec3(90, -20, 0), new Vec3(110, 20, 0));
            var clear = Around(view, new Vec3(110, -20, 0), new Vec3(130, 20, 0));

            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, view.Map, past.X, past.Y, past.X2, past.Y2, BoxMode.Crossing, null));
            Assert.Empty(BoxSelect.Select(view.Primitives, view.Map, clear.X, clear.Y, clear.X2, clear.Y2, BoxMode.Crossing, null));
        }

        [Fact]
        public void ACurveIsJudgedByItsDrawnChords_ToTheirTolerance()
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 10.0);
            var view = ViewFixture.Top(new SceneWriter().Add(circle));

            // A window that just holds the circle's box selects it; one a hundredth of a millimetre short on one side does not.
            var holds = Around(view, new Vec3(-10.01, -10.01, 0), new Vec3(10.01, 10.01, 0));
            var short_ = Around(view, new Vec3(-9.99, -10.01, 0), new Vec3(10.01, 10.01, 0));

            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, view.Map, holds.X, holds.Y, holds.X2, holds.Y2, BoxMode.Window, null));
            Assert.Empty(BoxSelect.Select(view.Primitives, view.Map, short_.X, short_.Y, short_.X2, short_.Y2, BoxMode.Window, null));
        }

        [Fact]
        public void InPerspective_AWindowAndACrossingKeepTheirMeaning()
        {
            var view = ViewFixture.Iso(new SceneWriter()
                .Add(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 50)))
                .Add(new GeoPolyline3(new GeoPoint3(500, 500, 0), new GeoPoint3(600, 500, 80))));
            var (x1, y1) = view.Pixel(new Vec3(0, 0, 0));
            var (x2, y2) = view.Pixel(new Vec3(100, 100, 50));
            var (x3, y3) = view.Pixel(new Vec3(100, 0, 0));

            // A box on the first chain's three vertices; the second chain is elsewhere.
            double left = new[] { x1, x2, x3 }.Min() - 20.0;
            double right = new[] { x1, x2, x3 }.Max() + 20.0;
            double top = new[] { y1, y2, y3 }.Min() - 20.0;
            double bottom = new[] { y1, y2, y3 }.Max() + 20.0;
            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, view.Map, left, top, right, bottom, BoxMode.Window, null));
            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, view.Map, right, bottom, left, top, BoxMode.Crossing, null));

            // A little box on the middle of the first segment holds none of its vertices: a crossing for it, no window.
            var (mx, my) = view.Pixel(new Vec3(50, 0, 0));
            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, view.Map, mx + 3.0, my + 3.0, mx - 3.0, my - 3.0, BoxMode.Crossing, null));
            Assert.Empty(BoxSelect.Select(view.Primitives, view.Map, mx - 3.0, my - 3.0, mx + 3.0, my + 3.0, BoxMode.Window, null));
        }
    }
}
