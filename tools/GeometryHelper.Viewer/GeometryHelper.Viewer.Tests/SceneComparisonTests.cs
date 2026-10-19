using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class SceneComparisonTests
    {
        private static GeoPolygon2 Triangle(double x = 10.0) => new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(x, 0), new GeoPoint2(0, 10));

        private static Scene Make(params Action<SceneWriter>[] steps)
        {
            var writer = new SceneWriter("hand scene");

            foreach (Action<SceneWriter> step in steps)
            {
                step(writer);
            }

            return writer.Scene;
        }

        private static Action<SceneWriter> Poly(string name, double x = 10.0, string layer = "L") => w => w.Add(Triangle(x), layer: layer, name: name);

        private static Action<SceneWriter> Line(string name, double x2 = 5.0, string layer = "L") => w => w.Add(new GeoLine2(0, 0, x2, 5), layer: layer, name: name);

        private static Action<SceneWriter> Point(string name, double x = 1.0, string layer = "L") => w => w.Add(new GeoPoint2(x, 2), layer: layer, name: name);

        [Fact]
        public void TwoScenesOfTheSameShapes_AreIdenticalItemByItem()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("a"), Line("b"), Point("c")), Make(Poly("a"), Line("b"), Point("c")));

            Assert.True(comparison.AreIdentical);
            Assert.Equal(3, comparison.Items.Count);
            Assert.All(comparison.Items, item => Assert.True(item.DataIdentical));
            Assert.All(comparison.Items, item => Assert.Equal(CompareMatch.Name, item.Match));
            Assert.Equal(0, comparison.ReorderedCount);
            Assert.Equal("3 item(s): 3 identical, 0 changed, 0 only left, 0 only right", comparison.Summary);
            Assert.Empty(comparison.Notes);
        }

        [Fact]
        public void AVertexMovedByOneTenMillionth_IsAChangedItemWithTheExactDelta()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("a"), Line("b")), Make(Poly("a", 10.0000001), Line("b")));

            ComparedItem moved = comparison.Items[0];
            Assert.Equal(CompareStatus.Changed, moved.Status);
            Assert.True(moved.SameShape);
            Assert.False(moved.DataIdentical);
            Assert.Equal(1, moved.DifferingNumbers);
            Assert.Equal(Math.Abs(10.0000001 - 10.0), moved.MaxDelta);
            Assert.Equal(CompareStatus.Identical, comparison.Items[1].Status);
            Assert.Contains("1 number(s) differ", moved.Difference, StringComparison.Ordinal);
        }

        [Fact]
        public void ANumberThatDiffersInTheLastBitOnly_ChangesTheItem()
        {
            double next = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(1.0) + 1);

            SceneComparison comparison = SceneComparison.Compare(Make(Point("p", 1.0)), Make(Point("p", next)));

            Assert.Equal(CompareStatus.Changed, comparison.Items.Single().Status);
            Assert.Equal(next - 1.0, comparison.Items.Single().MaxDelta);
        }

        [Fact]
        public void NegativeZeroAgainstZero_IsAChangedItem_SinceTheComparisonIsOfTheBits()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Point("p", 0.0)), Make(Point("p", -0.0)));

            ComparedItem item = comparison.Items.Single();
            Assert.Equal(CompareStatus.Changed, item.Status);
            Assert.Equal(1, item.DifferingNumbers);
            Assert.Equal(0.0, item.MaxDelta);
        }

        [Fact]
        public void AnExtraItemOnTheRight_IsOnlyRight_AndTheOthersAreUntouched()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("a"), Line("b")), Make(Poly("a"), Line("b"), Point("extra")));

            Assert.Equal(new[] { CompareStatus.Identical, CompareStatus.Identical, CompareStatus.OnlyRight }, comparison.Items.Select(i => i.Status));
            ComparedItem extra = comparison.Items[2];
            Assert.Equal(-1, extra.LeftIndex);
            Assert.Equal(2, extra.RightIndex);
            Assert.Equal("extra", extra.Name);
            Assert.Equal(CompareMatch.None, extra.Match);
            Assert.False(comparison.AreIdentical);
        }

        [Fact]
        public void AnItemMissingOnTheRight_IsOnlyLeft()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("a"), Line("b")), Make(Line("b")));

            Assert.Equal(new[] { CompareStatus.OnlyLeft, CompareStatus.Identical }, comparison.Items.Select(i => i.Status));
            Assert.Equal(0, comparison.Items[0].LeftIndex);
            Assert.Equal(-1, comparison.Items[0].RightIndex);
        }

        [Fact]
        public void ItemsInAnotherOrder_AreFoundByNameAndOnlyTheOneThatMovedIsFlagged()
        {
            // a b c against b c a: the climb b, c is kept in place, and a is the one that moved.
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("a"), Line("b"), Point("c")), Make(Line("b"), Point("c"), Poly("a")));

            Assert.True(comparison.AreIdentical);
            Assert.Equal(new[] { true, false, false }, comparison.Items.Select(i => i.Reordered));
            Assert.Equal(1, comparison.ReorderedCount);
            Assert.Equal(2, comparison.Items[0].RightIndex);
            Assert.EndsWith(", 1 moved", comparison.Summary, StringComparison.Ordinal);
        }

        [Fact]
        public void ItemsReversed_FlagAllButOneOfThem()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("a"), Line("b"), Point("c"), Poly("d")), Make(Poly("d"), Point("c"), Line("b"), Poly("a")));

            Assert.True(comparison.AreIdentical);
            Assert.Equal(3, comparison.ReorderedCount);
        }

        [Fact]
        public void ALayerRenamed_LeavesItsItemsUnpaired_OnBothSides()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("a", 10.0, "old"), Line("b", 5.0, "old")), Make(Poly("a", 10.0, "new"), Line("b", 5.0, "new")));

            Assert.Equal(2, comparison.Count(CompareStatus.OnlyLeft));
            Assert.Equal(2, comparison.Count(CompareStatus.OnlyRight));
            Assert.Equal(0, comparison.Count(CompareStatus.Identical));
            Assert.All(comparison.Items.Take(2), item => Assert.Equal("old", item.Layer));
            Assert.All(comparison.Items.Skip(2), item => Assert.Equal("new", item.Layer));
        }

        [Fact]
        public void AnItemRenamedOnItsLayer_IsPairedByOrderWithItsKind_AndSaysSo()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("before"), Line("kept")), Make(Poly("after", 12.0), Line("kept")));

            ComparedItem renamed = comparison.Items[0];
            Assert.Equal(CompareMatch.Order, renamed.Match);
            Assert.Equal(CompareStatus.Changed, renamed.Status);
            Assert.Equal("before", renamed.Name);
            Assert.Equal(CompareMatch.Name, comparison.Items[1].Match);
        }

        [Fact]
        public void TwoItemsOfOneNameAndLayer_ArePairedFirstWithFirstAndSecondWithSecond()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("twin", 10.0), Poly("twin", 20.0)), Make(Poly("twin", 10.0), Poly("twin", 21.0)));

            Assert.Equal(new[] { CompareStatus.Identical, CompareStatus.Changed }, comparison.Items.Select(i => i.Status));
            Assert.Equal(new[] { 0, 1 }, comparison.Items.Select(i => i.RightIndex));
        }

        [Fact]
        public void ABodyOfAnotherKind_IsChanged_AndNamesBothKinds()
        {
            SceneComparison comparison = SceneComparison.Compare(
                Make(w => w.Add(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(1, 0), new GeoPoint2(1, 1)), name: "shape")),
                Make(w => w.Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(1, 0), new GeoPoint2(1, 1)), name: "shape")));

            ComparedItem item = comparison.Items.Single();
            Assert.Equal(CompareStatus.Changed, item.Status);
            Assert.Equal("GeoPolyline2", item.Kind);
            Assert.Equal("GeoPolygon2", item.RightKind);
            Assert.False(item.SameShape);
            Assert.StartsWith("kind GeoPolyline2 -> GeoPolygon2", item.Difference, StringComparison.Ordinal);
        }

        [Fact]
        public void AVertexAdded_IsAChangeOfLayout_NotOfANumber()
        {
            var left = Make(w => w.Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10)), name: "loop"));
            var right = Make(w => w.Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10)), name: "loop"));

            ComparedItem item = SceneComparison.Compare(left, right).Items.Single();

            Assert.Equal(CompareStatus.Changed, item.Status);
            Assert.False(item.SameShape);
            Assert.True(double.IsNaN(item.MaxDelta));
            Assert.Contains("different layout", item.Difference, StringComparison.Ordinal);
        }

        [Fact]
        public void TheNumbersEachBuildGave_AreSetSideBySide_AndTheDifferenceIsWrittenToTenDigits()
        {
            Scene left = Make(Poly("a", 10.0));
            Scene right = Make(Poly("a", 12.0));
            left.Items[0].Properties["measure.length"] = "34.142135623730951";
            left.Items[0].Properties["measure.area"] = "50";
            left.Items[0].Properties["measure.vertices"] = "3";
            right.Items[0].Properties["measure.length"] = "37.234569999999998";
            right.Items[0].Properties["measure.area"] = "60";
            right.Items[0].Properties["measure.vertices"] = "3";

            ComparedItem item = SceneComparison.Compare(left, right).Items.Single();

            Assert.Equal(34.142135623730951, item.Left.Length);
            Assert.Equal(60.0, item.Right.Area);
            Assert.Equal(3, item.Right.Vertices);
            Assert.Contains("Length +3.092434376", item.Difference, StringComparison.Ordinal);
            Assert.Contains("Area +10", item.Difference, StringComparison.Ordinal);
            Assert.DoesNotContain("Vertices", item.Difference, StringComparison.Ordinal);
        }

        [Fact]
        public void IdenticalData_WithDifferentProperties_IsStillIdentical()
        {
            Scene left = Make(Poly("a"));
            Scene right = Make(Poly("a"));
            right.Items[0].Properties["note"] = "anything";
            right.Items[0].Color = "#112233";

            Assert.True(SceneComparison.Compare(left, right).AreIdentical);
        }

        [Fact]
        public void ScenesWrittenAtDifferentTolerances_SayAsMuch()
        {
            Scene left = Make(Poly("a"));
            Scene right = Make(Poly("a"));
            right.SetTolerance(new Tolerance(0.05, 1E-5));

            SceneComparison comparison = SceneComparison.Compare(left, right);

            string note = Assert.Single(comparison.Notes);
            Assert.Contains("different tolerances", note, StringComparison.Ordinal);
            Assert.Contains("point 0.05", note, StringComparison.Ordinal);
        }

        [Fact]
        public void TheRows_AreNumberedFromOne_InTheOrderOfTheLeftThenTheOnlyRight()
        {
            SceneComparison comparison = SceneComparison.Compare(Make(Poly("a"), Line("b")), Make(Point("z"), Line("b"), Poly("a")));

            Assert.Equal(new[] { 1, 2, 3 }, comparison.Items.Select(i => i.Number));
            Assert.Equal(new[] { "a", "b", "z" }, comparison.Items.Select(i => i.Name));
            Assert.Equal(CompareStatus.OnlyRight, comparison.Items[2].Status);
        }

        [Fact]
        public void ABodyWithNumbersOfTheSameKind_IsFoundIdenticalWhateverTheSizeOfTheData()
        {
            GeoSolid3 box = SampleShapes.Box(30, 20, 10, true);

            SceneComparison comparison = SceneComparison.Compare(Make(w => w.Add(box, name: "box")), Make(w => w.Add(box, name: "box")));

            Assert.True(comparison.AreIdentical);
        }

        [Fact]
        public void ANullScene_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => SceneComparison.Compare(null, new Scene()));
            Assert.Throws<ArgumentNullException>(() => SceneComparison.Compare(new Scene(), null));
        }

        [Fact]
        public void ComparingTwice_GivesTheSameRowsInTheSameOrder()
        {
            Scene left = Make(Poly("a"), Line("b"), Point("c"), Poly("a", 11.0));
            Scene right = Make(Poly("a", 11.0), Line("b", 6.0), Poly("a"), Point("d"));

            string First() => string.Join("|", SceneComparison.Compare(left, right).Items.Select(i => i.Number + ":" + i.Status + ":" + i.Match + ":" + i.LeftIndex + ":" + i.RightIndex + ":" + i.Reordered));

            Assert.Equal(First(), First());
        }
    }

    public class ComparisonSceneTests
    {
        private static Scene Make(params Action<SceneWriter>[] steps)
        {
            var writer = new SceneWriter("side");

            foreach (Action<SceneWriter> step in steps)
            {
                step(writer);
            }

            return writer.Scene;
        }

        private static (Scene Left, Scene Right, ComparisonScene Layout) Build()
        {
            // same, changed, only left, only right.
            Scene left = Make(
                w => w.Add(new GeoPoint2(1, 1), name: "same"),
                w => w.Add(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100)), name: "moved"),
                w => w.Add(new GeoPoint2(5, 5), name: "gone"));
            Scene right = Make(
                w => w.Add(new GeoPoint2(1, 1), name: "same"),
                w => w.Add(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 110)), name: "moved"),
                w => w.Add(new GeoLine2(0, 0, 9, 9), name: "new"));

            return (left, right, ComparisonScene.Build(SceneComparison.Compare(left, right), left, right));
        }

        [Fact]
        public void TheCombinedScene_HoldsTheSharedItemOnce_AndAChangedItemTwice_OnLayersOfTheirOwn()
        {
            var (_, _, layout) = Build();

            Assert.Equal(
                new[] { ComparisonScene.IdenticalLayer, ComparisonScene.ChangedLeftLayer, ComparisonScene.ChangedRightLayer, ComparisonScene.OnlyLeftLayer, ComparisonScene.OnlyRightLayer },
                layout.Scene.Layers.Select(l => l.Name));
            Assert.Equal(
                new[] { ComparisonScene.IdenticalColor, ComparisonScene.ChangedLeftColor, ComparisonScene.ChangedRightColor, ComparisonScene.OnlyLeftColor, ComparisonScene.OnlyRightColor },
                layout.Scene.Layers.Select(l => l.Color));
            Assert.Equal(5, layout.Scene.Items.Count);
            Assert.Equal(new[] { "same", "moved", "moved", "gone", "new" }, layout.Scene.Items.Select(i => i.Name));
            Assert.Equal(new[] { CompareSide.Both, CompareSide.Left, CompareSide.Right, CompareSide.Left, CompareSide.Right }, Enumerable.Range(0, 5).Select(layout.SideOf));
        }

        [Fact]
        public void ALayerNothingIsOn_IsNotMade()
        {
            var writer = new SceneWriter();
            writer.Add(new GeoPoint2(1, 1), name: "a");
            Scene scene = writer.Scene;

            ComparisonScene layout = ComparisonScene.Build(SceneComparison.Compare(scene, scene), scene, scene);

            Assert.Equal(new[] { ComparisonScene.IdenticalLayer }, layout.Scene.Layers.Select(l => l.Name));
            Assert.Empty(layout.DashedItems);
        }

        [Fact]
        public void TheRowsAndTheItems_PointAtEachOther()
        {
            var (_, _, layout) = Build();

            Assert.Equal(new[] { 0 }, layout.ItemsOfRow(0));
            Assert.Equal(new[] { 1, 2 }, layout.ItemsOfRow(1));
            Assert.Equal(new[] { 3 }, layout.ItemsOfRow(2));
            Assert.Equal(new[] { 4 }, layout.ItemsOfRow(3));
            Assert.Equal(new[] { 0, 1, 1, 2, 3 }, Enumerable.Range(0, 5).Select(layout.RowOf));
            Assert.Equal(new[] { 1 }, layout.DashedItems);
        }

        [Fact]
        public void TheSideShown_HidesTheOtherSidesOwnItemsAndNothingTheyShare()
        {
            var (_, _, layout) = Build();

            Assert.Equal(new[] { 2, 4 }, layout.HiddenFor(CompareView.Left));
            Assert.Equal(new[] { 1, 3 }, layout.HiddenFor(CompareView.Right));
            Assert.Empty(layout.HiddenFor(CompareView.Both));
        }

        [Fact]
        public void EachItemCarriesWhereItCameFrom_AndItsOwnProperties()
        {
            Scene left = Make(w => w.Add(new GeoPoint2(1, 1), name: "p", props: new { tag = "kept" }));
            Scene right = Make(w => w.Add(new GeoPoint2(2, 1), name: "p"));

            ComparisonScene layout = ComparisonScene.Build(SceneComparison.Compare(left, right), left, right);

            SceneItem leftCopy = layout.Scene.Items[0];
            Assert.Equal("kept", leftCopy.Properties["tag"]);
            Assert.Equal("1", leftCopy.Properties["compare.row"]);
            Assert.Equal("Changed", leftCopy.Properties["compare.status"]);
            Assert.Equal("Left", leftCopy.Properties["compare.side"]);
            Assert.Equal("Right", layout.Scene.Items[1].Properties["compare.side"]);
            Assert.False(left.Items[0].Properties.ContainsKey("compare.row"));
        }

        [Fact]
        public void TheDashes_CutTheLinesOfTheLeftCopyOnly_AndTheirTotalIsAboutHalf()
        {
            var (_, _, layout) = Build();
            Primitives drawn = Primitives.Build(layout.Scene);
            int rightLines = drawn.Items[2].Polylines.Count;
            double before = drawn.Items[1].Polylines.Sum(l => Length(l));
            Assert.Single(drawn.Items[1].Polylines);

            string warning = layout.ApplyDashes(drawn);

            Assert.Null(warning);
            List<DrawPolyline> pieces = drawn.Items[1].Polylines;
            Assert.True(pieces.Count > 10, pieces.Count + " dashes");
            Assert.Equal(rightLines, drawn.Items[2].Polylines.Count);
            double dash = drawn.Bounds.Size.Length / 150.0;
            Assert.All(pieces.Take(pieces.Count - 1), piece => Assert.Equal(dash, Length(piece), 6));
            Assert.InRange(pieces.Sum(Length) / before, 0.45, 0.55);
        }

        private static double Length(DrawPolyline line)
        {
            double length = 0.0;

            for (int i = 1; i < line.Points.Length; i++)
            {
                length += (line.Points[i] - line.Points[i - 1]).Length;
            }

            return length;
        }

        [Fact]
        public void TheDashes_ContinueAroundACorner_NotRestartThere()
        {
            // The legs are 100 and 110.8 long, so that a dash is a hundred and fiftieth of 149.25, 0.995, and the corner at 100 falls a quarter of the way into the 50th dash.
            Scene left = Make(w => w.Add(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 110.8)), name: "bent"));
            Scene right = Make(w => w.Add(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 110.9)), name: "bent"));
            ComparisonScene layout = ComparisonScene.Build(SceneComparison.Compare(left, right), left, right);
            Primitives drawn = Primitives.Build(layout.Scene);

            layout.ApplyDashes(drawn);

            // A dash that straddles the corner is one piece of three points.
            Assert.Contains(drawn.Items[0].Polylines, piece => piece.Points.Length == 3);
        }

        [Fact]
        public void ANullArgument_IsRefused()
        {
            var (left, right, layout) = Build();

            Assert.Throws<ArgumentNullException>(() => ComparisonScene.Build(null, left, right));
            Assert.Throws<ArgumentNullException>(() => ComparisonScene.Build(layout.Comparison, null, right));
            Assert.Throws<ArgumentNullException>(() => ComparisonScene.Build(layout.Comparison, left, null));
            Assert.Throws<ArgumentNullException>(() => layout.ApplyDashes(null));
        }
    }

    public class CompareKeysTests
    {
        [Theory]
        [InlineData("Ctrl+Shift+C", "Compare")]
        [InlineData("1", "CompareLeft")]
        [InlineData("2", "CompareRight")]
        [InlineData("3", "CompareBoth")]
        [InlineData("Ctrl+T", "ExportTest")]
        public void TheDefaultKeymap_BindsTheKeysOfTheComparison(string gesture, string action)
        {
            Assert.Equal(action, Keymap.Default().ActionFor(Gesture.Parse(gesture)));
            Assert.True(KeymapActions.All.Single(a => a.Name == action).Implemented);
        }
    }
}
