namespace GeometryHelper.Enums
{
    /// <summary>
    /// Which holes <see cref="Geometry.GeoSolid3.TryClose(out Geometry.GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>
    /// fills: none, those it can fill one way only, or every one it may, by the least surface across it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hole is a loop of open edges with nothing across it. A flat one takes one face, and no other; but the two flat
    /// ends of a hole through a body whose walls are missing can be capped, each by a face, or walled round between them:
    /// a plate missing the four walls of a hole through it is closed over by the caps, and round the hole by the walls,
    /// and either is valid. One out of flat can be filled by triangles on its corners in more than one way, and the ways
    /// part in the volume they close: a face of four corners with one of them lifted closes a sixth of the lift times its
    /// area more split along one diagonal than along the other. Two faces of a box meeting along its length, both left
    /// out, leave one loop round the two; on a box three times as long as it is wide and high, the triangles of least area
    /// across it cut the box along its diagonal, and close half its volume.
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
        /// A hole is filled where it can be filled one way only, as far as the volume goes: a flat one by one face, the two
        /// ends of a hole through the body only where the caps or the walls are the one way lying on no face of the body,
        /// and one out of flat by the triangles of least area across it only where every way of filling it with triangles
        /// on its corners closes the same volume within the tolerance; otherwise the report says
        /// <see cref="ClosingFailure.HoleAmbiguous"/>. The default.
        /// </summary>
        WhenUnambiguous,

        /// <summary>
        /// Every hole that may be filled is, the way adding the least area: the two ends of a hole through the body by the
        /// caps or the walls, whichever add less, the caps where they add as much; and one out of flat by the triangles of
        /// least area across it, whatever the other ways of filling it would close.
        /// </summary>
        MinArea,
    }
}
