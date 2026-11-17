using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// CHSPACE against AutoCAD 2021's console: "** Command not allowed in Model Tab **", "Set the SOURCE viewport active and press
    /// ENTER to continue.", "Ignoring 3 selected viewport(s)", "2 object(s) changed from PAPER space to MODEL space." and
    /// "Objects were scaled by a factor of 0.681818196712555 to maintain visual appearance." for a viewport at 1.4667 paper units
    /// to the model unit, where the line (0, 0) to (10, 10) of model space went to (-3.3, -2.35) to (11.3667, 12.3167).
    /// </summary>
    public class ChangeSpaceTests
    {
        /// <summary>
        /// Layout1 with a viewport 200 by 100 about (110, 60) showing model space at 1:10, its view about (1 000, 500): the model
        /// point (100, 100) is at (20, 20) on the paper.
        /// </summary>
        private static CadDocument WithViewport(out CadCommandSession session, out string viewport)
        {
            CadDocument document = Blank();
            session = Session(document);
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            viewport = All<DxfViewport>(document.CurrentSpace).Last().Handle;

            // The view of a viewport made in an empty model is one unit high: made 1 000 high about (1 000, 500).
            Assert.True(document.SetViewportView(viewport, P(1000, 500), 1000.0, "ZOOM").Succeeded);
            return document;
        }

        [Fact]
        public void OnTheModelTab_AndOnALayoutWithNoViewport_ItIsRefusedWithAutoCadsWords()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "CHSPACE");
            Assert.Equal("** Command not allowed in Model Tab **", session.History.Last());
            Assert.False(session.IsRunning);

            document.CurrentSpace = document.Spaces[1];
            Run(session, "CHSPACE");
            Assert.Equal("There are no active Model space viewports", session.History.Last());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void FromTheViewport_ObjectsOfModelSpaceGoToThePaperScaledAndPlacedAsTheViewportShowsThem()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string viewport);
            Assert.True(document.EnterViewport(viewport));
            Run(session, "LINE", P(100, 100), P(600, 300), Enter);
            Assert.Single(All<DxfLine>(document.ModelSpace));

            Run(session, "CHSPACE");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, "ALL", Enter);
            Assert.Equal("Set the SOURCE viewport active and press ENTER to continue.:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            DxfLine line = Assert.Single(All<DxfLine>(document.CurrentSpace));
            Near(new GeoPoint3(20, 20, 0), line.StartPoint, 1E-9);
            Near(new GeoPoint3(70, 40, 0), line.EndPoint, 1E-9);
            Assert.Contains("1 object(s) changed from MODEL space to PAPER space.", session.History);
            Assert.Contains("Objects were scaled by a factor of 0.1 to maintain visual", session.History);
            Assert.Contains("appearance.", session.History);

            // The command ends on the paper, as AutoCAD's does.
            Assert.Null(document.ActiveViewport);
            Assert.Equal("CHSPACE", document.NextUndo.Name);
        }

        [Fact]
        public void FromThePaper_ObjectsGoIntoTheViewportsModelSpaceScaledTheOtherWay_ViewportsAreIgnored()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string viewport);
            document.ApplyEdit("draw", null, s =>
                s.Append(document.CurrentSpace, new DxfCircle(new GeoPoint3(110, 60, 0), new GeoVector3(0, 0, 1), 10.0))
            );

            Run(session, "CHSPACE", "ALL", Enter);
            Assert.Contains("Ignoring 1 selected viewport(s)", session.History);
            Assert.Equal("Set the TARGET viewport active and press ENTER to continue.:", session.PromptText);
            Run(session, Enter);

            // The paper point at the middle of the viewport is the view's centre; 10 paper units are 100 of model space.
            DxfCircle circle = Assert.Single(All<DxfCircle>(document.ModelSpace));
            Near(new GeoPoint3(1000, 500, 0), circle.Center, 1E-9);
            Assert.Equal(100.0, circle.Radius, 9);
            Assert.Empty(All<DxfCircle>(document.CurrentSpace));
            Assert.Single(All<DxfViewport>(document.CurrentSpace), v => v.Handle == viewport);
            Assert.Contains("1 object(s) changed from PAPER space to MODEL space.", session.History);
            Assert.Contains("Objects were scaled by a factor of 10 to maintain visual", session.History);

            // And it ends in the viewport, as AutoCAD's does.
            Assert.Equal(viewport, document.ActiveViewport.Handle);
        }

        [Fact]
        public void AViewportTwisted_TurnsTheObjectsWithItsView()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string viewport);
            DxfViewport frame = Entity<DxfViewport>(document, viewport);
            frame.TwistAngle = Math.PI / 2.0;
            document.ApplyEdit("draw", null, s =>
                s.Append(
                    document.CurrentSpace,
                    new DxfLine { StartPoint = new GeoPoint3(110, 60, 0), EndPoint = new GeoPoint3(130, 60, 0) }
                )
            );

            Run(session, "CHSPACE", "L", Enter, Enter);

            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            GeoPoint2 start = CadViewports.ToModel(frame, P(110, 60));
            GeoPoint2 end = CadViewports.ToModel(frame, P(130, 60));
            Near(new GeoPoint3(start.X, start.Y, 0), line.StartPoint, 1E-9);
            Near(new GeoPoint3(end.X, end.Y, 0), line.EndPoint, 1E-9);

            // 20 paper units along x are 200 along the view's x, which a quarter turn lays along model y (down).
            Assert.Equal(start.X, end.X, 9);
            Assert.Equal(200.0, Math.Abs(end.Y - start.Y), 9);
        }

        [Fact]
        public void APointPickedInTheViewportAtTheTargetPrompt_NamesTheViewport_AndEnterWithNoneUsesTheFirst()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string viewport);
            document.ApplyEdit("draw", null, s =>
                s.Append(document.CurrentSpace, new DxfCircle(new GeoPoint3(110, 60, 0), new GeoVector3(0, 0, 1), 10.0))
            );

            Run(session, "CHSPACE", "L", Enter, P(100, 50));

            Assert.Single(All<DxfCircle>(document.ModelSpace));
            Assert.Equal(viewport, document.ActiveViewport.Handle);
        }

        [Fact]
        public void NothingSelected_ChangesNothing()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out _);

            Run(session, "CHSPACE", Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfLine>(document.ModelSpace));
        }
    }
}
