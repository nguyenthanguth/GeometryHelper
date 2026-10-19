using System;
using System.IO;
using System.Linq;

namespace GeometryHelper.Viewer.Builds
{
    /// <summary>
    /// Keeps the caches from growing without end: the newest few copies of the runner and builds of commits, by when each was last used, and nothing
    /// older, and the leftovers of copies and builds that were interrupted once they are a day old. It reaches only into the folders of the cache root
    /// that it is given, and only into folders directly in them.
    /// </summary>
    internal static class CacheTrimmer
    {
        private static readonly TimeSpan LeftoverAge = TimeSpan.FromDays(1);

        /// <summary>
        /// Marks a cached folder as used now, so that it is the last to be trimmed.
        /// </summary>
        /// <param name="marker">The ready marker file inside the folder.</param>
        internal static void Touch(string marker)
        {
            try
            {
                File.SetLastWriteTimeUtc(marker, DateTime.UtcNow);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // A marker that cannot be touched only trims a little sooner.
            }
        }

        /// <summary>
        /// Trims one cache folder.
        /// </summary>
        /// <param name="root">The cache root, which every folder deleted must lie inside.</param>
        /// <param name="folder">The folder of the cache to trim, such as the runners or the builds.</param>
        /// <param name="markerName">The name of the ready marker inside a finished entry.</param>
        /// <param name="keep">How many finished entries to keep, the newest by their marker; at least one.</param>
        /// <param name="spare">An entry in use now, which is never deleted; may be null.</param>
        /// <param name="protection">An entry whose marker was touched within this long is never deleted, however many are newer: a run, in this viewer or another, may be using it.</param>
        internal static void Trim(string root, string folder, string markerName, int keep, string spare, TimeSpan protection)
        {
            if (!Directory.Exists(folder) || !IsInside(root, folder))
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            string[] spared = { spare == null ? null : Path.GetFullPath(spare).TrimEnd(Path.DirectorySeparatorChar) };

            foreach (string entry in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(entry);

                // What an interrupted copy or build left: named for being a temporary, and old.
                if ((name.Contains(".tmp-") || name.Contains(".old-") || name.StartsWith(".stage-", StringComparison.Ordinal)) && now - Directory.GetLastWriteTimeUtc(entry) > LeftoverAge)
                {
                    TryDelete(root, entry);
                }
            }

            var finished = Directory.GetDirectories(folder)
                .Where(d => File.Exists(Path.Combine(d, markerName)))
                .Select(d => new { Path = d, Used = File.GetLastWriteTimeUtc(Path.Combine(d, markerName)) })
                .OrderByDescending(e => e.Used)
                .ThenBy(e => e.Path, StringComparer.Ordinal)
                .ToList();

            foreach (var entry in finished.Skip(Math.Max(CompareEnvironment.MinimumCacheKeep, keep)))
            {
                if (now - entry.Used > protection && !spared.Contains(Path.GetFullPath(entry.Path).TrimEnd(Path.DirectorySeparatorChar), StringComparer.OrdinalIgnoreCase))
                {
                    TryDelete(root, entry.Path);
                }
            }
        }

        private static bool IsInside(string root, string path)
        {
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string top = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(top, StringComparison.OrdinalIgnoreCase) && full.Length > top.Length;
        }

        private static void TryDelete(string root, string folder)
        {
            if (!IsInside(root, folder))
            {
                return;
            }

            // Renamed first, which is atomic and fails while a process has the folder open, then deleted: an entry is never half deleted under a run that is starting from it.
            string aside = folder + ".old-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            try
            {
                Directory.Move(folder, aside);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return;
            }

            try
            {
                Directory.Delete(aside, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // What could not be deleted stays under a name the leftover rule takes when it is a day old.
            }
        }
    }
}
