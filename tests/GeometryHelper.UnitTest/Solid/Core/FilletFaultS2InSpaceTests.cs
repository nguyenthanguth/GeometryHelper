using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// In space too, where the bends or cuts at a leg take all of it, give or take the tolerance, the pieces either side
    /// meet at one point, the bar's two ends stay where they were, and nothing throws.
    /// </summary>
    /// <remarks>
    /// The legs left within the tolerance were taken out and the chain, which reads the global tolerance, refused the
    /// gap: a hairpin whose turning leg is 0.1 long, asked for bends of 2 and 0.002 at a tolerance of 0.1, threw "The
    /// edges of a chain must run end to end", and a bar cut back along all of its first leg and a hair more moved its
    /// start or left a stub.
    /// </remarks>
    public class FilletFaultS2InSpaceTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        [Fact]
        public void AHairpinBentAtATenthWhoseTurningLegIsATenthLong_DoesNotThrowAndKeepsItsEnds()
        {
            var bar = new GeoPolyline3(P(6.320937493034144, 7.6613264287176186, 0), P(6.9788313128993886, 17.639661746884826, 0),
                P(6.8790479597177168, 17.64624068508348, 0), P(6.2211541398524721, 7.6679053669162709, 0));

            GeoPolylineArc3 bent = Corner3.Fillet(bar, new[] { 0.0001, 2.0, 0.002, 0.0 }, Tenth);

            // No bend fits beside a leg the tolerance reads as no length, and that leg goes, as it always has.
            Assert.Equal(0.0, bent.StartPoint.DistanceTo(bar.StartPoint));
            Assert.Equal(0.0, bent.EndPoint.DistanceTo(bar.EndPoint));
            Assert.Equal(3, bent.VertexCount);
        }

        [Fact]
        public void ABarCutBackAlongAllOfItsFirstLegAndAHairMore_KeepsItsStart()
        {
            // 10.05 back along the first leg, 10 long, at 0.1: the cut starts at the bar's own start.
            var bar = new GeoPolylineArc3(new GeoPolyline3(P(0, 0, 0), P(10, 0, 0), P(10, 0, 10)));

            GeoPolylineArc3 cut = Corner3.Chamfer(bar, 10.05, 5.0, Tenth);

            Assert.Equal(3, cut.VertexCount);
            Assert.True(cut.Vertices[0].IsEqualTo(P(0, 0, 0), Tight));
            Assert.True(cut.Vertices[1].IsEqualTo(P(10, 0, 5), Tight));
            Assert.True(cut.Vertices[2].IsEqualTo(P(10, 0, 10), Tight));
        }

        [Fact]
        public void AStraightBarCutBackAlongAllOfItsFirstLegAndAHairMore_LeavesNoStub()
        {
            var bar = new GeoPolyline3(P(0, 0, 0), P(10, 0, 0), P(10, 0, 10));

            GeoPolyline3 cut = Corner3.Chamfer(bar, 10.05, 5.0, Tenth);

            Assert.Equal(3, cut.VertexCount);
            Assert.True(cut.Vertices[0].IsEqualTo(P(0, 0, 0), Tight));
            Assert.True(cut.Vertices[1].IsEqualTo(P(10, 0, 5), Tight));
        }

        [Fact]
        public void ABarBentAtTwentyAtTheDefaultTolerance_IsTheSameAsBefore()
        {
            // Guard: nothing vanishes, so a right-angled bend is the quarter turn worked out by hand.
            var bar = new GeoPolyline3(P(0, 0, 0), P(300, 0, 0), P(300, 300, 0));

            GeoPolylineArc3 bent = Corner3.Fillet(bar, 20.0, Thousandth);

            Assert.Equal(4, bent.VertexCount);
            Assert.True(bent.Vertices[1].IsEqualTo(P(280, 0, 0), Tight));
            Assert.True(bent.Vertices[2].IsEqualTo(P(300, 20, 0), Tight));
        }
    }
}
