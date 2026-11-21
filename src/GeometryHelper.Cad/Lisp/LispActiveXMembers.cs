using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>What a member of an ActiveX object takes in or gives: how a LISP value is turned into it (D215).</summary>
    internal enum LispAxType
    {
        /// <summary>The value as it is: a variant, a safearray, a symbol, anything.</summary>
        Any,

        /// <summary>A Double: an integer or a real.</summary>
        Double,

        /// <summary>An Int32: an integer.</summary>
        Int,

        /// <summary>A String.</summary>
        String,

        /// <summary>A Boolean: <c>:vlax-true</c> or <c>:vlax-false</c> (and T, nil, -1 and 0).</summary>
        Bool,

        /// <summary>A point: a variant or a safearray of two or three doubles (a list only for the old <c>vlax-put</c> and <c>vlax-invoke</c>); becomes a <c>double[3]</c>.</summary>
        Point,

        /// <summary>A point a call may leave out: nil, or an empty variant, gives null; otherwise as <see cref="Point"/>.</summary>
        OptionalPoint,

        /// <summary>An object: a VLA-OBJECT.</summary>
        Object,

        /// <summary>A symbol the member sets (an out argument): <c>'lo</c>.</summary>
        Symbol,
    }

    /// <summary>
    /// One member of an interface of the object model: a property (read, and write where it has a setter) or a method, the
    /// objects it belongs to and what it does. Members of one name may be many, each for other objects (<c>Name</c> of a
    /// layer, of a block, of a document), and the first that applies to an object is the one used.
    /// </summary>
    internal sealed class LispAxMember
    {
        /// <summary>Gets or sets the name as the reference writes it: <c>Name</c>, <c>GetBoundingBox</c>.</summary>
        internal string Name { get; set; }

        /// <summary>Gets or sets what tells the objects the member belongs to.</summary>
        internal Func<LispVlaObject, bool> AppliesTo { get; set; }

        /// <summary>Gets or sets the read of a property; null for a method.</summary>
        internal Func<LispVlaObject, object> Get { get; set; }

        /// <summary>Gets or sets the write of a property; null for a read-only property or a method.</summary>
        internal Action<LispVlaObject, object> Put { get; set; }

        /// <summary>Gets or sets what a property takes.</summary>
        internal LispAxType PutType { get; set; }

        /// <summary>Gets or sets the body of a method, given its converted arguments; null for a property.</summary>
        internal Func<LispVlaObject, object[], object> Call { get; set; }

        /// <summary>Gets or sets the kinds of the parameters of a method, in order.</summary>
        internal LispAxType[] Parameters { get; set; } = Array.Empty<LispAxType>();

        /// <summary>Gets or sets how many parameters a method has to be given.</summary>
        internal int MinArguments { get; set; }
    }

    /// <summary>
    /// The ActiveX object model of one interpreter: the interfaces and their members (a registry the families add to), the
    /// values handed out for the objects of the drawing, and how a LISP value goes in and out of a member. The families are the
    /// partial files: <c>LispActiveXDocument</c> (application, document, collections, blocks, records, selection sets, colors)
    /// and <c>LispActiveXEntity</c> (the members every entity has); <c>LispDrawing.ActiveX</c> turns the registry into the
    /// <c>vla-</c> and <c>vlax-</c> functions. Everything here that reads or writes the drawing runs on the owner thread.
    /// </summary>
    internal sealed partial class LispActiveX
    {
        private readonly Dictionary<string, List<LispAxMember>> _members = new Dictionary<string, List<LispAxMember>>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly List<string> _order = new List<string>();

        /// <summary>Makes the object model of an interpreter and registers the members of the object families.</summary>
        /// <param name="lisp">The interpreter.</param>
        /// <param name="session">The session whose drawing the objects are.</param>
        /// <param name="state">What the entity functions keep between calls.</param>
        internal LispActiveX(LispInterpreter lisp, CadCommandSession session, LispDrawing.DrawingState state)
        {
            Lisp = lisp;
            Session = session;
            State = state;
            RegisterDocumentMembers();
            RegisterEntityMembers();
            RegisterTypeMembers();
        }

        /// <summary>Registers the members of each kind of entity (a line's StartPoint, a text's TextString); implemented where they are.</summary>
        partial void RegisterTypeMembers();

        /// <summary>Gets the interpreter.</summary>
        internal LispInterpreter Lisp { get; }

        /// <summary>Gets the session.</summary>
        internal CadCommandSession Session { get; }

        /// <summary>Gets what <c>entdel</c> and the entity functions keep between calls.</summary>
        internal LispDrawing.DrawingState State { get; }

        /// <summary>Gets the document of the session.</summary>
        internal CadDocument Document => Session.Document;

        #region The registry

        /// <summary>Gets the names of the members in the order they were registered, each once.</summary>
        internal IReadOnlyList<string> Names => _order;

        /// <summary>Gets the members of a name; none for a name that is no member.</summary>
        internal IReadOnlyList<LispAxMember> MembersOf(string name) =>
            _members.TryGetValue(name, out List<LispAxMember> list)
                ? list
                : (IReadOnlyList<LispAxMember>)Array.Empty<LispAxMember>();

        /// <summary>Registers a member.</summary>
        /// <param name="member">The member.</param>
        internal void Add(LispAxMember member)
        {
            if (!_members.TryGetValue(member.Name, out List<LispAxMember> list))
            {
                list = new List<LispAxMember>();
                _members.Add(member.Name, list);
                _order.Add(member.Name);
            }

            list.Add(member);
        }

        /// <summary>Registers a property: <paramref name="put"/> null makes it read only.</summary>
        internal void Property(
            string name,
            Func<LispVlaObject, bool> appliesTo,
            Func<LispVlaObject, object> get,
            LispAxType putType = LispAxType.Any,
            Action<LispVlaObject, object> put = null
        ) =>
            Add(
                new LispAxMember
                {
                    Name = name,
                    AppliesTo = appliesTo,
                    Get = get,
                    Put = put,
                    PutType = putType,
                }
            );

        /// <summary>Registers a method with the kinds of its parameters, <paramref name="minimum"/> of which have to be given.</summary>
        internal void Method(
            string name,
            Func<LispVlaObject, bool> appliesTo,
            int minimum,
            LispAxType[] parameters,
            Func<LispVlaObject, object[], object> call
        ) =>
            Add(
                new LispAxMember
                {
                    Name = name,
                    AppliesTo = appliesTo,
                    Call = call,
                    Parameters = parameters,
                    MinArguments = minimum,
                }
            );

        /// <summary>A test for the objects of some kinds.</summary>
        internal static Func<LispVlaObject, bool> OfKinds(params LispVlaKind[] kinds) =>
            o => Array.IndexOf(kinds, o.Kind) >= 0;

        /// <summary>A test for the objects of some interfaces, by the name they print with.</summary>
        internal static Func<LispVlaObject, bool> OfInterfaces(params string[] names) =>
            o => Array.IndexOf(names, o.InterfaceName) >= 0;

        /// <summary>The member of a name that applies to an object and suits a use; null where there is none.</summary>
        private LispAxMember Find(LispVlaObject target, string name, Func<LispAxMember, bool> suits)
        {
            foreach (LispAxMember member in MembersOf(name))
            {
                if (suits(member) && member.AppliesTo(target))
                {
                    return member;
                }
            }

            return null;
        }

        /// <summary>Gets whether an object has a property of the name; with <paramref name="writable"/>, one that can be set.</summary>
        internal bool HasProperty(LispVlaObject target, string name, bool writable) =>
            Find(target, name, m => m.Get != null && (!writable || m.Put != null)) != null;

        /// <summary>Gets whether an object has a method of the name.</summary>
        internal bool HasMethod(LispVlaObject target, string name) => Find(target, name, m => m.Call != null) != null;

        #endregion

        #region Dispatch

        /// <summary>The error of a name the object does not have, as the function or the argument spelled it.</summary>
        internal static LispException UnknownName(string name) =>
            new LispException("ActiveX Server returned the error: unknown name: " + name);

        /// <summary>The error of a value no member takes.</summary>
        internal static LispException Mismatch() =>
            new LispException("ActiveX Server returned an error: Type mismatch");

        /// <summary>The error of a value that was released (AutoCAD 2021, GUI g2).</summary>
        internal static LispException Released() =>
            new LispException("null interface pointer: #<VLA-OBJECT 0000000000000000>");

        /// <summary>The error of an object the member cannot be used on any more.</summary>
        internal static LispException Automation(string text) => new LispException("Automation Error. " + text);

        /// <summary>
        /// Reads a property: the value of the member, as a LISP value (a Boolean is <c>:vlax-true</c> or <c>:vlax-false</c>, or
        /// -1 or 0 for the old functions, and a point or an array a variant, or a list for them).
        /// </summary>
        /// <param name="target">The object.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="legacy">True for <c>vlax-get</c>.</param>
        /// <param name="spelled">The name as the caller gave it, for the error.</param>
        /// <returns>The value.</returns>
        internal object GetProperty(LispVlaObject target, string name, bool legacy, string spelled)
        {
            LispAxMember member = Find(target, name, m => m.Get != null) ?? throw UnknownName(spelled);
            EnsureLive(target);
            return ToLisp(member.Get(target), legacy);
        }

        /// <summary>Sets a property to a LISP value, converted as the property takes it (D215).</summary>
        /// <param name="target">The object.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value.</param>
        /// <param name="legacy">True for <c>vlax-put</c>, which takes a list for a point.</param>
        /// <param name="spelled">The name as the caller gave it, for the error.</param>
        internal void PutProperty(LispVlaObject target, string name, object value, bool legacy, string spelled)
        {
            LispAxMember member = Find(target, name, m => m.Get != null) ?? throw UnknownName(spelled);

            if (member.Put == null)
            {
                throw Mismatch();
            }

            EnsureLive(target);

            // vlax-put of a point fails whatever it is given (AutoCAD 2021, GUI g2).
            if (legacy && (member.PutType == LispAxType.Point || member.PutType == LispAxType.OptionalPoint))
            {
                throw new LispException("Exception occurred");
            }

            member.Put(target, FromLisp(value, member.PutType, legacy));
        }

        /// <summary>Calls a method with LISP values, each converted as its parameter takes it.</summary>
        /// <param name="target">The object.</param>
        /// <param name="name">The name of the method.</param>
        /// <param name="arguments">The arguments after the name.</param>
        /// <param name="legacy">True for <c>vlax-invoke</c>.</param>
        /// <param name="spelled">The name as the caller gave it, for the error.</param>
        /// <returns>What the method gives, as a LISP value.</returns>
        internal object Invoke(LispVlaObject target, string name, object[] arguments, bool legacy, string spelled)
        {
            LispAxMember member = Find(target, name, m => m.Call != null) ?? throw UnknownName(spelled);

            if (arguments.Length < member.MinArguments)
            {
                throw new LispException(legacy ? "Invalid number of parameters" : "too few actual parameters");
            }

            if (arguments.Length > member.Parameters.Length)
            {
                throw new LispException(legacy ? "Invalid number of parameters" : "Too many actual parameters");
            }

            EnsureLive(target);
            var converted = new object[arguments.Length];

            for (int i = 0; i < converted.Length; i++)
            {
                converted[i] = FromLisp(arguments[i], member.Parameters[i], legacy);
            }

            return ToLisp(member.Call(target, converted), legacy);
        }

        #endregion

        #region Values

        /// <summary>
        /// A value a member gave, as a LISP value: a Boolean is <c>:vlax-true</c> or <c>:vlax-false</c>, for the old functions -1
        /// or 0, and a variant or a safearray for them a list, nested by dimension.
        /// </summary>
        internal object ToLisp(object value, bool legacy)
        {
            switch (value)
            {
                case bool flag:
                    return legacy ? (object)(flag ? -1 : 0) : Lisp.Intern(flag ? ":VLAX-TRUE" : ":VLAX-FALSE");
                case LispVariant variant when legacy:
                    return ToLisp(variant.Value, true);
                case LispSafeArray array when legacy:
                    return ListOfArray(array);
                default:
                    return value;
            }
        }

        /// <summary>The items of a safearray as a list, nested by dimension; an element that is a variant gives its value.</summary>
        internal object ListOfArray(LispSafeArray array)
        {
            int at = 0;
            return ListOfDimension(array, 0, ref at);
        }

        private object ListOfDimension(LispSafeArray array, int dimension, ref int at)
        {
            int count = Math.Max(0, array.Upper[dimension] - array.Lower[dimension] + 1);
            var items = new List<object>(count);

            for (int i = 0; i < count; i++)
            {
                if (dimension == array.Rank - 1)
                {
                    items.Add(ToLisp(array.Items[at++], true));
                }
                else
                {
                    items.Add(ListOfDimension(array, dimension + 1, ref at));
                }
            }

            return LispInterpreter.ListOf(items);
        }

        /// <summary>
        /// A LISP value as a member takes it (D215): a Double an integer or a real, an Int32 an integer, a String a string, a
        /// Boolean <c>:vlax-true</c> or <c>:vlax-false</c> (T, nil, -1 and 0 too), a point a variant or a safearray of two or
        /// three doubles (a list for the old functions), an object a VLA-OBJECT. A variant stands for what it holds.
        /// </summary>
        internal object FromLisp(object value, LispAxType type, bool legacy)
        {
            if (type == LispAxType.Any)
            {
                return value;
            }

            if (type == LispAxType.Symbol)
            {
                return legacy ? null : value as LispSymbol;
            }

            object held = value is LispVariant variant ? variant.Value : value;

            switch (type)
            {
                case LispAxType.Double:
                    return held is int whole ? whole
                        : held is double real ? real
                        : throw Mismatch();
                case LispAxType.Int:
                    return IntOf(held);
                case LispAxType.String:
                    return held is string text ? text : throw Mismatch();
                case LispAxType.Bool:
                    return BoolOf(held);
                case LispAxType.Object:
                    return held as LispVlaObject ?? throw Mismatch();
                default:
                    if (
                        type == LispAxType.OptionalPoint
                        && (held == null || (value is LispVariant empty && empty.Type <= 1))
                    )
                    {
                        return null;
                    }

                    return PointFrom(value, held, legacy);
            }
        }

        /// <summary>An Int32 takes an integer, a real (rounded half to even) and a numeric string (AutoCAD 2021, GUI g2).</summary>
        internal static int IntOf(object held)
        {
            if (held is int whole)
            {
                return whole;
            }

            double number;

            if (held is double real)
            {
                number = real;
            }
            else if (
                !(held is string text)
                || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            )
            {
                throw Mismatch();
            }

            if (double.IsNaN(number) || Math.Abs(number) > int.MaxValue)
            {
                throw Mismatch();
            }

            return (int)Math.Round(number, MidpointRounding.ToEven);
        }

        private bool BoolOf(object held)
        {
            if (held == null)
            {
                throw new LispException("ActiveX Server returned an error: Parameter not optional");
            }

            if (held is LispSymbol symbol)
            {
                if (ReferenceEquals(symbol, Lisp.Intern(":VLAX-TRUE")) || symbol.Name == "T")
                {
                    return true;
                }

                if (ReferenceEquals(symbol, Lisp.Intern(":VLAX-FALSE")))
                {
                    return false;
                }
            }
            else if (held is int flag && (flag == -1 || flag == 0))
            {
                return flag != 0;
            }

            throw Mismatch();
        }

        private static double[] PointFrom(object value, object held, bool legacy)
        {
            if (held is LispCons)
            {
                if (!legacy)
                {
                    throw new LispException(
                        "lisp value has no coercion to VARIANT with this type:  " + LispInterpreter.Print(value, true)
                    );
                }

                List<object> items = LispInterpreter.ItemsOf(held);
                return PointOfItems(items);
            }

            if (held is LispSafeArray array && array.Rank == 1)
            {
                if (array.Items.Length != 3)
                {
                    throw Automation("Incorrect number of elements in SafeArray");
                }

                return PointOfItems(new List<object>(array.Items));
            }

            throw Mismatch();
        }

        private static double[] PointOfItems(List<object> items)
        {
            if (items.Count != 2 && items.Count != 3)
            {
                throw Mismatch();
            }

            var point = new double[3];

            for (int i = 0; i < items.Count; i++)
            {
                object item = items[i] is LispVariant variant ? variant.Value : items[i];
                point[i] =
                    item is double real ? real
                    : item is int whole ? whole
                    : throw Mismatch();
            }

            return point;
        }

        /// <summary>The value of a variant or a safearray element as a plain LISP value for a filter: a variant its value, an array a list.</summary>
        internal object PlainValue(object value) => ToLisp(value, true);

        /// <summary>Takes an argument that has to be an object: <c>bad argument type: VLA-OBJECT 5</c> for anything else.</summary>
        internal static LispVlaObject ObjectOf(object value)
        {
            if (value is LispVlaObject target)
            {
                return target;
            }

            throw LispInterpreter.TypeError("VLA-OBJECT", value);
        }

        /// <summary>The name a member is given as: a symbol or a string, in any case.</summary>
        internal static string NameOfMember(object value)
        {
            if (value is LispSymbol symbol)
            {
                return symbol.Name;
            }

            return value as string ?? throw LispInterpreter.TypeError("stringp", value);
        }

        /// <summary>A whole number as a LISP integer, a real where it does not fit one.</summary>
        internal static object Whole(long value) =>
            value >= int.MinValue && value <= int.MaxValue ? (object)(int)value : (double)value;

        #endregion
    }
}
