using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private void RegisterLists()
        {
            Define("CAR", 1, 1, a => Car(a[0]));
            Define("CDR", 1, 1, a => Cdr(a[0]));

            // cadr, cddr, caar, ... up to four letters, applied from the right: caddr is car of cdr of cdr.
            for (int length = 2; length <= 4; length++)
            {
                for (int bits = 0; bits < 1 << length; bits++)
                {
                    var letters = new char[length];

                    for (int i = 0; i < length; i++)
                    {
                        letters[i] = (bits >> i & 1) == 0 ? 'A' : 'D';
                    }

                    string path = new string(letters);
                    Define("C" + path + "R", 1, 1, a =>
                    {
                        object value = a[0];

                        for (int i = path.Length - 1; i >= 0; i--)
                        {
                            value = path[i] == 'A' ? Car(value) : Cdr(value);
                        }

                        return value;
                    });
                }
            }

            Define("CONS", 2, 2, a => new LispCons(a[0], a[1]));
            Define("LIST", 0, -1, a => ListOf(a));
            Define("APPEND", 0, -1, Append);
            Define("ASSOC", 2, 2, a => Assoc(a[0], a[1]));
            Define("NTH", 2, 2, a => Nth(a[0], a[1]));
            Define("MEMBER", 2, 2, a => Member(a[0], a[1]));
            Define("REVERSE", 1, 1, Reverse);
            Define("LENGTH", 1, 1, Count);
            Define("SUBST", 3, 3, a => Subst(a[0], a[1], a[2]));
            Define("LAST", 1, 1, Last);
            Define("LISTP", 1, 1, a => Bool(a[0] == null || a[0] is LispCons));
            Define("ATOM", 1, 1, a => Bool(!(a[0] is LispCons)));
            Define("VL-CONSP", 1, 1, a => Bool(a[0] is LispCons));
            Define("NULL", 1, 1, a => Bool(a[0] == null));
            Define("NOT", 1, 1, a => Bool(a[0] == null));
            Define("MAPCAR", 2, -1, Mapcar);
            Define("APPLY", 2, 2, a => Apply(Callable(a[0], catchAll: false), ArgumentsOf(a[1])));
        }

        private static object Car(object value)
        {
            if (value == null)
            {
                return null;
            }

            return value is LispCons cons ? cons.Car : throw TypeError("consp", value);
        }

        private static object Cdr(object value)
        {
            if (value == null)
            {
                return null;
            }

            return value is LispCons cons ? cons.Cdr : throw TypeError("consp", value);
        }

        /// <summary>The items of the list an argument is, for a function that takes the arguments of a call as one.</summary>
        private static object[] ArgumentsOf(object list)
        {
            if (list != null && !(list is LispCons))
            {
                throw TypeError("listp", list);
            }

            return ItemsOf(list).ToArray();
        }

        private static List<object> ListArgument(object value)
        {
            if (value != null && !(value is LispCons))
            {
                throw TypeError("listp", value);
            }

            return ItemsOf(value);
        }

        private object Append(object[] arguments)
        {
            object result = null;
            int last = arguments.Length - 1;

            while (last >= 0 && arguments[last] == null)
            {
                last--;
            }

            for (int i = 0; i <= last; i++)
            {
                if (arguments[i] != null && !(arguments[i] is LispCons))
                {
                    throw TypeError("listp", arguments[i]);
                }
            }

            if (last < 0)
            {
                return null;
            }

            // Every list but the last is copied; the last is shared, so nothing is copied twice.
            result = arguments[last];

            for (int i = last - 1; i >= 0; i--)
            {
                List<object> items = ItemsOf(arguments[i]);

                for (int j = items.Count - 1; j >= 0; j--)
                {
                    result = new LispCons(items[j], result);
                }
            }

            return result;
        }

        private object Assoc(object key, object list)
        {
            if (list != null && !(list is LispCons))
            {
                throw TypeError("listp", list);
            }

            for (object cell = list; cell is LispCons c; cell = c.Cdr)
            {
                if (c.Car == null)
                {
                    continue;
                }

                if (!(c.Car is LispCons entry))
                {
                    throw new LispException("bad association list: " + Print(list, true));
                }

                if (Equal(key, entry.Car, 0.0))
                {
                    return entry;
                }
            }

            return null;
        }

        private object Nth(object index, object list)
        {
            int n = Int(index);

            if (n < 0)
            {
                throw new LispException("bad argument value: non-negative: " + Inv(n));
            }

            if (!(list is LispCons cell))
            {
                throw TypeError("consp", list);
            }

            for (int i = 0; i < n; i++)
            {
                object next = cell.Cdr;

                if (next == null)
                {
                    return null;
                }

                cell = next as LispCons ?? throw new LispException("bad list: " + Print(next, true));
            }

            return cell.Car;
        }

        private object Member(object item, object list)
        {
            if (list != null && !(list is LispCons))
            {
                throw TypeError("listp", list);
            }

            object cell = list;

            while (cell != null)
            {
                if (!(cell is LispCons c))
                {
                    throw TypeError("consp", cell);
                }

                if (Equal(item, c.Car, 0.0))
                {
                    return c;
                }

                cell = c.Cdr;
            }

            return null;
        }

        private object Reverse(object[] a)
        {
            object result = null;

            foreach (object item in ListArgument(a[0]))
            {
                result = new LispCons(item, result);
            }

            return result;
        }

        private object Count(object[] a) => ListArgument(a[0]).Count;

        private object Last(object[] a)
        {
            List<object> items = ListArgument(a[0]);
            return items.Count == 0 ? null : items[items.Count - 1];
        }

        /// <summary><c>(subst new old list)</c> replaces each item of the list, at its top level, that equals <c>old</c>.</summary>
        private object Subst(object replacement, object old, object list)
        {
            if (list != null && !(list is LispCons))
            {
                throw TypeError("listp", list);
            }

            var items = new List<object>();
            object cell = list;

            while (cell != null)
            {
                if (!(cell is LispCons c))
                {
                    throw TypeError("consp", cell);
                }

                items.Add(Equal(old, c.Car, 0.0) ? replacement : c.Car);
                cell = c.Cdr;
            }

            return ListOf(items);
        }

        private object Mapcar(object[] a)
        {
            LispFunction function = Callable(a[0], catchAll: false);
            var cells = new object[a.Length - 1];

            for (int i = 0; i < cells.Length; i++)
            {
                if (a[i + 1] != null && !(a[i + 1] is LispCons))
                {
                    throw TypeError("listp", a[i + 1]);
                }

                cells[i] = a[i + 1];
            }

            var results = new List<object>();

            while (true)
            {
                var arguments = new object[cells.Length];

                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] == null)
                    {
                        return ListOf(results);
                    }

                    if (!(cells[i] is LispCons c))
                    {
                        throw new LispException("bad list: " + Print(cells[i], true));
                    }

                    arguments[i] = c.Car;
                    cells[i] = c.Cdr;
                }

                results.Add(Apply(function, arguments));
            }
        }

        /// <summary>
        /// Finds the function a value stands for, as <c>apply</c>, <c>mapcar</c> and <c>vl-catch-all-apply</c> take it: a
        /// symbol is its value. A symbol with no value gives <c>no function definition: nil</c> (the name of the function
        /// slot, which has none), and <c>vl-catch-all-apply</c> says <c>bad function: NAME</c> instead.
        /// </summary>
        /// <param name="value">The symbol, function or list.</param>
        /// <param name="catchAll">True for <c>vl-catch-all-apply</c>.</param>
        /// <returns>The function.</returns>
        internal LispFunction Callable(object value, bool catchAll)
        {
            object resolved = value;

            if (value is LispSymbol symbol)
            {
                resolved = symbol.Value;

                if (resolved == null)
                {
                    throw new LispException(catchAll ? "bad function: " + symbol.Name : "no function definition: nil");
                }
            }
            else if (value == null)
            {
                throw new LispException(catchAll ? "bad function: nil" : "no function definition: nil");
            }

            return AsFunction(resolved) ?? throw new LispException("bad function: " + Print(resolved, true));
        }
    }
}
