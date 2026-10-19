using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Viewer.Builds;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class RunnerCacheTests : IDisposable
    {
        private readonly TempFolder _root = new TempFolder("ghviewer-runner");
        private readonly CompareEnvironment _environment;

        public RunnerCacheTests()
        {
            string runner = _root.Combine("runner");
            Directory.CreateDirectory(Path.Combine(runner, "sub"));
            File.WriteAllText(Path.Combine(runner, "GeometryHelper.Viewer.Runner.dll"), "the runner");
            File.WriteAllText(Path.Combine(runner, "GeometryHelper.dll"), "the library the viewer was built with");
            File.WriteAllText(Path.Combine(runner, "GeometryHelper.pdb"), "its symbols");
            File.WriteAllText(Path.Combine(runner, "GeometryHelper.xml"), "its notes");
            File.WriteAllText(Path.Combine(runner, "sub", "dependency.txt"), "in a subfolder");
            _environment = new CompareEnvironment { CacheRoot = _root.Combine("cache"), RunnerFolder = runner };
        }

        public void Dispose() => _root.Dispose();

        private string Library(string name, string content)
        {
            string path = _root.Combine("libs", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void TheKey_IsTheSha256OfTheBytes_InLowerCaseHex()
        {
            string dll = Library("a.dll", "some bytes");

            string expected;

            using (SHA256 hash = SHA256.Create())
            {
                expected = string.Concat(hash.ComputeHash(File.ReadAllBytes(dll)).Select(b => b.ToString("x2")));
            }

            Assert.Equal(expected, RunnerCache.KeyOf(dll));
            Assert.Equal(64, expected.Length);
            Assert.Equal(expected, expected.ToLowerInvariant());
        }

        [Fact]
        public void TwoFilesOfTheSameBytes_ShareAKey_AndAByteOfDifference_DoesNot()
        {
            string a = Library("a.dll", "same bytes");
            string b = Library("elsewhere" + Path.DirectorySeparatorChar + "GeometryHelper.dll", "same bytes");
            string c = Library("c.dll", "same bytez");

            Assert.Equal(RunnerCache.KeyOf(a), RunnerCache.KeyOf(b));
            Assert.NotEqual(RunnerCache.KeyOf(a), RunnerCache.KeyOf(c));
            Assert.Equal(RunnerCache.FolderOf(_environment, a), RunnerCache.FolderOf(_environment, b));
            Assert.StartsWith(Path.Combine(_environment.RunnersFolder, RunnerCache.KeyOf(a) + "-"), RunnerCache.FolderOf(_environment, a), StringComparison.Ordinal);
        }

        [Fact]
        public void TheKeyOfAFileThatIsBeingWrittenTo_CanStillBeRead()
        {
            string dll = Library("busy.dll", "bytes");

            using (var writer = new FileStream(dll, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                writer.WriteByte(65);
                writer.Flush();

                Assert.Equal(64, RunnerCache.KeyOf(dll).Length);
            }
        }

        [Fact]
        public void Prepare_CopiesTheRunnerWithTheLibraryOfTheBuildOverTheOneItWasBuiltWith()
        {
            string dll = Library("lib.dll", "the other build");
            var log = new List<string>();

            string folder = RunnerCache.Prepare(_environment, dll, log.Add);

            Assert.Equal(RunnerCache.FolderOf(_environment, dll), folder);
            Assert.Equal("the other build", File.ReadAllText(Path.Combine(folder, "GeometryHelper.dll")));
            Assert.Equal("the runner", File.ReadAllText(Path.Combine(folder, "GeometryHelper.Viewer.Runner.dll")));
            Assert.Equal("in a subfolder", File.ReadAllText(Path.Combine(folder, "sub", "dependency.txt")));
            Assert.False(File.Exists(Path.Combine(folder, "GeometryHelper.pdb")));
            Assert.False(File.Exists(Path.Combine(folder, "GeometryHelper.xml")));
            Assert.True(File.Exists(Path.Combine(folder, ".ready")));
            Assert.Contains(log, line => line.StartsWith("runner copied to ", StringComparison.Ordinal));

            // The runner the copy was made from is as it was.
            Assert.Equal("the library the viewer was built with", File.ReadAllText(Path.Combine(_environment.RunnerFolder, "GeometryHelper.dll")));
            Assert.True(File.Exists(Path.Combine(_environment.RunnerFolder, "GeometryHelper.pdb")));
        }

        [Fact]
        public void AnotherPrepareForTheSameDll_IsACacheHit_AndCopiesNothing()
        {
            string dll = Library("lib.dll", "the other build");
            string folder = RunnerCache.Prepare(_environment, dll, null);
            File.WriteAllText(Path.Combine(folder, "marker.txt"), "made by the first");
            var log = new List<string>();

            string again = RunnerCache.Prepare(_environment, Library("copy" + Path.DirectorySeparatorChar + "x.dll", "the other build"), log.Add);

            Assert.Equal(folder, again);
            Assert.Equal("made by the first", File.ReadAllText(Path.Combine(folder, "marker.txt")));
            string hit = Assert.Single(log);
            Assert.StartsWith("runner cache hit ", hit, StringComparison.Ordinal);
            Assert.Single(Directory.GetDirectories(_environment.RunnersFolder));
        }

        [Fact]
        public void ADifferentDll_GetsACopyOfItsOwn()
        {
            string one = RunnerCache.Prepare(_environment, Library("one.dll", "one"), null);
            string two = RunnerCache.Prepare(_environment, Library("two.dll", "two"), null);

            Assert.NotEqual(one, two);
            Assert.Equal("one", File.ReadAllText(Path.Combine(one, "GeometryHelper.dll")));
            Assert.Equal("two", File.ReadAllText(Path.Combine(two, "GeometryHelper.dll")));
        }

        [Fact]
        public void WhenTheRunnerProgramChanges_ANewFolderIsMade_AndTheOldOneIsLeftWhereItIs()
        {
            string dll = Library("lib.dll", "the other build");
            string folder = RunnerCache.Prepare(_environment, dll, null);

            // A process that has the old copy open must not find it moved or replaced.
            using (var open = new FileStream(Path.Combine(folder, "GeometryHelper.Viewer.Runner.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                File.WriteAllText(Path.Combine(_environment.RunnerFolder, "GeometryHelper.Viewer.Runner.dll"), "a newer runner");
                var log = new List<string>();

                string again = RunnerCache.Prepare(_environment, dll, log.Add);

                Assert.NotEqual(folder, again);
                Assert.Equal("a newer runner", File.ReadAllText(Path.Combine(again, "GeometryHelper.Viewer.Runner.dll")));
                Assert.Equal("the runner", new StreamReader(open).ReadToEnd());
                Assert.Contains(log, line => line.StartsWith("runner copied to ", StringComparison.Ordinal));
            }

            Assert.Equal(2, Directory.GetDirectories(_environment.RunnersFolder).Length);
            Assert.Equal("the runner", File.ReadAllText(Path.Combine(folder, "GeometryHelper.Viewer.Runner.dll")));
        }

        [Fact]
        public void TheLibraryTheViewerWasBuiltWith_IsNotPartOfWhatSaysTheRunnerChanged()
        {
            string dll = Library("lib.dll", "the other build");
            RunnerCache.Prepare(_environment, dll, null);
            File.WriteAllText(Path.Combine(_environment.RunnerFolder, "GeometryHelper.dll"), "the viewer was rebuilt with a newer library");
            var log = new List<string>();

            RunnerCache.Prepare(_environment, dll, log.Add);

            Assert.StartsWith("runner cache hit ", Assert.Single(log), StringComparison.Ordinal);
        }

        [Fact]
        public void ACopyThatWasNeverFinished_IsNotUsed()
        {
            string dll = Library("lib.dll", "the other build");
            string folder = RunnerCache.FolderOf(_environment, dll);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "GeometryHelper.dll"), "half of it");

            string made = RunnerCache.Prepare(_environment, dll, null);

            Assert.Equal(folder, made);
            Assert.Equal("the other build", File.ReadAllText(Path.Combine(folder, "GeometryHelper.dll")));
            Assert.True(File.Exists(Path.Combine(folder, ".ready")));
            Assert.DoesNotContain(Directory.GetDirectories(_environment.RunnersFolder), d => d.Contains(".tmp-", StringComparison.Ordinal));

            // The half copy is put aside under a name the trimming takes when it is a day old, not deleted.
            string aside = Assert.Single(Directory.GetDirectories(_environment.RunnersFolder), d => d.Contains(".old-", StringComparison.Ordinal));
            Assert.Equal("half of it", File.ReadAllText(Path.Combine(aside, "GeometryHelper.dll")));
        }

        [Fact]
        public void ARunnerFolderThatIsNotThere_IsAnError()
        {
            _environment.RunnerFolder = _root.Combine("nothing");

            Assert.Throws<DirectoryNotFoundException>(() => RunnerCache.Prepare(_environment, Library("lib.dll", "x"), null));
        }

        [Fact]
        public void NullArguments_AreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => RunnerCache.KeyOf(null));
            Assert.Throws<ArgumentNullException>(() => RunnerCache.Prepare(null, "x", null));
            Assert.Throws<ArgumentNullException>(() => RunnerCache.Prepare(_environment, null, null));
        }
    }

    public class RunnerCacheRoundTwoTests : IDisposable
    {
        private readonly TempFolder _root = new TempFolder("ghviewer-runner2");
        private readonly CompareEnvironment _environment;

        public RunnerCacheRoundTwoTests()
        {
            string runner = _root.Combine("runner");
            Directory.CreateDirectory(runner);
            File.WriteAllText(Path.Combine(runner, "GeometryHelper.Viewer.Runner.dll"), "the runner");
            File.WriteAllText(Path.Combine(runner, "GeometryHelper.dll"), "the viewer's own");
            _environment = new CompareEnvironment { CacheRoot = _root.Combine("cache"), RunnerFolder = runner };
        }

        public void Dispose() => _root.Dispose();

        private string Library(string name, string content)
        {
            string path = _root.Combine("libs", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void TheKeyGiven_IsThatOfTheCopyTheRunnerRunsWith()
        {
            string dll = Library("lib.dll", "bytes of the build");

            string folder = RunnerCache.Prepare(_environment, dll, null, out string key);

            Assert.Equal(key, RunnerCache.KeyOf(Path.Combine(folder, "GeometryHelper.dll")));
            Assert.Equal(key, RunnerCache.KeyOf(dll));
            Assert.StartsWith(Path.Combine(_environment.RunnersFolder, key + "-"), folder, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SixRunsWantingTheSameCopyAtOnce_AllGetIt_AndLeaveNothingBehind()
        {
            string dll = Library("lib.dll", "bytes of the build");
            var folders = new string[6];

            await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Task.Run(() => folders[i] = RunnerCache.Prepare(_environment, dll, null))));

            Assert.Single(folders.Distinct());
            Assert.True(File.Exists(Path.Combine(folders[0], ".ready")));
            Assert.Equal("bytes of the build", File.ReadAllText(Path.Combine(folders[0], "GeometryHelper.dll")));
            Assert.Single(Directory.GetFileSystemEntries(_environment.RunnersFolder));
        }

        [Fact]
        public void OnlyTheNewestFewEntries_AreKept_ByWhenEachWasLastUsed_AndTheOneInUseIsSpared()
        {
            string runners = _environment.RunnersFolder;
            string[] folders = Enumerable.Range(0, 4).Select(i => Path.Combine(runners, "entry" + i)).ToArray();

            for (int i = 0; i < 4; i++)
            {
                Directory.CreateDirectory(folders[i]);
                File.WriteAllText(Path.Combine(folders[i], ".ready"), "x");
                File.SetLastWriteTimeUtc(Path.Combine(folders[i], ".ready"), DateTime.UtcNow.AddHours(-(10 - i)));
            }

            // Entry 3 is the newest, then 2, 1, 0; keeping two and sparing entry 0.
            CacheTrimmer.Trim(_environment.CacheRoot, runners, ".ready", 2, folders[0], TimeSpan.FromMinutes(1));

            Assert.Equal(new[] { true, false, true, true }, folders.Select(Directory.Exists));
        }

        [Fact]
        public void TheKeep_IsNeverLessThanTwo_SoASideCannotDeleteTheOthers()
        {
            _environment.CacheKeep = 1;
            Assert.Equal(2, _environment.CacheKeep);
            _environment.CacheKeep = -5;
            Assert.Equal(2, _environment.CacheKeep);
            _environment.CacheKeep = 7;
            Assert.Equal(7, _environment.CacheKeep);

            string first = RunnerCache.Prepare(_environment, Library("lib0.dll", "build 0"), null);
            _environment.CacheKeep = 1;
            string second = RunnerCache.Prepare(_environment, Library("lib1.dll", "build 1"), null);

            Assert.True(Directory.Exists(first));
            Assert.True(Directory.Exists(second));
        }

        [Fact]
        public void AnEntryUsedWithinTheProtectionTime_IsNeverTrimmed_HoweverManyAreNewer()
        {
            string runners = _environment.RunnersFolder;
            string[] folders = Enumerable.Range(0, 5).Select(i => Path.Combine(runners, "entry" + i)).ToArray();

            for (int i = 0; i < 5; i++)
            {
                Directory.CreateDirectory(folders[i]);
                File.WriteAllText(Path.Combine(folders[i], ".ready"), "x");
                File.SetLastWriteTimeUtc(Path.Combine(folders[i], ".ready"), DateTime.UtcNow.AddSeconds(-i));
            }

            CacheTrimmer.Trim(_environment.CacheRoot, runners, ".ready", 2, null, _environment.CacheProtection);

            Assert.All(folders, folder => Assert.True(Directory.Exists(folder), folder));
            Assert.True(_environment.CacheProtection > _environment.RunTimeout + _environment.BuildTimeout);
        }

        [Fact]
        public void AnEntryThatIsOpen_IsNotDeletedByTheTrimming_ItIsLeftWhole()
        {
            string runners = _environment.RunnersFolder;
            string[] folders = Enumerable.Range(0, 3).Select(i => Path.Combine(runners, "entry" + i)).ToArray();

            for (int i = 0; i < 3; i++)
            {
                Directory.CreateDirectory(folders[i]);
                File.WriteAllText(Path.Combine(folders[i], ".ready"), "x");
                File.WriteAllText(Path.Combine(folders[i], "runner.dll"), "x");
                File.SetLastWriteTimeUtc(Path.Combine(folders[i], ".ready"), DateTime.UtcNow.AddHours(-(10 - i)));
            }

            using (var open = new FileStream(Path.Combine(folders[0], "runner.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                CacheTrimmer.Trim(_environment.CacheRoot, runners, ".ready", 2, null, TimeSpan.FromMinutes(1));

                Assert.True(File.Exists(Path.Combine(folders[0], "runner.dll")));
                Assert.Equal((int)(char)120, open.ReadByte());
            }

            Assert.DoesNotContain(Directory.GetDirectories(runners), d => d.Contains(".old-", StringComparison.Ordinal) && !File.Exists(Path.Combine(d, "runner.dll")));
        }

        [Fact]
        public void LeftoversOlderThanADay_AreDeleted_AndYoungerOnesAreNot_AndNothingOutsideTheRootIsTouched()
        {
            string runners = _environment.RunnersFolder;
            Directory.CreateDirectory(runners);
            string oldTmp = Path.Combine(runners, "abc-1.tmp-1234");
            string newTmp = Path.Combine(runners, "abc-2.tmp-1234");
            string oldStage = Path.Combine(runners, ".stage-1234abcd");
            string plain = Path.Combine(runners, "no-marker-folder");

            foreach (string folder in new[] { oldTmp, newTmp, oldStage, plain })
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "x.txt"), "x");
            }

            Directory.SetLastWriteTimeUtc(oldTmp, DateTime.UtcNow.AddDays(-2));
            Directory.SetLastWriteTimeUtc(oldStage, DateTime.UtcNow.AddDays(-2));
            Directory.SetLastWriteTimeUtc(plain, DateTime.UtcNow.AddDays(-9));

            // A folder beside the cache root that the trimming must not reach, named as a leftover.
            string outside = _root.Combine("outside", "x.tmp-1");
            Directory.CreateDirectory(outside);
            Directory.SetLastWriteTimeUtc(outside, DateTime.UtcNow.AddDays(-9));

            CacheTrimmer.Trim(_environment.CacheRoot, runners, ".ready", 2, null, TimeSpan.FromMinutes(1));
            CacheTrimmer.Trim(_environment.CacheRoot, Path.GetDirectoryName(outside), ".ready", 2, null, TimeSpan.FromMinutes(1));

            Assert.False(Directory.Exists(oldTmp));
            Assert.False(Directory.Exists(oldStage));
            Assert.True(Directory.Exists(newTmp));
            Assert.True(Directory.Exists(plain));
            Assert.True(Directory.Exists(outside));
        }

        [Fact]
        public void ABuildWithNoRunnerFolder_IsStillAnErrorAndLeavesNoStage()
        {
            _environment.RunnerFolder = _root.Combine("nothing");

            Assert.Throws<DirectoryNotFoundException>(() => RunnerCache.Prepare(_environment, Library("lib.dll", "x"), null));
            Assert.False(Directory.Exists(_environment.RunnersFolder) && Directory.GetFileSystemEntries(_environment.RunnersFolder).Any());
        }
    }

    public class BuildSourceTests
    {
        [Fact]
        public void TheThreeForms_ParseAndWriteBackTheSame()
        {
            Assert.True(BuildSource.TryParse("own", null, out BuildSource own, out _));
            Assert.Equal(BuildSourceKind.Own, own.Kind);
            Assert.Equal("own", own.ToString());

            string dll = Path.Combine(Path.GetTempPath(), "GeometryHelper.dll");
            Assert.True(BuildSource.TryParse("dll:" + dll, null, out BuildSource byDll, out _));
            Assert.Equal(BuildSourceKind.Dll, byDll.Kind);
            Assert.Equal(dll, byDll.Dll);
            Assert.Equal("dll:" + dll, byDll.ToString());

            Assert.True(BuildSource.TryParse(" commit:9ee3f57 ", Path.GetTempPath(), out BuildSource commit, out _));
            Assert.Equal(BuildSourceKind.Commit, commit.Kind);
            Assert.Equal("9ee3f57", commit.Commit);
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()), commit.Repository);
            Assert.Equal("commit:9ee3f57", commit.ToString());
            Assert.Contains("commit 9ee3f57 of ", commit.Describe(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("dll:")]
        [InlineData("commit:")]
        [InlineData("git:abc")]
        public void ATextThatIsNoSource_IsRefusedWithAProblem(string text)
        {
            Assert.False(BuildSource.TryParse(text, Path.GetTempPath(), out BuildSource source, out string problem));
            Assert.Null(source);
            Assert.Contains("is not a source", problem, StringComparison.Ordinal);
        }

        [Fact]
        public void ACommitWithoutARepository_IsRefused()
        {
            Assert.False(BuildSource.TryParse("commit:abc", null, out _, out string problem));
            Assert.Contains("repository", problem, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => BuildSource.FromCommit("abc", " "));
            Assert.Throws<ArgumentException>(() => BuildSource.FromCommit(" ", "x"));
            Assert.Throws<ArgumentException>(() => BuildSource.FromDll(string.Empty));
        }
    }

    public class DllWatcherTests : IDisposable
    {
        private readonly TempFolder _root = new TempFolder("ghviewer-watch");

        public void Dispose() => _root.Dispose();

        private static async Task<bool> UntilAsync(Func<bool> condition, TimeSpan limit)
        {
            var clock = Stopwatch.StartNew();

            while (clock.Elapsed < limit)
            {
                if (condition())
                {
                    return true;
                }

                await Task.Delay(50);
            }

            return condition();
        }

        [Fact]
        public async Task ARebuiltFile_IsToldOnce_AfterItHasBeenStill()
        {
            string path = _root.Combine("GeometryHelper.dll");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            int told = 0;

            using (new DllWatcher(path, TimeSpan.FromMilliseconds(400), () => Interlocked.Increment(ref told)))
            {
                // A build writes in steps.
                for (int step = 0; step < 4; step++)
                {
                    File.WriteAllBytes(path, new byte[] { 9, (byte)step, 3 });
                    await Task.Delay(60);
                }

                Assert.True(await UntilAsync(() => Volatile.Read(ref told) == 1, TimeSpan.FromSeconds(10)));
                await Task.Delay(1200);
                Assert.Equal(1, Volatile.Read(ref told));
            }
        }

        [Fact]
        public async Task AFileWrittenAgainWithTheBytesItHad_IsNotNews()
        {
            string path = _root.Combine("GeometryHelper.dll");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            int told = 0;

            using (new DllWatcher(path, TimeSpan.FromMilliseconds(200), () => Interlocked.Increment(ref told)))
            {
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
                await Task.Delay(1500);

                Assert.Equal(0, Volatile.Read(ref told));
            }
        }

        [Fact]
        public async Task AFileReplacedByARename_IsToldToo()
        {
            string path = _root.Combine("GeometryHelper.dll");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            int told = 0;

            using (new DllWatcher(path, TimeSpan.FromMilliseconds(200), () => Interlocked.Increment(ref told)))
            {
                File.WriteAllBytes(path + ".new", new byte[] { 4, 5, 6 });
                File.Delete(path);
                File.Move(path + ".new", path);

                Assert.True(await UntilAsync(() => Volatile.Read(ref told) >= 1, TimeSpan.FromSeconds(10)));
            }
        }

        [Fact]
        public async Task AfterItIsDisposed_NothingIsTold()
        {
            string path = _root.Combine("GeometryHelper.dll");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            int told = 0;
            var watcher = new DllWatcher(path, TimeSpan.FromMilliseconds(200), () => Interlocked.Increment(ref told));
            watcher.Dispose();

            File.WriteAllBytes(path, new byte[] { 7, 7, 7 });
            await Task.Delay(1000);

            Assert.Equal(0, Volatile.Read(ref told));
            Assert.Equal(path, watcher.WatchedPath);
        }
    }
}
