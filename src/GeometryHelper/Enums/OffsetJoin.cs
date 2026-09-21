namespace GeometryHelper.Enums
{
    /// <summary>
    /// Says how an offset closes the gap that opens at a corner, where the two offset edges pull apart.
    /// <para>
    /// A gap opens on the outside of a turn: at a convex corner when a region grows, at a concave corner when
    /// it shrinks. On the inside of a turn the offset edges overlap instead and are simply cut where they
    /// meet, whatever the join. The three kinds match AutoCAD's offset gap types (OFFSETGAPTYPE 0, 1 and 2).
    /// </para>
    /// </summary>
    public enum OffsetJoin
    {
        /// <summary>
        /// Extends both offset edges until they meet, keeping the corner sharp. A very acute corner would send
        /// the point far out, so <see cref="OffsetOptions.MiterLimit"/> cuts it off square beyond a set
        /// distance. AutoCAD calls this Extend.
        /// </summary>
        Miter,

        /// <summary>
        /// Rounds the corner with an arc centred on the original vertex, of radius equal to the offset
        /// distance, drawn as straight segments within <see cref="OffsetOptions.ArcTolerance"/>. AutoCAD calls
        /// this Fillet.
        /// </summary>
        Round,

        /// <summary>
        /// Cuts the corner with one straight segment lying at the offset distance from the original vertex,
        /// tangent to the arc a <see cref="Round"/> join would draw. AutoCAD calls this Chamfer.
        /// </summary>
        Chamfer
    }
}
