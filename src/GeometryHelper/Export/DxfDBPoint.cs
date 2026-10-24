using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A POINT of a DXF file, as AutoCAD's <c>DBPoint</c>: a point at <see cref="Position"/>, in world coordinates, with the
    /// <see cref="Normal"/> its <see cref="Thickness"/> stands up along.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The point at (3, 4, 5) is drawn as the header's <see cref="DxfDatabase.Pdmode"/> and <see cref="DxfDatabase.Pdsize"/>
    /// say; with a thickness of 2 it is a line 2 long standing up from there along its normal. The file holds the position
    /// in world coordinates (groups 10, 20, 30), as AutoCAD gives it, and the turn of the point's symbol about its normal in
    /// degrees (group 50), which <see cref="EcsRotation"/> gives in radians.
    /// </para>
    /// <para>
    /// Every other group of the record is kept as it was; <c>new DxfDBPoint(position) { Layer = "marks" }</c> makes one in
    /// code, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfDBPoint : DxfEntity
    {
        // The groups of AcDbPoint in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 10, 20, 30, 39, 210, 220, 230, 50 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>Initializes a point at the origin, as AutoCAD's <c>new DBPoint()</c>, on layer 0 in no file.</summary>
        public DxfDBPoint()
            : this(Origin)
        {
        }

        /// <summary>
        /// Initializes a point, as AutoCAD's <c>new DBPoint(position)</c>, on layer 0 with no look of its own, in no file until
        /// it is added to one.
        /// </summary>
        /// <param name="position">Where it is, in world coordinates.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfDBPoint(GeoPoint3 position)
            : base("POINT", Groups(position))
        {
        }

        private DxfDBPoint(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets where the point is (groups 10, 20, 30), in world coordinates, as AutoCAD's <c>Position</c>; the origin where the record gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the point is erased.</exception>
        public GeoPoint3 Position
        {
            get => GetPoint(10, Origin);
            set => SetPoint(10, value, Origin);
        }

        /// <summary>
        /// Gets or sets the normal of the point (groups 210, 220, 230), the direction its <see cref="Thickness"/> stands up
        /// along, of unit length; (0, 0, 1) where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the point is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();
            set => SetNormal(value);
        }

        /// <summary>Gets or sets how far the point stands up along its <see cref="Normal"/> (group 39); nought where the record gives none.</summary>
        /// <value>Set, a finite number; nought takes the group out.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the point is erased.</exception>
        public double Thickness
        {
            get => GetThickness();
            set => SetThickness(value);
        }

        /// <summary>
        /// Gets or sets the turn of the point's symbol about its normal, as AutoCAD's <c>EcsRotation</c>: in radians, from the
        /// degrees of group 50; nought where the record gives none. A point with 90 in its group 50 is pi / 2.
        /// </summary>
        /// <value>Set, a finite number of radians, written in degrees.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the point is erased.</exception>
        public double EcsRotation
        {
            get => GetDouble(50, 0.0) * Math.PI / 180.0;

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (!Same(value, EcsRotation))
                {
                    SetDouble(50, value * 180.0 / Math.PI, 0.0);
                }
            }
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the point, as AutoCAD's <c>TransformBy</c> does: its position is moved, and its
        /// normal and thickness are carried as ezdxf carries them, as a <see cref="DxfLine"/>'s are; the turn of its symbol is
        /// kept, as ezdxf keeps it. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation takes the normal to no length.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the point is erased.</exception>
        public override void TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoPoint3 position = transform.Transform(Position);
            double thickness = Thickness;
            GeoVector3 raised = transform.Transform(Normal.Multiply(thickness != 0.0 ? thickness : 1.0));
            double length = raised.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("The transformation takes the normal of the point to no direction.", nameof(transform));
            }

            SetGroups(Join(PointGroups(10, position, Origin), NormalGroups(raised.Divide(length))));

            if (thickness != 0.0)
            {
                Thickness = thickness < 0.0 ? -length : length;
            }
        }

        [DxfType("POINT")]
        internal static DxfEntity Create(DxfItem item) => new DxfDBPoint(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The groups of a point made in code: its marker and its position.</summary>
        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 position)
        {
            if (!position.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + position + " is not.", nameof(position));
            }

            return new[]
            {
                new DxfTypedValue(100, "AcDbPoint"),
                new DxfTypedValue(10, Numbers.Exact(position.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(position.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(position.Z + 0.0)),
            };
        }
    }
}
