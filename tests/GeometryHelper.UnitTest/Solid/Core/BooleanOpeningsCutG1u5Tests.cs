using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The openings of a body cut in at once, as a boolean cuts them, come out closed where the cells an opening's planes
    /// reached and those beside them kept faces that did not match: such a cut is cut again with every plane carried through
    /// every cell it crosses. A plate with two blind openings whose ceilings stood 0.00095 apart came out of the cut open
    /// within a ten-thousandth to a thousand-millionth, and the plate less a box across both openings was 1.05 short and not
    /// valid within a ten-thousandth and a hundred-thousandth, where the boolean is not worked out again on the material,
    /// and the two joined were not worked out within a hundred-thousandth.
    /// </summary>
    public class BooleanOpeningsCutG1u5Tests
    {
        private static GeoPoint3 At(double far, double x, double y, double z) => new GeoPoint3(far + x, far + y, far + z);

        private static GeoSolid3 Box(double far, double x0, double y0, double z0, double x1, double y1, double z1)
            => GeoSolid3.Extrude(
                new GeoPolygon3(At(far, x0, y0, z0), At(far, x1, y0, z0), At(far, x1, y1, z0), At(far, x0, y1, z0)), new GeoVector3(0, 0, z1 - z0), Tolerance.Default);

        /// <summary>
        /// A plate 200 by 100 by 3 with a blind opening 38.6 by 60.7 up from its floor to 1.772, and one 38.9 by 60 to
        /// 1.77105: their ceilings stand 0.00095 apart, the openings 31.5 apart along them.
        /// </summary>
        private static GeoSolid3 Plate(double far)
            => Box(far, 0, 0, 0, 200, 100, 3).WithOpenings(new[] { Box(far, 13.5, 22.5, -7, 52.1, 83.2, 1.772), Box(far, 83.6, 24.9, -7, 122.5, 84.9, 1.77105) });

        /// <summary>The plate less what the openings take of it.</summary>
        private const double Held = 60_000 - 38.6 * 60.7 * 1.772 - 38.9 * 60 * 1.77105;

        /// <summary>A box 195.8 by 70.8 by 3.3 over both openings, standing out of the plate's top, end and side.</summary>
        private static GeoSolid3 Tool(double far) => Box(far, 8.2, 32.7, 0.3, 204, 103.5, 3.6);

        private const double ToolVolume = 195.8 * 70.8 * 3.3;

        /// <summary>What the box and the plate share: the box within the plate less what the openings take of it.</summary>
        private const double Shared = 191.8 * 67.3 * 2.7 - 38.6 * 50.5 * 1.472 - 38.9 * 52.2 * 1.47105;

        private static Tolerance Within(double tolerance) => new Tolerance(tolerance, 1E-5, Math.PI / 180, tolerance);

        /// <summary>The plate at the origin and a hundred thousand out, within a ten-thousandth and a hundred-thousandth.</summary>
        public static TheoryData<double, double> Coarser()
        {
            var cases = new TheoryData<double, double>();

            foreach (double far in new[] { 0.0, 1E5 })
            {
                foreach (double tolerance in new[] { 1E-4, 1E-5 })
                {
                    cases.Add(far, tolerance);
                }
            }

            return cases;
        }

        /// <summary>The plate at the origin and a hundred thousand out, within a ten-thousandth to a thousand-millionth.</summary>
        public static TheoryData<double, double> Finer()
        {
            var cases = new TheoryData<double, double>();

            foreach (double far in new[] { 0.0, 1E5 })
            {
                foreach (double tolerance in new[] { 1E-4, 1E-5, 1E-6, 1E-9 })
                {
                    cases.Add(far, tolerance);
                }
            }

            return cases;
        }

        [Theory]
        [MemberData(nameof(Finer))]
        public void ThePlatesOpeningsCutInAtOnce_CloseHoldingWhatIsLeft(double far, double tolerance)
        {
            // Cut in at once, the openings came out open, 1.05 short.
            Assert.True(Boolean3.TryCutOpeningsAtOnce(Plate(far), out GeoSolid3 material, Within(tolerance)));

            Assert.True(material.Validate(Within(tolerance)).IsValid);
            Assert.Equal(Held, material.GetVolume(Within(tolerance)), 1E-5);
        }

        [Theory]
        [MemberData(nameof(Coarser))]
        public void ThePlateLessABoxAcrossBothOpenings_HoldsWhatIsLeft(double far, double tolerance)
        {
            // The plate less the box held 22 717.96, 1.05 short, and was not valid.
            Assert.True(Plate(far).TrySubtract(Tool(far), out GeoSolid3 left, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(Held - Shared, left.GetVolume(Within(tolerance)), 1E-5);
            Assert.True(left.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [MemberData(nameof(Coarser))]
        public void ThePlateJoinedToTheBox_IsWorkedOut(double far, double tolerance)
        {
            // Within a hundred-thousandth the two joined were not worked out, and within a ten-thousandth held 1.05 too
            // little and were not valid.
            Assert.True(Plate(far).TryUnion(Tool(far), out GeoSolid3 union, Within(tolerance), out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(Held + ToolVolume - Shared, union.GetVolume(Within(tolerance)), 1E-5);
            Assert.True(union.Validate(Within(tolerance)).IsValid);
        }
    }
}
