using System.Diagnostics;
using System.Threading;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The drawing the next undo goes to read ahead after the Block Editor saves or discards, as after any other step: an undo
    /// that finds its drawing read and indexed costs about what the undo of a MOVE costs, where one that reads it then walks
    /// its 100 000 entities again to find one.
    /// </summary>
    [Collection(ViewerSpeedCollection.Name)]
    public class UndoReadAheadSpeedTests
    {
        private readonly ITestOutputHelper _output;

        public UndoReadAheadSpeedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static CadDocument Drawing(out string handle)
        {
            CadDocument document = CadDocument.From(DxfDatabase.Parse(BigDrawing.Text(100000)), "speed-100k.dxf");
            document.GetDisplay(document.ModelSpace);
            handle = null;

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                handle = entity.Handle;
                break;
            }

            return document;
        }

        private static void Move(CadDocument document, string handle) =>
            Assert.True(
                document
                    .ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, handle) }, s =>
                        s.Document.FindEntity(handle).TransformBy(GeoTransform3.Translation(new GeoVector3(5, 0, 0)))
                    )
                    .Succeeded
            );

        /// <summary>The block B20 opened in the Block Editor and given three lines, each an undo step of the editor.</summary>
        private static void EditBlock(CadDocument document)
        {
            Assert.True(document.OpenBlockEditor("B20").Succeeded);

            for (int i = 0; i < 3; i++)
            {
                double y = i;
                Assert.True(
                    document
                        .ApplyEdit("LINE", null, s =>
                            s.Append(
                                document.BlockEditor.Space,
                                new DxfLine(new GeoPoint3(0, y, 0), new GeoPoint3(4, y + 6, 0))
                            )
                        )
                        .Succeeded
                );
            }
        }

        /// <summary>
        /// The milliseconds of an undo and of finding an entity after it, two seconds after the step, by which the drawing the
        /// undo goes to is read ahead (about 0.3 s at this size).
        /// </summary>
        private static double UndoAndFind(CadDocument document, string handle)
        {
            Thread.Sleep(2000);
            var watch = Stopwatch.StartNew();
            Assert.True(document.Undo());
            Assert.NotNull(document.FindEntity(handle));
            return watch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Three lines added to a block in the Block Editor and saved make one step, BEDIT; its undo finds the drawing before it
        /// read ahead, as the undo of a MOVE does. Before, the three steps merged into one left the drawing read ahead for the
        /// last of them, and the undo read its drawing then and walked it again to find an entity. The limit is four times the
        /// undo of a MOVE and 50 ms; the times are written to the test's output.
        /// </summary>
        [Fact]
        public void TheUndoOfABlockSavedInADrawingOfAHundredThousandEntities_FindsItsDrawingReadAhead()
        {
            CadDocument document = Drawing(out string handle);
            Move(document, handle);
            double move = UndoAndFind(document, handle);

            EditBlock(document);
            Assert.True(document.CloseBlockEditor(true).Succeeded);
            document.GetDisplay(document.ModelSpace);
            Assert.Equal("BEDIT", document.NextUndo.Name);
            double saved = UndoAndFind(document, handle);
            _output.WriteLine("undo of a MOVE " + move.ToString("0") + " ms, of BEDIT " + saved.ToString("0") + " ms");

            Assert.True(saved < 4 * move + 50, "undo of BEDIT " + saved + " ms, of a MOVE " + move + " ms");
        }

        /// <summary>
        /// A MOVE, then three lines added in the Block Editor and discarded on closing it: the undo of the MOVE finds the drawing
        /// before it read ahead. Before, the drawing read back on discarding left none read ahead. The limit is four times the
        /// undo of a MOVE made alone and 50 ms; the times are written to the test's output.
        /// </summary>
        [Fact]
        public void TheUndoAfterTheBlockEditorDiscardsItsChanges_FindsItsDrawingReadAhead()
        {
            CadDocument document = Drawing(out string handle);
            Move(document, handle);
            double move = UndoAndFind(document, handle);

            Move(document, handle);
            EditBlock(document);
            Assert.True(document.CloseBlockEditor(false).Succeeded);
            document.GetDisplay(document.ModelSpace);
            Assert.Equal("MOVE", document.NextUndo.Name);
            double discarded = UndoAndFind(document, handle);
            _output.WriteLine(
                "undo of a MOVE " + move.ToString("0") + " ms, after discarding " + discarded.ToString("0") + " ms"
            );

            Assert.True(
                discarded < 4 * move + 50,
                "undo after discarding " + discarded + " ms, of a MOVE " + move + " ms"
            );
        }
    }
}
