using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A settings file keeps its own table of aliases, so an alias added to the defaults later (1 LAYISO, 2 LAYERP, 22 LAYON,
    /// 3 LAYOFF, from AutoCAD's acad.pgp) is merged in once: when it is not among the default aliases the file has already
    /// seen and the file has no alias of that name.
    /// </summary>
    public class KnownDefaultAliasesTests
    {
        private static readonly string[] Digits = { "1", "2", "22", "3" };

        private static GlobalSettings Load(TestFolder folder, string json) =>
            GlobalSettings.Load(folder.WriteText("settings.json", json));

        [Fact]
        public void AFileSavedBeforeTheDigitAliases_GetsThem_AndKeepsTheAliasesItHas()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings settings = Load(
                    folder,
                    "{ \"shortcuts\": { \"aliases\": { \"M\": \"MOVE\" }, \"keys\": { \"Ctrl+1\": \"PROPERTIES\" } } }"
                );

                Assert.Equal("MOVE", settings.Aliases["M"]);
                Assert.Equal("LAYISO", settings.Aliases["1"]);
                Assert.Equal("LAYERP", settings.Aliases["2"]);
                Assert.Equal("LAYON", settings.Aliases["22"]);
                Assert.Equal("LAYOFF", settings.Aliases["3"]);
                Assert.Equal(5, settings.Aliases.Count);
            }
        }

        [Fact]
        public void AnAliasOfTheSameNameTheUserMadeHimself_IsNeverOverwritten()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings settings = Load(
                    folder,
                    "{ \"shortcuts\": { \"aliases\": { \"2\": \"MOVE\", \"M\": \"MOVE\" } } }"
                );

                Assert.Equal("MOVE", settings.Aliases["2"]);
                Assert.Equal("LAYISO", settings.Aliases["1"]);
            }
        }

        [Fact]
        public void ADigitAliasClearedAfterTheMerge_StaysCleared_WhenTheFileIsSavedAndRead()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings settings = Load(folder, "{ \"shortcuts\": { \"aliases\": { \"M\": \"MOVE\" } } }");
                settings.SetShortcuts(settings.Aliases.Where(p => p.Key != "2"), settings.Keys);
                string path = System.IO.Path.Combine(folder.FullPath, "saved.json");
                settings.Save(path);

                GlobalSettings read = GlobalSettings.Load(path);

                Assert.False(read.Aliases.ContainsKey("2"));
                Assert.Equal("LAYISO", read.Aliases["1"]);
                Assert.Equal("MOVE", read.Aliases["M"]);
            }
        }

        [Fact]
        public void AFileWithTheListOfAliasesItHasSeen_IsTakenAsItIs()
        {
            using (var folder = new TestFolder())
            {
                GlobalSettings settings = Load(
                    folder,
                    "{ \"shortcuts\": { \"aliases\": { \"M\": \"MOVE\" }, \"knownDefaultAliases\": [\"1\", \"2\", \"22\"] } }"
                );

                Assert.False(settings.Aliases.ContainsKey("1"));
                Assert.False(settings.Aliases.ContainsKey("2"));
                Assert.False(settings.Aliases.ContainsKey("22"));
                Assert.Equal("LAYOFF", settings.Aliases["3"]);
            }
        }

        [Fact]
        public void TheSettingsFileWritesTheDefaultAliasesItHasSeen()
        {
            string json = new GlobalSettings().ToJson();

            Assert.Contains("\"knownDefaultAliases\": [", json);
            string list = json.Substring(json.IndexOf("knownDefaultAliases", System.StringComparison.Ordinal));
            Assert.All(Digits, d => Assert.Contains("\"" + d + "\"", list));
        }
    }
}
