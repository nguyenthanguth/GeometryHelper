using System;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The shortest and longest arc of a revision cloud kept in the settings file, as AutoCAD 2021 keeps REVCLOUDMINARCLENGTH and
    /// REVCLOUDMAXARCLENGTH in the profile between sessions (L8 probe m/routines, M6). None is kept by default; the two are
    /// written and read back, and a length below nought is refused.
    /// </summary>
    public sealed class RevcloudLengthSettingsTests
    {
        [Fact]
        public void NoArcLengthIsKept_ByDefault()
        {
            var settings = new GlobalSettings();

            Assert.Equal(0.0, settings.RevcloudMinimumArcLength);
            Assert.Equal(0.0, settings.RevcloudMaximumArcLength);
        }

        [Fact]
        public void TheTwoArcLengths_AreWrittenAndReadBack_AndCopiedWithTheSettings()
        {
            var settings = new GlobalSettings { RevcloudMinimumArcLength = 10.0, RevcloudMaximumArcLength = 20.5 };
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gh-rev-" + Guid.NewGuid().ToString("N") + ".json"
            );

            try
            {
                settings.Save(path);
                GlobalSettings loaded = GlobalSettings.Load(path);

                Assert.Equal(10.0, loaded.RevcloudMinimumArcLength);
                Assert.Equal(20.5, loaded.RevcloudMaximumArcLength);
                Assert.Equal(20.5, loaded.Clone().RevcloudMaximumArcLength);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public void AnArcLengthBelowNoughtOrNotANumber_IsRefused()
        {
            var settings = new GlobalSettings();

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.RevcloudMinimumArcLength = -1.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.RevcloudMaximumArcLength = double.NaN);
        }
    }
}
