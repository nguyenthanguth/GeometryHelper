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
    /// The AutoLISP files a document has loaded, which Load/Unload Applications lists (the interpreter's <c>LoadedFiles</c>, the
    /// session's <c>LoadedLispFiles</c>), the commands each defined, Unload, and the folder APPLOAD keeps in the settings.
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
            session.Document.SupportRoots = new CadSupportFolders(null);
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
        public void TheAppLoadFolderSetting_IsEmptyByDefault_AndKeptThroughTheFile()
        {
            string path = Path.Combine(_folder, "settings.json");
            Assert.Equal(string.Empty, new GlobalSettings().AppLoadFolder);

            new GlobalSettings { AppLoadFolder = @" D:\Lisp " }.Save(path);

            Assert.Equal(@"D:\Lisp", GlobalSettings.Load(path).AppLoadFolder);
        }

        [Fact]
        public void TheKeysOfTheOldHistoryList_AreIgnoredWhenTheSettingsAreRead()
        {
            string path = Path.Combine(_folder, "old.json");
            System.IO.File.WriteAllText(
                path,
                @"{ ""support"": { ""appLoadHistory"": [""D:\\Lisp\\a.lsp""], ""appLoadFileType"": ""dll"", ""appLoadAddToHistory"": false, ""appLoadFolder"": ""D:\\Lisp"" } }",
                new UTF8Encoding(false)
            );

            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(@"D:\Lisp", read.AppLoadFolder);
        }

        [Fact]
        public void TheCommandsAFileDefined_AreListedInTheOrderTheyWereDefined_EachOnce()
        {
            string path = File("defs.lsp", "(defun c:BB () 1) (defun helper () 2) (defun c:aa () 3) (defun c:bb () 4)");
            CadCommandSession session = Session(Blank());

            session.LoadLisp(path);

            Assert.Equal(new[] { "BB", "AA" }, session.LispCommandsOf(path));
            Assert.Equal(new[] { "BB", "AA" }, session.LispCommandsOf(path.ToUpperInvariant()));
        }

        [Fact]
        public void AFileThatDefinesNoCommand_HasNoCommands_AndSoHasAFileNotLoaded()
        {
            string path = File("plain.lsp", "(defun helper () 2)");
            CadCommandSession session = Session(Blank());

            Assert.Empty(session.LispCommandsOf(path));

            session.LoadLisp(path);

            Assert.Empty(session.LispCommandsOf(path));
        }

        [Fact]
        public void ACommandOfAFileItLoads_CountsForThatFile()
        {
            string inner = File("inner.lsp", "(defun c:inn () 1)");
            string outer = File(
                "outer.lsp",
                "(defun c:out () 1) (load \"" + inner.Replace('\\', '/') + "\") (defun c:out2 () 2)"
            );
            CadCommandSession session = Session(Blank());

            session.LoadLisp(outer);

            Assert.Equal(new[] { "OUT", "OUT2" }, session.LispCommandsOf(outer));
            Assert.Equal(new[] { "INN" }, session.LispCommandsOf(inner));
        }

        [Fact]
        public void UnloadingAFile_LeavesTheList_AndMakesItsCommandsUnknown_ButKeepsItsOtherFunctions()
        {
            string path = File("two.lsp", "(defun c:aa () 1) (defun c:bb () 2) (defun helper () 3)");
            CadCommandSession session = Session(Blank());
            session.LoadLisp(path);
            Assert.True(session.Start("AA"));

            Assert.True(session.UnloadLisp(path));

            Assert.Empty(session.LoadedLispFiles);
            Assert.Empty(session.LispCommandNames);
            Assert.Empty(session.LispCommandsOf(path));
            Assert.False(session.Start("BB"));
            Assert.Contains("Unknown command", session.History.Last());
            session.Feed("(helper)");
            Assert.Contains("3", session.History.Last());
            Assert.False(session.UnloadLisp(path));
        }

        [Fact]
        public void UnloadingAFile_KeepsACommandAnotherLoadedFileDefinedAsWell()
        {
            string first = File("first.lsp", "(defun c:shared () 1) (defun c:one () 1)");
            string second = File("second.lsp", "(defun c:shared () 2)");
            CadCommandSession session = Session(Blank());
            session.LoadLisp(first);
            session.LoadLisp(second);

            session.UnloadLisp(first);

            Assert.Equal(new[] { "SHARED" }, session.LispCommandNames);

            session.UnloadLisp(second);

            Assert.Empty(session.LispCommandNames);
        }

        [Fact]
        public void AFileLoadedAgain_DefinesItsCommandsAgain_AfterItWasUnloaded()
        {
            string path = File("again.lsp", "(defun c:aa () 1)");
            CadCommandSession session = Session(Blank());
            session.LoadLisp(path);
            session.UnloadLisp(path);

            session.LoadLisp(path);

            Assert.Equal(new[] { "AA" }, session.LispCommandsOf(path));
            Assert.Equal(new[] { "AA" }, session.LispCommandNames);
        }
    }
}
