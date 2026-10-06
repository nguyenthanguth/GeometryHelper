using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Takeoff;
using Xunit;
using static GeometryHelper.UnitTest.Takeoff.VolumeTakeoffTestKit;

namespace GeometryHelper.UnitTest.Takeoff
{
    /// <summary>
    /// Taking each overlap off once, by the highest-ranked part that holds it: a slab, a beam and a column at a joint,
    /// four parts at one corner, copies, a part inside another, parts that only touch, equal and extreme priorities, a
    /// null entry, one solid twice, openings in the part that loses and in the part that keeps, and bodies not lined up
    /// with the axes; and the options and arguments; see <see cref="VolumeTakeoff"/>.
    /// </summary>
    /// <remarks>
    /// The boxes are those of <see cref="VolumeTakeoffTestKit"/>, each case held both to the numbers worked out by hand
    /// and to the oracle there, within a part in a billion of each part's gross volume. Every overlap is 100 or more deep,
    /// clear of the tolerance and of the contact. Every item is asked to be exact, a part left nothing too: deductions a
    /// rounding over its gross, clamped within the point tolerance times its area, are no issue.
    /// </remarks>
    public class VolumeTakeoffTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        // The boolean within a thousandth, as it comes.
        private static readonly SolidBooleanOptions Plain = new SolidBooleanOptions(Fine);

        // Faces within a hundredth put onto each other, and a cut not valid worked out again within a hundredth: the
        // setting measured best on the 79 864 parts of sel2.
        private static readonly SolidBooleanOptions Contact = new SolidBooleanOptions(Fine, 0.01, new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01));

        /// <summary>The two settings of the boolean every box case runs with.</summary>
        public static IEnumerable<object[]> Settings() => new[] { new object[] { "plain" }, new object[] { "contact" } };

        private static VolumeTakeoffOptions Options(string setting) => new VolumeTakeoffOptions(setting == "contact" ? Contact : Plain);

        // Runs the set and holds every result to the oracle, with no issue on any item.
        private static IReadOnlyList<VolumeTakeoffResult> RunChecked(BoxSet set, string setting)
        {
            VolumeItem[] items = set.ToItems();
            IReadOnlyList<VolumeTakeoffResult> results = VolumeTakeoff.Run(items, Options(setting));
            Assert.Empty(Disagreements(items, results, Oracle.Of(set)));
            return results;
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

        #region Overlaps taken off once

        [Theory]
        [MemberData(nameof(Settings))]
        public void ASlabBeamAndColumnAtAJoint_TheSlabLosesTheBlockAllThreeShareOnce(string setting)
        {
            // Listed slab, beam, column; ranked column, beam, slab. The block 300 by 400 by 200 lies in all three: the column
            // takes it, and the beam takes from the slab only what lies over it outside the block.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(Joint(), setting);

            AssertTaken(results[0], 1.8E9, 1.56E9, (2, 2.4E7), (1, 2.16E8));
            AssertTaken(results[1], 4.8E8, 4.32E8, (2, 4.8E7));
            AssertTaken(results[2], 1.2E8, 1.2E8);
            Assert.Equal(2.112E9, results.Sum(r => r.NetVolume), Relative * 2.4E9);
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void FourPartsSharingACorner_TheLastGivesUpThePostAllFourShareOnce(string setting)
        {
            // Of one priority, so in list order. The third shares with the second only the post the first has taken, and
            // gives the second nothing; the fourth gives the post to the first and the rest of each slab to the second and
            // third, 160 000 000 each.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(FourAtACorner(), setting);

            AssertTaken(results[0], 1E9, 1E9);
            AssertTaken(results[1], 1E9, 8E8, (0, 2E8));
            AssertTaken(results[2], 1E9, 8E8, (0, 2E8));
            AssertTaken(results[3], 1E9, 6.4E8, (0, 4E7), (1, 1.6E8), (2, 1.6E8));
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void ThreeExactCopies_TheLaterTwoKeepNothing_EachGivingAllToTheFirst(string setting)
        {
            // The third copy's overlap with the second lies wholly inside its overlap with the first: no deduction by the
            // second, not one of nought.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(ThreeCopies(), setting);

            AssertTaken(results[0], 1.5E8, 1.5E8);
            AssertTaken(results[1], 1.5E8, 0.0, (0, 1.5E8));
            AssertTaken(results[2], 1.5E8, 0.0, (0, 1.5E8));
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void ACopyABarRunsThrough_KeepsNothing_TheBarAndTheFirstCopyTakingIt(string setting)
        {
            // The later copy loses 1 000 000 to the bar and the 5 000 000 left to the first copy. Taken off as the spec takes
            // them, the two come to 9.3E-10 over its gross of 6 000 000; clamped to nought within the point tolerance times its
            // area, 0.001 by 220 000, that is no issue, and the copy is exact.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(CopiesWithABarThrough(), setting);

            AssertTaken(results[0], 6E6, 5E6, (2, 1E6));
            AssertTaken(results[1], 6E6, 0.0, (2, 1E6), (0, 5E6));
            AssertTaken(results[2], 4E6, 4E6);
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void APartWhollyInsideAHigherOne_KeepsNothing(string setting)
        {
            // The block, priority 2, inside the cube, priority 1, listed first: the list order does not save it.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(BlockInACube(2, 1), setting);

            AssertTaken(results[0], 2.7E7, 0.0, (1, 2.7E7));
            AssertTaken(results[1], 1E9, 1E9);
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void APartWhollyInsideALowerOne_HollowsIt(string setting)
        {
            // The same two, the cube now losing: it keeps all but the block, a hollow clear of its faces.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(BlockInACube(1, 2), setting);

            AssertTaken(results[0], 2.7E7, 2.7E7);
            AssertTaken(results[1], 1E9, 9.73E8, (0, 2.7E7));
        }

        #endregion

        #region Parts that only touch

        [Theory]
        [MemberData(nameof(Settings))]
        public void PartsTouchingAtAFaceAnEdgeOrACorner_TakeNothing(string setting)
        {
            // A cube with four more against its face, edge, corner and top, flush: their boxes overlap by nought, and
            // nothing is deducted from any of them.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(Touching(), setting);

            Assert.All(results, result => Assert.Empty(result.Deductions));
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void ABlockFillingANotchFlush_TakesNothing_ThoughTheirBoxesOverlap(string setting)
        {
            // The block's box lies inside the notched cube's, 500 by 500 by 1 000 of overlap, but all their material shares
            // is the two faces of the notch.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(BlockInANotch(), setting);

            AssertTaken(results[0], 7.5E8, 7.5E8);
            AssertTaken(results[1], 2.5E8, 2.5E8);
        }

        #endregion

        #region Ranking

        [Theory]
        [MemberData(nameof(Settings))]
        public void EqualPriorities_TheEarlierInTheListKeepsTheOverlap_EitherWayRound(string setting)
        {
            BoxSet forward = HalfOverlap(1, 1);
            BoxSet backward = new BoxSet(forward.Boxes.Reverse().ToArray(), forward.Priorities);

            IReadOnlyList<VolumeTakeoffResult> first = RunChecked(forward, setting);
            AssertTaken(first[0], 1E9, 1E9);
            AssertTaken(first[1], 1E9, 5E8, (0, 5E8));

            IReadOnlyList<VolumeTakeoffResult> second = RunChecked(backward, setting);
            AssertTaken(second[0], 1E9, 1E9);
            AssertTaken(second[1], 1E9, 5E8, (0, 5E8));
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void ANegativePriority_RanksAboveNought_ThoughListedLater(string setting)
        {
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(HalfOverlap(0, -3), setting);

            AssertTaken(results[0], 1E9, 5E8, (1, 5E8));
            AssertTaken(results[1], 1E9, 1E9);
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void TheLeastAndGreatestPriorities_RankWithoutOverflow(string setting)
        {
            // int.MinValue first, then 0, then int.MaxValue: a comparison by subtraction would wrap round and rank them
            // backwards.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(ExtremePriorities(), setting);

            AssertTaken(results[0], 1E9, 3.75E8, (1, 5E8), (2, 1.25E8));
            AssertTaken(results[1], 1E9, 1E9);
            AssertTaken(results[2], 5E8, 3.75E8, (1, 1.25E8));
        }

        #endregion

        #region Entries

        [Theory]
        [MemberData(nameof(Settings))]
        public void ANullEntry_GetsANoughtResultWithOneIssue_AndTakesNoPart(string setting)
        {
            // The two cubes of HalfOverlap with a null between them: the later still gives the earlier half of itself, the
            // deduction naming the earlier by its index in the list, nulls counted.
            var set = new BoxSet(new[] { HalfOverlap(0, 0).Boxes[0], null, HalfOverlap(0, 0).Boxes[1] }, new[] { 0, 0, 0 });
            VolumeItem[] items = set.ToItems();

            IReadOnlyList<VolumeTakeoffResult> results = VolumeTakeoff.Run(items, Options(setting));

            Assert.Empty(Disagreements(items, results, Oracle.Of(set)));
            VolumeTakeoffResult none = results[1];
            Assert.Null(none.Item);
            Assert.Equal(0.0, none.GrossVolume);
            Assert.Equal(0.0, none.DeductedVolume);
            Assert.Equal(0.0, none.NetVolume);
            Assert.Empty(none.Deductions);
            Assert.Contains("no item", Assert.Single(none.Issues), StringComparison.OrdinalIgnoreCase);
            Assert.False(none.IsExact);
            AssertTaken(results[2], 1E9, 5E8, (0, 5E8));
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void TheSameSolidTwice_TheLaterKeepsNothing(string setting)
        {
            // Two items on one GeoSolid3 instance, each with its own name.
            BoxSet set = new BoxSet(new[] { ThreeCopies().Boxes[0], ThreeCopies().Boxes[0] }, new[] { 0, 0 }, new[] { -1, 0 });
            VolumeItem[] items = set.ToItems();
            Assert.Same(items[0].Solid, items[1].Solid);

            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(set, setting);

            AssertTaken(results[0], 1.5E8, 1.5E8);
            AssertTaken(results[1], 1.5E8, 0.0, (0, 1.5E8));
        }

        [Fact]
        public void TheSameItemTwice_TheLaterKeepsNothing()
        {
            var item = new VolumeItem(ThreeCopies().Boxes[0].ToSolid(), "block", 0);
            VolumeItem[] items = { item, item };

            IReadOnlyList<VolumeTakeoffResult> results = VolumeTakeoff.Run(items, Options("plain"));

            Assert.Same(item, results[0].Item);
            Assert.Same(item, results[1].Item);
            AssertTaken(results[0], 1.5E8, 1.5E8);
            AssertTaken(results[1], 1.5E8, 0.0, (0, 1.5E8));
        }

        [Fact]
        public void NoItems_GiveNoResults()
        {
            Assert.Empty(VolumeTakeoff.Run(new VolumeItem[0], Options("plain")));
        }

        #endregion

        #region Openings

        [Theory]
        [MemberData(nameof(Settings))]
        public void AnOpeningInThePartThatLoses_IsNotDeducted(string setting)
        {
            // The column crosses 400 by 400 of the slab, a quarter of it in the duct: the slab gives 24 000 000, not
            // 32 000 000, the duct's corner being no material of it.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(ColumnThroughADuct(), setting);

            AssertTaken(results[0], 1.728E9, 1.704E9, (1, 2.4E7));
            AssertTaken(results[1], 3.2E8, 3.2E8);
        }

        [Theory]
        [MemberData(nameof(Settings))]
        public void AnOpeningInThePartThatKeeps_LeavesTheLoserWhatLiesInIt(string setting)
        {
            // The slab gives the hollow column its ring, 120 000 by 200, and keeps the 200 by 200 by 200 inside the duct.
            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(HollowColumnThroughASlab(), setting);

            AssertTaken(results[0], 1.152E9, 1.128E9, (1, 2.4E7));
            AssertTaken(results[1], 2.4E8, 2.4E8);
        }

        #endregion

        #region Bodies not lined up with the axes

        [Fact]
        public void AColumnTurnedThirtyDegreesThroughASlab_TakesItsSectionTimesTheSlab()
        {
            // The column 300 by 400 by 2 000 turned 30 degrees about z, through a slab 2 000 by 2 000 by 200 it crosses
            // wholly: what they share is its section, 120 000, by the slab's 200, 24 000 000, whatever the turn.
            double turn = Math.PI / 6.0;
            GeoSolid3 column = new GeoObb3(new GeoPoint3(0, 0, 0), 300, 400, 2000, new GeoVector3(Math.Cos(turn), Math.Sin(turn), 0), new GeoVector3(-Math.Sin(turn), Math.Cos(turn), 0)).ToSolid();
            GeoSolid3 slab = new Box(-1000, -1000, 0, 1000, 1000, 200).ToSolid();
            VolumeItem[] items = { new VolumeItem(slab, "slab", 3), new VolumeItem(column, "column", 0) };

            IReadOnlyList<VolumeTakeoffResult> results = VolumeTakeoff.Run(items, Options("plain"));

            AssertTaken(results[0], 8E8, 7.76E8, (1, 2.4E7));
            AssertTaken(results[1], 2.4E8, 2.4E8);
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
        }

        [Fact]
        public void ARoundColumnThroughASlab_TakesItsPolygonTimesTheSlab()
        {
            // A column of radius 200 on 32 sides, corners on the circle, 2 000 long, through the same slab: its section is
            // 16 by 200 squared by sin(pi / 16), 124 857.8, and the slab gives it that by 200.
            GeoSolid3 column = GeoSolid3.Cylinder(new GeoPoint3(0, 0, -1000), new GeoPoint3(0, 0, 1000), 200, 32, Fine);
            GeoSolid3 slab = new Box(-1000, -1000, 0, 1000, 1000, 200).ToSolid();
            double section = 16.0 * 200.0 * 200.0 * Math.Sin(Math.PI / 16.0);
            VolumeItem[] items = { new VolumeItem(slab, "slab", 3), new VolumeItem(column, "column", 0) };

            IReadOnlyList<VolumeTakeoffResult> results = VolumeTakeoff.Run(items, Options("plain"));

            AssertTaken(results[0], 8E8, 8E8 - 200.0 * section, (1, 200.0 * section));
            AssertTaken(results[1], 2000.0 * section, 2000.0 * section);
            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
        }

        #endregion

        #region A keeper thinner than the contact

        [Fact]
        public void AWedgeThinnerThanTheContactOnTheLosersFace_IsTakenOffWithoutAnIssue()
        {
            // The box of ContactWedgeTests, 56 250 000, priority 1, and the wedge on its face, 194.4 and 0.006 at its
            // thickest, priority 0. With the contact of a hundredth the wedge is taken as touching, or as the overlap it is:
            // the box keeps between 56 249 805.6 and all of itself, and the takeoff has no issue. At 120c78b the boolean
            // throws on the pair, and the takeoff can only say it could not work the overlap out.
            GeoSolid3 box = new Box(0, 0, 0, 250, 250, 900).ToSolid();
            GeoSolid3 wedge = GeoSolid3.Extrude(new GeoPolygon3(new[] { new GeoPoint3(249.994, 90, 0), new GeoPoint3(250, 90, 0), new GeoPoint3(250, 162, 0) }, Fine), new GeoVector3(0, 0, 900), Fine);
            VolumeItem[] items = { new VolumeItem(box, "box", 1), new VolumeItem(wedge, "wedge", 0) };

            IReadOnlyList<VolumeTakeoffResult> results = VolumeTakeoff.Run(items, Options("contact"));

            Assert.All(results, result => Assert.True(result.IsExact, string.Join(" / ", result.Issues)));
            Assert.InRange(results[0].NetVolume, 56250000.0 - 194.4 - 1E-3, 56250000.0 + 1E-3);
            Assert.Equal(194.4, results[1].NetVolume, 1E-3);
        }

        #endregion

        #region The call itself

        [Fact]
        public void TheSameCallTwice_GivesTheSameResultsBitForBit_AndLeavesTheSolidsAsTheyWere()
        {
            VolumeItem[] items = Joint().ToItems();
            double[] before = items.Select(item => item.Solid.GetVolume(Fine)).ToArray();
            int[] faces = items.Select(item => item.Solid.Faces.Count).ToArray();

            IReadOnlyList<VolumeTakeoffResult> first = VolumeTakeoff.Run(items, Options("contact"));
            IReadOnlyList<VolumeTakeoffResult> second = VolumeTakeoff.Run(items, Options("contact"));

            Assert.Empty(Differences(first, second));
            Assert.Equal(before, items.Select(item => item.Solid.GetVolume(Fine)));
            Assert.Equal(faces, items.Select(item => item.Solid.Faces.Count));
        }

        [Fact]
        public void RunWithoutOptions_IsRunWithTheDefault()
        {
            VolumeItem[] items = FourAtACorner().ToItems();

            Assert.Empty(Differences(VolumeTakeoff.Run(items), VolumeTakeoff.Run(items, VolumeTakeoffOptions.Default)));
        }

        [Fact]
        public void NullArguments_Throw_NamingTheArgument()
        {
            VolumeItem[] items = FourAtACorner().ToItems();

            Assert.Equal("solid", Assert.Throws<ArgumentNullException>(() => new VolumeItem(null, "slab", 3)).ParamName);
            Assert.Equal("boolean", Assert.Throws<ArgumentNullException>(() => new VolumeTakeoffOptions(null)).ParamName);
            Assert.Equal("items", Assert.Throws<ArgumentNullException>(() => VolumeTakeoff.Run(null)).ParamName);
            Assert.Equal("items", Assert.Throws<ArgumentNullException>(() => VolumeTakeoff.Run(null, VolumeTakeoffOptions.Default)).ParamName);
            Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => VolumeTakeoff.Run(items, null)).ParamName);
        }

        #endregion

        #region Items and options

        [Fact]
        public void AnItem_KeepsWhatItWasGiven_ANullNameAndAnyPriorityAmongIt()
        {
            GeoSolid3 solid = Joint().Boxes[0].ToSolid();

            var item = new VolumeItem(solid, null, int.MinValue);

            Assert.Same(solid, item.Solid);
            Assert.Null(item.Name);
            Assert.Equal(int.MinValue, item.Priority);
            Assert.Equal("slab", new VolumeItem(solid, "slab", 3).Name);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        [InlineData(int.MinValue)]
        public void AParallelismOfNoughtOrBelowMinusOne_Throws(int maxDegreeOfParallelism)
        {
            ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new VolumeTakeoffOptions(Plain, maxDegreeOfParallelism));
            Assert.Equal("maxDegreeOfParallelism", thrown.ParamName);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(int.MaxValue)]
        public void AParallelismOfMinusOneOrAPositiveCount_IsKept(int maxDegreeOfParallelism)
        {
            var options = new VolumeTakeoffOptions(Contact, maxDegreeOfParallelism);

            Assert.Same(Contact, options.Boolean);
            Assert.Equal(maxDegreeOfParallelism, options.MaxDegreeOfParallelism);
        }

        [Fact]
        public void TheDefaultOptions_AreTheDefaultBooleanOnEveryProcessor()
        {
            Assert.Equal(SolidBooleanOptions.Default, VolumeTakeoffOptions.Default.Boolean);
            Assert.Equal(-1, VolumeTakeoffOptions.Default.MaxDegreeOfParallelism);
            Assert.Equal(-1, new VolumeTakeoffOptions(Plain).MaxDegreeOfParallelism);
        }

        [Fact]
        public void OptionsAlike_AreEqual_AndOptionsThatDifferInEither_AreNot()
        {
            var options = new VolumeTakeoffOptions(Contact, 2);
            var alike = new VolumeTakeoffOptions(new SolidBooleanOptions(Fine, 0.01, new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01)), 2);

            Assert.Equal(options, alike);
            Assert.True(options.Equals((object)alike));
            Assert.Equal(options.GetHashCode(), alike.GetHashCode());
            Assert.Equal(options.ToString(), alike.ToString());

            Assert.NotEqual(options, new VolumeTakeoffOptions(Contact, 3));
            Assert.NotEqual(options, new VolumeTakeoffOptions(Plain, 2));
            Assert.NotEqual(options.ToString(), new VolumeTakeoffOptions(Contact, 3).ToString());
            Assert.False(options.Equals(null));
            Assert.False(options.Equals((object)"options"));
        }

        #endregion

        #region What a result says when it cannot be sure

        [Fact]
        public void ABodyThatDoesNotClose_IsMeasuredByItsFaces_AndSaysSo()
        {
            // A cube 100 across with its first face left out encloses nothing it can be trusted for: TryGetVolume says so,
            // and the gross is what its faces give, with an issue and not exact.
            GeoSolid3 cube = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100)).ToObb().ToSolid();
            var open = new GeoSolid3(cube.Faces.Skip(1));

            VolumeTakeoffResult result = Assert.Single(VolumeTakeoff.Run(new[] { new VolumeItem(open, "OPEN", 0) }, new VolumeTakeoffOptions(Plain)));

            Assert.False(result.IsExact);
            Assert.StartsWith("the solid's own volume is not to be trusted", Assert.Single(result.Issues));
            Assert.Equal(open.GetVolume(Fine), result.GrossVolume);
            Assert.Equal(result.GrossVolume, result.NetVolume);
        }

        [Fact]
        public void AnOverlapReadByACut_IsJudgedByTheLeastAreaABodyOfItsVolumeCanHave_ABalls()
        {
            // A ball of radius 10 holds 4 188.79 within 1 256.64 of surface, the least any body of that volume has. A
            // common part read by a cut has no body to measure, so its thinness is judged against that: two slabs 6 000 by
            // 6 000 by 200 sharing a corner 10 by 10 by 200, 20 000, have at least 3 563.18 of surface between them there,
            // and 20 000 is far more than the 1.78 half the point tolerance over it allows; against the slabs' own
            // 76 800 000 it would have passed for touching.
            Assert.Equal(4.0 * Math.PI * 100.0, VolumeTakeoff.LeastArea(4.0 / 3.0 * Math.PI * 1000.0), 9);
            Assert.Equal(3563.1774804909223, VolumeTakeoff.LeastArea(20000.0), 9);
            Assert.True(VolumeTakeoff.LeastArea(1E6) <= 60000.0);
        }

        #endregion
    }
}
