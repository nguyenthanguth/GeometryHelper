namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// What an <see cref="ObjectMark"/> marks, which decides the fields of <see cref="MarkContent"/> its text is made of.
    /// </summary>
    public enum MarkKind
    {
        /// <summary>A part that is not the main part of an assembly: an end plate, a stiffener, a plate on its own.</summary>
        Part = 0,

        /// <summary>The main part of an assembly, which carries the assembly's mark: the column of a column with its base plate.</summary>
        Assembly = 1,

        /// <summary>A group of bolts, marked once with their count and size: "4 x M20 x 80".</summary>
        Bolt = 2,
    }
}
