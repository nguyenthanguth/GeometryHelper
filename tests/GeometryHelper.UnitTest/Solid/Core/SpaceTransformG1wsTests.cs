using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A curved loop and a curved chain in space are flattened corner for corner, and a coordinate system in space takes
    /// an X axis of any length but none.
    /// </summary>
    /// <remarks>
    /// Flattening put the corners back together within the global tolerance of a thousandth, which dropped the corners
    /// either side of an arc or a side a loop or a chain keeps within a finer one. A coordinate system read the global vector
    /// tolerance of 1E-5 as a length, so an X axis shorter than that was refused as having none, and a scaling by 1E-5 or
    /// less with it. No test sets the global tolerance.
    /// </remarks>
    public class SpaceTransformG1wsTests
    {
        private static readonly Tolerance Millionth = new Tolerance(1E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-6);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        [Fact]
        public void ARectangleStandingInSpaceFilletedAtFiveTenThousandthsAtAMillionthFlattened_KeepsItsEightCorners()
        {
            GeoPolygonArc3 rounded = new GeoPolygon3(P(20, 0, 0), P(20, 100, 0), P(20, 100, 50), P(20, 0, 50)).Fillet(0.0005, Millionth);

            GeoPolygon3 flat = rounded.Flatten();

            Assert.Equal(8, rounded.VertexCount);
            Assert.Equal(8, flat.VertexCount);

            for (int i = 0; i < rounded.VertexCount; i++)
            {
                Assert.Equal(rounded[i], flat[i]);
            }
        }

        [Fact]
        public void AChainInSpaceFilletedAtFiveTenThousandthsAtAMillionthFlattened_TurnsAtBothEndsOfItsArc()
        {
            GeoPolylineArc3 rounded = new GeoPolyline3(new[] { P(0, 0, 0), P(10, 0, 0), P(10, 0, 10) }).Fillet(0.0005, Millionth);

            GeoPolyline3 flat = rounded.Flatten();

            Assert.Equal(4, rounded.VertexCount);
            Assert.Equal(4, flat.VertexCount);

            for (int i = 0; i < rounded.VertexCount; i++)
            {
                Assert.Equal(rounded[i], flat[i]);
            }
        }

        [Fact]
        public void ACoordinateSystemInSpaceWithAnXAxisAMillionthLong_PointsItsXAxisAlongIt()
        {
            var system = new GeoCoordinateSystem3(P(1, 2, 3), new GeoVector3(0, 6E-7, 8E-7), new GeoVector3(1, 0, 0));

            Assert.Equal(0.0, system.XAxis.X, 15);
            Assert.Equal(0.6, system.XAxis.Y, 15);
            Assert.Equal(0.8, system.XAxis.Z, 15);
            Assert.Equal(1.0, system.YAxis.X, 15);
            Assert.Equal(0.0, system.ZAxis.X, 15);
            Assert.Equal(0.8, system.ZAxis.Y, 15);
            Assert.Equal(-0.6, system.ZAxis.Z, 15);
        }

        [Fact]
        public void ACoordinateSystemInSpaceScaledByATenMillionth_KeepsItsAxes()
        {
            var system = new GeoCoordinateSystem3(P(1, 2, 3), new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 1));

            GeoCoordinateSystem3 small = system.TransformBy(GeoTransform3.Scaling(P(0, 0, 0), 1E-7));

            Assert.True(small.Origin.IsEqualTo(P(1E-7, 2E-7, 3E-7), new Tolerance(1E-15, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-15)));
            Assert.True(small.XAxis.IsEqualTo(system.XAxis, new Tolerance(1E-12, 1E-12, Tolerance.DefaultEqualAngleRad, 1E-12)));
            Assert.True(small.YAxis.IsEqualTo(system.YAxis, new Tolerance(1E-12, 1E-12, Tolerance.DefaultEqualAngleRad, 1E-12)));
            Assert.True(small.ZAxis.IsEqualTo(system.ZAxis, new Tolerance(1E-12, 1E-12, Tolerance.DefaultEqualAngleRad, 1E-12)));
        }

        [Fact]
        public void ACoordinateSystemInSpaceWithAnXAxisOfNoLength_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoCoordinateSystem3(P(1, 2, 3), new GeoVector3(0, 0, 0), new GeoVector3(1, 0, 0)));
        }
    }
}
