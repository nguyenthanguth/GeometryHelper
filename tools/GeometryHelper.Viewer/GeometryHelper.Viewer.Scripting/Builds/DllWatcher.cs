using System;
using System.IO;
using System.Threading;

namespace GeometryHelper.Viewer.Builds
{
    /// <summary>
    /// Tells when a GeometryHelper.dll has been rebuilt on disk. A build writes the file in steps, and each step is a change, so the signal waits
    /// until the file has been still for the debounce time, and then only when the bytes are not the bytes it last told of.
    /// </summary>
    public sealed class DllWatcher : IDisposable
    {
        private readonly string _path;
        private readonly TimeSpan _debounce;
        private readonly Action _changed;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _timer;
        private readonly object _gate = new object();
        private string _lastKey;
        private bool _disposed;
        private int _unreadable;

        /// <summary>
        /// Starts watching a file.
        /// </summary>
        /// <param name="path">The path of GeometryHelper.dll.</param>
        /// <param name="debounce">How long the file must be still before the change is told; a second is the viewer's.</param>
        /// <param name="changed">Called, on a thread of the pool, once the file has changed and been still for the debounce time.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the folder of the file does not exist.</exception>
        public DllWatcher(string path, TimeSpan debounce, Action changed)
        {
            _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            _debounce = debounce;
            _changed = changed ?? throw new ArgumentNullException(nameof(changed));
            _lastKey = TryKey();
            _timer = new Timer(_ => Fire(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(_path), Path.GetFileName(_path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
            };
            _watcher.Changed += (s, e) => Restart();
            _watcher.Created += (s, e) => Restart();
            _watcher.Renamed += (s, e) => Restart();
            _watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Gets the path being watched.
        /// </summary>
        public string WatchedPath => _path;

        private void Restart()
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
                }
            }
        }

        private void Fire()
        {
            string key = TryKey();

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                // A file still being written cannot be read: look again after another wait, for a while.
                if (key == null)
                {
                    if (++_unreadable <= 30)
                    {
                        _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
                    }

                    return;
                }

                _unreadable = 0;

                // A file written with the bytes it had is not news.
                if (key == _lastKey)
                {
                    return;
                }

                _lastKey = key;
            }

            _changed();
        }

        private string TryKey()
        {
            try
            {
                return RunnerCache.KeyOf(_path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Stops watching.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
            }

            _watcher.Dispose();
            _timer.Dispose();
        }
    }
}
