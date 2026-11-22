using System.Text;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>autoload</c>, which AutoCAD 2021 defines in its <c>acad2021doc.lsp</c> and so every drawing has: <c>(autoload "app"
    /// '("cmd1" "cmd2"))</c> defines the commands <c>C:CMD1</c> and <c>C:CMD2</c> as stubs, unless the program has them already,
    /// and the first run of a stub loads the file <c>app</c> and runs the command again, which the file has now defined. Each
    /// stub puts <c>*merrmsg*</c> in <c>*error*</c> while it works (an error shows only its message, with no <c>; error:</c>),
    /// writes <c>Initializing...</c> before the load, and where the file is not on the search path writes
    /// <c>The file app(.lsp/.exe/.arx) was not found in your search path folders.</c>. The stubs are made as AutoCAD makes them,
    /// from AutoLISP text, with the helper functions it has: <c>ai_ffile</c>, <c>ai_nofile</c>, <c>_autoload</c> and
    /// <c>*merrmsg*</c> (measured with the file loaded into AutoCAD 2021's accoreconsole, probe g9).
    /// </summary>
    public sealed partial class LispInterpreter
    {
        private static readonly string[] AutoloadExtensions = { ".lsp", ".exp", ".exe", ".arx", "" };

        private void RegisterAutoload()
        {
            Define("AUTOLOAD", 2, 2, Autoload);
            Define("AI_FFILE", 1, 1, a => AutoloadFile(Str(a[0])));
            Define("AI_NOFILE", 1, 1, a =>
            {
                Write("\nThe file " + Str(a[0]) + "(.lsp/.exe/.arx) was not found in your search path folders.");
                Write("\nCheck the installation of the support files and try again.");
                return _empty;
            });
            Define("_AUTOLOAD", 1, 1, a => LoadFile(Str(a[0]), false, null));
            Define("*MERRMSG*", 1, 1, a =>
            {
                Write(Print(a[0], false));
                Intern("*ERROR*").Value = Intern("M:ERR").Value;
                Intern("M:ERR").Value = null;
                return _empty;
            });
        }

        /// <summary>
        /// <c>(autoload app commands)</c>: defines <c>C:</c> and each name of the list as a stub that loads <c>app</c>, for every
        /// name that is not a function already; gives nil. An item that is not a string is the error <c>bad argument type:
        /// stringp</c>, and the names before it keep their stubs.
        /// </summary>
        private object Autoload(object[] a)
        {
            string app = Str(a[0]);

            if (a[1] != null && !(a[1] is LispCons))
            {
                throw TypeError("listp", a[1]);
            }

            foreach (object item in ItemsOf(a[1]))
            {
                string command = Str(item);

                if (Intern("C:" + command.ToUpperInvariant()).Value != null)
                {
                    continue;
                }

                string quotedApp = Print(app, true);
                string name = "C:" + command;
                var source = new StringBuilder();
                source.Append("(defun ").Append(name).Append("( / rtn)");
                source.Append("(setq m:err *error* *error* *merrmsg*)");
                source.Append("(if (ai_ffile ").Append(quotedApp).Append(")");
                source.Append("(progn (princ \"\\nInitializing...\")");
                source.Append("(_autoload ").Append(quotedApp).Append(") (setq rtn (").Append(name).Append(")))");
                source.Append("(ai_nofile ").Append(quotedApp).Append("))");
                source.Append("(setq *error* m:err m:err nil)");
                source.Append("rtn)");

                new LispReader(source.ToString(), Intern, _quote).TryRead(out object form);
                Eval(form);
            }

            return null;
        }

        /// <summary><c>ai_ffile</c>: the application's file as <c>findfile</c> finds it with <c>.lsp</c>, <c>.exp</c>, <c>.exe</c> and <c>.arx</c> after it, or as it is; nil where there is none.</summary>
        private object AutoloadFile(string app)
        {
            foreach (string extension in AutoloadExtensions)
            {
                object found = FindOnPath(app + extension);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
