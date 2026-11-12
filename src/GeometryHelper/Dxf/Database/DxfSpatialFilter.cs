using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The clip boundary of a block reference or an external reference, as AutoCAD's XCLIP keeps it: a SPATIAL_FILTER in the
    /// dictionary ACAD_FILTER of the reference's extension dictionary, under the name SPATIAL. Reached by
    /// <see cref="DxfBlockReference.SpatialFilter"/>; made by <see cref="DxfBlockReference.ClipTo"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The boundary is a list of points in the clip coordinate system, the user coordinate system that was current when it was
    /// made: two points are a rectangle's opposite corners, three or more a polygon. <see cref="WorldToClip"/> carries a point
    /// of the world into that system, and <see cref="InverseBlockTransform"/> a point of the world into the block's own
    /// coordinates, measured from its origin, as the reference stood when it was clipped. Both are kept with the boundary, and
    /// the reference moved, turned or scaled afterwards leaves them as they are, so that the clip goes with the content: a
    /// block 100 by 100 inserted at (1000, 500) and clipped to the rectangle (1020, 520) to (1100, 600) still shows its corner
    /// 20 by 20 in from its lower left, in whatever place it is moved to.
    /// </para>
    /// <para>
    /// A clip turned inside out (XCLIP Invert) is kept as AutoCAD keeps it: a polygon that goes round the outside of the block
    /// and in along a slit to the boundary and round that, so that what the polygon holds is the part cut away.
    /// </para>
    /// </remarks>
    public sealed class DxfSpatialFilter : DxfDBObject
    {
        private const string Marker = "AcDbSpatialFilter";

        internal DxfSpatialFilter(DxfItem item)
            : base(item) { }

        /// <summary>
        /// Gets the points of the boundary in the clip coordinate system (groups 10 and 20 of 70 of them): two for a rectangle,
        /// its opposite corners, three or more for a polygon.
        /// </summary>
        public IReadOnlyList<GeoPoint2> Points => Read().Points.AsReadOnly();

        /// <summary>Gets the direction the clip coordinate system looks along (group 210): its Z axis in the world.</summary>
        public GeoVector3 Normal => Read().Normal;

        /// <summary>Gets the origin of the clip coordinate system in the world (group 11).</summary>
        public GeoPoint3 Origin => Read().Origin;

        /// <summary>
        /// Gets or sets whether the clip is on (group 71), as XCLIP's ON and OFF: off, the block or reference is shown whole and the
        /// boundary kept.
        /// </summary>
        /// <value>Set, written into the record; a value it has changes nothing.</value>
        /// <exception cref="InvalidOperationException">Thrown when the filter is erased.</exception>
        public bool IsEnabled
        {
            get => Read().Enabled;
            set
            {
                State state = Read();

                if (state.Enabled != value)
                {
                    state.Enabled = value;
                    Write(state);
                }
            }
        }

        /// <summary>
        /// Gets or sets the distance of the front clipping plane from the boundary plane (groups 72 and 40), as XCLIP's Clipdepth
        /// front sets it; null where there is none.
        /// </summary>
        /// <value>Set, written into the record; null takes the plane away.</value>
        /// <exception cref="InvalidOperationException">Thrown when the filter is erased.</exception>
        public double? FrontClip
        {
            get => Read().Front;
            set
            {
                State state = Read();
                state.Front = value;
                Write(state);
            }
        }

        /// <summary>
        /// Gets or sets the distance of the back clipping plane from the boundary plane (groups 73 and 41), as XCLIP's Clipdepth back
        /// sets it; null where there is none.
        /// </summary>
        /// <value>Set, written into the record; null takes the plane away.</value>
        /// <exception cref="InvalidOperationException">Thrown when the filter is erased.</exception>
        public double? BackClip
        {
            get => Read().Back;
            set
            {
                State state = Read();
                state.Back = value;
                Write(state);
            }
        }

        /// <summary>
        /// Gets the 3 by 4 matrix, row by row, that carries a point of the world into the block's own coordinates measured from
        /// its origin, as the reference stood when it was clipped (the first twelve groups 40).
        /// </summary>
        public IReadOnlyList<double> InverseBlockTransform => Array.AsReadOnly(Read().Inverse);

        /// <summary>
        /// Gets the 3 by 4 matrix, row by row, that carries a point of the world into the clip coordinate system (the second
        /// twelve groups 40).
        /// </summary>
        public IReadOnlyList<double> WorldToClip => Array.AsReadOnly(Read().Clip);

        /// <summary>
        /// Gets the boundary as a ring of points in the block's own coordinates measured from its origin: its points taken out of the clip
        /// coordinate system into the world and from there into the block, a rectangle's two corners made the four of a ring. Fewer
        /// than three points give none.
        /// </summary>
        /// <returns>The ring, the last point not repeating the first; empty where the filter has fewer than two points.</returns>
        public IReadOnlyList<GeoPoint2> GetRing()
        {
            State state = Read();
            List<GeoPoint2> points = state.Points;

            if (points.Count == 2)
            {
                GeoPoint2 a = points[0];
                GeoPoint2 b = points[1];
                points = new List<GeoPoint2>
                {
                    new GeoPoint2(a.X, a.Y),
                    new GeoPoint2(b.X, a.Y),
                    new GeoPoint2(b.X, b.Y),
                    new GeoPoint2(a.X, b.Y),
                };
            }

            var ring = new List<GeoPoint2>(points.Count);

            if (points.Count < 3 || !TryInvert(state.Clip, out double[] fromClip))
            {
                return ring.AsReadOnly();
            }

            foreach (GeoPoint2 point in points)
            {
                double[] world = Apply(fromClip, point.X, point.Y, 0.0);
                double[] block = Apply(state.Inverse, world[0], world[1], world[2]);
                ring.Add(new GeoPoint2(block[0], block[1]));
            }

            return ring.AsReadOnly();
        }

        /// <summary>
        /// A point of the boundary in the block's own coordinates measured from its origin: the chain of <see cref="GetRing"/> for
        /// one point, which the presenter's grips on the boundary draw and drag by.
        /// </summary>
        internal GeoPoint2 ToBlock(GeoPoint2 point)
        {
            State state = Read();

            if (!TryInvert(state.Clip, out double[] fromClip))
            {
                return point;
            }

            double[] world = Apply(fromClip, point.X, point.Y, 0.0);
            double[] block = Apply(state.Inverse, world[0], world[1], world[2]);
            return new GeoPoint2(block[0], block[1]);
        }

        /// <summary>
        /// The point of the clip coordinate system whose <see cref="ToBlock"/> is a given point of the block's own plane, as far
        /// as the boundary's plane is the block's: the way back, which a vertex dragged is put by.
        /// </summary>
        /// <returns>false where the matrices of the filter flatten space.</returns>
        internal bool TryFromBlock(GeoPoint2 block, out GeoPoint2 clip)
        {
            State state = Read();
            clip = default;

            if (!TryInvert(state.Inverse, out double[] toWorld))
            {
                return false;
            }

            double[] world = Apply(toWorld, block.X, block.Y, 0.0);
            double[] inside = Apply(state.Clip, world[0], world[1], world[2]);
            clip = new GeoPoint2(inside[0], inside[1]);
            return true;
        }

        /// <summary>Writes the points of the boundary, the rest of the filter as it is: how a vertex of the boundary is dragged.</summary>
        internal void SetPoints(IReadOnlyList<GeoPoint2> points)
        {
            State state = Read();
            state.Points = new List<GeoPoint2>(points);
            Write(state);
        }

        /// <summary>
        /// Erases the filter, as AutoCAD's <c>Erase</c>: it is taken out of the file, with the entry of the dictionary that names
        /// it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the filter is in no file, or is erased.</exception>
        public override void Erase() => DxfDBDictionary.EraseFromOwner(this, InFile("be erased"));

        /// <summary>Gives the filter as a line for debugging: its type, its handle and how many points its boundary has.</summary>
        /// <returns>The line, such as <c>SPATIAL_FILTER 2F, 4 points</c>.</returns>
        public override string ToString() =>
            "SPATIAL_FILTER "
            + (Handle ?? string.Empty)
            + ", "
            + Read().Points.Count.ToString(CultureInfo.InvariantCulture)
            + " points";

        /// <summary>What a filter holds: the groups after its subclass marker read, or to be written.</summary>
        internal sealed class State
        {
            public List<GeoPoint2> Points = new List<GeoPoint2>();
            public GeoVector3 Normal = new GeoVector3(0.0, 0.0, 1.0);
            public GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);
            public bool Enabled = true;
            public double? Front;
            public double? Back;
            public double[] Inverse = Identity();
            public double[] Clip = Identity();

            /// <summary>The groups, in the order AutoCAD writes them.</summary>
            public List<KeyValuePair<int, string>> Groups()
            {
                var groups = new List<KeyValuePair<int, string>>
                {
                    Pair(70, Points.Count.ToString(CultureInfo.InvariantCulture)),
                };

                foreach (GeoPoint2 point in Points)
                {
                    groups.Add(Pair(10, Real(point.X)));
                    groups.Add(Pair(20, Real(point.Y)));
                }

                groups.Add(Pair(210, Real(Normal.X)));
                groups.Add(Pair(220, Real(Normal.Y)));
                groups.Add(Pair(230, Real(Normal.Z)));
                groups.Add(Pair(11, Real(Origin.X)));
                groups.Add(Pair(21, Real(Origin.Y)));
                groups.Add(Pair(31, Real(Origin.Z)));
                groups.Add(Pair(71, Enabled ? "1" : "0"));
                groups.Add(Pair(72, Front.HasValue ? "1" : "0"));

                if (Front.HasValue)
                {
                    groups.Add(Pair(40, Real(Front.Value)));
                }

                groups.Add(Pair(73, Back.HasValue ? "1" : "0"));

                if (Back.HasValue)
                {
                    groups.Add(Pair(41, Real(Back.Value)));
                }

                foreach (double v in Inverse)
                {
                    groups.Add(Pair(40, Real(v)));
                }

                foreach (double v in Clip)
                {
                    groups.Add(Pair(40, Real(v)));
                }

                return groups;
            }
        }

        /// <summary>The identity as a 3 by 4 matrix, row by row.</summary>
        internal static double[] Identity() => new[] { 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0 };

        /// <summary>A point carried by a 3 by 4 matrix, row by row.</summary>
        internal static double[] Apply(double[] m, double x, double y, double z) =>
            new[]
            {
                m[0] * x + m[1] * y + m[2] * z + m[3],
                m[4] * x + m[5] * y + m[6] * z + m[7],
                m[8] * x + m[9] * y + m[10] * z + m[11],
            };

        /// <summary>The inverse of a 3 by 4 matrix that stands for a transformation of space; false where it flattens.</summary>
        internal static bool TryInvert(double[] m, out double[] inverse)
        {
            double a = m[0],
                b = m[1],
                c = m[2],
                d = m[4],
                e = m[5],
                f = m[6],
                g = m[8],
                h = m[9],
                i = m[10];
            double det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
            inverse = null;

            if (Math.Abs(det) < 1E-300 || double.IsNaN(det) || double.IsInfinity(det))
            {
                return false;
            }

            double[] r =
            {
                (e * i - f * h) / det,
                (c * h - b * i) / det,
                (b * f - c * e) / det,
                0.0,
                (f * g - d * i) / det,
                (a * i - c * g) / det,
                (c * d - a * f) / det,
                0.0,
                (d * h - e * g) / det,
                (b * g - a * h) / det,
                (a * e - b * d) / det,
                0.0,
            };

            for (int row = 0; row < 3; row++)
            {
                r[row * 4 + 3] = -(r[row * 4] * m[3] + r[row * 4 + 1] * m[7] + r[row * 4 + 2] * m[11]);
            }

            inverse = r;
            return true;
        }

        /// <summary>The 3 by 4 matrix of a transformation, row by row.</summary>
        internal static double[] Rows(GeoTransform3 transform)
        {
            var m = new double[12];

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    m[row * 4 + column] = transform[row, column];
                }
            }

            return m;
        }

        internal static string Real(double value) => Numbers.Exact(value + 0.0);

        private static KeyValuePair<int, string> Pair(int code, string value) =>
            new KeyValuePair<int, string>(code, value);

        /// <summary>Where the groups after the subclass marker AcDbSpatialFilter begin, as <see cref="DxfFile.TailOf"/> counts them.</summary>
        private int Start()
        {
            IReadOnlyList<DxfTypedValue> groups = Item.File.GroupsOf(Item);
            bool braced = false;

            for (int i = 0; i < groups.Count; i++)
            {
                DxfTypedValue group = groups[i];

                if (group.TypeCode == 102)
                {
                    braced = group.Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && group.TypeCode == 100 && group.Value.Trim() == Marker)
                {
                    return i + 1;
                }
                else if (group.TypeCode == 1001)
                {
                    return i;
                }
            }

            return groups.Count;
        }

        /// <summary>The record read: the points, the normal and origin, the flags, the planes and the two matrices.</summary>
        private State Read()
        {
            var state = new State();

            if (Item == null)
            {
                return state;
            }

            List<DxfTypedValue> groups = Item.File.TailOf(Item, Start());
            var matrices = new List<double>();
            bool front = false;
            bool back = false;
            bool wantFront = false;
            bool wantBack = false;
            double x = 0.0;
            var points = new List<GeoPoint2>();
            double[] normal = { 0.0, 0.0, 1.0 };
            double[] origin = { 0.0, 0.0, 0.0 };

            foreach (DxfTypedValue group in groups)
            {
                double v = Number(group.Value);

                switch (group.TypeCode)
                {
                    case 10:
                        x = v;
                        break;
                    case 20:
                        points.Add(new GeoPoint2(x, v));
                        break;
                    case 210:
                        normal[0] = v;
                        break;
                    case 220:
                        normal[1] = v;
                        break;
                    case 230:
                        normal[2] = v;
                        break;
                    case 11:
                        origin[0] = v;
                        break;
                    case 21:
                        origin[1] = v;
                        break;
                    case 31:
                        origin[2] = v;
                        break;
                    case 71:
                        state.Enabled = v != 0.0;
                        break;
                    case 72:
                        front = v != 0.0;
                        wantFront = front;
                        break;
                    case 73:
                        back = v != 0.0;
                        wantBack = back;
                        break;
                    case 41:
                        if (wantBack)
                        {
                            state.Back = v;
                            wantBack = false;
                        }

                        break;
                    case 40:
                        if (wantFront)
                        {
                            state.Front = v;
                            wantFront = false;
                        }
                        else if (back && wantBack)
                        {
                            // A writer that gives the back distance as a 40, as the front one is.
                            state.Back = v;
                            wantBack = false;
                        }
                        else
                        {
                            matrices.Add(v);
                        }

                        break;
                }
            }

            state.Points = points;
            state.Normal = new GeoVector3(normal[0], normal[1], normal[2]);
            state.Origin = new GeoPoint3(origin[0], origin[1], origin[2]);

            if (matrices.Count >= 24)
            {
                state.Inverse = matrices.GetRange(0, 12).ToArray();
                state.Clip = matrices.GetRange(12, 12).ToArray();
            }

            return state;
        }

        private static double Number(string text) =>
            double.TryParse(
                text == null ? string.Empty : text.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value
            )
                ? value
                : 0.0;

        /// <summary>Writes a state into the record, after its subclass marker.</summary>
        private void Write(State state)
        {
            DxfFile file = InFile("be changed");
            file.ReplaceTail(Item, Start(), state.Groups());
        }
    }
}
