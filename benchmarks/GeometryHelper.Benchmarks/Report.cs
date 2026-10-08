using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;

namespace GeometryHelper.Benchmarks
{
    /// <summary>
    /// What a run was made on: the commit, the runtime, the machine and the profile.
    /// </summary>
    internal sealed class RunInfo
    {
        private RunInfo()
        {
        }

        /// <summary>Gets when the run started, in UTC.</summary>
        internal DateTime StartedUtc { get; private set; }

        /// <summary>Gets the commit checked out, or <c>unknown</c> where git cannot say.</summary>
        internal string GitSha { get; private set; }

        /// <summary>Gets whether a tracked file differed from the commit; null where git cannot say.</summary>
        internal bool? GitDirty { get; private set; }

        /// <summary>Gets the framework the suite is built for, as its assembly records it: .NETFramework,Version=v4.8.</summary>
        internal string TargetFramework { get; private set; }

        /// <summary>Gets the runtime it runs on, as <see cref="System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription"/> gives it.</summary>
        internal string Runtime { get; private set; }

        /// <summary>Gets the architecture of the process: X64.</summary>
        internal string Architecture { get; private set; }

        /// <summary>Gets the operating system.</summary>
        internal string Os { get; private set; }

        /// <summary>Gets the processor, as Windows names it.</summary>
        internal string Processor { get; private set; }

        /// <summary>Gets how many logical processors there are.</summary>
        internal int ProcessorCount { get; private set; }

        /// <summary>Gets the profile.</summary>
        internal Profile Profile { get; private set; }

        /// <summary>
        /// Reads what the run is made on.
        /// </summary>
        /// <param name="root">The root of the repository, where git is asked.</param>
        /// <param name="profile">The profile.</param>
        internal static RunInfo Collect(string root, Profile profile)
        {
            string sha = Git(root, "rev-parse HEAD");
            string status = Git(root, "status --porcelain --untracked-files=no");

            return new RunInfo
            {
                StartedUtc = DateTime.UtcNow,
                GitSha = string.IsNullOrEmpty(sha) ? "unknown" : sha,
                GitDirty = status == null ? (bool?)null : status.Length > 0,
                TargetFramework = typeof(RunInfo).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "unknown",
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                Os = System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim(),
                Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown",
                ProcessorCount = Environment.ProcessorCount,
                Profile = profile,
            };
        }

        /// <summary>
        /// The provenance written at the top of the approved signatures.
        /// </summary>
        internal string Provenance()
            => GitSha + (GitDirty == true ? " with changes" : string.Empty) + ", " + Runtime + ", " + Architecture;

        /// <summary>
        /// Asks git, in the repository, for one line of output.
        /// </summary>
        /// <returns>The output, trimmed; null where git is not there or fails.</returns>
        private static string Git(string root, string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (Process git = Process.Start(start))
                {
                    string output = git.StandardOutput.ReadToEnd();
                    git.StandardError.ReadToEnd();
                    git.WaitForExit();

                    return git.ExitCode == 0 ? output.Trim() : null;
                }
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception || exception is InvalidOperationException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Writes the report of a run: JSON for a program to read, and Markdown to read.
    /// </summary>
    /// <remarks>
    /// Numbers are written in the invariant culture, milliseconds to four decimals. The JSON is written here rather
    /// than by a library, so that the suite needs nothing the library does not.
    /// </remarks>
    internal static class Report
    {
        /// <summary>The version of the layout of the JSON, raised when a field is renamed or removed.</summary>
        internal const int SchemaVersion = 1;

        /// <summary>
        /// Writes both reports into a folder.
        /// </summary>
        /// <param name="folder">The folder; made if need be.</param>
        /// <param name="info">What the run was made on.</param>
        /// <param name="results">What each case came to, in the order they ran.</param>
        /// <param name="approving">Whether the run approved its signatures.</param>
        /// <returns>The paths of the JSON and the Markdown.</returns>
        internal static (string Json, string Markdown) Write(string folder, RunInfo info, IReadOnlyList<CaseResult> results, bool approving)
        {
            Directory.CreateDirectory(folder);

            string stem = Path.Combine(folder, info.Profile.Name + "-" + info.StartedUtc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
            string json = stem + ".json";
            string markdown = stem + ".md";

            File.WriteAllText(json, Json(info, results, approving), new UTF8Encoding(false));
            File.WriteAllText(markdown, Markdown(info, results, approving), new UTF8Encoding(false));

            return (json, markdown);
        }

        private static string Json(RunInfo info, IReadOnlyList<CaseResult> results, bool approving)
        {
            var text = new StringBuilder();
            text.Append("{\n");
            Field(text, 1, "schema", SchemaVersion.ToString(CultureInfo.InvariantCulture), true);
            Field(text, 1, "startedUtc", Quote(info.StartedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)), true);
            Field(text, 1, "profile", Quote(info.Profile.Name), true);
            Field(text, 1, "approving", approving ? "true" : "false", true);
            Field(text, 1, "gitSha", Quote(info.GitSha), true);
            Field(text, 1, "gitDirty", info.GitDirty.HasValue ? (info.GitDirty.Value ? "true" : "false") : "null", true);
            Field(text, 1, "targetFramework", Quote(info.TargetFramework), true);
            Field(text, 1, "runtime", Quote(info.Runtime), true);
            Field(text, 1, "architecture", Quote(info.Architecture), true);
            Field(text, 1, "os", Quote(info.Os), true);
            Field(text, 1, "processor", Quote(info.Processor), true);
            Field(text, 1, "processorCount", info.ProcessorCount.ToString(CultureInfo.InvariantCulture), true);
            Field(text, 1, "p95", Quote(Statistics.P95Convention), true);
            text.Append("  \"results\": [\n");

            for (int i = 0; i < results.Count; i++)
            {
                CaseResult r = results[i];
                Statistics s = r.Statistics;

                text.Append("    {\n");
                Field(text, 3, "name", Quote(r.Case.Name), true);
                Field(text, 3, "size", r.Case.Size.ToString(CultureInfo.InvariantCulture), true);
                Field(text, 3, "unit", Quote(r.Case.Unit), true);
                Field(text, 3, "warmup", r.Warmup.ToString(CultureInfo.InvariantCulture), true);
                Field(text, 3, "samples", s.Count.ToString(CultureInfo.InvariantCulture), true);
                Field(text, 3, "medianMs", Number(s.Median), true);
                Field(text, 3, "p95Ms", Number(s.P95), true);
                Field(text, 3, "minMs", Number(s.Min), true);
                Field(text, 3, "maxMs", Number(s.Max), true);
                Field(text, 3, "samplesMs", "[" + string.Join(", ", Array.ConvertAll(r.Samples, Number)) + "]", true);
                Field(text, 3, "signature", Quote(r.Signature), true);
                Field(text, 3, "approvedSignature", r.Approved == null ? "null" : Quote(r.Approved), true);
                Field(text, 3, "matched", r.Matched ? "true" : "false", true);
                Field(text, 3, "deterministic", r.Deterministic ? "true" : "false", true);
                Field(text, 3, "note", Quote(r.Note), false);
                text.Append(i + 1 < results.Count ? "    },\n" : "    }\n");
            }

            text.Append("  ]\n");
            text.Append("}\n");

            return text.ToString();
        }

        private static string Markdown(RunInfo info, IReadOnlyList<CaseResult> results, bool approving)
        {
            var text = new StringBuilder();
            text.Append("# GeometryHelper benchmarks, ").Append(info.Profile.Name).Append(" profile\n\n");
            text.Append("- Started: ").Append(info.StartedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(" UTC\n");
            text.Append("- Commit: `").Append(info.GitSha).Append('`');
            text.Append(info.GitDirty == true ? ", with changes to tracked files" : string.Empty).Append('\n');
            text.Append("- Runtime: ").Append(info.Runtime).Append(", ").Append(info.Architecture);
            text.Append(", built for ").Append(info.TargetFramework).Append('\n');
            text.Append("- Machine: ").Append(info.Os).Append(", ").Append(info.Processor);
            text.Append(", ").Append(info.ProcessorCount.ToString(CultureInfo.InvariantCulture)).Append(" logical processors\n");
            text.Append("- Profile: ").Append(info.Profile.Name).Append(", ");
            text.Append(Count(info.Profile.Warmup)).Append(" warm-up and ").Append(Count(info.Profile.Samples)).Append(" timed runs a case, ");
            text.Append(Count(info.Profile.HeavyWarmup)).Append(" and ").Append(Count(info.Profile.HeavySamples)).Append(" of a heavy one\n");
            text.Append("- Times in milliseconds. p95 is by ").Append(Statistics.P95Convention).Append('\n');

            if (approving)
            {
                text.Append("- This run approved its signatures; \"matched\" compares them with those approved before it\n");
            }

            text.Append('\n');
            text.Append("| Case | Size | Warm-up | Samples | Median | p95 | Min | Max | Signature | Matched |\n");
            text.Append("|---|---:|---:|---:|---:|---:|---:|---:|---|---|\n");

            foreach (CaseResult r in results)
            {
                Statistics s = r.Statistics;
                text.Append("| ").Append(r.Case.Name);
                text.Append(" | ").Append(Count(r.Case.Size)).Append(' ').Append(r.Case.Unit);
                text.Append(" | ").Append(Count(r.Warmup));
                text.Append(" | ").Append(Count(s.Count));
                text.Append(" | ").Append(Millis(s.Median));
                text.Append(" | ").Append(Millis(s.P95));
                text.Append(" | ").Append(Millis(s.Min));
                text.Append(" | ").Append(Millis(s.Max));
                text.Append(" | `").Append(r.Signature.Substring(0, 12)).Append("`");
                text.Append(" | ").Append(!r.Deterministic ? "**not deterministic**" : r.Matched ? "yes" : r.Approved == null ? "**none approved**" : "**no**");
                text.Append(" |\n");
            }

            text.Append("\n## Cases\n\n");

            foreach (CaseResult r in results)
            {
                text.Append("- **").Append(r.Case.Name).Append("**: ").Append(r.Note).Append(". Signature `").Append(r.Signature).Append("`");

                if (r.Approved != null && !r.Matched)
                {
                    text.Append(", approved `").Append(r.Approved).Append("`");
                }

                text.Append(".\n");
            }

            return text.ToString();
        }

        /// <summary>
        /// A whole number with its thousands set apart by spaces: 12 004 650.
        /// </summary>
        internal static string Count(long value)
            => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');

        private static string Millis(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

        private static string Number(double value) => value.ToString("0.0###", CultureInfo.InvariantCulture);

        private static void Field(StringBuilder text, int depth, string name, string value, bool more)
        {
            text.Append(' ', 2 * depth).Append(Quote(name)).Append(": ").Append(value).Append(more ? ",\n" : "\n");
        }

        private static string Quote(string value)
        {
            var text = new StringBuilder(value.Length + 2);
            text.Append('"');

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            text.Append(c);
                        }

                        break;
                }
            }

            return text.Append('"').ToString();
        }
    }
}
