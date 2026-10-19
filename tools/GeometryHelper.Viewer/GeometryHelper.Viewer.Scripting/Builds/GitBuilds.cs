using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeometryHelper.Viewer.Builds
{
    /// <summary>
    /// Builds a commit of the library into the viewer's cache, <c>builds\&lt;full hash&gt;\GeometryHelper.dll</c>. The source is read out of the
    /// repository with <c>git archive</c>, which only reads its objects: the work tree, the branches, the index and the list of work trees of the
    /// repository are not touched, and nothing of the commit is left behind but the DLL.
    /// </summary>
    public static class GitBuilds
    {
        /// <summary>
        /// The project that is built, relative to the root of the repository.
        /// </summary>
        public const string Project = "src/GeometryHelper/GeometryHelper.csproj";

        private const string ReadyFile = "ready";

        /// <summary>
        /// Finds the full hash of a commit.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="repository">The folder of the repository, or of a work tree of it.</param>
        /// <param name="commit">A hash, a short hash, a tag or a branch name.</param>
        /// <param name="cancel">Kills the process.</param>
        /// <returns>The 40 hexadecimal digits of the commit.</returns>
        /// <exception cref="InvalidOperationException">Thrown when Git cannot be started, or the repository has no such commit.</exception>
        public static async Task<string> ResolveAsync(CompareEnvironment environment, string repository, string commit, CancellationToken cancel)
        {
            if (!Directory.Exists(repository))
            {
                throw new InvalidOperationException("No such folder for the repository: " + repository);
            }

            var output = new List<string>();
            var errors = new List<string>();
            ChildResult result = await ChildProcess.RunAsync(
                environment.GitPath,
                new[] { "-C", repository, "rev-parse", "--verify", "--quiet", commit + "^{commit}" },
                repository,
                TimeSpan.FromSeconds(60),
                line => output.Add(line),
                line => errors.Add(line),
                cancel).ConfigureAwait(false);

            cancel.ThrowIfCancellationRequested();
            string hash = output.Select(l => l.Trim()).FirstOrDefault(l => l.Length == 40 && l.All(Uri.IsHexDigit));

            if (result.ExitCode != 0 || hash == null)
            {
                throw new InvalidOperationException("The repository " + repository + " has no commit '" + commit + "'." + (errors.Count > 0 ? " " + string.Join(" ", errors) : string.Empty));
            }

            return hash.ToLowerInvariant();
        }

        /// <summary>
        /// Gets the DLL of a commit, building it first when it has not been built.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="repository">The folder of the repository, or of a work tree of it.</param>
        /// <param name="commit">A hash, a short hash, a tag or a branch name.</param>
        /// <param name="log">Told what is done and what the build says; may be null.</param>
        /// <param name="cancel">Kills the build.</param>
        /// <returns>The path of GeometryHelper.dll in the cache.</returns>
        /// <exception cref="InvalidOperationException">Thrown when Git or dotnet cannot be started, the commit does not exist or does not hold the project, or the build fails; the message holds the last lines of the build.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the build was cancelled.</exception>
        public static async Task<string> EnsureBuiltAsync(CompareEnvironment environment, string repository, string commit, Action<string> log, CancellationToken cancel)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            string hash = await ResolveAsync(environment, repository, commit, cancel).ConfigureAwait(false);
            string target = Path.Combine(environment.BuildsFolder, hash);
            string dll = Path.Combine(target, "GeometryHelper.dll");

            if (File.Exists(Path.Combine(target, ReadyFile)) && File.Exists(dll))
            {
                log?.Invoke("build cache hit " + hash.Substring(0, 12));
                CacheTrimmer.Touch(Path.Combine(target, ReadyFile));
                return dll;
            }

            Directory.CreateDirectory(environment.BuildsFolder);
            string temp = target + ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(temp);

            try
            {
                string tree = Path.Combine(temp, "tree");
                string zip = Path.Combine(temp, "source.zip");
                string[] paths = await PathsToArchiveAsync(environment, repository, hash, cancel).ConfigureAwait(false);

                log?.Invoke("reading " + hash.Substring(0, 12) + " out of " + repository + " with git archive");
                var errors = new List<string>();
                var arguments = new List<string> { "-C", repository, "archive", "--format=zip", "-o", zip, hash, "--" };
                arguments.AddRange(paths);
                ChildResult archived = await ChildProcess.RunAsync(environment.GitPath, arguments, repository, TimeSpan.FromMinutes(5), null, line => errors.Add(line), cancel).ConfigureAwait(false);
                cancel.ThrowIfCancellationRequested();

                if (archived.ExitCode != 0 || !File.Exists(zip))
                {
                    throw new InvalidOperationException("git archive failed for " + hash.Substring(0, 12) + ": " + string.Join(" ", errors));
                }

                ZipFile.ExtractToDirectory(zip, tree);
                File.Delete(zip);
                string project = Path.Combine(tree, Project.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(project))
                {
                    throw new InvalidOperationException("The commit " + hash.Substring(0, 12) + " has no " + Project + " to build.");
                }

                log?.Invoke("building " + Project + " (Release)");
                var tail = new Queue<string>();

                void Remember(string line)
                {
                    lock (tail)
                    {
                        tail.Enqueue(line);

                        while (tail.Count > 12)
                        {
                            tail.Dequeue();
                        }
                    }
                }

                var variables = new Dictionary<string, string>
                {
                    ["DOTNET_NOLOGO"] = "1",
                    ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                    ["MSBUILDDISABLENODEREUSE"] = "1",
                };

                // The compiler servers and build nodes a plain build leaves running would hold the folder open and outlive the process killed on a timeout.
                ChildResult built = await ChildProcess.RunAsync(
                    environment.DotnetPath,
                    new[] { "build", project, "-c", "Release", "-nologo", "-v:q", "-nodeReuse:false", "-p:UseSharedCompilation=false", "-p:GeneratePackageOnBuild=false" },
                    tree,
                    environment.BuildTimeout,
                    Remember,
                    Remember,
                    cancel,
                    variables).ConfigureAwait(false);
                cancel.ThrowIfCancellationRequested();

                if (built.TimedOut)
                {
                    throw new InvalidOperationException("The build of " + hash.Substring(0, 12) + " took more than " + environment.BuildTimeout.TotalMinutes.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " minutes and was killed.");
                }

                // Where the build puts the DLL is the project's business; its own bin folder first, then anywhere in the tree but obj.
                string output = new[] { Path.Combine(Path.GetDirectoryName(project), "bin"), tree }
                    .Where(Directory.Exists)
                    .SelectMany(folder => Directory.GetFiles(folder, "GeometryHelper.dll", SearchOption.AllDirectories).Where(f => f.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0).OrderBy(f => f, StringComparer.Ordinal))
                    .FirstOrDefault();

                if (built.ExitCode != 0 || output == null)
                {
                    throw new InvalidOperationException("The build of " + hash.Substring(0, 12) + " failed (exit code " + built.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + "):\n" + string.Join("\n", tail));
                }

                string done = Path.Combine(temp, "done");
                Directory.CreateDirectory(done);
                File.Copy(output, Path.Combine(done, "GeometryHelper.dll"), true);
                File.WriteAllText(Path.Combine(done, ReadyFile), hash + "\n");

                try
                {
                    Directory.Move(done, target);
                }
                catch (IOException) when (File.Exists(Path.Combine(target, ReadyFile)))
                {
                    // Another run built the same commit first.
                }

                log?.Invoke("built " + hash.Substring(0, 12));
                CacheTrimmer.Touch(Path.Combine(target, ReadyFile));
                CacheTrimmer.Trim(environment.CacheRoot, environment.BuildsFolder, ReadyFile, environment.CacheKeep, target, environment.CacheProtection);
                return dll;
            }
            finally
            {
                // The source tree of the commit goes: only the DLL is kept.
                TryDelete(temp);
            }
        }

        /// <summary>
        /// Lists what a build needs of a commit: the files at the root of the repository, which hold the shared build settings, and the library's own folder.
        /// </summary>
        private static async Task<string[]> PathsToArchiveAsync(CompareEnvironment environment, string repository, string hash, CancellationToken cancel)
        {
            var lines = new List<string>();
            var errors = new List<string>();
            ChildResult result = await ChildProcess.RunAsync(
                environment.GitPath,
                new[] { "-C", repository, "ls-tree", hash },
                repository,
                TimeSpan.FromSeconds(60),
                line => lines.Add(line),
                line => errors.Add(line),
                cancel).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("git ls-tree failed for " + hash.Substring(0, 12) + ": " + string.Join(" ", errors));
            }

            var paths = new List<string>();

            foreach (string line in lines)
            {
                // "100644 blob <hash>\t<name>"
                int tab = line.IndexOf('\t');

                if (tab > 0 && line.Contains(" blob ") && line[tab + 1] != '"')
                {
                    paths.Add(line.Substring(tab + 1));
                }
            }

            // git archive refuses a path the commit does not have, so the project is looked for first and the commit that lacks it is told so.
            var project = new List<string>();
            await ChildProcess.RunAsync(
                environment.GitPath,
                new[] { "-C", repository, "ls-tree", hash, "--", Project },
                repository,
                TimeSpan.FromSeconds(60),
                line => project.Add(line),
                null,
                cancel).ConfigureAwait(false);

            if (project.Count == 0)
            {
                throw new InvalidOperationException("The commit " + hash.Substring(0, 12) + " has no " + Project + " to build.");
            }

            paths.Add("src/GeometryHelper");
            return paths.ToArray();
        }

        private static void TryDelete(string folder)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(folder))
                    {
                        Directory.Delete(folder, true);
                    }

                    return;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // A process that has just ended can still hold a file for a moment.
                    Thread.Sleep(200);
                }
            }
        }
    }
}
