using System;
using System.IO;

namespace GeometryHelper.Viewer.Builds
{
    /// <summary>
    /// Where the comparison keeps its caches and finds its tools. The viewer uses <see cref="CreateDefault"/>; a test makes one over a folder
    /// of its own so that it leaves the user's cache alone.
    /// </summary>
    public sealed class CompareEnvironment
    {
        /// <summary>
        /// The longest a script may run against one build before it is killed, unless the environment says otherwise: two minutes.
        /// </summary>
        public static readonly TimeSpan DefaultRunTimeout = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Gets or sets the folder the caches live in, <c>%LOCALAPPDATA%\GeometryHelper.Viewer</c> by default.
        /// </summary>
        public string CacheRoot { get; set; }

        /// <summary>
        /// Gets or sets the folder of the runner program, the output of the Runner project, which is copied for each build of the library.
        /// </summary>
        public string RunnerFolder { get; set; }

        /// <summary>
        /// Gets or sets the GeometryHelper.dll the viewer runs with, which is the build <see cref="BuildSourceKind.Own"/> stands for.
        /// </summary>
        public string OwnLibrary { get; set; }

        /// <summary>
        /// Gets or sets the program that runs Git; <c>git</c>, found on the path, by default.
        /// </summary>
        public string GitPath { get; set; } = "git";

        /// <summary>
        /// Gets or sets the program that builds a commit; <c>dotnet</c>, found on the path, by default.
        /// </summary>
        public string DotnetPath { get; set; } = "dotnet";

        /// <summary>
        /// Gets or sets how long a script may run against one build before the process and everything it started are killed.
        /// </summary>
        public TimeSpan RunTimeout { get; set; } = DefaultRunTimeout;

        /// <summary>
        /// Gets or sets how long building a commit may take before it is killed: fifteen minutes by default.
        /// </summary>
        public TimeSpan BuildTimeout { get; set; } = TimeSpan.FromMinutes(15);

        private int _cacheKeep = 20;

        /// <summary>
        /// Gets or sets how many copies of the runner, and how many built commits, the cache keeps: the newest by when each was last used. Twenty by default, and
        /// never fewer than two: the two sides of a comparison each need a copy at once, and a side must not delete the other's.
        /// </summary>
        public int CacheKeep
        {
            get => _cacheKeep;
            set => _cacheKeep = Math.Max(MinimumCacheKeep, value);
        }

        /// <summary>
        /// The fewest copies and builds the cache keeps.
        /// </summary>
        public const int MinimumCacheKeep = 2;

        /// <summary>
        /// Gets how long after it was last used a copy of the runner or a built commit is safe from trimming: a run may have it open that long, the time a script
        /// may take and the time a build may take and an hour more, in this viewer or in another that shares the cache.
        /// </summary>
        public TimeSpan CacheProtection => RunTimeout + BuildTimeout + TimeSpan.FromHours(1);

        /// <summary>
        /// Gets the folder of the cached copies of the runner, one for each GeometryHelper.dll and runner program, named by the SHA-256 of the DLL and the
        /// start of that of the runner program.
        /// </summary>
        public string RunnersFolder => Path.Combine(CacheRoot, "runners");

        /// <summary>
        /// Gets the folder of the built commits, one for each commit, named by its full hash.
        /// </summary>
        public string BuildsFolder => Path.Combine(CacheRoot, "builds");

        /// <summary>
        /// Gets the folder the scenes the runners write go to before they are read.
        /// </summary>
        public string ScenesFolder => Path.Combine(CacheRoot, "runs");

        /// <summary>
        /// Makes the environment the viewer runs in: the caches in <c>%LOCALAPPDATA%\GeometryHelper.Viewer</c>, the runner in a folder
        /// called <c>runner</c> beside the viewer's own files, and the library the viewer is running with.
        /// </summary>
        public static CompareEnvironment CreateDefault()
            => new CompareEnvironment
            {
                CacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeometryHelper.Viewer"),
                RunnerFolder = Path.Combine(AppContext.BaseDirectory, "runner"),
                OwnLibrary = typeof(Tolerance).Assembly.Location,
            };
    }
}
