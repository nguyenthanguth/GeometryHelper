using System;
using System.Collections;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A polyface mesh of a DXF file (a POLYLINE of flag 64 of its group 70), as AutoCAD's <c>PolyFaceMesh</c>: its points
    /// (<see cref="DxfPolyFaceMeshVertex"/>) and its faces (<see cref="DxfFaceRecord"/>), each a VERTEX record, then its SEQEND.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mesh is walked as AutoCAD walks one, <c>foreach (DxfVertex vertex in mesh)</c>: its points, then its faces, in the
    /// order of the file. <see cref="DxfEntity.GetGeometry"/> gives the body <see cref="DxfReader"/> reads of it: the box of 8
    /// points and 6 faces is a <see cref="GeoSolid3"/> of 6 faces.
    /// </para>
    /// <para>
    /// A point or a face set is written in its own record; one appended is a record put in, every other keeping its handle,
    /// and the count of points or faces (group 71 or 72) raised with it.
    /// </para>
    /// </remarks>
    public sealed class DxfPolyFaceMesh : DxfEntity, IEnumerable<DxfVertex>
    {
        // The groups of AcDbPolyFaceMesh in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 66, 10, 20, 30, 70, 71, 72 });

        // The groups of a point in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> VertexOrder = Array.AsReadOnly(new[] { 100, 100, 10, 20, 30, 70 });

        /// <summary>Initializes a mesh of no points and no faces, as AutoCAD's <c>new PolyFaceMesh()</c>, on layer 0 in no file.</summary>
        public DxfPolyFaceMesh()
            : base("POLYLINE", new[]
            {
                new DxfTypedValue(100, "AcDbPolyFaceMesh"),
                new DxfTypedValue(66, "1"),
                new DxfTypedValue(10, "0"),
                new DxfTypedValue(20, "0"),
                new DxfTypedValue(30, "0"),
                new DxfTypedValue(70, "64"),
                new DxfTypedValue(71, "0"),
                new DxfTypedValue(72, "0"),
            })
        {
            SetFollowers(new[] { new KeyValuePair<string, KeyValuePair<int, string>[]>("SEQEND", new KeyValuePair<int, string>[0]) });
        }

        private DxfPolyFaceMesh(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets how many points the mesh has (group 71), as AutoCAD's <c>NumVertices</c>.</summary>
        public int NumVertices => GetInt(71, 0);

        /// <summary>Gets how many faces the mesh has (group 72), as AutoCAD's <c>NumFaces</c>.</summary>
        public int NumFaces => GetInt(72, 0);

        /// <summary>Gets an enumerator over the points and faces in the order of the file, as AutoCAD's <c>GetEnumerator</c>: each the same object while its record is there.</summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<DxfVertex> GetEnumerator()
        {
            var vertices = new List<DxfVertex>();

            for (int record = 1; record < RecordCount; record++)
            {
                if (RecordType(record) == "VERTEX" && FollowerAt(record) is DxfVertex vertex)
                {
                    vertices.Add(vertex);
                }
            }

            return vertices.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Appends a point made in code after the last point, before the faces, as AutoCAD's <c>AppendVertex</c>; group 71 counts it.</summary>
        /// <param name="vertexToAppend">The point, in no mesh.</param>
        /// <exception cref="ArgumentNullException">Thrown when the point is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the point is of a mesh already, or the mesh is erased.</exception>
        public void AppendVertex(DxfPolyFaceMeshVertex vertexToAppend)
        {
            if (vertexToAppend == null)
            {
                throw new ArgumentNullException(nameof(vertexToAppend));
            }

            int at = 1;

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in DxfFollowers.Vertices(Item))
            {
                if ((DxfFollowers.Whole(vertex.Value, 70, 0) & 64) != 0)
                {
                    at = vertex.Key + 1;
                }
            }

            InsertFollowerAt(at, vertexToAppend);
            SetInt(71, NumVertices + 1, 0);
        }

        /// <summary>Appends a face made in code after the last, as AutoCAD's <c>AppendFaceRecord</c>; group 72 counts it.</summary>
        /// <param name="faceRecord">The face, in no mesh.</param>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the face is of a mesh already, or the mesh is erased.</exception>
        public void AppendFaceRecord(DxfFaceRecord faceRecord)
        {
            if (faceRecord == null)
            {
                throw new ArgumentNullException(nameof(faceRecord));
            }

            InsertFollowerAt(RecordCount > 1 && RecordType(RecordCount - 1) == "SEQEND" ? RecordCount - 1 : RecordCount, faceRecord);
            SetInt(72, NumFaces + 1, 0);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the mesh, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: each point moved in
        /// the world, in one writing of the mesh, each record in place; the faces, which name points by number, kept.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the mesh is erased.</exception>
        public override void TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            SetGroupsAtMany(DxfFollowers.Moved(Item, transform, v => (DxfFollowers.Whole(v, 70, 0) & 64) != 0), VertexOrder);
        }

        [DxfType("POLYLINE")]
        internal static DxfEntity Create(DxfItem item) => (DxfPolyline2d.Flags(item) & 64) != 0 ? new DxfPolyFaceMesh(item) : null;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;
    }
}
