using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Geometry;
using Python.Runtime;

namespace GeometryHelper.Cad.Python
{
    /// <summary>
    /// The one class that calls the Python engine. It starts CPython the first time Python is asked for, runs every script and
    /// palette entry on the thread that owns the drawings (so a script uses the object model with no marshalling), makes each run
    /// one undo step, asks a script's prompts as a command does, and turns the functions of a loaded file into commands. One
    /// host lives for the process: Python starts once.
    /// </summary>
    public sealed partial class CadPythonHost : IDisposable
    {
        private static readonly object StartLock = new object();
        private static bool _engineStarted;

        private readonly ICadPythonOwner _owner;
        private readonly Dictionary<CadDocument, PyObject> _consoles = new Dictionary<CadDocument, PyObject>();
        private PyObject _app;
        private PyObject _stdout;
        private PyObject _stderr;
        private PyObject _stdin;
        private IntPtr _threadState;
        private int _ownerThread;
        private string _startProblem;
        private volatile CadPythonRun _current;
        private bool _disposed;

        /// <summary>Makes a host; Python does not start until the first run or entry.</summary>
        /// <param name="environment">The Python to use, from <see cref="CadPythonEnvironment.Find(string)"/>.</param>
        /// <param name="owner">The thread that owns the drawings.</param>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when Python has started in this process already.</exception>
        public CadPythonHost(CadPythonEnvironment environment, ICadPythonOwner owner)
        {
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

            // A host on a Python that is not there never starts the engine, so it may be made whatever ran before (D719).
            lock (StartLock)
            {
                if (_engineStarted && environment.IsUsable)
                {
                    throw new InvalidOperationException("Python is started once per process.");
                }
            }
        }

        /// <summary>Gets the Python this host uses.</summary>
        public CadPythonEnvironment Environment { get; }

        /// <summary>Gets whether the engine has started.</summary>
        public bool IsStarted { get; private set; }

        /// <summary>Gets the environment's problem, or why the start failed; null when there is none.</summary>
        public string Problem => _startProblem ?? Environment.Problem;

        /// <summary>Gets whether a run is going on, computing or at a prompt.</summary>
        public bool IsRunning => _current != null;

        /// <summary>Gets the drawing the run going on works on; null when none runs.</summary>
        public CadDocument RunningDocument => _current?.Session.Document;

        /// <summary>
        /// Gets or sets what every file read at the user's request is checked with: the full path in, null out to allow it, else the
        /// text that refuses it. Null allows every file, as <c>(load)</c> does.
        /// </summary>
        public Func<string, string> LoadCheck { get; set; }

        /// <summary>Gets the commands PYLOAD defined, sorted ordinally.</summary>
        public IReadOnlyList<string> CommandNames
        {
            get
            {
                var names = new List<string>();

                foreach (Defined defined in _defined)
                {
                    names.Add(defined.Name);
                }

                names.Sort(StringComparer.Ordinal);
                return names;
            }
        }

        /// <summary>Occurs on the owner thread for each whole line a script writes, and for each error line the host reports.</summary>
        public event EventHandler<CadPythonOutputEventArgs> Output;

        /// <summary>Occurs on the owner thread when a run starts.</summary>
        public event EventHandler<CadPythonRunEventArgs> RunStarted;

        /// <summary>Occurs on the owner thread when a run ends, or is refused, with its outcome.</summary>
        public event EventHandler<CadPythonRunEventArgs> RunFinished;

        /// <summary>
        /// Stops the run going on as Esc does while Python computes: the script gets <c>KeyboardInterrupt</c> (about 20 ms for a
        /// loop of Python; only when it returns for <c>time.sleep</c> or a blocking call). May be called from any thread. At a
        /// prompt it does nothing: Esc on the session ends the question.
        /// </summary>
        public void Stop()
        {
            if (!IsStarted || _disposed || _current == null)
            {
                return;
            }

            using (Py.GIL())
            {
                CadPythonRun run = _current;

                if (run != null && run.Computing)
                {
                    PythonEngine.Interrupt(run.ThreadId);
                }
            }
        }

        /// <summary>
        /// Ends the host at exit: a run going on is stopped as Esc does and waited for up to 2 seconds; then Python shuts down,
        /// unless it left a thread running, which would make it wait for ever (a Warn line says so).
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!IsStarted)
            {
                return;
            }

            CadPythonRun run = _current;

            if (run != null)
            {
                using (Py.GIL())
                {
                    if (run.Computing)
                    {
                        PythonEngine.Interrupt(run.ThreadId);
                    }
                }

                if (!run.Computing && run.Session.IsRunning)
                {
                    run.Session.Escape();
                }

                var wait = Stopwatch.StartNew();

                while (_current != null && wait.ElapsedMilliseconds < 2000)
                {
                    Thread.Sleep(10);
                }
            }

            if (_current != null)
            {
                GeometryHelperLog.Warn("CadPythonHost: a run is still going on: Shutdown skipped.");
                return;
            }

            int threads;

            using (Py.GIL())
            {
                using (PyObject threading = Py.Import("threading"))
                using (PyObject count = threading.InvokeMethod("active_count"))
                {
                    threads = count.As<int>();
                }

                if (threads == 1)
                {
                    foreach (PyObject console in _consoles.Values)
                    {
                        console.Dispose();
                    }

                    _consoles.Clear();
                    DisposeDefined();
                    using (_app.InvokeMethod("uninstall")) { }
                    _stdout.Dispose();
                    _stderr.Dispose();
                    _stdin.Dispose();
                    _app.Dispose();
                }
            }

            if (threads != 1)
            {
                GeometryHelperLog.Warn(
                    "Python left "
                        + threads.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " threads running: Shutdown skipped."
                );
                return;
            }

            PythonEngine.EndAllowThreads(_threadState);
            PythonEngine.Shutdown();
        }

        /// <summary>Starts the engine if it has not started; false, with <see cref="Problem"/> set, where it cannot.</summary>
        private bool EnsureStarted()
        {
            if (IsStarted)
            {
                return true;
            }

            if (_startProblem != null || !Environment.IsUsable)
            {
                return false;
            }

            lock (StartLock)
            {
                if (_engineStarted)
                {
                    throw new InvalidOperationException("Python is started once per process.");
                }

                _engineStarted = true;
            }

            try
            {
                // The three assemblies the package needs are loaded before the first import, so that it adds nothing of its own.
                GC.KeepAlive(typeof(GeoPoint2).Assembly);
                GC.KeepAlive(typeof(CadApplication).Assembly);
                GC.KeepAlive(typeof(CadDocument).Assembly);

                Runtime.PythonDLL = Environment.Library;

                if (Environment.Executable != null)
                {
                    PythonEngine.ProgramName = Environment.Executable;
                }

                PythonEngine.Initialize();
                _ownerThread = System.Environment.CurrentManagedThreadId;

                using (Py.GIL())
                {
                    string folder = Path.Combine(AppFolder(), "python");

                    using (PyObject sys = Py.Import("sys"))
                    using (PyObject path = sys.GetAttr("path"))
                    using (path.InvokeMethod("insert", new PyInt(0), new PyString(folder))) { }

                    _app = Py.Import("geometryhelper._app");
                    var emit = new Action<string, bool>(EmitFromPython);

                    using (PyObject delegateObject = emit.ToPython())
                    using (_app.InvokeMethod("install", delegateObject)) { }

                    using (PyObject sys = Py.Import("sys"))
                    {
                        _stdout = sys.GetAttr("stdout");
                        _stderr = sys.GetAttr("stderr");
                        _stdin = sys.GetAttr("stdin");
                    }
                }

                _threadState = PythonEngine.BeginAllowThreads();
                IsStarted = true;
                return true;
            }
            catch (Exception exception)
            {
                string first = (exception.Message ?? string.Empty).Split('\n')[0].Trim();
                _startProblem =
                    "Python could not start: "
                    + first
                    + ". Fix Global settings > Python and restart GeometryHelper CAD.";
                GeometryHelperLog.Warn("CadPythonHost: Python could not start.", exception);

                // Started but not set up (the package not found): the engine holds the GIL on this thread since Initialize,
                // and cannot be started again (research M2b), so it is shut down here rather than left for the exit.
                if (PythonEngine.IsInitialized && _threadState == IntPtr.Zero)
                {
                    try
                    {
                        PythonEngine.Shutdown();
                    }
                    catch (Exception shutdown)
                    {
                        GeometryHelperLog.Warn("CadPythonHost: Shutdown after a failed start failed.", shutdown);
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// The folder whose <c>python</c> holds the app's copy of the package: beside this assembly, or, where the host copies
        /// assemblies elsewhere before loading them (a shadow copy, as xUnit does on .NET Framework), the application's base.
        /// </summary>
        private static string AppFolder()
        {
            string location = typeof(CadPythonHost).Assembly.Location;
            string beside = string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
            return beside != null && Directory.Exists(Path.Combine(beside, "python", "geometryhelper"))
                ? beside
                : AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>A whole line from the script's stdout or stderr, which any thread may write: the owner thread takes it from there.</summary>
        private void EmitFromPython(string line, bool isError)
        {
            if (System.Environment.CurrentManagedThreadId == _ownerThread)
            {
                Emit(line, isError, true);
            }
            else
            {
                _owner.Post(() => Emit(line, isError, true));
            }
        }

        /// <summary>Writes a line to the command line of the run's drawing (when asked) and raises <see cref="Output"/>.</summary>
        private void Emit(string line, bool isError, bool toCommandLine)
        {
            if (toCommandLine)
            {
                _current?.Session.Write(line);
            }

            Output?.Invoke(this, new CadPythonOutputEventArgs(line, isError));
        }

        /// <summary>Writes text as lines, as a script's <c>write</c> does.</summary>
        internal void WriteLines(string text, bool isError)
        {
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                Emit(line, isError, true);
            }
        }

        /// <summary>Raises <see cref="Output"/> for each line of a text, as errors, not touching any command line.</summary>
        private void EmitErrors(string text)
        {
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                Emit(line, true, false);
            }
        }

        private void RaiseStarted(CadPythonRun run) =>
            RunStarted?.Invoke(this, new CadPythonRunEventArgs(run.Name, run.Session.Document, null, null, run.Path));

        private void RaiseFinished(
            string name,
            CadDocument document,
            CadPythonOutcome outcome,
            string error,
            string path = null
        ) => RunFinished?.Invoke(this, new CadPythonRunEventArgs(name, document, outcome, error, path));
    }
}
