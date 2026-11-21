using System;
using System.IO;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.SecurityKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// L6's wiring in the window (D903, D906, D911): the drawings of the app share one <see cref="CadLoadSecurity"/> built from
    /// Global settings, an answer given in one drawing holds in the others, an Always Load is kept in the settings and their
    /// file, and SETVAR of SECURELOAD in one drawing reads the same in the others and after a restart. Nothing here shows a
    /// window: the tests answer the question themselves through <see cref="CadLoadSecurity.Ask"/>.
    /// </summary>
    public sealed class MainViewModelSecurityTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private string SettingsFile => Path.Combine(_folder.FullPath, "settings.json");

        private string Room(string name) => _folder.Write(name, TestFiles.Room(out _, out _));

        /// <summary>A window's model on a settings file of its own with two drawings open; the first one is given back as <paramref name="first"/>.</summary>
        private MainViewModel TwoDrawings(out CadCommandSession first, GlobalSettings settings = null)
        {
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.ApplySettings(settings ?? new GlobalSettings());
            Assert.True(model.Open(Room("a.dxf")));
            first = model.Session;
            Assert.True(model.Open(Room("b.dxf")));
            Assert.NotSame(first, model.Session);
            return model;
        }

        [Fact]
        public void TwoDrawingsOfTheApp_ShareOneGate_OfSecureLoadOne_AndADrawingOpenedLaterGetsTheSame()
        {
            // D903: the window gives every session the one instance built from Global settings, whose default is
            // SECURELOAD 1 (AutoCAD's initial value), where a session made by a library has its own at 0.
            MainViewModel model = TwoDrawings(out CadCommandSession first);
            CadCommandSession second = model.Session;

            Assert.Same(first.LoadSecurity, second.LoadSecurity);
            Assert.Equal(1, first.LoadSecurity.SecureLoad);

            Assert.True(model.Open(Room("c.dxf")));

            Assert.Same(first.LoadSecurity, model.Session.LoadSecurity);
        }

        [Fact]
        public void TheGate_ReadsTheFourSettings_AtStart_AndAgainWhenSettingsAreApplied()
        {
            string trusted = Path.Combine(_folder.FullPath, "tools");
            string entry = new string('a', 64) + "|" + Path.Combine(_folder.FullPath, "once.lsp");
            var settings = new GlobalSettings
            {
                SecureLoad = 2,
                TrustedPaths = trusted + @"\...",
                LegacyCodeSearch = true,
                AlwaysLoadedFiles = new[] { entry },
            };
            MainViewModel model = TwoDrawings(out CadCommandSession first, settings);

            CadLoadSecurity gate = first.LoadSecurity;
            Assert.Equal(2, gate.SecureLoad);
            Assert.Equal(trusted + @"\...", gate.TrustedPaths);
            Assert.True(gate.LegacyCodeSearch);
            Assert.Equal(new[] { entry }, gate.AlwaysLoaded);

            // Apply of the Global settings window: the same gate, with the new values, in both open drawings.
            model.ApplySettings(
                new GlobalSettings
                {
                    SecureLoad = 0,
                    TrustedPaths = "",
                    LegacyCodeSearch = false,
                }
            );

            Assert.Same(gate, first.LoadSecurity);
            Assert.Same(gate, model.Session.LoadSecurity);
            Assert.Equal(0, gate.SecureLoad);
            Assert.Equal("", gate.TrustedPaths);
            Assert.False(gate.LegacyCodeSearch);
            Assert.Empty(gate.AlwaysLoaded);
        }

        [Fact]
        public void ALoadOnceAnsweredInOneDrawing_HoldsInTheOther_AndIsAskedOnce()
        {
            // The file is in the temporary folder: not the app's folder, and TRUSTEDPATHS is empty, so SECURELOAD 1 asks.
            string file = Code(_folder, "once.lsp");
            MainViewModel model = TwoDrawings(out CadCommandSession first);
            int asked = 0;
            first.LoadSecurity.Ask = path =>
            {
                asked++;
                return CadLoadAnswer.LoadOnce;
            };

            Assert.True(first.LoadSecurity.MayLoad(file));
            Assert.True(model.Session.LoadSecurity.MayLoad(file));
            Assert.True(first.LoadSecurity.MayLoad(file));

            Assert.Equal(1, asked);
        }

        [Fact]
        public void ADoNotLoad_IsAskedAgainInTheOtherDrawing_BecauseNothingWasKept()
        {
            string file = Code(_folder, "no.lsp");
            MainViewModel model = TwoDrawings(out CadCommandSession first);
            int asked = 0;
            first.LoadSecurity.Ask = path =>
            {
                asked++;
                return CadLoadAnswer.DoNotLoad;
            };

            Assert.False(first.LoadSecurity.MayLoad(file));
            Assert.False(model.Session.LoadSecurity.MayLoad(file));

            Assert.Equal(2, asked);
        }

        [Fact]
        public void AnAlwaysLoad_IsKeptInTheSettingsAndTheirFile_WithTheShaOfTheBytesAndTheFullPath()
        {
            string file = Code(_folder, "always.lsp", "(defun c:always () (princ))\n");
            MainViewModel model = TwoDrawings(out CadCommandSession first);
            first.LoadSecurity.Ask = path => CadLoadAnswer.AlwaysLoad;

            Assert.True(first.LoadSecurity.MayLoad(file));

            string entry = Sha256(file) + "|" + Path.GetFullPath(file);
            Assert.Equal(64, entry.IndexOf('|'));
            Assert.Equal(new[] { entry }, model.Settings.AlwaysLoadedFiles);
            Assert.Equal(new[] { entry }, GlobalSettings.Load(SettingsFile).AlwaysLoadedFiles);
        }

        [Fact]
        public void AnAlwaysLoadFromTheFile_LoadsWithNoQuestion_InANewRunOfTheApp_UntilTheFileIsEdited()
        {
            // Run one: Always Load. Run two: a new model reads the file. Then the file's bytes change by one character.
            string file = Code(_folder, "again.lsp", "(princ \"1\")\n");
            TwoDrawings(out CadCommandSession drawing);
            drawing.LoadSecurity.Ask = path => CadLoadAnswer.AlwaysLoad;
            Assert.True(drawing.LoadSecurity.MayLoad(file));

            var restarted = new MainViewModel { SettingsPath = SettingsFile };
            restarted.ApplySettings(GlobalSettings.Load(SettingsFile));
            Assert.True(restarted.Open(Room("restarted.dxf")));
            int asked = 0;
            restarted.Session.LoadSecurity.Ask = path =>
            {
                asked++;
                return CadLoadAnswer.DoNotLoad;
            };

            Assert.True(restarted.Session.LoadSecurity.MayLoad(file));
            Assert.Equal(0, asked);

            File.WriteAllText(file, "(princ \"2\")\n");

            Assert.False(restarted.Session.LoadSecurity.MayLoad(file));
            Assert.Equal(1, asked);
        }

        [Fact]
        public void SetvarSecureLoadInOneDrawing_ReadsTheSameInTheOther_InTheSettings_AndAfterARestart()
        {
            MainViewModel model = TwoDrawings(out CadCommandSession first);
            CadCommandSession second = model.Session;

            model.Execute("SETVAR");
            model.Execute("SECURELOAD");
            model.Execute("2");

            Assert.False(second.IsRunning);
            Assert.Equal(2, second.LoadSecurity.SecureLoad);
            Assert.Equal(2, first.LoadSecurity.SecureLoad);
            Assert.Equal(2, model.Settings.SecureLoad);
            Assert.Equal(2, GlobalSettings.Load(SettingsFile).SecureLoad);

            var restarted = new MainViewModel { SettingsPath = SettingsFile };
            restarted.ApplySettings(GlobalSettings.Load(SettingsFile));
            Assert.True(restarted.Open(Room("restarted.dxf")));
            Assert.Equal(2, restarted.Session.LoadSecurity.SecureLoad);
        }

        [Fact]
        public void SetvarTrustedPathsInOneDrawing_TrustsTheFolderInTheOther_AndIsKeptInTheSettings()
        {
            string tools = Path.Combine(_folder.FullPath, "tools");
            Directory.CreateDirectory(tools);
            string file = Path.Combine(tools, "tool.lsp");
            File.WriteAllText(file, "(princ)\n");
            MainViewModel model = TwoDrawings(out CadCommandSession first);
            first.LoadSecurity.Ask = path => CadLoadAnswer.DoNotLoad;
            Assert.False(model.Session.LoadSecurity.MayLoad(file));

            model.Execute("SETVAR");
            model.Execute("TRUSTEDPATHS");
            model.Execute(tools);

            Assert.True(first.LoadSecurity.MayLoad(file));
            Assert.Equal(tools, model.Settings.TrustedPaths);
            Assert.Equal(tools, GlobalSettings.Load(SettingsFile).TrustedPaths);
        }
    }
}
