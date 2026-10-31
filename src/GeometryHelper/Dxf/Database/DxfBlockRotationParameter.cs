using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A rotation parameter of a dynamic block, as <see cref="DxfBlockTableRecord.AddRotationParameter"/> wrote it: an angle
    /// about a base point, shown on every INSERT of the block as the dynamic property of its name, in radians, and followed by
    /// the rotate actions tied to it (<see cref="DxfBlockTableRecord.AddRotateAction"/>).
    /// </summary>
    /// <remarks>
    /// The parameter Angle about (50, 25, 0) of the radius 30 and the angle nought gives each INSERT the property Angle of
    /// nought; set to a quarter turn (pi / 2), by AutoCAD or by <see cref="DxfDynamicBlockReferenceProperty.Value"/>, it turns
    /// the entities of a rotate action tied to it a quarter turn counter-clockwise about (50, 25). What it holds is read once
    /// it is written: changing the block afterwards does not change it.
    /// </remarks>
    public sealed class DxfBlockRotationParameter
    {
        internal DxfBlockRotationParameter(DxfBlockTableRecord block, int node, string handle, string name, string description, GeoPoint3 basePoint, double radius, double angle)
        {
            Block = block;
            Node = node;
            Handle = handle;
            Name = name;
            Description = description;
            BasePoint = basePoint;
            Radius = radius;
            Angle = angle;
        }

        /// <summary>The block the parameter is of.</summary>
        internal DxfBlockTableRecord Block { get; }

        /// <summary>The id of the parameter's node in the block's evaluation graph (90), which its actions name.</summary>
        internal int Node { get; }

        /// <summary>Gets the handle of the parameter's object, the BLOCKROTATIONPARAMETER of the OBJECTS section.</summary>
        public string Handle { get; }

        /// <summary>Gets the label of the parameter, the name of the property it gives each INSERT (305), as Angle.</summary>
        public string Name { get; }

        /// <summary>Gets the description of the property (306); empty where none was given.</summary>
        public string Description { get; }

        /// <summary>Gets the point the angle turns about, in the block's coordinates.</summary>
        public GeoPoint3 BasePoint { get; }

        /// <summary>Gets how far the parameter's grip stands from the base point.</summary>
        public double Radius { get; }

        /// <summary>
        /// Gets the angle as given, in radians counter-clockwise from the block's x axis: the angle of the grip about the base
        /// point, which every INSERT of the block shows, taken into a turn, until its property is set.
        /// </summary>
        public double Angle { get; }
    }
}
