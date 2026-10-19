using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeometryHelper.Viewer.Builds
{
    /// <summary>
    /// How a child process ended.
    /// </summary>
    internal readonly struct ChildResult
    {
        internal ChildResult(int exitCode, bool timedOut, bool cancelled)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Cancelled = cancelled;
        }

        internal int ExitCode { get; }

        /// <summary>Gets whether the time ran out and the process was killed.</summary>
        internal bool TimedOut { get; }

        /// <summary>Gets whether the caller cancelled and the process was killed.</summary>
        internal bool Cancelled { get; }
    }

    /// <summary>
    /// A Windows job object that holds a process and everything it starts, so that all of them can be ended together even after the first has ended on its
    /// own (the processes a finished process left running are no longer reachable from it), and that ends them when it is closed.
    /// </summary>
    internal sealed class ProcessJob : IDisposable
    {
        private const uint KillOnJobClose = 0x2000;
        private const int ExtendedLimitInformation = 9;
        private IntPtr _handle;

        private ProcessJob(IntPtr handle)
        {
            _handle = handle;
        }

        /// <summary>
        /// Makes a job that ends its processes when it is closed; null where there are no jobs (not Windows) or the system refuses.
        /// </summary>
        internal static ProcessJob TryCreate()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }

            IntPtr handle = CreateJobObject(IntPtr.Zero, null);

            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var info = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = KillOnJobClose } };
            int size = Marshal.SizeOf(typeof(ExtendedLimits));
            IntPtr buffer = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(info, buffer, false);

                if (!SetInformationJobObject(handle, ExtendedLimitInformation, buffer, (uint)size))
                {
                    CloseHandle(handle);
                    return null;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return new ProcessJob(handle);
        }

        /// <summary>
        /// Puts a process in the job; what it starts afterwards is in the job too.
        /// </summary>
        /// <returns>false when the system would not, as where the process is already in a job that forbids it.</returns>
        internal bool TryAdd(Process process)
        {
            try
            {
                return AssignProcessToJobObject(_handle, process.Handle);
            }
            catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Ends every process in the job.
        /// </summary>
        internal void Terminate()
        {
            if (_handle != IntPtr.Zero)
            {
                TerminateJobObject(_handle, 1);
            }
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                // Closing the last handle ends what is still in the job.
                CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public IoCounters Io;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr attributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }

    /// <summary>
    /// Starts one program and waits for it, with its output given to the caller a line at a time. When the time is up or the caller cancels, the
    /// process it started and everything that process started is killed, and nothing else: they are the members of a job object made for this one run
    /// (where the system has none, the process and the tree below it, by the id it was given). The wait covers the output too: a process that
    /// ended and left a child holding its output open is given two seconds, and the child is then killed.
    /// </summary>
    internal static class ChildProcess
    {
        private static readonly TimeSpan OutputGrace = TimeSpan.FromSeconds(2);

        internal static async Task<ChildResult> RunAsync(
            string file,
            IEnumerable<string> arguments,
            string workingDirectory,
            TimeSpan timeout,
            Action<string> onOutput,
            Action<string> onError,
            CancellationToken cancel,
            IDictionary<string, string> environment = null)
        {
            var info = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            };

            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            if (environment != null)
            {
                foreach (KeyValuePair<string, string> pair in environment)
                {
                    info.Environment[pair.Key] = pair.Value;
                }
            }

            using (ProcessJob job = ProcessJob.TryCreate())
            using (var process = new Process { StartInfo = info, EnableRaisingEvents = true })
            {
                var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                process.Exited += (s, e) => exited.TrySetResult(true);
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        onOutput?.Invoke(e.Data);
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        onError?.Invoke(e.Data);
                    }
                };

                try
                {
                    process.Start();
                }
                catch (Win32Exception e)
                {
                    throw new InvalidOperationException("Cannot start " + file + ": " + e.Message, e);
                }

                bool jobHolds = job != null && job.TryAdd(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                void Kill()
                {
                    if (jobHolds)
                    {
                        job.Terminate();
                        return;
                    }

                    try
                    {
                        process.Kill(true);
                    }
                    catch (Exception e) when (e is InvalidOperationException || e is Win32Exception || e is NotSupportedException)
                    {
                        // It ended by itself in the meantime.
                    }
                }

                using (var limit = timeout > TimeSpan.Zero ? new CancellationTokenSource(timeout) : new CancellationTokenSource())
                using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancel, limit.Token))
                using (linked.Token.Register(Kill))
                {
                    await exited.Task.ConfigureAwait(false);

                    // The output handlers run on other threads and the pipes stay open while any process holds them, so the end of the output is waited for
                    // under the same limits: a grandchild that outlives the process is killed after a grace, or at once when the time is up or the caller cancels.
                    Task flushed = Task.Run(() => process.WaitForExit());
                    var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    using (linked.Token.Register(() => stopped.TrySetResult(true)))
                    {
                        Task first = await Task.WhenAny(flushed, stopped.Task, Task.Delay(OutputGrace)).ConfigureAwait(false);

                        if (!ReferenceEquals(first, flushed))
                        {
                            Kill();
                        }
                    }

                    // Where the job could not be made the kill reached only the process; a descendant may still hold the pipe, and the wait does not go on for it.
                    if (!ReferenceEquals(await Task.WhenAny(flushed, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(false), flushed))
                    {
                        try
                        {
                            process.CancelOutputRead();
                            process.CancelErrorRead();
                        }
                        catch (InvalidOperationException)
                        {
                            // The reading had ended.
                        }
                    }

                    bool cancelled = cancel.IsCancellationRequested;
                    bool timedOut = !cancelled && limit.IsCancellationRequested;
                    return new ChildResult(process.ExitCode, timedOut, cancelled);
                }
            }
        }
    }
}
