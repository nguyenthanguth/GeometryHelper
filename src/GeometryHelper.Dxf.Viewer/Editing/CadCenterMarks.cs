using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The pictures of CENTERMARK and CENTERLINE, as AutoCAD 2021 draws them: an INSERT of an anonymous block (<c>*U</c>) at the
    /// centre of a circle or an arc, or between two lines, the block's lines in the directions of the circle's own axes or of
    /// the lines, the cross in ByBlock colour and Continuous, the lines that run out to the rim in the linetype of CENTERLTYPE;
    /// and what is read off them again where the circle or the lines are edited.
    /// </summary>
    internal static partial class CadCenterMarks
    {
        /// <summary>One line of a block's picture, from <see cref="A"/> to <see cref="B"/>, and whether it is the cross.</summary>
        internal readonly struct Stroke
        {
            internal Stroke(GeoPoint3 a, GeoPoint3 b, bool cross)
            {
                A = a;
                B = b;
                Cross = cross;
            }

            internal GeoPoint3 A { get; }

            internal GeoPoint3 B { get; }

            internal bool Cross { get; }
        }

        /// <summary>What CENTEREXE and the other variables say a new mark or line is drawn with.</summary>
        internal sealed class Look
        {
            internal double Exe;
            internal bool Extensions;
            internal string Linetype;
            internal double LtScale;
            internal string Size;
            internal string Gap;
            internal string Layer;
        }

        /// <summary>The variables of the drawing, with the linetype made the name of one of its linetypes, loaded where it is of the presenter's own set.</summary>
        internal static Look LookOf(DxfDatabase database) =>
            new Look
            {
                Exe = database.CenterExe,
                Extensions = database.CenterMarkExe,
                Linetype = database.CenterLtype,
                LtScale = database.CenterLtScale,
                Size = database.CenterCrossSize,
                Gap = database.CenterCrossGap,
                Layer = database.CenterLayer,
            };

        /// <summary>
        /// A size as the variables write it: a number of drawing units (<c>6</c>), or a number of times the diameter
        /// (<c>0.1x</c>): a circle of radius 20 and 0.1x is 4 long.
        /// </summary>
        internal static double Measure(string expression, double diameter)
        {
            string text = (expression ?? string.Empty).Trim();
            bool times = text.EndsWith("x", StringComparison.OrdinalIgnoreCase);

            if (times)
            {
                text = text.Substring(0, text.Length - 1);
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? (times ? value * diameter : value)
                : 0.0;
        }

        /// <summary>
        /// The lines of a center mark about a circle or an arc of a radius, the block's origin at its centre: the cross (a
        /// length of <paramref name="size"/> either way across), then the four lines that run from the gap past the cross to
        /// the overshoot beyond the rim, left, right, bottom and top; AutoCAD 2021 drew a cross 4 long and lines from 4 to
        /// 20.12 on a circle of radius 20 under its defaults.
        /// </summary>
        internal static List<Stroke> MarkStrokes(string size, string gap, double overshoot, double radius)
        {
            double diameter = 2.0 * radius;
            double half = Measure(size, diameter) / 2.0;
            double from = half + Measure(gap, diameter);
            double to = radius + Math.Max(0.0, overshoot);
            var o = GeoPoint3.Origin;

            GeoPoint3 X(double x) => new GeoPoint3(x, 0.0, 0.0);
            GeoPoint3 Y(double y) => new GeoPoint3(0.0, y, 0.0);

            return new List<Stroke>
            {
                new Stroke(X(-half), X(half), true),
                new Stroke(Y(-half), Y(half), true),
                new Stroke(X(-to), X(-from), false),
                new Stroke(X(from), X(to), false),
                new Stroke(Y(-to), Y(-from), false),
                new Stroke(Y(from), Y(to), false),
            };
        }

        /// <summary>
        /// The centre line between two lines, as AutoCAD 2021 drew it: along the bisector of their directions (the second turned
        /// about where it runs against the first), from where it meets the line through the two start points to where it meets
        /// the line through the two end points, so that two parallel lines of 100 and 120 starting 30 apart give a line of 110
        /// about the middle of their middles. Where the lines cross and the two ends lie on both sides of the crossing, the line
        /// stops at the crossing, on the side the lines lie on (lines of 20 and 40 degrees ended where they cross); it is drawn
        /// from the end by the crossing outward, or for lines that do not cross from their starts to their ends.
        /// </summary>
        /// <param name="a0">The start of the first line.</param>
        /// <param name="a1">The end of the first line.</param>
        /// <param name="b0">The start of the second line.</param>
        /// <param name="b1">The end of the second line.</param>
        /// <param name="centre">Gives the middle of the centre line.</param>
        /// <param name="direction">Gives its direction, a unit vector.</param>
        /// <param name="first">Gives the end the picture is drawn from, as a distance along the direction from the middle.</param>
        /// <param name="last">Gives the end it is drawn to, as such a distance.</param>
        /// <returns>False where a line has no length, or the two run across one another with no way between them.</returns>
        internal static bool LineBetween(
            GeoPoint2 a0,
            GeoPoint2 a1,
            GeoPoint2 b0,
            GeoPoint2 b1,
            out GeoPoint2 centre,
            out GeoVector2 direction,
            out double first,
            out double last
        )
        {
            centre = default;
            direction = default;
            first = 0.0;
            last = 0.0;
            GeoVector2 da = a1 - a0;
            GeoVector2 db = b1 - b0;

            if (da.Length <= 0.0 || db.Length <= 0.0)
            {
                return false;
            }

            GeoVector2 ua = da / da.Length;
            GeoVector2 ub = db / db.Length;

            if (Dot(ua, ub) < 0.0)
            {
                ub = -ub;
                GeoPoint2 swap = b0;
                b0 = b1;
                b1 = swap;
                db = b1 - b0;
            }

            GeoVector2 sum = ua + ub;

            if (sum.Length <= 1E-9)
            {
                return false;
            }

            direction = sum / sum.Length;
            double turn = ua.X * ub.Y - ua.Y * ub.X;

            if (Math.Abs(turn) <= 1E-9)
            {
                // Parallel: the bisector runs through the middle of every chord from one line to the other.
                GeoPoint2 start = a0 + (b0 - a0) / 2.0;
                GeoPoint2 end = a1 + (b1 - a1) / 2.0;
                centre = start + (end - start) / 2.0;
                double length = Dot(end - start, direction);
                first = -length / 2.0;
                last = length / 2.0;
                return true;
            }

            double cross = da.X * db.Y - da.Y * db.X;
            double at = ((b0.X - a0.X) * db.Y - (b0.Y - a0.Y) * db.X) / cross;
            GeoPoint2 apex = a0 + da * at;

            // Where the line through two ends meets the bisector, as a distance along it from the crossing.
            double s0 = AlongBisector(apex, direction, a0, b0);
            double s1 = AlongBisector(apex, direction, a1, b1);

            if (s0 * s1 < 0.0)
            {
                double mean = 0.0;

                foreach (GeoPoint2 p in new[] { a0, a1, b0, b1 })
                {
                    mean += Dot(p - apex, direction);
                }

                double low = Math.Min(s0, s1);
                double high = Math.Max(s0, s1);
                low = mean >= 0.0 ? 0.0 : low;
                high = mean >= 0.0 ? high : 0.0;
                s0 = low;
                s1 = high;
            }

            double near = Math.Abs(s0) <= Math.Abs(s1) ? s0 : s1;
            double far = Math.Abs(s0) <= Math.Abs(s1) ? s1 : s0;
            double middle = (near + far) / 2.0;
            centre = apex + direction * middle;
            first = near - middle;
            last = far - middle;
            return true;
        }

        private static double Dot(GeoVector2 a, GeoVector2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>
        /// Where the line through two points meets the line through the apex in the direction, as a distance from the apex; the
        /// middle of the two points where the line runs along the direction.
        /// </summary>
        private static double AlongBisector(GeoPoint2 apex, GeoVector2 direction, GeoPoint2 p, GeoPoint2 q)
        {
            GeoVector2 chord = q - p;
            double denominator = chord.X * direction.Y - chord.Y * direction.X;

            if (Math.Abs(denominator) <= 1E-12 * Math.Max(1.0, chord.Length))
            {
                return Dot(p + chord / 2.0 - apex, direction);
            }

            double at = ((apex.X - p.X) * direction.Y - (apex.Y - p.Y) * direction.X) / denominator;
            return Dot(p + chord * at - apex, direction);
        }
    }
}
