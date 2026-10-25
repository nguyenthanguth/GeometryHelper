using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Booleans of bodies exactly the tolerance into or off each other: a union that held a slab twice, a gap closed by
    /// tilting a beam's end, rims that are no gap, a union holding more than the two, and what is left no thicker than the
    /// tolerance.
    /// </summary>
    public class Boolean3FaultF11Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly GeoTransform3 Turn = GeoTransform3.RotationAxis(new GeoPoint3(10, 30, 20), new GeoVector3(0.3, -0.2, 1), 0.7);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        [Fact]
        public void ACubeJoinedToASlabTheToleranceThickInsideItsTop_IsTheCubeWoundAlike()
        {
            // The slab, cut by the cube's planes, is one cell no thicker than the tolerance, read on the cube's skin and kept
            // beside the cube whole: the union came out wound against itself over the slab's four sides, 1 000 000.4.
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100).TransformBy(Turn);
            GeoSolid3 slab = Box(30, 30, 100 - 0.001, 70, 60, 100).TransformBy(Turn);

            Assert.True(cube.TryUnion(slab, out GeoSolid3 union, Tol));
            Assert.True(union.Validate(Tol).IsValid, union.Validate(Tol).ToString());
            Assert.Equal(1000000.0, union.GetVolume(Tol), 6);

            Assert.True(slab.TryUnion(cube, out union, Tol));
            Assert.True(union.Validate(Tol).IsValid, union.Validate(Tol).ToString());
            Assert.Equal(1000000.0, union.GetVolume(Tol), 6);
        }

        [Fact]
        public void AColumnJoinedToABeamWhoseEndStandsClearOfItsFace_HoldsTheGapFilledWhereTheyFaceEachOther()
        {
            // The beam's end, 387 wide and 118 high, stands 0.008 clear of the column's face, 300 wide, at a tolerance of a
            // hundredth. Kept on their planes, the two rims of the gap made no step, and the weld put the beam's end on the
            // column's face, tilting the parts of the end beyond the column into wedges: 41.06 more than the two and the gap.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            const double w = 387, h = 118, gap = 0.008;
            GeoSolid3 column = Box(0, 0, 0, 300, 300, 1000);
            GeoSolid3 beam = Box(300 + gap, 150 - w / 2, 500 - h / 2, 2000, 150 + w / 2, 500 + h / 2);
            double two = 300.0 * 300 * 1000 + (1700 - gap) * w * h;

            Assert.True(column.TryUnion(beam, out GeoSolid3 union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());
            Assert.InRange(union.GetVolume(tolerance), two - 1E-6, two + gap * 300 * h + 1E-6);
        }

        [Fact]
        public void ASlabTheToleranceThickInsideACube_IsNoCommonPartAndNothingLeftOfIt()
        {
            // A slab 40 by 30 and a thousandth thick encloses nothing, as Validate reads it: it was the common part of the two,
            // 1.2 cubic millimetres. Such a slab at the cube's top less the cube is the slab as it came, as it was: what is
            // left of a body that itself encloses nothing is not asked.
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100).TransformBy(Turn);
            GeoSolid3 within = Box(30, 30, 50, 70, 60, 50.001).TransformBy(Turn);
            GeoSolid3 atTop = Box(30, 30, 100 - 0.001, 70, 60, 100).TransformBy(Turn);

            Assert.False(cube.TryIntersect(within, out GeoSolid3 common, Tol, out BooleanOutcome outcome));
            Assert.Null(common);
            Assert.Equal(BooleanOutcome.Empty, outcome);

            Assert.True(atTop.TrySubtract(cube, out GeoSolid3 rest, Tol, out outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Same(atTop, rest);
        }

        private static (GeoSolid3 Column, GeoSolid3 Beam, double Exact, double Two, double End) ColumnAndBeam(double w, double h, double d, GeoVector3 axis, double angle)
        {
            // Placed as the sweep that found them placed them, a translation by nought and the turn, which rounds the corners
            // so: turned alone, the two join into a reading of the slab of their own.
            GeoTransform3 turn = GeoTransform3.Translation(new GeoVector3(0, 0, 0)) * GeoTransform3.RotationAxis(GeoPoint3.Origin, axis, angle);
            GeoSolid3 column = Box(0, 0, 0, 300, 300, 1000).TransformBy(turn);
            GeoSolid3 beam = Box(300 - d, 150 - w / 2, 500 - h / 2, 2000, 150 + w / 2, 500 + h / 2).TransformBy(turn);
            double two = 300.0 * 300 * 1000 + (1700 + d) * w * h;
            return (column, beam, two - d * Math.Min(w, 300) * h, two, d * w * h);
        }

        /// <summary>
        /// The turn of a quaternion of whole numbers, its matrix worked out without a sine or a cosine, so that the corners
        /// it turns are the same to the bit on every runtime.
        /// </summary>
        private static GeoTransform3 Quaternion(int a, int b, int c, int d)
        {
            double q = a * a + b * b + c * c + d * d;
            var m = new double[4, 4];
            m[0, 0] = (a * a + b * b - c * c - d * d) / q; m[0, 1] = 2.0 * (b * c - a * d) / q; m[0, 2] = 2.0 * (b * d + a * c) / q;
            m[1, 0] = 2.0 * (b * c + a * d) / q; m[1, 1] = (a * a - b * b + c * c - d * d) / q; m[1, 2] = 2.0 * (c * d - a * b) / q;
            m[2, 0] = 2.0 * (b * d - a * c) / q; m[2, 1] = 2.0 * (c * d + a * b) / q; m[2, 2] = (a * a - b * b - c * c + d * d) / q;
            m[3, 3] = 1;
            return new GeoTransform3(m);
        }

        [Fact]
        public void AColumnJoinedToABeamWhoseEndRunsExactlyTheToleranceIntoIt_HoldsTheSlabOnce()
        {
            // The rims a weld keeping the faces on their planes left, the column's face and the beam's end within it, stand a
            // thousandth apart and bound the slab both hold. Taken for a gap and closed by strips round it, the union held the
            // slab twice, 51.86 cubic millimetres over the exact union it held before.
            const double w = 201, h = 258, d = 0.001;
            GeoTransform3 turn = Quaternion(6, -1, -2, -6);
            GeoSolid3 column = Box(0, 0, 0, 300, 300, 1000).TransformBy(turn);
            GeoSolid3 beam = Box(300 - d, 150 - w / 2, 500 - h / 2, 2000, 150 + w / 2, 500 + h / 2).TransformBy(turn);
            double exact = 300.0 * 300 * 1000 + 1700 * w * h;

            foreach (bool columnFirst in new[] { true, false })
            {
                Assert.True((columnFirst ? column : beam).TryUnion(columnFirst ? beam : column, out GeoSolid3 union, Tol));
                Assert.True(union.Validate(Tol).IsValid, union.Validate(Tol).ToString());
                Assert.Equal(exact, union.GetVolume(Tol), 4);
            }
        }

        [Fact]
        public void ACubeJoinedToABlockExactlyTheToleranceIntoItsTopAtTheCorner_HoldsNoMoreThanTheTwo()
        {
            // Worked out again, a cell standing beside the block's slab along the cube's edge was left out with it, and the
            // union, valid, held 1 080 002.0, more than the two; as it came it was wound against itself, 1 080 003.21. A guard:
            // made, it is valid and in range; otherwise it says it was not worked out.
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(23.60293803438681, 0, 0), new GeoVector3(-0.470397974350675, 0.1516818411888936, -0.1895637070027011), 1.222499318058835);
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            GeoSolid3 block = Box(-20, -20, 100 - 0.001, 20, 20, 150).TransformBy(turn);
            double vc = cube.GetVolume(Tol), vb = block.GetVolume(Tol);

            if (cube.TryUnion(block, out GeoSolid3 union, Tol, out BooleanOutcome outcome))
            {
                Assert.True(union.Validate(Tol).IsValid, union.Validate(Tol).ToString());
                Assert.InRange(union.GetVolume(Tol), Math.Max(vc, vb) - 1E-6, vc + vb + 1E-6);
            }
            else
            {
                Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            }
        }

        [Fact]
        public void AColumnJoinedToAWideBeamRunningExactlyTheToleranceIntoIt_HoldsAReadingOfTheSlab()
        {
            // Worked out again, cells judged within the column by a side lying on its face from beyond, and none on the near
            // side, were left out, and the union, valid, held 230.1 less than the exact union, 146.8 less than the column and
            // the beam beyond the column's face. A guard: made, it is valid and holds a reading of the slab; otherwise it says it
            // was not worked out.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            var (column, beam, _, two, end) = ColumnAndBeam(377.4328325304356, 107.46428328913836, 0.01,
                new GeoVector3(0.24333679105310546, -0.48896821401499596, 0.3927866865600397), 0.6714398612600937);

            if (column.TryUnion(beam, out GeoSolid3 union, tolerance, out BooleanOutcome outcome))
            {
                Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());
                Assert.InRange(union.GetVolume(tolerance), two - end - 1E-6, two + 1E-6);
            }
            else
            {
                Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
            }
        }

        [Fact]
        public void APlateLessABoxLeavingARodThinnerThanTwiceTheTolerance_LeavesNothing()
        {
            // The rod, 0.0018 square and 10 long, encloses 0.0000324 cubic millimetres, no more than a slab the tolerance thick
            // over its faces, as Validate reads it; it was handed back.
            GeoSolid3 plate = Box(0, 0, 0, 10, 10, 0.0018);
            GeoSolid3 tool = Box(-1, 0.0018, -1, 11, 11, 1);

            Assert.False(plate.TrySubtract(tool, out GeoSolid3 rest, Tol, out BooleanOutcome outcome));
            Assert.Null(rest);
            Assert.Equal(BooleanOutcome.Empty, outcome);
        }

        [Fact]
        public void APlateThinnerThanTheToleranceLessABoxBesideIt_IsThePlateAsItCame()
        {
            // The plate, turned, encloses no more than a slab the tolerance thick, and the box beside it meets its bounding box
            // but not the plate. Read as nothing, it was empty or itself by where the box stood; what is left of a body that
            // itself encloses nothing is not asked, and it is the plate as it came, as it was.
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(0, 0, 1), Math.PI / 4);
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 0.0005).TransformBy(turn);

            Assert.True(plate.TrySubtract(Box(101, 101, -1, 120, 120, 1).TransformBy(turn), out GeoSolid3 rest, Tol, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Same(plate, rest);
        }

        [Fact]
        public void ASheetOfFourFacesLessABoxCrossingIt_IsNotTakenForNothing()
        {
            // The sheet does not close, and what is left of it encloses nothing to speak of; it is not asked whether that is a
            // slab the tolerance thick.
            var faces = new List<GeoFace3>();

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    faces.Add(new GeoFace3(new GeoPolygon3(new[]
                    {
                        new GeoPoint3(i * 100, j * 100, 0), new GeoPoint3(i * 100 + 100, j * 100, 0),
                        new GeoPoint3(i * 100 + 100, j * 100 + 100, 0), new GeoPoint3(i * 100, j * 100 + 100, 0),
                    })));
                }
            }

            Assert.True(new GeoSolid3(faces).TrySubtract(Box(50, 50, -1, 150, 150, 1), out GeoSolid3 rest, Tol, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.NotNull(rest);
        }

        [Fact]
        public void ABodyEnclosingNothingWoundAgainstItself_IsNotTakenForNothing()
        {
            // A slab the tolerance thick with its top laid on it again both ways round and its floor twice more: closed,
            // enclosing nothing, and wound against itself. Only a result whose issues are those of a slab is read as nothing.
            GeoSolid3 slab = Box(30, 30, 50, 70, 60, 50.001);
            var faces = new List<GeoFace3>(slab.Faces);
            GeoFace3 top = slab.Faces.OrderByDescending(face => face.Centroid.Z).First();
            GeoFace3 floor = slab.Faces.OrderBy(face => face.Centroid.Z).First();
            faces.AddRange(new[] { top, top.Flip(), floor, floor });
            var crossed = new GeoSolid3(faces);

            Assert.Contains(crossed.Validate(Tol).Issues, issue => issue.Kind == SolidIssueKind.SameWayEdge);
            Assert.Contains(crossed.Validate(Tol).Issues, issue => issue.Kind == SolidIssueKind.NoVolume);

            GeoSolid3 result = crossed;
            Assert.False(Boolean3.IsNothing("difference", ref result, Box(0, 0, 0, 100, 100, 100), slab, Tol));
            Assert.Same(crossed, result);
        }
    }
}
