using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What the session does around a routine that waits (D35 to D37, D42, D60): the selection ask Part B's ssget hands over,
    /// the undo step of an evaluation, a file that asks while it loads, transparent commands at a get prompt, a form that
    /// waits at the prompt of a command.
    /// </summary>
    public sealed class LispWaitSessionTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-wait-" + Guid.NewGuid().ToString("N")
        );

        public LispWaitSessionTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            WaitingRoutines.EndAll();

            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        private static CadCommandSession WithLineAndCircle()
        {
            CadCommandSession session = Run(Session(Blank()), "LINE", "0,0", "10,0", Enter, "CIRCLE", "0,0", "2");
            session.Lisp.Define("GHCOUNT", 1, 1, a => a[0] is List<string> list ? (object)list.Count : null);
            return session;
        }

        [Fact]
        public void ASelectionAskWithAFilter_CountsOnlyTheObjectsThatMatch()
        {
            // ours: D48, interactive ssget with a filter counts only the objects it keeps (q5: ALL with a CIRCLE filter, 1 found)
            CadCommandSession session = WithLineAndCircle();
            session.Lisp.Define("GHSELECT", 0, 0, a =>
                session.Lisp.Ask(
                    new LispAsk
                    {
                        Kind = LispAskKind.Selection,
                        Accepts = h => session.Document.FindEntity(h) is DxfCircle,
                    }
                )
            );

            Run(session, "(ghcount (ghselect))", "ALL", Enter);

            Assert.Equal(new[] { "Select objects: ALL", "1 found", "Select objects:", "1" }, session.History.Skip(8));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ASingleSelectionAsk_EndsWithTheFirstObjectsTaken()
        {
            CadCommandSession session = WithLineAndCircle();
            session.Lisp.Define("GHONE", 0, 0, a =>
                session.Lisp.Ask(new LispAsk { Kind = LispAskKind.Selection, Single = true })
            );

            Run(session, "(ghcount (ghone))", "ALL");

            Assert.False(session.IsRunning);
            Assert.Equal("2", session.History[session.History.Count - 1]);
        }

        [Fact]
        public void ACommandWithAGetInALoop_IsTheCommandEnterRepeats_AndEscEndsTheLoop()
        {
            CadCommandSession session = Run(
                Session(Blank()),
                "(defun c:ghloop (/ p) (while (setq p (getpoint \"\\nPt: \")) (princ p)) (princ \"done\") (princ))",
                "GHLOOP",
                "1,1",
                Enter
            );

            Assert.False(session.IsRunning);
            Assert.Equal("GHLOOP", session.LastCommandName);
            Assert.Equal("done", session.History[session.History.Count - 1]);

            Run(session, Enter, "3,3", Esc);

            Assert.Equal(
                new[] { "Command: GHLOOP", "Pt: 3,3", "(3.0 3.0 0.0)", "Pt: *Cancel*", "; error: Function cancelled" },
                session.History.Skip(session.History.Count - 5)
            );
        }

        [Fact]
        public void AFileThatAsksWhileItLoads_SaysItIsLoadedWhenTheRoutineIsDone()
        {
            string path = Path.Combine(_folder, "ghask.lsp");
            File.WriteAllText(path, "(setq gp (getpoint \"File: \"))");
            CadCommandSession session = Session(Blank());

            Assert.True(session.LoadLisp(path));
            Assert.True(session.IsRunning);

            Run(session, "4,5");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "File: 4,5", "ghask.lsp successfully loaded." }, session.History);
        }

        [Fact]
        public void ZoomTypedTransparentlyAtAGetPrompt_ReturnsToThePromptOfTheRoutine()
        {
            CadCommandSession session = Run(Session(Blank()), "(getpoint \"P: \")", "'ZOOM");

            // ZOOM asks its prompt over the routine's, as AutoCAD does; Extents ends it, and the question of the routine is asked again.
            Assert.StartsWith(">>Specify corner of window", session.PromptText, StringComparison.Ordinal);
            Run(session, "E");
            Assert.Contains("Resuming LISP command.", session.History);
            Assert.Equal("P: ", session.PromptText);
            Run(session, "1,2");
            Assert.Equal("(1.0 2.0 0.0)", session.History[session.History.Count - 1]);
        }

        [Fact]
        public void EscAtAFormTypedAtLinesPrompt_EndsTheRoutineAndLineKeepsAsking()
        {
            // ours: D37 says nothing of Esc; the routine gets Function cancelled and the command it interrupted goes on
            CadCommandSession session = Run(Session(Blank()), "LINE", "0,0", "(getpoint \"X: \")", Esc);

            Assert.True(session.IsRunning);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Assert.Equal("; error: Function cancelled", session.History[session.History.Count - 1]);
        }

        [Fact]
        public void TheChangesOfOneEvaluation_AreOneUndoStepNamedAfterTheFormOrTheCommand()
        {
            // ours: D42, the changes made outside a command during one evaluation are one step, kept across a wait
            CadCommandSession session = Session(Blank());
            session.Lisp.Define("GHMAKE", 0, 0, a => session.Lisp.OnOwner(() =>
                {
                    session.OpenLispStep();
                    session.Document.ApplyEdit("t", null, scope =>
                        scope.Append(
                            session.Document.ModelSpace,
                            new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.0)
                        )
                    );
                    return null;
                }));

            Run(session, "(progn (ghmake) (ghmake))");
            Assert.Equal(2, All<DxfCircle>(session.Document.ModelSpace).Count);
            Assert.Equal("(Lisp Expression)", session.Document.NextUndo.Name);
            session.Document.Undo();
            Assert.Empty(All<DxfCircle>(session.Document.ModelSpace));

            Run(session, "(defun c:ghmany () (ghmake) (getpoint \"P: \") (ghmake) (princ))", "GHMANY", "1,1");
            Assert.Equal(2, All<DxfCircle>(session.Document.ModelSpace).Count);
            Assert.Equal("GHMANY", session.Document.NextUndo.Name);
            session.Document.Undo();
            Assert.Empty(All<DxfCircle>(session.Document.ModelSpace));

            Run(session, "(progn (ghmake) (getpoint \"Q: \") (ghmake))", Esc);
            Assert.Single(All<DxfCircle>(session.Document.ModelSpace));
            Assert.False(session.IsRunning);
            Assert.True(session.Document.CanUndo);
        }
    }
}
