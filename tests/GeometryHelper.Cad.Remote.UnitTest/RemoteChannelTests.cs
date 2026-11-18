using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    public class RemoteChannelTests
    {
        private static CadDocument Blank(string name) => CadDocument.From(new DxfDatabase("AC1027"), name);

        [Fact]
        public void AServerStarted_AnnouncesItselfInTheFolder_AndAClientFindsIt()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost { Title = "A-101.dxf - GeometryHelper CAD" };
                host.Add(Blank("A-101.dxf"));
                host.Add(Blank("B-202.dxf"));
                CadRemoteServer server = harness.Serve(host);

                CadRunningApp app = Assert.Single(CadApplication.GetRunning(harness.Folder));

                Assert.Equal(Process.GetCurrentProcess().Id, app.ProcessId);
                Assert.Equal("A-101.dxf - GeometryHelper CAD", app.Title);
                Assert.Equal(new[] { "A-101.dxf", "B-202.dxf" }, app.Drawings);
                Assert.Equal(server.PipeName, app.Instance.Pipe);
                Assert.StartsWith(
                    "GeometryHelper.Cad." + app.ProcessId + ".",
                    server.PipeName,
                    StringComparison.Ordinal
                );
                Assert.True((DateTime.UtcNow - app.LastActivated).TotalSeconds < 30);
            }
        }

        [Fact]
        public void AServerStopped_TakesItsFileAway_AndNoAppIsRunning()
        {
            using (var harness = new Harness())
            {
                CadRemoteServer server = harness.Serve(new TestHost());
                Assert.Single(Directory.GetFiles(harness.Folder, "*.json"));

                server.Stop();

                Assert.Empty(Directory.GetFiles(harness.Folder, "*.json"));
                Assert.Empty(CadApplication.GetRunning(harness.Folder));
                CadRemoteException refused = Assert.Throws<CadRemoteException>(() => harness.Connect());
                Assert.Contains("No GeometryHelper CAD is running", refused.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheFileOfAProcessThatIsGone_IsIgnoredAndRemoved()
        {
            using (var harness = new Harness())
            {
                string stale = Path.Combine(harness.Folder, "2147480000.json");
                new RemoteInstance
                {
                    ProcessId = 2147480000,
                    Title = "gone",
                    LastActivated = DateTime.UtcNow,
                    Pipe = "GeometryHelper.Cad.2147480000",
                    FilePath = stale,
                }.Write();
                harness.Serve(new TestHost());

                CadRunningApp app = Assert.Single(CadApplication.GetRunning(harness.Folder));

                Assert.Equal(Process.GetCurrentProcess().Id, app.ProcessId);
                Assert.False(File.Exists(stale));
            }
        }

        [Fact]
        public void AFileOfAnotherProtocol_IsLeftAloneAndNotListed()
        {
            using (var harness = new Harness())
            {
                string newer = Path.Combine(harness.Folder, "9999.json");
                File.WriteAllText(newer, "{\"protocol\":99,\"processId\":9999,\"pipe\":\"x\"}");
                harness.Serve(new TestHost());

                Assert.Single(CadApplication.GetRunning(harness.Folder));
                Assert.True(File.Exists(newer));
            }
        }

        [Fact]
        public void TwoServers_AreFoundAndToldApart_ByTheirDrawingsAndTheLastActivated()
        {
            using (var harness = new Harness())
            {
                string folder = Path.Combine(harness.Folder, "files");
                Directory.CreateDirectory(folder);
                string plan = Path.Combine(folder, "plan.dxf");
                string detail = Path.Combine(folder, "detail.dxf");
                Blank("plan.dxf").SaveAs(plan);
                Blank("detail.dxf").SaveAs(detail);
                var first = new TestHost { Title = "plan" };
                first.Add(CadDocument.Open(plan));
                var second = new TestHost { Title = "detail" };
                second.Add(CadDocument.Open(detail));
                CadRemoteServer firstServer = harness.Serve(first);
                Thread.Sleep(30);
                CadRemoteServer secondServer = harness.Serve(second);

                var apps = CadApplication.GetRunning(harness.Folder);

                // The second was activated last, so it is first; both are this process, told apart by their pipes.
                Assert.Equal(new[] { "detail", "plan" }, apps.Select(a => a.Title));
                Assert.Equal(new[] { secondServer.PipeName, firstServer.PipeName }, apps.Select(a => a.Instance.Pipe));
                Assert.Equal(new[] { detail }, apps[0].Drawings);
                Assert.Equal(new[] { plan }, apps[1].Drawings);

                using (CadApplication latest = harness.Connect())
                {
                    Assert.Equal("detail.dxf", Assert.Single(latest.Documents).Name);
                }

                Thread.Sleep(30);
                firstServer.NoteActivated();

                using (CadApplication latest = harness.Connect())
                {
                    Assert.Equal("plan.dxf", Assert.Single(latest.Documents).Name);
                }

                using (CadApplication byFile = CadApplication.ForDocument(detail, harness.Folder))
                {
                    Assert.Equal("detail.dxf", Assert.Single(byFile.Documents).Name);
                    Assert.Equal("detail", byFile.Running.Title);
                }

                CadRemoteException none = Assert.Throws<CadRemoteException>(() =>
                    CadApplication.ForDocument(Path.Combine(folder, "other.dxf"), harness.Folder)
                );
                Assert.Contains("other.dxf open", none.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ThePipe_IsOpenToTheCurrentUserOnly()
        {
            using (var harness = new Harness())
            {
                CadRemoteServer server = harness.Serve(new TestHost());

                using (var raw = new NamedPipeClientStream(".", server.PipeName, PipeDirection.InOut))
                {
                    raw.Connect(2000);
                    var rules = raw.GetAccessControl()
                        .GetAccessRules(true, true, typeof(SecurityIdentifier))
                        .Cast<PipeAccessRule>()
                        .ToList();

                    PipeAccessRule rule = Assert.Single(rules);
                    Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
                    Assert.Equal(WindowsIdentity.GetCurrent().User, rule.IdentityReference);
                    Assert.True(raw.GetAccessControl().AreAccessRulesProtected);
                }
            }
        }

        [Fact]
        public void ASecondServerOfTheSameName_IsRefused_SoNoOtherProcessCanSitOnThePipe()
        {
            using (var harness = new Harness())
            {
                CadRemoteServer server = harness.Serve(new TestHost());
                var twin = new CadRemoteServer(
                    new TestHost(),
                    new InlineDispatcher(),
                    harness.Folder,
                    server.PipeName.Substring(server.PipeName.LastIndexOf('.') + 1)
                );

                Assert.Throws<IOException>(() => twin.Start());
            }
        }

        [Fact]
        public void ARequestThatTheAppDoesNotKnow_IsAnswered_AsAnErrorInTheAppsWords()
        {
            using (var harness = new Harness())
            {
                harness.Serve(new TestHost());
                CadApplication app = harness.Connect();

                CadRemoteException refused = Assert.Throws<CadRemoteException>(() =>
                    app.Connection.Request(new JsonObject().Set("op", "pickPoint"))
                );

                Assert.Contains("does not know the request pickPoint", refused.Message, StringComparison.Ordinal);
                Assert.Empty(app.Documents);
            }
        }

        [Fact]
        public async Task ASecondClient_WaitsForTheRequestOfTheFirst()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                host.Add(Blank("A-101.dxf"));
                harness.Serve(host);
                CadApplication first = harness.Connect();
                CadApplication second = harness.Connect();
                CadRemoteDocument drawing = first.Documents.Single();

                // The first starts LINE and waits for it a second and a half; the second asks for the list meanwhile.
                Task<CadRemoteCommandResult> slow = Task.Run(() =>
                    drawing.SendCommand("LINE 0,0", TimeSpan.FromMilliseconds(1500))
                );
                var clock = Stopwatch.StartNew();

                while (!host.SessionOf(host.Documents[0]).IsRunning && clock.ElapsedMilliseconds < 5000)
                {
                    Thread.Sleep(5);
                }

                var asked = Stopwatch.StartNew();
                int count = second.Documents.Count;
                asked.Stop();

                Assert.Equal(1, count);
                Assert.InRange(asked.ElapsedMilliseconds, 700, 5000);
                Assert.False((await slow).Completed);
            }
        }
    }
}
