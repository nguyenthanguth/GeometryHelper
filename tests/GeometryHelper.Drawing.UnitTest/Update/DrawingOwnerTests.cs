using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Update
{
    public class DrawingOwnerTests
    {
        private const string MarkId = "V-FRONT/M-TC2-endplate";
        private const string OverallId = "V-FRONT/D-Overall-X-1";

        private static DxfEntity First(CadDocument document, string id, string viewId = null)
            => document.Database.ModelSpace.Cast<DxfEntity>()
                .Concat(SheetLayout(document.Database).BlockTableRecord.Cast<DxfEntity>())
                .First(e => !e.IsErased && DrawingEntityTags.TryRead(e, out string o, out string v) && o == id && (viewId == null || v == viewId));

        private static DxfMText MarkText(CadDocument document) => Tagged(document.Database.ModelSpace.Cast<DxfEntity>(), MarkId).OfType<DxfMText>().Single();

        [Fact]
        public void AProjectedLineIsReadOnlyAndAMoveOfItIsRefusedAsTheModels()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            var line = (DxfLine)First(document, "TC2-beam", "V-FRONT");
            GeoPoint3 start = line.StartPoint;

            CadEditResult moved = document.ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, line.Handle, "Move", null, "10,0") }, scope =>
                ((DxfEntity)scope.Database.GetObject(line.Handle)).TransformBy(GeoTransform3.Translation(new GeoVector3(10, 0, 0))));

            Assert.Equal(CadEditRule.ReadOnly, document.Owners.GetEditRule(line));
            Assert.True(moved.Refused);
            Assert.StartsWith("This line belongs to the model", moved.Message);
            Assert.False(document.Owners.TryApply(document, new CadEdit(line.Handle, CadEditKind.Move, newValue: "10,0")));
            Assert.Equal(start, ((DxfLine)document.FindEntity(line.Handle)).StartPoint);
            Assert.Empty(drawing.Edits);
        }

        [Fact]
        public void AMarkIsMovedAsAHandEditRecordedInTheDrawingAndUndoingItTakesTheRecordBack()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            DxfMText text = MarkText(document);
            GeoPoint3 before = text.Location;
            DxfLine leader = Tagged(document.Database.ModelSpace.Cast<DxfEntity>(), MarkId).OfType<DxfLine>().Single();
            GeoPoint3 anchor = leader.StartPoint;

            Assert.Equal(CadEditRule.MoveOnly, document.Owners.GetEditRule(text));
            Assert.True(document.Owners.TryApply(document, new CadEdit(text.Handle, CadEditKind.Move, newValue: "40,0")));

            DrawingEdit edit = Assert.Single(drawing.Edits);
            Assert.Equal(new DrawingEdit(MarkId, CadEditKind.Move, newValue: "40,0"), edit);
            Assert.Equal(new[] { edit }, DrawingDocumentStore.Read(document.Database).Edits);
            DxfMText moved = MarkText(document);
            Assert.Equal(before.X + 40, moved.Location.X, 9);
            Assert.Equal(before.Y, moved.Location.Y, 9);
            DxfLine newLeader = Tagged(document.Database.ModelSpace.Cast<DxfEntity>(), MarkId).OfType<DxfLine>().Single();
            Assert.Equal(anchor, newLeader.StartPoint);
            Assert.Equal(moved.Location, newLeader.EndPoint);
            Assert.Equal(1, document.NextUndo.Records.Count(r => r.Kind == CadEditRecordKind.Transformed));

            Assert.True(document.Undo());

            Assert.Empty(drawing.Edits);
            Assert.Empty(DrawingDocumentStore.Read(document.Database).Edits);
            Assert.Equal(before, MarkText(document).Location);
            Assert.Same(document.Database, drawing.Database);

            Assert.True(document.Redo());
            Assert.Single(drawing.Edits);
        }

        [Fact]
        public void AMarkMovedAsTheMoveCommandMovesItIsRecordedAsAHandEdit()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            DxfMText text = MarkText(document);

            // As CadCommands.Transform moves an entity: the planned record, then TransformBy in the edit.
            CadEditResult moved = document.ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, text.Handle, "Move") }, scope =>
                scope.Document.FindEntity(text.Handle).TransformBy(GeoTransform3.Translation(new GeoVector3(40, 0, 0))));

            Assert.True(moved.Succeeded);
            Assert.Equal(new DrawingEdit(MarkId, CadEditKind.Move, newValue: "40,0"), Assert.Single(drawing.Edits));
        }

        [Fact]
        public void AMarkCannotBeTurnedNorErased()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            DxfMText text = MarkText(document);

            Assert.False(document.Owners.TryApply(document, new CadEdit(text.Handle, CadEditKind.Rotate, newValue: "0,0,1")));
            Assert.False(document.Owners.TryApply(document, new CadEdit(text.Handle, CadEditKind.Erase)));
            CadEditResult erased = document.ApplyEdit("ERASE", new[] { new CadEditRecord(CadEditRecordKind.Erased, text.Handle) }, scope => ((DxfEntity)scope.Database.GetObject(text.Handle)).Erase());

            Assert.True(erased.Refused);
            Assert.Contains("can only be moved", erased.Message);
            Assert.Empty(drawing.Edits);
        }

        [Fact]
        public void ADimensionsTextIsOverriddenAsAHandEditAndShownAtOnce()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            DxfDimension overall = Dimension(document.Database, OverallId);

            Assert.Equal(CadEditRule.Edit, document.Owners.GetEditRule(overall));
            Assert.True(document.Owners.TryApply(document, new CadEdit(overall.Handle, CadEditKind.Property, "Text override", string.Empty, "<> (cut on site)")));

            Assert.Equal(new DrawingEdit(OverallId, CadEditKind.Property, "Text override", string.Empty, "<> (cut on site)"), Assert.Single(drawing.Edits));
            DxfDimension edited = Dimension(document.Database, OverallId);
            Assert.Equal("<> (cut on site)", edited.DimensionText);
            Assert.Contains(edited.DimBlock.Cast<DxfEntity>().OfType<DxfMText>(), t => t.Contents == "3000 (cut on site)");
            Assert.False(document.Owners.TryApply(document, new CadEdit(overall.Handle, CadEditKind.Property, "Layer", "Dimension", "0")));
        }

        [Fact]
        public void AViewIsMovedOnTheSheetByItsViewportAndItsTitleGoesWithIt()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            DxfBlockTableRecord sheet = SheetLayout(document.Database).BlockTableRecord;
            var viewport = Tagged(sheet.Cast<DxfEntity>(), "V-RIGHT").OfType<DxfViewport>().Single();
            var title = Tagged(sheet.Cast<DxfEntity>(), "V-RIGHT").OfType<DxfDBText>().Single();
            GeoPoint3 centre = viewport.CenterPoint, at = title.Position;

            Assert.Equal(CadEditRule.MoveOnly, document.Owners.GetEditRule(viewport));
            Assert.True(document.Owners.TryApply(document, new CadEdit(viewport.Handle, CadEditKind.Move, newValue: "15,-5")));

            DxfBlockTableRecord after = SheetLayout(document.Database).BlockTableRecord;
            Assert.Equal(centre.X + 15, Tagged(after.Cast<DxfEntity>(), "V-RIGHT").OfType<DxfViewport>().Single().CenterPoint.X, 9);
            Assert.Equal(at.Y - 5, Tagged(after.Cast<DxfEntity>(), "V-RIGHT").OfType<DxfDBText>().Single().Position.Y, 9);
            Assert.Equal(new DrawingEdit("V-RIGHT", CadEditKind.Move, newValue: "15,-5"), Assert.Single(drawing.Edits));
        }

        [Fact]
        public void TheSheetIsReadOnlyAndALineDrawnByHandIsThePresentersOwn()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            CadSpace sheet = document.Spaces.Single(s => s.Name == DrawingDxfWriter.LayoutName);
            DxfLine byHand = null;
            document.ApplyEdit("LINE", null, scope => byHand = scope.Append(sheet, new DxfLine(new GeoPoint3(20, 100, 0), new GeoPoint3(120, 100, 0))));

            Assert.Equal(CadEditRule.ReadOnly, document.Owners.GetEditRule(First(document, "GH-SHEET")));
            Assert.Equal(CadEditRule.ReadOnly, document.Owners.GetEditRule(First(document, "GH-TITLE")));
            Assert.Equal(CadEditRule.ReadOnly, document.Owners.GetEditRule(First(document, "GH-PARTLIST")));
            Assert.Equal(CadEditRule.Free, document.Owners.GetEditRule(document.FindEntity(byHand.Handle)));
            Assert.Equal(DrawingEntityTags.OwnerName, Assert.Single(document.Owners.Owners).Name);
        }
    }
}
