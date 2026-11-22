using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Cad.Lisp;

namespace GeometryHelper.Cad
{
    /// <summary>What a routine's <c>command</c> got of one of its arguments.</summary>
    internal enum LispFeed
    {
        /// <summary>The argument was taken: it started a command, answered a prompt, or had nothing to do.</summary>
        Done,

        /// <summary>The prompt refused the argument and wrote its words; the command is left at that prompt.</summary>
        Refused,

        /// <summary>The argument is no value <c>command</c> takes: a symbol, a function, a list that is no point.</summary>
        Bad,
    }

    /// <summary>
    /// What AutoLISP's <c>command</c> and <c>vl-cmdf</c> do in the session: each argument goes, in turn, to the command running
    /// or starts one, as typed (never as AutoLISP), the lines the command line would show are written when CMDECHO is 1, and
    /// a <c>PAUSE</c> lets the user answer the prompt the command asks while the routine waits.
    /// </summary>
    public sealed partial class CadCommandSession
    {
        // An argument of command is being fed: it is text to the prompt, not AutoLISP.
        private bool _lispFeeding;

        // What the last line of the history holds that the routine's own text goes on from: the Command prompt after a command
        // of the routine ended ("Command: done7"), or the prompt of the command it left running ("Specify next point or
        // [Undo]: nil"); null for none.
        private string _lispPrefix;

        // A form was typed at the prompt of a command, and its text is the echo of that prompt, so that the first argument of
        // command that answers it does not write the prompt again.
        private bool _lispTyped;

        // The command a routine started with command, whose end gives the Command prompt to the routine's next text.
        private CadCommand _lispStarted;

        // The PAUSE the routine waits at: the command whose prompt the user answers, and what to call when the routine is done.
        private bool _lispPause;
        private bool _pauseUntilEnd;
        private CadCommand _pauseCommand;
        private Action<LispResult> _pauseFinished;

        /// <summary>Whether CMDECHO is on, as it is until a routine or the user sets it to 0.</summary>
        private bool CommandEchoOn => !(Remembered.TryGetValue("cmdecho", out double echo) && echo == 0.0);

        /// <summary>
        /// Whether the command running is one a routine started with <c>command</c>: TEXT takes the one line it is given and
        /// COPY makes one copy, and each ends then (AutoCAD 2021, q2: <c>(command "TEXT" '(0 0) 2.5 0 "Hello")</c> ends the
        /// command; runs/hhh: COPY does not wait for a second copy), as the in-place editor that would take a second line is
        /// not there.
        /// </summary>
        internal bool RunsForLisp => _lispStarted != null && ReferenceEquals(Current, _lispStarted);

        /// <summary>
        /// Whether the prompts of a routine's <c>command</c> are muted: NOMUTT is 1 and CMDECHO is on. AutoCAD 2021 (probes g10 and
        /// g10b) then writes one line for the call, the name of the command and each argument typed with a space after it, no
        /// prompts, and the value of the routine's form follows on the same line (<c>_.CIRCLE 0,0 5 nil</c>).
        /// </summary>
        private bool PromptsMuted => CommandEchoOn && Remembered.TryGetValue("nomutt", out double mute) && mute == 1.0;

        /// <summary>Whether a command of a routine runs, as <c>command</c> feeds it: one with a prompt up that is not the wait of a get function.</summary>
        private bool HasLispCommand => Current != null && Prompt != null && !ReferenceEquals(Current, _lispCommand);

        /// <summary>
        /// Gives one argument of <c>command</c> to the command running, or starts the command it names; called on the owner
        /// thread. A string is typed text, a number its text, a list of two or three numbers a point in the UCS, an entity name
        /// or a selection set the objects at "Select objects:" (the first object at a prompt for one), nil cancels the command
        /// running with no <c>*Cancel*</c> line. With no command running a string starts one by name, alias, or system variable
        /// (a name <c>c:</c> made is not found), and <c>""</c> runs the last again; a name no command has writes
        /// <c>Unknown command</c> and the arguments go on. With CMDECHO 0 nothing is written but the words of a refusal.
        /// </summary>
        /// <param name="argument">The argument, as the interpreter holds it.</param>
        /// <returns>What came of it.</returns>
        internal LispFeed FeedLispArgument(object argument)
        {
            if (
                !(
                    argument == null
                    || argument is string
                    || argument is int
                    || argument is double
                    || argument is LispEntityName
                    || argument is LispSelectionSet
                    || (argument is LispCons && LispDrawing.PointOf(argument) != null)
                )
            )
            {
                return LispFeed.Bad;
            }

            // Muted, the text the routine left on its last line is where the line of the call goes on from.
            bool muted = PromptsMuted;
            string tail = null;

            if (muted)
            {
                tail = TakeLispTail();
            }
            else
            {
                WriteLispBefore();
            }

            RetireLispCarrier();
            int mark = _history.Count;
            bool keepLast = false;
            LispFeed fed;

            if (muted)
            {
                _lispPrefix = tail;
            }

            if (HasLispCommand)
            {
                fed = FeedAtPrompt(argument);
                keepLast = fed == LispFeed.Refused;
            }
            else
            {
                fed = FeedAtCommandPrompt(argument, out keepLast);
            }

            if (!CommandEchoOn || muted)
            {
                // The words of a refusal and of an unknown name are the one line kept.
                if (_history.Count > mark)
                {
                    string last = _history[_history.Count - 1];
                    _history.RemoveRange(mark, _history.Count - mark);

                    // A refused argument ends the line of the call, and its words come under it.
                    if (muted && fed == LispFeed.Refused)
                    {
                        _history.Add(tail + MutedEcho(argument, false));
                    }

                    if (keepLast)
                    {
                        _history.Add(last);
                    }
                }
            }

            if (muted)
            {
                _lispPrefix = fed == LispFeed.Refused || keepLast ? null : tail + MutedEcho(argument, true);
            }
            else if (fed == LispFeed.Refused)
            {
                _lispPrefix = null;
            }

            Changed();
            return fed;
        }

        /// <summary>
        /// What the muted call of <c>command</c> writes for an argument: a string or a number as it is, then a space; nothing for
        /// an empty string, a point or nil; two spaces for an entity name or a selection set (AutoCAD 2021, probe g10b).
        /// </summary>
        private static string MutedEcho(object argument, bool space)
        {
            switch (argument)
            {
                case string text:
                    return text.Length == 0 ? string.Empty : text + (space ? " " : string.Empty);
                case int _:
                case double _:
                    return LispNumber(argument) + (space ? " " : string.Empty);
                case LispEntityName _:
                case LispSelectionSet _:
                    return "  ";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Writes the lines the routine finished and gives the text it left on its last line, with what the session held for
        /// that line; the muted call of <c>command</c> goes on from it.
        /// </summary>
        private string TakeLispTail()
        {
            string text = (_lispPrefix ?? string.Empty) + Lisp.TakeOutput();
            _lispPrefix = null;
            int cut = text.LastIndexOf('\n');

            if (cut < 0)
            {
                return text;
            }

            foreach (string line in text.Substring(0, cut).Split('\n'))
            {
                Write(line);
            }

            return text.Substring(cut + 1);
        }

        /// <summary>Writes what the routine wrote before this call of <c>command</c>, so that it comes before the lines the call makes; the text left on the last line stays where the routine wrote nothing.</summary>
        private void WriteLispBefore()
        {
            string written = Lisp.TakeOutput();

            if (written.Length > 0)
            {
                WriteLisp(written);
            }
        }

        /// <summary>
        /// <c>(alert text)</c>: asks the window for the message box; where nobody shows it (a session with no window) the lines of
        /// the text are written on lines of their own, after what the routine wrote before, and the Command prompt that waits to
        /// open the routine's next text stays for it (AutoCAD 2021, runs/tca: <c>Xong</c>, then <c>Command: nil</c>).
        /// </summary>
        /// <param name="text">The text of the message.</param>
        internal void ShowAlert(string text)
        {
            if (Request("ALERT", null, text))
            {
                return;
            }

            WriteLispBefore();
            string open = _lispPrefix;
            _lispPrefix = null;

            foreach (string line in text.Split('\n'))
            {
                Write(line);
            }

            _lispPrefix = open;
        }

        /// <summary>An argument for the prompt of the command running.</summary>
        private LispFeed FeedAtPrompt(object argument)
        {
            CadPrompt asked = Prompt;
            string promptText = asked.Text;
            int mark = _history.Count;
            bool typed = _lispTyped;
            bool took;
            _lispTyped = false;

            _lispFeeding = true;

            try
            {
                switch (argument)
                {
                    case null:
                        // nil cancels the command, with no *Cancel* line.
                        Finish(CadCommandOutcome.Cancelled);
                        return LispFeed.Done;
                    case LispEntityName name:
                        took = FeedSelection(new[] { name.Handle });
                        break;
                    case LispSelectionSet set:
                        took = FeedSelection(LiveHandles(set));
                        break;
                    case LispCons _:
                        // A point answers its prompt with no words after it; the point is typed, in the UCS, but not written.
                        Write(promptText);
                        took = Refeed(LispAnswer(argument));
                        break;
                    case string text:
                        took = Feed(text);
                        break;
                    default:
                        took = Feed(LispNumber(argument));
                        break;
                }
            }
            finally
            {
                _lispFeeding = false;
            }

            // The form typed at the prompt wrote that prompt already, as the first line of what the argument answers.
            if (typed && _history.Count > mark && _history[mark] == promptText)
            {
                _history.RemoveAt(mark);
            }

            // "0 found" is the answer of "Select objects:", not a refusal; the prompt is asked again with nothing wrong.
            bool nothingFound =
                asked.Kind == CadPromptKind.Selection
                && _history.Count > mark
                && _history[_history.Count - 1] == "0 found";
            return !took && ReferenceEquals(Prompt, asked) && !nothingFound ? LispFeed.Refused : LispFeed.Done;
        }

        /// <summary>An argument with no command running: a string starts one, <c>""</c> runs the last again, the rest does nothing.</summary>
        private LispFeed FeedAtCommandPrompt(object argument, out bool unknown)
        {
            unknown = false;
            string name;

            switch (argument)
            {
                case string text:
                    name = text;
                    break;
                case int _:
                case double _:
                    name = LispNumber(argument);
                    break;
                default:
                    return LispFeed.Done;
            }

            _lispTyped = false;

            if (name.Trim().Length > 0)
            {
                StartByLisp(name, false, out unknown);
            }
            else if (LastCommandName != null)
            {
                StartByLisp(LastCommandName, true, out unknown);
            }

            return LispFeed.Done;
        }

        /// <summary>
        /// The command a name a routine gave stands for, as <see cref="StartByLisp"/> finds it: the <c>_</c> and <c>.</c> in front
        /// of a name, which AutoCAD takes off to find the command (<c>_.LINE</c> is LINE), and an apostrophe, then the commands,
        /// their aliases and the system variables, which a name alone sets.
        /// </summary>
        /// <param name="given">The name as the routine gave it, trimmed.</param>
        /// <returns>The command; null where there is none.</returns>
        private CadCommand LispCommandNamed(string given)
        {
            string name = WithoutPrefixes(given);
            name = WithoutApostrophe(name) ?? name;
            name = WithoutPrefixes(name);
            CadCommand command = Registry.FindByName(name) ?? Registry.Find(name);

            if (command == null && Registry.FindVariable(name) is CadSystemVariable variable)
            {
                command = new CadCommand(variable.Name, c => CadCommands.TypedVariable(c, variable));
            }

            return command;
        }

        /// <summary>
        /// Starts the command a routine's <c>command</c> names, as typed at the Command prompt but that a <c>c:</c> command is
        /// not found and that a command with a command-line twin runs the twin (LAYER runs -LAYER's prompts). The name is
        /// written as given, after the Command prompt the routine's last command left. It is not the command Enter runs again.
        /// </summary>
        /// <param name="given">The name as the routine gave it: <c>layer</c>, <c>_.rectangle</c>, <c>.-dimstyle</c>.</param>
        /// <param name="repeated">Whether Enter, a <c>""</c>, repeated the last command: its name is then a line of its own.</param>
        /// <param name="unknown">Whether nothing was started: no command has the name, or the Block Editor or the edit of a reference refuses it; the last line written is its words.</param>
        private void StartByLisp(string given, bool repeated, out bool unknown)
        {
            unknown = false;
            string open = _lispPrefix ?? string.Empty;
            _lispPrefix = null;
            given = given.Trim();

            CadCommand command = LispCommandNamed(given);

            if (command == null)
            {
                string message = string.Format(CultureInfo.InvariantCulture, Unknown, given.ToUpperInvariant());

                if (repeated)
                {
                    Write(open + given);
                    Write(message);
                }
                else
                {
                    Write(open + given + " " + message);
                }

                unknown = true;
                _lispPrefix = CommandEchoOn ? "Command: " : null;
                return;
            }

            command = Registry.FindTwin(command) ?? command;
            Write(open + given);

            if (Document.BlockEditor != null && CadCommands.LeavesBlockEditor(command.Name))
            {
                Write(CadBlockEditor.NotAllowed);
                unknown = true;
                _lispPrefix = CommandEchoOn ? "Command: " : null;
                return;
            }

            if (Document.RefEdit != null && CadCommands.LeavesRefEdit(command.Name))
            {
                Write(Document.RefEdit.NotAllowedFor(command.Name));
                unknown = true;
                _lispPrefix = CommandEchoOn ? "Command: " : null;
                return;
            }

            // A command of its own is a step of its own: the changes the routine made outside a command are closed first.
            CloseLispStep();
            _lispStarted = command;
            Run(command, PreSelectedFor(command));
        }

        /// <summary>The handles of a selection set whose objects are still in the drawing.</summary>
        private List<string> LiveHandles(LispSelectionSet set)
        {
            var live = new List<string>();

            foreach (string handle in set.Handles)
            {
                if (Document.FindEntity(handle) != null)
                {
                    live.Add(handle);
                }
            }

            return live;
        }

        /// <summary>
        /// <c>command-s</c>: the first argument names the command, each of the others goes to its prompts one after the other as
        /// <c>command</c> feeds them, but a prompt that refuses one writes its words and takes the next argument at the same
        /// prompt, and what is left when the command ends is dropped (AutoCAD 2021, probes g6 to g6d). Called on the owner thread.
        /// </summary>
        /// <param name="arguments">The arguments, at least one, none of them nil.</param>
        /// <returns>True where the command is left at a prompt and the routine waits until it ends.</returns>
        /// <exception cref="LispException">Thrown with <c>Unknown (command-s) failure.</c> where the first argument is no command name.</exception>
        internal bool FeedLispStack(object[] arguments)
        {
            if (
                !(arguments[0] is string first)
                || first.Trim().Length == 0
                || (!HasLispCommand && LispCommandNamed(first.Trim()) == null)
            )
            {
                throw new LispException("Unknown (command-s) failure.");
            }

            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0 && !HasLispCommand)
                {
                    break;
                }

                // PAUSE has nothing to wait at in the middle of the call; the routine waits at its end.
                if (!(arguments[i] is string text && text == "\\"))
                {
                    FeedLispArgument(arguments[i]);
                }
            }

            // The prompt the command waits at is shown whatever CMDECHO says (AutoCAD 2021, probe g6).
            if (HasLispCommand && !CommandEchoOn)
            {
                Write(Prompt.Text);
            }

            return HasLispCommand;
        }

        /// <summary>
        /// The end of a call of <c>command-s</c>: the value the routine's form gives follows the last line of the command on that
        /// line (<c>Specify radius of circle or [Diameter]: 5 nil</c>), where <c>command</c> starts a Command prompt for it; after
        /// Esc it is a line of its own.
        /// </summary>
        internal void EndLispStack()
        {
            if (HasLispCommand || PromptsMuted || !CommandEchoOn)
            {
                return;
            }

            if (
                LastOutcome == CadCommandOutcome.Cancelled
                || _history.Count == 0
                || _history[_history.Count - 1].Length == 0
            )
            {
                _lispPrefix = null;
                return;
            }

            string last = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            _lispPrefix = last + " ";
            Changed();
        }

        /// <summary><c>(command)</c> with no argument: cancels the command running, with no <c>*Cancel*</c> line.</summary>
        internal void CancelLispCommand()
        {
            WriteLispBefore();
            RetireLispCarrier();

            if (HasLispCommand)
            {
                _lispFeeding = true;

                try
                {
                    Finish(CadCommandOutcome.Cancelled);
                }
                finally
                {
                    _lispFeeding = false;
                }
            }

            Changed();
        }

        /// <summary>Whether a <c>PAUSE</c> has a command to wait at: one is running, and it is not the wait of a get function.</summary>
        internal bool CanLispPause()
        {
            RetireLispCarrier();

            // A muted call has written no line yet: the pause shows its prompt under what the call wrote so far.
            if (PromptsMuted)
            {
                string tail = TakeLispTail();

                if (tail.Length > 0)
                {
                    Write(tail);
                }
            }

            return HasLispCommand;
        }

        /// <summary>
        /// The end of a call of <c>command</c> that left a command running: the prompt it asks is the line the routine's next
        /// text goes on from, as <c>Specify next point or [Undo]: nil</c> shows; nothing with CMDECHO 0.
        /// </summary>
        internal void EndLispCommand()
        {
            if (HasLispCommand && CommandEchoOn && !PromptsMuted)
            {
                _lispPrefix = Prompt.Text + " ";
            }
        }

        /// <summary>
        /// A routine's <c>command</c> starts a command while a get function of the routine waits: the wait gives up its place
        /// (the command that interrupted the routine's wait, where a form was typed at one, is back), and the body of the wait
        /// ends when the routine next asks the session, which goes on with what it asks.
        /// </summary>
        private void RetireLispCarrier()
        {
            if (_lispCommand == null || !ReferenceEquals(Current, _lispCommand))
            {
                return;
            }

            _lispCarrier.Retired = true;
            _retiredBody = _body;
            _lispAsk = null;
            _lispAskPrompt = null;
            _lispCommand = null;
            _lispCarrier = null;

            if (_suspended.Count > 0 && _suspended.Peek().ForLisp)
            {
                _suspended.Pop().Restore(this);
                return;
            }

            Clear();
            _body = null;
            _context = null;
            Current = null;
            Prompt = null;
        }

        /// <summary>
        /// The routine's <c>PAUSE</c>: the command it started now waits for the user at the prompt it shows; the routine goes on
        /// when the command asks its next prompt or ends, and is told <c>Function cancelled</c> if the user presses Esc. With
        /// CMDECHO 0 the prompt is not written, so the answer is echoed alone.
        /// </summary>
        private void BeginPause(Action<LispResult> finished)
        {
            _lispPrefix = null;

            if (!HasLispCommand)
            {
                LispContinue(Lisp.Resume(null), finished);
                return;
            }

            _lispPause = true;
            _pauseUntilEnd = Lisp.Waiting.UntilEnd;
            _pauseCommand = Current;
            _pauseFinished = finished;

            if (!CommandEchoOn)
            {
                Prompt.WithoutWords();
            }

            Changed();
        }

        /// <summary>The command a <c>PAUSE</c> waits at asked its next prompt or ended: the routine goes on, or is cancelled where Esc ended the command.</summary>
        private void ResumePause(CadCommandOutcome outcome)
        {
            Action<LispResult> finished = _pauseFinished;
            bool untilEnd = _pauseUntilEnd;
            _lispPause = false;
            _pauseUntilEnd = false;
            _pauseCommand = null;
            _pauseFinished = null;

            // command-s that waited for the end of the command is over with it, Esc or not, and gives nil.
            LispResult result =
                outcome == CadCommandOutcome.Cancelled && !untilEnd ? Lisp.CancelWait() : Lisp.Resume(null);
            LispContinue(result, finished);
        }
    }
}
