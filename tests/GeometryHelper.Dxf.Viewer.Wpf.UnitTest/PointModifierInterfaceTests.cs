using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The point modifiers in the window: Temporary track point, From, Mid Between 2 Points and Point Filters on the
    /// Shift+right-click menu, in AutoCAD's order, and each point a modifier asks snapped as any other.
    /// </summary>
    public sealed class PointModifierInterfaceTests
    {
        [Fact]
        public void TheShiftRightClickMenuStartsWithTheModifiers_AndPointFiltersOpensTheSix()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            model.StartCommand("LINE");

            IReadOnlyList<CadMenuItemViewModel> items = model.SnapOverrideMenu();

            Assert.Equal(
                new[]
                {
                    "Temporary track point",
                    "From",
                    "Mid Between 2 Points",
                    "Point Filters",
                    string.Empty,
                    "Endpoint",
                },
                items.Take(6).Select(i => i.Header)
            );
            CadMenuItemViewModel filters = items.Single(i => i.Header == "Point Filters");
            Assert.False(filters.IsSeparator);
            Assert.Equal(new[] { ".X", ".Y", ".Z", ".XY", ".XZ", ".YZ" }, filters.Items.Select(i => i.Header));
            Assert.True(items[4].IsSeparator);
        }

        [Fact]
        public void FromChosenFromTheMenu_TakesASnappedBasePoint_AndTheOffsetTyped()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            model.StartCommand("LINE");

            model.SnapOverrideMenu().Single(i => i.Header == "From").Command.Execute(null);
            Assert.Equal("Base point:", model.Prompt);
            Assert.Contains("Specify first point: _from", model.History);

            model.Execute("END");
            Assert.Equal(CadSnapModes.Endpoint, model.Snap.Override);
            model.ClickAt(new GeoPoint2(3999.5, 0.4), 2.0);
            Assert.Equal("<Offset>:", model.Prompt);
            Assert.Null(model.Snap.Override);

            model.Execute("@0,500");
            model.Execute("@100,0");
            model.Escape();

            DxfLine drawn = model.Document.ModelSpace.Block.OfType<DxfLine>().Last();
            Assert.Equal(4000.0, drawn.StartPoint.X, 12);
            Assert.Equal(500.0, drawn.StartPoint.Y, 12);
            Assert.Equal(4100.0, drawn.EndPoint.X, 12);
        }

        [Fact]
        public void MidBetweenTwoPointsFromTheMenu_TakesEachPointSnapped()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
            model.StartCommand("POINT");

            model.SnapOverrideMenu().Single(i => i.Header == "Mid Between 2 Points").Command.Execute(null);
            Assert.Equal("First point of mid:", model.Prompt);
            model.Execute("END");
            model.ClickAt(new GeoPoint2(0.5, 0.4), 2.0);
            Assert.Equal("Second point of mid:", model.Prompt);
            model.Execute("END");
            model.ClickAt(new GeoPoint2(3999.5, 2999.6), 2.0);

            DxfDBPoint point = model.Document.ModelSpace.Block.OfType<DxfDBPoint>().Single();
            Assert.Equal(2000.0, point.Position.X, 12);
            Assert.Equal(1500.0, point.Position.Y, 12);
            Assert.False(model.IsCommandRunning);
        }

        [Fact]
        public void APointFilterFromTheMenu_AsksOfThenTheCoordinatesItLacks()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            model.StartCommand("POINT");

            model
                .SnapOverrideMenu()
                .Single(i => i.Header == "Point Filters")
                .Items.Single(i => i.Header == ".X")
                .Command.Execute(null);
            Assert.Equal("of", model.Prompt);
            Assert.Contains("Specify a point: .X", model.History);

            model.Execute("MID");
            model.ClickAt(new GeoPoint2(2001, 3000.5), 2.0);
            Assert.Equal("(need YZ):", model.Prompt);
            model.Execute("750,0");

            DxfDBPoint point = model.Document.ModelSpace.Block.OfType<DxfDBPoint>().Single();
            Assert.Equal(2000.0, point.Position.X, 12);
            Assert.Equal(750.0, point.Position.Y, 12);
        }

        [Fact]
        public void ATemporaryTrackPoint_IsAcquiredForTracking_UntilThePointIsGiven()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            model.StartCommand("LINE");

            model.SnapOverrideMenu().Single(i => i.Header == "Temporary track point").Command.Execute(null);
            Assert.Equal("Specify temporary OTRACK point:", model.Prompt);
            model.ClickAt(new GeoPoint2(2000.5, 0.4), 2.0);

            Assert.Equal("Specify first point:", model.Prompt);
            CadAcquiredPoint acquired = Assert.Single(model.Snap.Acquired);
            Assert.Equal(2000.0, acquired.Point.X, 12);
            Assert.Equal(0.0, acquired.Point.Y, 12);

            CadSnapResult tracked = model.SnapAt(new GeoPoint2(2000.3, 700), 2.0);
            Assert.Equal(CadSnapKind.Tracking, tracked.Kind);
            Assert.Equal(2000.0, tracked.Point.X, 12);
            Assert.Equal(700.0, tracked.Point.Y, 12);

            model.ClickAt(new GeoPoint2(2000.3, 700), 2.0);

            Assert.Empty(model.Snap.Acquired);
            Assert.Equal(2000.0, model.Session.LastPoint.X, 12);
        }

        /// <summary>The room shown, the snap grid (SNAP, on by default) off so that a point not snapped is the cursor.</summary>
        private static void OpenRoom(MainViewModel model)
        {
            model.OpenDocument(CadDocument.From(TestFiles.Room(out _, out _), "room.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
        }
    }
}
