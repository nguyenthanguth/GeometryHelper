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
    /// Who keeps an overlap between parts of one priority when they carry ids: the larger id, an item with an id before
    /// one without, the list where the ids are equal or absent, and the priority always first; and the item's id itself;
    /// see <see cref="VolumeItem.Id"/>.
    /// </summary>
    /// <remarks>
    /// The rank is a total order: the priority, the highest first; an item with an id before one without; the larger id
    /// first; then the index in the list. Each case is held to the oracle of <see cref="VolumeTakeoffTestKit"/>, which
    /// ranks the same way, and to its numbers worked out by hand, within a part in a billion of each part's gross.
    /// </remarks>
    public class VolumeTakeoffIdTests
    {
        private const int Sets = 300;

        private static readonly SolidBooleanOptions Plain = new SolidBooleanOptions(Tolerance.Default);

        private static readonly VolumeTakeoffOptions Options = new VolumeTakeoffOptions(Plain);

        // Runs the set and holds every result to the oracle, with no issue on any item.
        private static IReadOnlyList<VolumeTakeoffResult> RunChecked(BoxSet set)
        {
            VolumeItem[] items = set.ToItems();
            IReadOnlyList<VolumeTakeoffResult> results = VolumeTakeoff.Run(items, Options);
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

        #region Ranking by id

        [Fact]
        public void EqualPrioritiesWithIds_TheLargerIdKeepsTheOverlap_WhicheverComesFirst()
        {
            // The two cubes of HalfOverlap, of one priority, sharing 500 000 000: the one with id 9 keeps it over the one
            // with id 7, listed second or first. By the list alone the first would keep it both times.
            BoxSet forward = HalfOverlap(1, 1);
            forward = new BoxSet(forward.Boxes, forward.Priorities, ids: new int?[] { 7, 9 });
            BoxSet backward = new BoxSet(forward.Boxes.Reverse().ToArray(), forward.Priorities, ids: new int?[] { 9, 7 });

            IReadOnlyList<VolumeTakeoffResult> first = RunChecked(forward);
            AssertTaken(first[0], 1E9, 5E8, (1, 5E8));
            AssertTaken(first[1], 1E9, 1E9);

            IReadOnlyList<VolumeTakeoffResult> second = RunChecked(backward);
            AssertTaken(second[0], 1E9, 1E9);
            AssertTaken(second[1], 1E9, 5E8, (0, 5E8));
        }

        [Fact]
        public void InOnePriority_AnItemWithAnIdRanksBeforeOnesWithout_ThoughItsIdIsTheLeast()
        {
            // Three copies of one block, 150 000 000, of one priority; only the middle one has an id, int.MinValue. It keeps
            // all of it, and the other two, first and last in the list, give it all of theirs.
            BoxSet copies = ThreeCopies();
            var set = new BoxSet(copies.Boxes, copies.Priorities, ids: new int?[] { null, int.MinValue, null });

            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(set);

            AssertTaken(results[0], 1.5E8, 0.0, (1, 1.5E8));
            AssertTaken(results[1], 1.5E8, 1.5E8);
            AssertTaken(results[2], 1.5E8, 0.0, (1, 1.5E8));
        }

        [Fact]
        public void EqualIds_AreDecidedByTheList_AfterTheLargerId()
        {
            // The four cubes of FourAtACorner, of one priority, with ids 5, 9, 5 and 5: ranked the second, then the first,
            // third and fourth in the order of the list. The first gives the second its slab, 200 000 000; the third gives
            // the second the post, 40 000 000, and the first the rest of their slab, 160 000 000; the fourth gives the
            // second their slab, 200 000 000, post and all, and the third the rest of theirs, 160 000 000.
            BoxSet corner = FourAtACorner();
            var set = new BoxSet(corner.Boxes, corner.Priorities, ids: new int?[] { 5, 9, 5, 5 });

            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(set);

            AssertTaken(results[0], 1E9, 8E8, (1, 2E8));
            AssertTaken(results[1], 1E9, 1E9);
            AssertTaken(results[2], 1E9, 8E8, (1, 4E7), (0, 1.6E8));
            AssertTaken(results[3], 1E9, 6.4E8, (1, 2E8), (2, 1.6E8));
        }

        [Fact]
        public void TheLeastAndGreatestIds_RankWithoutOverflow()
        {
            // The three parts of ExtremePriorities, all of priority 0 now, with ids int.MinValue, int.MaxValue and 0: ranked
            // as the priorities ranked them there, the second, the post, the first. A comparison by subtraction would wrap
            // round and rank them backwards.
            BoxSet extreme = ExtremePriorities();
            var set = new BoxSet(extreme.Boxes, new[] { 0, 0, 0 }, ids: new int?[] { int.MinValue, int.MaxValue, 0 });

            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(set);

            AssertTaken(results[0], 1E9, 3.75E8, (1, 5E8), (2, 1.25E8));
            AssertTaken(results[1], 1E9, 1E9);
            AssertTaken(results[2], 5E8, 3.75E8, (1, 1.25E8));
        }

        [Fact]
        public void AnIdNeverOutranksAHigherPriority_ButDecidesBetweenEqualOnes()
        {
            // The parts of ExtremePriorities with priorities 0, 0 and 1 and ids 1 000, 2 000 and 1: the post, of priority 1,
            // keeps all it holds though its id is the least; of the two cubes the second, id 2 000, keeps what they share.
            // The post keeps its 500 000 000. The second cube gives it 250 by 500 by 1 000, 125 000 000, and keeps
            // 875 000 000. The first gives it 500 by 500 by 1 000, 250 000 000, and the second the 375 000 000 left of
            // their overlap, keeping 375 000 000.
            BoxSet extreme = ExtremePriorities();
            var set = new BoxSet(extreme.Boxes, new[] { 0, 0, 1 }, ids: new int?[] { 1000, 2000, 1 });

            IReadOnlyList<VolumeTakeoffResult> results = RunChecked(set);

            AssertTaken(results[0], 1E9, 3.75E8, (2, 2.5E8), (1, 3.75E8));
            AssertTaken(results[1], 1E9, 8.75E8, (2, 1.25E8));
            AssertTaken(results[2], 5E8, 5E8);
        }

        [Fact]
        public void ThreeHundredRandomSetsWithIds_AgreeWithTheOracle()
        {
            // The sets of RandomSetWithIds, seeded 0 to 299: priorities from -2 to 2 and ids from six values or none, so that
            // every step of the rank decides somewhere.
            var failures = new List<string>();

            for (int seed = 0; seed < Sets && failures.Count < 20; seed++)
            {
                BoxSet set = RandomSetWithIds(seed);
                VolumeItem[] items = set.ToItems();

                List<string> wrong = Disagreements(items, VolumeTakeoff.Run(items, Options), Oracle.Of(set));
                if (wrong.Count > 0)
                {
                    failures.Add($"seed {seed}:\n{set}  " + string.Join("\n  ", wrong));
                }
            }

            Assert.Empty(failures);
        }

        #endregion

        #region The item's id

        [Fact]
        public void TheConstructorOfThreeArguments_GivesNoId()
        {
            var item = new VolumeItem(Joint().Boxes[0].ToSolid(), "slab", 3);

            Assert.Null(item.Id);
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(int.MaxValue)]
        public void TheConstructorOfFourArguments_KeepsTheIdAndTheRest(int id)
        {
            GeoSolid3 solid = Joint().Boxes[0].ToSolid();

            var item = new VolumeItem(solid, "slab", -2, id);

            Assert.Equal(id, item.Id);
            Assert.Same(solid, item.Solid);
            Assert.Equal("slab", item.Name);
            Assert.Equal(-2, item.Priority);
        }

        [Fact]
        public void TheConstructorOfFourArguments_ThrowsForANullSolid_NamingIt()
        {
            Assert.Equal("solid", Assert.Throws<ArgumentNullException>(() => new VolumeItem(null, "slab", 3, 7)).ParamName);
        }

        #endregion
    }
}
