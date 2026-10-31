using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A body whose openings were cut in at once before, as a boolean cuts them, keeps that cut, and a boolean that asks for
    /// it again within the same tolerance reads it rather than cutting again: what a boolean gives of it is what it gives of
    /// a body never cut, face for face and corner for corner.
    /// </summary>
    public class BooleanOpeningsG1u6Tests
    {
        private static GeoPoint3 At(double far, double x, double y, double z) => new GeoPoint3(far + x, far + y, far + z);

        private static GeoSolid3 Box(double far, double x0, double y0, double z0, double x1, double y1, double z1)
            => GeoSolid3.Extrude(
                new GeoPolygon3(At(far, x0, y0, z0), At(far, x1, y0, z0), At(far, x1, y1, z0), At(far, x0, y1, z0)), new GeoVector3(0, 0, z1 - z0), Tolerance.Default);

        /// <summary>
        /// A plate 200 by 100 by 3 with a blind opening 38.6 by 60.7 up from its floor to 1.772, and one 38.9 by 60 to
        /// 1.77105: their ceilings stand 0.00095 apart, the openings 31.5 apart along them.
        /// </summary>
        private static GeoSolid3 Plate(double far)
            => Box(far, 0, 0, 0, 200, 100, 3).WithOpenings(new[] { Box(far, 13.5, 22.5, -7, 52.1, 83.2, 1.772), Box(far, 83.6, 24.9, -7, 122.5, 84.9, 1.77105) });

        /// <summary>A box 195.8 by 70.8 by 3.3 over both openings, standing out of the plate's top, end and side.</summary>
        private static GeoSolid3 Tool(double far) => Box(far, 8.2, 32.7, 0.3, 204, 103.5, 3.6);

        private static Tolerance Within(double tolerance) => new Tolerance(tolerance, 1E-5, Math.PI / 180, tolerance);

        /// <summary>The plate at the origin and a hundred thousand out, within a ten-thousandth to a thousand-millionth.</summary>
        public static TheoryData<double, double> Finer()
        {
            var cases = new TheoryData<double, double>();

            foreach (double far in new[] { 0.0, 1E5 })
            {
                foreach (double tolerance in new[] { 1E-4, 1E-5, 1E-6, 1E-9 })
                {
                    cases.Add(far, tolerance);
                }
            }

            return cases;
        }

        [Theory]
        [MemberData(nameof(Finer))]
        public void TheFourBooleansOfThePlateCutBefore_AreThoseOfThePlateNeverCut(double far, double tolerance)
        {
            // The plate whose openings were cut in at once within the tolerance of the work is not cut again: each of the
            // four booleans gives what it gives of a plate never cut, face for face and corner for corner.
            GeoSolid3 cutBefore = Plate(far);
            Assert.True(Boolean3.TryCutOpeningsAtOnce(cutBefore, cutBefore.Openings, out _, Boolean3.ForWork(Within(tolerance)), out _, out _));

            foreach (string operation in new[] { "union", "common part", "difference", "the box less" })
            {
                GeoSolid3 before = Work(operation, cutBefore, Tool(far), Within(tolerance));
                GeoSolid3 never = Work(operation, Plate(far), Tool(far), Within(tolerance));

                Assert.Equal(Corners(never), Corners(before));
            }
        }

        private static GeoSolid3 Work(string operation, GeoSolid3 plate, GeoSolid3 box, Tolerance tolerance)
        {
            GeoSolid3 result;
            BooleanOutcome outcome;

            switch (operation)
            {
                case "union":
                    Assert.True(plate.TryUnion(box, out result, tolerance, out outcome));
                    break;
                case "common part":
                    Assert.True(plate.TryIntersect(box, out result, tolerance, out outcome));
                    break;
                case "difference":
                    Assert.True(plate.TrySubtract(box, out result, tolerance, out outcome));
                    break;
                default:
                    Assert.True(box.TrySubtract(plate, out result, tolerance, out outcome));
                    break;
            }

            Assert.Equal(BooleanOutcome.Made, outcome);
            return result;
        }

        private static double[] Corners(GeoSolid3 solid)
            => solid.Faces.SelectMany(face => face.Boundary.Vertices.Concat(face.Holes.SelectMany(hole => hole.Vertices)))
                .SelectMany(corner => new[] { corner.X, corner.Y, corner.Z }).ToArray();
    }
}
