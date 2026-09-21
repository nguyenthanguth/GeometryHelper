using System;
using System.Collections.Generic;

namespace GeometryHelper.IfcConvert.Models
{
    /// <summary>
    /// Represents a named set of IFC properties (IfcPropertySet or IfcQuantitySet).
    /// Does not expose any xBIM types.
    /// </summary>
    public sealed class IfcPropertySet
    {
        private readonly Dictionary<string, IfcPropertyValue> _properties;

        /// <summary>Gets the name (e.g. "Pset_WallCommon", "BaseQuantities").</summary>
        public string Name { get; }

        /// <summary>Gets all properties keyed by name (case-insensitive).</summary>
        public IReadOnlyDictionary<string, IfcPropertyValue> Properties => _properties;

        /// <summary>
        /// Initializes a new instance of <see cref="IfcPropertySet"/>.
        /// </summary>
        /// <param name="name">Property set name.</param>
        /// <param name="properties">Collection of property values.</param>
        public IfcPropertySet(string name, IEnumerable<IfcPropertyValue> properties)
        {
            Name = name ?? string.Empty;
            _properties = new Dictionary<string, IfcPropertyValue>(StringComparer.OrdinalIgnoreCase);
            if (properties != null)
            {
                foreach (IfcPropertyValue prop in properties)
                {
                    if (prop != null && !string.IsNullOrWhiteSpace(prop.Name))
                        _properties[prop.Name] = prop;
                }
            }
        }

        /// <summary>Tries to get a property by name (case-insensitive).</summary>
        public bool TryGetProperty(string propertyName, out IfcPropertyValue value)
            => _properties.TryGetValue(propertyName ?? string.Empty, out value);

        /// <inheritdoc />
        public override string ToString() => "IfcPropertySet(" + Name + ", " + _properties.Count + " properties)";
    }
}