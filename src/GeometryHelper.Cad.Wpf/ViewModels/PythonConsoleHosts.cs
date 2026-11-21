using System;
using System.Collections.Generic;
using GeometryHelper.Cad.Python;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>The Python host of the window, as the PYTHON palette's view model sees it: its events and members given on in plain form.</summary>
    internal sealed class CadPythonConsoleHost : IPythonConsoleHost
    {
        private CadPythonHost _host;

        /// <inheritdoc />
        public event Action<string, bool> Output;

        /// <inheritdoc />
        public event Action<string, CadDocument> RunStarted;

        /// <inheritdoc />
        public event Action<string, CadDocument, CadPythonOutcome, string> RunFinished;

        /// <inheritdoc />
        public bool IsStarted => _host.IsStarted;

        /// <inheritdoc />
        public string Problem => _host.Problem;

        /// <inheritdoc />
        public string Version => _host.Environment.Version;

        /// <inheritdoc />
        public string Executable => _host.Environment.Executable;

        /// <summary>Follows a host, and lets go of the one before it (a host not started is made again where the setting changes).</summary>
        /// <param name="host">The host.</param>
        internal void Use(CadPythonHost host)
        {
            if (_host != null)
            {
                _host.Output -= OnOutput;
                _host.RunStarted -= OnStarted;
                _host.RunFinished -= OnFinished;
            }

            _host = host;
            _host.Output += OnOutput;
            _host.RunStarted += OnStarted;
            _host.RunFinished += OnFinished;
        }

        /// <inheritdoc />
        public CadPythonEntry Enter(CadCommandSession session, string text) => _host.Enter(session, text);

        /// <inheritdoc />
        public void RunFile(CadCommandSession session, string path) => _host.RunFile(session, path);

        /// <inheritdoc />
        public void Stop() => _host.Stop();

        /// <inheritdoc />
        public void ResetConsole(CadDocument document) => _host.ResetConsole(document);

        private void OnOutput(object sender, CadPythonOutputEventArgs e) => Output?.Invoke(e.Text, e.IsError);

        private void OnStarted(object sender, CadPythonRunEventArgs e) => RunStarted?.Invoke(e.Name, e.Document);

        private void OnFinished(object sender, CadPythonRunEventArgs e) =>
            RunFinished?.Invoke(e.Name, e.Document, e.Outcome ?? CadPythonOutcome.Completed, e.Error);
    }

    /// <summary>
    /// A host that says what the screenshots of the palette show: a Python 3.14 in a venv, or none found, and the lines of a few
    /// entries made up. No engine runs.
    /// </summary>
    internal sealed class PythonSampleHost : IPythonConsoleHost
    {
        private const string NotFound =
            "No Python 3.11 to 3.15 (64-bit) was found. Install one from python.org, or set its python.exe in Global settings > Python.";

        private static readonly Dictionary<string, string[]> Answers = new Dictionary<string, string[]>
        {
            ["len(cad.selection())"] = new[] { "3" },
            ["x = 2 + 3"] = new string[0],
            ["x"] = new[] { "5" },
            ["for n in range(3):\n    print(n, n * n)\n"] = new[] { "0 0", "1 1", "2 4" },
        };

        private readonly bool _none;

        internal PythonSampleHost(bool none) => _none = none;

        /// <inheritdoc />
        public event Action<string, bool> Output;

        /// <inheritdoc />
        public event Action<string, CadDocument> RunStarted;

        /// <inheritdoc />
        public event Action<string, CadDocument, CadPythonOutcome, string> RunFinished;

        /// <inheritdoc />
        public bool IsStarted => true;

        /// <inheritdoc />
        public string Problem => _none ? NotFound : null;

        /// <inheritdoc />
        public string Version => _none ? null : "3.14.4";

        /// <inheritdoc />
        public string Executable => _none ? null : @"D:\Python\venv314\Scripts\python.exe";

        /// <inheritdoc />
        public CadPythonEntry Enter(CadCommandSession session, string text)
        {
            if (Answers.TryGetValue(text, out string[] lines))
            {
                foreach (string line in lines)
                {
                    Output?.Invoke(line, false);
                }
            }

            return CadPythonEntry.Ran;
        }

        /// <inheritdoc />
        public void RunFile(CadCommandSession session, string path)
        {
            RunStarted?.Invoke("PYRUN", session.Document);
            Output?.Invoke("Ten circles and ten lines from one point.", false);
        }

        /// <inheritdoc />
        public void Stop() { }

        /// <inheritdoc />
        public void ResetConsole(CadDocument document) { }

        /// <summary>Plays what the state shows: the entries typed, and for a prompt a script run that waits for a point.</summary>
        /// <param name="console">The palette's view model over this host, following the drawing shown.</param>
        /// <param name="session">The drawing's session, where the script's question is asked.</param>
        /// <param name="state">idle, prompt or none.</param>
        internal void Play(PythonConsoleViewModel console, CadCommandSession session, string state)
        {
            console.IsShown = true;

            if (_none)
            {
                return;
            }

            if (state == "idle")
            {
                // A file run and a file loaded come first, so that the host's muted lines are among the entries.
                RunFinish("PYRUN", session.Document, "Ten circles and ten lines from one point.");
                RunFinish("PYLOAD", session.Document, "tools.py loaded: HELLO, NUMBER.");
            }

            foreach (string text in new[] { "len(cad.selection())", "x = 2 + 3", "x" })
            {
                console.InputText = text;
                console.Submit();
            }

            if (state == "idle")
            {
                console.InputText = "for n in range(3):\n    print(n, n * n)\n";
                console.Submit();
                return;
            }

            // PYRUN circles.py asking for a point: the file run first, then its question on the command line.
            console.DropFile(@"D:\Scripts\circles.py");
            session.Start(new CadCommand("PYTHON", c => new[] { CadPrompt.ForPoint("Specify base point") }));
        }

        private void RunFinish(string name, CadDocument document, string line)
        {
            RunStarted?.Invoke(name, document);
            Output?.Invoke(line, false);
            RunFinished?.Invoke(name, document, CadPythonOutcome.Completed, null);
        }
    }
}
