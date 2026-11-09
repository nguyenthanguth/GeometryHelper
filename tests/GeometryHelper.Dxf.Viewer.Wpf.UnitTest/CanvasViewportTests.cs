using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Model space worked in through a viewport of Layout1 on the canvas (MSPACE, PSPACE), offscreen on an STA thread: the
    /// room's viewport is 200 by 150 about (150, 100) on the sheet, showing the room at 1:20 about (2 000, 1 500).
    /// </summary>
    public class CanvasViewportTests
    {
        private const int Width = 800;
        private const int Height = 600;

        private static void OnLayout(Action<MainViewModel, CadCanvas, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", TestFiles.RoomWithViewport(folder, out _, out string viewport)));
                        model.SelectSpace(model.Document.Spaces.Single(s => s.Name == "Layout1"));
                        var canvas = new CadCanvas { Document = model.Document, Space = model.Space, View = model.View, Session = model.Session, Snap = model.Snap };
                        canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                        canvas.Measure(new Size(Width, Height));
                        canvas.Arrange(new Rect(0, 0, Width, Height));
                        canvas.RenderNow();
                        model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                        test(model, canvas, viewport);
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

        private static DxfViewport Frame(MainViewModel model, string handle) => (DxfViewport)model.Document.FindEntity(handle);

        [Fact]
        public void ADoubleClickInsideTheViewport_WorksThroughIt_AClickPicksModelSpace_AndADoubleClickOffItGoesBackToThePaper()
        {
            OnLayout((model, canvas, viewport) =>
            {
                Assert.Equal("PAPER", model.SpaceLabel);

                Assert.True(canvas.ViewportClick(At(canvas, 150, 120), true));
                Assert.Equal(viewport, canvas.ThroughHandle);
                Assert.Equal("MODEL", model.SpaceLabel);
                Assert.Same(model.Document.ModelSpace, model.Document.WorkingSpace);

                // The room's bottom wall, (0, 0) to (4 000, 0), is shown at paper y 100 - 1 500 / 20 = 25: a click there picks it.
                Click(canvas, At(canvas, 150, 25));
                string picked = Assert.Single(model.Document.Selection.Handles);
                Assert.Same(model.Document.ModelSpace, model.Document.FindSpace(picked));
                canvas.RenderNow();
                Assert.True(canvas.GripCount > 0);

                // A click off the frame is taken by nothing; a double click there goes back to the paper.
                Assert.True(canvas.ViewportClick(At(canvas, 10, 10), false));
                Assert.Equal(viewport, canvas.ThroughHandle);
                Assert.True(canvas.ViewportClick(At(canvas, 10, 10), true));
                Assert.Null(canvas.ThroughHandle);
                Assert.Equal("PAPER", model.SpaceLabel);
            });
        }

        [Fact]
        public void TheWheelThroughTheViewport_ZoomsItsViewNotThePaper_OneUndoStepOnceStill()
        {
            OnLayout((model, canvas, viewport) =>
            {
                model.Document.EnterViewport(viewport);
                canvas.RenderNow();
                double paperZoom = canvas.View.PixelsPerUnit;
                Point at = At(canvas, 150, 100);

                Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                canvas.RenderNow();

                Assert.Equal(paperZoom, canvas.View.PixelsPerUnit);
                Assert.Equal(3000 / 1.44, Frame(model, viewport).ViewHeight, 6);
                Assert.Equal(2000, Frame(model, viewport).ViewCenter.X, 6);
                Assert.Equal("ZOOM", model.Document.NextUndo.Name);
                Assert.Equal("1:13.889", model.ViewportScale);

                Assert.True(model.Document.Undo());
                Assert.Equal(3000, Frame(model, viewport).ViewHeight, 9);
            });
        }

        [Fact]
        public void ALockedViewport_TheWheelZoomsThePaper()
        {
            OnLayout((model, canvas, viewport) =>
            {
                Frame(model, viewport).Locked = true;
                model.Document.EnterViewport(viewport);
                double paperZoom = canvas.View.PixelsPerUnit;

                Mouse(canvas, UIElement.MouseWheelEvent, At(canvas, 150, 100), delta: 120);
                canvas.RenderNow();

                Assert.Equal(paperZoom * 1.2, canvas.View.PixelsPerUnit, 9);
                Assert.Equal(3000, Frame(model, viewport).ViewHeight, 9);
            });
        }

        [Fact]
        public void TheModelPaperButton_RunsMspaceAndPspace_CtrlRCycles_AndTheScaleListSetsTheScale()
        {
            OnLayout((model, canvas, viewport) =>
            {
                Assert.False(model.HasViewportScale);

                model.ToggleSpaceCommand.Execute(null);
                Assert.Equal("MODEL", model.SpaceLabel);
                Assert.True(model.HasViewportScale);
                Assert.Equal("1:20", model.ViewportScale);

                model.ViewportScale = "1:50";
                Assert.Equal(7500, Frame(model, viewport).ViewHeight, 9);
                Assert.Equal("1:50", model.ViewportScale);

                // A second viewport: Ctrl+R goes to it and back.
                model.ToggleSpaceCommand.Execute(null);
                Assert.Equal("PAPER", model.SpaceLabel);
                model.StartCommand("MV");
                model.Execute("260,20");
                model.Execute("380,120");
                string second = model.Document.CurrentSpace.Block.Cast<DxfEntity>().OfType<DxfViewport>().Last().Handle;
                model.ToggleSpaceCommand.Execute(null);
                Assert.Equal(viewport, model.Document.ActiveViewport.Handle);
                Assert.True(model.RunKey("Ctrl+R"));
                Assert.Equal(second, model.Document.ActiveViewport.Handle);
                Assert.True(model.RunKey("Ctrl+R"));
                Assert.Equal(viewport, model.Document.ActiveViewport.Handle);
            });
        }
    }
}
