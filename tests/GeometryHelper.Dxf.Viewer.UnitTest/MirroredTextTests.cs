using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class MirroredTextTests
    {
        private static DisplayText2 Shown(string blocks, params string[] entities)
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, blocks, entities), "text.dxf");
            return document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();
        }

        private static string MirroredInsert(string block, double x, double y)
            => TestDrawings.Entity("INSERT", "0", 256, "B1", "2", block, "10", x, "20", y, "30", 0.0, "41", -1.0, "42", 1.0, "43", 1.0);

        [Fact]
        public void TextInABlockInsertedMirrored_IsDrawnBackwards_RunningLeftFromItsPoint()
        {
            string block = TestDrawings.Block("NOTE", TestDrawings.Text(0, 0, 5, "ABCD"));

            DisplayText2 text = Shown(block, MirroredInsert("NOTE", 100, 50));

            Assert.True(text.IsBackward);
            Assert.Equal(100, text.X, 12);
            Assert.Equal(0.0, text.Rotation, 12);
            Assert.True(text.Bounds.MaxX <= 100 + 1E-9);
            Assert.True(text.Bounds.MinX < 90);
            Assert.True(text.Bounds.MinY >= 50 - 1E-9);
        }

        [Fact]
        public void ATextWrittenBackwards_IsDrawnBackwards_AndOneUpsideDownIsBackwardsTurnedHalfRound()
        {
            DisplayText2 backward = Shown(null, TestDrawings.Entity("TEXT", "0", 256, "T1", "10", 0.0, "20", 0.0, "30", 0.0, "40", 5.0, "1", "AB", "71", 2));
            DisplayText2 upsideDown = Shown(null, TestDrawings.Entity("TEXT", "0", 256, "T1", "10", 0.0, "20", 0.0, "30", 0.0, "40", 5.0, "1", "AB", "71", 4));

            Assert.True(backward.IsBackward);
            Assert.Equal(0.0, backward.Rotation, 12);
            Assert.True(backward.Bounds.MaxX <= 1E-9);
            Assert.True(upsideDown.IsBackward);
            Assert.Equal(Math.PI, upsideDown.Rotation, 12);
            Assert.True(upsideDown.Bounds.MinX >= -1E-9);
            Assert.True(upsideDown.Bounds.MaxY <= 1E-9);
        }

        [Fact]
        public void TextInABlockInsertedTurned_IsDrawnAsBefore()
        {
            string block = TestDrawings.Block("NOTE", TestDrawings.Text(0, 0, 5, "ABCD"));

            DisplayText2 text = Shown(block, TestDrawings.Insert("NOTE", 100, 50, 2.0, 30.0, handle: "B1"));

            Assert.False(text.IsBackward);
            Assert.Equal(30.0 * Math.PI / 180.0, text.Rotation, 12);
            Assert.Equal(10, text.Height, 12);
        }
    }
}
