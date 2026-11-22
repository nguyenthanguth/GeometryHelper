using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        // The error modes a routine pushed in this top-level evaluation, innermost last: true for *push-error-using-command*,
        // false for *push-error-using-stack*. Empty at the start of every evaluation, as AutoCAD's is (probe g5b: a pop on the
        // next typed line is an underflow).
        private readonly List<bool> _errorModes = new List<bool>();

        // Whether *error* is running as the handler of an error, and whether command may be called in it: the mode that was
        // innermost when the error came was *push-error-using-command*.
        private bool _handlingError;
        private bool _handlerMayCommand;

        private void RegisterErrors()
        {
            Define("EXIT", 0, 0, a => throw new LispException("quit / exit abort"));
            Define("QUIT", 0, 0, a => throw new LispException("quit / exit abort"));
            Define("*PUSH-ERROR-USING-COMMAND*", 0, 0, a => PushErrorMode(true));
            Define("*PUSH-ERROR-USING-STACK*", 0, 0, a => PushErrorMode(false));
            Define("*POP-ERROR-MODE*", 0, 0, PopErrorMode);
            Define("VL-CATCH-ALL-APPLY", 2, 2, CatchAllApply);
            Define("VL-CATCH-ALL-ERROR-P", 1, 1, a => Bool(a[0] is LispCatchAllError));
            Define("VL-CATCH-ALL-ERROR-MESSAGE", 1, 1, a =>
                a[0] is LispCatchAllError error
                    ? error.Message
                    : throw new LispException("argument is not a vl-catch-all-apply error: " + Print(a[0], true))
            );
        }

        /// <summary>
        /// <c>(*push-error-using-command*)</c> and <c>(*push-error-using-stack*)</c>: tell AutoCAD that the <c>*error*</c> the
        /// routine runs next, if an error comes, calls <c>command</c> (or only <c>command-s</c>). The mode lasts until the
        /// matching <c>(*pop-error-mode*)</c> or the end of the evaluation. Gives T.
        /// </summary>
        private object PushErrorMode(bool usesCommand)
        {
            _errorModes.Add(usesCommand);
            return _true;
        }

        /// <summary>
        /// <c>(*pop-error-mode*)</c>: takes back the last mode pushed and gives T; with none pushed in this evaluation it is the
        /// error <c>(*pop-error-mode*) underflow.</c>.
        /// </summary>
        private object PopErrorMode(object[] a)
        {
            if (_errorModes.Count == 0)
            {
                throw new LispException("(*pop-error-mode*) underflow.");
            }

            _errorModes.RemoveAt(_errorModes.Count - 1);
            return _true;
        }

        /// <summary>
        /// Called by <c>command</c> and <c>vl-cmdf</c>: while <c>*error*</c> runs as the handler of an error, they are refused
        /// unless the routine pushed <c>*push-error-using-command*</c> before the error came (AutoCAD 2021, probes g5b and
        /// g5c); the error is written after what the handler wrote, on a line of its own. <c>command-s</c> is always allowed.
        /// </summary>
        /// <exception cref="LispException">Thrown with AutoCAD's two lines when the handler may not call <c>command</c>.</exception>
        internal void CheckCommandAllowed()
        {
            if (!_handlingError || _handlerMayCommand)
            {
                return;
            }

            WriteFreshLine();
            throw new LispException(
                "Cannot invoke (command) from *error* without prior call to (*push-error-using-command*).\n"
                    + "Converting (command) calls to (command-s) is recommended."
            );
        }

        /// <summary>Starts a new line of the command-line output unless it is at the start of one, as AutoCAD does before a message of its own.</summary>
        internal void WriteFreshLine()
        {
            if (_output.Length > 0 && _output[_output.Length - 1] != '\n')
            {
                _output.Append('\n');
            }
        }

        /// <summary>
        /// <c>(vl-catch-all-apply function list)</c> calls the function and gives its value, or, when it fails, the error as a
        /// value; the variables the failed call bound are given back at once. The hard error is not caught. A symbol with no
        /// function is an error of <c>vl-catch-all-apply</c> itself, <c>bad function: NAME</c>, which nothing catches; and a
        /// list of arguments that is not a list gives the empty symbol, as AutoCAD does.
        /// </summary>
        private object CatchAllApply(object[] a)
        {
            LispFunction function = Callable(a[0], catchAll: true);

            if (a[1] != null && !(a[1] is LispCons))
            {
                return _empty;
            }

            int depth = _depth;

            try
            {
                return Apply(function, ArgumentsOf(a[1]));
            }
            catch (LispException error) when (!error.IsHard)
            {
                Restore(error.Frames);
                _depth = depth;
                return new LispCatchAllError(error.Message);
            }
        }
    }
}
