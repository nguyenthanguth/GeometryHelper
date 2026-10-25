namespace GeometryHelper.Export
{
    /// <summary>
    /// Where a leader of a MULTILEADER meets its words, as AutoCAD's <c>TextAttachmentType</c> numbers it: groups 173 (left), 95
    /// (right), 273 (top) and 272 (bottom) of the MULTILEADER.
    /// </summary>
    public enum DxfTextAttachmentType
    {
        /// <summary>The top of the top line, 0.</summary>
        AttachmentTopOfTop = 0,

        /// <summary>The middle of the top line, 1.</summary>
        AttachmentMiddleOfTop = 1,

        /// <summary>The middle of the words, 2.</summary>
        AttachmentMiddle = 2,

        /// <summary>The middle of the bottom line, 3.</summary>
        AttachmentMiddleOfBottom = 3,

        /// <summary>The bottom of the bottom line, 4.</summary>
        AttachmentBottomOfBottom = 4,

        /// <summary>The bottom of the bottom line, which is underlined, 5.</summary>
        AttachmentBottomLine = 5,

        /// <summary>The bottom of the top line, which is underlined, 6.</summary>
        AttachmentBottomOfTopLine = 6,

        /// <summary>The bottom of the top line, 7.</summary>
        AttachmentBottomOfTop = 7,

        /// <summary>The bottom of the top line, every line underlined, 8.</summary>
        AttachmentAllLine = 8,

        /// <summary>The middle, above or below the words, 9.</summary>
        AttachmentCenter = 9,

        /// <summary>The middle, above or below the words, which are lined over or under, 10.</summary>
        AttachmentLinedCenter = 10,
    }
}
