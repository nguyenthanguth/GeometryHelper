namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The units the drawing's hatch patterns and linetypes are taken from, as AutoCAD's <c>MeasurementValue</c> numbers them
    /// (<c>$MEASUREMENT</c>, group 70).
    /// </summary>
    public enum DxfMeasurementValue
    {
        /// <summary>Imperial, 0: the patterns and linetypes of acad.pat and acad.lin.</summary>
        English = 0,

        /// <summary>Metric, 1: those of acadiso.pat and acadiso.lin.</summary>
        Metric = 1,
    }
}
