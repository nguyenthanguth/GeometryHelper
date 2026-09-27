using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A size, a distance or a tolerance that is not a number is refused, the way a negative one is.
    /// </summary>
    /// <remarks>
    /// Every comparison with NaN is false, so a check written as "if the radius is below nought, throw" let a
    /// NaN radius through, and the shape built from it answered NaN or false to every question without a word.
    /// A direction worked the same way: a vector of NaNs had a length that was not "too short", so it was
    /// normalised into another vector of NaNs and a plane was built on it.
    /// </remarks>
    public class NotANumberTests
    {
        public static TheoryData<double> NotSizes => new TheoryData<double> { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1.0 };

        [Theory]
        [MemberData(nameof(NotSizes))]
        public void AShapeRefusesASizeThatIsNotOne(double size)
        {
            Assert.ThrowsAny<ArgumentException>(() => new GeoCircle2(new GeoPoint2(0, 0), size));
            Assert.ThrowsAny<ArgumentException>(() => new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, size));
            Assert.ThrowsAny<ArgumentException>(() => new GeoObb3(GeoPoint3.Origin, size, 1, 1));
            Assert.ThrowsAny<ArgumentException>(() => new GeoObb3(GeoPoint3.Origin, 1, size, 1));
            Assert.ThrowsAny<ArgumentException>(() => new GeoObb3(GeoPoint3.Origin, 1, 1, size));
            Assert.ThrowsAny<ArgumentException>(() => new GeoRectangle2(new GeoPoint2(0, 0), size, 1));
            Assert.ThrowsAny<ArgumentException>(() => new GeoRectangle2(new GeoPoint2(0, 0), 1, size));
            Assert.ThrowsAny<ArgumentException>(() => new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis).ToLine(size));
        }

        [Theory]
        [MemberData(nameof(NotSizes))]
        public void AToleranceThatIsNotOneIsRefused(double value)
        {
            Assert.ThrowsAny<ArgumentException>(() => new Tolerance(value, 1E-4));
            Assert.ThrowsAny<ArgumentException>(() => new Tolerance(1E-4, value));
            Assert.ThrowsAny<ArgumentException>(() => new Tolerance(1E-4, 1E-4, value));
            Assert.ThrowsAny<ArgumentException>(() => new Tolerance(1E-4, 1E-4, 0.01, value));
        }

        [Fact]
        public void ARectangleRefusesAnAngleThatIsNotANumber()
        {
            Assert.ThrowsAny<ArgumentException>(() => new GeoRectangle2(new GeoPoint2(0, 0), 1, 1, double.NaN));
            Assert.ThrowsAny<ArgumentException>(() => new GeoRectangle2(new GeoPoint2(0, 0), 1, 1, double.PositiveInfinity));
        }

        [Fact]
        public void AVectorOfNaNsHasNoDirection()
        {
            Assert.False(new GeoVector3(double.NaN, 0, 0).TryGetNormal(out _));
            Assert.False(new GeoVector3(double.PositiveInfinity, 1, 0).TryGetNormal(out _));
            Assert.False(new GeoVector2(double.NaN, 1).TryGetNormal(out _));
            Assert.ThrowsAny<ArgumentException>(() => new GeoPlane3(GeoPoint3.Origin, new GeoVector3(double.NaN, 0, 1)));
            Assert.ThrowsAny<ArgumentException>(() => new GeoRay3(GeoPoint3.Origin, new GeoVector3(0, double.NaN, 1)));
        }

        [Fact]
        public void TheLimitsThemselvesAreStillAccepted()
        {
            Assert.Equal(0.0, new GeoCircle2(new GeoPoint2(0, 0), 0.0).Radius);
            Assert.Equal(0.0, new GeoObb3(GeoPoint3.Origin, 0, 0, 0).ExtentX);
            Assert.Equal(0.0, new GeoRectangle2(new GeoPoint2(0, 0), 0, 0).Width);
            Assert.Equal(0.0, new Tolerance(0.0, 0.0, 0.0, 0.0).EqualPoint);
            Assert.True(new GeoVector3(3, 4, 0).TryGetNormal(out GeoVector3 unit));
            Assert.Equal(0.6, unit.X, 12);
        }
    }
}
