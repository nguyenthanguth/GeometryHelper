using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class PickTests
    {
        private static PickIndex Index(params string[] entities)
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, entities), "test.dxf");
            return document.GetPickIndex(document.ModelSpace);
        }

        [Fact]
        public void APointNearALinePicksItAndOneFartherThanTheToleranceDoesNot()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 0, handle: "A1"), TestDrawings.Line(0, 50, 100, 50, handle: "A2"));

            Assert.Equal(new[] { "A1" }, index.Pick(40, 1.5, 2.0));
            Assert.Empty(index.Pick(40, 20, 2.0));
        }

        [Fact]
        public void TwoEntitiesWithinTheToleranceComeBackNearestFirst()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 0, handle: "A1"), TestDrawings.Line(0, 1.5, 100, 1.5, handle: "A2"));

            Assert.Equal(new[] { "A2", "A1" }, index.Pick(40, 1.0, 3.0));
        }

        [Fact]
        public void APointInsideACircleDoesNotPickItButOneOnTheRimDoes()
        {
            PickIndex index = Index(TestDrawings.Circle(0, 0, 10, handle: "A1"));

            Assert.Empty(index.Pick(0, 0, 1.0));
            Assert.Equal(new[] { "A1" }, index.Pick(10.4, 0, 1.0));
        }

        [Fact]
        public void AnArcIsPickedOnItsSweepAndNotOnTheRestOfItsCircle()
        {
            PickIndex index = Index(TestDrawings.Arc(0, 0, 10, 0, 90, handle: "A1"));

            Assert.Equal(new[] { "A1" }, index.Pick(7.07, 7.07, 0.5));
            Assert.Empty(index.Pick(-7.07, -7.07, 0.5));
        }

        [Fact]
        public void AWindowTakesTheEntitiesWhollyInsideAndACrossingBoxTheOnesItTouches()
        {
            PickIndex index = Index(
                TestDrawings.Line(10, 10, 20, 10, handle: "A1"),
                TestDrawings.Line(10, 10, 200, 10, handle: "A2"),
                TestDrawings.Line(500, 500, 600, 600, handle: "A3"));
            var box = new DisplayBox(0, 0, 100, 100);

            Assert.Equal(new[] { "A1" }, index.Window(box));
            Assert.Equal(new[] { "A1", "A2" }, index.Crossing(box));
        }

        [Fact]
        public void ACrossingBoxThatOnlyTheMiddleOfALineCrossesStillTakesIt()
        {
            PickIndex index = Index(TestDrawings.Line(-100, 50, 200, 50, handle: "A1"));

            Assert.Equal(new[] { "A1" }, index.Crossing(new DisplayBox(0, 0, 100, 100)));
            Assert.Empty(index.Window(new DisplayBox(0, 0, 100, 100)));
        }

        [Fact]
        public void AnInsertIsSelectedAsOneEntityWhateverItsBlockHolds()
        {
            string block = TestDrawings.Block("TWO", TestDrawings.Line(0, 0, 10, 0), TestDrawings.Line(0, 5, 10, 5));
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, block, TestDrawings.Insert("TWO", 0, 0, handle: "B1")), "test.dxf");
            PickIndex index = document.GetPickIndex(document.ModelSpace);

            Assert.Equal(new[] { "B1" }, index.Window(new DisplayBox(-1, -1, 11, 6)));
            Assert.Empty(index.Window(new DisplayBox(-1, -1, 11, 2)));
            Assert.Equal(new[] { "B1" }, index.Pick(5, 5, 0.5));
        }

        [Fact]
        public void TheSelectionTellsWhenItChangesAndNotWhenItDoesNot()
        {
            var selection = new CadSelection();
            int changes = 0;
            selection.Changed += (s, e) => changes++;

            selection.Add(new[] { "A1", "A2" });
            selection.Add(new[] { "A1" });
            selection.Toggle("A2");
            selection.Set(new[] { "A1" });
            selection.Clear();
            selection.Clear();

            Assert.Equal(3, changes);
            Assert.Equal(0, selection.Count);
        }

        [Fact]
        public void TheItemsInABoxComeInDrawingOrderAndAreThoseEveryItemsBoxWouldGive()
        {
            // 900 lines and circles on a grid 30 apart, and a line across them all, which the index keeps aside as large.
            var entities = new System.Collections.Generic.List<string>();

            for (int i = 0; i < 900; i++)
            {
                double x = (i % 30) * 30.0;
                double y = (i / 30) * 30.0;
                entities.Add(i % 2 == 0 ? TestDrawings.Line(x, y, x + 20, y + 5) : TestDrawings.Circle(x + 10, y + 10, 4));
            }

            entities.Add(TestDrawings.Line(-10, -10, 900, 900, handle: "BIG"));
            PickIndex index = Index(entities.ToArray());
            var random = new Random(3);

            for (int k = 0; k < 200; k++)
            {
                double x0 = random.NextDouble() * 1000 - 50;
                double y0 = random.NextDouble() * 1000 - 50;
                var box = new DisplayBox(x0, y0, x0 + random.NextDouble() * 300, y0 + random.NextDouble() * 300);
                int[] expected = Enumerable.Range(0, index.List.Count).Where(i => index.List.Items[i].Bounds.Intersects(box)).ToArray();

                Assert.Equal(expected, index.IndicesIn(box));
            }

            // A box over everything gives every item; each entity's own items come back by its handle.
            Assert.Equal(Enumerable.Range(0, index.List.Count), index.IndicesIn(index.List.Bounds.Inflate(1)));
            Assert.Equal(new[] { index.List.Count - 1 }, index.IndicesOf("BIG"));
            Assert.Empty(index.IndicesOf("NONE"));
        }
    }
}
