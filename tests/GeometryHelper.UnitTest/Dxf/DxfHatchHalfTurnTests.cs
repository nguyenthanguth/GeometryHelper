using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The pattern angle (52) of a hatch turned by half a circle in its own plane, which AutoCAD 2021 writes as 30 plus 180 or
    /// 30 minus 180 by the sign of a nought in its sums (probed in its console on 14 normals under 117 turns about x, y and z
    /// of a user pattern at 30 degrees: 198 half turns, 210 where AutoCAD wrote a plus and -150 where it wrote a minus).
    /// </summary>
    public class DxfHatchHalfTurnTests
    {
        private static DxfHatch UserHatch(GeoVector3 normal)
        {
            var database = new DxfDatabase("AC1027");
            var hatch = new DxfHatch { Normal = normal.Normalize() };
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = 30.0 * Math.PI / 180.0;
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            hatch.AppendSeedPoint(new GeoPoint2(45, 52));
            database.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        private static double Degrees(DxfHatch hatch) => hatch.PatternAngle * 180.0 / Math.PI;

        [Fact]
        public void AHatchOfANormalSlantingDownAndBack_TurnedAboutXTo45Degrees_HasItsAngleTurnedPlusAHalfCircle()
        {
            DxfHatch hatch = UserHatch(new GeoVector3(0, -1, -1));

            hatch.TransformBy(GeoTransform3.RotationX(45.0 * Math.PI / 180.0));

            // AutoCAD 2021: 210, where the library wrote -150 for any normal pointing down before and after.
            Assert.Equal(210.0, Degrees(hatch), 6);
        }

        [Fact]
        public void AHatchOfANormalAlongMinusX_TurnedAboutYTo100Degrees_HasItsAngleTurnedPlusAHalfCircle()
        {
            DxfHatch hatch = UserHatch(new GeoVector3(-1, 0, 0));

            hatch.TransformBy(GeoTransform3.RotationY(100.0 * Math.PI / 180.0));

            // AutoCAD 2021: 210, where the sum of the library came to the nought below nought and wrote -150.
            Assert.Equal(210.0, Degrees(hatch), 6);
        }

        [Fact]
        public void AHatchOfANormalDown_TurnedAboutXTo300Degrees_HasItsAngleTurnedMinusAHalfCircle()
        {
            DxfHatch hatch = UserHatch(new GeoVector3(0, 0, -1));

            hatch.TransformBy(GeoTransform3.RotationX(300.0 * Math.PI / 180.0));

            // AutoCAD 2021: -150 (the sum comes to +180 and AutoCAD wrote -180).
            Assert.Equal(-150.0, Degrees(hatch), 6);
        }

        [Fact]
        public void AHatchOfANormalUp_TurnedAboutZByAHalfCircle_HasItsAngleTurnedPlusAHalfCircle()
        {
            DxfHatch hatch = UserHatch(new GeoVector3(0, 0, 1));

            hatch.TransformBy(GeoTransform3.RotationZ(Math.PI));

            Assert.Equal(210.0, Degrees(hatch), 6);
        }

        [Fact]
        public void AHatchOfANormalDown_TurnedAboutZByAHalfCircle_HasItsAngleTurnedMinusAHalfCircle()
        {
            DxfHatch hatch = UserHatch(new GeoVector3(0, 0, -1));

            hatch.TransformBy(GeoTransform3.RotationZ(Math.PI));

            Assert.Equal(-150.0, Degrees(hatch), 6);
        }
    }
}
