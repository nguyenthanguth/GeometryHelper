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
    /// <summary>
    /// The window's drawings seen from another program: a <see cref="CadRemoteServer"/> over the view model, spoken to by the
    /// client over a real pipe. The work is run on the calling thread, as no window pumps messages here.
    /// </summary>
    public sealed class RemoteHostTests
    {
        private sealed class InlineDispatcher : ICadRemoteDispatcher
        {
            public void Invoke(Action action) => action();
        }

        [Fact]
        public void TheWindowsDrawings_AreListedEditedCommandedAndSavedThroughTheRemoteChannel()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                string plan = folder.Write("plan.dxf", new DxfDatabase("AC1027"));
                var model = new MainViewModel();
                Assert.True(model.Open(room));
                Assert.True(model.Open(plan));
                var host = new CadAppRemoteHost(
                    model,
                    () => "plan.dxf - GeometryHelper CAD",
                    Dispatcher.CurrentDispatcher
                );
                string files = Path.Combine(folder.FullPath, "remote");

                using (var server = new CadRemoteServer(host, new InlineDispatcher(), files, "wpf"))
                {
                    server.Start();

                    using (var app = new CadApplication(CadApplication.GetRunning(files).Single()))
                    {
                        Assert.Equal(new[] { "plan.dxf", "room.dxf" }, app.Documents.Select(d => d.Name));
                        Assert.Equal("plan.dxf", app.ActiveDocument.Name);
                        CadRemoteDocument other = app.Documents.Get("room.dxf");
                        int before = other.GetObjects().Count;

                        // An edit lands in the drawing asked for while the other is the one shown.
                        other.Insert(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(500, 0, 0)));
                        other.CommitChanges();
                        Assert.Equal(before + 1, other.GetObjects().Count);
                        Assert.Same(model.Document, host.ActiveDocument);
                        Assert.Equal("plan.dxf", app.ActiveDocument.Name);

                        // A command works on the drawing the user sees, so it is shown first.
                        CadRemoteCommandResult result = other.SendCommand("LINE 0,10 500,10\n\n");

                        Assert.True(result.Completed);
                        Assert.Equal("room.dxf", app.ActiveDocument.Name);
                        Assert.Equal(before + 2, other.GetObjects().Count);
                        Assert.Contains(model.History, l =>
                            l.StartsWith("Command: LINE", StringComparison.OrdinalIgnoreCase)
                        );

                        // Saved under another name, the drawing is listed under it and the old name is free.
                        string renamed = Path.Combine(folder.FullPath, "renamed.dxf");
                        other.SaveAs(renamed);
                        Assert.Equal(renamed, other.Path);
                        Assert.Equal(new[] { "plan.dxf", "renamed.dxf" }, app.Documents.Select(d => d.Name));
                        Assert.Equal(before + 2, DxfDatabase.Open(renamed).ModelSpace.Count());

                        CadRemoteDocument opened = app.Open(room);
                        Assert.Equal("room.dxf", opened.Name);
                        Assert.True(opened.IsActive);
                        Assert.Equal(3, app.Documents.Count);
                        Assert.Contains(
                            "Cannot open",
                            Assert.Throws<CadRemoteException>(() => app.Open(room + ".missing")).Message,
                            StringComparison.Ordinal
                        );
                    }
                }

                Assert.False(Directory.Exists(files) && Directory.GetFiles(files, "*.json").Length > 0);
            }
        }
    }
}
