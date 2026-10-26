using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The dimension variables AutoCAD's <c>Database</c> has as properties, those new dimensions take, each a variable of the
    /// HEADER section read and set as <see cref="Ltscale"/> is: <see cref="Dimasz"/> is <c>$DIMASZ</c>, <see cref="Dimfxlen"/>
    /// <c>$DIMFXL</c>. Where the header gives none, the value of AutoCAD's metric drawing (acadiso.dwt), which a new drawing of
    /// ezdxf 1.4.5 is given too: arrowheads and texts 2.5, extension lines 1.25 past the dimension line and 0.625 off the points,
    /// a gap of 0.625, two decimal places, a comma, trailing zeros left out, the text above the line. A variable R2000 (AC1015),
    /// R2004 (AC1018), R2007 (AC1021) or R2010 (AC1024) brought is refused in a file of an older version, as AutoCAD writes
    /// none there, and reads there as a dimension style of that file reads it, AutoCAD's own of acad.dwt (DIMDEC 4, DIMDSEP
    /// '.' and no text style in a file of R12), so that the header's data and a style's agree. Values are as AutoCAD's .NET gives them, as <see cref="DxfDimStyleTableRecord"/>'s are: a colour as a
    /// <see cref="DxfColor"/>, an arrowhead as the block it is drawn with (the header naming it), a linetype or the text style
    /// as its record; <see cref="Dimjogang"/> in radians, as the header holds it. <see cref="GetDimstyleData"/> gives them as a
    /// style in no table, and <see cref="SetDimstyleData"/> takes those of a style.
    /// </summary>
    public sealed partial class DxfDatabase
    {
        // The dimension variables of the header by AutoCAD's name: each its variable, group and version, what it holds, the
        // value of AutoCAD's metric drawing, and what AutoCAD takes, a range of whole numbers or a bound of reals.
        private static readonly Dictionary<string, DimHeader> DimHeaders = DimNamed(new Dictionary<string, DimHeader>(StringComparer.Ordinal)
        {
            ["Dimadec"] = DimWhole("$DIMADEC", 1015, 0, -1, 8),
            ["Dimalt"] = DimOf("$DIMALT", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimaltd"] = DimWhole("$DIMALTD", 1009, 3, 0, 8),
            ["Dimaltf"] = DimReal("$DIMALTF", 1009, 1.0 / 25.4, Bound.Finite),
            ["Dimaltrnd"] = DimReal("$DIMALTRND", 1015, 0.0, Bound.Finite),
            ["Dimalttd"] = DimWhole("$DIMALTTD", 1015, 3, 0, 8),
            ["Dimalttz"] = DimWhole("$DIMALTTZ", 1015, 0, 0, 15),
            ["Dimaltu"] = DimWhole("$DIMALTU", 1015, 2, 1, 8),
            ["Dimaltz"] = DimWhole("$DIMALTZ", 1015, 0, 0, 15),
            ["Dimapost"] = DimOf("$DIMAPOST", 1, 1009, DxfDimKind.Text, string.Empty),
            ["Dimarcsym"] = DimWhole("$DIMARCSYM", 1021, 0, 0, 2),
            ["Dimaso"] = DimOf("$DIMASO", 70, 1009, DxfDimKind.Flag, "1"),
            ["DimAssoc"] = DimWhole("$DIMASSOC", 1018, 2, 0, 2, 280),
            ["Dimasz"] = DimReal("$DIMASZ", 1009, 2.5, Bound.NonNegative),
            ["Dimatfit"] = DimWhole("$DIMATFIT", 1015, 3, 0, 3),
            ["Dimaunit"] = DimWhole("$DIMAUNIT", 1015, 0, 0, 3),
            ["Dimazin"] = DimWhole("$DIMAZIN", 1015, 0, 0, 3),
            ["Dimblk"] = DimOf("$DIMBLK", 1, 1009, DxfDimKind.Block, string.Empty),
            ["Dimblk1"] = DimOf("$DIMBLK1", 1, 1009, DxfDimKind.Block, string.Empty),
            ["Dimblk2"] = DimOf("$DIMBLK2", 1, 1009, DxfDimKind.Block, string.Empty),
            ["Dimcen"] = DimReal("$DIMCEN", 1009, 2.5, Bound.Finite),
            ["Dimclrd"] = DimOf("$DIMCLRD", 70, 1009, DxfDimKind.Colour, "0"),
            ["Dimclre"] = DimOf("$DIMCLRE", 70, 1009, DxfDimKind.Colour, "0"),
            ["Dimclrt"] = DimOf("$DIMCLRT", 70, 1009, DxfDimKind.Colour, "0"),
            ["Dimdec"] = DimWhole("$DIMDEC", 1015, 2, 0, 8),
            ["Dimdle"] = DimReal("$DIMDLE", 1009, 0.0, Bound.Finite),
            ["Dimdli"] = DimReal("$DIMDLI", 1009, 3.75, Bound.Finite),
            ["Dimdsep"] = DimOf("$DIMDSEP", 70, 1015, DxfDimKind.Letter, "44"),
            ["Dimexe"] = DimReal("$DIMEXE", 1009, 1.25, Bound.Finite),
            ["Dimexo"] = DimReal("$DIMEXO", 1009, 0.625, Bound.Finite),
            ["Dimfrac"] = DimWhole("$DIMFRAC", 1015, 0, 0, 2),
            ["Dimfxlen"] = DimReal("$DIMFXL", 1021, 1.0, Bound.Finite),
            ["DimfxlenOn"] = DimOf("$DIMFXLON", 70, 1021, DxfDimKind.Flag, "0"),
            ["Dimgap"] = DimReal("$DIMGAP", 1009, 0.625, Bound.Finite),
            ["Dimjogang"] = DimReal("$DIMJOGANG", 1021, Math.PI / 4.0, Bound.Finite),
            ["Dimjust"] = DimWhole("$DIMJUST", 1015, 0, 0, 4),
            ["Dimldrblk"] = DimOf("$DIMLDRBLK", 1, 1015, DxfDimKind.Block, string.Empty),
            ["Dimlfac"] = DimReal("$DIMLFAC", 1009, 1.0, Bound.Finite),
            ["Dimlim"] = DimOf("$DIMLIM", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimltex1"] = DimOf("$DIMLTEX1", 6, 1021, DxfDimKind.Linetype, string.Empty),
            ["Dimltex2"] = DimOf("$DIMLTEX2", 6, 1021, DxfDimKind.Linetype, string.Empty),
            ["Dimltype"] = DimOf("$DIMLTYPE", 6, 1021, DxfDimKind.Linetype, string.Empty),
            ["Dimlunit"] = DimWhole("$DIMLUNIT", 1015, 2, 1, 6),
            ["Dimlwd"] = DimOf("$DIMLWD", 70, 1015, DxfDimKind.Weight, "-2"),
            ["Dimlwe"] = DimOf("$DIMLWE", 70, 1015, DxfDimKind.Weight, "-2"),
            ["Dimpost"] = DimOf("$DIMPOST", 1, 1009, DxfDimKind.Text, string.Empty),
            ["Dimrnd"] = DimReal("$DIMRND", 1009, 0.0, Bound.Finite),
            ["Dimsah"] = DimOf("$DIMSAH", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimscale"] = DimReal("$DIMSCALE", 1009, 1.0, Bound.NonNegative),
            ["Dimsd1"] = DimOf("$DIMSD1", 70, 1015, DxfDimKind.Flag, "0"),
            ["Dimsd2"] = DimOf("$DIMSD2", 70, 1015, DxfDimKind.Flag, "0"),
            ["Dimse1"] = DimOf("$DIMSE1", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimse2"] = DimOf("$DIMSE2", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimsho"] = DimOf("$DIMSHO", 70, 1009, DxfDimKind.Flag, "1"),
            ["Dimsoxd"] = DimOf("$DIMSOXD", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimtad"] = DimWhole("$DIMTAD", 1009, 1, 0, 4),
            ["Dimtdec"] = DimWhole("$DIMTDEC", 1015, 2, 0, 8),
            ["Dimtfac"] = DimReal("$DIMTFAC", 1009, 1.0, Bound.Finite),
            ["Dimtfill"] = DimWhole("$DIMTFILL", 1021, 0, 0, 2),
            ["Dimtfillclr"] = DimOf("$DIMTFILLCLR", 70, 1021, DxfDimKind.Colour, "0"),
            ["Dimtih"] = DimOf("$DIMTIH", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimtix"] = DimOf("$DIMTIX", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimtm"] = DimReal("$DIMTM", 1009, 0.0, Bound.Finite),
            ["Dimtmove"] = DimWhole("$DIMTMOVE", 1015, 0, 0, 2),
            ["Dimtofl"] = DimOf("$DIMTOFL", 70, 1009, DxfDimKind.Flag, "1"),
            ["Dimtoh"] = DimOf("$DIMTOH", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimtol"] = DimOf("$DIMTOL", 70, 1009, DxfDimKind.Flag, "0"),
            ["Dimtolj"] = DimWhole("$DIMTOLJ", 1015, 0, 0, 2),
            ["Dimtp"] = DimReal("$DIMTP", 1009, 0.0, Bound.Finite),
            ["Dimtsz"] = DimReal("$DIMTSZ", 1009, 0.0, Bound.Finite),
            ["Dimtvp"] = DimReal("$DIMTVP", 1009, 0.0, Bound.Finite),
            ["Dimtxsty"] = DimOf("$DIMTXSTY", 7, 1015, DxfDimKind.TextStyle, "Standard"),
            ["Dimtxt"] = DimReal("$DIMTXT", 1009, 2.5, Bound.NonNegative),
            ["Dimtxtdirection"] = DimOf("$DIMTXTDIRECTION", 70, 1024, DxfDimKind.Flag, "0"),
            ["Dimtzin"] = DimWhole("$DIMTZIN", 1015, 8, 0, 15),
            ["Dimupt"] = DimOf("$DIMUPT", 70, 1015, DxfDimKind.Flag, "0"),
            ["Dimzin"] = DimWhole("$DIMZIN", 1009, 8, 0, 15),
        });

        /// <summary>Gets or sets the decimal places of the angles of new angular dimensions (<c>$DIMADEC</c>), as AutoCAD's <c>Dimadec</c>: -1 for those of <see cref="Dimdec"/>; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from -1 to 8.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimadec { get => (int)Dim("Dimadec"); set => SetDim("Dimadec", value); }

        /// <summary>Gets or sets whether new dimensions show their measurement in alternate units as well (<c>$DIMALT</c>), as AutoCAD's <c>Dimalt</c>; false where the header gives none.</summary>
        public bool Dimalt { get => (bool)Dim("Dimalt"); set => SetDim("Dimalt", value); }

        /// <summary>Gets or sets the decimal places of alternate units (<c>$DIMALTD</c>), as AutoCAD's <c>Dimaltd</c>; 3 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 8.</exception>
        public int Dimaltd { get => (int)Dim("Dimaltd"); set => SetDim("Dimaltd", value); }

        /// <summary>
        /// Gets or sets the factor a measurement is multiplied by in alternate units (<c>$DIMALTF</c>), as AutoCAD's
        /// <c>Dimaltf</c>: 25.4 for inches shown as millimetres; 1 / 25.4, millimetres shown as inches, where the header gives
        /// none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is not a finite number.</exception>
        public double Dimaltf { get => (double)Dim("Dimaltf"); set => SetDim("Dimaltf", value); }

        /// <summary>Gets or sets what alternate units are rounded to (<c>$DIMALTRND</c>), as AutoCAD's <c>Dimaltrnd</c>; nought, no rounding, where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public double Dimaltrnd { get => (double)Dim("Dimaltrnd"); set => SetDim("Dimaltrnd", value); }

        /// <summary>Gets or sets the decimal places of the tolerances in alternate units (<c>$DIMALTTD</c>), as AutoCAD's <c>Dimalttd</c>; 3 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 8.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimalttd { get => (int)Dim("Dimalttd"); set => SetDim("Dimalttd", value); }

        /// <summary>Gets or sets which zeros of the tolerances in alternate units are left out (<c>$DIMALTTZ</c>), as AutoCAD's <c>Dimalttz</c>, as <see cref="Dimzin"/> says; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 15.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimalttz { get => (int)Dim("Dimalttz"); set => SetDim("Dimalttz", value); }

        /// <summary>
        /// Gets or sets the units of alternate units (<c>$DIMALTU</c>), as AutoCAD's <c>Dimaltu</c>: 1 scientific, 2 decimal,
        /// 3 engineering, 4 architectural stacked, 5 fractional stacked, 6 architectural, 7 fractional, 8 the Windows desktop's;
        /// 2 where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 1 to 8.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimaltu { get => (int)Dim("Dimaltu"); set => SetDim("Dimaltu", value); }

        /// <summary>Gets or sets which zeros of alternate units are left out (<c>$DIMALTZ</c>), as AutoCAD's <c>Dimaltz</c>, as <see cref="Dimzin"/> says; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 15.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimaltz { get => (int)Dim("Dimaltz"); set => SetDim("Dimaltz", value); }

        /// <summary>Gets or sets the prefix and suffix of alternate units (<c>$DIMAPOST</c>), as AutoCAD's <c>Dimapost</c>: <c>[] in</c> writes the alternate measurement then " in"; empty where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string Dimapost { get => (string)Dim("Dimapost"); set => SetDim("Dimapost", value); }

        /// <summary>Gets or sets where the arc symbol of a new arc length dimension stands (<c>$DIMARCSYM</c>), as AutoCAD's <c>Dimarcsym</c>: 0 before the text, 1 above it, 2 none; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int Dimarcsym { get => (int)Dim("Dimarcsym"); set => SetDim("Dimarcsym", value); }

        /// <summary>Gets or sets whether new dimensions are made whole, as one entity, rather than of lines and texts (<c>$DIMASO</c>), as AutoCAD's <c>Dimaso</c>, which <see cref="DimAssoc"/> has taken over; true where the header gives none.</summary>
        public bool Dimaso { get => (bool)Dim("Dimaso"); set => SetDim("Dimaso", value); }

        /// <summary>
        /// Gets or sets how new dimensions are tied to what they measure (<c>$DIMASSOC</c>, group 280), as AutoCAD's
        /// <c>DimAssoc</c>: 0 exploded into lines and texts, 1 one entity tied to nothing, 2 one entity that follows the points
        /// it measures; 2 where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int DimAssoc { get => (int)Dim("DimAssoc"); set => SetDim("DimAssoc", value); }

        /// <summary>Gets or sets the size of the arrowheads (<c>$DIMASZ</c>), as AutoCAD's <c>Dimasz</c>; 2.5 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not a finite number of nought or more.</exception>
        public double Dimasz { get => (double)Dim("Dimasz"); set => SetDim("Dimasz", value); }

        /// <summary>Gets or sets what is moved out between the extension lines where text and arrows do not both fit (<c>$DIMATFIT</c>), as AutoCAD's <c>Dimatfit</c>: 0 both, 1 the arrows, 2 the text, 3 the one that fits worse; 3 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimatfit { get => (int)Dim("Dimatfit"); set => SetDim("Dimatfit", value); }

        /// <summary>Gets or sets the units of angles (<c>$DIMAUNIT</c>), as AutoCAD's <c>Dimaunit</c>: 0 decimal degrees, 1 degrees, minutes and seconds, 2 grads, 3 radians; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimaunit { get => (int)Dim("Dimaunit"); set => SetDim("Dimaunit", value); }

        /// <summary>Gets or sets which zeros of angles are left out (<c>$DIMAZIN</c>), as AutoCAD's <c>Dimazin</c>: 1 leading, 2 trailing, 3 both; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimazin { get => (int)Dim("Dimazin"); set => SetDim("Dimazin", value); }

        /// <summary>
        /// Gets or sets the block both arrowheads are drawn with (<c>$DIMBLK</c>, by its name), as AutoCAD's <c>Dimblk</c> gives
        /// its id: <c>_ARCHTICK</c>, the header naming it <c>_ARCHTICK</c> or <c>ARCHTICK</c>; null for AutoCAD's closed filled
        /// arrowhead, where the header names none, or a block the drawing has not.
        /// </summary>
        /// <value>Set, a block of the drawing, its name written; null writes none.</value>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, erased, or of another drawing.</exception>
        public DxfBlockTableRecord Dimblk { get => (DxfBlockTableRecord)Dim("Dimblk"); set => SetDim("Dimblk", value); }

        /// <summary>Gets or sets the block of the first arrowhead where <see cref="Dimsah"/> draws them apart (<c>$DIMBLK1</c>, by its name), as AutoCAD's <c>Dimblk1</c>; null for the closed filled one.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, erased, or of another drawing.</exception>
        public DxfBlockTableRecord Dimblk1 { get => (DxfBlockTableRecord)Dim("Dimblk1"); set => SetDim("Dimblk1", value); }

        /// <summary>Gets or sets the block of the second arrowhead where <see cref="Dimsah"/> draws them apart (<c>$DIMBLK2</c>, by its name), as AutoCAD's <c>Dimblk2</c>; null for the closed filled one.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, erased, or of another drawing.</exception>
        public DxfBlockTableRecord Dimblk2 { get => (DxfBlockTableRecord)Dim("Dimblk2"); set => SetDim("Dimblk2", value); }

        /// <summary>Gets or sets the centre marks of radial dimensions (<c>$DIMCEN</c>), as AutoCAD's <c>Dimcen</c>: above nought a mark that long, below nought a centre line, nought none; 2.5 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not a finite number.</exception>
        public double Dimcen { get => (double)Dim("Dimcen"); set => SetDim("Dimcen", value); }

        /// <summary>Gets or sets the colour of the dimension lines (<c>$DIMCLRD</c>), as AutoCAD's <c>Dimclrd</c>; BYBLOCK where the header gives none.</summary>
        /// <value>Set, a colour of the index, BYLAYER or BYBLOCK.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour, which the variable does not hold.</exception>
        public DxfColor Dimclrd { get => (DxfColor)Dim("Dimclrd"); set => SetDim("Dimclrd", value); }

        /// <summary>Gets or sets the colour of the extension lines (<c>$DIMCLRE</c>), as AutoCAD's <c>Dimclre</c>; BYBLOCK where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        public DxfColor Dimclre { get => (DxfColor)Dim("Dimclre"); set => SetDim("Dimclre", value); }

        /// <summary>Gets or sets the colour of the texts (<c>$DIMCLRT</c>), as AutoCAD's <c>Dimclrt</c>; BYBLOCK where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        public DxfColor Dimclrt { get => (DxfColor)Dim("Dimclrt"); set => SetDim("Dimclrt", value); }

        /// <summary>Gets or sets the decimal places of measurements (<c>$DIMDEC</c>), as AutoCAD's <c>Dimdec</c>; 2 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 8.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimdec { get => (int)Dim("Dimdec"); set => SetDim("Dimdec", value); }

        /// <summary>Gets or sets how far the dimension line runs past the extension lines where it ends in ticks (<c>$DIMDLE</c>), as AutoCAD's <c>Dimdle</c>; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number.</exception>
        public double Dimdle { get => (double)Dim("Dimdle"); set => SetDim("Dimdle", value); }

        /// <summary>Gets or sets how far apart baseline dimensions stand (<c>$DIMDLI</c>), as AutoCAD's <c>Dimdli</c>; 3.75 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing set is not a finite number.</exception>
        public double Dimdli { get => (double)Dim("Dimdli"); set => SetDim("Dimdli", value); }

        /// <summary>Gets or sets the decimal separator (<c>$DIMDSEP</c>, the letter's number), as AutoCAD's <c>Dimdsep</c>: '.' is 46; ',' (44) where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public char Dimdsep { get => (char)Dim("Dimdsep"); set => SetDim("Dimdsep", value); }

        /// <summary>Gets or sets how far the extension lines run past the dimension line (<c>$DIMEXE</c>), as AutoCAD's <c>Dimexe</c>; 1.25 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number.</exception>
        public double Dimexe { get => (double)Dim("Dimexe"); set => SetDim("Dimexe", value); }

        /// <summary>Gets or sets how far the extension lines stand off the points measured (<c>$DIMEXO</c>), as AutoCAD's <c>Dimexo</c>; 0.625 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset set is not a finite number.</exception>
        public double Dimexo { get => (double)Dim("Dimexo"); set => SetDim("Dimexo", value); }

        /// <summary>Gets or sets how fractions are stacked in architectural and fractional units (<c>$DIMFRAC</c>), as AutoCAD's <c>Dimfrac</c>: 0 across, 1 aslant, 2 not stacked; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimfrac { get => (int)Dim("Dimfrac"); set => SetDim("Dimfrac", value); }

        /// <summary>Gets or sets the fixed length of the extension lines where <see cref="DimfxlenOn"/> (<c>$DIMFXL</c>), as AutoCAD's <c>Dimfxlen</c>; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double Dimfxlen { get => (double)Dim("Dimfxlen"); set => SetDim("Dimfxlen", value); }

        /// <summary>Gets or sets whether the extension lines are of the fixed length <see cref="Dimfxlen"/> (<c>$DIMFXLON</c>), as AutoCAD's <c>DimfxlenOn</c>; false where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public bool DimfxlenOn { get => (bool)Dim("DimfxlenOn"); set => SetDim("DimfxlenOn", value); }

        /// <summary>Gets or sets the gap about the text of a dimension (<c>$DIMGAP</c>), as AutoCAD's <c>Dimgap</c>: below nought a box drawn about it; 0.625 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the gap set is not a finite number.</exception>
        public double Dimgap { get => (double)Dim("Dimgap"); set => SetDim("Dimgap", value); }

        /// <summary>
        /// Gets or sets the angle of the jog of a jogged radius dimension (<c>$DIMJOGANG</c>), as AutoCAD's <c>Dimjogang</c>: in
        /// radians, as the header holds it, so 60 degrees is pi / 3; 45 degrees, pi / 4, where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double Dimjogang { get => (double)Dim("Dimjogang"); set => SetDim("Dimjogang", value); }

        /// <summary>Gets or sets where the text stands along the dimension line (<c>$DIMJUST</c>), as AutoCAD's <c>Dimjust</c>: 0 centred, 1 by the first extension line, 2 by the second, 3 and 4 above them; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 4.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimjust { get => (int)Dim("Dimjust"); set => SetDim("Dimjust", value); }

        /// <summary>Gets or sets the block the arrowhead of a leader is drawn with (<c>$DIMLDRBLK</c>, by its name), as AutoCAD's <c>Dimldrblk</c>; null for the closed filled one.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, erased, or of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public DxfBlockTableRecord Dimldrblk { get => (DxfBlockTableRecord)Dim("Dimldrblk"); set => SetDim("Dimldrblk", value); }

        /// <summary>Gets or sets the factor a length measured is multiplied by (<c>$DIMLFAC</c>), as AutoCAD's <c>Dimlfac</c>: 100 for a detail drawn at 1:100; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is not a finite number.</exception>
        public double Dimlfac { get => (double)Dim("Dimlfac"); set => SetDim("Dimlfac", value); }

        /// <summary>Gets or sets whether a measurement is shown as its limits, <see cref="Dimtp"/> and <see cref="Dimtm"/> about it (<c>$DIMLIM</c>), as AutoCAD's <c>Dimlim</c>; false where the header gives none.</summary>
        public bool Dimlim { get => (bool)Dim("Dimlim"); set => SetDim("Dimlim", value); }

        /// <summary>Gets or sets the linetype of the first extension line (<c>$DIMLTEX1</c>, by its name), as AutoCAD's <c>Dimltex1</c>; null where the header names none, or one the drawing has not.</summary>
        /// <exception cref="ArgumentException">Thrown when the linetype set is erased, or of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfLinetypeTableRecord Dimltex1 { get => (DxfLinetypeTableRecord)Dim("Dimltex1"); set => SetDim("Dimltex1", value); }

        /// <summary>Gets or sets the linetype of the second extension line (<c>$DIMLTEX2</c>, by its name), as AutoCAD's <c>Dimltex2</c>; null where the header names none, or one the drawing has not.</summary>
        /// <exception cref="ArgumentException">Thrown when the linetype set is erased, or of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfLinetypeTableRecord Dimltex2 { get => (DxfLinetypeTableRecord)Dim("Dimltex2"); set => SetDim("Dimltex2", value); }

        /// <summary>Gets or sets the linetype of the dimension line (<c>$DIMLTYPE</c>, by its name), as AutoCAD's <c>Dimltype</c>; null where the header names none, or one the drawing has not.</summary>
        /// <exception cref="ArgumentException">Thrown when the linetype set is erased, or of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfLinetypeTableRecord Dimltype { get => (DxfLinetypeTableRecord)Dim("Dimltype"); set => SetDim("Dimltype", value); }

        /// <summary>Gets or sets the units of lengths (<c>$DIMLUNIT</c>), as AutoCAD's <c>Dimlunit</c>: 1 scientific, 2 decimal, 3 engineering, 4 architectural, 5 fractional, 6 the Windows desktop's; 2 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 1 to 6.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimlunit { get => (int)Dim("Dimlunit"); set => SetDim("Dimlunit", value); }

        /// <summary>Gets or sets the lineweight of the dimension lines (<c>$DIMLWD</c>), as AutoCAD's <c>Dimlwd</c>; <see cref="DxfLineWeight.ByBlock"/> where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight set is not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public DxfLineWeight Dimlwd { get => (DxfLineWeight)Dim("Dimlwd"); set => SetDim("Dimlwd", value); }

        /// <summary>Gets or sets the lineweight of the extension lines (<c>$DIMLWE</c>), as AutoCAD's <c>Dimlwe</c>; <see cref="DxfLineWeight.ByBlock"/> where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight set is not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public DxfLineWeight Dimlwe { get => (DxfLineWeight)Dim("Dimlwe"); set => SetDim("Dimlwe", value); }

        /// <summary>Gets or sets the prefix and suffix of measurements (<c>$DIMPOST</c>), as AutoCAD's <c>Dimpost</c>: <c>&lt;&gt; mm</c> writes the measurement then " mm", <c>mm</c> alone the same with no space; empty where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string Dimpost { get => (string)Dim("Dimpost"); set => SetDim("Dimpost", value); }

        /// <summary>Gets or sets what measurements are rounded to (<c>$DIMRND</c>), as AutoCAD's <c>Dimrnd</c>: 0.5 rounds 12.3 to 12.5; nought, no rounding, where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        public double Dimrnd { get => (double)Dim("Dimrnd"); set => SetDim("Dimrnd", value); }

        /// <summary>Gets or sets whether the two arrowheads are drawn apart, by <see cref="Dimblk1"/> and <see cref="Dimblk2"/> (<c>$DIMSAH</c>), as AutoCAD's <c>Dimsah</c>; false where the header gives none.</summary>
        public bool Dimsah { get => (bool)Dim("Dimsah"); set => SetDim("Dimsah", value); }

        /// <summary>Gets or sets the scale of every size of new dimensions (<c>$DIMSCALE</c>), as AutoCAD's <c>Dimscale</c>: 100 for a drawing plotted at 1:100, nought for the scale of the viewport in paper space; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number of nought or more.</exception>
        public double Dimscale { get => (double)Dim("Dimscale"); set => SetDim("Dimscale", value); }

        /// <summary>Gets or sets whether the first half of the dimension line is left out (<c>$DIMSD1</c>), as AutoCAD's <c>Dimsd1</c>; false where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public bool Dimsd1 { get => (bool)Dim("Dimsd1"); set => SetDim("Dimsd1", value); }

        /// <summary>Gets or sets whether the second half of the dimension line is left out (<c>$DIMSD2</c>), as AutoCAD's <c>Dimsd2</c>; false where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public bool Dimsd2 { get => (bool)Dim("Dimsd2"); set => SetDim("Dimsd2", value); }

        /// <summary>Gets or sets whether the first extension line is left out (<c>$DIMSE1</c>), as AutoCAD's <c>Dimse1</c>; false where the header gives none.</summary>
        public bool Dimse1 { get => (bool)Dim("Dimse1"); set => SetDim("Dimse1", value); }

        /// <summary>Gets or sets whether the second extension line is left out (<c>$DIMSE2</c>), as AutoCAD's <c>Dimse2</c>; false where the header gives none.</summary>
        public bool Dimse2 { get => (bool)Dim("Dimse2"); set => SetDim("Dimse2", value); }

        /// <summary>Gets or sets whether a dimension being dragged is drawn anew as it goes (<c>$DIMSHO</c>), as AutoCAD's <c>Dimsho</c>, which AutoCAD no longer reads; true where the header gives none.</summary>
        public bool Dimsho { get => (bool)Dim("Dimsho"); set => SetDim("Dimsho", value); }

        /// <summary>Gets or sets whether arrowheads that do not fit between the extension lines are left out (<c>$DIMSOXD</c>), as AutoCAD's <c>Dimsoxd</c>; false where the header gives none.</summary>
        public bool Dimsoxd { get => (bool)Dim("Dimsoxd"); set => SetDim("Dimsoxd", value); }

        /// <summary>Gets or sets where the text stands across the dimension line (<c>$DIMTAD</c>), as AutoCAD's <c>Dimtad</c>: 0 centred on it, 1 above it, 2 on the side away from the points, 3 as JIS, 4 below; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 4.</exception>
        public int Dimtad { get => (int)Dim("Dimtad"); set => SetDim("Dimtad", value); }

        /// <summary>Gets or sets the decimal places of tolerances (<c>$DIMTDEC</c>), as AutoCAD's <c>Dimtdec</c>; 2 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 8.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimtdec { get => (int)Dim("Dimtdec"); set => SetDim("Dimtdec", value); }

        /// <summary>Gets or sets the height of tolerances and fractions for that of the text (<c>$DIMTFAC</c>), as AutoCAD's <c>Dimtfac</c>: 0.8 makes them 2 high under a text 2.5 high; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is not a finite number.</exception>
        public double Dimtfac { get => (double)Dim("Dimtfac"); set => SetDim("Dimtfac", value); }

        /// <summary>Gets or sets the background of the text (<c>$DIMTFILL</c>), as AutoCAD's <c>Dimtfill</c>: 0 none, 1 the drawing's background, 2 <see cref="Dimtfillclr"/>; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int Dimtfill { get => (int)Dim("Dimtfill"); set => SetDim("Dimtfill", value); }

        /// <summary>Gets or sets the colour of the background of the text where <see cref="Dimtfill"/> is 2 (<c>$DIMTFILLCLR</c>), as AutoCAD's <c>Dimtfillclr</c>; BYBLOCK where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfColor Dimtfillclr { get => (DxfColor)Dim("Dimtfillclr"); set => SetDim("Dimtfillclr", value); }

        /// <summary>Gets or sets whether a text between the extension lines stands level (<c>$DIMTIH</c>), as AutoCAD's <c>Dimtih</c>; false, along the dimension line, where the header gives none.</summary>
        public bool Dimtih { get => (bool)Dim("Dimtih"); set => SetDim("Dimtih", value); }

        /// <summary>Gets or sets whether the text is kept between the extension lines (<c>$DIMTIX</c>), as AutoCAD's <c>Dimtix</c>; false where the header gives none.</summary>
        public bool Dimtix { get => (bool)Dim("Dimtix"); set => SetDim("Dimtix", value); }

        /// <summary>Gets or sets the lower tolerance (<c>$DIMTM</c>), as AutoCAD's <c>Dimtm</c>; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        public double Dimtm { get => (double)Dim("Dimtm"); set => SetDim("Dimtm", value); }

        /// <summary>Gets or sets what moving the text does (<c>$DIMTMOVE</c>), as AutoCAD's <c>Dimtmove</c>: 0 moves the dimension line with it, 1 adds a leader, 2 moves it freely; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimtmove { get => (int)Dim("Dimtmove"); set => SetDim("Dimtmove", value); }

        /// <summary>Gets or sets whether a dimension line is drawn between the extension lines though the text stands outside (<c>$DIMTOFL</c>), as AutoCAD's <c>Dimtofl</c>; true where the header gives none.</summary>
        public bool Dimtofl { get => (bool)Dim("Dimtofl"); set => SetDim("Dimtofl", value); }

        /// <summary>Gets or sets whether a text outside the extension lines stands level (<c>$DIMTOH</c>), as AutoCAD's <c>Dimtoh</c>; false, along the dimension line, where the header gives none.</summary>
        public bool Dimtoh { get => (bool)Dim("Dimtoh"); set => SetDim("Dimtoh", value); }

        /// <summary>Gets or sets whether tolerances are shown after the measurement (<c>$DIMTOL</c>), as AutoCAD's <c>Dimtol</c>; false where the header gives none.</summary>
        public bool Dimtol { get => (bool)Dim("Dimtol"); set => SetDim("Dimtol", value); }

        /// <summary>Gets or sets where tolerances stand against the measurement (<c>$DIMTOLJ</c>), as AutoCAD's <c>Dimtolj</c>: 0 bottom, 1 middle, 2 top; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimtolj { get => (int)Dim("Dimtolj"); set => SetDim("Dimtolj", value); }

        /// <summary>Gets or sets the upper tolerance (<c>$DIMTP</c>), as AutoCAD's <c>Dimtp</c>; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        public double Dimtp { get => (double)Dim("Dimtp"); set => SetDim("Dimtp", value); }

        /// <summary>Gets or sets the size of the ticks drawn in place of arrowheads (<c>$DIMTSZ</c>), as AutoCAD's <c>Dimtsz</c>: nought draws arrowheads; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not a finite number.</exception>
        public double Dimtsz { get => (double)Dim("Dimtsz"); set => SetDim("Dimtsz", value); }

        /// <summary>Gets or sets how far a centred text stands above or below the dimension line, in heights of the text (<c>$DIMTVP</c>), as AutoCAD's <c>Dimtvp</c>; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        public double Dimtvp { get => (double)Dim("Dimtvp"); set => SetDim("Dimtvp", value); }

        /// <summary>
        /// Gets or sets the text style of the texts (<c>$DIMTXSTY</c>, by its name), as AutoCAD's <c>Dimtxsty</c> gives its id:
        /// Standard where the header names none; null where the drawing has no style of the name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the style set is null, as AutoCAD's names one always; so
        /// <c>db.Dimtxsty = db.Dimtxsty</c> throws where the header names a style the drawing has not, which reads as null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is erased, or of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public DxfTextStyleTableRecord Dimtxsty { get => (DxfTextStyleTableRecord)Dim("Dimtxsty"); set => SetDim("Dimtxsty", value); }

        /// <summary>Gets or sets the height of the texts (<c>$DIMTXT</c>), as AutoCAD's <c>Dimtxt</c>; 2.5 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number of nought or more.</exception>
        public double Dimtxt { get => (double)Dim("Dimtxt"); set => SetDim("Dimtxt", value); }

        /// <summary>Gets or sets whether the text of new dimensions reads from right to left (<c>$DIMTXTDIRECTION</c>), as AutoCAD's <c>Dimtxtdirection</c>: true for Arabic or Hebrew; false where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2010 (AC1024), which has no such variable.</exception>
        public bool Dimtxtdirection { get => (bool)Dim("Dimtxtdirection"); set => SetDim("Dimtxtdirection", value); }

        /// <summary>Gets or sets which zeros of tolerances are left out (<c>$DIMTZIN</c>), as AutoCAD's <c>Dimtzin</c>, as <see cref="Dimzin"/> says; 8, the trailing ones, where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 15.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Dimtzin { get => (int)Dim("Dimtzin"); set => SetDim("Dimtzin", value); }

        /// <summary>Gets or sets whether the text is placed by the user as the dimension line is (<c>$DIMUPT</c>), as AutoCAD's <c>Dimupt</c>; false where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public bool Dimupt { get => (bool)Dim("Dimupt"); set => SetDim("Dimupt", value); }

        /// <summary>
        /// Gets or sets which zeros of measurements are left out (<c>$DIMZIN</c>), as AutoCAD's <c>Dimzin</c>: 4 the leading
        /// ones, so that 0.50 is .50, 8 the trailing ones, so that 12.50 is 12.5, and of feet and inches 0 nought feet and nought
        /// inches, 1 neither, 2 nought inches, 3 nought feet; 8 where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 15.</exception>
        public int Dimzin { get => (int)Dim("Dimzin"); set => SetDim("Dimzin", value); }

        /// <summary>
        /// Gets the dimension variables new dimensions take, as AutoCAD's <c>GetDimstyleData</c>: a new
        /// <see cref="DxfDimStyleTableRecord"/> in no table, named as <see cref="Dimstyle"/>, each of its 74 variables the header's,
        /// as the properties of the drawing read them. Of ezdxf's drawing of R2018, <c>Dimasz</c> 2.5 and <c>Dimdec</c> 2.
        /// </summary>
        /// <returns>The record, to be read, given to a dimension or a leader by their <c>SetDimstyleData</c>, or added to the <see cref="DimStyleTable"/>.</returns>
        public DxfDimStyleTableRecord GetDimstyleData()
        {
            var data = new DxfDimStyleTableRecord { Name = Dimstyle };

            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                data.Keep(variable, Dim(variable.Name));
            }

            return data;
        }

        /// <summary>
        /// Makes the variables of a dimension style those new dimensions take, as AutoCAD's <c>SetDimstyleData</c>: each that
        /// differs from the header's written into it, those it holds already writing nothing; <see cref="Dimstyle"/> is left as
        /// it is. A variable the header of the file's version has not is passed over whatever the record holds, as AutoCAD's
        /// SAVEAS to that version leaves it out: DIMDEC in a file of R12, and DIMARCSYM, which a style holds from R2000 and the
        /// header from R2007, in one of R2000 (a dimension's and a leader's <c>SetDimstyleData</c> refuse such a variable where
        /// the record holds another value than AutoCAD's own). A whole number is checked against AutoCAD's range, as each
        /// property checks it. A record that names no text style leaves <see cref="Dimtxsty"/> as it is. Every value is checked
        /// before any is written.
        /// </summary>
        /// <param name="style">The record: of the <see cref="DimStyleTable"/>, or made in code, as <see cref="GetDimstyleData"/> gives one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the record is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a value of the record is one AutoCAD does not take, a <see cref="Dimdec"/> of 9.</exception>
        /// <exception cref="ArgumentException">Thrown when a block, linetype or text style of the record is of another drawing.</exception>
        public void SetDimstyleData(DxfDimStyleTableRecord style)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            var changes = new List<KeyValuePair<DimHeader, string>>();

            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                DimHeader header = DimHeaders[variable.Name];
                object value = style.Get(variable);

                if ((value == null && header.Kind == DxfDimKind.TextStyle) || !File.Supports(header.Since))
                {
                    continue;
                }

                string text = DimChange(header, value, nameof(style));

                if (text != null)
                {
                    changes.Add(new KeyValuePair<DimHeader, string>(header, text));
                }
            }

            foreach (KeyValuePair<DimHeader, string> change in changes)
            {
                Set(change.Key.Variable, change.Key.Code, change.Value);
            }
        }

        private object Dim(string name) => DimRead(DimHeaders[name], this);

        private void SetDim(string name, object value)
        {
            DimHeader header = DimHeaders[name];
            string text = DimChange(header, value, nameof(value));

            if (text != null)
            {
                Set(header.Variable, header.Code, text);
            }
        }

        /// <summary>
        /// Refuses a value of a dimension variable, set on a dimension or a leader as a style's, that AutoCAD does not take in the
        /// header: a whole number out of its range, a size of the arrows, the text or the scale below nought.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of AutoCAD's range.</exception>
        internal static void CheckDimRange(DxfDimVariable variable, object value, string parameter)
        {
            DimHeader header = DimHeaders[variable.Name];

            if (header.Kind == DxfDimKind.Whole)
            {
                InRange((int)value, header.Least, header.Most, parameter);
            }
            else if (header.Kind == DxfDimKind.Real && header.Bound == Bound.NonNegative)
            {
                Guard.NonNegative((double)value, parameter, header.Variable.Substring(1) + " has to be a finite number of nought or more.");
            }
        }

        /// <summary>
        /// The value of a dimension variable of the header, of the kind AutoCAD's .NET gives; the value of AutoCAD's metric
        /// drawing where the header gives none, or where no drawing is given. A variable the file's version has not reads as a
        /// dimension style of that file reads it, AutoCAD's own (acad.dwt's, DIMDEC 4 in a file of R12), and a record as none,
        /// so that the header's data and a style's agree where the file can hold neither.
        /// </summary>
        private static object DimRead(DimHeader header, DxfDatabase database)
        {
            if (database != null && !database.File.Supports(header.Since) && header.Name != "DimAssoc")
            {
                DxfDimVariable variable = DxfDimVariable.Named(header.Name);
                return variable.IsReference ? null : variable.Read(null);
            }

            switch (header.Kind)
            {
                case DxfDimKind.Real:
                    return database == null ? header.Real : database.Real(header.Variable, header.Code, header.Real);
                case DxfDimKind.Text:
                    return database == null ? header.Fallback : database.TextOf(header.Variable, header.Code, header.Fallback);
                case DxfDimKind.Block:
                case DxfDimKind.Linetype:
                case DxfDimKind.TextStyle:
                    return database?.Named(header, database.Name(header.Variable, header.Code, header.Fallback));
            }

            int whole = database == null ? header.Whole : database.Whole(header.Variable, header.Code, header.Whole);

            switch (header.Kind)
            {
                case DxfDimKind.Flag:
                    return whole != 0;
                case DxfDimKind.Colour:
                    return DxfColor.OfIndex(Math.Min(256, Math.Abs(whole)));
                case DxfDimKind.Weight:
                    return (DxfLineWeight)whole;
                case DxfDimKind.Letter:
                    return (char)whole;
                default:
                    return whole;
            }
        }

        /// <summary>
        /// The record of the drawing a reference of the header names: a block by its name, or by the name of AutoCAD's arrowhead
        /// (<c>ARCHTICK</c> for the block <c>_ARCHTICK</c>), a linetype or a text style by its name; null for none, or one the
        /// drawing has not.
        /// </summary>
        private DxfSymbolTableRecord Named(DimHeader header, string name)
        {
            if (name.Length == 0)
            {
                return null;
            }

            if (header.Kind == DxfDimKind.Block)
            {
                return BlockTable.Has(name) ? BlockTable[name] : BlockTable.Has("_" + name) ? BlockTable["_" + name] : null;
            }

            DxfSymbolTable table = header.Kind == DxfDimKind.Linetype ? (DxfSymbolTable)LinetypeTable : TextStyleTable;
            return table.Has(name) ? table[name] : null;
        }

        /// <summary>
        /// The text a value set is written as; null where it is the value the header gives, nothing to write. Compared first,
        /// then checked as AutoCAD checks it, then refused where the file's version has not the variable.
        /// </summary>
        private string DimChange(DimHeader header, object value, string parameter)
        {
            object now = DimRead(header, this);
            string text;

            switch (header.Kind)
            {
                case DxfDimKind.Real:
                    if (Same((double)value, (double)now))
                    {
                        return null;
                    }

                    string what = header.Variable.Substring(1) + " has to be a finite number";

                    if (header.Bound == Bound.NonNegative)
                    {
                        Guard.NonNegative((double)value, parameter, what + " of nought or more.");
                    }
                    else
                    {
                        Guard.Finite((double)value, parameter, what + ".");
                    }

                    text = Numbers.Exact((double)value + 0.0);
                    break;
                case DxfDimKind.Text:
                    if (value == null)
                    {
                        throw new ArgumentNullException(parameter);
                    }

                    if (string.Equals((string)value, (string)now, StringComparison.Ordinal))
                    {
                        return null;
                    }

                    if (((string)value).IndexOf('\n') >= 0 || ((string)value).IndexOf('\r') >= 0)
                    {
                        throw new ArgumentException("A variable of the header is one line: " + header.Variable + " cannot hold a line break.", parameter);
                    }

                    text = (string)value;
                    break;
                case DxfDimKind.Block:
                case DxfDimKind.Linetype:
                case DxfDimKind.TextStyle:
                    if (ReferenceEquals(value, now) || (value is DxfDBObject a && now is DxfDBObject b && a.Item != null && a.Item == b.Item))
                    {
                        return null;
                    }

                    text = Reference(header, value, parameter);
                    break;
                case DxfDimKind.Colour:
                    if (value == null)
                    {
                        throw new ArgumentNullException(parameter);
                    }

                    if (Equals(value, now))
                    {
                        return null;
                    }

                    DxfDimVariable.Named(header.Name).Check(value, parameter);
                    text = ((DxfColor)value).ColorIndex.ToString(CultureInfo.InvariantCulture);
                    break;
                case DxfDimKind.Weight:
                    if ((DxfLineWeight)value == (DxfLineWeight)now)
                    {
                        return null;
                    }

                    DxfDimVariable.Named(header.Name).Check(value, parameter);
                    text = ((int)(DxfLineWeight)value).ToString(CultureInfo.InvariantCulture);
                    break;
                case DxfDimKind.Flag:
                    if ((bool)value == (bool)now)
                    {
                        return null;
                    }

                    text = (bool)value ? "1" : "0";
                    break;
                case DxfDimKind.Letter:
                    if ((char)value == (char)now)
                    {
                        return null;
                    }

                    text = ((int)(char)value).ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    if ((int)value == (int)now)
                    {
                        return null;
                    }

                    text = InRange((int)value, header.Least, header.Most, parameter).ToString(CultureInfo.InvariantCulture);
                    break;
            }

            File.Requires(header.Since, header.Variable, DimSince(header.Since));
            return text;
        }

        /// <summary>The name a record set is written as: a block, linetype or text style of this drawing, not erased, a block not a layout; null as none, the text style refused.</summary>
        private string Reference(DimHeader header, object value, string parameter)
        {
            if (value == null)
            {
                if (header.Kind == DxfDimKind.TextStyle)
                {
                    throw new ArgumentNullException(parameter, header.Variable + " names a text style always, as AutoCAD's does.");
                }

                return string.Empty;
            }

            bool kind = header.Kind == DxfDimKind.Block ? value is DxfBlockTableRecord : header.Kind == DxfDimKind.Linetype ? value is DxfLinetypeTableRecord : value is DxfTextStyleTableRecord;
            var record = value as DxfSymbolTableRecord;
            DxfFile of = value is DxfBlockTableRecord block ? block.File : record?.Item?.File;

            if (!kind || of != File || record.IsErased)
            {
                throw new ArgumentException(header.Variable + " names a " + (header.Kind == DxfDimKind.Block ? "block" : header.Kind == DxfDimKind.Linetype ? "linetype" : "text style") + " of this drawing, not erased.", parameter);
            }

            if (value is DxfBlockTableRecord layout && layout.IsLayout)
            {
                throw new ArgumentException(header.Variable + " names a block of arrows, not a layout: " + layout.Name + " is one.", parameter);
            }

            return record.Name;
        }

        /// <summary>The version a variable came with, said in a message: R12, R2000, R2004, R2007 or R2010, with AutoCAD's number.</summary>
        private static string DimSince(int version)
        {
            string release = version >= 1024 ? "R2010" : version >= 1021 ? "R2007" : version >= 1018 ? "R2004" : version >= 1015 ? "R2000" : "R12";
            return release + " (AC" + version.ToString(CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>Gives each variable of the table its name, the key it is kept under.</summary>
        private static Dictionary<string, DimHeader> DimNamed(Dictionary<string, DimHeader> headers)
        {
            foreach (KeyValuePair<string, DimHeader> header in headers)
            {
                header.Value.Name = header.Key;
            }

            return headers;
        }

        private static DimHeader DimOf(string variable, int code, int since, DxfDimKind kind, string fallback)
            => new DimHeader(variable, code, since, kind, fallback, 0.0, 0, 0, 0, Bound.Finite);

        private static DimHeader DimWhole(string variable, int since, int fallback, int least, int most, int code = 70)
            => new DimHeader(variable, code, since, DxfDimKind.Whole, null, 0.0, fallback, least, most, Bound.Finite);

        private static DimHeader DimReal(string variable, int since, double fallback, Bound bound)
            => new DimHeader(variable, 40, since, DxfDimKind.Real, null, fallback, 0, 0, 0, bound);

        /// <summary>A dimension variable of the header: its variable, group and version, what it holds, its value where the header gives none, and what AutoCAD takes.</summary>
        private sealed class DimHeader
        {
            internal DimHeader(string variable, int code, int since, DxfDimKind kind, string fallback, double real, int whole, int least, int most, Bound bound)
            {
                Variable = variable;
                Code = code;
                Since = since;
                Kind = kind;
                Fallback = fallback;
                Real = real;
                bool number = kind == DxfDimKind.Flag || kind == DxfDimKind.Colour || kind == DxfDimKind.Weight || kind == DxfDimKind.Letter;
                Whole = number ? int.Parse(fallback, CultureInfo.InvariantCulture) : whole;
                Least = least;
                Most = most;
                Bound = bound;
            }

            /// <summary>The name AutoCAD's .NET gives it: <c>Dimasz</c> of <c>$DIMASZ</c>, <c>Dimfxlen</c> of <c>$DIMFXL</c>.</summary>
            internal string Name { get; set; }

            internal string Variable { get; }

            internal int Code { get; }

            internal int Since { get; }

            internal DxfDimKind Kind { get; }

            internal string Fallback { get; }

            internal double Real { get; }

            internal int Whole { get; }

            internal int Least { get; }

            internal int Most { get; }

            internal Bound Bound { get; }
        }
    }
}
