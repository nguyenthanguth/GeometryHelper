using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid open by a hair where the welding is easily misled: a closed feature beside the gap whose corners
    /// pair with no corner of the edges they lie on, edges of many sides running on nearly straight, a strip too wide,
    /// a sharp corner, a body far from the origin or turned out of every axis; and the same body closed twice; see
    /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    public class ClosingWeldEdgeTests
    {
        private const double Volume = ClosingTestBodies.Volume;

        private const double Area = ClosingTestBodies.Area;

        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        [Fact]
        public void ASlotOfSplitFacesBesideAGap_IsKept_AndTheGapWelded()
        {
            // A slot 0.0035 wide whose wall at x = 15.0035 is in two, its corners at y = 15 lying on the top's and bottom's
            // long edges: closed along them, though no corner of theirs pairs with those. The top's corner at the slot's
            // mouth is moved out 0.003, which closes within four thousandths, and the slot's corner across its mouth stands
            // within that of it. Only corners on edges left open move: the slot stays, and the gap is welded.
            GeoSolid3 slot = ClosingTestBodies.SlotOfSplitFacesWithItsMouthMoved(0.0035, 0.0);
            GeoSolid3 body = ClosingTestBodies.SlotOfSplitFacesWithItsMouthMoved(0.0035, 0.003);
            Assert.True(slot.Validate(Fine).IsValid);
            Assert.False(body.Validate(Fine).IsClosed);

            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            SolidRepair3 weld = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Weld, weld.Kind);
            Assert.InRange(weld.Size, 0.0029, 0.0031);

            foreach (double y in new[] { 15.0, 19.0, 19.99 })
            {
                Assert.Equal(PointLocation.OutSide, closed.Locate(new GeoPoint3(15.00175, y, 9.99), Fine));
            }

            double volume = slot.GetVolume(Fine);
            double area = slot.GetSurfaceArea(Fine);
            Assert.InRange(closed.GetVolume(Fine), volume - 0.003 * area, volume + 0.003 * area);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(1000)]
        public void AManySidedPrismOnCopiesOfItsCorners_IsWelded_NotRefusedForAFin(int sides)
        {
            // A prism of 500 or 1 000 sides on a circle of radius 10, every face on copies of its corners moved by up to
            // 0.002: the edges beside a corner run on so nearly straight that copies of three of them come within the
            // tolerance of one line for a thousandth or two by the corner, and read as a stretch three faces run. That is no
            // fin standing off the surface but part of a gap no wider than 0.004, and it is welded.
            GeoSolid3 whole = ClosingTestBodies.PrismOfMovedCopies(sides, 10, 100, 0.0, 3);
            GeoSolid3 body = ClosingTestBodies.PrismOfMovedCopies(sides, 10, 100, 0.002, 3);
            Assert.True(whole.Validate(Fine).IsValid);
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.OpenEdge && issue.Faces.Count == 3);

            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Size, 0.0, 0.004 + 1E-9));

            double volume = whole.GetVolume(Fine);
            double area = whole.GetSurfaceArea(Fine);
            Assert.InRange(closed.GetVolume(Fine), volume - 0.002 * area, volume + 0.002 * area);
        }

        [Fact]
        public void ANarrowStripOfTheTopMissing_IsAGapTooWide_WhereNoFillIsAllowed()
        {
            // A strip 0.03 wide missing along the top's middle: its long sides run back alongside each other within eight
            // times the gap allowed, a gap and no hole, six times too wide for five thousandths. The trouble is on the strip.
            GeoPoint3 at = AssertRefused(ClosingTestBodies.BoxWithAStripOfTheTopMissing(0.03), 0.005, ClosingFailure.GapTooWide);
            Assert.InRange(at.Z, 10.0 - 1E-6, 10.0 + 1E-6);
            Assert.InRange(at.X, 15.0 - 0.015 - 1E-6, 15.0 + 0.015 + 1E-6);
            Assert.InRange(at.Y, -1E-6, 20.0 + 1E-6);
        }

        [Fact]
        public void ANarrowStripOfTheTopMissing_IsWeldedShut_WithinAGapAsWide()
        {
            // Within three hundredths the corners across the strip at each end are made one, moved along the top: two welds,
            // and the box again.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.BoxWithAStripOfTheTopMissing(0.03), 0.03, out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Size, 0.0299, 0.0301));
            AssertVolume(Volume, closed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ACornerHalfADegreeSharp_IsWeldedBack(bool inward)
        {
            // A prism over a triangle whose corner at the origin is half a degree sharp, its sides there within a thousandth
            // of each other for 0.11; the top's copy of that corner moved 0.003 along the bisector, out of the triangle or
            // into it, is welded back, and nothing between the near sides is taken for the gap.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.SharpPrism(0.5, 0.003, inward), 0.005, out GeoSolid3 closed);
            SolidRepair3 weld = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Weld, weld.Kind);
            Assert.InRange(weld.Size, 0.0029, 0.0031);
            AssertVolume(0.5 * 30.0 * 30.0 * Math.Tan(0.5 * Math.PI / 180.0) * 10.0, closed);
        }

        [Fact]
        public void ACornerOfAHoleMovedIntoThePlate_IsWeldedBack()
        {
            // The top's copy of the hole's corner over (10, 10) moved 0.003 into the plate, the walls of the hole keeping it.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.PlateWithItsHoleCornerMoved(0.003), 0.005, out GeoSolid3 closed);
            SolidRepair3 weld = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Weld, weld.Kind);
            Assert.InRange(weld.Size, 0.0029, 0.0031);
            AssertVolume(8000.0, closed);
        }

        [Fact]
        public void ThreeCopiesOfOneCorner_AreWeldedIntoOne()
        {
            // The top, the right and the back each on a copy of their own of the corner over (30, 20), every two 0.0035 apart:
            // one group, welded to one of them.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.ThreeCopiesOfACorner(0.0035), 0.005, out GeoSolid3 closed);
            SolidRepair3 weld = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Weld, weld.Kind);
            Assert.InRange(weld.Size, 0.0034, 0.0036);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.0035 * Area, Volume + 0.0035 * Area);
        }

        [Fact]
        public void ABoxAMillionFromTheOrigin_IsWeldedAsNearIt()
        {
            // Every coordinate a million and more: a corner 0.003 out is welded back as at the origin; one 0.02 out is too
            // wide for five thousandths, found where it is, and welded within three hundredths.
            var far = new GeoVector3(1E6, 1E6, 1E6);
            SolidClosing3 report = AssertWelded(ClosingTestBodies.BoxWithTopCornersMovedOut((6, 0.003)).Translate(far), 0.005, out GeoSolid3 closed);
            Assert.InRange(Assert.Single(report.Repairs).Size, 0.0029, 0.0031);
            AssertVolume(Volume, closed);

            GeoSolid3 wide = ClosingTestBodies.BoxWithTopCornersMovedOut((6, 0.02)).Translate(far);
            GeoPoint3 moved = ClosingTestBodies.Corners()[6].Add(ClosingTestBodies.OutOfTheTop(6).Multiply(0.02)).Add(far);
            Assert.InRange(AssertRefused(wide, 0.005, ClosingFailure.GapTooWide).DistanceTo(moved), 0.0, 0.03);
            AssertWelded(wide, 0.03, out _);
        }

        [Fact]
        public void ABoxTurnedOutOfEveryAxis_IsWeldedBack()
        {
            // Turned 0.7 about (1, 1, 1) and moved far off, no face square to an axis: the top's corner moved 0.003 in the
            // top's plane is welded back.
            GeoPoint3[] corners = ClosingTestBodies.RotatedCorners(new GeoVector3(1, 1, 1), 0.7, new GeoVector3(123456.7, -98765.4, 4321.0));
            SolidClosing3 report = AssertWelded(ClosingTestBodies.BoxWithTopCornerMovedAway(corners, 0.003), 0.005, out GeoSolid3 closed);
            Assert.InRange(Assert.Single(report.Repairs).Size, 0.0029, 0.0031);
            AssertVolume(Volume, closed);
        }

        [Theory]
        [InlineData(11)]
        [InlineData(12)]
        [InlineData(13)]
        [InlineData(14)]
        [InlineData(15)]
        public void CopiesMovedByHalfTheGap_AreWelded(int seed)
        {
            // Each copy moved by up to 0.0025, so that two copies of a corner stand up to the whole gap, 0.005, apart.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.BoxOfMovedCopies(seed, 0.0025), 0.005, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Size, 0.0, 0.005 + 1E-9));
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.0025 * Area, Volume + 0.0025 * Area);
        }

        [Fact]
        public void TheSameBody_IsClosedTheSameWayTwice()
        {
            // Face for face and bit for bit: the welds, the corners put on edges, where they are and how large.
            var twisted = ClosingTestBodies.BoxCrackedAlongTheFront(
                ClosingTestBodies.Bowed(5, 0.002, new GeoVector3(0, -1, 0), false),
                ClosingTestBodies.Bowed(3, 0.002, new GeoVector3(0, 0, -1), true));

            foreach (GeoSolid3 body in new[] { ClosingTestBodies.BoxOfMovedCopies(7, 0.002), twisted })
            {
                Assert.True(body.TryClose(out GeoSolid3 one, new SolidClosingOptions(Fine, 0.005), out SolidClosing3 first));
                Assert.True(body.TryClose(out GeoSolid3 two, new SolidClosingOptions(Fine, 0.005), out SolidClosing3 second));
                Assert.Equal(one, two);
                Assert.Equal(first.Repairs.Select(r => (r.Kind, r.Size, r.Location)), second.Repairs.Select(r => (r.Kind, r.Size, r.Location)));
                Assert.Equal(first.VolumeChange, second.VolumeChange);
            }
        }

        // Welded closed within a gap, filling nothing: another body, valid, with no needle and no faces lying back to back,
        // each change a weld or a corner put on an edge.
        private static SolidClosing3 AssertWelded(GeoSolid3 body, double gap, out GeoSolid3 closed)
        {
            Assert.True(body.TryClose(out closed, new SolidClosingOptions(Fine, gap), out SolidClosing3 report), report.ToString());
            Assert.NotSame(body, closed);
            SolidValidation3 check = closed.Validate(Fine);
            Assert.True(check.IsValid, check.ToString());
            Assert.Equal(0, ClosingTestBodies.Needles(closed));
            Assert.Equal(0, ClosingTestBodies.BackToBack(closed));
            Assert.Equal(ClosingFailure.None, report.Failure);
            Assert.Equal(0.0, report.AddedArea);
            Assert.NotEmpty(report.Repairs);
            Assert.All(report.Repairs, repair => Assert.Contains(repair.Kind, new[] { SolidRepairKind.Weld, SolidRepairKind.SplitEdge }));
            return report;
        }

        // Not closed within a gap: nothing back and nothing done, with why and where.
        private static GeoPoint3 AssertRefused(GeoSolid3 body, double gap, ClosingFailure failure)
        {
            Assert.False(body.TryClose(out GeoSolid3 closed, new SolidClosingOptions(Fine, gap), out SolidClosing3 report));
            Assert.Equal(failure, report.Failure);
            Assert.Null(closed);
            Assert.Empty(report.Repairs);
            Assert.True(report.FailureLocation.HasValue);
            return report.FailureLocation.Value;
        }

        // Within a millionth of what it should hold.
        private static void AssertVolume(double expected, GeoSolid3 closed)
            => Assert.InRange(closed.GetVolume(Fine), expected * (1.0 - 1E-6), expected * (1.0 + 1E-6));
    }
}
