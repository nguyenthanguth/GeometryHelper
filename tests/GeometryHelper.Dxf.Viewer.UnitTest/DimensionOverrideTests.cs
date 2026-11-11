using System;
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
            // AutoCAD 2021 writes DIMTMOVE 1 first on a continued one, DIMUPT 1 on a radius and a diameter, and DIMJOGANG on
            // a jogged radius whatever the style has.
            Assert.Equal(
                new[]
                {
                    "41 176",
                    "279 41 176",
                    "41 176",
                    "41 176",
                    "41 176 288",
                    "41 176 288",
                    "41 50 176",
                    "41 176",
                    "41 176",
                },
                made.Select(Groups)
            );
            Assert.All(made, d => Assert.Equal(4.0, d.Dimasz));
        }

        [Fact]
        public void AShortDimensionDimcontinueAdds_IsDrawnWithDimtmove1_ItsWordsOverTheLinePastALeader_AsAutoCad()
        {
            // AutoCAD 2021, acad.dwt's Standard current: DIMLINEAR (0, 0) to (100, 0) at y 20, DIMCONTINUE to (100.5, 0):
            // 279 1 written, the words "0.5000" over the line, past a leader from the middle of the line (100.25, 20). (Where
            // AutoCAD runs the leader up to y 20.4088 and on to x 100.43 and centres the words at (100.8933, 20.4088), the
            // presenter's picture of DIMTMOVE 1 runs it to (100.372, 20.405) and centres them at (101.02, 20.405).)
            CadDocument document = Blank();
            StandardCurrent(document.Database);
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));

            Run(session, "DCO", P(100.5, 0), Enter, Enter);

            DxfRotatedDimension dimension = All<DxfRotatedDimension>(document.ModelSpace).Last();
            Assert.Equal("279", Groups(dimension));
            Assert.Equal(1, dimension.Dimtmove);
            Assert.Contains(dimension.DimBlock.OfType<DxfLine>(), l =>
                Math.Abs(l.StartPoint.X - 100.25) < 1E-9 && Math.Abs(l.StartPoint.Y - 20) < 1E-9 && l.EndPoint.Y > 20.4
            );
            Assert.True(dimension.DimBlock.OfType<DxfMText>().Single().Location.Y > 20.4);
        }

        [Fact]
        public void ABlankDrawing_DrawsANewDimensionByItsHeader_InIso25_WhichItHolds()
        {
            // A blank DxfDatabase is acadiso.dwt's in its dimensions: ISO-25 current, the header holding its values.
            var document = CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf");
            document.Database.Dimasz = 5.0;
            CadCommandSession session = Session(document);

            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.Equal("ISO-25", dimension.DimensionStyleName);
            Assert.Equal("41", Groups(dimension));
            Assert.Equal(5.0, dimension.Dimasz);
            Assert.Equal(2.5, dimension.DimBlock.OfType<DxfMText>().Single().TextHeight, 9);
            Assert.EndsWith("100", dimension.DimBlock.OfType<DxfMText>().Single().Contents);
        }

        [Fact]
        public void ADrawingWhoseDimstyleNamesNoStyleOfIt_DrawsByTheHeaderAllTheSame_InStandard()
        {
            // $DIMSTYLE naming a style the drawing has not: the style fallen back on is Standard, the header drawn by and each
            // variable other than Standard's written, as where it names one.
            var db = new DxfDatabase("AC1027");
            DxfDimStyleTableRecord standard = db.DimStyleTable["Standard"];
            standard.Dimasz = 3.0;
            standard.Dimgap = 1.0;
            db.SetDimstyleData(standard);
            db.Dimstyle = "GONE";
            db.Dimasz = 5.0;
            var document = CadDocument.From(db, "gone.dxf");
            CadCommandSession session = Session(document);

            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.Equal("Standard", dimension.DimensionStyleName);
            Assert.Equal("41", Groups(dimension));
            DxfSolid arrow = dimension.DimBlock.OfType<DxfSolid>().First();
            double[] xs = Enumerable.Range(0, 4).Select(i => arrow.GetPointAt((short)i).X).ToArray();
            Assert.Equal(5.0, xs.Max() - xs.Min(), 9);

            // A leader alike: compared with Standard, whose DIMASZ the header holds, it writes no override.
            Run(session, "LEADER", P(0, 50), P(10, 60), P(20, 60), Enter, "Hi", Enter);
            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal("Standard", leader.DimensionStyleName);
            Assert.Equal(5.0, leader.Dimasz);
            Assert.Contains(leader.GetXDataForApplication("ACAD") ?? new List<DxfTypedValue>(), v =>
                v.TypeCode == 1070 && v.Value.Trim() == "41"
            );
            db.Dimasz = 3.0;
            Run(session, "LEADER", P(0, 80), P(10, 90), P(20, 90), Enter, "Ho", Enter);
            DxfLeader plain = All<DxfLeader>(document.ModelSpace).Last();
            Assert.Equal(3.0, plain.Dimasz);
            Assert.Null(plain.GetXDataForApplication("ACAD"));

            // QLEADER writes DIMGAP and DIMTAD first and moves neither, Standard holding the header's DIMGAP of 1, where
            // compared with AutoCAD's own 0.09 it moved DIMGAP after DIMTAD.
            Run(session, "QLEADER", P(0, 120), P(10, 130), P(20, 130), "0", "Q", Enter);
            DxfLeader quick = All<DxfLeader>(document.ModelSpace).Last();
            Assert.Equal(
                new[] { "147", "77" },
                quick.GetXDataForApplication("ACAD").Where(v => v.TypeCode == 1070).Select(v => v.Value.Trim()).Take(2)
            );
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
            Assert.Equal(
                new[] { "40", "40 279", "40", "40", "40 288", "40 288", "40 50", "40", "40" },
                made.Select(Groups)
            );
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
