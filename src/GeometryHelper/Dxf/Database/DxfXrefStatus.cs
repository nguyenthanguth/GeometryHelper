namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Where an external reference stands, as AutoCAD's <c>XrefStatus</c> names it and numbers it, for
    /// <see cref="DxfBlockTableRecord.XrefStatus"/>.
    /// </summary>
    public enum DxfXrefStatus
    {
        /// <summary>The block is no external reference.</summary>
        NotAnXref = 0,

        /// <summary>The external reference was resolved when AutoCAD saved the drawing (flag 32 of its BLOCK).</summary>
        Resolved = 1,

        /// <summary>The external reference is unloaded; never given from a DXF file, which does not say so.</summary>
        Unloaded = 2,

        /// <summary>No INSERT places the external reference.</summary>
        Unreferenced = 3,

        /// <summary>The drawing the external reference comes from is not found at its path.</summary>
        FileNotFound = 4,

        /// <summary>The external reference is not resolved, as one attached and not yet opened by AutoCAD.</summary>
        Unresolved = 5,
    }
}
