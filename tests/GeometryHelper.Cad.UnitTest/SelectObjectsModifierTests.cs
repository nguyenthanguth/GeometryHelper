using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The point modifiers at "Select objects:", a prompt for one object, and the corner of a window, as AutoCAD 2021's console
    /// takes them (probed through its standard input: TT, FROM, M2P, TK and the point filters, typed with or without the
    /// underscore): the point a modifier gives picks as a click does, and on no object is the first corner of the window.
    /// </summary>
    public class SelectObjectsModifierTests
    {
        private static CadCommandSession Erasing() => Run(Session(Plan()), "ERASE");

        [Fact]
        public void FromAtSelectObjects_PicksTheObjectAtTheOffsetPoint()
        {
            CadCommandSession session = Run(Erasing(), "FROM", "0,-10", "@50,10");

            Assert.Equal(
                new[] { "Select objects: FROM", "Base point: 0,-10", "<Offset>: @50,10", "1 found" },
                session.History.Skip(session.History.Count - 4)
            );
            Assert.Equal("Select objects:", session.PromptText);
            Assert.Equal(new[] { "A1" }, session.Gathering);
        }

        [Fact]
        public void MidBetweenAtSelectObjects_PicksTheObjectAtTheMiddle()
        {
            CadCommandSession session = Run(Erasing(), "_m2p", "0,0", "100,0");

            Assert.Equal(
                new[] { "Select objects: _m2p", "First point of mid: 0,0", "Second point of mid: 100,0", "1 found" },
                session.History.Skip(session.History.Count - 4)
            );
            Assert.Equal(new[] { "A1" }, session.Gathering);
        }

        [Fact]
        public void TrackingAtSelectObjects_PicksTheObjectAtTheLastPoint_WhenEnterEndsIt()
        {
            CadCommandSession session = Run(Erasing(), "TK", "50,-30", "50,0", Enter);

            Assert.Equal("1 found", session.History.Last());
            Assert.Equal(new[] { "A1" }, session.Gathering);
            Assert.Equal("Select objects:", session.PromptText);
        }

        [Fact]
        public void APointFilterAtSelectObjects_PicksTheObjectAtThePointItBuilds()
        {
            CadCommandSession session = Run(Erasing(), ".X", "50,999", P(888, 0));

            Assert.Equal("1 found", session.History.Last());
            Assert.Equal(new[] { "A1" }, session.Gathering);
        }

        [Fact]
        public void TemporaryTrackPointAtSelectObjects_AcquiresThePoint_AndAsksSelectObjectsAgain()
        {
            CadCommandSession session = Run(Erasing(), "TT", "50,50");

            Assert.Equal(
                new[] { "Select objects: TT", "Specify temporary OTRACK point: 50,50" },
                session.History.Skip(session.History.Count - 2)
            );
            Assert.Equal("Select objects:", session.PromptText);
            Assert.Equal(new[] { P(50, 50) }, session.TemporaryTrackPoints);
            Assert.Empty(session.Gathering);

            // A point is given at last: the tracking was for it.
            Run(session, "50,0");

            Assert.Empty(session.TemporaryTrackPoints);
            Assert.Equal(new[] { "A1" }, session.Gathering);
        }

        [Fact]
        public void FromToAPointOnNoObject_StartsTheWindowThere_AndTheCornerTakesFromToo()
        {
            CadCommandSession session = Run(Erasing(), "FROM", "0,0", "@-10,-10");

            Assert.Equal(
                new[] { "Select objects: FROM", "Base point: 0,0", "<Offset>: @-10,-10" },
                session.History.Skip(session.History.Count - 3)
            );
            Assert.True(session.IsWindowOpen);
            Assert.StartsWith("Specify opposite corner", session.PromptText);

            // The opposite corner is a point asked as any: FROM gives (100, 60), and the window from (-10, -10) takes A1 and A2.
            Run(session, "FROM", "0,0", "@110,60");

            Assert.Equal("2 found", session.History.Last());
            Assert.False(session.IsWindowOpen);
            Assert.Equal("Select objects:", session.PromptText);
            Assert.Equal(new[] { "A1", "A2" }, session.Gathering);
        }

        [Fact]
        public void FromAtTheFirstCornerOfWindow_GivesTheCorner()
        {
            CadCommandSession session = Run(Erasing(), "W", "FROM", "0,0", "@-10,-10");

            Assert.StartsWith("Specify opposite corner", session.PromptText);

            Run(session, "110,60");

            Assert.Equal("2 found", session.History.Last());
            Assert.Equal(new[] { "A1", "A2" }, session.Gathering);
        }

        [Fact]
        public void AModifierAtThePointOfAFence_GivesThePoint()
        {
            CadCommandSession session = Run(Erasing(), "F", "TT", "50,50", "10,-10", "FROM", "0,0", "@10,10", Enter);

            Assert.Equal("Select objects:", session.PromptText);
            Assert.Equal(new[] { "A1" }, session.Gathering);
        }

        [Fact]
        public void AModifierWhileMultipleGathersPoints_KeepsThePointForEnter()
        {
            CadCommandSession session = Run(Erasing(), "M", "FROM", "0,-10", "@50,10");

            Assert.Empty(session.Gathering);

            Run(session, Enter);

            Assert.Equal("1 found", session.History.Last());
            Assert.Equal(new[] { "A1" }, session.Gathering);
        }

        [Theory]
        [InlineData("FROM", "abc", "Base point:")]
        [InlineData("FROM", "0,0 abc", "<Offset>:")]
        [InlineData("M2P", "abc", "First point of mid:")]
        [InlineData("M2P", "0,0 abc", "Second point of mid:")]
        [InlineData("TK", "0,0 abc", "Next point  (Press ENTER to end tracking):")]
        [InlineData("TK", "abc", "Select objects:")]
        [InlineData(".X", "abc", "Select objects:")]
        [InlineData(".X", "0,0 abc", "Select objects:")]
        [InlineData("TT", "abc", "Select objects:")]
        public void TextNoModifierTakes_AtSelectObjects_IsAnInvalidSelection_AsAutoCad2021SaysIt(
            string modifier,
            string typed,
            string asked
        )
        {
            CadCommandSession session = Run(Erasing(), modifier);
            Run(session, typed.Split(' ').Cast<object>().ToArray());

            Assert.Equal("*Invalid selection*", session.History.Skip(session.History.Count - 3).First());
            Assert.Equal("Expects a point or", session.History.Skip(session.History.Count - 2).First());
            Assert.StartsWith("Window/Last/Crossing/BOX/ALL", session.History.Last());
            Assert.Equal(asked, session.PromptText);
        }

        [Theory]
        [InlineData("TT")]
        [InlineData("FROM")]
        public void EnterAtTheModifiersPrompt_IsTheEnterOfSelectObjects_AndEndsTheSelection(string modifier)
        {
            CadCommandSession session = Run(Erasing(), modifier, Enter);

            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AModifierAtAPromptForOneObject_PicksTheObjectAtItsPoint()
        {
            CadCommandSession session = Run(Session(Plan()), "BREAK", "FROM", "0,-10", "@50,10");

            Assert.Equal(
                new[] { "Select object: FROM", "Base point: 0,-10", "<Offset>: @50,10" },
                session.History.Skip(session.History.Count - 3)
            );
            Assert.StartsWith("Specify second break point", session.PromptText);
        }

        [Fact]
        public void AModifierAtAPromptForOneObject_GivingAPointOnNone_AsksAgain_StartingNoWindow()
        {
            CadCommandSession session = Run(Session(Plan()), "BREAK", "FROM", "0,0", "@150,150");

            Assert.False(session.IsWindowOpen);
            Assert.Equal("Select object:", session.PromptText);
        }
    }
}
