namespace GeometryHelper.Enums
{
    /// <summary>
    /// Why <see cref="Geometry.GeoSolid3.TryClose(out Geometry.GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>
    /// could not close a body, or nothing where it did.
    /// </summary>
    /// <remarks>
    /// The reason is that of the first step that could not go on, and <see cref="SolidClosing3.FailureLocation"/> is a
    /// point at the trouble. Where a later step closed what an earlier one could not, the body is closed and there is no
    /// reason.
    /// </remarks>
    public enum ClosingFailure
    {
        /// <summary>
        /// The body was closed, or was valid already.
        /// </summary>
        None,

        /// <summary>
        /// An open loop runs through an edge or a corner more than two faces meet on: a fin standing off the surface, or
        /// two holes touching at a corner. Which faces close with which cannot be told, and nothing is joined across it.
        /// </summary>
        NonManifold,

        /// <summary>
        /// Corners stand further apart than <see cref="SolidClosingOptions.MaxGap"/> across a gap only a weld would close.
        /// </summary>
        GapTooWide,

        /// <summary>
        /// A hole encloses more than <see cref="SolidClosingOptions.MaxHoleArea"/>, or no hole may be filled: the largest
        /// hole is nought, or the strategy is <see cref="FillStrategy.None"/>.
        /// </summary>
        HoleTooLarge,

        /// <summary>
        /// A hole stands further out of flat than <see cref="SolidClosingOptions.MaxOffFlat"/>.
        /// </summary>
        HoleOffFlat,

        /// <summary>
        /// A hole out of flat can be filled more than one way, the ways closing volumes further apart than the tolerance
        /// allows, and the strategy is <see cref="FillStrategy.WhenUnambiguous"/>.
        /// </summary>
        HoleAmbiguous,

        /// <summary>
        /// The body is still not valid after every step, or could not be worked out, which is logged with what was thrown.
        /// </summary>
        StillOpen,
    }
}
