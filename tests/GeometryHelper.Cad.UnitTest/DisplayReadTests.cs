using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.TestDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What the display reads of an entity in one pass of its groups, where each property read alone looked for its own:
    /// the picture is the same, and an entity edited after it was drawn is drawn with what it has now.
    /// </summary>
    public class DisplayReadTests
    {
        private static CadDocument Plan()
        {
            DxfDatabase database = Drawing(
                Layer("Steel", 1),
                string.Empty,
                Text(10, 20, 5, "PLATE", "Steel", handle: "A1"),
                Polyline("Steel", "A2", true, 0, 0, 0, 20, 0, 0.5, 20, 20, 0, 0, 20, 0),
                Line(0, 0, 30, 0, "Steel", handle: "A3")
            );
            return CadDocument.From(database, "plan.dxf");
        }

        private static DisplayText2 TheText(CadDocument document) =>
            Assert.IsType<DisplayText2>(
                Assert.Single(document.GetDisplay(document.ModelSpace).Items, i => i.Handle == "A1")
            );

        private static void Edit(CadDocument document, string handle, System.Action<DxfEntity> change) =>
            document.ApplyEdit("EDIT", new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle) }, s =>
                change(s.Document.FindEntity(handle))
            );

        [Fact]
        public void ATextEditedAfterItWasDrawn_IsDrawnWithItsNewWordsPlaceAndHeight()
        {
            CadDocument document = Plan();
            DisplayText2 before = TheText(document);

            Assert.Equal("PLATE", before.Text);
            Assert.Equal(10.0, before.X);
            Assert.Equal(20.0, before.Y);
            Assert.Equal(5.0, before.Height);

            Edit(document, "A1", e =>
            {
                var text = (DxfDBText)e;
                text.TextString = "BEAM";
                text.Height = 8.0;
                text.Position = new GeoPoint3(40, 50, 0);
                text.Rotation = 0.5;
            });
            DisplayText2 after = TheText(document);

            Assert.Equal("BEAM", after.Text);
            Assert.Equal(40.0, after.X);
            Assert.Equal(50.0, after.Y);
            Assert.Equal(8.0, after.Height);
            Assert.Equal(0.5, after.Rotation, 12);

            // What a REGEN draws of the edited drawing is what the patch drew.
            document.Regen();
            DisplayText2 regen = TheText(document);

            Assert.Equal(after.Text, regen.Text);
            Assert.Equal(after.X, regen.X);
            Assert.Equal(after.Y, regen.Y);
            Assert.Equal(after.Height, regen.Height);
            Assert.Equal(after.Rotation, regen.Rotation);
        }

        [Fact]
        public void ATextMadeHiddenAfterItWasDrawn_IsLeftOut_AndDrawnAgainWhenShown()
        {
            CadDocument document = Plan();

            Assert.Contains(document.GetDisplay(document.ModelSpace).Items, i => i.Handle == "A1");

            Edit(document, "A1", e => e.Visible = false);
            Assert.DoesNotContain(document.GetDisplay(document.ModelSpace).Items, i => i.Handle == "A1");

            Edit(document, "A1", e => e.Visible = true);
            Assert.Contains(document.GetDisplay(document.ModelSpace).Items, i => i.Handle == "A1");
        }

        [Fact]
        public void APolylineGivenAWidthAfterItWasDrawn_IsDrawnWide_AndThinAgainWhenTheWidthGoes()
        {
            CadDocument document = Plan();
            List<DisplayItem2> thin = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == "A2")
                .ToList();

            Assert.NotEmpty(thin);
            Assert.All(thin, i => Assert.False(i is DisplayFill2));

            Edit(document, "A2", e => ((DxfPolyline)e).ConstantWidth = 1.5);
            List<DisplayItem2> wide = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == "A2")
                .ToList();

            Assert.Contains(wide, i => i is DisplayFill2);

            Edit(document, "A2", e => ((DxfPolyline)e).ConstantWidth = 0.0);
            List<DisplayItem2> again = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == "A2")
                .ToList();

            Assert.Equal(thin.Count, again.Count);
            Assert.All(again, i => Assert.False(i is DisplayFill2));
        }

        [Fact]
        public void ABlockOfAFileWithNoBlockRecordsInsertedThreeTimes_IsDrawnThreeTimes()
        {
            // The block has no handle, as in a file of R12: it is kept by its name for the INSERTs that follow.
            DxfDatabase database = Drawing(
                Layer("Steel", 1),
                Block("PAIR", Line(0, 0, 10, 0, "0"), Circle(5, 5, 2, "0")),
                Insert("PAIR", 0, 0, 1.0, 0.0, "Steel", handle: "B1"),
                Insert("PAIR", 100, 0, 1.0, 90.0, "Steel", handle: "B2"),
                Insert("PAIR", 200, 0, 2.0, 0.0, "Steel", handle: "B3")
            );
            var document = CadDocument.From(database, "blocks.dxf");
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.Equal(6, list.Count);
            Assert.Equal(2, list.Items.Count(i => i.Handle == "B1"));
            Assert.Equal(2, list.Items.Count(i => i.Handle == "B2"));
            Assert.Equal(2, list.Items.Count(i => i.Handle == "B3"));

            // The second is turned a quarter: its line, 10 along from (100, 0), runs up.
            DisplayItem2 turned = list.Items.First(i => i.Handle == "B2");
            Assert.Equal(100.0 - 0.0, turned.Bounds.MinX, 6);
            Assert.Equal(10.0, turned.Bounds.MaxY, 6);
        }
    }
}
