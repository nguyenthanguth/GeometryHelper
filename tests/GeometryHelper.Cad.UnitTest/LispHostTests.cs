using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.UnitTest;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The hand-off between the thread that evaluates AutoLISP and the thread that owns the document (spec decisions D34 and
    /// D35, section 3): a built-in asks for the owner with <c>OnOwner</c> or for the user with <c>Ask</c>, the evaluation
    /// returns to its caller while it waits, and <c>Resume</c> or <c>CancelWait</c> goes on with it. The built-ins these tests
    /// need are defined here on a bare interpreter, so nothing of the drawing is involved. Each test runs its caller on a
    /// thread of its own, with a time limit, because the fault such a hand-off has is a deadlock.
    /// </summary>
    public sealed class LispHostTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-host-" + Guid.NewGuid().ToString("N")
        );

        public LispHostTests()
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

        // Runs the work on a new thread, which is the owner of everything the work does, and gives what it returned. A hand-off
        // that never comes back fails the test after 20 seconds instead of hanging the run; what the work threw is thrown again.
        private static T Owned<T>(Func<T> work, out int ownerId)
        {
            T result = default;
            Exception failure = null;
            int id = 0;
            var thread = new Thread(() =>
            {
                id = Thread.CurrentThread.ManagedThreadId;

                try
                {
                    result = work();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            })
            {
                IsBackground = true,
            };

            thread.Start();
            bool finished = thread.Join(TimeSpan.FromSeconds(20));
            Assert.True(finished, "the evaluation did not come back within 20 seconds (a deadlock in the hand-off?)");

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            ownerId = id;
            return result;
        }

        private static T Owned<T>(Func<T> work) => Owned(work, out _);

        // A bare interpreter whose wait Dispose cancels, where the test leaves it waiting.
        private static LispInterpreter Interpreter() => WaitingRoutines.Track(new LispInterpreter());

        // ask-point and the like: a built-in that waits for the user, asking as a get function would.
        private static LispInterpreter Asking()
        {
            var lisp = Interpreter();
            lisp.Define("ASK", 0, 1, a =>
                lisp.Ask(
                    new LispAsk { Kind = a.Length == 0 ? LispAskKind.Point : (LispAskKind)(int)a[0], Message = "P: " }
                )
            );
            return lisp;
        }

        private static string Shown(object value) => LispInterpreter.Print(value, true);

        [Fact]
        public void AWorkHandedToTheOwner_RunsOnTheThreadThatCalledRun()
        {
            // spec D34: the owner (the caller of Run) does the document's work
            var lisp = Interpreter();
            lisp.Define("OWNER-ID", 0, 0, a => lisp.OnOwner(() => Thread.CurrentThread.ManagedThreadId));

            LispResult result = Owned(() => lisp.Run("(owner-id)"), out int ownerId);

            Assert.Equal(ownerId, Assert.IsType<int>(result.Value));
        }

        [Fact]
        public void TheBuiltinsOfAnEvaluation_RunOnAThreadOfTheirOwn()
        {
            // L1 decision 3, which D34 keeps: the worker evaluates, so a deep recursion has a stack of its own
            var lisp = Interpreter();
            lisp.Define("WORKER-ID", 0, 0, a => Thread.CurrentThread.ManagedThreadId);

            LispResult result = Owned(() => lisp.Run("(worker-id)"), out int callerId);

            Assert.NotEqual(callerId, Assert.IsType<int>(result.Value));
        }

        [Fact]
        public void TheWorkerThread_IsABackgroundThread_SoAnEvaluationLeftWaitingDoesNotKeepTheProcessAlive()
        {
            // spec D34: worker threads are background threads (a drawing closed while a routine waits must not hold the process)
            var lisp = Interpreter();
            lisp.Define("BACKGROUND", 0, 0, a => lisp.Bool(Thread.CurrentThread.IsBackground));

            LispResult result = Owned(() => lisp.Run("(background)"));

            Assert.Equal("T", Shown(result.Value));
        }

        [Fact]
        public void AWorkHandedToTheOwner_GivesItsValueBackToTheRoutine()
        {
            var lisp = Interpreter();
            lisp.Define("OWNER-DOUBLE", 1, 1, a => lisp.OnOwner(() => (int)a[0] * 2));

            LispResult result = Owned(() => lisp.Run("(+ 1 (owner-double 21))"));

            Assert.Equal(43, Assert.IsType<int>(result.Value));
        }

        [Fact]
        public void AWorkHandedToTheOwnerThatThrowsALispException_IsAnErrorOfTheRoutine()
        {
            // spec D34: the owner hands the result, or the LispException, back
            var lisp = Interpreter();
            lisp.Define("OWNER-FAILS", 0, 0, a => lisp.OnOwner(() => throw new LispException("owner failed")));

            LispResult result = Owned(() => lisp.Run("(owner-fails)"));

            Assert.True(result.Failed);
            Assert.Equal("owner failed", result.Error);
            Assert.Equal("; error: owner failed", result.Output);
        }

        [Fact]
        public void AWorkOnTheOwnerThatHandsAnotherWorkToTheOwner_RunsItAtOnce()
        {
            // spec section 3: OnOwner on the owner itself runs at once, so a session function may call another that asks
            var lisp = Interpreter();
            lisp.Define("NESTED", 0, 0, a => lisp.OnOwner(() => lisp.OnOwner(() => 5)));

            LispResult result = Owned(() => lisp.Run("(nested)"));

            Assert.Equal(5, Assert.IsType<int>(result.Value));
        }

        [Fact]
        public void TwoHundredHandOffs_NeverRunTheWorkerAndTheOwnerAtOnce()
        {
            // spec D34: exactly one of the two threads runs at any time, so session code needs no locks
            var lisp = Interpreter();
            int active = 0;
            int most = 0;
            int ran = 0;

            void Probe()
            {
                int now = Interlocked.Increment(ref active);
                int seen;

                while (now > (seen = Volatile.Read(ref most)))
                {
                    Interlocked.CompareExchange(ref most, now, seen);
                }

                Thread.Sleep(1);
                Interlocked.Increment(ref ran);
                Interlocked.Decrement(ref active);
            }

            lisp.Define("PROBE", 0, 0, a =>
            {
                Probe();
                lisp.OnOwner(() =>
                {
                    Probe();
                    return null;
                });
                return null;
            });

            Owned(() => lisp.Run("(repeat 200 (probe))"));

            Assert.Equal(1, most);
            Assert.Equal(400, ran);
        }

        [Fact]
        public void TwoThousandHandOffs_AreWellUnderTwoSeconds()
        {
            var lisp = Interpreter();
            lisp.Define("OWNER-ADD", 2, 2, a => lisp.OnOwner(() => (int)a[0] + (int)a[1]));
            LispResult result = null;

            SpeedGuard.AssertWithin(2.0, () => result = Owned(() => lisp.Run("(repeat 2000 (owner-add 1 2))")));

            Assert.False(result.Failed);
        }

        [Fact]
        public void AMillionStepLoopOnASessionsInterpreter_StillRunsUnderTwoSeconds()
        {
            // AutoCAD 2021: (repeat 1000000 (setq i (1+ i))) 157 ms; L1's loop test on a bare interpreter, here on the one a
            // session makes (which has the drawing's functions and the hand-off)
            string output = null;

            SpeedGuard.AssertWithin(
                2.0,
                () => new CadCommandSession(Blank()).Lisp,
                lisp =>
                {
                    lisp.Run("(setq i 0)");
                    output = lisp.Run("(repeat 1000000 (setq i (1+ i)))").Output;
                }
            );

            Assert.Equal("1000000", output);
        }

        [Fact]
        public void AnAsk_ReturnsToTheCallerWithTheEvaluationWaiting_AndKeepsWhatWasAsked()
        {
            // spec D35: Run returns while the routine waits, the output so far in the result and the ask in Waiting
            LispInterpreter lisp = Asking();

            LispResult result = Owned(() => lisp.Run("(princ \"a\") (ask) (princ \"b\")"));

            Assert.True(result.IsWaiting);
            Assert.False(result.Failed);
            Assert.Equal("a", result.Output);
            Assert.True(lisp.IsWaiting);
            Assert.Equal(LispAskKind.Point, lisp.Waiting.Kind);
            Assert.Equal("P: ", lisp.Waiting.Message);
        }

        [Fact]
        public void AnAnswer_ResumesTheRoutineWithItsValue_AndTheRoutineGoesOnToTheEnd()
        {
            LispInterpreter lisp = Asking();
            var point = new LispCons(3.0, new LispCons(4.0, new LispCons(0.0, null)));

            LispResult after = Owned(() =>
            {
                lisp.Run("(setq p (ask)) (princ \"b\")");
                return lisp.Resume(point);
            });

            Assert.False(after.IsWaiting);
            Assert.False(after.Failed);
            Assert.Contains("b", after.Output);
            Assert.False(lisp.IsWaiting);
            Assert.Null(lisp.Waiting);
            Assert.Equal("(3.0 4.0 0.0)", Shown(lisp.GetValue("P")));
        }

        [Theory]
        [InlineData((int)LispAskKind.Distance, 2.5, "2.5")]
        [InlineData((int)LispAskKind.Angle, 1.5, "1.5")]
        [InlineData((int)LispAskKind.Real, 2.0, "2.0")]
        [InlineData((int)LispAskKind.Integer, 7, "7")]
        [InlineData((int)LispAskKind.String, "hello", "\"hello\"")]
        [InlineData((int)LispAskKind.Keyword, "Yes", "\"Yes\"")]
        [InlineData((int)LispAskKind.Pause, null, "nil")]
        public void TheAnswerOfEachKind_IsTheValueTheAskGives(int kind, object answer, string shown)
        {
            // spec section 3: Distance, Angle, Orient and Real a double, Integer an int, String and Keyword a string, Pause null
            LispInterpreter lisp = Asking();

            Owned(() =>
            {
                lisp.Run("(setq v (ask " + kind + "))");
                return lisp.Resume(answer);
            });

            Assert.Equal(shown, Shown(lisp.GetValue("V")));
        }

        [Fact]
        public void ARoutineThatAsksTwice_WaitsAgainAtTheSecondAsk_AndItsAnswersGoToTheRightAsks()
        {
            LispInterpreter lisp = Asking();

            Owned(() =>
            {
                LispResult first = lisp.Run("(setq a (ask 5)) (setq b (ask 6))");

                Assert.True(first.IsWaiting);
                Assert.Equal(LispAskKind.Integer, lisp.Waiting.Kind);

                LispResult second = lisp.Resume(11);

                Assert.True(second.IsWaiting);
                Assert.Equal(LispAskKind.Real, lisp.Waiting.Kind);

                LispResult last = lisp.Resume(2.5);

                Assert.False(last.IsWaiting);
                return last;
            });

            Assert.Equal("(11 2.5)", Shown(lisp.Run("(list a b)").Value));
        }

        [Fact]
        public void CancelWait_ThrowsFunctionCancelledAtTheWait()
        {
            // AutoCAD 2021 (q3): Esc at a get function is ; error: Function cancelled
            LispInterpreter lisp = Asking();

            LispResult result = Owned(() =>
            {
                lisp.Run("(princ \"a\") (ask) (princ \"after\")");
                return lisp.CancelWait();
            });

            Assert.True(result.Failed);
            Assert.False(result.IsWaiting);
            Assert.Equal("Function cancelled", result.Error);
            Assert.Contains("; error: Function cancelled", result.Output);
            Assert.DoesNotContain("after", result.Output);
            Assert.False(lisp.IsWaiting);
        }

        [Fact]
        public void CancelWaitOfACommandWithItsOwnErrorFunction_CallsItWithTheMessage()
        {
            // AutoCAD 2021 (q3, GHX): X:Function cancelled, the routine's *error* local to it and called with the message
            LispInterpreter lisp = Asking();
            lisp.Run(
                "(defun c:ghx (/ *error*) (defun *error* (m) (princ (strcat \"X:\" m)) (princ)) (ask) (princ \"after\"))"
            );

            LispResult result = Owned(() =>
            {
                lisp.RunCommand("ghx");
                return lisp.CancelWait();
            });

            Assert.Contains("X:Function cancelled", result.Output);
            Assert.DoesNotContain("after", result.Output);
        }

        [Fact]
        public void AfterTheWaitEnds_TheInterpreterRunsAgain_WhetherItWasAnsweredOrCancelled()
        {
            LispInterpreter answered = Asking();
            LispInterpreter cancelled = Asking();

            LispResult one = Owned(() =>
            {
                answered.Run("(ask)");
                answered.Resume(null);
                return answered.Run("(+ 1 2)");
            });
            LispResult two = Owned(() =>
            {
                cancelled.Run("(ask)");
                cancelled.CancelWait();
                return cancelled.Run("(+ 1 2)");
            });

            Assert.Equal(3, Assert.IsType<int>(one.Value));
            Assert.Equal(3, Assert.IsType<int>(two.Value));
            Assert.False(answered.IsWaiting);
            Assert.False(cancelled.IsWaiting);
        }

        [Fact]
        public void RunWhileAnEvaluationWaits_IsRefusedWithCantReenterLisp_AndTheEvaluationStillWaits()
        {
            // spec D36: host calls of Run while an evaluation waits are refused with Can't reenter LISP. (the form of the
            // refusal in a LispResult is our reading: a failed result with that error)
            LispInterpreter lisp = Asking();

            (LispResult refused, bool waiting, LispResult finished) = Owned(() =>
            {
                lisp.Run("(setq r 1) (ask) (setq r 2)");
                LispResult second = lisp.Run("(setq r 99)");
                bool stillWaiting = lisp.IsWaiting;
                return (second, stillWaiting, lisp.Resume(null));
            });

            Assert.True(refused.Failed);
            Assert.Equal("Can't reenter LISP.", refused.Error);
            Assert.True(waiting);
            Assert.False(finished.Failed);
            Assert.Equal(2, Assert.IsType<int>(lisp.GetValue("R")));
        }

        [Fact]
        public void RunCommandAndLoad_ReturnToTheCallerWhenTheRoutineWaits_AndAreRefusedWhileOneWaits()
        {
            // spec D35 and D36: RunCommand and Load return with the evaluation waiting, and while it waits they are refused
            LispInterpreter lisp = Asking();
            string path = Path.Combine(_folder, "waits.lsp");
            File.WriteAllText(path, "(ask)\n(setq loaded 1)\n", new UTF8Encoding(false));
            lisp.Run("(defun c:ghw () (ask) (setq ran 1) (princ))");

            LispResult[] results = Owned(() =>
            {
                LispResult command = lisp.RunCommand("ghw");
                LispResult refusedLoad = lisp.Load(path);
                LispResult refusedCommand = lisp.RunCommand("ghw");
                lisp.Resume(null);
                LispResult load = lisp.Load(path);
                LispResult loaded = lisp.Resume(null);
                return new[] { command, refusedLoad, refusedCommand, load, loaded };
            });

            Assert.True(results[0].IsWaiting);
            Assert.Equal("Can't reenter LISP.", results[1].Error);
            Assert.Equal("Can't reenter LISP.", results[2].Error);
            Assert.Equal(1, Assert.IsType<int>(lisp.GetValue("RAN")));
            Assert.True(results[3].IsWaiting);
            Assert.False(results[4].IsWaiting);
            Assert.False(results[4].Failed);
            Assert.Equal(1, Assert.IsType<int>(lisp.GetValue("LOADED")));
        }

        [Fact]
        public void TheFieldsOfAnAsk_AreWhatTheSessionReads()
        {
            // spec section 3: Waiting is the LispAsk the built-in made, with its message, base point, flags and keywords
            var lisp = Interpreter();
            var made = new LispAsk
            {
                Kind = LispAskKind.Distance,
                Message = "\nLen: ",
                Flags = 7,
                Keywords = new[] { "Up", "Down" },
                Spaces = true,
            };
            lisp.Define("ASK-WITH", 0, 0, a => lisp.Ask(made));

            Owned(() => lisp.Run("(ask-with)"));

            Assert.Same(made, lisp.Waiting);
            Assert.Equal("\nLen: ", lisp.Waiting.Message);
            Assert.Equal(7, lisp.Waiting.Flags);
            Assert.Equal(new[] { "Up", "Down" }, lisp.Waiting.Keywords);
            Assert.True(lisp.Waiting.Spaces);
        }
    }
}
