using System;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the triangle, which every other surface in the library is measured through.
    /// </summary>
    public class GeoTriangle3Tests
    {
        private static readonly GeoTriangle3 UnitTriangle = new GeoTriangle3(
            new GeoPoint3(0.0, 0.0, 0.0),
            new GeoPoint3(4.0, 0.0, 0.0),
            new GeoPoint3(0.0, 3.0, 0.0));

        [Fact]
        public void AreaMatchesTheClassicalFormula()
        {
            Assert.Equal(6.0, UnitTriangle.Area, 9);
            Assert.Equal(12.0, UnitTriangle.Perimeter, 9);
        }

        [Fact]
        public void NormalFollowsTheRightHandRuleAndFlipReversesIt()
        {
            Assert.True(UnitTriangle.Normal.IsEqualTo(GeoVector3.ZAxis));
            Assert.True(UnitTriangle.Flip().Normal.IsEqualTo(GeoVector3.ZAxis.Negate()));
            Assert.Equal(UnitTriangle.Area, UnitTriangle.Flip().Area, 9);
        }

        [Fact]
        public void CentroidIsTheAverageOfTheVertices()
        {
            Assert.True(UnitTriangle.Centroid.IsEqualTo(new GeoPoint3(4.0 / 3.0, 1.0, 0.0)));
        }

        [Fact]
        public void AreaVectorCarriesBothTheAreaAndTheNormal()
        {
            GeoVector3 areaVector = UnitTriangle.GetAreaVector();

            Assert.Equal(UnitTriangle.Area * 2.0, areaVector.Length, 9);
            Assert.True(areaVector.Normalize().IsEqualTo(UnitTriangle.Normal));
        }

        [Fact]
        public void DegenerateTrianglesAreAcceptedButReportedAsSuch()
        {
            GeoTriangle3 collinear = new GeoTriangle3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 1.0, 1.0),
                new GeoPoint3(2.0, 2.0, 2.0));

            Assert.True(collinear.IsDegenerate());
            Assert.Equal(0.0, collinear.Area, 9);
            Assert.Throws<InvalidOperationException>(() => collinear.Normal);
        }

        [Fact]
        public void IndexerAndEdgesRunAroundTheLoop()
        {
            Assert.Equal(UnitTriangle.A, UnitTriangle[0]);
            Assert.Equal(UnitTriangle.B, UnitTriangle[1]);
            Assert.Equal(UnitTriangle.C, UnitTriangle[2]);
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitTriangle[3]);

            Assert.True(UnitTriangle.GetEdgeAt(2).EndPoint.IsEqualTo(UnitTriangle.A));
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitTriangle.GetEdgeAt(3));
        }

        [Fact]
        public void BarycentricCoordinatesSumToOneAndReproduceThePoint()
        {
            GeoPoint3 inside = new GeoPoint3(1.0, 1.0, 0.0);

            Assert.True(UnitTriangle.TryGetBarycentric(inside, out double u, out double v, out double w));
            Assert.Equal(1.0, u + v + w, 9);
            Assert.True(UnitTriangle.GetPointAtBarycentric(u, v, w).IsEqualTo(inside));
        }

        [Fact]
        public void BarycentricCoordinatesOfTheVerticesArePureWeights()
        {
            Assert.True(UnitTriangle.TryGetBarycentric(UnitTriangle.A, out double u, out double v, out double w));
            Assert.Equal(1.0, u, 9);
            Assert.Equal(0.0, v, 9);
            Assert.Equal(0.0, w, 9);
        }

        [Fact]
        public void BarycentricCoordinatesGoNegativeOutsideTheTriangle()
        {
            Assert.True(UnitTriangle.TryGetBarycentric(new GeoPoint3(10.0, 10.0, 0.0), out double u, out _, out _));
            Assert.True(u < 0.0);
        }

        [Fact]
        public void BarycentricCoordinatesAreRefusedForADegenerateTriangle()
        {
            GeoTriangle3 collinear = new GeoTriangle3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0),
                new GeoPoint3(2.0, 0.0, 0.0));

            Assert.False(collinear.TryGetBarycentric(GeoPoint3.Origin, out double u, out double v, out double w));
            Assert.Equal(0.0, u);
            Assert.Equal(0.0, v);
            Assert.Equal(0.0, w);
        }

        [Fact]
        public void LocateSeparatesInteriorEdgeAndOutside()
        {
            Assert.Equal(PointLocation.Inside, UnitTriangle.Locate(new GeoPoint3(1.0, 1.0, 0.0)));
            Assert.Equal(PointLocation.OnSide, UnitTriangle.Locate(new GeoPoint3(2.0, 0.0, 0.0)));
            Assert.Equal(PointLocation.OnSide, UnitTriangle.Locate(UnitTriangle.A));
            Assert.Equal(PointLocation.OutSide, UnitTriangle.Locate(new GeoPoint3(10.0, 10.0, 0.0)));
        }

        [Fact]
        public void APointHoveringAboveTheTriangleIsOutsideIt()
        {
            Assert.Equal(PointLocation.OutSide, UnitTriangle.Locate(new GeoPoint3(1.0, 1.0, 5.0)));
            Assert.False(UnitTriangle.Contains(new GeoPoint3(1.0, 1.0, 5.0)));
        }

        [Fact]
        public void ADegenerateTriangleHoldsNothing()
        {
            GeoTriangle3 collinear = new GeoTriangle3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0),
                new GeoPoint3(2.0, 0.0, 0.0));

            Assert.Equal(PointLocation.OutSide, collinear.Locate(new GeoPoint3(1.0, 0.0, 0.0)));
        }

        [Fact]
        public void DistanceMeasuresToTheFilledSurface()
        {
            // Directly above the interior: straight down to the surface.
            Assert.Equal(5.0, UnitTriangle.DistanceTo(new GeoPoint3(1.0, 1.0, 5.0)), 9);

            // On the surface: zero.
            Assert.Equal(0.0, UnitTriangle.DistanceTo(new GeoPoint3(1.0, 1.0, 0.0)), 9);

            // Beyond a vertex: out to that vertex.
            Assert.Equal(5.0, UnitTriangle.DistanceTo(new GeoPoint3(-5.0, 0.0, 0.0)), 9);
        }

        [Fact]
        public void ClosestPointIsAlwaysOnTheTriangle()
        {
            GeoPoint3[] probes =
            {
                new GeoPoint3(1.0, 1.0, 5.0),
                new GeoPoint3(-5.0, -5.0, 0.0),
                new GeoPoint3(10.0, 0.0, 3.0),
                new GeoPoint3(0.0, 10.0, -3.0),
                new GeoPoint3(2.0, -4.0, 0.0)
            };

            foreach (GeoPoint3 probe in probes)
            {
                GeoPoint3 closest = UnitTriangle.GetClosestPointOnBoundary(probe);

                Assert.True(UnitTriangle.Contains(closest));
                Assert.Equal(UnitTriangle.DistanceTo(probe), closest.DistanceTo(probe), 9);
            }
        }

        [Fact]
        public void SegmentPiercingTheTriangleIsFoundAndOneMissingItIsNot()
        {
            GeoLine3 through = new GeoLine3(new GeoPoint3(1.0, 1.0, -5.0), new GeoPoint3(1.0, 1.0, 5.0));
            GeoLine3 beside = new GeoLine3(new GeoPoint3(9.0, 9.0, -5.0), new GeoPoint3(9.0, 9.0, 5.0));

            Assert.True(UnitTriangle.TryIntersectWith(through, out GeoPoint3 hit));
            Assert.True(hit.IsEqualTo(new GeoPoint3(1.0, 1.0, 0.0)));
            Assert.False(UnitTriangle.TryIntersectWith(beside, out _));
        }

        [Fact]
        public void RayPiercingTheTriangleIsFoundOnlyWhenAimedAtIt()
        {
            GeoRay3 towards = new GeoRay3(new GeoPoint3(1.0, 1.0, -5.0), GeoVector3.ZAxis);
            GeoRay3 away = new GeoRay3(new GeoPoint3(1.0, 1.0, -5.0), GeoVector3.ZAxis.Negate());

            Assert.True(UnitTriangle.TryIntersectWith(towards, out GeoPoint3 hit));
            Assert.True(hit.IsEqualTo(new GeoPoint3(1.0, 1.0, 0.0)));
            Assert.False(UnitTriangle.TryIntersectWith(away, out _));
        }

        [Fact]
        public void ToleranceEqualityAllowsARotationOfTheVerticesButNotAReversal()
        {
            GeoTriangle3 rotated = new GeoTriangle3(UnitTriangle.B, UnitTriangle.C, UnitTriangle.A);

            Assert.True(UnitTriangle.IsEqualTo(rotated));
            Assert.False(UnitTriangle.IsEqualTo(UnitTriangle.Flip()));
        }

        [Fact]
        public void PlaneOfTheTriangleCarriesEveryVertex()
        {
            GeoPlane3 plane = UnitTriangle.GetPlane();

            Assert.True(plane.IsPointOn(UnitTriangle.A));
            Assert.True(plane.IsPointOn(UnitTriangle.B));
            Assert.True(plane.IsPointOn(UnitTriangle.C));
        }
    }
}
