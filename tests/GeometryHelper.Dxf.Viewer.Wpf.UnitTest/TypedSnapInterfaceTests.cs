using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A point typed on the command line after END or another object snap typed or chosen from the Shift+right-click menu is
    /// snapped as AutoCAD 2021 snaps it (<see cref="CadCommandSession.SnapOverride"/>): the window sets the override with the
    /// snap's own and the aperture with the cursor.
    /// </summary>
    public sealed class TypedSnapInterfaceTests
    {
        /// <summary>A line from (0, 0) to (100, 0), with the grid snap off so that a point not snapped is the point typed.</summary>
        private static MainViewModel Open()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "line.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
            model.Session.SnapAperture = 1.3;
            return model;
        }

        [Fact]
        public void EndTyped_ThenAPointNearTheLine_GivesTheNearerEnd()
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");

            model.Execute("END");
            Assert.Equal(CadSnapModes.Endpoint, model.Session.SnapOverride);
            model.Execute("60,1");

            Assert.Equal(100.0, model.Session.LastPoint.X, 9);
            Assert.Equal(0.0, model.Session.LastPoint.Y, 9);
            Assert.Null(model.Session.SnapOverride);
            Assert.Null(model.Snap.Override);
        }

        [Fact]
        public void EndTyped_ThenAPointNearNothing_SaysNoEndpointFound_AndAsksAgain()
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");

            model.Execute("END");
            model.Execute("60,5");

            Assert.Equal(
                new[] { "No Endpoint found for specified point.", "Invalid point." },
                model.History.Skip(model.History.Count - 2)
            );
            Assert.Equal("Specify first point:", model.Prompt);
            Assert.Null(model.Snap.Override);
        }

        [Fact]
        public void EndChosenFromTheMenu_ThenAPointTypedAtSelectObjects_IsSnappedToo()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");

            model.SnapOverrideMenu().Single(i => i.Header == "Endpoint").Command.Execute(null);
            model.Execute("60,5");

            Assert.Contains("No Endpoint found for specified point.", model.History);
            Assert.Contains("*Invalid selection*", model.History);
            Assert.Equal("Select objects:", model.Prompt);
        }

        [Fact]
        public void AWiderAperture_FindsALineFurtherFromTheTypedPoint()
        {
            MainViewModel model = Open();
            model.Session.SnapAperture = 20.0;
            model.StartCommand("LINE");

            model.Execute("END");
            model.Execute("60,15");

            Assert.Equal(100.0, model.Session.LastPoint.X, 9);
        }
    }
}
