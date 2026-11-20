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

            _waiting = ask;
            _answer = null;
            _cancelled = false;
            _turn = Turn.Asking;
            _ownerTurn.Release();
            _workerTurn.Wait();

            object answer = _answer;
            _answer = null;

            if (_cancelled)
            {
                _cancelled = false;
                throw new LispException("Function cancelled");
            }

            return answer;
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
