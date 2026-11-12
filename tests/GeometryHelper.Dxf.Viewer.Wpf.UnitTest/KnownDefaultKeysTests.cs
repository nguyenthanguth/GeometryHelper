using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A settings file keeps its own table of keys, so a key added to the defaults later is merged in once: when it is not
    /// among the default keys the file has already seen, its key is free and nothing is bound to its command yet.
    /// </summary>
    public class KnownDefaultKeysTests
    {
        private const string OldFile =
            "{ \"shortcuts\": { \"aliases\": { \"M\": \"MOVE\" }, \"keys\": { \"Ctrl+1\": \"PROPERTIES\", \"F3\": \"OSNAP\" } } }";

        private static GlobalSettings Load(TestFolder folder, string json) =>
            GlobalSettings.Load(folder.WriteText("settings.json", json));

        [Fact]
        public void AFileSavedBeforeCtrlTwo_GetsIt_AndKeepsTheKeysItClearedOnPurpose()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings settings = Load(folder, OldFile);

                Assert.Equal("DRAWINGS", settings.Keys["Ctrl+2"]);
                Assert.Equal("PROPERTIES", settings.Keys["Ctrl+1"]);
                Assert.Equal("OSNAP", settings.Keys["F3"]);
                Assert.False(settings.Keys.ContainsKey("Ctrl+S"));
                Assert.False(settings.Keys.ContainsKey("F7"));
                Assert.Equal("MOVE", settings.Aliases["M"]);
            }
        }

        [Fact]
        public void ACtrlTwoClearedAfterTheMerge_StaysCleared_WhenTheFileIsSavedAndRead()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings settings = Load(folder, OldFile);
                settings.SetShortcuts(settings.Aliases, settings.Keys.Where(p => p.Key != "Ctrl+2"));
                string path = System.IO.Path.Combine(folder.FullPath, "saved.json");
                settings.Save(path);

                GlobalSettings read = GlobalSettings.Load(path);

                Assert.False(read.Keys.ContainsKey("Ctrl+2"));
                Assert.Equal("PROPERTIES", read.Keys["Ctrl+1"]);
            }
        }

        [Fact]
        public void ACtrlTwoKeptAndSaved_IsNotMergedTwice_AndAFileWithTheListIsTakenAsItIs()
        {
            using (var folder = new TestFolder())
            {
                string path = System.IO.Path.Combine(folder.FullPath, "saved.json");
                Load(folder, OldFile).Save(path);
                GlobalSettings read = GlobalSettings.Load(path);
                Assert.Equal("DRAWINGS", read.Keys["Ctrl+2"]);

                string withList =
                    "{ \"shortcuts\": { \"keys\": { \"Ctrl+1\": \"PROPERTIES\" }, \"knownDefaultKeys\": [\"Ctrl+1\", \"Ctrl+2\"] } }";
                GlobalSettings cleared = Load(folder, withList);

                Assert.False(cleared.Keys.ContainsKey("Ctrl+2"));
            }
        }

        [Fact]
        public void ACtrlTwoTheUserGaveToAnotherCommand_IsNotTakenFromIt()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings settings = Load(
                    folder,
                    "{ \"shortcuts\": { \"keys\": { \"Ctrl+2\": \"MOVE\", \"Ctrl+1\": \"PROPERTIES\" } } }"
                );

                Assert.Equal("MOVE", settings.Keys["Ctrl+2"]);
                Assert.DoesNotContain(settings.Keys, p => p.Value == "DRAWINGS");
            }
        }

        [Fact]
        public void DrawingsGivenAnotherKeyByTheUser_IsNotGivenCtrlTwoAsWell()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings settings = Load(
                    folder,
                    "{ \"shortcuts\": { \"keys\": { \"Ctrl+Shift+D\": \"DRAWINGS\", \"Ctrl+1\": \"PROPERTIES\" } } }"
                );

                Assert.False(settings.Keys.ContainsKey("Ctrl+2"));
                Assert.Equal("DRAWINGS", settings.Keys["Ctrl+Shift+D"]);
            }
        }

        [Fact]
        public void TheSettingsFileWritesTheDefaultKeysItHasSeen()
        {
            string json = new GlobalSettings().ToJson();

            Assert.Contains("\"knownDefaultKeys\": [", json);
            Assert.Contains(
                "\"Ctrl+2\"",
                json.Substring(json.IndexOf("knownDefaultKeys", System.StringComparison.Ordinal))
            );
        }
    }
}
