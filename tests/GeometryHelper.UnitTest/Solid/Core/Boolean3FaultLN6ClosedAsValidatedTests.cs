using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A boolean of two closed bodies gives a body closed as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it, or says it
    /// could not work one out.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoSolid3.IsClosed(Tolerance)"/> matches the ends of the edges first, made one within the tolerance, and called
    /// two copies of an edge about the tolerance apart one edge, which <c>Validate</c> reads as two, each open. The boolean
    /// took what <c>IsClosed</c> called closed and never tried another way: a box 100 by 60 by 40 less a cube 0.92 across a
    /// hair inside the tolerance off two of its faces, the two turned 0.0061 radians, came back closed to the one and open
    /// by two stretches to the other.
    /// </remarks>
    public class Boolean3FaultLN6ClosedAsValidatedTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static void Pair(GeoVector3 axis, double angle, double off, double size, out GeoSolid3 body, out GeoSolid3 tool)
        {
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, axis, angle);
            body = new GeoObb3(new GeoPoint3(50, 30, 20), 100, 60, 40).ToSolid().TransformBy(turn);
            tool = new GeoObb3(new GeoPoint3(off + size / 2, off + size / 2, 15 + size / 2), size, size, size).ToSolid().TransformBy(turn);
        }

        [Fact]
        public void ABoxLessACubeAHairInsideTheToleranceOffTwoFaces_ComesOutClosedAsValidateReadsIt()
        {
            const double size = 0.9158608495331653;
            Pair(new GeoVector3(0.9064756121773995, 0.08494096937711974, 0.4136266387080069), 0.0061, 0.0009999999999998814, size, out GeoSolid3 body, out GeoSolid3 tool);

            Assert.True(body.TrySubtract(tool, out GeoSolid3 rest, Tol));

            SolidValidation3 check = rest.Validate(Tol);
            Assert.True(check.IsClosed, check.ToString());
            Assert.True(check.IsValid, check.ToString());

            // Between the cube cut out and the cube taken with the outside through the walls a tolerance thick.
            double volume = rest.GetVolume(Tol);
            Assert.InRange(volume, 240000 - (size + 0.001) * (size + 0.001) * size - 1E-6, 240000 - size * size * size + 1E-6);
        }

        [Theory]
        [InlineData(-0.13249797760637283, 0.3141974303327898, 0.9400660937947356, 0.6096882655330413, 0.0010000000000000022, 0.5967218843739116)]
        [InlineData(-0.7068414755460608, 0.134164252260932, 0.6945322756093719, 1.0047172228827688, 0.001, 0.33758929848558705)]
        public void ABoxLessACubeTheToleranceOffTwoFacesTurned_IsClosedAsValidateReadsItOrNotMade(double x, double y, double z, double angle, double off, double size)
        {
            Pair(new GeoVector3(x, y, z), angle, off, size, out GeoSolid3 body, out GeoSolid3 tool);

            bool made = body.TrySubtract(tool, out GeoSolid3 rest, Tol, out BooleanOutcome outcome);

            if (made)
            {
                Assert.True(rest.Validate(Tol).IsClosed, rest.Validate(Tol).ToString());

                // Between the cube cut out and the cube taken with the outside through the walls a tolerance thick.
                Assert.InRange(rest.GetVolume(Tol), 240000 - (size + off) * (size + off) * size - 1E-6, 240000 - size * size * size + 1E-6);
            }
            else
            {
                Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            }
        }

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        [Fact]
        public void TwoCubesTheToleranceApartTurned_AreJoinedAcrossTheGapOrNotMade()
        {
            // At a tolerance of a hundredth the two share nothing and lie face to face the tolerance apart: welded closed, the
            // union holds the two, 1 999 900, and the gap filled, 0.01 by 10 000, 2 000 000.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(39.68698174678114, 0, 0), new GeoVector3(0.10283012017739479, 0.3881510584094334, -0.20000974417338602), 3.4686874055623482);
            GeoSolid3 a = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            GeoSolid3 b = Box(100.01, 0, 0, 200, 100, 100).TransformBy(turn);

            if (a.TryUnion(b, out GeoSolid3 union, tolerance, out BooleanOutcome outcome))
            {
                Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());
                Assert.Equal(2000000.0, union.GetVolume(tolerance), 4);
            }
            else
            {
                Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            }
        }

        [Fact]
        public void AUnionOfABoxAndACubeAHairThroughThreeOfItsFaces_HoldsTheTwoLessTheirCommonPartOrIsNotMade()
        {
            // Open as it came, and worked out the other way, the union held 240 001.2065: the cube counted whole, its common
            // part with the box twice, where the two less their common part hold 240 000.0032.
            const double size = 1.254227660854453;
            double[] off = { -0.0010184570546095718, -0.0009025284528167681, -0.0010085433063261394 };
            GeoSolid3 box = Box(0, 0, 0, 100, 60, 40);
            GeoSolid3 cube = Box(off[0], off[1], 40 - off[2] - size, off[0] + size, off[1] + size, 40 - off[2]);

            if (box.TryUnion(cube, out GeoSolid3 union, Tol, out BooleanOutcome outcome))
            {
                Assert.True(union.Validate(Tol).IsValid, union.Validate(Tol).ToString());
                Assert.InRange(union.GetVolume(Tol), 240000.0032 - 0.03, 240000.0032 + 0.03);
            }
            else
            {
                Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            }
        }

        [Fact]
        public void AUnionOfACubeAndAChipTheToleranceDeep_HoldsAReadingOfTheChipOrIsNotMade()
        {
            // Welded closed, the union held 1 059 999.53, less than any reading of the chip 0.001 deep: 1 060 000 without it,
            // 1 060 000.6 with it, 1 060 001.2 with the two counted whole.
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(42.2151892875392, 0, 0), new GeoVector3(-0.21499471260001635, -0.21264074403449928, -0.2137702689817037), 0.19047016100514225);
            GeoSolid3 a = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            GeoSolid3 b = Box(-20, 30, 100 - 0.001, 20, 60, 150).TransformBy(turn);

            if (a.TryUnion(b, out GeoSolid3 union, Tol, out BooleanOutcome outcome))
            {
                Assert.True(union.Validate(Tol).IsValid, union.Validate(Tol).ToString());
                Assert.InRange(union.GetVolume(Tol), 1060000 - 1E-6, 1060001.2 + 1E-6);
            }
            else
            {
                Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            }
        }

        [Fact]
        public void ACutThatComesOutOpen_IsStillClosedWhereAClosingIsAsked()
        {
            // The difference of two closed bodies that stays open is reported as not made, and the run of cuts asked to close
            // a cut it would skip is handed what came out open, as before.
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(0.8164402357379161, -0.8005254854450586, 0.5473250204451965), 0.6620032922653496);
            const double size = 0.5935231749403864;
            double[] off = { 0.001, 0.001000000000000001, 0.0010000000000001644 };
            GeoSolid3 body = Box(0, 0, 0, 100, 60, 40).TransformBy(turn);
            GeoSolid3 tool = Box(off[0], off[1], 40 - off[2] - size, off[0] + size, off[1] + size, 40 - off[2]).TransformBy(turn);

            Assert.False(body.TrySubtract(tool, out _, Tol, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);

            Assert.True(Boolean3.TrySubtractAll(body, new[] { tool }, out GeoSolid3 rest, new SolidBooleanOptions(Tol), new SolidClosingOptions(Tol, 0.005), out SubtractReport report));
            Assert.Single(report.Closed);
            Assert.Empty(report.Skipped);
            Assert.True(rest.Validate(Tol).IsValid);
        }

        [Fact]
        public void APlateWithAHole_PairsItsEdgesOneWayAndTheOther()
        {
            // A hole is wound as the boundary is, so its edges are run the other way round with the face on the left; the
            // plate's edges all pair, and the plate is closed without its edges being matched along lines.
            GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(P(0, 0, 10), P(100, 0, 10), P(100, 100, 10), P(0, 100, 10)), new[] { new GeoPolygon3(P(40, 40, 10), P(60, 40, 10), P(60, 60, 10), P(40, 60, 10)) }, Tol),
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0)), new[] { new GeoPolygon3(P(40, 40, 0), P(40, 60, 0), P(60, 60, 0), P(60, 40, 0)) }, Tol),
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(100, 0, 10), P(0, 0, 10))),
                new GeoFace3(new GeoPolygon3(P(100, 0, 0), P(100, 100, 0), P(100, 100, 10), P(100, 0, 10))),
                new GeoFace3(new GeoPolygon3(P(100, 100, 0), P(0, 100, 0), P(0, 100, 10), P(100, 100, 10))),
                new GeoFace3(new GeoPolygon3(P(0, 100, 0), P(0, 0, 0), P(0, 0, 10), P(0, 100, 10))),
                new GeoFace3(new GeoPolygon3(P(40, 40, 0), P(40, 40, 10), P(60, 40, 10), P(60, 40, 0))),
                new GeoFace3(new GeoPolygon3(P(60, 40, 0), P(60, 40, 10), P(60, 60, 10), P(60, 60, 0))),
                new GeoFace3(new GeoPolygon3(P(60, 60, 0), P(60, 60, 10), P(40, 60, 10), P(40, 60, 0))),
                new GeoFace3(new GeoPolygon3(P(40, 60, 0), P(40, 60, 10), P(40, 40, 10), P(40, 40, 0))),
            };
            var plate = new GeoSolid3(faces);

            Assert.True(plate.Validate(Tol).IsValid, plate.Validate(Tol).ToString());
            Assert.True(Shells3.PairsUp(plate.Faces, Tol));
            Assert.True(Shells3.Closes(plate, Tol));
        }

        [Theory]
        [InlineData(1E-9)]
        [InlineData(1E-8)]
        [InlineData(1E-7)]
        public void TwoCopiesOfAnEdgeEitherSideOfTheTolerance_ReadClosedAsValidateReadsThem_AndOpenAsTheBooleansDo(double extra)
        {
            // A prism 10 high on a plan with one side the tolerance long: the top's copy of that edge a hair longer than the
            // tolerance, the side face's a hair shorter. Validate keeps the shorter with its twin, their ends within the
            // tolerance, and reads the prism closed; the booleans' reading leaves the shorter out and reads the longer open, and
            // paired on the grid, the two were read closed.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            var u = new GeoVector2(0.6, 0.8);
            var p4 = new GeoPoint2(0, 49.9);
            var p3 = new GeoPoint2(p4.X + u.X * (0.01 + extra), p4.Y + u.Y * (0.01 + extra));
            GeoPoint2[] plan = { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 50), p3, p4 };
            GeoPoint3 Low(int i) => new GeoPoint3(plan[i].X, plan[i].Y, 0);
            GeoPoint3 High(int i) => new GeoPoint3(plan[i].X, plan[i].Y, 10);
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(High(0), High(1), High(2), High(3), High(4))),
                new GeoFace3(new GeoPolygon3(Low(4), Low(3), Low(2), Low(1), Low(0))),
            };

            for (int i = 0; i < 5; i++)
            {
                int j = (i + 1) % 5;
                GeoPoint3 top = High(j);

                if (i == 3)
                {
                    top = new GeoPoint3(top.X + u.X * 2 * extra, top.Y + u.Y * 2 * extra, top.Z);
                }

                faces.Add(new GeoFace3(new GeoPolygon3(Low(i), Low(j), top, High(i))));
            }

            var prism = new GeoSolid3(faces);

            Assert.True(prism.Validate(tolerance).IsClosed);
            Assert.False(Shells3.PairsUp(prism.Faces, tolerance));
            Assert.False(Shells3.Closes(prism, tolerance));
        }
    }
}
