using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The point modifiers AutoCAD takes at any prompt for a point, typed on the command line: FROM, M2P and MTP, TT and the
    /// point filters .X .Y .Z .XY .XZ .YZ, nested in one another and in the UCS.
    /// </summary>
    public class PointModifierTests
    {
        [Fact]
        public void FromWithABasePointAndAnOffset_StartsTheLineAtTheOffsetPoint_WithAutoCADsPrompts()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", "FROM");
            Assert.Equal("Base point:", session.PromptText);
            Run(session, "100,50");
            Assert.Equal("<Offset>:", session.PromptText);
            Assert.Equal(P(100, 50), session.Prompt.BasePoint);
            Run(session, "@30,20", "@100<0", Enter);

            DxfLine line = All<DxfLine>(document.ModelSpace).Single();
            Near(new GeoPoint3(130, 70, 0), line.StartPoint);
            Near(new GeoPoint3(230, 70, 0), line.EndPoint);
            Assert.Equal(
                new[]
                {
                    "Specify first point: FROM",
                    "Base point: 100,50",
                    "<Offset>: @30,20",
                    "Specify next point or [Undo]: @100<0",
                },
                session.History.SkipWhile(l => l != "Specify first point: FROM").Take(4)
            );
        }

        [Fact]
        public void FromWithAPickedBaseAndAPolarOffset_GivesThePointThatFarAlongTheAngle()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", "0,0", "fro", P(100, 50), "@50<90", Enter);

            DxfLine line = All<DxfLine>(document.ModelSpace).Single();
            Near(new GeoPoint3(100, 100, 0), line.EndPoint);
            Assert.Equal(P(100, 100), session.LastPoint);
        }

        [Fact]
        public void FromInAUcsTurnedAQuarter_TakesTheBaseAndTheOffsetInTheUcs()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "UCS", "100,50", Enter, "UCS", "Z", "90");

            Run(session, "POINT", "FROM", "0,0", "@10,0");

            // The UCS origin is the world (100, 50) and its X axis runs up the world's Y.
            DxfDBPoint point = All<DxfDBPoint>(document.ModelSpace).Single();
            Near(new GeoPoint3(100, 60, 0), point.Position);
        }

        [Fact]
        public void Tracking_RunsOrthogonallyFromPointToPoint_AndEnterGivesThePointTracked_WithAutoCADsPrompts()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // The centre of a plate 200 by 100 whose corner is at (0, 0): along X from the middle of its bottom edge, then up.
            Run(session, "CIRCLE", "TK");
            Assert.Equal("First tracking point:", session.PromptText);
            Run(session, "100,0");
            Assert.Equal("Next point  (Press ENTER to end tracking):", session.PromptText);
            Assert.Equal(P(100, 0), session.Prompt.BasePoint);

            // A point picked off the axes keeps the coordinate of the way it lies farther: (130, 50) is up, so x stays 100.
            Run(session, P(130, 50));
            Assert.Equal(P(100, 50), session.Prompt.BasePoint);
            Assert.Equal("Next point  (Press ENTER to end tracking):", session.PromptText);
            Run(session, Enter);

            Assert.StartsWith("Specify radius of circle or [Diameter]", session.PromptText);
            Run(session, "10");
            DxfCircle circle = All<DxfCircle>(document.ModelSpace).Single();
            Near(new GeoPoint3(100, 50, 0), circle.Center);
            Assert.Equal(
                new[]
                {
                    "Specify center point for circle or [3P/2P/Ttr (tan tan radius)]: TK",
                    "First tracking point: 100,0",
                    "Next point  (Press ENTER to end tracking):",
                },
                session.History.SkipWhile(l => !l.EndsWith(": TK", System.StringComparison.Ordinal)).Take(3)
            );
        }

        [Fact]
        public void Tracking_TakesADistanceTypedAlongTheAxisTheCursorIsNearest()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // From (0, 0), 60 along X towards the cursor up and to the right, then 40 up.
            Run(session, "POINT", "TRACKING", "0,0");
            session.Cursor = P(80, 10);
            Run(session, "60");
            Assert.Equal(P(60, 0), session.Prompt.BasePoint);
            session.Cursor = P(70, 90);
            Run(session, "40", Enter);

            DxfDBPoint point = All<DxfDBPoint>(document.ModelSpace).Single();
            Near(new GeoPoint3(60, 40, 0), point.Position);
        }

        [Fact]
        public void TtAndTkAreNotTakenInsideAnotherModifier_NorFromInsideTk_AsAutoCad()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // AutoCAD 2021 refuses TK and TT at FROM's "Base point:", and FROM at TK's "Next point", as text that is no point.
            Run(session, "LINE", "0,0", "FROM", "TK");
            Assert.Equal("Point or option keyword required.", session.History.Last());
            Assert.Equal("Base point:", session.PromptText);
            Run(session, "TT");
            Assert.Equal("Point or option keyword required.", session.History.Last());
            Run(session, "10,10", "@5,0");

            Run(session, "TK", "100,0", "FROM");
            Assert.Equal("Invalid 2D point.", session.History.Last());
            Assert.Equal("Next point  (Press ENTER to end tracking):", session.PromptText);

            // M2P is taken there, and FROM within M2P.
            Run(session, "M2P", "130,40", "150,60", Enter);
            Run(session, "M2P", "FROM", "10,10", "@5,0", "30,30", Enter);

            var lines = All<DxfLine>(document.ModelSpace);
            Near(new GeoPoint3(15, 10, 0), lines[0].EndPoint);
            Near(new GeoPoint3(100, 50, 0), lines[1].EndPoint);
            Near(new GeoPoint3(22.5, 20, 0), lines[2].EndPoint);
        }

        [Fact]
        public void Tracking_InAUcsTurnedAQuarter_RunsAlongTheAxesOfTheUcs()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "UCS", "Z", "90");

            // The UCS's X runs up the world's Y: (5, 100) lies farther along the UCS's X from (0, 0), so its world x is dropped.
            Run(session, "POINT", "TK", P(0, 0), P(5, 100), Enter);

            DxfDBPoint point = All<DxfDBPoint>(document.ModelSpace).Single();
            Near(new GeoPoint3(0, 100, 0), point.Position);
        }

        [Fact]
        public void M2PAndMtp_GiveThePointMidwayBetweenTwo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", "M2P");
            Assert.Equal("First point of mid:", session.PromptText);
            Run(session, "0,0");
            Assert.Equal("Second point of mid:", session.PromptText);
            Run(session, P(100, 40), "MTP", "200,0", "200,100", Enter);

            DxfLine line = All<DxfLine>(document.ModelSpace).Single();
            Near(new GeoPoint3(50, 20, 0), line.StartPoint);
            Near(new GeoPoint3(200, 50, 0), line.EndPoint);
            Assert.Contains("Specify first point: M2P", session.History);
            Assert.Contains("First point of mid: 0,0", session.History);
            Assert.Contains("Second point of mid:", session.History);
        }

        [Fact]
        public void M2PNestedInFrom_GivesTheBasePointMidway()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "POINT", "FROM", "M2P", "0,0", "100,0");
            Assert.Equal("<Offset>:", session.PromptText);
            Run(session, "@0,25");

            Near(new GeoPoint3(50, 25, 0), All<DxfDBPoint>(document.ModelSpace).Single().Position);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void M2PAtTheRadiusOfACircle_IsMeasuredFromTheCentre()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "CIRCLE", "0,0", "M2P", "0,0", "60,80");

            Assert.Equal(50.0, All<DxfCircle>(document.ModelSpace).Single().Radius, 12);
        }

        [Fact]
        public void TheXFilter_KeepsTheXOfTheFirstPoint_AndTakesYFromTheSecond()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "POINT", ".X");
            Assert.Equal("of", session.PromptText);
            Run(session, P(30, 999));
            Assert.Equal("(need YZ):", session.PromptText);
            Run(session, P(888, 40));

            Near(new GeoPoint3(30, 40, 0), All<DxfDBPoint>(document.ModelSpace).Single().Position);
            Assert.Contains("Specify a point: .X", session.History);
            Assert.Contains("of", session.History);
        }

        [Theory]
        [InlineData(".Y", "(need XZ):", 888.0, 999.0)]
        [InlineData(".Z", "(need XY):", 888.0, 40.0)]
        [InlineData(".XY", "(need Z):", 30.0, 999.0)]
        [InlineData(".XZ", "(need Y):", 30.0, 40.0)]
        [InlineData(".YZ", "(need X):", 888.0, 999.0)]
        public void EachFilterAsksForWhatItLacks_AndCombinesThePoints(string filter, string need, double x, double y)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "POINT", filter, P(30, 999));
            Assert.Equal(need, session.PromptText);
            Run(session, P(888, 40));

            Near(new GeoPoint3(x, y, 0), All<DxfDBPoint>(document.ModelSpace).Single().Position);
        }

        [Fact]
        public void TheCoordinatesNeededTyped_AreTakenInTheirOrder_AndZIsLeftOut()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // Two numbers where two are needed are a point, whose Y is taken, as AutoCAD 2021 takes (30, 3) here.
            Run(session, "POINT", ".X", "30,999", "40,3");
            Run(session, "POINT", ".XY", "70,80", "5");
            Run(session, "POINT", ".YZ", "1,15", "-20");

            var points = All<DxfDBPoint>(document.ModelSpace);
            Near(new GeoPoint3(30, 3, 0), points[0].Position);
            Near(new GeoPoint3(70, 80, 0), points[1].Position);
            Near(new GeoPoint3(-20, 15, 0), points[2].Position);
        }

        [Fact]
        public void AFilterInAUcsTurnedAQuarter_CombinesTheUcsCoordinates()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "UCS", "Z", "90");

            // The world (0, 30) is UCS (30, 0), the world (-40, 0) is UCS (0, 40): UCS (30, 40) is the world (-40, 30).
            Run(session, "POINT", ".X", "*0,30", "*-40,0");

            Near(new GeoPoint3(-40, 30, 0), All<DxfDBPoint>(document.ModelSpace).Single().Position);
        }

        [Fact]
        public void TtAsksForATemporaryTrackPoint_ThenAsksTheFirstPromptAgain_UntilAPointIsGiven()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", "TT");
            Assert.Equal("Specify temporary OTRACK point:", session.PromptText);
            Run(session, P(10, 10));

            Assert.Equal("Specify first point:", session.PromptText);
            Assert.Equal(new[] { P(10, 10) }, session.TemporaryTrackPoints);
            Run(session, "_tt", P(20, 0));
            Assert.Equal(new[] { P(10, 10), P(20, 0) }, session.TemporaryTrackPoints);

            Run(session, "10,0");

            Assert.Empty(session.TemporaryTrackPoints);
            Assert.Contains("Specify first point: TT", session.History);
            Assert.Contains("Specify temporary OTRACK point:", session.History);
        }

        [Fact]
        public void EnterAtAModifiersPrompt_IsNotAPoint_AndEscCancelsTheCommand()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // At LINE's first point, which has no options, AutoCAD 2021 says "Invalid point."; at its next point, "Point or
            // option keyword required."
            Run(session, "LINE", "M2P", Enter);

            Assert.Equal("First point of mid:", session.PromptText);
            Assert.Equal("Invalid point.", session.History.Last());

            Run(session, Esc);

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Run(session, "LINE", "0,0");
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void TrackingToAPointAsFarAlongBothAxes_GoesUpright_AsAutoCad()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // AutoCAD 2021 tracks from (100, 0) towards (130, 30) to (100, 30).
            Run(session, "POINT", "TK", "100,0", "130,30", Enter);

            Near(new GeoPoint3(100, 30, 0), All<DxfDBPoint>(document.ModelSpace).Single().Position);
        }

        [Fact]
        public void EnterAtTheFirstTrackingPoint_SaysInvalid2DPoint_AndAsksThePromptItWasTypedAtAgain()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", "0,0", "TK", Enter);

            Assert.Equal("Invalid 2D point.", session.History.Last());
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);

            // Text at "Next point" is no point either, and tracking goes on.
            Run(session, "TK", "100,0", "abc");
            Assert.Equal("Invalid 2D point.", session.History.Last());
            Assert.Equal("Next point  (Press ENTER to end tracking):", session.PromptText);
            Run(session, "130,50", Enter, Enter);

            Near(new GeoPoint3(100, 50, 0), All<DxfLine>(document.ModelSpace).Single().EndPoint);
        }

        [Fact]
        public void EnterAtFromTtOrAFilter_IsEnterAtThePromptTheyWereTypedAt_AsAutoCad()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // Each ends the LINE at its "Specify next point", as AutoCAD 2021 does, the segment drawn kept.
            foreach (
                object[] inputs in new[]
                {
                    new object[] { "FROM", Enter },
                    new object[] { "FROM", "5,5", Enter },
                    new object[] { "TT", Enter },
                    new object[] { ".X", Enter },
                    new object[] { ".X", "30,999", Enter },
                }
            )
            {
                Run(session, "LINE", "0,0", "10,0");
                Run(session, inputs);
                Assert.False(session.IsRunning, string.Join(" ", inputs));
            }

            Assert.Equal(5, All<DxfLine>(document.ModelSpace).Count);
        }

        [Fact]
        public void TextAtTtOrAFilter_IsAnsweredByThePromptTheyWereTypedAt_AsAutoCad()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", "0,0", "TT", "abc");
            Assert.Equal("Point or option keyword required.", session.History.Last());
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);

            Run(session, ".X", "30,999", "abc");
            Assert.Equal("Point or option keyword required.", session.History.Last());
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);

            Run(session, ".X", "abc");
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, "45,0", Enter);

            Near(new GeoPoint3(45, 0, 0), All<DxfLine>(document.ModelSpace).Single().EndPoint);
        }

        [Fact]
        public void FromAndM2P_AskAgainForWhatIsNoPoint_AsAutoCad()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", "0,0", "M2P", "abc");
            Assert.Equal("Point or option keyword required.", session.History.Last());
            Assert.Equal("First point of mid:", session.PromptText);
            Run(session, "10,10", Enter);
            Assert.Equal("Point or option keyword required.", session.History.Last());
            Assert.Equal("Second point of mid:", session.PromptText);
            Run(session, "30,30");

            Run(session, "FROM", "abc");
            Assert.Equal("Point or option keyword required.", session.History.Last());
            Assert.Equal("Base point:", session.PromptText);
            Run(session, "10,10", "@5,0", Enter);

            var lines = All<DxfLine>(document.ModelSpace);
            Near(new GeoPoint3(20, 20, 0), lines[0].EndPoint);
            Near(new GeoPoint3(15, 10, 0), lines[1].EndPoint);
        }

        [Fact]
        public void AModifierIsNotTakenWhereNoPointIsAsked()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Assert.False(session.StartPointModifier("FROM"));
            Run(session, "ERASE");

            Assert.False(session.StartPointModifier("M2P"));
            Assert.Equal("Select objects:", session.PromptText);
        }
    }
}
