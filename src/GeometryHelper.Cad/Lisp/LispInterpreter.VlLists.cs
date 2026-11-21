using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private void RegisterVlLists()
        {
            Define("VL-SORT", 2, 2, a => VlSort(a[0], a[1], indices: false));
            Define("VL-SORT-I", 2, 2, a => VlSort(a[0], a[1], indices: true));
            Define("VL-REMOVE", 2, 2, a => VlRemove(a[0], a[1]));
            Define("VL-REMOVE-IF", 2, 2, a => VlRemoveIf(a[0], a[1], keepMatches: false));
            Define("VL-REMOVE-IF-NOT", 2, 2, a => VlRemoveIf(a[0], a[1], keepMatches: true));
            Define("VL-MEMBER-IF", 2, 2, a => VlMemberIf(a[0], a[1], matches: true));
            Define("VL-MEMBER-IF-NOT", 2, 2, a => VlMemberIf(a[0], a[1], matches: false));
            Define("VL-POSITION", 2, 2, a => VlPosition(a[0], a[1]));
            Define("VL-SOME", 2, -1, a => VlSomeOrEvery(a, every: false));
            Define("VL-EVERY", 2, -1, a => VlSomeOrEvery(a, every: true));
            Define("VL-LIST*", 1, -1, VlListStar);
            Define("VL-LIST-LENGTH", 1, 1, a => VlListLength(a[0]));
        }

        /// <summary>
        /// <c>(vl-sort list f)</c> and <c>(vl-sort-i list f)</c>: a stable merge sort that takes the right item before the
        /// left one only when <c>(f right left)</c> is not nil, so <c>'&lt;</c> sorts up and <c>'&gt;</c> down. The list is
        /// cut with the left half <c>n/2</c> long. <c>vl-sort</c> then drops each integer or symbol that is the same as the
        /// one before it, and each zero real after a zero real, as AutoCAD does, so <c>(vl-sort '(3 1 2 3 1) '&lt;)</c> is
        /// <c>(1 2 3)</c> and <c>(vl-sort '(0.0 0.0) '&lt;)</c> is <c>(0.0)</c>; other reals, strings and lists are kept. <c>vl-sort-i</c> gives the places the items came from, sorting the list read from its end, so equal
        /// items come out last one first: <c>(vl-sort-i '(3 1 2 3 1) '&lt;)</c> is <c>(4 1 2 3 0)</c>.
        /// </summary>
        private object VlSort(object list, object function, bool indices)
        {
            List<object> items = ListArgument(list);
            LispFunction compare = Callable(function, catchAll: true);
            int count = items.Count;
            var order = new int[count];

            // The sort of indices is over the items in the order the sort reads them: as given, or from the end.
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            if (indices)
            {
                items.Reverse();
            }

            var buffer = new int[count];
            MergeSort(items, compare, order, buffer, 0, count);

            var result = new List<object>(count);

            for (int i = 0; i < count; i++)
            {
                if (indices)
                {
                    result.Add(count - 1 - order[i]);
                    continue;
                }

                object item = items[order[i]];

                if (result.Count > 0 && IsDuplicate(result[result.Count - 1], item))
                {
                    continue;
                }

                result.Add(item);
            }

            return ListOf(result);
        }

        /// <summary>
        /// Whether <c>vl-sort</c> drops an item for the one before it: two equal integers, one symbol twice, or a zero real
        /// after a zero real (0.0 and -0.0 alike). AutoCAD shares one 0.0 object, so its test of "the same object" sees every
        /// zero real as the one before; no other real is shared, and an integer 0 beside a 0.0 is a different kind.
        /// </summary>
        private static bool IsDuplicate(object before, object item)
        {
            if (before is double x)
            {
                // Zero is exactly zero here: this is the identity of one object, not a closeness of two lengths.
                return x == 0.0 && item is double y && y == 0.0;
            }

            return before is int a ? item is int b && a == b : before is LispSymbol && ReferenceEquals(before, item);
        }

        private void MergeSort(
            List<object> items,
            LispFunction compare,
            int[] order,
            int[] buffer,
            int start,
            int count
        )
        {
            if (count < 2)
            {
                return;
            }

            int left = count / 2;
            int right = count - left;
            MergeSort(items, compare, order, buffer, start, left);
            MergeSort(items, compare, order, buffer, start + left, right);

            int i = start;
            int j = start + left;
            int leftEnd = start + left;
            int rightEnd = start + count;
            int k = 0;

            while (i < leftEnd && j < rightEnd)
            {
                if (Apply(compare, new[] { items[order[j]], items[order[i]] }) != null)
                {
                    buffer[k++] = order[j++];
                }
                else
                {
                    buffer[k++] = order[i++];
                }
            }

            while (i < leftEnd)
            {
                buffer[k++] = order[i++];
            }

            while (j < rightEnd)
            {
                buffer[k++] = order[j++];
            }

            System.Array.Copy(buffer, 0, order, start, count);
        }

        /// <summary>
        /// Whether two values are the same for <c>vl-position</c> and <c>vl-remove</c>: of the same kind and equal, with no
        /// mixing of an integer and a real (<c>3.0</c> is not in <c>(1 2 3)</c>), strings by case, symbols by identity, lists
        /// item by item under the same rule.
        /// </summary>
        private static bool SameKind(object x, object y)
        {
            while (x is LispCons a && y is LispCons b)
            {
                CheckStack();

                if (!SameKind(a.Car, b.Car))
                {
                    return false;
                }

                x = a.Cdr;
                y = b.Cdr;
            }

            return Eq(x, y);
        }

        private object VlRemove(object item, object list)
        {
            var result = new List<object>();

            foreach (object each in ListArgument(list))
            {
                if (!SameKind(item, each))
                {
                    result.Add(each);
                }
            }

            return ListOf(result);
        }

        private object VlRemoveIf(object function, object list, bool keepMatches)
        {
            List<object> items = ListArgument(list);
            LispFunction test = Callable(function, catchAll: true);
            var result = new List<object>();

            foreach (object each in items)
            {
                if ((Apply(test, new[] { each }) != null) == keepMatches)
                {
                    result.Add(each);
                }
            }

            return ListOf(result);
        }

        /// <summary>
        /// <c>vl-member-if</c> and <c>vl-member-if-not</c>: the tail of the list from the first item the function answers
        /// non-nil (or nil) for. A tail is given as soon as it is found, so <c>(1 . 2)</c> is a result where
        /// <c>(vl-member-if f '(1 . 2))</c> finds 1 first; a list that ends in an atom before a match is <c>bad list</c>.
        /// </summary>
        private object VlMemberIf(object function, object list, bool matches)
        {
            if (list != null && !(list is LispCons))
            {
                throw TypeError("listp", list);
            }

            LispFunction test = Callable(function, catchAll: true);
            object cell = list;

            while (cell != null)
            {
                if (!(cell is LispCons c))
                {
                    throw new LispException("bad list: " + Print(cell, true));
                }

                if ((Apply(test, new[] { c.Car }) != null) == matches)
                {
                    return c;
                }

                cell = c.Cdr;
            }

            return null;
        }

        private object VlPosition(object item, object list)
        {
            if (list != null && !(list is LispCons))
            {
                throw TypeError("listp", list);
            }

            int index = 0;
            object cell = list;

            while (cell != null)
            {
                if (!(cell is LispCons c))
                {
                    throw new LispException("bad list: " + Print(cell, true));
                }

                if (SameKind(item, c.Car))
                {
                    return index;
                }

                index++;
                cell = c.Cdr;
            }

            return null;
        }

        /// <summary>
        /// <c>vl-some</c> and <c>vl-every</c> over one or more lists, stopping at the shortest: the first value of the function
        /// that is not nil (nil when there is none), or T when every value is not nil, nil at the first that is.
        /// </summary>
        private object VlSomeOrEvery(object[] a, bool every)
        {
            var cells = new object[a.Length - 1];

            for (int i = 0; i < cells.Length; i++)
            {
                if (a[i + 1] != null && !(a[i + 1] is LispCons))
                {
                    throw TypeError("listp", a[i + 1]);
                }

                cells[i] = a[i + 1];
            }

            LispFunction function = Callable(a[0], catchAll: true);

            while (true)
            {
                var arguments = new object[cells.Length];

                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] == null)
                    {
                        return every ? _true : null;
                    }

                    if (!(cells[i] is LispCons c))
                    {
                        throw new LispException("bad list: " + Print(cells[i], true));
                    }

                    arguments[i] = c.Car;
                    cells[i] = c.Cdr;
                }

                object value = Apply(function, arguments);

                if (every ? value == null : value != null)
                {
                    return every ? null : value;
                }
            }
        }

        /// <summary><c>(vl-list* 1 2 3)</c> is <c>(1 2 . 3)</c>: the arguments before the last are the items, the last is the tail.</summary>
        private static object VlListStar(object[] a)
        {
            object result = a[a.Length - 1];

            for (int i = a.Length - 2; i >= 0; i--)
            {
                result = new LispCons(a[i], result);
            }

            return result;
        }

        /// <summary><c>(vl-list-length list)</c>: the number of items, nil for a dotted list.</summary>
        private static object VlListLength(object list)
        {
            if (list != null && !(list is LispCons))
            {
                throw TypeError("listp", list);
            }

            int count = 0;
            object cell = list;

            while (cell is LispCons c)
            {
                count++;
                cell = c.Cdr;
            }

            return cell == null ? (object)count : null;
        }
    }
}
