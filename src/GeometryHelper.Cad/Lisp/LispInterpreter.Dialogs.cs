namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        /// <summary>
        /// Gets or sets how many dialogs <c>start_dialog</c> is showing. While there is one, a function that waits for the user
        /// and <c>command</c> are refused with <c>AutoCAD rejected function</c> (D327, D343).
        /// </summary>
        internal int RunningDialogs { get; set; }

        /// <summary>
        /// Reads and evaluates every form of the text, one after the other, inside the evaluation going on: the string of an
        /// action <c>action_tile</c> gave. Called on the worker thread; an error goes out as any error of a function does.
        /// </summary>
        /// <param name="text">The AutoLISP text.</param>
        /// <returns>The value of the last form; nil for no form.</returns>
        internal object EvalText(string text)
        {
            var reader = new LispReader(text, Intern, _quote);
            object last = null;

            while (reader.TryRead(out object form))
            {
                last = Eval(form);
            }

            return last;
        }

        /// <summary>
        /// Makes the error that ends the whole evaluation and that nothing in the routine sees, as AutoCAD 2021 ends it for an
        /// error inside the action of a dialog and for <c>command</c> or a get function called there (D343): the line
        /// <c>; error: message</c> is written now, <c>*error*</c> is not run, <c>vl-catch-all-apply</c> does not catch it, and
        /// every question asked from here on is refused. The caller throws what this gives.
        /// </summary>
        /// <param name="message">The message after <c>; error: </c>.</param>
        /// <param name="from">The error this one ends the evaluation for, whose bound variables are given back at the top; null for none.</param>
        /// <returns>The error to throw.</returns>
        internal LispException EndEvaluation(string message, LispException from = null)
        {
            Write("; error: " + message);
            Abort();
            var ended = new LispException(message, isHard: true);

            if (from != null)
            {
                ended.Frames.AddRange(from.Frames);
            }

            return ended;
        }
    }
}
