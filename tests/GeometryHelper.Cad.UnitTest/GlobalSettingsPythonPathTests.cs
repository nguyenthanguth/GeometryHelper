using System;
using System.IO;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <see cref="GlobalSettings.PythonPath"/> (P3 D721): the python.exe, or its folder, that GeometryHelper CAD starts
    /// Python from; empty for the automatic choice, never null, and kept in the settings file.
    /// </summary>
    public sealed class GlobalSettingsPythonPathTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gh-pypath-" + Guid.NewGuid().ToString("N"));

        public GlobalSettingsPythonPathTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        [Fact]
        public void ThePythonPath_IsEmptyByDefault()
        {
            Assert.Equal(string.Empty, new GlobalSettings().PythonPath);
        }

        [Fact]
        public void ANullPythonPath_IsEmpty()
        {
            var settings = new GlobalSettings { PythonPath = @"D:\python\python.exe" };

            settings.PythonPath = null;

            Assert.Equal(string.Empty, settings.PythonPath);
        }

        [Fact]
        public void ThePythonPath_IsKeptInTheSettingsFile_AndCopiedByClone()
        {
            string path = @"D:\tools\python 3.14\python.exe";
            var settings = new GlobalSettings { PythonPath = path };
            string file = Path.Combine(_folder, "settings.json");

            settings.Save(file);
            GlobalSettings read = GlobalSettings.Load(file);

            Assert.Equal(path, read.PythonPath);
            Assert.Equal(path, read.Clone().PythonPath);
            Assert.Contains("pythonPath", File.ReadAllText(file));
        }

        [Fact]
        public void AnEmptyPythonPath_IsSavedAndReadBackEmpty()
        {
            string file = Path.Combine(_folder, "settings.json");

            new GlobalSettings().Save(file);

            Assert.Equal(string.Empty, GlobalSettings.Load(file).PythonPath);
        }
    }
}
