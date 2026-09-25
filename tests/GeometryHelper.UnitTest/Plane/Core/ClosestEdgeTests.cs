using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Asking a shape which of its own edges lies nearest something else. The answer is a piece of the
    /// shape, never a segment built to join two shapes: that is what <c>GetShortestLineTo</c> is for, and
    /// the two were once confused with each other under one name.
    /// </summary>
    public class ClosestEdgeTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A square of side one hundred, counter-clockwise from the origin. Edge 0 is the bottom.
        /// </summary>
        private static GeoPolygon2 Square() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

        private static GeoPolyline2 Chain() => new GeoPolyline2(
            new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100));

        private static GeoRectangle2 Plate() => new GeoRectangle2(0, 0, 100, 60);

        /// <summary>
        /// The same square with its right-hand side swelled into a half circle reaching out to x = 150.
        /// </summary>
        private static GeoPolygonArc2 Slot() => new GeoPolygonArc2(
            new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
            new[] { 0.0, 1.0, 0.0, 0.0 });

        private static GeoPolylineArc2 OpenSlot() => Slot().ToPolylineArc2();



        [Fact]
        public void TheEdgeHandedBackIsOneOfTheShapesOwnAndIsTheNearestOfThem()
        {
            GeoPolygon2 square = Square();
            var probe = new GeoPoint2(50, -20);

            GeoLine2 nearest = square.GetClosestEdge(probe);

            // It is an edge of the square, not something built for the occasion.
            Assert.Contains(square.GetEdges(), edge => edge.IsEqualTo(nearest, Tight));

            // And none of the others is nearer.
            double best = square.GetEdges().Min(edge => Distance2.DistanceTo(edge, probe));
            Assert.Equal(best, Distance2.DistanceTo(nearest, probe), 9);
            Assert.Equal(20.0, best, 9);
        }

        [Fact]
        public void AProbeInsideAClosedShapeStillNamesAnEdge()
        {
            // Distance2 reads a polygon as a filled region and calls an inside point nought away. Here
            // every edge is measured on its own, so the question still has an answer.
            GeoPolygon2 square = Square();
            var inside = new GeoPoint2(50, 40);

            Assert.Equal(0.0, square.DistanceTo(inside), 9);

            GeoLine2 nearest = square.GetClosestEdge(inside);

            Assert.True(nearest.IsEqualTo(square.GetEdgeAt(0), Tight));
            Assert.Equal(40.0, Distance2.DistanceTo(nearest, inside), 9);
        }

        [Fact]
        public void EquallyNearEdgesGoToTheEarlierOne()
        {
            // The centre of the square stands fifty from all four sides.
            GeoPolygon2 square = Square();
            GeoLine2 nearest = square.GetClosestEdge(new GeoPoint2(50, 50));

            Assert.True(nearest.IsEqualTo(square.GetEdgeAt(0), Tight));

            // A segment cutting clean through touches two edges at nothing at all; the first still wins.
            var knife = new GeoLine2(new GeoPoint2(-50, 50), new GeoPoint2(150, 50));

            Assert.True(square.GetClosestEdge(knife).IsEqualTo(square.GetEdgeAt(1), Tight));
        }

        [Fact]
        public void EverySortOfProbeIsAnsweredByEverySortOfStraightShape()
        {
            GeoPolygon2 square = Square();
            GeoPolyline2 chain = Chain();
            GeoRectangle2 plate = Plate();

            var point = new GeoPoint2(120, 50);

            // Short, and level with the middle of the square, so only the right-hand side is nearest it.
            var line = new GeoLine2(new GeoPoint2(300, 40), new GeoPoint2(300, 60));
            var circle = new GeoCircle2(new GeoPoint2(50, 300), 10.0);

            // The right-hand side faces the point and the far segment; the top faces the high circle.
            Assert.True(square.GetClosestEdge(point).IsEqualTo(square.GetEdgeAt(1), Tight));
            Assert.True(square.GetClosestEdge(line).IsEqualTo(square.GetEdgeAt(1), Tight));
            Assert.True(square.GetClosestEdge(circle).IsEqualTo(square.GetEdgeAt(2), Tight));

            Assert.True(chain.GetClosestEdge(point).IsEqualTo(chain.GetEdgeAt(1), Tight));
            Assert.True(chain.GetClosestEdge(line).IsEqualTo(chain.GetEdgeAt(1), Tight));
            Assert.True(chain.GetClosestEdge(circle).IsEqualTo(chain.GetEdgeAt(1), Tight));

            foreach (GeoLine2 answer in new[]
            {
                plate.GetClosestEdge(point), plate.GetClosestEdge(line), plate.GetClosestEdge(circle)
            })
            {
                Assert.Contains(plate.GetEdges(), edge => edge.IsEqualTo(answer, Tight));
            }
        }

        [Fact]
        public void AProbeStandingOffAWholeCornerIsATieAndTheEarlierEdgeTakesIt()
        {
            // A segment running the full height two hundred out to the right stands exactly two hundred
            // from the bottom edge, the right-hand edge and the top edge alike: from the corner (100, 0),
            // from the side, and from the corner (100, 100). Three edges, one distance.
            GeoPolygon2 square = Square();
            var far = new GeoLine2(new GeoPoint2(300, 0), new GeoPoint2(300, 100));

            double[] reaches = square.GetEdges().Select(edge => Distance2.DistanceTo(edge, far)).ToArray();

            Assert.Equal(200.0, reaches[0], 9);
            Assert.Equal(200.0, reaches[1], 9);
            Assert.Equal(200.0, reaches[2], 9);

            // The bottom edge comes first, so the bottom edge is the answer.
            Assert.True(square.GetClosestEdge(far).IsEqualTo(square.GetEdgeAt(0), Tight));
        }

        [Fact]
        public void ACurvedShapeCanHandBackAnArc()
        {
            GeoPolygonArc2 slot = Slot();
            var point = new GeoPoint2(200, 50);

            GeoEdge2 nearest = slot.GetClosestEdge(point);

            Assert.True(nearest.IsArc);
            Assert.Equal(50.0, nearest.ToArc().Radius, 9);

            // The half circle reaches x = 150, so the point stands fifty clear of it.
            Assert.Equal(50.0, nearest.DistanceTo(point), 9);

            // And no straight edge of the slot comes nearer.
            Assert.Equal(slot.GetEdges().Min(edge => edge.DistanceTo(point)), nearest.DistanceTo(point), 9);
        }

        [Fact]
        public void ACurvedShapeAnswersAllFourProbesAndBothTolerances()
        {
            GeoPolygonArc2 slot = Slot();
            GeoPolylineArc2 open = OpenSlot();

            var point = new GeoPoint2(200, 50);
            var line = new GeoLine2(new GeoPoint2(300, 0), new GeoPoint2(300, 100));
            var circle = new GeoCircle2(new GeoPoint2(200, 50), 10.0);
            var arc = new GeoArc2(new GeoPoint2(200, 50), 10.0, 0.0, Math.PI);

            Assert.True(slot.GetClosestEdge(point).IsEqualTo(slot.GetClosestEdge(point, Tolerance.Global)));
            Assert.True(slot.GetClosestEdge(line).IsEqualTo(slot.GetClosestEdge(line, Tolerance.Global)));
            Assert.True(slot.GetClosestEdge(circle).IsEqualTo(slot.GetClosestEdge(circle, Tolerance.Global)));
            Assert.True(slot.GetClosestEdge(arc).IsEqualTo(slot.GetClosestEdge(arc, Tolerance.Global)));

            // All four probes sit out beyond the bulge, so all four are answered by it.
            foreach (GeoEdge2 answer in new[]
            {
                slot.GetClosestEdge(point), slot.GetClosestEdge(line),
                slot.GetClosestEdge(circle), slot.GetClosestEdge(arc)
            })
            {
                Assert.True(answer.IsArc);
            }

            Assert.True(open.GetClosestEdge(point).IsArc);
            Assert.True(open.GetClosestEdge(line, Tolerance.Global).IsArc);
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNothing()
        {
            GeoPolygon2 square = Square();
            GeoPolyline2 chain = Chain();
            GeoRectangle2 plate = Plate();
            GeoPolygonArc2 slot = Slot();
            GeoPolylineArc2 open = OpenSlot();

            var point = new GeoPoint2(120, 50);
            var line = new GeoLine2(new GeoPoint2(300, 0), new GeoPoint2(300, 100));
            var circle = new GeoCircle2(new GeoPoint2(50, 300), 10.0);
            var arc = new GeoArc2(new GeoPoint2(200, 50), 10.0, 0.0, Math.PI);

            Assert.True(ClosestEdge2.GetClosestEdge(square, point).IsEqualTo(square.GetClosestEdge(point), Tight));
            Assert.True(ClosestEdge2.GetClosestEdge(square, line).IsEqualTo(square.GetClosestEdge(line), Tight));
            Assert.True(ClosestEdge2.GetClosestEdge(square, circle).IsEqualTo(square.GetClosestEdge(circle), Tight));
            Assert.True(ClosestEdge2.GetClosestEdge(chain, point).IsEqualTo(chain.GetClosestEdge(point), Tight));
            Assert.True(ClosestEdge2.GetClosestEdge(plate, point).IsEqualTo(plate.GetClosestEdge(point), Tight));
            Assert.True(ClosestEdge2.GetClosestEdge(slot, arc).IsEqualTo(slot.GetClosestEdge(arc)));
            Assert.True(ClosestEdge2.GetClosestEdge(open, arc, Tolerance.Global).IsEqualTo(open.GetClosestEdge(arc, Tolerance.Global)));

            Assert.Throws<ArgumentNullException>(() => ClosestEdge2.GetClosestEdge((GeoPolygon2)null, point));
            Assert.Throws<ArgumentNullException>(() => ClosestEdge2.GetClosestEdge((GeoPolyline2)null, line));
            Assert.Throws<ArgumentNullException>(() => ClosestEdge2.GetClosestEdge((GeoPolygonArc2)null, arc));
            Assert.Throws<ArgumentNullException>(() => ClosestEdge2.GetClosestEdge((GeoPolylineArc2)null, arc, Tolerance.Global));
        }
    }
}
