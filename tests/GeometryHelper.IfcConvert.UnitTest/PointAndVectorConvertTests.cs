using GeometryHelper.IfcConvert.Converters.Internal;
using GeometryHelper.Geometry;
using Xbim.Common.Geometry;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    public class PointAndVectorConvertTests
    {
        [Fact]
        public void PointSurvivesRoundTrip()
        {
            var original = new XbimPoint3D(1.25, -3.5, 9.75);

            GeoPoint3 converted = original.ToGeoPoint3();

            Assert.Equal(1.25, converted.X, 10);
            Assert.Equal(-3.5, converted.Y, 10);
            Assert.Equal(9.75, converted.Z, 10);

            XbimPoint3D back = converted.ToXbimPoint3D();

            Assert.Equal(original.X, back.X, 10);
            Assert.Equal(original.Y, back.Y, 10);
            Assert.Equal(original.Z, back.Z, 10);
        }

        [Fact]
        public void PointScaleFactor_ScalesCorrectly()
        {
            var original = new XbimPoint3D(1000.0, 2000.0, 3000.0);

            // Scale mm to meters (0.001)
            GeoPoint3 scaled = original.ToGeoPoint3(0.001);

            Assert.Equal(1.0, scaled.X, 10);
            Assert.Equal(2.0, scaled.Y, 10);
            Assert.Equal(3.0, scaled.Z, 10);

            // Scale back from meters to mm (1000.0)
            XbimPoint3D back = scaled.ToXbimPoint3D(1000.0);

            Assert.Equal(1000.0, back.X, 10);
            Assert.Equal(2000.0, back.Y, 10);
            Assert.Equal(3000.0, back.Z, 10);
        }

        [Fact]
        public void VectorSurvivesRoundTrip()
        {
            var original = new XbimVector3D(3.0, 4.0, 12.0);

            GeoVector3 converted = original.ToGeoVector3();

            Assert.Equal(3.0, converted.X, 10);
            Assert.Equal(4.0, converted.Y, 10);
            Assert.Equal(12.0, converted.Z, 10);
            Assert.Equal(13.0, converted.Length, 10);

            XbimVector3D back = converted.ToXbimVector3D();

            Assert.Equal(original.X, back.X, 10);
            Assert.Equal(original.Y, back.Y, 10);
            Assert.Equal(original.Z, back.Z, 10);
        }

        [Fact]
        public void VectorScaleFactor_ScalesCorrectly()
        {
            var original = new XbimVector3D(3.0, 4.0, 0.0);

            GeoVector3 scaled = original.ToGeoVector3(2.5);

            Assert.Equal(7.5, scaled.X, 10);
            Assert.Equal(10.0, scaled.Y, 10);
            Assert.Equal(0.0, scaled.Z, 10);
            Assert.Equal(12.5, scaled.Length, 10);
        }

        [Fact]
        public void ZeroCoordinates_HandleGracefully()
        {
            var zeroPt = new XbimPoint3D(0, 0, 0);
            var zeroVec = new XbimVector3D(0, 0, 0);

            GeoPoint3 convertedPt = zeroPt.ToGeoPoint3();
            GeoVector3 convertedVec = zeroVec.ToGeoVector3();

            Assert.Equal(0, convertedPt.X);
            Assert.Equal(0, convertedPt.Y);
            Assert.Equal(0, convertedPt.Z);

            Assert.Equal(0, convertedVec.Length);
        }
    }
}
