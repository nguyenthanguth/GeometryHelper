namespace GeometryHelper.IfcConvert.Core
{
    /// <summary>
    /// Specifies target length units for automatic scaling.
    /// </summary>
    public enum LengthUnit
    {
        /// <summary>
        /// Keeps the original unit as defined in the IFC model.
        /// </summary>
        Original = 0,

        /// <summary>
        /// Scales coordinates to millimeters (mm).
        /// </summary>
        Millimeters = 1,

        /// <summary>
        /// Scales coordinates to meters (m).
        /// </summary>
        Meters = 2
    }
}