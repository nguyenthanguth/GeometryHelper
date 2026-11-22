using System.Collections.Generic;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The input of <c>grread</c>: the keys, clicks and moves the window gives a routine that reads the mouse. Each is one answer
    /// of the wait and runs the routine's loop once. Keys and clicks wait their turn in the order they came; a move that comes
    /// when the one before it has not been taken yet takes its place, so that a loop slower than the mouse never falls behind it.
    /// </summary>
    public sealed partial class CadCommandSession
    {
        // What one answer of grread is, in the codes grread gives: 2 a key, 3 a click, 5 a move, 11 a right click.
        private const int GrKey = 2;
        private const int GrClick = 3;
        private const int GrMove = 5;
        private const int GrRight = 11;

        private readonly struct GrEvent
        {
            internal GrEvent(int type, int code, GeoPoint2 point)
            {
                Type = type;
                Code = code;
                Point = point;
            }

            internal int Type { get; }

            internal int Code { get; }

            // The point in the world; the UCS of the moment it is handed over turns it into the coordinates of the answer.
            internal GeoPoint2 Point { get; }
        }

        // Answers that wait for the routine to ask again, the oldest first; the one answer being handed over; whether the routine
        // is being run by one now, so that what arrives meanwhile waits in the queue.
        private readonly List<GrEvent> _grQueue = new List<GrEvent>();
        private object _grAnswer;
        private bool _grDelivering;

        /// <summary>Gets whether the prompt waiting is <c>grread</c>'s.</summary>
        private bool IsGrRead => Prompt != null && Prompt.ReadsInput;

        /// <summary>
        /// A key pressed at the prompt of <c>grread</c>: the routine gets <c>(2 code)</c>. Where the routine has not asked again
        /// yet, the key waits behind those before it.
        /// </summary>
        /// <param name="code">13 for Enter, 32 for Space, 8 for Backspace, 9 for Tab, the character's code for a printable key.</param>
        /// <returns>Whether the prompt is <c>grread</c>'s and took the key.</returns>
        internal bool FeedKey(int code) => FeedGrRead(new GrEvent(GrKey, code, default));

        /// <summary>
        /// The cursor moved at the prompt of <c>grread</c> that reads moves (<c>(grread T)</c>): the routine gets <c>(5 (x y z))</c>.
        /// Moves are not kept: one that comes before the routine has asked again takes the place of the move before it.
        /// </summary>
        /// <param name="point">The cursor, in drawing units.</param>
        /// <returns>Whether the prompt reads moves and took it.</returns>
        internal bool FeedMove(GeoPoint2 point)
        {
            if (!IsGrRead || !Prompt.ReadsMoves)
            {
                return false;
            }

            int last = _grQueue.Count - 1;

            if (last >= 0 && _grQueue[last].Type == GrMove)
            {
                _grQueue.RemoveAt(last);
            }

            return FeedGrRead(new GrEvent(GrMove, 0, point));
        }

        /// <summary>The right button pressed at the prompt of <c>grread</c>: the routine gets <c>(11 0)</c>, and no menu opens.</summary>
        /// <param name="point">The cursor, in drawing units.</param>
        /// <returns>Whether the prompt is <c>grread</c>'s and took it.</returns>
        internal bool FeedRightClick(GeoPoint2 point) => FeedGrRead(new GrEvent(GrRight, 0, point));

        /// <summary>
        /// Text fed to the prompt of <c>grread</c> (a test, the remote server): one key for each character, then Enter, as the
        /// keys of a line typed come; a line that starts with a parenthesis is keys too, and no AutoLISP.
        /// </summary>
        private bool FeedGrReadText(string text)
        {
            foreach (char c in text)
            {
                if (c != '\r')
                {
                    _grQueue.Add(new GrEvent(GrKey, c == '\n' ? 13 : c, default));
                }
            }

            return FeedGrRead(new GrEvent(GrKey, 13, default));
        }

        /// <summary>Waits the answer in the queue and hands over what is there while the prompt is still <c>grread</c>'s.</summary>
        private bool FeedGrRead(GrEvent answer)
        {
            if (!IsGrRead)
            {
                return false;
            }

            _grQueue.Add(answer);

            // Handed over by the loop already running, which asks again before it comes back here.
            if (_grDelivering)
            {
                return true;
            }

            _grDelivering = true;

            try
            {
                while (IsGrRead && _grQueue.Count > 0)
                {
                    GrEvent next = _grQueue[0];
                    _grQueue.RemoveAt(0);
                    _grAnswer = GrAnswerOf(next);
                    Answer(CadInput.Nothing, null, null);
                }
            }
            finally
            {
                _grDelivering = false;

                // The routine stopped reading: what was left is not kept for another prompt.
                if (!IsGrRead)
                {
                    _grQueue.Clear();
                }
            }

            return true;
        }

        /// <summary>The list <c>grread</c> gives for an answer: <c>(2 code)</c>, <c>(3 (x y z))</c>, <c>(5 (x y z))</c> or <c>(11 0)</c>.</summary>
        private object GrAnswerOf(GrEvent answer)
        {
            switch (answer.Type)
            {
                case GrKey:
                    return LispInterpreter.ListOf(new object[] { GrKey, answer.Code });
                case GrRight:
                    return LispInterpreter.ListOf(new object[] { GrRight, 0 });
                default:
                    GeoPoint2 inUcs = Ucs.ToUcs(answer.Point);
                    return LispInterpreter.ListOf(
                        new object[] { answer.Type, LispInterpreter.ListOf(new object[] { inUcs.X, inUcs.Y, 0.0 }) }
                    );
            }
        }

        /// <summary>Takes the answer being handed over, for the routine to be resumed with.</summary>
        private object TakeGrAnswer()
        {
            object answer = _grAnswer;
            _grAnswer = null;
            return answer;
        }

        /// <summary>Drops what waits in the queue: Esc has ended the wait.</summary>
        private void ForgetGrRead() => _grQueue.Clear();
    }
}
