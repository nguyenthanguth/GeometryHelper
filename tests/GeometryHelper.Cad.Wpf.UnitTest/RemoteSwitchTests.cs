using System;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>"Allow other programs to connect": the setting, its place in Global settings, and the server it starts and stops.</summary>
    public sealed class RemoteSwitchTests
    {
        private sealed class InlineDispatcher : ICadRemoteDispatcher
        {
            public void Invoke(Action action) => action();
        }

        [Fact]
        public void AllowRemoteConnections_IsOnByDefault_AndKeptInTheSettingsFile()
        {
            using (var folder = new TestFolder())
            {
                string path = Path.Combine(folder.FullPath, "settings.json");

                Assert.True(new GlobalSettings().AllowRemoteConnections);
                Assert.True(GlobalSettings.Load(path).AllowRemoteConnections);

                new GlobalSettings { AllowRemoteConnections = false }.Save(path);

                Assert.Contains("\"allowRemoteConnections\": false", File.ReadAllText(path), StringComparison.Ordinal);
                Assert.False(GlobalSettings.Load(path).AllowRemoteConnections);
                Assert.False(GlobalSettings.Load(path).Clone().AllowRemoteConnections);

                // A file of an older version has no such key: the default stands.
                File.WriteAllText(path, "{ \"saveAndExport\": { \"autosaveMinutes\": 3 } }");
                Assert.True(GlobalSettings.Load(path).AllowRemoteConnections);
            }
        }

        [Fact]
        public void GlobalSettings_ShowTheSwitchAndWhoCanConnect_OnTheSaveAndExportPage()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Save and export");

            Assert.Equal(
                new[] { "Allow other programs to connect", "Now" },
                dialog.Sections.Single(s => s.Title == "Other programs").Rows.Select(r => r.Label)
            );
            Assert.Equal(
                GlobalSettingKind.Toggle,
                dialog.Row("Save and export", "Allow other programs to connect").Kind
            );
            Assert.Equal(GlobalSettingKind.Info, dialog.Row("Save and export", "Now").Kind);
            Assert.Equal(model.RemoteStatus, dialog.Row("Save and export", "Now").Value);
        }

        [Fact]
        public void TheSetting_StopsAndStartsTheServer_AndTheInstanceFileGoesAndComesBack()
        {
            using (var folder = new TestFolder())
            {
                string files = Path.Combine(folder.FullPath, "remote");
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                var model = new MainViewModel();
                Assert.True(model.Open(room));
                var host = new CadAppRemoteHost(
                    model,
                    () => "room.dxf - GeometryHelper CAD",
                    Dispatcher.CurrentDispatcher
                );

                using (var remote = new CadRemoteSwitch(model, host, new InlineDispatcher(), files, "sw"))
                {
                    model.SettingsApplied += (s, e) => remote.Apply();

                    // On at the start: the pipe is open, the file is there, and the line says so.
                    remote.Apply();
                    CadRemoteServer first = remote.Server;
                    Assert.True(first.IsRunning);
                    Assert.Single(Directory.GetFiles(files, "*.json"));
                    Assert.Equal("Other programs can connect: " + first.PipeName, model.RemoteStatus);
                    Assert.Single(CadApplication.GetRunning(files));

                    // Applied again as it is, nothing is started twice.
                    model.ApplySettings(new GlobalSettings { AllowRemoteConnections = true });
                    Assert.Same(first, remote.Server);

                    // Off: the server stops, the file goes, and nobody finds the app.
                    model.ApplySettings(new GlobalSettings { AllowRemoteConnections = false });
                    Assert.Null(remote.Server);
                    Assert.False(first.IsRunning);
                    Assert.Empty(Directory.GetFiles(files, "*.json"));
                    Assert.Empty(CadApplication.GetRunning(files));
                    Assert.Equal("Other programs cannot connect.", model.RemoteStatus);

                    // On again: a new server, which a program can use.
                    model.ApplySettings(new GlobalSettings { AllowRemoteConnections = true });
                    CadRemoteServer second = remote.Server;
                    Assert.NotSame(first, second);
                    Assert.True(second.IsRunning);
                    Assert.Single(Directory.GetFiles(files, "*.json"));
                    using (var app = new CadApplication(CadApplication.GetRunning(files).Single()))
                    {
                        Assert.Equal("room.dxf", app.Documents.Single().Name);
                    }
                }

                Assert.Empty(Directory.GetFiles(files, "*.json"));
            }
        }
    }
}
