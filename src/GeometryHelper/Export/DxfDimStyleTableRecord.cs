using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A dimension style of a DXF file, as AutoCAD's <c>DimStyleTableRecord</c>, read from its DIMSTYLE record: its name and
    /// its dimension variables, each by its group, <see cref="Dimasz"/> by 41, <see cref="Dimtxt"/> by 140.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The style ISO-25 has arrowheads 2.5 long (<see cref="Dimasz"/>), texts 2.5 high (<see cref="Dimtxt"/>) set 0.625 off the
    /// dimension line (<see cref="Dimgap"/>) and two decimal places (<see cref="Dimdec"/>); a variable its record gives no group
    /// for reads as AutoCAD's own, that of its template acad.dwt: arrowheads 0.18 long, texts 0.18 high, four decimal places.
    /// Values are as AutoCAD's .NET gives them: an angle in radians (<see cref="Dimjogang"/>; the file writes degrees), a colour
    /// as a <see cref="DxfColor"/>, an arrowhead as the block it is drawn with, null for AutoCAD's closed filled one.
    /// </para>
    /// <para>
    /// A style of a file is changed by its setters, its record written anew, every other group kept; a setter given the value
    /// its getter gives changes nothing. A variable a file's version has not, as <see cref="Dimdec"/> (R2000) in one of R12, is
    /// refused there with an <see cref="InvalidOperationException"/>. Each points at a block, a linetype or a text style by its
    /// handle from R2000 on (340 to 347), and at an arrowhead's block by its name (5, 6 and 7) in a file of R12.
    /// </para>
    /// <para>
    /// A style made in code holds AutoCAD's own values until it is given others, and is written whole, every variable its
    /// file's version has, when it is added to a <see cref="DxfDimStyleTable"/>; its handle written as group 105, as AutoCAD
    /// writes a DIMSTYLE's. Renamed, the dimensions, leaders and tolerances drawn in it, the styles AutoCAD keeps for each kind
    /// of dimension of it (<c>GH$0</c>, <c>GH$2</c> of GH) and the header's <c>$DIMSTYLE</c> follow.
    /// </para>
    /// </remarks>
    public sealed class DxfDimStyleTableRecord : DxfSymbolTableRecord
    {
        // The types that name a dimension style by its name (group 3).
        private static readonly HashSet<string> Users = new HashSet<string>(StringComparer.Ordinal) { "DIMENSION", "ARC_DIMENSION", "LARGE_RADIAL_DIMENSION", "LEADER", "TOLERANCE" };

        // The sections whose records name a dimension style.
        private static readonly string[] Naming = { "ENTITIES", "BLOCKS" };

        private readonly Dictionary<DxfDimVariable, object> _values = new Dictionary<DxfDimVariable, object>();

        /// <summary>
        /// Initializes a dimension style made in code, every variable AutoCAD's own, to be added to a
        /// <see cref="DxfDimStyleTable"/> with its <see cref="DxfSymbolTableRecord.Name"/> given.
        /// </summary>
        public DxfDimStyleTableRecord()
        {
        }

        private DxfDimStyleTableRecord(DxfItem item)
            : base(item)
        {
        }

        #region The variables

        /// <summary>Gets or sets the decimal places of the angles of angular dimensions (group 179), as AutoCAD's <c>Dimadec</c>; 0 where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimadec { get => (int)Get(V("Dimadec")); set => Set(V("Dimadec"), value); }

        /// <summary>Gets or sets whether dimensions show their measurement in alternate units as well (group 170), as AutoCAD's <c>Dimalt</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimalt { get => (bool)Get(V("Dimalt")); set => Set(V("Dimalt"), value); }

        /// <summary>Gets or sets the decimal places of alternate units (group 171), as AutoCAD's <c>Dimaltd</c>; 2 where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public int Dimaltd { get => (int)Get(V("Dimaltd")); set => Set(V("Dimaltd"), value); }

        /// <summary>Gets or sets the factor a measurement is multiplied by in alternate units (group 143), as AutoCAD's <c>Dimaltf</c>: 25.4 for inches shown as millimetres, where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimaltf { get => (double)Get(V("Dimaltf")); set => Set(V("Dimaltf"), value); }

        /// <summary>Gets or sets what alternate units are rounded to (group 148), as AutoCAD's <c>Dimaltrnd</c>; 0 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public double Dimaltrnd { get => (double)Get(V("Dimaltrnd")); set => Set(V("Dimaltrnd"), value); }

        /// <summary>Gets or sets the decimal places of the tolerances in alternate units (group 274), as AutoCAD's <c>Dimalttd</c>; 2 where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimalttd { get => (int)Get(V("Dimalttd")); set => Set(V("Dimalttd"), value); }

        /// <summary>Gets or sets which zeros of the tolerances in alternate units are left out (group 286), as AutoCAD's <c>Dimalttz</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimalttz { get => (int)Get(V("Dimalttz")); set => Set(V("Dimalttz"), value); }

        /// <summary>Gets or sets the units of alternate units (group 273), as AutoCAD's <c>Dimaltu</c>: 2 decimal, where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimaltu { get => (int)Get(V("Dimaltu")); set => Set(V("Dimaltu"), value); }

        /// <summary>Gets or sets which zeros of alternate units are left out (group 285), as AutoCAD's <c>Dimaltz</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimaltz { get => (int)Get(V("Dimaltz")); set => Set(V("Dimaltz"), value); }

        /// <summary>Gets or sets the prefix and suffix of alternate units (group 4), as AutoCAD's <c>Dimapost</c>: <c>[] in</c> writes the alternate measurement then " in"; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public string Dimapost { get => (string)Get(V("Dimapost")); set => Set(V("Dimapost"), value); }

        /// <summary>Gets or sets where the arc symbol of an arc length dimension stands (group 90), as AutoCAD's <c>Dimarcsym</c>: 0 before the text, 1 above it, 2 none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimarcsym { get => (int)Get(V("Dimarcsym")); set => Set(V("Dimarcsym"), value); }

        /// <summary>Gets or sets the size of the arrowheads (group 41), as AutoCAD's <c>Dimasz</c>: 2.5 in ISO-25; 0.18 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimasz { get => (double)Get(V("Dimasz")); set => Set(V("Dimasz"), value); }

        /// <summary>Gets or sets what is moved out between the extension lines where text and arrows do not both fit (group 289), as AutoCAD's <c>Dimatfit</c>; 3, the best fit, where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimatfit { get => (int)Get(V("Dimatfit")); set => Set(V("Dimatfit"), value); }

        /// <summary>Gets or sets the units of angles (group 275), as AutoCAD's <c>Dimaunit</c>: 0 decimal degrees, 1 degrees, minutes and seconds, 2 grads, 3 radians.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimaunit { get => (int)Get(V("Dimaunit")); set => Set(V("Dimaunit"), value); }

        /// <summary>Gets or sets which zeros of angles are left out (group 79), as AutoCAD's <c>Dimazin</c>: 1 leading, 2 trailing, 3 both.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimazin { get => (int)Get(V("Dimazin")); set => Set(V("Dimazin"), value); }

        /// <summary>
        /// Gets or sets the block both arrowheads are drawn with (group 342, the name 5 in a file of R12), as AutoCAD's
        /// <c>Dimblk</c> gives its id: <c>_ARCHTICK</c>; null for AutoCAD's closed filled arrowhead, where the record names none.
        /// </summary>
        /// <value>Set, a block of the style's file, or null.</value>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, or of another file.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfBlockTableRecord Dimblk { get => (DxfBlockTableRecord)Get(V("Dimblk")); set => Set(V("Dimblk"), value); }

        /// <summary>Gets or sets the block of the first arrowhead where <see cref="Dimsah"/> draws them apart (group 343, the name 6 in a file of R12), as AutoCAD's <c>Dimblk1</c>; null for the closed filled one.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, or of another file.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfBlockTableRecord Dimblk1 { get => (DxfBlockTableRecord)Get(V("Dimblk1")); set => Set(V("Dimblk1"), value); }

        /// <summary>Gets or sets the block of the second arrowhead where <see cref="Dimsah"/> draws them apart (group 344, the name 7 in a file of R12), as AutoCAD's <c>Dimblk2</c>; null for the closed filled one.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, or of another file.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfBlockTableRecord Dimblk2 { get => (DxfBlockTableRecord)Get(V("Dimblk2")); set => Set(V("Dimblk2"), value); }

        /// <summary>Gets or sets the centre marks of radial dimensions (group 141), as AutoCAD's <c>Dimcen</c>: above nought a mark that long, below nought a centre line, nought none; 0.09 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimcen { get => (double)Get(V("Dimcen")); set => Set(V("Dimcen"), value); }

        /// <summary>Gets or sets the colour of the dimension lines (group 176), as AutoCAD's <c>Dimclrd</c>; BYBLOCK where the record gives none.</summary>
        /// <value>Set, a colour of the index, BYLAYER or BYBLOCK.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour, which a style's group does not hold.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfColor Dimclrd { get => (DxfColor)Get(V("Dimclrd")); set => Set(V("Dimclrd"), value); }

        /// <summary>Gets or sets the colour of the extension lines (group 177), as AutoCAD's <c>Dimclre</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfColor Dimclre { get => (DxfColor)Get(V("Dimclre")); set => Set(V("Dimclre"), value); }

        /// <summary>Gets or sets the colour of the texts (group 178), as AutoCAD's <c>Dimclrt</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfColor Dimclrt { get => (DxfColor)Get(V("Dimclrt")); set => Set(V("Dimclrt"), value); }

        /// <summary>Gets or sets the decimal places of measurements (group 271), as AutoCAD's <c>Dimdec</c>: 2 in ISO-25; 4 where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimdec { get => (int)Get(V("Dimdec")); set => Set(V("Dimdec"), value); }

        /// <summary>Gets or sets how far the dimension line runs past the extension lines where it ends in ticks (group 46), as AutoCAD's <c>Dimdle</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimdle { get => (double)Get(V("Dimdle")); set => Set(V("Dimdle"), value); }

        /// <summary>Gets or sets how far apart baseline dimensions stand (group 43), as AutoCAD's <c>Dimdli</c>; 0.38 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimdli { get => (double)Get(V("Dimdli")); set => Set(V("Dimdli"), value); }

        /// <summary>Gets or sets the decimal separator (group 278, the letter's number), as AutoCAD's <c>Dimdsep</c>: ',' in ISO-25 (44); '.' where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public char Dimdsep { get => (char)Get(V("Dimdsep")); set => Set(V("Dimdsep"), value); }

        /// <summary>Gets or sets how far the extension lines run past the dimension line (group 44), as AutoCAD's <c>Dimexe</c>; 0.18 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimexe { get => (double)Get(V("Dimexe")); set => Set(V("Dimexe"), value); }

        /// <summary>Gets or sets how far the extension lines stand off the points measured (group 42), as AutoCAD's <c>Dimexo</c>; 0.0625 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimexo { get => (double)Get(V("Dimexo")); set => Set(V("Dimexo"), value); }

        /// <summary>Gets or sets how fractions are stacked (group 276), as AutoCAD's <c>Dimfrac</c>: 0 horizontal, 1 diagonal, 2 not stacked.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimfrac { get => (int)Get(V("Dimfrac")); set => Set(V("Dimfrac"), value); }

        /// <summary>Gets or sets the fixed length of the extension lines where <see cref="DimfxlenOn"/> (group 49), as AutoCAD's <c>Dimfxlen</c>; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2007.</exception>
        public double Dimfxlen { get => (double)Get(V("Dimfxlen")); set => Set(V("Dimfxlen"), value); }

        /// <summary>Gets or sets whether the extension lines are of the fixed length <see cref="Dimfxlen"/> (group 290), as AutoCAD's <c>DimfxlenOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2007.</exception>
        public bool DimfxlenOn { get => (bool)Get(V("DimfxlenOn")); set => Set(V("DimfxlenOn"), value); }

        /// <summary>Gets or sets the gap about the text of a dimension (group 147), as AutoCAD's <c>Dimgap</c>: 0.625 in ISO-25; 0.09 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the gap set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimgap { get => (double)Get(V("Dimgap")); set => Set(V("Dimgap"), value); }

        /// <summary>
        /// Gets or sets the angle of the jog of a jogged radius dimension (group 50), as AutoCAD's <c>Dimjogang</c>: in radians,
        /// the record writing degrees, so 60 degrees is pi / 3; 45 degrees where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2007.</exception>
        public double Dimjogang { get => (double)Get(V("Dimjogang")); set => Set(V("Dimjogang"), value); }

        /// <summary>Gets or sets where the text stands along the dimension line (group 280), as AutoCAD's <c>Dimjust</c>: 0 centred, 1 by the first extension line, 2 by the second, 3 and 4 above them.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimjust { get => (int)Get(V("Dimjust")); set => Set(V("Dimjust"), value); }

        /// <summary>Gets or sets the block the arrowhead of a leader is drawn with (group 341), as AutoCAD's <c>Dimldrblk</c>; null for the closed filled one.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, or of another file.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public DxfBlockTableRecord Dimldrblk { get => (DxfBlockTableRecord)Get(V("Dimldrblk")); set => Set(V("Dimldrblk"), value); }

        /// <summary>Gets or sets the factor a length measured is multiplied by (group 144), as AutoCAD's <c>Dimlfac</c>: 100 for a detail drawn at 1:100; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimlfac { get => (double)Get(V("Dimlfac")); set => Set(V("Dimlfac"), value); }

        /// <summary>Gets or sets whether a measurement is shown as its limits, <see cref="Dimtp"/> and <see cref="Dimtm"/> about it (group 72), as AutoCAD's <c>Dimlim</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimlim { get => (bool)Get(V("Dimlim")); set => Set(V("Dimlim"), value); }

        /// <summary>Gets or sets the linetype of the first extension line (group 346), as AutoCAD's <c>Dimltex1</c>; null where the record names none.</summary>
        /// <exception cref="ArgumentException">Thrown when the linetype set is of another file.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2007.</exception>
        public DxfLinetypeTableRecord Dimltex1 { get => (DxfLinetypeTableRecord)Get(V("Dimltex1")); set => Set(V("Dimltex1"), value); }

        /// <summary>Gets or sets the linetype of the second extension line (group 347), as AutoCAD's <c>Dimltex2</c>; null where the record names none.</summary>
        /// <exception cref="ArgumentException">Thrown when the linetype set is of another file.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2007.</exception>
        public DxfLinetypeTableRecord Dimltex2 { get => (DxfLinetypeTableRecord)Get(V("Dimltex2")); set => Set(V("Dimltex2"), value); }

        /// <summary>Gets or sets the linetype of the dimension line (group 345), as AutoCAD's <c>Dimltype</c>; null where the record names none.</summary>
        /// <exception cref="ArgumentException">Thrown when the linetype set is of another file.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2007.</exception>
        public DxfLinetypeTableRecord Dimltype { get => (DxfLinetypeTableRecord)Get(V("Dimltype")); set => Set(V("Dimltype"), value); }

        /// <summary>Gets or sets the units of lengths (group 277), as AutoCAD's <c>Dimlunit</c>: 1 scientific, 2 decimal, 3 engineering, 4 architectural, 5 fractional, 6 Windows desktop.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimlunit { get => (int)Get(V("Dimlunit")); set => Set(V("Dimlunit"), value); }

        /// <summary>Gets or sets the lineweight of the dimension lines (group 371), as AutoCAD's <c>Dimlwd</c>; <see cref="DxfLineWeight.ByBlock"/> where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight set is not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public DxfLineWeight Dimlwd { get => (DxfLineWeight)Get(V("Dimlwd")); set => Set(V("Dimlwd"), value); }

        /// <summary>Gets or sets the lineweight of the extension lines (group 372), as AutoCAD's <c>Dimlwe</c>; <see cref="DxfLineWeight.ByBlock"/> where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight set is not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public DxfLineWeight Dimlwe { get => (DxfLineWeight)Get(V("Dimlwe")); set => Set(V("Dimlwe"), value); }

        /// <summary>Gets or sets the prefix and suffix of measurements (group 3), as AutoCAD's <c>Dimpost</c>: <c>&lt;&gt; mm</c> writes the measurement then " mm"; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public string Dimpost { get => (string)Get(V("Dimpost")); set => Set(V("Dimpost"), value); }

        /// <summary>Gets or sets what measurements are rounded to (group 45), as AutoCAD's <c>Dimrnd</c>: 0.5 rounds 12.3 to 12.5; 0 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimrnd { get => (double)Get(V("Dimrnd")); set => Set(V("Dimrnd"), value); }

        /// <summary>Gets or sets whether the two arrowheads are drawn apart, by <see cref="Dimblk1"/> and <see cref="Dimblk2"/> (group 173), as AutoCAD's <c>Dimsah</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimsah { get => (bool)Get(V("Dimsah")); set => Set(V("Dimsah"), value); }

        /// <summary>Gets or sets the scale of every size of the style (group 40), as AutoCAD's <c>Dimscale</c>: 100 for a drawing plotted at 1:100; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimscale { get => (double)Get(V("Dimscale")); set => Set(V("Dimscale"), value); }

        /// <summary>Gets or sets whether the first half of the dimension line is left out (group 281), as AutoCAD's <c>Dimsd1</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public bool Dimsd1 { get => (bool)Get(V("Dimsd1")); set => Set(V("Dimsd1"), value); }

        /// <summary>Gets or sets whether the second half of the dimension line is left out (group 282), as AutoCAD's <c>Dimsd2</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public bool Dimsd2 { get => (bool)Get(V("Dimsd2")); set => Set(V("Dimsd2"), value); }

        /// <summary>Gets or sets whether the first extension line is left out (group 75), as AutoCAD's <c>Dimse1</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimse1 { get => (bool)Get(V("Dimse1")); set => Set(V("Dimse1"), value); }

        /// <summary>Gets or sets whether the second extension line is left out (group 76), as AutoCAD's <c>Dimse2</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimse2 { get => (bool)Get(V("Dimse2")); set => Set(V("Dimse2"), value); }

        /// <summary>Gets or sets whether arrowheads that do not fit between the extension lines are left out (group 175), as AutoCAD's <c>Dimsoxd</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimsoxd { get => (bool)Get(V("Dimsoxd")); set => Set(V("Dimsoxd"), value); }

        /// <summary>Gets or sets where the text stands across the dimension line (group 77), as AutoCAD's <c>Dimtad</c>: 0 centred on it, 1 above it, 2 on the side away from the points, 3 as JIS, 4 below.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public int Dimtad { get => (int)Get(V("Dimtad")); set => Set(V("Dimtad"), value); }

        /// <summary>Gets or sets the decimal places of tolerances (group 272), as AutoCAD's <c>Dimtdec</c>; 4 where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimtdec { get => (int)Get(V("Dimtdec")); set => Set(V("Dimtdec"), value); }

        /// <summary>Gets or sets the height of tolerances and fractions for that of the text (group 146), as AutoCAD's <c>Dimtfac</c>: 0.8 makes them 2 high under a text 2.5 high; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimtfac { get => (double)Get(V("Dimtfac")); set => Set(V("Dimtfac"), value); }

        /// <summary>Gets or sets the background of the text (group 69), as AutoCAD's <c>Dimtfill</c>: 0 none, 1 the drawing's background, 2 <see cref="Dimtfillclr"/>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2007.</exception>
        public int Dimtfill { get => (int)Get(V("Dimtfill")); set => Set(V("Dimtfill"), value); }

        /// <summary>Gets or sets the colour of the background of the text where <see cref="Dimtfill"/> is 2 (the record's second group 70, the first being its flags), as AutoCAD's <c>Dimtfillclr</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2007.</exception>
        public DxfColor Dimtfillclr { get => (DxfColor)Get(V("Dimtfillclr")); set => Set(V("Dimtfillclr"), value); }

        /// <summary>Gets or sets whether a text between the extension lines stands level (group 73), as AutoCAD's <c>Dimtih</c>; true where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimtih { get => (bool)Get(V("Dimtih")); set => Set(V("Dimtih"), value); }

        /// <summary>Gets or sets whether the text is kept between the extension lines (group 174), as AutoCAD's <c>Dimtix</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimtix { get => (bool)Get(V("Dimtix")); set => Set(V("Dimtix"), value); }

        /// <summary>Gets or sets the lower tolerance (group 48), as AutoCAD's <c>Dimtm</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimtm { get => (double)Get(V("Dimtm")); set => Set(V("Dimtm"), value); }

        /// <summary>Gets or sets what moving the text does (group 279), as AutoCAD's <c>Dimtmove</c>: 0 moves the dimension line with it, 1 adds a leader, 2 moves it freely.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimtmove { get => (int)Get(V("Dimtmove")); set => Set(V("Dimtmove"), value); }

        /// <summary>Gets or sets whether a dimension line is drawn between the extension lines though the text stands outside (group 172), as AutoCAD's <c>Dimtofl</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimtofl { get => (bool)Get(V("Dimtofl")); set => Set(V("Dimtofl"), value); }

        /// <summary>Gets or sets whether a text outside the extension lines stands level (group 74), as AutoCAD's <c>Dimtoh</c>; true where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimtoh { get => (bool)Get(V("Dimtoh")); set => Set(V("Dimtoh"), value); }

        /// <summary>Gets or sets whether tolerances are shown after the measurement (group 71), as AutoCAD's <c>Dimtol</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool Dimtol { get => (bool)Get(V("Dimtol")); set => Set(V("Dimtol"), value); }

        /// <summary>Gets or sets where tolerances stand against the measurement (group 283), as AutoCAD's <c>Dimtolj</c>: 0 bottom, 1 middle, 2 top; 1 where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimtolj { get => (int)Get(V("Dimtolj")); set => Set(V("Dimtolj"), value); }

        /// <summary>Gets or sets the upper tolerance (group 47), as AutoCAD's <c>Dimtp</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimtp { get => (double)Get(V("Dimtp")); set => Set(V("Dimtp"), value); }

        /// <summary>Gets or sets the size of the ticks drawn in place of arrowheads (group 142), as AutoCAD's <c>Dimtsz</c>: nought draws arrowheads.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimtsz { get => (double)Get(V("Dimtsz")); set => Set(V("Dimtsz"), value); }

        /// <summary>Gets or sets how far a centred text stands above or below the dimension line, in heights of the text (group 145), as AutoCAD's <c>Dimtvp</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimtvp { get => (double)Get(V("Dimtvp")); set => Set(V("Dimtvp"), value); }

        /// <summary>Gets or sets the text style of the texts (group 340), as AutoCAD's <c>Dimtxsty</c> gives its id; null where the record names none.</summary>
        /// <exception cref="ArgumentException">Thrown when the style set is of another file.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension style is erased, or its file is older than R2000.</exception>
        public DxfTextStyleTableRecord Dimtxsty { get => (DxfTextStyleTableRecord)Get(V("Dimtxsty")); set => Set(V("Dimtxsty"), value); }

        /// <summary>Gets or sets the height of the texts (group 140), as AutoCAD's <c>Dimtxt</c>: 2.5 in ISO-25; 0.18 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Dimtxt { get => (double)Get(V("Dimtxt")); set => Set(V("Dimtxt"), value); }

        /// <summary>Gets or sets which zeros of tolerances are left out (group 284), as AutoCAD's <c>Dimtzin</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public int Dimtzin { get => (int)Get(V("Dimtzin")); set => Set(V("Dimtzin"), value); }

        /// <summary>Gets or sets whether the text is placed by the user as the dimension line is (group 288), as AutoCAD's <c>Dimupt</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or its file is older than R2000.</exception>
        public bool Dimupt { get => (bool)Get(V("Dimupt")); set => Set(V("Dimupt"), value); }

        /// <summary>Gets or sets which zeros of measurements are left out (group 78), as AutoCAD's <c>Dimzin</c>: 8 leaves trailing zeros out, so that 12.50 is 12.5.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public int Dimzin { get => (int)Get(V("Dimzin")); set => Set(V("Dimzin"), value); }

        /// <summary>
        /// Gets the block an arrowhead is drawn with, as AutoCAD's <c>GetArrowId</c> gives its id: <see cref="Dimblk1"/> or
        /// <see cref="Dimblk2"/> where <see cref="Dimsah"/> draws them apart, else <see cref="Dimblk"/>.
        /// </summary>
        /// <param name="whichArrow">The arrowhead.</param>
        /// <returns>The block; null for AutoCAD's closed filled arrowhead.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the arrowhead is not one of <see cref="DxfDimArrowFlag"/>.</exception>
        public DxfBlockTableRecord GetArrow(DxfDimArrowFlag whichArrow)
        {
            if (whichArrow != DxfDimArrowFlag.FirstArrow && whichArrow != DxfDimArrowFlag.SecondArrow)
            {
                throw new ArgumentOutOfRangeException(nameof(whichArrow), whichArrow, "The arrowhead is the first or the second.");
            }

            if (!Dimsah)
            {
                return Dimblk;
            }

            return whichArrow == DxfDimArrowFlag.FirstArrow ? Dimblk1 : Dimblk2;
        }

        #endregion

        #region Erasing and renaming

        /// <summary>
        /// Erases the style from its table, as AutoCAD's <c>Erase</c> of a dimension style nothing is drawn in: its DIMSTYLE
        /// record taken out, the table's count lowered by one, its handle not given again.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is in no file or is erased already; when it is
        /// Standard or the current style (<c>$DIMSTYLE</c>), which AutoCAD keeps; or when a dimension, a leader or a tolerance
        /// is drawn in it.</exception>
        public override void Erase()
        {
            if (Item == null)
            {
                throw new InvalidOperationException("The dimension style " + Name + " is in no file to be erased from.");
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The dimension style " + Name + " is erased already.");
            }

            string name = Name;
            DxfFile file = Item.File;

            if (string.Equals(name, "Standard", StringComparison.OrdinalIgnoreCase) || IsCurrent(file, name))
            {
                throw new InvalidOperationException("The dimension style " + name + " is Standard or the current style, which AutoCAD keeps.");
            }

            if (file.Names(Naming, (type, code) => code == 3 && Users.Contains(type), name))
            {
                throw new InvalidOperationException("The dimension style " + name + " has dimensions, leaders or tolerances drawn in it, and is not erased, as AutoCAD erases a style only when nothing uses it.");
            }

            string handle = Handle;
            file.RemoveTableRecord(Item, "DIMSTYLE");
            DxfDimStyleTable.ListHandle(file, handle, false);
        }

        /// <summary>
        /// Renames the style with every reference to it: its DIMSTYLE record's 2; the 2 of each style AutoCAD keeps for a kind of
        /// dimension of it, <c>GH$0</c> becoming <c>NEW$0</c>; each group 3 of the dimensions, leaders and tolerances of model
        /// space, paper space and the blocks drawn in any of them; and <c>$DIMSTYLE</c>.
        /// </summary>
        private protected override void Rename(string name)
        {
            DxfSymbolTable.CheckName(name, "value");
            string old = Name;

            if (IsDependent)
            {
                throw new InvalidOperationException("The dimension style " + old + " comes from an external reference, and is renamed in its own drawing.");
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The dimension style " + old + " is erased, and is not changed.");
            }

            DxfFile file = Item.File;
            var children = new List<KeyValuePair<DxfItem, string>>();

            foreach (DxfItem other in file.TableItems("DIMSTYLE"))
            {
                string otherName = DxfReader.NameOf(file.ValueOf(other, 2) ?? string.Empty);

                if (other != Item && string.Equals(otherName, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The dimension style table has a style named " + name + " already, letters of either case the same.", "value");
                }

                if (other != Item && IsChild(otherName, old))
                {
                    children.Add(new KeyValuePair<DxfItem, string>(other, name + otherName.Substring(old.Length)));
                }
            }

            bool current = IsCurrent(file, old);
            SetName(Item, name);
            file.Rename(Naming, (type, code) => code == 3 && Users.Contains(type), -1, old, name);

            foreach (KeyValuePair<DxfItem, string> child in children)
            {
                string was = DxfReader.NameOf(file.ValueOf(child.Key, 2) ?? string.Empty);
                SetName(child.Key, child.Value);
                file.Rename(Naming, (type, code) => code == 3 && Users.Contains(type), -1, was, child.Value);
            }

            if (current)
            {
                file.SetHeader("$DIMSTYLE", new[] { new DxfTypedValue(2, name) });
            }

            GeometryHelperLog.Info("DxfDatabase: the dimension style " + old + " was renamed " + name + ", with every dimension drawn in it.");
        }

        /// <summary>Whether a style's name is that of a style AutoCAD keeps for one kind of dimension of another: GH$0 of GH.</summary>
        private static bool IsChild(string name, string parent)
        {
            if (name.Length < parent.Length + 2 || !name.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || name[parent.Length] != '$')
            {
                return false;
            }

            for (int i = parent.Length + 1; i < name.Length; i++)
            {
                if (!char.IsDigit(name[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCurrent(DxfFile file, string name)
        {
            IReadOnlyList<DxfTypedValue> current = file.HeaderOf("$DIMSTYLE");
            return current != null && current.Count > 0 && string.Equals(DxfReader.NameOf(current[0].Value), name, StringComparison.OrdinalIgnoreCase);
        }

        private static void SetName(DxfItem item, string name)
        {
            var groups = new DxfDimGroups(item);
            groups.Set(2, name, groups.All, DxfDimVariable.StyleOrder);
            groups.Write(DxfDimVariable.StyleOrder);
        }

        #endregion

        #region Reading and writing

        /// <summary>The object of a DIMSTYLE record, made once.</summary>
        internal static DxfDimStyleTableRecord Of(DxfItem item) => item.Wrapper as DxfDimStyleTableRecord ?? (DxfDimStyleTableRecord)item.KeepWrapper(new DxfDimStyleTableRecord(item));


        /// <summary>
        /// The value of a variable, as AutoCAD's .NET gives it: of the record's group, AutoCAD's own where it has none; of one
        /// made in code, the value it was given or AutoCAD's own. A reference is the record it points at, null where it points
        /// at none the file has.
        /// </summary>
        internal object Get(DxfDimVariable variable)
        {
            if (Item == null)
            {
                return _values.TryGetValue(variable, out object value) ? value : variable.IsReference ? null : variable.Read(null);
            }

            string text = Text(variable);
            return variable.IsReference ? Resolve(Item.File, variable, text) : variable.Read(text);
        }

        /// <summary>Keeps a value of a variable in a style made in code, as <see cref="DxfDimension.GetDimstyleData"/> gives it.</summary>
        internal void Keep(DxfDimVariable variable, object value) => _values[variable] = value;

        /// <summary>The text of a variable's group in the record; null where it has none, or the file's version has not the variable.</summary>
        internal string Text(DxfDimVariable variable)
        {
            if (!Item.File.Supports(variable.Since))
            {
                return null;
            }

            var groups = new DxfDimGroups(Item);

            if (variable.Second)
            {
                int at = groups.Find(70, groups.All, 1);
                return at < 0 ? null : groups.List[at].Value;
            }

            return groups.Text(variable.CodeIn(!Item.File.HasMarkers), groups.All);
        }

        /// <summary>
        /// Sets a variable: nothing where the value is the one it has; for one made in code, kept; for one of a file, its group
        /// written in its place, put in where AutoCAD writes it where the record has none.
        /// </summary>
        private void Set(DxfDimVariable variable, object value)
        {
            // The value read changes nothing, whatever the file holds, as a DIMLWD of 7; another is checked before it is written.
            if (variable.SameValue(value, Get(variable)))
            {
                return;
            }

            variable.Check(value, nameof(value));

            if (Item == null)
            {
                _values[variable] = value;
                return;
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The dimension style " + Name + " is erased, and is not changed.");
            }

            DxfFile file = Item.File;
            file.Requires(variable.Since, variable.Name.ToUpperInvariant(), variable.SinceSaid);
            bool r12 = !file.HasMarkers;
            string text = variable.IsReference ? Reference(file, variable, (DxfDBObject)value, r12, nameof(value)) : variable.Write(value);
            var groups = new DxfDimGroups(Item);

            if (variable.Second)
            {
                int at = groups.Find(70, groups.All, 1);

                if (at >= 0)
                {
                    groups.List[at] = new KeyValuePair<int, string>(70, text);
                }
                else
                {
                    groups.Insert(groups.Place(71, groups.All, DxfDimVariable.StyleOrder), new KeyValuePair<int, string>(70, text));
                }
            }
            else
            {
                groups.Set(variable.CodeIn(r12), text, groups.All, DxfDimVariable.StyleOrder);
            }

            groups.Write(DxfDimVariable.StyleOrder);
        }

        /// <summary>
        /// The text a reference is written as in a file: a record's handle from R2000 on, a block's name in a file of R12; null
        /// for none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the record is of another file, or has no handle where one is needed.</exception>
        internal static string Reference(DxfFile file, DxfDimVariable variable, DxfDBObject value, bool r12, string parameter)
        {
            if (value == null)
            {
                return null;
            }

            DxfFile of = value is DxfBlockTableRecord block ? block.File : value.Item?.File;

            if (of != file || value.IsErased)
            {
                throw new ArgumentException("The " + variable.Name + " is a record of the same drawing as the style, and not erased.", parameter);
            }

            if (r12)
            {
                return ((DxfSymbolTableRecord)value).Name;
            }

            string handle = value.Handle;

            if (handle == null)
            {
                throw new ArgumentException("The " + variable.Name + " points at its record by a handle, and " + ((DxfSymbolTableRecord)value).Name + " has none.", parameter);
            }

            return handle;
        }

        /// <summary>
        /// The record a reference's text names in a file: by handle from R2000 on (nought for none), a block by its name in a file
        /// of R12, AutoCAD's arrowhead names among them (<c>ARCHTICK</c> for the block <c>_ARCHTICK</c>); null where the file has
        /// none.
        /// </summary>
        internal static DxfDBObject Resolve(DxfFile file, DxfDimVariable variable, string text)
        {
            string value = text?.Trim();

            if (string.IsNullOrEmpty(value) || value == "0")
            {
                return null;
            }

            if (variable.Kind == DxfDimKind.Block)
            {
                DxfDatabase database = file.Database;

                if (database == null)
                {
                    return null;
                }

                if (file.HasMarkers)
                {
                    foreach (DxfBlockTableRecord block in database.BlockTable)
                    {
                        if (string.Equals(block.Handle, value, StringComparison.OrdinalIgnoreCase))
                        {
                            return block;
                        }
                    }

                    return null;
                }

                string name = DxfReader.NameOf(value);
                return database.BlockTable.Has(name) ? database.BlockTable[name] : database.BlockTable.Has("_" + name) ? database.BlockTable["_" + name] : null;
            }

            string table = variable.Kind == DxfDimKind.Linetype ? "LTYPE" : "STYLE";

            foreach (DxfItem item in file.TableItems(table))
            {
                if (string.Equals(item.Handle, value, StringComparison.OrdinalIgnoreCase))
                {
                    return table == "LTYPE" ? (DxfDBObject)DxfLinetypeTableRecord.Of(item) : DxfTextStyleTableRecord.Of(item);
                }
            }

            return null;
        }

        /// <summary>
        /// Writes the style made in code into a file's dimension style table, and stands for its record from then on: every
        /// variable the file's version has, in AutoCAD's order, its handle as group 105.
        /// </summary>
        internal void AddTo(DxfFile file)
        {
            bool r12 = !file.HasMarkers;

            foreach (KeyValuePair<DxfDimVariable, object> value in _values)
            {
                if (value.Key.IsReference && value.Value != null)
                {
                    if (!file.Supports(value.Key.Since) || (r12 && value.Key.R12Code == 0))
                    {
                        throw new ArgumentException("A DXF of version " + (file.Version ?? "R12") + " has no " + value.Key.Name.ToUpperInvariant() + ", which came with " + value.Key.SinceSaid + ".", "value");
                    }

                    Reference(file, value.Key, (DxfDBObject)value.Value, r12, "value");
                }
            }

            DxfItem item = file.AddTableRecord("DIMSTYLE", "AcDbDimStyleTableRecord", b =>
            {
                b.Group(2, Name);
                b.Group(70, "0");

                foreach (int code in DxfDimVariable.StyleOrder)
                {
                    DxfDimVariable variable = DxfDimVariable.OfCode(code, false);

                    if (code == 100 || code == 2 || code == 70 || code == 5 || code == 6 || code == 7 || variable == null || !file.Supports(variable.Since))
                    {
                        continue;
                    }

                    if (variable.IsReference)
                    {
                        if (!r12 && _values.TryGetValue(variable, out object target) && target != null)
                        {
                            b.Group(code, ((DxfDBObject)target).Handle);
                        }

                        continue;
                    }

                    b.Group(code, variable.Write(Get(variable)));

                    // DIMTFILLCLR, the second 70, after DIMTFILL.
                    if (code == 69)
                    {
                        DxfDimVariable fill = DxfDimVariable.Named("Dimtfillclr");
                        b.Group(70, fill.Write(Get(fill)));
                    }
                }
            });

            if (item == null)
            {
                throw new InvalidOperationException("The file has no dimension style table to add the style " + Name + " to.");
            }

            Dictionary<DxfDimVariable, object> values = new Dictionary<DxfDimVariable, object>(_values);
            Bind(item);
            _values.Clear();

            // The arrowheads of a file of R12, by their names (5, 6 and 7), in their places after DIMAPOST.
            if (r12)
            {
                foreach (string name in new[] { "Dimblk", "Dimblk1", "Dimblk2" })
                {
                    DxfDimVariable variable = DxfDimVariable.Named(name);

                    if (values.TryGetValue(variable, out object target) && target != null)
                    {
                        Set(variable, target);
                    }
                }
            }

            DxfDimStyleTable.ListHandle(file, Handle, true);
        }

        private static DxfDimVariable V(string name) => DxfDimVariable.Named(name);

        #endregion
    }
}
