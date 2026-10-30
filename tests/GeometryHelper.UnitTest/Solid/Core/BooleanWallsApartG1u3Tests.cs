using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A union, a common part and a difference of a body whose openings' walls stand apart within the tolerance of one plane
    /// keep each wall in its own plane, as the cut of the openings keeps it. A boolean cut the openings of its bodies in at once,
    /// and the plane of the first opening cut the whole of what it reached: the second opening's wall was cut in the first
    /// one's plane, 0.0009 off its own, however far away.
    /// </summary>
    public class BooleanWallsApartG1u3Tests
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

            foreach (double far in new[] { 0.0, 1E6 })
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

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheLLessABoxAcrossBothOpenings_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // Within a thousandth or a hundredth the L less the box held 130 189.39 one way round and 130 189.26 the other.
            Assert.True(L(far, turnedRound).TrySubtract(Tool(far), out GeoSolid3 left, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(Held - Shared, left.GetVolume(Within(tolerance)), 1E-5);
            Assert.True(left.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheBoxLessTheL_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // The box less the L held 28 457.23 one way round and 28 457.88 the other.
            Assert.True(Tool(far).TrySubtract(L(far, turnedRound), out GeoSolid3 left, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(ToolVolume - Shared, left.GetVolume(Within(tolerance)), 1E-5);
            Assert.True(left.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheLJoinedToTheBox_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // The two joined held 182 189.39 one way round and 182 189.26 the other.
            Assert.True(L(far, turnedRound).TryUnion(Tool(far), out GeoSolid3 union, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(Held + ToolVolume - Shared, union.GetVolume(Within(tolerance)), 1E-5);
            Assert.True(union.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void WhatTheLAndTheBoxShare_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // The two shared 23 542.77 one way round and 23 542.12 the other.
            Assert.True(L(far, turnedRound).TryIntersect(Tool(far), out GeoSolid3 shared, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(Shared, shared.GetVolume(Within(tolerance)), 1E-5);
            Assert.True(shared.Validate(Within(tolerance)).IsValid);
        }

        /// <summary>
        /// The L with an opening 8 by 44.4996 down from its top 31.2 deep in its upright, its near wall 0.0004 off the plane of
        /// the L's inner wall along the foot, 5 from it, and one 34.3996 by 9.1 up from its floor 22.3 deep in its foot, its
        /// near wall 0.0004 off the plane of the L's inner wall along the upright, 6.2 from it. Each opening's wall is turned
        /// the other way from the L's wall it stands off.
        /// </summary>
        private static GeoSolid3 LWithWallsOffItsOwn(double far)
            => Body(far).WithOpenings(new[] { Box(far, 7, 20.0004, 18.8, 15, 64.5, 57), Box(far, 20.0004, 4.7, -7, 54.4, 13.8, 22.3) });

        /// <summary>That L less what the openings take of it.</summary>
        private const double HeldOffItsOwn = 160_000 - 8 * 44.4996 * 31.2 - 34.3996 * 9.1 * 22.3;

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E6)]
        public void OpeningsWithWallsTurnedFromTheLsOwnAHairOffTheirPlanes_EachKeepItsOwnWall(double far)
        {
            // Within a thousandth the walls, turned opposite ways, were not found standing apart, and the L
            // held 0.09 too little.
            GeoSolid3 solid = LWithWallsOffItsOwn(far);

            Assert.Equal(HeldOffItsOwn, solid.GetVolume(Thousandth), 1E-5);
            Assert.True(solid.TryCutOpenings(out GeoSolid3 material, Thousandth));
            Assert.Equal(HeldOffItsOwn, material.GrossVolume, 1E-5);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E6)]
        public void ThatLLessABox_KeepsEachWallInItsOwnPlane(double far)
        {
            // The box over the corner of the L: x 3 to 60, y 3 to 40, z 10 to 40. What it left held 109 884.41, 0.06 too little.
            GeoSolid3 tool = Box(far, 3, 3, 10, 60, 40, 40);
            double shared = (57 * 17 + 17 * 20) * 30 - 8 * 19.9996 * 21.2 - 34.3996 * 9.1 * 12.3;

            Assert.True(LWithWallsOffItsOwn(far).TrySubtract(tool, out GeoSolid3 left, Thousandth, out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(HeldOffItsOwn - shared, left.GetVolume(Thousandth), 1E-5);
        }
    }
}
