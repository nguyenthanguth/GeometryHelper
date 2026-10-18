using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two triangles standing at the point tolerance collide alike from either side. The pair here, 90 000 from the origin, stands
    /// 0.00099999999999998 apart: asked of the first it collided, asked of the second it did not, because the closest-point
    /// search that decided it rounded one way round and the other the other.
    /// </summary>
    public class Collision3TrianglesAtTheToleranceTests
    {
        private static double H(long bits) => BitConverter.Int64BitsToDouble(bits);

        private static GeoTriangle3 First() => new GeoTriangle3(
            new GeoPoint3(H(0x405aae1951acc121), H(unchecked((long)0xc086c48cf071a588)), H(unchecked((long)0xc08d1c27eeebd321))),
            new GeoPoint3(H(0x403941dd5758a9cc), H(unchecked((long)0xc08550863f3ad945)), H(unchecked((long)0xc08c06e2f707dcbb))),
            new GeoPoint3(H(0x40507f963ab58bd2), H(unchecked((long)0xc0860a779e489643)), H(unchecked((long)0xc08c9186adc745e5))));

        private static GeoTriangle3 Second() => new GeoTriangle3(
            new GeoPoint3(H(0x405c5f49344f67ab), H(unchecked((long)0xc0838d7f5e0afd59)), H(unchecked((long)0xc08cc97d167281a5))),
            new GeoPoint3(H(0x4067c1dd43b94a8a), H(unchecked((long)0xc08542a47794d143)), H(unchecked((long)0xc08dd8ee038bd96a))),
            new GeoPoint3(H(0x404d33f4660e3da7), H(unchecked((long)0xc086250e67b3c09c)), H(unchecked((long)0xc08c7f4a4925a209))));

        private static Tolerance At(double point) => new Tolerance(point, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, point);

        [Fact]
        public void TwoTrianglesAtTheTolerance_CollideFromEitherSide()
        {
            Tolerance tolerance = At(0.001);

            Assert.True(First().CollidesWith(Second(), tolerance));
            Assert.True(Second().CollidesWith(First(), tolerance));
        }

        [Fact]
        public void TwoTrianglesAHairBeyondTheTolerance_StayApartFromEitherSide()
        {
            Tolerance tolerance = At(0.001 - 1E-9);

            Assert.False(First().CollidesWith(Second(), tolerance));
            Assert.False(Second().CollidesWith(First(), tolerance));
        }
    }
}
