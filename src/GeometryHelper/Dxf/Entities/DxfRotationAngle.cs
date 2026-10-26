namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A turn of a quarter at a time, as AutoCAD's <c>RotationAngle</c> numbers it: the turn of a cell's text,
    /// <see cref="DxfTable.TextRotation(int, int)"/>, read from the cell's 145 in radians.
    /// </summary>
    public enum DxfRotationAngle
    {
        /// <summary>A turn of none of the four, -1.</summary>
        DegreesUnknown = -1,

        /// <summary>No turn, 0.</summary>
        Degrees000 = 0,

        /// <summary>A quarter turn anticlockwise, 90 degrees, 1.</summary>
        Degrees090 = 1,

        /// <summary>A half turn, 180 degrees, 2.</summary>
        Degrees180 = 2,

        /// <summary>Three quarters anticlockwise, 270 degrees, 3.</summary>
        Degrees270 = 3,
    }
}
