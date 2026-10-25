namespace GeometryHelper.Export
{
    /// <summary>
    /// What <see cref="DxfMLeader.MoveMLeader"/> moves, as AutoCAD's <c>MoveType</c> numbers it.
    /// </summary>
    public enum DxfMoveType
    {
        /// <summary>Everything: the content, the doglegs and every vertex of the leaders, 0.</summary>
        MoveAllPoints = 0,

        /// <summary>Everything but the first vertex of each leader line, where its arrow is, 1.</summary>
        MoveAllExceptArrowHeaderPoints = 1,

        /// <summary>The content and the doglegs, with the points where the leader lines meet them, 2.</summary>
        MoveContentAndDoglegPoints = 2,
    }
}
