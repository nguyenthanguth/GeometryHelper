using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// When an arc's radius runs out, the joins either side of it are drawn about its own two ends, and the loop or chain
    /// comes back whole, whichever vertex it starts from and whichever way it runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An arc whose radius runs out was dropped, and the next join drawn about the end of the edge kept before it rather
    /// than about the vertices the dropped arc spanned; and a closed offset whose runs did not chain back to their start
    /// was dropped whole. The rectangle 100 by 30 with its right side bowed 6 in (radius 21.75) grown by 40 with round
    /// joins came back as one loop with a half circle of radius 55 about (100, 15), standing 40.18 from the rectangle;
    /// begun from its second vertex, or run the other way, it came back as nothing. Grown by 40, the bowed side's
    /// radius would be -18.25: the right of the answer is the two corner circles of 40 about (100, 0) and (100, 30),
    /// meeting at (100 + sqrt 1375, 15) = (137.08099, 15). Its area is the rectangle's 3 000, three sides of 40 (8 000 and
    /// 1 200), two quarter circles (2 513.27) and the right lobe of two sectors and two triangles (3 684.52): 18 397.80.
    /// </para>
    /// <para>
    /// The chain 10 along x, a half turn of radius 5 below the axis to (20, 0) and 10 more, offset by 10 upwards with
    /// round joins, threw "The edges of a chain must run end to end". The half circle's radius would be -5; the answer runs
    /// along y = 10 to (10, 10), round 30 degrees of the circle of 10 about (10, 0) to (15, 5 sqrt 3), 30 degrees of the
    /// circle about (20, 0) to (20, 10), and on along y = 10: 20 + 10 pi / 3 = 30.47198 long.
    /// </para>
    /// </remarks>
    public class OffsetFaultS5DroppedArcTests
    {
        private const double GrownArea = 18397.7979714689;

        private static readonly OffsetOptions Round = new OffsetOptions(OffsetJoin.Round);

        /// <summary>The same loop begun at vertex <paramref name="start"/>.</summary>
        private static GeoPolygonArc2 StartingAt(GeoPolygonArc2 loop, int start)
        {
            int n = loop.VertexCount;
            return new GeoPolygonArc2(
                Enumerable.Range(0, n).Select(i => loop.Vertices[(i + start) % n]),
                Enumerable.Range(0, n).Select(i => loop.Bulges[(i + start) % n]));
        }

        /// <summary>The same loop run the other way: vertex i comes from n - 1 - i, and edge i is edge n - 2 - i reversed.</summary>
        private static GeoPolygonArc2 Reversed(GeoPolygonArc2 loop)
        {
            int n = loop.VertexCount;
            return new GeoPolygonArc2(
                Enumerable.Range(0, n).Select(i => loop.Vertices[n - 1 - i]),
                Enumerable.Range(0, n).Select(i => -loop.Bulges[((n - 2 - i) % n + n) % n]));
        }

        private static void AssertGrownRectangle(GeoPolygonArc2[] grown, GeoPolygonArc2 source)
        {
            GeoPolygonArc2 one = Assert.Single(grown);
            Assert.Equal(GrownArea, one.Area, 1E-6);
            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(source), 40.0) <= Thousandth.EqualPoint);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(100 + Math.Sqrt(1375.0), 15)) <= 1E-6);
        }

        [Fact]
        public void ARectangleWithABowedInSideGrownPastTheBowsRadius_IsRoundedAboutTheBowsTwoEnds()
        {
            GeoPolygonArc2 rectangle = RectangleWithBowedInSide();

            AssertGrownRectangle(Offset2.Offset(rectangle, 40.0, Round, Thousandth), rectangle);
        }

        [Fact]
        public void TheSameRectangleBegunFromItsSecondVertex_GrowsTheSame()
        {
            GeoPolygonArc2 rectangle = StartingAt(RectangleWithBowedInSide(), 1);

            AssertGrownRectangle(Offset2.Offset(rectangle, 40.0, Round, Thousandth), rectangle);
        }

        [Fact]
        public void TheSameRectangleRunTheOtherWay_GrowsTheSame()
        {
            GeoPolygonArc2 rectangle = Reversed(RectangleWithBowedInSide());

            AssertGrownRectangle(Offset2.Offset(rectangle, 40.0, Round, Thousandth), rectangle);
        }

        [Fact]
        public void TheSameRectangleGrownLessThanTheBowsRadius_KeepsTheBowAsAnArc()
        {
            // Guard: grown by 10 the bow keeps a radius of 21.75 - 10 = 11.75 about (115.75, 15), the joins at its ends turn
            // 133.6 degrees round (100, 0) and (100, 30), and every point stands 10 from the rectangle.
            GeoPolygonArc2 rectangle = RectangleWithBowedInSide();

            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(rectangle, 10.0, Round, Thousandth));

            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(rectangle), 10.0) <= 1E-9);
            Assert.Contains(one.GetEdges(), e => e.IsArc && Math.Abs(e.ToArc().Radius - 11.75) <= 1E-9);
        }

        [Fact]
        public void AChainWhoseHalfTurnRunsOut_IsJoinedRoundTheHalfTurnsEnds()
        {
            GeoPolylineArc2 chain = Chain(new[] { P(0, 0), P(10, 0), P(20, 0), P(30, 0) }, 0.0, 1.0, 0.0);

            GeoPolylineArc2[] offset = Offset2.Offset(chain, 10.0, Round, Thousandth);

            GeoPolylineArc2 one = Assert.Single(offset);
            Assert.Equal(20.0 + 10.0 * Math.PI / 3.0, one.Length, 1E-9);
            Assert.Equal(0.0, one.StartPoint.DistanceTo(P(0, 10)), 1E-9);
            Assert.Equal(0.0, one.EndPoint.DistanceTo(P(30, 10)), 1E-9);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(15, 5 * Math.Sqrt(3.0))) <= 1E-9);
            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(chain), 10.0) <= 1E-9);
        }
    }
}
