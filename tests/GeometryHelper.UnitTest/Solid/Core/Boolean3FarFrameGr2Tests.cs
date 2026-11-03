using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A union, a common part and a difference of bodies a hundred million and a thousand million out are worked out as near
    /// the origin. There a trillionth of the coordinates, which a boolean reads a corner on a plane within, came to a ten- or a
    /// hundred-thousandth and a thousandth, and walls of a body and its openings 0.0009 apart were cut in one plane: the L
    /// less the box held 0.07 too much, the box less the L 0.45 too little. Each answer is held to the rounding of the
    /// coordinates, 2^-52 of them, times the area of the two bodies, the most moving their corners can cost.
    /// </summary>
    public class Boolean3FarFrameGr2Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Hundredth = new Tolerance(0.01, 1E-5, 0.017453292519943295, 0.01);

        private static GeoPoint3 At(double far, double x, double y, double z) => new GeoPoint3(far + x, far + y, far + z);

        private static GeoSolid3 Box(double far, double x0, double y0, double z0, double x1, double y1, double z1)
            => GeoSolid3.Extrude(
                new GeoPolygon3(At(far, x0, y0, z0), At(far, x1, y0, z0), At(far, x1, y1, z0), At(far, x0, y1, z0)), new GeoVector3(0, 0, z1 - z0), Thousandth);

        /// <summary>An L 100 by 80, its legs 20 wide, 50 high, its corner at (far, far, far).</summary>
        private static GeoSolid3 Body(double far)
            => GeoSolid3.Extrude(
                new GeoPolygon3(At(far, 0, 0, 0), At(far, 100, 0, 0), At(far, 100, 20, 0), At(far, 20, 20, 0), At(far, 20, 80, 0), At(far, 0, 80, 0)),
                new GeoVector3(0, 0, 50),
                Thousandth);

        /// <summary>
        /// The L with a blind opening 13 by 8.8271 up from its floor 22 deep, and one 15.5 by 6.528 down from its top 37 deep;
        /// the far walls of the two stand 0.0009 apart, the openings 27 apart along them.
        /// </summary>
        private static GeoSolid3 L(double far, bool turnedRound)
        {
            GeoSolid3 up = Box(far, 35, 4.6, -7, 48, 13.4271, 22);
            GeoSolid3 down = Box(far, 75, 6.9, 13, 90.5, 13.428, 57);

            return Body(far).WithOpenings(turnedRound ? new[] { down, up } : new[] { up, down });
        }

        /// <summary>The L less what the openings take of it.</summary>
        private const double Held = 160_000 - 13 * 8.8271 * 22 - 15.5 * 6.528 * 37;

        /// <summary>A box 65 by 20 by 40 across both openings' far walls, standing 10 out of the L's foot.</summary>
        private static GeoSolid3 Tool(double far) => Box(far, 30, 10, 5, 95, 30, 45);

        private const double ToolVolume = 65 * 20 * 40;

        /// <summary>What the box and the L share: the box within the foot less what the openings take of it.</summary>
        private const double Shared = 65 * 10 * 40 - 13 * 3.4271 * 17 - 15.5 * 3.428 * 32;

        public static TheoryData<double, bool, double> Cases()
        {
            var cases = new TheoryData<double, bool, double>();

            foreach (double far in new[] { 1E8, 1E9 })
            {
                foreach (bool turnedRound in new[] { false, true })
                {
                    cases.Add(far, turnedRound, 1E-3);
                    cases.Add(far, turnedRound, 1E-2);
                }
            }

            return cases;
        }

        private static Tolerance Within(double tolerance) => tolerance == 1E-3 ? Thousandth : Hundredth;

        /// <summary>The area of a body's faces and of its openings'.</summary>
        private static double Area(GeoSolid3 solid) => solid.Faces.Sum(f => f.Area) + solid.Openings.Sum(Area);

        /// <summary>What rounding the corners of two bodies far out can cost a volume: 2^-52 of far times their area.</summary>
        private static double Rounding(double far, GeoSolid3 first, GeoSolid3 second) => far * Math.Pow(2, -52) * (Area(first) + Area(second));

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheLLessABoxAcrossBothOpenings_FarOut_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // Within a thousandth or a hundredth the L less the box held 130 189.39 one way round and 130 189.26 the other.
            Assert.True(L(far, turnedRound).TrySubtract(Tool(far), out GeoSolid3 left, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(Held - Shared, left.GetVolume(Within(tolerance)), Rounding(far, L(far, turnedRound), Tool(far)));
            Assert.True(left.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheBoxLessTheL_FarOut_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // The box less the L held 28 457.23 one way round and 28 457.88 the other.
            Assert.True(Tool(far).TrySubtract(L(far, turnedRound), out GeoSolid3 left, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(ToolVolume - Shared, left.GetVolume(Within(tolerance)), Rounding(far, L(far, turnedRound), Tool(far)));
            Assert.True(left.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheLJoinedToTheBox_FarOut_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // The two joined held 182 189.39 one way round and 182 189.26 the other.
            Assert.True(L(far, turnedRound).TryUnion(Tool(far), out GeoSolid3 union, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(Held + ToolVolume - Shared, union.GetVolume(Within(tolerance)), Rounding(far, L(far, turnedRound), Tool(far)));
            Assert.True(union.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void WhatTheLAndTheBoxShare_FarOut_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // The two shared 23 542.77 one way round and 23 542.12 the other.
            Assert.True(L(far, turnedRound).TryIntersect(Tool(far), out GeoSolid3 shared, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(Shared, shared.GetVolume(Within(tolerance)), Rounding(far, L(far, turnedRound), Tool(far)));
            Assert.True(shared.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [InlineData(1E8)]
        [InlineData(1E9)]
        public void TheLLessTheBoxAndAnother_OneAfterTheOther_FarOut_KeepsEachWallInItsOwnPlane(double far)
        {
            // Cut one after the other as the options say, the L less the box and a block 10 by 10 by 10 over its far end, which
            // takes the top 5 of it less what the opening down from the top takes there. Taken as the box alone was, the cut
            // left 0.07 too much.
            GeoSolid3 block = Box(far, 85, 5, 45, 95, 15, 55);
            double expected = Held - Shared - (10 * 10 * 5 - 5.5 * 6.528 * 5);

            Assert.True(L(far, false).TrySubtractAll(new[] { Tool(far), block }, out GeoSolid3 left, new SolidBooleanOptions(Thousandth), out SubtractReport report));

            Assert.Equal(2, report.Taken);
            Assert.Empty(report.Skipped);
            Assert.Equal(expected, left.GetVolume(Thousandth), 2 * Rounding(far, L(far, false), Tool(far)));
            Assert.True(left.Validate(Thousandth).IsValid);
        }

        [Theory]
        [InlineData(1E8)]
        [InlineData(1E9)]
        public void TheLLessABoxWithAContact_FarOut_KeepsEachWallInItsOwnPlane(double far)
        {
            // Worked out with the options, the box put onto the L within a hundredth: no face of the box lies so near.
            var options = new SolidBooleanOptions(Thousandth, 0.01);

            Assert.True(L(far, true).TrySubtract(Tool(far), out GeoSolid3 left, options, out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(Held - Shared, left.GetVolume(Thousandth), Rounding(far, L(far, true), Tool(far)));
        }

        /// <summary>
        /// The L with an opening 8 by 44.4996 down from its top 31.2 deep in its upright, its near wall 0.0004 off the plane of
        /// the L's inner wall along the foot, and one 34.3996 by 9.1 up from its floor 22.3 deep in its foot, its near wall
        /// 0.0004 off the plane of the L's inner wall along the upright.
        /// </summary>
        private static GeoSolid3 LWithWallsOffItsOwn(double far)
            => Body(far).WithOpenings(new[] { Box(far, 7, 20.0004, 18.8, 15, 64.5, 57), Box(far, 20.0004, 4.7, -7, 54.4, 13.8, 22.3) });

        private const double HeldOffItsOwn = 160_000 - 8 * 44.4996 * 31.2 - 34.3996 * 9.1 * 22.3;

        [Theory]
        [InlineData(1E7)]
        [InlineData(1E8)]
        [InlineData(1E9)]
        public void ThatLLessABox_FarOut_KeepsEachWallInItsOwnPlane(double far)
        {
            // The box over the corner of the L: x 3 to 60, y 3 to 40, z 10 to 40. What it left held 109 884.41, 0.06 too
            // little, ten million out and further.
            GeoSolid3 tool = Box(far, 3, 3, 10, 60, 40, 40);
            double shared = (57 * 17 + 17 * 20) * 30 - 8 * 19.9996 * 21.2 - 34.3996 * 9.1 * 12.3;

            Assert.True(LWithWallsOffItsOwn(far).TrySubtract(tool, out GeoSolid3 left, Thousandth, out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(HeldOffItsOwn - shared, left.GetVolume(Thousandth), Rounding(far, LWithWallsOffItsOwn(far), tool));
        }

        [Theory]
        [InlineData(1E8)]
        [InlineData(1E9)]
        public void TheCornersOfTheLLessABox_FarOut_AreTheCornersGiven(double far)
        {
            // Moved near the origin and back coordinate by coordinate, a corner of the L that the difference keeps comes back
            // as it was given, to the bit, and so does every corner of the box's that lies on the L's faces.
            GeoSolid3 l = Body(far);
            GeoSolid3 tool = Box(far, 30, 10, 5, 95, 30, 45);
            GeoPoint3[] given = l.Faces.Concat(tool.Faces).SelectMany(f => f.Boundary.Vertices).ToArray();

            Assert.True(l.TrySubtract(tool, out GeoSolid3 left, Thousandth, out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            GeoPoint3[] kept = left.Faces.SelectMany(f => f.Boundary.Vertices).ToArray();
            int near = 0;

            foreach (GeoPoint3 corner in kept)
            {
                GeoPoint3[] nearest = given.Where(g => g.DistanceTo(corner) < 1E-6).ToArray();

                if (nearest.Length > 0)
                {
                    near++;
                    Assert.Contains(nearest, g => g.Equals(corner));
                }
            }

            Assert.True(near >= 12);
            Assert.Equal(160_000 - 65 * 10 * 40, left.GetVolume(Thousandth), Rounding(far, l, tool));
        }
    }
}
