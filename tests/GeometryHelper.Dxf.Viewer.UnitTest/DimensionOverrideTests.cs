using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// New dimensions drawn by the current dimension variables and writing each other than their style's as an override, in
    /// the order of the groups, and DIMSCALE nought taken through a viewport, as AutoCAD 2021 does (probed through
    /// accoreconsole: DIMSCALE 2, DIMASZ, DIMEXO, DIMJOGANG, DIMTAD, DIMTXT, DIMGAP, DIMCLRD, DIMCLRT, DIMDEC, DIMBLK and DIMLWD
    /// set out of order gave every kind <c>40 41 42 50 77 140 147 176 178 271 342 371</c>; DIMSCALE 0 through a viewport of
    /// 1:2 gave <c>40 2</c> and words 2 times DIMTXT high).
    /// </summary>
    public class DimensionOverrideTests
    {
        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");

        /// <summary>The groups the dimension's overrides name, in the order they are written.</summary>
        private static string Groups(DxfDimension dimension)
        {
            IReadOnlyList<DxfTypedValue> data = dimension.GetXDataForApplication("ACAD");

            if (data == null)
            {
                return string.Empty;
            }

            int open = data.ToList().FindIndex(v => v.TypeCode == 1002 && v.Value == "{");
            var groups = new List<string>();

            for (int i = open + 1; i < data.Count && data[i].TypeCode == 1070; i += 2)
            {
                groups.Add(data[i].Value.Trim());
            }

            return string.Join(" ", groups);
        }

        [Fact]
        public void ATemplateDrawingWithNothingTyped_GivesANewDimensionNoOverride()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);

            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.Equal(string.Empty, Groups(dimension));
            Assert.Equal(2.5, dimension.DimBlock.OfType<DxfMText>().Single().TextHeight, 9);
        }

        [Fact]
        public void VariablesOtherThanTheStyles_AreWrittenInTheOrderOfTheirGroups_AndDrawTheDimension_AsAutoCad()
        {
            CadDocument document = Metric();
            DxfDatabase db = document.Database;

            // As a drawing AutoCAD saved after they were typed, out of the order of their groups.
            db.Dimlwd = DxfLineWeight.LineWeight030;
            db.Dimclrt = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            db.Dimdec = 1;
            db.Dimgap = 1.0;
            db.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            db.Dimtad = 0;
            db.Dimexo = 1.0;
            db.Dimasz = 5.0;
            CadCommandSession session = Session(document);
            Run(session, "DIMSCALE", "2");

            Run(session, "DLI", P(0, 0), P(100.34, 0), P(50, 20));

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.Equal("40 41 42 77 147 176 178 271 371", Groups(dimension));
            Assert.Equal(2.0, dimension.Dimscale);
            Assert.Equal(5.0, dimension.Dimasz);
            Assert.Equal(1, dimension.Dimdec);
            Assert.Equal(DxfLineWeight.LineWeight030, dimension.Dimlwd);
            Assert.Equal(2.5, document.Database.DimStyleTable["ISO-25"].Dimasz);

            // Drawn by them: arrowheads 5 times 2 long, the line red, the words green, one decimal place.
            DxfSolid arrow = dimension.DimBlock.OfType<DxfSolid>().First();
            double[] xs = Enumerable.Range(0, 4).Select(i => arrow.GetPointAt((short)i).X).ToArray();
            Assert.Equal(10.0, xs.Max() - xs.Min(), 9);
            DxfMText words = dimension.DimBlock.OfType<DxfMText>().Single();
            Assert.Equal(3, words.Color.ColorIndex);
            Assert.EndsWith("100,3", words.Contents);
            Assert.Contains(dimension.DimBlock.OfType<DxfLine>(), l => l.Color.ColorIndex == 1);
            AssertReopens(document);
        }

        [Fact]
        public void EveryKind_WritesTheVariablesOtherThanTheStyles()
        {
            CadDocument document = Metric();
            document.Database.Dimasz = 4.0;
            document.Database.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            CadCommandSession session = Session(document);
            Run(session, "C", P(150, 150), "20");
            Run(session, "C", P(250, 150), "20");
            Run(session, "C", P(350, 150), "20");
            Run(session, "ARC", "C", P(150, 50), P(170, 50), P(130, 50));
            DxfCircle[] circles = All<DxfCircle>(document.ModelSpace).ToArray();
            DxfArc arc = Assert.Single(All<DxfArc>(document.ModelSpace));

            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));
            Run(session, "DCO", P(120, 0), Enter, Enter);
            Run(session, "DAL", P(0, 50), P(100, 100), P(40, 90));
            Run(session, "DAN", Enter, P(0, 150), P(60, 150), P(40, 190), P(50, 170));
            Run(session, "DRA", new[] { circles[0].Handle }, P(190, 160));
            Run(session, "DDI", new[] { circles[1].Handle }, P(290, 160));
            Run(session, "DIMJOGGED", new[] { circles[2].Handle }, P(390, 190), P(385, 165), P(380, 175));
            Run(session, "DIMARC", new[] { arc.Handle }, P(150, 90));
            Run(session, "DOR", P(10, 10), P(10, -30));

            List<DxfDimension> made = All<DxfDimension>(document.ModelSpace);
            Assert.Equal(9, made.Count);
            Assert.All(made, d => Assert.Equal("41 176", Groups(d)));
            Assert.All(made, d => Assert.Equal(4.0, d.Dimasz));
        }

        [Fact]
        public void DimscaleNought_ThroughAViewportOf1To2_DrawsEveryKindTwiceAsLarge_AndWritesScale2_AsAutoCad()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            Run(session, "REC", P(0, 0), P(400, 200));
            Run(session, "C", P(150, 150), "20");
            Run(session, "C", P(250, 150), "20");
            Run(session, "C", P(350, 150), "20");
            Run(session, "ARC", "C", P(150, 50), P(170, 50), P(130, 50));
            DxfCircle[] circles = All<DxfCircle>(document.ModelSpace).ToArray();
            DxfArc arc = Assert.Single(All<DxfArc>(document.ModelSpace));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            Run(session, "MS");
            Assert.NotNull(document.ActiveViewport);
            Assert.Equal(0.5, document.ActiveViewport.CustomScale, 9);
            Run(session, "DIMSCALE", "0");

            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));
            Run(session, "DBA", P(110, 0), Enter, Enter);
            Run(session, "DAL", P(0, 50), P(100, 100), P(40, 90));
            Run(session, "DAN", Enter, P(0, 150), P(60, 150), P(40, 190), P(50, 170));
            Run(session, "DRA", new[] { circles[0].Handle }, P(190, 160));
            Run(session, "DDI", new[] { circles[1].Handle }, P(290, 160));
            Run(session, "DIMJOGGED", new[] { circles[2].Handle }, P(390, 190), P(385, 165), P(380, 175));
            Run(session, "DIMARC", new[] { arc.Handle }, P(150, 90));
            Run(session, "DOR", P(10, 10), P(10, -30));

            List<DxfDimension> made = All<DxfDimension>(document.ModelSpace);
            Assert.Equal(9, made.Count);
            Assert.All(made, d => Assert.Equal("40", Groups(d)));
            Assert.All(made, d => Assert.Equal(2.0, d.Dimscale, 9));
            Assert.All(made, d => Assert.Equal(5.0, d.DimBlock.OfType<DxfMText>().First().TextHeight, 9));
            Assert.Equal(0.0, document.Database.Dimscale);

            // Nought in model space itself draws at 1, the style's, and writes nothing.
            Run(session, "PS");
            document.CurrentSpace = document.Spaces[0];
            Run(session, "DLI", P(0, -50), P(100, -50), P(50, -30));
            DxfDimension plain = All<DxfDimension>(document.ModelSpace).Last();
            Assert.Equal(string.Empty, Groups(plain));
            Assert.Equal(2.5, plain.DimBlock.OfType<DxfMText>().First().TextHeight, 9);
        }
    }
}
