using System;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The words of a dimension measured in the fonts of the document the dimension is in, and in no other's: two drawings
    /// open at once, one with test.shx in its font folder and one with none, each measure "IOI" 4 high in test.shx as their
    /// own fonts say (5.2 wide for the one, not measured for the other), whichever had its fonts set last and whichever
    /// measured first.
    /// </summary>
    public sealed class DimensionWordsOfEachDocumentTests : IDisposable
    {
        private readonly FontFolder _with = new FontFolder();
        private readonly FontFolder _without = new FontFolder();

        public void Dispose()
        {
            _with.Dispose();
            _without.Dispose();
        }

        /// <summary>A drawing whose Standard text style is test.shx, its fonts looked for in a folder of its own.</summary>
        private CadDocument Document(FontFolder folder)
        {
            CadDocument document = Blank();
            document.Database.TextStyleTable["Standard"].FileName = "test.shx";
            document.Fonts = new CadFonts(new[] { folder.FullPath }, null, null);
            return document;
        }

        private static bool Measure(CadDocument document, out double width)
        {
            bool measured = CadDimensionWords.TryMeasure(
                document.Database.TextStyleTable["Standard"],
                "IOI",
                4.0,
                out width,
                out _
            );
            return measured;
        }

        [Fact]
        public void ADocumentWhoseFontsWereSetFirst_IsStillMeasuredInItsOwnFonts()
        {
            _with.Write("test.shx", ShxFiles.Unifont());
            CadDocument has = Document(_with);
            CadDocument hasNot = Document(_without);

            Assert.True(Measure(has, out double width));
            Assert.Equal(5.2, width, 6);
            Assert.False(Measure(hasNot, out _));
        }

        [Fact]
        public void ADocumentWhoseFontsWereSetLast_IsStillMeasuredInItsOwnFonts()
        {
            _with.Write("test.shx", ShxFiles.Unifont());
            CadDocument hasNot = Document(_without);
            CadDocument has = Document(_with);

            Assert.False(Measure(hasNot, out _));
            Assert.True(Measure(has, out double width));
            Assert.Equal(5.2, width, 6);
        }

        [Fact]
        public void WordsMeasuredInOneDocument_AreNotKeptForAnotherOfTheSameStyleAndWords()
        {
            _with.Write("test.shx", ShxFiles.Unifont());
            CadDocument has = Document(_with);
            CadDocument hasNot = Document(_without);

            Assert.True(Measure(has, out _));
            Assert.False(Measure(hasNot, out _));
            Assert.True(Measure(has, out double width));
            Assert.Equal(5.2, width, 6);
        }
    }
}
