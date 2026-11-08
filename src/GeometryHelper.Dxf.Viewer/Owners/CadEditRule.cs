namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What the presenter may do to an entity, as its owner answers through <see cref="ICadEntityOwner.GetEditRule"/>: a
    /// line of a model drawing's view is read-only, a mark may be moved, a dimension edited through its owner.
    /// </summary>
    public enum CadEditRule
    {
        /// <summary>
        /// The presenter edits the entity itself and asks nobody: a line drawn by hand, everything in a plain DXF.
        /// </summary>
        Free = 0,

        /// <summary>
        /// Nothing may change the entity, erasing it included: a projected line of a view is the model, changed by
        /// changing the view, not the line.
        /// </summary>
        ReadOnly = 1,

        /// <summary>
        /// The entity may be moved (<see cref="CadEditKind.Move"/>) and nothing else; the move goes to its owner, which
        /// records it, as a mark dragged off its part is.
        /// </summary>
        MoveOnly = 2,

        /// <summary>
        /// Every edit goes to the owner, which takes or refuses each one: a dimension moved, its text overridden, a grip
        /// dragged.
        /// </summary>
        Edit = 3,
    }
}
