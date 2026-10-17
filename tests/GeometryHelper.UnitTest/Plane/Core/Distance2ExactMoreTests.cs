using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The pairs <see cref="Distance2ExactTests"/> leaves out: a polyline against a polyline, a segment and a rectangle
    /// corner, a straight chain against a polygon, a straight loop against every straight shape and against another loop,
    /// and two triangles. Each reads nought only where the shapes truly meet, whichever way round it is asked.
    /// </summary>
    public class Distance2ExactMoreTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>The gap is exact to rounding: 4E-14 of the largest coordinate here, 200, with room.</summary>
        private const double Allowance = 1E-11;

        private static GeoPolygon2 Box(double left, double bottom, double right, double top) => new GeoPolygon2(
            new GeoPoint2(left, bottom), new GeoPoint2(right, bottom), new GeoPoint2(right, top), new GeoPoint2(left, top));

        private static GeoPolygonArc2 StraightLoop(GeoPolygon2 polygon) => new GeoPolygonArc2(polygon.Vertices, new double[polygon.VertexCount]);

        private static double Within(Func<double> distance)
        {
            using (Tolerance.Use(Tol))
            {
                return distance();
            }
        }

        private static void AssertGap(double expected, double actual)
        {
            Assert.InRange(actual, expected - Allowance, expected + Allowance);
        }

        /// <summary>Half the tolerance off the side x = 100 of the square from the origin to (100, 100); the double nearest, less 100.</summary>
        private static double Off() => (100 + Tol.EqualPoint / 2) - 100;

        [Fact]
        public void APolylineStartingHalfAToleranceOffAnother_IsThatFarEitherWayRound()
        {
            double x = 100 + Tol.EqualPoint / 2;
            var chain = new GeoPolyline2(new GeoPoint2(x, 50), new GeoPoint2(150, 50), new GeoPoint2(150, 80));
            var upright = new GeoPolyline2(new GeoPoint2(100, 0), new GeoPoint2(100, 100));

            AssertGap(Off(), Within(() => chain.DistanceTo(upright)));
            AssertGap(Off(), Within(() => upright.DistanceTo(chain)));
        }

        [Fact]
        public void APolylineStartingHalfAToleranceOffASegment_IsThatFarEitherWayRound()
        {
            double x = 100 + Tol.EqualPoint / 2;
            var chain = new GeoPolyline2(new GeoPoint2(x, 50), new GeoPoint2(150, 50), new GeoPoint2(150, 80));
            var line = new GeoLine2(new GeoPoint2(100, 0), new GeoPoint2(100, 100));

            AssertGap(Off(), Within(() => chain.DistanceTo(line)));
            AssertGap(Off(), Within(() => line.DistanceTo(chain)));
        }

        [Fact]
        public void APolylineTipOneAndAFifthTolerancesOffARectangleCorner_IsThatFarAndDoesNotCollide()
        {
            var rect = new GeoRectangle2(0, 0, 100, 100);
            double d = 100.00085 - 100;
            var tip = new GeoPoint2(100 + d, 100 + d);
            var chain = new GeoPolyline2(new GeoPoint2(tip.X + 3, tip.Y + 9), tip, new GeoPoint2(tip.X + 9, tip.Y + 3));
            double gap = d * Math.Sqrt(2);

            AssertGap(gap, Within(() => rect.DistanceTo(chain)));
            AssertGap(gap, Within(() => chain.DistanceTo(rect)));
            Assert.False(rect.CollidesWith(chain, Tol));
            Assert.False(chain.CollidesWith(rect, Tol));
        }

        [Fact]
        public void AStraightChainStartingHalfAToleranceOffAPolygon_IsThatFarEitherWayRound()
        {
            double x = 100 + Tol.EqualPoint / 2;
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(x, 50), new GeoPoint2(150, 50), new GeoPoint2(150, 80) }, new[] { 0.0, 0.0, 0.0 });
            GeoPolygon2 square = Box(0, 0, 100, 100);

            AssertGap(Off(), Distance2.DistanceTo(chain, square, Tol));
            AssertGap(Off(), square.DistanceTo(chain, Tol));
        }

        [Fact]
        public void AStraightLoopHalfAToleranceOffEveryStraightShape_IsThatFarEitherWayRound()
        {
            double x = 100 + Tol.EqualPoint / 2;
            GeoPolygonArc2 loop = StraightLoop(Box(0, 0, 100, 100));

            GeoPolygon2 beside = Box(x, 0, 200, 100);
            AssertGap(Off(), loop.DistanceTo(beside, Tol));
            AssertGap(Off(), beside.DistanceTo(loop, Tol));

            var chain = new GeoPolyline2(new GeoPoint2(x, 50), new GeoPoint2(150, 50));
            AssertGap(Off(), loop.DistanceTo(chain, Tol));
            AssertGap(Off(), chain.DistanceTo(loop, Tol));

            var line = new GeoLine2(new GeoPoint2(x, 50), new GeoPoint2(150, 50));
            AssertGap(Off(), loop.DistanceTo(line, Tol));
            AssertGap(Off(), line.DistanceTo(loop, Tol));

            var rect = new GeoRectangle2(x, 20, 50, 60);
            AssertGap(Off(), Within(() => loop.DistanceTo(rect)));
            AssertGap(Off(), Within(() => rect.DistanceTo(loop)));
        }

        [Fact]
        public void AStraightLoopAndAStraightChainOrLoopHalfAToleranceApart_AreThatFarEitherWayRound()
        {
            double x = 100 + Tol.EqualPoint / 2;
            GeoPolygonArc2 loop = StraightLoop(Box(0, 0, 100, 100));

            GeoPolygonArc2 otherLoop = StraightLoop(Box(x, 0, 200, 100));
            AssertGap(Off(), loop.DistanceTo(otherLoop, Tol));
            AssertGap(Off(), otherLoop.DistanceTo(loop, Tol));

            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(x, 50), new GeoPoint2(150, 50) }, new[] { 0.0, 0.0 });
            AssertGap(Off(), loop.DistanceTo(chain, Tol));
            AssertGap(Off(), chain.DistanceTo(loop, Tol));
        }

        [Fact]
        public void ATriangleCornerHalfAToleranceOffAnotherTrianglesSide_IsThatFarEitherWayRound()
        {
            // The side from (100, 0) to (0, 100) of the first; the second's tip stands off its middle (50, 50) along its normal.
            var first = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(0, 100));
            double step = Tol.EqualPoint / 2 / Math.Sqrt(2);
            var second = new GeoTriangle2(new GeoPoint2(50 + step, 50 + step), new GeoPoint2(90, 60), new GeoPoint2(60, 90));

            AssertGap(Tol.EqualPoint / 2, Within(() => first.DistanceTo(second)));
            AssertGap(Tol.EqualPoint / 2, Within(() => second.DistanceTo(first)));
        }

        [Fact]
        public void ASegmentShorterThanTheToleranceAcrossASide_IsNoughtAway()
        {
            // 0.0005 long, from 0.0002 inside the square's side x = 100 to 0.0003 outside it.
            GeoPolygon2 square = Box(0, 0, 100, 100);
            var stub = new GeoLine2(new GeoPoint2(99.9998, 50), new GeoPoint2(100.0003, 50));

            Assert.Equal(0.0, Within(() => square.DistanceTo(stub)));
            Assert.Equal(0.0, Within(() => stub.DistanceTo(square)));
        }

        #region A rectangle of no width, or of next to none

        /// <summary>The square 6 by 6 turned 45 degrees, its bottom corner at (5, 1).</summary>
        private static GeoRectangle2 DiamondOverTheFlat() => new GeoRectangle2(new GeoPoint2(5, 1 + 6 / Math.Sqrt(2)), 6, 6, Math.PI / 4);

        private static void AssertApart(double gap, GeoRectangle2 first, GeoRectangle2 second)
        {
            AssertGap(gap, Within(() => first.DistanceTo(second)));
            AssertGap(gap, Within(() => second.DistanceTo(first)));
            Assert.False(first.CollidesWith(second, Tol));
            Assert.False(second.CollidesWith(first, Tol));
        }

        [Fact]
        public void ARectangleOfNoHeightAndATurnedSquareAboveIt_AreTheGapApart()
        {
            // The segment from (0, 0) to (10, 0), read as a rectangle, and the square's corner 1 above its middle.
            AssertApart(1.0, new GeoRectangle2(new GeoPoint2(5, 0), 10, 0), DiamondOverTheFlat());
        }

        [Fact]
        public void TwoRectanglesOfNoHeightFiveApart_AreFiveApart()
        {
            AssertApart(5.0, new GeoRectangle2(new GeoPoint2(5, 0), 10, 0), new GeoRectangle2(new GeoPoint2(5, 5), 10, 0));
        }

        [Fact]
        public void ARectangleAMillionthThickAndATurnedSquareAboveIt_AreTheGapApart()
        {
            AssertApart(1.0 - 5E-7, new GeoRectangle2(new GeoPoint2(5, 0), 10, 1E-6), DiamondOverTheFlat());
        }

        [Fact]
        public void ACornerTwoTenMillionthsFromTheEndOfARectangleAMillionthThick_IsThatFarFromIt()
        {
            // The end of the thin rectangle is a side 0.000001 long, shorter than the tolerance; the corner of the turned
            // square stands 2E-7 off its middle, 5.4E-7 from its ends. A side is the side it is, not its start.
            var thin = new GeoRectangle2(new GeoPoint2(0, 0), 10, 1E-6);
            var diamond = new GeoRectangle2(new GeoPoint2(5 + 2E-7 + 3 * Math.Sqrt(2), 0), 6, 6, Math.PI / 4);

            AssertGap(2E-7, Within(() => thin.DistanceTo(diamond)));
            AssertGap(2E-7, Within(() => diamond.DistanceTo(thin)));
            Assert.True(thin.CollidesWith(diamond, Tol));
        }

        #endregion

        #region A face with an L-shaped hole and a rectangle in it

        /// <summary>
        /// The square 100 by 100 about the origin with an L-shaped hole, its reflex corner at (10.00085, 10.00085) and
        /// listed first, so that a probe asked whether it holds the first corner of the hole is asked about that one.
        /// </summary>
        private static GeoFace2 PlateWithAnLShapedHole() => new GeoFace2(
            Box(-50, -50, 50, 50),
            new[]
            {
                new GeoPolygon2(
                    new GeoPoint2(10.00085, 10.00085), new GeoPoint2(10.00085, 20), new GeoPoint2(-5, 20),
                    new GeoPoint2(-5, -5), new GeoPoint2(20, -5), new GeoPoint2(20, 10.00085))
            });

        [Fact]
        public void ARectangleInAnLShapedHoleOneAndAFifthTolerancesFromItsReflexCorner_IsThatFarAndDoesNotCollide()
        {
            // 10 by 10 from the origin: its corner (10, 10) is 0.00085 in x and in y from the reflex corner, 0.0012 away.
            var rect = new GeoRectangle2(0, 0, 10, 10);
            GeoFace2 plate = PlateWithAnLShapedHole();
            double gap = (10.00085 - 10) * Math.Sqrt(2);

            AssertGap(gap, rect.DistanceTo(plate, Tol));
            AssertGap(gap, plate.DistanceTo(rect, Tol));
            Assert.False(plate.CollidesWith(rect, Tol));
            Assert.False(rect.CollidesWith(plate, Tol));
        }

        [Fact]
        public void ThePolygonOfTheSameBoxInTheSameHole_DoesNotCollide()
        {
            GeoPolygon2 box = Box(0, 0, 10, 10);
            GeoFace2 plate = PlateWithAnLShapedHole();

            Assert.False(plate.CollidesWith(box, Tol));
            Assert.False(box.CollidesWith(plate, Tol));
        }

        #endregion
    }
}
