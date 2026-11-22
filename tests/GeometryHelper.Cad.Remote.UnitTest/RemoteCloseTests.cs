using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    /// <summary>
    /// Closing a drawing of the app from outside (D1609, D1625): an unmodified one closes; a modified one is refused unless
    /// the caller says discard, and is then closed without saving; one with a command running is refused either way. The
    /// refusals change nothing. The app activates another drawing when the shown one closes.
    /// </summary>
    public class RemoteCloseTests
    {
        private static CadDocument Blank(string name) => CadDocument.From(new DxfDatabase("AC1027"), name);

        private static DxfLine Line(double x1, double y1, double x2, double y2) =>
            new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));

        /// <summary>Draws a line in the drawing from outside, so that the app's drawing is modified.</summary>
        private static void Modify(CadRemoteDocument remote)
        {
            remote.Insert(Line(0, 0, 100, 0));
            remote.CommitChanges();
        }

        [Fact]
        public void AnUnmodifiedDrawing_IsClosed_AndIsGoneFromTheListOfTheAppAndOfTheCaller()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument closing = host.Add(Blank("A-101.dxf"));
                host.Add(Blank("B-202.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();
                CadRemoteDocument remote = app.Documents.Get("A-101.dxf");

                remote.Close();

                Assert.DoesNotContain(closing, host.Documents);
                Assert.Equal(new[] { "B-202.dxf" }, host.Documents.Select(d => d.Title));
                Assert.Equal(new[] { "B-202.dxf" }, app.Documents.Select(d => d.Name));
                Assert.Equal(1, app.Documents.Count);
            }
        }

        [Fact]
        public void ACallOnADrawingClosed_ThrowsTheNoLongerOpenError_EvenAnotherClose()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                host.Add(Blank("A-101.dxf"));
                CadDocument other = host.Add(Blank("B-202.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();
                CadRemoteDocument closed = app.Documents.Get("A-101.dxf");
                closed.Close();

                CadRemoteException read = Assert.Throws<CadRemoteException>(() => closed.GetObjects());
                CadRemoteException again = Assert.Throws<CadRemoteException>(() => closed.Close());

                Assert.Equal("The drawing A-101.dxf is no longer open.", read.Message);
                Assert.Equal("The drawing A-101.dxf is no longer open.", again.Message);
                Assert.Same(other, Assert.Single(host.Documents));
            }
        }

        [Fact]
        public void AModifiedDrawing_IsRefusedWithoutDiscard_AndStaysOpenAndModified()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument document = host.Add(Blank("A-101.dxf"));
                harness.Serve(host);
                CadRemoteDocument remote = harness.Connect().Documents.Single();
                Modify(remote);
                Assert.True(document.IsModified);

                CadRemoteException refused = Assert.Throws<CadRemoteException>(() => remote.Close());

                Assert.Equal("A-101.dxf has changes not saved: save it, or close it with discard.", refused.Message);
                Assert.Same(document, Assert.Single(host.Documents));
                Assert.True(document.IsModified);
                Assert.Single(document.Database.ModelSpace);
                Assert.Single(remote.GetObjects());
            }
        }

        [Fact]
        public void AModifiedDrawing_IsClosedWithDiscard_AndItsFileOnDiskIsUnchanged()
        {
            using (var harness = new Harness())
            {
                string path = Path.Combine(harness.Folder, "plan.dxf");
                Blank("plan.dxf").SaveAs(path);
                byte[] before = File.ReadAllBytes(path);
                var host = new TestHost();
                harness.Serve(host);
                CadApplication app = harness.Connect();
                CadRemoteDocument remote = app.Open(path);
                Modify(remote);
                Assert.True(Assert.Single(host.Documents).IsModified);

                remote.Close(discard: true);

                Assert.Empty(host.Documents);
                Assert.Empty(app.Documents);
                Assert.Equal(before, File.ReadAllBytes(path));
            }
        }

        [Fact]
        public void ADrawingWithACommandRunning_IsRefused_WithOrWithoutDiscard_AndNothingChanges()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument document = host.Add(Blank("A-101.dxf"));
                harness.Serve(host);
                CadRemoteDocument remote = harness.Connect().Documents.Single();
                remote.SendCommand("LINE 0,0", TimeSpan.Zero);
                Assert.True(host.SessionOf(document).IsRunning);

                CadRemoteException plain = Assert.Throws<CadRemoteException>(() => remote.Close());
                CadRemoteException discarded = Assert.Throws<CadRemoteException>(() => remote.Close(discard: true));

                Assert.Contains("busy with the command LINE", plain.Message, StringComparison.Ordinal);
                Assert.Contains("busy with the command LINE", discarded.Message, StringComparison.Ordinal);
                Assert.Same(document, Assert.Single(host.Documents));
                Assert.True(host.SessionOf(document).IsRunning);
            }
        }

        [Fact]
        public void ClosingTheActiveDrawing_MakesAnotherOneActive_AndClosingAnotherLeavesTheActiveOneAlone()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                host.Add(Blank("A-101.dxf"));
                host.Add(Blank("B-202.dxf"));
                host.Add(Blank("C-303.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();
                Assert.Equal("C-303.dxf", app.ActiveDocument.Name);

                app.Documents.Get("A-101.dxf").Close();

                Assert.Equal("C-303.dxf", app.ActiveDocument.Name);

                app.Documents.Get("C-303.dxf").Close();

                Assert.Equal("B-202.dxf", app.ActiveDocument.Name);
                Assert.Equal("B-202.dxf", host.ActiveDocument.Title);
            }
        }

        [Fact]
        public void ClosingTheLastDrawing_LeavesNoneActive_AndTheAppTakesANewOne()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                host.Add(Blank("A-101.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();

                app.ActiveDocument.Close();

                Assert.Null(app.ActiveDocument);
                Assert.Null(host.ActiveDocument);
                Assert.Empty(app.Documents);

                CadRemoteDocument fresh = app.New();

                Assert.Equal("Drawing1.dxf", fresh.Name);
                Assert.True(fresh.IsActive);
            }
        }

        [Fact]
        public void ADrawingClosed_LeavesTheOthersWorking()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                host.Add(Blank("A-101.dxf"));
                CadDocument other = host.Add(Blank("B-202.dxf"));
                harness.Serve(host);
                CadApplication app = harness.Connect();
                app.Documents.Get("A-101.dxf").Close();
                CadRemoteDocument remaining = app.Documents.Get("B-202.dxf");

                remaining.Insert(Line(0, 0, 10, 0));
                remaining.CommitChanges();

                Assert.Single(other.Database.ModelSpace);
            }
        }
    }
}
