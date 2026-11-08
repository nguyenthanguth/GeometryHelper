using System.Diagnostics;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingSpeedTests
    {
        private readonly ITestOutputHelper _output;

        public EditingSpeedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// One entity of a drawing of 100 000 moved, the move undone and redone. Each edit keeps the drawing as it then stands
        /// (saved and compressed), and an undo reads it back and draws model space again, so on a drawing this large an edit
        /// and an undo cost what saving and opening it cost. The limits are generous (an edit 5 s, an undo or a redo 15 s,
        /// with its display list made again) and guard against a fall that is not noise; the times are written to the test's
        /// output.
        /// </summary>
        [Fact]
        public void AMoveInADrawingOfAHundredThousandEntities_IsMadeUndoneAndRedoneInSeconds()
        {
            CadDocument document = CadDocument.From(DxfDatabase.Parse(BigDrawing.Text(100000)), "speed-100k.dxf");
            document.GetDisplay(document.ModelSpace);
            string handle = null;

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                handle = entity.Handle;
                break;
            }

            var watch = Stopwatch.StartNew();
            CadEditResult first = document.ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, handle) }, s => s.Document.FindEntity(handle).TransformBy(GeoTransform3.Translation(new GeoVector3(5, 0, 0))));
            double firstEdit = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            CadEditResult second = document.ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, handle) }, s => s.Document.FindEntity(handle).TransformBy(GeoTransform3.Translation(new GeoVector3(5, 0, 0))));
            double edit = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            document.Undo();
            document.GetDisplay(document.ModelSpace);
            double undo = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            document.Redo();
            document.GetDisplay(document.ModelSpace);
            double redo = watch.Elapsed.TotalMilliseconds;
            _output.WriteLine("first edit " + firstEdit.ToString("0") + " ms, edit " + edit.ToString("0") + " ms, undo " + undo.ToString("0") + " ms, redo " + redo.ToString("0") + " ms");

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.True(firstEdit < 5000, "first edit " + firstEdit + " ms");
            Assert.True(edit < 5000, "edit " + edit + " ms");
            Assert.True(undo < 15000, "undo " + undo + " ms");
            Assert.True(redo < 15000, "redo " + redo + " ms");
        }
    }
}
