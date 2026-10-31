using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A row of the table of a lookup parameter of a dynamic block, AutoCAD's row of its Lookup Table: the label the lookup
    /// property shows, and the value of each input column of the table, in the order of the columns. See
    /// <see cref="DxfBlockTableRecord.AddLookupParameter"/>.
    /// </summary>
    /// <remarks>
    /// <c>new DxfBlockLookupRow("Large", 200, 80)</c>, in a table of the columns Width and Height, sets Width to 200 and Height
    /// to 80 when the lookup property is set to Large, and an INSERT of those values shows Large.
    /// </remarks>
    public sealed class DxfBlockLookupRow
    {
        private static readonly char[] LineBreaks = { '\r', '\n' };

        private readonly double[] _values;

        /// <summary>Makes a row of a label and the values of the input columns.</summary>
        /// <param name="label">The label the lookup property shows for the row, of one line at least one letter long.</param>
        /// <param name="values">The value of each input column, in the order of the columns, finite numbers.</param>
        /// <exception cref="ArgumentNullException">Thrown when the label or the values are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the label is empty or holds a line break, or a value is not a finite number.</exception>
        public DxfBlockLookupRow(string label, params double[] values)
        {
            if (label == null)
            {
                throw new ArgumentNullException(nameof(label));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (label.Trim().Length == 0 || label.IndexOfAny(LineBreaks) >= 0)
            {
                throw new ArgumentException("A row of a lookup table is labelled by a name of one line at least one letter long.", nameof(label));
            }

            foreach (double value in values)
            {
                if (!Guard.IsFinite(value))
                {
                    throw new ArgumentException("A value of a row of a lookup table is a finite number.", nameof(values));
                }
            }

            Label = label.Trim();
            _values = (double[])values.Clone();
        }

        /// <summary>Gets the label the lookup property shows for the row, its ends trimmed.</summary>
        public string Label { get; }

        /// <summary>Gets the value of each input column, in the order of the columns.</summary>
        public IReadOnlyList<double> Values => Array.AsReadOnly(_values);
    }
}
