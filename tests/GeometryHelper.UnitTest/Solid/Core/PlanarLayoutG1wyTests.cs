using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A chain of the plane laid into a frame keeps its legs side for side, and a chain, a polygon or a face of space laid
    /// out in a frame keeps every side that has a length there, while one seen edge-on is still refused.
    /// </summary>
    /// <remarks>
    /// Laying in a chain and laying out a chain, a polygon or a face went through the constructors at the global tolerance,
    /// so a step or a cut corner of a few ten-thousandths was merged away, and a polygon seen edge-on came out as a line of
    /// corners rather than being refused. No test sets the global tolerance.
    /// </remarks>
    public class PlanarLayoutG1wyTests
    {
        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static readonly GeoCoordinateSystem3 World = new GeoCoordinateSystem3(
            new GeoPlane3(P(0, 0, 0), new GeoVector3(0, 0, 1))
        );

        // A frame leaning every way, far from the origin.
        private static readonly GeoCoordinateSystem3 Leaning = new GeoCoordinateSystem3(
            new GeoPlane3(P(312.5, -87.25, 40.125), new GeoVector3(0.3, -0.5, 0.8))
        );

        // A frame tilted 60 degrees to the plane of the world about its X axis.
        private static readonly GeoCoordinateSystem3 Tilted = new GeoCoordinateSystem3(
            new GeoPlane3(P(5, -3, 7), new GeoVector3(0, Math.Sin(Math.PI / 3), Math.Cos(Math.PI / 3)))
        );

        // The square 10 by 10 with its corner at (0, 10) cut by 0.0005, built a thousand times larger and shrunk in the
        // plane side for side, then laid into the plane of the world.
        private static GeoPolygon3 CutSquare()
        {
            var large = new GeoPolygon2(P(0, 0), P(10000, 0), P(10000, 10000), P(0.5, 10000), P(0, 9999.5));

            return large.TransformBy(GeoTransform2.Scaling(0.001)).ToPolygon3(World);
        }

        [Fact]
        public void AChainWithAStepOfFourTenThousandths_IsLaidIntoAFrameWithItsFourVertices()
        {
            var large = new GeoPolyline2(P(0, 0), P(10000, 0), P(10000, 0.4), P(20000, 0.4));
            GeoPolyline2 chain = large.TransformBy(GeoTransform2.Scaling(0.001));
            Assert.Equal(4, chain.VertexCount);

            GeoPolyline3 laid = chain.ToPolyline3(Leaning);

            Assert.Equal(4, laid.VertexCount);
            Assert.Equal(chain.Length, laid.Length, 12);
        }

        [Fact]
        public void AChainWithAStepOfFourTenThousandths_IsLaidOutInAFrameWithItsFourVertices()
        {
            GeoPolyline3 chain = new GeoPolyline2(P(0, 0), P(10000, 0), P(10000, 0.4), P(20000, 0.4))
                .TransformBy(GeoTransform2.Scaling(0.001))
                .ToPolyline3(World);

            GeoPolyline2 flat = chain.ProjectToPolyline2(Tilted);

            Assert.Equal(4, flat.VertexCount);
        }

        [Fact]
        public void AChainWithALegAlongTheZAxisOfALeaningFrame_IsLaidOutWithoutThatLeg()
        {
            GeoVector3 z = Leaning.ZAxis;
            GeoPoint3 a = P(10, 20, 30);
            GeoPoint3 b = P(20, 25, 31);
            GeoPoint3 c = P(b.X + 5 * z.X, b.Y + 5 * z.Y, b.Z + 5 * z.Z);
            GeoPoint3 d = P(c.X + 10, c.Y - 4, c.Z + 2);

            GeoPolyline2 flat = new GeoPolyline3(a, b, c, d).ProjectToPolyline2(Leaning);

            // The leg seen end-on lands within the rounding of its start, not on it, and goes all the same.
            Assert.Equal(3, flat.VertexCount);
        }

        [Fact]
        public void AChainSeenEndOn_IsStillRefused()
        {
            GeoVector3 z = Leaning.ZAxis;
            var chain = new GeoPolyline3(P(10, 20, 30), P(10 + 10 * z.X, 20 + 10 * z.Y, 30 + 10 * z.Z));

            Assert.Throws<ArgumentException>(() => chain.ProjectToPolyline2(Leaning));
        }

        [Fact]
        public void ASquareWithACornerCutByFiveTenThousandths_IsLaidOutInATiltedFrameWithItsFiveCorners()
        {
            GeoPolygon3 square = CutSquare();
            Assert.Equal(5, square.VertexCount);

            GeoPolygon2 flat = square.ProjectToPolygon2(Tilted);

            Assert.Equal(5, flat.VertexCount);
            Assert.Equal(square.Area * 0.5, flat.Area, 9);
        }

        [Fact]
        public void AFaceWithACutCornerHole_IsLaidOutInATiltedFrameWithEveryCornerOfItsHole()
        {
            GeoPolygon3 boundary = new GeoPolygon2(P(-10, -10), P(20, -10), P(20, 20), P(-10, 20)).ToPolygon3(World);
            var face = new GeoFace3(boundary, new[] { CutSquare() }, Tolerance.Default);

            GeoFace2 flat = face.ProjectToFace2(Tilted);

            Assert.Equal(4, flat.Boundary.VertexCount);
            Assert.Single(flat.Holes);
            Assert.Equal(5, flat.Holes[0].VertexCount);
        }

        [Fact]
        public void ATriangleSeenEdgeOn_IsRefusedRatherThanLaidOutAsALine()
        {
            var triangle = new GeoPolygon3(new[] { P(0, 0, 0), P(10, 0, 0), P(5, 0, 8) }, Tolerance.Default);
            var edgeOn = new GeoCoordinateSystem3(new GeoPlane3(P(3, 4, 5), new GeoVector3(0.6, 0, 0.8)));

            Assert.Throws<ArgumentException>(() => triangle.ProjectToPolygon2(edgeOn));
            Assert.Throws<ArgumentException>(() => new GeoFace3(triangle).ProjectToFace2(edgeOn));
        }

        [Fact]
        public void ATriangleATenThousandthOfARadianOffEdgeOn_IsLaidOutAsASliver()
        {
            var triangle = new GeoPolygon3(new[] { P(0, 0, 0), P(10, 0, 0), P(5, 0, 8) }, Tolerance.Default);
            double tilt = 1E-4;
            var nearlyEdgeOn = new GeoCoordinateSystem3(
                new GeoPlane3(P(3, 4, 5), new GeoVector3(0.6 * Math.Cos(tilt), Math.Sin(tilt), 0.8 * Math.Cos(tilt)))
            );

            GeoPolygon2 flat = triangle.ProjectToPolygon2(nearlyEdgeOn);

            Assert.Equal(3, flat.VertexCount);
            Assert.Equal(40 * Math.Sin(tilt), flat.Area, 9);
        }
    }
}
