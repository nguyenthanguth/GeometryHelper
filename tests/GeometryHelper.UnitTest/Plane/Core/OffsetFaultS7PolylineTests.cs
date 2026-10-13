using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A straight polyline's offset is the edge of the band it sweeps, as its remarks say, and nothing else: no stretch
    /// running on past the other leg of a sharp inside corner, no piece of the polyline itself, and no time quadratic in
    /// its length.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At an inside corner sharper than 90 degrees the moved edge ran on past where it crosses the other leg to its
    /// detour corner, and that stretch came back as extra pieces. The chain (10, 10), (0, 0), (10, 0) offset by 1 to the
    /// left, the inside of its 135 degree turn, gave the right chain from (10.70711, 9.29289) through (1 + sqrt 2, 1) to
    /// (10, 1), and two more: (1.41421, 0) to (0.70711, -0.70711), below the chain, and (0, 1) to (1, 1). A zigzag of 4 000
    /// vertices, 2 apart and 3 high, offset by 1 gave its chain and 3 998 such pieces, one at every valley, in 4.8 s on the
    /// audit's machine, 242 ms at 1 000 vertices: the runs were sorted by a comparator that walks the raw offset on every
    /// comparison.
    /// </para>
    /// <para>
    /// Where an offset edge lies on another part of the chain, the chain itself was matched as offset curve and came back:
    /// the step (0, 0), (10, 0), (10, 1), (-5, 1), 1 high, offset by 1 gave (0, 1) to (10, 1) and (10, 0) to (-5, 0), both
    /// on the chain. The band's edge is "minus the polyline itself" (its remarks), and so is the distance reading, so a
    /// piece along the chain is wrong whichever side of the step's height the rounding falls; the step a hair higher and a
    /// hair lower gives it too.
    /// </para>
    /// <para>
    /// K1: <c>GeoPolyline2</c> keeps the band reading. The hairpin offset into its inside keeps the band's edge to the end
    /// of the band of its last leg, (1, -0.6), which the curved path, reading distances, cuts at 0.55279: that difference
    /// stays.
    /// </para>
    /// </remarks>
    public class OffsetFaultS7PolylineTests
    {
        /// <summary>The vertices of a chain within the precision of the points given.</summary>
        private static void AssertChain(GeoPolyline2 actual, double precision, params GeoPoint2[] expected)
        {
            Assert.Equal(expected.Length, actual.VertexCount);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(0.0, actual.Vertices[i].DistanceTo(expected[i]), precision);
        }

        [Fact]
        public void TheInsideOfASharpCornerOffsetByOne_IsOneChainAndNoStretchPastTheOtherLeg()
        {
            var chain = new GeoPolyline2(P(10, 10), P(0, 0), P(10, 0));

            GeoPolyline2[] offset = Offset2.Offset(chain, 1.0, OffsetOptions.Default, Thousandth);

            GeoPolyline2 one = Assert.Single(offset);
            AssertChain(one, 1E-9, P(10 + Math.Sqrt(0.5), 10 - Math.Sqrt(0.5)), P(1 + Math.Sqrt(2.0), 1), P(10, 1));
        }

        [Fact]
        public void TheInsideOfAnObtuseCornerOffsetByOne_StaysOneChain()
        {
            // Guard: the leg from (-5, 5 sqrt 3) to the origin turns 60 degrees left into the x axis, an inside corner of
            // 120 degrees. The moved legs cross at (1 / sqrt 3, 1).
            var chain = new GeoPolyline2(P(-5, 5 * Math.Sqrt(3.0)), P(0, 0), P(10, 0));

            GeoPolyline2 one = Assert.Single(Offset2.Offset(chain, 1.0, OffsetOptions.Default, Thousandth));

            AssertChain(one, 1E-9, P(-5 + Math.Sqrt(0.75), 5 * Math.Sqrt(3.0) + 0.5), P(1 / Math.Sqrt(3.0), 1), P(10, 1));
        }

        [Fact]
        public void AZigzagOfFourThousandVertices_OffsetsAsOneChainInLinearTime()
        {
            // Every valley is an inside corner of 67.4 degrees on the left. The offset is one chain of 4 000 vertices: the
            // first moved by 1 along the first leg's normal (-3, 2) / sqrt 13, then every peak mitred at y = 3 + sqrt 13 / 2
            // = 4.80278 and every valley trimmed at y = sqrt 13 / 2 = 1.80278.
            GeoPolyline2 zigzag = Zigzag(4000);

            var watch = Stopwatch.StartNew();
            GeoPolyline2[] offset = Offset2.Offset(zigzag, 1.0, OffsetOptions.Default, Thousandth);
            watch.Stop();

            GeoPolyline2 one = Assert.Single(offset);
            double half = Math.Sqrt(13.0) / 2;
            Assert.Equal(4000, one.VertexCount);
            Assert.Equal(0.0, one.Vertices[0].DistanceTo(P(-3 / Math.Sqrt(13.0), 2 / Math.Sqrt(13.0))), 1E-9);
            Assert.Equal(0.0, one.Vertices[1].DistanceTo(P(2, 3 + half)), 1E-9);
            Assert.Equal(0.0, one.Vertices[2].DistanceTo(P(4, half)), 1E-9);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), "offsetting a zigzag of 4 000 vertices took " + watch.ElapsedMilliseconds + " ms");
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(1.0 - 1E-6)]
        [InlineData(1.0 + 1E-6)]
        public void AStepAsHighAsTheOffset_GivesNoPieceAlongTheChainItself(double height)
        {
            var chain = new GeoPolyline2(P(0, 0), P(10, 0), P(10, height), P(-5, height));

            GeoPolyline2[] offset = Offset2.Offset(chain, 1.0, OffsetOptions.Default, Thousandth);

            Piece[] source = PiecesOf(chain);
            foreach (GeoPolyline2 piece in offset)
            {
                // The middle of the longest edge of every piece of the answer stands off the chain.
                Piece longest = PiecesOf(piece).OrderByDescending(p => LengthOf(new[] { p })).First();
                longest.At(0.5, out double x, out double y);
                Assert.True(DistanceTo(source, x, y) > Thousandth.EqualPoint, "the piece " + string.Join(" ", piece.Vertices) + " lies along the chain");
            }
        }

        [Fact]
        public void TheInsideOfAHairpinOffsetAsAPolyline_KeepsTheBandsEdgeToTheEndOfTheLastLeg()
        {
            // Guard for K1: the band of the hairpin's last leg ends square at x = 1, and the first leg's band edge y = -0.6
            // runs to it: (0, -0.6) to (1, -0.6), 0.577 from the chain's end at its right end.
            var chain = new GeoPolyline2(Hairpin());

            GeoPolyline2 one = Assert.Single(Offset2.Offset(chain, -0.6, OffsetOptions.Default, Thousandth));

            AssertChain(one, 1E-9, P(0, -0.6), P(1, -0.6));
        }
    }
}
