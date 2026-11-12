using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// EXPLODE of an MTEXT keeps the colours and SHX fonts of its runs, as AutoCAD 2021's Explode makes them (probed: an
    /// MTEXT of simplex in blue, "AB{\Ftxt.shx;CD}EF" gives CD in a style MtXpl_txt_shx of txt.shx; \C1 red, \C256 ByLayer,
    /// \C0 ByBlock, \c16711680 the true colour 0,0,255; a run of Arial stays in the MTEXT's style and colour).
    /// </summary>
    public sealed class MTextExplodeLookTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void ExplodedRunsOfTheirOwnColoursAndShxFonts_AreTextsOfThoseColoursAndOfAStyleOfThatFont()
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            _folder.Write("other.shx", ShxFiles.Unifont());
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
            var mtext = new DxfMText
            {
                Contents =
                    @"AB {\Fother.shx;CD} {\C1;EF} {\C256;GH} {\C0;IJ} {\c16711680;KL} {\fArial|b0|i0|c0|p34;MN} OP",
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 10.0,
            };
            database.ModelSpace.AppendEntity(mtext);
            mtext.TextStyleName = "GH_SHX";
            mtext.ColorIndex = 5;
            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null, f => f == "Arial");

            Run(Session(document), "EXPLODE", new[] { mtext.Handle }, Enter);

            Dictionary<string, DxfDBText> texts = All<DxfDBText>(document.ModelSpace)
                .Where(t => t.TextString.Trim().Length > 0)
                .ToDictionary(t => t.TextString.Trim(), t => t);
            Assert.Equal(new[] { "AB", "CD", "EF", "GH", "IJ", "KL", "MN", "OP" }, texts.Keys.OrderBy(k => k));

            // The SHX font of its own: a style of it, which the drawing now has.
            Assert.Equal("MtXpl_other_shx", texts["CD"].TextStyleName);
            Assert.Equal("other.shx", database.TextStyleTable["MtXpl_other_shx"].FileName);

            // The rest in the MTEXT's style, Arial's run too.
            Assert.All(texts.Where(t => t.Key != "CD"), t => Assert.Equal("GH_SHX", t.Value.TextStyleName));

            // Colours of their own; the rest the MTEXT's.
            Assert.Equal(1, texts["EF"].Color.ColorIndex);
            Assert.Equal(DxfColorMethod.ByLayer, texts["GH"].Color.ColorMethod);
            Assert.Equal(DxfColorMethod.ByBlock, texts["IJ"].Color.ColorMethod);
            DxfColor blue = texts["KL"].Color;
            Assert.Equal(DxfColorMethod.ByColor, blue.ColorMethod);
            Assert.Equal(new byte[] { 0, 0, 255 }, new[] { blue.Red, blue.Green, blue.Blue });
            Assert.All(new[] { "AB", "CD", "MN", "OP" }, k => Assert.Equal(5, texts[k].Color.ColorIndex));

            // One undo step takes the TEXTs and the style back.
            Assert.True(document.Undo());
            Assert.Single(All<DxfMText>(document.ModelSpace));
            Assert.False(document.Database.TextStyleTable.Has("MtXpl_other_shx"));
        }
    }
}
