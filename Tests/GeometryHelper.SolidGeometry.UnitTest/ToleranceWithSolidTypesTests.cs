using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest
{
    /// <summary>
    /// Covers how the shared Tolerance behaves once a solid-geometry type is measured against it. The
    /// Tolerance type itself is tested in GeometryHelper.CommonGeometry.UnitTest; what is checked here is that
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
            Tolerance original = Tolerance.Global;

            try
            {
                GeoPoint3 a = GeoPoint3.Origin;
                GeoPoint3 b = new GeoPoint3(0.01, 0.0, 0.0);

                Assert.False(a.IsEqualTo(b));

                Tolerance.Global = new Tolerance(0.1, 0.1);

                Assert.True(a.IsEqualTo(b));
            }
            finally
            {
                Tolerance.Global = original;
            }
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
