using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A difference, a common part and a union asked at nought of a body that does not close within the rounding of its
    /// coordinates: a cube 10 across cracked 0.0001 wide along its top, its bottom and a side. Worked out within the rounding,
    /// nothing such a body gives is checked on the way, and what the tolerance alone did not work out came out wrong and was
    /// trusted: the cube less a box inside it came back whole, joined to a box through its corner it held 125 too much, and
    /// its common part with a slab through it was the whole cube. What does not hold up is not worked out now, where the
    /// tolerance alone gives nothing either.
    /// </summary>
    public class RoundingLevelG1zTests
    {
        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        /// <summary>No turn.</summary>
        private static readonly double[] Upright = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        /// <summary>A turn about space, rows of a matrix.</summary>
        private static readonly double[] Turned =
        {
            -0.4666789604312006, 0.8516652585856114, -0.23848906727386426,
            0.8627545427570836, 0.3790510820157432, -0.33462647261492956,
            -0.19459020233010133, -0.361920660561121, -0.9116732356585512,
        };

        /// <summary>Another turn about space, rows of a matrix.</summary>
        private static readonly double[] TurnedOtherwise =
        {
            -0.04029114060306993, -0.9684804885766289, -0.24580920901235115,
            0.24840464323125316, -0.24799421774904123, 0.9363727896432035,
            -0.9678180393080393, -0.023332621143266508, 0.25056702013738225,
        };

        private static GeoPoint3 P(double[] turn, double x, double y, double z)
            => new GeoPoint3(
                turn[0] * x + turn[1] * y + turn[2] * z,
                turn[3] * x + turn[4] * y + turn[5] * z,
                turn[6] * x + turn[7] * y + turn[8] * z);

        private static GeoFace3 F(params GeoPoint3[] corners) => new GeoFace3(new GeoPolygon3(corners));

        /// <summary>
        /// A cube 0 to 10, turned, cracked 0.0001 wide along its top, its bottom and its side at x = 0, each crack leaning
        /// 0.02 across the face: its faces close within 0.0001 and not within nought.
        /// </summary>
        private static GeoSolid3 CrackedCube(double[] turn)
        {
            const double gap = 0.0001;
            const double skew = 0.01;
            GeoPoint3 Q(double x, double y, double z) => P(turn, x, y, z);
            GeoPoint3 a = Q(5 - skew, 0, 10), a2 = Q(5 - skew + gap, 0, 10), b = Q(5 + skew, 10, 10);
            GeoPoint3 c = Q(0, 5 - skew, 0), c2 = Q(0, 5 - skew + gap, 0), d = Q(10, 5 + skew, 0);
            GeoPoint3 e = Q(0, 0, 5 - skew), e2 = Q(0, 0, 5 - skew + gap), f = Q(0, 10, 5 + skew);

            return new GeoSolid3(
                F(Q(0, 0, 10), a, b, Q(0, 10, 10)),
                F(a2, Q(10, 0, 10), Q(10, 10, 10), b),
                F(Q(0, 0, 0), c, d, Q(10, 0, 0)),
                F(c2, Q(0, 10, 0), Q(10, 10, 0), d),
                F(e, f, Q(0, 10, 0), c2, c, Q(0, 0, 0)),
                F(Q(0, 0, 10), Q(0, 10, 10), f, e2),
                F(Q(10, 0, 0), d, Q(10, 10, 0), Q(10, 10, 10), Q(10, 0, 10)),
                F(Q(0, 0, 0), Q(10, 0, 0), Q(10, 0, 10), a2, a, Q(0, 0, 10), e2, e),
                F(f, Q(0, 10, 10), b, Q(10, 10, 10), Q(10, 10, 0), Q(0, 10, 0)));
        }

        /// <summary>
        /// A body of six four-sided faces on eight corners, turned: the corner at i, j, k of 0 or 1 along each of three
        /// directions, faces wound outwards where the three turn right-handed.
        /// </summary>
        private static GeoSolid3 Hexahedron(double[] turn, Func<int, int, int, double[]> corner)
        {
            GeoPoint3 C(int i, int j, int k)
            {
                double[] q = corner(i, j, k);
                return P(turn, q[0], q[1], q[2]);
            }

            return new GeoSolid3(
                F(C(0, 0, 0), C(0, 0, 1), C(0, 1, 1), C(0, 1, 0)),
                F(C(1, 0, 0), C(1, 1, 0), C(1, 1, 1), C(1, 0, 1)),
                F(C(0, 0, 0), C(1, 0, 0), C(1, 0, 1), C(0, 0, 1)),
                F(C(0, 1, 0), C(0, 1, 1), C(1, 1, 1), C(1, 1, 0)),
                F(C(0, 0, 0), C(0, 1, 0), C(1, 1, 0), C(1, 0, 0)),
                F(C(0, 0, 1), C(1, 0, 1), C(1, 1, 1), C(0, 1, 1)));
        }

        /// <summary>A box from and to a point, turned by an angle about the x axis, then by a turn.</summary>
        private static GeoSolid3 Box(double[] turn, double angle, double x0, double y0, double z0, double x1, double y1, double z1)
        {
            double cos = Math.Cos(angle), sin = Math.Sin(angle);

            return Hexahedron(turn, (i, j, k) =>
            {
                double x = i == 0 ? x0 : x1, y = j == 0 ? y0 : y1, z = k == 0 ? z0 : z1;
                return new[] { x, y * cos - z * sin, y * sin + z * cos };
            });
        }

        [Fact]
        public void ACrackedCubeLessABoxTurnedInsideIt_AtNought_IsNotWorkedOut()
        {
            // The box 3 across inside the cube, turned 0.3 about x: within the rounding the difference came back as the whole
            // cube, 999.998, where 973 is left, and was trusted; within nought alone it was not worked out. Their common part,
            // the box whole, holds up.
            GeoSolid3 cube = CrackedCube(Upright);
            GeoSolid3 box = Box(Upright, 0.3, 3, 3, 3, 6, 6, 6);

            Assert.False(Boolean3.TrySubtract(cube, box, out GeoSolid3 left, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            Assert.Null(left);

            Assert.True(Boolean3.TryIntersect(cube, box, out GeoSolid3 common, Nought, out outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(27, common.GetVolume(Nought), 1E-9);
        }

        [Fact]
        public void ACrackedCubeTurnedJoinedToABoxThroughItsCorner_AtNought_IsNotWorkedOut()
        {
            // The box 7.5 by 6.2 by 5 through the corner at the origin, its floor 0.003 below the cube's: within the rounding
            // the union held 1 232.638, where the two less their common part, 125, hold 1 107.64, and was trusted; within
            // nought alone it was not worked out. The difference and the common part hold up and stay as they were.
            GeoSolid3 cube = CrackedCube(Turned);
            GeoSolid3 box = Box(Turned, 0, -2.5, -1.2, -0.003, 5, 5, 5);

            Assert.False(Boolean3.TryUnion(cube, box, out GeoSolid3 union, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            Assert.Null(union);

            Assert.True(Boolean3.TrySubtract(cube, box, out GeoSolid3 left, Nought, out outcome));
            Assert.Equal(875, left.GetVolume(Nought), 0.01);
            Assert.True(Boolean3.TryIntersect(cube, box, out GeoSolid3 common, Nought, out outcome));
            Assert.Equal(125, common.GetVolume(Nought), 1E-9);
        }

        [Fact]
        public void ACrackedCubeTurnedAndASlabLeaningThroughIt_AtNought_IsNotTheWholeCube()
        {
            // The slab lies between the planes where 0.7648 x - 0.6442 y - 0.0013 z is 0.5885 and 3.3394, 2.75 apart and a
            // thousandth off upright, cut to x from 5 to 11.5 and y from -1.2 to 5, and thousands long: it shares 76.79 with
            // the cube. Within the rounding the common part came back as the whole cube, 999.998, and was trusted; within
            // nought alone it was not worked out.
            double[] x = { 4.994000304893591, 11.498810735238163 };
            double[] y = { -1.2363702722212162, 5.005346466681615 };
            double[] c = { 0.5885214529754836, 3.339384974684993 };
            double[] n = { 0.7648415259367669, -0.6442171301921897, -0.0013150550016997737 };
            GeoSolid3 cube = CrackedCube(TurnedOtherwise);
            GeoSolid3 slab = Hexahedron(TurnedOtherwise, (i, j, k) => new[] { x[i], y[j], (c[k] - n[0] * x[i] - n[1] * y[j]) / n[2] });

            Assert.False(Boolean3.TryIntersect(cube, slab, out GeoSolid3 common, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            Assert.Null(common);
        }

        [Fact]
        public void ACrackedCubeLessABoxThroughItsTop_AtNought_LeavesWhatTheBoxLeaves()
        {
            // The box 5 by 5 from the middle of the cube up through its top: the difference, the common part and the union of
            // the cracked cube hold up and are made, within what the cracks move of the clean cube's 875, 125 and 1 075.
            GeoSolid3 cube = CrackedCube(Upright);
            GeoSolid3 box = Box(Upright, 0, 2, 2, 5, 7, 7, 13);

            Assert.True(Boolean3.TrySubtract(cube, box, out GeoSolid3 left, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(875, left.GetVolume(Nought), 0.01);

            Assert.True(Boolean3.TryIntersect(cube, box, out GeoSolid3 common, Nought, out outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(125, common.GetVolume(Nought), 0.01);

            Assert.True(Boolean3.TryUnion(cube, box, out GeoSolid3 union, Nought, out outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(1_075, union.GetVolume(Nought), 0.01);
        }
    }
}
