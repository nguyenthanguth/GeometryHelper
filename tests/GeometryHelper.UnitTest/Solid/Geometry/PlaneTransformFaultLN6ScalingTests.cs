using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A plane is carried under an even scaling of any size that can be undone, and so is a circle on it.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoTransform3.Transform(GeoPlane3)"/> carried the normal by the inverse transpose, which an even scaling by
    /// 100 000 makes a hundred-thousandth long, and handed it to the plane's constructor, which read it against the global
    /// vector tolerance and threw: the plane, and every circle and ball on one, was refused under an even scaling of about
    /// 1E5 or more. The normal is made of unit length by its own length now.
    /// </remarks>
    public class PlaneTransformFaultLN6ScalingTests
    {
        private static readonly GeoPlane3 Tilted = new GeoPlane3(new GeoPoint3(1, 2, 3), new GeoVector3(0.3, 0.4, 0.5));

        [Theory]
        [InlineData(1E5)]
        [InlineData(1E6)]
        [InlineData(1E8)]
        public void APlaneScaledEvenly_KeepsItsNormal(double scale)
        {
            GeoPlane3 moved = GeoTransform3.Scaling(scale).Transform(Tilted);

            Assert.Equal(0.0, moved.Normal.GetAngleTo(Tilted.Normal), 12);
            Assert.Equal(1.0, moved.Normal.Length, 14);
            Assert.True(moved.Origin.IsEqualTo(new GeoPoint3(scale, 2 * scale, 3 * scale), new Tolerance(1E-9 * scale, 1E-9, 1E-9, 1E-9 * scale)));
        }

        [Fact]
        public void ACircleScaledByAMillion_IsTheCircleAMillionTimesTheSize()
        {
            var circle = new GeoCircle3(new GeoPoint3(1, 2, 3), new GeoVector3(0.3, 0.4, 0.5), 10);

            GeoCircle3 moved = circle.TransformBy(GeoTransform3.Scaling(1E6));

            Assert.Equal(1E7, moved.Radius, 6);
            Assert.Equal(0.0, moved.Normal.GetAngleTo(circle.Normal), 12);
        }

        [Fact]
        public void APlaneScaledByAThousand_IsTheSameToTheBitAsBefore()
        {
            // Under 1E5 the normal was long enough for the constructor, and is divided by its length as it divided it.
            GeoTransform3 map = GeoTransform3.Scaling(1000);
            Assert.True(map.TryGetInverse(out GeoTransform3 inverse));
            GeoVector3 n = Tilted.Normal;
            var carried = new GeoVector3(
                inverse[0, 0] * n.X + inverse[1, 0] * n.Y + inverse[2, 0] * n.Z,
                inverse[0, 1] * n.X + inverse[1, 1] * n.Y + inverse[2, 1] * n.Z,
                inverse[0, 2] * n.X + inverse[1, 2] * n.Y + inverse[2, 2] * n.Z);
            GeoPlane3 before = new GeoPlane3(map.Transform(Tilted.Origin), carried);

            GeoPlane3 moved = map.Transform(Tilted);

            Assert.Equal(before.Normal.X, moved.Normal.X);
            Assert.Equal(before.Normal.Y, moved.Normal.Y);
            Assert.Equal(before.Normal.Z, moved.Normal.Z);
            Assert.Equal(before.Origin.X, moved.Origin.X);
        }

        [Fact]
        public void ATransformationThatCollapsesSpace_IsStillRefused()
        {
            Assert.Throws<InvalidOperationException>(() => GeoTransform3.Scaling(1, 0, 1).Transform(Tilted));
        }
    }
}
