using System;
using System.Collections.Generic;
using GeometryHelper.Cad.Python;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Python host as the palette's view model sees it (<see cref="IPythonConsoleHost"/>), with no engine: what the palette
    /// asks of it is kept for the test to read, and what the real host raises (the lines a script wrote, a run's start and
    /// end) is raised by the test through <see cref="Print"/>, <see cref="PrintError"/>, <see cref="Start"/> and
    /// <see cref="Finish"/>, on the thread of the test, as the host raises them on the window's.
    /// </summary>
    internal sealed class FakePythonConsoleHost : IPythonConsoleHost
    {
        public event Action<string, bool> Output;

        public event Action<string, CadDocument> RunStarted;

        public event Action<string, CadDocument, CadPythonOutcome, string> RunFinished;

        public bool IsStarted { get; set; }

        public string Problem { get; set; }

        public string Version { get; set; } = "3.14.4";

        public string Executable { get; set; } = @"D:\python\venv314\Scripts\python.exe";

        /// <summary>Gets what the palette handed to <see cref="Enter"/>: the drawing's session and the text.</summary>
        public List<(CadCommandSession Session, string Text)> Entered { get; } =
            new List<(CadCommandSession Session, string Text)>();

        /// <summary>Gets or sets how the host answers an entry; it may raise lines first, as the host does for a syntax error or a refusal.</summary>
        public Func<string, CadPythonEntry> OnEnter { get; set; } = text => CadPythonEntry.Ran;

        /// <summary>Gets the files the palette asked to run: the drawing's session and the path.</summary>
        public List<(CadCommandSession Session, string Path)> Files { get; } =
            new List<(CadCommandSession Session, string Path)>();

        /// <summary>Gets how many times the palette called <see cref="Stop"/>.</summary>
        public int Stops { get; private set; }

        /// <summary>Gets the drawings whose console the palette asked to reset.</summary>
        public List<CadDocument> Reset { get; } = new List<CadDocument>();

        public CadPythonEntry Enter(CadCommandSession session, string text)
        {
            Entered.Add((session, text));
            return OnEnter(text);
        }

        public void RunFile(CadCommandSession session, string path) => Files.Add((session, path));

        public void Stop() => Stops++;

        public void ResetConsole(CadDocument document) => Reset.Add(document);

        /// <summary>The host writes a line a script printed.</summary>
        public void Print(string text) => Output?.Invoke(text, false);

        /// <summary>The host writes a line of stderr, of a traceback, or one it reports itself (a refusal).</summary>
        public void PrintError(string text) => Output?.Invoke(text, true);

        /// <summary>A run begins: the step name (<c>PYRUN</c>, a command's name, <c>Python</c> for a palette entry) and its drawing.</summary>
        public void Start(string name, CadDocument document) => RunStarted?.Invoke(name, document);

        /// <summary>A run ends, or is refused.</summary>
        public void Finish(
            string name,
            CadDocument document,
            CadPythonOutcome outcome = CadPythonOutcome.Completed,
            string error = null
        ) => RunFinished?.Invoke(name, document, outcome, error);
    }

    /// <summary>What the tests of the PYTHON palette make the same way: a drawing with its session, and the command that asks a question as a script's does.</summary>
    internal static class PythonConsoleKit
    {
        /// <summary>A blank drawing called <paramref name="name"/> and its command session.</summary>
        public static CadCommandSession Drawing(string name) =>
            new CadCommandSession(CadDocument.From(new DxfDatabase("AC1027"), name));

        /// <summary>
        /// The command the host starts for the question of a script (D706), named PYTHON, that asks <c>Specify base point</c> and
        /// waits for the answer.
        /// </summary>
        public static CadCommand Question() => new CadCommand("PYTHON", Ask);

        private static IEnumerable<CadPrompt> Ask(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify base point");
        }
    }
}
