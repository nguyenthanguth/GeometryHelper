using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private const int Attempts = 3;
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
        /// Measures an undo against the undo of a MOVE, each attempt on a drawing made anew after a full collection, until one
        /// is within four times the undo of the MOVE and 50 ms or the attempts are used up; the shortest of each over the
        /// attempts is judged, as the best of three runs is elsewhere. An undo that finds its drawing read ahead costs a few
        /// milliseconds (6 for the first undo of a MOVE, 1 for BEDIT), and one that reads it first took 304 ms at this size,
        /// in every attempt; a machine busy enough to hold one undo 50 ms longer than the next, as one beside several builds
        /// does, is not mistaken for that fault.
        /// </summary>
        /// <param name="attempt">One measurement: the milliseconds of the undo of a MOVE, and of the undo under test.</param>
        /// <param name="what">What the second time is of, for the output and the message.</param>
        private void AssertNearTheUndoOfAMove(Func<double[]> attempt, string what)
        {
            double move = double.MaxValue;
            double other = double.MaxValue;
            var runs = new List<string>();

            for (int run = 0; run < Attempts; run++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                double[] times = attempt();
                move = Math.Min(move, times[0]);
                other = Math.Min(other, times[1]);
                string line =
                    "undo of a MOVE "
                    + times[0].ToString("0", CultureInfo.InvariantCulture)
                    + " ms, "
                    + what
                    + " "
                    + times[1].ToString("0", CultureInfo.InvariantCulture)
                    + " ms";
                runs.Add(line);
                _output.WriteLine(line);

                if (other < (4 * move) + 50)
                {
                    return;
                }
            }

            Assert.Fail(
                "Undo "
                    + what
                    + " took "
                    + other.ToString("0", CultureInfo.InvariantCulture)
                    + " ms at its best, that of a MOVE "
                    + move.ToString("0", CultureInfo.InvariantCulture)
                    + " ms at its best ("
                    + string.Join("; ", runs)
                    + ")."
            );
        }

        /// <summary>
        /// Three lines added to a block in the Block Editor and saved make one step, BEDIT; its undo finds the drawing before it
        /// read ahead, as the undo of a MOVE does. Before, the three steps merged into one left the drawing read ahead for the
        /// last of them, and the undo read its drawing then and walked it again to find an entity. The limit is four times the
        /// undo of a MOVE and 50 ms, each at its best of three attempts; the times are written to the test's output.
        /// </summary>
        [Fact]
        public void TheUndoOfABlockSavedInADrawingOfAHundredThousandEntities_FindsItsDrawingReadAhead() =>
            AssertNearTheUndoOfAMove(
                () =>
                {
                    CadDocument document = Drawing(out string handle);
                    Move(document, handle);
                    double move = UndoAndFind(document, handle);

                    EditBlock(document);
                    Assert.True(document.CloseBlockEditor(true).Succeeded);
                    document.GetDisplay(document.ModelSpace);
                    Assert.Equal("BEDIT", document.NextUndo.Name);
                    return new[] { move, UndoAndFind(document, handle) };
                },
                "of BEDIT"
            );

        /// <summary>
        /// A MOVE, then three lines added in the Block Editor and discarded on closing it: the undo of the MOVE finds the drawing
        /// before it read ahead. Before, the drawing read back on discarding left none read ahead. The limit is four times the
        /// undo of a MOVE made alone and 50 ms, each at its best of three attempts; the times are written to the test's output.
        /// </summary>
        [Fact]
        public void TheUndoAfterTheBlockEditorDiscardsItsChanges_FindsItsDrawingReadAhead() =>
            AssertNearTheUndoOfAMove(
                () =>
                {
                    CadDocument document = Drawing(out string handle);
                    Move(document, handle);
                    double move = UndoAndFind(document, handle);

                    Move(document, handle);
                    EditBlock(document);
                    Assert.True(document.CloseBlockEditor(false).Succeeded);
                    document.GetDisplay(document.ModelSpace);
                    Assert.Equal("MOVE", document.NextUndo.Name);
                    return new[] { move, UndoAndFind(document, handle) };
                },
                "after discarding"
            );
    }
}
