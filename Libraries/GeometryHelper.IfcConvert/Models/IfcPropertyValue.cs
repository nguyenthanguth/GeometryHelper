using System.Collections.Generic;

namespace GeometryHelper.IfcConvert.Models
{
    /// <summary>
    /// Represents a single IFC property value with its unit and type information.
    /// Does not expose any xBIM types.
    /// </summary>
    public sealed class IfcPropertyValue
    {
        /// <summary>Gets the name of this property.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets the raw value. Possible CLR types: double, int, long, bool, string.
        /// </summary>
        public object Value { get; }

        /// <summary>Gets the unit symbol if available (e.g. "m", "m2", "kg"). Empty if none.</summary>
        public string Unit { get; }

        /// <summary>Gets the IFC type name (e.g. "IfcLengthMeasure", "IfcLabel", "IfcBoolean").</summary>
        public string TypeName { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="IfcPropertyValue"/>.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="value">Raw value (double, int, long, bool, or string).</param>
        /// <param name="typeName">IFC type name (optional).</param>
        /// <param name="unit">Unit symbol (optional).</param>
        public IfcPropertyValue(string name, object value, string typeName = null, string unit = null)
        {
            Name = name ?? string.Empty;
            Value = value;
            TypeName = typeName ?? string.Empty;
            Unit = unit ?? string.Empty;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string unitPart = string.IsNullOrEmpty(Unit) ? string.Empty : " " + Unit;
            return Name + " = " + Value + unitPart + " [" + TypeName + "]";
        }
    }
}