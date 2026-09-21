namespace GeometryHelper.IfcConvert.Models
{
    /// <summary>
    /// Lightweight metadata describing an IFC product without converting its 3D geometry.
    /// </summary>
    public sealed class IfcProductMetadata
    {
        /// <summary>
        /// Gets the GlobalId (GUID) of the product.
        /// </summary>
        public string GlobalId { get; }

        /// <summary>
        /// Gets the name of the product.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the IFC entity type name (e.g. "IfcWall", "IfcBeam").
        /// </summary>
        public string IfcType { get; }

        /// <summary>
        /// Gets the product tag, if available.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="IfcProductMetadata"/>.
        /// </summary>
        /// <param name="globalId">The GlobalId (GUID) of the product.</param>
        /// <param name="name">The name of the product.</param>
        /// <param name="ifcType">The IFC entity type name.</param>
        /// <param name="tag">The product tag (optional).</param>
        public IfcProductMetadata(string globalId, string name, string ifcType, string tag = null)
        {
            GlobalId = globalId ?? string.Empty;
            Name = name ?? string.Empty;
            IfcType = ifcType ?? string.Empty;
            Tag = tag ?? string.Empty;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{IfcType} (Name: '{Name}', GUID: {GlobalId})";
        }
    }
}