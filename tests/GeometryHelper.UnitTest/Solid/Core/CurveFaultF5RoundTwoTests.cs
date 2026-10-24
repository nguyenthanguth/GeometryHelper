using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The second review of near touches in space: a huge rim passing a thin one far from its centre, places kept off one
    /// of the two rims, crossings of an ellipse and a circle closer than twice the tolerance, and the order of the points of
    /// two rims in parallel planes.
    /// </summary>
    public class CurveFaultF5RoundTwoTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static double Off(GeoEllipse3 ellipse, GeoPoint3 place) => ellipse.GetClosestPointOnBoundary(place).DistanceTo(place);

        [Fact]
        public void AHugeRimPassingAThinOneFarFromItsCentre_TouchesIt()
        {
            // The rims come within 0.00092 of each other near (45348.35, -81998.98, 6775.64), 93 000 from the huge one's
            // centre. Read along the thin one's foot as one wave about the huge one's centre, the test of which side of the
            // thin ellipse the foot lies on cancelled to nothing, the piece holding the touch was judged inside and proved
            // clear, and no place was given.
            var big = new GeoEllipse3(
                new GeoPoint3(27.55163692755235, -49.253088514904064, -30.21544715399642),
                new GeoVector3(-0.16156381872433273, -0.007394132480129507, 0.986834565306707),
                new GeoVector3(-0.27219940612489085, -0.9608475438625376, -0.05176370116776691),
                93893.83772569422,
                93893.83772569422);
            var thin = new GeoEllipse3(
                new GeoPoint3(45349.559353425466, -82000.69372863983, 6774.229241139239),
                new GeoVector3(0.1745884128486597, -0.550031737715422, 0.8166908678347881),
                new GeoVector3(0.4782845629933804, -0.6776317342821664, -0.5586225107315088),
                3.333015914230149,
                0.000616322530496745);

            Assert.True(big.CollidesWith(thin, Thousandth));
            Assert.True(thin.CollidesWith(big, Thousandth));

            GeoPoint3[] places = big.GetIntersections(thin, Thousandth);

            Assert.NotEmpty(places);
            Assert.Contains(places, place => place.DistanceTo(new GeoPoint3(45348.3517, -81998.9820, 6775.6402)) < 2E-3);
            Assert.All(places, place => Assert.InRange(Math.Max(Off(big, place), Off(thin, place)), 0.0, Thousandth.EqualPoint));
        }

        [Fact]
        public void APlaceWhereAThinRimCrossesTheOthersPlane_IsKeptOnlyOnBothRims()
        {
            // The thin rim's crossing of the other's plane was read as the middle of a chord 0.0084 off its own rim, kept
            // because it lay on the other's, and given asked either way.
            var one = new GeoEllipse3(
                new GeoPoint3(22222.22030266292, -39284.76355005277, 5329.377714232253),
                new GeoVector3(-0.7274549909637492, -0.6446170740548052, 0.23511287493234728),
                new GeoVector3(-0.4887314216465123, 0.7272884868759789, 0.4818641451208299),
                182.39506751410434,
                0.0040076341202609335);
            var two = new GeoEllipse3(
                new GeoPoint3(22264.520038336654, -39353.5576577493, 5287.2722735652105),
                new GeoVector3(-0.7074659811746338, -0.6731983379656752, 0.21516478159974373),
                new GeoVector3(0.5118474390946772, -0.6979777583065321, -0.5008385448441536),
                303.99143388680716,
                0.03948332730091812);

            GeoPoint3[] first = one.GetIntersections(two, Thousandth);
            GeoPoint3[] second = two.GetIntersections(one, Thousandth);

            // The rims come within 8.8E-4 near (22311.3617, -39417.4167, 5241.4882): one touch there.
            GeoPoint3 place = Assert.Single(first);
            Assert.InRange(Math.Max(Off(one, place), Off(two, place)), 0.0, Thousandth.EqualPoint);
            Assert.InRange(place.DistanceTo(new GeoPoint3(22311.3617, -39417.4167, 5241.4882)), 0.0, 0.01);
            Assert.Equal(first, second.Reverse());
        }

        [Fact]
        public void TwoCrossingsOfAnEllipseAndACircleFewerThanTwoTolerancesApart_AreOnePlace()
        {
            // The thin rim crosses the circle's plane twice, 0.0011 apart, both on the circle: one place, as two ellipses'.
            var ellipse = new GeoEllipse3(
                new GeoPoint3(91849.02952604416, 43741.93602415823, 1337.4010340019126),
                new GeoVector3(0.43797776991754817, 0.8755124914146062, 0.20409152465264316),
                new GeoVector3(0.4955820293578561, -0.04572609664898428, -0.8673566603553562),
                182.17669591409,
                0.0020565376626237105);
            var circle = new GeoCircle3(
                new GeoPoint3(91501.6885070714, 43850.70059321635, 1616.2102843899133),
                new GeoVector3(0.14549326808481364, 0.8934351320786732, -0.42497690961929263),
                303.22013897971254);

            GeoPoint3[] places = ellipse.GetIntersections(circle, Thousandth);

            Assert.NotEmpty(places);

            for (int i = 0; i < places.Length; i++)
            {
                for (int j = i + 1; j < places.Length; j++)
                {
                    Assert.True(places[i].DistanceTo(places[j]) > 2.0 * Thousandth.EqualPoint);
                }
            }
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        public void TwoCirclesInParallelPlanesWithinTheTolerance_GiveTheirPointsInTheOppositeOrderAskedTheOtherWay(double z)
        {
            // The planes are 0.005 apart, past the planar tolerance, so the circles are read in space; the normals crossed
            // are nought, and the points came in the order they were found either way.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 1E-4);
            var one = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 100);
            var two = new GeoCircle3(new GeoPoint3(60, 20, 0.005), new GeoVector3(0, 0, z), 90);

            GeoPoint3[] first = one.GetIntersections(two, tolerance);
            GeoPoint3[] second = two.GetIntersections(one, tolerance);

            Assert.Equal(2, first.Length);
            Assert.Equal(first, second.Reverse());
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        public void TwoEllipsesInParallelPlanesWithinTheTolerance_GiveTheirPointsInTheOppositeOrderAskedTheOtherWay(double z)
        {
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 1E-4);
            var one = new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 300, 100);
            var two = new GeoEllipse3(new GeoPoint3(60, 20, 0.005), new GeoVector3(0, 0, z), new GeoVector3(1, 0, 0), 200, 150);

            GeoPoint3[] first = one.GetIntersections(two, tolerance);
            GeoPoint3[] second = two.GetIntersections(one, tolerance);

            Assert.True(first.Length >= 2);
            Assert.Equal(first, second.Reverse());
        }

        [Fact]
        public void TryIntersectWithAnEllipseOnACircleAndAnArc_GivesWhatGetIntersectionsGives()
        {
            var ellipse = new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 300, 100);
            var circle = new GeoCircle3(new GeoPoint3(250, 0, 0), new GeoVector3(0, 1, 0), 50);
            var arc = new GeoArc3(new GeoPoint3(250, 0, 0), new GeoVector3(0, 1, 0), 50, 0, Math.PI);

            Assert.True(circle.TryIntersectWith(ellipse, out GeoPoint3[] onCircle, Thousandth));
            Assert.Equal(circle.GetIntersections(ellipse, Thousandth), onCircle);
            Assert.Equal(arc.GetIntersections(ellipse, Thousandth).Length > 0, arc.TryIntersectWith(ellipse, out GeoPoint3[] onArc, Thousandth));
            Assert.Equal(arc.GetIntersections(ellipse, Thousandth), onArc);

            var far = new GeoCircle3(new GeoPoint3(1000, 0, 0), new GeoVector3(0, 1, 0), 50);

            Assert.False(far.TryIntersectWith(ellipse, out GeoPoint3[] none, Thousandth));
            Assert.Empty(none);
        }
    }
}
