using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The question of SECURELOAD 1 as <c>load</c> puts it (decisions D902, D903, D906 and D907 of the L6 spec), against the
    /// full AutoCAD's warning (research G1-G3): the answers, what is asked about, what is never asked about, and the
    /// <see cref="CadLoadSecurity"/> a session holds. The session is given an <see cref="CadLoadSecurity.Ask"/> that records
    /// the paths it is asked about and answers from a list, the last answer repeating. The files are made in a folder of the
    /// test's own under the temporary folder.
    /// </summary>
    [Collection("LispSerial")]
    public sealed class LispLoadSecurityAskTests : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-loadask-" + Guid.NewGuid().ToString("N")
        );

        public LispLoadSecurityAskTests()
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
            File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
            return path;
        }

        // The file of the fixture: sets ghl6u1 to 1 and gives 42.
        private string Untrusted() => Make(@"untrusted\u1.lsp", "(setq ghl6u1 1)\n42\n");

        private string Folder(string name) => Path.Combine(_root, name);

        private static string Slashed(string path) => path.Replace('\\', '/');

        private static string LoadForm(string path) => "(load \"" + Slashed(path) + "\")";

        private static string Canceled(string path) => "; error: File load canceled: " + Slashed(path);

        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        // A session at the level given, whose question records what it is asked and answers from the list.
        private static CadCommandSession Asking(int level, out List<string> asked, params CadLoadAnswer[] answers)
        {
            CadCommandSession session = Session(Blank());
            session.LoadSecurity.SecureLoad = level;
            Answering(session.LoadSecurity, out asked, answers);
            return session;
        }

        private static void Answering(CadLoadSecurity security, out List<string> asked, params CadLoadAnswer[] answers)
        {
            var seen = new List<string>();
            security.Ask = path =>
            {
                seen.Add(path);
                return answers[Math.Min(seen.Count - 1, answers.Length - 1)];
            };
            asked = seen;
        }

        #region The thread the question is asked on

        [Fact]
        public void TheQuestion_AndTheChangeAnAlwaysLoadMakes_ComeOnTheThreadThatTypedTheLoad()
        {
            // ours (L6 wave 3): AutoLISP evaluates on a worker thread of its own while the typing thread waits for it, so a
            // question asked on the worker made the window's Dispatcher.Invoke wait for a thread that was waiting in turn: the
            // app hung on the first untrusted (load) typed at SECURELOAD 1. The question and the Changed of an Always Load are
            // asked on the thread that typed, the owner, as every built-in that touches the session is.
            string path = Untrusted();
            CadCommandSession session = Session(Blank());
            session.LoadSecurity.SecureLoad = 1;
            int owner = Environment.CurrentManagedThreadId;
            var askedOn = new List<int>();
            var changedOn = new List<int>();
            session.LoadSecurity.Ask = p =>
            {
                askedOn.Add(Environment.CurrentManagedThreadId);
                return CadLoadAnswer.AlwaysLoad;
            };
            session.LoadSecurity.Changed += (s, e) => changedOn.Add(Environment.CurrentManagedThreadId);

            Assert.Equal("42", Value(session, LoadForm(path)));
            Assert.Equal(new[] { owner }, askedOn);
            Assert.Equal(new[] { owner }, changedOn);
        }

        #endregion

        #region The security of a session

        [Fact]
        public void ASessionOfTheLibrary_HasItsOwnLoadSecurity_AtSecureLoadZero()
        {
            // ours (D903): never null, one instance for each session, SECURELOAD 0 so that a library user loads as before
            CadCommandSession first = Session(Blank());
            CadCommandSession second = Session(Blank());

            Assert.NotNull(first.LoadSecurity);
            Assert.NotSame(first.LoadSecurity, second.LoadSecurity);
            Assert.Equal(0, first.LoadSecurity.SecureLoad);
            Assert.Null(first.LoadSecurity.Ask);
        }

        [Fact]
        public void TheLoadSecurityOfASession_CannotBeNull()
        {
            CadCommandSession session = Session(Blank());

            Assert.Throws<ArgumentNullException>(() => session.LoadSecurity = null);
            Assert.NotNull(session.LoadSecurity);
        }

        [Fact]
        public void TheVariables_ReadTheLoadSecurityTheSessionIsGiven()
        {
            CadCommandSession session = Session(Blank());
            session.LoadSecurity = new CadLoadSecurity
            {
                SecureLoad = 2,
                TrustedPaths = @"C:\a;C:\b\...",
                LegacyCodeSearch = true,
            };

            Assert.Equal("2", Value(session, "(getvar \"SECURELOAD\")"));
            Assert.Equal("\"C:\\\\a;C:\\\\b\\\\...\"", Value(session, "(getvar \"TRUSTEDPATHS\")"));
            Assert.Equal("1", Value(session, "(getvar \"LEGACYCODESEARCH\")"));
        }

        [Fact]
        public void Setvar_WritesThroughToTheLoadSecurity_AndRaisesItsChangedEvent()
        {
            CadCommandSession session = Session(Blank());
            int raised = 0;
            session.LoadSecurity.Changed += (sender, e) => raised++;

            Value(session, "(setvar \"SECURELOAD\" 1)");
            Value(session, "(setvar \"TRUSTEDPATHS\" \"C:\\\\x\")");
            Value(session, "(setvar \"LEGACYCODESEARCH\" 1)");

            Assert.Equal(1, session.LoadSecurity.SecureLoad);
            Assert.Equal(@"C:\x", session.LoadSecurity.TrustedPaths);
            Assert.True(session.LoadSecurity.LegacyCodeSearch);
            Assert.Equal(3, raised);
        }

        [Fact]
        public void TwoSessionsGivenOneLoadSecurity_ShareItsLevelAndItsAnswers()
        {
            // the window gives every drawing one instance: a Load Once answered in one holds in the other
            string path = Untrusted();
            var shared = new CadLoadSecurity();
            Answering(shared, out List<string> asked, CadLoadAnswer.LoadOnce);
            CadCommandSession first = Session(Blank());
            CadCommandSession second = Session(Blank());
            first.LoadSecurity = shared;
            second.LoadSecurity = shared;

            Assert.Equal("42", Value(first, LoadForm(path)));
            Assert.Equal("42", Value(second, LoadForm(path)));

            Assert.Single(asked);

            Value(second, "(setvar \"SECURELOAD\" 2)");
            Assert.Equal("2", Value(first, "(getvar \"SECURELOAD\")"));
        }

        #endregion

        #region The answers

        [Fact]
        public void LoadOnce_LoadsTheFile_AndTheSecondLoadDoesNotAsk()
        {
            // AutoCAD 2021 (G1): Load Once loads, and the same file loads again without asking in that session
            string path = Untrusted();
            CadCommandSession session = Asking(1, out List<string> asked, CadLoadAnswer.LoadOnce);

            Assert.Equal("42", Value(session, LoadForm(path)));
            Assert.Equal("42", Value(session, LoadForm(path)));

            Assert.Equal(new[] { path }, asked);
            Assert.Equal("1", Value(session, "!ghl6u1"));
        }

        [Fact]
        public void DoNotLoad_IsTheCancel_AndTheNextLoadAsksAgain()
        {
            // AutoCAD 2021 (G1): Do Not Load gives ; error: File load canceled: <name>, also with the fallback argument
            string path = Untrusted();
            CadCommandSession session = Asking(1, out List<string> asked, CadLoadAnswer.DoNotLoad);

            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));
            Assert.Equal(Canceled(path), Value(session, "(load \"" + Slashed(path) + "\" \"fb\")"));

            Assert.Equal(2, asked.Count);
            Assert.Equal("nil", Value(session, "(boundp 'ghl6u1)"));
        }

        [Fact]
        public void AlwaysLoad_LoadsTheFile_KeepsIt_AndAnotherSessionGivenTheListLoadsWithoutAsking()
        {
            // AutoCAD 2021 (G1, G2): Always Load loads, TRUSTEDPATHS unchanged; a new session loads the file with no warning
            string path = Untrusted();
            CadCommandSession first = Asking(1, out List<string> asked, CadLoadAnswer.AlwaysLoad);

            Assert.Equal("42", Value(first, LoadForm(path)));
            Assert.Equal("42", Value(first, LoadForm(path)));

            Assert.Single(asked);
            Assert.Single(first.LoadSecurity.AlwaysLoaded);
            Assert.Equal("\"\"", Value(first, "(getvar \"TRUSTEDPATHS\")"));

            CadCommandSession second = Session(Blank());
            second.LoadSecurity.SecureLoad = 1;
            second.LoadSecurity.AlwaysLoaded = first.LoadSecurity.AlwaysLoaded;
            second.LoadSecurity.Ask = p => throw new InvalidOperationException("asked about " + p);

            Assert.Equal("42", Value(second, LoadForm(path)));
        }

        [Fact]
        public void AnAlwaysLoadFile_UnderSecureLoadTwo_IsCanceled()
        {
            // AutoCAD 2021 (G3): the Always Load file under SECURELOAD 2: refused
            string path = Untrusted();
            CadCommandSession session = Asking(1, out List<string> asked, CadLoadAnswer.AlwaysLoad);
            Value(session, LoadForm(path));

            Value(session, "(setvar \"SECURELOAD\" 2)");

            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));
            Assert.Single(asked);
        }

        [Fact]
        public void AnAlwaysLoadFile_ThatWasEdited_IsAskedAgain()
        {
            // AutoCAD 2021 (G3): after one comment line was appended it was asked again
            string path = Untrusted();
            CadCommandSession session = Asking(1, out List<string> asked, CadLoadAnswer.AlwaysLoad);
            Value(session, LoadForm(path));

            File.AppendAllText(path, "; one comment\n");
            session.LoadSecurity.Ask = p =>
            {
                asked.Add(p);
                return CadLoadAnswer.DoNotLoad;
            };

            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));
            Assert.Equal(2, asked.Count);
        }

        [Fact]
        public void AnAskThatThrows_IsTheCancel_AndTheSessionGoesOn()
        {
            string path = Untrusted();
            CadCommandSession session = Session(Blank());
            session.LoadSecurity.SecureLoad = 1;
            session.LoadSecurity.Ask = p => throw new InvalidOperationException("no window");

            Assert.Equal(Canceled(path), Value(session, LoadForm(path)));
            Assert.Equal("3", Value(session, "(+ 1 2)"));
        }

        #endregion

        #region What is asked about

        [Fact]
        public void NothingIsAsked_ForAFileInATrustedFolder_AtLevelZeroOrTwo_OrForAMissingFile()
        {
            Make(@"trusted\t1.lsp", "42");
            string untrusted = Untrusted();
            CadCommandSession session = Asking(1, out List<string> asked, CadLoadAnswer.LoadOnce);
            Value(session, "(setvar \"TRUSTEDPATHS\" \"" + Folder("trusted").Replace("\\", "\\\\") + "\")");

            Value(session, LoadForm(Path.Combine(Folder("trusted"), "t1.lsp")));
            Value(session, LoadForm(Folder(@"untrusted\nothere.lsp")));
            Value(session, "(setvar \"SECURELOAD\" 0)");
            Value(session, LoadForm(untrusted));
            Value(session, "(setvar \"SECURELOAD\" 2)");
            Value(session, LoadForm(untrusted));

            Assert.Empty(asked);
        }

        [Fact]
        public void AFileFoundByASearch_IsAskedAboutWithItsFullPath()
        {
            // AutoCAD 2021 (G2): a bare name found beside the drawing is asked with the file found, the extension added
            string path = Untrusted();
            CadCommandSession session = Asking(1, out List<string> asked, CadLoadAnswer.LoadOnce);
            session.Document.SupportFolders = new[] { Folder("untrusted") };

            Assert.Equal("42", Value(session, "(load \"u1\")"));

            Assert.Equal(new[] { path }, asked);
        }

        [Fact]
        public void ARefusedFileOfTheSearch_IsPassedOver_AndTheNextFolderIsAskedAbout()
        {
            // research D907: the search goes on after a refusal, and the next file is asked about in its turn
            string first = Make(@"a\same.lsp", "(setq ghl6which 'a)");
            string second = Make(@"b\same.lsp", "(setq ghl6which 'b)");
            CadCommandSession session = Asking(
                1,
                out List<string> asked,
                CadLoadAnswer.DoNotLoad,
                CadLoadAnswer.LoadOnce
            );
            session.Document.SupportFolders = new[] { Folder("a"), Folder("b") };

            Value(session, "(load \"same\")");

            Assert.Equal(new[] { first, second }, asked);
            Assert.Equal("B", Value(session, "!ghl6which"));
        }

        [Fact]
        public void AFileBesideTheDrawing_FoundWithLegacyCodeSearch_IsAskedAbout_AndLoadsOnLoadOnce()
        {
            // AutoCAD 2021 (G2): LEGACYCODESEARCH 1, the bare name found beside the drawing is asked
            string drawings = Folder("plans");
            string path = Make(@"plans\ghl6leg.lsp", "(setq ghl6leg 7)\n42\n");
            CadDocument document = Blank();
            document.SaveAs(Path.Combine(drawings, "plan.dxf"));
            CadCommandSession session = Session(document);
            session.LoadSecurity.SecureLoad = 1;
            session.LoadSecurity.LegacyCodeSearch = true;
            Answering(session.LoadSecurity, out List<string> asked, CadLoadAnswer.LoadOnce);

            Assert.Equal("42", Value(session, "(load \"ghl6leg\")"));

            Assert.Equal(new[] { path }, asked);
        }

        [Fact]
        public void AppLoad_AsksOnce_ForALoadOnce_AndSaysLoadedEachTime()
        {
            string path = Untrusted();
            CadCommandSession session = Asking(1, out List<string> asked, CadLoadAnswer.LoadOnce);

            Assert.True(session.LoadLisp(path));
            Assert.True(session.LoadLisp(path));

            Assert.Single(asked);
            Assert.Equal(2, session.History.Count(l => l == "u1.lsp successfully loaded."));
        }

        [Fact]
        public void AppLoad_OfDoNotLoad_WritesTheCancel_AndReturnsFalse()
        {
            string path = Untrusted();
            CadCommandSession session = Asking(1, out List<string> asked, CadLoadAnswer.DoNotLoad);

            Assert.False(session.LoadLisp(path));

            Assert.Equal("; error: File load canceled: " + path, session.History.Last());
            Assert.Single(asked);
        }

        #endregion
    }
}
