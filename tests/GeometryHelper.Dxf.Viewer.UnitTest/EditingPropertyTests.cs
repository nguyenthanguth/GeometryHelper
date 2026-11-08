using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingPropertyTests
    {
        private static string Shown(CadDocument document, string handle, string label)
            => CadProperties.Describe(document, null, new[] { handle }).Groups.SelectMany(g => g.Rows).Single(r => r.Label == label).Value;

        private static List<string> Rows(CadDocument document, string handle)
            => CadProperties.Describe(document, null, new[] { handle }).Groups.SelectMany(g => g.Rows).Select(r => r.Label + "=" + r.Value).ToList();

        [Theory]
        [InlineData("A1")]
        [InlineData("A3")]
        [InlineData("A4")]
        [InlineData("A6")]
        public void EveryRowThatCanBeEdited_TakesBackTheTextItShows_AndShowsTheSame(string handle)
        {
            CadDocument document = Plan();
            List<string> before = Rows(document, handle);

            foreach (CadPropertyRow row in CadProperties.Describe(document, null, new[] { handle }).Groups.SelectMany(g => g.Rows).Where(r => !r.IsReadOnly))
            {
                CadEditResult result = CadPropertyEditor.Set(document, new[] { handle }, row.Label, row.Value);
                Assert.True(result.Succeeded, row.Label + ": " + result.Message);
            }

            Assert.Equal(before, Rows(document, handle));
        }

        [Fact]
        public void TheLookOfALineIsSetAsThePaletteWritesIt()
        {
            CadDocument document = Plan();
            string[] line = { "A1" };

            Assert.True(CadPropertyEditor.Set(document, line, "Layer", "0").Succeeded);
            Assert.True(CadPropertyEditor.Set(document, line, "Color", "Red").Succeeded);
            Assert.True(CadPropertyEditor.Set(document, line, "Lineweight", "0.35 mm").Succeeded);
            Assert.True(CadPropertyEditor.Set(document, line, "Linetype scale", "2").Succeeded);
            Assert.True(CadPropertyEditor.Set(document, line, "Color", "8").Succeeded);

            Assert.Equal("0", Shown(document, "A1", "Layer"));
            Assert.Equal("8", Shown(document, "A1", "Color"));
            Assert.Equal("0.35 mm", Shown(document, "A1", "Lineweight"));
            Assert.Equal("2.0000", Shown(document, "A1", "Linetype scale"));
            Assert.Equal("Properties", document.NextUndo.Name);
        }

        [Fact]
        public void TheGeometryRowsOfALineACircleAnArcAndATextAreSet()
        {
            CadDocument document = Plan();
            document.Database.Auprec = 0;

            CadPropertyEditor.Set(document, new[] { "A1" }, "End Y", "40");
            CadPropertyEditor.Set(document, new[] { "A3" }, "Diameter", "50");
            CadPropertyEditor.Set(document, new[] { "A4" }, "End angle", "135");
            CadPropertyEditor.Set(document, new[] { "A6" }, "Contents", "BEAM B-12");
            CadPropertyEditor.Set(document, new[] { "A6" }, "Position X", "15");

            Near(new GeoPoint3(100, 40, 0), Entity<DxfLine>(document, "A1").EndPoint);
            Assert.Equal("25.0000", Shown(document, "A3", "Radius"));
            Assert.Equal("135", Shown(document, "A4", "End angle"));
            Assert.Equal("BEAM B-12", Shown(document, "A6", "Contents"));
            Near(new GeoPoint3(15, 200, 0), Entity<DxfDBText>(document, "A6").Position);
        }

        [Fact]
        public void WithNothingSelected_TheViewGroupShowsTheViewOfTheCanvas_WhenItIsGiven()
        {
            CadDocument document = Plan();
            var view = new CadView(1000, 500);
            view.ZoomTo(new DisplayBox(50, 25, 150, 75), 0.0);

            CadPropertyGroup shown = CadProperties.Describe(document, document.ModelSpace, null, view).Groups.Single(g => g.Title == "View");
            CadPropertyGroup drawn = CadProperties.Describe(document, document.ModelSpace, null).Groups.Single(g => g.Title == "View");

            Assert.Equal("100.0000", shown.Rows.Single(r => r.Label == "Center X").Value);
            Assert.Equal("50.0000", shown.Rows.Single(r => r.Label == "Center Y").Value);
            Assert.Equal("50.0000", shown.Rows.Single(r => r.Label == "Height").Value);
            Assert.Equal("100.0000", shown.Rows.Single(r => r.Label == "Width").Value);
            Assert.Equal("255.0000", drawn.Rows.Single(r => r.Label == "Center X").Value);
        }

        [Fact]
        public void ARefusalOfTheWindowIsReportedAsTheDocumentsOwnAre()
        {
            CadEditResult refused = CadEditResult.Refuse("The selection holds a projected line.");

            Assert.True(refused.Refused);
            Assert.False(refused.Succeeded);
            Assert.Equal("The selection holds a projected line.", refused.Message);
            Assert.Empty(refused.Records);
        }

        [Fact]
        public void OneSetOfSeveralEntitiesIsOneUndoStep()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();

            CadEditResult result = CadPropertyEditor.Set(document, new[] { "A3", "A2", "A1" }, "Color", "Blue");

            Assert.Equal(new[] { "A1", "A2", "A3" }, result.Records.Select(r => r.Handle));
            Assert.Equal("ByLayer", result.Records[0].OldValue);
            Assert.Equal("Blue", result.Records[0].NewValue);
            Assert.Equal("Blue", Shown(document, "A2", "Color"));
            document.Undo();
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void AValueThatCannotBeTaken_IsRefusedWithItsReasonAndNothingWritten()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();

            Assert.Equal("The drawing has no layer \"Nope\".", CadPropertyEditor.Set(document, new[] { "A1" }, "Layer", "Nope").Message);
            Assert.True(CadPropertyEditor.Set(document, new[] { "A3" }, "Radius", "-2").Refused);
            Assert.True(CadPropertyEditor.Set(document, new[] { "A1" }, "Length", "20").Refused);
            Assert.True(CadPropertyEditor.Set(document, new[] { "A1" }, "Lineweight", "0.33").Refused);
            Assert.True(CadPropertyEditor.Set(document, new[] { "A1", "A3" }, "Radius", "5").Refused);
            Assert.True(CadPropertyEditor.Set(document, new[] { "A8" }, "Color", "Red").Refused);
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void WithNothingSelected_TheCurrentSettingsAreSet_WhichNewEntitiesTake()
        {
            CadDocument document = Plan();
            CadPropertyEditor.Set(document, null, "Layer", "Steel");
            CadPropertyEditor.Set(document, null, "Color", "Green");

            CadCommandSession session = Session(document);
            Run(session, "LINE", P(1000, 0), P(1100, 0), Enter);

            DxfLine made = All<DxfLine>(document.ModelSpace).Last();
            Assert.Equal("Steel", made.Layer);
            Assert.Equal(3, made.ColorIndex);
            Assert.Equal("Steel", CadProperties.Describe(document, null, null).Groups[0].Rows.Single(r => r.Label == "Layer").Value);
        }
    }
}
