using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>The defaults a host gives the text and dimension commands through the values the session remembers.</summary>
    public class EditingTextDefaultsTests
    {
        [Fact]
        public void TextAndMTextWriteTheWidthFactorTheHostGives()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Remembered["text.widthFactor"] = 0.8;

            Run(session, "DT", P(0, 0), "2.5", "0", "BEAM B-12", Enter);
            Run(session, "T", P(0, 40), P(50, 30), "PLATE");

            DxfDBText text = Assert.Single(All<DxfDBText>(document.ModelSpace));
            Assert.Equal(0.8, text.WidthFactor, 9);
            DxfMText mtext = Assert.Single(All<DxfMText>(document.ModelSpace));
            Assert.Equal(@"\W0.8000;PLATE", mtext.Contents);
            Assert.Equal("PLATE", mtext.Text);
            AssertReopens(document);
        }

        [Fact]
        public void WithNoWidthFactorGivenTextAndMTextKeepTheirOwnWidth()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "DT", P(0, 0), "2.5", "0", "BEAM B-12", Enter);
            Run(session, "T", P(0, 40), P(50, 30), "PLATE");

            Assert.Equal(1.0, Assert.Single(All<DxfDBText>(document.ModelSpace)).WidthFactor, 9);
            Assert.Equal("PLATE", Assert.Single(All<DxfMText>(document.ModelSpace)).Contents);
        }

        [Fact]
        public void DimLinearWritesItsWordsAsHighAsTheHostSaysAndKeepsTheHeightAsAnOverride()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            double before = style.Dimtxt;
            session.Remembered["dim.text.height"] = 3.5;

            Run(session, "DLI", P(0, 0), P(300, 0), P(150, -30));

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            DxfMText words = dimension.DimBlock.OfType<DxfMText>().Single();
            double scale = style.Dimscale > 0.0 ? style.Dimscale : 1.0;
            Assert.Equal(3.5 * scale, words.TextHeight, 9);
            Assert.Equal(3.5, dimension.Dimtxt, 9);
            Assert.Equal(before, style.Dimtxt, 9);
            Assert.NotEqual(3.5, before);
            AssertReopens(document);
        }

        [Fact]
        public void WithNoHeightGivenADimensionTakesItsStylesAndHasNoOverride()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            DxfDimStyleTableRecord style = document.Database.DimStyleTable[document.Database.Dimstyle];

            Run(session, "DLI", P(0, 0), P(300, 0), P(150, -30));

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            double scale = style.Dimscale > 0.0 ? style.Dimscale : 1.0;
            Assert.Equal(style.Dimtxt * scale, dimension.DimBlock.OfType<DxfMText>().Single().TextHeight, 9);
            Assert.DoesNotContain("DSTYLE", document.Database.ToText());
        }
    }
}
