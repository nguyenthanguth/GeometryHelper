namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How the scale of a drawing's geographic location is judged, as AutoCAD's <c>ScaleEstimationMethod</c> numbers it (group
    /// 95 of a GEODATA).
    /// </summary>
    public enum DxfScaleEstimationMethod
    {
        /// <summary>None, a scale of 1, 1.</summary>
        Unity = 1,

        /// <summary>The scale factor the user gives, 2.</summary>
        UserDefined = 2,

        /// <summary>The scale of the grid at the reference point, 3.</summary>
        ReferencePoint = 3,

        /// <summary>Prismoidal, 4.</summary>
        Prismoidal = 4,
    }
}
