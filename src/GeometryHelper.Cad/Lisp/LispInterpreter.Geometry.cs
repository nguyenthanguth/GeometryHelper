using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        /// <summary>
        /// <c>angle</c>, <c>distance</c>, <c>polar</c>, <c>inters</c> and <c>cvunit</c>, which need no drawing: they are defined
        /// in every interpreter. Points are lists of two or three numbers; a point of two stays one of two in the answer.
        /// </summary>
        private void RegisterGeometry()
        {
            Define("ANGLE", 2, 2, a => AngleOf(PointOf(a[0]), PointOf(a[1])));
            Define("DISTANCE", 2, 2, a => DistanceOf(PointOf(a[0]), PointOf(a[1])));
            Define("POLAR", 3, 3, a => PolarOf(PointOf(a[0]), ToDouble(Number(a[1])), ToDouble(Number(a[2]))));
            Define("INTERS", 4, 5, a =>
                Intersection(PointOf(a[0]), PointOf(a[1]), PointOf(a[2]), PointOf(a[3]), a.Length < 5 || a[4] != null)
            );
            Define("CVUNIT", 3, 3, a => ConvertUnit(a[0], Str(a[1]), Str(a[2])));
        }

        /// <summary>A point of a list: its coordinates, and whether the list gave a Z.</summary>
        private readonly struct Pt
        {
            internal Pt(double x, double y, double z, bool is3D)
            {
                X = x;
                Y = y;
                Z = z;
                Is3D = is3D;
            }

            internal double X { get; }

            internal double Y { get; }

            internal double Z { get; }

            internal bool Is3D { get; }
        }

        /// <summary>
        /// Takes a point: a list of two or three numbers, whole or real. Anything else is <c>bad argument type: 2D/3D point: 5</c>
        /// (a guess: AutoCAD's own wording was not measured).
        /// </summary>
        private static Pt PointOf(object value)
        {
            if (value is LispCons first && first.Car is object x && IsNumber(x) && first.Cdr is LispCons second)
            {
                if (IsNumber(second.Car))
                {
                    if (second.Cdr == null)
                    {
                        return new Pt(ToDouble(x), ToDouble(second.Car), 0.0, false);
                    }

                    if (second.Cdr is LispCons third && third.Cdr == null && IsNumber(third.Car))
                    {
                        return new Pt(ToDouble(x), ToDouble(second.Car), ToDouble(third.Car), true);
                    }
                }
            }

            throw TypeError("2D/3D point", value, colon: true);
        }

        private static bool IsNumber(object value) => value is int || value is double;

        private static object ListOfNumbers(double x, double y, double z, bool is3D) =>
            is3D ? ListOf(new object[] { x + 0.0, y + 0.0, z + 0.0 }) : ListOf(new object[] { x + 0.0, y + 0.0 });

        /// <summary>The angle of the line from one point to the other in the XY plane, from nought up to a whole turn.</summary>
        private static object AngleOf(Pt from, Pt to)
        {
            double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
            double turn = 2.0 * Math.PI;

            if (angle < 0.0)
            {
                angle += turn;
            }

            return angle >= turn ? 0.0 : angle + 0.0;
        }

        /// <summary>The distance between two points, in space when both have a Z and in the plane otherwise.</summary>
        private static object DistanceOf(Pt a, Pt b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double dz = a.Is3D && b.Is3D ? b.Z - a.Z : 0.0;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // A quarter turn in three pieces of 33 bits each and a tail, so that a multiple of them is exact below a million: the
        // constants of the C library's argument reduction (fdlibm's pio2_1 to pio2_3t).
        private const double HalfPiOne = 1.57079632673412561417e+00;
        private const double HalfPiTwo = 6.07710050630396597660e-11;
        private const double HalfPiThree = 2.02226624871116645580e-21;
        private const double HalfPiTail = 8.47842766036889956997e-32;

        /// <summary>
        /// The sine and the cosine of an angle to the last digit even close to a multiple of a quarter turn, where the framework's
        /// own <c>Math.Sin</c> gives 1.22461e-16 for <c>pi</c> where AutoCAD, and .NET Core, give 1.22465e-16. The angle is cut
        /// down to within an eighth of a turn of nought, with the quarter turns it lost counted, and the framework's functions
        /// are asked only for that small angle.
        /// </summary>
        private static void SineAndCosine(double angle, out double sine, out double cosine)
        {
            angle = Reduced(angle);

            if (double.IsNaN(angle) || double.IsInfinity(angle) || Math.Abs(angle) < Math.PI / 4.0)
            {
                sine = Math.Sin(angle);
                cosine = Math.Cos(angle);
                return;
            }

            double quarters = Math.Round(angle / (Math.PI / 2.0));
            double rest = angle - quarters * HalfPiOne;
            rest -= quarters * HalfPiTwo;
            rest -= quarters * HalfPiThree;
            rest -= quarters * HalfPiTail;
            double s = Math.Sin(rest);
            double c = Math.Cos(rest);

            switch ((int)quarters & 3)
            {
                case 0:
                    sine = s;
                    cosine = c;
                    break;
                case 1:
                    sine = c;
                    cosine = -s;
                    break;
                case 2:
                    sine = -s;
                    cosine = -c;
                    break;
                default:
                    sine = -c;
                    cosine = s;
                    break;
            }
        }

        /// <summary>The point at an angle and a distance from a point, with its Z kept where it had one.</summary>
        private static object PolarOf(Pt from, double angle, double distance)
        {
            SineAndCosine(angle, out double sine, out double cosine);
            return ListOfNumbers(from.X + distance * cosine, from.Y + distance * sine, from.Z, from.Is3D);
        }

        /// <summary>
        /// The point where the line through the first two points crosses the line through the second two: on both segments,
        /// or anywhere on the lines when <paramref name="onSegments"/> is false. Nil for lines that are parallel (the sine of
        /// the angle between them within the vector tolerance), and, for four points that all have a Z, for lines that pass
        /// further apart than the point tolerance. A point with no Z makes all four points flat.
        /// </summary>
        private static object Intersection(Pt a, Pt b, Pt c, Pt d, bool onSegments)
        {
            bool space = a.Is3D && b.Is3D && c.Is3D && d.Is3D;
            double rx = b.X - a.X;
            double ry = b.Y - a.Y;
            double rz = space ? b.Z - a.Z : 0.0;
            double sx = d.X - c.X;
            double sy = d.Y - c.Y;
            double sz = space ? d.Z - c.Z : 0.0;
            double wx = a.X - c.X;
            double wy = a.Y - c.Y;
            double wz = space ? a.Z - c.Z : 0.0;
            double rr = rx * rx + ry * ry + rz * rz;
            double ss = sx * sx + sy * sy + sz * sz;
            double rs = rx * sx + ry * sy + rz * sz;
            double rw = rx * wx + ry * wy + rz * wz;
            double sw = sx * wx + sy * wy + sz * wz;
            double sine = Tolerance.Default.EqualVector;
            double apart = rr * ss - rs * rs;

            // A line with no length, or two that run the same way, has no single crossing.
            if (!(rr > 0.0) || !(ss > 0.0) || apart <= sine * sine * rr * ss)
            {
                return null;
            }

            double t = (rs * sw - ss * rw) / apart;
            double u = (rr * sw - rs * rw) / apart;

            if (onSegments)
            {
                double near = Tolerance.Default.EqualPoint;
                double beyondR = near / Math.Sqrt(rr);
                double beyondS = near / Math.Sqrt(ss);

                if (t < -beyondR || t > 1.0 + beyondR || u < -beyondS || u > 1.0 + beyondS)
                {
                    return null;
                }
            }

            double px = a.X + t * rx;
            double py = a.Y + t * ry;
            double pz = space ? a.Z + t * rz : 0.0;

            if (space)
            {
                double gx = px - (c.X + u * sx);
                double gy = py - (c.Y + u * sy);
                double gz = pz - (c.Z + u * sz);

                if (Math.Sqrt(gx * gx + gy * gy + gz * gz) > Tolerance.Default.EqualPoint)
                {
                    return null;
                }
            }

            return ListOfNumbers(px, py, pz, space);
        }

        /// <summary>
        /// <c>cvunit</c>: a number, or each number of a list, from one unit to another of the same kind; nil where a unit is
        /// not in the table or the two are of different kinds.
        /// </summary>
        private static object ConvertUnit(object value, string from, string to)
        {
            if (value is LispCons)
            {
                var converted = new System.Collections.Generic.List<object>();

                foreach (object item in ItemsOf(value))
                {
                    if (!LispUnitTable.TryConvert(ToDouble(Number(item)), from, to, out double result))
                    {
                        return null;
                    }

                    converted.Add(result);
                }

                return ListOf(converted);
            }

            return LispUnitTable.TryConvert(ToDouble(Number(value)), from, to, out double single)
                ? single
                : (object)null;
        }
    }
}
