using System;
using System.IO;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// What the files functions take from the session: the drawing's folder, where <c>findfile</c> looks after the current
    /// folder, the keyboard <c>read-line</c> and <c>read-char</c> read when they are given no file, and <c>getfiled</c>, which
    /// asks the window for a file dialog. The rest of the files functions are the interpreter's own.
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterFiles(LispInterpreter lisp, CadCommandSession session)
        {
            lisp.DrawingFolder = () => (string)lisp.OnOwner(() => FolderOf(session.Document));

            // The keyboard asks for one whole line, with no prompt of its own; Esc gives Function cancelled, as at any ask. The
            // ask takes no initget: those belong to the next get function.
            lisp.KeyboardLine = () => lisp.Ask(new LispAsk { Kind = LispAskKind.Line }) as string;

            lisp.Define("GETFILED", 4, 4, a => GetFile(lisp, session, a));
        }

        /// <summary>
        /// The folder of the drawing's file, as <c>DWGPREFIX</c> gives it without its last separator; empty for a drawing never
        /// saved, which has no folder to look in (<c>DWGPREFIX</c> itself gives the documents folder then).
        /// </summary>
        private static string FolderOf(CadDocument document)
        {
            if (!document.HasFile || string.IsNullOrEmpty(document.FileName))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetDirectoryName(Path.GetFullPath(document.FileName)) ?? string.Empty;
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// <c>(getfiled title default ext flags)</c>: the arguments are checked here, then the window is asked for the dialog
        /// (<c>GETFILED</c>, a <see cref="CadGetFileRequest"/>). The path the user chose is the value; nil when the user cancelled
        /// and where no window answers, as the console gives.
        /// </summary>
        private static object GetFile(LispInterpreter lisp, CadCommandSession session, object[] a)
        {
            var request = new CadGetFileRequest(
                LispInterpreter.Str(a[0]),
                LispInterpreter.Str(a[1]),
                LispInterpreter.Str(a[2]),
                LispInterpreter.Int(a[3])
            );

            return lisp.OnOwner(() =>
            {
                CadUiRequestEventArgs asked = session.Ask("GETFILED", session.Document, request);
                return asked.Handled && !string.IsNullOrEmpty(asked.Answer) ? asked.Answer : null;
            });
        }
    }
}
