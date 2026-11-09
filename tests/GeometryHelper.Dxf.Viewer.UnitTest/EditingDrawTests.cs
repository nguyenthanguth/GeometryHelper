using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingDrawTests
    {
        private static CadDocument Drawn(params object[] inputs)
        {
            CadDocument document = Blank();
            CadLayerEditor.Add(document, "Steel");
            CadLayerEditor.SetCurrent(document, "Steel");
            Run(Session(document), inputs);
            return document;
        }

        private static T Last<T>(CadDocument document)
            where T : DxfEntity
        {
            T made = All<T>(document.ModelSpace).LastOrDefault();
            Assert.NotNull(made);
            Assert.Equal("Steel", made.Layer);
            Assert.Equal(256, made.ColorIndex);
            return made;
        }

        [Fact]
        public void LineAsksAsAutoCadDoes_AndDrawsOnTheCurrentLayerByLayer()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE");
            Assert.Equal("Specify first point:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, P(10, 0), Enter);

            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.Equal("0", line.Layer);
            Assert.True(line.Color.IsByLayer);
            AssertReopens(document);
        }

        [Fact]
        public void PlineDrawsLinesThenATangentArcAndCloses()
        {
            CadDocument document = Drawn("PL", P(0, 0), P(100, 0), "A", P(100, 100), "L", P(0, 100), "C");

            DxfPolyline polyline = Last<DxfPolyline>(document);
            Assert.True(polyline.Closed);
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.Equal(0.0, polyline.GetBulgeAt(0), 12);
            Assert.Equal(1.0, polyline.GetBulgeAt(1), 12);
            Near(P(0, 100), polyline.GetPoint2dAt(3));
            Assert.Equal(100 * 100 + Math.PI * 50 * 50 / 2.0, polyline.Area, 6);
            Assert.Single(All<DxfPolyline>(document.ModelSpace));
            AssertReopens(document);
        }

        [Fact]
        public void PlineUndoTakesTheLastSegmentBack()
        {
            CadDocument document = Drawn("PL", P(0, 0), P(100, 0), P(100, 50), "U", P(50, 50), Enter);

            DxfPolyline polyline = Last<DxfPolyline>(document);
            Assert.Equal(3, polyline.NumberOfVertices);
            Near(P(50, 50), polyline.GetPoint2dAt(2));
        }

        [Fact]
        public void CircleByCentreAndRadius_ByTwoPoints_AndByThreePoints()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "C");
            Assert.Equal("Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Specify radius of circle or [Diameter]:", session.PromptText);
            Run(session, "15");
            Run(session, "C", P(100, 0));
            Assert.Equal("Specify radius of circle or [Diameter] <15.0000>:", session.PromptText);
            Run(session, "D", "50");
            Run(session, "C", "2P", P(200, 0), P(220, 0));
            Run(session, "C", "3p", P(310, 0), P(300, 10), P(290, 0));

            List<DxfCircle> circles = All<DxfCircle>(document.ModelSpace);
            Assert.Equal(new[] { 15.0, 25.0, 10.0, 10.0 }, circles.Select(c => Math.Round(c.Radius, 9)));
            Near(new GeoPoint3(210, 0, 0), circles[2].Center);
            Near(new GeoPoint3(300, 0, 0), circles[3].Center, 1E-9);
        }

        [Fact]
        public void ArcThroughThreePoints_RunsCounterClockwiseAsDxfHoldsIt()
        {
            CadDocument document = Drawn("A", P(10, 0), P(0, 10), P(-10, 0));

            DxfArc arc = Last<DxfArc>(document);
            Near(new GeoPoint3(0, 0, 0), arc.Center, 1E-9);
            Assert.Equal(10, arc.Radius, 9);
            Assert.Equal(0.0, arc.StartAngle, 9);
            Assert.Equal(Math.PI, arc.EndAngle, 9);
        }

        [Fact]
        public void RectangDrawsAClosedPolylineOfFourCorners()
        {
            CadDocument document = Drawn("REC", P(0, 0), P(300, 200));

            DxfPolyline rectangle = Last<DxfPolyline>(document);
            Assert.True(rectangle.Closed);
            Assert.Equal(60000, rectangle.Area, 9);
            Near(P(300, 0), rectangle.GetPoint2dAt(1));
        }

        [Fact]
        public void TextAsksItsPointHeightAndTurn_AndTakesTheCurrentStyle()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "DT", P(10, 20));
            Assert.Equal("Specify height <2.5000>:", session.PromptText);
            Run(session, "5");
            Assert.Equal("Specify rotation angle of text <0>:", session.PromptText);
            Run(session, "90", "BEAM B-12");

            DxfDBText text = Assert.Single(All<DxfDBText>(document.ModelSpace));
            Assert.Equal("BEAM B-12", text.TextString);
            Assert.Equal(5, text.Height, 9);
            Assert.Equal(Math.PI / 2.0, text.Rotation, 9);
            Assert.Equal("Standard", text.TextStyleName);
            Near(new GeoPoint3(10, 20, 0), text.Position);
        }

        [Fact]
        public void TextGoesOnLineAfterLine_EachFiveThirdsOfTheHeightBelow_UntilAnEmptyLine_AsOneStep()
        {
            CadDocument document = Blank();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);

            Run(session, "DT", P(0, 0), "6", "0", "FIRST");
            Assert.Equal("Enter text:", session.PromptText);
            Run(session, "SECOND", Enter);

            List<DxfDBText> texts = All<DxfDBText>(document.ModelSpace);
            Assert.Equal(new[] { "FIRST", "SECOND" }, texts.Select(t => t.TextString));
            Near(new GeoPoint3(0, -10, 0), texts[1].Position, 1E-9);
            Assert.False(session.IsRunning);
            document.Undo();
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void DimAngularOfTwoLines_MeasuresTheAngleItsArcIsPutIn()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "L", P(0, 0), P(100, 0), Enter, "L", P(0, 0), P(100, 100), Enter);

            Run(session, "DAN");
            Assert.Equal("Select arc, circle, line, or <specify vertex>:", session.PromptText);
            Run(session, P(50, 0));
            Assert.Equal("Select second line:", session.PromptText);
            Run(session, P(50, 50), P(40, 15));

            DxfLineAngularDimension2 dimension = Assert.Single(All<DxfLineAngularDimension2>(document.ModelSpace));
            Assert.Equal(Math.PI / 4.0, dimension.Measurement, 9);
            Assert.StartsWith("Dimension text = 45", session.History.Last(), StringComparison.Ordinal);
            Assert.True(dimension.DimBlock.IsAnonymous);
            DxfArc arc = dimension.DimBlock.OfType<DxfArc>().Single();
            Assert.Equal(Math.Sqrt(40 * 40 + 15 * 15), arc.Radius, 9);
            Assert.Equal(0.0, arc.StartAngle, 9);
            Assert.Equal(Math.PI / 4.0, arc.EndAngle, 9);
            AssertReopens(document);
        }

        [Fact]
        public void DimAngularByItsVertex_MeasuresTheSideTheArcIsPutOn_ThreeQuartersOfATurnBehindIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "DAN", Enter);
            Assert.Equal("Specify angle vertex:", session.PromptText);
            Run(session, P(0, 0), P(10, 0), P(0, 10), P(-5, -5));

            DxfPoint3AngularDimension dimension = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));
            Assert.StartsWith("Dimension text = 270", session.History.Last(), StringComparison.Ordinal);
            DxfArc arc = dimension.DimBlock.OfType<DxfArc>().Single();
            Assert.Equal(Math.PI / 2.0, arc.StartAngle, 9);
            Assert.Equal(0.0, arc.EndAngle, 9);
        }

        [Fact]
        public void MTextFillsTheBoxFromItsTopLeftCorner()
        {
            CadDocument document = Drawn("T", P(0, 0), "H", "3.5", P(120, 40), "Plate 300 x 200");

            DxfMText text = Last<DxfMText>(document);
            Assert.Equal("Plate 300 x 200", text.Contents);
            Assert.Equal(120, text.Width, 9);
            Assert.Equal(3.5, text.TextHeight, 9);
            Near(new GeoPoint3(0, 40, 0), text.Location);
        }

        [Fact]
        public void DimLinearMeasuresLevelOrUprightByWhereItsLineIsPut_AndIsDrawnByABlockOfItsOwn()
        {
            CadDocument document = Blank();
            document.Database.DimStyleTable["Standard"].Dimtad = 1;
            CadCommandSession session = Session(document);

            Run(session, "DLI", P(0, 0), P(300, 100));
            Assert.Equal("Specify dimension line location or [Text/Horizontal/Vertical]:", session.PromptText);
            Run(session, P(150, -30));
            string words = session.History.Last().Substring("Dimension text = ".Length);
            Assert.StartsWith("300", words, StringComparison.Ordinal);
            Run(session, "DLI", P(0, 0), P(300, 100), P(350, 50));

            List<DxfRotatedDimension> dimensions = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(2, dimensions.Count);
            Assert.Equal(300, dimensions[0].Measurement, 9);
            Assert.Equal(100, dimensions[1].Measurement, 9);
            Assert.StartsWith("*D", dimensions[0].DimBlock.Name, StringComparison.Ordinal);
            Assert.NotEqual(dimensions[0].DimBlock.Name, dimensions[1].DimBlock.Name);
            Assert.True(dimensions[0].DimBlock.IsAnonymous);
            Assert.Equal("Standard", dimensions[0].DimensionStyleName);

            // The block draws the dimension line 30 below, two arrowheads and the words 300 above it.
            DisplayList2 shown = document.GetDisplay(document.ModelSpace);
            Assert.Contains(shown.Items.OfType<DisplayText2>(), t => t.Handle == dimensions[0].Handle && t.Text == words && t.Y > -30);
            Assert.Equal(2, shown.Items.OfType<DisplayFill2>().Count(f => f.Handle == dimensions[0].Handle));
            Assert.Contains(shown.Items.OfType<DisplayPolyline2>(), l => l.Handle == dimensions[0].Handle && l.Count == 2 && Math.Abs(l.Xy[0]) < 1E-9 && Math.Abs(l.Xy[1] + 30) < 1E-9 && Math.Abs(l.Xy[2] - 300) < 1E-9 && Math.Abs(l.Xy[3] + 30) < 1E-9);
            AssertReopens(document);
        }

        [Fact]
        public void DimAlignedMeasuresAlongItsPoints_AndTypedTextTakesThePlaceOfTheMeasurement()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "DAL", P(0, 0), P(300, 400), "T", "<> mm", P(-40, 30));

            DxfAlignedDimension dimension = Assert.Single(All<DxfAlignedDimension>(document.ModelSpace));
            Assert.Equal(500, dimension.Measurement, 9);
            string words = session.History.Last().Substring("Dimension text = ".Length);
            Assert.StartsWith("500", words, StringComparison.Ordinal);
            Assert.EndsWith(" mm", words, StringComparison.Ordinal);
            Assert.Contains(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>(), t => t.Text == words);
        }

        [Fact]
        public void DimRadiusAndDimDiameterMeasureTheCirclePicked()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "C", P(0, 0), "20");

            Run(session, "DRA", P(20, 0));
            Assert.StartsWith("Dimension text = R20", session.History.Last(), StringComparison.Ordinal);
            Run(session, P(40, 40));
            Run(session, "DDI", P(-20, 0), P(-40, -40));

            Assert.Equal(20, Assert.Single(All<DxfRadialDimension>(document.ModelSpace)).Measurement, 9);
            Assert.Equal(40, Assert.Single(All<DxfDiametricDimension>(document.ModelSpace)).Measurement, 9);
            AssertReopens(document);
        }

        [Fact]
        public void MLeaderPointsAtThePointWithItsWordsAtTheLanding()
        {
            CadDocument document = BlankWithMLeaderStyle();
            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");

            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            Assert.Equal("B-12", mleader.MText.Text);
            Assert.Equal("Standard", mleader.MLeaderStyle);
            Assert.Contains(document.GetDisplay(document.ModelSpace).Items, i => i.Handle == mleader.Handle);
            AssertReopens(document);
        }

        [Fact]
        public void MLeaderInADrawingOfNoMultileaderStyle_SaysSoAndDrawsNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Run(Session(document), "MLD");

            Assert.False(session.IsRunning);
            Assert.Equal("The drawing has no multileader style to draw in: one made by AutoCAD's MLEADERSTYLE is needed.", session.History.Last());
            Assert.Empty(All<DxfMLeader>(document.ModelSpace));
        }

        [Fact]
        public void MViewInALayoutMakesAViewportOfAllOfModelSpace()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));

            Run(session, "MV");
            Assert.Equal("** Command not allowed in Model Tab **", session.History.Last());

            CadSpace layout = document.Spaces[1];
            document.CurrentSpace = layout;
            Run(session, "MV", P(10, 10), P(210, 110));

            DisplayViewport2 viewport = Assert.Single(document.GetDisplay(layout).Items.OfType<DisplayViewport2>());
            Assert.Equal(110, viewport.CenterX, 9);
            Assert.Equal(200, viewport.Width, 9);
            Assert.Equal(0.1, viewport.Scale, 9);
            Assert.Equal(1000, viewport.ViewCenterX, 9);
            AssertReopens(document);

            document.Undo();
            Assert.Empty(document.GetDisplay(layout).Items.OfType<DisplayViewport2>());
        }

        [Fact]
        public void EveryDrawCommandIsOneStep_AndUndoingThemAllGivesTheStartText()
        {
            CadDocument document = BlankWithMLeaderStyle();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);

            Run(session, "L", P(0, 0), P(10, 0), P(10, 10), Enter);
            Run(session, "PL", P(20, 0), P(30, 0), "A", P(30, 10), Enter);
            Run(session, "C", P(50, 0), "5");
            Run(session, "A", P(60, 0), P(65, 5), P(70, 0));
            Run(session, "REC", P(80, 0), P(90, 10));
            Run(session, "DT", P(0, 20), "2.5", "0", "NOTE", Enter);
            Run(session, "MT", P(0, 40), P(50, 30), "NOTE");
            Run(session, "DLI", P(0, 0), P(10, 0), P(5, -5));
            Run(session, "MLD", P(0, 0), P(-10, 10), "M1");

            Assert.Equal(9, Enumerable.Range(0, 20).TakeWhile(i => document.Undo()).Count());
            Assert.Equal(start, document.Database.ToText());
        }
    }
}
