using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The owner of the tests (D704): there is no window and no second thread. <see cref="Post"/> keeps the work until
    /// <see cref="Drain"/> runs it on the thread of the test, and <see cref="WaitUntil"/> plays the answers the test queued
    /// with <see cref="Answer"/>, one for each time the script waits, until the wait is over. When the script waits and no
    /// answer is left the owner presses Esc and notes what was asked, so that a test that gave too few answers fails with
    /// the prompt it did not answer (see <see cref="Unanswered"/>) and never hangs.
    /// </summary>
    internal sealed class TestPythonOwner : ICadPythonOwner
    {
        private readonly Queue<Action> _posted = new Queue<Action>();
        private readonly Queue<Action<CadCommandSession>> _answers = new Queue<Action<CadCommandSession>>();
        private readonly List<string> _asked = new List<string>();
        private readonly List<string> _unanswered = new List<string>();

        /// <summary>Gets or sets the session the answers are given to: the one the run started in.</summary>
        public CadCommandSession Session { get; set; }

        /// <summary>Gets the text of the prompt each wait showed, in order, answered or not.</summary>
        public IReadOnlyList<string> Asked => _asked;

        /// <summary>Gets the prompts a script waited at when no answer was left to give.</summary>
        public IReadOnlyList<string> Unanswered => _unanswered;

        /// <summary>Gets the number of answers queued and not yet used.</summary>
        public int PendingAnswers => _answers.Count;

        /// <summary>Forgets the posts, the answers and the record of what was asked, for the next test.</summary>
        public void Reset()
        {
            _posted.Clear();
            _answers.Clear();
            _asked.Clear();
            _unanswered.Clear();
            Session = null;
        }

        /// <summary>Queues answers, each given to the session when a script waits.</summary>
        public void Answer(params Action<CadCommandSession>[] answers)
        {
            foreach (Action<CadCommandSession> answer in answers)
            {
                _answers.Enqueue(answer);
            }
        }

        /// <inheritdoc/>
        public void Post(Action work) => _posted.Enqueue(work);

        /// <summary>Runs the posted work, and what that posts, until none is left.</summary>
        public void Drain()
        {
            while (_posted.Count > 0)
            {
                _posted.Dequeue()();
            }
        }

        /// <inheritdoc/>
        public void WaitUntil(Func<bool> done)
        {
            while (!done())
            {
                _asked.Add(Session.PromptText);

                if (_answers.Count == 0)
                {
                    _unanswered.Add(Session.PromptText);
                    Session.Escape();

                    if (!done())
                    {
                        throw new InvalidOperationException(
                            "The wait did not end after Esc at '" + Session.PromptText + "'."
                        );
                    }

                    return;
                }

                _answers.Dequeue()(Session);
            }
        }
    }
}
