using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Cad.Lisp;

namespace GeometryHelper.Cad
{
    /// <summary>The AutoLISP of a document: the interpreter, the lines typed at the command line, the <c>c:</c> commands and <c>load</c>.</summary>
    public sealed partial class CadCommandSession
    {
        private LispInterpreter _lisp;

        // The text of a line of AutoLISP that waits for its closing parenthesis, and the prompt that asks for the rest; null else.
        private string _lispText;
        private string _lispPrompt;

        /// <summary>
        /// Gets the AutoLISP interpreter of this document, made on first use: what a routine defines is seen by this document
        /// alone. <c>load</c> searches <see cref="CadDocument.SupportFolders"/>, read each time the interpreter is asked for,
        /// and <c>rtos</c> and <c>angtos</c> take the document's LUNITS, LUPREC, AUNITS, AUPREC and DIMZIN.
        /// </summary>
        public LispInterpreter Lisp
        {
            get
            {
                if (_lisp == null)
                {
                    _lisp = new LispInterpreter { Variable = LispVariable };
                    LispDrawing.Register(_lisp, this);
                }

                _lisp.SupportFolders = Document.SupportFolders;
                return _lisp;
            }
        }

        private object LispVariable(string name)
        {
            switch (name)
            {
                case "LUNITS":
                    return Document.Database.Lunits;
                case "LUPREC":
                    return Document.Database.Luprec;
                case "AUNITS":
                    return Document.Database.Aunits;
                case "AUPREC":
                    return Document.Database.Auprec;
                case "DIMZIN":
                    return Document.Database.Dimzin;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets whether text typed at the command line is AutoLISP: a line of it waits for more, or the text starts with
        /// <c>(</c>, a form, or <c>!</c>, the value of a variable. A bare word or number at the Command prompt is a command.
        /// While a routine waits for an answer, or for a command it started, nothing is AutoLISP: AutoCAD does not run LISP
        /// inside LISP, and the text goes to the prompt as typed.
        /// </summary>
        /// <param name="line">The text typed.</param>
        /// <returns>True when <see cref="Feed(string)"/> takes it as AutoLISP.</returns>
        public bool IsLispInput(string line)
        {
            if (IsLispWaiting)
            {
                return false;
            }

            if (_lispText != null)
            {
                return true;
            }

            string typed = line?.TrimStart();
            return !string.IsNullOrEmpty(typed) && (typed[0] == '(' || typed[0] == '!');
        }

        /// <summary>Gets the names of the commands AutoLISP routines defined with <c>defun c:name</c>, without <c>c:</c>, sorted by ordinal comparison; empty where no AutoLISP has run.</summary>
        public IReadOnlyList<string> LispCommandNames =>
            _lisp != null ? _lisp.CommandNames : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// Loads an AutoLISP file as APPLOAD does: the lines the file wrote, then <c>name.lsp successfully loaded.</c>, or the
        /// <c>; error:</c> line an error gave, which leaves what the file ran before it.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>False where the load failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public bool LoadLisp(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            // Where the file waits for the user the line comes when it is done; a load asked while a routine waits is refused.
            LispResult result = RunHosted(
                () => Lisp.Load(path),
                done =>
                {
                    if (!done.Failed)
                    {
                        Write(Path.GetFileName(path) + " successfully loaded.");
                    }
                }
            );

            Changed();
            return !result.Failed;
        }

        /// <summary>Writes what AutoLISP wrote, a line of the history for each line of it, a blank one where it wrote nothing between two new lines; nothing where it wrote nothing.</summary>
        internal void WriteLisp(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return;
            }

            foreach (string line in output.Split('\n'))
            {
                Write(line);
            }
        }

        /// <summary>The command a <c>defun c:name</c> made: its body runs the function on the interpreter and writes what it wrote.</summary>
        private CadCommand LispCommand(string name)
        {
            _lispCommand = new CadCommand(name, c => RunLispCommand(c, name));
            return _lispCommand;
        }

        /// <summary>The body of a <c>c:</c> command: it runs the function, and where the routine waits for the user its prompts are this command's, until the routine ends.</summary>
        private IEnumerable<CadPrompt> RunLispCommand(CadCommandContext c, string name)
        {
            _lispStep = false;
            _lispStepName = name.ToUpperInvariant();
            LispResult result = Lisp.RunCommand(name);
            WriteLisp(result.Output);

            if (!result.IsWaiting)
            {
                CloseLispStep();
                yield break;
            }

            foreach (CadPrompt prompt in LispWait(c, null))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// Takes a line of AutoLISP typed at the command line: a form, or <c>!</c> and the symbol or form whose value is
        /// written. With a parenthesis or a string left open the line waits for the rest under the prompt <c>(_&gt;</c>, each
        /// parenthesis open one more <c>(</c> and a string open a <c>"</c>. At the Command prompt the value is written after the
        /// output; at the prompt of a command it answers the prompt too: a list of two or three numbers as a point, a number
        /// or a string as typed; nil and the rest ask again.
        /// </summary>
        private bool FeedLisp(string text)
        {
            bool continuing = _lispText != null;
            string asked = continuing ? _lispPrompt : (Prompt == null ? "Command:" : Prompt.Text);
            string source = continuing ? _lispText + "\n" + text : text.TrimStart();
            string echo = asked + " " + (continuing ? text.TrimEnd() : text.Trim());
            int open = LispInterpreter.OpenParens(source, out bool inString);
            Write(echo);

            if (open > 0 || inString)
            {
                _lispText = source;
                _lispPrompt = new string('(', open) + (inString ? "\"" : string.Empty) + "_>";
                Changed();
                return true;
            }

            _lispText = null;
            _lispPrompt = null;
            _lispStep = false;
            _lispStepName = null;
            LispResult result = Lisp.Run(source[0] == '!' ? source.Substring(1) : source);
            WriteLisp(result.Output);

            // A routine that waits for the user is asked as a command of its own; the line is over when it is.
            if (result.IsWaiting)
            {
                StartLispWait(null);
                return true;
            }

            CloseLispStep();
            Changed();

            if (Prompt == null)
            {
                return !result.Failed;
            }

            // At the prompt of a command the value answers it, with no echo of its own: the line above was the echo.
            string answer = result.Failed ? null : LispAnswer(result.Value);
            return answer != null && Refeed(answer);
        }

        /// <summary>The text a prompt is answered with by a value of AutoLISP: <c>x,y</c> or <c>x,y,z</c> for a list of two or three numbers, a number as it is, a string as it is; null for nil and the rest.</summary>
        private static string LispAnswer(object value)
        {
            if (value is string text)
            {
                return text;
            }

            if (value is int || value is double)
            {
                return LispNumber(value);
            }

            var parts = new List<string>();
            object cell = value;

            while (cell is LispCons item)
            {
                if (!(item.Car is int || item.Car is double))
                {
                    return null;
                }

                parts.Add(LispNumber(item.Car));
                cell = item.Cdr;
            }

            return cell == null && (parts.Count == 2 || parts.Count == 3) ? string.Join(",", parts) : null;
        }

        private static string LispNumber(object number) =>
            number is int whole
                ? whole.ToString(CultureInfo.InvariantCulture)
                : ((double)number).ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Enter with a line of AutoLISP waiting: the new line is part of its text, which a string keeps; the prompt is written again.</summary>
        private void EnterLisp()
        {
            _lispText += "\n";
            Write(_lispPrompt);
            Changed();
        }

        /// <summary>
        /// Esc with a line of AutoLISP waiting: the text is dropped, <c>*Cancel*</c> written after its prompt, and the error
        /// <c>Function cancelled</c> reported as AutoCAD 2021 reports it: the <c>*error*</c> function is called with it where
        /// the routine defined one, else <c>; error: Function cancelled</c> is written.
        /// </summary>
        private void CancelLisp()
        {
            Write(_lispPrompt + " *Cancel*");
            _lispText = null;
            _lispPrompt = null;

            if (Lisp.GetValue("*ERROR*") == null)
            {
                Write("; error: Function cancelled");
            }
            else
            {
                RunHosted(() => Lisp.Run("(progn (*error* \"Function cancelled\") (princ))"));
            }

            Changed();
        }
    }
}
