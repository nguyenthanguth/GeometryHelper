namespace GeometryHelper.Meshing
{
    /// <summary>
    /// Where a grid stands along one of its axes against the shape it is laid over, measured between the shape's two
    /// furthest corners along that axis.
    /// </summary>
    /// <remarks>
    /// The width of a shape is seldom a whole number of cells, and the alignment says where the cut cells go. A tiler
    /// picks between the two centred ones so that the cut cells at the two ends are not too narrow.
    /// </remarks>
    public enum GridAlignment
    {
        /// <summary>
        /// A cell starts at the shape's first side, and what is left over is cut off at the far side.
        /// </summary>
        Start,

        /// <summary>
        /// A cell ends at the shape's far side, and what is left over is cut off at the first.
        /// </summary>
        End,

        /// <summary>
        /// A cell stands on the middle of the shape, and what is left over is cut off equally at both sides.
        /// </summary>
        CenterCell,

        /// <summary>
        /// A joint stands on the middle of the shape, the line between two cells where there is no joint, and what is
        /// left over is cut off equally at both sides.
        /// </summary>
        CenterJoint,
    }
}
