namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Which way the lines of an MTEXT follow one another, as AutoCAD's <c>FlowDirection</c> numbers it: group 72 of the MTEXT,
    /// <see cref="LeftToRight"/> where it gives none.
    /// </summary>
    public enum DxfFlowDirection
    {
        /// <summary>Not set, 0.</summary>
        NotSet = 0,

        /// <summary>Left to right, 1: what an MTEXT that gives none has.</summary>
        LeftToRight = 1,

        /// <summary>Right to left, 2.</summary>
        RightToLeft = 2,

        /// <summary>Top to bottom, 3.</summary>
        TopToBottom = 3,

        /// <summary>Bottom to top, 4.</summary>
        BottomToTop = 4,

        /// <summary>As the text style says, 5.</summary>
        ByStyle = 5,
    }
}
