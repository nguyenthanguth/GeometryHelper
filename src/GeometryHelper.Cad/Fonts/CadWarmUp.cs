using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// Reads a list ahead, so that the first window that shows it finds it ready: the work runs on a thread of its own at
    /// below normal priority, never on the caller's thread, and a failure is logged and not thrown. A window opened while the
    /// work still runs waits on the same work through the cache it fills (a <see cref="Lazy{T}"/>), and never repeats it.
    /// </summary>
    internal static class CadWarmUp
    {
        /// <summary>Starts the work on a thread of its own, below normal priority, and gives back at once.</summary>
        /// <param name="what">What is read, for the log.</param>
        /// <param name="work">The work; it fills a cache and gives nothing back.</param>
        /// <returns>The task, complete when the work is done or failed.</returns>
        internal static Task Run(string what, Action work) =>
            Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        // The task has a thread to itself (LongRunning), so its priority is its own to set.
                        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                        var watch = Stopwatch.StartNew();
                        work();
                        GeometryHelperLog.Debug(
                            "CadWarmUp: " + what + " read ahead in " + watch.ElapsedMilliseconds + " ms."
                        );
                    }
                    catch (Exception exception)
                    {
                        GeometryHelperLog.Warn("CadWarmUp: " + what + " could not be read ahead.", exception);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            );
    }
}
