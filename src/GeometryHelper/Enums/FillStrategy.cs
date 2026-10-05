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
    /// area more split along one diagonal than along the other.
    /// </para>
    /// <para>
    /// Only a way none of whose faces lies back to back with a face of the body is a way at all: such a face is a skin of
    /// no thickness. A box 10 by 1 by 1 with its top and front left out, the two faces meeting along its length, has one
    /// loop round the two. The triangles of least area across it, 15.1, cut the box along its diagonal, but each end of
    /// that cut is a triangle lying on the end face it stands in, and is no way. Of the fourteen ways of filling the loop
    /// six lie on no face: four are the two faces again, 20 of area, and close the box whole, 10, and two cut across its
    /// corner, 20.05 of area, and close 8.3.
    /// </para>
    /// <para>
    /// Every hole is held to <see cref="SolidClosingOptions.MaxHoleArea"/> and <see cref="SolidClosingOptions.MaxOffFlat"/>
    /// whatever the strategy, and triangles are found across a loop of no more than 256 corners with nothing inside it.
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
        /// A hole is filled where it can be filled one way only, as far as the volume goes: a flat one by one face; the two
        /// ends of a hole through the body only where the caps or the walls are the one way lying on no face of the body;
        /// and one out of flat by the triangles of least area across it only where the ways of filling it lying on no face
        /// of the body close one volume within its area times the planar tolerance, the uncertainty a face called flat
        /// carries already. Otherwise the report says <see cref="ClosingFailure.HoleAmbiguous"/>, as it does of the long box
        /// above, its ways closing 10 and 8.3. The default.
        /// </summary>
        WhenUnambiguous,

        /// <summary>
        /// Every hole that may be filled is, the way adding the least area of those lying on no face of the body: the two
        /// ends of a hole through the body by the caps or the walls, whichever add less, the caps where they add as much;
        /// and one out of flat by the triangles of least area across it, whatever the other ways would close. The long box
        /// above is filled by its two faces again, whole.
        /// </summary>
        MinArea,
    }
}
