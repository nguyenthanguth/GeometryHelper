using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The coder's smoke checks of where the rim of an ellipse in space crosses other shapes and whether it touches them:
    /// the measured examples of the documentation, the circle's answers when the radii are equal, and the cases the work
    /// turned up.
    /// </summary>
    public class GeoEllipse3QuerySmokeTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static readonly GeoEllipse3 Plan = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 300, 100);

        private static GeoVector3 RandomUnit(Random random)
        {
            while (true)
            {
                var v = new GeoVector3(2 * random.NextDouble() - 1, 2 * random.NextDouble() - 1, 2 * random.NextDouble() - 1);
                double length = v.Length;

                if (length > 0.1 && length <= 1.0)
                {
                    return v.Multiply(1.0 / length);
                }
            }
        }

        private static GeoVector3 Square(GeoVector3 normal, Random random)
        {
            GeoVector3 v = RandomUnit(random);
            v = v.Subtract(normal.Multiply(v.DotProduct(normal)));

            return v.Multiply(1.0 / v.Length);
        }

        private static GeoPoint3 RandomPoint(Random random, double reach)
        {
            return new GeoPoint3(reach * (2 * random.NextDouble() - 1), reach * (2 * random.NextDouble() - 1), reach * (2 * random.NextDouble() - 1));
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual, double within)
        {
            Assert.True(expected.DistanceTo(actual) <= within, $"{expected} against {actual}");
        }

        private static void Same(GeoPoint3[] expected, GeoPoint3[] actual, double within)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Near(expected[i], actual[i], within);
            }
        }

        [Fact]
        public void ThePlaneSquareToTheMajorAxisCrossesTheRimTwiceInOrderAlongTheLineThePlanesShare()
        {
            GeoPoint3[] points = Plan.GetIntersections(new GeoPlane3(new GeoPoint3(150, 0, 0), GeoVector3.XAxis), Tol);
            double y = 100 * Math.Sqrt(0.75);

            Same(new[] { new GeoPoint3(150, -y, 0), new GeoPoint3(150, y, 0) }, points, 1E-9);
            Assert.True(Plan.TryIntersectWith(new GeoPlane3(new GeoPoint3(150, 0, 0), GeoVector3.XAxis), out _, Tol));
        }

        [Fact]
        public void TheEllipseLyingInAPlaneCrossesItNowhereAndTouchesIt()
        {
            var plan = new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);

            Assert.Empty(Plan.GetIntersections(plan, Tol));
            Assert.False(Plan.TryIntersectWith(plan, out _, Tol));
            Assert.True(Plan.CollidesWith(plan, Tol));
            Assert.False(Plan.CollidesWith(new GeoPlane3(new GeoPoint3(0, 0, 1), GeoVector3.ZAxis), Tol));
        }

        [Fact]
        public void ASegmentThroughTheMiddleOfTheDiscTouchesNothingAndOneThroughTheRimMeetsItWhereItPierces()
        {
            Assert.False(Plan.CollidesWith(new GeoLine3(new GeoPoint3(0, 0, -10), new GeoPoint3(0, 0, 10)), Tol));

            Same(new[] { new GeoPoint3(300, 0, 0) }, Plan.GetIntersections(new GeoLine3(new GeoPoint3(295, 0, -10), new GeoPoint3(305, 0, 10)), Tol), 1E-9);

            double x = 300 * Math.Sqrt(0.75);
            Same(
                new[] { new GeoPoint3(-x, 50, 0), new GeoPoint3(x, 50, 0) },
                Plan.GetIntersections(new GeoLine3(new GeoPoint3(-400, 50, 0), new GeoPoint3(400, 50, 0)), Tol),
                1E-9);
        }

        [Fact]
        public void ARayInThePlaneFromTheCentreCrossesTheRimOnceAhead()
        {
            Same(new[] { new GeoPoint3(300, 0, 0) }, Plan.GetIntersections(new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis), Tol), 1E-9);
        }

        [Fact]
        public void TheCircleOfRadius150AboutTheCentreCrossesTheRimAtFourPointsInTheOrderOfTheEccentricAngle()
        {
            GeoPoint3[] points = Plan.GetIntersections(new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 150), Tol);

            Assert.Equal(4, points.Length);
            Near(new GeoPoint3(118.585, 91.856, 0), points[0], 1E-3);
            Near(new GeoPoint3(-118.585, -91.856, 0), points[2], 1E-3);
        }

        [Fact]
        public void TheSameEllipseTurnedAQuarterCrossesItAtFourPointsAndTheSameEllipseCrossesItNowhereButTouches()
        {
            var turned = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.YAxis, 300, 100);
            GeoPoint3[] points = Plan.GetIntersections(turned, Tol);
            double c = 300 * 100 / Math.Sqrt(300 * 300 + 100 * 100);

            Assert.Equal(4, points.Length);
            Near(new GeoPoint3(c, c, 0), points[0], 1E-9);
            Assert.Empty(Plan.GetIntersections(Plan, Tol));
            Assert.True(Plan.CollidesWith(Plan, Tol));
        }

        [Fact]
        public void TheRimsAreWhatCountSoAShapeInsideTheDiscTouchesNothing()
        {
            Assert.False(Plan.CollidesWith(new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 50), Tol));
            Assert.False(Plan.CollidesWith(new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 150, 50), Tol));
        }

        [Fact]
        public void AFlatRegionHoldingTheWholeRimInItsPlaneTouchesItAndCrossesItNowhere()
        {
            var square = new GeoPolygon3(new GeoPoint3(-400, -400, 0), new GeoPoint3(400, -400, 0), new GeoPoint3(400, 400, 0), new GeoPoint3(-400, 400, 0));
            var triangle = new GeoTriangle3(new GeoPoint3(-1000, -1000, 0), new GeoPoint3(1000, -1000, 0), new GeoPoint3(0, 1000, 0));

            Assert.Empty(Plan.GetIntersections(square, Tol));
            Assert.True(Plan.CollidesWith(square, Tol));
            Assert.Empty(Plan.GetIntersections(triangle, Tol));
            Assert.True(Plan.CollidesWith(triangle, Tol));
        }

        [Fact]
        public void TheRimLeavesABoxThroughTheTwoFacesSquareToItsMajorAxisAndARimInsideTouchesIt()
        {
            var box = new GeoObb3(GeoPoint3.Origin, 200, 300, 200);
            double y = 100 * Math.Sqrt(1 - 1.0 / 9);
            var expected = new[] { new GeoPoint3(100, -y, 0), new GeoPoint3(100, y, 0), new GeoPoint3(-100, y, 0), new GeoPoint3(-100, -y, 0) };

            Same(expected, Plan.GetIntersections(box, Tol), 1E-9);
            Same(expected, Plan.GetIntersections(box.ToSolid(), Tol), 1E-9);
            Same(expected, Plan.GetIntersections(new GeoAabb3(new GeoPoint3(-100, -150, -100), new GeoPoint3(100, 150, 100)), Tol), 1E-9);

            var small = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 30, 10);

            Assert.Empty(small.GetIntersections(box.ToSolid(), Tol));
            Assert.True(small.CollidesWith(box.ToSolid(), Tol));
            Assert.True(small.CollidesWith(box, Tol));
        }

        [Fact]
        public void AChainWithAStraightLegThroughTheRimMeetsItThere()
        {
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(300, 0, -10), new GeoPoint3(300, 0, 10), new GeoPoint3(400, 0, 10) });

            Same(new[] { new GeoPoint3(300, 0, 0) }, Plan.GetIntersections(chain, Tol), 1E-9);
            Assert.True(Plan.CollidesWith(chain, Tol));
            Assert.True(Plan.CollidesWith(chain.GetEdgeAt(0), Tol));
            Assert.False(Plan.CollidesWith(chain.GetEdgeAt(1), Tol));
        }

        /// <summary>
        /// The line the two planes share cuts a chord 1.6 long only 0.0004 deep into the rim. Read as a segment in the plane
        /// is read, by how deep it cuts, the chord was one touching point at its middle, 0.8 from either crossing, and the
        /// circle standing on one of them was found to cross the ellipse nowhere.
        /// </summary>
        [Fact]
        public void AShallowChordOnTheLineThePlanesShareStillGivesTheCrossingAnUprightCircleStandsOn()
        {
            double half = 0.8;
            double y = 100 * Math.Sqrt(1 - (half / 300) * (half / 300));
            var circle = new GeoCircle3(new GeoPoint3(half, y, 50), GeoVector3.YAxis, 50);

            Same(new[] { new GeoPoint3(half, y, 0) }, Plan.GetIntersections(circle, Tol), 1E-9);
            Assert.True(Plan.CollidesWith(circle, Tol));
            Assert.Equal(2, Plan.GetIntersections(new GeoPlane3(new GeoPoint3(0, y, 0), GeoVector3.YAxis), Tol).Length);
        }

        [Fact]
        public void TwoCirclesInPlanesATenThousandthApartCrossWhereTheRimsCross()
        {
            var other = new GeoCircle3(new GeoPoint3(300, 0, 1E-4), GeoVector3.ZAxis, 100);
            GeoPoint3[] points = Plan.GetIntersections(other, Tol);

            Assert.Equal(2, points.Length);

            foreach (GeoPoint3 point in points)
            {
                Assert.True(Plan.GetClosestPointOnBoundary(point).DistanceTo(point) <= 1E-9);
                Assert.True(other.GetClosestPointOnBoundary(point).DistanceTo(point) <= 2E-4);
            }
        }

        [Fact]
        public void AnEllipseMadeFromACircleCrossesWhatTheCircleCrossesInTheSameOrder()
        {
            var random = new Random(23);

            for (int i = 0; i < 200; i++)
            {
                double radius = 1 + 999 * random.NextDouble();
                var circle = new GeoCircle3(RandomPoint(random, 2000), RandomUnit(random), radius);
                GeoEllipse3 ellipse = GeoEllipse3.FromCircle(circle);
                double within = 1E-11 * (radius + circle.Center.DistanceTo(GeoPoint3.Origin));
                GeoPoint3 rim = circle.GetPointAtAngle(2 * Math.PI * random.NextDouble());

                var plane = new GeoPlane3(rim, RandomUnit(random));
                Same(circle.GetIntersections(plane, Tol), ellipse.GetIntersections(plane, Tol), within);
                Assert.Equal(circle.CollidesWith(plane, Tol), ellipse.CollidesWith(plane, Tol));

                GeoVector3 normal = RandomUnit(random);
                double other = 1 + 999 * random.NextDouble();
                var upright = new GeoCircle3(rim.Add(Square(normal, random).Multiply(other)), normal, other);
                Same(circle.GetIntersections(upright, Tol), ellipse.GetIntersections(upright, Tol), within);
                Same(circle.GetIntersections(upright, Tol), ellipse.GetIntersections(GeoEllipse3.FromCircle(upright), Tol), within);
                Assert.Equal(circle.CollidesWith(upright, Tol), ellipse.CollidesWith(upright, Tol));

                var flat = new GeoCircle3(circle.Center.Add(Square(circle.Normal, random).Multiply(radius * random.NextDouble())), circle.Normal, radius * (0.5 + random.NextDouble()));
                Same(circle.GetIntersections(flat, Tol), ellipse.GetIntersections(flat, Tol), within);
                Assert.Equal(circle.CollidesWith(flat, Tol), ellipse.CollidesWith(flat, Tol));

                GeoVector3 across = Square(normal, random);
                GeoVector3 up = normal.CrossProduct(across);
                var triangle = new GeoTriangle3(
                    rim.Add(across.Multiply(-radius)).Add(up.Multiply(-radius)),
                    rim.Add(across.Multiply(radius)).Add(up.Multiply(-0.5 * radius)),
                    rim.Add(up.Multiply(radius)));
                Same(circle.GetIntersections(triangle, Tol), ellipse.GetIntersections(triangle, Tol), within);
                Assert.Equal(circle.CollidesWith(triangle, Tol), ellipse.CollidesWith(triangle, Tol));

                var box = new GeoObb3(circle.Center.Add(RandomUnit(random).Multiply(radius * random.NextDouble())), radius, 1.5 * radius, 0.7 * radius, RandomUnit(random), RandomUnit(random));
                Same(circle.GetIntersections(box, Tol), ellipse.GetIntersections(box, Tol), within);
                Assert.Equal(circle.CollidesWith(box, Tol), ellipse.CollidesWith(box, Tol));
            }
        }

        [Fact]
        public void ANullShapeIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Plan.GetIntersections((GeoPolygon3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => Plan.GetIntersections((GeoFace3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => Plan.CollidesWith((GeoSolid3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => Plan.CollidesWith((GeoPolylineArc3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => Plan.GetIntersections((GeoPolygonArc3)null, Tol));
        }
    }
}
