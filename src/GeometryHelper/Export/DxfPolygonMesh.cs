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
        /// <param name="type">The surface it is smoothed into: <see cref="DxfPolyMeshType.SimpleMesh"/>, the one made in code.</param>
        /// <param name="size">How many rows, M, 2 or more.</param>
        /// <param name="sizeValue">How many points in each row, N, 2 or more.</param>
        /// <param name="vertices">The points, in the world, row after row, M times N of them.</param>
        /// <param name="closedValue">Whether the mesh is closed in M, the last row joined to the first.</param>
        /// <param name="closed">Whether it is closed in N, the last point of each row joined to its first.</param>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not finite, or the points are not M times N.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a size is below 2 or above 32 767.</exception>
        /// <exception cref="NotSupportedException">Thrown for a smoothed type, whose surface the library does not make yet.</exception>
        public DxfPolygonMesh(DxfPolyMeshType type, int size, int sizeValue, IEnumerable<GeoPoint3> vertices, bool closedValue, bool closed)
            : base("POLYLINE", Head(size, sizeValue, closedValue, closed))
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (type != DxfPolyMeshType.SimpleMesh)
            {
                throw new NotSupportedException("A smoothed mesh is not made in code yet: make it a SimpleMesh.");
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
        /// Gets the surface the mesh is smoothed into, as AutoCAD's <c>PolyMeshType</c>: group 75 where flag 4 of group 70 says it
        /// is smoothed, <see cref="DxfPolyMeshType.SimpleMesh"/> where it is not.
        /// </summary>
        public DxfPolyMeshType PolyMeshType => (GetInt(70, 0) & 4) == 0 ? DxfPolyMeshType.SimpleMesh : (DxfPolyMeshType)GetInt(75, 0);

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
