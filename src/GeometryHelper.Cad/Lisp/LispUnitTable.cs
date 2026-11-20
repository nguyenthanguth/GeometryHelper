using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The table <c>cvunit</c> converts by, of our own: lengths (mm, cm, m, km, inch, foot, yard, mile), angles (degree,
    /// radian, grad), areas and volumes (a length with "square" or "cubic" before it), masses (g, kg, lb) and times (second,
    /// minute, hour, day), each by its name or its short form in letters of either case. A unit is a factor to the base of its
    /// kind (millimetre, radian, square millimetre, cubic millimetre, gram, second), so 1 inch is 25.4 and 90 degrees are
    /// 1.5708 radians. A name that is not in the table, or two units of different kinds, give no conversion.
    /// </summary>
    internal static class LispUnitTable
    {
        private enum UnitKind
        {
            Length,
            Angle,
            Area,
            Volume,
            Mass,
            Time,
        }

        private readonly struct Unit
        {
            internal Unit(UnitKind kind, double factor)
            {
                Kind = kind;
                Factor = factor;
            }

            internal UnitKind Kind { get; }

            internal double Factor { get; }
        }

        private static readonly Dictionary<string, Unit> Units = Build();

        /// <summary>
        /// Converts a value from one unit to another.
        /// </summary>
        /// <param name="value">The value, in <paramref name="from"/>.</param>
        /// <param name="from">The unit it is in: "inch", "in", "square foot", "degree".</param>
        /// <param name="to">The unit wanted.</param>
        /// <param name="result">The value in <paramref name="to"/>; nought where there is none.</param>
        /// <returns>False where a unit is not in the table or the two units are of different kinds.</returns>
        internal static bool TryConvert(double value, string from, string to, out double result)
        {
            result = 0.0;

            if (!Units.TryGetValue(Normal(from), out Unit source) || !Units.TryGetValue(Normal(to), out Unit target))
            {
                return false;
            }

            if (source.Kind != target.Kind)
            {
                return false;
            }

            result = value * source.Factor / target.Factor;
            return true;
        }

        private static string Normal(string name) => (name ?? string.Empty).Trim().ToLowerInvariant();

        private static Dictionary<string, Unit> Build()
        {
            var units = new Dictionary<string, Unit>(StringComparer.Ordinal);

            // Lengths, in millimetres; each with its square and its cube.
            var lengths = new (string[] Names, double Millimetres)[]
            {
                (new[] { "mm", "millimeter", "millimeters", "millimetre", "millimetres" }, 1.0),
                (new[] { "cm", "centimeter", "centimeters", "centimetre", "centimetres" }, 10.0),
                (new[] { "m", "meter", "meters", "metre", "metres" }, 1000.0),
                (new[] { "km", "kilometer", "kilometers", "kilometre", "kilometres" }, 1000000.0),
                (new[] { "in", "inch", "inches" }, 25.4),
                (new[] { "ft", "foot", "feet" }, 304.8),
                (new[] { "yd", "yard", "yards" }, 914.4),
                (new[] { "mi", "mile", "miles" }, 1609344.0),
            };

            foreach ((string[] names, double millimetres) in lengths)
            {
                foreach (string name in names)
                {
                    units[name] = new Unit(UnitKind.Length, millimetres);
                    units["square " + name] = new Unit(UnitKind.Area, millimetres * millimetres);
                    units["sq " + name] = new Unit(UnitKind.Area, millimetres * millimetres);
                    units["cubic " + name] = new Unit(UnitKind.Volume, millimetres * millimetres * millimetres);
                    units["cu " + name] = new Unit(UnitKind.Volume, millimetres * millimetres * millimetres);
                }
            }

            Add(units, UnitKind.Angle, Math.PI / 180.0, "degree", "degrees", "deg");
            Add(units, UnitKind.Angle, 1.0, "radian", "radians", "rad");
            Add(units, UnitKind.Angle, Math.PI / 200.0, "grad", "grads", "gradian", "gradians");
            Add(units, UnitKind.Mass, 1.0, "g", "gram", "grams");
            Add(units, UnitKind.Mass, 1000.0, "kg", "kilogram", "kilograms");
            Add(units, UnitKind.Mass, 453.59237, "lb", "lbs", "pound", "pounds");
            Add(units, UnitKind.Time, 1.0, "s", "sec", "second", "seconds");
            Add(units, UnitKind.Time, 60.0, "min", "minute", "minutes");
            Add(units, UnitKind.Time, 3600.0, "h", "hr", "hour", "hours");
            Add(units, UnitKind.Time, 86400.0, "d", "day", "days");
            return units;
        }

        private static void Add(Dictionary<string, Unit> units, UnitKind kind, double factor, params string[] names)
        {
            foreach (string name in names)
            {
                units[name] = new Unit(kind, factor);
            }
        }
    }
}
