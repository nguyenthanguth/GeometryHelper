using System;
using System.Collections.Generic;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A radius dimension of a DXF file, as AutoCAD's <c>RadialDimension</c>: the radius of a circle or an arc, from its centre to
    /// a point on it; a DIMENSION of kind 4.
    /// </summary>
    /// <remarks>
    /// The dimension of the circle of radius 25 about (150, 0, 0), measured at 45 degrees, has <see cref="Center"/> (150, 0, 0)
    /// (group 10), <see cref="ChordPoint"/> (167.68, 17.68, 0) (group 15) and a <see cref="DxfDimension.Measurement"/> of 25. As
    /// every <see cref="DxfDimension"/>, it is drawn by its block, which is not drawn anew when a point is moved.
    /// </remarks>
    public sealed class DxfRadialDimension : DxfDimension
    {
        // The groups of AcDbRadialDimension in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 15, 25, 35, 40 });

        /// <summary>Initializes a radius dimension of the origin, as AutoCAD's <c>new RadialDimension()</c>, in the style Standard.</summary>
        public DxfRadialDimension()
            : this(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), 0.0, string.Empty, null)
        {
        }

        /// <summary>
        /// Initializes a radius dimension, as AutoCAD's <c>new RadialDimension(center, chordPoint, leaderLength, dimensionText,
        /// dimensionStyle)</c>, in no file until it is added to one; it needs its <see cref="DxfDimension.DimBlock"/> before it is.
        /// </summary>
        /// <param name="center">The centre, in world coordinates.</param>
        /// <param name="chordPoint">The point of the circle or arc measured to.</param>
        /// <param name="leaderLength">The length of the leader to the text.</param>
        /// <param name="dimensionText">The text, as <see cref="DxfDimension.DimensionText"/>; empty or null for the measurement.</param>
        /// <param name="dimensionStyle">The style it is drawn in, by its name; null for Standard.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is not a finite number.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfRadialDimension(GeoPoint3 center, GeoPoint3 chordPoint, double leaderLength, string dimensionText, DxfDimStyleTableRecord dimensionStyle)
            : base("DIMENSION", 4, center, dimensionText, dimensionStyle, Groups("AcDbRadialDimension", chordPoint, leaderLength))
        {
        }

        internal DxfRadialDimension(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the centre (groups 10, 20, 30), as AutoCAD's <c>Center</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 Center { get => DefinitionPoint; set => SetDefinitionPoint(value); }

        /// <summary>Gets or sets the point of the circle or arc measured to (groups 15, 25, 35), as AutoCAD's <c>ChordPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 ChordPoint { get => SubPoint(15); set => SetSubPoint(15, value); }

        /// <summary>Gets or sets the length of the leader to the text (group 40), as AutoCAD's <c>LeaderLength</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double LeaderLength { get => SubReal(40, 0.0); set => SetSubReal(40, value, LeaderLength); }

        /// <summary>Gets the radius, from the centre to the chord point, as AutoCAD's <c>Measurement</c>: 25 for a circle of radius 25.</summary>
        public override double Measurement => Center.DistanceTo(ChordPoint);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> SubOrder => Order;

        /// <summary>The groups of a radius or a diameter made in code: its marker, its chord point and its leader's length.</summary>
        internal static IEnumerable<DxfTypedValue> Groups(string marker, GeoPoint3 chord, double leaderLength)
        {
            Guard.Finite(leaderLength, nameof(leaderLength), "A length has to be a finite number.");
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, marker) };
            groups.AddRange(PointGroups(15, chord));
            groups.Add(new DxfTypedValue(40, Numbers.Exact(leaderLength + 0.0)));
            return groups;
        }
    }
}
