using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Viewer.Builds;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// Builds commits of a throwaway repository made under the temporary folder: a library named GeometryHelper with one constant in it, so a
    /// build shows which commit it is of, and a shared Directory.Build.props at the root, so a build shows the root files were read.
    /// </summary>
    public class GitBuildsTests : IDisposable
    {
        private const string Project = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>netstandard2.0</TargetFramework>\n    <AssemblyName>GeometryHelper</AssemblyName>\n  </PropertyGroup>\n</Project>\n";
        private const string RootProps = "<Project>\n  <PropertyGroup>\n    <AssemblyVersion>7.7.7.0</AssemblyVersion>\n  </PropertyGroup>\n</Project>\n";

        private readonly TempFolder _root = new TempFolder("ghviewer-git");
        private readonly string _repository;
        private readonly CompareEnvironment _environment;

        public GitBuildsTests()
        {
            _repository = _root.Combine("repo");
            Directory.CreateDirectory(_repository);
            _environment = new CompareEnvironment { CacheRoot = _root.Combine("cache"), BuildTimeout = TimeSpan.FromMinutes(5) };
        }

        public void Dispose() => _root.Dispose();

        private string[] BuildEntries() => Directory.Exists(_environment.BuildsFolder) ? Directory.GetFileSystemEntries(_environment.BuildsFolder) : new string[0];

        private static string Git(string folder, params string[] arguments)
        {
            var info = new ProcessStartInfo("git") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = folder };
            info.ArgumentList.Add("-C");
            info.ArgumentList.Add(folder);

            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.True(process.ExitCode == 0, "git " + string.Join(" ", arguments) + ": " + errors);
                return output.Trim();
            }
        }

        private string Commit(string message, int value, bool breakIt = false)
        {
            Directory.CreateDirectory(Path.Combine(_repository, "src", "GeometryHelper"));
            File.WriteAllText(Path.Combine(_repository, "Directory.Build.props"), RootProps);
            File.WriteAllText(Path.Combine(_repository, "src", "GeometryHelper", "GeometryHelper.csproj"), Project);
            File.WriteAllText(Path.Combine(_repository, "src", "GeometryHelper", "Marker.cs"), "public static class Marker { public const int Value = " + value + (breakIt ? " this is not C#" : string.Empty) + "; }\n");

            if (!Directory.Exists(Path.Combine(_repository, ".git")))
            {
                Git(_repository, "init", "-q", "-b", "main");
                Git(_repository, "config", "user.email", "test@example.com");
                Git(_repository, "config", "user.name", "Test");
                Git(_repository, "config", "commit.gpgsign", "false");
                Git(_repository, "config", "core.autocrlf", "false");
            }

            Git(_repository, "add", "-A");
            Git(_repository, "commit", "-q", "-m", message);
            return Git(_repository, "rev-parse", "HEAD");
        }

        private static int MarkerOf(string dll, out Version version)
        {
            // A load context of its own, so that the DLL of the throwaway library does not meet the real GeometryHelper in this process.
            var context = new AssemblyLoadContext("marker-" + Guid.NewGuid().ToString("N"), true);

            try
            {
                Assembly assembly;

                using (var stream = new MemoryStream(File.ReadAllBytes(dll)))
                {
                    assembly = context.LoadFromStream(stream);
                }

                version = assembly.GetName().Version;
                return (int)assembly.GetType("Marker").GetField("Value").GetRawConstantValue();
            }
            finally
            {
                context.Unload();
            }
        }

        private static string Sha(string file)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(file)));
            }
        }

        [Fact]
        public async Task EachCommit_IsBuiltIntoAFolderNamedByItsFullHash_HoldingTheDllAndTheMarkerOnly()
        {
            string first = Commit("one", 1);
            string second = Commit("two", 2);
            var log = new List<string>();

            string dll1 = await GitBuilds.EnsureBuiltAsync(_environment, _repository, first.Substring(0, 8), log.Add, CancellationToken.None);
            string dll2 = await GitBuilds.EnsureBuiltAsync(_environment, _repository, "HEAD", log.Add, CancellationToken.None);

            Assert.Equal(first, Path.GetFileName(Path.GetDirectoryName(dll1)));
            Assert.Equal(second, Path.GetFileName(Path.GetDirectoryName(dll2)));
            Assert.Equal(1, MarkerOf(dll1, out Version version1));
            Assert.Equal(2, MarkerOf(dll2, out Version version2));
            Assert.Equal(new Version(7, 7, 7, 0), version1);
            Assert.Equal(version1, version2);
            Assert.NotEqual(Sha(dll1), Sha(dll2));
            Assert.Equal(new[] { "GeometryHelper.dll", "ready" }, Directory.GetFiles(Path.GetDirectoryName(dll1)).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
            Assert.Equal(2, Directory.GetDirectories(_environment.BuildsFolder).Length);
            Assert.Contains(log, line => line.StartsWith("building ", StringComparison.Ordinal));
        }

        [Fact]
        public async Task AnAbbreviatedHash_ATag_AndABranch_AreTheSameBuild_AndTheSecondAskIsACacheHit()
        {
            string first = Commit("one", 1);
            Git(_repository, "tag", "v1");
            Git(_repository, "branch", "side");
            Commit("two", 2);
            var log = new List<string>();

            string byTag = await GitBuilds.EnsureBuiltAsync(_environment, _repository, "v1", log.Add, CancellationToken.None);
            DateTime written = File.GetLastWriteTimeUtc(byTag);
            log.Clear();
            string byHash = await GitBuilds.EnsureBuiltAsync(_environment, _repository, first.Substring(0, 7), log.Add, CancellationToken.None);
            string byBranch = await GitBuilds.EnsureBuiltAsync(_environment, _repository, "side", log.Add, CancellationToken.None);

            Assert.Equal(byTag, byHash);
            Assert.Equal(byTag, byBranch);
            Assert.Equal(written, File.GetLastWriteTimeUtc(byTag));
            Assert.Equal(2, log.Count(line => line.StartsWith("build cache hit ", StringComparison.Ordinal)));
            Assert.DoesNotContain(log, line => line.StartsWith("building ", StringComparison.Ordinal));
        }

        [Fact]
        public async Task TheRepository_IsNotTouched_NoWorkTree_NoBranch_NoIndexChange_NoFileChange()
        {
            Commit("one", 1);
            string head = Commit("two", 2);
            File.WriteAllText(Path.Combine(_repository, "untracked.txt"), "left alone\n");
            File.AppendAllText(Path.Combine(_repository, "src", "GeometryHelper", "Marker.cs"), "// an uncommitted edit\n");
            string Snapshot() => string.Join(
                "\n--\n",
                Git(_repository, "rev-parse", "HEAD"),
                Git(_repository, "status", "--porcelain"),
                Git(_repository, "branch", "--list"),
                Git(_repository, "worktree", "list"),
                Git(_repository, "stash", "list"),
                Git(_repository, "for-each-ref"),
                Sha(Path.Combine(_repository, ".git", "index")),
                Sha(Path.Combine(_repository, "src", "GeometryHelper", "Marker.cs")),
                File.ReadAllText(Path.Combine(_repository, "untracked.txt")),
                string.Join(",", Directory.GetFileSystemEntries(Path.Combine(_repository, ".git")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).Where(n => n != "objects")));

            string before = Snapshot();

            await GitBuilds.EnsureBuiltAsync(_environment, _repository, head, null, CancellationToken.None);
            await GitBuilds.EnsureBuiltAsync(_environment, _repository, "HEAD~1", null, CancellationToken.None);

            Assert.Equal(before, Snapshot());
            Assert.False(Directory.Exists(Path.Combine(_repository, ".git", "worktrees")));
            Assert.Single(Git(_repository, "worktree", "list").Split('\n'));
        }

        [Fact]
        public async Task ACommitThatDoesNotExist_IsAnErrorThatNamesIt()
        {
            Commit("one", 1);

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => GitBuilds.EnsureBuiltAsync(_environment, _repository, "nope1234", null, CancellationToken.None));

            Assert.Contains("has no commit 'nope1234'", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AFolderThatIsNotARepository_IsAnError()
        {
            string plain = _root.Combine("plain");
            Directory.CreateDirectory(plain);

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => GitBuilds.EnsureBuiltAsync(_environment, plain, "HEAD", null, CancellationToken.None));

            Assert.Contains("has no commit 'HEAD'", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ACommitWithoutTheProject_IsAnError_AndLeavesNothingInTheCache()
        {
            Directory.CreateDirectory(_repository);
            File.WriteAllText(Path.Combine(_repository, "README.md"), "nothing to build\n");
            Git(_repository, "init", "-q", "-b", "main");
            Git(_repository, "config", "user.email", "test@example.com");
            Git(_repository, "config", "user.name", "Test");
            Git(_repository, "config", "commit.gpgsign", "false");
            Git(_repository, "add", "-A");
            Git(_repository, "commit", "-q", "-m", "readme");

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => GitBuilds.EnsureBuiltAsync(_environment, _repository, "HEAD", null, CancellationToken.None));

            Assert.Contains("has no " + GitBuilds.Project, error.Message, StringComparison.Ordinal);
            Assert.Empty(BuildEntries());
        }

        [Fact]
        public async Task ABuildThatFails_SaysWhyWithTheCompilerLines_AndCachesNothing()
        {
            Commit("broken", 1, true);

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => GitBuilds.EnsureBuiltAsync(_environment, _repository, "HEAD", null, CancellationToken.None));

            Assert.Contains("failed", error.Message, StringComparison.Ordinal);
            Assert.Contains("error CS", error.Message, StringComparison.Ordinal);
            Assert.Empty(BuildEntries());
        }

        [Fact]
        public async Task ACancelledBuild_ThrowsAndLeavesNoFolderBehind()
        {
            Commit("one", 1);

            using (var cancel = new CancellationTokenSource())
            {
                cancel.CancelAfter(TimeSpan.FromMilliseconds(300));

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitBuilds.EnsureBuiltAsync(_environment, _repository, "HEAD", null, cancel.Token));
            }

            Assert.Empty(BuildEntries());
        }

        [Fact]
        public async Task AResolvedCommit_IsTheFullHash()
        {
            string head = Commit("one", 1);

            Assert.Equal(head, await GitBuilds.ResolveAsync(_environment, _repository, head.Substring(0, 6), CancellationToken.None));
            Assert.Equal(head, await GitBuilds.ResolveAsync(_environment, _repository, "main", CancellationToken.None));
        }
    }
}
