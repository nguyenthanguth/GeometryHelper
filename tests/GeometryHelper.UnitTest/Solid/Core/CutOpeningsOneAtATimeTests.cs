using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Openings taken out one at a time by differences are taken out within the rounding of the coordinates too, as the cut
    /// of openings reads a corner: holes whose floors lay the tolerance under the top of a plate, to a few bits, came out of
    /// twelve differences wound two ways at the top, or closed and short by what faces sloping across the plate lost.
    /// </summary>
    public class CutOpeningsOneAtATimeTests
    {
        private static readonly Tolerance Thousandth = new Tolerance(0.001, 1E-5, 1E-5, 0.001);
        private static readonly Tolerance Hundredth = new Tolerance(0.01, 1E-5, 1E-5, 0.01);

        /// <summary>The area of a hole 22 across, a cylinder of 16 facets.</summary>
        private static readonly double HoleArea = 8 * 121 * Math.Sin(Math.PI / 8);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        /// <summary>
        /// A plate 300 by 200 by 12 with twelve openings in four rows of three, turned about an axis and carried far from the
        /// origin, or left near it; each opening is made of its row, its place in the row, and its own depth under the faces.
        /// </summary>
        private static GeoSolid3 Plate(GeoVector3 axis, double angle, IReadOnlyList<double> under, Func<int, int, double, GeoSolid3> opening, bool far = true)
        {
            GeoTransform3 place = GeoTransform3.RotationAxis(GeoPoint3.Origin, axis, angle);

            if (far)
            {
                place = GeoTransform3.Translation(new GeoVector3(123456.7, -65432.1, 9876.5)) * place;
            }

            var openings = new List<GeoSolid3>();

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    openings.Add(opening(i, j, under[3 * i + j]).TransformBy(place));
                }
            }

            return new GeoSolid3(Box(0, 0, 0, 300, 200, 12).TransformBy(place).Faces, openings);
        }

        /// <summary>A round hole 22 across from under the plate to the depth given under its top.</summary>
        private static GeoSolid3 Hole(int i, int j, double under)
            => GeoSolid3.Cylinder(new GeoPoint3(40 + i * 70, 40 + j * 60, -5), new GeoPoint3(40 + i * 70, 40 + j * 60, 12 - under), 11, 16, Tolerance.Default);

        /// <summary>
        /// A box 22 by 22 hidden in the plate the depth given under its top and over its bottom; those of the first row run
        /// out to the same depth inside the side of the plate.
        /// </summary>
        private static GeoSolid3 Hidden(int i, int j, double under)
            => Box(i == 0 ? under : 29 + i * 70, 29 + j * 60, under, 51 + i * 70, 51 + j * 60, 12 - under);

        /// <summary>
        /// Asserts the material valid, without openings, and holding a volume its skins can leave: each opening whose depth
        /// under a face is further than a millionth from the tolerance leaves its skin or not as it is thicker or thinner,
        /// and each nearer may do either.
        /// </summary>
        private static void AssertValidAndHolds(GeoSolid3 material, Tolerance tolerance, IReadOnlyList<double> under, Func<int, double, (double Taken, double Skins)> each)
        {
            double least = 300.0 * 200 * 12;
            double most = least;

            for (int k = 0; k < under.Count; k++)
            {
                (double taken, double skins) = each(k, under[k]);
                double off = under[k] - tolerance.EqualPoint;
                least -= taken + (off > 1E-6 ? 0.0 : skins);
                most -= taken + (off < -1E-6 ? skins : 0.0);
            }

            SolidValidation3 check = material.Validate(tolerance);

            Assert.True(check.IsValid, check.ToString());
            Assert.Empty(material.Openings);
            Assert.InRange(material.GetVolume(tolerance), least - 1E-3, most + 1E-3);
        }

        /// <summary>What a round hole takes, its floor under the skin it leaves, and that skin.</summary>
        private static (double Taken, double Skins) HoleTakes(int k, double under) => (HoleArea * (12 - under), HoleArea * under);

        /// <summary>What a hidden box takes, and the skins it leaves over, under and, in the first row, beside it.</summary>
        private static (double Taken, double Skins) HiddenTakes(int k, double under)
        {
            double wide = k < 3 ? 51 - under : 22;
            return (wide * 22 * (12 - 2 * under), 2 * wide * 22 * under + (k < 3 ? 22 * 12 * under : 0.0));
        }

        [Fact]
        public void TwelveHolesEndingAToleranceUnderTheTopToAFewBits_AreCutValidWithinAThousandth()
        {
            // Eleven floors lie 0.001 under the top to within 1E-7, the fourth 1.8E-7 further down. Cut in at once together
            // the material came out open, and taken out by differences wound two ways at its top, holding 666 653.98 where it
            // holds 666 657.36; the cut was refused.
            double[] under =
            {
                0.000999999999997085, 0.0009999999999978242, 0.0009999999999984524, 0.001000182829348675,
                0.001000000000338416, 0.0010000000037927847, 0.000999992828409303, 0.0009999999991872966,
                0.0010000000000238078, 0.001000000000060705, 0.0009999999993513405, 0.0009998373030014206,
            };
            GeoSolid3 plate = Plate(new GeoVector3(0.6235469298546887, -0.07871804343476796, -0.5839141474961835), 3.5611189154727008, under, Hole);
            int takenOut = Boolean3.TakenOutOneAtATime;

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Thousandth));

            Assert.Equal(1, Boolean3.TakenOutOneAtATime - takenOut);
            AssertValidAndHolds(material, Thousandth, under, HoleTakes);
        }

        [Fact]
        public void TwelveHolesEndingAToleranceUnderTheTopToAFewBits_HoldWhatTheirSkinsLeaveWithinAHundredth()
        {
            // Every floor lies 0.01 under the top to within a millionth. Taken out by differences, the plate came out valid
            // and holding 666 526.29, 130.70 short of the plate less every hole through.
            double[] under =
            {
                0.010000000039626927, 0.010000706922175806, 0.010000258156049005, 0.010000000003067545,
                0.00999999999810011, 0.009999999559677062, 0.010000012101920657, 0.010000125533663319,
                0.009999999962249328, 0.00999999997870332, 0.010000008728859318, 0.010000000000020142,
            };
            GeoSolid3 plate = Plate(new GeoVector3(0.31632946679197693, -0.2783142678804297, 0.06093210776379898), 4.172316909847929, under, Hole);
            int takenOut = Boolean3.TakenOutOneAtATime;

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Hundredth));

            Assert.Equal(1, Boolean3.TakenOutOneAtATime - takenOut);
            AssertValidAndHolds(material, Hundredth, under, HoleTakes);
        }

        [Fact]
        public void TwelveHolesEndingAToleranceUnderTheTopToAFewMillionths_HoldWhatTheirSkinsLeaveWithinAHundredth()
        {
            // Every floor but one lies 0.01 under the top to within a millionth, that one 3.4E-6 nearer. Taken out by
            // differences, the plate came out valid and holding 666 655.64, 1.35 short of the least its skins can leave.
            double[] under =
            {
                0.009999999965552526, 0.010000495005469512, 0.009996612998560883, 0.010000003206910467,
                0.01000000000042038, 0.01000000002642145, 0.010000000000136495, 0.009999988494707205,
                0.010000204212565504, 0.00999999999928264, 0.009999999984163257, 0.009999999679441237,
            };
            GeoSolid3 plate = Plate(new GeoVector3(0.3145759046611263, 0.8739737476566685, -0.242138828729344), 2.2312098444584803, under, Hole);
            int takenOut = Boolean3.TakenOutOneAtATime;

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Hundredth));

            Assert.Equal(1, Boolean3.TakenOutOneAtATime - takenOut);
            AssertValidAndHolds(material, Hundredth, under, HoleTakes);
        }

        [Fact]
        public void TwelveBoxesHiddenAToleranceInsideTheFacesToAFewBits_HoldWhatTheirSkinsLeaveWithinAHundredth()
        {
            // Every box lies 0.01 inside the faces to within 3E-6. Taken out by differences, the plate came out valid and
            // holding 627 158.75, 186.93 short of the least its skins can leave; welded otherwise, a box would not come out
            // within the volume it can leave, and the cut was refused.
            double[] under =
            {
                0.009999999578386749, 0.01000000000011218, 0.009999571297254073, 0.009999999999985321,
                0.009999989123255063, 0.010000000000116662, 0.009999997873703254, 0.010002115347096075,
                0.01000002044296976, 0.009999999991221253, 0.009999999999989663, 0.009999999991608699,
            };
            GeoSolid3 plate = Plate(new GeoVector3(-0.31998947324230775, -0.8356493859717853, 0.8844909527732483), 0.28257377924517435, under, Hidden);
            int takenOut = Boolean3.TakenOutOneAtATime;

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Hundredth));

            Assert.Equal(1, Boolean3.TakenOutOneAtATime - takenOut);
            AssertValidAndHolds(material, Hundredth, under, HiddenTakes);
        }

        [Fact]
        public void TwelveBoxesHiddenAToleranceInsideTheFacesNearTheOrigin_AreTakenOutWithinTheToleranceWhereTheRoundingWillNotDo()
        {
            // Every box lies 0.001 inside the faces to within 1.1E-7, near the origin. Taken out by differences within the
            // tolerance alone, the fourth came out 6.7 cubic millimetres short of what it can leave, and the cut was refused;
            // with every difference read within the rounding too, it was refused as well.
            double[] under =
            {
                0.0010000000000286095, 0.0010000853795860585, 0.00100010855901111, 0.0009999998581121641,
                0.0009999999999066938, 0.00099999998624157, 0.0010000000001273792, 0.0010000062696645172,
                0.0009999999998709457, 0.0010000003967607165, 0.0009999999911612957, 0.0009999999999978017,
            };
            GeoSolid3 plate = Plate(new GeoVector3(0.9194017517936424, -0.950992581411727, 0.12353155069217614), 3.484424194080953, under, Hidden, far: false);
            int takenOut = Boolean3.TakenOutOneAtATime;

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Thousandth));

            Assert.Equal(1, Boolean3.TakenOutOneAtATime - takenOut);
            AssertValidAndHolds(material, Thousandth, under, HiddenTakes);
        }
    }
}
