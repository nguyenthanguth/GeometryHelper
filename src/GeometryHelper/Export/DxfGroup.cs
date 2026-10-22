namespace GeometryHelper.Export
{
    /// <summary>
    /// One group of a DXF file as it stands in the file: its code, and its value as the text printed on the line after it.
    /// </summary>
    /// <remarks>
    /// The value is the line as it is, its spaces kept and nothing read into it: the real of group 10 is its digits, as
    /// <c>"4.8721540113315704"</c>, and the handle of group 5 its hexadecimal, as <c>"1A3"</c>.
    /// </remarks>
    public readonly struct DxfGroup
    {
        internal DxfGroup(int code, string value)
        {
            Code = code;
            Value = value;
        }

        /// <summary>
        /// Gets the group code: 0 for the type of a record, 8 for a layer, 10, 20 and 30 for the coordinates of a first
        /// point, 1000 to 1071 for extended data, and so on, as the DXF reference numbers them.
        /// </summary>
        public int Code { get; }

        /// <summary>
        /// Gets the value as the file prints it, a carriage return at the end of its line taken off.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the group as the two lines of a file would print it, the code and the value with a space between.
        /// </summary>
        /// <returns>The code, a space, and the value: <c>8 steel</c>.</returns>
        public override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Value;
    }
}
