using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A segment ending within the planar tolerance of a plane touches it there and is given the point, as a triangle and a
    /// polygon in that plane give it: the plane collided with the segment and named no place.
    /// </summary>
    public class PlaneTouchFaultF1Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly GeoPlane3 Floor = new GeoPlane3(GeoPoint3.Origin, new GeoVector3(0, 0, 1));

        [Theory]
        [InlineData(0.5, 0.0005)]
        [InlineData(0.003, 0.0005)]
        [InlineData(0.0019, 0.0009)]
        [InlineData(-0.5, -0.0005)]
        public void ASegmentEndingAHairOverThePlane_TouchesItAtThatEnd(double startHeight, double endHeight)
        {
            var segment = new GeoLine3(new GeoPoint3(-50, 0, startHeight), new GeoPoint3(50, 0, endHeight));

            Assert.True(segment.CollidesWith(Floor, Thousandth));
            Assert.Equal(segment.EndPoint, Assert.Single(segment.GetIntersections(Floor, Thousandth)));
        }

        [Theory]
        [InlineData(0.5, 0.0005)]
        [InlineData(0.0019, 0.0009)]
        public void APlaneAskedAboutASegmentEndingAHairOverIt_TouchesItAtThatEndAsTheSegmentAsksIt(double startHeight, double endHeight)
        {
            // Asked from the plane, the segment and its edge were given no point where asked from the segment they were.
            var segment = new GeoLine3(new GeoPoint3(-50, 0, startHeight), new GeoPoint3(50, 0, endHeight));
            var edge = new GeoEdge3(segment.StartPoint, segment.EndPoint);

            Assert.Equal(segment.EndPoint, Assert.Single(Floor.GetIntersections(segment, Thousandth)));
            Assert.Equal(segment.EndPoint, Assert.Single(Floor.GetIntersections(edge, Thousandth)));
            Assert.Equal(segment.EndPoint, Assert.Single(edge.GetIntersections(Floor, Thousandth)));
        }

        [Fact]
        public void AChainWhoseCornerHoversOverThePlaneBesideACrossing_IsGivenTheCrossing()
        {
            // The first segment ends 0.0005 over the plane and touches it there; the second crosses it 0.0007 from that corner.
            // The two are one place, and it is the crossing, as the chain was given before segments touched planes.
            var corners = new[] { new GeoPoint3(-50, 0, 0.5), new GeoPoint3(0, 0, 0.0005), new GeoPoint3(0.5, 0, -0.5) };
            GeoPoint3 crossing = Assert.Single(new GeoLine3(corners[1], corners[2]).GetIntersections(Floor, Thousandth));
            var up = new GeoVector3(0, 1, 0);

            Assert.Equal(crossing, Assert.Single(new GeoPolyline3(corners).GetIntersections(Floor, Thousandth)));
            Assert.Equal(crossing, Assert.Single(Floor.GetIntersections(new GeoPolyline3(corners), Thousandth)));
            Assert.Equal(crossing, Assert.Single(new GeoPolylineArc3(corners, new[] { 0.0, 0.0 }, new[] { up, up }).GetIntersections(Floor, Thousandth)));
        }

        [Fact]
        public void ASegmentLyingInThePlaneWithinItsTolerance_NamesNoPlace()
        {
            var segment = new GeoLine3(new GeoPoint3(-50, 0, 0.0004), new GeoPoint3(50, 0, -0.0003));

            Assert.True(segment.CollidesWith(Floor, Thousandth));
            Assert.Empty(segment.GetIntersections(Floor, Thousandth));
        }

        [Fact]
        public void ASegmentCrossingThePlane_IsGivenItsCrossingAsBefore()
        {
            var segment = new GeoLine3(new GeoPoint3(-50, 0, 1), new GeoPoint3(50, 0, -1));

            Assert.Equal(new GeoPoint3(0, 0, 0), Assert.Single(segment.GetIntersections(Floor, Thousandth)));
        }
    }
}
