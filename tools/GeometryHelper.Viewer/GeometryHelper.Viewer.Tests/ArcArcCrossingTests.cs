using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// Two arcs of huge radius cross where they should: measured from their starts, as an arc and a line are, and not from centres a million times
    /// the size of the arcs away.
    /// </summary>
    public class ArcArcCrossingTests
    {
        private static ExactGeometry Exact(SceneWriter writer) => ExactGeometry.Build(writer.Scene, Primitives.Build(writer.Scene));

        /// <summary>
        /// Where two arcs on chords of length L cross, one along X and one along Y, both through the origin with their middles bowed out by their bulges, worked
        /// out in the small numbers about the middle, where nothing is lost: the first bows towards -Y and the second towards +X.
        /// </summary>
        private static Vec3 Expected(double length, double bulgeAlongX, double bulgeAlongY)
        {
            double half = length * 0.5;
            double r1 = length * (1.0 + bulgeAlongX * bulgeAlongX) / (4.0 * bulgeAlongX);
            double r2 = length * (1.0 + bulgeAlongY * bulgeAlongY) / (4.0 * bulgeAlongY);
            double x = 0.0;
            double y = 0.0;

            for (int i = 0; i < 40; i++)
            {
                y = -bulgeAlongX * half + x * x / (r1 + Math.Sqrt(r1 * r1 - x * x));
                x = bulgeAlongY * half - y * y / (r2 + Math.Sqrt(r2 * r2 - y * y));
            }

            return new Vec3(x, y, 0.0);
        }

        [Theory]
        [InlineData(1E-6, 1E-7)]
        [InlineData(1E-7, 1E-8)]
        [InlineData(1E-8, 3E-9)]
        [InlineData(3E-9, 2E-9)]
        [InlineData(0.05, 0.02)]
        [InlineData(1E-4, 1E-5)]
        public void TwoArcsOnChordsOf60000_CrossWhereTheyShouldToABillionth(double bulgeAlongX, double bulgeAlongY)
        {
            // Radii from 7.5E12 down to 3E5; the centres are as far away as the radii, 100 000 from the origin for the arcs themselves.
            double length = 60000.0;
            var centre = new GeoPoint2(100000.0, 100000.0);
            ExactGeometry g = Exact(new SceneWriter()
                .Add(new GeoPolylineArc2(new[] { new GeoPoint2(centre.X - 30000.0, centre.Y), new GeoPoint2(centre.X + 30000.0, centre.Y) }, new[] { bulgeAlongX, 0.0 }))
                .Add(new GeoPolylineArc2(new[] { new GeoPoint2(centre.X, centre.Y - 30000.0), new GeoPoint2(centre.X, centre.Y + 30000.0) }, new[] { bulgeAlongY, 0.0 })));

            Vec3 hit = Assert.Single(CurveIntersections.Find(g.Curves[0], g.Curves[1], 1E-3));
            Vec3 expected = new Vec3(centre.X, centre.Y, 0.0) + Expected(length, bulgeAlongX, bulgeAlongY);

            Assert.True((hit - expected).Length < 1E-9, "crossing " + (hit - expected).Length + " from where it is");
        }

        [Fact]
        public void TwoArcsThatMeetAtAShallowAngle_StillCross_AndTheCrossingIsOnBoth()
        {
            // Two arcs of radius 1E12 on chords of 2000, the second chord lifted 0.01 at its far end and sunk 0.01 at its near end so that they cross halfway, at an angle of 1E-5 radian:
            // the arcs are never further apart than 0.01, ten times the tolerance, and the crossing found lies on both to the rounding of its coordinates.
            double r = 1E12;
            double chord = 2000.0;
            double bulge = chord / (4.0 * r);
            ExactGeometry g = Exact(new SceneWriter()
                .Add(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(chord, 0) }, new[] { bulge, 0.0 }))
                .Add(new GeoPolylineArc2(new[] { new GeoPoint2(0, -0.01), new GeoPoint2(chord, 0.01) }, new[] { bulge, 0.0 })));

            Vec3[] hits = CurveIntersections.Find(g.Curves[0], g.Curves[1], 1E-3).ToArray();

            Vec3 hit = Assert.Single(hits);
            Assert.True(Math.Abs(hit.X - 1000.0) < 1.0, "crossing at x = " + hit.X);
            Assert.True((g.Curves[0].ClosestPointTo(hit) - hit).Length < 1E-6);
            Assert.True((g.Curves[1].ClosestPointTo(hit) - hit).Length < 1E-6);
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-4)]
        [InlineData(1E-5)]
        [InlineData(1E-6)]
        [InlineData(1E-7)]
        public void AnArcOnAChordOf60000AndACircleOfRadius1DippingATenthBelowIt_CrossTwiceAtPlusAndMinus0Point4359(double bulge)
        {
            // The arc's radius is 1.5E7 to 1.5E11; its centre is as far off, and the two crossings, 0.87 apart, are found from the start of the arc and the circle's own centre.
            double sagitta = bulge * 30000.0;
            ExactGeometry g = Exact(new SceneWriter()
                .Add(new GeoPolylineArc2(new[] { new GeoPoint2(-30000, 0), new GeoPoint2(30000, 0) }, new[] { bulge, 0.0 }))
                .Add(new GeoCircle2(new GeoPoint2(0, -sagitta + 0.9), 1.0)));

            Vec3[] hits = CurveIntersections.Find(g.Curves[0], g.Curves[1], 1E-3).OrderBy(h => h.X).ToArray();

            Assert.Equal(2, hits.Length);
            Assert.Equal(-0.4359, hits[0].X, 4);
            Assert.Equal(0.4359, hits[1].X, 4);
        }
    }
}
