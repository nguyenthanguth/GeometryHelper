using System;
using System.IO;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Where DRAWINGS and PROPERTIES are docked and how wide they are: kept in the settings file, saved when the user lets a
    /// panel go, and put back by Reset to defaults of Global settings.
    /// </summary>
    public sealed class PanelLayoutTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void ThePanelsDefaultToDrawingsLeft288_Properties312Right_AndAnExtensionsPanel320()
        {
            var settings = new GlobalSettings();

            Assert.Equal(288.0, settings.FileListWidth);
            Assert.Equal(312.0, settings.PropertiesWidth);
            Assert.Equal(320.0, settings.SidePanelWidth);
            Assert.False(settings.FileListOnRight);
            Assert.False(settings.PropertiesOnLeft);
        }

        [Fact]
        public void ThePanelLayoutWrittenReadsBackTheSame()
        {
            var settings = new GlobalSettings
            {
                FileListWidth = 412.5,
                PropertiesWidth = 250.0,
                SidePanelWidth = 700.0,
                FileListOnRight = true,
                PropertiesOnLeft = true,
            };
            string path = Path.Combine(_folder.FullPath, "settings.json");

            settings.Save(path);
            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(412.5, read.FileListWidth);
            Assert.Equal(250.0, read.PropertiesWidth);
            Assert.Equal(700.0, read.SidePanelWidth);
            Assert.True(read.FileListOnRight);
            Assert.True(read.PropertiesOnLeft);
        }

        [Fact]
        public void APanelLetGo_IsKeptInTheSettingsInUse_AndTheWindowIsToldToSaveThem()
        {
            var model = new MainViewModel();
            int changed = 0;
            model.SettingsChanged += (s, e) => changed++;

            model.KeepPanelLayout(400.0, 500.0, 600.0, true, true);

            Assert.Equal(1, changed);
            Assert.Equal(400.0, model.Settings.FileListWidth);
            Assert.Equal(500.0, model.Settings.PropertiesWidth);
            Assert.Equal(600.0, model.Settings.SidePanelWidth);
            Assert.True(model.Settings.FileListOnRight);
            Assert.True(model.Settings.PropertiesOnLeft);
        }

        [Fact]
        public void ResetToDefaultsApplied_PutsThePanelsBackWhereTheyStarted()
        {
            var model = new MainViewModel();
            model.KeepPanelLayout(400.0, 500.0, 600.0, true, true);
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings(null);

            Assert.True(dialog.Apply());
            Assert.Equal(400.0, model.Settings.FileListWidth);

            dialog.Reset();
            Assert.True(dialog.Apply());

            Assert.Equal(288.0, model.Settings.FileListWidth);
            Assert.Equal(312.0, model.Settings.PropertiesWidth);
            Assert.Equal(320.0, model.Settings.SidePanelWidth);
            Assert.False(model.Settings.FileListOnRight);
            Assert.False(model.Settings.PropertiesOnLeft);
        }
    }
}
