namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What a dynamic property of an INSERT measures, as AutoCAD's <c>DynamicBlockReferencePropertyUnitsType</c> numbers it.
    /// </summary>
    public enum DxfDynamicBlockReferencePropertyUnitsType
    {
        /// <summary>Nothing: a state, a choice or a flip, 0.</summary>
        NoUnits = 0,

        /// <summary>An angle, in radians, 1.</summary>
        Angular = 1,

        /// <summary>A length, in the units of the drawing, 2.</summary>
        Distance = 2,

        /// <summary>An area, in the square of the units of the drawing, 3.</summary>
        Area = 3,
    }
}
