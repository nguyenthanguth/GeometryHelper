using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Viewports whose view is twisted or looks at a target off the origin, shown and worked through as AutoCAD 2021 shows
    /// them. The points are AutoCAD's own: TRANS from the world to the view (0 to 2) and on to the paper (2 to 3) in each
    /// viewport of a drawing its API made, read in its console.
    /// </summary>
    public class TwistedViewportTests
    {
        /// <summary>
        /// On Layout1, as AutoCAD's probe made them: A 120 by 80 about (100, 100), its view about (100, 50) 400 high, twisted 30
        /// degrees; B 120 by 80 about (250, 100), its view about (0, 0) at the target (10, 20); C 120 by 80 about (100, 220),
        /// its view about (30, 40) at the target (10, 20), twisted 30 degrees. All at 1:5.
        /// </summary>
        private static CadDocument ThreeViews(out CadCommandSession session, out string a, out string b, out string c)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "LINE", P(0, 0), P(1000, 0), Enter);
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(40, 60), P(160, 140));
            Run(session, "MV", P(190, 60), P(310, 140));
            Run(session, "MV", P(40, 180), P(160, 260));
            DxfViewport[] made = All<DxfViewport>(document.CurrentSpace).ToArray();
            Set(made[made.Length - 3], new GeoPoint3(0, 0, 0), P(100, 50), Math.PI / 6.0);
            Set(made[made.Length - 2], new GeoPoint3(10, 20, 0), P(0, 0), 0.0);
            Set(made[made.Length - 1], new GeoPoint3(10, 20, 0), P(30, 40), Math.PI / 6.0);
            a = made[made.Length - 3].Handle;
            b = made[made.Length - 2].Handle;
            c = made[made.Length - 1].Handle;
            document.Regen();
            return document;
        }

        private static void Set(DxfViewport viewport, GeoPoint3 target, GeoPoint2 center, double twist)
        {
            viewport.ViewTarget = target;
            viewport.ViewCenter = center;
            viewport.ViewHeight = 400.0;
            viewport.TwistAngle = twist;
        }

        [Fact]
        public void AModelPoint_IsShownOnThePaperWhereAutoCADShowsIt_ThroughATwistAndATarget()
        {
            CadDocument document = ThreeViews(out _, out string a, out string b, out string c);
            DxfViewport twisted = Entity<DxfViewport>(document, a);
            DxfViewport targeted = Entity<DxfViewport>(document, b);
            DxfViewport both = Entity<DxfViewport>(document, c);

            // AutoCAD 2021: TRANS (100 0 0) 0 2 in A is (86.6025 50), and on to the paper (97.3205 100).
            Near(P(86.6025, 50.0), CadViewports.ToView(twisted, P(100, 0)), 1E-4);
            Near(P(80.0, 90.0), CadViewports.ToPaper(twisted, P(0, 0)), 1E-4);
            Near(P(97.3205, 100.0), CadViewports.ToPaper(twisted, P(100, 0)), 1E-4);
            Near(P(70.0, 107.321), CadViewports.ToPaper(twisted, P(0, 100)), 1E-3);
            Near(P(248.0, 96.0), CadViewports.ToPaper(targeted, P(0, 0)), 1E-4);
            Near(P(268.0, 106.0), CadViewports.ToPaper(targeted, P(100, 50)), 1E-4);
            Near(P(94.2679, 207.536), CadViewports.ToPaper(both, P(0, 0)), 1E-3);
            Near(P(111.588, 217.536), CadViewports.ToPaper(both, P(100, 0)), 1E-3);
            Near(P(94.0, 212.0), CadViewports.ToPaper(both, P(10, 20)), 1E-4);
            Near(P(100, 0), CadViewports.ToModel(twisted, P(97.3205080756888, 100.0)), 1E-9);
            Near(P(100, 50), CadViewports.ToModel(both, CadViewports.ToPaper(both, P(100, 50))), 1E-9);

            // The display draws model space through each where the arithmetic puts it.
            CadSpace layout = document.Spaces[1];
            foreach (string handle in new[] { a, b, c })
            {
                DisplayViewport2 shown = document
                    .GetDisplay(layout)
                    .Items.OfType<DisplayViewport2>()
                    .Single(v => v.Handle == handle);
                DxfViewport viewport = Entity<DxfViewport>(document, handle);

                foreach (GeoPoint2 model in new[] { P(0, 0), P(100, 0), P(0, 100), P(100, 50) })
                {
                    GeoPoint2 paper = CadViewports.ToPaper(viewport, model);
                    Assert.Equal(paper.X, shown.ModelToPaper.X(model.X, model.Y), 9);
                    Assert.Equal(paper.Y, shown.ModelToPaper.Y(model.X, model.Y), 9);
                }
            }
        }

        [Fact]
        public void ATwistedViewport_IsEntered_AndTheCanvasWorksThroughItTurned()
        {
            CadDocument document = ThreeViews(out CadCommandSession session, out string a, out _, out _);
            var paper = new CadView(800, 600);
            paper.ZoomTo(new DisplayBox(0, 0, 420, 297));
            session.View = paper;

            Assert.True(document.EnterViewport(a));
            DxfViewport viewport = Entity<DxfViewport>(document, a);
            CadView model = CadViewports.ModelView(paper, viewport);
            Assert.Equal(Math.PI / 6.0, model.Twist, 15);

            // A model point is under the pixel the paper shows it at, and back.
            foreach (GeoPoint2 point in new[] { P(0, 0), P(100, 0), P(37, -12) })
            {
                GeoPoint2 on = CadViewports.ToPaper(viewport, point);
                paper.DrawingToPixel(on.X, on.Y, out double px, out double py);
                model.DrawingToPixel(point.X, point.Y, out double mx, out double my);
                Assert.Equal(px, mx, 6);
                Assert.Equal(py, my, 6);
                model.ScreenToDrawing(px, py, out double x, out double y);
                Near(point, P(x, y), 1E-9);
                Assert.Equal(px, model.DrawingToScreen.X(point.X, point.Y), 6);
                Assert.Equal(py, model.DrawingToScreen.Y(point.X, point.Y), 6);
            }

            // Zoomed in twice about a pixel and panned, the view the viewport takes keeps the model point under it.
            model.ScreenToDrawing(300, 200, out double ax, out double ay);
            model.ZoomAt(300, 200, 2.0);
            model.ScreenToDrawing(300, 200, out double bx, out double by);
            Near(P(ax, ay), P(bx, by), 1E-9);
            model.Pan(25, -10);
            model.ScreenToDrawing(325, 190, out double cx, out double cy);
            Near(P(ax, ay), P(cx, cy), 1E-9);
            CadViewports.ViewOf(paper, model, viewport, out GeoPoint2 center, out double height);
            Assert.Equal(200, height, 9);
            Assert.True(document.SetViewportView(a, center, height, "ZOOM").Succeeded);
            paper.ScreenToDrawing(325, 190, out double sx, out double sy);
            Near(P(ax, ay), CadViewports.ToModel(viewport, P(sx, sy)), 1E-6);

            // A line drawn through it is a line of model space where the points were given.
            Run(session, "LINE", P(10, 10), P(60, 10), Enter);
            Assert.Equal(2, All<DxfLine>(document.ModelSpace).Count);
        }

        [Fact]
        public void PanAndZoomWindowThroughATwistedViewport_MoveItsViewAsTheModelPointsSay()
        {
            CadDocument document = ThreeViews(out CadCommandSession session, out string a, out _, out _);
            session.View = new CadView(800, 600);
            document.EnterViewport(a);
            DxfViewport viewport = Entity<DxfViewport>(document, a);
            GeoPoint2 middle = CadViewports.ToModel(viewport, P(100, 100));

            // PAN by (100, 0) of model space moves the point at the frame's middle by as much.
            Run(session, "PAN", P(0, 0), P(100, 0));
            Near(
                P(middle.X - 100, middle.Y),
                CadViewports.ToModel(Entity<DxfViewport>(document, a), P(100, 100)),
                1E-9
            );

            // ZOOM Window about (500, 0) puts that point at the middle, the window's box turned fitted to the frame.
            Run(session, "ZOOM", P(400, -50), P(600, 50));
            viewport = Entity<DxfViewport>(document, a);
            Near(P(500, 0), CadViewports.ToModel(viewport, P(100, 100)), 1E-9);
            double turnedHeight = 200 * Math.Sin(Math.PI / 6.0) + 100 * Math.Cos(Math.PI / 6.0);
            double turnedWidth = 200 * Math.Cos(Math.PI / 6.0) + 100 * Math.Sin(Math.PI / 6.0);
            Assert.Equal(Math.Max(turnedHeight, turnedWidth * 80.0 / 120.0), viewport.ViewHeight, 9);
        }

        [Fact]
        public void ATwistedView_ZoomsAboutAPixel_AndShowsATurnedBox()
        {
            var view = new CadView(400, 300);
            view.ZoomTo(new DisplayBox(0, 0, 100, 100));
            var turned = new CadView(400, 300);
            turned.ZoomTo(new DisplayBox(0, 0, 100, 100));
            CadView model = CadViewports.ModelView(turned, NewTwisted(Math.PI / 2.0));

            // A quarter turn: the X axis of the drawing runs up the canvas.
            model.DrawingToPixel(model.CenterX + 10, model.CenterY, out double px, out double py);
            Assert.Equal(200, px, 9);
            Assert.True(py < 150);
            DisplayBox box = model.VisibleBox;
            Assert.Equal(300 / model.PixelsPerUnit, box.Width, 6);
            Assert.Equal(400 / model.PixelsPerUnit, box.Height, 6);

            // Untwisted, the arithmetic is as it always was.
            Assert.Equal(0.0, view.Twist);
            view.ScreenToDrawing(10, 20, out double x, out double y);
            Assert.Equal(view.CenterX + (10 - 200) / view.PixelsPerUnit, x);
            Assert.Equal(view.CenterY - (20 - 150) / view.PixelsPerUnit, y);
        }

        private static DxfViewport NewTwisted(double twist)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(90, 90));
            DxfViewport viewport = All<DxfViewport>(document.CurrentSpace).Last();
            viewport.TwistAngle = twist;
            return viewport;
        }
    }
}
