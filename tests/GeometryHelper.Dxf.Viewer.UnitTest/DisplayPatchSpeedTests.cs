using System.Diagnostics;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    [Collection(ViewerSpeedCollection.Name)]
    public class DisplayPatchSpeedTests
    {
        private readonly ITestOutputHelper _output;

        public DisplayPatchSpeedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// A drawing of 100 000 entities drawn whole, then drawn again after one line moved and after the layer of a third of
        /// them switched off and on. Only what changed is drawn again, so each of those costs a fraction of the whole: the limits
        /// are a third of the whole drawing for the move and the layer switched off, and two thirds for the layer switched on,
        /// which draws its third again. On the maintainer's machine the whole takes about 1 s under .NET Framework 4.8, the move
        /// about 40 ms, the layer off 50 ms and on 250 ms; the times are written to the test's output.
        /// </summary>
        [Fact]
        public void AMoveAndALayerSwitchedInADrawingOfAHundredThousandEntities_DrawOnlyWhatChanged()
        {
            CadDocument document = CadDocument.From(DxfDatabase.Parse(BigDrawing.Text(100000)), "speed-100k.dxf");
            document.GetDisplay(document.ModelSpace);
            document.Regen();
            var watch = Stopwatch.StartNew();
            DisplayList2 whole = document.GetDisplay(document.ModelSpace);
            double first = watch.Elapsed.TotalMilliseconds;
            string handle = null;

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                if (entity is DxfLine)
                {
                    handle = entity.Handle;
                    break;
                }
            }

            Assert.True(document.ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, handle) }, s => s.Document.FindEntity(handle).TransformBy(GeoTransform3.Translation(new GeoVector3(5, 0, 0)))).Succeeded);
            watch.Restart();
            DisplayList2 moved = document.GetDisplay(document.ModelSpace);
            double move = watch.Elapsed.TotalMilliseconds;
            Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
            watch.Restart();
            DisplayList2 off = document.GetDisplay(document.ModelSpace);
            double layerOff = watch.Elapsed.TotalMilliseconds;
            Assert.True(CadLayerEditor.SetOn(document, "Steel", true).Succeeded);
            watch.Restart();
            DisplayList2 on = document.GetDisplay(document.ModelSpace);
            double layerOn = watch.Elapsed.TotalMilliseconds;
            _output.WriteLine("whole " + first.ToString("0") + " ms, move " + move.ToString("0") + " ms, layer off " + layerOff.ToString("0") + " ms, layer on " + layerOn.ToString("0") + " ms");

            Assert.Equal(whole.Count, moved.Count);
            Assert.InRange(off.Count, 1, whole.Count * 3 / 4);
            Assert.Equal(whole.Count, on.Count);
            Assert.True(move < first / 3, "move " + move + " ms of " + first);
            Assert.True(layerOff < first / 3, "layer off " + layerOff + " ms of " + first);
            Assert.True(layerOn < first * 2 / 3, "layer on " + layerOn + " ms of " + first);
        }
    }
}
