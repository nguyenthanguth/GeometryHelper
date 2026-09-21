using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using System.Linq;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The operations the plane half gained so that it answers the same questions as the solid half:
    /// which side of a line a point lies on, whether two vectors point the same way, and where a point
    /// falls on the line a segment carries. Plus the measurements the region types were missing.
    /// </summary>
    public class PlaneSymmetryTests
    {
        private static readonly GeoLine2 Rising = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 10));

        [Fact]
        public void GetSide_NamesTheSideSeenAlongTheSegment()
        {
            // Looking from (0,0) towards (10,10): up and left of the line, down and right of it.
            Assert.Equal(LineSide.Left, Containment2.GetSide(Rising, new GeoPoint2(0, 5)));
            Assert.Equal(LineSide.Right, Containment2.GetSide(Rising, new GeoPoint2(5, 0)));
            Assert.Equal(LineSide.On, Containment2.GetSide(Rising, new GeoPoint2(5, 5)));

            // Reversing the segment swaps left and right, and leaves On alone.
            var falling = new GeoLine2(new GeoPoint2(10, 10), new GeoPoint2(0, 0));
            Assert.Equal(LineSide.Right, Containment2.GetSide(falling, new GeoPoint2(0, 5)));
            Assert.Equal(LineSide.Left, Containment2.GetSide(falling, new GeoPoint2(5, 0)));
        }

        [Fact]
        public void GetSide_ReadsTheSegmentAsTheLineCarryingIt()
        {
            // Well beyond the end of the segment, but still clearly to one side of its line.
            Assert.Equal(LineSide.Left, Containment2.GetSide(Rising, new GeoPoint2(100, 200)));
            Assert.Equal(LineSide.On, Containment2.GetSide(Rising, new GeoPoint2(-50, -50)));
        }

        [Fact]
        public void GetSide_MeasuresTheDistanceOffTheLineAgainstTheTolerance()
        {
            var tolerance = new Tolerance(0.01, 0.01);

            // A point half a hundredth off a line ten units long: nearer than the tolerance, so On.
            Assert.Equal(LineSide.On, Containment2.GetSide(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                                                           new GeoPoint2(5, 0.005), tolerance));
            Assert.Equal(LineSide.Left, Containment2.GetSide(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                                                             new GeoPoint2(5, 0.02), tolerance));
        }

        [Fact]
        public void GetSide_RefusesASegmentWithNoDirection()
        {
            var point = new GeoPoint2(1, 1);
            var degenerate = new GeoLine2(point, point);

            Assert.Throws<InvalidOperationException>(() => Containment2.GetSide(degenerate, new GeoPoint2(5, 5)));
        }

        [Fact]
        public void GetSideOf_OnThePointAgreesWithTheStaticForm()
        {
            var point = new GeoPoint2(0, 5);

            Assert.Equal(Containment2.GetSide(Rising, point), point.GetSideOf(Rising));
            Assert.Equal(Containment2.GetSide(Rising, point, Tolerance.Global), point.GetSideOf(Rising, Tolerance.Global));
        }

        [Fact]
        public void IsCodirectional_TellsTheTwoWaysAlongALineApart()
        {
            var along = new GeoVector2(3, 4);

            Assert.True(Parallel2.IsCodirectional(along, new GeoVector2(6, 8)));
            Assert.False(Parallel2.IsCodirectional(along, new GeoVector2(-6, -8)));
            Assert.False(Parallel2.IsCodirectional(along, new GeoVector2(-4, 3)));

            // Both are parallel; only one points the same way.
            Assert.True(Parallel2.IsParallel(along, new GeoVector2(-6, -8)));

            Assert.True(along.IsCodirectionalTo(new GeoVector2(6, 8)));
            Assert.False(along.IsCodirectionalTo(new GeoVector2(-6, -8)));
        }

        [Fact]
        public void IsCodirectional_AVectorWithNoLengthPointsNowhere()
        {
            Assert.False(Parallel2.IsCodirectional(new GeoVector2(1, 0), GeoVector2.Zero));
            Assert.False(Parallel2.IsCodirectional(GeoVector2.Zero, GeoVector2.Zero));
        }

        [Fact]
        public void ProjectToInfiniteLine_ReachesBeyondTheEndsWhereProjectToLineStops()
        {
            var segment = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var beyond = new GeoPoint2(25, 4);

            Assert.True(Projection2.ProjectToInfiniteLine(segment, beyond).IsEqualTo(new GeoPoint2(25, 0)));

            // The bounded form clamps to the segment, which is the difference between the two.
            Assert.True(Projection2.ProjectToLine(segment, beyond).IsEqualTo(new GeoPoint2(10, 0)));

            // Within the segment the two agree.
            var inside = new GeoPoint2(6, 3);
            Assert.True(Projection2.ProjectToInfiniteLine(segment, inside).IsEqualTo(Projection2.ProjectToLine(segment, inside)));
        }

        [Fact]
        public void ProjectToInfiniteLine_RefusesASegmentWithNoDirection()
        {
            var point = new GeoPoint2(1, 1);

            Assert.Throws<InvalidOperationException>(() => Projection2.ProjectToInfiniteLine(new GeoLine2(point, point), new GeoPoint2(5, 5)));
        }

        [Fact]
        public void ARectangleReportsItsArea()
        {
            var rectangle = new GeoRectangle2(new GeoPoint2(5, -3), 8.0, 2.5);

            Assert.Equal(20.0, rectangle.Area, 12);

            // Turning it moves no corner nearer another, so the area is the same.
            Assert.Equal(20.0, rectangle.RotateBy(0.7, GeoPoint2.Origin).Area, 12);
            Assert.Equal(rectangle.ToPolygon().Area, rectangle.Area, 9);
        }

        [Fact]
        public void AFaceBalancesAtItsCentroidWithTheHolesTakenOut()
        {
            var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

            // No holes: the centroid of the boundary.
            Assert.True(new GeoFace2(square).Centroid.IsEqualTo(new GeoPoint2(5, 5)));

            // A hole on the left pulls the centroid right, by the share of the area it takes with it.
            var hole = new GeoPolygon2(new GeoPoint2(1, 4), new GeoPoint2(3, 4), new GeoPoint2(3, 6), new GeoPoint2(1, 6));
            GeoPoint2 centroid = new GeoFace2(square, new[] { hole }).Centroid;

            double expectedX = (100.0 * 5.0 - 4.0 * 2.0) / 96.0;
            Assert.Equal(expectedX, centroid.X, 9);
            Assert.Equal(5.0, centroid.Y, 9);
            Assert.True(centroid.X > 5.0);
        }

        [Fact]
        public void AFaceInSpaceBalancesTheSameWay()
        {
            var square = new GeoPolygon3(
                new GeoPoint3(0, 0, 4), new GeoPoint3(10, 0, 4),
                new GeoPoint3(10, 10, 4), new GeoPoint3(0, 10, 4));
            var hole = new GeoPolygon3(
                new GeoPoint3(1, 4, 4), new GeoPoint3(3, 4, 4),
                new GeoPoint3(3, 6, 4), new GeoPoint3(1, 6, 4));

            GeoPoint3 centroid = new GeoFace3(square, new[] { hole }).Centroid;

            Assert.Equal((100.0 * 5.0 - 4.0 * 2.0) / 96.0, centroid.X, 9);
            Assert.Equal(5.0, centroid.Y, 9);
            Assert.Equal(4.0, centroid.Z, 9);
        }

        [Fact]
        public void TheMeasurementsOfAPolygonAreReadTheSameWayInBothDimensions()
        {
            var flat = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(4, 3), new GeoPoint2(0, 3));
            var inSpace = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(4, 0, 0), new GeoPoint3(4, 3, 0), new GeoPoint3(0, 3, 0));

            // Properties on both sides, named alike.
            Assert.Equal(inSpace.Area, flat.Area, 9);
            Assert.Equal(12.0, flat.Area, 9);
            Assert.Equal(12.0, flat.SignedArea, 9);
            Assert.False(flat.IsClockwise);
            Assert.True(flat.Centroid.IsEqualTo(new GeoPoint2(2, 1.5)));

            // A circle gives its length the way every other curve does.
            Assert.Equal(2.0 * Math.PI * 5.0, new GeoCircle2(GeoPoint2.Origin, 5).Length, 12);
            Assert.Equal(new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 5).Length,
                         new GeoCircle2(GeoPoint2.Origin, 5).Length, 12);
        }

        [Fact]
        public void TheShapesOfThePlaneCompareWithinToleranceLikeTheirSolidCounterparts()
        {
            var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(4, 3), new GeoPoint2(0, 3));
            var nudged = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(4, 0.00001), new GeoPoint2(4, 3), new GeoPoint2(0, 3));

            Assert.True(square.IsEqualTo(nudged));
            Assert.False(square.IsEqualTo(nudged, new Tolerance(1E-9, 1E-9)));

            // "Where a loop starts is not part of the shape it encloses"
            var started = new GeoPolygon2(new GeoPoint2(4, 3), new GeoPoint2(0, 3), new GeoPoint2(0, 0), new GeoPoint2(4, 0));
            Assert.True(square.IsEqualTo(started));

            // "The direction it runs in is part of the shape"
            var reversed = new GeoPolygon2(square.Vertices.Reverse().ToArray());
            Assert.False(square.IsEqualTo(reversed));
            Assert.False(square.IsEqualTo(null));

            // A chain has ends, so it is read from its start.
            var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(3, 0), new GeoPoint2(3, 4));
            Assert.True(chain.IsEqualTo(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(3, 0), new GeoPoint2(3, 4))));
            Assert.False(chain.IsEqualTo(chain.Reverse()));

            // A face matches hole for hole, in whatever order they are held.
            var hole1 = new GeoPolygon2(new GeoPoint2(1, 1), new GeoPoint2(2, 1), new GeoPoint2(2, 2), new GeoPoint2(1, 2));
            var hole2 = new GeoPolygon2(new GeoPoint2(3, 1), new GeoPoint2(3.5, 1), new GeoPoint2(3.5, 2), new GeoPoint2(3, 2));
            Assert.True(new GeoFace2(square, new[] { hole1, hole2 }).IsEqualTo(new GeoFace2(square, new[] { hole2, hole1 })));
            Assert.False(new GeoFace2(square, new[] { hole1 }).IsEqualTo(new GeoFace2(square)));
        }
    }
}
