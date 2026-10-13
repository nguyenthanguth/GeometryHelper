using System;
using System.Collections.Generic;
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
    /// The shapes the audit, the S4 rerun and the ripple found wrong where an arc's radius runs out or a curved loop
    /// failed to close, and the three readings that close them: a cusp opening round its tip, runs meeting out of the
    /// order they were cut in, and a lens of two arcs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every expected point is either computed by hand, as written beside it, or is the point the audit oracle reported
    /// missing on the build before (4fe3c80). Where joins may run further than the distance, the answer is held to
    /// what the oracle holds it to: no point of it nearer to the source than the distance, the oracle's point on it,
    /// and the same area whichever vertex the loop starts from and whichever way it runs.
    /// </para>
    /// <para>
    /// The ripple's loop #25 at the tolerance (1E-4, 1E-6), offset by -10, came back empty; so did the loop the S4
    /// rerun found as GA 12734 grown by 80 at 0.1, and the star GA 15298 grown by 1, whose runs meet out of order
    /// round a twisted spike. The chain PA 18671 offset by 80 gave nothing one way and a piece the other. The
    /// crescent C19 (its bump made simple, bulge 0.3) grown by 2.773 came back empty.
    /// </para>
    /// </remarks>
    public class OffsetFaultS5SpentArcTests
    {
        private static readonly Tolerance TenThousandth = new Tolerance(1E-4, 1E-6);

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

        /// <summary>The loop begun from every vertex, and run the other way.</summary>
        private static IEnumerable<GeoPolygonArc2> EveryWay(GeoPolygonArc2 loop)
        {
            for (int i = 0; i < loop.VertexCount; i++)
            {
                yield return StartingAt(loop, i);
            }

            yield return Reversed(loop);
        }

        /// <summary>The same chain run the other way.</summary>
        private static GeoPolylineArc2 Reversed(GeoPolylineArc2 chain)
        {
            int n = chain.VertexCount;
            return new GeoPolylineArc2(
                Enumerable.Range(0, n).Select(i => chain.Vertices[n - 1 - i]),
                Enumerable.Range(0, n).Select(i => i < n - 1 ? -chain.Bulges[n - 2 - i] : 0.0));
        }

        /// <summary>How near the answer comes to the source, over 25 points a piece.</summary>
        private static double Nearest(Piece[] answer, Piece[] source)
        {
            double nearest = double.PositiveInfinity;
            foreach (Piece p in answer)
            {
                for (int k = 0; k <= 24; k++)
                {
                    p.At(k / 24.0, out double x, out double y);
                    nearest = Math.Min(nearest, DistanceTo(source, x, y));
                }
            }
            return nearest;
        }

        /// <summary>
        /// Holds a loop's offset to the oracle's reading from every vertex and both ways: one loop, nothing nearer than
        /// the distance, the point on it, and one area.
        /// </summary>
        private static void AssertOneLoopEveryWay(GeoPolygonArc2 source, double distance, OffsetOptions options, Tolerance tolerance, GeoPoint2 onIt)
        {
            double area = double.NaN;

            foreach (GeoPolygonArc2 loop in EveryWay(source))
            {
                GeoPolygonArc2 one = Assert.Single(Offset2.Offset(loop, distance, options, tolerance));

                Assert.True(Nearest(PiecesOf(one), PiecesOf(source)) >= Math.Abs(distance) - tolerance.EqualPoint);
                Assert.True(DistanceTo(PiecesOf(one), onIt.X, onIt.Y) <= tolerance.EqualPoint);

                if (double.IsNaN(area))
                {
                    area = one.Area;
                }
                else
                {
                    Assert.Equal(area, one.Area, 1E-6);
                }
            }
        }

        [Fact]
        public void TheRipplesLoopTwentyFiveOffsetByMinusTen_ComesBackOneLoopFromEveryVertexAndBothWays()
        {
            // The first arc, bulge 0.48 and radius 9.127, runs out at 10. The point the oracle found missing: the start of
            // the arc of bulge 0.24 from (81.481, 19.999), moved 10 in.
            GeoPolygonArc2 source = Loop(
                new[]
                {
                    P(86.599909020618355, 42.541093657196484), P(77.254510766822335, 31.792613079980093),
                    P(73.481982550741847, 23.351300746392063), P(81.481282980552763, 19.998997294721079),
                    P(94.4075905344884, 15.955555821220791), P(100.79629506502833, 29.744013488297355),
                    P(100.90765793699708, 39.347132073683511),
                },
                0.48, -0.03, 0.03, 0.24, 0.0, 0.12, 0.03);

            AssertOneLoopEveryWay(source, -10.0, OffsetOptions.Default, TenThousandth, P(88.473116726302, 27.148421108693942));
        }

        [Fact]
        public void ACrescentGrownPastItsBumpsRadius_KeepsItsGrownArcFromEveryVertexAndBothWays()
        {
            // The bump, bulge 0.3 on a chord of 0.279, has a radius of 0.25372 and runs out. The big arc, radius 0.25771 about
            // (0.080344, 0.204670), grows to 3.03024: its start (2.38973, -1.75724) is the point the oracle found missing,
            // and a quarter of the way round it passes (0.19084, -2.82355).
            GeoPolygonArc2 source = Loop(
                new[] { P(0, 0), P(0.27675024081339111, 0.037815865676107036), P(-0.0832558703393812, 0.40379518812798743) },
                0.3, -1.0936498917144024, 0.0);
            var options = new OffsetOptions(OffsetJoin.Miter, 2.0);

            AssertOneLoopEveryWay(source, 2.7725246864243061, options, Thousandth, P(2.3897299297651293, -1.7572356575146937));

            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(source, 2.7725246864243061, options, Thousandth));
            Assert.True(DistanceTo(PiecesOf(one), 0.1908416050689537, -2.8235518664866595) <= 1E-9);
        }

        [Fact]
        public void TheNearlyFlatLoopGrownByEighty_ComesBackOneLoopFromEveryVertexAndBothWays()
        {
            // Its arc of bulge 0.5 runs out at 80; the point is the start of the first side moved 80 out.
            GeoPolygonArc2 source = Loop(
                new[]
                {
                    P(30.347361874528612, -40.471679110477979), P(-46.912102794524543, -103.96163988422022),
                    P(-79.116496453599481, -64.772858165511238), P(-68.140787607186866, -55.628199204337051),
                    P(-56.748510670114968, -46.990452316982541), P(-44.915392347073265, -38.889155115041518),
                    P(-34.953967124717877, -28.510237873310047), P(-23.478583547120635, -19.97362153445772),
                    P(-12.527056455067306, -10.799536355755784), P(-1.6199040605512947, -1.5714526340589621),
                },
                0.0, 0.0, -0.3, 0.0, -1.0, 0.0, 0.5, -0.001, -0.001, 0.0);

            AssertOneLoopEveryWay(source, 80.0, OffsetOptions.Default, Tenth, P(81.1393304935224, -102.2792508457205));
        }

        [Fact]
        public void AStarWhoseRunsMeetOutOfOrder_ComesBackOneLoopFromEveryVertexAndBothWays()
        {
            // No arc runs out here: round the spike at its fourth to sixth vertices the moved pieces cross each other in
            // turn, and the runs left meet in another order than they were cut in. The point is the start of the first
            // side moved 1 out.
            GeoPolygonArc2 source = Loop(
                new[]
                {
                    P(-10.321723325822894, 17.895212520097925), P(-12.99241113774551, 17.696037768522981),
                    P(-15.338646079245283, 15.872138844556581), P(-17.371615612290036, 13.089182083795558),
                    P(-15.526173935203436, 13.320297160738711), P(-17.279296698105437, 12.627511082052539),
                    P(-13.118362236585062, 11.1117758004299), P(-12.890286694365003, 11.000208441907493),
                    P(-12.771978187177895, 9.0342584247543485), P(-8.45682082730249, 12.642700428279863),
                    P(-11.022980483517122, 14.677339995816471),
                });

            AssertOneLoopEveryWay(source, 1.0, new OffsetOptions(OffsetJoin.Miter), Thousandth, P(-10.396094862855739, 18.892443122556237));
        }

        [Fact]
        public void AChainWhoseArcRunsOutOffsetByEighty_GivesTheSameRunTheOtherWay()
        {
            // The arc of bulge 0.09 (radius 46.67) runs out at 80. Nothing stands 80 from the chain on its left: the oracle
            // asks for no point of it. Run the other way with -80, it is the same nothing.
            GeoPolylineArc2 chain = Chain(
                new[]
                {
                    P(13.915731170573718, -6.9915071547593417), P(29.983907952449233, -2.565253138671717),
                    P(46.069494962411326, 1.797562410191377), P(62.110311754948889, 6.3235093067081465),
                    P(78.208720304327073, 10.63960640915163), P(94.301787983069261, 14.975164293217622),
                    P(110.32532027013544, 19.564091555550824), P(97.1175340629847, 67.68992358052779),
                    P(0.683269050802588, 41.224237654026091),
                },
                0.0, 0.0, 0.0, 0.0, 0.09, -0.6, 0.0, 0.09, 0.0);

            Assert.Empty(Offset2.Offset(chain, 80.0, OffsetOptions.Default, Thousandth));
            Assert.Empty(Offset2.Offset(Reversed(chain), -80.0, OffsetOptions.Default, Thousandth));
        }

        [Fact]
        public void ARectangleWhoseSideIsAHalfCircleBowedIn_GrownByTwo_GoesRoundBothEndsOfTheHalfCircle()
        {
            // The rectangle 20 by 10 whose left side is the half circle of radius 5 about (0, 5), bowed in to (5, 5). The
            // half circle leaves (0, 0) and (0, 10) back along the sides there, bending away from them, so grown by 2 the
            // offset goes round each end on a half turn of radius 2. The area is the box 20 by 14, the strip 2 by 10 and
            // two quarter circles on the right, two half circles on the left, less the half circle of radius 3 the bite
            // keeps: 280 + 20 + 2 pi + 4 pi - 4.5 pi = 300 + 1.5 pi.
            GeoPolygonArc2 rectangle = Loop(new[] { P(0, 0), P(0, 10), P(20, 10), P(20, 0) }, 1.0, 0.0, 0.0, 0.0);

            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(rectangle, 2.0, Round, Thousandth));

            Assert.Equal(300.0 + 1.5 * Math.PI, one.Area, 1E-9);
            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(rectangle), 2.0) <= 1E-9);
        }

        [Fact]
        public void TheSameRectangleGrownBySix_RoundsTheHalfCircleOffAboutItsTwoEnds()
        {
            // Grown by 6 the half circle's radius runs out, and the circles of 6 about (0, 0) and (0, 10) meet at
            // (-sqrt 11, 5). The area is the box 20 by 22, the strip 6 by 10 and two quarter circles of 6 on the right,
            // and on the left two half circles of 6 less the half of their overlap: 500 + 54 pi - 36 acos(5/6) + 5 sqrt 11.
            GeoPolygonArc2 rectangle = Loop(new[] { P(0, 0), P(0, 10), P(20, 10), P(20, 0) }, 1.0, 0.0, 0.0, 0.0);

            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(rectangle, 6.0, Round, Thousandth));

            Assert.Equal(500.0 + 54.0 * Math.PI - 36.0 * Math.Acos(5.0 / 6.0) + 5.0 * Math.Sqrt(11.0), one.Area, 1E-9);
            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(rectangle), 6.0) <= 1E-9);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(-Math.Sqrt(11.0), 5)) <= 1E-9);
        }

        [Fact]
        public void ASegmentOfACircleWithAShortSideShrunkByOne_ComesBackAsTheLensLeft()
        {
            // The loop along x from the origin to (20, 0), up 1, and back round the arc of bulge 0.5, radius sqrt 401 / 1.6
            // = 12.51562 about (10.375, -7). Shrunk by 1, the short side folds away and what is left is the lens between
            // y = 1 and the arc moved in to radius 11.51562, 8 above its centre: r^2 acos(8 / r) - 8 sqrt(r^2 - 64).
            GeoPolygonArc2 segment = Loop(new[] { P(0, 0), P(20, 0), P(20, 1) }, 0.0, 0.0, 0.5);
            double radius = Math.Sqrt(401.0) / 1.6 - 1.0;
            double half = Math.Sqrt(radius * radius - 64.0);

            GeoPolygonArc2 one = Assert.Single(Offset2.Offset(segment, -1.0, OffsetOptions.Default, Thousandth));

            Assert.Equal(radius * radius * Math.Acos(8.0 / radius) - 8.0 * half, one.Area, 1E-9);
            Assert.True(WorstOffDistance(PiecesOf(one), PiecesOf(segment), 1.0) <= 1E-9);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(10.375 - half, 1)) <= 1E-9);
            Assert.Contains(one.Vertices, v => v.DistanceTo(P(10.375 + half, 1)) <= 1E-9);
        }
    }
}
