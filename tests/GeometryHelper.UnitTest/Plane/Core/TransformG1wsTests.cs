using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A polygon, a chain and a face in the plane are transformed side for side, a polygon and a chain are reversed corner
    /// for corner, a coordinate system takes an X axis of any length but none, and a curved chain offset with miters is
    /// mitered alike at any size.
    /// </summary>
    /// <remarks>
    /// A transformation and a reversal put the corners back together within the global tolerance of a thousandth, which
    /// dropped the sides a polygon or a chain keeps within a finer one, and refused a shape scaled under a thousandth across
    /// for having too few corners left. A coordinate system read the global vector tolerance of 1E-5 as a length, so an X
    /// axis shorter than that was refused as having none. A miter took a line for touching an arc's moved circle where the
    /// difference of the squared radius and distance, an area, was within the tolerance, a length. No test sets the global
    /// tolerance.
    /// </remarks>
    public class TransformG1wsTests
    {
        private static readonly Tolerance Millionth = new Tolerance(1E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-6);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        // The triangle (0, 0), (10, 0), (10, 10) with every corner cut by 0.0003 at a millionth: six corners.
        private static GeoPolygon2 Cut() => Corner2.Chamfer(new GeoPolygon2(P(0, 0), P(10, 0), P(10, 10)), 0.0003, 0.0003, Millionth);

        // The chain (0, 0), (10, 0), (10, 10) with its corner cut by 0.0003 at a millionth: four vertices.
        private static GeoPolyline2 CutChain() => Corner2.Chamfer(new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10)), 0.0003, 0.0003, Millionth);

        private static GeoTransform2 Rigid() => GeoTransform2.Translation(new GeoVector2(5, 7)) * GeoTransform2.Rotation(P(3, 4), 0.7);

        private static void AssertWhereTheTransformPutsThem(GeoTransform2 transform, GeoPolygon2 from, GeoPolygon2 to)
        {
            Assert.Equal(from.VertexCount, to.VertexCount);

            for (int i = 0; i < from.VertexCount; i++)
            {
                Assert.Equal(transform.Transform(from[i]), to[i]);
            }
        }

        [Fact]
        public void ATriangleCutByThreeTenThousandthsAtAMillionthTurnedAndMoved_KeepsItsSixCornersWhereTheTransformPutsThem()
        {
            GeoPolygon2 cut = Cut();
            GeoTransform2 rigid = Rigid();

            GeoPolygon2 moved = cut.TransformBy(rigid);

            Assert.Equal(6, cut.VertexCount);
            AssertWhereTheTransformPutsThem(rigid, cut, moved);
        }

        [Fact]
        public void ATriangleCutByThreeTenThousandthsAtAMillionthMirrored_KeepsItsSixCorners()
        {
            GeoPolygon2 cut = Cut();
            GeoTransform2 mirror = GeoTransform2.Mirror(new GeoLine2(P(0, 20), P(10, 25)));

            GeoPolygon2 mirrored = mirror.Transform(cut);

            AssertWhereTheTransformPutsThem(mirror, cut, mirrored);
            Assert.Equal(cut.Area, mirrored.Area, 9);
        }

        [Fact]
        public void ASquareTenByTenScaledByAHundredThousandth_IsTheSquareTenThousandthsAcross()
        {
            var square = new GeoPolygon2(P(0, 0), P(10, 0), P(10, 10), P(0, 10));
            GeoTransform2 scaling = GeoTransform2.Scaling(P(0, 0), 1E-5);

            GeoPolygon2 small = square.TransformBy(scaling);

            AssertWhereTheTransformPutsThem(scaling, square, small);
            Assert.Equal(1.0, small.Area * 1E8, 12);
        }

        [Fact]
        public void AChainCutAtItsCornerByThreeTenThousandthsAtAMillionthTurnedAndMoved_TurnsAtBothEndsOfTheCut()
        {
            GeoPolyline2 cut = CutChain();
            GeoTransform2 rigid = Rigid();

            GeoPolyline2 moved = cut.TransformBy(rigid);

            Assert.Equal(4, cut.VertexCount);
            Assert.Equal(4, moved.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.Equal(rigid.Transform(cut[i]), moved[i]);
            }
        }

        [Fact]
        public void AFaceWithTheCutTriangleForAHoleTurnedAndMoved_KeepsTheSixCornersOfItsHole()
        {
            GeoPolygon2 hole = Corner2.Chamfer(new GeoPolygon2(P(0, 0), P(10, 10), P(10, 0)), 0.0003, 0.0003, Millionth);
            var face = new GeoFace2(new GeoPolygon2(P(-20, -20), P(30, -20), P(30, 30), P(-20, 30)), new[] { hole });
            GeoTransform2 rigid = Rigid();

            GeoFace2 moved = face.TransformBy(rigid);

            Assert.Equal(6, hole.VertexCount);
            Assert.Single(moved.Holes);
            AssertWhereTheTransformPutsThem(rigid, hole, moved.Holes[0]);
            AssertWhereTheTransformPutsThem(rigid, face.Boundary, moved.Boundary);
            Assert.Equal(face.Area, moved.Area, 9);
        }

        [Fact]
        public void ATriangleCutByThreeTenThousandthsAtAMillionthReversed_KeepsItsSixCornersTheOtherWayRound()
        {
            GeoPolygon2 cut = Cut();

            GeoPolygon2 reversed = cut.Reverse();

            Assert.Equal(6, reversed.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.Equal(cut[cut.VertexCount - 1 - i], reversed[i]);
            }
        }

        [Fact]
        public void AChainCutAtItsCornerByThreeTenThousandthsAtAMillionthReversed_KeepsItsFourVerticesTheOtherWayRound()
        {
            GeoPolyline2 cut = CutChain();

            GeoPolyline2 reversed = cut.Reverse();

            Assert.Equal(4, reversed.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.Equal(cut[cut.VertexCount - 1 - i], reversed[i]);
            }
        }

        [Fact]
        public void AChainOffsetWithMitersPastALineThatMissesTheMovedArcBySeventyTolerances_IsOneChainAsWhenScaledUp()
        {
            GeoPoint2[] corners =
            {
                P(-0.001165, -0.002711), P(0.00062, -0.001059), P(0.002589, 0.00029), P(0.000415, 0.000801), P(-0.00214, 0.003174),
                P(-0.001103, 0.000257),
            };
            double[] bulges = { 0, 0, 0.24, -0.24, 0, 0 };
            var miters = new OffsetOptions(OffsetJoin.Miter, 1.5);
            var chain = new GeoPolylineArc2(corners, bulges);
            var scaled = new GeoPolylineArc2(Array.ConvertAll(corners, p => P(p.X * 64, p.Y * 64)), bulges);

            GeoPolylineArc2[] offset = chain.Offset(-0.0000702, miters, new Tolerance(2E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 2E-6));
            GeoPolylineArc2[] offsetScaled = scaled.Offset(-0.0000702 * 64, miters, new Tolerance(2E-6 * 64, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 2E-6 * 64));

            Assert.Single(offsetScaled);
            Assert.Equal(8, offsetScaled[0].VertexCount);
            Assert.Single(offset);
            Assert.Equal(8, offset[0].VertexCount);
            Assert.Equal(offsetScaled[0].Length / 64, offset[0].Length, 12);
        }

        [Fact]
        public void ACoordinateSystemWithAnXAxisAMillionthLong_PointsItsXAxisAlongIt()
        {
            var system = new GeoCoordinateSystem2(P(1, 2), new GeoVector2(6E-7, 8E-7));

            Assert.Equal(0.6, system.XAxis.X, 15);
            Assert.Equal(0.8, system.XAxis.Y, 15);
            Assert.Equal(-0.8, system.YAxis.X, 15);
            Assert.Equal(0.6, system.YAxis.Y, 15);
        }

        [Fact]
        public void ACoordinateSystemWithAnXAxisOfNoLength_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoCoordinateSystem2(P(1, 2), new GeoVector2(0, 0)));
        }
    }
}
