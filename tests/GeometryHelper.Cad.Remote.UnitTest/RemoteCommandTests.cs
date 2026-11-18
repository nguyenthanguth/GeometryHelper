using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    public class RemoteCommandTests
    {
        private static CadDocument Blank(string name) => CadDocument.From(new DxfDatabase("AC1027"), name);

        [Fact]
        public void ACommandTyped_RunsOnTheDrawingsSession_AndReturnsWhenItEnds()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument document = host.Add(Blank("A-101.dxf"));
                harness.Serve(host);
                CadRemoteDocument drawing = harness.Connect().Documents.Single();

                CadRemoteCommandResult result = drawing.SendCommand("LINE 0,0 100,0\n\n");

                Assert.True(result.Completed);
                DxfLine line = Assert.IsType<DxfLine>(Assert.Single(document.Database.ModelSpace));
                Assert.Equal(100.0, line.EndPoint.X, 9);
                Assert.Contains(result.Output, l => l.IndexOf("LINE", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.Equal("LINE", document.NextUndo.Name);
            }
        }

        [Fact]
        public void ACommandThatWaitsForTheUser_ReturnsAtTheTimeout_WithItsPromptAndStaysRunning()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument document = host.Add(Blank("A-101.dxf"));
                harness.Serve(host);
                CadRemoteDocument drawing = harness.Connect().Documents.Single();
                var clock = Stopwatch.StartNew();

                CadRemoteCommandResult result = drawing.SendCommand("LINE 0,0", TimeSpan.FromMilliseconds(300));

                Assert.False(result.Completed);
                Assert.InRange(clock.ElapsedMilliseconds, 250, 5000);
                Assert.Contains(result.Output, l => l.IndexOf("next point", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.True(host.SessionOf(document).IsRunning);

                // Typed again, the rest of the line is the command's answer; Enter ends it.
                CadRemoteCommandResult rest = drawing.SendCommand("50,0\n\n");

                Assert.True(rest.Completed);
                Assert.Equal(50.0, ((DxfLine)document.Database.ModelSpace.Single()).EndPoint.X, 9);
            }
        }

        [Fact]
        public void ACommandForADrawingThatIsNotActive_ShowsItFirst_AndRunsOnIt()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument first = host.Add(Blank("A-101.dxf"));
                CadDocument second = host.Add(Blank("B-202.dxf"));
                host.Activate(first);
                harness.Serve(host);
                CadRemoteDocument other = harness.Connect().Documents.Get("B-202.dxf");

                other.SendCommand("LINE 1,1 2,2\n\n");

                Assert.Same(second, host.ActiveDocument);
                Assert.Single(second.Database.ModelSpace);
                Assert.Empty(first.Database.ModelSpace);
            }
        }

        [Fact]
        public void AnUnknownCommand_IsAnsweredOnTheCommandLine_AndNothingIsLeftRunning()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                host.Add(Blank("A-101.dxf"));
                harness.Serve(host);
                CadRemoteDocument drawing = harness.Connect().Documents.Single();

                CadRemoteCommandResult result = drawing.SendCommand("NOSUCHCOMMAND");

                Assert.True(result.Completed);
                Assert.NotEmpty(result.Output);
                Assert.Throws<ArgumentNullException>(() => drawing.SendCommand(null));
                Assert.Throws<ArgumentOutOfRangeException>(() => drawing.SendCommand("LINE", TimeSpan.FromSeconds(-1)));
            }
        }
    }
}
