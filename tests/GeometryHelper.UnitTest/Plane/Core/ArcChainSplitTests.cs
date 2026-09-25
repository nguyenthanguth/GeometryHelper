using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Cutting a chain that may curve, and the measurements that come with a loop: its centroid and
    /// whether it crosses itself. A cut inside an arc has to leave two arcs, not two chords, so the pieces
    /// put back together draw what went in.
    /// </summary>
    public class ArcChainSplitTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-8, 1E-8);

        private static GeoPolygonArc2 Slot()
        {
            return new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, 1.0, 0.0, 1.0 });
        }

        private static GeoPolylineArc2 Chain()
        {
            // Straight 100, then a half circle of radius 50, then straight 100.
            return new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new[] { 0.0, 1.0, 0.0 });
        }

        [Fact]
        public void CuttingInsideAnArcLeavesTwoArcsOfTheSameRadius()
        {
            GeoPolylineArc2 chain = Chain();

            Assert.Equal(200.0 + Math.PI * 50.0, chain.Length, 8);

            // A quarter of the way round the arc, which starts 100 along.
            double at = 100.0 + Math.PI * 12.5;

            Assert.True(chain.TrySplitAtDistance(at, out GeoPolylineArc2 first, out GeoPolylineArc2 second));

            // Nothing is lost and nothing is straightened.
            Assert.Equal(chain.Length, first.Length + second.Length, 8);
            Assert.True(first.HasArcs);
            Assert.True(second.HasArcs);

            foreach (GeoEdge2 edge in first.GetEdges().Concat(second.GetEdges()).Where(edge => edge.IsArc))
            {
                Assert.Equal(50.0, edge.ToArc().Radius, 8);
            }

            // The two ends are where they were, and they meet where the cut was made.
            Assert.True(first[0].IsEqualTo(chain[0], Tight));
            Assert.True(second[second.VertexCount - 1].IsEqualTo(chain[chain.VertexCount - 1], Tight));
            Assert.True(first[first.VertexCount - 1].IsEqualTo(second[0], Tight));
            Assert.True(first[first.VertexCount - 1].IsEqualTo(chain.GetPointAtDistance(at), Tight));
        }

        [Fact]
        public void CuttingOnAVertexEndsThePieceThereWithoutSplittingAnEdge()
        {
            GeoPolylineArc2 chain = Chain();

            Assert.True(chain.TrySplitAtDistance(100.0, out GeoPolylineArc2 first, out GeoPolylineArc2 second));

            Assert.Equal(2, first.VertexCount);
            Assert.False(first.HasArcs);
            Assert.Equal(100.0, first.Length, 9);
            Assert.Equal(100.0 + Math.PI * 50.0, second.Length, 8);

            // A cut at either end leaves nothing to cut off.
            Assert.False(chain.TrySplitAtDistance(0.0, out _, out _));
            Assert.False(chain.TrySplitAtDistance(chain.Length, out _, out _));
            Assert.False(chain.TrySplitAtDistance(-5.0, out _, out _));
        }

        [Fact]
        public void SeveralCutsGiveThePiecesBetweenThemInOrder()
        {
            GeoPolylineArc2 chain = Chain();

            GeoPolylineArc2[] pieces = chain.SplitAtDistances(new[] { 250.0, 50.0, 150.0 });

            Assert.Equal(4, pieces.Length);
            Assert.Equal(chain.Length, pieces.Sum(piece => piece.Length), 8);

            // In order along the chain, each starting where the one before it ended.
            Assert.True(pieces[0][0].IsEqualTo(chain[0], Tight));

            for (int i = 1; i < pieces.Length; i++)
            {
                Assert.True(pieces[i][0].IsEqualTo(pieces[i - 1][pieces[i - 1].VertexCount - 1], Tight));
            }

            // Distances outside the chain are ignored rather than clamped onto its ends.
            Assert.Single(chain.SplitAtDistances(new[] { -10.0, 9999.0 }));
        }

        [Fact]
        public void CuttingALoopGivesChainsThatRunOnThroughWhereItStarted()
        {
            GeoPolygonArc2 slot = Slot();

            // A vertical line through the middle of the slot meets it twice.
            var knife = new GeoLine2(new GeoPoint2(100, -50), new GeoPoint2(100, 100));

            Assert.True(slot.TrySplitBy(knife, out GeoPolylineArc2[] pieces));
            Assert.Equal(2, pieces.Length);
            Assert.Equal(slot.Length, pieces.Sum(piece => piece.Length), 6);

            // Each half keeps one round end, so each is longer than the straight run it holds.
            foreach (GeoPolylineArc2 piece in pieces)
            {
                Assert.True(piece.HasArcs);
                Assert.Equal(200.0 + Math.PI * 25.0, piece.Length, 6);
            }

            // One meeting is not a split: a loop opened up in one place is still one piece.
            var graze = new GeoLine2(new GeoPoint2(225, -50), new GeoPoint2(225, 100));
            Assert.False(slot.TrySplitBy(graze, out _));
        }

        [Fact]
        public void TheCentroidOfALoopCountsWhatTheArcsAddAndTakeAway()
        {
            GeoPolygonArc2 slot = Slot();

            // The slot is symmetric about both its middles.
            Assert.True(slot.Centroid.IsEqualTo(new GeoPoint2(100, 25), new Tolerance(1E-9, 1E-9)));

            // One round end only: the centroid moves toward it, and the flattened loop agrees.
            var lopsided = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, 1.0, 0.0, 0.0 });

            Assert.True(lopsided.Centroid.X > 100.0);
            Assert.True(lopsided.Centroid.IsEqualTo(lopsided.Flatten(0.0005).Centroid, new Tolerance(1E-3, 1E-6)));

            // A straight loop gives what the straight world gives.
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

            Assert.True(new GeoPolygonArc2(square).Centroid.IsEqualTo(square.Centroid, Tight));
        }

        [Fact]
        public void ALoopSaysWhetherItCrossesItself()
        {
            Assert.True(Slot().IsSimple());
            Assert.True(Slot().Reverse().IsSimple());

            // Two ends bulging so far inward that they run into each other.
            var pinched = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, -3.0, 0.0, -3.0 });

            Assert.False(pinched.IsSimple());

            // A bow tie is not simple however straight it is.
            var crossed = new GeoPolygonArc2(new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 10), new GeoPoint2(10, 0), new GeoPoint2(0, 10)));

            Assert.False(crossed.IsSimple());
        }

        [Fact]
        public void AChainClosesIntoALoopAndALoopOpensIntoAChain()
        {
            GeoPolygonArc2 slot = Slot();

            GeoPolylineArc2 opened = slot.ToPolylineArc2();

            Assert.Equal(slot.VertexCount + 1, opened.VertexCount);
            Assert.Equal(slot.Length, opened.Length, 8);
            Assert.True(opened[0].IsEqualTo(opened[opened.VertexCount - 1], Tight));

            // And back again, with every arc kept.
            Assert.True(opened.ToPolygonArc2().IsEqualTo(slot));

            // Two points cannot close into anything.
            Assert.Throws<InvalidOperationException>(
                () => new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0) }).ToPolygonArc2());
        }

        [Fact]
        public void TheOperatorsSayWhatEqualsSays()
        {
            GeoPolygonArc2 slot = Slot();
            GeoPolygonArc2 same = Slot();

            Assert.True(slot == same);
            Assert.False(slot != same);
            Assert.False(slot == slot.Reverse());
            Assert.True((GeoPolygonArc2)null == (GeoPolygonArc2)null);
            Assert.True(slot != null);
            Assert.True(null != slot);

            GeoPolylineArc2 chain = Chain();

            Assert.True(chain == Chain());
            Assert.True(chain != chain.Reverse());
            Assert.False(chain == null);
        }
        [Fact]
        public void OneCornerCanBeCutOnItsOwnAndTheShorthandsWork()
        {
            var square = new GeoPolygonArc2(new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100)));

            Assert.True(square.TryChamferAt(1, 10.0, 20.0, out GeoPolygonArc2 cut));
            Assert.Equal(5, cut.VertexCount);
            Assert.False(cut.HasArcs);

            // Only that corner moved: the other three vertices are where they were.
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(0, 0), Tight));
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(90, 0), Tight));
            Assert.Contains(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(100, 20), Tight));
            Assert.DoesNotContain(cut.Vertices, v => v.IsEqualTo(new GeoPoint2(100, 0), Tight));

            // A corner against an arc is refused rather than guessed at.
            Assert.False(Slot().TryChamferAt(1, 5.0, 5.0, out _));

            // The ends of a chain are not corners.
            GeoPolylineArc2 chain = Chain();
            Assert.False(chain.TryChamferAt(0, 5.0, 5.0, out _));
            Assert.False(chain.TryChamferAt(chain.VertexCount - 1, 5.0, 5.0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => chain.TryChamferAt(99, 5.0, 5.0, out _));

            // Moving by a vector reads either way.
            var step = new GeoVector2(10, -5);

            Assert.True((square + step).IsEqualTo(square.Translate(step)));
            Assert.True((square + step - step).IsEqualTo(square));
            Assert.True((chain + step).IsEqualTo(chain.Translate(step)));

            // And the nearest piece of a curved loop may be an arc.
            GeoEdge2 nearest = Slot().GetClosestEdge(new GeoLine2(new GeoPoint2(300, 0), new GeoPoint2(300, 50)));
            Assert.True(nearest.IsArc);
            Assert.Equal(25.0, nearest.ToArc().Radius, 8);
        }
    }
}
