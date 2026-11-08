namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Something outside the presenter that owns some of a drawing's entities and is asked before they change: a model
    /// drawing owns the lines, dimensions and marks it wrote. An entity is owned when its extended data carries a
    /// <see cref="CadOwnerTag"/> naming the owner; owners are found through a <see cref="CadOwnerRegistry"/>.
    /// </summary>
    /// <remarks>
    /// The presenter knows owners only through this contract. A line of a view answers <see cref="CadEditRule.ReadOnly"/>,
    /// a mark <see cref="CadEditRule.MoveOnly"/> and a dimension <see cref="CadEditRule.Edit"/>; an edit the rule lets
    /// through goes to <see cref="TryApply"/>, which records it and writes the owner's entities again.
    /// </remarks>
    public interface ICadEntityOwner
    {
        /// <summary>
        /// Gets the owner's name, as the <see cref="CadOwnerTag.Owner"/> of its entities gives it: "GeometryHelper.Drawing"
        /// for a model drawing. Names are compared character by character, case and all.
        /// </summary>
        string Name { get; }

        /// <summary>Says what the presenter may do to one of the owner's entities.</summary>
        /// <param name="entity">An entity whose tag names this owner.</param>
        /// <returns>The rule for the entity.</returns>
        CadEditRule GetEditRule(DxfEntity entity);

        /// <summary>
        /// Takes an edit of one of the owner's entities: the owner records it and changes or writes its entities again in
        /// the document; or refuses it, and the document stays as it was.
        /// </summary>
        /// <param name="document">The document the entity is in.</param>
        /// <param name="edit">The edit.</param>
        /// <returns>Whether the owner took the edit.</returns>
        bool TryApply(CadDocument document, CadEdit edit);
    }
}
