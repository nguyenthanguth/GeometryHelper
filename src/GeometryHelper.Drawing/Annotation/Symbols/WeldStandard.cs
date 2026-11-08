namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The standard a weld symbol is drawn to. Which the drawings use is open question Q3 of the drawing plan.
    /// </summary>
    public enum WeldStandard
    {
        /// <summary>
        /// ISO 2553, system A: the symbol on the continuous reference line for a weld on the arrow side, on the dashed
        /// identification line for one on the other side; the default.
        /// </summary>
        Iso2553 = 0,

        /// <summary>
        /// AWS A2.4: not drawn yet. Making weld symbols to it throws <see cref="System.NotSupportedException"/> until
        /// question Q3 is answered.
        /// </summary>
        AwsA24 = 1,
    }
}
