using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid open at the two ends of a hole through it whose walls are left out, where the walls, if they were
    /// there, would not run straight along the ends' normal: slanted, tapering, or with the rims listed from different
    /// corners; and loops that look like the ends of such a hole and are not: the walls would lie on faces the body has,
    /// cross it, or bridge a gap, or the rims turn at different numbers of corners; see
    /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plates are 30 by 30, the hole's rim in the top at z = the thickness and its rim in the bottom at z = 0. Two flat
    /// loops of one shell are the ends of a hole through it where, counted at the corners where each turns, they have as
    /// many corners; their caps face opposite ways and away from each other; a matching of corner c of the one with corner
    /// s - c of the other makes every wall, the quad from an edge of the one to the matched edge of the other, flat and
    /// convex; and the walls lie on no face of the body, cross none, and close it valid. Such a hole is closed by its caps
    /// or by its walls: a <see cref="ClosingFailure.HoleAmbiguous"/> where only an unambiguous fill is taken, and the less
    /// area of the two under <see cref="FillStrategy.MinArea"/>, a Fill for each cap or one for the walls.
    /// </para>
    /// <para>
    /// Otherwise each loop is a flat hole of its own and is capped, without ambiguity.
    /// </para>
    /// </remarks>
    public class ClosingThroughHoleTests
    {
        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        [Fact]
        public void ASlantedHoleMissingItsWalls_IsAHoleAmbiguous_WhereOnlyAnUnambiguousFillIsTaken()
        {
            // A plate 2 thick with a square hole 10 by 10 slanting through it, its bottom rim the top rim moved 2 down and 1
            // along x: the walls, parallelograms, close it round the hole, 1 600, and the caps close it over, 1 800. Neither
            // lies on a face, and either closes it valid.
            GeoPoint3[] square = ClosingTestBodies.Rectangle(10, 10, 20, 20);
            GeoSolid3 body = ClosingTestBodies.PlateWithAHoleWithoutItsWalls(2, square, Moved(square, 1, 0));
            Assert.Equal(8, body.Validate(Fine).Issues.Count(issue => issue.Kind == SolidIssueKind.OpenEdge));

            GeoPoint3 at = AssertRefused(body, Filling(1000.0), ClosingFailure.HoleAmbiguous);
            AssertOnARim(at, 2, 10, 21, 10, 20);
        }

        [Fact]
        public void ASlantedHoleMissingItsWalls_IsWalledRound_WhereTheLeastAreaIsTaken()
        {
            // The walls add 20 each along x and the square root of 500 each across, 84.72 in all, the caps 200: the least area
            // walls the hole round, one Fill, and the plate keeps its hole.
            GeoPoint3[] square = ClosingTestBodies.Rectangle(10, 10, 20, 20);
            GeoSolid3 body = ClosingTestBodies.PlateWithAHoleWithoutItsWalls(2, square, Moved(square, 1, 0));
            double walls = 40.0 + 2.0 * Math.Sqrt(500.0);

            SolidClosing3 report = AssertFilled(body, Filling(1000.0, FillStrategy.MinArea), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(walls, fill.Size, 9);
            Assert.Equal(walls, report.AddedArea, 9);
            Assert.Equal(10, closed.Faces.Count);
            AssertVolume(1600.0, closed);
        }

        [Fact]
        public void ATaperingHoleMissingItsWalls_IsAHoleAmbiguous_WhereOnlyAnUnambiguousFillIsTaken()
        {
            // A plate 10 thick with a square hole 10 by 10 at the top narrowing to 6 by 6 at the bottom: the walls,
            // trapezoids, close it round the hole, 8 346.67, and the caps over it, 9 000. Neither lies on a face, and either
            // is valid.
            GeoSolid3 body = ClosingTestBodies.PlateWithAHoleWithoutItsWalls(10, ClosingTestBodies.Rectangle(10, 10, 20, 20), ClosingTestBodies.Rectangle(12, 12, 18, 18));

            GeoPoint3 at = AssertRefused(body, Filling(1000.0), ClosingFailure.HoleAmbiguous);
            AssertOnARim(at, 10, 10, 20, 10, 20);
        }

        [Fact]
        public void ATaperingHoleMissingItsWalls_IsCapped_WhereTheLeastAreaIsTaken()
        {
            // The caps add 100 and 36, 136 in all, the four walls the square root of 104 times 8 each, 326.34: the least area
            // caps the plate over, two Fills, as though it had no hole.
            GeoSolid3 body = ClosingTestBodies.PlateWithAHoleWithoutItsWalls(10, ClosingTestBodies.Rectangle(10, 10, 20, 20), ClosingTestBodies.Rectangle(12, 12, 18, 18));

            SolidClosing3 report = AssertFilled(body, Filling(1000.0, FillStrategy.MinArea), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Fill, repair.Kind));
            Assert.Contains(report.Repairs, repair => Math.Abs(repair.Size - 100.0) <= 1E-9);
            Assert.Contains(report.Repairs, repair => Math.Abs(repair.Size - 36.0) <= 1E-9);
            Assert.Equal(136.0, report.AddedArea, 9);
            Assert.Equal(8, closed.Faces.Count);
            AssertVolume(9000.0, closed);
        }

        [Theory]
        [InlineData(FillStrategy.WhenUnambiguous)]
        [InlineData(FillStrategy.MinArea)]
        public void AHoleWhoseRimsAreListedFromDifferentCorners_IsPairedCornerForCorner(FillStrategy strategy)
        {
            // A pentagonal hole of 104 slanting through a plate 4 thick, its bottom rim the top one moved 4 down and (2, 1)
            // along, the bottom's ring of it listed from its third corner. Of the five ways to match the corners round, only
            // each corner of the top with the one moved from it makes the five walls flat: 167.74 in all, against 208 for the
            // caps, so the least area walls the hole round, 3 184 left. Where only an unambiguous fill is taken, the hole is
            // ambiguous.
            GeoPoint3[] pentagon = { new GeoPoint3(10, 10, 0), new GeoPoint3(20, 10, 0), new GeoPoint3(22, 16, 0), new GeoPoint3(15, 21, 0), new GeoPoint3(9, 17, 0) };
            GeoSolid3 body = ClosingTestBodies.PlateWithAHoleWithoutItsWalls(4, pentagon, Moved(pentagon, 2, 1), 2);

            if (strategy == FillStrategy.WhenUnambiguous)
            {
                AssertOnARim(AssertRefused(body, Filling(1000.0), ClosingFailure.HoleAmbiguous), 4, 9, 24, 10, 22);
                return;
            }

            // Each wall the parallelogram an edge of the rim sweeps along (2, 1, -4).
            double walls = Math.Sqrt(1700) + Math.Sqrt(740) + Math.Sqrt(1473) + Math.Sqrt(836) + Math.Sqrt(1025);
            SolidClosing3 report = AssertFilled(body, Filling(1000.0, strategy), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(walls, fill.Size, 9);
            Assert.Equal(11, closed.Faces.Count);
            AssertVolume(3184.0, closed);
        }

        [Fact]
        public void ASkewedBoxMissingItsTopAndBottom_IsCappedTwice_ItsSidesBeingTheWalls()
        {
            // The box sheared 5 along x over its height, its top and bottom left out: the rims are the one the other moved
            // along a slant, and the walls between them would lie back to back on the sides it has. The caps alone close it,
            // without ambiguity: the top and the bottom again, 600 each.
            GeoSolid3 body = ClosingTestBodies.ShearedBoxMissingItsTopAndBottom();

            SolidClosing3 report = AssertFilled(body, Filling(1000.0), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Fill, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.Equal(600.0, repair.Size, 9));
            Assert.Equal(6, closed.Faces.Count);
            AssertVolume(6000.0, closed);
        }

        [Fact]
        public void AHoleWhoseWallsWouldCrossTheBody_IsCapped_WithoutAmbiguity()
        {
            // A C of two plates 2 thick joined by a web, its top and its bottom pierced by square holes, one over the other:
            // walls from the one rim to the other would cross the top plate's underside and the bottom plate's top, through
            // the gap between them. The body they would close reads valid, its edges all paired, but they are no walls of a
            // hole: the caps alone close it, 100 each, without ambiguity.
            GeoSolid3 body = ClosingTestBodies.ChannelPiercedTopAndBottom();
            Assert.Equal(8, body.Validate(Fine).Issues.Count(issue => issue.Kind == SolidIssueKind.OpenEdge));

            SolidClosing3 report = AssertFilled(body, Filling(1000.0), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Fill, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.Equal(100.0, repair.Size, 9));
            Assert.Equal(12, closed.Faces.Count);
            AssertVolume(3960.0, closed);
        }

        [Theory]
        [InlineData(FillStrategy.WhenUnambiguous)]
        [InlineData(FillStrategy.MinArea)]
        public void TwoHolesFacingEachOtherAcrossAGap_AreCapped_NotBridged(FillStrategy strategy)
        {
            // A U whose arms each have a square hole in the face into the gap 2 wide between them, opposite each other: the
            // caps face each other, not away from each other, and walls between the rims would lay a bar across the gap, 80
            // of area against the caps' 200, not a hole through anything. The caps alone close it, under either strategy.
            GeoSolid3 body = ClosingTestBodies.ChannelPiercedFacingAcrossItsGap();

            SolidClosing3 report = AssertFilled(body, Filling(1000.0, strategy), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Fill, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.Equal(100.0, repair.Size, 9));
            Assert.Equal(12, closed.Faces.Count);
            AssertVolume(16920.0, closed);
        }

        [Fact]
        public void RimsTurningAtDifferentNumbersOfCorners_AreCapped_WithoutAmbiguity()
        {
            // The top rim a square of 4 corners, the bottom rim an octagon of 8, its corners cut 2 off the square: no corner
            // of the one goes with one of the other, so no wall runs from an edge of the one to an edge of the other, and a
            // tube between them could only be triangles laid one of many ways. Each rim is filled as the flat hole it is, 100
            // and 92, without ambiguity.
            GeoPoint3[] octagon =
            {
                new GeoPoint3(12, 10, 0), new GeoPoint3(18, 10, 0), new GeoPoint3(20, 12, 0), new GeoPoint3(20, 18, 0),
                new GeoPoint3(18, 20, 0), new GeoPoint3(12, 20, 0), new GeoPoint3(10, 18, 0), new GeoPoint3(10, 12, 0),
            };
            GeoSolid3 body = ClosingTestBodies.PlateWithAHoleWithoutItsWalls(10, ClosingTestBodies.Rectangle(10, 10, 20, 20), octagon);

            SolidClosing3 report = AssertFilled(body, Filling(1000.0), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.Contains(report.Repairs, repair => Math.Abs(repair.Size - 100.0) <= 1E-9);
            Assert.Contains(report.Repairs, repair => Math.Abs(repair.Size - 92.0) <= 1E-9);
            Assert.Equal(8, closed.Faces.Count);
            AssertVolume(9000.0, closed);
        }

        [Fact]
        public void ARimWithACornerItRunsStraightThrough_IsStillAnEndOfTheHole()
        {
            // The straight hole through a plate 10 thick, the bottom's ring of it with a corner more, half way along an edge,
            // where it runs straight on: counted at the corners where they turn, the rims have 4 each, and the hole is
            // ambiguous where only an unambiguous fill is taken, as without the corner.
            GeoPoint3[] withAStraightCorner = { new GeoPoint3(10, 10, 0), new GeoPoint3(15, 10, 0), new GeoPoint3(20, 10, 0), new GeoPoint3(20, 20, 0), new GeoPoint3(10, 20, 0) };
            GeoSolid3 body = ClosingTestBodies.PlateWithAHoleWithoutItsWalls(10, ClosingTestBodies.Rectangle(10, 10, 20, 20), withAStraightCorner, 1);

            AssertOnARim(AssertRefused(body, Filling(1000.0), ClosingFailure.HoleAmbiguous), 10, 10, 20, 10, 20);
        }

        // The corners of a plan moved along x and y.
        private static GeoPoint3[] Moved(GeoPoint3[] plan, double x, double y) => plan.Select(p => new GeoPoint3(p.X + x, p.Y + y, 0)).ToArray();

        // Welds within five thousandths, and fills a hole of no more than given, as the strategy says.
        private static SolidClosingOptions Filling(double maxHoleArea, FillStrategy fill = FillStrategy.WhenUnambiguous)
            => new SolidClosingOptions(Fine, 0.005, maxHoleArea, 0.0, fill);

        // Filled: another body, valid, with no needle and no skin of no thickness, nothing gone wrong.
        private static SolidClosing3 AssertFilled(GeoSolid3 body, SolidClosingOptions options, out GeoSolid3 closed)
        {
            Assert.False(body.Validate(Fine).IsClosed);
            Assert.True(body.TryClose(out closed, options, out SolidClosing3 report), report.ToString());
            Assert.NotSame(body, closed);
            SolidValidation3 check = closed.Validate(Fine);
            Assert.True(check.IsValid, check.ToString());
            Assert.Equal(ClosingFailure.None, report.Failure);
            Assert.Null(report.FailureLocation);
            Assert.Equal(0, ClosingTestBodies.Needles(closed));
            Assert.Equal(0, ClosingTestBodies.BackToBack(closed));
            return report;
        }

        // Not closed: nothing back and nothing done, with why and where.
        private static GeoPoint3 AssertRefused(GeoSolid3 body, SolidClosingOptions options, ClosingFailure failure)
        {
            Assert.False(body.TryClose(out GeoSolid3 closed, options, out SolidClosing3 report));
            Assert.Equal(failure, report.Failure);
            Assert.Null(closed);
            Assert.Empty(report.Repairs);
            Assert.Equal(0.0, report.AddedArea);
            Assert.Equal(0.0, report.VolumeChange);
            Assert.True(report.FailureLocation.HasValue);
            return report.FailureLocation.Value;
        }

        // A point on the plane of the plate's top or bottom, within the box of the hole's rims seen from above.
        private static void AssertOnARim(GeoPoint3 at, double thickness, double x0, double x1, double y0, double y1)
        {
            Assert.InRange(Math.Min(Math.Abs(at.Z), Math.Abs(at.Z - thickness)), 0.0, 1E-6);
            Assert.InRange(at.X, x0 - 1E-6, x1 + 1E-6);
            Assert.InRange(at.Y, y0 - 1E-6, y1 + 1E-6);
        }

        // Within a millionth of what it should hold.
        private static void AssertVolume(double expected, GeoSolid3 closed)
            => Assert.InRange(closed.GetVolume(Fine), expected * (1.0 - 1E-6), expected * (1.0 + 1E-6));
    }
}
