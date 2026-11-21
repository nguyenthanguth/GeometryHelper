using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Python;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Python section of Global settings, in Folder preference (D720, D721, <c>design/PySettingsPython.png</c>): its path
    /// box is <c>GlobalSettings.PythonPath</c>, and what it found, where it looked and what it passed over are what
    /// <see cref="CadPythonEnvironment.Find(string)"/> gives for the text in the box, read again as the text changes. The
    /// installs are folders made in a temporary folder, with a <c>python3NN.dll</c> that is no Python but has a PE header of the
    /// machine asked for, which is all the finder reads; no engine starts.
    /// </summary>
    public sealed class PythonSettingsTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private string SettingsFile => Path.Combine(_folder.FullPath, "settings.json");

        private GlobalSettingsViewModel Dialog(string pythonPath = "")
        {
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.ApplySettings(new GlobalSettings { PythonPath = pythonPath });
            return model.CreateGlobalSettings("Folder preference");
        }

        /// <summary>A folder with a python.exe and the DLL named <paramref name="library"/>, of the machine asked for; gives the folder.</summary>
        private string Install(string name, string library, bool x64 = true)
        {
            string folder = Path.Combine(_folder.FullPath, name);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "python.exe"), new byte[] { 0x4D, 0x5A });
            var header = new byte[0x100];
            header[0] = 0x4D;
            header[1] = 0x5A;
            header[0x3C] = 0x80;
            header[0x80] = 0x50;
            header[0x81] = 0x45;
            header[0x84] = x64 ? (byte)0x64 : (byte)0x4C;
            header[0x85] = x64 ? (byte)0x86 : (byte)0x01;
            File.WriteAllBytes(Path.Combine(folder, library), header);
            return folder;
        }

        [Fact]
        public void ThePathBox_ReadsTheSettingsPythonPath_AndWritingItIsAppliedAndSavedWithTheOthers()
        {
            string first = Install("first", "python313.dll");
            string second = Install("second", "python312.dll");
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.ApplySettings(new GlobalSettings { PythonPath = first });
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Folder preference");
            Assert.Equal(first, dialog.PythonPath);

            dialog.PythonPath = second;
            Assert.Equal(first, model.Settings.PythonPath);

            Assert.True(dialog.Apply());
            Assert.Equal(second, model.Settings.PythonPath);

            Assert.True(dialog.Ok());
            Assert.Equal(second, GlobalSettings.Load(SettingsFile).PythonPath);
        }

        [Fact]
        public void WithNothingSet_ThePathBoxIsEmpty_AndAutomaticIsWhatIsLookedFor()
        {
            GlobalSettingsViewModel dialog = Dialog();

            CadPythonEnvironment automatic = CadPythonEnvironment.Find("");
            Assert.Equal("", dialog.PythonPath);
            Assert.Equal(automatic.IsUsable, dialog.IsPythonUsable);
            Assert.Equal(automatic.IsUsable ? automatic.ToString() : automatic.Problem, dialog.PythonFound);
            Assert.Equal(automatic.Source ?? "Automatic: nothing usable", dialog.PythonSource);
            Assert.Equal(automatic.PassedOver, dialog.PythonPassedOver);
            Assert.Equal(automatic.PassedOver.Count > 0, dialog.HasPythonPassedOver);
        }

        [Fact]
        public void AFolderTyped_IsTheInstallInIt_FoundAsGlobalSettings_WithItsVersionAndProgram()
        {
            string folder = Install("py313", "python313.dll");
            GlobalSettingsViewModel dialog = Dialog();

            dialog.PythonPath = folder;

            Assert.True(dialog.IsPythonUsable);
            Assert.Equal("Python 3.13 (" + Path.Combine(folder, "python.exe") + ")", dialog.PythonFound);
            Assert.Equal("Global settings", dialog.PythonSource);
            Assert.Empty(dialog.PythonPassedOver);
            Assert.False(dialog.HasPythonPassedOver);
        }

        [Fact]
        public void ThePythonExeTyped_IsFoundAsTheFolderIs()
        {
            string exe = Path.Combine(Install("py312", "python312.dll"), "python.exe");
            GlobalSettingsViewModel dialog = Dialog();

            dialog.PythonPath = exe;

            Assert.True(dialog.IsPythonUsable);
            Assert.Equal("Python 3.12 (" + exe + ")", dialog.PythonFound);
        }

        [Fact]
        public void APathThatIsNotThere_ShowsTheFindersTextWordForWord_AndSaysWhereItLooked()
        {
            string path = Path.Combine(_folder.FullPath, "nowhere", "python.exe");
            GlobalSettingsViewModel dialog = Dialog();

            dialog.PythonPath = path;

            Assert.False(dialog.IsPythonUsable);
            Assert.Equal("The Python set in Global settings (" + path + ") was not found.", dialog.PythonFound);
            Assert.Equal("Global settings", dialog.PythonSource);
        }

        [Fact]
        public void APythonOlderThanTheLeast_ShowsThatItIs310_AndThat311To315IsNeeded()
        {
            string folder = Install("py310", "python310.dll");
            GlobalSettingsViewModel dialog = Dialog();

            dialog.PythonPath = folder;

            Assert.False(dialog.IsPythonUsable);
            Assert.Equal(
                Path.Combine(folder, "python.exe") + " is Python 3.10: GeometryHelper CAD needs 3.11 to 3.15.",
                dialog.PythonFound
            );
        }

        [Fact]
        public void A32BitPython_ShowsThatA64BitOneIsNeeded()
        {
            string folder = Install("py32", "python313.dll", x64: false);
            GlobalSettingsViewModel dialog = Dialog();

            dialog.PythonPath = folder;

            Assert.False(dialog.IsPythonUsable);
            Assert.Equal(
                Path.Combine(folder, "python.exe") + " is a 32-bit Python: GeometryHelper CAD needs a 64-bit one.",
                dialog.PythonFound
            );
        }

        [Fact]
        public void TheFoundSourceAndPassedOverLines_AreReadAgainEachTimeTheTextChanges()
        {
            GlobalSettingsViewModel dialog = Dialog();
            var changed = new List<string>();
            dialog.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            dialog.PythonPath = Install("py313", "python313.dll");

            Assert.Contains(nameof(GlobalSettingsViewModel.PythonPath), changed);
            Assert.Contains(nameof(GlobalSettingsViewModel.PythonFound), changed);
            Assert.Contains(nameof(GlobalSettingsViewModel.PythonSource), changed);
            Assert.Contains(nameof(GlobalSettingsViewModel.PythonPassedOver), changed);

            string before = dialog.PythonFound;
            dialog.PythonPath = Install("py312", "python312.dll");

            Assert.NotEqual(before, dialog.PythonFound);
            Assert.StartsWith("Python 3.12 (", dialog.PythonFound, StringComparison.Ordinal);
        }

        [Fact]
        public void Browse_PutsTheFileChosenInTheBox_AndChoosingNothingLeavesTheBoxAsItWas()
        {
            string exe = Path.Combine(Install("py313", "python313.dll"), "python.exe");
            GlobalSettingsViewModel dialog = Dialog();

            dialog.BrowsePythonFile = () => null;
            dialog.BrowsePythonCommand.Execute(null);
            Assert.Equal("", dialog.PythonPath);

            dialog.BrowsePythonFile = () => exe;
            dialog.BrowsePythonCommand.Execute(null);

            Assert.Equal(exe, dialog.PythonPath);
            Assert.StartsWith("Python 3.13 (", dialog.PythonFound, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSectionIsShownOnlyWhereTheAppUsesPython()
        {
            Assert.False(Dialog().IsPythonUsed);

            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.ApplySettings(
                new GlobalSettings { PythonPath = Path.Combine(_folder.FullPath, "nowhere", "python.exe") }
            );
            model.UsePython(System.Windows.Threading.Dispatcher.CurrentDispatcher);

            Assert.True(model.CreateGlobalSettings("Folder preference").IsPythonUsed);
        }
    }
}
