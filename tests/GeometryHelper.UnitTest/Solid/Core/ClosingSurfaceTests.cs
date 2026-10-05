using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid, its first stages: what the options take and refuse; a body valid already, taken as it is; the
    /// faces cleaned, a copy dropped and faces turned so that the body is wound alike and outwards; and the loops left
    /// open read, a fin stopping the closing where it stands and a hole stopping it where no fill is allowed; see
    /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// The damaged bodies are the box of <see cref="ClosingTestBodies"/>, 30 by 20 by 10, 6 000 in all: its top and bottom
    /// are 600 each, its front and back 300 and its sides 200.
    /// </remarks>
    public class ClosingSurfaceTests
    {
        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        // Welds no further than five thousandths, and fills nothing.
        private static readonly SolidClosingOptions NoFill = new SolidClosingOptions(Fine, 0.005);

        // Fills a hole of any size, out of flat by up to half a millimetre, by the least area whatever else would.
        private static readonly SolidClosingOptions AnyFill = new SolidClosingOptions(Fine, 0.005, double.PositiveInfinity, 0.5, FillStrategy.MinArea);

        [Fact]
        public void Options_RefuseAMaxGapThatIsNoDistance()
        {
            // A gap is closed by moving corners up to that far: nought closes nothing, and no distance is endless.
            foreach (double gap in new[] { 0.0, -0.005, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new SolidClosingOptions(Fine, gap));
                Assert.Equal("maxGap", thrown.ParamName);
            }

            Assert.Equal(1E-6, new SolidClosingOptions(Fine, 1E-6).MaxGap);
        }

        [Fact]
        public void Options_RefuseAMaxHoleAreaThatIsNoArea_AndTakeInfinityForNoLimit()
        {
            // Nought fills no hole and infinity fills any; less than nought is no area.
            foreach (double area in new[] { -1.0, double.NaN, double.NegativeInfinity })
            {
                ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new SolidClosingOptions(Fine, 0.005, area));
                Assert.Equal("maxHoleArea", thrown.ParamName);
            }

            Assert.Equal(double.PositiveInfinity, new SolidClosingOptions(Fine, 0.005, double.PositiveInfinity).MaxHoleArea);
            Assert.Equal(0.0, new SolidClosingOptions(Fine, 0.005, 0.0).MaxHoleArea);
        }

        [Fact]
        public void Options_RefuseAMaxOffFlatThatIsNoDistance()
        {
            // How far a hole may stand off flat and still be filled: nought fills flat holes only, and no distance is endless.
            foreach (double off in new[] { -0.001, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new SolidClosingOptions(Fine, 0.005, 100.0, off));
                Assert.Equal("maxOffFlat", thrown.ParamName);
            }

            Assert.Equal(0.0, new SolidClosingOptions(Fine, 0.005, 100.0, 0.0).MaxOffFlat);
        }

        [Fact]
        public void Options_RefuseAFillThatIsNoStrategy()
        {
            // Numbers cast to the strategy that name none of its three ways.
            foreach (FillStrategy fill in new[] { (FillStrategy)3, (FillStrategy)(-1), (FillStrategy)99 })
            {
                ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new SolidClosingOptions(Fine, 0.005, 100.0, 0.1, fill));
                Assert.Equal("fill", thrown.ParamName);
            }
        }

        [Fact]
        public void Options_HoldWhatTheyAreGiven()
        {
            // Each of the five as it was given.
            var coarse = new Tolerance(0.01, 1E-4, Tolerance.DefaultEqualAngleRad, 0.02);
            var options = new SolidClosingOptions(coarse, 0.03, 250.0, 0.5, FillStrategy.MinArea);
            Assert.Equal(coarse, options.Tolerance);
            Assert.Equal(0.03, options.MaxGap);
            Assert.Equal(250.0, options.MaxHoleArea);
            Assert.Equal(0.5, options.MaxOffFlat);
            Assert.Equal(FillStrategy.MinArea, options.Fill);

            // Given a gap alone, they fill nothing: no area may be filled, flat or not, and a fill would be taken only where
            // it is the one way.
            var plain = new SolidClosingOptions(Fine, 0.005);
            Assert.Equal(Fine, plain.Tolerance);
            Assert.Equal(0.005, plain.MaxGap);
            Assert.Equal(0.0, plain.MaxHoleArea);
            Assert.Equal(0.0, plain.MaxOffFlat);
            Assert.Equal(FillStrategy.WhenUnambiguous, plain.Fill);
        }

        [Fact]
        public void Options_AreEqualByWhatTheyHold()
        {
            // Two of the same five things are one, and any one of the five different makes them two.
            var options = new SolidClosingOptions(Fine, 0.005, 250.0, 0.5, FillStrategy.MinArea);
            var same = new SolidClosingOptions(Fine, 0.005, 250.0, 0.5, FillStrategy.MinArea);
            Assert.Equal(options, same);
            Assert.Equal((object)options, (object)same);
            Assert.Equal(options.GetHashCode(), same.GetHashCode());

            Assert.NotEqual(options, new SolidClosingOptions(new Tolerance(0.01, 1E-5), 0.005, 250.0, 0.5, FillStrategy.MinArea));
            Assert.NotEqual(options, new SolidClosingOptions(Fine, 0.006, 250.0, 0.5, FillStrategy.MinArea));
            Assert.NotEqual(options, new SolidClosingOptions(Fine, 0.005, 251.0, 0.5, FillStrategy.MinArea));
            Assert.NotEqual(options, new SolidClosingOptions(Fine, 0.005, 250.0, 0.4, FillStrategy.MinArea));
            Assert.NotEqual(options, new SolidClosingOptions(Fine, 0.005, 250.0, 0.5, FillStrategy.WhenUnambiguous));
            Assert.False(options.Equals(null));
            Assert.False(options.Equals((object)Fine));
        }

        [Fact]
        public void Options_SayWhatTheyHold_WhateverTheCulture()
        {
            // Each value by its name, in invariant culture: a German culture would write 0,005.
            CultureInfo before = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                string text = new SolidClosingOptions(Fine, 0.005, 250.5, 0.25, FillStrategy.MinArea).ToString();

                Assert.Contains("MaxGap: 0.005", text, StringComparison.Ordinal);
                Assert.Contains("MaxHoleArea: 250.5", text, StringComparison.Ordinal);
                Assert.Contains("MaxOffFlat: 0.25", text, StringComparison.Ordinal);
                Assert.Contains("Fill: MinArea", text, StringComparison.Ordinal);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [Fact]
        public void Closing_WithoutOptions_Throws()
        {
            // A null for the options is the caller's mistake, not the body's.
            GeoSolid3 box = ClosingTestBodies.Box();

            ArgumentNullException none = Assert.Throws<ArgumentNullException>(() => box.TryClose(out _, (SolidClosingOptions)null, out _));
            Assert.Equal("options", none.ParamName);

            // The short form makes its options of what it is given, and refuses a gap that is no distance as they do, valid
            // as the body is.
            ArgumentOutOfRangeException noGap = Assert.Throws<ArgumentOutOfRangeException>(() => box.TryClose(out _, Fine, 0.0));
            Assert.Equal("maxGap", noGap.ParamName);
        }

        [Fact]
        public void ABox_IsTakenAsItIs()
        {
            // Six faces, each edge run once each way, wound outwards.
            AssertTakenAsItIs(ClosingTestBodies.Box());
        }

        [Fact]
        public void ABoxMadeOfAnAxisAlignedBox_IsTakenAsItIs()
        {
            // The six faces an axis-aligned box 2 000 by 500 by 300 gives as a solid.
            AssertTakenAsItIs(new GeoAabb3(new GeoPoint3(-500, 200, 0), new GeoPoint3(1500, 700, 300)).ToObb().ToSolid());
        }

        [Fact]
        public void AnLShapedPrism_IsTakenAsItIs()
        {
            // Its top and bottom are concave, six corners each.
            GeoSolid3 prism = ClosingTestBodies.LShapedPrism();
            Assert.Equal(3000.0, prism.GetVolume(Fine), 6);

            AssertTakenAsItIs(prism);
        }

        [Fact]
        public void TwoBoxesApartInOneBody_AreTakenAsTheyAre()
        {
            // Two shells 70 apart, each closed: one body of 12 000.
            AssertTakenAsItIs(ClosingTestBodies.TwoBoxesApart());
        }

        [Fact]
        public void TwoBoxesMeetingAlongAnEdge_AreTakenAsTheyAre()
        {
            // Four faces meet on the edge the two share, two running it each way: closed, and only noted.
            GeoSolid3 body = ClosingTestBodies.TwoBoxesAlongAnEdge();
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.NonManifoldEdge);

            AssertTakenAsItIs(body);
        }

        [Fact]
        public void APlateWithAHoleThroughIt_IsTakenAsItIs()
        {
            // The rims of the hole in its top and bottom are edges as any other, each met by a wall of the hole.
            GeoSolid3 plate = ClosingTestBodies.PlateWithAHole();
            Assert.Equal(8000.0, plate.GetVolume(Fine), 6);

            AssertTakenAsItIs(plate);
        }

        [Fact]
        public void ABoxWithACavity_IsTakenAsItIs()
        {
            // A shell wound inwards inside another is a cavity, 2 by 2 by 2 in the middle of the box.
            var body = new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners()).Concat(ClosingTestBodies.CavityFaces()));
            Assert.Equal(5992.0, body.GetVolume(Fine), 6);

            AssertTakenAsItIs(body);
        }

        [Fact]
        public void ABoxWithASheetInside_ReadsValid_AndIsTakenAsItIs()
        {
            // Two faces back to back inside the box run each of their edges once each way and hold nothing between them: the
            // body reads valid, and what reads valid is taken as it is, the sheet and all.
            AssertTakenAsItIs(ClosingTestBodies.BoxWithASheetInside());
        }

        [Fact]
        public void ABoxInsideOut_IsTurnedOutwards()
        {
            // Every face wound the wrong way round: closed and wound alike, enclosing less than nothing.
            GeoSolid3 body = ClosingTestBodies.BoxInsideOut();
            SolidValidation3 check = body.Validate(Fine);
            Assert.True(check.IsClosed);
            Assert.True(check.IsWoundAlike);
            Assert.Contains(check.Issues, issue => issue.Kind == SolidIssueKind.InsideOut);

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Flip, repair.Kind));
            Assert.Equal(ClosingTestBodies.Area, report.Repairs.Sum(repair => repair.Size), 6);
            Assert.Equal(0.0, report.AddedArea);
            Assert.Equal(6, closed.Faces.Count);
            Assert.Equal(ClosingTestBodies.Volume, closed.GetVolume(Fine), 6);
        }

        [Fact]
        public void ABoxWithOneFaceTurnedOver_HasThatFaceTurnedBack()
        {
            // The right, 20 by 10, wound the wrong way round: closed, but it runs each of its edges the way the face beside
            // it does. It is not the first face, the one a turning might start from.
            GeoSolid3 body = ClosingTestBodies.BoxWithFaceFlipped(ClosingTestBodies.Right);
            SolidValidation3 check = body.Validate(Fine);
            Assert.True(check.IsClosed);
            Assert.False(check.IsWoundAlike);

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            SolidRepair3 flip = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Flip, flip.Kind);
            Assert.Equal(200.0, flip.Size, 9);
            Assert.Equal(0.0, report.AddedArea);
            Assert.Equal(6, closed.Faces.Count);
            Assert.Equal(ClosingTestBodies.Volume, closed.GetVolume(Fine), 6);
        }

        [Fact]
        public void ABoxWithAFaceListedTwice_HasTheCopyDropped()
        {
            // The front, 30 by 10, given twice the same way round: three faces on each of its edges, two running it one way,
            // and read so they are open. One of the two goes, and the box is as it was.
            GeoSolid3 body = ClosingTestBodies.BoxWithFaceTwice(ClosingTestBodies.Front);
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.OpenEdge && issue.Faces.Count == 3);

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            SolidRepair3 drop = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Drop, drop.Kind);
            Assert.Equal(300.0, drop.Size, 9);
            Assert.Equal(0.0, report.AddedArea);
            Assert.Equal(6, closed.Faces.Count);
            Assert.Equal(ClosingTestBodies.Volume, closed.GetVolume(Fine), 6);
        }

        [Fact]
        public void ABoxWithAFaceAndACopyTurnedOver_HasTheCopyDropped()
        {
            // The front and a copy of it facing into the box, lying back to back: three faces on each of its edges, as for a
            // face listed twice. The front runs its edges as the faces beside it want and the copy against them, so the copy
            // goes, listed after the front or before it.
            foreach (bool copyFirst in new[] { false, true })
            {
                GeoSolid3 body = ClosingTestBodies.BoxWithFaceAndItsFlip(ClosingTestBodies.Front, copyFirst);
                Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.OpenEdge && issue.Faces.Count == 3);

                SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
                SolidRepair3 drop = Assert.Single(report.Repairs);
                Assert.Equal(SolidRepairKind.Drop, drop.Kind);
                Assert.Equal(300.0, drop.Size, 9);
                Assert.Equal(0.0, report.AddedArea);
                Assert.Equal(6, closed.Faces.Count);
                Assert.Equal(ClosingTestBodies.Volume, closed.GetVolume(Fine), 6);
            }
        }

        [Fact]
        public void TwoBoxesApart_OneWithItsRightTurnedOver_HaveThatFaceAloneTurnedBack()
        {
            // A shell closed already is left as it is: the first box keeps every face it came with.
            List<GeoFace3> first = ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners());
            List<GeoFace3> second = ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(100, 0, 0, 130, 20, 10));
            second[ClosingTestBodies.Right] = second[ClosingTestBodies.Right].Flip();

            SolidClosing3 report = AssertClosed(new GeoSolid3(first.Concat(second)), NoFill, out GeoSolid3 closed);
            SolidRepair3 flip = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Flip, flip.Kind);
            Assert.Equal(200.0, flip.Size, 9);
            Assert.Equal(2.0 * ClosingTestBodies.Volume, closed.GetVolume(Fine), 6);

            foreach (GeoFace3 face in first)
            {
                Assert.Contains(face, closed.Faces);
            }
        }

        [Fact]
        public void TwoEqualBoxesApart_OneInsideOut_HaveThatOneTurnedOutwards()
        {
            // Wound one each way, the two hold nothing together and read as a body of no volume. The one wound inwards lies
            // within no other, so it is no cavity: it is turned, and the first keeps its faces.
            List<GeoFace3> first = ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners());
            IEnumerable<GeoFace3> second = ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(100, 0, 0, 130, 20, 10)).Select(face => face.Flip());
            var body = new GeoSolid3(first.Concat(second));
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.NoVolume);

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Flip, repair.Kind));
            Assert.Equal(ClosingTestBodies.Area, report.Repairs.Sum(repair => repair.Size), 6);
            Assert.Equal(2.0 * ClosingTestBodies.Volume, closed.GetVolume(Fine), 6);

            foreach (GeoFace3 face in first)
            {
                Assert.Contains(face, closed.Faces);
            }
        }

        [Fact]
        public void ABoxWithACavity_AndItsRightTurnedOver_HasTheRightAloneTurnedBack()
        {
            // The cavity is wound inwards as a cavity is, and stays so. Turned outwards, it would be a block of its own
            // within the box, closed and wound alike, and the body would read valid holding 6 008.
            List<GeoFace3> faces = ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners());
            faces[ClosingTestBodies.Right] = faces[ClosingTestBodies.Right].Flip();
            var body = new GeoSolid3(faces.Concat(ClosingTestBodies.CavityFaces()));

            SolidClosing3 report = AssertClosed(body, NoFill, out GeoSolid3 closed);
            SolidRepair3 flip = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Flip, flip.Kind);
            Assert.Equal(200.0, flip.Size, 9);
            Assert.Equal(5992.0, closed.GetVolume(Fine), 6);
        }

        [Fact]
        public void TheVolumeChange_IsWhatTheResultHolds_LessWhatTheFacesGivenHeld_FromTheMiddleOfTheirBox()
        {
            // Measured from the middle of the box, each of its faces holds a sixth of it, 1 000. Listed twice, the front holds
            // that twice over, and the faces given 7 000; turned over, the right holds it the other way, and they 4 000.
            SolidClosing3 dropped = AssertClosed(ClosingTestBodies.BoxWithFaceTwice(ClosingTestBodies.Front), NoFill, out _);
            Assert.Equal(-1000.0, dropped.VolumeChange, 6);

            SolidClosing3 turned = AssertClosed(ClosingTestBodies.BoxWithFaceFlipped(ClosingTestBodies.Right), NoFill, out _);
            Assert.Equal(2000.0, turned.VolumeChange, 6);
        }

        [Fact]
        public void ABoxWithAFinOnAnEdge_IsRefusedAsNonManifold_AtThatEdge()
        {
            // A face standing off the front edge of the top at a slant, 5 out and 5 up: three faces meet on that edge, and the
            // loop round the fin runs along it. Filled, that loop would be the fin again the other way round, a sheet that
            // reads as closed; the fin stops the closing before any fill is looked at.
            GeoPoint3[] c = ClosingTestBodies.Corners();
            GeoSolid3 body = ClosingTestBodies.BoxWithFin(4, 5, new GeoVector3(0, -5, 5));
            Assert.Contains(body.Validate(Fine).Issues, issue => issue.Kind == SolidIssueKind.OpenEdge && issue.Faces.Count == 3);

            foreach (SolidClosingOptions options in new[] { NoFill, AnyFill })
            {
                GeoPoint3 at = AssertRefused(body, options, ClosingFailure.NonManifold);
                Assert.InRange(ClosingTestBodies.DistanceToSegment(at, c[4], c[5]), 0.0, 1E-6);
            }
        }

        [Fact]
        public void ABoxMissingItsTop_WithAFinOnTheBottom_IsRefusedAsNonManifold_AtTheFin()
        {
            // Two loops are open, the rim of the top and the loop round a fin on the front edge of the bottom: the fin's
            // stops the closing before the hole is looked at, whatever fill is allowed.
            GeoPoint3[] c = ClosingTestBodies.Corners();
            List<GeoFace3> faces = ClosingTestBodies.BoxFaces(c);
            faces.RemoveAt(ClosingTestBodies.Top);
            faces.Add(ClosingTestBodies.Fin(c[0], c[1], new GeoVector3(0, -5, -5)));
            var body = new GeoSolid3(faces);

            foreach (SolidClosingOptions options in new[] { NoFill, AnyFill })
            {
                GeoPoint3 at = AssertRefused(body, options, ClosingFailure.NonManifold);
                Assert.InRange(ClosingTestBodies.DistanceToSegment(at, c[0], c[1]), 0.0, 1E-6);
            }
        }

        [Fact]
        public void ABoxMissingItsTop_WithNoFillAllowed_IsRefusedAsAHoleTooLarge_InTheHole()
        {
            // The rim of the top is a loop of four edges, one face on each, lying flat; no area may be filled, and the
            // trouble is where the top was: on its plane, within its 30 by 20.
            GeoSolid3 body = ClosingTestBodies.BoxWithout(ClosingTestBodies.Top);
            Assert.False(body.Validate(Fine).IsClosed);

            GeoPoint3 at = AssertRefused(body, NoFill, ClosingFailure.HoleTooLarge);
            Assert.InRange(at.Z, ClosingTestBodies.SizeZ - 1E-6, ClosingTestBodies.SizeZ + 1E-6);
            Assert.InRange(at.X, -1E-6, ClosingTestBodies.SizeX + 1E-6);
            Assert.InRange(at.Y, -1E-6, ClosingTestBodies.SizeY + 1E-6);
        }

        [Fact]
        public void TheShortForm_FillsNothing_AndOtherwiseClosesAsItsOptionsDo()
        {
            // Within a tolerance and a gap, filling nothing: as options that fill no hole close the body.
            GeoSolid3 open = ClosingTestBodies.BoxWithout(ClosingTestBodies.Top);
            Assert.False(open.TryClose(out GeoSolid3 notClosed, Fine, 0.005));
            Assert.Null(notClosed);
            Assert.False(open.TryClose(out _, NoFill, out _));

            GeoSolid3 box = ClosingTestBodies.Box();
            Assert.True(box.TryClose(out GeoSolid3 same, Fine, 0.005));
            Assert.Same(box, same);

            GeoSolid3 turned = ClosingTestBodies.BoxWithFaceFlipped(ClosingTestBodies.Right);
            Assert.True(turned.TryClose(out GeoSolid3 withTolerance, Fine, 0.005));
            Assert.True(turned.TryClose(out GeoSolid3 withOptions, NoFill, out _));
            Assert.Equal(withOptions, withTolerance);
        }

        // Valid already: the very body back and nothing done, whatever the options allow.
        private static void AssertTakenAsItIs(GeoSolid3 body)
        {
            SolidValidation3 check = body.Validate(Fine);
            Assert.True(check.IsValid, check.ToString());

            foreach (SolidClosingOptions options in new[] { NoFill, AnyFill })
            {
                Assert.True(body.TryClose(out GeoSolid3 closed, options, out SolidClosing3 report));
                Assert.Same(body, closed);
                Assert.Empty(report.Repairs);
                Assert.Equal(0.0, report.AddedArea);
                Assert.Equal(0.0, report.VolumeChange);
                Assert.Equal(ClosingFailure.None, report.Failure);
                Assert.Null(report.FailureLocation);
            }
        }

        // Closed: another body, valid, with what was done to it and nothing gone wrong.
        private static SolidClosing3 AssertClosed(GeoSolid3 body, SolidClosingOptions options, out GeoSolid3 closed)
        {
            Assert.True(body.TryClose(out closed, options, out SolidClosing3 report), report.ToString());
            Assert.NotSame(body, closed);
            SolidValidation3 check = closed.Validate(Fine);
            Assert.True(check.IsValid, check.ToString());
            Assert.Equal(ClosingFailure.None, report.Failure);
            Assert.Null(report.FailureLocation);
            Assert.NotEmpty(report.Repairs);
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
    }
}
