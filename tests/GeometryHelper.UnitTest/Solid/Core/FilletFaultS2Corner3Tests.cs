using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Rounding a bar in space leaves a bend alone where its fillet has no room, rather than throwing.
    /// </summary>
    /// <remarks>
    /// <c>Corner3</c> built each bend's arc from its two touching points with a constructor that throws when they lie
    /// within the tolerance of each other: the bar from the origin to (10, 0, 0) and on to (20, 0.7, 0), a bend of 4.0
    /// degrees, asked for a radius of 1E-4 at 1E-3, threw "Two points in the same place have no chord to bulge". Its
    /// touching points lie 1E-4 tan(2.0 deg) = 3.5E-6 either side of the bend, a chord far inside the tolerance: there is
    /// nothing to round, and the bend is left as it is. Three sides of the square of side 10 asked for 5.02 at both bends
    /// at a tolerance of 0.1 threw "The edges of a chain must run end to end", as the plane's fillet did.
    /// </remarks>
    public class FilletFaultS2Corner3Tests
    {
        [Fact]
        public void ABendWhoseFilletChordIsWithinTheTolerance_IsLeftAlone()
        {
            var bar = new GeoPolyline3(P(0, 0, 0), P(10, 0, 0), P(20, 0.7, 0));

            GeoPolylineArc3 bent = Corner3.Fillet(bar, new[] { 0.0, 1E-4, 0.0 }, Thousandth);

            Assert.Equal(3, bent.VertexCount);
            Assert.DoesNotContain(bent.GetEdges(), e => e.IsArc);
            Assert.Equal(0.0, bent.Vertices[1].DistanceTo(P(10, 0, 0)), 1E-12);
        }

        [Fact]
        public void ABendOfATenthOfAMicroradianInATiltedPlane_IsLeftAloneAtATenThousandth()
        {
            // The bar runs 10 along (1, 1, 0) / sqrt 2 and 10 more, ending 1E-6 up z: a bend of 1E-7 radians, far straighter
            // than the angle tolerance of one degree. At (1E-4, 1E-6) Corner3 took the legs as not parallel and the frame
            // it then built took them as parallel: "The X and Y directions must not be parallel".
            var along = new GeoVector3(1, 1, 0).Normalize();
            var bar = new GeoPolyline3(P(0, 0, 0), P(0, 0, 0) + along * 10, P(0, 0, 0) + along * 20 + new GeoVector3(0, 0, 1E-6));

            GeoPolylineArc3 bent = Corner3.Fillet(bar, new[] { 0.0, 200.0, 0.0 }, new Tolerance(1E-4, 1E-6));

            Assert.Equal(3, bent.VertexCount);
            Assert.DoesNotContain(bent.GetEdges(), e => e.IsArc);
        }

        [Fact]
        public void ThreeSidesOfASquareBentTwoHundredthsPastHalfASideAtATenth_DoNotThrow()
        {
            var bar = new GeoPolyline3(P(0, 0, 0), P(10, 0, 0), P(10, 10, 0), P(0, 10, 0));

            GeoPolylineArc3 bent = Corner3.Fillet(bar, new[] { 0.0, 5.02, 5.02, 0.0 }, Tenth);

            Assert.Equal(0.0, bent.StartPoint.DistanceTo(P(0, 0, 0)));
            Assert.Equal(0.0, bent.EndPoint.DistanceTo(P(0, 10, 0)));
            Assert.Contains(bent.GetEdges(), e => e.IsArc);
        }

        [Fact]
        public void ThreeSidesOfASquareWhereOneBendAsksMore_GiveWayAtAThousandth()
        {
            // Guard: 5.04 and 5 of the side of 10, 0.04 too much at 1E-3: the bend asking 5.04 stays square, the other is
            // rounded between (10, 5, 0) and (5, 10, 0).
            var bar = new GeoPolyline3(P(0, 0, 0), P(10, 0, 0), P(10, 10, 0), P(0, 10, 0));

            GeoPolylineArc3 bent = Corner3.Fillet(bar, new[] { 0.0, 5.04, 5.0, 0.0 }, Thousandth);

            Assert.Equal(5, bent.VertexCount);
            Assert.Equal(0.0, bent.Vertices[1].DistanceTo(P(10, 0, 0)), 1E-12);
            Assert.Equal(0.0, bent.Vertices[2].DistanceTo(P(10, 5, 0)), 1E-9);
            Assert.Equal(0.0, bent.Vertices[3].DistanceTo(P(5, 10, 0)), 1E-9);
            Assert.Single(bent.GetEdges(), e => e.IsArc);
        }
    }
}
