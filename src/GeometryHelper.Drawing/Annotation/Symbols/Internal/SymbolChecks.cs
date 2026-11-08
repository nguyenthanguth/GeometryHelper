using System;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>The argument checks and the sizes the symbols share.</summary>
    internal static class SymbolChecks
    {
        /// <summary>The length of one round of the CENTER and HIDDEN patterns as made: 50.8 and 9.525; CENTER's is taken.</summary>
        private const double CentrePattern = 50.8;

        /// <summary>A text that is neither null nor empty.</summary>
        internal static string Text(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Length == 0)
            {
                throw new ArgumentException("The text cannot be empty.", name);
            }

            return value;
        }

        /// <summary>A finite number above nought.</summary>
        internal static double Positive(double value, string name)
        {
            if (!(value > 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, "The value has to be a finite number above nought.");
            }

            return value;
        }

        /// <summary>A point of finite numbers.</summary>
        internal static Geometry.GeoPoint2 Finite(Geometry.GeoPoint2 point, string name)
        {
            if (double.IsNaN(point.X) || double.IsInfinity(point.X) || double.IsNaN(point.Y) || double.IsInfinity(point.Y))
            {
                throw new ArgumentException("The point has to be of finite numbers.", name);
            }

            return point;
        }

        /// <summary>
        /// The linetype scale that makes one round of CENTER twice the text's height long where it is drawn: about a tenth
        /// for a text 2.5 high on paper.
        /// </summary>
        internal static double PatternScale(double textHeight, ViewPlacement2 placement) => 2.0 * textHeight / placement.Scale / CentrePattern;
    }
}
