using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The AutoLISP files a document has loaded, which the Loaded Applications list of APPLOAD shows (the interpreter's
    /// <c>LoadedFiles</c>, the session's <c>LoadedLispFiles</c>), and the settings APPLOAD keeps: its History list, the folder
    /// it opens on and Add to History.
    /// </summary>
    public sealed class LispLoadedFilesTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-loaded-" + Guid.NewGuid().ToString("N")
        );

        public LispLoadedFilesTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            WaitingRoutines.EndAll();

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

        [Fact]
        public void ADocumentThatRanNoAutoLisp_HasLoadedNoFile()
        {
            CadCommandSession session = Session(Blank());

            Assert.Empty(session.LoadedLispFiles);

            session.Feed("(setq a 1)");

            Assert.Empty(session.LoadedLispFiles);
        }

        [Fact]
        public void AFileLoadedByLoadLisp_IsListedByItsFullPath()
        {
            string path = File("one.lsp", "(setq one 1)");
            CadCommandSession session = Session(Blank());

            Assert.True(session.LoadLisp(path));

            Assert.Equal(new[] { path }, session.LoadedLispFiles);
        }

        [Fact]
        public void AFileLoadedByTheLoadFunctionFromASupportFolder_IsListedByTheFileFound()
        {
            string path = File("viasupport.lsp", "(setq vs 1)");
            CadCommandSession session = Session(Blank());
            session.Document.SupportFolders = new[] { _folder };

            session.Feed("(load \"viasupport\")");

            Assert.Equal(new[] { path }, session.LoadedLispFiles);
        }

        [Fact]
        public void AFileThatLoadsAnother_ListsBothInTheOrderTheyFinished()
        {
            string inner = File("inner.lsp", "(setq inner 1)");
            string outer = File("outer.lsp", "(load \"" + inner.Replace("\\", "\\\\") + "\")\n(setq outer 1)");
            CadCommandSession session = Session(Blank());

            Assert.True(session.LoadLisp(outer));

            Assert.Equal(new[] { inner, outer }, session.LoadedLispFiles);
        }

        [Fact]
        public void AFileLoadedTwice_IsListedOnceAtItsFirstPlaceEitherCase()
        {
            string first = File("first.lsp", "(setq f 1)");
            string second = File("second.lsp", "(setq s 1)");
            CadCommandSession session = Session(Blank());

            session.LoadLisp(first);
            session.LoadLisp(second);
            session.LoadLisp(first);
            session.LoadLisp(Path.Combine(_folder.ToUpperInvariant(), "FIRST.LSP"));

            Assert.Equal(new[] { first, second }, session.LoadedLispFiles);
        }

        [Fact]
        public void AFileWithAnError_AndAFileThatIsMissing_AreNotListed()
        {
            string bad = File("bad.lsp", "(setq b 1)\n(car 1)");
            CadCommandSession session = Session(Blank());

            Assert.False(session.LoadLisp(bad));
            Assert.False(session.LoadLisp(Path.Combine(_folder, "missing.lsp")));
            session.Feed("(load \"missing\" \"not found\")");

            Assert.Empty(session.LoadedLispFiles);
        }

        [Fact]
        public void ACompiledFile_IsRefusedAndNotListed()
        {
            string compiled = File("tools.fas", "x");
            CadCommandSession session = Session(Blank());

            Assert.False(session.LoadLisp(compiled));

            Assert.Empty(session.LoadedLispFiles);
        }

        [Fact]
        public void TheStartupFiles_AreListedInTheOrderTheyLoaded()
        {
            string acaddoc = File("acaddoc.lsp", "(setq d 1)");
            string suite = File("suite.lsp", "(setq s 1)");
            var startup = new LispStartup();
            startup.StartupSuite.Add(suite);
            CadCommandSession session = Session(Blank());
            session.Document.SupportFolders = new[] { _folder };

            startup.OnDocumentOpened(session);

            Assert.Equal(new[] { acaddoc, suite }, session.LoadedLispFiles);
        }

        [Fact]
        public void TwoDocuments_EachListTheFilesLoadedIntoItself()
        {
            string path = File("only.lsp", "(setq o 1)");
            CadCommandSession first = Session(Blank());
            CadCommandSession second = Session(Blank());

            first.LoadLisp(path);

            Assert.Single(first.LoadedLispFiles);
            Assert.Empty(second.LoadedLispFiles);
        }

        [Fact]
        public void TheAppLoadSettings_AreEmptyAndOnByDefault()
        {
            var settings = new GlobalSettings();

            Assert.Empty(settings.AppLoadHistory);
            Assert.Equal(string.Empty, settings.AppLoadFolder);
            Assert.True(settings.AppLoadAddToHistory);
        }

        [Fact]
        public void TheAppLoadHistory_KeepsItsOrderThroughTheFileAndLeavesBlanksAndRepeatsOut()
        {
            string path = Path.Combine(_folder, "settings.json");

            new GlobalSettings
            {
                AppLoadHistory = new[] { @" D:\Lisp\b.lsp ", "", "  ", @"D:\Lisp\a.lsp", @"d:\lisp\B.LSP" },
                AppLoadFolder = @" D:\Lisp ",
                AppLoadAddToHistory = false,
            }.Save(path);

            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(new[] { @"D:\Lisp\b.lsp", @"D:\Lisp\a.lsp" }, read.AppLoadHistory);
            Assert.Equal(@"D:\Lisp", read.AppLoadFolder);
            Assert.False(read.AppLoadAddToHistory);
        }

        [Fact]
        public void TheAppLoadHistory_KeepsTheFirstFiftyOnly()
        {
            var settings = new GlobalSettings
            {
                AppLoadHistory = Enumerable.Range(1, 60).Select(i => @"D:\Lisp\f" + i + ".lsp").ToList(),
            };

            Assert.Equal(GlobalSettings.AppLoadHistoryLimit, settings.AppLoadHistory.Count);
            Assert.Equal(@"D:\Lisp\f1.lsp", settings.AppLoadHistory[0]);
            Assert.Equal(@"D:\Lisp\f50.lsp", settings.AppLoadHistory[49]);
        }

        [Fact]
        public void ACopyOfTheSettings_HasAnAppLoadHistoryOfItsOwnAndNullIsEmpty()
        {
            var settings = new GlobalSettings { AppLoadHistory = new[] { "a.lsp" } };
            GlobalSettings copy = settings.Clone();

            copy.AppLoadHistory = new[] { "b.lsp" };

            Assert.Equal(new[] { "a.lsp" }, settings.AppLoadHistory);
            Assert.Equal(new[] { "b.lsp" }, copy.AppLoadHistory);

            settings.AppLoadHistory = null;
            Assert.Empty(settings.AppLoadHistory);
        }
    }
}
