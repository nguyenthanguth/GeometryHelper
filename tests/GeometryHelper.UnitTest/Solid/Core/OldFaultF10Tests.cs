using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// The prepared body's and the index's ray members read a ray starting within the planar tolerance of a face as the
    /// body's own members do: dipping, it crosses where it reaches height nought; rising, it touches at its origin; level,
    /// it names no place on that face. They used to keep the reading the counting casts keep, so a bar dipping into a
    /// plate from the band entered it at its first point rather than where it crosses the top.
    /// </summary>
    /// <remarks>
    /// The box runs from (0, 0, -10) to (10, 10, 0), its top in z = 0; each ray starts at (5, 5, 0.0004), 0.4 of the
    /// planar tolerance over the top.
    /// </remarks>
    public class OldFaultF10Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPoint3 Pt(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(Pt(x0, y0, z0), Pt(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 Block() => Box(0, 0, -10, 10, 10, 0);

        private static readonly GeoPoint3 Start = Pt(5, 5, 0.0004);

        // The body's own crossings, in order along the ray, and the prepared body's, place by place.
        private static void AssertSameCrossings(GeoRay3 ray, GeoSolid3 body)
        {
            GeoPoint3[] expected = Intersection3.GetIntersections(ray, body, Tol);
            GeoPoint3[] prepared = new GeoPreparedSolid3(body, Tol).GetIntersections(ray, Tol);

            Assert.Equal(expected.Length, prepared.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i].DistanceTo(prepared[i]) <= Tol.EqualPoint, $"{expected[i]} against {prepared[i]}");
            }
        }

        [Fact]
        public void ARayDippingFromTheBand_EntersThePreparedBodyAndTheIndexWhereItCrossesTheTop()
        {
            var ray = new GeoRay3(Start, new GeoVector3(1, 0, -0.01));
            var prepared = new GeoPreparedSolid3(Block(), Tol);

            Assert.True(prepared.GetIntersections(ray, Tol)[0].DistanceTo(Pt(5.04, 5, 0)) <= 1E-9);
            Assert.True(prepared.CollidesWith(ray, Tol));
            Assert.Contains(new GeoBvh3(Block().Triangulate(Tol)).GetIntersections(ray, Tol), p => p.DistanceTo(Pt(5.04, 5, 0)) <= 1E-9);
            AssertSameCrossings(ray, Block());
        }

        [Fact]
        public void ARayRisingFromTheBand_TouchesThePreparedBodyAtItsOrigin()
        {
            var ray = new GeoRay3(Start, new GeoVector3(1, 0, 0.01));

            GeoPoint3 touch = Assert.Single(new GeoPreparedSolid3(Block(), Tol).GetIntersections(ray, Tol));
            Assert.True(touch.DistanceTo(Start) <= 1E-9);
            AssertSameCrossings(ray, Block());
        }

        [Fact]
        public void ARayLevelInTheBand_NamesNoPlaceOnTheTop()
        {
            // It reaches the side x = 10 at (10, 5, 0.0004), on its rim within the tolerance; the top names nothing.
            var ray = new GeoRay3(Start, new GeoVector3(1, 0, 0));
            GeoPoint3[] crossings = new GeoPreparedSolid3(Block(), Tol).GetIntersections(ray, Tol);

            Assert.DoesNotContain(crossings, p => p.X < 10 - Tol.EqualPoint);
            AssertSameCrossings(ray, Block());
        }

        [Fact]
        public void ARayDippingFromTheBandIntoABodyWithAnOpening_CrossesWhereTheBodyDoes()
        {
            // The block 20 by 10 by 10 with an opening 4 wide right through it from x = 8 to 12: the ray enters the top at
            // (2.04, 5, 0), leaves through the opening's wall at x = 8 and enters again at x = 12.
            GeoSolid3 pierced = Box(0, 0, -10, 20, 10, 0).WithOpenings(new[] { Box(8, -5, -15, 12, 15, 5) });
            var ray = new GeoRay3(Pt(2, 5, 0.0004), new GeoVector3(1, 0, -0.01));

            Assert.True(new GeoPreparedSolid3(pierced, Tol).GetIntersections(ray, Tol)[0].DistanceTo(Pt(2.04, 5, 0)) <= 1E-9);
            AssertSameCrossings(ray, pierced);
        }

        [Fact]
        public void ABarDippingIntoAPlateFromTheBand_IsInsideFromWhereItCrossesTheTop()
        {
            // A plate 1000 by 100 by 10 with its top in z = 10, and a bar along x from 0.0004 over the top dipping at half
            // a degree, 400 long: it crosses the top 0.0004 / sin 0.5 degrees, about 0.0458, along, and stays inside after.
            var plate = Box(0, 0, 0, 1000, 100, 10);
            double angle = 0.5 * Math.PI / 180.0;
            GeoPoint3 from = Pt(10, 50, 10.0004);
            GeoPoint3 to = from.Add(new GeoVector3(Math.Cos(angle), 0, -Math.Sin(angle)).Multiply(400));
            var bar = new ClashBar(new GeoPolyline3(from, to), 2.0);

            ClashResult clash = Clash3.Find(new[] { bar }, new[] { plate }, ClashOptions.Default, Tol).Single();

            Assert.Equal(400 - 0.0004 / Math.Sin(angle), clash.LengthInside, 6);
        }
    }
}
