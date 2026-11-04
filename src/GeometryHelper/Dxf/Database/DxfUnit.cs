namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A unit of length, as AutoCAD's <c>Unit</c> numbers it: the resolution of an image's pixels (group 281 of its IMAGEDEF)
    /// and the unit images are scaled in (group 72 of the RASTERVARIABLES). Not the numbering of <c>$INSUNITS</c>, which
    /// <see cref="DxfUnits"/> is, the unit a drawing is in: a millimetre is 1 here and 4 there. The two keep AutoCAD's names,
    /// <c>Unit</c> and <c>UnitsValue</c>.
    /// </summary>
    public enum DxfUnit
    {
        /// <summary>No unit, 0.</summary>
        None = 0,

        /// <summary>Millimetres, 1.</summary>
        Millimeter = 1,

        /// <summary>Centimetres, 2.</summary>
        Centimeter = 2,

        /// <summary>Metres, 3.</summary>
        Meter = 3,

        /// <summary>Kilometres, 4.</summary>
        Kilometer = 4,

        /// <summary>Inches, 5.</summary>
        Inch = 5,

        /// <summary>Feet, 6.</summary>
        Foot = 6,

        /// <summary>Yards, 7.</summary>
        Yard = 7,

        /// <summary>Miles, 8.</summary>
        Mile = 8,

        /// <summary>Microinches, 9.</summary>
        Microinches = 9,

        /// <summary>Mils, thousandths of an inch, 10.</summary>
        Mils = 10,

        /// <summary>Angstroms, 11.</summary>
        Angstroms = 11,

        /// <summary>Nanometres, 12.</summary>
        Nanometers = 12,

        /// <summary>Microns, 13.</summary>
        Microns = 13,

        /// <summary>Decimetres, 14.</summary>
        Decimeters = 14,

        /// <summary>Dekametres, 15.</summary>
        Dekameters = 15,

        /// <summary>Hectometres, 16.</summary>
        Hectometers = 16,

        /// <summary>Gigametres, 17.</summary>
        GigaMeters = 17,

        /// <summary>Astronomical units, 18.</summary>
        Astronomical = 18,

        /// <summary>Light years, 19.</summary>
        LightYears = 19,

        /// <summary>Parsecs, 20.</summary>
        Parsecs = 20,
    }
}
