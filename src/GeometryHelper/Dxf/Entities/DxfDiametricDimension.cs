using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A diameter dimension of a DXF file, as AutoCAD's <c>DiametricDimension</c>: the diameter of a circle or an arc, between
    /// two points opposite on it; a DIMENSION of kind 3.
    /// </summary>
    /// <remarks>
    /// The dimension of the circle of radius 15 about (220, 0, 0), measured at 30 degrees, has <see cref="ChordPoint"/>
    /// (207.01, -7.5, 0) (group 15), <see cref="FarChordPoint"/> (232.99, 7.5, 0) (group 10) and a
    /// <see cref="DxfDimension.Measurement"/> of 30. As every <see cref="DxfDimension"/>, it is drawn by its block, which is not
    /// drawn anew when a point is moved.
    /// </remarks>
    public sealed class DxfDiametricDimension : DxfDimension
    {
        // The groups of AcDbDiametricDimension in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 15, 25, 35, 40 });

        /// <summary>Initializes a diameter dimension of the origin, as AutoCAD's <c>new DiametricDimension()</c>, in the style Standard.</summary>
        public DxfDiametricDimension()
            : this(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), 0.0, string.Empty, null)
        {
        }

        /// <summary>
        /// Initializes a diameter dimension, as AutoCAD's <c>new DiametricDimension(chordPoint, farChordPoint, leaderLength,
        /// dimensionText, dimensionStyle)</c>, in no file until it is added to one; it needs its
        /// <see cref="DxfDimension.DimBlock"/> before it is.
        /// </summary>
        /// <param name="chordPoint">The point of the circle the leader starts at, in world coordinates.</param>
        /// <param name="farChordPoint">The point opposite it.</param>
        /// <param name="leaderLength">The length of the leader to the text.</param>
        /// <param name="dimensionText">The text, as <see cref="DxfDimension.DimensionText"/>; empty or null for the measurement.</param>
        /// <param name="dimensionStyle">The style it is drawn in, by its name; null for Standard.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is not a finite number.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfDiametricDimension(GeoPoint3 chordPoint, GeoPoint3 farChordPoint, double leaderLength, string dimensionText, DxfDimStyleTableRecord dimensionStyle)
            : base("DIMENSION", 3, farChordPoint, dimensionText, dimensionStyle, DxfRadialDimension.Groups("AcDbDiametricDimension", chordPoint, leaderLength))
        {
        }

        internal DxfDiametricDimension(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the point of the circle the leader starts at (groups 15, 25, 35), as AutoCAD's <c>ChordPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 ChordPoint { get => SubPoint(15); set => SetSubPoint(15, value); }

        /// <summary>Gets or sets the point opposite the chord point (groups 10, 20, 30), as AutoCAD's <c>FarChordPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 FarChordPoint { get => DefinitionPoint; set => SetDefinitionPoint(value); }

        /// <summary>Gets or sets the length of the leader to the text (group 40), as AutoCAD's <c>LeaderLength</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double LeaderLength { get => SubReal(40, 0.0); set => SetSubReal(40, value, LeaderLength); }

        /// <summary>Gets the diameter, between the two chord points, as AutoCAD's <c>Measurement</c>: 30 for a circle of radius 15.</summary>
        public override double Measurement => ChordPoint.DistanceTo(FarChordPoint);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> SubOrder => Order;
    }
}
