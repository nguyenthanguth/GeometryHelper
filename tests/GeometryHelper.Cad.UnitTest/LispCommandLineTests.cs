using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// AutoLISP typed at the command line, as AutoCAD 2021's accoreconsole wrote it (probes p1 to p4): a form or a
    /// <c>!</c> at the Command prompt and at a command's prompt, the <c>(_&gt;</c> prompt of a form left open, the <c>c:</c>
    /// commands and where they come among the names and aliases, <c>load</c> through the document's support folders and what
    /// APPLOAD writes.
    /// </summary>
    public sealed class LispCommandLineTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-line-" + Guid.NewGuid().ToString("N")
        );

        public LispCommandLineTests()
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

        private static CadCommandSession Typed(params object[] inputs) => Run(Session(Blank()), inputs);

        private static string[] After(CadCommandSession session, int skip) => session.History.Skip(skip).ToArray();

        [Fact]
        public void AFormAtTheCommandPrompt_IsEchoedAndItsValueWrittenBelow()
        {
            // AutoCAD 2021: Command: (setq a 1) / 1
            CadCommandSession session = Typed("(setq a 1)");

            Assert.Equal(new[] { "Command: (setq a 1)", "1" }, session.History);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void TextWrittenOverTwoLines_StartsEachLineAfterItsNewLine()
        {
            // AutoCAD 2021: (princ "a\nb") gives the lines a and b"a\nb"
            CadCommandSession session = Typed("(princ \"a\\nb\")");

            Assert.Equal(new[] { "Command: (princ \"a\\nb\")", "a", "b\"a\\nb\"" }, session.History);
        }

        [Fact]
        public void AFormThatGivesTheEmptySymbol_WritesNothingAfterItsEcho()
        {
            // AutoCAD 2021: Command: (princ) with nothing below it
            Assert.Equal(new[] { "Command: (princ)" }, Typed("(princ)").History);
        }

        [Fact]
        public void ANewLineWrittenOnItsOwn_IsABlankLineBeforeTheValue()
        {
            // AutoCAD 2021: (terpri) gives a blank line and then nil
            Assert.Equal(new[] { "Command: (terpri)", "", "nil" }, Typed("(terpri)").History);
        }

        [Fact]
        public void AnErrorAtTheCommandPrompt_IsTheErrorLineAndTheLineAfterAPartialOutputIsHerOwn()
        {
            // AutoCAD 2021: x; error: bad argument type: consp 1
            CadCommandSession session = Typed("(progn (princ \"x\") (car 1))", "(car 1)");

            Assert.Equal(
                new[]
                {
                    "Command: (progn (princ \"x\") (car 1))",
                    "x; error: bad argument type: consp 1",
                    "Command: (car 1)",
                    "; error: bad argument type: consp 1",
                },
                session.History
            );
        }

        [Fact]
        public void AFormWithAParenthesisOpen_WaitsUnderTheContinuationPrompt()
        {
            // AutoCAD 2021: Command: (setq q / (_> "multi") / "multi"
            CadCommandSession session = Typed("(setq q");

            Assert.Equal("(_>", session.PromptText);
            Assert.False(session.IsRunning);
            Assert.True(session.IsLispInput("anything"));

            Run(session, "\"multi\")");

            Assert.Equal(new[] { "Command: (setq q", "(_> \"multi\")", "\"multi\"" }, session.History);
            Assert.Equal(string.Empty, session.PromptText);
            Assert.False(session.IsLispInput("LINE"));
        }

        [Fact]
        public void TheContinuationPromptHasOneParenthesisForEachOneOpen()
        {
            // AutoCAD 2021: Command: (setq w (list / ((_> 1 2 / ((_> ) / (_> ) / (1 2)
            CadCommandSession session = Typed("(setq w (list", "1 2", ")");

            Assert.Equal("(_>", session.PromptText);

            Run(session, ")");

            Assert.Equal(new[] { "Command: (setq w (list", "((_> 1 2", "((_> )", "(_> )", "(1 2)" }, session.History);
        }

        [Fact]
        public void AStringLeftOpen_ShowsAQuoteInThePromptAndKeepsItsNewLine()
        {
            // AutoCAD 2021: Command: (setq s "x / ("_> y") / "x\ny"
            CadCommandSession session = Typed("(setq s \"x");

            Assert.Equal("(\"_>", session.PromptText);

            Run(session, " y\")");

            Assert.Equal(new[] { "Command: (setq s \"x", "(\"_>  y\")", "\"x\\n y\"" }, session.History);
        }

        [Fact]
        public void EnterWhileAFormWaits_IsAnEmptyLineOfItsText()
        {
            CadCommandSession session = Typed("(princ \"a");
            Run(session, Enter, "b\")");

            Assert.Equal(
                new[] { "Command: (princ \"a", "(\"_>", "(\"_> b\")", "a", "", "b\"a\\n\\nb\"" },
                session.History
            );
        }

        [Fact]
        public void EscapeWhileAFormWaits_DropsItAndWritesCancelAfterItsPromptAndTheErrorFunctionCancelled()
        {
            // AutoCAD 2021: Command: (setq q / (_> *Cancel* / ; error: Function cancelled
            CadCommandSession session = Typed("(setq q 1)", "(setq q");
            Run(session, Esc);

            Assert.Equal(
                new[] { "(_> *Cancel*", "; error: Function cancelled" },
                session.History.Skip(session.History.Count - 2)
            );
            Assert.Equal(string.Empty, session.PromptText);
            Assert.False(session.IsLispInput("LINE"));

            Run(session, "!q");
            Assert.Equal("1", session.History[session.History.Count - 1]);
        }

        [Fact]
        public void EscapeWhileAFormWaits_CallsTheErrorFunctionOfTheRoutineWithFunctionCancelled()
        {
            CadCommandSession session = Typed("(defun *error* (m) (princ (strcat \"E:\" m)) (princ))", "(setq q", Esc);

            Assert.Equal(
                new[] { "(_> *Cancel*", "E:Function cancelled" },
                session.History.Skip(session.History.Count - 2)
            );
        }

        [Fact]
        public void EscapeWhileAFormWaitsAtAPrompt_DropsTheFormAndTheCommandKeepsAsking()
        {
            // AutoCAD 2021: Specify next point or [Undo]: (setq r / (_> *Cancel* / ; error: Function cancelled, and LINE asks again;
            // the string a later form gives is no point there: Point or option keyword required.
            CadCommandSession session = Typed("LINE", "0,0", "(setq r", Esc);

            Assert.True(session.IsRunning);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);

            Run(session, "(princ \"in-line\")");

            Assert.Equal(
                new[]
                {
                    "Specify next point or [Undo]: (setq r",
                    "(_> *Cancel*",
                    "; error: Function cancelled",
                    "Specify next point or [Undo]: (princ \"in-line\")",
                    "in-line\"in-line\"",
                    "Point or option keyword required.",
                },
                session.History.Skip(2)
            );
            Assert.True(session.IsRunning);
        }

        [Fact]
        public void ABangAndAVariable_WritesTheVariablesValue()
        {
            // AutoCAD 2021: !p (0 0), !pi 3.14159, !nosuch nil, !(+ 1 2) 3, !"abc" "abc"
            CadCommandSession session = Typed("(setq p '(0 0))", "!p", "!pi", "!nosuch", "!(+ 1 2)", "!\"abc\"");

            Assert.Equal(
                new[]
                {
                    "Command: (setq p '(0 0))",
                    "(0 0)",
                    "Command: !p",
                    "(0 0)",
                    "Command: !pi",
                    "3.14159",
                    "Command: !nosuch",
                    "nil",
                    "Command: !(+ 1 2)",
                    "3",
                    "Command: !\"abc\"",
                    "\"abc\"",
                },
                session.History
            );
        }

        [Fact]
        public void ABareNumberOrStringAtTheCommandPrompt_IsACommandNameAndNotAutoLisp()
        {
            // AutoCAD 2021: "a\qb" at the Command prompt is Unknown command; 1 ran LAYISO by alias.
            CadCommandSession session = Typed("\"abc\"");

            Assert.StartsWith("Unknown command", session.History[1]);
            Assert.False(session.IsLispInput("1"));
            Assert.False(session.IsLispInput("\"abc\""));
            Assert.False(session.IsLispInput(""));
            Assert.True(session.IsLispInput("  (+ 1 2)"));
            Assert.True(session.IsLispInput("!p"));
        }

        [Fact]
        public void LineAnsweredWithABangAndAVariable_TakesTheListAsAPoint()
        {
            // AutoCAD 2021: Specify first point: !p / (0 0) / Specify next point or [Undo]: 10,0
            CadCommandSession session = Typed("(setq p '(0 0))", "LINE", "!p", "10,0");

            Assert.Contains("Specify first point: !p", session.History);
            Assert.Equal("(0 0)", session.History[session.History.ToList().IndexOf("Specify first point: !p") + 1]);
            Run(session, Enter);

            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Near(new GeoPoint3(0, 0, 0), line.StartPoint);
            Near(new GeoPoint3(10, 0, 0), line.EndPoint);
        }

        [Fact]
        public void LineAnsweredWithLists_MakesALineFromTheirNumbers()
        {
            // AutoCAD 2021: Specify first point: (list 0 0) / (0 0) / Specify next point or [Undo]: (list 10 0) / (10 0)
            CadCommandSession session = Typed("LINE", "(list 0 0)", "(list 10 0)");

            Assert.Equal(
                new[]
                {
                    "Command: LINE",
                    "Specify first point: (list 0 0)",
                    "(0 0)",
                    "Specify next point or [Undo]: (list 10 0)",
                    "(10 0)",
                },
                session.History
            );
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);

            Run(session, Enter);

            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Near(new GeoPoint3(0, 0, 0), line.StartPoint);
            Near(new GeoPoint3(10, 0, 0), line.EndPoint);
        }

        [Fact]
        public void ANumberAtLinesNextPoint_IsADirectDistanceTowardsTheCursor()
        {
            // AutoCAD 2021: (setq zz 5) at LINE's next point was a direct distance.
            CadCommandSession session = Session(Blank());
            session.Cursor = new GeoPoint2(100, 0);
            Run(session, "LINE", "0,0", "(setq zz 5)", Enter);

            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Near(new GeoPoint3(5, 0, 0), line.EndPoint);
        }

        [Fact]
        public void NilAtAPrompt_AsksTheSamePromptAgain()
        {
            CadCommandSession session = Typed("LINE", "(setq nn nil)");

            Assert.True(session.IsRunning);
            Assert.Equal("Specify first point:", session.PromptText);
            Assert.Equal(new[] { "Command: LINE", "Specify first point: (setq nn nil)", "nil" }, session.History);
        }

        [Fact]
        public void AnErrorAtAPrompt_IsWrittenAndTheCommandKeepsAsking()
        {
            CadCommandSession session = Typed("LINE", "0,0", "(car 1)");

            Assert.True(session.IsRunning);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Assert.Equal("; error: bad argument type: consp 1", session.History[session.History.Count - 1]);
        }

        [Fact]
        public void AFormAtAPromptForText_IsTheTextAnswered()
        {
            CadCommandSession session = Typed("TEXT", "0,0", "2.5", "0");

            Assert.Equal(CadPromptKind.String, session.Prompt.Kind);
            Run(session, "(not lisp)");
            Assert.Equal("(not lisp)", Assert.Single(All<DxfDBText>(session.Document.ModelSpace)).TextString);
        }

        [Fact]
        public void ACommandMadeByDefun_RunsWhenItsNameIsTypedAndWritesItsOutputAndValue()
        {
            // AutoCAD 2021: Command: GHTST / hi5 (the value 5 follows what it wrote)
            CadCommandSession session = Typed("(defun c:ghtst () (princ \"hi\") 5)", "GHTST");

            Assert.Equal(
                new[] { "Command: (defun c:ghtst () (princ \"hi\") 5)", "C:GHTST", "Command: GHTST", "hi5" },
                session.History
            );
            Assert.False(session.IsRunning);
            Assert.Equal("GHTST", session.LastCommandName);
            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
        }

        [Fact]
        public void EnterAfterACommandMadeByDefun_RunsItAgain()
        {
            CadCommandSession session = Typed("(defun c:ghtst () (princ \"hi\") (princ))", "GHTST", Enter);

            Assert.Equal(new[] { "Command: GHTST", "hi", "Command: GHTST", "hi" }, After(session, 2));
        }

        [Fact]
        public void ACommandMadeByDefun_IsTypedInEitherCaseAndEchoedAsTyped()
        {
            // AutoCAD 2021: Command: ghmixed
            CadCommandSession session = Typed("(defun c:GhMixed () (princ \"m\") (princ))", "ghmixed");

            Assert.Equal(new[] { "Command: ghmixed", "m" }, After(session, 2));
        }

        [Fact]
        public void ACommandMadeByDefun_AppearsInTheCommandNamesAndNotBefore()
        {
            CadCommandSession session = Session(Blank());
            Assert.Empty(session.LispCommandNames);

            Run(session, "(defun c:ghb () 1)", "(defun c:gha () 1)", "(defun ghc () 1)", "(setq c:ghd (lambda () 1))");

            Assert.Equal(new[] { "GHA", "GHB" }, session.LispCommandNames);
        }

        [Fact]
        public void AFunctionMadeByDefunWithNoCPrefixAndALambdaSetToACName_AreNotCommands()
        {
            // AutoCAD 2021: Unknown command for (defun ghz ...) and for (setq c:ghy (lambda ...))
            CadCommandSession session = Typed("(defun ghz () 1)", "(setq c:ghy (lambda () 1))", "GHZ", "GHY");

            Assert.Equal(2, session.History.Count(h => h.StartsWith("Unknown command", StringComparison.Ordinal)));
        }

        [Fact]
        public void ACommandMadeByDefun_BeatsAnAliasOfTheSameName()
        {
            // AutoCAD 2021: (defun c:l () ...) then L runs the function, not LINE
            CadCommandSession session = Typed("(defun c:l () (princ \"lisp-l\") (princ))", "L");

            Assert.Equal(new[] { "Command: L", "lisp-l" }, After(session, 2));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ACommandMadeByDefun_BeatsTheNameOfASystemVariable()
        {
            // AutoCAD 2021: (defun c:osmode () ...) then OSMODE runs the function
            CadCommandSession session = Typed("(defun c:osmode () (princ \"lisp-osmode\") (princ))", "OSMODE");

            Assert.Equal(new[] { "Command: OSMODE", "lisp-osmode" }, After(session, 2));
        }

        [Theory]
        [InlineData("LINE", "Specify first point:")]
        [InlineData("line", "Specify first point:")]
        [InlineData("CIRCLE", "Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:")]
        public void ABuiltInCommandName_BeatsACommandMadeByDefunOfTheSameName(string typed, string prompt)
        {
            // AutoCAD 2021: (defun c:line ...) then LINE asks for its first point; c:circle loses to CIRCLE too (and c:zoom to ZOOM).
            CadCommandSession session = Typed(
                "(defun c:line () (princ \"mine\") (princ))",
                "(defun c:circle () (princ \"mine\") (princ))",
                typed
            );

            Assert.True(session.IsRunning);
            Assert.DoesNotContain("mine", session.History);

            Assert.Equal(prompt, session.PromptText);
        }

        [Fact]
        public void ACommandMadeByDefunThatTakesAnArgument_GivesTooFewArguments()
        {
            // AutoCAD 2021: a c: with arguments gives ; error: too few arguments
            CadCommandSession session = Typed("(defun c:gharg (x) x)", "GHARG");

            Assert.Equal("; error: too few arguments", session.History[session.History.Count - 1]);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ACommandMadeByDefunWithItsOwnErrorFunction_SeesTheErrorAndItsLocals()
        {
            // AutoCAD 2021: GHE of p2 writes cmd-err:bad argument type: consp 1
            CadCommandSession session = Typed(
                "(defun c:ghe () (defun *error* (m) (princ \"cmd-err:\") (princ m) (princ)) (car 1))",
                "GHE"
            );

            Assert.Equal(new[] { "Command: GHE", "cmd-err:bad argument type: consp 1" }, After(session, 2));
        }

        [Fact]
        public void UAfterACommandMadeByDefunThatChangesNothing_UndoesTheCommandBeforeIt()
        {
            // AutoCAD 2021: a c: run that changes nothing makes no undo step; U undid the LINE before it.
            CadCommandSession session = Typed("LINE", "0,0", "10,0", Enter);
            Assert.Single(All<DxfLine>(session.Document.ModelSpace));

            Run(session, "(defun c:ghnop () (princ \"nop\") (princ))", "GHNOP", "U");

            Assert.Empty(All<DxfLine>(session.Document.ModelSpace));
        }

        [Fact]
        public void TheOtherDocumentsSession_DoesNotSeeWhatTheFirstDefined()
        {
            // AutoCAD 2021: LISPINIT 1, each document its own namespace.
            CadCommandSession first = Typed("(setq a 1)", "(defun c:ghown () 1)");
            CadCommandSession second = Typed("!a", "GHOWN");

            Assert.Equal(new[] { "Command: !a", "nil", "Command: GHOWN" }, second.History.Take(3));
            Assert.StartsWith("Unknown command", second.History[3]);
            Assert.Empty(second.LispCommandNames);
            Assert.Equal(new[] { "GHOWN" }, first.LispCommandNames);
        }

        [Fact]
        public void LoadOfAName_SearchesTheDocumentsSupportFoldersAndGivesTheLastValue()
        {
            // AutoCAD 2021: (load "name") finds the file in the support folders and gives the value of its last form.
            File.WriteAllText(
                Path.Combine(_folder, "ghl.lsp"),
                "(defun c:ghl () (princ \"L\") (princ))\n42\n",
                new UTF8Encoding(false)
            );
            CadCommandSession session = Session(Blank());
            session.Document.SupportFolders = new[] { _folder };

            Run(session, "(load \"ghl\")", "GHL");

            Assert.Equal(new[] { "Command: (load \"ghl\")", "42", "Command: GHL", "L" }, session.History);
        }

        [Fact]
        public void LoadOfAFileNotInTheSupportFolders_IsLoadFailed()
        {
            // AutoCAD 2021: ; error: LOAD failed: "ghl1e" (the folder of the drawing is not searched)
            CadCommandSession session = Typed("(load \"ghnone\")");

            Assert.Equal("; error: LOAD failed: \"ghnone\"", session.History[1]);
        }

        [Fact]
        public void LoadLisp_WritesWhatTheFileWroteAndThenTheFileNameLoaded()
        {
            string path = Path.Combine(_folder, "ghapp.lsp");
            File.WriteAllText(path, "(princ \"hello\")\n(defun c:ghapp () 1)\n", new UTF8Encoding(false));
            CadCommandSession session = Session(Blank());

            Assert.True(session.LoadLisp(path));

            Assert.Equal(new[] { "hello", "ghapp.lsp successfully loaded." }, session.History);
            Assert.Equal(new[] { "GHAPP" }, session.LispCommandNames);
        }

        [Fact]
        public void LoadLispOfAFileWithAnError_WritesTheErrorLineAndReturnsFalse()
        {
            string path = Path.Combine(_folder, "ghbad.lsp");
            File.WriteAllText(path, "(setq ghb 1)\n(car 1)\n(setq ghb 2)\n", new UTF8Encoding(false));
            CadCommandSession session = Session(Blank());

            Assert.False(session.LoadLisp(path));

            Assert.Equal(new[] { "; error: bad argument type: consp 1" }, session.History);
            Assert.Equal(1, session.Lisp.GetValue("GHB"));
            Assert.False(session.LoadLisp(Path.Combine(_folder, "ghmissing.lsp")));
        }

        [Fact]
        public void LoadLispOfAFileThatIsMissing_WritesLoadFailed()
        {
            CadCommandSession session = Session(Blank());
            string path = Path.Combine(_folder, "ghmissing.lsp");

            Assert.False(session.LoadLisp(path));

            Assert.Single(session.History);
            Assert.StartsWith("; error: LOAD failed: ", session.History[0]);
        }

        [Fact]
        public void ASymbolWithADotInIt_IsItsPartBeforeTheDotInALoadedFileToo()
        {
            // AutoCAD 2021: (setq e.d 5) sets E (boundp 'e is T, !e.d is 5), and a local named a.b is A; a routine of the
            // support folder, an edit-text one, names its temporary file by a variable EDITEXT.DCL in its argument list.
            string path = Path.Combine(_folder, "ghdot.lsp");
            File.WriteAllText(
                path,
                "(defun c:ghdot (/ ed.dcl) (setq ed.dcl 3) ed.dcl)\n(setq e.d 5)\n",
                new UTF8Encoding(false)
            );
            CadCommandSession session = Session(Blank());

            Assert.True(session.LoadLisp(path));
            Run(session, "!e", "GHDOT");

            Assert.Equal(
                new[] { "ghdot.lsp successfully loaded.", "Command: !e", "5", "Command: GHDOT", "3" },
                session.History
            );
        }

        [Fact]
        public void TheInterpreter_ReadsTheDocumentsUnitsForRtos()
        {
            CadCommandSession session = Session(Blank());
            session.Document.Database.Luprec = 2;
            session.Document.Database.Dimzin = 0;

            Run(session, "(rtos 1.5)");

            Assert.Equal("\"1.50\"", session.History[1]);
        }
    }
}
