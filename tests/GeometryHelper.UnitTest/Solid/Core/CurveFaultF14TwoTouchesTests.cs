using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two curves in space that pass within the point tolerance of each other at two places close together along the rim,
    /// the gap rising above the tolerance between, are given both places: read every 64th of a turn, both fell in one piece
    /// of the rim and were one place, as a circle of radius 247 passing a rim 5 995 by 2 863 at two places 11.4 apart, and a
    /// circle crossing a needle 33.5 long at two places 0.141 apart.
    /// </summary>
    public class CurveFaultF14TwoTouchesTests
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

        private static void AssertNear(GeoPoint3[] places, GeoPoint3 expected, double within)
        {
            Assert.Contains(places, place => place.DistanceTo(expected) <= within);
        }

        [Fact]
        public void ACirclePassingAnEllipseAtTwoPlacesElevenApart_IsGivenBoth()
        {
            // Along the circle the gap is least, 0.0184, at (2522.2798, 744.7548, -2697.6351) and, 0.0172, at
            // (2533.2429, 747.8536, -2695.5378), and rises to 0.0686 between. Both lay in one 64th of a turn of the ellipse,
            // and only the second was named.
            var ellipse = new GeoEllipse3(
                new GeoPoint3(-14.598558454121722, 27.0001149163582, 12.97602609869839),
                new GeoVector3(0.2721252041791679, -0.9622618463390966, -0.00011104286987786893),
                new GeoVector3(-0.9601071491276105, -0.27152357587368514, 0.06685214984441014),
                5995.113407259394,
                2862.676242427194);
            var circle = new GeoCircle3(
                new GeoPoint3(2484.771086997963, 734.1435931706027, -2453.899663682633),
                new GeoVector3(0.2720034470524818, -0.9622962763784162, -3.549369958752394E-05),
                246.832853884824);

            GeoPoint3[] first = ellipse.GetIntersections(circle, Coarse);
            GeoPoint3[] second = circle.GetIntersections(ellipse, Coarse);

            Assert.Equal(2, first.Length);
            AssertNear(first, new GeoPoint3(2522.2798, 744.7548, -2697.6351), 0.05);
            AssertNear(first, new GeoPoint3(2533.2429, 747.8536, -2695.5378), 0.05);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(ellipse, place), Off(circle, place)), 0.0, Coarse.EqualPoint));
            Assert.Equal(first, second);
        }

        [Fact]
        public void ACircleCrossingANeedleTwice_IsGivenBothCrossings()
        {
            // A needle 33.47 by 0.000134 and a circle of radius 2.12 nearly in its plane, which dips over it and back 0.141
            // further along: one place was named, at (26556.5856, 80323.4102, 8195.5042).
            var needle = new GeoEllipse3(
                new GeoPoint3(26553.36648530948, 80329.53794129638, 8195.504293868087),
                new GeoVector3(0, 0, 1),
                new GeoVector3(0.4650875488268597, -0.885264690319355, 0),
                33.466059218377836,
                0.00013412161480361672);
            var circle = new GeoCircle3(
                new GeoPoint3(26558.49212168355, 80324.33675045847, 8195.504413335902),
                new GeoVector3(-0.0002814249554052923, 0.00019709132339468938, 0.9999999409775007),
                2.119711841215205);

            GeoPoint3[] first = needle.GetIntersections(circle, Thousandth);

            Assert.Equal(2, first.Length);
            AssertNear(first, new GeoPoint3(26556.5856, 80323.4102, 8195.5042), 0.001);
            AssertNear(first, new GeoPoint3(26556.6475, 80323.2924, 8195.5042), 0.001);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(needle, place), Off(circle, place)), 0.0, Thousandth.EqualPoint));
            Assert.Equal(first, circle.GetIntersections(needle, Thousandth));
        }

        [Fact]
        public void TwoThinEllipsesRunningAlongEachOther_TouchAtBothEnds()
        {
            // An ellipse 313.85 by 4.54 and another 0.001 larger, nearly in its plane: their rims run along each other 1.0002
            // tolerances apart down the long sides and touch either side of each end, the gap rising above the tolerance
            // round the very end. Read as one run over the whole turn, the far end's touches near (-49521.2, 34509.6) were
            // not named; read from the start of the turn, a run over it was two, and gave a place at either end of it.
            var thin = new GeoEllipse3(
                new GeoPoint3(-49560.50503512868, 34818.419131831455, 7906.214631118911),
                new GeoVector3(0, 0, 1),
                new GeoVector3(0.1262946144378075, -0.991992777374919, 0),
                313.84929834578617,
                4.542464212928044);
            var larger = new GeoEllipse3(
                new GeoPoint3(-49560.50503512868, 34818.419131831455, 7906.21463356758),
                new GeoVector3(2.8827477272527813E-06, 1.90868754854313E-06, 0.9999999999940233),
                new GeoVector3(0.12629461444236387, -0.99199277737316, 1.5293287496780174E-06),
                313.8502983491737,
                4.543464216315594);

            GeoPoint3[] first = thin.GetIntersections(larger, Thousandth);

            Assert.Equal(4, first.Length);
            Assert.Equal(2, first.Count(place => place.DistanceTo(new GeoPoint3(-49600.0, 35127.5, 7906.2146)) <= 1.5));
            Assert.Equal(2, first.Count(place => place.DistanceTo(new GeoPoint3(-49521.2, 34509.6, 7906.2149)) <= 1.5));
            Assert.All(first, place => Assert.InRange(Math.Max(Off(thin, place), Off(larger, place)), 0.0, Thousandth.EqualPoint));
            Assert.Equal(first.Reverse(), larger.GetIntersections(thin, Thousandth));
        }

        [Fact]
        public void ALongThinEllipsePassingASmallOneAtTwoPlaces_IsGivenBoth()
        {
            // A rim 67 620 by 6.84 passes one 3.40 by 3.30 at two places 0.167 apart. Parted, the second run was taken for
            // the first's, the first place read 0.0000075 radians along the long rim from where it lies.
            var thin = new GeoEllipse3(
                new GeoPoint3(-96369.9281198764, 18449.35050161991, 1144.1546730437112),
                new GeoVector3(-0.28102472683447405, 0.3797980175582092, -0.8813504233654532),
                new GeoVector3(-0.8582891657082389, 0.3114185970217811, 0.4078702801833457),
                67620.3338231986,
                6.841657484208821);
            var small = new GeoEllipse3(
                new GeoPoint3(-40384.11525397166, -1862.678598456805, -25461.038585024347),
                new GeoVector3(-0.3574071827316303, 0.20674240635578486, -0.9107786136850484),
                new GeoVector3(0.6334518186323617, -0.6629387236087401, -0.3990627046109022),
                3.404369002396413,
                3.297169904492431);

            GeoPoint3[] first = thin.GetIntersections(small, Thousandth);

            Assert.Equal(2, first.Length);
            AssertNear(first, new GeoPoint3(-40385.1924, -1865.8042, -25461.3251), 0.001);
            AssertNear(first, new GeoPoint3(-40385.3357, -1865.7523, -25461.2570), 0.001);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(thin, place), Off(small, place)), 0.0, Thousandth.EqualPoint));
        }

        [Fact]
        public void AnEllipseAndANearCopyCircle_AreTouchedOnceWhereARunCrossesTheStartOfTheTurn()
        {
            // An ellipse 458.5675 by 458.5645 and a circle of radius 458.5668 nearly on it: four places where the rims come
            // nearest. One run crosses the angle the search starts at, 2.0169641759523502, where that angle plus a turn less
            // itself measures a hair short of a turn; read as two runs, it gave a fifth place 143 from the fourth.
            var ellipse = new GeoEllipse3(
                new GeoPoint3(-26.724187087604864, -21.70802074098402, 44.184184956450096),
                new GeoVector3(-0.44955160680737877, 0.01390233911168801, -0.893146168207717),
                new GeoVector3(-0.7458104912932043, 0.5444381498043546, 0.383866919797214),
                458.56753846563754,
                458.56447065378245);
            var circle = new GeoCircle3(
                new GeoPoint3(-26.724586528227356, -21.70897782051644, 44.184838146548444),
                new GeoVector3(-0.44955141326421355, 0.013902471037456747, -0.8931462635712031),
                458.566792552306);

            GeoPoint3[] first = ellipse.GetIntersections(circle, Thousandth);

            Assert.Equal(4, first.Length);
            Assert.All(first, place => Assert.All(first.Where(other => !other.Equals(place)), other => Assert.True(place.DistanceTo(other) > 300)));
            Assert.All(first, place => Assert.InRange(Math.Max(Off(ellipse, place), Off(circle, place)), 0.0, Thousandth.EqualPoint));
            Assert.Equal(first, circle.GetIntersections(ellipse, Thousandth));
        }

        [Fact]
        public void AThinEllipsePassingACircleAtTwoPlacesAThirdApart_IsGivenBoth()
        {
            // A rim 285.55 by 3.38 passes a circle of radius 21.2 within 0.00044 at two places 0.348 apart, the gap rising to
            // 0.00111 between along either. Parted by the search, the second was taken for the first, both curves passing
            // within the tolerance of the point halfway between, though the circle leaves it between the two.
            var thin = new GeoEllipse3(
                new GeoPoint3(38.663942221861305, -13.480186072867454, 25.458365760491404),
                new GeoVector3(0.8563343510641875, 0.3201901449602639, 0.4051786646132818),
                new GeoVector3(-0.2759515882337974, -0.37947214933458406, 0.8830920726802137),
                285.55037741807774,
                3.3812121871164766);
            var circle = new GeoCircle3(
                new GeoPoint3(-46.47100029998064, -100.60143279854442, 274.2502659457216),
                new GeoVector3(0.8564312846913384, 0.31998255713828294, 0.4051377762307399),
                21.23512104211148);

            GeoPoint3[] first = thin.GetIntersections(circle, Thousandth);

            Assert.Equal(2, first.Length);
            AssertNear(first, new GeoPoint3(-37.5505, -119.4600, 270.2866), 0.001);
            AssertNear(first, new GeoPoint3(-37.4474, -119.3427, 269.9761), 0.001);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(thin, place), Off(circle, place)), 0.0, Thousandth.EqualPoint));
            Assert.Equal(first, circle.GetIntersections(thin, Thousandth));
        }

        [Fact]
        public void ANeedlePassingAnArcAtTwoPlacesFourApart_IsGivenBoth()
        {
            // A needle 19.5 by 0.055 passes an arc of radius 126 within 0.019 at (18.056, -17.198, -46.681) and within 0.040
            // at (18.803, -21.465, -46.422), the gap rising to 0.0586 between, at a tolerance of 0.05. The second was taken
            // for the first, both curves passing within the tolerance of the point halfway between, near straight there.
            var needle = new GeoEllipse3(
                new GeoPoint3(18.319031534865047, -18.60173566667444, -46.58644012947867),
                new GeoVector3(-0.4659982142670521, -0.027906496315682545, 0.8843454595141548),
                new GeoVector3(-0.14998856086795945, 0.987528046052129, -0.04787264217925191),
                19.50681237015259,
                0.05535047908097063);
            var arc = new GeoArc3(
                new GeoPoint3(-91.30651819909187, -41.04287520609369, -105.04032038031943),
                new GeoVector3(-0.46600700292197916, -0.02789925946485594, 0.8843410566907924),
                126.23250651538194,
                3.2972645237411564,
                6.58982101576218,
                false);

            GeoPoint3[] first = needle.GetIntersections(arc, Coarse);

            Assert.Equal(2, first.Length);
            AssertNear(first, new GeoPoint3(18.056, -17.198, -46.681), Coarse.EqualPoint);
            AssertNear(first, new GeoPoint3(18.803, -21.465, -46.422), Coarse.EqualPoint);
            Assert.All(first, place => Assert.InRange(Off(needle, place), 0.0, Coarse.EqualPoint));
            Assert.Equal(first, arc.GetIntersections(needle, Coarse));
        }

        [Fact]
        public void AHugeThinEllipseRingedByACopyALittleLarger_TouchesOnceAtEachEnd()
        {
            // A rim 100 000 by 1 and another 0.001 larger in its plane. From the larger, the gap rises a hair above the
            // tolerance round each end, between the two sides; from the smaller it does not, its end lying within the
            // tolerance of the larger all round. Each end is one place, as before the runs were parted.
            var thin = new GeoEllipse3(
                new GeoPoint3(72206.69536488442, -74306.22511278196, -3641.724751164077),
                new GeoVector3(0, 0, 1),
                new GeoVector3(0.7699999703616046, -0.6380439214059861, 0),
                100000,
                1);
            var larger = new GeoEllipse3(
                new GeoPoint3(72206.69536488442, -74306.22511278196, -3641.7247910197684),
                new GeoVector3(1.7642562851799126E-08, 1.0678999216121345E-08, 0.9999999999999998),
                new GeoVector3(0.7699999703616045, -0.6380439214059862, -6.771102336442558E-09),
                100000.00100011282,
                1.0010001128306836);

            GeoPoint3[] first = thin.GetIntersections(larger, Thousandth);

            Assert.Equal(2, first.Length);
            AssertNear(first, new GeoPoint3(-4792.7863, -10502.2553, -3641.7244), 0.01);
            AssertNear(first, new GeoPoint3(149206.3096, -138110.2959, -3641.7255), 0.01);
            Assert.Equal(first.Reverse(), larger.GetIntersections(thin, Thousandth));
        }
    }
}
