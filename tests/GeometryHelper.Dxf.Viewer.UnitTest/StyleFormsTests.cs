using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The style forms of C10 with no window: -STYLE typed and STYLE ? listed, the text style editor's Set Current, Delete
    /// and Rename, TEXT taking the current style, and the dimension style editor's New, Modify, Override and Compare, each
    /// change one undo step and the dimensions drawn in a style drawn again.
    /// </summary>
    public sealed class StyleFormsTests
    {
        [Fact]
        public void DashStyleTyped_WritesTheStyleAsAutoCADWritesIt_MakesItCurrent_AndIsOneUndoStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            string before = document.Database.ToText();

            Run(session, "-STYLE", "GH_NOTES", "romans.shx", "0", "0.8", "15", "Y", "Y", "N");

            DxfTextStyleTableRecord style = document.Database.TextStyleTable["GH_NOTES"];
            Assert.Equal("romans.shx", style.FileName);
            Assert.Equal(0.0, style.TextSize);
            Assert.Equal(0.8, style.XScale, 12);
            Assert.Equal(15.0, style.ObliquingAngle * 180.0 / Math.PI, 9);
            Assert.Equal(6, style.FlagBits);
            Assert.False(style.IsVertical);
            Assert.Equal("GH_NOTES", document.Database.Textstyle);
            Assert.Contains("\"GH_NOTES\" is now the current text style.", session.History);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void DashStyleOfATrueTypeFamily_WritesItsFileAndTheFontsExtendedData()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "-STYLE", "GH_ARIAL", "Arial", "2.5", Enter, Enter, Enter, Enter);

            DxfTextStyleTableRecord style = document.Database.TextStyleTable["GH_ARIAL"];
            Assert.Equal(CadTrueTypeFonts.FileOf("Arial"), style.FileName);
            Assert.Equal("Arial", style.Font.TypeFace);
            Assert.Equal(2.5, style.TextSize);
            Assert.Contains(
                "1001\nACAD\n1000\nArial\n1071\n34\n",
                document.Database.ToText(),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void StyleQuestionMark_ListsEachStyleAsAutoCADDoes()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "-STYLE", "GH_NOTES", "romans.shx", "3.5", "1", "0", "Y", "N", "N");

            Run(session, "-STYLE", "?", Enter);

            Assert.Contains("Style name: \"GH_NOTES\"  Font files: romans.shx", session.History);
            Assert.Contains("   Height: 3.5000  Width factor: 1.0000  Obliquing angle: 0", session.History);
            Assert.Contains("   Generation: Backwards", session.History);
        }

        [Fact]
        public void TextTakesTheCurrentStyle_ItsFixedHeightUnasked_ItsWidthSlantAndMirroring()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "-STYLE", "GH_FIXED", "romans.shx", "5", "0.7", "10", "Y", "N", "N");

            Run(session, "DT", P(0, 0), "0", "BEAM B-12", Enter);

            DxfDBText text = Assert.Single(All<DxfDBText>(document.ModelSpace));
            Assert.Equal("GH_FIXED", text.TextStyleName);
            Assert.Equal(5.0, text.Height);
            Assert.Equal(0.7, text.WidthFactor, 12);
            Assert.Equal(10.0, text.Oblique * 180.0 / Math.PI, 9);
            Assert.True(text.IsMirroredInX);
            Assert.False(text.IsMirroredInY);
        }

        [Fact]
        public void AStyleInUseIsNotDeleted_SayingHowManyTexts_AndARenameTakesItsTextsWithIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "-STYLE", "GH_NOTES", "romans.shx", "0", "1", "0", "N", "N", "N");
            Run(session, "DT", P(0, 0), "2.5", "0", "A", "B", Enter);
            Assert.True(CadTextStyleEditor.SetCurrent(document, "Standard").Succeeded);

            CadEditResult delete = CadTextStyleEditor.Delete(document, "GH_NOTES");
            CadEditResult rename = CadTextStyleEditor.Rename(document, "GH_NOTES", "GH_LABELS");

            Assert.True(delete.Refused);
            Assert.Equal("Style \"GH_NOTES\" is in use by 2 texts, and cannot be deleted.", delete.Message);
            Assert.True(rename.Succeeded);
            Assert.All(All<DxfDBText>(document.ModelSpace), t => Assert.Equal("GH_LABELS", t.TextStyleName));
            Assert.True(CadTextStyleEditor.Delete(document, "Standard").Refused);
            AssertReopens(document);
        }

        [Fact]
        public void ADimensionStyleModified_RedrawsTheDimensionsDrawnInIt_InOneUndoStep()
        {
            CadDocument document = Blank();
            document.Database.DimStyleTable["Standard"].Dimtad = 1;
            CadCommandSession session = Session(document);
            string style = CadDimStyleEditor.Current(document.Database);
            Run(session, "DLI", P(0, 0), P(300, 0), P(150, -30));
            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            string handle = dimension.Handle;
            int solids = dimension.DimBlock.OfType<DxfSolid>().Count();
            string before = document.Database.ToText();
            SortedDictionary<string, object> values = CadDimStyleEditor.Read(document.Database.DimStyleTable[style]);
            values["Dimblk"] = "Architectural tick";
            values["Dimasz"] = 3.0;
            values["Dimdle"] = 1.25;

            CadEditResult result = CadDimStyleEditor.Apply(document, style, values);

            Assert.True(result.Succeeded, result.Message);
            DxfDimStyleTableRecord record = document.Database.DimStyleTable[style];
            Assert.Equal("_ArchTick", record.Dimblk.Name);
            Assert.Equal(3.0, record.Dimasz);
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal(2, dimension.DimBlock.OfType<DxfPolyline>().Count(p => p.ConstantWidth > 0.0));
            Assert.Equal(2, solids);
            DxfLine line = dimension
                .DimBlock.OfType<DxfLine>()
                .Single(l => Math.Abs(l.StartPoint.Y - l.EndPoint.Y) < 1E-9 && l.StartPoint.Y < -1.0);
            Assert.Equal(300.0 + 2.0 * 1.25 * Scale(record), Math.Abs(line.EndPoint.X - line.StartPoint.X), 9);
            Assert.Contains("_ArchTick", document.Database.ToText(), StringComparison.Ordinal);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void ANewDimensionStyleStartsWithAnothers_AndNewDimensionsTakeItOnceItIsCurrent()
        {
            CadDocument document = Blank();
            StandardCurrent(document.Database);
            CadCommandSession session = Session(document);
            string start = CadDimStyleEditor.Current(document.Database);

            Assert.True(
                CadDimStyleEditor
                    .Add(
                        document,
                        "GH_25",
                        start,
                        new Dictionary<string, object>
                        {
                            ["Dimdec"] = 1,
                            ["Dimclrd"] = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1),
                        }
                    )
                    .Succeeded
            );
            Assert.True(CadDimStyleEditor.Add(document, "gh_25", start, null).Refused);
            Assert.True(CadDimStyleEditor.SetCurrent(document, "GH_25").Succeeded);
            Run(session, "DLI", P(0, 0), P(300, 0), P(150, -30));

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.Equal("GH_25", dimension.DimensionStyleName);
            Assert.Equal("GH_25", document.Database.Dimstyle);
            Assert.Equal(1, document.Database.Dimdec);
            Assert.Contains(dimension.DimBlock.OfType<DxfLine>(), l => l.Color.ColorIndex == 1);
            Assert.Equal("300.0", dimension.DimBlock.OfType<DxfMText>().Single().Contents);
            Assert.Empty(
                CadDimStyleEditor.Compare(
                    CadDimStyleEditor.Read(document.Database.DimStyleTable["GH_25"]),
                    CadDimStyleEditor.Read(document.Database.DimStyleTable["GH_25"])
                )
            );
            Assert.Equal(
                new[] { "Dimclrd", "Dimdec" },
                CadDimStyleEditor
                    .Compare(
                        CadDimStyleEditor.Read(document.Database.DimStyleTable[start]),
                        CadDimStyleEditor.Read(document.Database.DimStyleTable["GH_25"])
                    )
                    .Select(d => d.Key)
            );
            Assert.True(CadDimStyleEditor.Delete(document, "GH_25").Refused);
            AssertReopens(document);
        }

        [Fact]
        public void AnOverrideIsWrittenIntoTheDimensionsSelected_TheStyleKept()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            string style = CadDimStyleEditor.Current(document.Database);
            double asz = document.Database.DimStyleTable[style].Dimasz;
            Run(session, "DLI", P(0, 0), P(300, 0), P(150, -30));
            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));

            Assert.True(
                CadDimStyleEditor
                    .Override(document, Array.Empty<string>(), new Dictionary<string, object> { ["Dimasz"] = 7.0 })
                    .Refused
            );
            CadEditResult result = CadDimStyleEditor.Override(
                document,
                new[] { dimension.Handle },
                new Dictionary<string, object> { ["Dimasz"] = 7.0 }
            );

            Assert.True(result.Succeeded, result.Message);
            dimension = Entity<DxfRotatedDimension>(document, dimension.Handle);
            Assert.Equal(7.0, dimension.Dimasz);
            Assert.Equal(asz, document.Database.DimStyleTable[style].Dimasz);
            Assert.Contains("DSTYLE", document.Database.ToText(), StringComparison.Ordinal);
            AssertReopens(document);
        }

        [Fact]
        public void TheSamplesOfThePreview_AreDimensionsDrawnByTheCommandsBuilders()
        {
            CadDocument document = Blank();
            SortedDictionary<string, object> values = CadDimStyleEditor.Read(
                document.Database.DimStyleTable[CadDimStyleEditor.Current(document.Database)]
            );
            values["Dimblk"] = "Dot";

            DxfDatabase samples = CadDimStyleEditor.Samples(document.Database, values);

            Assert.Single(samples.ModelSpace.OfType<DxfRotatedDimension>());
            Assert.Single(samples.ModelSpace.OfType<DxfRadialDimension>());
            Assert.Single(samples.ModelSpace.OfType<DxfPoint3AngularDimension>());
            Assert.True(samples.BlockTable.Has("_DOT"));
            Assert.All(samples.ModelSpace.OfType<DxfDimension>(), d => Assert.NotEmpty(d.DimBlock.OfType<DxfMText>()));
        }

        [Fact]
        public void StAndDAreTheAliasesOfStyleAndDimstyle_AndWithNoWindowDimstyleSaysSo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Assert.Equal("STYLE", session.Registry.Find("ST").Name);
            Assert.Equal("DIMSTYLE", session.Registry.Find("D").Name);
            Assert.Equal("DIMSTYLE", session.Registry.Find("DDIM").Name);
            Run(session, "D");

            Assert.Contains(session.History, l =>
                l.EndsWith("The Dimension Style Manager is shown by the window.", StringComparison.Ordinal)
            );
        }

        private static double Scale(DxfDimStyleTableRecord record) => record.Dimscale > 0.0 ? record.Dimscale : 1.0;
    }
}
