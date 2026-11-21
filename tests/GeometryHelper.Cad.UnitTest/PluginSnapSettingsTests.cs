using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>The user's choice of the object snaps plugins add, kept in the settings by keyword as "CTOP=1" and "BOLT=0" (D515).</summary>
    public class PluginSnapSettingsTests
    {
        [Fact]
        public void NoChoiceIsKeptUntilOneIsMadeAndNullSetsAnEmptyList()
        {
            var settings = new GlobalSettings();

            Assert.NotNull(settings.PluginSnaps);
            Assert.Empty(settings.PluginSnaps);

            settings.PluginSnaps = null;

            Assert.NotNull(settings.PluginSnaps);
            Assert.Empty(settings.PluginSnaps);
        }

        [Fact]
        public void ACloneKeepsTheChoicesAndChangingTheCloneLeavesTheOriginalAlone()
        {
            var settings = new GlobalSettings
            {
                PluginSnaps = new List<string> { "CTOP=1", "BOLT=0" },
            };

            GlobalSettings copy = settings.Clone();
            copy.PluginSnaps.Add("NUT=1");
            copy.PluginSnaps[0] = "CTOP=0";

            Assert.Equal(new[] { "CTOP=1", "BOLT=0" }, settings.PluginSnaps);
            Assert.Equal(new[] { "CTOP=0", "BOLT=0", "NUT=1" }, copy.PluginSnaps);
        }

        [Fact]
        public void TheChoicesSurviveSavingAndLoadingInTheOrderTheyWereMade()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gk-plugin-snap-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "settings.json");

            try
            {
                var settings = new GlobalSettings
                {
                    PluginSnaps = new List<string> { "CTOP=1", "BOLT=0" },
                };
                settings.Save(path);

                GlobalSettings loaded = GlobalSettings.Load(path);

                Assert.Equal(new[] { "CTOP=1", "BOLT=0" }, loaded.PluginSnaps);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void ASettingsFileMadeBeforePluginSnapsExistedLoadsWithNoChoices()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gk-plugin-snap-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "settings.json");

            try
            {
                new GlobalSettings().Save(path);
                string without = File.ReadAllText(path).Replace("\"pluginSnaps\"", "\"someOtherList\"");
                File.WriteAllText(path, without);

                GlobalSettings loaded = GlobalSettings.Load(path);

                Assert.NotNull(loaded.PluginSnaps);
                Assert.Empty(loaded.PluginSnaps);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
