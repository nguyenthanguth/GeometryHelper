using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    /// <summary>
    /// The user's picks: a program asks, the prompt appears on the drawing's command line, and the answer is fed to the
    /// session as the canvas would feed it, from a thread that takes the host's gate as the window's thread would.
    /// </summary>
    public class RemotePickTests
    {
        private static DxfLine Line(double x1, double y1, double x2, double y2) =>
            new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));

        /// <summary>A drawing with two lines, from (0, 0) to (100, 0) and from (0, 50) to (100, 50).</summary>
        private static CadDocument TwoLines(string name)
        {
            var db = new DxfDatabase("AC1027");
            db.ModelSpace.AppendEntity(Line(0, 0, 100, 0));
            db.ModelSpace.AppendEntity(Line(0, 50, 100, 50));
            return CadDocument.From(db, name);
        }

        private sealed class Scene : IDisposable
        {
            public Harness Harness = new Harness();
            public TestHost Host = new TestHost();
            public CadDocument Document;
            public CadCommandSession Session;
            public CadRemoteDocument Drawing;

            public Scene()
            {
                Document = Host.Add(TwoLines("A-101.dxf"));
                Session = Host.SessionOf(Document);
                Harness.Serve(Host);
                Drawing = Harness.Connect().Documents.Single();
            }

            public void Dispose() => Harness.Dispose();

            /// <summary>Waits until the pick has put its command on the session.</summary>
            public async Task WaitForPrompt()
            {
                for (int i = 0; i < 1000; i++)
                {
                    if (Host.OnAppThread(() => Session.IsRunning))
                    {
                        return;
                    }

                    await Task.Delay(10);
                }

                throw new TimeoutException("The pick never asked.");
            }

            public T OnApp<T>(Func<T> function) => Host.OnAppThread(function);

            public void OnApp(Action action) => Host.OnAppThread(action);
        }

        [Fact]
        public async Task APointPicked_IsAnsweredAsTheCanvasWouldAnswerIt_InWorldCoordinates()
        {
            using (var scene = new Scene())
            {
                Task<GeoPoint3> pending = Task.Run(() => scene.Drawing.PickPoint("Pick the corner of the frame"));
                await scene.WaitForPrompt();

                Assert.Equal("Pick the corner of the frame:", scene.OnApp(() => scene.Session.PromptText));
                Assert.Equal(CadPromptKind.Point, scene.OnApp(() => scene.Session.Prompt.Kind));
                Assert.True(scene.OnApp(() => scene.Session.Feed(new GeoPoint2(120.5, 80.25))));
                GeoPoint3 point = await pending;

                Assert.Equal(120.5, point.X, 9);
                Assert.Equal(80.25, point.Y, 9);
                Assert.Equal(0.0, point.Z, 9);
                Assert.False(scene.OnApp(() => scene.Session.IsRunning));
                Assert.False(scene.Document.CanUndo);
            }
        }

        [Fact]
        public async Task APointPickedFromABasePoint_AsksWithARubberBand_AndAPointTypedAnswersIt()
        {
            using (var scene = new Scene())
            {
                Task<GeoPoint3> pending = Task.Run(() =>
                    scene.Drawing.PickPoint("Pick the end", new GeoPoint3(10, 20, 0))
                );
                await scene.WaitForPrompt();

                GeoPoint2? band = scene.OnApp(() => scene.Session.Prompt.BasePoint);
                Assert.Equal(10.0, band.Value.X, 9);
                Assert.Equal(20.0, band.Value.Y, 9);

                // A point typed answers it as one clicked does.
                scene.OnApp(() => scene.Session.Feed("40,25"));
                GeoPoint3 point = await pending;

                Assert.Equal(40.0, point.X, 9);
                Assert.Equal(25.0, point.Y, 9);
            }
        }

        [Fact]
        public async Task AnObjectPicked_IsReadBack_WithItsHandle()
        {
            using (var scene = new Scene())
            {
                DxfEntity second = scene.Document.Database.ModelSpace.Skip(1).First();
                Task<DxfEntity> pending = Task.Run(() => scene.Drawing.PickObject("Select the plate"));
                await scene.WaitForPrompt();

                Assert.Equal("Select the plate:", scene.OnApp(() => scene.Session.PromptText));
                scene.OnApp(() => scene.Session.Feed(new GeoPoint2(50, 50.2)));
                DxfEntity picked = await pending;

                DxfLine line = Assert.IsType<DxfLine>(picked);
                Assert.Equal(50.0, line.StartPoint.Y, 9);
                Assert.Equal(second.Handle, scene.Drawing.HandleOf(picked));
                Assert.False(scene.OnApp(() => scene.Session.IsRunning));
            }
        }

        [Fact]
        public async Task ObjectsPicked_AreReadBackInTheOrderOfTheirHandles_WhenTheUserPressesEnter()
        {
            using (var scene = new Scene())
            {
                List<string> handles = scene.Document.Database.ModelSpace.Select(e => e.Handle).ToList();
                Task<IReadOnlyList<DxfEntity>> pending = Task.Run(() =>
                    scene.Drawing.PickObjects("Select the bars to dimension")
                );
                await scene.WaitForPrompt();

                Assert.Equal("Select the bars to dimension:", scene.OnApp(() => scene.Session.PromptText));

                // The second line is clicked first, the first line after it; Enter ends the selection.
                scene.OnApp(() => scene.Session.Feed(new GeoPoint2(50, 50.2)));
                Assert.True(scene.OnApp(() => scene.Session.IsRunning));
                scene.OnApp(() => scene.Session.Feed(new GeoPoint2(50, 0.2)));
                scene.OnApp(() => scene.Session.Enter());
                IReadOnlyList<DxfEntity> picked = await pending;

                Assert.Equal(2, picked.Count);
                Assert.Equal(handles, picked.Select(e => scene.Drawing.HandleOf(e)).ToList());
                Assert.False(scene.OnApp(() => scene.Session.IsRunning));
            }
        }

        [Fact]
        public async Task EnterWithNothingPicked_GivesNoObjects_AndEnterAtAPointIsACancel()
        {
            using (var scene = new Scene())
            {
                Task<IReadOnlyList<DxfEntity>> objects = Task.Run(() => scene.Drawing.PickObjects("Select objects"));
                await scene.WaitForPrompt();
                scene.OnApp(() => scene.Session.Enter());
                Assert.Empty(await objects);

                Task<GeoPoint3> point = Task.Run(() => scene.Drawing.PickPoint("Pick the corner"));
                await scene.WaitForPrompt();
                scene.OnApp(() => scene.Session.Enter());
                Assert.True((await Assert.ThrowsAsync<CadRemoteException>(() => point)).IsCancelled);
            }
        }

        [Fact]
        public async Task ObjectsTheUserSelectedBefore_AreTakenAtOnce_AsTheCommandsTakeThem()
        {
            using (var scene = new Scene())
            {
                string handle = scene.Document.Database.ModelSpace.First().Handle;
                scene.OnApp(() => scene.Document.Selection.Set(new[] { handle }));

                IReadOnlyList<DxfEntity> picked = await Task.Run(() => scene.Drawing.PickObjects("Select objects"));

                Assert.Equal(handle, scene.Drawing.HandleOf(Assert.Single(picked)));
                Assert.False(scene.OnApp(() => scene.Session.IsRunning));
                Assert.Contains(scene.Session.History, l => l == "1 found");
            }
        }

        [Fact]
        public async Task AnObjectSelectedBefore_IsTheOneObjectPicked_WithoutAsking()
        {
            using (var scene = new Scene())
            {
                string handle = scene.Document.Database.ModelSpace.Skip(1).First().Handle;
                scene.OnApp(() => scene.Document.Selection.Set(new[] { handle }));

                DxfEntity picked = await Task.Run(() => scene.Drawing.PickObject("Select the plate"));

                Assert.Equal(handle, scene.Drawing.HandleOf(picked));
            }
        }

        [Fact]
        public async Task EscapeAtAPick_ThrowsCancelled_AndLeavesTheSessionFree()
        {
            using (var scene = new Scene())
            {
                Task<GeoPoint3> pending = Task.Run(() => scene.Drawing.PickPoint("Pick the corner"));
                await scene.WaitForPrompt();

                scene.OnApp(() => scene.Session.Escape());
                CadRemoteException cancelled = await Assert.ThrowsAsync<CadRemoteException>(() => pending);

                Assert.True(cancelled.IsCancelled);
                Assert.False(scene.OnApp(() => scene.Session.IsRunning));
                Assert.Equal(CadCommandOutcome.Cancelled, scene.OnApp(() => scene.Session.LastOutcome));

                // The channel is free again, and a second pick is answered.
                Task<GeoPoint3> again = Task.Run(() => scene.Drawing.PickPoint("Pick it again"));
                await scene.WaitForPrompt();
                scene.OnApp(() => scene.Session.Feed(new GeoPoint2(1, 2)));
                Assert.Equal(2.0, (await again).Y, 9);
            }
        }

        [Fact]
        public async Task EscapeAtAnObjectPick_ThrowsCancelled()
        {
            using (var scene = new Scene())
            {
                Task<IReadOnlyList<DxfEntity>> pending = Task.Run(() => scene.Drawing.PickObjects("Select objects"));
                await scene.WaitForPrompt();

                scene.OnApp(() => scene.Session.Escape());

                Assert.True((await Assert.ThrowsAsync<CadRemoteException>(() => pending)).IsCancelled);
            }
        }

        [Fact]
        public async Task AnotherProgramDuringAPick_IsToldTheAppIsBusy_AndIsServedOnceItIsDone()
        {
            using (var scene = new Scene())
            {
                CadApplication second = scene.Harness.Connect();
                CadRemoteDocument other = second.Documents.Single();
                Task<GeoPoint3> pending = Task.Run(() => scene.Drawing.PickPoint("Pick the corner"));
                await scene.WaitForPrompt();

                // Neither a request of a program already connected nor a new connection waits for the user.
                var clock = System.Diagnostics.Stopwatch.StartNew();
                CadRemoteException busy = Assert.Throws<CadRemoteException>(() => other.GetObjects());
                Assert.Contains("waiting for the user to pick", busy.Message, StringComparison.Ordinal);
                Assert.False(busy.IsCancelled);
                CadRemoteException late = Assert.Throws<CadRemoteException>(() => scene.Harness.Connect());
                Assert.Contains("waiting for the user to pick", late.Message, StringComparison.Ordinal);
                Assert.InRange(clock.ElapsedMilliseconds, 0, 5000);
                Assert.True(scene.OnApp(() => scene.Session.IsRunning));

                scene.OnApp(() => scene.Session.Feed(new GeoPoint2(5, 5)));
                await pending;

                Assert.Equal(2, other.GetObjects().Count);
                Assert.NotNull(scene.Harness.Connect());
            }
        }

        [Fact]
        public async Task APickThatTheUserDoesNotAnswer_EndsAtTheTimeout_AndTheCommandIsEnded()
        {
            using (var scene = new Scene())
            {
                CadRemoteException late = await Assert.ThrowsAsync<CadRemoteException>(() =>
                    Task.Run(() => scene.Drawing.PickPoint("Pick the corner", null, TimeSpan.FromMilliseconds(300)))
                );

                Assert.False(late.IsCancelled);
                Assert.Contains("did not pick in time", late.Message, StringComparison.Ordinal);
                Assert.False(scene.OnApp(() => scene.Session.IsRunning));
                Assert.Contains(scene.Session.History, l => l.EndsWith("*Cancel*", StringComparison.Ordinal));
            }
        }

        [Fact]
        public void ANegativeTime_IsRefusedAtOnce()
        {
            using (var scene = new Scene())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    scene.Drawing.PickPoint("Pick", null, TimeSpan.FromSeconds(-1))
                );
                Assert.Throws<ArgumentNullException>(() => scene.Drawing.PickObject(null));
            }
        }

        [Fact]
        public void APickWhileACommandRuns_IsRefusedWithTheCommandsName()
        {
            using (var scene = new Scene())
            {
                Assert.False(scene.Drawing.SendCommand("LINE 0,0", TimeSpan.FromMilliseconds(100)).Completed);

                CadRemoteException busy = Assert.Throws<CadRemoteException>(() =>
                    scene.Drawing.PickPoint("Pick the corner")
                );

                Assert.Contains("busy with the command LINE", busy.Message, StringComparison.Ordinal);
                Assert.False(busy.IsCancelled);
                Assert.Equal("LINE", scene.OnApp(() => scene.Session.Current.Name));
            }
        }

        [Fact]
        public async Task APickInADrawingThatIsNotShown_ShowsItFirst()
        {
            using (var scene = new Scene())
            {
                CadDocument shown = scene.Host.Add(TwoLines("B-202.dxf"));
                scene.Host.Activate(scene.Document);
                CadRemoteDocument second = scene.Harness.Connect().Documents.Get("B-202.dxf");
                CadCommandSession session = scene.Host.SessionOf(shown);

                Task<GeoPoint3> pending = Task.Run(() => second.PickPoint("Pick the corner"));

                for (int i = 0; i < 1000 && !scene.Host.OnAppThread(() => session.IsRunning); i++)
                {
                    await Task.Delay(10);
                }

                Assert.Same(shown, scene.Host.ActiveDocument);
                Assert.False(scene.OnApp(() => scene.Session.IsRunning));
                scene.Host.OnAppThread(() => session.Feed(new GeoPoint2(3, 4)));
                Assert.Equal(4.0, (await pending).Y, 9);
            }
        }
    }
}
