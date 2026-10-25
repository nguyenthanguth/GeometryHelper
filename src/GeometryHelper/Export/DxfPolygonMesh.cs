using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A polygon mesh of a DXF file (a POLYLINE of flag 16 of its group 70), as AutoCAD's <c>PolygonMesh</c>: a grid of
    /// <see cref="MSize"/> rows of <see cref="NSize"/> points (<see cref="DxfPolygonMeshVertex"/>), row after row, each cell a
    /// face of four corners, closed in M where flag 1 is set and in N where flag 32 is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mesh is walked as AutoCAD walks one, <c>foreach (DxfPolygonMeshVertex vertex in mesh)</c>.
    /// <see cref="DxfEntity.GetGeometry"/> gives the body <see cref="DxfReader"/> reads of it: a grid of 3 by 4 closed in N is 8
    /// cells, 8 faces of four corners.
    /// </para>
    /// <para>
    /// A point set is written in its own record; one appended is a record put in, every other keeping its handle. The sizes are the record's groups 71 and 72, which are not changed by appending: set them to the grid the points make.
    /// </para>
    /// </remarks>
    public sealed class DxfPolygonMesh : DxfEntity, IEnumerable<DxfPolygonMeshVertex>
    {
        // The groups of AcDbPolygonMesh in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 66, 10, 20, 30, 70, 71, 72, 73, 74, 75 });

        // The groups of a point in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> VertexOrder = Array.AsReadOnly(new[] { 100, 100, 10, 20, 30, 70 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>Initializes a mesh of no points, as AutoCAD's <c>new PolygonMesh()</c>, on layer 0 in no file.</summary>
        public DxfPolygonMesh()
            : base("POLYLINE", Head(0, 0, false, false))
        {
            SetFollowers(new[] { new KeyValuePair<string, KeyValuePair<int, string>[]>("SEQEND", new KeyValuePair<int, string>[0]) });
        }

        /// <summary>
        /// Initializes a mesh of a grid of points, as AutoCAD's <c>new PolygonMesh(type, size, sizeValue, vertices, closedValue,
        /// closed)</c>, on layer 0 in no file until it is added to one.
        /// </summary>
        /// <param name="type">The surface it is smoothed into: <see cref="DxfPolyMeshType.SimpleMesh"/>, the one made in code; smooth it afterwards with <see cref="ConvertToPolyMeshType"/>.</param>
        /// <param name="size">How many rows, M, 2 or more.</param>
        /// <param name="sizeValue">How many points in each row, N, 2 or more.</param>
        /// <param name="vertices">The points, in the world, row after row, M times N of them.</param>
        /// <param name="closedValue">Whether the mesh is closed in M, the last row joined to the first.</param>
        /// <param name="closed">Whether it is closed in N, the last point of each row joined to its first.</param>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not finite, or the points are not M times N.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a size is below 2 or above 32 767.</exception>
        /// <exception cref="NotSupportedException">Thrown for a smoothed type: make the mesh simple and smooth it with <see cref="ConvertToPolyMeshType"/>.</exception>
        public DxfPolygonMesh(DxfPolyMeshType type, int size, int sizeValue, IEnumerable<GeoPoint3> vertices, bool closedValue, bool closed)
            : base("POLYLINE", Head(size, sizeValue, closedValue, closed))
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (type != DxfPolyMeshType.SimpleMesh)
            {
                throw new NotSupportedException("A mesh is made in code simple: make it a SimpleMesh and smooth it with ConvertToPolyMeshType.");
            }

            if (size < 2 || size > short.MaxValue || sizeValue < 2 || sizeValue > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException(size < 2 || size > short.MaxValue ? nameof(size) : nameof(sizeValue), "A mesh has from 2 to 32 767 rows and points in a row.");
            }

            var points = new List<DxfPolygonMeshVertex>();

            foreach (GeoPoint3 point in vertices)
            {
                points.Add(new DxfPolygonMeshVertex(point));
            }

            if (points.Count != size * sizeValue)
            {
                throw new ArgumentException("A mesh of " + size.ToString(CultureInfo.InvariantCulture) + " by " + sizeValue.ToString(CultureInfo.InvariantCulture) + " has as many points as that makes, and was given " + points.Count.ToString(CultureInfo.InvariantCulture) + ".", nameof(vertices));
            }

            SetFollowers(new[] { new KeyValuePair<string, KeyValuePair<int, string>[]>("SEQEND", new KeyValuePair<int, string>[0]) });

            for (int i = 0; i < points.Count; i++)
            {
                InsertFollowerAt(i + 1, points[i]);
            }
        }

        private DxfPolygonMesh(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets how many rows the mesh has, M (group 71), as AutoCAD's <c>MSize</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a mesh that is erased.</exception>
        public short MSize
        {
            get => (short)GetInt(71, 0);
            set => SetSize(71, value);
        }

        /// <summary>Gets or sets how many points each row of the mesh has, N (group 72), as AutoCAD's <c>NSize</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a mesh that is erased.</exception>
        public short NSize
        {
            get => (short)GetInt(72, 0);
            set => SetSize(72, value);
        }

        /// <summary>Gets or sets how many rows the smoothed surface has, M (group 73), as AutoCAD's <c>MSurfaceDensity</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the density set is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a mesh that is erased.</exception>
        public short MSurfaceDensity
        {
            get => (short)GetInt(73, 0);
            set => SetSize(73, value);
        }

        /// <summary>Gets or sets how many points each row of the smoothed surface has, N (group 74), as AutoCAD's <c>NSurfaceDensity</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the density set is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a mesh that is erased.</exception>
        public short NSurfaceDensity
        {
            get => (short)GetInt(74, 0);
            set => SetSize(74, value);
        }

        /// <summary>Gets whether the last row is joined to the first (flag 1 of group 70), as AutoCAD's <c>IsMClosed</c>.</summary>
        public bool IsMClosed => (GetInt(70, 0) & 1) != 0;

        /// <summary>Gets whether the last point of each row is joined to its first (flag 32 of group 70), as AutoCAD's <c>IsNClosed</c>.</summary>
        public bool IsNClosed => (GetInt(70, 0) & 32) != 0;

        /// <summary>
        /// Gets or sets the surface the mesh is smoothed into, as AutoCAD's <c>PolyMeshType</c>: group 75 where flag 4 of group 70
        /// says it is smoothed, <see cref="DxfPolyMeshType.SimpleMesh"/> where it is not.
        /// </summary>
        /// <value>Set, the mesh converted to the type as <see cref="ConvertToPolyMeshType"/> converts it; its own type changes nothing.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfPolyMeshType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a mesh that is erased, or whose grid cannot be smoothed so.</exception>
        public DxfPolyMeshType PolyMeshType
        {
            get => (GetInt(70, 0) & 4) == 0 ? DxfPolyMeshType.SimpleMesh : (DxfPolyMeshType)GetInt(75, 0);

            set
            {
                if (value != PolyMeshType)
                {
                    ConvertToPolyMeshType(value);
                }
            }
        }

        /// <summary>
        /// Converts the mesh to a type, as AutoCAD's <c>ConvertToPolyMeshType</c>: straightened for
        /// <see cref="DxfPolyMeshType.SimpleMesh"/>, otherwise smoothed into that surface as <see cref="SurfaceFit(DxfPolyMeshType, int, int)"/>
        /// smooths it, of the mesh's own densities (groups 73 and 74) where they are 2 or more, else the header's SURFU and SURFV
        /// (6 where the file gives none).
        /// </summary>
        /// <param name="newVal">The type.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not one of <see cref="DxfPolyMeshType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the mesh is erased, or its grid cannot be smoothed so.</exception>
        public void ConvertToPolyMeshType(DxfPolyMeshType newVal)
        {
            switch (newVal)
            {
                case DxfPolyMeshType.SimpleMesh:
                    Straighten();
                    break;

                case DxfPolyMeshType.QuadSurfaceMesh:
                case DxfPolyMeshType.CubicSurfaceMesh:
                case DxfPolyMeshType.BezierSurfaceMesh:
                    int u = MSurfaceDensity >= 2 ? MSurfaceDensity : DxfPolylineFit.Header(Item, "$SURFU", 6);
                    int v = NSurfaceDensity >= 2 ? NSurfaceDensity : DxfPolylineFit.Header(Item, "$SURFV", 6);
                    SurfaceFit(newVal, u, v);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(newVal), newVal, "A mesh is simple, or smoothed into a quadratic, cubic or Bezier surface.");
            }
        }

        /// <summary>
        /// Straightens a smoothed mesh, as AutoCAD's <c>Straighten</c> (PEDIT Desmooth): the points the smoothing made (flag 8 of
        /// their group 70) taken out, those of the grid (16) points of the mesh again, flag 4 of the mesh's group 70 and its
        /// group 75 taken out; its densities (73 and 74) kept. A mesh that is not smoothed is left as it is.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the mesh is erased.</exception>
        public void Straighten()
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            var grid = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();
            var made = new List<int>();
            bool smoothed = (GetInt(70, 0) & 4) != 0;

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                int flags = DxfFollowers.Whole(vertex.Value, 70, 0);
                smoothed |= (flags & 24) != 0;

                if ((flags & 8) != 0)
                {
                    made.Add(vertex.Key);
                }
                else if ((flags & 16) != 0)
                {
                    grid.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertex.Key, DxfFollowers.WholeChange(vertex.Value, 70, flags & ~16, 0)));
                }
            }

            if (!smoothed)
            {
                return;
            }

            SetGroupsAtMany(grid, VertexOrder);

            Item.File.RemoveFollowers(Item, made);

            SetGroups(new KeyValuePair<int, string>(70, (GetInt(70, 0) & ~4).ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(75, null));
        }

        /// <summary>
        /// Smooths the mesh into the header's surface, as AutoCAD's <c>SurfaceFit</c> (PEDIT Smooth surface): of SURFTYPE (5
        /// quadratic, 6 cubic, 8 Bezier; cubic where the file gives none), SURFU and SURFV (6 where it gives none), as
        /// <see cref="SurfaceFit(DxfPolyMeshType, int, int)"/> smooths it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the mesh is erased, or its grid cannot be smoothed so.</exception>
        public void SurfaceFit()
        {
            int type = DxfPolylineFit.Header(Item, "$SURFTYPE", 6);
            DxfPolyMeshType surface = type == 5 ? DxfPolyMeshType.QuadSurfaceMesh : type == 8 ? DxfPolyMeshType.BezierSurfaceMesh : DxfPolyMeshType.CubicSurfaceMesh;
            SurfaceFit(surface, DxfPolylineFit.Header(Item, "$SURFU", 6), DxfPolylineFit.Header(Item, "$SURFV", 6));
        }

        /// <summary>
        /// Smooths the mesh into a surface, as AutoCAD's <c>SurfaceFit</c>: its grid made the frame of the surface (flag 16 of
        /// each point's group 70, each keeping its handle), and the points of the surface added after them (flag 8), a grid of
        /// <paramref name="surfU"/> rows of <paramref name="surfV"/> points spread evenly from end to end of an open direction and
        /// round a closed one; flag 4 of the mesh's group 70, its densities in groups 73 and 74 and the surface in 75. A quadratic
        /// or cubic surface is the uniform B-spline on the grid of that degree each way (one less than the rows or points where
        /// they are fewer), clamped to the edges of an open direction; a Bezier surface the Bernstein one of the whole grid, which
        /// AutoCAD takes up to 11 by 11. A smoothed mesh is straightened first. The reader reads a smoothed mesh as the grid of its
        /// first M by N points, its frame.
        /// </summary>
        /// <param name="surfType">The surface: <see cref="DxfPolyMeshType.QuadSurfaceMesh"/>, <see cref="DxfPolyMeshType.CubicSurfaceMesh"/> or <see cref="DxfPolyMeshType.BezierSurfaceMesh"/>.</param>
        /// <param name="surfU">The rows of the surface, along M, from 2 to 200.</param>
        /// <param name="surfV">The points of each row of the surface, along N, from 2 to 200.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not a surface, or a density is outside 2 to 200.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the mesh has fewer points than its sizes ask for, sizes below 2,
        /// a Bezier grid above 11 by 11, or is erased.</exception>
        public void SurfaceFit(DxfPolyMeshType surfType, int surfU, int surfV)
        {
            if (surfType != DxfPolyMeshType.QuadSurfaceMesh && surfType != DxfPolyMeshType.CubicSurfaceMesh && surfType != DxfPolyMeshType.BezierSurfaceMesh)
            {
                throw new ArgumentOutOfRangeException(nameof(surfType), surfType, "A mesh is smoothed into a quadratic, cubic or Bezier surface.");
            }

            if (surfU < 2 || surfU > 200)
            {
                throw new ArgumentOutOfRangeException(nameof(surfU), surfU, "A surface has from 2 to 200 rows.");
            }

            if (surfV < 2 || surfV > 200)
            {
                throw new ArgumentOutOfRangeException(nameof(surfV), surfV, "A surface has from 2 to 200 points in a row.");
            }

            Straighten();
            int m = MSize;
            int n = NSize;
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);

            if (m < 2 || n < 2 || vertices.Count < m * n)
            {
                throw new InvalidOperationException("The mesh of " + m.ToString(CultureInfo.InvariantCulture) + " by " + n.ToString(CultureInfo.InvariantCulture) + " has " + vertices.Count.ToString(CultureInfo.InvariantCulture) + " points, and no grid to smooth.");
            }

            bool bezier = surfType == DxfPolyMeshType.BezierSurfaceMesh;

            if (bezier && (m > 11 || n > 11))
            {
                throw new InvalidOperationException("A Bezier surface is of a grid of 11 by 11 at most, as AutoCAD takes one; this mesh is " + m.ToString(CultureInfo.InvariantCulture) + " by " + n.ToString(CultureInfo.InvariantCulture) + ".");
            }

            int degree = surfType == DxfPolyMeshType.QuadSurfaceMesh ? 2 : 3;
            bool closedM = IsMClosed;
            bool closedN = IsNClosed;
            var grid = new GeoPoint3[m, n];
            var changes = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();

            for (int k = 0; k < vertices.Count; k++)
            {
                IReadOnlyList<DxfTypedValue> own = vertices[k].Value;

                if (k < m * n)
                {
                    grid[k / n, k % n] = DxfFollowers.Point(own, 10, Origin);
                }

                changes.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertices[k].Key, DxfFollowers.WholeChange(own, 70, DxfFollowers.Whole(own, 70, 0) | 16, 0)));
            }

            // Along M for each column, then along N through those points, for each row of the surface.
            var columns = new List<GeoPoint3>[n];
            List<double> along = null;

            for (int j = 0; j < n; j++)
            {
                var column = new List<GeoPoint3>(m);

                for (int i = 0; i < m; i++)
                {
                    column.Add(grid[i, j]);
                }

                DxfNurbs curve = Direction(column, bezier, degree, closedM, out double start, out double end);
                along = along ?? DxfPolylineFit.Spread(start, end, surfU, closedM);
                columns[j] = new List<GeoPoint3>(surfU);

                foreach (double t in along)
                {
                    columns[j].Add(curve.PointAt(t));
                }
            }

            var made = new List<DxfPolygonMeshVertex>(surfU * surfV);

            for (int r = 0; r < surfU; r++)
            {
                var row = new List<GeoPoint3>(n);

                for (int j = 0; j < n; j++)
                {
                    row.Add(columns[j][r]);
                }

                DxfNurbs curve = Direction(row, bezier, degree, closedN, out double start, out double end);

                foreach (double t in DxfPolylineFit.Spread(start, end, surfV, closedN))
                {
                    var vertex = new DxfPolygonMeshVertex(curve.PointAt(t));
                    vertex.SetFlags(8 | 64);
                    made.Add(vertex);
                }
            }

            SetGroupsAtMany(changes, VertexOrder);
            int place = RecordCount > 1 && RecordType(RecordCount - 1) == "SEQEND" ? RecordCount - 1 : RecordCount;

            foreach (DxfPolygonMeshVertex vertex in made)
            {
                InsertFollowerAt(place++, vertex);
            }

            SetGroups(
                new KeyValuePair<int, string>(70, (GetInt(70, 0) | 4).ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<int, string>(73, surfU.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<int, string>(74, surfV.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<int, string>(75, ((int)surfType).ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>The curve of a direction of the surface on its points: the Bernstein one of them all, the last joined to the first round a closed direction, or the uniform B-spline.</summary>
        private static DxfNurbs Direction(List<GeoPoint3> points, bool bezier, int degree, bool closed, out double start, out double end)
        {
            if (!bezier)
            {
                return DxfPolylineFit.Spline(points, degree, closed, out start, out end);
            }

            var all = new List<GeoPoint3>(points);

            if (closed)
            {
                all.Add(points[0]);
            }

            var knots = new List<double>();

            for (int i = 0; i < all.Count; i++)
            {
                knots.Add(0.0);
            }

            for (int i = 0; i < all.Count; i++)
            {
                knots.Add(1.0);
            }

            start = 0.0;
            end = 1.0;
            return new DxfNurbs(all.Count - 1, knots, all, null);
        }

        /// <summary>Joins the last row to the first, as AutoCAD's <c>MakeMClosed</c>: flag 1 of group 70.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the mesh is erased.</exception>
        public void MakeMClosed() => SetFlag(1, true);

        /// <summary>Parts the last row from the first, as AutoCAD's <c>MakeMOpen</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the mesh is erased.</exception>
        public void MakeMOpen() => SetFlag(1, false);

        /// <summary>Joins the last point of each row to its first, as AutoCAD's <c>MakeNClosed</c>: flag 32 of group 70.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the mesh is erased.</exception>
        public void MakeNClosed() => SetFlag(32, true);

        /// <summary>Parts the last point of each row from its first, as AutoCAD's <c>MakeNOpen</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the mesh is erased.</exception>
        public void MakeNOpen() => SetFlag(32, false);

        /// <summary>Gets an enumerator over the points, row after row, as AutoCAD's <c>GetEnumerator</c>: each the same object while its record is there.</summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<DxfPolygonMeshVertex> GetEnumerator()
        {
            var vertices = new List<DxfPolygonMeshVertex>();

            for (int record = 1; record < RecordCount; record++)
            {
                if (RecordType(record) == "VERTEX" && FollowerAt(record) is DxfPolygonMeshVertex vertex)
                {
                    vertices.Add(vertex);
                }
            }

            return vertices.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Appends a point made in code after the last, as AutoCAD's <c>AppendVertex</c>; the sizes are not changed.</summary>
        /// <param name="vertexToAppend">The point, in no mesh.</param>
        /// <exception cref="ArgumentNullException">Thrown when the point is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the point is of a mesh already, or the mesh is erased.</exception>
        public void AppendVertex(DxfPolygonMeshVertex vertexToAppend)
        {
            if (vertexToAppend == null)
            {
                throw new ArgumentNullException(nameof(vertexToAppend));
            }

            InsertFollowerAt(RecordCount > 1 && RecordType(RecordCount - 1) == "SEQEND" ? RecordCount - 1 : RecordCount, vertexToAppend);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the mesh, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: each point moved in
        /// the world, in one writing of the mesh, each record in place, its handle kept.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the mesh is erased.</exception>
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

            SetGroupsAtMany(DxfFollowers.Moved(Item, transform, _ => true), VertexOrder);
        }

        [DxfType("POLYLINE")]
        internal static DxfEntity Create(DxfItem item)
        {
            int flags = DxfPolyline2d.Flags(item);
            return (flags & 16) != 0 && (flags & 64) == 0 ? new DxfPolygonMesh(item) : null;
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        private static IEnumerable<DxfTypedValue> Head(int m, int n, bool closedM, bool closedN) => new[]
        {
            new DxfTypedValue(100, "AcDbPolygonMesh"),
            new DxfTypedValue(66, "1"),
            new DxfTypedValue(10, "0"),
            new DxfTypedValue(20, "0"),
            new DxfTypedValue(30, "0"),
            new DxfTypedValue(70, (16 | (closedM ? 1 : 0) | (closedN ? 32 : 0)).ToString(CultureInfo.InvariantCulture)),
            new DxfTypedValue(71, m.ToString(CultureInfo.InvariantCulture)),
            new DxfTypedValue(72, n.ToString(CultureInfo.InvariantCulture)),
        };

        private void SetSize(int code, short value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A size is nought or more.");
            }

            SetInt(code, value, 0);
        }

        private void SetFlag(int flag, bool on)
        {
            int flags = GetInt(70, 0);
            SetInt(70, on ? flags | flag : flags & ~flag, 0);
        }
    }
}
