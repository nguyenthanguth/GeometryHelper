using System;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>A drawing of the window closed from another program: no question, no dialog, nothing saved.</summary>
    public sealed class PyLoadBRemoteCloseTests
    {
        private sealed class InlineDispatcher : ICadRemoteDispatcher
        {
            public void Invoke(Action action) => action();
        }

        [Fact]
        public void ADrawingWithChanges_IsClosedWithDiscard_WithoutAskingAndWithoutSaving_AndTheNextOneIsShown()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                string plan = folder.Write("plan.dxf", new DxfDatabase("AC1027"));
                var model = new MainViewModel();
                Assert.True(model.Open(room));
                Assert.True(model.Open(plan));
                int asked = 0;
                model.AskSaveChanges = document =>
                {
                    asked++;
                    return CadSaveChoice.Cancel;
                };
                var host = new CadAppRemoteHost(model, () => "GeometryHelper CAD", Dispatcher.CurrentDispatcher);
                string files = Path.Combine(folder.FullPath, "remote");

                using (var server = new CadRemoteServer(host, new InlineDispatcher(), files, "wpf"))
                {
                    server.Start();

                    using (var app = new CadApplication(CadApplication.GetRunning(files).Single()))
                    {
                        CadRemoteDocument shown = app.Documents.Get("plan.dxf");
                        shown.Insert(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(500, 0, 0)));
                        shown.CommitChanges();
                        Assert.True(model.Document.IsModified);

                        CadRemoteException refused = Assert.Throws<CadRemoteException>(() => shown.Close());
                        Assert.Equal(
                            "plan.dxf has changes not saved: save it, or close it with discard.",
                            refused.Message
                        );
                        Assert.Equal("plan.dxf", model.Document.Title);

                        shown.Close(true);

                        Assert.Equal(0, asked);
                        Assert.Equal("room.dxf", model.Document.Title);
                        Assert.Equal(new[] { "room.dxf" }, app.Documents.Select(d => d.Name));
                        Assert.Empty(DxfDatabase.Open(plan).ModelSpace);
                        Assert.Throws<CadRemoteException>(() => shown.Activate());

                        app.Documents.Get("room.dxf").Close();

                        Assert.False(model.HasDocument);
                        Assert.Equal(0, app.Documents.Count);
                    }
                }
            }
        }
    }
}
