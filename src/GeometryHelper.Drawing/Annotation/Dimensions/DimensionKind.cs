namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// Which rule made a dimension, which also says how it is drawn: the linear ones as rotated DIMENSIONs along the view's
    /// axes, the cumulative and ordinate ones as ordinate DIMENSIONs.
    /// </summary>
    public enum DimensionKind
    {
        /// <summary>The overall length, width or height of the main part, outermost of its side.</summary>
        Overall = 0,

        /// <summary>
        /// One of a chain of dimensions end to end between the part's ends and the centres of its holes and bolts; equal
        /// neighbours merged into one of "n x p = total" where the settings say so.
        /// </summary>
        Chain = 1,

        /// <summary>
        /// The pitch of a row of holes or bolts, equal pitches merged into one of "n x p = total": the four holes of a row
        /// 100 apart give "3 x 100 = 300".
        /// </summary>
        HoleGroup = 2,

        /// <summary>The distance from the end of a part to the first or last hole of a row through it.</summary>
        HoleEdge = 3,

        /// <summary>
        /// One of a line of dimensions all from the main part's first end, each written where its point is: 0, 100, 2 900
        /// and 3 000 along a beam 3 000 long with holes at 100 and 2 900.
        /// </summary>
        Cumulative = 4,

        /// <summary>One of a line of dimensions all from the main part's work point, its start, wherever that lies.</summary>
        Ordinate = 5,
    }
}
