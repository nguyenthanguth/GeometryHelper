namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The properties of a line of a table's grid, together, as AutoCAD's <c>GridPropertyParameter</c>: those its
    /// <see cref="PropertyMask"/> names are given or read, the others not; the line of colour 1 and lineweight 0.35 mm has
    /// the mask <c>Color | LineWeight</c>.
    /// </summary>
    public sealed class DxfGridPropertyParameter
    {
        /// <summary>Gets or sets which properties are given, as AutoCAD's <c>PropertyMask</c>.</summary>
        public DxfGridProperties PropertyMask { get; set; }

        /// <summary>Gets or sets the colour, as AutoCAD's <c>Color</c>: one of the index, BYLAYER or BYBLOCK.</summary>
        public DxfColor Color { get; set; }

        /// <summary>Gets or sets the lineweight, as AutoCAD's <c>LineWeight</c>.</summary>
        public DxfLineWeight LineWeight { get; set; }

        /// <summary>Gets or sets whether the line is drawn, as AutoCAD's <c>Visibility</c> of <c>Visible</c>.</summary>
        public bool Visible { get; set; }
    }
}
