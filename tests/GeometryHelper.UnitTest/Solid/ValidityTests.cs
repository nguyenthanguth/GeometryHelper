using System;
using System.Linq;
using System.Threading;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Telling a shape from a default value, a polygon crossing itself from the region it covers, and a tolerance
    /// set for a while on one thread.
    /// </summary>
    public class ValidityTests
    {
        [Fact]
        public void ADefaultValueWithNoDirectionOrRadiusIsNotValid()
        {
            Assert.False(default(GeoPlane3).IsValid);
            Assert.False(default(GeoRay3).IsValid);
            Assert.False(default(GeoCoordinateSystem2).IsValid);
            Assert.False(default(GeoCoordinateSystem3).IsValid);
            Assert.False(default(GeoArc2).IsValid);
            Assert.False(default(GeoArc3).IsValid);
            Assert.False(default(GeoCircle3).IsValid);
            Assert.All(new GeoPlane3[3], plane => Assert.False(plane.IsValid));
        }

        [Fact]
        public void ADefaultValueThatIsAShapeAnywayIsValid()
        {
            // The origin, a point-sized segment, a circle of no radius: each a shape a constructor makes too.
            Assert.True(default(GeoPoint2).IsValid);
            Assert.True(default(GeoPoint3).IsValid);
            Assert.True(default(GeoVector3).IsValid);
            Assert.True(default(GeoLine2).IsValid);
            Assert.True(default(GeoLine3).IsValid);
            Assert.True(default(GeoCircle2).IsValid);
            Assert.True(default(GeoRectangle2).IsValid);
            Assert.True(default(GeoTriangle3).IsValid);
            Assert.True(default(GeoEdge2).IsValid);
            Assert.True(default(GeoEdge3).IsValid);
            Assert.True(default(GeoAabb3).IsValid);
        }

        [Fact]
        public void EveryConstructedValueIsValid()
        {
            var p = new GeoPoint3(1, 2, 3);

            Assert.True(new GeoPlane3(p, new GeoVector3(1, 1, 0)).IsValid);
            Assert.True(new GeoRay3(p, new GeoVector3(0, 3, 4)).IsValid);
            Assert.True(new GeoCoordinateSystem3(p, new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 1)).IsValid);
            Assert.True(new GeoCoordinateSystem2(new GeoPoint2(1, 2), 0.7).IsValid);
            Assert.True(new GeoArc2(new GeoPoint2(0, 0), 5, 0.2, 1.4).IsValid);
            Assert.True(GeoArc3.FromThreePoints(new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0), new GeoPoint3(-1, 0, 0)).IsValid);
            Assert.True(new GeoCircle3(p, new GeoVector3(0, 0, 2), 4).IsValid);
            Assert.True(new GeoCircle2(new GeoPoint2(1, 1), 3).IsValid);
            Assert.True(new GeoEdge3(p, new GeoPoint3(5, 2, 3), 0.5, GeoVector3.ZAxis).IsValid);
            Assert.True(new GeoRectangle2(new GeoPoint2(0, 0), 4, 2, 0.3).IsValid);
            Assert.True(new GeoAabb3(p, new GeoPoint3(4, 5, 6)).IsValid);
        }

        [Fact]
        public void ANumberThatIsNotOneMakesAPointOrAVectorInvalid()
        {
            Assert.False(new GeoPoint3(double.NaN, 0, 0).IsValid);
            Assert.False(new GeoPoint2(0, double.PositiveInfinity).IsValid);
            Assert.False(new GeoVector3(0, double.NaN, 0).IsValid);
            Assert.False(new GeoLine3(GeoPoint3.Origin, new GeoPoint3(double.NaN, 1, 1)).IsValid);
        }

        [Fact]
        public void APolygonCrossingItselfIsMadeIntoTheRegionItCovers()
        {
            // A bow tie: two triangles of 25 meeting at a point. The shoelace sum sets one against the other.
            var bowTie = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 10), new GeoPoint2(10, 0), new GeoPoint2(0, 10));

            Assert.False(bowTie.IsSimple());
            Assert.Equal(0.0, bowTie.Area, 9);

            GeoFace2[] region = bowTie.MakeValid();

            Assert.Equal(2, region.Length);
            Assert.Equal(50.0, region.Sum(face => face.Area), 6);
            Assert.Equal(bowTie.Union(bowTie).Sum(face => face.Area), region.Sum(face => face.Area), 6);
        }

        [Fact]
        public void ASimplePolygonIsMadeIntoItself()
        {
            var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

            GeoFace2 face = Assert.Single(square.MakeValid());

            Assert.Equal(100.0, face.Area, 9);
            Assert.Empty(face.Holes);
        }

        [Fact]
        public void AToleranceScopeHoldsOnThisThreadUntilItIsDisposed()
        {
            // Five hundredths apart: beyond the default hundredth, within the loose tenth.
            Tolerance before = Tolerance.Global;
            var loose = new Tolerance(1E-1, 1E-1);
            var looser = new Tolerance(1.0, 1.0);
            var a = new GeoPoint3(0, 0, 0);
            var b = new GeoPoint3(5E-2, 0, 0);

            Assert.False(a.IsEqualTo(b));

            using (Tolerance.Use(loose))
            {
                Assert.Equal(loose, Tolerance.Global);
                Assert.True(a.IsEqualTo(b));

                using (Tolerance.Use(looser))
                {
                    Assert.Equal(looser, Tolerance.Global);
                }

                Assert.Equal(loose, Tolerance.Global);

                // Another thread keeps the process-wide setting.
                Tolerance elsewhere = default;
                var other = new Thread(() => elsewhere = Tolerance.Global);
                other.Start();
                other.Join();
                Assert.Equal(before, elsewhere);
            }

            Assert.Equal(before, Tolerance.Global);
            Assert.False(a.IsEqualTo(b));
        }

        [Fact]
        public void AScopeIsClosedOnTheThreadThatOpenedItAndOnlyOnce()
        {
            Tolerance before = Tolerance.Global;
            IDisposable scope = Tolerance.Use(new Tolerance(1E-3, 1E-3));

            Exception elsewhere = null;
            var other = new Thread(() => elsewhere = Record.Exception(() => scope.Dispose()));
            other.Start();
            other.Join();
            Assert.IsType<InvalidOperationException>(elsewhere);

            scope.Dispose();
            scope.Dispose();

            Assert.Equal(before, Tolerance.Global);
        }
    }
}
