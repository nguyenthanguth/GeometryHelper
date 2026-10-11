using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.MeetingInSpaceOracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two boxes 0.005 apart, five point tolerances, whose nearest features are two edges crossing at a sine of a tenth,
    /// read as touching.
    /// </summary>
    /// <remarks>
    /// The separating direction of two such edges is the cross product of their directions, a vector a tenth long. The
    /// separating axis test measures both boxes along it unscaled but adds the point tolerance as it is, so along that
    /// axis it allows ten point tolerances, 0.01.
    /// </remarks>
    public class OldFaultF5Tests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        private const double Gap = 0.005;

        // The box 10 by 10 by 10 from the origin; its edge along x at y = z = 10 is the farthest of it along n.
        private static GeoObb3 Cube() => new GeoObb3(Pt(5, 5, 5), 10, 10, 10);

        // n = (0, 1, 1) / sqrt 2 points out of the cube's top edge, m = (0, 1, -1) / sqrt 2 runs across it.
        private static readonly GeoVector3 N = new GeoVector3(0, 1, 1).Multiply(1 / Math.Sqrt(2));
        private static readonly GeoVector3 M = new GeoVector3(0, 1, -1).Multiply(1 / Math.Sqrt(2));

        // A bar 40 long and 6 by 6 across, along cos a x + sin a m with sin a = 0.1, square to n, so that x cross its
        // direction is a tenth of n. It is turned 45 degrees about its length, so that an edge, not a side, faces the cube:
        // that edge passes Gap beyond the cube's top edge at (5, 10, 10), and the bar lies beyond it along n.
        private static GeoObb3 Bar() => Bar(Gap);

        private static GeoObb3 Bar(double gap)
        {
            double sine = 0.1, cosine = Math.Sqrt(1 - sine * sine);
            GeoVector3 along = new GeoVector3(cosine, 0, 0).Add(M.Multiply(sine));
            GeoVector3 across = N.Add(along.CrossProduct(N)).Multiply(1 / Math.Sqrt(2));
            GeoPoint3 centre = Pt(5, 10, 10).Add(N.Multiply(gap + 3 * Math.Sqrt(2)));

            return new GeoObb3(centre, 40, 6, 6, along, across);
        }

        private static GeoPoint3[] Corners(GeoObb3 box)
            => (from i in new[] { -1, 1 }
                from j in new[] { -1, 1 }
                from k in new[] { -1, 1 }
                select box.Center.Add(box.AxisX.Multiply(i * box.ExtentX)).Add(box.AxisY.Multiply(j * box.ExtentY)).Add(box.AxisZ.Multiply(k * box.ExtentZ))).ToArray();

        private static double Along(GeoPoint3 p) => p.X * N.X + p.Y * N.Y + p.Z * N.Z;

        [Fact]
        public void TwoBoxesFiveThousandthsApartAcrossTwoSkewEdges_AreThatFarApart()
        {
            // The oracle: along n the cube reaches no farther than its top edge and the bar starts Gap beyond it, so n
            // separates them by Gap; the bar's nearest edge, its two corners lowest along n, passes Gap from the cube's
            // point (5, 10, 10), so no more separates them.
            double cubeReach = Corners(Cube()).Max(Along);
            GeoPoint3[] nearest = Corners(Bar()).OrderBy(Along).Take(2).ToArray();
            Assert.Equal(Gap, Along(nearest[0]) - cubeReach, 9);
            Assert.Equal(Gap, ToSegment(Pt(5, 10, 10), nearest[0], nearest[1]), 9);

            Assert.Equal(Gap, Cube().DistanceTo(Bar(), Within), 9);
            Assert.Equal(Gap, Bar().DistanceTo(Cube(), Within), 9);
            Assert.False(Cube().CollidesWith(Bar(), Within));
        }

        [Fact]
        public void TwoBoxesNineTenThousandthsApartAcrossTwoSkewEdges_StillTouch()
        {
            // The same pair 0.0009 apart, within the point tolerance: the edge axis allows the tolerance itself.
            Assert.True(Cube().CollidesWith(Bar(0.0009), Within));
            Assert.Equal(0.0, Cube().DistanceTo(Bar(0.0009), Within));
        }

        [Fact]
        public void TwoBoxesElevenTenThousandthsApartAcrossTwoSkewEdges_DoNotTouch()
        {
            // Just past the point tolerance, 0.0011 apart: apart, and measured as their bodies are.
            Assert.False(Cube().CollidesWith(Bar(0.0011), Within));
            Assert.Equal(0.0011, Cube().DistanceTo(Bar(0.0011), Within), 9);
        }
    }
}
