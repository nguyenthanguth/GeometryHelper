using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class ViewTests
    {
        [Fact]
        public void ZoomingAboutAPixelKeepsThePointUnderItWhereItWas()
        {
            var view = new CadView(1000, 600);
            view.ZoomTo(new DisplayBox(0, 0, 4000, 3000));
            view.ScreenToDrawing(700, 200, out double x0, out double y0);

            view.ZoomAt(700, 200, 1.2);
            view.ScreenToDrawing(700, 200, out double x1, out double y1);

            Assert.Equal(x0, x1, 6);
            Assert.Equal(y0, y1, 6);
        }

        [Fact]
        public void ZoomExtentsShowsTheWholeBoxWithItsMarginAndCentresIt()
        {
            var view = new CadView(1000, 500);

            view.ZoomTo(new DisplayBox(0, 0, 2000, 500), 0.05);

            Assert.Equal(1000.0, view.CenterX, 9);
            Assert.Equal(250.0, view.CenterY, 9);
            Assert.Equal(0.45, view.PixelsPerUnit, 9);
            Assert.True(view.VisibleBox.Contains(new DisplayBox(0, 0, 2000, 500)));
        }

        [Fact]
        public void PanningByAPixelMovesTheDrawingByThatPixelAndYGoesUpOnTheDrawingNotTheScreen()
        {
            var view = new CadView(800, 600);
            view.ZoomTo(new DisplayBox(0, 0, 800, 600), 0.0);
            view.DrawingToPixel(100, 100, out double px0, out double py0);

            view.Pan(30, -20);
            view.DrawingToPixel(100, 100, out double px1, out double py1);

            Assert.Equal(px0 + 30, px1, 6);
            Assert.Equal(py0 - 20, py1, 6);
        }

        [Fact]
        public void TheScreenTransformationAgreesWithPixelAndDrawingPointConversion()
        {
            var view = new CadView(640, 480);
            view.ZoomTo(new DisplayBox(-50, -20, 150, 80));

            view.DrawingToPixel(33, -7, out double px, out double py);
            Affine2 m = view.DrawingToScreen;

            Assert.Equal(px, m.X(33, -7), 6);
            Assert.Equal(py, m.Y(33, -7), 6);
        }
    }
}
