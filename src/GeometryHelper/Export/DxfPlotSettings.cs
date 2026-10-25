using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The settings a layout is plotted with, as AutoCAD's <c>PlotSettings</c>: the part AcDbPlotSettings of a LAYOUT, and a
    /// PLOTSETTINGS of the page setups (<see cref="DxfDatabase.PlotSettingsDictionary"/>); read as the file holds them.
    /// </summary>
    /// <remarks>
    /// The sizes of the sheet are in millimetres whatever <see cref="PlotPaperUnits"/> says, as AutoCAD writes them: a sheet of
    /// A3 laid on its side is 420 by 297, its margins 7.5 at the sides and 20 above and below in a drawing ezdxf made. The
    /// settings are read and not written: a change to them is AutoCAD's plotting to see to.
    /// </remarks>
    public class DxfPlotSettings : DxfDBObject
    {
        private const string Marker = "AcDbPlotSettings";

        internal DxfPlotSettings(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets the name of the page setup (group 1), as AutoCAD's <c>PlotSettingsName</c>; empty where it has none.</summary>
        public string PlotSettingsName => Text(Marker, 1);

        /// <summary>Gets the name of the plotter's configuration (group 2), as AutoCAD's <c>PlotConfigurationName</c>, such as <c>DWG To PDF.pc3</c>; empty where it has none.</summary>
        public string PlotConfigurationName => Text(Marker, 2);

        /// <summary>Gets the name of the sheet as the plotter calls it (group 4), as AutoCAD's <c>CanonicalMediaName</c>, such as <c>ISO_full_bleed_A3_(420.00_x_297.00_MM)</c>; empty where it has none.</summary>
        public string CanonicalMediaName => Text(Marker, 4);

        /// <summary>Gets the name of the view plotted where <see cref="PlotType"/> is a view (group 6), as AutoCAD's <c>PlotViewName</c>; empty where it has none.</summary>
        public string PlotViewName => Text(Marker, 6);

        /// <summary>Gets the name of the plot style table (group 7), as AutoCAD's <c>CurrentStyleSheet</c>, such as <c>monochrome.ctb</c>; empty where it has none.</summary>
        public string CurrentStyleSheet => Text(Marker, 7);

        /// <summary>
        /// Gets the margins of the sheet, as AutoCAD's <c>PlotPaperMargins</c>: its <see cref="DxfExtents2d.MinPoint"/> the left
        /// (group 40) and bottom (41) margins, its <see cref="DxfExtents2d.MaxPoint"/> the right (42) and top (43), in millimetres.
        /// </summary>
        public DxfExtents2d PlotPaperMargins => new DxfExtents2d(new GeoPoint2(Real(Marker, 40, 0.0), Real(Marker, 41, 0.0)), new GeoPoint2(Real(Marker, 42, 0.0), Real(Marker, 43, 0.0)));

        /// <summary>Gets the size of the sheet (groups 44 and 45), as AutoCAD's <c>PlotPaperSize</c>, in millimetres: (420, 297) for A3 on its side.</summary>
        public GeoPoint2 PlotPaperSize => new GeoPoint2(Real(Marker, 44, 0.0), Real(Marker, 45, 0.0));

        /// <summary>Gets how far the plot is moved on the sheet (groups 46 and 47), as AutoCAD's <c>PlotOrigin</c>, in millimetres.</summary>
        public GeoPoint2 PlotOrigin => new GeoPoint2(Real(Marker, 46, 0.0), Real(Marker, 47, 0.0));

        /// <summary>Gets the window plotted where <see cref="PlotType"/> is a window (groups 48 and 49, 140 and 141), as AutoCAD's <c>PlotWindowArea</c>.</summary>
        public DxfExtents2d PlotWindowArea => new DxfExtents2d(new GeoPoint2(Real(Marker, 48, 0.0), Real(Marker, 49, 0.0)), new GeoPoint2(Real(Marker, 140, 0.0), Real(Marker, 141, 0.0)));

        /// <summary>Gets the scale of the plot as a ratio (groups 142 and 143), as AutoCAD's <c>CustomPrintScale</c>; 1:1 where it gives none.</summary>
        public DxfCustomScale CustomPrintScale => new DxfCustomScale(Real(Marker, 142, 1.0), Real(Marker, 143, 1.0));

        /// <summary>Gets the units the sheet is set out in (group 72), as AutoCAD's <c>PlotPaperUnits</c>; inches where it gives none.</summary>
        public DxfPlotPaperUnit PlotPaperUnits => (DxfPlotPaperUnit)Whole(Marker, 72, 0);

        /// <summary>Gets how the plot is turned on its sheet (group 73), as AutoCAD's <c>PlotRotation</c>.</summary>
        public DxfPlotRotation PlotRotation => (DxfPlotRotation)Whole(Marker, 73, 0);

        /// <summary>Gets what is plotted (group 74), as AutoCAD's <c>PlotType</c>; what is shown where it gives none.</summary>
        public DxfPlotType PlotType => (DxfPlotType)Whole(Marker, 74, 0);

        /// <summary>Gets the standard scale of the plot (group 75), as AutoCAD's <c>StdScaleType</c>; scaled to fit where it gives none.</summary>
        public DxfStdScaleType StdScaleType => (DxfStdScaleType)Whole(Marker, 75, 0);

        /// <summary>Gets the standard scale of the plot as a number (group 147), as AutoCAD's <c>StdScale</c>; 1 where it gives none.</summary>
        public double StdScale => Real(Marker, 147, 1.0);

        /// <summary>Gets how the viewports are shaded when plotted (group 76), as AutoCAD's <c>ShadePlot</c>.</summary>
        public DxfPlotSettingsShadePlotType ShadePlot => (DxfPlotSettingsShadePlotType)Whole(Marker, 76, 0);

        /// <summary>Gets how finely a shaded plot is made (group 77), as AutoCAD's <c>ShadePlotResLevel</c>; normal where it gives none.</summary>
        public DxfShadePlotResLevel ShadePlotResLevel => (DxfShadePlotResLevel)Whole(Marker, 77, 2);

        /// <summary>Gets the dots per inch of a shaded plot of the custom level (group 78), as AutoCAD's <c>ShadePlotCustomDpi</c>; 300 where it gives none.</summary>
        public short ShadePlotCustomDpi => (short)Whole(Marker, 78, 300);

        /// <summary>Gets whether the borders of the viewports are plotted (flag 1 of group 70), as AutoCAD's <c>PlotViewportBorders</c>.</summary>
        public bool PlotViewportBorders => (Flags() & 1) != 0;

        /// <summary>Gets whether the plot styles are shown on the sheet (flag 2), as AutoCAD's <c>ShowPlotStyles</c>.</summary>
        public bool ShowPlotStyles => (Flags() & 2) != 0;

        /// <summary>Gets whether the plot is centred on the sheet (flag 4), as AutoCAD's <c>PlotCentered</c>.</summary>
        public bool PlotCentered => (Flags() & 4) != 0;

        /// <summary>Gets whether the lines behind are hidden in the plot (flag 8), as AutoCAD's <c>PlotHidden</c>.</summary>
        public bool PlotHidden => (Flags() & 8) != 0;

        /// <summary>Gets whether the plot is at its standard scale (flag 16), as AutoCAD's <c>UseStandardScale</c>.</summary>
        public bool UseStandardScale => (Flags() & 16) != 0;

        /// <summary>Gets whether the plot styles are plotted (flag 32), as AutoCAD's <c>PlotPlotStyles</c>.</summary>
        public bool PlotPlotStyles => (Flags() & 32) != 0;

        /// <summary>Gets whether the lineweights are scaled with the plot (flag 64), as AutoCAD's <c>ScaleLineweights</c>.</summary>
        public bool ScaleLineweights => (Flags() & 64) != 0;

        /// <summary>Gets whether the lineweights of the entities are plotted (flag 128), as AutoCAD's <c>PrintLineweights</c>.</summary>
        public bool PrintLineweights => (Flags() & 128) != 0;

        /// <summary>Gets whether the viewports are plotted before the rest of paper space (flag 512), as AutoCAD's <c>DrawViewportsFirst</c>.</summary>
        public bool DrawViewportsFirst => (Flags() & 512) != 0;

        /// <summary>Gets whether the settings are those of model space (flag 1024), as AutoCAD's <c>ModelType</c>: true for the layout Model.</summary>
        public bool ModelType => (Flags() & 1024) != 0;

        /// <summary>Gives the settings as a line for debugging: their type, handle and name.</summary>
        /// <returns>The line, such as <c>PLOTSETTINGS 2F A3 plan</c>.</returns>
        public override string ToString() => Item.Type + " " + (Handle ?? string.Empty) + " " + PlotSettingsName;

        /// <summary>The flags of the plot settings (group 70 of AcDbPlotSettings).</summary>
        private int Flags() => Whole(Marker, 70, 0);

        /// <summary>
        /// The index of the first group of a code in the part of a subclass of the record, among the groups as
        /// <see cref="DxfItem.Groups"/> counts them: after the subclass's marker and before the next marker or the extended data,
        /// outside the groups of the applications; -1 where the part has none.
        /// </summary>
        private protected int PartIndex(string marker, int code)
        {
            IReadOnlyList<DxfTypedValue> groups = Item.File.GroupsOf(Item);
            bool braced = false;
            bool inside = false;

            for (int i = 0; i < groups.Count; i++)
            {
                DxfTypedValue group = groups[i];

                if (group.TypeCode == 1001)
                {
                    break;
                }

                if (group.TypeCode == 102)
                {
                    braced = group.Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && group.TypeCode == 100)
                {
                    if (inside)
                    {
                        break;
                    }

                    inside = group.Value.Trim() == marker;
                }
                else if (!braced && inside && group.TypeCode == code)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The value of the first group of a code in the part of a subclass, as the file writes it; null where it has none.</summary>
        private protected string PartValue(string marker, int code)
        {
            int at = PartIndex(marker, code);
            return at < 0 ? null : Item.File.GroupsOf(Item)[at].Value;
        }

        /// <summary>A text of the part of a subclass, its escapes read as letters; empty where it has none.</summary>
        private protected string Text(string marker, int code) => DxfReader.Decode(PartValue(marker, code) ?? string.Empty);

        /// <summary>A number of the part of a subclass; the fallback where it has none, or not a finite number.</summary>
        private protected double Real(string marker, int code, double fallback)
            => Numbers.TryRead(PartValue(marker, code), out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>A whole number of the part of a subclass; the fallback where it has none, or not a whole number.</summary>
        private protected int Whole(string marker, int code, int fallback)
            => int.TryParse(PartValue(marker, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    }
}
