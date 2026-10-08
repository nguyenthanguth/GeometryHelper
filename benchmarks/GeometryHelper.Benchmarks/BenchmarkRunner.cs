using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GeometryHelper.Benchmarks
{
    /// <summary>
    /// How many times each case is run before it is timed, and how many times it is timed.
    /// </summary>
    internal sealed class Profile
    {
        private Profile(string name, int warmup, int samples, int heavyWarmup, int heavySamples)
        {
            Name = name;
            Warmup = warmup;
            Samples = samples;
            HeavyWarmup = heavyWarmup;
            HeavySamples = heavySamples;
        }

        /// <summary>
        /// Gets the short profile, a check that every result is still what was approved, with timings rough enough to
        /// show a case gone wrong: one run to warm up, five timed, and three of a heavy case.
        /// </summary>
        internal static Profile Short { get; } = new Profile("short", 1, 5, 1, 3);

        /// <summary>
        /// Gets the full profile, for timings to compare: three runs to warm up and twenty timed, so that the 95th
        /// percentile is not simply the slowest, and of a heavy case one and seven.
        /// </summary>
        internal static Profile Full { get; } = new Profile("full", 3, 20, 1, 7);

        /// <summary>Gets the name, as the command line gives it.</summary>
        internal string Name { get; }

        /// <summary>Gets how many runs warm a case up, untimed.</summary>
        internal int Warmup { get; }

        /// <summary>Gets how many runs of a case are timed.</summary>
        internal int Samples { get; }

        /// <summary>Gets how many runs warm a heavy case up.</summary>
        internal int HeavyWarmup { get; }

        /// <summary>Gets how many runs of a heavy case are timed.</summary>
        internal int HeavySamples { get; }

        /// <summary>
        /// Finds a profile by its name.
        /// </summary>
        /// <returns>The profile, or null for a name that is none.</returns>
        internal static Profile Named(string name)
        {
            if (string.Equals(name, Short.Name, StringComparison.Ordinal))
            {
                return Short;
            }

            return string.Equals(name, Full.Name, StringComparison.Ordinal) ? Full : null;
        }
    }

    /// <summary>
    /// What running one case came to.
    /// </summary>
    internal sealed class CaseResult
    {
        internal CaseResult(BenchmarkCase benchmark, int warmup, double[] samples, string signature, bool deterministic, string note)
        {
            Case = benchmark;
            Warmup = warmup;
            Samples = samples;
            Statistics = Statistics.Of(samples);
            Signature = signature;
            Deterministic = deterministic;
            Note = note;
        }

        /// <summary>Gets the case.</summary>
        internal BenchmarkCase Case { get; }

        /// <summary>Gets how many untimed runs warmed it up.</summary>
        internal int Warmup { get; }

        /// <summary>Gets the timed samples, in milliseconds, in the order they were taken.</summary>
        internal double[] Samples { get; }

        /// <summary>Gets the figures of the samples.</summary>
        internal Statistics Statistics { get; }

        /// <summary>Gets the signature of the result of the first timed run.</summary>
        internal string Signature { get; }

        /// <summary>Gets whether every run, warm-up included, signed as the first timed run did.</summary>
        internal bool Deterministic { get; }

        /// <summary>Gets what is worth knowing of the inputs and of the result.</summary>
        internal string Note { get; }

        /// <summary>Gets or sets the signature approved for the case; null when none is.</summary>
        internal string Approved { get; set; }

        /// <summary>Gets whether the signature is the one approved.</summary>
        internal bool Matched => Approved != null && string.Equals(Approved, Signature, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runs a case: builds its fixture, warms it up, times it, and signs every result.
    /// </summary>
    /// <remarks>
    /// Each timed run starts from a clean heap: a full collection, the finalizers it leaves run, and a second
    /// collection for what they freed, so that the garbage of one run is not collected inside the next. Only the call
    /// under test sits between the two readings of the <see cref="Stopwatch"/>; building the fixture and signing the
    /// result do not. Every result is signed, the warm-up runs' included, and a case whose results do not all sign the
    /// same is not deterministic, which fails the run whatever was approved.
    /// </remarks>
    internal static class BenchmarkRunner
    {
        /// <summary>
        /// Runs a case under a profile.
        /// </summary>
        /// <param name="benchmark">The case.</param>
        /// <param name="profile">How many times to warm it up and to time it.</param>
        /// <returns>The samples, the signature, and whether every run signed the same.</returns>
        internal static CaseResult Run(BenchmarkCase benchmark, Profile profile)
        {
            Fixture fixture = benchmark.Prepare();
            int warmup = benchmark.Heavy ? profile.HeavyWarmup : profile.Warmup;
            int count = benchmark.Heavy ? profile.HeavySamples : profile.Samples;

            var warmupSignatures = new List<string>(warmup);

            for (int w = 0; w < warmup; w++)
            {
                warmupSignatures.Add(fixture.Sign(fixture.Run()));
            }

            var samples = new double[count];
            string signature = null;
            string described = null;
            bool deterministic = true;
            var stopwatch = new Stopwatch();

            for (int s = 0; s < count; s++)
            {
                Clean();

                stopwatch.Restart();
                object result = fixture.Run();
                stopwatch.Stop();

                samples[s] = stopwatch.Elapsed.TotalMilliseconds;

                string signed = fixture.Sign(result);

                if (signature == null)
                {
                    signature = signed;
                    described = fixture.Describe(result);
                }
                else if (!string.Equals(signed, signature, StringComparison.Ordinal))
                {
                    deterministic = false;
                }
            }

            foreach (string signed in warmupSignatures)
            {
                if (!string.Equals(signed, signature, StringComparison.Ordinal))
                {
                    deterministic = false;
                }
            }

            string note = fixture.Note.Length == 0 ? described : fixture.Note + "; " + described;

            return new CaseResult(benchmark, warmup, samples, signature, deterministic, note);
        }

        private static void Clean()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
