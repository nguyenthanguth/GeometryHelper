namespace GeometryHelper.Export
{
    /// <summary>
    /// How a <see cref="DxfTransparency"/> gives its transparency, numbered as AutoCAD's <c>TransparencyMethod</c>.
    /// </summary>
    public enum DxfTransparencyMethod
    {
        /// <summary>The transparency of the entity's layer, what an entity that gives none has.</summary>
        ByLayer = 0,

        /// <summary>The transparency of the INSERT that places the block the entity is in.</summary>
        ByBlock = 1,

        /// <summary>A transparency of its own, its <see cref="DxfTransparency.Alpha"/>.</summary>
        ByAlpha = 2,

        /// <summary>A value a file gives that is none of the others, as AutoCAD names it.</summary>
        ErrorValue = 3,
    }
}
