using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest
{
    public class VectorTests
    {
        [Fact]
        public void Vector_BasicOperations_WorkCorrectly()
        {
            var v1 = new GeoVector2(3.0, 4.0);
            Assert.Equal(5.0, v1.Length, 12);
            Assert.Equal(25.0, v1.LengthSquared, 12);

            Assert.True(v1.TryGetNormal(out var unit));
            Assert.Equal(1.0, unit.Length, 12);
            Assert.Equal(new GeoVector2(0.6, 0.8), unit);

            var v2 = new GeoVector2(1.0, -2.0);
            var sum = v1 + v2;
            Assert.Equal(new GeoVector2(4.0, 2.0), sum);

            var scaled = v1 * 2.0;
            Assert.Equal(new GeoVector2(6.0, 8.0), scaled);
        }

        [Fact]
        public void Vector_AnglesAndProducts_WorkCorrectly()
        {
            var v1 = GeoVector2.XAxis;
            var v2 = GeoVector2.YAxis;

            Assert.Equal(0.0, v1.DotProduct(v2), 12);
            Assert.Equal(1.0, v1.CrossProduct(v2), 12);

            Assert.Equal(Math.PI / 2.0, v1.GetAngleTo(v2), 12);
            Assert.Equal(Math.PI / 2.0, v1.GetSignedAngleTo(v2), 12);
            Assert.Equal(-Math.PI / 2.0, v2.GetSignedAngleTo(v1), 12);

            Assert.True(v1.IsPerpendicularTo(v2));
            Assert.False(v1.IsParallelTo(v2));
        }

        [Fact]
        public void Vector_ZeroLengthNormalization_ReturnsFalseAndZero()
        {
            var zeroVec = GeoVector2.Zero;
            Assert.False(zeroVec.TryGetNormal(out var normal));
            Assert.Equal(GeoVector2.Zero, normal);
        }

        [Fact]
        public void Vector_DotAndCrossProductEdgeCases_WorkCorrectly()
        {
            var v1 = new GeoVector2(3.0, 4.0);

            // Dot product with itself = squared length
            Assert.Equal(v1.LengthSquared, v1.DotProduct(v1), 12);

            // Dot product with opposite vector = negative squared length
            var vInv = v1 * -1.0;
            Assert.Equal(-v1.LengthSquared, v1.DotProduct(vInv), 12);

            // Cross product of 2 parallel vectors must be 0
            Assert.Equal(0.0, v1.CrossProduct(vInv), 12);
            Assert.Equal(0.0, v1.CrossProduct(v1), 12);
        }

        [Fact]
        public void Vector_AngleEdgeCases_WorkCorrectly()
        {
            var v1 = new GeoVector2(2.0, 2.0);
            var v2 = new GeoVector2(-2.0, -2.0); // Opposite direction

            // Angle between 2 opposite vectors is PI (180 degrees)
            Assert.Equal(Math.PI, v1.GetAngleTo(v2), 12);

            // Angle between 2 collinear vectors in the same direction is 0
            Assert.Equal(0.0, v1.GetAngleTo(v1), 12);
        }

        [Fact]
        public void Vector_ParallelAndPerpendicularTolerance_WorksCorrectly()
        {
            var v1 = new GeoVector2(1.0, 0.0);
            // A vector very close to perpendicular (deviates by 1e-4 radians)
            var vNearlyPerpendicular = new GeoVector2(1e-4, 1.0);

            Assert.True(v1.IsPerpendicularTo(vNearlyPerpendicular, new Tolerance(1e-9, 1e-9, 1e-3)));
            Assert.False(v1.IsPerpendicularTo(vNearlyPerpendicular, new Tolerance(1e-9, 1e-9, 1e-5)));

            // A vector very close to parallel (deviates by 1e-4 radians)
            var vNearlyParallel = new GeoVector2(1.0, 1e-4);
            Assert.True(v1.IsParallelTo(vNearlyParallel, new Tolerance(1e-9, 1e-9, 1e-3)));
            Assert.False(v1.IsParallelTo(vNearlyParallel, new Tolerance(1e-9, 1e-9, 1e-5)));
        }

        [Fact]
        public void Vector_RotateBy_RotatesCounterClockwise()
        {
            var v = GeoVector2.XAxis;

            Assert.True(v.RotateBy(Math.PI / 2.0).IsEqualTo(GeoVector2.YAxis));
            Assert.True(v.RotateBy(Math.PI).IsEqualTo(new GeoVector2(-1.0, 0.0)));
            Assert.True(v.RotateBy(2.0 * Math.PI).IsEqualTo(v));
            Assert.True(v.RotateBy(0.0).IsEqualTo(v));
        }

        [Fact]
        public void Vector_RotateBy_PreservesLength()
        {
            var v = new GeoVector2(3.0, 4.0);
            var rotated = v.RotateBy(0.7);

            Assert.Equal(v.Length, rotated.Length, 12);
        }

        [Fact]
        public void Vector_GetPerpendicularVector_TurnsNinetyDegreesCounterClockwise()
        {
            var v = new GeoVector2(3.0, 4.0);
            var perpendicular = v.GetPerpendicularVector();

            Assert.Equal(new GeoVector2(-4.0, 3.0), perpendicular);
            Assert.Equal(0.0, v.DotProduct(perpendicular), 12);
            Assert.Equal(v.Length, perpendicular.Length, 12);
        }

        [Fact]
        public void Vector_Operators_WorkCorrectly()
        {
            var v = new GeoVector2(4.0, -6.0);

            Assert.Equal(new GeoVector2(-4.0, 6.0), -v);
            Assert.Equal(new GeoVector2(2.0, -3.0), v / 2.0);
            Assert.Equal(new GeoVector2(8.0, -12.0), 2.0 * v);
            Assert.Equal(new GeoVector2(8.0, -12.0), v * 2.0);
            Assert.Equal(new GeoVector2(3.0, -6.0), v - new GeoVector2(1.0, 0.0));
        }

        [Fact]
        public void Vector_Equality_AndHashCode_WorkCorrectly()
        {
            var a = new GeoVector2(1.5, -2.5);
            var b = new GeoVector2(1.5, -2.5);
            var c = new GeoVector2(1.5, 2.5);

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            object notAVector = "not a GeoVector2";
            Assert.True(a.Equals((object)b));
            Assert.False(a.Equals(notAVector));

            Assert.True(a != c);
        }

        [Fact]
        public void Vector_Normalize_ThrowsForZeroLength()
        {
            Assert.Throws<InvalidOperationException>(() => GeoVector2.Zero.Normalize());
        }

        [Fact]
        public void Vector_AngleFromZeroLengthVector_Throws()
        {
            var v = GeoVector2.XAxis;

            Assert.Throws<InvalidOperationException>(() => GeoVector2.Zero.GetAngleTo(v));
            Assert.Throws<InvalidOperationException>(() => v.GetAngleTo(GeoVector2.Zero));
            Assert.Throws<InvalidOperationException>(() => GeoVector2.Zero.GetSignedAngleTo(v));
            Assert.Throws<InvalidOperationException>(() => v.GetSignedAngleTo(GeoVector2.Zero));
        }

        [Fact]
        public void Vector_GetAngleTo_StaysAccurateNearCollinear()
        {
            // Acos suffers severe loss of precision near ±1; Atan2 formula must preserve sufficient digits.
            var v = new GeoVector2(2.0, 2.0);

            Assert.Equal(Math.PI, v.GetAngleTo(-v), 12);
            Assert.Equal(0.0, v.GetAngleTo(v * 3.0), 12);
            Assert.Equal(Math.PI, v.GetAngleTo(v * -7.5), 12);
        }

        [Fact]
        public void Vector_GetAngleTo_IsSymmetric_ButSignedAngleIsNot()
        {
            var a = new GeoVector2(1.0, 0.0);
            var b = new GeoVector2(1.0, 1.0);

            Assert.Equal(a.GetAngleTo(b), b.GetAngleTo(a), 12);
            Assert.Equal(Math.PI / 4.0, a.GetAngleTo(b), 12);

            Assert.Equal(Math.PI / 4.0, a.GetSignedAngleTo(b), 12);
            Assert.Equal(-Math.PI / 4.0, b.GetSignedAngleTo(a), 12);
        }
    }
}

