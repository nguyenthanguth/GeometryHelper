using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A polygon in the plane is written as a chain side for side and a chain is closed into a polygon corner for corner,
    /// and a corner where a straight piece runs into an arc is rounded alike at any size.
    /// </summary>
    /// <remarks>
    /// The conversions put the corners back together within the global tolerance of a thousandth, which dropped the sides a
    /// polygon or a chain keeps within a finer one. A fillet took a line for touching an arc's moved circle where the
    /// difference of the squared radius and distance, an area, was within the tolerance, a length, and then drew or refused
    /// the half circle about the point it touched as the rounding of its sweep fell. No test sets the global tolerance.
    /// </remarks>
    public class ConvertG1wtTests
    {
        private static readonly Tolerance Millionth = new Tolerance(
            1E-6,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-6
        );

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        // The triangle (0, 0), (10, 0), (10, 10) with every corner cut by 0.0003 at a millionth: six corners.
        private static GeoPolygon2 Cut() =>
            Corner2.Chamfer(new GeoPolygon2(P(0, 0), P(10, 0), P(10, 10)), 0.0003, 0.0003, Millionth);

        // The chain (0, 0), (10, 0), (10, 10) with its corner cut by 0.0003 at a millionth: four vertices.
        private static GeoPolyline2 CutChain() =>
            Corner2.Chamfer(new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10)), 0.0003, 0.0003, Millionth);

        [Fact]
        public void ATriangleCutByThreeTenThousandthsAtAMillionthAsAChain_RunsItsSixCornersAndBackToTheFirst()
        {
            GeoPolygon2 cut = Cut();

            GeoPolyline2 chain = cut.ToPolyline();

            Assert.Equal(6, cut.VertexCount);
            Assert.Equal(7, chain.VertexCount);

            for (int i = 0; i < chain.VertexCount; i++)
            {
                Assert.Equal(cut[i % cut.VertexCount], chain[i]);
            }
        }

        [Fact]
        public void AChainRoundTheCutTriangleClosedOnItsFirstCorner_IsThePolygonOfItsSixCorners()
        {
            GeoPolygon2 cut = Cut();

            GeoPolygon2 closed = cut.ToPolyline().ToPolygon();

            Assert.Equal(6, closed.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.Equal(cut[i], closed[i]);
            }
        }

        [Fact]
        public void AChainCutAtItsCornerByThreeTenThousandthsAtAMillionth_IsThePolygonOfItsFourVertices()
        {
            GeoPolyline2 cut = CutChain();

            GeoPolygon2 closed = cut.ToPolygon();

            Assert.Equal(4, cut.VertexCount);
            Assert.Equal(4, closed.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.Equal(cut[i], closed[i]);
            }
        }

        // The square 10 across from (100, 100), as a chain ending a gap off its start along x.
        private static GeoPolyline2 SquareEndingOff(double gap) =>
            new GeoPolyline2(P(100, 100), P(110, 100), P(110, 110), P(100, 110), P(100 + gap, 100));

        [Fact]
        public void AChainRoundASquareEndingAMillionthOfAMillionthOffItsStart_IsThatSquare()
        {
            GeoPolygon2 closed = SquareEndingOff(1E-12).ToPolygon();

            Assert.Equal(4, closed.VertexCount);
            Assert.Equal(P(100, 100), closed[0]);
            Assert.Equal(P(100, 110), closed[3]);
        }

        [Fact]
        public void AChainRoundASquareEndingATenThousandthOffItsStart_KeepsTheSideBackToItsStart()
        {
            GeoPolyline2 chain = SquareEndingOff(1E-4);

            GeoPolygon2 closed = chain.ToPolygon();

            Assert.Equal(5, closed.VertexCount);
            Assert.Equal(chain[4], closed[4]);
        }

        // The cap of the circle of radius 12 about the origin above the chord y = 4, turned by half a radian about the origin
        // and moved to (30, 40): the chord runs from x = -5.66 to the circle and the arc on round it counter-clockwise. The
        // largest circle in the cap is the one of radius 4 touching the chord at (0, 4) and the arc at (0, 12), on either side
        // of it, so its arc would sweep half a turn, which a fillet never draws.
        private static GeoPolylineArc2 Cap(double scale)
        {
            double x0 = Math.Sqrt(12.0 * 12.0 - 4.0 * 4.0);
            double corner = Math.Atan2(4.0, x0);
            double end = Math.PI / 2 + (Math.PI / 2 - corner) / 2;
            GeoTransform2 place =
                GeoTransform2.Translation(new GeoVector2(30 * scale, 40 * scale))
                * GeoTransform2.Rotation(P(0, 0), 0.5);

            return new GeoPolylineArc2(
                new[]
                {
                    P(-0.5 * x0 * scale, 4.0 * scale),
                    P(x0 * scale, 4.0 * scale),
                    P(12.0 * Math.Cos(end) * scale, 12.0 * Math.Sin(end) * scale),
                }.Select(place.Transform),
                new[] { 0.0, Math.Tan((end - corner) / 4), 0.0 }
            );
        }

        [Fact]
        public void ACapFilletedAtItsCornerByARadiusAQuarterOfTheToleranceTooLargeDrawnAThousandTimesSmaller_IsLeftSquareAsAtFullSize()
        {
            double radius = 4.0 + 0.25E-4;

            bool full = Cap(1.0)
                .TryFilletAt(
                    1,
                    radius,
                    out _,
                    new Tolerance(1E-4, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-4)
                );
            bool small = Cap(1E-3)
                .TryFilletAt(
                    1,
                    radius * 1E-3,
                    out _,
                    new Tolerance(1E-7, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-7)
                );

            Assert.False(full);
            Assert.False(small);
        }

        [Fact]
        public void ACapFilletedAtItsCornerByARadiusThatFits_IsRoundedAlikeAtFullSizeAndAThousandTimesSmaller()
        {
            double radius = 3.5;

            bool full = Cap(1.0)
                .TryFilletAt(
                    1,
                    radius,
                    out GeoPolylineArc2 rounded,
                    new Tolerance(1E-4, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-4)
                );
            bool small = Cap(1E-3)
                .TryFilletAt(
                    1,
                    radius * 1E-3,
                    out GeoPolylineArc2 roundedSmall,
                    new Tolerance(1E-7, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-7)
                );

            Assert.True(full);
            Assert.True(small);
            Assert.Equal(4, rounded.VertexCount);
            Assert.Equal(4, roundedSmall.VertexCount);
            Assert.Equal(rounded.Length / 1000, roundedSmall.Length, 12);
        }
    }
}
