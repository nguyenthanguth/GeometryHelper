using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Booleans of a cracked U and a cracked box in and about its notch that were not worked out, the cut leaving a rim gap
    /// at a plane crossing a crack: worked out again on the bodies welded closed and their faces in one plane merged, they
    /// hold what they should; what was worked out as the bodies came is as it was.
    /// </summary>
    public class Boolean3CrackedGr3Tests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        // A face of the corners given, wound to face along (ox, oy, oz).
        private static GeoFace3 Facing(double ox, double oy, double oz, params GeoPoint3[] corners)
        {
            double nx = 0,
                ny = 0,
                nz = 0;

            for (int i = 0; i < corners.Length; i++)
            {
                GeoPoint3 p = corners[i],
                    q = corners[(i + 1) % corners.Length];
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
        private static GeoSolid3 CrackedU(
            double left,
            double right,
            double floor,
            double a,
            double c,
            double h,
            double slant,
            double gap
        )
        {
            double xa = a - slant,
                xb = a + slant,
                xc = c - slant,
                xd = c + slant,
                za = h - slant,
                zb = h + slant;
            return new GeoSolid3(
                new List<GeoFace3>
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
                    Facing(
                        0,
                        -1,
                        0,
                        P(0, 0, 0),
                        P(xa, 0, 0),
                        P(xa + gap, 0, 0),
                        P(xc, 0, 0),
                        P(xc + gap, 0, 0),
                        P(30, 0, 0),
                        P(30, 0, za),
                        P(30, 0, za + gap),
                        P(30, 0, 10),
                        P(right, 0, 10),
                        P(right, 0, floor),
                        P(left, 0, floor),
                        P(left, 0, 10),
                        P(0, 0, 10),
                        P(0, 0, za + gap),
                        P(0, 0, za)
                    ),
                    Facing(
                        0,
                        1,
                        0,
                        P(0, 10, 0),
                        P(xb, 10, 0),
                        P(xd, 10, 0),
                        P(30, 10, 0),
                        P(30, 10, zb),
                        P(30, 10, 10),
                        P(right, 10, 10),
                        P(right, 10, floor),
                        P(left, 10, floor),
                        P(left, 10, 10),
                        P(0, 10, 10),
                        P(0, 10, zb)
                    ),
                }
            );
        }

        /// <summary>
        /// A box x0..x1, y0..y1, z0..z1 whose bottom is faces meeting along cracks from x = p - slant to p + slant across y,
        /// one for each p among the planes given that lies within it, and whose sides x = x0 and x1 are faces meeting along
        /// cracks from z = q - slant to q + slant, the copies of each crack's edge gap apart at y = y0.
        /// </summary>
        private static GeoSolid3 CrackedBox(
            double x0,
            double y0,
            double z0,
            double x1,
            double y1,
            double z1,
            double[] xs,
            double[] zs,
            double slant,
            double gap
        )
        {
            double[] px = xs.Where(p => p - slant > x0 + 0.05 && p + slant < x1 - 0.05).OrderBy(p => p).ToArray();
            double[] qz = zs.Where(q => q - slant > z0 + 0.05 && q + slant < z1 - 0.05).OrderBy(q => q).ToArray();
            var faces = new List<GeoFace3>();

            for (int k = 0; k <= px.Length; k++)
            {
                double a0 = k == 0 ? x0 : px[k - 1] - slant + gap,
                    a1 = k == px.Length ? x1 : px[k] - slant;
                double b0 = k == 0 ? x0 : px[k - 1] + slant,
                    b1 = k == px.Length ? x1 : px[k] + slant;
                faces.Add(Facing(0, 0, -1, P(a0, y0, z0), P(a1, y0, z0), P(b1, y1, z0), P(b0, y1, z0)));
            }

            foreach (double x in new[] { x0, x1 })
            {
                for (int k = 0; k <= qz.Length; k++)
                {
                    double a0 = k == 0 ? z0 : qz[k - 1] - slant + gap,
                        a1 = k == qz.Length ? z1 : qz[k] - slant;
                    double b0 = k == 0 ? z0 : qz[k - 1] + slant,
                        b1 = k == qz.Length ? z1 : qz[k] + slant;
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

        private static double Overlap(double a0, double a1, double b0, double b1) =>
            System.Math.Max(0, System.Math.Min(a1, b1) - System.Math.Max(a0, b0));

        // The cracked U and the cracked box its cracks lie under the planes of, of a case (the U's notch from left to right
        // down to floor, the box x0..x1, y0..y1, z0..z1, its cracks' slant and gap), as built, turned about (1, 2, 3) by 0.7,
        // or turned and moved some 96 metres off; with the volumes of the U, the box and their common part.
        private static void Bodies(
            double[] c,
            int place,
            out GeoSolid3 u,
            out GeoSolid3 box,
            out double ofU,
            out double ofBox,
            out double shared
        )
        {
            double left = c[0],
                right = c[1],
                floor = c[2],
                x0 = c[3],
                y0 = c[4],
                z0 = c[5],
                x1 = c[6],
                y1 = c[7],
                z1 = c[8];
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
            shared =
                Overlap(0, 30, x0, x1) * across * Overlap(0, 10, z0, z1)
                - Overlap(left, right, x0, x1) * across * Overlap(floor, 10, z0, z1);
        }

        // The box dips 0.088 into the U's left arm over its floor, 0.52 through the floor of the notch, and into both arms and
        // the floor at once; then 0.028 into the left arm.
        private static readonly double[][] Dipping =
        {
            new[]
            {
                9.831254792321126,
                20.755215746702262,
                6.95927136855166,
                9.743056404610655,
                -1,
                7.104404962064886,
                19.139957489185015,
                11,
                11.34425965992001,
                0.004060055014866619,
                1.6527461274192402E-05,
            },
            new[]
            {
                9.001576882322121,
                18.308039685854705,
                4.155695153472803,
                10.125711419072799,
                3.7333651016156026,
                3.6320367093812846,
                16.822596754470187,
                6.776817525167399,
                14.578653996148452,
                0.005766650169674883,
                7.222208116626426E-05,
            },
            new[]
            {
                10.990523764393537,
                19.713903291949027,
                6.643715063409747,
                10.295542171222877,
                1.901233025780522,
                6.4998677578754105,
                19.825917890819685,
                11,
                11.384676223799902,
                0.0032609160211616818,
                1.739178200016202E-05,
            },
            new[]
            {
                10.648314857226012,
                19.374989923730023,
                5.535605032712037,
                10.620669045169219,
                -1,
                6.951269459748301,
                18.71476751194092,
                11,
                12.905254750468421,
                0.027730188153931353,
                5.715732746215051E-05,
            },
        };

        // Made, a result is valid and holds what it should within the tolerance times its area.
        private static void AssertRight(
            bool made,
            GeoSolid3 result,
            BooleanOutcome outcome,
            double expected,
            Tolerance tolerance
        )
        {
            Assert.True(made);
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.True(result.Validate(tolerance).IsValid);
            double bound = tolerance.EqualPoint * result.GetSurfaceArea(tolerance);
            Assert.InRange(result.GetVolume(tolerance), expected - bound, expected + bound);
        }

        // A box x0..x1, y0..y1, z0..z1, whole.
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1) =>
            CrackedBox(x0, y0, z0, x1, y1, z1, new double[0], new double[0], 0, 0);

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 1)]
        [InlineData(0, 2)]
        [InlineData(1, 0)]
        [InlineData(1, 1)]
        [InlineData(1, 2)]
        [InlineData(2, 0)]
        [InlineData(2, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 0)]
        [InlineData(3, 1)]
        [InlineData(3, 2)]
        public void ACrackedBoxLessTheCrackedUItDipsInto_IsWorkedOutOnTheBodiesWelded(int which, int place)
        {
            // Not worked out as they came: the box less the U leaves 475.54, 212.44, 390.55 and 577.46.
            Bodies(
                Dipping[which],
                place,
                out GeoSolid3 u,
                out GeoSolid3 box,
                out _,
                out double ofBox,
                out double shared
            );
            bool made = Boolean3.TrySubtract(box, u, out GeoSolid3 rest, Fine, out BooleanOutcome outcome);
            AssertRight(made, rest, outcome, ofBox - shared, Fine);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 2)]
        [InlineData(1, 1)]
        [InlineData(2, 0)]
        [InlineData(2, 2)]
        public void TheCommonPartAndTheUnionOfACrackedUAndACrackedBoxDippingIntoIt_AreWorkedOutOnTheBodiesWelded(
            int which,
            int place
        )
        {
            // Not worked out as they came: the box dipping 0.088 into the U's arm shares 2.55 with it, and the two join into
            // 3 143.38.
            Bodies(
                Dipping[which],
                place,
                out GeoSolid3 u,
                out GeoSolid3 box,
                out double ofU,
                out double ofBox,
                out double shared
            );
            bool common = Boolean3.TryIntersect(u, box, out GeoSolid3 part, Fine, out BooleanOutcome commonOutcome);
            AssertRight(common, part, commonOutcome, shared, Fine);
            bool joined = Boolean3.TryUnion(u, box, out GeoSolid3 union, Fine, out BooleanOutcome unionOutcome);
            AssertRight(joined, union, unionOutcome, ofU + ofBox - shared, Fine);
        }

        [Theory]
        [InlineData(1E7)]
        [InlineData(1E8)]
        [InlineData(1E9)]
        public void ACrackedBoxLessTheCrackedUItDipsIntoFarOut_IsWorkedOutOnTheBodiesWeldedInTheFrame(double far)
        {
            // Turned and moved far out, the two are worked out near the origin, and welded there.
            Bodies(Dipping[0], 1, out GeoSolid3 u, out GeoSolid3 box, out _, out double ofBox, out double shared);
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(far, -0.7 * far, 0.4 * far));
            bool made = Boolean3.TrySubtract(
                box.TransformBy(move),
                u.TransformBy(move),
                out GeoSolid3 rest,
                Fine,
                out BooleanOutcome outcome
            );
            AssertRight(made, rest, outcome, ofBox - shared, Fine);
        }

        [Fact]
        public void ACrackedBoxLessTheCrackedUWithAnOpeningClearOfTheBox_IsWorkedOutOnTheBodiesWelded()
        {
            // The opening of the U, a cube of 3 in its right arm, shares no space with the box and is left out; the box less
            // the U leaves 475.54.
            Bodies(Dipping[0], 1, out GeoSolid3 u, out GeoSolid3 box, out _, out double ofBox, out double shared);
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(1, 2, 3), 0.7);
            var holed = new GeoSolid3(u.Faces, new[] { Box(24, 3, 3, 27, 6, 6).TransformBy(turn) });
            bool made = Boolean3.TrySubtract(box, holed, out GeoSolid3 rest, Fine, out BooleanOutcome outcome);
            AssertRight(made, rest, outcome, ofBox - shared, Fine);
        }

        [Fact]
        public void ACrackedBoxWithAnOpeningLessTheCrackedU_IsAsItWasWithTheOpeningCutIn()
        {
            // Worked out as the two came, as before: the box's opening, 2 by 3 by 0.7 under its top, clear of the U, is cut
            // in, and 471.34 is left.
            Bodies(Dipping[0], 1, out GeoSolid3 u, out GeoSolid3 box, out _, out double ofBox, out double shared);
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(1, 2, 3), 0.7);
            var holed = new GeoSolid3(box.Faces, new[] { Box(14, 3, 10.5, 16, 6, 11.2).TransformBy(turn) });
            bool made = Boolean3.TrySubtract(holed, u, out GeoSolid3 rest, Fine, out BooleanOutcome outcome);
            AssertRight(made, rest, outcome, ofBox - shared - 2 * 3 * 0.7, Fine);
            Assert.Empty(rest.Openings);
        }

        [Fact]
        public void ACrackedBoxLessTheCrackedUAtNought_IsStillNotWorkedOut()
        {
            // At nought a crack is a crack, and nothing is welded.
            var nought = new Tolerance(0, 0, 0, 0);
            Bodies(Dipping[0], 0, out GeoSolid3 u, out GeoSolid3 box, out _, out _, out _);
            Assert.False(Boolean3.TrySubtract(box, u, out _, nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
        }
    }
}
