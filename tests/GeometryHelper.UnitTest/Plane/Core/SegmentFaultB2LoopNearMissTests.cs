using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment collides with a loop that may curve where it crosses the boundary, starts inside, or comes within the
    /// point tolerance of the boundary, as a segment does with an open curve.
    /// </summary>
    /// <remarks>
    /// The loop asked only for crossings and whether the segment's start lay inside, so a segment coming at a grazing
    /// angle to within the tolerance of an edge, its line meeting the edge further on than the tolerance past its end,
    /// did not collide. Before the bowed edges were read by their arcs, the scene below collided only because its start
    /// was wrongly read inside through the chord. The distance keeps measuring the gap.
    /// </remarks>
    public class SegmentFaultB2LoopNearMissTests
    {
        private static Tolerance Of(double equalPoint) => new Tolerance(equalPoint, 1E-5);

        /// <summary>
        /// The loop of Line-PolygonArc#157, a quadrilateral with two edges bowed inwards, by bulges -0.24 and -0.03.
        /// </summary>
        private static GeoPolygonArc2 Scene()
            => new GeoPolygonArc2(
                new[]
                {
                    P(24.806883156629215, 75.780150742220826), P(12.584658796972409, 50.129705882999886),
                    P(41.008163161248945, 46.181215165554775), P(48.8987865966493, 68.561530347969949),
                },
                new[] { -0.24, 0.0, 0.0, -0.03 });

        /// <summary>
        /// The segment of Line-PolygonArc#157, 0.3 long outside the loop, its end 0.0817 from the edge bowed by -0.03 and
        /// running nearly along it between the arc and its chord, outside the loop.
        /// </summary>
        private static GeoLine2 SceneSegment()
            => new GeoLine2(P(45.881361764584945, 69.4648016230239), P(45.581520021103209, 69.455058478468118));

        /// <summary>
        /// The segment from (101, 40) to (100 + gap, 60), coming at a grazing angle to within gap of the right side of
        /// <see cref="SegmentFaultShapes.SquareWithBowedTop"/>. Its line would meet the side about 21 gap further on,
        /// past its end, so the two do not cross.
        /// </summary>
        private static GeoLine2 EndingBesideTheSide(double gap) => new GeoLine2(P(101, 40), P(100 + gap, 60));

        [Fact]
        public void TheSceneSegmentEndingWithinATenthOfABowedEdge_CollidesWithTheLoopFromEverySide()
        {
            GeoPolygonArc2 loop = Scene();
            GeoLine2 segment = SceneSegment();

            Assert.True(Collision2.CollidesWith(loop, segment, Tenth));
            Assert.True(loop.CollidesWith(segment, Tenth));
            Assert.True(segment.CollidesWith(loop, Tenth));
            Assert.Equal(0.0817265800, loop.DistanceTo(segment, Tenth), 9);
            Assert.Equal(0.0817265800, segment.DistanceTo(loop, Tenth), 9);
        }

        [Fact]
        public void TheSceneSegment_DoesNotCollideWithTheLoopAtAThousandth()
        {
            GeoPolygonArc2 loop = Scene();
            GeoLine2 segment = SceneSegment();

            Assert.False(loop.CollidesWith(segment, Thousandth));
            Assert.False(segment.CollidesWith(loop, Thousandth));
        }

        [Fact]
        public void ASegmentEndingFiveHundredthsBesideAStraightSide_CollidesAtATenth()
        {
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);
            GeoLine2 segment = EndingBesideTheSide(0.05);

            Assert.True(loop.CollidesWith(segment, Tenth));
            Assert.True(segment.CollidesWith(loop, Tenth));
            Assert.Equal(0.05, loop.DistanceTo(segment, Tenth), 9);
        }

        [Theory]
        [InlineData(0.0005, true)]
        [InlineData(0.002, false)]
        public void ASegmentEndingBesideAStraightSide_CollidesAtAThousandthOnlyWithinIt(double gap, bool collides)
        {
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);
            GeoLine2 segment = EndingBesideTheSide(gap);

            Assert.Equal(collides, loop.CollidesWith(segment, Thousandth));
            Assert.Equal(collides, segment.CollidesWith(loop, Thousandth));
            Assert.Equal(gap, loop.DistanceTo(segment, Thousandth), 9);
        }

        [Theory]
        [InlineData(0.1, true)]
        [InlineData(1E-3, false)]
        public void ARayRunningFiveHundredthsBesideAStraightSide_CollidesOnlyWithinTheTolerance(double equalPoint, bool collides)
        {
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);
            var ray = new GeoRay2(P(100.05, -200), new GeoVector2(0, 1));

            Assert.Equal(collides, loop.CollidesWith(ray, Of(equalPoint)));
            Assert.Equal(collides, ray.CollidesWith(loop, Of(equalPoint)));
        }
    }
}
