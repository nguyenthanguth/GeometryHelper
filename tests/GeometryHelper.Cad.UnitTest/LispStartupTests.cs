using System;
using System.IO;
using System.Text;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What loads when a drawing is opened (from the published reference of AutoCAD 2021, which the console cannot show
    /// without the support path of the machine): acad.lsp once for the program, acaddoc.lsp for every drawing, the Startup
    /// Suite, then S::STARTUP; and the Startup Suite in the settings file. The files write a mark each, so that the order
    /// shows in the history.
    /// </summary>
    public sealed class LispStartupTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-startup-" + Guid.NewGuid().ToString("N")
        );

        public LispStartupTests()
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

        private string File(string name, string text)
        {
            string path = Path.Combine(_folder, name);
            System.IO.File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        private CadCommandSession Opened(LispStartup startup)
        {
            CadCommandSession session = Session(Blank());
            session.Document.SupportFolders = new[] { _folder };
            startup.OnDocumentOpened(session);
            return session;
        }

        [Fact]
        public void TheFilesLoadInTheOrderOfAcadAcaddocSuiteAndStartupFunction()
        {
            File("acad.lsp", "(princ \"[acad]\")");
            File("acaddoc.lsp", "(princ \"[acaddoc]\")\n(defun s::startup () (princ \"[startup]\"))");
            var startup = new LispStartup();
            startup.StartupSuite.Add(File("suite1.lsp", "(princ \"[suite1]\")"));
            startup.StartupSuite.Add(File("suite2.lsp", "(princ \"[suite2]\")"));

            CadCommandSession session = Opened(startup);

            Assert.Equal(new[] { "[acad]", "[acaddoc]", "[suite1]", "[suite2]", "[startup]" }, session.History);
        }

        [Fact]
        public void AcadLspLoadsOnceForTheProgram_AndAcaddocForEveryDrawing()
        {
            File("acad.lsp", "(princ \"[acad]\")");
            File("acaddoc.lsp", "(princ \"[acaddoc]\")");
            var startup = new LispStartup();

            CadCommandSession first = Opened(startup);
            CadCommandSession second = Opened(startup);

            Assert.Equal(new[] { "[acad]", "[acaddoc]" }, first.History);
            Assert.Equal(new[] { "[acaddoc]" }, second.History);
        }

        [Fact]
        public void WithAcadLspAsDoc_AcadLspLoadsIntoEveryDrawing()
        {
            File("acad.lsp", "(princ \"[acad]\")");
            File("acaddoc.lsp", "(princ \"[acaddoc]\")");
            var startup = new LispStartup { AcadLspAsDoc = true };

            CadCommandSession first = Opened(startup);
            CadCommandSession second = Opened(startup);

            Assert.Equal(new[] { "[acad]", "[acaddoc]" }, first.History);
            Assert.Equal(new[] { "[acad]", "[acaddoc]" }, second.History);
        }

        [Fact]
        public void AnErrorInOneStep_IsWrittenAndTheNextStepRuns()
        {
            File("acad.lsp", "(princ \"[acad]\")\n(car 1)\n(princ \"[never]\")");
            File("acaddoc.lsp", "(princ \"[acaddoc]\")");
            var startup = new LispStartup();
            startup.StartupSuite.Add(Path.Combine(_folder, "missing.lsp"));
            startup.StartupSuite.Add(File("suite.lsp", "(princ \"[suite]\")"));

            CadCommandSession session = Opened(startup);

            Assert.Equal(
                new[]
                {
                    "[acad]; error: bad argument type: consp 1",
                    "[acaddoc]",
                    "; error: LOAD failed: \"" + Path.Combine(_folder, "missing.lsp").Replace("\\", "\\\\") + "\"",
                    "[suite]",
                },
                session.History
            );
        }

        [Fact]
        public void TheFunctionStartup_IsCalledLastAndItsValueIsNotWritten()
        {
            File("acaddoc.lsp", "(defun s::startup () (princ \"[startup]\") 5)");
            var startup = new LispStartup();
            startup.StartupSuite.Add(File("suite.lsp", "(princ \"[suite]\")"));

            CadCommandSession session = Opened(startup);

            Assert.Equal(new[] { "[suite]", "[startup]" }, session.History);
        }

        [Fact]
        public void WithNoFilesAndNoFunction_NothingIsWritten()
        {
            CadCommandSession session = Opened(new LispStartup());

            Assert.Empty(session.History);
        }

        [Fact]
        public void TheCommandsOfAStartupFile_AreTheDocumentsToTypeAfterwards()
        {
            File("acaddoc.lsp", "(defun c:ghup () (princ \"up\") (princ))");
            CadCommandSession session = Opened(new LispStartup());

            Run(session, "GHUP");

            Assert.Equal(new[] { "Command: GHUP", "up" }, session.History);
        }

        [Fact]
        public void ASessionIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new LispStartup().OnDocumentOpened(null));
        }

        [Fact]
        public void TheStartupSuiteOfTheSettings_IsEmptyByDefaultAndKeepsItsOrderThroughTheFile()
        {
            string path = Path.Combine(_folder, "settings.json");
            Assert.Empty(new GlobalSettings().LispStartupSuite);

            new GlobalSettings
            {
                LispStartupSuite = new[] { @" D:\Lisp\b.lsp ", "", "  ", @"D:\Lisp\a.lsp", @"d:\lisp\B.LSP" },
            }.Save(path);

            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(new[] { @"D:\Lisp\b.lsp", @"D:\Lisp\a.lsp" }, read.LispStartupSuite);
        }

        [Fact]
        public void ACopyOfTheSettings_HasASuiteOfItsOwnAndNullIsEmpty()
        {
            var settings = new GlobalSettings { LispStartupSuite = new[] { "a.lsp" } };
            GlobalSettings copy = settings.Clone();

            copy.LispStartupSuite = new[] { "b.lsp" };

            Assert.Equal(new[] { "a.lsp" }, settings.LispStartupSuite);
            Assert.Equal(new[] { "b.lsp" }, copy.LispStartupSuite);

            settings.LispStartupSuite = null;
            Assert.Empty(settings.LispStartupSuite);
        }
    }
}
