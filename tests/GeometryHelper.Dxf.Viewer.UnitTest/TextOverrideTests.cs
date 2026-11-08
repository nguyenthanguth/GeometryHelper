using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>The text override of a dimension typed into Properties, as AutoCAD reads it.</summary>
    public class TextOverrideTests
    {
        [Fact]
        public void AnOverrideOfOneSpaceIsKept_AndTheDimensionShowsNoText_AfterSavingToo()
        {
            CadDocument document = Blank();
            Run(Session(document), "DLI", P(0, 0), P(300, 0), P(150, -30));
            string handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;

            CadEditResult result = CadPropertyEditor.Set(document, new[] { handle }, "Text override", " ");

            Assert.True(result.Succeeded, result.Message);
            Assert.Contains(result.Records, r => r.Handle == handle && r.NewValue == " ");
            Assert.Equal(" ", Entity<DxfRotatedDimension>(document, handle).DimensionText);
            Assert.Equal(string.Empty, Entity<DxfRotatedDimension>(document, handle).DimBlock.OfType<DxfMText>().Single().Contents);
            Assert.Equal(" ", CadProperties.Describe(document, null, new[] { handle }).Groups.SelectMany(g => g.Rows).Single(r => r.Label == "Text override").Value);

            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());
            Assert.Equal(" ", again.ModelSpace.OfType<DxfRotatedDimension>().Single().DimensionText);
        }

        [Fact]
        public void AnOverrideOfSpacesAroundAWordIsTrimmed_AndOneOfTwoSpacesIsTheMeasurement()
        {
            CadDocument document = Blank();
            Run(Session(document), "DLI", P(0, 0), P(300, 0), P(150, -30));
            string handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;

            Assert.True(CadPropertyEditor.Set(document, new[] { handle }, "Text override", "  Hold ").Succeeded);
            Assert.Equal("Hold", Entity<DxfRotatedDimension>(document, handle).DimensionText);

            Assert.True(CadPropertyEditor.Set(document, new[] { handle }, "Text override", "  ").Succeeded);
            Assert.Equal(string.Empty, Entity<DxfRotatedDimension>(document, handle).DimensionText);
        }
    }
}
