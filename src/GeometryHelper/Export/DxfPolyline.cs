using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A LWPOLYLINE of a DXF file, as AutoCAD's <c>Polyline</c>: a chain of vertices in the plane of its <see cref="Normal"/>
    /// at its <see cref="Elevation"/>, each segment a line or, where the vertex it leaves has a bulge, an arc, with a width at
    /// each end of each segment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The vertices are in the system of the polyline's normal, as AutoCAD's <see cref="GetPoint2dAt(int)"/> gives them;
    /// <see cref="GetPoint3dAt(int)"/> gives them in the world. The polyline (0, 0), (10, 0), (10, 10) with a bulge of 1 on its
    /// second vertex is a line 10 long and a half circle of radius 5, <see cref="Length"/> 25.71; its parameter is AutoCAD's,
    /// the index of the vertex a segment leaves plus how far along it, in angle on an arc: 1.5 is (15, 5), the far side of the
    /// half circle.
    /// </para>
    /// <para>
    /// A vertex set is written in its groups 10 and 20 (40 and 41 for its widths, 42 for its bulge) as <see cref="DxfWriter"/>
    /// writes a number, the other vertices and every other group of the record kept as they were. A file older than R14 has no
    /// LWPOLYLINE: <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> refuses one there, and a
    /// <see cref="DxfPolyline2d"/> is added instead.
    /// </para>
    /// </remarks>
    public sealed class DxfPolyline : DxfCurve
    {
        // The groups of AcDbPolyline in the order AutoCAD writes them, a vertex being 10, 20, 91, 40, 41 and 42.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 90, 70, 43, 38, 39, 10, 20, 91, 40, 41, 42, 210, 220, 230 });

        // The groups of a vertex, and their order in it.
        private static readonly int[] VertexOrder = { 10, 20, 91, 40, 41, 42 };

        private static readonly IReadOnlyCollection<int> VertexCodes = Array.AsReadOnly(VertexOrder);

        /// <summary>
        /// Initializes a polyline of no vertices, as AutoCAD's <c>new Polyline()</c>, on layer 0 in no file; give it its vertices
        /// with <see cref="AddVertexAt(int, GeoPoint2, double, double, double)"/>.
        /// </summary>
        public DxfPolyline()
            : base("LWPOLYLINE", new[] { new DxfTypedValue(100, "AcDbPolyline"), new DxfTypedValue(90, "0"), new DxfTypedValue(70, "0") })
        {
        }

        /// <summary>
        /// Initializes a polyline of no vertices, as AutoCAD's <c>new Polyline(vertices)</c>, which only makes room for that many.
        /// </summary>
        /// <param name="vertices">How many vertices it is to have, nought or more; none are added.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is below nought.</exception>
        public DxfPolyline(int vertices)
            : this()
        {
            if (vertices < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vertices), vertices, "A polyline makes room for nought vertices or more.");
            }
        }

        private DxfPolyline(DxfItem item)
            : base(item)
        {
        }

        #region The vertices

        /// <summary>Gets how many vertices the polyline has, as AutoCAD's <c>NumberOfVertices</c>: its groups 10.</summary>
        public int NumberOfVertices => Vertices().Count;

        /// <summary>
        /// Gets a vertex in the system of the polyline's normal (groups 10 and 20), as AutoCAD's <c>GetPoint2dAt</c>.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The vertex.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public GeoPoint2 GetPoint2dAt(int index)
        {
            List<KeyValuePair<int, string>> vertex = VertexAt(Vertices(), index);
            return new GeoPoint2(Value(vertex, 10, 0.0), Value(vertex, 20, 0.0));
        }

        /// <summary>
        /// Gets a vertex in the world, as AutoCAD's <c>GetPoint3dAt</c>: the vertex at the polyline's elevation, taken from the
        /// system of its normal to the world as <see cref="DxfReader"/> takes it.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The vertex.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public GeoPoint3 GetPoint3dAt(int index)
        {
            GeoPoint2 point = GetPoint2dAt(index);
            return OcsToWorld(Normal, new GeoPoint3(point.X, point.Y, Elevation));
        }

        /// <summary>Moves a vertex, given in the system of the polyline's normal, as AutoCAD's <c>SetPointAt</c>.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <param name="pt">Where it is to be.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public void SetPointAt(int index, GeoPoint2 pt)
        {
            if (!pt.IsValid)
            {
                throw new ArgumentException("A vertex has to be of finite numbers: " + pt + " is not.", nameof(pt));
            }

            List<List<KeyValuePair<int, string>>> vertices = Vertices();
            List<KeyValuePair<int, string>> vertex = VertexAt(vertices, index);
            bool changed = Put(vertex, 10, pt.X, 0.0, false) | Put(vertex, 20, pt.Y, 0.0, false);

            if (changed)
            {
                Write(vertices);
            }
        }

        /// <summary>Gets the bulge of the segment that leaves a vertex (group 42), as AutoCAD's <c>GetBulgeAt</c>: nought for a line.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The tangent of a quarter of the angle the arc sweeps, above nought counter-clockwise; nought where the vertex has none.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public double GetBulgeAt(int index) => Value(VertexAt(Vertices(), index), 42, 0.0);

        /// <summary>Sets the bulge of the segment that leaves a vertex, as AutoCAD's <c>SetBulgeAt</c>: 1 is a half circle to the left.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <param name="bulge">The bulge, a finite number; nought takes the group out.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index, or the bulge is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public void SetBulgeAt(int index, double bulge) => SetVertexNumber(index, 42, bulge, nameof(bulge));

        /// <summary>
        /// Gets the width of the polyline where the segment that leaves a vertex starts (group 40), as AutoCAD's
        /// <c>GetStartWidthAt</c>: the polyline's constant width (43) where the vertex gives none.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The width.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public double GetStartWidthAt(int index) => Value(VertexAt(Vertices(), index), 40, GetDouble(43, 0.0));

        /// <summary>
        /// Gets the width of the polyline where the segment that leaves a vertex ends (group 41), as AutoCAD's
        /// <c>GetEndWidthAt</c>: the polyline's constant width (43) where the vertex gives none.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The width.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public double GetEndWidthAt(int index) => Value(VertexAt(Vertices(), index), 41, GetDouble(43, 0.0));

        /// <summary>
        /// Sets the width where the segment that leaves a vertex starts, as AutoCAD's <c>SetStartWidthAt</c>. A constant width
        /// the polyline had is given to every vertex first, as the widths are no longer one.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <param name="startWidth">The width, nought or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index, or the width is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public void SetStartWidthAt(int index, double startWidth) => SetWidth(index, 40, startWidth, nameof(startWidth));

        /// <summary>Sets the width where the segment that leaves a vertex ends, as AutoCAD's <c>SetEndWidthAt</c>.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <param name="endWidth">The width, nought or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index, or the width is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public void SetEndWidthAt(int index, double endWidth) => SetWidth(index, 41, endWidth, nameof(endWidth));

        /// <summary>
        /// Adds a vertex, as AutoCAD's <c>AddVertexAt</c>: before the vertex of the index, or after the last where the index is
        /// the count. Its widths are written where either is not nought, its bulge where it is not.
        /// </summary>
        /// <param name="index">Where it goes, from nought to <see cref="NumberOfVertices"/>.</param>
        /// <param name="pt">The vertex, in the system of the polyline's normal.</param>
        /// <param name="bulge">The bulge of the segment that leaves it.</param>
        /// <param name="startWidth">The width where that segment starts.</param>
        /// <param name="endWidth">The width where it ends.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or above the count, or a number is not finite or a width below nought.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public void AddVertexAt(int index, GeoPoint2 pt, double bulge, double startWidth, double endWidth)
        {
            if (!pt.IsValid)
            {
                throw new ArgumentException("A vertex has to be of finite numbers: " + pt + " is not.", nameof(pt));
            }

            Guard.Finite(bulge, nameof(bulge), "A bulge has to be a finite number.");
            Guard.NonNegative(startWidth, nameof(startWidth), "A width has to be a finite number, nought or more.");
            Guard.NonNegative(endWidth, nameof(endWidth), "A width has to be a finite number, nought or more.");
            List<List<KeyValuePair<int, string>>> vertices = Vertices();

            if (index < 0 || index > vertices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A vertex goes in from nought to the count of the vertices, " + vertices.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            var vertex = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(10, Numbers.Exact(pt.X + 0.0)),
                new KeyValuePair<int, string>(20, Numbers.Exact(pt.Y + 0.0)),
            };

            if (vertices.Count > 0 && Text(vertices[0], 91) != null)
            {
                vertex.Add(new KeyValuePair<int, string>(91, "0"));
            }

            if (startWidth != 0.0 || endWidth != 0.0)
            {
                vertex.Add(new KeyValuePair<int, string>(40, Numbers.Exact(startWidth + 0.0)));
                vertex.Add(new KeyValuePair<int, string>(41, Numbers.Exact(endWidth + 0.0)));
            }

            if (bulge != 0.0)
            {
                vertex.Add(new KeyValuePair<int, string>(42, Numbers.Exact(bulge + 0.0)));
            }

            vertices.Insert(index, vertex);
            Write(vertices);
        }

        /// <summary>
        /// Takes a vertex out, as AutoCAD's <c>RemoveVertexAt</c>. A polyline keeps two vertices at least, as AutoCAD keeps one
        /// on save and audit: the vertex is not taken out where fewer would be left.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has two vertices or fewer, or is erased.</exception>
        public void RemoveVertexAt(int index)
        {
            List<List<KeyValuePair<int, string>>> vertices = Vertices();
            VertexAt(vertices, index);

            if (vertices.Count <= 2)
            {
                throw new InvalidOperationException("A polyline keeps two vertices at least, and this one has " + vertices.Count.ToString(CultureInfo.InvariantCulture) + ": erase the polyline instead.");
            }

            vertices.RemoveAt(index);
            Write(vertices);
        }

        /// <summary>
        /// Keeps the first vertices and takes the others out, as AutoCAD's <c>Reset</c>: where the vertices are reused, as many
        /// as given are kept. A polyline keeps two vertices at least, so a reset that would leave fewer is refused.
        /// </summary>
        /// <param name="reuse">Whether the first vertices are kept; where they are not, none would be, which is refused.</param>
        /// <param name="vertices">How many are kept where they are reused.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when fewer than two vertices would be left, or the polyline is erased.</exception>
        public void Reset(bool reuse, int vertices)
        {
            if (vertices < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vertices), vertices, "A count of vertices is nought or more.");
            }

            List<List<KeyValuePair<int, string>>> all = Vertices();
            int keep = reuse ? Math.Min(vertices, all.Count) : 0;

            if (keep < all.Count)
            {
                if (keep < 2)
                {
                    throw new InvalidOperationException("A polyline keeps two vertices at least, and the reset would leave " + keep.ToString(CultureInfo.InvariantCulture) + ".");
                }

                Write(all.GetRange(0, keep));
            }
        }

        #endregion

        #region The polyline

        /// <summary>
        /// Gets or sets whether a segment runs from the last vertex back to the first (flag 1 of group 70), as AutoCAD's
        /// <c>Closed</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public new bool Closed
        {
            get => (GetInt(70, 0) & 1) != 0;
            set => SetFlag(1, value);
        }

        /// <summary>
        /// Gets or sets whether the linetype runs on round the vertices rather than starting again at each (flag 128 of group
        /// 70), as AutoCAD's <c>Plinegen</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public bool Plinegen
        {
            get => (GetInt(70, 0) & 128) != 0;
            set => SetFlag(128, value);
        }

        /// <summary>
        /// Gets or sets the width of the whole polyline, as AutoCAD's <c>ConstantWidth</c>: the width every segment has at both
        /// ends, a vertex with no width of its own taking the constant width of group 43; nought where the widths differ.
        /// </summary>
        /// <value>Set, nought or more: written in group 43, every width of a vertex taken out.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public double ConstantWidth
        {
            get
            {
                double constant = GetDouble(43, 0.0);
                List<List<KeyValuePair<int, string>>> vertices = Vertices();

                if (vertices.Count == 0)
                {
                    return constant;
                }

                double width = Value(vertices[0], 40, constant);

                foreach (List<KeyValuePair<int, string>> vertex in vertices)
                {
                    if (!Same(Value(vertex, 40, constant), width) || !Same(Value(vertex, 41, constant), width))
                    {
                        return 0.0;
                    }
                }

                return width;
            }

            set
            {
                Guard.NonNegative(value, nameof(value), "A width has to be a finite number, nought or more.");

                if (Same(value, ConstantWidth))
                {
                    return;
                }

                List<List<KeyValuePair<int, string>>> vertices = Vertices();

                foreach (List<KeyValuePair<int, string>> vertex in vertices)
                {
                    vertex.RemoveAll(g => g.Key == 40 || g.Key == 41);
                }

                Write(vertices);
                SetDouble(43, value, 0.0);
            }
        }

        /// <summary>
        /// Gets or sets how far the plane of the polyline stands from the origin along its normal (group 38), as AutoCAD's
        /// <c>Elevation</c>; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the elevation set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public double Elevation
        {
            get => GetDouble(38, 0.0);
            set => SetDouble(38, value, 0.0);
        }

        /// <summary>
        /// Gets or sets the normal of the plane of the polyline (groups 210, 220, 230), of unit length; (0, 0, 1) where the record
        /// gives none.
        /// </summary>
        /// <value>
        /// Set, a direction of some length, written made of unit length. The vertices keep their coordinates in the plane of the
        /// normal, as AutoCAD's polyline keeps them, so the polyline turns with the plane and moves in the world: the vertex
        /// (10, 0) given the normal (0, 0, -1) is at (-10, 0, 0) in the world. A <see cref="DxfCircle"/>, <see cref="DxfArc"/>,
        /// <see cref="DxfDBText"/> or <see cref="DxfBlockReference"/> keeps its place in the world instead, and a
        /// <see cref="DxfSolid"/> its corners.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();
            set => SetNormal(value);
        }

        /// <summary>Gets or sets how far the polyline stands up along its <see cref="Normal"/> (group 39); nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public double Thickness
        {
            get => GetThickness();
            set => SetThickness(value);
        }

        /// <summary>Gets whether a vertex has a bulge other than nought, as AutoCAD's <c>HasBulges</c>.</summary>
        public bool HasBulges
        {
            get
            {
                foreach (List<KeyValuePair<int, string>> vertex in Vertices())
                {
                    if (Value(vertex, 42, 0.0) != 0.0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Gets whether the polyline has a width other than nought anywhere, as AutoCAD's <c>HasWidth</c>: a vertex's, or the constant width.</summary>
        public bool HasWidth
        {
            get
            {
                double constant = GetDouble(43, 0.0);
                List<List<KeyValuePair<int, string>>> vertices = Vertices();

                if (vertices.Count == 0)
                {
                    return constant != 0.0;
                }

                foreach (List<KeyValuePair<int, string>> vertex in vertices)
                {
                    if (Value(vertex, 40, constant) != 0.0 || Value(vertex, 41, constant) != 0.0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Gets whether every segment is a line, none an arc, as AutoCAD's <c>IsOnlyLines</c>.</summary>
        public bool IsOnlyLines
        {
            get
            {
                DxfBulgeChain chain = Chain();

                for (int i = 0; i < chain.SegmentCount; i++)
                {
                    if (chain.IsArc(i))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Gets the length of the polyline, the sum of those of its segments, as AutoCAD's <c>Length</c>.</summary>
        public double Length => Chain().Length;

        #endregion

        #region The segments

        /// <summary>
        /// Gets what the segment that leaves a vertex is, as AutoCAD's <c>GetSegmentType</c>: a line, an arc, one whose ends are
        /// the same point within the point tolerance, the one vertex of a polyline that has no other, or none for the last vertex
        /// of an open polyline.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>What the segment is.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public DxfSegmentType GetSegmentType(int index)
        {
            List<List<KeyValuePair<int, string>>> vertices = Vertices();
            VertexAt(vertices, index);
            int n = vertices.Count;

            if (n == 1)
            {
                return DxfSegmentType.Point;
            }

            if (index == n - 1 && !Closed)
            {
                return DxfSegmentType.Empty;
            }

            GeoPoint2 start = GetPoint2dAt(index);
            GeoPoint2 end = GetPoint2dAt((index + 1) % n);

            if (start.IsEqualTo(end, Tolerance.Default))
            {
                return DxfSegmentType.Coincident;
            }

            return Value(vertices[index], 42, 0.0) != 0.0 ? DxfSegmentType.Arc : DxfSegmentType.Line;
        }

        /// <summary>Gets the line segment that leaves a vertex in the world, as AutoCAD's <c>GetLineSegmentAt</c>.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The segment, from the vertex to the next.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the segment is not a line.</exception>
        public GeoLine3 GetLineSegmentAt(int index)
        {
            Segment(index, DxfSegmentType.Line);
            return new GeoLine3(GetPoint3dAt(index), GetPoint3dAt((index + 1) % NumberOfVertices));
        }

        /// <summary>Gets the line segment that leaves a vertex in the system of the polyline's normal, as AutoCAD's <c>GetLineSegment2dAt</c>.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The segment, from the vertex to the next.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the segment is not a line.</exception>
        public GeoLine2 GetLineSegment2dAt(int index)
        {
            Segment(index, DxfSegmentType.Line);
            return new GeoLine2(GetPoint2dAt(index), GetPoint2dAt((index + 1) % NumberOfVertices));
        }

        /// <summary>Gets the arc that leaves a vertex in the world, as AutoCAD's <c>GetArcSegmentAt</c>, as the reader reads one.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The arc, from the vertex to the next.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the segment is not an arc.</exception>
        public GeoArc3 GetArcSegmentAt(int index)
        {
            Segment(index, DxfSegmentType.Arc);
            return new GeoEdge3(GetPoint3dAt(index), GetPoint3dAt((index + 1) % NumberOfVertices), GetBulgeAt(index), Normal, Tolerance.Default).ToArc();
        }

        /// <summary>Gets the arc that leaves a vertex in the system of the polyline's normal, as AutoCAD's <c>GetArcSegment2dAt</c>.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The arc, from the vertex to the next.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the segment is not an arc.</exception>
        public GeoArc2 GetArcSegment2dAt(int index)
        {
            Segment(index, DxfSegmentType.Arc);
            return GeoArc2.FromBulge(GetPoint2dAt(index), GetPoint2dAt((index + 1) % NumberOfVertices), GetBulgeAt(index));
        }

        /// <summary>
        /// Gets whether a point of the plane lies on the segment that leaves a vertex, within the point tolerance, as AutoCAD's
        /// <c>OnSegmentAt</c>.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <param name="pt2d">The point, in the system of the polyline's normal.</param>
        /// <param name="value">Not read: AutoCAD's parameter of the point on the segment, which it does not give back either.</param>
        /// <returns>Whether the point is on the segment; false where the vertex leaves none.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public bool OnSegmentAt(int index, GeoPoint2 pt2d, double value)
        {
            DxfSegmentType type = GetSegmentType(index);

            if (type == DxfSegmentType.Empty)
            {
                return false;
            }

            GeoPoint2 start = GetPoint2dAt(index);
            GeoPoint2 end = GetPoint2dAt((index + 1) % NumberOfVertices);

            switch (type)
            {
                case DxfSegmentType.Line: return new GeoLine2(start, end).GetClosestPointOnBoundary(pt2d).IsEqualTo(pt2d, Tolerance.Default);
                case DxfSegmentType.Arc: return GeoArc2.FromBulge(start, end, GetBulgeAt(index)).GetClosestPointOnBoundary(pt2d).IsEqualTo(pt2d, Tolerance.Default);
                default: return start.IsEqualTo(pt2d, Tolerance.Default);
            }
        }

        #endregion

        #region The curve

        /// <summary>Gets the parameter at the end of the polyline: the count of its segments, as AutoCAD's.</summary>
        public override double EndParam => Chain().EndParam;

        /// <summary>
        /// Gets the area the polyline closes, as AutoCAD's <c>Area</c>: that of the polygon of its vertices with what each arc
        /// adds or takes off; an open polyline taken as closed by a line from its last vertex to its first.
        /// </summary>
        public override double Area => Chain().Area;

        /// <summary>Gets the point at a parameter, the index of a vertex plus how far along the segment it leaves, as AutoCAD's <c>GetPointAtParameter</c>.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The point, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.PointAt(value);
        }

        /// <summary>Gets the parameter at a point of the polyline, as AutoCAD's <c>GetParameterAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the polyline.</param>
        /// <returns>The parameter: the first segment's where two meet at the point.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the polyline.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point) => ParameterOn(Chain(), point);

        /// <summary>Gets the distance along the polyline from its start to a parameter, as AutoCAD's <c>GetDistanceAtParameter</c>.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.DistanceAt(value);
        }

        /// <summary>Gets the parameter at a distance along the polyline from its start, as AutoCAD's <c>GetParameterAtDistance</c>.</summary>
        /// <param name="dist">The distance, from nought to <see cref="Length"/>.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            DxfBulgeChain chain = Chain();
            Within(dist, chain.Length, nameof(dist));
            return chain.ParameterAt(dist);
        }

        /// <summary>Gets the distance along the polyline from its start to a point of it, as AutoCAD's <c>GetDistAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the polyline.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the polyline.</exception>
        public override double GetDistAtPoint(GeoPoint3 point)
        {
            DxfBulgeChain chain = Chain();
            return chain.DistanceAt(ParameterOn(chain, point));
        }

        /// <summary>Gets the point at a distance along the polyline from its start, as AutoCAD's <c>GetPointAtDist</c>.</summary>
        /// <param name="value">The distance, from nought to <see cref="Length"/>.</param>
        /// <returns>The point, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override GeoPoint3 GetPointAtDist(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.Length, nameof(value));
            return chain.PointAt(chain.ParameterAt(value));
        }

        /// <summary>
        /// Gets the point of the polyline nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: the first segment's where two
        /// are as near. The polyline is not extended past its ends.
        /// </summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Not read: the polyline is taken as it is.</param>
        /// <returns>The nearest point, in the world.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has no vertex.</exception>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            Chain().ClosestParameter(givenPoint, out GeoPoint3 closest);
            return closest;
        }

        /// <summary>
        /// Gets the derivative of the point by the parameter, as AutoCAD's <c>GetFirstDerivative</c>: on a line the vector from
        /// its start to its end, on an arc the tangent as long as the arc.
        /// </summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The derivative, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.FirstDerivative(value);
        }

        /// <summary>Gets the second derivative of the point by the parameter, as AutoCAD's <c>GetSecondDerivative</c>: nought on a line.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The second derivative, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.SecondDerivative(value);
        }

        /// <summary>
        /// Turns the polyline to run from its end to its start, as AutoCAD's <c>ReverseCurve</c>: the vertices in the other order,
        /// each segment's bulge turned over and its widths swapped; a closed polyline keeps its first vertex.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public override void ReverseCurve()
        {
            List<List<KeyValuePair<int, string>>> vertices = Vertices();
            int n = vertices.Count;

            if (n < 2)
            {
                return;
            }

            bool closed = Closed;
            double constant = GetDouble(43, 0.0);
            var reversed = new List<List<KeyValuePair<int, string>>>(n);

            for (int j = 0; j < n; j++)
            {
                int from = closed ? (n - j) % n : n - 1 - j;
                int segment = closed ? (n - 1 - j + n) % n : n - 2 - j;
                List<KeyValuePair<int, string>> source = vertices[from];
                var vertex = new List<KeyValuePair<int, string>>
                {
                    new KeyValuePair<int, string>(10, Text(source, 10) ?? "0"),
                    new KeyValuePair<int, string>(20, Text(source, 20) ?? "0"),
                };

                if (Text(source, 91) != null)
                {
                    vertex.Add(new KeyValuePair<int, string>(91, Text(source, 91)));
                }

                if (segment >= 0)
                {
                    List<KeyValuePair<int, string>> leaves = vertices[segment];
                    double start = Value(leaves, 41, constant);
                    double end = Value(leaves, 40, constant);
                    double bulge = -Value(leaves, 42, 0.0);

                    if (Text(leaves, 40) != null || Text(leaves, 41) != null)
                    {
                        vertex.Add(new KeyValuePair<int, string>(40, Numbers.Exact(start + 0.0)));
                        vertex.Add(new KeyValuePair<int, string>(41, Numbers.Exact(end + 0.0)));
                    }

                    if (bulge != 0.0)
                    {
                        vertex.Add(new KeyValuePair<int, string>(42, Numbers.Exact(bulge + 0.0)));
                    }
                }

                reversed.Add(vertex);
            }

            Write(reversed);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the polyline, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: each vertex taken
        /// from the old plane to the world, moved, and into the plane of the new normal, square to where the old plane's axes
        /// go; the elevation that of the first vertex there, the widths and thickness scaled with it, the bulges kept.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it takes the plane of the polyline to no plane, or scales it more one
        /// way than another while the polyline has arcs, which would no longer be arcs.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            var ocs = new DxfOcsTransform(Normal, transform);
            List<List<KeyValuePair<int, string>>> vertices = Vertices();

            if (!ocs.ScaleUniform && HasBulges)
            {
                throw new ArgumentException("A polyline with arcs is scaled the same along every direction of its plane, or its arcs would be ellipses.", nameof(transform));
            }

            double elevation = Elevation;
            double newElevation = elevation;

            for (int i = 0; i < vertices.Count; i++)
            {
                List<KeyValuePair<int, string>> vertex = vertices[i];
                GeoPoint3 moved = ocs.Vertex(new GeoPoint3(Value(vertex, 10, 0.0), Value(vertex, 20, 0.0), elevation));

                if (i == 0)
                {
                    newElevation = moved.Z;
                }

                Put(vertex, 10, moved.X, 0.0, false);
                Put(vertex, 20, moved.Y, 0.0, false);

                foreach (int code in new[] { 40, 41 })
                {
                    if (Text(vertex, code) != null)
                    {
                        Put(vertex, code, ocs.Width(Value(vertex, code, 0.0)), 0.0, false);
                    }
                }
            }

            Write(vertices);
            var head = new List<KeyValuePair<int, string>>();
            head.AddRange(NumberGroups(38, newElevation, 0.0));

            if (GetString(43) != null)
            {
                head.AddRange(NumberGroups(43, ocs.Width(GetDouble(43, 0.0)), 0.0));
            }

            if (GetString(39) != null)
            {
                head.AddRange(NumberGroups(39, ocs.Thickness(GetThickness()), 0.0));
            }

            head.AddRange(NormalGroups(ocs.NewExtrusion));
            SetGroups(head.ToArray());
        }

        #endregion

        [DxfType("LWPOLYLINE")]
        internal static DxfEntity Create(DxfItem item) => new DxfPolyline(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The vertices as the record has them, each its groups 10, 20, 91, 40, 41 and 42 in their order, read as the reader reads them.</summary>
        private List<List<KeyValuePair<int, string>>> Vertices()
        {
            var vertices = new List<List<KeyValuePair<int, string>>>();
            List<KeyValuePair<int, string>> current = null;

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                if (group.TypeCode == 10)
                {
                    current = new List<KeyValuePair<int, string>>();
                    vertices.Add(current);
                }

                if (current != null && Array.IndexOf(VertexOrder, (int)group.TypeCode) >= 0)
                {
                    current.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
            }

            return vertices;
        }

        /// <summary>Writes the vertices in one run where they stood, and their count.</summary>
        private void Write(List<List<KeyValuePair<int, string>>> vertices)
        {
            var run = new List<KeyValuePair<int, string>>();

            foreach (List<KeyValuePair<int, string>> vertex in vertices)
            {
                run.AddRange(vertex);
            }

            ReplaceGroups(0, VertexCodes, run.ToArray());
            SetInt(90, vertices.Count, 0);
        }

        /// <summary>The chain of the vertices, at the elevation, in the system of the normal.</summary>
        private DxfBulgeChain Chain()
        {
            List<List<KeyValuePair<int, string>>> vertices = Vertices();
            double elevation = Elevation;
            var points = new List<GeoPoint3>(vertices.Count);
            var bulges = new List<double>(vertices.Count);

            foreach (List<KeyValuePair<int, string>> vertex in vertices)
            {
                points.Add(new GeoPoint3(Value(vertex, 10, 0.0), Value(vertex, 20, 0.0), elevation));
                bulges.Add(Value(vertex, 42, 0.0));
            }

            GeoVector3 normal = Normal;
            OcsAxes(normal, out GeoVector3 u, out GeoVector3 v);
            return new DxfBulgeChain(normal, u, v, points, bulges, Closed);
        }

        /// <summary>Refuses a segment that is not of a type.</summary>
        private void Segment(int index, DxfSegmentType type)
        {
            DxfSegmentType actual = GetSegmentType(index);

            if (actual != type)
            {
                throw new InvalidOperationException("The segment that leaves the vertex " + index.ToString(CultureInfo.InvariantCulture) + " is " + actual + ", not " + type + ".");
            }
        }

        private void SetVertexNumber(int index, int code, double value, string parameter)
        {
            Guard.Finite(value, parameter, "The number has to be a finite number.");
            List<List<KeyValuePair<int, string>>> vertices = Vertices();

            if (Put(VertexAt(vertices, index), code, value, 0.0, true))
            {
                Write(vertices);
            }
        }

        private void SetWidth(int index, int code, double value, string parameter)
        {
            Guard.NonNegative(value, parameter, "A width has to be a finite number, nought or more.");
            List<List<KeyValuePair<int, string>>> vertices = Vertices();
            List<KeyValuePair<int, string>> vertex = VertexAt(vertices, index);
            double constant = GetDouble(43, 0.0);

            if (Same(value, Value(vertex, code, constant)))
            {
                return;
            }

            // The widths are no longer one: the constant width goes to each vertex that had none of its own.
            if (constant != 0.0)
            {
                foreach (List<KeyValuePair<int, string>> each in vertices)
                {
                    foreach (int width in new[] { 40, 41 })
                    {
                        if (Text(each, width) == null)
                        {
                            Put(each, width, constant, 0.0, false);
                        }
                    }
                }
            }

            Put(vertex, code, value, 0.0, false);
            int other = code == 40 ? 41 : 40;

            if (Text(vertex, other) == null)
            {
                Put(vertex, other, 0.0, double.NaN, false);
            }

            if (Value(vertex, 40, 0.0) == 0.0 && Value(vertex, 41, 0.0) == 0.0)
            {
                vertex.RemoveAll(g => g.Key == 40 || g.Key == 41);
            }

            Write(vertices);

            if (constant != 0.0)
            {
                SetString(43, null);
            }
        }

        private void SetFlag(int flag, bool on)
        {
            int flags = GetInt(70, 0);
            SetInt(70, on ? flags | flag : flags & ~flag, 0);
        }

        /// <summary>The group that sets a number of the head, none where it is the one read.</summary>
        private KeyValuePair<int, string>[] NumberGroups(int code, double value, double fallback)
            => Same(value, GetDouble(code, fallback)) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(code, Numbers.Exact(value + 0.0)) };

        private static double ParameterOn(DxfBulgeChain chain, GeoPoint3 point)
        {
            double parameter = chain.ClosestParameter(point, out GeoPoint3 closest);

            if (!closest.IsEqualTo(point, Tolerance.Default))
            {
                throw new ArgumentException("The point " + point + " is not on the polyline.", nameof(point));
            }

            return parameter;
        }

        private static List<KeyValuePair<int, string>> VertexAt(List<List<KeyValuePair<int, string>>> vertices, int index)
        {
            if (index < 0 || index >= vertices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The polyline has " + vertices.Count.ToString(CultureInfo.InvariantCulture) + " vertices, from nought.");
            }

            return vertices[index];
        }

        /// <summary>The text of the last group of a code of a vertex, as the reader takes the last; null where it has none.</summary>
        private static string Text(List<KeyValuePair<int, string>> vertex, int code)
        {
            for (int i = vertex.Count - 1; i >= 0; i--)
            {
                if (vertex[i].Key == code)
                {
                    return vertex[i].Value;
                }
            }

            return null;
        }

        private static double Value(List<KeyValuePair<int, string>> vertex, int code, double fallback)
            => Numbers.TryRead(Text(vertex, code), out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>
        /// Gives a group of a vertex a number, in place where it has one and where its order puts it where not; takes it out
        /// where it is the fallback and asked to. False where the number read is the same to the bit.
        /// </summary>
        private static bool Put(List<KeyValuePair<int, string>> vertex, int code, double value, double fallback, bool dropAtNought)
        {
            if (Text(vertex, code) != null && Same(value, Value(vertex, code, fallback)))
            {
                return false;
            }

            if (Text(vertex, code) == null && Same(value, fallback))
            {
                return false;
            }

            if (dropAtNought && value == 0.0)
            {
                return vertex.RemoveAll(g => g.Key == code) > 0;
            }

            string text = Numbers.Exact(value + 0.0);
            int at = vertex.FindLastIndex(g => g.Key == code);

            if (at >= 0)
            {
                vertex[at] = new KeyValuePair<int, string>(code, text);
                return true;
            }

            int rank = Array.IndexOf(VertexOrder, code);
            int put = vertex.FindLastIndex(g => Array.IndexOf(VertexOrder, g.Key) < rank) + 1;
            vertex.Insert(put, new KeyValuePair<int, string>(code, text));
            return true;
        }
    }
}
