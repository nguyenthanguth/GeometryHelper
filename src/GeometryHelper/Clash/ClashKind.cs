namespace GeometryHelper.Clash
{
    /// <summary>
    /// What two parts found by a clash check do to each other.
    /// </summary>
    public enum ClashKind
    {
        /// <summary>
        /// The two share volume: one runs into the other.
        /// </summary>
        Hard,

        /// <summary>
        /// The two touch without sharing volume: face to face, along an edge or at a corner.
        /// </summary>
        Touch,

        /// <summary>
        /// The two stay apart, but by less than the clearance asked for.
        /// </summary>
        Clearance,

        /// <summary>
        /// Checking the pair failed, so nothing is known of it; the result carries the error.
        /// </summary>
        Unresolved,
    }
}
