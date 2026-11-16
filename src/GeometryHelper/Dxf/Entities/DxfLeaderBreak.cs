using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A break in a leader line of a <see cref="DxfMLeader"/>, as AutoCAD 2021's DIMBREAK leaves it and keeps it in the line (90,
    /// 11 and 12 of its LEADER_LINE): the stretch of one segment of the line that is not drawn, from where it starts to where
    /// it ends, both on the segment.
    /// </summary>
    /// <remarks>
    /// A leader line from (0, 0, 0) to (100, -1.25, 0) crossed by a line at x 40 is broken, at the break size 0.125, from
    /// (39.9375, -0.499219, 0) to (40.0625, -0.500781, 0) in segment 0.
    /// </remarks>
    public readonly struct DxfLeaderBreak
    {
        /// <summary>Initializes a new instance of the <see cref="DxfLeaderBreak"/> struct.</summary>
        /// <param name="segment">The segment of the line the break is in, from nought: 0 the first, from the arrow to the next vertex.</param>
        /// <param name="start">Where the break starts, the nearer to the arrow, in the world.</param>
        /// <param name="end">Where the break ends, in the world.</param>
        public DxfLeaderBreak(int segment, GeoPoint3 start, GeoPoint3 end)
        {
            Segment = segment;
            Start = start;
            End = end;
        }

        /// <summary>Gets the segment of the leader line the break is in (90), from nought: 0 the first, from the arrow to the next vertex.</summary>
        public int Segment { get; }

        /// <summary>Gets where the break starts (11), the end nearer the arrow, in the world.</summary>
        public GeoPoint3 Start { get; }

        /// <summary>Gets where the break ends (12), in the world.</summary>
        public GeoPoint3 End { get; }
    }
}
