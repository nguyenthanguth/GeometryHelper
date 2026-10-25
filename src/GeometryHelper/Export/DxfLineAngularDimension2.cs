using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An angular dimension of two lines of a DXF file, as AutoCAD's <c>LineAngularDimension2</c>: the angle between two lines,
    /// on the side of the arc point; a DIMENSION of kind 2.
    /// </summary>
    /// <remarks>
    /// The dimension of the line from (0, 100, 0) to (50, 100, 0) and that from (0, 100, 0) to (40, 140, 0), its arc through
    /// (40, 120, 0), has <see cref="XLine1Start"/> and <see cref="XLine1End"/> (groups 13, 14), <see cref="XLine2Start"/> and
    /// <see cref="XLine2End"/> (15, 10), <see cref="ArcPoint"/> (16, written in the system of the normal) and a
    /// <see cref="DxfDimension.Measurement"/> of pi / 4: of the four angles the two lines make where they meet, the one the arc
    /// point lies in. As every <see cref="DxfDimension"/>, it is drawn by its block, which is not drawn anew when a point is moved.
    /// </remarks>
    public sealed class DxfLineAngularDimension2 : DxfDimension
    {
        // The groups of AcDb2LineAngularDimension in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 13, 23, 33, 14, 24, 34, 15, 25, 35, 16, 26, 36 });

        /// <summary>Initializes an angular dimension of two lines of no length at the origin, as AutoCAD's <c>new LineAngularDimension2()</c>, in the style Standard.</summary>
        public DxfLineAngularDimension2()
            : this(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(0.0, 0.0, 0.0), string.Empty, null)
        {
        }

        /// <summary>
        /// Initializes an angular dimension of two lines, as AutoCAD's <c>new LineAngularDimension2(line1Start, line1End,
        /// line2Start, line2End, arcPoint, dimensionText, dimensionStyle)</c>, in no file until it is added to one; it needs its
        /// <see cref="DxfDimension.DimBlock"/> before it is.
        /// </summary>
        /// <param name="line1Start">The start of the first line, in world coordinates.</param>
        /// <param name="line1End">Its end.</param>
        /// <param name="line2Start">The start of the second line.</param>
        /// <param name="line2End">Its end.</param>
        /// <param name="arcPoint">A point of the dimension's arc, in world coordinates.</param>
        /// <param name="dimensionText">The text, as <see cref="DxfDimension.DimensionText"/>; empty or null for the measurement.</param>
        /// <param name="dimensionStyle">The style it is drawn in, by its name; null for Standard.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfLineAngularDimension2(GeoPoint3 line1Start, GeoPoint3 line1End, GeoPoint3 line2Start, GeoPoint3 line2End, GeoPoint3 arcPoint, string dimensionText, DxfDimStyleTableRecord dimensionStyle)
            : base("DIMENSION", 2, line2End, dimensionText, dimensionStyle, Groups(line1Start, line1End, line2Start, arcPoint))
        {
        }

        internal DxfLineAngularDimension2(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the start of the first line (groups 13, 23, 33), as AutoCAD's <c>XLine1Start</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine1Start { get => SubPoint(13); set => SetSubPoint(13, value); }

        /// <summary>Gets or sets the end of the first line (groups 14, 24, 34), as AutoCAD's <c>XLine1End</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine1End { get => SubPoint(14); set => SetSubPoint(14, value); }

        /// <summary>Gets or sets the start of the second line (groups 15, 25, 35), as AutoCAD's <c>XLine2Start</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine2Start { get => SubPoint(15); set => SetSubPoint(15, value); }

        /// <summary>Gets or sets the end of the second line (groups 10, 20, 30), as AutoCAD's <c>XLine2End</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 XLine2End { get => DefinitionPoint; set => SetDefinitionPoint(value); }

        /// <summary>Gets or sets a point of the dimension's arc (groups 16, 26, 36, written in the system of the normal), as AutoCAD's <c>ArcPoint</c>, in world coordinates.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 ArcPoint { get => SubPlanePoint(16); set => SetSubPlanePoint(16, value); }

        /// <summary>
        /// Gets the angle measured, as AutoCAD's <c>Measurement</c>: in radians, of the four the two lines make where they meet,
        /// seen in the plane of the normal, the one the arc point lies in; pi / 4 for a level line and one at 45 degrees with the
        /// arc point between them. Lines that never meet give the angle from the first to the second on the arc point's side.
        /// </summary>
        public override double Measurement
        {
            get
            {
                GeoVector3 first = XLine1Start.GetVectorTo(XLine1End);
                GeoVector3 second = XLine2Start.GetVectorTo(XLine2End);
                GeoPoint3 meet = Meeting(XLine1Start, first, XLine2Start, second, out bool found);

                if (!found)
                {
                    return Sweep(first, second, XLine1Start.GetVectorTo(ArcPoint));
                }

                double at = AngleOf(meet.GetVectorTo(ArcPoint));
                var rays = new List<double> { AngleOf(first), AngleOf(first.Negate()), AngleOf(second), AngleOf(second.Negate()) };
                rays.Sort();

                for (int i = 0; i < 4; i++)
                {
                    double from = rays[i];
                    double to = i == 3 ? rays[0] + 2.0 * Math.PI : rays[i + 1];
                    double point = at < from ? at + 2.0 * Math.PI : at;

                    if (point >= from && point <= to)
                    {
                        return to - from;
                    }
                }

                return rays[1] - rays[0];
            }
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> SubOrder => Order;

        /// <summary>Where two lines meet, seen in the plane of the normal; false where they run alike.</summary>
        private GeoPoint3 Meeting(GeoPoint3 a, GeoVector3 da, GeoPoint3 b, GeoVector3 db, out bool found)
        {
            OcsAxes(Normal, out GeoVector3 u, out GeoVector3 v);
            double ax = da.DotProduct(u), ay = da.DotProduct(v), bx = db.DotProduct(u), by = db.DotProduct(v);
            double cross = ax * by - ay * bx;
            double scale = Math.Sqrt((ax * ax + ay * ay) * (bx * bx + by * by));
            found = scale > 0.0 && Math.Abs(cross) > 1E-12 * scale;

            if (!found)
            {
                return a;
            }

            GeoVector3 ab = a.GetVectorTo(b);
            double t = (ab.DotProduct(u) * by - ab.DotProduct(v) * bx) / cross;
            return a.Add(da.Multiply(t));
        }

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 line1Start, GeoPoint3 line1End, GeoPoint3 line2Start, GeoPoint3 arcPoint)
        {
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDb2LineAngularDimension") };
            groups.AddRange(PointGroups(13, line1Start));
            groups.AddRange(PointGroups(14, line1End));
            groups.AddRange(PointGroups(15, line2Start));
            groups.AddRange(PointGroups(16, arcPoint));
            return groups;
        }
    }
}
