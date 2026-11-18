using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    public class RemoteDocumentTests
    {
        private static CadDocument Blank(string name) => CadDocument.From(new DxfDatabase("AC1027"), name);

        private static DxfLine Line(double x1, double y1, double x2, double y2) =>
            new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));

        [Fact]
        public void TwoDrawings_AreListedWithIdsAndTheActiveOneIsMarked()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                host.Add(Blank("A-101.dxf"));
                host.Add(Blank("B-202.dxf"));
                host.Activate(host.Documents[0]);
                harness.Serve(host);
                CadApplication app = harness.Connect();

                var listed = app.Documents.ToList();

                Assert.Equal(new[] { "A-101.dxf", "B-202.dxf" }, listed.Select(d => d.Name));
                Assert.Equal(new[] { 1, 2 }, listed.Select(d => d.Id));
                Assert.Equal(new[] { true, false }, listed.Select(d => d.IsActive));
                Assert.Equal(2, app.Documents.Count);
                Assert.Equal("A-101.dxf", app.ActiveDocument.Name);
                Assert.Same(listed[1], app.Documents.Get("b-202.DXF"));
                Assert.Null(app.Documents.Get("C-303.dxf"));
            }
        }

        [Fact]
        public void TheDrawingEdited_IsTheOneAskedFor_WhileAnotherIsActive()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument first = host.Add(Blank("A-101.dxf"));
                CadDocument second = host.Add(Blank("B-202.dxf"));
                host.Activate(first);
                harness.Serve(host);
                CadApplication app = harness.Connect();
                CadRemoteDocument other = app.Documents.Get("B-202.dxf");

                other.Insert(Line(0, 0, 100, 0));
                other.CommitChanges();

                Assert.Empty(first.Database.ModelSpace);
                Assert.Single(second.Database.ModelSpace);
                Assert.Same(first, host.ActiveDocument);
            }
        }

        [Fact]
        public void ADrawingClosedMeanwhile_IsRefused_AndNeverFallsThroughToAnother()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument first = host.Add(Blank("A-101.dxf"));
                CadDocument second = host.Add(Blank("B-202.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();
                CadRemoteDocument closing = app.Documents.Get("B-202.dxf");
                closing.Insert(Line(0, 0, 10, 0));
                host.Close(second);

                CadRemoteException refused = Assert.Throws<CadRemoteException>(() => closing.CommitChanges());

                Assert.Equal("The drawing B-202.dxf is no longer open.", refused.Message);
                Assert.Equal(
                    "The drawing B-202.dxf is no longer open.",
                    Assert.Throws<CadRemoteException>(() => closing.GetObjects()).Message
                );
                Assert.Empty(first.Database.ModelSpace);
                Assert.Equal(new[] { "A-101.dxf" }, app.Documents.Select(d => d.Name));
            }
        }

        [Fact]
        public void AnIdOfADrawingClosed_IsNotGivenToTheNextOneOpened()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument first = host.Add(Blank("A-101.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();
                int closedId = app.Documents.Single().Id;
                host.Close(first);

                CadRemoteDocument fresh = app.New();

                Assert.NotEqual(closedId, fresh.Id);
                Assert.Equal("Drawing1.dxf", fresh.Name);
            }
        }

        [Fact]
        public void OpenAFileOpenAlready_ReturnsTheDrawingAndDoesNotOpenItTwice()
        {
            using (var harness = new Harness())
            {
                string path = Path.Combine(harness.Folder, "plan.dxf");
                Blank("plan.dxf").SaveAs(path);
                var host = new TestHost();
                host.Add(Blank("B-202.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();

                CadRemoteDocument opened = app.Open(path);
                CadRemoteDocument again = app.Open(path);

                Assert.Same(opened, again);
                Assert.Equal(path, opened.Path);
                Assert.Equal("plan.dxf", opened.Name);
                Assert.True(opened.IsActive);
                Assert.Equal(2, host.Documents.Count);
                Assert.Contains(
                    "Cannot open",
                    Assert.Throws<CadRemoteException>(() => app.Open(path + ".missing")).Message,
                    StringComparison.Ordinal
                );
            }
        }

        [Fact]
        public void ANewDrawingAndActivate_ShowTheDrawingAsTheTabsDo()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument first = host.Add(Blank("A-101.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();

                CadRemoteDocument made = app.New();

                Assert.True(made.IsActive);
                Assert.Equal(2, host.Documents.Count);
                Assert.Equal(string.Empty, made.Path);

                app.Documents.Get("A-101.dxf").Activate();

                Assert.Same(first, host.ActiveDocument);
                Assert.False(app.Documents.Get("Drawing1.dxf").IsActive);
            }
        }

        [Fact]
        public void SaveAs_NamesTheFile_AndSaveWritesItAgain()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument document = host.Add(Blank("Drawing1.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();
                CadRemoteDocument drawing = app.Documents.Single();
                string path = Path.Combine(harness.Folder, "saved.dxf");

                Assert.Contains(
                    "use SaveAs",
                    Assert.Throws<CadRemoteException>(() => drawing.Save()).Message,
                    StringComparison.Ordinal
                );

                drawing.SaveAs(path);

                Assert.True(File.Exists(path));
                Assert.Equal(path, drawing.Path);
                Assert.Equal("saved.dxf", drawing.Name);

                drawing.Insert(Line(0, 0, 5, 5));
                drawing.CommitChanges();
                Assert.True(drawing.IsModified);
                drawing.Save();

                Assert.False(drawing.IsModified);
                Assert.Single(DxfDatabase.Open(path).ModelSpace);
                Assert.False(document.IsModified);
            }
        }
    }
}
