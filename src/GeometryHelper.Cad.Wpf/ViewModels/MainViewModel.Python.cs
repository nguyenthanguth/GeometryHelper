using System;
using System.Linq;
using System.Windows.Threading;
using GeometryHelper.Cad.Python;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Python in the window: one <see cref="CadPythonHost"/> for the process, made when the host of the app asks for it
    /// (<see cref="CadAppHost.EnablePython"/>), PYRUN, PYLOAD, PYTHON and PYTHONCLOSE in every drawing's commands, and the
    /// PYTHON palette's view model, which follows the drawing shown.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // The names of the commands the window adds (CadPythonNames.Show and Hide, which the host keeps to itself): PYTHON shows the palette, PYTHONCLOSE hides it.
        private const string PythonShowName = "PYTHON";
        private const string PythonHideName = "PYTHONCLOSE";

        private CadPythonHost _pythonHost;
        private CadPythonWindowOwner _pythonOwner;
        private CadPythonConsoleHost _pythonSeam;
        private string _pythonPathUsed;

        /// <summary>Gets the view model of the PYTHON palette; null where the app does not use Python (<see cref="CadAppHost.EnablePython"/> is false).</summary>
        public PythonConsoleViewModel PythonConsole { get; private set; }

        /// <summary>Gets whether the Python engine has started in this process: a Python set in Global settings from now on takes effect at the next start.</summary>
        internal bool HasPythonStarted => _pythonHost != null && _pythonHost.IsStarted;

        /// <summary>
        /// Makes the Python host of the window on the Python the settings name (or found as <see cref="CadPythonEnvironment.Find"/>
        /// finds it), the palette's view model, and the commands of every drawing open and made later. Python itself starts at the
        /// first run or entry, not here. Called once, on the window's thread, after the settings are applied.
        /// </summary>
        /// <param name="dispatcher">The dispatcher of the window's thread, which owns the drawings.</param>
        internal void UsePython(Dispatcher dispatcher)
        {
            if (_pythonHost != null)
            {
                return;
            }

            _pythonOwner = new CadPythonWindowOwner(dispatcher);
            _pythonSeam = new CadPythonConsoleHost();
            _pythonSeam.RunFinished += (name, document, outcome, error) => AfterPythonRun();
            UsePythonHost();
            PythonConsole = new PythonConsoleViewModel(_pythonSeam, StartCommand, () =>
                RequestSettings("Folder preference")
            );
            Raise(nameof(PythonConsole));

            foreach (KnownFile file in _files.Values.Where(f => f.Session != null))
            {
                AddPythonCommands(file.Session.Registry);
            }

            if (_current != null)
            {
                PythonConsole.Follow(_current.Document, _current.Session);
            }
        }

        private void UsePythonHost()
        {
            _pythonPathUsed = _settings.PythonPath;
            var host = new CadPythonHost(CadPythonEnvironment.Find(_pythonPathUsed), _pythonOwner)
            {
                // The same gate as AutoLISP's: a file outside the trusted folders is asked about, or refused (SECURELOAD).
                LoadCheck = path => _loadSecurity.MayLoad(path) ? null : "File load canceled: " + path,
            };
            _pythonOwner.Watch(host);
            _pythonHost?.Dispose();
            _pythonHost = host;
            _pythonSeam.Use(host);
        }

        /// <summary>
        /// A Python set in Global settings and applied takes effect at once while the engine has not started: the host is made
        /// again on it, and PYRUN and PYLOAD are given to the drawings open again. Once the engine has started the setting waits
        /// for the next start of the app (Python starts once per process).
        /// </summary>
        private void RefreshPython()
        {
            if (_pythonHost == null || _pythonHost.IsStarted || _settings.PythonPath == _pythonPathUsed)
            {
                return;
            }

            UsePythonHost();

            foreach (KnownFile file in _files.Values.Where(f => f.Session != null))
            {
                _pythonHost.AddCommandsTo(file.Session.Registry);
            }

            PythonConsole.Refresh();
        }

        /// <summary>
        /// Gives a drawing's commands PYRUN, PYLOAD and the commands PYLOAD has defined, and PYTHON and PYTHONCLOSE, which show
        /// and hide the palette; nothing where the app does not use Python.
        /// </summary>
        /// <param name="registry">The drawing's registry.</param>
        private void AddPythonCommands(CadCommandRegistry registry)
        {
            if (_pythonHost == null)
            {
                return;
            }

            _pythonHost.AddCommandsTo(registry);
            registry.Add(new CadCommand(PythonShowName, c => RunWindow(() => PythonConsole.Show())));
            registry.Add(new CadCommand(PythonHideName, c => RunWindow(() => PythonConsole.IsShown = false)));
        }

        /// <summary>A run ended: what it wrote to the command line is shown, and the drawing, its undo steps and the prompt are drawn again.</summary>
        private void AfterPythonRun()
        {
            SyncHistory();
            RaiseModified();
            Raise(nameof(Prompt));
            Raise(nameof(IsCommandRunning));
        }

        /// <summary>The palette follows the drawing shown.</summary>
        private void FollowPython(KnownFile file) => PythonConsole?.Follow(file.Document, file.Session);

        /// <summary>A drawing was closed: its console's globals are dropped and its transcript forgotten.</summary>
        private void ForgetPython(CadDocument document) => PythonConsole?.Forget(document);

        /// <summary>Ends Python at the close of the window: a run going on is stopped, then the engine is shut down.</summary>
        internal void DisposePython()
        {
            _pythonHost?.Dispose();
            _pythonOwner?.Dispose();
        }

        /// <summary>
        /// Shows the palette over a host that makes up what it says, for the screenshot's <c>--python</c>: idle with a history of
        /// entries, at a prompt of a script, or with no Python found. No engine starts.
        /// </summary>
        /// <param name="state">idle, prompt or none.</param>
        /// <returns>False where the state is not one of the three.</returns>
        internal bool UsePythonSample(string state)
        {
            string name = (state ?? string.Empty).ToLowerInvariant();

            if (_current == null || (name != "idle" && name != "prompt" && name != "none"))
            {
                return false;
            }

            var host = new PythonSampleHost(name == "none");
            PythonConsole = new PythonConsoleViewModel(host, StartCommand, () => RequestSettings("Folder preference"));
            PythonConsole.Follow(_current.Document, _current.Session);
            Raise(nameof(PythonConsole));
            host.Play(PythonConsole, _current.Session, name);
            SyncHistory();
            return true;
        }
    }
}
