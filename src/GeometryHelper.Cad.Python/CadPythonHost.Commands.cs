using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Cad.Plugins;
using Python.Runtime;

namespace GeometryHelper.Cad.Python
{
    public sealed partial class CadPythonHost
    {
        private readonly List<CadCommandRegistry> _registries = new List<CadCommandRegistry>();
        private readonly List<Defined> _defined = new List<Defined>();
        private readonly List<string> _loaded = new List<string>();
        private readonly Dictionary<string, LoadedExtras> _extras = new Dictionary<string, LoadedExtras>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>A command a loaded file defined with <c>@command</c>.</summary>
        private sealed class Defined
        {
            internal string Name;
            internal string Alias;
            internal string File;
            internal bool UsesSelection;
            internal PyObject Function;
            internal CadCommand Command;
        }

        /// <summary>What a loaded file keeps besides its commands: its own plugin host, and the functions <c>@on_unload</c> marked.</summary>
        private sealed class LoadedExtras
        {
            internal CadPluginHost Host;
            internal List<PyObject> Unloaders = new List<PyObject>();
        }

        /// <summary>
        /// Gets the plugin host of each file loaded and not unloaded, in the order the files first loaded: what the module-level
        /// code added to the window (<c>add_ribbon_tab</c> and the other <c>add_*</c> functions of the module), which the window
        /// reads after the hosts of the <c>.dll</c> plugins. A file that added nothing has an empty host.
        /// </summary>
        public IReadOnlyList<CadPluginHost> PluginHosts
        {
            get
            {
                var hosts = new List<CadPluginHost>();

                foreach (string file in _loaded)
                {
                    if (_extras.TryGetValue(file, out LoadedExtras extras))
                    {
                        hosts.Add(extras.Host);
                    }
                }

                return hosts;
            }
        }

        /// <summary>Occurs on the owner thread when <see cref="PluginHosts"/> changed in what the window draws: a file that added to the window loaded, loaded again or was unloaded.</summary>
        public event EventHandler PluginHostsChanged;

        /// <summary>
        /// Adds PYRUN, PYLOAD and the commands PYLOAD has defined to a registry, and to each command PYLOAD defines from now on:
        /// each drawing's registry is given here once.
        /// </summary>
        /// <param name="registry">The registry.</param>
        /// <exception cref="ArgumentNullException">Thrown when the registry is null.</exception>
        public void AddCommandsTo(CadCommandRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (!_registries.Contains(registry))
            {
                _registries.Add(registry);
            }

            registry.Add(new CadCommand(CadPythonNames.Run, c => FileCommand(c, false)));
            registry.Add(new CadCommand(CadPythonNames.Load, c => FileCommand(c, true)));

            foreach (Defined defined in _defined)
            {
                AddTo(registry, defined);
            }
        }

        /// <summary>PYRUN and PYLOAD: the file by the file dialog where the window shows one, else asked on the command line as SCRIPT asks.</summary>
        private IEnumerable<CadPrompt> FileCommand(CadCommandContext c, bool load)
        {
            string name = null;
            bool asked = false;

            if (c.Session.FileDialogs)
            {
                CadUiRequestEventArgs reply = c.Session.Ask(
                    "GETFILED",
                    c.Document,
                    new CadGetFileRequest(load ? "Load Python file" : "Run Python file", string.Empty, "py", 4)
                );

                if (reply.Handled)
                {
                    asked = true;
                    name = reply.Answer;
                }
            }

            if (!asked)
            {
                yield return CadPrompt.ForString("Enter Python file name");
                name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : null;
            }

            if (string.IsNullOrEmpty(name))
            {
                yield break;
            }

            if (!Path.HasExtension(name))
            {
                name += ".py";
            }

            if (!Path.IsPathRooted(name) && c.Document.HasFile)
            {
                name = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(c.Document.FileName)), name);
            }

            if (load)
            {
                // The command is running now: the load is posted, to begin when it has ended.
                CadCommandSession session = c.Session;
                string file = name;
                _owner.Post(() => LoadNow(session, file, false));
            }
            else
            {
                RunFile(c.Session, name);
            }
        }

        private IEnumerable<CadPrompt> DefinedBody(CadCommandContext c, Defined defined)
        {
            RunDefined(c.Session, defined);
            yield break;
        }

        /// <summary>Gets the files loaded and not unloaded, as full paths, in the order they first loaded.</summary>
        public IReadOnlyList<string> LoadedFiles => _loaded.ToArray();

        /// <summary>
        /// Gets the names of the commands a loaded file defined, in the order the file defined them, without their aliases; empty
        /// where the file is not loaded, or defined none (a name another command held is not one of them).
        /// </summary>
        /// <param name="path">The file, as a full path or any path to it.</param>
        /// <returns>The names.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public IReadOnlyList<string> CommandsOf(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string full = FullPath(path);
            var names = new List<string>();

            foreach (Defined defined in _defined)
            {
                if (string.Equals(defined.File, full, StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(defined.Name);
                }
            }

            return names;
        }

        /// <summary>
        /// Takes the commands and aliases of a loaded file out of every registry the host holds, and the file out of
        /// <see cref="LoadedFiles"/>. Nothing is written; what the module defined besides commands stays in Python.
        /// </summary>
        /// <param name="path">The file, as a full path or any path to it.</param>
        /// <returns>False when the file was not loaded, or a Python run is going on.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public bool UnloadFile(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string full = FullPath(path);
            int index = _loaded.FindIndex(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase));

            if (index < 0 || _current != null || _disposed)
            {
                return false;
            }

            bool changed;

            using (Py.GIL())
            {
                RunUnloaders(full);
                RemoveDefinedOf(full);
                changed = DropExtras(full);
            }

            _loaded.RemoveAt(index);

            if (changed)
            {
                PluginHostsChanged?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        /// <summary>Runs the <c>@on_unload</c> functions of a file, in the order they were defined; an error is one line and the others still run. Called with the GIL held.</summary>
        private void RunUnloaders(string full)
        {
            if (!_extras.TryGetValue(full, out LoadedExtras extras))
            {
                return;
            }

            foreach (PyObject hook in extras.Unloaders)
            {
                using (PyObject answer = _app.InvokeMethod("call_hook", hook))
                {
                    Result result = Result.Of(answer);

                    if (result.Status != "completed")
                    {
                        Emit("Python error: " + result.Summary, true, true);
                    }
                }
            }
        }

        /// <summary>Lets go of a file's hooks and plugin host; true when the host had added something to the window. Called with the GIL held.</summary>
        private bool DropExtras(string full)
        {
            if (!_extras.TryGetValue(full, out LoadedExtras extras))
            {
                return false;
            }

            foreach (PyObject hook in extras.Unloaders)
            {
                hook.Dispose();
            }

            _extras.Remove(full);
            return !extras.Host.IsEmpty;
        }

        /// <summary>Runs the unload hooks of every file loaded, as the app closes.</summary>
        private void RunAllUnloaders()
        {
            foreach (string file in _loaded.ToArray())
            {
                RunUnloaders(file);
            }
        }

        private void DisposeExtras()
        {
            foreach (LoadedExtras extras in _extras.Values)
            {
                foreach (PyObject hook in extras.Unloaders)
                {
                    hook.Dispose();
                }

                // The callbacks the file gave the window are delegates over its functions: let go of them with the rest.
                extras.Host.Clear();
            }

            _extras.Clear();
        }

        /// <summary>Starts the engine for the files of the start; where Python cannot start, writes the one line that says so (not a run's refusal) and gives false.</summary>
        internal bool StartForFiles(CadCommandSession session)
        {
            if (EnsureStarted())
            {
                return true;
            }

            string text = "Python files were not loaded: " + Problem;
            session.Write(text);
            Emit(text, true, false);
            return false;
        }

        private static string FullPath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                return path;
            }
        }

        private bool LoadNow(CadCommandSession session, string path, bool inCommand)
        {
            if (!Prepare(session, CadPythonNames.Load, path, out string source, out string full, inCommand))
            {
                return false;
            }

            string folder = Path.GetDirectoryName(full);
            string file = Path.GetFileName(full);
            string module = "geometryhelper_user_" + Identifier(Path.GetFileNameWithoutExtension(full));
            bool reached = false;
            bool hadAdditions = false;
            var host = new CadPluginHost(file, folder);

            CadPythonOutcome outcome = Execute(
                session,
                CadPythonNames.Load,
                () =>
                {
                    reached = true;

                    // Loading a file again replaces its commands, its unload hooks and its plugin host: the old ones go first,
                    // the hooks running for the copy being replaced.
                    RunUnloaders(full);
                    RemoveDefinedOf(full);
                    hadAdditions = DropExtras(full);

                    using (PyObject pyHost = host.ToPython())
                    using (
                        PyObject answer = _app.InvokeMethod(
                            "load_file",
                            new PyString(source),
                            new PyString(full),
                            new PyString(folder),
                            new PyString(module),
                            pyHost
                        )
                    )
                    {
                        return Result.Of(answer);
                    }
                },
                result =>
                {
                    Register(file, full);
                    host.Close();
                    _extras[full] = new LoadedExtras { Host = host, Unloaders = Found("loaded_unloaders") };

                    if (!_loaded.Exists(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase)))
                    {
                        _loaded.Add(full);
                    }
                },
                full
            );

            // A file that fails keeps no command (its old ones went before it ran), so it is no longer loaded.
            if (outcome != CadPythonOutcome.Completed && reached)
            {
                _loaded.RemoveAll(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase));
            }

            if (hadAdditions || (outcome == CadPythonOutcome.Completed && !host.IsEmpty))
            {
                PluginHostsChanged?.Invoke(this, EventArgs.Empty);
            }

            return outcome == CadPythonOutcome.Completed;
        }

        /// <summary>Takes the items of a list the package's <c>_app</c> returns, each as a Python object of its own; called with the GIL held.</summary>
        private List<PyObject> Found(string function)
        {
            var items = new List<PyObject>();

            using (PyObject found = _app.InvokeMethod(function))
            {
                long count = found.Length();

                for (long i = 0; i < count; i++)
                {
                    items.Add(found.GetItem((int)i));
                }
            }

            return items;
        }

        private static string Identifier(string stem)
        {
            var text = new StringBuilder();

            foreach (char c in stem)
            {
                text.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
            }

            return text.ToString();
        }

        /// <summary>Makes a command of each function the file decorated, writing what was added and what was refused.</summary>
        private void Register(string file, string full)
        {
            var added = new List<string>();

            using (PyObject found = _app.InvokeMethod("loaded_commands"))
            {
                long count = found.Length();

                for (long i = 0; i < count; i++)
                {
                    using (PyObject entry = found.GetItem((int)i))
                    {
                        string name = Text(entry, 0);
                        string alias = Text(entry, 1);
                        bool taken = false;

                        foreach (CadCommandRegistry registry in _registries)
                        {
                            taken |= registry.Find(name) != null;
                        }

                        if (taken)
                        {
                            Emit(
                                CadPythonNames.Load
                                    + ": "
                                    + name
                                    + " is a command already; "
                                    + file
                                    + " did not define it.",
                                true,
                                true
                            );
                            continue;
                        }

                        var defined = new Defined
                        {
                            Name = name,
                            Alias = alias,
                            File = full,
                            UsesSelection = Flag(entry, 4),
                            Function = entry.GetItem(3),
                        };
                        defined.Command = new CadCommand(name, c => DefinedBody(c, defined), defined.UsesSelection);
                        _defined.Add(defined);
                        added.Add(name);

                        foreach (CadCommandRegistry registry in _registries)
                        {
                            AddTo(registry, defined);
                        }
                    }
                }
            }

            added.Sort(StringComparer.Ordinal);
            Emit(file + " loaded" + (added.Count == 0 ? "." : ": " + string.Join(", ", added) + "."), false, true);
        }

        private static string Text(PyObject entry, int index)
        {
            using (PyObject item = entry.GetItem(index))
            {
                return item.IsNone() ? null : item.As<string>();
            }
        }

        private static bool Flag(PyObject entry, int index)
        {
            using (PyObject item = entry.GetItem(index))
            {
                return item.IsTrue();
            }
        }

        private void AddTo(CadCommandRegistry registry, Defined defined)
        {
            registry.Add(defined.Command);

            if (
                string.IsNullOrWhiteSpace(defined.Alias)
                || registry.Shortcuts.Aliases.ContainsKey(defined.Alias)
                || registry.Find(defined.Alias) != null
            )
            {
                return;
            }

            var aliases = new List<KeyValuePair<string, string>>(registry.Shortcuts.Aliases)
            {
                new KeyValuePair<string, string>(defined.Alias, defined.Name),
            };
            registry.Shortcuts = new CadShortcuts(aliases, registry.Shortcuts.Keys);
        }

        /// <summary>Takes the commands of one file, and their aliases, out of every registry; called with the GIL held.</summary>
        private void RemoveDefinedOf(string full)
        {
            for (int i = _defined.Count - 1; i >= 0; i--)
            {
                Defined defined = _defined[i];

                if (!string.Equals(defined.File, full, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (CadCommandRegistry registry in _registries)
                {
                    if (ReferenceEquals(registry.Find(defined.Name), defined.Command))
                    {
                        registry.Remove(defined.Name);
                    }

                    var aliases = new List<KeyValuePair<string, string>>();

                    foreach (KeyValuePair<string, string> alias in registry.Shortcuts.Aliases)
                    {
                        if (
                            !(
                                string.Equals(alias.Key, defined.Alias, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(alias.Value, defined.Name, StringComparison.OrdinalIgnoreCase)
                            )
                        )
                        {
                            aliases.Add(alias);
                        }
                    }

                    if (aliases.Count != registry.Shortcuts.Aliases.Count)
                    {
                        registry.Shortcuts = new CadShortcuts(aliases, registry.Shortcuts.Keys);
                    }
                }

                defined.Function.Dispose();
                _defined.RemoveAt(i);
            }
        }

        private void DisposeDefined()
        {
            foreach (Defined defined in _defined)
            {
                defined.Function.Dispose();
            }

            _defined.Clear();
        }
    }
}
