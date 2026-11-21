using System;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using GeometryHelper.Cad.Python;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.SecurityKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The gate of Python in the window (D917): the host the window makes refuses a <c>.py</c> that the shared
    /// <see cref="CadLoadSecurity"/> does not allow, with <c>File load canceled: &lt;full path&gt;</c>. The Python these tests
    /// name is a folder that does not exist, so no engine starts (Python starts once per process, and the one test class that
    /// runs it is <see cref="CadPythonWindowOwnerRunTests"/>); what is checked is the delegate the host will call, which is
    /// what PYRUN, PYLOAD and the palette's Run file and Load call before they read a file. The host is the model's private
    /// <c>_pythonHost</c>, read by reflection because nothing outside the model holds it.
    /// </summary>
    public sealed class MainViewModelPythonGateTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private string Missing(string name) => Path.Combine(_folder.FullPath, name, "python.exe");

        private static CadPythonHost HostOf(MainViewModel model) =>
            (CadPythonHost)
                typeof(MainViewModel)
                    .GetField("_pythonHost", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(model);

        /// <summary>The gate of a host: the delegate PYRUN and PYLOAD call with a file's full path, null for allowed, else the refusal.</summary>
        private static Func<string, string> Gate(CadPythonHost host)
        {
            Func<string, string> gate = host.LoadCheck;
            Assert.NotNull(gate);
            return gate;
        }

        private static Func<string, string> Gate(MainViewModel model) => Gate(HostOf(model));

        /// <summary>A window's model with one drawing open and Python used, on settings that make a gate of <paramref name="secureLoad"/>.</summary>
        private MainViewModel Using(int secureLoad, string trustedPaths = "", string python = "nowhere")
        {
            var model = new MainViewModel();
            model.ApplySettings(
                new GlobalSettings
                {
                    PythonPath = Missing(python),
                    SecureLoad = secureLoad,
                    TrustedPaths = trustedPaths,
                }
            );
            Assert.True(model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            model.UsePython(Dispatcher.CurrentDispatcher);
            return model;
        }

        private string Script(string name = "circles.py") => Code(_folder, name, "print('hi')\n");

        [Fact]
        public void AnUntrustedPyFile_UnderSecureLoadTwo_IsRefusedWithItsFullPath()
        {
            MainViewModel model = Using(2);
            string file = Script();

            string refusal = Gate(model)(file);

            Assert.Equal("File load canceled: " + file, refusal);
        }

        [Fact]
        public void TheSamePyFile_UnderSecureLoadZero_IsAllowed()
        {
            MainViewModel model = Using(0);

            Assert.Null(Gate(model)(Script()));
        }

        [Fact]
        public void APyFileInATrustedFolder_UnderSecureLoadTwo_IsAllowed_AndOneBesideItInAnotherFolderIsNot()
        {
            string trusted = Path.Combine(_folder.FullPath, "trusted");
            string other = Path.Combine(_folder.FullPath, "other");
            Directory.CreateDirectory(trusted);
            Directory.CreateDirectory(other);
            string inside = Path.Combine(trusted, "tool.py");
            string outside = Path.Combine(other, "tool.py");
            File.WriteAllText(inside, "pass\n");
            File.WriteAllText(outside, "pass\n");
            MainViewModel model = Using(2, trusted);

            Assert.Null(Gate(model)(inside));
            Assert.Equal("File load canceled: " + outside, Gate(model)(outside));
        }

        [Fact]
        public void UnderSecureLoadOne_TheQuestionIsAskedWithTheFullPath_AndItsAnswerDecides()
        {
            MainViewModel model = Using(1);
            string file = Script();
            string asked = null;
            CadLoadAnswer answer = CadLoadAnswer.DoNotLoad;
            model.Session.LoadSecurity.Ask = path =>
            {
                asked = path;
                return answer;
            };

            string refused = Gate(model)(file);
            answer = CadLoadAnswer.LoadOnce;
            string allowed = Gate(model)(file);

            Assert.Equal(file, asked);
            Assert.Equal("File load canceled: " + file, refused);
            Assert.Null(allowed);
        }

        [Fact]
        public void SetvarSecureLoadZero_AfterPythonIsUsed_AllowsTheNextFile_BecauseTheHostReadsTheGateLive()
        {
            MainViewModel model = Using(2);
            string file = Script();
            Assert.NotNull(Gate(model)(file));

            model.Execute("SETVAR");
            model.Execute("SECURELOAD");
            model.Execute("0");

            Assert.Null(Gate(model)(file));
        }

        [Fact]
        public void APythonSetInGlobalSettingsBeforeTheEngineStarts_MakesTheHostAgain_AndTheNewHostIsGatedToo()
        {
            // RefreshPython makes a new CadPythonHost when the Python path changes before the engine has started: a gate set
            // only on the first host would be lost with it.
            MainViewModel model = Using(2, python: "first");
            CadPythonHost first = HostOf(model);
            string file = Script();

            model.ApplySettings(new GlobalSettings { PythonPath = Missing("second"), SecureLoad = 2 });

            CadPythonHost second = HostOf(model);
            Assert.NotSame(first, second);
            Assert.Equal("File load canceled: " + file, Gate(second)(file));
        }
    }
}
