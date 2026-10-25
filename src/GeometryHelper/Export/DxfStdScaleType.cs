namespace GeometryHelper.Export
{
    /// <summary>
    /// The standard scale of a plot, as AutoCAD's <c>StdScaleType</c> numbers it (group 75 of a layout's plot settings).
    /// </summary>
    public enum DxfStdScaleType
    {
        /// <summary>Scaled to fit the sheet, 0.</summary>
        ScaleToFit = 0,

        /// <summary>1/128 inch to the foot.</summary>
        StdScale1To128InchIs1ft = 1,

        /// <summary>1/64 inch to the foot.</summary>
        StdScale1To64InchIs1ft = 2,

        /// <summary>1/32 inch to the foot.</summary>
        StdScale1To32InchIs1ft = 3,

        /// <summary>1/16 inch to the foot.</summary>
        StdScale1To16InchIs1ft = 4,

        /// <summary>3/32 inch to the foot.</summary>
        StdScale3To32InchIs1ft = 5,

        /// <summary>1/8 inch to the foot.</summary>
        StdScale1To8InchIs1ft = 6,

        /// <summary>3/16 inch to the foot.</summary>
        StdScale3To16InchIs1ft = 7,

        /// <summary>1/4 inch to the foot.</summary>
        StdScale1To4InchIs1ft = 8,

        /// <summary>3/8 inch to the foot.</summary>
        StdScale3To8InchIs1ft = 9,

        /// <summary>1/2 inch to the foot.</summary>
        StdScale1To2InchIs1ft = 10,

        /// <summary>3/4 inch to the foot.</summary>
        StdScale3To4InchIs1ft = 11,

        /// <summary>An inch to the foot.</summary>
        StdScale1InchIs1ft = 12,

        /// <summary>Three inches to the foot.</summary>
        StdScale3InchIs1ft = 13,

        /// <summary>Six inches to the foot.</summary>
        StdScale6InchIs1ft = 14,

        /// <summary>A foot to the foot.</summary>
        StdScale1ftIs1ft = 15,

        /// <summary>1:1.</summary>
        StdScale1To1 = 16,

        /// <summary>1:2.</summary>
        StdScale1To2 = 17,

        /// <summary>1:4.</summary>
        StdScale1To4 = 18,

        /// <summary>1:5.</summary>
        StdScale1To5 = 19,

        /// <summary>1:8.</summary>
        StdScale1To8 = 20,

        /// <summary>1:10.</summary>
        StdScale1To10 = 21,

        /// <summary>1:16.</summary>
        StdScale1To16 = 22,

        /// <summary>1:20.</summary>
        StdScale1To20 = 23,

        /// <summary>1:30.</summary>
        StdScale1To30 = 24,

        /// <summary>1:40.</summary>
        StdScale1To40 = 25,

        /// <summary>1:50.</summary>
        StdScale1To50 = 26,

        /// <summary>1:100.</summary>
        StdScale1To100 = 27,

        /// <summary>2:1.</summary>
        StdScale2To1 = 28,

        /// <summary>4:1.</summary>
        StdScale4To1 = 29,

        /// <summary>8:1.</summary>
        StdScale8To1 = 30,

        /// <summary>10:1.</summary>
        StdScale10To1 = 31,

        /// <summary>100:1.</summary>
        StdScale100To1 = 32,

        /// <summary>1000:1.</summary>
        StdScale1000To1 = 33,
    }
}
