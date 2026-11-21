using System;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The helper the Lisp tests end with, which cancels a routine a test left waiting so that its worker thread, with a
    /// stack of 256 MB, does not outlive the test. Each test leaves a routine waiting on purpose and then asks the helper to
    /// end it.
    /// </summary>
    public sealed class WaitingRoutinesTests : IDisposable
    {
        // Ends what an assertion that failed left behind.
        public void Dispose() => WaitingRoutines.EndAll();

        [Fact]
        public void ASessionWithAGetPointWaiting_IsNotWaitingAfterEndAll()
        {
            CadCommandSession session = Run(Session(Blank()), "(getpoint \"P: \")");
            Assert.True(session.IsLispWaiting);

            WaitingRoutines.EndAll();

            Assert.False(session.IsLispWaiting);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ARoutineThatCatchesEscAndAsksAgain_IsNotWaitingAfterEndAll()
        {
            // the loop of the reviewer's first fault: Esc is caught and the routine asks the next question at once
            CadCommandSession session = Session(Blank());
            Run(
                session,
                "(defun c:loopy () (while (vl-catch-all-error-p (vl-catch-all-apply 'getpoint '(\"P: \")))) (princ))",
                "LOOPY",
                Esc
            );
            Assert.True(session.IsLispWaiting);

            WaitingRoutines.EndAll();

            Assert.False(session.IsLispWaiting);
        }

        [Fact]
        public void ABareInterpreterWaitingForTheUser_IsNotWaitingAfterEndAll()
        {
            var lisp = WaitingRoutines.Track(new LispInterpreter());
            lisp.Define("ASK", 0, 0, a => lisp.Ask(new LispAsk { Kind = LispAskKind.Point, Message = "P: " }));
            LispResult first = lisp.Run("(ask)");
            Assert.True(first.IsWaiting);
            Assert.True(lisp.IsWaiting);

            WaitingRoutines.EndAll();

            Assert.False(lisp.IsWaiting);
        }

        [Fact]
        public void ASessionWithACommandRunningAndNoRoutineWaiting_IsLeftRunning()
        {
            CadCommandSession session = Run(Session(Blank()), "LINE", "0,0");

            WaitingRoutines.EndAll();

            Assert.True(session.IsRunning);
        }
    }
}
