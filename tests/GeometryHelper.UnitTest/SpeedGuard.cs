using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Xunit;

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

        private static TimeSpan AssertWithin(double seconds, Func<Action> prepare, int attempts)
        {
            TimeSpan limit = TimeSpan.FromSeconds(seconds);
            var times = new TimeSpan[attempts];
            int runs = 0;

            while (runs < attempts)
            {
                Action run = prepare();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var watch = Stopwatch.StartNew();
                run();
                watch.Stop();
                times[runs++] = watch.Elapsed;

                if (watch.Elapsed < limit)
                {
                    return watch.Elapsed;
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
                    string.Join(
                        ", ",
                        times.Select(t => t.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s")
                    )
                )
            );

            return best;
        }
    }
}
