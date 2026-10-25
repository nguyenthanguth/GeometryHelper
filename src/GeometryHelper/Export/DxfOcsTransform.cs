using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A transformation seen from the system of an extrusion, as ezdxf's <c>OCSTransform</c> sees it: points, directions,
    /// angles and lengths of an entity that stands in the system of its extrusion carried into the system of the extrusion the
    /// transformation gives it. A TEXT, an ATTRIB and an INSERT are moved through it, so that they land where ezdxf's
    /// <c>transform()</c> lands them.
    /// </summary>
    internal sealed class DxfOcsTransform
    {
        private readonly GeoTransform3 _m;
        private readonly Axes _old;
        private readonly Axes _new;

        /// <summary>The transformation seen from the system of an extrusion, carried to the one the transformation makes of it.</summary>
        internal DxfOcsTransform(GeoVector3 extrusion, GeoTransform3 m)
        {
            _m = m;
            _old = new Axes(extrusion);
            GeoVector3 x = m.Transform(_old.ToWorld(new GeoVector3(1.0, 0.0, 0.0)));
            GeoVector3 y = m.Transform(_old.ToWorld(new GeoVector3(0.0, 1.0, 0.0)));
            double xx = x.DotProduct(x);
            double yy = y.DotProduct(y);
            ScaleUniform = IsUniform(x, y);
            GeoVector3 z = Cross(x, y);
            double length = z.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("The transformation takes the plane of the entity to no plane.", nameof(m));
            }

            NewExtrusion = Normalized(z);
            _new = new Axes(NewExtrusion);
        }

        /// <summary>The transformation seen from one system, carried into another given.</summary>
        internal DxfOcsTransform(GeoVector3 oldExtrusion, GeoVector3 newExtrusion, GeoTransform3 m)
        {
            _m = m;
            _old = new Axes(oldExtrusion);
            _new = new Axes(newExtrusion);
            NewExtrusion = _new.Z;
            ScaleUniform = true;
        }

        /// <summary>The extrusion of the new system, of unit length.</summary>
        internal GeoVector3 NewExtrusion { get; }

        /// <summary>
        /// Whether the plane of the system is scaled alike along its x and y, within 1E-9, and its axes stay square, to
        /// <see cref="Tolerance.Default"/>: a shear whose axes come out of one length, as S(2, 1) R(45°), is not.
        /// </summary>
        internal bool ScaleUniform { get; }

        /// <summary>
        /// Whether a transformation leans the frame of a text: the images of its direction and of its upright (the normal crossed
        /// with the direction) no longer square, the cosine of the angle between them beyond <see cref="Tolerance.EqualVector"/>
        /// of <see cref="Tolerance.Default"/>. An MTEXT, which has no lean, cannot carry it.
        /// </summary>
        internal static bool Shears(GeoTransform3 m, GeoVector3 normal, GeoVector3 direction)
        {
            GeoVector3 along = m.Transform(direction);
            GeoVector3 up = m.Transform(Cross(normal, direction));
            return !(Math.Abs(along.DotProduct(up)) <= Tolerance.Default.EqualVector * along.Length * up.Length);
        }

        /// <summary>
        /// Whether two images of the axes of a plane make it a plane scaled alike: of one length, within 1E-9 of their squares as
        /// ezdxf compares them, and square, the cosine of the angle between them within <see cref="Tolerance.EqualVector"/> of
        /// <see cref="Tolerance.Default"/>.
        /// </summary>
        internal static bool IsUniform(GeoVector3 x, GeoVector3 y)
        {
            double xx = x.DotProduct(x);
            double yy = y.DotProduct(y);
            return Math.Abs(xx - yy) <= Math.Max(1E-9 * Math.Max(Math.Abs(xx), Math.Abs(yy)), 1E-9)
                && Math.Abs(x.DotProduct(y)) <= Tolerance.Default.EqualVector * Math.Sqrt(xx * yy);
        }

        /// <summary>A point of the old system in the new.</summary>
        internal GeoPoint3 TransformVertex(GeoPoint3 point)
        {
            GeoVector3 world = _old.ToWorld(new GeoVector3(point.X, point.Y, point.Z));
            GeoPoint3 moved = _m.Transform(new GeoPoint3(world.X, world.Y, world.Z));
            GeoVector3 ocs = _new.FromWorld(new GeoVector3(moved.X, moved.Y, moved.Z));
            return new GeoPoint3(ocs.X, ocs.Y, ocs.Z);
        }

        /// <summary>A direction of the old system in the new.</summary>
        internal GeoVector3 TransformDirection(GeoVector3 direction) => _new.FromWorld(_m.Transform(_old.ToWorld(direction)));

        /// <summary>The length a direction of the old system has once transformed.</summary>
        internal double TransformLength(GeoVector3 direction) => _m.Transform(_old.ToWorld(direction)).Length;

        /// <summary>An angle in degrees of the old system's plane, as the angle in degrees of the new.</summary>
        internal double TransformDegrees(double degrees)
        {
            double radians = degrees * (Math.PI / 180.0);
            GeoVector3 turned = TransformDirection(new GeoVector3(Math.Cos(radians), Math.Sin(radians), 0.0));
            return Math.Atan2(turned.Y, turned.X) * (180.0 / Math.PI);
        }

        /// <summary>A thickness along the old extrusion, as one along the new.</summary>
        internal double TransformThickness(double thickness) => TransformDirection(new GeoVector3(0.0, 0.0, thickness)).Z;

        /// <summary>A point of the old system in the new, as <see cref="TransformVertex"/> gives it.</summary>
        internal GeoPoint3 Vertex(GeoPoint3 point) => TransformVertex(point);

        /// <summary>A thickness along the old extrusion, as one along the new, of either sign, as <see cref="TransformThickness"/> gives it.</summary>
        internal double Thickness(double thickness) => TransformThickness(thickness);

        /// <summary>A width of a polyline, as ezdxf carries one: the longer of the two axes of the old plane scaled by it; nought for nought.</summary>
        internal double Width(double width)
        {
            double size = Math.Abs(width);

            if (!(size > 1E-12))
            {
                return 0.0;
            }

            return Math.Max(TransformLength(new GeoVector3(size, 0.0, 0.0)), TransformLength(new GeoVector3(0.0, size, 0.0)));
        }

        /// <summary>Whether a transformation is the identity, to the bit: one that leaves a record as it is.</summary>
        internal static bool IsIdentity(GeoTransform3 m)
        {
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    if (BitConverter.DoubleToInt64Bits(m[r, c]) != BitConverter.DoubleToInt64Bits(r == c ? 1.0 : 0.0))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// An angle in radians as the degrees a file writes: the number of degrees nearest it that reads back, times pi over
        /// 180, as the very angle, where one within four steps of the last bit does; else the nearest.
        /// </summary>
        internal static double Degrees(double radians)
        {
            double degrees = radians * (180.0 / Math.PI);
            double up = degrees;
            double down = degrees;

            for (int step = 0; step <= 4; step++)
            {
                if (BitConverter.DoubleToInt64Bits(up * (Math.PI / 180.0)) == BitConverter.DoubleToInt64Bits(radians))
                {
                    return up + 0.0;
                }

                if (BitConverter.DoubleToInt64Bits(down * (Math.PI / 180.0)) == BitConverter.DoubleToInt64Bits(radians))
                {
                    return down + 0.0;
                }

                up = Next(up, 1);
                down = Next(down, -1);
            }

            return degrees + 0.0;
        }

        /// <summary>The double next to one, up or down.</summary>
        private static double Next(double value, int direction)
        {
            if (value == 0.0)
            {
                return direction > 0 ? double.Epsilon : -double.Epsilon;
            }

            long bits = BitConverter.DoubleToInt64Bits(value);
            bits += (value > 0.0) == (direction > 0) ? 1 : -1;
            return BitConverter.Int64BitsToDouble(bits);
        }

        /// <summary>A direction of unit length, as ezdxf makes one: multiplied by one over its length.</summary>
        internal static GeoVector3 Normalized(GeoVector3 v) => v.Multiply(1.0 / v.Length);

        internal static GeoVector3 Cross(GeoVector3 a, GeoVector3 b) => new GeoVector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        /// <summary>The axes of the system of an extrusion by the arbitrary axis algorithm, as ezdxf's <c>OCS</c> makes them.</summary>
        internal sealed class Axes
        {
            private readonly bool _world;

            internal Axes(GeoVector3 extrusion)
            {
                Z = Normalized(extrusion);
                _world = Math.Abs(Z.X) <= 1E-12 && Math.Abs(Z.Y) <= 1E-12 && Math.Abs(Z.Z - 1.0) <= 1E-12;

                if (_world)
                {
                    X = new GeoVector3(1.0, 0.0, 0.0);
                    Y = new GeoVector3(0.0, 1.0, 0.0);
                    return;
                }

                bool nearZ = Math.Abs(Z.X) < 1.0 / 64.0 && Math.Abs(Z.Y) < 1.0 / 64.0;
                X = Normalized(Cross(nearZ ? new GeoVector3(0.0, 1.0, 0.0) : new GeoVector3(0.0, 0.0, 1.0), Z));
                Y = Normalized(Cross(Z, X));
            }

            internal GeoVector3 X { get; }

            internal GeoVector3 Y { get; }

            internal GeoVector3 Z { get; }

            internal GeoVector3 ToWorld(GeoVector3 v)
                => _world ? v : new GeoVector3(X.X * v.X + Y.X * v.Y + Z.X * v.Z, X.Y * v.X + Y.Y * v.Y + Z.Y * v.Z, X.Z * v.X + Y.Z * v.Y + Z.Z * v.Z);

            internal GeoVector3 FromWorld(GeoVector3 v)
                => _world ? v : new GeoVector3(X.DotProduct(v), Y.DotProduct(v), Z.DotProduct(v));
        }
    }
}
