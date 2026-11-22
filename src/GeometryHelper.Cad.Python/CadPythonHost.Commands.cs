using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Python.Runtime;

namespace GeometryHelper.Cad.Python
{
    public sealed partial class CadPythonHost
    {
        private readonly List<CadCommandRegistry> _registries = new List<CadCommandRegistry>();
        private readonly List<Defined> _defined = new List<Defined>();
        private readonly List<string> _loaded = new List<string>();

        /// <summary>A command a loaded file defined with <c>@cad_command</c>.</summary>
        private sealed class Defined
        {
            internal string Name;
            internal string Alias;
            internal string File;
            internal PyObject Function;
            internal CadCommand Command;
        }

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

            using (Py.GIL())
            {
                RemoveDefinedOf(full);
            }

            _loaded.RemoveAt(index);
            return true;
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

            CadPythonOutcome outcome = Execute(
                session,
                CadPythonNames.Load,
                () =>
                {
                    reached = true;

                    // Loading a file again replaces its commands: the old ones go first.
                    RemoveDefinedOf(full);

                    using (
                        PyObject answer = _app.InvokeMethod(
                            "load_file",
                            new PyString(source),
                            new PyString(full),
                            new PyString(folder),
                            new PyString(module)
                        )
                    )
                    {
                        return Result.Of(answer);
                    }
                },
                result =>
                {
                    Register(file, full);

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

            return outcome == CadPythonOutcome.Completed;
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
                            Function = entry.GetItem(3),
                        };
                        defined.Command = new CadCommand(name, c => DefinedBody(c, defined));
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
