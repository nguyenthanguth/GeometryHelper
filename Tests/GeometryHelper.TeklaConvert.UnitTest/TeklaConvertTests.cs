using System;
using System.Collections.Generic;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using GeometryHelper.TeklaConvert;
using GeometryHelper.PlaneGeometry.Geometry;
using TSG = Tekla.Structures.Geometry3d;
using Xunit;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// Covers the conversions over the geometry types of the Tekla API, which are plain data and need no
    /// running Tekla. Reading a <c>Model.Solid</c> does need one and is not covered here.
    /// </summary>
    public class TeklaConvertTests
    {
        [Fact]
        public void APointSurvivesTheRoundTrip()
        {
            var original = new TSG.Point(1.5, -2.5, 3.5);

            GeoPoint3 converted = original.ToGeoPoint3();

            Assert.Equal(1.5, converted.X, 12);
            Assert.Equal(-2.5, converted.Y, 12);
            Assert.Equal(3.5, converted.Z, 12);

            TSG.Point back = converted.ToTeklaPoint();

            Assert.Equal(original.X, back.X, 12);
            Assert.Equal(original.Y, back.Y, 12);
            Assert.Equal(original.Z, back.Z, 12);
        }

        [Fact]
        public void AVectorSurvivesTheRoundTrip()
        {
            var original = new TSG.Vector(3.0, 4.0, 12.0);

            GeoVector3 converted = original.ToGeoVector3();

            Assert.Equal(13.0, converted.Length, 12);

            TSG.Vector back = converted.ToTeklaVector();

            Assert.Equal(original.X, back.X, 12);
            Assert.Equal(original.Y, back.Y, 12);
            Assert.Equal(original.Z, back.Z, 12);
        }

        [Fact]
        public void ASegmentSurvivesTheRoundTrip()
        {
            var original = new TSG.LineSegment(new TSG.Point(0, 0, 0), new TSG.Point(3, 4, 0));

            GeoLine3 converted = original.ToGeoLine3();

            Assert.Equal(5.0, converted.Length, 12);

            TSG.LineSegment back = converted.ToTeklaLineSegment();

            Assert.Equal(original.EndPoint.X, back.EndPoint.X, 12);
            Assert.Equal(original.EndPoint.Y, back.EndPoint.Y, 12);
        }

        [Fact]
        public void APlaneKeepsItsOriginAndNormal()
        {
            var original = new TSG.GeometricPlane(new TSG.Point(1, 2, 3), new TSG.Vector(0, 0, 7));

            GeoPlane3 converted = original.ToGeoPlane3();

            // The normal is normalized on the way in, which Tekla does not insist on.
            Assert.True(converted.Normal.IsEqualTo(GeoVector3.ZAxis));
            Assert.True(converted.Origin.IsEqualTo(new GeoPoint3(1, 2, 3)));

            TSG.GeometricPlane back = converted.ToTeklaPlane();

            Assert.Equal(3.0, back.Origin.Z, 12);
        }

        [Fact]
        public void ACoordinateSystemIsSquaredUpOnTheWayIn()
        {
            // A Y axis that is not square to X: Tekla allows it, SolidGeometry does not.
            var original = new TSG.CoordinateSystem(
                new TSG.Point(10, -20, 30),
                new TSG.Vector(2, 0, 0),
                new TSG.Vector(3, 4, 0));

            GeoCoordinateSystem3 converted = original.ToGeoCoordinateSystem3();

            Assert.True(converted.XAxis.IsEqualTo(GeoVector3.XAxis));
            Assert.True(converted.YAxis.IsEqualTo(GeoVector3.YAxis));
            Assert.True(converted.ZAxis.IsEqualTo(GeoVector3.ZAxis));
            Assert.True(converted.Origin.IsEqualTo(new GeoPoint3(10, -20, 30)));

            TSG.CoordinateSystem back = converted.ToTeklaCoordinateSystem();

            Assert.Equal(10.0, back.Origin.X, 12);
            Assert.Equal(1.0, back.AxisX.X, 12);
        }

        [Fact]
        public void ABoundingBoxSurvivesTheRoundTrip()
        {
            var original = new TSG.AABB(new TSG.Point(1, 2, 3), new TSG.Point(4, 6, 9));

            GeoAabb3 converted = original.ToGeoAabb3();

            Assert.Equal(3.0, converted.SizeX, 12);
            Assert.Equal(4.0, converted.SizeY, 12);
            Assert.Equal(6.0, converted.SizeZ, 12);

            TSG.AABB back = converted.ToTeklaAabb();

            Assert.Equal(original.MinPoint.X, back.MinPoint.X, 12);
            Assert.Equal(original.MaxPoint.Z, back.MaxPoint.Z, 12);
        }

        [Fact]
        public void AnEmptyBoundingBoxCannotBeHandedToTekla()
        {
            Assert.Throws<InvalidOperationException>(() => GeoAabb3.Empty.ToTeklaAabb());
        }

        [Fact]
        public void ARunOfPointsBecomesAChain()
        {
            List<TSG.Point> points = new List<TSG.Point>
            {
                new TSG.Point(0, 0, 0),
                new TSG.Point(3, 0, 0),
                new TSG.Point(3, 4, 0)
            };

            GeoPolyline3 chain = points.ToGeoPolyline3();

            Assert.Equal(3, chain.VertexCount);
            Assert.Equal(7.0, chain.Length, 12);
        }

        #region Matrix

        /// <summary>
        /// Checks a converted matrix against the one it came from by transforming the same points through
        /// both, which is the only way to be sure the two conventions were lined up correctly rather than
        /// merely plausibly.
        /// </summary>
        private static void AssertSameTransform(TSG.Matrix tekla, GeoTransform3 converted)
        {
            TSG.Point[] probes =
            {
                new TSG.Point(0, 0, 0),
                new TSG.Point(1, 0, 0),
                new TSG.Point(0, 1, 0),
                new TSG.Point(0, 0, 1),
                new TSG.Point(7, -3, 11)
            };

            foreach (TSG.Point probe in probes)
            {
                TSG.Point expected = tekla.Transform(probe);
                GeoPoint3 actual = converted.Transform(probe.ToGeoPoint3());

                Assert.Equal(expected.X, actual.X, 9);
                Assert.Equal(expected.Y, actual.Y, 9);
                Assert.Equal(expected.Z, actual.Z, 9);
            }
        }

        [Fact]
        public void ARotationMatrixConvertsToTheSameTransformation()
        {
            TSG.Matrix rotation = TSG.MatrixFactory.Rotate(0.7, new TSG.Vector(1, 2, 3));

            AssertSameTransform(rotation, rotation.ToGeoTransform3());
        }

        [Fact]
        public void AFrameMatrixWithATranslationConvertsToTheSameTransformation()
        {
            var frame = new TSG.CoordinateSystem(
                new TSG.Point(100, -50, 25),
                new TSG.Vector(1, 1, 0),
                new TSG.Vector(-1, 1, 0));

            TSG.Matrix toFrame = TSG.MatrixFactory.ToCoordinateSystem(frame);
            TSG.Matrix fromFrame = TSG.MatrixFactory.FromCoordinateSystem(frame);

            AssertSameTransform(toFrame, toFrame.ToGeoTransform3());
            AssertSameTransform(fromFrame, fromFrame.ToGeoTransform3());
        }

        [Fact]
        public void AMatrixSurvivesTheRoundTripBackToTekla()
        {
            var frame = new TSG.CoordinateSystem(
                new TSG.Point(100, -50, 25),
                new TSG.Vector(1, 1, 0),
                new TSG.Vector(-1, 1, 0));

            TSG.Matrix original = TSG.MatrixFactory.FromCoordinateSystem(frame);
            TSG.Matrix back = original.ToGeoTransform3().ToTeklaMatrix();

            AssertSameTransform(back, original.ToGeoTransform3());
        }

        [Fact]
        public void ATransformationBuiltHereConvertsIntoOneTeklaAgreesWith()
        {
            GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(10, -20, 30))
                .Multiply(GeoTransform3.RotationZ(Math.PI / 3.0));

            AssertSameTransform(motion.ToTeklaMatrix(), motion);
        }

        #endregion

        [Fact]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ((TSG.Point)null).ToGeoPoint3());
            Assert.Throws<ArgumentNullException>(() => ((TSG.Vector)null).ToGeoVector3());
            Assert.Throws<ArgumentNullException>(() => ((TSG.Vector)null).ToGeoVector2());
            Assert.Throws<ArgumentNullException>(() => ((TSG.LineSegment)null).ToGeoLine3());
            Assert.Throws<ArgumentNullException>(() => ((TSG.Matrix)null).ToGeoTransform3());
        }

        [Fact]
        public void APoint2SurvivesTheRoundTrip()
        {
            var original = new TSG.Point(1.5, -2.5, 0.0);
            GeoPoint2 converted = original.ToGeoPoint2();

            Assert.Equal(1.5, converted.X, 12);
            Assert.Equal(-2.5, converted.Y, 12);

            TSG.Point back = converted.ToTeklaPoint();

            Assert.Equal(original.X, back.X, 12);
            Assert.Equal(original.Y, back.Y, 12);
            Assert.Equal(0.0, back.Z, 12);
        }

        [Fact]
        public void AVector2SurvivesTheRoundTrip()
        {
            var original = new TSG.Vector(3.0, 4.0, 0.0);
            GeoVector2 converted = original.ToGeoVector2();

            Assert.Equal(5.0, converted.Length, 12);

            TSG.Vector back = converted.ToTeklaVector();

            Assert.Equal(original.X, back.X, 12);
            Assert.Equal(original.Y, back.Y, 12);
            Assert.Equal(0.0, back.Z, 12);
        }
    }
}
