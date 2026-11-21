using System.IO;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The undo step of a run (D705 steps 3 and 5): everything a run draws, through the object model and through
    /// <c>command()</c>, is one step; a run that fails or is cancelled leaves the drawing as it was, with no group open, so
    /// that it can be saved.
    /// </summary>
    public sealed class UndoStepTests : PythonHostTestBase
    {
        public UndoStepTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>A line by the object model, a circle by command(), a second line, a LINE by command(): four entities.</summary>
        private static string[] DrawFour(params string[] after) =>
            new[]
            {
                UsesCad,
                AddLineInPython,
                "doc = cad.document()",
                "add_line(doc, 0)",
                "cad.command('CIRCLE', (0, 0), 5)",
                "add_line(doc, 50)",
                "cad.command('_LINE', (0, 100), (100, 100), '')",
            }
                .Concat(after)
                .ToArray();

        [Fact]
        public void ObjectModelEditsAndTwoCommands_AreOneStepOfTheRun_ThatUndoAndRedoTakeWhole()
        {
            // Four entities drawn: one U takes all four away, one Redo puts all four back. Without the run's group they would
            // be four steps, as the same edits are when typed one after the other.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawFour());

            CadDocument document = session.Document;
            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal(4, document.EntityCount);
            Assert.Equal("PYRUN", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
            Assert.True(document.Redo());
            Assert.Equal(4, document.EntityCount);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
        }

        [Fact]
        public void ARunThatOnlyAsksAndWrites_MakesNoStep_AndDoesNotMarkTheDrawingModified()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "cad.write('hello')", "cad.get_point('Where')" }, Typed("1,1"));

            Assert.False(session.Document.CanUndo);
            Assert.False(session.Document.IsModified);
        }

        [Fact]
        public void AnUndoGroupOfTheScript_NestsInTheStepOfTheRun()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    AddLineInPython,
                    "import geometryhelper",
                    "doc = cad.document()",
                    "with geometryhelper.undo_group(doc, 'inner'):",
                    "    add_line(doc, 0)",
                    "add_line(doc, 50)",
                }
            );

            CadDocument document = session.Document;
            Assert.Equal("PYRUN", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AnExceptionAfterEdits_LeavesNothing_NoGroupOpen_AndTheDrawingCanBeSaved()
        {
            // Four entities drawn, then an error: the group is cancelled (D705 step 5), the drawing is as before the run, and
            // SaveAs, which throws while a group is open, works.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawFour("raise ValueError('boom')"));

            CadDocument document = session.Document;
            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.IsInGroup);
            Assert.False(document.CanUndo);
            Assert.False(document.IsModified);
            string saved = Path.Combine(Folder, "saved.dxf");
            document.SaveAs(saved);
            Assert.True(File.Exists(saved));
        }

        [Fact]
        public void AnExceptionInsideAnInnerUndoGroup_CancelsTheWholeRun()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[]
                {
                    UsesCad,
                    AddLineInPython,
                    "import geometryhelper",
                    "doc = cad.document()",
                    "add_line(doc, 0)",
                    "with geometryhelper.undo_group(doc, 'inner'):",
                    "    add_line(doc, 50)",
                    "    raise ValueError('boom')",
                }
            );

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Equal(0, session.Document.EntityCount);
            Assert.False(session.Document.IsInGroup);
        }

        [Fact]
        public void AGroupTheScriptLeftOpen_IsEndedWhenTheRunCompletes()
        {
            // BeginGroup with no EndGroup: the run ends every group it left open, so the line is one step and SaveAs works.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[] { UsesCad, AddLineInPython, "doc = cad.document()", "doc.BeginGroup('mine')", "add_line(doc, 0)" }
            );

            CadDocument document = session.Document;
            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.False(document.IsInGroup);
            Assert.Equal(1, document.EntityCount);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AGroupTheScriptLeftOpen_IsCancelledWhenTheRunFails()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[]
                {
                    UsesCad,
                    AddLineInPython,
                    "doc = cad.document()",
                    "doc.BeginGroup('mine')",
                    "add_line(doc, 0)",
                    "raise ValueError('boom')",
                }
            );

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.False(session.Document.IsInGroup);
            Assert.Equal(0, session.Document.EntityCount);
            Assert.False(session.Document.CanUndo);
        }

        [Fact]
        public void ARunThatCancelsTheGroupItself_IsNotUndoneTwice()
        {
            // The script cancels the run's own group (CancelGroup closes every open group) and goes on: the end of the run
            // finds no group open, ends none (a bare EndGroup would throw) and completes.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[] { UsesCad, AddLineInPython, "doc = cad.document()", "add_line(doc, 0)", "doc.CancelGroup()" }
            );

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.False(session.Document.IsInGroup);
            Assert.Equal(0, session.Document.EntityCount);
        }
    }
}
