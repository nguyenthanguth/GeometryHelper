namespace GeometryHelper.Export
{
    /// <summary>
    /// The standard scale of a viewport, as AutoCAD's <c>StandardScaleType</c> numbers it: the paper's units to the
    /// drawing's, 1:50 for a plan of millimetres shown at a fiftieth; <see cref="CustomScale"/> for any other.
    /// </summary>
    public enum DxfStandardScaleType
    {
        /// <summary>Scaled to fit the viewport, 0.</summary>
        ScaleToFit = 0,

        /// <summary>A scale of its own, 1.</summary>
        CustomScale = 1,

        /// <summary>1:1.</summary>
        Scale1To1 = 2,

        /// <summary>1:2.</summary>
        Scale1To2 = 3,

        /// <summary>1:4.</summary>
        Scale1To4 = 4,

        /// <summary>1:5.</summary>
        Scale1To5 = 5,

        /// <summary>1:8.</summary>
        Scale1To8 = 6,

        /// <summary>1:10.</summary>
        Scale1To10 = 7,

        /// <summary>1:16.</summary>
        Scale1To16 = 8,

        /// <summary>1:20.</summary>
        Scale1To20 = 9,

        /// <summary>1:30.</summary>
        Scale1To30 = 10,

        /// <summary>1:40.</summary>
        Scale1To40 = 11,

        /// <summary>1:50.</summary>
        Scale1To50 = 12,

        /// <summary>1:100.</summary>
        Scale1To100 = 13,

        /// <summary>2:1.</summary>
        Scale2To1 = 14,

        /// <summary>4:1.</summary>
        Scale4To1 = 15,

        /// <summary>8:1.</summary>
        Scale8To1 = 16,

        /// <summary>10:1.</summary>
        Scale10To1 = 17,

        /// <summary>100:1.</summary>
        Scale100To1 = 18,

        /// <summary>1/128 inch to the foot.</summary>
        Scale1To128inAnd1ft = 19,

        /// <summary>1/64 inch to the foot.</summary>
        Scale1To64inchAnd1ft = 20,

        /// <summary>1/32 inch to the foot.</summary>
        Scale1To32inchAnd1ft = 21,

        /// <summary>1/16 inch to the foot.</summary>
        Scale1To16inchAnd1ft = 22,

        /// <summary>3/32 inch to the foot.</summary>
        Scale3To32inchAnd1ft = 23,

        /// <summary>1/8 inch to the foot.</summary>
        Scale1To8inchAnd1ft = 24,

        /// <summary>3/16 inch to the foot.</summary>
        Scale3To16inchAnd1ft = 25,

        /// <summary>1/4 inch to the foot.</summary>
        Scale1To4inchAnd1ft = 26,

        /// <summary>3/8 inch to the foot.</summary>
        Scale3To8inchAnd1ft = 27,

        /// <summary>1/2 inch to the foot.</summary>
        Scale1To2inchAnd1ft = 28,

        /// <summary>3/4 inch to the foot.</summary>
        Scale3To4inchAnd1ft = 29,

        /// <summary>An inch to the foot.</summary>
        Scale1inAnd1ft = 30,

        /// <summary>Three inches to the foot.</summary>
        Scale3inAnd1ft = 32,

        /// <summary>Six inches to the foot.</summary>
        Scale6inAnd1ft = 33,

        /// <summary>A foot to the foot.</summary>
        Scale1ftAnd1ft = 34,
    }
}
