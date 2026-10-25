using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A VERTEX of a <see cref="DxfPolyline2d"/>, as AutoCAD's <c>Vertex2d</c>: a point of the polyline in the system of its
    /// normal, with the bulge and the widths of the segment that leaves it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Position"/> is in the system of the polyline's normal, as AutoCAD gives it, its height the polyline's
    /// elevation; <see cref="DxfPolyline2d.VertexPosition(DxfVertex2d)"/> gives it in the world. A vertex of (12, 0) with a
    /// bulge of 0.3 starts an arc that sweeps 4 atan 0.3, 66.8 degrees, to the next vertex.
    /// </para>
    /// <para>
    /// <c>new DxfVertex2d(new GeoPoint3(5, 0, 0), 0.5, 0, 0, 0)</c> makes one in code, to be added to a polyline by
    /// <see cref="DxfPolyline2d.AppendVertex(DxfVertex2d)"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfVertex2d : DxfVertex
    {
        // The groups of AcDbVertex and AcDb2dVertex in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 10, 20, 30, 40, 41, 42, 70, 50, 71, 72, 73, 74, 91 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>Initializes a vertex at the origin with no bulge and no width, as AutoCAD's <c>new Vertex2d()</c>, in no polyline.</summary>
        public DxfVertex2d()
            : this(Origin, 0.0, 0.0, 0.0, 0.0)
        {
        }

        /// <summary>
        /// Initializes a vertex, as AutoCAD's <c>new Vertex2d(position, bulge, startWidth, endWidth, tangent)</c>, in no polyline
        /// until it is appended to one.
        /// </summary>
        /// <param name="position">The point, in the system of the polyline's normal.</param>
        /// <param name="bulge">The bulge of the segment that leaves it.</param>
        /// <param name="startWidth">The width where that segment starts.</param>
        /// <param name="endWidth">The width where it ends.</param>
        /// <param name="tangent">The direction the curve fitting takes at it, in radians; not used where <see cref="TangentUsed"/> is false.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is not finite, or a width is below nought.</exception>
        public DxfVertex2d(GeoPoint3 position, double bulge, double startWidth, double endWidth, double tangent)
            : base(Groups(position, bulge, startWidth, endWidth, tangent))
        {
        }

        private DxfVertex2d(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the point of the vertex (groups 10 and 20), in the system of the polyline's normal, as AutoCAD's
        /// <c>Position</c>: its height the polyline's elevation, or for a vertex in no polyline its own group 30.
        /// </summary>
        /// <value>Set, the point; of a vertex of a polyline its height is not written, the polyline's elevation being its height.</value>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a vertex that is erased.</exception>
        public GeoPoint3 Position
        {
            get
            {
                GeoPoint3 point = DxfFollowers.Point(OwnGroups(), 10, Origin);
                return Polyline is DxfPolyline2d polyline ? new GeoPoint3(point.X, point.Y, polyline.Elevation) : point;
            }

            set => SetGroups(DxfFollowers.PointChanges(OwnGroups(), 10, value, Origin, Polyline == null ? 3 : 2));
        }

        /// <summary>Gets or sets the bulge of the segment that leaves the vertex (group 42), as AutoCAD's <c>Bulge</c>; nought where it has none.</summary>
        /// <value>Set, a finite number; nought takes the group out.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the bulge set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a vertex that is erased.</exception>
        public double Bulge
        {
            get => DxfFollowers.Number(OwnGroups(), 42, 0.0);
            set => SetGroups(DxfFollowers.NumberChange(OwnGroups(), 42, value, 0.0, true));
        }

        /// <summary>Gets or sets the width where the segment that leaves the vertex starts (group 40), as AutoCAD's <c>StartWidth</c>; nought where it has none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a vertex that is erased.</exception>
        public double StartWidth
        {
            get => DxfFollowers.Number(OwnGroups(), 40, 0.0);

            set
            {
                Guard.NonNegative(value, nameof(value), "A width has to be a finite number, nought or more.");
                SetGroups(DxfFollowers.NumberChange(OwnGroups(), 40, value, 0.0, true));
            }
        }

        /// <summary>Gets or sets the width where the segment that leaves the vertex ends (group 41), as AutoCAD's <c>EndWidth</c>; nought where it has none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a vertex that is erased.</exception>
        public double EndWidth
        {
            get => DxfFollowers.Number(OwnGroups(), 41, 0.0);

            set
            {
                Guard.NonNegative(value, nameof(value), "A width has to be a finite number, nought or more.");
                SetGroups(DxfFollowers.NumberChange(OwnGroups(), 41, value, 0.0, true));
            }
        }

        /// <summary>
        /// Gets or sets the direction the curve fitting takes at the vertex (group 50, in degrees), as AutoCAD's <c>Tangent</c>:
        /// in radians; nought where it has none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the direction set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a vertex that is erased.</exception>
        public double Tangent
        {
            get => DxfFollowers.Number(OwnGroups(), 50, 0.0) * Math.PI / 180.0;

            set
            {
                Guard.Finite(value, nameof(value), "A direction has to be a finite number.");

                if (!DxfFollowers.Same(value, Tangent))
                {
                    double degrees = value * 180.0 / Math.PI;
                    SetGroups(new KeyValuePair<int, string>(50, degrees == 0.0 ? null : Numbers.Exact(degrees + 0.0)));
                }
            }
        }

        /// <summary>Gets or sets whether the curve fitting takes <see cref="Tangent"/> at the vertex (flag 2 of group 70), as AutoCAD's <c>TangentUsed</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a vertex that is erased.</exception>
        public bool TangentUsed
        {
            get => (DxfFollowers.Whole(OwnGroups(), 70, 0) & 2) != 0;

            set
            {
                IReadOnlyList<DxfTypedValue> groups = OwnGroups();
                int flags = DxfFollowers.Whole(groups, 70, 0);
                SetGroups(DxfFollowers.WholeChange(groups, 70, value ? flags | 2 : flags & ~2, 0));
            }
        }

        /// <summary>
        /// Gets what the vertex is to its polyline, as AutoCAD's <c>VertexType</c>: a vertex of the frame of a spline (flag 16),
        /// one the spline fitting made (8), one the curve fitting made (1), or one of its own.
        /// </summary>
        public DxfVertex2dType VertexType
        {
            get
            {
                int flags = DxfFollowers.Whole(OwnGroups(), 70, 0);
                return (flags & 16) != 0 ? DxfVertex2dType.SplineControlVertex
                    : (flags & 8) != 0 ? DxfVertex2dType.SplineFitVertex
                    : (flags & 1) != 0 ? DxfVertex2dType.CurveFitVertex
                    : DxfVertex2dType.SimpleVertex;
            }
        }

        /// <summary>
        /// Moves the vertex, as AutoCAD's <c>TransformBy</c>: its point taken to the world through the polyline's normal and
        /// elevation, moved, and taken back into the polyline's plane, its height left to the polyline; a vertex in no polyline
        /// is moved as a point of the world.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the vertex is erased.</exception>
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

            if (Polyline is DxfPolyline2d polyline)
            {
                GeoVector3 normal = polyline.Normal;
                GeoPoint3 moved = transform.Transform(OcsToWorld(normal, Position));
                Position = WorldToOcs(normal, moved);
                return;
            }

            Position = transform.Transform(Position);
        }

        /// <summary>Puts a vertex made in code at the height of the polyline it is to be appended to, as AutoCAD keeps a vertex in the plane of its polyline.</summary>
        internal void AtElevation(double elevation)
        {
            GeoPoint3 at = DxfFollowers.Point(OwnGroups(), 10, Origin);
            SetGroups(DxfFollowers.PointChanges(OwnGroups(), 10, new GeoPoint3(at.X, at.Y, elevation), Origin, 3));
        }

        [DxfType("VERTEX")]
        internal static DxfEntity Create(DxfItem item)
            => item.Parent != null && (ParentFlags(item) & (8 | 16 | 64)) == 0 ? new DxfVertex2d(item) : null;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 position, double bulge, double startWidth, double endWidth, double tangent)
        {
            if (!position.IsValid)
            {
                throw new ArgumentException("A vertex has to be of finite numbers: " + position + " is not.", nameof(position));
            }

            Guard.Finite(bulge, nameof(bulge), "A bulge has to be a finite number.");
            Guard.NonNegative(startWidth, nameof(startWidth), "A width has to be a finite number, nought or more.");
            Guard.NonNegative(endWidth, nameof(endWidth), "A width has to be a finite number, nought or more.");
            Guard.Finite(tangent, nameof(tangent), "A direction has to be a finite number.");
            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDbVertex"),
                new DxfTypedValue(100, "AcDb2dVertex"),
                new DxfTypedValue(10, Numbers.Exact(position.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(position.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(position.Z + 0.0)),
            };

            if (startWidth != 0.0)
            {
                groups.Add(new DxfTypedValue(40, Numbers.Exact(startWidth + 0.0)));
            }

            if (endWidth != 0.0)
            {
                groups.Add(new DxfTypedValue(41, Numbers.Exact(endWidth + 0.0)));
            }

            if (bulge != 0.0)
            {
                groups.Add(new DxfTypedValue(42, Numbers.Exact(bulge + 0.0)));
            }

            groups.Add(new DxfTypedValue(70, "0"));

            if (tangent != 0.0)
            {
                groups.Add(new DxfTypedValue(50, Numbers.Exact(tangent * 180.0 / Math.PI + 0.0)));
            }

            return groups;
        }
    }
}
