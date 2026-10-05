using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid where a weld would cross the body: the faces a weld changes may cross no face of the body, as no face
    /// added may. A strip of the top missing, no wider than the welds reach, with a blade standing in it is still open at the
    /// blade, the gap not too wide but blocked, whether fills are allowed or not; the same strip with the blade beyond it,
    /// or with no blade, is welded shut, and one touching the strip at an edge or a point is no crossing; see
    /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The check reads no face crossing another, so a body welded through another shell reads valid: the box of
    /// <see cref="ClosingTestBodies"/> with a strip 0.004 wide missing along its top, a blade 0.002 thick standing in the
    /// strip, had the corners across the strip at each end made one, within five thousandths, and the top's halves met
    /// through the blade: valid, and crossing it.
    /// </para>
    /// <para>
    /// The box is 30 by 20 by 10, 6 000, the strip of <see cref="ClosingTestBodies.BoxWithAStripOfTheTopMissing"/> along y
    /// with its middle at x = 15; the blade of <see cref="ClosingTestBodies.BladeFaces"/> stands from (15 less half its
    /// thickness, 8, 5) to (15 and half its thickness, 12, 15). Each case is a strip 0.004 wide welded within 0.005 and
    /// one 0.04 wide welded within 0.05, each with a blade half as thick as the strip is wide.
    /// </para>
    /// </remarks>
    public class ClosingWeldCrossingTests
    {
        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        [Theory]
        [InlineData(0.004, 0.002, 0.005)]
        [InlineData(0.04, 0.02, 0.05)]
        public void AStripOfTheTopMissing_WithABladeStandingInIt_IsRefusedAsStillOpen_AtTheBlade(double strip, double blade, double gap)
        {
            // The strip is within the welds' reach: welded shut, the top's halves would meet through the blade, valid and
            // crossing it. No weld crosses the body, and no fill is allowed, so the body is still open where the blade
            // stands through the top: the gap is not too wide, it is blocked.
            var body = new GeoSolid3(ClosingTestBodies.BoxWithAStripOfTheTopMissing(strip).Faces.Concat(ClosingTestBodies.BladeFaces(blade)));

            GeoPoint3 at = AssertRefused(body, new SolidClosingOptions(Fine, gap), ClosingFailure.StillOpen);
            AssertOnTheBlade(at, blade);
        }

        [Theory]
        [InlineData(0.004, 0.005)]
        [InlineData(0.04, 0.05)]
        public void AStripOfTheTopMissing_WithNoBlade_IsWeldedShut(double strip, double gap)
        {
            // The corners across the strip at each end made one: two welds as long as the strip is wide, and the box again.
            SolidClosing3 report = AssertClosed(ClosingTestBodies.BoxWithAStripOfTheTopMissing(strip), new SolidClosingOptions(Fine, gap), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Size, strip - 1E-9, strip + 1E-9));
            AssertVolume(6000.0, closed);
        }

        [Theory]
        [InlineData(0.004, 0.002, 0.005)]
        [InlineData(0.04, 0.02, 0.05)]
        public void AStripOfTheTopMissing_WithTheBladeBeyondTheFront_ClearOfItByMoreThanTheWeldsReach_IsWeldedShut_TheBladeKept(double strip, double blade, double gap)
        {
            // The blade stands through the top's plane in line with the strip, out beyond the box's front and clear of it by
            // a fifth more than the welds reach: the welded top crosses nothing, and the blade is kept as it is.
            List<GeoFace3> beyond = ClosingTestBodies.BladeBeyondTheFrontFaces(blade, 1.2 * gap);
            var body = new GeoSolid3(ClosingTestBodies.BoxWithAStripOfTheTopMissing(strip).Faces.Concat(beyond));

            SolidClosing3 report = AssertClosed(body, new SolidClosingOptions(Fine, gap), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            AssertVolume(6000.0 + (40.0 * blade), closed);
            AssertKept(beyond, closed);
        }

        [Theory]
        [InlineData(0.004, 0.0016, 0.005)]
        [InlineData(0.04, 0.02, 0.05)]
        public void AStripOfTheTopMissing_WithABladeStandingInIt_FillsAllowed_IsRefusedAsStillOpen_AtTheBlade(double strip, double blade, double gap)
        {
            // Neither a weld nor a fill closes the strip across the blade: a face across the strip would cross it as the
            // welded top would. In the strip 0.004 wide the blade is 0.0016 thick, its walls 0.0012 in from the strip's
            // edges: half the strip, they would stand the point tolerance in, and whether a face across the strip crosses
            // them would be a rounding's call.
            var body = new GeoSolid3(ClosingTestBodies.BoxWithAStripOfTheTopMissing(strip).Faces.Concat(ClosingTestBodies.BladeFaces(blade)));

            GeoPoint3 at = AssertRefused(body, new SolidClosingOptions(Fine, gap, double.PositiveInfinity, 0.0, FillStrategy.WhenUnambiguous), ClosingFailure.StillOpen);
            AssertOnTheBlade(at, blade);
        }

        [Theory]
        [InlineData(0.004, 0.005)]
        [InlineData(0.04, 0.05)]
        public void AStripOfTheTopMissing_WithARidgeLyingAcrossIt_IsWeldedShut_TheTopTouchingTheRidgeAlongAnEdge(double strip, double gap)
        {
            // The ridge of the crossing tests lies on the top's plane across the strip, along its lowest edge from x = 13 to
            // 17: the welded top touches it along that edge, crosses none of it, and the ridge is kept. 6 000 and 24.
            List<GeoFace3> ridge = ClosingTestBodies.RidgeFaces(0.0);
            var body = new GeoSolid3(ClosingTestBodies.BoxWithAStripOfTheTopMissing(strip).Faces.Concat(ridge));

            SolidClosing3 report = AssertClosed(body, new SolidClosingOptions(Fine, gap), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            AssertVolume(6024.0, closed);
            AssertKept(ridge, closed);
        }

        [Theory]
        [InlineData(0.004, 0.005, -0.5)]
        [InlineData(0.004, 0.005, 0.0)]
        [InlineData(0.004, 0.005, 0.5)]
        [InlineData(0.04, 0.05, -0.5)]
        [InlineData(0.04, 0.05, 0.0)]
        [InlineData(0.04, 0.05, 0.5)]
        public void AStripOfTheTopMissing_WithAPyramidOnItsTipAtTheStripsEnd_IsWeldedShut_TheTopTouchingItAtAPoint(double strip, double gap, double across)
        {
            // The pyramid of the crossing tests stands on its tip at the strip's end on the front, on the corner of the top's
            // left half, in the middle of the strip, or on the corner of its right half: whichever corner the weld keeps, the
            // welded top touches the tip at a corner or on an edge, crosses none of it, and the pyramid is kept. 6 000 and 16.
            List<GeoFace3> pyramid = ClosingTestBodies.PyramidOnItsTipFaces(15.0 + (across * strip), 0.0);
            var body = new GeoSolid3(ClosingTestBodies.BoxWithAStripOfTheTopMissing(strip).Faces.Concat(pyramid));

            SolidClosing3 report = AssertClosed(body, new SolidClosingOptions(Fine, gap), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            AssertVolume(6016.0, closed);
            AssertKept(pyramid, closed);
        }

        // Closed: another body, valid, with no needle and no skin of no thickness, nothing gone wrong.
        private static SolidClosing3 AssertClosed(GeoSolid3 body, SolidClosingOptions options, out GeoSolid3 closed)
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
            Assert.False(body.TryClose(out GeoSolid3 closed, options, out SolidClosing3 report), report.ToString());
            Assert.Equal(failure, report.Failure);
            Assert.Null(closed);
            Assert.Empty(report.Repairs);
            Assert.Equal(0.0, report.AddedArea);
            Assert.Equal(0.0, report.VolumeChange);
            Assert.True(report.FailureLocation.HasValue);
            return report.FailureLocation.Value;
        }

        // Where the blade, as thick as given, stands through the top's plane: within its plan, at z = 10.
        private static void AssertOnTheBlade(GeoPoint3 at, double blade)
        {
            Assert.InRange(at.X, 15.0 - (blade / 2) - 1E-6, 15.0 + (blade / 2) + 1E-6);
            Assert.InRange(at.Y, 8.0 - 1E-6, 12.0 + 1E-6);
            Assert.InRange(at.Z, 10.0 - 1E-6, 10.0 + 1E-6);
        }

        // Each face in the body closed, as it was given and the same way round.
        private static void AssertKept(IEnumerable<GeoFace3> faces, GeoSolid3 closed)
        {
            foreach (GeoFace3 face in faces)
            {
                Assert.Contains(face, closed.Faces);
            }
        }

        // Within a millionth of what it should hold.
        private static void AssertVolume(double expected, GeoSolid3 closed)
            => Assert.InRange(closed.GetVolume(Fine), expected * (1.0 - 1E-6), expected * (1.0 + 1E-6));
    }
}
