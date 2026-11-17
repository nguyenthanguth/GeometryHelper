using System;
using System.Linq;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.CanvasMouseTests;
using static GeometryHelper.Cad.Wpf.UnitTest.CanvasViewportDoubleClickTests;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Object snaps in a layout, offscreen on an STA thread: the room's viewport is 200 by 150 about (150, 100) on the sheet,
    /// showing the room at 1:20 about (2 000, 1 500); a model line from (1 000, 500) to (3 000, 2 500) is shown from (100, 50)
    /// to (200, 150) on the sheet, the column of radius 100 about (2 000, 1 500) as a circle of 5 about (150, 100). The cursor
    /// snaps to what the viewport shows, from the paper and through the viewport once it is worked through.
    /// </summary>
    public class CanvasViewportSnapTests
    {
        private static void WithModelLine(DxfDatabase db, string viewport)
        {
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(1000, 500, 0), new GeoPoint3(3000, 2500, 0)) { Layer = "WALLS" }
            );
        }

        private static void SnapOn(MainViewModel model, CadSnapModes modes)
        {
            model.Snap.Modes = modes;
            model.Toggles.Single(t => t.Name == "OSNAP").IsOn = true;
        }

        /// <summary>Where the cursor goes to ask for a point at a point of the sheet, a few pixels off it.</summary>
        private static Point Near(CadCanvas canvas, double x, double y, double pixels = 3.0)
        {
            Point at = At(canvas, x, y);
            return new Point(at.X + pixels, at.Y - pixels);
        }

        [Fact]
        public void FromThePaper_TheEndOfAModelLineShownInTheViewport_IsSnappedToAtItsPlaceOnTheSheet()
        {
            OnLayout(WithModelLine, (model, canvas, viewport) =>
            {
                SnapOn(model, CadSnapModes.Endpoint);
                model.StartCommand("LINE");

                Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 200, 150));

                Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);
                Assert.Equal(200.0, canvas.CurrentSnap.Point.X, 6);
                Assert.Equal(150.0, canvas.CurrentSnap.Point.Y, 6);
            });
        }

        [Fact]
        public void FromThePaper_TheCentreOfAModelCircleAndAPointOnItsRim_AreSnappedToThroughTheViewport()
        {
            OnLayout(WithModelLine, (model, canvas, viewport) =>
            {
                SnapOn(model, CadSnapModes.Center | CadSnapModes.Quadrant);
                model.StartCommand("LINE");

                Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 155, 100));
                Assert.Equal(CadSnapKind.Quadrant, canvas.CurrentSnap.Kind);
                Assert.Equal(155.0, canvas.CurrentSnap.Point.X, 6);
                Assert.Equal(100.0, canvas.CurrentSnap.Point.Y, 6);
            });
        }

        [Fact]
        public void FromThePaper_AClickOnAModelEndpoint_StartsThePaperLineThere()
        {
            OnLayout(WithModelLine, (model, canvas, viewport) =>
            {
                SnapOn(model, CadSnapModes.Endpoint);
                model.StartCommand("LINE");

                Click(canvas, Near(canvas, 100, 50));
                Click(canvas, Near(canvas, 200, 150));
                model.Execute(string.Empty);

                var line = model.Document.CurrentSpace.Block.Cast<DxfEntity>().OfType<DxfLine>().Single();
                Assert.Equal(100.0, line.StartPoint.X, 6);
                Assert.Equal(50.0, line.StartPoint.Y, 6);
                Assert.Equal(200.0, line.EndPoint.X, 6);
                Assert.Equal(150.0, line.EndPoint.Y, 6);
            });
        }

        [Fact]
        public void FromThePaper_APointOutsideTheViewportsFrame_SnapsToNothingOfTheModel()
        {
            OnLayout(
                (db, viewport) =>
                    db.ModelSpace.AppendEntity(
                        new DxfLine(new GeoPoint3(1000, 500, 0), new GeoPoint3(5000, 500, 0)) { Layer = "WALLS" }
                    ),
                (model, canvas, viewport) =>
                {
                    SnapOn(model, CadSnapModes.Endpoint);
                    model.StartCommand("LINE");

                    // (5 000, 500) would be shown at (300, 50), off the frame, where the viewport shows nothing.
                    Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 300, 50));

                    Assert.Equal(CadSnapKind.None, canvas.CurrentSnap.Kind);
                }
            );
        }

        [Fact]
        public void FromThePaper_ALayerFrozenInTheViewport_IsNotSnappedTo()
        {
            OnLayout(WithModelLine, (model, canvas, viewport) =>
            {
                DxfViewport frame = (DxfViewport)model.Document.FindEntity(viewport);
                frame.FreezeLayersInViewport(new[] { model.Document.Database.LayerTable["WALLS"] });
                model.Document.Regen();
                SnapOn(model, CadSnapModes.Endpoint);
                model.StartCommand("LINE");

                Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 200, 150));

                Assert.Equal(CadSnapKind.None, canvas.CurrentSnap.Kind);
            });
        }

        [Fact]
        public void FromThePaper_AClosePaperObjectWinsOverAFarModelOne()
        {
            OnLayout(
                (db, viewport) =>
                {
                    WithModelLine(db, viewport);
                    ((DxfBlockTableRecord)FrameOf(db, viewport).Owner).AppendEntity(
                        new DxfLine(new GeoPoint3(199, 148, 0), new GeoPoint3(220, 148, 0))
                    );
                },
                (model, canvas, viewport) =>
                {
                    SnapOn(model, CadSnapModes.Endpoint);
                    model.StartCommand("LINE");

                    Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 199, 148, 1.0));

                    Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);
                    Assert.Equal(199.0, canvas.CurrentSnap.Point.X, 6);
                    Assert.Equal(148.0, canvas.CurrentSnap.Point.Y, 6);
                }
            );
        }

        [Fact]
        public void FromThePaper_ModelSpaceOutsideAClippedViewportsOutline_IsNotSnappedTo()
        {
            OnLayout(
                (db, viewport) =>
                {
                    WithModelLine(db, viewport);
                    DxfViewport frame = FrameOf(db, viewport);
                    var triangle = new DxfPolyline();
                    triangle.AddVertexAt(0, new GeoPoint2(60, 30), 0.0, 0.0, 0.0);
                    triangle.AddVertexAt(1, new GeoPoint2(240, 30), 0.0, 0.0, 0.0);
                    triangle.AddVertexAt(2, new GeoPoint2(240, 170), 0.0, 0.0, 0.0);
                    triangle.Closed = true;
                    ((DxfBlockTableRecord)frame.Owner).AppendEntity(triangle);
                    frame.ClipTo(triangle);
                },
                (model, canvas, viewport) =>
                {
                    var frame = (DxfViewport)model.Document.FindEntity(viewport);
                    DisplayClip2 outline = CadViewportClips.OutlineOf(frame);
                    GeoPoint2 inside = CadViewports.ToPaper(frame, new GeoPoint2(1000, 500));
                    GeoPoint2 outside = CadViewports.ToPaper(frame, new GeoPoint2(3000, 2500));
                    Assert.True(outline.Contains(inside.X, inside.Y));
                    Assert.False(outline.Contains(outside.X, outside.Y));
                    SnapOn(model, CadSnapModes.Endpoint);
                    model.StartCommand("LINE");

                    Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, inside.X, inside.Y));
                    Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);
                    Assert.Equal(inside.X, canvas.CurrentSnap.Point.X, 6);

                    Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, outside.X, outside.Y));
                    Assert.Equal(CadSnapKind.None, canvas.CurrentSnap.Kind);
                }
            );
        }

        [Fact]
        public void TheCornerOfTheViewportsFrame_IsSnappedToFromThePaper()
        {
            OnLayout(
                (db, viewport) =>
                {
                    // A frame narrower than the room's walls, so that no model endpoint lies at its corner.
                    DxfViewport frame = FrameOf(db, viewport);
                    frame.Width = 160;
                    frame.ViewHeight = 150 * 20.0;
                },
                (model, canvas, viewport) =>
                {
                    SnapOn(model, CadSnapModes.Endpoint | CadSnapModes.Midpoint);
                    model.StartCommand("LINE");

                    Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 70, 25));
                    Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);
                    Assert.Equal(70.0, canvas.CurrentSnap.Point.X, 6);
                    Assert.Equal(25.0, canvas.CurrentSnap.Point.Y, 6);
                    Assert.Equal(viewport, canvas.CurrentSnap.Handle);

                    Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 230, 100));
                    Assert.Equal(CadSnapKind.Midpoint, canvas.CurrentSnap.Kind);
                    Assert.Equal(230.0, canvas.CurrentSnap.Point.X, 6);
                    Assert.Equal(100.0, canvas.CurrentSnap.Point.Y, 6);
                }
            );
        }

        [Fact]
        public void WorkingThroughTheViewport_TheEndOfAModelLineIsSnappedTo_InModelUnits()
        {
            OnLayout(WithModelLine, (model, canvas, viewport) =>
            {
                SnapOn(model, CadSnapModes.Endpoint);
                model.Document.EnterViewport(viewport);
                canvas.RenderNow();
                model.StartCommand("LINE");

                Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 200, 150));

                Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);
                Assert.Equal(3000.0, canvas.CurrentSnap.Point.X, 6);
                Assert.Equal(2500.0, canvas.CurrentSnap.Point.Y, 6);
            });
        }

        [Fact]
        public void WorkingThroughTheViewport_TheApertureIsTenPixelsOnTheScreen_NotInModelUnits()
        {
            OnLayout(WithModelLine, (model, canvas, viewport) =>
            {
                SnapOn(model, CadSnapModes.Endpoint);
                model.Document.EnterViewport(viewport);
                canvas.RenderNow();
                model.StartCommand("LINE");

                Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 200, 150, 6.0));
                Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);

                Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 200, 150, 40.0));
                Assert.NotEqual(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);
            });
        }

        [Fact]
        public void WorkingThroughATwistedViewport_TheSnapPointIsMappedThroughTheTurn()
        {
            OnLayout(WithModelLine, (model, canvas, viewport) =>
            {
                DxfViewport frame = (DxfViewport)model.Document.FindEntity(viewport);
                frame.TwistAngle = Math.PI / 2.0;
                frame.ViewCenter = new GeoPoint2(-1500, 2000);
                model.Document.Regen();
                SnapOn(model, CadSnapModes.Endpoint);
                model.Document.EnterViewport(viewport);
                canvas.RenderNow();
                model.StartCommand("LINE");
                GeoPoint2 shown = CadViewports.ToPaper(frame, new GeoPoint2(3000, 2500));

                Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, shown.X, shown.Y));

                Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);
                Assert.Equal(3000.0, canvas.CurrentSnap.Point.X, 6);
                Assert.Equal(2500.0, canvas.CurrentSnap.Point.Y, 6);

                // From the paper, the same end is snapped to where the turned view shows it.
                model.Document.LeaveViewport();
                canvas.RenderNow();
                model.Session.Escape();
                model.StartCommand("LINE");
                Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, shown.X, shown.Y));
                Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);
                Assert.Equal(shown.X, canvas.CurrentSnap.Point.X, 6);
                Assert.Equal(shown.Y, canvas.CurrentSnap.Point.Y, 6);
            });
        }

        [Fact]
        public void WorkingThroughTheViewport_ALayerFrozenInIt_IsNotSnappedTo()
        {
            OnLayout(WithModelLine, (model, canvas, viewport) =>
            {
                DxfViewport frame = (DxfViewport)model.Document.FindEntity(viewport);
                frame.FreezeLayersInViewport(new[] { model.Document.Database.LayerTable["WALLS"] });
                model.Document.Regen();
                SnapOn(model, CadSnapModes.Endpoint);
                model.Document.EnterViewport(viewport);
                canvas.RenderNow();
                model.StartCommand("LINE");

                Mouse(canvas, System.Windows.UIElement.MouseMoveEvent, Near(canvas, 200, 150));

                Assert.Equal(CadSnapKind.None, canvas.CurrentSnap.Kind);
            });
        }
    }
}
