namespace GeometryHelper.Export
{
    /// <summary>
    /// What a MULTILEADER's leaders point to, as AutoCAD's <c>ContentType</c> numbers it: group 172 of the MULTILEADER;
    /// <see cref="MTextContent"/> where it gives none.
    /// </summary>
    public enum DxfContentType
    {
        /// <summary>Nothing, 0.</summary>
        NoneContent = 0,

        /// <summary>A block, 1.</summary>
        BlockContent = 1,

        /// <summary>Words of an MTEXT, 2.</summary>
        MTextContent = 2,

        /// <summary>A feature control frame, 3.</summary>
        ToleranceContent = 3,
    }
}
