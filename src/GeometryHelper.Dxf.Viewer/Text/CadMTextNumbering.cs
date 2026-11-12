namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// How the MTEXT editor labels paragraphs as a list (<see cref="CadMTextEditor.SetNumbering"/>), as AutoCAD 2021's Bullets
    /// and Numbering writes them: each paragraph opens with its label and a tab, and hangs 3 heights in.
    /// </summary>
    public enum CadMTextNumbering
    {
        /// <summary>No label: the labels, the hanging indent and its tab stop taken away.</summary>
        Off,

        /// <summary>A bullet, written <c>{\fSymbol|b0|i0|c2|p18;·\t}</c>: the middle dot of the Symbol font.</summary>
        Bullet,

        /// <summary>Numbers: <c>1.</c>, <c>2.</c>, <c>3.</c>.</summary>
        Number,

        /// <summary>Small letters: <c>a.</c>, <c>b.</c>, <c>c.</c>.</summary>
        LetterLower,

        /// <summary>Capital letters: <c>A.</c>, <c>B.</c>, <c>C.</c>.</summary>
        LetterUpper,
    }
}
