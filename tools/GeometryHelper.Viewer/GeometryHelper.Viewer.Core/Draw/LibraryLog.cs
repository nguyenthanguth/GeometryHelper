using System;
using System.Collections.Generic;
using System.Diagnostics;
using GeometryHelper.Enums;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Collects what the library logs, at Warn and above, while a piece of work runs on this thread, so that the viewer can say why
    /// the library gave nothing: a cut it could not make is logged, not thrown. The library has one writer for the whole process,
    /// so the capture installs its own once, hands each thread's lines to the capture open on that thread, and passes everything
    /// else on to the writer that was there, putting it back when the last capture ends.
    /// </summary>
    public sealed class LibraryLog : IDisposable
    {
        private static readonly object Gate = new object();
        private static int _open;
        private static Action<GeometryHelperLogLevel, string, Exception> _previous;

        [ThreadStatic]
        private static LibraryLog _current;

        private readonly List<string> _lines = new List<string>();
        private readonly LibraryLog _outer;
        private bool _disposed;

        private LibraryLog()
        {
            _outer = _current;
            _current = this;
        }

        /// <summary>
        /// Starts collecting what the library logs on this thread, at Warn and Err, until the capture is disposed on the same thread.
        /// Captures nest; each line goes to the innermost.
        /// </summary>
        public static LibraryLog Begin()
        {
            lock (Gate)
            {
                if (_open++ == 0)
                {
                    _previous = GeometryHelperLog.Writer;
                    GeometryHelperLog.Writer = Handle;
                }
            }

            return new LibraryLog();
        }

        /// <summary>
        /// Gets the lines collected so far, each as the library formats it.
        /// </summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>
        /// Ends the capture and gives the writer back when no other capture is open.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _current = _outer;

            lock (Gate)
            {
                if (--_open == 0)
                {
                    GeometryHelperLog.Writer = _previous;
                    _previous = null;
                }
            }
        }

        private static void Handle(GeometryHelperLogLevel level, string message, Exception exception)
        {
            LibraryLog capture = _current;

            if (capture != null)
            {
                if (level >= GeometryHelperLogLevel.Warn)
                {
                    capture._lines.Add(GeometryHelperLog.Format(level, message, exception));
                }

                return;
            }

            Action<GeometryHelperLogLevel, string, Exception> previous;

            lock (Gate)
            {
                previous = _previous;
            }

            if (previous != null)
            {
                previous(level, message, exception);
            }
            else
            {
                Trace.WriteLine(GeometryHelperLog.Format(level, message, exception), GeometryHelperLog.TraceCategory);
            }
        }
    }
}
