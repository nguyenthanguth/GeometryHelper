using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// INT and APP clicked on the canvas over one object ask " and" for the second, as AutoCAD 2021 asks it for a point typed
    /// (<see cref="CadCommandSession.SnapOverride"/>): the second click, taken as it lies and not snapped, gives where the two
    /// cross, or "No Intersection found for specified point." with the prompt asked again.
    /// </summary>
    public sealed class ClickedIntersectionInterfaceTests
    {
        /// <summary>
        /// A line C1 from (0, 0) to (100, 0), a line from (50, -50) to (50, 50) crossing it, and a line from (150, -50) to (150, 50)
        /// it would meet run on; the snap on the grid off, so that a point not snapped is the point clicked.
        /// </summary>
        private static MainViewModel Open()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(50, -50, 0), new GeoPoint3(50, 50, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(150, -50, 0), new GeoPoint3(150, 50, 0)));
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "lines.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
            return model;
        }

        [Fact]
        public void IntClickedOverOneObject_AsksAnd_AndAClickOnTheSecondGivesTheirCrossing()
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");
            model.Execute("INT");

            Assert.True(model.ClickAt(new GeoPoint2(20, 0.5), 2.0));

            Assert.Equal(" and", model.Prompt);
            Assert.Null(model.Snap.Override);

            model.ClickAt(new GeoPoint2(50.5, 30), 2.0);

            Assert.Equal(50.0, model.Session.LastPoint.X, 9);
            Assert.Equal(0.0, model.Session.LastPoint.Y, 9);
            Assert.Equal("Specify next point or [Undo]:", model.Prompt);
        }

        [Fact]
        public void AppClickedOverOneObject_AsksAnd_AndGivesWhereTheTwoWouldCrossRunOn()
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");
            model.Execute("APP");

            Assert.True(model.ClickAt(new GeoPoint2(20, 0.5), 2.0));
            Assert.Equal(" and", model.Prompt);

            model.ClickAt(new GeoPoint2(150.5, 30), 2.0);

            Assert.Equal(150.0, model.Session.LastPoint.X, 9);
            Assert.Equal(0.0, model.Session.LastPoint.Y, 9);
        }

        [Fact]
        public void IntClickedWhereTwoObjectsCross_GivesTheCrossingAtOnce_AskingNothing()
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");
            model.Execute("INT");

            model.ClickAt(new GeoPoint2(50.3, 0.4), 2.0);

            Assert.Equal(50.0, model.Session.LastPoint.X, 9);
            Assert.Equal(0.0, model.Session.LastPoint.Y, 9);
            Assert.Equal("Specify next point or [Undo]:", model.Prompt);
        }

        [Fact]
        public void TheSecondClick_OnNoCrossingObject_SaysNoIntersectionFound_AndAsksThePromptAgain()
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");
            model.Execute("INT");
            model.ClickAt(new GeoPoint2(20, 0.5), 2.0);

            model.ClickAt(new GeoPoint2(150.5, 30), 2.0);

            Assert.Contains("No Intersection found for specified point.", model.History);
            Assert.Equal("Specify first point:", model.Prompt);
        }

        [Theory]
        [InlineData("END", " and END")]
        [InlineData("_end", " and _end")]
        [InlineData("QUA", " and QUA")]
        [InlineData("NON", " and NON")]
        public void ASnapTypedAtAnd_IsRefusedAsAnyTextIs_AndThePromptINTWasTypedAtIsAskedAgain(
            string typed,
            string echoed
        )
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");
            model.Execute("INT");
            model.ClickAt(new GeoPoint2(20, 0.5), 2.0);
            int before = model.History.Count;

            model.Execute(typed);

            Assert.Equal(new[] { echoed, "Invalid point." }, model.History.Skip(before));
            Assert.Equal("Specify first point:", model.Prompt);
            Assert.Null(model.Snap.Override);
            Assert.Null(model.Session.SnapOverride);
        }

        [Fact]
        public void ASnapChosenFromTheMenuAtAnd_IsRefusedToo()
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");
            model.Execute("INT");
            model.ClickAt(new GeoPoint2(20, 0.5), 2.0);

            model.SnapOverrideMenu().Single(i => i.Header == "Endpoint").Command.Execute(null);

            Assert.Contains(" and _end", model.History);
            Assert.Equal("Specify first point:", model.Prompt);
            Assert.Null(model.Snap.Override);
        }

        [Fact]
        public void IntClickedAtSelectObjects_OverOneObject_AsksAnd_AsItDoesForAPointTyped()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");
            model.Execute("INT");

            Assert.True(model.ClickAt(new GeoPoint2(20, 0.5), 2.0));

            Assert.Equal(" and", model.Prompt);
        }
    }
}
