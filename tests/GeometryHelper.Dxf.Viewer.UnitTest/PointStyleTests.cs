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
    /// The point style: PDMODE and PDSIZE asked as AutoCAD's SETVAR asks them, PTYPE (DDPTYPE) asking both with no window,
    /// and each mode drawn by the presenter: a plus, a cross, a tick, a circle and a square, sized in drawing units or as a
    /// share of the view.
    /// </summary>
    public class PointStyleTests
    {
        private static CadDocument Points(int pdmode, double pdsize, params GeoPoint3[] at)
        {
            var database = new DxfDatabase("AC1027") { Pdmode = pdmode, Pdsize = pdsize };

            foreach (GeoPoint3 p in at)
            {
                database.ModelSpace.AppendEntity(new DxfDBPoint(p));
            }

            return CadDocument.From(database, "points.dxf");
        }

        private static List<DisplayItem2> Drawn(CadDocument document) =>
            document.GetDisplay(document.ModelSpace).Items.ToList();

        private static void Segment(DisplayItem2 item, double x0, double y0, double x1, double y1)
        {
            DisplayPolyline2 line = Assert.IsType<DisplayPolyline2>(item);
            Assert.Equal(4, line.Xy.Count);
            Assert.Equal(x0, line.Xy[0], 9);
            Assert.Equal(y0, line.Xy[1], 9);
            Assert.Equal(x1, line.Xy[2], 9);
            Assert.Equal(y1, line.Xy[3], 9);
        }

        [Fact]
        public void PdmodeIsAskedAsSetvarAsks_AndAChangeIsOneUndoStepThatDrawsThePointsAgain()
        {
            CadDocument document = Points(0, 10.0, new GeoPoint3(100, 50, 0));
            Assert.Single(Drawn(document));

            CadCommandSession session = Run(Session(document), "PDMODE");
            Assert.Equal("Enter new value for PDMODE <0>:", session.PromptText);
            Run(session, "3");

            Assert.False(session.IsRunning);
            Assert.Equal(3, document.Database.Pdmode);
            List<DisplayItem2> cross = Drawn(document);
            Assert.Equal(2, cross.Count);
            Segment(cross[0], 95, 45, 105, 55);
            Segment(cross[1], 95, 55, 105, 45);

            Assert.True(document.Undo());
            Assert.Equal(0, document.Database.Pdmode);
            Assert.Single(Drawn(document));
        }

        [Fact]
        public void PdmodeRefusesAWholeNumberOutOfRange_AndEnterKeepsIt()
        {
            CadDocument document = Points(0, 0.0);
            CadCommandSession session = Run(Session(document), "PDMODE", "40000");

            Assert.Equal("Requires an integer between -32768 and 32767.", session.History.Last());
            Assert.Equal("Enter new value for PDMODE <0>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void PdsizeTakesASizeOrAShareOfTheView_AsAutoCadAsksIt()
        {
            CadDocument document = Points(0, 0.0);

            CadCommandSession session = Run(Session(document), "PDSIZE");
            Assert.Equal("Enter new value for PDSIZE <0.0000>:", session.PromptText);
            Run(session, "-10");

            Assert.Equal(-10.0, document.Database.Pdsize);
            Assert.True(document.Undo());
            Assert.Equal(0.0, document.Database.Pdsize);
        }

        [Fact]
        public void PtypeAndDdptypeWithNoWindow_AskPdmodeAndThenPdsize()
        {
            CadDocument document = Points(0, 0.0);

            CadCommandSession session = Run(Session(document), "PTYPE", "34");
            Assert.Equal("Enter new value for PDSIZE <0.0000>:", session.PromptText);
            Run(session, "2.5");
            Run(session, "DDPTYPE", Enter);
            Assert.Equal("Enter new value for PDSIZE <2.5000>:", session.PromptText);
            Run(session, Enter);

            Assert.Equal(34, document.Database.Pdmode);
            Assert.Equal(2.5, document.Database.Pdsize);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void APlusWithACircle_ATick_AndASquareAlone_AreDrawnAboutThePointAtPdsize()
        {
            List<DisplayItem2> plus = Drawn(Points(34, 4.0, new GeoPoint3(10, 20, 0)));
            List<DisplayItem2> tick = Drawn(Points(4, 4.0, new GeoPoint3(10, 20, 0)));
            List<DisplayItem2> square = Drawn(Points(65, 4.0, new GeoPoint3(10, 20, 0)));
            List<DisplayItem2> dot = Drawn(Points(32, 4.0, new GeoPoint3(10, 20, 0)));

            Assert.Equal(3, plus.Count);
            Segment(plus[0], 8, 20, 12, 20);
            Segment(plus[1], 10, 18, 10, 22);
            DisplayArc2 circle = Assert.IsType<DisplayArc2>(plus[2]);
            Assert.Equal(10.0, circle.Cx, 9);
            Assert.Equal(20.0, circle.Cy, 9);
            Assert.Equal(2.0, circle.Radius, 9);
            Segment(Assert.Single(tick), 10, 20, 10, 22);
            Assert.Equal(4, square.Count);
            Assert.Equal(8.0, square.Min(i => i.Bounds.MinX), 9);
            Assert.Equal(12.0, square.Max(i => i.Bounds.MaxX), 9);
            Assert.Equal(2, dot.Count);
            Assert.Equal(new[] { 10.0, 20.0 }, Assert.IsType<DisplayPolyline2>(dot[0]).Xy);
        }

        [Fact]
        public void ARelativeSizeIsAShareOfTheViewRegeneratedIn_NoughtFivePerCent()
        {
            CadDocument relative = Points(3, -10.0, new GeoPoint3(0, 0, 0));
            CadDocument fivePercent = Points(3, 0.0, new GeoPoint3(0, 0, 0));
            relative.PointViewHeight = 300.0;
            fivePercent.PointViewHeight = 300.0;

            Segment(Drawn(relative)[0], -15, -15, 15, 15);
            Segment(Drawn(fivePercent)[0], -7.5, -7.5, 7.5, 7.5);

            // REGEN takes the height of the view of its command line, 600 pixels at 4 a unit.
            CadCommandSession session = Session(relative);
            session.View = new CadView(800, 600);
            session.View.ZoomAt(400, 300, 4.0);
            Run(session, "REGEN");

            Assert.Equal(150.0, relative.PointViewHeight, 9);
            Segment(Drawn(relative)[0], -7.5, -7.5, 7.5, 7.5);
        }
    }
}
