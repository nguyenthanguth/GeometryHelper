using System;
using System.Collections.Generic;

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
        /// What a member that needs a finite point says to one that is not.
        /// </summary>
        internal const string NotFinitePoint = "The point has a coordinate that is not a finite number.";

        /// <summary>
        /// What a shape whose constructor refuses a point that is not finite says to a vector that would move it to one.
        /// </summary>
        internal const string NotFiniteVector =
            "The vector has to be finite: the shape cannot have a point that is not.";

        /// <summary>
        /// What a shape whose constructor refuses a point that is not finite says to a transformation that would carry it
        /// to one.
        /// </summary>
        internal const string NotFiniteTransform =
            "The transformation has to be a matrix of finite numbers: the shape cannot have a point that is not.";

        /// <summary>
        /// What a shape with a coordinate that is not a finite number says when asked to be made into another shape.
        /// </summary>
        internal const string NotFiniteShape = "The shape has a coordinate that is not a finite number.";

        /// <summary>
        /// Refuses to work on a shape of which a coordinate is not a finite number: the fault is the shape's, not an
        /// argument's, so it is not named as one.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the shape is not finite.</exception>
        internal static void FiniteShape(bool finite)
        {
            if (!finite)
            {
                throw new InvalidOperationException(NotFiniteShape);
            }
        }

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
        /// <remarks>
        /// One comparison, false for NaN as every comparison with it is, and for either infinity: the same answer as
        /// <see cref="double.IsNaN"/> and <see cref="double.IsInfinity"/> together, which .NET Framework does not inline.
        /// </remarks>
        internal static bool IsFinite(double value) => Math.Abs(value) <= double.MaxValue;

        /// <summary>
        /// Determines whether every value is a finite number.
        /// </summary>
        internal static bool AreFinite(IReadOnlyList<double> values)
        {
            if (values is double[] array)
            {
                // The lists of the shapes are arrays: read straight, not through the interface.
                foreach (double value in array)
                {
                    if (!IsFinite(value))
                    {
                        return false;
                    }
                }

                return true;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (!IsFinite(values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether every point is a valid one, both its coordinates finite numbers.
        /// </summary>
        internal static bool AreValid(IReadOnlyList<Geometry.GeoPoint2> points)
        {
            if (points is Geometry.GeoPoint2[] array)
            {
                // The vertices of the shapes are arrays: read straight, not through the interface.
                foreach (Geometry.GeoPoint2 point in array)
                {
                    if (!point.IsValid)
                    {
                        return false;
                    }
                }

                return true;
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (!points[i].IsValid)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether every point is a valid one, its three coordinates finite numbers.
        /// </summary>
        internal static bool AreValid(IReadOnlyList<Geometry.GeoPoint3> points)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (!points[i].IsValid)
                {
                    return false;
                }
            }

            return true;
        }

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

        /// <summary>
        /// Refuses an argument of which a condition does not hold.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the condition is false.</exception>
        internal static void Argument(bool holds, string name, string message)
        {
            if (!holds)
            {
                throw new ArgumentException(message, name);
            }
        }

        /// <summary>
        /// Refuses a count above a most, worked out in 64 bits so that a product of two counts cannot wrap round past it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is above the most.</exception>
        internal static void AtMost(long value, long most, string name, string message)
        {
            if (value > most)
            {
                throw new ArgumentOutOfRangeException(name, value, message);
            }
        }
    }
}
