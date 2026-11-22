using System;
using System.Collections.Generic;
using System.Threading;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// A symbol of an AutoLISP interpreter. Its name is upper case (<c>abc</c>, <c>AbC</c> and <c>ABC</c> are one
    /// symbol), and it has one value cell, which holds a variable's value and a function alike: AutoLISP has no separate
    /// function slot, so <c>(setq car nil)</c> removes CAR. The ones AutoCAD registers (<c>vla-get-Name</c>, <c>acRed</c>) print in
    /// the case it registers them in once <c>(vl-load-com)</c> has run; the symbol is the one the reader finds in any case.
    /// </summary>
    public sealed class LispSymbol
    {
        internal LispSymbol(string name)
        {
            Name = name;
        }

        /// <summary>The name as it prints: upper case for a symbol read from <c>abc</c>, which is named <c>ABC</c>, and the registered case for an ActiveX name.</summary>
        public string Name { get; internal set; }

        /// <summary>The value cell: what the symbol evaluates to, null (nil) when it has none.</summary>
        internal object Value;

        /// <summary>The name.</summary>
        /// <returns>The name of the symbol.</returns>
        public override string ToString() => Name;
    }

    /// <summary>
    /// A cons cell, the pair every list is made of: <c>(1 2 3)</c> is a cell holding 1 whose cdr is the cell holding 2, and
    /// so on to nil, and <c>(1 . 2)</c> is a cell whose cdr is the number 2. A cell never changes once made, so a list
    /// shared by two variables cannot be altered through one of them.
    /// </summary>
    public sealed class LispCons
    {
        /// <summary>Makes a cell.</summary>
        /// <param name="car">What the cell holds.</param>
        /// <param name="cdr">The rest: another cell, nil (null) at the end of a list, or any value for a dotted pair.</param>
        public LispCons(object car, object cdr)
        {
            Car = car;
            Cdr = cdr;
        }

        /// <summary>What the cell holds, the first item of the list.</summary>
        public object Car { get; }

        /// <summary>The rest of the list: another cell, null at the end, or any value for a dotted pair.</summary>
        public object Cdr { get; }
    }

    /// <summary>
    /// A function value, what AutoLISP's <c>type</c> calls a SUBR: a built-in (<c>CAR</c>), a <c>lambda</c>, or one made by
    /// <c>defun</c>. It prints as <c>#&lt;SUBR @000001d38254e2a0 NAME&gt;</c> with an address that is made up and stays the
    /// same for the life of the function.
    /// </summary>
    public abstract class LispFunction
    {
        private static long s_nextAddress = 0x1d38254e000;

        internal LispFunction(string name)
        {
            Name = name;
            Address = Interlocked.Add(ref s_nextAddress, 0xb0);
        }

        /// <summary>The name: <c>CAR</c> for a built-in, the name given to <c>defun</c>, and <c>-lambda-</c> for a lambda.</summary>
        public string Name { get; internal set; }

        /// <summary>The made-up address the function prints with.</summary>
        internal long Address { get; }
    }

    /// <summary>A built-in function: one that gets its arguments evaluated, or a special form that gets them as written.</summary>
    internal sealed class LispBuiltin : LispFunction
    {
        internal LispBuiltin(string name, int min, int max, Func<object[], object> implementation)
            : base(name)
        {
            Min = min;
            Max = max;
            Implementation = implementation;
        }

        internal LispBuiltin(string name, Func<object, object> special)
            : base(name)
        {
            Special = special;
        }

        /// <summary>The fewest arguments the function takes.</summary>
        internal int Min { get; }

        /// <summary>The most arguments it takes, or -1 for any number.</summary>
        internal int Max { get; }

        /// <summary>The body of an ordinary built-in, given its evaluated arguments.</summary>
        internal Func<object[], object> Implementation { get; }

        /// <summary>The body of a special form, given the list of its arguments as they were written; null for an ordinary function.</summary>
        internal Func<object, object> Special { get; }
    }

    /// <summary>A function written in AutoLISP: <c>(lambda (a b / c) ...)</c> or a <c>defun</c>.</summary>
    internal sealed class LispLambda : LispFunction
    {
        internal LispLambda(string name, LispSymbol[] parameters, LispSymbol[] locals, object body)
            : base(name)
        {
            Parameters = parameters;
            Body = body;

            var bound = new LispSymbol[parameters.Length + locals.Length];
            parameters.CopyTo(bound, 0);
            locals.CopyTo(bound, parameters.Length);
            Bound = bound;
        }

        /// <summary>The arguments the function takes, in order.</summary>
        internal LispSymbol[] Parameters { get; }

        /// <summary>The arguments followed by the local variables: every symbol a call binds.</summary>
        internal LispSymbol[] Bound { get; }

        /// <summary>The forms of the body, as a list.</summary>
        internal object Body { get; }
    }

    /// <summary>
    /// The name of an entity in a drawing, what <c>(entlast)</c> and <c>(car (entsel))</c> return. It prints as
    /// <c>&lt;Entity name: 1d21fb9b8d0&gt;</c>, and two names of the same handle are equal.
    /// </summary>
    public sealed class LispEntityName
    {
        /// <summary>Makes the name of the entity with a handle.</summary>
        /// <param name="handle">The handle of the entity, as the drawing writes it.</param>
        public LispEntityName(string handle)
        {
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
        }

        /// <summary>The handle of the entity.</summary>
        public string Handle { get; }
    }

    /// <summary>
    /// A selection set, what <c>(ssget)</c> returns: the handles of the entities picked. It prints as
    /// <c>&lt;Selection set: 5&gt;</c> with a number that is made up.
    /// </summary>
    public sealed class LispSelectionSet
    {
        private static int s_nextNumber;
        private readonly List<string> _handles;

        /// <summary>Makes a selection set.</summary>
        /// <param name="handles">The handles of the entities in it.</param>
        public LispSelectionSet(IEnumerable<string> handles)
        {
            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            _handles = new List<string>(handles);
            Number = Interlocked.Increment(ref s_nextNumber);
        }

        /// <summary>The handles of the entities in the set, in the order they were added.</summary>
        public IReadOnlyList<string> Handles => _handles;

        /// <summary>How the entities of the set were selected, as <c>ssnamex</c> numbers it: 0 by a program, <c>X</c>, Last, Previous or Implied, 1 by a pick, 2 by a window, 3 by a crossing, 4 by a fence.</summary>
        internal int Method { get; set; }

        /// <summary>The corners of the window, crossing or polygon that selected the set, as points of the UCS; null for the other methods.</summary>
        internal IReadOnlyList<double[]> Region { get; set; }

        /// <summary>The point that picked the set, a point of the UCS; null for the other methods.</summary>
        internal double[] PickPoint { get; set; }

        /// <summary>The made-up number the set prints with.</summary>
        internal int Number { get; }

        /// <summary>Adds an entity to the set in place, as <c>ssadd</c> does; one already in it (a handle is compared without regard to case) stays where it was.</summary>
        /// <param name="handle">The handle of the entity.</param>
        /// <returns>True when the entity was added, false when the set held it.</returns>
        internal bool Add(string handle)
        {
            if (Contains(handle))
            {
                return false;
            }

            _handles.Add(handle);
            return true;
        }

        /// <summary>Takes an entity out of the set in place, as <c>ssdel</c> does.</summary>
        /// <param name="handle">The handle of the entity.</param>
        /// <returns>True when the set held it.</returns>
        internal bool Remove(string handle)
        {
            int at = _handles.FindIndex(h => string.Equals(h, handle, StringComparison.OrdinalIgnoreCase));

            if (at < 0)
            {
                return false;
            }

            _handles.RemoveAt(at);
            return true;
        }

        /// <summary>Whether the set holds an entity.</summary>
        /// <param name="handle">The handle of the entity.</param>
        /// <returns>True when it does.</returns>
        internal bool Contains(string handle) =>
            _handles.Exists(h => string.Equals(h, handle, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// What <c>vl-catch-all-apply</c> returns when the function it called failed: the error as a value, so that a routine can
    /// go on. <c>vl-catch-all-error-p</c> tells it from a result, and <c>vl-catch-all-error-message</c> gives the text. It
    /// prints as <c>#&lt;%catch-all-apply-error%&gt;</c>.
    /// </summary>
    public sealed class LispCatchAllError
    {
        internal LispCatchAllError(string message)
        {
            Message = message;
        }

        /// <summary>The text of the error, without the <c>; error: </c> in front: <c>divide by zero</c>.</summary>
        public string Message { get; }
    }

    /// <summary>
    /// An error of an AutoLISP evaluation. <see cref="Exception.Message"/> is the text AutoCAD writes after
    /// <c>; error: </c> (<c>bad argument type: numberp: "a"</c>). A hard error, the stack limit, is the one neither
    /// <c>*error*</c> nor <c>vl-catch-all-apply</c> gets to see.
    /// </summary>
    public sealed class LispException : Exception
    {
        internal LispException(string message, bool isHard = false)
            : base(message)
        {
            IsHard = isHard;
        }

        /// <summary>True for the hard error (<c>internal stack limit reached (simulated)</c>), which nothing in the program can catch.</summary>
        public bool IsHard { get; }

        /// <summary>
        /// The variables the functions this error went out of had bound, innermost first, each with the value it had before. They
        /// are given back after <c>*error*</c> has run, which is why <c>*error*</c> sees the locals of the function that failed.
        /// </summary>
        internal List<LispFrame> Frames { get; } = new List<LispFrame>();

        /// <summary>Notes the variables a function bound as the error goes out of it; always false, so that it can be an exception filter.</summary>
        /// <param name="symbols">The symbols the function bound.</param>
        /// <param name="values">What each held before.</param>
        /// <returns>False, so that the error goes on.</returns>
        internal bool Record(LispSymbol[] symbols, object[] values)
        {
            Frames.Add(new LispFrame(symbols, values));
            return false;
        }
    }

    /// <summary>The variables one call bound and the values they had before, to be given back when the call is over.</summary>
    internal readonly struct LispFrame
    {
        internal LispFrame(LispSymbol[] symbols, object[] values)
        {
            Symbols = symbols;
            Values = values;
        }

        internal LispSymbol[] Symbols { get; }

        internal object[] Values { get; }
    }

    /// <summary>What running AutoLISP text gave: the value, and the text AutoCAD would have put on the command line.</summary>
    public sealed class LispResult
    {
        internal LispResult(object value, string output, bool failed, string error, bool waiting = false)
        {
            Value = value;
            Output = output;
            Failed = failed;
            Error = error;
            IsWaiting = waiting;
        }

        /// <summary>
        /// True when the evaluation is not over but waits for the user (a <c>get</c> function, <c>entsel</c>, <c>ssget</c>), the
        /// text it wrote so far in <see cref="Output"/>; <see cref="LispInterpreter.Resume"/> answers it. The text of the rest comes
        /// with the next result, so that a host writes each piece once.
        /// </summary>
        internal bool IsWaiting { get; }

        /// <summary>The value of the last form, null (nil) when an error ended the run.</summary>
        public object Value { get; }

        /// <summary>
        /// The command-line text, lines separated by <c>\n</c>: what the forms wrote with <c>princ</c>, <c>prin1</c>,
        /// <c>print</c>, <c>prompt</c> and <c>terpri</c>, then the value of the last form in <c>prin1</c> form on the same line
        /// (<c>(princ "a\nb")</c> gives the lines <c>a</c> and <c>b"a\nb"</c>), and nothing for the empty symbol
        /// <c>(princ)</c> returns. An error adds <c>; error: </c> and its text where the writing stopped.
        /// </summary>
        public string Output { get; }

        /// <summary>True when an error reached the top level, after <c>*error*</c> had run.</summary>
        public bool Failed { get; }

        /// <summary>The text of the error, or null when there was none.</summary>
        public string Error { get; }
    }
}
