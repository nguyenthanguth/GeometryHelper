using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
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
                        model.Open(
                            folder.Write("room.dxf", TestFiles.RoomWithViewport(folder, out _, out string viewport))
                        );
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

        private static DxfViewport Frame(MainViewModel model, string handle) =>
            (DxfViewport)model.Document.FindEntity(handle);

        [Fact]
        public void ADoubleClickInsideTheViewport_WorksThroughIt_AClickPicksModelSpace_AndADoubleClickOffItGoesBackToThePaper()
        {
            OnLayout(
                (model, canvas, viewport) =>
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
                }
            );
        }

        [Fact]
        public void AGripClickedThroughTheViewport_StretchesInModelSpace()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    model.Document.EnterViewport(viewport);
                    canvas.RenderNow();

                    // The bottom wall's end (4 000, 0) is shown at paper (250, 25); its grip clicked, then the point (4 000, 400)
                    // shown at (250, 45) clicked, the end goes there in model space.
                    Click(canvas, At(canvas, 200, 25));
                    canvas.RenderNow();
                    string wall = Assert.Single(model.Document.Selection.Handles);
                    Click(canvas, At(canvas, 249.99, 25));
                    Assert.True(model.Session.IsGripEditing);
                    Click(canvas, At(canvas, 250, 45));

                    var line = (DxfLine)model.Document.FindEntity(wall);
                    Assert.Equal(4000, line.EndPoint.X, 6);
                    Assert.Equal(400, line.EndPoint.Y, 6);
                    Assert.Same(model.Document.ModelSpace, model.Document.FindSpace(wall));
                }
            );
        }

        [Fact]
        public void TheWheelThroughTheViewport_ZoomsItsViewNotThePaper_OneUndoStepOnceStill()
        {
            OnLayout(
                (model, canvas, viewport) =>
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
                }
            );
        }

        [Fact]
        public void ATwistedViewport_IsWorkedThroughTurned_ItsPicksBoxesAndUcsIconTurnedWithIt()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    // Twisted a quarter turn about the room's middle, as DVIEW TWist 90 leaves it: the view's centre (-1 500, 2 000)
                    // is the room's middle turned. The bottom wall then runs up the frame at paper x 225.
                    DxfViewport frame = Frame(model, viewport);
                    frame.TwistAngle = Math.PI / 2.0;
                    frame.ViewCenter = new GeoPoint2(-1500, 2000);
                    model.Document.Regen();
                    Assert.True(canvas.ViewportClick(At(canvas, 150, 120), true));
                    Assert.Equal(viewport, canvas.ThroughHandle);
                    canvas.RenderNow();

                    Click(canvas, At(canvas, 225, 140));
                    string picked = Assert.Single(model.Document.Selection.Handles);
                    Assert.Equal(0.0, ((DxfLine)model.Document.FindEntity(picked)).StartPoint.Y, 9);

                    // The X arm of the icon, and the crosshair, run up the canvas.
                    Assert.True(canvas.TryPlaceUcsIcon(out _, out Vector xArm, out _));
                    Assert.Equal(0.0, xArm.X, 9);
                    Assert.Equal(-1.0, xArm.Y, 9);

                    // A crossing box right to left over the top wall, shown at paper x 75, takes it alone.
                    model.Document.Selection.Clear();
                    Click(canvas, At(canvas, 85, 60));
                    Click(canvas, At(canvas, 65, 70));
                    picked = Assert.Single(model.Document.Selection.Handles);
                    Assert.Equal(3000.0, ((DxfLine)model.Document.FindEntity(picked)).StartPoint.Y, 9);
                }
            );
        }

        [Fact]
        public void WheelRollsAndAPanDragDrawnBetween_AreOneUndoStep()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    model.Document.EnterViewport(viewport);
                    canvas.RenderNow();
                    CadUndoStep start = model.Document.NextUndo;
                    Point at = At(canvas, 150, 100);

                    // A roll, the view drawn again (the canvas still for a moment), another roll, and a drag of the middle button
                    // drawn again half way: one step, as AutoCAD combines consecutive real-time zooms and pans.
                    Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                    canvas.RenderNow();
                    Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                    canvas.RenderNow();
                    Mouse(canvas, UIElement.MouseMoveEvent, at);
                    Mouse(canvas, UIElement.MouseDownEvent, at, MouseButton.Middle);
                    Mouse(canvas, UIElement.MouseMoveEvent, new Point(at.X + 20, at.Y));
                    canvas.RenderNow();
                    Mouse(canvas, UIElement.MouseMoveEvent, new Point(at.X + 40, at.Y));
                    Mouse(canvas, UIElement.MouseUpEvent, new Point(at.X + 40, at.Y), MouseButton.Middle);
                    canvas.RenderNow();

                    Assert.Equal(3000 / 1.44, Frame(model, viewport).ViewHeight, 6);
                    Assert.NotEqual(2000, Frame(model, viewport).ViewCenter.X, 6);
                    Assert.Equal("ZOOM", model.Document.NextUndo.Name);
                    Assert.True(model.Document.Undo());
                    Assert.Same(start, model.Document.NextUndo);
                    Assert.Equal(3000, Frame(model, viewport).ViewHeight, 9);
                    Assert.Equal(2000, Frame(model, viewport).ViewCenter.X, 9);
                }
            );
        }

        [Fact]
        public void ALockedViewport_TheWheelZoomsThePaper()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    Frame(model, viewport).Locked = true;
                    model.Document.EnterViewport(viewport);
                    double paperZoom = canvas.View.PixelsPerUnit;

                    Mouse(canvas, UIElement.MouseWheelEvent, At(canvas, 150, 100), delta: 120);
                    canvas.RenderNow();

                    Assert.Equal(paperZoom * 1.2, canvas.View.PixelsPerUnit, 9);
                    Assert.Equal(3000, Frame(model, viewport).ViewHeight, 9);
                }
            );
        }

        [Fact]
        public void TheModelPaperButton_RunsMspaceAndPspace_CtrlRCycles_AndTheScaleListSetsTheScale()
        {
            OnLayout(
                (model, canvas, viewport) =>
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
                    string second = model
                        .Document.CurrentSpace.Block.Cast<DxfEntity>()
                        .OfType<DxfViewport>()
                        .Last()
                        .Handle;
                    model.ToggleSpaceCommand.Execute(null);
                    Assert.Equal(viewport, model.Document.ActiveViewport.Handle);
                    Assert.True(model.RunKey("Ctrl+R"));
                    Assert.Equal(second, model.Document.ActiveViewport.Handle);
                    Assert.True(model.RunKey("Ctrl+R"));
                    Assert.Equal(viewport, model.Document.ActiveViewport.Handle);
                }
            );
        }
    }
}
