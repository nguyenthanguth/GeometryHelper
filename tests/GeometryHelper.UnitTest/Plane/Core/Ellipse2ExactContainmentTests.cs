using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// What an ellipse reads from a polygon, a rectangle, a curved loop and a curved edge when its centre, or an end of the
    /// edge, stands within the point tolerance of the shape: the centre is judged with no slack, so a tiny ellipse beside a
    /// side is the gap away, and an edge ending on the rim is nought away.
    /// </summary>
    public class Ellipse2ExactContainmentTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>An ellipse 0.0002 by 0.0001 whose centre stands 0.0004 right of x = 100, so that its rim is 0.0002 off.</summary>
        private static GeoEllipse2 Speck() => new GeoEllipse2(new GeoPoint2(100.0004, 50), GeoVector2.XAxis, 0.0002, 0.0001);

        [Fact]
        public void ATinyEllipseWhoseCentreIsInsideTheToleranceOfAPolygon_IsTheGapFromIt()
        {
            var polygon = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

            Assert.InRange(Speck().DistanceTo(polygon, Tol), 0.0002 - 1E-11, 0.0002 + 1E-11);
            Assert.True(Speck().CollidesWith(polygon, Tol));
        }

        [Fact]
        public void ATinyEllipseWhoseCentreIsInsideTheToleranceOfARectangle_IsTheGapFromIt()
        {
            var rect = new GeoRectangle2(new GeoPoint2(50, 50), 100, 100, 0);

            Assert.InRange(Speck().DistanceTo(rect, Tol), 0.0002 - 1E-11, 0.0002 + 1E-11);
            Assert.True(Speck().CollidesWith(rect, Tol));
        }

        [Fact]
        public void ATinyEllipseWhoseCentreIsInsideTheToleranceOfACurvedLoop_IsTheGapFromIt()
        {
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new[] { 0.0, 0, 0, 0 });

            Assert.InRange(Speck().DistanceTo(loop, Tol), 0.0002 - 1E-11, 0.0002 + 1E-11);
            Assert.True(Speck().CollidesWith(loop, Tol));
        }

        [Fact]
        public void ATinyEllipseBeyondTheToleranceOfAPolygon_IsTheGapFromItAndDoesNotCollide()
        {
            // A control: 0.0008 right of the side, at a tolerance of 0.0001, the rim is 0.0006 off and past the tolerance.
            var polygon = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));
            var speck = new GeoEllipse2(new GeoPoint2(100.0008, 50), GeoVector2.XAxis, 0.0002, 0.0001);
            var tight = new Tolerance(0.0001, Tolerance.DefaultEqualVector);

            Assert.InRange(speck.DistanceTo(polygon, tight), 0.0006 - 1E-11, 0.0006 + 1E-11);
            Assert.False(speck.CollidesWith(polygon, tight));
        }

        [Fact]
        public void ACurvedEdgeBowingLessThanItsRoundingAndStandingHalfAToleranceOff_IsThatFarAway()
        {
            // From (60.0005, -500) to (60.0005, 500) with a bulge of 1E-12: a radius of 2.5E14, whose arc the ellipse used to
            // read as touching it; read as its chord it is 0.0005 off the end (60, 0).
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 60, 25);
            var edge = new GeoEdge2(new GeoPoint2(60.0005, -500), new GeoPoint2(60.0005, 500), 1E-12);

            Assert.InRange(ellipse.DistanceTo(edge, Tol), 0.0005 - 1E-11, 0.0005 + 1E-11);
            Assert.True(ellipse.CollidesWith(edge, Tol));
        }

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector);

        /// <summary>The left half of the circle of radius r whose leftmost point, (60.0005, 0), is 0.0005 off the end of the ellipse 60 by 25.</summary>
        private static GeoArc2 TinyArc(double r) => new GeoArc2(new GeoPoint2(60.0005 + r, 0), r, Math.PI / 2, 3 * Math.PI / 2);

        [Theory]
        [InlineData(0.0008, 0.001)]
        [InlineData(0.05, 0.1)]
        public void AnArcNoLargerThanTheToleranceHalfAToleranceOffTheRim_IsThatFarAwayAndCollides(double radius, double equalPoint)
        {
            // GetParameterAtPoint snaps a point within the tolerance of the arc's centre to its start, so the arc used to be
            // weighed at its two ends only: 0.0013 for the first, 0.0506 for the second.
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 60, 25);
            Tolerance tol = At(equalPoint);

            Assert.InRange(ellipse.DistanceTo(TinyArc(radius), tol), 0.0005 - 1E-11, 0.0005 + 1E-11);
            Assert.True(ellipse.CollidesWith(TinyArc(radius), tol));
        }

        [Fact]
        public void ATinyArcAsAChainEdge_IsThatFarAwayAsTheArcIs()
        {
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 60, 25);
            var edge = new GeoEdge2(TinyArc(0.0008));

            Assert.InRange(ellipse.DistanceTo(edge, At(0.001)), 0.0005 - 1E-11, 0.0005 + 1E-11);
        }

        [Fact]
        public void TheShortestLineToATinyArc_JoinsTheRimToTheNearestPointOfTheArc()
        {
            // Not the touch rule, which is unchanged: an arc 0.003 off at a tolerance of 0.001 does not touch, and the line is
            // the one the gap is read off, ending at the arc's leftmost point (60.003, 0), where the arc's ends alone, 0.0008
            // up and down, used to give a line of 0.0038.
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 60, 25);
            var arc = new GeoArc2(new GeoPoint2(60.0038, 0), 0.0008, Math.PI / 2, 3 * Math.PI / 2);
            GeoLine2 line = ellipse.GetShortestLineTo(arc, At(0.001));

            Assert.InRange(line.Length, 0.003 - 1E-11, 0.003 + 1E-11);
            Assert.InRange(line.EndPoint.X, 60.003 - 1E-9, 60.003 + 1E-9);
        }

        [Fact]
        public void ACurvedEdgeStartingOnTheRimAndBowingAwayFromIt_IsNoughtAway()
        {
            // The edge (60, 0) to (84.31, -3.9) with a bulge of 0.05 starts at the end of the ellipse's major axis. The arc
            // places that end through its centre a few parts in 1E15 off; the edge's own end point is the one weighed that
            // reads exactly 0, where the arc alone read 1.4E-14.
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 60, 25);
            var edge = new GeoEdge2(new GeoPoint2(60, 0), new GeoPoint2(84.31, -3.9), 0.05);

            Assert.Equal(0.0, ellipse.DistanceTo(edge, Tol));
            Assert.True(ellipse.CollidesWith(edge, Tol));
        }

        [Fact]
        public void TheCollisionWithAFace_IsTheDistanceWithinThePointTolerance()
        {
            // 600 ellipses, in the hole of a square plate, outside it, and across its rims, at four tolerances.
            var plate = new GeoFace2(
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200), new GeoPoint2(0, 200)),
                new[] { new GeoPolygon2(new GeoPoint2(60, 60), new GeoPoint2(140, 60), new GeoPoint2(140, 140), new GeoPoint2(60, 140)) });
            var random = new Random(4172);
            int collided = 0;
            int apart = 0;

            for (int i = 0; i < 600; i++)
            {
                double a = 0.5 + 40 * random.NextDouble();
                double b = a * (0.05 + 0.95 * random.NextDouble());
                double turn = Math.PI * random.NextDouble();
                var ellipse = new GeoEllipse2(
                    new GeoPoint2(-40 + 280 * random.NextDouble(), -40 + 280 * random.NextDouble()),
                    new GeoVector2(Math.Cos(turn), Math.Sin(turn)), a, b);
                Tolerance tol = At(new[] { 1E-4, 1E-3, 1E-2, 0.5 }[i % 4]);

                bool collides = ellipse.CollidesWith(plate, tol);
                Assert.Equal(ellipse.DistanceTo(plate, tol) <= tol.EqualPoint, collides);

                if (collides)
                {
                    collided++;
                }
                else
                {
                    apart++;
                }
            }

            Assert.True(collided > 100 && apart > 100, $"{collided} collided, {apart} apart");
        }

        /// <summary>The triangle (0, 0), (0, 100), (0.08, 100): its top is 0.08 wide, less than a tolerance of 0.1, and its left side is the line x = 0.</summary>
        private static GeoTriangle2 Sliver() => new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(0, 100), new GeoPoint2(0.08, 100));

        /// <summary>The ellipse 30 by 10 whose right end, (-0.15, 50), is 0.15 off the left side of the sliver; read as its longest side, (0, 0) to (0.08, 100), the triangle stands 0.19 away.</summary>
        private static GeoEllipse2 BesideTheSliver() => new GeoEllipse2(new GeoPoint2(-30.15, 50), GeoVector2.XAxis, 30, 10);

        [Fact]
        public void ASliverTriangleWithASideShorterThanTheTolerance_IsMeasuredBySidesNotAsItsLongestSide()
        {
            Tolerance tol = At(0.1);
            GeoEllipse2 ellipse = BesideTheSliver();

            Assert.InRange(ellipse.DistanceTo(Sliver(), tol), 0.15 - 1E-9, 0.15 + 1E-9);

            GeoLine2 line = ellipse.GetShortestLineTo(Sliver(), tol);
            Assert.InRange(line.Length, 0.15 - 1E-9, 0.15 + 1E-9);
            Assert.InRange(line.EndPoint.X, -1E-9, 1E-9);
            Assert.InRange(line.EndPoint.Y, 50 - 1E-9, 50 + 1E-9);
        }

        [Theory]
        [InlineData(0.14, false)]
        [InlineData(0.17, true)]
        public void ASliverTriangleWithASideShorterThanTheTolerance_CollidesWhereItsSidesComeWithinTheTolerance(double equalPoint, bool expected)
        {
            // 0.15 away by its sides; the longest side alone, 0.19 away, would not collide at 0.17.
            Assert.Equal(expected, BesideTheSliver().CollidesWith(Sliver(), At(equalPoint)));
        }

        [Fact]
        public void ASliverTriangleWithASideShorterThanTheTolerance_IsCrossedAsThePolygonOfItsCornersIs()
        {
            Tolerance tol = At(0.1);
            // Read by its sides, the left side x = 0 is crossed at (0, 47.44) and (0, 52.56); read as its longest side alone,
            // the crossings stood 0.04 to the right, on the line to (0.08, 100).
            var ellipse = new GeoEllipse2(new GeoPoint2(-29, 50), GeoVector2.XAxis, 30, 10);
            var polygon = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(0, 100), new GeoPoint2(0.08, 100));

            GeoPoint2[] crossings = ellipse.GetIntersections(Sliver(), tol);
            GeoPoint2[] expected = ellipse.GetIntersections(polygon, tol);

            Assert.Equal(2, crossings.Length);
            Assert.Equal(2, expected.Length);
            Assert.InRange(crossings[0].X, -1E-9, 1E-9);
            Assert.InRange(crossings[1].X, -1E-9, 1E-9);
            Assert.InRange(crossings[0].Y, expected[0].Y - 1E-9, expected[0].Y + 1E-9);
            Assert.InRange(crossings[1].Y, expected[1].Y - 1E-9, expected[1].Y + 1E-9);
        }

        [Fact]
        public void ACurvedEdgeBowingLessThanItsRoundingAndEndingOnTheRim_IsNoughtAway()
        {
            // A control, which 75d1767 already read as 0: from (60, -50) to (60, 50) with a bulge of 1E-9, on a radius of
            // 2.5E10, read as the chord, which touches the ellipse 60 by 25 at its end (60, 0).
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 60, 25);
            var edge = new GeoEdge2(new GeoPoint2(60, -50), new GeoPoint2(60, 50), 1E-9);

            Assert.Equal(0.0, ellipse.DistanceTo(edge, Tol));
            Assert.True(ellipse.CollidesWith(edge, Tol));
        }
    }
}
