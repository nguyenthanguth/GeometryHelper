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
                    _lisp = new LispInterpreter
                    {
                        Variable = LispVariable,
                        // On the owner thread, as every built-in that touches the session: the question a window shows
                        // must not wait for the owner while the owner waits for the evaluation.
                        MayLoad = path => (bool)_lisp.OnOwner(() => LoadSecurity.MayLoad(path)),
                        LegacyCodeSearch = () => LoadSecurity.LegacyCodeSearch,
                    };
                    LispDrawing.Register(_lisp, this);
                }

                _lisp.SupportFolders = Document.SupportFolders;
                return _lisp;
            }
        }

        private CadLoadSecurity _loadSecurity = new CadLoadSecurity { SecureLoad = 0 };

        /// <summary>
        /// Gets or sets the gate every file of code this session loads goes through: SECURELOAD, TRUSTEDPATHS and
        /// LEGACYCODESEARCH, which the variables of those names read and set. A session of the library has an instance of its
        /// own at SECURELOAD 0, so what it loads is as it was before the gate; the window gives all its sessions one shared
        /// instance made from the Global settings. <c>load</c> and what is built on it (APPLOAD, the startup files) ask it for
        /// each file, and <c>findfile</c> and <c>load_dialog</c> ask it where to look.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public CadLoadSecurity LoadSecurity
        {
            get => _loadSecurity;
            set => _loadSecurity = value ?? throw new ArgumentNullException(nameof(value));
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

        /// <summary>Gets the AutoLISP files loaded into this document, as full paths in the order they were first loaded (<see cref="LispInterpreter.LoadedFiles"/>): by <see cref="LoadLisp"/>, by <c>(load)</c> and by the startup files; empty where no AutoLISP has run.</summary>
        public IReadOnlyList<string> LoadedLispFiles =>
            _lisp != null ? _lisp.LoadedFiles : (IReadOnlyList<string>)Array.Empty<string>();

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

        /// <summary>
        /// Writes what AutoLISP wrote, a line of the history for each line of it, a blank one where it wrote nothing between two
        /// new lines; nothing where it wrote nothing. The text a command of the routine left on its last line, the Command
        /// prompt or the prompt it asks, starts the first line (<c>Command: done7</c>, <c>Specify next point or [Undo]: nil</c>).
        /// </summary>
        internal void WriteLisp(string output)
        {
            string open = _lispPrefix;
            _lispPrefix = null;

            if (string.IsNullOrEmpty(output))
            {
                return;
            }

            string[] lines = output.Split('\n');

            if (open != null)
            {
                lines[0] = open + lines[0];
            }

            foreach (string line in lines)
            {
                Write(line);
            }
        }

        /// <summary>
        /// Runs the function of a <c>defun c:name</c> when its name is typed: the session keeps the evaluation, and where the
        /// routine waits for the user its prompts are asked as commands of their own, until the routine ends. The command is
        /// the one Enter runs again, and the undo step of what the routine changes outside a command is named after it.
        /// </summary>
        private void StartLispCommand(string name)
        {
            ReleaseLispHold();
            LastCommandName = name.ToUpperInvariant();
            _lispStep = false;
            _lispStepName = LastCommandName;
            _lispAnswers = false;
            _lispPrefix = null;
            LispContinue(Lisp.RunCommand(name), done => LastOutcome = CadCommandOutcome.Completed);
            Changed();
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
            _lispPrefix = null;

            // A form typed at the prompt of a command: its value answers that command, unless the form ran commands of its own.
            bool atPrompt = Prompt != null;
            CadCommand interrupted = Current;
            _lispAnswers = atPrompt;
            _lispTyped = atPrompt;
            LispResult result = Lisp.Run(source[0] == '!' ? source.Substring(1) : source);
            _lispTyped = false;

            // A routine that waits for the user is asked as a command of its own; the line is over when it is.
            LispContinue(result, null);

            if (result.IsWaiting)
            {
                return true;
            }

            Changed();

            if (!atPrompt || Prompt == null || !ReferenceEquals(Current, interrupted))
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
