using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class DrawOptionsTests
    {
        private static DxfArc LastArc(CadDocument document) => All<DxfArc>(document.ModelSpace).Last();

        private static void Arc(DxfArc arc, double x, double y, double radius, double startDegrees, double endDegrees)
        {
            Near(new GeoPoint3(x, y, 0), arc.Center, 1E-9);
            Assert.Equal(radius, arc.Radius, 9);
            Assert.Equal(startDegrees, arc.StartAngle * 180.0 / Math.PI, 9);
            Assert.Equal(endDegrees, arc.EndAngle * 180.0 / Math.PI, 9);
        }

        [Fact]
        public void ArcByStartCenterEnd_AsksAsAutoCad2021Asks_AndRunsCounterClockwise()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "ARC");
            Assert.Equal("Specify start point of arc or [Center]:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Specify second point of arc or [Center/End]:", session.PromptText);
            Run(session, "C");
            Assert.Equal("Specify center point of arc:", session.PromptText);
            Run(session, P(10, 0));
            Assert.Equal("Specify end point of arc (hold Ctrl to switch direction) or [Angle/chord Length]:", session.PromptText);
            Run(session, P(10, 10));

            Arc(LastArc(document), 10, 0, 10, 180, 90);
        }

        [Fact]
        public void ArcByStartCenterEnd_WithCtrlHeld_RunsClockwise()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "ARC", P(0, 0), "C", P(10, 0));
            session.Feed(P(10, 10), false, true);

            // Clockwise from (0, 0) to (10, 10) about (10, 0) is the quarter from 90 to 180 degrees.
            Arc(LastArc(document), 10, 0, 10, 90, 180);
        }

        [Fact]
        public void ArcCenterFirst_ByIncludedAngle_AndByChordLength()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "ARC", "C", P(10, 0));
            Assert.Equal("Specify start point of arc:", session.PromptText);
            Run(session, P(0, 0), "A");
            Assert.Equal("Specify included angle (hold Ctrl to switch direction):", session.PromptText);
            Run(session, "90");
            Arc(LastArc(document), 10, 0, 10, 180, 270);

            Run(session, "ARC", P(0, 0), "C", P(10, 0), "L");
            Assert.Equal("Specify length of chord (hold Ctrl to switch direction):", session.PromptText);
            Run(session, "10");

            // A chord of 10 on a circle of radius 10 cuts off 60 degrees.
            Arc(LastArc(document), 10, 0, 10, 180, 240);
        }

        [Fact]
        public void ArcByStartEnd_ByAngle_Direction_AndRadius()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "ARC", P(0, 0), "E");
            Assert.Equal("Specify end point of arc:", session.PromptText);
            Run(session, P(20, 0));
            Assert.Equal("Specify center point of arc (hold Ctrl to switch direction) or [Angle/Direction/Radius]:", session.PromptText);
            Run(session, "A", "180");
            Arc(LastArc(document), 10, 0, 10, 180, 0);

            // Leaving (0, 0) straight up and ending at (20, 0): the half circle over the chord, clockwise.
            Run(session, "ARC", P(0, 0), "E", P(20, 0), "D");
            Assert.Equal("Specify tangent direction for the start point of arc (hold Ctrl to switch direction):", session.PromptText);
            Run(session, "90");
            Arc(LastArc(document), 10, 0, 10, 0, 180);

            Run(session, "ARC", P(0, 0), "E", P(20, 0), "R");
            Assert.Equal("Specify radius of arc (hold Ctrl to switch direction):", session.PromptText);
            Run(session, "10");
            Arc(LastArc(document), 10, 0, 10, 180, 0);
        }

        [Fact]
        public void ArcEnterAtTheFirstPrompt_GoesOnTangentFromTheLastLine()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", P(0, 0), P(10, 0), Enter, "ARC", Enter);
            Assert.Equal("Specify end point of arc (hold Ctrl to switch direction):", session.PromptText);
            Run(session, P(20, 10));

            // Leaving (10, 0) along +X and ending at (20, 10): the quarter about (10, 10) from 270 to 360 degrees.
            Arc(LastArc(document), 10, 10, 10, 270, 0);

            // And on again from the end of that arc, now going straight up.
            Run(session, "ARC", Enter, P(0, 10));
            Arc(LastArc(document), 10, 10, 10, 0, 180);
        }

        [Fact]
        public void CircleTtr_TouchesTwoLinesAtTheRadius_NearestThePicks()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "LINE", P(0, 0), P(0, 100), Enter);

            Run(session, "CIRCLE");
            Assert.Equal("Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:", session.PromptText);
            Run(session, "T");
            Assert.Equal("Specify point on object for first tangent of circle:", session.PromptText);
            Run(session, P(50, 50));
            Assert.Equal("Specify point on object for first tangent of circle:", session.PromptText);
            Run(session, P(30, 0));
            Assert.Equal("Specify point on object for second tangent of circle:", session.PromptText);
            Run(session, P(0, 30), "15");

            DxfCircle circle = All<DxfCircle>(document.ModelSpace).Single();
            Near(new GeoPoint3(15, 15, 0), circle.Center, 1E-9);
            Assert.Equal(15.0, circle.Radius, 9);
        }

        [Fact]
        public void CircleTanTanTan_OfThreeSidesOfASquare_IsItsInscribedCircle()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(40, 0), P(40, 40), P(0, 40), Enter);

            Run(session, "CIRCLE", "3P", "_tan", P(20, 0), "_tan", P(40, 20), "_tan", P(20, 40));

            DxfCircle circle = All<DxfCircle>(document.ModelSpace).Single();
            Near(new GeoPoint3(20, 20, 0), circle.Center, 1E-6);
            Assert.Equal(20.0, circle.Radius, 6);
        }

        [Fact]
        public void PlineWidthHalfwidthAndLength_AreKeptPerVertex_AndPlinewidTakesTheEndingWidth()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "PLINE", P(0, 0));
            Assert.Equal("Specify next point or [Arc/Halfwidth/Length/Undo/Width]:", session.PromptText);
            Run(session, "W");
            Assert.Equal("Specify starting width <0.0000>:", session.PromptText);
            Run(session, "1");
            Assert.Equal("Specify ending width <1.0000>:", session.PromptText);
            Run(session, "2", P(10, 0));
            Assert.Equal("Specify next point or [Arc/Close/Halfwidth/Length/Undo/Width]:", session.PromptText);
            Run(session, "H");
            Assert.Equal("Specify starting half-width <1.0000>:", session.PromptText);
            Run(session, "3");
            Assert.Equal("Specify ending half-width <3.0000>:", session.PromptText);
            Run(session, "4", "L");
            Assert.Equal("Specify length of line:", session.PromptText);
            Run(session, "5", Enter);

            DxfPolyline polyline = All<DxfPolyline>(document.ModelSpace).Single();
            Assert.Equal(3, polyline.NumberOfVertices);
            Near(P(15, 0), polyline.GetPoint2dAt(2));
            Assert.Equal(1.0, polyline.GetStartWidthAt(0));
            Assert.Equal(2.0, polyline.GetEndWidthAt(0));
            Assert.Equal(6.0, polyline.GetStartWidthAt(1));
            Assert.Equal(8.0, polyline.GetEndWidthAt(1));
            Assert.Equal(8.0, document.Database.Plinewid);
        }

        [Fact]
        public void PlineArcOptions_Angle_CEnter_Radius_SecondPt_AndDirection()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "PLINE", P(0, 0), P(10, 0), "A");
            Assert.Equal("Specify endpoint of arc (hold Ctrl to switch direction) or [Angle/CEnter/CLose/Direction/Halfwidth/Line/Radius/Second pt/Undo/Width]:", session.PromptText);
            Run(session, "A");
            Assert.Equal("Specify included angle:", session.PromptText);
            Run(session, "90");
            Assert.Equal("Specify endpoint of arc (hold Ctrl to switch direction) or [CEnter/Radius]:", session.PromptText);
            Run(session, P(20, 10), "CE", P(20, 20));
            Assert.Equal("Specify endpoint of arc (hold Ctrl to switch direction) or [Angle/Length]:", session.PromptText);
            Run(session, "A", "90", "R", "10");
            Assert.Equal("Specify endpoint of arc (hold Ctrl to switch direction) or [Angle]:", session.PromptText);
            Run(session, P(40, 30), "S");
            Assert.Equal("Specify second point on arc:", session.PromptText);
            Run(session, P(50, 40), P(60, 30), "D");
            Assert.Equal("Specify the tangent direction for the start point of arc:", session.PromptText);
            Run(session, "0");
            Assert.Equal("Specify endpoint of the arc (hold Ctrl to switch direction):", session.PromptText);
            Run(session, P(70, 40), Enter);

            DxfPolyline polyline = All<DxfPolyline>(document.ModelSpace).Single();
            double quarter = Math.Tan(Math.PI / 8.0);
            Assert.Equal(7, polyline.NumberOfVertices);
            Near(P(30, 20), polyline.GetPoint2dAt(3), 1E-9);
            Assert.Equal(0.0, polyline.GetBulgeAt(0), 12);
            Assert.Equal(quarter, polyline.GetBulgeAt(1), 12);
            Assert.Equal(quarter, polyline.GetBulgeAt(2), 12);
            Assert.Equal(quarter, polyline.GetBulgeAt(3), 12);
            Assert.Equal(-1.0, polyline.GetBulgeAt(4), 12);
            Assert.Equal(quarter, polyline.GetBulgeAt(5), 12);
        }

        [Fact]
        public void PlineArcByAngleAndRadius_AsksTheChordDirection_OfferingTheWayItLeaves()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "PLINE", P(0, 0), "A", "A", "180", "R", "5");
            Assert.Equal("Specify direction of chord for arc (hold Ctrl to switch direction) <0>:", session.PromptText);
            Run(session, "90", Enter);

            DxfPolyline polyline = All<DxfPolyline>(document.ModelSpace).Single();
            Near(P(0, 10), polyline.GetPoint2dAt(1), 1E-9);
            Assert.Equal(1.0, polyline.GetBulgeAt(0), 12);
        }

        [Fact]
        public void TextJustifiedMiddleCenter_IsHeldByItsPoint_AndEnterGoesOnBelowIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "TEXT");
            Assert.Equal("Specify start point of text or [Justify/Style]:", session.PromptText);
            Run(session, "J");
            Assert.Equal("Enter an option [Left/Center/Right/Align/Middle/Fit/TL/TC/TR/ML/MC/MR/BL/BC/BR]:", session.PromptText);
            Run(session, "MC");
            Assert.Equal("Specify middle point of text:", session.PromptText);
            Run(session, P(100, 50), "3", "0", "A-1", Enter);

            Run(session, "TEXT", Enter);
            Assert.Equal("Enter text:", session.PromptText);
            Run(session, "A-2", Enter);

            DxfDBText[] texts = All<DxfDBText>(document.ModelSpace).ToArray();
            Assert.Equal(2, texts.Length);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, texts[0].Justify);
            Near(new GeoPoint3(100, 50, 0), texts[0].AlignmentPoint, 1E-9);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, texts[1].Justify);
            Near(new GeoPoint3(100, 45, 0), texts[1].AlignmentPoint, 1E-9);
            Assert.Equal(3.0, texts[1].Height);
        }

        [Fact]
        public void TextAligned_RunsBetweenItsTwoBaselinePoints_AskingNoHeightNorRotation()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "TEXT", "J", "A");
            Assert.Equal("Specify first endpoint of text baseline:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Specify second endpoint of text baseline:", session.PromptText);
            Run(session, P(0, 30));
            Assert.Equal("Enter text:", session.PromptText);
            Run(session, "ABC", Enter);

            DxfDBText text = All<DxfDBText>(document.ModelSpace).Single();
            Assert.Equal(DxfAttachmentPoint.BaseAlign, text.Justify);
            Assert.Equal(Math.PI / 2.0, text.Rotation, 12);
            Near(new GeoPoint3(0, 30, 0), text.AlignmentPoint, 1E-9);
        }

        [Fact]
        public void TextStyle_MakesTheStyleNamedCurrent_AndSaysWhereThereIsNone()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "TEXT", "S");
            Assert.Equal("Enter style name or [?] <Standard>:", session.PromptText);
            Run(session, "Nothing");
            Assert.Contains("Cannot find text style \"Nothing\".", session.History);
            Assert.Equal("Specify start point of text or [Justify/Style]:", session.PromptText);
        }

        [Fact]
        public void MTextCornerOptions_Justify_LineSpacing_Rotation_AndWidth()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "MTEXT", P(0, 100));
            Assert.Equal("Specify opposite corner or [Height/Justify/Line spacing/Rotation/Style/Width/Columns]:", session.PromptText);
            Run(session, "J");
            Assert.Equal("Enter justification [TL/TC/TR/ML/MC/MR/BL/BC/BR] <TL>:", session.PromptText);
            Run(session, "MC", "L");
            Assert.Equal("Enter line spacing type [At least/Exactly] <At least>:", session.PromptText);
            Run(session, "E");
            Assert.Equal("Enter line spacing factor or distance <1x>:", session.PromptText);
            Run(session, "1.5x", "R", "10", P(100, 0), "Words");

            DxfMText text = All<DxfMText>(document.ModelSpace).Single();
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, text.Attachment);
            Near(new GeoPoint3(50, 50, 0), text.Location, 1E-9);
            Assert.Equal(100.0, text.Width, 9);
            Assert.Equal(DxfLineSpacingStyle.Exactly, text.LineSpacingStyle);
            Assert.Equal(1.5, text.LineSpacingFactor, 12);
            Assert.Equal(10.0, text.Rotation * 180.0 / Math.PI, 9);

            Run(session, "MTEXT", P(0, 0), "W");
            Assert.Equal("Specify width:", session.PromptText);
            Run(session, "40", "More");
            Assert.Equal(40.0, All<DxfMText>(document.ModelSpace).Last().Width, 9);
        }

        [Fact]
        public void MTextStaticColumns_AreStoredAsAutoCadStoresThem()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "MTEXT", P(0, 0), "C");
            Assert.Equal("Enter column type [Dynamic/Static/No columns] <Dynamic>:", session.PromptText);
            Run(session, "S", "30", "2", "4", "20", "Words");

            DxfMText text = All<DxfMText>(document.ModelSpace).Single();
            Assert.Equal(DxfColumnType.StaticColumns, text.ColumnType);
            Assert.Equal(2, text.ColumnCount);
            Assert.Equal(13.0, text.ColumnWidth, 9);
            Assert.Equal(4.0, text.ColumnGutterWidth, 9);
        }
    }
}
