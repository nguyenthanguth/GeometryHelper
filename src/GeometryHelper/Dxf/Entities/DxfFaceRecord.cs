using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A VERTEX of a <see cref="DxfPolyFaceMesh"/> that is a face of it (flag 128 of its group 70 alone), as AutoCAD's
    /// <c>FaceRecord</c>: the numbers of its three or four corners among the mesh's points, from 1, in groups 71 to 74.
    /// </summary>
    /// <remarks>
    /// A corner's number is negative where the edge that starts at it is hidden: the face 1, 2, -3, 4 hides its edge from the
    /// third corner to the fourth. A number of nought is no corner, as the fourth of a triangle. <c>new DxfFaceRecord(1, 2, 3,
    /// 0)</c> makes a triangle in code, to be added to a mesh by <see cref="DxfPolyFaceMesh.AppendFaceRecord(DxfFaceRecord)"/>.
    /// </remarks>
    public sealed class DxfFaceRecord : DxfVertex
    {
        // The groups of AcDbFaceRecord in the order AutoCAD writes them: its marker alone, no AcDbVertex before it, as AutoCAD
        // refuses a face record with one ("Class separator for class AcDbFaceRecord expected").
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 10, 20, 30, 70, 71, 72, 73, 74 });

        /// <summary>Initializes a face of no corners, as AutoCAD's <c>new FaceRecord()</c>, in no mesh.</summary>
        public DxfFaceRecord()
            : this(0, 0, 0, 0)
        {
        }

        /// <summary>
        /// Initializes a face of the points of its numbers, as AutoCAD's <c>new FaceRecord(vtx0, vtx1, vtx2, vtx3)</c>, in no mesh
        /// until it is appended to one.
        /// </summary>
        /// <param name="vtx0">The number of the first corner among the mesh's points, from 1; negative where the edge from it is hidden.</param>
        /// <param name="vtx1">The number of the second corner.</param>
        /// <param name="vtx2">The number of the third corner.</param>
        /// <param name="vtx3">The number of the fourth corner; nought for a triangle.</param>
        public DxfFaceRecord(short vtx0, short vtx1, short vtx2, short vtx3)
            : base(Groups(vtx0, vtx1, vtx2, vtx3))
        {
        }

        private DxfFaceRecord(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets the number of a corner among the mesh's points (group 71 + index), from 1, as AutoCAD's <c>GetVertexAt</c>: its
        /// size, whether its edge is drawn or not; nought where the face has no such corner.
        /// </summary>
        /// <param name="index">The corner, from 0 to 3.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        public short GetVertexAt(short index) => (short)Math.Abs(Number(index));

        /// <summary>Sets the number of a corner among the mesh's points, as AutoCAD's <c>SetVertexAt</c>: whether its edge is drawn is kept.</summary>
        /// <param name="index">The corner, from 0 to 3.</param>
        /// <param name="vertexIndex">The number, from 1; nought for no corner, which takes the group out.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3, or the number is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the face is erased.</exception>
        public void SetVertexAt(short index, short vertexIndex)
        {
            if (vertexIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexIndex), vertexIndex, "A corner's number is from 1, or nought for none.");
            }

            int now = Number(index);
            Write(index, now < 0 ? -vertexIndex : vertexIndex);
        }

        /// <summary>Gets whether the edge that starts at a corner is drawn, as AutoCAD's <c>IsEdgeVisibleAt</c>: its number is not below nought.</summary>
        /// <param name="index">The corner, from 0 to 3.</param>
        /// <returns>Whether it is drawn.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        public bool IsEdgeVisibleAt(short index) => Number(index) >= 0;

        /// <summary>Draws the edge that starts at a corner, as AutoCAD's <c>MakeEdgeVisibleAt</c>: its number made above nought.</summary>
        /// <param name="index">The corner, from 0 to 3.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the face is erased.</exception>
        public void MakeEdgeVisibleAt(short index) => Write(index, Math.Abs(Number(index)));

        /// <summary>Hides the edge that starts at a corner, as AutoCAD's <c>MakeEdgeInvisibleAt</c>: its number made below nought.</summary>
        /// <param name="index">The corner, from 0 to 3.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the face is erased.</exception>
        public void MakeEdgeInvisibleAt(short index) => Write(index, -Math.Abs(Number(index)));

        /// <summary>Does nothing, as ezdxf does: a face has numbers of points, no geometry of its own; the mesh's points are moved.</summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }
        }

        [DxfType("VERTEX")]
        internal static DxfEntity Create(DxfItem item)
            => item.Parent != null && (ParentFlags(item) & 64) != 0 && (DxfPolyline2d.Flags(item) & 64) == 0 ? new DxfFaceRecord(item) : null;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        private int Number(short index)
        {
            if (index < 0 || index > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A face has the corners 0 to 3.");
            }

            return DxfFollowers.Whole(OwnGroups(), 71 + index, 0);
        }

        private void Write(short index, int value)
        {
            if (value != Number(index))
            {
                SetGroups(new KeyValuePair<int, string>(71 + index, value == 0 ? null : value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static IEnumerable<DxfTypedValue> Groups(short vtx0, short vtx1, short vtx2, short vtx3)
        {
            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDbFaceRecord"),
                new DxfTypedValue(10, "0"),
                new DxfTypedValue(20, "0"),
                new DxfTypedValue(30, "0"),
                new DxfTypedValue(70, "128"),
            };

            short[] corners = { vtx0, vtx1, vtx2, vtx3 };

            for (int i = 0; i < 4; i++)
            {
                if (corners[i] != 0)
                {
                    groups.Add(new DxfTypedValue(71 + i, corners[i].ToString(CultureInfo.InvariantCulture)));
                }
            }

            return groups;
        }
    }
}
