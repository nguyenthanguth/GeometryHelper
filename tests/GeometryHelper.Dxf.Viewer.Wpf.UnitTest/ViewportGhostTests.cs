using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The ghost a transforming command drags with a viewport of a layout among its objects: the frame as the edit will leave
    /// it, and the model shown through it as the edit will leave that. The room's viewport is 200 by 150 about (150, 100) on the
    /// sheet, showing the room at 1:20 about (2 000, 1 500); the column of radius 100 at the middle of the room is at the
    /// middle of the frame, 5 on the sheet in radius.
    /// </summary>
    public class ViewportGhostTests
    {
        /// <summary>How the viewport is clipped, if it is: by a circle of radius 40 about the middle of its frame.</summary>
        private enum Clip
        {
            None,
            BoundaryNotSelected,
            BoundarySelected,
        }

        /// <summary>The room with its viewport selected on Layout1, a command started and its base point given as (0, 0).</summary>
        private static CadGhost Dragging(
            TestFolder folder,
            string command,
            Clip clip,
            out CadDocument document,
            out CadCommandSession session,
            out CadSpace layout
        )
        {
            DxfDatabase db = TestFiles.RoomWithViewport(folder, out _, out string handle);
            DxfViewport frame = db
                .Layouts.Single(l => l.LayoutName == "Layout1")
                .GetViewports()
                .Single(v => v.Handle == handle);
            var selected = new List<string> { handle };

            if (clip != Clip.None)
            {
                var circle = new DxfCircle(new GeoPoint3(150, 100, 0), new GeoVector3(0, 0, 1), 40);
                ((DxfBlockTableRecord)frame.Owner).AppendEntity(circle);
                frame.ClipTo(circle);

                if (clip == Clip.BoundarySelected)
                {
                    selected.Add(circle.Handle);
                }
            }

            document = CadDocument.From(db, folder.Write("room.dxf", db));
            layout = document.Spaces.Single(s => s.Name == "Layout1");
            document.CurrentSpace = layout;
            document.Selection.Set(selected);
            session = new CadCommandSession(document) { PickAperture = 1.0 };
            CadCommandSession own = session;
            var ghost = new CadGhost();
            session.PromptChanged += (s, e) => ghost.Follow(own);
            session.Feed(command);
            session.Feed(new GeoPoint2(0, 0));
            return ghost;
        }

        private static double[] Bounds(IReadOnlyList<double[]> paths)
        {
            double minX = double.MaxValue,
                minY = double.MaxValue,
                maxX = double.MinValue,
                maxY = double.MinValue;

            foreach (double[] path in paths)
            {
                for (int i = 0; i + 1 < path.Length; i += 2)
                {
                    minX = Math.Min(minX, path[i]);
                    maxX = Math.Max(maxX, path[i]);
                    minY = Math.Min(minY, path[i + 1]);
                    maxY = Math.Max(maxY, path[i + 1]);
                }
            }

            return new[] { minX, minY, maxX, maxY };
        }

        /// <summary>Where the middle of the room, (2 000, 1 500), is drawn on the sheet through the viewport.</summary>
        private static GeoPoint2 RoomMiddle(DisplayViewport2 viewport)
        {
            Affine2 m = viewport.ModelToPaper;
            return new GeoPoint2(m.X(2000, 1500), m.Y(2000, 1500));
        }

        [Fact]
        public void AViewportDraggedByMove_HasItsFrameInTheGhost()
        {
            using (var folder = new TestFolder())
            {
                CadGhost ghost = Dragging(
                    folder,
                    "MOVE",
                    Clip.None,
                    out CadDocument document,
                    out _,
                    out CadSpace layout
                );

                IReadOnlyList<double[]> paths = ghost.Paths(document, layout, 0.01);

                // The frame 200 by 150 about (150, 100): the box from (50, 25) to (250, 175), which the cursor then moves.
                double[] box = Bounds(paths);
                Assert.Equal(50.0, box[0], 6);
                Assert.Equal(25.0, box[1], 6);
                Assert.Equal(250.0, box[2], 6);
                Assert.Equal(175.0, box[3], 6);
            }
        }

        [Fact]
        public void AViewportDraggedByMove_CarriesTheModelItShowsWithTheFrame()
        {
            using (var folder = new TestFolder())
            {
                CadGhost ghost = Dragging(
                    folder,
                    "MOVE",
                    Clip.None,
                    out CadDocument document,
                    out CadCommandSession session,
                    out CadSpace layout
                );
                ghost.Paths(document, layout, 0.01);
                Affine2 move = CadGhost.Transform(session, new GeoPoint2(30, 20)).Value;

                DisplayViewport2 moved = Assert.Single(ghost.Viewports(move));

                Assert.Equal(180.0, moved.CenterX, 6);
                Assert.Equal(120.0, moved.CenterY, 6);
                Assert.Equal(180.0, RoomMiddle(moved).X, 6);
                Assert.Equal(120.0, RoomMiddle(moved).Y, 6);
                Assert.Equal(0.05, moved.Scale, 9);
            }
        }

        [Fact]
        public void AViewportDraggedByScale_ScalesTheFrameAndLeavesTheModelWhereItStandsOnTheSheet()
        {
            using (var folder = new TestFolder())
            {
                CadGhost ghost = Dragging(
                    folder,
                    "SCALE",
                    Clip.None,
                    out CadDocument document,
                    out CadCommandSession session,
                    out CadSpace layout
                );
                ghost.Paths(document, layout, 0.01);
                Affine2 scale = CadGhost.Transform(session, new GeoPoint2(0, 2)).Value;

                DisplayViewport2 scaled = Assert.Single(ghost.Viewports(scale));

                // Scaled by 2 about the origin: 400 by 300 about (300, 200), the room's middle still at (150, 100).
                Assert.Equal(400.0, scaled.Width, 6);
                Assert.Equal(300.0, scaled.Height, 6);
                Assert.Equal(300.0, scaled.CenterX, 6);
                Assert.Equal(200.0, scaled.CenterY, 6);
                Assert.Equal(150.0, RoomMiddle(scaled).X, 6);
                Assert.Equal(100.0, RoomMiddle(scaled).Y, 6);
                Assert.Equal(0.05, scaled.Scale, 9);
            }
        }

        [Fact]
        public void AViewportDraggedByRotate_TurnsTheFrame()
        {
            using (var folder = new TestFolder())
            {
                CadGhost ghost = Dragging(
                    folder,
                    "ROTATE",
                    Clip.None,
                    out CadDocument document,
                    out CadCommandSession session,
                    out CadSpace layout
                );
                ghost.Paths(document, layout, 0.01);
                Affine2 turn = CadGhost.Transform(session, new GeoPoint2(0, 10)).Value;

                DisplayViewport2 turned = Assert.Single(ghost.Viewports(turn));

                // A quarter turn about the origin: the frame stands 150 by 200 about (-100, 150), the view twisted by 90 degrees.
                Assert.Equal(150.0, turned.Width, 6);
                Assert.Equal(200.0, turned.Height, 6);
                Assert.Equal(-100.0, turned.CenterX, 6);
                Assert.Equal(150.0, turned.CenterY, 6);
                Assert.Equal(Math.PI / 2.0, turned.Twist, 6);
            }
        }

        [Fact]
        public void AClippedViewportWithItsBoundary_IsInTheGhostAsItsOutline()
        {
            using (var folder = new TestFolder())
            {
                CadGhost ghost = Dragging(
                    folder,
                    "MOVE",
                    Clip.BoundarySelected,
                    out CadDocument document,
                    out CadCommandSession session,
                    out CadSpace layout
                );

                IReadOnlyList<double[]> paths = ghost.Paths(document, layout, 0.01);
                Affine2 move = CadGhost.Transform(session, new GeoPoint2(30, 20)).Value;

                // The circle of radius 40 about (150, 100), as the viewport's frame and as the boundary itself: both move.
                double[] box = Bounds(paths);
                Assert.Equal(110.0, box[0], 1);
                Assert.Equal(60.0, box[1], 1);
                Assert.Equal(190.0, box[2], 1);
                Assert.Equal(140.0, box[3], 1);
                DisplayViewport2 moved = Assert.Single(ghost.Viewports(move));
                Assert.NotNull(moved.Clip);
                Assert.Equal(220.0, moved.Clip.X(0), 6);
                Assert.Equal(120.0, RoomMiddle(moved).Y, 6);
            }
        }

        [Fact]
        public void AClippedViewportMovedWithoutItsBoundary_StaysWhereItIsAndIsNotInTheGhost()
        {
            using (var folder = new TestFolder())
            {
                CadGhost ghost = Dragging(
                    folder,
                    "MOVE",
                    Clip.BoundaryNotSelected,
                    out CadDocument document,
                    out _,
                    out CadSpace layout
                );

                // The edit leaves a clipped viewport where it stands unless its boundary goes with it.
                Assert.Empty(ghost.Paths(document, layout, 0.01));
                Assert.Empty(ghost.Viewports(Affine2.Translation(30, 20)));
            }
        }

        // The room's walls and its column, drawn through the viewport, on a layout in an offscreen canvas of 800 by 600.
        private const int Width = 800;
        private const int Height = 600;

        private static int[] Pixels(CadCanvas canvas)
        {
            canvas.RenderNow();
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new int[Width * Height];
            bitmap.CopyPixels(pixels, Width * 4, 0);
            return pixels;
        }

        /// <summary>The pixels of a canvas that differ from another's in a box of the sheet, sheet units, counted.</summary>
        private static int Differing(CadCanvas canvas, int[] was, int[] now, double x0, double y0, double x1, double y1)
        {
            int count = 0;

            for (int iy = 0; iy < Height; iy++)
            {
                for (int ix = 0; ix < Width; ix++)
                {
                    canvas.View.ScreenToDrawing(ix, iy, out double x, out double y);

                    if (x >= x0 && x <= x1 && y >= y0 && y <= y1 && was[iy * Width + ix] != now[iy * Width + ix])
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Runs a test on an STA thread with the room's layout open in an offscreen canvas of 800 by 600 on a black sheet, the
        /// viewport selected: the model is drawn by the canvas as the viewport shows it, and what follows the cursor over it.
        /// </summary>
        private static void OnLayout(Action<MainViewModel, CadCanvas, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        DxfDatabase db = TestFiles.RoomWithViewport(folder, out _, out string handle);
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", db));
                        model.SelectSpace(model.Document.Spaces.Single(s => s.Name == "Layout1"));
                        var canvas = new CadCanvas
                        {
                            Document = model.Document,
                            Space = model.Space,
                            View = model.View,
                            Session = model.Session,
                            Snap = model.Snap,
                        };
                        canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                        canvas.Measure(new Size(Width, Height));
                        canvas.Arrange(new Rect(0, 0, Width, Height));
                        canvas.RenderNow();
                        model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                        canvas.Options.Paper = 0x000000;
                        model.Document.Selection.Set(new[] { handle });
                        test(model, canvas, handle);
                        canvas.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void MoveOfAViewport_DrawsTheFrameAndTheModelAtTheCursor_OnTheCanvas()
        {
            OnLayout(
                (model, canvas, handle) =>
                {
                    model.Session.Feed("MOVE");
                    model.Session.Feed(new GeoPoint2(0, 0));
                    canvas.MoveCursorTo(0, 0);
                    int[] still = Pixels(canvas);

                    // The cursor 100 to the right and 60 up from the base point.
                    canvas.MoveCursorTo(100, 60);
                    int[] dragged = Pixels(canvas);

                    // The column of radius 5 about (150, 100) is the middle of the room: at (250, 160) the ghost draws its ring, where
                    // the sheet shows nothing, and the frame's lower edge (with the wall on it) runs along y = 85 from x = 150.
                    int ring = Differing(canvas, still, dragged, 244, 154, 256, 166);
                    int edge = Differing(canvas, still, dragged, 160, 80, 240, 90);
                    Assert.True(
                        ring > 10,
                        "The model's column is not drawn at the cursor: " + ring + " pixels differ."
                    );
                    Assert.True(edge > 20, "The frame is not drawn at the cursor: " + edge + " pixels differ.");
                }
            );
        }

        [Fact]
        public void ACornerGripOfAViewportDragged_DrawsTheResizedFrame_AndTheModelWhereItWas_OnTheCanvas()
        {
            OnLayout(
                (model, canvas, handle) =>
                {
                    // The lower right corner, (250, 25): its grip clicked, and the cursor moved to (240, 5), 10 left and 20 down.
                    Assert.True(model.Session.StartGripEdit(new[] { new KeyValuePair<string, int>(handle, 1) }));
                    canvas.MoveCursorTo(250, 25);
                    int[] still = Pixels(canvas);
                    canvas.MoveCursorTo(240, 5);
                    int[] dragged = Pixels(canvas);

                    // The frame's right side moves from x = 250 to 240 and its lower side from y = 25 to 5.
                    int side = Differing(canvas, still, dragged, 238, 40, 242, 150);
                    int bottom = Differing(canvas, still, dragged, 100, 3, 200, 7);
                    Assert.True(side > 50, "The frame's new right side is not drawn: " + side + " pixels differ.");
                    Assert.True(bottom > 50, "The frame's new lower side is not drawn: " + bottom + " pixels differ.");
                }
            );
        }
    }
}
