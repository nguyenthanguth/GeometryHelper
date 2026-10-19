using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace GeometryHelper.Viewer.Builds
{
    /// <summary>
    /// Keeps a copy of the runner program for each build of the library: the runner's own folder with that build's GeometryHelper.dll put in it, in
    /// <c>runners\&lt;SHA-256 of the DLL&gt;-&lt;first 12 digits of the SHA-256 of the runner program&gt;</c> under the cache. A DLL that has been seen before
    /// costs no copying, two paths that hold the same bytes share a folder, and a runner program that has changed gets folders of its own, so that a copy
    /// a running process may have open is never moved or replaced.
    /// </summary>
    public static class RunnerCache
    {
        private const string ReadyFile = ".ready";

        /// <summary>
        /// Gets the key of a library: the SHA-256 of the bytes of the DLL, in lower-case hexadecimal.
        /// </summary>
        /// <param name="dll">The path of GeometryHelper.dll.</param>
        /// <returns>64 hexadecimal digits.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static string KeyOf(string dll)
        {
            if (dll == null)
            {
                throw new ArgumentNullException(nameof(dll));
            }

            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = new FileStream(dll, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                return Hex(hash.ComputeHash(stream));
            }
        }

        /// <summary>
        /// Gets the folder the copy of the runner for a library lives in, whether or not it has been made.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="dll">The path of GeometryHelper.dll.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="IOException">Thrown when the DLL, or the runner program, cannot be read.</exception>
        public static string FolderOf(CompareEnvironment environment, string dll)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            return Path.Combine(environment.RunnersFolder, FolderName(KeyOf(dll), RunnerKey(environment.RunnerFolder)));
        }

        /// <summary>
        /// Makes the copy of the runner for a library if there is none, and says where it is.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="dll">The path of GeometryHelper.dll.</param>
        /// <param name="log">Told what was done: the cache hit, or the copy made; may be null.</param>
        /// <returns>The folder, which holds GeometryHelper.Viewer.Runner.dll and the library.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the runner folder of the environment does not exist.</exception>
        /// <exception cref="IOException">Thrown when a file cannot be read or copied.</exception>
        public static string Prepare(CompareEnvironment environment, string dll, Action<string> log) => Prepare(environment, dll, log, out _);

        /// <summary>
        /// Makes the copy of the runner for a library if there is none, and says where it is and what the library's key is. The DLL is copied once, first, into a
        /// folder of its own, and its key is taken from that copy, so that the key names the bytes the runner will run and not whatever the file held a moment
        /// before; a copy is made in that folder and moved into place whole, so a copy that was interrupted is never used, and two runs that want the same copy
        /// take turns, the second finding it made.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="dll">The path of GeometryHelper.dll.</param>
        /// <param name="log">Told what was done; may be null.</param>
        /// <param name="key">The SHA-256 of the copy of the DLL, in lower-case hexadecimal.</param>
        /// <returns>The folder, which holds GeometryHelper.Viewer.Runner.dll and the library.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the runner folder of the environment does not exist.</exception>
        /// <exception cref="IOException">Thrown when a file cannot be read or copied.</exception>
        public static string Prepare(CompareEnvironment environment, string dll, Action<string> log, out string key)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (dll == null)
            {
                throw new ArgumentNullException(nameof(dll));
            }

            if (!Directory.Exists(environment.RunnerFolder))
            {
                throw new DirectoryNotFoundException("The runner program is not at " + environment.RunnerFolder + ".");
            }

            Directory.CreateDirectory(environment.RunnersFolder);
            string stage = Path.Combine(environment.RunnersFolder, ".stage-" + Guid.NewGuid().ToString("N").Substring(0, 8));

            try
            {
                Directory.CreateDirectory(stage);
                string copy = Path.Combine(stage, "GeometryHelper.dll");
                File.Copy(dll, copy, true);
                key = KeyOf(copy);
                string runnerKey = RunnerKey(environment.RunnerFolder);
                string folder = Path.Combine(environment.RunnersFolder, FolderName(key, runnerKey));

                using (new NamedLock("runner." + Path.GetFileName(folder)))
                {
                    // Looked at again under the lock: the run that held it may have made this very copy.
                    if (IsReady(folder, runnerKey))
                    {
                        log?.Invoke("runner cache hit " + key.Substring(0, 12));
                    }
                    else
                    {
                        string tree = Path.Combine(stage, "tree");
                        CopyFolder(environment.RunnerFolder, tree);
                        File.Move(copy, Path.Combine(tree, "GeometryHelper.dll"), true);

                        // The library's own symbols and notes belong to the build that was copied over, not to this one.
                        foreach (string stale in new[] { "GeometryHelper.pdb", "GeometryHelper.xml" })
                        {
                            File.Delete(Path.Combine(tree, stale));
                        }

                        File.WriteAllText(Path.Combine(tree, ReadyFile), runnerKey, new UTF8Encoding(false));

                        if (Directory.Exists(folder))
                        {
                            // Not ready, so a copy that was never finished. It is put aside, never deleted: where something has it open it cannot be moved, and the copy
                            // is then made under a name of its own.
                            try
                            {
                                Directory.Move(folder, folder + ".old-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                            }
                            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                            {
                                folder = folder + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                            }
                        }

                        Directory.Move(tree, folder);
                        log?.Invoke("runner copied to " + folder);
                    }
                }

                CacheTrimmer.Touch(Path.Combine(folder, ReadyFile));
                CacheTrimmer.Trim(environment.CacheRoot, environment.RunnersFolder, ReadyFile, environment.CacheKeep, folder, environment.CacheProtection);
                return folder;
            }
            finally
            {
                TryDelete(stage);
            }
        }

        private static string FolderName(string key, string runnerKey) => key + "-" + runnerKey.Substring(0, 12);

        private static bool IsReady(string folder, string runnerKey)
        {
            try
            {
                string marker = Path.Combine(folder, ReadyFile);
                return File.Exists(marker) && File.ReadAllText(marker, Encoding.UTF8) == runnerKey && File.Exists(Path.Combine(folder, "GeometryHelper.dll"));
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets what says the runner program has not changed since a copy was made: the SHA-256 of its own files, taken in the order of their names.
        /// </summary>
        private static string RunnerKey(string folder)
        {
            using (SHA256 hash = SHA256.Create())
            {
                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                {
                    string relative = file.Substring(folder.Length).TrimStart(Path.DirectorySeparatorChar);

                    // The library the viewer was built with is not part of the runner: every copy has its own put over it.
                    if (string.Equals(relative, "GeometryHelper.dll", StringComparison.OrdinalIgnoreCase) || string.Equals(relative, "GeometryHelper.pdb", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(relative, "GeometryHelper.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    byte[] name = Encoding.UTF8.GetBytes(relative.ToLowerInvariant() + "\n");
                    hash.TransformBlock(name, 0, name.Length, null, 0);
                    byte[] content = File.ReadAllBytes(file);
                    hash.TransformBlock(content, 0, content.Length, null, 0);
                }

                hash.TransformFinalBlock(new byte[0], 0, 0);
                return Hex(hash.Hash);
            }
        }

        private static void CopyFolder(string from, string to)
        {
            Directory.CreateDirectory(to);

            foreach (string file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            }

            foreach (string directory in Directory.GetDirectories(from))
            {
                CopyFolder(directory, Path.Combine(to, Path.GetFileName(directory)));
            }
        }

        private static void TryDelete(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Left; the trimming of the cache takes it when it is a day old.
            }
        }

        private static string Hex(byte[] bytes) => string.Concat(bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

        /// <summary>
        /// A lock that every process of the viewer shares, by name, for work that must not be done twice at once.
        /// </summary>
        private sealed class NamedLock : IDisposable
        {
            private readonly Mutex _mutex;

            internal NamedLock(string name)
            {
                _mutex = new Mutex(false, "Local\\GeometryHelper.Viewer." + name);

                try
                {
                    if (!_mutex.WaitOne(TimeSpan.FromSeconds(120)))
                    {
                        _mutex.Dispose();
                        throw new IOException("Another run has been making the runner copy '" + name + "' for more than two minutes.");
                    }
                }
                catch (AbandonedMutexException)
                {
                    // The run that held it ended without letting go; the lock is ours now, and the copy is looked at again, so nothing is lost.
                }
            }

            public void Dispose()
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
    }
}
