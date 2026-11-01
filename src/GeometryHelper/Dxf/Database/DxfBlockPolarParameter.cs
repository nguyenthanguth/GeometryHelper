using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A polar parameter of a dynamic block, as <see cref="DxfBlockTableRecord.AddPolarParameter"/> wrote it: the distance from
    /// its base point to its end point and the angle of that line from the block's x axis, shown on every INSERT of the block
    /// as two dynamic properties, and followed by the polar stretch actions tied to it
    /// (<see cref="DxfBlockTableRecord.AddPolarStretchAction"/>), which turn and stretch with its end point.
    /// </summary>
    /// <remarks>
    /// The parameter of the labels Length and Angle from (0, 0, 0) to (100, 0, 0) gives each INSERT the properties Length of
    /// 100 and Angle of nought; Length set to 150 and Angle to a quarter turn, by AutoCAD or by
    /// <see cref="DxfDynamicBlockReferenceProperty.Value"/>, move the end point to (0, 150, 0). What it holds is read once it
    /// is written: changing the block afterwards does not change it.
    /// </remarks>
    public sealed class DxfBlockPolarParameter
    {
        internal DxfBlockPolarParameter(DxfBlockTableRecord block, int node, string handle, string distanceName, string distanceDescription, string angleName, string angleDescription,
            GeoPoint3 basePoint, GeoPoint3 endPoint, double labelOffset, DxfBlockParameterValueSet distanceValueSet, DxfBlockParameterValueSet angleValueSet)
        {
            Block = block;
            Node = node;
            Handle = handle;
            DistanceName = distanceName;
            DistanceDescription = distanceDescription;
            AngleName = angleName;
            AngleDescription = angleDescription;
            BasePoint = basePoint;
            EndPoint = endPoint;
            LabelOffset = labelOffset;
            DistanceValueSet = distanceValueSet;
            AngleValueSet = angleValueSet;
        }

        /// <summary>The block the parameter is of.</summary>
        internal DxfBlockTableRecord Block { get; }

        /// <summary>The id of the parameter's node in the block's evaluation graph (90), which its actions name.</summary>
        internal int Node { get; }

        /// <summary>Gets the handle of the parameter's object, the BLOCKPOLARPARAMETER of the OBJECTS section.</summary>
        public string Handle { get; }

        /// <summary>Gets the label of the distance, the name of its property (305), as Length.</summary>
        public string DistanceName { get; }

        /// <summary>Gets the description of the distance (306); empty where none was given.</summary>
        public string DistanceDescription { get; }

        /// <summary>Gets the label of the angle, the name of its property (307), as Angle.</summary>
        public string AngleName { get; }

        /// <summary>Gets the description of the angle (308); empty where none was given.</summary>
        public string AngleDescription { get; }

        /// <summary>Gets the point the distance is measured from and the angle turns about, in the block's coordinates.</summary>
        public GeoPoint3 BasePoint { get; }

        /// <summary>Gets the point the distance is measured to, in the block's coordinates, which follows the properties.</summary>
        public GeoPoint3 EndPoint { get; }

        /// <summary>
        /// Gets how far the label stands off the line from the base point to the end point (140), to the left of it looking
        /// from the base to the end.
        /// </summary>
        public double LabelOffset { get; }

        /// <summary>Gets the values the distance may take; null where it may take any.</summary>
        public DxfBlockParameterValueSet DistanceValueSet { get; }

        /// <summary>Gets the values the angle may take, in radians; null where it may take any.</summary>
        public DxfBlockParameterValueSet AngleValueSet { get; }

        /// <summary>Gets the distance from the base point to the end point, the value every INSERT shows until its property is set.</summary>
        public double Distance => BasePoint.GetVectorTo(EndPoint).Length;

        /// <summary>
        /// Gets the angle of the line from the base point to the end point, in radians counter-clockwise from the block's x
        /// axis, from nought up to 2 pi: the value every INSERT shows until its property is set.
        /// </summary>
        public double Angle
        {
            get
            {
                double angle = Math.Atan2(EndPoint.Y - BasePoint.Y, EndPoint.X - BasePoint.X);
                double turned = angle < 0.0 ? angle + 2.0 * Math.PI : angle + 0.0;
                return turned >= 2.0 * Math.PI ? 0.0 : turned;
            }
        }
    }
}
