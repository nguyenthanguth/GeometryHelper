using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A difference, a common part and a union of two plain bodies asked below a few roundings of their coordinates, a
    /// quadrillionth of the largest of them, nought among it, are worked out within that rounding and welded to close within
    /// the tolerance asked. Within the tolerance alone, rounding put the corners of a face lying in a plane of the other body
    /// on either side of it: a plate at the origin less a hole through it came back whole, a plate less a slot across it was
    /// said to leave nothing, the common part of a plate and a slot was said to be nothing, and unions held the wrong volume.
    /// </summary>
    public class RoundingLevelG1yTests
    {
        private static readonly double[] Plate = { 0, 0, 200, 0, 200, 100, 0, 100 };

        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        /// <summary>
        /// A plate 200 by 100 by 3 standing in a frame turned about in space: its up, a direction its length is taken square to
        /// the up from, and the corner it starts at.
        /// </summary>
        private sealed class Frame
        {
            private readonly GeoVector3 _up;
            private readonly GeoVector3 _along;
            private readonly GeoVector3 _across;
            private readonly GeoPoint3 _origin;

            public Frame(GeoVector3 up, GeoVector3 along, GeoPoint3 origin)
            {
                _up = up;
                GeoVector3 square = along.Subtract(up.Multiply(along.DotProduct(up)));
                _along = square.Multiply(1 / square.Length);
                _across = up.CrossProduct(_along);
                _origin = origin;
            }

            private GeoPoint3 At(double x, double y, double z) => _origin.Add(_along.Multiply(x)).Add(_across.Multiply(y)).Add(_up.Multiply(z));

            /// <summary>The plate.</summary>
            public GeoSolid3 Plate()
                => GeoSolid3.Extrude(
                    new GeoPolygon3(Enumerable.Range(0, RoundingLevelG1yTests.Plate.Length / 2).Select(i => At(RoundingLevelG1yTests.Plate[2 * i], RoundingLevelG1yTests.Plate[2 * i + 1], 0)).ToArray()),
                    _up.Multiply(3),
                    Tolerance.Default);

            /// <summary>A box from and to a length, a width and a height in the frame.</summary>
            public GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
                => GeoSolid3.Extrude(new GeoPolygon3(At(x0, y0, z0), At(x1, y0, z0), At(x1, y1, z0), At(x0, y1, z0)), _up.Multiply(z1 - z0), Tolerance.Default);
        }

        /// <summary>A tolerance of a number of roundings of the coordinates of a body, a quadrillionth of the largest of them.</summary>
        private static Tolerance Roundings(GeoSolid3 body, double count)
        {
            GeoAabb3 box = body.GetAabb();
            double rounding = 1E-15 * new[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z }.Max(Math.Abs);

            return new Tolerance(count * rounding, count * rounding, count * rounding, count * rounding);
        }

        /// <summary>At the origin, turned.</summary>
        private static readonly Frame AtTheOrigin = new Frame(
            new GeoVector3(0.0013332455674188487, 0.798042809225385, -0.6025992840187522),
            new GeoVector3(-0.43360272631941693, 0.21250335107216525, -0.8756888725520434),
            new GeoPoint3(0, 0, 0));

        /// <summary>A thousand out.</summary>
        private static readonly Frame ThousandOut = new Frame(
            new GeoVector3(0.0013332455674188487, 0.798042809225385, -0.6025992840187522),
            new GeoVector3(-0.43360272631941693, 0.21250335107216525, -0.8756888725520434),
            new GeoPoint3(554.2731227140283, -899.0416647396245, -775.2947992530161));

        /// <summary>A hundred thousand out.</summary>
        private static readonly Frame HundredThousandOut = new Frame(
            new GeoVector3(0.5092589940624794, -0.042439272485513636, 0.8595662773267528),
            new GeoVector3(0.9388021143056186, 0.34429947634992636, 0.010414449599808872),
            new GeoPoint3(35357.806149571115, -59718.80720915217, -94690.75156128535));

        /// <summary>A million out.</summary>
        private static readonly Frame MillionOut = new Frame(
            new GeoVector3(-0.1243591260351397, -0.613569631445631, -0.7797865830722204),
            new GeoVector3(-0.3166215227442763, 0.11928498764860565, -0.9410217335730178),
            new GeoPoint3(-301412.54947586573, -683055.8943017647, -614917.595691475));

        /// <summary>A hundred million out.</summary>
        private static readonly Frame HundredMillionOut = new Frame(
            new GeoVector3(-0.10835418780431474, -0.6823614471167708, 0.7229399874636714),
            new GeoVector3(0.7216048511268258, -0.6888882078267337, -0.06869844210389839),
            new GeoPoint3(-33434566.731301397, -10632728.18486799, 48622304.92225956));

        [Fact]
        public void APlateAtTheOriginLessAHoleThroughIt_AtNought_LeavesThePlateLessTheHole()
        {
            // The hole 20 by 20 in the middle, 7 past both faces: at nought the difference came back as the whole plate, 60 000,
            // and was trusted.
            GeoSolid3 plate = AtTheOrigin.Plate();
            GeoSolid3 hole = AtTheOrigin.Box(90, 40, -7, 110, 60, 10);

            Assert.True(Boolean3.TrySubtract(plate, hole, out GeoSolid3 left, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(58_800, left.GrossVolume, 1E-6 * 58_800);
            Assert.True(left.Validate(Nought).IsValid);
        }

        [Fact]
        public void APlateAHundredThousandOutLessASlotAcrossIt_AtNought_LeavesTheTwoPieces()
        {
            // The slot 20 wide reaches 10 past both sides and 7 past both faces: at nought the difference came back as the whole
            // plate, 60 000, where 54 000 is left.
            GeoSolid3 plate = HundredThousandOut.Plate();
            GeoSolid3 slot = HundredThousandOut.Box(90, -10, -7, 110, 110, 10);

            Assert.True(Boolean3.TrySubtract(plate, slot, out GeoSolid3 left, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(54_000, left.GrossVolume, 1E-6 * 54_000);
            Assert.True(left.Validate(Nought).IsValid);
        }

        [Fact]
        public void APlateAMillionOutLessASlotAcrossIt_AtThreeTenthsOfTheRounding_LeavesTheTwoPieces()
        {
            // Within 2.0E-10 the difference was said to leave nothing.
            GeoSolid3 plate = MillionOut.Plate();
            GeoSolid3 slot = MillionOut.Box(90, -10, -7, 110, 110, 10);
            Tolerance tolerance = Roundings(plate, 0.3);

            Assert.True(Boolean3.TrySubtract(plate, slot, out GeoSolid3 left, tolerance, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(54_000, left.GrossVolume, 1E-6 * 54_000);
            Assert.True(left.Validate(tolerance).IsValid);
        }

        [Fact]
        public void APlateAMillionOutAndASlotAlongIt_AtNought_ShareWhatTheSlotCrosses()
        {
            // The slot 100 by 10 through the middle: at nought the common part was said to be nothing.
            GeoSolid3 plate = MillionOut.Plate();
            GeoSolid3 slot = MillionOut.Box(50, 45, -7, 150, 55, 10);

            Assert.True(Boolean3.TryIntersect(plate, slot, out GeoSolid3 common, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(3_000, common.GrossVolume, 1E-6 * 60_000);
            Assert.True(common.Validate(Nought).IsValid);
        }

        [Fact]
        public void APlateAThousandOutJoinedToABoxThroughIt_AtNought_HoldsTheTwoLessTheirOverlap()
        {
            // The box 50 by 20 standing 7 past both faces: at nought the union held 68 588.24, where the two hold 74 000, and was
            // trusted.
            GeoSolid3 plate = ThousandOut.Plate();
            GeoSolid3 box = ThousandOut.Box(50, 40, -7, 100, 60, 10);

            Assert.True(Boolean3.TryUnion(plate, box, out GeoSolid3 union, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(74_000, union.GrossVolume, 1E-6 * 74_000);
            Assert.True(union.Validate(Nought).IsValid);
        }

        [Fact]
        public void APlateAHundredMillionOutJoinedToABoxThroughIt_AtThreeTenthsOfTheRounding_HoldsTheTwoLessTheirOverlap()
        {
            // Within 1.5E-8 the union held the two whole, 77 000, where they hold 74 000, and was trusted.
            GeoSolid3 plate = HundredMillionOut.Plate();
            GeoSolid3 box = HundredMillionOut.Box(50, 40, -7, 100, 60, 10);
            Tolerance tolerance = Roundings(plate, 0.3);

            Assert.True(Boolean3.TryUnion(plate, box, out GeoSolid3 union, tolerance, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(74_000, union.GrossVolume, 1E-6 * 74_000);
        }
    }
}
