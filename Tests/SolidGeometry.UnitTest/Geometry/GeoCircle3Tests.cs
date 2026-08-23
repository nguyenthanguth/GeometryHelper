using System;
using CommonGeometry;
using CommonGeometry.Enums;
using SolidGeometry;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the circular disc, the second planar region alongside the polygon.
    /// </summary>
    public class GeoCircle3Tests
    {
        private static readonly GeoCircle3 UnitDisc = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 5.0);

        [Fact]
        public void MeasurementsFollowTheRadius()
        {
            Assert.Equal(10.0, UnitDisc.Diameter, 9);
            Assert.Equal(Math.PI * 25.0, UnitDisc.Area, 9);
            Assert.Equal(2.0 * Math.PI * 5.0, UnitDisc.Length, 9);
        }

        [Fact]
        public void ANonPositiveRadiusIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, -1.0));
        }

        [Fact]
        public void ADegenerateNormalIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new GeoCircle3(GeoPoint3.Origin, GeoVector3.Zero, 1.0));
        }

        [Fact]
        public void EveryPointOnTheCircumferenceIsAtTheRadiusAndOnThePlane()
        {
            for (int i = 0; i < 16; i++)
            {
                GeoPoint3 point = UnitDisc.GetPointAtParameter(i / 16.0);

                Assert.Equal(UnitDisc.Radius, UnitDisc.Center.DistanceTo(point), 9);
                Assert.True(UnitDisc.GetPlane().IsPointOn(point));
                Assert.Equal(PointLocation.OnSide, UnitDisc.Locate(point));
            }
        }

        [Fact]
        public void TheParameterWrapsAroundTheCircumference()
        {
            Assert.True(UnitDisc.GetPointAtParameter(0.0).IsEqualTo(UnitDisc.GetPointAtParameter(1.0)));
            Assert.True(UnitDisc.GetPointAtParameter(0.25).IsEqualTo(UnitDisc.GetPointAtParameter(1.25)));
        }

        [Fact]
        public void LocateSeparatesInteriorRimAndOutside()
        {
            Assert.Equal(PointLocation.Inside, UnitDisc.Locate(GeoPoint3.Origin));
            Assert.Equal(PointLocation.OnSide, UnitDisc.Locate(new GeoPoint3(5.0, 0.0, 0.0)));
            Assert.Equal(PointLocation.OutSide, UnitDisc.Locate(new GeoPoint3(6.0, 0.0, 0.0)));
        }

        [Fact]
        public void APointHoveringAboveTheDiscIsOutsideIt()
        {
            Assert.Equal(PointLocation.OutSide, UnitDisc.Locate(new GeoPoint3(0.0, 0.0, 1.0)));
        }

        [Fact]
        public void DistanceMeasuresToTheFilledDisc()
        {
            // Above the middle: straight down.
            Assert.Equal(4.0, UnitDisc.DistanceTo(new GeoPoint3(0.0, 0.0, 4.0)), 9);

            // Beyond the rim in the plane: out to the rim.
            Assert.Equal(3.0, UnitDisc.DistanceTo(new GeoPoint3(8.0, 0.0, 0.0)), 9);

            // On the surface: zero.
            Assert.Equal(0.0, UnitDisc.DistanceTo(new GeoPoint3(1.0, 1.0, 0.0)), 9);
        }

        [Fact]
        public void ClosestPointOnTheRimIsAtTheRadius()
        {
            GeoPoint3 closest = UnitDisc.GetClosestPointOnBoundary(new GeoPoint3(20.0, 0.0, 7.0));

            Assert.True(closest.IsEqualTo(new GeoPoint3(5.0, 0.0, 0.0)));
        }

        [Fact]
        public void APointOnTheAxisStillGetsAPointOnTheRim()
        {
            GeoPoint3 closest = UnitDisc.GetClosestPointOnBoundary(new GeoPoint3(0.0, 0.0, 10.0));

            Assert.Equal(UnitDisc.Radius, UnitDisc.Center.DistanceTo(closest), 9);
        }

        [Fact]
        public void ApproximatingAsAPolygonInscribesItAndConvergesOnTheArea()
        {
            GeoPolygon3 coarse = UnitDisc.ToPolygon(6);
            GeoPolygon3 fine = UnitDisc.ToPolygon(2000);

            Assert.Equal(6, coarse.VertexCount);
            Assert.True(coarse.Area < UnitDisc.Area);
            Assert.True(fine.Area < UnitDisc.Area);
            Assert.Equal(UnitDisc.Area, fine.Area, 3);
            Assert.True(fine.Normal.IsEqualTo(UnitDisc.Normal));
        }

        [Fact]
        public void FewerThanThreeSegmentsIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitDisc.ToPolygon(2));
        }

        [Fact]
        public void BoundingBoxIsFullWidthAcrossTheNormalAndFlatAlongIt()
        {
            GeoAabb3 bounds = UnitDisc.GetAabb();

            Assert.Equal(10.0, bounds.SizeX, 9);
            Assert.Equal(10.0, bounds.SizeY, 9);
            Assert.Equal(0.0, bounds.SizeZ, 9);
        }

        [Fact]
        public void BoundingBoxOfATiltedDiscEnclosesEveryPointOnItsRim()
        {
            GeoCircle3 tilted = new GeoCircle3(new GeoPoint3(1.0, 2.0, 3.0), new GeoVector3(1.0, 1.0, 1.0), 4.0);
            GeoAabb3 bounds = tilted.GetAabb();

            for (int i = 0; i < 64; i++)
            {
                Assert.True(bounds.Contains(tilted.GetPointAtParameter(i / 64.0), new Tolerance(1E-6, 1E-6)));
            }
        }
    }
}
