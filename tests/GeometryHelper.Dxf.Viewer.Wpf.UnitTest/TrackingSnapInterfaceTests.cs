using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Extension, Insertion, Apparent Intersection, Parallel and object snap tracking in the window (C16): OTRACK and F11, the
    /// menus, Drafting Settings, the overrides typed, a click that picks the first object, and a rest on the canvas.
    /// </summary>
    public sealed class TrackingSnapInterfaceTests
    {
        [Fact]
        public void OtrackStandsBeforeOsnapOnTheStatusBar_OnAsInAutoCAD_AndF11SwitchesIt()
        {
            var model = new MainViewModel();
            OpenRoom(model);

            StatusToggleViewModel otrack = model.Toggles.Single(t => t.Name == "OTRACK");
            Assert.Equal(model.Toggles.IndexOf(model.Toggles.Single(t => t.Name == "OSNAP")) - 1, model.Toggles.IndexOf(otrack));
            Assert.True(otrack.IsOn);
            Assert.True(model.Snap.Tracking);

            Assert.True(model.RunKey("F11"));

            Assert.False(otrack.IsOn);
            Assert.False(model.Snap.Tracking);
        }

        [Fact]
        public void TheFourSnapsAreOnTheMenus_AndDraftingSettingsTicksThemAndTracking()
        {
            var model = new MainViewModel();
            OpenRoom(model);

            IReadOnlyList<CadMenuItemViewModel> menu = model.OsnapMenu();
            Assert.All(menu.Where(i => !i.IsSeparator), i => Assert.True(i.IsEnabled));
            menu.Single(i => i.Header == "Extension").Command.Execute(null);
            Assert.Equal(CadSnapModes.Extension, model.SnapModes & CadSnapModes.Extension);

            DraftingSettingsViewModel dialog = model.CreateDraftingSettings();
            Assert.True(dialog.IsTrackingOn);
            dialog.ClearAllCommand.Execute(null);
            dialog.Modes.Single(r => r.Name == "Parallel").IsChecked = true;
            dialog.Modes.Single(r => r.Name == "Insertion").IsChecked = true;
            dialog.IsTrackingOn = false;
            Assert.True(dialog.Apply());

            Assert.Equal(CadSnapModes.Parallel | CadSnapModes.Insertion, model.SnapModes);
            Assert.Equal(CadSnapModes.Parallel | CadSnapModes.Insertion, model.Settings.Snaps);
            Assert.False(model.Toggles.Single(t => t.Name == "OTRACK").IsOn);

            model.StartCommand("LINE");
            Assert.Equal(new[] { "Endpoint", "Midpoint", "Intersection", "Apparent Intersection", "Extension", "Center", "Geometric Center", "Quadrant", "Tangent", "Perpendicular", "Parallel", "Node", "Insertion", "Nearest" },
                model.SnapOverrideMenu().SkipWhile(i => i.Header != "Endpoint").TakeWhile(i => i.Header != "None").Select(i => i.Header));
        }

        [Fact]
        public void ExtParAppAppintAndInsTypedAtAPointPrompt_TakeTheirSnapForThatPoint()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            model.StartCommand("LINE");

            var typed = new[] { ("EXT", CadSnapModes.Extension), ("PAR", CadSnapModes.Parallel), ("APP", CadSnapModes.ApparentIntersection), ("APPINT", CadSnapModes.ApparentIntersection), ("INS", CadSnapModes.Insertion) };

            foreach ((string word, CadSnapModes mode) in typed)
            {
                model.Execute(word);
                Assert.Equal(mode, model.Snap.Override);
            }

            Assert.Contains("Specify first point: _ext of", model.History);
            Assert.Contains("Specify first point: _ins of", model.History);
            Assert.True(model.IsCommandRunning);
        }

        [Fact]
        public void AppTyped_AClickOnTheBottomWallPicksIt_AndAClickOnAnotherLineTakesWhereTheyWouldCross()
        {
            var model = new MainViewModel();
            DxfDatabase db = TestFiles.Room(out _, out _);
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(5000, 500, 0), new GeoPoint3(5000, 2000, 0)) { Layer = "WALLS" });
            model.OpenDocument(CadDocument.From(db, "room.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
            model.StartCommand("LINE");
            model.Execute("APP");

            Assert.Equal(CadSnapKind.ExtendedApparentIntersection, model.SnapAt(new GeoPoint2(1000, 1), 2.0).Kind);
            Assert.False(model.ClickAt(new GeoPoint2(1000, 1), 2.0));
            Assert.Equal("Specify first point:", model.Prompt);
            Assert.Equal(CadSnapKind.ApparentIntersection, model.SnapAt(new GeoPoint2(5001, 1000), 2.0).Kind);

            model.ClickAt(new GeoPoint2(5001, 1000), 2.0);
            model.ClickAt(new GeoPoint2(6000.5, 2500.5), 2.0);
            model.Escape();

            DxfLine drawn = model.Document.ModelSpace.Block.OfType<DxfLine>().Last();
            Assert.Equal(5000.0, drawn.StartPoint.X, 9);
            Assert.Equal(0.0, drawn.StartPoint.Y, 9);
        }

        [Fact]
        public void ARestOnTheEndOfTheWallAcquiresIt_TheCursorIsTrackedUpright_AndTheClickLetsTheAcquiredPointsGo()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var model = new MainViewModel();
                    OpenRoom(model);
                    var canvas = new CadCanvas { Document = model.Document, Space = model.Space, View = model.View, Session = model.Session, Snap = model.Snap };
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.Measure(new Size(800, 600));
                    canvas.Arrange(new Rect(0, 0, 800, 600));
                    canvas.RenderNow();
                    model.StartCommand("LINE");

                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, CanvasMouseTests.At(canvas, 3998, 2));
                    Assert.True(canvas.AcquireAtRest());
                    Assert.Equal(CadSnapKind.Endpoint, Assert.Single(model.Snap.Acquired).Kind);

                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, CanvasMouseTests.At(canvas, 4010, 1500));
                    Assert.Equal(CadSnapKind.Tracking, canvas.CurrentSnap.Kind);
                    Assert.Equal(4000.0, canvas.CurrentSnap.Point.X, 9);
                    Assert.Single(canvas.CurrentSnap.Tracks);
                    canvas.RenderNow();

                    CanvasMouseTests.Click(canvas, CanvasMouseTests.At(canvas, 4010, 1500));

                    Assert.Empty(model.Snap.Acquired);
                    model.Escape();
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

        /// <summary>The room shown, the snap grid (SNAP, on by default) off so that a point not snapped is the cursor.</summary>
        private static void OpenRoom(MainViewModel model)
        {
            model.OpenDocument(CadDocument.From(TestFiles.Room(out _, out _), "room.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
        }
    }
}
