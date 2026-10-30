using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
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

        // The own groups of the VERTEX records last read, by the bytes they stand in.
        private Dictionary<RecordKey, IReadOnlyList<DxfTypedValue>> _read;

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

            // After the last point: found from the end back over the faces, which follow the points, so that points appended one
            // by one each cost one record read, not the whole mesh.
            int at = 1;

            for (int record = RecordCount - 1; record >= 1; record--)
            {
                if (RecordType(record) == "VERTEX" && (DxfFollowers.Whole(GetOwnGroups(record), 70, 0) & 64) != 0)
                {
                    at = record + 1;
                    break;
                }
            }

            InsertFollowerAt(at, vertexToAppend);
            Count(71, NumVertices + 1);
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
            Count(72, NumFaces + 1);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the mesh, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: each point moved in
        /// the world, in one writing of the mesh, each record in place; the faces, which name points by number, kept.
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

            SetGroupsAtMany(DxfFollowers.Moved(Item, transform, v => (DxfFollowers.Whole(v, 70, 0) & 64) != 0), VertexOrder);
        }

        /// <summary>
        /// Gets the faces the mesh breaks into, as AutoCAD's <c>Explode</c>, as ezdxf's <c>virtual_entities</c> gives them: a
        /// 3DFACE for each face, in the order of the file, its corners the points its numbers name, the edge from a corner whose
        /// number is below nought hidden. A face of three corners ends on its third, as AutoCAD writes one, and the edge from its
        /// third back to its first, the 3DFACE's fourth, is hidden with its third. Each has the mesh's look and the layer its face
        /// writes of its own; a colour of the face's own is not taken, as AutoCAD 2021's <c>Explode</c> gives the mesh's. The pyramid of a square base and four sides
        /// gives five faces, the base of four corners and four triangles. Its corners are its numbers up to the first of nought,
        /// as the reader takes them: a face of fewer than three corners so, (1, 2, 0, 4) among them, or one naming a point the
        /// mesh has not, is left out, as the reader leaves it out of the mesh's body.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            var look = new Look(this);
            var origin = new GeoPoint3(0.0, 0.0, 0.0);
            var points = new List<GeoPoint3>();
            var faces = new List<DxfEntity>();
            List<KeyValuePair<IReadOnlyList<DxfTypedValue>, IReadOnlyList<DxfTypedValue>>> records = DxfFollowers.WholeVertices(Item);

            // The points come before the faces, as AutoCAD writes them; a face names them by their number among the points.
            foreach (KeyValuePair<IReadOnlyList<DxfTypedValue>, IReadOnlyList<DxfTypedValue>> record in records)
            {
                if ((DxfFollowers.Whole(record.Key, 70, 0) & 64) != 0)
                {
                    points.Add(DxfFollowers.Point(record.Key, 10, origin));
                }
            }

            foreach (KeyValuePair<IReadOnlyList<DxfTypedValue>, IReadOnlyList<DxfTypedValue>> record in records)
            {
                int flags = DxfFollowers.Whole(record.Key, 70, 0);

                if ((flags & 128) == 0 || (flags & 64) != 0)
                {
                    continue;
                }

                var corners = new List<GeoPoint3>(4);
                int hidden = 0;

                for (int code = 71; code <= 74; code++)
                {
                    int number = DxfFollowers.Whole(record.Key, code, 0);

                    if (number == 0)
                    {
                        break;
                    }

                    int index = number == int.MinValue ? int.MaxValue : Math.Abs(number);

                    if (index > points.Count)
                    {
                        corners.Clear();
                        break;
                    }

                    corners.Add(points[index - 1]);
                    hidden |= number < 0 ? 1 << (code - 71) : 0;
                }

                if (corners.Count < 3)
                {
                    continue;
                }

                if (corners.Count == 3)
                {
                    corners.Add(corners[2]);
                    hidden |= (hidden & 4) << 1;
                }

                var face = new DxfFace(corners[0], corners[1], corners[2], corners[3], (hidden & 1) == 0, (hidden & 2) == 0, (hidden & 4) == 0, (hidden & 8) == 0);
                look.GiveTo(face);
                OwnLook(face, record.Value);
                faces.Add(face);
            }

            return faces.AsReadOnly();
        }

        /// <summary>
        /// Gives a face exploded the layer its face record writes of its own (8); its colour stays the mesh's, as AutoCAD 2021's
        /// <c>Explode</c> gives a face of colour 1 of its own the mesh's BYLAYER.
        /// </summary>
        private static void OwnLook(DxfFace face, IReadOnlyList<DxfTypedValue> record)
        {
            string layer = DxfFollowers.Text(record, 8);
            string name = layer == null ? string.Empty : DxfReader.NameOf(layer);

            if (name.Length > 0)
            {
                face.Layer = name;
            }
        }

        /// <summary>
        /// Takes a point or a face of the mesh out, as AutoCAD's <c>Erase</c> of one, the mesh kept true: a face with the count of
        /// the faces (group 72) one less; a point that no face names with the count of the points (71) one less and every
        /// face's number of a later point one less, the edge it starts hidden or not as it was. A point a face names is kept.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a face names the point.</exception>
        internal void Remove(DxfVertex vertex)
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> records = Vertices();
            int number = 0;
            bool point = false;

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> record in records)
            {
                bool isPoint = (DxfFollowers.Whole(record.Value, 70, 0) & 64) != 0;
                number += isPoint ? 1 : 0;

                if (record.Key == vertex.Record)
                {
                    point = isPoint;
                    break;
                }
            }

            if (!point)
            {
                vertex.EraseRecord();
                Count(72, Math.Max(0, NumFaces - 1));
                return;
            }

            var renumbered = new List<KeyValuePair<int, IList<KeyValuePair<int, string>>>>();

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> record in records)
            {
                if ((DxfFollowers.Whole(record.Value, 70, 0) & 64) != 0)
                {
                    continue;
                }

                var changes = new List<KeyValuePair<int, string>>();

                for (int code = 71; code <= 74; code++)
                {
                    int corner = DxfFollowers.Whole(record.Value, code, 0);

                    if (Math.Abs(corner) == number)
                    {
                        throw new InvalidOperationException("The point " + number.ToString(CultureInfo.InvariantCulture) + " of the mesh is a corner of a face: erase the face first.");
                    }

                    if (Math.Abs(corner) > number)
                    {
                        changes.AddRange(DxfFollowers.WholeChange(record.Value, code, corner > 0 ? corner - 1 : corner + 1, 0));
                    }
                }

                if (changes.Count > 0)
                {
                    renumbered.Add(new KeyValuePair<int, IList<KeyValuePair<int, string>>>(record.Key, changes.ToArray()));
                }
            }

            // The faces name the same points by their new numbers: the mesh's shape is as it was, so the extents are not
            // grown by it, which would read all its faces at each point erased.
            Item.File.SetValuesAtMany(Item, renumbered, VertexOrder, false);
            vertex.EraseRecord();
            Count(71, Math.Max(0, NumVertices - 1));
        }

        /// <summary>
        /// The VERTEX records of the mesh, each its index among the records and its own groups, as
        /// <see cref="DxfFollowers.Vertices"/> gives them: a record whose bytes are those read before is not read again, so that
        /// points erased one at a time read only the faces renumbered since, not all 20 000 points each time.
        /// </summary>
        private List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> Vertices()
        {
            List<DxfFile.Rec> all = Item.File.RecordsOf(Item);
            var read = new Dictionary<RecordKey, IReadOnlyList<DxfTypedValue>>(all.Count, RecordKey.Comparer);
            var vertices = new List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>>(all.Count);

            for (int r = 1; r < all.Count; r++)
            {
                if (all[r].Type != "VERTEX")
                {
                    continue;
                }

                var key = new RecordKey(all[r].Bytes, all[r].Start);

                if (_read == null || !_read.TryGetValue(key, out IReadOnlyList<DxfTypedValue> groups))
                {
                    groups = GetOwnGroups(r);
                }

                read[key] = groups;
                vertices.Add(new KeyValuePair<int, IReadOnlyList<DxfTypedValue>>(r, groups));
            }

            _read = read;
            return vertices;
        }

        /// <summary>
        /// Writes a count of the mesh's head (71 its points, 72 its faces) with no growing of the extents by it: a count is no
        /// geometry, and growing by the whole mesh at each point appended would read every point each time.
        /// </summary>
        private void Count(int code, int value)
        {
            if (value != GetInt(code, 0))
            {
                Item.File.SetValues(Item, new[] { new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture)) }, GroupOrder, false);
            }
        }

        [DxfType("POLYLINE")]
        internal static DxfEntity Create(DxfItem item) => (DxfPolyline2d.Flags(item) & 64) != 0 ? new DxfPolyFaceMesh(item) : null;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>A record by the bytes it stands in: the array, the same one only while the record is not written anew, and where in it the record begins.</summary>
        private readonly struct RecordKey
        {
            internal static readonly IEqualityComparer<RecordKey> Comparer = new KeyComparer();

            internal RecordKey(byte[] bytes, int start)
            {
                Bytes = bytes;
                Start = start;
            }

            internal byte[] Bytes { get; }

            internal int Start { get; }

            private sealed class KeyComparer : IEqualityComparer<RecordKey>
            {
                public bool Equals(RecordKey x, RecordKey y) => ReferenceEquals(x.Bytes, y.Bytes) && x.Start == y.Start;

                public int GetHashCode(RecordKey key) => (RuntimeHelpers.GetHashCode(key.Bytes) * 397) ^ key.Start;
            }
        }
    }
}
