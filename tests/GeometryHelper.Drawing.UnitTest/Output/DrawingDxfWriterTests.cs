using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Output
{
    public class DrawingDxfWriterTests
    {
        private readonly ITestOutputHelper _output;

        public DrawingDxfWriterTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static IEnumerable<DxfEntity> Live(IEnumerable<DxfEntity> space) => space.Where(e => !e.IsErased);

        /// <summary>The visible lines of a view drawing an object, in a space.</summary>
        private static List<DxfLine> Lines(IEnumerable<DxfEntity> space, string objectId, string viewId) =>
            Live(space)
                .OfType<DxfLine>()
                .Where(e =>
                    e.Layer == "Visible"
                    && DrawingEntityTags.TryRead(e, out string o, out string v)
                    && o == objectId
                    && v == viewId
                )
                .ToList();

        [Fact]
        public void AsALayoutTheLinesAreAtOneToOneInModelSpaceAndEachViewIsAViewportAtItsScaleLookingAtThem()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            DxfDatabase database = SavedAndOpened(drawing.Database);

            List<DxfLine> beam = Lines(database.ModelSpace, "TC2-beam", "V-FRONT");
            Assert.Equal(3000.0, beam.Max(l => l.StartPoint.DistanceTo(l.EndPoint)), 6);

            DxfLayout layout = SheetLayout(database);
            List<DxfViewport> viewports = layout
                .GetViewports()
                .Where(v => DrawingEntityTags.TryRead(v, out _, out _))
                .ToList();
            Assert.Equal(
                new[] { "V-FRONT", "V-TOP", "V-RIGHT" },
                viewports.Select(v =>
                {
                    DrawingEntityTags.TryRead(v, out string id, out _);
                    return id;
                })
            );
            Assert.False(database.TileMode);

            foreach (DrawingView view in drawing.Views)
            {
                DxfViewport viewport = viewports.Single(v =>
                    DrawingEntityTags.TryRead(v, out string id, out _) && id == view.Id
                );
                Assert.Equal(1.0 / view.Scale, viewport.CustomScale, 9);
                Assert.Equal(view.SheetBox.Center.X, viewport.CenterPoint.X, 6);
                Assert.Equal(view.SheetBox.Width, viewport.Width, 6);
                Assert.Equal(view.ModelOrigin.X + view.Box.Center.X, viewport.ViewCenter.X, 6);
                Assert.Equal(view.ModelOrigin.Y + view.Box.Center.Y, viewport.ViewCenter.Y, 6);
                Assert.True(viewport.On);
            }

            // The front view's lines lie where its viewport looks: the beam's start at the front view's origin in model space.
            DrawingView front = drawing.Views[0];
            Assert.Contains(beam, l =>
                l.StartPoint.DistanceTo(new GeoPoint3(front.ModelOrigin.X, front.ModelOrigin.Y - 150, 0))
                    <= Tol.EqualPoint
                || l.EndPoint.DistanceTo(new GeoPoint3(front.ModelOrigin.X, front.ModelOrigin.Y - 150, 0))
                    <= Tol.EqualPoint
            );
        }

        [Fact]
        public void AsALayoutADimensionInModelSpaceReadsTheLengthItSpansInAStyleWhoseDimlfacIsOne()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            DxfDatabase database = SavedAndOpened(drawing.Database);

            DxfDimension overall = Dimension(database, "V-FRONT/D-Overall-X-1");

            Assert.Equal(3000.0, overall.Measurement, 6);
            Assert.Equal("GH_MODEL_10", overall.DimensionStyleName);
            Assert.Equal(1.0, overall.DimensionStyle.Dimlfac);
            Assert.Equal(25.0, overall.DimensionStyle.Dimtxt, 9);
            Assert.True(DrawingEntityTags.TryRead(overall, out _, out string viewId));
            Assert.Equal("V-FRONT", viewId);
        }

        [Fact]
        public void WithTheSheetInModelSpaceEachViewIsDrawnAtItsScaleAndItsDimensionsReadTheModelsLength()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(DrawingExportMode.SheetInModelSpace), Tol);
            DxfDatabase database = SavedAndOpened(drawing.Database);

            List<DxfLine> beam = Lines(database.ModelSpace, "TC2-beam", "V-FRONT");
            Assert.Equal(300.0, beam.Max(l => l.StartPoint.DistanceTo(l.EndPoint)), 6);

            DxfDimension overall = Dimension(database, "V-FRONT/D-Overall-X-1");
            Assert.Equal("GH_10", overall.DimensionStyleName);
            Assert.Equal(10.0, overall.DimensionStyle.Dimlfac);
            Assert.Equal(3000.0, overall.Measurement * overall.DimensionStyle.Dimlfac, 6);
            Assert.DoesNotContain(database.Layouts, l => l.LayoutName == DrawingDxfWriter.LayoutName);

            // The frame and the title block are drawn on the sheet, in model space.
            Assert.Single(Tagged(database.ModelSpace.Cast<DxfEntity>(), "GH-TITLE").OfType<DxfBlockReference>());
            Assert.Equal(2, Tagged(database.ModelSpace.Cast<DxfEntity>(), "GH-SHEET").Count);
        }

        [Fact]
        public void EveryEntityWrittenIsTaggedWithTheDrawingAndTheIdOfWhatItDraws()
        {
            foreach (
                DrawingExportMode mode in new[]
                {
                    DrawingExportMode.LayoutAndViewports,
                    DrawingExportMode.SheetInModelSpace,
                }
            )
            {
                Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(mode), Tol);
                DxfDatabase database = SavedAndOpened(drawing.Database);
                var ids = new HashSet<string>(
                    drawing.Elements.Select(e => e.Id).Concat(drawing.Source.Objects.Select(o => o.Id)),
                    StringComparer.Ordinal
                );

                var spaces = new List<DxfBlockTableRecord> { database.ModelSpace };
                spaces.AddRange(
                    database
                        .Layouts.Where(l => l.LayoutName != "Model" && l.BlockTableRecord != null)
                        .Select(l => l.BlockTableRecord)
                );
                List<DxfEntity> all = spaces.SelectMany(s => Live(s.Cast<DxfEntity>())).ToList();
                List<DxfEntity> untagged = all.Where(e => !DrawingEntityTags.TryRead(e, out _, out _)).ToList();

                // Only the viewport that shows the sheet itself, which the layout is given, is not the drawing's.
                Assert.True(
                    untagged.All(e => e is DxfViewport),
                    string.Join(", ", untagged.Select(e => e.GetType().Name))
                );
                Assert.Equal(mode == DrawingExportMode.LayoutAndViewports ? 1 : 0, untagged.Count);
                Assert.All(all.Except(untagged), e =>
                {
                    DrawingEntityTags.TryRead(e, out string id, out _);
                    Assert.Contains(id, ids);
                });
                Assert.Equal(all.Count - untagged.Count, DrawingEntityTags.Find(database).Count);
            }
        }

        [Fact]
        public void SavedAndOpenedTheDrawingHasTheSameDocumentAndItsEntitiesAreFoundByTheirTags()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            string path = Path.Combine(Folder(), "tc2-saved.dxf");
            drawing.Save(path);

            Drawing opened = Drawing.Open(path, null, Tol);

            Assert.Equal(drawing.Id, opened.Id);
            Assert.Equal(drawing.Elements, opened.Elements);
            Assert.Equal(drawing.Edits, opened.Edits);
            Assert.Equal(10.0, opened.Scale);
            Assert.Empty(opened.Views);
            Assert.Null(opened.Sheet);
            Assert.Null(opened.Source);
            Assert.Equal(DrawingEntityTags.Find(drawing.Database).Count, DrawingEntityTags.Find(opened.Database).Count);
            Assert.Equal(DrawingExportMode.LayoutAndViewports, opened.Settings.ExportMode);
            Assert.Equal(Packing.PaperSize.A3, opened.Settings.Sheet.Size);
            Assert.Equal(drawing.Settings.Scales, opened.Settings.Scales);
            Assert.Throws<InvalidOperationException>(() => DrawingDxfWriter.Write(opened));

            // Made again from the same model with the settings read back, it is the drawing it was.
            opened.Update(TC2());
            Assert.Equal(drawing.Elements, opened.Elements);
            Assert.Equal(Bytes(DrawingDxfWriter.Write(drawing)), Bytes(DrawingDxfWriter.Write(opened)));
        }

        [Fact]
        public void APlainDxfIsNotOpenedAsADrawing()
        {
            string path = Path.Combine(Folder(), "plain.dxf");
            new DxfDatabase("AC1027").Save(path);

            Assert.Throws<ArgumentException>(() => Drawing.Open(path, null, Tol));
        }

        [Fact]
        public void TheSameSourceAndSettingsGiveTheSameBytesEitherWay()
        {
            foreach (
                DrawingExportMode mode in new[]
                {
                    DrawingExportMode.LayoutAndViewports,
                    DrawingExportMode.SheetInModelSpace,
                }
            )
            {
                DrawingSettings settings = AssemblyOnA3(mode);
                settings.Views.Sections.Add(new SectionViewRequest("A", ViewKind.Right, 0.5));
                byte[] one = Bytes(DrawingBuilder.Build(TC2(), settings, Tol).Database);
                byte[] two = Bytes(DrawingBuilder.Build(TC2(), settings, Tol).Database);
                byte[] written = Bytes(DrawingDxfWriter.Write(DrawingBuilder.Build(TC2(), settings, Tol)));

                Assert.Equal(one, two);
                Assert.Equal(one, written);
            }
        }

        [Fact]
        public void TheTitleBlockIsAnInsertOfItsBlockWithItsTextsAsAttributes()
        {
            DrawingSettings settings = AssemblyOnA3();
            settings.Sheet.TitleBlock.Company = "HDC";
            DxfDatabase database = SavedAndOpened(DrawingBuilder.Build(TC2(), settings, Tol).Database);

            DxfBlockReference insert = Tagged(SheetLayout(database).BlockTableRecord.Cast<DxfEntity>(), "GH-TITLE")
                .OfType<DxfBlockReference>()
                .Single();
            Dictionary<string, string> texts = insert
                .AttributeCollection.Cast<DxfAttributeReference>()
                .ToDictionary(a => a.Tag, a => a.TextString);

            Assert.Equal(DrawingDxfWriter.TitleBlockName, insert.Name);
            Assert.Equal("BEAM B1", texts["TITLE"]);
            Assert.Equal("TC2-beam", texts["DRAWING_NO"]);
            Assert.Equal("1:10", texts["SCALE"]);
            Assert.Equal("A3", texts["SHEET"]);
            Assert.Equal("HDC", texts["COMPANY"]);
            Assert.Equal(new GeoPoint3(230, 10, 0), insert.Position);
        }

        [Fact]
        public void ThePartListIsDrawnAsARowOfTextsForEachMarkUnderItsHead()
        {
            DxfDatabase database = SavedAndOpened(DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol).Database);

            List<string> texts = Tagged(SheetLayout(database).BlockTableRecord.Cast<DxfEntity>(), "GH-PARTLIST")
                .OfType<DxfDBText>()
                .Select(t => t.TextString)
                .ToList();

            Assert.Equal(
                new[]
                {
                    "MARK",
                    "QTY",
                    "PROFILE",
                    "MATERIAL",
                    "LENGTH",
                    "NAME",
                    "B1",
                    "1",
                    "I300*150*12*8",
                    "S355",
                    "3000",
                    "BEAM",
                    "P1",
                    "1",
                    "PL20*200",
                    "S355",
                    "200",
                    "PLATE",
                },
                texts
            );
        }

        [Fact]
        public void ADetailIsCutAtItsRectangleOnTheSheet()
        {
            DrawingSettings settings = AssemblyOnA3(DrawingExportMode.SheetInModelSpace);
            settings.Views.Details.Add(
                new DetailViewRequest("B", ViewKind.Front, new GeoRectangle2(2800, -200, 300, 400), 4)
            );
            Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);
            DrawingView detail = drawing.Views.Single(v => v.Kind == ViewKind.Detail);
            DxfDatabase database = SavedAndOpened(drawing.Database);

            List<DxfLine> lines = Live(database.ModelSpace)
                .OfType<DxfLine>()
                .Where(e =>
                    DrawingEntityTags.TryRead(e, out string o, out string v)
                    && v == detail.Id
                    && (o == "TC2-beam" || o == "TC2-endplate")
                )
                .ToList();
            double x0 = detail.SheetOrigin.X + 2800 / detail.Scale,
                x1 = detail.SheetOrigin.X + 3100 / detail.Scale;

            Assert.NotEmpty(lines);
            Assert.All(lines, l =>
                Assert.InRange(Math.Min(l.StartPoint.X, l.EndPoint.X), x0 - Tol.EqualPoint, x1 + Tol.EqualPoint)
            );
            Assert.All(lines, l =>
                Assert.InRange(Math.Max(l.StartPoint.X, l.EndPoint.X), x0 - Tol.EqualPoint, x1 + Tol.EqualPoint)
            );
            Assert.True(detail.Scale < drawing.Scale, detail.Title);
            _output.WriteLine(detail.Title + " of a drawing at 1:" + drawing.Scale);
        }

        [Fact]
        public void TheDrawingOpensInThePresenterAndIsDrawnAsPicturesOfItsLayoutAndModelSpace()
        {
            DrawingSettings settings = AssemblyOnA3();
            settings.Views.Sections.Add(new SectionViewRequest("A", ViewKind.Right, 0.5));
            settings.Views.Details.Add(
                new DetailViewRequest("B", ViewKind.Front, new GeoRectangle2(2800, -200, 300, 400), 4)
            );
            Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);
            drawing.Save(Path.Combine(Folder(), "tc2-layout.dxf"));
            Drawing flat = DrawingBuilder.Build(TC2(), AssemblyOnA3(DrawingExportMode.SheetInModelSpace), Tol);
            flat.Save(Path.Combine(Folder(), "tc2-sheet-in-model.dxf"));

            CadDocument document = CadDocument.From(SavedAndOpened(drawing.Database));
            Assert.Contains(document.Spaces, s => s.Name == DrawingDxfWriter.LayoutName);
            Assert.False(
                document.GetDisplay(document.Spaces.Single(s => s.Name == DrawingDxfWriter.LayoutName)).Bounds.IsEmpty
            );

            _output.WriteLine(SavePicture(drawing.Database, DrawingDxfWriter.LayoutName, "tc2-layout"));
            _output.WriteLine(SavePicture(drawing.Database, "Model", "tc2-model"));
            _output.WriteLine(SavePicture(flat.Database, "Model", "tc2-sheet-in-model"));
        }
    }
}
