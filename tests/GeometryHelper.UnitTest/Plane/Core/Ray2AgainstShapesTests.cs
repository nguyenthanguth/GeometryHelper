using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.Ray2Shapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The ray in the plane asked the five queries about each of the eleven bounded shapes, with the numbers worked out
    /// by hand. Each shape is met by a ray through it, one passing beside it, one starting past it and pointing away, one
    /// starting inside or on it, and one passing four tenths from a corner.
    /// </summary>
    /// <remarks>
    /// The readings are <see cref="GeoLine2"/>'s (D8): a closed shape is filled for the distance and the collision, so a
    /// ray starting inside is nought from it and touches it; the shortest line runs boundary to boundary, so it is the
    /// zero-length line where the ray leaves the outline; the crossings are those of the boundary, in the order the
    /// segment gives them. That order runs along the ray against a circle or an arc, and edge by edge against everything
    /// else; it is checked against a segment drawn ten thousand along the ray, which shares no code with the ray.
    /// Four tenths sits between the two tolerances, a thousandth and 1, so a member that drops the tolerance it is given
    /// answers wrongly under one of them. The shapes are those of <see cref="Ray2Shapes"/>.
    /// </remarks>
    public class Ray2AgainstShapesTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        #region Asking and judging

        /// <summary>The five answers about one pair, under one tolerance; the ones D4 does not offer stay null.</summary>
        private sealed class Answers
        {
            internal bool Collides;
            internal double Distance;
            internal GeoPoint2[] Crossings;
            internal bool? Tried;
            internal GeoPoint2[] TriedPoints;
            internal GeoLine2 Shortest;
        }

        private static Answers Ask(GeoRay2 r, GeoArc2 s, Tolerance t) { bool hit = r.TryIntersectWith(s, out GeoPoint2[] p, t); return new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Tried = hit, TriedPoints = p, Shortest = r.GetShortestLineTo(s, t) }; }

        private static Answers Ask(GeoRay2 r, GeoCircle2 s, Tolerance t) { bool hit = r.TryIntersectWith(s, out GeoPoint2[] p, t); return new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Tried = hit, TriedPoints = p, Shortest = r.GetShortestLineTo(s, t) }; }

        private static Answers Ask(GeoRay2 r, GeoEdge2 s, Tolerance t) => new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Shortest = r.GetShortestLineTo(s, t) };

        private static Answers Ask(GeoRay2 r, GeoFace2 s, Tolerance t) { bool hit = r.TryIntersectWith(s, out GeoPoint2[] p, t); return new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Tried = hit, TriedPoints = p, Shortest = r.GetShortestLineTo(s, t) }; }

        private static Answers Ask(GeoRay2 r, GeoPolygon2 s, Tolerance t) => new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Shortest = r.GetShortestLineTo(s, t) };

        private static Answers Ask(GeoRay2 r, GeoPolygonArc2 s, Tolerance t) { bool hit = r.TryIntersectWith(s, out GeoPoint2[] p, t); return new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Tried = hit, TriedPoints = p, Shortest = r.GetShortestLineTo(s, t) }; }

        private static Answers Ask(GeoRay2 r, GeoPolyline2 s, Tolerance t) { bool hit = r.TryIntersectWith(s, out GeoPoint2[] p, t); return new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Tried = hit, TriedPoints = p, Shortest = r.GetShortestLineTo(s, t) }; }

        private static Answers Ask(GeoRay2 r, GeoPolylineArc2 s, Tolerance t) { bool hit = r.TryIntersectWith(s, out GeoPoint2[] p, t); return new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Tried = hit, TriedPoints = p, Shortest = r.GetShortestLineTo(s, t) }; }

        private static Answers Ask(GeoRay2 r, GeoRectangle2 s, Tolerance t) { bool hit = r.TryIntersectWith(s, out GeoPoint2[] p, t); return new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Tried = hit, TriedPoints = p, Shortest = r.GetShortestLineTo(s, t) }; }

        private static Answers Ask(GeoRay2 r, GeoTriangle2 s, Tolerance t) { bool hit = r.TryIntersectWith(s, out GeoPoint2[] p, t); return new Answers { Collides = r.CollidesWith(s, t), Distance = r.DistanceTo(s, t), Crossings = r.GetIntersections(s, t), Tried = hit, TriedPoints = p, Shortest = r.GetShortestLineTo(s, t) }; }

        private static void AssertNear(GeoPoint2 expected, GeoPoint2 actual, double within = 1E-9)
        {
            Assert.True(expected.DistanceTo(actual) <= within, actual + " is not " + expected);
        }

        private static void AssertSamePoints(GeoPoint2[] expected, GeoPoint2[] actual, double within = 1E-9)
        {
            Assert.NotNull(actual);
            Assert.Equal(expected.Length, actual.Length);

            foreach (GeoPoint2 point in expected)
            {
                Assert.Contains(actual, p => p.DistanceTo(point) <= within);
            }
        }

        private static void AssertInOrder(GeoPoint2[] expected, GeoPoint2[] actual, double within = 1E-9)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                AssertNear(expected[i], actual[i], within);
            }
        }

        /// <summary>
        /// The ray runs into the shape's boundary at these points: it touches, is nought away, crosses there, and its
        /// shortest line has no length and sits at one of them.
        /// </summary>
        private static void AssertCrosses(Answers a, params GeoPoint2[] crossings)
        {
            Assert.True(a.Collides);
            Assert.Equal(0.0, a.Distance);
            AssertSamePoints(crossings, a.Crossings);

            if (a.Tried.HasValue)
            {
                Assert.True(a.Tried.Value);
                AssertSamePoints(crossings, a.TriedPoints);
            }

            Assert.InRange(a.Shortest.Length, 0.0, 1E-9);
            Assert.Contains(crossings, p => p.DistanceTo(a.Shortest.StartPoint) <= 1E-9);
        }

        /// <summary>
        /// The ray misses the shape by this much, along the line from <paramref name="onRay"/> to <paramref name="onShape"/>.
        /// </summary>
        private static void AssertApart(Answers a, double distance, GeoPoint2 onRay, GeoPoint2 onShape)
        {
            Assert.False(a.Collides);
            Assert.Equal(distance, a.Distance, 9);

            if (a.Crossings != null)
            {
                Assert.Empty(a.Crossings);
            }

            if (a.Tried.HasValue)
            {
                Assert.False(a.Tried.Value);
                Assert.Empty(a.TriedPoints);
            }

            AssertNear(onRay, a.Shortest.StartPoint);
            AssertNear(onShape, a.Shortest.EndPoint);
        }

        #endregion

        #region The square as a polygon

        [Fact]
        public void ARayFromMinusFiftyFiftyAlongX_CrossesTheSquareAtNoughtFiftyAndAHundredFifty()
        {
            // The measured example of D16.
            var ray = new GeoRay2(P(-50, 50), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, Square(), Strict), P(0, 50), P(100, 50));
            AssertInOrder(FarAlong(ray, GeoVector2.XAxis).GetIntersections(Square(), Strict), ray.GetIntersections(Square(), Strict));
        }

        [Fact]
        public void ARayPassingTheTopRightCorner_IsTenRootTwoFromIt()
        {
            // Along (-1, 1) from (220, 0) the ray runs on x + y = 220; the corner (100, 100) is 20/√2 from it, its foot
            // (110, 110), 110√2 along. Every other point of the square has x + y below 200, so is further.
            AssertApart(Ask(new GeoRay2(P(220, 0), V(-1, 1)), Square(), Strict), 10 * Root2, P(110, 110), P(100, 100));
        }

        [Fact]
        public void ASquareBehindTheOrigin_IsMeasuredFromTheOrigin()
        {
            AssertApart(Ask(new GeoRay2(P(150, 50), GeoVector2.XAxis), Square(), Strict), 50, P(150, 50), P(100, 50));
        }

        [Fact]
        public void ARayFromInsideTheSquare_IsNoughtFromIt_AndItsShortestLineIsWhereItLeaves()
        {
            // D8: filled for the distance and the collision; boundary to boundary for the line, which is the exit (100, 40).
            AssertCrosses(Ask(new GeoRay2(P(30, 40), GeoVector2.XAxis), Square(), Strict), P(100, 40));
        }

        [Fact]
        public void ARayFourTenthsPastACornerOfTheSquare_TouchesItWithinOneButNotWithinAThousandth()
        {
            GeoRay2 ray = Grazing(P(100, 100), U(1, -1), U(1, 1), out GeoPoint2 passing);

            AssertApart(Ask(ray, Square(), Strict), 0.4, passing, P(100, 100));
            Assert.True(ray.CollidesWith(Square(), Loose));
        }

        #endregion

        #region The square as a rectangle

        [Fact]
        public void ARayFromMinusFiftyFiftyAlongX_CrossesTheRectangleAtNoughtFiftyAndAHundredFifty()
        {
            var ray = new GeoRay2(P(-50, 50), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, Rectangle(), Strict), P(0, 50), P(100, 50));
            AssertInOrder(FarAlong(ray, GeoVector2.XAxis).GetIntersections(Rectangle(), Strict), ray.GetIntersections(Rectangle(), Strict));
        }

        [Fact]
        public void ARayPassingTheRectanglesTopRightCorner_IsTenRootTwoFromIt()
        {
            AssertApart(Ask(new GeoRay2(P(220, 0), V(-1, 1)), Rectangle(), Strict), 10 * Root2, P(110, 110), P(100, 100));
        }

        [Fact]
        public void ARectangleBehindTheOrigin_IsMeasuredFromTheOrigin()
        {
            AssertApart(Ask(new GeoRay2(P(150, 50), GeoVector2.XAxis), Rectangle(), Strict), 50, P(150, 50), P(100, 50));
        }

        [Fact]
        public void ARayFromInsideTheRectangle_IsNoughtFromIt_AndItsShortestLineIsWhereItLeaves()
        {
            AssertCrosses(Ask(new GeoRay2(P(30, 40), GeoVector2.XAxis), Rectangle(), Strict), P(100, 40));
        }

        [Fact]
        public void ARayFourTenthsPastACornerOfTheRectangle_TouchesItWithinOneButNotWithinAThousandth()
        {
            GeoRay2 ray = Grazing(P(100, 100), U(1, -1), U(1, 1), out GeoPoint2 passing);

            AssertApart(Ask(ray, Rectangle(), Strict), 0.4, passing, P(100, 100));
            Assert.True(ray.CollidesWith(Rectangle(), Loose));
        }

        #endregion

        #region The square with a hole, as a face

        [Fact]
        public void ARayAcrossThePlate_CrossesTheOutlineAndTheHole()
        {
            // y = 50 crosses the outline at x = 0 and 100 and the hole at 30 and 70.
            var ray = new GeoRay2(P(-50, 50), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, Plate(), Strict), P(0, 50), P(30, 50), P(70, 50), P(100, 50));
            AssertInOrder(FarAlong(ray, GeoVector2.XAxis).GetIntersections(Plate(), Strict), ray.GetIntersections(Plate(), Strict));
        }

        [Fact]
        public void ARayPassingThePlatesTopRightCorner_IsTenRootTwoFromIt()
        {
            AssertApart(Ask(new GeoRay2(P(220, 0), V(-1, 1)), Plate(), Strict), 10 * Root2, P(110, 110), P(100, 100));
        }

        [Fact]
        public void APlateBehindTheOrigin_IsMeasuredFromTheOrigin()
        {
            AssertApart(Ask(new GeoRay2(P(150, 50), GeoVector2.XAxis), Plate(), Strict), 50, P(150, 50), P(100, 50));
        }

        [Fact]
        public void ARayFromTheMaterialOfThePlate_CrossesTheHoleAndTheOutline()
        {
            AssertCrosses(Ask(new GeoRay2(P(10, 50), GeoVector2.XAxis), Plate(), Strict), P(30, 50), P(70, 50), P(100, 50));
        }

        [Fact]
        public void ARayFromInsideTheHole_ReachesTheMaterial_AndIsNoughtFromIt()
        {
            // From (50, 50) it leaves the hole at (70, 50) and the plate at (100, 50).
            AssertCrosses(Ask(new GeoRay2(P(50, 50), GeoVector2.XAxis), Plate(), Strict), P(70, 50), P(100, 50));
        }

        [Fact]
        public void ARayFourTenthsPastACornerOfThePlate_TouchesItWithinOneButNotWithinAThousandth()
        {
            GeoRay2 ray = Grazing(P(100, 100), U(1, -1), U(1, 1), out GeoPoint2 passing);

            AssertApart(Ask(ray, Plate(), Strict), 0.4, passing, P(100, 100));
            Assert.True(ray.CollidesWith(Plate(), Loose));
        }

        #endregion

        #region The triangle

        [Fact]
        public void ARayAlongYTwenty_CrossesTheTriangleAtNoughtTwentyAndEightyTwenty()
        {
            // y = 20 meets the left side x = 0 and the long side x + y = 100 at x = 80.
            var ray = new GeoRay2(P(-50, 20), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, Triangle(), Strict), P(0, 20), P(80, 20));
            AssertInOrder(FarAlong(ray, GeoVector2.XAxis).GetIntersections(Triangle(), Strict), ray.GetIntersections(Triangle(), Strict));
        }

        [Fact]
        public void ARayUpXAHundredFifty_IsFiftyFromTheTrianglesCorner()
        {
            // The corner (100, 0) is the triangle's furthest point in x; the ray's foot from it is (150, 0), 50 along.
            AssertApart(Ask(new GeoRay2(P(150, -50), GeoVector2.YAxis), Triangle(), Strict), 50, P(150, 0), P(100, 0));
        }

        [Fact]
        public void ATriangleBehindTheOrigin_IsMeasuredFromTheOrigin()
        {
            AssertApart(Ask(new GeoRay2(P(20, -30), -GeoVector2.YAxis), Triangle(), Strict), 30, P(20, -30), P(20, 0));
        }

        [Fact]
        public void ARayFromInsideTheTriangle_LeavesThroughTheLongSide()
        {
            // From (20, 20) along (1, 1) it meets x + y = 100 at (50, 50).
            AssertCrosses(Ask(new GeoRay2(P(20, 20), V(1, 1)), Triangle(), Strict), P(50, 50));
        }

        [Fact]
        public void ARayFourTenthsPastACornerOfTheTriangle_TouchesItWithinOneButNotWithinAThousandth()
        {
            // Up x = 100.4, past the corner (100, 0), the furthest point in x.
            GeoRay2 ray = Grazing(P(100, 0), GeoVector2.YAxis, GeoVector2.XAxis, out GeoPoint2 passing);

            AssertApart(Ask(ray, Triangle(), Strict), 0.4, passing, P(100, 0));
            Assert.True(ray.CollidesWith(Triangle(), Loose));
        }

        #endregion

        #region The circle

        [Fact]
        public void ARayAlongYThirty_CrossesTheCircleAtMinusFortyAndForty_InThatOrder()
        {
            // 30² + 40² = 50²; along the ray, the entry comes first.
            var ray = new GeoRay2(P(-100, 30), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, Circle(), Strict), P(-40, 30), P(40, 30));
            AssertInOrder(new[] { P(-40, 30), P(40, 30) }, ray.GetIntersections(Circle(), Strict));
        }

        [Fact]
        public void ARayAlongYEighty_IsThirtyAboveTheCircle()
        {
            AssertApart(Ask(new GeoRay2(P(-100, 80), GeoVector2.XAxis), Circle(), Strict), 30, P(0, 80), P(0, 50));
        }

        [Fact]
        public void ACircleBehindTheOrigin_IsMeasuredFromTheOrigin()
        {
            AssertApart(Ask(new GeoRay2(P(100, 0), GeoVector2.XAxis), Circle(), Strict), 50, P(100, 0), P(50, 0));
        }

        [Fact]
        public void ARayFromTheCentre_IsNoughtFromTheCircle_AndLeavesItAtFiftyNought()
        {
            AssertCrosses(Ask(new GeoRay2(P(0, 0), GeoVector2.XAxis), Circle(), Strict), P(50, 0));
        }

        [Fact]
        public void ACircleAThousandAlong_IsCrossedOnBothSides()
        {
            // The cut reaches the far side: centre 1000 along plus the radius.
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);

            AssertInOrder(new[] { P(950, 0), P(1050, 0) }, ray.GetIntersections(new GeoCircle2(P(1000, 0), 50), Strict));
        }

        [Fact]
        public void ARayFourTenthsAboveTheCircle_TouchesItWithinOne_AtTheFootAboveTheTop()
        {
            // Within a tolerance of 1 the circle's tangent band gives one point, the foot on the ray, (0, 50.4).
            var ray = new GeoRay2(P(-100, 50.4), GeoVector2.XAxis);

            AssertApart(Ask(ray, Circle(), Strict), 0.4, P(0, 50.4), P(0, 50));
            Assert.True(ray.CollidesWith(Circle(), Loose));
            AssertInOrder(new[] { P(0, 50.4) }, ray.GetIntersections(Circle(), Loose));
        }

        #endregion

        #region The arc

        [Fact]
        public void ARayAlongYThirty_CrossesTheArcAtMinusFortyAndForty_InThatOrder()
        {
            // (40, 30) is at 36.9 degrees and (-40, 30) at 143.1, both within 30 to 180.
            var ray = new GeoRay2(P(-100, 30), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, Arc(), Strict), P(-40, 30), P(40, 30));
            AssertInOrder(new[] { P(-40, 30), P(40, 30) }, ray.GetIntersections(Arc(), Strict));
        }

        [Fact]
        public void ARayAlongYMinusThirty_CrossesTheCircleButNotTheArc_AndIsThirtyFromItsEnd()
        {
            // The arc lies at y ≥ 0, so its nearest point to y = -30 is where it reaches y = 0: its end (-50, 0).
            AssertApart(Ask(new GeoRay2(P(-100, -30), GeoVector2.XAxis), Arc(), Strict), 30, P(-50, -30), P(-50, 0));
        }

        [Fact]
        public void AnArcBehindTheOrigin_IsMeasuredToItsStart()
        {
            // From (100, 0) the distance to the arc point at angle θ grows with θ, so the nearest is the start, (50 cos 30°,
            // 25), √((100 - 43.30)² + 25²) = 61.97 away.
            var start = P(50 * Math.Cos(Math.PI / 6), 25);

            AssertApart(Ask(new GeoRay2(P(100, 0), GeoVector2.XAxis), Arc(), Strict), P(100, 0).DistanceTo(start), P(100, 0), start);
        }

        [Fact]
        public void ARayUpFromTheArcsCentre_CrossesItAtTheTop()
        {
            AssertCrosses(Ask(new GeoRay2(P(0, 0), GeoVector2.YAxis), Arc(), Strict), P(0, 50));
        }

        [Fact]
        public void AnArcBulgingPastItsEnds_IsCutByItsCircle_NotItsEnds()
        {
            // The right half's ends are at x = 0, yet it reaches (50, 0): a cut taken over its ends would stop short of it.
            var ray = new GeoRay2(P(-100, 0), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, RightHalf(), Strict), P(50, 0));
        }

        [Fact]
        public void ARayFourTenthsAboveTheArc_TouchesItWithinOne()
        {
            var ray = new GeoRay2(P(-100, 50.4), GeoVector2.XAxis);

            AssertApart(Ask(ray, Arc(), Strict), 0.4, P(0, 50.4), P(0, 50));
            Assert.True(ray.CollidesWith(Arc(), Loose));
        }

        #endregion

        #region Edges, straight and curved

        [Fact]
        public void AStraightEdgeAcrossTheRay_IsCrossedAtFiftyNought()
        {
            AssertCrosses(Ask(new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoEdge2(P(50, -10), P(50, 10)), Strict), P(50, 0));
        }

        [Fact]
        public void AStraightEdgeBesideTheRay_IsTenFromItsNearerEnd()
        {
            AssertApart(Ask(new GeoRay2(P(0, 20), GeoVector2.XAxis), new GeoEdge2(P(50, -10), P(50, 10)), Strict), 10, P(50, 20), P(50, 10));
        }

        [Fact]
        public void AStraightEdgeBehindTheOrigin_IsMeasuredFromTheOrigin()
        {
            AssertApart(Ask(new GeoRay2(P(60, 0), GeoVector2.XAxis), new GeoEdge2(P(50, -10), P(50, 10)), Strict), 10, P(60, 0), P(50, 0));
        }

        [Fact]
        public void ACurvedEdge_IsCrossedWhereItsArcIs()
        {
            var ray = new GeoRay2(P(-100, 30), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, new GeoEdge2(Arc()), Strict), P(-40, 30), P(40, 30));
            AssertInOrder(new[] { P(-40, 30), P(40, 30) }, ray.GetIntersections(new GeoEdge2(Arc()), Strict));
            AssertApart(Ask(new GeoRay2(P(-100, -30), GeoVector2.XAxis), new GeoEdge2(Arc()), Strict), 30, P(-50, -30), P(-50, 0));
        }

        [Fact]
        public void ACurvedEdgeBulgingPastItsEnds_IsCutByItsCircle_NotItsEnds()
        {
            AssertCrosses(Ask(new GeoRay2(P(-100, 0), GeoVector2.XAxis), new GeoEdge2(RightHalf()), Strict), P(50, 0));
        }

        [Fact]
        public void ARayFourTenthsPastTheEndOfAStraightEdge_TouchesItWithinOne()
        {
            // Along X at y = 10.4, past the end (50, 10) of the edge up x = 50.
            var ray = new GeoRay2(P(0, 10.4), GeoVector2.XAxis);
            var edge = new GeoEdge2(P(50, -10), P(50, 10));

            AssertApart(Ask(ray, edge, Strict), 0.4, P(50, 10.4), P(50, 10));
            Assert.True(ray.CollidesWith(edge, Loose));
        }

        #endregion

        #region The polyline

        [Fact]
        public void ARayAcrossTheCorner_CrossesBothLegsOfTheL()
        {
            // Along (1, 1) from (20, -50) the ray runs on y = x - 70: it meets the bottom at (70, 0) and x = 100 at (100, 30).
            var ray = new GeoRay2(P(20, -50), V(1, 1));

            AssertCrosses(Ask(ray, Ell(), Strict), P(70, 0), P(100, 30));
            AssertInOrder(FarAlong(ray, U(1, 1)).GetIntersections(Ell(), Strict), ray.GetIntersections(Ell(), Strict));
        }

        [Fact]
        public void ARayPassingTheOuterCorner_IsAHundredAndSixFromIt()
        {
            // On y = x - 250 the corner (100, 0) is 150/√2 = 106.07 away, its foot (175, -75), 25√2 along from (150, -100).
            AssertApart(Ask(new GeoRay2(P(150, -100), V(1, 1)), Ell(), Strict), 150 / Root2, P(175, -75), P(100, 0));
        }

        [Fact]
        public void APolylineBehindTheOrigin_IsMeasuredFromTheOriginToItsStart()
        {
            // From (-30, 50) pointing away, the nearest point is (0, 0), √3400 = 58.31 away.
            AssertApart(Ask(new GeoRay2(P(-30, 50), -GeoVector2.XAxis), Ell(), Strict), Math.Sqrt(3400), P(-30, 50), P(0, 0));
        }

        [Fact]
        public void ARayFromAPointOfThePolyline_MeetsItThere()
        {
            AssertCrosses(Ask(new GeoRay2(P(50, 0), GeoVector2.YAxis), Ell(), Strict), P(50, 0));
        }

        [Fact]
        public void ARayFourTenthsPastTheOuterCornerOfThePolyline_TouchesItWithinOne()
        {
            GeoRay2 ray = Grazing(P(100, 0), U(1, 1), U(1, -1), out GeoPoint2 passing);

            AssertApart(Ask(ray, Ell(), Strict), 0.4, passing, P(100, 0));
            Assert.True(ray.CollidesWith(Ell(), Loose));
        }

        #endregion

        #region The curved chain

        [Fact]
        public void ARayAlongYFifty_CrossesTheHookAtTheTipOfItsHalfCircle()
        {
            // y = 50 meets the circle about (100, 50) at x = 50 and 150; the half circle is the right one. The chain's
            // vertices reach only x = 100, so a cut taken over them would stop short of (150, 50).
            var ray = new GeoRay2(P(-100, 50), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, Hook(), Strict), P(150, 50));
        }

        [Fact]
        public void ARayUpXTwoHundred_IsFiftyFromTheTipOfTheHook()
        {
            AssertApart(Ask(new GeoRay2(P(200, -100), GeoVector2.YAxis), Hook(), Strict), 50, P(200, 50), P(150, 50));
        }

        [Fact]
        public void AHookBehindTheOrigin_IsMeasuredToItsStart()
        {
            // From (-30, -40) pointing away the nearest point is the start (0, 0), 50 away.
            AssertApart(Ask(new GeoRay2(P(-30, -40), -GeoVector2.XAxis), Hook(), Strict), 50, P(-30, -40), P(0, 0));
        }

        [Fact]
        public void ARayFromTheCentreOfTheHooksHalfCircle_CrossesItAtTheTip()
        {
            AssertCrosses(Ask(new GeoRay2(P(100, 50), GeoVector2.XAxis), Hook(), Strict), P(150, 50));
        }

        [Fact]
        public void ARayFourTenthsPastTheTipOfTheHook_TouchesItWithinOne()
        {
            var ray = new GeoRay2(P(150.4, -100), GeoVector2.YAxis);

            AssertApart(Ask(ray, Hook(), Strict), 0.4, P(150.4, 50), P(150, 50));
            Assert.True(ray.CollidesWith(Hook(), Loose));
        }

        #endregion

        #region The curved loop

        [Fact]
        public void ARayAlongTheSlot_CrossesItsRoundEnds()
        {
            // At y = 25 the round ends reach x = -25 and 225, 25 past the vertices: a cut taken over the vertices would end
            // at x = 201 and miss (225, 25).
            var ray = new GeoRay2(P(-100, 25), GeoVector2.XAxis);

            AssertCrosses(Ask(ray, Slot(), Strict), P(-25, 25), P(225, 25));
            AssertInOrder(FarAlong(ray, GeoVector2.XAxis).GetIntersections(Slot(), Strict), ray.GetIntersections(Slot(), Strict));
        }

        [Fact]
        public void ARayUpXTwoSixty_IsThirtyFiveFromTheSlotsEnd()
        {
            AssertApart(Ask(new GeoRay2(P(260, -100), GeoVector2.YAxis), Slot(), Strict), 35, P(260, 25), P(225, 25));
        }

        [Fact]
        public void ASlotBehindTheOrigin_IsMeasuredFromTheOrigin()
        {
            AssertApart(Ask(new GeoRay2(P(300, 25), GeoVector2.XAxis), Slot(), Strict), 75, P(300, 25), P(225, 25));
        }

        [Fact]
        public void ARayFromInsideTheSlot_IsNoughtFromIt_AndLeavesThroughTheTop()
        {
            AssertCrosses(Ask(new GeoRay2(P(100, 25), GeoVector2.YAxis), Slot(), Strict), P(100, 50));
        }

        [Fact]
        public void ARayFourTenthsPastTheSlotsEnd_TouchesItWithinOne()
        {
            var ray = new GeoRay2(P(225.4, -100), GeoVector2.YAxis);

            AssertApart(Ask(ray, Slot(), Strict), 0.4, P(225.4, 25), P(225, 25));
            Assert.True(ray.CollidesWith(Slot(), Loose));
        }

        #endregion

        #region A segment, through TryIntersectWith

        [Fact]
        public void TheSegmentsCrossingPoint_IsTheOnePointTryIntersectWithGives()
        {
            // Against a segment the out is one point (D4), as GeoLine2's is.
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);

            Assert.True(ray.TryIntersectWith(new GeoLine2(P(50, -10), P(50, 10)), out GeoPoint2 crossing, Strict));
            AssertNear(P(50, 0), crossing);
        }

        #endregion

        #region Asking twice, and the shapes left as they were

        [Fact]
        public void AskingTwice_GivesTheSameAnswersToTheBit_AndLeavesTheShapesAsTheyWere()
        {
            var ray = new GeoRay2(P(-50, 50), GeoVector2.XAxis);
            GeoPolygon2 square = Square();
            GeoFace2 plate = Plate();
            GeoPolygonArc2 slot = Slot();
            GeoPoint2[] squareBefore = square.Vertices.ToArray();

            GeoPoint2[] first = ray.GetIntersections(plate, Strict);
            GeoPoint2[] second = ray.GetIntersections(plate, Strict);

            Assert.Equal(first, second);
            Assert.Equal(ray.GetShortestLineTo(slot, Strict), ray.GetShortestLineTo(slot, Strict));
            Assert.Equal(ray.DistanceTo(square, Strict), ray.DistanceTo(square, Strict));
            Assert.Equal(squareBefore, square.Vertices.ToArray());
            Assert.Equal(Plate(), plate);
            Assert.Equal(Slot(), slot);
            Assert.Equal(new GeoRay2(P(-50, 50), GeoVector2.XAxis), ray);
        }

        #endregion
    }
}
