using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// Where a grid, or strips, stand in space: which way the axes run, as <see cref="MeshAxes"/> says, and, when given, the
    /// point a cell starts at.
    /// <para>
    /// The same placement serves a flat shape, meshed into a <see cref="GeoMesh3"/>, and a body, cut into a
    /// <see cref="GeoCellGrid3"/>: over a flat shape the axes give the direction of the grid's first axis in the shape's
    /// plane, and through a body all three axes of the grid. The placement is immutable, so one instance can be shared
    /// between threads and kept as a setting.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Without an origin the grid stands against the shape as the alignments of the options say. With one, a cell has its
    /// first corner there, as <see cref="MeshOptions.Origin"/> places a grid of the plane: over a flat shape the point is put
    /// onto the shape's plane along its normal, so that faces of one building laid from one origin line up across their
    /// edges. Along a line of space the cells stand where they would with its axis running the world's way, X before Y before
    /// Z, whichever way a face's own runs along it, and so do the joints between them.
    /// </remarks>
    public sealed class MeshPlacement3 : IEquatable<MeshPlacement3>
    {
        private MeshPlacement3(MeshAxes axes, GeoVector3? direction, GeoCoordinateSystem3? coordinateSystem, GeoPoint3? origin)
        {
            Axes = axes;
            Direction = direction;
            CoordinateSystem = coordinateSystem;
            Origin = origin;
        }

        /// <summary>
        /// Along the world's axes: over a flat shape the first axis level and the second up the slope, through a body X, Y
        /// and Z.
        /// </summary>
        public static MeshPlacement3 World { get; } = new MeshPlacement3(MeshAxes.World, null, null, null);

        /// <summary>
        /// Along the shape's own sides: the smallest rectangle round a flat shape, a box's own axes, the smallest box round a
        /// body.
        /// </summary>
        public static MeshPlacement3 Own { get; } = new MeshPlacement3(MeshAxes.Own, null, null, null);

        /// <summary>
        /// Standing up: through a body, Z up and X along the long side of its plan; over a flat shape as <see cref="World"/>.
        /// </summary>
        public static MeshPlacement3 Upright { get; } = new MeshPlacement3(MeshAxes.Upright, null, null, null);

        /// <summary>
        /// The first axis along a direction: laid onto the plane of a flat shape, the X axis of a body's grid.
        /// </summary>
        /// <param name="direction">The direction; its length does not matter.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the direction is not finite or has no length.</exception>
        public static MeshPlacement3 Along(GeoVector3 direction)
        {
            if (!direction.IsValid || !(direction.LengthSquared > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "A direction has to be finite and of some length.");
            }

            return new MeshPlacement3(MeshAxes.Along, direction.Divide(direction.Length), null, null);
        }

        /// <summary>
        /// Along the axes of a coordinate system: its X axis laid onto the plane of a flat shape, its three axes a body's.
        /// </summary>
        /// <param name="frame">The coordinate system; its origin is not where a cell starts unless <see cref="At"/> says so.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the coordinate system is not a valid one.</exception>
        public static MeshPlacement3 Frame(GeoCoordinateSystem3 frame)
        {
            if (!frame.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), frame, "A coordinate system has to have a finite origin and square axes of unit length.");
            }

            return new MeshPlacement3(MeshAxes.Frame, null, frame, null);
        }

        /// <summary>
        /// Gets these axes with a cell starting at a point, which takes the place of the alignments.
        /// </summary>
        /// <param name="origin">Where a cell has its first corner; over a flat shape, put onto its plane along its normal.</param>
        /// <returns>A placement with the same axes and the origin.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the origin is not a finite point.</exception>
        public MeshPlacement3 At(GeoPoint3 origin)
        {
            if (!origin.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin, "An origin has to be a finite point.");
            }

            return new MeshPlacement3(Axes, Direction, CoordinateSystem, origin);
        }

        /// <summary>
        /// Gets which way the axes run.
        /// </summary>
        public MeshAxes Axes { get; }

        /// <summary>
        /// Gets the direction of the first axis, of unit length, for <see cref="MeshAxes.Along"/>; null for the others.
        /// </summary>
        public GeoVector3? Direction { get; }

        /// <summary>
        /// Gets the coordinate system whose axes the grid takes, for <see cref="MeshAxes.Frame"/>; null for the others.
        /// </summary>
        public GeoCoordinateSystem3? CoordinateSystem { get; }

        /// <summary>
        /// Gets where a cell has its first corner, which takes the place of the alignments; null when the alignments place
        /// the grid.
        /// </summary>
        public GeoPoint3? Origin { get; }

        #region Frames

        /// <summary>
        /// The frame a flat shape is meshed in: its origin at a point of the shape, its Z axis the shape's normal, and its X
        /// axis the grid's first axis, as these axes say, turned counter-clockwise about the normal by an angle.
        /// </summary>
        /// <param name="at">Where the frame has its origin, a corner of the shape, so that the shape is laid out near it.</param>
        /// <param name="normal">The shape's normal, of unit length.</param>
        /// <param name="corners">The shape's corners, which <see cref="MeshAxes.Own"/> fits its rectangle round.</param>
        /// <param name="angleRad">How far to turn the first axis about the normal.</param>
        /// <param name="tolerance">The tolerance, whose angle says when a shape is level and a direction square to it.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the direction of <see cref="MeshAxes.Along"/> stands square to the shape, so that it gives no way along
        /// it.
        /// </exception>
        internal GeoCoordinateSystem3 FrameOnPlane(GeoPoint3 at, GeoVector3 normal, IReadOnlyList<GeoPoint3> corners, double angleRad, Tolerance tolerance)
        {
            GeoVector3 u = FirstAxisOnPlane(normal, corners, tolerance);

            if (angleRad != 0.0)
            {
                double cos = Math.Cos(angleRad);
                double sin = Math.Sin(angleRad);
                u = u.Multiply(cos).Add(normal.CrossProduct(u).Multiply(sin));
            }

            return new GeoCoordinateSystem3(at, u, normal.CrossProduct(u));
        }

        /// <summary>
        /// The direction of a grid's first axis over a flat shape, of unit length and square to the normal.
        /// </summary>
        private GeoVector3 FirstAxisOnPlane(GeoVector3 normal, IReadOnlyList<GeoPoint3> corners, Tolerance tolerance)
        {
            switch (Axes)
            {
                case MeshAxes.Own:
                    return OwnAxisOnPlane(normal, corners, tolerance);

                case MeshAxes.Along:
                    if (TryLayOnto(Direction.Value, normal, tolerance, out GeoVector3 along))
                    {
                        return along;
                    }

                    throw new ArgumentException("The direction stands square to the shape, so it gives no way along it.", "placement");

                case MeshAxes.Frame:
                    GeoCoordinateSystem3 frame = CoordinateSystem.Value;

                    // A face square to the frame's X axis takes its Y axis, which then lies along the face.
                    return TryLayOnto(frame.XAxis, normal, tolerance, out GeoVector3 x)
                        ? x
                        : TryLayOnto(frame.YAxis, normal, tolerance, out GeoVector3 y) ? y : LaidOnto(frame.ZAxis, normal);

                default:
                    return LevelAxis(normal, tolerance);
            }
        }

        /// <summary>
        /// The level direction in a plane, so that the second axis runs up its slope; the world's X axis laid onto a plane
        /// level within the angle tolerance, where no direction in it is more level than another.
        /// </summary>
        internal static GeoVector3 LevelAxis(GeoVector3 normal, Tolerance tolerance)
        {
            GeoVector3 across = GeoVector3.ZAxis.CrossProduct(normal);
            double tilt = across.Length;

            if (tilt > Math.Sin(tolerance.EqualAngleRad) && tilt > 1E-12)
            {
                return across.Divide(tilt);
            }

            return LaidOnto(GeoVector3.XAxis, normal);
        }

        /// <summary>
        /// The long side of the smallest rectangle round a shape's corners, laid out in its plane, pointing along the world's
        /// X axis rather than against it, or along Y, or Z, where it stands square to the ones before.
        /// </summary>
        private static GeoVector3 OwnAxisOnPlane(GeoVector3 normal, IReadOnlyList<GeoPoint3> corners, Tolerance tolerance)
        {
            GeoVector3 b1 = LevelAxis(normal, tolerance);
            GeoVector3 b2 = normal.CrossProduct(b1);
            GeoPoint3 first = corners[0];
            var flat = new List<GeoPoint2>(corners.Count);

            foreach (GeoPoint3 corner in corners)
            {
                GeoVector3 offset = first.GetVectorTo(corner);
                flat.Add(new GeoPoint2(offset.DotProduct(b1), offset.DotProduct(b2)));
            }

            GeoRectangle2 rectangle = BoxFit.Rectangle(flat, tolerance);
            double cos = Math.Cos(rectangle.AngleRad);
            double sin = Math.Sin(rectangle.AngleRad);
            GeoVector3 along = rectangle.Width >= rectangle.Height
                ? b1.Multiply(cos).Add(b2.Multiply(sin))
                : b1.Multiply(-sin).Add(b2.Multiply(cos));

            return Canonical(along);
        }

        /// <summary>
        /// A direction turned, if need be, to point along the world's X axis rather than against it, or along Y where it is
        /// square to X, or along Z where it is square to both, so that a shape's own axis comes out the same whichever corner
        /// its loop starts at.
        /// </summary>
        internal static GeoVector3 Canonical(GeoVector3 direction)
        {
            const double Square = 1E-9;

            if (direction.X < -Square || (Math.Abs(direction.X) <= Square && (direction.Y < -Square || (Math.Abs(direction.Y) <= Square && direction.Z < 0.0))))
            {
                return direction.Negate();
            }

            return direction;
        }

        /// <summary>
        /// The axes of a body's grid, as these axes say, about a point of the body.
        /// </summary>
        /// <param name="points">The body's corners: the box's own axes fit round them, and the plan's rectangle.</param>
        /// <param name="box">The body's own box, whose axes <see cref="MeshAxes.Own"/> takes; null to fit one round the points.</param>
        /// <param name="tolerance">The tolerance the box and the rectangle are fitted within.</param>
        internal GeoCoordinateSystem3 AxesOfBody(IReadOnlyList<GeoPoint3> points, GeoObb3 box, Tolerance tolerance)
        {
            GeoPoint3 at = points[0];

            switch (Axes)
            {
                case MeshAxes.Own:
                    return box != null ? box.CoordinateSystem.WithOrigin(at) : FittedAxes(points, at, tolerance);

                case MeshAxes.Upright:
                    return UprightAxes(points, at, tolerance);

                case MeshAxes.Along:
                    GeoVector3 x = Direction.Value;
                    GeoVector3 level = GeoVector3.ZAxis.CrossProduct(x);

                    // A direction straight up has no level across it; Y then runs along the world's Y laid square to it.
                    GeoVector3 y = level.Length > 1E-9 ? level.Divide(level.Length) : LaidOnto(GeoVector3.YAxis, x);
                    return new GeoCoordinateSystem3(at, x, y);

                case MeshAxes.Frame:
                    return CoordinateSystem.Value.WithOrigin(at);

                default:
                    return GeoCoordinateSystem3.Global.WithOrigin(at);
            }
        }

        /// <summary>
        /// The axes of the smallest box round a body's corners, the one nearest upright as Z, pointing up, and the longer of
        /// the other two as X, pointing along the world's X axis rather than against it.
        /// </summary>
        private static GeoCoordinateSystem3 FittedAxes(IReadOnlyList<GeoPoint3> points, GeoPoint3 at, Tolerance tolerance)
        {
            GeoObb3 fitted = BoxFit.Box(points, tolerance);
            int up = 0;

            for (int a = 1; a < 3; a++)
            {
                if (Math.Abs(fitted.GetAxisAt(a).Z) > Math.Abs(fitted.GetAxisAt(up).Z) + 1E-12)
                {
                    up = a;
                }
            }

            int first = (up + 1) % 3;
            int second = (up + 2) % 3;
            int along = fitted.GetExtentAt(second) > fitted.GetExtentAt(first) + 1E-9 * Math.Max(1.0, fitted.GetExtentAt(first)) ? second : first;

            GeoVector3 z = fitted.GetAxisAt(up);

            if (z.Z < -1E-12 || (Math.Abs(z.Z) <= 1E-12 && !Canonical(z).Equals(z)))
            {
                z = z.Negate();
            }

            GeoVector3 x = Canonical(fitted.GetAxisAt(along));

            return new GeoCoordinateSystem3(at, x, z.CrossProduct(x));
        }

        /// <summary>
        /// Axes standing up: Z along the world's, and X along the long side of the smallest rectangle round the body's plan.
        /// </summary>
        private static GeoCoordinateSystem3 UprightAxes(IReadOnlyList<GeoPoint3> points, GeoPoint3 at, Tolerance tolerance)
        {
            var plan = new List<GeoPoint2>(points.Count);

            foreach (GeoPoint3 point in points)
            {
                plan.Add(new GeoPoint2(point.X - at.X, point.Y - at.Y));
            }

            GeoRectangle2 rectangle = BoxFit.Rectangle(plan, tolerance);
            double cos = Math.Cos(rectangle.AngleRad);
            double sin = Math.Sin(rectangle.AngleRad);
            GeoVector3 x = rectangle.Width >= rectangle.Height ? new GeoVector3(cos, sin, 0.0) : new GeoVector3(-sin, cos, 0.0);
            x = Canonical(x);

            return new GeoCoordinateSystem3(at, x, GeoVector3.ZAxis.CrossProduct(x));
        }

        /// <summary>
        /// Lays a direction onto a plane, refusing one that stands within the angle tolerance of square to it.
        /// </summary>
        private static bool TryLayOnto(GeoVector3 direction, GeoVector3 normal, Tolerance tolerance, out GeoVector3 laid)
        {
            GeoVector3 unit = direction.Divide(direction.Length);
            GeoVector3 onto = unit.Subtract(normal.Multiply(unit.DotProduct(normal)));
            double length = onto.Length;

            if (length > Math.Sin(tolerance.EqualAngleRad) && length > 1E-9)
            {
                laid = onto.Divide(length);
                return true;
            }

            laid = default;
            return false;
        }

        /// <summary>
        /// Lays a direction onto a plane, or gives some direction in the plane when the direction stands square to it.
        /// </summary>
        private static GeoVector3 LaidOnto(GeoVector3 direction, GeoVector3 normal)
        {
            GeoVector3 onto = direction.Subtract(normal.Multiply(direction.DotProduct(normal)));
            double length = onto.Length;

            if (length > 1E-12)
            {
                return onto.Divide(length);
            }

            new GeoPlane3(GeoPoint3.Origin, normal).GetAxes(out GeoVector3 u, out _);
            return u;
        }

        #endregion

        /// <summary>
        /// Determines whether another placement stands a grid the same way.
        /// </summary>
        public bool Equals(MeshPlacement3 other)
        {
            return other != null
                && Axes == other.Axes
                && Nullable.Equals(Direction, other.Direction)
                && Nullable.Equals(CoordinateSystem, other.CoordinateSystem)
                && Nullable.Equals(Origin, other.Origin);
        }

        /// <summary>
        /// Determines whether the specified object is an equal placement.
        /// </summary>
        public override bool Equals(object obj) => obj is MeshPlacement3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this placement.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Axes;
                hash = hash * 397 ^ Direction.GetHashCode();
                hash = hash * 397 ^ CoordinateSystem.GetHashCode();
                hash = hash * 397 ^ Origin.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Describes the placement.
        /// </summary>
        public override string ToString()
        {
            string axes = Axes.ToString();

            if (Direction.HasValue)
            {
                axes += " " + Direction.Value;
            }
            else if (CoordinateSystem.HasValue)
            {
                axes += " " + CoordinateSystem.Value;
            }

            return Origin.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "(Axes: {0}, Origin: {1})", axes, Origin.Value)
                : string.Format(CultureInfo.InvariantCulture, "(Axes: {0}, Align)", axes);
        }
    }
}
