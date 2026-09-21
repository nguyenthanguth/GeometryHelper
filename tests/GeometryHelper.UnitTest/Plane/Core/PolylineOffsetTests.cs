using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Offsetting open chains to one side (Offset2 for polylines), and the concentric offsets of circles and
    /// rectangles.
    /// </summary>
    public class PolylineOffsetTests
    {
        private static GeoPolyline2 Chain(params double[] xy)
        {
            var points = new GeoPoint2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) { points[i] = new GeoPoint2(xy[2 * i], xy[2 * i + 1]); }
            return new GeoPolyline2(points);
        }

        private static void AssertChain(GeoPolyline2 expected, GeoPolyline2 actual, double tolerance = 1e-9)
        {
            Assert.Equal(expected.VertexCount, actual.VertexCount);
            for (int i = 0; i < expected.VertexCount; i++)
            {
                Assert.True(expected[i].DistanceTo(actual[i]) <= tolerance, $"vertex {i}: expected {expected[i]}, got {actual[i]}");
            }
        }

        [Fact]
        public void TwoPointChain_MovesLikeASegment()
        {
            var chain = Chain(0, 0, 10, 0);

            AssertChain(Chain(0, 2, 10, 2), chain.Offset(2.0).Single());
            AssertChain(Chain(0, -2, 10, -2), chain.Offset(-2.0).Single());
        }

        [Fact]
        public void Corner_IsCutOnTheInsideAndJoinedOnTheOutside()
        {
            var chain = Chain(0, 0, 10, 0, 10, 10);

            // Left is the inside of this left turn: the offset edges are cut where they meet.
            AssertChain(Chain(0, 1, 9, 1, 9, 10), chain.Offset(1.0).Single());

            // Right is the outside: the sharp join carries both edges on until they meet.
            AssertChain(Chain(0, -1, 11, -1, 11, 10), chain.Offset(-1.0).Single());

            // Chamfer: one segment across the corner, square to its bisector at the offset distance.
            GeoPolyline2 chamfered = chain.Offset(-1.0, OffsetJoin.Chamfer).Single();
            Assert.Equal(4, chamfered.VertexCount);
            double reach = new GeoLine2(chamfered[1], chamfered[2]).GetClosestPointOnBoundary(new GeoPoint2(10, 0)).DistanceTo(new GeoPoint2(10, 0));
            Assert.Equal(1.0, reach, 9);
        }

        [Fact]
        public void RoundCorner_FollowsAQuarterCircle()
        {
            var chain = Chain(0, 0, 10, 0, 10, 10);
            var options = new OffsetOptions(OffsetJoin.Round, arcTolerance: 1e-5);

            GeoPolyline2 rounded = chain.Offset(-1.0, options).Single();

            Assert.True(rounded.VertexCount > 10);
            Assert.Equal(20.0 + Math.PI / 2.0, rounded.Length, 3);
            Assert.All(rounded.Vertices.Skip(1).Take(rounded.VertexCount - 2), v => Assert.Equal(1.0, chain.DistanceTo(v), 9));
        }

        [Fact]
        public void Hairpin_OffsetInsideTheTurn_IsCutIntoTwoPieces()
        {
            var hairpin = Chain(0, 0, 10, 0, 10, 1, 0, 1);

            GeoPolyline2[] inside = hairpin.Offset(2.0);

            // The legs' offsets cross over each other inside the narrow turn; the loop they make is cut away.
            Assert.Equal(2, inside.Length);
            AssertChain(Chain(0, 2, 10, 2), inside[0]);
            AssertChain(Chain(10, -1, 0, -1), inside[1]);

            // Outside, the offset runs round the turn in one piece.
            AssertChain(Chain(0, -2, 12, -2, 12, 3, 0, 3), hairpin.Offset(-2.0).Single());
        }

        [Fact]
        public void ClosedChain_OffsetInward_ComesBackAsAClosedChain()
        {
            // A square drawn as an open chain that ends where it began.
            var loop = Chain(0, 0, 10, 0, 10, 10, 0, 10, 0, 0);

            GeoPolyline2 inner = loop.Offset(1.0).Single();

            Assert.Equal(inner[0], inner[inner.VertexCount - 1]);
            Assert.Equal(32.0, inner.Length, 9);
            Assert.All(inner.Vertices, v => Assert.True(Math.Abs(v.X - 1) < 1e-9 || Math.Abs(v.X - 9) < 1e-9));
        }

        [Fact]
        public void GentleCurve_RoundOffset_StaysAtTheDistance()
        {
            var rng = new Random(12);
            var options = new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.0005);

            for (int t = 0; t < 50; t++)
            {
                // A chain running left to right that never turns back on itself.
                var points = Enumerable.Range(0, 12).Select(i => new GeoPoint2(i * 5.0, rng.NextDouble() * 4.0)).ToArray();
                var chain = new GeoPolyline2(points);

                foreach (double d in new[] { 0.8, -0.8 })
                {
                    GeoPolyline2[] result = chain.Offset(d, options);
                    Assert.Single(result);

                    GeoPolyline2 curve = result[0];
                    Assert.True(curve[curve.VertexCount - 1].X > curve[0].X, "the offset runs the same way as the chain");

                    foreach (GeoPoint2 v in curve.Vertices)
                    {
                        Assert.InRange(chain.DistanceTo(v), Math.Abs(d) - 0.0006, Math.Abs(d) + 1e-7);
                    }

                    // On the side asked for.
                    GeoPoint2 middle = curve.GetPointAtParameter(0.5);
                    GeoPoint2 foot = chain.GetClosestPointOnBoundary(middle);
                    Assert.True(middle.Y > foot.Y == d > 0, "offset to the wrong side");
                }
            }
        }

        [Fact]
        public void TinyDistance_BadArguments_AndCoreAgreement()
        {
            var chain = Chain(0, 0, 10, 0, 10, 10);

            Assert.Equal(chain, chain.Offset(0.00001).Single());
            Assert.Throws<ArgumentNullException>(() => Offset2.Offset((GeoPolyline2)null, 1.0));
            Assert.Throws<ArgumentNullException>(() => chain.Offset(1.0, (OffsetOptions)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => chain.Offset(double.NaN));

            Assert.Equal(Offset2.Offset(chain, 1.0), chain.Offset(1.0));
            Assert.Equal(Offset2.Offset(chain, 1.0, Tolerance.Global), chain.Offset(1.0, Tolerance.Global));
            Assert.Equal(Offset2.Offset(chain, -1.0, OffsetJoin.Round), chain.Offset(-1.0, OffsetJoin.Round));
            Assert.Equal(Offset2.Offset(chain, -1.0, OffsetJoin.Round, Tolerance.Global), chain.Offset(-1.0, OffsetJoin.Round, Tolerance.Global));
            Assert.Equal(Offset2.Offset(chain, -1.0, OffsetOptions.Default), chain.Offset(-1.0, OffsetOptions.Default));
            Assert.Equal(Offset2.Offset(chain, -1.0, OffsetOptions.Default, Tolerance.Global), chain.Offset(-1.0, OffsetOptions.Default, Tolerance.Global));
        }

        [Fact]
        public void FarFromTheOrigin_TheOffsetIsTheSame()
        {
            var chain = Chain(0, 0, 10, 0, 10, 10);
            var shift = new GeoVector2(3e6, 4e6);

            GeoPolyline2 moved = chain.Translate(shift).Offset(-1.0).Single();
            AssertChain(Chain(0, -1, 11, -1, 11, 10).Translate(shift), moved, 1e-8);
        }

        #region Circle and rectangle

        [Fact]
        public void Circle_OffsetChangesOnlyTheRadius()
        {
            var circle = new GeoCircle2(new GeoPoint2(3, 4), 5.0);

            Assert.True(circle.TryOffset(2.0, out GeoCircle2 grown));
            Assert.Equal(new GeoCircle2(new GeoPoint2(3, 4), 7.0), grown);

            Assert.True(circle.TryOffset(-4.5, out GeoCircle2 shrunk));
            Assert.Equal(0.5, shrunk.Radius, 12);

            Assert.False(circle.TryOffset(-5.0, out GeoCircle2 unchanged));
            Assert.Equal(circle, unchanged);
            Assert.False(circle.TryOffset(-7.0, out _));

            Assert.Equal(Offset2.TryOffset(circle, 1.0, out GeoCircle2 a), circle.TryOffset(1.0, out GeoCircle2 b, Tolerance.Global));
            Assert.Equal(a, b);
            Assert.Throws<ArgumentOutOfRangeException>(() => circle.TryOffset(double.NaN, out _));
        }

        [Fact]
        public void Rectangle_OffsetKeepsItsCornersSquare()
        {
            var rect = new GeoRectangle2(new GeoPoint2(5, 5), 10, 4, 0.3);

            Assert.True(rect.TryOffset(1.0, out GeoRectangle2 grown));
            Assert.Equal(12.0, grown.Width, 12);
            Assert.Equal(6.0, grown.Height, 12);
            Assert.Equal(rect.Center, grown.Center);
            Assert.Equal(rect.AngleRad, grown.AngleRad);

            // The same region the polygon offset with sharp corners gives.
            GeoPolygon2 viaPolygon = rect.ToPolygon().Offset(1.0).Single();
            Assert.Equal(grown.ToPolygon().Area, viaPolygon.Area, 9);

            Assert.False(rect.TryOffset(-2.0, out _));
            Assert.True(rect.TryOffset(-1.9, out GeoRectangle2 thin));
            Assert.Equal(0.2, thin.Height, 12);

            Assert.Equal(Offset2.TryOffset(rect, 1.0, out GeoRectangle2 a), rect.TryOffset(1.0, out GeoRectangle2 b, Tolerance.Global));
            Assert.Equal(a, b);
        }

        #endregion
    }
}
