using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>grread</c>: the wait of a routine for one key, click or move of the cursor (L8, D1458 to D1460), answered through
    /// <see cref="CadCommandSession.FeedKey"/>, <see cref="CadCommandSession.FeedMove"/>,
    /// <see cref="CadCommandSession.FeedRightClick"/> and <see cref="CadCommandSession.Feed(GeoPoint2)"/>, the loop that
    /// draws with <c>grvecs</c> as the cursor moves, and Esc.
    /// </summary>
    public sealed class LispGrReadTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public LispGrReadTests(ITestOutputHelper output) => _output = output;

        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static string Value(CadCommandSession session, string form) => LispEntityTests.Value(session, form);

        private static CadCommandSession Fresh()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setvar \"CMDECHO\" 0)");
            return session;
        }

        // A session whose routine waits in the grread of the form given.
        private static CadCommandSession Waiting(string form)
        {
            CadCommandSession session = Fresh();
            session.Feed(form);
            Assert.True(session.IsRunning);
            return session;
        }

        #region Keys

        [Fact]
        public void AKeyPressed_GivesItsCode_AndWritesNothingToTheHistoryButTheValue()
        {
            CadCommandSession session = Waiting("(setq g (grread))");
            int lines = session.History.Count;

            Assert.True(session.FeedKey('A'));

            Assert.False(session.IsRunning);
            Assert.Equal(lines + 1, session.History.Count);
            Assert.Equal("(2 65)", session.History[session.History.Count - 1]);
            Assert.Equal("(2 65)", Value(session, "!g"));
        }

        [Theory]
        [InlineData(13)]
        [InlineData(32)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(97)]
        public void EachKeyCode_IsGivenAsItIsPressed(int code)
        {
            CadCommandSession session = Waiting("(setq g (grread))");

            Assert.True(session.FeedKey(code));

            Assert.Equal("(2 " + code + ")", Value(session, "!g"));
        }

        [Fact]
        public void EnterAtTheWait_IsTheKey13()
        {
            CadCommandSession session = Waiting("(setq g (grread))");

            session.Enter();

            Assert.Equal("(2 13)", Value(session, "!g"));
        }

        [Fact]
        public void TextFedAtTheWait_IsKeysOnePerCall_ThenEnter()
        {
            // The line typed is the keys of its characters, a space among them, then 13: one answer for each grread call.
            CadCommandSession session = Fresh();
            Value(session, "(setq s nil)");
            session.Feed("(while (/= 13 (cadr (setq g (grread)))) (setq s (cons (cadr g) s)))");

            Assert.True(session.Feed("ab c"));

            Assert.False(session.IsRunning);
            Assert.Equal("(99 32 98 97)", Value(session, "!s"));
        }

        [Fact]
        public void TextThatStartsWithAParenthesis_IsKeysAndNoAutoLisp()
        {
            CadCommandSession session = Fresh();
            Value(session, "(setq s nil)");
            session.Feed("(while (/= 13 (cadr (setq g (grread)))) (setq s (cons (cadr g) s)))");

            session.Feed("(a");

            Assert.Equal("(97 40)", Value(session, "!s"));
        }

        [Fact]
        public void TextFedWhileTheRoutineStopsReadingAfterOneKey_IsNotKeptForTheNextPrompt()
        {
            CadCommandSession session = Fresh();
            session.Feed("(setq g (grread))");

            session.Feed("xyz");

            Assert.False(session.IsRunning);
            Assert.Equal("(2 120)", Value(session, "!g"));

            // A grread asked later waits for what is pressed then, not for the "yz" and the Enter left over.
            session.Feed("(setq h (grread))");
            Assert.True(session.IsRunning);
        }

        [Fact]
        public void KeysWhereNoRoutineReads_AreNotTaken()
        {
            CadCommandSession session = Fresh();

            Assert.False(session.FeedKey('A'));
            Assert.False(session.FeedMove(P(1, 1)));
            Assert.False(session.FeedRightClick(P(1, 1)));

            session.Feed("(getpoint \"P: \")");

            Assert.False(session.FeedKey('A'));
            Assert.False(session.FeedMove(P(1, 1)));
            Assert.False(session.FeedRightClick(P(1, 1)));
            Assert.Equal("P: ", session.PromptText);
        }

        #endregion

        #region Clicks, moves and the right button

        [Fact]
        public void AClick_GivesTheRawPointInTheUcs_WithNoSnap()
        {
            CadCommandSession session = Run(Session(Blank()), "LINE", "0,0", "100,0", Enter);
            session.Feed("(setq g (grread))");

            // 0.4 off the end of the line: object snap does not take it (the cursor of the click is given as it lies).
            Assert.True(session.Feed(P(99.6, 0.4)));

            Assert.Equal("(3 (99.6 0.4 0.0))", Value(session, "!g"));
        }

        [Fact]
        public void AClick_GivesTheCoordinatesOfTheCurrentUcs()
        {
            CadCommandSession session = Run(Session(Blank()), "UCS", "100,50", Enter);
            session.Feed("(setq g (grread))");

            session.Feed(P(110, 70));

            Assert.Equal("(3 (10.0 20.0 0.0))", Value(session, "!g"));
        }

        [Fact]
        public void AMoveWithTrack_GivesTheCursorAsAFive()
        {
            CadCommandSession session = Waiting("(setq g (grread T))");

            Assert.True(session.FeedMove(P(3, 4)));

            Assert.Equal("(5 (3.0 4.0 0.0))", Value(session, "!g"));
        }

        [Theory]
        [InlineData("(grread nil 1)")]
        [InlineData("(grread nil 3)")]
        [InlineData("(grread 1 0)")]
        public void AMoveIsGivenWhereTrackIsNotNilOrAllkeysHasTheBit1(string call)
        {
            CadCommandSession session = Waiting("(setq g " + call + ")");

            Assert.True(session.FeedMove(P(3, 4)));

            Assert.Equal("(5 (3.0 4.0 0.0))", Value(session, "!g"));
        }

        [Theory]
        [InlineData("(grread)")]
        [InlineData("(grread nil)")]
        [InlineData("(grread nil 0)")]
        [InlineData("(grread nil 2)")]
        public void AMoveWithNoTrack_IsNotTaken_AndTheRoutineKeepsWaiting(string call)
        {
            CadCommandSession session = Waiting("(setq g " + call + ")");

            Assert.False(session.FeedMove(P(3, 4)));
            Assert.True(session.IsRunning);

            // A key still answers it.
            Assert.True(session.FeedKey('q'));
            Assert.Equal("(2 113)", Value(session, "!g"));
        }

        [Fact]
        public void TheRightButton_GivesEleven_AndNothingElse()
        {
            CadCommandSession session = Waiting("(setq g (grread))");

            Assert.True(session.FeedRightClick(P(5, 5)));

            Assert.Equal("(11 0)", Value(session, "!g"));
        }

        [Fact]
        public void TwoMovesBeforeTheRoutineAsksAgain_KeepTheLast()
        {
            // The moves arrive while the routine runs the body of its loop (the function below feeds them from there): the second
            // takes the first's place, so the next grread gives the second alone.
            CadCommandSession session = Fresh();
            session.Lisp.Define("GHMOVES", 0, 0, a => session.Lisp.OnOwner(() =>
                {
                    session.FeedMove(P(1, 1));
                    session.FeedMove(P(2, 2));
                    return null;
                }));
            session.Feed("(progn (setq a (grread T)) (ghmoves) (setq b (grread T)) (list a b))");

            session.FeedKey('x');

            Assert.False(session.IsRunning);
            Assert.Equal("((2 120) (5 (2.0 2.0 0.0)))", session.History[session.History.Count - 1]);
        }

        [Fact]
        public void AClickAfterAMove_QueuesInOrder_AndAMoveAfterTheClickIsKept()
        {
            CadCommandSession session = Fresh();
            session.Lisp.Define("GHMOVES", 0, 0, a => session.Lisp.OnOwner(() =>
                {
                    session.FeedMove(P(1, 1));
                    session.Feed(P(5, 5));
                    session.FeedMove(P(2, 2));
                    session.FeedMove(P(3, 3));
                    return null;
                }));
            session.Feed("(progn (setq a (grread T)) (ghmoves) (list (grread T) (grread T) (grread T)))");

            session.FeedKey('x');

            Assert.False(session.IsRunning);
            Assert.Equal(
                "((5 (1.0 1.0 0.0)) (3 (5.0 5.0 0.0)) (5 (3.0 3.0 0.0)))",
                session.History[session.History.Count - 1]
            );
        }

        #endregion

        #region Esc

        [Fact]
        public void EscAtTheWait_EndsTheFormWithFunctionCancelled()
        {
            CadCommandSession session = Waiting("(grread)");

            session.Escape();

            Assert.False(session.IsRunning);
            Assert.Equal("; error: Function cancelled", session.History[session.History.Count - 1]);
        }

        [Fact]
        public void EscAtTheWait_IsCaughtByVlCatchAllApply()
        {
            CadCommandSession session = Waiting("(setq r (vl-catch-all-apply 'grread nil))");

            session.Escape();

            Assert.Equal("T", Value(session, "(vl-catch-all-error-p r)"));
            Assert.Equal("\"Function cancelled\"", Value(session, "(vl-catch-all-error-message r)"));
        }

        [Fact]
        public void EscAtTheWait_CallsTheErrorFunctionWithTheMessage()
        {
            CadCommandSession session = Fresh();
            Value(session, "(setq msg nil)");
            Value(session, "(defun *error* (m) (setq msg m))");
            session.Feed("(grread T)");

            session.Escape();

            Assert.Equal("\"Function cancelled\"", Value(session, "!msg"));
        }

        [Fact]
        public void EscDropsTheKeysThatWaited()
        {
            CadCommandSession session = Fresh();
            session.Feed("(progn (setq r (vl-catch-all-apply 'grread nil)) (setq g (grread)))");

            session.Escape();

            // The routine caught the Esc and reads again: it waits for the next key, none was kept.
            Assert.True(session.IsRunning);
            session.FeedKey('k');
            Assert.Equal("(2 107)", Value(session, "!g"));
        }

        #endregion

        #region Arguments

        [Theory]
        [InlineData("(grread 1 \"a\")", "; error: bad argument type: fixnump: \"a\"")]
        [InlineData("(grread 1 1.5)", "; error: bad argument type: fixnump: 1.5")]
        [InlineData("(grread 1 1 \"a\")", "; error: bad argument type: fixnump: \"a\"")]
        [InlineData("(grread 1 1 2 3)", "; error: too many arguments")]
        public void GrreadWithTheWrongArguments_GivesAnErrorAndDoesNotWait(string form, string error)
        {
            CadCommandSession session = Fresh();

            Assert.Equal(error, Value(session, form));
            Assert.False(session.IsRunning);
        }

        #endregion

        #region Loops

        private const string Drag =
            "(defun c:ghdrag ( / g) (while (and (setq g (grread T)) (/= 3 (car g))) (if (= 5 (car g)) (progn (redraw) (grvecs (list 1 '(0 0) (cadr g)))))) (setq *end* (cadr g)) (princ))";

        [Fact]
        public void ALoopThatDrawsWithGrvecsAtEachMove_LeavesTheLastVectorsAndTheDrawingAsItWas()
        {
            CadCommandSession session = Fresh();
            Value(session, Drag);
            int before = session.Document.Database.ModelSpace.Cast<object>().Count();

            // The c: command reads inside the command it started: the carrier suspends nothing, the loop runs on the answers.
            session.Feed("GHDRAG");
            Assert.True(session.IsRunning);

            for (int i = 1; i <= 5; i++)
            {
                Assert.True(session.FeedMove(P(i * 10, i * 5)));

                // redraw took the vectors of the move before off: one is on the screen.
                Assert.Single(session.TemporaryVectors);
                Assert.Equal(i * 10.0, session.TemporaryVectors[0].To.X, 9);
            }

            Assert.True(session.Feed(P(70, 35)));

            Assert.False(session.IsRunning);
            Assert.Single(session.TemporaryVectors);
            Assert.Equal("(70.0 35.0 0.0)", Value(session, "!*end*"));
            Assert.Equal(before, session.Document.Database.ModelSpace.Cast<object>().Count());
        }

        [Fact]
        public void AThousandMoves_RunTheLoopOnceEach_WellUnderASecond()
        {
            CadCommandSession session = Fresh();
            Value(session, Drag);
            session.Feed("GHDRAG");

            var watch = Stopwatch.StartNew();

            for (int i = 0; i < 1000; i++)
            {
                Assert.True(session.FeedMove(P(i, i / 2.0)));
            }

            watch.Stop();
            _output.WriteLine("1 000 moves of a grvecs loop: " + watch.ElapsedMilliseconds + " ms");
            session.Feed(P(0, 0));

            Assert.False(session.IsRunning);
            Assert.Single(session.TemporaryVectors);
            Assert.Equal(999.0, session.TemporaryVectors[0].To.X, 9);
        }

        [Fact]
        public void AKeyLoopTypedAtTheCommandPrompt_ReadsKeysUntilEnter()
        {
            CadCommandSession session = Fresh();
            session.Feed(
                "(progn (setq s \"\") (while (/= 13 (cadr (setq g (grread)))) (setq s (strcat s (chr (cadr g))))) s)"
            );

            foreach (char c in "Hi!")
            {
                Assert.True(session.FeedKey(c));
            }

            Assert.True(session.IsRunning);
            session.FeedKey(13);

            Assert.False(session.IsRunning);
            Assert.Equal("\"Hi!\"", session.History[session.History.Count - 1]);
        }

        [Fact]
        public void TheWaitShowsNoWordsAndWritesNoLine()
        {
            CadCommandSession session = Fresh();
            int lines = session.History.Count;

            session.Feed("(grread T)");

            Assert.Equal(lines + 1, session.History.Count);
            Assert.Equal(string.Empty, session.PromptText);
            Assert.True(session.Prompt.ReadsInput);
            Assert.True(session.Prompt.ReadsMoves);
            Assert.False(Waiting("(grread)").Prompt.ReadsMoves);
        }

        #endregion
    }
}
