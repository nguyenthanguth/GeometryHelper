namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What a number of a cell of a table measures, as AutoCAD's <c>UnitType</c> numbers it: group 94 of the cell's value
    /// (<c>301 CELL_VALUE</c>), from R2007 on. A cell of 12.5 that is a length is <see cref="Distance"/>; flags may go
    /// together, an angle not turned with the drawing's angle base being <c>Angle | AngleNotTransformed</c>, 65 538.
    /// </summary>
    public enum DxfUnitType
    {
        /// <summary>Nothing, 0.</summary>
        Unitless = 0,

        /// <summary>A length, 1.</summary>
        Distance = 1,

        /// <summary>An angle, 2.</summary>
        Angle = 2,

        /// <summary>An area, 4.</summary>
        Area = 4,

        /// <summary>A volume, 8.</summary>
        Volume = 8,

        /// <summary>Money, 16.</summary>
        Currency = 16,

        /// <summary>A percentage, 32.</summary>
        Percentage = 32,

        /// <summary>An angle not turned with the drawing's angle base, 65 536.</summary>
        AngleNotTransformed = 65536,
    }
}
