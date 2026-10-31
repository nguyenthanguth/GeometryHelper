using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// One dynamic property of an INSERT of a dynamic block, as AutoCAD's <c>DynamicBlockReferenceProperty</c> gives it: the
    /// name a parameter of the block shows it by, its value for this INSERT as AutoCAD saved it, and the values it may take.
    /// Read only: the library does not work a dynamic block out anew, so a value is never set here.
    /// </summary>
    /// <remarks>
    /// The INSERT of a door whose linear parameter Width AutoCAD stretched from 900 to 1 000 gives the property Width, of the
    /// value 1 000.0 (a <see cref="double"/>), <see cref="DxfDynamicBlockReferencePropertyUnitsType.Distance"/>, and
    /// <see cref="GetAllowedValues"/> 800, 900 and 1 000 where the parameter keeps that list. See
    /// <see cref="DxfBlockReference.DynamicBlockReferencePropertyCollection"/> for what each kind of parameter gives.
    /// </remarks>
    public sealed class DxfDynamicBlockReferenceProperty
    {
        private readonly object[] _allowed;

        /// <summary>A property read from a parameter, its allowed values kept as given.</summary>
        internal DxfDynamicBlockReferenceProperty(string name, string description, object value, short typeCode, DxfDynamicBlockReferencePropertyUnitsType units, bool readOnly, object[] allowed)
        {
            PropertyName = name;
            Description = description;
            Value = value;
            PropertyTypeCode = typeCode;
            UnitsType = units;
            ReadOnly = readOnly;
            _allowed = allowed;
        }

        /// <summary>
        /// Gets the name the property is shown by, as AutoCAD's <c>PropertyName</c>: the label of its parameter, Width for a
        /// linear parameter labelled so; a point parameter's label with " X" and " Y" after it, Anchor X and Anchor Y.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets the property's value for this INSERT, as AutoCAD's <c>Value</c>: a <see cref="double"/> for a length or an
        /// angle (in radians), a <see cref="short"/> for the state of a flip (0 not flipped, 1 flipped), a <see cref="string"/>
        /// for a visibility state or a value looked up.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets the kind of value, as AutoCAD's <c>PropertyTypeCode</c> numbers it by its <c>DwgDataType</c>: 1 for a
        /// <see cref="double"/>, 3 for a <see cref="short"/>, 5 for a <see cref="string"/>.
        /// </summary>
        public short PropertyTypeCode { get; }

        /// <summary>Gets what the value measures, as AutoCAD's <c>UnitsType</c>: a length, an angle, or nothing.</summary>
        public DxfDynamicBlockReferencePropertyUnitsType UnitsType { get; }

        /// <summary>
        /// Gets whether AutoCAD lets the property be set, as its <c>ReadOnly</c>: true only for a value looked up whose table
        /// does not allow looking back (its column's reverse lookup off).
        /// </summary>
        public bool ReadOnly { get; }

        /// <summary>Gets the description the parameter gives the property, as AutoCAD's <c>Description</c>; empty where it gives none.</summary>
        public string Description { get; }

        /// <summary>
        /// Gets the values the property may take, as AutoCAD's <c>GetAllowedValues</c>: the list of a parameter's value set,
        /// lengths scaled as <see cref="Value"/> is; every visibility state by name; 0 and 1 for a flip; the values a lookup
        /// table gives. Empty where the parameter keeps no list, as for a value set of an increment or of none.
        /// </summary>
        /// <returns>A new array on each call, its values of the type of <see cref="Value"/>.</returns>
        public object[] GetAllowedValues() => (object[])_allowed.Clone();

        /// <summary>Gives the property as a line for debugging: its name and its value.</summary>
        /// <returns>The line, such as <c>Width = 1000</c>.</returns>
        public override string ToString() => PropertyName + " = " + Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
