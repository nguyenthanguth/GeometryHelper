using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// LEGACYCODESEARCH (decisions D908 and D909 of the L6 spec): with 0, the default, a search for a code file does not look
    /// in the current folder or the folder of the drawing; with 1 it does. <c>findfile</c>, <c>load</c> and
    /// <c>load_dialog</c> against AutoCAD 2021 (research M2, M3 and M5, console): <c>findfile</c> of <c>ghl6f.&lt;ext&gt;</c>
    /// beside the drawing found <c>txt js lin pat</c> and not <c>lsp dcl scr fas vlx mnl dll arx dvb cuix</c> with 0, and all
    /// fourteen with 1. The drawing is saved in a folder of the test's own under the temporary folder, and its files are made
    /// beside it. A session of the library is at SECURELOAD 0, so the search is what these tests see unless one sets it.
    /// </summary>
    public sealed class LispLegacyCodeSearchTests : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-legacy-" + Guid.NewGuid().ToString("N")
        );

        private readonly string _plans;

        public LispLegacyCodeSearchTests()
        {
            _plans = Path.Combine(_root, "plans");
            Directory.CreateDirectory(_plans);
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

        private string Make(string relative, string text = "42")
        {
            string path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
            return path;
        }

        // A session whose drawing is saved in the plans folder; the current folder is the one given, none by default.
        private CadCommandSession Beside(string current = null)
        {
            CadDocument document = Blank();
            document.SaveAs(Path.Combine(_plans, "plan.dxf"));
            CadCommandSession session = Session(document);
            session.Lisp.CurrentFolder = () => current ?? Path.Combine(_root, "nowhere");
            return session;
        }

        // A session whose drawing was never saved, so only the current folder is a place to look beside.
        private CadCommandSession InCurrentFolder(string current)
        {
            CadCommandSession session = Session(Blank());
            session.Lisp.CurrentFolder = () => current;
            return session;
        }

        private static string Printed(string path) => "\"" + path.Replace("\\", "\\\\") + "\"";

        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        private static void Search(CadCommandSession session, int value) =>
            Value(session, "(setvar \"LEGACYCODESEARCH\" " + value + ")");

        #region findfile

        [Theory]
        [InlineData("txt", true)] // AutoCAD 2021 (M5): found with LEGACYCODESEARCH 0 ...
        [InlineData("js", true)]
        [InlineData("lin", true)]
        [InlineData("pat", true)]
        [InlineData("lsp", false)] // ... and these ten are not
        [InlineData("dcl", false)]
        [InlineData("scr", false)]
        [InlineData("fas", false)]
        [InlineData("vlx", false)]
        [InlineData("mnl", false)]
        [InlineData("dll", false)]
        [InlineData("arx", false)]
        [InlineData("dvb", false)]
        [InlineData("cuix", false)]
        public void Findfile_BesideTheDrawing_WithLegacyCodeSearchZero_FindsOnlyTheFilesThatAreNotCode(
            string extension,
            bool found
        )
        {
            string path = Make(@"plans\ghl6f." + extension);
            CadCommandSession session = Beside();

            Search(session, 0);

            Assert.Equal(found ? Printed(path) : "nil", Value(session, "(findfile \"ghl6f." + extension + "\")"));
        }

        [Theory]
        [InlineData("txt")]
        [InlineData("js")]
        [InlineData("lin")]
        [InlineData("pat")]
        [InlineData("lsp")]
        [InlineData("dcl")]
        [InlineData("scr")]
        [InlineData("fas")]
        [InlineData("vlx")]
        [InlineData("mnl")]
        [InlineData("dll")]
        [InlineData("arx")]
        [InlineData("dvb")]
        [InlineData("cuix")]
        public void Findfile_BesideTheDrawing_WithLegacyCodeSearchOne_FindsAllFourteen(string extension)
        {
            // AutoCAD 2021 (M5): with LEGACYCODESEARCH 1 all fourteen are found
            string path = Make(@"plans\ghl6f." + extension);
            CadCommandSession session = Beside();

            Search(session, 1);

            Assert.Equal(Printed(path), Value(session, "(findfile \"ghl6f." + extension + "\")"));
        }

        [Theory]
        [InlineData("txt", true)]
        [InlineData("lin", true)]
        [InlineData("lsp", false)]
        [InlineData("dcl", false)]
        [InlineData("cuix", false)]
        public void Findfile_InTheCurrentFolder_FollowsTheSameRule(string extension, bool foundWithZero)
        {
            // D909: the current folder is left out for the code files too, the drawing's folder or not
            string current = Path.Combine(_root, "current");
            string path = Make(@"current\ghl6c." + extension);
            CadCommandSession session = InCurrentFolder(current);
            string form = "(findfile \"ghl6c." + extension + "\")";

            Search(session, 0);
            Assert.Equal(foundWithZero ? Printed(path) : "nil", Value(session, form));

            Search(session, 1);
            Assert.Equal(Printed(path), Value(session, form));
        }

        [Fact]
        public void Findfile_OfACodeFile_AfterLegacyCodeSearchGoesBackToZero_IsNotFoundAgain()
        {
            Make(@"plans\ghl6f.lsp");
            CadCommandSession session = Beside();

            Search(session, 1);
            Assert.NotEqual("nil", Value(session, "(findfile \"ghl6f.lsp\")"));

            Search(session, 0);
            Assert.Equal("nil", Value(session, "(findfile \"ghl6f.lsp\")"));
        }

        [Fact]
        public void Findfile_OfACodeFile_IsFoundOnTheSupportFolders_WithLegacyCodeSearchZero()
        {
            // AutoCAD 2021 (M5): the support path is what is searched with 0
            string path = Make(@"support\ghl6f.lsp");
            CadCommandSession session = Beside();
            session.Document.SupportFolders = new[] { Path.Combine(_root, "support") };

            Search(session, 0);

            Assert.Equal(Printed(path), Value(session, "(findfile \"ghl6f.lsp\")"));
        }

        [Fact]
        public void Findfile_OfACodeFileWithAFolder_IsFoundWhereItPoints_WithLegacyCodeSearchZero()
        {
            // D909 is for a bare name: a name that says where is that file
            string path = Make(@"plans\ghl6f.lsp");
            CadCommandSession session = Beside();

            Search(session, 0);

            Assert.Equal(Printed(path), Value(session, "(findfile \"" + path.Replace('\\', '/') + "\")"));
        }

        [Fact]
        public void Findfile_OfAFileOfAnyCase_FollowsTheRuleToo()
        {
            // the extension is read without regard to case: GHL6F.LSP on disk, asked as ghl6f.lsp
            Make(@"plans\GHL6F.LSP");
            CadCommandSession session = Beside();

            Search(session, 0);
            Assert.Equal("nil", Value(session, "(findfile \"ghl6f.lsp\")"));

            Search(session, 1);
            Assert.NotEqual("nil", Value(session, "(findfile \"ghl6f.lsp\")"));
        }

        [Fact]
        public void Findfile_OfANameWithNoExtension_IsFoundBesideTheDrawing_WithLegacyCodeSearchZero()
        {
            // D909: only the ten extensions are left out
            string path = Make(@"plans\ghl6noext");
            CadCommandSession session = Beside();

            Search(session, 0);

            Assert.Equal(Printed(path), Value(session, "(findfile \"ghl6noext\")"));
        }

        [Fact]
        public void WithLegacyCodeSearchOne_TheOrderIsTheCurrentFolder_ThenTheDrawing_ThenTheSupportFolders()
        {
            // AutoCAD 2021 (M3): load found the file beside the drawing; the order is findfile's (L3)
            string current = Make(@"current\same.lsp");
            string drawing = Make(@"plans\same.lsp");
            string support = Make(@"support\same.lsp");
            CadCommandSession session = Beside(Path.Combine(_root, "current"));
            session.Document.SupportFolders = new[] { Path.Combine(_root, "support") };
            Search(session, 1);

            Assert.Equal(Printed(current), Value(session, "(findfile \"same.lsp\")"));

            File.Delete(current);
            Assert.Equal(Printed(drawing), Value(session, "(findfile \"same.lsp\")"));

            File.Delete(drawing);
            Assert.Equal(Printed(support), Value(session, "(findfile \"same.lsp\")"));
        }

        #endregion

        #region load

        [Fact]
        public void Load_OfABareName_BesideTheDrawing_IsNotFound_WithLegacyCodeSearchZero()
        {
            // AutoCAD 2021 (M3): LEGACYCODESEARCH 0, (load "ghl6_leg") ; error: LOAD failed: "ghl6_leg"
            Make(@"plans\ghl6leg.lsp", "(setq ghl6leg 7)\n42\n");
            CadCommandSession session = Beside();

            Assert.Equal("; error: LOAD failed: \"ghl6leg\"", Value(session, "(load \"ghl6leg\")"));
            Assert.Equal("; error: LOAD failed: \"ghl6leg.lsp\"", Value(session, "(load \"ghl6leg.lsp\")"));
            Assert.Equal("\"fb\"", Value(session, "(load \"ghl6leg\" \"fb\")"));
        }

        [Fact]
        public void Load_OfABareName_BesideTheDrawing_Loads_WithLegacyCodeSearchOneAndSecureLoadZero()
        {
            // AutoCAD 2021 (M3): SECURELOAD 0 and LEGACYCODESEARCH 1: the file beside the drawing loads
            Make(@"plans\ghl6leg.lsp", "(setq ghl6leg 7)\n42\n");
            CadCommandSession session = Beside();
            Search(session, 1);

            Assert.Equal("42", Value(session, "(load \"ghl6leg\")"));
            Assert.Equal("7", Value(session, "!ghl6leg"));
            Assert.Equal("42", Value(session, "(load \"ghl6leg.lsp\")"));
        }

        [Fact]
        public void Load_OfABareName_InTheCurrentFolder_Loads_WithLegacyCodeSearchOne()
        {
            Make(@"current\ghl6cur.lsp", "(setq ghl6cur 9)\n42\n");
            CadCommandSession session = InCurrentFolder(Path.Combine(_root, "current"));

            Assert.Equal("; error: LOAD failed: \"ghl6cur\"", Value(session, "(load \"ghl6cur\")"));

            Search(session, 1);

            Assert.Equal("42", Value(session, "(load \"ghl6cur\")"));
        }

        [Fact]
        public void Load_OfABareName_BesideTheDrawing_WithSecureLoadOneAndNoQuestion_IsNotFound()
        {
            // AutoCAD 2021 (M2): LEGACYCODESEARCH 1 with SECURELOAD 1 in the console: (load "ghl6_leg") ; error: LOAD failed
            Make(@"plans\ghl6leg.lsp", "(setq ghl6leg 7)\n42\n");
            CadCommandSession session = Beside();
            Search(session, 1);
            Value(session, "(setvar \"SECURELOAD\" 1)");

            Assert.Equal("; error: LOAD failed: \"ghl6leg\"", Value(session, "(load \"ghl6leg\")"));
            Assert.Equal("nil", Value(session, "(boundp 'ghl6leg)"));
        }

        [Fact]
        public void Load_OfABareName_BesideTheDrawing_WithSecureLoadOne_Loads_WhenTheDrawingsFolderIsTrusted()
        {
            Make(@"plans\ghl6leg.lsp", "(setq ghl6leg 7)\n42\n");
            CadCommandSession session = Beside();
            Search(session, 1);
            Value(session, "(setvar \"SECURELOAD\" 1)");
            Value(session, "(setvar \"TRUSTEDPATHS\" \"" + _plans.Replace("\\", "\\\\") + "\")");

            Assert.Equal("42", Value(session, "(load \"ghl6leg\")"));
        }

        #endregion

        #region load_dialog

        private const string Dcl = "ghl6d : dialog { label = \"x\"; ok_only; }\n";

        [Fact]
        public void LoadDialog_OfABareDclName_BesideTheDrawing_IsNotFound_WithLegacyCodeSearchZero()
        {
            // D909: load_dialog follows findfile; -1 is the answer for a file not found
            Make(@"plans\ghl6d.dcl", Dcl);
            CadCommandSession session = Beside();

            Assert.Equal("-1", Value(session, "(load_dialog \"ghl6d.dcl\")"));
        }

        [Fact]
        public void LoadDialog_OfABareDclName_BesideTheDrawing_GivesAnId_WithLegacyCodeSearchOne()
        {
            Make(@"plans\ghl6d.dcl", Dcl);
            CadCommandSession session = Beside();
            Search(session, 1);

            Assert.NotEqual("-1", Value(session, "(load_dialog \"ghl6d.dcl\")"));
        }

        [Fact]
        public void LoadDialog_OfABareDclName_OnTheSupportFolders_GivesAnId_WithLegacyCodeSearchZero()
        {
            Make(@"support\ghl6d.dcl", Dcl);
            CadCommandSession session = Beside();
            session.Document.SupportFolders = new[] { Path.Combine(_root, "support") };

            Assert.NotEqual("-1", Value(session, "(load_dialog \"ghl6d.dcl\")"));
        }

        [Fact]
        public void LoadDialog_OfAFullPath_GivesAnId_WithLegacyCodeSearchZero()
        {
            string path = Make(@"plans\ghl6d.dcl", Dcl);
            CadCommandSession session = Beside();

            Assert.NotEqual("-1", Value(session, "(load_dialog \"" + path.Replace('\\', '/') + "\")"));
        }

        #endregion
    }
}
