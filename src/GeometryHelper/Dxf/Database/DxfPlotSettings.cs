using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The settings a layout is plotted with, as AutoCAD's <c>PlotSettings</c>: the part AcDbPlotSettings of a LAYOUT, and a
    /// PLOTSETTINGS of the page setups (<see cref="DxfDatabase.PlotSettingsDictionary"/>); read as the file holds them.
    /// </summary>
    /// <remarks>
    /// The sizes of the sheet are in millimetres whatever <see cref="PlotPaperUnits"/> says, as AutoCAD writes them: a sheet of
    /// A3 laid on its side is 420 by 297, its margins 7.5 at the sides and 20 above and below in a drawing ezdxf made. What a
    /// sheet needs is set as AutoCAD's <c>PlotSettingsValidator</c> sets it: its name and with it its size
    /// (<see cref="CanonicalMediaName"/>), its <see cref="PlotPaperUnits"/>, <see cref="PlotRotation"/>,
    /// <see cref="PlotPaperMargins"/> and <see cref="PlotOrigin"/>; a layout set to <c>ISO_A2_(594.00_x_420.00_MM)</c> lies on
    /// a sheet 594 by 420. The rest is read and not written: a change to it is AutoCAD's plotting to see to.
    /// </remarks>
    public class DxfPlotSettings : DxfDBObject
    {
        private const string Marker = "AcDbPlotSettings";

        // The groups of AcDbPlotSettings in the order AutoCAD writes them, a 100 where the marker stands.
        private static readonly IReadOnlyList<int> PlotOrder = Array.AsReadOnly(new[]
        {
            100, 1, 2, 4, 6, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 140, 141, 142, 143, 70, 72, 73, 74, 7, 75, 76, 77, 78, 147, 148, 149, 333,
        });

        // A name of a sheet that ends in its size in millimetres, width then height, as AutoCAD's ISO_A3_(420.00_x_297.00_MM).
        private static readonly Regex MediaSize = new Regex(@"^[A-Za-z0-9_.\-]+_\(([0-9]+(?:\.[0-9]+)?)_x_([0-9]+(?:\.[0-9]+)?)_MM\)$", RegexOptions.CultureInvariant);

        internal DxfPlotSettings(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Settings made in code, those of a layout made in code, given their record when it is added to a drawing.</summary>
        private protected DxfPlotSettings()
        {
        }

        /// <summary>Gets the name of the page setup (group 1), as AutoCAD's <c>PlotSettingsName</c>; empty where it has none.</summary>
        public string PlotSettingsName => Text(Marker, 1);

        /// <summary>Gets the name of the plotter's configuration (group 2), as AutoCAD's <c>PlotConfigurationName</c>, such as <c>DWG To PDF.pc3</c>; empty where it has none.</summary>
        public string PlotConfigurationName => Text(Marker, 2);

        /// <summary>
        /// Gets or sets the name of the sheet as the plotter calls it (group 4), as AutoCAD's <c>CanonicalMediaName</c>, such as
        /// <c>ISO_full_bleed_A3_(420.00_x_297.00_MM)</c>; empty where it has none.
        /// </summary>
        /// <value>
        /// Set, as AutoCAD's <c>PlotSettingsValidator.SetCanonicalMediaName</c> sets it, to a name that ends in the size of the
        /// sheet in millimetres, width then height, as AutoCAD names the sheets of no plotter: the ISO sheets
        /// <c>ISO_A0_(1189.00_x_841.00_MM)</c>, <c>ISO_A1_(841.00_x_594.00_MM)</c>, <c>ISO_A2_(594.00_x_420.00_MM)</c>,
        /// <c>ISO_A3_(420.00_x_297.00_MM)</c> and <c>ISO_A4_(297.00_x_210.00_MM)</c> on their sides, and
        /// <c>ISO_A0_(841.00_x_1189.00_MM)</c> to <c>ISO_A4_(210.00_x_297.00_MM)</c> upright; a sheet of a size of its own as
        /// ezdxf's <c>page_setup</c> names one, <c>ezdxf_(1000.00_x_500.00_MM)</c>. The name is written as group 4 and its size
        /// as <see cref="PlotPaperSize"/> (44 and 45): a layout set to <c>ISO_A2_(594.00_x_420.00_MM)</c> has a sheet 594 by 420.
        /// The margins, the plot's origin and its turn are kept, and a layout's <see cref="DxfLayout.Limits"/> made the sheet
        /// about its plotted area again. The name it has changes nothing.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set does not end in a size in millimetres above nought, as
        /// <c>_(594.00_x_420.00_MM)</c>, after letters, digits, <c>_</c>, <c>.</c> and <c>-</c>; or the margins leave no room on
        /// a sheet of that size.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the settings are in no drawing, as a layout made in code before
        /// it is added, or are erased.</exception>
        public string CanonicalMediaName
        {
            get => Text(Marker, 4);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                Match match = MediaSize.Match(value);
                double width = match.Success ? Size(match.Groups[1].Value) : 0.0;
                double height = match.Success ? Size(match.Groups[2].Value) : 0.0;
                Guard.Argument(width > 0.0 && height > 0.0, nameof(value), "A sheet is named with its size in millimetres at its end, as ISO_A2_(594.00_x_420.00_MM): '" + value + "' is not one.");
                DxfFile file = InFile("be given a sheet");

                if (string.Equals(value, CanonicalMediaName, StringComparison.Ordinal))
                {
                    return;
                }

                CheckRoom(PlotPaperMargins, new GeoPoint2(width, height), nameof(value));
                GeoPoint2 size = PlotPaperSize;
                var values = new List<KeyValuePair<int, string>> { Pair(4, value) };
                Changed(values, 44, width, size.X);
                Changed(values, 45, height, size.Y);
                SetSheet(file, values);
            }
        }

        /// <summary>Gets the name of the view plotted where <see cref="PlotType"/> is a view (group 6), as AutoCAD's <c>PlotViewName</c>; empty where it has none.</summary>
        public string PlotViewName => Text(Marker, 6);

        /// <summary>Gets the name of the plot style table (group 7), as AutoCAD's <c>CurrentStyleSheet</c>, such as <c>monochrome.ctb</c>; empty where it has none.</summary>
        public string CurrentStyleSheet => Text(Marker, 7);

        /// <summary>
        /// Gets or sets the margins of the sheet, as AutoCAD's <c>PlotPaperMargins</c>: its <see cref="DxfExtents2d.MinPoint"/> the
        /// left (group 40) and bottom (41) margins, its <see cref="DxfExtents2d.MaxPoint"/> the right (42) and top (43), in
        /// millimetres.
        /// </summary>
        /// <value>
        /// Set, the four written in millimetres whatever <see cref="PlotPaperUnits"/> says, and a layout's
        /// <see cref="DxfLayout.Limits"/> made the sheet about its plotted area again: a layout of A2 on its side given margins of
        /// 10 all round has limits (-10, -10) to (584, 410). AutoCAD takes its margins from the plotter's sheet, which a DXF does
        /// not hold; a reader that has no plotter, as ezdxf and the library, takes these. The margins it has change nothing.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a margin set is not a finite number of nought or more.</exception>
        /// <exception cref="ArgumentException">Thrown when the margins set leave no room on the sheet: the left and right as
        /// wide as it or wider, or the bottom and top as high.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the settings are in no drawing, as a layout made in code before
        /// it is added, or are erased.</exception>
        public DxfExtents2d PlotPaperMargins
        {
            get => new DxfExtents2d(new GeoPoint2(Real(Marker, 40, 0.0), Real(Marker, 41, 0.0)), new GeoPoint2(Real(Marker, 42, 0.0), Real(Marker, 43, 0.0)));

            set
            {
                const string Message = "A margin is a finite number of millimetres, nought or more.";
                Guard.NonNegative(value.MinPoint.X, nameof(value), Message);
                Guard.NonNegative(value.MinPoint.Y, nameof(value), Message);
                Guard.NonNegative(value.MaxPoint.X, nameof(value), Message);
                Guard.NonNegative(value.MaxPoint.Y, nameof(value), Message);
                DxfFile file = InFile("be given margins");
                DxfExtents2d was = PlotPaperMargins;

                if (value.Equals(was))
                {
                    return;
                }

                CheckRoom(value, PlotPaperSize, nameof(value));
                var values = new List<KeyValuePair<int, string>>();
                Changed(values, 40, value.MinPoint.X, was.MinPoint.X);
                Changed(values, 41, value.MinPoint.Y, was.MinPoint.Y);
                Changed(values, 42, value.MaxPoint.X, was.MaxPoint.X);
                Changed(values, 43, value.MaxPoint.Y, was.MaxPoint.Y);
                SetSheet(file, values);
            }
        }

        /// <summary>
        /// Gets the size of the sheet (groups 44 and 45), as AutoCAD's <c>PlotPaperSize</c>, in millimetres: (420, 297) for A3 on
        /// its side. It is set with the sheet's name, <see cref="CanonicalMediaName"/>, as AutoCAD sets it.
        /// </summary>
        public GeoPoint2 PlotPaperSize => new GeoPoint2(Real(Marker, 44, 0.0), Real(Marker, 45, 0.0));

        /// <summary>Gets or sets how far the plot is moved on the sheet (groups 46 and 47), as AutoCAD's <c>PlotOrigin</c>, in millimetres.</summary>
        /// <value>
        /// Set, as AutoCAD's <c>PlotSettingsValidator.SetPlotOrigin</c> sets it, in millimetres whatever
        /// <see cref="PlotPaperUnits"/> says, and a layout's <see cref="DxfLayout.Limits"/> moved with it: the layout of A3 on its
        /// side moved by (5, 0) has limits (-12.5, -20) to (407.5, 277). The origin it has changes nothing.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the origin set is not of finite numbers.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the settings are in no drawing, as a layout made in code before
        /// it is added, or are erased.</exception>
        public GeoPoint2 PlotOrigin
        {
            get => new GeoPoint2(Real(Marker, 46, 0.0), Real(Marker, 47, 0.0));

            set
            {
                Guard.Finite(value.X, nameof(value), "The origin of a plot is of finite numbers of millimetres.");
                Guard.Finite(value.Y, nameof(value), "The origin of a plot is of finite numbers of millimetres.");
                DxfFile file = InFile("be moved on its sheet");
                GeoPoint2 was = PlotOrigin;
                var values = new List<KeyValuePair<int, string>>();
                Changed(values, 46, value.X, was.X);
                Changed(values, 47, value.Y, was.Y);

                if (values.Count > 0)
                {
                    SetSheet(file, values);
                }
            }
        }

        /// <summary>Gets the window plotted where <see cref="PlotType"/> is a window (groups 48 and 49, 140 and 141), as AutoCAD's <c>PlotWindowArea</c>.</summary>
        public DxfExtents2d PlotWindowArea => new DxfExtents2d(new GeoPoint2(Real(Marker, 48, 0.0), Real(Marker, 49, 0.0)), new GeoPoint2(Real(Marker, 140, 0.0), Real(Marker, 141, 0.0)));

        /// <summary>Gets the scale of the plot as a ratio (groups 142 and 143), as AutoCAD's <c>CustomPrintScale</c>; 1:1 where it gives none.</summary>
        public DxfCustomScale CustomPrintScale => new DxfCustomScale(Real(Marker, 142, 1.0), Real(Marker, 143, 1.0));

        /// <summary>Gets or sets the units the sheet is set out in (group 72), as AutoCAD's <c>PlotPaperUnits</c>; inches where it gives none.</summary>
        /// <value>
        /// Set, as AutoCAD's <c>PlotSettingsValidator.SetPlotPaperUnits</c> sets it; the sizes, margins and origin stay in
        /// millimetres, as the file writes them, and a layout's <see cref="DxfLayout.Limits"/> are given in the units set: the
        /// layout of A3 on its side set to inches has limits (-0.295, -0.787) to (16.240, 10.906), to three places. The units it
        /// has change nothing.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the units set are none of <see cref="DxfPlotPaperUnit"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the settings are in no drawing, as a layout made in code before
        /// it is added, or are erased.</exception>
        public DxfPlotPaperUnit PlotPaperUnits
        {
            get => (DxfPlotPaperUnit)Whole(Marker, 72, 0);

            set
            {
                Guard.Argument(Enum.IsDefined(typeof(DxfPlotPaperUnit), value), nameof(value), "The units of a sheet are inches (0), millimetres (1) or pixels (2).");
                DxfFile file = InFile("be given units");

                if (value != PlotPaperUnits)
                {
                    SetSheet(file, new List<KeyValuePair<int, string>> { Pair(72, ((int)value).ToString(CultureInfo.InvariantCulture)) });
                }
            }
        }

        /// <summary>Gets or sets how the plot is turned on its sheet (group 73), as AutoCAD's <c>PlotRotation</c>.</summary>
        /// <value>
        /// Set, as AutoCAD's <c>PlotSettingsValidator.SetPlotRotation</c> sets it. A layout turned a quarter or three quarters
        /// lies across its sheet: an upright sheet of A2, <c>ISO_A2_(420.00_x_594.00_MM)</c>, turned a quarter has
        /// <see cref="DxfLayout.Limits"/> 594 wide and 420 high, the left margin its sheet's bottom one and the bottom margin its
        /// left one. The turn it has changes nothing.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the turn set is none of <see cref="DxfPlotRotation"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the settings are in no drawing, as a layout made in code before
        /// it is added, or are erased.</exception>
        public DxfPlotRotation PlotRotation
        {
            get => (DxfPlotRotation)Whole(Marker, 73, 0);

            set
            {
                Guard.Argument(Enum.IsDefined(typeof(DxfPlotRotation), value), nameof(value), "A plot is turned by 0 (0), 90 (1), 180 (2) or 270 (3) degrees.");
                DxfFile file = InFile("be turned");

                if (value != PlotRotation)
                {
                    SetSheet(file, new List<KeyValuePair<int, string>> { Pair(73, ((int)value).ToString(CultureInfo.InvariantCulture)) });
                }
            }
        }

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

        /// <summary>The codes of the record's own groups in the order AutoCAD writes them, a 100 where each marker stands: those of AcDbPlotSettings.</summary>
        private protected virtual IReadOnlyList<int> GroupOrder => PlotOrder;

        /// <summary>The sheet set anew, its size, margins, origin, units or turn: nothing for a page setup; a layout's limits made to agree.</summary>
        private protected virtual void SheetSet(DxfFile file)
        {
        }

        private protected static KeyValuePair<int, string> Pair(int code, string value) => new KeyValuePair<int, string>(code, value);

        /// <summary>A number written as the file keeps it, exactly, a negative nought as nought.</summary>
        private protected static string Written(double value) => Numbers.Exact(value + 0.0);

        /// <summary>Adds a group of a number to those to write where the number is not the one the record gives, to the bit.</summary>
        private protected static void Changed(List<KeyValuePair<int, string>> values, int code, double now, double was)
        {
            if (BitConverter.DoubleToInt64Bits(now + 0.0) != BitConverter.DoubleToInt64Bits(was))
            {
                values.Add(Pair(code, Written(now)));
            }
        }

        /// <summary>The groups of the sheet written, the record once, then what follows from them.</summary>
        private void SetSheet(DxfFile file, List<KeyValuePair<int, string>> values)
        {
            file.SetValues(Item, values, GroupOrder, false);
            SheetSet(file);
        }

        /// <summary>A size of a sheet's name, as <c>594.00</c>; nought where it is not a finite number.</summary>
        private static double Size(string text)
            => double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value) && Guard.IsFinite(value) ? value : 0.0;

        /// <summary>Refuses margins that leave no room on a sheet of a size; a sheet of no size given takes any.</summary>
        private static void CheckRoom(DxfExtents2d margins, GeoPoint2 size, string parameter)
        {
            if (size.X > 0.0 && size.Y > 0.0)
            {
                Guard.Argument(
                    margins.MinPoint.X + margins.MaxPoint.X < size.X && margins.MinPoint.Y + margins.MaxPoint.Y < size.Y,
                    parameter,
                    "The margins leave no room on a sheet " + size.X.ToString(CultureInfo.InvariantCulture) + " by " + size.Y.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        /// <summary>
        /// The index of the first group of a code in the part of a subclass of the record, among the groups as
        /// <see cref="DxfItem.Groups"/> counts them: after the subclass's marker and before the next marker or the extended data,
        /// outside the groups of the applications; -1 where the part has none, or the settings are made in code and in no file.
        /// </summary>
        private protected int PartIndex(string marker, int code)
        {
            if (Item == null)
            {
                return -1;
            }

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
