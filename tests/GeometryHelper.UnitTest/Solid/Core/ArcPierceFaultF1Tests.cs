using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A segment or a ray piercing the plane of an arc a hair beside its rim touches the arc there, and is given the point:
    /// every arc, circle, curved edge and curved chain read such a touch as a collision with no point to name.
    /// </summary>
    public class ArcPierceFaultF1Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly GeoPoint3 OnTheRim = new GeoPoint3(0, 50, 0);

        private static GeoLine3 Pin(double gap) => new GeoLine3(new GeoPoint3(0, 50 + gap, 1), new GeoPoint3(0, 50 + gap, -1));

        private static void AssertTheTouch(GeoPoint3[] places)
        {
            GeoPoint3 place = Assert.Single(places);

            Assert.Equal(0.0, place.DistanceTo(OnTheRim), 9);
        }

        [Theory]
        [InlineData(1E-7)]
        [InlineData(1E-5)]
        [InlineData(0.0006)]
        [InlineData(-0.0006)]
        public void ASegmentPiercingTheCirclesPlaneAHairBesideItsRim_TouchesItAtTheRim(double gap)
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 50);
            GeoLine3 pin = Pin(gap);

            Assert.True(circle.CollidesWith(pin, Thousandth));
            AssertTheTouch(circle.GetIntersections(pin, Thousandth));
            AssertTheTouch(pin.GetIntersections(circle, Thousandth));
        }

        [Theory]
        [InlineData(1E-7)]
        [InlineData(0.0006)]
        [InlineData(-0.0006)]
        public void ASegmentAndARayPiercingTheArcsPlaneAHairBesideItsRim_TouchItAtTheRim(double gap)
        {
            GeoArc3 arc = GeoArc3.FromThreePoints(new GeoPoint3(50, 0, 0), OnTheRim, new GeoPoint3(-50, 0, 0), Thousandth);
            GeoLine3 pin = Pin(gap);
            var ray = new GeoRay3(pin.StartPoint, new GeoVector3(0, 0, -1));

            Assert.True(arc.CollidesWith(pin, Thousandth));
            AssertTheTouch(arc.GetIntersections(pin, Thousandth));
            AssertTheTouch(pin.GetIntersections(arc, Thousandth));

            Assert.True(arc.CollidesWith(ray, Thousandth));
            AssertTheTouch(arc.GetIntersections(ray, Thousandth));
        }

        [Theory]
        [InlineData(-1E-6)]
        [InlineData(-1E-3)]
        [InlineData(-0.2)]
        public void ASegmentPiercingTheChainsPlaneBesideACurvedEdge_TouchesTheEdgeAndTheChain(double bulge)
        {
            // The segment pierces the plane 0.0005 outside the edge, square to it, where the edge bows furthest.
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), bulge, new GeoVector3(0, 0, 1));
            var chain = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0) },
                new[] { bulge, 0.0 },
                new[] { new GeoVector3(0, 0, 1), new GeoVector3(0, 0, 1) });
            GeoPoint3 middle = edge.GetPointAtParameter(0.5);
            var pin = new GeoLine3(new GeoPoint3(middle.X, middle.Y - 0.0005, 1), new GeoPoint3(middle.X, middle.Y - 0.0005, -1));

            Assert.True(edge.CollidesWith(pin, Thousandth));
            Assert.Equal(0.0, Assert.Single(edge.GetIntersections(pin, Thousandth)).DistanceTo(middle), 6);
            Assert.True(chain.CollidesWith(pin, Thousandth));
            Assert.Equal(0.0, Assert.Single(chain.GetIntersections(pin, Thousandth)).DistanceTo(middle), 6);
        }

        [Fact]
        public void ASegmentTouchingAChainBesideTheCornerOfItsArcAndItsLeg_IsGivenOnePoint()
        {
            // The arc gives the place where the segment passes within the tolerance of it, and the leg the point of a touch
            // 0.001 away, each within the tolerance of the other edge: one touch of the corner, named once, the arc's.
            var normal = new GeoVector3(-0.09088544844354864, -0.04556165794521301, -0.9948185616415179);
            var chain = new GeoPolylineArc3(
                new[]
                {
                    new GeoPoint3(-59.22711806335818, -21.436646730376708, -93.98837033379282),
                    new GeoPoint3(-1091.2417064030255, 208.13818430667007, -10.219025902295428),
                    new GeoPoint3(-934.0436633141649, 932.1346622663968, -57.73874089928437),
                },
                new[] { -0.2550969892517928, 0.0 },
                new[] { normal, normal });
            var segment = new GeoLine3(
                new GeoPoint3(-1091.1390035842012, 208.664687582695, -11.685215707054827),
                new GeoPoint3(-1091.2740328997459, 207.97157256167947, -9.753591227045133));

            GeoPoint3[] fromArc = chain.GetEdgeAt(0).GetIntersections(segment, Thousandth);
            GeoPoint3 place = Assert.Single(chain.GetIntersections(segment, Thousandth));

            Assert.Equal(Assert.Single(fromArc), place);
            Assert.Single(chain.GetEdgeAt(1).GetIntersections(segment, Thousandth));
        }

        [Theory]
        [InlineData(0.01)]
        [InlineData(0.05)]
        public void ASegmentCrossingBothLegsOfAHairpinFarFromItsTip_IsGivenBothCrossings(double slope)
        {
            // The legs run within 0.0009 of each other all the way to the tip at (100, 0, 0), so each crossing lies within the
            // tolerance of the other leg; the two are 0.043 apart for the shallower segment, and both are kept.
            var up = new GeoVector3(0, 0, 1);
            var hairpin = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 0.0009, 0) },
                new[] { 0.0, 0.0 },
                new[] { up, up });
            var segment = new GeoLine3(new GeoPoint3(50 - 0.01 / slope, -0.01, 0), new GeoPoint3(50 + 0.01 / slope, 0.0109, 0));

            GeoPoint3[] places = hairpin.GetIntersections(segment, Thousandth);

            Assert.Equal(2, places.Length);
            Assert.Equal(Assert.Single(hairpin.GetEdgeAt(0).GetIntersections(segment, Thousandth)), places[0]);
            Assert.Equal(Assert.Single(hairpin.GetEdgeAt(1).GetIntersections(segment, Thousandth)), places[1]);
        }

        [Fact]
        public void ASegmentTouchingAnArcOfRadius3E13AMillionFromTheOrigin_IsGivenThePoint()
        {
            // The distance from the centre less the radius rounds by a few thousandths at that size, and read without that
            // rounding the segment was taken to stand clear of the circle and given nothing, though it collides.
            // The arc is built from its edge, its ends and a bow of 4.9E-8 off the chord: through its three points it is
            // refused, since an arc so large misses them by up to 0.028 (G1k).
            var start = new GeoPoint3(1113557.2177864336, -792087.8076833521, 542349.5851905062);
            var middle = new GeoPoint3(1115188.3471700898, -792820.278604614, 542222.7485826516);
            var end = new GeoPoint3(1116819.47655371, -793552.7495259601, 542095.9119748235);
            GeoVector3 normal = start.GetVectorTo(middle).CrossProduct(start.GetVectorTo(end)).Normalize();
            GeoArc3 arc = new GeoEdge3(start, end, 2 * 4.9E-8 / start.DistanceTo(end), normal).ToArc();
            var segment = new GeoLine3(
                new GeoPoint3(1116327.3417597327, -793332.7394228477, 542133.0571931278),
                new GeoPoint3(1116329.091936263, -793330.8561671121, 542135.9621686899));

            Assert.True(arc.CollidesWith(segment, Thousandth));
            Assert.Single(arc.GetIntersections(segment, Thousandth));
        }

        [Fact]
        public void ASegmentPiercingThePlaneFurtherThanTheToleranceFromTheRim_MeetsNothing()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 50);
            GeoLine3 pin = Pin(0.0015);

            Assert.False(circle.CollidesWith(pin, Thousandth));
            Assert.Empty(circle.GetIntersections(pin, Thousandth));
        }
    }
}
