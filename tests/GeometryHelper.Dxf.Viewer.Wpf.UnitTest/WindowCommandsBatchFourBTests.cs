using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Batch 4 of acad.pgp's commands that are the window's own: QUIT and EXIT, COMMANDLINE and COMMANDLINEHIDE, HIDEPALETTES
    /// and SHOWPALETTES with Ctrl+Shift+H, and -OSNAP, in the words of AutoCAD 2021's console.
    /// </summary>
    public class WindowCommandsBatchFourBTests
    {
        private static MainViewModel Open()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "a.dxf"));
            return model;
        }

        #region QUIT

        [Fact]
        public void Quit_AsksTheWindowToCloseOnceTheCommandHasEnded_TheDrawingsNotClosedByIt()
        {
            MainViewModel model = Open();
            int asked = 0;
            model.QuitRequested += (s, e) => asked++;

            model.Execute("QUIT");
            model.Execute("EXIT");

            Assert.Equal(2, asked);
            Assert.True(model.HasDocument);
            Assert.Equal("QUIT", model.Session.Registry.Find("EXIT").Name);
        }

        [Fact]
        public void Quit_WorksWithNoDrawingOpen_AndSaysSoWhereNoWindowListens()
        {
            var model = new MainViewModel();
            model.Execute("EXIT");
            Assert.Contains("QUIT is done by the window; its x closes it.", model.History);

            int asked = 0;
            model.QuitRequested += (s, e) => asked++;
            model.Execute("QUIT");
            Assert.Equal(1, asked);
        }

        [Fact]
        public void Quit_GoesThroughTheQuestionOfTheWindowClosed_WhereADrawingHasChangesNotSaved()
        {
            MainViewModel model = Open();
            model.Execute("LINE");
            model.Execute("0,0");
            model.Execute("10,0");
            model.Execute(string.Empty);
            Assert.True(model.Document.IsModified);
            int questions = 0;
            bool leave = true;
            model.AskSaveAll = documents =>
            {
                questions++;
                return CadSaveChoice.Cancel;
            };
            model.QuitRequested += (s, e) => leave = model.ConfirmExit();

            model.Execute("QUIT");

            Assert.Equal(1, questions);
            Assert.False(leave);
            Assert.True(model.HasDocument);
        }

        #endregion

        #region COMMANDLINE and the palettes

        [Fact]
        public void CommandLineHide_HidesTheCommandLine_AndCommandLineShowsIt_AsCtrlNineDoes()
        {
            MainViewModel model = Open();
            Assert.True(model.IsCommandLineShown);

            model.Execute("COMMANDLINEHIDE");
            Assert.False(model.IsCommandLineShown);
            model.Execute("COMMANDLINEHIDE");
            Assert.False(model.IsCommandLineShown);

            model.Execute("COMMANDLINE");
            Assert.True(model.IsCommandLineShown);
            model.Execute("COMMANDLINE");
            Assert.True(model.IsCommandLineShown);

            model.Execute("CLI");
            Assert.True(model.IsCommandLineShown);
            Assert.True(model.RunKey("Ctrl+9"));
            Assert.False(model.IsCommandLineShown);
            Assert.Equal("COMMANDLINE", CadShortcuts.Default.Aliases["CLI"]);
        }

        [Fact]
        public void TheCommandLineCommands_NeedNoDrawing()
        {
            var model = new MainViewModel();

            model.Execute("COMMANDLINEHIDE");
            Assert.False(model.IsCommandLineShown);
            model.Execute("COMMANDLINE");
            Assert.True(model.IsCommandLineShown);
            model.Execute("POFF");
            Assert.True(model.ArePalettesHidden);
            model.Execute("PON");
            Assert.False(model.ArePalettesHidden);
        }

        [Fact]
        public void HidePalettes_HidesPropertiesTheDrawingsListAndTheCommandLine_TheRibbonStays_ShowPalettesBringsThemBack()
        {
            MainViewModel model = Open();
            Assert.True(model.IsPropertiesPanelShown);
            Assert.True(model.IsFileListShown);
            Assert.True(model.IsCommandLineShown);
            Assert.True(model.IsRibbonShown);

            model.Execute("HIDEPALETTES");

            Assert.False(model.IsPropertiesPanelShown);
            Assert.False(model.IsFileListShown);
            Assert.False(model.IsCommandLineShown);
            Assert.True(model.IsRibbonShown);
            Assert.True(model.ArePalettesHidden);

            model.Execute("SHOWPALETTES");

            Assert.True(model.IsPropertiesPanelShown);
            Assert.True(model.IsFileListShown);
            Assert.True(model.IsCommandLineShown);
            Assert.False(model.ArePalettesHidden);
        }

        [Fact]
        public void ShowPalettes_BringsBackOnlyWhatWasShown_AndASecondHidePalettesKeepsWhatTheFirstRemembered()
        {
            MainViewModel model = Open();
            model.Execute("PROPERTIESCLOSE");
            model.Execute("HIDEPALETTES");
            model.Execute("HIDEPALETTES");
            Assert.True(model.ArePalettesHidden);

            model.Execute("SHOWPALETTES");

            Assert.False(model.IsPropertiesPanelShown);
            Assert.True(model.IsFileListShown);
            Assert.True(model.IsCommandLineShown);

            // With none hidden by HIDEPALETTES, SHOWPALETTES shows nothing.
            model.Execute("SHOWPALETTES");
            Assert.False(model.IsPropertiesPanelShown);
        }

        [Fact]
        public void CtrlShiftH_HidesThePalettes_AndShowsThemAsTheyWereWhenPressedAgain()
        {
            MainViewModel model = Open();

            Assert.True(model.RunKey("Ctrl+Shift+H"));
            Assert.False(model.IsPropertiesPanelShown);
            Assert.False(model.IsFileListShown);
            Assert.False(model.IsCommandLineShown);

            Assert.True(model.RunKey("Ctrl+Shift+H"));
            Assert.True(model.IsPropertiesPanelShown);
            Assert.True(model.IsFileListShown);
            Assert.True(model.IsCommandLineShown);

            Assert.Equal("HIDEPALETTES", CadShortcuts.Default.Aliases["POFF"]);
            Assert.Equal("SHOWPALETTES", CadShortcuts.Default.Aliases["PON"]);
        }

        #endregion

        #region -OSNAP

        private const CadSnapModes Running =
            CadSnapModes.Endpoint
            | CadSnapModes.Midpoint
            | CadSnapModes.Center
            | CadSnapModes.Intersection
            | CadSnapModes.Perpendicular;

        [Fact]
        public void MinusOsnap_SaysTheModesOnInAutoCadsShortNames_ThenAsksTheListAndReplacesThem()
        {
            MainViewModel model = Open();
            model.SnapModes = Running;
            Assert.Equal(167, (int)model.SnapModes);

            model.Execute("-OSNAP");

            Assert.Contains("Current osnap modes: End,Mid,Cen,Int,Perp", model.History);
            Assert.Equal("Enter list of object snap modes:", model.Prompt);
            model.Execute("END,MID");

            Assert.False(model.IsCommandRunning);
            Assert.Equal(CadSnapModes.Endpoint | CadSnapModes.Midpoint, model.SnapModes);
        }

        [Fact]
        public void MinusOsnap_TakesModesByTheirFirstThreeLettersOrLongerWords_NoneAsNONe()
        {
            MainViewModel model = Open();
            model.SnapModes = Running;

            model.Execute("-OS");
            model.Execute("int,Cen,qua");
            Assert.Equal(52, (int)model.SnapModes);

            model.Execute("-OSNAP");
            Assert.Contains("Current osnap modes: Cen,Quad,Int", model.History);
            model.Execute("ENDpoint,MIDpoint,NODe");
            Assert.Equal(11, (int)model.SnapModes);

            model.Execute("-OSNAP");
            model.Execute("NONe");
            Assert.Equal(CadSnapModes.None, model.SnapModes);

            // With none on there is nothing to say of the current ones.
            int said = model.History.Count(l => l.StartsWith("Current osnap modes", System.StringComparison.Ordinal));
            model.Execute("-OSNAP");
            Assert.Equal(
                said,
                model.History.Count(l => l.StartsWith("Current osnap modes", System.StringComparison.Ordinal))
            );
            model.Execute(string.Empty);
            Assert.False(model.IsCommandRunning);
        }

        [Fact]
        public void MinusOsnap_RefusesAListWithANameThatIsNoSnap_AsksAgain_AndKeepsTheModes()
        {
            MainViewModel model = Open();
            model.SnapModes = Running;

            model.Execute("-OSNAP");
            model.Execute("FOO");
            Assert.Contains("Invalid object snap modes.", model.History);
            Assert.True(model.IsCommandRunning);
            Assert.Equal("Enter list of object snap modes:", model.Prompt);
            model.Execute("END,FOO");
            Assert.Equal(2, model.History.Count(l => l == "Invalid object snap modes."));
            Assert.Equal(Running, model.SnapModes);

            model.Execute(string.Empty);
            Assert.False(model.IsCommandRunning);
            Assert.Equal(Running, model.SnapModes);
        }

        [Fact]
        public void MinusOsnap_LeavesTheOsnapToggleAsItWas_AndHasItsAlias()
        {
            MainViewModel model = Open();
            model.SnapModes = Running;
            model.Execute("OSNAP");
            model.Execute("OFF");
            Assert.False(model.Toggles.First(t => t.Name == "OSNAP").IsOn);

            model.Execute("-OSNAP");
            model.Execute("CEN");

            Assert.Equal(CadSnapModes.Center, model.SnapModes);
            Assert.False(model.Toggles.First(t => t.Name == "OSNAP").IsOn);
            Assert.Equal("-OSNAP", CadShortcuts.Default.Aliases["-OS"]);
            Assert.Equal("DSETTINGS", model.Session.Registry.Find("OS").Name);
        }

        #endregion
    }
}
