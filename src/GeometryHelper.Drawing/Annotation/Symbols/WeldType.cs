namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The kind of a weld, which decides the basic symbol drawn on the reference line. A weld object gives it in its
    /// attribute "WeldType" by these names, letters of either case the same.
    /// </summary>
    public enum WeldType
    {
        /// <summary>A fillet weld: a right triangle, its upright side on the left.</summary>
        Fillet = 0,

        /// <summary>A square butt weld: two short uprights side by side.</summary>
        SquareButt = 1,

        /// <summary>A single-V butt weld: a V, its point on the reference line.</summary>
        VButt = 2,

        /// <summary>A single-bevel butt weld: half a V, its upright side on the left.</summary>
        BevelButt = 3,
    }
}
