using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Covers how the shared Tolerance behaves once a solid-geometry type is measured against it. The
    /// Tolerance type itself is tested in GeometryHelper.UnitTest.Common; what is checked here is that
    /// GeoPoint3 reads the thresholds the caller set, including the global one.
    /// </summary>
    public class ToleranceWithSolidTypesTests
    {
        [Fact]
        public void PointsWithinToleranceCompareEqualAndOutsideItDoNot()
        {
            GeoPoint3 origin = GeoPoint3.Origin;
            Tolerance tolerance = new Tolerance(0.1, 0.1);

            Assert.True(origin.IsEqualTo(new GeoPoint3(0.05, 0.0, 0.0), tolerance));
            Assert.False(origin.IsEqualTo(new GeoPoint3(0.2, 0.0, 0.0), tolerance));
        }

        [Fact]
        public void WideningTheGlobalToleranceChangesWhatCountsAsEqual()
        {
            // Five hundredths apart: beyond the default hundredth, within a tenth. The wider tolerance is a scope on
            // this thread, which the overloads without a tolerance read as the global one; setting the process-wide
            // value would change it under every test running beside this one.
            GeoPoint3 a = GeoPoint3.Origin;
            GeoPoint3 b = new GeoPoint3(0.05, 0.0, 0.0);

            Assert.False(a.IsEqualTo(b));

            using (Tolerance.Use(new Tolerance(0.1, 0.1)))
            {
                Assert.True(a.IsEqualTo(b));
            }

            Assert.False(a.IsEqualTo(b));
        }

        [Fact]
        public void ExactEqualityIsSeparateFromToleranceEquality()
        {
            GeoPoint3 a = GeoPoint3.Origin;
            GeoPoint3 b = new GeoPoint3(1E-9, 0.0, 0.0);

            Assert.True(a.IsEqualTo(b));
            Assert.False(a.Equals(b));
            Assert.False(a == b);
        }
    }
}
