using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Walls of a body's openings a fraction of the tolerance apart, cut near the origin, leave cells between their planes
    /// thinner than the global tolerance. A face is laid out in its plane within that tolerance however fine the work, and
    /// the face of such a cell, a strip, was nothing there: it shared nothing with the face against it, and nothing was
    /// taken from it. The strip and the part of that face against it stayed in the material as a sheet bounding none; the
    /// volume held, the area did not.
    /// </summary>
    public class Boolean3StripsGr2cTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Hundredth = new Tolerance(0.01, 1E-5, 0.017453292519943295, 0.01);

        private static readonly Tolerance Millionth = new Tolerance(1E-6, 1E-5, 0.017453292519943295, 1E-6);

        private static Tolerance Within(double tolerance) => tolerance == 1E-2 ? Hundredth : tolerance == 1E-3 ? Thousandth : Millionth;

        private static GeoPoint3 At(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => GeoSolid3.Extrude(new GeoPolygon3(At(x0, y0, z0), At(x1, y0, z0), At(x1, y1, z0), At(x0, y1, z0)), new GeoVector3(0, 0, z1 - z0), Thousandth);

        /// <summary>
        /// The L 100 by 80 by 50 of <see cref="Boolean3FarFrameGr2bTests"/> at the origin, its openings given down first: one
        /// 15.5 by 6.528 down from its top 37 deep, and one 13 by 8.8271 up from its floor 22 deep, their far walls 0.0009
        /// apart.
        /// </summary>
        private static GeoSolid3 LDownFirst()
            => GeoSolid3.Extrude(
                    new GeoPolygon3(At(0, 0, 0), At(100, 0, 0), At(100, 20, 0), At(20, 20, 0), At(20, 80, 0), At(0, 80, 0)),
                    new GeoVector3(0, 0, 50),
                    Thousandth)
                .WithOpenings(new[] { Box(75, 6.9, 13, 90.5, 13.428, 57), Box(35, 4.6, -7, 48, 13.4271, 22) });

        /// <summary>The L's faces, 24 400, and the walls of its openings.</summary>
        private const double LSurface = 24_400 + 2 * (13 + 8.8271) * 22 + 2 * (15.5 + 6.528) * 37;

        private const double LHeld = 160_000 - 13 * 8.8271 * 22 - 15.5 * 6.528 * 37;

        /// <summary>
        /// The plate 200 by 100 by 3 of <see cref="Boolean3FarFrameGr2bTests"/> at the origin: two openings 50 by 70 up from
        /// its floor, 15 apart, 1.5 and 1.50015 deep.
        /// </summary>
        private static GeoSolid3 Plate()
            => GeoSolid3.Extrude(new GeoPolygon3(At(0, 0, 0), At(200, 0, 0), At(200, 100, 0), At(0, 100, 0)), new GeoVector3(0, 0, 3), Thousandth)
                .WithOpenings(new[] { Box(10, 15, -7, 60, 85, 1.5), Box(75, 15, -7, 125, 85, 1.50015) });

        /// <summary>The plate's faces, 41 800, and the walls of its openings, 360 and 360.036.</summary>
        private const double PlateSurface = 41_800 + 2 * 120 * 1.5 + 2 * 120 * 1.50015;

        private const double PlateHeld = 60_000 - 50 * 70 * 1.5 - 50 * 70 * 1.50015;

        [Theory]
        [InlineData(1E-2)]
        [InlineData(1E-3)]
        [InlineData(1E-6)]
        public void TheLWithItsOpeningsGivenDownFirst_KeepsNoSheetBetweenTheFarWalls(double tolerance)
        {
            // It kept a sheet 0.0009 by 13 at x = 75, under the opening down from the top: 0.0234 too much.
            Assert.True(LDownFirst().TryCutOpenings(out GeoSolid3 material, Within(tolerance)));

            Assert.Equal(LSurface, material.GetSurfaceArea(Within(tolerance)), 1E-9);
            Assert.Equal(LHeld, material.GetVolume(Within(tolerance)), 1E-9);
            Assert.True(material.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [InlineData(1E-2)]
        [InlineData(1E-3)]
        [InlineData(1E-6)]
        public void ThePlateWithCeilingsApart_KeepsNoSheetBesideTheFirstOpening(double tolerance)
        {
            // It kept a sheet 0.00015 by 100 at x = 60, beside the opening 1.5 deep: 0.03 too much.
            Assert.True(Plate().TryCutOpenings(out GeoSolid3 material, Within(tolerance)));

            Assert.Equal(PlateSurface, material.GetSurfaceArea(Within(tolerance)), 1E-9);
            Assert.Equal(PlateHeld, material.GetVolume(Within(tolerance)), 1E-9);
            Assert.True(material.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-6)]
        public void TheLTurnedAboutInSpace_KeepsNoSheetBetweenTheFarWalls(double tolerance)
        {
            // Turned 0.7 radians about a slanted axis, its corners off the planes by the rounding.
            GeoSolid3 turned = LDownFirst().TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(30, 20, 10), new GeoVector3(0.3, -0.5, 0.81), 0.7));

            Assert.True(turned.TryCutOpenings(out GeoSolid3 material, Within(tolerance)));

            Assert.Equal(LSurface, material.GetSurfaceArea(Within(tolerance)), 1E-6);
            Assert.Equal(LHeld, material.GetVolume(Within(tolerance)), 1E-6);
        }

        [Fact]
        public void AStripNarrowerThanTheGlobalTolerance_IsTakenFromTheFaceAgainstIt()
        {
            // Two cells side by side at x = 75, one cut at y = 13.4271 and the other not: the face of the one across the
            // other's two, the strip of them 0.0009 wide. Taken from each other, nothing is left.
            var face = new GeoFace3(new GeoPolygon3(new[] { At(75, 6.9, 0), At(75, 6.9, 13), At(75, 13.428, 13), At(75, 13.428, 0) }, Millionth), new GeoPolygon3[0], Millionth);
            var wide = new GeoFace3(new GeoPolygon3(new[] { At(75, 6.9, 0), At(75, 13.4271, 0), At(75, 13.4271, 13), At(75, 6.9, 13) }, Millionth), new GeoPolygon3[0], Millionth);
            var strip = new GeoFace3(new GeoPolygon3(new[] { At(75, 13.4271, 0), At(75, 13.428, 0), At(75, 13.428, 13), At(75, 13.4271, 13) }, Millionth), new GeoPolygon3[0], Millionth);

            Assert.Empty(Boolean3.CancelBackToBack(new List<GeoFace3> { face, wide, strip }, Millionth));
        }

        [Fact]
        public void AStripWithNothingAgainstIt_StaysAsItCame()
        {
            // The strip alone, and the face it does not lie against, 0.5 away.
            var strip = new GeoFace3(new GeoPolygon3(new[] { At(75, 13.4271, 0), At(75, 13.428, 0), At(75, 13.428, 13), At(75, 13.4271, 13) }, Millionth), new GeoPolygon3[0], Millionth);
            var apart = new GeoFace3(new GeoPolygon3(new[] { At(75, 14, 0), At(75, 14, 13), At(75, 20, 13), At(75, 20, 0) }, Millionth), new GeoPolygon3[0], Millionth);

            List<GeoFace3> left = Boolean3.CancelBackToBack(new List<GeoFace3> { strip, apart }, Millionth);

            Assert.Equal(2, left.Count);
            Assert.Same(strip, left[0]);
            Assert.Same(apart, left[1]);
        }
    }
}
