using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Plane;
using Xunit;
using static GeometryHelper.UnitTest.Solid.Ellipse3Oracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Decisions 18 and 19: where the rim of an ellipse in space crosses an arc, a circle or another ellipse, and whether
    /// they touch, as <see cref="GeoCircle3"/> answers (Arc3.Curves.cs). In planes that cross, every shared point is on the
    /// line the planes share, so the crossings are the points both curves keep on that line (Arc3.Curves.cs:120-130),
    /// named once, in order along it: along N × the other's normal for a circle or an ellipse, whose answers start from
    /// the one asked (GeoCircle3.Intersection.cs:281, Arc3.Curves.cs:229-230); along the arc's normal × N for an arc,
    /// which the circle asks first (GeoCircle3.Intersection.cs:256). In one plane, the 2D answer laid out (decision 19),
    /// compared here as a set: the circle's order along its radical line has no counterpart for an ellipse. Two curves
    /// lying on each other cross nowhere and touch (Arc3.Curves.cs:148-156). A rim a hair inside the other touches it
    /// once, midway (Arc3.Curves.cs:60-80). Planes are one when the rim stands off by no more than the planar tolerance
    /// (Arc3.cs:44-53). Bodies are built so the answer is known: the second curve is made through rim points of the first.
    /// </summary>
    public class Ellipse3QueryCurveTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private const double Close = 1E-9;

        private const double Touch = 1E-6;

        /// <summary>
        /// Planes a thousandth of a degree apart meet in a line found from a near-singular system: the circle itself puts
        /// the crossing 1.2E-5 off at 0.001 degrees, 3.6E-7 at 0.01 (measured on 81e409e). A tenth of the point tolerance.
        /// </summary>
        private const double NearCoplanar = 1E-4;

        /// <summary>The rim points at t = 0.6 and t = 2.8 of <see cref="Ellipse3Oracle.Tilted()"/>, which the partners are built through.</summary>
        private static void Pair(out Specimen e, out GeoPoint3 first, out GeoPoint3 second)
        {
            e = Tilted();
            first = e.Rim(0.6);
            second = e.Rim(2.8);
        }

        private static double Deg(double degrees) => degrees * Math.PI / 180;

        #region Planes that cross

        [Fact]
        public void AnEllipseInAPlaneAcrossCrossesTheOtherWhereBothMeetTheLineThePlanesShare()
        {
            // The second ellipse, 330.4 by 132.1, lies in the plane leaning 70 degrees about the chord between the two rim
            // points and passes through both, so the line the planes share is the chord's, and each rim meets it there.
            Pair(out Specimen e, out GeoPoint3 first, out GeoPoint3 second);
            Specimen other = ThroughTwo(first, second, LeanAbout(e, first.GetVectorTo(second), Deg(70)), 0.3, 0.4, 0.25);

            GeoPoint3[] crossings = e.Build().GetIntersections(other.Build(), Tol);

            SamePointsInOrder(Along(e.N.CrossProduct(other.N), first, second), crossings, Close);
            Assert.True(e.Build().CollidesWith(other.Build(), Tol));
            Assert.True(e.Build().TryIntersectWith(other.Build(), out GeoPoint3[] tried, Tol));
            SamePointsInOrder(crossings, tried, 0.0);

            // Asked the other way the line runs the other way.
            SamePointsInOrder(Along(other.N.CrossProduct(e.N), first, second), other.Build().GetIntersections(e.Build(), Tol), Close);
        }

        [Fact]
        public void AnEllipseCrossesACircleInAPlaneAcrossWhereBothMeetTheLine()
        {
            // The circle through both rim points, its centre 30 off the chord in the leaning plane: radius 267.1.
            Pair(out Specimen e, out GeoPoint3 first, out GeoPoint3 second);
            GeoCircle3 circle = CircleThroughTwo(first, second, LeanAbout(e, first.GetVectorTo(second), Deg(70)), 30);

            SamePointsInOrder(Along(e.N.CrossProduct(circle.Normal), first, second), e.Build().GetIntersections(circle, Tol), Close);
            Assert.True(e.Build().CollidesWith(circle, Tol));
            Assert.True(e.Build().TryIntersectWith(circle, out _, Tol));
        }

        [Fact]
        public void AnEllipseCrossesAnArcInAPlaneAcrossWhereTheArcReachesInTheArcsOrder()
        {
            // An arc of that circle from one rim point to the other over its far side holds both; an arc 0.4 rad about
            // the first holds that one only; one about the circle's far point holds neither.
            Pair(out Specimen e, out GeoPoint3 first, out GeoPoint3 second);
            GeoCircle3 circle = CircleThroughTwo(first, second, LeanAbout(e, first.GetVectorTo(second), Deg(70)), 30);
            GeoVector3 x = circle.Center.GetVectorTo(first).Normalize();
            GeoVector3 y = circle.Normal.CrossProduct(x);
            GeoPoint3 On(double angle) => circle.Center.Add(x.Multiply(circle.Radius * Math.Cos(angle))).Add(y.Multiply(circle.Radius * Math.Sin(angle)));
            GeoPoint3 far = circle.Center.Add(first.Add(first.GetVectorTo(second).Multiply(0.5)).GetVectorTo(circle.Center).Normalize().Multiply(circle.Radius));

            GeoArc3 both = GeoArc3.FromThreePoints(first, far, second);
            SamePointsInOrder(Along(both.Normal.CrossProduct(e.N), first, second), e.Build().GetIntersections(both, Tol), Close);

            GeoPoint3 one = Assert.Single(e.Build().GetIntersections(GeoArc3.FromThreePoints(On(-0.2), On(0.05), On(0.2)), Tol));
            Assert.True(one.DistanceTo(first) <= Close, $"{one} is {one.DistanceTo(first):E2} from {first}");

            GeoVector3 toFar = circle.Center.GetVectorTo(far).Normalize();
            GeoVector3 aside = circle.Normal.CrossProduct(toFar);
            GeoPoint3 Far(double angle) => circle.Center.Add(toFar.Multiply(circle.Radius * Math.Cos(angle))).Add(aside.Multiply(circle.Radius * Math.Sin(angle)));
            GeoArc3 neither = GeoArc3.FromThreePoints(Far(-0.2), Far(0.05), Far(0.2));
            Assert.Empty(e.Build().GetIntersections(neither, Tol));
            Assert.False(e.Build().CollidesWith(neither, Tol));
        }

        [Fact]
        public void AnEllipseTouchingTheLineThePlanesShareMeetsTheOtherThereOnly()
        {
            // The second plane holds the tangent of the first rim at t = 1.1 and leans 65 degrees about it; the second
            // ellipse, 90.8 by 54.5, crosses that line at the touching point and 150 along it, where the first rim is
            // 14.3 away. So the rims share the touching point and nothing else, whichever is asked.
            Specimen e = Tilted();
            GeoPoint3 touch = e.Rim(1.1);
            GeoVector3 line = Tangent(e, 1.1);
            Specimen other = ThroughTwo(touch, touch.Add(line.Multiply(150)), LeanAbout(e, line, Deg(65)), 0.5, 0.6, -0.2);

            GeoPoint3 asked = Assert.Single(e.Build().GetIntersections(other.Build(), Tol));
            GeoPoint3 back = Assert.Single(other.Build().GetIntersections(e.Build(), Tol));

            Assert.True(asked.DistanceTo(touch) <= Touch, $"{asked} is {asked.DistanceTo(touch):E2} from {touch}");
            Assert.True(back.DistanceTo(touch) <= Touch, $"{back} is {back.DistanceTo(touch):E2} from {touch}");
            Assert.True(e.Build().CollidesWith(other.Build(), Tol));
            Assert.True(other.Build().CollidesWith(e.Build(), Tol));
        }

        [Fact]
        public void TwoEllipsesBothTouchingTheLineThePlanesShareAtOnePointMeetThere()
        {
            // The second, 120 by 45, is built so its rim at t = 2 is the touching point with its tangent along the line.
            Specimen e = Tilted();
            GeoPoint3 touch = e.Rim(1.1);
            GeoVector3 line = Tangent(e, 1.1);
            Specimen other = TouchingLineAt(touch, line, LeanAbout(e, line, Deg(65)), 120, 45, 2.0);

            GeoPoint3 asked = Assert.Single(e.Build().GetIntersections(other.Build(), Tol));
            GeoPoint3 back = Assert.Single(other.Build().GetIntersections(e.Build(), Tol));

            Assert.True(asked.DistanceTo(touch) <= Touch, $"{asked} is {asked.DistanceTo(touch):E2} from {touch}");
            Assert.True(back.DistanceTo(touch) <= Touch, $"{back} is {back.DistanceTo(touch):E2} from {touch}");
            Assert.True(e.Build().CollidesWith(other.Build(), Tol));
        }

        #endregion

        #region Planes nearly one

        [Theory]
        [InlineData(0.001)]
        [InlineData(0.01)]
        [InlineData(0.1)]
        [InlineData(1.0)]
        public void AnEllipseInAPlaneNearlyTheSameCrossesTheOtherOnlyWhereTheyMeetOnTheSharedLine(double degrees)
        {
            // The second, 347.7 by 173.8, passes through both rim points in a plane leaning a thousandth of a degree to a
            // degree about the chord. Even at 0.001 degrees the planes are apart by every reading of the circle's test
            // (Arc3.cs:49-51): 300 sin θ = 0.0052 by the major radius, 100 sin θ = 0.0017 by the minor, and 0.0018 by the
            // rim's own stray from the plane through its centre; the rim reaches 0.0025 from the second plane. Laid flat
            // the rims cross at those two points only, at 60 and 65 degrees, and stay 15.9 apart elsewhere.
            Pair(out Specimen e, out GeoPoint3 first, out GeoPoint3 second);
            Specimen other = ThroughTwo(first, second, LeanAbout(e, first.GetVectorTo(second), Deg(degrees)), 0.3, 0.5, -0.6);

            SamePoints(new[] { first, second }, e.Build().GetIntersections(other.Build(), Tol), NearCoplanar);
            SamePoints(new[] { first, second }, other.Build().GetIntersections(e.Build(), Tol), NearCoplanar);
            Assert.True(e.Build().CollidesWith(other.Build(), Tol));
        }

        [Fact]
        public void AnEllipseFarFromTheOriginInAPlaneAThousandthOfADegreeOffCrossesTheOtherAtBothPoints()
        {
            // The pair of the test above at 0.001 degrees, moved to (30 000, 30 000, 30 000), where real models lie. Each
            // rim still stands 0.0052 off the other's plane. On 81e409e two circles placed alike, radius 300 about the same
            // centre and radius 332 through its rim points at 0.5 and 2.5 rad, leaning 0.001 degrees, give no point at all:
            // the line the planes share is found from their distances to the world origin (Intersection3.cs:438-446), and
            // a thousandth of a degree turns the rounding of 52 000 into a millimetre. Within the point tolerance here.
            Specimen near = Tilted();
            var e = new Specimen(new GeoPoint3(30000, 30000, 30000), near.U, near.V, 300, 100);
            GeoPoint3 first = e.Rim(0.6), second = e.Rim(2.8);
            Specimen other = ThroughTwo(first, second, LeanAbout(e, first.GetVectorTo(second), Deg(0.001)), 0.3, 0.5, -0.6);

            SamePoints(new[] { first, second }, e.Build().GetIntersections(other.Build(), Tol), Tol.EqualPoint);
            SamePoints(new[] { first, second }, other.Build().GetIntersections(e.Build(), Tol), Tol.EqualPoint);
        }

        [Theory]
        [InlineData(0.001)]
        [InlineData(0.01)]
        [InlineData(1.0)]
        public void AnEllipseInAPlaneNearlyTheSameCrossesACircleAndAnArcOnlyOnTheSharedLine(double degrees)
        {
            // The circle through both rim points with its centre 200 back across the chord, radius 332.3: laid flat it
            // crosses the rim at those two points only. The arc is the half of it about the first point.
            Pair(out Specimen e, out GeoPoint3 first, out GeoPoint3 second);
            GeoCircle3 circle = CircleThroughTwo(first, second, LeanAbout(e, first.GetVectorTo(second), Deg(degrees)), -200);
            GeoVector3 x = circle.Center.GetVectorTo(first).Normalize();
            GeoVector3 y = circle.Normal.CrossProduct(x);
            GeoPoint3 On(double angle) => circle.Center.Add(x.Multiply(circle.Radius * Math.Cos(angle))).Add(y.Multiply(circle.Radius * Math.Sin(angle)));

            SamePoints(new[] { first, second }, e.Build().GetIntersections(circle, Tol), NearCoplanar);

            GeoPoint3 one = Assert.Single(e.Build().GetIntersections(GeoArc3.FromThreePoints(On(-0.3), On(0.05), On(0.3)), Tol));
            Assert.True(one.DistanceTo(first) <= NearCoplanar, $"{one} is {one.DistanceTo(first):E2} from {first}");
        }

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.002)]
        public void AnEllipseInAPlaneTiltedSoTheRimStandsOffAHairCrossesTheOtherAtTheTwoPoints(double standOff)
        {
            // Leaning about the chord by θ with 300 sin θ the given stand-off. At 0.0005 the planes are one by every reading
            // and the 2D answer is laid out. At 0.002 they are apart by the major radius but one by the rim's own stray
            // across the chord, 0.00067, and by the minor radius: the readings part here, and the answer does not, the rims
            // sharing the two points either way.
            Pair(out Specimen e, out GeoPoint3 first, out GeoPoint3 second);
            Specimen other = ThroughTwo(first, second, LeanAbout(e, first.GetVectorTo(second), Math.Asin(standOff / 300)), 0.3, 0.5, -0.6);

            SamePoints(new[] { first, second }, e.Build().GetIntersections(other.Build(), Tol), NearCoplanar);
        }

        [Fact]
        public void AnEllipseInAParallelPlaneATenThousandthAwayCrossesAsIfInTheSamePlane()
        {
            // The second, 260 by 70 about (60, 20) of the frame and turned 0.4 rad, crosses the rim four times when laid
            // out; moved 0.0001 off the plane, a tenth of the tolerance, it still does, within that height.
            Specimen e = Tilted();
            GeoVector3 u = e.U.Multiply(Math.Cos(0.4)).Add(e.V.Multiply(Math.Sin(0.4)));
            GeoVector3 v = e.N.CrossProduct(u);
            var inPlane = new Specimen(e.At(60, 20, 0), u, v, 260, 70);
            var apart = new Specimen(e.At(60, 20, 0.0001), u, v, 260, 70);
            GeoPoint3[] expected = e.Flat().GetIntersections(LaidOutIn(e, inPlane), Tol).Select(e.Lift).ToArray();

            Assert.Equal(4, expected.Length);
            SamePoints(expected, e.Build().GetIntersections(apart.Build(), Tol), 0.0002);
            Assert.True(e.Build().CollidesWith(apart.Build(), Tol));
        }

        #endregion

        #region One plane

        [Fact]
        public void AnEllipseAndTheSameTurnedAQuarterAboutTheCentreCrossFourTimes()
        {
            // 300 by 100 and 300 by 100 along V: x²/300² + y²/100² = x²/100² + y²/300² gives x = ±y, and then
            // x² = 90 000 / 10, so the four points are (±94.868, ±94.868). The circle's rule for concentric circles, which
            // share no point unless they are one (Arc3.Curves.cs:53-58), does not carry over: these do cross.
            Specimen e = Tilted();
            var turned = new Specimen(e.Center, e.V, e.U.Negate(), 300, 100);
            double s = Math.Sqrt(9000);

            GeoPoint3[] crossings = e.Build().GetIntersections(turned.Build(), Tol);

            SamePoints(new[] { e.At(s, s, 0), e.At(-s, s, 0), e.At(-s, -s, 0), e.At(s, -s, 0) }, crossings, Close);
            Assert.True(e.Build().CollidesWith(turned.Build(), Tol));
        }

        [Fact]
        public void TwoCoplanarEllipsesApartCrossWhereTheLaidOutEllipsesDo()
        {
            // 200 by 80 about (150, 40), turned 0.5 rad: the 2D ellipse and a dense sample of the rim, refined by bisection,
            // both put the crossings at (280.237, 35.695) and (86.852, 95.718).
            Specimen e = Tilted();
            GeoVector3 u = e.U.Multiply(Math.Cos(0.5)).Add(e.V.Multiply(Math.Sin(0.5)));
            var other = new Specimen(e.At(150, 40, 0), u, e.N.CrossProduct(u), 200, 80);
            GeoEllipse2 laidOut = LaidOutIn(e, other);
            GeoPoint3[] byTheEllipse2 = e.Flat().GetIntersections(laidOut, Tol).Select(e.Lift).ToArray();
            GeoPoint3[] bySampling = Ellipse2Oracle.RimCrossings(e.Flat(), Ellipse2Oracle.SideOfEllipse(laidOut)).Select(e.Lift).ToArray();

            SamePoints(bySampling, byTheEllipse2, Close);
            SamePoints(bySampling, e.Build().GetIntersections(other.Build(), Tol), Close);
            SamePoints(bySampling, other.Build().GetIntersections(e.Build(), Tol), Close);
            Assert.Contains(bySampling, p => p.DistanceTo(e.At(280.237205, 35.694863, 0)) <= 1E-6);
        }

        [Fact]
        public void AnEllipseCrossesACoplanarCircleAboutItsCentreFourTimes()
        {
            // Radius 150 about the centre: x² + y² = 22 500 with x²/90 000 + y²/10 000 = 1 gives x² = 14 062.5 and
            // y² = 8 437.5, the points (±118.585, ±91.856), as the 2D ellipse's own example has them.
            Specimen e = Tilted();
            var circle = new GeoCircle3(e.Center, e.N, 150);
            double x = Math.Sqrt(14062.5), y = Math.Sqrt(8437.5);

            SamePoints(new[] { e.At(x, y, 0), e.At(-x, y, 0), e.At(-x, -y, 0), e.At(x, -y, 0) }, e.Build().GetIntersections(circle, Tol), Close);
            Assert.True(e.Build().CollidesWith(circle, Tol));

            // A quarter of that circle, from (150, 0) to (0, 150), reaches the first of them only.
            double h = 150 * Math.Sqrt(0.5);
            GeoArc3 quarter = GeoArc3.FromThreePoints(e.At(150, 0, 0), e.At(h, h, 0), e.At(0, 150, 0));
            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(quarter, Tol));
            Assert.True(point.DistanceTo(e.At(x, y, 0)) <= Close, $"{point}");
        }

        [Fact]
        public void AnEllipseLyingOnTheSameEllipseCrossesNowhereButTouches()
        {
            // The same ellipse, the same with its normal turned over, and the same with its major axis turned round.
            Specimen e = Tilted();
            foreach (GeoEllipse3 same in new[] { e.Build(), e.Flipped().Build(), new Specimen(e.Center, e.U.Negate(), e.V.Negate(), 300, 100).Build() })
            {
                Assert.Empty(e.Build().GetIntersections(same, Tol));
                Assert.True(e.Build().CollidesWith(same, Tol));
                Assert.False(e.Build().TryIntersectWith(same, out GeoPoint3[] tried, Tol));
                Assert.Empty(tried);
            }
        }

        [Fact]
        public void AnEllipseInsideAConcentricCoplanarOneTouchesNothing()
        {
            Specimen e = Tilted();
            GeoEllipse3 inner = new Specimen(e.Center, e.U, e.V, 200, 50).Build();

            Assert.Empty(e.Build().GetIntersections(inner, Tol));
            Assert.False(e.Build().CollidesWith(inner, Tol));
            Assert.False(inner.CollidesWith(e.Build(), Tol));
        }

        [Fact]
        public void AnEllipseFourTenThousandthsInsideTheRimTouchesItOnceMidway()
        {
            // 100 by 50 about (0, 49.9996): its top (0, 99.9996) is 0.0004 under the top of 300 by 100, and both rims bend
            // away from there (curvature radii 200 and 900), so they touch, at the one point midway, (0, 99.9998).
            Specimen e = Tilted();
            Specimen inner = new Specimen(e.At(0, 50 - 0.0004, 0), e.U, e.V, 100, 50);

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(inner.Build(), Tol));

            e.Read(point, out double u, out double v, out double h);
            Assert.True(Math.Abs(u) <= 1E-3, $"u = {u}");
            Assert.Equal(99.9998, v, 6);
            Assert.True(Math.Abs(h) <= 1E-9, $"h = {h}");
            Assert.True(e.Build().CollidesWith(inner.Build(), Tol));
            Assert.Single(inner.Build().GetIntersections(e.Build(), Tol));
        }

        [Fact]
        public void AnEllipseFourThousandthsInsideTheRimTouchesNothing()
        {
            // The same, 0.004 under, four times the point tolerance.
            Specimen e = Tilted();
            GeoEllipse3 inner = new Specimen(e.At(0, 50 - 0.004, 0), e.U, e.V, 100, 50).Build();

            Assert.Empty(e.Build().GetIntersections(inner, Tol));
            Assert.False(e.Build().CollidesWith(inner, Tol));
        }

        [Fact]
        public void AnEllipseFourTenThousandthsOutsideTheRimTouchesItOnceMidway()
        {
            // 100 by 50 about (0, 150.0004) sits on top of 300 by 100 with a gap of 0.0004: one point, (0, 100.0002).
            Specimen e = Tilted();
            Specimen above = new Specimen(e.At(0, 150 + 0.0004, 0), e.U, e.V, 100, 50);

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(above.Build(), Tol));

            e.Read(point, out double u, out double v, out _);
            Assert.True(Math.Abs(u) <= 1E-3, $"u = {u}");
            Assert.Equal(100.0002, v, 6);
            Assert.True(e.Build().CollidesWith(above.Build(), Tol));
        }

        [Fact]
        public void TwoCirclesAsEllipsesFourTenThousandthsApartTouchOnceMidway()
        {
            // Radius 250 about the origin and radius 100 about (0, 350.0004), in z = 0: a gap of 0.0004 between the rims.
            // GeoCircle2 gives the one touching point, (0, 250.0002); GeoCircle3 gives none on 81e409e, its radical line
            // read through the length-scaled band of Arc3.cs:95 (only the touch from inside was mended, Arc3.cs:60-80).
            // The ellipses are held to the touch, as the 2D ellipse and the 2D circle give it.
            GeoEllipse3 big = GeoEllipse3.FromCircle(new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 250));
            GeoEllipse3 small = GeoEllipse3.FromCircle(new GeoCircle3(new GeoPoint3(0, 350.0004, 0), GeoVector3.ZAxis, 100));

            GeoPoint3 point = Assert.Single(big.GetIntersections(small, Tol));

            Assert.True(Math.Abs(point.X) <= 1E-3, $"{point}");
            Assert.Equal(250.0002, point.Y, 6);
            Assert.True(big.CollidesWith(small, Tol));
        }

        #endregion
    }
}
