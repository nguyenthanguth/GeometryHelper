using System;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the polyline: an open chain that encloses nothing and need not be planar.
    /// </summary>
    public class GeoPolyline3Tests
    {
        private static GeoPolyline3 MakeLShape() => new GeoPolyline3(
            new GeoPoint3(0.0, 0.0, 0.0),
            new GeoPoint3(3.0, 0.0, 0.0),
            new GeoPoint3(3.0, 4.0, 0.0));

        [Fact]
        public void LengthIsTheSumOfTheSegments()
        {
            Assert.Equal(7.0, MakeLShape().Length, 9);
            Assert.Equal(3, MakeLShape().VertexCount);
            Assert.Equal(2, MakeLShape().EdgeCount);
        }

        [Fact]
        public void ConsecutiveDuplicateVerticesAreDropped()
        {
            GeoPolyline3 polyline = new GeoPolyline3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 1E-9));

            Assert.Equal(2, polyline.VertexCount);
            Assert.Equal(1.0, polyline.Length, 9);
        }

        [Fact]
        public void FewerThanTwoDistinctVerticesAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoPolyline3(new GeoPoint3(1.0, 1.0, 1.0)));
            Assert.Throws<ArgumentException>(() => new GeoPolyline3(
                new GeoPoint3(1.0, 1.0, 1.0),
                new GeoPoint3(1.0, 1.0, 1.0)));
            Assert.Throws<ArgumentNullException>(() => new GeoPolyline3((System.Collections.Generic.IEnumerable<GeoPoint3>)null));
        }

        [Fact]
        public void NoZeroLengthEdgeSurvivesConstruction()
        {
            GeoPolyline3 polyline = new GeoPolyline3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(5.0, 0.0, 0.0));

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                Assert.False(polyline.GetEdgeAt(i).IsDegenerate());
            }
        }

        [Fact]
        public void ParametrizationClampsAtBothEndsBecauseAnOpenChainCannotExtend()
        {
            GeoPolyline3 polyline = MakeLShape();

            Assert.True(polyline.GetPointAtParameter(-1.0).IsEqualTo(polyline.StartPoint));
            Assert.True(polyline.GetPointAtParameter(2.0).IsEqualTo(polyline.EndPoint));
            Assert.True(polyline.GetPointAtDistance(-5.0).IsEqualTo(polyline.StartPoint));
            Assert.True(polyline.GetPointAtDistance(100.0).IsEqualTo(polyline.EndPoint));
        }

        [Fact]
        public void PointAtDistanceWalksThroughTheCorner()
        {
            GeoPolyline3 polyline = MakeLShape();

            Assert.True(polyline.GetPointAtDistance(0.0).IsEqualTo(new GeoPoint3(0.0, 0.0, 0.0)));
            Assert.True(polyline.GetPointAtDistance(3.0).IsEqualTo(new GeoPoint3(3.0, 0.0, 0.0)));
            Assert.True(polyline.GetPointAtDistance(5.0).IsEqualTo(new GeoPoint3(3.0, 2.0, 0.0)));
            Assert.True(polyline.GetPointAtDistance(7.0).IsEqualTo(new GeoPoint3(3.0, 4.0, 0.0)));
        }

        [Fact]
        public void ParameterAndDistanceRoundTrip()
        {
            GeoPolyline3 polyline = MakeLShape();
            GeoPoint3 probe = new GeoPoint3(3.0, 2.0, 0.0);

            Assert.Equal(5.0, polyline.GetDistanceAtPoint(probe), 9);
            Assert.Equal(5.0 / 7.0, polyline.GetParameterAtPoint(probe), 9);
            Assert.True(polyline.GetPointAtParameter(polyline.GetParameterAtPoint(probe)).IsEqualTo(probe));
        }

        [Fact]
        public void ReverseKeepsTheLengthAndSwapsTheEnds()
        {
            GeoPolyline3 polyline = MakeLShape();
            GeoPolyline3 reversed = polyline.Reverse();

            Assert.Equal(polyline.Length, reversed.Length, 9);
            Assert.True(reversed.StartPoint.IsEqualTo(polyline.EndPoint));
            Assert.True(reversed.EndPoint.IsEqualTo(polyline.StartPoint));
            Assert.True(polyline.IsEqualTo(reversed));
        }

        [Fact]
        public void ACurveHoldsOnlyThePointsOnItsPath()
        {
            // A chain tracing a square still has no interior.
            GeoPolyline3 traced = new GeoPolyline3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0),
                new GeoPoint3(0.0, 0.0, 0.0));

            GeoPoint3 middle = new GeoPoint3(5.0, 5.0, 0.0);

            Assert.False(traced.IsPointOn(middle));
            Assert.Equal(5.0, traced.DistanceTo(middle), 9);

            // Converted to a region, the same middle point is inside and at distance zero.
            Assert.True(traced.ToPolygon().Contains(middle));
            Assert.Equal(0.0, traced.ToPolygon().DistanceTo(middle), 9);
        }

        [Fact]
        public void ANonPlanarChainIsAcceptedAndReportsItself()
        {
            GeoPolyline3 skew = new GeoPolyline3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0),
                new GeoPoint3(1.0, 1.0, 0.0),
                new GeoPoint3(1.0, 1.0, 1.0));

            Assert.False(skew.IsPlanar());
            Assert.False(skew.TryGetPlane(out _));
        }

        [Fact]
        public void APlanarChainReportsItsCarrierPlane()
        {
            GeoPolyline3 flat = MakeLShape();

            Assert.True(flat.IsPlanar());
            Assert.True(flat.TryGetPlane(out GeoPlane3 plane));

            foreach (GeoPoint3 vertex in flat.Vertices)
            {
                Assert.True(plane.IsPointOn(vertex));
            }
        }

        [Fact]
        public void ACollinearChainHasNoSinglePlane()
        {
            GeoPolyline3 straight = new GeoPolyline3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 1.0, 1.0),
                new GeoPoint3(2.0, 2.0, 2.0));

            Assert.False(straight.TryGetPlane(out _));
        }

        [Fact]
        public void ClosestPointOnTheChainIsOnOneOfItsSegments()
        {
            GeoPolyline3 polyline = MakeLShape();
            GeoPoint3 probe = new GeoPoint3(5.0, 2.0, 3.0);

            GeoPoint3 closest = polyline.GetClosestPointOnBoundary(probe);

            Assert.True(polyline.IsPointOn(closest));
            Assert.Equal(polyline.DistanceTo(probe), closest.DistanceTo(probe), 9);
        }

        [Fact]
        public void VerticesAreReadOnlyFromOutside()
        {
            GeoPolyline3 polyline = MakeLShape();

            Assert.IsNotType<System.Collections.Generic.List<GeoPoint3>>(polyline.Vertices);
            Assert.Throws<ArgumentOutOfRangeException>(() => polyline[polyline.VertexCount]);
        }

        [Fact]
        public void CloneIsIndependentAndEqual()
        {
            GeoPolyline3 polyline = MakeLShape();
            GeoPolyline3 copy = polyline.Clone();

            Assert.NotSame(polyline, copy);
            Assert.Equal(polyline, copy);
            Assert.Equal(polyline.Length, copy.Length, 9);
        }

        [Fact]
        public void BoundingBoxEnclosesEveryVertex()
        {
            GeoAabb3 box = MakeLShape().GetAabb();

            Assert.True(box.Min.IsEqualTo(new GeoPoint3(0.0, 0.0, 0.0)));
            Assert.True(box.Max.IsEqualTo(new GeoPoint3(3.0, 4.0, 0.0)));
        }
    }
}
