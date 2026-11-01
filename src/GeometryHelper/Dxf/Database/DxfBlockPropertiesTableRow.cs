using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A row of the block properties table of a dynamic block, AutoCAD's row of its Block Properties Table: a value for each
    /// column of the table, in the order of the columns, a number for a parameter's column, the name of a state for the
    /// visibility's and a text, or null for an empty cell, for an attribute definition's. See
    /// <see cref="DxfBlockTableRecord.AddBlockPropertiesTable"/>.
    /// </summary>
    /// <remarks>
    /// <c>new DxfBlockPropertiesTableRow(200.0, 80.0, "P200")</c>, in a table of the columns Width, Height and the attribute
    /// MARK, sets Width to 200, Height to 80 and the INSERT's attribute MARK to P200 when the table is set to it.
    /// </remarks>
    public sealed class DxfBlockPropertiesTableRow
    {
        private static readonly char[] LineBreaks = { '\r', '\n' };

        private readonly object[] _values;

        /// <summary>Makes a row of a value for each column.</summary>
        /// <param name="values">The value of each column, in the order of the columns: a finite <see cref="double"/> for a
        /// parameter's column, a <see cref="string"/> of one line for the visibility's or an attribute definition's, or null
        /// for an empty cell, which only an attribute definition's column takes
        /// (<see cref="DxfBlockTableRecord.AddBlockPropertiesTable"/> refuses another).</param>
        /// <exception cref="ArgumentNullException">Thrown when the values are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a value is neither null, a finite <see cref="double"/> nor a
        /// <see cref="string"/> of one line.</exception>
        public DxfBlockPropertiesTableRow(params object[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            foreach (object value in values)
            {
                if (value != null && !(value is double number && Guard.IsFinite(number)) && !(value is string text && text.IndexOfAny(LineBreaks) < 0))
                {
                    throw new ArgumentException("A value of a row of a block properties table is a finite double or a string of one line; a " + value.GetType().Name + " " + value + " is not.", nameof(values));
                }
            }

            _values = (object[])values.Clone();
        }

        /// <summary>A row read from a table, its values kept as read, an empty cell as null.</summary>
        private DxfBlockPropertiesTableRow(List<object> values)
        {
            _values = values.ToArray();
        }

        /// <summary>A row read from a table, its values kept as read, an empty cell as null.</summary>
        internal static DxfBlockPropertiesTableRow Read(object[] values) => new DxfBlockPropertiesTableRow(new List<object>(values));

        /// <summary>
        /// Gets the value of each column, in the order of the columns: a <see cref="double"/> or a <see cref="string"/>; null for
        /// a cell AutoCAD left empty in a table read from a drawing.
        /// </summary>
        public IReadOnlyList<object> Values => Array.AsReadOnly(_values);
    }
}
