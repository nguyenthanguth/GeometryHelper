using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The grips of a clipped block reference on the canvas, as AutoCAD 2021 gives them: the arrow at the middle of the first
    /// side of the boundary, drawn as a triangle across it, clicked to turn the clip inside out, and a grip at each corner,
    /// dragged to move the corner of the boundary.
    /// </summary>
    public sealed class XClipGripCanvasTests
    {
        private const int Width = 800;
        private const int Height = 600;
        private const int Blue = 0x3D9BFF;

        /// <summary>
        /// Runs a test on an STA thread: a block of a line from (0, 0) to (100, 0) inserted at the origin, clipped to the
        /// rectangle (20, -10) to (60, 10) and selected, under a canvas of 800 by 600 showing -20,-20 to 120,120, the window's
        /// wiring of a click on the arrow in place.
        /// </summary>
        private static void OnClippedLine(Action<MainViewModel, CadCanvas, DxfBlockReference> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var database = new DxfDatabase("AC1027");
                    var block = new DxfBlockTableRecord { Name = "LINE100" };
                    database.BlockTable.Add(block);
                    block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
                    var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
                    database.ModelSpace.AppendEntity(insert);
                    var model = new MainViewModel();
                    model.OpenDocument(CadDocument.From(database, "xclipgrips.dxf"), "xclipgrips.dxf");
                    model.Execute("XCLIP");
                    model.Session.FeedSelection(new[] { insert.Handle });
                    model.Session.Enter();
                    model.Session.Enter();
                    model.Session.Enter();
                    model.Session.Feed(new GeoPoint2(20, -10));
                    model.Session.Feed(new GeoPoint2(60, 10));
                    Assert.NotNull(insert.SpatialFilter);
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                    };
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.CustomGripClicked += (s, e) => model.ClickCustomGrip(e.Handle, e.GripIndex);
                    canvas.Measure(new Size(Width, Height));
                    canvas.Arrange(new Rect(0, 0, Width, Height));
                    canvas.RenderNow();
                    canvas.View.ZoomTo(new DisplayBox(-20, -20, 120, 120));
                    model.Document.Selection.Set(new[] { insert.Handle });
                    canvas.RenderNow();
                    test(model, canvas, insert);
                    canvas.Dispose();
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

        private static Point At(CadCanvas canvas, double x, double y) => CanvasMouseTests.At(canvas, x, y);

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

        [Fact]
        public void TheSelectedReference_ShowsItsInsertionGrip_TheArrowAndTheFourCorners()
        {
            OnClippedLine(
                (model, canvas, insert) =>
                {
                    canvas.RenderNow();

                    Assert.Equal(6, canvas.GripCount);
                }
            );
        }

        [Fact]
        public void TheArrow_IsDrawnAsATriangleInTheGripColour_AtTheMiddleOfTheFirstSide()
        {
            OnClippedLine(
                (model, canvas, insert) =>
                {
                    int[] pixels = Pixels(canvas);
                    Point arrow = At(canvas, 40, -10);

                    Assert.Equal(Blue, pixels[(int)Math.Round(arrow.Y) * Width + (int)Math.Round(arrow.X)] & 0xFFFFFF);
                }
            );
        }

        [Fact]
        public void AClickOnTheArrow_TurnsTheClipInsideOut_WithoutStartingAGripEdit_InOneUndoStep()
        {
            OnClippedLine(
                (model, canvas, insert) =>
                {
                    CanvasMouseTests.Click(canvas, At(canvas, 40, -10));

                    Assert.False(model.Session.IsGripEditing);
                    Assert.Equal(
                        11,
                        ((DxfBlockReference)model.Document.FindEntity(insert.Handle)).SpatialFilter.Points.Count
                    );
                    Assert.Equal("GRIP", model.Document.NextUndo.Name);

                    Assert.True(model.Document.Undo());
                    Assert.Equal(
                        2,
                        ((DxfBlockReference)model.Document.FindEntity(insert.Handle)).SpatialFilter.Points.Count
                    );
                }
            );
        }

        [Fact]
        public void ACornerGripDragged_MovesTheCornerOfTheBoundary()
        {
            OnClippedLine(
                (model, canvas, insert) =>
                {
                    CanvasMouseTests.Drag(canvas, At(canvas, 20, -10), At(canvas, 10, -20));

                    DxfSpatialFilter filter = (
                        (DxfBlockReference)model.Document.FindEntity(insert.Handle)
                    ).SpatialFilter;
                    Assert.Equal(2, filter.Points.Count);
                    Assert.Equal(10.0, filter.Points[0].X, 6);
                    Assert.Equal(-20.0, filter.Points[0].Y, 6);
                    Assert.Equal(60.0, filter.Points[1].X, 6);
                    Assert.Equal(10.0, filter.Points[1].Y, 6);
                }
            );
        }
    }
}
