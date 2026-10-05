namespace GeometryHelper.Enums
{
    /// <summary>
    /// Which holes <see cref="Geometry.GeoSolid3.TryClose(out Geometry.GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>
    /// fills: none, those it can fill one way only, or every one it may, by the least surface across it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hole is a loop of open edges with nothing across it. A flat one takes one face, and no other. One out of flat
    /// can be filled by triangles on its corners in more than one way, and the ways part in the volume they close: a
    /// face of four corners with one of them lifted closes a sixth of the lift times its area more split along one
    /// diagonal than along the other. Two faces of a box meeting along its length, both left out, leave one loop round
    /// the two; on a box three times as long as it is wide and high, the triangles of least area across it cut the box
    /// along its diagonal, and close half its volume.
    /// </para>
    /// <para>
    /// Every hole is held to <see cref="SolidClosingOptions.MaxHoleArea"/> and <see cref="SolidClosingOptions.MaxOffFlat"/>
    /// whatever the strategy.
    /// </para>
    /// </remarks>
    public enum FillStrategy
    {
        /// <summary>
        /// No hole is filled: a body open by one is not closed, and the report says
        /// <see cref="ClosingFailure.HoleTooLarge"/>. Corners are still welded and put on edges.
        /// </summary>
        None,

        /// <summary>
        /// A hole is filled where it can be filled one way only, as far as the volume goes: a flat one by one face, and one
        /// out of flat by the triangles of least area across it only where every way of filling it with triangles on its
        /// corners closes the same volume within the tolerance; otherwise the report says
        /// <see cref="ClosingFailure.HoleAmbiguous"/>. The default.
        /// </summary>
        WhenUnambiguous,

        /// <summary>
        /// Every hole that may be filled is: one out of flat by the triangles of least area across it, whatever the other
        /// ways of filling it would close.
        /// </summary>
        MinArea,
    }
}
