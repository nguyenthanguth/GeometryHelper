using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The exact gap in space for shapes that are thin, far from the origin, or a hair off flat: a triangle with no area
    /// touches nothing, a sliver is measured to where it is and not to where its cancelling arithmetic puts it, a point on
    /// the rim of a polygon a few ten-thousandths out of flat is on it, a face far from the origin is flat where it is, and
    /// a collision and a distance never disagree about a pair standing the tolerance apart. The scenes are from a fuzz of
    /// 135 715, judged against a 40-digit oracle; each spec is a shape written as hexadecimal doubles.
    /// </summary>
    public class Collision3ThinShapesTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static double H(string hex) => BitConverter.Int64BitsToDouble(Convert.ToInt64(hex, 16));

        private static GeoPoint3 P(double[] v, int i) => new GeoPoint3(v[i], v[i + 1], v[i + 2]);

        private static List<GeoPolygon3> Loops(string[] tok, ref int i, int count, Tolerance tolerance)
        {
            var loops = new List<GeoPolygon3>();

            for (int l = 0; l < count; l++)
            {
                int k = int.Parse(tok[i], CultureInfo.InvariantCulture);
                double[] vals = tok.Skip(i + 1).Take(3 * k).Select(H).ToArray();
                loops.Add(new GeoPolygon3(Enumerable.Range(0, k).Select(j => P(vals, 3 * j)).ToArray(), tolerance));
                i += 1 + 3 * k;
            }

            return loops;
        }

        /// <summary>A shape from its spec: PT, LN, RY, TR with their numbers, or FC with its loops.</summary>
        private static object Build(string spec, Tolerance tolerance)
        {
            string[] tok = spec.Split(' ');

            if (tok[0] == "FC")
            {
                int i = 2;
                List<GeoPolygon3> loops = Loops(tok, ref i, int.Parse(tok[1], CultureInfo.InvariantCulture), tolerance);

                return new GeoFace3(loops[0], loops.Skip(1));
            }

            double[] v = tok.Skip(1).Select(H).ToArray();

            switch (tok[0])
            {
                case "PT": return P(v, 0);
                case "LN": return new GeoLine3(P(v, 0), P(v, 3));
                case "RY": return new GeoRay3(P(v, 0), new GeoVector3(v[3], v[4], v[5]));
                default: return new GeoTriangle3(P(v, 0), P(v, 3), P(v, 6));
            }
        }

        private static double Distance(dynamic a, dynamic b, Tolerance tolerance) => (double)a.DistanceTo(b, tolerance);

        private static bool Collides(dynamic a, dynamic b, Tolerance tolerance) => (bool)a.CollidesWith(b, tolerance);

        #region A triangle with no area touches nothing

        public static IEnumerable<object[]> Partners() => new[]
        {
            "segment", "ray", "polygon", "face", "triangle", "oriented box", "aligned box", "solid",
        }.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(Partners))]
        public void ATriangleWithNoAreaCollidesWithNothing_AndKeepsItsDistance(string partner)
        {
            // 100 long and 1E-8 wide: its area vector is 5E-7, below the vector tolerance of 1E-5.
            var sliver = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 1E-8, 0));
            Assert.True(sliver.IsDegenerate(Tol));

            // Each partner passes through the middle of the sliver, so that anything with an area would collide.
            object other;

            switch (partner)
            {
                case "segment": other = new GeoLine3(new GeoPoint3(50, 0, -1), new GeoPoint3(50, 0, 1)); break;
                case "ray": other = new GeoRay3(new GeoPoint3(50, 0, -1), new GeoVector3(0, 0, 1)); break;
                case "polygon": other = new GeoPolygon3(new GeoPoint3(40, -10, 0), new GeoPoint3(60, -10, 0), new GeoPoint3(60, 10, 0), new GeoPoint3(40, 10, 0)); break;
                case "face": other = new GeoFace3(new GeoPolygon3(new GeoPoint3(40, -10, 0), new GeoPoint3(60, -10, 0), new GeoPoint3(60, 10, 0), new GeoPoint3(40, 10, 0))); break;
                case "triangle": other = new GeoTriangle3(new GeoPoint3(40, -5, 0), new GeoPoint3(60, -5, 0), new GeoPoint3(50, 5, 0)); break;
                case "oriented box": other = new GeoObb3(new GeoPoint3(50, 0, 0), 20, 10, 10); break;
                case "aligned box": other = new GeoAabb3(new GeoPoint3(40, -5, -5), new GeoPoint3(60, 5, 5)); break;
                default: other = new GeoObb3(new GeoPoint3(50, 0, 0), 20, 10, 10).ToSolid(); break;
            }

            Assert.False(Collides(sliver, other, Tol));
            Assert.False(Collides(other, sliver, Tol));
            Assert.Equal(0.0, Distance(sliver, other, Tol), 12);
            Assert.Equal(0.0, Distance(other, sliver, Tol), 12);
        }

        #endregion

        #region Slivers are measured where they are

        // Specs of a pair, the gap worked at 40 digits, the largest coordinate, the tolerance: a segment 1.9E-6 off a sliver\n        // triangle 90 000 from the origin where they meet (scene 26560), another (54475), a ray at a hundredth (25342), and two sliver\n        // triangles 3.0271528E-5 apart which the cancelling arithmetic put 8E-9 further (8239).
        public static IEnumerable<object[]> Slivers() => new[]
        {
            // A segment 1.9E-6 off a sliver triangle 90 000 from the origin, where they meet.
            new object[]
            {
                "TR c0a7c8d93ba827f5 c0e7c529a0b905a9 40f637246cc80410 c0a7e75683b5a3aa c0e7bef2e9d91ff3 40f631cdb58158c2 c0a7d817e8d145c8 c0e7c20e44d3e32c 40f634791153d21f",
                "LN c0a7df3015e3371a c0e7c09c209c9452 40f6333b0bc5e56d c0a7dde9455058ca c0e7c0d60c74e198 40f6332da923efe1",
                0.0, 91144.2765579375, 0.001,
            },
            new object[]
            {
                "TR 406941d1ea717ee9 c087ad159bb0492e c089807221b3b46e 4065d01c6150e546 c0880ebc24d4d815 c08685a342795e3d 406788f791257261 c087dde8b5344d79 c088030aa4d3bde3",
                "LN 40678e5d82bcc160 c087dd4fa8be5e8e c08807b63d920c9e 40678763ac40f655 c087e80930273910 c08810b72827b214",
                1.1233721150860678E-14, 966.05572834391, 0.001,
            },
            // A ray and a sliver at a tolerance of a hundredth.
            new object[]
            {
                "TR c0272613c63ab184 c060721a81010e28 407aba07b426feba c0517520ae8ff152 c067c2afcd63364c 4077337fb8511eb6 c04459e37c9ab676 c0641a648bcb7aa1 4078f6c370a27642",
                "RY c04af8c90b48e77b c065c406fb976d71 4078299ef0763f7a 3fe692a5f533dee6 3fe1100179ebb0ab 3fdde390c68936b0",
                0.0, 577.6268807910197, 0.01,
            },
            // Two sliver triangles 3.0271528E-5 apart, which the cancelling arithmetic put 8E-9 further.
            new object[]
            {
                "TR 40835c18d7dd1cc7 408123ef161bded6 c0884ecef0dd775c 408472fa437a82ce 407f3ca08d4e04f1 c08acf732ebf94e9 4083e789b681536a 4080611fcf313e78 c0898f2111fc7c14",
                "TR 4083556c02f56e94 4080415ad9260054 c0892f7b086a5ace 408366282a9741c2 4082794d56495e5f c086fc508c59bc34 40835dc85794c852 40815d535dc789f7 c08815e50193da51",
                3.0271528373746698E-05, 927.851045351224, 0.001,
            },
        };

        [Theory]
        [MemberData(nameof(Slivers))]
        public void ASliverIsAsFarAsItTrulyIs_FromEitherSide(string specA, string specB, double gap, double size, double tolerance)
        {
            var t = new Tolerance(tolerance, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, tolerance);
            object a = Build(specA, t);
            object b = Build(specB, t);

            // As the oracle judges: shapes that meet are read as meeting within 1E-11 of the size of the scene, the rest to a billionth
            // of the gap or the rounding of a coordinate of this size.
            double allowance = gap <= 1E-12 * size ? 1E-11 * size : Math.Max(1E-9 * gap, 4E-14 * size);

            Assert.InRange(Distance(a, b, t), Math.Max(0.0, gap - allowance), gap + allowance);
            Assert.InRange(Distance(b, a, t), Math.Max(0.0, gap - allowance), gap + allowance);
            Assert.Equal(gap <= t.EqualPoint, Collides(a, b, t));
            Assert.Equal(gap <= t.EqualPoint, Collides(b, a, t));
        }

        #endregion

        #region A point against a sliver triangle

        // A triangle, a point on it (or 1.5E-12 from it), the gap, the largest coordinate: a sliver 1E-4 wide and 100 long (scene
        // 8205, where the closest-point search put the point 5.6E-4 off) and a sliver 90 000 from the origin (scene 26560, where it
        // put its start point 2E-4 off).
        public static IEnumerable<object[]> PointsOnSlivers() => new[]
        {
            new object[]
            {
                "TR 0000000000000000 0000000000000000 0000000000000000 4059000000000000 0000000000000000 0000000000000000 4049000000000000 3f1a36e2eb1c432d 0000000000000000",
                "PT 405382f55a363c73 3f06a2ccf190d2ab 0000000000000000", 0.0, 150.0,
            },
            new object[]
            {
                "TR c0a7c8d93ba827f5 c0e7c529a0b905a9 40f637246cc80410 c0a7e75683b5a3aa c0e7bef2e9d91ff3 40f631cdb58158c2 c0a7d817e8d145c8 c0e7c20e44d3e32c 40f634791153d21f",
                "PT c0a7df3015e3371a c0e7c09c209c9452 40f6333b0bc5e56d", 1.5322194620335233E-12, 91144.2765579375,
            },
        };

        [Theory]
        [MemberData(nameof(PointsOnSlivers))]
        public void APointOnASliverTriangleIsOnIt_FromEitherSide(string triangleSpec, string pointSpec, double gap, double size)
        {
            var triangle = (GeoTriangle3)Build(triangleSpec, Tol);
            var point = (GeoPoint3)Build(pointSpec, Tol);
            double allowance = gap + 1E-11 * size;

            Assert.InRange(triangle.DistanceTo(point), 0.0, allowance);
            Assert.InRange(point.DistanceTo(triangle), 0.0, allowance);
            Assert.InRange(Distance3.DistanceTo(triangle, point), 0.0, allowance);
        }

        #endregion
        #region A polygon a hair off flat holds its own rim

        [Fact]
        public void APointOnTheRimOfAPolygonFourTenThousandthsOffFlat_IsOnIt()
        {
            // The corner (100, 100) stands 4E-4 over the plane through the others, so the side x = 100 climbs from 0 to 4E-4
            // and its middle (100, 50, 2E-4) is on the polygon, though its foot on the flat reading lands inside it.
            var polygon = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 4E-4), new GeoPoint3(0, 100, 0));
            var onTheRim = new GeoPoint3(100, 50, 2E-4);

            Assert.Equal(0.0, Distance3.DistanceTo(polygon, onTheRim, Tol), 12);
            Assert.Equal(0.0, Distance3.DistanceTo(new GeoFace3(polygon), onTheRim, Tol), 12);
        }

        [Fact]
        public void APointOnTheRimOfAPolygonOneTenThousandthOffFlat_IsOnIt()
        {
            var polygon = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 1E-4), new GeoPoint3(0, 100, 0));
            var onTheRim = new GeoPoint3(50, 100, 5E-5);

            Assert.Equal(0.0, Distance3.DistanceTo(polygon, onTheRim, Tol), 12);
        }

        #endregion

        #region A face far from the origin is flat where it is

        [Fact]
        public void APointOnAFaceFarFromTheOrigin_IsAsFarAsItTrulyIs()
        {
            // Scene 14503: a sliver face near (22156, 80746, -42158) and a point 4.8E-12 from it. Its normal, summed from
            // the coordinates as they stand, lost its digits to cancellation and tilted the face by 1.3E-6.
            var t = Tol;
            var face = (GeoFace3)Build(
                "FC 1 3 40d5a9b5d82255ee 40f3b5e8aa0d809a c0e491fbf2cfe199 40d599f6714b40d4 40f3b7a2ee0fe25f c0e49b0e91054346 40d5a1d623790eca 40f3b6c5cbe226be c0e4968541829f86", t);
            var point = (GeoPoint3)Build("PT 40d5a3242a23525f 40f3b6a126f8c009 c0e495c4d04c4d25", t);
            double allowance = 4E-14 * 80884.5415167831;

            Assert.InRange(Distance3.DistanceTo(face, point, t), 0.0, 4.810894936840392E-12 + allowance);
            Assert.InRange(point.DistanceTo(face, t), 0.0, 4.810894936840392E-12 + allowance);
        }

        #endregion

        #region A collision and a distance agree at the tolerance

        [Fact]
        public void TwoFacesStandingExactlyTheToleranceApart_CollideAsTheirDistanceSays()
        {
            // Scene 33668: a face with a hole and a hexagon a thousandth apart. The distance reads 0.00099999999999718 and the
            // collision, deciding by its own pruned search, said no.
            object a = Build(
                "FC 2 4 408eee8ca8032a1e 407e4e20e9ae2076 c0646efb899f3bab 409107465401950f 407e4e20e9ae2076 c0646efb899f3bab 409107465401950f 4082471074d7103b c0646efb899f3bab 408eee8ca8032a1e 4082471074d7103b c0646efb899f3bab 4 408fde8ca8032a1e 4080171074d7103b c0646efb899f3bab 408fde8ca8032a1e 4081571074d7103b c0646efb899f3bab 40908f465401950f 4081571074d7103b c0646efb899f3bab 40908f465401950f 4080171074d7103b c0646efb899f3bab", Tol);
            object b = Build(
                "FC 1 6 4090d68dcef43341 40816c02a0c78282 c0646f03bac62524 40903753273eaa63 407d1c4120f84e3c c0646f03bac62524 4090ca19f74dbcab 407c1d7ce16f7340 c0646f03bac62524 409129a38f210ec9 407f8e25c1c9e0ec c0646f03bac62524 409205cdc737aa34 407e0fff627c9873 c0646f03bac62524 4092457ed719e0f3 40802d8d515c70c8 c0646f03bac62524", Tol);

            double ab = Distance(a, b, Tol);
            double ba = Distance(b, a, Tol);

            Assert.Equal(ab <= Tol.EqualPoint, Collides(a, b, Tol));
            Assert.Equal(ba <= Tol.EqualPoint, Collides(b, a, Tol));
        }

        #endregion
    }
}
