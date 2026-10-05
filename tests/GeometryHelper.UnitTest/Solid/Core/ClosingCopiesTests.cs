using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid whose copies of a corner the welding is easily kept apart: two in one face's ring, each shared by
    /// two faces, or a hair beyond the tolerance; faces lying across every axis, each on copies of its own, the right way
    /// round or inside out; a flap beside a gap; bodies whose faces are all finer than the widest gap, some wound the wrong
    /// way; and caps of many corners that the welding is not to break up; see
    /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    public class ClosingCopiesTests
    {
        private const double Volume = ClosingTestBodies.Volume;

        private const double Area = ClosingTestBodies.Area;

        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        [Fact]
        public void ACrackThatTheTopAndBottomSpan_IsWeldedShut()
        {
            // The front in two with a crack 0.003 wide between the halves from the bottom to the top, the top's and the
            // bottom's rings running through the corners of both sides of it: each spans the crack by an edge 0.003 long, its
            // two corners neighbours on the ring and copies of one point. Made one, the edge goes, and the crack closes in the
            // front's plane.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.BoxCrackedThroughTheFront(0.003), 0.005, out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            Assert.All(report.Repairs, repair => Assert.InRange(repair.Location.X, 15.0 - 1E-9, 15.003 + 1E-9));
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void ATopCornerDoubledByACopyBeyondIt_IsWeldedOntoTheCorner()
        {
            // The top's ring runs through the corner over (30, 20) and on through a copy of it 0.003 out along the top's
            // diagonal, the right and the back meeting at the corner alone: the copy stands off the back's edge beyond its
            // end, so that no edge takes it, and it is made one with the corner, which the top has as well.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.BoxWithATopCornerDoubled(0.003), 0.005, out GeoSolid3 closed);
            SolidRepair3 weld = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Weld, weld.Kind);
            Assert.InRange(weld.Location.DistanceTo(new GeoPoint3(30, 20, 10)), 0.0, 1E-9);
            Assert.InRange(weld.Size, 0.003 - 1E-9, 0.003 + 1E-9);
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void ATopCornerInTwoCopies_IsWeldedOntoTheCornerTheSidesKeep()
        {
            // The top's corner over (30, 20) in two copies beside each other on the top's ring, one 0.002 off the right's
            // plane and the other off the back's, the right and the back meeting at the corner itself: both are made one with
            // it, and the top lies flat on the box again.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.BoxWithATopCornerInTwo(0.002), 0.005, out GeoSolid3 closed);
            SolidRepair3 weld = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Weld, weld.Kind);
            Assert.InRange(weld.Location.DistanceTo(new GeoPoint3(30, 20, 10)), 0.0, 1E-9);
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void ACornerCutOffByATriangleLeftOpen_IsWeldedShut()
        {
            // The top corner over (30, 20) cut off 0.003 along each edge and the triangle left out: each corner of it is a
            // corner of the two faces meeting along its edge, so every two of them share a face, on whose ring they are
            // neighbours. The three, 0.0042 apart, are made one within a gap of 0.005.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.BoxWithItsCornerCutOffOpen(0.003), 0.005, out GeoSolid3 closed);
            SolidRepair3 weld = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Weld, weld.Kind);
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.003 * Area, Volume + 0.003 * Area);
        }

        [Fact]
        public void AChamferStripLeftOpen_IsWeldedShut()
        {
            // The edge between the top and the right chamfered 0.003 along each and the chamfer left out: a strip 0.0042 wide,
            // which the front and the back each span by a short edge from the top's corner to the right's. Welded at both
            // ends, the strip closes.
            SolidClosing3 report = AssertWelded(ClosingTestBodies.BoxWithAChamferMissing(0.003), 0.005, out GeoSolid3 closed);
            Assert.Equal(2, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.003 * Area, Volume + 0.003 * Area);
        }

        [Theory]
        [InlineData(0.005)]
        [InlineData(0.01)]
        public void AFlapBesideAGap_IsAFin_HoweverWideAGapIsAllowed(double gap)
        {
            // A flap 0.004 by 0.004 stands out of the top's front edge beside the top's corner over (30, 0), which is moved
            // out 0.003. The corner welds back, but the flap is a fin, a stretch of the edge three faces run, and welding its
            // corners together or onto the box's would hide it, not close it: the body is refused at the flap.
            GeoPoint3 at = AssertRefused(ClosingTestBodies.BoxWithAMovedCornerAndAFlapBesideIt(), gap, ClosingFailure.NonManifold);
            Assert.InRange(at.DistanceTo(new GeoPoint3(29.994, 0, 10)), 0.0, 0.006);
        }

        [Theory]
        [InlineData(1, 0.0008)]
        [InlineData(17, 0.0007)]
        [InlineData(21, 0.0006)]
        public void ABoxOfCopiesMovedJustBeyondTheTolerance_IsWeldedShut(int seed, double most)
        {
            // Every face on copies of its corners, each moved in its face's plane by up to 0.0006 to 0.0008: the copies' edges
            // lie within the tolerance of each other's lines but for one stretch by a corner, a hair over a thousandth long,
            // which one face runs alone. No other edge left open runs back alongside it, and it bounds no hole: it is a gap of
            // no width, and the copies there are welded.
            GeoSolid3 body = ClosingTestBodies.BoxOfMovedCopies(seed, most);
            SolidIssue3 open = Assert.Single(body.Validate(Fine).Issues);
            Assert.Equal(SolidIssueKind.OpenEdge, open.Kind);
            Assert.InRange(open.Size, Fine.EqualPoint, 2.0 * Fine.EqualPoint);

            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            Assert.InRange(closed.GetVolume(Fine), Volume - 2.0 * most * Area, Volume + 2.0 * most * Area);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void TrianglesOnCopiesOfTheirOwn_LyingAcrossEveryAxis_AreWelded_NoneTurned(int seed)
        {
            // The box turned out of every axis, each face two triangles on copies of corners of their own moved by up to
            // 0.002: few edges lie within the tolerance of another's line, and a triangle can be a shell by itself, which,
            // measured from the middle of its own box, seems to hold a volume one way or the other. One face holds none, and
            // none is turned: the copies are welded, and every triangle faces out as it was given.
            GeoSolid3 body = ClosingTestBodies.TurnedBoxOfTrianglesOnMovedCopies(seed, 0.002);
            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.004 * Area, Volume + 0.004 * Area);
        }

        [Fact]
        public void AQuadTurnedInAMeshFinerThanTheWidestGap_IsTurnedBack()
        {
            // A patch of 10 by 10 quads 0.004 a side in the top, the one at the middle wound the wrong way: every stretch of
            // edge it runs is shorter than the widest gap, 0.005, and all four run the same way as its neighbours'. It is
            // turned back, as any face wound against every face beside it is.
            GeoSolid3 body = ClosingTestBodies.BoxWithAFinePatchOneTurned(10, 0.004, false);
            Assert.True(body.TryClose(out GeoSolid3 closed, new SolidClosingOptions(Fine, 0.005), out SolidClosing3 report), report.ToString());
            Assert.True(closed.Validate(Fine).IsValid);
            SolidRepair3 flip = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Flip, flip.Kind);
            Assert.InRange(flip.Location.DistanceTo(new GeoPoint3(15.022, 10.022, 10)), 0.0, 1E-9);
            Assert.InRange(flip.Size, 1.6E-5 - 1E-12, 1.6E-5 + 1E-12);
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void ATriangleTurnedInAMeshFinerThanTheWidestGap_IsTurnedBack()
        {
            // The patch of triangles 0.3 a side, the first of the middle cell's two wound the wrong way, closed within a gap
            // of 0.5: no edge of it is longer than 0.43.
            GeoSolid3 body = ClosingTestBodies.BoxWithAFinePatchOneTurned(10, 0.3, true);
            Assert.True(body.TryClose(out GeoSolid3 closed, new SolidClosingOptions(Fine, 0.5), out SolidClosing3 report), report.ToString());
            Assert.True(closed.Validate(Fine).IsValid);
            SolidRepair3 flip = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Flip, flip.Kind);
            Assert.InRange(flip.Size, 0.045 - 1E-9, 0.045 + 1E-9);
            AssertVolume(Volume, closed);
        }

        [Fact]
        public void ABodyOfFacesAllFinerThanTheWidestGap_InsideOut_IsTurnedOut()
        {
            // A cube 3 wide, each face 10 by 10 quads 0.3 a side and every quad wound inwards, closed within a gap of 0.5: no
            // stretch of edge is as long as the gap, and the body is one shell all the same, wound inwards. Every face is
            // turned out.
            GeoSolid3 body = ClosingTestBodies.FineCubeInsideOut(3, 10);
            Assert.True(body.TryClose(out GeoSolid3 closed, new SolidClosingOptions(Fine, 0.5), out SolidClosing3 report), report.ToString());
            Assert.True(closed.Validate(Fine).IsValid);
            Assert.Equal(600, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Flip, repair.Kind));
            AssertVolume(27, closed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ATurnedTop_OrABoxInsideOut_IsTurned_WhereTheGapAllowedIsWiderThanEveryEdge(bool insideOut)
        {
            // The box with its top wound the wrong way, or with every face so, closed within a gap of 50: no edge of it is
            // as long as that, and every face still faces the wrong way. The top is turned, or all six.
            GeoSolid3 body = insideOut ? ClosingTestBodies.BoxInsideOut() : ClosingTestBodies.BoxWithFaceFlipped(ClosingTestBodies.Top);
            Assert.True(body.TryClose(out GeoSolid3 closed, new SolidClosingOptions(Fine, 50), out SolidClosing3 report), report.ToString());
            Assert.True(closed.Validate(Fine).IsValid);
            Assert.Equal(insideOut ? 6 : 1, report.Repairs.Count);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Flip, repair.Kind));
            AssertVolume(Volume, closed);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public void ASphereOfFacesOnCopiesOfTheirOwn_IsWelded_NoneTurned(int seed)
        {
            // A sphere of radius 1 000, six bands by eight round, every face on copies of corners of its own moved by up to
            // 0.002: few edges lie within the tolerance of another's line, and the faces fall into patches of one or two. Such
            // a patch, measured from the middle of its own box, which lies outside the sphere, seems wound inwards; but it is
            // open, and says nothing of which way it faces. The copies are welded, and no face is turned.
            GeoSolid3 whole = ClosingTestBodies.SphereOfMovedCopies(1000, 6, 8, 0.0, seed);
            GeoSolid3 body = ClosingTestBodies.SphereOfMovedCopies(1000, 6, 8, 0.002, seed);
            Assert.True(whole.Validate(Fine).IsValid);

            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.All(report.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            double volume = whole.GetVolume(Fine), area = whole.GetSurfaceArea(Fine);
            Assert.InRange(closed.GetVolume(Fine), volume - 0.004 * area, volume + 0.004 * area);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void ABoxOfCopiesWoundInwards_IsWelded_AndTurnedOut(int seed)
        {
            // Every face of the box on copies of its corners moved by up to 0.002, and every face wound inwards: open all
            // round before the welding, so that no shell of it says which way it faces, and closed after it, inside out. The
            // body welded is turned out, all six faces, as a closed shell wound inwards is.
            var body = new GeoSolid3(ClosingTestBodies.BoxOfMovedCopies(seed, 0.002).Faces.Select(face => face.Flip()));
            Assert.True(body.TryClose(out GeoSolid3 closed, new SolidClosingOptions(Fine, 0.005), out SolidClosing3 report), report.ToString());
            Assert.True(closed.Validate(Fine).IsValid);
            Assert.Equal(6, report.Repairs.Count(repair => repair.Kind == SolidRepairKind.Flip));
            Assert.All(report.Repairs, repair => Assert.Contains(repair.Kind, new[] { SolidRepairKind.Weld, SolidRepairKind.Flip }));
            Assert.InRange(closed.GetVolume(Fine), Volume - 0.004 * Area, Volume + 0.004 * Area);
        }

        [Fact]
        public void AManySidedPrism_WeldedOnCopiesNearItsCapsPlanes_KeepsEachCapOneFace()
        {
            // A prism of 1 000 sides, every face on copies of its own: the sides' copies moved along the rim by up to 0.002
            // and up or down by up to 0.0008, the caps' in their planes. Welded, a cap's corners stand within 0.0008 of its
            // plane, within the planar tolerance: each cap is still one face, and no sliver of a triangle lies along its rim.
            GeoSolid3 body = ClosingTestBodies.PrismOfCopiesMovedAlongTheRim(1000, 10, 10, 0.002, 8E-4, 1);
            AssertWelded(body, 0.005, out GeoSolid3 closed);
            AssertCapsWhole(body, closed, 10);
        }

        [Fact]
        public void AManySidedPrism_OnCopiesMovedEveryWay_IsWeldedWithItsCapsWhole()
        {
            // The prism of 1 000 sides on copies moved every way in their faces' planes by up to 0.002: a side's copy of a
            // corner of the top may stand 0.002 above or below the top. A corner goes to the copy that bends the faces round
            // it least, each weighed by its whole area, not by the corner of it: the sides, 0.063 wide, bend, and the caps
            // stay flat and whole.
            GeoSolid3 body = ClosingTestBodies.PrismOfMovedCopies(1000, 10, 10, 0.002, 1);
            AssertWelded(body, 0.005, out GeoSolid3 closed);
            AssertCapsWhole(body, closed, 10);
        }

        [Fact]
        public void AManySidedPrismWithOneCopyOfItsRimMoved_KeepsEachCapOneFace()
        {
            // The top's copy of one corner of the rim of a prism of 1 000 sides moved out 0.003 and up 0.00001: welded back,
            // the top's corners lie within a hair of its plane, and the top stays one face.
            GeoSolid3 body = ClosingTestBodies.PrismWithTopCornerMoved(ClosingTestBodies.RoundPlan(1000, 10), 0, 10, 0, new GeoVector3(0.003, 0, 1E-5));
            SolidClosing3 report = AssertWelded(body, 0.005, out GeoSolid3 closed);
            Assert.Equal(SolidRepairKind.Weld, Assert.Single(report.Repairs).Kind);
            AssertCapsWhole(body, closed, 10);
        }

        // Each cap of a prism from z = 0 to the height given one face lying in its plane within the planar tolerance, and no
        // sliver of a face or stretch of four faces that the body given did not have.
        private static void AssertCapsWhole(GeoSolid3 given, GeoSolid3 closed, double height)
        {
            Assert.Equal(1, FacesIn(closed, 0.0));
            Assert.Equal(1, FacesIn(closed, height));
            SolidValidation3 before = given.Validate(Fine), after = closed.Validate(Fine);
            int slivers = before.Issues.Count(issue => issue.Kind == SolidIssueKind.SliverFace);
            Assert.Equal(slivers, after.Issues.Count(issue => issue.Kind == SolidIssueKind.SliverFace));
            Assert.DoesNotContain(after.Issues, issue => issue.Kind == SolidIssueKind.NonManifoldEdge);
        }

        // How many faces lie in the plane z = at, facing up or down, within the planar tolerance.
        private static int FacesIn(GeoSolid3 body, double at)
            => body.Faces.Count(face => Math.Abs(face.Normal.Z) > 0.999 && face.Boundary.Vertices.All(p => Math.Abs(p.Z - at) <= Fine.EqualPlanar));

        // Closed within a gap by welds and corners put on edges alone: valid, no ring doubling back, no skin of no thickness,
        // nothing filled.
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
