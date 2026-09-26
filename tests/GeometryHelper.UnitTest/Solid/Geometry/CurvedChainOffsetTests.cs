using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Offsetting a curved chain in space within its own plane. <see cref="GeoPolyline3"/> had this and
    /// <see cref="GeoPolylineArc3"/> did not, so a bar set out at one cover and wanted at another had to be
    /// straightened first, which throws the bends away.
    /// </summary>
    /// <remarks>
    /// The two things worth checking: an arc comes back as an arc with its radius moved by the distance, and a
    /// chain holding no arc gives exactly what the straight version gives, because it is handed to it.
    /// </remarks>
    public class CurvedChainOffsetTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>A bar in z = 0: along x to (400, 0), then up to (400, 200), with a fifty radius at the bend.</summary>
        private static GeoPolylineArc3 Bar() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0)).Fillet(50.0);

        [Fact]
        public void ABarMovedToAnotherCoverKeepsItsBendsAndMovesTheirRadii()
        {
            GeoPolylineArc3 bar = Bar();

            Assert.Single(bar.GetEdges(), edge => edge.IsArc);

            double radius = bar.GetEdges().Single(edge => edge.IsArc).ToArc().Radius;

            Assert.Equal(50.0, radius, 6);

            // Seen from +z, the bar turns left, so its inside is on the left: thirty to the left tightens the bend.
            GeoPolylineArc3[] inwards = bar.OffsetInPlane(30.0, GeoVector3.ZAxis);

            Assert.Single(inwards);
            Assert.Single(inwards[0].GetEdges(), edge => edge.IsArc);
            Assert.Equal(20.0, inwards[0].GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);

            // Thirty the other way opens it out, and the whole chain stays in the plane it started in.
            GeoPolylineArc3[] outwards = bar.OffsetInPlane(-30.0, GeoVector3.ZAxis);

            Assert.Single(outwards);
            Assert.Equal(80.0, outwards[0].GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);
            Assert.All(outwards[0].Vertices, vertex => Assert.Equal(0.0, vertex.Z, 6));
        }

        [Fact]
        public void TheNormalSaysWhichSideTheChainIsSeenFromAndSoWhichWayIsLeft()
        {
            GeoPolylineArc3 bar = Bar();

            GeoPolylineArc3[] fromAbove = bar.OffsetInPlane(30.0, GeoVector3.ZAxis);
            GeoPolylineArc3[] fromBelow = bar.OffsetInPlane(30.0, GeoVector3.ZAxis.Negate());

            // Seen from the other side, left is the other way, so the same distance goes the opposite way.
            Assert.Equal(20.0, fromAbove[0].GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);
            Assert.Equal(80.0, fromBelow[0].GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);

            // A normal lying in the chain's own plane gives no side at all.
            Assert.Throws<ArgumentException>(() => bar.OffsetInPlane(30.0, GeoVector3.XAxis));
            Assert.Throws<ArgumentException>(() => bar.OffsetInPlane(30.0, new GeoVector3(0, 0, 0)));
        }

        [Fact]
        public void AChainHoldingNoArcGivesExactlyWhatTheStraightVersionGives()
        {
            var setOut = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0));
            var asChain = new GeoPolylineArc3(setOut);

            foreach (double distance in new[] { 30.0, -30.0, 5.0 })
            {
                GeoPolyline3[] straight = setOut.OffsetInPlane(distance, GeoVector3.ZAxis);
                GeoPolylineArc3[] curved = asChain.OffsetInPlane(distance, GeoVector3.ZAxis);

                Assert.Equal(straight.Length, curved.Length);

                for (int i = 0; i < straight.Length; i++)
                {
                    Assert.Equal(straight[i].VertexCount, curved[i].VertexCount);
                    Assert.Equal(straight[i].Length, curved[i].Length, 6);

                    for (int v = 0; v < straight[i].VertexCount; v++)
                    {
                        Assert.True(straight[i][v].IsEqualTo(curved[i][v], Loose));
                    }
                }
            }
        }

        [Fact]
        public void AChainRunningStraightAlongOneLineStillHasAPlanePickedForIt()
        {
            // Two points lie in every plane through them, so the normal picks the plane as well as the side.
            var along = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0)));

            GeoPolylineArc3[] moved = along.OffsetInPlane(50.0, GeoVector3.ZAxis);

            Assert.Single(moved);
            Assert.Equal(along.Length, moved[0].Length, 6);
            Assert.Equal(50.0, moved[0].StartPoint.Y, 6);
            Assert.Equal(0.0, moved[0].StartPoint.Z, 6);

            // A normal along the chain gives no side to go to.
            Assert.Throws<ArgumentException>(() => along.OffsetInPlane(50.0, GeoVector3.XAxis));
        }

        [Fact]
        public void AChainInNoOnePlaneIsRefusedRatherThanAnsweredApproximately()
        {
            // A bar bent out of plane: the second bend leaves z = 0.
            GeoPolylineArc3 crooked = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 400, 0), new GeoPoint3(400, 400, 400))
                .Fillet(50.0);

            Assert.False(crooked.IsPlanar(Loose));
            Assert.Throws<ArgumentException>(() => crooked.OffsetInPlane(30.0, GeoVector3.ZAxis));
        }

        [Fact]
        public void ATiltedBarIsOffsetInItsOwnPlaneAndStaysInIt()
        {
            GeoPolylineArc3 bar = Bar();

            // Stand the whole bar up on edge and turn it, so no axis plane is its plane.
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, GeoVector3.XAxis, Math.PI / 4.0);
            GeoPolylineArc3 tilted = bar.TransformBy(turn);

            Assert.True(tilted.TryGetPlane(out GeoPlane3 plane, Loose));

            GeoPolylineArc3[] moved = tilted.OffsetInPlane(30.0, plane.Normal);

            Assert.Single(moved);
            Assert.All(moved[0].Vertices, vertex => Assert.True(plane.IsPointOn(vertex, Loose)));
            Assert.Equal(20.0, moved[0].GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);

            // And the answer is the offset bar turned the same way, not something re-fitted.
            GeoPolylineArc3[] flatFirst = bar.OffsetInPlane(30.0, GeoVector3.ZAxis);

            Assert.Equal(flatFirst[0].Length, moved[0].Length, 6);
        }

        [Fact]
        public void ATurnTighterThanTheDistanceLosesTheLoopItWouldHaveMade()
        {
            // A bend of fifty offset by eighty to the inside: the parallel curve would cross itself there.
            GeoPolylineArc3 bar = Bar();

            GeoPolylineArc3[] far = bar.OffsetInPlane(80.0, GeoVector3.ZAxis);

            Assert.NotEmpty(far);
            Assert.All(far, piece => Assert.True(piece.Length > 0.0));

            // Nothing loops back on itself: every piece is shorter than the bar it came from.
            Assert.All(far, piece => Assert.True(piece.Length < bar.Length));
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoPolylineArc3 bar = Bar();

            Assert.Equal(
                bar.OffsetInPlane(30.0, GeoVector3.ZAxis).Length,
                bar.OffsetInPlane(30.0, GeoVector3.ZAxis, global).Length);
            Assert.Equal(
                bar.OffsetInPlane(30.0, GeoVector3.ZAxis)[0].Length,
                bar.OffsetInPlane(30.0, GeoVector3.ZAxis, OffsetJoin.Round, global)[0].Length, 6);
            Assert.Equal(
                bar.OffsetInPlane(30.0, GeoVector3.ZAxis, OffsetOptions.Default)[0].Length,
                bar.OffsetInPlane(30.0, GeoVector3.ZAxis, OffsetOptions.Default, global)[0].Length, 6);

            // Nought moves nothing, and the chain comes back as it was.
            Assert.Equal(bar.Length, bar.OffsetInPlane(0.0, GeoVector3.ZAxis)[0].Length, 9);

            Assert.Throws<ArgumentNullException>(() => Offset3.OffsetInPlane((GeoPolylineArc3)null, 10.0, GeoVector3.ZAxis));
            Assert.Throws<ArgumentNullException>(() => bar.OffsetInPlane(10.0, GeoVector3.ZAxis, (OffsetOptions)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => bar.OffsetInPlane(double.NaN, GeoVector3.ZAxis));
        }
    }
}
