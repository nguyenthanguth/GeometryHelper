using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Five faults a review of the AutoLISP wait found, each with the routine that showed it: Esc at a get inside a catch-all
    /// loop, an <c>*error*</c> that starts a command, the undo step left open when a transparent wait ends, a load refused
    /// while a routine waits, and a built-in that fails with a .NET exception inside a function with local variables.
    /// </summary>
    [Collection("LispSerial")]
    public sealed class LispReviewFixTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-review-" + Guid.NewGuid().ToString("N")
        );

        public LispReviewFixTests()
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

        // Runs the action on a thread of its own and says whether it came back in time; a hung one is left behind.
        private static bool Within(int milliseconds, Action action)
        {
            var thread = new Thread(() => action()) { IsBackground = true };
            thread.Start();
            return thread.Join(milliseconds);
        }

        private static int Count<T>(CadCommandSession session)
            where T : DxfEntity => All<T>(session.Document.ModelSpace).Count;

        private const string Loopy =
            "(defun c:loopy (/ r) (while (vl-catch-all-error-p (setq r (vl-catch-all-apply 'getpoint '(\"P: \"))))) (princ))";

        [Fact]
        public void EscAtAGetThatARoutineCatchesInALoop_CancelsThatOneAskAndReturns()
        {
            CadCommandSession session = Run(Session(Blank()), Loopy, "LOOPY");

            Assert.True(Within(5000, () => session.Escape()), "Esc did not return");

            Assert.True(session.IsLispWaiting);
            Assert.Equal("P: ", session.PromptText);
        }

        [Fact]
        public void TheLoopThatEscKeepsAsking_EndsWhenAPointIsGiven()
        {
            CadCommandSession session = Run(Session(Blank()), Loopy, "LOOPY");
            Assert.True(Within(5000, () => Run(session, Esc, Esc)));

            Run(session, "1,1");

            Assert.False(session.IsLispWaiting);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void EscAtAGetThatARoutineCatches_LetsTheNextGetAskItsQuestion()
        {
            CadCommandSession session = Run(
                Session(Blank()),
                "(progn (vl-catch-all-apply 'getpoint '(\"A: \")) (setq b (getpoint \"B: \")) (princ \"after B\"))",
                Esc
            );

            Assert.True(session.IsLispWaiting);
            Assert.Equal("B: ", session.PromptText);

            Run(session, "2,3");

            Assert.False(session.IsLispWaiting);
            Assert.Contains(session.History, l => l.StartsWith("after B", StringComparison.Ordinal));
            Assert.Equal("(2.0 3.0 0.0)", LispInterpreter.Print(session.Lisp.GetValue("B"), true));
        }

        [Fact]
        public void AbortingTheRoutine_LetsEscEndEvenAnAskThatIsCaughtAndAskedAgain()
        {
            CadCommandSession session = Run(Session(Blank()), Loopy, "LOOPY");

            Assert.True(Within(5000, () =>
                {
                    session.AbortLisp();

                    for (int press = 0; press < 32 && (session.IsRunning || session.IsLispWaiting); press++)
                    {
                        session.Escape();
                    }
                }));

            Assert.False(session.IsLispWaiting);
            Assert.False(session.IsRunning);
            Assert.False(session.Lisp.IsEvaluating);
            Assert.DoesNotContain(session.History, l => l.Contains("Hard error"));
        }

        [Fact]
        public void AfterAnAbort_TheInterpreterEvaluatesAgainAndAsksAgain()
        {
            CadCommandSession session = Run(Session(Blank()), Loopy, "LOOPY");
            session.AbortLisp();
            Assert.True(Within(5000, () => Run(session, Esc, Esc)));
            Assert.False(session.Lisp.IsEvaluating);

            Run(session, "(setq q (getpoint \"Again: \"))");

            Assert.True(session.IsLispWaiting);
            Assert.Equal("Again: ", session.PromptText);
        }

        [Fact]
        public void AnErrorHandlerThatStartsACommand_LeavesTheCommandRunningAfterEsc()
        {
            CadCommandSession session = Run(
                Session(Blank()),
                "(defun *error* (m) (command \"LINE\" \"0,0\") (princ))",
                "(getpoint \"P: \")",
                Esc
            );

            Assert.True(session.IsRunning);
            Assert.Equal("LINE", session.Current.Name);
            Assert.False(session.IsLispWaiting);
        }

        [Fact]
        public void ACommandOfAnErrorHandler_IsItsOwnUndoStepAndTheNextCommandIsAnother()
        {
            CadCommandSession session = Run(
                Session(Blank()),
                "(defun *error* (m) (command \"LINE\" \"0,0\" \"5,5\") (princ))",
                "(getpoint \"P: \")",
                Esc
            );
            Assert.Equal(1, Count<DxfLine>(session));

            Run(session, Esc, "CIRCLE", "0,0", "2");
            Assert.Equal(1, Count<DxfCircle>(session));
            Assert.True(session.Document.CanUndo);
            session.Document.Undo();

            Assert.Equal(0, Count<DxfCircle>(session));
            Assert.Equal(1, Count<DxfLine>(session));
        }

        [Fact]
        public void ATransparentWaitThatMadeAnEditAndIsCancelled_EndsItsUndoStepOnce()
        {
            // The logger is the process's, and the test classes run side by side: only what this thread writes is the session's
            // (the owner thread ends the undo step), so another test's line is neither added to the list nor read as a Warn.
            // A worker another test left behind can still reach the writer while this one reads the list, so both take the lock.
            var logs = new List<string>();
            object gate = new object();
            int owner = Thread.CurrentThread.ManagedThreadId;
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, error) =>
            {
                if (Thread.CurrentThread.ManagedThreadId == owner)
                {
                    lock (gate)
                    {
                        logs.Add(level + " " + message + " " + error?.Message);
                    }
                }
            };

            try
            {
                CadCommandSession session = Run(Session(Blank()), "LINE", "0,0");
                session.Lisp.Define("GHMAKE", 0, 0, a => session.Lisp.OnOwner(() =>
                    {
                        session.OpenLispStep();
                        session.Document.ApplyEdit("t", null, scope =>
                            scope.Append(
                                session.Document.ModelSpace,
                                new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.0)
                            )
                        );
                        return null;
                    }));

                Run(session, "(progn (getpoint \"a: \") (ghmake) (getpoint \"b: \"))", "1,1", Esc);

                string[] lines;
                lock (gate)
                {
                    lines = logs.ToArray();
                }

                string[] warned = lines
                    .Where(l =>
                        !l.StartsWith("Info", StringComparison.Ordinal)
                        && !l.StartsWith("Debug", StringComparison.Ordinal)
                    )
                    .ToArray();
                Assert.Empty(warned);
                Assert.Equal(1, Count<DxfCircle>(session));
                Assert.True(session.IsRunning);
                Assert.True(session.Document.CanUndo);
                session.Document.Undo();
                Assert.Equal(0, Count<DxfCircle>(session));
            }
            finally
            {
                GeometryHelperLog.Writer = null;
                GeometryHelperLog.Enable = false;
            }
        }

        [Fact]
        public void ALoadAskedWhileARoutineWaitsInsideLine_IsRefusedAndTheAnswerStillGoesToLine()
        {
            string path = Path.Combine(_folder, "refused.lsp");
            File.WriteAllText(path, "(setq zz 1)");
            CadCommandSession session = Run(Session(Blank()), "LINE", "0,0", "(getpoint \"X: \")");

            bool loaded = session.LoadLisp(path);

            Assert.False(loaded);
            Assert.Equal("Can't reenter LISP.", session.History[session.History.Count - 1]);
            Assert.True(session.IsLispWaiting);
            Assert.Null(session.Lisp.GetValue("ZZ"));

            Run(session, "5,5");

            Assert.Equal(1, Count<DxfLine>(session));
            DxfLine line = All<DxfLine>(session.Document.ModelSpace)[0];
            Assert.Equal(5.0, line.EndPoint.X, 3);
            Assert.Equal(5.0, line.EndPoint.Y, 3);
        }

        [Fact]
        public void ALoadAskedWhileARoutineWaitsAtTheCommandPrompt_LeavesTheWaitAsItWas()
        {
            string path = Path.Combine(_folder, "refused2.lsp");
            File.WriteAllText(path, "(setq zz 1)");
            CadCommandSession session = Run(Session(Blank()), "(setq p (getpoint \"X: \"))");

            Assert.False(session.LoadLisp(path));

            Assert.True(session.IsLispWaiting);
            Assert.Equal("X: ", session.PromptText);
            Assert.Equal(1, session.History.Count(l => l == "Can't reenter LISP."));

            Run(session, "5,5");

            Assert.Equal("(5.0 5.0 0.0)", LispInterpreter.Print(session.Lisp.GetValue("P"), true));
        }

        [Fact]
        public void ABuiltInThatThrowsADotNetException_GivesBackTheLocalsOfTheFunctionItFailedIn()
        {
            var lisp = new LispInterpreter();
            lisp.Define("GHBOOM", 0, 0, a => throw new InvalidOperationException("boom"));
            lisp.Run("(setq x 1)");

            LispResult result = lisp.Run("(defun f (/ x) (setq x 5) (ghboom)) (f)");

            Assert.True(result.Failed);
            Assert.Equal("internal error: boom", result.Error);
            Assert.Equal("; error: internal error: boom", result.Output);
            Assert.Equal(1, lisp.GetValue("X"));
        }

        [Fact]
        public void ABuiltInThatThrowsADotNetException_CanBeCaughtByTheRoutine()
        {
            var lisp = new LispInterpreter();
            lisp.Define("GHBOOM", 0, 0, a => throw new InvalidOperationException("boom"));

            LispResult result = lisp.Run("(vl-catch-all-error-message (vl-catch-all-apply 'ghboom nil))");

            Assert.False(result.Failed);
            Assert.Equal("internal error: boom", result.Value);
        }
    }
}
