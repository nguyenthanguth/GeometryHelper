using System;
using System.Collections.Generic;
using GeometryHelper.Cad.Lisp;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Ends the routines a test leaves waiting. A routine of AutoLISP that asks the user (<c>getpoint</c>, a PAUSE of
    /// <c>command</c>) keeps a worker thread with a stack of 256 MB blocked until it is answered or cancelled, and a test that
    /// ends with the question still up would keep it for the rest of the run. A test class that makes such sessions
    /// implements <see cref="IDisposable"/> and calls <see cref="EndAll"/>: <see cref="EditingDrawings.Session"/> and the
    /// tests' own interpreters are remembered here (weakly, so a session of any other test is not kept alive), and each one
    /// still waiting is cancelled the way closing a document does it. The list is the thread's own, because xUnit runs the
    /// classes side by side and a test begins, runs and ends on one thread.
    /// </summary>
    internal static class WaitingRoutines
    {
        // Presses of Esc, or cancelled waits, before a routine that will not end is given up: the same few as the window's.
        private const int MostPresses = 32;

        // Past this many entries the sessions already collected are dropped from the list before the next is added.
        private const int PruneAt = 256;

        [ThreadStatic]
        private static List<WeakReference> _tracked;

        /// <summary>Remembers a session so that <see cref="EndAll"/> can end the routine it leaves waiting.</summary>
        /// <param name="session">The session a test made.</param>
        /// <returns>The same session.</returns>
        internal static CadCommandSession Track(CadCommandSession session) => Remember(session);

        /// <summary>Remembers an interpreter that has no session, so that <see cref="EndAll"/> can cancel its wait.</summary>
        /// <param name="lisp">The interpreter a test made.</param>
        /// <returns>The same interpreter.</returns>
        internal static LispInterpreter Track(LispInterpreter lisp) => Remember(lisp);

        private static T Remember<T>(T routine)
            where T : class
        {
            _tracked ??= new List<WeakReference>();

            if (_tracked.Count >= PruneAt)
            {
                _tracked.RemoveAll(reference => !reference.IsAlive);
            }

            _tracked.Add(new WeakReference(routine));
            return routine;
        }

        /// <summary>
        /// Ends the evaluation each session and interpreter of this thread still waits in, as closing a document does: the
        /// routine is aborted so that one which catches <c>Function cancelled</c> and asks again cannot hold on, then Esc is
        /// pressed (a session) or the wait cancelled (an interpreter) until it is over, at most 32 times. What is not waiting is
        /// left as the test left it. The list is emptied.
        /// </summary>
        internal static void EndAll()
        {
            List<WeakReference> tracked = _tracked;
            _tracked = null;

            if (tracked == null)
            {
                return;
            }

            foreach (WeakReference reference in tracked)
            {
                switch (reference.Target)
                {
                    case CadCommandSession session:
                        End(session);
                        break;
                    case LispInterpreter lisp:
                        End(lisp);
                        break;
                }
            }
        }

        private static void End(CadCommandSession session)
        {
            if (!session.IsLispWaiting)
            {
                return;
            }

            session.AbortLisp();

            for (int press = 0; press < MostPresses && session.IsLispWaiting; press++)
            {
                session.Escape();
            }
        }

        private static void End(LispInterpreter lisp)
        {
            if (!lisp.IsWaiting)
            {
                return;
            }

            lisp.Abort();

            for (int cancel = 0; cancel < MostPresses && lisp.IsWaiting; cancel++)
            {
                lisp.CancelWait();
            }
        }
    }
}
