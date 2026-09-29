using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GeometryHelper.Geometry;
using Xunit;
using GeometryHelper.Arranging;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// What a run promises about what it is given and what it gives back, apart from where the labels end up.
    /// </summary>
    public class ArrangerTests : ArrangeTestKit
    {
        private static ArrangeOptions OneRow(ArrangeAlgorithmType algorithm)
        {
            return new ArrangeOptions { Algorithm = algorithm, RowGap = 5.0, PerpendicularLevels = 1 };
        }

        /// <summary>
        /// Six labels on one short leader with a single row of room, a block line along each side of the row and
        /// a region every label shares: the first pass leaves labels overlapping, so the second pass runs.
        /// </summary>
        private static List<ArrangeItem> Crowd()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var shared = new List<GeoPolygon2>
            {
                new GeoPolygon2(new GeoPoint2(-5.0, 30.0), new GeoPoint2(5.0, 30.0), new GeoPoint2(5.0, 40.0), new GeoPoint2(-5.0, 40.0))
            };

            var items = new List<ArrangeItem>();
            for (int i = 0; i < 6; i++)
            {
                ArrangeItem item = LabelOn(leader);
                item.BlockPolygons = shared;
                item.BlockLines = new List<GeoLine2> { new GeoLine2(-100.0 + i, 10.0, 100.0, 10.0) };
                items.Add(item);
            }

            return items;
        }

        /// <summary>
        /// A label its own block lines box in fails the first pass and is tried again in a second, which lent it
        /// relaxed blocks for the while and handed its own back after. Listed twice, it was lent them twice, and
        /// the second loan took the first for the label's own.
        /// </summary>
        [Fact]
        public void AnItemListedTwiceKeepsItsOwnBlocks()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var lines = new List<GeoLine2>
            {
                new GeoLine2(-100.0, 10.0, 100.0, 10.0),
                new GeoLine2(-100.0, -10.0, 100.0, -10.0)
            };
            var polygons = new List<GeoPolygon2>();

            ArrangeItem label = LabelOn(leader);
            label.BlockLines = lines;
            label.BlockPolygons = polygons;

            Arranger.Run(new List<ArrangeItem> { label, label }, OneRow(ArrangeAlgorithmType.Greedy));

            Assert.Same(lines, label.BlockLines);
            Assert.Same(polygons, label.BlockPolygons);
            Assert.Equal(2, lines.Count);
            Assert.Empty(polygons);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void ARunLeavesTheItemsAsItFoundThem(ArrangeAlgorithmType algorithm)
        {
            List<ArrangeItem> items = Crowd();

            // Some labels have a gap of their own on a side, the others leave both to the offset.
            for (int i = 0; i < items.Count; i += 2)
            {
                items[i].OffsetTop = 3.0;
                items[i].OffsetBottom = 7.0;
            }

            var before = items.Select(item => new
            {
                item.Box,
                item.Leader,
                item.Offset,
                item.OffsetTop,
                item.OffsetBottom,
                Polygons = item.BlockPolygons,
                PolygonsHeld = item.BlockPolygons.ToArray(),
                Lines = item.BlockLines,
                LinesHeld = item.BlockLines.ToArray(),
            }).ToList();

            ArrangeResult[] results = Arranger.Run(items, OneRow(algorithm));

            // Some label is left overlapping, so the second pass ran and relaxed the blocks of the others.
            Assert.Contains(results, r => !r.Placed);

            for (int i = 0; i < items.Count; i++)
            {
                Assert.Equal(before[i].Box, items[i].Box);
                Assert.Equal(before[i].Leader, items[i].Leader);
                Assert.Equal(before[i].Offset, items[i].Offset);
                Assert.Equal(before[i].OffsetTop, items[i].OffsetTop);
                Assert.Equal(before[i].OffsetBottom, items[i].OffsetBottom);
                Assert.Same(before[i].Polygons, items[i].BlockPolygons);
                Assert.Equal(before[i].PolygonsHeld, items[i].BlockPolygons);
                Assert.Same(before[i].Lines, items[i].BlockLines);
                Assert.Equal(before[i].LinesHeld, items[i].BlockLines);
            }
        }

        /// <summary>
        /// Nothing a run keeps is shared with another run, so the same items arranged on several threads at once
        /// come out as they do arranged alone.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void RunsOnSeveralThreadsAtOnceAgree(ArrangeAlgorithmType algorithm)
        {
            List<ArrangeItem> items = Crowd();
            ArrangeOptions options = OneRow(algorithm);
            ArrangeResult[] alone = Arranger.Run(items, options);

            var answers = new ArrangeResult[6][];
            var errors = new Exception[answers.Length];
            List<Thread> threads = Enumerable.Range(0, answers.Length).Select(k => new Thread(() =>
            {
                try
                {
                    answers[k] = Arranger.Run(items, options);
                }
                catch (Exception e)
                {
                    errors[k] = e;
                }
            })).ToList();

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join());

            Assert.All(errors, Assert.Null);
            Assert.All(answers, answer => Assert.Equal(alone, answer));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void EachItemIsAnsweredInItsPlaceAndANullWithDefault(ArrangeAlgorithmType algorithm)
        {
            ArrangeItem first = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            ArrangeItem second = LabelOn(new GeoLine2(200.0, 0.0, 240.0, 0.0));

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { first, null, second },
                new ArrangeOptions { Algorithm = algorithm, RowGap = 5.0 });

            Assert.Equal(3, results.Length);
            Assert.Equal(default(ArrangeResult), results[1]);
            Assert.True(results[0].Placed);
            Assert.True(results[2].Placed);

            // Each moves to one of its own candidates.
            Assert.Contains(first.GetPlacePoints(), p => p.IsEqualTo(first.Box.Center + results[0].Translation));
            Assert.Contains(second.GetPlacePoints(), p => p.IsEqualTo(second.Box.Center + results[2].Translation));
        }

        /// <summary>
        /// A region is kept clear of by every label, whichever item it is given to. The second pass gathered the regions
        /// of the labels it tried again only, so a region given to a label the first pass placed was lost: here the
        /// middle label, pushed off its clear place in the first pass by the third, which fell back onto it, went back
        /// onto the region in the second.
        /// </summary>
        [Theory]
        [InlineData(ArrangeAlgorithmType.Greedy)]
        [InlineData(ArrangeAlgorithmType.BoundedBacktracking)]
        [InlineData(ArrangeAlgorithmType.ConstraintSatisfaction)]
        public void TheSecondPassKeepsClearOfTheRegionsOfEveryItem(ArrangeAlgorithmType algorithm)
        {
            var region = new GeoPolygon2(new GeoPoint2(-100.0, 1.0), new GeoPoint2(200.0, 1.0), new GeoPoint2(200.0, 30.0), new GeoPoint2(-100.0, 30.0));
            var options = new ArrangeOptions
            {
                Algorithm = algorithm,
                PerpendicularLevels = 1,
                PlaceMostConstrainedFirst = false,
                PlaceFromInsideOut = false,
            };

            List<ArrangeItem> Scene(bool regionOnTheMiddleLabelToo)
            {
                ArrangeItem owner = LabelOn(new GeoLine2(1000.0, 0.0, 1100.0, 0.0));
                owner.BlockPolygons = new[] { region };
                ArrangeItem middle = LabelOn(new GeoLine2(0.0, 0.0, 100.0, 0.0));
                if (regionOnTheMiddleLabelToo)
                {
                    middle.BlockPolygons = new[] { region };
                }

                ArrangeItem stuck = LabelOn(new GeoLine2(101.0, -1.0, 1.0, -1.0));
                stuck.BlockLines = new[] { new GeoLine2(-200.0, -15.5, 300.0, -15.5) };
                return new List<ArrangeItem> { owner, middle, stuck };
            }

            ArrangeResult[] results = Arranger.Run(Scene(regionOnTheMiddleLabelToo: false), options);

            // Clear below the leader, 5 + 5 off, clear of the region above it.
            Assert.True(results[1].Placed, $"{algorithm}: {results[1]}");
            Assert.True(results[1].Translation.IsEqualTo(new GeoVector2(0.0, -10.0)), $"{algorithm}: {results[1]}");
            Assert.Equal(Arranger.Run(Scene(regionOnTheMiddleLabelToo: true), options), results);
        }

        [Fact]
        public void BlockListsStartEmptyAndNullReadsAsEmpty()
        {
            ArrangeItem fresh = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            Assert.Empty(fresh.BlockPolygons);
            Assert.Empty(fresh.BlockLines);

            fresh.BlockPolygons = null;
            fresh.BlockLines = null;

            ArrangeResult result = Assert.Single(Arranger.Run(new[] { fresh }));
            Assert.True(result.Placed);
        }

        /// <summary>
        /// The defaults were one shared instance: changing it changed every later run in the process, and its
        /// tolerance was whatever held on the thread that first touched it, a scope included, for good.
        /// </summary>
        [Fact]
        public void DefaultOptionsAreNotSharedAndFollowTheTolerance()
        {
            Assert.NotSame(ArrangeOptions.Default, ArrangeOptions.Default);

            ArrangeOptions.Default.RowGap = 1234.0;
            Assert.Equal(20.0, ArrangeOptions.Default.RowGap);

            var loose = new Tolerance(1E-3, 1E-3);
            using (Tolerance.Use(loose))
            {
                Assert.Equal(loose, ArrangeOptions.Default.Tolerance);
            }

            Assert.Equal(Tolerance.Global, ArrangeOptions.Default.Tolerance);
        }

        [Fact]
        public void AResultIsAValue()
        {
            var moved = new ArrangeResult(new GeoVector2(1.5, -2.0), true);

            Assert.Equal(new ArrangeResult(new GeoVector2(1.5, -2.0), true), moved);
            Assert.Equal(new ArrangeResult(new GeoVector2(1.5, -2.0), true).GetHashCode(), moved.GetHashCode());
            Assert.True(moved == new ArrangeResult(new GeoVector2(1.5, -2.0), true));
            Assert.True(moved != new ArrangeResult(new GeoVector2(1.5, -2.0), false));
            Assert.NotEqual(new ArrangeResult(new GeoVector2(1.5, -2.5), true), moved);
            Assert.False(moved.Equals((object)new GeoVector2(1.5, -2.0)));

            Assert.Equal(GeoVector2.Zero, default(ArrangeResult).Translation);
            Assert.False(default(ArrangeResult).Placed);

            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.Equal("ArrangeResult[placed, moved (1.5, -2)]", moved.ToString());
                Assert.Equal("ArrangeResult[not placed, moved (0, 0)]", default(ArrangeResult).ToString());
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }
    }
}
