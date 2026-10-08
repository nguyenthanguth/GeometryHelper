using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GeometryHelper.Benchmarks.Scenarios;

namespace GeometryHelper.Benchmarks
{
    /// <summary>
    /// The command line of the benchmark suite.
    /// </summary>
    /// <remarks>
    /// <code>
    /// dotnet run -c Release --project benchmarks/GeometryHelper.Benchmarks [-- options]
    ///
    ///   --profile short|full   short (the default) checks every result with rough timings; full times for comparing
    ///   --filter PREFIX        runs only the cases whose names start with it, such as Clash.AxisTrap
    ///   --out FOLDER           where the reports go; artifacts/benchmarks at the root of the repository unless given
    ///   --approve              writes the signatures of this run into benchmarks/baselines as the approved ones
    /// </code>
    /// Without --approve, a case whose signature differs from the one approved for it, or has none approved, fails the
    /// run, and so does a case whose runs do not all sign the same. The exit code is 0 when every case matched, 1 when
    /// one did not, and 2 for a command line that cannot be run.
    /// </remarks>
    internal static class Program
    {
        private const string Usage =
            "Usage: dotnet run -c Release --project benchmarks/GeometryHelper.Benchmarks -- [--profile short|full] [--filter PREFIX] [--out FOLDER] [--approve]";

        private static int Main(string[] args)
        {
            Profile profile = Profile.Short;
            string filter = null;
            string output = null;
            bool approve = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg == "--approve")
                {
                    approve = true;
                }
                else if ((arg == "--profile" || arg == "--filter" || arg == "--out") && i + 1 < args.Length)
                {
                    string value = args[++i];

                    if (arg == "--profile")
                    {
                        profile = Profile.Named(value);

                        if (profile == null)
                        {
                            return Refuse("No profile is called " + value + ".");
                        }
                    }
                    else if (arg == "--filter")
                    {
                        filter = value;
                    }
                    else
                    {
                        output = Path.GetFullPath(value);
                    }
                }
                else
                {
                    return Refuse(arg == "--help" || arg == "-h" ? null : "Not an option: " + arg + ".");
                }
            }

            string unoptimised = Unoptimised();

            if (unoptimised != null)
            {
                return Refuse(unoptimised + " is built without optimisation, and its timings would mean nothing. Build with -c Release.");
            }

            string root = FindRoot();

            if (root == null)
            {
                return Refuse("The root of the repository, where GeometryHelper.slnx is, cannot be found above " + AppDomain.CurrentDomain.BaseDirectory + ".");
            }

            output = output ?? Path.Combine(root, "artifacts", "benchmarks");
            string baselines = Path.Combine(root, "benchmarks", "baselines", ApprovedSignatures.FileName);

            List<BenchmarkCase> cases = AllCases().Where(c => filter == null || c.Name.StartsWith(filter, StringComparison.Ordinal)).ToList();

            if (cases.Count == 0)
            {
                return Refuse("No case's name starts with " + filter + ".");
            }

            if (Debugger.IsAttached)
            {
                Console.WriteLine("A debugger is attached: the timings will be slower than they are.");
            }

            SortedDictionary<string, string> approved = ApprovedSignatures.Read(baselines);
            RunInfo info = RunInfo.Collect(root, profile);

            Console.WriteLine("GeometryHelper benchmarks, " + profile.Name + " profile, " + cases.Count.ToString(CultureInfo.InvariantCulture) + " cases");
            Console.WriteLine(info.GitSha + (info.GitDirty == true ? " with changes" : string.Empty) + ", " + info.Runtime + ", " + info.Architecture);
            Console.WriteLine();

            var results = new List<CaseResult>();

            foreach (BenchmarkCase benchmark in cases)
            {
                Console.Write(benchmark.Name.PadRight(36));

                CaseResult result = BenchmarkRunner.Run(benchmark, profile);
                result.Approved = approved.TryGetValue(benchmark.Name, out string signature) ? signature : null;
                results.Add(result);

                Statistics s = result.Statistics;
                Console.WriteLine(
                    "median {0,10:0.000} ms  p95 {1,10:0.000} ms  n {2,2}  {3}  {4}",
                    s.Median, s.P95, s.Count, result.Signature.Substring(0, 12), Verdict(result, approve));
            }

            (string json, string markdown) = Report.Write(output, info, results, approve);

            Console.WriteLine();
            Console.WriteLine("Reports: " + json);
            Console.WriteLine("         " + markdown);

            bool failed = false;

            foreach (CaseResult result in results.Where(r => !r.Deterministic))
            {
                Console.WriteLine("NOT DETERMINISTIC: " + result.Case.Name + " signed differently from one run to another.");
                failed = true;
            }

            if (approve)
            {
                if (failed)
                {
                    Console.WriteLine("Nothing approved: a case that is not deterministic has no signature to approve.");
                    return 1;
                }

                // A run of every case approves exactly those; a filtered run updates its own cases and keeps the rest.
                SortedDictionary<string, string> written = filter == null ? new SortedDictionary<string, string>(StringComparer.Ordinal) : approved;

                foreach (CaseResult result in results)
                {
                    written[result.Case.Name] = result.Signature;
                }

                ApprovedSignatures.Write(baselines, written, info.Provenance());
                Console.WriteLine("Approved " + results.Count.ToString(CultureInfo.InvariantCulture) + " signatures into " + baselines);

                return 0;
            }

            foreach (CaseResult result in results.Where(r => r.Deterministic && !r.Matched))
            {
                Console.WriteLine(result.Approved == null
                    ? "NONE APPROVED: " + result.Case.Name + " signs " + result.Signature + ", and no signature is approved for it."
                    : "MISMATCH: " + result.Case.Name + " signs " + result.Signature + ", approved " + result.Approved + ".");
                failed = true;
            }

            Console.WriteLine(failed ? "FAILED" : "Every signature matched the approved one.");

            return failed ? 1 : 0;
        }

        /// <summary>
        /// Every case, in the order they run.
        /// </summary>
        private static IEnumerable<BenchmarkCase> AllCases()
        {
            var all = new List<BenchmarkCase>();
            all.AddRange(ClashScenarios.All());
            all.AddRange(SimplicityScenarios.All());
            all.AddRange(EarClippingScenarios.All());
            all.AddRange(ArrangingScenarios.All());

            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (BenchmarkCase benchmark in all)
            {
                if (!names.Add(benchmark.Name))
                {
                    throw new InvalidOperationException("Two cases are called " + benchmark.Name + ".");
                }
            }

            return all;
        }

        private static string Verdict(CaseResult result, bool approving)
        {
            if (!result.Deterministic)
            {
                return "NOT DETERMINISTIC";
            }

            if (result.Matched)
            {
                return "matched";
            }

            if (result.Approved == null)
            {
                return approving ? "new" : "NONE APPROVED";
            }

            return approving ? "changed" : "MISMATCH";
        }

        /// <summary>
        /// Names the first of the suite and the library that the JIT does not optimise.
        /// </summary>
        /// <returns>The name of the assembly, or null when both are optimised.</returns>
        private static string Unoptimised()
        {
            foreach (Assembly assembly in new[] { typeof(Program).Assembly, typeof(Tolerance).Assembly })
            {
                DebuggableAttribute debuggable = assembly.GetCustomAttribute<DebuggableAttribute>();

                if (debuggable != null && debuggable.IsJITOptimizerDisabled)
                {
                    return assembly.GetName().Name;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the root of the repository, the folder holding GeometryHelper.slnx, above where the suite runs from.
        /// </summary>
        private static string FindRoot()
        {
            for (DirectoryInfo folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); folder != null; folder = folder.Parent)
            {
                if (File.Exists(Path.Combine(folder.FullName, "GeometryHelper.slnx")))
                {
                    return folder.FullName;
                }
            }

            return null;
        }

        private static int Refuse(string reason)
        {
            if (reason != null)
            {
                Console.Error.WriteLine(reason);
            }

            Console.Error.WriteLine(Usage);

            return 2;
        }
    }
}
