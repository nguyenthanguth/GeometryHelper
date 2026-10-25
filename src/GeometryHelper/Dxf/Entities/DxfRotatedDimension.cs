using System;
using System.Collections.Generic;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A rotated dimension of a DXF file, as AutoCAD's <c>RotatedDimension</c>: the distance between two points measured along
    /// a direction, level, upright or turned; a DIMENSION of kind 0.
    /// </summary>
    /// <remarks>
    /// The dimension of (0, 0, 0) and (100, 20, 0) along 30 degrees has <see cref="Rotation"/> pi / 6 (group 50 of 30) and a
    /// <see cref="DxfDimension.Measurement"/> of 96.60, the distance between the points along that direction. As every
    /// <see cref="DxfDimension"/>, it is drawn by its block, which is not drawn anew when a point is moved.
    /// </remarks>
    public sealed class DxfRotatedDimension : DxfDimension
    {
        // The groups of AcDbAlignedDimension and AcDbRotatedDimension in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 12, 22, 32, 13, 23, 33, 14, 24, 34, 50, 52, 100 });

        /// <summary>Initializes a level dimension of the origin to the origin, as AutoCAD's <c>new RotatedDimension()</c>, in the style Standard.</summary>
        public DxfRotatedDimension()
            : this(0.0, new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), string.Empty, null)
        {
        }

        /// <summary>
        /// Initializes a rotated dimension, as AutoCAD's <c>new RotatedDimension(rotation, line1Point, line2Point,
        /// dimensionLinePoint, dimensionText, dimensionStyle)</c>, in no file until it is added to one; it needs its
        /// <see cref="DxfDimension.DimBlock"/> before it is.
        /// </summary>
        /// <param name="rotation">The direction measured along, in radians from the x axis: nought level, pi / 2 upright.</param>
        /// <param name="line1Point">Where the first extension line starts, in world coordinates.</param>
        /// <param name="line2Point">Where the second starts.</param>
        /// <param name="dimensionLinePoint">A point of the dimension line.</param>
        /// <param name="dimensionText">The text, as <see cref="DxfDimension.DimensionText"/>; empty or null for the measurement.</param>
        /// <param name="dimensionStyle">The style it is drawn in, by its name; null for Standard.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the rotation is not a finite number.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfRotatedDimension(double rotation, GeoPoint3 line1Point, GeoPoint3 line2Point, GeoPoint3 dimensionLinePoint, string dimensionText, DxfDimStyleTableRecord dimensionStyle)
            : base("DIMENSION", 0, dimensionLinePoint, dimensionText, dimensionStyle, Groups(rotation, line1Point, line2Point))
        {
        }

        internal DxfRotatedDimension(DxfItem item)
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
        /// Gets or sets the direction measured along (group 50, in degrees, in the plane of the normal), as AutoCAD's
        /// <c>Rotation</c>: in radians, nought along x, pi / 2 along y; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Rotation
        {
            get => SubReal(50, 0.0) * (Math.PI / 180.0);
            set => SetAngle(50, value, Rotation, true);
        }

        /// <summary>Gets or sets the angle of the extension lines from the square of the dimension line (group 52, in degrees), as AutoCAD's <c>Oblique</c>: in radians.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Oblique { get => ObliqueAngle(); set => SetOblique(value); }

        /// <summary>
        /// Gets the distance between the two points along <see cref="Rotation"/>, as AutoCAD's <c>Measurement</c>, the direction
        /// taken in the plane of the normal: 96.60 from (0, 0, 0) to (100, 20, 0) along 30 degrees.
        /// </summary>
        public override double Measurement
        {
            get
            {
                double rotation = Rotation;
                OcsAxes(Normal, out GeoVector3 u, out GeoVector3 v);
                GeoVector3 along = u.Multiply(Math.Cos(rotation)).Add(v.Multiply(Math.Sin(rotation)));
                return Math.Abs(XLine1Point.GetVectorTo(XLine2Point).DotProduct(along));
            }
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> SubOrder => Order;

        private static IEnumerable<DxfTypedValue> Groups(double rotation, GeoPoint3 line1, GeoPoint3 line2)
        {
            Guard.Finite(rotation, nameof(rotation), "A rotation has to be a finite number.");
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbAlignedDimension") };
            groups.AddRange(PointGroups(13, line1));
            groups.AddRange(PointGroups(14, line2));
            groups.Add(new DxfTypedValue(50, Numbers.Exact(DxfOcsTransform.Degrees(rotation))));
            groups.Add(new DxfTypedValue(100, "AcDbRotatedDimension"));
            return groups;
        }
    }
}
