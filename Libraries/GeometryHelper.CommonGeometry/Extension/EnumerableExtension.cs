using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.CommonGeometry.Extension
{
    /// <summary>
    /// Provides extension methods for the IEnumerable(T) type.
    /// </summary>
    public static class EnumerableExtension
    {
        /// <summary>
        /// Returns the element that yields the maximum value for a selector function.
        /// </summary>
        public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer = null)
        {
            comparer = comparer ?? Comparer<TKey>.Default;
            using IEnumerator<TSource> enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException("Empty sequence");
            }

            TSource maxElement = enumerator.Current;
            TKey maxValue = selector(maxElement);
            while (enumerator.MoveNext())
            {
                TSource currentElement = enumerator.Current;
                TKey currentValue = selector(currentElement);
                if (comparer.Compare(currentValue, maxValue) > 0)
                {
                    maxElement = currentElement;
                    maxValue = currentValue;
                }
            }

            return maxElement;
        }

        /// <summary>
        /// Returns the element that yields the minimum value for a selector function.
        /// </summary>
        public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer = null)
        {
            comparer = comparer ?? Comparer<TKey>.Default;
            using IEnumerator<TSource> enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException("Empty sequence");
            }

            TSource minElement = enumerator.Current;
            TKey minValue = selector(minElement);
            while (enumerator.MoveNext())
            {
                TSource currentElement = enumerator.Current;
                TKey currentValue = selector(currentElement);
                if (comparer.Compare(currentValue, minValue) < 0)
                {
                    minElement = currentElement;
                    minValue = currentValue;
                }
            }

            return minElement;
        }

        /// <summary>
        /// Converts IEnumerator to List.
        /// </summary>
        public static List<T> ToList<T>(this IEnumerator enumerator)
        {
            List<T> result = new List<T>();
            while (enumerator != null && enumerator.MoveNext())
            {
                if (enumerator.Current is T item)
                {
                    result.Add(item);
                }
            }
            return result;
        }
    }
}
