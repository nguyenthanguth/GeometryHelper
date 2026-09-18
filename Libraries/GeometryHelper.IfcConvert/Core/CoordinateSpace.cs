namespace GeometryHelper.IfcConvert.Core
{
    /// <summary>
    /// Specifies the target coordinate space for converted geometry.
    /// </summary>
    public enum CoordinateSpace
    {
        /// <summary>
        /// Converts geometry into global (world) coordinates using the product's placement hierarchy.
        /// </summary>
        Global = 0,

        /// <summary>
        /// Keeps geometry in local coordinates relative to the product's object placement.
        /// </summary>
        Local = 1
    }
}