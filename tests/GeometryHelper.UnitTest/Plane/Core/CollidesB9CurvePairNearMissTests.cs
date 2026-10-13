using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two curves, neither a segment nor a ray, collide where they cross, where one holds the other, or where they come
    /// within the point tolerance of each other; and two open curves lying along each other collide.
    /// </summary>
    /// <remarks>
    /// Arc against arc, polyline against arc, a chain against a polyline, an arc or a chain, a curved loop against an
    /// arc, a polyline, a chain or a loop, and the circle against an arc, a chain or a loop asked only for a crossing or
    /// for a start inside. So a curve ending half a thousandth above the top of another, at a shallow angle, did not
    /// collide with it at a tolerance of a thousandth, though their distance said 0.0005; and a polyline lying on a
    /// chain's straight edge did not collide at any tolerance, though their distance said nought. A polygon,
    /// rectangle, triangle or face against an arc, a chain or a curved loop read the same way (F2, Q3), and so did two
    /// edges tangent at a shared start, one nearly flat (F4).
    /// </remarks>
    public class CollidesB9CurvePairNearMissTests
    {
        /// <summary>The gap the near curves leave above the top of the circle of radius 50 about the origin.</summary>
        private const double Gap = 0.0005;

        /// <summary>The angle, 10 degrees, at which the near curves come down to their lowest point.</summary>
        private static readonly double Slope = 10 * Math.PI / 180;

        #region The far side: the top of the circle of radius 50 about the origin

        /// <summary>The upper half of the circle as a curved chain of one edge, from (50, 0) to (-50, 0) bulging 1.</summary>
        private static GeoPolylineArc2 TopChain() => new GeoPolylineArc2(new[] { P(50, 0), P(-50, 0) }, new[] { 1.0 });

        /// <summary>The curved loop of that half circle closed by two straight edges through (0, -50).</summary>
        private static GeoPolygonArc2 TopLoop() => new GeoPolygonArc2(new[] { P(50, 0), P(-50, 0), P(0, -50) }, new[] { 1.0, 0.0, 0.0 });

        #endregion

        #region The near side: curves whose lowest point is (0, 50 + gap)

        /// <summary>The lowest point of every near curve, straight above the top of the circle.</summary>
        private static GeoPoint2 Low(double gap) => P(0, 50 + gap);

        /// <summary>
        /// The segment 10 long coming down at 10 degrees from the upper left to (0, 50 + gap). Going on, its line would
        /// meet the circle about gap / sin 10 degrees further, 0.0029 past its end for a gap of 0.0005, outside the end
        /// slack of a crossing at a thousandth; so the nearest pair is its end and (0, 50), gap apart.
        /// </summary>
        private static GeoPolyline2 DescendingPolyline(double gap)
            => new GeoPolyline2(P(-10 * Math.Cos(Slope), 50 + gap + 10 * Math.Sin(Slope)), Low(gap));

        /// <summary>The same segment as a straight chain.</summary>
        private static GeoPolylineArc2 DescendingChain(double gap)
            => new GeoPolylineArc2(new[] { P(-10 * Math.Cos(Slope), 50 + gap + 10 * Math.Sin(Slope)), Low(gap) }, new[] { 0.0 });

        /// <summary>
        /// The arc of radius 20 sweeping one radian counter-clockwise down to (0, 50 + gap), arriving there at 10 degrees
        /// below the horizontal: its centre lies 20 from that point at right angles to the way, at angle -100 degrees seen
        /// from it. Every other point of it is higher and further left, so it too comes nearest at its end.
        /// </summary>
        private static GeoArc2 DescendingArc(double gap)
        {
            GeoPoint2 low = Low(gap);
            var centre = P(low.X + 20 * Math.Sin(Slope), low.Y + 20 * Math.Cos(Slope));
            double end = -(Math.PI / 2 + Slope);

            return new GeoArc2(centre, 20, end - 1, end, false);
        }

        /// <summary>
        /// The triangle hanging from (0, 50 + gap), its two sides rising from there at 10 degrees, 10 long. The low corner
        /// is listed last, so that no start-inside test reads it.
        /// </summary>
        private static GeoPolygonArc2 HangingLoop(double gap)
        {
            GeoPoint2 low = Low(gap);
            var right = P(10 * Math.Cos(Slope), low.Y + 10 * Math.Sin(Slope));
            var left = P(-10 * Math.Cos(Slope), low.Y + 10 * Math.Sin(Slope));

            return new GeoPolygonArc2(new[] { right, left, low }, new[] { 0.0, 0.0, 0.0 });
        }

        #endregion

        /// <summary>
        /// One pair, both ways round: whether they collide, and their distance, asked from either side, at a tolerance.
        /// </summary>
        private sealed class Pair
        {
            internal Func<Tolerance, bool> Collides, CollidesBack;
            internal Func<Tolerance, double> Distance, DistanceBack;
        }

        private static Pair Make(string name, double gap)
        {
            switch (name)
            {
                case "arc-arc":
                    {
                        GeoArc2 a = UpperHalf(), b = DescendingArc(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "arc-polyline":
                    {
                        GeoArc2 a = UpperHalf();
                        GeoPolyline2 b = DescendingPolyline(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "chain-polyline":
                    {
                        GeoPolylineArc2 a = TopChain();
                        GeoPolyline2 b = DescendingPolyline(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "chain-arc":
                    {
                        GeoPolylineArc2 a = TopChain();
                        GeoArc2 b = DescendingArc(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "arc-chain":
                    {
                        GeoArc2 a = UpperHalf();
                        GeoPolylineArc2 b = DescendingChain(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "chain-chain":
                    {
                        GeoPolylineArc2 a = TopChain(), b = DescendingChain(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "chain-curved chain":
                    {
                        GeoPolylineArc2 a = TopChain();
                        var b = new GeoPolylineArc2(new[] { new GeoEdge2(DescendingArc(gap)) });
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "loop-arc":
                    {
                        GeoPolygonArc2 a = TopLoop();
                        GeoArc2 b = DescendingArc(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "loop-polyline":
                    {
                        GeoPolygonArc2 a = TopLoop();
                        GeoPolyline2 b = DescendingPolyline(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "loop-chain":
                    {
                        GeoPolygonArc2 a = TopLoop();
                        GeoPolylineArc2 b = DescendingChain(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "loop-loop":
                    {
                        GeoPolygonArc2 a = TopLoop(), b = HangingLoop(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "circle-arc":
                    {
                        GeoCircle2 a = Circle();
                        GeoArc2 b = DescendingArc(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "circle-chain":
                    {
                        GeoCircle2 a = Circle();
                        GeoPolylineArc2 b = DescendingChain(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                case "circle-loop":
                    {
                        GeoCircle2 a = Circle();
                        GeoPolygonArc2 b = HangingLoop(gap);
                        return new Pair { Collides = t => a.CollidesWith(b, t), CollidesBack = t => b.CollidesWith(a, t), Distance = t => a.DistanceTo(b, t), DistanceBack = t => b.DistanceTo(a, t) };
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "No such pair.");
            }
        }

        [Theory]
        [InlineData("arc-arc")]
        [InlineData("arc-polyline")]
        [InlineData("chain-polyline")]
        [InlineData("chain-arc")]
        [InlineData("arc-chain")]
        [InlineData("chain-chain")]
        [InlineData("chain-curved chain")]
        [InlineData("loop-arc")]
        [InlineData("loop-polyline")]
        [InlineData("loop-chain")]
        [InlineData("loop-loop")]
        [InlineData("circle-arc")]
        [InlineData("circle-chain")]
        [InlineData("circle-loop")]
        public void ACurveComingFiveTenThousandthsShortOfAnother_CollidesAtAThousandthAndATenthButNotATenThousandth(string name)
        {
            // Half a thousandth apart: more than 1E-4, within 1E-3 and 0.1. The distance is the gap at the two tighter
            // tolerances, where nothing crosses within the end slack.
            Pair pair = Make(name, Gap);

            Assert.False(pair.Collides(TenThousandth));
            Assert.False(pair.CollidesBack(TenThousandth));
            Assert.True(pair.Collides(Thousandth));
            Assert.True(pair.CollidesBack(Thousandth));
            Assert.True(pair.Collides(Tenth));
            Assert.True(pair.CollidesBack(Tenth));
            Assert.Equal(Gap, pair.Distance(TenThousandth), 1E-9);
            Assert.Equal(Gap, pair.DistanceBack(TenThousandth), 1E-9);
            Assert.Equal(Gap, pair.Distance(Thousandth), 1E-9);
            Assert.Equal(Gap, pair.DistanceBack(Thousandth), 1E-9);
        }

        [Theory]
        [InlineData("arc-arc")]
        [InlineData("arc-polyline")]
        [InlineData("chain-polyline")]
        [InlineData("chain-arc")]
        [InlineData("arc-chain")]
        [InlineData("chain-chain")]
        [InlineData("chain-curved chain")]
        [InlineData("loop-arc")]
        [InlineData("loop-polyline")]
        [InlineData("loop-chain")]
        [InlineData("loop-loop")]
        [InlineData("circle-arc")]
        [InlineData("circle-chain")]
        [InlineData("circle-loop")]
        public void ACurveComingTwoThousandthsShortOfAnother_DoesNotCollideAtAThousandth(string name)
        {
            // A guard: twice the tolerance apart.
            Pair pair = Make(name, 0.002);

            Assert.False(pair.Collides(Thousandth));
            Assert.False(pair.CollidesBack(Thousandth));
            Assert.Equal(0.002, pair.Distance(Thousandth), 1E-9);
            Assert.Equal(0.002, pair.DistanceBack(Thousandth), 1E-9);
        }

        [Fact]
        public void AnArcEndingFiveTenThousandthsFromAnother_CollidesEvenWhereItsCircleCrossesTheOtherFarOff()
        {
            // Arc-Arc#191 of the audit: the end of the second arc lies 0.0005 from the first (the oracle's distance, and
            // the library's own), and their circles cross well away from both. On 2755f30 false even at 0.1.
            var first = new GeoArc2(P(21.391250534677738, 33.4279742297677), 30, -0.12414555675539819, -0.12414555675539819 - 3, true);
            var second = new GeoArc2(P(26.081473765001391, 32.868166562574061), 25.27606689709987, 3.6511742160359923, 3.6511742160359923 + 3.1889034258615707, false);

            Assert.Equal(Gap, first.DistanceTo(second, Thousandth), 1E-9);
            Assert.False(Collision2.CollidesWith(first, second, TenThousandth));
            Assert.True(Collision2.CollidesWith(first, second, Thousandth));
            Assert.True(Collision2.CollidesWith(second, first, Thousandth));
            Assert.True(Collision2.CollidesWith(first, second, Tenth));
            Assert.True(Collision2.CollidesWith(second, first, Tenth));
        }

        #region B10: open curves lying along each other

        [Fact]
        public void APolylineLyingOnTheStraightEdgeOfAChain_CollidesWithItAtEveryTolerance()
        {
            // The polyline from (10, 0) to (20, 0) lies on the chain's first edge, from (0, 0) to (30, 0).
            var polyline = new GeoPolyline2(P(10, 0), P(20, 0));
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(30, 0), P(40, 10) }, new[] { 0.0, 0.0 });

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.True(polyline.CollidesWith(chain, tolerance));
                Assert.True(chain.CollidesWith(polyline, tolerance));
                Assert.True(Collision2.CollidesWith(chain, polyline, tolerance));
                Assert.Equal(0.0, chain.DistanceTo(polyline, tolerance));
            }
        }

        [Fact]
        public void AChainLyingOnTheStraightEdgeOfAnother_CollidesWithItAtEveryTolerance()
        {
            var shorter = new GeoPolylineArc2(new[] { P(10, 0), P(20, 0) }, new[] { 0.0 });
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(30, 0), P(40, 10) }, new[] { 0.0, 0.0 });
            var polyline = new GeoPolyline2(P(0, 0), P(30, 0), P(40, 10));

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.True(shorter.CollidesWith(chain, tolerance));
                Assert.True(chain.CollidesWith(shorter, tolerance));
                Assert.True(shorter.CollidesWith(polyline, tolerance));
                Assert.True(polyline.CollidesWith(shorter, tolerance));
            }
        }

        [Fact]
        public void AnArcLyingOnTheCurvedEdgeOfAChain_CollidesWithItAtEveryTolerance()
        {
            // The arc of radius 50 about the origin from 0.2 rad sweeping 0.5 lies on the chain's half circle; two arcs
            // of one circle already collide, and so must the arc and the chain made of the other.
            var arc = new GeoArc2(P(0, 0), 50, 0.2, 0.7);
            GeoPolylineArc2 chain = TopChain();

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.True(arc.CollidesWith(chain, tolerance));
                Assert.True(chain.CollidesWith(arc, tolerance));
                Assert.True(arc.CollidesWith(UpperHalf(), tolerance));
            }
        }

        [Fact]
        public void TwoChainsTenThousandthOfARadianApartFromACommonPoint_CollideAtEveryTolerance()
        {
            // The second starts on the first, at (20, 0), and leaves it at 1E-4 rad. At 0.1, 1E-4 x 80 is within the
            // tolerance and the two edges read as parallel, so on 2755f30 they collided at 1E-4 and 1E-3 but not at 0.1.
            var first = new GeoPolylineArc2(new[] { P(0, 0), P(80, 0) }, new[] { 0.0 });
            var second = new GeoPolylineArc2(new[] { P(20, 0), P(60, 0.004) }, new[] { 0.0 });

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.True(first.CollidesWith(second, tolerance));
                Assert.True(second.CollidesWith(first, tolerance));
            }
        }

        [Fact]
        public void TwoPolylinesTenThousandthOfARadianApartFromACommonPoint_StillCollideAtEveryTolerance()
        {
            // A guard: the straight pair already reads the start lying on the other.
            var first = new GeoPolyline2(P(0, 0), P(80, 0));
            var second = new GeoPolyline2(P(20, 0), P(60, 0.004));

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.True(first.CollidesWith(second, tolerance));
                Assert.True(second.CollidesWith(first, tolerance));
            }
        }

        #endregion

        #region F2 (Q3): straight closed shapes against curves

        /// <summary>The rectangle 100 by 50 from (-50, 0) to (50, 50): its top runs along y = 50, under the near curves.</summary>
        private static GeoPoint2[] TopBox() => new[] { P(-50, 0), P(50, 0), P(50, 50), P(-50, 50) };

        [Theory]
        [InlineData("arc")]
        [InlineData("chain")]
        [InlineData("curved chain")]
        [InlineData("loop")]
        public void ACurveComingFiveTenThousandthsShortOfAStraightClosedShape_CollidesAtAThousandthButNotATenThousandth(string near)
        {
            // The near curves of the theory above, against the polygon, the rectangle, the triangle and the face whose
            // top is y = 50 (Collision2.ArcChain.cs:94, Collision2.cs:631, :883 and the forms through them).
            GeoPoint2[] box = TopBox();
            var polygon = new GeoPolygon2(box);
            var rectangle = new GeoRectangle2(P(0, 25), 100, 50);
            var triangle = new GeoTriangle2(box[0], box[2], box[3]);
            var face = new GeoFace2(polygon, new[] { new GeoPolygon2(P(-5, 20), P(5, 20), P(5, 30), P(-5, 30)) });
            Func<GeometryHelper.Tolerance, bool>[] asks;

            switch (near)
            {
                case "arc":
                    {
                        GeoArc2 c = DescendingArc(Gap);
                        asks = new Func<GeometryHelper.Tolerance, bool>[] { t => c.CollidesWith(polygon, t), t => polygon.CollidesWith(c, t), t => c.CollidesWith(rectangle, t), t => rectangle.CollidesWith(c, t), t => c.CollidesWith(triangle, t), t => triangle.CollidesWith(c, t), t => c.CollidesWith(face, t), t => face.CollidesWith(c, t) };
                        break;
                    }

                case "chain":
                    {
                        GeoPolylineArc2 c = DescendingChain(Gap);
                        asks = new Func<GeometryHelper.Tolerance, bool>[] { t => c.CollidesWith(polygon, t), t => polygon.CollidesWith(c, t), t => c.CollidesWith(rectangle, t), t => rectangle.CollidesWith(c, t), t => c.CollidesWith(triangle, t), t => triangle.CollidesWith(c, t), t => c.CollidesWith(face, t), t => face.CollidesWith(c, t) };
                        break;
                    }

                case "curved chain":
                    {
                        var c = new GeoPolylineArc2(new[] { new GeoEdge2(DescendingArc(Gap)) });
                        asks = new Func<GeometryHelper.Tolerance, bool>[] { t => c.CollidesWith(polygon, t), t => polygon.CollidesWith(c, t), t => c.CollidesWith(rectangle, t), t => rectangle.CollidesWith(c, t), t => c.CollidesWith(triangle, t), t => triangle.CollidesWith(c, t), t => c.CollidesWith(face, t), t => face.CollidesWith(c, t) };
                        break;
                    }

                default:
                    {
                        GeoPolygonArc2 c = HangingLoop(Gap);
                        asks = new Func<GeometryHelper.Tolerance, bool>[] { t => c.CollidesWith(polygon, t), t => polygon.CollidesWith(c, t), t => c.CollidesWith(rectangle, t), t => rectangle.CollidesWith(c, t), t => c.CollidesWith(triangle, t), t => triangle.CollidesWith(c, t), t => c.CollidesWith(face, t), t => face.CollidesWith(c, t) };
                        break;
                    }
            }

            foreach (Func<GeometryHelper.Tolerance, bool> ask in asks)
            {
                Assert.True(ask(Thousandth));
                Assert.True(ask(Tenth));
                Assert.False(ask(TenThousandth));
            }
        }

        [Fact]
        public void ACurvedLoopFiveTenThousandthsAboveAPolygon_CollidesWithItAtAThousandth()
        {
            // Loop against polygon (Collision2.cs:883), the polygon's top half a thousandth below the loop's low corner.
            GeoPolygonArc2 loop = HangingLoop(Gap);
            var polygon = new GeoPolygon2(TopBox());

            Assert.True(Collision2.CollidesWith(loop, polygon, Thousandth));
            Assert.True(polygon.CollidesWith(loop, Thousandth));
            Assert.Equal(Gap, loop.DistanceTo(polygon, Thousandth), 1E-9);
        }

        #endregion

        #region F4: two edges tangent at a shared start

        [Fact]
        public void TwoEdgesTangentAtTheirSharedStartOneNearlyFlat_CollideAtATenThousandth()
        {
            // Edge-Edge#370 of the audit: they share their start point exactly and leave it 1E-5 rad apart, one bowed by
            // -1E-7; their distance is 7E-10, but on 2755f30 they did not collide at 1E-4, from either side.
            var bowed = new GeoEdge2(P(74.529013770878777, 81.543504810679465), P(111.83421153607796, 77.24277921629124), 0.3);
            var flat = new GeoEdge2(P(74.529013770878777, 81.543504810679465), P(78.360617379513513, 78.331213761241813), -1E-07);

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.True(bowed.CollidesWith(flat, tolerance));
                Assert.True(flat.CollidesWith(bowed, tolerance));
            }

            Assert.True(bowed.DistanceTo(flat, TenThousandth) <= 1E-9);
        }

        #endregion
    }
}
