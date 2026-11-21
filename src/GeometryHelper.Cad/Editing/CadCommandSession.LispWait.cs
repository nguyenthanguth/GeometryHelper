using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A routine of AutoLISP that waits for the user: a get function, <c>entsel</c> or <c>ssget</c> asks, the evaluation
    /// returns to the session, and the question is asked as a prompt of a small command of its own, so that object snaps,
    /// typed points, the rubber band, transparent ZOOM and PAN and the refusal messages work as at any prompt. The answer
    /// resumes the routine, Esc gives it <c>Function cancelled</c>, and the command ends when the routine does.
    /// </summary>
    public sealed partial class CadCommandSession
    {
        // The wait now shown: what the routine asked and the prompt that asks it; null when no get function waits.
        private LispAsk _lispAsk;
        private CadPrompt _lispAskPrompt;

        // The command that carries the wait of the evaluation while a get function asks, so that a change made by the routine
        // is told from one made by a command it started; the carrier gives up its place when the routine starts a command
        // (RetireLispCarrier), and the body it gave up is told apart in Advance. The undo step the evaluation opened for itself.
        private CadCommand _lispCommand;
        private LispCarrier _lispCarrier;
        private IEnumerator<CadPrompt> _retiredBody;
        private bool _lispStep;
        private string _lispStepName;

        // The value a form typed at a prompt ended with, which answers the prompt it interrupted; whether the evaluation began
        // at a command's prompt, so that its value is to answer it.
        private object _lispValue;
        private bool _lispAnswers;

        // The wait that Esc or an error ended while its command was being taken away: the routine is told Function cancelled once
        // that command is gone (RunLispCancel), so that what it does then, a command of its own, an edit, another question,
        // starts from a session with nothing left of the wait.
        private bool _lispCancelling;
        private Action<LispResult> _lispCancelFinished;

        /// <summary>Whether the carrier of a wait has given its place to a command of the routine.</summary>
        private sealed class LispCarrier
        {
            internal bool Retired { get; set; }
        }

        /// <summary>
        /// Ends the evaluation of AutoLISP for good, as closing a document must: from now on the first question the routine asks
        /// is a hard error that neither <c>*error*</c> nor <c>vl-catch-all-apply</c> sees, so a routine that catches Esc and asks
        /// again cannot hold the document open. Esc then ends the wait and the routine unwinds; nothing happens where no
        /// evaluation goes on.
        /// </summary>
        internal void AbortLisp() => _lisp?.Abort();

        /// <summary>
        /// Closes the files a routine of this document left open, as AutoCAD does when the drawing closes, so that they can be
        /// deleted and read by others. Does nothing where no AutoLISP has run: the interpreter is not made to close nothing.
        /// </summary>
        internal void CloseLispFiles() => _lisp?.CloseFiles();

        /// <summary>Gets whether an evaluation of AutoLISP waits for the user.</summary>
        internal bool IsLispWaiting => _lisp != null && _lisp.IsWaiting;

        /// <summary>
        /// Opens the undo step of the evaluation before a change made outside any command (entmake, entmod, entdel, setvar of
        /// a drawing variable), once: named after the <c>c:</c> command, or <c>(Lisp Expression)</c> for a form typed. Inside a
        /// command the routine started, the change joins that command's step and this does nothing. The step ends with the
        /// evaluation, or where the routine starts a command.
        /// </summary>
        internal void OpenLispStep()
        {
            if (IsRunning && !ReferenceEquals(Current, _lispCommand))
            {
                return;
            }

            if (!_grouped)
            {
                Document.BeginGroup(_lispStepName ?? "(Lisp Expression)");
                _grouped = true;
                _lispStep = true;
            }
        }

        /// <summary>Closes the undo step <see cref="OpenLispStep"/> opened, so that the edits that follow are steps of their own.</summary>
        internal void CloseLispStep()
        {
            if (_lispStep && _grouped)
            {
                CloseGroup();
            }

            _lispStep = false;
            _lispStepName = null;
        }

        /// <summary>
        /// Runs what a host asks of the interpreter (APPLOAD, a startup file) and writes what it wrote; where the routine waits
        /// for the user the wait goes on as a command, and <paramref name="finished"/> is called when the routine is done.
        /// </summary>
        /// <param name="run">The call of <c>Run</c> or <c>Load</c>.</param>
        /// <param name="finished">Called with the last result of the evaluation; null for none.</param>
        /// <returns>The first result: the evaluation may not be over where it <c>IsWaiting</c>; a failed one with <c>Can't reenter LISP.</c> where an evaluation was going on and nothing was run.</returns>
        internal LispResult RunHosted(Func<LispResult> run, Action<LispResult> finished = null)
        {
            // AutoCAD does not run LISP inside LISP: while an evaluation goes on or waits, the host is told so and nothing
            // of the evaluation's state is touched, so that the answer still goes to what asked.
            if (_lisp != null && _lisp.IsEvaluating)
            {
                const string reentry = "Can't reenter LISP.";
                Write(reentry);
                Changed();
                return new LispResult(null, reentry, true, reentry);
            }

            _lispAnswers = false;
            _lispPrefix = null;
            LispResult result = run();
            LispContinue(result, finished);
            return result;
        }

        /// <summary>
        /// Goes on after the evaluation returned to the session: writes what it wrote, then, where it waits for the user, asks
        /// what it asks (a get function's prompt, as a command of its own; or the prompt of the command it started, for a
        /// <c>PAUSE</c>), and where it is over, ends its undo step and calls <paramref name="finished"/>.
        /// </summary>
        /// <param name="result">What the evaluation gave: the first, or one after an answer.</param>
        /// <param name="finished">Called with the last result of the evaluation; null for none.</param>
        private void LispContinue(LispResult result, Action<LispResult> finished)
        {
            WriteLisp(result.Output);

            if (result.IsWaiting)
            {
                if (Lisp.Waiting.Kind == LispAskKind.Pause)
                {
                    BeginPause(finished);
                }
                else
                {
                    StartLispWait(finished);
                }

                return;
            }

            _lispTyped = false;
            finished?.Invoke(result);
            CloseLispStep();
        }

        /// <summary>
        /// Starts the command that asks what the routine waits for: at the Command prompt one of its own; at the prompt of
        /// a command it suspends that command as a transparent one does, without the <c>&gt;&gt;</c> and the "Resuming" line,
        /// and answers it with the value of the form when the routine is done.
        /// </summary>
        private void StartLispWait(Action<LispResult> finished)
        {
            var carrier = new LispCarrier();
            var command = new CadCommand("LISP", c => LispWait(c, finished, carrier));
            _lispCommand = command;
            _lispCarrier = carrier;

            if (Current == null)
            {
                Run(command, Array.Empty<string>());
                return;
            }

            _suspended.Push(new Suspended(this) { ForLisp = true });
            Clear();
            Current = command;
            _context = new CadCommandContext(this, command, Array.Empty<string>());

            try
            {
                _body = command.Body(_context).GetEnumerator();
            }
            catch (Exception exception)
            {
                Fail(exception);
                return;
            }

            Advance();
        }

        /// <summary>
        /// The prompts of an evaluation that waits: each question the routine asks is a prompt, its answer resumes the routine,
        /// and the body ends when the routine does. Ending early (Esc, an error) gives the routine <c>Function cancelled</c> until
        /// it ends.
        /// </summary>
        private IEnumerable<CadPrompt> LispWait(CadCommandContext c, Action<LispResult> finished, LispCarrier carrier)
        {
            LispResult result;

            // A get function leaves LASTPOINT as it was, base point and answer alike; the base point stands in for it only while
            // the prompt is up, so that @ measures from it (q3, q10).
            GeoPoint2 lastPoint = LastPoint;
            bool ended = false;

            try
            {
                do
                {
                    LispAsk ask = Lisp.Waiting;
                    object answer = null;
                    bool first = true;
                    lastPoint = LastPoint;

                    while (ask.Kind != LispAskKind.Pause)
                    {
                        CadPrompt prompt = LispPrompt(ask, first);
                        first = false;
                        _lispAsk = ask;
                        _lispAskPrompt = prompt;
                        yield return prompt;
                        _lispAsk = null;
                        _lispAskPrompt = null;

                        if (TakeLispAnswer(ask, prompt, c.Input, out answer))
                        {
                            break;
                        }
                    }

                    LastPoint = lastPoint;
                    result = Lisp.Resume(answer);

                    // The routine ran a command meanwhile, which took this command's place: the rest of the evaluation goes on
                    // in the session, not in this body.
                    if (carrier.Retired)
                    {
                        LispContinue(result, finished);
                        yield break;
                    }

                    WriteLisp(result.Output);
                } while (result.IsWaiting);

                ended = true;
                _lispValue = !_lispAnswers || result.Failed ? null : result.Value;
                finished?.Invoke(result);
            }
            finally
            {
                _lispAsk = null;
                _lispAskPrompt = null;

                if (!carrier.Retired)
                {
                    // Esc or an error ended the wait with a base point still the last point.
                    if (!ended)
                    {
                        LastPoint = lastPoint;
                    }

                    // The wait ended before the routine did: it is told Function cancelled by RunLispCancel, once this command is
                    // gone, and asks again as often as it likes, each question a wait of its own. The undo step ends here and
                    // is opened again, under the same name, by what the routine changes after that.
                    string stepName = _lispStepName;
                    CloseLispStep();

                    if (Lisp.IsWaiting)
                    {
                        _lispStepName = stepName;
                        _lispCancelling = true;
                        _lispCancelFinished = finished;
                    }
                }
            }
        }

        /// <summary>
        /// Tells the routine whose wait ended <c>Function cancelled</c>, now that the command that carried the wait is gone: the
        /// routine goes on from there, and what it asks next is a wait of its own, what it starts a command of its own. Does
        /// nothing where no wait was cancelled.
        /// </summary>
        private void RunLispCancel()
        {
            if (!_lispCancelling)
            {
                return;
            }

            Action<LispResult> finished = _lispCancelFinished;
            _lispCancelling = false;
            _lispCancelFinished = null;
            _lispCommand = null;
            _lispCarrier = null;
            LispResult result = Lisp.CancelWait();

            // A routine the cancel ended is not told so again: the command line keeps what Esc made of the command.
            LispContinue(result, result.IsWaiting ? finished : null);
        }

        /// <summary>The prompt a question of a routine is asked with: its message as it wrote it, the keywords of <c>initget</c>, the base point.</summary>
        private CadPrompt LispPrompt(LispAsk ask, bool first)
        {
            // The message up to its last new line is written as lines, the rest is the prompt.
            string message = ask.Message ?? string.Empty;
            int lineEnd = message.LastIndexOf('\n');

            if (first && lineEnd > 0)
            {
                foreach (string line in message.Substring(0, lineEnd).Split('\n'))
                {
                    Write(line);
                }
            }

            string text = message.Substring(lineEnd + 1);

            // entsel and nentsel with no message ask AutoCAD's own.
            if (ask.Message == null && (ask.Kind == LispAskKind.Entity || ask.Kind == LispAskKind.NestedEntity))
            {
                text = "Select object: ";
            }

            IReadOnlyList<string> keywords = ask.Keywords;
            bool anyText = (ask.Flags & 128) != 0;
            GeoPoint2? basePoint = null;

            if (ask.BasePoint.HasValue)
            {
                GeoPoint2 world = Ucs.ToWorld(new GeoPoint2(ask.BasePoint.Value.X, ask.BasePoint.Value.Y));
                basePoint = world;

                // A point typed as @dx,dy is measured from the base point given.
                LastPoint = world;
            }

            switch (ask.Kind)
            {
                case LispAskKind.Point:
                case LispAskKind.Corner:
                    return CadPrompt.ForPoint(text, basePoint, keywords, null, anyText).AsRaw();
                case LispAskKind.Distance:
                    CadPrompt distance = CadPrompt.ForDistance(text, basePoint, keywords).AsRaw();
                    return anyText ? distance.AcceptingText() : distance;
                case LispAskKind.Angle:
                case LispAskKind.Orient:
                    CadPrompt angle = CadPrompt.ForAngle(text, basePoint, keywords).AsRaw();
                    angle = ask.Kind == LispAskKind.Orient ? angle.Plain() : angle;
                    return anyText ? angle.AcceptingText() : angle;
                case LispAskKind.Integer:
                case LispAskKind.Real:
                    return CadPrompt.ForString(text).AsRaw(endsAtSpace: true);
                case LispAskKind.String:
                    return CadPrompt.ForString(text).AsRaw(endsAtSpace: !ask.Spaces);
                case LispAskKind.Line:
                    // read-line and read-char show no prompt: the line typed is echoed alone (AutoCAD 2021, r7), spaces and all.
                    return CadPrompt.ForString(string.Empty).AsRaw();
                case LispAskKind.Keyword:
                    CadPrompt keyword = CadPrompt.ForKeyword(text, keywords).AsRaw();
                    return anyText ? keyword.AcceptingText() : keyword;
                case LispAskKind.Entity:
                case LispAskKind.NestedEntity:
                    return CadPrompt.ForEntity(text, keywords).Expecting("Last").AsRaw();
                default:
                    CadPrompt selection = CadPrompt.ForSelection();

                    if (keywords.Count > 0)
                    {
                        selection = selection.Hiding(new List<string>(keywords).ToArray());
                    }

                    return selection;
            }
        }

        /// <summary>
        /// Turns the answer a prompt got into the value the routine is resumed with, or writes the refusal and says the prompt
        /// is to be asked again: a point as a list of three reals in the UCS, a distance or an angle as a real, an integer or a
        /// real, the text, the keyword as <c>initget</c> wrote it, the object as <c>(ename (x y z))</c>, the handles of a set.
        /// </summary>
        private bool TakeLispAnswer(LispAsk ask, CadPrompt prompt, CadInput input, out object answer)
        {
            answer = null;
            int flags = ask.Flags;

            // A keyword of initget is the answer whatever kind asked.
            if (input.Kind == CadInputKind.Keyword)
            {
                answer = input.Text;
                return true;
            }

            switch (ask.Kind)
            {
                case LispAskKind.Point:
                case LispAskKind.Corner:
                    if (input.IsPoint)
                    {
                        answer = LispPointOf(ask, input);
                        return true;
                    }

                    if (input.IsNone && (flags & 1) != 0)
                    {
                        return Refuse("Point or option keyword required.");
                    }

                    answer = input.Kind == CadInputKind.Text ? input.Text : null;
                    return true;
                case LispAskKind.Distance:
                case LispAskKind.Angle:
                case LispAskKind.Orient:
                    if (input.Kind == CadInputKind.Number)
                    {
                        double value = ask.Kind == LispAskKind.Angle ? CountAngle(input.Number) : input.Number;
                        answer = value;
                        return !NumberRefused(flags, value);
                    }

                    if (input.IsNone && (flags & 1) != 0)
                    {
                        return Refuse(
                            ask.Kind == LispAskKind.Distance
                                ? Refused(prompt)
                                : "Requires valid numeric angle or two points."
                        );
                    }

                    answer = input.Kind == CadInputKind.Text ? input.Text : null;
                    return true;
                case LispAskKind.Integer:
                case LispAskKind.Real:
                    return TakeLispNumber(ask, prompt, input, out answer);
                case LispAskKind.String:
                case LispAskKind.Line:
                    answer = input.Kind == CadInputKind.Text ? input.Text : string.Empty;
                    return true;
                case LispAskKind.Keyword:
                    if (input.IsNone && (flags & 1) != 0)
                    {
                        return Refuse("Invalid option keyword.");
                    }

                    answer = input.Kind == CadInputKind.Text ? input.Text : null;
                    return true;
                case LispAskKind.Entity:
                case LispAskKind.NestedEntity:
                    answer = input.Handles.Count == 0 ? null : LispEntityOf(input);
                    return true;
                default:
                    answer = new List<string>(
                        input.PickOrder.Count == input.Handles.Count ? input.PickOrder : input.Handles
                    );
                    return true;
            }
        }

        /// <summary>
        /// The handles in the order of their numbers, the newest first, ties by the text: the order AutoCAD counts the set of ALL in
        /// (runs/hhh and runs/nnn: a routine that walks the set meets the line made last before the text made first).
        /// </summary>
        private static List<string> NewestFirst(IEnumerable<string> handles)
        {
            var sorted = new List<string>(handles);
            sorted.Sort(
                (a, b) =>
                {
                    ulong.TryParse(a, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong x);
                    ulong.TryParse(b, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong y);
                    int order = y.CompareTo(x);
                    return order != 0 ? order : string.CompareOrdinal(a, b);
                }
            );
            return sorted;
        }

        /// <summary>getint and getreal: the text typed read as a number, with the refusals of AutoCAD 2021 for text, for an integer out of range and for the bits of <c>initget</c>.</summary>
        private bool TakeLispNumber(LispAsk ask, CadPrompt prompt, CadInput input, out object answer)
        {
            answer = null;
            string typed = (input.Text ?? string.Empty).Trim();
            bool integer = ask.Kind == LispAskKind.Integer;

            if (input.IsNone || typed.Length == 0)
            {
                return (ask.Flags & 1) == 0
                    || Refuse(integer ? "Requires an integer value." : "Requires numeric value.");
            }

            string keyword = prompt.MatchKeyword(typed);

            if (keyword != null)
            {
                answer = keyword;
                return true;
            }

            if (integer)
            {
                if (!int.TryParse(typed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int whole))
                {
                    return AnyTextOr(ask, typed, "Requires an integer value.", out answer);
                }

                if (whole < -32768 || whole > 32767)
                {
                    return Refuse("Requires an integer between -32768 and 32767.");
                }

                answer = whole;
                return !NumberRefused(ask.Flags, whole);
            }

            if (
                !double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double real)
                || double.IsNaN(real)
                || double.IsInfinity(real)
            )
            {
                return AnyTextOr(ask, typed, "Requires numeric value.", out answer);
            }

            answer = real;
            return !NumberRefused(ask.Flags, real);
        }

        /// <summary>With <c>(initget 128)</c> the text that is no number is the answer; else the refusal is written.</summary>
        private bool AnyTextOr(LispAsk ask, string typed, string refusal, out object answer)
        {
            answer = null;

            if ((ask.Flags & 128) != 0)
            {
                answer = typed;
                return true;
            }

            return Refuse(refusal);
        }

        /// <summary>Writes a refusal and tells the caller the prompt is asked again.</summary>
        private bool Refuse(string message)
        {
            Write(message);
            Changed();
            return false;
        }

        /// <summary>Whether the bits of <c>initget</c> refuse a number: 2 a zero, 4 a negative, both either; writes the words.</summary>
        private bool NumberRefused(int flags, double value)
        {
            // A zero typed is exactly zero; the bits ask for nothing finer.
            bool zero = (flags & 2) != 0 && Math.Abs(value) < 1E-12;
            bool negative = (flags & 4) != 0 && value < 0.0;
            string message = null;

            if ((flags & 6) == 6)
            {
                message = zero || negative ? "Value must be positive and nonzero." : null;
            }
            else if (zero)
            {
                message = "Value must be nonzero.";
            }
            else if (negative)
            {
                message = "Value must be positive.";
            }

            if (message == null)
            {
                return false;
            }

            Refuse(message);
            return true;
        }

        /// <summary>The point a prompt was answered with, in the UCS, as a list of three reals; a third coordinate typed is kept.</summary>
        private LispCons LispPointOf(LispAsk ask, CadInput input)
        {
            GeoPoint2 inUcs = Ucs.ToUcs(input.Point);
            double z = 0.0;
            string typed = input.Text;

            if (typed != null && typed.IndexOf('<') < 0)
            {
                string[] parts = typed.Split(',');

                if (parts.Length == 3 && TryLength(parts[2], out double height))
                {
                    bool relative = parts[0].TrimStart().StartsWith("@", StringComparison.Ordinal);
                    z = height + (relative && ask.BasePoint.HasValue ? ask.BasePoint.Value.Z : 0.0);
                }
            }

            return (LispCons)LispInterpreter.ListOf(new object[] { inUcs.X, inUcs.Y, z });
        }

        /// <summary>What entsel gives for the object picked: <c>(ename (x y z))</c>, the point the object was picked at in the UCS.</summary>
        private object LispEntityOf(CadInput input)
        {
            string handle = input.Handles[0];

            if (Document.FindEntity(handle) == null)
            {
                return null;
            }

            GeoPoint2 at = input.IsPickedAt ? Ucs.ToUcs(input.Point) : GeoPoint2.Origin;
            object point = LispInterpreter.ListOf(new object[] { at.X, at.Y, 0.0 });
            return LispInterpreter.ListOf(new[] { new LispEntityName(handle), point });
        }

        /// <summary>entsel's L: the object made last, or nil where there is none.</summary>
        private bool AnswerLispLast(string echo)
        {
            Echo(echo);
            string last = LastVisible();

            return Answer(
                last == null
                    ? CadInput.Nothing
                    : new CadInput(CadInputKind.Selection, default, 0.0, null, new[] { last }),
                null,
                null
            );
        }

        /// <summary>
        /// Takes text typed while a routine waits: what starts with <c>(</c> or <c>!</c> is not AutoLISP then, as AutoCAD's
        /// "Can't reenter LISP." says, and goes to the prompt as typed; at <c>getstring</c> without its first argument the
        /// space ends the text and the rest of the line is the next input.
        /// </summary>
        /// <param name="text">The text typed.</param>
        /// <param name="taken">Whether the text was taken here, and what taking it gave.</param>
        /// <returns>True when this dealt with the text.</returns>
        private bool FeedLispAsk(string text, out bool taken)
        {
            taken = false;

            // read-line and read-char take the line as typed: a parenthesis starts no refusal and a space ends nothing.
            if (_lispAsk == null || Prompt == null || _lispAsk.Kind == LispAskKind.Line)
            {
                return false;
            }

            string start = text.TrimStart();

            if (start.StartsWith("(", StringComparison.Ordinal) || start.StartsWith("!", StringComparison.Ordinal))
            {
                Write(Prompt.Echo(Prompt.Kind == CadPromptKind.String ? text : text.Trim()));
                Write("Can't reenter LISP.");
                taken = Refeed(text);
                return true;
            }

            int space = text.IndexOf(' ');

            if (space < 0 || !ReferenceEquals(Prompt, _lispAskPrompt) || Prompt.TakesSpaces)
            {
                return false;
            }

            // The space typed is the end of the answer and leaves the line open: what the routine writes next (its value) goes on
            // the echo line, as AutoCAD writes "Str: hello "hello"".
            string rest = text.Substring(space + 1);
            string answered = text.Substring(0, space);
            string echo = Prompt.Echo(answered);
            int line = _history.Count;
            taken = Feed(answered);

            if (_history.Count > line + 1 && _history[line] == echo)
            {
                _history[line] += " " + _history[line + 1];
                _history.RemoveAt(line + 1);
            }

            if (!string.IsNullOrWhiteSpace(rest))
            {
                taken = Feed(rest);
            }

            return true;
        }
    }
}
