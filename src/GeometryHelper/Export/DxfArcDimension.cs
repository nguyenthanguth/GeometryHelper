using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An arc length dimension of a DXF file, as AutoCAD's <c>ArcDimension</c>: the length of an arc between two points of it,
    /// an ARC_DIMENSION (R2004 on; ezdxf writes one from R2000).
    /// </summary>
    /// <remarks>
    /// The dimension of the arc about (200, 100, 0) from (240, 100, 0) to (200, 140, 0), its own arc through (230, 130, 0), has
    /// <see cref="CenterPoint"/> (group 15), <see cref="XLine1Point"/> and <see cref="XLine2Point"/> (13, 14), <see cref="ArcPoint"/>
    /// (10) and a <see cref="DxfDimension.Measurement"/> of 62.83, a quarter of a circle of radius 40. Its kind (group 70) is 5
    /// in a file older than R2018 and 8 from R2018 on, as AutoCAD and ezdxf write it; a copy added to a file of the other kind of
    /// version is written so. As every <see cref="DxfDimension"/>, it is drawn by its block, which is not drawn anew when a
    /// point is moved.
    /// </remarks>
    public sealed class DxfArcDimension : DxfDimension
    {
        // The groups of AcDbArcDimension in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 13, 23, 33, 14, 24, 34, 15, 25, 35, 40, 41, 70, 71, 16, 26, 36, 17, 27, 37 });

        private static readonly IReadOnlyList<int> World = Array.AsReadOnly(new[] { 13, 14, 15, 16, 17 });

        /// <summary>
        /// Initializes an arc length dimension, as AutoCAD's <c>new ArcDimension(centerPoint, xLine1Point, xLine2Point, arcPoint,
        /// dimensionText, dimensionStyle)</c>, in no file until it is added to one; it needs its
        /// <see cref="DxfDimension.DimBlock"/> before it is.
        /// </summary>
        /// <param name="centerPoint">The centre of the arc, in world coordinates.</param>
        /// <param name="xLine1Point">The point of the arc where the length starts.</param>
        /// <param name="xLine2Point">The point where it ends.</param>
        /// <param name="arcPoint">A point of the dimension's arc.</param>
        /// <param name="dimensionText">The text, as <see cref="DxfDimension.DimensionText"/>; empty or null for the measurement.</param>
        /// <param name="dimensionStyle">The style it is drawn in, by its name; null for Standard.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfArcDimension(GeoPoint3 centerPoint, GeoPoint3 xLine1Point, GeoPoint3 xLine2Point, GeoPoint3 arcPoint, string dimensionText, DxfDimStyleTableRecord dimensionStyle)
            : base("ARC_DIMENSION", 8, arcPoint, dimensionText, dimensionStyle, Groups(xLine1Point, xLine2Point, centerPoint))
        {
        }

        private DxfArcDimension(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the centre of the arc (groups 15, 25, 35), as AutoCAD's <c>CenterPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 CenterPoint { get => SubPoint(15); set => SetSubPoint(15, value); }

        /// <summary>Gets or sets the point of the arc where the length starts (groups 13, 23, 33), as AutoCAD's <c>XLine1Point</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine1Point { get => SubPoint(13); set => SetSubPoint(13, value); }

        /// <summary>Gets or sets the point of the arc where the length ends (groups 14, 24, 34), as AutoCAD's <c>XLine2Point</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine2Point { get => SubPoint(14); set => SetSubPoint(14, value); }

        /// <summary>Gets or sets a point of the dimension's arc (groups 10, 20, 30), as AutoCAD's <c>ArcPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 ArcPoint { get => DefinitionPoint; set => SetDefinitionPoint(value); }

        /// <summary>Gets or sets the parameter of the arc where a partial length starts (group 40 of AcDbArcDimension), as AutoCAD's <c>ArcStartParam</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double ArcStartParam { get => SubReal(40, 0.0); set => SetSubReal(40, value, ArcStartParam); }

        /// <summary>Gets or sets the parameter of the arc where a partial length ends (group 41 of AcDbArcDimension), as AutoCAD's <c>ArcEndParam</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double ArcEndParam { get => SubReal(41, 0.0); set => SetSubReal(41, value, ArcEndParam); }

        /// <summary>Gets or sets whether the length is of a part of the arc (group 70 of AcDbArcDimension), as AutoCAD's <c>IsPartial</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool IsPartial { get => SubWhole(70, 0) != 0; set => SetSubWhole(70, value ? 1 : 0, IsPartial ? 1 : 0); }

        /// <summary>Gets or sets whether a leader is drawn to the arc (group 71 of AcDbArcDimension), as AutoCAD's <c>HasLeader</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool HasLeader { get => SubWhole(71, 0) != 0; set => SetSubWhole(71, value ? 1 : 0, HasLeader ? 1 : 0); }

        /// <summary>Gets or sets the first point of the leader (groups 16, 26, 36), as AutoCAD's <c>Leader1Point</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 Leader1Point { get => SubPoint(16); set => SetSubPoint(16, value); }

        /// <summary>Gets or sets the second point of the leader (groups 17, 27, 37), as AutoCAD's <c>Leader2Point</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 Leader2Point { get => SubPoint(17); set => SetSubPoint(17, value); }

        /// <summary>
        /// Gets or sets where the arc symbol stands, as AutoCAD's <c>ArcSymbolType</c>: the dimension's <see cref="DxfDimension.Dimarcsym"/>,
        /// 0 before the text, 1 above it, 2 none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int ArcSymbolType { get => Dimarcsym; set => Dimarcsym = value; }

        /// <summary>
        /// Gets the length of the arc between the two points, as AutoCAD's <c>Measurement</c>: its radius, from the centre to the
        /// first point, by the angle swept on the side of the arc point, seen in the plane of the normal; 62.83 for a quarter of a
        /// circle of radius 40.
        /// </summary>
        public override double Measurement
        {
            get
            {
                GeoPoint3 center = CenterPoint;
                return center.DistanceTo(XLine1Point) * Sweep(center.GetVectorTo(XLine1Point), center.GetVectorTo(XLine2Point), center.GetVectorTo(ArcPoint));
            }
        }

        [DxfType("ARC_DIMENSION")]
        internal static new DxfEntity Create(DxfItem item) => new DxfArcDimension(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> SubOrder => Order;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> WorldPoints => World;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> PlanePoints => new int[0];

        /// <summary>The kind written as the drawing's version writes it: 5 before R2018, 8 from R2018 on, its flags kept.</summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            base.Appending(owner);
            int kind = Kind;
            int wanted = (kind & ~15) | (owner.File.Supports(1032) ? 8 : 5);

            if (wanted != kind)
            {
                Write(g => g.Set(70, wanted.ToString(CultureInfo.InvariantCulture), g.Base, new[] { 70 }));
            }
        }

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 line1, GeoPoint3 line2, GeoPoint3 center)
        {
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbArcDimension") };
            groups.AddRange(PointGroups(13, line1));
            groups.AddRange(PointGroups(14, line2));
            groups.AddRange(PointGroups(15, center));
            groups.Add(new DxfTypedValue(40, "0.0"));
            groups.Add(new DxfTypedValue(41, "0.0"));
            groups.Add(new DxfTypedValue(70, "0"));
            groups.Add(new DxfTypedValue(71, "0"));
            return groups;
        }
    }
}
