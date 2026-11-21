using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// What a DXF the presenter opens is given for the header values it leaves out, as AutoCAD 2021 reads such a file: the
    /// values of its own imperial drawing, whatever else the header says. A file whose header holds nothing but the version,
    /// the code page, the handle seed, the units and the extents reads MEASUREMENT 0, the dimension style Standard current
    /// with arrowheads and text 0.18 high, four decimal places, a decimal point and the text on the line (DIMASZ 0.18,
    /// DIMTXT 0.18, DIMDEC 4, DIMTAD 0), and CELTYPE <c>Continuous</c>, so that a line drawn on it carries the linetype
    /// <c>Continuous</c> as its own (group 6) and not ByLayer. A header that says MEASUREMENT 1 and leaves the dimension
    /// variables out reads the same imperial ones (measured in AutoCAD 2021's console on a blank drawing with each).
    /// </summary>
    /// <remarks>
    /// The library reads a header variable that is left out as the metric drawing of ezdxf has it, which its own drawings
    /// rely on (<see cref="CadTemplate"/> writes what differs), so the values are written into the header of a file read: each
    /// variable it already has is left as it is, as is a drawing older than R2000, whose header has none of them.
    /// </remarks>
    internal static class CadHeaderDefaults
    {
        /// <summary>
        /// Writes into the header of a drawing read from a DXF the MEASUREMENT, the dimension variables, the current dimension
        /// style and the current linetype it leaves out, with the values of AutoCAD's own drawing.
        /// </summary>
        /// <param name="database">The drawing just read.</param>
        internal static void Apply(DxfDatabase database)
        {
            if (!database.File.Supports(1015))
            {
                return;
            }

            if (database.GetHeader("$MEASUREMENT") == null)
            {
                database.Measurement = DxfMeasurementValue.English;
            }

            database.TakeLeftOutDimensions(new DxfDimStyleTableRecord());

            if (database.GetHeader("$DIMSTYLE") == null && database.DimStyleTable.Has("Standard"))
            {
                database.Dimstyle = "Standard";
            }

            if (database.GetHeader("$CELTYPE") == null && database.LinetypeTable.Has("Continuous"))
            {
                database.Celtype = "Continuous";
            }
        }
    }
}
