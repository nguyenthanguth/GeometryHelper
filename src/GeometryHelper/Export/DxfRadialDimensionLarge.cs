using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A jogged radius dimension of a DXF file, as AutoCAD's <c>RadialDimensionLarge</c>: the radius of a circle or an arc whose
    /// centre lies off the drawing, its line jogged to a centre drawn in its place; a LARGE_RADIAL_DIMENSION (R2004 on).
    /// </summary>
    /// <remarks>
    /// The dimension of the radius 25 about (300, -40, 0), at 45 degrees, has <see cref="Center"/> (group 10),
    /// <see cref="ChordPoint"/> (13), <see cref="OverrideCenter"/> (14), the centre drawn, <see cref="JogPoint"/> (15),
    /// <see cref="JogAngle"/> (40, in radians) and a <see cref="DxfDimension.Measurement"/> of 25. As every
    /// <see cref="DxfDimension"/>, it is drawn by its block, which is not drawn anew when a point is moved.
    /// </remarks>
    public sealed class DxfRadialDimensionLarge : DxfDimension
    {
        // The groups of AcDbRadialDimensionLarge in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 13, 23, 33, 14, 24, 34, 15, 25, 35, 40 });

        /// <summary>Initializes a jogged radius dimension at the origin, as AutoCAD's <c>new RadialDimensionLarge()</c>, its jog at 45 degrees, in the style Standard.</summary>
        public DxfRadialDimensionLarge()
            : this(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), Math.PI / 4.0, string.Empty, null)
        {
        }

        /// <summary>
        /// Initializes a jogged radius dimension, as AutoCAD's <c>new RadialDimensionLarge(center, chordPoint, overrideCenter,
        /// jogPoint, jogAngle, dimensionText, dimensionStyle)</c>, in no file until it is added to one; it needs its
        /// <see cref="DxfDimension.DimBlock"/> before it is.
        /// </summary>
        /// <param name="center">The centre of the circle or arc, in world coordinates.</param>
        /// <param name="chordPoint">The point of the circle or arc measured to.</param>
        /// <param name="overrideCenter">The centre drawn in its place.</param>
        /// <param name="jogPoint">Where the line jogs.</param>
        /// <param name="jogAngle">The angle of the jog, in radians.</param>
        /// <param name="dimensionText">The text, as <see cref="DxfDimension.DimensionText"/>; empty or null for the measurement.</param>
        /// <param name="dimensionStyle">The style it is drawn in, by its name; null for Standard.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is not a finite number.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfRadialDimensionLarge(GeoPoint3 center, GeoPoint3 chordPoint, GeoPoint3 overrideCenter, GeoPoint3 jogPoint, double jogAngle, string dimensionText, DxfDimStyleTableRecord dimensionStyle)
            : base("LARGE_RADIAL_DIMENSION", 9, center, dimensionText, dimensionStyle, Groups(chordPoint, overrideCenter, jogPoint, jogAngle))
        {
        }

        private DxfRadialDimensionLarge(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the centre of the circle or arc (groups 10, 20, 30), as AutoCAD's <c>Center</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 Center { get => DefinitionPoint; set => SetDefinitionPoint(value); }

        /// <summary>Gets or sets the point of the circle or arc measured to (groups 13, 23, 33), as AutoCAD's <c>ChordPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 ChordPoint { get => SubPoint(13); set => SetSubPoint(13, value); }

        /// <summary>Gets or sets the centre drawn in place of the true one (groups 14, 24, 34), as AutoCAD's <c>OverrideCenter</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 OverrideCenter { get => SubPoint(14); set => SetSubPoint(14, value); }

        /// <summary>Gets or sets where the line jogs (groups 15, 25, 35), as AutoCAD's <c>JogPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 JogPoint { get => SubPoint(15); set => SetSubPoint(15, value); }

        /// <summary>Gets or sets the angle of the jog (group 40 of AcDbRadialDimensionLarge, in radians as the file writes it), as AutoCAD's <c>JogAngle</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double JogAngle { get => SubReal(40, 0.0); set => SetSubReal(40, value, JogAngle); }

        /// <summary>Gets the radius, from the centre to the chord point, as AutoCAD's <c>Measurement</c>: 25 for a circle of radius 25.</summary>
        public override double Measurement => Center.DistanceTo(ChordPoint);

        [DxfType("LARGE_RADIAL_DIMENSION")]
        internal static new DxfEntity Create(DxfItem item) => new DxfRadialDimensionLarge(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> SubOrder => Order;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> PlanePoints => new int[0];

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 chord, GeoPoint3 overrideCenter, GeoPoint3 jog, double jogAngle)
        {
            Guard.Finite(jogAngle, nameof(jogAngle), "An angle has to be a finite number.");
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbRadialDimensionLarge") };
            groups.AddRange(PointGroups(13, chord));
            groups.AddRange(PointGroups(14, overrideCenter));
            groups.AddRange(PointGroups(15, jog));
            groups.Add(new DxfTypedValue(40, Numbers.Exact(jogAngle + 0.0)));
            return groups;
        }
    }
}
