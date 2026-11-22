using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A name with a relative folder (<c>Sub/x</c>, <c>./x.lsp</c>, <c>../x.lsp</c>) is looked up along the same folders as a bare
    /// name, in the same order, by <c>findfile</c>, <c>load</c> and <c>load_dialog</c>. Against AutoCAD 2021's accoreconsole
    /// (lispux research section 5 and C1's probes p1 to p3): <c>(findfile "en-us/3d.lsp")</c> and <c>"./en-us/3d.lsp"</c> found the
    /// file of the support folder; with the same relative name under the current folder too, LEGACYCODESEARCH 0 gave the support
    /// folder's and 1 the current folder's; a code file found only under the current folder was nil at 0 and found at 1, a
    /// <c>.txt</c> in both; <c>(load "mysub/mine")</c> followed the same rule. At SECURELOAD 1 a refused <c>Sub/x</c> was
    /// <c>LOAD failed: "Sub/x"</c> and a refused <c>./Sub/x.lsp</c>, with its extension, <c>File load canceled:</c> and the full
    /// path with forward slashes. The folders are made under the temporary folder and removed after.
    /// </summary>
    public sealed class LispRelativeNameTests : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-relative-" + Guid.NewGuid().ToString("N")
        );

        public LispRelativeNameTests()
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

        // A session with the folder "support" on the search path and the folder "current" as the current folder.
        private CadCommandSession NewSession(int legacyCodeSearch = 0, int secureLoad = 0)
        {
            Directory.CreateDirectory(Path.Combine(_root, "support"));
            Directory.CreateDirectory(Path.Combine(_root, "current"));
            CadCommandSession session = Session(Blank());
            session.Document.SupportRoots = new CadSupportFolders(null);
            session.Document.SupportFolders = new[] { Path.Combine(_root, "support") };
            session.Lisp.CurrentFolder = () => Path.Combine(_root, "current");
            session.Lisp.Run("(setvar \"LEGACYCODESEARCH\" " + legacyCodeSearch + ")");
            session.Lisp.Run("(setvar \"SECURELOAD\" " + secureLoad + ")");
            return session;
        }

        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        private static string Printed(string path) => "\"" + path.Replace("\\", "\\\\") + "\"";

        private string At(string relative) => Path.Combine(_root, relative);

        #region findfile

        [Theory]
        [InlineData("Sub/x.lsp")]
        [InlineData("./Sub/x.lsp")]
        [InlineData("Sub\\\\x.lsp")]
        [InlineData("../support/Sub/x.lsp")]
        public void Findfile_OfARelativeNameWithAFolder_LooksAlongTheSupportFolders(string name)
        {
            // AutoCAD 2021: (findfile "en-us/3d.lsp") and "./en-us/3d.lsp" are found in the support folder
            string path = Make(@"support\Sub\x.lsp", "1");
            CadCommandSession session = NewSession();

            Assert.Equal(Printed(path), Value(session, "(findfile \"" + name + "\")"));
        }

        [Fact]
        public void Findfile_WithBothFolders_TheSupportFolderWinsAtZero_AndTheCurrentFolderAtOne()
        {
            // AutoCAD 2021: LEGACYCODESEARCH 0 gave the support folder's en-us/3d.lsp, 1 the current folder's
            string support = Make(@"support\Sub\x.lsp", "1");
            string current = Make(@"current\Sub\x.lsp", "2");
            CadCommandSession session = NewSession();

            Assert.Equal(Printed(support), Value(session, "(findfile \"Sub/x.lsp\")"));
            Assert.Equal(Printed(support), Value(session, "(findfile \"./Sub/x.lsp\")"));
            session.Lisp.Run("(setvar \"LEGACYCODESEARCH\" 1)");
            Assert.Equal(Printed(current), Value(session, "(findfile \"Sub/x.lsp\")"));
            Assert.Equal(Printed(current), Value(session, "(findfile \"./Sub/x.lsp\")"));
        }

        [Fact]
        public void Findfile_OfACodeFileOnlyUnderTheCurrentFolder_IsNilAtZero_AndFoundAtOne()
        {
            // AutoCAD 2021: mysub/mine.lsp nil at 0 and found at 1; mysub/mine.txt found at both
            string code = Make(@"current\Sub\mine.lsp", "1");
            string text = Make(@"current\Sub\mine.txt", "x");
            CadCommandSession session = NewSession();

            Assert.Equal("nil", Value(session, "(findfile \"Sub/mine.lsp\")"));
            Assert.Equal("nil", Value(session, "(findfile \"./Sub/mine.lsp\")"));
            Assert.Equal(Printed(text), Value(session, "(findfile \"Sub/mine.txt\")"));
            session.Lisp.Run("(setvar \"LEGACYCODESEARCH\" 1)");
            Assert.Equal(Printed(code), Value(session, "(findfile \"Sub/mine.lsp\")"));
            Assert.Equal(Printed(text), Value(session, "(findfile \"Sub/mine.txt\")"));
        }

        [Fact]
        public void Findfile_OfARootedName_StaysWhereItPoints()
        {
            string path = Make(@"elsewhere\y.lsp", "1");
            CadCommandSession session = NewSession();

            Assert.Equal(Printed(path), Value(session, "(findfile " + Printed(path) + ")"));
        }

        #endregion

        #region load

        [Theory]
        [InlineData("Sub/x")]
        [InlineData("Sub/x.lsp")]
        [InlineData("./Sub/x.lsp")]
        [InlineData("./Sub/x")]
        public void Load_OfARelativeNameWithAFolder_FollowsTheSupportPath(string name)
        {
            // the parked line of L6: (load "./x.lsp") was taken from the folder of the process
            Make(@"support\Sub\x.lsp", "42");
            CadCommandSession session = NewSession();

            Assert.Equal("42", Value(session, "(load \"" + name + "\")"));
        }

        [Fact]
        public void Load_OfDotSlashAndAName_FollowsTheSupportPath()
        {
            Make(@"support\x.lsp", "42");
            CadCommandSession session = NewSession();

            Assert.Equal("42", Value(session, "(load \"./x.lsp\")"));
            Assert.Equal("42", Value(session, "(load \"./x\")"));
        }

        [Fact]
        public void Load_WithBothFolders_TheSupportFolderWinsAtZero_AndTheCurrentFolderAtOne()
        {
            // AutoCAD 2021: (load "en-us/3d") ran the support folder's file at 0 and the current folder's at 1
            Make(@"support\Sub\x.lsp", "1");
            Make(@"current\Sub\x.lsp", "2");
            CadCommandSession session = NewSession();

            Assert.Equal("1", Value(session, "(load \"Sub/x\")"));
            session.Lisp.Run("(setvar \"LEGACYCODESEARCH\" 1)");
            Assert.Equal("2", Value(session, "(load \"Sub/x\")"));
            Assert.Equal("2", Value(session, "(load \"./Sub/x.lsp\")"));
        }

        [Fact]
        public void Load_OfACodeFileOnlyUnderTheCurrentFolder_FailsAtZero_AndLoadsAtOne()
        {
            // AutoCAD 2021: (load "mysub/mine") LOAD failed at 0 and loaded at 1
            Make(@"current\Sub\mine.lsp", "42");
            CadCommandSession session = NewSession();

            Assert.Equal("; error: LOAD failed: \"Sub/mine\"", Value(session, "(load \"Sub/mine\")"));
            Assert.Equal("; error: LOAD failed: \"./Sub/mine.lsp\"", Value(session, "(load \"./Sub/mine.lsp\")"));
            Assert.Equal("\"fb\"", Value(session, "(load \"Sub/mine\" \"fb\")"));
            session.Lisp.Run("(setvar \"LEGACYCODESEARCH\" 1)");
            Assert.Equal("42", Value(session, "(load \"Sub/mine\")"));
            Assert.Equal("42", Value(session, "(load \"./Sub/mine.lsp\")"));
        }

        [Fact]
        public void Load_OfARefusedName_WithAnExtension_IsCanceledWithTheFullPath()
        {
            // AutoCAD 2021, SECURELOAD 1, LEGACYCODESEARCH 1, an untrusted current folder: "mysub/mine.lsp", "./mysub/mine.lsp" and
            // "../p2/mysub/mine.lsp" gave File load canceled: D:/.../p2/mysub/mine.lsp (the full path, forward slashes); the
            // fallback was not used
            string path = Make(@"current\Sub\mine.lsp", "42");
            CadCommandSession session = NewSession(legacyCodeSearch: 1, secureLoad: 1);
            string canceled = "; error: File load canceled: " + path.Replace('\\', '/');

            Assert.Equal(canceled, Value(session, "(load \"Sub/mine.lsp\")"));
            Assert.Equal(canceled, Value(session, "(load \"./Sub/mine.lsp\")"));
            Assert.Equal(canceled, Value(session, "(load \"../current/Sub/mine.lsp\")"));
            Assert.Equal(canceled, Value(session, "(load \"./Sub/mine.lsp\" \"FB\")"));
        }

        [Fact]
        public void Load_OfARefusedName_WithNoExtension_IsPassedOver_AsABareNameIs()
        {
            // AutoCAD 2021, SECURELOAD 1, LEGACYCODESEARCH 1: (load "mysub/mine") and (load "./mysub/mine") LOAD failed
            Make(@"current\Sub\mine.lsp", "42");
            CadCommandSession session = NewSession(legacyCodeSearch: 1, secureLoad: 1);

            Assert.Equal("; error: LOAD failed: \"Sub/mine\"", Value(session, "(load \"Sub/mine\")"));
            Assert.Equal("; error: LOAD failed: \"./Sub/mine\"", Value(session, "(load \"./Sub/mine\")"));
            Assert.Equal("\"FB\"", Value(session, "(load \"Sub/mine\" \"FB\")"));
        }

        [Fact]
        public void Load_OfARootedPath_IsUnchanged()
        {
            string path = Make(@"elsewhere\y.lsp", "42");
            CadCommandSession session = NewSession();

            Assert.Equal("42", Value(session, "(load \"" + path.Replace('\\', '/') + "\")"));
            Assert.Equal(
                "; error: LOAD failed: \"" + At(@"elsewhere\none.lsp").Replace('\\', '/') + "\"",
                Value(session, "(load \"" + At(@"elsewhere\none.lsp").Replace('\\', '/') + "\")")
            );
        }

        #endregion

        #region load_dialog

        [Fact]
        public void LoadDialog_OfARelativeNameWithAFolder_FollowsTheSupportPath()
        {
            Make(@"support\Sub\kit.dcl", "ghok : dialog { label = \"Only\"; : text { label = \"x\"; } ok_only; }");
            CadCommandSession session = NewSession();

            Assert.Equal("1", Value(session, "(load_dialog \"Sub/kit\")"));
            Assert.Equal("2", Value(session, "(load_dialog \"./Sub/kit.dcl\")"));
        }

        #endregion
    }
}
