using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    public class RemoteSpeedTests
    {
        private readonly ITestOutputHelper _output;

        public RemoteSpeedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void AThousandLines_GoInOneCommitInSeconds_AndTheRoundTripOfOneIsInMilliseconds()
        {
            using (var harness = new Harness())
            {
                var host = new TestHost();
                CadDocument document = host.Add(CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf"));
                harness.Serve(host);
                CadRemoteDocument drawing = harness.Connect().Documents.Single();

                // The first request pays for the JIT of both sides: not counted.
                drawing.Insert(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
                drawing.CommitChanges();

                var one = Stopwatch.StartNew();
                drawing.Insert(new DxfLine(new GeoPoint3(0, 1, 0), new GeoPoint3(1, 1, 0)));
                string handle = drawing.CommitChanges().Single();
                one.Stop();

                var many = Stopwatch.StartNew();

                for (int i = 0; i < 1000; i++)
                {
                    drawing.Insert(new DxfLine(new GeoPoint3(i, 0, 0), new GeoPoint3(i, 10, 0)));
                }

                var queued = many.Elapsed;
                var handles = drawing.CommitChanges();
                many.Stop();

                var read = Stopwatch.StartNew();
                var objects = drawing.GetObjects();
                read.Stop();

                _output.WriteLine(
                    "Insert + CommitChanges of 1 line: {0:0.00} ms; of 1000 lines: {1:0.00} ms (queued in {2:0.00} ms); GetObjects of {3}: {4:0.00} ms.",
                    one.Elapsed.TotalMilliseconds,
                    many.Elapsed.TotalMilliseconds,
                    queued.TotalMilliseconds,
                    objects.Count,
                    read.Elapsed.TotalMilliseconds
                );
                Assert.NotNull(handle);
                Assert.Equal(1000, handles.Count);
                Assert.Equal(1002, objects.Count);
                Assert.Equal(1002, document.Database.ModelSpace.Count());
                Assert.True(many.ElapsedMilliseconds < 30000, "1000 lines took " + many.ElapsedMilliseconds + " ms.");
            }
        }
    }
}
