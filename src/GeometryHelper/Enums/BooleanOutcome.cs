namespace GeometryHelper.Enums
{
    /// <summary>
    /// How a boolean of two solids came out: a body made, nothing left of it, or no answer at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Core.Boolean3.TrySubtract(Geometry.GeoSolid3, Geometry.GeoSolid3, out Geometry.GeoSolid3, Tolerance)"/> and
    /// <see cref="Core.Boolean3.TryIntersect(Geometry.GeoSolid3, Geometry.GeoSolid3, out Geometry.GeoSolid3, Tolerance)"/> give
    /// false both where nothing is left and where the result could not be worked out, which only the log tells apart. A
    /// net volume worked out by taking cutters off a part one after another reads the two the opposite way: nothing left
    /// takes the part to nought, as it should be taken, and no answer is a step to work out some other way, as by slicing.
    /// The overloads giving an outcome say which it was.
    /// </para>
    /// </remarks>
    public enum BooleanOutcome
    {
        /// <summary>
        /// A body was made, and is the result.
        /// </summary>
        Made,

        /// <summary>
        /// Nothing is left: of a difference, the tool took the whole subject; of an intersection, the two share no volume.
        /// Either is nothing too where what it made, closed, encloses no more than a slab the tolerance thick, as
        /// <see cref="Geometry.GeoSolid3.Validate(Tolerance)"/> reads it: a plate 10 by 10 and 0.0018 thick less a box over all
        /// but 0.0018 of its width leaves a rod 0.0018 square, 0.000 032 4 cubic millimetres enclosing nothing at a tolerance
        /// of a thousandth, and is empty. A union is never empty.
        /// </summary>
        Empty,

        /// <summary>
        /// The result could not be worked out, and why is written to <see cref="GeometryHelperLog"/>. No body is given, and
        /// none is to be assumed: not the subject as it was, and not nothing.
        /// </summary>
        NotWorkedOut,
    }
}
