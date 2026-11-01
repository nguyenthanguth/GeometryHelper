namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The symbols AutoCAD 2021's text editor enters from its Symbol menu, each the letter it stands for, and the three
    /// control codes of that menu, to be put into the words of a TEXT, an MTEXT, an attribute or a dimension without
    /// remembering a code: <c>new DxfMText { Contents = DxfTextSymbols.Diameter + "20" }</c> reads ∅20. The letters are those
    /// of AutoCAD's help, "Text Symbols and Special Characters Reference", and of its menu, and nothing else. Each is
    /// written as AutoCAD writes it: as it is in a drawing of R2007 or later, and before, where the code page has not the
    /// letter, as its escape, so that ∅20 in an MTEXT of R2004 is <c>\U+220520</c>. Written by the library into an MTEXT, a
    /// TEXT, an attribute and a dimension of R2004 and of R2018, each of the 21 was read back by AutoCAD 2021 as the same
    /// letter, in its <c>Contents</c>, <c>Text</c>, <c>TextString</c> and <c>DimensionText</c> alike, and AutoCAD wrote its
    /// own texts of them in the same bytes. A letter shows only in a font that has it: AutoCAD's help names simplex, romans,
    /// gdt, amgdt, isocp and isoct among the SHX fonts, and isocpeur and isocteur among the TrueType.
    /// </summary>
    public static class DxfTextSymbols
    {
        /// <summary>
        /// The control code of the degree sign, <c>%%d</c>, which AutoCAD draws as <see cref="Degree"/> in a TEXT, an
        /// attribute, a dimension and an MTEXT. The words keep it as written: a TEXT's <c>TextString</c> and an
        /// MTEXT's <c>Contents</c> of <c>45%%d</c> are <c>45%%d</c>, and the MTEXT's <c>Text</c> is 45°, in AutoCAD 2021 and in
        /// <see cref="DxfMText.Text"/> alike, as <see cref="DxfReader"/> reads either.
        /// </summary>
        public const string DegreeCode = "%%d";

        /// <summary>
        /// The control code of the plus-minus sign, <c>%%p</c>, which AutoCAD draws as <see cref="PlusMinus"/> where
        /// <see cref="DegreeCode"/> draws a degree, kept in the words as written in the same way: the <c>Text</c> of an MTEXT of
        /// <c>%%p0.5</c> is ±0.5.
        /// </summary>
        public const string PlusMinusCode = "%%p";

        /// <summary>
        /// The control code of the diameter sign, <c>%%c</c>, which AutoCAD draws as the diameter sign where
        /// <see cref="DegreeCode"/> draws a degree, kept in the words as written in the same way: a TEXT's <c>TextString</c>
        /// of <c>%%c20</c> is <c>%%c20</c>. AutoCAD 2021 gives the <c>Text</c> of an MTEXT of <c>%%c20</c> as ∅20, the letter
        /// <see cref="Diameter"/>; <see cref="DxfMText.Text"/> and <see cref="DxfReader"/> read it as ⌀20, the sign U+2300.
        /// </summary>
        public const string DiameterCode = "%%c";

        /// <summary>The degree sign, °, U+00B0: <c>"45" + Degree</c> is 45°. A letter of the code page ANSI_1252, so a new drawing of any version holds it as it is.</summary>
        public const string Degree = "°";

        /// <summary>The plus-minus sign, ±, U+00B1: <c>PlusMinus + "0.5"</c> is ±0.5. A letter of ANSI_1252, so a new drawing of any version holds it as it is.</summary>
        public const string PlusMinus = "±";

        /// <summary>
        /// The diameter sign, ∅, U+2205, the letter AutoCAD's Symbol menu enters for a diameter and its help lists for
        /// <c>%%c</c>; not Ø, U+00D8, a Latin letter, nor ⌀, U+2300. <c>Diameter + "20"</c> is written into an MTEXT or a TEXT of
        /// R2004 as <c>\U+220520</c> and of R2018 as the bytes E2 88 85 and 20, and reads back as ∅20 from both, in AutoCAD 2021
        /// and in the library.
        /// </summary>
        public const string Diameter = "∅";

        /// <summary>The sign almost equal to, ≈, U+2248: <c>AlmostEqual + "3.14"</c> is ≈3.14.</summary>
        public const string AlmostEqual = "≈";

        /// <summary>The angle sign, ∠, U+2220: <c>Angle + "45" + Degree</c> is ∠45°.</summary>
        public const string Angle = "∠";

        /// <summary>The boundary line of a survey, U+E100, a letter of AutoCAD's own fonts in the private range, which no other font is sure to have.</summary>
        public const string BoundaryLine = "";

        /// <summary>The centre line sign, ℄, U+2104: <c>CenterLine + "A"</c> marks the centre line A.</summary>
        public const string CenterLine = "℄";

        /// <summary>The capital delta, Δ, U+0394: <c>Delta + "L"</c> is ΔL.</summary>
        public const string Delta = "Δ";

        /// <summary>The electrical phase sign, ɸ, U+0278.</summary>
        public const string ElectricalPhase = "ɸ";

        /// <summary>The flow line, U+E101, a letter of AutoCAD's own fonts in the private range, which no other font is sure to have.</summary>
        public const string FlowLine = "";

        /// <summary>The sign identical to, ≡, U+2261.</summary>
        public const string Identity = "≡";

        /// <summary>
        /// The initial length sign, the arc over the length of an arc's dimension, U+E200, a letter of AutoCAD's own fonts in the
        /// private range: AutoCAD's help names simplex, romans, gdt, amgdt and isocpeur as the fonts that have it.
        /// </summary>
        public const string InitialLength = "";

        /// <summary>The monument line of a survey, U+E102, a letter of AutoCAD's own fonts in the private range, which no other font is sure to have.</summary>
        public const string MonumentLine = "";

        /// <summary>The sign not equal to, ≠, U+2260: <c>"a" + NotEqual + "b"</c> is a≠b.</summary>
        public const string NotEqual = "≠";

        /// <summary>The ohm sign, Ω, U+2126, the unit: <c>"50" + Ohm</c> is 50Ω. Not <see cref="Omega"/>, the Greek letter.</summary>
        public const string Ohm = "Ω";

        /// <summary>The capital omega, Ω, U+03A9, the Greek letter. Not <see cref="Ohm"/>, the unit.</summary>
        public const string Omega = "Ω";

        /// <summary>The property line sign, ⅊, U+214A, AutoCAD's "Plate/property line".</summary>
        public const string PropertyLine = "⅊";

        /// <summary>The subscript two, ₂, U+2082: <c>"CO" + Subscript2</c> is CO₂.</summary>
        public const string Subscript2 = "₂";

        /// <summary>The superscript two, ², U+00B2: <c>"20 m" + Squared</c> is 20 m². A letter of ANSI_1252, so a new drawing of any version holds it as it is.</summary>
        public const string Squared = "²";

        /// <summary>The superscript three, ³, U+00B3: <c>"5 m" + Cubed</c> is 5 m³. A letter of ANSI_1252, so a new drawing of any version holds it as it is.</summary>
        public const string Cubed = "³";

        /// <summary>
        /// The space a line is not broken at, U+00A0, which AutoCAD's menu enters with Ctrl+Shift+Space: <c>"M16" +
        /// NonBreakingSpace + "x 40"</c> keeps M16 with its length. A letter of ANSI_1252, so a new drawing of any version holds it
        /// as it is, in a TEXT as in an MTEXT. In an MTEXT the code <see cref="DxfMText.NonBreakSpace"/>, <c>\~</c>, is one too,
        /// which AutoCAD 2021 gives as this letter in an MTEXT's <c>Text</c>;
        /// <see cref="DxfMText.Text"/> and <see cref="DxfReader"/> read it as a plain space.
        /// </summary>
        public const string NonBreakingSpace = " ";
    }
}
