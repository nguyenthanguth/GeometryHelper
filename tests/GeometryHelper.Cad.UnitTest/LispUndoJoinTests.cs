using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The undo of forms typed one after the other at the Command prompt (D250, part F1 of L4): the changes of the forms typed
    /// with no command between them are one <c>U</c>, as the full AutoCAD 2021 gave it (probe g3), and no undo group stays
    /// open between the forms, so that autosave, the remote server and the block editor see a drawing at rest and an edit of
    /// the Properties palette is a step of its own.
    /// </summary>
    public sealed class LispUndoJoinTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        // A blank drawing with one line a2 on layer 0 and the layer Steel, the ActiveX functions loaded, the line as the
        // VLA-OBJECT o and the document as doc.
        private static CadCommandSession Loaded()
        {
            CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf");
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel", ColorIndex = 1 });
            CadCommandSession session = Session(document);
            Feed(session, "(vl-load-com)");
            Feed(session, "(entmake '((0 . \"LINE\") (8 . \"0\") (10 0.0 0.0 0.0) (11 0.0 50.0 0.0)))");
            Feed(session, "(setq a2 (entlast))");
            Feed(session, "(setq o (vlax-ename->vla-object a2) doc (vla-get-ActiveDocument (vlax-get-acad-object)))");

            // The forms typed above are one step with the puts the test types next (AutoCAD 2021, feed undo A: typed changes with no
            // command between them are one U); a command between them parts the two.
            Run(session, "REGEN");
            return session;
        }

        // Types a form and gives what it wrote after the echo.
        private static string Feed(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form.StartsWith("(", StringComparison.Ordinal) ? "!" + form : form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        private static string Color(CadCommandSession session) => Feed(session, "(vla-get-Color o)");

        private static string Handle(CadCommandSession session) =>
            Feed(session, "(cdr (assoc 5 (entget a2)))").Trim('"');

        private static void Put(CadCommandSession session, int color) =>
            Assert.Equal("nil", Feed(session, "(vla-put-Color o " + color + ")"));

        [Fact]
        public void TwoTypedPuts_OneUndoTakesBoth()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g3 A): (vla-put-Color OU 3) and (vla-put-Color OU 4) typed one after the other, one U gives
            // the color before both.
            Put(session, 3);
            Put(session, 4);
            Assert.Equal("4", Color(session));
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void ThreeTypedPuts_OneUndoTakesAllThree()
        {
            CadCommandSession session = Loaded();

            // ours: D250, the joined steps chain: each joins the one before it.
            Put(session, 3);
            Put(session, 4);
            Put(session, 5);
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void ARegenBetweenTwoTypedPuts_MakesThemTwoUndos()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g3 B): REGEN, put, REGEN, then U takes only the REGEN and a second U the put. Ours: REGEN is
            // no step of the drawing, so the first U takes the put after it; the put before it is the second.
            Put(session, 3);
            Run(session, "REGEN");
            Put(session, 4);
            Run(session, "U");
            Assert.Equal("3", Color(session));
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void APutAfterACommand_IsAStepOfItsOwn()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g3 D): REGEN, put 2, U gives the color before the put.
            Run(session, "REGEN");
            Put(session, 2);
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void ADrawingCommandBetweenTwoTypedPuts_TakesTheLaterPutIntoItsStep()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (feed undo G, rule 2; D250 had three steps here): a command that made a step separates the typed forms
            // before it from those after it, and the forms after it are part of its step: the first U takes the circle and the
            // put after it, the second the put before it.
            Put(session, 3);
            Run(session, "CIRCLE", "50,50", "5");
            Put(session, 4);
            Run(session, "U");
            Assert.Equal("3", Color(session));
            Assert.Null(session.Document.Database.ModelSpace.OfType<DxfCircle>().FirstOrDefault());
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void ARoutineThatIsACommand_BetweenTwoTypedPuts_IsAStepOfItsOwn()
        {
            CadCommandSession session = Loaded();
            Feed(session, "(defun c:ghp () (vla-put-Color o 5) (vla-put-Color o 6) (princ))");

            // AutoCAD 2021 (GUI g2): a c: command's two puts are one step; ours: D250 it does not join the typed puts around it.
            Put(session, 3);
            Run(session, "GHP");
            Put(session, 4);
            Run(session, "U");
            Assert.Equal("6", Color(session));
            Run(session, "U");
            Assert.Equal("3", Color(session));
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void AFormThatChangesNothing_BetweenTwoTypedPuts_DoesNotSeparateThem()
        {
            CadCommandSession session = Loaded();

            // ours: D250, a form with no change is no command: the puts around it are still one U.
            Put(session, 3);
            Assert.Equal("3", Feed(session, "(+ 1 2)"));
            Put(session, 4);
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void UndoMarks_AroundTypedPuts_AreOneUndo()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): StartUndoMark, two puts, EndUndoMark, then U returned to the color before the marks.
            Put(session, 2);
            Run(session, "REGEN");
            Feed(session, "(vla-StartUndoMark doc)");
            Put(session, 7);
            Put(session, 8);
            Feed(session, "(vla-EndUndoMark doc)");
            Assert.False(session.Document.IsInGroup);
            Run(session, "U");
            Assert.Equal("2", Color(session));
        }

        [Fact]
        public void AfterAnUndo_ARedoPutsBothTypedPutsBack()
        {
            CadCommandSession session = Loaded();

            // ours: D250, the joined steps are put again together as they were taken back.
            Put(session, 3);
            Put(session, 4);
            Run(session, "U");
            Assert.Equal("256", Color(session));
            Run(session, "REDO");
            Assert.Equal("4", Color(session));
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void APutAfterAnUndo_JoinsTheStepThatIsLastAgain()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (feed undo, sections B to G; D250 kept it apart): U makes no step of its own, so the form typed after
            // it joins the step that is last again, and the next U takes both.
            Put(session, 3);
            Run(session, "REGEN");
            Put(session, 4);
            Run(session, "U");
            Assert.Equal("3", Color(session));
            Put(session, 5);
            Assert.Equal("5", Color(session));
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void TwoTypedEntmods_OneUndoTakesBoth()
        {
            CadCommandSession session = Loaded();
            string colorOf = "(cdr (assoc 62 (entget a2)))";
            string layerOf = "(cdr (assoc 8 (entget a2)))";

            // AutoCAD 2021 (GUI g3 C): two entmods typed one after the other, one U takes both.
            Feed(session, "(entmod (append (entget a2) '((62 . 7))))");
            Feed(session, "(entmod (subst '(8 . \"Steel\") (assoc 8 (entget a2)) (entget a2)))");
            Assert.Equal("7", Feed(session, colorOf));
            Assert.Equal("\"Steel\"", Feed(session, layerOf));
            Run(session, "U");
            Assert.Equal("nil", Feed(session, colorOf));
            Assert.Equal("\"0\"", Feed(session, layerOf));
        }

        [Fact]
        public void ATypedPutThenATypedEntmod_AreOneUndo()
        {
            CadCommandSession session = Loaded();

            // ours: D250, whoever made the change, the typed forms are one U.
            Put(session, 3);
            Feed(session, "(entmod (append (entget a2) '((62 . 8))))");
            Run(session, "U");
            Assert.Equal("256", Color(session));
        }

        [Fact]
        public void AfterEveryTypedForm_NoUndoGroupIsOpen()
        {
            CadCommandSession session = Loaded();
            CadDocument document = session.Document;

            // ours: D250, a closed step per form (red against the group the hold kept open): autosave, the remote server's commit
            // and the block editor's HasUnsavedChanges read IsInGroup.
            Put(session, 3);
            Assert.False(document.IsInGroup);
            Assert.True(document.CanUndo);
            Feed(session, "(entmod (append (entget a2) '((62 . 8))))");
            Assert.False(document.IsInGroup);
            Feed(session, "(entmake '((0 . \"LINE\") (8 . \"0\") (10 0.0 0.0 0.0) (11 5.0 5.0 0.0)))");
            Assert.False(document.IsInGroup);
            Feed(session, "(vla-Delete (vlax-ename->vla-object (entlast)))");
            Assert.False(document.IsInGroup);
            Feed(session, "(vla-put-ActiveLayer doc (vla-Item (vla-get-Layers doc) \"Steel\"))");
            Assert.False(document.IsInGroup);
            Assert.True(document.CanUndo);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void APropertiesEditAfterATypedPut_IsAStepOfItsOwn()
        {
            CadCommandSession session = Loaded();
            CadDocument document = session.Document;
            string handle = Handle(session);

            // ours: D250, with the group closed the edit does not join the routine's step, so U takes the edit and leaves the put.
            Put(session, 3);
            Assert.True(
                document
                    .ApplyEdit(
                        "Properties",
                        new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle) },
                        s => s.Document.FindEntity(handle).Layer = "Steel"
                    )
                    .Succeeded
            );
            Assert.Equal("Properties", document.NextUndo.Name);
            Run(session, "U");
            Assert.Equal("0", document.FindEntity(handle).Layer);
            Assert.Equal("3", Color(session));
        }

        [Fact]
        public void APutAfterAPropertiesEdit_IsNotJoinedToTheEdit()
        {
            CadCommandSession session = Loaded();
            CadDocument document = session.Document;
            string handle = Handle(session);

            // ours: D250, only a step of a typed form is joined by the next one: here the edit of the palette is in between.
            Put(session, 3);
            document.ApplyEdit(
                "Properties",
                new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle) },
                s => s.Document.FindEntity(handle).Layer = "Steel"
            );
            Put(session, 4);
            Run(session, "U");
            Assert.Equal("Steel", document.FindEntity(handle).Layer);
            Assert.Equal("3", Color(session));
        }

        [Fact]
        public void TheJoinedStepsOfTypedForms_KeepTheNameOfTheFirst()
        {
            CadCommandSession session = Loaded();

            // ours: D250, a joined step carries the name of the step it joins (as CommitView's), so UNDO names the first form's.
            Put(session, 3);
            Put(session, 4);
            Assert.Equal("(Lisp Expression)", session.Document.NextUndo.Name);
            Assert.True(session.Document.CanUndo);
        }

        [Fact]
        public void ATypedFormWaitingForAPoint_ThenAnotherTypedPut_AreOneUndo()
        {
            CadCommandSession session = Loaded();

            // ours: D250, a form that asks the user (getpoint) is a typed form too: its changes join the step of the form before.
            Put(session, 3);
            Feed(session, "(progn (getpoint \"Pick: \") (vla-put-Color o 4))");
            Run(session, "1,1");
            Assert.Equal("4", Color(session));
            Run(session, "U");
            Assert.Equal("256", Color(session));
            Assert.False(session.IsRunning);
        }
    }
}
