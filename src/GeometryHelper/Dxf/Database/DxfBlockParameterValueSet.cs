using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The values a linear parameter of a dynamic block may take, as AutoCAD's Block Editor sets them in its Value Set: from a
    /// minimum to a maximum in steps of an increment, or one of a list. A parameter given none takes any distance.
    /// </summary>
    /// <remarks>
    /// AutoCAD keeps the set in the parameter's flags (96): 1 a minimum, 2 a maximum, 4 an increment, 8 a list. An increment
    /// set is written with the flags 7, a list with 8, as AutoCAD 2021 read them (plugin v17): of the set from 50 to 300 by 10,
    /// a Width set to 75 became 70, 77 became 80 and 400 became 300; of the list 50, 100 ... 300, 75 and 77 became 100 and 400
    /// became 300. Only a list is given back by
    /// <see cref="DxfDynamicBlockReferenceProperty.GetAllowedValues"/>, as AutoCAD gives back only a list.
    /// </remarks>
    public sealed class DxfBlockParameterValueSet
    {
        private readonly double[] _values;

        private DxfBlockParameterValueSet(double minimum, double maximum, double increment, double[] values)
        {
            Minimum = minimum;
            Maximum = maximum;
            Increment = increment;
            _values = values;
        }

        /// <summary>
        /// Makes a set of the distances from a minimum to a maximum in steps of an increment, as AutoCAD's Value Set of the type
        /// Increment: <c>Increments(50, 300, 10)</c> lets a Width of 50, 60 ... 300, a value between steps going to the
        /// nearest step and one beyond an end to that end.
        /// </summary>
        /// <param name="minimum">The least distance, nought or more.</param>
        /// <param name="maximum">The greatest distance, above the minimum.</param>
        /// <param name="increment">The step from the minimum, above nought.</param>
        /// <returns>The set.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is not finite, the minimum is below nought, the
        /// maximum is not above the minimum, or the increment is not above nought.</exception>
        public static DxfBlockParameterValueSet Increments(double minimum, double maximum, double increment)
        {
            Guard.NonNegative(minimum, nameof(minimum), "The minimum of a distance is a finite number of nought or more.");
            Guard.Positive(maximum - minimum, nameof(maximum), "The maximum of a distance is a finite number above its minimum.");
            Guard.Positive(increment, nameof(increment), "The increment of a distance is a finite number above nought.");
            return new DxfBlockParameterValueSet(minimum, maximum, increment, new double[0]);
        }

        /// <summary>
        /// Makes a set of the distances listed, as AutoCAD's Value Set of the type List: <c>List(new[] { 300.0, 50, 100 })</c>
        /// lets a Width of 50, 100 or 300, a value between them going to the nearest. The list is kept from the least up, a
        /// value within the point tolerance of <see cref="Tolerance.Default"/> of the one before it left out.
        /// </summary>
        /// <param name="values">The distances, each above nought.</param>
        /// <returns>The set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the values are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there are none.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not a finite number above nought.</exception>
        public static DxfBlockParameterValueSet List(IEnumerable<double> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var sorted = new List<double>();

            foreach (double value in values)
            {
                Guard.Positive(value, nameof(values), "A distance of a list is a finite number above nought.");
                sorted.Add(value);
            }

            if (sorted.Count == 0)
            {
                throw new ArgumentException("A list of distances holds one at least.", nameof(values));
            }

            sorted.Sort();
            var kept = new List<double> { sorted[0] };

            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] - kept[kept.Count - 1] > Tolerance.Default.EqualPoint)
                {
                    kept.Add(sorted[i]);
                }
            }

            return new DxfBlockParameterValueSet(0.0, 0.0, 0.0, kept.ToArray());
        }

        /// <summary>Gets the least distance of a set of increments; nought for a list.</summary>
        public double Minimum { get; }

        /// <summary>Gets the greatest distance of a set of increments; nought for a list.</summary>
        public double Maximum { get; }

        /// <summary>Gets the step of a set of increments; nought for a list.</summary>
        public double Increment { get; }

        /// <summary>Gets the distances of a list, from the least up; none for a set of increments.</summary>
        public IReadOnlyList<double> Values => Array.AsReadOnly(_values);

        /// <summary>Whether the set is a list, else one of increments.</summary>
        internal bool IsList => _values.Length > 0;

        /// <summary>
        /// Whether a distance is one of the set, within the point tolerance of <see cref="Tolerance.Default"/>: listed, or
        /// between the minimum and the maximum on a step from the minimum.
        /// </summary>
        internal bool Holds(double distance)
        {
            double slack = Tolerance.Default.EqualPoint;

            if (IsList)
            {
                return Array.Exists(_values, v => Math.Abs(v - distance) <= slack);
            }

            if (distance < Minimum - slack || distance > Maximum + slack)
            {
                return false;
            }

            double steps = (distance - Minimum) / Increment;
            return Math.Abs(Math.Round(steps) * Increment - (distance - Minimum)) <= slack;
        }
    }
}
