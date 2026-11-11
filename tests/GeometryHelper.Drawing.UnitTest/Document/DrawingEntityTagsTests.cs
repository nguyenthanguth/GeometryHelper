using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Drawing.UnitTest
{
    public class DrawingEntityTagsTests
    {
        private static DxfDatabase SavedAndOpened(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                database.Save(stream);
                stream.Position = 0;
                return DxfDatabase.Open(stream);
            }
        }

        private static DxfLine Line(DxfBlockTableRecord space, double y)
        {
            var line = new DxfLine(new GeoPoint3(0, y, 0), new GeoPoint3(3000, y, 0));
            space.AppendEntity(line);
            return line;
        }

        /// <summary>
        /// A sheet of a beam: two lines of the front view V-1 and its dimension D-7 in model space, a mark M-3 in paper space,
        /// a line drawn by hand and a line another owner tagged.
        /// </summary>
        private static DxfDatabase Sheet()
        {
            var database = new DxfDatabase("AC1027");
            DrawingEntityTags.Tag(Line(database.ModelSpace, 0), "B-12", "V-1");
            DrawingEntityTags.Tag(Line(database.ModelSpace, 300), "B-12", "V-1");
            Line(database.ModelSpace, 600);
            new CadOwnerTag("Someone.Else", "X-1").WriteTo(Line(database.ModelSpace, 900));
            DrawingEntityTags.Tag(Line(database.ModelSpace, 1200), "D-7", "V-1");
            var mark = new DxfDBText
            {
                TextString = "P1",
                Position = new GeoPoint3(50, 50, 0),
                Height = 3.5,
            };
            database.PaperSpace.AppendEntity(mark);
            DrawingEntityTags.Tag(mark, "M-3", null);
            return database;
        }

        [Fact]
        public void TheDrawingsEntitiesAreFoundAndTakenOutWhileALineDrawnByHandStays()
        {
            DxfDatabase database = SavedAndOpened(Sheet());

            IReadOnlyList<DxfEntity> found = DrawingEntityTags.Find(database);
            int removed = DrawingEntityTags.RemoveAll(database);

            Assert.Equal(
                new[] { "B-12", "B-12", "D-7", "M-3" },
                found.Select(e =>
                {
                    DrawingEntityTags.TryRead(e, out string id, out _);
                    return id;
                })
            );
            Assert.Equal(4, removed);
            DxfDatabase after = SavedAndOpened(database);
            Assert.Empty(DrawingEntityTags.Find(after));
            List<DxfEntity> left = after.ModelSpace.Cast<DxfEntity>().ToList();
            Assert.Equal(2, left.Count);
            Assert.Equal(new[] { 600.0, 900.0 }, left.Select(e => ((DxfLine)e).StartPoint.Y));
            Assert.False(CadOwnerTag.TryRead(left[0], out _));
            Assert.True(CadOwnerTag.TryRead(left[1], out CadOwnerTag other));
            Assert.Equal("Someone.Else", other.Owner);
            Assert.Empty(after.PaperSpace.Cast<DxfEntity>());
        }

        [Fact]
        public void ATagComesBackWithItsElementAndView()
        {
            DxfDatabase database = SavedAndOpened(Sheet());

            List<DxfEntity> model = database.ModelSpace.Cast<DxfEntity>().ToList();
            DxfEntity mark = Assert.Single(database.PaperSpace.Cast<DxfEntity>());

            Assert.True(DrawingEntityTags.TryRead(model[4], out string objectId, out string viewId));
            Assert.Equal("D-7", objectId);
            Assert.Equal("V-1", viewId);
            Assert.True(DrawingEntityTags.TryRead(mark, out objectId, out viewId));
            Assert.Equal("M-3", objectId);
            Assert.Null(viewId);
            Assert.False(DrawingEntityTags.TryRead(model[2], out objectId, out viewId));
            Assert.Null(objectId);
            Assert.False(DrawingEntityTags.TryRead(model[3], out _, out _));
        }

        [Fact]
        public void RemovingFromADrawingWithNothingTaggedLeavesItAsItIs()
        {
            var database = new DxfDatabase("AC1027");
            Line(database.ModelSpace, 0);
            string before = database.ToText();

            Assert.Equal(0, DrawingEntityTags.RemoveAll(database));
            Assert.Equal(before, database.ToText());
        }

        [Fact]
        public void TheSavedDrawingOpensInThePresenterWithEveryEntityShownAndTheDrawingsReadOnlyThere()
        {
            DxfDatabase sheet = Sheet();
            DrawingDocumentStore.Write(sheet, DrawingDocumentStoreTests.Beam());

            DxfDatabase opened = SavedAndOpened(sheet);
            CadDocument document = CadDocument.From(opened, "B-101.dxf");
            DisplayList2 model = document.GetDisplay(document.ModelSpace);
            var registry = new CadOwnerRegistry();

            List<DxfEntity> entities = opened.ModelSpace.Cast<DxfEntity>().ToList();
            Assert.Equal(
                entities.Select(e => e.Handle).OrderBy(h => h, System.StringComparer.Ordinal),
                model.Items.Select(i => i.Handle).Distinct().OrderBy(h => h, System.StringComparer.Ordinal)
            );
            Assert.Equal(
                new[]
                {
                    CadEditRule.ReadOnly,
                    CadEditRule.ReadOnly,
                    CadEditRule.Free,
                    CadEditRule.ReadOnly,
                    CadEditRule.ReadOnly,
                },
                entities.Select(registry.GetEditRule)
            );
            Assert.Equal(DrawingDocumentStoreTests.Beam(), DrawingDocumentStore.Read(opened));
        }
    }
}
