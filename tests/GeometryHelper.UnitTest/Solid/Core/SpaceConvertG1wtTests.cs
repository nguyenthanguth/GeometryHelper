using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A polygon in space is reversed corner for corner, written as a chain side for side and transformed side for side
    /// however it is transformed, a chain in space is closed into a polygon corner for corner and transformed leg for leg,
    /// and a face follows its polygons.
    /// </summary>
    /// <remarks>
    /// Each of these put the corners back together within the global tolerance of a thousandth, which dropped the sides a
    /// polygon or a chain keeps within a finer one, and a reversal checked the polygon flat within the global tolerance
    /// again, which refused one built within a looser one. A rigid move of a polygon carried it over already. No test sets
    /// the global tolerance.
    /// </remarks>
    public class SpaceConvertG1wtTests
    {
        private static readonly Tolerance Millionth = new Tolerance(1E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-6);

        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        // The triangle (0, 0, 0), (10, 0, 0), (10, 0, 10) with every corner cut by 0.0003 at a millionth: six corners.
        private static GeoPolygon3 Cut() => new GeoPolygon3(P(0, 0, 0), P(10, 0, 0), P(10, 0, 10)).Chamfer(0.0003, Millionth);

        // The chain (0, 0, 0), (10, 0, 0), (10, 0, 10) with its corner cut by 0.0003 at a millionth: four vertices.
        private static GeoPolyline3 CutChain() => new GeoPolyline3(P(0, 0, 0), P(10, 0, 0), P(10, 0, 10)).Chamfer(0.0003, Millionth);

        private static void AssertWhereTheTransformPutsThem(GeoTransform3 transform, GeoPolygon3 from, GeoPolygon3 to)
        {
            Assert.Equal(from.VertexCount, to.VertexCount);

            for (int i = 0; i < from.VertexCount; i++)
            {
                Assert.Equal(transform.Transform(from[i]), to[i]);
            }
        }

        [Fact]
        public void ATriangleInSpaceCutByThreeTenThousandthsAtAMillionthReversed_KeepsItsSixCornersTheOtherWayRound()
        {
            GeoPolygon3 cut = Cut();

            GeoPolygon3 reversed = cut.Reverse();

            Assert.Equal(6, cut.VertexCount);
            Assert.Equal(6, reversed.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.Equal(cut[cut.VertexCount - 1 - i], reversed[i]);
            }

            Assert.Equal(-cut.Normal.Y, reversed.Normal.Y, 12);
            Assert.Equal(cut.Area, reversed.Area, 9);
        }

        [Fact]
        public void ASquareBuiltAtAHundredthWithACornerFiveThousandthsOffFlatReversed_KeepsItsCornersTheOtherWayRound()
        {
            var square = new GeoPolygon3(new[] { P(0, 0, 0), P(100, 0, 0), P(100, 100, 0.005), P(0, 100, 0) }, Hundredth);

            GeoPolygon3 reversed = square.Reverse();

            Assert.Equal(4, reversed.VertexCount);

            for (int i = 0; i < square.VertexCount; i++)
            {
                Assert.Equal(square[square.VertexCount - 1 - i], reversed[i]);
            }

            Assert.Equal(-square.Normal.Z, reversed.Normal.Z, 12);
        }

        [Fact]
        public void TheCutTriangleInSpaceAsAChain_RunsItsSixCornersAndBackToTheFirst()
        {
            GeoPolygon3 cut = Cut();

            GeoPolyline3 chain = cut.ToPolyline();

            Assert.Equal(7, chain.VertexCount);

            for (int i = 0; i < chain.VertexCount; i++)
            {
                Assert.Equal(cut[i % cut.VertexCount], chain[i]);
            }
        }

        [Fact]
        public void AChainRoundTheCutTriangleInSpaceClosedOnItsFirstCorner_IsThePolygonOfItsSixCorners()
        {
            GeoPolygon3 cut = Cut();

            GeoPolygon3 closed = cut.ToPolyline().ToPolygon();

            Assert.Equal(6, closed.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.Equal(cut[i], closed[i]);
            }

            Assert.Equal(cut.Area, closed.Area, 9);
        }

        [Fact]
        public void AChainInSpaceCutAtItsCornerByThreeTenThousandthsAtAMillionth_IsThePolygonOfItsFourVertices()
        {
            GeoPolyline3 cut = CutChain();

            GeoPolygon3 closed = cut.ToPolygon();

            Assert.Equal(4, cut.VertexCount);
            Assert.Equal(4, closed.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.Equal(cut[i], closed[i]);
            }
        }

        // The square 10 across from (100, 100, 0), as a chain ending a gap off its start along x.
        private static GeoPolyline3 SquareEndingOff(double gap)
            => new GeoPolyline3(P(100, 100, 0), P(110, 100, 0), P(110, 110, 0), P(100, 110, 0), P(100 + gap, 100, 0));

        [Fact]
        public void AChainInSpaceRoundASquareEndingAMillionthOfAMillionthOffItsStart_IsThatSquare()
        {
            GeoPolygon3 closed = SquareEndingOff(1E-12).ToPolygon();

            Assert.Equal(4, closed.VertexCount);
            Assert.Equal(P(100, 100, 0), closed[0]);
            Assert.Equal(100.0, closed.Area, 9);
        }

        [Fact]
        public void AChainInSpaceRoundASquareEndingATenThousandthOffItsStart_KeepsTheSideBackToItsStart()
        {
            GeoPolyline3 chain = SquareEndingOff(1E-4);

            GeoPolygon3 closed = chain.ToPolygon();

            Assert.Equal(5, closed.VertexCount);
            Assert.Equal(chain[4], closed[4]);
        }

        [Fact]
        public void AChainInSpaceThatDoesNotLieFlat_IsStillRefusedAsAPolygon()
        {
            var chain = new GeoPolyline3(P(0, 0, 0), P(10, 0, 0), P(10, 10, 0), P(0, 10, 1));

            Assert.Throws<ArgumentException>(() => chain.ToPolygon());
        }

        [Fact]
        public void AChainInSpaceCutAtItsCornerTurnedAndMoved_TurnsAtBothEndsOfTheCut()
        {
            GeoPolyline3 cut = CutChain();
            GeoTransform3 rigid = GeoTransform3.Translation(new GeoVector3(5, 7, -3)) * GeoTransform3.RotationAxis(P(3, 4, 5), new GeoVector3(1, 2, 2), 0.7);

            GeoPolyline3 moved = cut.TransformBy(rigid);

            Assert.Equal(4, moved.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.Equal(rigid.Transform(cut[i]), moved[i]);
            }
        }

        [Fact]
        public void AChainInSpaceScaledByAHundredThousandth_KeepsItsLegsAHundredThousandthAsLong()
        {
            var chain = new GeoPolyline3(P(0, 0, 0), P(10, 0, 0), P(10, 0, 10));
            GeoTransform3 scaling = GeoTransform3.Scaling(P(0, 0, 0), 1E-5);

            GeoPolyline3 small = chain.TransformBy(scaling);

            Assert.Equal(3, small.VertexCount);
            Assert.Equal(2E-4, small.Length, 15);
        }

        [Fact]
        public void ATriangleInSpaceCutByThreeTenThousandthsScaledByTwo_KeepsItsSixCornersWhereTheScalingPutsThem()
        {
            GeoPolygon3 cut = Cut();
            GeoTransform3 scaling = GeoTransform3.Scaling(P(1, 2, 3), 2.0);

            GeoPolygon3 scaled = cut.TransformBy(scaling);

            AssertWhereTheTransformPutsThem(scaling, cut, scaled);
            Assert.Equal(4 * cut.Area, scaled.Area, 9);
        }

        [Fact]
        public void ATriangleInSpaceCutByThreeTenThousandthsSheared_KeepsItsSixCornersWhereTheShearPutsThem()
        {
            GeoPolygon3 cut = Cut();
            var shear = new GeoTransform3(new double[,] { { 1, 0, 0.3, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });

            GeoPolygon3 sheared = cut.TransformBy(shear);

            AssertWhereTheTransformPutsThem(shear, cut, sheared);
        }

        [Fact]
        public void ATriangleInSpaceSquashedOntoALine_IsStillRefusedAsEnclosingNoArea()
        {
            var triangle = new GeoPolygon3(P(0, 0, 0), P(10, 0, 0), P(10, 0, 10));

            Assert.Throws<ArgumentException>(() => triangle.TransformBy(GeoTransform3.Scaling(1, 1, 0)));
        }

        [Fact]
        public void AFaceInSpaceWithTheCutTriangleForAHoleScaledByTwo_KeepsTheSixCornersOfItsHole()
        {
            GeoPolygon3 hole = Cut();
            var face = new GeoFace3(new GeoPolygon3(P(-20, 0, -20), P(30, 0, -20), P(30, 0, 30), P(-20, 0, 30)), new[] { hole });
            GeoTransform3 scaling = GeoTransform3.Scaling(P(1, 2, 3), 2.0);

            GeoFace3 scaled = face.TransformBy(scaling);

            Assert.Single(scaled.Holes);
            AssertWhereTheTransformPutsThem(scaling, face.Holes[0], scaled.Holes[0]);
            AssertWhereTheTransformPutsThem(scaling, face.Boundary, scaled.Boundary);
            Assert.Equal(4 * face.Area, scaled.Area, 9);
        }
    }
}
