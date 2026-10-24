using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A SOLID of a DXF file, as AutoCAD's <c>Solid</c>: a filled area of three or four corners in the plane of its
    /// <see cref="Normal"/>, standing up by its <see cref="Thickness"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The corners are groups 10 to 13 in the system of the normal, in DXF's order: the area runs 0, 1, 3, 2 round, so the
    /// square of side 2 is (0, 0), (2, 0), (0, 2), (2, 2). <see cref="GetPointAt(short)"/> gives a corner in the world, taken
    /// there as <see cref="DxfReader"/> takes it, a corner with no height of its own at that of the first; a triangle repeats its
    /// third corner as its fourth.
    /// </para>
    /// <para>
    /// A TRACE, which AutoCAD reads as a <c>Trace</c> of its own, is not a <see cref="DxfSolid"/>.
    /// <c>new DxfSolid(a, b, c, d)</c> makes one in code, to be added to a drawing of any version.
    /// </para>
    /// </remarks>
    public sealed class DxfSolid : DxfEntity
    {
        // The groups of AcDbTrace in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 33, 39, 210, 220, 230 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>Initializes a solid whose four corners are at the origin, as AutoCAD's <c>new Solid()</c>, on layer 0 in no file; give it its corners with <see cref="SetPointAt(short, GeoPoint3)"/>.</summary>
        public DxfSolid()
            : this(Origin, Origin, Origin, Origin)
        {
        }

        /// <summary>Initializes a triangle, as AutoCAD's <c>new Solid(pointer1, pointer2, pointer3)</c>: the third corner written again as the fourth.</summary>
        /// <param name="pointer1">The first corner, in the plane of the normal (0, 0, 1).</param>
        /// <param name="pointer2">The second corner.</param>
        /// <param name="pointer3">The third corner.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfSolid(GeoPoint3 pointer1, GeoPoint3 pointer2, GeoPoint3 pointer3)
            : this(pointer1, pointer2, pointer3, pointer3)
        {
        }

        /// <summary>
        /// Initializes a solid of four corners, as AutoCAD's <c>new Solid(pointer1, pointer2, pointer3, pointer4)</c>, in DXF's
        /// order (the area runs 1, 2, 4, 3 round), on layer 0 in no file until it is added to one.
        /// </summary>
        /// <param name="pointer1">The first corner, in the plane of the normal (0, 0, 1).</param>
        /// <param name="pointer2">The second corner.</param>
        /// <param name="pointer3">The third corner.</param>
        /// <param name="pointer4">The fourth corner.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfSolid(GeoPoint3 pointer1, GeoPoint3 pointer2, GeoPoint3 pointer3, GeoPoint3 pointer4)
            : base("SOLID", Groups(new[] { pointer1, pointer2, pointer3, pointer4 }))
        {
        }

        private DxfSolid(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets a corner in the world, as AutoCAD's <c>GetPointAt</c>: groups 10 + index, 20 + index and 30 + index in the system
        /// of the normal, the height of the first where the corner gives none, the third where the record gives no fourth.
        /// </summary>
        /// <param name="index">The corner, from 0 to 3.</param>
        /// <returns>The corner.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        public GeoPoint3 GetPointAt(short index)
        {
            Corner(index);
            return OcsToWorld(Normal, Local(index));
        }

        /// <summary>
        /// Moves a corner, given in the world, as AutoCAD's <c>SetPointAt</c>: written in the system of the normal, its height
        /// with it.
        /// </summary>
        /// <param name="index">The corner, from 0 to 3.</param>
        /// <param name="point">Where it is to be, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 0 to 3.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the solid is erased.</exception>
        public void SetPointAt(short index, GeoPoint3 point)
        {
            Corner(index);

            if (!point.IsValid)
            {
                throw new ArgumentException("A corner has to be of finite numbers: " + point + " is not.", nameof(point));
            }

            if (GetPointAt(index).Equals(point))
            {
                return;
            }

            SetPoint(10 + index, WorldToOcs(Normal, point), Fallback(index));
        }

        /// <summary>
        /// Gets or sets the normal of the plane of the solid (groups 210, 220, 230), of unit length; (0, 0, 1) where the record
        /// gives none.
        /// </summary>
        /// <value>
        /// Set, a direction of some length, written made of unit length. The corners keep their places in the world, as
        /// <see cref="GetPointAt(short)"/> and <see cref="SetPointAt(short, GeoPoint3)"/> give and take them: each is written
        /// anew in the system of the new normal, its height with it, to the rounding of that change. The square of side 2 at
        /// the origin given the normal (0, 0, -1) has its corner (2, 0, 0) written as (-2, 0, 0), where it stands.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a solid that is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();

            set
            {
                KeyValuePair<int, string>[] normal = NormalGroups(value);

                if (normal.Length == 0)
                {
                    return;
                }

                double length = value.Length;
                GeoVector3 unit = Math.Abs(length - 1.0) <= 1E-12 ? value : value.Divide(length);
                GeoVector3 old = GetNormal();
                var parts = new List<KeyValuePair<int, string>[]> { normal };

                for (int i = 0; i < 4; i++)
                {
                    if (GetString(10 + i) != null || GetString(20 + i) != null || GetString(30 + i) != null)
                    {
                        parts.Add(PointGroups(10 + i, WorldToOcs(unit, OcsToWorld(old, Local(i))), Origin));
                    }
                }

                SetGroups(Join(parts.ToArray()));
            }
        }

        /// <summary>Gets or sets how far the solid stands up along its <see cref="Normal"/> (group 39); nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a solid that is erased.</exception>
        public double Thickness
        {
            get => GetThickness();
            set => SetThickness(value);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the solid, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: each corner the record
        /// gives taken from the old plane to the world, moved, and into the plane of the new normal, square to where the old
        /// plane's axes go; the thickness scaled with it. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it takes the plane of the solid to no plane.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the solid is erased.</exception>
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

            var ocs = new DxfOcsTransform(Normal, transform);
            var parts = new List<KeyValuePair<int, string>[]>();

            for (int i = 0; i < 4; i++)
            {
                if (GetString(10 + i) != null || GetString(20 + i) != null || GetString(30 + i) != null)
                {
                    parts.Add(PointGroups(10 + i, ocs.Vertex(GetPoint(10 + i, Origin)), Origin));
                }
            }

            if (GetString(39) != null)
            {
                double thickness = ocs.Thickness(GetThickness());
                parts.Add(Same(thickness, GetThickness()) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(39, Numbers.Exact(thickness + 0.0)) });
            }

            parts.Add(NormalGroups(ocs.NewExtrusion));
            SetGroups(Join(parts.ToArray()));
        }

        [DxfType("SOLID")]
        internal static DxfEntity Create(DxfItem item) => new DxfSolid(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>A corner in the system of the normal, as the reader reads it.</summary>
        private GeoPoint3 Local(int index)
        {
            GeoPoint3 first = GetPoint(10, Origin);
            int at = index == 3 && GetString(13) == null && GetString(23) == null && GetString(33) == null ? 2 : index;
            GeoPoint3 p = GetPoint(10 + at, Origin);
            return GetString(30 + at) != null || at == 0 ? p : new GeoPoint3(p.X, p.Y, first.Z);
        }

        /// <summary>What a corner reads as where the record gives none of its groups, for the setter's comparison.</summary>
        private GeoPoint3 Fallback(int index) => index == 3 ? GetPoint(12, Origin) : Origin;

        private static void Corner(short index)
        {
            if (index < 0 || index > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A solid has the corners 0 to 3.");
            }
        }

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3[] corners)
        {
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbTrace") };

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

            return groups;
        }
    }
}
