using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// A box dragged at "Select objects:" while an object snap override waits (END typed or chosen from the menu) takes the
    /// override at the corner the drag began at, as a box clicked takes it at its first corner, and spends it there.
    /// </summary>
    public class CanvasOverrideDragTests
    {
        private static void OnCanvas(Action<MainViewModel, CadCanvas, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out _)));
                        var canvas = new CadCanvas
                        {
                            Document = model.Document,
                            Space = model.Space,
                            View = model.View,
                            Session = model.Session,
                            Snap = model.Snap,
                            SelectByDragging = true,
                        };
                        canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                        canvas.Measure(new Size(800, 600));
                        canvas.Arrange(new Rect(0, 0, 800, 600));
                        canvas.RenderNow();
                        test(model, canvas, bottom);
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

        /// <summary>
        /// The room's top wall runs from (0, 3000) to (4000, 3000). A crossing dragged from just below and right of its end to
        /// (3000, 2000) touches nothing as it lies, and the wall when the first corner is snapped to the end.
        /// </summary>
        [Fact]
        public void ACrossingDraggedWithEndWaiting_BeginsAtTheEndSnapped_AndSpendsTheOverride()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    double near = 4.0 / canvas.View.PixelsPerUnit;
                    model.StartCommand("ERASE");
                    model.Execute("END");
                    Assert.Equal(CadSnapModes.Endpoint, model.Snap.Override);

                    CanvasMouseTests.Drag(
                        canvas,
                        CanvasMouseTests.At(canvas, 4000 + near, 3000 - near),
                        CanvasMouseTests.At(canvas, 3000, 2000)
                    );

                    Assert.Contains("1 found", model.History);
                    Assert.Null(model.Snap.Override);
                    Assert.Null(model.Session.SnapOverride);
                }
            );
        }

        [Fact]
        public void ACrossingDraggedWithNoOverride_TouchesNothingWhereItLies()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    double near = 4.0 / canvas.View.PixelsPerUnit;
                    model.StartCommand("ERASE");

                    CanvasMouseTests.Drag(
                        canvas,
                        CanvasMouseTests.At(canvas, 4000 + near, 3000 - near),
                        CanvasMouseTests.At(canvas, 3000, 2000)
                    );

                    Assert.DoesNotContain("1 found", model.History);
                }
            );
        }
    }
}
