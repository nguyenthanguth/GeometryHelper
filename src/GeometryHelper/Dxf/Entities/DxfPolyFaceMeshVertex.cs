using System;
using System.Collections.Generic;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A VERTEX of a <see cref="DxfPolyFaceMesh"/> that is a point of it (flags 64 and 128 of its group 70), as AutoCAD's
    /// <c>PolyFaceMeshVertex</c>: a corner its <see cref="DxfFaceRecord"/> records name by number, from 1.
    /// </summary>
    /// <remarks>
    /// <c>new DxfPolyFaceMeshVertex(new GeoPoint3(0, 4, 0))</c> makes one in code, to be added to a mesh by
    /// <see cref="DxfPolyFaceMesh.AppendVertex(DxfPolyFaceMeshVertex)"/>, after its other points and before its faces.
    /// </remarks>
    public sealed class DxfPolyFaceMeshVertex : DxfVertex
    {
        // The groups of AcDbVertex and AcDbPolyFaceMeshVertex in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 10, 20, 30, 70 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>Initializes a point of a mesh at the origin, as AutoCAD's <c>new PolyFaceMeshVertex()</c>, in no mesh.</summary>
        public DxfPolyFaceMeshVertex()
            : this(Origin)
        {
        }

        /// <summary>Initializes a point of a mesh, as AutoCAD's <c>new PolyFaceMeshVertex(point)</c>, in no mesh until it is appended to one.</summary>
        /// <param name="position">The point, in the world.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfPolyFaceMeshVertex(GeoPoint3 position)
            : base(Groups(position))
        {
        }

        private DxfPolyFaceMeshVertex(DxfItem item)
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
            => item.Parent != null && (ParentFlags(item) & 64) != 0 && (DxfPolyline2d.Flags(item) & 64) != 0 ? new DxfPolyFaceMeshVertex(item) : null;

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
                new DxfTypedValue(100, "AcDbPolyFaceMeshVertex"),
                new DxfTypedValue(10, Numbers.Exact(position.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(position.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(position.Z + 0.0)),
                new DxfTypedValue(70, "192"),
            };
        }
    }
}
