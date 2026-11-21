using System;
using System.IO;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// Which Python the app starts (D713): the Global settings path, <c>PYTHONNET_PYDLL</c>, then PEP 514, each checked on
    /// disk before <c>Initialize</c>, because a start that fails cannot be tried again in the same process (research M2b).
    /// Pythons are made in a folder of the test: a <c>python.exe</c> beside a file with the header of a PE file of the machine
    /// asked for, which the finder reads and never loads. The registry is not read: the registrations are given to the seam,
    /// <c>CadPythonEnvironment.Find(setting, registrations, pythonnetPydll)</c>. The class is in the collection of the host so
    /// that the environment variable it changes is never seen by a test that runs at the same time.
    /// </summary>
    [Collection(PythonHostCollection.Name)]
    public sealed class EnvironmentTests : IDisposable
    {
        private const string NoPython =
            "No Python 3.11 to 3.15 (64-bit) was found. Install one from python.org, or set its python.exe in Global settings > Python.";

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gh-pyenv-" + Guid.NewGuid().ToString("N"));

        public EnvironmentTests()
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

        private string Install(string name, string library, bool x64 = true) =>
            FakeInstall(Path.Combine(_folder, name), library, x64);

        private static CadPythonRegistration Registered(string hive, string tag, string version, string exe) =>
            new CadPythonRegistration
            {
                Hive = hive,
                Tag = tag,
                Version = version,
                SysArchitecture = "64bit",
                InstallPath = exe == null ? null : Path.GetDirectoryName(exe),
                ExecutablePath = exe,
            };

        private static CadPythonEnvironment Find(
            string setting,
            string pydll,
            params CadPythonRegistration[] registered
        ) => CadPythonEnvironment.Find(setting, registered, pydll);

        [Fact]
        public void ASettingThatNamesAPythonExe_IsUsed_WithTheVersionedDllBesideIt()
        {
            string exe = Install("py314", "python314.dll");

            CadPythonEnvironment environment = Find(exe, null);

            Assert.True(environment.IsUsable);
            Assert.Null(environment.Problem);
            Assert.Equal(exe, environment.Executable);
            Assert.Equal(Path.Combine(Path.GetDirectoryName(exe), "python314.dll"), environment.Library);
            Assert.Equal("3.14", environment.Version);
            Assert.Equal("Global settings", environment.Source);
            Assert.Empty(environment.PassedOver);
        }

        [Fact]
        public void ASettingThatNamesTheFolder_IsTheSameAsOneThatNamesTheExe()
        {
            string exe = Install("py312", "python312.dll");

            CadPythonEnvironment environment = Find(Path.GetDirectoryName(exe), null);

            Assert.True(environment.IsUsable);
            Assert.Equal(exe, environment.Executable);
            Assert.Equal("3.12", environment.Version);
        }

        [Fact]
        public void AVenv_TakesTheDllFromItsHome_AndTheVersionFromItsPyvenvCfg()
        {
            string baseExe = Install("base", "python314.dll");
            string venvExe = FakeVenv(Path.Combine(_folder, "venv"), Path.GetDirectoryName(baseExe), "3.14.9");

            CadPythonEnvironment environment = Find(venvExe, null);

            Assert.True(environment.IsUsable);
            Assert.Equal(venvExe, environment.Executable);
            Assert.Equal(Path.Combine(Path.GetDirectoryName(baseExe), "python314.dll"), environment.Library);
            Assert.Equal("3.14.9", environment.Version);
        }

        [Fact]
        public void ASettingThatIsNotThere_IsAProblem_AndNoOtherPythonTakesItsPlace()
        {
            // A setting that does not work is not silently replaced: the registry holds a good 3.14, and it is not used.
            string good = Install("good", "python314.dll");
            string missing = Path.Combine(_folder, "nowhere", "python.exe");

            CadPythonEnvironment environment = Find(missing, null, Registered("HKCU", "3.14", "3.14.4", good));

            Assert.False(environment.IsUsable);
            Assert.Equal("The Python set in Global settings (" + missing + ") was not found.", environment.Problem);
            Assert.Null(environment.Executable);
            Assert.Null(environment.Library);
            // Source says where it looked even when the Python there failed, so the settings page can name it (D728).
            Assert.Equal("Global settings", environment.Source);
        }

        [Theory]
        [InlineData("python310.dll", "10")]
        [InlineData("python316.dll", "16")]
        public void APythonOutsideThreeElevenToFifteen_IsAProblemThatNamesItsVersion(string library, string minor)
        {
            string exe = Install("py" + minor, library);

            CadPythonEnvironment environment = Find(exe, null);

            Assert.False(environment.IsUsable);
            Assert.Equal(
                exe + " is Python 3." + minor + ": GeometryHelper CAD needs 3.11 to 3.15.",
                environment.Problem
            );
        }

        [Theory]
        [InlineData("python311.dll", "3.11")]
        [InlineData("python313.dll", "3.13")]
        [InlineData("python315.dll", "3.15")]
        public void APythonFromElevenToFifteen_IsUsable(string library, string version)
        {
            string exe = Install("py" + version, library);

            CadPythonEnvironment environment = Find(exe, null);

            Assert.True(environment.IsUsable);
            Assert.Equal(version, environment.Version);
        }

        [Fact]
        public void A32BitPython_IsAProblem()
        {
            string exe = Install("py32", "python312.dll", x64: false);

            CadPythonEnvironment environment = Find(exe, null);

            Assert.False(environment.IsUsable);
            Assert.Equal(exe + " is a 32-bit Python: GeometryHelper CAD needs a 64-bit one.", environment.Problem);
        }

        [Fact]
        public void OnlyThePythonThreeDll_IsNotAPython()
        {
            // python3.dll forwards to the real one and pythonnet cannot load it (research M3): it is no reason to go on.
            string folder = Path.Combine(_folder, "stable");
            string exe = FakeInstall(folder, "python3.dll");

            CadPythonEnvironment environment = Find(exe, null);

            Assert.False(environment.IsUsable);
            Assert.Null(environment.Library);
            Assert.False(string.IsNullOrEmpty(environment.Problem));
        }

        [Fact]
        public void PythonnetPydll_IsUsedWhenNoSettingIsGiven_WithThePythonExeBesideIt()
        {
            string exe = Install("env", "python312.dll");
            string dll = Path.Combine(Path.GetDirectoryName(exe), "python312.dll");

            CadPythonEnvironment environment = Find(
                string.Empty,
                dll,
                Registered("HKCU", "3.14", "3.14.4", Install("reg", "python314.dll"))
            );

            Assert.True(environment.IsUsable);
            Assert.Equal("PYTHONNET_PYDLL", environment.Source);
            Assert.Equal(dll, environment.Library);
            Assert.Equal(exe, environment.Executable);
            Assert.Equal("3.12", environment.Version);
        }

        [Fact]
        public void TheSetting_WinsOverPythonnetPydll()
        {
            string exe = Install("set", "python313.dll");
            string other = Path.Combine(Path.GetDirectoryName(Install("env", "python312.dll")), "python312.dll");

            CadPythonEnvironment environment = Find(exe, other);

            Assert.Equal("Global settings", environment.Source);
            Assert.Equal("3.13", environment.Version);
        }

        [Fact]
        public void TheNewestRegisteredPython_Wins_AndHkcuBeatsHklmOnATie()
        {
            // The registrations are given with HKLM first: the finder orders them itself.
            string twelve = Install("reg312", "python312.dll");
            string machine = Install("regm314", "python314.dll");
            string user = Install("regu314", "python314.dll");
            string thirteen = Install("reg313", "python313.dll");

            CadPythonEnvironment environment = Find(
                string.Empty,
                null,
                Registered("HKLM", "3.14", "3.14.1", machine),
                Registered("HKLM", "3.13", "3.13.9", thirteen),
                Registered("HKCU", "3.12", "3.12.3", twelve),
                Registered("HKCU", "3.14", "3.14.4", user)
            );

            Assert.True(environment.IsUsable);
            Assert.Equal("Registry (PEP 514)", environment.Source);
            Assert.Equal(user, environment.Executable);
            Assert.Equal("3.14.4", environment.Version);
            Assert.Equal("Python 3.14.4 (" + user + ")", environment.ToString());
        }

        [Fact]
        public void ARegistrationWithOnlyAnInstallPath_HasItsPythonExeInThatFolder()
        {
            string exe = Install("reg311", "python311.dll");
            var registration = new CadPythonRegistration
            {
                Hive = "HKCU",
                Tag = "3.11",
                Version = "3.11.9",
                SysArchitecture = "64bit",
                InstallPath = Path.GetDirectoryName(exe),
                ExecutablePath = null,
            };

            CadPythonEnvironment environment = Find(string.Empty, null, registration);

            Assert.True(environment.IsUsable);
            Assert.Equal(exe, environment.Executable);
        }

        [Fact]
        public void EachRegistrationLeftOut_IsListedWithItsReason_InTheOrderLookedAt()
        {
            // The registry of the maintainer's machine: 3.10 with no InstallPath, 3.13 registered with its python.exe gone,
            // 3.14 good. Each is left out for its own reason, and 3.14 is used.
            string good = Install("reg314", "python314.dll");
            string gone = Path.Combine(_folder, "Python313", "python.exe");

            CadPythonEnvironment environment = Find(
                string.Empty,
                null,
                Registered("HKCU", "3.10", "3.10.0", null),
                Registered("HKCU", "3.13", "3.13.0", gone),
                Registered("HKCU", "3.14", "3.14.4", good)
            );

            Assert.True(environment.IsUsable);
            Assert.Equal(good, environment.Executable);
            Assert.Contains("3.10: no InstallPath", environment.PassedOver);
            Assert.Contains("3.13: " + gone + " is missing", environment.PassedOver);
        }

        [Fact]
        public void A32BitRegistration_IsLeftOut_AndTheNext64BitOneIsUsed()
        {
            string x86 = Install("reg314-32", "python314.dll", x64: false);
            string x64 = Install("reg313", "python313.dll");
            CadPythonRegistration thirtyTwo = Registered("HKCU", "3.14-32", "3.14.4", x86);
            thirtyTwo.SysArchitecture = "32bit";

            CadPythonEnvironment environment = Find(
                string.Empty,
                null,
                thirtyTwo,
                Registered("HKCU", "3.13", "3.13.9", x64)
            );

            Assert.Equal(x64, environment.Executable);
            string left = Assert.Single(environment.PassedOver);
            Assert.StartsWith("3.14-32:", left, StringComparison.Ordinal);
        }

        [Fact]
        public void NoSettingNoVariableAndNoUsableRegistration_IsTheNoPythonProblem()
        {
            string tooOld = Install("reg310", "python310.dll");

            CadPythonEnvironment environment = Find(string.Empty, null, Registered("HKCU", "3.10", "3.10.0", tooOld));

            Assert.False(environment.IsUsable);
            Assert.Equal(NoPython, environment.Problem);
            Assert.Equal(NoPython, environment.ToString());
            Assert.Null(environment.Source);
            string left = Assert.Single(environment.PassedOver);
            Assert.StartsWith("3.10:", left, StringComparison.Ordinal);
        }

        [Fact]
        public void NothingAtAll_IsTheNoPythonProblem()
        {
            CadPythonEnvironment environment = Find(string.Empty, null);

            Assert.False(environment.IsUsable);
            Assert.Equal(NoPython, environment.Problem);
            Assert.Empty(environment.PassedOver);
        }
    }
}
