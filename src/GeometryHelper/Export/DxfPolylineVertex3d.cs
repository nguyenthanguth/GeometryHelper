using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A VERTEX of a <see cref="DxfPolyline3d"/>, as AutoCAD's <c>PolylineVertex3d</c>: a point of the polyline in the world.
    /// </summary>
    /// <remarks>
    /// The point is in groups 10, 20 and 30, and the vertex is flagged 32 in its group 70, as a vertex of a 3D polyline is.
    /// <c>new DxfPolylineVertex3d(new GeoPoint3(5, 1, 2))</c> makes one in code, to be added to a polyline by
    /// <see cref="DxfPolyline3d.AppendVertex(DxfPolylineVertex3d)"/>.
    /// </remarks>
    public sealed class DxfPolylineVertex3d : DxfVertex
    {
        // The groups of AcDbVertex and AcDb3dPolylineVertex in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 10, 20, 30, 70 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>Initializes a vertex at the origin, as AutoCAD's <c>new PolylineVertex3d()</c>, in no polyline.</summary>
        public DxfPolylineVertex3d()
            : this(Origin)
        {
        }

        /// <summary>Initializes a vertex at a point, as AutoCAD's <c>new PolylineVertex3d(point)</c>, in no polyline until it is appended to one.</summary>
        /// <param name="position">The point, in the world.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfPolylineVertex3d(GeoPoint3 position)
            : base(Groups(position))
        {
        }

        private DxfPolylineVertex3d(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the point of the vertex (groups 10, 20, 30), in the world, as AutoCAD's <c>Position</c>.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a vertex that is erased.</exception>
        public GeoPoint3 Position
        {
            get => DxfFollowers.Point(OwnGroups(), 10, Origin);
            set => SetGroups(DxfFollowers.PointChanges(OwnGroups(), 10, value, Origin, 3));
        }

        /// <summary>
        /// Gets what the vertex is to its polyline, as AutoCAD's <c>VertexType</c>: a vertex of the frame of a spline (flag 16 of
        /// group 70), one the spline fitting made (8), or one of its own.
        /// </summary>
        public DxfVertex3dType VertexType
        {
            get
            {
                int flags = DxfFollowers.Whole(OwnGroups(), 70, 0);
                return (flags & 16) != 0 ? DxfVertex3dType.ControlVertex : (flags & 8) != 0 ? DxfVertex3dType.FitVertex : DxfVertex3dType.SimpleVertex;
            }
        }

        /// <summary>Moves the vertex, as AutoCAD's <c>TransformBy</c>: its point moved in the world.</summary>
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

        /// <summary>Sets the flags of the vertex (group 70), as the fitting of its polyline marks the vertices it makes.</summary>
        internal void SetFlags(int flags) => SetGroups(DxfFollowers.WholeChange(OwnGroups(), 70, flags, 0));

        [DxfType("VERTEX")]
        internal static DxfEntity Create(DxfItem item)
            => item.Parent != null && (ParentFlags(item) & 8) != 0 && (ParentFlags(item) & (16 | 64)) == 0 ? new DxfPolylineVertex3d(item) : null;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 position)
        {
            if (!position.IsValid)
            {
                throw new ArgumentException("A vertex has to be of finite numbers: " + position + " is not.", nameof(position));
            }

            return new[]
            {
                new DxfTypedValue(100, "AcDbVertex"),
                new DxfTypedValue(100, "AcDb3dPolylineVertex"),
                new DxfTypedValue(10, Numbers.Exact(position.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(position.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(position.Z + 0.0)),
                new DxfTypedValue(70, "32"),
            };
        }
    }
}
