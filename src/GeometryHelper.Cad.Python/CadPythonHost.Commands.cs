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
                LoadFile(c.Session, name);
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

        private void LoadNow(CadCommandSession session, string path)
        {
            if (!Prepare(session, CadPythonNames.Load, path, out string source, out string full))
            {
                return;
            }

            string folder = Path.GetDirectoryName(full);
            string file = Path.GetFileName(full);
            string module = "geometryhelper_user_" + Identifier(Path.GetFileNameWithoutExtension(full));

            Execute(
                session,
                CadPythonNames.Load,
                () =>
                {
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
                result => Register(file, full)
            );
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
