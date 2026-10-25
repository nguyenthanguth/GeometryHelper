namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How a TEXT, an ATTRIB or an ATTDEF stands across its alignment point, as AutoCAD's <c>TextHorizontalMode</c> numbers it:
    /// group 72 of the record, so <see cref="TextCenter"/> is a text centred on its point.
    /// </summary>
    public enum DxfTextHorizontalMode
    {
        /// <summary>Starting at its position, 0: what a text that gives none has.</summary>
        TextLeft = 0,

        /// <summary>Centred on its alignment point, 1.</summary>
        TextCenter = 1,

        /// <summary>Ending at its alignment point, 2.</summary>
        TextRight = 2,

        /// <summary>Running from its position to its alignment point, its height following its length, 3.</summary>
        TextAlign = 3,

        /// <summary>Centred both ways on its alignment point, 4.</summary>
        TextMid = 4,

        /// <summary>Running from its position to its alignment point, its width following its length, 5.</summary>
        TextFit = 5,
    }
}
