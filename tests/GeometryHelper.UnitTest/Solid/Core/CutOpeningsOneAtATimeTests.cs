using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Openings taken out one at a time by differences leave what is refused where it is not valid: holes whose floors lay
    /// the tolerance under the top of a plate, to a few bits, came out of twelve differences wound two ways at the top, and
    /// the cut was said to have worked.
    /// </summary>
    public class CutOpeningsOneAtATimeTests
    {
        private static readonly Tolerance Thousandth = new Tolerance(0.001, 1E-5, 1E-5, 0.001);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        /// <summary>
        /// A plate 300 by 200 by 12 with twelve round holes 22 across, in four rows of three, each from under the plate to its
        /// own depth under the top, turned about an axis and carried far from the origin.
        /// </summary>
        private static GeoSolid3 Plate(GeoVector3 axis, double angle, IReadOnlyList<double> under)
        {
            GeoTransform3 place = GeoTransform3.Translation(new GeoVector3(123456.7, -65432.1, 9876.5))
                * GeoTransform3.RotationAxis(GeoPoint3.Origin, axis, angle);
            var openings = new List<GeoSolid3>();

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double x = 40 + i * 70;
                    double y = 40 + j * 60;
                    openings.Add(GeoSolid3.Cylinder(new GeoPoint3(x, y, -5), new GeoPoint3(x, y, 12 - under[3 * i + j]), 11, 16, Tolerance.Default).TransformBy(place));
                }
            }

            return new GeoSolid3(Box(0, 0, 0, 300, 200, 12).TransformBy(place).Faces, openings);
        }

        [Fact]
        public void TwelveHolesEndingAToleranceUnderTheTopToAFewBits_AreNotCutIntoABodyThatIsNotValid()
        {
            // Eleven floors lie 0.001 under the top to within 1E-7, the fourth 1.8E-7 further down. Cut in at once together
            // the material came out open, and taken out by differences wound two ways at its top, holding 666 653.98 where it
            // holds 666 657.36; the cut returned true.
            double[] under =
            {
                0.000999999999997085, 0.0009999999999978242, 0.0009999999999984524, 0.001000182829348675,
                0.001000000000338416, 0.0010000000037927847, 0.000999992828409303, 0.0009999999991872966,
                0.0010000000000238078, 0.001000000000060705, 0.0009999999993513405, 0.0009998373030014206,
            };
            GeoSolid3 plate = Plate(new GeoVector3(0.6235469298546887, -0.07871804343476796, -0.5839141474961835), 3.5611189154727008, under);
            int takenOut = Boolean3.TakenOutOneAtATime;

            bool cut = plate.TryCutOpenings(out GeoSolid3 material, Thousandth);

            Assert.Equal(1, Boolean3.TakenOutOneAtATime - takenOut);
            Assert.False(cut, cut ? material.Validate(Thousandth).ToString() : null);
            Assert.Null(material);
        }
    }
}
