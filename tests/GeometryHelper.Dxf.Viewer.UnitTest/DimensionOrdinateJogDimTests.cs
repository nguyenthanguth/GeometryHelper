using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>DIMORDINATE (and its rows of DIMCONTINUE, DIMBASELINE and QDIM), DIMJOGLINE and DIM, run in a session with no window.</summary>
    public class DimensionOrdinateJogDimTests
    {
        private sealed class MoveOnlyOwner : ICadEntityOwner
        {
            public string Name => "Test.Model";

            public CadEditRule GetEditRule(DxfEntity entity) => CadEditRule.MoveOnly;

            public bool TryApply(CadDocument document, CadEdit edit) => false;
        }

        private static List<DxfLine> PictureLines(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased && l.Layer != "Defpoints").ToList();

        private static double Scale(DxfDimStyleTableRecord style) => style.Dimscale > 0.0 ? style.Dimscale : 1.0;

        [Fact]
        public void DimOrdinate_MeasuresAlongXWhereTheLeaderRunsUpOrDown_AlongYWhereItRunsAside()
        {
            CadDocument document = Blank();
            StandardCurrent(document.Database);
            CadCommandSession session = Session(document);
            string before = document.Database.ToText();

            Run(session, "DOR");
            Assert.Equal("Specify feature location:", session.PromptText);
            Run(session, P(300, 40));
            Assert.Equal("Specify leader endpoint or [Xdatum/Ydatum/Mtext/Text/Angle]:", session.PromptText);
            Run(session, P(310, 80));
            Assert.StartsWith("Dimension text = 300", session.History.Last(), StringComparison.Ordinal);
            Run(session, "DIMORDINATE", P(300, 40), P(360, 45));
            Assert.StartsWith("Dimension text = 40", session.History.Last(), StringComparison.Ordinal);

            List<DxfOrdinateDimension> dimensions = All<DxfOrdinateDimension>(document.ModelSpace);
            Assert.Equal(2, dimensions.Count);
            Assert.True(dimensions[0].UsingXAxis);
            Assert.True(dimensions[1].UsingYAxis);
            Assert.Equal(300, dimensions[0].Measurement, 9);
            Assert.Equal(40, dimensions[1].Measurement, 9);
            Near(new GeoPoint3(0, 0, 0), dimensions[0].Origin);
            Near(new GeoPoint3(300, 40, 0), dimensions[0].DefiningPoint);
            Near(new GeoPoint3(310, 80, 0), dimensions[0].LeaderEndPoint);
            Assert.True(dimensions[0].DimBlock.IsAnonymous);

            // The leader: up from the point, jogged across, then two arrowheads straight up to its end; the words read upward.
            DxfDimStyleTableRecord style = dimensions[0].GetDimstyleData();
            double leg = 2.0 * style.Dimasz * Scale(style);
            List<DxfLine> leader = PictureLines(dimensions[0]);
            Assert.Equal(3, leader.Count);
            Near(new GeoPoint3(300, 40 + style.Dimexo * Scale(style), 0), leader[0].StartPoint);
            Near(new GeoPoint3(310, 80 - leg, 0), leader[1].EndPoint);
            Near(new GeoPoint3(310, 80, 0), leader[2].EndPoint);
            Assert.Equal(Math.PI / 2.0, dimensions[0].DimBlock.OfType<DxfMText>().Single().Rotation, 9);
            Assert.Equal(0.0, dimensions[1].DimBlock.OfType<DxfMText>().Single().Rotation, 12);
            Assert.True(dimensions[0].DimBlock.OfType<DxfMText>().Single().Location.Y > 80);
            Assert.Equal("DIMORDINATE", document.NextUndo.Name);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void DimOrdinate_XdatumTextAndAngle_AndTheOriginOfTheUcs()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.Ucs.SetCurrent(CadUcs.InPlane(P(100, 50), 0.0));

            Run(session, "DOR", P(300, 40), "X", "T", "<> TYP", "A", "30", P(350, 45));

            DxfOrdinateDimension dimension = Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace));
            Assert.True(dimension.UsingXAxis);
            Assert.Equal(200, dimension.Measurement, 9);
            Near(new GeoPoint3(100, 50, 0), dimension.Origin);
            Assert.Equal("<> TYP", dimension.DimensionText);
            Assert.Equal(Math.PI / 6.0, dimension.TextRotation, 9);
            Assert.StartsWith("200", dimension.DimBlock.OfType<DxfMText>().Single().Contents, StringComparison.Ordinal);
            Assert.EndsWith(" TYP", dimension.DimBlock.OfType<DxfMText>().Single().Contents, StringComparison.Ordinal);
            AssertReopens(document);
        }

        [Fact]
        public void DimOrdinate_InAUcsTurned_MeasuresAlongItsAxes_AndIsDrawnAgainTheSame()
        {
            CadDocument document = Blank();
            StandardCurrent(document.Database);
            CadCommandSession session = Session(document);
            document.Ucs.SetCurrent(CadUcs.InPlane(P(0, 0), Math.PI / 2.0));

            // In a UCS turned a quarter turn its X runs up the world's Y: (0, 70) stands 70 along it.
            Run(session, "DOR", P(0, 70), P(-30, 72));

            DxfOrdinateDimension dimension = Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace));
            Assert.True(dimension.UsingXAxis);
            Assert.Equal(70, dimension.Measurement, 9);
            List<DxfLine> made = PictureLines(dimension);

            // DIMEDIT Home draws it again from its record, its turn read from group 51.
            Run(session, "DIMEDIT", Enter, new[] { dimension.Handle }, Enter);
            List<DxfLine> again = PictureLines(Entity<DxfOrdinateDimension>(document, dimension.Handle));
            Assert.Equal(made.Count, again.Count);

            for (int i = 0; i < made.Count; i++)
            {
                Near(made[i].StartPoint, again[i].StartPoint);
                Near(made[i].EndPoint, again[i].EndPoint);
            }

            Assert.Equal(-30, made[2].EndPoint.X, 9);
            Assert.Equal(72, made[2].EndPoint.Y, 9);
        }

        [Fact]
        public void AnOrdinateLeaderInLineWithItsPoint_IsOneLine_AndWithDimtad1RunsOnUnderTheWords()
        {
            CadDocument document = Blank();
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            style.Dimtad = 1;
            StandardCurrent(document.Database);
            CadCommandSession session = Session(document);

            Run(session, "DOR", P(600, 0), P(600, -30));

            DxfOrdinateDimension dimension = Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace));
            DxfLine leader = Assert.Single(PictureLines(dimension));
            DxfMText words = dimension.DimBlock.OfType<DxfMText>().Single();
            double scale = Scale(style);
            double gap = style.Dimgap * scale;
            double lift = gap + dimension.Dimtxt * scale / 2.0;

            // The leader runs down past its end by DIMGAP and the words, which stand to its left reading upward.
            Assert.Equal(600, leader.EndPoint.X, 9);
            Assert.True(leader.EndPoint.Y < -30 - gap);
            Assert.Equal(600 - lift, words.Location.X, 9);
            Assert.Equal(-30 - gap - (-30 - gap - leader.EndPoint.Y) / 2.0, words.Location.Y, 9);
            Assert.Equal(Math.PI / 2.0, words.Rotation, 9);
            Assert.Contains(dimension.DimBlock.OfType<DxfDBPoint>(), p =>
                Math.Abs(p.Position.X) < 1E-9 && Math.Abs(p.Position.Y) < 1E-9
            );
        }

        [Fact]
        public void TheWordsOfAnOrdinateInAUcsTurned30Degrees_ReadAt120Degrees_AsAutoCadTurnsThem()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.Ucs.SetCurrent(CadUcs.InPlane(P(800, 0), Math.PI / 6.0));

            Run(session, "DOR", P(850, 60), P(830, 120));

            DxfOrdinateDimension dimension = Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace));
            Assert.True(dimension.UsingXAxis);
            Assert.Equal(2.0 * Math.PI / 3.0, dimension.DimBlock.OfType<DxfMText>().Single().Rotation, 9);
            Assert.Equal(3, PictureLines(dimension).Count);
        }

        [Fact]
        public void DimContinueAndDimBaseline_OfAnOrdinate_GoOnFromItsOrigin_TheLeadersEndingLevel()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DOR", P(100, 0), P(100, -40));

            Run(session, "DCO");
            Assert.Equal("Specify feature location or [Select/Undo] <Select>:", session.PromptText);
            Run(session, P(250, 10), P(400, 20), "U", Enter, Enter);
            Run(session, "DBA", P(320, 5), Enter, Enter);

            List<DxfOrdinateDimension> dimensions = All<DxfOrdinateDimension>(document.ModelSpace);
            Assert.Equal(new[] { 100.0, 250.0, 320.0 }, dimensions.Select(d => Math.Round(d.Measurement, 9)));
            Assert.All(dimensions, d => Assert.True(d.UsingXAxis));
            Assert.All(dimensions, d => Assert.Equal(-40, d.LeaderEndPoint.Y, 9));
            Near(new GeoPoint3(250, -40, 0), dimensions[1].LeaderEndPoint);
            Assert.Equal("DIMBASELINE", document.NextUndo.Name);
            AssertReopens(document);
        }

        [Fact]
        public void QDim_Ordinate_DimensionsEachPointFromTheOrigin_TheLeadersEndingOnTheLine()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "L", P(10, 0), P(100, 0), P(100, 50), Enter);
            string[] lines = All<DxfLine>(document.ModelSpace).Select(l => l.Handle).ToArray();

            Run(session, "QDIM", lines, Enter, "O", P(50, -30));

            List<DxfOrdinateDimension> along = All<DxfOrdinateDimension>(document.ModelSpace);
            Assert.Equal(new[] { 10.0, 100.0 }, along.Select(d => Math.Round(d.Measurement, 9)));
            Assert.All(along, d => Assert.True(d.UsingXAxis));
            Assert.All(along, d => Assert.Equal(-30, d.LeaderEndPoint.Y, 9));
            Assert.Equal("QDIM", document.NextUndo.Name);

            Run(session, "QDIM", lines, Enter, "O", "P", P(0, 20), P(200, 25));
            List<DxfOrdinateDimension> upright = All<DxfOrdinateDimension>(document.ModelSpace).Skip(2).ToList();
            Assert.Equal(new[] { 20.0, 30.0 }, upright.Select(d => Math.Round(d.Measurement, 9)));
            Assert.All(upright, d => Assert.True(d.UsingYAxis));
            Assert.All(upright, d => Assert.Equal(200, d.LeaderEndPoint.X, 9));
        }

        [Fact]
        public void DimJogLine_StoresTheJogAsAutoCadDoes_DrawsIt_AndRemoveTakesItOut()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            string handle = dimension.Handle;
            int lines = PictureLines(dimension).Count;
            string start = document.Database.ToText();

            Run(session, "DJL");
            Assert.Equal("Select dimension to add jog or [Remove]:", session.PromptText);
            Run(session, (object)new[] { handle });
            Assert.Equal("Specify jog location (or press ENTER):", session.PromptText);
            Run(session, P(20, -28));

            dimension = Entity<DxfRotatedDimension>(document, handle);
            IReadOnlyList<DxfTypedValue> jog = dimension.GetXDataForApplication("ACAD_DSTYLE_DIMJAG_POSITION");
            Assert.Equal(new[] { 1001, 1070, 1070, 1070, 1010, 1020, 1030 }, jog.Select(g => (int)g.TypeCode));
            Assert.Equal(new[] { "387", "3", "389" }, jog.Skip(1).Take(3).Select(g => g.Value.Trim()));
            Assert.Equal(20, double.Parse(jog[4].Value, CultureInfo.InvariantCulture), 9);
            Assert.Equal(-30, double.Parse(jog[5].Value, CultureInfo.InvariantCulture), 9);
            Assert.Equal("DIMJOGLINE", document.NextUndo.Name);

            // The line is broken a jog high about (20, -30), the jog's three strokes put in the gap.
            DxfDimStyleTableRecord style = dimension.GetDimstyleData();
            double height = 1.5 * dimension.Dimtxt * Scale(style);
            List<DxfLine> picture = PictureLines(dimension);
            Assert.Equal(lines + 4, picture.Count);
            Assert.Contains(picture, l =>
                Math.Abs(l.EndPoint.X - (20 - height / 2)) < 1E-9 && Math.Abs(l.EndPoint.Y + 30) < 1E-9
            );
            Assert.Contains(picture, l =>
                Math.Abs(l.EndPoint.X - (20 - height / 6)) < 1E-9 && Math.Abs(l.EndPoint.Y - (-30 + height / 2)) < 1E-9
            );
            Assert.Contains(picture, l =>
                Math.Abs(l.EndPoint.X - (20 + height / 6)) < 1E-9 && Math.Abs(l.EndPoint.Y - (-30 - height / 2)) < 1E-9
            );
            Assert.Contains("ACAD_DSTYLE_DIMJAG_POSITION", document.Database.ToText(), StringComparison.Ordinal);
            AssertReopens(document);

            Run(session, "DIMJOGLINE", "R");
            Assert.Equal("Select jog to remove:", session.PromptText);
            Run(session, (object)new[] { handle });
            Assert.Null(
                Entity<DxfRotatedDimension>(document, handle).GetXDataForApplication("ACAD_DSTYLE_DIMJAG_POSITION")
            );
            Assert.Equal(lines, PictureLines(Entity<DxfRotatedDimension>(document, handle)).Count);

            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void DimJogLine_EnterPutsTheJogBetweenTheFirstExtensionLineAndTheWords_AndOtherKindsAndTheModelAreRefused()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            Run(session, "C", P(300, 0), "20");
            Run(session, "DRA", P(320, 0), P(340, 10));
            List<DxfDimension> dimensions = All<DxfDimension>(document.ModelSpace);

            Run(session, "DJL", new[] { dimensions[0].Handle }, Enter);
            IReadOnlyList<DxfTypedValue> jog = dimensions[0].GetXDataForApplication("ACAD_DSTYLE_DIMJAG_POSITION");
            double x = double.Parse(jog[4].Value, CultureInfo.InvariantCulture);
            Assert.True(x > 0 && x < 50);

            Run(session, "DJL", new[] { dimensions[1].Handle });
            Assert.Equal("Jogs can be added to linear dimensions only.", session.History.Last());

            Run(session, "DLI", P(0, 100), P(100, 100), P(50, 70));
            DxfRotatedDimension owned = All<DxfRotatedDimension>(document.ModelSpace).Last();
            new CadOwnerTag("Test.Model", "VIEW-1").WriteTo(owned);
            document.Owners.Register(new MoveOnlyOwner());
            string start = document.Database.ToText();
            Run(session, "DJL", new[] { owned.Handle });
            Assert.False(session.IsRunning);
            Assert.Equal("This dimension belongs to the model and can only be moved.", session.History.Last());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void Dim_ALinePickedIsDimensioned_LevelOrAlongItByWhereTheLineIsPut_AndASecondLineGivesTheAngle()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "L", P(0, 0), P(100, 0), Enter, "L", P(0, 0), P(30, 40), Enter);
            string before = document.Database.ToText();

            Run(session, "DIM");
            Assert.Equal(
                "Select objects or specify first extension line origin or [Angular/Baseline/Continue/Ordinate/aliGn/Distribute/Layer/Undo]:",
                session.PromptText
            );
            Run(session, P(50, 0));
            Assert.Equal(
                "Specify dimension line location or second line for angle or [Mtext/Text/text aNgle/Undo]:",
                session.PromptText
            );
            Run(session, P(50, -30));
            Assert.StartsWith(
                "Select objects or specify first extension line origin",
                session.PromptText,
                StringComparison.Ordinal
            );

            // Beside the slanting line, square to a point of it: along it, 50.
            Run(session, P(15, 20), P(-5, 35));

            // Picked again, then the level line: the angle between them.
            Run(session, P(15, 20), P(80, 0));
            Assert.Equal("Specify dimension arc line location or [Text]:", session.PromptText);
            Run(session, P(40, 15), Enter);
            Assert.False(session.IsRunning);

            Assert.Equal(100, Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Measurement, 9);
            Assert.Equal(50, Assert.Single(All<DxfAlignedDimension>(document.ModelSpace)).Measurement, 9);
            Assert.Equal(
                Math.Atan2(40, 30),
                Assert.Single(All<DxfLineAngularDimension2>(document.ModelSpace)).Measurement,
                9
            );
            Assert.Equal("DIM", document.NextUndo.Name);

            // The whole run is one undo step.
            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void Dim_ACircleIsGivenItsDiameter_ARadiusOnRequest_AndAPointOnNothingStartsALinearDimension()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "C", P(0, 0), "20", "A", P(100, 0), P(120, 20), P(140, 0));

            Run(session, "DIM", P(20, 0));
            Assert.Equal(
                "Specify diameter dimension location or [Radius/Mtext/Text/text aNgle/Undo]:",
                session.PromptText
            );
            Run(session, P(30, 30), P(-20, 0), "R", P(-30, -30));
            Run(session, P(120, 20));
            Assert.Equal(
                "Specify radius dimension location or [Diameter/Angular/Mtext/Text/text aNgle/Undo]:",
                session.PromptText
            );
            Run(session, P(130, 40));

            Run(session, P(0, 100));
            Assert.Equal("Specify second extension line origin:", session.PromptText);
            Run(session, P(80, 100), P(40, 130), Enter);

            Assert.Equal(40, Assert.Single(All<DxfDiametricDimension>(document.ModelSpace)).Measurement, 9);
            Assert.Equal(
                new[] { 20.0, 20.0 },
                All<DxfRadialDimension>(document.ModelSpace).Select(d => Math.Round(d.Measurement, 9))
            );
            Assert.Equal(80, Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Measurement, 9);
        }

        [Fact]
        public void Dim_UndoTakesBackTheLastMade_OrdinateAndContinueRunTheirCommands_LayerPutsThemOnALayer()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "DIMS" });

            Run(session, "DIM", "LA", "DIMS", P(0, 0), P(100, 0), P(50, -30), "U");
            Assert.Empty(All<DxfRotatedDimension>(document.ModelSpace));
            Run(session, P(0, 0), P(100, 0), P(50, -30));
            Run(session, "C", P(100, -15), P(200, 0), Enter, Enter);
            Run(session, "O", P(300, 40), P(300, 80));
            Run(session, Enter);
            Assert.False(session.IsRunning);

            List<DxfRotatedDimension> row = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(new[] { 100.0, 100.0 }, row.Select(d => Math.Round(d.Measurement, 9)));
            Assert.Equal(300, Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace)).Measurement, 9);
            Assert.All(All<DxfDimension>(document.ModelSpace), d => Assert.Equal("DIMS", d.Layer));
        }

        [Fact]
        public void Dim_ALignPutsDimensionsOnTheBasesLine_AndDistributeSpacesThemEvenly()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            Run(session, "DLI", P(100, 0), P(200, 0), P(150, -45));
            Run(session, "DLI", P(0, 0), P(300, 0), P(50, -90));
            Run(session, "DLI", P(0, 0), P(200, 0), P(50, -50));
            List<DxfRotatedDimension> dimensions = All<DxfRotatedDimension>(document.ModelSpace);

            Run(session, "DIM", "G", new[] { dimensions[0].Handle }, new[] { dimensions[1].Handle }, Enter);
            Assert.Equal(-30, Entity<DxfRotatedDimension>(document, dimensions[1].Handle).DimLinePoint.Y, 9);

            Run(
                session,
                "D",
                Enter,
                new[] { dimensions[0].Handle },
                new[] { dimensions[2].Handle, dimensions[3].Handle },
                Enter,
                Enter
            );
            Assert.Equal(-60, Entity<DxfRotatedDimension>(document, dimensions[3].Handle).DimLinePoint.Y, 9);
            Assert.Equal(-90, Entity<DxfRotatedDimension>(document, dimensions[2].Handle).DimLinePoint.Y, 9);
        }

        [Fact]
        public void TheNewCommandsAndTheirAliasesAreAutoCads()
        {
            CadCommandRegistry registry = Session(Blank()).Registry;

            Assert.Equal("DIMORDINATE", registry.Find("DOR").Name);
            Assert.Equal("DIMORDINATE", registry.Find("DIMORD").Name);
            Assert.Equal("DIMJOGLINE", registry.Find("DJL").Name);
            Assert.Equal("DIMJOGGED", registry.Find("DJO").Name);
            Assert.Equal("DIMJOGGED", registry.Find("JOG").Name);
            Assert.Equal("DIM", registry.Find("DIM").Name);
        }
    }
}
