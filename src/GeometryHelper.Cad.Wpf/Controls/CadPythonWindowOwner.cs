using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using GeometryHelper.Cad.Python;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The window's thread as the Python host sees it: work is posted to the dispatcher at background priority (so the command
    /// line shows a command's end before a script starts), a script waiting for an answer is waited for in a dispatcher frame
    /// that the user's keys and clicks keep running, and Esc pressed while Python computes is watched for by a thread of its
    /// own, since the window does not read keys then.
    /// </summary>
    internal sealed class CadPythonWindowOwner : ICadPythonOwner, IDisposable
    {
        private const int EscapeKey = 0x1B;
        private const int PollMilliseconds = 25;
        private static readonly int ProcessId = Process.GetCurrentProcess().Id;

        private readonly Dispatcher _dispatcher;
        private Action _stop;
        private ManualResetEvent _end;
        private int _waiting;

        /// <summary>Makes the owner of the window whose dispatcher it is given.</summary>
        /// <param name="dispatcher">The dispatcher of the thread that owns the drawings.</param>
        internal CadPythonWindowOwner(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <inheritdoc />
        public void Post(Action work)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            _dispatcher.BeginInvoke(DispatcherPriority.Background, work);
        }

        /// <inheritdoc />
        public void WaitUntil(Func<bool> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (done() || _dispatcher.HasShutdownStarted)
            {
                return;
            }

            var frame = new DispatcherFrame();

            // The answer is a key or a click, handled as a dispatcher operation: after each one done is looked at again.
            void Look(object sender, DispatcherHookEventArgs e)
            {
                if (done())
                {
                    frame.Continue = false;
                }
            }

            EventHandler shutdown = (s, e) => frame.Continue = false;
            _dispatcher.Hooks.OperationCompleted += Look;
            _dispatcher.ShutdownStarted += shutdown;
            Interlocked.Increment(ref _waiting);

            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                Interlocked.Decrement(ref _waiting);
                _dispatcher.ShutdownStarted -= shutdown;
                _dispatcher.Hooks.OperationCompleted -= Look;
            }
        }

        /// <summary>
        /// Watches Esc for the length of each run of the host: from its start to its end, outside the waits for an answer (where
        /// the window reads Esc itself), the press of Esc while a window of this process is in front stops the run.
        /// </summary>
        /// <param name="host">The host whose runs are watched.</param>
        internal void Watch(CadPythonHost host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            _stop = host.Stop;
            host.RunStarted += (s, e) => Begin();
            host.RunFinished += (s, e) => End();
        }

        private void Begin()
        {
            if (_end != null)
            {
                return;
            }

            var end = new ManualResetEvent(false);
            _end = end;
            var watcher = new Thread(() => Poll(end)) { IsBackground = true, Name = "Python Esc watcher" };
            watcher.Start();
        }

        private void End()
        {
            ManualResetEvent end = _end;
            _end = null;

            if (end != null)
            {
                end.Set();
            }
        }

        // One Esc is one press: the key going down, or a tap shorter than a poll; a key held when the run began is not one.
        private void Poll(ManualResetEvent end)
        {
            bool wasDown = (GetAsyncKeyState(EscapeKey) & 0x8000) != 0;

            while (!end.WaitOne(PollMilliseconds))
            {
                short state = GetAsyncKeyState(EscapeKey);
                bool isDown = (state & 0x8000) != 0;
                bool tapped = (state & 0x0001) != 0 && !isDown;
                bool pressed = !wasDown && (isDown || tapped);
                wasDown = isDown;

                if (pressed && Volatile.Read(ref _waiting) == 0 && IsOurWindowInFront())
                {
                    _stop();
                }
            }

            end.Dispose();
        }

        private static bool IsOurWindowInFront()
        {
            IntPtr front = GetForegroundWindow();
            return front != IntPtr.Zero
                && GetWindowThreadProcessId(front, out uint owner) != 0
                && owner == (uint)ProcessId;
        }

        /// <summary>Ends the watch of a run going on.</summary>
        public void Dispose() => End();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
