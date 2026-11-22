namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The functions only AutoCAD can run: ObjectARX, the Visual LISP compiler and projects, AutoCAD's command table and
    /// registry, its debugger and editor. They are defined, so that a routine written for AutoCAD finds the name, and calling
    /// one is an error that says so, <c>ARXLOAD is not available: ObjectARX applications load only in AutoCAD.</c>, which
    /// <c>vl-catch-all-apply</c> catches like any other. Where AutoCAD names a value for a failed load (<c>(arxload "app"
    /// nil)</c>), that value is given, as it is when AutoCAD cannot load the application. Which of them exist before
    /// <c>(vl-load-com)</c> was measured in AutoCAD 2021 (probe g8): all but the registration and product keys of ActiveX.
    /// </summary>
    public sealed partial class LispInterpreter
    {
        private const string ArxReason = "ObjectARX applications load only in AutoCAD.";
        private const string CompilerReason = "Visual LISP compiles and makes projects only in AutoCAD.";

        private static readonly string[] CompilerFunctions =
        {
            "VLISP-COMPILE",
            "VLISP-COMPILE-LIST",
            "VLISP-DCLRES-LIST",
            "VLISP-DCLRES-LOAD-DIALOG",
            "VLISP-DROP-PROJECT",
            "VLISP-EXPORT-SYMBOL",
            "VLISP-FASRES-LIST",
            "VLISP-FASRES-LOAD",
            "VLISP-FIND-PROJECT",
            "VLISP-IMPORT-EXSUBRS",
            "VLISP-IMPORT-SYMBOL",
            "VLISP-INIRES-LIST",
            "VLISP-LIST-LOADED-PROJECTS",
            "VLISP-LOAD-PROJECT",
            "VLISP-MAKE-FILE-FAS",
            "VLISP-MAKE-PROJECT-FAS",
            "VLISP-MAKE-PROJECT-LOAD-FILE",
            "VLISP-PROJECT-FAS-LIST",
            "VLISP-READ-FILE-XDV-LIST",
            "VLISP-READ-PROJECT-XDV-LIST",
        };

        private void RegisterAutoCadOnly()
        {
            // Nothing is loaded: AutoCAD 2021 answers the list of the ObjectARX applications it has (probe g7).
            Define("ARX", 0, 0, a => null);
            Define("ADS", 0, 0, a => null);
            Define("ARXLOAD", 1, 2, a => Unavailable("ARXLOAD", ArxReason, a));
            Define("ARXUNLOAD", 1, 2, a => Unavailable("ARXUNLOAD", ArxReason, a));

            foreach (string name in CompilerFunctions)
            {
                DefineUnavailable(name, CompilerReason);
            }

            DefineUnavailable("VLAX-ADD-CMD", "AutoCAD's command table is not here to add a command to.");
            DefineUnavailable("VLAX-REMOVE-CMD", "AutoCAD's command table is not here to take a command from.");
            DefineUnavailable("VL-INIT", "the Visual LISP runtime belongs to AutoCAD.");
            DefineUnavailable("VLARTS-INIT", "the Visual LISP runtime belongs to AutoCAD.");
            DefineUnavailable("VL-BT", "the backtrace needs AutoCAD's Visual LISP debugger.");
            DefineUnavailable("VL-BT-ON", "the backtrace needs AutoCAD's Visual LISP debugger.");
            DefineUnavailable("VL-BT-OFF", "the backtrace needs AutoCAD's Visual LISP debugger.");
            DefineUnavailable("C:VLIDE", "the Visual LISP editor is part of AutoCAD only.");
            DefineUnavailable("C:VLDEBUG", "the Visual LISP debugger is part of AutoCAD only.");

            // These three and the registration need the ActiveX library: defined by (vl-load-com), as in AutoCAD.
            OnLoadCom(() =>
            {
                DefineUnavailable(
                    "VLAX-REG-APP",
                    "AutoCAD's ActiveX server is not here to register an application with."
                );
                DefineUnavailable("VLAX-MACHINE-PRODUCT-KEY", "AutoCAD's product keys are not kept here.");
                DefineUnavailable("VLAX-PRODUCT-KEY", "AutoCAD's product keys are not kept here.");
                DefineUnavailable("VLAX-USER-PRODUCT-KEY", "AutoCAD's product keys are not kept here.");
            });
        }

        /// <summary>Defines a function of AutoCAD alone: any call, with any arguments, is the error <c>NAME is not available: reason</c>.</summary>
        private void DefineUnavailable(string name, string reason) =>
            Define(name, 0, -1, a => throw new LispException(name + " is not available: " + reason));

        /// <summary>
        /// The body of <c>arxload</c> and <c>arxunload</c>: the name has to be a string, as in AutoCAD; the second argument, what
        /// to give when the application cannot be loaded, is the value (nil too); without it the call is the refusal.
        /// </summary>
        private static object Unavailable(string name, string reason, object[] a)
        {
            Str(a[0]);

            if (a.Length > 1)
            {
                return a[1];
            }

            throw new LispException(name + " is not available: " + reason);
        }
    }
}
