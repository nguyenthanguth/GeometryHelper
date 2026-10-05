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
    /// Closing a solid where a face added would cross the body: no edge of a face of the body may pass through the inside of
    /// a face added, nor an edge of a face added through the inside of a face of the body. A flat face or triangles that
    /// cross refuse the hole, still open at the crossing; caps that cross are no way, and the walls of the hole are taken;
    /// and a fill that only touches a face, along an edge or at a point, crosses nothing; see
    /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The check reads no face crossing another, so a body closed by a face through another shell reads valid: the box of
    /// <see cref="ClosingTestBodies"/> missing its top, with a post standing in it and poking out through the top's plane,
    /// took the top across the post's four walls and held 6 208, valid.
    /// </para>
    /// <para>
    /// The box is 30 by 20 by 10, 6 000, its top 600 at z = 10; the post of <see cref="ClosingTestBodies.PostFaces"/> is 4 by
    /// 4, from (13, 8) to (17, 12), and stands touching none of the box's faces.
    /// </para>
    /// </remarks>
    public class ClosingCrossingTests
    {
        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        // Both ways of choosing between fills.
        private static readonly FillStrategy[] Strategies = { FillStrategy.WhenUnambiguous, FillStrategy.MinArea };

        [Fact]
        public void ABoxMissingItsTop_WithAPostPokingThroughTheTopsPlane_IsRefusedAsStillOpen_AtTheCrossing()
        {
            // The post stands from z = 2 to 15, 5 out through the plane the top would fill: the top would cross its four walls
            // and close it valid, 6 208. The top is the one way of closing the hole, so the hole is refused, whatever the
            // strategy, where a wall of the post passes through the top.
            var body = new GeoSolid3(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).Faces.Concat(ClosingTestBodies.PostFaces(2, 15)));

            foreach (FillStrategy fill in Strategies)
            {
                GeoPoint3 at = AssertRefused(body, Filling(fill), ClosingFailure.StillOpen);
                AssertOnThePost(at, 10.0, 1E-6);
            }
        }

        [Fact]
        public void ABoxMissingItsTop_WithAPostShortOfTheTopsPlane_IsFilled()
        {
            // The post stands from z = 2 to 8, 2 short of the top: the top crosses nothing and is filled, 600, the post kept
            // as it is, a block within the box, 6 000 and 96.
            List<GeoFace3> post = ClosingTestBodies.PostFaces(2, 8);
            var body = new GeoSolid3(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).Faces.Concat(post));

            foreach (FillStrategy fill in Strategies)
            {
                SolidClosing3 report = AssertFilled(body, Filling(fill), out GeoSolid3 closed);
                SolidRepair3 top = Assert.Single(report.Repairs);
                Assert.Equal(SolidRepairKind.Fill, top.Kind);
                Assert.Equal(600.0, top.Size, 9);
                AssertVolume(6096.0, closed);
                AssertKept(post, closed);
            }
        }

        [Fact]
        public void ABoxMissingItsTopLiftedAtACorner_WithAPostPokingThroughIt_IsRefusedAsStillOpen_AtTheCrossing()
        {
            // The top's corner over (30, 20) lifted 0.005, the rim stands off flat by a quarter of that, and the hole is
            // filled by two triangles, either way across it; the post passes through both ways, so the triangles chosen cross
            // it and the hole is refused, where a wall of the post passes through them, a hair above z = 10.
            GeoSolid3 open = ClosingTestBodies.PrismWithoutItsTopLifted(ClosingTestBodies.Rectangle(0, 0, 30, 20), 10.0, 2, 0.005);
            var body = new GeoSolid3(open.Faces.Concat(ClosingTestBodies.PostFaces(2, 15)));

            foreach (FillStrategy fill in Strategies)
            {
                GeoPoint3 at = AssertRefused(body, Filling(fill, 0.01), ClosingFailure.StillOpen);
                AssertOnThePost(at, 10.0025, 0.0025 + 1E-6);
            }
        }

        [Fact]
        public void ABoxMissingItsTopLiftedAtACorner_WithAPostShortOfIt_IsFilledByTriangles()
        {
            // The post 2 short of the top, the triangles chosen cross nothing and are taken: one fill of a hair over 600, and
            // with the box's and the post's volume what the lifted corner adds under them, a half or one as they fall.
            List<GeoFace3> post = ClosingTestBodies.PostFaces(2, 8);
            GeoSolid3 open = ClosingTestBodies.PrismWithoutItsTopLifted(ClosingTestBodies.Rectangle(0, 0, 30, 20), 10.0, 2, 0.005);
            var body = new GeoSolid3(open.Faces.Concat(post));

            foreach (FillStrategy fill in Strategies)
            {
                SolidClosing3 report = AssertFilled(body, Filling(fill, 0.01), out GeoSolid3 closed);
                SolidRepair3 top = Assert.Single(report.Repairs);
                Assert.Equal(SolidRepairKind.Fill, top.Kind);
                Assert.InRange(top.Size, 600.0, 600.001);
                Assert.Equal(open.Faces.Count + post.Count + 2, closed.Faces.Count);
                Assert.InRange(closed.GetVolume(Fine), 6096.5 - 1E-6, 6097.0 + 1E-6);
                AssertKept(post, closed);
            }
        }

        [Fact]
        public void APlateWithAHoleMissingItsWalls_WithAPostThroughItsTopCapsPlane_IsWalledRound_UnderEitherStrategy()
        {
            // A plate 30 by 30 by 10 with a hole 10 by 10 through it whose walls are left out, and a post 4 by 4 standing in
            // the hole from z = 3 to 13, 3 out through the top's plane, touching nothing. The caps, 100 each, would cross the
            // post and are no way; the walls, 400, cross nothing and close the hole round it, the one way: taken whatever the
            // strategy, where the least area would cap it and an unambiguous fill would see two ways. 8 000 and the post, 160.
            GeoPoint3[] square = ClosingTestBodies.Rectangle(10, 10, 20, 20);
            List<GeoFace3> post = ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(13, 13, 3, 17, 17, 13));
            var body = new GeoSolid3(ClosingTestBodies.PlateWithAHoleWithoutItsWalls(10, square, square).Faces.Concat(post));

            foreach (FillStrategy fill in Strategies)
            {
                SolidClosing3 report = AssertFilled(body, Filling(fill), out GeoSolid3 closed);
                SolidRepair3 walls = Assert.Single(report.Repairs);
                Assert.Equal(SolidRepairKind.Fill, walls.Kind);
                Assert.Equal(400.0, walls.Size, 9);
                AssertVolume(8160.0, closed);
                AssertKept(post, closed);
            }
        }

        [Fact]
        public void APlateWithAHoleMissingItsWalls_WithAPostInTheHoleShortOfBothCaps_HasTwoWaysStill()
        {
            // The post from z = 3 to 7 reaches neither cap's plane: caps and walls cross nothing, and the hole has two ways of
            // closing it as it had. An unambiguous fill takes neither, and the least area caps it, 100 each: 9 000 and 64.
            GeoPoint3[] square = ClosingTestBodies.Rectangle(10, 10, 20, 20);
            List<GeoFace3> post = ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(13, 13, 3, 17, 17, 7));
            var body = new GeoSolid3(ClosingTestBodies.PlateWithAHoleWithoutItsWalls(10, square, square).Faces.Concat(post));

            AssertRefused(body, Filling(FillStrategy.WhenUnambiguous), ClosingFailure.HoleAmbiguous);

            SolidClosing3 report = AssertFilled(body, Filling(FillStrategy.MinArea), out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Fill, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.Equal(100.0, repair.Size, 9));
            AssertVolume(9064.0, closed);
        }

        [Fact]
        public void ABoxMissingItsTop_WithARidgeLyingAlongTheTopsPlane_IsFilled_TheTopTouchingItAlongAnEdge()
        {
            // The ridge stands over the top's plane on its lowest edge, which lies in that plane inside the top: the top
            // touches the ridge along that edge and crosses none of it, and is filled, the ridge kept: 6 000 and 24.
            List<GeoFace3> ridge = ClosingTestBodies.RidgeFaces(0.0);
            var body = new GeoSolid3(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).Faces.Concat(ridge));

            foreach (FillStrategy fill in Strategies)
            {
                SolidClosing3 report = AssertFilled(body, Filling(fill), out GeoSolid3 closed);
                SolidRepair3 top = Assert.Single(report.Repairs);
                Assert.Equal(SolidRepairKind.Fill, top.Kind);
                Assert.Equal(600.0, top.Size, 9);
                AssertVolume(6024.0, closed);
                AssertKept(ridge, closed);
            }
        }

        [Theory]
        [InlineData(15.0, 10.0)]
        [InlineData(15.0, 0.0)]
        [InlineData(30.0, 20.0)]
        public void ABoxMissingItsTop_WithAPyramidOnItsTipOnTheTopsPlane_IsFilled_TheTopTouchingItAtAPoint(double x, double y)
        {
            // The pyramid stands on its tip in the top's plane: in the middle of the top, on its front edge, or on its corner.
            // The top touches it at that point and crosses none of it, and is filled, the pyramid kept: 6 000 and 16.
            List<GeoFace3> pyramid = ClosingTestBodies.PyramidOnItsTipFaces(x, y);
            var body = new GeoSolid3(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).Faces.Concat(pyramid));

            foreach (FillStrategy fill in Strategies)
            {
                SolidClosing3 report = AssertFilled(body, Filling(fill), out GeoSolid3 closed);
                SolidRepair3 top = Assert.Single(report.Repairs);
                Assert.Equal(SolidRepairKind.Fill, top.Kind);
                Assert.Equal(600.0, top.Size, 9);
                AssertVolume(6016.0, closed);
                AssertKept(pyramid, closed);
            }
        }

        [Fact]
        public void ABoxMissingItsTop_WithARidgeSunkIntoTheTopsPlaneByHalfThePlanarTolerance_IsFilled()
        {
            // Its lowest edge 0.0005 below the top's plane, the ridge's slanting edges reach through it by less than the
            // planar tolerance: they end on it, and the top touches the ridge, crossing none of it.
            List<GeoFace3> ridge = ClosingTestBodies.RidgeFaces(0.5 * Fine.EqualPlanar);
            var body = new GeoSolid3(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).Faces.Concat(ridge));

            foreach (FillStrategy fill in Strategies)
            {
                SolidClosing3 report = AssertFilled(body, Filling(fill), out GeoSolid3 closed);
                SolidRepair3 top = Assert.Single(report.Repairs);
                Assert.Equal(SolidRepairKind.Fill, top.Kind);
                AssertVolume(6024.0 + (8.0 * 0.5 * Fine.EqualPlanar), closed);
                AssertKept(ridge, closed);
            }
        }

        [Fact]
        public void ABoxMissingItsTop_WithARidgeSunkIntoTheTopsPlaneByTwiceThePlanarTolerance_IsRefusedAsStillOpen_AtTheCrossing()
        {
            // Its lowest edge 0.002 below the top's plane, the ridge's slanting edges pass through it beyond the planar
            // tolerance, inside the top: the top would cross the ridge, and the hole is refused where they do.
            var body = new GeoSolid3(ClosingTestBodies.BoxWithout(ClosingTestBodies.Top).Faces.Concat(ClosingTestBodies.RidgeFaces(2.0 * Fine.EqualPlanar)));

            foreach (FillStrategy fill in Strategies)
            {
                GeoPoint3 at = AssertRefused(body, Filling(fill), ClosingFailure.StillOpen);
                AssertOnThePost(at, 10.0, 2.0 * Fine.EqualPlanar);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void AHopperMissingItsTop_WithARimCornerLiftedWithinTheTolerance_IsFilled_TheWallsOnlyTouchingTheTop(int corner)
        {
            // A corner of the rim lifted 0.0036 leaves the rim flat within the planar tolerance, off by a quarter of that, and
            // the top is one face across it. Its plane, read through its first corner, can stand twice the tolerance off a
            // corner of the rim, and the walls' edges sloping in under the top from that corner meet it a hair in from the
            // rim: they end on the top's edges, and only touch it. One fill: 600, and the sliver the corner moved out adds.
            const double lift = 0.0036;
            GeoSolid3 open = ClosingTestBodies.HopperMissingItsTop(corner, lift);

            // The hopper with its top across the rim's corners, each face read as the fan of its boundary from its first
            // corner: 2 580, and 200 times the lift where the corner lifted lies on the diagonal of the top's fan, 100 off it.
            double[] wholes = { 2580.0 + (100.0 * lift), 2580.0 + (200.0 * lift) };

            foreach (FillStrategy fill in Strategies)
            {
                SolidClosing3 report = AssertFilled(open, Filling(fill), out GeoSolid3 closed);
                SolidRepair3 top = Assert.Single(report.Repairs);
                Assert.Equal(SolidRepairKind.Fill, top.Kind);
                Assert.InRange(top.Size, 600.0 + (22.5 * lift) - 1E-4, 600.0 + (22.5 * lift) + 1E-4);
                Assert.Equal(open.Faces.Count + 1, closed.Faces.Count);
                double volume = closed.GetVolume(Fine);
                Assert.Contains(wholes, whole => Math.Abs(volume - whole) <= 1E-6 * whole);
                AssertKept(open.Faces, closed);
            }
        }

        [Fact]
        public void TwoBoxesOpenTowardsEachOther_EachAlone_IsFilledByItsTrianglesOfLeastArea()
        {
            // Each rim, about 1.8 off flat, alone: filled by the triangles of least area across it, crossing nothing.
            SolidClosing3 lower = AssertFilled(new GeoSolid3(ClosingTestBodies.BoxOpenUpwardsFaces()), Filling(FillStrategy.MinArea, 1.9), out GeoSolid3 below);
            Assert.InRange(Assert.Single(lower.Repairs).Size, 51.07, 51.08);
            AssertVolume(528.0, below);

            SolidClosing3 upper = AssertFilled(new GeoSolid3(ClosingTestBodies.BoxOpenDownwardsFaces()), Filling(FillStrategy.MinArea, 1.9), out GeoSolid3 above);
            Assert.InRange(Assert.Single(upper.Repairs).Size, 49.37, 49.38);
            AssertVolume(333.0, above);
        }

        [Fact]
        public void TwoBoxesOpenTowardsEachOther_WhoseFillsWouldCrossEachOther_AreRefusedAsStillOpen_AtTheCrossing()
        {
            // Together, the lower fill rises to 16 across the middle where the upper dips to 14.5: each crosses no face given,
            // but the second taken would cross the first, and the two closed would read valid, 861. A face added is of the
            // body the next is set against, so the second hole is refused where the two cross; and an unambiguous fill sees
            // two ways across each rim, and takes neither.
            var body = new GeoSolid3(ClosingTestBodies.BoxOpenUpwardsFaces().Concat(ClosingTestBodies.BoxOpenDownwardsFaces()));

            GeoPoint3 at = AssertRefused(body, Filling(FillStrategy.MinArea, 1.9), ClosingFailure.StillOpen);
            Assert.InRange(at.X, -1E-6, 6.0 + 1E-6);
            Assert.InRange(at.Y, -1E-6, 6.0 + 1E-6);
            Assert.InRange(at.Z, 14.5 - 1E-6, 16.0 + 1E-6);

            AssertRefused(body, Filling(FillStrategy.WhenUnambiguous, 1.9), ClosingFailure.HoleAmbiguous);
        }

        // Welds within five thousandths, and fills a hole of any size out of flat by up to as much as given, as the strategy
        // says.
        private static SolidClosingOptions Filling(FillStrategy fill, double maxOffFlat = 0.0)
            => new SolidClosingOptions(Fine, 0.005, double.PositiveInfinity, maxOffFlat, fill);

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
            Assert.False(body.TryClose(out GeoSolid3 closed, options, out SolidClosing3 report), report.ToString());
            Assert.Equal(failure, report.Failure);
            Assert.Null(closed);
            Assert.Empty(report.Repairs);
            Assert.Equal(0.0, report.AddedArea);
            Assert.Equal(0.0, report.VolumeChange);
            Assert.True(report.FailureLocation.HasValue);
            return report.FailureLocation.Value;
        }

        // Where the post or the ridge, both from (13, 8) to (17, 12) seen from above, passes through the top's plane: within
        // their plan, and within as much as given of the height given.
        private static void AssertOnThePost(GeoPoint3 at, double z, double within)
        {
            Assert.InRange(at.X, 13.0 - 1E-6, 17.0 + 1E-6);
            Assert.InRange(at.Y, 8.0 - 1E-6, 12.0 + 1E-6);
            Assert.InRange(at.Z, z - within, z + within);
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
