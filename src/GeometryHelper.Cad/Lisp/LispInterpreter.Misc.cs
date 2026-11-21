using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private void RegisterMisc()
        {
            Define("TYPE", 1, 1, a => TypeOf(a[0]));
            Define("BOUNDP", 1, 1, a => Bool(a[0] is LispSymbol s && s.Value != null));
            Define("EQ", 2, 2, a => Bool(Eq(a[0], a[1])));
            Define("EQUAL", 2, 3, a => Bool(Equal(a[0], a[1], a.Length > 2 ? ToDouble(Number(a[2])) : 0.0)));
            Define("SET", 2, 2, Set);
            Define("EVAL", 1, 1, a => Eval(a[0]));
            Define("READ", 1, 1, a => Read(Str(a[0])));
            Define("ATOMS-FAMILY", 1, 2, AtomsFamily);
            Define("DEFUN-Q-LIST-REF", 1, 1, a => a[0] is LispSymbol s ? s.Value as LispCons : null);
            Define("VL-SYMBOL-VALUE", 1, 1, a => a[0] is LispSymbol s ? s.Value : throw TypeError("symbolp", a[0]));
            Define("VL-SYMBOLP", 1, 1, a => Bool(a[0] is LispSymbol));
            Define("DEFUN-Q-LIST-SET", 2, 2, a =>
            {
                LispSymbol symbol = a[0] as LispSymbol ?? throw TypeError("symbolp", a[0]);
                symbol.Value = a[1] as LispCons ?? throw TypeError("listp", a[1]);
                return symbol;
            });
            Define("LOAD", 1, 2, a => LoadFile(Str(a[0]), a.Length > 1, a.Length > 1 ? a[1] : null));
        }

        #region Arguments

        /// <summary>The error for an argument of the wrong kind: <c>bad argument type: consp 1</c>.</summary>
        /// <param name="predicate">The test the function applied: <c>consp</c>, <c>stringp</c>, <c>numberp</c>.</param>
        /// <param name="value">The argument.</param>
        /// <param name="colon">True for the predicates AutoCAD writes with a colon after them (<c>numberp: "a"</c>, <c>fixnump: 3.0</c>).</param>
        /// <returns>The error to throw.</returns>
        internal static LispException TypeError(string predicate, object value, bool colon = false) =>
            new LispException("bad argument type: " + predicate + (colon ? ": " : " ") + Print(value, true));

        /// <summary>Takes an argument that has to be a string.</summary>
        /// <param name="value">The argument.</param>
        /// <returns>The string.</returns>
        internal static string Str(object value) => value as string ?? throw TypeError("stringp", value);

        /// <summary>Takes an argument that has to be an integer.</summary>
        /// <param name="value">The argument.</param>
        /// <returns>The integer.</returns>
        internal static int Int(object value) => value is int i ? i : throw TypeError("fixnump", value, colon: true);

        #endregion

        #region Equality

        /// <summary>
        /// <c>eq</c>: the same object. Numbers and strings count as the same when their values are (AutoCAD's <c>(eq "a" "a")</c>
        /// is T), entity names when their handles are; a list is the same only as itself.
        /// </summary>
        /// <param name="x">One value.</param>
        /// <param name="y">The other.</param>
        /// <returns>True when they are eq.</returns>
        internal static bool Eq(object x, object y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            switch (x)
            {
                case int i:
                    return y is int j && i == j;
                case double d:
                    return y is double e && d == e;
                case string s:
                    return y is string t && string.Equals(s, t, StringComparison.Ordinal);
                case LispEntityName n:
                    return y is LispEntityName m && string.Equals(n.Handle, m.Handle, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        /// <summary>
        /// <c>equal</c>: lists are compared item by item, numbers by value within the fuzz (an int equals the real of the same
        /// value), variants and safearrays by type and elements, and anything else as <c>eq</c>.
        /// </summary>
        /// <param name="x">One value.</param>
        /// <param name="y">The other.</param>
        /// <param name="fuzz">How far apart two numbers may be and still be equal: nought when not given.</param>
        /// <returns>True when they are equal.</returns>
        internal static bool Equal(object x, object y, double fuzz)
        {
            while (true)
            {
                if (x is LispVlaObject vx && y is LispVlaObject vy)
                {
                    return vx.Kind == vy.Kind && string.Equals(vx.Key, vy.Key, StringComparison.Ordinal);
                }

                if (x is LispCons a && y is LispCons b)
                {
                    CheckStack();

                    if (!Equal(a.Car, b.Car, fuzz))
                    {
                        return false;
                    }

                    x = a.Cdr;
                    y = b.Cdr;
                    continue;
                }

                if ((x is int || x is double) && (y is int || y is double))
                {
                    double p = ToDouble(x);
                    double q = ToDouble(y);
                    return p == q || Math.Abs(p - q) <= fuzz;
                }

                if (x is LispVariant v && y is LispVariant w)
                {
                    return LispVariantConversion.Equal(v, w, fuzz);
                }

                if (x is LispSafeArray r && y is LispSafeArray s)
                {
                    return LispVariantConversion.Equal(r, s, fuzz);
                }

                return Eq(x, y);
            }
        }

        #endregion

        private object TypeOf(object value)
        {
            switch (value)
            {
                case int _:
                    return Intern("INT");
                case double _:
                    return Intern("REAL");
                case string _:
                    return Intern("STR");
                case LispSymbol _:
                    return Intern("SYM");
                case LispCons _:
                    return Intern("LIST");
                case LispFunction _:
                    return Intern("SUBR");
                case LispSelectionSet _:
                    return Intern("PICKSET");
                case LispFile _:
                    return Intern("FILE");
                case LispEntityName _:
                    return Intern("ENAME");
                case LispCatchAllError _:
                    return Intern("VL-CATCH-ALL-APPLY-ERROR");
                case LispVariant _:
                    return Intern("VARIANT");
                case LispSafeArray _:
                    return Intern("SAFEARRAY");
                case LispVlaObject _:
                    return Intern("VLA-OBJECT");
                default:
                    return null;
            }
        }

        private static object Set(object[] a)
        {
            if (!(a[0] is LispSymbol symbol))
            {
                throw TypeError("symbolp", a[0]);
            }

            return symbol.Value = a[1];
        }

        /// <summary>
        /// <c>(atoms-family format [names])</c>: with names, the ones that are symbols of the program, the others nil; with none,
        /// every symbol, by name. Format 1 gives the names as strings, any other as symbols.
        /// </summary>
        private object AtomsFamily(object[] a)
        {
            bool strings = Int(a[0]) == 1;
            var result = new List<object>();

            if (a.Length == 1)
            {
                var names = new List<string>(_symbols.Keys);
                names.Sort(StringComparer.Ordinal);

                foreach (string name in names)
                {
                    result.Add(strings ? (object)name : _symbols[name]);
                }

                return ListOf(result);
            }

            foreach (object item in ListArgument(a[1]))
            {
                if (!(item is string wanted))
                {
                    throw new LispException("invalid argument supplied, must be a string. " + Print(item, true));
                }

                if (_symbols.TryGetValue(wanted.ToUpperInvariant(), out LispSymbol found))
                {
                    result.Add(strings ? (object)found.Name : found);
                }
                else
                {
                    result.Add(null);
                }
            }

            return ListOf(result);
        }

        /// <summary>Writes an int the way every message does, whatever the culture of the machine.</summary>
        private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
