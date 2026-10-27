namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What the coordinates of a drawing's geographic location are, as AutoCAD's <c>TypeOfCoordinates</c> numbers them (group
    /// 70 of a GEODATA).
    /// </summary>
    public enum DxfTypeOfCoordinates
    {
        /// <summary>Not known, 0.</summary>
        Unknown = 0,

        /// <summary>A local grid, 1: the reference point is in the coordinate system's own coordinates.</summary>
        Local = 1,

        /// <summary>A projected grid, 2.</summary>
        Grid = 2,

        /// <summary>Geographic, latitude and longitude, 3.</summary>
        Geographic = 3,
    }
}
