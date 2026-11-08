using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Update
{
    public class DrawingUpdateTests
    {
        private const string MarkId = "V-FRONT/M-TC2-endplate";
        private const string ExtraMarkId = "V-FRONT/M-P-EXTRA";
        private const string OverallId = "V-FRONT/D-Overall-X-1";

        private readonly ITestOutputHelper _output;

        public DrawingUpdateTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static DxfMText MarkText(DxfDatabase database, string id) => Tagged(database.ModelSpace.Cast<DxfEntity>(), id).OfType<DxfMText>().Single();

        /// <summary>TC2 with its extra plate, saved with a plugin run in its document, opened again and shown in the presenter.</summary>
        private static (Drawing Drawing, CadDocument Document, DrawingPluginRun Run) Saved()
        {
            Drawing built = DrawingBuilder.Build(TC2(3000, extra: true), AssemblyOnA3(), Tol);
            DrawingDocumentRecords records = DrawingDocumentStore.Read(built.Database);
            var run = new DrawingPluginRun("R-1", "BoltDimensions", "1.2", "V-FRONT", new DrawingLinks(new[] { "TC2-endplate" }), new[] { new KeyValuePair<string, string>("Gap", "8") });
            DrawingDocumentStore.Write(built.Database, new DrawingDocumentRecords(records.DrawingId, records.Views, records.Annotations, records.Edits, new[] { run }));
            string path = Path.Combine(Folder(), "tc2-update.dxf");
            built.Save(path);
            Drawing drawing = Drawing.Open(path, TC2(3000, extra: true), Tol);
            return (drawing, drawing.OpenInPresenter(), run);
        }

        [Fact]
        public void TC2MadeLongerKeepsTheHandEditsThatStillApplyAndReportsTheOneOnAPlateTakenOut()
        {
            (Drawing drawing, CadDocument document, DrawingPluginRun run) = Saved();
            Assert.Equal(new[] { run }, drawing.PluginRuns);

            // By hand: the end plate's mark 40 to the right, the extra plate's mark 30 down, the overall dimension's text
            // overridden, and a line drawn on the sheet.
            Assert.True(document.Owners.TryApply(document, new CadEdit(MarkText(document.Database, MarkId).Handle, CadEditKind.Move, newValue: "40,0")));
            Assert.True(document.Owners.TryApply(document, new CadEdit(MarkText(document.Database, ExtraMarkId).Handle, CadEditKind.Move, newValue: "0,-30")));
            DxfDimension overall = Dimension(document.Database, OverallId);
            Assert.Equal(3000.0, overall.Measurement, 6);
            Assert.True(document.Owners.TryApply(document, new CadEdit(overall.Handle, CadEditKind.Property, "Text override", string.Empty, "<> (check)")));
            CadSpace sheet = document.Spaces.Single(s => s.Name == DrawingDxfWriter.LayoutName);
            DxfLine byHand = null;
            document.ApplyEdit("LINE", null, scope => byHand = scope.Append(sheet, new DxfLine(new GeoPoint3(20, 100, 0), new GeoPoint3(120, 100, 0))));
            string handle = byHand.Handle;
            Assert.Equal(3, drawing.Edits.Count);

            UpdateReport report = drawing.Update(TC2(3500));
            _output.WriteLine(report.ToString());

            foreach (DroppedEdit dropped in report.EditsDropped)
            {
                _output.WriteLine(dropped.ToString());
            }

            // The model's changes.
            Assert.Equal(new[] { "TC2-beam", "TC2-endplate" }, report.ChangedObjects);
            Assert.Equal(new[] { "P-EXTRA" }, report.RemovedObjects);
            Assert.Empty(report.AddedObjects);
            Assert.Contains(OverallId, report.ChangedElements);
            Assert.Contains(ExtraMarkId, report.RemovedElements);

            // The overall dimension follows, its text override kept.
            DxfDatabase after = document.Database;
            DxfDimension longer = Dimension(after, OverallId);
            Assert.Equal(3500.0, longer.Measurement, 6);
            Assert.Equal("<> (check)", longer.DimensionText);
            Assert.Contains(longer.DimBlock.Cast<DxfEntity>().OfType<DxfMText>(), t => t.Contents == "3500 (check)");
            Assert.Equal("3500", drawing.Views[0].Dimensions[drawing.Views[0].DimensionIds.ToList().IndexOf(OverallId)].Text);

            // The end plate's mark stands 40 to the right of where the drawing puts it, at the plate's new place.
            DrawingView front = drawing.Views[0];
            DrawingMark mark = front.Marks.Single(m => "V-FRONT/" + m.Id == MarkId);
            DxfMText text = MarkText(after, MarkId);
            Assert.Equal(front.ModelOrigin.X + mark.TextBox.Center.X + 40, text.Location.X, 9);
            Assert.Equal(front.ModelOrigin.Y + mark.TextBox.Center.Y, text.Location.Y, 9);
            Assert.True(mark.Anchor.X > 3000, "The anchor of the mark is on the plate, now past 3 500.");

            // The line drawn by hand is still there; the edit of the plate taken out is reported and dropped.
            Assert.NotNull(document.FindEntity(handle));
            Assert.False(document.FindEntity(handle).IsErased);
            Assert.Equal(new[] { MarkId, OverallId }, report.EditsKept.Select(e => e.ObjectId));
            DroppedEdit gone = Assert.Single(report.EditsDropped);
            Assert.Equal(ExtraMarkId, gone.Edit.ObjectId);
            Assert.Equal("the object P-EXTRA is no longer in the model", gone.Reason);
            Assert.Equal(report.EditsKept, drawing.Edits);
            Assert.Equal(report.EditsKept, DrawingDocumentStore.Read(after).Edits);

            // The plugin run is kept, to be run again.
            Assert.Equal(new[] { run }, report.PluginRunsToRunAgain);
            Assert.Equal(new[] { run }, DrawingDocumentStore.Read(after).PluginRuns);
        }

        [Fact]
        public void AnUpdateInThePresenterIsOneUndoStepThatTakesBackTheDxfAndTheEdits()
        {
            (Drawing drawing, CadDocument document, _) = Saved();
            Assert.True(document.Owners.TryApply(document, new CadEdit(MarkText(document.Database, ExtraMarkId).Handle, CadEditKind.Move, newValue: "0,-30")));

            drawing.Update(TC2(3500));
            Assert.Empty(drawing.Edits);
            Assert.Equal("UPDATE", document.NextUndo.Name);

            Assert.True(document.Undo());

            Assert.Equal(3000.0, Dimension(document.Database, OverallId).Measurement, 6);
            Assert.Equal(ExtraMarkId, Assert.Single(drawing.Edits).ObjectId);
        }

        [Fact]
        public void AnUpdateWithNoPresenterWritesAgainOnlyTheDrawingsOwnEntities()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(DrawingExportMode.SheetInModelSpace), Tol);
            var byHand = new DxfLine(new GeoPoint3(20, 100, 0), new GeoPoint3(120, 100, 0));
            drawing.Database.ModelSpace.AppendEntity(byHand);
            new CadOwnerTag("Someone.Else", "X-1").WriteTo(byHand);

            UpdateReport report = drawing.Update(TC2(3500));
            Drawing fresh = DrawingBuilder.Build(TC2(3500), AssemblyOnA3(DrawingExportMode.SheetInModelSpace), Tol);

            Assert.False(byHand.IsErased);
            Assert.Equal(DrawingEntityTags.Find(fresh.Database).Count, DrawingEntityTags.Find(drawing.Database).Count);
            Assert.Equal(fresh.Elements, drawing.Elements);
            Assert.Empty(report.EditsKept);
            Assert.Empty(report.PluginRunsToRunAgain);
            Assert.Equal(new[] { "TC2-beam", "TC2-endplate" }, report.ChangedObjects);
        }

        [Fact]
        public void AnUpdateFromTheSameModelChangesNothing()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            byte[] before = Bytes(drawing.Database);

            UpdateReport report = drawing.Update(TC2());

            Assert.Empty(report.ChangedObjects);
            Assert.Empty(report.ChangedElements);
            Assert.Empty(report.AddedElements);
            Assert.Empty(report.RemovedElements);
            Assert.Equal(DrawingEntityTags.Find(DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol).Database).Count, DrawingEntityTags.Find(drawing.Database).Count);
            Assert.NotEmpty(before);
        }
    }
}
