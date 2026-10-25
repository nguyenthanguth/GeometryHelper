using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An ordinate dimension of a DXF file, as AutoCAD's <c>OrdinateDimension</c>: how far a point stands from an origin along x
    /// or along y; a DIMENSION of kind 6, flag 64 of its group 70 set for one along x.
    /// </summary>
    /// <remarks>
    /// The dimension along x of (300, 40, 0) from the origin (280, 10, 0), its leader ending at (300, 60, 0), has
    /// <see cref="Origin"/> (group 10), <see cref="DefiningPoint"/> (13), <see cref="LeaderEndPoint"/> (14),
    /// <see cref="UsingXAxis"/> true and a <see cref="DxfDimension.Measurement"/> of 20. As every <see cref="DxfDimension"/>, it
    /// is drawn by its block, which is not drawn anew when a point is moved.
    /// </remarks>
    public sealed class DxfOrdinateDimension : DxfDimension
    {
        // The groups of AcDbOrdinateDimension in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 13, 23, 33, 14, 24, 34 });

        /// <summary>Initializes an ordinate dimension along y at the origin, as AutoCAD's <c>new OrdinateDimension()</c>, in the style Standard.</summary>
        public DxfOrdinateDimension()
            : this(false, new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), string.Empty, null)
        {
        }

        /// <summary>
        /// Initializes an ordinate dimension, as AutoCAD's <c>new OrdinateDimension(useXAxis, definingPoint, leaderEndPoint,
        /// dimText, dimStyle)</c>, from the origin of the world, in no file until it is added to one; it needs its
        /// <see cref="DxfDimension.DimBlock"/> before it is.
        /// </summary>
        /// <param name="useXAxis">Whether it measures along x; along y where false.</param>
        /// <param name="definingPoint">The point measured, in world coordinates.</param>
        /// <param name="leaderEndPoint">Where its leader ends.</param>
        /// <param name="dimText">The text, as <see cref="DxfDimension.DimensionText"/>; empty or null for the measurement.</param>
        /// <param name="dimStyle">The style it is drawn in, by its name; null for Standard.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfOrdinateDimension(bool useXAxis, GeoPoint3 definingPoint, GeoPoint3 leaderEndPoint, string dimText, DxfDimStyleTableRecord dimStyle)
            : base("DIMENSION", useXAxis ? 6 | 64 : 6, new GeoPoint3(0.0, 0.0, 0.0), dimText, dimStyle, Groups(definingPoint, leaderEndPoint))
        {
        }

        internal DxfOrdinateDimension(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the origin measured from (groups 10, 20, 30), as AutoCAD's <c>Origin</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 Origin { get => DefinitionPoint; set => SetDefinitionPoint(value); }

        /// <summary>Gets or sets the point measured (groups 13, 23, 33), as AutoCAD's <c>DefiningPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 DefiningPoint { get => SubPoint(13); set => SetSubPoint(13, value); }

        /// <summary>Gets or sets where the leader ends (groups 14, 24, 34), as AutoCAD's <c>LeaderEndPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 LeaderEndPoint { get => SubPoint(14); set => SetSubPoint(14, value); }

        /// <summary>Gets or sets whether the dimension measures along x (flag 64 of group 70), as AutoCAD's <c>UsingXAxis</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool UsingXAxis
        {
            get => (Kind & 64) != 0;

            set
            {
                if (value != UsingXAxis)
                {
                    Write(g => SetFlag(g, 64, value));
                }
            }
        }

        /// <summary>Gets whether the dimension measures along y (flag 64 of group 70 not set), as AutoCAD's <c>UsingYAxis</c>.</summary>
        public bool UsingYAxis => !UsingXAxis;

        /// <summary>
        /// Gets how far the point stands from the origin along x or y of the user's system the dimension was made in, as AutoCAD's
        /// <c>Measurement</c>, its size: 20 for (300, 40, 0) from (280, 10, 0) along x. That system's x lies in the plane of the
        /// normal at the angle <see cref="DxfDimension.HorizontalRotation"/> gives, the negative of it from the x of the normal's
        /// system (group 51, as the DXF reference says), so that a dimension moved or turned, which carries its 51 along,
        /// measures the same.
        /// </summary>
        public override double Measurement
        {
            get
            {
                OcsAxes(Normal, out GeoVector3 u, out GeoVector3 v);
                double angle = -HorizontalRotation;
                GeoVector3 x = u.Multiply(Math.Cos(angle)).Add(v.Multiply(Math.Sin(angle)));
                GeoVector3 y = v.Multiply(Math.Cos(angle)).Subtract(u.Multiply(Math.Sin(angle)));
                return Math.Abs(Origin.GetVectorTo(DefiningPoint).DotProduct(UsingXAxis ? x : y));
            }
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> SubOrder => Order;

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 defining, GeoPoint3 leaderEnd)
        {
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbOrdinateDimension") };
            groups.AddRange(PointGroups(13, defining));
            groups.AddRange(PointGroups(14, leaderEnd));
            return groups;
        }
    }
}
