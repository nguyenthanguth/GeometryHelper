using System;
using System.IO;
using System.Threading;
using GeometryHelper.Viewer.Builds;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// A folder under the temporary folder that is gone when the test is done.
    /// </summary>
    internal sealed class TempFolder : IDisposable
    {
        internal TempFolder(string prefix = "ghviewer-test")
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 10));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        internal string Combine(params string[] parts) => System.IO.Path.Combine(Path, System.IO.Path.Combine(parts));

        public void Dispose()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    if (Directory.Exists(Path))
                    {
                        // Git keeps its object files read-only.
                        foreach (string file in Directory.GetFiles(Path, "*", SearchOption.AllDirectories))
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                        }

                        Directory.Delete(Path, true);
                    }

                    return;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Thread.Sleep(250);
                }
            }
        }
    }

    /// <summary>
    /// The environment a test of the comparison runs in: a cache of its own under the temporary folder, so the user's cache is left alone,
    /// and the runner program that was built beside the tests.
    /// </summary>
    public sealed class CompareFixture : IDisposable
    {
        private readonly TempFolder _root = new TempFolder("ghviewer-cache");

        public CompareFixture()
        {
            Environment = new CompareEnvironment
            {
                CacheRoot = _root.Path,
                RunnerFolder = System.IO.Path.Combine(AppContext.BaseDirectory, "runner"),
                OwnLibrary = typeof(Tolerance).Assembly.Location,
                RunTimeout = TimeSpan.FromSeconds(120),
            };
        }

        public CompareEnvironment Environment { get; }

        public void Dispose() => _root.Dispose();
    }
}
