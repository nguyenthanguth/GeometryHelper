using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// TABLE, -TABLE, TABLEDIT and TABLESTYLE, and the edits of a table's cells, as AutoCAD 2021's: each table made and each
    /// cell written one undo step, the table drawn anew in its block after each.
    /// </summary>
    public class TableCommandTests
    {
        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");

        /// <summary>Answers each request of a name in turn, as the window's dialog would, the request then handled.</summary>
        private static void Answer<T>(CadCommandSession session, string name, params Action<T>[] steps)
        {
            int next = 0;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == name && next < steps.Length)
                {
                    steps[next++]((T)e.Payload);
                    e.Handled = true;
                }
            };
        }

        /// <summary>A schedule of five columns, a title, a header and four rows of data, made by TABLE at the origin, its typing left.</summary>
        private static string Schedule(CadDocument document)
        {
            CadCommandSession session = Session(document);
            Answer<CadInsertTableRequest>(session, "TABLE", r => r.Action = CadDialogAction.Ok);
            Run(session, "TABLE", P(0, 0), Esc);
            return Assert.Single(All<DxfTable>(document.ModelSpace)).Handle;
        }

        [Fact]
        public void TableFromTheDialog_MakesATitleAHeaderAndDataRows_OfStandard_AndOpensTheTitleForTyping()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            Answer<CadInsertTableRequest>(session, "TABLE", r =>
            {
                Assert.Equal("Standard", r.TableStyle);
                Assert.Equal(5, r.Columns);
                Assert.Equal(63.5, r.ColumnWidth);
                r.DataRows = 4;
                r.Action = CadDialogAction.Ok;
            });
            Run(session, "TB");
            Assert.Equal("Specify insertion point:", session.PromptText);
            Run(session, P(100, 200));

            DxfTable table = Assert.Single(All<DxfTable>(document.ModelSpace));
            Assert.Equal(6, table.NumRows);
            Assert.Equal(5, table.NumColumns);
            Assert.Equal(317.5, table.Width, 9);
            Assert.Equal(11.0, table.RowHeight(0), 9);
            Assert.Equal(9.0, table.RowHeight(1), 9);
            Assert.Equal(9.0, table.RowHeight(5), 9);
            Assert.Equal("Standard", table.TableStyleName);
            Assert.Equal(new DxfCellRange(0, 0, 0, 4), table.GetMergeRange(0, 3));
            Assert.Equal("*T1", table.BlockTableRecord.Name);

            CadInPlaceEdit edit = session.Prompt.InPlace;
            Assert.True(edit.IsTableCell);
            Assert.Equal(0, edit.TableRow);
            Assert.Equal(CadInPlaceKind.MText, edit.Kind);
            Assert.Equal(6.0, edit.Height);
            Near(P(100 + 317.5 / 2.0, 200 - 5.5), edit.Location);
            Assert.Equal("Edit table cell A1:", session.PromptText);

            // The title typed, Tab to the header's first cell, typed, then Esc.
            edit.Move = CadTableCellMove.Next;
            Run(session, "STEEL SCHEDULE");
            Assert.Equal(1, session.Prompt.InPlace.TableRow);
            Assert.Equal(0, session.Prompt.InPlace.TableColumn);
            session.Prompt.InPlace.Move = CadTableCellMove.Next;
            Run(session, "Mark");
            Assert.Equal(1, session.Prompt.InPlace.TableColumn);
            Run(session, Esc);
            Assert.False(session.IsRunning);

            table = Assert.Single(All<DxfTable>(document.ModelSpace));
            Assert.Equal("STEEL SCHEDULE", table.TextString(0, 0));
            Assert.Equal("Mark", table.TextString(1, 0));
            Assert.Equal(new[] { "STEEL SCHEDULE", "Mark" }, table.BlockTableRecord.OfType<DxfMText>().Select(m => m.Contents).ToArray());
            AssertReopens(document);

            // Each cell written is a step of its own; the table made one more.
            Assert.True(document.Undo());
            Assert.Equal(string.Empty, Assert.Single(All<DxfTable>(document.ModelSpace)).TextString(1, 0));
            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Empty(All<DxfTable>(document.ModelSpace));
        }

        [Fact]
        public void TableTyped_AsksColumnsRowsAndThePoint_WidthAndStyleChangedFirst()
        {
            CadDocument document = Metric();
            CadCommandSession session = Run(Session(document), "-TABLE", "3", "2", "W", "40", "S", "GH_NONE", P(0, 0));

            Assert.Contains(session.History, line => line.Contains("Current table style: \"Standard\"  Cell width: 63.5  Cell height: 1 line(s)"));
            Assert.Contains(session.History, line => line.Contains("Cannot find table style \"GH_NONE\"."));
            DxfTable table = Assert.Single(All<DxfTable>(document.ModelSpace));
            Assert.Equal(4, table.NumRows);
            Assert.Equal(3, table.NumColumns);
            Assert.Equal(40.0, table.ColumnWidth(2), 9);
            Assert.False(session.IsRunning);
            Assert.Equal("-TABLE", document.NextUndo.Name);
        }

        [Fact]
        public void ATableOfAStyleNotCurrent_IsMadeInIt_AndTheStyleIsMadeCurrent_InADrawingMadeNew_Too()
        {
            CadDocument document = Metric();
            Assert.True(CadTableStyleEditor.Add(document, "GH_SCHEDULE", null).Succeeded);
            Run(Session(document), "-TABLE", "2", "1", "S", "GH_SCHEDULE", P(0, 0));

            DxfTable table = Assert.Single(All<DxfTable>(document.ModelSpace));
            Assert.Equal("GH_SCHEDULE", table.TableStyleName);
            // A drawing made new is given the class DICTIONARYVAR as AutoCAD 2021 lists it, so CTABLESTYLE takes the style.
            Assert.Equal("GH_SCHEDULE", CadTableStyleEditor.Current(document.Database));
        }

        /// <summary>A new drawing of R2013 written without its OBJECTS section, as a program that writes only the tables, blocks and entities saves one.</summary>
        private static CadDocument NoObjects()
        {
            string text = new DxfDatabase("AC1027").ToText().Replace("\r\n", "\n");
            int objects = text.IndexOf("  0\nSECTION\n  2\nOBJECTS\n", StringComparison.Ordinal);
            int end = text.IndexOf("  0\nENDSEC\n", objects, StringComparison.Ordinal) + "  0\nENDSEC\n".Length;
            CadDocument document = CadDocument.From(DxfDatabase.Parse(text.Substring(0, objects) + text.Substring(end)), "noobjects.dxf");
            Assert.Null(document.Database.NamedObjectsDictionary);
            return document;
        }

        [Fact]
        public void ATableStyleAdded_OnADrawingWithNoObjectsSection_MakesTheNamedObjectDictionary_AsAutoCadOpensOne()
        {
            CadDocument document = NoObjects();

            Assert.True(CadTableStyleEditor.AddStandard(document).Succeeded);
            Assert.True(CadTableStyleEditor.Add(document, "GH_SCHEDULE", "Standard").Succeeded);

            Assert.Equal(new[] { "GH_SCHEDULE", "Standard" }, CadTableStyleEditor.Styles(document.Database).OrderBy(n => n, StringComparer.Ordinal));
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Null(document.Database.TableStyleDictionary);
        }

        [Fact]
        public void TableTyped_OnADrawingWithNoObjectsSection_MakesStandardInANewNamedObjectDictionary()
        {
            CadDocument document = NoObjects();
            Run(Session(document), "-TABLE", "2", "1", P(0, 0));

            DxfTable table = Assert.Single(All<DxfTable>(document.ModelSpace));
            Assert.Equal("Standard", table.TableStyleName);
            Assert.NotNull(CadTableStyleEditor.Find(document.Database, "Standard"));
            AssertReopens(document);
        }

        [Fact]
        public void TableEdit_PicksACell_AndEnterGoesDown_ShiftTabBack()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            CadCommandSession session = Run(Session(document), "TABLEDIT", P(63.5 + 10, -11 - 9 - 2));

            CadInPlaceEdit edit = session.Prompt.InPlace;
            Assert.Equal(2, edit.TableRow);
            Assert.Equal(1, edit.TableColumn);
            edit.Move = CadTableCellMove.Down;
            Run(session, "HEA300");
            Assert.Equal(3, session.Prompt.InPlace.TableRow);
            session.Prompt.InPlace.Move = CadTableCellMove.Previous;
            Run(session, "PL20*300");
            Assert.Equal(3, session.Prompt.InPlace.TableRow);
            Assert.Equal(0, session.Prompt.InPlace.TableColumn);
            Run(session, Enter);
            Assert.False(session.IsRunning);

            var table = Entity<DxfTable>(document, handle);
            Assert.Equal("HEA300", table.TextString(2, 1));
            Assert.Equal("PL20*300", table.TextString(3, 1));
        }

        [Fact]
        public void TheCellEdits_AreEachOneUndoStep_AndDrawTheTableAnew()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            CadTableEditor.SetText(document, handle, 2, 0, "b1");

            Assert.True(CadTableEditor.InsertRow(document, handle, 2, true).Succeeded);
            Assert.Equal(7, Entity<DxfTable>(document, handle).NumRows);
            Assert.Equal("TABLEDIT", document.NextUndo.Name);
            Assert.True(CadTableEditor.InsertColumn(document, handle, 0, false).Succeeded);
            Assert.Equal(6, Entity<DxfTable>(document, handle).NumColumns);
            Assert.Equal("b1", Entity<DxfTable>(document, handle).TextString(2, 1));

            Assert.True(CadTableEditor.Merge(document, handle, new DxfCellRange(4, 1, 5, 2)).Succeeded);
            Assert.True(Entity<DxfTable>(document, handle).IsMergedCell(5, 2, out DxfCellRange merged));
            Assert.Equal(new DxfCellRange(4, 1, 5, 2), merged);
            Assert.True(CadTableEditor.Unmerge(document, handle, new DxfCellRange(5, 2, 5, 2)).Succeeded);
            Assert.False(Entity<DxfTable>(document, handle).IsMergedCell(5, 2, out _));

            Assert.True(CadTableEditor.SetAlignment(document, handle, new DxfCellRange(2, 1, 2, 1), DxfCellAlignment.MiddleLeft).Succeeded);
            DxfMText b1 = Entity<DxfTable>(document, handle).BlockTableRecord.OfType<DxfMText>().Single();
            Assert.Equal(DxfAttachmentPoint.MiddleLeft, b1.Attachment);
            Assert.Equal(63.5 + 1.5, b1.Location.X, 9);

            Assert.True(CadTableEditor.SetBackground(document, handle, new DxfCellRange(1, 0, 1, 5), DxfColor.FromColorIndex(DxfColorMethod.ByAci, 8)).Succeeded);
            Assert.Equal(6, Entity<DxfTable>(document, handle).BlockTableRecord.OfType<DxfSolid>().Count());

            int lines = Entity<DxfTable>(document, handle).BlockTableRecord.OfType<DxfLine>().Count();
            Assert.True(CadTableEditor.SetBorders(document, handle, new DxfCellRange(0, 0, 6, 5), DxfGridLineType.AllGridLines, false).Succeeded);
            Assert.Empty(Entity<DxfTable>(document, handle).BlockTableRecord.OfType<DxfLine>());
            Assert.True(CadTableEditor.SetBorders(document, handle, new DxfCellRange(0, 0, 6, 5), DxfGridLineType.OuterGridLines, true).Succeeded);
            Assert.Equal(4, Entity<DxfTable>(document, handle).BlockTableRecord.OfType<DxfLine>().Count());
            Assert.True(lines > 4);

            Assert.True(CadTableEditor.ClearContents(document, handle, new DxfCellRange(2, 1, 2, 1)).Succeeded);
            Assert.Empty(Entity<DxfTable>(document, handle).BlockTableRecord.OfType<DxfMText>());
            Assert.True(CadTableEditor.DeleteRows(document, handle, 2, 1).Succeeded);
            Assert.True(CadTableEditor.DeleteColumns(document, handle, 0, 1).Succeeded);
            Assert.Equal(6, Entity<DxfTable>(document, handle).NumRows);
            Assert.Equal(5, Entity<DxfTable>(document, handle).NumColumns);
            Assert.False(CadTableEditor.DeleteRows(document, handle, 0, 6).Succeeded);
            AssertReopens(document);

            // Every edit one step: undone one by one back to the table as made.
            for (int k = 0; k < 12; k++)
            {
                Assert.Equal("TABLEDIT", document.NextUndo.Name);
                Assert.True(document.Undo());
            }

            Assert.Equal("TABLE", document.NextUndo.Name);
            Assert.Equal(6, Entity<DxfTable>(document, handle).NumRows);
            Assert.Empty(Entity<DxfTable>(document, handle).BlockTableRecord.OfType<DxfMText>());
        }

        [Fact]
        public void ACellEdit_IsRefusedByTheGate_AndNothingIsWritten()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            string before = document.Database.ToText();
            document.EditGate = new Refusing();

            CadEditResult result = CadTableEditor.SetText(document, handle, 2, 0, "b1");

            Assert.True(result.Refused);
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void FindAndQuickSelect_FindTheCellsOfATableMade_AndReplaceDrawsItAnew()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            CadTableEditor.SetText(document, handle, 2, 2, "HEA300");
            CadTableEditor.SetText(document, handle, 3, 2, "PL20*300");

            IReadOnlyList<CadFindResult> found = CadFind.Find(document, "HEA", new CadFindOptions(), CadFindScope.EntireDrawing);
            CadFindResult hit = Assert.Single(found);
            Assert.Equal("Table cell", hit.ObjectType);
            Assert.Equal(handle, hit.Handle);

            Assert.True(CadFind.Replace(document, hit, "HEA", "HEB", new CadFindOptions()).Succeeded);
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal("HEB300", table.TextString(2, 2));
            Assert.Contains(table.BlockTableRecord.OfType<DxfMText>(), m => m.Contents == "HEB300");

            var quick = new CadQuickSelect(document);
            Assert.Contains("Table", quick.ObjectTypes);
            Assert.Equal(new[] { handle }, quick.Match("Table", "Layer", CadQuickSelectOperator.EqualTo, "0", true, Tolerance.Default).ToArray());
        }

        [Fact]
        public void TableStyles_AreMadeChangedAndDeleted_AndAStyleChangedDrawsItsTablesAnew()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            CadTableEditor.SetText(document, handle, 2, 0, "b1");

            Assert.True(CadTableStyleEditor.Add(document, "GH_SCHEDULE", "Standard").Succeeded);
            Assert.Equal(new[] { "GH_SCHEDULE", "Standard" }, CadTableStyleEditor.Styles(document.Database).ToArray());
            Assert.False(CadTableStyleEditor.Add(document, "gh_schedule", null).Succeeded);
            Assert.Equal("The Standard table style cannot be deleted.", CadTableStyleEditor.WhyNotDeleted(document.Database, "Standard"));
            Assert.Null(CadTableStyleEditor.WhyNotDeleted(document.Database, "GH_SCHEDULE"));
            Assert.Equal(1, CadTableStyleEditor.CountUses(document.Database, "Standard"));

            CadTableStyleSettings settings = CadTableStyleSettings.Of(CadTableStyleEditor.Find(document.Database, "Standard"));
            settings.Data.TextHeight = 3.5;
            settings.Data.Alignment = DxfCellAlignment.MiddleLeft;
            Assert.True(CadTableStyleEditor.Apply(document, "Standard", settings).Succeeded);

            DxfMText b1 = Entity<DxfTable>(document, handle).BlockTableRecord.OfType<DxfMText>().Single();
            Assert.Equal(3.5, b1.TextHeight);
            Assert.Equal(DxfAttachmentPoint.MiddleLeft, b1.Attachment);
            Assert.True(CadTableStyleEditor.Apply(document, "Standard", settings).Succeeded);
            Assert.Equal("TABLESTYLE", document.NextUndo.Name);

            Assert.True(CadTableStyleEditor.Delete(document, "GH_SCHEDULE").Succeeded);
            Assert.Equal(new[] { "Standard" }, CadTableStyleEditor.Styles(document.Database).ToArray());
            AssertReopens(document);
        }

        [Fact]
        public void ATableTurned_IsDrawnWhereItStands()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Run(Session(document), "ROTATE", new[] { handle }, Enter, P(0, 0), "90");

            // Turned a quarter about its corner, the table runs up the drawing from the origin, 317.5 long, its rows to the right.
            DisplayBox box = document.GetDisplay(document.ModelSpace).Bounds;
            Assert.Equal(0.0, box.MinY, 6);
            Assert.Equal(317.5, box.MaxY, 6);
            Assert.Equal(56.0, box.MaxX, 6);
            Assert.Equal(0.0, box.MinX, 6);
        }

        [Fact]
        public void ACellOfATableOfAHundredRowsOfTwentyColumns_IsWrittenAndDrawnWellUnderAHundredMilliseconds()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            Answer<CadInsertTableRequest>(session, "TABLE", r =>
            {
                r.Columns = 20;
                r.DataRows = 98;
                r.ColumnWidth = 30;
                r.Action = CadDialogAction.Ok;
            });
            Run(session, "TABLE", P(0, 0), Esc);
            string handle = Assert.Single(All<DxfTable>(document.ModelSpace)).Handle;

            for (int r = 1; r < 100; r++)
            {
                Entity<DxfTable>(document, handle).SetTextString(r, r % 20, "R" + r);
            }

            var watch = new Stopwatch();
            long best = long.MaxValue;

            for (int k = 0; k < 4; k++)
            {
                watch.Restart();
                Assert.True(CadTableEditor.SetText(document, handle, 50 + k, 5, "HEA" + k).Succeeded);
                DisplayList2 display = document.GetDisplay(document.ModelSpace);
                watch.Stop();
                Assert.True(display.Count > 100);
                best = Math.Min(best, watch.ElapsedMilliseconds);
            }

            Assert.True(best < 1000, "An edit and its display took " + best + " ms.");
            Console.WriteLine("TIMING edit+display " + best + " ms");
        }

        private sealed class Refusing : ICadEditGate
        {
            public bool Allows(CadDocument document, DxfEntity entity, CadEditRecord record, out string reason)
            {
                reason = "The table belongs to a plugin.";
                return false;
            }
        }
    }
}
