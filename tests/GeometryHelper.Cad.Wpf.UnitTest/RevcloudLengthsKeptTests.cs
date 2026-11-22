using System;
using System.IO;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// REVCLOUD's shortest and longest arc kept between sessions, as AutoCAD 2021 keeps them in the profile (L8 probe m/routines,
    /// M6: a run that set 15 left REVCLOUDMINARCLENGTH at 10 and REVCLOUDMAXARCLENGTH at 20 for the next session, and the
    /// approximate length, REVCLOUDAPPROXARCLEN, at nought): set in one window, saved in the settings, and the value of a
    /// window opened later from them.
    /// </summary>
    public sealed class RevcloudLengthsKeptTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private string SettingsFile => Path.Combine(_folder.FullPath, "settings.json");

        private MainViewModel Opened(GlobalSettings settings = null)
        {
            string room = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel { SettingsPath = SettingsFile };

            if (settings != null)
            {
                model.ApplySettings(settings);
            }

            Assert.True(model.Open(room));
            return model;
        }

        [Fact]
        public void TheArcLengthsOfASession_AreKeptInTheSettingsOnceTheCommandEnds()
        {
            MainViewModel model = Opened();

            model.Execute("REVCLOUDMINARCLENGTH");
            model.Execute("10");
            model.Execute("REVCLOUDMAXARCLENGTH");
            model.Execute("20");

            GlobalSettings saved = GlobalSettings.Load(SettingsFile);
            Assert.Equal(10.0, saved.RevcloudMinimumArcLength);
            Assert.Equal(20.0, saved.RevcloudMaximumArcLength);
            Assert.Equal(10.0, model.Settings.RevcloudMinimumArcLength);
        }

        [Fact]
        public void ASessionStartedFromTheKeptLengths_HasThemAsTheVariablesAndTheApproximateLengthIsNotKept()
        {
            var kept = new GlobalSettings { RevcloudMinimumArcLength = 10.0, RevcloudMaximumArcLength = 20.0 };

            MainViewModel model = Opened(kept);

            Assert.Equal(10.0, model.Session.Remembered["revcloud.least"]);
            Assert.Equal(20.0, model.Session.Remembered["revcloud.most"]);
            Assert.False(model.Session.Remembered.ContainsKey("revcloud.approx"));
        }

        [Fact]
        public void WithNoLengthsKept_ASessionStartsWithNoneAndNothingIsWritten()
        {
            MainViewModel model = Opened();

            Assert.False(model.Session.Remembered.ContainsKey("revcloud.least"));
            Assert.False(File.Exists(SettingsFile));
        }
    }
}
