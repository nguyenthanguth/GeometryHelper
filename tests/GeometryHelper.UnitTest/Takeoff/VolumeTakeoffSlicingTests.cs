using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Takeoff;
using Xunit;
using static GeometryHelper.UnitTest.Takeoff.VolumeTakeoffTestKit;

namespace GeometryHelper.UnitTest.Takeoff
{
    /// <summary>
    /// Overlaps the booleans cannot work out, taken off by slicing instead: a curved wall and a girder on it whose sides and
    /// tops lie a hair apart, alone and with a column through both; and boxes whose booleans the test hooks break, where
    /// the oracle of <see cref="VolumeTakeoffTestKit"/> says what slicing must give; see <see cref="VolumeTakeoff"/>.
    /// </summary>
    /// <remarks>
    /// The bodies are those of <see cref="CurvedWallAndGirder"/> and <see cref="ColumnAcrossTheCurve"/>, and what they
    /// share is worked out there by hand, plan by plan, with no call into the library. A volume the slicing gives is exact,
    /// so it is held within a part in a billion of the part's gross, and the part has no issue.
    /// </remarks>
    public class VolumeTakeoffSlicingTests
    {
        private static readonly SolidBooleanOptions Plain = new SolidBooleanOptions(Tolerance.Default);

        private static readonly SolidBooleanOptions Contact = new SolidBooleanOptions(Tolerance.Default, 0.01, new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01));

        /// <summary>The two settings of the boolean each case runs with.</summary>
        public static IEnumerable<object[]> Settings() => new[] { new object[] { "plain" }, new object[] { "contact" } };

        private static VolumeTakeoffOptions Options(string setting) => new VolumeTakeoffOptions(setting == "contact" ? Contact : Plain);

        // The guard of the cases built to break the boolean: if the common part of the pair ever comes out valid, they no
        // longer reach the slicing and must be built again, so the test says so rather than pass on a path it never took.
        private static void AssertTheBooleanCannotMeet(GeoSolid3 loser, GeoSolid3 keeper, SolidBooleanOptions boolean)
        {
            bool made = Boolean3.TryIntersect(loser, keeper, out GeoSolid3 piece, boolean, out BooleanOutcome outcome);

            Assert.False(made && piece.Validate(boolean.Tolerance).IsValid, $"the common part came out valid ({outcome}, {piece?.Faces.Count} faces): the case no longer reaches the slicing, build it again");
        }

        private static IReadOnlyList<VolumeTakeoffResult> RunCountingSlicedPairs(VolumeItem[] items, VolumeTakeoffOptions options, out int slicedPairs)
            => RunCountingSlicedPairs(items, options, out slicedPairs, out _);

        // Runs the items with every message caught, and gives the number of pairs the takeoff's Debug line says it sliced
        // because the booleans could not make their common part, and how many of those it says were near-coincident and
        // sent straight to slicing; both 0 where there is no such line, and the second -1 where the line does not say. The
        // writer and the switch are put back whatever happens.
        private static IReadOnlyList<VolumeTakeoffResult> RunCountingSlicedPairs(VolumeItem[] items, VolumeTakeoffOptions options, out int slicedPairs, out int straight)
        {
            var debug = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> writer = GeometryHelperLog.Writer;
            bool enable = GeometryHelperLog.Enable;
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (level == GeometryHelperLogLevel.Debug)
                {
                    debug.Add(message);
                }
            };
            GeometryHelperLog.Enable = true;

            try
            {
                IReadOnlyList<VolumeTakeoffResult> results = VolumeTakeoff.Run(items, options);
                Match line = debug.Select(message => Regex.Match(message, @"^VolumeTakeoff: (\d+) pairs whose common part the booleans could not make(, (\d+) of them near-coincident)?")).FirstOrDefault(match => match.Success);
                slicedPairs = line == null ? 0 : int.Parse(line.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                straight = line == null ? 0 : line.Groups[3].Success ? int.Parse(line.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
                return results;
            }
            finally
            {
                GeometryHelperLog.Writer = writer;
                GeometryHelperLog.Enable = enable;
            }
        }

        private static void AssertTaken(VolumeTakeoffResult result, double gross, double net, params (int By, double Volume)[] deductions)
        {
            double within = Relative * gross;
            Assert.Equal(gross, result.GrossVolume, within);
            Assert.Equal(net, result.NetVolume, within);
            Assert.Equal(deductions.Select(d => d.By), result.Deductions.Select(d => d.ByIndex));
            for (int i = 0; i < deductions.Length; i++)
            {
                Assert.Equal(deductions[i].Volume, result.Deductions[i].Volume, within);
            }
        }

        #region A real boolean failure

        [Theory]
        [MemberData(nameof(Settings))]
        public void ACurvedWallAndAGirderTheBooleanCannotMeet_TheWallLosesWhatTheyShare_WorkedOutBySlicing(string setting)
        {
            // The wall, 1 842 210 349.5, priority 1, under the girder, 762 080 321.7, priority 2: the girder keeps the
            // 449 090 144.2 they share, its depth over their common plan less the 170 322.6 where its top stands above the
            // wall's. Neither the common part nor the cut is valid, so the overlap is sliced: exact, and no issue. At
            // 4dde2c6 the wall says the overlap could not be worked out and keeps all of itself.
            (CurvedBand wall, CurvedBand girder) = CurvedWallAndGirder();
            VolumeItem[] items = { new VolumeItem(wall.ToSolid(), "wall", 1), new VolumeItem(girder.ToSolid(), "girder", 2) };
            double shared = CurvedOverlap(wall, girder);
            AssertTheBooleanCannotMeet(items[0].Solid, items[1].Solid, Options(setting).Boolean);

            IReadOnlyList<VolumeTakeoffResult> results = RunCountingSlicedPairs(items, Options(setting), out int slicedPairs);

            Assert.Equal(1, slicedPairs);
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
            AssertTaken(results[0], BandVolume(wall), BandVolume(wall) - shared, (1, shared));
            AssertTaken(results[1], BandVolume(girder), BandVolume(girder));
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void TheSameWithAColumnThroughBoth_TheSliceLeavesOutWhatTheColumnTookFirst(string setting)
        {
            // A column 1 000 by 400 by 4 000, 1 600 000 000, priority 3, through the middle of both: it takes from the wall
            // its 432 064 861.1 there and from the girder its 180 022 369.3, both valid common parts. What the wall shares
            // with the girder, 449 090 144.2, it then shares with the column too where the column stands, 107 956 479.2, which
            // the column has taken already: the slice of the wall within the girder and outside the column gives the girder
            // only 341 133 665.0. At 4dde2c6 the girder takes nothing, with an issue.
            (CurvedBand wall, CurvedBand girder) = CurvedWallAndGirder();
            (GeoSolid3 column, double[][] plan) = ColumnAcrossTheCurve();
            VolumeItem[] items =
            {
                new VolumeItem(wall.ToSolid(), "wall", 1),
                new VolumeItem(girder.ToSolid(), "girder", 2),
                new VolumeItem(column, "column", 3),
            };
            double wallInColumn = BandVolume(wall, plan), girderInColumn = BandVolume(girder, plan);
            double wallInGirder = CurvedOverlap(wall, girder) - CurvedOverlap(wall, girder, plan);
            AssertTheBooleanCannotMeet(items[0].Solid, items[1].Solid, Options(setting).Boolean);

            IReadOnlyList<VolumeTakeoffResult> results = RunCountingSlicedPairs(items, Options(setting), out int slicedPairs);

            Assert.InRange(slicedPairs, 1, 3);
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
            AssertTaken(results[0], BandVolume(wall), BandVolume(wall) - wallInColumn - wallInGirder, (2, wallInColumn), (1, wallInGirder));
            AssertTaken(results[1], BandVolume(girder), BandVolume(girder) - girderInColumn, (2, girderInColumn));
            AssertTaken(results[2], 1.6E9, 1.6E9);
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void TheGirderRankedOverTheColumn_TheColumnsPieceLessTheGirder_IsExactToo(string setting)
        {
            // The same three, the girder now ranked first, priority 3, and the column second, priority 2. The wall gives the
            // girder all they share, 449 090 144.2, by slicing. The column's piece of the wall, 432 064 861.1, a valid body,
            // then has the girder's solid taken out of it, as an overlap with no body is taken out: what is left,
            // 324 108 381.9, is what the column takes. That cut meets the same warped tops a hair apart that broke the
            // pair, and at 4dde2c6 it comes out valid and 6.2 over, 3.4E-9 of the wall.
            (CurvedBand wall, CurvedBand girder) = CurvedWallAndGirder();
            (GeoSolid3 column, double[][] plan) = ColumnAcrossTheCurve();
            VolumeItem[] items =
            {
                new VolumeItem(wall.ToSolid(), "wall", 1),
                new VolumeItem(girder.ToSolid(), "girder", 3),
                new VolumeItem(column, "column", 2),
            };
            double wallInGirder = CurvedOverlap(wall, girder);
            double wallInColumn = BandVolume(wall, plan) - CurvedOverlap(wall, girder, plan);
            double girderInColumn = BandVolume(girder, plan);
            AssertTheBooleanCannotMeet(items[0].Solid, items[1].Solid, Options(setting).Boolean);

            IReadOnlyList<VolumeTakeoffResult> results = RunCountingSlicedPairs(items, Options(setting), out int slicedPairs);

            Assert.InRange(slicedPairs, 1, 3);
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
            AssertTaken(results[0], BandVolume(wall), BandVolume(wall) - wallInGirder - wallInColumn, (1, wallInGirder), (2, wallInColumn));
            AssertTaken(results[1], BandVolume(girder), BandVolume(girder));
            AssertTaken(results[2], 1.6E9, 1.6E9 - girderInColumn, (1, girderInColumn));
        }

        #endregion

        #region Near-coincident pairs, sent straight to slicing

        [Theory]
        [MemberData(nameof(Settings))]
        public void ACurvedWallAndGirderInManyChords_AreSentStraightToSlicing_AndTakenOffExactly(string setting)
        {
            // The curved wall and girder of CurvedWallAndGirder in 90 and 70 chords: hundreds of pairs of their faces stand
            // beside each other, 0.1 degrees or less off parallel and within a millimetre, so the boolean is not tried and
            // the overlap is sliced, the Debug line counting the pair as sent straight. The wall gives the girder all they
            // share, as the plans work it out, with no issue.
            (CurvedBand wall, CurvedBand girder) = CurvedWallAndGirder(90, 70);
            VolumeItem[] items = { new VolumeItem(wall.ToSolid(), "wall", 1), new VolumeItem(girder.ToSolid(), "girder", 2) };
            double shared = CurvedOverlap(wall, girder);

            IReadOnlyList<VolumeTakeoffResult> results = RunCountingSlicedPairs(items, Options(setting), out int slicedPairs, out int straight);

            Assert.Equal(1, slicedPairs);
            Assert.Equal(1, straight);
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
            AssertTaken(results[0], BandVolume(wall), BandVolume(wall) - shared, (1, shared));
            AssertTaken(results[1], BandVolume(girder), BandVolume(girder));
        }

        [Fact]
        public void BandsExactlyParallelHalfAMillimetreApart_AreLeftToTheBooleans()
        {
            // The wall and girder of ParallelWallAndGirder, the girder's inner side 0.5 out from the wall's and its top 0.5
            // under: every face beside another is parallel to it to rounding, which the booleans do well, so none is sent
            // straight to slicing. The common part is the girder's depth over their common plan.
            (CurvedBand wall, CurvedBand girder) = ParallelWallAndGirder(0.5, 0.0);
            VolumeItem[] items = { new VolumeItem(wall.ToSolid(), "wall", 1), new VolumeItem(girder.ToSolid(), "girder", 2) };
            double shared = CurvedOverlap(wall, girder);

            IReadOnlyList<VolumeTakeoffResult> results = RunCountingSlicedPairs(items, Options("plain"), out _, out int straight);

            Assert.Equal(0, straight);
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
            AssertTaken(results[0], BandVolume(wall), BandVolume(wall) - shared, (1, shared));
        }

        [Fact]
        public void ABoxTurnedAHairOffItsNeighbour_WithFewFacesBeside_IsLeftToTheBooleans()
        {
            // A cube 1 000 across, priority 0, and a block 600 by 1 000 by 1 000, priority 1, turned 0.05 degrees about z,
            // its end 0.5 inside the cube's face x = 1 000 at its middle: that end and its two long sides stand beside the
            // cube's faces, 0.05 degrees off and within a millimetre, but three pairs are far too few to send the pair
            // straight to slicing. The cube gives the block what of its plan lies in the cube's, by 1 000.
            double turn = 0.05 * Math.PI / 180.0;
            double ux = Math.Cos(turn), uy = Math.Sin(turn);
            var centre = new GeoPoint3(699.5, 500, 500);
            GeoSolid3 cube = new Box(0, 0, 0, 1000, 1000, 1000).ToSolid();
            GeoSolid3 block = new GeoObb3(centre, 600, 1000, 1000, new GeoVector3(ux, uy, 0), new GeoVector3(-uy, ux, 0)).ToSolid();
            double[][] square = { new[] { 0.0, 0.0 }, new[] { 1000.0, 0.0 }, new[] { 1000.0, 1000.0 }, new[] { 0.0, 1000.0 } };
            double[][] plan = new[] { (-300.0, -500.0), (300.0, -500.0), (300.0, 500.0), (-300.0, 500.0) }
                .Select(c => new[] { centre.X + c.Item1 * ux - c.Item2 * uy, centre.Y + c.Item1 * uy + c.Item2 * ux }).ToArray();
            double shared = 1000.0 * PlanOverlap(square, plan);
            VolumeItem[] items = { new VolumeItem(cube, "cube", 0), new VolumeItem(block, "block", 1) };

            IReadOnlyList<VolumeTakeoffResult> results = RunCountingSlicedPairs(items, Options("plain"), out _, out int straight);

            Assert.Equal(0, straight);
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
            AssertTaken(results[0], 1E9, 1E9 - shared, (1, shared));
        }

        [Fact]
        public void BandsFlushWithinThePointTolerance_AreLeftToTheBooleans()
        {
            // The bands of ParallelWallAndGirder with the girder's inner side on the wall's, its corners wobbled 0.0004 out
            // and in by turns: each of its inner faces is turned some 4E-5 radians from the wall's beside it, more than the
            // vector tolerance, but lies within the point tolerance of it, flush as the booleans read it, so none is sent
            // straight to slicing. The common part comes within the point tolerance times the two bodies' area.
            (CurvedBand wall, CurvedBand girder) = ParallelWallAndGirder(0.0, 0.0004);
            VolumeItem[] items = { new VolumeItem(wall.ToSolid(), "wall", 1), new VolumeItem(girder.ToSolid(), "girder", 2) };
            double shared = CurvedOverlap(wall, girder);
            double slack = Plain.Tolerance.EqualPoint * (items[0].Solid.GetSurfaceArea(Plain.Tolerance) + items[1].Solid.GetSurfaceArea(Plain.Tolerance));

            IReadOnlyList<VolumeTakeoffResult> results = RunCountingSlicedPairs(items, Options("plain"), out _, out int straight);

            Assert.Equal(0, straight);
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
            Assert.Equal(1, Assert.Single(results[0].Deductions).ByIndex);
            Assert.Equal(shared, results[0].Deductions[0].Volume, slack);
        }

        #endregion

        #region Driven by the test hooks

        // Runs the set with the hooks set as given, and puts them back to null whatever happens. The hooks are static, and
        // the assembly runs one test at a time, so no other test sees them.
        private static IReadOnlyList<VolumeTakeoffResult> RunBroken(VolumeItem[] items, VolumeTakeoffOptions options, Func<int, int, bool> intersection, Func<int, int, bool> takeOff)
        {
            VolumeTakeoff.BreakIntersection = intersection;
            VolumeTakeoff.BreakTakeOff = takeOff;
            try
            {
                return VolumeTakeoff.Run(items, options);
            }
            finally
            {
                VolumeTakeoff.BreakIntersection = null;
                VolumeTakeoff.BreakTakeOff = null;
            }
        }

        private static IEnumerable<(string Name, BoxSet Set)> FixedSets() => new[]
        {
            ("joint", Joint()),
            ("four at a corner", FourAtACorner()),
            ("three copies", ThreeCopies()),
            ("copies with a bar through", CopiesWithABarThrough()),
            ("block in a cube, kept by the cube", BlockInACube(1, 2)),
            ("block in a cube, kept by the block", BlockInACube(2, 1)),
            ("half overlap", HalfOverlap(1, 1)),
            ("extreme priorities", ExtremePriorities()),
            ("column through a duct", ColumnThroughADuct()),
            ("hollow column through a slab", HollowColumnThroughASlab()),
            ("block in a notch", BlockInANotch()),
        };

        [Theory]
        [MemberData(nameof(Settings))]
        public void EveryPairSliced_TheFixedSetsOfBoxes_AgreeWithTheOracle(string setting)
        {
            // Every common part taken as not made, so every overlap is sliced, each within its keeper and outside the
            // keepers before it: the joint's slab still loses the block all three share once, a later copy all of itself,
            // and an opening in the loser or the keeper is still no material. The block in the notch shares no material
            // with the cube, and its slice of nought is no deduction.
            var failures = new List<string>();

            foreach ((string name, BoxSet set) in FixedSets())
            {
                VolumeItem[] items = set.ToItems();
                List<string> wrong = Disagreements(items, RunBroken(items, Options(setting), (loser, keeper) => true, null), Oracle.Of(set));
                if (wrong.Count > 0)
                {
                    failures.Add(name + ":\n  " + string.Join("\n  ", wrong));
                }
            }

            Assert.Empty(failures);
        }

        [Fact]
        public void TheSlabsOverlapWithTheBeamSliced_TakesOffOnlyWhatTheColumnDidNotTake()
        {
            // The joint with only the slab's pair with the beam broken: the column's piece of the slab, 24 000 000, is a
            // body, and the beam's share is the slab within the beam and outside the column, 216 000 000, with no issue.
            BoxSet set = Joint();
            VolumeItem[] items = set.ToItems();

            IReadOnlyList<VolumeTakeoffResult> results = RunBroken(items, Options("plain"), (loser, keeper) => loser == 0 && keeper == 1, null);

            Assert.Empty(Disagreements(items, results, Oracle.Of(set)));
            AssertTaken(results[0], 1.8E9, 1.56E9, (2, 2.4E7), (1, 2.16E8));
        }

        [Fact]
        public void ACutInTheSecondPhaseThatSkips_IsSlicedInstead()
        {
            // The joint again, the beam's piece of the slab, 240 000 000, a body, but taking the column's piece out of it
            // taken as skipped: the slice of the slab within the beam and outside the column gives the same 216 000 000,
            // with no issue. At 4dde2c6 the piece would be taken off whole, 24 000 000 twice, with an issue.
            BoxSet set = Joint();
            VolumeItem[] items = set.ToItems();

            IReadOnlyList<VolumeTakeoffResult> results = RunBroken(items, Options("plain"), null, (loser, keeper) => loser == 0 && keeper == 1);

            Assert.Empty(Disagreements(items, results, Oracle.Of(set)));
            AssertTaken(results[0], 1.8E9, 1.56E9, (2, 2.4E7), (1, 2.16E8));
        }

        [Theory]
        [InlineData("intersection")]
        [InlineData("take off")]
        public void TwoHundredRandomSetsWithEveryStepSliced_AgreeWithTheOracle_AndOneThreadWithEveryProcessorBitForBit(string broken)
        {
            // The sets of RandomSet, seeded 0 to 199, with every common part, or every cut of the second phase, taken as
            // failed: flush faces and copies common, and overlaps shared by three and four parts, all sliced.
            var failures = new List<string>();
            Func<int, int, bool> always = (loser, keeper) => true;

            for (int seed = 0; seed < 200 && failures.Count < 20; seed++)
            {
                BoxSet set = RandomSet(seed);
                VolumeItem[] items = set.ToItems();
                Func<int, int, bool> intersection = broken == "intersection" ? always : null;
                Func<int, int, bool> takeOff = broken == "take off" ? always : null;

                IReadOnlyList<VolumeTakeoffResult> every = RunBroken(items, new VolumeTakeoffOptions(Plain, -1), intersection, takeOff);
                IReadOnlyList<VolumeTakeoffResult> one = RunBroken(items, new VolumeTakeoffOptions(Plain, 1), intersection, takeOff);

                List<string> wrong = Disagreements(items, every, Oracle.Of(set));
                wrong.AddRange(Differences(one, every).Select(difference => "one thread against every processor, " + difference));
                if (wrong.Count > 0)
                {
                    failures.Add($"seed {seed}:\n{set}  " + string.Join("\n  ", wrong));
                }
            }

            Assert.Empty(failures);
        }

        [Fact]
        public void ThreeHundredRandomSetsWithSomePairsAndStepsSliced_AgreeWithTheOracle_AndOneThreadWithEveryProcessorBitForBit()
        {
            // The sets of RandomSetWithOpenings, seeded 0 to 299: openings flush or running past, copies, equal priorities
            // and ids. A third of the pairs, picked by a hash of the seed and the two indices, have their common part taken as
            // not made, and a third of the steps of the second phase their cut taken as skipped: pieces, sliced overlaps,
            // pieces cut by a sliced keeper's solid and sliced steps all meet in one part. Every net, deduction and the union
            // must stand within a part in a billion of the oracle's, with no issue, and one thread must give every processor's
            // result bit for bit.
            var failures = new List<string>();
            int slicedPairs = 0, slicedSteps = 0;

            for (int seed = 0; seed < 300 && failures.Count < 20; seed++)
            {
                BoxSet set = RandomSetWithOpenings(seed);
                VolumeItem[] items = set.ToItems();
                int at = seed;
                Func<int, int, bool> intersection = (loser, keeper) =>
                {
                    bool broken = Hashed(at, loser, keeper, 1) % 3 == 0;
                    if (broken)
                    {
                        System.Threading.Interlocked.Increment(ref slicedPairs);
                    }

                    return broken;
                };
                Func<int, int, bool> takeOff = (loser, keeper) =>
                {
                    bool broken = Hashed(at, loser, keeper, 2) % 3 == 0;
                    if (broken)
                    {
                        System.Threading.Interlocked.Increment(ref slicedSteps);
                    }

                    return broken;
                };

                IReadOnlyList<VolumeTakeoffResult> every = RunBroken(items, new VolumeTakeoffOptions(Plain, -1), intersection, takeOff);
                IReadOnlyList<VolumeTakeoffResult> one = RunBroken(items, new VolumeTakeoffOptions(Plain, 1), intersection, takeOff);

                List<string> wrong = Disagreements(items, every, Oracle.Of(set));
                wrong.AddRange(Differences(one, every).Select(difference => "one thread against every processor, " + difference));
                if (wrong.Count > 0)
                {
                    failures.Add($"seed {seed}:\n{set}  " + string.Join("\n  ", wrong));
                }
            }

            Assert.Empty(failures);
            Assert.True(slicedPairs > 0 && slicedSteps > 0, $"the hooks broke {slicedPairs} pairs and {slicedSteps} steps");
        }

        // A hash of a seed, the two indices of a pair and a salt, the same on every run and every thread.
        private static uint Hashed(int seed, int loser, int keeper, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (int value in new[] { seed, loser, keeper, salt })
                {
                    h = (h ^ (uint)value) * 16777619u;
                    h ^= h >> 15;
                }

                return h;
            }
        }

        [Fact]
        public void APairBrokenThatCannotBeSlicedEither_IsNotDeducted_WithOneIssueSayingSo()
        {
            // A cube 1 000 across, priority 0, and inside it a tetrahedron 300 by 300 by 300 with half its slanted face left
            // out, priority 1: the common part taken as not made, and the keeper not a body the slicing can read. The cube
            // keeps all of itself, as an upper bound, and says so once; the tetrahedron's own issue is its own volume.
            GeoSolid3 cube = new Box(0, 0, 0, 1000, 1000, 1000).ToSolid();
            GeoSolid3 open = Tetrahedron(300, 300, 300, leaveOutHalfTheSlantedFace: true).Translate(new GeoVector3(100, 100, 100));
            VolumeItem[] items = { new VolumeItem(cube, "cube", 0), new VolumeItem(open, "open", 1) };

            IReadOnlyList<VolumeTakeoffResult> results = RunBroken(items, Options("plain"), (loser, keeper) => true, null);

            Assert.Equal(
                "overlap with #1 could not be worked out, by booleans or by slicing: not deducted, so the net volume is an upper bound",
                Assert.Single(results[0].Issues));
            Assert.False(results[0].IsExact);
            Assert.Empty(results[0].Deductions);
            Assert.Equal(1E9, results[0].NetVolume, Relative * 1E9);
            Assert.DoesNotContain(results[1].Issues, issue => issue.StartsWith("overlap", StringComparison.Ordinal));
        }

        #endregion
    }
}
