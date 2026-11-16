using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Shift+right-click menu at "Select objects:" and at a prompt for one object, where AutoCAD 2021 shows it and takes TT,
    /// FROM, the other modifiers and the object snaps: the modifiers pick at the point they give, a snap takes the click.
    /// </summary>
    public sealed class SelectObjectsMenuInterfaceTests
    {
        /// <summary>A short line X from (50, 2.8) up to (50, 30), its end 1.8 from (50, 1), then a line Y from (0, 0) to (100, 0), 1 from it and drawn last.</summary>
        private static MainViewModel Open()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(50, 2.8, 0), new GeoPoint3(50, 30, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "two.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
            return model;
        }

        [Fact]
        public void TheMenuIsShownAtSelectObjects_WithTheItemsItHasAtAPoint()
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");
            string[] atAPoint = model.SnapOverrideMenu().Select(i => i.Header).ToArray();
            model.Escape();

            model.StartCommand("ERASE");

            Assert.Equal("Select objects:", model.Prompt);
            Assert.Equal(atAPoint, model.SnapOverrideMenu().Select(i => i.Header));
            Assert.True(CadPicking.WantsModifiers(model.Session));
        }

        [Fact]
        public void FromChosenAtSelectObjects_AsksItsBasePoint_ThePointItGivesPicking()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");

            model.SnapOverrideMenu().Single(i => i.Header == "From").Command.Execute(null);

            Assert.Contains("Select objects: _from", model.History);
            Assert.Equal("Base point:", model.Prompt);

            model.Execute("0,-10");
            model.Execute("@40,10");

            Assert.Equal("1 found", model.History.Last());
            Assert.Equal("Select objects:", model.Prompt);
        }

        [Fact]
        public void TemporaryTrackPointChosenAtSelectObjects_AsksItsPoint_AndSelectObjectsAgain()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");

            model.SnapOverrideMenu().Single(i => i.Header == "Temporary track point").Command.Execute(null);

            Assert.Contains("Select objects: _tt", model.History);
            Assert.Equal("Specify temporary OTRACK point:", model.Prompt);

            model.Execute("10,10");

            Assert.Equal("Select objects:", model.Prompt);
        }

        [Fact]
        public void AModifierChosenAtSelectObjects_IsStarted_NotAnsweredAsText()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");

            Assert.True(model.ModifyPoint("_m2p"));
            Assert.Equal("First point of mid:", model.Prompt);
            Assert.DoesNotContain("*Invalid selection*", model.History);
        }

        [Fact]
        public void EndpointChosenAtSelectObjects_TakesTheClickToTheEndOfTheLineNear_AndEndsWithThePick()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");

            model.SnapOverrideMenu().Single(i => i.Header == "Endpoint").Command.Execute(null);
            Assert.Equal(CadSnapModes.Endpoint, model.Snap.Override);
            Assert.Contains("Select objects: _end of", model.History);

            // (50, 1) is 1 from the long line, 1.8 from the end of the short one: the pick without the snap takes the long one;
            // the end of the short line within the aperture of 2 is the point the object is picked at.
            model.ClickAt(new GeoPoint2(50, 1), 2.0);

            Assert.Null(model.Snap.Override);
            Assert.Equal("1 found", model.History.Last());
            model.Execute(string.Empty);
            DxfLine[] left = model.Document.ModelSpace.Block.OfType<DxfLine>().ToArray();
            Assert.Single(left);
            Assert.Equal(100.0, left[0].EndPoint.X, 9);
        }

        [Fact]
        public void TheClickWithNoSnapOverride_PicksTheNearerLine()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");

            model.ClickAt(new GeoPoint2(50, 1), 2.0);
            model.Execute(string.Empty);

            DxfLine[] left = model.Document.ModelSpace.Block.OfType<DxfLine>().ToArray();
            Assert.Single(left);
            Assert.Equal(30.0, left[0].EndPoint.Y, 9);
        }

        [Fact]
        public void EndTyped_AtSelectObjects_TakesTheSnapForTheNextPick()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");

            model.Execute("END");

            Assert.Equal(CadSnapModes.Endpoint, model.Snap.Override);
            Assert.Contains("Select objects: _end of", model.History);
            Assert.DoesNotContain("*Invalid selection*", model.History);
        }

        [Fact]
        public void TheCommandMenuAtSelectObjects_HasOsnapOverridesEnabled()
        {
            MainViewModel model = Open();
            model.Execute("SHORTCUTMENU");
            model.Execute("15");
            model.StartCommand("ERASE");

            CadMenuItemViewModel overrides = model.CanvasMenu().Single(i => i.Header == "Osnap Overrides");

            Assert.True(overrides.IsEnabled);
            Assert.NotEmpty(overrides.Items);
        }

        [Fact]
        public void AModifierChosenAtAPromptThatAsksNeitherPointNorObjects_IsStillAnsweredAsText()
        {
            MainViewModel model = Open();
            model.StartCommand("POLYGON");

            Assert.False(model.ModifyPoint("_tt"));
            Assert.Contains("Enter number of sides <4>: _tt", model.History);
        }
    }
}
