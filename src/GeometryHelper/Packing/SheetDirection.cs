namespace GeometryHelper.Packing
{
    /// <summary>
    /// Where each new sheet goes, when the boxes do not all fit on the sheets before it: beside the one before, on this
    /// side of it, so that the sheets run in one line away from the first.
    /// </summary>
    public enum SheetDirection
    {
        /// <summary>To the right, towards greater X. The default.</summary>
        Right,

        /// <summary>To the left, towards smaller X.</summary>
        Left,

        /// <summary>Above, towards greater Y.</summary>
        Top,

        /// <summary>Below, towards smaller Y.</summary>
        Bottom,
    }
}
