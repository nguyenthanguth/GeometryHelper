using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid with shells inside shells: each closed shell keeps the way it is wound against the shell it lies in,
    /// the same way a block within it and the other way a cavity, and only an outermost shell wound inwards is turned, with
    /// every shell inside it; a shell that may lie inside a shell still open waits for the closing before its sign is read;
    /// see <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The check reads only the sign of the whole body's volume: a box with a block inside, both wound outwards, 6 600, and
    /// the box with the block wound inwards, a cavity, 5 400, both read valid, so which of the two a body is is told by how
    /// its shells are wound, and the least change turns neither. A part of a real model came with three boxes inside its
    /// main shell, all wound outwards, and with one face of the main shell turned over, the boxes were turned into cavities.
    /// </para>
    /// <para>
    /// The box is that of <see cref="ClosingTestBodies"/>, 30 by 20 by 10, 6 000, its faces 2 200; the block of
    /// <see cref="ClosingTestBodies.BlockFaces"/> 10 by 10 by 6, 600, its faces 440, and its top 100; the block inside that,
    /// of <see cref="ClosingTestBodies.InnerBlockFaces"/>, 4 by 4 by 4, 64, its faces 96.
    /// </para>
    /// </remarks>
    public class ClosingNestingTests
    {
        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        // Welds no further than five thousandths, and fills nothing.
        private static readonly SolidClosingOptions NoFill = new SolidClosingOptions(Fine, 0.005);

        // Welds no further than five thousandths, and fills a flat hole of up to 1 000 where that is the one way.
        private static readonly SolidClosingOptions Filling = new SolidClosingOptions(Fine, 0.005, 1000.0, 0.0, FillStrategy.WhenUnambiguous);

        [Fact]
        public void ABoxWithABlockInside_BothWoundOutwards_ReadsValid_AndIsTakenAsItIs()
        {
            // A solid inside a solid, as the real part came, each closed and wound outwards: 6 600 together.
            var body = new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners()).Concat(ClosingTestBodies.BlockFaces()));
            Assert.Equal(6600.0, body.GetVolume(Fine), 6);

            AssertTakenAsItIs(body);
        }

        [Fact]
        public void ABoxWithABlockInside_AndTheBoxsTopTurnedOver_HasTheTopAloneTurnedBack()
        {
            // The block is wound the same way as the box round it, a block within it, and stays so. Turned with the top, it
            // would be a cavity, seven faces turned where one is wrong, and the body would read valid holding 5 400.
            List<GeoFace3> block = ClosingTestBodies.BlockFaces();
            var body = new GeoSolid3(ClosingTestBodies.BoxWithFaceFlipped(ClosingTestBodies.Top).Faces.Concat(block));

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            SolidRepair3 flip = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Flip, flip.Kind);
            Assert.Equal(600.0, flip.Size, 9);
            Assert.Equal(12, closed.Faces.Count);
            Assert.Equal(6600.0, closed.GetVolume(Fine), 6);
            AssertKept(block, closed);
        }

        [Fact]
        public void ABoxWithACavity_AndTheBoxsTopTurnedOver_HasTheTopAloneTurnedBack()
        {
            // The block wound inwards is wound the other way from the box round it, a cavity, and stays so: 6 000 less 600.
            List<GeoFace3> cavity = ClosingTestBodies.Turned(ClosingTestBodies.BlockFaces());
            var body = new GeoSolid3(ClosingTestBodies.BoxWithFaceFlipped(ClosingTestBodies.Top).Faces.Concat(cavity));

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            SolidRepair3 flip = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Flip, flip.Kind);
            Assert.Equal(600.0, flip.Size, 9);
            Assert.Equal(5400.0, closed.GetVolume(Fine), 6);
            AssertKept(cavity, closed);
        }

        [Fact]
        public void ABoxInsideOut_WithACavityWoundOutwards_IsTurnedWithItsCavity_IntoABoxWithACavity()
        {
            // The whole body inside out: the box wound inwards, the cavity in the middle of it the other way. The box lies in
            // no other and is turned, and the cavity with it, keeping its winding against the box: every face, 2 200 and 24.
            var body = new GeoSolid3(ClosingTestBodies.Turned(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners()).Concat(ClosingTestBodies.CavityFaces())));
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.InsideOut);

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            Assert.Equal(12, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Flip, repair.Kind));
            Assert.Equal(2224.0, report.Repairs.Sum(repair => repair.Size), 6);
            Assert.Equal(5992.0, closed.GetVolume(Fine), 6);
            AssertKept(ClosingTestBodies.CavityFaces(), closed);
        }

        [Fact]
        public void ABoxInsideOut_WithABlockInsideOut_IsTurnedWithItsBlock_TheBlockStayingABlock()
        {
            // Both wound inwards, the same way: a block within the box, the whole solid inside out. Turned with the box, the
            // block stays a block; left as it is, it would be a cavity in the box turned, 5 400. Every face, 2 200 and 440.
            var body = new GeoSolid3(ClosingTestBodies.Turned(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners()).Concat(ClosingTestBodies.BlockFaces())));
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.InsideOut);

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            Assert.Equal(12, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Flip, repair.Kind));
            Assert.Equal(2640.0, report.Repairs.Sum(repair => repair.Size), 6);
            Assert.Equal(6600.0, closed.GetVolume(Fine), 6);
            AssertKept(ClosingTestBodies.BlockFaces(), closed);
        }

        [Fact]
        public void ABoxWithACavityHoldingABlock_AndTheBoxsTopTurnedOver_HasTheTopAloneTurnedBack()
        {
            // Three shells, each in the next: the box outwards, the cavity in it inwards and the block in the cavity outwards,
            // each wound against the one round it, 6 000 less 600 and 64 back. Only the top is wrong.
            List<GeoFace3> inside = ClosingTestBodies.Turned(ClosingTestBodies.BlockFaces()).Concat(ClosingTestBodies.InnerBlockFaces()).ToList();
            var body = new GeoSolid3(ClosingTestBodies.BoxWithFaceFlipped(ClosingTestBodies.Top).Faces.Concat(inside));

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            SolidRepair3 flip = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Flip, flip.Kind);
            Assert.Equal(600.0, flip.Size, 9);
            Assert.Equal(5464.0, closed.GetVolume(Fine), 6);
            AssertKept(inside, closed);
        }

        [Fact]
        public void ABoxWithACavityHoldingABlock_AllThreeInsideOut_IsTurnedWhole()
        {
            // The box inwards, the cavity outwards and the block in it inwards: the whole body inside out. The box lies in no
            // other and is turned with both shells inside it, each keeping its winding: every face, 2 200, 440 and 96.
            var body = new GeoSolid3(ClosingTestBodies.Turned(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners())
                .Concat(ClosingTestBodies.Turned(ClosingTestBodies.BlockFaces()))
                .Concat(ClosingTestBodies.InnerBlockFaces())));
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.InsideOut);

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            Assert.Equal(18, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Flip, repair.Kind));
            Assert.Equal(2736.0, report.Repairs.Sum(repair => repair.Size), 6);
            Assert.Equal(5464.0, closed.GetVolume(Fine), 6);
        }

        [Fact]
        public void ABlockInsideABox_WithTheBlocksTopTurnedOver_HasThatFaceAloneTurnedBack()
        {
            // The block's own faces say which way it is wound, five of them against its top: turned back, 100, the block is
            // wound the same way as the box round it and stays a block. The box keeps every face.
            List<GeoFace3> box = ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners());
            List<GeoFace3> block = ClosingTestBodies.BlockFaces();
            block[ClosingTestBodies.Top] = block[ClosingTestBodies.Top].Flip();
            var body = new GeoSolid3(box.Concat(block));

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            SolidRepair3 flip = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Flip, flip.Kind);
            Assert.Equal(100.0, flip.Size, 9);
            Assert.Equal(6600.0, closed.GetVolume(Fine), 6);
            AssertKept(box, closed);
        }

        [Fact]
        public void TwoBoxesApart_EachWithABlockInside_OneInsideOutWithItsBlock_HaveThoseTwoAloneTurned()
        {
            // The first box and its block wound inwards, that solid inside out; the second, 100 along x, and its block
            // outwards. The two hold nothing together. Only the first box is outermost and wound inwards: it is turned with
            // its block, 2 200 and 440, and the second keeps every face of both. Two boxes with a block each, 13 200.
            List<GeoFace3> second = ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(100, 0, 0, 130, 20, 10))
                .Concat(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(110, 5, 2, 120, 15, 8)))
                .ToList();
            List<GeoFace3> first = ClosingTestBodies.Turned(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners()).Concat(ClosingTestBodies.BlockFaces()));
            var body = new GeoSolid3(first.Concat(second));
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.NoVolume);

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            Assert.Equal(12, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Flip, repair.Kind));
            Assert.Equal(2640.0, report.Repairs.Sum(repair => repair.Size), 6);
            Assert.Equal(13200.0, closed.GetVolume(Fine), 6);
            AssertKept(second, closed);
        }

        [Fact]
        public void ABoxMissingItsTop_WithACavity_IsFilled_AndKeepsTheCavity()
        {
            // The cavity is closed and wound inwards inside a shell still open: no outermost shell, its sign waits for the
            // closing. Once the top is filled, it is wound against the box round it, a cavity, and stays so.
            List<GeoFace3> cavity = ClosingTestBodies.CavityFaces();
            var body = new GeoSolid3(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).Faces.Concat(cavity));

            SolidClosing3 report = AssertClosed(body, Filling, out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(600.0, fill.Size, 9);
            Assert.Equal(600.0, report.AddedArea, 9);
            Assert.Equal(5992.0, closed.GetVolume(Fine), 6);
            AssertKept(cavity, closed);
        }

        [Fact]
        public void ABoxMissingItsTop_WithABlockInside_IsFilled_AndKeepsTheBlock()
        {
            // The block is closed and wound outwards inside a shell still open. Once the top is filled, it is wound the same
            // way as the box round it, a block, and stays so: turned, it would be a cavity, and the body would hold 5 400.
            List<GeoFace3> block = ClosingTestBodies.BlockFaces();
            var body = new GeoSolid3(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).Faces.Concat(block));

            SolidClosing3 report = AssertClosed(body, Filling, out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(600.0, fill.Size, 9);
            Assert.Equal(6600.0, closed.GetVolume(Fine), 6);
            AssertKept(block, closed);
        }

        [Fact]
        public void ABoxMissingItsTop_InsideOut_WithABlockInsideOut_IsFilled_AndTurnedWithItsBlock()
        {
            // The five faces of the box and the block all wound inwards: an open shell keeps its winding, and the top is
            // filled as they run. Closed, the box lies in no other and is wound inwards: it is turned, its fill with it, and
            // the block inside it too, staying a block. Five faces of the box, 1 600, and the block's six, 440, and 6 600.
            GeoSolid3 open = ClosingTestBodies.BoxWithout(ClosingTestBodies.Top);
            var body = new GeoSolid3(ClosingTestBodies.Turned(open.Faces.Concat(ClosingTestBodies.BlockFaces())));

            SolidClosing3 report = AssertClosed(body, Filling, out GeoSolid3 closed);
            List<SolidRepair3> flips = Of(report, SolidRepairKind.Flip);
            SolidRepair3 fill = Assert.Single(Of(report, SolidRepairKind.Fill));
            Assert.Equal(12, report.Repairs.Count);
            Assert.Equal(2040.0, flips.Sum(repair => repair.Size), 6);
            Assert.Equal(600.0, fill.Size, 9);
            Assert.Equal(6600.0, closed.GetVolume(Fine), 6);
            AssertKept(ClosingTestBodies.BlockFaces(), closed);
        }

        [Fact]
        public void ABoxMissingItsTopAndBottom_WithACavity_IsCappedTwice_AndKeepsTheCavity()
        {
            // With two faces left out, the four sides cover about a third of the sphere round any point of the cavity, and
            // read so, the shell still open does not hold it. It may yet: capped twice, top and bottom, the box holds it,
            // wound against the box round it, a cavity, and it stays so. Turned outwards, the body would hold 6 008.
            List<GeoFace3> cavity = ClosingTestBodies.CavityFaces();
            GeoSolid3 open = ClosingTestBodies.WithoutFaces(ClosingTestBodies.Box(), ClosingTestBodies.Top, ClosingTestBodies.Bottom);
            var body = new GeoSolid3(open.Faces.Concat(cavity));

            SolidClosing3 report = AssertClosed(body, Filling, out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Fill, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.Equal(600.0, repair.Size, 9));
            Assert.Equal(5992.0, closed.GetVolume(Fine), 6);
            AssertKept(cavity, closed);
        }

        [Fact]
        public void AnLShapedPrismMissingItsTop_WithABlockInsideOutInItsNotch_IsFilled_AndHasTheBlockTurned()
        {
            // The block lies in the box of the L, in its notch, and touches nothing: it may lie inside the shell still open,
            // and its sign waits. Once the top is filled, 300, the L holds it not, so it is outermost and wound inwards, and
            // is turned, its six faces, 192: the L, 3 000, and the block, 180.
            List<GeoFace3> block = ClosingTestBodies.Turned(ClosingTestBodies.NotchBlockFaces());
            GeoSolid3 open = ClosingTestBodies.WithoutFaces(ClosingTestBodies.LShapedPrism(), ClosingTestBodies.Top);
            var body = new GeoSolid3(open.Faces.Concat(block));

            SolidClosing3 report = AssertClosed(body, Filling, out GeoSolid3 closed);
            List<SolidRepair3> flips = Of(report, SolidRepairKind.Flip);
            SolidRepair3 fill = Assert.Single(Of(report, SolidRepairKind.Fill));
            Assert.Equal(7, report.Repairs.Count);
            Assert.Equal(192.0, flips.Sum(repair => repair.Size), 6);
            Assert.Equal(300.0, fill.Size, 9);
            Assert.Equal(3180.0, closed.GetVolume(Fine), 6);
            AssertKept(open.Faces, closed);
        }

        [Fact]
        public void ABoxOpenByAHairAtACorner_WithABlockInside_IsWelded_AndKeepsTheBlock()
        {
            // The top's corner over (30, 20) moved 0.003 out of it leaves the box a shell still open, and the block's sign
            // waits for the welding. Welded, the box is closed and wound outwards, and the block, wound the same way, stays a
            // block: welds alone, and 6 600 within what the corner moved.
            List<GeoFace3> block = ClosingTestBodies.BlockFaces();
            var body = new GeoSolid3(ClosingTestBodies.BoxWithTopCornersMovedOut((6, 0.003)).Faces.Concat(block));

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            Assert.NotEmpty(report.Repairs);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            Assert.InRange(closed.GetVolume(Fine), 6600.0 - (0.003 * ClosingTestBodies.Area), 6600.0 + (0.003 * ClosingTestBodies.Area));
            AssertKept(block, closed);
        }

        [Fact]
        public void ABoxOpenByAHairAtACorner_InsideOut_WithABlockInsideOut_IsWelded_AndTurnedWithItsBlock()
        {
            // All wound inwards. Welded, the box is closed, lies in no other and is wound inwards: it is turned, and the block
            // inside it with it, staying a block, twelve faces, 2 640 and what the corner moved. Left as it is, the block
            // would be a cavity in the box turned, 5 400.
            GeoSolid3 open = ClosingTestBodies.BoxWithTopCornersMovedOut((6, 0.003));
            var body = new GeoSolid3(ClosingTestBodies.Turned(open.Faces.Concat(ClosingTestBodies.BlockFaces())));

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            List<SolidRepair3> flips = Of(report, SolidRepairKind.Flip);
            Assert.Equal(12, flips.Count);
            Assert.NotEmpty(Of(report, SolidRepairKind.Weld));
            Assert.Equal(report.Repairs.Count, flips.Count + Of(report, SolidRepairKind.Weld).Count);
            Assert.InRange(flips.Sum(repair => repair.Size), 2640.0 - 0.1, 2640.0 + 0.1);
            Assert.InRange(closed.GetVolume(Fine), 6600.0 - (0.003 * ClosingTestBodies.Area), 6600.0 + (0.003 * ClosingTestBodies.Area));
        }

        // Valid already: the very body back and nothing done, whatever the options allow.
        private static void AssertTakenAsItIs(GeoSolid3 body)
        {
            SolidValidation3 check = body.Validate(Fine);
            Assert.True(check.IsValid, check.ToString());

            foreach (SolidClosingOptions options in new[] { NoFill, Filling })
            {
                Assert.True(body.TryClose(out GeoSolid3 closed, options, out SolidClosing3 report));
                Assert.Same(body, closed);
                Assert.Empty(report.Repairs);
                Assert.Equal(ClosingFailure.None, report.Failure);
            }
        }

        // Closed: another body, valid, from one that is not, with nothing gone wrong.
        private static SolidClosing3 AssertClosed(GeoSolid3 body, SolidClosingOptions options, out GeoSolid3 closed)
        {
            Assert.False(body.Validate(Fine).IsValid);
            Assert.True(body.TryClose(out closed, options, out SolidClosing3 report), report.ToString());
            Assert.NotSame(body, closed);
            SolidValidation3 check = closed.Validate(Fine);
            Assert.True(check.IsValid, check.ToString());
            Assert.Equal(ClosingFailure.None, report.Failure);
            Assert.Null(report.FailureLocation);
            return report;
        }

        // The repairs of one kind, in their order.
        private static List<SolidRepair3> Of(SolidClosing3 report, SolidRepairKind kind)
            => report.Repairs.Where(repair => repair.Kind == kind).ToList();

        // Each face in the body closed, as it was given and the same way round.
        private static void AssertKept(IEnumerable<GeoFace3> faces, GeoSolid3 closed)
        {
            foreach (GeoFace3 face in faces)
            {
                Assert.Contains(face, closed.Faces);
            }
        }
    }
}
