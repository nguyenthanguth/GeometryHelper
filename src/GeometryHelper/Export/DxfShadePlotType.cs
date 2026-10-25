namespace GeometryHelper.Export
{
    /// <summary>
    /// How a viewport is shaded when plotted, as AutoCAD's <c>ShadePlotType</c> of a viewport numbers it (group 170).
    /// </summary>
    public enum DxfShadePlotType
    {
        /// <summary>As it is shown, 0.</summary>
        AsDisplayed = 0,

        /// <summary>As wires, 1.</summary>
        Wireframe = 1,

        /// <summary>With the lines behind hidden, 2.</summary>
        Hidden = 2,

        /// <summary>Rendered, 3.</summary>
        Rendered = 3,

        /// <summary>By a visual style (group 333), 4.</summary>
        VisualStyle = 4,

        /// <summary>By a preset of rendering (group 333), 5.</summary>
        RenderPreset = 5,
    }
}
