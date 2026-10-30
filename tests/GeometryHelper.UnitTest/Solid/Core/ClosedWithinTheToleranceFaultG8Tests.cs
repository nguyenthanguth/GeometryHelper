using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// <c>IsClosed</c> and <c>Validate</c> read a body alike, the edges matched along a line within the point tolerance.
    /// <c>IsClosed</c> matched the ends of the edges first, each to the first corner seen within the tolerance of it, and so
    /// made one of two corners a tolerance and a half apart where a corner lay between them: a cube whose top stood a
    /// tolerance and a half over its sides, a speck of a face at each corner of its top half way between, read closed where
    /// <c>Validate</c> read it open along every edge of the top, and open again with the specks given last. Twin edges no
    /// longer than about the tolerance read open to <c>Validate</c>, and at a tolerance of nought a cube turned in space read
    /// open along nearly every edge, an edge and its twin standing apart by the rounding of the reckoning. The booleans and
    /// the cells read what they work on as before.
    /// </summary>
    public class ClosedWithinTheToleranceFaultG8Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>The places the bodies are tried in: as built, turned, far from the origin, and both.</summary>
        private static GeoTransform3 Placement(int which)
        {
            GeoTransform3 turn = GeoTransform3.RotationAxis(P(10, 30, 20), new GeoVector3(0.3, -0.2, 1), 0.7);
            GeoTransform3 far = GeoTransform3.Translation(new GeoVector3(523456.7, 734567.8, 12345.6));

            switch (which)
            {
                case 1: return turn;
                case 2: return GeoTransform3.Translation(new GeoVector3(123456.7, 234567.8, 12345.6));
                case 3: return far * turn;
                default: return GeoTransform3.Translation(new GeoVector3(0, 0, 0));
            }
        }

        /// <summary>
        /// A cube of 100 whose top stands a gap over the tops of its sides, with a speck of a face 0.00001 across at each
        /// corner of the top half way between, given before the cube's faces or after them.
        /// </summary>
        private static GeoSolid3 LiftedTop(double gap, bool specks, bool specksFirst, int placement)
        {
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0))),
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(100, 0, 100), P(0, 0, 100))),
                new GeoFace3(new GeoPolygon3(P(100, 0, 0), P(100, 100, 0), P(100, 100, 100), P(100, 0, 100))),
                new GeoFace3(new GeoPolygon3(P(100, 100, 0), P(0, 100, 0), P(0, 100, 100), P(100, 100, 100))),
                new GeoFace3(new GeoPolygon3(P(0, 100, 0), P(0, 0, 0), P(0, 0, 100), P(0, 100, 100))),
                new GeoFace3(new GeoPolygon3(P(0, 0, 100 + gap), P(100, 0, 100 + gap), P(100, 100, 100 + gap), P(0, 100, 100 + gap))),
            };

            if (specks)
            {
                var fine = new Tolerance(1E-9, 1E-9, 1E-9, 1E-9);
                var added = new List<GeoFace3>();

                foreach (GeoPoint2 corner in new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) })
                {
                    double z = 100 + gap / 2;
                    added.Add(new GeoFace3(new GeoPolygon3(new[] { P(corner.X, corner.Y, z), P(corner.X + 1E-5, corner.Y, z), P(corner.X, corner.Y + 1E-5, z) }, fine)));
                }

                faces.InsertRange(specksFirst ? 0 : faces.Count, added);
            }

            return new GeoSolid3(faces).TransformBy(Placement(placement));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void ACubeWhoseTopStandsHalfTheToleranceOverItsSides_IsClosedToEveryReading(int placement)
        {
            GeoSolid3 cube = LiftedTop(0.5 * Tol.EqualPoint, false, false, placement);

            Assert.True(cube.IsClosed(Tol));
            Assert.True(cube.Validate(Tol).IsClosed);
            Assert.True(Shells3.Closes(cube, Tol));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void ACubeWhoseTopStandsOneAndAHalfTolerancesOverItsSides_IsOpenToEveryReading(int placement)
        {
            GeoSolid3 cube = LiftedTop(1.5 * Tol.EqualPoint, false, false, placement);

            Assert.False(cube.IsClosed(Tol));
            Assert.False(cube.Validate(Tol).IsClosed);
            Assert.False(Shells3.Closes(cube, Tol));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void ACubeWhoseTopStandsTheToleranceOverItsSides_ReadsAlikeToEveryReading(int placement)
        {
            // A thousandth over, 100.001 less 100 is a hair more than a thousandth, and the rounding says whether the gap is
            // within the tolerance; whichever it says, the three readings say it alike.
            GeoSolid3 cube = LiftedTop(Tol.EqualPoint, false, false, placement);
            bool closed = cube.Validate(Tol).IsClosed;

            Assert.Equal(closed, cube.IsClosed(Tol));
            Assert.Equal(closed, Shells3.Closes(cube, Tol));
        }

        [Fact]
        public void ACubeWhoseTopStandsExactlyTheToleranceOverItsSides_IsClosedToEveryReading()
        {
            // 2^-10 over 100 is 100.0009765625 to the bit, and the gap the tolerance exactly.
            var tolerance = new Tolerance(0.0009765625, 1E-5, 1E-5, 0.0009765625);
            GeoSolid3 cube = LiftedTop(tolerance.EqualPoint, false, false, 0);

            Assert.True(cube.IsClosed(tolerance));
            Assert.True(cube.Validate(tolerance).IsClosed);
            Assert.True(Shells3.Closes(cube, tolerance));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(1, false)]
        [InlineData(3, true)]
        [InlineData(3, false)]
        public void ASpeckAtEachCornerHalfWayUpAGapOfAToleranceAndAHalf_DoesNotCloseIt(int placement, bool specksFirst)
        {
            // Each speck's corners are one point within the tolerance and its edges no edge; the top's corners and the sides'
            // stand 0.0015 apart, each 0.00075 from the speck. Matched to the speck seen first, they were one corner.
            GeoSolid3 cube = LiftedTop(1.5 * Tol.EqualPoint, true, specksFirst, placement);

            Assert.False(cube.Validate(Tol).IsClosed);
            Assert.False(cube.IsClosed(Tol));
            Assert.False(Shells3.Closes(cube, Tol));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void ACubeTurnedInSpace_IsClosedAtAToleranceOfNought(int placement)
        {
            GeoSolid3 cube = LiftedTop(0.0, false, false, placement);

            Assert.True(cube.Validate(Nought).IsClosed, cube.Validate(Nought).ToString());
            Assert.True(cube.IsClosed(Nought));
        }

        [Fact]
        public void AUnionOfTwoTurnedBoxes_IsClosedAtAToleranceOfNought()
        {
            // Made within a thousandth, the union's faces meet on corners each worked out once: read at nought, its edges and
            // their twins stood apart by the rounding of the reckoning, and it was open to both.
            GeoTransform3 turn = Placement(1);
            GeoSolid3 a = new GeoObb3(P(50, 50, 50), 100, 100, 100).ToSolid().TransformBy(turn);
            GeoSolid3 b = new GeoObb3(P(100, 90, 110), 100, 80, 120).ToSolid().TransformBy(turn);

            Assert.True(a.TryUnion(b, out GeoSolid3 union, Tol));
            Assert.True(union.Validate(Nought).IsClosed, union.Validate(Nought).ToString());
            Assert.True(union.IsClosed(Nought));
        }

        /// <summary>
        /// A slab 100 by 100 and the height given, its side at x = 100 slid up by the slide given: each upright edge of that
        /// side as long as the slab is high, beside the edge of the face next to it slid along it.
        /// </summary>
        private static GeoSolid3 SlidSlab(double height, double slide)
        {
            var fine = new Tolerance(1E-9, 1E-9, 1E-9, 1E-9);
            GeoFace3 Quad(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c, GeoPoint3 d) => new GeoFace3(new GeoPolygon3(new[] { a, b, c, d }, fine));

            return new GeoSolid3(
                Quad(P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0)),
                Quad(P(0, 0, height), P(100, 0, height), P(100, 100, height), P(0, 100, height)),
                Quad(P(0, 0, 0), P(100, 0, 0), P(100, 0, height), P(0, 0, height)),
                Quad(P(100, 0, slide), P(100, 100, slide), P(100, 100, height + slide), P(100, 0, height + slide)),
                Quad(P(100, 100, 0), P(0, 100, 0), P(0, 100, height), P(100, 100, height)),
                Quad(P(0, 100, 0), P(0, 0, 0), P(0, 0, height), P(0, 100, height)));
        }

        [Theory]
        [InlineData(0.0012, 0.0002)]
        [InlineData(0.0014, 0.0005)]
        [InlineData(0.0016, 0.0009)]
        public void ASlabWhoseSideIsSlidLessThanTheToleranceUpItsShortEdges_IsClosed(double height, double slide)
        {
            // Each upright edge of the side shares no more than the tolerance of its line with its twin, 0.001 of the 0.0012
            // at the least, and matched by overlap the two were apart: open along all four, where their ends lie within the
            // tolerance of each other.
            GeoSolid3 slab = SlidSlab(height, slide);

            Assert.True(slab.Validate(Tol).IsClosed, slab.Validate(Tol).ToString());
            Assert.True(slab.Validate(Tol).IsValid, slab.Validate(Tol).ToString());
            Assert.True(slab.IsClosed(Tol));
        }

        /// <summary>
        /// A slab 100 by 100 and the height given whose side at x = 100 runs from the inset given above its floor to as far
        /// under its top: each upright edge of that side no longer than the tolerance, beside its copy as long as the slab
        /// is high on the face next to it.
        /// </summary>
        private static GeoSolid3 ShortSidedSlab(double height, double inset)
        {
            var fine = new Tolerance(1E-9, 1E-9, 1E-9, 1E-9);
            GeoFace3 Quad(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c, GeoPoint3 d) => new GeoFace3(new GeoPolygon3(new[] { a, b, c, d }, fine));

            return new GeoSolid3(
                Quad(P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0)),
                Quad(P(0, 0, height), P(100, 0, height), P(100, 100, height), P(0, 100, height)),
                Quad(P(0, 0, 0), P(100, 0, 0), P(100, 0, height), P(0, 0, height)),
                Quad(P(100, 0, inset), P(100, 100, inset), P(100, 100, height - inset), P(100, 0, height - inset)),
                Quad(P(100, 100, 0), P(0, 100, 0), P(0, 100, height), P(100, 100, height)),
                Quad(P(0, 100, 0), P(0, 0, 0), P(0, 0, height), P(0, 100, height)));
        }

        [Theory]
        [InlineData(0.0015, 0.0003)]
        [InlineData(0.0012, 0.0001)]
        public void ASlabWhoseSideIsShortenedLessThanTheToleranceAtEachEnd_IsClosedAndCutIntoCells(double height, double inset)
        {
            // Each upright edge of the side, 0.0009 or 0.001 long, is no edge, and was left out beside its copy on the face
            // next to it, which then read open; kept with that copy, its corners within the tolerance of the copy's, it closes.
            GeoSolid3 slab = ShortSidedSlab(height, inset);

            Assert.True(slab.Validate(Tol).IsClosed, slab.Validate(Tol).ToString());
            Assert.True(slab.IsClosed(Tol));
            Assert.Equal(4, Mesh3.ToCells(slab, CellOptions3.Divide(2, 2, 1), Tol).Cells.Count);
        }

        /// <summary>
        /// A cube of 100 whose top has a square hole the width given across at its middle, open, with a sliver triangle in
        /// the top's plane in each side of the hole: its edge 0.00095 long, as far in from each end of the side, and its apex
        /// 0.0001 in towards the middle, every edge of it no longer than the tolerance.
        /// </summary>
        private static GeoSolid3 HoledTopWithSpecks(double width)
        {
            var fine = new Tolerance(1E-9, 1E-9, 1E-9, 1E-9);
            double h = width / 2, inset = (width - 0.00095) / 2;
            GeoPoint3[] square = { P(50 - h, 50 - h, 100), P(50 + h, 50 - h, 100), P(50 + h, 50 + h, 100), P(50 - h, 50 + h, 100) };
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(new[] { P(0, 0, 100), P(100, 0, 100), P(100, 100, 100), P(0, 100, 100) }, fine), new[] { new GeoPolygon3(square, fine) }, fine),
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0))),
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(100, 0, 100), P(0, 0, 100))),
                new GeoFace3(new GeoPolygon3(P(100, 0, 0), P(100, 100, 0), P(100, 100, 100), P(100, 0, 100))),
                new GeoFace3(new GeoPolygon3(P(100, 100, 0), P(0, 100, 0), P(0, 100, 100), P(100, 100, 100))),
                new GeoFace3(new GeoPolygon3(P(0, 100, 0), P(0, 0, 0), P(0, 0, 100), P(0, 100, 100))),
            };

            for (int k = 0; k < 4; k++)
            {
                GeoPoint3 a = square[k], b = square[(k + 1) % 4];
                GeoVector3 along = a.GetVectorTo(b).Multiply(1.0 / width);
                GeoPoint3 s0 = a.Add(along.Multiply(inset)), s1 = b.Add(along.Multiply(-inset));
                GeoPoint3 middle = P((s0.X + s1.X) / 2, (s0.Y + s1.Y) / 2, 100);
                GeoVector3 inward = middle.GetVectorTo(P(50, 50, 100));
                GeoPoint3 apex = middle.Add(inward.Multiply(0.0001 / inward.Length));
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { s1, s0, apex }, fine)));
            }

            return new GeoSolid3(faces);
        }

        [Theory]
        [InlineData(0.0012)]
        [InlineData(0.0015)]
        [InlineData(0.0019)]
        public void AHoleWiderThanTheToleranceWithASpeckInEachSide_IsStillOpen(double width)
        {
            // Each speck's edge lies within the tolerance of a side of the hole at both its ends; kept as the side's copy, its
            // stops cut the open side into stretches no longer than the tolerance, and the hole read closed. A speck keeps none
            // of its edges: none of them is longer than the tolerance.
            GeoSolid3 cube = HoledTopWithSpecks(width);

            Assert.False(cube.Validate(Tol).IsClosed, cube.Validate(Tol).ToString());
            Assert.False(cube.IsClosed(Tol));
        }

        [Fact]
        public void TryClose_ReadsTheBodyAsTheBooleansDo_NotAsValidate()
        {
            // Validate pairs the side's upright edges with their copies by their ends; the closing pairs them by overlap, as
            // the booleans read what they make, and so refuses one slab and welds the other, though Validate calls both valid.
            var options = new SolidClosingOptions(Tol, 0.01, 0.0, 0.0, FillStrategy.None);
            GeoSolid3 refused = SlidSlab(0.0012, 0.0002);
            GeoSolid3 welded = SlidSlab(0.0014, 0.0005);

            Assert.True(refused.Validate(Tol).IsValid);
            Assert.False(refused.TryClose(out _, options, out SolidClosing3 report));
            Assert.Equal(ClosingFailure.GapTooWide, report.Failure);

            Assert.True(welded.Validate(Tol).IsValid);
            Assert.True(welded.TryClose(out GeoSolid3 closed, options, out _));
            Assert.NotSame(welded, closed);
            Assert.InRange(closed.GetVolume(Tol), 11.6, 11.7);
        }

        [Fact]
        public void ACubeLessABoxThroughIt_FarAndTurned_AtAToleranceOfNought_LeavesWhatTheBoxLeaves()
        {
            // Read within the rounding, what the cut made at nought, unraised, was once taken for the cube itself, 1 000 000
            // where 800 000 is left, and nothing was logged; it was then not worked out. Below four tenths of the rounding the
            // difference is worked out within the rounding now and welded to close at nought.
            GeoTransform3 place = Placement(3);
            GeoSolid3 cube = new GeoObb3(P(50, 50, 50), 100, 100, 100).ToSolid().TransformBy(place);
            GeoSolid3 box = new GeoObb3(P(40, 45, 50), 40, 50, 120).ToSolid().TransformBy(place);

            Assert.True(cube.TrySubtract(box, out GeoSolid3 rest, Nought, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(800_000, rest.GrossVolume, 1E-6 * 800_000);
            Assert.True(rest.Validate(Nought).IsValid);
        }

        [Fact]
        public void AUnionClosedOnlyToTheRounding_IsNotCutIntoCellsAtAToleranceOfNought()
        {
            // Closed to IsClosed at nought, the union was cut into no cell at all, its 1 835 000 cubic millimetres lost.
            GeoTransform3 turn = Placement(1);
            GeoSolid3 a = new GeoObb3(P(50, 50, 50), 100, 100, 100).ToSolid().TransformBy(turn);
            GeoSolid3 b = new GeoObb3(P(100, 90, 110), 100, 80, 120).ToSolid().TransformBy(turn);
            Assert.True(a.TryUnion(b, out GeoSolid3 union, Tol));

            Assert.Throws<ArgumentException>(() => Mesh3.ToCells(union, CellOptions3.Divide(2, 2, 2), Nought));
            Assert.Equal(8, Mesh3.ToCells(union, CellOptions3.Divide(2, 2, 2), Tol).Cells.Count);
        }

        [Fact]
        public void ACubeOpenByAGapOverTheToleranceAcrossSpecks_IsNotCutIntoCells()
        {
            // Read closed by its corners, it was cut into 8 cells; open to IsClosed, it holds no volume to cut.
            GeoSolid3 cube = LiftedTop(1.5 * Tol.EqualPoint, true, true, 0);

            Assert.Throws<ArgumentException>(() => Mesh3.ToCells(cube, CellOptions3.Divide(2, 2, 2), Tol));
        }
    }
}
