using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two arcs, and two circles, cross where they cross whichever is asked first, though one of them is nearly flat.
    /// </summary>
    /// <remarks>
    /// An edge from (0, 0) to (100, 0) with a bulge of 1E-7 is an arc of radius 2.5E8, bowed 5E-6 below its chord. The
    /// circles were crossed in the frame of the first one asked, so asked from that edge the crossing with an ordinary
    /// arc came out too far off to lie on both, and was lost: no collision, and a distance of 8.357 where the two cross.
    /// Asked from the ordinary arc, the same pair crossed.
    /// </remarks>
    public class CollidesB11NearFlatArcTests
    {
        /// <summary>
        /// The arc about (50, 30) of radius 40 from 3.4 radians sweeping 1.6, counter-clockwise: it passes below its
        /// centre from (11.36, 19.79) to (61.35, -8.36), crossing the X axis once, at x = 50 - sqrt(700) = 23.5425; its
        /// circle's other crossing, at 76.4575, lies past its end.
        /// </summary>
        private static GeoArc2 ArcAcrossTheAxis() => new GeoArc2(P(50, 30), 40, 3.4, 5.0);

        /// <summary>The edge from (0, 0) to (100, 0) bowed by the bulge given: below its chord for a positive bulge.</summary>
        private static GeoEdge2 NearlyFlat(double bulge) => new GeoEdge2(P(0, 0), P(100, 0), bulge);

        /// <summary>
        /// Where <see cref="NearlyFlat"/> crosses <see cref="ArcAcrossTheAxis"/>, worked by hand in 50 digits: the edge's
        /// circle has radius R = 100 (1 + b^2) / 4b about (50, R - s), with sagitta s = 50 b, and subtracting the two
        /// circles' equations gives y = (2 R s - s^2 - 700) / (60 - 2 R + 2 s), then x = 50 - sqrt(1600 - (y - 30)^2).
        /// </summary>
        public static TheoryData<double, double, double, double> Crossings => new TheoryData<double, double, double, double>
        {
            // bulge, x, y, how close the crossing is asked to be: the arc's centre, 2.5E10 off for a bulge of 1E-9, is
            // itself held only to 4E-6, so that one is asked within 1E-5; the others within 1E-7.
            { 1E-6, 23.542527709622146, -3.6000043200087842E-05, 1E-7 },
            { 1E-7, 23.542490971371453, -3.6000004320000878E-06, 1E-7 },
            { 1E-9, 23.542486930174256, -3.6000000043200002E-08, 1E-5 },
        };

        [Fact]
        public void AnEdgeBowedByATenMillionthCrossingAnArc_CollidesWithItAskedFromEitherSide()
        {
            // The repro: asked from the edge, false and 8.357 on 2755f30; asked from the arc, true and 0.
            GeoEdge2 edge = NearlyFlat(1E-7);
            GeoArc2 arc = ArcAcrossTheAxis();

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.True(edge.CollidesWith(arc, tolerance));
                Assert.True(arc.CollidesWith(edge, tolerance));
                Assert.Equal(0.0, edge.DistanceTo(arc, tolerance));
                Assert.Equal(0.0, arc.DistanceTo(edge, tolerance));
            }
        }

        [Theory]
        [MemberData(nameof(Crossings))]
        public void ANearlyFlatArcCrossingAnOrdinaryArc_CrossesAtTheSamePointWhicheverIsAskedFirst(double bulge, double x, double y, double within)
        {
            // The arcs themselves, through the static form both ways round, and the edge's own forms.
            GeoArc2 flat = NearlyFlat(bulge).ToArc();
            GeoArc2 arc = ArcAcrossTheAxis();

            Assert.True(Collision2.CollidesWith(flat, arc, Thousandth));
            Assert.True(Collision2.CollidesWith(arc, flat, Thousandth));

            GeoPoint2 fromFlat = Assert.Single(flat.GetIntersections(arc, Thousandth));
            GeoPoint2 fromArc = Assert.Single(arc.GetIntersections(flat, Thousandth));

            Assert.Equal(x, fromFlat.X, within);
            Assert.Equal(y, fromFlat.Y, within);
            Assert.Equal(x, fromArc.X, within);
            Assert.Equal(y, fromArc.Y, within);
        }

        [Fact]
        public void AnEdgeBowedByATenMillionthCrossingABowedEdge_CollidesWithItAskedFromEitherSide()
        {
            // The edge from (20, -10) to (30, 10) bowed by 0.5 crosses the nearly flat one once, near (30.96, 0): on
            // 2755f30 asked from the flat edge, false at every tolerance and a distance of 10.0.
            GeoEdge2 flat = NearlyFlat(1E-7);
            var bowed = new GeoEdge2(P(20, -10), P(30, 10), 0.5);

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.True(flat.CollidesWith(bowed, tolerance));
                Assert.True(bowed.CollidesWith(flat, tolerance));
                Assert.Equal(0.0, flat.DistanceTo(bowed, tolerance));
                Assert.Equal(0.0, bowed.DistanceTo(flat, tolerance));
            }
        }

        [Fact]
        public void AHugeCircleCrossingAnOrdinaryOne_CrossesAtTheSamePointsWhicheverIsAskedFirst()
        {
            // The circle of radius 1.25E8 about (50, -1.25E8) has its top at (50, 0) and stands 700 / 2.5E8 = 2.8E-6
            // below the X axis at x = 50 -+ 26.46. Subtracting the equations: y = -700 / (2.5E8 + 60), and the crossings
            // are 50 -+ sqrt(1600 - (y - 30)^2) = 23.542490064255244 and 76.457509935744756. On 2755f30, asked from the
            // huge circle, they came out 3.2E-6 off.
            var huge = new GeoCircle2(P(50, -1.25E8), 1.25E8);
            var circle = new GeoCircle2(P(50, 30), 40);
            const double y = -2.7999993280001613E-06;

            foreach (GeoPoint2[] crossings in new[] { huge.GetIntersections(circle, Thousandth), circle.GetIntersections(huge, Thousandth) })
            {
                Assert.Equal(2, crossings.Length);
                Array.Sort(crossings, (a, b) => a.X.CompareTo(b.X));
                Assert.Equal(23.542490064255244, crossings[0].X, 1E-7);
                Assert.Equal(76.457509935744756, crossings[1].X, 1E-7);
                Assert.Equal(y, crossings[0].Y, 1E-7);
                Assert.Equal(y, crossings[1].Y, 1E-7);
            }
        }

        [Fact]
        public void TwoOrdinaryArcsThatCross_KeepTheirCrossingAskedFromEitherSide()
        {
            // A guard. The upper half of the circle of radius 50 about the origin and the left half of the circle of
            // radius 30 about (60, 0) cross where x = (2500 - 900 + 3600) / 120 = 130 / 3 and y = sqrt(2500 - x^2).
            var big = new GeoArc2(P(0, 0), 50, 0.0, Math.PI);
            var small = new GeoArc2(P(60, 0), 30, Math.PI / 2, 3 * Math.PI / 2);

            GeoPoint2 one = Assert.Single(big.GetIntersections(small, Thousandth));
            GeoPoint2 other = Assert.Single(small.GetIntersections(big, Thousandth));

            Assert.Equal(43.333333333333336, one.X, 1E-12);
            Assert.Equal(24.944382578492942, one.Y, 1E-12);
            Assert.Equal(43.333333333333336, other.X, 1E-12);
            Assert.Equal(24.944382578492942, other.Y, 1E-12);
            Assert.True(big.CollidesWith(small, TenThousandth));
            Assert.True(small.CollidesWith(big, TenThousandth));
        }
    }
}
