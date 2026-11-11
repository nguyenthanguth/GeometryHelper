using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// SNAP, GRID and the variables of the snap and the grid typed, in AutoCAD 2021's words as accoreconsole gives them:
    /// SETVAR of SNAPUNIT, GRIDUNIT, POLARDIST, SNAPTYPE, GRIDSTYLE, GRIDDISPLAY, SNAPMODE and LWDISPLAY with AutoCAD's
    /// refusals, SNAP's Aspect, Legacy, Style and Type, and GRID's spacing, Snap, Major, aDaptive, Limits, Follow and Aspect.
    /// </summary>
    public sealed class SnapGridCommandTests : IDisposable
    {
        private static readonly double Root3 = Math.Sqrt(3.0);

        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>The room with a snap of 10 by 10, a grid of 4 by 6, snap and grid off, GRIDDISPLAY 3.</summary>
        private MainViewModel Room(Action<DxfViewportTableRecord> set = null)
        {
            DxfDatabase db = TestFiles.Room(out _, out _);
            DxfViewportTableRecord active = db.ViewportTable["*Active"];
            active.SnapIncrements = new GeoVector2(10, 10);
            active.GridIncrements = new GeoVector2(4, 6);
            active.SnapEnabled = false;
            active.GridEnabled = false;
            active.GridDisplay = 3;
            set?.Invoke(active);
            var model = new MainViewModel();
            Assert.True(model.Open(_folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", db)));
            return model;
        }

        private static DxfViewportTableRecord Active(MainViewModel model) =>
            model.Document.Database.ViewportTable["*Active"];

        private static bool On(MainViewModel model, string name) => model.Toggles.Single(t => t.Name == name).IsOn;

        private static void Near(double x, double y, GeoVector2 actual)
        {
            Assert.Equal(x, actual.X, 9);
            Assert.Equal(y, actual.Y, 9);
        }

        [Fact]
        public void SetvarSnapunit_RefusesNoughtOrBelowAndEnds_AndTextIsAskedAgain()
        {
            MainViewModel model = Room();

            model.Execute("SETVAR SNAPUNIT");
            Assert.Equal("Enter new value for SNAPUNIT <10.0000,10.0000>:", model.Prompt);
            model.Execute("0,5");
            Assert.Equal("Cannot set SNAPUNIT to that value.", model.History.Last());
            Assert.False(model.IsCommandRunning);

            model.Execute("SNAPUNIT");
            model.Execute("-1,1");
            Assert.Equal("Cannot set SNAPUNIT to that value.", model.History.Last());

            model.Execute("SNAPUNIT");
            model.Execute("abc");
            Assert.Equal("Invalid 2D point.", model.History.Last());
            Assert.Equal("Enter new value for SNAPUNIT <10.0000,10.0000>:", model.Prompt);
            model.Execute("3,4");

            Assert.Equal(3.0, model.Snap.GridX);
            Assert.Equal(4.0, model.Snap.GridY);
            Near(3, 4, Active(model).SnapIncrements);
        }

        [Fact]
        public void SetvarSnapunit_WithAnIsometricSnap_ReadsTheYDoubled_AndKeepsWhatIsTyped()
        {
            MainViewModel model = Room();
            model.Execute("SNAPSTYL 1");

            model.Execute("SNAPUNIT");
            Assert.Equal("Enter new value for SNAPUNIT <10.0000,20.0000>:", model.Prompt);
            model.Execute("4,6");
            Near(4, 6, Active(model).SnapIncrements);

            model.Execute("SNAPUNIT");
            Assert.Equal("Enter new value for SNAPUNIT <4.0000,12.0000>:", model.Prompt);
            model.Enter();
        }

        [Fact]
        public void SetvarGridunit_TakesNoughtAndDropsTheSign()
        {
            MainViewModel model = Room();

            model.Execute("GRIDUNIT");
            Assert.Equal("Enter new value for GRIDUNIT <4.0000,6.0000>:", model.Prompt);
            model.Execute("-2,3");
            Near(2, 3, Active(model).GridIncrements);

            model.Execute("GRIDUNIT 0,5");
            Near(0, 5, Active(model).GridIncrements);
            Assert.Equal(10.0, model.GridSpacingX);
            Assert.Equal(5.0, model.GridSpacingY);
        }

        [Fact]
        public void SetvarPolardist_TakesADistanceBelowNought_AndRefusesText()
        {
            MainViewModel model = Room();

            model.Execute("POLARDIST");
            Assert.Equal("Enter new value for POLARDIST <0.0000>:", model.Prompt);
            model.Execute("abc");
            Assert.Equal("Requires numeric distance or two points.", model.History.Last());
            Assert.Equal("Enter new value for POLARDIST <0.0000>:", model.Prompt);
            model.Execute("-1");

            Assert.Equal(-1.0, model.Settings.PolarDistance);
            Assert.Equal(-1.0, model.Snap.PolarDistance);
            model.Execute("POLARDIST");
            Assert.Equal("Enter new value for POLARDIST <-1.0000>:", model.Prompt);
            model.Execute("0");
        }

        [Fact]
        public void SetvarSnaptypeGridstyleAndGriddisplay_RefuseAsAutoCadDoes()
        {
            MainViewModel model = Room();

            model.Execute("SNAPTYPE");
            model.Execute("2");
            Assert.Equal("Requires 0 or 1 only.", model.History.Last());
            model.Execute("abc");
            Assert.Equal("Requires 0 or 1 only.", model.History.Last());
            model.Execute("1");
            Assert.True(model.Settings.PolarSnap);
            Assert.True(model.Snap.PolarSnap);

            model.Execute("GRIDSTYLE");
            model.Execute("8");
            Assert.Equal("Requires an integer between 0 and 7.", model.History.Last());
            model.Execute("abc");
            Assert.Equal("Requires an integer between 0 and 7.", model.History.Last());
            model.Execute("5");
            Assert.Equal(5, model.Settings.GridStyle);
            Assert.Equal(5, model.GridStyle);

            model.Execute("GRIDDISPLAY");
            model.Execute("2.5");
            Assert.Equal("Requires an integer between 0 and 15.", model.History.Last());
            model.Execute("6");
            Assert.Equal(6, Active(model).GridDisplay);
            Assert.Equal(6, model.GridDisplay);
        }

        [Fact]
        public void SetvarSnapmode_TakesTwo_AsAutoCadDoes_AndReadsItBackWhileSnapStaysOn()
        {
            MainViewModel model = Room();

            model.Execute("SNAPMODE");
            model.Execute("3");
            Assert.Equal("Requires an integer between 0 and 2.", model.History.Last());
            model.Execute("2");
            Assert.True(On(model, "SNAP"));

            model.Execute("SNAPMODE");
            Assert.Equal("Enter new value for SNAPMODE <2>:", model.Prompt);
            model.Enter();

            // SNAP switched by the status bar reads 0 and 1 again.
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = true;
            model.Execute("SNAPMODE");
            Assert.Equal("Enter new value for SNAPMODE <1>:", model.Prompt);
            model.Enter();
        }

        [Fact]
        public void Lwdisplay_IsShownOffOrOn_AndTakesItsKeywords()
        {
            MainViewModel model = Room();
            model.Execute("LWDISPLAY 0");

            model.Execute("LWDISPLAY");
            Assert.Equal("Enter new value for LWDISPLAY <OFF>:", model.Prompt);
            model.Execute("on");
            Assert.True(model.ShowLineWeights);

            model.Execute("LWDISPLAY");
            Assert.Equal("Enter new value for LWDISPLAY <ON>:", model.Prompt);
            model.Execute("abc");
            Assert.Equal("Requires 0 or 1 only, or option keyword.", model.History.Last());
            model.Execute("2");
            Assert.Equal("Requires 0 or 1 only, or option keyword.", model.History.Last());
            model.Execute("OFF");
            Assert.False(model.ShowLineWeights);
        }

        [Fact]
        public void Snap_AsksAutoCadsOptions_RefusesNoughtAndText_AndAnySpacingTurnsItOn()
        {
            MainViewModel model = Room();

            model.Execute("SNAP");
            Assert.Equal("Specify snap spacing or [ON/OFF/Aspect/Legacy/Style/Type] <10.0000>:", model.Prompt);
            model.Execute("0");
            Assert.Equal("Value must be positive and nonzero.", model.History.Last());
            model.Execute("abc");
            Assert.Equal("Requires numeric distance, two points, or option keyword.", model.History.Last());
            model.Enter();
            Assert.True(On(model, "SNAP"));

            model.Execute("SNAP OFF");
            Assert.False(On(model, "SNAP"));

            model.Execute("SNAP A");
            Assert.Equal("Specify horizontal spacing <10.0000>:", model.Prompt);
            model.Execute("-5");
            Assert.Equal("Value must be positive and nonzero.", model.History.Last());
            model.Execute("3");
            Assert.Equal("Specify vertical spacing <10.0000>:", model.Prompt);
            model.Execute("4");
            Near(3, 4, Active(model).SnapIncrements);
            Assert.True(On(model, "SNAP"));

            // Spacings that differ are offered as A, which Enter takes as Aspect.
            model.Execute("SNAP");
            Assert.Equal("Specify snap spacing or [ON/OFF/Aspect/Legacy/Style/Type] <A>:", model.Prompt);
            model.Enter();
            Assert.Equal("Specify horizontal spacing <3.0000>:", model.Prompt);
            model.Enter();
            model.Enter();
            Assert.False(model.IsCommandRunning);
        }

        [Fact]
        public void SnapLegacyAndType_AskAsAutoCad_TypePolarSettingPolardist()
        {
            MainViewModel model = Room();

            model.Execute("SNAP L");
            Assert.Equal("Maintain legacy behavior of always snapping to the grid? [Yes/No] <No>:", model.Prompt);
            model.Execute("Y");
            model.Execute("SNAP L");
            Assert.Equal("Maintain legacy behavior of always snapping to the grid? [Yes/No] <Yes>:", model.Prompt);
            model.Execute("N");

            model.Execute("SNAP T");
            Assert.Equal("Enter snap type [Polar/Grid] <Grid>:", model.Prompt);
            model.Execute("P");
            Assert.Equal("Enter polar snap tracking distance <0.0000>:", model.Prompt);
            model.Execute("7");
            Assert.True(model.Settings.PolarSnap);
            Assert.Equal(7.0, model.Settings.PolarDistance);
            Assert.Equal(7.0, model.Snap.PolarDistance);

            model.Execute("SNAP T");
            Assert.Equal("Enter snap type [Polar/Grid] <Polar>:", model.Prompt);
            model.Execute("G");
            Assert.False(model.Settings.PolarSnap);
            Assert.True(On(model, "SNAP"));
        }

        [Fact]
        public void SnapStyle_TurnsTheSnapAndTheGridOverAsAutoCadDoes()
        {
            MainViewModel model = Room();

            model.Execute("SNAP S");
            Assert.Equal("Enter snap grid style [Standard/Isometric] <S>:", model.Prompt);
            model.Execute("I");
            Assert.Equal("Specify vertical spacing <10.0000>:", model.Prompt);
            model.Execute("-3");
            Assert.Equal("Value must be positive and nonzero.", model.History.Last());
            model.Execute("5");

            // A snap of 5 vertical kept as 8.6603 by 2.5, the grid of 4 by 6 as 6 times the root of 3 by 3.
            Assert.True(Active(model).IsometricSnapEnabled);
            Near(5 * Root3, 2.5, Active(model).SnapIncrements);
            Near(6 * Root3, 3, Active(model).GridIncrements);

            model.Execute("SNAP");
            Assert.Equal("Specify snap spacing or [ON/OFF/Legacy/Style/Type] <5.0000>:", model.Prompt);
            model.Execute("S");
            Assert.Equal("Enter snap grid style [Standard/Isometric] <I>:", model.Prompt);
            model.Execute("S");
            Assert.Equal("Specify snap spacing or [Aspect] <5.0000>:", model.Prompt);
            model.Execute("4");

            Assert.False(Active(model).IsometricSnapEnabled);
            Near(4, 4, Active(model).SnapIncrements);
            Near(6, 6, Active(model).GridIncrements);
            Assert.True(On(model, "SNAP"));
        }

        [Fact]
        public void Grid_TakesASpacingOrAMultipleOfTheSnap_AndRefusesAsAutoCadDoes()
        {
            MainViewModel model = Room();

            model.Execute("GRID");
            Assert.Equal(
                "Specify grid spacing(X) or [ON/OFF/Snap/Major/aDaptive/Limits/Follow/Aspect] <A>:",
                model.Prompt
            );
            model.Execute("-1");
            Assert.Equal("Value must be positive.", model.History.Last());
            model.Execute("abc");
            Assert.Equal("Requires a distance, numberX, or option keyword.", model.History.Last());
            model.Execute("2");
            Near(2, 2, Active(model).GridIncrements);
            Assert.True(model.GridOn);

            model.Execute("GRID");
            Assert.Equal(
                "Specify grid spacing(X) or [ON/OFF/Snap/Major/aDaptive/Limits/Follow/Aspect] <2.0000>:",
                model.Prompt
            );
            model.Execute("2x");
            Near(20, 20, Active(model).GridIncrements);

            model.Execute("GRID S");
            Near(0, 0, Active(model).GridIncrements);

            model.Execute("GRID A");
            Assert.Equal("Specify the horizontal spacing(X) <0.0000>:", model.Prompt);
            model.Execute("2x");
            Assert.Equal("Specify the vertical spacing(Y) <0.0000>:", model.Prompt);
            model.Execute("3");
            Near(20, 3, Active(model).GridIncrements);

            model.Execute("GRID OFF");
            Assert.False(model.GridOn);
        }

        [Fact]
        public void GridMajorAdaptiveLimitsAndFollow_SetGridmajorAndTheBitsOfGriddisplay()
        {
            MainViewModel model = Room();

            model.Execute("GRID M");
            Assert.Equal("Enter the number of grid divisions per major grid line <5>:", model.Prompt);
            model.Execute("0");
            Assert.Equal("Requires an integer between 1 and 100.", model.History.Last());
            model.Execute("4");
            Assert.Equal(4, model.GridMajor);

            model.Execute("GRID D");
            Assert.Equal("Turn adaptive behavior on [Yes/No] <Yes>:", model.Prompt);
            model.Execute("N");
            Assert.Equal(1, model.GridDisplay);

            model.Execute("GRID D Y");
            Assert.Equal("Allow subdivision below grid spacing [Yes/No] <No>:", model.Prompt);
            model.Execute("Y");
            Assert.Equal(7, model.GridDisplay);

            model.Execute("GRID L");
            Assert.Equal("Display grid beyond Limits [Yes/No] <Yes>:", model.Prompt);
            model.Execute("N");
            Assert.Equal(6, model.GridDisplay);

            model.Execute("GRID F");
            Assert.Equal("Follow dynamic UCS [Yes/No] <No>:", model.Prompt);
            model.Execute("Y");
            Assert.Equal(14, model.GridDisplay);
            Assert.Equal(14, Active(model).GridDisplay);
        }

        [Fact]
        public void Grid_WithAnIsometricSnap_OffersNoAspect_AndKeepsTheVerticalSpacing()
        {
            MainViewModel model = Room();
            model.Execute("SNAPSTYL 1");

            model.Execute("GRID");
            Assert.Equal(
                "Specify grid spacing(X) or [ON/OFF/Snap/Major/aDaptive/Limits/Follow] <12.0000>:",
                model.Prompt
            );
            model.Execute("A");
            Assert.Equal("Requires a distance, numberX, or option keyword.", model.History.Last());
            model.Execute("1");
            Near(Root3, 0.5, Active(model).GridIncrements);
        }
    }
}
