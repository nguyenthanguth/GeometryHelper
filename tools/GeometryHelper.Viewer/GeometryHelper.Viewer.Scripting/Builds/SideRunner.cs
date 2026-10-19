using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeometryHelper.Viewer.Builds
{
    /// <summary>
    /// How a script ran against one build of the library.
    /// </summary>
    public sealed class SideRunResult
    {
        internal SideRunResult(string label, BuildSource source)
        {
            Label = label;
            Source = source;
        }

        /// <summary>
        /// Gets the name of the side, such as "left".
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the build the script was run against.
        /// </summary>
        public BuildSource Source { get; }

        /// <summary>
        /// Gets the path of the GeometryHelper.dll the build resolved to, or null when it did not get that far.
        /// </summary>
        public string LibraryPath { get; internal set; }

        /// <summary>
        /// Gets the SHA-256 of that DLL, which names its folder in the runner cache, or null.
        /// </summary>
        public string LibraryKey { get; internal set; }

        /// <summary>
        /// Gets the line the runner printed about the library it loaded, with the path and the assembly version, or null.
        /// </summary>
        public string LibraryLine { get; internal set; }

        /// <summary>
        /// Gets the SHA-256 of the text of the script as it was when the run started, so that a later run can tell the script has been edited; null when it did not get that far.
        /// </summary>
        public string ScriptHash { get; internal set; }

        /// <summary>
        /// Gets whether the script compiled, ran to its end and gave a scene.
        /// </summary>
        public bool Succeeded { get; internal set; }

        /// <summary>
        /// Gets the scene the script built, or null when it did not succeed.
        /// </summary>
        public Scene Scene { get; internal set; }

        /// <summary>
        /// Gets the lines the script said with <c>Note</c>.
        /// </summary>
        public IReadOnlyList<string> Notes { get; internal set; } = new string[0];

        /// <summary>
        /// Gets the errors: a compile error with its file and line number, an exception the script ended with, why a build failed, or that the process was killed.
        /// </summary>
        public IReadOnlyList<string> Errors { get; internal set; } = new string[0];

        /// <summary>
        /// Gets how long resolving the build, copying the runner and running the script took.
        /// </summary>
        public TimeSpan Elapsed { get; internal set; }

        /// <summary>
        /// Gets whether the run was killed because it ran past the time it was given.
        /// </summary>
        public bool TimedOut { get; internal set; }

        /// <summary>
        /// Gets whether the run was cancelled.
        /// </summary>
        public bool Cancelled { get; internal set; }
    }

    /// <summary>
    /// Runs a script against one build of the library in a child process and reads the scene it wrote.
    /// </summary>
    public static class SideRunner
    {
        /// <summary>
        /// Resolves the build to a GeometryHelper.dll (building the commit if it is one), makes the copy of the runner for it, runs the script there
        /// with <c>--measure</c>, and reads the scene it wrote. Nothing in it throws but running out of memory: whatever goes wrong comes back in
        /// <see cref="SideRunResult.Errors"/>.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="script">The path of the .csx script.</param>
        /// <param name="source">The build to run against.</param>
        /// <param name="label">The name of the side, put in front of each line sent to <paramref name="log"/>.</param>
        /// <param name="log">Told each line the build, the runner and the script say, as they say it; may be null, and is called from other threads.</param>
        /// <param name="cancel">Kills the process, and the ones it started, when cancelled.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the environment, the script or the source is null.</exception>
        public static async Task<SideRunResult> RunAsync(CompareEnvironment environment, string script, BuildSource source, string label, Action<string> log, CancellationToken cancel)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (script == null)
            {
                throw new ArgumentNullException(nameof(script));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var result = new SideRunResult(label, source);
            var notes = new List<string>();
            var errors = new List<string>();
            var clock = Stopwatch.StartNew();
            string scenePath = null;

            void Say(string line) => log?.Invoke("[" + label + "] " + line);

            try
            {
                result.ScriptHash = HashOfScript(script);
                string dll = await ResolveAsync(environment, source, Say, cancel).ConfigureAwait(false);
                result.LibraryPath = dll;
                string folder = await Task.Run(
                    () =>
                    {
                        // The key is taken from the copy of the DLL the runner is made with, so it is the key of the bytes that run.
                        string copied = RunnerCache.Prepare(environment, dll, Say, out string key);
                        result.LibraryKey = key;
                        return copied;
                    },
                    cancel).ConfigureAwait(false);

                Directory.CreateDirectory(environment.ScenesFolder);
                scenePath = Path.Combine(environment.ScenesFolder, Guid.NewGuid().ToString("N") + ".ghscene.json");
                string exe = Path.Combine(folder, "GeometryHelper.Viewer.Runner.exe");
                bool apphost = File.Exists(exe);
                var arguments = new List<string>();

                if (!apphost)
                {
                    exe = environment.DotnetPath;
                    arguments.Add(Path.Combine(folder, "GeometryHelper.Viewer.Runner.dll"));
                }

                arguments.Add(Path.GetFullPath(script));
                arguments.Add("--out");
                arguments.Add(scenePath);
                arguments.Add("--measure");
                Say("running " + Path.GetFileName(script) + " against " + source.Describe());

                ChildResult ran = await ChildProcess.RunAsync(
                    exe,
                    arguments,
                    Path.GetDirectoryName(Path.GetFullPath(script)),
                    environment.RunTimeout,
                    line =>
                    {
                        if (line.StartsWith("note: ", StringComparison.Ordinal))
                        {
                            lock (notes)
                            {
                                notes.Add(line.Substring(6));
                            }
                        }
                        else if (line.StartsWith("library: ", StringComparison.Ordinal))
                        {
                            result.LibraryLine = line.Substring(9);
                        }

                        Say(line);
                    },
                    line =>
                    {
                        lock (errors)
                        {
                            errors.Add(line);
                        }

                        Say(line);
                    },
                    cancel).ConfigureAwait(false);

                result.Cancelled = ran.Cancelled;
                result.TimedOut = ran.TimedOut;

                if (ran.Cancelled)
                {
                    errors.Add("The run was cancelled and the process killed.");
                }
                else if (ran.TimedOut)
                {
                    errors.Add("The script ran for more than " + environment.RunTimeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s against " + source.Describe() + " and was killed.");
                }
                else if (ran.ExitCode == 0 && File.Exists(scenePath))
                {
                    string path = scenePath;
                    result.Scene = await Task.Run(() => SceneReader.ReadFile(path), cancel).ConfigureAwait(false);
                    result.Succeeded = true;
                }
                else if (errors.Count == 0)
                {
                    errors.Add("The runner stopped with exit code " + ran.ExitCode.ToString(CultureInfo.InvariantCulture) + " and said nothing.");
                }
                else if (ran.ExitCode != 1)
                {
                    errors.Add("The runner stopped with exit code " + ran.ExitCode.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
                errors.Add("The run was cancelled.");
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                errors.Add(e.Message);
                Say(e.Message);
            }
            finally
            {
                if (scenePath != null)
                {
                    try
                    {
                        File.Delete(scenePath);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                    }
                }
            }

            result.Notes = notes;
            result.Errors = errors;
            result.Elapsed = clock.Elapsed;
            return result;
        }

        /// <summary>
        /// Gets the SHA-256 of the bytes of a script, in lower-case hexadecimal, or null when it cannot be read.
        /// </summary>
        /// <param name="script">The path of the script.</param>
        public static string HashOfScript(string script)
        {
            try
            {
                using (var hash = System.Security.Cryptography.SHA256.Create())
                {
                    return string.Concat(hash.ComputeHash(File.ReadAllBytes(script)).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the GeometryHelper.dll a source stands for, building the commit if it is one.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="source">The source.</param>
        /// <param name="log">Told what is done; may be null.</param>
        /// <param name="cancel">Kills the build.</param>
        /// <returns>The path of the DLL.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the DLL does not exist, or the commit cannot be built.</exception>
        public static async Task<string> ResolveAsync(CompareEnvironment environment, BuildSource source, Action<string> log, CancellationToken cancel)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string dll;

            switch (source.Kind)
            {
                case BuildSourceKind.Own:
                    dll = environment.OwnLibrary;
                    break;

                case BuildSourceKind.Dll:
                    dll = source.Dll;
                    break;

                default:
                    dll = await GitBuilds.EnsureBuiltAsync(environment, source.Repository, source.Commit, log, cancel).ConfigureAwait(false);
                    break;
            }

            if (string.IsNullOrEmpty(dll) || !File.Exists(dll))
            {
                throw new InvalidOperationException("There is no GeometryHelper.dll at " + dll + ".");
            }

            return dll;
        }
    }
}
