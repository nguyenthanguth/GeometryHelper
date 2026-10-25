using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
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

        // The vertices last read and the record they were read from; where each vertex begins in its bytes, and where the run
        // of them ends, none where they are not spliced; where the count stands, and the count.
        private List<List<KeyValuePair<int, string>>> _read;
        private byte[] _readBytes;
        private int _readStart;
        private int _readEnd;
        private List<int> _starts;
        private int _countFrom;
        private int _countTo;
        private int _count;

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
        public int NumberOfVertices => Read().Count;

        /// <summary>
        /// Gets a vertex in the system of the polyline's normal (groups 10 and 20), as AutoCAD's <c>GetPoint2dAt</c>.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The vertex.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public GeoPoint2 GetPoint2dAt(int index)
        {
            List<KeyValuePair<int, string>> vertex = VertexAt(Read(), index);
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

            var vertex = new List<KeyValuePair<int, string>>(VertexAt(Read(), index));
            bool changed = Put(vertex, 10, pt.X, 0.0, false) | Put(vertex, 20, pt.Y, 0.0, false);

            if (changed)
            {
                SetVertex(index, vertex);
            }
        }

        /// <summary>Gets the bulge of the segment that leaves a vertex (group 42), as AutoCAD's <c>GetBulgeAt</c>: nought for a line.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The tangent of a quarter of the angle the arc sweeps, above nought counter-clockwise; nought where the vertex has none.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public double GetBulgeAt(int index) => Value(VertexAt(Read(), index), 42, 0.0);

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
        public double GetStartWidthAt(int index) => Value(VertexAt(Read(), index), 40, GetDouble(43, 0.0));

        /// <summary>
        /// Gets the width of the polyline where the segment that leaves a vertex ends (group 41), as AutoCAD's
        /// <c>GetEndWidthAt</c>: the polyline's constant width (43) where the vertex gives none.
        /// </summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The width.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no vertex of the index.</exception>
        public double GetEndWidthAt(int index) => Value(VertexAt(Read(), index), 41, GetDouble(43, 0.0));

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
            List<List<KeyValuePair<int, string>>> vertices = Read();

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

            if (!Splice(index, 0, vertex))
            {
                vertices = Vertices();
                vertices.Insert(index, vertex);
                Write(vertices);
            }
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
            List<List<KeyValuePair<int, string>>> vertices = Read();
            VertexAt(vertices, index);

            if (vertices.Count <= 2)
            {
                throw new InvalidOperationException("A polyline keeps two vertices at least, and this one has " + vertices.Count.ToString(CultureInfo.InvariantCulture) + ": erase the polyline instead.");
            }

            if (!Splice(index, 1))
            {
                vertices = Vertices();
                vertices.RemoveAt(index);
                Write(vertices);
            }
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
                List<List<KeyValuePair<int, string>>> vertices = Read();

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
                foreach (List<KeyValuePair<int, string>> vertex in Read())
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
                List<List<KeyValuePair<int, string>>> vertices = Read();

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
            List<List<KeyValuePair<int, string>>> vertices = Read();
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
        /// way than another or shears it while the polyline has arcs, which would no longer be arcs.</exception>
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

        /// <summary>
        /// Gets the pieces the polyline is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: polylines of the same look
        /// in no file, an open one from its start to the first parameter, on to the next and to its end, a closed one from each
        /// to the next and from the last on round its first vertex to the first, given whole where it is cut at fewer than two
        /// places. A segment cut keeps the part of its arc (its bulge that of the part) and its widths read off along it: the
        /// polyline (0, 0), (10, 0), (10, 10) with a bulge of 1 on its second vertex cut at 1.5 is (0, 0), (10, 0), (15, 5) with
        /// a bulge of 0.414 and (15, 5), (10, 10) with the same. A parameter at an end or within the point tolerance along the
        /// polyline of another cuts nothing.
        /// </summary>
        /// <param name="parameters">The parameters to cut at, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The pieces, in the order of the polyline.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is past either end.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices.</exception>
        public override IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters)
        {
            List<DxfPolyVertex> v = Model();
            DxfBulgeChain chain = Chain();
            Two(v.Count);
            double end = chain.EndParam;
            bool closed = Closed;
            List<double> cuts = DxfCurveTools.Cuts(parameters, 0.0, end, closed, (a, b) => chain.DistanceAt(b) - chain.DistanceAt(a), x => Within(x, end, nameof(parameters)));
            List<List<DxfPolyVertex>> pieces = DxfPolylineEdit.Split(v, closed, cuts);

            if (pieces.Count == 0)
            {
                return new DxfCurve[] { (DxfPolyline)Copy() };
            }

            var result = new List<DxfCurve>(pieces.Count);

            foreach (List<DxfPolyVertex> piece in pieces)
            {
                var copy = (DxfPolyline)Copy();
                copy.WriteModel(piece, null, false);
                result.Add(copy);
            }

            return result;
        }

        /// <summary>
        /// Gets the polylines at a distance from this one in its plane, as AutoCAD's <c>GetOffsetCurves</c>: to the right of its
        /// way seen from the tip of its <see cref="Normal"/> where the distance is above nought, so that a closed polyline running
        /// counter-clockwise grows and an arc segment running so takes the larger radius, as a <see cref="DxfArc"/> does. The
        /// arcs stay arcs and the corners are run on until the segments meet, as AutoCAD's OFFSETGAPTYPE 0 runs them; where a
        /// part comes nearer the polyline than the distance it is cut away, as the library's offsets of a chain cut it, which
        /// can leave several polylines or none. The square 10 by 10 counter-clockwise offset by 1 is the square 12 by 12 about
        /// the same centre. Each keeps the look, elevation, normal, thickness and constant width; widths of the vertices go.
        /// </summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The polylines, of the same look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist)
        {
            Guard.Finite(offsetDist, nameof(offsetDist), "An offset has to be a finite number.");
            List<DxfPolyVertex> v = Model();
            Two(v.Count);
            var result = new List<DxfCurve>();

            foreach (KeyValuePair<List<DxfPolyVertex>, bool> one in DxfPolylineEdit.Offset(v, Closed, offsetDist, Elevation))
            {
                var copy = (DxfPolyline)Copy();
                copy.WriteModel(one.Key, null, one.Value);
                result.Add(copy);
            }

            return result;
        }

        /// <summary>
        /// Gets the polylines at a distance from this one in the plane of a normal, as AutoCAD's
        /// <c>GetOffsetCurvesGivenPlaneNormal</c>: as <see cref="GetOffsetCurves(double)"/>, the plane being the polyline's own,
        /// and the side the right seen from the tip of the normal given, the left of the polyline's way where it is the
        /// polyline's normal turned over. A <see cref="DxfArc"/> and a <see cref="DxfCircle"/> grow their radius whichever way
        /// the normal given points, so that under the normal turned over a polyline of one arc and that arc offset to opposite sides.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane, along the polyline's <see cref="Normal"/> either way.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The polylines, of the same look, in no file.</returns>
        /// <exception cref="ArgumentException">Thrown when the normal is not along the polyline's within the vector tolerance.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
        {
            GeoVector3 normal = Normal;

            if (!planeNormal.IsValid || planeNormal.IsZeroLength(Tolerance.Default) || !planeNormal.IsParallelTo(normal, Tolerance.Default))
            {
                throw new ArgumentException("A polyline is offset in its own plane: the normal " + planeNormal + " is not along the polyline's.", nameof(planeNormal));
            }

            return GetOffsetCurves(planeNormal.DotProduct(normal) < 0.0 ? -offsetDist : offsetDist);
        }

        /// <summary>
        /// Runs an end segment of the polyline on to a parameter past an end, as AutoCAD's <c>Extend</c>: above
        /// <see cref="EndParam"/> the last segment on, its end vertex moved to how far past it the parameter is along that
        /// segment (EndParam + 0.5 runs it on by half its length, or half its angle on an arc); below nought the first back.
        /// A line runs on along itself, an arc round its circle, its bulge that of the arc it becomes; the widths run on as they
        /// change along it. The record is written once.
        /// </summary>
        /// <param name="newParameter">The parameter, below nought or above <see cref="EndParam"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter lies on the polyline, is not finite, or would
        /// take an arc round a whole turn.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is closed, has fewer than two vertices or an end
        /// segment of no length, or is erased.</exception>
        public override void Extend(double newParameter)
        {
            Guard.Finite(newParameter, nameof(newParameter), "A parameter has to be a finite number.");
            List<List<KeyValuePair<int, string>>> was = Vertices();
            List<DxfPolyVertex> v = Model();
            Open(v.Count);

            if (newParameter >= 0.0 && newParameter <= v.Count - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(newParameter), newParameter, "The parameter lies on the polyline: one past an end extends it.");
            }

            DxfPolylineEdit.Extend(v, newParameter, nameof(newParameter));
            WriteModel(v, was, false);
        }

        /// <summary>
        /// Moves an end of the polyline to a point on its end segment run on, as AutoCAD's <c>Extend</c>: on the line through a
        /// line segment or the circle of an arc, in the polyline's plane, past the end it moves: a point on the segment itself is
        /// refused, as a spline's is, an end being run on and not cut back.
        /// </summary>
        /// <param name="extendStart">Whether the first vertex is moved, else the last.</param>
        /// <param name="toPoint">The point, in the world, within the point tolerance of the end segment run on.</param>
        /// <exception cref="ArgumentException">Thrown when the point is off the end segment run on, on the polyline itself rather than past the end, or out of the polyline's plane.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is closed, has fewer than two vertices or an end
        /// segment of no length, or is erased.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint)
        {
            List<List<KeyValuePair<int, string>>> was = Vertices();
            List<DxfPolyVertex> v = Model();
            Open(v.Count);
            GeoPoint3 local = WorldToOcs(Normal, toPoint);
            double share = Math.Abs(local.Z - Elevation) <= Tolerance.Default.EqualPoint ? DxfPolylineEdit.ShareOf(v, extendStart, local.X, local.Y, 0.0, true) : double.NaN;

            if (double.IsNaN(share))
            {
                throw new ArgumentException("The point " + toPoint + " is not on the polyline's end segment run on.", nameof(toPoint));
            }

            DxfPolylineEdit.Beyond(share, extendStart, toPoint, nameof(toPoint));
            DxfPolylineEdit.Extend(v, !extendStart, share, nameof(toPoint));
            WriteModel(v, was, false);
        }

        #endregion

        #region Converting

        /// <summary>
        /// Gives the polyline the vertices, look and data of a <see cref="DxfPolyline2d"/>, as AutoCAD's <c>ConvertFrom</c>: its
        /// vertices with their bulges and widths in the same system, its normal, elevation, thickness, closure, linetype
        /// generation and constant width; with <paramref name="transferId"/> this polyline takes the other's place in its
        /// drawing, its handle, owner, extension dictionary and reactors, as AutoCAD hands the object id over, and the other is
        /// erased. A curve-fitted polyline gives its vertices as drawn, the extra ones the fitting made among them; one fitted
        /// with a spline is refused, as AutoCAD refuses it.
        /// </summary>
        /// <param name="entity">The polyline, a <see cref="DxfPolyline2d"/>.</param>
        /// <param name="transferId">Whether this polyline takes the other's place in its drawing; it is then to be in no file itself.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity is not a <see cref="DxfPolyline2d"/>, or is fitted with a
        /// spline, or its drawing is of R12, which has no LWPOLYLINE.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the place is to be taken and this polyline is in a file, or the
        /// other is in none; or either is erased.</exception>
        public void ConvertFrom(DxfEntity entity, bool transferId)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!(entity is DxfPolyline2d source))
            {
                throw new ArgumentException("A polyline is converted from a DxfPolyline2d, and was given a " + entity.GetType().Name + ".", nameof(entity));
            }

            DxfPolylineConvert.Into(source, this, transferId);
        }

        /// <summary>
        /// Gets a <see cref="DxfPolyline2d"/> of the polyline's vertices, look and data, as AutoCAD's <c>ConvertTo</c>: its
        /// vertices with their bulges and widths, its normal, elevation, thickness, closure and linetype generation; with
        /// <paramref name="transferId"/> the new polyline takes this one's place in its drawing, its handle, owner, extension
        /// dictionary and reactors, and this one is erased, as AutoCAD hands the object id over.
        /// </summary>
        /// <param name="transferId">Whether the new polyline takes this one's place; this one is then to be in a file.</param>
        /// <returns>The polyline of the plane: in no file, or in this one's place.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the place is to be taken and this polyline is in no file, or is erased.</exception>
        public DxfPolyline2d ConvertTo(bool transferId) => DxfPolylineConvert.Out(this, transferId);

        #endregion

        #region The vertices as the editing sees them

        /// <summary>The vertices in the editing's model: points in the system of the normal at the elevation, bulges, widths (the constant width where a vertex gives none).</summary>
        internal List<DxfPolyVertex> Model()
        {
            List<List<KeyValuePair<int, string>>> vertices = Read();
            double elevation = Elevation;
            double constant = GetDouble(43, 0.0);
            var model = new List<DxfPolyVertex>(vertices.Count);

            foreach (List<KeyValuePair<int, string>> vertex in vertices)
            {
                bool widths = Text(vertex, 40) != null || Text(vertex, 41) != null;
                model.Add(new DxfPolyVertex(Value(vertex, 10, 0.0), Value(vertex, 20, 0.0), elevation, Value(vertex, 42, 0.0), Value(vertex, 40, constant), Value(vertex, 41, constant), widths));
            }

            return model;
        }

        /// <summary>
        /// Writes the vertices of the editing's model, the groups of a vertex as they were (<paramref name="was"/>, by index)
        /// where a number is the same to the bit, and the polyline closed or not, in one writing of the record and its count.
        /// </summary>
        internal void WriteModel(List<DxfPolyVertex> model, List<List<KeyValuePair<int, string>>> was, bool closed)
        {
            var vertices = new List<List<KeyValuePair<int, string>>>(model.Count);

            for (int i = 0; i < model.Count; i++)
            {
                DxfPolyVertex q = model[i];
                List<KeyValuePair<int, string>> vertex = was != null && i < was.Count ? new List<KeyValuePair<int, string>>(was[i]) : new List<KeyValuePair<int, string>>();
                Put(vertex, 10, q.X, double.NaN, false);
                Put(vertex, 20, q.Y, double.NaN, false);

                if (q.Widths)
                {
                    Put(vertex, 40, q.StartWidth, double.NaN, false);
                    Put(vertex, 41, q.EndWidth, double.NaN, false);
                }
                else
                {
                    vertex.RemoveAll(g => g.Key == 40 || g.Key == 41);
                }

                Put(vertex, 42, q.Bulge, 0.0, true);
                vertices.Add(vertex);
            }

            Write(vertices);
            SetFlag(1, closed);
        }

        /// <summary>
        /// Gives the polyline its vertices in one writing, as a polyline made in code from a list of points is given them: each
        /// point in the system of the normal, the bulge where it is not nought; and whether it is closed.
        /// </summary>
        internal void SetVertices(IReadOnlyList<GeoPoint2> points, IReadOnlyList<double> bulges, bool closed)
        {
            var model = new List<DxfPolyVertex>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                model.Add(new DxfPolyVertex(points[i].X, points[i].Y, 0.0, bulges == null ? 0.0 : bulges[i], 0.0, 0.0, false));
            }

            WriteModel(model, null, closed);
        }

        /// <summary>
        /// Gives the polyline vertices of the editing's model and the data of a polyline: its normal, elevation, thickness,
        /// closure and linetype generation, and a constant width where it is not nought (the vertices then giving none of
        /// their own), as a polyline converted from a <see cref="DxfPolyline2d"/> takes them.
        /// </summary>
        internal void Take(List<DxfPolyVertex> vertices, bool closed, GeoVector3 normal, double elevation, double thickness, bool generation, double constantWidth)
        {
            var model = new List<DxfPolyVertex>(vertices.Count);

            foreach (DxfPolyVertex vertex in vertices)
            {
                DxfPolyVertex q = vertex;
                q.Widths = constantWidth == 0.0 && (q.Widths || q.StartWidth != 0.0 || q.EndWidth != 0.0);
                model.Add(q);
            }

            SetGroups(Join(NormalGroups(normal), NumberGroups(38, elevation, 0.0)));

            if (constantWidth != 0.0)
            {
                SetDouble(43, constantWidth, 0.0);
            }
            else
            {
                SetString(43, null);
            }

            SetThickness(thickness);
            WriteModel(model, null, closed);
            SetFlag(128, generation);
        }

        /// <summary>
        /// The record of a LWPOLYLINE of vertices of the editing's model and the data of a polyline, as AutoCAD writes one: its
        /// marker, count, flags, constant width, elevation and thickness where not nought, its vertices, and its normal where it
        /// is not (0, 0, 1); for a polyline of the plane converted to take its place.
        /// </summary>
        internal static List<KeyValuePair<string, List<DxfTypedValue>>> Records(List<DxfPolyVertex> model, bool closed, GeoVector3 normal, double elevation, double thickness, bool generation, double constantWidth)
        {
            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDbPolyline"),
                new DxfTypedValue(90, model.Count.ToString(CultureInfo.InvariantCulture)),
                new DxfTypedValue(70, ((closed ? 1 : 0) | (generation ? 128 : 0)).ToString(CultureInfo.InvariantCulture)),
            };

            if (constantWidth != 0.0)
            {
                groups.Add(new DxfTypedValue(43, Numbers.Exact(constantWidth + 0.0)));
            }

            if (elevation != 0.0)
            {
                groups.Add(new DxfTypedValue(38, Numbers.Exact(elevation + 0.0)));
            }

            if (thickness != 0.0)
            {
                groups.Add(new DxfTypedValue(39, Numbers.Exact(thickness + 0.0)));
            }

            foreach (DxfPolyVertex q in model)
            {
                groups.Add(new DxfTypedValue(10, Numbers.Exact(q.X + 0.0)));
                groups.Add(new DxfTypedValue(20, Numbers.Exact(q.Y + 0.0)));

                if (constantWidth == 0.0 && (q.Widths || q.StartWidth != 0.0 || q.EndWidth != 0.0))
                {
                    groups.Add(new DxfTypedValue(40, Numbers.Exact(q.StartWidth + 0.0)));
                    groups.Add(new DxfTypedValue(41, Numbers.Exact(q.EndWidth + 0.0)));
                }

                if (q.Bulge != 0.0)
                {
                    groups.Add(new DxfTypedValue(42, Numbers.Exact(q.Bulge + 0.0)));
                }
            }

            if (!(normal.X == 0.0 && normal.Y == 0.0 && normal.Z == 1.0))
            {
                groups.Add(new DxfTypedValue(210, Numbers.Exact(normal.X + 0.0)));
                groups.Add(new DxfTypedValue(220, Numbers.Exact(normal.Y + 0.0)));
                groups.Add(new DxfTypedValue(230, Numbers.Exact(normal.Z + 0.0)));
            }

            return new List<KeyValuePair<string, List<DxfTypedValue>>> { new KeyValuePair<string, List<DxfTypedValue>>("LWPOLYLINE", groups) };
        }

        /// <summary>Refuses a polyline of fewer than two vertices, which has no segment to work on.</summary>
        private static void Two(int count)
        {
            if (count < 2)
            {
                throw new InvalidOperationException("A polyline of " + count.ToString(CultureInfo.InvariantCulture) + " vertices has no segment.");
            }
        }

        /// <summary>Refuses a closed polyline, which has no end, or one of fewer than two vertices.</summary>
        private void Open(int count)
        {
            Two(count);

            if (Closed)
            {
                throw new InvalidOperationException("A closed polyline has no end to be extended.");
            }
        }

        #endregion

        /// <summary>
        /// Gets the lines and arcs the polyline breaks into, as AutoCAD's <c>Explode</c>: a LINE for each straight segment and an
        /// ARC for each bulged one, in its order, in the plane of its normal at its elevation, each with its look and thickness;
        /// its widths left out, as AutoCAD leaves them out, and a segment of no length too. A closed square of side 10 with one
        /// side bulged by 1 comes out as three lines and a half circle, and a donut, two vertices 10 apart each bulged by 1 and
        /// closed, as two half circles of radius 5. Each LINE and ARC is on the polyline's
        /// <see cref="Normal"/>, as AutoCAD gives it the polyline's, so that its wall stands up the same way: on the normal
        /// (0, 0, -1) at elevation 5 with a thickness of 2, the walls stand from z = -5 down to -7.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            // The vertices in the world, as GetPoint3dAt gives each, read at once.
            GeoVector3 normal = Normal;
            double elevation = Elevation;
            var vertices = new List<GeoPoint3>();

            foreach (List<KeyValuePair<int, string>> vertex in Read())
            {
                vertices.Add(OcsToWorld(normal, new GeoPoint3(Value(vertex, 10, 0.0), Value(vertex, 20, 0.0), elevation)));
            }

            return ChainParts(normal, vertices).AsReadOnly();
        }

        [DxfType("LWPOLYLINE")]
        internal static DxfEntity Create(DxfItem item) => new DxfPolyline(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The vertices as the record has them, each its groups 10, 20, 91, 40, 41 and 42 in their order, read as the reader reads them: a copy to change.</summary>
        private List<List<KeyValuePair<int, string>>> Vertices()
        {
            List<List<KeyValuePair<int, string>>> read = Read();
            var vertices = new List<List<KeyValuePair<int, string>>>(read.Count);

            foreach (List<KeyValuePair<int, string>> vertex in read)
            {
                vertices.Add(new List<KeyValuePair<int, string>>(vertex));
            }

            return vertices;
        }

        /// <summary>
        /// The vertices as <see cref="Vertices"/> gives them, read once and kept while the record is the one they were read from,
        /// so that a vertex read, put in, set or taken out one at a time reads no group of the others: not to be changed.
        /// </summary>
        private List<List<KeyValuePair<int, string>>> Read()
        {
            DxfFile.Rec record = Item.File.RecordAt(Item, 0);

            if (_read != null && ReferenceEquals(record.Bytes, _readBytes) && record.Start == _readStart && record.End == _readEnd)
            {
                return _read;
            }

            var vertices = new List<List<KeyValuePair<int, string>>>();
            var starts = new List<int>();
            List<KeyValuePair<int, string>> current = null;
            var places = new List<int>();
            IReadOnlyList<DxfTypedValue> groups = Item.File.OwnGroupsOf(Item, 0, places);
            int first = -1;
            int last = -1;
            int taken = 0;
            int stray = 0;
            int counts = 0;
            int countAt = -1;
            bool joined = true;

            for (int i = 0; i < groups.Count; i++)
            {
                DxfTypedValue group = groups[i];
                bool ofVertex = Array.IndexOf(VertexOrder, (int)group.TypeCode) >= 0;

                if (group.TypeCode == 10)
                {
                    current = new List<KeyValuePair<int, string>>();
                    vertices.Add(current);
                    starts.Add(places[2 * i]);
                    first = first < 0 ? i : first;
                }

                if (current != null && ofVertex)
                {
                    current.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                    joined &= last < 0 || places[2 * last + 1] == places[2 * i];
                    last = i;
                    taken++;
                }
                else if (ofVertex)
                {
                    stray++;
                }

                if (group.TypeCode == 90)
                {
                    counts++;
                    countAt = first < 0 ? i : -1;
                    _count = int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
                }
            }

            // The vertices are spliced in place only where their groups are one run of bytes, none of their codes stands
            // outside it, and one count stands before it; any other record is written whole, as the reader would read it.
            if (first >= 0 && last - first + 1 == taken && joined && stray == 0 && counts == 1 && countAt >= 0)
            {
                starts.Add(places[2 * last + 1]);
                _starts = starts;
                _countFrom = places[2 * countAt];
                _countTo = places[2 * countAt + 1];
            }
            else
            {
                _starts = null;
            }

            _read = vertices;
            _readBytes = record.Bytes;
            _readStart = record.Start;
            _readEnd = record.End;
            return vertices;
        }

        /// <summary>
        /// Writes a vertex in place of a run of those read, or none, the bytes of the others kept, and the count where it
        /// changes: one copy of the bytes of the record for each, no reading of its groups. False, with nothing written, where
        /// the record is not one <see cref="Read"/> splices.
        /// </summary>
        private bool Splice(int index, int count, List<KeyValuePair<int, string>> made = null)
        {
            List<List<KeyValuePair<int, string>>> read = Read();

            if (_starts == null)
            {
                return false;
            }

            int from = _starts[index];
            int to = _starts[index + count];
            int expected = _readEnd - _readStart - (to - from);
            int written = Item.File.SpliceRecord(Item, 0, from, to, made ?? new List<KeyValuePair<int, string>>(), true);
            expected += written;
            _starts.RemoveRange(index + 1, count);
            read.RemoveRange(index, count);

            if (made != null)
            {
                _starts.Insert(index + 1, from + written);
                read.Insert(index, made);
            }

            Shift(made != null ? index + 2 : index + 1, written - (to - from));
            int number = read.Count;

            if (number != _count)
            {
                // The count is no geometry, one group before the vertices.
                int length = Item.File.SpliceRecord(Item, 0, _countFrom, _countTo, new[] { new KeyValuePair<int, string>(90, number.ToString(CultureInfo.InvariantCulture)) }, false);
                Shift(0, length - (_countTo - _countFrom));
                expected += length - (_countTo - _countFrom);
                _countTo = _countFrom + length;
                _count = number;
            }

            // A record that came out other than spliced, as one given extended data in the same writing, is read again.
            DxfFile.Rec record = Item.File.RecordAt(Item, 0);
            _read = record.End - record.Start == expected ? read : null;
            _readBytes = record.Bytes;
            _readStart = record.Start;
            _readEnd = record.End;
            return true;
        }

        /// <summary>Moves where the vertices from one on begin by some bytes.</summary>
        private void Shift(int from, int by)
        {
            for (int i = from; i < _starts.Count && by != 0; i++)
            {
                _starts[i] += by;
            }
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

            // The count is no geometry: written with no second growing of the extents by the whole polyline.
            if (vertices.Count != GetInt(90, 0))
            {
                Item.File.SetValues(Item, new[] { new KeyValuePair<int, string>(90, vertices.Count.ToString(CultureInfo.InvariantCulture)) }, GroupOrder, false);
            }
        }

        /// <summary>The chain of the vertices, at the elevation, in the system of the normal.</summary>
        private DxfBulgeChain Chain()
        {
            List<List<KeyValuePair<int, string>>> vertices = Read();
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

        /// <summary>Writes one vertex in place of the one of its index, the others kept as they are.</summary>
        private void SetVertex(int index, List<KeyValuePair<int, string>> vertex)
        {
            if (!Splice(index, 1, vertex))
            {
                List<List<KeyValuePair<int, string>>> vertices = Vertices();
                vertices[index] = vertex;
                Write(vertices);
            }
        }

        private void SetVertexNumber(int index, int code, double value, string parameter)
        {
            Guard.Finite(value, parameter, "The number has to be a finite number.");
            var vertex = new List<KeyValuePair<int, string>>(VertexAt(Read(), index));

            if (Put(vertex, code, value, 0.0, true))
            {
                SetVertex(index, vertex);
            }
        }

        private void SetWidth(int index, int code, double value, string parameter)
        {
            Guard.NonNegative(value, parameter, "A width has to be a finite number, nought or more.");
            double constant = GetDouble(43, 0.0);

            if (Same(value, Value(VertexAt(Read(), index), code, constant)))
            {
                return;
            }

            List<List<KeyValuePair<int, string>>> vertices;
            List<KeyValuePair<int, string>> vertex;

            if (constant == 0.0)
            {
                // No constant width to give the others: the vertex alone is written.
                vertex = new List<KeyValuePair<int, string>>(Read()[index]);
                Widths(vertex, code, value);
                SetVertex(index, vertex);
                return;
            }

            vertices = Vertices();
            vertex = vertices[index];

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

            Widths(vertex, code, value);
            Write(vertices);
            SetString(43, null);
        }

        /// <summary>Gives a vertex a width at one end, the other end written as nought where it had none, both taken out where both are nought.</summary>
        private static void Widths(List<KeyValuePair<int, string>> vertex, int code, double value)
        {
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
