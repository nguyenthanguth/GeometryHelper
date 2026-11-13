using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// One edge of a planar body: a line, or an arc of an ellipse (a circle is the ellipse of ratio 1), as ACIS holds it. The
    /// ellipse is its <see cref="Center"/>, its <see cref="Normal"/>, its <see cref="Major"/> axis (the vector to the
    /// farthest point, its length the major radius) and its <see cref="Ratio"/> of minor to major; the point at the parameter
    /// t is <c>Center + Major cos t + (Normal x Major) Ratio sin t</c>, so the curve runs counter-clockwise about its normal as
    /// t grows, from <see cref="T0"/> to <see cref="T1"/>.
    /// </summary>
    internal sealed class AcisCurve
    {
        private AcisCurve() { }

        /// <summary>Whether the edge is a line.</summary>
        internal bool IsLine { get; private set; }

        /// <summary>
        /// For a line, whether its loop runs against the direction of its edge (the coedge is reversed): AutoCAD's Explode gives the
        /// line in the direction of its edge, so a box whose loop runs up a side its edge runs down is exploded to a line down it.
        /// </summary>
        internal bool AgainstEdge { get; private set; }

        internal AcisVec Center { get; private set; }

        internal AcisVec Normal { get; private set; }

        internal AcisVec Major { get; private set; }

        internal double Ratio { get; private set; }

        internal double T0 { get; private set; }

        internal double T1 { get; private set; }

        /// <summary>The first point of a line; of an ellipse its point at <see cref="T0"/>.</summary>
        internal AcisVec Start { get; private set; }

        /// <summary>The last point of a line; of an ellipse its point at <see cref="T1"/>.</summary>
        internal AcisVec End { get; private set; }

        /// <summary>Whether the edge is a whole ellipse, one closed edge of a single vertex.</summary>
        internal bool IsFull => !IsLine && Math.Abs(T1 - T0 - 2.0 * Math.PI) < 1E-9;

        /// <summary>This line marked as run by its loop against its edge (see <see cref="AgainstEdge"/>); any other curve as it is.</summary>
        internal AcisCurve MarkedAgainstEdge() =>
            IsLine
                ? new AcisCurve
                {
                    IsLine = true,
                    Start = Start,
                    End = End,
                    AgainstEdge = true,
                }
                : this;

        /// <summary>A line from one point to another.</summary>
        internal static AcisCurve Line(AcisVec start, AcisVec end) =>
            new AcisCurve
            {
                IsLine = true,
                Start = start,
                End = end,
            };

        /// <summary>
        /// An arc of the circle about <paramref name="center"/> from <paramref name="start"/>, turning counter-clockwise about
        /// <paramref name="normal"/> by <paramref name="sweep"/> (nought to a turn); as AutoCAD writes one, its major axis the
        /// vector to the start and its parameter running from nought to the sweep. A negative sweep turns the other way: the
        /// curve's normal is turned over.
        /// </summary>
        internal static AcisCurve Arc(AcisVec center, AcisVec normal, AcisVec start, double sweep)
        {
            AcisVec turned = sweep < 0.0 ? -normal : normal;
            return Ellipse(center, turned, start - center, 1.0, 0.0, Math.Abs(sweep));
        }

        /// <summary>An arc of an ellipse between two parameters, the second above the first by at most a turn.</summary>
        internal static AcisCurve Ellipse(
            AcisVec center,
            AcisVec normal,
            AcisVec major,
            double ratio,
            double t0,
            double t1
        )
        {
            var curve = new AcisCurve
            {
                Center = center,
                Normal = normal.Unit,
                Major = major,
                Ratio = ratio,
                T0 = t0,
                T1 = t1,
            };
            curve.Start = curve.At(t0);
            curve.End = curve.At(t1);
            return curve;
        }

        /// <summary>
        /// A whole ellipse as AutoCAD writes one: from <paramref name="major"/>, the end of the major axis the entity gives, its
        /// major axis turned over and its parameter running from -pi to pi, which starts at that point.
        /// </summary>
        internal static AcisCurve Whole(AcisVec center, AcisVec normal, AcisVec major, double ratio) =>
            Ellipse(center, normal, -major, ratio, -Math.PI, Math.PI);

        /// <summary>The minor axis: the normal turned a quarter about the major axis, scaled by the ratio.</summary>
        internal AcisVec Minor => Normal.Cross(Major) * Ratio;

        /// <summary>The point of an ellipse at a parameter.</summary>
        internal AcisVec At(double t) => Center + Major * Math.Cos(t) + Minor * Math.Sin(t);

        /// <summary>The same edge run the other way, the first point last.</summary>
        internal AcisCurve Reversed()
        {
            if (IsLine)
            {
                return Line(End, Start);
            }

            if (IsFull)
            {
                return Ellipse(Center, -Normal, Major, Ratio, T0, T1);
            }

            if (Math.Abs(Ratio - 1.0) < 1E-12)
            {
                // A circle's arc, written from its new start as AutoCAD writes one.
                return Arc(Center, Normal, End, -(T1 - T0));
            }

            return Ellipse(Center, -Normal, Major, Ratio, -T1, -T0);
        }

        /// <summary>
        /// The edge moved by a transformation that scales alike in every direction: its points moved, its axes turned and scaled,
        /// the parameters kept. Where the transformation mirrors (<paramref name="mirrors"/>), the curve's normal is turned over,
        /// so that the curve still runs as its parameter grows, counter-clockwise about its normal.
        /// </summary>
        internal AcisCurve Transformed(GeoTransform3 transform, bool mirrors)
        {
            if (IsLine)
            {
                AcisCurve line = Line(Moved(transform, Start), Moved(transform, End));
                return AgainstEdge ? line.MarkedAgainstEdge() : line;
            }

            AcisVec normal = Turned(transform, Normal);
            return Ellipse(
                Moved(transform, Center),
                mirrors ? -normal : normal,
                Turned(transform, Major),
                Ratio,
                T0,
                T1
            );
        }

        /// <summary>A point moved by a transformation.</summary>
        internal static AcisVec Moved(GeoTransform3 transform, AcisVec point)
        {
            GeoPoint3 moved = transform.Transform(new GeoPoint3(point.X, point.Y, point.Z));
            return new AcisVec(moved.X, moved.Y, moved.Z);
        }

        /// <summary>A direction turned and scaled by a transformation.</summary>
        internal static AcisVec Turned(GeoTransform3 transform, AcisVec direction)
        {
            GeoVector3 turned = transform.Transform(new GeoVector3(direction.X, direction.Y, direction.Z));
            return new AcisVec(turned.X, turned.Y, turned.Z);
        }

        /// <summary>
        /// The parameter of a point on the curve, or the nearest it comes: of a line, the distance along it from its start; of an
        /// ellipse, the angle in the range -pi to pi of the point's place in the frame of its axes.
        /// </summary>
        internal double ParameterOf(AcisVec point)
        {
            if (IsLine)
            {
                AcisVec along = End - Start;
                return (point - Start).Dot(along) / along.Dot(along);
            }

            AcisVec offset = point - Center;
            AcisVec minor = Minor;
            return Math.Atan2(offset.Dot(minor) / minor.Dot(minor), offset.Dot(Major) / Major.Dot(Major));
        }

        /// <summary>The point at a parameter, along the whole line or the whole ellipse (which <see cref="ParameterOf"/> reads back).</summary>
        internal AcisVec PointAt(double parameter) => IsLine ? Start + (End - Start) * parameter : At(parameter);

        /// <summary>The velocity at a parameter: the vector from the start to the end of a line, the derivative of the point of an ellipse.</summary>
        internal AcisVec VelocityAt(double parameter) =>
            IsLine ? End - Start : Minor * Math.Cos(parameter) - Major * Math.Sin(parameter);

        /// <summary>The length of the edge; of an ellipse by Gauss-Legendre over pieces of a thirty-second of a turn.</summary>
        internal double Length
        {
            get
            {
                if (IsLine)
                {
                    return Start.DistanceTo(End);
                }

                if (Math.Abs(Ratio - 1.0) < 1E-12)
                {
                    return Major.Length * (T1 - T0);
                }

                int pieces = Math.Max(1, (int)Math.Ceiling((T1 - T0) / (Math.PI / 16.0)));
                double step = (T1 - T0) / pieces;
                double total = 0.0;

                for (int i = 0; i < pieces; i++)
                {
                    double a = T0 + i * step;
                    double b = a + step;

                    for (int k = 0; k < GaussX.Length; k++)
                    {
                        double t = 0.5 * (a + b) + 0.5 * (b - a) * GaussX[k];
                        AcisVec velocity = Minor * Math.Cos(t) - Major * Math.Sin(t);
                        total += 0.5 * (b - a) * GaussW[k] * velocity.Length;
                    }
                }

                return total;
            }
        }

        private static readonly double[] GaussX =
        {
            -0.9061798459386640,
            -0.5384693101056831,
            0.0,
            0.5384693101056831,
            0.9061798459386640,
        };

        private static readonly double[] GaussW =
        {
            0.2369268850561891,
            0.4786286704993665,
            0.5688888888888889,
            0.4786286704993665,
            0.2369268850561891,
        };

        /// <summary>
        /// Half the integral of the position crossed with its step along the edge: summed over a closed loop, the area it
        /// encloses as a vector along the loop's normal, counter-clockwise positive. A line is half of start x end; an ellipse
        /// has it in closed form, <c>(c x (a (cos t1 - cos t0) + b (sin t1 - sin t0)) + (a x b)(t1 - t0)) / 2</c> with a and b
        /// its axes.
        /// </summary>
        internal AcisVec AreaVector()
        {
            if (IsLine)
            {
                return Start.Cross(End) * 0.5;
            }

            AcisVec a = Major;
            AcisVec b = Minor;
            AcisVec around = a * (Math.Cos(T1) - Math.Cos(T0)) + b * (Math.Sin(T1) - Math.Sin(T0));
            return (Center.Cross(around) + a.Cross(b) * (T1 - T0)) * 0.5;
        }
    }
}
