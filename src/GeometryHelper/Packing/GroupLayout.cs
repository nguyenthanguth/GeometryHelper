namespace GeometryHelper.Packing
{
    /// <summary>
    /// How the boxes of one group are laid out before the group is packed onto a sheet as one block.
    /// </summary>
    public enum GroupLayout
    {
        /// <summary>
        /// Afresh, as close together as they go, in their order from the upper left: a view and its sections become
        /// one compact block. The default.
        /// </summary>
        Compact,

        /// <summary>
        /// As they stand to one another, the group moving as one block: a layout already drawn is kept. A group too
        /// large to keep so on one sheet is laid out as <see cref="Compact"/> lays it out.
        /// </summary>
        Keep,
    }
}
