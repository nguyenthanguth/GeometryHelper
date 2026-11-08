namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// What one <see cref="MarkField"/> of a mark's text shows, read from the <see cref="DrawingObject"/> marked.
    /// </summary>
    /// <remarks>
    /// A field with nothing to show, a part with no material or a bolt with no length, is left out of the text together
    /// with its separator: a part mark of the fields Mark and Material reads "P1 S355", or "P1" where the part has no
    /// material.
    /// </remarks>
    public enum MarkFieldKind
    {
        /// <summary>The position mark, <see cref="DrawingObject.Mark"/>: "P1". An assembly mark shows its main part's.</summary>
        Mark = 0,

        /// <summary>The name, <see cref="DrawingObject.Name"/>: "BASE PLATE".</summary>
        Name = 1,

        /// <summary>The profile, <see cref="DrawingObject.Profile"/>: "PL20*200".</summary>
        Profile = 2,

        /// <summary>The material grade, <see cref="DrawingObject.Material"/>: "S355".</summary>
        Material = 3,

        /// <summary>
        /// The length in whole millimetres: the attribute "Length" where the object has one, written as it is; otherwise how
        /// far its body reaches along the X of its frame, 300 for the plate of TC1 and 80 for a bolt 80 long.
        /// </summary>
        Length = 4,

        /// <summary>
        /// How many: for a bolt mark the bolts of its group, 4 for the four bolts of a base plate; for a part the parts of
        /// the same position mark among the objects of the view, 4 for four stiffeners S1.
        /// </summary>
        Count = 5,

        /// <summary>
        /// A bolt's or a hole's size, "M" and its diameter: the attribute "Diameter" where it has one, otherwise twice the
        /// furthest a corner of its body stands off its axis, the X of its frame, to a tenth: "M20". Empty for a part.
        /// </summary>
        Diameter = 6,

        /// <summary>The value of a named attribute of the object, <see cref="MarkField.AttributeName"/>; empty where it has none.</summary>
        Attribute = 7,
    }
}
