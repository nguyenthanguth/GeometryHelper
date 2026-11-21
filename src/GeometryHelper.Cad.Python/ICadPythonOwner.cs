using System;

namespace GeometryHelper.Cad.Python
{
    /// <summary>The thread that owns the drawings, as the host of a Python run sees it.</summary>
    public interface ICadPythonOwner
    {
        /// <summary>Runs work on the owner thread after the call that posts it has returned (never inline).</summary>
        /// <param name="work">The work.</param>
        void Post(Action work);

        /// <summary>On the owner thread: lets the user answer (pick, type, Esc) until <paramref name="done"/> is true.</summary>
        /// <param name="done">Whether the wait is over.</param>
        void WaitUntil(Func<bool> done);
    }
}
