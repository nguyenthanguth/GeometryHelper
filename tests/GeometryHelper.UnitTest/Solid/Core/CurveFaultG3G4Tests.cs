using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Ellipses, circles and arcs in space read along both rims, and curves drawing the same one read alike whatever their
    /// type. Searched along the one rim only, two touches lying within a couple of tolerances of each other along it were one
    /// place, though the other rim parts them; an ellipse and a circle drawing the same curve crossed nowhere asked of the
    /// circle and at four places asked of the circle read as an ellipse; and a near copy leaning so that where each starts
    /// stands off the other was taken to touch nothing, its rim within 0.00042 of the other's.
    /// </summary>
    public class CurveFaultG3G4Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Hundredth = new Tolerance(0.01, 1E-5, 1E-5, 0.01);

        private static double Off(GeoEllipse3 ellipse, GeoPoint3 place) => ellipse.GetClosestPointOnBoundary(place).DistanceTo(place);

        /// <summary>
        /// An ellipse 100.0009 by 99.9995 about the centre of the circle of radius 100 in the plan, its major axis towards
        /// where the circle starts and leaning 8E-6 radians, so that the end of that axis stands 0.0008 above the circle:
        /// both starts lie 0.0012 off the other rim, and the rims come within 0.00042 of each other elsewhere.
        /// </summary>
        private static GeoEllipse3 Leaning(GeoCircle3 circle)
        {
            GeoVector3 toStart = circle.Center.GetVectorTo(circle.GetPointAtAngle(0.0)).Normalize();
            double lean = 8E-6;
            GeoVector3 normal = circle.Normal.Multiply(Math.Cos(lean)).Subtract(toStart.Multiply(Math.Sin(lean)));
            GeoVector3 major = toStart.Multiply(Math.Cos(lean)).Add(circle.Normal.Multiply(Math.Sin(lean)));

            return new GeoEllipse3(circle.Center, normal, major, 100.0009, 99.9995);
        }

        [Fact]
        public void AHugeRimPassingANeedleOnBothSidesOfItsTip_IsGivenBothTouchesEitherWay()
        {
            // A rim 30 080 by 18 333 passes a needle 2.78 by 0.0028, leaning 0.56 radians off its plane, 0.000013 from
            // either side of its tip, the gap rising past the tolerance across the needle between. Searched along the huge
            // rim, read at points up to twice the tolerance apart, both touches fell between two readings 0.0026 apart, and
            // only the first was named; searched along the needle as well, both are.
            var huge = new GeoEllipse3(
                new GeoPoint3(-74109.99954403842, 7906.181229234766, 9657.809808690945),
                new GeoVector3(0, 0, 1),
                new GeoVector3(0.5080256669908793, -0.8613419307559992, 0),
                30080.20931392918,
                18333.345100170103);
            var needle = new GeoEllipse3(
                new GeoPoint3(-56660.75035476632, 14172.498393310634, 9659.115329538632),
                new GeoVector3(-0.47785274661964566, -0.2381165956678904, 0.8455514410227181),
                new GeoVector3(0.755978024025799, 0.37875509709921573, 0.5338930638352706),
                2.7820692128883997,
                0.0027703695027531347);

            GeoPoint3[] first = huge.GetIntersections(needle, Thousandth);
            GeoPoint3[] second = needle.GetIntersections(huge, Thousandth);

            Assert.Equal(2, first.Length);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(-56662.59833, 14171.57106, 9657.80981)) <= 1E-4);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(-56662.59951, 14171.57342, 9657.80981)) <= 1E-4);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(huge, place), Off(needle, place)), 0.0, Thousandth.EqualPoint));
            Assert.Equal(first[1], second[0]);
            Assert.Equal(first[0], second[1]);
        }

        [Fact]
        public void AnEllipseAndTheSameCircleReadAsAnEllipse_CrossNowhereAsTheCircleDoes()
        {
            // The ellipse 100.0007 by 99.9995 runs within 0.0007 of the circle of radius 100 about its centre all the way
            // round. Asked of the circle it drew the same curve and crossed nowhere; asked of the circle read as an ellipse,
            // whose axis is not the ellipse's, it crossed at four places.
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0);
            var ellipse = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 100.0007, 99.9995);
            GeoEllipse3 asEllipse = GeoEllipse3.FromCircle(circle);

            Assert.Empty(ellipse.GetIntersections(circle, Thousandth));
            Assert.Empty(ellipse.GetIntersections(asEllipse, Thousandth));
            Assert.Empty(asEllipse.GetIntersections(ellipse, Thousandth));
            Assert.True(ellipse.CollidesWith(circle, Thousandth));
            Assert.True(ellipse.CollidesWith(asEllipse, Thousandth));
            Assert.True(asEllipse.CollidesWith(ellipse, Thousandth));
        }

        [Fact]
        public void ALeaningNearCopyOfACircle_TouchesItThoughBothStartsStandOff()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0);
            GeoEllipse3 ellipse = Leaning(circle);

            Assert.True(ellipse.CollidesWith(circle, Thousandth));
            Assert.True(circle.CollidesWith(ellipse, Thousandth));

            // Drawing the same curve, within the tolerance, the two still cross nowhere.
            Assert.Empty(ellipse.GetIntersections(circle, Thousandth));
        }

        [Fact]
        public void ALeaningNearCopyOfACircleReadAsAnEllipse_TouchesItThoughBothStartsStandOff()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0);
            GeoEllipse3 ellipse = Leaning(circle);
            GeoEllipse3 asEllipse = GeoEllipse3.FromCircle(circle);

            Assert.True(ellipse.CollidesWith(asEllipse, Thousandth));
            Assert.True(asEllipse.CollidesWith(ellipse, Thousandth));
            Assert.Empty(ellipse.GetIntersections(asEllipse, Thousandth));
        }

        [Fact]
        public void AnArcOfACircleRunningPastALeaningNearCopy_TouchesItThoughBothEndsStandOff()
        {
            // The arc runs the long way round from 0.3 radians past where the circle starts to 0.3 short of it, its ends
            // 0.0011 off the rim, and comes within 0.00042 of it between.
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0);
            GeoEllipse3 ellipse = Leaning(circle);
            var arc = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0, 0.3, 2.0 * Math.PI - 0.3, false);

            Assert.True(ellipse.CollidesWith(arc, Thousandth));
            Assert.True(arc.CollidesWith(ellipse, Thousandth));
            Assert.Empty(ellipse.GetIntersections(arc, Thousandth));
        }

        [Fact]
        public void AnEllipsePassingBothSidesOfOneJustOverFourTolerancesWide_IsGivenBothTouches()
        {
            // At a tolerance of 0.01, an ellipse 21.25 by 0.93, b² / a 4.07 tolerances, passes a rim 5.26 by 3.59 within
            // 0.0036 and 0.0072 at two places 0.058 apart, the gap rising 4.4 % above the tolerance between. Searched only
            // where the other was within four tolerances of its major axis, the second was missed.
            var narrow = new GeoEllipse3(
                new GeoPoint3(-4219.376623732678, 8984.126238610654, -10285.090035891668),
                new GeoVector3(-0.8535056798574471, -0.15916194046684468, 0.4961809459843311),
                new GeoVector3(-0.519751619805289, 0.32807057669035616, -0.7888142686461976),
                21.253666516442145,
                0.9302766323877225);
            var rim = new GeoEllipse3(
                new GeoPoint3(-4226.764012418236, 8993.86951596434, -10304.046397387096),
                new GeoVector3(0.6945234045236949, -0.5268098059587566, 0.49000884575129183),
                new GeoVector3(0.6856215238933573, 0.6910615389307593, -0.22881668511015787),
                5.259294101756426,
                3.593496663943271);

            GeoPoint3[] first = narrow.GetIntersections(rim, Hundredth);
            GeoPoint3[] second = rim.GetIntersections(narrow, Hundredth);

            Assert.Equal(2, first.Length);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(-4230.4107, 8991.1307, -10301.8235)) <= 0.01);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(-4230.4227, 8991.0863, -10301.8583)) <= 0.01);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(narrow, place), Off(rim, place)), 0.0, Hundredth.EqualPoint));
            Assert.Equal(2, second.Length);
            Assert.Contains(second, place => place.Equals(first[0]));
            Assert.Contains(second, place => place.Equals(first[1]));
        }

        [Fact]
        public void ARimPassingBothSidesOfANarrowEllipseCloseToItsEnd_IsGivenBothTouchesEitherWay()
        {
            // At a tolerance of 0.01, a rim 36.66 by 30.95 passes an ellipse 21.88 by 0.908, b² / a 3.77 tolerances, within
            // 0.0020 at two places 0.056 apart either side of its end, the gap rising 4 % above the tolerance between. The
            // search along the narrow ellipse began inside the run of the touch named first, read the two runs as one across
            // the rise, and took the second touch for the first.
            var narrow = new GeoEllipse3(
                new GeoPoint3(-33.68619365323622, 2110.4826275773726, 7216.968758598421),
                new GeoVector3(-0.17011324961544955, -0.9845678765818896, -0.0410801498086472),
                new GeoVector3(0.894257413589406, -0.1717533619040553, 0.41328496333043696),
                21.87594796641295,
                0.908454967542619);
            var rim = new GeoEllipse3(
                new GeoPoint3(-42.1902147746862, 2114.6410212566825, 7214.014290213758),
                new GeoVector3(-0.24505633413802438, -0.967315666142056, -0.06517511131546705),
                new GeoVector3(0.04426300266663882, 0.05599202064159013, -0.9974495878085292),
                36.66079772208999,
                30.949647423011815);

            GeoPoint3[] first = rim.GetIntersections(narrow, Hundredth);
            GeoPoint3[] second = narrow.GetIntersections(rim, Hundredth);

            Assert.Equal(2, first.Length);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(-14.1228, 2106.7257, 7225.9740)) <= 1E-3);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(-14.1394, 2106.7263, 7226.0277)) <= 1E-3);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(narrow, place), Off(rim, place)), 0.0, Hundredth.EqualPoint));
            Assert.Equal(2, second.Length);
            Assert.Contains(second, place => place.Equals(first[0]));
            Assert.Contains(second, place => place.Equals(first[1]));
        }

        [Fact]
        public void ARimPassingBothSidesOfAnEllipseNineTolerancesWideBesideTheTouch_IsGivenBothTouchesEitherWay()
        {
            // At a tolerance of 0.01, a rim 10 336.8 by 7 088.3 passes an ellipse 23.46 by 1.379, b² / a 8.11 tolerances and
            // its rim 9.29 tolerances from its major axis beside the touch, within 0.0030 and 0.00036 at two places 0.0755
            // apart, the gap rising 4 % above the tolerance between. Searched only where the ellipse is no more than eight
            // tolerances wide, it was given one place either way.
            var narrow = new GeoEllipse3(
                new GeoPoint3(10.69218835825668, 7.215837229609413, 35.74210507131281),
                new GeoVector3(0, 0, 1),
                new GeoVector3(-0.9989662412109681, -0.0454582107086272, 0),
                23.45998725549317,
                1.3790586216437397);
            var rim = new GeoEllipse3(
                new GeoPoint3(-6721.345618352533, -2427.4723542966935, 6645.565925897813),
                new GeoVector3(-0.709544231034093, 0.0634433656492966, -0.7017990620977825),
                new GeoVector3(0.6102671818446129, 0.5532631701298678, -0.5669866235999605),
                10336.847674907878,
                7088.311734136534);

            GeoPoint3[] first = rim.GetIntersections(narrow, Hundredth);
            GeoPoint3[] second = narrow.GetIntersections(rim, Hundredth);

            Assert.Equal(2, first.Length);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(34.1146, 8.2377, 35.7421)) <= 1E-3);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(34.1206, 8.3130, 35.7425)) <= 1E-3);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(narrow, place), Off(rim, place)), 0.0, Hundredth.EqualPoint));
            Assert.Equal(2, second.Length);
            Assert.Contains(second, place => place.Equals(first[0]));
            Assert.Contains(second, place => place.Equals(first[1]));
        }

        [Fact]
        public void ACircleDrawnAsAnEllipseWithItsRadiiARoundingApart_IsTheCircleItDraws()
        {
            // The circle of radius 100 drawn as an ellipse 100 by 99.99999999999999, as a transform may leave it, and an
            // ellipse 100.0007 by 99.9995 about its centre, its axis turned a quarter. Told a circle only by radii exactly one,
            // the first crossed the second at four places; read as the circle it draws, the two cross nowhere, as the circle
            // read by FromCircle does.
            var circle = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 100.0, 99.99999999999999);
            var ellipse = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.YAxis, 100.0007, 99.9995);

            Assert.Empty(circle.GetIntersections(ellipse, Thousandth));
            Assert.Empty(ellipse.GetIntersections(circle, Thousandth));
            Assert.True(circle.CollidesWith(ellipse, Thousandth));
            Assert.True(ellipse.CollidesWith(circle, Thousandth));
        }

        [Fact]
        public void TwoNearlyRoundEllipsesWithTheirAxesTurned_AreNotTheSameCurve()
        {
            // An ellipse 1.04978 by 1.04840 and one 1.04936 by 1.04903, their centres 0.00032 apart and their axes turned
            // 1.28 radians, neither drawn as a circle: their rims run within the tolerance of each other along two stretches,
            // 0.00008 and 0.00019 apart at the nearest, and part between. Read as one curve whichever way the axes point,
            // they crossed nowhere; they cross at four places, three on the longer stretch and one on the other, as before.
            var one = new GeoEllipse3(
                new GeoPoint3(7326.410892905933, -8618.711014631606, 6026.865754327713),
                new GeoVector3(0.06711405790543741, 0.264315066582733, -0.9620983571386197),
                new GeoVector3(-0.31176824335149117, 0.9215437635723144, 0.2314252671133811),
                1.0497778417408365,
                1.048396117137604);
            var other = new GeoEllipse3(
                new GeoPoint3(7326.410914457596, -8618.711325628083, 6026.8656704699915),
                new GeoVector3(0.06677353255933914, 0.2640737324852595, -0.9621883179299417),
                new GeoVector3(-0.9974439645003124, -0.0069140136622864844, -0.07111774811520252),
                1.0493615053679153,
                1.0490306611782552);

            GeoPoint3[] first = one.GetIntersections(other, Thousandth);

            Assert.Equal(4, first.Length);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(7327.1889, -8619.4022, 6026.7301)) <= 1E-4);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(7325.4856, -8618.2216, 6026.9357)) <= 1E-4);
            Assert.All(first, place => Assert.InRange(Math.Max(Off(one, place), Off(other, place)), 0.0, Thousandth.EqualPoint));
            Assert.Equal(first.Length, other.GetIntersections(one, Thousandth).Length);
            Assert.True(one.CollidesWith(other, Thousandth));
        }
    }
}
