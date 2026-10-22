using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Booleans and cuts of bodies within the tolerance of each other: a weld that keeps every face on its plane, a bent bar
    /// that no longer runs for minutes, openings cut in closed, unions bridged across a gap, and a chip that reads one way
    /// to every operation.
    /// </summary>
    public class Boolean3FaultLN7Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        [Fact]
        public void AColumnAndABeamWhoseEndStandsClearOfItsFace_JoinWithTheGapFilledAndTheEndOnItsPlane()
        {
            // The beam's end stands 0.008 clear of the column's face at a tolerance of 0.01, and the end is wider than the
            // column. Welded closed with the end's corners on the column's face, the two strips of the end beyond the column
            // were tilted into wedges, and the union held 999.8 more than the two, where filling the gap holds 878.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(-0.272847032534353, -0.3397102569414816, -0.23120555145861837), 2.312383505661219);
            const double d = -0.008, w = 383.2002013377846, h = 365.84882050093677;
            GeoSolid3 column = Box(0, 0, 0, 300, 300, 1000).TransformBy(turn);
            GeoSolid3 beam = Box(300 - d, 150 - w / 2, 500 - h / 2, 2000, 150 + w / 2, 500 + h / 2).TransformBy(turn);
            double two = 300.0 * 300 * 1000 + (1700 + d) * w * h;
            double gap = -d * 300 * h;

            Assert.True(column.TryUnion(beam, out GeoSolid3 union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());
            Assert.InRange(union.GetVolume(tolerance), two - 1E-3, two + gap + 1E-3);
        }

        [Fact]
        public void ABoxLessACubeAHairInsideTheToleranceOffThreeFaces_KeepsItsSideFlat()
        {
            // A corner of the box's top stood 0.0099 in from the edge of its side, and the weld put it on that edge, which
            // took the side, 4 000 square millimetres, off flat: read as triangles, the box held 6.5 less than the box less
            // the cube.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            const double off = 0.009999999782436016, size = 0.8856200266562496;
            GeoTransform3 place = GeoTransform3.Translation(new GeoVector3(123456.7, 234567.8, 12345.6))
                * GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(0.524017871318493, -0.6732559283872018, 0.521662463121818), 0.0061);
            GeoSolid3 body = new GeoObb3(new GeoPoint3(50, 30, 20), 100, 60, 40).ToSolid().TransformBy(place);
            GeoSolid3 tool = new GeoObb3(new GeoPoint3(off + size / 2, off + size / 2, off + size / 2), size, size, size).ToSolid().TransformBy(place);

            Assert.True(body.TrySubtract(tool, out GeoSolid3 rest, tolerance));
            Assert.True(rest.Validate(tolerance).IsValid, rest.Validate(tolerance).ToString());

            // Between the cube cut out and the cube taken with the walls the tolerance thick between it and the faces.
            Assert.InRange(rest.GetVolume(tolerance), 240000 - Math.Pow(size + off, 3) - 1E-6, 240000 - Math.Pow(size, 3) + 1E-6);
        }

        [Fact]
        public void ACubeAndACubeTiltedOffItsFaceWithinTheTolerance_JoinWithoutAFold()
        {
            // Kept on their planes, the weld closed a step lying on the first cube's top facing down, over it: closed and
            // valid, with 100 000 square millimetres of surface where the union has 99 998.02, and two faces running back over
            // themselves.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(10, 30, 20), new GeoVector3(0.3, -0.2, 1), 0.7);
            GeoTransform3 tilt = GeoTransform3.RotationAxis(new GeoPoint3(100, 0, 0), new GeoVector3(0, 1, 0), -Math.Atan(0.0099 / 100));
            GeoSolid3 a = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            GeoSolid3 b = Box(100, 0, 0, 200, 100, 100).TransformBy(tilt).TransformBy(turn);

            Assert.True(a.TryUnion(b, out GeoSolid3 union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            var fine = new Tolerance(1E-7, 1E-9, 1E-9, 1E-7);
            Assert.All(union.Faces, face => Assert.True(face.Boundary.IsSimple(fine)));
            Assert.Equal(99998.02, union.Faces.Sum(face => face.Area), 2);
        }

        private static GeoSolid3 IBeam(GeoCoordinateSystem3 frame, double length, double h, double b, double tw, double tf, Tolerance tolerance)
        {
            var profile = new GeoPolygon2(
                new GeoPoint2(-b / 2, -h / 2), new GeoPoint2(b / 2, -h / 2), new GeoPoint2(b / 2, -h / 2 + tf), new GeoPoint2(tw / 2, -h / 2 + tf),
                new GeoPoint2(tw / 2, h / 2 - tf), new GeoPoint2(b / 2, h / 2 - tf), new GeoPoint2(b / 2, h / 2), new GeoPoint2(-b / 2, h / 2),
                new GeoPoint2(-b / 2, h / 2 - tf), new GeoPoint2(-tw / 2, h / 2 - tf), new GeoPoint2(-tw / 2, -h / 2 + tf), new GeoPoint2(-b / 2, -h / 2 + tf));
            return GeoSolid3.Extrude(profile, frame, length, tolerance);
        }

        [Fact]
        public void AColumnAndABeamOverhangingItsFlangeEdge_JoinWithoutFacesLyingOverEachOther()
        {
            // The beam frames 0.002 into the column's flange and its own flange runs past the flange's edge. Kept on their
            // planes, the weld closed two steps 0.002 by 16 lying on the side of the column's flange, facing the other way:
            // the check of that read the two as lying in different planes, at a millionth, and took them for not over each
            // other.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(0.30418070722566015, 0.2621019909913195, 0.23835954942664106), 1.1387303905276258);
            GeoSolid3 column = IBeam(new GeoCoordinateSystem3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 1, 0), new GeoVector3(-1, 0, 0)), 4000, 300, 300, 11, 19, tolerance).TransformBy(turn);
            GeoSolid3 beam = IBeam(new GeoCoordinateSystem3(new GeoPoint3(150 - 0.002, 120, 3000), new GeoVector3(0, 1, 0), new GeoVector3(0, 0, 1)), 3000, 500, 180, 10, 16, tolerance).TransformBy(turn);

            Assert.True(column.TryUnion(beam, out GeoSolid3 union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            var fine = new Tolerance(1E-6, 1E-9, 1E-9, 1E-6);
            IReadOnlyList<GeoFace3> faces = union.Faces;

            for (int i = 0; i < faces.Count; i++)
            {
                for (int j = i + 1; j < faces.Count; j++)
                {
                    if (Math.Abs(Math.Abs(faces[i].Normal.DotProduct(faces[j].Normal)) - 1) > 1E-9
                        || faces[j].Boundary.Vertices.Any(v => Math.Abs(faces[i].GetPlane().SignedDistanceTo(v)) > 1E-6))
                    {
                        continue;
                    }

                    double shared = 0.0;

                    try
                    {
                        shared = Boolean3.Intersect(faces[i], faces[j], fine).Sum(piece => piece.Area);
                    }
                    catch (ArgumentException)
                    {
                        // Not in one plane after all.
                    }

                    Assert.True(shared <= 1E-6, $"faces {i} and {j} lie over each other by {shared}");
                }
            }

            Assert.Equal(88447981.04, union.GetVolume(tolerance), 2);
        }

        private static GeoSolid3 BentBar(out GeoSolid3 beam)
        {
            // A bar 16 across bent through a right angle in forty steps, its straight leg ending 0.001 above the beam's floor.
            const double off = 0.0009999999998681038;
            var profile = new GeoPolygon2(Enumerable.Range(0, 16).Select(i => new GeoPoint2(8 * Math.Cos(i * Math.PI / 8), 8 * Math.Sin(i * Math.PI / 8))));
            var points = new List<GeoPoint3> { new GeoPoint3(100, 0, off) };

            for (int i = 0; i <= 40; i++)
            {
                double a = i * Math.PI / 80;
                points.Add(new GeoPoint3(200 - 100 * Math.Cos(a), 0, 600 + 100 * Math.Sin(a)));
            }

            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(0.9036610740719648, 0.9464961616073251, -0.14039001946355678), 0.5412497015396365);
            beam = Box(-1000, -100, 0, 1000, 100, 300).TransformBy(turn);
            return GeoSolid3.Sweep(profile, new GeoPolyline3(points), new GeoVector3(0, 1, 0), Tol).TransformBy(turn);
        }

        [Fact]
        public void ABeamLessABentBarEndingTheToleranceOffItsFloor_ComesOutInSeconds()
        {
            // Cut the bar's way it came out open, holding what it should; cut by the 664 planes of both it ran for minutes into
            // tens of thousands of cells. Sealed, it is taken.
            GeoSolid3 bar = BentBar(out GeoSolid3 beam);
            Assert.True(bar.TryIntersect(beam, out GeoSolid3 common, Tol));
            double expected = 2000.0 * 200 * 300 - common.GetVolume(Tol);

            var watch = Stopwatch.StartNew();
            Assert.True(beam.TrySubtract(bar, out GeoSolid3 rest, Tol));
            watch.Stop();

            Assert.True(rest.Validate(Tol).IsValid, rest.Validate(Tol).ToString());

            // The bar's floor, 0.001 thick over its 196 square millimetres, either way.
            Assert.InRange(rest.GetVolume(Tol), expected - 0.2, expected + 1E-6);
            Assert.True(watch.Elapsed.TotalSeconds < 20, watch.Elapsed.ToString());
        }

        [Fact]
        public void APlateWithOpeningsTheToleranceInsideItsFaces_JoinsACornerBoxAsItDid()
        {
            // Twelve openings 22 by 22 standing the tolerance, to a few parts in a thousand, inside the plate's faces, those of
            // the first column the tolerance inside its end too. Taking the openings out one at a time where cutting them in
            // at once left the material open sent the union on to cutting both by every plane of both, which came out wound
            // two ways, 718 137.69, where it holds 722 622.74; the cut of the openings is left as it was.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            GeoTransform3 place = GeoTransform3.Translation(new GeoVector3(123456.7, -65432.1, 9876.5))
                * GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(-0.47142400847348576, 0.9050639140908905, -0.07537376930721751), 2.438707009162152);
            double[] e =
            {
                0.009999999898301977, 0.010000000000277657, 0.00999999118912301, 0.00999942410557668, 0.01000000000003207, 0.009999779454259545,
                0.009999935819087829, 0.009999999797697863, 0.009991874825534958, 0.009999876793812675, 0.010000291495906071, 0.009999999999421782,
            };
            var openings = new List<GeoSolid3>();

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double x = 40 + i * 70, y = 40 + j * 60, ej = e[i * 3 + j];
                    openings.Add(Box(i == 0 ? ej : x - 11, y - 11, ej, x + 11, y + 11, 12 - ej).TransformBy(place));
                }
            }

            var plate = new GeoSolid3(Box(0, 0, 0, 300, 200, 12).TransformBy(place).Faces, openings);
            GeoSolid3 corner = Box(250, 150, -5, 320, 220, 20).TransformBy(place);

            Assert.True(plate.TryUnion(corner, out GeoSolid3 union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());
            Assert.Equal(722622.74, union.GetVolume(tolerance), 2);
        }

        [Theory]
        [InlineData(0.01, 0.10283012017739479, 0.3881510584094334, -0.20000974417338602, 39.68698174678114, 3.4686874055623482)]
        public void TwoCubesExactlyTheToleranceApart_AreJoinedAcrossTheGap(double tol, double x, double y, double z, double ox, double angle)
        {
            // Welded closed, the union holds the two and the gap filled over the face they share, which the two less their
            // common part, nothing, did not add up to, and it was not made.
            var tolerance = new Tolerance(tol, 1E-5, 1E-5, tol);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(ox, 0, 0), new GeoVector3(x, y, z), angle);
            GeoSolid3 a = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            GeoSolid3 b = Box(100 + tol, 0, 0, 200, 100, 100).TransformBy(turn);

            Assert.True(a.TryUnion(b, out GeoSolid3 union, tolerance, out BooleanOutcome outcome), outcome.ToString());
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());
            Assert.Equal(2000000.0, union.GetVolume(tolerance), 6);
        }

        [Fact]
        public void AChipATenthOfANanometreDeeperThanTheTolerance_ReadsAtTheCornerAsAlongTheEdge()
        {
            // Read by the rounding of each cell's own corners, the chip at the corner of the cube was cut and the one along its
            // edge, as deep, was not. Both are nothing now: no common part, and the cube less either is the cube.
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(10, 30, 20), new GeoVector3(0.3, -0.2, 1), 0.7);
            const double depth = 0.0010000001;
            GeoSolid3 a = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            double va = a.GetVolume(Tol);

            foreach (GeoSolid3 chip in new[] { Box(-20, -20, 100 - depth, 20, 20, 150), Box(-20, 30, 100 - depth, 20, 60, 150) })
            {
                GeoSolid3 b = chip.TransformBy(turn);
                double common = a.TryIntersect(b, out GeoSolid3 shared, Tol) ? shared.GetVolume(Tol) : 0.0;
                Assert.True(a.TrySubtract(b, out GeoSolid3 rest, Tol));

                Assert.Equal(0.0, common, 6);
                Assert.Equal(va, rest.GetVolume(Tol), 6);
            }
        }

        [Fact]
        public void AChipInTheRoundingBandAtTheCornerOfATurnedCube_IsCountedOnceByTheCommonPartAndTheDifference()
        {
            // At a tolerance of a hundredth, a corner chip 1.17E-10 deeper than the tolerance, against a rounding of its cell's
            // corners of 1.1685375E-10: the common part, cutting the block, measured it 1.1685903E-10 deeper and cut it; the
            // difference, cutting the cube, measured it 1.1685354E-10 deeper and did not, and the two held the chip twice.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(41.71615887513205, 0, 0), new GeoVector3(0.16530560174272657, 0.08338576582418089, 0.4660798919448069), 3.99186147749045);
            const double depth = 0.01000000011685055;
            GeoSolid3 a = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            GeoSolid3 b = Box(-20, -20, 100 - depth, 20, 20, 150).TransformBy(turn);
            double va = a.GetVolume(tolerance), vb = b.GetVolume(tolerance);

            Assert.True(a.TryUnion(b, out GeoSolid3 union, tolerance));
            Assert.True(a.TrySubtract(b, out GeoSolid3 rest, tolerance));
            Assert.True(b.TrySubtract(a, out GeoSolid3 beyond, tolerance));
            double common = a.TryIntersect(b, out GeoSolid3 shared, tolerance) ? shared.GetVolume(tolerance) : 0.0;

            Assert.Equal(va, rest.GetVolume(tolerance) + common, 6);
            Assert.Equal(vb, beyond.GetVolume(tolerance) + common, 6);
            Assert.Equal(va + vb, union.GetVolume(tolerance) + common, 6);
        }
    }
}
