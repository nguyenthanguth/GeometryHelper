using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An ellipse in space read in one plane with another ellipse, a circle or an arc touches it wherever the two rims pass
    /// within the point tolerance of each other, and two ellipses give their points in the opposite order asked the other
    /// way: read only in the plane of the one lying within the planar tolerance of the other's, with the other laid flat
    /// in it, a huge rim and a thin one passing 0.00016 apart touched neither way, and their crossings came in the same
    /// order both ways round.
    /// </summary>
    public class CurveFaultF12InPlaneTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Coarse = new Tolerance(0.05, 1E-5, 1E-5, 0.05);

        private static double Off(GeoEllipse3 ellipse, GeoPoint3 place) => ellipse.GetClosestPointOnBoundary(place).DistanceTo(place);

        private static double Off(GeoCircle3 circle, GeoPoint3 place)
        {
            GeoVector3 offset = circle.Center.GetVectorTo(place);
            double height = offset.DotProduct(circle.Normal);
            double across = offset.Subtract(circle.Normal.Multiply(height)).Length - circle.Radius;

            return Math.Sqrt(height * height + across * across);
        }

        private static void AssertOnBoth(GeoEllipse3 one, GeoEllipse3 two, GeoPoint3[] places, Tolerance tolerance)
        {
            Assert.All(places, place => Assert.InRange(Math.Max(Off(one, place), Off(two, place)), 0.0, tolerance.EqualPoint));
        }

        [Fact]
        public void AThinEllipseLyingInThePlaneOfAHugeOne_TouchesItAskedEitherWay()
        {
            // The thin one lies within the planar tolerance of the huge one's plane, which leans 0.020 radians off its own:
            // laid flat in the thin one's plane with its radii kept, the huge rim stood 3.1 off its true place there, and
            // the touch 0.00016 across, near (-5595.7424, -6793.7403, -12510.0886), was missed both ways.
            var huge = new GeoEllipse3(
                new GeoPoint3(-20.525762564747485, -49.040734860599386, 17.04087460741441),
                new GeoVector3(0.8871434945355793, -0.43204794577722067, -0.1622066356623068),
                new GeoVector3(0.3878196821071947, 0.8884642978283096, -0.24541207316414418),
                15338.192021166064,
                15273.718032499803);
            var thin = new GeoEllipse3(
                new GeoPoint3(-5595.193125096015, -6792.215603527947, -12511.143114477847),
                new GeoVector3(0.8943832960479363, -0.4229517046743804, -0.1455691425524829),
                new GeoVector3(0.2840483940562612, 0.7884377207421301, -0.5456028503820465),
                1.9740015715239576,
                0.0023454472369559397);

            GeoPoint3[] first = huge.GetIntersections(thin, Thousandth);
            GeoPoint3[] second = thin.GetIntersections(huge, Thousandth);

            Assert.True(huge.CollidesWith(thin, Thousandth));
            Assert.True(thin.CollidesWith(huge, Thousandth));
            GeoPoint3 place = Assert.Single(first);
            Assert.InRange(place.DistanceTo(new GeoPoint3(-5595.7424, -6793.7403, -12510.0886)), 0.0, 0.001);
            AssertOnBoth(huge, thin, first, Thousandth);
            Assert.Equal(first, second);
            Assert.True(huge.TryIntersectWith(thin, out GeoPoint3[] tried, Thousandth));
            Assert.Equal(first, tried);
        }

        [Fact]
        public void TheCrossingsOfAThinEllipseInThePlaneOfAHugeOne_ComeInTheOppositeOrderAskedTheOtherWay()
        {
            // Both were read from the thin one, which alone lies within the planar tolerance of the other's plane, and put in
            // order from it whichever was asked first.
            var huge = new GeoEllipse3(
                new GeoPoint3(-47.798393200057745, -4.198494113142829, 39.43068971365257),
                new GeoVector3(-0.2982440183971668, -0.8468045933135845, 0.44042307640872447),
                new GeoVector3(-0.7926271187950792, 0.4768021152105001, 0.3800026229927951),
                69635.41002461474,
                50234.278560237915);
            var thin = new GeoEllipse3(
                new GeoPoint3(35411.72431612721, -34800.71415943265, -42851.73310769554),
                new GeoVector3(-0.2986259562854496, -0.8467711273839822, 0.440228572518255),
                new GeoVector3(-0.904133482301196, 0.10332559605563492, -0.41456780794179005),
                2.1101864532614996,
                0.00031695278899562915);

            GeoPoint3[] first = huge.GetIntersections(thin, Thousandth);
            GeoPoint3[] second = thin.GetIntersections(huge, Thousandth);

            Assert.Equal(2, first.Length);
            Assert.Equal(first, second.Reverse());
            AssertOnBoth(huge, thin, first, Thousandth);
        }

        [Fact]
        public void AThinEllipseTiltedAcrossAnotherWithinACoarsePlanarTolerance_TouchesIt()
        {
            // At a tolerance of 0.05, the thin one, 489 by 0.02, lies within the planar tolerance of the other's plane, which
            // leans 0.98 radians off its own; the rims pass 0.0087 apart, and the other laid flat in the thin one's plane
            // stood 0.10 off its true place there.
            var thin = new GeoEllipse3(
                new GeoPoint3(-96794.47784870607, 72754.55136446029, 4978.340535880226),
                new GeoVector3(-0.6677518711276011, 0.6617193232420358, 0.34093250923561075),
                new GeoVector3(0.45215144338042873, 0.7243935711505581, -0.5203969891580236),
                489.24183263873766,
                0.019939292260408713);
            var other = new GeoEllipse3(
                new GeoPoint3(-93063.16399369996, 78777.36639193294, 704.0507478411089),
                new GeoVector3(-0.8625634021972824, 0.20663165515226978, -0.46183085245453503),
                new GeoVector3(-0.4509380516696174, -0.7278998268864811, 0.5165430432936257),
                8204.890507368786,
                3.601434272043523);

            GeoPoint3[] first = thin.GetIntersections(other, Coarse);
            GeoPoint3[] second = other.GetIntersections(thin, Coarse);

            Assert.True(thin.CollidesWith(other, Coarse));
            Assert.True(other.CollidesWith(thin, Coarse));
            Assert.NotEmpty(first);
            AssertOnBoth(thin, other, first, Coarse);
            Assert.Equal(first, second.Reverse());
        }

        [Fact]
        public void AThinEllipseLyingInThePlaneOfAHugeCircle_TouchesIt()
        {
            // The ellipse 262.6 by 0.041 lies in the plan, within the planar tolerance of 0.05 of the plane of the circle of
            // radius 12 846, which leans 0.61 radians off it; laid flat in the plan with its radius kept, the circle stood
            // 2 327 off its true place at the touch, 0.0019 across, which was missed.
            var ellipse = new GeoEllipse3(
                new GeoPoint3(-11.435036995185087, -38.41557283346335, 21.503817183665845),
                new GeoVector3(0, 0, 1),
                new GeoVector3(-0.5042752307360704, -0.8635429877348799, 0),
                262.5778879609787,
                0.04091461845488277);
            var circle = new GeoCircle3(
                new GeoPoint3(-9172.217774117431, 5132.727773736095, -7352.542130169015),
                new GeoVector3(-0.49567063598826383, 0.28954623466757606, 0.81882452247647),
                12845.794182664618);

            GeoPoint3[] first = ellipse.GetIntersections(circle, Coarse);
            GeoPoint3[] second = circle.GetIntersections(ellipse, Coarse);

            Assert.True(ellipse.CollidesWith(circle, Coarse));
            Assert.True(circle.CollidesWith(ellipse, Coarse));
            Assert.NotEmpty(first);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(ellipse, place), Off(circle, place)), 0.0, Coarse.EqualPoint));
            Assert.Equal(first, second);
        }

        [Fact]
        public void AnArcLyingInThePlaneOfAThinEllipse_TouchesIt()
        {
            // The arc of radius 117.4 lies within the planar tolerance of 0.05 of the plane of the ellipse 31.7 by 0.01, and
            // passes 0.038 from its rim; laid flat in that plane, it stood 0.035 off its true place there, and the gap read
            // flat was past the tolerance.
            var ellipse = new GeoEllipse3(
                new GeoPoint3(15.824122478172242, 10.594291966685233, -6.470349969561376),
                new GeoVector3(0.2973725253530134, 0.3806979653062529, -0.8755790314853654),
                new GeoVector3(0.32005451374401545, 0.8242563459447546, 0.46708305942506934),
                31.656997060243505,
                0.010023465761165393);
            var arc = new GeoArc3(
                new GeoPoint3(130.876636705284, -14.309221238378875, 21.817125404857514),
                new GeoVector3(0.2974749382279511, 0.38094613640826064, -0.8754362925318414),
                117.42156934804356,
                2.4835392476019345,
                1.9307993188214663,
                true);

            GeoPoint3[] first = ellipse.GetIntersections(arc, Coarse);
            GeoPoint3[] second = arc.GetIntersections(ellipse, Coarse);

            Assert.True(ellipse.CollidesWith(arc, Coarse));
            Assert.True(arc.CollidesWith(ellipse, Coarse));
            Assert.NotEmpty(first);
            Assert.All(first, place => Assert.InRange(Off(ellipse, place), 0.0, Coarse.EqualPoint));
            Assert.All(first, place => Assert.True(arc.IsPointOn(place, Coarse)));
            Assert.Equal(first, second);
        }

        [Fact]
        public void TwoPlacesReadInOnePlaneFewerThanTwoTolerancesApart_AreOnePlace()
        {
            // At a tolerance of 0.05, two crossings read in the plane 0.0916 apart, within twice the tolerance, were kept
            // as two places, merged only within one tolerance where the space path merges within two.
            var thin = new GeoEllipse3(
                new GeoPoint3(-59756.77983824014, -90606.90300101736, -2374.1230798764723),
                new GeoVector3(0, 0, 1),
                new GeoVector3(0.3064955732150466, -0.9518720836328692, 0),
                280.63545425452077,
                0.01745584732172206);
            var other = new GeoEllipse3(
                new GeoPoint3(-59753.97769944059, -90594.1438971677, -2374.1280791790755),
                new GeoVector3(0.0005857415263532276, -0.0005665931771252312, 0.9999996679394628),
                new GeoVector3(-0.039170855921200405, -0.9992323798619025, -0.0005432144322365911),
                39.61241715569628,
                0.035332311736324486);

            foreach (GeoPoint3[] places in new[] { thin.GetIntersections(other, Coarse), other.GetIntersections(thin, Coarse) })
            {
                Assert.NotEmpty(places);
                AssertOnBoth(thin, other, places, Coarse);

                for (int i = 0; i < places.Length; i++)
                {
                    for (int j = i + 1; j < places.Length; j++)
                    {
                        Assert.True(places[i].DistanceTo(places[j]) > 2.0 * Coarse.EqualPoint);
                    }
                }
            }

            Assert.Equal(thin.GetIntersections(other, Coarse), other.GetIntersections(thin, Coarse).Reverse());
        }

        [Fact]
        public void AnEllipseAndItsCopyMovedLessThanTheTolerance_StillCrossNowhereAndCollide()
        {
            // The same ellipse, as documented, though its rim runs within the tolerance of the other's all the way round.
            var ellipse = new GeoEllipse3(new GeoPoint3(1000, 2000, 30), new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 300, 100);
            var copy = new GeoEllipse3(new GeoPoint3(1000.0004, 2000.0003, 30.0002), new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 300.0005, 99.9996);

            Assert.Empty(ellipse.GetIntersections(copy, Thousandth));
            Assert.Empty(copy.GetIntersections(ellipse, Thousandth));
            Assert.True(ellipse.CollidesWith(copy, Thousandth));
            Assert.True(copy.CollidesWith(ellipse, Thousandth));
        }
    }
}
