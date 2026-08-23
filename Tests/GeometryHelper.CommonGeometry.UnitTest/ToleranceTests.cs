using System;
using GeometryHelper.CommonGeometry;
using Xunit;

namespace GeometryHelper.CommonGeometry.UnitTest
{
    public class ToleranceTests
    {
        [Fact]
        public void Tolerance_Constructor_StoresBothThresholds()
        {
            var tolerance = new Tolerance(1e-3, 1e-5);

            Assert.Equal(1e-3, tolerance.EqualPoint);
            Assert.Equal(1e-5, tolerance.EqualVector);
        }

        [Fact]
        public void Tolerance_ZeroThresholds_AreAllowed()
        {
            // Zero tolerance means exact comparison ??valid, not an error.
            var tolerance = new Tolerance(0.0, 0.0);

            Assert.Equal(0.0, tolerance.EqualPoint);
            Assert.Equal(0.0, tolerance.EqualVector);
        }

        [Theory]
        [InlineData(-1e-9, 1e-4)]
        [InlineData(1e-4, -1e-9)]
        [InlineData(-1.0, -1.0)]
        public void Tolerance_NegativeThreshold_Throws(double equalPoint, double equalVector)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tolerance(equalPoint, equalVector));
        }

        [Fact]
        public void Tolerance_Equality_ComparesBothThresholds()
        {
            var a = new Tolerance(1e-4, 1e-4);
            var b = new Tolerance(1e-4, 1e-4);
            var differsOnVector = new Tolerance(1e-4, 1e-3);

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());

            Assert.False(a == differsOnVector);
            Assert.True(a != differsOnVector);
            Assert.False(a.Equals(differsOnVector));
        }

        [Fact]
        public void Tolerance_Equals_ReturnsFalseForOtherTypes()
        {
            var tolerance = new Tolerance(1e-4, 1e-4);
            object notATolerance = "not a Tolerance";

            Assert.False(tolerance.Equals(notATolerance));
            Assert.False(tolerance.Equals(null));
        }

        [Fact]
        public void Tolerance_Global_UsesDocumentedDefaults()
        {
            Assert.Equal(Tolerance.DefaultEqualPoint, Tolerance.Global.EqualPoint);
            Assert.Equal(Tolerance.DefaultEqualVector, Tolerance.Global.EqualVector);
        }

        [Fact]
        public void Tolerance_ToString_UsesInvariantCulture()
        {
            var tolerance = new Tolerance(0.5, 0.25, 0.017);

            // Must always use a decimal point, independent of the runner machine's locale.
            // EqualPlanar follows EqualPoint on this constructor, because coplanarity is measured as a
            // distance. The plane library never reads it; it is here because Tolerance is shared with
            // SolidGeometry, which does.
            Assert.Equal("(EqualPoint: 0.5, EqualVector: 0.25, EqualAngleRad: 0.017, EqualPlanar: 0.5)", tolerance.ToString());
        }
    
        #region Cases that came from the SolidGeometry copy of this type

        [Fact]
        public void GlobalToleranceUsesTheDocumentedDefaults()
        {
            Tolerance global = Tolerance.Global;

            Assert.Equal(Tolerance.DefaultEqualPoint, global.EqualPoint);
            Assert.Equal(Tolerance.DefaultEqualVector, global.EqualVector);
            Assert.Equal(Tolerance.DefaultEqualAngleRad, global.EqualAngleRad);
            Assert.Equal(Tolerance.DefaultEqualPlanar, global.EqualPlanar);
        }

        [Fact]
        public void PlanarThresholdFollowsThePointThresholdOnTheThreeArgumentConstructor()
        {
            Tolerance tolerance = new Tolerance(0.5, 0.25, 0.1);

            Assert.Equal(0.5, tolerance.EqualPlanar);
        }

        [Theory]
        [InlineData(-1.0, 1.0, 1.0, 1.0)]
        [InlineData(1.0, -1.0, 1.0, 1.0)]
        [InlineData(1.0, 1.0, -1.0, 1.0)]
        [InlineData(1.0, 1.0, 1.0, -1.0)]
        public void NegativeThresholdsAreRejected(double point, double vector, double angle, double planar)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tolerance(point, vector, angle, planar));
        }

        [Fact]
        public void EqualityComparesEveryThreshold()
        {
            Tolerance a = new Tolerance(1E-3, 1E-3, 0.01, 1E-3);
            Tolerance b = new Tolerance(1E-3, 1E-3, 0.01, 1E-3);
            Tolerance c = new Tolerance(1E-3, 1E-3, 0.01, 1E-2);

            Assert.True(a == b);
            Assert.True(a != c);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        #endregion
    }
}
