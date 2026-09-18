using System;
using GeometryHelper.IfcConvert.Converters.Internal;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Common.Geometry;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    public class MatrixConvertTests
    {
        [Fact]
        public void Identity_ConvertsCorrectly()
        {
            var identityXbim = XbimMatrix3D.Identity;
            GeoTransform3 transform = identityXbim.ToGeoTransform3();

            Assert.True(transform.IsIdentity());

            XbimMatrix3D back = transform.ToXbimMatrix3D();
            Assert.True(back.IsIdentity);
        }

        [Fact]
        public void Translation_ConvertsCorrectly()
        {
            var translationXbim = XbimMatrix3D.CreateTranslation(150.0, -250.0, 350.0);
            GeoTransform3 transform = translationXbim.ToGeoTransform3();

            Assert.Equal(150.0, transform[0, 3], 10);
            Assert.Equal(-250.0, transform[1, 3], 10);
            Assert.Equal(350.0, transform[2, 3], 10);

            XbimMatrix3D back = transform.ToXbimMatrix3D();
            Assert.Equal(150.0, back.OffsetX, 10);
            Assert.Equal(-250.0, back.OffsetY, 10);
            Assert.Equal(350.0, back.OffsetZ, 10);
        }

        [Fact]
        public void ScaleFactor_ScalesTranslationOffsets()
        {
            var translationXbim = XbimMatrix3D.CreateTranslation(1000.0, 2000.0, 3000.0);

            // mm to m
            GeoTransform3 transform = translationXbim.ToGeoTransform3(0.001);

            Assert.Equal(1.0, transform[0, 3], 10);
            Assert.Equal(2.0, transform[1, 3], 10);
            Assert.Equal(3.0, transform[2, 3], 10);

            // m to mm
            XbimMatrix3D back = transform.ToXbimMatrix3D(1000.0);
            Assert.Equal(1000.0, back.OffsetX, 10);
            Assert.Equal(2000.0, back.OffsetY, 10);
            Assert.Equal(3000.0, back.OffsetZ, 10);
        }

        [Fact]
        public void PointTransformationInvariance_AsymmetricTransformationMatches()
        {
            // Test non-symmetric composite matrix: rotation around Z + translation
            double c = Math.Cos(Math.PI / 4.0);
            double s = Math.Sin(Math.PI / 4.0);

            // xBIM row-major: rows are X, Y, Z, and row 4 is translation offset
            var compositeXbim = new XbimMatrix3D(
                c, -s, 0, 0,
                s,  c, 0, 0,
                0,  0, 1, 0,
                120.0, 240.0, -50.0, 1.0);

            GeoTransform3 geoTransform = compositeXbim.ToGeoTransform3();

            var ptXbim = new XbimPoint3D(10.0, 20.0, 30.0);
            var ptGeo = new GeoPoint3(10.0, 20.0, 30.0);

            XbimPoint3D transformedXbim = compositeXbim.Transform(ptXbim);
            GeoPoint3 transformedGeo = geoTransform.Transform(ptGeo);

            Assert.Equal(transformedXbim.X, transformedGeo.X, 6);
            Assert.Equal(transformedXbim.Y, transformedGeo.Y, 6);
            Assert.Equal(transformedXbim.Z, transformedGeo.Z, 6);

            // Verify round-trip back
            XbimMatrix3D roundtripXbim = geoTransform.ToXbimMatrix3D();
            XbimPoint3D transformedBack = roundtripXbim.Transform(ptXbim);

            Assert.Equal(transformedXbim.X, transformedBack.X, 6);
            Assert.Equal(transformedXbim.Y, transformedBack.Y, 6);
            Assert.Equal(transformedXbim.Z, transformedBack.Z, 6);
        }
    }
}
