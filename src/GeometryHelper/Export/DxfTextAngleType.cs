namespace GeometryHelper.Export
{
    /// <summary>
    /// How the words of a MULTILEADER are turned, as AutoCAD's <c>TextAngleType</c> numbers it: group 174 of the MULTILEADER;
    /// <see cref="HorizontalAngle"/> where it gives none.
    /// </summary>
    public enum DxfTextAngleType
    {
        /// <summary>Along the last segment of the leader, 0.</summary>
        InsertAngle = 0,

        /// <summary>Level, 1.</summary>
        HorizontalAngle = 1,

        /// <summary>Along the last segment of the leader, turned over where it would read upside down, 2.</summary>
        AlwaysRightReadingAngle = 2,
    }
}
