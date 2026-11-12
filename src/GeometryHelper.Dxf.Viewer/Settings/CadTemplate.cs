using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

            // ISO-25, current and written into the header where it differs from AutoCAD's own style (acad.dwt), comes with the
            // library's new drawing, as acadiso.dwt has it.
            Pin(database, "$DIMTXSTY", 7, "Standard");
            Pin(database, "$DIMSTYLE", 2, DimensionStyle);
            return database;
        }

        /// <summary>
        /// Adds the layers of Global settings (Layers, colours and lines) to a new drawing, in the order they are listed: each
        /// with its colour as a true colour, its lineweight (the width of AutoCAD's nearest, 0.5 mm for 0.50) and whether it is
        /// plotted. A linetype is taken from the presenter's own set (<see cref="CadLinetypeFile.BuiltIn"/>) and added to the
        /// drawing where it has not got it: HIDDEN for Hidden, CENTER for Centre. One that set has not is not made up; the
        /// layer is drawn Continuous. A layer named as one the drawing has already, 0 among them, is left as it is, and the
        /// current layer stays 0.
        /// </summary>
        /// <param name="database">The new drawing.</param>
        /// <param name="layers">The layers, as <see cref="GlobalSettings.Layers"/> lists them.</param>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the layers are null.</exception>
        public static void AddLayers(DxfDatabase database, IEnumerable<GlobalLayer> layers)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (layers == null)
            {
                throw new ArgumentNullException(nameof(layers));
            }

            foreach (GlobalLayer layer in layers)
            {
                if (database.LayerTable.Has(layer.Name))
                {
                    continue;
                }

                var record = new DxfLayerTableRecord
                {
                    Name = layer.Name,
                    Color = DxfColor.FromRgb((byte)(layer.Rgb >> 16), (byte)(layer.Rgb >> 8), (byte)layer.Rgb),
                    LineWeight = database.GetNearestLineWeight((int)Math.Round(layer.LineWeight * 100.0)),
                };
                string linetype = LinetypeOf(database, layer.Linetype);

                if (linetype != null)
                {
                    record.LinetypeName = linetype;
                }

                database.LayerTable.Add(record);
                record.IsPlottable = layer.Plot;
            }
        }

        /// <summary>The name of a linetype the drawing has once it is added from the presenter's own set; null for Continuous or one that set has not.</summary>
        private static string LinetypeOf(DxfDatabase database, string name)
        {
            if (database.LinetypeTable.Has(name))
            {
                return string.Equals(name, "Continuous", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : database.LinetypeTable[name].Name;
            }

            CadLinetypeDefinition own = CadLinetypeFile.BuiltIn.FirstOrDefault(l =>
                string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)
            );

            if (own == null || own.Fault != null)
            {
                return null;
            }

            try
            {
                return CadLinetypes.Add(database, own).Name;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Writes a variable into the header, whether or not the library reads the same where it gives none.</summary>
        private static void Pin(DxfDatabase database, string name, int code, string value) =>
            database.SetHeader(name, new DxfTypedValue(code, value));

        private static void Pin(DxfDatabase database, string name, int code, double value) =>
            Pin(database, name, code, value.ToString("R", CultureInfo.InvariantCulture));
    }
}
