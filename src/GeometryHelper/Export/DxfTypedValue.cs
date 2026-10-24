using System;

namespace GeometryHelper.Export
{
    /// <summary>
    /// One group of a DXF file as it stands in the file, as AutoCAD's <c>TypedValue</c> is one value of extended data or of a
    /// result buffer: its code, and its value as the text printed on the line after it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is the line as it is, its spaces kept and nothing read into it: the real of group 10 is its digits, as
    /// <c>"4.8721540113315704"</c>, and the handle of group 5 its hexadecimal, as <c>"1A3"</c>. Where AutoCAD's
    /// <c>TypedValue</c> holds a number as a number, this one holds the text a file writes, so that what is read is written
    /// back to the byte.
    /// </para>
    /// <para>
    /// <c>new DxfTypedValue(1000, "B-12")</c> is a text of extended data, and <c>new DxfTypedValue(70, "4")</c> the group of
    /// the header that says a drawing is in millimetres.
    /// </para>
    /// </remarks>
    public readonly struct DxfTypedValue
    {
        /// <summary>
        /// Initializes a group of a code and the value it is written with, for extended data and the header, as AutoCAD's
        /// <c>TypedValue(typeCode, value)</c>: <c>new DxfTypedValue(1000, "B-12")</c> or <c>new DxfTypedValue(70, "4")</c>.
        /// </summary>
        /// <param name="typeCode">The group code, from -5 to 1071 as the DXF reference numbers them.</param>
        /// <param name="value">The value as it is to be written; a number in the digits it is to be written with.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the code is not one of DXF's.</exception>
        /// <exception cref="ArgumentException">Thrown when the value has a line break in it, which would end its line.</exception>
        public DxfTypedValue(int typeCode, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (typeCode < -5 || typeCode > 1071)
            {
                throw new ArgumentOutOfRangeException(nameof(typeCode), typeCode, "A group code runs from -5 to 1071.");
            }

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("The value of a group is one line, with no line break in it.", nameof(value));
            }

            TypeCode = (short)typeCode;
            Value = value;
        }

        private DxfTypedValue(string value, int typeCode)
        {
            TypeCode = (short)typeCode;
            Value = value;
        }

        /// <summary>A group as a file has it, its code and value taken as they are.</summary>
        internal static DxfTypedValue Read(int code, string value) => new DxfTypedValue(value, code);

        /// <summary>
        /// Gets the group code, as AutoCAD's <c>TypeCode</c>: 0 for the type of a record, 8 for a layer, 10, 20 and 30 for the
        /// coordinates of a first point, 1000 to 1071 for extended data, and so on, as the DXF reference numbers them.
        /// </summary>
        public short TypeCode { get; }

        /// <summary>
        /// Gets the value as the file prints it, a carriage return at the end of its line taken off, as AutoCAD's <c>Value</c>
        /// gives a value: here always the text, the number 2.5 as <c>"2.5"</c>.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the group as the two lines of a file would print it, the code and the value with a space between.
        /// </summary>
        /// <returns>The code, a space, and the value: <c>8 steel</c>.</returns>
        public override string ToString() => TypeCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Value;
    }
}
