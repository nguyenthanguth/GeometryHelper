using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An entity of a type the library has no class for, such as a HATCH, a DIMENSION or a MULTILEADER: kept as the file has
    /// it, its groups read as they are, its look editable and the entity erasable.
    /// </summary>
    /// <remarks>
    /// Its <see cref="DxfEntity.Layer"/>, colour, linetype, lineweight and visibility are set as any entity's are, every other
    /// group of its record kept as it was; its geometry is not changed, so <see cref="TransformBy(GeoTransform3)"/> is refused.
    /// It is copied as it is by <see cref="DxfEntity.Clone"/>, and by <see cref="DxfEntity.GetTransformedCopy"/> with the
    /// identity. <see cref="DxfEntity.GetGeometry"/> gives what <see cref="DxfReader"/> reads of it, nothing for a HATCH.
    /// </remarks>
    public sealed class DxfUnknownEntity : DxfEntity
    {
        internal DxfUnknownEntity(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets the type of the entity as the file names it in its group 0, as AutoCAD's class names its <c>DxfName</c>:
        /// <c>HATCH</c>, <c>DIMENSION</c>, <c>ACAD_PROXY_ENTITY</c>.
        /// </summary>
        public string DxfName => Item.Type;

        /// <summary>
        /// Gets every group of the entity's record, in the order of the file, after the group 0 that names its type, each value
        /// the text the file prints; a new list on each call. Of a POLYLINE they run on through its vertices and of an INSERT
        /// through its attributes, each opened by its own group 0. A comment (999) is not among them, though the file keeps it.
        /// </summary>
        public IReadOnlyList<DxfTypedValue> Groups => Item.Groups;

        /// <summary>Refuses: the geometry of a type the library keeps as it is is not changed.</summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override void TransformBy(GeoTransform3 transform)
            => throw new NotSupportedException("A " + DxfName + " is kept as the file has it, and is not moved: the library has no class for it yet.");
    }
}
