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
        /// A hole encloses more than <see cref="SolidClosingOptions.MaxHoleArea"/>, or is out of flat with more corners than
        /// a fill out of flat takes, 256; or no hole may be filled: the largest hole is nought, or the strategy is
        /// <see cref="FillStrategy.None"/>.
        /// </summary>
        HoleTooLarge,

        /// <summary>
        /// A hole stands further out of flat than <see cref="SolidClosingOptions.MaxOffFlat"/>.
        /// </summary>
        HoleOffFlat,

        /// <summary>
        /// A hole can be filled more than one way, and the strategy is <see cref="FillStrategy.WhenUnambiguous"/>: one out
        /// of flat by ways closing volumes further apart than its area times the planar tolerance, or the two flat ends of a
        /// hole through the body, whose walls are missing, by caps or by walls, neither lying on a face of the body nor
        /// crossing one. Or, whatever the strategy, a hole holds another inside it where one of the two is out of flat:
        /// triangles are found across a loop with nothing inside it only, and a face takes no hole out of flat.
        /// </summary>
        HoleAmbiguous,

        /// <summary>
        /// The body is still not valid after every step, or could not be worked out, which is logged with what was thrown:
        /// among others, a hole every fill of which would lie back to back with a face of the body, a skin of no thickness,
        /// or cross one, or a face filling a hole before it, an edge of either passing through the inside of the other, the
        /// point then where it would; and a gap the welds would close only through a face standing in it, as a crack round
        /// a blade, at the point where the face welded would pass through.
        /// </summary>
        StillOpen,
    }
}
