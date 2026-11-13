using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The commands of round 2 that open a dialog, driven as the window's dialog would answer them: RENAME with the Rename
    /// dialog, SPELL with Check Spelling and BOUNDARY with Boundary Creation; each with no window keeps its command-line form.
    /// </summary>
    public class DialogsRoundTwoTests
    {
        private static readonly string Folder = TestRun.Folder("dialogs4");

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>Answers the request of a name with a step, as the window's dialog would, the request then handled.</summary>
        private static void Answer<T>(CadCommandSession session, string name, Action<T> step)
        {
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == name)
                {
                    step((T)e.Payload);
                    e.Handled = true;
                }
            };
        }

        /// <summary>A text T1 "Helo wrold", a text T2 "Helo again" on layer Steel and a text T3 "Teh end" on the locked layer.</summary>
        private static CadDocument Texts()
        {
            DxfDatabase database = TestDrawings.Drawing(
                TestDrawings.Layer("Steel", 1) + TestDrawings.Layer("Locked", 3),
                null,
                TestDrawings.Text(0, 0, 5, "Helo wrold", handle: "T1"),
                TestDrawings.Text(0, 20, 5, "Helo again", "Steel", handle: "T2"),
                TestDrawings.Text(0, 40, 5, "Teh end", "Locked", handle: "T3")
            );
            database.LayerTable["Locked"].IsLocked = true;
            return CadDocument.From(database, "texts.dxf");
        }

        #region RENAME

        [Fact]
        public void Rename_ListsTheKindsOfAutoCad2021sDialog_AndOnlyTheNamesThatCanBeRenamed()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            CadRenameRequest asked = null;
            Answer<CadRenameRequest>(session, "RENAME", r => asked = r);

            Run(session, "REN");

            Assert.NotNull(asked);
            Assert.Equal(
                new[]
                {
                    "Blocks",
                    "Dimension styles",
                    "Layers",
                    "Linetypes",
                    "Multileader styles",
                    "Table styles",
                    "Text styles",
                    "UCSs",
                    "Viewports",
                    "Views",
                },
                asked.Kinds
            );
            Assert.Equal(new[] { "BOX" }, asked.NamesOf("Blocks"));
            Assert.Equal(new[] { "Locked", "Steel" }, asked.NamesOf("Layers"));
            Assert.DoesNotContain("Standard", asked.NamesOf("Text styles"));
            Assert.Empty(asked.NamesOf("Nothing"));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Rename_TheDialogsRenames_AreMadeInOrderOnOk_AsOneUndoStep()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Answer<CadRenameRequest>(session, "RENAME", r =>
            {
                r.Renames.Add(new CadRename("Layers", "Steel", "S-BEAM"));
                r.Renames.Add(new CadRename("Blocks", "BOX", "CRATE"));
                r.IsAccepted = true;
            });

            Run(session, "RENAME");

            Assert.Equal("S-BEAM", Entity<DxfLine>(document, "A1").Layer);
            Assert.Equal("CRATE", Entity<DxfBlockReference>(document, "A7").Name);
            Assert.Equal("RENAME", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal("Steel", Entity<DxfLine>(document, "A1").Layer);
            Assert.Equal("BOX", Entity<DxfBlockReference>(document, "A7").Name);
        }

        [Fact]
        public void Rename_ARefusedRenameIsWritten_AndCancelRenamesNothing()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            bool accept = true;
            Answer<CadRenameRequest>(session, "RENAME", r =>
            {
                r.Renames.Add(new CadRename("Layers", "Steel", "Locked"));
                r.Renames.Add(new CadRename("Blocks", "BOX", "CRATE"));
                r.IsAccepted = accept;
            });

            Run(session, "RENAME");

            Assert.Contains("A layer named \"Locked\" already exists.", session.History);
            Assert.Equal("CRATE", Entity<DxfBlockReference>(document, "A7").Name);

            accept = false;
            Run(session, "RENAME");
            Assert.True(document.Database.BlockTable.Has("CRATE"));
            Assert.Equal("Steel", Entity<DxfLine>(document, "A1").Layer);
        }

        #endregion

        #region SPELL

        [Fact]
        public void Spell_TheDialogsAnswers_ChangeTheWordsOnceItCloses_InOneUndoStep()
        {
            CadDocument document = Texts();
            CadCommandSession session = Session(document);
            var seen = new List<string>();
            Answer<CadSpellCheck>(session, "SPELL", check =>
            {
                Assert.Equal(CadSpellCheck.EntireDrawing, check.Where);
                Assert.True(check.Start());
                seen.Add(check.Word);
                Assert.Equal("Helo wrold", check.Context);
                Assert.Equal("Hello", check.Suggestions[0]);
                Assert.StartsWith("1 was on a locked layer.", check.Status);

                // Change All takes the second Helo with it; nothing is written while the dialog is open.
                Assert.True(check.ChangeAll(""));
                Assert.Equal("Helo wrold", Entity<DxfDBText>(document, "T1").TextString);
                seen.Add(check.Word);
                Assert.True(check.Change("world"));
                Assert.False(check.IsChecking);
                Assert.EndsWith("Spelling check complete.", check.Status);
                Assert.Equal(3, check.ChangedCount);
            });

            Run(session, "SPELL");

            Assert.Equal(new[] { "Helo", "wrold" }, seen);
            Assert.Equal("Hello world", Entity<DxfDBText>(document, "T1").TextString);
            Assert.Equal("Hello again", Entity<DxfDBText>(document, "T2").TextString);
            Assert.Equal("Teh end", Entity<DxfDBText>(document, "T3").TextString);
            Assert.False(session.IsRunning);
            Assert.Equal("SPELL", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal("Helo wrold", Entity<DxfDBText>(document, "T1").TextString);
            Assert.Equal("Helo again", Entity<DxfDBText>(document, "T2").TextString);
        }

        [Fact]
        public void Spell_IgnoreAllAndAddToDictionary_PassTheWordFromThenOn_AndTheWordIsAppendedToTheFile()
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "custom-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, "steel\n");
            CadDocument document = Texts();
            CadCommandSession session = Session(document);
            Answer<CadSpellCheck>(session, "SPELL", check =>
            {
                Assert.Null(check.LoadDictionary(path));
                Assert.Equal("Cannot read custom dictionary.", check.LoadDictionary(Path.Combine(Folder, "no.txt")));
                Assert.Equal(path, check.CustomDictionary);
                check.Start();
                Assert.Equal("Helo", check.Word);
                check.AddToDictionary();
                Assert.Equal("wrold", check.Word);
                check.IgnoreAll();
                Assert.False(check.IsChecking);
                Assert.Equal(new[] { "Helo" }, check.Added);
            });

            Run(session, "SPELL");

            Assert.Equal(new[] { "steel", "Helo" }, File.ReadAllLines(path));
            Assert.Equal("Helo wrold", Entity<DxfDBText>(document, "T1").TextString);
        }

        [Fact]
        public void Spell_SelectObjects_AsksForThem_AndShowsTheDialogAgainCheckingThoseOnly()
        {
            CadDocument document = Texts();
            CadCommandSession session = Session(document);
            int shown = 0;
            Answer<CadSpellCheck>(session, "SPELL", check =>
            {
                shown++;

                if (shown == 1)
                {
                    check.IsSelecting = true;
                    return;
                }

                Assert.Equal(CadSpellCheck.SelectedObjects, check.Where);
                Assert.True(check.Start());
                Assert.Equal("Helo again", check.Context);
                check.Change("Hello");
                Assert.False(check.IsChecking);
            });

            Run(session, "SPELL");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { "T2" }, Enter);

            Assert.Equal(2, shown);
            Assert.Equal("Hello again", Entity<DxfDBText>(document, "T2").TextString);
            Assert.Equal("Helo wrold", Entity<DxfDBText>(document, "T1").TextString);
        }

        [Fact]
        public void Spell_ChangeWithNothingTypedAndNoSuggestion_KeepsTheWordWaiting()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Text(0, 0, 5, "qzxv", handle: "T1")),
                "q.dxf"
            );
            CadCommandSession session = Session(document);
            Answer<CadSpellCheck>(session, "SPELL", check =>
            {
                check.Where = CadSpellCheck.CurrentSpace;
                Assert.True(check.Start());
                Assert.Empty(check.Suggestions);
                Assert.False(check.Change(" "));
                Assert.Equal("No suggestions.", check.Status);
                Assert.True(check.IsChecking);
                check.Where = "Nowhere";
                Assert.Equal(CadSpellCheck.EntireDrawing, check.Where);
            });

            Run(session, "SPELL");

            Assert.Equal("qzxv", Entity<DxfDBText>(document, "T1").TextString);
            Assert.Null(document.NextUndo);
        }

        #endregion

        #region LWDEFAULT

        [Fact]
        public void DefaultLineWeight_DrawsTheLinesOfDefaultAndOfALayerOfDefault_AtItsWidthAfterARegen()
        {
            CadDocument document = Plan();
            DxfLine own = Entity<DxfLine>(document, "A2");
            own.LineWeight = DxfLineWeight.ByLineWeightDefault;
            document.Database.LayerTable["Steel"].LineWeight = DxfLineWeight.ByLineWeightDefault;

            double Width(string handle) =>
                document.GetDisplay(document.ModelSpace).Items.First(i => i.Handle == handle).Style.LineWeight;

            Assert.Equal(0.25, Width("A2"), 6);
            Assert.Equal(0.25, Width("A1"), 6);

            document.DefaultLineWeight = 0.5;
            document.Regen();

            Assert.Equal(0.5, Width("A2"), 6);
            Assert.Equal(0.5, Width("A1"), 6);
        }

        #endregion

        #region BOUNDARY

        /// <summary>A square of 100 at the origin with a square of 60 inside it, as closed polylines.</summary>
        private static CadDocument Squares() =>
            CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Polyline("0", "S1", true, 0, 0, 0, 100, 0, 0, 100, 100, 0, 0, 100, 0),
                    TestDrawings.Polyline("0", "S2", true, 20, 20, 0, 80, 20, 0, 80, 80, 0, 20, 80, 0)
                ),
                "squares.dxf"
            );

        [Fact]
        public void Boundary_PickPointsWithIslandDetectionOff_KeepsTheSetting_AndBoundsTheOuterAreaAlone()
        {
            CadDocument document = Squares();
            CadCommandSession session = Session(document);
            var seen = new List<bool>();
            Answer<CadBoundaryRequest>(session, "BOUNDARY", r =>
            {
                seen.Add(r.IslandDetection);
                Assert.False(r.IsRegion);
                r.IslandDetection = false;
                r.IsPicking = true;
            });
            session.Tolerance = Tolerance.Default;

            Run(session, "BO");
            Assert.Equal("Pick internal point:", session.PromptText);
            Run(session, P(10, 10), Enter);

            Assert.Contains("BOUNDARY created 1 polyline", session.History);

            // -BOUNDARY shares the setting: its question offers No.
            Run(session, "-BOUNDARY", "A", "I");
            Assert.Equal("Do you want island detection? [Yes/No] <N>:", session.PromptText);
            Run(session, Esc);
            Run(session, "BOUNDARY");
            Assert.Equal(new[] { true, false }, seen);
        }

        [Fact]
        public void Boundary_CancelInTheDialog_EndsTheCommandAndKeepsNothing()
        {
            CadDocument document = Squares();
            CadCommandSession session = Session(document);
            Answer<CadBoundaryRequest>(session, "BOUNDARY", r => r.IslandDetection = false);

            Run(session, "BOUNDARY");

            Assert.False(session.IsRunning);
            Run(session, "-BOUNDARY", "A", "I");
            Assert.Equal("Do you want island detection? [Yes/No] <Y>:", session.PromptText);
        }

        [Fact]
        public void Boundary_WithNoWindow_AsksThePointsAtOnce()
        {
            Assert.Equal("Pick internal point:", Start(Squares(), "BOUNDARY").PromptText);
        }

        #endregion
    }
}
