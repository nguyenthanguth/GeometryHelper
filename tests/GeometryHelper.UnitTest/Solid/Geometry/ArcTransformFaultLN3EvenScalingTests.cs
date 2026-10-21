using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc in space, its circle and a ball are carried or refused alike, by how far they would land from where the
    /// transformation puts their points, at any size.
    /// </summary>
    /// <remarks>
    /// The arcs carried through where their ends and their middle land compared the lengths of the two axes of their plane
    /// outright with the global vector tolerance, a number with no size, while a circle and a ball read them as a share of the
    /// stretch: an arc a thousandth or a radian short of a whole turn threw where its circle was carried, and the share let a
    /// true stretch of 3E-6 through scaled by 1 000, the circle of radius 100 landing 0.29 off. All three now ask how far the
    /// shape would land from where the transformation puts it, against the global point tolerance.
    /// </remarks>
    public class ArcTransformFaultLN3EvenScalingTests
    {
        private static readonly GeoTransform3 Turn = GeoTransform3.RotationAxis(new GeoVector3(0.3, 0.7, -0.2), 0.7);

        private static GeoTransform3 Scaled(double scale, double stretchX)
        {
            var matrix = new double[4, 4];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    bool linear = i < 3 && j < 3;
                    matrix[i, j] = Turn[i, j] * (linear ? scale : 1.0) * (linear && j == 0 ? stretchX : 1.0);
                }
            }

            return new GeoTransform3(matrix);
        }

        private static readonly GeoPoint3 Centre = new GeoPoint3(10, 20, 30);
        private static readonly GeoVector3 Normal = new GeoVector3(0.2, -0.1, 1);

        private static GeoArc3 Arc(double radius, double shortOfATurn)
            => new GeoArc3(Centre, Normal, radius, 0.3, 0.3 + 2.0 * Math.PI - shortOfATurn);

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1.0)]
        public void AStretchOf3EMinus6Scaled1000_RefusesTheCircleTheArcAndTheBall(double shortOfATurn)
        {
            GeoTransform3 map = Scaled(1000, 1 + 3E-6);

            Assert.Throws<InvalidOperationException>(() => map.Transform(new GeoCircle3(Centre, Normal, 100)));
            Assert.Throws<InvalidOperationException>(() => Arc(100, shortOfATurn).TransformBy(map));
            Assert.Throws<InvalidOperationException>(() => map.Transform(new GeoSphere3(Centre, 100)));
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1.0)]
        public void AStretchOf3EMinus6AtTheScaleDrawn_MovesTheCircleTheArcAndTheBall(double shortOfATurn)
        {
            // 0.0003 off at the most.
            GeoTransform3 map = Scaled(1, 1 + 3E-6);
            GeoArc3 arc = Arc(100, shortOfATurn);

            GeoArc3 moved = arc.TransformBy(map);

            Assert.Equal(100.0, map.Transform(new GeoCircle3(Centre, Normal, 100)).Radius, 2);
            Assert.Equal(100.0, map.Transform(new GeoSphere3(Centre, 100)).Radius, 2);
            Assert.Equal(0.0, moved.StartPoint.DistanceTo(map.Transform(arc.StartPoint)), 6);
            Assert.Equal(0.0, moved.EndPoint.DistanceTo(map.Transform(arc.EndPoint)), 6);
        }

        [Fact]
        public void AnExactTurnScaled1000_MovesTheArcWhole()
        {
            GeoTransform3 map = Scaled(1000, 1);

            GeoArc3 moved = Arc(100, 1).TransformBy(map);

            Assert.Equal(100000.0, moved.Radius, 6);
            Assert.Equal(map.Transform(new GeoCircle3(Centre, Normal, 100)).Radius, moved.Radius);
        }
    }
}
