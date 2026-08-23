using System;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest.Core
{
    public class ParametrizationTests
    {
        // A 10 by 10 square walked counter-clockwise from the origin: perimeter 40.
        private static GeoPolygon2 Square() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

        // An L of three vertices: 10 along X, then 10 up. Total length 20.
        private static GeoPolyline2 OpenPath() => new GeoPolyline2(
            new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

        // The same square traced as an open chain: the path returns to its start but never closes,
        // so its length is the full perimeter while it stays a curve.
        private static GeoPolyline2 TracedSquare() => new GeoPolyline2(
            new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10), new GeoPoint2(0, 0));

        private static GeoLine2 Segment() => new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

        private static GeoCircle2 Circle() => new GeoCircle2(new GeoPoint2(0, 0), 10);

        private static GeoRectangle2 Rect() => new GeoRectangle2(new GeoPoint2(0, 0), 20, 10);

        [Theory]
        [InlineData(0.0, 0, 0)]
        [InlineData(0.25, 2.5, 0)]
        [InlineData(0.5, 5, 0)]
        [InlineData(1.0, 10, 0)]
        public void Line_PointAtParameter_WalksTheSegment(double parameter, double x, double y)
        {
            Assert.True(Segment().GetPointAtParameter(parameter).IsEqualTo(new GeoPoint2(x, y)));
        }

        [Fact]
        public void Line_ParameterAndDistance_AreProportional()
        {
            var line = Segment();

            Assert.Equal(5.0, line.GetDistanceAtParameter(0.5), 9);
            Assert.Equal(0.5, line.GetParameterAtDistance(5.0), 9);

            Assert.True(line.GetPointAtDistance(7.5).IsEqualTo(new GeoPoint2(7.5, 0)));
            Assert.Equal(7.5, line.GetDistanceAtPoint(new GeoPoint2(7.5, 4)), 9);
            Assert.Equal(0.75, line.GetParameterAtPoint(new GeoPoint2(7.5, 4)), 9);
        }

        [Fact]
        public void Line_ExtrapolatesBeyondItsEndpoints()
        {
            var line = Segment();

            // A segment lies on an infinite line, so running past either end is meaningful.
            Assert.True(line.GetPointAtParameter(-0.5).IsEqualTo(new GeoPoint2(-5, 0)));
            Assert.True(line.GetPointAtParameter(2.0).IsEqualTo(new GeoPoint2(20, 0)));
            Assert.True(line.GetPointAtDistance(-5.0).IsEqualTo(new GeoPoint2(-5, 0)));

            Assert.Equal(-0.5, line.GetParameterAtPoint(new GeoPoint2(-5, 3)), 9);
            Assert.Equal(-5.0, line.GetDistanceAtPoint(new GeoPoint2(-5, 3)), 9);
        }

        [Fact]
        public void Line_DegenerateSegment_ReportsZeroParameter()
        {
            var degenerate = new GeoLine2(new GeoPoint2(4, 4), new GeoPoint2(4, 4));

            Assert.Equal(0.0, degenerate.GetParameterAtDistance(10.0), 9);
            Assert.Equal(0.0, degenerate.GetDistanceAtParameter(0.5), 9);
            Assert.True(degenerate.GetPointAtDistance(3.0).IsEqualTo(new GeoPoint2(4, 4)));
        }

        [Theory]
        [InlineData(0.0, 10, 0)]        // angle 0
        [InlineData(0.25, 0, 10)]       // quarter turn counter-clockwise
        [InlineData(0.5, -10, 0)]
        [InlineData(0.75, 0, -10)]
        public void Circle_PointAtParameter_RunsCounterClockwiseFromAngleZero(double parameter, double x, double y)
        {
            Assert.True(Circle().GetPointAtParameter(parameter).IsEqualTo(new GeoPoint2(x, y)));
        }

        [Fact]
        public void Circle_ParameterWrapsAroundTheCircumference()
        {
            var circle = Circle();

            Assert.True(circle.GetPointAtParameter(1.25).IsEqualTo(circle.GetPointAtParameter(0.25)));
            Assert.True(circle.GetPointAtParameter(-0.25).IsEqualTo(circle.GetPointAtParameter(0.75)));
            Assert.True(circle.GetPointAtParameter(1.0).IsEqualTo(circle.GetPointAtParameter(0.0)));
        }

        [Fact]
        public void Circle_DistanceIsArcLength()
        {
            var circle = Circle();
            double circumference = circle.Circumference;

            Assert.Equal(circumference * 0.25, circle.GetDistanceAtParameter(0.25), 9);
            Assert.Equal(0.25, circle.GetParameterAtDistance(circumference * 0.25), 9);

            // A quarter of the way round lands at the top of the circle.
            Assert.True(circle.GetPointAtDistance(circumference * 0.25).IsEqualTo(new GeoPoint2(0, 10)));
        }

        [Fact]
        public void Circle_ParameterAtPoint_UsesOnlyTheDirectionFromTheCentre()
        {
            var circle = Circle();

            // Both points sit on the same ray from the centre, so they share a parameter.
            Assert.Equal(0.25, circle.GetParameterAtPoint(new GeoPoint2(0, 10)), 9);
            Assert.Equal(0.25, circle.GetParameterAtPoint(new GeoPoint2(0, 3)), 9);
            Assert.Equal(0.0, circle.GetParameterAtPoint(new GeoPoint2(50, 0)), 9);
        }

        [Fact]
        public void Circle_ZeroRadius_ReportsZeroParameter()
        {
            var point = new GeoCircle2(new GeoPoint2(1, 2), 0.0);

            Assert.Equal(0.0, point.GetParameterAtDistance(5.0), 9);
            Assert.True(point.GetPointAtDistance(5.0).IsEqualTo(new GeoPoint2(1, 2)));
        }

        [Fact]
        public void Rectangle_WalksThePerimeterFromLowerLeft()
        {
            var rect = Rect();   // X: [-10, 10], Y: [-5, 5], perimeter 60

            Assert.Equal(60.0, rect.Length, 9);
            Assert.True(rect.GetPointAtParameter(0.0).IsEqualTo(rect.LowerLeft));

            // 20 units along the bottom edge reaches LowerRight.
            Assert.True(rect.GetPointAtDistance(20.0).IsEqualTo(rect.LowerRight));

            // 10 more up the right edge reaches UpperRight.
            Assert.True(rect.GetPointAtDistance(30.0).IsEqualTo(rect.UpperRight));

            // Half way round is the same thing expressed as a parameter.
            Assert.True(rect.GetPointAtParameter(0.5).IsEqualTo(rect.UpperRight));
        }

        [Fact]
        public void Rectangle_ParameterWrapsAroundThePerimeter()
        {
            var rect = Rect();

            Assert.True(rect.GetPointAtParameter(1.25).IsEqualTo(rect.GetPointAtParameter(0.25)));
            Assert.True(rect.GetPointAtDistance(60.0 + 20.0).IsEqualTo(rect.GetPointAtDistance(20.0)));
        }

        [Fact]
        public void Polygon_WalksThePerimeterFromTheFirstVertex()
        {
            var poly = Square();   // perimeter 40

            Assert.Equal(40.0, poly.Length, 9);
            Assert.True(poly.GetPointAtParameter(0.0).IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(poly.GetPointAtDistance(5.0).IsEqualTo(new GeoPoint2(5, 0)));
            Assert.True(poly.GetPointAtDistance(10.0).IsEqualTo(new GeoPoint2(10, 0)));
            Assert.True(poly.GetPointAtDistance(15.0).IsEqualTo(new GeoPoint2(10, 5)));
            Assert.True(poly.GetPointAtParameter(0.25).IsEqualTo(new GeoPoint2(10, 0)));
        }

        [Fact]
        public void Polygon_DistanceAtPoint_MeasuresAlongTheBoundary()
        {
            var poly = Square();

            Assert.Equal(5.0, poly.GetDistanceAtPoint(new GeoPoint2(5, 0)), 9);
            Assert.Equal(15.0, poly.GetDistanceAtPoint(new GeoPoint2(10, 5)), 9);

            // A point off the boundary is measured at its closest point on the boundary.
            Assert.Equal(5.0, poly.GetDistanceAtPoint(new GeoPoint2(5, -3)), 9);
            Assert.Equal(0.125, poly.GetParameterAtPoint(new GeoPoint2(5, -3)), 9);
        }

        [Fact]
        public void Polyline_Open_ClampsOutsideItsEnds()
        {
            var path = OpenPath();   // length 20

            Assert.Equal(20.0, path.Length, 9);
            Assert.True(path.GetPointAtParameter(0.0).IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(path.GetPointAtParameter(0.5).IsEqualTo(new GeoPoint2(10, 0)));
            Assert.True(path.GetPointAtParameter(1.0).IsEqualTo(new GeoPoint2(10, 10)));

            // An open path has no natural extension, so it stops at its endpoints.
            Assert.True(path.GetPointAtParameter(-1.0).IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(path.GetPointAtParameter(2.0).IsEqualTo(new GeoPoint2(10, 10)));
            Assert.True(path.GetPointAtDistance(-5.0).IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(path.GetPointAtDistance(100.0).IsEqualTo(new GeoPoint2(10, 10)));
        }

        [Fact]
        public void Polyline_ClampsInsteadOfWrapping()
        {
            var path = TracedSquare();   // 10 by 10 square traced open, length 40

            Assert.Equal(40.0, path.Length, 9);

            // A polyline is an open chain with no natural extension, so running past either end stops
            // at the endpoint. Wrapping is a closed-curve behaviour and lives on GeoPolygon2.
            Assert.True(path.GetPointAtParameter(1.25).IsEqualTo(path.GetPointAtParameter(1.0)));
            Assert.True(path.GetPointAtParameter(-0.25).IsEqualTo(path.GetPointAtParameter(0.0)));
            Assert.True(path.GetPointAtDistance(45.0).IsEqualTo(path.GetPointAtDistance(40.0)));

            GeoPolygon2 square = path.ToPolygon();
            Assert.True(square.GetPointAtParameter(1.25).IsEqualTo(square.GetPointAtParameter(0.25)));
        }

        [Fact]
        public void Polyline_DistanceAndParameter_AreProportional()
        {
            var path = OpenPath();

            Assert.Equal(10.0, path.GetDistanceAtParameter(0.5), 9);
            Assert.Equal(0.5, path.GetParameterAtDistance(10.0), 9);
            Assert.Equal(15.0, path.GetDistanceAtPoint(new GeoPoint2(10, 5)), 9);
            Assert.Equal(0.75, path.GetParameterAtPoint(new GeoPoint2(10, 5)), 9);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.1)]
        [InlineData(0.37)]
        [InlineData(0.5)]
        [InlineData(0.99)]
        public void PointAndDistanceRoundTrip_OnEveryCurve(double parameter)
        {
            // Walking to a parameter and asking where you ended up must give the parameter back.
            var line = Segment();
            var circle = Circle();
            var rect = Rect();
            var poly = Square();
            var loop = TracedSquare();
            var path = OpenPath();

            Assert.Equal(parameter, line.GetParameterAtPoint(line.GetPointAtParameter(parameter)), 9);
            Assert.Equal(parameter, circle.GetParameterAtPoint(circle.GetPointAtParameter(parameter)), 9);
            Assert.Equal(parameter, rect.GetParameterAtPoint(rect.GetPointAtParameter(parameter)), 9);
            Assert.Equal(parameter, poly.GetParameterAtPoint(poly.GetPointAtParameter(parameter)), 9);
            Assert.Equal(parameter, loop.GetParameterAtPoint(loop.GetPointAtParameter(parameter)), 9);
            Assert.Equal(parameter, path.GetParameterAtPoint(path.GetPointAtParameter(parameter)), 9);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.2)]
        [InlineData(0.65)]
        [InlineData(1.0)]
        public void ParameterAndDistanceConversions_AreInverses(double parameter)
        {
            var line = Segment();
            var circle = Circle();
            var rect = Rect();
            var poly = Square();
            var path = OpenPath();

            Assert.Equal(parameter, line.GetParameterAtDistance(line.GetDistanceAtParameter(parameter)), 9);
            Assert.Equal(parameter, circle.GetParameterAtDistance(circle.GetDistanceAtParameter(parameter)), 9);
            Assert.Equal(parameter, rect.GetParameterAtDistance(rect.GetDistanceAtParameter(parameter)), 9);
            Assert.Equal(parameter, poly.GetParameterAtDistance(poly.GetDistanceAtParameter(parameter)), 9);
            Assert.Equal(parameter, path.GetParameterAtDistance(path.GetDistanceAtParameter(parameter)), 9);
        }

        [Fact]
        public void PointAtParameter_AgreesWithPointAtTheMatchingDistance()
        {
            var poly = Square();
            var circle = Circle();
            var path = OpenPath();

            foreach (double parameter in new[] { 0.0, 0.15, 0.4, 0.85 })
            {
                Assert.True(poly.GetPointAtParameter(parameter)
                    .IsEqualTo(poly.GetPointAtDistance(poly.GetDistanceAtParameter(parameter))));
                Assert.True(circle.GetPointAtParameter(parameter)
                    .IsEqualTo(circle.GetPointAtDistance(circle.GetDistanceAtParameter(parameter))));
                Assert.True(path.GetPointAtParameter(parameter)
                    .IsEqualTo(path.GetPointAtDistance(path.GetDistanceAtParameter(parameter))));
            }
        }

        [Fact]
        public void ResultOfPointAtParameter_LiesOnTheCurve()
        {
            var poly = Square();
            var loop = TracedSquare();
            var rect = Rect();
            var circle = Circle();

            foreach (double parameter in new[] { 0.0, 0.13, 0.5, 0.77, 0.99 })
            {
                Assert.True(Containment2.IsPointOn(poly, poly.GetPointAtParameter(parameter)));
                Assert.True(Containment2.IsPointOn(loop, loop.GetPointAtParameter(parameter)));
                Assert.True(Containment2.IsPointOn(circle, circle.GetPointAtParameter(parameter)));

                GeoPoint2 onRect = rect.GetPointAtParameter(parameter);
                bool onAnEdge = false;
                foreach (GeoLine2 edge in rect.GetEdges())
                {
                    if (Containment2.IsPointOn(edge, onRect)) onAnEdge = true;
                }
                Assert.True(onAnEdge);
            }
        }

        [Fact]
        public void NullShapes_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => Parametrization2.GetPointAtParameter((GeoPolyline2)null, 0.5));
            Assert.Throws<ArgumentNullException>(() => Parametrization2.GetDistanceAtPoint((GeoPolygon2)null, new GeoPoint2(0, 0)));
        }
    }
}
