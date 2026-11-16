using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Xunit;
using Xunit.Sdk;

namespace GeometryHelper.UnitTest
{
    /// <summary>
    /// Times a piece of work against a limit in a way a busy machine does not fail. A limit in these tests is a ceiling that a
    /// complexity blow-up or a hang crosses by a wide margin, but one run beside several builds can take three times as long as
    /// alone, and a collection owed by the test before can land in it. So the work is run again, up to the attempts given, until
    /// one run is within the limit; a slowdown that is real is slow in every run and fails after the last, while a hiccup of the
    /// machine passes at the next. The file is linked into each test project that times work, not copied.
    /// </summary>
    internal static class SpeedGuard
    {
        /// <summary>
        /// Runs the work, each run after a full collection so that what an earlier test left to collect is not counted against
        /// it, until one run takes less than the limit or the attempts are used up, and fails the test when none did.
        /// </summary>
        /// <param name="seconds">The seconds a run must take less than.</param>
        /// <param name="work">The work: the whole of it is timed, and it may be run more than once, so it must start from the same state each time.</param>
        /// <param name="attempts">The most runs: three when not given.</param>
        /// <returns>The shortest time of the runs made.</returns>
        public static TimeSpan AssertWithin(double seconds, Action work, int attempts = 3) =>
            AssertWithin(seconds, () => work, attempts);

        /// <summary>
        /// The same for work that changes what it works on: each run gets its own state from <paramref name="prepare"/>, which
        /// is not timed.
        /// </summary>
        /// <typeparam name="T">What the work works on.</typeparam>
        /// <param name="seconds">The seconds a run must take less than.</param>
        /// <param name="prepare">Makes what one run works on.</param>
        /// <param name="work">The work, timed.</param>
        /// <param name="attempts">The most runs: three when not given.</param>
        /// <returns>The shortest time of the runs made.</returns>
        public static TimeSpan AssertWithin<T>(double seconds, Func<T> prepare, Action<T> work, int attempts = 3) =>
            AssertWithin(
                seconds,
                () =>
                {
                    T state = prepare();

                    return () => work(state);
                },
                attempts
            );

        /// <summary>
        /// Writes the times of every run for a failure message, in seconds to two places: "0.31 s, 0.29 s, 0.30 s". The tests that
        /// judge a ratio or keep their own runs put it in the message of the assertion, so that a failure shows every run and not
        /// only the best.
        /// </summary>
        /// <param name="milliseconds">The time of each run, in milliseconds, in the order they ran.</param>
        /// <returns>The times, joined.</returns>
        public static string Describe(IEnumerable<double> milliseconds) =>
            string.Join(
                ", ",
                milliseconds.Select(t => (t / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " s")
            );

        private static TimeSpan AssertWithin(double seconds, Func<Action> prepare, int attempts)
        {
            TimeSpan limit = TimeSpan.FromSeconds(seconds);
            var times = new TimeSpan[attempts];
            int runs = 0;

            while (runs < attempts)
            {
                try
                {
                    Action run = prepare();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    var watch = Stopwatch.StartNew();
                    run();
                    watch.Stop();
                    times[runs] = watch.Elapsed;
                }
                catch (Exception ex) when (runs > 0 && !(ex is XunitException))
                {
                    // A machine short of memory or disk fails in the middle of a run: say what the runs before it took, which the
                    // exception alone does not.
                    throw new XunitException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Run {0} of {1} threw {2}: {3} The {4} before it took {5} (the limit being {6:0.00} s).",
                            runs + 1,
                            attempts,
                            ex.GetType().Name,
                            ex.Message,
                            runs == 1 ? "run" : runs + " runs",
                            Describe(times.Take(runs).Select(t => t.TotalMilliseconds)),
                            seconds
                        ),
                        ex
                    );
                }

                if (times[runs++] < limit)
                {
                    return times[runs - 1];
                }
            }

            TimeSpan best = times.Min();
            Assert.Fail(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Took {0:0.00} s at the best of {1} runs, the limit being {2:0.00} s ({3}).",
                    best.TotalSeconds,
                    runs,
                    seconds,
                    Describe(times.Select(t => t.TotalMilliseconds))
                )
            );

            return best;
        }
    }
}
