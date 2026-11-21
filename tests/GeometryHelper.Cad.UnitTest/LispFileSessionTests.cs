using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What the files functions of AutoLISP take from the session (decisions D111, D113, D123 and D129 of the L3 spec):
    /// <c>getfiled</c> through the window's file dialog, <c>read-line</c> and <c>read-char</c> from the keyboard as the line of
    /// the command line, the folder of a drawing <c>findfile</c> looks in, and the files a routine left open closed with its
    /// drawing. Every file is made in a folder of the test's own under the temporary folder.
    /// </summary>
    public sealed class LispFileSessionTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-filesession-" + Guid.NewGuid().ToString("N")
        );

        public LispFileSessionTests()
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

        // The history without the trailing spaces of each line.
        private static string[] Lines(CadCommandSession session) => session.History.Select(l => l.TrimEnd()).ToArray();

        // A path as prin1 writes it.
        private static string Printed(string path) => "\"" + path.Replace("\\", "\\\\") + "\"";

        // A path as a routine writes it in a string.
        private static string Slashed(string path) => path.Replace('\\', '/');

        #region getfiled

        [Fact]
        public void Getfiled_WithNoWindow_GivesNil()
        {
            // AutoCAD 2021 (r6): the console shows no dialog and (getfiled "Pick a file" "" "txt" 0) gives nil
            CadCommandSession session = Run(Session(Blank()), "(getfiled \"Pick a file\" \"\" \"txt\" 0)");

            Assert.Equal(new[] { "Command: (getfiled \"Pick a file\" \"\" \"txt\" 0)", "nil" }, Lines(session));
        }

        [Fact]
        public void Getfiled_WithAWindowThatAnswersAPath_GivesItAndHandsOverWhatWasAsked()
        {
            CadCommandSession session = Session(Blank());
            var seen = new List<CadUiRequestEventArgs>();
            CadGetFileRequest asked = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "GETFILED")
                {
                    asked = e.GetFile;
                    seen.Add(e);
                    e.Answer = "D:\\Plans\\site.dwg";
                    e.Handled = true;
                }
            };

            Run(session, "(getfiled \"Select a plan\" \"D:/Plans/old.dwg\" \"dwg;dxf\" 1)");

            Assert.Equal(Printed("D:\\Plans\\site.dwg"), Lines(session).Last());
            Assert.Single(seen);
            Assert.Null(seen[0].Plot);
            Assert.NotNull(asked);
            Assert.Equal("Select a plan", asked.Title);
            Assert.Equal("D:/Plans/old.dwg", asked.DefaultName);
            Assert.Equal("dwg;dxf", asked.Extension);
            Assert.Equal(1, asked.Flags);
        }

        [Fact]
        public void Getfiled_WithAWindowThatLeavesTheAnswerNull_GivesNil()
        {
            CadCommandSession session = Session(Blank());
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "GETFILED")
                {
                    e.Handled = true;
                }
            };

            Run(session, "(getfiled \"Open\" \"\" \"txt\" 0)");

            Assert.Equal("nil", Lines(session).Last());
        }

        [Fact]
        public void Getfiled_WithAnAnswerThatNobodyHandled_GivesNil()
        {
            CadCommandSession session = Session(Blank());
            session.UiRequested += (sender, e) => e.Answer = "C:\\x.txt";

            Run(session, "(getfiled \"Open\" \"\" \"txt\" 0)");

            Assert.Equal("nil", Lines(session).Last());
        }

        [Theory]
        [InlineData("(getfiled 5 \"\" \"txt\" 0)", "; error: bad argument type: stringp 5")]
        [InlineData("(getfiled \"T\" 5 \"txt\" 0)", "; error: bad argument type: stringp 5")]
        [InlineData("(getfiled \"T\" \"\" nil 0)", "; error: bad argument type: stringp nil")]
        [InlineData("(getfiled \"T\" \"\" \"txt\" \"0\")", "; error: bad argument type: fixnump: \"0\"")]
        [InlineData("(getfiled \"T\" \"\" \"txt\")", "; error: too few arguments")]
        public void Getfiled_WithAnArgumentOfTheWrongType_IsAnErrorBeforeTheWindowIsAsked(string form, string error)
        {
            CadCommandSession session = Session(Blank());
            int asked = 0;
            session.UiRequested += (sender, e) => asked++;

            Run(session, form);

            Assert.Equal(error, Lines(session).Last());
            Assert.Equal(0, asked);
        }

        #endregion

        #region read-line and read-char

        [Fact]
        public void ReadLine_AsksForALine_AndGivesItWithItsSpaces()
        {
            // AutoCAD 2021 (r7): Command: (read-line) / hello world / "hello world"
            CadCommandSession session = Run(Session(Blank()), "(read-line)");

            Assert.True(session.IsLispWaiting);

            Run(session, "hello world");

            Assert.Equal(new[] { "Command: (read-line)", "hello world", "\"hello world\"" }, Lines(session));
            Assert.False(session.IsLispWaiting);
        }

        [Fact]
        public void TheLineAsked_TakesSpaces_AndShowsNoPromptText()
        {
            CadCommandSession session = Run(Session(Blank()), "(read-line)");

            Assert.True(session.Prompt.TakesSpaces);
            Assert.Equal(string.Empty, session.PromptText);

            Run(session, Esc);
        }

        [Fact]
        public void ReadLine_TakesATextThatStartsWithAParenthesisAsText()
        {
            // AutoCAD 2021 (r7): (setq x (read-line)) then (1 2) gives "(1 2)", with no Can't reenter LISP.
            CadCommandSession session = Run(Session(Blank()), "(setq x (read-line))", "(1 2)");

            Assert.Equal(new[] { "Command: (setq x (read-line))", "(1 2)", "\"(1 2)\"" }, Lines(session));

            Run(session, "!x");

            Assert.Equal("\"(1 2)\"", Lines(session).Last());
        }

        [Fact]
        public void ReadLine_TakesATextThatStartsWithABangAsTextToo()
        {
            // ours: D111, as a parenthesis: the line is not LISP while the routine reads the keyboard
            CadCommandSession session = Run(Session(Blank()), "(read-line)", "!x and (y)");

            Assert.Equal(new[] { "Command: (read-line)", "!x and (y)", "\"!x and (y)\"" }, Lines(session));
        }

        [Fact]
        public void ReadLine_WithEnterAlone_GivesTheEmptyString()
        {
            CadCommandSession session = Run(Session(Blank()), "(read-line)", Enter);

            Assert.Equal("\"\"", Lines(session).Last());
            Assert.False(session.IsLispWaiting);
        }

        [Fact]
        public void ReadChar_GivesTheCodes_ThenTheEndOfTheLineAndTheRestWithoutAsking()
        {
            // AutoCAD 2021 (r7): (read-char) then ab gives 97; the next (read-char) 98 with no question; then (read-line) ""
            CadCommandSession session = Run(Session(Blank()), "(read-char)", "ab");

            Assert.Equal(new[] { "Command: (read-char)", "ab", "97" }, Lines(session));

            Run(session, "(read-char)");

            Assert.False(session.IsLispWaiting);
            Assert.Equal("98", Lines(session).Last());

            Run(session, "(read-line)");

            Assert.False(session.IsLispWaiting);
            Assert.Equal("\"\"", Lines(session).Last());

            // the line is used up: the next read asks again
            Run(session, "(read-char)");

            Assert.True(session.IsLispWaiting);

            Run(session, Esc);
        }

        [Fact]
        public void ReadLine_PressedWithEsc_IsFunctionCancelled()
        {
            CadCommandSession session = Run(Session(Blank()), "(read-line)", Esc);

            Assert.Equal("; error: Function cancelled", Lines(session).Last());
            Assert.False(session.IsLispWaiting);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ReadLine_AfterInitget_LeavesTheBitsForTheNextGetFunction()
        {
            // ours: D111, the keyboard is no get function: initget waits for the next one
            CadCommandSession session = Run(Session(Blank()), "(initget 1)", "(read-line)", "text");

            Run(session, "(getint)", Enter);

            Assert.Equal("Requires an integer value.", Lines(session).Last());

            Run(session, Esc);
        }

        [Fact]
        public void ReadLineWithAFile_AsksNoOne()
        {
            string path = Path.Combine(_folder, "two.txt");
            File.WriteAllText(path, "one\ntwo\n");
            CadCommandSession session = Session(Blank());

            Run(session, "(setq f (open \"" + Slashed(path) + "\" \"r\"))", "(read-line f)");

            Assert.False(session.IsLispWaiting);
            Assert.Equal("\"one\"", Lines(session).Last());

            Run(session, "(close f)");
        }

        #endregion

        #region The drawing's folder

        [Fact]
        public void Findfile_OfABareName_LooksBesideADrawingThatWasSaved()
        {
            // AutoCAD 2021 (r4): a bare name is looked for in the current folder, then in the drawing's folder
            string drawings = Path.Combine(_folder, "plans");
            string elsewhere = Path.Combine(_folder, "elsewhere");
            Directory.CreateDirectory(drawings);
            Directory.CreateDirectory(elsewhere);
            string beside = Path.Combine(drawings, "beside.txt");
            File.WriteAllText(beside, "x");
            CadDocument document = Blank();
            document.SaveAs(Path.Combine(drawings, "plan.dxf"));
            CadCommandSession session = Session(document);
            session.Lisp.CurrentFolder = () => elsewhere;

            Run(session, "(findfile \"beside.txt\")");

            Assert.Equal(Printed(beside), Lines(session).Last());
        }

        [Fact]
        public void Findfile_OfABareName_DoesNotLookBesideADrawingNeverSaved()
        {
            string elsewhere = Path.Combine(_folder, "elsewhere");
            Directory.CreateDirectory(elsewhere);
            CadCommandSession session = Session(Blank());
            session.Lisp.CurrentFolder = () => elsewhere;

            // ours: D113, DWGPREFIX of such a drawing is the documents folder, which findfile does not search
            Run(session, "(findfile \"beside.txt\")");

            Assert.Equal("nil", Lines(session).Last());
        }

        [Fact]
        public void Findfile_PrefersTheCurrentFolderToTheDrawingsFolder()
        {
            // AutoCAD 2021 (r4, r5): the current folder wins
            string drawings = Path.Combine(_folder, "plans");
            string current = Path.Combine(_folder, "current");
            Directory.CreateDirectory(drawings);
            Directory.CreateDirectory(current);
            File.WriteAllText(Path.Combine(drawings, "both.txt"), "drawing");
            string expected = Path.Combine(current, "both.txt");
            File.WriteAllText(expected, "current");
            CadDocument document = Blank();
            document.SaveAs(Path.Combine(drawings, "plan.dxf"));
            CadCommandSession session = Session(document);
            session.Lisp.CurrentFolder = () => current;

            Run(session, "(findfile \"both.txt\")");

            Assert.Equal(Printed(expected), Lines(session).Last());
        }

        #endregion

        #region Files left open

        [Fact]
        public void CloseLispFiles_ClosesAFileTheRoutineLeftOpen_SoItCanBeDeleted()
        {
            string path = Path.Combine(_folder, "left-open.txt");
            CadCommandSession session = Session(Blank());
            Run(session, "(setq f (open \"" + Slashed(path) + "\" \"w\"))", "(write-line \"kept\" f)");

            // the name is locked while the routine holds the file
            Assert.ThrowsAny<IOException>(() => File.Delete(path));

            session.CloseLispFiles();

            Assert.Equal("kept\r\n", File.ReadAllText(path));
            File.Delete(path);
            Assert.False(File.Exists(path));
            Assert.Equal("#<FILE internal>", ValueOf(session, "f"));
        }

        [Fact]
        public void CloseLispFiles_LeavesAlone_AFileTheRoutineClosed()
        {
            string path = Path.Combine(_folder, "closed.txt");
            CadCommandSession session = Session(Blank());
            Run(session, "(setq f (open \"" + Slashed(path) + "\" \"w\"))", "(write-line \"a\" f)", "(close f)");

            session.CloseLispFiles();

            Assert.Equal("a\r\n", File.ReadAllText(path));
        }

        [Fact]
        public void CloseLispFiles_WhereNoLispRan_MakesNoInterpreterAndDoesNothing()
        {
            CadCommandSession session = Session(Blank());

            session.CloseLispFiles();
            session.CloseLispFiles();

            Assert.Empty(session.LoadedLispFiles);
        }

        [Fact]
        public void TheInterpreter_ClosesEveryFileOpenedInOrder_AndForgetsThem()
        {
            string one = Path.Combine(_folder, "one.txt");
            string two = Path.Combine(_folder, "two.txt");
            var lisp = new LispInterpreter();
            lisp.Run("(setq a (open \"" + Slashed(one) + "\" \"w\"))");
            lisp.Run("(setq b (open \"" + Slashed(two) + "\" \"w\"))");
            lisp.Run("(close a)");

            lisp.CloseFiles();
            lisp.CloseFiles();

            File.Delete(one);
            File.Delete(two);
            Assert.False(File.Exists(one));
            Assert.False(File.Exists(two));
        }

        // What a symbol holds, as the form !name prints it.
        private static string ValueOf(CadCommandSession session, string name)
        {
            Run(session, "!" + name);
            return Lines(session).Last();
        }

        #endregion
    }
}
