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
    /// The Lisps folder beside the program (D1210 to D1216 of the lispux spec): every <c>.lsp</c> in it, at any depth, loads into each
    /// drawing when it opens, between acaddoc.lsp and the Startup Suite, in name order; the folder is on the AutoLISP search
    /// path after the user's support folders. AutoCAD has no such folder, so the rules are ours; the order of the steps is the
    /// published one of AutoCAD 2021 with the program's own files (a menu's .mnl) between acaddoc.lsp and the Startup Suite.
    /// The program folder is made under the temporary folder and removed after; each file writes a mark so that the order
    /// shows in the history.
    /// </summary>
    public sealed class LispLispsFolderTests : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-lisps-" + Guid.NewGuid().ToString("N")
        );

        public LispLispsFolderTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            WaitingRoutines.EndAll();

            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException) { }
        }

        private string Make(string relative, string text)
        {
            string path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        private string Program => Path.Combine(_root, "app");

        private string Lisps(string name) => Path.Combine(Program, "Lisps", name);

        // A file of the folder that writes its own name in square brackets when loaded.
        private void Mark(string name) => Make(Path.Combine("app", "Lisps", name), "(princ \"[" + name + "]\")");

        private CadCommandSession NewSession(params string[] support)
        {
            CadCommandSession session = Session(Blank());
            session.Document.SupportRoots = new CadSupportFolders(Program);
            session.Document.SupportFolders = support;
            return session;
        }

        private CadCommandSession Opened(LispStartup startup, params string[] support)
        {
            CadCommandSession session = NewSession(support);
            startup.OnDocumentOpened(session);
            return session;
        }

        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        private static string Printed(string path) => "\"" + path.Replace("\\", "\\\\") + "\"";

        #region The folder

        [Fact]
        public void TheLispsFolder_IsTheFourthDefaultFolderBesideTheProgram()
        {
            var roots = new CadSupportFolders(Program);

            Assert.Equal(Path.Combine(Program, "Lisps"), roots.FolderOf(CadSupportKind.Lisps));
            Assert.Empty(roots.ExistingOf(CadSupportKind.Lisps));
            Mark("a.lsp");
            Assert.Equal(new[] { Path.Combine(Program, "Lisps") }, roots.ExistingOf(CadSupportKind.Lisps));
        }

        #endregion

        #region Loading at start

        [Fact]
        public void TwoFilesInLisps_LoadIntoANewDrawing_InNameOrder()
        {
            Mark("b.lsp");
            Mark("a.lsp");

            CadCommandSession session = Opened(new LispStartup());

            Assert.Equal(new[] { "[a.lsp]", "[b.lsp]" }, session.History);
        }

        [Fact]
        public void TheNameOrder_IgnoresCase_AndComparesTheWholeName()
        {
            Mark("B.lsp");
            Mark("a.lsp");
            Mark("a-b.lsp");
            Mark("C.lsp");

            CadCommandSession session = Opened(new LispStartup());

            // '-' comes before '.', so a-b.lsp is before a.lsp: the whole name is compared, not the part before the dot
            Assert.Equal(new[] { "[a-b.lsp]", "[a.lsp]", "[B.lsp]", "[C.lsp]" }, session.History);
        }

        [Fact]
        public void EveryDrawingOpened_GetsTheFilesAgain()
        {
            Mark("a.lsp");
            var startup = new LispStartup();

            CadCommandSession first = Opened(startup);
            CadCommandSession second = Opened(startup);

            Assert.Equal(new[] { "[a.lsp]" }, first.History);
            Assert.Equal(new[] { "[a.lsp]" }, second.History);
        }

        [Fact]
        public void AFileThatErrors_IsWritten_AndSkipped_AndTheOthersLoad()
        {
            Make(@"app\Lisps\a.lsp", "(princ \"[a]\")\n(car 1)\n(princ \"[never]\")");
            Mark("b.lsp");

            CadCommandSession session = Opened(new LispStartup());

            Assert.Equal(new[] { "[a]; error: bad argument type: consp 1", "[b.lsp]" }, session.History);
        }

        [Fact]
        public void OnlyTheLspFiles_Load_ThoseInASubfolderToo_AfterTheTopOnes()
        {
            Mark("a.lsp");
            Mark("x.lspx");
            Mark("readme.txt");
            Mark("tools.dcl");
            Make(@"app\Lisps\Sub\c.lsp", "(princ \"[c]\")");

            CadCommandSession session = Opened(new LispStartup());

            Assert.Equal(new[] { "[a.lsp]", "[c]" }, session.History);
        }

        [Fact]
        public void ACompiledFile_WritesTheRefusal_AndTheRestLoads()
        {
            Mark("a.lsp");
            Make(@"app\Lisps\b.fas", "x");
            Make(@"app\Lisps\c.vlx", "x");
            Mark("d.lsp");

            CadCommandSession session = Opened(new LispStartup());

            Assert.Equal(
                new[]
                {
                    "[a.lsp]",
                    "; error: LOAD failed: " + Printed(Lisps("b.fas")) + " (compiled AutoLISP is not supported)",
                    "; error: LOAD failed: " + Printed(Lisps("c.vlx")) + " (compiled AutoLISP is not supported)",
                    "[d.lsp]",
                },
                session.History
            );
        }

        [Fact]
        public void WithNoLispsFolder_NothingIsWritten()
        {
            Directory.CreateDirectory(Program);

            CadCommandSession session = Opened(new LispStartup());

            Assert.Empty(session.History);
        }

        [Fact]
        public void TheCommandsOfAFile_AreTheDocumentsToTypeAfterwards()
        {
            Make(@"app\Lisps\tools.lsp", "(defun c:ghlisps () (princ \"up\") (princ))");
            CadCommandSession session = Opened(new LispStartup());

            Run(session, "GHLISPS");

            Assert.Equal(new[] { "Command: GHLISPS", "up" }, session.History);
            Assert.Equal(new[] { Lisps("tools.lsp") }, session.LoadedLispFiles, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void AFileInLisps_GoesThroughTheLoadGate()
        {
            // the gate trusts the folder of the program (L6 D902); here the program folder is a temporary one, so it asks
            Mark("a.lsp");
            CadCommandSession session = NewSession();
            session.Lisp.Run("(setvar \"SECURELOAD\" 1)");

            new LispStartup().OnDocumentOpened(session);

            Assert.Equal(new[] { "; error: File load canceled: " + Lisps("a.lsp") }, session.History);
        }

        #endregion

        #region The order of the steps

        [Fact]
        public void TheOrder_IsAcadLsp_AcaddocLsp_Lisps_TheStartupSuite_ThenStartupFunction()
        {
            string support = Path.Combine(_root, "support");
            Make(@"support\acad.lsp", "(princ \"[acad]\")");
            Make(@"support\acaddoc.lsp", "(princ \"[acaddoc]\")\n(defun s::startup () (princ \"[startup]\"))");
            Mark("b.lsp");
            Mark("a.lsp");
            var startup = new LispStartup();
            startup.StartupSuite.Add(Make(@"suite\suite1.lsp", "(princ \"[suite1]\")"));

            CadCommandSession session = Opened(startup, support);

            Assert.Equal(
                new[] { "[acad]", "[acaddoc]", "[a.lsp]", "[b.lsp]", "[suite1]", "[startup]" },
                session.History
            );
        }

        [Fact]
        public void AnAcaddocLspInLisps_IsFoundAsAcaddocLsp_AndLoadsOnce()
        {
            Make(@"app\Lisps\acaddoc.lsp", "(princ \"[acaddoc]\")");
            Mark("a.lsp");

            CadCommandSession session = Opened(new LispStartup());

            Assert.Equal(new[] { "[acaddoc]", "[a.lsp]" }, session.History);
        }

        [Fact]
        public void AnAcadLspInLisps_LoadsOnceForTheProgram_NotForEveryDrawing()
        {
            Make(@"app\Lisps\acad.lsp", "(princ \"[acad]\")");
            Mark("a.lsp");
            var startup = new LispStartup();

            CadCommandSession first = Opened(startup);
            CadCommandSession second = Opened(startup);

            Assert.Equal(new[] { "[acad]", "[a.lsp]" }, first.History);
            Assert.Equal(new[] { "[a.lsp]" }, second.History);
        }

        [Fact]
        public void AnAcaddocLspOfTheSupportFolders_WinsOverOneInLisps()
        {
            string support = Path.Combine(_root, "support");
            Make(@"support\acaddoc.lsp", "(princ \"[mine]\")");
            Make(@"app\Lisps\acaddoc.lsp", "(princ \"[lisps]\")");

            CadCommandSession session = Opened(new LispStartup(), support);

            Assert.Equal(new[] { "[mine]" }, session.History);
        }

        #endregion

        #region The search path

        [Fact]
        public void TheSearchPath_IsTheSupportFoldersThenLisps_AndTheDocumentsOwnListIsNotChanged()
        {
            string support = Path.Combine(_root, "support");
            Directory.CreateDirectory(support);
            Mark("a.lsp");
            CadCommandSession session = NewSession(support);

            Assert.Equal(new[] { support, Path.Combine(Program, "Lisps") }, session.Lisp.SupportFolders);
            Assert.Equal(new[] { support }, session.Document.SupportFolders);
        }

        [Fact]
        public void ALispsFolderTheUserListedAsWell_IsOnThePathOnce()
        {
            string listed = Path.Combine(Program, "Lisps") + "\\";
            Mark("a.lsp");
            CadCommandSession session = NewSession(listed);

            Assert.Equal(new[] { listed }, session.Lisp.SupportFolders);
        }

        [Fact]
        public void Load_OfABareName_FindsAFileInLisps()
        {
            Make(@"app\Lisps\a.lsp", "42");
            CadCommandSession session = NewSession();

            Assert.Equal("42", Value(session, "(load \"a\")"));
        }

        [Fact]
        public void Findfile_FindsAFileInLisps_AfterTheSupportFolders()
        {
            string support = Path.Combine(_root, "support");
            Make(@"support\both.lsp", "1");
            Make(@"app\Lisps\both.lsp", "2");
            Make(@"app\Lisps\only.lsp", "3");
            CadCommandSession session = NewSession(support);

            Assert.Equal(Printed(Path.Combine(support, "both.lsp")), Value(session, "(findfile \"both.lsp\")"));
            Assert.Equal(Printed(Lisps("only.lsp")), Value(session, "(findfile \"only.lsp\")"));
        }

        [Fact]
        public void Load_OfAFileInASubfolderOfLisps_ReachesIt()
        {
            Make(@"app\Lisps\Sub\x.lsp", "7");
            CadCommandSession session = NewSession();

            Assert.Equal("7", Value(session, "(load \"Sub/x\")"));
            Assert.Equal("7", Value(session, "(load \"Sub\\\\x.lsp\")"));
        }

        [Fact]
        public void LoadDialog_OfADclInLisps_AndInItsSubfolder_FindsIt()
        {
            Make(@"app\Lisps\kit.dcl", "ghok : dialog { label = \"Only\"; : text { label = \"x\"; } ok_only; }");
            Make(@"app\Lisps\Sub\kit2.dcl", "ghok2 : dialog { label = \"Only\"; : text { label = \"x\"; } ok_only; }");
            CadCommandSession session = NewSession();

            Assert.Equal("1", Value(session, "(load_dialog \"kit\")"));
            Assert.Equal("2", Value(session, "(load_dialog \"Sub/kit2\")"));
        }

        #endregion
    }
}
