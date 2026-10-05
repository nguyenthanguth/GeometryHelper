using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid cracked along an edge: two faces meeting on it each run it through corners of their own, a few
    /// thousandths apart at the most and pairing nowhere, and the crack is closed within the gap allowed, or found too wide;
    /// see <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bodies are the box of <see cref="ClosingTestBodies"/>, 30 by 20 by 10, its top's front edge and its front's top
    /// edge each run through corners of their own, bowed off the straight line as a parabola; and a prism over a polygon of
    /// 24 sides whose top has 32 corners on the same circle. Every face is flat.
    /// </para>
    /// <para>
    /// A crack lying in the plane of one face, the top's corners standing off the front's edge within the top, is closed by
    /// putting those corners on the edge. Threaded into the front instead, they would bend it out along the top, and the
    /// front built again on its corners would fold under the top as triangles lying flat against it: a skin of no thickness
    /// that reads valid and holds no material, as a needle does along an edge.
    /// </para>
    /// <para>
    /// A corner is put on an edge within the reach of the edge as it was given, not of the pieces putting another corner on
    /// it left: a crack wider than the gap stays open, however near its corners come to the pieces.
    /// </para>
    /// </remarks>
    public class ClosingCrackTests
    {
        private const double Volume = ClosingTestBodies.Volume;

        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        private static readonly GeoVector3 Out = new GeoVector3(0, -1, 0);

        private static readonly GeoVector3 Down = new GeoVector3(0, 0, -1);

        [Theory]
        [InlineData(1, 0.003)]
        [InlineData(5, 0.004)]
        [InlineData(59, 0.004)]
        public void ACrackInThePlaneOfTheTop_IsClosed_WithNoSkinOfNoThickness(int corners, double sag)
        {
            // The top's front edge runs through corners of its own bowed out of the front, in the top's plane; the front's top
            // edge is straight. One corner is a corner standing off the edge beside it; fifty-nine are a crack half a unit
            // between corners, its middle 0.004 wide.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedAlongTheFront(ClosingTestBodies.Bowed(corners, sag, Out, false), new GeoPoint3[0]);
            Assert.False(body.Validate(Fine).IsClosed);

            SolidClosing3 report = AssertCrackClosed(body, 0.005, sag, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Size, 0.0, sag + 1E-9));
            Assert.InRange(closed.GetVolume(Fine), Volume - sag * 300.0, Volume + sag * 300.0);
        }

        [Fact]
        public void ACrackWithCornersOfItsOwnOnBothSides_IsClosed_WithNoSkinOfNoThickness()
        {
            // The top's front edge through seven corners bowed out by 0.003 in the top's plane, the front's top edge straight
            // through two of its own at x = 20 and 10, none of them pairing.
            var front = new List<GeoPoint3> { new GeoPoint3(20, 0, 10), new GeoPoint3(10, 0, 10) };
            GeoSolid3 body = ClosingTestBodies.BoxCrackedAlongTheFront(ClosingTestBodies.Bowed(7, 0.003, Out, false), front);
            Assert.False(body.Validate(Fine).IsClosed);

            AssertCrackClosed(body, 0.005, 0.003, out GeoSolid3 closed);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.003 * 300.0, Volume + 0.003 * 300.0);
        }

        [Fact]
        public void AnEdgeFacetedTwoWays_IsClosed_WithNoSkinOfNoThickness()
        {
            // A prism of 24 sides on a circle of radius 0.5 whose top has 32 corners on the same circle, sharing 8: the top
            // stands up to 0.0043 off the sides' top edges and they off it, in its plane. Closed, it holds what the prism of
            // 24 sides holds, or a little more where the sides are bent out to the top, and no more than the prism of 32.
            GeoSolid3 body = ClosingTestBodies.FacetedPrism(0.5, 24, 32);
            Assert.False(body.Validate(Fine).IsClosed);
            double low = 12.0 * 0.25 * Math.Sin(2.0 * Math.PI / 24.0);
            double high = 16.0 * 0.25 * Math.Sin(2.0 * Math.PI / 32.0);

            AssertCrackClosed(body, 0.005, 0.0043, out GeoSolid3 closed);
            Assert.InRange(closed.GetVolume(Fine), low - 1E-9, high + 1E-9);
        }

        [Theory]
        [InlineData(5, 0.002, 3, 0.002)]
        [InlineData(3, 0.002, 4, 0.002)]
        [InlineData(2, 0.0015, 1, 0.002)]
        public void ACrackTwistedBetweenTheTwoFaces_IsClosed(int topCorners, double topSag, int frontCorners, double frontSag)
        {
            // The top's front edge bowed out in the top's plane and the front's top edge bowed down in the front's, through
            // corners at other places: the two sides of the crack lie in no one plane, no wider apart than 0.003 anywhere.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedAlongTheFront(
                ClosingTestBodies.Bowed(topCorners, topSag, Out, false),
                ClosingTestBodies.Bowed(frontCorners, frontSag, Down, true));
            Assert.False(body.Validate(Fine).IsClosed);

            SolidClosing3 report = AssertCrackClosed(body, 0.005, 0.003, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Size, 0.0, 0.003));
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.003 * 300.0, Volume + 0.003 * 300.0);
        }

        [Fact]
        public void ACrackWiderThanTheGap_IsAGapTooWide_AtTheCrack()
        {
            // Bowed out by 0.008 through five corners, the crack is 0.0044 wide at the corners nearest its ends and up to
            // 0.008 between: the two nearest the ends are within five thousandths of the front's edge, and put on it they
            // would leave the three between as near to the pieces of it. No reach of five thousandths closes 0.008.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedAlongTheFront(ClosingTestBodies.Bowed(5, 0.008, Out, false), new GeoPoint3[0]);
            GeoPoint3[] c = ClosingTestBodies.Corners();

            Assert.False(body.TryClose(out GeoSolid3 notClosed, new SolidClosingOptions(Fine, 0.005), out SolidClosing3 report));
            Assert.Equal(ClosingFailure.GapTooWide, report.Failure);
            Assert.Null(notClosed);
            Assert.Empty(report.Repairs);
            Assert.Equal(0.0, report.AddedArea);
            Assert.Equal(0.0, report.VolumeChange);
            Assert.True(report.FailureLocation.HasValue);
            Assert.InRange(ClosingTestBodies.DistanceToSegment(report.FailureLocation.Value, c[4], c[5]), 0.0, 0.01);
        }

        [Fact]
        public void ACrackWiderThanTheGap_IsClosedWithinAWiderOne()
        {
            GeoSolid3 body = ClosingTestBodies.BoxCrackedAlongTheFront(ClosingTestBodies.Bowed(5, 0.008, Out, false), new GeoPoint3[0]);

            AssertCrackClosed(body, 0.01, 0.008, out GeoSolid3 closed);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.008 * 300.0, Volume + 0.008 * 300.0);
        }

        // Closed within a gap, filling nothing: another body, valid, with no needle and no skin of no thickness, each change
        // no larger than the crack is wide.
        private static SolidClosing3 AssertCrackClosed(GeoSolid3 body, double gap, double widest, out GeoSolid3 closed)
        {
            Assert.True(body.TryClose(out closed, new SolidClosingOptions(Fine, gap), out SolidClosing3 report), report.ToString());
            SolidValidation3 check = closed.Validate(Fine);
            Assert.True(check.IsValid, check.ToString());
            Assert.Equal(ClosingFailure.None, report.Failure);
            Assert.Null(report.FailureLocation);
            Assert.NotEmpty(report.Repairs);
            Assert.Equal(0, ClosingTestBodies.Needles(closed));
            Assert.Equal(0, ClosingTestBodies.BackToBack(closed));
            Assert.All(report.Repairs, repair => Assert.Contains(repair.Kind, new[] { SolidRepairKind.Weld, SolidRepairKind.SplitEdge }));
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Size, 0.0, widest + 1E-9));
            return report;
        }
    }
}
