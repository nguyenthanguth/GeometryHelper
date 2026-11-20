namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private void RegisterErrors()
        {
            Define("EXIT", 0, 0, a => throw new LispException("quit / exit abort"));
            Define("QUIT", 0, 0, a => throw new LispException("quit / exit abort"));
            Define("VL-CATCH-ALL-APPLY", 2, 2, CatchAllApply);
            Define("VL-CATCH-ALL-ERROR-P", 1, 1, a => Bool(a[0] is LispCatchAllError));
            Define("VL-CATCH-ALL-ERROR-MESSAGE", 1, 1, a =>
                a[0] is LispCatchAllError error
                    ? error.Message
                    : throw new LispException("argument is not a vl-catch-all-apply error: " + Print(a[0], true))
            );
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
