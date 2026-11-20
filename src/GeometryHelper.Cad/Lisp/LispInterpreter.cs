using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// An AutoLISP interpreter that answers as AutoCAD 2021 does: the same values printed the same way, the same error
    /// messages, <c>defun</c> and <c>c:</c> commands, <c>*error*</c>, <c>load</c>. A tree walker with dynamic scoping: a
    /// symbol has one value cell, a function's arguments and locals are bound by saving what the cell held and putting it
    /// back when the function returns, and the special forms (<c>if</c>, <c>setq</c>, <c>defun</c>) are values of their
    /// symbols like any other function. Integers are 32 bits and wrap, as in AutoCAD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each document has an interpreter of its own, and nothing it defines is seen by another. Each call of
    /// <see cref="Run"/>, <see cref="RunCommand"/> or <see cref="Load"/> is evaluated on a new thread with a stack of 256 MB
    /// while the caller waits, so that a routine that recurses deeply cannot take the host down: user functions nested past
    /// 20 000 deep give the hard error <c>internal stack limit reached (simulated)</c>, which neither <c>*error*</c> nor
    /// <c>vl-catch-all-apply</c> sees.
    /// </para>
    /// <para>
    /// A function of a later stage (<c>command</c>, <c>ssget</c>, <c>vla-</c>, <c>vlax-</c>) is not defined, and calling it
    /// gives <c>no function definition: NAME</c>, as AutoCAD does for a function it does not have.
    /// </para>
    /// </remarks>
    public sealed partial class LispInterpreter
    {
        private const int MaxDepth = 20000;
        private const int StackSize = 256 * 1024 * 1024;

        private readonly Dictionary<string, LispSymbol> _symbols = new Dictionary<string, LispSymbol>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<LispSymbol, object> _defined = new Dictionary<LispSymbol, object>();
        private readonly StringBuilder _output = new StringBuilder();
        private readonly object _gate = new object();
        private readonly LispSymbol _true;
        private readonly LispSymbol _empty;
        private readonly LispSymbol _quote;
        private readonly LispSymbol _quoteLower;
        private readonly LispSymbol _lambda;
        private readonly LispSymbol _error;
        private LispBuiltin _quoteFunction;
        private IReadOnlyList<string> _supportFolders = Array.Empty<string>();
        private readonly List<string> _loadedFiles = new List<string>();
        private Thread _worker;
        private int _depth;

        /// <summary>Makes an interpreter with the functions of the core language and nothing defined by a program.</summary>
        public LispInterpreter()
        {
            _true = Intern("T");
            _true.Value = _true;
            _empty = new LispSymbol("");
            _quote = Intern("QUOTE");
            _quoteLower = new LispSymbol("quote");
            _lambda = Intern("LAMBDA");
            _error = Intern("*ERROR*");
            Intern("PI").Value = Math.PI;

            RegisterSpecialForms();
            RegisterLists();
            RegisterNumbers();
            RegisterStrings();
            RegisterOutput();
            RegisterErrors();
            RegisterMisc();
            RegisterUnits();
        }

        /// <summary>
        /// Wires in the functions that read the drawing's unit settings (<c>rtos</c>, <c>angtos</c>, <c>distof</c>,
        /// <c>angtof</c>); implemented where those functions are.
        /// </summary>
        partial void RegisterUnits();

        /// <summary>
        /// The folders <c>load</c> searches for a file given without a folder, in order: the support file search path. Empty
        /// by default, and null is taken as empty. The folder of the drawing is not searched, as in AutoCAD.
        /// </summary>
        public IReadOnlyList<string> SupportFolders
        {
            get => _supportFolders;
            set => _supportFolders = value ?? Array.Empty<string>();
        }

        /// <summary>
        /// Gets the files this interpreter has loaded, as full paths in the order they were first loaded: the files of
        /// <see cref="Load"/> and of <c>(load)</c> that were read to their end, which includes the startup files and what
        /// one file loads from another. A file whose load failed, from a missing file to an error inside it, is not in the
        /// list, and a file loaded twice is listed once. Empty until a file has been loaded.
        /// </summary>
        public IReadOnlyList<string> LoadedFiles => _loadedFiles.AsReadOnly();

        /// <summary>
        /// Reads a setting of the drawing by its name (<c>LUNITS</c>, <c>LUPREC</c>, <c>AUNITS</c>, <c>AUPREC</c>,
        /// <c>DIMZIN</c>), the way <c>rtos</c> and <c>angtos</c> take their defaults. When it is null, or gives null for a name,
        /// the defaults of a blank drawing apply: 2, 4, 0, 0 and 0.
        /// </summary>
        public Func<string, object> Variable { get; set; }

        /// <summary>
        /// Reads and evaluates every form of the text, one after the other, and stops at the first error.
        /// </summary>
        /// <param name="text">AutoLISP text: one expression typed at the command line, or several.</param>
        /// <returns>The value of the last form and the command-line text it made.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        public LispResult Run(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return Execute(() =>
            {
                var reader = new LispReader(text, Intern, _quote);
                object last = null;

                while (reader.TryRead(out object form))
                {
                    last = Eval(form);
                }

                return last;
            });
        }

        /// <summary>
        /// Runs the command <c>C:NAME</c> with no arguments, as the command line does when its name is typed. A function that
        /// takes arguments gives <c>too few arguments</c>.
        /// </summary>
        /// <param name="name">The name without <c>C:</c>, in either case.</param>
        /// <returns>The value and the command-line text, as for <see cref="Run"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public LispResult RunCommand(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return Execute(() => Eval(new LispCons(Intern("C:" + name.ToUpperInvariant()), null)));
        }

        /// <summary>
        /// Loads a file as <c>(load path)</c> does: a name with a folder is that file, any other is looked for in the
        /// <see cref="SupportFolders"/> in order, and <c>.lsp</c> is added to a name with no extension. The forms are read and
        /// evaluated one at a time, so an error leaves what ran before it.
        /// </summary>
        /// <param name="path">The file name as the program writes it.</param>
        /// <param name="onFailure">What to return when the file is not found; null for none, which makes it an error: <c>LOAD failed: "name"</c>.</param>
        /// <returns>The value of the last form in the file, and what the forms wrote; the value is not written after it, as
        /// <c>(load)</c> typed at the command line writes it, since the caller (APPLOAD, the startup files) writes its own
        /// line. A <c>.fas</c> or <c>.vlx</c> file is refused.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public LispResult Load(string path, object onFailure = null)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return Execute(() => LoadFile(path, onFailure != null, onFailure), echo: false);
        }

        /// <summary>Reads the first form of the text, as <c>(read text)</c> does.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The form: a list, a symbol, a number, a string; null for nil or for text with nothing in it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="LispException">Thrown for a reader error: <c>malformed list on input</c>, <c>extra right paren on input</c>, and the others of AutoCAD.</exception>
        public object Read(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            new LispReader(text, Intern, _quote).TryRead(out object form);
            return form;
        }

        /// <summary>Gets the value of a symbol.</summary>
        /// <param name="name">The name, in either case.</param>
        /// <returns>The value: an int, a double, a string, a <see cref="LispSymbol"/>, a <see cref="LispCons"/> list, a <see cref="LispFunction"/>; null for nil or a symbol with no value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public object GetValue(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return _symbols.TryGetValue(name.ToUpperInvariant(), out LispSymbol symbol) ? symbol.Value : null;
        }

        /// <summary>Sets the value of a symbol, making the symbol if there is none of that name.</summary>
        /// <param name="name">The name, in either case.</param>
        /// <param name="value">The value, of the kinds <see cref="GetValue"/> gives; null for nil.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public void SetValue(string name, object value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Intern(name.ToUpperInvariant()).Value = value;
        }

        /// <summary>Tells whether <c>C:NAME</c> is a command: bound by <c>defun</c> or <c>defun-q</c>, which a <c>setq</c> of a lambda to <c>c:name</c> is not.</summary>
        /// <param name="name">The name without <c>C:</c>, in either case.</param>
        /// <returns>True when typing the name at the command line runs a function.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public bool IsCommand(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return _symbols.TryGetValue("C:" + name.ToUpperInvariant(), out LispSymbol symbol) && IsDefined(symbol);
        }

        /// <summary>The names of the commands the program defined, without <c>C:</c>, sorted by ordinal comparison.</summary>
        public IReadOnlyList<string> CommandNames
        {
            get
            {
                var names = new List<string>();

                foreach (LispSymbol symbol in _defined.Keys)
                {
                    if (symbol.Name.StartsWith("C:", StringComparison.Ordinal) && IsDefined(symbol))
                    {
                        names.Add(symbol.Name.Substring(2));
                    }
                }

                names.Sort(StringComparer.Ordinal);
                return names;
            }
        }

        /// <summary>Writes a value as <c>prin1</c> (quoted) or <c>princ</c> does.</summary>
        /// <param name="value">The value: nil is null.</param>
        /// <param name="quoted">True for <c>prin1</c>, which writes a string in quotes with its escapes; false for <c>princ</c>, which writes it as it is.</param>
        /// <returns>The text: <c>1.0</c>, <c>"a\nb"</c>, <c>(1 . 2)</c>, <c>#&lt;SUBR @000001d38254e2a0 CAR&gt;</c>.</returns>
        public static string Print(object value, bool quoted) => LispPrinter.Print(value, quoted);

        /// <summary>
        /// Counts the parentheses still open in text typed so far, to tell the command line whether to wait for more with its
        /// <c>(_&gt;</c> prompt. Parentheses in strings and comments are not counted.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="inString">True when the text ends inside a string, which the prompt shows as <c>("_&gt;</c>.</param>
        /// <returns>The number of <c>(</c> still open, or a negative number once a <c>)</c> had no <c>(</c>.</returns>
        public static int OpenParens(string text, out bool inString)
        {
            inString = false;
            int depth = 0;

            if (text == null)
            {
                return 0;
            }

            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                }
                else if (c == '"')
                {
                    inString = true;
                }
                else if (c == ';')
                {
                    if (i + 1 < text.Length && text[i + 1] == '|')
                    {
                        int end = text.IndexOf("|;", i + 2, StringComparison.Ordinal);
                        i = end < 0 ? text.Length : end + 1;
                    }
                    else
                    {
                        int end = text.IndexOf('\n', i);
                        i = end < 0 ? text.Length : end;
                    }
                }
                else if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;

                    if (depth < 0)
                    {
                        return -1;
                    }
                }

                i++;
            }

            return depth;
        }

        #region Symbols and definitions

        /// <summary>Gets the symbol of an upper-case name, making it when there is none.</summary>
        /// <param name="name">The name, already in upper case.</param>
        /// <returns>The symbol.</returns>
        internal LispSymbol Intern(string name)
        {
            if (!_symbols.TryGetValue(name, out LispSymbol symbol))
            {
                symbol = new LispSymbol(name);
                _symbols.Add(name, symbol);
            }

            return symbol;
        }

        private bool IsDefined(LispSymbol symbol) =>
            symbol.Value != null
            && _defined.TryGetValue(symbol, out object made)
            && ReferenceEquals(made, symbol.Value);

        /// <summary>Defines an ordinary built-in function.</summary>
        /// <param name="name">The name, in upper case.</param>
        /// <param name="min">The fewest arguments it takes.</param>
        /// <param name="max">The most it takes, or -1 for any number.</param>
        /// <param name="implementation">The body, given the evaluated arguments.</param>
        /// <returns>The function.</returns>
        internal LispBuiltin Define(string name, int min, int max, Func<object[], object> implementation)
        {
            var function = new LispBuiltin(name, min, max, implementation);
            Intern(name).Value = function;
            return function;
        }

        /// <summary>Defines a special form: a built-in that gets its arguments as they were written.</summary>
        /// <param name="name">The name, in upper case.</param>
        /// <param name="special">The body, given the list of the arguments.</param>
        /// <returns>The function.</returns>
        internal LispBuiltin DefineSpecial(string name, Func<object, object> special)
        {
            var function = new LispBuiltin(name, special);
            Intern(name).Value = function;
            return function;
        }

        /// <summary>Tells the symbol <c>T</c> from nil by a condition.</summary>
        /// <param name="condition">The condition.</param>
        /// <returns><c>T</c> when it holds, nil when not.</returns>
        internal object Bool(bool condition) => condition ? _true : null;

        #endregion

        #region Evaluation

        private static void CheckStack()
        {
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }
            catch (InsufficientExecutionStackException)
            {
                throw new LispException("internal stack limit reached (simulated)", isHard: true);
            }
        }

        /// <summary>Evaluates a form: a symbol gives its value, a list is a call, anything else is itself.</summary>
        /// <param name="form">The form.</param>
        /// <returns>Its value.</returns>
        internal object Eval(object form)
        {
            if (form is LispSymbol symbol)
            {
                return symbol.Value;
            }

            return form is LispCons call ? EvalCall(call) : form;
        }

        private object EvalCall(LispCons call)
        {
            CheckStack();
            object head = call.Car;
            object function;

            if (head is LispSymbol named)
            {
                function = named.Value;
            }
            else if (head is LispCons)
            {
                function = Eval(head);
            }
            else
            {
                function = head;
            }

            if (function is LispBuiltin builtin)
            {
                if (builtin.Special != null)
                {
                    return builtin.Special(call.Cdr);
                }

                object[] values = EvalArguments(call.Cdr);
                CheckArity(builtin, values.Length);
                return builtin.Implementation(values);
            }

            object[] arguments = EvalArguments(call.Cdr);
            LispFunction callable = function as LispFunction ?? (function is LispCons ? AsFunction(function) : null);

            if (callable == null)
            {
                if (function == null)
                {
                    throw new LispException("no function definition: " + (head is LispSymbol s ? s.Name : "nil"));
                }

                throw new LispException("bad function: " + Print(function, true));
            }

            return Apply(callable, arguments);
        }

        private object[] EvalArguments(object list)
        {
            int count = 0;

            for (object cell = list; cell is LispCons c; cell = c.Cdr)
            {
                count++;
            }

            if (count == 0)
            {
                return Array.Empty<object>();
            }

            var values = new object[count];
            int i = 0;

            for (object cell = list; cell is LispCons c; cell = c.Cdr)
            {
                values[i++] = Eval(c.Car);
            }

            return values;
        }

        private static void CheckArity(LispBuiltin builtin, int count)
        {
            if (count < builtin.Min)
            {
                throw new LispException("too few arguments");
            }

            if (builtin.Max >= 0 && count > builtin.Max)
            {
                throw new LispException("too many arguments");
            }
        }

        /// <summary>Calls a function with arguments already evaluated.</summary>
        /// <param name="function">The function.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>What it returns.</returns>
        internal object Apply(LispFunction function, object[] arguments)
        {
            if (function is LispLambda lambda)
            {
                return CallLambda(lambda, arguments);
            }

            var builtin = (LispBuiltin)function;

            if (builtin.Special != null)
            {
                // AutoCAD evaluates the arguments and hands the values to a special form as its forms (so
                // (apply (quote setq) (quote (zz 1))) is a syntax error); quote gives back the whole form.
                if (ReferenceEquals(builtin, _quoteFunction))
                {
                    return new LispCons(_quoteLower, ListOf(arguments));
                }

                var values = new object[arguments.Length];

                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = Eval(arguments[i]);
                }

                return builtin.Special(ListOf(values));
            }

            CheckArity(builtin, arguments.Length);
            return builtin.Implementation(arguments);
        }

        private object CallLambda(LispLambda function, object[] arguments)
        {
            LispSymbol[] parameters = function.Parameters;

            if (arguments.Length < parameters.Length)
            {
                throw new LispException("too few arguments");
            }

            if (arguments.Length > parameters.Length)
            {
                throw new LispException("too many arguments");
            }

            if (++_depth > MaxDepth)
            {
                throw new LispException("internal stack limit reached (simulated)", isHard: true);
            }

            LispSymbol[] bound = function.Bound;
            var saved = new object[bound.Length];

            // The error filter below notes the bindings as an error goes out, and does not catch it: the values are given
            // back only after *error* has run, so that it sees the locals of the function that failed.
            try
            {
                for (int i = 0; i < bound.Length; i++)
                {
                    saved[i] = bound[i].Value;
                }

                for (int i = 0; i < bound.Length; i++)
                {
                    bound[i].Value = i < arguments.Length ? arguments[i] : null;
                }

                object result = null;

                for (object cell = function.Body; cell is LispCons c; cell = c.Cdr)
                {
                    result = Eval(c.Car);
                }

                for (int i = bound.Length - 1; i >= 0; i--)
                {
                    bound[i].Value = saved[i];
                }

                _depth--;
                return result;
            }
            catch (LispException error) when (error.Record(bound, saved))
            {
                throw;
            }
        }

        /// <summary>Gives the variables back the values they had, innermost call first.</summary>
        /// <param name="frames">The bindings an error went out of.</param>
        internal static void Restore(List<LispFrame> frames)
        {
            foreach (LispFrame frame in frames)
            {
                for (int i = frame.Symbols.Length - 1; i >= 0; i--)
                {
                    frame.Symbols[i].Value = frame.Values[i];
                }
            }
        }

        /// <summary>
        /// Makes a function of a value used as one: a function, the wrapper <c>(function (lambda ...))</c> gives, a list
        /// <c>(lambda (args) ...)</c>, or a list <c>((args) ...)</c> as <c>defun-q</c> makes.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The function, or null when the value is not one.</returns>
        /// <exception cref="LispException">Thrown for <c>syntax error</c> when the list is made like a function but its arguments are not.</exception>
        internal LispFunction AsFunction(object value)
        {
            if (value is LispFunction function)
            {
                return function;
            }

            if (!(value is LispCons cons))
            {
                return null;
            }

            if (ReferenceEquals(cons.Car, _quoteLower))
            {
                return cons.Cdr is LispCons wrapped ? wrapped.Car as LispFunction : null;
            }

            if (ReferenceEquals(cons.Car, _lambda))
            {
                return MakeLambda("-lambda-", cons.Cdr, allowEmptyBody: true);
            }

            if (cons.Car == null || cons.Car is LispCons)
            {
                return MakeLambda("-lambda-", cons, allowEmptyBody: true);
            }

            return null;
        }

        /// <summary>
        /// Makes a function from the list that follows <c>lambda</c> or a name in a definition: the argument list, then the
        /// body.
        /// </summary>
        /// <param name="name">The name the function carries: <c>-lambda-</c> or that of the <c>defun</c>.</param>
        /// <param name="definition">The argument list <c>(a b / c)</c> and the forms.</param>
        /// <param name="allowEmptyBody">True for a lambda, which may have no body; a <c>defun</c> may not.</param>
        /// <returns>The function.</returns>
        /// <exception cref="LispException">Thrown for <c>syntax error</c>.</exception>
        internal LispLambda MakeLambda(string name, object definition, bool allowEmptyBody)
        {
            if (
                !(definition is LispCons head)
                || !ParseParameters(head.Car, out LispSymbol[] parameters, out LispSymbol[] locals)
            )
            {
                throw new LispException("syntax error");
            }

            if (!allowEmptyBody && head.Cdr == null)
            {
                throw new LispException("syntax error");
            }

            return new LispLambda(name, parameters, locals, head.Cdr);
        }

        /// <summary>
        /// Reads the argument list of a function: symbols, then <c>/</c> and the local variables. A name twice among the
        /// arguments is refused; a local named like an argument is the argument.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <param name="parameters">The arguments.</param>
        /// <param name="locals">The locals that are not also arguments.</param>
        /// <returns>False when it is not a list of symbols.</returns>
        private static bool ParseParameters(object list, out LispSymbol[] parameters, out LispSymbol[] locals)
        {
            var arguments = new List<LispSymbol>();
            var variables = new List<LispSymbol>();
            bool afterSlash = false;
            parameters = null;
            locals = null;

            object cell = list;

            for (; cell is LispCons c; cell = c.Cdr)
            {
                if (!(c.Car is LispSymbol symbol))
                {
                    return false;
                }

                if (symbol.Name == "/")
                {
                    if (afterSlash)
                    {
                        return false;
                    }

                    afterSlash = true;
                }
                else if (!afterSlash)
                {
                    if (arguments.Contains(symbol))
                    {
                        return false;
                    }

                    arguments.Add(symbol);
                }
                else if (!arguments.Contains(symbol) && !variables.Contains(symbol))
                {
                    variables.Add(symbol);
                }
            }

            if (cell != null)
            {
                return false;
            }

            parameters = arguments.ToArray();
            locals = variables.ToArray();
            return true;
        }

        #endregion

        #region Lists

        /// <summary>Makes a list of the items.</summary>
        /// <param name="items">The items.</param>
        /// <returns>The list; nil for no items.</returns>
        internal static object ListOf(IList<object> items)
        {
            object list = null;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                list = new LispCons(items[i], list);
            }

            return list;
        }

        /// <summary>Takes the items of a list.</summary>
        /// <param name="list">A list or nil.</param>
        /// <returns>The items.</returns>
        /// <exception cref="LispException">Thrown for <c>bad list</c> when the list ends in something that is not nil.</exception>
        internal static List<object> ItemsOf(object list)
        {
            var items = new List<object>();
            object cell = list;

            while (cell is LispCons c)
            {
                items.Add(c.Car);
                cell = c.Cdr;
            }

            if (cell != null)
            {
                throw new LispException("bad list: " + Print(cell, true));
            }

            return items;
        }

        #endregion

        #region Top level

        /// <summary>
        /// Runs a piece of work as one top-level evaluation: on a thread of its own with a large stack while the caller
        /// waits, with the output collected, an error reported the way AutoCAD does, and the value of the work written after
        /// what it wrote.
        /// </summary>
        private LispResult Execute(Func<object> work, bool echo = true)
        {
            return OnWorker(() =>
            {
                bool nested = _worker == Thread.CurrentThread && _topLevelRunning;
                int start = nested ? _output.Length : 0;

                if (!nested)
                {
                    _output.Clear();
                    _depth = 0;
                    _topLevelRunning = true;
                }

                object value = null;
                bool failed = false;
                string error = null;

                try
                {
                    value = work();

                    if (echo && (!(value is LispSymbol symbol) || symbol.Name.Length != 0))
                    {
                        _output.Append(LispPrinter.Print(value, true, display: true));
                    }
                }
                catch (LispException exception)
                {
                    failed = true;
                    error = exception.Message;
                    value = null;
                    Report(exception);
                }
                catch (Exception exception) when (!(exception is ThreadAbortException))
                {
                    failed = true;
                    error = "internal error: " + exception.Message;
                    value = null;
                    _output.Append("; error: ").Append(error);
                }
                finally
                {
                    if (!nested)
                    {
                        _topLevelRunning = false;
                    }
                }

                string text = _output.ToString(start, _output.Length - start);
                return new LispResult(value, text, failed, error);
            });
        }

        private bool _topLevelRunning;

        private T OnWorker<T>(Func<T> work)
        {
            if (_worker == Thread.CurrentThread)
            {
                return work();
            }

            lock (_gate)
            {
                T result = default;
                Exception failure = null;
                var thread = new Thread(
                    () =>
                    {
                        try
                        {
                            result = work();
                        }
                        catch (Exception exception)
                        {
                            failure = exception;
                        }
                    },
                    StackSize
                );

                _worker = thread;

                try
                {
                    thread.Start();
                    thread.Join();
                }
                finally
                {
                    _worker = null;
                }

                if (failure != null)
                {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }

                return result;
            }
        }

        /// <summary>
        /// Writes what AutoCAD writes for an error that reached the top level: <c>*error*</c> is called with the message when
        /// it is a function and writes what it likes; with no <c>*error*</c> the line is <c>; error: message</c>; and the
        /// hard error is two lines that nothing sees. The variables are given back last.
        /// </summary>
        private void Report(LispException exception)
        {
            _depth = 0;

            if (exception.IsHard)
            {
                WriteHardError();
                Restore(exception.Frames);
                return;
            }

            object handler = _error.Value;

            if (handler == null)
            {
                _output.Append("; error: ").Append(exception.Message);
            }
            else
            {
                LispFunction function = null;

                try
                {
                    function = AsFunction(handler);
                }
                catch (LispException)
                {
                    // A handler that is a list made like a function but not one: it is not a function.
                }

                if (function == null)
                {
                    _output.Append("bad function: ").Append(Print(handler, true));
                }
                else
                {
                    try
                    {
                        Apply(function, new object[] { exception.Message });
                    }
                    catch (LispException inner)
                    {
                        if (inner.IsHard)
                        {
                            WriteHardError();
                        }
                        else
                        {
                            _output.Append(inner.Message);
                        }

                        Restore(inner.Frames);
                    }
                }
            }

            _depth = 0;
            Restore(exception.Frames);
        }

        private void WriteHardError()
        {
            if (_output.Length > 0 && _output[_output.Length - 1] != '\n')
            {
                _output.Append('\n');
            }

            _output.Append("Hard error occurred ***\ninternal stack limit reached (simulated)");
        }

        /// <summary>Writes text to the command-line output of the evaluation in progress.</summary>
        /// <param name="text">The text.</param>
        internal void Write(string text) => _output.Append(text);

        #endregion

        #region Load

        private object LoadFile(string name, bool hasFallback, object fallback)
        {
            string extension = ExtensionOf(name);

            if (extension == ".fas" || extension == ".vlx")
            {
                if (hasFallback)
                {
                    return fallback;
                }

                throw new LispException("LOAD failed: " + Print(name, true) + " (compiled AutoLISP is not supported)");
            }

            string path = FindFile(name);
            string text = path == null ? null : ReadText(path);

            if (text == null)
            {
                if (hasFallback)
                {
                    return fallback;
                }

                throw new LispException("LOAD failed: " + Print(name, true));
            }

            var reader = new LispReader(text, Intern, _quote);
            object last = null;

            while (reader.TryRead(out object form))
            {
                last = Eval(form);
            }

            string full = Path.GetFullPath(path);

            if (!_loadedFiles.Exists(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase)))
            {
                _loadedFiles.Add(full);
            }

            return last;
        }

        private static string ExtensionOf(string name)
        {
            try
            {
                return Path.GetExtension(name).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return "";
            }
        }

        private string FindFile(string name)
        {
            try
            {
                string withExtension = Path.HasExtension(name) ? name : name + ".lsp";

                if (name.IndexOfAny(new[] { '/', '\\' }) >= 0 || Path.IsPathRooted(name))
                {
                    return File.Exists(withExtension) ? Path.GetFullPath(withExtension) : null;
                }

                foreach (string folder in _supportFolders)
                {
                    if (string.IsNullOrEmpty(folder))
                    {
                        continue;
                    }

                    string candidate = Path.Combine(folder, withExtension);

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }

            return null;
        }

        /// <summary>Reads a file as UTF-8 when its bytes are UTF-8 (or begin with its byte order mark), and as the system's ANSI code page when not.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The text, or null when the file cannot be read.</returns>
        private static string ReadText(string path)
        {
            byte[] bytes;

            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            int skip = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes, skip, bytes.Length - skip);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes, skip, bytes.Length - skip);
            }
        }

        #endregion
    }
}
