using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The measures of a body with openings, its volume, area and centroid, read its material as
    /// <see cref="GeoSolid3.TryCutOpenings(out GeoSolid3, Tolerance)"/> cuts it at a tolerance at or a little above the rounding
    /// of the coordinates, or below it, nought among them; and its surface and the projection onto it are cut within the
    /// rounding below it. Cut by their own road, at the tolerance given, a plate turned about in space measured 60 000 with its
    /// hole through it left in, or nought, and was said to be trusted so.
    /// </summary>
    public class RoundingLevelG1sTests
    {
        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        private static readonly double[] Plate = { 0, 0, 200, 0, 200, 100, 0, 100 };

        /// <summary>The plate's up, leaning in space.</summary>
        private static readonly GeoVector3 Up = Unit(new GeoVector3(0.3, -0.4, 0.866));

        /// <summary>The plate's length, square to its up.</summary>
        private static readonly GeoVector3 Along = Unit(new GeoVector3(1, 0.2, 0).Subtract(Up.Multiply(new GeoVector3(1, 0.2, 0).DotProduct(Up))));

        /// <summary>The plate's width.</summary>
        private static readonly GeoVector3 Across = Up.CrossProduct(Along);

        private static GeoVector3 Unit(GeoVector3 vector) => vector.Multiply(1 / vector.Length);

        /// <summary>The point at a length, a width and a height in the turned frame standing at (out, out, out).</summary>
        private static GeoPoint3 At(double far, double x, double y, double z)
            => new GeoPoint3(far, far, far).Add(Along.Multiply(x)).Add(Across.Multiply(y)).Add(Up.Multiply(z));

        /// <summary>
        /// The plate 200 by 100 by 3 standing in the turned frame, with openings each given as its length from and to, its width
        /// from and to and its height from and to.
        /// </summary>
        private static GeoSolid3 TurnedPlate(double far, params double[][] holes)
        {
            var corners = new List<GeoPoint3>();

            for (int i = 0; i < Plate.Length; i += 2)
            {
                corners.Add(At(far, Plate[i], Plate[i + 1], 0));
            }

            GeoSolid3 body = GeoSolid3.Extrude(new GeoPolygon3(corners.ToArray()), Up.Multiply(3), Tolerance.Default);
            GeoSolid3[] openings = holes
                .Select(h => GeoSolid3.Extrude(
                    new GeoPolygon3(At(far, h[0], h[1], h[4]), At(far, h[2], h[1], h[4]), At(far, h[2], h[3], h[4]), At(far, h[0], h[3], h[4])),
                    Up.Multiply(h[5] - h[4]), Tolerance.Default))
                .ToArray();

            return body.WithOpenings(openings);
        }

        /// <summary>The rounding of the coordinates of a body: a quadrillionth of the largest of them.</summary>
        private static double RoundingOf(GeoSolid3 body)
        {
            GeoAabb3 box = body.GetAabb();

            return 1E-15 * new[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z }.Max(Math.Abs);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E6)]
        public void AtToleranceNought_ATurnedPlateWithAHoleThroughIt_MeasuresWhatTheHoleLeaves(double far)
        {
            // The hole 20 by 20 drawn 7 past both faces: measured at nought the plate held 60 000, the hole left in, and
            // TryGetVolume trusted it.
            GeoSolid3 plate = TurnedPlate(far, new double[] { 90, 40, 110, 60, -7, 10 });

            Assert.Equal(58_800, plate.GetVolume(Nought), 1E-6 * 58_800);
            Assert.True(plate.TryGetVolume(out double volume, Nought));
            Assert.Equal(58_800, volume, 1E-6 * 58_800);
        }

        [Fact]
        public void AtToleranceNought_ATurnedPlateWithTwoBlindHoles_MeasuresWhatTheHolesLeave()
        {
            // A million out, two holes from under it 1 and 1.5 deep: measured at nought the plate held 60 000.
            GeoSolid3 plate = TurnedPlate(1E6, new double[] { 20, 25, 50, 75, -7, 1 }, new double[] { 80, 30, 120, 70, -7, 1.5 });

            Assert.Equal(56_100, plate.GetVolume(Nought), 1E-6 * 56_100);
        }

        [Theory]
        [InlineData(1E3)]
        [InlineData(1E5)]
        public void AtToleranceNought_ATurnedPlateWithTwoNotches_IsNotTrustedAsHoldingNothing(double far)
        {
            // A notch 20 by 20 at one end and one 20 wide and 1 deep across the other, flush with three faces: measured at nought
            // the plate held nothing, and TryGetVolume trusted that.
            GeoSolid3 plate = TurnedPlate(far, new double[] { 0, 40, 20, 60, -7, 10 }, new double[] { 180, 0, 200, 100, -7, 1 });

            Assert.Equal(56_800, plate.GetVolume(Nought), 1E-6 * 56_800);

            if (plate.TryGetVolume(out double volume, Nought))
            {
                Assert.Equal(56_800, volume, 1E-6 * 56_800);
            }
        }

        [Fact]
        public void AtToleranceNought_ATurnedPlateWithAHoleNearOneEnd_HasTheAreaAndCentroidOfWhatTheHoleLeaves()
        {
            // The hole 20 by 20 through the plate at 20 to 40 along it: the plate loses 800 of its faces and gains 240 of wall,
            // and its centre moves 1.43 along it, away from the hole. Measured at nought, neither moved.
            GeoSolid3 plate = TurnedPlate(0, new double[] { 20, 40, 40, 60, -7, 10 });

            Assert.Equal(41_800 - 800 + 240, plate.GetSurfaceArea(Nought), 1E-6 * 41_800);
            Assert.True(plate.GetCentroid(Nought).DistanceTo(At(0, (60_000 * 100 - 1_200 * 30) / 58_800.0, 50, 1.5)) < 1E-6);
        }

        [Theory]
        [InlineData(1E6, 1.0)]
        [InlineData(1E6, 2.0)]
        [InlineData(1E7, 1.0)]
        public void AtOrJustAboveTheRounding_ATurnedPlateWithTwoNotches_MeasuresWhatTheyLeave(double far, double roundings)
        {
            // At the rounding and twice it, the notch flush with three faces would not come out of the plate by a difference
            // at the tolerance alone, and was measured as material: 58 800 where TryCutOpenings gave 56 800.
            GeoSolid3 plate = TurnedPlate(far, new double[] { 0, 40, 20, 60, -7, 10 }, new double[] { 180, 0, 200, 100, -7, 1 });
            double tolerance = roundings * RoundingOf(plate);
            var within = new Tolerance(tolerance, tolerance, tolerance, tolerance);

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, within));
            Assert.Equal(56_800, material.GetVolume(within), 1E-6 * 56_800);
            Assert.Equal(56_800, plate.GetVolume(within), 1E-6 * 56_800);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E3)]
        [InlineData(1E6)]
        public void AtToleranceNought_TheSurfaceOfATurnedPlateWithAHoleThroughIt_HoldsWhatTheHoleLeaves(double far)
        {
            // The mesh DxfWriter writes too: cut at nought, its triangles held 29 999.99 at the origin, the plate's bottom lost.
            GeoSolid3 plate = TurnedPlate(far, new double[] { 90, 40, 110, 60, -7, 10 });
            GeoTriangle3[] surface = plate.TriangulateSurface(Nought);

            GeoPoint3 origin = surface[0].A;
            double sixTimes = 0;

            foreach (GeoTriangle3 triangle in surface)
            {
                GeoVector3 a = origin.GetVectorTo(triangle.A);
                sixTimes += a.DotProduct(origin.GetVectorTo(triangle.B).CrossProduct(origin.GetVectorTo(triangle.C)));
            }

            Assert.Equal(58_800, Math.Abs(sixTimes) / 6, 1E-6 * 58_800);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E6)]
        public void AtToleranceNought_APointInTheMiddleOfAHoleThroughATurnedPlate_IsProjectedOntoTheWall(double far)
        {
            // The wall of the hole 20 by 20 stands 10 from its middle: at nought the point was projected 1.5 from it at the
            // origin, onto a face across the hole, and a million out 10.11 from it, onto the rim of the wall.
            GeoSolid3 plate = TurnedPlate(far, new double[] { 90, 40, 110, 60, -7, 10 });
            GeoPoint3 middle = At(far, 100, 50, 1.5);

            Assert.Equal(10, middle.DistanceTo(plate.GetClosestPointOnBoundary(middle, Nought)), 1E-6);
        }
    }
}
