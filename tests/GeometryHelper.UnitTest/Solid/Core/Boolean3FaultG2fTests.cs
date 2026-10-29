using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Booleans of a cracked U and a cracked box in and about its notch, whose cells planes crossed and could not cut: what
    /// such a cell holds on the wrong side of the planes is weighed against the tolerance times the area of what the
    /// boolean gives, and a result holding more so is worked out another way or not at all.
    /// </summary>
    public class Boolean3FaultG2fTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

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
        /// apart at y = 0, nought at y = 10.
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

        // The cracked U and the cracked box its cracks lie under the planes of, of a case (the U's notch from left to right
        // down to floor, the box x0..x1, y0..y1, z0..z1, its cracks' slant and gap), as built, turned about (1, 2, 3) by 0.7,
        // or turned and moved some 96 metres off; with the volumes of the U, the box and their common part.
        private static void Bodies(double[] c, int place, out GeoSolid3 u, out GeoSolid3 box, out double ofU, out double ofBox, out double shared)
        {
            double left = c[0], right = c[1], floor = c[2], x0 = c[3], y0 = c[4], z0 = c[5], x1 = c[6], y1 = c[7], z1 = c[8];
            u = CrackedU(left, right, floor, x0, x1, z0, c[9], c[10]);
            box = CrackedBox(x0, y0, z0, x1, y1, z1, new[] { left, right }, new[] { floor, 10.0 }, c[9], c[10]);

            if (place > 0)
            {
                GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(1, 2, 3), 0.7);

                if (place == 2)
                {
                    turn = GeoTransform3.Translation(new GeoVector3(52000, -71000, 38000)) * turn;
                }

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

        // The box dips 0.088 into the U's left arm over its floor, 0.52 through the floor of the notch, and into both arms and
        // the floor at once; then 0.028 into the left arm.
        private static readonly double[][] Dipping =
        {
            new[] { 9.831254792321126, 20.755215746702262, 6.95927136855166, 9.743056404610655, -1, 7.104404962064886, 19.139957489185015, 11, 11.34425965992001, 0.004060055014866619, 1.6527461274192402E-05 },
            new[] { 9.001576882322121, 18.308039685854705, 4.155695153472803, 10.125711419072799, 3.7333651016156026, 3.6320367093812846, 16.822596754470187, 6.776817525167399, 14.578653996148452, 0.005766650169674883, 7.222208116626426E-05 },
            new[] { 10.990523764393537, 19.713903291949027, 6.643715063409747, 10.295542171222877, 1.901233025780522, 6.4998677578754105, 19.825917890819685, 11, 11.384676223799902, 0.0032609160211616818, 1.739178200016202E-05 },
            new[] { 10.648314857226012, 19.374989923730023, 5.535605032712037, 10.620669045169219, -1, 6.951269459748301, 18.71476751194092, 11, 12.905254750468421, 0.027730188153931353, 5.715732746215051E-05 },
        };

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 0)]
        [InlineData(2, 1)]
        public void ACrackedBoxLessTheCrackedUItDipsInto_IsRightOrNotWorkedOut(int which, int place)
        {
            // The box's cells, cut by every plane of both, were judged apart by the U's planes with no open result to check
            // them by, and the box was left whole: 478.10 where 475.54 is left, 223.11 where 212.44 is, and 423.58 where
            // 390.55 is. They hold 2.55, 10.67 and 33.04 on the wrong side, more than the tolerance times the area of what is
            // left, 0.41, 0.25 and 0.36.
            Bodies(Dipping[which], place, out GeoSolid3 u, out GeoSolid3 box, out _, out double ofBox, out double shared);
            bool made = Boolean3.TrySubtract(box, u, out GeoSolid3 rest, Fine, out BooleanOutcome outcome);
            AssertRightOrNotMade(made, rest, outcome, ofBox - shared);
        }

        [Fact]
        public void ACrackedBoxDippingAHairIntoAnArmLessTheU_IsRightOrNotWorkedOut()
        {
            // The box takes 0.84 of the U's arm, 0.028 of it wide: the U cut by the box's planes was one cell judged alike on
            // both sides of them, the 0.84 within the tolerance times the U's area, 1.39, and said the box took nothing of it,
            // which left the box whole, 578.30 where 577.46 is left, more than the tolerance times the box's area, 0.45.
            Bodies(Dipping[3], 0, out GeoSolid3 u, out GeoSolid3 box, out _, out double ofBox, out double shared);
            bool made = Boolean3.TrySubtract(box, u, out GeoSolid3 rest, Fine, out BooleanOutcome outcome);
            AssertRightOrNotMade(made, rest, outcome, ofBox - shared);
        }

        /// <summary>
        /// A comb x 0..10 + 20n, y 0..10, z 0..10 less n notches x 10 + 20i .. 20 + 20i down to floor, whose bottom is faces
        /// meeting along cracks from x = c - slant to c + slant across y, one for each c of the cracks given in order, and whose
        /// outer sides are two faces each meeting along a crack from z = h - slant to h + slant; the copies of each crack's edge
        /// gap apart at y = 0.
        /// </summary>
        private static GeoSolid3 CrackedComb(int n, double floor, double[] cracks, double h, double slant, double gap)
        {
            double w = 10 + 20 * n, za = h - slant, zb = h + slant;
            var faces = new List<GeoFace3>();

            for (int k = 0; k <= cracks.Length; k++)
            {
                double a0 = k == 0 ? 0 : cracks[k - 1] - slant + gap, a1 = k == cracks.Length ? w : cracks[k] - slant;
                double b0 = k == 0 ? 0 : cracks[k - 1] + slant, b1 = k == cracks.Length ? w : cracks[k] + slant;
                faces.Add(Facing(0, 0, -1, P(a0, 0, 0), P(a1, 0, 0), P(b1, 10, 0), P(b0, 10, 0)));
            }

            faces.Add(Facing(-1, 0, 0, P(0, 0, 0), P(0, 10, 0), P(0, 10, zb), P(0, 0, za)));
            faces.Add(Facing(-1, 0, 0, P(0, 0, za + gap), P(0, 10, zb), P(0, 10, 10), P(0, 0, 10)));
            faces.Add(Facing(1, 0, 0, P(w, 0, 0), P(w, 10, 0), P(w, 10, zb), P(w, 0, za)));
            faces.Add(Facing(1, 0, 0, P(w, 0, za + gap), P(w, 10, zb), P(w, 10, 10), P(w, 0, 10)));

            for (int i = 0; i <= n; i++)
            {
                faces.Add(Facing(0, 0, 1, P(20 * i, 0, 10), P(20 * i + 10, 0, 10), P(20 * i + 10, 10, 10), P(20 * i, 10, 10)));
            }

            for (int i = 0; i < n; i++)
            {
                double l = 10 + 20 * i, r = 20 + 20 * i;
                faces.Add(Facing(1, 0, 0, P(l, 0, floor), P(l, 0, 10), P(l, 10, 10), P(l, 10, floor)));
                faces.Add(Facing(-1, 0, 0, P(r, 0, floor), P(r, 10, floor), P(r, 10, 10), P(r, 0, 10)));
                faces.Add(Facing(0, 0, 1, P(l, 0, floor), P(l, 10, floor), P(r, 10, floor), P(r, 0, floor)));
            }

            var front = new List<GeoPoint3> { P(0, 0, 0) };
            var back = new List<GeoPoint3> { P(0, 10, 0) };

            foreach (double c in cracks)
            {
                front.Add(P(c - slant, 0, 0));
                front.Add(P(c - slant + gap, 0, 0));
                back.Add(P(c + slant, 10, 0));
            }

            front.AddRange(new[] { P(w, 0, 0), P(w, 0, za), P(w, 0, za + gap), P(w, 0, 10) });
            back.AddRange(new[] { P(w, 10, 0), P(w, 10, zb), P(w, 10, 10) });

            for (int i = n - 1; i >= 0; i--)
            {
                double l = 10 + 20 * i, r = 20 + 20 * i;
                front.AddRange(new[] { P(r, 0, 10), P(r, 0, floor), P(l, 0, floor), P(l, 0, 10) });
                back.AddRange(new[] { P(r, 10, 10), P(r, 10, floor), P(l, 10, floor), P(l, 10, 10) });
            }

            front.AddRange(new[] { P(0, 0, 10), P(0, 0, za + gap), P(0, 0, za) });
            back.AddRange(new[] { P(0, 10, 10), P(0, 10, zb) });
            faces.Add(Facing(0, -1, 0, front.ToArray()));
            faces.Add(Facing(0, 1, 0, back.ToArray()));
            return new GeoSolid3(faces);
        }

        [Fact]
        public void ACrackedCombOfTwoNotchesLessTwoBoxesDippingIntoItsTeeth_IsRightOrNotWorkedOut()
        {
            // A comb 50 by 10 by 10 with two notches, its bottom cracked under the sides of two boxes taken as one body, each
            // dipping 0.088 into the tooth left of a notch. The comb is one cell to the boxes' planes, crossed by their four sides
            // and the floor they share, five planes, more than were read together: it was left with the 2.55 of each box in it,
            // 4 391.85 where 4 386.75 is left.
            const int n = 2;
            const double floor = 6.95927136855166, z0 = 7.104404962064886, z1 = 11.34425965992001, dip = 0.088198387711, wide = 9.308702697;
            const double slant = 0.004060055014866619, gap = 1.6527461274192402E-05;
            double[] cracks = Enumerable.Range(0, n).SelectMany(i => new[] { 10 + 20 * i - dip, 10 + 20 * i + wide }).ToArray();
            GeoSolid3 comb = CrackedComb(n, floor, cracks, z0, slant, gap);
            var faces = new List<GeoFace3>();

            for (int i = 0; i < n; i++)
            {
                double left = 10 + 20 * i;
                faces.AddRange(CrackedBox(left - dip, -1, z0, left + wide, 11, z1, new[] { left, left + 10 }, new[] { floor, 10.0 }, slant, gap).Faces);
            }

            var boxes = new GeoSolid3(faces);
            double expected = (10 + 20 * n) * 100 - n * 100 * (10 - floor) - n * dip * 10 * (10 - z0);
            Assert.True(comb.Validate(Fine).IsValid);
            bool made = Boolean3.TrySubtract(comb, boxes, out GeoSolid3 rest, Fine, out BooleanOutcome outcome);
            AssertRightOrNotMade(made, rest, outcome, expected);
        }

        // The box's floor 0.014, 0.020 and 0.015 under the floor of the notch, across it: the two share a slab that thin.
        private static readonly double[][] Under =
        {
            new[] { 9.927086132544598, 18.33932695367342, 2.3833997786899097, 11.015323563811986, 3.020751816695906, 2.3693486927400107, 16.276511131122945, 11, 13.116749634554958, 0.01777688523291731, 6.587849079639819E-05 },
            new[] { 11.689633445669726, 18.102323953109945, 2.754587564503116, 13.177525095584581, -1, 2.734543579832904, 16.486405791149664, 11, 15.578553733685311, 0.006714906362388451, 4.1989139242889985E-05 },
            new[] { 10.523773106990276, 18.21437684084027, 2.4513945013524006, 12.095047225614566, -1, 2.4361537425481496, 16.71489217645251, 7.569705017641981, 12.122244946249875, 0.012384986722811452, 4.662460119570792E-05 },
        };

        [Theory]
        [InlineData(0, 1)]
        [InlineData(0, 2)]
        [InlineData(1, 1)]
        [InlineData(1, 2)]
        [InlineData(2, 1)]
        [InlineData(2, 2)]
        public void TheThinSlabACrackedBoxSharesWithTheFloorOfTheNotchTurned_IsTheCommonPart(int which, int place)
        {
            // Axis-aligned, the planes cut both bodies cleanly. Turned, or turned and far off, the U cut by the box's planes
            // was one cell judged alike on both sides of those that could not cut it, beyond the box, and kept nothing; the box
            // cut by the U's planes kept the slab, 0.52, 0.66 and 0.53, and was not taken, neither way being judged apart
            // across a plane: the common part was nothing.
            Bodies(Under[which], place, out GeoSolid3 u, out GeoSolid3 box, out _, out _, out double shared);

            foreach ((GeoSolid3 first, GeoSolid3 second) in new[] { (u, box), (box, u) })
            {
                Assert.True(Boolean3.TryIntersect(first, second, out GeoSolid3 common, Fine, out BooleanOutcome outcome));
                Assert.Equal(BooleanOutcome.Made, outcome);
                AssertRightOrNotMade(true, common, outcome, shared);
            }
        }
    }
}
