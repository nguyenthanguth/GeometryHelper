using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A cell that is not convex, crossed by planes that could not cut it, is misjudged where the other body takes a part of
    /// it no one or two of the planes part from the rest, and the middles of the parts they do part from it lie outside its
    /// material: a U less a box dipping into its notch.
    /// </summary>
    public class MisjudgedConcaveCellTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(P(x0, y0, z0), P(x1, y1, z1)).ToObb().ToSolid();

        // A face of the corners given, wound to face along (ox, oy, oz).
        private static GeoFace3 Facing(double ox, double oy, double oz, params GeoPoint3[] corners)
        {
            double nx = 0, ny = 0, nz = 0;

            for (int i = 0; i < corners.Length; i++)
            {
                GeoPoint3 p = corners[i], q = corners[(i + 1) % corners.Length];
                nx += (p.Y - q.Y) * (p.Z + q.Z);
                ny += (p.Z - q.Z) * (p.X + q.X);
                nz += (p.X - q.X) * (p.Y + q.Y);
            }

            if (nx * ox + ny * oy + nz * oz < 0)
            {
                System.Array.Reverse(corners);
            }

            return new GeoFace3(new GeoPolygon3(corners));
        }

        /// <summary>
        /// A U, x 0..30, y 0..10 and z 0..10 less a notch x from left to right down to floor, whose bottom is three faces
        /// meeting along cracks from x = a - slant to a + slant and from c - slant to c + slant across y, and whose outer sides
        /// are two faces each meeting along a crack from z = h - slant to h + slant; the two copies of each crack's edge are gap
        /// apart at y = 0, nought at y = 10. Closed within the default tolerance where the gap is.
        /// </summary>
        private static GeoSolid3 CrackedU(double left, double right, double floor, double a, double c, double h, double slant, double gap)
        {
            double xa = a - slant, xb = a + slant, xc = c - slant, xd = c + slant, za = h - slant, zb = h + slant;
            return new GeoSolid3(new List<GeoFace3>
            {
                Facing(0, 0, -1, P(0, 0, 0), P(xa, 0, 0), P(xb, 10, 0), P(0, 10, 0)),
                Facing(0, 0, -1, P(xa + gap, 0, 0), P(xc, 0, 0), P(xd, 10, 0), P(xb, 10, 0)),
                Facing(0, 0, -1, P(xc + gap, 0, 0), P(30, 0, 0), P(30, 10, 0), P(xd, 10, 0)),
                Facing(-1, 0, 0, P(0, 0, 0), P(0, 10, 0), P(0, 10, zb), P(0, 0, za)),
                Facing(-1, 0, 0, P(0, 0, za + gap), P(0, 10, zb), P(0, 10, 10), P(0, 0, 10)),
                Facing(1, 0, 0, P(30, 0, 0), P(30, 10, 0), P(30, 10, zb), P(30, 0, za)),
                Facing(1, 0, 0, P(30, 0, za + gap), P(30, 10, zb), P(30, 10, 10), P(30, 0, 10)),
                Facing(0, 0, 1, P(0, 0, 10), P(left, 0, 10), P(left, 10, 10), P(0, 10, 10)),
                Facing(0, 0, 1, P(right, 0, 10), P(30, 0, 10), P(30, 10, 10), P(right, 10, 10)),
                Facing(1, 0, 0, P(left, 0, floor), P(left, 0, 10), P(left, 10, 10), P(left, 10, floor)),
                Facing(-1, 0, 0, P(right, 0, floor), P(right, 10, floor), P(right, 10, 10), P(right, 0, 10)),
                Facing(0, 0, 1, P(left, 0, floor), P(left, 10, floor), P(right, 10, floor), P(right, 0, floor)),
                Facing(0, -1, 0, P(0, 0, 0), P(xa, 0, 0), P(xa + gap, 0, 0), P(xc, 0, 0), P(xc + gap, 0, 0), P(30, 0, 0), P(30, 0, za), P(30, 0, za + gap), P(30, 0, 10), P(right, 0, 10), P(right, 0, floor), P(left, 0, floor), P(left, 0, 10), P(0, 0, 10), P(0, 0, za + gap), P(0, 0, za)),
                Facing(0, 1, 0, P(0, 10, 0), P(xb, 10, 0), P(xd, 10, 0), P(30, 10, 0), P(30, 10, zb), P(30, 10, 10), P(right, 10, 10), P(right, 10, floor), P(left, 10, floor), P(left, 10, 10), P(0, 10, 10), P(0, 10, zb)),
            });
        }

        /// <summary>
        /// A box x0..x1, y0..y1, z0..z1 whose bottom is faces meeting along cracks from x = p - slant to p + slant across y,
        /// one for each p among the planes given that lies within it, and whose sides x = x0 and x1 are faces meeting along
        /// cracks from z = q - slant to q + slant, the copies of each crack's edge gap apart at y = y0.
        /// </summary>
        private static GeoSolid3 CrackedBox(double x0, double y0, double z0, double x1, double y1, double z1, double[] xs, double[] zs, double slant, double gap)
        {
            double[] px = xs.Where(p => p - slant > x0 + 0.05 && p + slant < x1 - 0.05).OrderBy(p => p).ToArray();
            double[] qz = zs.Where(q => q - slant > z0 + 0.05 && q + slant < z1 - 0.05).OrderBy(q => q).ToArray();
            var faces = new List<GeoFace3>();

            for (int k = 0; k <= px.Length; k++)
            {
                double a0 = k == 0 ? x0 : px[k - 1] - slant + gap, a1 = k == px.Length ? x1 : px[k] - slant;
                double b0 = k == 0 ? x0 : px[k - 1] + slant, b1 = k == px.Length ? x1 : px[k] + slant;
                faces.Add(Facing(0, 0, -1, P(a0, y0, z0), P(a1, y0, z0), P(b1, y1, z0), P(b0, y1, z0)));
            }

            foreach (double x in new[] { x0, x1 })
            {
                for (int k = 0; k <= qz.Length; k++)
                {
                    double a0 = k == 0 ? z0 : qz[k - 1] - slant + gap, a1 = k == qz.Length ? z1 : qz[k] - slant;
                    double b0 = k == 0 ? z0 : qz[k - 1] + slant, b1 = k == qz.Length ? z1 : qz[k] + slant;
                    faces.Add(Facing(x == x0 ? -1 : 1, 0, 0, P(x, y0, a0), P(x, y0, a1), P(x, y1, b1), P(x, y1, b0)));
                }
            }

            faces.Add(Facing(0, 0, 1, P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1)));
            var front = new List<GeoPoint3> { P(x0, y0, z0) };

            foreach (double p in px)
            {
                front.Add(P(p - slant, y0, z0));
                front.Add(P(p - slant + gap, y0, z0));
            }

            front.Add(P(x1, y0, z0));

            foreach (double q in qz)
            {
                front.Add(P(x1, y0, q - slant));
                front.Add(P(x1, y0, q - slant + gap));
            }

            front.Add(P(x1, y0, z1));
            front.Add(P(x0, y0, z1));

            foreach (double q in qz.Reverse())
            {
                front.Add(P(x0, y0, q - slant + gap));
                front.Add(P(x0, y0, q - slant));
            }

            faces.Add(Facing(0, -1, 0, front.ToArray()));
            var back = new List<GeoPoint3> { P(x0, y1, z0) };
            back.AddRange(px.Select(p => P(p + slant, y1, z0)));
            back.Add(P(x1, y1, z0));
            back.AddRange(qz.Select(q => P(x1, y1, q + slant)));
            back.Add(P(x1, y1, z1));
            back.Add(P(x0, y1, z1));
            back.AddRange(qz.Reverse().Select(q => P(x0, y1, q + slant)));
            faces.Add(Facing(0, 1, 0, back.ToArray()));
            return new GeoSolid3(faces);
        }

        private static double Overlap(double a0, double a1, double b0, double b1) => System.Math.Max(0, System.Math.Min(a1, b1) - System.Math.Max(a0, b0));

        [Fact]
        public void AUCellABoxDipsIntoAnArmOf_IsMisjudged()
        {
            // The box takes 5.5 of the left arm, x from 9.9 to 10 over z = 4.5, and none of x = 9.9, x = 19.5 and z = 4.5
            // cuts the U. No one plane or two part what the box takes from the rest, and the middle of the part over z = 4.5,
            // the two arms, lies in the notch, outside the material; the parts between x = 9.9 and 19.5, over z = 4.5 and
            // under it, are read too.
            GeoSolid3 u = CrackedU(10, 20, 4, 9.9, 19.5, 4.5, 0.01, 0.0001).TurnOutwards();
            GeoSolid3 box = Box(9.9, -1, 4.5, 19.5, 11, 15);
            var stuck = new List<GeoPlane3>
            {
                new GeoPlane3(P(9.9, 0, 0), GeoVector3.XAxis),
                new GeoPlane3(P(19.5, 0, 0), GeoVector3.XAxis),
                new GeoPlane3(P(0, 0, 4.5), GeoVector3.ZAxis),
            };

            Assert.True(u.IsClosed(Fine));
            Assert.True(Boolean3.Misjudged(new List<GeoSolid3> { u }, stuck, u, box, false, Fine));
        }

        // The three cases: the U's notch from left to right down to floor, the box x0..x1, y0..y1, z0..z1, its cracks' slant and
        // gap. The box takes 2.55 of the left arm, 0.088 of it wide; 10.67 of the floor, 0.52 of it deep; and 33.04 of both arms
        // and the floor, a U itself, its middle in the notch.
        private static readonly double[][] Cases =
        {
            new[] { 9.831254792321126, 20.755215746702262, 6.95927136855166, 9.743056404610655, -1, 7.104404962064886, 19.139957489185015, 11, 11.34425965992001, 0.004060055014866619, 1.6527461274192402E-05 },
            new[] { 9.001576882322121, 18.308039685854705, 4.155695153472803, 10.125711419072799, 3.7333651016156026, 3.6320367093812846, 16.822596754470187, 6.776817525167399, 14.578653996148452, 0.005766650169674883, 7.222208116626426E-05 },
            new[] { 10.990523764393537, 19.713903291949027, 6.643715063409747, 10.295542171222877, 1.901233025780522, 6.4998677578754105, 19.825917890819685, 11, 11.384676223799902, 0.0032609160211616818, 1.739178200016202E-05 },
        };

        // The cracked U and the cracked box its cracks lie under the planes of, of one of the cases, axis-aligned or both turned
        // about (1, 2, 3) by 0.7, with the volumes of the U, the box and their common part.
        private static void Bodies(int which, bool turned, out GeoSolid3 u, out GeoSolid3 box, out double ofU, out double ofBox, out double shared)
        {
            double[] c = Cases[which];
            double left = c[0], right = c[1], floor = c[2], x0 = c[3], y0 = c[4], z0 = c[5], x1 = c[6], y1 = c[7], z1 = c[8];
            u = CrackedU(left, right, floor, x0, x1, z0, c[9], c[10]);
            box = CrackedBox(x0, y0, z0, x1, y1, z1, new[] { left, right }, new[] { floor, 10.0 }, c[9], c[10]);

            if (turned)
            {
                GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(1, 2, 3), 0.7);
                u = u.TransformBy(turn);
                box = box.TransformBy(turn);
            }

            double across = Overlap(0, 10, y0, y1);
            ofU = 3000 - (right - left) * 10 * (10 - floor);
            ofBox = (x1 - x0) * (y1 - y0) * (z1 - z0);
            shared = Overlap(0, 30, x0, x1) * across * Overlap(0, 10, z0, z1) - Overlap(left, right, x0, x1) * across * Overlap(floor, 10, z0, z1);
        }

        // Made, a result is valid and holds what it should within the tolerance times its area; otherwise it is not worked out.
        private static void AssertRightOrNotMade(bool made, GeoSolid3 result, BooleanOutcome outcome, double expected)
        {
            if (!made)
            {
                Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
                return;
            }

            Assert.True(result.Validate(Fine).IsValid);
            double bound = Fine.EqualPoint * result.GetSurfaceArea(Fine);
            Assert.InRange(result.GetVolume(Fine), expected - bound, expected + bound);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(2, true)]
        public void ACrackedULessACrackedBoxDippingIntoItsNotch_HoldsWhatIsLeft(int which, bool turned)
        {
            // Read by no more than two of the box's planes at a time, the U was one cell lying beyond the box, left whole and
            // trusted: 2 667.83 where 2 665.28 is left, 2 456.10 where 2 445.43 is, and 2 707.22 where 2 674.18 is.
            Bodies(which, turned, out GeoSolid3 u, out GeoSolid3 box, out double ofU, out _, out double shared);
            Assert.True(Boolean3.TrySubtract(u, box, out GeoSolid3 rest, Fine, out BooleanOutcome outcome));
            AssertRightOrNotMade(true, rest, outcome, ofU - shared);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(2, true)]
        public void TheUnionOfACrackedUAndACrackedBoxDippingIntoItsNotch_IsRightOrNotMadeEitherWayRound(int which, bool turned)
        {
            // Each body's cells judged apart by the other's planes, the union of the box and the U counted what the two share
            // twice: 3 145.93 where the two hold 3 143.38, and 10.67 and 33.04 over in the other cases.
            Bodies(which, turned, out GeoSolid3 u, out GeoSolid3 box, out double ofU, out double ofBox, out double shared);

            foreach ((GeoSolid3 first, GeoSolid3 second) in new[] { (u, box), (box, u) })
            {
                bool made = Boolean3.TryUnion(first, second, out GeoSolid3 union, Fine, out BooleanOutcome outcome);
                AssertRightOrNotMade(made, union, outcome, ofU + ofBox - shared);
            }
        }

        [Theory(Skip = "Not fixed: the box's cells judged apart by the U's planes, with no open result to check them by, are taken; see the CHANGELOG, Unreleased.")]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ACrackedBoxLessTheCrackedUItDipsInto_IsRightOrNotMade(int which)
        {
            // The box is left whole, 478.10 where 475.54 is left, and 10.67 and 33.04 over in the other cases.
            Bodies(which, false, out GeoSolid3 u, out GeoSolid3 box, out _, out double ofBox, out double shared);
            bool made = Boolean3.TrySubtract(box, u, out GeoSolid3 rest, Fine, out BooleanOutcome outcome);
            AssertRightOrNotMade(made, rest, outcome, ofBox - shared);
        }
    }
}
