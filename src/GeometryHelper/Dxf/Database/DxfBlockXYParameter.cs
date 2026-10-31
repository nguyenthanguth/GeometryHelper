using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An XY parameter of a dynamic block, as <see cref="DxfBlockTableRecord.AddXYParameter"/> wrote it: the distances along x
    /// and along y from a base point to an end point, shown on every INSERT of the block as two dynamic properties, and
    /// followed by the stretch and move actions tied to it, which follow its end point both ways.
    /// </summary>
    /// <remarks>
    /// The parameter of the labels Width and Depth from (0, 0, 0) to (100, 50, 0) gives each INSERT the properties Width of 100
    /// and Depth of 50; Width set to 160 and Depth to 80, by AutoCAD or by <see cref="DxfDynamicBlockReferenceProperty.Value"/>,
    /// move the end point to (160, 80, 0), and a stretch tied to it moves the points in its frame by 60 along x and 30 along y.
    /// What it holds is read once it is written: changing the block afterwards does not change it.
    /// </remarks>
    public sealed class DxfBlockXYParameter
    {
        internal DxfBlockXYParameter(DxfBlockTableRecord block, int node, string handle, string xName, string xDescription, string yName, string yDescription, GeoPoint3 basePoint, GeoPoint3 endPoint)
        {
            Block = block;
            Node = node;
            Handle = handle;
            XName = xName;
            XDescription = xDescription;
            YName = yName;
            YDescription = yDescription;
            BasePoint = basePoint;
            EndPoint = endPoint;
        }

        /// <summary>The block the parameter is of.</summary>
        internal DxfBlockTableRecord Block { get; }

        /// <summary>The id of the parameter's node in the block's evaluation graph (90), which its actions name.</summary>
        internal int Node { get; }

        /// <summary>Gets the handle of the parameter's object, the BLOCKXYPARAMETER of the OBJECTS section.</summary>
        public string Handle { get; }

        /// <summary>Gets the label of the distance along x, the name of its property (306), as Width.</summary>
        public string XName { get; }

        /// <summary>Gets the description of the distance along x (308); empty where none was given.</summary>
        public string XDescription { get; }

        /// <summary>Gets the label of the distance along y, the name of its property (305), as Depth.</summary>
        public string YName { get; }

        /// <summary>Gets the description of the distance along y (307); empty where none was given.</summary>
        public string YDescription { get; }

        /// <summary>Gets the point the distances are measured from, in the block's coordinates.</summary>
        public GeoPoint3 BasePoint { get; }

        /// <summary>Gets the point the distances are measured to, in the block's coordinates, which follows the properties.</summary>
        public GeoPoint3 EndPoint { get; }

        /// <summary>Gets the distance along x from the base point to the end point, the value every INSERT shows until its property is set.</summary>
        public double XDistance => Math.Abs(EndPoint.X - BasePoint.X);

        /// <summary>Gets the distance along y from the base point to the end point, the value every INSERT shows until its property is set.</summary>
        public double YDistance => Math.Abs(EndPoint.Y - BasePoint.Y);
    }
}
