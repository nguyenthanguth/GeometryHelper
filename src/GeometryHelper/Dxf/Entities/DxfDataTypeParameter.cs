using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The kind of a cell's value and what its number measures, together, as AutoCAD's <c>DataTypeParameter</c>: a cell of
    /// 12.5 mm has <see cref="DxfDataType.Double"/> and <see cref="DxfUnitType.Distance"/>.
    /// </summary>
    public readonly struct DxfDataTypeParameter : IEquatable<DxfDataTypeParameter>
    {
        /// <summary>Initializes the pair, as AutoCAD's <c>new DataTypeParameter(dataType, unitType)</c>.</summary>
        /// <param name="dataType">The kind of value.</param>
        /// <param name="unitType">What its number measures.</param>
        public DxfDataTypeParameter(DxfDataType dataType, DxfUnitType unitType)
        {
            DataType = dataType;
            UnitType = unitType;
        }

        /// <summary>Gets the kind of value, as AutoCAD's <c>DataType</c>.</summary>
        public DxfDataType DataType { get; }

        /// <summary>Gets what its number measures, as AutoCAD's <c>UnitType</c>.</summary>
        public DxfUnitType UnitType { get; }

        /// <summary>Gets whether the pair is another, kind and unit alike.</summary>
        /// <param name="other">The other pair.</param>
        /// <returns>Whether they are the same.</returns>
        public bool Equals(DxfDataTypeParameter other) => DataType == other.DataType && UnitType == other.UnitType;

        /// <summary>Gets whether an object is a pair of the same kind and unit.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => obj is DxfDataTypeParameter other && Equals(other);

        /// <summary>Gets a hash of the kind and the unit.</summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode() => unchecked(((int)DataType * 397) ^ (int)UnitType);

        /// <summary>Gives the pair as a line for debugging: <c>Double, Distance</c>.</summary>
        /// <returns>The pair as a line.</returns>
        public override string ToString() => DataType + ", " + UnitType;
    }
}
