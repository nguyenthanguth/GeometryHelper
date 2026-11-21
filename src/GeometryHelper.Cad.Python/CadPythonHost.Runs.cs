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
        private const string CommandRunning = "A command is running: finish it or press Esc first.";

        /// <summary>What a guard of <c>_app.py</c> gave back: how the code ended, the last line of the error and the whole traceback.</summary>
        private sealed class Result
        {
            internal string Status = "completed";
            internal string Summary = string.Empty;
            internal string Trace = string.Empty;

            internal static Result Of(PyObject list)
            {
                var result = new Result();

                using (PyObject status = list.GetItem(0))
                using (PyObject summary = list.GetItem(1))
                using (PyObject trace = list.GetItem(2))
                {
                    result.Status = status.As<string>();
                    result.Summary = summary.As<string>();
                    result.Trace = trace.As<string>();
                }

                return result;
            }
        }

        /// <summary>
        /// Posts a run of a Python file, which begins after the command that asked for it has ended: PYRUN, one undo step. The
        /// file is read, after <see cref="LoadCheck"/>, when the run begins.
        /// </summary>
        /// <param name="session">The command session of the drawing to work in.</param>
        /// <param name="path">The file.</param>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public void RunFile(CadCommandSession session, string path)
        {
            Check(session, path);
            _owner.Post(() => RunNow(session, path));
        }

        /// <summary>
        /// Posts the load of a Python file that defines commands (<c>@cad_command</c>): PYLOAD. Its module-level code runs as one
        /// undo step named PYLOAD, and each command it defines is added to the registries <see cref="AddCommandsTo"/> was given.
        /// </summary>
        /// <param name="session">The command session of the drawing to work in.</param>
        /// <param name="path">The file.</param>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public void LoadFile(CadCommandSession session, string path)
        {
            Check(session, path);
            _owner.Post(() => LoadNow(session, path));
        }

        /// <summary>
        /// Runs a palette entry as <c>python -i</c> does, at once, as one undo step named Python: an expression's value is written
        /// by <c>repr</c>, an incomplete entry is not run, a syntax error is reported.
        /// </summary>
        /// <param name="session">The command session of the drawing the console belongs to.</param>
        /// <param name="text">The entry: one or more lines.</param>
        /// <returns>What was done with it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the session or the text is null.</exception>
        public CadPythonEntry Enter(CadCommandSession session, string text)
        {
            Check(session, text);

            if (_current != null)
            {
                Emit(BusyText(), true, false);
                return CadPythonEntry.Refused;
            }

            if (session.IsRunning)
            {
                Emit(CommandRunning, true, false);
                return CadPythonEntry.Refused;
            }

            if (!EnsureStarted())
            {
                Emit(Problem, true, false);
                return CadPythonEntry.Refused;
            }

            Result compiled;

            using (Py.GIL())
            using (PyObject answer = _app.InvokeMethod("console_compile", new PyString(text)))
            {
                compiled = Result.Of(answer);
            }

            if (compiled.Status == "incomplete")
            {
                return CadPythonEntry.Incomplete;
            }

            // The palette echoes the entry itself, in its own colour (D727): nothing of it goes through Output.
            if (compiled.Status == "error")
            {
                EmitErrors(compiled.Trace);
                session.Write("Python error: " + compiled.Summary);
                return CadPythonEntry.Ran;
            }

            // The entry runs on a posted run, as every run does, so that it begins with the session at the Command prompt.
            _owner.Post(() => EnterNow(session, text));
            return CadPythonEntry.Ran;
        }

        private void EnterNow(CadCommandSession session, string text)
        {
            string busy =
                _current != null ? BusyText()
                : session.IsRunning ? CommandRunning
                : null;

            if (busy != null)
            {
                Refuse(session, CadPythonNames.Entry, busy);
                return;
            }

            Execute(
                session,
                CadPythonNames.Entry,
                () =>
                {
                    using (PyObject compiled = _app.InvokeMethod("console_compile", new PyString(text)))
                    {
                        Result result = Result.Of(compiled);

                        if (result.Status != "ok")
                        {
                            return result.Status == "error" ? Failed(result) : new Result();
                        }
                    }

                    return ConsoleRun(session.Document);
                },
                null
            );
        }

        private static Result Failed(Result error)
        {
            error.Status = "failed";
            return error;
        }

        /// <summary>Gives the console of a drawing new globals: what it defined and imported there is forgotten.</summary>
        /// <param name="document">The drawing.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public void ResetConsole(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (IsStarted && !_disposed && _consoles.TryGetValue(document, out PyObject globals))
            {
                _consoles.Remove(document);

                using (Py.GIL())
                {
                    globals.Dispose();
                }
            }
        }

        private static void Check(CadCommandSession session, string text)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
        }

        private string BusyText() =>
            "A Python script is running in " + _current.Session.Document.Title + ": wait for it or press Esc.";

        private Result ConsoleRun(CadDocument document)
        {
            if (!_consoles.TryGetValue(document, out PyObject globals))
            {
                globals = _app.InvokeMethod("new_console");
                _consoles[document] = globals;
            }

            using (PyObject answer = _app.InvokeMethod("console_run", globals))
            {
                return Result.Of(answer);
            }
        }

        /// <summary>Begins a posted run, after the checks every run starts with: none going on, Python started, the file allowed and read.</summary>
        private bool Prepare(CadCommandSession session, string step, string path, out string source, out string full)
        {
            source = null;
            full = null;

            string busy =
                _current != null ? BusyText()
                : session.IsRunning ? CommandRunning
                : null;

            if (busy != null)
            {
                Refuse(session, step, busy);
                return false;
            }

            if (!EnsureStarted())
            {
                Refuse(session, step, Problem);
                return false;
            }

            string name = Path.GetFileName(path);

            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                Refuse(session, step, "Cannot find Python file \"" + path + "\".");
                return false;
            }

            string refusal = LoadCheck?.Invoke(full);

            if (refusal == null && !File.Exists(full))
            {
                refusal = "Cannot find Python file \"" + path + "\".";
            }

            if (refusal == null)
            {
                try
                {
                    source = ReadScript(full);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    refusal = name + " could not be read: " + exception.Message;
                }
            }

            if (refusal != null)
            {
                Refuse(session, step, refusal);
                return false;
            }

            return true;
        }

        /// <summary>The one place a file is read at the user's request, so that a trust check (L6) has one door to guard.</summary>
        private static string ReadScript(string full) => File.ReadAllText(full, new UTF8Encoding(false));

        private void Refuse(CadCommandSession session, string step, string text)
        {
            session.Write(text);
            Emit(text, true, false);
            RaiseFinished(step, session.Document, CadPythonOutcome.Refused, text);
        }

        private void RunNow(CadCommandSession session, string path)
        {
            if (!Prepare(session, CadPythonNames.Run, path, out string source, out string full))
            {
                return;
            }

            string folder = Path.GetDirectoryName(full);

            Execute(
                session,
                CadPythonNames.Run,
                () =>
                {
                    using (
                        PyObject answer = _app.InvokeMethod(
                            "run_file",
                            new PyString(source),
                            new PyString(full),
                            new PyString(folder)
                        )
                    )
                    {
                        return Result.Of(answer);
                    }
                },
                null,
                full
            );
        }

        /// <summary>Posts a run of the function of a command PYLOAD defined, which begins after the command's own end.</summary>
        private void RunDefined(CadCommandSession session, Defined defined) =>
            _owner.Post(() =>
            {
                string busy =
                    _current != null ? BusyText()
                    : session.IsRunning ? CommandRunning
                    : null;

                if (busy != null)
                {
                    Refuse(session, defined.Name, busy);
                    return;
                }

                if (!EnsureStarted())
                {
                    Refuse(session, defined.Name, Problem);
                    return;
                }

                Execute(
                    session,
                    defined.Name,
                    () =>
                    {
                        using (PyObject answer = _app.InvokeMethod("call", defined.Function))
                        {
                            return Result.Of(answer);
                        }
                    },
                    null
                );
            });

        /// <summary>
        /// Runs code as one run: the undo step opened, the code run under the GIL on this thread, a stray interrupt drained, the
        /// groups ended (kept where it completed, cancelled else), the outcome written. Never throws to its caller.
        /// </summary>
        private CadPythonOutcome Execute(
            CadCommandSession session,
            string step,
            Func<Result> work,
            Action<Result> afterwards,
            string path = null
        )
        {
            CadDocument document = session.Document;
            var run = new CadPythonRun(step, session, path);
            Result result = new Result { Status = "failed" };
            string hostError = null;
            _current = run;

            try
            {
                document.BeginGroup(step);
                RaiseStarted(run);

                using (Py.GIL())
                {
                    run.ThreadId = PythonEngine.GetPythonThreadID();
                    RestoreStreams();
                    CadPythonScript.SetCurrent(new CadPythonScript(this, run));
                    run.Computing = true;

                    try
                    {
                        result = work();

                        if (result.Status == "completed")
                        {
                            afterwards?.Invoke(result);
                        }
                    }
                    finally
                    {
                        run.Computing = false;
                    }

                    // An interrupt that landed as the script ended waits for the next bytecode: it is taken here, not by the next run.
                    try
                    {
                        PythonEngine.Exec("try:\n    pass\nexcept KeyboardInterrupt:\n    pass");
                    }
                    catch (PythonException) { }
                }
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadPythonHost: the run " + step + " failed in the host.", exception);
                hostError = exception.Message;
                result = new Result { Status = "failed" };
            }

            CadPythonOutcome outcome =
                result.Status == "completed" ? CadPythonOutcome.Completed
                : result.Status == "cancelled" ? CadPythonOutcome.Cancelled
                : CadPythonOutcome.Failed;

            try
            {
                if (session.IsRunning)
                {
                    session.Escape();
                }

                if (document.IsInGroup)
                {
                    if (outcome == CadPythonOutcome.Completed)
                    {
                        while (document.IsInGroup)
                        {
                            document.EndGroup();
                        }
                    }
                    else
                    {
                        document.CancelGroup();
                    }
                }
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadPythonHost: the undo step of " + step + " could not be ended.", exception);
                hostError = hostError ?? exception.Message;
            }

            string error = null;

            if (hostError != null)
            {
                error = "Python host error: " + hostError;
                Emit(error, true, true);
            }
            else if (outcome == CadPythonOutcome.Failed)
            {
                error = LastLine(result.Trace) ?? result.Summary;
                EmitErrors(result.Trace);
                session.Write("Python error: " + result.Summary);
            }
            else if (outcome == CadPythonOutcome.Cancelled)
            {
                error = result.Summary;

                if (!run.CancelWritten)
                {
                    session.Write("*Cancel*");
                }

                Emit("KeyboardInterrupt", true, false);
            }

            CadPythonScript.SetCurrent(null);
            _current = null;
            RaiseFinished(step, document, outcome, error, path);
            return outcome;
        }

        /// <summary>
        /// Puts the host's writers back as <c>sys.stdout</c>, <c>sys.stderr</c> and <c>sys.stdin</c>: a script that left one as None, as a
        /// closed file or as a file of its own would otherwise fail every later run, or take its output away from the command line.
        /// Called with the GIL held.
        /// </summary>
        private void RestoreStreams()
        {
            using (PyObject sys = Py.Import("sys"))
            {
                sys.SetAttr("stdout", _stdout);
                sys.SetAttr("stderr", _stderr);
                sys.SetAttr("stdin", _stdin);
            }
        }

        /// <summary>The last line of a traceback, <c>ZeroDivisionError: division by zero</c>, as <see cref="CadPythonRunEventArgs.Error"/> gives it; null for none.</summary>
        private static string LastLine(string trace)
        {
            if (string.IsNullOrWhiteSpace(trace))
            {
                return null;
            }

            string[] lines = trace.Replace("\r\n", "\n").TrimEnd('\n', ' ').Split('\n');
            return lines[lines.Length - 1].Trim();
        }

        /// <summary>Asks a question of the script as the prompts of a small command, and waits on the owner for the answer.</summary>
        private void Carry(CadPythonRun run, Func<CadCommandContext, IEnumerable<CadPrompt>> body)
        {
            if (run.Cancelled)
            {
                throw new CadPythonCancelledException();
            }

            CadCommandSession session = run.Session;

            if (session.IsRunning)
            {
                throw new InvalidOperationException(Still(session));
            }

            bool done = false;

            IEnumerable<CadPrompt> Wrapped(CadCommandContext c)
            {
                foreach (CadPrompt prompt in body(c))
                {
                    yield return prompt;
                }

                done = true;
            }

            var carrier = new CadCommand(CadPythonNames.Carrier, Wrapped);

            if (!session.Start(carrier))
            {
                throw new InvalidOperationException(Still(session));
            }

            run.Computing = false;

            try
            {
                _owner.WaitUntil(() => !ReferenceEquals(session.Current, carrier));
            }
            finally
            {
                run.Computing = true;
            }

            // The owner left the wait with the question still open (the window is closing): end it.
            if (ReferenceEquals(session.Current, carrier))
            {
                session.Escape();
            }

            if (!done)
            {
                run.Cancelled = true;
                run.CancelWritten = true;
                throw new CadPythonCancelledException();
            }
        }

        private static string Still(CadCommandSession session) =>
            (session.Current?.Name ?? CadPythonNames.Carrier)
            + " is still running: a prompt can only be asked at the Command prompt.";

        internal CadInput Ask(CadPythonRun run, CadPrompt prompt)
        {
            if (prompt == null)
            {
                throw new ArgumentNullException(nameof(prompt));
            }

            CadInput input = null;

            IEnumerable<CadPrompt> Body(CadCommandContext c)
            {
                yield return prompt;
                input = c.Input;
            }

            Carry(run, Body);
            return input;
        }

        internal IReadOnlyList<string> AskSelection(CadPythonRun run)
        {
            IReadOnlyList<string> handles = Array.Empty<string>();

            IEnumerable<CadPrompt> Body(CadCommandContext c)
            {
                foreach (CadPrompt prompt in c.SelectObjects())
                {
                    yield return prompt;
                }

                // Asking is not a command that uses the selection up: what was selected before stays selected.
                c.ClearsSelection = false;
                handles = c.Selected;
            }

            Carry(run, Body);
            return handles;
        }

        /// <summary>Runs a command with its answers, as <c>command(...)</c> of the module does.</summary>
        internal void FeedCommand(CadPythonRun run, object[] answers)
        {
            if (
                answers == null
                || answers.Length == 0
                || !(answers[0] is string name)
                || string.IsNullOrWhiteSpace(name)
            )
            {
                throw new ArgumentException("command needs the name of a command first.");
            }

            CadCommandSession session = run.Session;

            if (session.IsRunning)
            {
                throw new InvalidOperationException(Still(session));
            }

            // Started as a command of the registry (the _ and . in front of a name taken off, as typing it does), but not as typed, so that
            // Enter afterwards still repeats the command the user ran, the one that started this run.
            string typed = name.Trim().TrimStart('_', '.');
            CadCommand command = session.Registry.Find(typed);

            if (command == null)
            {
                throw new ArgumentException(name.Trim() + " is not a command.");
            }

            session.Write("Command: " + typed);
            session.Start(command);
            string shown = command.Name;

            for (int i = 1; i < answers.Length; i++)
            {
                if (!session.IsRunning)
                {
                    throw new ArgumentException(
                        shown + " had ended before " + Describe(answers[i]) + " could be given."
                    );
                }

                string prompt = session.PromptText;

                if (!FeedOne(session, answers[i]))
                {
                    session.Escape();
                    throw new ArgumentException(
                        shown + " did not take " + Describe(answers[i]) + " at '" + prompt + "'."
                    );
                }
            }

            if (session.IsRunning)
            {
                string prompt = session.PromptText;
                session.Escape();
                throw new InvalidOperationException(shown + " still asked '" + prompt + "' after the last answer.");
            }
        }

        private static bool FeedOne(CadCommandSession session, object answer)
        {
            switch (answer)
            {
                case string text when text.Length == 0:
                    session.Enter();
                    return true;
                case string text:
                    return session.Feed(text);
                case Geometry.GeoPoint2 point:
                    return session.Feed(point);
                case IEnumerable<string> handles:
                    if (!session.FeedSelection(handles))
                    {
                        return false;
                    }

                    session.Enter();
                    return true;
                case IFormattable number when answer is int || answer is long || answer is double || answer is float:
                    return session.Feed(number.ToString(null, CultureInfo.InvariantCulture));
                default:
                    throw new ArgumentException(Describe(answer) + " cannot be fed to a command.");
            }
        }

        private static string Describe(object answer)
        {
            switch (answer)
            {
                case string text:
                    return text.Length == 0 ? "Enter" : "'" + text + "'";
                case Geometry.GeoPoint2 point:
                    return point.X.ToString("R", CultureInfo.InvariantCulture)
                        + ","
                        + point.Y.ToString("R", CultureInfo.InvariantCulture);
                case IEnumerable<string> handles:
                    return "[" + string.Join(", ", handles) + "]";
                case IFormattable number:
                    return number.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return answer?.ToString() ?? "null";
            }
        }
    }
}
