namespace GeometryHelper.Export
{
    /// <summary>
    /// What marks the centre of a radial dimension, as AutoCAD's <c>DimensionCenterMarkType</c> numbers it: by the sign of
    /// DIMCEN, a mark above nought, lines below.
    /// </summary>
    public enum DxfDimensionCenterMarkType
    {
        /// <summary>A cross at the centre, 0: DIMCEN above nought.</summary>
        Mark = 0,

        /// <summary>A cross and centre lines through the circle, 1: DIMCEN below nought.</summary>
        Line = 1,

        /// <summary>Nothing, 2: DIMCEN nought.</summary>
        None = 2,
    }
}
