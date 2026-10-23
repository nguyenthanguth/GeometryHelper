using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A plane a hair over the tolerance inside a face, and all but parallel to it, cuts off a slab little thicker than the
    /// tolerance, every edge down its sides about the tolerance long. Cut within the tolerance, the slab came out open to
    /// Validate on 79 of 3 000 hulls; a body that closes now comes apart into two halves that close, or not at all.
    /// </summary>
    public class SplitNearAFaceTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        [Fact]
        public void AHullCutAHairOverTheToleranceInsideAFace_ComesApartIntoTwoClosedHalves()
        {
            // A hull of 14 points 20 across, far from the origin; the plane stands 0.0011 inside a face, tilted 1.2E-6 off it.
            // The slab above came out open by an edge 0.0011 long; cut again within a tenth of the tolerance, it closes.
            GeoSolid3 hull = ConvexHull3.Of(new[]
            {
                P(123448.93557929613, -65427.77540879625, 9866.7040191927), P(123446.91883861172, -65434.60227165059, 9873.197804996138),
                P(123447.30449964394, -65425.615651963424, 9868.346838482585), P(123451.3473508443, -65422.71292487225, 9884.610735685616),
                P(123456.42943959279, -65424.68414144748, 9867.634261750212), P(123465.69036786472, -65428.787788631154, 9880.387609892472),
                P(123459.38567120316, -65431.21127062007, 9882.39483936126), P(123450.21320553734, -65426.77296587521, 9882.644679084488),
                P(123462.54784781366, -65425.98547150599, 9876.936067083123), P(123460.03486885453, -65426.48150408881, 9870.10279251989),
                P(123462.38087882161, -65424.92953760718, 9879.076405924083), P(123449.61834302382, -65435.97931704236, 9871.189848788405),
                P(123451.40878027599, -65436.65317930064, 9874.89989791084), P(123452.55289565187, -65439.45400633763, 9883.183367843127),
            }, Tolerance);
            var plane = new GeoPlane3(
                P(123465.68994821994, -65428.787537903314, 9880.386594513484),
                new GeoVector3(0.37238139560221806, -0.222488001211834, 0.9010167509686527));

            Assert.True(hull.Validate(Tolerance).IsClosed);
            Assert.True(hull.TrySplitBy(plane, out GeoSolid3 above, out GeoSolid3 below, Tolerance));

            Assert.True(above.Validate(Tolerance).IsClosed, above.Validate(Tolerance).ToString());
            Assert.True(below.Validate(Tolerance).IsClosed, below.Validate(Tolerance).ToString());
            Assert.Equal(0.022, above.GetVolume(Tolerance), 3);

            Assert.Equal(hull.GetVolume(Tolerance), above.GetVolume(Tolerance) + below.GetVolume(Tolerance), 6);
        }

        [Fact]
        public void ABoxCutWithNoTolerance_ComesApartAsCut_WithoutTryingToCloseIt()
        {
            // With no tolerance a finer cut is the same cut and a weld widened from nought stays nought: trying to close the
            // halves so ran for ever. They come back as cut, as before the closing was added.
            var none = new Tolerance(0.0, 0.0, 0.0, 0.0);
            GeoSolid3 box = new GeoObb3(P(0, 0, 0), 30, 20, 10).ToSolid();
            var plane = new GeoPlane3(P(-15, -10, -5), new GeoVector3(0.5378474378628374, -0.34175691089780075, -0.7706635760420847));

            Assert.True(box.TrySplitBy(plane, out GeoSolid3 above, out GeoSolid3 below, none));

            double[] volumes = { above.GrossVolume, below.GrossVolume };
            Array.Sort(volumes);
            Assert.Equal(2703.698661493917, volumes[0], 6);
            Assert.Equal(3296.301338506083, volumes[1], 6);
        }

        [Fact]
        public void AHullWhoseSlabNoWeldCloses_IsCutAgainFinerAndItsHalvesAddUp()
        {
            // A hull of 11 points; the plane stands 0.0012 inside a face. The slab above came out open by three edges, and
            // welded it does not close either: cut again within a tenth of the tolerance, the halves close and hold the hull.
            GeoSolid3 hull = ConvexHull3.Of(new[]
            {
                P(123466.35107769689, -65439.84778845615, 9881.225761709142), P(123458.83200631184, -65422.21999653658, 9867.20676610838),
                P(123454.3464701852, -65429.02676724257, 9884.140852982942), P(123450.62119465577, -65427.43994819286, 9885.90078450339),
                P(123449.207475327, -65434.3299505734, 9883.864037687594), P(123455.42764516096, -65439.79162513208, 9875.49209777312),
                P(123448.26186134627, -65432.55635106529, 9874.310427447692), P(123449.90734684505, -65429.364993790194, 9883.800202472741),
                P(123451.92975592185, -65428.173890070466, 9872.34032622438), P(123464.606003789, -65433.03205032448, 9873.759206588966),
                P(123455.29785868255, -65437.83726621258, 9870.565672347353),
            }, Tolerance);
            var plane = new GeoPlane3(
                P(123458.83138228966, -65422.22088299812, 9867.20624449688),
                new GeoVector3(0.518707499635251, 0.7368544622417991, 0.43357586567577217));

            Assert.True(hull.TrySplitBy(plane, out GeoSolid3 above, out GeoSolid3 below, Tolerance));

            Assert.True(above.Validate(Tolerance).IsClosed, above.Validate(Tolerance).ToString());
            Assert.True(below.Validate(Tolerance).IsClosed, below.Validate(Tolerance).ToString());
            Assert.Equal(0.0433, above.GetVolume(Tolerance), 4);
            Assert.Equal(hull.GetVolume(Tolerance), above.GetVolume(Tolerance) + below.GetVolume(Tolerance), 6);
        }

        [Fact]
        public void RandomHullsCutNearAFace_NeverGiveAnOpenHalf()
        {
            // 600 hulls of 14 random points 20 across, half of them far from the origin, each cut by a plane through a corner
            // of one of its faces, up to 0.002 off the face and tilted off it by 1E-6 to 1E-2. Fifteen gave an open half.
            var random = new Random(17);
            int made = 0;

            for (int i = 0; i < 600; i++)
            {
                var points = new List<GeoPoint3>();

                for (int k = 0; k < 14; k++)
                {
                    points.Add(P(random.NextDouble() * 20 - 10, random.NextDouble() * 20 - 10, random.NextDouble() * 20 - 10));
                }

                GeoSolid3 hull = ConvexHull3.Of(points, Tolerance);

                if (random.Next(2) == 0)
                {
                    hull = hull.TransformBy(GeoTransform3.Translation(new GeoVector3(123456.7, -65432.1, 9876.5)));
                }

                GeoFace3 face = hull.Faces[random.Next(hull.Faces.Count)];
                IReadOnlyList<GeoPoint3> corners = face.Boundary.Vertices;
                GeoVector3 normal = face.Boundary.Normal;
                double tilt = Math.Pow(10, -6 + 4 * random.NextDouble());
                GeoVector3 side = corners[1].GetVectorTo(corners[0]).Normalize();
                GeoVector3 tilted = new GeoVector3(normal.X + side.X * tilt, normal.Y + side.Y * tilt, normal.Z + side.Z * tilt).Normalize();
                double shift = (random.NextDouble() - 0.5) * 4E-3;
                GeoPoint3 origin = P(corners[0].X + normal.X * shift, corners[0].Y + normal.Y * shift, corners[0].Z + normal.Z * shift);

                if (!hull.TrySplitBy(new GeoPlane3(origin, tilted), out GeoSolid3 above, out GeoSolid3 below, Tolerance))
                {
                    continue;
                }

                made++;
                Assert.True(above.Validate(Tolerance).IsClosed, $"hull {i}: above {above.Validate(Tolerance)}");
                Assert.True(below.Validate(Tolerance).IsClosed, $"hull {i}: below {below.Validate(Tolerance)}");
            }

            Assert.Equal(154, made);
        }
    }
}
