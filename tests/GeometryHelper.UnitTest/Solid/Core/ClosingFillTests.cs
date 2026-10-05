using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid with faces left out: a flat hole, its rim lying in one plane within the planar tolerance, filled by
    /// one face, the loops in its plane inside it the face's holes, where it encloses no more than the options allow; see
    /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// The bodies are those of <see cref="ClosingTestBodies"/> with faces left out: the box 30 by 20 by 10, 6 000 in all,
    /// its top and bottom 600 each, its front and back 300 and its sides 200. Measured from the middle of its box, each face
    /// of the box holds a sixth of it, 1 000, so a face filling the rim of one left out adds 1 000 to the faces given.
    /// </remarks>
    public class ClosingFillTests
    {
        private const double Volume = ClosingTestBodies.Volume;

        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        [Fact]
        public void ABoxMissingItsTop_IsFilledByOneFace_WhereAHoleAsLargeIsAllowed()
        {
            // The rim of the top is flat and encloses 600, as much as may be filled: one face, the top again, its middle where
            // the top's was.
            GeoSolid3 body = ClosingTestBodies.BoxWithout(ClosingTestBodies.Top);

            SolidClosing3 report = AssertFilled(body, Filling(600.0), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(600.0, fill.Size, 9);
            Assert.InRange(fill.Location.DistanceTo(new GeoPoint3(15, 10, 10)), 0.0, 1E-9);
            Assert.Equal(600.0, report.AddedArea, 9);
            Assert.Equal(1000.0, report.VolumeChange, 6);
            Assert.Equal(6, closed.Faces.Count);
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void ABoxMissingItsTop_WithLessThanItsAreaAllowed_IsAHoleTooLarge_InTheHole()
        {
            // 599.9 may be filled, and the hole is 600.
            GeoPoint3 at = AssertRefused(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top), Filling(599.9), ClosingFailure.HoleTooLarge);
            AssertInTheTop(at);
        }

        [Fact]
        public void ABoxMissingItsTop_FilledByNone_IsAHoleTooLarge_HoweverLargeAHoleIsAllowed()
        {
            GeoPoint3 at = AssertRefused(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top), Filling(double.PositiveInfinity, FillStrategy.None), ClosingFailure.HoleTooLarge);
            AssertInTheTop(at);
        }

        [Fact]
        public void ABoxMissingItsTopAndBottom_IsFilledTwice()
        {
            // Two loops 10 apart, each flat and each filled by a face of its own; the four sides given hold 4 000 measured
            // from the middle.
            GeoSolid3 body = ClosingTestBodies.WithoutFaces(ClosingTestBodies.Box(), ClosingTestBodies.Top, ClosingTestBodies.Bottom);

            SolidClosing3 report = AssertFilled(body, Filling(600.0), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Fill, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.Equal(600.0, repair.Size, 9));
            Assert.Equal(1200.0, report.AddedArea, 9);
            Assert.Equal(2000.0, report.VolumeChange, 6);
            Assert.Equal(6, closed.Faces.Count);
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void APlateWithAHoleThroughIt_MissingItsTop_IsFilledRoundTheHole()
        {
            // The rim of the plate's top and the rim of the hole lie in one plane, the one inside the other: one fill, the top
            // again with its hole, 900 less 100. The faces facing up at the top cover that and no more.
            GeoSolid3 body = ClosingTestBodies.WithoutFaces(ClosingTestBodies.PlateWithAHole(), 1);

            SolidClosing3 report = AssertFilled(body, Filling(800.0), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(800.0, fill.Size, 9);
            Assert.Equal(800.0, report.AddedArea, 9);
            Assert.Equal(800.0, closed.Faces.Where(face => face.Normal.Z > 0.999 && Math.Abs(face.Boundary[0].Z - 10.0) <= 1E-9).Sum(face => face.Area), 9);
            Assert.Equal(800.0 * 5.0 / 3.0, report.VolumeChange, 6);
            AssertVolume(8000.0, closed);
        }

        [Fact]
        public void AnLShapedPrism_MissingItsTop_IsFilledByOneFace()
        {
            // The rim runs round the L, six corners, one of them inward: 300.
            GeoSolid3 body = ClosingTestBodies.WithoutFaces(ClosingTestBodies.LShapedPrism(), 1);

            SolidClosing3 report = AssertFilled(body, Filling(300.0), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(300.0, fill.Size, 9);
            Assert.Equal(300.0, report.AddedArea, 9);
            Assert.Equal(300.0 * 5.0 / 3.0, report.VolumeChange, 6);
            Assert.Equal(8, closed.Faces.Count);
            AssertVolume(3000.0, closed);
        }

        [Fact]
        public void ARimOffFlatByLessThanThePlanarTolerance_IsFilledByOneFace()
        {
            // The box's corner over (30, 20) raised 0.0004 in the right and the back, its top left out: the rim stands off
            // flat by less than the planar tolerance, a thousandth, and one face fills it, a few hundredths of a unit of
            // volume more than the box.
            GeoSolid3 raised = ClosingTestBodies.BoxWithCornerMoved(6, new GeoVector3(0, 0, 0.0004), ClosingTestBodies.Right, ClosingTestBodies.Back);
            GeoSolid3 body = ClosingTestBodies.WithoutFaces(raised, ClosingTestBodies.Top);

            SolidClosing3 report = AssertFilled(body, Filling(601.0), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.InRange(fill.Size, 600.0 - 0.01, 600.0 + 0.01);
            Assert.Equal(6, closed.Faces.Count);
            Assert.InRange(closed.GetVolume(Fine), Volume, Volume + 0.0004 * 600.0);
        }

        [Fact]
        public void AGapAndAHole_AreWeldedAndFilled()
        {
            // The bottom's corner under (30, 0) moved 0.003 out in the bottom's plane, and the top left out: the thin loop at
            // the corner is a gap, welded first, and the rim of the top a hole, filled after.
            GeoSolid3 moved = ClosingTestBodies.BoxWithCornerMoved(1, new GeoVector3(1, -1, 0).Multiply(0.003 / Math.Sqrt(2.0)), ClosingTestBodies.Bottom);
            GeoSolid3 body = ClosingTestBodies.WithoutFaces(moved, ClosingTestBodies.Top);

            SolidClosing3 report = AssertFilled(body, Filling(600.0), out GeoSolid3 closed);
            int weld = report.Repairs.ToList().FindIndex(repair => repair.Kind == SolidRepairKind.Weld && repair.Size >= 0.0029 && repair.Size <= 0.0031);
            int fill = report.Repairs.ToList().FindIndex(repair => repair.Kind == SolidRepairKind.Fill && Math.Abs(repair.Size - 600.0) <= 1E-6);
            Assert.InRange(weld, 0, report.Repairs.Count - 1);
            Assert.InRange(fill, weld + 1, report.Repairs.Count - 1);
            Assert.Equal(600.0, report.AddedArea, 6);
            Assert.Equal(6, closed.Faces.Count);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.003 * ClosingTestBodies.Area, Volume + 0.003 * ClosingTestBodies.Area);
        }

        [Fact]
        public void ABodyWithAnOpening_MissingAFace_IsFilled_AndKeepsTheOpeningAsItWas()
        {
            // A hole 5 by 5 through the box is carried as an opening, the top of the box round it left out. Filled, the body
            // keeps the very opening, and its material is the box less the hole, 6 000 less 250.
            var opening = new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(5, 5, -1, 10, 10, 11)));
            GeoSolid3 body = ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).WithOpenings(new[] { opening });

            SolidClosing3 report = AssertFilled(body, Filling(600.0), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(600.0, fill.Size, 9);
            Assert.Same(opening, Assert.Single(closed.Openings));
            Assert.Equal(1000.0, report.VolumeChange, 6);
            AssertVolume(5750.0, closed);
        }

        [Theory]
        [InlineData(ClosingTestBodies.Bottom)]
        [InlineData(ClosingTestBodies.Top)]
        [InlineData(ClosingTestBodies.Front)]
        [InlineData(ClosingTestBodies.Back)]
        [InlineData(ClosingTestBodies.Right)]
        [InlineData(ClosingTestBodies.Left)]
        public void ABoxMissingAFace_IsFilledFacingOut_OnEverySide(int side)
        {
            // Whichever face is left out, the one filling its rim faces the way it did, out of the box.
            GeoFace3 missing = ClosingTestBodies.Box().Faces[side];
            GeoSolid3 body = ClosingTestBodies.BoxWithout(side);

            SolidClosing3 report = AssertFilled(body, Filling(600.0), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(missing.Area, fill.Size, 9);
            Assert.Equal(missing.Area, report.AddedArea, 9);
            Assert.Equal(1000.0, report.VolumeChange, 6);
            Assert.Contains(closed.Faces, face => face.Normal.DotProduct(missing.Normal) > 0.999 && Math.Abs(face.Area - missing.Area) <= 1E-9);
            Assert.Equal(6, closed.Faces.Count);
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void APlateWhoseHoleHasNoWalls_IsAHoleAmbiguous_WhereOnlyAnUnambiguousFillIsTaken()
        {
            // The rims of the hole in the plate's top and bottom are two flat loops, the one the other moved 10 along their
            // normal: a cap on each closes the plate over, 9 000, and four walls between them close it round a hole through
            // it, 8 000. Neither way lies on a face of the plate, and either is valid.
            GeoSolid3 body = ClosingTestBodies.WithoutFaces(ClosingTestBodies.PlateWithAHole(), 3, 5, 7, 9);
            Assert.Equal(8, body.Validate(Fine).Issues.Count(issue => issue.Kind == SolidIssueKind.OpenEdge));

            GeoPoint3 at = AssertRefused(body, Filling(1000.0), ClosingFailure.HoleAmbiguous);
            Assert.InRange(Math.Min(Math.Abs(at.Z), Math.Abs(at.Z - 10.0)), 0.0, 1E-6);
            Assert.InRange(at.X, 10.0 - 1E-6, 20.0 + 1E-6);
            Assert.InRange(at.Y, 10.0 - 1E-6, 20.0 + 1E-6);
        }

        [Fact]
        public void APlateWhoseHoleHasNoWalls_IsCapped_WhereTheLeastAreaIsTaken()
        {
            // The caps add 100 each, the walls 400 in all: the least area closes the plate over, as though it had no hole.
            GeoSolid3 body = ClosingTestBodies.WithoutFaces(ClosingTestBodies.PlateWithAHole(), 3, 5, 7, 9);

            SolidClosing3 report = AssertFilled(body, Filling(1000.0, FillStrategy.MinArea), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Fill, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.Equal(100.0, repair.Size, 9));
            Assert.Equal(200.0, report.AddedArea, 9);
            Assert.Equal(1000.0 / 3.0, report.VolumeChange, 6);
            Assert.Equal(8, closed.Faces.Count);
            AssertVolume(9000.0, closed);
        }

        [Fact]
        public void AStrayFaceOnTheInsideOfTheTop_IsDropped_NotClosedByItselfTurnedOver()
        {
            // A face 10 by 10 more, lying on the box's top and facing into it: one-sided, its rim an open loop of its own.
            // Filled, the loop would take the face turned over, the two lying back to back on the top, which reads valid and
            // holds nothing. The face pairs with nothing along any edge and lies back to back with the top, a sheet of no
            // thickness: it is dropped, and the box is as it was. Measured from the middle of the box, it took 100 times 5
            // over 3 off what the faces given held.
            GeoFace3 stray = ClosingTestBodies.Face(new GeoPoint3(10, 5, 10), new GeoPoint3(10, 15, 10), new GeoPoint3(20, 15, 10), new GeoPoint3(20, 5, 10));
            var body = new GeoSolid3(ClosingTestBodies.Box().Faces.Concat(new[] { stray }));
            Assert.Equal(4, body.Validate(Fine).Issues.Count(issue => issue.Kind == SolidIssueKind.OpenEdge && issue.Faces.Count == 1));

            SolidClosing3 report = AssertFilled(body, Filling(double.PositiveInfinity), out GeoSolid3 closed);
            SolidRepair3 drop = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Drop, drop.Kind);
            Assert.Equal(100.0, drop.Size, 9);
            Assert.InRange(drop.Location.DistanceTo(new GeoPoint3(15, 10, 10)), 0.0, 1E-9);
            Assert.Equal(0.0, report.AddedArea);
            Assert.Equal(500.0 / 3.0, report.VolumeChange, 6);
            Assert.Equal(6, closed.Faces.Count);
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void ABoxMissingItsTop_ItsOtherFacesTurnedInwards_IsTurnedOutAndFilled()
        {
            // The five faces left are wound alike, all into the box. Their sign, decided again on the body closed, says turn
            // them, 1 600 of them, and the top then fills facing out, 600.
            GeoSolid3 body = new GeoSolid3(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).Faces.Select(face => face.Flip()));
            Assert.True(body.Validate(Fine).IsWoundAlike);

            SolidClosing3 report = AssertFilled(body, Filling(600.0), out GeoSolid3 closed);
            Assert.Equal(6, report.Repairs.Count);
            Assert.Equal(5, report.Repairs.Count(repair => repair.Kind == SolidRepairKind.Flip));
            Assert.Equal(1600.0, report.Repairs.Where(repair => repair.Kind == SolidRepairKind.Flip).Sum(repair => repair.Size), 9);
            Assert.Equal(600.0, Assert.Single(report.Repairs, repair => repair.Kind == SolidRepairKind.Fill).Size, 9);
            Assert.Equal(1000.0, report.VolumeChange, 6);
            Assert.Equal(6, closed.Faces.Count);
            AssertVolume(Volume, closed);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void EveryFaceOnCopiesOfItsCorners_IsWelded_NotFilled_ThoughFillingIsAllowed(int seed)
        {
            // Every open loop is the whole outline of a face, 600, 300 or 200, any of which may be filled; but each runs along
            // the outlines beside it within the gap, a gap and no hole, and is welded.
            GeoSolid3 body = ClosingTestBodies.BoxOfMovedCopies(seed, 0.002);

            SolidClosing3 report = AssertFilled(body, Filling(double.PositiveInfinity), out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.Contains(repair.Kind, new[] { SolidRepairKind.Weld, SolidRepairKind.SplitEdge }));
            Assert.Equal(0.0, report.AddedArea);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.002 * ClosingTestBodies.Area, Volume + 0.002 * ClosingTestBodies.Area);
        }

        [Fact]
        public void AFill_IsBuiltOnTheCornersOfTheBodyItself()
        {
            // The box turned 0.3 about the vertical and moved a few tenths, its top left out: half the ends of the stretches
            // Validate finds along the rim stand a rounding, a few 1E-15, off its corners. The face filling it stands on the
            // corners themselves, bit for bit.
            GeoPoint3[] corners = ClosingTestBodies.TurnedCorners(0.3, new GeoVector3(0.1, 0.2, 0.3));
            GeoSolid3 body = ClosingTestBodies.WithoutFaces(new GeoSolid3(ClosingTestBodies.BoxFaces(corners)), ClosingTestBodies.Top);
            var given = new HashSet<GeoPoint3>(body.Faces.SelectMany(face => face.Boundary.Vertices));
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Edge.HasValue && !(given.Contains(issue.Edge.Value.StartPoint) && given.Contains(issue.Edge.Value.EndPoint)));

            AssertFilled(body, Filling(601.0), out GeoSolid3 closed);
            GeoFace3 fill = Assert.Single(closed.Faces, face => !body.Faces.Contains(face));
            Assert.Equal(4, fill.Boundary.VertexCount);
            Assert.All(fill.Boundary.Vertices, corner => Assert.Contains(corner, given));
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void ANarrowStripOfTheTopMissing_IsFilled_WhereFillingIsAllowed()
        {
            // A strip 0.03 wide missing along the top's middle is a gap too wide for five thousandths, and a flat hole a fill
            // may close: the first reason is kept only where nothing after it closes the body. One face of 20 by 0.03 more.
            GeoSolid3 body = ClosingTestBodies.BoxWithAStripOfTheTopMissing(0.03);

            SolidClosing3 report = AssertFilled(body, Filling(1.0), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(0.6, fill.Size, 9);
            Assert.Equal(0.6, report.AddedArea, 9);
            AssertVolume(Volume, closed);
        }

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

        // A point on the plane of the box's top, within its 30 by 20.
        private static void AssertInTheTop(GeoPoint3 at)
        {
            Assert.InRange(at.Z, ClosingTestBodies.SizeZ - 1E-6, ClosingTestBodies.SizeZ + 1E-6);
            Assert.InRange(at.X, -1E-6, ClosingTestBodies.SizeX + 1E-6);
            Assert.InRange(at.Y, -1E-6, ClosingTestBodies.SizeY + 1E-6);
        }

        // Within a millionth of what it should hold.
        private static void AssertVolume(double expected, GeoSolid3 closed)
            => Assert.InRange(closed.GetVolume(Fine), expected * (1.0 - 1E-6), expected * (1.0 + 1E-6));
    }
}
