using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The hand-off between the thread that calls the interpreter (the owner: the UI thread in the window, the test thread
    /// in a test) and the worker thread that evaluates. Exactly one of the two runs at any time, so the code that touches the
    /// document needs no locks: the worker asks the owner to do that work (<see cref="OnOwner"/>) or to wait for the user
    /// (<see cref="Ask"/>), and the owner, waiting in <see cref="Pump"/> instead of <c>Join</c>, does it and gives the turn
    /// back. An evaluation that waits for the user returns to its caller with <see cref="LispResult.IsWaiting"/> and is
    /// answered later by <see cref="Resume"/> or ended by <see cref="CancelWait"/>.
    /// </summary>
    public sealed partial class LispInterpreter
    {
        private enum Turn
        {
            Worker,
            OwnerWork,
            Asking,
            Done,
        }

        private readonly SemaphoreSlim _ownerTurn = new SemaphoreSlim(0);
        private readonly SemaphoreSlim _workerTurn = new SemaphoreSlim(0);
        private Thread _worker;
        private Turn _turn;
        private Func<LispResult> _body;
        private LispResult _finished;
        private Exception _workerFailure;
        private Func<object> _ownerWork;
        private object _ownerResult;
        private Exception _ownerFailure;
        private LispAsk _waiting;
        private object _answer;
        private bool _cancelled;
        private bool _aborting;
        private int _chunkStart;
        private int _initgetBits;
        private IReadOnlyList<string> _initgetKeywords = Array.Empty<string>();

        /// <summary>Gets whether an evaluation is waiting for the user: a get function, <c>entsel</c> or <c>ssget</c> asked and nobody has answered.</summary>
        internal bool IsWaiting => _waiting != null;

        /// <summary>Gets what the evaluation waits for; null when it does not wait.</summary>
        internal LispAsk Waiting => _waiting;

        /// <summary>
        /// Gets whether an evaluation is going on, running or waiting. A host that calls <see cref="Run"/>, <see cref="RunCommand"/>
        /// or <see cref="Load"/> meanwhile is refused with <c>Can't reenter LISP.</c>.
        /// </summary>
        internal bool IsEvaluating => _worker != null;

        /// <summary>
        /// Runs a piece of work on the owner thread, as a built-in does for everything that reads or changes the document or
        /// the session: the worker hands the delegate over and blocks, the owner runs it and gives back its value, or throws
        /// its <see cref="LispException"/> in the worker. Called on any other thread than the worker's it just runs the work.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <returns>What the work returned.</returns>
        internal object OnOwner(Func<object> work)
        {
            if (Thread.CurrentThread != _worker)
            {
                return work();
            }

            _ownerWork = work;
            _turn = Turn.OwnerWork;
            _ownerTurn.Release();
            _workerTurn.Wait();

            Exception failure = _ownerFailure;
            object result = _ownerResult;
            _ownerWork = null;
            _ownerResult = null;
            _ownerFailure = null;

            // The events the work raised are answered as soon as the work is over, before the built-in returns.
            DrainReactions();

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return result;
        }

        /// <summary>
        /// Waits for the user, as a get function does: the worker blocks and the evaluation returns to its caller as a result
        /// that <see cref="LispResult.IsWaiting"/>; the answer given to <see cref="Resume"/> is what this gives back.
        /// </summary>
        /// <param name="ask">What is asked.</param>
        /// <returns>The answer, a LISP value of the kind the ask says, or the string of a keyword.</returns>
        /// <exception cref="LispException">Thrown with <c>Function cancelled</c> when the user pressed Esc.</exception>
        internal object Ask(LispAsk ask)
        {
            if (Thread.CurrentThread != _worker)
            {
                throw new LispException("Can't reenter LISP.");
            }

            if (RunningDialogs > 0)
            {
                throw EndEvaluation("AutoCAD rejected function");
            }

            if (_aborting)
            {
                throw new LispException("evaluation aborted", isHard: true);
            }

            // A callback that runs at once, for an event no evaluation was going on at, has nobody to wait for the user; one that
            // runs inside an evaluation waits as the evaluation does (AutoCAD 2021, probe m1: getstring in a callback works).
            if (_reacting > 0 && _idleReactions)
            {
                throw new LispException("AutoCAD rejected function");
            }

            _waiting = ask;
            _answer = null;
            _cancelled = false;
            _turn = Turn.Asking;
            _ownerTurn.Release();
            _workerTurn.Wait();

            // The events that came while the routine waited are answered before it goes on.
            DrainReactions();

            object answer = _answer;
            _answer = null;

            if (_cancelled)
            {
                _cancelled = false;
                _cancelledAtAsk = true;
                throw new LispException("Function cancelled");
            }

            return answer;
        }

        /// <summary>
        /// Ends the evaluation that goes on for good: the next question it asks is a hard error, which <c>*error*</c> and
        /// <c>vl-catch-all-apply</c> do not see and which writes nothing, so that a routine that catches <c>Function cancelled</c>
        /// and asks again unwinds. Forgotten when the evaluation is over; does nothing where none goes on.
        /// </summary>
        internal void Abort()
        {
            _aborting = _worker != null;
        }

        /// <summary>Answers the wait: the evaluation goes on until it asks again or ends.</summary>
        /// <param name="answer">The answer, as the kind of the ask says.</param>
        /// <returns>The result at the next wait (with the text written since), or the end.</returns>
        internal LispResult Resume(object answer)
        {
            if (_waiting == null)
            {
                throw new InvalidOperationException("No evaluation waits for an answer.");
            }

            _answer = answer;
            return GiveTurn();
        }

        /// <summary>Esc at the wait: the evaluation gets <c>Function cancelled</c> where it asked, and goes on until it asks again or ends.</summary>
        /// <returns>The result at the next wait, or the end.</returns>
        internal LispResult CancelWait()
        {
            if (_waiting == null)
            {
                throw new InvalidOperationException("No evaluation waits for an answer.");
            }

            _cancelled = true;
            return GiveTurn();
        }

        /// <summary>
        /// Starts the evaluation on a thread of its own, with a large stack, and returns when it waits for the user or ends.
        /// While another evaluation goes on the text <c>Can't reenter LISP.</c> is the failure.
        /// </summary>
        private LispResult Evaluate(Func<LispResult> body)
        {
            if (_worker != null)
            {
                return new LispResult(null, "Can't reenter LISP.", true, "Can't reenter LISP.");
            }

            _body = body;
            _finished = null;
            _workerFailure = null;
            _turn = Turn.Worker;
            _worker = new Thread(Work, StackSize) { IsBackground = true };
            _worker.Start();
            return Pump();
        }

        private void Work()
        {
            try
            {
                _finished = _body();
            }
            catch (Exception exception)
            {
                _workerFailure = exception;
            }

            _turn = Turn.Done;
            _ownerTurn.Release();
        }

        private LispResult GiveTurn()
        {
            _waiting = null;
            _turn = Turn.Worker;
            _workerTurn.Release();
            return Pump();
        }

        /// <summary>
        /// The owner's loop: waits for the worker to ask for something, does the work it hands over, and returns when it
        /// waits for the user or is done.
        /// </summary>
        private LispResult Pump()
        {
            while (true)
            {
                _ownerTurn.Wait();

                switch (_turn)
                {
                    case Turn.OwnerWork:
                        try
                        {
                            _ownerResult = _ownerWork();
                        }
                        catch (Exception exception)
                        {
                            _ownerFailure = exception;
                        }

                        _turn = Turn.Worker;
                        _workerTurn.Release();
                        break;
                    case Turn.Asking:
                        string text = _output.ToString(_chunkStart, _output.Length - _chunkStart);
                        _chunkStart = _output.Length;
                        return new LispResult(null, text, false, null, waiting: true);
                    default:
                        _worker.Join();
                        _worker = null;
                        _body = null;
                        _aborting = false;
                        Exception failure = _workerFailure;
                        LispResult finished = _finished;
                        _workerFailure = null;
                        _finished = null;

                        if (failure != null)
                        {
                            ExceptionDispatchInfo.Capture(failure).Throw();
                        }

                        return finished;
                }
            }
        }

        /// <summary>
        /// Makes the ask of a get function with what <c>initget</c> said, which holds for this one ask and is forgotten
        /// after it.
        /// </summary>
        /// <param name="kind">What is asked.</param>
        /// <param name="message">The message as the routine gave it; null for none.</param>
        /// <returns>The ask, with its <see cref="LispAsk.Flags"/> and <see cref="LispAsk.Keywords"/>.</returns>
        internal LispAsk NewAsk(LispAskKind kind, string message)
        {
            var ask = new LispAsk
            {
                Kind = kind,
                Message = message,
                Flags = _initgetBits,
                Keywords = _initgetKeywords,
            };
            _initgetBits = 0;
            _initgetKeywords = Array.Empty<string>();
            return ask;
        }

        /// <summary>
        /// Takes the text the evaluation has written since the host last took it, so that the host can write it before a line
        /// of its own that comes later in the evaluation (the name of a command <c>command</c> starts), and the lines come in
        /// the order they were made. Called on the owner thread, where the worker waits.
        /// </summary>
        /// <returns>The text; empty where there is none.</returns>
        internal string TakeOutput()
        {
            string text = _output.ToString(_chunkStart, _output.Length - _chunkStart);
            _chunkStart = _output.Length;
            return text;
        }

        /// <summary>The most callbacks one drain runs: an event a callback raises again and again (a <c>setvar</c> of the variable it answers) stops here.</summary>
        internal const int MaxReactionsPerDrain = 1000;

        private readonly Queue<LispReactionCall> _reactions = new Queue<LispReactionCall>();
        private int _reacting;
        private LispSymbol _reactionName;

        private bool _cancelledAtAsk;
        private bool _idleReactions;

        /// <summary>Tells whether the user's Esc ended a get function since this was last asked (the <c>Function cancelled</c> of <see cref="Ask"/>), and forgets it: what <c>vlr-lispCancelled</c> is raised for.</summary>
        /// <returns>True when it did.</returns>
        internal bool TakeCancelledAtAsk()
        {
            bool cancelled = _cancelledAtAsk;
            _cancelledAtAsk = false;
            return cancelled;
        }

        /// <summary>Gets the symbol with no name that <c>(princ)</c> returns, which prints nothing.</summary>
        internal LispSymbol EmptySymbol => _empty;

        /// <summary>Gets whether a reaction is running: the callback of a reactor answers an event.</summary>
        internal bool IsReacting => _reacting > 0;

        /// <summary>Gets the event name of the reaction running, as the registered symbol; null when none runs.</summary>
        internal LispSymbol ReactionName => _reactionName;

        /// <summary>
        /// Keeps the callbacks of an event the owner raised while an evaluation goes on (inside work the worker asked for, or
        /// while a get function waits): the worker runs them as soon as it has the turn back.
        /// </summary>
        /// <param name="calls">The callbacks, in the order they are run.</param>
        internal void QueueReactions(IEnumerable<LispReactionCall> calls)
        {
            foreach (LispReactionCall call in calls)
            {
                _reactions.Enqueue(call);
            }
        }

        /// <summary>
        /// Runs the callbacks of an event raised while no evaluation goes on, as an evaluation of its own with no echo: what an
        /// error in one writes is in the output, and the others run all the same.
        /// </summary>
        /// <param name="calls">The callbacks.</param>
        /// <returns>The evaluation's result, whose output is what the callbacks wrote.</returns>
        internal LispResult RunReactions(IReadOnlyList<LispReactionCall> calls)
        {
            QueueReactions(calls);
            _idleReactions = true;

            try
            {
                return Execute(
                    () =>
                    {
                        DrainReactions();
                        return null;
                    },
                    echo: false
                );
            }
            finally
            {
                _idleReactions = false;
            }
        }

        /// <summary>
        /// Runs the callbacks the owner queued, in order, on the worker: each is called with its arguments; an error in one is
        /// written and the next runs; the callbacks' own events join the end of the queue. Does nothing inside a callback (the
        /// drain going on picks the new ones up) or with nothing queued. After <see cref="MaxReactionsPerDrain"/> the rest is
        /// dropped, with one line in the log and <c>; error: reactor cascade stopped</c> in the output.
        /// </summary>
        private void DrainReactions()
        {
            if (_reacting > 0 || _reactions.Count == 0)
            {
                return;
            }

            int ran = 0;

            while (_reactions.Count > 0)
            {
                if (ran == MaxReactionsPerDrain)
                {
                    int dropped = _reactions.Count;
                    _reactions.Clear();
                    GeometryHelperLog.Warn(
                        "LispInterpreter: reactor cascade stopped after "
                            + MaxReactionsPerDrain
                            + " callbacks; "
                            + dropped
                            + " dropped."
                    );
                    WriteFreshLine();
                    _output.Append("; error: reactor cascade stopped\n");
                    return;
                }

                LispReactionCall call = _reactions.Dequeue();
                ran++;

                if (call.Reactor.Added)
                {
                    RunReaction(call);
                }
            }
        }

        private void RunReaction(LispReactionCall call)
        {
            int depth = _depth;
            LispSymbol previous = _reactionName;
            _reacting++;
            _reactionName = call.Reaction;

            try
            {
                // The function is looked up now, as AutoCAD does: a callback redefined since the reactor was made is the new one.
                if (call.Callback.Value == null)
                {
                    throw new LispException("no function definition: " + call.Callback.Name);
                }

                Apply(Callable(call.Callback, catchAll: false), call.Arguments);
            }
            catch (LispException error) when (!error.IsHard)
            {
                // What an error in a callback does is what one at the top level does: *error* is called with the message where the
                // routine defined it, else "; error: message" follows what the callback wrote; the event's command goes on and the
                // reactor stays (AutoCAD 2021, probe m1).
                Report(error);
                _depth = depth;
            }
            finally
            {
                _reacting--;
                _reactionName = previous;
            }
        }

        /// <summary>Keeps what <c>initget</c> said for the next ask.</summary>
        /// <param name="bits">The bits.</param>
        /// <param name="keywords">The keywords, as written.</param>
        internal void SetInitget(int bits, IReadOnlyList<string> keywords)
        {
            _initgetBits = bits;
            _initgetKeywords = keywords ?? Array.Empty<string>();
        }
    }
}
