using System;

namespace GeometryHelper
{
    /// <summary>
    /// Argument checks that a value which is not a number cannot slip past.
    /// </summary>
    /// <remarks>
    /// Every comparison with NaN is false, so a check written as "if the radius is below nought, throw" let a
    /// NaN radius through: the negative radius was refused and the NaN one taken, and the shape built from it
    /// then answered NaN or false to every question without a word. These checks state what the value has to
    /// be, so NaN fails them; infinity fails them too, since no size, distance or tolerance is infinite.
    /// </remarks>
    internal static class Guard
    {
        /// <summary>
        /// Refuses a value that is not a finite number of nought or more.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative, NaN or infinite.</exception>
        internal static void NonNegative(double value, string name, string message)
        {
            if (!(value >= 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, message);
            }
        }

        /// <summary>
        /// Refuses a value that is not a finite number above nought.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is nought or less, NaN or infinite.</exception>
        internal static void Positive(double value, string name, string message)
        {
            if (!(value > 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, message);
            }
        }

        /// <summary>
        /// Determines whether a value is a finite number.
        /// </summary>
        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>
        /// Determines whether a vector is of unit length, as a constructor that normalises leaves it.
        /// </summary>
        internal static bool IsUnit(Geometry.GeoVector2 vector) => Math.Abs(vector.Length - 1.0) <= 1E-9;

        /// <summary>
        /// Determines whether a vector is of unit length, as a constructor that normalises leaves it.
        /// </summary>
        internal static bool IsUnit(Geometry.GeoVector3 vector) => Math.Abs(vector.Length - 1.0) <= 1E-9;

        /// <summary>
        /// Refuses a value that is not a finite number.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is NaN or infinite.</exception>
        internal static void Finite(double value, string name, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, message);
            }
        }
    }
}
