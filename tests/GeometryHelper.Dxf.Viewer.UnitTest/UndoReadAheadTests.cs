using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Undo and redo reading the drawing of a step ahead on a worker thread: the drawing an undo or a redo gives back is the
    /// one saved at that step, byte for byte, whether its read ahead was done, still running or let go.
    /// </summary>
    public class UndoReadAheadTests
    {
        /// <summary>The drawing of <see cref="BigDrawing"/> with 2 000 entities, an MTEXT and a layer nothing is on, for PURGE.</summary>
        private static CadDocument Drawing(out string mtext)
        {
            DxfDatabase database = DxfDatabase.Parse(BigDrawing.Text(2000));
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Unused" });
            var text = new DxfMText { Contents = "BEAM B-1", Location = new GeoPoint3(10, -40, 0), TextHeight = 2.5, Layer = "Notes" };
            database.ModelSpace.AppendEntity(text);
            mtext = text.Handle;
            return CadDocument.From(DxfDatabase.Parse(database.ToText()), "undo-read-ahead.dxf");
        }

        private static byte[] Saved(CadDocument document)
        {
            var stream = new MemoryStream();
            document.Database.Save(stream);
            return stream.ToArray();
        }

        private static string[] Handles(CadDocument document, int from, int count)
            => document.ModelSpace.Block.Cast<DxfEntity>().Where(e => !e.IsErased).Skip(from).Take(count).Select(e => e.Handle).ToArray();

        /// <summary>
        /// MOVE, ERASE, COPY, a layer changed, a block edited, PURGE, a grip stretched and an MTEXT edited, each saved as it
        /// stands; then every step undone and every step redone, each time the drawing saved and set against the one saved at
        /// that step. With <paramref name="draw"/> model space is drawn after every step, as the canvas draws it.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EightCommandsUndoneAndRedone_GiveBackTheDrawingOfEachStepByteForByte(bool draw)
        {
            CadDocument document = Drawing(out string mtext);
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            var saved = new List<byte[]> { Saved(document) };

            void Step(string name)
            {
                Assert.False(session.IsRunning, name + " still runs: " + session.PromptText);
                Assert.Equal(name, document.NextUndo.Name);
                saved.Add(Saved(document));

                if (draw)
                {
                    document.GetDisplay(document.ModelSpace);
                }
            }

            document.Selection.Set(Handles(document, 0, 20));
            Run(session, "MOVE", P(0, 0), P(15, 7));
            Step("MOVE");

            document.Selection.Set(Handles(document, 100, 300));
            Run(session, "ERASE");
            Step("ERASE");

            document.Selection.Set(Handles(document, 500, 10));
            Run(session, "COPY", P(0, 0), P(0, 9000), Enter);
            Step("COPY");

            Assert.True(CadPropertyEditor.Set(document, Handles(document, 700, 50), "Layer", "Bolts").Succeeded);
            Step("Properties");

            Run(session, "-BEDIT", "B20", "LINE", P(0, 0), P(4, 6), Enter, "BCLOSE", Enter);
            Assert.Null(document.BlockEditor);
            Step("BEDIT");

            Run(session, "-PURGE", "LA", Enter, "N");
            Assert.False(document.Database.LayerTable.Has("Unused"));
            Step("-PURGE");

            string line = document.ModelSpace.Block.Cast<DxfEntity>().First(e => e is DxfLine && !e.IsErased).Handle;
            Assert.True(document.GripDrag(line, 1, P(-50, -60)).Succeeded);
            Step("GRIP");

            Assert.True(CadPropertyEditor.Set(document, new[] { mtext }, "Contents", "BEAM B-12").Succeeded);
            Step("Properties");

            for (int i = saved.Count - 2; i >= 0; i--)
            {
                Assert.True(document.Undo());
                Assert.True(saved[i].SequenceEqual(Saved(document)), "after undoing to step " + i);

                if (draw)
                {
                    document.GetDisplay(document.ModelSpace);
                }
            }

            Assert.False(document.CanUndo);

            for (int i = 1; i < saved.Count; i++)
            {
                Assert.True(document.Redo());
                Assert.True(saved[i].SequenceEqual(Saved(document)), "after redoing to step " + i);

                if (draw)
                {
                    document.GetDisplay(document.ModelSpace);
                }
            }

            Assert.False(document.CanRedo);
        }

        /// <summary>
        /// An undo, then a new edit that forgets the step undone, then undo after undo, and undo and redo back and forth: each
        /// gives back the drawing of its step, the drawings read ahead for steps forgotten let go.
        /// </summary>
        [Fact]
        public void UndoAfterANewEditForgetsTheStepsUndone_GivesBackTheRightDrawings()
        {
            CadDocument document = Drawing(out _);
            string[] handles = Handles(document, 0, 3);
            byte[] start = Saved(document);

            CadEditResult Move(string handle, double dx)
                => document.ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, handle) }, s => s.Document.FindEntity(handle).TransformBy(GeoTransform3.Translation(new GeoVector3(dx, 0, 0))));

            Assert.True(Move(handles[0], 5).Succeeded);
            byte[] first = Saved(document);
            Assert.True(Move(handles[1], 5).Succeeded);
            Assert.True(document.Undo());
            Assert.True(first.SequenceEqual(Saved(document)));

            Assert.True(Move(handles[2], 5).Succeeded);
            byte[] branch = Saved(document);
            Assert.False(document.CanRedo);

            for (int round = 0; round < 3; round++)
            {
                Assert.True(document.Undo());
                Assert.True(first.SequenceEqual(Saved(document)));
                Assert.True(document.Undo());
                Assert.True(start.SequenceEqual(Saved(document)));
                Assert.True(document.Redo());
                Assert.True(first.SequenceEqual(Saved(document)));
                Assert.True(document.Redo());
                Assert.True(branch.SequenceEqual(Saved(document)));
            }

            Assert.NotNull(document.FindEntity(handles[2]));
        }
    }
}
