namespace GeometryHelper.Packing
{
    /// <summary>
    /// The size of a sheet of paper: one of the ISO 216 A series, or a size of its own.
    /// </summary>
    /// <remarks>
    /// The sizes are given short side by long side, in millimetres; <see cref="SheetOrientation"/> decides which of the
    /// two runs across.
    /// </remarks>
    public enum PaperSize
    {
        /// <summary>841 by 1189 mm.</summary>
        A0,

        /// <summary>594 by 841 mm.</summary>
        A1,

        /// <summary>420 by 594 mm.</summary>
        A2,

        /// <summary>297 by 420 mm.</summary>
        A3,

        /// <summary>210 by 297 mm.</summary>
        A4,

        /// <summary>A width and a height of its own, as <see cref="Sheet.Custom(double, double)"/> is given them.</summary>
        Custom,
    }
}
