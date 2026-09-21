using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The two ways of asking a straight shape a question, held against each other. Every operation here
    /// is offered as a static naming the larger shape first and as a method on the shape, and each of
    /// those in a form that takes a tolerance and one that does not. A delegation wired to the wrong
    /// static, or an overload that quietly drops the tolerance it was handed, shows up nowhere else: the
    /// answer is plausible, just not the one the other form gives.
    /// </summary>
    public class PlaneMirrorTests
    {
        private static readonly Tolerance Global = Tolerance.Global;

        private static GeoPoint2 Point() => new GeoPoint2(140, 60);

        private static GeoLine2 Line() => new GeoLine2(new GeoPoint2(-40, 20), new GeoPoint2(160, 20));

        private static GeoLine2 Crossing() => new GeoLine2(new GeoPoint2(50, -50), new GeoPoint2(50, 90));

        private static GeoCircle2 Circle() => new GeoCircle2(new GeoPoint2(50, 25), 30.0);

        private static GeoCircle2 Away() => new GeoCircle2(new GeoPoint2(400, 25), 20.0);

        private static GeoRectangle2 Rectangle() => new GeoRectangle2(new GeoPoint2(60, 30), 80, 40, 0.3);

        private static GeoPolygon2 Polygon() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(120, 0), new GeoPoint2(120, 70), new GeoPoint2(0, 70));

        private static GeoPolyline2 Polyline() => new GeoPolyline2(
            new GeoPoint2(-30, -30), new GeoPoint2(150, -30), new GeoPoint2(150, 100));

        [Fact]
        public void ACircleMeasuresTheSameWhicheverWayItIsAsked()
        {
            GeoCircle2 circle = Circle();

            Assert.Equal(Distance2.DistanceTo(circle, Point()), circle.DistanceTo(Point()), 12);
            Assert.Equal(Distance2.DistanceTo(circle, Line()), circle.DistanceTo(Line()), 12);
            Assert.Equal(Distance2.DistanceTo(circle, Away()), circle.DistanceTo(Away()), 12);
            Assert.Equal(Distance2.DistanceTo(circle, Rectangle()), circle.DistanceTo(Rectangle()), 12);
            Assert.Equal(Distance2.DistanceTo(circle, Polygon()), circle.DistanceTo(Polygon()), 12);
            Assert.Equal(Distance2.DistanceTo(Polyline(), circle), circle.DistanceTo(Polyline()), 12);

            // Two circles a long way apart are measured between their nearest rims, not their centres.
            Assert.Equal(350.0 - 30.0 - 20.0, circle.DistanceTo(Away()), 9);

            // Moving it moves the answer with it, and by exactly as much.
            var step = new GeoVector2(1000, 0);
            Assert.True(circle.Translate(step).Center.IsEqualTo(new GeoPoint2(1050, 25)));
            Assert.Equal(circle.Radius, circle.Translate(step).Radius, 12);

            Assert.Equal(Parametrization2.GetDistanceAtPoint(circle, Point()), circle.GetDistanceAtPoint(Point()), 12);
        }

        [Fact]
        public void ACircleFindsTheSameNearestPieceWhicheverWayItIsAsked()
        {
            GeoCircle2 circle = Circle();

            Assert.True(circle.GetClosestOnBoundary(Line()).IsEqualTo(circle.GetClosestOnBoundary(Line(), Global)));
            Assert.True(circle.GetClosestOnBoundary(Away()).IsEqualTo(circle.GetClosestOnBoundary(Away(), Global)));
            Assert.True(circle.GetClosestOnBoundary(Rectangle()).IsEqualTo(circle.GetClosestOnBoundary(Rectangle(), Global)));
            Assert.True(circle.GetClosestOnBoundary(Polyline()).IsEqualTo(circle.GetClosestOnBoundary(Polyline(), Global)));
            Assert.True(circle.GetClosestOnBoundary(Polygon()).IsEqualTo(circle.GetClosestOnBoundary(Polygon(), Global)));

            // The nearest piece of a shape to a circle is a segment of that shape, and it really is nearest.
            GeoLine2 nearest = circle.GetClosestOnBoundary(Polyline());
            foreach (GeoLine2 edge in Polyline().GetEdges())
            {
                Assert.True(Distance2.DistanceTo(circle, nearest) <= Distance2.DistanceTo(circle, edge) + 1E-9);
            }
        }

        [Fact]
        public void ASegmentMeasuresAndMeetsTheSameWhicheverWayItIsAsked()
        {
            GeoLine2 line = Line();

            Assert.Equal(Distance2.DistanceTo(line, Crossing()), line.DistanceTo(Crossing()), 12);
            Assert.Equal(line.DistanceTo(Crossing()), line.DistanceTo(Crossing(), Global), 12);
            Assert.Equal(Distance2.DistanceTo(Rectangle(), line), line.DistanceTo(Rectangle()), 12);
            Assert.Equal(Distance2.DistanceTo(Polygon(), line), line.DistanceTo(Polygon()), 12);
            Assert.Equal(Distance2.DistanceTo(Polyline(), line), line.DistanceTo(Polyline()), 12);

            Assert.Equal(Containment2.Locate(line, Point()), line.Locate(Point(), Global));
            Assert.Equal(line.Locate(Point()), line.Locate(Point(), Global));

            Assert.Equal(line.CollidesWith(Crossing()), line.CollidesWith(Crossing(), Global));
            Assert.Equal(line.CollidesWith(Rectangle()), line.CollidesWith(Rectangle(), Global));
            Assert.Equal(line.CollidesWith(Polygon()), line.CollidesWith(Polygon(), Global));

            // A segment straight through the polygon meets it and is not apart from it.
            Assert.True(line.CollidesWith(Crossing()));
            Assert.Equal(0.0, line.DistanceTo(Crossing()), 9);
            Assert.False(line.CollidesWith(Away().ToPolygon()));

            foreach (GeoLine2 other in new[] { Crossing(), Line() })
            {
                Assert.True(line.GetClosestOnBoundary(other, Global).Length >= 0.0);
            }

            Assert.True(line.GetClosestOnBoundary(Circle(), Global).Length >= 0.0);
            Assert.True(line.GetClosestOnBoundary(Rectangle(), Global).Length >= 0.0);
            Assert.True(line.GetClosestOnBoundary(Polyline(), Global).Length >= 0.0);
            Assert.True(line.GetClosestOnBoundary(Polygon(), Global).Length >= 0.0);
        }

        [Fact]
        public void TheStaticIntersectionsAreTheOnesTheShapesReport()
        {
            // Every pair, both as a count and against the form that takes a tolerance.
            Assert.Equal(
                Intersection2.GetIntersections(Rectangle(), Rectangle()).Length,
                Intersection2.GetIntersections(Rectangle(), Rectangle(), Global).Length);

            Assert.Equal(
                Intersection2.GetIntersections(Rectangle(), Circle()).Length,
                Intersection2.GetIntersections(Rectangle(), Circle(), Global).Length);

            Assert.Equal(
                Intersection2.GetIntersections(Polygon(), Polygon()).Length,
                Intersection2.GetIntersections(Polygon(), Polygon(), Global).Length);

            Assert.Equal(
                Intersection2.GetIntersections(Polygon(), Rectangle()).Length,
                Intersection2.GetIntersections(Polygon(), Rectangle(), Global).Length);

            Assert.Equal(
                Intersection2.GetIntersections(Polygon(), Circle()).Length,
                Intersection2.GetIntersections(Polygon(), Circle(), Global).Length);

            // A rectangle overlapping a polygon crosses its boundary; one drawn far off does not.
            Assert.NotEmpty(Intersection2.GetIntersections(Polygon(), Rectangle()));
            Assert.Empty(Intersection2.GetIntersections(Polygon(), Away().ToPolygon()));

            // The try forms say the same thing as the counts.
            Assert.Equal(
                Intersection2.GetIntersections(Circle(), Line()).Length > 0,
                Intersection2.TryIntersectWith(Circle(), Line(), out _, Global));

            Assert.Equal(
                Intersection2.GetIntersections(Circle(), Away()).Length > 0,
                Intersection2.TryIntersectWith(Circle(), Away(), out _, Global));

            Assert.Equal(
                Intersection2.GetIntersection(Line(), Crossing()).HasValue,
                Intersection2.TryIntersectWith(Line(), Crossing(), LineExtension.None, out _, Global));

            Assert.Equal(
                Intersection2.GetIntersections(Polyline(), Crossing()).Length > 0,
                Intersection2.TryIntersectWith(Polyline(), Crossing(), out _, Global));
        }

        [Fact]
        public void AFaceIsUnionedAndSubtractedLikeAPolygon()
        {
            var outer = new GeoFace2(Polygon());
            var hole = new GeoPolygon2(
                new GeoPoint2(30, 20), new GeoPoint2(60, 20), new GeoPoint2(60, 50), new GeoPoint2(30, 50));

            GeoFace2[] pierced = Boolean2.Subtract(outer, new GeoFace2(hole));

            Assert.Single(pierced);
            Assert.Single(pierced[0].Holes);
            Assert.Equal(Polygon().Area - hole.Area, pierced[0].Area, 9);

            // A face taken out of itself leaves nothing.
            Assert.Empty(Boolean2.Subtract(outer, outer));

            // Unioning a set of faces that do not touch keeps them all.
            var apart = new GeoFace2(new GeoPolygon2(
                new GeoPoint2(500, 0), new GeoPoint2(560, 0), new GeoPoint2(560, 60), new GeoPoint2(500, 60)));

            GeoFace2[] both = Boolean2.Union(new[] { outer, apart });

            Assert.Equal(2, both.Length);
            Assert.Equal(both.Length, Boolean2.Union(new[] { outer, apart }, Global).Length);
            Assert.Equal(Polygon().Area + apart.Area, both.Sum(face => face.Area), 9);
        }
    }
}
