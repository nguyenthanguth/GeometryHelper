namespace GeometryHelper.Export
{
    /// <summary>
    /// How finely a shaded plot is made, as AutoCAD's <c>ShadePlotResLevel</c> (group 77).
    /// </summary>
    public enum DxfShadePlotResLevel
    {
        /// <summary>As a draft, 0.</summary>
        Draft = 0,

        /// <summary>As a preview, 1.</summary>
        Preview = 1,

        /// <summary>Normal, 2.</summary>
        Normal = 2,

        /// <summary>For presentation, 3.</summary>
        Presentation = 3,

        /// <summary>As finely as the device plots, 4.</summary>
        Maximum = 4,

        /// <summary>At the dots per inch of group 78, 5.</summary>
        Custom = 5,
    }
}
