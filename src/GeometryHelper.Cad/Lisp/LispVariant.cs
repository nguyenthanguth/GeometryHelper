using System.Globalization;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// A variant of AutoLISP, what <c>vlax-make-variant</c> makes and what ActiveX gives for a point: a VARTYPE (0 empty, 2 and
    /// 3 integers, 5 a double, 8 a string, 11 a boolean, 8192 plus the element type an array) and a value. It prints as
    /// <c>#&lt;variant 3 5&gt;</c>, the text after the type being the value as a string, and <c>...</c> for an array (AutoCAD
    /// 2021, a2).
    /// </summary>
    internal sealed class LispVariant
    {
        /// <summary>Makes a variant.</summary>
        /// <param name="type">The VARTYPE.</param>
        /// <param name="value">An int, a double, a string, a bool, a <see cref="LispSafeArray"/>, a <see cref="LispVlaObject"/> or null.</param>
        /// <param name="text">What the print form shows after the type.</param>
        internal LispVariant(int type, object value, string text)
        {
            Type = type;
            Value = value;
            PrintForm = "#<variant " + type.ToString(CultureInfo.InvariantCulture) + " " + text + ">";
        }

        /// <summary>Gets the VARTYPE.</summary>
        internal int Type { get; }

        /// <summary>Gets the value: an int, a double, a string, a bool, a <see cref="LispSafeArray"/>, a <see cref="LispVlaObject"/> or null.</summary>
        internal object Value { get; }

        /// <summary>Gets how prin1 and princ write it: <c>#&lt;variant 8 abc&gt;</c>.</summary>
        internal string PrintForm { get; }

        /// <summary>A variant of an array: its type is 8192 plus the element type, and it prints with <c>...</c>.</summary>
        /// <param name="array">The array.</param>
        /// <returns>The variant.</returns>
        internal static LispVariant OfArray(LispSafeArray array) =>
            new LispVariant(8192 + array.ElementType, array, "...");

        /// <summary>A point as ActiveX gives it: a variant 8197 of three doubles.</summary>
        /// <param name="x">X.</param>
        /// <param name="y">Y.</param>
        /// <param name="z">Z.</param>
        /// <returns>The variant.</returns>
        internal static LispVariant Point(double x, double y, double z) => OfArray(LispSafeArray.Doubles(x, y, z));
    }
}
