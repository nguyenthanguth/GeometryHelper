using System;
using Xbim.Common;
using Xbim.Common.Metadata;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Resolves IFC entity type names from the EXPRESS schema metadata rather than CLR type names,
    /// so that names such as "IfcWall" are reported exactly as defined in the schema.
    /// </summary>
    internal static class IfcTypeNames
    {
        /// <summary>
        /// Gets the EXPRESS entity name (e.g. "IfcWall", "IfcBeam") of an IFC entity.
        /// </summary>
        /// <param name="entity">The IFC entity.</param>
        /// <returns>The EXPRESS entity name, or an empty string when <paramref name="entity"/> is null.</returns>
        public static string GetName(IPersistEntity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            return entity.ExpressType?.ExpressName ?? entity.GetType().Name;
        }

        // Supertypes of products that do not represent physical material.
        // IfcSpatialStructureElement is listed for IFC2x3, which has no IfcSpatialElement.
        private static readonly string[] NonPhysicalTypes =
        {
            "IfcFeatureElementSubtraction", "IfcSpatialElement", "IfcSpatialStructureElement",
            "IfcAnnotation", "IfcGrid", "IfcVirtualElement", "IfcStructuralItem", "IfcPort"
        };

        /// <summary>
        /// Checks whether a product represents physical material, i.e. it is not an opening, a spatial element,
        /// an annotation, a grid, a virtual element, a port or a structural analysis item.
        /// </summary>
        public static bool IsPhysical(IPersistEntity entity)
        {
            foreach (string typeName in NonPhysicalTypes)
            {
                if (IsOfType(entity, typeName))
                {
                    return false;
                }
            }

            return entity != null;
        }

        /// <summary>
        /// Checks whether an IFC entity is of the given EXPRESS type or one of its subtypes.
        /// The comparison is case-insensitive and the "Ifc" prefix is optional ("Wall" matches "IfcWall").
        /// </summary>
        /// <param name="entity">The IFC entity.</param>
        /// <param name="typeName">The EXPRESS type name to test against.</param>
        /// <returns>true if the entity is of that type or a subtype of it; otherwise false.</returns>
        public static bool IsOfType(IPersistEntity entity, string typeName)
        {
            if (entity == null || string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            string name = typeName.Trim();
            string prefixed = name.StartsWith("Ifc", StringComparison.OrdinalIgnoreCase) ? name : "Ifc" + name;

            for (ExpressType type = entity.ExpressType; type != null; type = type.SuperType)
            {
                if (string.Equals(type.ExpressName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type.ExpressName, prefixed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
