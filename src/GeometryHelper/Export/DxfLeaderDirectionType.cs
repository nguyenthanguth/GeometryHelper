namespace GeometryHelper.Export
{
    /// <summary>
    /// Which side of a MULTILEADER's content a leader meets, as AutoCAD's <c>LeaderDirectionType</c> numbers it.
    /// </summary>
    public enum DxfLeaderDirectionType
    {
        /// <summary>Not known, 0.</summary>
        UnknownLeader = 0,

        /// <summary>The left, 1.</summary>
        LeftLeader = 1,

        /// <summary>The right, 2.</summary>
        RightLeader = 2,

        /// <summary>The top, 3.</summary>
        TopLeader = 3,

        /// <summary>The bottom, 4.</summary>
        BottomLeader = 4,
    }
}
