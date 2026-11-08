using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Update
{
    /// <summary>
    /// A drawing changed after it is made: made again with other settings, prepared on a thread of its own and applied, one
    /// view dimensioned, marked or given its centre lines again, a view, a section or a detail added; each one undo step in
    /// the presenter, the hand edits kept.
    /// </summary>
    public class DrawingChangeTests
    {
        private const string MarkId = "V-FRONT/M-TC2-endplate";

        private static DxfMText MarkText(DxfDatabase database) => Tagged(database.ModelSpace.Cast<DxfEntity>(), MarkId).OfType<DxfMText>().Single();

        [Fact]
        public void MadeAgainOnAnA2WithoutItsTopViewTheDrawingKeepsItsHandEditsAndWhatWasDrawnByHand()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            Assert.True(document.Owners.TryApply(document, new CadEdit(MarkText(document.Database).Handle, CadEditKind.Move, newValue: "0,60")));
            DxfDimension top = Dimension(document.Database, "V-TOP/D-Overall-X-1");
            Assert.True(document.Owners.TryApply(document, new CadEdit(top.Handle, CadEditKind.Property, "Text override", string.Empty, "<> (top)")));
            CadSpace sheet = document.Spaces.Single(s => s.Name == DrawingDxfWriter.LayoutName);
            DxfLine byHand = null;
            document.ApplyEdit("LINE", null, scope => byHand = scope.Append(sheet, new DxfLine(new GeoPoint3(20, 100, 0), new GeoPoint3(120, 100, 0))));

            DrawingSettings settings = drawing.Settings;
            settings.Sheet.Size = PaperSize.A2;
            settings.Views.Kinds.Remove(ViewKind.Top);
            UpdateReport report = drawing.Update(TC2(), settings);

            Assert.Equal(new[] { "V-FRONT", "V-RIGHT" }, drawing.Views.Select(v => v.Id));
            Assert.Equal("A2", drawing.Sheet.SizeName);
            Assert.Equal(PaperSize.A2, drawing.Settings.Sheet.Size);
            Assert.Contains("V-TOP", report.RemovedElements);
            Assert.Equal(new[] { MarkId }, report.EditsKept.Select(e => e.ObjectId));
            Assert.Equal("V-TOP/D-Overall-X-1", Assert.Single(report.EditsDropped).Edit.ObjectId);
            Assert.False(document.FindEntity(byHand.Handle).IsErased);
            DrawingView front = drawing.Views[0];
            DrawingMark mark = front.Marks.Single();
            Assert.Equal(front.ModelOrigin.Y + mark.TextBox.Center.Y + 60, MarkText(document.Database).Location.Y, 9);
            Assert.Equal("UPDATE", document.NextUndo.Name);

            // One undo step takes the settings, views and edits back with the DXF.
            Assert.True(document.Undo());
            Assert.Equal(PaperSize.A3, drawing.Settings.Sheet.Size);
            Assert.Equal(new[] { "V-FRONT", "V-TOP", "V-RIGHT" }, drawing.Views.Select(v => v.Id));
            Assert.Equal("A3", drawing.Sheet.SizeName);
            Assert.Equal(2, drawing.Edits.Count);
            Assert.NotNull(Dimension(document.Database, "V-TOP/D-Overall-X-1"));

            Assert.True(document.Redo());
            Assert.Equal(PaperSize.A2, drawing.Settings.Sheet.Size);
            Assert.Equal(new[] { "V-FRONT", "V-RIGHT" }, drawing.Views.Select(v => v.Id));
        }

        [Fact]
        public async Task AnUpdatePreparedOnAThreadOfItsOwnIsAppliedAsUpdateMakesIt()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            Drawing same = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            DrawingSettings settings = drawing.Settings;
            settings.Marks.TextHeight = 5;

            PreparedUpdate prepared = await Task.Run(() => drawing.Prepare(TC2(3500), settings));
            Assert.False(prepared.IsApplied);
            Assert.Equal(3.5, drawing.Settings.Marks.TextHeight);
            UpdateReport applied = drawing.Apply(prepared);
            UpdateReport updated = same.Update(TC2(3500), settings);

            Assert.True(prepared.IsApplied);
            Assert.Equal(same.Elements, drawing.Elements);
            Assert.Equal(updated.ChangedElements, applied.ChangedElements);
            Assert.Equal(Bytes(same.Database), Bytes(drawing.Database));
            Assert.Throws<InvalidOperationException>(() => drawing.Apply(prepared));
            Assert.Throws<ArgumentException>(() => same.Apply(drawing.Prepare(TC2(), null)));
        }

        [Fact]
        public void AViewDimensionedAgainByHandStaysDimensionedThroughUpdatesAndTheUndoTakesItBack()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            Assert.Empty(drawing.Views.Single(v => v.Id == "V-RIGHT").Dimensions);

            UpdateReport report = drawing.ReDimension("V-RIGHT");

            Assert.NotEmpty(drawing.Views.Single(v => v.Id == "V-RIGHT").Dimensions);
            Assert.Contains(report.AddedElements, id => id.StartsWith("V-RIGHT/D-", StringComparison.Ordinal));
            Assert.Equal(new[] { "V-RIGHT" }, drawing.Settings.Views.DimensionedViews);
            drawing.Update(TC2(3500));
            Assert.NotEmpty(drawing.Views.Single(v => v.Id == "V-RIGHT").Dimensions);

            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Empty(drawing.Settings.Views.DimensionedViews);
            Assert.Empty(drawing.Views.Single(v => v.Id == "V-RIGHT").Dimensions);
            Assert.DoesNotContain(drawing.Elements, e => e.Id.StartsWith("V-RIGHT/D-", StringComparison.Ordinal));
            Assert.Throws<ArgumentException>(() => drawing.ReDimension("V-NONE"));
        }

        [Fact]
        public void AViewMarkedAgainGetsMarksAndCentreLinesDrawnAgainChangeNothingWhereTheModelHasNot()
        {
            Drawing drawing = DrawingBuilder.Build(TestBodies.TC5().ToSource(), DrawingSettings.Assembly, Tol);
            Assert.Empty(drawing.Views.Single(v => v.Id == "V-TOP").Marks);

            UpdateReport marked = drawing.ReMark("V-TOP");
            UpdateReport centred = drawing.ReCentreLines("V-FRONT");

            Assert.NotEmpty(drawing.Views.Single(v => v.Id == "V-TOP").Marks);
            Assert.Contains(marked.AddedElements, id => id.StartsWith("V-TOP/M-", StringComparison.Ordinal));
            Assert.Empty(centred.AddedElements);
            Assert.Empty(centred.ChangedElements);
            Assert.Empty(centred.RemovedElements);
        }

        [Fact]
        public void AViewASectionAlongALineAndADetailAreAddedToADrawing()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);

            drawing.AddView(ViewKind.Back);
            drawing.AddSection("B", "V-FRONT", new GeoLine2(1000, -200, 1000, 200), 100);
            double scale = drawing.Views.Single(v => v.Id == "V-FRONT").Scale;
            drawing.AddDetail("C", "V-FRONT", new GeoRectangle2(2800, -200, 300, 400), scale / 2);

            Assert.Equal(new[] { "V-FRONT", "V-TOP", "V-RIGHT", "V-BACK", "V-SECTION-B", "V-DETAIL-C" }, drawing.Views.Select(v => v.Id));

            // The section looks along the beam towards its far end, its plane at 1 000, and is marked B in the front view.
            DrawingView section = drawing.Views.Single(v => v.Id == "V-SECTION-B");
            Assert.Equal(1000.0, section.SectionPlane.Value.Origin.X, 9);
            Assert.Equal(-1.0, section.SectionPlane.Value.Normal.X, 9);
            Assert.Equal(100.0, section.Frame.DepthBehind, 9);
            Assert.NotEmpty(section.Lines.VisibleEdges);
            SectionMark mark = Assert.Single(drawing.Views[0].SectionMarks);
            Assert.Equal("B", mark.Letter);
            Assert.Equal(new GeoLine2(1000, -200, 1000, 200), mark.CutLine);
            Assert.Equal(1.0, mark.Direction.X, 9);

            DrawingView detail = drawing.Views.Single(v => v.Id == "V-DETAIL-C");
            Assert.Equal(drawing.Views[0].Scale / 2, detail.Scale, 9);
            Assert.Equal("C", Assert.Single(drawing.Views[0].DetailMarks).Letter);

            Assert.Throws<ArgumentException>(() => drawing.AddSection("B", "V-FRONT", new GeoLine2(500, -200, 500, 200), 100));
            Assert.Throws<ArgumentException>(() => drawing.AddView(ViewKind.Section));
        }

        [Fact]
        public void TheViewsDimensionedAndMarkedByIdAndASectionAlongALineAreKeptInTheDocument()
        {
            DrawingSettings settings = AssemblyOnA3();
            settings.Views.DimensionedViews.Add("V-RIGHT");
            settings.Views.MarkedViews.Add("V-TOP");
            settings.Views.Sections.Add(new SectionViewRequest("B", ViewKind.Front, new GeoLine2(1000, -200, 1000, 200), 100));
            Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);

            string path = Path.Combine(Folder(), "tc2-settings-by-view.dxf");
            drawing.Save(path);
            DrawingSettings read = Drawing.Open(path, TC2(), Tol).Settings;

            Assert.Equal(new[] { "V-RIGHT" }, read.Views.DimensionedViews);
            Assert.Equal(new[] { "V-TOP" }, read.Views.MarkedViews);
            SectionViewRequest section = Assert.Single(read.Views.Sections);
            Assert.Equal(new GeoLine2(1000, -200, 1000, 200), section.CutLine);
            Assert.Equal(100.0, section.Depth);
            Assert.Equal(ViewKind.Front, section.LookingAs);
        }
    }
}
