using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Moving, turning and transforming the pieces that may curve. The rule everything here checks is that
    /// transforming a shape and then flattening it draws the same thing as flattening it and then
    /// transforming it: a bulge that survives a move is a bulge that means the same afterwards.
    /// </summary>
    public class ArcTransformTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-6, 1E-9);

        private static GeoPolygonArc2 Slot()
        {
            // A slot: two straight sides and a half turn at each end.
            return new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, 1.0, 0.0, 1.0 });
        }

        private static GeoPolylineArc2 Chain()
        {
            return new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(200, 100) },
                new[] { 0.5, 0.0, -0.25 });
        }

        [Fact]
        public void MovingAndTurningLeaveEveryArcAsItWas()
        {
            GeoPolygonArc2 slot = Slot();
            var vector = new GeoVector2(1000, -250);

            GeoPolygonArc2 moved = slot.Translate(vector);
            GeoPolygonArc2 turned = slot.RotateBy(Math.PI / 3.0, new GeoPoint2(17, -43));

            foreach (GeoPolygonArc2 other in new[] { moved, turned })
            {
                Assert.Equal(slot.Length, other.Length, 8);
                Assert.Equal(slot.SignedArea, other.SignedArea, 6);

                for (int i = 0; i < slot.EdgeCount; i++)
                {
                    Assert.Equal(slot.GetBulgeAt(i), other.GetBulgeAt(i), 12);
                }
            }

            Assert.True(moved[0].IsEqualTo(slot[0].Add(vector), Loose));

            // A chain keeps its ends, in order.
            GeoPolylineArc2 chain = Chain();
            GeoPolylineArc2 shifted = chain.Translate(vector);

            Assert.True(shifted[0].IsEqualTo(chain[0].Add(vector), Loose));
            Assert.True(shifted[shifted.VertexCount - 1].IsEqualTo(chain[chain.VertexCount - 1].Add(vector), Loose));

            // And one edge on its own moves the same way.
            GeoEdge2 edge = chain.GetEdgeAt(0);
            Assert.Equal(edge.Bulge, edge.Translate(vector).Bulge, 12);
            Assert.Equal(edge.Length, edge.RotateBy(1.1, GeoPoint2.Origin).Length, 9);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(2.5)]
        [InlineData(0.04)]
        public void ScalingEvenlyScalesTheArcsWithTheShape(double factor)
        {
            GeoPolygonArc2 slot = Slot();
            GeoTransform2 scale = GeoTransform2.Scaling(new GeoPoint2(30, 15), factor);

            GeoPolygonArc2 scaled = slot.TransformBy(scale);

            Assert.Equal(slot.Length * factor, scaled.Length, 6);
            Assert.Equal(slot.Area * factor * factor, scaled.Area, 4);

            // A bulge is the arc measured against its own chord, so scaling both leaves it alone.
            for (int i = 0; i < slot.EdgeCount; i++)
            {
                Assert.Equal(slot.GetBulgeAt(i), scaled.GetBulgeAt(i), 9);
            }
        }

        [Fact]
        public void MirroringTurnsEveryArcTheOtherWay()
        {
            GeoPolygonArc2 slot = Slot();
            GeoTransform2 mirror = GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0, 1)));

            GeoPolygonArc2 flipped = slot.TransformBy(mirror);

            Assert.Equal(slot.Length, flipped.Length, 6);
            Assert.Equal(slot.Area, flipped.Area, 4);

            // The shape encloses as much, but it is walked the other way round now.
            Assert.Equal(-Math.Sign(slot.SignedArea), Math.Sign(flipped.SignedArea));

            for (int i = 0; i < slot.EdgeCount; i++)
            {
                Assert.Equal(-slot.GetBulgeAt(i), flipped.GetBulgeAt(i), 9);
            }

            // A chain still starts where its start point went.
            GeoPolylineArc2 chain = Chain();
            GeoPolylineArc2 other = chain.TransformBy(mirror);

            Assert.True(other[0].IsEqualTo(mirror.Transform(chain[0]), Loose));
            Assert.True(other[other.VertexCount - 1].IsEqualTo(mirror.Transform(chain[chain.VertexCount - 1]), Loose));
        }

        [Theory]
        [InlineData("move")]
        [InlineData("turn")]
        [InlineData("scale")]
        [InlineData("mirror")]
        [InlineData("frame")]
        public void TransformingThenFlatteningDrawsWhatFlatteningThenTransformingDraws(string which)
        {
            GeoTransform2 transform;

            switch (which)
            {
                case "move":
                    transform = GeoTransform2.Translation(new GeoVector2(-410, 77));
                    break;
                case "turn":
                    transform = GeoTransform2.Rotation(new GeoPoint2(12, 34), Math.PI / 5.0);
                    break;
                case "scale":
                    transform = GeoTransform2.Scaling(new GeoPoint2(-5, 8), 3.0);
                    break;
                case "mirror":
                    transform = GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(10, 0), new GeoPoint2(13, 4)));
                    break;
                default:
                    transform = GeoTransform2.FromFrame(new GeoPoint2(100, -60), new GeoVector2(0.6, 0.8));
                    break;
            }

            GeoPolygonArc2 slot = Slot();

            GeoPolygon2 first = slot.TransformBy(transform).Flatten(0.001);
            GeoPolygon2 second = slot.Flatten(0.001).TransformBy(transform);

            // Both approximate the same curve from the inside, so they enclose the same area and start at
            // the same place. They need not have the same number of pieces: a chord tolerance is measured
            // in drawing units, so a shape scaled up needs more of them to stay within it.
            Assert.True(Math.Abs(first.Area - second.Area) / second.Area < 1E-5);
            Assert.True(first[0].IsEqualTo(second[0], Loose));
            Assert.True(Math.Abs(first.Length - second.Length) / second.Length < 1E-5);

            // A transformation that does not change size does not change the tessellation either, so there
            // the two agree piece for piece.
            if (which != "scale")
            {
                Assert.Equal(second.VertexCount, first.VertexCount);
                Assert.True(first.IsEqualTo(second, Loose));
            }

            GeoPolylineArc2 chain = Chain();

            GeoPolyline2 movedThenFlat = chain.TransformBy(transform).Flatten(0.001);
            GeoPolyline2 flatThenMoved = chain.Flatten(0.001).TransformBy(transform);

            Assert.True(movedThenFlat[0].IsEqualTo(flatThenMoved[0], Loose));
            Assert.True(movedThenFlat[movedThenFlat.VertexCount - 1]
                .IsEqualTo(flatThenMoved[flatThenMoved.VertexCount - 1], Loose));
            Assert.True(Math.Abs(movedThenFlat.Length - flatThenMoved.Length) / flatThenMoved.Length < 1E-5);
        }

        [Fact]
        public void AnUnevenScalingIsRefusedRatherThanAveraged()
        {
            GeoTransform2 uneven = GeoTransform2.Scaling(2.0, 3.0);

            Assert.Throws<InvalidOperationException>(() => Slot().TransformBy(uneven));
            Assert.Throws<InvalidOperationException>(() => Chain().TransformBy(uneven));
            Assert.Throws<InvalidOperationException>(() => Chain().GetEdgeAt(0).TransformBy(uneven));

            // A straight edge has no curvature to stretch, so it goes through.
            var straight = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(10, 10));
            Assert.True(straight.TransformBy(uneven).EndPoint.IsEqualTo(new GeoPoint2(20, 30), Loose));

            // And a chain with no arcs goes through as well.
            var plain = new GeoPolygonArc2(new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10)));

            Assert.Equal(300.0, plain.TransformBy(uneven).Area, 9);

            Assert.Throws<ArgumentNullException>(() => Slot().TransformBy(null));
            Assert.Throws<ArgumentNullException>(() => Chain().TransformBy(null));
        }

        [Fact]
        public void AShapeComesBackUnderTheInverseOfWhatMovedIt()
        {
            GeoTransform2 there = GeoTransform2.Rotation(new GeoPoint2(4, 9), 0.7)
                .Multiply(GeoTransform2.Scaling(GeoPoint2.Origin, 1.75))
                .Multiply(GeoTransform2.Translation(new GeoVector2(60, -20)));

            Assert.True(there.TryGetInverse(out GeoTransform2 back));

            Assert.True(Slot().TransformBy(there).TransformBy(back).IsEqualTo(Slot(), Loose));
            Assert.True(Chain().TransformBy(there).TransformBy(back).IsEqualTo(Chain(), Loose));
        }
    }
}
