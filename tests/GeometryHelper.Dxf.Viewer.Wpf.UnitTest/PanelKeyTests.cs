using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Ctrl+1 and Ctrl+2 show Properties and the Drawings list when they are hidden and hide them when they are shown, as
    /// AutoCAD's Ctrl+1 does; the commands typed keep their meaning.
    /// </summary>
    public class PanelKeyTests
    {
        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        [Fact]
        public void CtrlOne_HidesPropertiesWhenShown_AndShowsItWhenHidden()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                int requested = 0;
                model.PropertiesRequested += (s, e) => requested++;
                Assert.True(model.IsPropertiesPanelShown);

                Assert.True(model.RunKey("Ctrl+1"));
                Assert.False(model.IsPropertiesPanelShown);
                Assert.Equal(0, requested);

                Assert.True(model.RunKey("Ctrl+1"));
                Assert.True(model.IsPropertiesPanelShown);
                Assert.Equal(1, requested);

                Assert.True(model.RunKey("Ctrl+1"));
                Assert.False(model.IsPropertiesPanelShown);
            }
        }

        [Fact]
        public void CtrlTwo_HidesTheDrawingsListWhenShown_AndShowsItWhenHidden()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                Assert.True(model.IsFileListShown);

                Assert.True(model.RunKey("Ctrl+2"));
                Assert.False(model.IsFileListShown);
                Assert.True(model.IsPropertiesPanelShown);

                Assert.True(model.RunKey("Ctrl+2"));
                Assert.True(model.IsFileListShown);

                Assert.True(model.RunKey("Ctrl+2"));
                Assert.False(model.IsFileListShown);
            }
        }

        [Fact]
        public void TheTwoKeys_WorkWithNoDrawingOpen_EachLeavingTheOtherPanelAlone()
        {
            var model = new MainViewModel();
            Assert.True(model.IsPropertiesPanelShown);
            Assert.True(model.IsFileListShown);

            Assert.True(model.RunKey("Ctrl+1"));
            Assert.False(model.IsPropertiesPanelShown);
            Assert.True(model.IsFileListShown);

            Assert.True(model.RunKey("Ctrl+2"));
            Assert.False(model.IsFileListShown);
            Assert.False(model.IsPropertiesPanelShown);
        }

        [Fact]
        public void TheCommandsTyped_KeepTheirMeaning_PropertiesOpensAndPropertiesCloseCloses()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);

                model.Execute("PROPERTIES");
                Assert.True(model.IsPropertiesPanelShown);

                model.Execute("PROPERTIESCLOSE");
                Assert.False(model.IsPropertiesPanelShown);
                model.Execute("PROPERTIESCLOSE");
                Assert.False(model.IsPropertiesPanelShown);

                model.Execute("PROPERTIES");
                Assert.True(model.IsPropertiesPanelShown);
                model.Execute("PROPERTIES");
                Assert.True(model.IsPropertiesPanelShown);
            }
        }

        [Fact]
        public void TheDefaultKeys_GiveCtrlOneProperties_AndCtrlTwoDrawings_AndShowInTheShortcutsPage()
        {
            Assert.Equal("PROPERTIES", CadShortcuts.Default.Keys["Ctrl+1"]);
            Assert.Equal("DRAWINGS", CadShortcuts.Default.Keys["Ctrl+2"]);
            Assert.Equal(
                1,
                CadShortcuts.Default.Keys.Count(p =>
                    string.Equals(p.Key, "Ctrl+2", System.StringComparison.OrdinalIgnoreCase)
                )
            );

            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Shortcuts");
            ShortcutRowViewModel drawings = dialog.Shortcuts.Single(r => r.Command == "DRAWINGS");

            Assert.Equal("Ctrl+2", drawings.Keys);
            Assert.Equal("Show or hide the Drawings list", drawings.Description);
            Assert.DoesNotContain(dialog.Shortcuts, r => r.HasConflict);
        }

        [Fact]
        public void AKeyGivenToDrawingsInTheShortcuts_TogglesTheListAsCtrlTwoDoes()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Shortcuts");
            dialog.Shortcuts.Single(r => r.Command == "DRAWINGS").Keys = "Ctrl+Shift+D";
            Assert.False(dialog.HasErrors);
            dialog.OkCommand.Execute(null);

            Assert.True(model.IsFileListShown);
            Assert.True(model.RunKey("Ctrl+Shift+D"));
            Assert.False(model.IsFileListShown);
            Assert.False(model.RunKey("Ctrl+2"));
            Assert.False(model.IsFileListShown);
        }
    }
}
