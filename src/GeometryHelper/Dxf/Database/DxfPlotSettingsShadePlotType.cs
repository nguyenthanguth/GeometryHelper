namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How the viewports of a layout are shaded when it is plotted, as AutoCAD's <c>PlotSettingsShadePlotType</c> (group
    /// 76).
    /// </summary>
    public enum DxfPlotSettingsShadePlotType
    {
        /// <summary>As they are shown, 0.</summary>
        AsDisplayed = 0,

        /// <summary>As wires, 1.</summary>
        Wireframe = 1,

        /// <summary>With the lines behind hidden, 2.</summary>
        Hidden = 2,

        /// <summary>Rendered, 3.</summary>
        Rendered = 3,

        /// <summary>By a visual style, 4.</summary>
        VisualStyle = 4,

        /// <summary>By a preset of rendering, 5.</summary>
        RenderPreset = 5,
    }
}
