using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Python
{
    /// <summary>
    /// What <c>geometryhelper.cad</c> calls while a script runs: the drawing and its command session, and the questions a script
    /// asks, each asked as the prompt of a small command of its own so that object snaps, typed points and Esc work as at any
    /// prompt. It is valid during a run only.
    /// </summary>
    public sealed class CadPythonScript
    {
        private static volatile CadPythonScript _current;
        private readonly CadPythonHost _host;
        private readonly CadPythonRun _run;

        internal CadPythonScript(CadPythonHost host, CadPythonRun run)
        {
            _host = host;
            _run = run;
        }

        /// <summary>Gets the script of the run going on; null when none runs, where the module raises.</summary>
        public static CadPythonScript Current => _current;

        /// <summary>Gets the drawing the run started in.</summary>
        public CadDocument Document => _run.Session.Document;

        /// <summary>Gets that drawing's command session.</summary>
        public CadCommandSession Session => _run.Session;

        internal static void SetCurrent(CadPythonScript script) => _current = script;

        /// <summary>Asks a prompt at the command line and waits for the answer.</summary>
        /// <param name="prompt">The prompt, made with the <c>CadPrompt.For...</c> factories.</param>
        /// <returns>The answer.</returns>
        /// <exception cref="CadPythonCancelledException">Thrown when Esc ended the question, or an earlier question of the run.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a command is still running, so the session is not at the Command prompt.</exception>
        public CadInput Ask(CadPrompt prompt) => _host.Ask(_run, prompt);

        /// <summary>Asks "Select objects:" until Enter.</summary>
        /// <returns>The handles selected; none where Enter came first.</returns>
        /// <exception cref="CadPythonCancelledException">Thrown when Esc ended the question, or an earlier question of the run.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a command is still running, so the session is not at the Command prompt.</exception>
        public IReadOnlyList<string> AskSelection() => _host.AskSelection(_run);

        /// <summary>
        /// Runs a command to its end inside the run's undo step: the first answer is its name, each other is fed in order, an empty
        /// text as Enter, another text as typed, a number typed in invariant culture, a <c>GeoPoint2</c> as a point picked, a
        /// <c>GeoPoint3</c> typed as <c>x,y</c>, and a list of handles as the objects of "Select objects:".
        /// </summary>
        /// <param name="answers">The command's name and its answers.</param>
        /// <exception cref="ArgumentException">Thrown when the command is unknown, has ended before the last answer, or refused one.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a command is already running, or the command still asks after the last answer.</exception>
        public void Command(object[] answers) => _host.FeedCommand(_run, answers);

        /// <summary>Writes one line or more on the command line and in the palette.</summary>
        /// <param name="text">The text.</param>
        public void Write(string text) => _host.WriteLines(text ?? string.Empty, false);
    }

    /// <summary>The state of the run going on, which the host keeps and the script reads.</summary>
    internal sealed class CadPythonRun
    {
        internal CadPythonRun(string name, CadCommandSession session, string path)
        {
            Name = name;
            Session = session;
            Path = path;
        }

        internal string Name { get; }

        /// <summary>The full path of the file the run reads; null for an entry or a defined command.</summary>
        internal string Path { get; }

        internal CadCommandSession Session { get; }

        /// <summary>The thread of Python the run executes on, for <c>PythonEngine.Interrupt</c>.</summary>
        internal ulong ThreadId { get; set; }

        /// <summary>Whether Python is computing, not waiting at a prompt: read and written with the GIL held (or by the owner thread when it holds none), so that a stop cannot meet the run's end.</summary>
        internal volatile bool Computing;

        /// <summary>Whether Esc has ended a question: every later question raises at once.</summary>
        internal bool Cancelled { get; set; }

        /// <summary>Whether the session wrote *Cancel* for it already.</summary>
        internal bool CancelWritten { get; set; }
    }
}
