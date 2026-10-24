namespace GeometryHelper.Export
{
    /// <summary>
    /// Where a text is held by its point, as AutoCAD's <c>AttachmentPoint</c> numbers it: an MTEXT's attachment (group 71), from
    /// <see cref="TopLeft"/> 1 to <see cref="BottomRight"/> 9, and the justification of a TEXT, made of its groups 72 and 73,
    /// from <see cref="BaseLeft"/> 10 on.
    /// </summary>
    public enum DxfAttachmentPoint
    {
        /// <summary>The top left corner, 1: an MTEXT that gives none is held so.</summary>
        TopLeft = 1,

        /// <summary>The middle of the top, 2.</summary>
        TopCenter = 2,

        /// <summary>The top right corner, 3.</summary>
        TopRight = 3,

        /// <summary>The middle of the left side, 4.</summary>
        MiddleLeft = 4,

        /// <summary>The middle, 5.</summary>
        MiddleCenter = 5,

        /// <summary>The middle of the right side, 6.</summary>
        MiddleRight = 6,

        /// <summary>The bottom left corner, 7.</summary>
        BottomLeft = 7,

        /// <summary>The middle of the bottom, 8.</summary>
        BottomCenter = 8,

        /// <summary>The bottom right corner, 9.</summary>
        BottomRight = 9,

        /// <summary>The left end of the baseline, 10: a TEXT with no justification of its own.</summary>
        BaseLeft = 10,

        /// <summary>The middle of the baseline, 11.</summary>
        BaseCenter = 11,

        /// <summary>The right end of the baseline, 12.</summary>
        BaseRight = 12,

        /// <summary>Aligned between its two points on the baseline, 13.</summary>
        BaseAlign = 13,

        /// <summary>Aligned between its two points on the bottom, 14.</summary>
        BottomAlign = 14,

        /// <summary>Aligned between its two points in the middle, 15.</summary>
        MiddleAlign = 15,

        /// <summary>Aligned between its two points on the top, 16.</summary>
        TopAlign = 16,

        /// <summary>Fitted between its two points on the baseline, 17.</summary>
        BaseFit = 17,

        /// <summary>Fitted between its two points on the bottom, 18.</summary>
        BottomFit = 18,

        /// <summary>Fitted between its two points in the middle, 19.</summary>
        MiddleFit = 19,

        /// <summary>Fitted between its two points on the top, 20.</summary>
        TopFit = 20,

        /// <summary>Centred both ways on its point, AutoCAD's Middle, on the baseline, 21.</summary>
        BaseMid = 21,

        /// <summary>Centred both ways on its point, on the bottom, 22.</summary>
        BottomMid = 22,

        /// <summary>Centred both ways on its point, in the middle, 23.</summary>
        MiddleMid = 23,

        /// <summary>Centred both ways on its point, on the top, 24.</summary>
        TopMid = 24,
    }
}
