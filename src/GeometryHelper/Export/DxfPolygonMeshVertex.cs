using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A VERTEX of a <see cref="DxfPolygonMesh"/> (flag 64 of its group 70), as AutoCAD's <c>PolygonMeshVertex</c>: a point of
    /// its grid, row after row.
    /// </summary>
    /// <remarks>
    /// <c>new DxfPolygonMeshVertex(new GeoPoint3(0, 1.5, 0.84))</c> makes one in code, to be added to a mesh by
    /// <see cref="DxfPolygonMesh.AppendVertex(DxfPolygonMeshVertex)"/>.
    /// </remarks>
    public sealed class DxfPolygonMeshVertex : DxfVertex
    {
        // The groups of AcDbVertex and AcDbPolygonMeshVertex in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 10, 20, 30, 70 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>Initializes a point of a mesh at the origin, as AutoCAD's <c>new PolygonMeshVertex()</c>, in no mesh.</summary>
        public DxfPolygonMeshVertex()
            : this(Origin)
        {
        }

        /// <summary>Initializes a point of a mesh, as AutoCAD's <c>new PolygonMeshVertex(point)</c>, in no mesh until it is appended to one.</summary>
        /// <param name="position">The point, in the world.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfPolygonMeshVertex(GeoPoint3 position)
            : base(Groups(position))
        {
        }

        private DxfPolygonMeshVertex(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the point (groups 10, 20, 30), in the world, as AutoCAD's <c>Position</c>.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a vertex that is erased.</exception>
        public GeoPoint3 Position
        {
            get => DxfFollowers.Point(OwnGroups(), 10, Origin);
            set => SetGroups(DxfFollowers.PointChanges(OwnGroups(), 10, value, Origin, 3));
        }

        /// <summary>
        /// Gets what the point is to its mesh, as AutoCAD's <c>VertexType</c>: a point of the grid a smoothed mesh is the frame of
        /// (flag 16 of group 70), one the smoothing made (8), or one of its own.
        /// </summary>
        public DxfVertex3dType VertexType
        {
            get
            {
                int flags = DxfFollowers.Whole(OwnGroups(), 70, 0);
                return (flags & 16) != 0 ? DxfVertex3dType.ControlVertex : (flags & 8) != 0 ? DxfVertex3dType.FitVertex : DxfVertex3dType.SimpleVertex;
            }
        }

        /// <summary>Sets the flags of the point (group 70), as the smoothing of its mesh marks the points it makes.</summary>
        internal void SetFlags(int flags) => SetGroups(DxfFollowers.WholeChange(OwnGroups(), 70, flags, 0));

        /// <summary>Moves the point, as AutoCAD's <c>TransformBy</c>, in the world.</summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the vertex is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (!DxfOcsTransform.IsIdentity(transform))
            {
                Position = transform.Transform(Position);
            }
        }

        [DxfType("VERTEX")]
        internal static DxfEntity Create(DxfItem item)
            => item.Parent != null && (ParentFlags(item) & 16) != 0 && (ParentFlags(item) & 64) == 0 ? new DxfPolygonMeshVertex(item) : null;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 position)
        {
            if (!position.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + position + " is not.", nameof(position));
            }

            return new[]
            {
                new DxfTypedValue(100, "AcDbVertex"),
                new DxfTypedValue(100, "AcDbPolygonMeshVertex"),
                new DxfTypedValue(10, Numbers.Exact(position.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(position.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(position.Z + 0.0)),
                new DxfTypedValue(70, "64"),
            };
        }
    }
}
