using System;
using System.Collections.Generic;
using GeometryHelper;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The filter list of <c>ssget</c>: groups an object has to have, each <c>(code . value)</c> equal to a group of the object
    /// as AutoCAD's list gives it (<see cref="LispEntityData"/>), joined by AND, with the operators of group -4: <c>&lt;AND</c>
    /// to <c>AND&gt;</c>, <c>&lt;OR</c>, <c>&lt;XOR</c>, <c>&lt;NOT</c> around groups, and a relation for the group that
    /// follows it: <c>=</c>, <c>/=</c>, <c>!=</c>, <c>&lt;&gt;</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>,
    /// <c>*</c> (always), <c>&amp;</c> (some bit of the value set) and <c>&amp;=</c> (every bit of it set); for a point one of
    /// them for each coordinate, <c>"&gt;,&gt;,*"</c>. A string is a pattern as <c>wcmatch</c> takes it, case ignored for the
    /// names (group 0, 2, 3, 6, 7, 8 and 410); a number is equal within the point tolerance. A group an object lacks is its
    /// default (colour 256, linetype BYLAYER, lineweight -1, thickness 0): the groups AutoCAD's list always has are there.
    /// </summary>
    internal sealed class LispSelectionFilter
    {
        private readonly List<KeyValuePair<int, object>> _items;
        private readonly Tolerance _tolerance;

        private LispSelectionFilter(List<KeyValuePair<int, object>> items, Tolerance tolerance)
        {
            _items = items;
            _tolerance = tolerance;
            OnlyType = items.TrueForAll(i => i.Key == 0 || i.Key == -4);
        }

        /// <summary>Whether the filter looks at the type of an object alone, so that its groups need not be read.</summary>
        internal bool OnlyType { get; }

        /// <summary>Reads a filter list; false for a list that is no list of groups. Nil is a filter that takes every object.</summary>
        internal static bool TryParse(object list, Tolerance tolerance, out LispSelectionFilter filter)
        {
            filter = null;

            if (list == null)
            {
                filter = new LispSelectionFilter(new List<KeyValuePair<int, object>>(), tolerance);
                return true;
            }

            if (!LispEntityData.TryRead(list, out List<KeyValuePair<int, object>> items))
            {
                return false;
            }

            filter = new LispSelectionFilter(items, tolerance);
            return true;
        }

        /// <summary>Whether an object passes the filter.</summary>
        /// <param name="groups">The groups of the object, <c>(0 . type)</c> first, then AutoCAD's list.</param>
        /// <param name="applications">The applications that have extended data on the object.</param>
        internal bool Accepts(IReadOnlyList<KeyValuePair<int, object>> groups, IReadOnlyCollection<string> applications)
        {
            int at = 0;
            bool all = true;

            while (at < _items.Count)
            {
                all &= One(ref at, groups, applications);
            }

            return all;
        }

        private bool One(
            ref int at,
            IReadOnlyList<KeyValuePair<int, object>> groups,
            IReadOnlyCollection<string> applications
        )
        {
            KeyValuePair<int, object> item = _items[at++];

            if (item.Key != -4 || !(item.Value is string text))
            {
                return Compare(item, "=", groups, applications);
            }

            string op = text.Trim().ToUpperInvariant();

            switch (op)
            {
                case "<AND":
                case "<OR":
                case "<XOR":
                case "<NOT":
                    string closer = op.Substring(1) + ">";
                    bool value = op == "<AND" || op == "<NOT";

                    while (at < _items.Count && !IsCloser(_items[at], closer))
                    {
                        bool next = One(ref at, groups, applications);
                        value =
                            op == "<OR" ? value || next
                            : op == "<XOR" ? value ^ next
                            : value && next;
                    }

                    if (at < _items.Count)
                    {
                        at++;
                    }

                    return op == "<NOT" ? !value : value;
                case "AND>":
                case "OR>":
                case "XOR>":
                case "NOT>":
                    return true;
                default:
                    return at < _items.Count && Compare(_items[at++], op, groups, applications);
            }
        }

        private static bool IsCloser(KeyValuePair<int, object> item, string closer) =>
            item.Key == -4
            && item.Value is string text
            && string.Equals(text.Trim(), closer, StringComparison.OrdinalIgnoreCase);

        private bool Compare(
            KeyValuePair<int, object> wanted,
            string op,
            IReadOnlyList<KeyValuePair<int, object>> groups,
            IReadOnlyCollection<string> applications
        )
        {
            int code = wanted.Key;

            if (code == -3)
            {
                return HasApplications(wanted.Value, applications);
            }

            if (op == "*")
            {
                return true;
            }

            bool different = op == "/=" || op == "!=" || op == "<>";
            string relation = different ? "=" : op;
            bool any = false;
            bool seen = false;

            foreach (KeyValuePair<int, object> group in groups)
            {
                if (group.Key == code)
                {
                    seen = true;
                    any |= Holds(group.Value, wanted.Value, relation, code);
                }
            }

            if (!seen)
            {
                object fallback = Default(code);

                if (fallback != null)
                {
                    any = Holds(fallback, wanted.Value, relation, code);
                }
            }

            return different ? !any : any;
        }

        private static object Default(int code)
        {
            switch (code)
            {
                case 62:
                    return 256;
                case 6:
                    return "BYLAYER";
                case 370:
                    return -1;
                case 39:
                    return 0.0;
                case 48:
                    return 1.0;
                case 60:
                    return 0;
                default:
                    return null;
            }
        }

        private static bool HasApplications(object wanted, IReadOnlyCollection<string> applications)
        {
            object cell = wanted;
            bool any = false;

            while (cell is LispCons item)
            {
                if (item.Car is string name)
                {
                    any = true;
                    bool found = false;

                    foreach (string application in applications)
                    {
                        found |= LispInterpreter.WildcardMatch(application.ToUpperInvariant(), name.ToUpperInvariant());
                    }

                    if (!found)
                    {
                        return false;
                    }
                }

                cell = item.Cdr;
            }

            return any;
        }

        private static bool IsName(int code) =>
            code == 0 || code == 2 || code == 3 || code == 6 || code == 7 || code == 8 || code == 410;

        /// <summary>Whether a group's value stands in a relation to the value of the filter.</summary>
        private bool Holds(object have, object want, string op, int code)
        {
            if (op == "*")
            {
                return true;
            }

            switch (want)
            {
                case string pattern:
                    if (!(have is string text))
                    {
                        return false;
                    }

                    if (op == "=")
                    {
                        return IsName(code)
                            ? LispInterpreter.WildcardMatch(text.ToUpperInvariant(), pattern.ToUpperInvariant())
                            : LispInterpreter.WildcardMatch(text, pattern);
                    }

                    int order = string.Compare(text, pattern, StringComparison.OrdinalIgnoreCase);
                    return Order(order, op);
                case int whole when have is int found:
                    return op == "&" ? (found & whole) != 0
                        : op == "&=" ? (found & whole) == whole
                        : Order(found.CompareTo(whole), op);
                case int _:
                case double _:
                    if (!(have is int || have is double))
                    {
                        return false;
                    }

                    double haveNumber = Convert.ToDouble(have, System.Globalization.CultureInfo.InvariantCulture);
                    double wantNumber = Convert.ToDouble(want, System.Globalization.CultureInfo.InvariantCulture);
                    return Number(haveNumber, wantNumber, op);
                case double[] point:
                    if (!(have is double[] at))
                    {
                        return false;
                    }

                    string[] each = op.Split(',');

                    for (int i = 0; i < point.Length; i++)
                    {
                        string relation = each.Length == point.Length ? each[i].Trim() : each[0].Trim();

                        if (!Number(i < at.Length ? at[i] : 0.0, point[i], relation))
                        {
                            return false;
                        }
                    }

                    return true;
                case LispEntityName name:
                    return op == "="
                        && have is LispEntityName other
                        && string.Equals(other.Handle, name.Handle, StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        private static bool Order(int order, string op)
        {
            switch (op)
            {
                case "=":
                    return order == 0;
                case "<":
                    return order < 0;
                case "<=":
                    return order <= 0;
                case ">":
                    return order > 0;
                case ">=":
                    return order >= 0;
                default:
                    return false;
            }
        }

        /// <summary>A number in a relation to another, equal within the point tolerance.</summary>
        private bool Number(double have, double want, string op)
        {
            double least = _tolerance.EqualPoint;

            switch (op)
            {
                case "*":
                    return true;
                case "=":
                    return Math.Abs(have - want) <= least;
                case "/=":
                case "!=":
                case "<>":
                    return Math.Abs(have - want) > least;
                case "<":
                    return have < want - least;
                case "<=":
                    return have <= want + least;
                case ">":
                    return have > want + least;
                case ">=":
                    return have >= want - least;
                default:
                    return false;
            }
        }
    }
}
