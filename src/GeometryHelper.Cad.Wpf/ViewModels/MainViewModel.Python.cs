using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Python;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// How the window reaches the Python files loaded in the process: load one into a drawing, list those loaded and the
    /// commands each defined, and unload one. Over <see cref="CadPythonHost"/> in the app; a test gives a fake, since the
    /// engine starts once per process.
    /// </summary>
    internal interface IPythonFiles
    {
        /// <summary>Loads a file as <see cref="CadPythonHost.LoadFile"/> does; true when it loaded with no error.</summary>
        bool Load(CadCommandSession session, string path);

        /// <summary>Gets the files loaded and not unloaded, as full paths, in the order they first loaded.</summary>
        IReadOnlyList<string> Loaded { get; }

        /// <summary>Gets the names of the commands a loaded file defined, in its order; empty for a file not loaded.</summary>
        IReadOnlyList<string> CommandsOf(string path);

        /// <summary>Takes a loaded file's commands out of every drawing; false where it was not loaded.</summary>
        bool Unload(string path);

        /// <summary>Gets the plugin host of each file loaded, in the order they first loaded: what each added to the window.</summary>
        IReadOnlyList<CadPluginHost> PluginHosts { get; }

        /// <summary>Occurs when what the files added to the window changed: one loaded, loaded again or was unloaded.</summary>
        event EventHandler PluginHostsChanged;
    }

    /// <summary>The Python files of the process, over the host.</summary>
    internal sealed class HostPythonFiles : IPythonFiles
    {
        private readonly CadPythonHost _host;

        internal HostPythonFiles(CadPythonHost host)
        {
            _host = host;
        }

        public bool Load(CadCommandSession session, string path) => _host.LoadFile(session, path);

        public IReadOnlyList<string> Loaded => _host.LoadedFiles;

        public IReadOnlyList<string> CommandsOf(string path) => _host.CommandsOf(path);

        public bool Unload(string path) => _host.UnloadFile(path);

        public IReadOnlyList<CadPluginHost> PluginHosts => _host.PluginHosts;

        public event EventHandler PluginHostsChanged
        {
            add => _host.PluginHostsChanged += value;
            remove => _host.PluginHostsChanged -= value;
        }
    }

    /// <summary>
    /// Python in the window: one <see cref="CadPythonHost"/> for the process, made when the host of the app asks for it
    /// (<see cref="CadAppHost.EnablePython"/>), PYRUN, PYLOAD, PYTHON and PYTHONCLOSE in every drawing's commands, and the
    /// PYTHON palette's view model, which follows the drawing shown. The <c>.py</c> files of the Pythons folder and of the
    /// Python Startup Suite load once, when the first drawing opens.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // The names of the commands the window adds (CadPythonNames.Show and Hide, which the host keeps to itself): PYTHON shows the palette, PYTHONCLOSE hides it.
        private const string PythonShowName = "PYTHON";
        private const string PythonHideName = "PYTHONCLOSE";

        // The window's PYLOAD, which replaces the host's: the same name as CadPythonNames.Load, which the host keeps to itself.
        private const string PythonLoadName = "PYLOAD";

        private CadPythonHost _pythonHost;
        private CadPythonWindowOwner _pythonOwner;
        private CadPythonConsoleHost _pythonSeam;
        private CadPythonStartup _pythonStartup;
        private bool _pythonStartupDone;
        private string _pythonPathUsed;
        private IPythonFiles _pythonFiles;
        private Dispatcher _pythonDispatcher;

        /// <summary>Gets or sets the Python files of the process, as Load/Unload Applications reaches them; set over the host by <see cref="UsePython"/>, null where the app does not use Python.</summary>
        internal IPythonFiles PythonFiles
        {
            get => _pythonFiles;
            set
            {
                IPythonFiles old = _pythonFiles;

                if (ReferenceEquals(old, value))
                {
                    return;
                }

                if (old != null)
                {
                    old.PluginHostsChanged -= OnPythonHostsChanged;
                }

                _pythonFiles = value;

                if (value != null)
                {
                    value.PluginHostsChanged += OnPythonHostsChanged;
                }

                // What the files added to the window follows the files: the old ones' tabs and panels go, the new ones' come.
                if ((old != null && old.PluginHosts.Count > 0) || (value != null && value.PluginHosts.Count > 0))
                {
                    RebuildPluginUi();
                }
            }
        }

        private void OnPythonHostsChanged(object sender, EventArgs e) => RebuildPluginUi();

        /// <summary>
        /// Gets or sets how the window asks for the files of PYLOAD: given the filter (<see cref="AppLoadViewModel.PythonFilter"/>)
        /// and the folder of the last load, it shows an Open dialog with several files allowed and gives the paths chosen; null
        /// where none was. Null where no window shows one, when PYLOAD says so.
        /// </summary>
        public Func<string, string, IReadOnlyList<string>> AskPythonFiles { get; set; }

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

            _pythonDispatcher = dispatcher;
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

                // The drawings a script lists, opens, makes, saves and closes are those of the window, as a program outside sees them.
                DocumentHost = new CadAppRemoteHost(this, () => "GeometryHelper CAD", _pythonDispatcher),
            };
            _pythonOwner.Watch(host);
            _pythonHost?.Dispose();
            _pythonHost = host;
            _pythonSeam.Use(host);
            _pythonStartup = new CadPythonStartup(host);
            PythonFiles = new HostPythonFiles(host);
        }

        /// <summary>
        /// Loads the Python files of the start, once, when the first drawing opens and after its AutoLISP start: the <c>.py</c>
        /// files directly in the Pythons folder, then the Python Startup Suite. The engine starts only where there is a file
        /// to load; nothing happens where the app does not use Python.
        /// </summary>
        /// <param name="session">The session of the drawing that opened.</param>
        private void RunPythonStartup(CadCommandSession session)
        {
            if (_pythonStartup == null || _pythonStartupDone)
            {
                return;
            }

            _pythonStartupDone = true;
            _pythonStartup.Run(
                session,
                CadPythonStartup.FilesToLoad(
                    session.Document.SupportRoots.ExistingOf(CadSupportKind.Pythons),
                    _settings.PythonStartupSuite
                )
            );
        }

        /// <summary>
        /// PYLOAD: asks for Python files with <see cref="AskPythonFiles"/> and loads them in the order chosen, as the Load…
        /// button of Load/Unload Applications does, the folder chosen kept for the next load.
        /// </summary>
        private void LoadPythonByDialog()
        {
            if (AskPythonFiles == null)
            {
                Say("PYLOAD needs a file dialog, which the window cannot show here.");
                return;
            }

            IReadOnlyList<string> paths = AskPythonFiles(AppLoadViewModel.PythonFilter, Settings.AppLoadFolder);

            if (paths != null && paths.Count > 0)
            {
                using (var loading = new AppLoadViewModel(this))
                {
                    loading.Load(paths);
                }
            }
        }

        /// <summary>Gives a drawing's commands the window's PYLOAD, which replaces the host's: a registry holds one PYLOAD.</summary>
        private void AddPythonLoad(CadCommandRegistry registry) =>
            registry.Add(new CadCommand(PythonLoadName, c => RunWindow(LoadPythonByDialog)));

        /// <summary>Gets the Python files loaded, as full paths in the order they loaded; empty where the app does not use Python.</summary>
        internal IReadOnlyList<string> LoadedPythonFiles =>
            PythonFiles?.Loaded ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Gets the commands a loaded Python file defined, in the order it defined them; empty where it is not loaded.</summary>
        internal IReadOnlyList<string> PythonCommandsOf(string path) =>
            PythonFiles?.CommandsOf(path) ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Unloads a Python file: its commands are unknown again in every drawing.</summary>
        internal bool UnloadPythonFile(string path) => PythonFiles != null && PythonFiles.Unload(path);

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
                AddPythonLoad(file.Session.Registry);
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
            AddPythonLoad(registry);
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
