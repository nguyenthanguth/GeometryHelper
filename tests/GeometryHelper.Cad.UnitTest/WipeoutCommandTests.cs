using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// WIPEOUT as AutoCAD 2021's command asks and writes it (probe draw1, accoreconsole through standard input): the points of
    /// an outline, Polyline with its "Erase polyline?", Frames and the variable WIPEOUTFRAME.
    /// </summary>
    public class WipeoutCommandTests
    {
        private static DxfWipeout Last(CadDocument document) => All<DxfWipeout>(document.ModelSpace).Last();

        private static int FramesOf(CadDocument document) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.OfType<DisplayPolyline2>()
                .Count(i => i.Handle == Last(document).Handle);

        [Fact]
        public void TheFirstPrompt_OffersFramesAndPolyline_WithPolylineTheDefault()
        {
            CadCommandSession session = Session(Blank());

            Run(session, "WIPEOUT");

            Assert.Equal("Specify first point or [Frames/Polyline] <Polyline>:", session.PromptText);
        }

        [Fact]
        public void ThePointsAsked_OfferUndoFromTheThirdAndCloseFromTheFourth()
        {
            CadCommandSession session = Session(Blank());

            Run(session, "WIPEOUT", P(10, 10));
            Assert.Equal("Specify next point:", session.PromptText);
            Run(session, P(50, 10));
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, P(50, 40));
            Assert.Equal("Specify next point or [Close/Undo]:", session.PromptText);
        }

        [Fact]
        public void FourPointsAndEnter_MakeTheWipeout_FramedAsAutoCadsCommandFramesIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "WIPEOUT", P(10, 10), P(50, 10), P(50, 40), P(20, 40), Enter);

            DxfWipeout wipeout = Last(document);
            Assert.False(session.IsRunning);

            // The square on the box of the points, at its lowest corner (the API's SetFrom lays it on the origin as well).
            wipeout.GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
            Near(new GeoPoint3(10, 10, 0), origin);
            Assert.Equal(40.0, u.X, 9);
            Assert.Equal(40.0, v.Y, 9);
            GeoPoint2[] boundary = wipeout.GetClipBoundary().ToArray();
            Assert.Equal(5, boundary.Length);
            Near(P(-0.5, 0.5), boundary[0]);
            Near(P(0.5, 0.5), boundary[1]);
            Near(P(0.5, -0.25), boundary[2]);
            Near(P(-0.25, -0.25), boundary[3]);
            Near(P(-0.5, 0.5), boundary[4]);
            GeoPoint3[] corners = wipeout.GetVertices().ToArray();
            Near(new GeoPoint3(10, 10, 0), corners[0]);
            Near(new GeoPoint3(50, 10, 0), corners[1]);
            Near(new GeoPoint3(50, 40, 0), corners[2]);
            Near(new GeoPoint3(20, 40, 0), corners[3]);
            AssertReopens(document);
        }

        [Fact]
        public void AWipeoutFarFromTheOrigin_IsFramedOnItsOwnBox_NotOnTheOriginToo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "WIPEOUT", P(70, 10), P(90, 10), P(90, 30), "C");

            DxfWipeout wipeout = Last(document);
            wipeout.GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
            Near(new GeoPoint3(70, 10, 0), origin);
            Assert.Equal(20.0, u.X, 9);
            Assert.Equal(20.0, v.Y, 9);
            Assert.Equal(4, wipeout.GetClipBoundary().Count);
        }

        [Fact]
        public void TheFirstWipeout_MakesTheVariablesOfWipeouts_FramesShown()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Assert.False(document.Database.NamedObjectsDictionary.Contains("ACAD_WIPEOUT_VARS"));

            Run(session, "WIPEOUT", P(0, 0), P(10, 0), P(10, 10), "C");

            var variables = Assert.IsType<DxfWipeoutVariables>(
                document.Database.NamedObjectsDictionary.GetAt("ACAD_WIPEOUT_VARS")
            );
            Assert.True(variables.ShowFrames);
        }

        [Fact]
        public void EnterOnTwoPoints_MakesNone_AndUndoTakesTheLastPointBack()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "WIPEOUT", P(10, 10), P(50, 10), Enter);
            Assert.Empty(All<DxfWipeout>(document.ModelSpace));

            Run(session, "WIPEOUT", P(10, 10), P(50, 10), "U");
            Assert.Equal("Specify next point:", session.PromptText);
            Run(session, P(50, 40));
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, P(10, 40), Enter);

            // 10,10 50,40 10,40: the point undone is not among them.
            GeoPoint3[] corners = Last(document).GetVertices().ToArray();
            Assert.Equal(4, corners.Length);
            Near(new GeoPoint3(50, 40, 0), corners[1]);
        }

        [Fact]
        public void PointsThatLieInALine_MakeNoWipeout()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "WIPEOUT", P(10, 10), P(20, 10), P(30, 10), Enter);

            Assert.Empty(All<DxfWipeout>(document.ModelSpace));
            Assert.Contains("*Invalid* The points do not close an area.", session.History);
        }

        [Fact]
        public void AWipeoutInATurnedUcs_HasItsPointsWhereTheUcsPutsThem()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "UCS", "Z", "90");
            Run(session, "WIPEOUT", P(0, 0), P(10, 0), P(10, 10), "C");

            // The picked points are world points; the wipeout stands on them.
            GeoPoint3[] corners = Last(document).GetVertices().ToArray();
            Near(new GeoPoint3(0, 0, 0), corners[0]);
            Near(new GeoPoint3(10, 0, 0), corners[1]);
            Near(new GeoPoint3(10, 10, 0), corners[2]);
        }

        [Fact]
        public void Polyline_TakesAClosedPolylinesCorners_AsksWhetherToEraseIt_AndKeepsItOnNo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "RECTANG", P(10, 10), P(40, 30));
            string handle = All<DxfPolyline>(document.ModelSpace).Single().Handle;

            Run(session, "WIPEOUT", "P");
            Assert.Equal("Select a closed polyline:", session.PromptText);
            Run(session, (object)new[] { handle });
            Assert.Equal("Erase polyline? [Yes/No] <No>:", session.PromptText);
            Run(session, "N");

            Assert.NotNull(document.FindEntity(handle));
            GeoPoint3[] corners = Last(document).GetVertices().ToArray();
            Near(new GeoPoint3(10, 10, 0), corners[0]);
            Near(new GeoPoint3(40, 10, 0), corners[1]);
            Near(new GeoPoint3(40, 30, 0), corners[2]);
            Near(new GeoPoint3(10, 30, 0), corners[3]);
        }

        [Fact]
        public void Polyline_TakenByEnter_AndErasedOnYes_InOneUndoStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "RECTANG", P(10, 10), P(40, 30));
            string handle = All<DxfPolyline>(document.ModelSpace).Single().Handle;

            Run(session, "WIPEOUT", Enter);
            Assert.Equal("Select a closed polyline:", session.PromptText);
            Run(session, new[] { handle }, "Y");

            Assert.Null(document.FindEntity(handle));
            Assert.Single(All<DxfWipeout>(document.ModelSpace));

            Assert.True(document.Undo());
            Assert.NotNull(document.FindEntity(handle));
            Assert.Empty(All<DxfWipeout>(document.ModelSpace));
        }

        [Fact]
        public void APolylineNotClosedOrOfArcs_IsRefused_AndTheCommandEnds()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "PLINE", P(0, 0), P(10, 0), P(10, 10), Enter);
            string open = All<DxfPolyline>(document.ModelSpace).Single().Handle;

            Run(session, "WIPEOUT", "P", new[] { open });

            Assert.Contains("The polyline must be closed and made up of only line segments.", session.History);
            Assert.False(session.IsRunning);

            // A filleted rectangle is closed, but of arcs.
            Run(session, "RECTANG", "F", "2", P(20, 20), P(50, 40));
            string rounded = All<DxfPolyline>(document.ModelSpace).Last().Handle;
            Run(session, "WIPEOUT", "P", new[] { rounded });

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfWipeout>(document.ModelSpace));
        }

        [Fact]
        public void Frames_AsksTheModeWithTheCurrentOneTheDefault_AndSetsWipeoutframe()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "WIPEOUT", "F");
            Assert.Equal("Enter mode [ON/OFF/Display but not plot] <ON>:", session.PromptText);

            Run(session, "OFF");
            Assert.False(DxfWipeoutVariables.GetWipeoutVariables(document.Database).ShowFrames);
            Run(session, "WIPEOUTFRAME");
            Assert.Equal("Enter new value for WIPEOUTFRAME <0>:", session.PromptText);
            Run(session, Esc);

            Run(session, "WIPEOUT", "F");
            Assert.Equal("Enter mode [ON/OFF/Display but not plot] <OFF>:", session.PromptText);
            Run(session, "D");

            // Shown and not plotted is written as shown (70 of 1), as AutoCAD 2021 writes it, and read as 2 by the session.
            Assert.True(DxfWipeoutVariables.GetWipeoutVariables(document.Database).ShowFrames);
            Run(session, "WIPEOUTFRAME");
            Assert.Equal("Enter new value for WIPEOUTFRAME <2>:", session.PromptText);
            Run(session, Esc);

            Run(session, "WIPEOUT", "F");
            Assert.Equal("Enter mode [ON/OFF/Display but not plot] <Display but not plot>:", session.PromptText);
            Run(session, "ON");
            Run(session, "WIPEOUTFRAME");
            Assert.Equal("Enter new value for WIPEOUTFRAME <1>:", session.PromptText);
        }

        [Fact]
        public void TheVariable_SetsTheFrames_AndEveryChangeIsOneUndoStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "WIPEOUTFRAME", "0");
            Assert.False(DxfWipeoutVariables.GetWipeoutVariables(document.Database).ShowFrames);

            Assert.True(document.Undo());
            Assert.True(DxfWipeoutVariables.GetWipeoutVariables(document.Database).ShowFrames);

            Run(session, "WIPEOUTFRAME", "3");
            Assert.Equal("Enter new value for WIPEOUTFRAME <1>:", session.PromptText);
        }

        [Fact]
        public void TheFrameOfAWipeout_IsDrawnInItsColourUntilFramesAreSetOff()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "WIPEOUT", P(10, 10), P(50, 10), P(50, 40), "C");

            Assert.Equal(1, FramesOf(document));
            Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>());

            Run(session, "WIPEOUT", "F", "OFF");

            Assert.Equal(0, FramesOf(document));
            Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>());

            Run(session, "WIPEOUT", "F", "ON");
            Assert.Equal(1, FramesOf(document));
        }

        [Fact]
        public void AWipeoutAndItsVariables_ReopenFromTheirText()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "WIPEOUT", P(10, 10), P(50, 10), P(50, 40), "C", "WIPEOUT", "F", "OFF");

            AssertReopens(document);
            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());
            Assert.False(DxfWipeoutVariables.GetWipeoutVariables(again).ShowFrames);
            Assert.IsType<DxfWipeout>(again.ModelSpace.First());
        }
    }
}
