using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The shapes, tolerances and the small oracle the fillet and offset faults (S1 to S8) are shown on, shared by their
    /// test files. The oracle works on plain doubles from the vertices and bulges alone, and calls nothing in the library
    /// that measures, so a test of an offset does not check the library against itself.
    /// </summary>
    internal static class OffsetFaultShapes
    {
        internal static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        internal static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>A chain through the points, every edge straight unless a bulge is given for it.</summary>
        internal static GeoPolylineArc2 Chain(GeoPoint2[] points, params double[] bulges)
        {
            var all = new double[points.Length];
            Array.Copy(bulges, all, bulges.Length);
            return new GeoPolylineArc2(points, all);
        }

        /// <summary>A loop through the points, every edge straight unless a bulge is given for it.</summary>
        internal static GeoPolygonArc2 Loop(GeoPoint2[] points, params double[] bulges)
        {
            var all = new double[points.Length];
            Array.Copy(bulges, all, bulges.Length);
            return new GeoPolygonArc2(points, all);
        }

        #region Tolerances

        /// <summary>The default reading: 1E-3 for points, 1E-5 for vectors, one degree for angles.</summary>
        internal static readonly Tolerance Thousandth = new Tolerance(1E-3, 1E-5);

        /// <summary>A coarse tolerance, 0.1 for points and 1E-3 for vectors, the audit's t1.</summary>
        internal static readonly Tolerance Tenth = new Tolerance(0.1, 1E-3);

        #endregion

        #region The oracle

        /// <summary>
        /// A segment or an arc read from its two ends and its bulge, on plain doubles: the bulge is the tangent of a quarter
        /// of the signed sweep, positive counter-clockwise, and the centre lies to the left of the chord for a positive one.
        /// </summary>
        internal sealed class Piece
        {
            internal double Ax, Ay, Bx, By, Cx, Cy, Radius, StartAngle, Sweep;
            internal bool IsArc;

            internal static Piece Of(GeoPoint2 a, GeoPoint2 b, double bulge)
            {
                var p = new Piece { Ax = a.X, Ay = a.Y, Bx = b.X, By = b.Y };
                double dx = b.X - a.X, dy = b.Y - a.Y, chord = Math.Sqrt(dx * dx + dy * dy);
                if (bulge == 0.0 || chord == 0.0) return p;
                p.IsArc = true;
                p.Sweep = 4.0 * Math.Atan(bulge);
                double s = chord / 2, h = bulge * s, k = (s * s - h * h) / (2 * h);
                p.Cx = (a.X + b.X) / 2 - dy / chord * k;
                p.Cy = (a.Y + b.Y) / 2 + dx / chord * k;
                p.Radius = (s * s + h * h) / (2 * Math.Abs(h));
                p.StartAngle = Math.Atan2(a.Y - p.Cy, a.X - p.Cx);
                return p;
            }

            /// <summary>The point a fraction t of the way along.</summary>
            internal void At(double t, out double x, out double y)
            {
                if (!IsArc)
                {
                    x = Ax + (Bx - Ax) * t;
                    y = Ay + (By - Ay) * t;
                    return;
                }
                double angle = StartAngle + Sweep * t;
                x = Cx + Radius * Math.Cos(angle);
                y = Cy + Radius * Math.Sin(angle);
            }

            /// <summary>How far the point is from the piece.</summary>
            internal double DistanceTo(double x, double y)
            {
                if (!IsArc)
                {
                    double dx = Bx - Ax, dy = By - Ay;
                    double t = Math.Max(0.0, Math.Min(1.0, ((x - Ax) * dx + (y - Ay) * dy) / (dx * dx + dy * dy)));
                    return Math.Sqrt(Sq(x - Ax - dx * t) + Sq(y - Ay - dy * t));
                }
                double phi = Math.Atan2(y - Cy, x - Cx);
                double turned = Sweep > 0 ? Wrap(phi - StartAngle) : Wrap(StartAngle - phi);
                if (turned <= Math.Abs(Sweep)) return Math.Abs(Math.Sqrt(Sq(x - Cx) + Sq(y - Cy)) - Radius);
                return Math.Min(Math.Sqrt(Sq(x - Ax) + Sq(y - Ay)), Math.Sqrt(Sq(x - Bx) + Sq(y - By)));
            }

            private static double Sq(double v) => v * v;

            private static double Wrap(double angle)
            {
                double t = angle % (2 * Math.PI);
                return t < 0 ? t + 2 * Math.PI : t;
            }
        }

        /// <summary>The pieces of a chain or a loop given by its vertices and bulges.</summary>
        internal static Piece[] PiecesOf(IReadOnlyList<GeoPoint2> vertices, IReadOnlyList<double> bulges, bool closed)
        {
            int n = vertices.Count, count = closed ? n : n - 1;
            var pieces = new Piece[count];
            for (int i = 0; i < count; i++) pieces[i] = Piece.Of(vertices[i], vertices[(i + 1) % n], bulges == null ? 0.0 : bulges[i]);
            return pieces;
        }

        internal static Piece[] PiecesOf(GeoPolylineArc2 chain) => PiecesOf(chain.Vertices, chain.Bulges, false);

        internal static Piece[] PiecesOf(GeoPolygonArc2 loop) => PiecesOf(loop.Vertices, loop.Bulges, true);

        internal static Piece[] PiecesOf(GeoPolyline2 chain) => PiecesOf(chain.Vertices, null, false);

        internal static Piece[] PiecesOf(GeoPolygon2 loop) => PiecesOf(loop.Vertices, null, true);

        /// <summary>How far the point is from the nearest of the pieces.</summary>
        internal static double DistanceTo(Piece[] pieces, double x, double y)
        {
            double best = double.PositiveInfinity;
            foreach (Piece p in pieces) best = Math.Min(best, p.DistanceTo(x, y));
            return best;
        }

        /// <summary>
        /// The worst amount by which a point of the answer (25 a piece, ends included) stands off the distance d from the
        /// source: what plane.md promises a curved offset is, "every point of a valid offset stands exactly the offset
        /// distance from the shape it came from".
        /// </summary>
        internal static double WorstOffDistance(Piece[] answer, Piece[] source, double distance)
        {
            double worst = 0.0;
            foreach (Piece p in answer)
            {
                for (int k = 0; k <= 24; k++)
                {
                    p.At(k / 24.0, out double x, out double y);
                    worst = Math.Max(worst, Math.Abs(DistanceTo(source, x, y) - Math.Abs(distance)));
                }
            }
            return worst;
        }

        /// <summary>The length of the pieces.</summary>
        internal static double LengthOf(Piece[] pieces)
        {
            double sum = 0.0;
            foreach (Piece p in pieces) sum += p.IsArc ? Math.Abs(p.Sweep) * p.Radius : Math.Sqrt((p.Bx - p.Ax) * (p.Bx - p.Ax) + (p.By - p.Ay) * (p.By - p.Ay));
            return sum;
        }

        /// <summary>
        /// Whether a straight loop crosses itself: two edges that are not neighbours share a point, read exactly on the
        /// signs of the cross products.
        /// </summary>
        internal static bool CrossesItself(IReadOnlyList<GeoPoint2> loop)
        {
            int n = loop.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;
                    if (Cross(loop[i], loop[(i + 1) % n], loop[j], loop[(j + 1) % n])) return true;
                }
            }
            return false;
        }

        private static bool Cross(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c, GeoPoint2 d)
        {
            double d1 = Side(c, d, a), d2 = Side(c, d, b), d3 = Side(a, b, c), d4 = Side(a, b, d);
            return d1 * d2 < 0 && d3 * d4 < 0;
        }

        private static double Side(GeoPoint2 a, GeoPoint2 b, GeoPoint2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

        #endregion

        #region S1: a corner between a segment and a leg turning to the left

        /// <summary>
        /// The chain from the origin to (10, 0), then 8 along a leg turning <paramref name="turnDeg"/> degrees to the left:
        /// the corner the audit's fillet sweep is made of.
        /// </summary>
        internal static GeoPolylineArc2 Turning(double turnDeg)
        {
            double a = turnDeg * Math.PI / 180.0;
            return new GeoPolylineArc2(new[] { P(0, 0), P(10, 0), P(10 + 8 * Math.Cos(a), 8 * Math.Sin(a)) });
        }

        /// <summary>
        /// A bent bar: 10 straight along x, a quarter turn of radius 5 about (10, 5) up to (15, 5), and 10 straight up. Both
        /// bends run on tangentially from the straights, so neither joint is a corner.
        /// </summary>
        internal static GeoPolylineArc2 BentBar()
            => Chain(new[] { P(0, 0), P(10, 0), P(15, 5), P(15, 15) }, 0.0, Math.Tan(Math.PI / 8));

        #endregion

        #region S4: chains whose offsets fold over

        /// <summary>
        /// The audit's AR1 chain: 10 along x, then an arc of bulge 0.5 (106.26 degrees, radius 6.25 about
        /// (8.68667, -6.11046)) whose start tangent turns 167.87 degrees to the left, so it rises 0.14 above the axis and
        /// curls back down to (2.4529, -6.5606).
        /// </summary>
        internal static GeoPolylineArc2 CurlingBack()
            => Chain(new[] { P(0, 0), P(10, 0), P(2.4529041977722796, -6.560590289905073) }, 0.0, 0.5);

        /// <summary>A hairpin 1 wide: 10 along x, 1 down, and 9 back, ending 1 short of the start.</summary>
        internal static GeoPoint2[] Hairpin() => new[] { P(0, 0), P(10, 0), P(10, -1), P(1, -1) };

        #endregion

        #region S5: arcs whose radius runs out

        /// <summary>
        /// The rectangle 100 by 30 from the origin with its right side bowed 6 inwards (bulge -0.4: radius 21.75 about
        /// (115.75, 15)), so that growing it by 40 runs that side's radius out.
        /// </summary>
        internal static GeoPolygonArc2 RectangleWithBowedInSide()
            => Loop(new[] { P(0, 0), P(100, 0), P(100, 30), P(0, 30) }, 0.0, -0.4, 0.0, 0.0);

        #endregion

        #region S6: corners that open

        /// <summary>The square 10 by 10 from the origin, counter-clockwise.</summary>
        internal static GeoPoint2[] Square10() => new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 10) };

        /// <summary>The triangle with a 10 degree tip at the origin, its two long sides 100.</summary>
        internal static GeoPoint2[] TenDegreeTip() => new[] { P(0, 0), P(100, 0), P(98.4807753012208, 17.364817766693) };

        #endregion

        #region S6a: corners that open beside an arc

        /// <summary>
        /// The square 10 by 10 from the origin, counter-clockwise, its right side an arc of 45 degrees bowed out (bulge
        /// tan(pi / 16): radius 13.06563 about (-2.07107, 5)). It leaves (10, 0) at 67.5 degrees and reaches (10, 10) at
        /// 112.5, so both corners beside it turn 67.5 degrees.
        /// </summary>
        internal static GeoPolygonArc2 SquareWithItsRightSideBowedOut()
            => Loop(Square10(), 0.0, Math.Tan(Math.PI / 16.0), 0.0, 0.0);

        /// <summary>
        /// The chain from (100, 0) to the origin and on round an arc of bulge 0.05 (radius 501.25, 11.45 degrees), 100 long,
        /// that leaves the origin at 10 degrees: a 10 degree tip whose second leg curves away from the first.
        /// </summary>
        internal static GeoPolylineArc2 TenDegreeTipWithAnArcLeg()
        {
            double chord = Math.PI / 18.0 + 2.0 * Math.Atan(0.05);
            return Chain(new[] { P(100, 0), P(0, 0), P(100 * Math.Cos(chord), 100 * Math.Sin(chord)) }, 0.0, 0.05);
        }

        /// <summary>
        /// The chain 100 along x from the origin, then clockwise round an arc of bulge -0.9 (radius 36.25582 about
        /// (123.16667, 27.88889), 167.95 degrees) to (140, 60): the corner (100, 0) turns 140.28 degrees to the left.
        /// </summary>
        internal static GeoPolylineArc2 StraightIntoATightArc()
            => Chain(new[] { P(0, 0), P(100, 0), P(140, 60) }, 0.0, -0.9);

        #endregion

        #region S7: straight chains

        /// <summary>
        /// A zigzag of <paramref name="count"/> vertices, 2 apart along x and 3 high: every valley an inside corner of 67.4
        /// degrees on the left.
        /// </summary>
        internal static GeoPolyline2 Zigzag(int count)
        {
            var points = new GeoPoint2[count];
            for (int i = 0; i < count; i++) points[i] = P(2.0 * i, i % 2 == 0 ? 0.0 : 3.0);
            return new GeoPolyline2(points);
        }

        #endregion
    }
}
