using System;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>command-s</c> and the variable NOMUTT against AutoCAD 2021's accoreconsole (probes g6, g6b, g6c, g6d, g10 and g10b of
    /// the C4a measurements): the arguments to one command, a refusal that goes on, the end of the call, the arguments that
    /// are dropped, the failures with AutoCAD's words, and the one line a muted call writes.
    /// </summary>
    public sealed class LispCommandStackTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Quiet()
        {
            CadCommandSession session = Session(Blank());
            session.Feed("(setvar \"CMDECHO\" 0)");
            return session;
        }

        private static string[] Written(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return session.History.Skip(before + 1).Select(l => l.TrimEnd()).ToArray();
        }

        private static int Points(CadCommandSession session) => All<DxfDBPoint>(session.Document.ModelSpace).Count();

        #region What the call does

        [Fact]
        public void ACommandThatEndsWithItsArguments_GivesNilAndNothingElseIsWritten()
        {
            // AutoCAD 2021 (g6): (command-s "_.CIRCLE" "0,0" "5") nil; (command-s) nil; (command-s "_.REGEN") nil
            CadCommandSession session = Quiet();

            Assert.Equal(new[] { "nil" }, Written(session, "(command-s \"_.POINT\" \"0,0\")"));
            Assert.Equal(1, Points(session));
            Assert.Equal(new[] { "nil" }, Written(session, "(command-s)"));
            Assert.Equal(new[] { "nil" }, Written(session, "(command-s \"_.REGEN\")"));
        }

        [Fact]
        public void WhatTheCommandDoesNotTake_IsDroppedOnceItHasEnded()
        {
            // AutoCAD 2021 (g6c): (command-s "_.POINT" "0,0" "_.POINT" "1,1") makes one point, as do two answers after the first;
            // (command "_.POINT" "0,0" "_.POINT" "1,1") makes two.
            CadCommandSession session = Quiet();

            session.Feed("(command-s \"_.POINT\" \"0,0\" \"_.POINT\" \"1,1\")");
            Assert.Equal(1, Points(session));
            session.Feed("(command-s \"_.POINT\" \"0,0\" \"1,1\")");
            Assert.Equal(2, Points(session));
            session.Feed("(command \"_.POINT\" \"0,0\" \"_.POINT\" \"1,1\")");
            Assert.Equal(4, Points(session));
        }

        [Fact]
        public void APromptThatRefusesAnArgument_WritesItsWordsAndTakesTheNextAtTheSamePrompt()
        {
            // AutoCAD 2021 (g6b): (command-s "_.CIRCLE" "0,0" "abc" "7") writes Requires numeric radius, point on circumference,
            // or "D". and nil; the circle has the radius 7. (command with the same arguments is Function cancelled.)
            CadCommandSession session = Quiet();

            Assert.Equal(
                new[] { "Requires numeric radius, point on circumference, or \"D\".", "nil" },
                Written(session, "(command-s \"_.CIRCLE\" \"0,0\" \"abc\" \"7\")")
            );
            DxfCircle circle = Assert.Single(All<DxfCircle>(session.Document.ModelSpace));
            Assert.Equal(7.0, circle.Radius, 6);
        }

        [Fact]
        public void ARefusalOfTheLastArgument_LeavesTheCommandWaitingUntilItEnds()
        {
            // AutoCAD 2021 (g6b): (command-s "_.LINE" "0,0" "abc" "1,1" "") makes the line 0,0 to 1,1 with the words after abc.
            CadCommandSession session = Quiet();

            Assert.Equal(
                new[] { "Point or option keyword required.", "nil" },
                Written(session, "(command-s \"_.LINE\" \"0,0\" \"abc\" \"1,1\" \"\")")
            );
            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Assert.Equal(1.0, line.EndPoint.X, 6);
        }

        [Fact]
        public void ACommandLeftAtAPrompt_IsWaitedForUntilItEnds_AndEscGivesNilNotFunctionCancelled()
        {
            // AutoCAD 2021 (g5, g6): (command-s "_.LINE" "0,0" "1,1") writes Specify next point or [Undo]: and waits; what is
            // typed goes to the command, and Esc gives nil. (PAUSE would give the routine its turn at the next prompt.)
            CadCommandSession session = Quiet();

            int before = session.History.Count;
            session.Feed("(progn (command-s \"_.LINE\" \"0,0\" \"1,1\") (princ \"after\"))");
            Assert.True(session.IsLispWaiting);
            Assert.Contains("Specify next point or [Undo]:", session.History.Last());
            Assert.DoesNotContain(session.History.Skip(before + 1), l => l.Contains("after"));

            session.Feed("2,2");
            Assert.True(session.IsLispWaiting);
            session.Escape();
            Assert.False(session.IsLispWaiting);
            Assert.Contains(session.History, l => l.TrimEnd().EndsWith("after\"after\"", StringComparison.Ordinal));
            Assert.DoesNotContain(session.History, l => l.Contains("Function cancelled"));
            Assert.Equal(2, All<DxfLine>(session.Document.ModelSpace).Count());
        }

        [Fact]
        public void ACommandThatEndsWhereTheRoutineWaits_ReturnsAtOnce()
        {
            CadCommandSession session = Quiet();

            session.Feed("(progn (command-s \"_.LINE\" \"0,0\" \"1,1\") (princ \"after\"))");
            session.Feed(string.Empty);

            Assert.False(session.IsLispWaiting);
            Assert.Contains(session.History, l => l.Contains("after"));
        }

        [Fact]
        public void ATransparentCommandInTheArguments_RunsInsideTheCommand()
        {
            // AutoCAD 2021 (g6): (command-s "_.LINE" "0,0" "'_.ZOOM" "_E" "1,1" "") nil, and (command-s "'_.ZOOM" "_E") nil. ID
            // stands for ZOOM here, which has no view to zoom in a session with no window.
            CadCommandSession session = Quiet();

            string[] lines = Written(session, "(command-s \"_.LINE\" \"0,0\" \"'_.ID\" \"3,3\" \"1,1\" \"\")");

            Assert.Equal("nil", lines.Last());
            Assert.DoesNotContain(lines, l => l.Contains("Point or option keyword required."));
            DxfLine line = Assert.Single(All<DxfLine>(session.Document.ModelSpace));
            Assert.Equal(1.0, line.EndPoint.X, 6);
            Assert.Equal(new[] { "nil" }, Written(session, "(command-s \"'_.ZOOM\" \"_E\")"));
        }

        [Fact]
        public void WithCmdecho1_TheValueFollowsTheLastLineOfTheCommand_AsAutoCadWritesIt()
        {
            // AutoCAD 2021 (g6d): (command-s "_.POINT" "0,0") writes _.POINT, the prompt line, and ends it with " nil"; the
            // value is not on a Command: line of its own as it is after command.
            CadCommandSession session = Session(Blank());

            string[] lines = Written(session, "(command-s \"_.CIRCLE\" \"0,0\" \"5\")");

            Assert.Equal(
                new[]
                {
                    "_.CIRCLE",
                    "Specify center point for circle or [3P/2P/Ttr (tan tan radius)]: 0,0",
                    "Specify radius of circle or [Diameter]: 5 nil",
                },
                lines
            );
            Assert.Equal("Command: nil", Written(session, "(command \"_.REGEN\")").Last());
        }

        #endregion

        #region What it refuses

        [Theory]
        [InlineData("(command-s 5)")] // AutoCAD 2021 (g6): ; error: Unknown (command-s) failure.
        [InlineData("(command-s \"\")")] // AutoCAD 2021 (g6)
        [InlineData("(command-s \"NOSUCH\")")] // AutoCAD 2021 (g6d): nothing written for the name
        [InlineData("(command-s \"_.NOSUCHCMD\")")] // AutoCAD 2021 (g6)
        [InlineData("(command-s '(1 2))")] // AutoCAD 2021 (g6)
        [InlineData("(command-s (ssadd))")] // AutoCAD 2021 (g6)
        public void AFirstArgumentThatNamesNoCommand_IsAFailureWithNoOtherLine(string form)
        {
            CadCommandSession session = Quiet();

            Assert.Equal(new[] { "; error: Unknown (command-s) failure." }, Written(session, form));
        }

        [Fact]
        public void AnArgumentThatIsNilOrAListThatIsNoPoint_WritesItsWordsFirst_AndNothingIsRun()
        {
            // AutoCAD 2021 (g6c, g6d): nil gives Application ERROR: Invalid type sent as command input, a list of one number
            // Application ERROR: Invalid entity/point list.; then ; error: Unknown (command-s) failure.; the arguments before it
            // are not run either ((command-s "_.POINT" "0,0" nil) makes no point).
            CadCommandSession session = Quiet();

            Assert.Equal(
                new[]
                {
                    "Application ERROR: Invalid type sent as command input",
                    "; error: Unknown (command-s) failure.",
                },
                Written(session, "(command-s \"_.POINT\" \"0,0\" nil)")
            );
            Assert.Equal(
                new[] { "Application ERROR: Invalid entity/point list.", "; error: Unknown (command-s) failure." },
                Written(session, "(command-s \"_.POINT\" \"0,0\" '(1))")
            );
            Assert.Equal(
                new[]
                {
                    "Application ERROR: Invalid type sent as command input",
                    "; error: Unknown (command-s) failure.",
                },
                Written(session, "(command-s nil)")
            );
            Assert.Equal(0, Points(session));
        }

        [Theory]
        [InlineData("(command-s \"_.POINT\" 'a)")] // AutoCAD 2021 (g6d): ; error: Invalid data type for (command-s).
        [InlineData("(command-s \"_.POINT\" car)")] // AutoCAD 2021 (g6d)
        [InlineData("(command-s \"_.POINT\" T)")] // AutoCAD 2021 (g6d)
        [InlineData("(command-s T)")] // AutoCAD 2021 (g6d)
        [InlineData("(command-s 'a)")] // AutoCAD 2021 (g6d)
        public void ASymbolOrAFunction_IsInvalidDataType(string form)
        {
            CadCommandSession session = Quiet();

            Assert.Equal(new[] { "; error: Invalid data type for (command-s)." }, Written(session, form));
            Assert.Equal(0, Points(session));
        }

        [Fact]
        public void TheFailuresAreErrorsVlCatchAllApplySees()
        {
            // AutoCAD 2021 (g6): (vl-catch-all-error-message (vl-catch-all-apply 'command-s '(5))) "Unknown (command-s) failure."
            CadCommandSession session = Quiet();

            Assert.Equal(
                new[] { "\"Unknown (command-s) failure.\"" },
                Written(session, "(vl-catch-all-error-message (vl-catch-all-apply 'command-s '(5)))")
            );
        }

        #endregion

        #region NOMUTT

        private static CadCommandSession Muted()
        {
            CadCommandSession session = Session(Blank());
            session.Feed("(setvar \"NOMUTT\" 1)");
            return session;
        }

        [Theory]
        [InlineData("(command \"_.CIRCLE\" \"0,0\" \"5\")", "_.CIRCLE 0,0 5 nil")] // AutoCAD 2021 (g10)
        [InlineData("(command \"_.CIRCLE\" \"0,0\" 5)", "_.CIRCLE 0,0 5 nil")] // AutoCAD 2021 (g10b)
        [InlineData("(command \"_.CIRCLE\" \"0,0\" 5.5)", "_.CIRCLE 0,0 5.5 nil")] // AutoCAD 2021 (g10b)
        [InlineData("(command \"_.CIRCLE\" '(0 0 0) 3.25)", "_.CIRCLE 3.25 nil")] // AutoCAD 2021 (g10b): a point is not written
        [InlineData("(command \"_.POINT\" '(1 2))", "_.POINT nil")] // AutoCAD 2021 (g10b)
        [InlineData("(command \"_.LINE\" \"0,0\" \"1,1\" \"\")", "_.LINE 0,0 1,1 nil")] // AutoCAD 2021 (g10): "" is not written
        [InlineData("(command \"_.LINE\" \"0,0\" \"\" \"_.POINT\" \"1,1\")", "_.LINE 0,0 _.POINT 1,1 nil")] // AutoCAD 2021 (g10b)
        [InlineData("(command \"_.ZOOM\" \"_W\" \"0,0\" \"10,10\")", "_.ZOOM _W 0,0 10,10 nil")] // AutoCAD 2021 (g10b)
        [InlineData("(command \"_.UNDO\" \"_M\")", "_.UNDO _M nil")] // AutoCAD 2021 (g10)
        [InlineData("(command \"_.TEXT\" \"0,0\" \"5\" \"0\" \"hello world\")", "_.TEXT 0,0 5 0 hello world nil")] // AutoCAD 2021 (g10b)
        public void WithNomutt1_ACallWritesOneLineAndTheValueFollowsIt(string form, string line)
        {
            CadCommandSession session = Muted();

            Assert.Equal(new[] { line }, Written(session, form));
        }

        [Fact]
        public void WithNomutt1_AnEntityNameOrASelectionSet_IsTwoSpaces()
        {
            // AutoCAD 2021 (g10, g10b): (command "_.ERASE" (entlast) "") writes "_.ERASE   nil" and
            // (command "_.ERASE" "L" "") "_.ERASE L nil".
            CadCommandSession session = Muted();
            session.Feed("(command \"_.POINT\" \"0,0\")");
            session.Feed("(command \"_.POINT\" \"1,1\")");

            Assert.Equal(new[] { "_.ERASE   nil" }, Written(session, "(command \"_.ERASE\" (entlast) \"\")"));
            Assert.Equal(new[] { "_.ERASE   nil" }, Written(session, "(command \"_.ERASE\" (ssget \"_X\") \"\")"));
        }

        [Fact]
        public void WithNomutt1_TheTextTheRoutineLeftOnItsLineStartsTheCallsLine()
        {
            // AutoCAD 2021 (g10b): (progn (princ "a") (command "_.REGEN") (princ "b") (command "_.REGEN") (princ "c")) is the
            // line a_.REGEN b_.REGEN c"c"; with a new line after each princ, a / _.REGEN b / _.REGEN 5.
            CadCommandSession session = Muted();

            Assert.Equal(
                new[] { "a_.REGEN b_.REGEN c\"c\"" },
                Written(
                    session,
                    "(progn (princ \"a\") (command \"_.REGEN\") (princ \"b\") (command \"_.REGEN\") (princ \"c\"))"
                )
            );
            Assert.Equal(
                new[] { "a", "_.REGEN b", "_.REGEN 5" },
                Written(
                    session,
                    "(progn (princ \"a\\n\") (command \"_.REGEN\") (princ \"b\\n\") (command \"_.REGEN\") 5)"
                )
            );
        }

        [Fact]
        public void WithNomutt1_ARefusedArgumentEndsTheLineAndItsWordsFollow()
        {
            // AutoCAD 2021 (g10): _.CIRCLE 0,0 abc / Requires numeric radius, point on circumference, or "D". / ; error: Function
            // cancelled; and command-s goes on: _.CIRCLE 0,0 abc / the words / 5 nil.
            CadCommandSession session = Muted();

            Assert.Equal(
                new[]
                {
                    "_.CIRCLE 0,0 abc",
                    "Requires numeric radius, point on circumference, or \"D\".",
                    "; error: Function cancelled",
                },
                Written(session, "(command \"_.CIRCLE\" \"0,0\" \"abc\" \"5\")")
            );
            session.Escape();
            Assert.Equal(
                new[] { "_.CIRCLE 0,0 abc", "Requires numeric radius, point on circumference, or \"D\".", "5 nil" },
                Written(session, "(command-s \"_.CIRCLE\" \"0,0\" \"abc\" \"5\")")
            );
        }

        [Fact]
        public void WithNomutt1_AnUnknownNameIsOnTheLineAndNilIsOnTheNext()
        {
            // AutoCAD 2021 (g10): NOSUCH Unknown command "NOSUCH".  Press F1 for help. / nil
            CadCommandSession session = Muted();

            Assert.Equal(
                new[] { "NOSUCH Unknown command \"NOSUCH\".  Press F1 for help.", "nil" },
                Written(session, "(command \"NOSUCH\")")
            );
        }

        [Fact]
        public void WithNomutt1AndCmdecho0_OnlyTheWordsOfARefusalAreWritten()
        {
            // AutoCAD 2021 (g10): with CMDECHO 0 the line of the call is not written, as before.
            CadCommandSession session = Muted();
            session.Feed("(setvar \"CMDECHO\" 0)");

            Assert.Equal(new[] { "nil" }, Written(session, "(command \"_.POINT\" \"0,0\")"));
        }

        [Fact]
        public void WithNomutt0_TheCallWritesWhatItAlwaysDid()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal(
                new[]
                {
                    "_.POINT",
                    "Current point modes:  PDMODE=0  PDSIZE=0.0000",
                    "Specify a point: 0,0",
                    "Command: nil",
                },
                Written(session, "(command \"_.POINT\" \"0,0\")")
            );
        }

        #endregion
    }
}
