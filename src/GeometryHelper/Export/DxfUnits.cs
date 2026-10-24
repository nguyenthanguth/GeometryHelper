namespace GeometryHelper.Export
{
    /// <summary>
    /// The unit a DXF file says its drawing is in, the header variable <c>$INSUNITS</c>, numbered as the DXF reference
    /// numbers it: a file <see cref="DxfWriter"/> writes is in <see cref="Millimeters"/>, 4.
    /// </summary>
    public enum DxfUnits
    {
        /// <summary>No unit given, 0, or a number the DXF reference does not list; also a file with no <c>$INSUNITS</c>.</summary>
        Undefined = 0,

        /// <summary>Inches, 1: 25.4 millimetres.</summary>
        Inches = 1,

        /// <summary>Feet, 2: 304.8 millimetres.</summary>
        Feet = 2,

        /// <summary>Miles, 3: 1 609 344 millimetres.</summary>
        Miles = 3,

        /// <summary>Millimetres, 4.</summary>
        Millimeters = 4,

        /// <summary>Centimetres, 5: 10 millimetres.</summary>
        Centimeters = 5,

        /// <summary>Metres, 6: 1 000 millimetres.</summary>
        Meters = 6,

        /// <summary>Kilometres, 7: 1 000 000 millimetres.</summary>
        Kilometers = 7,

        /// <summary>Microinches, 8: 2.54E-5 millimetres.</summary>
        MicroInches = 8,

        /// <summary>Mils, thousandths of an inch, 9: 0.0254 millimetres.</summary>
        Mils = 9,

        /// <summary>Yards, 10: 914.4 millimetres.</summary>
        Yards = 10,

        /// <summary>Angstroms, 11: 1E-7 millimetres.</summary>
        Angstroms = 11,

        /// <summary>Nanometres, 12: 1E-6 millimetres.</summary>
        Nanometers = 12,

        /// <summary>Microns, 13: 0.001 millimetres.</summary>
        Microns = 13,

        /// <summary>Decimetres, 14: 100 millimetres.</summary>
        Decimeters = 14,

        /// <summary>Decametres, 15: 10 000 millimetres.</summary>
        Dekameters = 15,

        /// <summary>Hectometres, 16: 100 000 millimetres.</summary>
        Hectometers = 16,

        /// <summary>Gigametres, 17: 1E12 millimetres.</summary>
        Gigameters = 17,

        /// <summary>Astronomical units, 18: 1.495978707E14 millimetres.</summary>
        Astronomical = 18,

        /// <summary>Light years, 19: 9.4607304725808E18 millimetres.</summary>
        LightYears = 19,

        /// <summary>Parsecs, 20: 3.0856775814913673E19 millimetres.</summary>
        Parsecs = 20,

        /// <summary>US survey feet, 21: 1 200 / 3 937 of a metre, 304.80061 millimetres.</summary>
        USSurveyFeet = 21,

        /// <summary>US survey inches, 22: a twelfth of a US survey foot, 25.400051 millimetres.</summary>
        USSurveyInch = 22,

        /// <summary>US survey yards, 23: three US survey feet, 914.40183 millimetres.</summary>
        USSurveyYard = 23,

        /// <summary>US survey miles, 24: 5 280 US survey feet, 1 609 347.2 millimetres.</summary>
        USSurveyMile = 24,
    }
}
