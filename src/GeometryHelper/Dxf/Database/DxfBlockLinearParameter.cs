using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A linear parameter of a dynamic block, as <see cref="DxfBlockTableRecord.AddLinearParameter"/> wrote it: the distance
    /// from its base point to its end point, shown on every INSERT of the block as the dynamic property of its name, and
    /// changed by the actions tied to it
    /// (<see cref="DxfBlockTableRecord.AddStretchAction(DxfBlockLinearParameter, GeoPoint2, GeoPoint2, System.Collections.Generic.IEnumerable{DxfEntity})"/>,
    /// <see cref="DxfBlockTableRecord.AddMoveAction(DxfBlockLinearParameter, System.Collections.Generic.IEnumerable{DxfEntity})"/>,
    /// <see cref="DxfBlockTableRecord.AddScaleAction"/>).
    /// </summary>
    /// <remarks>
    /// The parameter Width from (0, 0, 0) to (100, 0, 0) of a rectangle 100 by 50 gives each INSERT of the block the property
    /// Width of 100; set to 200, by AutoCAD or by <see cref="DxfDynamicBlockReferenceProperty.Value"/>, it moves its end point to
    /// (200, 0, 0), and a stretch action tied to it moves the points in its frame by 100 along x. What it holds is read once it is written: changing the block afterwards does not change it.
    /// </remarks>
    public sealed class DxfBlockLinearParameter
    {
        internal DxfBlockLinearParameter(DxfBlockTableRecord block, int node, string handle, string name, string description, GeoPoint3 basePoint, GeoPoint3 endPoint, double labelOffset, DxfBlockParameterValueSet valueSet)
        {
            Block = block;
            Node = node;
            Handle = handle;
            Name = name;
            Description = description;
            BasePoint = basePoint;
            EndPoint = endPoint;
            LabelOffset = labelOffset;
            ValueSet = valueSet;
        }

        /// <summary>The block the parameter is of.</summary>
        internal DxfBlockTableRecord Block { get; }

        /// <summary>The id of the parameter's node in the block's evaluation graph (90), which its actions name.</summary>
        internal int Node { get; }

        /// <summary>Gets the handle of the parameter's object, the BLOCKLINEARPARAMETER of the OBJECTS section.</summary>
        public string Handle { get; }

        /// <summary>Gets the label of the parameter, the name of the property it gives each INSERT (305), as Width.</summary>
        public string Name { get; }

        /// <summary>Gets the description of the property (306); empty where none was given.</summary>
        public string Description { get; }

        /// <summary>Gets the point the distance is measured from, in the block's coordinates, which the stretch does not move.</summary>
        public GeoPoint3 BasePoint { get; }

        /// <summary>Gets the point the distance is measured to, in the block's coordinates, which follows the value set.</summary>
        public GeoPoint3 EndPoint { get; }

        /// <summary>
        /// Gets how far the label stands off the line from the base point to the end point (140), to the left of it looking
        /// from the base to the end: -20 puts the label of a Width along x 20 below it.
        /// </summary>
        public double LabelOffset { get; }

        /// <summary>Gets the values the distance may take; null where it may take any.</summary>
        public DxfBlockParameterValueSet ValueSet { get; }

        /// <summary>Gets the distance from the base point to the end point, the value every INSERT of the block shows until its property is set.</summary>
        public double Distance => BasePoint.GetVectorTo(EndPoint).Length;
    }
}
