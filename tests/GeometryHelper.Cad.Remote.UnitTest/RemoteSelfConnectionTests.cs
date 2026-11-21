using System;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    public class RemoteSelfConnectionTests
    {
        private const string Refusal =
            "A script inside GeometryHelper CAD cannot drive the same app through CadApplication: use geometryhelper.cad, which reaches the drawing directly.";

        /// <summary>A dispatcher that owns the thread that made it, as the window's does: the thread a script of the app runs on.</summary>
        private sealed class OwnedDispatcher : ICadRemoteDispatcher, ICadRemoteOwnerThread
        {
            private readonly Thread _owner = Thread.CurrentThread;
            private readonly InlineDispatcher _inline;

            public OwnedDispatcher(object gate) => _inline = new InlineDispatcher(gate);

            public bool IsOwnerThread() => Thread.CurrentThread == _owner;

            public void Invoke(Action action) => _inline.Invoke(action);
        }

        private static TestHost HostWithOneDrawing()
        {
            var host = new TestHost();
            host.Add(CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf"));
            return host;
        }

        [Fact]
        public void ACallerOnTheThreadTheAppOwns_IsRefusedAtOnce_NotLeftWaitingForItself()
        {
            using (var harness = new Harness())
            {
                TestHost host = HostWithOneDrawing();
                harness.Serve(host, new OwnedDispatcher(host.Gate));

                // The test's thread is the app's thread: a request to the drawings from here would wait for itself for ever.
                CadRemoteException refused = Assert.Throws<CadRemoteException>(() => harness.Connect());

                Assert.Equal(Refusal, refused.Message);
            }
        }

        [Fact]
        public async Task ACallerOnAnotherThreadOfTheSameProcess_StillConnects()
        {
            using (var harness = new Harness())
            {
                TestHost host = HostWithOneDrawing();
                harness.Serve(host, new OwnedDispatcher(host.Gate));

                int open = await Task.Run(() => harness.Connect().Documents.Count);

                Assert.Equal(1, open);
            }
        }

        [Fact]
        public void AServerWhoseDispatcherSaysNothingOfItsThread_RefusesNobody()
        {
            using (var harness = new Harness())
            {
                TestHost host = HostWithOneDrawing();
                harness.Serve(host);

                Assert.Equal(1, harness.Connect().Documents.Count);
            }
        }

        [Fact]
        public void AServerStopped_NoLongerRefusesItsOwnThread()
        {
            using (var harness = new Harness())
            {
                TestHost host = HostWithOneDrawing();
                CadRemoteServer server = harness.Serve(host, new OwnedDispatcher(host.Gate));
                CadRunningApp running = Assert.Single(CadApplication.GetRunning(harness.Folder));
                server.Stop();

                // The pipe is gone, so the request fails as any request to a closed app does, with the connection's words.
                CadRemoteException closed = Assert.Throws<CadRemoteException>(() => harness.Connect(running));

                Assert.DoesNotContain("cannot drive the same app", closed.Message, StringComparison.Ordinal);
            }
        }
    }
}
