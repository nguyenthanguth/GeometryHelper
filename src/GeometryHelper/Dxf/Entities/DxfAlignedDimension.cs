using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An aligned dimension of a DXF file, as AutoCAD's <c>AlignedDimension</c>: the distance between two points, measured
    /// along the line through them; a DIMENSION of kind 1.
    /// </summary>
    /// <remarks>
    /// The dimension of the points (0, -20, 0) and (80, -60, 0), its line 10 off them, has <see cref="XLine1Point"/> and
    /// <see cref="XLine2Point"/> those points (groups 13 and 14), <see cref="DimLinePoint"/> on its dimension line (10) and a
    /// <see cref="DxfDimension.Measurement"/> of 89.44, the distance between them. As every <see cref="DxfDimension"/>, it is
    /// drawn by its block, which is not drawn anew when a point is moved.
    /// </remarks>
    public sealed class DxfAlignedDimension : DxfDimension
    {
        // The groups of AcDbAlignedDimension in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 12, 22, 32, 13, 23, 33, 14, 24, 34, 50, 52 });

        /// <summary>Initializes an aligned dimension of the origin to the origin, as AutoCAD's <c>new AlignedDimension()</c>, in the style Standard.</summary>
        public DxfAlignedDimension()
            : this(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), string.Empty, null)
        {
        }

        /// <summary>
        /// Initializes an aligned dimension, as AutoCAD's <c>new AlignedDimension(line1Point, line2Point, dimensionLinePoint,
        /// dimensionText, dimensionStyle)</c>, in no file until it is added to one; it needs its <see cref="DxfDimension.DimBlock"/>
        /// before it is.
        /// </summary>
        /// <param name="line1Point">Where the first extension line starts, in world coordinates.</param>
        /// <param name="line2Point">Where the second starts.</param>
        /// <param name="dimensionLinePoint">A point of the dimension line.</param>
        /// <param name="dimensionText">The text, as <see cref="DxfDimension.DimensionText"/>; empty or null for the measurement.</param>
        /// <param name="dimensionStyle">The style it is drawn in, by its name; null for Standard.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfAlignedDimension(GeoPoint3 line1Point, GeoPoint3 line2Point, GeoPoint3 dimensionLinePoint, string dimensionText, DxfDimStyleTableRecord dimensionStyle)
            : base("DIMENSION", 1, dimensionLinePoint, dimensionText, dimensionStyle, Groups(line1Point, line2Point))
        {
        }

        internal DxfAlignedDimension(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets where the first extension line starts (groups 13, 23, 33), as AutoCAD's <c>XLine1Point</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine1Point { get => SubPoint(13); set => SetSubPoint(13, value); }

        /// <summary>Gets or sets where the second extension line starts (groups 14, 24, 34), as AutoCAD's <c>XLine2Point</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine2Point { get => SubPoint(14); set => SetSubPoint(14, value); }

        /// <summary>Gets or sets a point of the dimension line (groups 10, 20, 30), as AutoCAD's <c>DimLinePoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 DimLinePoint { get => DefinitionPoint; set => SetDefinitionPoint(value); }

        /// <summary>
        /// Gets or sets the angle of the extension lines from the square of the dimension line (group 52, in degrees), as
        /// AutoCAD's <c>Oblique</c>: in radians; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Oblique { get => ObliqueAngle(); set => SetOblique(value); }

        /// <summary>Gets the distance between the two points, as AutoCAD's <c>Measurement</c>: 89.44 from (0, -20, 0) to (80, -60, 0).</summary>
        public override double Measurement => XLine1Point.DistanceTo(XLine2Point);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> SubOrder => Order;

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 line1, GeoPoint3 line2)
        {
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbAlignedDimension") };
            groups.AddRange(PointGroups(13, line1));
            groups.AddRange(PointGroups(14, line2));
            return groups;
        }
    }
}
