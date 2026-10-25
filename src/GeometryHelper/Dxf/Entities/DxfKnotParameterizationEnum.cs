namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How a spline through fit points gives each point its parameter, as AutoCAD's <c>KnotParameterizationEnum</c>: by the
    /// distance from the point before it, its square root, or one step each.
    /// </summary>
    public enum DxfKnotParameterizationEnum
    {
        /// <summary>By the chord from the point before: points 10 and 30 apart are 10 and 30 apart in the parameter too.</summary>
        Chord = 0,

        /// <summary>By the square root of the chord.</summary>
        SqrtChord = 1,

        /// <summary>One step from each point to the next, however far apart they are.</summary>
        Uniform = 2,

        /// <summary>Parameters of the program's own.</summary>
        CustomParameterization = 15,

        /// <summary>None said.</summary>
        NotDefinedParameterization = 16,
    }
}
