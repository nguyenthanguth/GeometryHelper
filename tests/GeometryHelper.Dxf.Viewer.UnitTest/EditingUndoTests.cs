using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingUndoTests
    {
        private static CadEditResult MoveA1(CadDocument document, double dx) =>
            document.ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, "A1") }, s =>
                s.Document.FindEntity("A1").TransformBy(GeoTransform3.Translation(new GeoVector3(dx, 0, 0)))
            );

        [Fact]
        public void AnEditIsOneUndoStep_AndUndoingItGivesTheDrawingBackByteForByte()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();
            Assert.False(document.IsModified);

            CadEditResult result = MoveA1(document, 25);
            string moved = document.Database.ToText();

            Assert.True(result.Succeeded);
            Assert.NotEqual(start, moved);
            Assert.True(document.IsModified);
            Assert.Equal("MOVE", document.NextUndo.Name);
            Near(new GeoPoint3(125, 0, 0), Entity<DxfLine>(document, "A1").EndPoint);

            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.IsModified);
            Near(new GeoPoint3(100, 0, 0), Entity<DxfLine>(document, "A1").EndPoint);

            Assert.True(document.Redo());
            Assert.Equal(moved, document.Database.ToText());
            Assert.False(document.CanRedo);
        }

        [Fact]
        public void UndoingEveryStepOfAddsErasesAndTableChanges_ReturnsTheStartText()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();

            document.ApplyEdit("LINE", null, s =>
                s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(5, 5, 0), new GeoPoint3(50, 60, 0)))
            );
            document.ApplyEdit("ERASE", new[] { new CadEditRecord(CadEditRecordKind.Erased, "A2") }, s =>
                s.Document.FindEntity("A2").Erase()
            );
            CadPropertyEditor.Set(document, new[] { "A3" }, "Radius", "25");
            CadLayerEditor.Add(document, "Walls");
            CadLayerEditor.SetCurrent(document, "Walls");
            Assert.NotEqual(start, document.Database.ToText());

            int steps = 0;

            while (document.Undo())
            {
                steps++;
            }

            Assert.Equal(5, steps);
            Assert.Equal(start, document.Database.ToText());
            Assert.NotNull(document.FindEntity("A2"));
            Assert.False(document.Database.LayerTable.Has("Walls"));
        }

        [Fact]
        public void TheGateIsAskedBeforeAnythingIsWritten_AndItsRefusalLeavesTheDrawingAsItWas()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();
            var gate = new RefusingGate("A1");
            document.EditGate = gate;

            CadEditResult result = document.ApplyEdit(
                "MOVE",
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.Transformed, "A2"),
                    new CadEditRecord(CadEditRecordKind.Transformed, "A1"),
                },
                s =>
                {
                    s.Document.FindEntity("A2").TransformBy(GeoTransform3.Translation(new GeoVector3(1, 0, 0)));
                    s.Document.FindEntity("A1").TransformBy(GeoTransform3.Translation(new GeoVector3(1, 0, 0)));
                }
            );

            Assert.True(result.Refused);
            Assert.Equal("This line belongs to the model", result.Message);
            Assert.Equal(new[] { "A2", "A1" }, gate.Asked);
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
            Assert.False(document.IsModified);
        }

        [Fact]
        public void AnEditThatThrowsPartWay_IsTakenBackWhole()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();

            CadEditResult result = document.ApplyEdit(
                "ERASE",
                new[] { new CadEditRecord(CadEditRecordKind.Erased, "A1") },
                s =>
                {
                    s.Document.FindEntity("A1").Erase();
                    throw new InvalidOperationException("half done");
                }
            );

            Assert.False(result.Succeeded);
            Assert.False(result.Refused);
            Assert.Equal("half done", result.Message);
            Assert.Equal(start, document.Database.ToText());
            Assert.NotNull(document.FindEntity("A1"));
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AGroupOfEditsIsOneStep_AndAFailureInItTakesTheWholeGroupBack()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();

            document.BeginGroup("LINE");
            document.ApplyEdit("LINE", null, s =>
                s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0)))
            );
            document.ApplyEdit("LINE", null, s =>
                s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(1, 1, 0), new GeoPoint3(2, 0, 0)))
            );
            Assert.True(document.IsModified);
            Assert.False(document.CanUndo);
            document.EndGroup();

            Assert.Equal("LINE", document.NextUndo.Name);
            Assert.Equal(2, document.NextUndo.Records.Count);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());

            document.BeginGroup("LINE");
            document.ApplyEdit("LINE", null, s =>
                s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0)))
            );
            CadEditResult failed = document.ApplyEdit("LINE", null, s => throw new ArgumentException("no"));
            document.EndGroup();

            Assert.False(failed.Succeeded);
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ChangedNamesTheHandlesAndDropsOnlyTheSpacesTouched()
        {
            CadDocument document = Blank();
            CadSpace layout = document.Spaces[1];
            document.ApplyEdit("LINE", null, s =>
                s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)))
            );
            DisplayList2 model = document.GetDisplay(document.ModelSpace);
            DisplayList2 paper = document.GetDisplay(layout);
            var seen = new List<CadDocumentChangedEventArgs>();
            document.Changed += (sender, e) => seen.Add(e);

            CadEditResult added = document.ApplyEdit("CIRCLE", null, s =>
                s.Append(layout, new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 5))
            );

            CadDocumentChangedEventArgs change = Assert.Single(seen);
            Assert.Equal(CadChangeKind.Edit, change.Kind);
            Assert.Equal(new[] { added.Records[0].Handle }, change.Handles);
            Assert.Equal(new[] { layout }, change.Spaces);
            Assert.Same(model, document.GetDisplay(document.ModelSpace));
            Assert.NotSame(paper, document.GetDisplay(layout));

            seen.Clear();
            Assert.True(document.Undo());

            change = Assert.Single(seen);
            Assert.Equal(CadChangeKind.Undo, change.Kind);
            Assert.Equal(new[] { layout }, change.Spaces);
            Assert.Same(model, document.GetDisplay(document.ModelSpace));
        }

        [Fact]
        public void AnEditOfModelSpaceDropsTheLayoutsToo_AsTheirViewportsDrawIt()
        {
            CadDocument document = Blank();
            var seen = new List<CadDocumentChangedEventArgs>();
            document.Changed += (sender, e) => seen.Add(e);

            document.ApplyEdit("LINE", null, s =>
                s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)))
            );

            Assert.Equal(document.Spaces, Assert.Single(seen).Spaces);
        }

        [Fact]
        public void AfterAnUndo_TheCanvasDrawsTheEntityWhereItWas()
        {
            CadDocument document = Plan();
            MoveA1(document, 1000);
            Assert.Equal(1100, document.GetDisplay(document.ModelSpace).Bounds.MaxX, 9);

            document.Undo();

            Assert.Equal(510, document.GetDisplay(document.ModelSpace).Bounds.MaxX, 9);
            Assert.Equal(new[] { "A1" }, document.GetPickIndex(document.ModelSpace).Pick(50, 0, 0.5));
        }

        [Fact]
        public void TheEntityCountOfModelSpaceFollowsEditsAndUndo()
        {
            CadDocument document = Plan();
            Assert.Equal(8, document.EntityCount);

            document.ApplyEdit("LINE", null, s =>
                s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0)))
            );
            Assert.Equal(9, document.EntityCount);
            document.ApplyEdit(
                "ERASE",
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.Erased, "A1"),
                    new CadEditRecord(CadEditRecordKind.Erased, "A2"),
                },
                s =>
                {
                    s.Document.FindEntity("A1").Erase();
                    s.Document.FindEntity("A2").Erase();
                }
            );
            Assert.Equal(7, document.EntityCount);

            document.Undo();
            Assert.Equal(9, document.EntityCount);
            document.Undo();
            Assert.Equal(8, document.EntityCount);
        }

        [Fact]
        public void ANewEditForgetsTheStepsUndoneBeforeIt()
        {
            CadDocument document = Plan();
            MoveA1(document, 1);
            MoveA1(document, 2);
            document.Undo();
            Assert.True(document.CanRedo);

            MoveA1(document, 3);

            Assert.False(document.CanRedo);
            Near(new GeoPoint3(104, 0, 0), Entity<DxfLine>(document, "A1").EndPoint);
            document.Undo();
            Near(new GeoPoint3(101, 0, 0), Entity<DxfLine>(document, "A1").EndPoint);
        }

        [Fact]
        public void SaveAsWritesTheDrawing_ItOpensAsTheSameText_AndSaveWritesTheNextEditThere()
        {
            string path = Path.Combine(Path.GetTempPath(), "gh-viewer-saveas-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                CadDocument document = Plan();
                MoveA1(document, 10);

                document.SaveAs(path);

                Assert.False(document.IsModified);
                Assert.True(document.HasFile);
                Assert.Equal(Path.GetFullPath(path), document.FileName);
                Assert.Equal(document.Database.ToText(), CadDocument.Open(path).Database.ToText());

                MoveA1(document, 10);
                Assert.True(document.IsModified);
                document.Save();

                Assert.False(document.IsModified);
                Near(new GeoPoint3(120, 0, 0), ((DxfLine)CadDocument.Open(path).FindEntity("A1")).EndPoint);
                document.Undo();
                Assert.True(document.IsModified);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ADrawingWithNoFileOfItsOwnIsSavedWithSaveAs()
        {
            CadDocument document = Plan();

            Assert.False(document.HasFile);
            Assert.Throws<InvalidOperationException>(() => document.Save());
        }

        [Fact]
        public void TheSelectionLosesWhatAnUndoTakesAway()
        {
            CadDocument document = Plan();
            CadEditResult added = document.ApplyEdit("LINE", null, s =>
                s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0)))
            );
            string handle = added.Records[0].Handle;
            document.Selection.Set(new[] { handle, "A1" });

            document.Undo();

            Assert.Equal(new[] { "A1" }, document.Selection.Handles.ToArray());
        }

        private sealed class RefusingGate : ICadEditGate
        {
            private readonly string _refused;

            public RefusingGate(string refused)
            {
                _refused = refused;
            }

            public List<string> Asked { get; } = new List<string>();

            public bool Allows(CadDocument document, DxfEntity entity, CadEditRecord record, out string reason)
            {
                Asked.Add(record.Handle);
                reason = string.Equals(entity.Handle, _refused, StringComparison.OrdinalIgnoreCase)
                    ? "This line belongs to the model"
                    : null;
                return reason == null;
            }
        }
    }
}
