using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Openings cut in at once together leave each its own skin: a plane of one opening the tolerance and a hair inside a
    /// face cut a skin off the whole plate, and every other opening thinner under that face than the tolerance kept that
    /// skin over it, where cut in alone it loses its own.
    /// </summary>
    public class CutOpeningsG8fTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(0.01, 1E-5, 1E-5, 0.01);

        /// <summary>The area of a hole 22 across, a cylinder of 16 facets.</summary>
        private static readonly double HoleArea = 8 * 121 * Math.Sin(Math.PI / 8);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        /// <summary>
        /// A plate 300 by 200 by 12 with twelve openings in four rows of three, turned about an axis and carried far from the
        /// origin, or left near it; each opening is made of its row, its place in the row, and its own depth under the faces.
        /// </summary>
        private static GeoSolid3 Plate(GeoVector3 axis, double angle, IReadOnlyList<double> under, Func<int, int, double, GeoSolid3> opening, bool far)
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
        /// The least and the most a plate can hold that its skins leave: each opening whose depth under a face is further than
        /// a millionth from the tolerance leaves its skin or not as it is thicker or thinner, and each nearer may do either.
        /// </summary>
        private static (double Least, double Most) Holds(Tolerance tolerance, IReadOnlyList<double> under, Func<int, double, (double Taken, double Skins)> each)
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

            return (least, most);
        }

        /// <summary>Asserts the material valid, without openings, and holding a volume its skins can leave.</summary>
        private static void AssertValidAndHolds(GeoSolid3 material, Tolerance tolerance, IReadOnlyList<double> under, Func<int, double, (double Taken, double Skins)> each)
        {
            (double least, double most) = Holds(tolerance, under, each);
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

        /// <summary>
        /// Twelve boxes hidden 0.01 inside the faces of a plate near the origin to within a millionth, the fourth 5.7E-6
        /// nearer them.
        /// </summary>
        private static readonly double[] HiddenOneNearer =
        {
            0.010000172404266065, 0.01000000005209842, 0.00999999973479821, 0.009994300949894473,
            0.009999998310455213, 0.010000003116576532, 0.009999990501179131, 0.01000007447325479,
            0.009999999999976207, 0.0100000080930157, 0.009999981209556532, 0.01000000000001387,
        };

        private static GeoSolid3 PlateHiddenOneNearer()
            => Plate(new GeoVector3(0.29093832862141467, -0.1318809060062659, 0.9886565692669975), 1.1779636597158218, HiddenOneNearer, Hidden, far: false);

        [Fact]
        public void TwelveBoxesHiddenAToleranceInsideTheFacesOneAFewMillionthsNearer_LeaveThatOneNoSkin()
        {
            // The first box lies 1.7E-7 deeper than the tolerance, and a plane of its floor cut a skin off the whole plate;
            // the fourth, 5.7E-6 nearer the faces than the tolerance, kept that skin over and under it, and the plate came
            // out valid holding 9.68 more than the most its skins can leave.
            GeoSolid3 plate = PlateHiddenOneNearer();
            int takenOut = Boolean3.TakenOutOneAtATime;

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Hundredth));

            Assert.Equal(0, Boolean3.TakenOutOneAtATime - takenOut);
            AssertValidAndHolds(material, Hundredth, HiddenOneNearer, HiddenTakes);
        }

        [Fact]
        public void TwelveBoxesHiddenAToleranceInsideTheFacesOneAFewMillionthsNearer_AreMeasuredWithoutThatOnesSkin()
        {
            // The measures cut the openings in at once as TryCutOpenings does, and held the same 9.68 too much.
            (double least, double most) = Holds(Hundredth, HiddenOneNearer, HiddenTakes);

            Assert.InRange(PlateHiddenOneNearer().GetVolume(Hundredth), least - 1E-3, most + 1E-3);
        }

        [Fact]
        public void TwelveBoxesHiddenAToleranceInsideTheFacesOneAMillionthNearerFarFromTheOrigin_LeaveThatOneNoSkin()
        {
            // The first and the last box lie 3.8E-7 and 4.2E-7 deeper than the tolerance; the second, which runs out to the
            // side of the plate, lies 1.3E-6 nearer the faces than it, and the plate, turned and far from the origin, came out
            // valid holding 25.08 more than the most its skins can leave.
            double[] under =
            {
                0.01000037650034386, 0.009998748565880412, 0.010000000000016149, 0.009999999971859396,
                0.009999999933852208, 0.009999707381043346, 0.009999485188981468, 0.009999999999974847,
                0.010000000000513124, 0.00999999999998823, 0.010000000083074431, 0.01000041877218394,
            };
            GeoSolid3 plate = Plate(new GeoVector3(-0.6324676226975712, 0.4216184361938473, 0.6201243985538019), 1.0704505774520574, under, Hidden, far: true);

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Hundredth));

            AssertValidAndHolds(material, Hundredth, under, HiddenTakes);
        }

        [Fact]
        public void TwelveHolesEndingAToleranceUnderTheTopOneTwoMillionthsNearer_LeaveThatOneNoSkin()
        {
            // The first floor lies 1.5E-8 deeper than the tolerance under the top, and its plane cut a skin off the whole
            // plate; the eighth, 2.0E-6 nearer the top than the tolerance, kept that skin, and the plate came out valid
            // holding 3.71 more than the most its skins can leave.
            double[] under =
            {
                0.010000014947610774, 0.009999918808676731, 0.009999999993254176, 0.010000000000193146,
                0.01000001421309503, 0.010000000161051369, 0.010000000005465293, 0.009997976656178244,
                0.010000000439806813, 0.009999999999688704, 0.010000000000182568, 0.009999998920389538,
            };
            GeoSolid3 plate = Plate(new GeoVector3(-0.14056896937106222, 0.11051479219948623, 0.048984577436458565), 2.8807551995295824, under, Hole, far: false);

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Hundredth));

            AssertValidAndHolds(material, Hundredth, under, HoleTakes);
        }

        [Fact]
        public void TwelveHolesEndingAToleranceUnderTheTopTwoAFewMillionthsNearer_LeaveThoseNoSkin()
        {
            // The fifth floor lies 7.4E-6 nearer the top than the tolerance and the eighth 1.3E-6; both kept the skin the
            // plane of another floor cut off the plate, and the plate came out valid holding 7.38 more than the most its
            // skins can leave.
            double[] under =
            {
                0.01000005173855961, 0.009999999999988577, 0.009999999999972012, 0.0099999979273275,
                0.009992597973169969, 0.009999401851882209, 0.010000649306492231, 0.00999868800034209,
                0.010000000598891577, 0.010000094078525333, 0.01000125997519815, 0.010000000000044907,
            };
            GeoSolid3 plate = Plate(new GeoVector3(-0.5248011152841157, 0.364619797731107, 0.6477217896132366), 0.6900391088286597, under, Hole, far: false);

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Hundredth));

            AssertValidAndHolds(material, Hundredth, under, HoleTakes);
        }
    }
}
