using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A mirror across a plane or a line at 45 degrees swaps two coordinates, and so lands on whole numbers where the
    /// points were whole: (0, 10) across the line y = x comes to (10, 0). The reflection matrix was built from the unit
    /// normal (0.70710678118654757, -0.70710678118654757), whose square is not quite a half, so every entry carried the
    /// last digit of that and a point came back as 9.999999999999957.
    /// </summary>
    /// <remarks>
    /// The numbers asserted with <see cref="Assert.Equal(double, double)"/> are exact on purpose: they are what a swap of
    /// two coordinates gives. The turned and tilted mirrors are held to 1E-9, and to their own inverse.
    /// </remarks>
    public class MirrorExactTests
    {
        private static readonly Tolerance Tight = new Tolerance(
            1E-9,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-9
        );

        [Theory]
        [InlineData(0, 10, 10, 0)]
        [InlineData(7, 10, 10, 7)]
        [InlineData(-3, 250, 250, -3)]
        [InlineData(123456, 654321, 654321, 123456)]
        public void ThePlaneThroughTheOriginAt45Degrees_SwapsTheTwoCoordinatesExactly(
            double x,
            double y,
            double expectedX,
            double expectedY
        )
        {
            // The plane x = y has the normal (1, -1, 0), and so does the one a viewer builds from the way (1, 1).
            GeoTransform3 mirror = GeoTransform3.Mirror(new GeoPlane3(GeoPoint3.Origin, new GeoVector3(1, -1, 0)));

            GeoPoint3 mirrored = mirror.Transform(new GeoPoint3(x, y, 5));

            Assert.Equal(expectedX, mirrored.X);
            Assert.Equal(expectedY, mirrored.Y);
            Assert.Equal(5.0, mirrored.Z);
        }

        [Fact]
        public void ThePlaneOfAUnitNormalAlreadyNormalizedByTheCaller_SwapsTheTwoCoordinatesExactly()
        {
            // The viewer normalizes the normal itself, and the plane normalizes it again.
            GeoVector3 normal = new GeoVector3(-1, 1, 0).Normalize();
            GeoTransform3 mirror = GeoTransform3.Mirror(new GeoPlane3(GeoPoint3.Origin, normal));

            GeoPoint3 mirrored = mirror.Transform(new GeoPoint3(0, 10, 0));

            Assert.Equal(10.0, mirrored.X);
            Assert.Equal(0.0, mirrored.Y);
        }

        [Fact]
        public void ThePlaneThroughAPointAt45Degrees_PutsAPointExactlyWhereTheSwapDoes()
        {
            // The plane y = x - 50 sends (x, y) to (y + 50, x - 50).
            GeoTransform3 mirror = GeoTransform3.Mirror(
                new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, -1, 0))
            );

            GeoPoint3 mirrored = mirror.Transform(new GeoPoint3(0, 10, 0));

            Assert.Equal(60.0, mirrored.X);
            Assert.Equal(-50.0, mirrored.Y);
        }

        [Fact]
        public void ThePlaneAt45DegreesAboutAnotherAxis_SwapsExactlyToo()
        {
            // Normal (0, 1, -1): the plane z = y + 0 through (4, 20, 20) swaps y and z.
            GeoTransform3 mirror = GeoTransform3.Mirror(
                new GeoPlane3(new GeoPoint3(4, 20, 20), new GeoVector3(0, 1, -1))
            );

            GeoPoint3 mirrored = mirror.Transform(new GeoPoint3(1, 30, 10));

            Assert.Equal(1.0, mirrored.X);
            Assert.Equal(10.0, mirrored.Y);
            Assert.Equal(30.0, mirrored.Z);
        }

        [Fact]
        public void MirroringTwiceAcrossAPlaneAt45Degrees_GivesTheSamePointsBack()
        {
            GeoTransform3 mirror = GeoTransform3.Mirror(
                new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, -1, 0))
            );

            GeoPoint3 twice = mirror.Transform(mirror.Transform(new GeoPoint3(12, 345, 6)));

            Assert.Equal(12.0, twice.X);
            Assert.Equal(345.0, twice.Y);
            Assert.Equal(6.0, twice.Z);
        }

        [Fact]
        public void ThePlanesOfTheAxes_StayExact()
        {
            GeoPoint3 p = new GeoPoint3(12, 345, 6);

            GeoPoint3 inX = GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(50, 0, 0), GeoVector3.XAxis)).Transform(p);
            GeoPoint3 inY = GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(0, 50, 0), GeoVector3.YAxis)).Transform(p);
            GeoPoint3 inZ = GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(0, 0, 50), GeoVector3.ZAxis)).Transform(p);

            Assert.Equal(88.0, inX.X);
            Assert.Equal(345.0, inX.Y);
            Assert.Equal(6.0, inX.Z);
            Assert.Equal(-245.0, inY.Y);
            Assert.Equal(94.0, inZ.Z);
        }

        [Theory]
        [InlineData(30)]
        [InlineData(37.5)]
        [InlineData(121)]
        public void ATurnedPlane_KeepsADistanceAndComesBackOnItself(double degrees)
        {
            double a = degrees * Math.PI / 180.0;
            GeoPlane3 plane = new GeoPlane3(
                new GeoPoint3(1000, -2000, 300),
                new GeoVector3(Math.Cos(a), Math.Sin(a), 0.3)
            );
            GeoTransform3 mirror = GeoTransform3.Mirror(plane);
            GeoPoint3 p = new GeoPoint3(12345, -6789, 4321);

            GeoPoint3 mirrored = mirror.Transform(p);

            Assert.Equal(
                plane.Normal.DotProduct(plane.Origin.GetVectorTo(p)),
                -plane.Normal.DotProduct(plane.Origin.GetVectorTo(mirrored)),
                9
            );
            Assert.True(mirror.Transform(mirrored).IsEqualTo(p, Tight));
        }

        [Theory]
        [InlineData(0, 10, 10, 0)]
        [InlineData(7, 10, 10, 7)]
        [InlineData(123456, 654321, 654321, 123456)]
        public void TheLineAt45Degrees_SwapsTheTwoCoordinatesExactly(
            double x,
            double y,
            double expectedX,
            double expectedY
        )
        {
            GeoTransform2 mirror = GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 1)));

            GeoPoint2 mirrored = mirror.Transform(new GeoPoint2(x, y));

            Assert.Equal(expectedX, mirrored.X);
            Assert.Equal(expectedY, mirrored.Y);
        }

        [Fact]
        public void TheLineThroughAPointAt45Degrees_PutsAPointExactlyWhereTheSwapDoes()
        {
            // The line y = x - 50 sends (x, y) to (y + 50, x - 50); its other end is far from the first.
            GeoTransform2 mirror = GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(50, 0), new GeoPoint2(1050, 1000)));

            GeoPoint2 mirrored = mirror.Transform(new GeoPoint2(0, 10));

            Assert.Equal(60.0, mirrored.X);
            Assert.Equal(-50.0, mirrored.Y);
        }

        [Fact]
        public void ALineOfAnyLength_StillCarriesItsLine()
        {
            GeoTransform2 mirror = GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(7E-6, 7E-6)));

            GeoPoint2 mirrored = mirror.Transform(new GeoPoint2(1, 0));

            Assert.True(mirrored.IsEqualTo(new GeoPoint2(0, 1), Tight));
        }

        [Fact]
        public void ATurnedLine_KeepsADistanceAndComesBackOnItself()
        {
            GeoLine2 line = new GeoLine2(new GeoPoint2(100, -200), new GeoPoint2(1100, 400));
            GeoTransform2 mirror = GeoTransform2.Mirror(line);
            GeoPoint2 p = new GeoPoint2(12345, -6789);

            GeoPoint2 mirrored = mirror.Transform(p);

            Assert.True(mirror.Transform(mirrored).IsEqualTo(p, Tight));
            Assert.Equal(p.DistanceTo(new GeoPoint2(100, -200)), mirrored.DistanceTo(new GeoPoint2(100, -200)), 9);
        }
    }
}
