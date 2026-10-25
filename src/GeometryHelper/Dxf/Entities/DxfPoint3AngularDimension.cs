using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An angular dimension of three points of a DXF file, as AutoCAD's <c>Point3AngularDimension</c>: the angle at a centre
    /// between two points, on the side of the arc point; a DIMENSION of kind 5.
    /// </summary>
    /// <remarks>
    /// The dimension about (120, 100, 0) of (160, 100, 0) and (120, 140, 0), its arc through (140, 130, 0), has
    /// <see cref="CenterPoint"/> (group 15), <see cref="XLine1Point"/> and <see cref="XLine2Point"/> (13, 14),
    /// <see cref="ArcPoint"/> (10) and a <see cref="DxfDimension.Measurement"/> of pi / 2. As every <see cref="DxfDimension"/>,
    /// it is drawn by its block, which is not drawn anew when a point is moved.
    /// </remarks>
    public sealed class DxfPoint3AngularDimension : DxfDimension
    {
        // The groups of AcDb3PointAngularDimension in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 13, 23, 33, 14, 24, 34, 15, 25, 35, 16, 26, 36 });

        /// <summary>Initializes an angular dimension of three points at the origin, as AutoCAD's <c>new Point3AngularDimension()</c>, in the style Standard.</summary>
        public DxfPoint3AngularDimension()
            : this(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), string.Empty, null)
        {
        }

        /// <summary>
        /// Initializes an angular dimension of three points, as AutoCAD's <c>new Point3AngularDimension(centerPoint, line1Point,
        /// line2Point, arcPoint, dimensionText, dimensionStyle)</c>, in no file until it is added to one; it needs its
        /// <see cref="DxfDimension.DimBlock"/> before it is.
        /// </summary>
        /// <param name="centerPoint">The vertex of the angle, in world coordinates.</param>
        /// <param name="line1Point">The point of the first leg.</param>
        /// <param name="line2Point">The point of the second leg.</param>
        /// <param name="arcPoint">A point of the dimension's arc.</param>
        /// <param name="dimensionText">The text, as <see cref="DxfDimension.DimensionText"/>; empty or null for the measurement.</param>
        /// <param name="dimensionStyle">The style it is drawn in, by its name; null for Standard.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfPoint3AngularDimension(GeoPoint3 centerPoint, GeoPoint3 line1Point, GeoPoint3 line2Point, GeoPoint3 arcPoint, string dimensionText, DxfDimStyleTableRecord dimensionStyle)
            : base("DIMENSION", 5, arcPoint, dimensionText, dimensionStyle, Groups(line1Point, line2Point, centerPoint))
        {
        }

        internal DxfPoint3AngularDimension(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the vertex of the angle (groups 15, 25, 35), as AutoCAD's <c>CenterPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 CenterPoint { get => SubPoint(15); set => SetSubPoint(15, value); }

        /// <summary>Gets or sets the point of the first leg (groups 13, 23, 33), as AutoCAD's <c>XLine1Point</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine1Point { get => SubPoint(13); set => SetSubPoint(13, value); }

        /// <summary>Gets or sets the point of the second leg (groups 14, 24, 34), as AutoCAD's <c>XLine2Point</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine2Point { get => SubPoint(14); set => SetSubPoint(14, value); }

        /// <summary>Gets or sets a point of the dimension's arc (groups 10, 20, 30), as AutoCAD's <c>ArcPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 ArcPoint { get => DefinitionPoint; set => SetDefinitionPoint(value); }

        /// <summary>
        /// Gets the angle measured, as AutoCAD's <c>Measurement</c>: in radians, about the centre from the first leg to the second,
        /// seen in the plane of the normal, on the side the arc point lies; pi / 2 for legs along x and y with the arc point between.
        /// </summary>
        public override double Measurement
        {
            get
            {
                GeoPoint3 center = CenterPoint;
                return Sweep(center.GetVectorTo(XLine1Point), center.GetVectorTo(XLine2Point), center.GetVectorTo(ArcPoint));
            }
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> SubOrder => Order;

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 line1, GeoPoint3 line2, GeoPoint3 center)
        {
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDb3PointAngularDimension") };
            groups.AddRange(PointGroups(13, line1));
            groups.AddRange(PointGroups(14, line2));
            groups.AddRange(PointGroups(15, center));
            return groups;
        }
    }
}
