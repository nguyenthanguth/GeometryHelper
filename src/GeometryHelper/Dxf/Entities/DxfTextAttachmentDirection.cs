namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Which sides of a MULTILEADER's words its leaders meet, as AutoCAD's <c>TextAttachmentDirection</c> numbers it: group 271 of
    /// the MULTILEADER; <see cref="AttachmentHorizontal"/> where it gives none.
    /// </summary>
    public enum DxfTextAttachmentDirection
    {
        /// <summary>The left and the right, 0.</summary>
        AttachmentHorizontal = 0,

        /// <summary>The top and the bottom, 1.</summary>
        AttachmentVertical = 1,
    }
}
