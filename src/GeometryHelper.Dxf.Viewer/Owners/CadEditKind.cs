namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What an edit does to an entity, as a <see cref="CadEdit"/> tells an owner. Numbers in the values of an edit are
    /// written in the invariant culture, as many digits as bring them back to the bit, and separated by commas.
    /// </summary>
    public enum CadEditKind
    {
        /// <summary>
        /// A property changed in Properties: <see cref="CadEdit.Property"/> is the row's label ("Layer", "Color",
        /// "Text override") and the old and new values are the texts the row shows, "0" and "Steel" for a layer.
        /// </summary>
        Property = 0,

        /// <summary>
        /// The entity moved, as MOVE moves it: the new value is the displacement "dx,dy", "10,-2.5" for 10 to the right
        /// and 2.5 down; the old value is null.
        /// </summary>
        Move = 1,

        /// <summary>
        /// The entity turned, as ROTATE turns it: the new value is the base point and the angle in radians,
        /// anticlockwise, "x,y,angle"; the old value is null.
        /// </summary>
        Rotate = 2,

        /// <summary>
        /// The entity scaled, as SCALE scales it: the new value is the base point and the factor, "x,y,factor"; the old
        /// value is null.
        /// </summary>
        Scale = 3,

        /// <summary>
        /// The entity mirrored, as MIRROR mirrors it: the new value is the two points of the mirror line,
        /// "x0,y0,x1,y1"; the old value is null.
        /// </summary>
        Mirror = 4,

        /// <summary>
        /// A grip dragged: <see cref="CadEdit.Property"/> is the grip's index among the entity's grips ("0", "1", ...),
        /// the old value where it was, "x,y", and the new value where it went.
        /// </summary>
        Grip = 5,

        /// <summary>The entity erased; the property and both values are null.</summary>
        Erase = 6,
    }
}
