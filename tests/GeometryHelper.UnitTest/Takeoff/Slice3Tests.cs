using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Takeoff.VolumeTakeoffTestKit;

namespace GeometryHelper.UnitTest.Takeoff
{
    /// <summary>
    /// The volume of a subject within some bodies and outside others, worked out by slicing: against the cells of boxes,
    /// fixed and random, with openings and copies; against volumes known by hand for turned boxes, leaning columns and
    /// bars, round columns, wedges, a tetrahedron and leaning planes; and on input that is empty, wholly outside, open, or
    /// far from the origin; see <see cref="Slice3.TryVolume"/>.
    /// </summary>
    /// <remarks>
    /// The boxes are those of <see cref="VolumeTakeoffTestKit"/>, and every answer is held within a part in a billion of
    /// the subject's outline. The slicing is exact for bodies bounded by planes, so the only error is rounding, as
    /// long as its levels hold every height where the section stops growing as one polynomial: the corners of the bodies,
    /// and where an edge of one crosses a face of another, or faces of three meet.
    /// </remarks>
    public class Slice3Tests
    {
        private const int Sets = 1000;

        // Slices and asks it to succeed: the volume of the subject within any of the bodies within and outside all those outside.
        private static double Slice(GeoSolid3 subject, IReadOnlyList<GeoSolid3> within, IReadOnlyList<GeoSolid3> outside)
        {
            Assert.True(Slice3.TryVolume(subject, within, outside, out double volume), "the slicing failed");
            return volume;
        }

        private static GeoSolid3[] None => new GeoSolid3[0];

        #region Against the cells of boxes

        [Fact]
        public void AThousandRandomSetsOfBoxes_AgreeWithTheCells_ToAPartInABillionOfTheSubject()
        {
            // The sets of RandomSliceSet, seeded 0 to 999, so a failure names the seed that shows it: flush faces, openings
            // flush or running past, and copies of the subject, its very solid among them, within and outside.
            var failures = new List<string>();

            for (int seed = 0; seed < Sets && failures.Count < 20; seed++)
            {
                SliceSet set = RandomSliceSet(seed);
                double expected = set.Oracle();
                double within = Relative * set.SubjectOutline();
                (GeoSolid3 subject, GeoSolid3[] inside, GeoSolid3[] outside) = set.ToSolids();

                if (!Slice3.TryVolume(subject, inside, outside, out double volume))
                {
                    failures.Add($"seed {seed}: the slicing failed\n{set}");
                }
                else if (!(Math.Abs(volume - expected) <= within))
                {
                    failures.Add($"seed {seed}: {volume:R}, the cells' {expected:R}, off by {volume - expected:G3}\n{set}");
                }
            }

            Assert.Empty(failures);
        }

        [Fact]
        public void TheSlabWithinTheBeamOutsideTheColumn_IsWhatTheBeamTakesFromIt()
        {
            // The joint of VolumeTakeoffTestKit.Joint: the slab within the beam is 3 000 by 400 by 200, 240 000 000, less the
            // block the column holds, 24 000 000: the 216 000 000 the beam takes from the slab in the takeoff.
            BoxSet joint = Joint();
            GeoSolid3 slab = joint.Boxes[0].ToSolid(), beam = joint.Boxes[1].ToSolid(), column = joint.Boxes[2].ToSolid();

            Assert.Equal(2.16E8, Slice(slab, new[] { beam }, new[] { column }), Relative * 1.8E9);
            Assert.Equal(2.4E8, Slice(slab, new[] { beam }, None), Relative * 1.8E9);
            Assert.Equal(2.4E7, Slice(slab, new[] { column }, None), Relative * 1.8E9);
        }

        [Fact]
        public void TheLastOfFourAtACorner_WithinTheSecondOutsideTheFirst_IsTheRestOfTheirSlab()
        {
            // The fourth cube shares a slab 200 by 1 000 by 1 000 with the second, 200 000 000, of which the post the first
            // holds too, 200 by 200 by 1 000, is outside: 160 000 000. Within both the first and the second it is their
            // slabs together, 360 000 000.
            GeoSolid3[] cubes = FourAtACorner().Boxes.Select(box => box.ToSolid()).ToArray();

            Assert.Equal(1.6E8, Slice(cubes[3], new[] { cubes[1] }, new[] { cubes[0] }), Relative * 1E9);
            Assert.Equal(3.6E8, Slice(cubes[3], new[] { cubes[0], cubes[1], cubes[2] }, None), Relative * 1E9);
        }

        [Fact]
        public void TheOrderOfTheBodiesWithinAndOutside_DoesNotMatter()
        {
            // The fourth cube within the other three, 360 000 000, and outside the first and the third: listed either way
            // round, what of it only the second holds, 160 000 000.
            GeoSolid3[] cubes = FourAtACorner().Boxes.Select(box => box.ToSolid()).ToArray();

            double forward = Slice(cubes[3], new[] { cubes[0], cubes[1], cubes[2] }, new[] { cubes[0], cubes[2] });
            double backward = Slice(cubes[3], new[] { cubes[2], cubes[1], cubes[0] }, new[] { cubes[2], cubes[0] });

            Assert.Equal(1.6E8, forward, Relative * 1E9);
            Assert.Equal(forward, backward, Relative * 1E9);
        }

        #endregion

        #region Openings

        [Fact]
        public void ASubjectWithAnOpening_CountsOnlyItsMaterial()
        {
            // The slab of ColumnThroughADuct within its column: 400 by 400 by 200 of the slab is in the column, 32 000 000,
            // a quarter of it in the duct, so 24 000 000.
            BoxSet set = ColumnThroughADuct();

            Assert.Equal(2.4E7, Slice(set.Boxes[0].ToSolid(), new[] { set.Boxes[1].ToSolid() }, None), Relative * 1.728E9);
        }

        [Fact]
        public void ABodyWithinWithAnOpening_LeavesOutWhatLiesInTheOpening()
        {
            // The slab of HollowColumnThroughASlab within the hollow column: the column's ring, 120 000, by the slab's 200,
            // 24 000 000; the 200 by 200 by 200 in the duct is no material of the column.
            BoxSet set = HollowColumnThroughASlab();

            Assert.Equal(2.4E7, Slice(set.Boxes[0].ToSolid(), new[] { set.Boxes[1].ToSolid() }, None), Relative * 1.152E9);
        }

        [Fact]
        public void ABodyOutsideWithAnOpening_KeepsWhatLiesInTheOpening()
        {
            // The same slab, 1 152 000 000, within a box holding all of it and outside the hollow column: it loses the ring,
            // 24 000 000, and keeps the 8 000 000 in the duct, 1 128 000 000.
            BoxSet set = HollowColumnThroughASlab();
            GeoSolid3 all = new Box(-2000, -2000, -2000, 2000, 2000, 2000).ToSolid();

            Assert.Equal(1.128E9, Slice(set.Boxes[0].ToSolid(), new[] { all }, new[] { set.Boxes[1].ToSolid() }), Relative * 1.152E9);
        }

        [Fact]
        public void ABlockWithinACubeNotchedWhereItLies_IsNought()
        {
            // The block of BlockInANotch fills the cube's notch: their boxes share 250 000 000, their material nothing.
            BoxSet set = BlockInANotch();

            Assert.Equal(0.0, Slice(set.Boxes[1].ToSolid(), new[] { set.Boxes[0].ToSolid() }, None), Relative * 2.5E8);
        }

        #endregion

        #region Degenerate input

        [Fact]
        public void NothingWithin_IsNought()
        {
            Assert.Equal(0.0, Slice(Joint().Boxes[0].ToSolid(), None, None), Relative * 1.8E9);
        }

        [Fact]
        public void ASubjectWhollyInsideABodyOutside_IsNought()
        {
            // The block of BlockInACube, 300 across, within itself but outside the cube 1 000 across that holds it.
            BoxSet set = BlockInACube(0, 0);
            GeoSolid3 block = set.Boxes[0].ToSolid();

            Assert.Equal(0.0, Slice(block, new[] { block }, new[] { set.Boxes[1].ToSolid() }), Relative * 2.7E7);
        }

        [Fact]
        public void ASubjectWithinItselfOrACopy_IsAllOfIt_AndOutsideItself_IsNought()
        {
            // The block, 27 000 000, within its very solid and within a copy built on its own; then outside itself.
            Box box = BlockInACube(0, 0).Boxes[0];
            GeoSolid3 block = box.ToSolid();

            Assert.Equal(2.7E7, Slice(block, new[] { block }, None), Relative * 2.7E7);
            Assert.Equal(2.7E7, Slice(block, new[] { box.ToSolid() }, None), Relative * 2.7E7);
            Assert.Equal(0.0, Slice(block, new[] { block }, new[] { block }), Relative * 2.7E7);
        }

        [Fact]
        public void BodiesWithinThatOnlyTouchTheSubject_GiveNought()
        {
            // The cube of Touching within the four that touch it at a face, an edge, a corner and its top, flush.
            GeoSolid3[] parts = Touching().Boxes.Select(box => box.ToSolid()).ToArray();

            Assert.Equal(0.0, Slice(parts[0], parts.Skip(1).ToArray(), None), Relative * 1E9);
        }

        [Theory]
        [InlineData("subject")]
        [InlineData("within")]
        [InlineData("outside")]
        public void ATetrahedronMissingHalfItsSlantedFace_IsRefused_WhereverItIs(string role)
        {
            // The tetrahedron 300 by 300 by 300, 4 500 000, inside a cube 1 000 across is sliced in any of the three places;
            // with half its slanted face left out, every level through it cuts it in a triangle with half its long side
            // missing, so a line across the section, along x or along y, meets it an odd number of times somewhere. The
            // data is not a body, and the slicing says so rather than guess.
            var move = new GeoVector3(100, 100, 100);
            GeoSolid3 closed = Tetrahedron(300, 300, 300).Translate(move);
            GeoSolid3 open = Tetrahedron(300, 300, 300, leaveOutHalfTheSlantedFace: true).Translate(move);
            GeoSolid3 cube = new Box(0, 0, 0, 1000, 1000, 1000).ToSolid();

            bool Sliced(GeoSolid3 tetrahedron, out double volume)
                => role == "subject" ? Slice3.TryVolume(tetrahedron, new[] { cube }, None, out volume)
                : role == "within" ? Slice3.TryVolume(cube, new[] { tetrahedron }, None, out volume)
                : Slice3.TryVolume(cube, new[] { cube }, new[] { tetrahedron }, out volume);

            Assert.True(Sliced(closed, out double whole));
            Assert.Equal(role == "outside" ? 1E9 - 4.5E6 : 4.5E6, whole, Relative * 1E9);
            Assert.False(Sliced(open, out _));
        }

        [Fact]
        public void LevelsAHairApart_AreNoTrouble()
        {
            // A cube 1 000 across within a box 2 000 across whose bottom lies 1E-7 above the cube's: the two levels are closer
            // than the slicing tells apart, and what lies between them, 1E6 by 1E-7, a tenth, is far below a part in a billion
            // of the cube's 1E9 either way it is read.
            GeoSolid3 cube = new Box(0, 0, 0, 1000, 1000, 1000).ToSolid();
            GeoSolid3 above = new GeoAabb3(new GeoPoint3(-500, -500, 1E-7), new GeoPoint3(1500, 1500, 2000)).ToObb().ToSolid();

            Assert.Equal(1E9, Slice(cube, new[] { above }, None), Relative * 1E9);
        }

        [Fact]
        public void FarFromTheOrigin_TheVolumeIsTheSame()
        {
            // The joint of the slab, beam and column moved by 150 000, 31 000 and 10 900, whole millimetres, as a Tekla model
            // lies: the slab within the beam outside the column is still 216 000 000, to a part in a billion.
            var move = new GeoVector3(150000, 31000, 10900);
            BoxSet joint = Joint();
            GeoSolid3[] parts = joint.Boxes.Select(box => box.ToSolid().Translate(move)).ToArray();

            Assert.Equal(2.16E8, Slice(parts[0], new[] { parts[1] }, new[] { parts[2] }), Relative * 1.8E9);
        }

        [Fact]
        public void TheSameCallTwice_GivesTheSameVolumeBitForBit_AndLeavesTheBodiesAsTheyWere()
        {
            // The slab of ColumnThroughADuct, with its duct, within the column and outside a block over a corner of it.
            BoxSet set = ColumnThroughADuct();
            GeoSolid3 slab = set.Boxes[0].ToSolid(), column = set.Boxes[1].ToSolid();
            GeoSolid3 block = new Box(1300, 1300, -500, 1500, 1500, 500).ToSolid();
            GeoSolid3[] all = { slab, column, block };
            int[] faces = all.Select(body => body.Faces.Count).ToArray();
            long[] volumes = all.Select(body => BitConverter.DoubleToInt64Bits(body.GetVolume(Tolerance.Default))).ToArray();

            double first = Slice(slab, new[] { column }, new[] { block });
            double second = Slice(slab, new[] { column }, new[] { block });

            Assert.Equal(BitConverter.DoubleToInt64Bits(first), BitConverter.DoubleToInt64Bits(second));
            Assert.Equal(faces, all.Select(body => body.Faces.Count));
            Assert.Equal(volumes, all.Select(body => BitConverter.DoubleToInt64Bits(body.GetVolume(Tolerance.Default))));
        }

        #endregion

        #region Bodies not lined up with the axes

        [Fact]
        public void AColumnTurnedThirtyDegreesThroughASlab_IsItsSectionTimesTheSlab_EitherWayRound()
        {
            // The column 300 by 400 by 2 000 about the origin, turned 30 degrees about z, crosses the slab 2 000 by 2 000 by
            // 200 wholly: its section, 120 000, by 200, 24 000 000, whichever is the subject.
            GeoSolid3 column = TurnedBox(new GeoPoint3(0, 0, 0), 300, 400, 2000, Math.PI / 6.0, 0.0);
            GeoSolid3 slab = new Box(-1000, -1000, 0, 1000, 1000, 200).ToSolid();

            Assert.Equal(2.4E7, Slice(slab, new[] { column }, None), Relative * 2.4E8);
            Assert.Equal(2.4E7, Slice(column, new[] { slab }, None), Relative * 2.4E8);
        }

        [Fact]
        public void AColumnLeaningTwentyDegreesThroughASlab_IsItsSectionOverTheCosineTimesTheSlab()
        {
            // The column 300 by 400 by 4 000 about the origin leans 20 degrees about x. Each level cuts it in 300 by
            // 400 / cos 20, 127 701.1, and the slab 200 thick about the origin is crossed wholly: 25 540 222.4. Its sides
            // lean over the levels, so the section moves between them, and the slicing must follow it.
            double lean = 20.0 * Math.PI / 180.0;
            GeoSolid3 column = TurnedBox(new GeoPoint3(0, 0, 0), 300, 400, 4000, 0.0, lean);
            GeoSolid3 slab = new Box(-2000, -2000, -100, 2000, 2000, 100).ToSolid();

            Assert.Equal(120000.0 / Math.Cos(lean) * 200.0, Slice(slab, new[] { column }, None), Relative * 1.6E9);
        }

        [Fact]
        public void ABarLeaningThroughACube_IsItsSectionTimesItsLengthInside_ThoughItsEdgesCrossTheFacesBetweenLevels()
        {
            // A bar 200 by 200 by 3 000 about the cube's centre, leaning 30 degrees off level about y, goes in through the
            // cube's face x = 0 and out through x = 1 000, clear of its top and bottom: what they share is its section,
            // 40 000, by its length between the two faces, 1 000 / cos 30, 46 188 021.5. Both bodies' corners give the
            // levels 0 and 1 000 only, but the bar's edges cross the two faces at heights 95.9, 326.8, 673.2 and 904.1,
            // where the section stops growing as a polynomial: those heights are levels too, or the slicing is not exact.
            double lean = Math.PI / 6.0;
            GeoSolid3 cube = new Box(0, 0, 0, 1000, 1000, 1000).ToSolid();
            GeoSolid3 bar = new GeoObb3(new GeoPoint3(500, 500, 500), 3000, 200, 200, new GeoVector3(Math.Cos(lean), 0, Math.Sin(lean)), new GeoVector3(0, 1, 0)).ToSolid();

            Assert.Equal(40000.0 * 1000.0 / Math.Cos(lean), Slice(cube, new[] { bar }, None), Relative * 1E9);
        }

        [Fact]
        public void ACubeUnderOneLeaningPlaneAndOverAnother_IsExact_ThoughThreeFacesMeetBetweenLevels()
        {
            // The cube 1 000 across within a block whose top is the plane z = 280 + 0.4 x + 0.05 y, and outside a block whose
            // bottom is z = 650 + 0.1 x - 0.3 y, both far wider than the cube: what is left stands up to the lower of the two
            // planes. They meet along 0.3 x + 0.35 y = 370, so the cube keeps the first plane over 1 880 000 / 3 of its plan
            // and the second over the other 1 120 000 / 3: 4 231 400 000 / 9, 470 155 555.6. That line leaves the cube
            // through its faces y = 1 000 and x = 1 000 at heights 356.7 and 690, where three faces of three bodies meet and
            // no edge of any of them crosses a face: those heights are levels too, or the slicing is not exact.
            GeoSolid3 cube = new Box(0, 0, 0, 1000, 1000, 1000).ToSolid();
            GeoSolid3 under = BlockBetweenPlanes(-1000, -1000, 2000, 2000, (-2000, 0, 0), (280, 0.4, 0.05));
            GeoSolid3 over = BlockBetweenPlanes(-1000, -1000, 2000, 2000, (650, 0.1, -0.3), (3000, 0, 0));

            Assert.Equal(4231400000.0 / 9.0, Slice(cube, new[] { under }, new[] { over }), Relative * 1E9);
        }

        [Fact]
        public void ABoxTurnedFortyFiveDegreesWithinItsUprightCopy_KeepsTheOctagonTheyShare()
        {
            // Two boxes 1 000 by 1 000 by 300 about the origin, one turned 45 degrees about z: they share the regular octagon
            // of inradius 500, 2 times 1 000 squared times (root 2 - 1), 828 427.1, by 300.
            GeoSolid3 upright = new Box(-500, -500, 0, 500, 500, 300).ToSolid();
            GeoSolid3 turned = TurnedBox(new GeoPoint3(0, 0, 150), 1000, 1000, 300, Math.PI / 4.0, 0.0);

            Assert.Equal(2.0 * 1E6 * (Math.Sqrt(2.0) - 1.0) * 300.0, Slice(turned, new[] { upright }, None), Relative * 3E8);
        }

        [Fact]
        public void ARoundColumnThroughASlab_IsItsPolygonTimesTheSlab()
        {
            // A column of radius 200 on 32 sides, corners on the circle, 2 000 long, through a slab 200 thick: 16 times 200
            // squared times sin(pi / 16), 124 857.8, by 200.
            GeoSolid3 column = GeoSolid3.Cylinder(new GeoPoint3(0, 0, -1000), new GeoPoint3(0, 0, 1000), 200, 32, Tolerance.Default);
            GeoSolid3 slab = new Box(-1000, -1000, 0, 1000, 1000, 200).ToSolid();

            Assert.Equal(16.0 * 200.0 * 200.0 * Math.Sin(Math.PI / 16.0) * 200.0, Slice(slab, new[] { column }, None), Relative * 8E8);
        }

        [Fact]
        public void APipeLyingThroughAWall_IsItsPolygonTimesTheWall()
        {
            // A pipe of radius 200 on 24 sides lying along x at height 500, 2 000 long, through a wall 200 thick across it: 12
            // times 200 squared times sin(pi / 12), 124 233.0, by 200. Every level cuts the pipe in a strip whose width
            // changes between its corners, and the slicing must add those up exactly.
            GeoSolid3 pipe = GeoSolid3.Cylinder(new GeoPoint3(-1000, 0, 500), new GeoPoint3(1000, 0, 500), 200, 24, Tolerance.Default);
            GeoSolid3 wall = new Box(0, -1000, 0, 200, 1000, 1000).ToSolid();

            Assert.Equal(12.0 * 200.0 * 200.0 * Math.Sin(Math.PI / 12.0) * 200.0, Slice(wall, new[] { pipe }, None), Relative * 4E8);
        }

        [Fact]
        public void AWedgeBelowALevel_IsTheTrapezoidUnderIt()
        {
            // The wedge of the triangle 1 000 long and 500 high, 250 000, swept 300 wide. Above z = 200 lies a triangle 0.6 of
            // it across, 90 000, so under it 160 000 by 300, 48 000 000.
            GeoSolid3 wedge = Wedge(1000, 500, 300);
            GeoSolid3 below = new Box(-100, -100, -100, 1100, 400, 200).ToSolid();

            Assert.Equal(4.8E7, Slice(wedge, new[] { below }, None), Relative * 7.5E7);
            Assert.Equal(7.5E7 - 4.8E7, Slice(wedge, new[] { wedge }, new[] { below }), Relative * 7.5E7);
        }

        [Fact]
        public void ATetrahedron_IsExactThoughItsSectionGrowsAsTheSquare()
        {
            // The tetrahedron 600 by 900 by 1 200, 108 000 000. Its section shrinks as (1 - z / 1 200) squared, a polynomial
            // of degree two between its only two levels; under z = 400 lies 1 - (2 / 3) cubed of it, 19 / 27, 76 000 000.
            GeoSolid3 tetrahedron = Tetrahedron(600, 900, 1200);
            GeoSolid3 all = new Box(-100, -100, -100, 1000, 1000, 1300).ToSolid();
            GeoSolid3 below = new Box(-100, -100, -100, 1000, 1000, 400).ToSolid();

            Assert.Equal(1.08E8, Slice(tetrahedron, new[] { all }, None), Relative * 1.08E8);
            Assert.Equal(7.6E7, Slice(tetrahedron, new[] { below }, None), Relative * 1.08E8);
        }

        #endregion
    }
}
