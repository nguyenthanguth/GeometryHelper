using System;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>command</c> and <c>vl-cmdf</c> as AutoCAD 2021's accoreconsole wrote them (probes q1, q2, q4 and q8 of the L2
    /// spec): the arguments in turn to the prompt of the command running, the echo with CMDECHO 1 and 0, the values, the
    /// command left running, <c>PAUSE</c>, nil, an unknown name, the command-line twin of a command, an entity name or a
    /// selection set, the Command prompt that starts the next text of the routine, and the undo step of each command
    /// (decisions D38 to D42, D60). A line of a transcript is compared as the harness does, without its trailing spaces.
    /// </summary>
    public sealed class LispCommandTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Typed(params object[] inputs) => Run(Session(Blank()), inputs);

        // The history without the trailing spaces of each line.
        private static string[] Lines(CadCommandSession session) => session.History.Select(l => l.TrimEnd()).ToArray();

        // The lines written after the first n.
        private static string[] After(CadCommandSession session, int n) => Lines(session).Skip(n).ToArray();

        #region Echo and values (q1)

        [Fact]
        public void CommandWithALineAndItsPoints_WritesItsNameAndEachPromptAnsweredAndThenTheValueAfterTheCommandPrompt()
        {
            // AutoCAD 2021 (q1): the name, each prompt with nothing after a point, then Command: nil
            CadCommandSession session = Typed("(command \"LINE\" '(0 0) '(10 0) \"\")");

            Assert.Equal(
                new[]
                {
                    "Command: (command \"LINE\" '(0 0) '(10 0) \"\")",
                    "LINE",
                    "Specify first point:",
                    "Specify next point or [Undo]:",
                    "Specify next point or [Undo]:",
                    "Command: nil",
                },
                Lines(session)
            );
            Assert.False(session.IsRunning);

            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Near(new GeoPoint3(0, 0, 0), line.StartPoint);
            Near(new GeoPoint3(10, 0, 0), line.EndPoint);
        }

        [Fact]
        public void VlCmdfWithALine_WritesTheSameAndGivesT()
        {
            // AutoCAD 2021 (q1): the lines of command, then Command: T
            CadCommandSession session = Typed("(vl-cmdf \"LINE\" '(0 20) '(10 20) \"\")");

            Assert.Equal(
                new[]
                {
                    "Command: (vl-cmdf \"LINE\" '(0 20) '(10 20) \"\")",
                    "LINE",
                    "Specify first point:",
                    "Specify next point or [Undo]:",
                    "Specify next point or [Undo]:",
                    "Command: T",
                },
                Lines(session)
            );
        }

        [Fact]
        public void CommandWithCmdecho0_WritesNothingOfTheCommandAndTheValueAloneAfterIt()
        {
            // AutoCAD 2021 (q1): after (setvar "CMDECHO" 0) the call writes just nil
            CadCommandSession session = Typed("CMDECHO", "0");
            int before = session.History.Count;

            Run(session, "(command \"LINE\" '(0 10) '(10 10) \"\")");

            Assert.Equal(new[] { "Command: (command \"LINE\" '(0 10) '(10 10) \"\")", "nil" }, After(session, before));
            Assert.Single(All<DxfLine>(session.Document.ModelSpace));
        }

        [Fact]
        public void ARefusalWithCmdecho0_IsStillWritten()
        {
            // ours: D41 says nothing of refusals under CMDECHO 0; the words of a refusal stay, nothing else of the command is written
            CadCommandSession session = Typed("CMDECHO", "0");
            int before = session.History.Count;

            Run(session, "(vl-cmdf \"LINE\" \"abc\")");

            Assert.Equal(
                new[] { "Command: (vl-cmdf \"LINE\" \"abc\")", "Invalid point.", "T" },
                After(session, before)
            );
        }

        [Fact]
        public void ANumberAndAStringForAPrompt_AreWrittenAfterIt()
        {
            // AutoCAD 2021 (q1): (command "CIRCLE" '(0 70) 5) writes ... Specify radius of circle or [Diameter]: 5
            CadCommandSession session = Typed("(command \"CIRCLE\" '(0 70) 5)");

            Assert.Equal(
                new[]
                {
                    "Command: (command \"CIRCLE\" '(0 70) 5)",
                    "CIRCLE",
                    "Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:",
                    "Specify radius of circle or [Diameter]: 5",
                    "Command: nil",
                },
                Lines(session)
            );
            DxfCircle circle = Assert.Single(All<DxfCircle>(session.Document.ModelSpace));
            Assert.Equal(5.0, circle.Radius, 9);

            Run(session, "(command \"CIRCLE\" '(0 80) \"5\")");

            Assert.Equal(
                "Specify radius of circle or [Diameter] <5.0000>: 5",
                Lines(session)[Lines(session).Length - 2]
            );
        }

        [Fact]
        public void PointsTypedAsStrings_AreWrittenAfterTheirPrompts()
        {
            // AutoCAD 2021 (q1): (command "LINE" "0,90" "10,90" "") writes Specify first point: 0,90 and the next
            CadCommandSession session = Typed("(command \"LINE\" \"0,90\" \"10,90\" \"\")");

            Assert.Equal(
                new[]
                {
                    "Command: (command \"LINE\" \"0,90\" \"10,90\" \"\")",
                    "LINE",
                    "Specify first point: 0,90",
                    "Specify next point or [Undo]: 10,90",
                    "Specify next point or [Undo]:",
                    "Command: nil",
                },
                Lines(session)
            );
        }

        [Fact]
        public void ARelativePointTypedAsAString_IsMeasuredFromTheLastPointOfTheCommand()
        {
            // AutoCAD 2021 (q2): (command "LINE" '(0 0) "@5<0" "") writes @5<0 after the next-point prompt and ends with nil
            CadCommandSession session = Typed("(command \"LINE\" '(0 0) \"@5<0\" \"\")");

            Assert.Equal("Specify next point or [Undo]: @5<0", Lines(session)[3]);
            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Near(new GeoPoint3(5, 0, 0), line.EndPoint);
        }

        #endregion

        #region The command left running (q1, q2)

        [Fact]
        public void ACommandLeftRunningByTheLastArgument_WritesItsPromptBeforeTheValueAndWaitsForTheUser()
        {
            // AutoCAD 2021 (q1): (command "LINE" '(0 30) '(10 30)) ends Specify next point or [Undo]: nil, LINE still running
            CadCommandSession session = Typed("(command \"LINE\" '(0 30) '(10 30))");

            Assert.Equal(
                new[]
                {
                    "Command: (command \"LINE\" '(0 30) '(10 30))",
                    "LINE",
                    "Specify first point:",
                    "Specify next point or [Undo]:",
                    "Specify next point or [Undo]: nil",
                },
                Lines(session)
            );
            Assert.True(session.IsRunning);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void ACallTypedAtThePromptOfTheCommandLeftRunning_GoesOnFeedingIt()
        {
            // AutoCAD 2021 (q1): (command '(20 30) "") at LINE's prompt takes the point and the Enter that ends LINE
            CadCommandSession session = Typed("(command \"LINE\" '(0 30) '(10 30))", "(command '(20 30) \"\")");

            Assert.Equal(
                new[]
                {
                    "Specify next point or [Undo]: (command '(20 30) \"\")",
                    "Specify next point or [Close/Undo]:",
                    "Command: nil",
                },
                After(session, 5)
            );
            Assert.False(session.IsRunning);
            Assert.Equal(2, All<DxfLine>(session.Document.ModelSpace).Count);
        }

        [Fact]
        public void ACommandLeftRunningByARoutine_IsAnsweredByTheUserWithTheEchoOfThePrompt()
        {
            // AutoCAD 2021 (q2, GHK): the routine's text goes on from the prompt, then the user's answer is written after it
            CadCommandSession session = Typed(
                "(defun c:ghk () (command \"LINE\" '(0 0)) (princ \"left\"))",
                "GHK",
                "4,4",
                Enter
            );

            Assert.Equal(
                new[]
                {
                    "Command: GHK",
                    "LINE",
                    "Specify first point:",
                    "Specify next point or [Undo]: left\"left\"",
                    "Specify next point or [Undo]: 4,4",
                    "Specify next point or [Undo]:",
                },
                After(session, 2)
            );
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ACommandLeftRunningWithCmdecho0_HasNoPromptBeforeTheRoutinesText()
        {
            // AutoCAD 2021 (q4, GHK): with CMDECHO 0 when LINE is left, the text is k"k" alone, the user's answer has its prompt
            CadCommandSession session = Session(Blank());
            session.Lisp.Define("GHECHO", 1, 1, a =>
            {
                session.Remembered["cmdecho"] = Convert.ToDouble(a[0]);
                return null;
            });
            Run(
                session,
                "(defun c:ghk () (ghecho 0) (command \"LINE\" '(0 0)) (ghecho 1) (princ \"k\"))",
                "GHK",
                "2,2",
                Enter
            );

            Assert.Equal(
                new[]
                {
                    "Command: GHK",
                    "k\"k\"",
                    "Specify next point or [Undo]: 2,2",
                    "Specify next point or [Undo]:",
                },
                After(session, 2)
            );
        }

        [Fact]
        public void TheCommandPromptThatStartsTheNextTextOfARoutine_FollowsEachCommandThatEnded()
        {
            // AutoCAD 2021 (q2, GHU): Command: CIRCLE for the second command, Command: done7 for the text and the value
            CadCommandSession session = Typed(
                "(defun c:ghu () (command \"LINE\" '(0 0) '(1 1) \"\") (command \"CIRCLE\" '(0 0) 3) (princ \"done\") 7)",
                "GHU"
            );

            Assert.Equal(
                new[]
                {
                    "Command: GHU",
                    "LINE",
                    "Specify first point:",
                    "Specify next point or [Undo]:",
                    "Specify next point or [Undo]:",
                    "Command: CIRCLE",
                    "Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:",
                    "Specify radius of circle or [Diameter]: 3",
                    "Command: done7",
                },
                After(session, 2)
            );
        }

        [Fact]
        public void TwoCommandsInOneCall_WriteTheSecondAfterTheCommandPrompt()
        {
            // AutoCAD 2021 (q2): (command "LINE" '(0 0) '(1 0) "" "CIRCLE" '(0 0) 3) writes Command: CIRCLE
            CadCommandSession session = Typed("(command \"LINE\" '(0 0) '(1 0) \"\" \"CIRCLE\" '(0 0) 3)");

            Assert.Contains("Command: CIRCLE", Lines(session));
            Assert.Equal("Command: nil", Lines(session).Last());
            Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Assert.Single(All<DxfCircle>(session.Document.ModelSpace));
        }

        [Fact]
        public void CopyRunByCommand_MakesOneCopyAndEnds_ForTheNextCommandOfTheRoutine()
        {
            // AutoCAD 2021 (runs/hhh): (command "copy" ename "" base to) twice in a row made one copy each, COPY did not wait at
            // Specify second point or [Array/Exit/Undo] <Exit>: for the next command to be refused there
            CadCommandSession session = Typed(
                "(command \"LINE\" '(0 0) '(10 0) \"\")",
                "(setq e (entlast))",
                "(progn (command \"copy\" e \"\" '(0 0) '(0 10)) (command \"copy\" e \"\" '(0 0) '(0 20)) (princ))"
            );

            Assert.False(session.IsRunning);
            Assert.Equal(3, All<DxfLine>(session.Document.ModelSpace).Count);
        }

        [Fact]
        public void TextRunByCommand_TakesTheOneLineItIsGivenAndEnds()
        {
            // AutoCAD 2021 (q2, q5, the routine feeds): (command "TEXT" '(0 0) 2.5 0 "Hello") ends with Command: nil, no second
            // Enter text: prompt; the next form is then AutoLISP, not a second line of the text
            CadCommandSession session = Typed(
                "(command \"TEXT\" '(0 0) 2.5 0 \"Hello\")",
                "(command \"TEXT\" '(0 10) 2.5 0 \"Hello world\")"
            );

            Assert.False(session.IsRunning);
            Assert.Equal("Command: nil", Lines(session).Last());
            Assert.Equal(
                new[] { "Hello", "Hello world" },
                All<DxfDBText>(session.Document.ModelSpace).Select(t => t.TextString)
            );
        }

        #endregion

        #region Refusals (q1, q2)

        [Fact]
        public void CommandWithAPointAsAStringThatIsNone_WritesTheRefusalAndFailsWithFunctionCancelled()
        {
            // AutoCAD 2021 (q1): (command "LINE" "(list 0 100)" ...) writes the text after the prompt, Invalid point., then the error
            CadCommandSession session = Typed("(command \"LINE\" \"(list 0 100)\" \"10,100\" \"\")");

            Assert.Equal(
                new[]
                {
                    "Command: (command \"LINE\" \"(list 0 100)\" \"10,100\" \"\")",
                    "LINE",
                    "Specify first point: (list 0 100)",
                    "Invalid point.",
                    "; error: Function cancelled",
                },
                Lines(session)
            );
            Assert.True(session.IsRunning);
            Assert.Equal("Specify first point:", session.PromptText);
        }

        [Fact]
        public void VlCmdfWithARefusedArgument_WritesTheRefusalPassesNothingMoreAndGivesT()
        {
            // AutoCAD 2021 (q1): (vl-cmdf "LINE" "abc") writes Invalid point. and T, LINE left at its first point
            CadCommandSession session = Typed("(vl-cmdf \"LINE\" \"abc\" '(0 0))");

            Assert.Equal(
                new[]
                {
                    "Command: (vl-cmdf \"LINE\" \"abc\" '(0 0))",
                    "LINE",
                    "Specify first point: abc",
                    "Invalid point.",
                    "T",
                },
                Lines(session)
            );
            Assert.True(session.IsRunning);
            Assert.Equal("Specify first point:", session.PromptText);
        }

        [Fact]
        public void ANumberAtANextPointPrompt_IsRefusedWithThePointsWordsAndTheCommandFails()
        {
            // AutoCAD 2021 (q2): (command "LINE" '(0 0) 2.5 "") writes ... Specify next point or [Undo]: 2.5 / Point or option keyword required.
            CadCommandSession session = Typed("(command \"LINE\" '(0 0) 2.5 \"\")");

            Assert.Equal(
                new[]
                {
                    "Specify next point or [Undo]: 2.5",
                    "Point or option keyword required.",
                    "; error: Function cancelled",
                },
                After(session, 3)
            );
        }

        [Fact]
        public void ARefusedArgumentOfARoutine_ReachesItsErrorFunctionAsFunctionCancelled()
        {
            // AutoCAD 2021 (q2): an *error* of the routine is called with Function cancelled
            CadCommandSession session = Typed(
                "(defun c:ghx (/ *error*) (defun *error* (m) (princ (strcat \"E:\" m)) (princ)) (command \"LINE\" \"abc\"))",
                "GHX"
            );

            Assert.Equal("E:Function cancelled", Lines(session).Last());
        }

        [Fact]
        public void ASymbolForACommand_IsABadArgumentValueNamingIt()
        {
            // AutoCAD 2021 (q1): (command 'line ...) gives ; error: bad argument value: AutoCAD command: LINE
            CadCommandSession session = Typed("(command 'line '(0 0) '(1 0) \"\")");

            Assert.Equal(
                new[]
                {
                    "Command: (command 'line '(0 0) '(1 0) \"\")",
                    "; error: bad argument value: AutoCAD command: LINE",
                },
                Lines(session)
            );
            Assert.False(session.IsRunning);
        }

        #endregion

        #region nil, no argument, unknown names (q1, q2)

        [Fact]
        public void CommandWithNoArgument_CancelsTheCommandRunningWithNoCancelLine()
        {
            // AutoCAD 2021 (q1): (command) at LINE's prompt gives Command: nil and no *Cancel*
            CadCommandSession session = Typed("(command \"LINE\" '(0 110))", "(command)");

            Assert.Equal(new[] { "Specify next point or [Undo]: (command)", "Command: nil" }, After(session, 4));
            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfLine>(session.Document.ModelSpace));
        }

        [Fact]
        public void NilForACommandRunning_CancelsItAndNilWithNoneDoesNothing()
        {
            // AutoCAD 2021 (q2): (command "LINE" '(0 0) nil) ends with the prompt answered and Command: nil; (command nil) alone is quiet
            CadCommandSession session = Typed("(command \"LINE\" '(0 0) nil)");

            Assert.Equal(
                new[] { "Command: (command \"LINE\" '(0 0) nil)", "LINE", "Specify first point:", "Command: nil" },
                Lines(session)
            );
            Assert.False(session.IsRunning);

            int before = session.History.Count;
            Run(session, "(command nil)");

            Assert.Equal(new[] { "Command: (command nil)", "nil" }, After(session, before));
        }

        [Fact]
        public void AnUnknownName_IsWrittenAfterTheNameGivenAndTheArgumentsGoOn()
        {
            // AutoCAD 2021 (q1): the name as given and Unknown command with it in capitals, no error
            CadCommandSession session = Typed("(command \"01GaepoSB3F111TCS10\" \"02Gaepo\")");

            Assert.Equal(
                new[]
                {
                    "Command: (command \"01GaepoSB3F111TCS10\" \"02Gaepo\")",
                    "01GaepoSB3F111TCS10 Unknown command \"01GAEPOSB3F111TCS10\".  Press F1 for help.",
                    "Command: 02Gaepo Unknown command \"02GAEPO\".  Press F1 for help.",
                    "Command: nil",
                },
                Lines(session)
            );
        }

        [Fact]
        public void ACommandMadeByDefun_IsNotFoundByCommandAndAnEmptyStringRepeatsTheLastCommandNotAnyCOne()
        {
            // AutoCAD 2021 (runs: COPY's extra "" gave Unknown command "GHS"): the last command is the c: one, which command never finds
            CadCommandSession session = Typed("(defun c:ghs () (command \"\") (princ))", "GHS");

            Assert.Equal(
                new[] { "Command: GHS", "GHS", "Unknown command \"GHS\".  Press F1 for help." },
                After(session, 2)
            );
        }

        [Fact]
        public void AnEmptyStringAtTheCommandPrompt_RepeatsTheLastBuiltInCommand()
        {
            // AutoCAD 2021: "" at the Command prompt repeats the last command
            CadCommandSession session = Typed("LINE", "0,0", "1,0", Enter);
            int before = session.History.Count;

            Run(session, "(command \"\")");

            Assert.Equal("LINE", Lines(session)[before + 1]);
            Assert.True(session.IsRunning);
            Assert.Equal("Specify first point:", session.PromptText);
        }

        [Fact]
        public void TextThatLooksLikeAFormGivenToCommand_IsTextAtThePromptAndNeverAutoLisp()
        {
            // AutoCAD 2021 (q1, q2): "(list 0 100)" and "!ss" are typed text, not LISP: Invalid point. / Point or option keyword required.
            CadCommandSession session = Typed("(command \"LINE\" '(0 0) \"(list 5 120)\")");

            Assert.Equal(
                new[] { "Specify next point or [Undo]: (list 5 120)", "Point or option keyword required." },
                After(session, 3).Take(2)
            );
            Assert.Equal("; error: Function cancelled", Lines(session).Last());
            Assert.True(session.IsRunning);
        }

        #endregion

        #region PAUSE (q1, q2, q8)

        [Fact]
        public void APause_WaitsForTheUserAtThePromptAndTheRoutineGoesOnWhenTheCommandAsksTheNext()
        {
            // AutoCAD 2021 (q1): (command "LINE" '(0 50) pause '(20 50) "") with 10,50 typed at the pause
            CadCommandSession session = Typed("(command \"LINE\" '(0 50) pause '(20 50) \"\")");

            Assert.True(session.IsRunning);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Assert.Equal(
                new[] { "Command: (command \"LINE\" '(0 50) pause '(20 50) \"\")", "LINE", "Specify first point:" },
                Lines(session)
            );

            Run(session, "10,50");

            Assert.Equal(
                new[]
                {
                    "Specify next point or [Undo]: 10,50",
                    "Specify next point or [Undo]:",
                    "Specify next point or [Close/Undo]:",
                    "Command: nil",
                },
                After(session, 3)
            );
            Assert.False(session.IsRunning);
            Assert.Equal(2, All<DxfLine>(session.Document.ModelSpace).Count);
        }

        [Fact]
        public void TheValueOfPause_IsABackslash()
        {
            // AutoCAD 2021 (q7): PAUSE is the symbol whose value is a backslash, printed escaped
            Assert.Equal(new[] { "Command: !pause", "\"\\\\\"" }, Lines(Typed("!pause")));
        }

        [Fact]
        public void TextThatLooksLikeAFormAtAPause_IsTextToThePromptAndNeverAutoLisp()
        {
            // AutoCAD 2021 (q1, q2): (list 5 120) typed at a pause is Point or option keyword required., no LISP
            CadCommandSession session = Typed("(command \"LINE\" '(0 120) pause \"\")", "(list 5 120)");

            Assert.Equal(
                new[] { "Specify next point or [Undo]: (list 5 120)", "Point or option keyword required." },
                After(session, 3)
            );
            Assert.True(session.IsRunning);
            Assert.True(session.IsLispInput("(list 1)") == false);
        }

        [Fact]
        public void EscAtAPauseWithNoErrorFunction_CancelsTheCommandAndWritesTheErrorAfterTheCommandPrompt()
        {
            // AutoCAD 2021 (q2): Specify next point or [Undo]: *Cancel* / Command: ; error: Function cancelled
            CadCommandSession session = Typed("(command \"LINE\" '(0 130) pause \"\")", Esc);

            Assert.Equal(
                new[] { "Specify next point or [Undo]: *Cancel*", "Command: ; error: Function cancelled" },
                After(session, 3)
            );
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void EscAtAPauseOfARoutineWithAnErrorFunction_CallsItWithFunctionCancelled()
        {
            // AutoCAD 2021 (q2, GHE): Command: E:Function cancelled
            CadCommandSession session = Typed(
                "(defun c:ghe (/ *error*) (defun *error* (m) (princ (strcat \"E:\" m)) (princ)) (command \"LINE\" '(0 0) pause \"\") (princ \"after\"))",
                "GHE",
                Esc
            );

            Assert.Equal(
                new[] { "Specify next point or [Undo]: *Cancel*", "Command: E:Function cancelled" },
                After(session, 5)
            );
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void APauseAnsweredByTheLastPromptOfTheCommand_LetsTheRoutineGoOnAfterTheCommandEnded()
        {
            // AutoCAD 2021 (q2, GHP) and ours: the routine's text follows the Command prompt when the answer ended the command
            CadCommandSession session = Typed(
                "(defun c:ghp () (command \"LINE\" '(0 0) pause pause) (princ \"after\"))",
                "GHP",
                "3,3",
                Enter
            );

            Assert.Equal(
                new[]
                {
                    "Specify next point or [Undo]: 3,3",
                    "Specify next point or [Undo]:",
                    "Command: after\"after\"",
                },
                After(session, 5)
            );
        }

        [Fact]
        public void APauseWithCmdecho0_EchoesTheUsersAnswerAloneAndEscWritesCancelAlone()
        {
            // AutoCAD 2021 (q8, GHQ): 5,5 alone and q"q"; then *Cancel* alone and ; error: Function cancelled
            CadCommandSession session = Typed(
                "CMDECHO",
                "0",
                "(defun c:ghq () (command \"LINE\" '(0 0) pause \"\") (princ \"q\"))"
            );
            int before = session.History.Count;

            Run(session, "GHQ", "5,5");

            Assert.Equal(new[] { "Command: GHQ", "5,5", "q\"q\"" }, After(session, before));

            before = session.History.Count;
            Run(session, "GHQ", Esc);

            Assert.Equal(new[] { "Command: GHQ", "*Cancel*", "; error: Function cancelled" }, After(session, before));
        }

        [Fact]
        public void APauseWhereNoCommandRuns_IsPassedOver()
        {
            // ours: nothing waits at a pause with no command running, and the routine goes on
            CadCommandSession session = Typed("(progn (command \"LINE\" '(0 0) '(1 1) \"\" pause) (princ \"x\"))");

            Assert.False(session.IsRunning);
            Assert.Equal("Command: x\"x\"", Lines(session).Last());
        }

        #endregion

        #region Objects as arguments (q1, q2, q8, runs)

        [Fact]
        public void AnEntityNameAtSelectObjects_PicksThatEntity()
        {
            // AutoCAD 2021 (q1): (command "ERASE" (entlast) "") writes Select objects:   1 found and Select objects:; the presenter's two lines
            CadCommandSession session = Typed("LINE", "0,0", "10,0", Enter);
            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            session.Lisp.SetValue("e", new LispEntityName(line.Handle));
            int before = session.History.Count;

            Run(session, "(command \"ERASE\" e \"\")");

            Assert.Equal(
                new[]
                {
                    "Command: (command \"ERASE\" e \"\")",
                    "ERASE",
                    "Select objects:",
                    "1 found",
                    "Select objects:",
                    "Command: nil",
                },
                After(session, before)
            );
            Assert.Empty(All<DxfLine>(session.Document.ModelSpace));
        }

        [Fact]
        public void ASelectionSetAtSelectObjects_GivesItsLiveObjectsAndSkipsAnErasedOne()
        {
            // AutoCAD 2021 (q8): (command "MOVE" ss "" ...) takes every object of the set: 3 found; ours: a dead one is skipped
            CadCommandSession session = Typed("CIRCLE", "0,0", "1", "CIRCLE", "5,0", "1", "CIRCLE", "9,0", "1");
            string[] handles = All<DxfCircle>(session.Document.ModelSpace).Select(c => c.Handle).ToArray();
            session.Lisp.SetValue("ss", new LispSelectionSet(handles.Concat(new[] { "FFFF" })));
            int before = session.History.Count;

            Run(session, "(command \"MOVE\" ss \"\" '(0 0) '(0 100))");

            Assert.Equal(
                new[]
                {
                    "Command: (command \"MOVE\" ss \"\" '(0 0) '(0 100))",
                    "MOVE",
                    "Select objects:",
                    "3 found",
                    "Select objects:",
                },
                After(session, before).Take(5)
            );
            Assert.All(All<DxfCircle>(session.Document.ModelSpace), c => Assert.Equal(100.0, c.Center.Y, 9));
        }

        [Fact]
        public void ASelectionSetAtAPromptForOneObject_GivesItsFirstObject()
        {
            // AutoCAD 2021 (runs: nn, PEDIT's Select polyline): a set at a prompt for one object gives its first entity
            CadCommandSession session = Typed("LINE", "0,0", "10,0", Enter);
            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            session.Lisp.SetValue("ss", new LispSelectionSet(new[] { line.Handle }));
            int before = session.History.Count;

            Run(session, "(command \"PEDIT\" ss)");

            Assert.True(session.IsRunning);
            Assert.Contains("turn it into one", session.PromptText);
            Assert.Equal("PEDIT", Lines(session)[before + 1]);
        }

        #endregion

        #region Names, twins and prefixes (q2, runs)

        [Fact]
        public void ADotAndAnUnderscoreBeforeTheName_AreTakenOffAndTheNameIsWrittenAsGiven()
        {
            // AutoCAD 2021 (q2): (command "_.LINE" '(0 0 0) '(1 1 0) "") writes _.LINE
            CadCommandSession session = Typed("(command \"_.LINE\" '(0 0 0) '(1 1 0) \"\")");

            Assert.Equal("_.LINE", Lines(session)[1]);
            Assert.Single(All<DxfLine>(session.Document.ModelSpace));
        }

        [Fact]
        public void ACommandWithAnAlias_IsFoundByIt()
        {
            CadCommandSession session = Typed("(command \"L\" '(0 0) '(1 1) \"\")");

            Assert.Equal("L", Lines(session)[1]);
            Assert.Single(All<DxfLine>(session.Document.ModelSpace));
        }

        [Fact]
        public void LayerRunByCommand_AsksThePromptsOfItsCommandLineTwinAndWritesTheNameAsGiven()
        {
            // AutoCAD 2021 (q2, runs: ll): (command "layer" ...) runs -LAYER's prompts, the name as given; New makes the layer
            CadCommandSession session = Typed("(command \"layer\" \"N\" \"GHQ1\" \"\")");

            Assert.Equal("layer", Lines(session)[1]);
            Assert.StartsWith("Enter an option", Lines(session)[3]);
            Assert.False(session.IsRunning);
            Assert.True(session.Document.Database.LayerTable.Has("GHQ1"));
        }

        [Fact]
        public void ASystemVariableNamedAlone_IsAskedItsNewValue()
        {
            // AutoCAD 2021 (runs: dd): (command "DIMSCALE" 1) runs the variable's own prompt, no command has the name
            CadCommandSession session = Typed("(command \"LTSCALE\" 2)");

            Assert.Equal("LTSCALE", Lines(session)[1]);
            Assert.False(session.IsRunning);
            Assert.EndsWith("nil", Lines(session).Last());
        }

        [Fact]
        public void TheCommandsOfARoutine_AreNotTheCommandEnterRepeats()
        {
            // AutoCAD 2021 (q8): the last command stays the c: one, however many commands the routine ran
            CadCommandSession session = Typed("(defun c:ghr () (command \"CIRCLE\" '(0 0) 1) (princ))", "GHR", Enter);

            Assert.Equal("GHR", session.LastCommandName);
            Assert.Equal(2, All<DxfCircle>(session.Document.ModelSpace).Count);
        }

        [Fact]
        public void AValueOfAFormTypedAtAPrompt_AnswersItOnlyWhenTheFormRanNoCommand()
        {
            // L1 decision 9 and ours: the value of a form that started a command is not typed at it
            CadCommandSession session = Typed("(progn (command \"LINE\" '(0 0)) 5)");

            Assert.True(session.IsRunning);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Assert.DoesNotContain("Point or option keyword required.", session.History);
        }

        #endregion

        #region Undo (q4)

        [Fact]
        public void UAfterARoutineThatRanTwoCommands_UndoesTheLastOneAlone()
        {
            // AutoCAD 2021 (q4): U after a c: that ran LINE and CIRCLE took the CIRCLE, 2 objects to 1
            CadCommandSession session = Typed(
                "(defun c:ghu () (command \"LINE\" '(0 0) '(1 1) \"\") (command \"CIRCLE\" '(0 0) 3) (princ))",
                "GHU"
            );
            Assert.Single(All<DxfCircle>(session.Document.ModelSpace));
            Assert.Single(All<DxfLine>(session.Document.ModelSpace));

            Run(session, "U");

            Assert.Empty(All<DxfCircle>(session.Document.ModelSpace));
            Assert.Single(All<DxfLine>(session.Document.ModelSpace));
        }

        [Fact]
        public void UAfterTwoCallsOfCommandInOneForm_UndoesTheSecondCallAlone()
        {
            // AutoCAD 2021 (q4): U after (progn (command LINE...) (command LINE...)) took one line of the two
            CadCommandSession session = Typed(
                "(progn (command \"LINE\" '(0 0) '(1 1) \"\") (command \"LINE\" '(0 0) '(1 2) \"\"))"
            );
            Assert.Equal(2, All<DxfLine>(session.Document.ModelSpace).Count);

            Run(session, "U");

            Assert.Single(All<DxfLine>(session.Document.ModelSpace));
        }

        [Fact]
        public void UndoBeginAndEndRunByCommand_GroupTheCommandsBetweenThem()
        {
            // AutoCAD 2021 (q4, GHV): UNDO BE ... UNDO E around two lines groups them; U takes both
            CadCommandSession session = Typed(
                "(defun c:ghv () (command \"UNDO\" \"BE\") (command \"LINE\" '(0 0) '(1 1) \"\") (command \"LINE\" '(0 0) '(1 3) \"\") (command \"UNDO\" \"E\") (princ))",
                "GHV"
            );
            Assert.Equal(2, All<DxfLine>(session.Document.ModelSpace).Count);

            Run(session, "U");

            Assert.Empty(All<DxfLine>(session.Document.ModelSpace));
        }

        [Fact]
        public void UAfterARoutineThatOnlyMadeAnEntity_UndoesThatEntityAloneWhereAutoCadTakesTheStepBeforeToo()
        {
            // AutoCAD 2021 (q4, GHN): U after a c: that only ran entmake took the entity and the object made before it, 5 objects to
            // 3; ours (D42): what a routine changes outside any command is one undo step of its own, so U takes the one object
            CadCommandSession session = Typed(
                "(command \"LINE\" '(0 0) '(10 0) \"\")",
                "(defun c:ghn () (entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 5.0 5.0 0.0))) (princ))",
                "GHN"
            );
            Assert.Equal(2, All<DxfLine>(session.Document.ModelSpace).Count);

            Run(session, "U");

            DxfLine left = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Near(new GeoPoint3(10, 0, 0), left.EndPoint);
        }

        #endregion

        #region A routine with a get and a command

        [Fact]
        public void ACommandRunAfterAGetFunctionAnswered_RunsAndTheRoutineGoesOn()
        {
            // ours: D60, a command run by the routine after its get takes the get's place; the evaluation then ends normally
            CadCommandSession session = Typed(
                "(defun c:ghg (/ p) (setq p (getpoint \"P: \")) (command \"CIRCLE\" p 2) (princ \"ok\"))",
                "GHG",
                "5,6"
            );

            DxfCircle circle = Assert.Single(All<DxfCircle>(session.Document.ModelSpace));
            Near(new GeoPoint3(5, 6, 0), circle.Center);
            Assert.False(session.IsRunning);
            Assert.Equal("Command: ok\"ok\"", Lines(session).Last());
        }

        [Fact]
        public void AGetFunctionAfterACommand_IsAskedAndAnsweredThenTheRoutineEnds()
        {
            // ours: a get after a command ended is asked as any get is
            CadCommandSession session = Typed(
                "(defun c:ghh (/ p) (command \"CIRCLE\" '(0 0) 1) (setq p (getpoint \"P: \")) (princ))",
                "GHH"
            );

            Assert.True(session.IsRunning);
            Assert.Equal("P: ", session.PromptText);

            Run(session, "2,3");

            Assert.Equal("P: 2,3", Lines(session).Last());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ACommandRunByAFormThatAsksAtThePromptOfACommand_FeedsThatCommandAfterTheAsk()
        {
            // ours: a form typed at LINE's prompt asks, then runs command: LINE is back as the command running and takes the points
            CadCommandSession session = Typed("LINE", "0,0", "(progn (getpoint \"X: \") (command \"1,1\" \"\"))");

            Assert.Equal("X: ", session.PromptText);

            Run(session, "3,3");

            Assert.False(session.IsRunning);
            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Near(new GeoPoint3(1, 1, 0), line.EndPoint);
        }

        [Fact]
        public void AGetFunctionAfterAPause_IsAskedWhileTheCommandWaitsAndThenTheCommandIsAskedAgain()
        {
            // ours: the routine's get after the command's next prompt is asked first; LINE asks its own prompt when it is answered
            CadCommandSession session = Typed(
                "(progn (command \"LINE\" '(0 0) pause) (getpoint \"P: \") (princ))",
                "1,0"
            );

            Assert.True(session.IsRunning);
            Assert.Equal("P: ", session.PromptText);

            Run(session, "9,9");

            Assert.True(session.IsRunning);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void TheLinesOfARoutine_ComeInTheOrderTheyWereMade()
        {
            // ours: text written before a command comes before the command's lines
            CadCommandSession session = Typed(
                "(defun c:gho () (princ \"first\") (command \"CIRCLE\" '(0 0) 1) (princ \"last\"))",
                "GHO"
            );

            string[] lines = After(session, 3);
            Assert.Equal("first", lines[0]);
            Assert.Equal("CIRCLE", lines[1]);
        }

        #endregion
    }
}
