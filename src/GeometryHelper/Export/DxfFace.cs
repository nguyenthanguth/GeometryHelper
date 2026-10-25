using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A 3DFACE of a DXF file, as AutoCAD's <c>Face</c>: three or four corners in the world, each edge drawn or hidden.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The corners are groups 10 to 13 (with 20 to 23 and 30 to 33), in the world; a face of three corners repeats its third
    /// as its fourth, as AutoCAD's <see cref="GetVertexAt(short)"/> gives it. Edge 0 runs from corner 0 to corner 1, edge 3
    /// from corner 3 back to corner 0; a hidden edge is a flag of group 70, 1 for edge 0 up to 8 for edge 3. The face (0, 0, 0),
    /// (10, 0, 1), (10, 8, 0), (0, 8, 2) with 70 of 5 hides its edges 0 and 2.
    /// </para>
    /// <para>
    /// <c>new DxfFace(a, b, c, true, true, true, true)</c> makes a triangle in code, to be added to a drawing of any version.
    /// </para>
    /// </remarks>
    public sealed class DxfFace : DxfEntity
    {
        // The groups of AcDbFace in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 33, 70 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>Initializes a face whose four corners are at the origin, as AutoCAD's <c>new Face()</c>, on layer 0 in no file; give it its corners with <see cref="SetVertexAt(short, GeoPoint3)"/>.</summary>
        public DxfFace()
            : this(Origin, Origin, Origin, Origin, true, true, true, true)
        {
        }

        /// <summary>
        /// Initializes a face of three corners, as AutoCAD's <c>new Face(pointer1, pointer2, pointer3, value1, value2, value3,
        /// value4)</c>: the third corner written again as the fourth.
        /// </summary>
        /// <param name="pointer1">The first corner, in the world.</param>
        /// <param name="pointer2">The second corner.</param>
        /// <param name="pointer3">The third corner.</param>
        /// <param name="value1">Whether edge 0 is drawn.</param>
        /// <param name="value2">Whether edge 1 is drawn.</param>
        /// <param name="value3">Whether edge 2 is drawn.</param>
        /// <param name="value4">Whether edge 3, of no length on a triangle, is drawn.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfFace(GeoPoint3 pointer1, GeoPoint3 pointer2, GeoPoint3 pointer3, bool value1, bool value2, bool value3, bool value4)
            : this(pointer1, pointer2, pointer3, pointer3, value1, value2, value3, value4)
        {
        }

        /// <summary>
        /// Initializes a face of four corners, as AutoCAD's <c>new Face(pointer1, pointer2, pointer3, pointer4, value1, value2,
        /// value3, value4)</c>, on layer 0 in no file until it is added to one.
        /// </summary>
        /// <param name="pointer1">The first corner, in the world.</param>
        /// <param name="pointer2">The second corner.</param>
        /// <param name="pointer3">The third corner.</param>
        /// <param name="pointer4">The fourth corner.</param>
        /// <param name="value1">Whether edge 0 is drawn.</param>
        /// <param name="value2">Whether edge 1 is drawn.</param>
        /// <param name="value3">Whether edge 2 is drawn.</param>
        /// <param name="value4">Whether edge 3 is drawn.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfFace(GeoPoint3 pointer1, GeoPoint3 pointer2, GeoPoint3 pointer3, GeoPoint3 pointer4, bool value1, bool value2, bool value3, bool value4)
            : base("3DFACE", Groups(new[] { pointer1, pointer2, pointer3, pointer4 }, (value1 ? 0 : 1) | (value2 ? 0 : 2) | (value3 ? 0 : 4) | (value4 ? 0 : 8)))
        {
        }

        private DxfFace(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets a corner of the face in the world (groups 10 + index, 20 + index, 30 + index), as AutoCAD's <c>GetVertexAt</c>:
        /// the third where the record gives no fourth.
        /// </summary>
        /// <param name="index">The corner, from 0 to 3.</param>
        /// <returns>The corner.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        public GeoPoint3 GetVertexAt(short index)
        {
            Corner(index);
            return GetPoint(10 + index, index == 3 ? GetPoint(12, Origin) : Origin);
        }

        /// <summary>Moves a corner of the face, given in the world, as AutoCAD's <c>SetVertexAt</c>.</summary>
        /// <param name="index">The corner, from 0 to 3.</param>
        /// <param name="point">Where it is to be.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the face is erased.</exception>
        public void SetVertexAt(short index, GeoPoint3 point)
        {
            Corner(index);
            SetPoint(10 + index, point, index == 3 ? GetPoint(12, Origin) : Origin);
        }

        /// <summary>Gets whether an edge of the face is drawn, as AutoCAD's <c>IsEdgeVisibleAt</c>: not hidden by its flag of group 70.</summary>
        /// <param name="index">The edge, from 0 to 3: edge 0 runs from corner 0 to corner 1.</param>
        /// <returns>Whether it is drawn.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        public bool IsEdgeVisibleAt(short index)
        {
            Corner(index);
            return (GetInt(70, 0) & (1 << index)) == 0;
        }

        /// <summary>Draws an edge of the face, as AutoCAD's <c>MakeEdgeVisibleAt</c>: its flag of group 70 taken off.</summary>
        /// <param name="index">The edge, from 0 to 3.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the face is erased.</exception>
        public void MakeEdgeVisibleAt(short index)
        {
            Corner(index);
            SetInt(70, GetInt(70, 0) & ~(1 << index), 0);
        }

        /// <summary>Hides an edge of the face, as AutoCAD's <c>MakeEdgeInvisibleAt</c>: its flag of group 70 set.</summary>
        /// <param name="index">The edge, from 0 to 3.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the face is erased.</exception>
        public void MakeEdgeInvisibleAt(short index)
        {
            Corner(index);
            SetInt(70, GetInt(70, 0) | (1 << index), 0);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the face, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: its four corners moved
        /// in the world, a triangle's third written again as its fourth. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the face is erased.</exception>
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

            var parts = new KeyValuePair<int, string>[4][];

            for (short i = 0; i < 4; i++)
            {
                parts[i] = PointGroups(10 + i, transform.Transform(GetVertexAt(i)), i == 3 ? GetPoint(12, Origin) : Origin);
            }

            SetGroups(Join(parts));
        }

        [DxfType("3DFACE")]
        internal static DxfEntity Create(DxfItem item) => new DxfFace(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        private static void Corner(short index)
        {
            if (index < 0 || index > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A face has the corners and edges 0 to 3.");
            }
        }

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3[] corners, int hidden)
        {
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbFace") };

            for (int i = 0; i < corners.Length; i++)
            {
                if (!corners[i].IsValid)
                {
                    throw new ArgumentException("A corner has to be of finite numbers: " + corners[i] + " is not.", "pointer" + (i + 1).ToString(CultureInfo.InvariantCulture));
                }

                groups.Add(new DxfTypedValue(10 + i, Numbers.Exact(corners[i].X + 0.0)));
                groups.Add(new DxfTypedValue(20 + i, Numbers.Exact(corners[i].Y + 0.0)));
                groups.Add(new DxfTypedValue(30 + i, Numbers.Exact(corners[i].Z + 0.0)));
            }

            if (hidden != 0)
            {
                groups.Add(new DxfTypedValue(70, hidden.ToString(CultureInfo.InvariantCulture)));
            }

            return groups;
        }
    }
}
