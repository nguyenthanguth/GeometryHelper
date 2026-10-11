using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.MeetingInSpaceOracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A ray starting within the planar tolerance of a plane and running at less than the angle tolerance to it, read as
    /// lying in the plane though it leaves the tolerance a few hundredths along.
    /// </summary>
    /// <remarks>
    /// Each ray starts at (5, 5, 0.0004), 0.4 of the planar tolerance over the square 10 by 10 in z = 0, and runs along x
    /// at a slope of a hundredth, a sine of 0.0099995 against the one degree's 0.0175. An endless ray lies in a plane only
    /// where it is level with it: one dipping crosses where it reaches height nought, one rising touches at its origin.
    /// </remarks>
    public class OldFaultF4Tests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        private static readonly GeoPoint3 Start = Pt(5, 5, 0.0004);

        private static GeoPlane3 Floor() => new GeoPlane3(Pt(0, 0, 0), new GeoVector3(0, 0, 1));

        [Fact]
        public void ARayDippingAHundredthFromInsideThePlanarTolerance_CrossesWhereItReachesHeightNought()
        {
            // Height nought is reached 0.0004 / 0.01 = 0.04 along x: at (5.04, 5, 0), inside the square.
            var ray = new GeoRay3(Start, new GeoVector3(1, 0, -0.01));
            GeoPoint3 expected = Pt(5.04, 5, 0);

            GeoPoint3 crossing = Assert.Single(ray.GetIntersections(Square(0, 0, 10, 10), Within));
            Assert.Equal(0.0, crossing.DistanceTo(expected), 9);
            Assert.True(ray.CollidesWith(Square(0, 0, 10, 10), Within));

            Assert.True(Intersection3.TryIntersectWith(ray, Floor(), out GeoPoint3 onPlane, Within));
            Assert.Equal(0.0, onPlane.DistanceTo(expected), 9);
        }

        [Fact]
        public void ARayRisingAHundredthFromInsideThePlanarTolerance_TouchesAtItsOrigin()
        {
            // Its origin is the one point within the planar tolerance it never comes back to.
            var ray = new GeoRay3(Start, new GeoVector3(1, 0, 0.01));

            GeoPoint3 touch = Assert.Single(ray.GetIntersections(Square(0, 0, 10, 10), Within));
            Assert.Equal(0.0, touch.DistanceTo(Start), 9);
            Assert.True(ray.CollidesWith(Square(0, 0, 10, 10), Within));

            Assert.True(Intersection3.TryIntersectWith(ray, Floor(), out GeoPoint3 onPlane, Within));
            Assert.Equal(0.0, onPlane.DistanceTo(Start), 9);
        }

        [Fact]
        public void ARayRisingSteeplyFromInsideThePlanarTolerance_TouchesAtItsOrigin()
        {
            // From 0.0009 over the floor at 45 degrees upwards: its foot lies 0.00127 behind it, past the slack an origin
            // standing off the plane is allowed, but the origin itself is within the planar tolerance.
            GeoPoint3 origin = Pt(5, 5, 0.0009);
            var ray = new GeoRay3(origin, new GeoVector3(1, 0, 1));

            Assert.True(Intersection3.TryIntersectWith(ray, Floor(), out GeoPoint3 onPlane, Within));
            Assert.Equal(0.0, onPlane.DistanceTo(origin), 9);
            Assert.True(ray.CollidesWith(Square(0, 0, 10, 10), Within));
        }

        [Fact]
        public void ARayLevelWithThePlaneFromInsideThePlanarTolerance_NamesNoPlace()
        {
            // Level with the floor: it lies in the plane as drawn and crosses it nowhere.
            var ray = new GeoRay3(Start, new GeoVector3(1, 0, 0));

            Assert.False(Intersection3.TryIntersectWith(ray, Floor(), out _, Within));
            Assert.Empty(ray.GetIntersections(Floor(), Within));
            Assert.True(ray.CollidesWith(Floor(), Within));
        }

        [Fact]
        public void ARayDippingFromInsideThePlanarToleranceIntoATriangleAndAFace_CrossesThem()
        {
            var ray = new GeoRay3(Start, new GeoVector3(1, 0, -0.01));
            GeoPoint3 expected = Pt(5.04, 5, 0);
            var triangle = new GeoTriangle3(Pt(0, 0, 0), Pt(20, 0, 0), Pt(0, 20, 0));
            GeoFace3 face = Holed(Square(0, 0, 10, 10), Square(7, 7, 9, 9));

            Assert.True(Intersection3.TryIntersectWith(ray, triangle, out GeoPoint3 onTriangle, Within));
            Assert.Equal(0.0, onTriangle.DistanceTo(expected), 9);
            Assert.True(Intersection3.TryIntersectWith(ray, face, out GeoPoint3 onFace, Within));
            Assert.Equal(0.0, onFace.DistanceTo(expected), 9);
        }
    }
}
