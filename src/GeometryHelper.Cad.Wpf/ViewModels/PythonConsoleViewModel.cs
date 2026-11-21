using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using GeometryHelper.Cad.Python;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>What a line of the PYTHON palette's transcript is, which sets its colour.</summary>
    public enum PythonLineKind
    {
        /// <summary>An entry typed in the palette, shown after its <c>&gt;&gt;&gt; </c> or <c>... </c> mark.</summary>
        Echo,

        /// <summary>A line a script printed, or the value of an expression.</summary>
        Output,

        /// <summary>A line of stderr or of a traceback, and <c>KeyboardInterrupt</c>.</summary>
        Error,

        /// <summary>A line of the host's own: a run that did not start in the palette, a refusal, the result of PYLOAD.</summary>
        Host,
    }

    /// <summary>One line of the PYTHON palette's transcript.</summary>
    public sealed class PythonLine
    {
        internal PythonLine(PythonLineKind kind, string mark, string text)
        {
            Kind = kind;
            Mark = mark ?? string.Empty;
            Text = text ?? string.Empty;
        }

        /// <summary>Gets what the line is.</summary>
        public PythonLineKind Kind { get; }

        /// <summary>Gets the mark before the text, <c>&gt;&gt;&gt; </c> for the first line of an entry and <c>... </c> for the others; empty for any other line.</summary>
        public string Mark { get; }

        /// <summary>Gets the text of the line, without its end.</summary>
        public string Text { get; }
    }

    /// <summary>How the status line of the PYTHON palette is lit: a green dot, an amber one while a run goes on, a red one where Python is not available.</summary>
    public enum PythonStatusKind
    {
        /// <summary>Python is there and nothing runs.</summary>
        Ready,

        /// <summary>A run is going on, computing or at a prompt.</summary>
        Busy,

        /// <summary>Python cannot be used: the environment's problem is shown.</summary>
        Problem,
    }

    /// <summary>
    /// What the PYTHON palette needs of the Python host, in plain members, so that its view model can be driven with no engine and
    /// no window. The events come on the thread that owns the drawings, as the host's own do.
    /// </summary>
    internal interface IPythonConsoleHost
    {
        /// <summary>A whole line a script wrote (text, whether it is an error line), or an error line the host reports.</summary>
        event Action<string, bool> Output;

        /// <summary>A run began (step name, drawing): <c>PYRUN</c>, the name of a command PYLOAD defined, or <c>Python</c> for a palette entry.</summary>
        event Action<string, CadDocument, string> RunStarted;

        /// <summary>A run ended or was refused (step name, drawing, outcome, the last line of the traceback or the refusal text).</summary>
        event Action<string, CadDocument, CadPythonOutcome, string> RunFinished;

        /// <summary>Gets whether the engine has started.</summary>
        bool IsStarted { get; }

        /// <summary>Gets why Python cannot be used; null when it can.</summary>
        string Problem { get; }

        /// <summary>Gets the version of the Python found, <c>3.14.4</c>; null when none.</summary>
        string Version { get; }

        /// <summary>Gets the python.exe the engine uses; null when none was found.</summary>
        string Executable { get; }

        /// <summary>Runs or refuses a palette entry; see <see cref="CadPythonHost.Enter"/>.</summary>
        CadPythonEntry Enter(CadCommandSession session, string text);

        /// <summary>Posts a run of a file; see <see cref="CadPythonHost.RunFile"/>.</summary>
        void RunFile(CadCommandSession session, string path);

        /// <summary>Stops the run computing; see <see cref="CadPythonHost.Stop"/>.</summary>
        void Stop();

        /// <summary>Gives a drawing's console new globals.</summary>
        void ResetConsole(CadDocument document);
    }

    /// <summary>
    /// The PYTHON palette: a console under the drawing for Python, one per drawing. It keeps what each drawing's console
    /// printed (the last 5 000 lines), the entries typed there (the last 200) and the text being typed, follows the drawing
    /// shown, and says in its status line whether Python is ready, running a script, waiting for it to answer a prompt, or not
    /// available. The window's palette is <c>PythonConsolePalette</c>; the host's work is reached through a narrow seam.
    /// </summary>
    public sealed class PythonConsoleViewModel : ObservableObject
    {
        /// <summary>The lines of a drawing's transcript that are kept.</summary>
        internal const int MostLines = 5000;

        /// <summary>The entries of a drawing's console that are kept for Up and Down.</summary>
        internal const int MostEntries = 200;

        // The step name the host gives a palette entry (CadPythonNames.Entry, which the host keeps to itself) and PYLOAD's.
        private const string EntryStep = "Python";
        private const string LoadStep = "PYLOAD";
        private const string Hint = "Enter runs · Shift+Enter new line · Up/Down history";
        private static readonly ObservableCollection<PythonLine> NoLines = new ObservableCollection<PythonLine>();

        private readonly IPythonConsoleHost _host;
        private readonly Action<string> _startCommand;
        private readonly Action _openSettings;
        private readonly Dictionary<CadDocument, ConsoleState> _consoles = new Dictionary<CadDocument, ConsoleState>();
        private ConsoleState _active;
        private ConsoleState _running;
        private string _runName;
        private string _runFile;
        private string _pendingFile;
        private List<PythonLine> _collected;
        private bool _isShown;

        /// <summary>Makes the palette's view model over the host's seam.</summary>
        /// <param name="host">The Python host.</param>
        /// <param name="startCommand">Starts a command on the drawing shown, as a button of the window does: <c>PYRUN</c> and <c>PYLOAD</c>.</param>
        /// <param name="openSettings">Shows Global settings on Folder preference, where the Python is set.</param>
        internal PythonConsoleViewModel(IPythonConsoleHost host, Action<string> startCommand, Action openSettings)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _startCommand = startCommand ?? throw new ArgumentNullException(nameof(startCommand));
            _openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
            _host.Output += OnOutput;
            _host.RunStarted += OnRunStarted;
            _host.RunFinished += OnRunFinished;
            RunFileCommand = new DelegateCommand(_ => _startCommand("PYRUN"));
            LoadCommand = new DelegateCommand(_ => _startCommand("PYLOAD"));
            StopCommand = new DelegateCommand(_ => Stop());
            ClearCommand = new DelegateCommand(_ => Clear());
            ResetCommand = new DelegateCommand(_ => ResetConsole());
            CloseCommand = new DelegateCommand(_ => IsShown = false);
            SettingsCommand = new DelegateCommand(_ => _openSettings());
        }

        /// <summary>Gets the command of Run file…: PYRUN, which asks for the file as it does on the command line.</summary>
        public ICommand RunFileCommand { get; }

        /// <summary>Gets the command of Load…: PYLOAD.</summary>
        public ICommand LoadCommand { get; }

        /// <summary>Gets the command of Stop: <see cref="Stop"/>.</summary>
        public ICommand StopCommand { get; }

        /// <summary>Gets the command of Clear: the transcript of the drawing shown emptied.</summary>
        public ICommand ClearCommand { get; }

        /// <summary>Gets the command of Reset console: <see cref="ResetConsole"/>.</summary>
        public ICommand ResetCommand { get; }

        /// <summary>Gets the command of the close button: the palette hidden, as PYTHONCLOSE hides it.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Gets the command of the status line's Global settings… and of the banner's button.</summary>
        public ICommand SettingsCommand { get; }

        /// <summary>Gets or sets whether the palette is shown: PYTHON shows it, PYTHONCLOSE and its close button hide it.</summary>
        public bool IsShown
        {
            get => _isShown;
            set => Set(ref _isShown, value);
        }

        /// <summary>Raised by <see cref="Show"/>: the keyboard goes to the input, where the palette was shown already as well as where it was hidden.</summary>
        public event EventHandler InputFocusRequested;

        /// <summary>Shows the palette, as PYTHON does, and asks for the keyboard to go to its input.</summary>
        public void Show()
        {
            IsShown = true;
            InputFocusRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Gets the name of the drawing the console shown belongs to; empty with none open.</summary>
        public string DocumentName => _active?.Document.Title ?? string.Empty;

        /// <summary>Gets the lines of the console shown, oldest first; the view follows the collection as lines are added to it.</summary>
        public ObservableCollection<PythonLine> Lines => _active?.Lines ?? NoLines;

        /// <summary>
        /// Gets or sets the text being typed in the input of the console shown, its lines parted by <c>\n</c> where it is
        /// more than one. Set by the input box; the view model changes it with <see cref="Submit"/> and the history walk.
        /// </summary>
        public string InputText
        {
            get => _active?.Input ?? string.Empty;
            set
            {
                if (_active != null)
                {
                    _active.Input = Normal(value);
                    Raise(nameof(InputMarks));
                }
            }
        }

        /// <summary>Gets the marks that stand before the lines of the input: <c>&gt;&gt;&gt;</c>, then <c>...</c> for each line after the first, one mark per line.</summary>
        public string InputMarks => ">>>" + MoreMarks(InputText);

        /// <summary>Gets whether the input takes text: Python is usable, a drawing is open and no run is going on.</summary>
        public bool IsInputEnabled => IsUsable && _active != null && _running == null;

        /// <summary>Gets what the input says while it is greyed: why, as <c>circles.py is running: Esc stops it</c>; empty while it takes text.</summary>
        public string InputPlaceholder
        {
            get
            {
                if (!IsUsable)
                {
                    return "No Python";
                }

                if (_active == null)
                {
                    return "No drawing open";
                }

                if (_running == null)
                {
                    return string.Empty;
                }

                return IsAtPrompt ? "Answer on the command line, or press Esc" : RunLabel + " is running: Esc stops it";
            }
        }

        /// <summary>Gets whether Python can be used (the environment is usable and the engine did not fail to start).</summary>
        public bool IsUsable => _host.Problem == null;

        /// <summary>Gets why Python cannot be used, exactly as the environment words it; null when it can.</summary>
        public string Problem => _host.Problem;

        /// <summary>Gets whether a run is going on, in any drawing, computing or at a prompt.</summary>
        public bool IsRunning => _running != null;

        /// <summary>Gets whether the run going on waits at a prompt of its drawing, to be answered on the command line.</summary>
        public bool IsAtPrompt => _running?.Session != null && _running.Session.IsRunning;

        /// <summary>Gets whether Run file… and Load… can be pressed: Python is usable, a drawing is open and nothing runs.</summary>
        public bool CanRunFile => IsInputEnabled;

        /// <summary>Gets whether Reset console can be pressed: as <see cref="CanRunFile"/>.</summary>
        public bool CanReset => IsInputEnabled;

        /// <summary>Gets whether Stop can be pressed: a run is going on.</summary>
        public bool CanStop => _running != null;

        /// <summary>Gets how the dot of the status line is lit.</summary>
        public PythonStatusKind StatusKind =>
            !IsUsable ? PythonStatusKind.Problem
            : _running != null ? PythonStatusKind.Busy
            : PythonStatusKind.Ready;

        /// <summary>Gets the left text of the status line: the Python in use, or what runs, or that none was found.</summary>
        public string StatusLeft
        {
            get
            {
                if (!IsUsable)
                {
                    return string.IsNullOrEmpty(_host.Executable) ? "No Python found" : "Python is not available";
                }

                if (_running != null)
                {
                    return IsAtPrompt
                        ? RunLabel + " waits at a prompt: " + _running.Session.PromptText.TrimEnd(':', ' ')
                        : "Running " + RunLabel + " in " + _running.Document.Title;
                }

                return "Python "
                    + _host.Version
                    + " ("
                    + ProgramLabel(_host.Executable)
                    + ")"
                    + (_host.IsStarted ? string.Empty : " · starts at the first run");
            }
        }

        /// <summary>Gets the right text of the status line: the keys, how the last run of the drawing ended where it did not complete, or what stops the run going on.</summary>
        public string StatusRight
        {
            get
            {
                if (!IsUsable)
                {
                    return string.Empty;
                }

                if (_running != null)
                {
                    return IsAtPrompt ? "Esc or Stop cancels" : "Esc stops it";
                }

                return _active?.Result ?? Hint;
            }
        }

        /// <summary>Gets whether the right of the status line is the link Global settings… (where Python is not available).</summary>
        public bool ShowsSettingsLink => !IsUsable;

        /// <summary>Gets the title of the banner at the top of the transcript where Python cannot be used.</summary>
        public string BannerTitle => "Python is not available";

        /// <summary>
        /// Names the program the status line shows in brackets: the folder of a venv (<c>venv314</c> for
        /// <c>...\venv314\Scripts\python.exe</c>), else the folder that holds python.exe.
        /// </summary>
        /// <param name="executable">The full path of python.exe; null or empty for none.</param>
        /// <returns>The name, or empty for none.</returns>
        internal static string ProgramLabel(string executable)
        {
            string folder = string.IsNullOrEmpty(executable) ? null : Path.GetDirectoryName(executable);

            if (string.IsNullOrEmpty(folder))
            {
                return string.Empty;
            }

            if (string.Equals(Path.GetFileName(folder), "Scripts", StringComparison.OrdinalIgnoreCase))
            {
                string venv = Path.GetDirectoryName(folder);

                if (!string.IsNullOrEmpty(venv))
                {
                    return Path.GetFileName(venv);
                }
            }

            return Path.GetFileName(folder);
        }

        /// <summary>Shows the palette over the drawing it is given, which is the one it follows from now; called as the drawing shown changes.</summary>
        /// <param name="document">The drawing shown; null with none.</param>
        /// <param name="session">Its command session.</param>
        internal void Follow(CadDocument document, CadCommandSession session)
        {
            ConsoleState next = null;

            if (document != null)
            {
                next = ConsoleOf(document, session);
            }

            if (next == _active)
            {
                return;
            }

            _active = next;
            RaiseActive();
        }

        /// <summary>Lights the state again where the host changed under it (a Python set in Global settings and applied before the engine started).</summary>
        internal void Refresh() => RaiseState();

        /// <summary>Forgets the console of a drawing that was closed: its transcript and entries go, and its globals are dropped.</summary>
        /// <param name="document">The drawing.</param>
        internal void Forget(CadDocument document)
        {
            if (document == null || !_consoles.TryGetValue(document, out ConsoleState console))
            {
                return;
            }

            _consoles.Remove(document);

            if (console.Session != null)
            {
                console.Session.PromptChanged -= OnPromptChanged;
            }

            _host.ResetConsole(document);

            if (_running == console)
            {
                _running = null;
            }

            if (_active == console)
            {
                _active = null;
                RaiseActive();
            }
        }

        /// <summary>
        /// Runs the entry typed, as Enter does: a complete entry is echoed to the transcript, run and cleared and put in the
        /// entries kept; an incomplete one (<c>def area(r):</c>) keeps its text and opens a <c>...</c> line; a refused one
        /// (a run is going on, a command is running) keeps its text and shows the refusal. An empty input does nothing.
        /// </summary>
        public void Submit()
        {
            ConsoleState console = _active;

            if (console == null || !IsInputEnabled)
            {
                return;
            }

            string text = console.Input;

            // A blank line ends a block, so it is sent; a blank first line is not an entry.
            if (text.IndexOf('\n') < 0 && text.Trim().Length == 0)
            {
                SetInput(string.Empty);
                return;
            }

            // What the host writes while it takes the entry is held, to be placed after the echo, or as a refusal.
            var held = new List<PythonLine>();
            _collected = held;
            CadPythonEntry result;

            try
            {
                result = _host.Enter(console.Session, text);
            }
            finally
            {
                _collected = null;

                // A start that failed turns Python to its problem, whatever came of the entry.
                RaiseState();
            }

            if (result == CadPythonEntry.Incomplete)
            {
                SetInput(text + "\n");
                return;
            }

            if (result == CadPythonEntry.Refused)
            {
                foreach (PythonLine line in held)
                {
                    Add(console, PythonLineKind.Host, string.Empty, line.Text);
                }

                return;
            }

            string[] typed = text.Split('\n');

            for (int i = 0; i < typed.Length; i++)
            {
                Add(console, PythonLineKind.Echo, i == 0 ? ">>> " : "... ", typed[i]);
            }

            foreach (PythonLine line in held)
            {
                Add(console, line.Kind, string.Empty, line.Text);
            }

            Remember(console, text);
            SetInput(string.Empty);
        }

        private static void Remember(ConsoleState console, string text)
        {
            string entry = text.TrimEnd('\n');

            if (console.Entries.Count == 0 || console.Entries[console.Entries.Count - 1] != entry)
            {
                console.Entries.Add(entry);

                if (console.Entries.Count > MostEntries)
                {
                    console.Entries.RemoveAt(0);
                }
            }

            console.Walk = console.Entries.Count;
            console.Draft = string.Empty;
        }

        /// <summary>
        /// Walks to the entry before the one shown, as Up does at the first line of the input: the text being typed is kept first,
        /// to come back to. False where there is none before, or while the input is greyed.
        /// </summary>
        /// <returns>Whether an entry was put in the input.</returns>
        public bool HistoryUp()
        {
            ConsoleState console = _active;

            if (console == null || !IsInputEnabled || console.Entries.Count == 0 || console.Walk == 0)
            {
                return false;
            }

            if (console.Walk == console.Entries.Count)
            {
                console.Draft = console.Input;
            }

            console.Walk--;
            SetInput(console.Entries[console.Walk]);
            return true;
        }

        /// <summary>
        /// Walks to the entry after the one shown, as Down does at the last line of the input; past the newest, the text that was
        /// being typed comes back. False where the input shows no entry of the walk, or while it is greyed.
        /// </summary>
        /// <returns>Whether the input was changed.</returns>
        public bool HistoryDown()
        {
            ConsoleState console = _active;

            if (console == null || !IsInputEnabled || console.Walk >= console.Entries.Count)
            {
                return false;
            }

            console.Walk++;
            SetInput(console.Walk == console.Entries.Count ? console.Draft : console.Entries[console.Walk]);
            return true;
        }

        /// <summary>
        /// Runs a Python file dropped on the transcript, as PYRUN does: a host line names it and the run is posted. Refused while
        /// the input is greyed, and for any file that is not a <c>.py</c>.
        /// </summary>
        /// <param name="path">The full path of the file.</param>
        /// <returns>Whether the run was posted.</returns>
        public bool DropFile(string path)
        {
            if (
                _active == null
                || !IsInputEnabled
                || string.IsNullOrEmpty(path)
                || !string.Equals(Path.GetExtension(path), ".py", StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            _pendingFile = Path.GetFileName(path);
            _host.RunFile(_active.Session, path);
            return true;
        }

        /// <summary>
        /// Stops the run going on: at a prompt Esc is pressed on its drawing's session (the question is cancelled and the script
        /// unwinds); while Python computes the host interrupts it, as Esc does.
        /// </summary>
        public void Stop()
        {
            ConsoleState console = _running;

            if (console == null)
            {
                return;
            }

            _host.Stop();

            if (console.Session != null && console.Session.IsRunning)
            {
                console.Session.Escape();
            }
        }

        /// <summary>Empties the transcript of the drawing shown. The entries kept and the globals stay.</summary>
        public void Clear() => _active?.Lines.Clear();

        /// <summary>Gives the console of the drawing shown new globals: what it defined and imported is forgotten. A host line says so.</summary>
        public void ResetConsole()
        {
            if (_active == null || !CanReset)
            {
                return;
            }

            _host.ResetConsole(_active.Document);
            Add(_active, PythonLineKind.Host, string.Empty, "Console reset.");
        }

        /// <summary>The prompt of the carrier changed, or its command ended: the state of the palette is lit again.</summary>
        private void OnPromptChanged(object sender, EventArgs e) => RaiseState();

        private void OnOutput(string text, bool isError)
        {
            PythonLineKind kind = isError ? PythonLineKind.Error : PythonLineKind.Output;

            // Lines of an entry being handed over wait for the answer: a refusal is a host line, the rest follow the echo.
            if (_collected != null)
            {
                _collected.Add(new PythonLine(kind, string.Empty, text));
                return;
            }

            ConsoleState console = _running ?? _active;

            if (console != null)
            {
                Add(console, kind, string.Empty, text);
            }
        }

        private void OnRunStarted(string name, CadDocument document, string path)
        {
            ConsoleState console = ConsoleOf(document, null);
            _running = console;
            _runName = name;
            // The host names the file of a PYRUN or PYLOAD however it was started (typed, dropped, the toolbar).
            _runFile = path != null ? Path.GetFileName(path) : _pendingFile;
            _pendingFile = null;
            console.Result = null;

            if (name != EntryStep)
            {
                Add(console, PythonLineKind.Host, string.Empty, _runFile == null ? name : name + " " + _runFile);
            }

            RaiseState();
        }

        private void OnRunFinished(string name, CadDocument document, CadPythonOutcome outcome, string error)
        {
            if (outcome == CadPythonOutcome.Refused)
            {
                // The refusal came as an error line just before: it is the host's word, not a script's.
                _pendingFile = null;
                ConsoleState shown = _running ?? _active;

                if (shown != null)
                {
                    Recolour(shown, error, PythonLineKind.Host);
                }

                RaiseState();
                return;
            }

            // The drawing may have been closed under the run: its console is gone, and only the state is put right.
            _consoles.TryGetValue(document, out ConsoleState console);
            console = _running ?? console;
            string what = _runFile ?? name;

            if (console == null)
            {
                // Nothing to write to.
            }
            else if (outcome == CadPythonOutcome.Failed)
            {
                console.Result = what + " failed: the drawing is as before";
            }
            else if (outcome == CadPythonOutcome.Cancelled)
            {
                console.Result = what + " was cancelled: the drawing is as before";
            }
            else if (name == LoadStep && console.Lines.Count > 0)
            {
                // PYLOAD's last line, "tools.py loaded: HELLO, NUMBER.", is the host's own.
                PythonLine last = console.Lines[console.Lines.Count - 1];

                if (last.Kind == PythonLineKind.Output && last.Text.Contains(" loaded") && last.Text.EndsWith("."))
                {
                    Recolour(console, last.Text, PythonLineKind.Host);
                }
            }

            _running = null;
            _runName = null;
            _runFile = null;
            RaiseState();
        }

        private string RunLabel => _runFile ?? _runName ?? EntryStep;

        private ConsoleState ConsoleOf(CadDocument document, CadCommandSession session)
        {
            if (!_consoles.TryGetValue(document, out ConsoleState console))
            {
                console = new ConsoleState(document, session);
                _consoles[document] = console;

                if (session != null)
                {
                    session.PromptChanged += OnPromptChanged;
                }
            }
            else if (console.Session == null && session != null)
            {
                console.Session = session;
                session.PromptChanged += OnPromptChanged;
            }

            return console;
        }

        private void Add(ConsoleState console, PythonLineKind kind, string mark, string text)
        {
            console.Lines.Add(new PythonLine(kind, mark, text));

            while (console.Lines.Count > MostLines)
            {
                console.Lines.RemoveAt(0);
            }
        }

        // The last line of a console that reads as the text is given another kind (the refusals, PYLOAD's result).
        private static void Recolour(ConsoleState console, string text, PythonLineKind kind)
        {
            for (int i = console.Lines.Count - 1; i >= 0 && i >= console.Lines.Count - 4; i--)
            {
                PythonLine line = console.Lines[i];

                if (line.Text == text && line.Kind != kind && line.Kind != PythonLineKind.Echo)
                {
                    console.Lines[i] = new PythonLine(kind, string.Empty, text);
                    return;
                }
            }
        }

        private void SetInput(string text)
        {
            if (_active != null)
            {
                _active.Input = text;
                Raise(nameof(InputText));
                Raise(nameof(InputMarks));
            }
        }

        private void RaiseActive()
        {
            Raise(nameof(DocumentName));
            Raise(nameof(Lines));
            Raise(nameof(InputText));
            Raise(nameof(InputMarks));
            RaiseState();
        }

        private void RaiseState()
        {
            Raise(nameof(IsUsable));
            Raise(nameof(Problem));
            Raise(nameof(IsRunning));
            Raise(nameof(IsAtPrompt));
            Raise(nameof(IsInputEnabled));
            Raise(nameof(InputPlaceholder));
            Raise(nameof(CanRunFile));
            Raise(nameof(CanReset));
            Raise(nameof(CanStop));
            Raise(nameof(StatusKind));
            Raise(nameof(StatusLeft));
            Raise(nameof(StatusRight));
            Raise(nameof(ShowsSettingsLink));
        }

        private static string Normal(string text) => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

        private static string MoreMarks(string text)
        {
            var marks = new System.Text.StringBuilder();

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    marks.Append("\n...");
                }
            }

            return marks.ToString();
        }

        /// <summary>What the palette keeps of one drawing's console.</summary>
        private sealed class ConsoleState
        {
            internal ConsoleState(CadDocument document, CadCommandSession session)
            {
                Document = document;
                Session = session;
            }

            internal CadDocument Document { get; }

            internal CadCommandSession Session { get; set; }

            internal ObservableCollection<PythonLine> Lines { get; } = new ObservableCollection<PythonLine>();

            internal List<string> Entries { get; } = new List<string>();

            // Where Up and Down are in Entries: Entries.Count is the text being typed, not an entry.
            internal int Walk { get; set; }

            internal string Draft { get; set; } = string.Empty;

            internal string Input { get; set; } = string.Empty;

            internal string Result { get; set; }
        }
    }
}
