using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A hull cut near a face with a box opening the plane runs through: each half keeps its piece of the box, and the
    /// material of a half is that piece cut out of it. A plane of the box ran through a cell of the half and would not cut
    /// it, and the cell, judged whole by one point, kept the box in the material or took material out with it, while
    /// <see cref="GeoSolid3.TryGetVolume(out double, Tolerance)"/> said every opening was cut out. The volumes expected are
    /// the hull less the box on the side of the plane, worked out from their planes.
    /// </summary>
    public class OpeningCrossingTheCutTests
    {
        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static void AssertHolds(GeoSolid3 half, double expected, Tolerance tolerance)
        {
            Assert.True(half.TryGetVolume(out double volume, tolerance));
            double bound = tolerance.EqualPoint * half.GetSurfaceArea(tolerance);
            Assert.InRange(volume, expected - bound, expected + bound);
            Assert.Equal(volume, half.GetVolume(tolerance));
        }

        private static readonly Tolerance Hundredth = new Tolerance(0.01, 1E-5, Tolerance.Default.EqualAngleRad, 0.01);

        /// <summary>
        /// A hull of 14 points 20 across, far from the origin, cut within a hundredth by a plane 0.019 inside a face and
        /// tilted 4.4E-4 off it; a box 3.5 by 3.5 by 5, turned to the face, stands through it. The half below, and the box.
        /// </summary>
        private static GeoSolid3 HalfTheBoxRunsInto(out GeoSolid3 box)
        {
            GeoSolid3 hull = ConvexHull3.Of(new[]
            {
                P(123449.9921695352, -65433.855227303015, 9879.044869218274), P(123462.54003744453, -65431.135650430726, 9877.581475453024),
                P(123453.71571457415, -65436.440114488425, 9869.09513210626), P(123455.07342838215, -65425.83912868264, 9876.273440654284),
                P(123448.88806423348, -65432.90653621387, 9866.734110420679), P(123454.20711390166, -65431.14698522251, 9866.96806057937),
                P(123446.9255414241, -65424.974799753945, 9884.820830072425), P(123449.1216766946, -65433.23163356256, 9873.15108869162),
                P(123455.60915492033, -65431.50050177714, 9885.221227999182), P(123456.4500901994, -65442.082805419705, 9884.867227641105),
                P(123455.24089045363, -65426.06222800201, 9885.296539669296), P(123455.85307326668, -65424.65016322366, 9882.591329732952),
                P(123450.32452626397, -65429.78215542086, 9876.257955861165), P(123453.04946113747, -65435.80117410724, 9878.226282002277),
            }, Tolerance.Default);
            box = new GeoObb3(
                P(123455.7407020891, -65436.499369014055, 9873.345008152948), 3.543198523364588, 3.543198523364588, 4.984897529699326,
                new GeoVector3(-0.6613663098099799, -0.39755954886140243, -0.6360353837306916),
                new GeoVector3(0.25805537777178966, 0.675605683662742, -0.6906260798765551)).ToSolid();
            var plane = new GeoPlane3(
                P(123453.70209444115, -65436.4281069576, 9869.101789265085),
                new GeoVector3(0.7039813949794719, -0.6210650291331034, -0.3445118649780505));

            Assert.True(hull.WithOpenings(new[] { box }).TrySplitBy(plane, out _, out GeoSolid3 below, Hundredth));
            return below;
        }

        [Fact]
        public void AHalfTheBoxRunsInto_LosesTheBox_AtAHundredth()
        {
            // The half below held 1 465.33, the box left in.
            AssertHolds(HalfTheBoxRunsInto(out _), 1434.7733828079529, Hundredth);
        }

        [Fact]
        public void AHalfTheBoxRunsInto_HasTheBoxCutOutOfIt_AtAHundredth()
        {
            // Cut in at once, the half held 1 465.33 with the box in it, and came back as its material; the box is taken out
            // one at a time now, as GetVolume takes it out.
            GeoSolid3 below = HalfTheBoxRunsInto(out _);

            Assert.True(below.TryCutOpenings(out GeoSolid3 material, Hundredth));
            Assert.Empty(material.Openings);
            double bound = Hundredth.EqualPoint * material.GetSurfaceArea(Hundredth);
            Assert.InRange(material.GetVolume(Hundredth), 1434.7733828079529 - bound, 1434.7733828079529 + bound);
        }

        [Fact]
        public void ABooleanOfAHalfTheBoxRunsInto_WarnsOfNothingItWorksOut()
        {
            // The half less a cube 4 across centred on the box: the boolean reads the half as it read it before, and no cut
            // of its openings is warned of as one that could not be worked out.
            GeoSolid3 below = HalfTheBoxRunsInto(out GeoSolid3 box);
            GeoPoint3 middle = box.GetAabb().Center;
            GeoSolid3 cube = new GeoAabb3(P(middle.X - 2, middle.Y - 2, middle.Z - 2), P(middle.X + 2, middle.Y + 2, middle.Z + 2)).ToObb().ToSolid();
            var warnings = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> writer = GeometryHelperLog.Writer;
            bool enable = GeometryHelperLog.Enable;
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (level >= GeometryHelperLogLevel.Warn)
                {
                    warnings.Add(message);
                }
            };

            try
            {
                below.TrySubtract(cube, out _, Hundredth);
            }
            finally
            {
                GeometryHelperLog.Writer = writer;
                GeometryHelperLog.Enable = enable;
            }

            Assert.Empty(warnings);
        }

        [Fact]
        public void AHalfWhoseBoxWillNotComeOut_IsReadAsCutInAtOnce()
        {
            // A hull of 14 points, far from the origin, cut within a hundredth by a plane 0.012 inside a face and tilted 0.0028
            // off it; a box 1.4 by 1.4 by 3.9, turned to the face, stands through it. A piece of the half below is left part
            // in the box and part out, and taken out one at a time the box will not come out closed: the surface is the cut at
            // once, as it was, where nothing was read and the surface had no triangle.
            GeoSolid3 hull = ConvexHull3.Of(new[]
            {
                P(123463.04048709475, -65425.482822407124, 9882.646830495516), P(123446.70520136207, -65423.390273406214, 9870.520939229065),
                P(123454.23319631681, -65432.317147297326, 9866.824346823769), P(123461.10587221384, -65434.19744357136, 9878.840615667561),
                P(123459.36320159317, -65424.79118494479, 9882.825792221503), P(123460.81674176069, -65441.791860098245, 9873.020806283932),
                P(123463.99965729513, -65441.94022653654, 9882.076530031663), P(123448.30768576972, -65437.92625271558, 9886.0837865116),
                P(123464.44255487031, -65433.02419872569, 9874.767052447547), P(123464.69208852369, -65436.344557495344, 9874.423587024176),
                P(123454.99325667968, -65432.67136377812, 9877.33730910478), P(123449.60511155636, -65439.76483546126, 9878.07254666629),
                P(123446.98026039725, -65426.66355527256, 9878.491548525166), P(123456.73283789848, -65431.26092865595, 9885.184553372992),
            }, Tolerance.Default);
            GeoSolid3 box = new GeoObb3(
                P(123449.00019122043, -65424.180119459794, 9871.62368868115), 1.3513336350914713, 1.3513336350914713, 3.9164179018309424,
                new GeoVector3(-0.7987388993542848, 0.10231839915037239, -0.5929140880883258),
                new GeoVector3(-0.34052350299910444, 0.7355531021210485, 0.585666609826206)).ToSolid();
            var plane = new GeoPlane3(
                P(123446.69917580973, -65423.3984083407, 9870.52765266302),
                new GeoVector3(0.49382737436589813, 0.6699770499917368, -0.5543151421450194));

            Assert.True(hull.WithOpenings(new[] { box }).TrySplitBy(plane, out _, out GeoSolid3 below, Hundredth));

            Assert.False(below.TryGetVolume(out _, Hundredth));
            Assert.False(below.TryCutOpenings(out _, Hundredth));
            Assert.NotEmpty(below.TriangulateSurface(Hundredth));
        }

        [Fact]
        public void APieceTwoPlanesOfABoxRunThroughUncut_IsReadAsPartInTheBox()
        {
            // A cube 10 across under the corner of a box lying along an edge of it, x and z from 8 to 12: neither plane, z = 8
            // nor x = 8, cuts the cube. Each alone leaves both its parts out of the box, as the middle of each places them; of
            // the four the two make, one, 2 by 10 by 2, is in it.
            Tolerance tolerance = Tolerance.Default;
            GeoSolid3 cube = new GeoAabb3(P(0, 0, 0), P(10, 10, 10)).ToObb().ToSolid().TurnOutwards();
            GeoSolid3 box = new GeoAabb3(P(8, -1, 8), P(12, 11, 12)).ToObb().ToSolid().TurnOutwards();
            var floor = new GeoPlane3(P(0, 0, 8), new GeoVector3(0, 0, 1));
            var wall = new GeoPlane3(P(8, 0, 0), new GeoVector3(1, 0, 0));
            var pieces = new List<GeoSolid3> { cube };

            Assert.False(Boolean3.JudgedWhole(pieces, new List<(GeoPlane3, GeoSolid3)> { (floor, box) }, tolerance));
            Assert.False(Boolean3.JudgedWhole(pieces, new List<(GeoPlane3, GeoSolid3)> { (wall, box) }, tolerance));
            Assert.True(Boolean3.JudgedWhole(pieces, new List<(GeoPlane3, GeoSolid3)> { (floor, box), (wall, box) }, tolerance));
        }

        [Fact]
        public void APieceASlotRunsThroughBetweenTwoUncutPlanes_IsReadAsPartInIt()
        {
            // A cube 10 across with a slot z from 4 to 6 running through it in x and y: both its planes are uncut, and each
            // alone leaves the middles of both its parts out of the slot; the slab between them is in it.
            Tolerance tolerance = Tolerance.Default;
            GeoSolid3 cube = new GeoAabb3(P(0, 0, 0), P(10, 10, 10)).ToObb().ToSolid().TurnOutwards();
            GeoSolid3 slot = new GeoAabb3(P(-1, -1, 4), P(11, 11, 6)).ToObb().ToSolid().TurnOutwards();
            var under = new GeoPlane3(P(0, 0, 4), new GeoVector3(0, 0, -1));
            var over = new GeoPlane3(P(0, 0, 6), new GeoVector3(0, 0, 1));
            var pieces = new List<GeoSolid3> { cube };

            Assert.False(Boolean3.JudgedWhole(pieces, new List<(GeoPlane3, GeoSolid3)> { (under, slot) }, tolerance));
            Assert.True(Boolean3.JudgedWhole(pieces, new List<(GeoPlane3, GeoSolid3)> { (under, slot), (over, slot) }, tolerance));
        }

        [Fact]
        public void AHalfTheBoxRunsInto_KeepsTheMaterialBesideTheBox()
        {
            // A hull of 14 points, far from the origin, cut by a plane 0.0016 inside a face and tilted 1.5E-6 off it; an
            // upright box 2.4 by 2.4 by 4.7 stands through it. The half below held 854.10, 2.09 of its material taken with
            // the box.
            Tolerance tolerance = Tolerance.Default;
            GeoSolid3 hull = ConvexHull3.Of(new[]
            {
                P(123459.82926521204, -65429.78979937196, 9879.844349606588), P(123458.20507322116, -65433.04058656643, 9882.025316715019),
                P(123454.60477859224, -65440.05806918198, 9877.26735865826), P(123449.12868071535, -65436.37228643292, 9876.35823774238),
                P(123461.85596586985, -65441.93975383446, 9878.01144455723), P(123462.44073969188, -65441.66948389279, 9874.29209960615),
                P(123460.21157162036, -65432.40413752901, 9882.698937872517), P(123459.74239939573, -65430.100541640444, 9871.973640917555),
                P(123462.62965416421, -65438.406641956935, 9880.881120714537), P(123461.51582007129, -65438.27884415955, 9870.877633800907),
                P(123457.06006497702, -65433.44004240452, 9868.418780348225), P(123463.89717846121, -65433.53479034837, 9873.25694131607),
                P(123463.66893691875, -65438.6325438867, 9884.282725215788), P(123459.86727596016, -65429.92928374029, 9866.503592474388),
            }, Tolerance.Default);
            GeoSolid3 box = new GeoObb3(
                P(123463.12047205775, -65437.499755788944, 9883.830083305887), 2.4078152726440765, 2.4078152726440765, 4.670803926731834,
                GeoVector3.XAxis, GeoVector3.YAxis).ToSolid();
            var plane = new GeoPlane3(
                P(123463.66770733632, -65438.63337904316, 9884.282125022692),
                new GeoVector3(0.7670540363280667, 0.5209964643952526, 0.37442060498918517));

            GeoSolid3 body = hull.WithOpenings(new[] { box });

            Assert.True(body.TrySplitBy(plane, out _, out GeoSolid3 below, tolerance));
            AssertHolds(below, 856.188392620331, tolerance);
        }

        [Fact]
        public void APlaneOfTheBoxRunningUncutThroughMaterialClearOfIt_LeavesTheCutAsItWas()
        {
            // The plane 0.002 inside a face and tilted 8E-6 off it. A plane of the box runs uncut through a cell of the half
            // below, the parts either side both clear of the box: the cell is judged as before, and the box cut in at once.
            // Taken out by a difference instead, the box would not come out closed, and the half would hold 1 680.68 with the
            // box left in.
            Tolerance tolerance = Tolerance.Default;
            GeoSolid3 hull = ConvexHull3.Of(new[]
            {
                P(123458.87716021099, -65433.243173049734, 9870.259029136858), P(123459.15561482034, -65426.612779472634, 9868.94295311274),
                P(123451.45659759936, -65427.110305384645, 9885.033701616589), P(123462.89170137503, -65440.21210527928, 9885.660491898265),
                P(123449.94440777453, -65436.17151145585, 9880.202886865336), P(123459.57802950147, -65426.2020930205, 9866.660685889497),
                P(123448.129421148, -65433.675093526195, 9883.77080836765), P(123448.28541842438, -65433.43546979694, 9881.766348670826),
                P(123451.89362826142, -65422.3014307213, 9870.603304103064), P(123455.33174019783, -65426.219881887046, 9866.563447216555),
                P(123461.87907232753, -65433.49035109961, 9884.746085018965), P(123452.11381011968, -65430.60913072401, 9872.430245605265),
                P(123451.59166667913, -65440.02961646706, 9876.864129371179), P(123462.81908134824, -65428.80543776206, 9868.462661827896),
            }, Tolerance.Default);
            GeoSolid3 box = new GeoObb3(
                P(123449.40746425095, -65430.442139998806, 9881.625302365483), 4.228835621489601, 4.228835621489601, 3.444083287587428,
                GeoVector3.XAxis, GeoVector3.YAxis).ToSolid();
            var plane = new GeoPlane3(
                P(123448.13120571927, -65433.67595326651, 9883.770575908977),
                new GeoVector3(-0.8947662309656179, 0.43105609577185106, 0.11655056508447897));

            GeoSolid3 body = hull.WithOpenings(new[] { box });

            Assert.True(body.TrySplitBy(plane, out _, out GeoSolid3 below, tolerance));
            AssertHolds(below, 1650.3977067818385, tolerance);
        }
    }
}
