namespace GeometryHelper.CommonGeometry.Enums
{
    /// <summary>
    /// Represents which side of an oriented plane a point lies on.
    /// <para>
    /// The sides are named after the plane normal rather than after world up, because a plane in this
    /// library carries its own orientation and may point anywhere.
    /// </para>
    /// </summary>
    public enum PlaneSide
    {
        /// <summary>
        /// The point lies on the side the plane normal points towards (positive signed distance).
        /// </summary>
        Above,

        /// <summary>
        /// The point lies on the side opposite the plane normal (negative signed distance).
        /// </summary>
        Below,

        /// <summary>
        /// The point lies on the plane itself, within tolerance.
        /// </summary>
        On
    }
}
