using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Takeoff;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Two spiral beams faceted at different stations, a ramp and a girder lying along its outer edge, overlapping: each plane
    /// of the one crosses the other at many places along its turn, and cut by every plane wherever it runs, the body cut came
    /// apart into tens of thousands of cells. Cut by each plane only where its faces are once the cells grow many, it makes a
    /// few thousand, and the common part is what slicing gives.
    /// </summary>
    public class Boolean3SpiralBeamsTests
    {
        private static readonly SolidBooleanOptions Options = new SolidBooleanOptions(
            new Tolerance(0.001, 1e-5, Tolerance.DefaultEqualAngleRad, 0.001), 0.01,
            new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01));

        // The most cells either may be cut into: cut by every plane everywhere, the girder was cut into 36 509.
        private const long MostCells = 10000;

        // The most cells a difference or a union of the two may be cut into, the check of what it gave included: cut by
        // every plane everywhere, the ramp less the girder was cut into 52 712, and the union into 36 509.
        private const long MostCellsBothWays = 20000;

        /// <summary>
        /// A rectangle from radius r0 to r1 and height h0 to h1 swept about the z axis from angle a0 to a1, rising 3 000 a
        /// half turn, in n facets: the sides at r0 and r1 flat, the top and the bottom two triangles a facet.
        /// </summary>
        private static GeoSolid3 Spiral(double r0, double r1, double h0, double h1, double a0, double a1, int n)
        {
            double rise = 3000.0 / Math.PI;
            GeoPoint3 P(double r, double a, double h) => new GeoPoint3(r * Math.Cos(a), r * Math.Sin(a), h + rise * a);
            GeoFace3 Face(params GeoPoint3[] corners) => new GeoFace3(new GeoPolygon3(corners));
            var faces = new List<GeoFace3>();

            for (int k = 0; k < n; k++)
            {
                double a = a0 + (a1 - a0) * k / n, b = a0 + (a1 - a0) * (k + 1) / n;
                faces.Add(Face(P(r1, a, h0), P(r1, b, h0), P(r1, b, h1), P(r1, a, h1)));
                faces.Add(Face(P(r0, a, h0), P(r0, a, h1), P(r0, b, h1), P(r0, b, h0)));
                faces.Add(Face(P(r0, a, h0), P(r0, b, h0), P(r1, b, h0)));
                faces.Add(Face(P(r0, a, h0), P(r1, b, h0), P(r1, a, h0)));
                faces.Add(Face(P(r0, a, h1), P(r1, a, h1), P(r1, b, h1)));
                faces.Add(Face(P(r0, a, h1), P(r1, b, h1), P(r0, b, h1)));
            }

            faces.Add(Face(P(r0, a0, h0), P(r1, a0, h0), P(r1, a0, h1), P(r0, a0, h1)));
            faces.Add(Face(P(r0, a1, h0), P(r0, a1, h1), P(r1, a1, h1), P(r1, a1, h0)));
            return new GeoSolid3(faces);
        }

        // A ramp 7 600 wide and 300 thick, from radius 3 000, in 32 facets over half a turn.
        private static GeoSolid3 Ramp() => Spiral(3000, 10600, 0, 300, 0, Math.PI, 32);

        // A girder 600 wide and 500 deep, in 38 facets over nearly the same half turn, 100 into the ramp's outer edge and
        // its top 20 over the ramp's floor.
        private static GeoSolid3 Girder() => Spiral(10500, 11100, -480, 20, 0.01, Math.PI - 0.01, 38);

        // The two faceted 56 and 66 times, the girder's top a hair over the ramp's floor.
        private static GeoSolid3 FineRamp() => Spiral(3000, 10600, 0, 300, 0, Math.PI, 56);

        private static GeoSolid3 FineGirder(double top) => Spiral(10500, 11100, top - 500, top, 0.01, Math.PI - 0.01, 66);

        private static double Sliced(GeoSolid3 part, GeoSolid3 within)
        {
            Assert.True(Slice3.TryVolume(part, new[] { within }, new GeoSolid3[0], out double volume));
            return volume;
        }

        [Fact]
        public void TheCommonPartOfTwoSpiralBeams_IsCutInAFewThousandCells_AndHoldsWhatSlicingGives()
        {
            GeoSolid3 girder = Girder(), ramp = Ramp();
            double expected = Sliced(girder, ramp);

            long before = Boolean3.CellsMade;
            bool made = Boolean3.TryIntersect(girder, ramp, out GeoSolid3 common, Options, out BooleanOutcome outcome);
            long cells = Boolean3.CellsMade - before;

            Assert.True(made, outcome.ToString());
            Assert.True(common.Validate(Options.Tolerance).IsValid);
            double slack = Options.Tolerance.EqualPoint * common.GetSurfaceArea(Options.Tolerance);
            Assert.InRange(common.GetVolume(Options.Tolerance) - expected, -slack, slack);
            Assert.InRange(cells, 1, MostCells);
        }

        [Fact]
        public void TheTakeoffOfTwoSpiralBeams_IsCutInAFewThousandCells_AndDeductsWhatSlicingGives()
        {
            GeoSolid3 girder = Girder(), ramp = Ramp();
            double expected = Sliced(girder, ramp);
            Assert.True(Boolean3.TryIntersect(girder, ramp, out GeoSolid3 common, Options, out _));
            double slack = Options.Tolerance.EqualPoint * common.GetSurfaceArea(Options.Tolerance);
            var items = new List<VolumeItem> { new VolumeItem(girder, "girder", 4, 0), new VolumeItem(ramp, "ramp", 6, 1) };

            // One thread: the take-off runs on this one, which the count of cells is kept for.
            long before = Boolean3.CellsMade;
            VolumeTakeoffResult result = VolumeTakeoff.Run(items, new VolumeTakeoffOptions(Options, 1))[0];
            long cells = Boolean3.CellsMade - before;

            Assert.Empty(result.Issues);
            Assert.InRange(result.DeductedVolume - expected, -slack, slack);
            Assert.InRange(cells, 1, MostCells);
        }

        [Fact]
        public void TheRampLessTheGirder_IsCutInAFewThousandCells_AndLeavesWhatSlicingGives()
        {
            GeoSolid3 girder = Girder(), ramp = Ramp();
            double expected = ramp.GetVolume(Options.Tolerance) - Sliced(girder, ramp);
            Assert.True(Boolean3.TryIntersect(girder, ramp, out GeoSolid3 common, Options, out _));
            double slack = Options.Tolerance.EqualPoint * common.GetSurfaceArea(Options.Tolerance);

            long before = Boolean3.CellsMade;
            bool made = Boolean3.TrySubtract(ramp, girder, out GeoSolid3 rest, Options, out BooleanOutcome outcome);
            long cells = Boolean3.CellsMade - before;

            Assert.True(made, outcome.ToString());
            Assert.True(rest.Validate(Options.Tolerance).IsValid);
            Assert.InRange(rest.GetVolume(Options.Tolerance) - expected, -slack, slack);
            Assert.InRange(cells, 1, MostCellsBothWays);
        }

        [Fact]
        public void TheUnionOfTwoSpiralBeams_IsCutInAFewThousandCells_AndHoldsWhatSlicingGives()
        {
            GeoSolid3 girder = Girder(), ramp = Ramp();
            double expected = ramp.GetVolume(Options.Tolerance) + girder.GetVolume(Options.Tolerance) - Sliced(girder, ramp);
            Assert.True(Boolean3.TryIntersect(girder, ramp, out GeoSolid3 common, Options, out _));
            double slack = Options.Tolerance.EqualPoint * common.GetSurfaceArea(Options.Tolerance);

            long before = Boolean3.CellsMade;
            bool made = Boolean3.TryUnion(girder, ramp, out GeoSolid3 union, Options, out BooleanOutcome outcome);
            long cells = Boolean3.CellsMade - before;

            Assert.True(made, outcome.ToString());
            Assert.True(union.Validate(Options.Tolerance).IsValid);
            Assert.InRange(union.GetVolume(Options.Tolerance) - expected, -slack, slack);
            Assert.InRange(cells, 1, MostCellsBothWays);
        }

        [Fact]
        public void TheRampLessAGirderWhoseTopLiesTwoThousandthsIntoItsFloor_IsLeftWithinTheToleranceOrNotWorkedOut()
        {
            // Within a hundredth, the ramp cut came out open, and sealed it did not add up; the cells of both cut by every
            // plane of both had a cell judged apart, and that open result, welded closed, was taken unchecked: 35 554.6
            // cubic millimetres short of what is left, an eighth of the 263 740.0 the two share.
            GeoSolid3 girder = FineGirder(0.002), ramp = FineRamp();
            double expected = ramp.GetVolume(Options.Tolerance) - Sliced(girder, ramp);
            Assert.True(Boolean3.TryIntersect(girder, ramp, out GeoSolid3 common, Options, out _));
            double slack = Options.Tolerance.EqualPoint * common.GetSurfaceArea(Options.Tolerance);

            bool made = Boolean3.TrySubtract(ramp, girder, out GeoSolid3 rest, Options, out BooleanOutcome outcome);

            if (made)
            {
                Assert.InRange(rest.GetVolume(Options.Tolerance) - expected, -slack, slack);
            }
            else
            {
                Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            }
        }
    }
}
