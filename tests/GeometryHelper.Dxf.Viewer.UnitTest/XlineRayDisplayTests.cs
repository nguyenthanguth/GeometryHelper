using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Wpf;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// XLINE and RAY as AutoCAD shows them: drawn without end across every view they cross, an XLINE both ways from its base
    /// point and a RAY on from it, picked along their whole length, and left out of ZOOM Extents but for their base points.
    /// </summary>
    public class XlineRayDisplayTests
    {
        private static string Xline(double x, double y, double dx, double dy, int color = 256, string handle = null)
            => TestDrawings.Entity("XLINE", "0", color, handle, 100, "AcDbXline", 10, x, 20, y, 30, 0.0, 11, dx, 21, dy, 31, 0.0);

        private static string Ray(double x, double y, double dx, double dy, int color = 256, string handle = null)
            => TestDrawings.Entity("RAY", "0", color, handle, 100, "AcDbRay", 10, x, 20, y, 30, 0.0, 11, dx, 21, dy, 31, 0.0);

        private static CadDocument Document(params string[] entities) => CadDocument.From(TestDrawings.Drawing(null, null, entities), "test.dxf");

        [Fact]
        public void AnXlineAndARay_AreDrawnWithoutEnd_AndOnlyTheirBasePointsCountInTheExtents()
        {
            // A line 100 by 50 from the origin, an xline level through (200, 300) and a ray up from (-50, -20).
            CadDocument document = Document(TestDrawings.Line(0, 0, 100, 50, handle: "A1"), Xline(200, 300, 1, 0, handle: "B1"), Ray(-50, -20, 0, 1, handle: "B2"));

            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            DisplayRay2 xline = Assert.IsType<DisplayRay2>(Assert.Single(list.Items, i => i.Handle == "B1"));
            Assert.True(xline.IsBothWays);
            Assert.Equal(200.0, xline.X, 9);
            Assert.Equal(300.0, xline.Y, 9);
            Assert.Equal(1.0, xline.Dx, 12);
            Assert.Equal(0.0, xline.Dy, 12);
            Assert.Equal(double.NegativeInfinity, xline.Bounds.MinX);
            Assert.Equal(double.PositiveInfinity, xline.Bounds.MaxX);
            Assert.Equal(300.0, xline.Bounds.MinY, 9);
            Assert.Equal(300.0, xline.Bounds.MaxY, 9);

            DisplayRay2 ray = Assert.IsType<DisplayRay2>(Assert.Single(list.Items, i => i.Handle == "B2"));
            Assert.False(ray.IsBothWays);
            Assert.Equal(-20.0, ray.Bounds.MinY, 9);
            Assert.Equal(double.PositiveInfinity, ray.Bounds.MaxY);

            // The extents reach the base points (200, 300) and (-50, -20), and no further.
            Assert.Equal(-50.0, list.Bounds.MinX, 9);
            Assert.Equal(-20.0, list.Bounds.MinY, 9);
            Assert.Equal(200.0, list.Bounds.MaxX, 9);
            Assert.Equal(300.0, list.Bounds.MaxY, 9);
        }

        [Fact]
        public void AnXlineAndARay_ArePickedFarAlongThem_AndARayNotBehindItsBasePoint()
        {
            CadDocument document = Document(TestDrawings.Line(0, 0, 100, 50, handle: "A1"), Xline(200, 300, 1, 0, handle: "B1"), Ray(-50, -20, 0, 1, handle: "B2"));
            PickIndex index = document.GetPickIndex(document.ModelSpace);

            Assert.Equal(new[] { "B1" }, index.Pick(1E6, 300.5, 1.0));
            Assert.Equal(new[] { "B1" }, index.Pick(-1E6, 299.6, 1.0));
            Assert.Equal(new[] { "B2" }, index.Pick(-50.3, 5000, 1.0));
            Assert.Empty(index.Pick(-50, -1000, 1.0));

            // A crossing box the xline runs through takes it; a window never holds a line without end.
            var box = new DisplayBox(500, 290, 510, 310);
            Assert.Equal(new[] { "B1" }, index.Crossing(box));
            Assert.Empty(index.Window(box));
            Assert.Empty(index.Crossing(new DisplayBox(500, 310, 510, 320)));
        }

        [Fact]
        public void ARayInABlock_IsPlacedAndTurnedByTheInsert_AndPickedAsTheInsert()
        {
            // The block's ray runs along x from (10, 0); inserted at (100, 100) turned a quarter, it runs up from (100, 110).
            DxfDatabase database = TestDrawings.Drawing(null, TestDrawings.Block("K", Ray(10, 0, 1, 0)), TestDrawings.Insert("K", 100, 100, rotation: 90, handle: "C1"));
            CadDocument document = CadDocument.From(database, "test.dxf");

            DisplayRay2 ray = Assert.IsType<DisplayRay2>(Assert.Single(document.GetDisplay(document.ModelSpace).Items));
            Assert.Equal("C1", ray.Handle);
            Assert.Equal(100.0, ray.X, 9);
            Assert.Equal(110.0, ray.Y, 9);
            Assert.Equal(0.0, ray.Dx, 12);
            Assert.Equal(1.0, ray.Dy, 12);
        }

        [Fact]
        public void AnXlineCrossesTheWholeView_AndARayOnlyTheSideItRunsTo()
        {
            // At one pixel to the unit, a red xline along y = 74.5 lies on pixel row 75 and a green ray from (100, 24.5) running
            // to the left on row 125.
            CadDocument document = Document(Xline(100, 74.5, 1, 0, color: 1, handle: "B1"), Ray(100, 24.5, -1, 0, color: 3, handle: "B2"));
            var view = new CadView(200, 150);
            view.ZoomTo(new DisplayBox(0, 0, 200, 150), 0.0);

            using (var bitmap = new SKBitmap(200, 150, SKColorType.Rgba8888, SKAlphaType.Premul))
            {
                using (var canvas = new SKCanvas(bitmap))
                using (var renderer = new SkiaCanvasRenderer())
                {
                    renderer.Render(canvas, document, document.ModelSpace, view, new CanvasOptions());
                }

                Func<SKColor, bool> red = c => c.Red > 200 && c.Green < 60 && c.Blue < 60;
                Func<SKColor, bool> green = c => c.Green > 100 && c.Red < 60 && c.Blue < 60;

                for (int x = 0; x < 200; x++)
                {
                    Assert.True(red(bitmap.GetPixel(x, 75)), "xline pixel " + x);
                }

                for (int x = 0; x <= 97; x++)
                {
                    Assert.True(green(bitmap.GetPixel(x, 125)), "ray pixel " + x);
                }

                Assert.DoesNotContain(Enumerable.Range(103, 97), x => green(bitmap.GetPixel(x, 125)));
            }
        }
    }
}
