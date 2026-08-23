using System;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the transformation matrix and the local coordinate system built on it.
    /// </summary>
    public class GeoTransform3Tests
    {
        #region Coordinate system

        [Fact]
        public void AxesAreOrthonormalEvenWhenTheInputIsSkewed()
        {
            GeoCoordinateSystem3 system = new GeoCoordinateSystem3(
                new GeoPoint3(1.0, 2.0, 3.0),
                new GeoVector3(2.0, 0.0, 0.0),
                new GeoVector3(3.0, 4.0, 0.0));

            Assert.True(system.XAxis.IsEqualTo(GeoVector3.XAxis));
            Assert.True(system.YAxis.IsEqualTo(GeoVector3.YAxis));
            Assert.True(system.ZAxis.IsEqualTo(GeoVector3.ZAxis));
        }

        [Fact]
        public void ParallelOrDegenerateAxesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new GeoCoordinateSystem3(
                GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.XAxis));
            Assert.Throws<ArgumentException>(() => new GeoCoordinateSystem3(
                GeoPoint3.Origin, GeoVector3.Zero, GeoVector3.YAxis));
        }

        [Fact]
        public void ToLocalAndToGlobalAreInverses()
        {
            GeoCoordinateSystem3 system = new GeoCoordinateSystem3(
                new GeoPoint3(10.0, -20.0, 30.0),
                new GeoVector3(1.0, 1.0, 0.0),
                new GeoVector3(-1.0, 1.0, 1.0));

            GeoPoint3 point = new GeoPoint3(3.0, -7.0, 11.0);

            Assert.True(system.ToGlobal(system.ToLocal(point)).IsEqualTo(point));
            Assert.True(system.ToLocal(system.ToGlobal(point)).IsEqualTo(point));
        }

        [Fact]
        public void AVectorIgnoresTheOriginWhileAPointDoesNot()
        {
            GeoCoordinateSystem3 shifted = new GeoCoordinateSystem3(
                new GeoPoint3(100.0, 100.0, 100.0), GeoVector3.XAxis, GeoVector3.YAxis);

            GeoVector3 vector = new GeoVector3(1.0, 2.0, 3.0);

            Assert.True(shifted.ToLocal(vector).IsEqualTo(vector));
            Assert.True(shifted.ToLocal(new GeoPoint3(1.0, 2.0, 3.0)).IsEqualTo(new GeoPoint3(-99.0, -98.0, -97.0)));
        }

        [Fact]
        public void ConvertingToLocalPreservesDistances()
        {
            GeoCoordinateSystem3 system = new GeoCoordinateSystem3(
                new GeoPoint3(5.0, 5.0, 5.0),
                new GeoVector3(1.0, 2.0, 3.0),
                new GeoVector3(3.0, -1.0, 0.0));

            GeoPoint3 a = new GeoPoint3(1.0, 2.0, 3.0);
            GeoPoint3 b = new GeoPoint3(-4.0, 8.0, 0.5);

            Assert.Equal(a.DistanceTo(b), system.ToLocal(a).DistanceTo(system.ToLocal(b)), 9);
        }

        [Fact]
        public void ASystemBuiltFromAPlaneHasThatPlaneAsItsXYPlane()
        {
            GeoPlane3 plane = new GeoPlane3(new GeoPoint3(1.0, 2.0, 3.0), new GeoVector3(1.0, 1.0, 1.0));
            GeoCoordinateSystem3 system = new GeoCoordinateSystem3(plane);

            Assert.True(system.ZAxis.IsEqualTo(plane.Normal));
            Assert.True(system.GetPlane().IsEqualTo(plane));
        }

        [Fact]
        public void TheGlobalSystemIsTheIdentityMapping()
        {
            GeoPoint3 point = new GeoPoint3(1.0, 2.0, 3.0);

            Assert.True(GeoCoordinateSystem3.Global.ToLocal(point).IsEqualTo(point));
            Assert.True(GeoCoordinateSystem3.Global.ToTransform().IsIdentity());
        }

        #endregion

        #region Transformation

        [Fact]
        public void TheIdentityLeavesEverythingWhereItIs()
        {
            GeoPoint3 point = new GeoPoint3(1.0, 2.0, 3.0);

            Assert.True(GeoTransform3.Identity.Transform(point).IsEqualTo(point));
            Assert.True(GeoTransform3.Identity.IsIdentity());
            Assert.Equal(1.0, GeoTransform3.Identity.GetDeterminant(), 9);
        }

        [Fact]
        public void TranslationMovesPointsButNotVectors()
        {
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(10.0, 0.0, 0.0));

            Assert.True(move.Transform(GeoPoint3.Origin).IsEqualTo(new GeoPoint3(10.0, 0.0, 0.0)));
            Assert.True(move.Transform(GeoVector3.XAxis).IsEqualTo(GeoVector3.XAxis));
        }

        [Fact]
        public void RotationsAroundTheWorldAxesFollowTheRightHandRule()
        {
            double quarter = Math.PI / 2.0;

            Assert.True(GeoTransform3.RotationZ(quarter).Transform(GeoVector3.XAxis).IsEqualTo(GeoVector3.YAxis));
            Assert.True(GeoTransform3.RotationX(quarter).Transform(GeoVector3.YAxis).IsEqualTo(GeoVector3.ZAxis));
            Assert.True(GeoTransform3.RotationY(quarter).Transform(GeoVector3.ZAxis).IsEqualTo(GeoVector3.XAxis));
        }

        [Fact]
        public void RotationAroundAnArbitraryAxisAgreesWithTheVectorMethod()
        {
            GeoVector3 axis = new GeoVector3(1.0, 2.0, 3.0);
            GeoVector3 vector = new GeoVector3(4.0, -5.0, 6.0);
            double angle = 0.83;

            Assert.True(GeoTransform3.RotationAxis(axis, angle).Transform(vector)
                .IsEqualTo(vector.RotateBy(angle, axis)));
        }

        [Fact]
        public void RotationAroundAnOffsetAxisLeavesThatAxisFixed()
        {
            GeoPoint3 pivot = new GeoPoint3(10.0, 0.0, 0.0);
            GeoTransform3 spin = GeoTransform3.RotationAxis(pivot, GeoVector3.ZAxis, 1.1);

            Assert.True(spin.Transform(pivot).IsEqualTo(pivot));
            Assert.True(spin.Transform(pivot.Add(new GeoVector3(0.0, 0.0, 5.0)))
                .IsEqualTo(pivot.Add(new GeoVector3(0.0, 0.0, 5.0))));
        }

        [Fact]
        public void RotationPreservesLengthsAndAngles()
        {
            GeoTransform3 spin = GeoTransform3.RotationAxis(new GeoVector3(1.0, 1.0, 1.0), 0.7);
            GeoVector3 a = new GeoVector3(3.0, 0.0, 0.0);
            GeoVector3 b = new GeoVector3(0.0, 4.0, 0.0);

            Assert.Equal(a.Length, spin.Transform(a).Length, 9);
            Assert.Equal(a.GetAngleTo(b), spin.Transform(a).GetAngleTo(spin.Transform(b)), 9);
            Assert.Equal(1.0, spin.GetDeterminant(), 9);
        }

        [Fact]
        public void ScalingMultipliesTheDeterminantByTheVolumeFactor()
        {
            Assert.Equal(8.0, GeoTransform3.Scaling(2.0).GetDeterminant(), 9);
            Assert.Equal(24.0, GeoTransform3.Scaling(2.0, 3.0, 4.0).GetDeterminant(), 9);
        }

        [Fact]
        public void ScalingAboutACentreLeavesThatCentreFixed()
        {
            GeoPoint3 centre = new GeoPoint3(5.0, -5.0, 5.0);
            GeoTransform3 grow = GeoTransform3.Scaling(centre, 3.0);

            Assert.True(grow.Transform(centre).IsEqualTo(centre));
            Assert.True(grow.Transform(centre.Add(GeoVector3.XAxis)).IsEqualTo(centre.Add(GeoVector3.XAxis.Multiply(3.0))));
        }

        [Fact]
        public void MirroringLeavesThePlaneFixedAndReversesHandedness()
        {
            GeoTransform3 mirror = GeoTransform3.Mirror(GeoPlane3.XY);

            Assert.True(mirror.Transform(new GeoPoint3(1.0, 2.0, 0.0)).IsEqualTo(new GeoPoint3(1.0, 2.0, 0.0)));
            Assert.True(mirror.Transform(new GeoPoint3(1.0, 2.0, 3.0)).IsEqualTo(new GeoPoint3(1.0, 2.0, -3.0)));
            Assert.Equal(-1.0, mirror.GetDeterminant(), 9);
        }

        [Fact]
        public void MirroringAcrossAnOffsetPlaneReflectsThroughIt()
        {
            GeoTransform3 mirror = GeoTransform3.Mirror(GeoPlane3.XY.Offset(5.0));

            Assert.True(mirror.Transform(new GeoPoint3(0.0, 0.0, 7.0)).IsEqualTo(new GeoPoint3(0.0, 0.0, 3.0)));
            Assert.True(mirror.Transform(new GeoPoint3(0.0, 0.0, 5.0)).IsEqualTo(new GeoPoint3(0.0, 0.0, 5.0)));
        }

        [Fact]
        public void MirroringTwiceReturnsToTheStart()
        {
            GeoTransform3 mirror = GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(1.0, 1.0, 1.0), new GeoVector3(1.0, 2.0, 3.0)));

            Assert.True(mirror.Multiply(mirror).IsIdentity());
        }

        [Fact]
        public void MultiplicationAppliesTheRightHandSideFirst()
        {
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(10.0, 0.0, 0.0));
            GeoTransform3 spin = GeoTransform3.RotationZ(Math.PI / 2.0);

            // Rotate first, then move: the point ends up at the translation offset.
            Assert.True(move.Multiply(spin).Transform(GeoPoint3.Origin).IsEqualTo(new GeoPoint3(10.0, 0.0, 0.0)));

            // Move first, then rotate: the offset is carried around by the rotation.
            Assert.True(spin.Multiply(move).Transform(GeoPoint3.Origin).IsEqualTo(new GeoPoint3(0.0, 10.0, 0.0)));

            Assert.Equal(move.Multiply(spin), move * spin);
        }

        [Fact]
        public void TheInverseUndoesTheTransformation()
        {
            GeoTransform3 combined = GeoTransform3.Translation(new GeoVector3(3.0, -4.0, 5.0))
                .Multiply(GeoTransform3.RotationAxis(new GeoVector3(1.0, 2.0, 3.0), 0.9))
                .Multiply(GeoTransform3.Scaling(2.0, 3.0, 4.0));

            GeoPoint3 point = new GeoPoint3(7.0, -8.0, 9.0);

            Assert.True(combined.Inverse().Transform(combined.Transform(point)).IsEqualTo(point));
            Assert.True(combined.Multiply(combined.Inverse()).IsIdentity());
        }

        [Fact]
        public void ATransformationThatFlattensSpaceCannotBeInverted()
        {
            GeoTransform3 flatten = GeoTransform3.Scaling(1.0, 1.0, 0.0);

            Assert.Equal(0.0, flatten.GetDeterminant(), 9);
            Assert.False(flatten.TryGetInverse(out _));
            Assert.Throws<InvalidOperationException>(() => flatten.Inverse());
        }

        [Fact]
        public void APlaneNormalStaysPerpendicularUnderNonUniformScaling()
        {
            // This is what the inverse transpose is for: applying the matrix itself to the normal would
            // leave it tilted with respect to the transformed surface.
            GeoPlane3 plane = new GeoPlane3(GeoPoint3.Origin, new GeoVector3(1.0, 1.0, 0.0));
            GeoTransform3 stretch = GeoTransform3.Scaling(4.0, 1.0, 1.0);

            GeoPlane3 moved = stretch.Transform(plane);

            plane.GetAxes(out GeoVector3 u, out GeoVector3 v);

            Assert.True(moved.Normal.IsPerpendicularTo(stretch.Transform(u)));
            Assert.True(moved.Normal.IsPerpendicularTo(stretch.Transform(v)));
        }

        [Fact]
        public void TransformingAShapeMovesEveryPartOfIt()
        {
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(1.0, 2.0, 3.0));
            GeoLine3 line = new GeoLine3(GeoPoint3.Origin, new GeoPoint3(10.0, 0.0, 0.0));
            GeoTriangle3 triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(1.0, 0.0, 0.0), new GeoPoint3(0.0, 1.0, 0.0));

            Assert.Equal(line.Length, move.Transform(line).Length, 9);
            Assert.Equal(triangle.Area, move.Transform(triangle).Area, 9);
            Assert.True(move.Transform(line).StartPoint.IsEqualTo(new GeoPoint3(1.0, 2.0, 3.0)));
        }

        [Fact]
        public void AMatrixMustBeFourByFour()
        {
            Assert.Throws<ArgumentException>(() => new GeoTransform3(new double[3, 3]));
            Assert.Throws<ArgumentNullException>(() => new GeoTransform3(null));
        }

        [Fact]
        public void TheMatrixIsCopiedOnConstructionSoLaterEditsDoNotLeakIn()
        {
            double[,] raw = new double[4, 4];
            raw[0, 0] = 1.0; raw[1, 1] = 1.0; raw[2, 2] = 1.0; raw[3, 3] = 1.0;

            GeoTransform3 transform = new GeoTransform3(raw);
            raw[0, 3] = 999.0;

            Assert.Equal(0.0, transform[0, 3], 9);
            Assert.True(transform.IsIdentity());
        }

        #endregion
    }
}
