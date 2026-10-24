namespace GeometryHelper.Export
{
    /// <summary>
    /// How a TEXT, an ATTRIB or an ATTDEF stands up and down from its alignment point, as AutoCAD's <c>TextVerticalMode</c>
    /// numbers it: group 73 of a TEXT, group 74 of an ATTRIB or an ATTDEF, so <see cref="TextTop"/> hangs the text from its point.
    /// </summary>
    public enum DxfTextVerticalMode
    {
        /// <summary>Standing on its baseline, 0: what a text that gives none has.</summary>
        TextBase = 0,

        /// <summary>Standing on the bottom of its lowest letters, 1.</summary>
        TextBottom = 1,

        /// <summary>Centred up and down on its point, 2.</summary>
        TextVerticalMid = 2,

        /// <summary>Hanging from the top of its tallest letters, 3.</summary>
        TextTop = 3,
    }
}
