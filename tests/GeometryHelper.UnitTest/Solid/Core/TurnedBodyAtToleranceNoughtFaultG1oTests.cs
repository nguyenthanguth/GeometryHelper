using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// At a tolerance below the rounding of the coordinates, nought among them, the openings of a body are cut within that
    /// rounding, as the queries read them. Cut at nought, a plate turned about in space with a hole through it came back as a
    /// shell left open, its bottom lost, and the cut said it had worked; where cutting the openings in together left the
    /// material open, taking them out one at a time by a difference within the rounding came back closed and wrong.
    /// </summary>
    public class TurnedBodyAtToleranceNoughtFaultG1oTests
    {
        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

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
        /// A prism of an outline, given as pairs of length and width, standing a height up the turned frame, with openings
        /// each given as its length from and to, its width from and to and its height from and to.
        /// </summary>
        private static GeoSolid3 Prism(double far, double[] outline, double height, params double[][] holes)
        {
            var corners = new List<GeoPoint3>();

            for (int i = 0; i < outline.Length; i += 2)
            {
                corners.Add(At(far, outline[i], outline[i + 1], 0));
            }

            GeoSolid3 body = GeoSolid3.Extrude(new GeoPolygon3(corners.ToArray()), Up.Multiply(height), Tolerance.Default);
            GeoSolid3[] openings = holes
                .Select(h => GeoSolid3.Extrude(
                    new GeoPolygon3(At(far, h[0], h[1], h[4]), At(far, h[2], h[1], h[4]), At(far, h[2], h[3], h[4]), At(far, h[0], h[3], h[4])),
                    Up.Multiply(h[5] - h[4]), Tolerance.Default))
                .ToArray();

            return body.WithOpenings(openings);
        }

        /// <summary>
        /// Asserts the openings cut in at a tolerance leave a closed body, valid at the default tolerance, holding a volume to a
        /// millionth of it, and gives the material.
        /// </summary>
        private static GeoSolid3 AssertCutClosed(GeoSolid3 body, double volume, Tolerance tolerance)
        {
            Assert.True(body.TryCutOpenings(out GeoSolid3 material, tolerance));
            Assert.True(material.IsClosed(tolerance));
            Assert.True(material.IsClosed(Tolerance.Default));
            Assert.True(material.Validate(Tolerance.Default).IsValid);
            Assert.Equal(volume, material.GetVolume(tolerance), 1E-6 * volume);

            return material;
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(1E6, 0.0)]
        [InlineData(1E6, 1E-12)]
        public void BelowTheRounding_ATurnedPlateWithAHoleThroughIt_IsCutClosed(double far, double tolerance)
        {
            // The plate 200 by 100 by 3, the hole 20 by 20 drawn 7 past both faces: at nought the cut gave 7 faces, the bottom
            // lost, and said it had worked; a million out at a millionth of a millionth it gave the plate with no hole cut.
            GeoSolid3 plate = Prism(far, new double[] { 0, 0, 200, 0, 200, 100, 0, 100 }, 3, new double[] { 90, 40, 110, 60, -7, 10 });

            var within = new Tolerance(tolerance, tolerance, tolerance, tolerance);

            Assert.True(AssertCutClosed(plate, 58_800, within).Validate(within).IsValid);
        }

        [Fact]
        public void AtToleranceNought_ATurnedPlateWithTwoBlindHoles_HoldsWhatTheHolesLeave()
        {
            // The plate 200 by 100 by 3 a million out, two holes from under it, 1 and 1.5 deep: at nought the cut gave a shell
            // of 148 faces left open. Within the rounding, cut in together they leave it open, and taken out one at a time by a
            // difference the first took nothing out, and the plate held 57 600, closed.
            GeoSolid3 plate = Prism(1E6, new double[] { 0, 0, 200, 0, 200, 100, 0, 100 }, 3,
                new double[] { 20, 25, 50, 75, -7, 1 }, new double[] { 80, 30, 120, 70, -7, 1.5 });

            AssertCutClosed(plate, 60_000 - 30 * 50 * 1 - 40 * 40 * 1.5, Nought);
        }

        [Theory]
        [InlineData(1E3)]
        [InlineData(1E7)]
        public void AtToleranceNought_ATurnedLWithAHoleThroughAndABlindOne_HoldsWhatTheHolesLeave(double far)
        {
            // The L 100 by 80, its legs 20 wide, 50 tall; a hole 25 by 12 through the one leg, one 12 by 35 down 30 into the other.
            GeoSolid3 body = Prism(far, new double[] { 0, 0, 100, 0, 100, 20, 20, 20, 20, 80, 0, 80 }, 50,
                new double[] { 30, 4, 55, 16, -7, 57 }, new double[] { 4, 35, 16, 70, 20, 57 });

            AssertCutClosed(body, 160_000 - 25 * 12 * 50 - 12 * 35 * 30, Nought);
        }

        [Theory]
        [InlineData(1E3)]
        [InlineData(1E8)]
        public void AtToleranceNought_ATurnedStarWithThreeHoles_HoldsWhatTheHolesLeave(double far)
        {
            // The star of ten points, 50 out and 20 in about (50, 50), 40 tall; a hole 12 by 12 through it, one down 30 from
            // its top and one 12 by 11 up 25 from its foot.
            var outline = new List<double>();

            for (int i = 0; i < 20; i++)
            {
                double angle = Math.PI * i / 10;
                double reach = i % 2 == 0 ? 50 : 20;

                outline.Add(50 + reach * Math.Cos(angle));
                outline.Add(50 + reach * Math.Sin(angle));
            }

            double area = 0;

            for (int i = 0, j = 19; i < 20; j = i++)
            {
                area += outline[2 * j] * outline[2 * i + 1] - outline[2 * i] * outline[2 * j + 1];
            }

            GeoSolid3 body = Prism(far, outline.ToArray(), 40,
                new double[] { 37, 37, 49, 49, -7, 47 }, new double[] { 51, 37, 63, 49, 10, 47 }, new double[] { 44, 51, 56, 62, -7, 25 });

            AssertCutClosed(body, Math.Abs(area) / 2 * 40 - 144 * 40 - 144 * 30 - 132 * 25, Nought);
        }

        [Fact]
        public void AtToleranceNought_ThePlateCutWithinTheRounding_HasItsVolumeTrustedAtNought()
        {
            // The plate 200 by 100 by 3 standing on the floor, the hole 20 by 20 through it: cut within the rounding, its faces
            // met on corners up to 1.4E-14 apart, and TryGetVolume at nought refused the material it gave.
            GeoSolid3 box = GeoSolid3.Extrude(
                new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0), new GeoPoint3(200, 100, 0), new GeoPoint3(0, 100, 0)),
                new GeoVector3(0, 0, 3), Tolerance.Default);
            GeoSolid3 hole = GeoSolid3.Extrude(
                new GeoPolygon3(new GeoPoint3(90, 40, -7), new GeoPoint3(110, 40, -7), new GeoPoint3(110, 60, -7), new GeoPoint3(90, 60, -7)),
                new GeoVector3(0, 0, 17), Tolerance.Default);

            GeoSolid3 material = AssertCutClosed(box.WithOpenings(new[] { hole }), 58_800, Nought);

            Assert.True(material.TryGetVolume(out double volume, Nought));
            Assert.Equal(58_800, volume, 1E-9);
        }

        [Theory]
        [InlineData(40, 30, 80, 70, 0, 1.5, 120, 30, 160, 70, 1, 3, 54_400, 124)]
        [InlineData(40, 30, 80, 70, 1.5, 10, 60, 30, 100, 70, -7, 2, 54_800, 126)]
        public void APlanarToleranceOfNoughtWithAPointToleranceOfAThousandth_CutsAsItDid(
            double x0, double y0, double x1, double y1, double z0, double z1,
            double u0, double v0, double u1, double v1, double w0, double w1, double volume, int faces)
        {
            // The plate a thousand out with a hole from under it and one from over it, flush with the faces or overlapping each
            // other: a planar tolerance below the rounding alone does not raise the cut to it. Raised, the overlapping holes
            // were refused, and the flush ones gave 35 faces where they give 124.
            GeoSolid3 plate = Prism(1E3, new double[] { 0, 0, 200, 0, 200, 100, 0, 100 }, 3,
                new[] { x0, y0, x1, y1, z0, z1 }, new[] { u0, v0, u1, v1, w0, w1 });

            GeoSolid3 material = AssertCutClosed(plate, volume, new Tolerance(1E-3, 1E-5, Math.PI / 180, 0));

            Assert.Equal(faces, material.Faces.Count);
        }

        [Fact]
        public void AtToleranceNought_AnOpeningFlushWithAFace_IsRefusedRatherThanCutWrong()
        {
            // The plate a thousand out with a hole 1.5 up from its floor and one 2 down from its top, each flush with a face:
            // cut at nought it came back holding 48 880 where it holds 54 400, not valid, and the cut said it had worked. Within
            // the rounding neither way of cutting closes it, and the method says so.
            GeoSolid3 plate = Prism(1E3, new double[] { 0, 0, 200, 0, 200, 100, 0, 100 }, 3,
                new double[] { 40, 30, 80, 70, 0, 1.5 }, new double[] { 120, 30, 160, 70, 1, 3 });

            Assert.False(plate.TryCutOpenings(out GeoSolid3 material, Nought));
            Assert.Null(material);
        }
    }
}
