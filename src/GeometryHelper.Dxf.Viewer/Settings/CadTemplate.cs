using System;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The drawing NEW starts from: the presenter's own metric template, made in code with the settings of AutoCAD's
    /// acadiso.dwt (not its file). A drawing of R2013 in millimetres, its linear units decimal to four places and its angles
    /// decimal degrees to none, its limits an A3 sheet of 420 by 297, its text 2.5 high, its points dots, and its dimensions
    /// drawn in ISO-25: arrowheads and text 2.5, the text above the line, two decimal places and a decimal comma.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every variable the template sets is written into the header, those the library would read the same where the header
    /// gives none among them, so that AutoCAD, which reads a variable the header leaves out as its own imperial drawing has it
    /// (MEASUREMENT 0, DIMSTYLE Standard, DIMASZ 0.18), opens the drawing as metric too.
    /// </para>
    /// <para>
    /// The tables are layer 0, the linetypes ByBlock, ByLayer and Continuous, the text styles Standard (arial.ttf, as
    /// acadiso.dwt of AutoCAD 2021 has it) and Annotative (arial.ttf too), and the dimension styles Standard and
    /// ISO-25. AutoCAD adds Defpoints with the first dimension, and the multileader and table styles Standard when it opens
    /// the drawing; the library reads those styles but does not write them, so MLEADER has none to draw in until a drawing of
    /// AutoCAD's gives it one.
    /// </para>
    /// </remarks>
    public static class CadTemplate
    {
        /// <summary>The name of the dimension style the template makes current.</summary>
        public const string DimensionStyle = "ISO-25";

        /// <summary>
        /// Makes a new drawing of the metric template: R2013 (AC1027), every setting of <see cref="CadTemplate"/> in its header
        /// and tables, nothing drawn.
        /// </summary>
        /// <returns>The drawing.</returns>
        public static DxfDatabase CreateMetric()
        {
            var database = new DxfDatabase("AC1027");

            // The units, limits, text height and point style of acadiso.dwt.
            Pin(database, "$MEASUREMENT", 70, "1");
            Pin(database, "$INSUNITS", 70, "4");
            Pin(database, "$LUNITS", 70, "2");
            Pin(database, "$LUPREC", 70, "4");
            Pin(database, "$AUNITS", 70, "0");
            Pin(database, "$AUPREC", 70, "0");
            Pin(database, "$LTSCALE", 40, 1.0);
            Pin(database, "$PDMODE", 70, "0");
            Pin(database, "$PDSIZE", 40, 0.0);
            Pin(database, "$TEXTSIZE", 40, 2.5);
            database.SetHeader("$LIMMIN", new DxfTypedValue(10, "0.0"), new DxfTypedValue(20, "0.0"));
            database.SetHeader("$LIMMAX", new DxfTypedValue(10, "420.0"), new DxfTypedValue(20, "297.0"));
            Pin(database, "$TEXTSTYLE", 7, "Standard");
            Pin(database, "$CELTYPE", 6, "ByLayer");
            Pin(database, "$CLAYER", 8, "0");

            database.TextStyleTable["Standard"].FileName = "arial.ttf";

            var annotative = new DxfTextStyleTableRecord { Name = "Annotative", FileName = "arial.ttf" };
            database.TextStyleTable.Add(annotative);
            annotative.Annotative = DxfAnnotativeStates.True;

            var iso = new DxfDimStyleTableRecord { Name = DimensionStyle };
            database.DimStyleTable.Add(iso);
            iso.Dimtxsty = database.TextStyleTable["Standard"];

            // ISO-25 where it differs from AutoCAD's own style (acad.dwt), each written into the header as well.
            Dim(database, "$DIMASZ", 2.5, v => iso.Dimasz = v);
            Dim(database, "$DIMCEN", 2.5, v => iso.Dimcen = v);
            Dim(database, "$DIMDLI", 3.75, v => iso.Dimdli = v);
            Dim(database, "$DIMEXE", 1.25, v => iso.Dimexe = v);
            Dim(database, "$DIMEXO", 0.625, v => iso.Dimexo = v);
            Dim(database, "$DIMGAP", 0.625, v => iso.Dimgap = v);
            Dim(database, "$DIMTXT", 2.5, v => iso.Dimtxt = v);
            Dim(database, "$DIMALTF", 1.0 / 25.4, v => iso.Dimaltf = v);
            Whole(database, "$DIMDEC", 2, v => iso.Dimdec = v);
            Whole(database, "$DIMTDEC", 2, v => iso.Dimtdec = v);
            Whole(database, "$DIMTAD", 1, v => iso.Dimtad = v);
            Whole(database, "$DIMZIN", 8, v => iso.Dimzin = v);
            Whole(database, "$DIMTZIN", 8, v => iso.Dimtzin = v);
            Whole(database, "$DIMALTD", 3, v => iso.Dimaltd = v);
            Whole(database, "$DIMALTTD", 3, v => iso.Dimalttd = v);
            Whole(database, "$DIMTIH", 0, v => iso.Dimtih = v != 0);
            Whole(database, "$DIMTOH", 0, v => iso.Dimtoh = v != 0);
            Whole(database, "$DIMTOFL", 1, v => iso.Dimtofl = v != 0);
            Whole(database, "$DIMTOLJ", 0, v => iso.Dimtolj = v);
            Whole(database, "$DIMDSEP", ',', v => iso.Dimdsep = (char)v);
            Pin(database, "$DIMTXSTY", 7, "Standard");
            Pin(database, "$DIMSTYLE", 2, DimensionStyle);
            return database;
        }

        /// <summary>Writes a variable into the header, whether or not the library reads the same where it gives none.</summary>
        private static void Pin(DxfDatabase database, string name, int code, string value) =>
            database.SetHeader(name, new DxfTypedValue(code, value));

        private static void Pin(DxfDatabase database, string name, int code, double value) =>
            Pin(database, name, code, value.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>A real dimension variable: the style's and the header's.</summary>
        private static void Dim(DxfDatabase database, string name, double value, Action<double> style)
        {
            style(value);
            Pin(database, name, 40, value);
        }

        /// <summary>A whole dimension variable, a flag or a letter: the style's and the header's.</summary>
        private static void Whole(DxfDatabase database, string name, int value, Action<int> style)
        {
            style(value);
            Pin(database, name, 70, value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
