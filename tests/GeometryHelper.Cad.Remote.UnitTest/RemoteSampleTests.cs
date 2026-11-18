using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Cad.Remote.Sample;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    /// <summary>The sample program's logic, run against a headless app with the pick answered by the test.</summary>
    public class RemoteSampleTests
    {
        private static async Task WaitForPrompt(TestHost host, CadCommandSession session)
        {
            for (int i = 0; i < 1000; i++)
            {
                if (host.OnAppThread(() => session.IsRunning))
                {
                    return;
                }

                await Task.Delay(10);
            }

            throw new TimeoutException("The sample never asked for a point.");
        }

        [Fact]
        public async Task TheSample_DrawsTheTitleOnItsOwnLayer_AsksForAPoint_AndDrawsTheCircleThere()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument document = host.Add(CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf"));
                CadCommandSession session = host.SessionOf(document);
                harness.Serve(host);
                CadApplication app = harness.Connect();
                var said = new StringWriter();

                Task<int> run = Task.Run(() => RemoteSample.Run(app, said));
                await WaitForPrompt(host, session);

                // The title is in the drawing while the app waits for the point.
                Assert.Equal(3, host.OnAppThread(() => document.Database.ModelSpace.Count()));
                Assert.Equal("Pick the centre of the circle:", host.OnAppThread(() => session.PromptText));
                host.OnAppThread(() => session.Feed(new GeoPoint2(300, 120)));

                Assert.Equal(0, await run);
                DxfEntity[] drawn = document.Database.ModelSpace.ToArray();
                Assert.Equal(4, drawn.Length);
                Assert.All(drawn, e => Assert.Equal(RemoteSample.LayerName, e.Layer));
                Assert.IsType<DxfPolyline>(drawn[0]);
                Assert.True(((DxfPolyline)drawn[0]).Closed);
                Assert.IsType<DxfLine>(drawn[1]);
                Assert.Contains(
                    "GeometryHelper CAD",
                    Assert.IsType<DxfMText>(drawn[2]).Contents,
                    StringComparison.Ordinal
                );
                var circle = Assert.IsType<DxfCircle>(drawn[3]);
                Assert.Equal(300.0, circle.Center.X, 9);
                Assert.Equal(120.0, circle.Center.Y, 9);
                Assert.Equal(RemoteSample.CircleRadius, circle.Radius, 9);
                Assert.Equal(3, document.Database.LayerTable[RemoteSample.LayerName].ColorIndex);

                // The title was one undo step and the circle another.
                Assert.True(document.Undo());
                Assert.Equal(3, document.Database.ModelSpace.Count());
                Assert.True(document.Undo());
                Assert.Empty(document.Database.ModelSpace);
                Assert.Contains("Drew the circle", said.ToString(), StringComparison.Ordinal);
                Assert.Contains("Open: A-101.dxf (active)", said.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task TheSample_WhenTheUserPressesEsc_KeepsTheTitle_AndDrawsNoCircle()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument document = host.Add(CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf"));
                CadCommandSession session = host.SessionOf(document);
                harness.Serve(host);
                CadApplication app = harness.Connect();
                var said = new StringWriter();

                Task<int> run = Task.Run(() => RemoteSample.Run(app, said));
                await WaitForPrompt(host, session);
                host.OnAppThread(() => session.Escape());

                Assert.Equal(1, await run);
                Assert.Equal(3, document.Database.ModelSpace.Count());
                Assert.Contains("cancelled", said.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task TheSample_InAnAppWithNoDrawing_StartsANewOne()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument first = host.Add(CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf"));
                host.Close(first);
                harness.Serve(host);
                CadApplication app = harness.Connect();

                Task<int> run = Task.Run(() => RemoteSample.Run(app, new StringWriter()));

                for (int i = 0; i < 1000 && host.OnAppThread(() => host.Documents.Count) == 0; i++)
                {
                    await Task.Delay(10);
                }

                CadDocument made = host.Documents.Single();
                CadCommandSession session = host.SessionOf(made);
                await WaitForPrompt(host, session);
                host.OnAppThread(() => session.Feed(new GeoPoint2(10, 10)));

                Assert.Equal(0, await run);
                Assert.Equal(4, made.Database.ModelSpace.Count());
            }
        }
    }
}
