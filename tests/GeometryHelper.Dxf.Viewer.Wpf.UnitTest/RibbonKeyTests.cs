using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Ctrl+3 shows or hides the whole ribbon (its tabs and panels) as Ctrl+1 and Ctrl+2 do Properties and the Drawings list;
    /// RIBBON and RIBBONCLOSE typed show and hide it, as AutoCAD's do; a settings file saved before Ctrl+3 gets it once.
    /// </summary>
    public class RibbonKeyTests
    {
        [Fact]
        public void CtrlThree_HidesTheRibbonWhenShown_AndShowsItWhenHidden_LeavingTheOtherPanelsAlone()
        {
            var model = new MainViewModel();
            Assert.True(model.IsRibbonShown);

            Assert.True(model.RunKey("Ctrl+3"));
            Assert.False(model.IsRibbonShown);
            Assert.True(model.IsPropertiesPanelShown);
            Assert.True(model.IsFileListShown);

            Assert.True(model.RunKey("Ctrl+3"));
            Assert.True(model.IsRibbonShown);

            Assert.True(model.RunKey("Ctrl+3"));
            Assert.False(model.IsRibbonShown);
        }

        [Fact]
        public void CtrlThree_WorksWithADrawingOpen_AndWithACommandRunning()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                model.Execute("LINE");
                Assert.True(model.IsCommandRunning);

                Assert.True(model.RunKey("Ctrl+3"));

                Assert.False(model.IsRibbonShown);
                Assert.True(model.IsCommandRunning);
            }
        }

        [Fact]
        public void TheCommandsTyped_RibbonShowsIt_AndRibbonCloseHidesIt()
        {
            var model = new MainViewModel();

            model.Execute("RIBBONCLOSE");
            Assert.False(model.IsRibbonShown);
            model.Execute("RIBBONCLOSE");
            Assert.False(model.IsRibbonShown);

            model.Execute("RIBBON");
            Assert.True(model.IsRibbonShown);
            model.Execute("RIBBON");
            Assert.True(model.IsRibbonShown);
        }

        [Fact]
        public void TheCommandsTyped_WorkWithADrawingOpen()
        {
            var model = new MainViewModel();
            model.NewDrawing();

            model.Execute("RIBBONCLOSE");
            Assert.False(model.IsRibbonShown);
            Assert.False(model.IsCommandRunning);

            model.Execute("RIBBON");
            Assert.True(model.IsRibbonShown);
        }

        [Fact]
        public void TheDefaultKeys_GiveCtrlThreeTheRibbon_AndNoOtherCommandHasThatKey()
        {
            Assert.Equal("RIBBON", CadShortcuts.Default.Keys["Ctrl+3"]);
            Assert.Equal(
                1,
                CadShortcuts.Default.Keys.Count(p =>
                    string.Equals(p.Key, "Ctrl+3", System.StringComparison.OrdinalIgnoreCase)
                )
            );
            Assert.Single(CadShortcuts.Default.Keys, p => p.Value == "RIBBON");
        }

        [Fact]
        public void AFileSavedBeforeCtrlThree_GetsIt_AndAFileWithTheListAndNoCtrlThreeToo()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings old = GlobalSettings.Load(
                    folder.WriteText(
                        "old.json",
                        "{ \"shortcuts\": { \"keys\": { \"Ctrl+1\": \"PROPERTIES\", \"Ctrl+2\": \"DRAWINGS\" } } }"
                    )
                );
                Assert.Equal("RIBBON", old.Keys["Ctrl+3"]);
                Assert.Equal("DRAWINGS", old.Keys["Ctrl+2"]);

                GlobalSettings listed = GlobalSettings.Load(
                    folder.WriteText(
                        "listed.json",
                        "{ \"shortcuts\": { \"keys\": { \"Ctrl+1\": \"PROPERTIES\" }, \"knownDefaultKeys\": [\"Ctrl+1\", \"Ctrl+2\"] } }"
                    )
                );
                Assert.Equal("RIBBON", listed.Keys["Ctrl+3"]);
                Assert.False(listed.Keys.ContainsKey("Ctrl+2"));
            }
        }

        [Fact]
        public void ACtrlThreeTheUserClearedOrGaveAway_IsNotPutBack()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings cleared = GlobalSettings.Load(
                    folder.WriteText(
                        "cleared.json",
                        "{ \"shortcuts\": { \"keys\": { \"Ctrl+1\": \"PROPERTIES\" }, \"knownDefaultKeys\": [\"Ctrl+1\", \"Ctrl+2\", \"Ctrl+3\"] } }"
                    )
                );
                Assert.False(cleared.Keys.ContainsKey("Ctrl+3"));

                GlobalSettings given = GlobalSettings.Load(
                    folder.WriteText("given.json", "{ \"shortcuts\": { \"keys\": { \"Ctrl+3\": \"MOVE\" } } }")
                );
                Assert.Equal("MOVE", given.Keys["Ctrl+3"]);
                Assert.DoesNotContain(given.Keys, p => p.Value == "RIBBON");

                GlobalSettings moved = GlobalSettings.Load(
                    folder.WriteText("moved.json", "{ \"shortcuts\": { \"keys\": { \"Ctrl+Shift+R\": \"RIBBON\" } } }")
                );
                Assert.False(moved.Keys.ContainsKey("Ctrl+3"));
                Assert.Equal("RIBBON", moved.Keys["Ctrl+Shift+R"]);
            }
        }

        [Fact]
        public void TheSettingsFileWritesCtrlThreeAmongTheDefaultKeysItHasSeen()
        {
            string json = new GlobalSettings().ToJson();

            Assert.Contains(
                "\"Ctrl+3\"",
                json.Substring(json.IndexOf("knownDefaultKeys", System.StringComparison.Ordinal))
            );
        }
    }
}
