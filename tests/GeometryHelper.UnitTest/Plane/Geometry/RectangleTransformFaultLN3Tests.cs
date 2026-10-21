using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A rectangle is moved where its corners land within the global point tolerance of where the transformation puts
    /// them, and refused as a parallelogram otherwise, at any size.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoTransform2.Transform(GeoRectangle2)"/> compared the lengths of the two axes, and their dot product, with
    /// the global vector tolerance outright, a number with no size: a shear of 5E-6 scaled by 1E5 moved the rectangle 100 by
    /// 50 with its corners 17.7 off, and a skew at a thousandth of the scale, which a circle is refused under, was let
    /// through. It reads the axes as a circle as large as its half diagonal does now.
    /// </remarks>
    public class RectangleTransformFaultLN3Tests
    {
        private static readonly GeoRectangle2 Plate = new GeoRectangle2(new GeoPoint2(1000, 2000), 100, 50, 0.3);

        private static GeoTransform2 Map(double a, double b, double c, double d) => new GeoTransform2(new double[,] { { a, b, 0 }, { c, d, 0 }, { 0, 0, 1 } });

        private static double WorstCorner(GeoRectangle2 rectangle, GeoTransform2 map)
        {
            GeoPoint2[] wanted = rectangle.ToPolygon().Vertices.Select(map.Transform).ToArray();
            GeoPoint2[] got = rectangle.TransformBy(map).ToPolygon().Vertices.ToArray();

            return wanted.Max(w => got.Min(g => g.DistanceTo(w)));
        }

        [Theory]
        [InlineData(1000.0)]
        [InlineData(1E5)]
        public void AShearOf5EMinus6Scaled_IsRefused(double scale)
        {
            GeoTransform2 map = Map(scale, 5E-6 * scale, 0, scale);

            Assert.Throws<InvalidOperationException>(() => map.Transform(Plate));
            Assert.Throws<InvalidOperationException>(() => Plate.TransformBy(map));
        }

        [Fact]
        public void AShearOf5EMinus6AtTheScaleDrawn_MovesItWithinTheTolerance()
        {
            Assert.True(WorstCorner(Plate, Map(1, 5E-6, 0, 1)) < 1E-3);
        }

        [Fact]
        public void ASkewAtAThousandthOfTheScale_IsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Map(0.001, 0.0006, 0, 0.0008).Transform(Plate));
        }

        [Fact]
        public void ATurnRoundedToAFloatOnItsOwnScaled1E5_MovesIt()
        {
            double c = (float)Math.Cos(0.7), s = (float)Math.Sin(0.7);

            Assert.True(WorstCorner(Plate, Map(c * 1E5, -s * 1E5, s * 1E5, c * 1E5)) < 1E-6);
        }

        [Fact]
        public void AScalingToNothing_StillGivesARectangleOfNoSize()
        {
            GeoRectangle2 moved = GeoTransform2.Scaling(0.0).Transform(Plate);

            Assert.Equal(0.0, moved.Width);
            Assert.Equal(0.0, moved.Height);
        }
    }
}
