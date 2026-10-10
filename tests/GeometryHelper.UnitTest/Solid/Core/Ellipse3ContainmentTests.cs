using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The ellipse in space read as a disc, case by case as <see cref="GeoCircle3"/> reads one:
    /// <list type="bullet">
    /// <item>Locate (Containment3.cs:626-642): a point further than EqualPlanar off the plane is outside (628-631); one
    /// within EqualPoint of the rim is on it (636-639); else inside or outside (641).</item>
    /// <item>Contains (651-654) is Locate not outside, so a point a hair outside the rim is held; IsPointOn (664-667) is
    /// Locate on the rim, so it too is refused off the plane.</item>
    /// <item>DistanceTo (Distance3.cs:276-279) is the distance to the closest point of the filled disc, which
    /// (Projection3.cs:749-757) is the foot of the point when the foot is inside, and the rim point otherwise.</item>
    /// </list>
    /// The tolerance here sets the planar band to 0.1 and the point band to a thousandth, so that a case reading the
    /// wrong field gives the wrong answer; every offset sits at least twice clear of the band it tests.
    /// </summary>
    public class Ellipse3ContainmentTests
    {
        private static readonly Tolerance Bands = new Tolerance(1E-3, 1E-5, Tolerance.Default.EqualAngleRad, 0.1);

        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-12);

        [Fact]
        public void APointOfThePlaneInsideTheRimIsInside()
        {
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();

            foreach (GeoPoint3 point in new[] { specimen.Center, specimen.At(250, 30, 0), specimen.At(-20, -95, 0) })
            {
                Assert.Equal(PointLocation.Inside, ellipse.Locate(point, Bands));
                Assert.True(ellipse.Contains(point, Bands));
                Assert.False(ellipse.IsPointOn(point, Bands));
            }
        }

        [Fact]
        public void APointHoveringAboveTheDiscIsOutsideIt()
        {
            // 0.2 above, twice the planar band, over the middle and over the rim: outside, not held, not on the rim. The
            // circle answers so (Containment3.cs:628-631).
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            var circle = new GeoCircle3(specimen.Center, specimen.N, 300);

            foreach (GeoPoint3 point in new[] { specimen.At(0, 0, 0.2), specimen.At(100, 20, -0.2), specimen.OffRim(1.0, 0.0, 0.2) })
            {
                Assert.Equal(PointLocation.OutSide, ellipse.Locate(point, Bands));
                Assert.False(ellipse.Contains(point, Bands));
                Assert.False(ellipse.IsPointOn(point, Bands));
            }

            Assert.Equal(PointLocation.OutSide, circle.Locate(specimen.At(0, 0, 0.2), Bands));
        }

        [Fact]
        public void APointWithinThePlanarBandOfThePlaneIsReadInIt()
        {
            // 0.05 off the plane, half the planar band and fifty times the point band: the plane test reads EqualPlanar.
            // Over the middle it is inside, over the rim on it, as the circle's is.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            var circle = new GeoCircle3(specimen.Center, specimen.N, 300);

            Assert.Equal(PointLocation.Inside, ellipse.Locate(specimen.At(100, 20, 0.05), Bands));
            Assert.Equal(PointLocation.Inside, ellipse.Locate(specimen.At(100, 20, -0.05), Bands));
            Assert.Equal(PointLocation.OnSide, ellipse.Locate(specimen.OffRim(1.0, 0.0, 0.05), Bands));
            Assert.True(ellipse.IsPointOn(specimen.OffRim(1.0, 0.0, -0.05), Bands));

            Assert.Equal(PointLocation.Inside, circle.Locate(specimen.At(100, 20, 0.05), Bands));
            Assert.Equal(PointLocation.OnSide, circle.Locate(specimen.At(300, 0, 0.05), Bands));
        }

        [Theory]
        [InlineData(-0.05, PointLocation.Inside)]
        [InlineData(-0.002, PointLocation.Inside)]
        [InlineData(-0.0005, PointLocation.OnSide)]
        [InlineData(0.0005, PointLocation.OnSide)]
        [InlineData(0.002, PointLocation.OutSide)]
        [InlineData(0.05, PointLocation.OutSide)]
        public void TheRimIsABandOfThePointToleranceEitherSide(double offset, PointLocation expected)
        {
            // In the plane, the rim reads the point band, a thousandth, as a distance, wherever round the rim: at the end
            // of the major axis, where it bends tightest (radius 33.3), at the end of the minor, and between. 0.05 out is
            // within the planar band and still outside: the rim does not read EqualPlanar. A point on the band is held,
            // as the circle holds one (Containment3.cs:636-639, 651-654; 2D A4).
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();

            foreach (double t in new[] { 0.0, 0.7, Math.PI / 2, 2.4, 4.0 })
            {
                GeoPoint3 point = specimen.OffRim(t, offset, 0.0);

                Assert.Equal(expected, ellipse.Locate(point, Bands));
                Assert.Equal(expected != PointLocation.OutSide, ellipse.Contains(point, Bands));
                Assert.Equal(expected == PointLocation.OnSide, ellipse.IsPointOn(point, Bands));
            }
        }

        [Fact]
        public void TheCircleReadsTheRimBandsTheSameWay()
        {
            // The pin for the theory above: the circle of 300 about the same centre, a thousandth band, a planar 0.1.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            var circle = new GeoCircle3(specimen.Center, specimen.N, 300);
            GeoEllipse3 round = Ellipse3Oracle.Tilted(300, 300).Build();

            foreach (double offset in new[] { -0.05, -0.002, -0.0005, 0.0005, 0.002, 0.05 })
            {
                GeoPoint3 point = specimen.At(300 + offset, 0, 0);

                Assert.Equal(circle.Locate(point, Bands), round.Locate(point, Bands));
                Assert.Equal(circle.Contains(point, Bands), round.Contains(point, Bands));
                Assert.Equal(circle.IsPointOn(point, Bands), round.IsPointOn(point, Bands));
            }
        }

        [Fact]
        public void TheDistanceIsStraightDownInsideAndOutToTheRimBeyondIt()
        {
            // Above the inside, the height: 40 over (100, 20), 70 under the centre. In the plane inside, nought. Beyond the
            // rim, the gap to the rim, √(h² + d²) with h the height and d the gap of the foot to the rim in the plane, d
            // from a dense sample.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();

            Assert.Equal(40.0, ellipse.DistanceTo(specimen.At(100, 20, 40)), 1E-9);
            Assert.Equal(70.0, ellipse.DistanceTo(specimen.At(0, 0, -70)), 1E-9);
            Assert.Equal(0.0, ellipse.DistanceTo(specimen.At(-150, 60, 0)), 1E-9);

            foreach (GeoPoint3 point in new[] { specimen.At(400, 50, 0), specimen.At(400, 50, 30), specimen.At(-100, -150, -45), specimen.At(0, 500, 12) })
            {
                specimen.Read(point, out double u, out double v, out double h);
                double d = Plane.Ellipse2Oracle.ClosestDistance(300, 100, u, v);

                Assert.Equal(Math.Sqrt(h * h + d * d), ellipse.DistanceTo(point), 1E-9);
            }
        }

        [Fact]
        public void TheClosestPointOfTheSurfaceIsTheFootInsideAndTheRimPointBeyond()
        {
            // As ProjectToDisc (Projection3.cs:749-757): above the inside, the foot, the centre included; beyond the rim,
            // the nearest rim point.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();

            Assert.True(ellipse.GetClosestPointOnSurface(specimen.At(100, 20, 40)).IsEqualTo(specimen.At(100, 20, 0), Tight));
            Assert.True(ellipse.GetClosestPointOnSurface(specimen.At(0, 0, -70)).IsEqualTo(specimen.Center, Tight));
            Assert.True(ellipse.GetClosestPointOnSurface(specimen.At(-150, 60, 0)).IsEqualTo(specimen.At(-150, 60, 0), Tight));

            foreach (GeoPoint3 point in new[] { specimen.At(400, 50, 0), specimen.At(400, 50, 30), specimen.At(-100, -150, -45) })
            {
                GeoPoint3 expected = Ellipse3Oracle.ClosestOnRim(specimen, point, out double distance);
                GeoPoint3 closest = ellipse.GetClosestPointOnSurface(point);

                Assert.True(closest.IsEqualTo(expected, Tight), $"{closest} against {expected}");
                Assert.Equal(distance, closest.DistanceTo(point), 1E-9);
                Assert.Equal(distance, ellipse.DistanceTo(point), 1E-9);
            }
        }
    }
}
