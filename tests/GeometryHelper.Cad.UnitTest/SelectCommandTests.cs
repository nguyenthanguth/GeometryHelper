using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The SELECT command, with the transcripts of AutoCAD 2021's accoreconsole (the L8 measurements m/entmod, s1 and s2): three
    /// lines, (0,0)-(10,0), (0,10)-(10,10) and (50,50)-(60,50), taken by a window, a crossing, ALL, Last, Previous, Remove, a pick
    /// and a selection set a routine hands to <c>(command "_.SELECT" ss "")</c>. "Select objects:" ends at Enter; what was taken is
    /// the Previous set. Typed, the objects stay selected as the implied set; run by a routine, none does.
    /// </summary>
    public sealed class SelectCommandTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Lines()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "LINE", "0,0", "10,0", Enter);
            Run(session, "LINE", "0,10", "10,10", Enter);
            Run(session, "LINE", "50,50", "60,50", Enter);
            return session;
        }

        private static string[] Handles(CadCommandSession session) =>
            session.Document.ModelSpace.Block.OfType<DxfEntity>().Select(e => e.Handle).ToArray();

        private static string Previous(CadCommandSession session) =>
            LispEntityTests.Value(session, "(if (ssget \"_P\") (sslength (ssget \"_P\")) 0)");

        [Fact]
        public void SelectTyped_AsksSelectObjects_AndEnterEndsItWithNothingTaken()
        {
            CadCommandSession session = Lines();

            Run(session, "SELECT");

            Assert.True(session.IsRunning);
            Assert.Equal("Select objects:", session.PromptText);

            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(0, session.Document.Selection.Count);
            Assert.Equal("0", Previous(session));
        }

        [Fact]
        public void AWindowOverTwoLines_FindsBothAndAsksAgain_ThenEnterEndsWithThemSelected()
        {
            CadCommandSession session = Lines();
            int before = session.History.Count;

            Run(session, "SELECT", "-5,-5", "15,15");

            string[] said = session.History.Skip(before).ToArray();
            Assert.Equal("Command: SELECT", said[0]);
            Assert.Equal("Select objects: -5,-5", said[1]);
            Assert.Contains("15,15", said[2]);
            Assert.Equal("2 found", said[3]);
            Assert.Equal("Select objects:", session.PromptText);

            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("2", Previous(session));
            Assert.Equal(2, session.Document.Selection.Count);
        }

        [Fact]
        public void ACrossingAtTheCorner_FindsOne()
        {
            CadCommandSession session = Lines();

            Run(session, "SELECT", "C", "-5,-5", "5,5", Enter);

            Assert.Equal("1", Previous(session));
        }

        [Fact]
        public void AllTypedAtSelect_TakesEveryObject()
        {
            CadCommandSession session = Lines();
            int before = session.History.Count;

            Run(session, "SELECT", "ALL", Enter);

            Assert.Equal(
                new[] { "Command: SELECT", "Select objects: ALL", "3 found", "Select objects:" },
                session.History.Skip(before).ToArray()
            );
            Assert.Equal("3", Previous(session));
        }

        [Fact]
        public void LastTypedAtSelect_TakesTheObjectMadeLast()
        {
            CadCommandSession session = Lines();

            Run(session, "SELECT", "L", Enter);

            Assert.Equal("1", Previous(session));
            Assert.Equal(Handles(session).Last(), session.Document.Selection.Handles.Single());
        }

        [Fact]
        public void RemoveAfterAll_LeavesTheOthers()
        {
            CadCommandSession session = Lines();
            int before = session.History.Count;

            Run(session, "SELECT", "ALL", "R", "5,0", Enter);

            Assert.Contains("1 found, 1 removed, 2 total", session.History.Skip(before));
            Assert.Equal("2", Previous(session));
        }

        [Fact]
        public void AnEnterAtOnce_LeavesThePreviousSetOfTheCommandBefore()
        {
            CadCommandSession session = Lines();
            Run(session, "SELECT", "ALL", Enter);

            Run(session, "SELECT", Enter);

            Assert.Equal("3", Previous(session));
        }

        [Fact]
        public void APointPick_FindsTheLineUnderIt()
        {
            CadCommandSession session = Lines();

            Run(session, "SELECT", "5,0", Enter);

            Assert.Equal("1", Previous(session));
        }

        [Fact]
        public void AFenceAlongNoLine_FindsNothingAndKeepsThePreviousSet()
        {
            CadCommandSession session = Lines();
            Run(session, "SELECT", "ALL", Enter);
            LispEntityTests.Value(session, "(sssetfirst nil nil)");
            int before = session.History.Count;

            Run(session, "SELECT", "F", "-5,5", "15,5", Enter, Enter);

            Assert.Contains("0 found", session.History.Skip(before));
            Assert.Equal("3", Previous(session));
        }

        [Fact]
        public void ObjectsHandedByHandle_BecomeThePreviousSet()
        {
            CadCommandSession session = Lines();
            string[] two = Handles(session).Take(2).ToArray();

            Run(session, "SELECT", two, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("2", Previous(session));
        }

        [Fact]
        public void ObjectsSelectedBeforeASelectTyped_AreTakenAtOnceWithNoPrompt()
        {
            CadCommandSession session = Lines();
            session.Document.Selection.Set(Handles(session).Take(1).ToArray());
            int before = session.History.Count;

            Run(session, "SELECT");

            // AutoCAD 2021 (s2): the next typed SELECT takes the implied set with "1 found" and ends
            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "Command: SELECT", "1 found" }, session.History.Skip(before).ToArray());
        }

        [Fact]
        public void ASelectionSetHandedByARoutine_IsThePreviousSet_AndNothingStaysSelected()
        {
            CadCommandSession session = Lines();
            LispEntityTests.Value(session, "(setq ss (ssget \"_X\"))");

            LispEntityTests.Value(session, "(command \"_.SELECT\" ss \"\")");

            Assert.False(session.IsRunning);
            Assert.Equal("3", Previous(session));
            Assert.Equal(0, session.Document.Selection.Count);
            Assert.Equal("nil", LispEntityTests.Value(session, "(ssget \"_I\")"));
        }

        [Fact]
        public void LastHandedByARoutine_TakesTheLastObject()
        {
            CadCommandSession session = Lines();

            LispEntityTests.Value(session, "(command \"_.SELECT\" \"_L\" \"\")");

            Assert.Equal("1", Previous(session));
        }

        [Fact]
        public void SelectThenEraseWithPrevious_ErasesWhatSelectTook()
        {
            CadCommandSession session = Lines();
            LispEntityTests.Value(session, "(command \"_.SELECT\" \"_W\" \"-5,-5\" \"15,15\" \"\")");

            LispEntityTests.Value(session, "(command \"_.ERASE\" \"_P\" \"\")");

            Assert.Equal(1, session.Document.EntityCount);
        }
    }
}
