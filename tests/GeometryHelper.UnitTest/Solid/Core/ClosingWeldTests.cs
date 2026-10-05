using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid open by a hair: corners standing apart across a gap made one, within the tolerance and then twice it,
    /// four times and so on up to the gap allowed, the first reach that closes the body taken; and a corner standing off the
    /// edge of a face beside it put on that edge; see
    /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// The bodies are the box of <see cref="ClosingTestBodies"/>, 30 by 20 by 10, 6 000 in all and 2 200 in area, opened by
    /// moving corners of a face in its own plane, so that every face stays flat and only the edges at the corners moved are
    /// open. No hole may be filled: what closes is welded.
    /// </remarks>
    public class ClosingWeldTests
    {
        private const double Volume = ClosingTestBodies.Volume;

        private const double Area = ClosingTestBodies.Area;

        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        [Fact]
        public void ACornerOfTheTopMovedThreeThousandths_IsWeldedBack()
        {
            // The top's corner over (30, 20) moved 0.003 out along the diagonal, the right and the back keeping it: open along
            // the three edges there. Moved back along the top it moves no volume, where the others moved to it would tilt.
            GeoSolid3 body = ClosingTestBodies.BoxWithTopCornersMovedOut((6, 0.003));
            Assert.False(body.Validate(Fine).IsClosed);

            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.InRange(LargestWeld(report), 0.0029, 0.0031);
            Assert.Equal(6, closed.Faces.Count);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.003 * Area, Volume + 0.003 * Area);
            Assert.InRange(report.VolumeChange, -0.003 * Area, 0.003 * Area);
        }

        [Fact]
        public void ACornerMovedTwoHundredths_IsAGapTooWideForFiveThousandths_AndWeldedWithinThreeHundredths()
        {
            // Within five thousandths no reach makes the two copies of the corner one, and no hole may be filled: the gap is
            // too wide, and the trouble is at the corner. Within three hundredths it closes.
            GeoSolid3 body = ClosingTestBodies.BoxWithTopCornersMovedOut((6, 0.02));
            GeoPoint3 moved = ClosingTestBodies.Corners()[6].Add(ClosingTestBodies.OutOfTheTop(6).Multiply(0.02));

            Assert.False(body.TryClose(out GeoSolid3 notClosed, new SolidClosingOptions(Fine, 0.005), out SolidClosing3 refused));
            Assert.Equal(ClosingFailure.GapTooWide, refused.Failure);
            Assert.Null(notClosed);
            Assert.Empty(refused.Repairs);
            Assert.Equal(0.0, refused.AddedArea);
            Assert.Equal(0.0, refused.VolumeChange);
            Assert.True(refused.FailureLocation.HasValue);
            Assert.InRange(refused.FailureLocation.Value.DistanceTo(moved), 0.0, 0.03);

            AssertWelded(body, 0.03, out GeoSolid3 closed);
            Assert.Equal(6, closed.Faces.Count);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.02 * Area, Volume + 0.02 * Area);
        }

        [Fact]
        public void ACornerTwoHundredthsOut_IsWeldedWithinThreeHundredths_NotLeftOnANeedle()
        {
            // The box's corner over (30, 20) stands 0.014 off the two edges of the top beside the corner moved, nearer than
            // the 0.02 it stands off that corner. Put on both, as it can be within 0.016, it would close the top by a needle
            // out to the moved corner and back: that reads valid, and is no face of a body. The copies are welded instead.
            GeoSolid3 body = ClosingTestBodies.BoxWithTopCornersMovedOut((6, 0.02));

            SolidClosing3 report = AssertWelded(body, 0.03, out GeoSolid3 closed);
            Assert.InRange(LargestWeld(report), 0.0199, 0.0201);

            GeoPoint3[] box = ClosingTestBodies.Corners();

            foreach (GeoPoint3 corner in closed.Faces.SelectMany(face => face.Boundary.Vertices))
            {
                Assert.InRange(box.Min(c => c.DistanceTo(corner)), 0.0, 1E-9);
            }
        }

        [Fact]
        public void ACornerMovedFifteenTenThousandths_IsWeldedAtTheLeastReachThatCloses_NotAtTheGap()
        {
            // A chamfer 0.05 along each side cuts off the box's corner at (30, 20), and the top's corner at its start is moved
            // 0.0015 along x: within two thousandths the copies are one. Within a tenth, the gap allowed, the chamfer's ends
            // at the top, 0.07 apart and corners of edges open as well, would be made one with them.
            GeoPoint3[] plan = ClosingTestBodies.ChamferedPlan();
            GeoSolid3 chamfered = ClosingTestBodies.Prism(plan, 0, 10);
            GeoSolid3 body = ClosingTestBodies.PrismWithTopCornerMoved(plan, 0, 10, 2, new GeoVector3(0.0015, 0, 0));
            Assert.True(chamfered.Validate(Fine).IsValid);
            Assert.False(body.Validate(Fine).IsClosed);

            SolidClosing3 report = AssertWelded(body, 0.1, out GeoSolid3 closed);
            Assert.InRange(LargestWeld(report), 0.0014, 0.0016);
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Size, 0.0, 0.0016));
            Assert.Equal(7, closed.Faces.Count);

            double volume = chamfered.GetVolume(Fine);
            double area = chamfered.GetSurfaceArea(Fine);
            Assert.InRange(closed.GetVolume(Fine), volume - 0.0015 * area, volume + 0.0015 * area);
        }

        [Fact]
        public void TwoCornersOpenByDifferentGaps_AreBothWelded()
        {
            // The top's corners over (30, 20) and over the origin moved out by 0.0015 and 0.0035: the first closes within two
            // thousandths and the second only within four, the two then welded together. A gap of 0.004 would lie on the
            // reach itself, and rounding would decide whether it is within it.
            GeoSolid3 body = ClosingTestBodies.BoxWithTopCornersMovedOut((6, 0.0015), (4, 0.0035));
            Assert.False(body.Validate(Fine).IsClosed);

            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.Contains(report.Repairs, repair => repair.Kind == SolidRepairKind.Weld && repair.Size >= 0.0014 && repair.Size <= 0.0016);
            Assert.Contains(report.Repairs, repair => repair.Kind == SolidRepairKind.Weld && repair.Size >= 0.0034 && repair.Size <= 0.0036);
            Assert.Equal(6, closed.Faces.Count);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.0035 * Area, Volume + 0.0035 * Area);
        }

        [Fact]
        public void ACornerStandingOffTheEdgeOfTheFaceBeside_IsPutOnThatEdge()
        {
            // The top carries a corner of its own halfway along its front edge, 0.003 out from the front's straight top edge:
            // no corner of the front is near it. The crack lies in the top's plane, and the corner is moved onto the front's
            // edge within it, the edge split there; threaded into the front instead, it would fold the front under the top.
            GeoSolid3 body = ClosingTestBodies.BoxWithTheTopsFrontBentOut(0.003);
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.OpenEdge && issue.Faces.Count == 1 && issue.Size > 29.0);

            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.Contains(report.Repairs, repair => repair.Size >= 0.0029 && repair.Size <= 0.0031);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.003 * Area, Volume + 0.003 * Area);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void EveryFaceOnCopiesOfItsCorners_EachMovedInItsPlane_IsWeldedClosed(int seed)
        {
            // Each copy moved by up to 0.002, so that no two copies of a corner are more than 0.004 apart, within the gap:
            // every edge is open. Welded, every corner is one of the copies of a corner of the box, nothing further from it.
            GeoSolid3 body = ClosingTestBodies.BoxOfMovedCopies(seed, 0.002);
            Assert.False(body.Validate(Fine).IsClosed);

            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Size, 0.0, 0.004 + 1E-9));
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.002 * Area, Volume + 0.002 * Area);

            GeoPoint3[] box = ClosingTestBodies.Corners();

            foreach (GeoPoint3 corner in closed.Faces.SelectMany(face => face.Boundary.Vertices))
            {
                Assert.InRange(box.Min(c => c.DistanceTo(corner)), 0.0, 0.002 + 1E-9);
            }
        }

        [Fact]
        public void ASlotThinnerThanTheGap_IsKept_WhereACornerElsewhereIsWelded()
        {
            // A slot 0.0035 wide cut 10 into the back of the box is closed, and stays: only the corners of edges open are
            // welded. The top's corner over (30, 0), moved out by 0.003, closes only within four thousandths, wider than the
            // slot, and a weld of every corner that near would close the slot up.
            const double Width = 0.0035;
            GeoPoint3[] plan = ClosingTestBodies.SlottedPlan(Width);
            GeoSolid3 slotted = ClosingTestBodies.Prism(plan, 0, 10);
            var inTheSlot = new GeoPoint3(15 + Width / 2, 15, 5);
            var besideIt = new GeoPoint3(14.99, 15, 5);
            Assert.True(slotted.Validate(Fine).IsValid);
            Assert.Equal(PointLocation.OutSide, slotted.Locate(inTheSlot, Fine));

            GeoSolid3 body = ClosingTestBodies.PrismWithTopCornerMoved(plan, 0, 10, 1, new GeoVector3(1, -1, 0).Multiply(0.003 / Math.Sqrt(2.0)));
            Assert.False(body.Validate(Fine).IsClosed);

            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.InRange(LargestWeld(report), 0.0029, 0.0031);
            Assert.Equal(PointLocation.OutSide, closed.Locate(inTheSlot, Fine));
            Assert.Equal(PointLocation.Inside, closed.Locate(besideIt, Fine));
            Assert.Equal(slotted.Faces.Count, closed.Faces.Count);

            double volume = slotted.GetVolume(Fine);
            double area = slotted.GetSurfaceArea(Fine);
            Assert.InRange(closed.GetVolume(Fine), volume - 0.003 * area, volume + 0.003 * area);
        }

        [Fact]
        public void TheBodyGiven_IsLeftAsItWas()
        {
            // Closing makes another body: the one given keeps its faces and their corners, open as it was.
            GeoSolid3 body = ClosingTestBodies.BoxWithTopCornersMovedOut((6, 0.003));
            GeoSolid3 before = body.Clone();

            Assert.True(body.TryClose(out GeoSolid3 closed, new SolidClosingOptions(Fine, 0.005), out _));
            Assert.NotSame(body, closed);
            Assert.Equal(before, body);
            Assert.False(body.Validate(Fine).IsClosed);
        }

        // Welded closed within a gap, filling nothing: another body, valid, with no ring of it doubling back and no faces
        // lying back to back, each change a weld or a corner put on an edge.
        private static SolidClosing3 AssertWelded(GeoSolid3 body, double gap, out GeoSolid3 closed)
        {
            Assert.True(body.TryClose(out closed, new SolidClosingOptions(Fine, gap), out SolidClosing3 report), report.ToString());
            Assert.NotSame(body, closed);
            SolidValidation3 check = closed.Validate(Fine);
            Assert.True(check.IsValid, check.ToString());
            Assert.Equal(0, ClosingTestBodies.Needles(closed));
            Assert.Equal(0, ClosingTestBodies.BackToBack(closed));
            Assert.Equal(ClosingFailure.None, report.Failure);
            Assert.Null(report.FailureLocation);
            Assert.Equal(0.0, report.AddedArea);
            Assert.NotEmpty(report.Repairs);
            Assert.All(report.Repairs, repair => Assert.Contains(repair.Kind, new[] { SolidRepairKind.Weld, SolidRepairKind.SplitEdge }));
            return report;
        }

        // The furthest a corner of any group welded moved; there is at least one weld.
        private static double LargestWeld(SolidClosing3 report)
        {
            Assert.Contains(report.Repairs, repair => repair.Kind == SolidRepairKind.Weld);
            return report.Repairs.Where(repair => repair.Kind == SolidRepairKind.Weld).Max(repair => repair.Size);
        }
    }
}
