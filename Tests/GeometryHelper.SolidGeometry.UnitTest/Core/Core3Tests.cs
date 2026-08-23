using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers the Core operation classes directly, and checks that every instance method mirrors the
    /// static one it forwards to.
    /// </summary>
    public class Core3Tests
    {
        #region Parallel3

        [Fact]
        public void ParallelismIsScaleInvariant()
        {
            GeoVector3 small = new GeoVector3(1E-2, 0.0, 0.0);
            GeoVector3 large = new GeoVector3(1E6, 0.0, 0.0);

            // Both are longer than the vector tolerance of 1E-4, so neither counts as degenerate and the
            // answer must not depend on the difference of eight orders of magnitude between them.
            Assert.True(Parallel3.IsParallel(small, large));
            Assert.True(Parallel3.IsParallel(large, small));
        }

        [Fact]
        public void AVectorShorterThanTheToleranceCountsAsDegenerateAndAnswersFalse()
        {
            // Below the vector threshold there is no direction left to compare, so the answer is a
            // refusal rather than a confident yes drawn from rounding noise.
            GeoVector3 belowThreshold = new GeoVector3(1E-6, 0.0, 0.0);

            Assert.True(belowThreshold.IsZeroLength());
            Assert.False(Parallel3.IsParallel(belowThreshold, GeoVector3.XAxis));
            Assert.False(Parallel3.IsPerpendicular(belowThreshold, GeoVector3.YAxis));

            // Widening the tolerance is what makes such a vector usable again.
            Tolerance fine = new Tolerance(1E-9, 1E-9);
            Assert.True(Parallel3.IsParallel(belowThreshold, GeoVector3.XAxis, fine));
        }

        [Fact]
        public void PerpendicularityUsesTheAngularThreshold()
        {
            GeoVector3 axis = GeoVector3.XAxis;
            GeoVector3 almostSquare = new GeoVector3(0.0, 1.0, 0.0).RotateBy(0.5 * Math.PI / 180.0, GeoVector3.ZAxis);
            GeoVector3 clearlyNot = new GeoVector3(1.0, 1.0, 0.0);

            Assert.True(Parallel3.IsPerpendicular(axis, almostSquare));
            Assert.False(Parallel3.IsPerpendicular(axis, clearlyNot));
        }

        [Fact]
        public void ALineIsParallelToAPlaneWhenItsDirectionIsSquareToTheNormal()
        {
            GeoLine3 lyingIn = new GeoLine3(GeoPoint3.Origin, new GeoPoint3(1.0, 1.0, 0.0));
            GeoLine3 above = new GeoLine3(new GeoPoint3(0.0, 0.0, 5.0), new GeoPoint3(1.0, 1.0, 5.0));
            GeoLine3 standing = new GeoLine3(GeoPoint3.Origin, new GeoPoint3(0.0, 0.0, 1.0));

            Assert.True(Parallel3.IsParallel(lyingIn, GeoPlane3.XY));
            Assert.True(Parallel3.IsParallel(above, GeoPlane3.XY));
            Assert.True(Parallel3.IsPerpendicular(standing, GeoPlane3.XY));
            Assert.False(Parallel3.IsParallel(standing, GeoPlane3.XY));
        }

        [Fact]
        public void APlaneIsParallelToItselfAndToItsFlip()
        {
            Assert.True(Parallel3.IsParallel(GeoPlane3.XY, GeoPlane3.XY));
            Assert.True(Parallel3.IsParallel(GeoPlane3.XY, GeoPlane3.XY.Flip()));
            Assert.True(Parallel3.IsPerpendicular(GeoPlane3.XY, GeoPlane3.YZ));
        }

        #endregion

        #region Distance3

        [Fact]
        public void DistanceBetweenShapesIsSymmetricWhicheverWayItIsAsked()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(-3.0, 0.0, 0.0), new GeoPoint3(3.0, 0.0, 0.0));
            GeoPoint3 point = new GeoPoint3(0.0, 4.0, 0.0);

            Assert.Equal(Distance3.DistanceTo(line, point), point.DistanceTo(line), 12);
            Assert.Equal(Distance3.DistanceTo(GeoPlane3.XY, point), point.DistanceTo(GeoPlane3.XY), 12);
        }

        [Fact]
        public void ASegmentCrossingAPlaneIsAtDistanceZero()
        {
            GeoLine3 crossing = new GeoLine3(new GeoPoint3(0.0, 0.0, -1.0), new GeoPoint3(0.0, 0.0, 1.0));
            GeoLine3 above = new GeoLine3(new GeoPoint3(0.0, 0.0, 3.0), new GeoPoint3(0.0, 0.0, 7.0));

            Assert.Equal(0.0, Distance3.DistanceTo(GeoPlane3.XY, crossing), 9);
            Assert.Equal(3.0, Distance3.DistanceTo(GeoPlane3.XY, above), 9);
        }

        [Fact]
        public void ParallelPlanesKeepTheirGapAndCrossingOnesAreAtZero()
        {
            Assert.Equal(5.0, Distance3.DistanceTo(GeoPlane3.XY, GeoPlane3.XY.Offset(5.0)), 9);
            Assert.Equal(0.0, Distance3.DistanceTo(GeoPlane3.XY, GeoPlane3.YZ), 9);
        }

        [Fact]
        public void ARayIsMeasuredFromItsOriginForwards()
        {
            GeoRay3 ray = new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis);
            GeoLine3 behind = new GeoLine3(new GeoPoint3(-10.0, 0.0, 0.0), new GeoPoint3(-6.0, 0.0, 0.0));
            GeoLine3 crossing = new GeoLine3(new GeoPoint3(5.0, -1.0, 0.0), new GeoPoint3(5.0, 1.0, 0.0));

            Assert.Equal(6.0, Distance3.DistanceTo(ray, behind), 9);
            Assert.Equal(0.0, Distance3.DistanceTo(ray, crossing), 9);
        }

        #endregion

        #region Projection3

        [Fact]
        public void ProjectingOntoTheInfiniteLineDoesNotClampButProjectingOntoTheSegmentDoes()
        {
            GeoLine3 line = new GeoLine3(GeoPoint3.Origin, new GeoPoint3(10.0, 0.0, 0.0));
            GeoPoint3 beyond = new GeoPoint3(25.0, 5.0, 0.0);

            Assert.True(Projection3.ProjectToInfiniteLine(line, beyond).IsEqualTo(new GeoPoint3(25.0, 0.0, 0.0)));
            Assert.True(Projection3.ProjectToLine(line, beyond).IsEqualTo(new GeoPoint3(10.0, 0.0, 0.0)));
        }

        [Fact]
        public void ProjectingIntoABoxLeavesInteriorPointsWhereTheyAreButTheSurfaceVariantDoesNot()
        {
            GeoObb3 box = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 10.0);
            GeoPoint3 inside = new GeoPoint3(1.0, 2.0, 3.0);

            Assert.True(Projection3.ProjectToObb(box, inside).IsEqualTo(inside));

            GeoPoint3 onSurface = Projection3.ProjectToObbSurface(box, inside);
            Assert.Equal(PointLocation.OnSide, box.Locate(onSurface));

            // The nearest face is the one the point has least room to: +Z at 5, two units away.
            Assert.True(onSurface.IsEqualTo(new GeoPoint3(1.0, 2.0, 5.0)));
        }

        [Fact]
        public void ProjectingOutsideABoxAgreesBetweenBothVariants()
        {
            GeoObb3 box = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 10.0);
            GeoPoint3 outside = new GeoPoint3(20.0, 0.0, 0.0);

            Assert.True(Projection3.ProjectToObb(box, outside).IsEqualTo(Projection3.ProjectToObbSurface(box, outside)));
            Assert.True(Projection3.ProjectToObb(box, outside).IsEqualTo(new GeoPoint3(5.0, 0.0, 0.0)));
        }

        [Fact]
        public void ProjectingOntoAPolygonBoundaryAlwaysLandsOnAnEdge()
        {
            GeoPolygon3 square = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0));

            GeoPoint3 middle = new GeoPoint3(5.0, 5.0, 0.0);

            Assert.True(Projection3.ProjectToPolygon(square, middle).IsEqualTo(middle));
            Assert.Equal(PointLocation.OnSide, square.Locate(Projection3.ProjectToPolygonBoundary(square, middle)));
        }

        [Fact]
        public void TheClosestConnectingSegmentIsSymmetricInLength()
        {
            GeoLine3 a = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(10.0, 0.0, 0.0));
            GeoLine3 b = new GeoLine3(new GeoPoint3(3.0, 7.0, 2.0), new GeoPoint3(8.0, 9.0, -4.0));

            Assert.Equal(
                Projection3.GetClosestSegment(a, b).Length,
                Projection3.GetClosestSegment(b, a).Length,
                9);
        }

        [Fact]
        public void TheClosestSegmentBetweenTwoPointsIsTheGapBetweenThem()
        {
            GeoLine3 dot1 = new GeoLine3(GeoPoint3.Origin, GeoPoint3.Origin);
            GeoLine3 dot2 = new GeoLine3(new GeoPoint3(3.0, 4.0, 0.0), new GeoPoint3(3.0, 4.0, 0.0));

            Assert.Equal(5.0, Projection3.GetClosestSegment(dot1, dot2).Length, 9);
        }

        #endregion

        #region Intersection3

        [Fact]
        public void GetIntersectionReturnsNullWhereTryIntersectReturnsFalse()
        {
            GeoLine3 a = new GeoLine3(new GeoPoint3(-5.0, 0.0, 0.0), new GeoPoint3(5.0, 0.0, 0.0));
            GeoLine3 crossing = new GeoLine3(new GeoPoint3(0.0, -5.0, 0.0), new GeoPoint3(0.0, 5.0, 0.0));
            GeoLine3 skew = new GeoLine3(new GeoPoint3(0.0, -5.0, 9.0), new GeoPoint3(0.0, 5.0, 9.0));

            Assert.NotNull(Intersection3.GetIntersection(a, crossing));
            Assert.Null(Intersection3.GetIntersection(a, skew));
        }

        [Fact]
        public void TheLineWhereTwoPlanesMeetLiesOnBothOfThem()
        {
            GeoPlane3 first = new GeoPlane3(new GeoPoint3(1.0, 2.0, 3.0), new GeoVector3(1.0, 1.0, 0.0));
            GeoPlane3 second = new GeoPlane3(new GeoPoint3(-4.0, 0.0, 2.0), new GeoVector3(0.0, 1.0, 1.0));

            Assert.True(Intersection3.TryIntersectWith(first, second, out GeoRay3 line));

            for (double distance = -20.0; distance <= 20.0; distance += 5.0)
            {
                GeoPoint3 sample = line.GetPointAtDistance(distance);

                Assert.True(first.IsPointOn(sample));
                Assert.True(second.IsPointOn(sample));
            }
        }

        [Fact]
        public void APlaneCuttingACubeMeetsItsEdgesAtFourCorners()
        {
            GeoSolid3 cube = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0)).ToObb().ToSolid();

            GeoPoint3[] hits = Intersection3.GetIntersections(GeoPlane3.XY.Offset(5.0), cube);

            Assert.Equal(4, hits.Length);

            foreach (GeoPoint3 hit in hits)
            {
                Assert.Equal(5.0, hit.Z, 9);
            }
        }

        [Fact]
        public void APlaneMissingASolidMeetsNothing()
        {
            GeoSolid3 cube = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0)).ToObb().ToSolid();

            Assert.Empty(Intersection3.GetIntersections(GeoPlane3.XY.Offset(50.0), cube));
        }

        #endregion

        #region Instance methods mirror the static ones

        [Fact]
        public void EveryMirroredMethodAgreesWithTheStaticOneItForwardsTo()
        {
            GeoPoint3 point = new GeoPoint3(2.0, 3.0, 4.0);
            GeoLine3 line = new GeoLine3(GeoPoint3.Origin, new GeoPoint3(10.0, 0.0, 0.0));
            GeoRay3 ray = new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis);
            GeoTriangle3 triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(4.0, 0.0, 0.0), new GeoPoint3(0.0, 3.0, 0.0));

            Assert.Equal(Distance3.DistanceTo(line, point), line.DistanceTo(point), 12);
            Assert.Equal(Distance3.DistanceTo(ray, point), ray.DistanceTo(point), 12);
            Assert.Equal(Distance3.DistanceTo(triangle, point), triangle.DistanceTo(point), 12);

            Assert.Equal(Containment3.IsPointOn(line, point), line.IsPointOn(point));
            Assert.Equal(Containment3.IsPointOn(line, point), point.IsPointOn(line));
            Assert.Equal(Containment3.Locate(triangle, point), triangle.Locate(point));
            Assert.Equal(Containment3.Locate(triangle, point), point.LocateIn(triangle));

            Assert.Equal(Parallel3.IsParallel(line, line), line.IsParallelTo(line));
            Assert.Equal(Projection3.ProjectToLine(line, point), line.GetClosestPointOnBoundary(point));
            Assert.Equal(Projection3.ProjectToLine(line, point), point.GetClosestPointOnBoundary(line));
        }

        [Fact]
        public void TheDefaultOverloadAgreesWithPassingTheGlobalTolerance()
        {
            GeoLine3 line = new GeoLine3(GeoPoint3.Origin, new GeoPoint3(10.0, 0.0, 0.0));
            GeoPoint3 point = new GeoPoint3(5.0, 1.0, 0.0);

            Assert.Equal(
                Containment3.IsPointOn(line, point),
                Containment3.IsPointOn(line, point, Tolerance.Global));

            Assert.Equal(
                Distance3.DistanceTo(line, point),
                Distance3.DistanceTo(line, point, Tolerance.Global),
                12);
        }

        #endregion
    }
}
