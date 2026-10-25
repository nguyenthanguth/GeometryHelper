namespace GeometryHelper.Export
{
    /// <summary>
    /// What a LEADER points to, as AutoCAD's <c>AnnotationType</c> numbers it: group 73 of the LEADER; <see cref="NoAnnotation"/>
    /// where it gives none.
    /// </summary>
    public enum DxfAnnotationType
    {
        /// <summary>An MTEXT, 0.</summary>
        MText = 0,

        /// <summary>A TOLERANCE, a feature control frame, 1.</summary>
        FeatureControlFrame = 1,

        /// <summary>An INSERT, 2.</summary>
        BlockRef = 2,

        /// <summary>Nothing, 3.</summary>
        NoAnnotation = 3,
    }
}
