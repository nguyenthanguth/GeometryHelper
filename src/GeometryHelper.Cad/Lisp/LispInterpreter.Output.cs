namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private void RegisterOutput()
        {
            Define("PRINC", 0, 2, a => Emit(a, "", quoted: false, ""));
            Define("PRIN1", 0, 2, a => Emit(a, "", quoted: true, ""));
            Define("PRINT", 0, 2, a => Emit(a, "\n", quoted: true, " "));
            Define("PROMPT", 1, 1, Prompt);
            Define("TERPRI", 0, 0, a =>
            {
                Write("\n");
                return null;
            });
            RegisterFiles();
        }

        /// <summary>
        /// <c>princ</c>, <c>prin1</c> and <c>print</c>: write the value, <c>print</c> with a new line before and a space after, and give
        /// it back. With no value they write nothing and give the empty symbol, which the command line does not echo. A second
        /// argument is a file from <c>open</c>, which gets the text instead of the screen; nil there, and anything else that
        /// is no file, is refused with <c>bad argument type: FILE nil</c>.
        /// </summary>
        private object Emit(object[] a, string before, bool quoted, string after)
        {
            if (a.Length == 0)
            {
                return _empty;
            }

            if (a.Length > 1)
            {
                if (a[1] == null)
                {
                    throw TypeError("FILE", a[1]);
                }

                OutputFile(a[1]).Write(before + LispPrinter.Print(a[0], quoted, display: false) + after);
                return a[0];
            }

            Write(before + LispPrinter.Print(a[0], quoted, display: true) + after);
            return a[0];
        }

        private object Prompt(object[] a)
        {
            Write(LispPrinter.Display(Str(a[0])));
            return null;
        }
    }
}
