using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class TextTests
    {
        private static DisplayText2 MText(string contents, Action<DxfDatabase, DxfMText> more = null)
        {
            var database = new DxfDatabase("AC1018");
            var mtext = new DxfMText { Contents = contents, Location = new GeoPoint3(10, 20, 0), TextHeight = 2.5 };
            database.ModelSpace.AppendEntity(mtext);
            more?.Invoke(database, mtext);
            CadDocument document = CadDocument.From(database);
            return Assert.IsType<DisplayText2>(Assert.Single(document.GetDisplay(document.ModelSpace).Items));
        }

        [Theory]
        [InlineData(@"B-12\P{\C1;\LM16\l}", "B-12\nM16")]
        [InlineData(@"{\fArial|b1|i0|c0|p34;Bold} and \H5x;big", "Bold and big")]
        [InlineData(@"a\~b", "a b")]
        [InlineData(@"\U+2205" + "20", "∅20")]
        [InlineData(@"45%%d %%p0.5 %%c20", "45° ±0.5 ∅20")]
        [InlineData(@"\A1;1\S1^2;", "11/2")]
        public void TheCodesOfAnMTextComeOutAsItsPlainWordsWithLineFeeds(string contents, string shown)
        {
            Assert.Equal(shown, MText(contents).Text);
        }

        [Theory]
        [InlineData(@"\W0.8000;PLATE", 0.8)]
        [InlineData(@"{\H1.2x;\W0.75;PLATE}", 0.75)]
        [InlineData(@"\W2;\W0.4x;PLATE", 0.8)]
        [InlineData(@"PLATE", 1.0)]
        public void AnMTextIsDrawnAtTheWidthItsCodesSetBeforeItsWordsAndMeasuredAtIt(string contents, double factor)
        {
            DisplayText2 shown = MText(contents);

            // PLATE 2.5 high, five letters each 0.6 of the height wide at a width factor of 1: 7.5 wide, 6 at 0.8.
            Assert.Equal("PLATE", shown.Text);
            Assert.Equal(factor, shown.WidthFactor, 9);
            Assert.Equal(5 * 2.5 * 0.6 * factor, shown.EstimatedWidth, 9);
        }

        [Fact]
        public void AnMTextWrittenByTheMTextCommandAtAWidthFactorIsDrawnAtItAsTheTextIs()
        {
            CadDocument document = EditingDrawings.Blank();
            CadCommandSession session = EditingDrawings.Session(document);
            session.Remembered["text.widthFactor"] = 0.8;

            EditingDrawings.Run(session, "DT", EditingDrawings.P(0, 0), "2.5", "0", "PLATE", EditingDrawings.Enter);
            EditingDrawings.Run(session, "T", EditingDrawings.P(0, 40), EditingDrawings.P(50, 30), "PLATE");

            DisplayText2[] shown = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().ToArray();
            Assert.Equal(2, shown.Length);
            Assert.All(shown, t => Assert.Equal(0.8, t.WidthFactor, 9));
        }

        [Fact]
        public void ATextCarriesItsStylesTypefaceAndItsOwnSlant()
        {
            var database = new DxfDatabase("AC1018");
            var style = new DxfTextStyleTableRecord { Name = "GH_ARIAL", FileName = "arial.ttf" };
            database.TextStyleTable.Add(style);
            style.Font = new DxfFontDescriptor("Arial", false, false, 0, 34);
            var text = new DxfDBText { TextString = "SLANT", Position = new GeoPoint3(0, 0, 0), Height = 5 };
            database.ModelSpace.AppendEntity(text);
            text.TextStyleName = "GH_ARIAL";
            text.Oblique = 15.0 * Math.PI / 180.0;
            CadDocument document = CadDocument.From(database);

            DisplayText2 shown = Assert.IsType<DisplayText2>(Assert.Single(document.GetDisplay(document.ModelSpace).Items));

            Assert.Equal("Arial", shown.FontName);
            Assert.Equal(15.0 * Math.PI / 180.0, shown.Oblique, 9);
        }

        [Fact]
        public void AnMTextOfAStyleOfAFontFileCarriesTheFileAndTheStylesSlant()
        {
            DisplayText2 shown = MText("ROMANS", (database, mtext) =>
            {
                var style = new DxfTextStyleTableRecord { Name = "GH_ROMANS", FileName = "romans.shx" };
                database.TextStyleTable.Add(style);
                style.ObliquingAngle = 10.0 * Math.PI / 180.0;
                mtext.TextStyleName = "GH_ROMANS";
            });

            Assert.Equal("romans.shx", shown.FontName);
            Assert.Equal(10.0 * Math.PI / 180.0, shown.Oblique, 9);
        }
    }
}
