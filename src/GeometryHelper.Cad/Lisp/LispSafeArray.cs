using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// A safearray of AutoLISP, what <c>vlax-make-safearray</c> makes and what ActiveX gives for a point or a list of objects:
    /// an element type (a VARTYPE: 5 doubles, 8 strings, 9 objects, 12 variants), a lower and an upper bound per dimension and
    /// the elements, the last index running fastest. It prints as <c>#&lt;safearray...&gt;</c>.
    /// </summary>
    internal sealed class LispSafeArray
    {
        /// <summary>Makes an array with every element the empty value of its type.</summary>
        /// <param name="elementType">The VARTYPE of the elements.</param>
        /// <param name="lower">The lower bound of each dimension.</param>
        /// <param name="upper">The upper bound of each dimension; one below the lower makes a dimension with no element.</param>
        internal LispSafeArray(int elementType, int[] lower, int[] upper)
        {
            ElementType = elementType;
            Lower = lower;
            Upper = upper;
            int count = 1;

            for (int i = 0; i < lower.Length; i++)
            {
                count *= Math.Max(0, upper[i] - lower[i] + 1);
            }

            Items = new object[count];
            object empty = EmptyOf(elementType);

            for (int i = 0; i < count; i++)
            {
                Items[i] = empty;
            }
        }

        /// <summary>Gets the VARTYPE of the elements.</summary>
        internal int ElementType { get; }

        /// <summary>Gets the lower bound of each dimension.</summary>
        internal int[] Lower { get; }

        /// <summary>Gets the upper bound of each dimension.</summary>
        internal int[] Upper { get; }

        /// <summary>Gets the number of dimensions.</summary>
        internal int Rank => Lower.Length;

        /// <summary>
        /// Gets the elements, the last index running fastest: an int, a double, a string, a bool, a <see cref="LispVariant"/>, a
        /// <see cref="LispVlaObject"/> or null.
        /// </summary>
        internal object[] Items { get; }

        /// <summary>A one-dimensional array of doubles indexed from 0, as a point is given.</summary>
        /// <param name="values">The values.</param>
        /// <returns>The array.</returns>
        internal static LispSafeArray Doubles(params double[] values)
        {
            var array = new LispSafeArray(5, new[] { 0 }, new[] { values.Length - 1 });

            for (int i = 0; i < values.Length; i++)
            {
                array.Items[i] = values[i];
            }

            return array;
        }

        /// <summary>A one-dimensional array of objects indexed from 0; with none its bounds are 0 and -1.</summary>
        /// <param name="values">The objects.</param>
        /// <returns>The array.</returns>
        internal static LispSafeArray Objects(IReadOnlyList<LispVlaObject> values)
        {
            var array = new LispSafeArray(9, new[] { 0 }, new[] { values.Count - 1 });

            for (int i = 0; i < values.Count; i++)
            {
                array.Items[i] = values[i];
            }

            return array;
        }

        /// <summary>
        /// The value an element starts with (AutoCAD 2021, a3): 0.0 for doubles and singles, 0 for the integer kinds, "" for
        /// strings, false for booleans, an empty variant for variants, null for objects and anything else.
        /// </summary>
        private static object EmptyOf(int elementType)
        {
            switch (elementType)
            {
                case 2:
                case 3:
                    return 0;
                case 4:
                case 5:
                    return 0.0;
                case 8:
                    return "";
                case 11:
                    return false;
                case 12:
                    return new LispVariant(0, null, "");
                default:
                    return null;
            }
        }
    }
}
